<script setup lang="ts">
/**
 * 全文填写规则 —— 模板上挂的「全文填写提示词」版本列表 + 挂接/新建入口
 *
 * 【与「锚点规则」的关系】
 *   两条通路**并列、不互斥**：
 *   - 锚点规则（`cert_doc_template_anchor`）：确定性落笔，逐处 `{AnchorCode: value}`
 *   - 全文规则（本页）：把整份文档 + 结构化企业信息交给模型，让它一次性产出「值」
 *   引擎先跑锚点（便宜、可重放），未覆盖的部分才可能走全文。
 *
 * 【为什么必须显示「当前生效版本」】
 *   同一 `PromptCode` 下可以有多个版本（v1 draft / v2 active / v3 …）。
 *   实施人员看到 3 行却不知道运行期会用哪一个，就会「改了没生效」。
 *   顶部横幅直接给出后端 `resolve` 的**唯一选取口径**结果：
 *   ① 机构更具体优先 → ② `IsDefault` 优先 → ③ `Version` 新优先。
 *
 * 【★ 2026-10-04 修「先有鸡还是先有蛋」】
 *   旧版只在 `promptCode` 非空时才有内容，而**新模板的 `FillPromptCode` 必然是空的**
 *   ⇒ ① 父页把页签写成 `:disabled="!promptCode"` ⇒ 页签点不开；
 *     ② 即便点开，面板里只有「版本表 + 设为默认」，**没有任何新建/挂接入口**。
 *   现在补上两条破环路径：
 *   - **挂接已有**：从 `DocFillPrompt/codes` 拉现有 `PromptCode` 选一个
 *   - **新建**：直接建 v1（`Version=0` 由后端自增），建成后自动挂到本模板
 *   两者最终都调 `DocTemplate/set-prompt` —— 它**只改 `FillPromptCode` 一列**，
 *   ⛔ 不走通用 `update`（那会把全部 `BcFlag` 列提交一遍，漏传的业务键被清空）。
 *
 * 【★ 2026-10-05 用户裁定两条（逐字）】
 *   ① 「全局规则，我建议也是侧边栏」⇒ 编辑区从**页内**搬进 `YzhDrawer`（右侧抽屉），
 *      与「锚点规则」抽屉形态一致。
 *   ② 「为什么全局规则有编码」⇒ **删掉编码输入**，改为**自动生成**。
 *      设计 `41` §8.5.4 本就要求「`PromptCode` 自动生成」+「⛔ 不要求用户先去别的页面
 *      建提示词（打破先有鸡先有蛋）」，而原实现让用户手敲编码 —— **实现偏离设计**。
 *      同时按同一节「`SystemPrompt` 一期用系统默认（角色设定），页面**不暴露**」
 *      删掉系统提示词输入框（原实现把内部实现细节暴露给了业务用户）。
 */
import { CircleClose, Link, Plus, RefreshRight } from '@element-plus/icons-vue'
import {
  addDocFillPrompt,
  getDocFillPromptCodes,
  getDocFillPromptVersions,
  resolveDocFillPrompt,
  setDocFillPromptDefault,
  setDocTemplatePrompt,
  updateDocFillPrompt,
  type DocFillPromptVersion,
} from '@share/api/workflow/doc-fill-rule'
import {
  confirmOrFalse,
  unwrapOk,
  YzhDialog,
  YzhDrawer,
  YzhStatusBadge,
  YzhTable,
  type PageParams,
  type YzhAction,
  type YzhTableColumn,
} from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, inject, nextTick, onMounted, ref, watch } from 'vue'
import type { DocFillRuleLogic } from '../logic'
import MaterialArea from './MaterialArea.vue'
import { genPromptCode } from './promptCode'

const props = defineProps<{
  /** 当前模板 Code（挂接/解绑的对象） */
  templateCode: string
  /** 已挂接的提示词编码；空串 = 未挂接 */
  promptCode: string
  /** 模板所属机构（提示词「机构更具体优先」的选取依据） */
  orgCode: string
  /** 模板名（对话框标题用） */
  templateName: string
}>()

const logic = inject<DocFillRuleLogic>('logic')

/**
 * ★ 本模板的锚点清单 —— 素材区书签、已引用清单、非法引用校验的**唯一来源**。
 *
 * 【为什么本组件自取，而不是由父页 `:anchors` 传进来】
 *   父页原来传的是 `anchorTabRef?.rows || []`，即通过函数 ref 去读**另一个页签内部**的
 *   `defineExpose`。而两个页签在父页是 `v-if / v-else-if` **互斥**的：切到本页签时
 *   `AnchorRuleTab` 已被卸载、函数 ref 被置 `null` ⇒ 传进来的 `anchors` **恒为 `[]`**。
 *   后果是三处一起错：
 *     ① 素材区「锚点引用」永远显示「无锚点」；
 *     ② 已引用清单把所有 `{{__FILL__.x}}` 判为未知 token（标红）；
 *     ③ 非法引用校验把**每一个**合法锚点都报成「模板中不存在」。
 *   改为自取后，本组件不再依赖任何兄弟组件的挂载时序。
 */
const anchors = ref<any[]>([])

async function loadAnchors() {
  if (!logic || !props.templateCode) {
    anchors.value = []
    return
  }
  try {
    anchors.value = await logic.loadAnchors()
  } catch {
    // 锚点取不到只影响「书签 / 校验」的完整性，⛔ 不该把整个面板打挂
    anchors.value = []
  }
}

/** 挂接/解绑成功 ⇒ 通知父页刷新左树徽标 */
const emit = defineEmits<{ bound: [promptCode: string] }>()

const tableRef = ref<any>(null)
const loading = ref(false)
const resolved = ref<any>(null)
const total = ref(0)

const fillTokenSample = '{{__FILL__.锚点}}'

const hasPrompt = computed(() => !!props.promptCode)

const columns: YzhTableColumn<DocFillPromptVersion>[] = [
  { prop: 'Version', label: '版本', width: 80, slot: true },
  { prop: 'PromptName', label: '名称', minWidth: 200 },
  { prop: 'Status', label: '状态', width: 100, slot: true },
  { prop: 'IsDefault', label: '默认', width: 90, slot: true },
  { prop: 'Temperature', label: '温度', width: 80 },
  { prop: 'MaxTokens', label: '最大 tokens', width: 110 },
  { prop: 'UpdateTime', label: '更新时间', width: 180 },
]

/** 提示词引用校验 */
const invalidPromptRefs = computed(() => {
  if (!logic) return []
  return logic.validatePromptAnchors(editForm.value.UserTemplate, anchors.value)
})

/**
 * 客户端分页。
 *
 * ⚠️ `versions` 是**一次性全量**（含已软删）—— 同一 `PromptCode` 的版本数在个位数，
 * 不值得为它加服务端分页。分页只在本组件内做。
 */
async function dataLoader(params: PageParams) {
  if (!hasPrompt.value) return { rows: [], total: 0 }
  const data = unwrapOk(
    await getDocFillPromptVersions(props.promptCode, props.orgCode),
    '加载提示词版本失败',
  )
  const all = data?.Items ?? []
  total.value = all.length
  const page = params.page || 1
  const rows = params.rows || 20
  return { rows: all.slice((page - 1) * rows, page * rows), total: all.length }
}

/**
 * 编辑抽屉开关（`v-model` 直接绑 `YzhDrawer`）。
 *
 * ⚠️ `currentVersion` 是「正在编辑哪一行」的**唯一来源** —— 抽屉标题、保存载荷都读它。
 */
const isEditing = ref(false)
const currentVersion = ref<DocFillPromptVersion | null>(null)
/**
 * 编辑表单。
 *
 * ⛔ **不含 `SystemPrompt`**：设计 `41` §8.5.4 裁定「一期用系统默认（角色设定），
 *    页面不暴露」。不放进表单 ⇒ 保存载荷里它仍取 `...currentVersion` 的原值（整行提交，
 *    见 §8.5.4 的「必须提交整行」告警），既不改动也不清空。
 */
const editForm = ref({
  PromptName: '',
  UserTemplate: '',
  Temperature: 0.7,
  MaxTokens: 2000,
})

/** 开始编辑某个版本 ⇒ 打开右侧抽屉 */
function onEditVersion(row: DocFillPromptVersion) {
  currentVersion.value = row
  editForm.value = {
    PromptName: row.PromptName,
    UserTemplate: row.UserTemplate || '',
    Temperature: row.Temperature ?? 0.7,
    MaxTokens: row.MaxTokens ?? 2000,
  }
  isEditing.value = true
}

/** 抽屉关干净后清掉编辑对象（下次点同一行才会重新初始化表单） */
function onEditorClosed() {
  currentVersion.value = null
}

async function onSaveVersion() {
  if (!currentVersion.value) return
  saving.value = true
  try {
    const payload = {
      ...currentVersion.value,
      ...editForm.value,
    }
    unwrapOk(await updateDocFillPrompt(payload), '保存失败')
    ElMessage.success('提示词已保存')
    isEditing.value = false
    await refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

/** 行按钮：已是默认 → 禁用；已软删 → 只读 */
function rowActions(row: DocFillPromptVersion): YzhAction[] {
  if (row.IsDeleted) return [{ key: 'deleted', text: '已删除', disabled: true }]
  const actions: YzhAction[] = [{ key: 'edit', text: '编辑', type: 'primary' }]
  if (!row.IsDefault) {
    actions.push({ key: 'setDefault', text: '设为默认' })
  } else {
    actions.push({ key: 'default', text: '当前默认', disabled: true })
  }
  return actions
}

async function handleRowAction(key: string, row: DocFillPromptVersion) {
  if (key === 'edit') {
    onEditVersion(row)
    return
  }
  if (key !== 'setDefault') return
  const ok = await confirmOrFalse(
    `把「${row.PromptName}」（v${row.Version}）设为默认？\n\n` +
      '同一 PromptCode 下默认位是排他的 —— 原默认版本会被自动取消。',
    '设为默认',
    {
      type: 'warning',
      confirmButtonText: '设为默认',
      cancelButtonText: '取消',
    },
  )
  if (!ok) return
  unwrapOk(await setDocFillPromptDefault(row), '设置失败')
  ElMessage.success('已设为默认')
  await refresh()
}

/** 生效版本横幅：与版本表一起刷新，避免「横幅说 v2、表里 v2 已不是默认」 */
async function loadResolved() {
  if (!hasPrompt.value) {
    resolved.value = null
    return
  }
  try {
    resolved.value = unwrapOk(
      await resolveDocFillPrompt(props.promptCode, props.orgCode),
      '解析生效版本失败',
    )
  } catch {
    resolved.value = null
  }
}

async function refresh() {
  loading.value = true
  try {
    await Promise.all([loadResolved(), tableRef.value?.refresh()])
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadResolved()
  loadAnchors()
})

// 切文件/切模板 ⇒ 重新解析生效版本 + 重取锚点
// （版本表由 YzhTable 的 dataLoader 自己重载）
watch(
  () => `${props.templateCode}|${props.promptCode}|${props.orgCode}`,
  () => {
    loadResolved()
    loadAnchors()
  },
)

/* ========================================================
   挂接 / 新建 对话框
   ======================================================== */

type BindMode = 'pick' | 'create'

const bindVisible = ref(false)
const bindMode = ref<BindMode>('pick')
const saving = ref(false)

/** 可选提示词（`DocFillPrompt/codes` 聚合结果） */
interface PromptCodeOption {
  PromptCode: string
  PromptName?: string
  VersionCount: number
  MaxVersion: number
  Orgs: string[]
  ActiveVersion: number | null
}
const codeOptions = ref<PromptCodeOption[]>([])
const codesLoading = ref(false)
const pickedCode = ref('')

/**
 * 新建表单。
 *
 * ⛔ 没有「编码」字段 —— `PromptCode` 由 `genPromptCode()` 自动生成（设计 §8.5.4）。
 * ⛔ 没有「系统提示词」字段 —— 一期用系统默认（同节）。
 */
const newName = ref('')
const newUserTemplate = ref('')

/** 编辑模式那个裸 `<textarea>` 的 DOM 引用（插入素材时要读/摆光标） */
const userTemplateEl = ref<HTMLTextAreaElement | null>(null)
/** 「新建提示词」对话框里「用户模板」textarea 的 DOM 引用（由 `@focus` 捕获） */
const createTemplateEl = ref<HTMLTextAreaElement | null>(null)

function onCreateTemplateFocus(e: FocusEvent) {
  createTemplateEl.value = e.target as HTMLTextAreaElement
}

/**
 * 把 `val` 插到 `el` 当前光标处。
 *
 * 【为什么不再 `el.value = x` + `dispatchEvent(new Event('input'))`】
 *   那是**绕过 Vue 响应式**的写法：值直接进了 DOM，靠伪造一个原生 `input` 事件
 *   让 `v-model` 事后同步。副作用是任何监听 `input` 的地方都会被惊动一次，
 *   而且一旦绑定方式从 `v-model` 换成别的，这段代码会**静默失效**（插进去就没了）。
 *   正确做法是**改状态**，再在 `nextTick` 之后把光标按新状态摆回去。
 *
 * 【为什么没有光标时追加到末尾而不是放弃】
 *   用户可能压根没点过 textarea 就直接点素材 chip。此时「插到哪」没有答案，
 *   追加到末尾是唯一不会丢内容的选择。
 */
function insertInto(
  el: HTMLTextAreaElement | null | undefined,
  current: string,
  val: string,
  apply: (next: string) => void,
) {
  const hasCursor =
    !!el && typeof el.selectionStart === 'number' && el.isConnected
  const start = hasCursor ? (el as HTMLTextAreaElement).selectionStart : current.length
  const end = hasCursor ? (el as HTMLTextAreaElement).selectionEnd : current.length
  apply(current.slice(0, start) + val + current.slice(end))
  if (!hasCursor) return
  nextTick(() => {
    const node = el as HTMLTextAreaElement
    node.focus()
    node.setSelectionRange(start + val.length, start + val.length)
  })
}

/**
 * 素材 chip → 光标处插入。
 *
 * 【为什么写入目标必须由调用点指定】
 *   原实现把内容插进「最后一个获得焦点的输入框」（`lastFocusedInput`）。
 *   但「新建提示词」对话框里**也有一个素材区**，而它旁边还有「编码 / 名称」两个输入框 ——
 *   用户在「名称」里点了光标、再点一个素材 chip，token 就被插进了**名称**里。
 *   ⇒ 目标字段必须显式给定，⛔ 不能靠「最后焦点」猜。
 */
function onMaterialSelect(val: string, target: 'edit' | 'create' = 'edit') {
  if (target === 'create') {
    insertInto(createTemplateEl.value, newUserTemplate.value, val, (n) => {
      newUserTemplate.value = n
    })
    return
  }
  insertInto(userTemplateEl.value, editForm.value.UserTemplate, val, (n) => {
    editForm.value.UserTemplate = n
  })
}

/** 已引用清单 (V6)：从 UserTemplate 提取 {{token}} 并标注是否已知 */
const promptRefs = computed(() => {
  const text = editForm.value.UserTemplate || ''
  const matches = [...new Set(text.match(/\{\{[^{}]{1,60}\}\}/g) || [])]

  // 收集已知 token：① 本模板锚点 ② 全局参数（这里简化处理，认为 params 加载后即可知）
  // ⚠️ 实际运行中 params 可能还在加载，但在编辑态通常已经有了。
  const knownTokens = new Set<string>()
  anchors.value.forEach((a) => {
    knownTokens.add(`{{__FILL__.${a.AnchorRef}}}`)
  })
  // 系统方法
  knownTokens.add('{{__NOW__}}')
  knownTokens.add('{{__PROFILE__.}}')

  return matches.map((t) => ({
    token: t,
    known: knownTokens.has(t) || /\{\{[A-Z0-9_]+\}\}/.test(t), // 简单判定全局参数
  }))
})

/**
 * ★ 自动生成 `PromptCode`（设计 `41` §8.5.4：「`PromptCode` 自动生成」）。
 *
 * 生成规则与后端约束（`^[a-z][a-z0-9_]{1,49}$`、不接受空编码）全部收在
 * `./promptCode.ts` —— 那里有单测钉住，⛔ 不要在本组件里再写一份。
 */
async function loadCodeOptions() {
  codesLoading.value = true
  try {
    const data = unwrapOk(await getDocFillPromptCodes(), '加载提示词清单失败')
    codeOptions.value = data?.Items ?? []
  } catch (e: any) {
    codeOptions.value = []
    ElMessage.error(e?.message || '加载提示词清单失败')
  } finally {
    codesLoading.value = false
  }
}

function openBind(mode: BindMode) {
  if (!props.templateCode) {
    ElMessage.warning('请先在左侧选择一个已上传空白模板的文件')
    return
  }
  bindMode.value = mode
  pickedCode.value = ''
  newName.value = props.templateName ? `${props.templateName} 全文填写` : ''
  newUserTemplate.value = ''
  bindVisible.value = true
  if (mode === 'pick') loadCodeOptions()
}

/** 挂接已有：选中的 code 直接写回模板 */
async function confirmPick() {
  if (!pickedCode.value) {
    ElMessage.warning('请选择一个提示词')
    return
  }
  saving.value = true
  try {
    unwrapOk(
      await setDocTemplatePrompt(props.templateCode, pickedCode.value),
      '挂接失败',
    )
    ElMessage.success(`已挂接「${pickedCode.value}」`)
    bindVisible.value = false
    emit('bound', pickedCode.value)
    await refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '挂接失败')
  } finally {
    saving.value = false
  }
}

/**
 * 新建：先建 v1，再挂到本模板。
 *
 * ⚠️ 两步**不是事务**（两个不同实体，跨控制器）。若第二步失败，
 * 提示词已存在但没挂上 —— 这时提示用户「提示词已建好，可在『挂接已有』里选它」，
 * 而不是假装整体成功。
 */
async function confirmCreate() {
  const code = genPromptCode()
  const name = newName.value.trim()
  if (!name) {
    ElMessage.warning('请填写名称')
    return
  }
  saving.value = true
  try {
    unwrapOk(
      await addDocFillPrompt({
        PromptCode: code,
        PromptName: name,
        UserTemplate: newUserTemplate.value.trim() || undefined,
      }),
      '新建提示词失败',
    )

    try {
      unwrapOk(await setDocTemplatePrompt(props.templateCode, code), '挂接失败')
    } catch (e: any) {
      ElMessage.warning(
        `提示词「${code}」已建好，但挂接失败：${e?.message || ''}。可在「挂接已有」里选它`,
      )
      bindVisible.value = false
      await loadCodeOptions()
      return
    }

    ElMessage.success(`已新建并挂接「${code}」v1（草稿）`)
    bindVisible.value = false
    emit('bound', code)
    await refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '新建提示词失败')
  } finally {
    saving.value = false
  }
}

/** 解绑：空串 = 该模板不走全文规则 */
async function onUnbind() {
  const ok = await confirmOrFalse(
    `解绑后本模板将只走「锚点规则」填充，不再使用全文提示词。\n\n提示词本身不会被删除，可随时再挂接。`,
    '解绑提示词',
    { type: 'warning', confirmButtonText: '解绑', cancelButtonText: '取消' },
  )
  if (!ok) return
  try {
    unwrapOk(await setDocTemplatePrompt(props.templateCode, ''), '解绑失败')
    ElMessage.success('已解绑')
    emit('bound', '')
    await refresh()
  } catch (e: any) {
    ElMessage.error(e?.message || '解绑失败')
  }
}

defineExpose({ refresh })
</script>

<template>
  <div class="prompt-panel">
    <!-- 未挂提示词 -->
    <div v-if="!hasPrompt" class="prompt-empty">
      <p class="prompt-empty__title">该模板未挂「全文填写提示词」</p>
      <p class="prompt-empty__desc">
        不挂也能用 —— 只走<strong>锚点规则</strong>逐处填充。
        需要让模型按上下文通篇组织内容时，再挂一个提示词。
      </p>
      <div class="prompt-empty__actions">
        <el-button type="primary" :icon="Link" @click="openBind('pick')"
          >挂接已有提示词</el-button
        >
        <el-button type="default" :icon="Plus" @click="openBind('create')"
          >新建提示词</el-button
        >
      </div>
    </div>

    <!-- 列表模式（编辑已搬到右侧抽屉，见文件末尾的 `YzhDrawer`） -->
    <template v-else>
      <el-alert
        type="success"
        :closable="false"
        show-icon
        class="prompt-panel__banner"
      >
        <template #title>
          <template v-if="resolved?.Found">
            当前生效：<strong>{{ resolved.Picked.PromptName }}</strong> （v{{
              resolved.Picked.Version
            }}
            · {{ resolved.Picked.Status }}）
            <span class="prompt-panel__banner-sub">
              · 同 PromptCode 下共 {{ resolved.CandidateCount }} 个候选版本
            </span>
          </template>
          <template v-else>
            未找到可用版本（<code>{{ promptCode }}</code
            >）—— 运行期将<strong>不</strong>走全文规则。
          </template>
        </template>
      </el-alert>

      <YzhTable
        ref="tableRef"
        :columns="columns"
        :data-loader="dataLoader"
        :row-action-buttons="rowActions"
        row-key="Code"
        :page-size="10"
        empty-text="该提示词还没有任何版本"
        @row-action="handleRowAction"
      >
        <template #column-Version="{ row }">
          <YzhStatusBadge
            :type="row.IsDefault ? 'success' : 'info'"
            :text="`v${row.Version}`"
          />
        </template>

        <template #column-Status="{ row }">
          <YzhStatusBadge
            :type="
              row.Status === 'active'
                ? 'success'
                : row.Status === 'draft'
                  ? 'warning'
                  : 'info'
            "
            :text="row.Status"
          />
        </template>

        <template #column-IsDefault="{ row }">
          <YzhStatusBadge v-if="row.IsDefault" type="success" text="默认" />
          <span v-else class="prompt-panel__muted">—</span>
        </template>

        <template #toolbar-left>
          <el-button
            type="default"
            :icon="RefreshRight"
            :loading="loading"
            @click="refresh"
            >刷新</el-button
          >
          <el-button type="default" :icon="Link" @click="openBind('pick')"
            >换绑</el-button
          >
          <el-button type="danger" plain @click="onUnbind">解绑</el-button>
          <span class="prompt-panel__meta">
            已挂接 <code>{{ promptCode }}</code> · 共
            {{ total }} 个版本（含已删除）
          </span>
        </template>
      </YzhTable>
    </template>

    <!--
      ── 编辑抽屉（★ 用户 2026-10-05 裁定：「全局规则，我建议也是侧边栏」）──
      形态与「锚点规则」抽屉一致：标题 / 遮罩 / 底部按钮全部由 `YzhDrawer` 提供，
      本组件只负责内容（素材区 + 提示词 + 已引用清单 + 采样参数）。
      `size` 取 820px —— 提示词是长句，比锚点抽屉（620px）更吃宽度。
    -->
    <YzhDrawer
      v-model="isEditing"
      :title="
        currentVersion
          ? `全文填写规则 · ${editForm.PromptName} (v${currentVersion.Version})`
          : '全文填写规则'
      "
      size="820px"
      confirm-text="保存规则"
      :confirm-loading="saving"
      @confirm="onSaveVersion"
      @closed="onEditorClosed"
    >
      <div v-if="currentVersion" class="pmpage">
        <!-- 上：素材区（显式声明写入目标，见 `onMaterialSelect` 注释） -->
        <div class="pm-mats">
          <MaterialArea
            :anchors="anchors"
            @select="(v) => onMaterialSelect(v, 'edit')"
          />
        </div>

        <!-- 下：编辑区 -->
        <div class="pm-ed">
          <div class="hint">
            使用光标处插入素材，或直接编写提示词。支持
            {{ fillTokenSample }} 引用锚点。
          </div>
          <textarea
            ref="userTemplateEl"
            v-model="editForm.UserTemplate"
            placeholder="在此输入 AI 全文填写提示词模板..."
          ></textarea>

          <div v-if="invalidPromptRefs.length" class="prompt-error">
            <el-icon><CircleClose /></el-icon>
            <span
              >非法引用锚点：{{
                invalidPromptRefs.join('、')
              }}（模板中不存在）</span
            >
          </div>

          <!-- 已引用清单 (V6) -->
          <div class="pm-ref">
            <span v-if="promptRefs.length" class="refhd">已引用：</span>
            <span
              v-for="t in promptRefs"
              :key="t.token"
              class="refchip"
              :class="{ unk: !t.known }"
              :title="t.known ? '可识别' : '⚠️ 未知 token'"
            >
              {{ t.token }}
            </span>
          </div>
        </div>

        <div class="pm-ed__footer">
          <div class="pm-ed__item">
            <label>温度</label>
            <el-input-number
              v-model="editForm.Temperature"
              size="small"
              :min="0"
              :max="1"
              :step="0.1"
            />
          </div>
          <div class="pm-ed__item">
            <label>最大 Tokens</label>
            <el-input-number
              v-model="editForm.MaxTokens"
              size="small"
              :min="100"
              :max="8000"
              :step="500"
            />
          </div>
        </div>
      </div>
    </YzhDrawer>

    <!-- ── 挂接 / 新建 对话框（★ 用 YzhDialog，法条 S07 禁裸 el-dialog）── -->
    <YzhDialog
      v-model="bindVisible"
      :title="bindMode === 'pick' ? '挂接已有提示词' : '新建提示词'"
      width="640px"
      :confirm-text="bindMode === 'pick' ? '挂接' : '新建并挂接'"
      :confirm-loading="saving"
      @confirm="bindMode === 'pick' ? confirmPick() : confirmCreate()"
    >
      <template v-if="bindMode === 'pick'">
        <p class="bind-hint">
          选择后，本模板的
          <code>FillPromptCode</code> 会被指向该提示词（只改这一列）。
        </p>
        <el-select
          v-model="pickedCode"
          filterable
          clearable
          class="bind-select"
          :loading="codesLoading"
          placeholder="搜索并选择提示词"
        >
          <el-option
            v-for="o in codeOptions"
            :key="o.PromptCode"
            :label="`${o.PromptCode} · ${o.PromptName || '(未命名)'} · ${o.VersionCount} 版`"
            :value="o.PromptCode"
          >
            <span class="bind-option">
              <code>{{ o.PromptCode }}</code>
              <span class="bind-option__name">{{
                o.PromptName || '(未命名)'
              }}</span>
              <span class="bind-option__meta">
                {{ o.VersionCount }} 版<template v-if="o.ActiveVersion">
                  · 生效 v{{ o.ActiveVersion }}</template
                >
                <template v-else>· 无 active 默认版</template>
              </span>
            </span>
          </el-option>
        </el-select>
        <p
          v-if="!codesLoading && codeOptions.length === 0"
          class="bind-hint bind-hint--warn"
        >
          系统里还没有任何提示词 —— 请改用「新建提示词」。
        </p>
      </template>

      <template v-else>
        <p class="bind-hint">
          新建的是<strong>第 1 版（草稿）</strong>；版本号由后端自增，
          <strong>编码由系统自动生成</strong>（无需填写）。
          提示词落<strong>全局作用域</strong>（机构级差异化请在「提示词工作台」另建）。
        </p>

        <div class="bind-material">
          <MaterialArea
            :anchors="anchors"
            @select="(v) => onMaterialSelect(v, 'create')"
          />
        </div>

        <el-form label-width="96px" label-position="right">
          <el-form-item label="名称" required>
            <el-input
              v-model="newName"
              placeholder="如 质量手册全文填写"
            />
          </el-form-item>
          <el-form-item label="用户模板">
            <el-input
              v-model="newUserTemplate"
              type="textarea"
              :rows="4"
              placeholder="含 {{__FILL__.xxx}} 占位符的用户模板，可留空后在工作台补写"
              @focus="onCreateTemplateFocus"
            />
          </el-form-item>
        </el-form>
      </template>
    </YzhDialog>
  </div>
</template>

<style scoped>
.prompt-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.prompt-panel__banner {
  margin-bottom: var(--yzh-space-2, 8px);
  flex-shrink: 0;
}

.prompt-panel__banner-sub {
  font-weight: 400;
  opacity: 0.75;
}

.prompt-panel__muted {
  color: var(--yzh-color-text-placeholder, #c0c4cc);
}

.prompt-panel__meta {
  margin-left: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
  line-height: 32px;
}

.hint {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-text-placeholder, #909399);
  line-height: 1.6;
  margin: 0 0 var(--yzh-space-1, 4px);
}

/* ── 未挂提示词的空状态 ── */
.prompt-empty {
  margin: auto;
  max-width: 460px;
  padding: var(--yzh-space-6, 24px);
  text-align: center;
}
.prompt-empty__title {
  margin: 0 0 var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: 500;
  color: var(--yzh-color-text-primary, #303133);
}
.prompt-empty__desc {
  margin: 0 0 var(--yzh-space-4, 16px);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: 1.6;
  color: var(--yzh-color-text-placeholder, #909399);
}
.prompt-empty__actions {
  display: flex;
  justify-content: center;
  gap: 8px;
}

/*
 * ── 编辑抽屉内容（上下结构：上素材 / 下编辑，设计 `41` §8.5.6）──
 * `flex:1 + min-height:0`：抽屉内容区（`.yzh-drawer__body`）已是 `height:100%` 的
 * flex 列，这里必须撑满，`textarea` 的 `flex:1` 才拿得到剩余高度。
 */
.pmpage {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
}

.pm-mats {
  border: 1px solid var(--yzh-color-border-light, #e4e7ed);
  border-radius: var(--yzh-radius-md, 8px);
  background: var(--yzh-color-bg-subtle, #fbfcfe);
  padding: var(--yzh-space-1, 5px) var(--yzh-space-2, 9px);
  flex-shrink: 0;
}

.pm-ed {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 7px;
  min-height: 0;
}

.pm-ed textarea {
  width: 100%;
  flex: 1;
  min-height: 200px;
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  resize: none;
  padding: var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border, #dcdfe6);
  border-radius: var(--yzh-radius-md, 8px);
  outline: none;
  background: var(--yzh-color-bg-container, #fff);
  color: var(--yzh-color-text-primary, #303133);
}
.pm-ed textarea:focus {
  border-color: var(--yzh-color-primary, #409eff);
  box-shadow: 0 0 0 2px var(--yzh-color-primary-light-9, #ecf5ff);
}

.pm-ref {
  display: flex;
  flex-wrap: wrap;
  gap: 5px;
  align-items: center;
  min-height: 24px;
  padding: var(--yzh-space-1, 4px) 0;
}
.pm-ref .refhd {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-text-placeholder, #909399);
}
.refchip {
  font-size: var(--yzh-font-size-xs, 11px);
  padding: var(--yzh-space-1, 2px) var(--yzh-space-2, 8px);
  border-radius: 12px;
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  background: var(--yzh-color-primary-light-9, #ecf5ff);
  color: var(--yzh-color-primary, #409eff);
  border: 1px solid var(--yzh-color-primary-light-8, #d9ecff);
}
.refchip.unk {
  background: var(--yzh-color-danger-light-9, #fef0f0);
  color: var(--yzh-color-danger, #f56c6c);
  border-color: var(--yzh-color-danger-light-8, #fde2e2);
}

.pm-ed__footer {
  flex-shrink: 0;
  padding: var(--yzh-space-3, 12px) 0;
  border-top: 1px dashed var(--yzh-color-border-light, #ebeef5);
  display: flex;
  align-items: center;
  gap: 20px;
}
.pm-ed__item {
  display: flex;
  align-items: center;
  gap: 8px;
}
.pm-ed__item label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
}

.prompt-error {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-danger, #f56c6c);
  padding: var(--yzh-space-2, 6px) var(--yzh-space-3, 12px);
  background: var(--yzh-color-danger-light-9, #fef0f0);
  border: 1px solid var(--yzh-color-danger-light-8, #fde2e2);
  border-radius: var(--yzh-radius-sm, 4px);
}

/* 对话框 */
.bind-hint {
  margin: 0 0 var(--yzh-space-2, 10px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
  line-height: 1.6;
}
.bind-hint code {
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  background: var(--yzh-color-bg-subtle, #f5f7fa);
  padding: 0 var(--yzh-space-1, 3px);
  border-radius: 3px;
}
.bind-select {
  width: 100%;
}
.bind-material {
  margin-bottom: var(--yzh-space-3, 12px);
}

/*
 * ★ 以下 4 条此前**只有类名没有样式**（`bind-option` / `bind-option__name` /
 *   `bind-option__meta` / `bind-hint--warn` 在 style 块里都查不到）：
 *   - 下拉项退化成「编码 名称 3 版」三段裸文字挤在一起，看不出主次，
 *     长名称也不会省略号截断（把版本数挤出可视区）；
 *   - 「系统里还没有任何提示词 —— 请改用『新建提示词』」这句**关键引导**
 *     和普通提示同色，用户很容易忽略。
 */
.bind-option {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  min-width: 0;
}
.bind-option code {
  flex-shrink: 0;
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  color: var(--yzh-color-primary, #1e3a8a);
}
.bind-option__name {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: var(--yzh-color-text-primary, #303133);
}
.bind-option__meta {
  flex-shrink: 0;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
}
.bind-hint--warn {
  color: var(--yzh-color-warning, #d97706);
}
</style>
