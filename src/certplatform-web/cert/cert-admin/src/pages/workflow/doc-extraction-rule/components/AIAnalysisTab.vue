<script setup lang="ts">
import { ref, watch } from 'vue'
import { MagicStick, Plus, Delete, View } from '@element-plus/icons-vue'
import type { FieldDefDto, TableDefDto } from '@share/api/workflow/doc-extraction-rule'

const props = defineProps<{
  fields: FieldDefDto[]
  tables: TableDefDto[]
  analyzing: boolean
}>()

const emit = defineEmits<{
  analyze: []
  'update:fields': [fields: FieldDefDto[]]
  'update:tables': [tables: TableDefDto[]]
}>()

const showRawJson = ref(false)
const rawJson = ref('')
const localFields = ref<FieldDefDto[]>([])
const localTables = ref<TableDefDto[]>([])
const expandedTables = ref<number[]>([])

watch(() => props.fields, (v) => {
  localFields.value = JSON.parse(JSON.stringify(v || []))
}, { immediate: true })

watch(() => props.tables, (v) => {
  localTables.value = JSON.parse(JSON.stringify(v || []))
}, { immediate: true })

function normalizeEn(v: string): string {
  return (v || '').toLowerCase().replace(/[^a-z0-9]/g, '_').replace(/^_+|_+$/g, '')
}

function addField() {
  localFields.value.push({
    name: '', nameEn: '', code: '', dataType: 'string',
    isRequired: false, isManual: false, isAiRecommended: false
  })
}

function removeField(i: number) {
  localFields.value.splice(i, 1)
  emit('update:fields', [...localFields.value])
}

function onFieldChange() {
  emit('update:fields', [...localFields.value])
}

function addTable() {
  localTables.value.push({
    name: '', nameEn: '', code: '', columns: [],
    isAiRecommended: false
  })
  expandedTables.value = [...expandedTables.value, localTables.value.length - 1]
}

function removeTable(i: number) {
  localTables.value.splice(i, 1)
  emit('update:tables', [...localTables.value])
  expandedTables.value = expandedTables.value.filter(v => v !== i)
}

function addTableColumn(table: TableDefDto) {
  table.columns.push({ name: '', nameEn: '', code: '', dataType: 'string', isRequired: false })
}

function removeTableColumn(table: TableDefDto, i: number) {
  table.columns.splice(i, 1)
  emit('update:tables', [...localTables.value])
}

function fieldNameEnError(field: FieldDefDto, i: number): boolean {
  const code = normalizeEn(field.nameEn || field.code)
  if (!code) return false
  return localFields.value.some((f, j) => j !== i && normalizeEn(f.nameEn || f.code) === code)
}

function tableNameEnError(table: TableDefDto, i: number): boolean {
  const code = normalizeEn(table.nameEn || table.code)
  if (!code) return false
  return localTables.value.some((t, j) => j !== i && normalizeEn(t.nameEn || t.code) === code)
}

function onFieldBlur(field: FieldDefDto) {
  field.nameEn = normalizeEn(field.nameEn || '')
  field.code = field.nameEn
  emit('update:fields', [...localFields.value])
}

function onTableBlur(table: TableDefDto) {
  table.nameEn = normalizeEn(table.nameEn || '')
  table.code = table.nameEn
  emit('update:tables', [...localTables.value])
}

function previewColumns(table: TableDefDto): string[] {
  const first = table.extractedData?.[0]
  return first ? Object.keys(first) : table.columns.map(c => c.code || c.name)
}

function toggleDataPreview(i: number) {
  if (expandedTables.value.includes(i)) {
    expandedTables.value = expandedTables.value.filter(v => v !== i)
  } else {
    expandedTables.value = [...expandedTables.value, i]
  }
}
</script>

<template>
  <div class="ai-analysis-tab">
    <div class="section-header">
      <h4>AI 自动分析</h4>
      <div class="header-actions">
        <el-switch v-model="showRawJson" active-text="JSON" size="small" style="margin-right: 12px" />
        <el-button type="primary" size="small" :loading="analyzing" @click="emit('analyze')">
          <el-icon><MagicStick /></el-icon> 开始分析
        </el-button>
      </div>
    </div>

    <div v-if="showRawJson && rawJson" class="raw-json-section">
      <pre class="raw-json">{{ rawJson }}</pre>
    </div>

    <div class="section">
      <div class="section-title">
        <span>字段定义（{{ localFields.length }}）</span>
        <el-button link type="primary" size="small" @click="addField">
          <el-icon><Plus /></el-icon> 添加
        </el-button>
      </div>
      <div v-if="!localFields.length" class="empty-hint">暂无字段，请点击"开始分析"或手动添加</div>
      <div v-for="(field, i) in localFields" :key="i" class="field-item" :class="{ manual: field.isManual }">
        <div class="field-row">
          <el-input v-model="field.name" placeholder="中文名" size="small" style="flex:1" @change="onFieldChange" />
          <el-input v-model="field.nameEn" placeholder="English Name" size="small" style="flex:1"
            @blur="onFieldBlur(field)" :class="{ 'is-error': fieldNameEnError(field, i) }" />
          <el-select v-model="field.dataType" size="small" style="width: 90px" @change="onFieldChange">
            <el-option label="string" value="string" />
            <el-option label="number" value="number" />
            <el-option label="date" value="date" />
            <el-option label="boolean" value="boolean" />
          </el-select>
          <el-switch v-model="field.isRequired" size="small" @change="onFieldChange" />
          <el-button link type="danger" :icon="Delete" @click="removeField(i)" />
        </div>
        <div v-if="fieldNameEnError(field, i)" class="error-text">英文名重复</div>
        <el-input v-model="field.description" placeholder="描述" size="small" type="textarea" :rows="1"
          style="margin-top:4px" @change="onFieldChange" />
        <div v-if="field.extractedValue" class="extracted-value">
          <span class="label">提取值：</span>{{ field.extractedValue }}
        </div>
      </div>
    </div>

    <div class="section">
      <div class="section-title">
        <span>表格定义（{{ localTables.length }}）</span>
        <el-button link type="primary" size="small" @click="addTable">
          <el-icon><Plus /></el-icon> 添加
        </el-button>
      </div>
      <div v-if="!localTables.length" class="empty-hint">暂无表格</div>
      <el-collapse v-model="expandedTables">
        <el-collapse-item v-for="(table, i) in localTables" :key="i" :name="i">
          <template #title>
            <span class="table-title">
              {{ table.name || table.nameEn || `表格 ${i + 1}` }}
              <el-tag v-if="table.isAiRecommended" size="small" type="success">AI推荐</el-tag>
            </span>
          </template>
          <div class="table-form">
            <div class="field-row">
              <el-input v-model="table.name" placeholder="中文名" size="small" style="flex:1" />
              <el-input v-model="table.nameEn" placeholder="English Name" size="small" style="flex:1"
                @blur="onTableBlur(table)" :class="{ 'is-error': tableNameEnError(table, i) }" />
              <el-button link type="danger" :icon="Delete" @click="removeTable(i)" />
            </div>
            <el-input v-model="table.description" placeholder="描述" size="small" type="textarea" :rows="1" style="margin-top:8px" />

            <div v-if="table.extractedData?.length" style="margin-top:8px">
              <el-button link size="small" @click="toggleDataPreview(i)">
                <el-icon><View /></el-icon> 预览提取数据（{{ table.extractedData.length }} 行）
              </el-button>
              <el-table v-if="expandedTables.includes(i)" :data="table.extractedData" size="small" border style="margin-top:4px" max-height="200">
                <el-table-column v-for="col in previewColumns(table)" :key="col" :prop="col" :label="col" min-width="100" show-overflow-tooltip />
              </el-table>
            </div>

            <div style="margin-top:8px">
              <div class="section-title" style="margin-top:8px">
                <span>列定义</span>
                <el-button link type="primary" size="small" @click="addTableColumn(table)">
                  <el-icon><Plus /></el-icon> 添加列
                </el-button>
              </div>
              <el-table :data="table.columns" size="small" border>
                <el-table-column label="列名" min-width="120">
                  <template #default="{ row }">
                    <el-input v-model="row.name" size="small" placeholder="中文名" />
                  </template>
                </el-table-column>
                <el-table-column label="编码" min-width="120">
                  <template #default="{ row }">
                    <el-input v-model="row.nameEn" size="small" placeholder="code"
                      @blur="row.code = normalizeEn(row.nameEn || '')" />
                  </template>
                </el-table-column>
                <el-table-column label="类型" width="90">
                  <template #default="{ row }">
                    <el-select v-model="row.dataType" size="small">
                      <el-option label="string" value="string" />
                      <el-option label="number" value="number" />
                      <el-option label="date" value="date" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column label="操作" width="50">
                  <template #default="{ $index }">
                    <el-button link type="danger" :icon="Delete" @click="removeTableColumn(table, $index)" />
                  </template>
                </el-table-column>
              </el-table>
            </div>
          </div>
        </el-collapse-item>
      </el-collapse>
    </div>
  </div>
</template>

<style scoped>
.ai-analysis-tab { padding: 0; }
.section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.header-actions { display: flex; align-items: center; }
.section { margin-bottom: 16px; }
.section-title { display: flex; justify-content: space-between; align-items: center; font-weight: 500; margin-bottom: 8px; }
.empty-hint { color: #909399; font-size: 13px; padding: 12px 0; }
.field-item { padding: 8px; border: 1px solid #ebeef5; border-radius: 4px; margin-bottom: 8px; }
.field-item.manual { border-left: 3px solid #e6a23c; }
.field-row { display: flex; gap: 8px; align-items: center; }
.error-text { color: #f56c6c; font-size: 12px; margin-top: 4px; }
.is-error :deep(.el-input__wrapper) { box-shadow: 0 0 0 1px #f56c6c inset; }
.extracted-value { font-size: 12px; color: #67c23a; margin-top: 4px; }
.extracted-value .label { color: #909399; }
.table-title { display: flex; align-items: center; gap: 8px; }
.table-form { padding: 8px 0; }
.raw-json-section { margin-bottom: 12px; }
.raw-json { background: #1e1e1e; color: #d4d4d4; padding: 12px; border-radius: 4px; font-size: 12px; max-height: 200px; overflow: auto; margin: 0; }
</style>
