<script setup lang="ts">
/**
 * 文档语义规则 —— 提示词工作台（2026-10-02 二次重构）
 *
 * 菜单：MENU_00210 / 业务管理 · 规则定义 · 文档语义规则（OrderNo 250，URL 与 Controller 均不变）
 * 后端：/api/PromptTemplate（⚠️ 路由名永不改名 —— 改名 = ApiCode 断链）
 *
 * ┌───────────────────────────────────────────────────────────────┐
 * │ WorkbenchBar：作用域 › 类型切换 │ AI生成 恢复 保存 专注        │  ← 共享操作条
 * ├──────────┬────────────────────────────────────────────────────┤
 * │          │ PromptEditor（宽度 = 全部剩余，纵向长文阅读）        │
 * │ 标准树   ├══════ 可拖动分隔（上下分栏，比例持久化）═══════     │
 * │（可折叠）│ PromptTestPanel（上传 + 结构化结果，5 个页签）      │
 * └──────────┴────────────────────────────────────────────────────┘
 *
 * 布局决策（35 号评估）：
 *   重构前是「左树 + 中编辑 + 右测试」三栏。实测中栏仅 474px ≈ 34 字/行，
 *   而提示词正文有 1500–2500 字 —— 长文被压成窄条；测试是偶发动作却永久占 410px。
 *   改为上下分栏后同时满足「正文够宽」与「边改边看结果」，左树与结果区均可折叠。
 *
 * 三条业务铁律（2026-10-02 裁决）：
 *  ① 左栏固定根「全部标准」= 平台级默认；子节点 = 具体标准；节点下标 [分类] [作用]
 *  ② 测试 = 真实上传 → 分析 → 改提示词 → 再分析（迭代）
 *  ③ Markdown 落 Redis 复用（8h），换文件才重新转换 —— 「转完即弃」是错的
 *
 * 保存闸门（P5 硬拦）：没测过 / 测的正文与当前不一致 / 分析输出不合规 → 禁止保存。
 * ⛔ 界面不展示 AI 模型：模型由 cert_sys_config 统一固定，UI 不可选也不展示。
 */
import { ref, computed, onMounted, watch, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { YzhPageLayout } from '@yzh-core'
import {
  PROMPT_TYPE,
  getStandardOptions,
  listPrompts,
  resolveActivePrompt,
  generatePromptDraft,
  testPrompt,
  savePrompt,
  type StandardOptionDto,
  type PromptTemplateDto,
  type PromptTestResultDto
} from '@share/api/workflow/prompt-workbench'
import WorkbenchBar from './components/WorkbenchBar.vue'
import StandardTree from './components/StandardTree.vue'
import PromptEditor from './components/PromptEditor.vue'
import PromptTestPanel from './components/PromptTestPanel.vue'
import {
  PROMPT_TYPES,
  findScopeRow,
  makePromptCode,
  makePromptName,
  normScope,
  isBlankTemplate,
  ROOT_SCOPE
} from './logic'

// ==================== 持久化的视图偏好 ====================

const LS_KEY = 'yzh.prompt-workbench.view'
const view = ref({
  treeCollapsed: false,
  resultOpen: true,
  resultRatio: 0.45
})
try {
  const raw = localStorage.getItem(LS_KEY)
  if (raw) view.value = { ...view.value, ...JSON.parse(raw) }
} catch {
  /* 忽略：偏好读不出来就用默认值 */
}
watch(view, (v) => localStorage.setItem(LS_KEY, JSON.stringify(v)), { deep: true })

// ==================== ★ 草稿暂存（2026-10-03：修「切换即丢」） ====================

/**
 * 未保存正文的本地草稿（localStorage）。
 *
 * ⚠️ **为什么必须有这一层**：`loadCurrent()` 会按「作用域 + 类型」重新载入正文，
 *   而 AI 生成的结果**只存在 `templateText` 内存里**（`generatePromptDraft` 刻意不落库）
 *   ⇒ 用户点完「AI 生成」再切一个类型页签，一次几十秒 + 一次 token 换来的正文**静默消失**。
 *   实测复现：选「食品标准」→ AI 生成 → 切到「作用提示词」→ 编辑器空。
 *
 * 存 localStorage 而不是 Redis / DB：草稿是**本机编辑态**，不该污染服务端数据；
 * 且刷新页面、误关标签页后仍能恢复。
 */
const DRAFT_KEY = 'yzh.prompt-workbench.drafts'
type DraftMap = Record<string, string>

/** 草稿键 = 类型 + 作用域。⚠️ 平台级 scope 是空串，用固定串做 key 以免出现 `doc_group|` 这种易错键 */
function draftId(type: string, scopeCode: string): string {
  return `${type}|${scopeCode || '__platform__'}`
}

const drafts = ref<DraftMap>({})
try {
  const raw = localStorage.getItem(DRAFT_KEY)
  if (raw) drafts.value = JSON.parse(raw) as DraftMap
} catch {
  /* 草稿读不出来就当没有，不影响主流程 */
}
function persistDrafts() {
  try {
    localStorage.setItem(DRAFT_KEY, JSON.stringify(drafts.value))
  } catch {
    /* 配额满 / 隐私模式：忽略 —— 草稿是尽力而为，⛔ 不能因此中断编辑 */
  }
}
function dropDraft(type: string, scopeCode: string) {
  const key = draftId(type, scopeCode)
  if (key in drafts.value) {
    delete drafts.value[key]
    persistDrafts()
  }
}

/** 本次载入是否恢复了一份未保存草稿（供状态条提示） */
const draftRestored = ref(false)

/**
 * 载入期间为 true：此时的 `templateText` 赋值是「同步库内值」，⛔ 不能当用户编辑去存草稿。
 * （不加这个开关的话，切作用域时刚载入的库内值会被当成编辑写入草稿，越滚越乱）
 */
let syncing = false
let draftTimer: number | undefined

// ⚠️ 草稿监听必须放在「数据」声明**之后** —— 它引用 templateText / savedTemplate /
//    activeType / scope，放前面会 TS2448「used before its declaration」。
//    见下方「数据」节末尾。


// ==================== 数据 ====================

const standards = ref<StandardOptionDto[]>([])
/** 两类提示词的全量行（供树徽章 + 取本作用域 promptCode） */
const allRows = ref<PromptTemplateDto[]>([])

/** 当前作用域 code：'' = 全部标准（平台级） */
const scope = ref('')
const activeType = ref<string>(PROMPT_TYPE.Group)

/** resolve 结果（标准级 → 平台级回退），仅用于展示与取原值 */
const resolved = ref<PromptTemplateDto | null>(null)
const templateText = ref('')
const savedTemplate = ref('')

/** Markdown 缓存键（跨类型复用 —— 同一批文件） */
const cacheKey = ref('')
const result = ref<PromptTestResultDto | null>(null)
/** **最后一次测试通过时**的正文；与 templateText 不一致 = 闸门关闭 */
const testedTemplate = ref<string | null>(null)

const loading = ref(false)
const generating = ref(false)
const testing = ref(false)
const saving = ref(false)
const focused = ref(false)

const treeWidth = ref(240)
const editorArea = ref<HTMLElement>()

/**
 * 草稿落盘：正文变化 700ms 后写入 localStorage。
 *
 * ⚠️ 位置说明：必须放在 `templateText` / `savedTemplate` / `activeType` / `scope`
 *   声明**之后**，否则 TS2448「used before its declaration」。
 *   `syncing` 开关见上方「草稿暂存」节。
 */
watch(templateText, (v) => {
  if (syncing) return
  window.clearTimeout(draftTimer)
  draftTimer = window.setTimeout(() => {
    if (v === savedTemplate.value) {
      // 与库内一致 ⇒ 草稿已无意义，清掉，避免下次载入用旧草稿盖掉库内新值
      dropDraft(activeType.value, scope.value)
    } else {
      drafts.value[draftId(activeType.value, scope.value)] = v
      persistDrafts()
    }
  }, 700)
})


// ==================== 计算 ====================

const typeDef = computed(
  () => PROMPT_TYPES.find((t) => t.type === activeType.value) || PROMPT_TYPES[0]
)

/** 当前节点显示名（空 = 平台级，落在根上） */
const nodeLabel = computed(() => {
  if (!scope.value) return ''
  // ★ 比对键 = `code`（GUID）—— 与 wf_prompt_template.StandardCode 同一空间
  const hit = standards.value.find((x) => normScope(x.code) === scope.value)
  return hit?.standardName || hit?.display || scope.value
})

/** 本作用域**恰好**有没有行（决定保存是覆盖还是新建） */
const scopeRow = computed(() => findScopeRow(allRows.value, activeType.value, scope.value))

const scopeHint = computed(() => {
  if (!resolved.value) return '本作用域暂无提示词，保存后新建'
  if (normScope(resolved.value.standardCode) !== normScope(scope.value))
    return '显示平台级默认，保存将新建本标准版本'
  return ''
})

const dirty = computed(() => templateText.value !== savedTemplate.value)

/**
 * 正文是否有内容（★ 2026-10-03）。
 * ⚠️ 与 `dirty` 是**两件事**：`dirty` = 与库内不一致；`hasContent` = 正文非空。
 *   AI 按钮的文案与 `onGenerate` 的分支都取决于它 —— 原先按钮用 `dirty` 判断，
 *   导致「正文已保存但非空」时按钮写「AI 生成」、实际走「优化」，文案与行为不符。
 */
const hasContent = computed(() => !isBlankTemplate(templateText.value))

/** ★ P5 保存闸门（硬拦） */
const saveBlockedReason = computed(() => {
  if (isBlankTemplate(templateText.value)) return '正文为空'
  if (result.value && !result.value.success) return `分析未通过：${result.value.message}`
  if (!testedTemplate.value) return '未测试'
  if (testedTemplate.value !== templateText.value) return '已修改，需重新测试'
  return ''
})

// ==================== 加载 ====================

async function loadStandards() {
  try {
    standards.value = await getStandardOptions()
  } catch (e: any) {
    ElMessage.error('加载标准列表失败：' + (e?.message || '未知错误'))
  }
}

async function loadRows() {
  try {
    const [g, c] = await Promise.all([
      listPrompts(PROMPT_TYPE.Group),
      listPrompts(PROMPT_TYPE.Content)
    ])
    allRows.value = [...g, ...c]
  } catch (e: any) {
    ElMessage.error('加载提示词列表失败：' + (e?.message || '未知错误'))
  }
}

/**
 * 按「作用域 + 类型」重新定位并载入正文；同时清掉旧的测试结论。
 *
 * ★ 2026-10-03：载入时**先看有没有未保存草稿**。有且与库内不一致 ⇒ 恢复草稿，
 *   并把 `savedTemplate` 留在库内值上（⇒ `dirty = true`，用户能看出还没落库）。
 *   这是「切走再切回来内容还在」的关键一步。
 */
async function loadCurrent() {
  loading.value = true
  syncing = true
  try {
    const r = await resolveActivePrompt(activeType.value, scope.value || undefined)
    resolved.value = r
    const fromDb = r?.template || ''
    const draft = drafts.value[draftId(activeType.value, scope.value)]
    if (draft !== undefined && draft !== fromDb) {
      templateText.value = draft
      savedTemplate.value = fromDb
      draftRestored.value = true
    } else {
      templateText.value = fromDb
      savedTemplate.value = fromDb
      draftRestored.value = false
    }
    testedTemplate.value = null
    result.value = null
  } catch {
    resolved.value = null
    templateText.value = ''
    savedTemplate.value = ''
    draftRestored.value = false
    testedTemplate.value = null
    result.value = null
  } finally {
    loading.value = false
    // ⚠️ 必须等 watch 跑完再解除同步标记，否则这次赋值会被当成用户编辑写进草稿
    await nextTick()
    syncing = false
  }
}

watch([scope, activeType], () => {
  void loadCurrent()
})

// ==================== ★ 切换守卫（2026-10-03） ====================

const treeComp = ref<InstanceType<typeof StandardTree>>()

type SwitchChoice = 'save' | 'draft' | 'stay'

/**
 * 切走前询问。三选一：**保存并切换 / 暂存草稿并切换 / 留在本页**。
 * 点 X 或 Esc = 「留在本页」—— 破坏性动作默认不执行。
 */
async function askBeforeSwitch(): Promise<SwitchChoice> {
  try {
    await ElMessageBox.confirm(
      '当前正文有未保存的改动。切换后会自动保留为草稿，随时可以切回来继续编辑。',
      '有未保存改动',
      {
        confirmButtonText: '保存并切换',
        cancelButtonText: '暂存草稿并切换',
        distinguishCancelAndClose: true,
        type: 'warning'
      }
    )
    return 'save'
  } catch (action) {
    return action === 'cancel' ? 'draft' : 'stay'
  }
}

/**
 * 切作用域（树节点）。被拒绝时把树高亮拨回来，否则界面显示已切换、内容却没换。
 *
 * ★ 2026-10-03：选「保存并切换」而**保存失败**时，同样**不切**。
 *   原实现不检查 `doSave` 返回值 —— 保存失败会弹红字「保存失败」，界面却照样切走，
 *   用户看到「失败 + 已切换」只会认为内容丢了（其实还在草稿里）。留在本页让他先处理。
 */
async function onSelectScope(code: string) {
  if (code === scope.value) return
  if (dirty.value) {
    const choice = await askBeforeSwitch()
    if (choice === 'stay') {
      treeComp.value?.resync()
      return
    }
    if (choice === 'save') {
      const ok = await doSave({ bypassGate: true })
      if (!ok) {
        treeComp.value?.resync()
        return
      }
    }
  }
  scope.value = code
}

/** 切类型（分类 / 作用）。被拒绝时不需要回拨 —— 单选组是受控的，值没变就自动弹回 */
async function onSelectType(v: string) {
  if (v === activeType.value) return
  if (dirty.value) {
    const choice = await askBeforeSwitch()
    if (choice === 'stay') return
    if (choice === 'save') {
      // 同 onSelectScope：保存失败就不切，避免「报错却已切走」的错觉
      const ok = await doSave({ bypassGate: true })
      if (!ok) return
    }
  }
  activeType.value = v
}

onMounted(async () => {
  await Promise.all([loadStandards(), loadRows()])
  await loadCurrent()
  await nextTick()
})

// ==================== AI 生成 / 优化（Q4 两用） ====================

async function onGenerate() {
  const has = hasContent.value
  let extra: string | null = null
  try {
    const { value } = await ElMessageBox.prompt(
      has
        ? dirty.value
          ? // ★ 2026-10-03：AI 优化是**破坏性替换** —— 生成结果会自动落库，
            //   用户手改的那份既不在库里、也会被 dropDraft 清掉。原实现一字不提，
            //   等于「点一下优化，手改的内容静默消失」。这里在唯一的输入弹窗里说清楚。
            '当前正文有未保存的改动 —— AI 会以它为输入重新生成，并整体替换正文（原改动不会单独留存）。优化方向：'
          : '优化方向（留空 = 按原意优化）'
        : '补充要求（留空 = 自动生成）',
      has ? '优化' : '生成',
      {
        confirmButtonText: '开始',
        cancelButtonText: '取消',
        inputPlaceholder: '可留空',
        inputPattern: /^[\s\S]{0,500}$/,
        inputErrorMessage: '最多 500 字'
      }
    )
    extra = (value || '').trim() || null
  } catch {
    return
  }

  generating.value = true
  try {
    const r = await generatePromptDraft({
      promptType: activeType.value,
      standardCode: scope.value || undefined,
      extraRequirement: extra,
      currentTemplate: has ? templateText.value : null
    })
    if (!r.prompt || !r.prompt.trim()) {
      ElMessage.warning('AI 未返回内容，请重试')
      return
    }
    templateText.value = r.prompt
    // 正文变了 → 旧测试结论作废，必须重新测试
    testedTemplate.value = null
    result.value = null
    view.value.resultOpen = true

    // ★ 2026-10-03 用户明确要求：「ai 自动生成的时候，进行自动保存」。
    //   动机：生成结果原先只存内存，切作用域/类型即静默丢失（已实测复现）。
    //   自动落库后 dirty 归零，切换守卫不再触发，内容也不会丢。
    //   ⚠️ 绕过 P5 闸门是刻意的：刚生成的内容必然「未测试」，若被闸门拦下，
    //      自动保存就永远失败，等于没做。保存 ≠ 值得信任 —— 生效与否仍由测试结果说话。
    const saved = await doSave({ bypassGate: true, silent: true })
    if (saved) {
      ElMessage.success(has ? '已生成优化稿并自动保存' : '已生成草稿并自动保存')
    } else {
      ElMessage.warning('已生成，但自动保存失败 —— 内容已保留在编辑器并自动暂存为草稿')
    }
  } catch (e: any) {
    ElMessage.error('AI 生成失败：' + (e?.message || '未知错误'))
  } finally {
    generating.value = false
  }
}

// ==================== 测试（迭代闭环） ====================

async function onTest(files: File[]) {
  if (!files.length && !cacheKey.value) {
    ElMessage.warning('请先选择要测试的文件')
    return
  }
  view.value.resultOpen = true
  testing.value = true
  try {
    const r = await testPrompt({
      files,
      promptType: activeType.value,
      template: templateText.value || undefined,
      standardCode: scope.value || undefined,
      cacheKey: cacheKey.value || undefined
    })
    result.value = r
    if (r.cacheKey) cacheKey.value = r.cacheKey
    if (r.success) {
      testedTemplate.value = templateText.value
      ElMessage.success(r.message || '分析通过')
    } else {
      ElMessage.error(r.message || '分析失败')
    }
  } catch (e: any) {
    ElMessage.error('分析失败：' + (e?.message || '未知错误'))
  } finally {
    testing.value = false
  }
}

// ==================== 保存 ====================

/**
 * 保存正文（唯一落库入口）。
 *
 * @param bypassGate true = 跳过 P5 闸门（供「AI 生成后自动保存」与「保存并切换」使用）
 * @param silent     true = 不弹成功提示（调用方自己组织文案，避免一条操作弹两条消息）
 *
 * ★ 2026-10-03 闸门由「硬拦」改为「软提示」：
 *   原实现直接把保存按钮 `disabled`，导致「有改动 → 想保存 → 被拦 → 卡死」。
 *   现改为弹确认框（「未测试，仍要保存吗？」），保留安全意识但不再阻塞操作流。
 *   依据：用户裁决「测试频率不高、同一文件会反复调提示词」—— 硬拦会打断调试闭环。
 */
async function doSave(opts: { bypassGate?: boolean; silent?: boolean } = {}): Promise<boolean> {
  if (isBlankTemplate(templateText.value)) {
    ElMessage.warning('正文为空，无法保存')
    return false
  }
  if (!opts.bypassGate && saveBlockedReason.value) {
    try {
      await ElMessageBox.confirm(`${saveBlockedReason.value}。仍要保存吗？`, '未通过测试闸门', {
        confirmButtonText: '仍然保存',
        cancelButtonText: '取消',
        type: 'warning'
      })
    } catch {
      return false
    }
  }

  saving.value = true
  try {
    const row = scopeRow.value
    await savePrompt({
      promptCode: row?.promptCode || makePromptCode(activeType.value, scope.value),
      promptName: row?.promptName || makePromptName(activeType.value, nodeLabel.value || ROOT_SCOPE.label),
      promptType: activeType.value,
      standardCode: scope.value || null,
      skillTarget: row?.skillTarget ?? null,
      template: templateText.value,
      description: row?.description || typeDef.value.desc
    })
    savedTemplate.value = templateText.value
    // ★ 已落库 ⇒ 草稿作废（否则下次切回来会拿旧草稿覆盖刚保存的内容）
    dropDraft(activeType.value, scope.value)
    draftRestored.value = false
    if (!opts.silent) ElMessage.success('保存成功')
    await loadRows()
    return true
  } catch (e: any) {
    if (!opts.silent) ElMessage.error('保存失败：' + (e?.message || '未知错误'))
    return false
  } finally {
    saving.value = false
  }
}

async function onSave() {
  await doSave()
}

function onReset() {
  templateText.value = savedTemplate.value
  // 恢复 = 放弃本地编辑 ⇒ 草稿一并清掉，避免「点了恢复、切走再切回又冒出来」
  dropDraft(activeType.value, scope.value)
  draftRestored.value = false
}

/** 保存状态（供操作条显示） */
const saveState = computed<'saved' | 'unsaved' | 'draft'>(() => {
  if (!dirty.value) return 'saved'
  return draftRestored.value ? 'draft' : 'unsaved'
})

// ==================== 分栏拖拽 ====================

/** 左树宽度 */
function startResizeTree(e: MouseEvent) {
  const startX = e.clientX
  const startW = treeWidth.value
  const onMove = (ev: MouseEvent) => {
    treeWidth.value = Math.min(400, Math.max(180, startW + ev.clientX - startX))
  }
  const onUp = () => {
    document.removeEventListener('mousemove', onMove)
    document.removeEventListener('mouseup', onUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
  }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
  document.body.style.cursor = 'col-resize'
  document.body.style.userSelect = 'none'
}

/** 上下分栏：编辑器 / 结果区 比例 */
function startResizeResult(e: MouseEvent) {
  const box = editorArea.value?.getBoundingClientRect()
  if (!box) return
  const startY = e.clientY
  const startRatio = view.value.resultRatio
  const onMove = (ev: MouseEvent) => {
    const delta = ev.clientY - startY
    // 往下拖 = 结果区变小
    const next = startRatio - delta / box.height
    view.value.resultRatio = Math.min(0.85, Math.max(0.15, next))
  }
  const onUp = () => {
    document.removeEventListener('mousemove', onMove)
    document.removeEventListener('mouseup', onUp)
    document.body.style.cursor = ''
    document.body.style.userSelect = ''
  }
  document.addEventListener('mousemove', onMove)
  document.addEventListener('mouseup', onUp)
  document.body.style.cursor = 'row-resize'
  document.body.style.userSelect = 'none'
}
</script>

<template>
  <YzhPageLayout no-padding>
    <!-- ★ 共享操作条走框架标准工具栏插槽：内边距 / 边框 / 高度由
         `YzhPageLayout.__toolbar` 统一提供（12px 20px），⛔ 不自造 chrome ——
         自造会与全站其他页面产生 44px vs 56px、14px vs 20px 的尺寸漂移。 -->
    <template #toolbar>
      <WorkbenchBar
        :root-label="ROOT_SCOPE.label"
        :node-label="nodeLabel"
        :scope-hint="scopeHint"
        :types="PROMPT_TYPES"
        :active-type="activeType"
        :dirty="dirty"
        :has-content="hasContent"
        :save-state="saveState"
        :generating="generating"
        :saving="saving"
        :save-blocked-reason="saveBlockedReason"
        :focused="focused"
        @update:active-type="onSelectType"
        @generate="onGenerate"
        @save="onSave"
        @reset="onReset"
        @toggle-focus="focused = !focused"
      />
    </template>

    <div class="wb">
      <div class="wb__body" v-loading="loading" element-loading-text="载入提示词…">
        <!-- 左：标准树（专注模式下隐藏） -->
        <template v-if="!focused">
          <div class="wb__left" :style="{ width: treeWidth + 'px' }">
            <StandardTree
              ref="treeComp"
              :model-value="scope"
              :standards="standards"
              :rows="allRows"
              @select="(v) => onSelectScope(v.code)"
            />
          </div>
          <div class="wb__handle" @mousedown.prevent="startResizeTree">
            <div class="wb__bar" />
          </div>
        </template>

        <!-- 中：编辑器（上） + 结果区（下） -->
        <div ref="editorArea" class="wb__main">
          <div class="wb__editor" :style="view.resultOpen ? { flex: `1 1 ${(1 - view.resultRatio) * 100}%` } : { flex: '1 1 100%' }">
            <PromptEditor v-model="templateText" :type-def="typeDef" :dirty="dirty" />
          </div>

          <template v-if="view.resultOpen">
            <div class="wb__handle wb__handle--h" @mousedown.prevent="startResizeResult">
              <div class="wb__bar" />
            </div>
            <div class="wb__result" :style="{ flex: `1 1 ${view.resultRatio * 100}%` }">
              <PromptTestPanel
                v-model:cache-key="cacheKey"
                :prompt-type="activeType"
                :standard-code="scope"
                :testing="testing"
                :result="result"
                @run="onTest"
              />
            </div>
          </template>
        </div>
      </div>
    </div>
  </YzhPageLayout>
</template>

<style scoped>
.wb {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  height: 100%;
  background: var(--yzh-color-bg-container);
}

.wb__body {
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}

.wb__left {
  flex-shrink: 0;
  min-width: 180px;
  max-width: 400px;
  border-right: 1px solid var(--yzh-color-border-light);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}

.wb__main {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.wb__editor,
.wb__result {
  min-height: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
}
.wb__result {
  background: var(--yzh-color-bg-container);
}

/* 拖拽把手 */
.wb__handle {
  width: 4px;
  flex-shrink: 0;
  cursor: col-resize;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color var(--yzh-transition-base);
}
.wb__handle:hover,
.wb__handle:active {
  background: var(--el-color-primary-light-9);
}
.wb__handle--h {
  width: auto;
  height: 4px;
  cursor: row-resize;
}
.wb__bar {
  width: 2px;
  height: 40px;
  background: var(--yzh-color-border);
  border-radius: 9999px;
  transition: all var(--yzh-transition-base);
}
.wb__handle--h .wb__bar {
  width: 40px;
  height: 2px;
}
.wb__handle:hover .wb__bar {
  background: var(--yzh-color-primary-light);
  height: 50px;
}
.wb__handle--h:hover .wb__bar {
  width: 50px;
  height: 2px;
}
</style>
