<script setup lang="ts">
/**
 * ★ 标准文档填写规则（`MENU_00218` → `/business/doc-fill-rule`）
 *
 * ────────────────────────────────────────────────────────────────
 * 【七步闭环】（`37` 号 §7.1）
 *   ① 左树选文档 → ② [下载标准文档] → ③ 本地 Word/Excel 加 `{{标签}}`
 *   → ④ [上传空白模板] → ⑤ [重新扫描] → ⑥ [校验] → ⑦ [发布]
 * ────────────────────────────────────────────────────────────────
 * 【三条硬约束（用户原话，逐字）】
 *   H-1「只有选择一个文档后，可以下载、并上传空白格式的文档」
 *       ⇒ 未选中模板叶子时，操作条**全部禁用**。
 *   H-2「如果没有空白格式的文档，不能设置规则」
 *       ⇒ 无空白模板 ⇒ 右栏规则页签只读/禁用 + 明确引导（后端 save 也会拒）。
 *   H-3「点击分析，带出定义的字段和表格」
 *       ⇒ 扫描是**零 LLM 的确定性解析**（模板自己声明了要填什么），⛔ 不是 AI 推断。
 *
 * 【★ 本页与上一版的根本区别】
 *   上一版把锚点当**手工 CRUD 数据**（弹窗新建/编辑）。这是方向性错误：
 *   锚点是从空白模板里**扫描出来的**，手工新建的行运行期永远匹配不到。
 *   ⇒ 本版：锚点只读 + 点行开**侧边栏**配属性；新增锚点唯一路径是
 *     「改模板 → 重新上传 → 重新扫描」。
 *
 * 【按 `DocCategory` 分流】（`37` 号 §3.6）
 *   `editable` → 组 E：锚点与字段规则 / 全文填写规则 / 文档契约 / 校验结果
 *   `fixed`    → 组 F：指纹规则 / 文档契约（上传、扫描按钮禁用）
 *
 * 【⚠️ 已知能力边界（⛔ 不假装已实现）】
 *   ① 「自动分析」里的**语义分析**（分类/作用/标签）后端返回 `not_wired` —— 可先在契约页手工填；
 *   ② `fixed` 文档的**指纹规则编辑器**（组 F）尚未实现，页签以占位说明呈现；
 *   ③ 中栏预览**按模板状态切换**（`components/PreviewPane.vue`）：
 *      未上传模板 → 资料清单**原始文档**的 PDF；已上传 → **空白模板**的 PDF。
 *      两条预览链见 `DocPreview.vue` 的 `fetchPreviewBlob()`。
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import {
  Download,
  Upload,
  MagicStick,
  Search,
  CircleCheck,
  Promotion,
  Refresh,
  WarningFilled,
} from '@element-plus/icons-vue'
import {
  YzhTreeTableLayout,
  YzhPageLayout,
  YzhStatusBadge,
  useTreeTable,
  unwrapOk,
  confirmOrFalse,
  type TreeNode,
} from '@yzh-core'
import { downloadFile } from '@share/composables/useDirectoryApi'
import {
  getDocContract,
  uploadDocTemplate,
  scanTemplateAnchors,
  publishTemplate,
  analyzeDocForFill,
  type DocContractDetail,
} from '@share/api/workflow/doc-fill-rule'
import { DocFillRuleLogic } from './logic'
import PreviewPane from './components/PreviewPane.vue'
import PromptPanel from './components/PromptPanel.vue'
import AnchorRuleTab from './components/AnchorRuleTab.vue'
import ContractTab from './components/ContractTab.vue'
import ValidateTab from './components/ValidateTab.vue'

const { logic } = useTreeTable(DocFillRuleLogic)

/* ============ 子组件引用 ============ */
const anchorTabRef = ref<any>(null)
const validateTabRef = ref<any>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)

// ⚠️ 用**显式函数 ref**，不用模板里的 `ref="anchorTabRef"`：
//   ① 若将来把页签改回 `v-for`，字符串 ref 会被 Vue 收集成**数组**，`ref.value?.refresh()` 静默失效；
//   ② 显式 setter 让「谁挂上来的」一眼可见。
function setAnchorTabRef(el: any) {
  anchorTabRef.value = el
}
function setValidateTabRef(el: any) {
  validateTabRef.value = el
}

/* ============ 页面状态 ============ */
const activeTab = ref('anchor')

const contract = ref<DocContractDetail | null>(null)
const contractLoading = ref(false)

const uploading = ref(false)
const scanning = ref(false)
const validating = ref(false)
const publishing = ref(false)
const analyzing = ref(false)
const downloading = ref(false)
const treeReloading = ref(false)

/**
 * ★ 能否发布 —— 由 `ValidateTab` 校验后回传（口径唯一在后端 `CanPublish`）。
 *   ⛔ 父页不复算：否则「后端说不能发、前端按钮却能点」或反之。
 *   `blockReason` 用于按钮的悬停说明，让用户知道**为什么**点不了。
 */
const canPublish = ref(false)
const publishBlockReason = ref('')

/**
 * ★ 引导文案里的标签样例 —— 必须在脚本里写成常量。
 *
 * ⚠️ 若直接在模板里写 `{{ '{{标签}}' }}`，Vue 的插值 tokenizer 会在**第一个 `}}`** 处收尾，
 * 表达式退化成 `'{{标签` ⇒ `vite build` 报 `Unterminated string constant`
 * （而 `vue-tsc` **不报**，只有真实构建才发现）。
 */
const tokenSample = '{{标签}}'

/** 锚点/扫描/模板发生变化 ⇒ 上一次的校验结论作废，发布按钮回到禁用 */
function invalidatePublishability() {
  canPublish.value = false
  publishBlockReason.value = '锚点已变更，请重新「校验」'
}

function onValidated(p: { canPublish: boolean; blockReason: string }) {
  canPublish.value = p.canPublish
  publishBlockReason.value = p.blockReason || ''
}

/* ============ 派生 ============ */
/**
 * ★★ 两级前置条件 —— 页面所有按钮都按这两级判定，⛔ 不要混成一个。
 *
 * | 级别 | 含义 | 放行的动作 |
 * |---|---|---|
 * | `hasFile` | 选中了**资料清单里的文件** | 下载标准文档、上传空白模板 |
 * | `hasTemplate` | 该文件**已上传空白模板** | 重新扫描、校验、发布 |
 *
 * 旧实现只有一个 `hasTemplate`（实为「选中了节点」）⇒ 没上传模板的文件也能点
 * 「扫描 / 校验 / 发布」，只能吃后端报错。
 */
const hasFile = computed(() => logic.anySelected)
const hasTemplate = computed(() => logic.hasTemplate)
const isFixedDoc = computed(() => logic.isFixedDoc)
const scanStatus = computed(() => logic.scanStatus)
const publishStatus = computed(() => logic.publishStatus)
const templateCode = computed(() => logic.templateCode)

/**
 * ★ `[发布]` 的禁用口径（一处收口，⛔ 不再散落在模板里）。
 *
 * - `fixed`（免填）：其前置是**指纹规则**，该页签尚未实现 ⇒ 暂不允许发布（保持既有行为）。
 * - `editable`：必须**校验通过**（`CanPublish`）。⛔ 不能只判 `isFixedDoc` ——
 *   否则 0 锚点 / 有红牌时按钮仍可点，只能吃后端报错。
 */
const publishDisabled = computed(() => {
  if (!hasTemplate.value) return true
  if (isFixedDoc.value) return true
  return !canPublish.value
})

const publishTitle = computed(() => {
  if (!hasFile.value) return '请先在左侧选择文件'
  if (!hasTemplate.value) return '该文件还没有空白模板，请先「下载可编辑版」→ 加工 → 「上传空白模板」'
  if (isFixedDoc.value) return '固定格式文档的「指纹规则」尚未实现，暂不支持发布'
  if (canPublish.value) {
    return publishStatus.value === 'published'
      ? '该模板已发布；改过规则后可再次发布以刷新'
      : '发布后该标准文档可用此模板生成'
  }
  return publishBlockReason.value || '请先点「校验」，通过后才能发布'
})

/* ============ 派生：路径 ============ */
/**
 * ★★ 下载/预览有**两条**路径，⛔ 不能合成一个（这是本页最早的坑）。
 *
 * | 路径 | 字段 | 内容 | 用途 |
 * |---|---|---|---|
 * | 原始件 | `StoragePath` | `.doc` / `.xls`（143/168 + 11/168） | 存档、比对、兜底预览 |
 * | 归一产物 | `EditableStoragePath` | `.docx` / `.xlsx`（`…/editable/x.doc.docx`） | **加工成空白模板的正确起点** |
 *
 * 【为什么必须默认下归一产物】
 *   用户第 ② 步是「下载 → 本地加工成空白模板」。原始件里绝大多数是 `.doc`/`.xls`，
 *   而填写引擎只认 `.docx`/`.xlsx`；NPOI 2.7.2 又没有 `NPOI.HWPF` ⇒ `.doc` 连读都读不了。
 *   归一产物**已经生成好了**（实测 `ConvertStatus=completed`），下载原始件等于让用户先自己转一次。
 *
 * ★ 树里已带 `standardStoragePath` / `standardEditablePath` ⇒ 选中即可下载，
 *   不必等 `contract` 请求回来（第 ② 步不该被网络延迟卡住）；契约同名字段作兜底。
 */
const originalPath = computed(
  () =>
    logic.standardStoragePath ||
    contract.value?.StandardStoragePath ||
    contract.value?.StandardConvertedPath ||
    '',
)

const editablePath = computed(
  () => logic.standardEditablePath || contract.value?.StandardEditablePath || '',
)

const canDownloadOriginal = computed(() => !!originalPath.value)
const canDownloadEditable = computed(() => !!editablePath.value)

/** 主按钮默认目标：有归一产物就下它 */
const defaultDownloadKind = computed<'editable' | 'original'>(() =>
  canDownloadEditable.value ? 'editable' : 'original',
)

/**
 * 原始文件名（契约优先，其次树节点的**干净名**）—— 下载文件名与预览标题都用它。
 *
 * ⛔ 兜底**不能用** `logic.fileNode?.Name`：那是**带左树徽标的显示名**
 *   （`附录一 …识别图.doc  ⬜未上传模板`）。2026-10-04 实测缺陷：
 *   契约接口回来之前，中栏拿这个当文件名 ⇒ 扩展名被推成 `doc  ⬜未上传模板`
 *   ⇒ 不在 Office 白名单 ⇒ 报「暂不支持在线预览」。用 `logic.fileName`
 *   （读 `Extra.rawName`，后端下发的原名）就干净。
 */
const baseFileName = computed(
  () => contract.value?.FileName || logic.fileName || '标准文档',
)

/** 提示词命名基底：去掉扩展名（`附录一 …识别图.doc` → `附录一 …识别图`），名字更干净 */
const promptBaseName = computed(() => baseFileName.value.replace(/\.[A-Za-z0-9]{1,8}$/, ''))

const templatePath = computed(() => logic.templateStoragePath)

/** 空白模板文件名（`preview-by-path` 靠它判扩展名，⛔ 不能只给路径） */
const templateFileName = computed(() => logic.templateFileName)

/**
 * ★ 中栏预览的文件对象由 `PreviewPane` 自己组装（2026-10-04 起）。
 *
 * 【为什么不在这里算】
 *   「该看原始文档还是空白模板」是**页面策略**，且切换、重载、下载文案三件事
 *   必须**同时**变 —— 分散在父页 + 子组件两处，改一处漏一处是必然的。
 *   ⇒ 策略全部收口到 `components/PreviewPane.vue`，本页只负责把**事实**（路径、状态）传下去。
 *
 * 传下去的四个事实：`fileCode` / `fileName` / `originalPath` / `templatePath` / `templateFileName` / `hasTemplate`。
 */
const previewPaneRef = ref<any>(null)

/** 发布状态 → 徽标 */
const publishMeta = computed(() => {
  const map: Record<string, { text: string; type: any }> = {
    draft: { text: '草稿', type: 'info' },
    scanned: { text: '已扫描', type: 'warning' },
    ready: { text: '可发布', type: 'primary' },
    published: { text: '已发布', type: 'success' },
  }
  return map[publishStatus.value] || { text: publishStatus.value || '—', type: 'info' }
})

/* ============ 选中节点 → 载入契约 ============ */
async function handleNodeClick(node: TreeNode) {
  invalidatePublishability()
  await logic.onNodeClick(node)
  await loadContract()
}

async function loadContract() {
  const fileCode = logic.standardFileCode
  if (!fileCode) {
    contract.value = null
    return
  }
  contractLoading.value = true
  try {
    contract.value = unwrapOk(await getDocContract(fileCode), '加载文档契约失败') ?? null
  } catch (e: any) {
    contract.value = null
    ElMessage.error(e?.message || '加载文档契约失败')
  } finally {
    contractLoading.value = false
  }
}

/**
 * 切模板：回到该分类下的第一个页签。
 *
 * ⚠️ 这里**不**调 `anchorTabRef.refresh()` —— 锚点页签自己 watch 了 `templateCode`
 * 并会重载。两处都调会重复请求一次（且更难判断「到底谁在加载」）。
 */
watch(
  templateCode,
  () => {
    activeTab.value = isFixedDoc.value ? 'fingerprint' : 'anchor'
  },
  { flush: 'post' },
)

/* ============ ② 下载标准文档 ============ */
/**
 * 下载文件名 —— **扩展名必须跟随「实际下载的那个产物」**。
 *
 * ⚠️ 归一产物保留了**完整原名**再追加目标扩展名：`陪审人员.doc` → `陪审人员.doc.docx`。
 * 若沿用原始文件名 `陪审人员.doc` 去存 docx 内容，用户双击会直接报「文件已损坏」——
 * 这类静默错配比下载失败更难查。
 */
function downloadFileName(kind: 'editable' | 'original', path: string): string {
  const base = baseFileName.value
  if (kind === 'original') return base
  const targetExt = path.split('.').pop()?.toLowerCase() || ''
  const baseExt = base.split('.').pop()?.toLowerCase() || ''
  if (!targetExt || targetExt === baseExt) return base
  return `${base.replace(/\.[^.]+$/, '')}.${targetExt}`
}

async function onDownloadStandard(kind: 'editable' | 'original' = defaultDownloadKind.value) {
  const path = kind === 'editable' ? editablePath.value : originalPath.value
  if (!path) {
    ElMessage.warning(
      kind === 'editable'
        ? '该文档还没有可编辑版（归一产物未生成），请改下「原始件」'
        : '该标准文档没有可下载的存储路径',
    )
    return
  }
  downloading.value = true
  try {
    const blob = await downloadFile(path)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = downloadFileName(kind, path)
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (e: any) {
    ElMessage.error('下载失败：' + (e?.message || ''))
  } finally {
    downloading.value = false
  }
}

/* ============ ④ 上传空白模板 ============ */
function pickTemplateFile() {
  if (!logic.standardFileCode) {
    ElMessage.warning('该模板没有关联标准文件，无法上传')
    return
  }
  fileInputRef.value?.click()
}

async function onTemplateFilePicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = '' // 允许重复选同一个文件
  if (!file) return

  uploading.value = true
  try {
    unwrapOk(await uploadDocTemplate(file, logic.standardFileCode), '上传空白模板失败')
    ElMessage.success('空白模板上传成功，正在扫描锚点…')
    invalidatePublishability()
    await logic.reloadTree()
    await loadContract()
    // ★★ 上传后中栏必须**切到并重载**空白模板（用户 2026-10-04 原话：
    //    「如果我们上传了空白文档，我们应该**刷新**查看空白文档的 pdf」）。
    //    ⛔ 不能只靠 PreviewPane 内部的 watch：换版（重新上传同名模板）时
    //      `hasTemplate` 已是 true、`templatePath` 也没变 ⇒ watch 不触发
    //      ⇒ 用户会一直看着**上一版**的 PDF，还以为上传没生效。
    previewPaneRef.value?.showTemplate()
    // ★ 上传后锚点必然是空的 —— 后端返回 NeedScan=true，这里直接续跑第 ⑤ 步
    await onRescan(true)
  } catch (err: any) {
    ElMessage.error(err?.message || '上传失败')
  } finally {
    uploading.value = false
  }
}

/* ============ ⑤ 重新扫描 ============ */
async function onRescan(silent = false) {
  if (!templateCode.value) return
  if (!templatePath.value) {
    ElMessage.warning('请先「上传空白模板」—— 锚点是从模板里扫出来的')
    return
  }
  scanning.value = true
  try {
    const res = await scanTemplateAnchors(templateCode.value, true)
    const data = unwrapOk(res, '扫描失败')
    if (data?.Skipped) {
      if (!silent) ElMessage.info('模板未变化，已跳过重扫')
    } else {
      ElMessage.success(
        `扫描完成：识别到 ${data?.Total ?? 0} 个锚点（新增 ${data?.Inserted ?? 0} / 更新 ${data?.Updated ?? 0} / 孤儿 ${data?.Orphaned ?? 0}）`,
      )
    }
    await logic.reloadTree()
    await anchorTabRef.value?.refresh?.()
    // 锚点集变了 ⇒ 上一次「可发布」结论作废
    invalidatePublishability()
  } catch (e: any) {
    ElMessage.error(e?.message || '扫描失败')
  } finally {
    scanning.value = false
  }
}

/* ============ ⑥ 校验 ============ */
async function onValidate() {
  if (!templateCode.value) return
  activeTab.value = 'validate'
  validating.value = true
  try {
    await validateTabRef.value?.refresh?.()
    await logic.reloadTree()
  } finally {
    validating.value = false
  }
}

/* ============ ⑦ 发布 ============ */
async function onPublish() {
  if (!templateCode.value) return
  const ok = await confirmOrFalse(
    '发布后该标准文档将用此模板生成。\n\n发布前系统会重新校验一遍 —— 有红牌则拒绝发布。确定继续吗？',
    '发布模板',
    { type: 'warning', confirmButtonText: '发布', cancelButtonText: '取消' },
  )
  if (!ok) return

  publishing.value = true
  try {
    unwrapOk(await publishTemplate(templateCode.value), '发布失败')
    ElMessage.success('模板已发布')
    // 已发布 ⇒ 结论作废（发布按钮不该再是可点的「可发布」态）
    invalidatePublishability()
    await logic.reloadTree()
    // 同步刷新校验页签，让它显示「已发布」
    await validateTabRef.value?.refresh?.()
  } catch (e: any) {
    ElMessage.error(e?.message || '发布失败')
  } finally {
    publishing.value = false
  }
}

/* ============ 自动分析 ============ */
async function onAnalyze() {
  const fileCode = logic.standardFileCode
  if (!fileCode) {
    ElMessage.warning('该模板没有关联标准文件')
    return
  }
  analyzing.value = true
  try {
    const data = unwrapOk(await analyzeDocForFill(fileCode), '自动分析失败')
    const parts: string[] = []
    if (data?.Field?.Status === 'ok') {
      parts.push(`字段 ${data.Field.FieldCount} 个 / 表格 ${data.Field.TableCount} 个`)
    } else if (data?.Field?.Status === 'empty') {
      parts.push('字段提取：无结果')
    } else if (data?.Field?.Status === 'failed') {
      parts.push(`字段提取失败：${data.Field.Message}`)
    }
    if (data?.Scan?.Status === 'blocked') parts.push('锚点扫描：需先上传空白模板')
    else if (data?.Scan?.Status === 'not_wired') parts.push('锚点扫描：请点「重新扫描」')

    ElMessage.success(parts.length ? `分析完成 —— ${parts.join('；')}` : '分析完成')
    await loadContract()
  } catch (e: any) {
    ElMessage.error(e?.message || '自动分析失败')
  } finally {
    analyzing.value = false
  }
}

/* ============ 其他 ============ */
/** 手动刷新左树（资料清单可能在别的页面被改过 —— 上传/删除文件夹或文件） */
async function onReloadTree() {
  treeReloading.value = true
  try {
    await logic.reloadTree()
    await loadContract()
  } finally {
    treeReloading.value = false
  }
}

async function onContractSaved() {
  await loadContract()
  await logic.reloadTree()
}

/** 锚点侧边栏保存成功 ⇒ 刷新左树徽标，并作废上一次的「可发布」结论 */
async function onAnchorSaved() {
  invalidatePublishability()
  await logic.reloadTree()
}

/**
 * 提示词挂接 / 解绑成功。
 *
 * ★ 必须 `reloadTree()`：`logic.promptCode` 是从**树节点的 `Extra.fillPromptCode`** 读的，
 * 而 `Extra` 是后端 `directory-tree` 下发的快照 —— 不重载树，界面上的「已挂接」
 * 与库里的 `cert_doc_template.FillPromptCode` 就会不一致。
 */
async function onPromptBound(_promptCode: string) {
  await logic.reloadTree()
}
</script>

<template>
  <YzhPageLayout title="标准文档填写规则" no-padding hide-toolbar>
    <YzhTreeTableLayout
      :tree-data="logic.treeData"
      :tree-width="340"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="false"
      :tree-default-expanded-keys="logic.defaultExpandedKeys"
      :node-actions="logic.nodeActions"
      @tree-node-click="handleNodeClick"
      @tree-node-action="logic.onNodeAction"
    >
      <!--
        左树底部：**不再有「登记文档」**（2026-10-04 用户裁定）。
        文件来自**标准资料清单**，不需要也不应该在这里「登记」——
        只要在资料清单里存在，树上就有；上传空白模板是文件级的动作，走操作条。
      -->
      <template #treeFooter>
        <div class="tree-foot">
          <span class="tree-foot__hint">共 {{ logic.treeData.length }} 个机构</span>
          <el-button
            type="default"
            size="small"
            :icon="Refresh"
            :loading="treeReloading"
            @click="onReloadTree"
          >
            刷新清单
          </el-button>
        </div>
      </template>

      <template #default>
        <div class="workspace">
          <!-- ── 操作条（七步闭环；★ 按钮按 hasFile / hasTemplate 两级禁用）── -->
          <div class="opbar">
            <template v-if="hasFile">
              <!--
                ★ 下载默认给「可编辑版」（归一产物 .docx/.xlsx）——
                  原始件里 143/168 是 .doc、11 是 .xls，而填写引擎只认 docx/xlsx，
                  下原始件等于让用户先自己转一次格式。

                ⛔ 这里**不做下拉**：`el-dropdown` + 内嵌 `el-button` 会「点一下既下载又展开菜单」，
                  `split-button` 又接不了 loading 语义。原始件下载由中栏预览面板的
                  「下载原始件」承担（它本来就取原始字节），职责不重叠。
              -->
              <el-button
                type="default"
                size="small"
                :icon="Download"
                :loading="downloading"
                :disabled="!canDownloadOriginal && !canDownloadEditable"
                :title="
                  canDownloadEditable
                    ? '下载归一产物（.docx / .xlsx）—— 下载后加工成空白模板'
                    : '该文档归一产物未生成，将下载原始件（.doc / .xls），需自行另存为 .docx / .xlsx'
                "
                @click="onDownloadStandard(defaultDownloadKind)"
              >
                {{ canDownloadEditable ? '下载可编辑版' : '下载原始件' }}
              </el-button>

              <el-button
                size="small"
                :icon="Upload"
                type="primary"
                plain
                :loading="uploading"
                :disabled="isFixedDoc"
                :title="isFixedDoc ? '固定格式文档无需空白模板' : '上传加工好的空白模板（.docx / .xlsx）—— 会存到与源文件同级的 _template/ 目录'"
                @click="pickTemplateFile"
              >
                上传空白模板
              </el-button>

              <el-button
                type="default"
                size="small"
                :icon="Search"
                :loading="scanning"
                :disabled="isFixedDoc || !hasTemplate || !templatePath"
                :title="
                  !hasTemplate
                    ? '还没有空白模板 —— 请先「下载可编辑版」→ 加工 → 「上传空白模板」'
                    : isFixedDoc
                      ? '固定格式文档无需扫描锚点'
                      : '从空白模板里扫描标签占位符，生成锚点清单（零 LLM）'
                "
                @click="onRescan(false)"
              >
                重新扫描
              </el-button>

              <el-divider direction="vertical" />

              <el-button
                type="default"
                size="small"
                :icon="MagicStick"
                :loading="analyzing"
                title="聚合：字段提取（LLM）+ 锚点扫描状态（未上传模板时也能跑，会告诉你下一步做什么）"
                @click="onAnalyze"
              >
                自动分析
              </el-button>

              <el-button
                type="default"
                size="small"
                :icon="CircleCheck"
                :loading="validating"
                :disabled="isFixedDoc || !hasTemplate"
                :title="hasTemplate ? '三色校验：红牌阻断发布，黄牌进清单' : '还没有空白模板，无法校验'"
                @click="onValidate"
              >
                校验
              </el-button>

              <el-button
                type="primary"
                size="small"
                :icon="Promotion"
                :loading="publishing"
                :disabled="publishDisabled"
                :title="publishTitle"
                @click="onPublish"
              >
                发布
              </el-button>

              <span class="opbar__spacer" />

              <!-- 已上传模板才显示模板状态；否则显示「待上传」提示 -->
              <template v-if="hasTemplate">
                <YzhStatusBadge :type="publishMeta.type" :text="publishMeta.text" />
                <YzhStatusBadge
                  :type="isFixedDoc ? 'info' : 'success'"
                  :text="isFixedDoc ? '固定格式（免填）' : '可编辑'"
                />
              </template>
              <YzhStatusBadge v-else type="warning" :icon="WarningFilled" text="未上传空白模板" />
            </template>

            <span v-else class="opbar__hint">
              请在左侧展开「机构 → 标准 → 阶段」，选择一个<strong>文件</strong> —— 文件来自标准资料清单。
            </span>
          </div>

          <!--
            ★ 未上传模板时的显式引导（H-2 硬约束）：
              没有空白模板 ⇒ 不能扫描、不能配规则、不能设全文填写规则。
              用户按这三步走完，右侧四个页签才有意义。
          -->
          <div v-if="hasFile && !hasTemplate" class="guide-bar">
            <el-icon class="guide-bar__icon"><WarningFilled /></el-icon>
            <div class="guide-bar__body">
              <div class="guide-bar__title">该文件还没有空白模板 —— 右侧规则暂时无法配置</div>
              <div class="guide-bar__steps">
                <span>① 点「下载可编辑版」拿到 <code>.docx</code> / <code>.xlsx</code>（系统已自动归一，无需自己转格式）</span>
                <span>② 本地把它加工成空白模板：删掉示例数据，在要填的位置加
                  <code>{{ tokenSample }}</code> 标签（或 Word 书签 / <code>YZH_Mark</code> 标记），
                  保存时保持 <code>.docx</code> / <code>.xlsx</code></span>
                <span>③ 点「上传空白模板」—— 模板会存到源文件同级的 <code>_template/</code> 目录，
                  并自动触发一次扫描</span>
              </div>
            </div>
          </div>

          <!-- ── 主体：中栏预览 + 右栏规则 ── -->
          <div class="body">
            <div class="preview-col">
              <!--
                ★ 预览源（原始文档 / 空白模板）由 `PreviewPane` 决定，见该组件头部注释。
                  ⛔ 不要在这里 `v-if` 挑文件 —— 那会把策略又拆回两处。
              -->
              <PreviewPane
                ref="previewPaneRef"
                :file-code="logic.standardFileCode"
                :file-name="baseFileName"
                :original-path="originalPath"
                :template-path="templatePath"
                :template-file-name="templateFileName"
                :has-template="hasTemplate"
              />
            </div>

            <div class="right-col">
              <!--
                ⚠️ 页签**逐个显式书写**，⛔ 不用 v-for：
                  ① 按 DocCategory 分流时页签组不同，显式写法一眼能看出「哪套分类有哪几个页签」；
                  ② `v-for` 里的 `ref` 会被 Vue 收集成**数组**，`ref.value?.refresh()` 会静默失效。

                ⛔ **页签标签内不放 `<el-icon>`**（2026-10-04 实测缺陷：用户报「tab 页面显示不全」）。
                  `el-tabs` 的溢出滚动在 **`#label` 插槽**下不可靠（宽度测量时机问题，实测没有出现
                  左右箭头）⇒ 一旦溢出就是**硬裁掉**，「校验结果」被切成了「校验结」。
                  4 个图标约占 80~90px，去掉后 + 收紧 padding 就稳定放得下；
                  再配合 `.right-col` 加 20px 作余量。⛔ 别只加宽右栏 —— 中栏预览是 `flex:1`。
              -->
              <el-tabs v-model="activeTab" class="right-tabs">
                <!-- ── 组 E：editable（可编辑文档，要配填写规则）── -->
                <template v-if="!isFixedDoc">
                  <el-tab-pane name="anchor">
                    <template #label>
                      <span class="tab-label">锚点与字段规则</span>
                    </template>
                    <AnchorRuleTab
                      :ref="setAnchorTabRef"
                      :logic="logic"
                      :template-code="templateCode"
                      :has-template="hasTemplate"
                      :scan-status="scanStatus"
                      @saved="onAnchorSaved"
                    />
                  </el-tab-pane>

                  <!--
                    ⛔ 页签**不加 `:disabled`**（2026-10-04 修）。
                      旧版写 `:disabled="!logic.promptCode"`，而新模板的 `FillPromptCode`
                      **必然是空的** ⇒ 页签永远点不开，用户连「挂接/新建提示词」的入口都看不到
                      —— 典型的先有鸡还是先有蛋。现在未挂接时面板内给出两条破环路径。
                  -->
                  <el-tab-pane name="prompt">
                    <template #label>
                      <span class="tab-label">全文填写规则</span>
                    </template>
                    <PromptPanel
                      :key="templateCode || 'no-template'"
                      :template-code="templateCode"
                      :prompt-code="logic.promptCode"
                      :org-code="logic.templateOrgCode"
                      :template-name="promptBaseName"
                      @bound="onPromptBound"
                    />
                  </el-tab-pane>
                </template>

                <!-- ── 组 F：fixed（固定格式文档，免填）── -->
                <template v-else>
                  <el-tab-pane name="fingerprint">
                    <template #label>
                      <span class="tab-label">指纹规则</span>
                    </template>
                    <div class="placeholder">
                      <el-alert type="info" :closable="false" show-icon>
                        <template #title>
                          固定格式文档（<code>fixed</code>）不生成内容，只靠指纹匹配
                        </template>
                        <template #default>
                          <p>
                            这类文档（营业执照、生产许可、身份证等 PDF / 图片 / 扫描件）不需要填写规则 ——
                            系统靠「文件名 + 关键字段正则」把它匹配到标准文档。
                          </p>
                          <p class="placeholder__todo">
                            ⚠️ 指纹规则编辑器<strong>尚未实现</strong>（对应 <code>39</code> 号 §2.3 里未落地的能力）。
                            当前可先在「文档契约」页维护分类、作用与标签。
                          </p>
                        </template>
                      </el-alert>
                    </div>
                  </el-tab-pane>
                </template>

                <!-- ── 两种分类都有：文档契约 ── -->
                <el-tab-pane name="contract">
                  <template #label>
                    <span class="tab-label">文档契约</span>
                  </template>
                  <ContractTab
                    :detail="contract"
                    :loading="contractLoading"
                    @reload="loadContract"
                    @saved="onContractSaved"
                  />
                </el-tab-pane>

                <!-- ── 组 E 独有：校验结果 ── -->
                <el-tab-pane v-if="!isFixedDoc" name="validate">
                  <template #label>
                    <span class="tab-label">校验结果</span>
                  </template>
                  <ValidateTab
                    :ref="setValidateTabRef"
                    :template-code="templateCode"
                    :has-template="hasTemplate"
                    @validated="onValidated"
                  />
                </el-tab-pane>
              </el-tabs>
            </div>
          </div>
        </div>

        <!-- 隐藏的模板上传入口（⛔ 不用 el-upload：手动控制 multipart，避免它自带的上传流程） -->
        <input
          ref="fileInputRef"
          type="file"
          accept=".docx,.xlsx"
          style="display: none"
          @change="onTemplateFilePicked"
        />
      </template>
    </YzhTreeTableLayout>
  </YzhPageLayout>
</template>

<style scoped>
.workspace {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
}

/* ── 操作条 ── */
.opbar {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
  flex-wrap: wrap;
}
.opbar__spacer {
  flex: 1;
}
.opbar__hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
}

/*
  ── 引导条（H-2：未上传模板时的三步引导）──
  ⚠️ 2026-10-04 实测缺陷：这段样式在上一轮重做页面时**整段丢失** ⇒ 三个 `<span>`
  按 inline 排 ⇒ ①②③ 挤成一行且紧贴边框（用户原话「没有 padding」）。
  它只在「选中文件但没上传模板」时出现，正是用户第一次用本页必经的界面 ⇒ 必修。
*/
.guide-bar {
  flex-shrink: 0;
  display: flex;
  align-items: flex-start;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  background: var(--yzh-color-bg-subtle, #fffbeb);
  border-bottom: 1px solid var(--yzh-color-warning, #fde68a);
}
.guide-bar__icon {
  flex-shrink: 0;
  margin-top: var(--yzh-space-1, 4px);
  color: var(--yzh-color-warning, #d97706);
}
.guide-bar__body {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}
.guide-bar__title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-medium, 500);
  color: var(--yzh-color-warning, #b45309);
}
/* ★ 三步必须**竖排**：横排会把 ①②③ 挤成一行跑出边框（这正是修复前的样子） */
.guide-bar__steps {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-base, 1.6);
  color: var(--yzh-color-text-secondary, #606266);
}
.guide-bar__steps code {
  padding: 0 var(--yzh-space-1, 4px);
  border-radius: 3px;
  background: var(--yzh-color-bg-active, #eff6ff);
  color: var(--yzh-color-primary, #1e3a8a);
}

/* ── 主体 ── */
.body {
  flex: 1;
  min-height: 0;
  display: flex;
}
.preview-col {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
/*
  右栏宽度 460 → 480（2026-10-04）。
  实测 460 时 4 个**带图标**的页签放不下，「校验结果」被裁掉（用户原话「tab 页面显示不全」）。
  修复是两件事一起做（见模板里的页签注释）：
    ① 去掉页签图标、收紧 `el-tabs__item` 横向 padding —— 这是**主修复**，省出 ~150px；
    ② 宽度 +20px —— 只是余量，顺带让右栏的表格不那么挤。
  ⛔ 不要只靠「加宽」：中栏预览是 `flex:1`，右栏每加 100px，预览就少 100px，
    而预览才是本页的主内容。
*/
.right-col {
  width: 480px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  border-left: 1px solid var(--yzh-color-border-light, #f1f5f9);
  overflow: hidden;
}

.right-tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.right-tabs :deep(.el-tabs__header) {
  margin: 0;
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
}
/*
  ⚠️ 页签横向留白（2026-10-04 两次实测，方向相反，务必读完再改）。

  ① 第一次缺陷「tab 页面显示不全」：4 个中文页签**带图标**时在 460px 里溢出，
     而 `#label` 插槽下溢出**不出现滚动箭头**、直接硬裁（「校验结果」被切成「校验结」）。
     当时的修复是**去掉图标** + 收紧留白 + 右栏 460→480。

  ② 第二次缺陷「应该加上 padding」（本次）：上一轮把 `nav-wrap` 的 padding 也归零了，
     于是**第一个页签贴住右栏左边框**。但这里有个 Element Plus 的坑 ——
     `.el-tabs--top > .el-tabs__header .el-tabs__item:nth-child(2) { padding-left: 0 }`
     与 `:last-child { padding-right: 0 }`（特异性 0,4,0）会**强制抹掉首尾页签的内边距**，
     作用域规则（0,3,0）压不过它 ⇒ 光把 item 的 padding 写回去是**无效的**。
     ⇒ 修法：`nav-wrap` 补回 12px，并对 item 的 padding 加 `!important` 压过 EP。

  宽度核算（13px 中文字，实测 7 字 ≈ 79px）：
     可编辑组 79+68+45+45 = 237px 文本 + 4×24 内边距 + 24 外留白 ≈ 357px ＜ 480px ✓
     固定组   45+45 = 90px 文本 + 2×24 + 24 ≈ 162px ✓
  ⛔ 不要再加回页签图标：那会直接回到缺陷 ①。
*/
.right-tabs :deep(.el-tabs__nav-wrap) {
  padding: 0 var(--yzh-space-3, 12px);
}
.right-tabs :deep(.el-tabs__item) {
  height: 42px;
  line-height: 42px;
  font-size: var(--yzh-font-size-sm, 13px);
  /* `!important` 限定在 `:deep()` 内 —— 样式法条 S01–S12 明确放行此写法。
     原因见上方注释 ②：EP 对首尾页签的 `padding-*: 0` 特异性更高，不加压不过。 */
  padding: 0 var(--yzh-space-3, 12px) !important;
}
.right-tabs :deep(.el-tabs__content) {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-3, 12px);
}
.right-tabs :deep(.el-tab-pane) {
  height: 100%;
}
.tab-label {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
}

.placeholder {
  padding: var(--yzh-space-1, 4px) 0;
}
.placeholder :deep(p) {
  margin: var(--yzh-space-1, 4px) 0 0;
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.8);
}
.placeholder__todo {
  color: var(--yzh-color-warning, #d97706);
}
</style>
