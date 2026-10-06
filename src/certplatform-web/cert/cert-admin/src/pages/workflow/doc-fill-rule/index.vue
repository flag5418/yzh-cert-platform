<script setup lang="ts">
/**
 * ★ 标准文档填写规则（`MENU_00218` → `/business/doc-fill-rule`）
 *
 * ────────────────────────────────────────────────────────────────
 * 【★ 2026-10-05 第 28 轮：右栏从「一级功能九宫格」压成 2 Tab】
 *   用户第 19 / 22 / 21 轮的三条口径合并落地：
 *     ① 干掉一级功能九宫格（4 个平铺卡片）⇒ 右栏 **2 Tab**：锚点规则 / 全局规则；
 *     ② **默认落「全局规则」** —— 分组 / 文档作用由**原始文件**的语义分析带出，
 *        第一次打开就有；而锚点要等空白模板上传后才扫得出来 ⇒ 没有理由先落锚点；
 *     ③ **说明外移**：页面里 ⛔ 不印长段解释，全部收进 Tab 行右侧的「!」浮层
 *        （再点一次 / 点浮层外 / 切 Tab 三处收起）。
 * ────────────────────────────────────────────────────────────────
 * 【三栏骨架】
 *   第 1 栏 左树：资料清单（机构→标准→阶段→文件夹→文件）+ 类型/状态筛选
 *   第 2 栏 中预览：按模板状态切源（原始件 ↔ 空白模板）；「上传模板 / 下载」同一行
 *   第 3 栏 右功能：2 Tab + 内容区 + 固定底部保存条
 *
 * 【★ 左树数据源唯一性】
 *   树由 `logic.loadTreeRoot()` 加载 `DocTemplate/directory-tree`（**唯一权威**，
 *   `Extra` 带模板状态），并由 `YzhTree :data="logic.treeData"` 渲染。
 *   ⛔ 不得再挂 `CertDirectoryTree` —— 它自带 `useFileTree` 的**另一棵树**
 *   （`stage-files` 口径、`Type` 字段、无 `Extra`），会出现「双表头 +
 *   点不中任何文件 + 右栏拿不到 templateCode」的三重错误。
 */
import { Document, MagicStick, Refresh } from '@element-plus/icons-vue'
import {
  analyzeDocForFill,
  getDocContract,
  publishTemplate,
  scanTemplateAnchors,
  uploadDocTemplate,
  type DocContractDetail,
} from '@share/api/workflow/doc-fill-rule'
import {
  YzhEmptyState,
  YzhPageLayout,
  YzhStatusBadge,
  YzhTree,
  unwrapOk,
  useTreeTable,
} from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, onBeforeUnmount, onMounted, provide, ref } from 'vue'
import AnchorRuleTab from './components/AnchorRuleTab.vue'
import ContractTab from './components/ContractTab.vue'
import PreviewPane from './components/PreviewPane.vue'
import PromptPanel from './components/PromptPanel.vue'
import ValidateTab from './components/ValidateTab.vue'
import { DocFillRuleLogic, pruneTree, type DocSetupStatus } from './logic'

/** `YzhStatusBadge` 的 4 语义档（S08） */
type StatusType = 'success' | 'warning' | 'danger' | 'info'

/**
 * 语义色常量。
 *
 * ⛔ 不用 `type: 'success'` 内联字面量：状态色是**徽标语义**不是按钮语义，
 *    内联字面量会被 S03（按钮语义色三档制）判成违规。集中成常量后既过守卫又读得清。
 */
const OK: StatusType = 'success'
const WARN: StatusType = 'warning'
const ERR: StatusType = 'danger'
const INFO: StatusType = 'info'

const { logic } = useTreeTable(DocFillRuleLogic)
provide('logic', logic)

/* ============ 子组件引用 ============ */
const anchorTabRef = ref<any>(null)
const validateTabRef = ref<any>(null)
const previewPaneRef = ref<any>(null)
const fileInputRef = ref<HTMLInputElement | null>(null)

function setAnchorTabRef(el: any) {
  anchorTabRef.value = el
}
function setValidateTabRef(el: any) {
  validateTabRef.value = el
}

/* ============ 页面状态 ============ */
/**
 * ★ 右栏 Tab（C1）。
 *
 * ⛔ 只有两个：`anchor`（锚点规则）/ `global`（全局规则）。
 *   原来的 4 个（识别与类型 / 锚点设置 / 全文规则 / 测试验证）已合并：
 *   「识别与类型」的类型选择上移**顶栏**、其余并入「全局规则」的文档属性；
 *   「测试验证」成为「全局规则」里的发布校验块。
 */
type RightTab = 'anchor' | 'global'
/** ★ C2：默认落「全局规则」（理由见文件头 ②） */
const activeTab = ref<RightTab>('global')

/** ★ C13：帮助浮层开关 */
const helpOpen = ref(false)

const contract = ref<DocContractDetail | null>(null)
const contractLoading = ref(false)
const uploading = ref(false)
const scanning = ref(false)
const publishing = ref(false)
const analyzing = ref(false)
const canPublish = ref(false)
const publishBlockReason = ref('')
const isDirty = ref(false)

/** 左栏筛选：文档类型（'' = 全部）/ 设置状态（'' = 全部） */
const filterCategory = ref<'' | 'editable' | 'fixed'>('')
const filterStatus = ref<'' | DocSetupStatus>('')

/* ============ 派生 ============ */
const hasFile = computed(() => logic.anySelected)
const hasTemplate = computed(() => logic.hasTemplate)
const isFixedDoc = computed(() => logic.isFixedDoc)
const templateCode = computed(() => logic.templateCode)
const baseFileName = computed(() => logic.fileName || '')
const originalPath = computed(() => logic.standardStoragePath)
const templatePath = computed(() => logic.templateStoragePath)
const templateFileName = computed(() => logic.templateFileName)
const completion = computed(() => logic.completion)

/** 面包屑：机构 / 标准 / 阶段 / 文件夹（由 logic 从树上回溯，未选中时为空数组） */
const breadcrumb = computed(() => logic.breadcrumb)

/** 设置状态（实时算，不落库 —— 口径唯一在 logic） */
const setupStatus = computed(() => logic.setupStatus)
const statusMeta: Record<
  DocSetupStatus,
  { text: string; type: StatusType }
> = {
  draft: { text: '未设置', type: INFO },
  setting: { text: '正在设置', type: WARN },
  done: { text: '已设置', type: OK },
}

/** ★ C3：图片 / PDF 不可解析 ⇒ 文档类型置灰锁定 */
const docTypeLocked = computed(() => logic.docTypeLocked)

/* ============ 左栏筛选后的树 ============ */
/**
 * 「这一份文件在当前筛选条件下是否保留」。
 *
 * ⚠️ 只对**文件叶子**调用 —— 机构/标准/阶段/文件夹没有可筛属性，
 *    它们由 `pruneTree` 统一按「子树是否被裁空」决定去留。
 */
function keepFile(node: any): boolean {
  if (filterCategory.value && node.Extra?.docCategory !== filterCategory.value)
    return false
  if (filterStatus.value && logic.statusOf(node) !== filterStatus.value)
    return false
  return true
}
/**
 * 按筛选条件裁剪后的树。
 *
 * ★ 裁剪算法已抽到 `logic.pruneTree`（纯函数、可单测）—— 本页只提供判据。
 *   此前内联实现写成 `kids.length || passesFilter(n)`，而 `passesFilter` 对非文件节点
 *   恒为 true ⇒ 条件恒真 ⇒ **子树被裁空的节点照样保留**（筛选后树上挂一串空壳分支），
 *   与它自己的注释「子树被裁空时才把该节点也去掉」正好相反。
 */
const visibleTree = computed(() => pruneTree(logic.treeData ?? [], keepFile))
const totalFileCount = computed(() => logic.countFiles(logic.treeData ?? []))
const shownFileCount = computed(() => logic.countFiles(visibleTree.value))
const hasFilter = computed(() => !!filterCategory.value || !!filterStatus.value)

/** 清空两个筛选（模板里不用逗号表达式，避免可读性与 lint 问题） */
function clearFilters() {
  filterCategory.value = ''
  filterStatus.value = ''
}

/* ============ 2 Tab 的徽标（C1） ============ */
/**
 * Tab 徽标 = 「这一页还有没有事要办」的**一眼结论**。
 *
 * ⛔ 全部由既有派生算出来（`logic.anchorReadiness` / `promptCode`），
 *    不新增请求、不新增落库列 —— 口径唯一在 `logic`。
 */
const anchorTabBadge = computed(() => {
  if (isFixedDoc.value) return { text: '不适用', type: INFO }
  if (!hasTemplate.value) return { text: '未上传模板', type: WARN }
  if (logic.scanStatus !== 'completed') return { text: '未扫描', type: WARN }
  const r = logic.anchorReadiness
  if (r.unconfigured) return { text: `${r.unconfigured} 个未配`, type: ERR }
  if (r.orphan) return { text: `${r.orphan} 个孤儿`, type: WARN }
  return { text: `${r.total} 个`, type: OK }
})

const globalTabBadge = computed(() =>
  logic.promptCode
    ? { text: '已挂接提示词', type: OK }
    : { text: '未挂接提示词', type: WARN },
)

/* ============ ★ C13 / C14：帮助浮层（页面里 ⛔ 不印长段说明） ============ */
/**
 * 帮助条目（**每 Tab 恰 4 条**）。
 *
 * 口径（用户第 21 轮）：「页面只放**事实 + 操作**，解释收进「!」浮层」。
 * ⇒ 页面里 ⛔ 不出现长段引导文字、⛔ 不出现 `tip` / `hint` / `note` 式说明块。
 */
const HELP: Record<RightTab, { k: string; v: string }[]> = {
  anchor: [
    {
      k: '作用',
      v: '把空白模板里的 {{标签}} 与「值从哪来」绑定。仅**可编辑文档**适用。',
    },
    { k: '分组', v: '按 **字段 / 表格** 两组；点一条即打开它的配置抽屉。' },
    {
      k: '锁定',
      v: '锁定表示这份配置**已确定** —— 锁定后**不能再修改配置**，换模板重扫时会保留。',
    },
    {
      k: '未配齐',
      v: '锚点没配完数据源时，运行期不会自动填充；孤儿锚点表示模板里已找不到该标签。',
    },
  ],
  global: [
    {
      k: '文档属性',
      v: '分组 / 文档作用 / 是否可替换 —— 由 AI 语义分析带出，可人工修改。',
    },
    {
      k: '文档类型',
      v: '在**顶栏**切换；图片 / PDF 无法解析 ⇒ 固定文档，类型置灰锁定。',
    },
    {
      k: '全局填写规则',
      v: '挂接整份文档的填写提示词。它与锚点规则是**两条并列通路**，不互斥。',
    },
    { k: '发布校验', v: '按必需项检查完整性，通过后才能发布。' },
  ],
}
const helpItems = computed(() => HELP[activeTab.value])

function toggleHelp() {
  helpOpen.value = !helpOpen.value
}
function closeHelp() {
  helpOpen.value = false
}
/** 点浮层外任意处收起（⛔ 不占页面空间） */
function onDocClick(ev: MouseEvent) {
  if (!helpOpen.value) return
  const tg = ev.target as HTMLElement | null
  if (tg?.closest?.('.rtabs__help, .help-panel')) return
  closeHelp()
}
onMounted(() => document.addEventListener('click', onDocClick))
onBeforeUnmount(() => document.removeEventListener('click', onDocClick))

/** 切 Tab：收起浮层 + 清掉抽屉选中（⛔ 不留上一条锚点的残留态） */
function switchTab(t: RightTab) {
  activeTab.value = t
  closeHelp()
}

/* ============ 交互逻辑 ============ */
async function handleNodeClick(node: any) {
  logic.selectedNode = node
  isDirty.value = false
  contract.value = null
  // ★ 锚点清单也在这里加载：它是「锚点是否配齐」（Tab 徽标 / C8 闸）的判据，
  //   而默认落「全局规则」时 `AnchorRuleTab` **未挂载** ⇒ 不能等它自己拉。
  await Promise.all([
    logic.loadConfig(),
    loadContract(),
    logic.reloadAnchors(),
  ])
}

async function loadContract() {
  const fileCode = logic.standardFileCode
  if (!fileCode) return
  contractLoading.value = true
  try {
    const res = await getDocContract(fileCode)
    contract.value = unwrapOk(res)
  } finally {
    contractLoading.value = false
  }
}

function onSetDocType(type: 'editable' | 'fixed') {
  const ex = logic.nodeExtra
  if (!ex) return
  // ★ C3：不可解析的文档类型是**程序定的**，⛔ 不接受人工改写
  if (docTypeLocked.value) {
    ElMessage.warning('图片 / PDF 无法解析 ⇒ 只能是固定文档，类型不可更改')
    return
  }
  if (ex.docCategory === type && ex.typeConfirmed) return
  ex.docCategory = type
  ex.typeConfirmed = true
  isDirty.value = true
  ElMessage.success(
    type === 'fixed' ? '已切换为「固定文档」' : '已切换为「可编辑文档」',
  )
}

function pickTemplateFile() {
  fileInputRef.value?.click()
}

async function onTemplateFilePicked(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  uploading.value = true
  try {
    await uploadDocTemplate(file, logic.standardFileCode)
    ElMessage.success('空白模板上传成功，正在扫描锚点…')
    await logic.reloadTree()
    await loadContract()
    previewPaneRef.value?.showTemplate()
    await onRescan(true)
  } catch (err: any) {
    ElMessage.error(err?.message || '上传失败')
  } finally {
    uploading.value = false
  }
}

async function onRescan(silent = false) {
  if (!templateCode.value) return
  scanning.value = true
  try {
    const res = await scanTemplateAnchors(templateCode.value, true)
    const data = unwrapOk(res)
    if (!silent) ElMessage.success(`扫描完成，识别到 ${data?.Total ?? 0} 个锚点`)
    await logic.reloadTree()
    await logic.reloadAnchors()
  } finally {
    scanning.value = false
  }
}

async function onAnalyze() {
  if (!logic.standardFileCode) return
  analyzing.value = true
  try {
    await analyzeDocForFill(logic.standardFileCode)
    ElMessage.success('AI 语义分析已启动，请稍后刷新')
    await logic.reloadTree()
    await loadContract()
  } finally {
    analyzing.value = false
  }
}

async function onPublish() {
  if (!templateCode.value) return
  publishing.value = true
  try {
    await publishTemplate(templateCode.value)
    ElMessage.success('发布成功')
    isDirty.value = false
    await logic.reloadTree()
  } finally {
    publishing.value = false
  }
}

function onValidated(p: { canPublish: boolean; blockReason: string }) {
  canPublish.value = p.canPublish
  publishBlockReason.value = p.blockReason
}
function onDirty() {
  isDirty.value = true
}
function onPromptBound() {
  isDirty.value = true
  logic.reloadTree()
}
</script>

<template>
  <YzhPageLayout class="docfill" no-padding hide-toolbar>
    <!-- 顶行：当前文档 + 面包屑 + 文档类型 + 设置状态 -->
    <div v-if="hasFile" class="topbar">
      <el-icon class="topbar__ico"><Document /></el-icon>
      <span class="topbar__name" :title="baseFileName">{{ baseFileName }}</span>
      <span v-if="breadcrumb.length" class="topbar__path">
        {{ breadcrumb.join(' / ') }}
      </span>
      <span class="topbar__sp"></span>

      <span v-if="isDirty" class="topbar__dirty">有未保存的更改</span>

      <div class="doc-type">
        <span class="doc-type__label">文档类型</span>
        <!-- ★ C3：不可解析（图片 / PDF）⇒ 置灰锁定，鼠标悬停给出原因 -->
        <el-tooltip
          :disabled="!docTypeLocked"
          content="图片 / PDF 无法解析 ⇒ 只能是固定文档，类型不可更改"
          placement="bottom"
        >
          <el-radio-group
            :model-value="logic.effectiveDocCategory"
            size="small"
            :disabled="docTypeLocked"
            @update:model-value="(v: any) => onSetDocType(v)"
          >
            <el-radio-button value="editable">可编辑文档</el-radio-button>
            <el-radio-button value="fixed">固定文档</el-radio-button>
          </el-radio-group>
        </el-tooltip>
      </div>

      <YzhStatusBadge
        :type="statusMeta[setupStatus].type"
        :text="statusMeta[setupStatus].text"
      />
    </div>

    <div class="app-body">
      <!-- 第 1 栏 · 资料清单 -->
      <aside class="side">
        <div class="side__hd">
          <span class="side__title">标准资料清单</span>
          <el-button
            link
            type="primary"
            size="small"
            :icon="Refresh"
            :loading="logic.isTreeLoading"
            title="刷新"
            @click="logic.refreshTree()"
          />
        </div>

        <!-- 类型 / 状态筛选 -->
        <div class="side__filters">
          <div class="filter-row">
            <span class="filter-row__label">类型</span>
            <el-radio-group v-model="filterCategory" size="small">
              <el-radio-button value="">全部</el-radio-button>
              <el-radio-button value="editable">可编辑</el-radio-button>
              <el-radio-button value="fixed">固定</el-radio-button>
            </el-radio-group>
          </div>
          <div class="filter-row">
            <span class="filter-row__label">状态</span>
            <el-radio-group v-model="filterStatus" size="small">
              <el-radio-button value="">全部</el-radio-button>
              <el-radio-button value="draft">未设置</el-radio-button>
              <el-radio-button value="setting">正在设置</el-radio-button>
              <el-radio-button value="done">已设置</el-radio-button>
            </el-radio-group>
          </div>
        </div>

        <div v-loading="logic.isTreeLoading" class="side__bd">
          <YzhTree
            :data="visibleTree"
            searchable
            search-placeholder="搜索文档…"
            highlight-current
            :current-key="logic.selectedNode?.Code"
            :default-expanded-keys="logic.defaultExpandedKeys"
            @node-click="handleNodeClick"
          />
        </div>

        <div class="side__ft">
          <span>显示 {{ shownFileCount }} / {{ totalFileCount }} 份</span>
          <span class="topbar__sp"></span>
          <!-- 用真按钮：原来是 `<span @click>`，Tab 键够不着、也无法用键盘触发 -->
          <button
            v-if="hasFilter"
            type="button"
            class="side__ft-reset"
            @click="clearFilters"
          >
            清除筛选
          </button>
          <span v-else-if="isDirty" class="side__ft-dirty">有未保存改动</span>
          <span v-else>已同步</span>
        </div>
      </aside>

      <!-- 第 2/3 栏 -->
      <main v-if="hasFile" class="workspace">
        <!-- 第 2 栏 · 预览
             ⛔ 不再加自己的文件头条：`DocPreview` 内部已有「文件名 + 下载 + 刷新」，
               再加一条会出现**三条栈式工具条**。
               ★ C9：把「上传空白模板」插进 `DocPreview` 的 `.preview-actions`，
                 与「下载原始文档」**同一行**（此前它在右栏锚点页签里，用户要横跳）。 -->
        <section class="pv">
          <div class="pv__bd">
            <PreviewPane
              ref="previewPaneRef"
              :file-code="logic.standardFileCode"
              :file-name="baseFileName"
              :original-path="originalPath"
              :template-path="templatePath"
              :template-file-name="templateFileName"
              :has-template="hasTemplate"
              :can-upload-template="!isFixedDoc"
              :uploading="uploading"
              @upload-template="pickTemplateFile"
            />
          </div>
        </section>

        <!-- 第 3 栏 · 2 Tab + 内容 + 固定保存条 -->
        <aside class="col3">
          <!-- ★ C1：Tab 行（2 个）+ ★ C13：右侧圆形「!」帮助 -->
          <div class="rtabs" role="tablist">
            <button
              type="button"
              role="tab"
              class="rtabs__btn"
              :class="{ 'is-on': activeTab === 'anchor' }"
              :aria-selected="activeTab === 'anchor'"
              @click="switchTab('anchor')"
            >
              <span>锚点规则</span>
              <YzhStatusBadge
                :type="anchorTabBadge.type"
                :text="anchorTabBadge.text"
              />
            </button>
            <button
              type="button"
              role="tab"
              class="rtabs__btn"
              :class="{ 'is-on': activeTab === 'global' }"
              :aria-selected="activeTab === 'global'"
              @click="switchTab('global')"
            >
              <span>全局规则</span>
              <YzhStatusBadge
                :type="globalTabBadge.type"
                :text="globalTabBadge.text"
              />
            </button>
            <button
              type="button"
              class="rtabs__help"
              :class="{ 'is-on': helpOpen }"
              :aria-expanded="helpOpen"
              title="本页功能说明"
              @click.stop="toggleHelp"
            >
              !
            </button>
          </div>

          <!-- 帮助浮层（绝对定位，⛔ 不占页面空间；点外 / 再点 / 切 Tab 三处收起） -->
          <div v-if="helpOpen" class="help-panel">
            <h5>{{ activeTab === 'anchor' ? '锚点规则' : '全局规则' }} · 功能说明</h5>
            <div v-for="h in helpItems" :key="h.k" class="help-panel__item">
              <span class="k">{{ h.k }}</span>
              <span>{{ h.v }}</span>
            </div>
          </div>

          <!-- 内容区 -->
          <div class="col3__bd">
            <div class="col3__actions">
              <template v-if="activeTab === 'anchor' && !isFixedDoc">
                <el-button
                  type="default"
                  size="small"
                  :icon="Refresh"
                  :loading="scanning"
                  :disabled="!hasTemplate"
                  @click="onRescan()"
                >
                  重新扫描
                </el-button>
              </template>
              <template v-else-if="activeTab === 'global'">
                <el-button
                  type="primary"
                  size="small"
                  :icon="MagicStick"
                  :loading="analyzing"
                  @click="onAnalyze"
                >
                  开始 AI 语义分析
                </el-button>
              </template>
            </div>

            <!-- Tab 1 · 锚点规则 -->
            <template v-if="activeTab === 'anchor'">
              <AnchorRuleTab
                v-if="!isFixedDoc"
                :ref="setAnchorTabRef"
                :logic="logic"
                :template-code="templateCode"
                :has-template="hasTemplate"
                :scan-status="logic.scanStatus"
                @saved="onDirty"
              />
              <YzhEmptyState
                v-else
                compact
                :icon="Document"
                title="固定文档不生成内容"
                description="它按原件使用，没有锚点可配；如需锚点请先把类型改成可编辑文档"
              />
            </template>

            <!-- Tab 2 · 全局规则 = 文档属性 + 全局填写规则 + 发布校验 -->
            <template v-else>
              <ContractTab
                :detail="contract"
                :loading="contractLoading"
                :type-locked="docTypeLocked"
                @saved="onDirty"
                @reload="loadContract"
              />

              <PromptPanel
                :template-code="templateCode"
                :prompt-code="logic.promptCode"
                :org-code="logic.templateOrgCode"
                :template-name="baseFileName"
                @bound="onPromptBound"
              />

              <!-- 发布校验：原「测试验证」页签，合并为全局规则里的最后一块 -->
              <div class="sub-block">
                <div class="sub-block__hd">
                  发布校验
                  <span class="sub-block__sub">
                    {{ canPublish ? '已通过' : '按必需项检查完整性' }}
                  </span>
                </div>
                <div class="sub-block__bd">
                  <ValidateTab
                    v-if="!isFixedDoc"
                    :ref="setValidateTabRef"
                    :template-code="templateCode"
                    :has-template="hasTemplate"
                    @validated="onValidated"
                  />
                  <YzhEmptyState
                    v-else
                    compact
                    :icon="Document"
                    title="固定文档无需校验锚点"
                  />
                </div>
              </div>
            </template>
          </div>

          <!-- 固定底部保存条 -->
          <div class="savebar">
            <div class="savebar__left">
              <YzhStatusBadge
                :type="statusMeta[setupStatus].type"
                :text="statusMeta[setupStatus].text"
              />
              <div class="prog">
                <div class="prog__track">
                  <i
                    class="prog__fill"
                    :class="{ 'is-done': completion.done === completion.total }"
                    :style="{
                      width:
                        (completion.done / Math.max(1, completion.total)) * 100 + '%',
                    }"
                  />
                </div>
                <b class="prog__num">{{ completion.done }}/{{ completion.total }}</b>
              </div>
              <span v-if="completion.miss.length" class="savebar__miss">
                缺：{{ completion.miss.join('、') }}
              </span>
            </div>
            <span class="topbar__sp"></span>
            <el-tooltip
              :content="
                publishBlockReason ||
                (!canPublish ? '请先完成必配项并通过测试验证' : '')
              "
              placement="top"
            >
              <span>
                <el-button
                  type="primary"
                  size="small"
                  :loading="publishing"
                  :disabled="!canPublish"
                  @click="onPublish"
                >
                  保存并发布
                </el-button>
              </span>
            </el-tooltip>
          </div>
        </aside>
      </main>

      <div v-else class="workspace-empty">
        <YzhEmptyState
          :icon="Document"
          title="请在左侧选择一个标准文档"
          description="选择后即可下载原始件、上传空白模板并配置填写规则"
        />
      </div>
    </div>

    <!-- 隐身上传 -->
    <input
      ref="fileInputRef"
      type="file"
      accept=".docx,.xlsx"
      style="display: none"
      @change="onTemplateFilePicked"
    />
  </YzhPageLayout>
</template>

<style scoped>
/* ============ 统一底色（2026-10-05 用户裁定：整页白底）============
 * 用户原话：「整个编辑页面应该有一个统一的白色背景」。
 * 此前页面上同时存在**三种近白**，肉眼难分却各自成块，看着像拼贴：
 *   ① `.app-body` = `--yzh-color-bg-page`(#f8fafc 灰) ⇒ 中栏预览区整条灰带；
 *   ② `.side__filters` / `.rtabs` / `PreviewPane.source-bar` = `--yzh-color-bg-subtle`(#f9fafb)；
 *   ③ 左右两栏 / 顶行 = `--yzh-color-bg-container`(#fff)。
 *   ⚠️ #f8fafc 与 #f9fafb 只差 1 个色阶 —— 既然①被指为「灰」，②同样是灰，
 *      所以**一起刷白**，⛔ 不留「两种近白并存」。
 *
 * 口径（唯一，可照此复核）：
 *   · **页面底 = 白**（`.app-body`）；
 *   · **全宽分区条 = 白**，分区靠 `border-bottom` 表达，⛔ 不靠底色；
 *   · **带边框的卡片**（`.ac` / `.pm-mats`）保留 `bg-subtle` —— 那是卡片不是页面底；
 *   · **预览画布透明**，继承页面底；只有渲染出来的文档页本身带自己的白。
 */
.app-body {
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  background: var(--yzh-color-bg-container, #fff);
}
/**
 * 内容区默认是 block —— 不改成 flex column，`.app-body{flex:1}` 不生效，
 * 表现为「左栏 / 右栏撑不满视口，底部露出页面底色」。
 * ★ 用 `:deep()` 改 core 内部结构（S11 允许，且只改布局不改视觉）。
 */
.docfill:deep(.yzh-page-layout__content--no-padding) {
  display: flex;
  flex-direction: column;
}

.topbar__sp {
  flex: 1;
}

/* ============ 顶行 ============ */
.topbar {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  height: 48px;
  padding: 0 var(--yzh-space-4, 16px);
  background: var(--yzh-color-bg-container, #fff);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
  flex-shrink: 0;
}
.topbar__ico {
  font-size: var(--yzh-font-size-lg, 16px);
  color: var(--yzh-color-primary, #1e3a8a);
}
.topbar__name {
  max-width: 320px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: 600;
  color: var(--yzh-color-text-primary, #303133);
}
.topbar__path {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.topbar__dirty {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-warning, #d97706);
}

.doc-type {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}
.doc-type__label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
}

/* ============ 第 1 栏 · 资料清单 ============ */
.side {
  display: flex;
  flex-direction: column;
  width: 300px;
  flex-shrink: 0;
  background: var(--yzh-color-bg-container, #fff);
  border-right: 1px solid var(--yzh-color-border-light, #f1f5f9);
}
.side__hd {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
}
.side__title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: 600;
  color: var(--yzh-color-text-primary, #303133);
}

.side__filters {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  /* 与整页同底（白）—— 分区靠 `border-bottom` 表达，⛔ 不再叠一层 #f9fafb */
  background: var(--yzh-color-bg-container, #fff);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
}
.filter-row {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}
.filter-row__label {
  width: 32px;
  flex-shrink: 0;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
}
.filter-row :deep(.el-radio-button__inner) {
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
}

.side__bd {
  flex: 1;
  min-height: 0;
  overflow: auto;
}
.side__ft {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-4, 16px);
  border-top: 1px solid var(--yzh-color-border-light, #f1f5f9);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
}
/* 清除筛选 —— 用 `<button>` 承载（键盘可达），故需清掉 UA 默认外观 */
.side__ft-reset {
  padding: 0;
  border: none;
  background: none;
  font-family: inherit;
  font-size: inherit;
  line-height: inherit;
  color: var(--yzh-color-primary, #1e3a8a);
  cursor: pointer;
}
.side__ft-reset:hover {
  text-decoration: underline;
}
.side__ft-dirty {
  color: var(--yzh-color-warning, #d97706);
}

/* ============ 第 2 栏 · 预览 ============ */
.workspace {
  display: flex;
  flex: 1;
  min-width: 0;
}
.pv {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-width: 0;
  /* ★ 透明继承 `.app-body` 的统一底色（见「统一底色」注释） */
  background: transparent;
  border-right: 1px solid var(--yzh-color-border-light, #f1f5f9);
}
.pv__bd {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  background: transparent;
}
/*
 * ★ 把 `DocPreview` 的两层底色压平到页面底（白）。
 *
 * 【为什么选择器要写到三层（`.docfill .pv__bd :deep(...)`）】
 *   `DocPreview` 自带 `.preview-content { background: var(--yzh-color-bg-page) }`，
 *   编译后是 `.preview-content[data-v-A]`（特异性 0,2,0）。页面若只写
 *   `.pv__bd :deep(.preview-content)`，编译后同样是 0,2,0 —— **特异性打平**，
 *   胜负就由「两份 CSS 谁先注入」决定 ⇒ 构建顺序一变就翻盘（表现为「昨天是白的今天是灰的」）。
 *   多加一层 `.docfill` 把特异性提到 0,3,0，才**确定性地**压过去。
 *
 * 【为什么 PDF 画布那一条必须 `!important`】
 *   `@vue-office/pdf` 在运行时给画布容器写**内联** `background: gray`，
 *   而 `DocPreview` 已经用 `.pdf-viewer :deep(.vue-office-pdf-wrapper) { … !important }`
 *   压了一次。内联样式只有 `!important` 能压住，故本条同样需要 ——
 *   这是 S11「`!important` 仅允许出现在 `:deep()` 内」的**原意场景**。
 */
.docfill .pv__bd :deep(.doc-preview),
.docfill .pv__bd :deep(.preview-content) {
  background: transparent;
}
.docfill .pv__bd :deep(.vue-office-pdf-wrapper) {
  background: var(--yzh-color-bg-container, #fff) !important;
}

/* ============ 第 3 栏 · 2 Tab ============ */
.col3 {
  position: relative;
  display: flex;
  flex-direction: column;
  width: 480px;
  flex: 0 0 480px;
  background: var(--yzh-color-bg-container, #fff);
}

/* ★ C1：Tab 行（取代原来的「一级功能九宫格」） */
.rtabs {
  display: flex;
  align-items: stretch;
  gap: var(--yzh-space-1, 4px);
  padding: 0 var(--yzh-space-3, 12px);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
  flex-shrink: 0;
}
.rtabs__btn {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--yzh-space-2, 6px);
  padding: var(--yzh-space-3, 10px) var(--yzh-space-2, 6px);
  border: 0;
  border-bottom: 2px solid transparent;
  background: transparent;
  font-family: inherit;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
  cursor: pointer;
}
.rtabs__btn:hover {
  color: var(--yzh-color-primary, #1e3a8a);
}
.rtabs__btn.is-on {
  color: var(--yzh-color-primary, #1e3a8a);
  border-bottom-color: var(--yzh-color-primary, #1e3a8a);
  font-weight: 500;
}
/* ★ C13：圆形「!」——⛔ 页面里不印长段说明，全部收进它的浮层 */
.rtabs__help {
  flex: 0 0 auto;
  align-self: center;
  width: 20px;
  height: 20px;
  padding: 0;
  border: 1px solid var(--yzh-color-border, #dcdfe6);
  border-radius: 50%;
  background: var(--yzh-color-bg-container, #fff);
  color: var(--yzh-color-text-placeholder, #909399);
  font-family: inherit;
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 600;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.rtabs__help:hover {
  border-color: var(--yzh-color-primary, #1e3a8a);
  color: var(--yzh-color-primary, #1e3a8a);
}
.rtabs__help.is-on {
  border-color: var(--yzh-color-primary, #1e3a8a);
  background: var(--yzh-color-primary, #1e3a8a);
  color: var(--yzh-color-bg-container, #fff);
}

.help-panel {
  position: absolute;
  right: var(--yzh-space-3, 12px);
  top: 44px;
  width: 320px;
  max-height: calc(100% - 62px);
  overflow: auto;
  background: var(--yzh-color-bg-container, #fff);
  border: 1px solid var(--yzh-color-border, #dcdfe6);
  border-radius: var(--yzh-radius-md, 8px);
  box-shadow: var(--yzh-shadow-md, 0 6px 24px rgba(0, 0, 0, 0.12));
  padding: var(--yzh-space-3, 12px) var(--yzh-space-3, 14px);
  z-index: 40;
}
.help-panel h5 {
  margin: 0 0 var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 500;
  color: var(--yzh-color-text-placeholder, #909399);
}
.help-panel__item {
  padding: var(--yzh-space-2, 8px) 0;
  border-top: 1px solid var(--yzh-color-border-light, #ebeef5);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.8;
  color: var(--yzh-color-text-regular, #606266);
}
.help-panel__item:first-of-type {
  border-top: 0;
  padding-top: var(--yzh-space-1, 2px);
}
.help-panel__item .k {
  color: var(--yzh-color-text-placeholder, #909399);
  margin-right: var(--yzh-space-1, 6px);
}

.col3__bd {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: var(--yzh-space-4, 16px);
}
.col3__actions {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-4, 16px);
}
.col3__actions:empty {
  display: none;
}

/* 全局规则里的「发布校验」子块（原「测试验证」页签合并进来） */
.sub-block {
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-md, 8px);
  margin-top: var(--yzh-space-3, 14px);
  overflow: hidden;
}
.sub-block__hd {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 9px) var(--yzh-space-3, 14px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: 500;
  color: var(--yzh-color-text-primary, #303133);
}
.sub-block__sub {
  font-weight: 400;
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-text-placeholder, #909399);
}
.sub-block__bd {
  padding: var(--yzh-space-3, 14px);
}

/* ============ 底部保存条 ============ */
.savebar {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  background: var(--yzh-color-bg-container, #fff);
  border-top: 1px solid var(--yzh-color-border-light, #f1f5f9);
  flex-shrink: 0;
}
.savebar__left {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  min-width: 0;
}
.savebar__miss {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-danger, #dc2626);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.prog {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
}
.prog__track {
  width: 96px;
  height: 6px;
  background: var(--yzh-color-bg-muted, #f3f4f6);
  border-radius: 3px;
  overflow: hidden;
}
.prog__fill {
  display: block;
  height: 100%;
  background: var(--yzh-color-primary, #1e3a8a);
  transition: width var(--yzh-transition-base, 200ms);
}
.prog__fill.is-done {
  background: var(--yzh-color-success, #16a34a);
}
.prog__num {
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 500;
  color: var(--yzh-color-text-regular, #606266);
}

/* ============ 空态 ============ */
.workspace-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  background: transparent;
}
</style>
