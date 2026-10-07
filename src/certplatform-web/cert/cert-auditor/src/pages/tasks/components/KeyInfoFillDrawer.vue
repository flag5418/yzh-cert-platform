<script setup lang="ts">
/**
 * KeyInfoFillDrawer —— 「关键信息补录」抽屉（★ 2026-10-07 用户裁决）
 *
 * 【用户裁决原文】
 *   「裁 1 这个弹窗叫关键信息补录」
 *   「裁 2 运行跳过，针对工作流缺失关键信息的，该工作流不执行，再队列完成后，
 *          详细记录，哪些规则或条款未执行成功，什么原因」
 *   「裁 3 这个是我们在 http://127.0.0.1:9990/business/doc-extraction-rule 定义的
 *          每个字段都有中文和英文字段描述，表格的信息也有，这个关键信息补录，分为
 *          2 个 tab，一个是关键字段补录，一个是关键表格信息补录」
 *
 * 【它解决什么】
 *   旧流程：「点提交 → 跳详情 → 再点启动 → 去第 4 个 Tab 找缺什么」—— 四步全推给审核员。
 *   新流程：点「启动任务」时**先校验**，缺关键信息就**不启动**，当场弹这个抽屉补齐，
 *   补齐后自动继续启动。⛔ 不把系统内部准备工作的负担转嫁给用户。
 *
 * 【两个 Tab 的数据来源】
 *   ① 关键字段补录    ← `gapList.Fields`（每个 = 一个待补字段）
 *   ② 关键表格信息补录 ← `gapList.Tables`（每个 = 一张待补表格，列头来自 `gap.Columns`）
 *   中文名权威来源 = 后台「文档提取规则」页（`cert_doc_field_def.FieldName` /
 *   `cert_doc_table_def.TableName` / `cert_doc_table_field_def.ColumnName`），
 *   由后端 `GapLabelResolver` 解析后随缺口清单一起下发 —— 前端⛔ 不自己查字典。
 *
 * 【本组件是「纯表单」】
 *   只负责收集值并 `emit('fill', items)` / `emit('skip')`；
 *   补录落库、提交、启动队列全部由 `logic.ts` 编排（页面逻辑层）。
 *   ⛔ 本组件不发任何请求。
 *
 * 【守卫法条】
 *   S07 → 用 `YzhDrawer`（⛔ 禁裸抽屉组件）；S04 → 每个按钮组件显式 `type`；
 *   S08 → 状态徽标用 `YzhStatusBadge`（⛔ 禁裸标签组件）；R18 → 样式走令牌 + 兜底。
 *
 * ⚠️ 本注释**不能**出现裸的组件标签字面量 —— S04 / S08 是对全文做正则计数
 *    （不跳注释），写一个示例标签就会把自己判成违规。
 */
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhDrawer, YzhStatusBadge } from '@yzh-core'
import type { GapColumn, TaskGap, TaskGapList } from '@share/api/auditor/expert-task'
import TableGapEditor from './TableGapEditor.vue'

const props = withDefaults(
  defineProps<{
    modelValue: boolean
    /**
     * 使用场景：
     *  · `start`（默认）= 任务列表「启动任务」的前置补录 → 主按钮「补齐并启动」
     *  · `fill`        = 任务详情里补单张表格 → 主按钮「保存补录」，不影响任务启动
     */
    mode?: 'start' | 'fill'
    /** 任务名（标题里显示，让用户确认自己在给哪条任务补录） */
    taskName?: string
    /** 缺口清单（由 `precheckGaps` 实时重算后传入） */
    gapList: TaskGapList | null
    /** 提交中（父组件正在补录 + 启动） */
    submitting?: boolean
  }>(),
  { mode: 'start', taskName: '', gapList: null, submitting: false },
)

const isFillMode = computed(() => props.mode === 'fill')

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  /** 用户点了「补齐并启动」—— 只带**填了值**的项 */
  (e: 'fill', items: { GapCode: string; Value: string }[]): void
  /** 用户点了「运行跳过」—— 全部未补录项按跳过处理并启动 */
  (e: 'skip'): void
}>()

const activeTab = ref<'fields' | 'tables'>('fields')

/** 字段补录值：gapCode → 文本 */
const fieldValues = reactive<Record<string, string>>({})
/** 表格补录行：gapCode → 行数组（列 Code 为 key） */
const tableRows = reactive<Record<string, Record<string, string>[]>>({})
/**
 * 无列定义表格的兜底输入：gapCode → JSON 文本。
 *
 * ⚠️ 为什么必须留这个兜底：`cert_doc_table_field_def` 实测只覆盖 `table2` / `table3`，
 *    `table1/4/5/6` **没有任何列定义** ⇒ 没有列头就渲染不出输入格。
 *    这时宁可退化成「手填 JSON + 明确告知管理员去补列定义」，也不能让功能直接卡死。
 */
const tableJson = reactive<Record<string, string>>({})

const pendingFields = computed<TaskGap[]>(
  () => (props.gapList?.Fields ?? []).filter((g) => g.GapStatus === 'pending'),
)
const pendingTables = computed<TaskGap[]>(
  () => (props.gapList?.Tables ?? []).filter((g) => g.GapStatus === 'pending'),
)

const pendingCount = computed(() => props.gapList?.PendingCount ?? 0)

/** 中文名缺失的条数（`cert_doc_field_def` 实测全库只有 1 行 ⇒ 常命中） */
const unnamedCount = computed(() => {
  const all = [...(props.gapList?.Fields ?? []), ...(props.gapList?.Tables ?? [])]
  return all.filter((g) => g.IsUnnamed === true).length
})

function emptyRow(cols: GapColumn[]): Record<string, string> {
  const r: Record<string, string> = {}
  for (const c of cols) r[c.Code] = ''
  return r
}

/** 重置编辑态 —— 每次打开都从空白开始，⛔ 不残留上次输入 */
function resetEditors() {
  for (const k of Object.keys(fieldValues)) delete fieldValues[k]
  for (const k of Object.keys(tableRows)) delete tableRows[k]
  for (const k of Object.keys(tableJson)) delete tableJson[k]

  for (const g of pendingFields.value) fieldValues[g.Code] = ''
  for (const g of pendingTables.value) {
    const cols = g.Columns ?? []
    if (cols.length > 0) tableRows[g.Code] = [emptyRow(cols)]
    else tableJson[g.Code] = ''
  }
}

// 打开时（或清单换了）重置：父组件先写 `gapList` 再置 `visible=true`，同一次 flush 到达
watch([() => props.modelValue, () => props.gapList], ([open]) => {
  if (!open) return
  resetEditors()
  activeTab.value = pendingFields.value.length > 0 ? 'fields' : 'tables'
})

/** 已填写项数（字段 + 表格各算 1 项） */
const filledCount = computed(() => {
  let n = 0
  for (const g of pendingFields.value) {
    if ((fieldValues[g.Code] ?? '').trim()) n++
  }
  for (const g of pendingTables.value) {
    const rows = tableRows[g.Code] ?? []
    const hasText = rows.some((r) => Object.values(r).some((v) => String(v ?? '').trim()))
    const hasJson = (tableJson[g.Code] ?? '').trim().length > 0
    if (hasText || hasJson) n++
  }
  return n
})

/** 未填写（= 将被跳过）的项数 */
const willSkipCount = computed(() => Math.max(0, pendingCount.value - filledCount.value))

const footerHint = computed(() => {
  if (pendingCount.value === 0) {
    return isFillMode.value ? '关键信息已齐全。' : '关键信息已齐全，可以直接启动。'
  }
  if (filledCount.value === 0) {
    return isFillMode.value
      ? '尚未填写。填好后点「保存补录」即可。'
      : `尚未填写。点「补齐并启动」需先填至少一项；点「运行跳过」= 这 ${pendingCount.value} 项全部按「跳过」处理并立即启动。`
  }
  if (willSkipCount.value === 0) {
    return isFillMode.value
      ? `已填 ${filledCount.value} 项，点「保存补录」写入。`
      : `已填 ${filledCount.value} 项，点「补齐并启动」会先保存再启动任务。`
  }
  return isFillMode.value
    ? `已填 ${filledCount.value} 项，还有 ${willSkipCount.value} 项未填（未填的不会被保存）。`
    : `已填 ${filledCount.value} 项，还有 ${willSkipCount.value} 项未填 —— 未填的会按「跳过」处理（相关检查项标「数据不足，未检查」，不计入「符合」）。`
})

/** 表格型：列定义存在 ⇒ 收集行；否则校验 JSON */
function collectTable(g: TaskGap, items: { GapCode: string; Value: string }[]): boolean {
  const cols = g.Columns ?? []
  if (cols.length > 0) {
    const rows = (tableRows[g.Code] ?? []).filter((r) =>
      Object.values(r).some((v) => String(v ?? '').trim()),
    )
    if (rows.length > 0) items.push({ GapCode: g.Code, Value: JSON.stringify(rows) })
    return true
  }

  const raw = (tableJson[g.Code] ?? '').trim()
  if (!raw) return true
  try {
    const parsed = JSON.parse(raw)
    if (!Array.isArray(parsed)) throw new Error('not array')
  } catch {
    ElMessage.error(`「${g.GapLabel}」的内容必须是 JSON 数组，例如 [{"列名":"值"}]`)
    return false
  }
  items.push({ GapCode: g.Code, Value: raw })
  return true
}

function onConfirm() {
  const items: { GapCode: string; Value: string }[] = []

  for (const g of pendingFields.value) {
    const v = (fieldValues[g.Code] ?? '').trim()
    if (v) items.push({ GapCode: g.Code, Value: v })
  }
  for (const g of pendingTables.value) {
    if (!collectTable(g, items)) return
  }

  if (items.length === 0) {
    ElMessage.warning('还没有填写任何内容。若确认不补录、直接启动，请点「运行跳过」。')
    return
  }
  emit('fill', items)
}
</script>

<template>
  <YzhDrawer
    :model-value="modelValue"
    title="关键信息补录"
    size="920px"
    :close-on-click-modal="false"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <div class="kif">
      <!-- ════ 头部：为什么弹这个窗 ════ -->
      <div class="kif__head">
        <el-alert type="warning" :closable="false" show-icon class="kif__alert">
          <template #title>
            {{ isFillMode ? '发现' : '启动前发现' }} {{ pendingCount }} 项关键信息缺失
            <template v-if="taskName"> · {{ taskName }}</template>
          </template>
          <div class="kif__alert-body">
            <template v-if="isFillMode">
              缺这些数据的检查项<strong>不会执行</strong>（标记为「数据不足，未检查」，
              <strong>不计入「符合」数量</strong>）。补录后回任务详情重新启动即可执行。
            </template>
            <template v-else>
              缺这些数据的检查项<strong>不会执行</strong>（标记为「数据不足，未检查」，
              <strong>不计入「符合」数量</strong>）。
              补齐后启动即可全部执行；点「运行跳过」则直接启动，
              未执行的规则 / 条款会在任务详情的「未执行清单」里逐条列明原因。
            </template>
          </div>
        </el-alert>

        <el-alert v-if="unnamedCount > 0" type="info" :closable="false" show-icon class="kif__alert">
          <template #title>有 {{ unnamedCount }} 项在「文档提取规则」里还没配中文名</template>
          <div class="kif__alert-body">
            下面用「未命名字段 / 未命名表格」占位。请管理员到后台
            「文档提取规则」页给这些字段 / 表格补上中文名，之后这里就会显示业务名称。
          </div>
        </el-alert>
      </div>

      <!-- ════ 两个 Tab ════ -->
      <el-tabs v-model="activeTab" class="kif__tabs">
        <!-- ── Tab 1 · 关键字段补录 ── -->
        <el-tab-pane name="fields">
          <template #label>
            <span class="kif__tab-label">
              关键字段补录
              <span class="kif__tab-count">{{ pendingFields.length }}</span>
            </span>
          </template>

          <div v-if="pendingFields.length === 0" class="kif__empty">
            没有待补录的字段。
          </div>

          <div v-else class="kif__list">
            <div v-for="g in pendingFields" :key="g.Code" class="kif__item">
              <div class="kif__item-head">
                <span class="kif__item-label">{{ g.GapLabel }}</span>
                <YzhStatusBadge v-if="g.IsUnnamed" type="warning" text="未登记中文名" />
                <span class="kif__item-src">
                  应来自：{{ g.ExpectedFileName || '—' }}
                </span>
                <span v-if="g.ImpactedItemCount > 0" class="kif__item-impact">
                  影响 {{ g.ImpactedItemCount }} 条检查项
                </span>
              </div>

              <el-input
                v-model="fieldValues[g.Code]"
                clearable
                :disabled="submitting"
                :placeholder="`请输入「${g.GapLabel}」`"
              />

              <div v-if="g.ImpactedItems?.length" class="kif__chips">
                <span class="kif__chips-label">受影响：</span>
                <span
                  v-for="it in g.ImpactedItems.slice(0, 6)"
                  :key="it.ItemCode"
                  class="kif__chip"
                >
                  {{ it.ItemName }}{{ it.ClauseCode ? `（${it.ClauseCode}）` : '' }}
                </span>
                <span v-if="g.ImpactedItems.length > 6" class="kif__muted">
                  等 {{ g.ImpactedItems.length }} 条
                </span>
              </div>
            </div>
          </div>
        </el-tab-pane>

        <!-- ── Tab 2 · 关键表格信息补录 ── -->
        <el-tab-pane name="tables">
          <template #label>
            <span class="kif__tab-label">
              关键表格信息补录
              <span class="kif__tab-count">{{ pendingTables.length }}</span>
            </span>
          </template>

          <div v-if="pendingTables.length === 0" class="kif__empty">
            没有待补录的表格。
          </div>

          <div v-else class="kif__list">
            <div v-for="g in pendingTables" :key="g.Code" class="kif__item">
              <div class="kif__item-head">
                <span class="kif__item-label">{{ g.GapLabel }}</span>
                <YzhStatusBadge v-if="g.IsUnnamed" type="warning" text="未登记中文名" />
                <span class="kif__item-src">
                  应来自：{{ g.ExpectedFileName || '—' }}
                </span>
                <span v-if="g.ImpactedItemCount > 0" class="kif__item-impact">
                  影响 {{ g.ImpactedItemCount }} 条检查项
                </span>
              </div>

              <TableGapEditor
                v-if="(g.Columns?.length ?? 0) > 0"
                v-model="tableRows[g.Code]"
                :columns="g.Columns ?? []"
                :disabled="submitting"
              />

              <div v-else class="kif__fallback">
                <el-alert
                  type="warning"
                  :closable="false"
                  show-icon
                  title="这张表在「文档提取规则」里还没有定义列"
                  class="kif__alert"
                >
                  <div class="kif__alert-body">
                    无法渲染成表格，只能先按 JSON 数组填写。请管理员到后台
                    「文档提取规则」页给这张表补上列定义，之后这里会自动变成可编辑表格。
                  </div>
                </el-alert>
                <el-input
                  v-model="tableJson[g.Code]"
                  type="textarea"
                  :rows="3"
                  :disabled="submitting"
                  placeholder='JSON 数组，例如 [{"项目":"值"}]'
                />
              </div>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </div>

    <!-- ════ 底部：补齐并启动 / 运行跳过 ════ -->
    <template #footer>
      <div class="kif__foot">
        <span class="kif__foot-hint">{{ footerHint }}</span>
        <div class="kif__foot-btns">
          <el-button
            type="default"
            :disabled="submitting"
            @click="emit('update:modelValue', false)"
          >
            {{ isFillMode ? '取消' : '稍后再说' }}
          </el-button>
          <el-button
            type="default"
            :disabled="submitting"
            @click="emit('skip')"
          >
            {{ isFillMode ? '跳过此项' : '运行跳过' }}
          </el-button>
          <el-button type="primary" :loading="submitting" @click="onConfirm">
            {{ isFillMode ? '保存补录' : '补齐并启动' }}
          </el-button>
        </div>
      </div>
    </template>
  </YzhDrawer>
</template>

<style scoped>
.kif {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.kif__head {
  flex: none;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  padding-bottom: var(--yzh-space-2, 8px);
}

.kif__alert {
  flex: none;
}

.kif__alert-body {
  line-height: 1.7;
  font-size: var(--yzh-font-size-xs, 12px);
}

.kif__tabs {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.kif__tabs :deep(.el-tabs__content) {
  flex: 1;
  min-height: 0;
  overflow: auto;
}

.kif__tab-label {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
}

.kif__tab-count {
  display: inline-block;
  min-width: 18px;
  text-align: center;
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--el-fill-color);
  color: var(--el-text-color-secondary);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1;
  padding: var(--yzh-space-1, 4px);
}

.kif__list {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-4, 16px);
  padding-bottom: var(--yzh-space-2, 8px);
}

.kif__item {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border, #e4e7ed);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--el-fill-color-blank);
}

.kif__item-head {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--yzh-space-2, 8px);
}

.kif__item-label {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: 600;
  color: var(--el-text-color-primary);
}

.kif__item-src {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}

.kif__item-impact {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-color-primary);
}

.kif__chips {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--yzh-space-1, 4px);
}

.kif__chips-label {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}

.kif__chip {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-regular);
  background: var(--el-fill-color-light);
  border-radius: var(--yzh-radius-sm, 4px);
  padding: var(--yzh-space-1, 4px);
}

.kif__muted {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}

.kif__empty {
  padding: var(--yzh-space-5, 20px);
  text-align: center;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-secondary);
}

.kif__fallback {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.kif__foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-4, 16px);
  width: 100%;
}

.kif__foot-hint {
  flex: 1;
  min-width: 0;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
  line-height: 1.6;
}

.kif__foot-btns {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex: none;
}
</style>
