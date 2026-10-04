<script setup lang="ts">
/**
 * ★ 锚点侧边栏 —— 「点一个锚点 → 在侧边栏配它的属性」
 *
 * 【为什么用侧边栏而不是弹窗】
 *   锚点配置是「边看文档边配」的动作：用户需要在**预览还留在视野里**的情况下改规则。
 *   弹窗会盖住文档 ⇒ 改到一半忘了锚点在哪。侧边栏（`el-drawer`）保留预览上下文。
 *
 * 【为什么来源链编辑器单独实现，不复用 `YzhFormDialog`】
 *   `SourceSpec` 不是「一个字段」而是一个**有序列表 + 组合方式 + 每项随 `kind` 变化的第二列**，
 *   EntityConfig 的 `Memo` 列只能给一个裸 JSON 文本框 —— 实施人员手写 JSON 必然出错。
 *
 * 【保存走 `save-batch`（整行 upsert）】
 *   ⚠️ 必须提交**完整行**：`save-batch` 的语义是「items[i] = 该锚点的完整状态」，
 *   未出现的字段会被写成 CLR 默认值（实测：只发 `defaultText` 会把 `required` 静默改回 false）。
 *   ⇒ 本组件用 `{ ...props.anchor, ...本次编辑的字段 }` 提交，⛔ 不做「只发差异」。
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { Delete, Plus } from '@element-plus/icons-vue'
import { unwrapOk, YzhStatusBadge } from '@yzh-core'
import {
  saveAnchorBatch,
  listFillParamDefs,
} from '@share/api/workflow/doc-fill-rule'
import {
  COMBINE_MODES,
  ON_MISSING_OPTIONS,
  SOURCE_KINDS,
  DEFAULT_ON_MISSING,
  emptySourceSpec,
  humanPreview,
  isImplementedKind,
  parseSourceSpec,
  stringifySourceSpec,
  type SourceSpecEntry,
  type SourceSpecModel,
} from './sourceSpec'

const props = defineProps<{
  visible: boolean
  /** 锚点整行（PascalCase，来自 `/filter`）—— 保存时原样回传 */
  anchor: any | null
  templateCode: string
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

const WRITE_MODES = [
  { value: 'overwrite', label: '重写 overwrite', hint: '锚点处为空，直接写入' },
  { value: 'replace', label: '替换 replace', hint: '锚点处已有内容，替换掉它（需留原值快照）' },
  { value: 'append', label: '追加 append', hint: '保留原内容，在锚点后追加' },
  { value: 'remove', label: '删除 remove', hint: '该内容不适用，整块移除（必须挂条件）' },
]
const VALUE_TYPES = ['text', 'number', 'date', 'bool', 'enum']

/* ============ 本地状态 ============ */
const model = ref<SourceSpecModel>(emptySourceSpec())
const parseError = ref(false)
/** 手写 JSON 的原始值（解析失败时保留，避免用户以为「打开就把我的配置清了」） */
const rawSpec = ref('')
const saving = ref(false)
const dragIndex = ref(-1)

const form = ref({
  WriteMode: 'overwrite',
  ValueType: 'text',
  Required: false,
  DefaultText: '',
  NumberFormat: '',
  FieldCode: '',
  Remark: '',
})

/** 全局参数候选（best-effort：取不到就退化成自由输入，⛔ 不阻塞配置） */
const paramOptions = ref<{ value: string; label: string }[]>([])

/* ============ 派生 ============ */
const title = computed(() => props.anchor?.AnchorRef || '锚点规则')
const anchorType = computed(() => String(props.anchor?.AnchorType || ''))
const isDomainAuto = computed(
  () => anchorType.value === 'domain' && String(props.anchor?.DomainKind || '') === 'auto',
)
const isTableTotal = computed(() => anchorType.value === 'table_total')

/** 预览（防错关键） */
const preview = computed(() => humanPreview(model.value))

/** 非法组合提示（22 号 §6.1；后端保存时也会复核） */
const combos = computed(() => {
  const list: string[] = []
  if (isDomainAuto.value && model.value.sources.length > 0) {
    list.push('域自动值（如 PAGE / NUMPAGES）不能配取值来源 —— 页码交给 Word 算。')
  }
  if (isTableTotal.value && model.value.sources.some((s) => s.kind === 'manual')) {
    list.push('合计锚点不能配「人工录入」—— 合计必须能算，人工填必然算错。')
  }
  if (anchorType.value === 'scalar' && form.value.WriteMode === 'remove') {
    list.push('行内标量（scalar）不能用「删除」写入 —— 会破坏排版，应改用 block 锚点。')
  }
  return list
})

/* ============ 初始化 ============ */
watch(
  () => props.anchor,
  (row) => {
    if (!row) return
    const parsed = parseSourceSpec(row.SourceSpec)
    model.value = parsed.model
    parseError.value = parsed.parseError
    rawSpec.value = row.SourceSpec || ''
    form.value = {
      WriteMode: row.WriteMode || 'overwrite',
      ValueType: row.ValueType || 'text',
      Required: !!row.Required,
      DefaultText: row.DefaultText || '',
      NumberFormat: row.NumberFormat || '',
      FieldCode: row.FieldCode || '',
      Remark: row.Remark || '',
    }
    dragIndex.value = -1
  },
  { immediate: true },
)

/* ============ 来源链操作 ============ */
function addSource() {
  model.value.sources.push({ kind: 'global', ref: '', onMissing: DEFAULT_ON_MISSING })
}

function removeSource(i: number) {
  model.value.sources.splice(i, 1)
}

function moveSource(from: number, to: number) {
  if (from < 0 || to < 0 || from === to) return
  const [item] = model.value.sources.splice(from, 1)
  model.value.sources.splice(to, 0, item)
}

function onDragStart(i: number) {
  dragIndex.value = i
}
function onDragOver(e: DragEvent) {
  e.preventDefault()
}
function onDrop(i: number) {
  moveSource(dragIndex.value, i)
  dragIndex.value = -1
}

/**
 * 切换来源类别时清掉不属于新类别的字段。
 *
 * ⚠️ 不清会留下「`kind=manual` 却带着 `field=统一社会信用代码`」这种脏数据，
 * 后端按 `kind` 分派时读不到，但 JSON 里看得见 —— 属于最难查的一类不一致。
 */
function onKindChange(entry: SourceSpecEntry) {
  entry.ref = ''
  entry.field = undefined
  entry.minConfidence = undefined
  entry.onMissing = entry.onMissing || DEFAULT_ON_MISSING
}

/** 某个 `kind` 的选项列表；当前值不在列表里时补一条，避免 el-select 显示成裸值 */
function kindOptions(current: string) {
  const opts = SOURCE_KINDS.map((k) => ({
    value: k.value,
    label: k.implemented ? k.label : `${k.label}（未实现）`,
    disabled: !k.implemented,
  }))
  if (!SOURCE_KINDS.some((k) => k.value === current)) {
    opts.push({ value: current, label: `${current}（未知类别）`, disabled: true })
  }
  return opts
}

/** 载入全局参数候选（best-effort） */
async function loadParams() {
  try {
    const res = await listFillParamDefs()
    const items = res?.data?.Items ?? []
    paramOptions.value = items.map((p: any) => ({
      value: String(p.ParamCode ?? p.Code ?? ''),
      label: `${p.ParamCode ?? p.Code ?? ''}${p.ParamName ? ` · ${p.ParamName}` : ''}`,
    }))
  } catch {
    paramOptions.value = []
  }
}

watch(
  () => props.visible,
  (v) => {
    if (v && paramOptions.value.length === 0) loadParams()
  },
)

/* ============ 保存 ============ */
async function onSave() {
  if (!props.anchor || !props.templateCode) {
    ElMessage.warning('缺少所属模板，无法保存')
    return
  }
  if (combos.value.length > 0) {
    ElMessage.warning('存在非法组合，请先修正（见上方提示）')
    return
  }

  saving.value = true
  try {
    const sourceSpec = stringifySourceSpec(model.value)
    // ★ 整行提交（见文件头注释：save-batch = 整行 upsert）
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
    }
    unwrapOk(await saveAnchorBatch(props.templateCode, [payload]), '保存锚点规则失败')
    ElMessage.success('锚点规则已保存')
    emit('saved')
    emit('update:visible', false)
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

function onClose() {
  emit('update:visible', false)
}
</script>

<template>
  <el-drawer
    :model-value="visible"
    :title="`锚点规则 · ${title}`"
    direction="rtl"
    size="580px"
    :close-on-click-modal="false"
    @update:model-value="emit('update:visible', $event)"
  >
    <div v-if="anchor" class="anchor-panel">
      <!-- ── ① 定位信息（只读：扫描出来的，⛔ 不在这里改）── -->
      <section class="block">
        <h4 class="block__title">锚点定位<span class="block__sub">扫描结果，只读</span></h4>
        <el-descriptions :column="2" size="small" border>
          <el-descriptions-item label="锚点引用">
            <code class="mono">{{ anchor.AnchorRef }}</code>
          </el-descriptions-item>
          <el-descriptions-item label="锚点类型">
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
          </el-descriptions-item>
          <el-descriptions-item label="定位方式">{{ anchor.AnchorKind }}</el-descriptions-item>
          <el-descriptions-item label="位置">
            <span v-if="anchor.SheetName">{{ anchor.SheetName }}</span>
            <span v-else-if="anchor.HeaderKind">页眉/页脚 · {{ anchor.HeaderKind }}</span>
            <span v-else>正文</span>
          </el-descriptions-item>
          <el-descriptions-item label="语义字段" :span="2">
            <el-input
              v-model="form.FieldCode"
              size="small"
              placeholder="须与 cert_doc_field_def.FieldCode 对齐（可留空）"
              :disabled="isDomainAuto"
            />
          </el-descriptions-item>
        </el-descriptions>

        <el-alert v-if="anchor.IsOrphan" type="warning" :closable="false" show-icon class="mt8">
          <template #title>
            该锚点在最近一次扫描中<strong>没有出现</strong>（模板可能已换版）—— 请确认保留还是删除。
          </template>
        </el-alert>
        <el-alert v-if="isDomainAuto" type="info" :closable="false" show-icon class="mt8">
          <template #title>域自动值（PAGE / NUMPAGES 等）由 Word 自行计算，<strong>不需要配取值来源</strong>。</template>
        </el-alert>
      </section>

      <!-- ── ② 取值来源链 ── -->
      <section class="block">
        <h4 class="block__title">
          取值来源链
          <span class="block__sub">顺序 = 回退优先级，可拖拽排序</span>
        </h4>

        <el-alert v-if="parseError" type="error" :closable="false" show-icon class="mb8">
          <template #title>
            原值无法解析为合法 JSON，已按空链展示。保存后将<strong>覆盖</strong>原值。
          </template>
          <template #default>
            <div class="mono raw-spec">{{ rawSpec }}</div>
          </template>
        </el-alert>

        <div class="combine">
          <span class="combine__label">组合方式</span>
          <el-radio-group v-model="model.combine" size="small">
            <el-radio-button v-for="m in COMBINE_MODES" :key="m.value" :value="m.value">
              {{ m.label }}
            </el-radio-button>
          </el-radio-group>
        </div>
        <div class="combine__hint">
          {{ COMBINE_MODES.find((m) => m.value === model.combine)?.hint }}
        </div>

        <div v-if="model.combine === 'concat'" class="param-row">
          <span class="param-row__label">分隔符</span>
          <el-input v-model="model.separator" size="small" placeholder="、" style="width: 120px" />
        </div>
        <div v-if="model.combine === 'template'" class="param-row">
          <span class="param-row__label">小模板</span>
          <el-input
            v-model="model.expr"
            size="small"
            placeholder="例如 {{Name}}（{{CreditCode}}）"
          />
        </div>

        <!-- 来源列表 -->
        <div class="sources">
          <div
            v-for="(s, i) in model.sources"
            :key="i"
            class="source-item"
            :class="{ 'source-item--dragging': dragIndex === i }"
            draggable="true"
            @dragstart="onDragStart(i)"
            @dragover="onDragOver"
            @drop="onDrop(i)"
          >
            <span class="source-item__handle" title="拖拽调整优先级">⠿</span>
            <span class="source-item__index">{{ i + 1 }}</span>

            <el-select
              v-model="s.kind"
              size="small"
              style="width: 150px"
              @change="onKindChange(s)"
            >
              <el-option
                v-for="o in kindOptions(s.kind)"
                :key="o.value"
                :value="o.value"
                :label="o.label"
                :disabled="o.disabled"
              />
            </el-select>

            <!-- 第二列随 kind 变化 -->
            <template v-if="s.kind === 'global'">
              <el-select
                v-model="s.ref"
                size="small"
                filterable
                allow-create
                default-first-option
                placeholder="参数编码"
                style="width: 190px"
              >
                <el-option v-for="p in paramOptions" :key="p.value" :value="p.value" :label="p.label" />
              </el-select>
            </template>
            <template v-else-if="s.kind === 'profile'">
              <el-input v-model="s.ref" size="small" placeholder="文档名" style="width: 120px" />
              <el-input v-model="s.field" size="small" placeholder="字段名" style="width: 130px" />
              <el-input-number
                v-model="s.minConfidence"
                size="small"
                :min="0"
                :max="1"
                :step="0.05"
                :controls="false"
                placeholder="置信≥"
                style="width: 80px"
              />
            </template>
            <template v-else-if="s.kind === 'compute'">
              <el-input v-model="s.ref" size="small" placeholder="表达式，如 SUM_ROWS" style="width: 190px" />
            </template>
            <template v-else-if="s.kind === 'self' || s.kind === 'sibling'">
              <el-input v-model="s.ref" size="small" placeholder="锚点 / 文档引用" style="width: 190px" />
            </template>
            <template v-else>
              <span class="source-item__none">无需参数</span>
            </template>

            <el-select v-model="s.onMissing" size="small" style="width: 118px">
              <el-option
                v-for="o in ON_MISSING_OPTIONS"
                :key="o.value"
                :value="o.value"
                :label="o.label"
                :title="o.hint"
              />
            </el-select>

            <el-button
              size="small"
              :icon="Delete"
              text
              type="danger"
              title="删除该来源"
              @click="removeSource(i)"
            />
          </div>

          <div v-if="!model.sources.length" class="sources__empty">
            还没有来源 —— 运行期该锚点将留空。
          </div>
        </div>

        <el-button size="small" type="default" :icon="Plus" class="mt8" @click="addSource">
          添加来源
        </el-button>

        <!-- ★ 实时人话预览（防错关键） -->
        <div class="preview-box">
          <div class="preview-box__label">运行期行为预览</div>
          <div class="preview-box__text">{{ preview }}</div>
        </div>

        <el-alert v-if="!isImplementedKind(model.sources[0]?.kind || '') && model.sources.length"
          type="warning" :closable="false" show-icon class="mt8">
          <template #title>
            含有<strong>尚未实现</strong>的来源类别 —— 现在配置不会报错，但运行期不会生效。
          </template>
        </el-alert>
      </section>

      <!-- ── ③ 写入属性 ── -->
      <section class="block">
        <h4 class="block__title">写入属性</h4>
        <el-form label-width="92px" label-position="left" size="small">
          <el-form-item label="写入方式">
            <el-select v-model="form.WriteMode" style="width: 100%">
              <el-option
                v-for="w in WRITE_MODES"
                :key="w.value"
                :value="w.value"
                :label="w.label"
                :title="w.hint"
              />
            </el-select>
          </el-form-item>
          <el-form-item label="值类型">
            <el-select v-model="form.ValueType" style="width: 100%">
              <el-option v-for="t in VALUE_TYPES" :key="t" :value="t" :label="t" />
            </el-select>
          </el-form-item>
          <el-form-item label="必填">
            <el-switch v-model="form.Required" />
            <span class="form-hint">开启后「校验」页会把它计入必填统计</span>
          </el-form-item>
          <el-form-item label="空值兜底">
            <el-input v-model="form.DefaultText" placeholder="所有来源都落空时写入的文本" />
          </el-form-item>
          <el-form-item label="格式串">
            <el-input v-model="form.NumberFormat" placeholder="0.00 / yyyy-MM-dd" />
          </el-form-item>
          <el-form-item label="备注">
            <el-input v-model="form.Remark" type="textarea" :rows="2" />
          </el-form-item>
        </el-form>

        <el-alert v-for="(c, i) in combos" :key="i" type="error" :closable="false" show-icon class="mt8">
          <template #title>{{ c }}</template>
        </el-alert>
      </section>
    </div>

    <template #footer>
      <el-button type="default" @click="onClose">取消</el-button>
      <el-button type="primary" :loading="saving" :disabled="combos.length > 0" @click="onSave">
        保存规则
      </el-button>
    </template>
  </el-drawer>
</template>

<style scoped>
.anchor-panel {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-5, 20px);
}

.block__title {
  margin: 0 0 var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  display: flex;
  align-items: baseline;
  gap: var(--yzh-space-2, 8px);
}
.block__sub {
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: var(--yzh-font-weight-normal, 400);
  color: var(--yzh-color-text-secondary, #606266);
}

.mono {
  font-family: var(--yzh-font-family-mono, 'Courier New', monospace);
  font-size: var(--yzh-font-size-xs, 12px);
}
.raw-spec {
  margin-top: var(--yzh-space-2, 8px);
  max-height: 90px;
  overflow: auto;
  word-break: break-all;
}
.ml4 {
  margin-left: var(--yzh-space-1, 4px);
}
.mt8 {
  margin-top: var(--yzh-space-2, 8px);
}
.mb8 {
  margin-bottom: var(--yzh-space-2, 8px);
}

.combine {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
}
.combine__label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  flex-shrink: 0;
}
.combine__hint {
  margin: var(--yzh-space-2, 8px) 0 var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
}

.param-row {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-3, 12px);
}
.param-row__label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  flex-shrink: 0;
}

.sources {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}
.source-item {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 8px);
  border: 1px solid var(--yzh-color-border-light, #f1f5f9);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-container, #fff);
  flex-wrap: wrap;
}
.source-item--dragging {
  opacity: 0.5;
  border-style: dashed;
}
.source-item__handle {
  cursor: grab;
  color: var(--yzh-color-text-placeholder, #a8abb2);
  font-size: var(--yzh-font-size-md, 14px);
  user-select: none;
}
.source-item__index {
  width: 16px;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  text-align: center;
}
.source-item__none {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #a8abb2);
  width: 190px;
}
.sources__empty {
  padding: var(--yzh-space-3, 12px);
  text-align: center;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
  border: 1px dashed var(--yzh-color-border, #e2e8f0);
  border-radius: var(--yzh-radius-sm, 4px);
}

.preview-box {
  margin-top: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-active, #eff6ff);
  border: 1px solid var(--yzh-color-primary-lighter, #3b82f6);
}
.preview-box__label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-primary, #1e3a8a);
  margin-bottom: var(--yzh-space-1, 4px);
}
.preview-box__text {
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-base, 1.6);
  color: var(--yzh-color-text-primary, #303133);
}

.form-hint {
  margin-left: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
}
</style>
