<script setup lang="ts">
/**
 * ★ 锚点规则抽屉 —— 「点一个锚点 → 在右侧抽屉里配它的属性」
 *
 * 【★ 2026-10-09 结构定稿：严格三段，⛔ ①② 不得合并】
 *   用户原话：「其实针对一个锚点，分为 3 部分我觉得合理，1、先选择来源，
 *   2、根据不同的来源设置不同的属性（ai 节点需要设置有无参数，ai 的提示词等），
 *   3、最后是设置填写规则…… 而不是将 1 和 2 合并在一起来设计」。
 *
 *   ⇒ ① **选来源**（6 选 1，卡片）
 *      ② **来源属性**（随来源不同而不同；AI 节点 = 固定 3 属性）
 *      ③ **填写规则**（覆盖 / 填充 + 值类型 + 必填 + 空值兜底 + 备注）
 *
 * 【★ 本轮删掉的东西（⛔ 不要加回来）】
 *   - **「组合方式」整层**（顺序回退 / 拼接 / 模板套用）—— 用户：
 *     「没有顺序回退 / 拼接 / 模板套用这种选项，我们的规则是必须正确的，
 *      没有想当然的，有问题我们改程序」。
 *   - **「企业资料画像」来源** —— 用户从未要求过该概念。
 *   - **来源链列表 / 拖拽排序 / 「缺失时行为」下拉** —— 「一个锚点 = 一个来源」后
 *     这些控件全部失去意义。
 *
 * 【★ AI 节点 = 固定 3 属性（顺序不可换）】
 *   ① 参数（无 / 有 —— 有 ⇒ 就是全局参数）
 *   ② 是否依赖企业资料（不依赖 ⇒ 只用「参数 + 提示词」生成，如 xxx 规则 / xxx 内容）
 *   ③ 提示词
 *
 * 【★ 锚点定位（原「①」）降级为只读条】
 *   它不承载配置，只是「你在配哪个位置」，所以**不占段号**。
 */
import { listFillParamDefs, saveAnchorBatch } from '@share/api/workflow/doc-fill-rule'
import { unwrapOk, YzhDrawer, YzhStatusBadge } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, nextTick, ref, watch } from 'vue'
import {
  aiParamRefs,
  cardOf,
  DEFAULT_WRITE_MODE,
  DOC_INFO_ITEMS,
  emptySourceSpec,
  ENTERPRISE_ATTRS,
  findParam,
  humanPreview,
  isAiKind,
  newEntry,
  ORG_SYS_ATTRS,
  parseSourceSpec,
  SOURCE_KINDS,
  stringifySourceSpec,
  validateSource,
  WRITE_MODES,
  type SourceSpecModel,
} from './sourceSpec'

const props = defineProps<{
  /** 抽屉开关（`v-model:visible`） */
  visible: boolean
  /** 锚点整行（PascalCase） */
  anchor: any | null
  templateCode: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
  (e: 'close'): void
  /** 抽屉离场动画结束后 —— 由父页用它清掉 `anchor`（避免下次点同一行不重置表单） */
  (e: 'closed'): void
}>()

const VALUE_TYPES = ['text', 'number', 'date', 'bool', 'enum']

/**
 * 操作方式 —— **只读展示**（⛔ 不让人选）。
 *
 * ⚠️ 与 ③「填写规则」是**两件不同的事**，⛔ 不要混淆：
 *   - **操作方式**（本项）= 落笔到**哪个容器**（单元格 / 表格区域）—— 由锚点类型**推导**；
 *   - **填写规则**（③ 的「覆盖 / 填充」）= 落笔时**怎么处理原有文字** —— 由**用户选**。
 *   原实现把两者混为一谈（`autoOp` 注释里写「操作方式由锚点类型推导」），
 *   本轮把「怎么处理原有文字」独立出来成为 ③ 的第一个控件。
 */
const TABLE_TYPES = ['table', 'table_total']
const OP_LABELS: Record<'cell' | 'table', string> = {
  cell: '单元格更新',
  table: '表格更新',
}
const OP_HINTS: Record<'cell' | 'table', string> = {
  cell: '把值替换进单元格 / 正文 / 页眉里的 {{token}}',
  table: '按行列填充表格区域（行不足克隆末行；多余行写空串，⛔ 不删）',
}

/* ============ 本地状态 ============ */
const model = ref<SourceSpecModel>(emptySourceSpec())
const parseError = ref(false)
/** 旧数据：原配置有多个来源，新模型只保留第 1 个 */
const legacyMultiSource = ref(false)
const droppedCount = ref(0)
const rawSpec = ref('')
const saving = ref(false)

const form = ref({
  /** ★ ③ 填写规则：`overwrite`（覆盖）/ `replace`（填充） */
  WriteMode: DEFAULT_WRITE_MODE,
  ValueType: 'text',
  Required: false,
  DefaultText: '',
  NumberFormat: '',
  FieldCode: '',
  Remark: '',
  /**
   * ★ G5「表格更新」参数（评审 63 号：规则页没有配置起始行/列/工作表的 UI）
   *
   * ⚠️ **UI 是 1 起的 Excel 行/列号，存储（TokenModifiersJson.startRow/StartCol）是 0-based**
   * —— 与 `FillTableSkill.start_row`（0-based）一致；watch 读入时 -1，保存时 +1。
   * 留空 = 不写该键 ⇒ 后端 `TableStartOf` 回落解析 `AnchorRef`（从锚点单元格开始）。
   */
  StartRow: null as number | null,
  StartCol: null as number | null,
  /** Excel 工作表名覆盖（空 = 锚点所在工作表；Word 忽略） */
  Sheet: '',
  /** 列定义（JSON 数组，决定 AI 输出列与写入顺序） */
  ColumnsJson: '',
})

const paramOptions = ref<
  { value: string; label: string; standardCode: string }[]
>([])

/* ============ 派生 ============ */
const title = computed(() => props.anchor?.AnchorRef || '锚点规则')
const anchorType = computed(() => String(props.anchor?.AnchorType || ''))
const isDomainAuto = computed(
  () =>
    anchorType.value === 'domain' &&
    String(props.anchor?.DomainKind || '') === 'auto',
)
/** 操作方式（推导，⛔ 不可选）—— 与 `AnchorRuleTab` 的字段/表格分组同一口径 */
const autoOp = computed<'cell' | 'table'>(() =>
  TABLE_TYPES.includes(anchorType.value) ? 'table' : 'cell',
)

/**
 * ★ G5：表格标签（只读展示）—— Word 走 `{{table:标签}}` 定位，Excel 走列定义对齐。
 * 优先 `FieldCode`（扫描器给的 inner）；空则从 `AnchorRef` 剥 `{{table:…}}`。
 */
const tableTag = computed(() => {
  const fc = String(props.anchor?.FieldCode || '').trim()
  if (fc) return fc
  const ref = String(props.anchor?.AnchorRef || '')
  const m = ref.match(/^\{\{\s*table:([^}|]+?)\s*\}\}$/)
  return m ? m[1] : ref
})

/** 当前锚点是否 Excel 区域（坐标参数只对它有意义；Word 表格走标签定位） */
const isExcelRange = computed(
  () =>
    autoOp.value === 'table' &&
    String(props.anchor?.AnchorKind || '') === 'range',
)

/** 当前来源（单来源模型：`kind === ''` 表示还没选） */
const src = computed(() => model.value.source)
const isAi = computed(() => isAiKind(src.value.kind))

/**
 * ★ 当前选中的**卡片**。
 *
 * ⚠️ 不能用 `src.kind` 直接判 —— 存储 kind 里的 `replace` 在界面上**归在「全局参数」卡片下**
 *   （用户裁决「需要合并」）。卡片高亮 / ② 段渲染条件都必须走 `card`。
 */
const card = computed(() => cardOf(src.value.kind))

/** ★ 阻塞保存的问题（空数组 = 可保存） */
const issues = computed(() => validateSource(model.value))
const preview = computed(() => humanPreview(model.value))

/**
 * ★ AI「加入提示词」按钮列表 —— 把已勾选的全局参数逐个做成可点击 chip。
 * 点一下 ⇒ 在提示词光标处插入 `{{参数编码}}`（48 §2.2 ① / 61 S-1）。
 */
const aiParamList = computed(() =>
  aiParamRefs(src.value).map((r) => ({
    ref: r,
    label: findParam(r, paramOptions.value)?.label ?? r,
  })),
)
const writeModeHint = computed(
  () => WRITE_MODES.find((m) => m.value === form.value.WriteMode)?.hint ?? '',
)

/**
 * ★ 「全局参数」卡片的 ② 段下拉 —— **4 组**。
 *
 * 【为什么 ①② 是硬编码、③④ 是动态】
 *   ①② 对应引擎 `ReplaceResolver` 的**硬编码属性清单**（不是数据库行）⇒ 前端同源硬编码；
 *   ③④ 来自 `cert_fill_param_def`（后台可维护）⇒ 由 `listFillParamDefs()` 拉取。
 *
 * 【★ 只读 / 可覆盖 —— 本项目最易搞错的一处】
 *   ①② `replace` **只读**：企业改档案即改文档，在「企业资料参数」页改了**不生效**；
 *   ③④ `global` **可覆盖**：读企业填的 `cert_fill_param_value`。
 *   组标题上标出来，⛔ 不让用户去猜。
 *
 * ⚠️ 空的动态组**保留显示**（带「库里还没有」占位）——
 *   直接 `.filter(非空)` 会让用户以为「这类参数不存在」，而事实是「表里还没配」。
 */
const paramGroups = computed(() => [
  {
    label: '企业基本信息 · 只读',
    items: ENTERPRISE_ATTRS.map(([value, label]) => ({ value, label })),
  },
  {
    label: '机构 / 系统信息 · 只读',
    items: ORG_SYS_ATTRS.map(([value, label]) => ({ value, label })),
  },
  {
    label: '后台定义的全局参数 · 可覆盖',
    items: paramOptions.value.filter((p) => !p.standardCode),
  },
  {
    label: '标准对应的参数 · 可覆盖',
    items: paramOptions.value.filter((p) => !!p.standardCode),
  },
])

/** 「文档信息」卡片的 ② 段下拉（6 项，走引擎 `headerFooter` 能力） */
const docInfoOptions = computed(() =>
  DOC_INFO_ITEMS.map(([value, label]) => ({ value, label })),
)

/* ============ ★ G5「表格更新」参数（评审 63 号 G5 / 2.4） ============ */
/** 读 `TokenModifiersJson` 为受控表单字段（存储 0-based ⇒ UI 1-based） */
function readTableParams(row: any): {
  StartRow: number | null
  StartCol: number | null
  Sheet: string
  ColumnsJson: string
} {
  let mods: Record<string, unknown> = {}
  try {
    const p = JSON.parse(String(row?.TokenModifiersJson || ''))
    if (p && typeof p === 'object' && !Array.isArray(p)) mods = p
  } catch {
    /* 没配 / 坏 JSON ⇒ 空对象（保存时会以当前表单重建） */
  }
  const asPos = (v: unknown): number | null => {
    const n = Number(v)
    return Number.isInteger(n) && n >= 0 && n <= 1048575 ? n + 1 : null
  }
  return {
    StartRow: asPos(mods.startRow ?? mods.StartRow),
    StartCol: asPos(mods.startCol ?? mods.StartCol),
    Sheet: String(mods.sheet ?? mods.Sheet ?? ''),
    ColumnsJson: String(row?.ColumnsJson || ''),
  }
}

/**
 * 组装保存用的 `TokenModifiersJson` / `ColumnsJson`（仅表格锚点调用）。
 *
 * ⚠️ **合并写**：以行上现有 `TokenModifiersJson` 为底，只增删 startRow/startCol/sheet 三键 ——
 * ⛔ 不整串覆盖（其他键如 `fmt`/`def` 不能被静默抹掉）。
 * 返回 null = 三键全空 ⇒ 存 null（后端回落 `AnchorRef` 解析）。
 */
function buildTableParams(row: any): {
  TokenModifiersJson: string | null
  ColumnsJson: string | null
} {
  let mods: Record<string, unknown> = {}
  try {
    const p = JSON.parse(String(row?.TokenModifiersJson || ''))
    if (p && typeof p === 'object' && !Array.isArray(p)) mods = { ...p }
  } catch {
    /* 空/坏 ⇒ 重建 */
  }
  const put = (key: string, v: unknown) => {
    if (v === undefined || v === null || v === '') delete mods[key]
    else mods[key] = v
  }
  put('startRow', form.value.StartRow == null ? null : form.value.StartRow - 1)
  put('startCol', form.value.StartCol == null ? null : form.value.StartCol - 1)
  put('sheet', form.value.Sheet.trim())

  const keys = Object.keys(mods)
  const columns = form.value.ColumnsJson.trim()
  return {
    TokenModifiersJson: keys.length ? JSON.stringify(mods) : null,
    ColumnsJson: columns || null,
  }
}

/** 列定义是合法的非空对象数组、且每项有 field_code —— 否则 AI 输出列无从对齐 */
function validateColumnsJson(): string | null {
  const text = form.value.ColumnsJson.trim()
  if (!text) return null // 空 = 还没配（后端会记可操作待办，⛔ 不在保存时硬拦）
  let arr: any
  try {
    arr = JSON.parse(text)
  } catch {
    return '列定义不是合法 JSON'
  }
  if (!Array.isArray(arr) || arr.length === 0) return '列定义必须是 JSON 数组'
  const bad = arr.some(
    (c: any) => !c || typeof c !== 'object' || !String(c.field_code || '').trim(),
  )
  if (bad) return '列定义每项都必须有 field_code'
  return null
}

/* ============ 初始化 ============ */
watch(
  () => props.anchor,
  (row) => {
    if (!row) return
    const parsed = parseSourceSpec(row.SourceSpec)
    model.value = parsed.model
    parseError.value = parsed.parseError
    legacyMultiSource.value = parsed.legacyMultiSource
    droppedCount.value = parsed.droppedCount
    rawSpec.value = row.SourceSpec || ''
    // ★ AI 多参数：老行没有 `params` ⇒ 补空数组，否则 `el-select multiple` 会拿到 undefined
    if (
      isAiKind(model.value.source.kind) &&
      !Array.isArray(model.value.source.params)
    )
      model.value.source.params = []
    // ★ ③ 填写规则：旧值可能是 `append`/`remove`（22 号时代的死字段，从未生效）
    //   ⇒ 不在受控值里的归一到默认「覆盖」，避免单选框空选。
    const wm = row.WriteMode || DEFAULT_WRITE_MODE
    form.value = {
      WriteMode: WRITE_MODES.some((m) => m.value === wm)
        ? wm
        : DEFAULT_WRITE_MODE,
      ValueType: row.ValueType || 'text',
      Required: !!row.Required,
      DefaultText: row.DefaultText || '',
      NumberFormat: row.NumberFormat || '',
      FieldCode: row.FieldCode || '',
      Remark: row.Remark || '',
      // ★ G5：表格参数 —— ⚠️ TokenModifiersJson **合并读**（其他键如 fmt/def 原样保留，
      //   ⛔ 不能整串替换 —— 那会静默抹掉别的功能写进去的修饰符）
      ...readTableParams(row),
    }
  },
  { immediate: true },
)

/* ============ 交互 ============ */
/**
 * ① 选来源。
 *
 * ⚠️ 换来源 ⇒ **整个条目重建**（`newEntry`），⛔ 不做字段合并 ——
 *   上一类来源的属性（如 AI 的提示词）对新来源毫无意义，
 *   留着只会让人以为「已经配过了」。
 */
function pickKind(kind: string) {
  // ★ 判据用 `card`（不是 `src.kind`）——
  //   已配 `enterprise.Name`（存储 kind = `replace`）的锚点，点「全局参数」卡片时
  //   `src.kind !== 'global'`，用 `src.kind` 判会**误判成「换了来源」而把配置清空**。
  if (card.value === kind) return
  model.value.source = newEntry(kind)
}

async function loadParams() {
  try {
    const res = await listFillParamDefs()
    paramOptions.value = (res?.data?.Items ?? []).map((p: any) => ({
      value: String(p.ParamCode ?? p.Code ?? ''),
      label: `${p.ParamCode ?? p.Code ?? ''}${p.ParamName ? ` · ${p.ParamName}` : ''}`,
      standardCode: String(p.StandardCode ?? ''),
    }))
  } catch {
    paramOptions.value = []
  }
}

watch(
  () => props.visible,
  (v: boolean) => {
    if (v && paramOptions.value.length === 0) loadParams()
  },
  { immediate: true },
)

/* ============ 「加入提示词」 ============ */
/** 提示词 textarea 引用（插入参数 token 时读光标位置） */
const promptRef = ref<any>(null)

/**
 * 把 `{{参数编码}}` 插到提示词**光标处**（无光标 ⇒ 追加末尾）。
 *
 * ⚠️ 与 `PromptPanel.insertAtCursor` 同一思路：**先改状态**，再在 `nextTick` 摆回光标 ——
 * 直接改 DOM value + 伪造事件是绕过 Vue 响应式的写法，绑定方式一变就静默失效。
 */
function insertParamRef(code: string) {
  const token = `{{${code}}}`
  const el: HTMLTextAreaElement | undefined = promptRef.value?.textarea
  const cur = src.value.prompt ?? ''
  if (!el) {
    src.value.prompt = cur + token
    return
  }
  const start = el.selectionStart ?? cur.length
  const end = el.selectionEnd ?? cur.length
  src.value.prompt = cur.slice(0, start) + token + cur.slice(end)
  nextTick(() => {
    el.focus()
    el.setSelectionRange(start + token.length, start + token.length)
  })
}

/* ============ 保存 ============ */
async function onSave() {
  if (!props.anchor || !props.templateCode) return
  if (issues.value.length > 0) {
    ElMessage.error(issues.value[0])
    return
  }
  // ★ G5：表格列定义先校验再进 saving（校验失败 ⛔ 不能把按钮转成 loading）
  const tableParams =
    autoOp.value === 'table' ? buildTableParams(props.anchor) : null
  if (tableParams) {
    const err = validateColumnsJson()
    if (err) {
      ElMessage.error(err)
      return
    }
  }
  saving.value = true
  try {
    const sourceSpec = stringifySourceSpec(model.value)
    const payload = {
      ...props.anchor,
      SourceSpec: sourceSpec,
      WriteMode: form.value.WriteMode,
      ValueType: form.value.ValueType,
      Required: form.value.Required,
      DefaultText: form.value.DefaultText || null,
      NumberFormat: form.value.NumberFormat || null,
      FieldCode: form.value.FieldCode || null,
      Remark: form.value.Remark || null,
      // ★ G5：仅表格锚点提交（cell 锚点保持行上原值，⛔ 不动这两列）
      ...(tableParams || {}),
    }
    unwrapOk(await saveAnchorBatch(props.templateCode, [payload]), '保存失败')
    ElMessage.success('锚点规则已保存')
    emit('saved')
    // 保存即关闭：抽屉是「点一行 → 配一处」的短事务，留在原地只会让人不确定存没存上
    emit('update:visible', false)
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <YzhDrawer
    :model-value="visible"
    :title="`锚点规则 · ${title}`"
    size="620px"
    confirm-text="保存规则"
    :confirm-disabled="issues.length > 0"
    :confirm-loading="saving"
    @update:model-value="(v: boolean) => emit('update:visible', v)"
    @confirm="onSave"
    @close="emit('close')"
    @closed="emit('closed')"
  >
    <div v-if="anchor" class="anchor-panel">
      <div class="panel-scroll">
        <!-- ── 锚点定位（只读条，⛔ 不占段号）── -->
        <div class="loc-info">
          <div class="loc-item">
            <label>锚点</label>
            <span class="loc-ref">{{ anchor.AnchorRef }}</span>
          </div>
          <div class="loc-item">
            <label>类型</label>
            <YzhStatusBadge
              :type="anchorType === 'domain' ? 'info' : 'success'"
              :text="anchorType"
            />
            <YzhStatusBadge
              v-if="anchor.DomainKind"
              type="warning"
              :text="anchor.DomainKind"
              class="ml4"
            />
          </div>
          <div class="loc-item">
            <label>位置</label>
            <span>{{
              anchor.SheetName ||
              (anchor.HeaderKind ? `页眉页脚(${anchor.HeaderKind})` : '正文')
            }}</span>
          </div>
          <div class="loc-item">
            <label>语义字段</label>
            <el-input
              v-model="form.FieldCode"
              size="small"
              placeholder="字段编码"
              :disabled="isDomainAuto"
            />
          </div>
        </div>

        <!--
          ★ 原值无法解析 / 旧多来源数据 的提示。
          `parseSourceSpec` 对半截 JSON 会**静默回落空模型**（绝不抛异常，否则面板打不开），
          于是「来源配置其实坏了」这件事在界面上完全看不出来 ——
          用户只会看到「未配置来源」，随手配一个一保存，**原值就被覆盖没了**。
        -->
        <el-alert
          v-if="parseError"
          type="warning"
          show-icon
          :closable="false"
          title="原有来源配置无法解析"
          class="parse-alert"
        >
          <template #default>
            <div class="parse-alert__body">
              <span>保存后将以当前界面内容覆盖原值。原值为：</span>
              <code>{{ rawSpec }}</code>
            </div>
          </template>
        </el-alert>

        <!--
          ★ 旧数据（`22` 号时代的多来源链）：新模型只支持 1 个来源。
          ⛔ 绝不静默丢弃 —— 不提示的话，用户以为「只是打开看了一眼」，
             其实多余来源已经在保存时被砍掉了。
        -->
        <el-alert
          v-if="legacyMultiSource"
          type="warning"
          show-icon
          :closable="false"
          :title="`原配置有多个来源，新的规则是「一个锚点 = 一个来源」`"
          class="parse-alert"
        >
          <template #default>
            <div class="parse-alert__body">
              <span
                >已保留**第 1 个**来源，保存将丢弃其余 {{ droppedCount }} 个。请确认第
                1 个是你要的那个。</span
              >
            </div>
          </template>
        </el-alert>

        <!-- ── ① 选来源 ── -->
        <div class="section">
          <div class="section-hd"><span>① 选来源</span></div>
          <div class="kind-grid" role="radiogroup" aria-label="取值来源">
            <button
              v-for="k in SOURCE_KINDS"
              :key="k.value"
              type="button"
              class="kind"
              :class="{ on: card === k.value }"
              role="radio"
              :aria-checked="card === k.value"
              @click="pickKind(k.value)"
            >
              <span class="kl">{{ k.label }}</span>
              <span class="kh">{{ k.hint }}</span>
            </button>
          </div>
          <div v-if="!src.kind" class="hint">
            还没有选来源 —— 运行期该锚点会留空并挂人工待办。
          </div>
        </div>

        <!-- ── ② 来源属性（随来源不同而不同）── -->
        <div v-if="src.kind" class="section">
          <div class="section-hd"><span>② 来源属性</span></div>

          <!-- 全局参数：4 组下拉（企业基本信息 / 机构·系统 为只读，后台参数为可覆盖） -->
          <template v-if="card === 'global'">
            <div class="form-item">
              <label>参数</label>
              <el-select
                v-model="src.ref"
                size="small"
                filterable
                clearable
                placeholder="选一个参数"
                style="width: 100%"
              >
                <el-option-group
                  v-for="g in paramGroups"
                  :key="g.label"
                  :label="g.label"
                >
                  <el-option
                    v-for="p in g.items"
                    :key="p.value"
                    :value="p.value"
                    :label="p.label"
                  />
                  <!-- ★ 空的动态组也要可见：直接隐藏会让用户以为「这类参数不存在」 -->
                  <el-option
                    v-if="!g.items.length"
                    :key="`${g.label}__empty`"
                    :value="`__empty__${g.label}`"
                    label="（库里还没有这类参数）"
                    disabled
                  />
                </el-option-group>
              </el-select>
            </div>
            <div class="hint">
              <b>企业基本信息</b> / <b>机构·系统信息</b>是<b>只读</b>的（直读企业档案，改档案即改文档）；
              <b>后台定义的全局参数</b> / <b>标准对应的参数</b>企业可在专家端「<b>企业资料参数</b>」页覆盖。
            </div>
          </template>

          <!-- 文档信息：6 项（运行期从文档本身取） -->
          <template v-else-if="card === 'headerFooter'">
            <div class="form-item">
              <label>项</label>
              <el-select
                v-model="src.ref"
                size="small"
                clearable
                placeholder="选一项文档信息"
                style="width: 100%"
              >
                <el-option
                  v-for="d in docInfoOptions"
                  :key="d.value"
                  :value="d.value"
                  :label="d.label"
                />
              </el-select>
            </div>
            <div class="hint">
              文档信息是<b>运行期</b>从这份文档本身取的值（编号 / 版本 / 认证阶段 …），⛔ 不需要企业填。
            </div>
          </template>

          <!-- AI 三类：固定 3 属性，顺序不可换 -->
          <template v-else-if="isAi">
            <div class="form-item">
              <label>① 参数</label>
              <el-radio-group v-model="src.hasParam" size="small">
                <el-radio-button :value="false">无</el-radio-button>
                <el-radio-button :value="true">有</el-radio-button>
              </el-radio-group>
            </div>
            <div v-if="src.hasParam" class="form-item full">
              <label>选参数（可多选）</label>
              <el-select
                v-model="src.params"
                size="small"
                multiple
                filterable
                clearable
                placeholder="选全局参数（可多个）"
                style="width: 100%"
              >
                <el-option-group
                  v-for="g in paramGroups"
                  :key="g.label"
                  :label="g.label"
                >
                  <el-option
                    v-for="p in g.items"
                    :key="p.value"
                    :value="p.value"
                    :label="p.label"
                  />
                </el-option-group>
              </el-select>
              <!-- ★ 「加入」按钮：把勾选的全局参数插入提示词（48 §2.2 ① / 61 S-1） -->
              <div v-if="aiParamList.length" class="param-join">
                <span class="pj-hd">加入提示词：</span>
                <el-button
                  v-for="p in aiParamList"
                  :key="p.ref"
                  link
                  type="primary"
                  size="small"
                  :title="`在提示词光标处插入 {{${p.ref}}}`"
                  @click="insertParamRef(p.ref)"
                >
                  {{ p.label }}
                </el-button>
              </div>
            </div>

            <div class="form-item">
              <label>② 依赖企业资料</label>
              <el-switch v-model="src.dependsOnEnterprise" size="small" />
              <span class="sw-note">
                {{
                  src.dependsOnEnterprise
                    ? '会读取企业资料作为输入'
                    : '不读企业资料 —— 只靠「参数 + 提示词」生成'
                }}
              </span>
            </div>

            <div class="form-item full">
              <label>③ 提示词</label>
              <el-input
                ref="promptRef"
                v-model="src.prompt"
                size="small"
                type="textarea"
                :rows="4"
                placeholder="例如：根据企业持有的资质证书，写一段 100 字以内的企业概况；可选参数处插入 {{参数}} 引用全局参数"
              />
            </div>
          </template>

          <!-- 人工填写：无属性 -->
          <template v-else>
            <div class="hint">
              人工填写**没有来源属性** —— 运行期挂人工待办，由审核专家在专家端填写。
            </div>
          </template>
        </div>

        <!-- ── ③ 填写规则 ── -->
        <div class="section">
          <div class="section-hd"><span>③ 填写规则</span></div>

          <div class="form-item">
            <label>写入方式</label>
            <el-radio-group v-model="form.WriteMode" size="small">
              <el-radio-button
                v-for="m in WRITE_MODES"
                :key="m.value"
                :value="m.value"
                >{{ m.label }}</el-radio-button
              >
            </el-radio-group>
          </div>
          <div class="hint">{{ writeModeHint }}</div>

          <div class="form-grid">
            <div class="form-item">
              <label>操作方式</label>
              <div class="auto-op" :title="OP_HINTS[autoOp]">
                {{ OP_LABELS[autoOp] }}
              </div>
            </div>
            <div class="form-item">
              <label>值类型</label>
              <el-select v-model="form.ValueType" size="small">
                <el-option v-for="t in VALUE_TYPES" :key="t" :value="t" :label="t" />
              </el-select>
            </div>
            <div class="form-item">
              <label>必填项</label>
              <el-switch v-model="form.Required" size="small" />
            </div>
            <div class="form-item">
              <label>空值兜底</label>
              <el-input
                v-model="form.DefaultText"
                size="small"
                placeholder="落空时写入"
              />
            </div>
            <div class="form-item full">
              <label>备注</label>
              <el-input
                v-model="form.Remark"
                size="small"
                type="textarea"
                :rows="1"
              />
            </div>
          </div>

          <!--
            ★ G5「表格更新」参数（评审 63 号 2.3/2.4：规则页没有配置起始行/列/工作表的 UI）
              · Word 表格 = 标签定位 + 列定义（坐标忽略）；
              · Excel 区域 = 起始行/列 + 工作表 + 列定义（留空 ⇒ 从锚点单元格开始）。
              · 列定义 = `ColumnsJson` —— 决定 AI 输出列与写入顺序（kind：text/number/date/bool/enum）。
          -->
          <template v-if="autoOp === 'table'">
            <div class="section-hd"><span>表格参数</span></div>
            <div class="form-grid">
              <template v-if="isExcelRange">
                <div class="form-item">
                  <label>起始行</label>
                  <el-input-number
                    v-model="form.StartRow"
                    :min="1"
                    :max="1048576"
                    :controls="false"
                    size="small"
                    placeholder="Excel 行号（从 1 起）"
                  />
                </div>
                <div class="form-item">
                  <label>起始列</label>
                  <el-input-number
                    v-model="form.StartCol"
                    :min="1"
                    :max="16384"
                    :controls="false"
                    size="small"
                    placeholder="列号（B 列 = 2）"
                  />
                </div>
                <div class="form-item">
                  <label>工作表</label>
                  <el-input
                    v-model="form.Sheet"
                    size="small"
                    :placeholder="`留空 = ${anchor.SheetName || '首个工作表'}`"
                  />
                </div>
              </template>
              <div class="form-item">
                <label>表格标签</label>
                <div class="auto-op" title="Word 按 {{table:标签}} 定位；AI 输出写入 tables.标签">
                  {{ tableTag }}
                </div>
              </div>
              <div class="form-item full">
                <label>列定义 ColumnsJson</label>
                <el-input
                  v-model="form.ColumnsJson"
                  size="small"
                  type="textarea"
                  :rows="3"
                  :placeholder='[{"field_code":"train_date","title":"培训日期","kind":"date"}]'
                />
              </div>
            </div>
            <div class="hint">
              起始行/列留空 = 从锚点单元格开始；列定义决定 AI 输出列与写入顺序（kind：
              text/number/date/bool/enum），每项必须有 field_code。
              <template v-if="!isExcelRange"
                >Word 表格按标签定位，坐标参数不适用。</template
              >
            </div>
          </template>
        </div>

        <!-- 运行期人话预览（防错的关键） -->
        <div class="preview-banner">
          <span class="label">运行期逻辑：</span>
          <span class="text">{{ preview }}</span>
        </div>

        <el-alert
          v-for="(c, i) in issues"
          :key="i"
          type="error"
          show-icon
          :title="c"
          class="mt8"
        />
      </div>
    </div>
  </YzhDrawer>
</template>

<style scoped>
/*
 * 抽屉内容根 —— 同时承担两件事：
 *   ① 撑满 `YzhDrawer` 的内容区（`flex:1 + min-height:0`），让 `.panel-scroll` 能自己滚；
 *   ② 局部令牌别名层（`--pri` / `--t1` / `--bd-l` …）—— 下拉/卡片等大量声明都引用它，
 *      声明在这里即可被全部后代继承（CSS 自定义属性按 DOM 继承，与 `scoped` 无关）。
 *   ⚠️ 该别名层是历史遗留（与全项目 `--yzh-*` 令牌**部分重复**），
 *      删留待用户裁决，本次只做「随结构搬家」，⛔ 不顺手清理。
 */
.anchor-panel {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: var(--yzh-color-bg-container, #fff);
  --pri: var(--yzh-color-primary, #409eff);
  --pri-l: var(--yzh-color-primary-light-9, #ecf5ff);
  --pri-b: var(--yzh-color-primary-light-8, #d9ecff);
  --pri-d: var(--yzh-color-primary-dark, #337ecc);
  --t1: var(--yzh-color-text-primary, #303133);
  --t2: var(--yzh-color-text-regular, #606266);
  --t3: var(--yzh-color-text-tertiary, #909399);
  --t4: var(--yzh-color-text-disabled, #c0c4cc);
  --bd: var(--yzh-color-border-input, #dcdfe6);
  --bd-l: var(--yzh-color-border, #e4e7ed);
  --bd-xl: var(--yzh-color-border-light, #ebeef5);
  --bg: var(--yzh-color-bg-page, #f5f7fa);
  --bg2: var(--yzh-color-bg-subtle, #fafafa);
  --r: 4px;
  --mono: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}

.panel-scroll {
  flex: 1;
  overflow-y: auto;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.section-hd {
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 600;
  color: var(--t3);
  margin-bottom: var(--yzh-space-2, 8px);
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 6px);
}
.section-hd::after {
  content: '';
  flex: 1;
  height: 1px;
  background: var(--bd-xl);
}

/* ── 锚点定位（只读条） ── */
.loc-info {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  background: var(--bg);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-2, 10px);
  border-radius: var(--r);
  border: 1px solid var(--bd-l);
}
.loc-item {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--yzh-font-size-xs, 11px);
  min-width: 0;
}
.loc-item label {
  color: var(--t3);
  width: 56px;
  flex-shrink: 0;
  font-weight: 500;
}
.loc-item .loc-ref {
  font-family: var(--mono);
  color: var(--t1);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── ① 来源卡片 ── */
.kind-grid {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.kind {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  text-align: left;
  font-family: inherit;
  padding: var(--yzh-space-2, 7px) var(--yzh-space-3, 11px);
  background: var(--yzh-color-bg-container, #fff);
  border: 1px solid var(--bd-l);
  border-radius: var(--r);
  cursor: pointer;
  transition: all 0.2s;
}
.kind:hover {
  border-color: var(--pri-b);
}
.kind.on {
  border-color: var(--pri);
  background: var(--pri-l);
  box-shadow: 0 0 0 1px var(--pri-b);
}
.kind .kl {
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 600;
  color: var(--t1);
}
.kind.on .kl {
  color: var(--pri-d);
}
.kind .kh {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--t3);
  line-height: 1.5;
}

/* ── 表单 ── */
.form-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: var(--yzh-font-size-xs, 11px);
  margin-bottom: var(--yzh-space-2, 8px);
}
.form-item > label {
  color: var(--t2);
  width: 88px;
  flex-shrink: 0;
  font-weight: 500;
}
.form-item.full {
  align-items: flex-start;
  flex-direction: column;
  gap: 4px;
}
.form-item.full > label {
  width: auto;
}
.form-item.full .el-input,
.form-item.full :deep(.el-textarea) {
  width: 100%;
}
.sw-note {
  color: var(--t3);
  font-size: var(--yzh-font-size-xs, 10px);
}

/* AI「加入提示词」按钮行 —— 紧跟多参数下拉 */
.param-join {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  margin-top: var(--yzh-space-1, 4px);
}
.param-join .pj-hd {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--t3);
}

.hint {
  font-size: var(--yzh-font-size-xs, 11px);
  line-height: 1.6;
  color: var(--t3);
  margin-top: var(--yzh-space-1, 4px);
}
.hint b {
  color: var(--t2);
}

/* ③ 填写规则的次级表单（两列） */
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
  margin-top: var(--yzh-space-2, 8px);
}
.form-grid .form-item {
  margin-bottom: 0;
}
.form-grid .form-item > label {
  width: 50px;
}
.form-grid .form-item.full {
  grid-column: span 2;
}

/*
 * ★ 操作方式（推导，只读）。
 * 视觉上刻意与可编辑控件区分 —— 虚线框 + 浅底 + `cursor:help`（悬停看口径说明），
 * 让人一眼看出「这不是让你选的」。
 */
.auto-op {
  flex: 1;
  min-width: 0;
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  border: 1px dashed var(--bd);
  border-radius: var(--r);
  background: var(--bg2);
  color: var(--t1);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.6;
  cursor: help;
}

.preview-banner {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-2, 10px);
  background: var(--pri-l);
  border-radius: var(--r);
  border: 1px solid var(--pri-b);
  font-size: var(--yzh-font-size-xs, 11px);
  line-height: 1.6;
}
.preview-banner .label {
  color: var(--pri-d);
  font-weight: 600;
  margin-right: var(--yzh-space-1, 4px);
}
.preview-banner .text {
  color: var(--t1);
}

.ml4 {
  margin-left: var(--yzh-space-1, 4px);
}
.mt8 {
  margin-top: var(--yzh-space-2, 8px);
}

/* 「原值无法解析 / 旧多来源」告警 —— 原值以等宽字体整段亮出，长 JSON 换行不溢出 */
.parse-alert {
  margin-bottom: var(--yzh-space-1, 4px);
}
.parse-alert__body {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.6;
}
.parse-alert__body code {
  display: block;
  font-family: var(--mono);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--t1);
  background: var(--bg2);
  border: 1px solid var(--bd-l);
  border-radius: 3px;
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 6px);
  word-break: break-all;
  max-height: 96px;
  overflow: auto;
}

:deep(.el-input--small .el-input__inner) {
  font-size: var(--yzh-font-size-xs, 11px);
}
:deep(.el-select--small .el-select__wrapper) {
  font-size: var(--yzh-font-size-xs, 11px);
}
</style>
