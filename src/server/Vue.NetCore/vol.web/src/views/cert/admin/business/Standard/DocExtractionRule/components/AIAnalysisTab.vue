<template>
  <div class="ai-analysis-tab">
    <!-- AI 分析按钮 + 原始JSON开关 -->
    <div class="section-header">
      <div class="header-main-row">
        <h4>AI 自动分析</h4>
        <el-button type="primary" :loading="analyzing" @click="startAnalysis" class="analyze-btn">
          <el-icon><IconAnalyze /></el-icon>
          开始分析
        </el-button>
      </div>
      <div class="header-sub-row">
        <div class="switch-wrapper">
          <el-switch v-model="showRawJson" size="small" @change="onShowRawJsonChange" />
          <span class="switch-label">查看 AI 分析的原始 JSON 响应</span>
        </div>
      </div>
    </div>

    <el-alert v-if="!hasSkills" title="请先配置 AI 技能" type="info" :closable="false" show-icon />

    <!-- 原始JSON显示区 -->
    <div v-if="showRawJson && rawJsonDisplay" class="raw-json-section">
      <div class="section-title">
        <span class="section-title-text"
          ><el-icon><IconCode /></el-icon>原始响应 JSON</span
        >
        <el-button size="small" text @click="copyRawJson">
          <el-icon><IconCopy /></el-icon>复制
        </el-button>
      </div>
      <pre class="json-preview">{{ rawJsonDisplay }}</pre>
    </div>

    <!-- 提取字段列表 -->
    <div class="section">
      <div class="section-title">
        <span class="section-title-text"
          ><el-icon><IconCode /></el-icon>提取字段 ({{ localFields.length }})</span
        >
        <el-button size="small" @click="addField">
          <el-icon><IconAdd /></el-icon>添加
        </el-button>
      </div>

      <div class="field-list">
        <div
          v-for="(field, index) in localFields"
          :key="index"
          class="field-item"
          :class="{ manual: field.isManual }"
        >
          <div class="field-header">
            <div class="field-info-grid">
              <div class="info-item">
                <span class="info-label">字段名称</span>
                <el-input
                  v-model="field.name"
                  size="small"
                  placeholder="中文名"
                  class="field-input"
                />
              </div>
              <div class="info-item">
                <span class="info-label">唯一编码</span>
                <el-input
                  v-model="field.nameEn"
                  size="small"
                  placeholder="英文名"
                  class="field-input"
                  :class="{ 'input-error': fieldNameEnError(field, index) }"
                  @input="onFieldNameEnInput(field)"
                />
              </div>
            </div>
            <div class="field-ops">
              <el-select
                v-model="field.dataType"
                size="small"
                placeholder="类型"
                class="field-type-select"
              >
                <el-option label="文本" value="string" />
                <el-option label="数字" value="number" />
                <el-option label="日期" value="date" />
                <el-option label="布尔" value="boolean" />
              </el-select>
              <el-button
                type="danger"
                size="small"
                circle
                @click="removeField(index)"
                class="delete-btn"
              >
                <el-icon><IconDelete /></el-icon>
              </el-button>
            </div>
          </div>
          <div v-if="fieldNameEnError(field, index)" class="field-error-text">
            英文名重复：{{ field.nameEn }}
          </div>
          <div class="field-body">
            <el-input
              v-model="field.description"
              size="small"
              placeholder="字段描述（AI提取依据）"
              type="textarea"
              :rows="2"
            />
            <div class="field-footer">
              <div class="field-meta">
                <span v-if="field.extractedValue" class="extracted-info">
                  提取值：{{ field.extractedValue }}
                </span>
              </div>
              <el-switch
                v-model="field.isRequired"
                size="small"
                active-text="必填"
                class="required-switch"
              />
            </div>
          </div>
        </div>
      </div>

      <el-empty v-if="localFields.length === 0" description="暂无字段，点击AI分析或手动添加" />
    </div>

    <!-- 提取表格列表 -->
    <div class="section">
      <div class="section-title">
        <span class="section-title-text"
          ><el-icon><IconGrid /></el-icon>提取表格 ({{ localTables.length }})</span
        >
        <el-button size="small" @click="addTable">
          <el-icon><IconAdd /></el-icon>添加
        </el-button>
      </div>

      <div class="table-list">
        <div v-for="(table, index) in localTables" :key="index" class="table-item">
          <div class="table-header">
            <div class="field-info-grid">
              <div class="info-item">
                <span class="info-label">表格名称</span>
                <el-input
                  v-model="table.name"
                  size="small"
                  placeholder="中文表名"
                  class="field-input"
                />
              </div>
              <div class="info-item">
                <span class="info-label">唯一编码</span>
                <el-input
                  v-model="table.nameEn"
                  size="small"
                  placeholder="英文表名"
                  class="field-input"
                  :class="{ 'input-error': tableNameEnError(table, index) }"
                  @input="onTableNameEnInput(table)"
                />
              </div>
            </div>
            <el-button
              type="danger"
              size="small"
              circle
              @click="removeTable(index)"
              class="table-delete-btn"
            >
              <el-icon><IconDelete /></el-icon>
            </el-button>
          </div>
          <div v-if="tableNameEnError(table, index)" class="field-error-text">
            英文名重复：{{ table.nameEn }}
          </div>
          <div class="table-body">
            <el-input
              v-model="table.description"
              size="small"
              placeholder="表格描述（AI提取依据）"
              type="textarea"
              :rows="2"
            />

            <!-- 表格字段 -->
            <div class="table-fields">
              <div class="table-fields-header">
                <span>表格列定义</span>
                <el-button size="small" text @click="addTableField(index)">
                  <el-icon><IconAdd /></el-icon>添加列
                </el-button>
              </div>
              <!-- 提取数据预览（AI 已提取的行数据，展开查看） -->
              <div
                v-if="table.extractedData && table.extractedData.length"
                class="table-data-preview"
              >
                <div class="table-data-header">
                  <span>提取数据预览（{{ table.extractedData.length }} 行）</span>
                  <el-button size="small" text type="primary" @click="toggleDataPreview(index)">
                    {{ expandedTables.has(index) ? '收起' : '展开' }}
                  </el-button>
                </div>
                <el-table
                  v-if="expandedTables.has(index)"
                  :data="table.extractedData"
                  size="small"
                  border
                  max-height="320"
                >
                  <el-table-column
                    v-for="col in previewColumns(table)"
                    :key="col"
                    :prop="col"
                    :label="col"
                    min-width="110"
                    show-overflow-tooltip
                  />
                </el-table>
              </div>

              <template v-for="(col, colIndex) in table.columns" :key="colIndex">
                <div class="table-field-row">
                  <div class="col-inputs">
                    <el-input v-model="col.name" size="small" placeholder="列名" class="col-name" />
                    <el-input
                      v-model="col.nameEn"
                      size="small"
                      placeholder="英文名"
                      class="col-nameen"
                      :class="{ 'input-error': columnNameEnError(index, col, colIndex) }"
                      @input="onColumnNameEnInput(col)"
                    />
                  </div>
                  <div class="col-ops">
                    <el-select
                      v-model="col.dataType"
                      size="small"
                      placeholder="类型"
                      class="col-type-select"
                    >
                      <el-option label="文本" value="string" />
                      <el-option label="数字" value="number" />
                      <el-option label="日期" value="date" />
                    </el-select>
                    <div class="col-switches">
                      <el-switch
                        v-model="col.isRequired"
                        size="small"
                        active-text="必填"
                        class="required-switch"
                      />
                    </div>
                    <el-button
                      type="danger"
                      size="small"
                      text
                      @click="removeTableField(index, colIndex)"
                      class="col-delete-btn"
                    >
                      <el-icon><IconDelete /></el-icon>
                    </el-button>
                  </div>
                </div>
                <div v-if="columnNameEnError(index, col, colIndex)" class="field-error-text">
                  英文名重复：{{ col.nameEn }}
                </div>
              </template>
            </div>
          </div>
        </div>
      </div>

      <el-empty v-if="localTables.length === 0" description="暂无表格，点击AI分析或手动添加" />
    </div>
  </div>
</template>

<script setup>
import { IconAdd, IconAnalyze, IconCode, IconCopy, IconDelete, IconGrid } from '@/yzh'
import { ElMessage } from 'element-plus'
import { ref, watch } from 'vue'

const props = defineProps({
  fields: { type: Array, default: () => [] },
  tables: { type: Array, default: () => [] },
  // 供父组件传入原始JSON（由父组件从API响应中提取）
  rawJson: { type: String, default: '' },
  // 分析进行中的全局等待态（由父组件控制，覆盖整个异步调用周期）
  analyzing: { type: Boolean, default: false }
})

const emit = defineEmits(['analyze', 'update:fields', 'update:tables', 'update:rawJson'])

const hasSkills = ref(true)
const showRawJson = ref(false)
const rawJsonDisplay = ref('')

// 表格提取数据预览的展开状态（按表格索引）
const expandedTables = ref(new Set())

// 预览列 = 表格列定义名 ∪ 数据行中出现的键，保证列名与数据都能展示
const previewColumns = (table) => {
  const cols = new Set()
  ;(table.columns || []).forEach((c) => {
    if (c && c.name) cols.add(c.name)
  })
  const first = (table.extractedData || [])[0]
  if (first) Object.keys(first).forEach((k) => cols.add(k))
  return Array.from(cols)
}

const toggleDataPreview = (index) => {
  const next = new Set(expandedTables.value)
  if (next.has(index)) next.delete(index)
  else next.add(index)
  expandedTables.value = next
}

const localFields = ref([])
const localTables = ref([])

// ====== 英文名规范化与唯一性校验 ======
// 必须在 immediate watch 之前声明（immediate watch 在 setup 同步阶段就会调用这些函数）
// snake_case：小写 + 空格/非法字符转下划线 + 合并连续下划线
const normalizeEn = (v) =>
  (v || '')
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')

// nameEn 是唯一编辑源：code 始终与 nameEn 同步（保存/落库/工作流引用统一用 code=英文名）
// 修复：手动添加字段无 code、编辑英文名后 code 不更新，导致保存时提交旧英文名的问题
const normalizeField = (f) => {
  if (!f || typeof f !== 'object')
    return {
      name: '',
      nameEn: '',
      dataType: 'string',
      description: '',
      isRequired: false,
      isManual: false,
      isAiRecommended: true
    }
  f.nameEn = normalizeEn(f.nameEn ?? f.code)
  f.code = f.nameEn
  // 默认 isAiRecommended=true（AI 分析来的字段）；手动添加的为 false
  if (f.isAiRecommended === undefined) f.isAiRecommended = true
  return f
}

const normalizeTable = (t) => {
  if (!t || typeof t !== 'object') return { name: '', nameEn: '', description: '', columns: [] }
  t.nameEn = normalizeEn(t.nameEn ?? t.code)
  t.code = t.nameEn
  t.columns = (t.columns || []).map(normalizeColumn)
  return t
}

const normalizeColumn = (c) => {
  if (!c || typeof c !== 'object')
    return { name: '', nameEn: '', dataType: 'string', isRequired: false }
  c.nameEn = normalizeEn(c.nameEn ?? c.code)
  c.code = c.nameEn
  return c
}

// 父组件换引用（新数组）时才重载本地副本；去掉 deep，避免与本地编辑同步循环冲突
// 记录规范化后的 props 快照：仅当用户真实编辑（local 与 props 内容不同）时才回传父组件，
// 否则（规则加载回显）不 emit → 不会触发父组件 onFieldsUpdate 清空已回显的 Prompt
let propsFieldsJson = ''
let propsTablesJson = ''

watch(
  () => props.fields,
  (val) => {
    const normalized = (JSON.parse(JSON.stringify(val)) || []).map(normalizeField)
    localFields.value = normalized
    propsFieldsJson = JSON.stringify(normalized)
  },
  { immediate: true }
)

watch(
  () => props.tables,
  (val) => {
    const normalized = (JSON.parse(JSON.stringify(val)) || []).map(normalizeTable)
    localTables.value = normalized
    propsTablesJson = JSON.stringify(normalized)
  },
  { immediate: true }
)

// ====== 本地编辑实时同步父组件（修复：字段/表格内容编辑不 emit 导致保存提交旧数据）======
// 任何编辑（增删/改名称/英文名/描述/类型/开关）都同步到父组件，保存时提交最新数据
// 通过 JSON 比较去重：① 与 props 快照相同 = 规则回显/加载，不 emit；② 与上次 emit 相同 = 回环，不重复 emit
let lastEmittedFieldsJson = ''
let lastEmittedTablesJson = ''

watch(
  localFields,
  (val) => {
    const json = JSON.stringify(val)
    if (json === propsFieldsJson) return
    if (json !== lastEmittedFieldsJson) {
      lastEmittedFieldsJson = json
      emit('update:fields', JSON.parse(JSON.stringify(val)))
    }
  },
  { deep: true }
)

watch(
  localTables,
  (val) => {
    const json = JSON.stringify(val)
    if (json === propsTablesJson) return
    if (json !== lastEmittedTablesJson) {
      lastEmittedTablesJson = json
      emit('update:tables', JSON.parse(JSON.stringify(val)))
    }
  },
  { deep: true }
)

watch(
  () => props.rawJson,
  (val) => {
    rawJsonDisplay.value = val
  },
  { immediate: true }
)

const addField = () => {
  localFields.value.push({
    name: '',
    nameEn: '',
    code: '',
    dataType: 'string',
    description: '',
    isRequired: false,
    isManual: false,
    isAiRecommended: false
  })
  emit('update:fields', localFields.value)
}
const removeField = (index) => {
  localFields.value.splice(index, 1)
  emit('update:fields', localFields.value)
}
const addTable = () => {
  localTables.value.push({
    name: '',
    nameEn: '',
    code: '',
    description: '',
    columns: [],
    isAiRecommended: true
  })
  emit('update:tables', localTables.value)
}
const removeTable = (index) => {
  localTables.value.splice(index, 1)
  emit('update:tables', localTables.value)
}
const addTableField = (tableIndex) => {
  localTables.value[tableIndex].columns.push({
    name: '',
    nameEn: '',
    code: '',
    dataType: 'string',
    isRequired: false
  })
  emit('update:tables', localTables.value)
}
const removeTableField = (tableIndex, colIndex) => {
  localTables.value[tableIndex].columns.splice(colIndex, 1)
  emit('update:tables', localTables.value)
}

// 输入时实时规范化（小写 + 非法字符转下划线），code 同步跟随 nameEn
const onFieldNameEnInput = (field) => {
  field.nameEn = normalizeEn(field.nameEn)
  field.code = field.nameEn
}
const onTableNameEnInput = (table) => {
  table.nameEn = normalizeEn(table.nameEn)
  table.code = table.nameEn
}
const onColumnNameEnInput = (col) => {
  col.nameEn = normalizeEn(col.nameEn)
  col.code = col.nameEn
}

// 字段英文名在同一文档内必须唯一
const fieldNameEnError = (field, index) => {
  const en = normalizeEn(field.nameEn)
  if (!en) return false
  return localFields.value.some((f, i) => i !== index && normalizeEn(f.nameEn) === en)
}

// 表格英文名在同一文档内必须唯一
const tableNameEnError = (table, index) => {
  const en = normalizeEn(table.nameEn)
  if (!en) return false
  return localTables.value.some((t, i) => i !== index && normalizeEn(t.nameEn) === en)
}

// 列英文名在同一表格内必须唯一
const columnNameEnError = (tableIndex, col, colIndex) => {
  const cols = localTables.value[tableIndex]?.columns || []
  const en = normalizeEn(col.nameEn)
  if (!en) return false
  return cols.some((c, i) => i !== colIndex && normalizeEn(c.nameEn) === en)
}

const startAnalysis = () => {
  // 等待态由父组件通过 analyzing prop 控制（覆盖整个异步分析周期）
  emit('analyze')
}

const onShowRawJsonChange = (val) => {
  if (!val) rawJsonDisplay.value = ''
}

const copyRawJson = () => {
  navigator.clipboard.writeText(rawJsonDisplay.value).then(() => {
    ElMessage.success('已复制到剪贴板')
  })
}
</script>

<style scoped>
/* yzh 设计令牌 */
@import '@/yzh/styles/yzh.css';

.ai-analysis-tab {
  height: 100%;
  overflow-y: auto;
}

.section-header {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: var(--yzh-space-5, 20px);
  padding-bottom: var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.header-main-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
}

.header-sub-row {
  display: flex;
  align-items: center;
  width: 100%;
}

.section-header h4 {
  margin: 0;
  font-size: var(--yzh-font-size-lg, 15px);
  font-weight: var(--yzh-font-weight-bold, 600);
  color: var(--yzh-color-text-primary, #303133);
  white-space: nowrap;
}
.switch-wrapper {
  display: flex;
  align-items: center;
  gap: 8px;
  white-space: nowrap;
}
.switch-label {
  font-size: 12px;
  color: var(--yzh-color-text-secondary, #909399);
}
.analyze-btn {
  min-width: 100px;
  flex-shrink: 0;
}
.section-title-text {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
}
.section-title-text .el-icon {
  font-size: 14px;
  color: var(--yzh-color-text-secondary, #909399);
}

/* 提取字段/表格项重构 */
.field-header {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 16px;
}

.table-header {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  margin-bottom: 16px;
}

.field-info-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  flex: 1;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.info-label {
  font-size: 12px;
  color: var(--yzh-color-text-secondary, #909399);
  font-weight: 500;
}

.table-delete-btn {
  margin-top: 22px; /* 精确对齐输入框 */
  flex-shrink: 0;
}

.field-ops {
  display: flex;
  align-items: center;
  gap: 8px;
  justify-content: flex-end;
  padding-top: 8px;
  border-top: 1px dashed var(--yzh-color-border-light, #ebeef5);
}

.field-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-top: 10px;
  padding-top: 10px;
  border-top: 1px dashed var(--yzh-color-border-light, #ebeef5);
}

.field-meta {
  display: flex;
  align-items: center;
  gap: 8px;
}

.extracted-info {
  font-size: 12px;
  color: #67c23a;
  background: #f0f9eb;
  padding: 4px 8px;
  border: 1px solid #e1f3d8;
}

.required-switch {
  white-space: nowrap;
}

.required-switch :deep(.el-switch__label) {
  font-size: 12px;
  font-weight: 500;
}

.field-type-select {
  width: 120px !important;
}

.delete-btn {
  flex-shrink: 0;
}

.field-body,
.table-body {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

/* 原始JSON区域 */
.raw-json-section {
  margin-bottom: 20px;
  border: 1px solid #e4e7ed;
  border-radius: 0;
  overflow: hidden;
}
.raw-json-section .section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  background: #f5f7fa;
  font-size: 13px;
  color: #606266;
  border-bottom: 1px solid #e4e7ed;
}
.json-preview {
  margin: 0;
  padding: 16px;
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: 12px;
  line-height: 1.6;
  color: #303133;
  background: #fafafa;
  white-space: pre-wrap;
  word-break: break-all;
  max-height: 400px;
  overflow-y: auto;
}

.section {
  margin-bottom: 28px;
}
.section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
  font-weight: 600;
  font-size: 14px;
  color: #606266;
}
.field-list,
.table-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.field-item,
.table-item {
  position: relative;
  border: 1px solid #e4e7ed;
  border-radius: 0;
  padding: 16px;
  background: #fff;
  box-shadow: none;
  transition: all 0.3s;
}
.field-item::before,
.field-item::after,
.table-item::before,
.table-item::after {
  display: none !important;
  content: none !important;
}
.field-item:hover,
.table-item:hover {
  border-color: #c6e2ff;
  box-shadow: none;
}
.field-item.manual {
  border-left: 3px solid #e6a23c;
  background: #fff;
}

/* 英文名重复校验 */
.input-error :deep(.el-input__wrapper) {
  box-shadow: 0 0 0 1px #f56c6c inset;
}
.field-error-text {
  margin: -4px 0 10px;
  font-size: 12px;
  line-height: 1.5;
  color: #f56c6c;
}

.table-fields {
  margin-top: 12px;
  padding: 12px;
  background: #f8fafc;
  border-radius: 0;
  border: 1px dashed var(--yzh-color-border-light, #ebeef5);
}
.table-fields-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
  font-size: 12px;
  color: #606266;
  font-weight: 600;
}
.table-field-row {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 12px;
  background: #fff;
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  margin-bottom: 8px;
}
.col-inputs {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}
.col-ops {
  display: flex;
  align-items: center;
  gap: 16px;
  justify-content: flex-end;
  border-top: 1px dashed var(--yzh-color-border-light, #f1f5f9);
  padding-top: 8px;
}
.col-switches {
  display: flex;
  align-items: center;
  white-space: nowrap;
}
.col-type-select {
  width: 100px !important;
}
.col-delete-btn {
  margin-left: 4px;
  padding: 0 !important;
}

/* 提取数据预览 */
.table-data-preview {
  margin-top: 12px;
  padding: 12px;
  background: #f0f9eb;
  border: 1px solid #e1f3d8;
  border-radius: 6px;
}
.table-data-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 8px;
  font-size: 12px;
  color: #529b2e;
  font-weight: 600;
}

.field-meta {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
}
.field-tag-ai {
  flex-shrink: 0;
}
.field-extracted {
  max-width: 100%;
  white-space: normal;
  word-break: break-all;
  line-height: 1.5;
}
</style>
