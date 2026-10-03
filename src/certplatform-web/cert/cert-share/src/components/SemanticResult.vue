<script setup lang="ts">
/**
 * 右栏 · 「结果」页签：把 33 号 §3.3 语义输出渲染成结论。
 *
 * 形态两种（与后端 OutputSchemaJson / ValidateSemanticOutput 一致）：
 *   doc_group   → { items: [ … ] }（批次，可能退化为单对象）
 *   doc_content → { … }（单对象）
 * 字段名逐字对齐 Schema，⛔ 不做 camelCase 改写。
 */
import { computed } from 'vue'
import { PROMPT_TYPE } from '@share/api/workflow/prompt-workbench'

const props = defineProps<{
  promptType: string
  json?: string | null
}>()

interface SemanticTag {
  tagCode?: string
  tagName?: string
  confidence?: number
  reason?: string
}
interface InfoItem {
  itemName?: string
  itemDesc?: string
  valueType?: string
  isKey?: boolean
}
interface FieldRow {
  name?: string
  value?: string
  isKeyField?: boolean
}
interface TableRow {
  index?: number
  name?: string
  rowCount?: number
  columnCount?: number
}
interface Doc {
  fileName?: string
  tags: SemanticTag[]
  purpose?: string
  typeGuess?: string
  infoItems?: InfoItem[]
  fields?: FieldRow[]
  tables?: TableRow[]
  suggestedPolicy?: string
  policyReason?: string
  confidence?: number
}

const POLICY: Record<string, 'success' | 'info' | 'warning' | 'primary'> = {
  analyze: 'success',
  skip: 'info',
  ignore: 'warning',
  hold: 'primary'
}

const parseError = computed(() => {
  if (!props.json) return '无 JSON 输出'
  try {
    JSON.parse(props.json)
    return ''
  } catch {
    return '输出不是合法 JSON'
  }
})

const docs = computed<Doc[]>(() => {
  if (!props.json) return []
  let root: any
  try {
    root = JSON.parse(props.json)
  } catch {
    return []
  }
  const list = Array.isArray(root?.items) ? root.items : [root]
  return list.filter((x: any) => x && typeof x === 'object')
})

/** 【…】分段拆行 —— purpose 四段式原样输出会糊成一坨 */
function purposeLines(text?: string): string[] {
  if (!text) return []
  return text
    .split(/(?=【)/g)
    .map((s) => s.trim())
    .filter(Boolean)
}
</script>

<template>
  <div class="sr">
    <div v-if="parseError" class="sr__none">{{ parseError }}</div>

    <div v-else class="sr__doc" v-for="(d, i) in docs" :key="i">
      <!-- 主行：文件 / 类型 + 策略 + 置信度 -->
      <div class="sr__head">
        <span class="sr__title">
          {{ d.fileName || (promptType === PROMPT_TYPE.Group ? `#${i + 1}` : d.typeGuess || '输出') }}
          <em v-if="promptType === PROMPT_TYPE.Group && d.typeGuess" class="sr__guess">
            {{ d.typeGuess }}
          </em>
        </span>
        <el-tag
          v-if="d.suggestedPolicy"
          size="small"
          effect="dark"
          :type="POLICY[d.suggestedPolicy] || 'info'"
          :title="d.policyReason || undefined"
        >
          {{ d.suggestedPolicy }}
        </el-tag>
        <span v-if="typeof d.confidence === 'number'" class="sr__conf" title="confidence">
          {{ Math.round(d.confidence * 100) }}%
        </span>
      </div>

      <!-- 分类结论 -->
      <div v-if="d.tags?.length" class="sr__tags">
        <span
          v-for="(t, ti) in d.tags"
          :key="ti"
          class="sr__tag"
          :title="[t.tagCode, t.reason].filter(Boolean).join(' · ')"
        >
          {{ t.tagName || t.tagCode }}
          <em v-if="t.tagCode && t.tagCode !== t.tagName">{{ t.tagCode }}</em>
        </span>
      </div>
      <div v-else class="sr__none">未返回标签</div>

      <!-- 作用结论 -->
      <template v-if="d.purpose">
        <div class="sr__label">作用</div>
        <p v-for="(line, li) in purposeLines(d.purpose)" :key="li" class="sr__purpose">
          {{ line }}
        </p>
      </template>

      <!-- 关键要素 -->
      <template v-if="d.infoItems?.length">
        <div class="sr__label">关键要素 <em>{{ d.infoItems.length }}</em></div>
        <ul class="sr__list">
          <li v-for="(it, ii) in d.infoItems" :key="ii">
            <b>{{ it.itemName }}</b>
            <span>{{ it.itemDesc }}</span>
            <em>{{ it.valueType }}<template v-if="it.isKey"> · 关键</template></em>
          </li>
        </ul>
      </template>

      <!-- 结构 -->
      <template v-if="d.fields?.length || d.tables?.length">
        <div class="sr__label">结构</div>
        <div class="sr__struct">
          <span v-if="d.fields?.length" class="sr__stat">
            字段 <b>{{ d.fields.length }}</b>
            <em :title="d.fields.map((f) => f.name).filter(Boolean).join(' / ')">
              {{ d.fields.filter((f) => f.isKeyField).map((f) => f.name).join(' / ') || '' }}
            </em>
          </span>
          <span v-for="(tb, tbi) in (d.tables || []).slice(0, 6)" :key="tbi" class="sr__stat">
            表格 <b>{{ tb.rowCount ?? 0 }}×{{ tb.columnCount ?? 0 }}</b>
            <em>{{ tb.name || `#${tb.index ?? tbi}` }}</em>
          </span>
          <!-- ⚠️ 截断必须显式告知：只显示前 6 个表格，若模型返回 20 个表格
               而界面静默截断，用户会以为「只识别出 6 个」—— 结论被界面误导。 -->
          <span v-if="(d.tables || []).length > 6" class="sr__stat sr__stat--more">
            … 另有 <b>{{ (d.tables || []).length - 6 }}</b> 个表格未展开
          </span>
        </div>
      </template>

      <!-- 策略理由 -->
      <p v-if="d.policyReason" class="sr__reason">{{ d.policyReason }}</p>

      <el-divider v-if="i < docs.length - 1" class="sr__div" />
    </div>
  </div>
</template>

<style scoped>
.sr {
  font-size: var(--yzh-font-size-sm);
}
.sr__doc {
  padding: 4px 0;
}
.sr__head {
  display: flex;
  align-items: center;
  gap: 8px;
}
.sr__title {
  flex: 1;
  min-width: 0;
  font-weight: 600;
  color: var(--el-text-color-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.sr__guess {
  font-style: normal;
  font-weight: 400;
  font-size: var(--yzh-font-size-xs);
  color: var(--el-text-color-secondary);
  margin-left: 6px;
}
.sr__conf {
  flex-shrink: 0;
  font-size: var(--yzh-font-size-xs);
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-secondary);
}

.sr__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 6px;
}
.sr__tag {
  display: inline-flex;
  align-items: baseline;
  gap: 5px;
  padding: 2px 8px;
  border: 1px solid var(--el-color-primary-light-7);
  border-radius: 3px;
  background: var(--el-color-primary-light-9);
  color: var(--el-color-primary);
  line-height: 1.5;
  cursor: default;
}
.sr__tag em {
  font-style: normal;
  font-size: var(--yzh-font-size-xs);
  color: var(--el-color-primary-dark-2);
  opacity: 0.75;
}

.sr__label {
  margin-top: 10px;
  font-size: var(--yzh-font-size-xs);
  color: var(--el-text-color-secondary);
}
.sr__label em {
  font-style: normal;
  font-variant-numeric: tabular-nums;
}

.sr__purpose {
  margin: 4px 0 0;
  line-height: 1.7;
  color: var(--el-text-color-regular);
  word-break: break-word;
}
.sr__purpose::before {
  content: '';
  display: inline-block;
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: var(--el-border-color);
  margin-right: 6px;
  vertical-align: 3px;
}

.sr__list {
  margin: 4px 0 0;
  padding: 0;
  list-style: none;
}
.sr__list li {
  display: flex;
  align-items: baseline;
  gap: 8px;
  padding: 3px 0;
  border-bottom: 1px dashed var(--el-border-color-lighter);
}
.sr__list li:last-child {
  border-bottom: none;
}
.sr__list b {
  flex-shrink: 0;
  min-width: 72px;
  font-weight: 600;
  color: var(--el-text-color-primary);
}
.sr__list span {
  flex: 1;
  min-width: 0;
  color: var(--el-text-color-regular);
}
.sr__list em {
  flex-shrink: 0;
  font-style: normal;
  font-size: var(--yzh-font-size-xs);
  color: var(--el-text-color-placeholder);
}

.sr__struct {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 14px;
  margin-top: 4px;
}
.sr__stat {
  font-size: var(--yzh-font-size-xs);
  color: var(--el-text-color-secondary);
}
.sr__stat b {
  font-variant-numeric: tabular-nums;
  color: var(--el-text-color-primary);
}
.sr__stat em {
  font-style: normal;
  margin-left: 5px;
  color: var(--el-text-color-placeholder);
}
.sr__stat--more {
  color: var(--el-color-warning);
}

.sr__reason {
  margin: 6px 0 0;
  font-size: var(--yzh-font-size-xs);
  line-height: 1.6;
  color: var(--el-text-color-secondary);
  word-break: break-word;
}

.sr__none {
  font-size: var(--yzh-font-size-xs);
  color: var(--el-text-color-placeholder);
  padding: 8px 0;
}
.sr__div {
  margin: 14px 0;
}
</style>
