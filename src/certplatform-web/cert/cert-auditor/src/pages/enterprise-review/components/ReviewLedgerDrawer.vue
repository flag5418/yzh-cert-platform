<script setup lang="ts">
/**
 * 取值账本抽屉（企业资料审核 · 逐锚点改值 / 钉住）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户裁决：审核的产出 = **逐锚点改值 / 钉住**
 *
 * 用户原话（AskUserQuestion 回答）：「逐锚点改值 / 钉住」。
 *
 * ⇒ 抽屉只做两件事，都**只改账本**：
 *   ① **改值** —— 人工给这个锚点一个新值（`Kind='value'`）
 *   ② **钉住 / 取消钉住** —— 钉住后「全部重写」会跳过这一行（`IsPin`）
 *
 * ⛔⛔ **改账本 ≠ 改文档**（`54` §5.2 端点 11 的明文约定）：
 *   本抽屉**不会**重跑编排器、**不会**动产物文件。账本改了、产物还是旧的，
 *   必须再走一次生成才会体现在文档里 ⇒ 抽屉顶部必须**显式说破**这件事，
 *   否则用户改完下载一看没变，会判定「改了没用」。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 为什么用「行内编辑」而不是再弹一个对话框
 *
 * 改值和钉住都**必须填理由**（后端 `Reason` 必填，留痕用）。
 * 再套一层对话框 = 弹窗叠弹窗（`appendToBody` 的抽屉里再挂遮罩），
 * 层级与焦点都会打架。行内编辑把「值 / 理由 / 确定」放在同一行，一眼看清在改什么。
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhDrawer, YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import { formatDateTime } from '@share/utils/format'
import {
  fetchNormalizeValues,
  overrideNormalizeValue,
  pathFileName,
  SOURCE_KIND_TEXT,
  toPercent,
  VALUE_STATUS_TEXT,
  VALUE_STATUS_TYPE,
  type FillValueDto,
  type NormalizeLedgerResult,
} from '@share/api/ent/enterprise-normalize'

const props = defineProps<{
  modelValue: boolean
  /** ★ 必需：账本按「企业 × 文件」定位 */
  enterpriseCode: string
  /** 文档名（标题用） */
  fileName: string
  standardFileCode: string
  /** ★ 本任务那一份留痕的 Code —— 不传则后端取**最近一次**填充 */
  fillLogCode?: string
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', v: boolean): void
  /** 账本被改过 ⇒ 页面据此刷新右表（状态 / 待办数可能已变） */
  (e: 'changed'): void
}>()

const loading = ref(false)
const ledger = ref<NormalizeLedgerResult | null>(null)

/** 行内编辑态：`AnchorCode` → 正在编辑什么 */
const editing = ref<{ anchorCode: string; kind: 'value' | 'pin'; value: string; reason: string } | null>(
  null,
)
const submitting = ref(false)

const visible = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
})

const title = computed(() =>
  props.fileName ? `审核取值 · ${props.fileName}` : '审核取值',
)

/** 抽屉打开就（重新）读一次账本 —— ⛔ 不复用上次的缓存，可能已被别人改过 */
watch(
  () => props.modelValue,
  async (open) => {
    if (!open) {
      editing.value = null
      return
    }
    await load()
  },
)

async function load(): Promise<void> {
  if (!props.enterpriseCode || !props.standardFileCode) {
    ledger.value = null
    return
  }
  loading.value = true
  try {
    ledger.value = await fetchNormalizeValues({
      EnterpriseCode: props.enterpriseCode,
      StandardFileCode: props.standardFileCode,
      FillLogCode: props.fillLogCode || undefined,
    })
  } catch (e) {
    ledger.value = null
    ElMessage.error((e as Error)?.message || '读取取值账本失败')
  } finally {
    loading.value = false
  }
}

/** 空账本的文案（★ 「从未规范化」与「查失败」是两回事，⛔ 不能合成一句） */
const emptyText = computed(() =>
  ledger.value && !ledger.value.FillLogCode
    ? '这份文档还没有规范化过 —— 请先在「企业资料规范化」里跑一次任务。'
    : '这份文档没有取值记录。',
)

function startEditValue(row: FillValueDto): void {
  editing.value = {
    anchorCode: row.AnchorCode,
    kind: 'value',
    // ★ 预填当前值（`ValueText` 是落库原值；`ValueDisplay` 是给眼睛看的）——
    //   让用户在现有值上改，而不是从空白开始重打一遍
    value: row.ValueText ?? '',
    reason: '',
  }
}

function startEditPin(row: FillValueDto): void {
  editing.value = { anchorCode: row.AnchorCode, kind: 'pin', value: '', reason: '' }
}

function cancelEdit(): void {
  editing.value = null
}

/**
 * 提交一次账本写入。
 *
 * ⚠️ 三条后端契约（写错会**静默**落错值）：
 *   ① `ValueText` **空串 = 显式清空**（落 `removed`，不计完成率分子）；`undefined` = 没给 ⇒ 拒绝
 *      ⇒ 这里永远传字符串，让「清空」成为一个**明确动作**
 *   ② `IsPin` **`undefined` = 本次不改钉住状态**；⛔ 不能用 `false` 表达「不改」（那会静默解开）
 *      ⇒ 改值时**不传** `IsPin` 字段
 *   ③ `Reason` 必填（留痕）
 */
async function submitEdit(): Promise<void> {
  const e = editing.value
  const lg = ledger.value
  if (!e || !lg?.FillLogCode) return

  if (!e.reason.trim()) {
    ElMessage.warning('请填写理由 —— 改值 / 钉住都会留痕，理由会写进操作记录')
    return
  }

  submitting.value = true
  try {
    const row = currentRow()
    const result = await overrideNormalizeValue({
      FillLogCode: lg.FillLogCode,
      AnchorCode: e.anchorCode,
      Kind: e.kind === 'value' ? 'value' : '',
      ValueText: e.kind === 'value' ? e.value : undefined,
      Reason: e.reason.trim(),
      // ★ 只有「钉住」动作才传 `IsPin`（见本函数注释 ②）
      ...(e.kind === 'pin' ? { IsPin: !row?.IsPinned } : {}),
    })

    let msg = e.kind === 'value' ? '已改写取值（账本）' : result.IsPinned ? '已钉住' : '已取消钉住'
    // ★ AuditWarning 非空 = 写入生效但**留痕链断了** —— 必须显式提示，⛔ 不能吞
    if (result.AuditWarning) msg += `；⚠️ ${result.AuditWarning}`
    ElMessage.success(msg)

    editing.value = null
    await load()
    emit('changed')
  } catch (e2) {
    ElMessage.error((e2 as Error)?.message || '改写失败')
  } finally {
    submitting.value = false
  }
}

/** 当前编辑行（`IsPin` 取反用） */
function currentRow(): FillValueDto | undefined {
  const code = editing.value?.anchorCode
  return ledger.value?.Items.find((i) => i.AnchorCode === code)
}
</script>

<template>
  <YzhDrawer
    v-model="visible"
    :title="title"
    size="860px"
    :show-footer="false"
    append-to-body
  >
    <div v-loading="loading" class="rld">
      <!-- ⛔⛔ 必须先说破「改账本 ≠ 改文档」—— 否则用户改完下载发现没变，判定「改了没用」 -->
      <el-alert type="warning" :closable="false" show-icon class="rld__warn">
        <template #title>改的是「取值账本」，不会自动改写已生成的文档</template>
        <!--
          ⚠️ 正文里 ⛔ 不要写 Markdown 的 `**加粗**` —— Vue 文本节点**不解析 Markdown**，
            星号会原样印出来（真机实测过，同页 `index.vue` 的 `.rv-note` 也是同一坑）。
            要加粗就用标签。
        -->
        <div>
          这里改完，记录立刻生效、留痕立刻可查；但已经生成的文档还是旧的。
          要让新值落到文档里，需要回到「企业资料规范化」对该文档<b>重新执行</b>一次。
        </div>
      </el-alert>

      <!-- 留痕上下文：读的是哪一次填充（★ 不传 FillLogCode 时后端挑最近一次，必须让用户看见） -->
      <div v-if="ledger?.FillLogCode" class="rld__ctx">
        <span>填充留痕 <code>{{ ledger.FillLogCode }}</code></span>
        <span>·</span>
        <span>{{ ledger.FillLogTime ? formatDateTime(ledger.FillLogTime) : '时间未知' }}</span>
        <span>·</span>
        <span>锚点 {{ ledger.FillLogTotalAnchors }} 个 / 待办 {{ ledger.FillLogPendingCount }} 个</span>
        <span v-if="ledger.OutputPath" class="rld__ctx-path" :title="ledger.OutputPath">
          · 产物 {{ pathFileName(ledger.OutputPath) }}
        </span>
      </div>

      <YzhEmptyState v-if="!loading && !ledger?.FillLogCode" :title="emptyText" />

      <div v-else-if="ledger" class="rld__list">
        <div v-for="row in ledger.Items" :key="row.AnchorCode" class="rld__row">
          <!-- ── 第一行：锚点原文 + 状态徽标 ── -->
          <div class="rld__head">
            <code class="rld__anchor">{{ row.AnchorRef }}</code>
            <YzhStatusBadge
              :type="VALUE_STATUS_TYPE[row.FillStatus] || 'info'"
              :text="VALUE_STATUS_TEXT[row.FillStatus] || row.FillStatus"
            />
            <YzhStatusBadge v-if="row.IsPinned" type="warning" text="已钉住" />
            <YzhStatusBadge v-if="row.IsOverridden" type="info" text="已人工改写" />
            <YzhStatusBadge v-if="row.IsOrphan" type="danger" text="规则已删" />
            <span v-if="row.LocationDesc" class="rld__loc">{{ row.LocationDesc }}</span>
          </div>

          <!-- ── 第二行：取值 + 来源 + 置信度 ── -->
          <div class="rld__body">
            <div class="rld__value">
              <span class="rld__label">取值</span>
              <span class="rld__value-text">{{ row.ValueDisplay || '（空）' }}</span>
            </div>
            <div class="rld__meta">
              <span class="rld__label">来源</span>
              <span>{{ row.SourceLabel || SOURCE_KIND_TEXT[row.SourceKind] || row.SourceKind }}</span>
              <span class="rld__sep">·</span>
              <span>可信度 {{ toPercent(row.Confidence) }}%</span>
              <template v-if="row.OverrideReason">
                <span class="rld__sep">·</span>
                <span>人工理由：{{ row.OverrideReason }}</span>
              </template>
            </div>
          </div>

          <!-- ── 第三行：动作 / 行内编辑 ── -->
          <div
            v-if="editing && editing.anchorCode === row.AnchorCode"
            class="rld__edit"
          >
            <el-input
              v-if="editing.kind === 'value'"
              v-model="editing.value"
              size="small"
              placeholder="新的取值（留空 = 显式清空该锚点）"
              class="rld__edit-value"
            />
            <el-input
              v-model="editing.reason"
              size="small"
              :placeholder="
                editing.kind === 'value' ? '为什么改（必填，会写进留痕）' : '为什么钉住 / 取消钉住（必填）'
              "
              class="rld__edit-reason"
            />
            <el-button type="primary" size="small" :loading="submitting" @click="submitEdit()">
              确定
            </el-button>
            <el-button type="default" size="small" :disabled="submitting" @click="cancelEdit()">
              取消
            </el-button>
          </div>

          <div v-else class="rld__actions">
            <el-button type="primary" plain size="small" @click="startEditValue(row)">
              改值
            </el-button>
            <el-button type="default" size="small" @click="startEditPin(row)">
              {{ row.IsPinned ? '取消钉住' : '钉住' }}
            </el-button>
          </div>
        </div>
      </div>
    </div>
  </YzhDrawer>
</template>

<style scoped>
.rld {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
}

.rld__warn {
  flex: none;
}

.rld__ctx {
  flex: none;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.rld__ctx code {
  font-family: var(--yzh-font-family-mono, monospace);
  color: var(--yzh-color-text-regular, #606266);
}

.rld__ctx-path {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

.rld__list {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.rld__row {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border: 1px solid var(--yzh-color-border-light, #f1f5f9);
  border-radius: var(--yzh-radius-sm, 4px);
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
}

.rld__head {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--yzh-space-2, 8px);
}

.rld__anchor {
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-primary, #303133);
  word-break: break-all;
}

.rld__loc {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.rld__body {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
}

.rld__label {
  flex: none;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #a8abb2);
  margin-right: var(--yzh-space-1, 4px);
}

.rld__value-text {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-primary, #303133);
  word-break: break-all;
}

.rld__meta {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  word-break: break-all;
}

.rld__sep {
  margin: 0 var(--yzh-space-1, 4px);
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

.rld__actions,
.rld__edit {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--yzh-space-2, 8px);
  margin-top: var(--yzh-space-1, 4px);
}

.rld__edit-value {
  width: 300px;
}

.rld__edit-reason {
  flex: 1;
  min-width: 220px;
}
</style>
