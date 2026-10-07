<script setup lang="ts">
/**
 * ★ 锚点规则抽屉 —— 「点一个锚点 → 在右侧抽屉里配它的属性」
 *
 * 【2026-10-05 用户裁定：从「页内下方展开」改为「右侧抽屉」】
 *   用户原话：「点击配置规则，为什么是在下方进行操作，我们不是用侧边栏更好吗」。
 *   ⇒ 本组件**只负责内容**，抽屉外壳（标题 / 遮罩 / 底部按钮）统一由 `YzhDrawer` 提供，
 *     ⛔ 不再有 `embedded` / `visible` 双模式（那是「页内下半区」时代的产物）。
 *
 * 【为什么底部按钮改用 `YzhDrawer` 的默认页脚】
 *   原实现自带 `.panel-footer`（取消 + 保存规则），与抽屉外壳的页脚是两份实现；
 *   交给 `YzhDrawer` 后，「保存规则」的 loading / disabled 只通过 props 表达，
 *   ⛔ 不需要在内容区再画一遍。
 *
 * 【2026-10-04 UI 升级（保留）】
 *   - 严谨简洁大气：去掉冗余描述，说明文字收纳进 `!` 浮层。
 *   - 提示词分组：支持 AI 来源的批量提示词组（PromptGroup）配置。
 *
 * 【★ C6（2026-10-07）：操作方式改为「自动推导」】
 *   原先这里是一个「写入方式」下拉（`overwrite`/`replace`/`append`/`remove`）—— 那是 `52` 号
 *   清单里的**最后一处「设计走样」**：
 *     ① `WriteMode` 是**死字段**（写入侧从不读，`48` 号 G10 实测）；
 *     ② `51-V1` 原型 / `55` 号「操作区域」都规定：**操作方式由锚点类型推导，⛔ 不让人选**。
 *   ⇒ 换成只读展示「单元格更新 / 表格更新」，分类口径**复用 C4 的 `TABLE_TYPES`**。
 *   ⚠️ 该列仍**原样回写**（`saveAnchorBatch` 整行 upsert，少传一列 = 静默清空）。
 */
import { Delete, Plus } from '@element-plus/icons-vue'
import {
  listFillParamDefs,
  saveAnchorBatch,
} from '@share/api/workflow/doc-fill-rule'
import { unwrapOk, YzhDrawer, YzhStatusBadge } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, ref, watch } from 'vue'
import {
  COMBINE_MODES,
  DEFAULT_ON_MISSING,
  emptySourceSpec,
  humanPreview,
  ON_MISSING_OPTIONS,
  parseSourceSpec,
  SOURCE_KINDS,
  stringifySourceSpec,
  type SourceSpecEntry,
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
 * ★ C6（2026-10-07）：**操作方式自动推导**，⛔ 不让人选。
 *
 * 【为什么删掉「写入方式」下拉】
 *   `WriteMode`（`replace`/`overwrite`/`append`/`remove`）是**死字段** —— 全仓只有
 *   `DocTemplateAnchorController` 在读它（受控值集合 / 保存白名单 / 默认值+校验），
 *   **写入侧（`WordFillWriter` / `ExcelFillWriter`）从不读**（`48` 号 G10 实测）。
 *   而 `51-V1` 原型、`52` 号 C6、`55` 号「操作区域」都明确：设置页**只展示推导出来的操作方式**。
 *   ⇒ 页面不再暴露这个选择；表单仍**原样回写**该列（⛔ 不重置既有值 ——
 *      `saveAnchorBatch` 是整行 upsert，少传一列就是静默清空）。
 *
 * 【分类口径必须与 C4 分组逐字一致】
 *   `AnchorRuleTab.vue` 的 `TABLE_TYPES = ['table','table_total']` 已在做「字段 / 表格」分组；
 *   这里**复用同一口径**，否则会出现「左边分到『表格』组、右边却写『单元格更新』」的自相矛盾。
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
const rawSpec = ref('')
const saving = ref(false)
const dragIndex = ref(-1)

const form = ref({
  /** ⚠️ 页面上**已无此控件**（C6：操作方式改为自动推导）—— 仅作**原样回写**，⛔ 不从 UI 改 */
  WriteMode: 'overwrite',
  ValueType: 'text',
  Required: false,
  DefaultText: '',
  NumberFormat: '',
  FieldCode: '',
  Remark: '',
})

const paramOptions = ref<{ value: string; label: string }[]>([])

/* ============ 派生 ============ */
const title = computed(() => props.anchor?.AnchorRef || '锚点规则')
const anchorType = computed(() => String(props.anchor?.AnchorType || ''))
const isDomainAuto = computed(
  () =>
    anchorType.value === 'domain' &&
    String(props.anchor?.DomainKind || '') === 'auto',
)
const isTableTotal = computed(() => anchorType.value === 'table_total')
/** ★ C6：操作方式（自动推导，⛔ 不可选）—— 与 C4 的字段/表格分组同一口径 */
const autoOp = computed<'cell' | 'table'>(() =>
  TABLE_TYPES.includes(anchorType.value) ? 'table' : 'cell',
)
const preview = computed(() => humanPreview(model.value))

const combos = computed(() => {
  const list: string[] = []
  if (isDomainAuto.value && model.value.sources.length > 0)
    list.push('域自动值无需配置取值来源')
  if (
    isTableTotal.value &&
    model.value.sources.some((s) => s.kind === 'manual')
  )
    list.push('合计锚点不能配「人工录入」')
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
  },
  { immediate: true },
)

/* ============ 来源链操作 ============ */
function addSource() {
  model.value.sources.push({
    kind: 'global',
    ref: '',
    onMissing: DEFAULT_ON_MISSING,
  })
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

function onKindChange(entry: SourceSpecEntry) {
  entry.ref = ''
  entry.field = undefined
  entry.minConfidence = undefined
  entry.promptGroup = undefined
}

/**
 * 来源类别下拉项。
 *
 * ⚠️ 必须是 `computed` —— 原来写成 `kindOptions()` 函数并在 `v-for` 里逐行调用，
 *    每个来源卡片**每次渲染都会重新 map 一遍 `SOURCE_KINDS`**（N 行 × M 次渲染
 *    的无谓分配），而且拿到的是**新数组**，`el-option` 每次都要重挂。
 */
const kindOptions = computed(() =>
  SOURCE_KINDS.map((k) => ({
    value: k.value,
    label: k.implemented ? k.label : `${k.label}（未实现）`,
    disabled: !k.implemented,
  })),
)

async function loadParams() {
  try {
    const res = await listFillParamDefs()
    paramOptions.value = (res?.data?.Items ?? []).map((p: any) => ({
      value: String(p.ParamCode ?? p.Code ?? ''),
      label: `${p.ParamCode ?? p.Code ?? ''}${p.ParamName ? ` · ${p.ParamName}` : ''}`,
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

/* ============ 保存 ============ */
async function onSave() {
  if (!props.anchor || !props.templateCode) return
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
    :confirm-disabled="combos.length > 0"
    :confirm-loading="saving"
    @update:model-value="(v: boolean) => emit('update:visible', v)"
    @confirm="onSave"
    @close="emit('close')"
    @closed="emit('closed')"
  >
    <div v-if="anchor" class="anchor-panel">
      <div class="panel-scroll">
        <!-- ── ① 定位信息（严谨简洁）── -->
        <div class="section">
          <div class="section-hd">
            <span>锚点定位</span>
            <el-popover placement="top" :width="300" trigger="hover">
              <template #reference>
                <el-icon class="info-icon"><InfoFilled /></el-icon>
              </template>
              <div class="help-content">
                <p><strong>锚点定位</strong>：该锚点在空白模板中的物理位置。</p>
                <p>
                  · <b>域自动值</b>：如页码、日期等，系统自动识别，无需配来源。
                </p>
                <p>· <b>语义字段</b>：对应数据库中的业务字段编码。</p>
              </div>
            </el-popover>
          </div>
          <div class="loc-info">
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
            <div class="loc-item full">
              <label>语义字段</label>
              <el-input
                v-model="form.FieldCode"
                size="small"
                placeholder="字段编码"
                :disabled="isDomainAuto"
              />
            </div>
          </div>
        </div>

        <!-- ── ② 取值来源链 ── -->
        <div class="section">
          <div class="section-hd">
            <span>取值来源链</span>
            <el-popover placement="top" :width="320" trigger="hover">
              <template #reference>
                <el-icon class="info-icon"><InfoFilled /></el-icon>
              </template>
              <div class="help-content">
                <p><strong>取值来源链</strong>：定义该锚点的数据从哪里来。</p>
                <p>
                  ·
                  <b>组合方式</b>：多个来源时如何合并（首个非空、拼接、模板）。
                </p>
                <p>
                  · <b>来源类型</b>：全局参数、AI 提取、计算公式、其他文档等。
                </p>
              </div>
            </el-popover>
          </div>

          <!--
            ★ 原值无法解析的提示。
            `parseSourceSpec` 对半截 JSON 会**静默回落空模型**（绝不抛异常，否则面板打不开），
            于是「来源配置其实坏了」这件事在界面上完全看不出来 ——
            用户只会看到「未配置来源」，随手加一个来源一保存，**原值就被覆盖没了**。
            这里把 `rawSpec` 原样亮出来，让用户至少有机会自己抄回去。
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

          <div class="combine-row">
            <el-radio-group v-model="model.combine" size="small">
              <el-radio-button
                v-for="m in COMBINE_MODES"
                :key="m.value"
                :value="m.value"
                >{{ m.label }}</el-radio-button
              >
            </el-radio-group>
            <div v-if="model.combine === 'concat'" class="combine-param">
              <span class="label">分隔符</span>
              <el-input
                v-model="model.separator"
                size="small"
                placeholder="、"
                style="width: 60px"
              />
            </div>
            <div v-if="model.combine === 'template'" class="combine-param">
              <span class="label">模板</span>
              <el-input
                v-model="model.expr"
                size="small"
                placeholder="{{Name}}({{Code}})"
              />
            </div>
          </div>

          <div class="sources-list">
            <div
              v-for="(s, i) in model.sources"
              :key="i"
              class="source-card"
              draggable="true"
              @dragstart="onDragStart(i)"
              @dragover="onDragOver"
              @drop="onDrop(i)"
            >
              <div class="card-left">
                <span class="drag-handle">⠿</span>
                <span class="idx">{{ i + 1 }}</span>
                <el-select
                  v-model="s.kind"
                  size="small"
                  style="width: 100px"
                  @change="onKindChange(s)"
                >
                  <el-option
                    v-for="o in kindOptions"
                    :key="o.value"
                    :value="o.value"
                    :label="o.label"
                    :disabled="o.disabled"
                  />
                </el-select>
              </div>

              <div class="card-center">
                <template v-if="s.kind === 'global'">
                  <el-select
                    v-model="s.ref"
                    size="small"
                    filterable
                    allow-create
                    placeholder="参数编码"
                    style="width: 100%"
                  >
                    <el-option
                      v-for="p in paramOptions"
                      :key="p.value"
                      :value="p.value"
                      :label="p.label"
                    />
                  </el-select>
                </template>
                <template v-else-if="s.kind === 'profile'">
                  <el-input
                    v-model="s.ref"
                    size="small"
                    placeholder="文档"
                    style="width: 40%"
                  />
                  <el-input
                    v-model="s.field"
                    size="small"
                    placeholder="字段"
                    style="width: 40%"
                  />
                  <el-input-number
                    v-model="s.minConfidence"
                    size="small"
                    :min="0"
                    :max="1"
                    :step="0.1"
                    :controls="false"
                    placeholder="置信度"
                    style="width: 20%"
                  />
                </template>
                <template v-else-if="s.kind === 'ai'">
                  <el-input
                    v-model="s.ref"
                    size="small"
                    placeholder="提示词"
                    style="width: 50%"
                  />
                  <el-input
                    v-model="s.promptGroup"
                    size="small"
                    placeholder="提示词组"
                    style="width: 50%"
                  />
                </template>
                <template
                  v-else-if="
                    s.kind === 'compute' ||
                    s.kind === 'self' ||
                    s.kind === 'sibling'
                  "
                >
                  <el-input
                    v-model="s.ref"
                    size="small"
                    placeholder="引用/表达式"
                    style="width: 100%"
                  />
                </template>
                <span v-else class="muted">无需参数</span>
              </div>

              <div class="card-right">
                <el-select
                  v-model="s.onMissing"
                  size="small"
                  style="width: 90px"
                >
                  <el-option
                    v-for="o in ON_MISSING_OPTIONS"
                    :key="o.value"
                    :value="o.value"
                    :label="o.label"
                  />
                </el-select>
                <el-button
                  :icon="Delete"
                  circle
                  size="small"
                  text
                  type="danger"
                  @click="removeSource(i)"
                />
              </div>
            </div>

            <el-button
              type="primary"
              :icon="Plus"
              size="small"
              text
              class="add-btn"
              @click="addSource"
              >添加来源</el-button
            >
          </div>

          <div class="preview-banner">
            <span class="label">运行期逻辑：</span>
            <span class="text">{{ preview }}</span>
          </div>
        </div>

        <!-- ── ③ 写入属性 ── -->
        <div class="section">
          <div class="section-hd">
            <span>写入属性</span>
            <el-popover placement="top" :width="280" trigger="hover">
              <template #reference>
                <el-icon class="info-icon"><InfoFilled /></el-icon>
              </template>
              <div class="help-content">
                <p><strong>写入属性</strong>：定义如何将值写回文档。</p>
                <p>
                  · <b>操作方式</b>：由锚点类型<b>自动推导</b>（表格锚点 ⇒ 表格更新；
                  其余 ⇒ 单元格更新），⛔ 不需要选。
                </p>
                <p>· <b>空值兜底</b>：当所有来源都拿不到值时使用的默认文字。</p>
              </div>
            </el-popover>
          </div>
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
                <el-option
                  v-for="t in VALUE_TYPES"
                  :key="t"
                  :value="t"
                  :label="t"
                />
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
        </div>

        <el-alert
          v-for="(c, i) in combos"
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
  --suc: var(--yzh-color-success, #67c23a);
  --suc-l: var(--yzh-color-success-light-9, #f0f9eb);
  --suc-b: var(--yzh-color-success-light-7, #e1f3d8);
  --warn: var(--yzh-color-warning, #e6a23c);
  --warn-l: var(--yzh-color-warning-light-9, #fdf6ec);
  --warn-b: var(--yzh-color-warning-light-7, #faecd8);
  --dan: var(--yzh-color-danger, #f56c6c);
  --dan-l: var(--yzh-color-danger-light-9, #fef0f0);
  --dan-b: var(--yzh-color-danger-light-7, #fde2e2);
  --info: var(--yzh-color-text-tertiary, #909399);
  --info-l: var(--yzh-color-info-light-9, #f4f4f5);
  --info-b: var(--yzh-color-info-light-7, #e9e9eb);
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

.info-icon {
  color: var(--t4);
  cursor: help;
  font-size: var(--yzh-font-size-md, 14px);
  transition: color 0.2s;
}
.info-icon:hover {
  color: var(--pri);
}

.help-content {
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  color: var(--t2);
}

/* ── 定位信息 ── */
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
}
.loc-item label {
  color: var(--t3);
  width: 50px;
  font-weight: 500;
}
.loc-item.full {
  grid-column: span 2;
}

/* ── 来源链 ── */
.combine-row {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: var(--yzh-space-2, 8px);
  padding: 0 var(--yzh-space-1, 4px);
}
.combine-param {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: var(--yzh-font-size-xs, 11px);
}
.combine-param .label {
  color: var(--t3);
}

.sources-list {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.source-card {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: var(--yzh-space-2, 6px) var(--yzh-space-2, 10px);
  background: var(--yzh-color-bg-container, #fff);
  border: 1px solid var(--bd-l);
  border-radius: var(--r);
  transition: all 0.2s;
}
.source-card:hover {
  border-color: var(--pri-b);
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.04);
}
.card-left {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-shrink: 0;
}
.drag-handle {
  cursor: grab;
  color: var(--t4);
  font-size: var(--yzh-font-size-md, 14px);
  padding: 0 var(--yzh-space-1, 2px);
}
.idx {
  width: 16px;
  height: 16px;
  line-height: 16px;
  font-size: var(--yzh-font-size-xs, 10px);
  color: var(--t3);
  text-align: center;
  background: var(--bg);
  border-radius: 50%;
  font-family: var(--mono);
}
.card-center {
  flex: 1;
  display: flex;
  gap: 5px;
  min-width: 0;
}
.card-right {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
}

.add-btn {
  margin-top: var(--yzh-space-1, 4px);
  align-self: flex-start;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--pri);
}

.preview-banner {
  margin-top: var(--yzh-space-2, 8px);
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
  font-family: var(--mono);
}

/* ── 写入属性 ── */
.form-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}
.form-item {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: var(--yzh-font-size-xs, 11px);
}
.form-item label {
  color: var(--t2);
  width: 50px;
  flex-shrink: 0;
  font-weight: 500;
}
.form-item.full {
  grid-column: span 2;
  align-items: flex-start;
}
.form-item.full label {
  margin-top: var(--yzh-space-2, 6px);
}

/*
 * ★ C6：操作方式（自动推导，只读）。
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

.ml4 {
  margin-left: var(--yzh-space-1, 4px);
}
.mt8 {
  margin-top: var(--yzh-space-2, 8px);
}

/* 「原值无法解析」告警 —— 原值以等宽字体整段亮出，长 JSON 换行不溢出 */
.parse-alert {
  margin-bottom: var(--yzh-space-2, 8px);
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
.muted {
  color: var(--t4);
  font-size: var(--yzh-font-size-xs, 10px);
}

:deep(.el-input-number.is-without-controls .el-input__inner) {
  text-align: left;
  padding-left: var(--yzh-space-2, 8px);
}

:deep(.el-input--small .el-input__inner) {
  font-size: var(--yzh-font-size-xs, 11px);
}
:deep(.el-select--small .el-select__wrapper) {
  font-size: var(--yzh-font-size-xs, 11px);
}
</style>
