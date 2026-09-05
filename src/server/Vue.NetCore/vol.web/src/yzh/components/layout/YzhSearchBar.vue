<script setup lang="ts">
/**
 * YzhSearchBar - 紧凑 Grid 布局搜索栏
 * 特点：
 * - 单行 4 列 Grid 布局
 * - 超过 4 个字段自动折叠（可配置）
 * - 与 YzhTable 联动通过 v-model:default-values + @search/@reset
 */
import { reactive, ref, watch } from 'vue'
import type { SearchField } from '../table/types'

const props = withDefaults(
  defineProps<{
    fields: SearchField[]
    collapsible?: boolean
    defaultValues?: Record<string, any>
    cols?: number
  }>(),
  { collapsible: true, cols: 4 }
)

const emit = defineEmits<{
  (e: 'search', values: Record<string, any>): void
  (e: 'reset'): void
}>()

const formValues = reactive<Record<string, any>>({})
const expanded = ref(false)

watch(
  () => props.defaultValues,
  (val) => {
    if (val) {
      Object.keys(formValues).forEach((k) => delete formValues[k])
      Object.assign(formValues, val)
    }
  },
  { immediate: true, deep: true }
)

function getDefault(field: SearchField) {
  if (formValues[field.prop] !== undefined) return formValues[field.prop]
  if (field.defaultValue !== undefined) return field.defaultValue
  if (field.type === 'dateRange') return []
  return ''
}

const visibleFields = () => {
  if (!props.collapsible || expanded.value) return props.fields
  return props.fields.slice(0, props.cols)
}

function onSearch() {
  const payload: Record<string, any> = {}
  props.fields.forEach((f) => {
    const v = formValues[f.prop]
    if (v !== undefined && v !== '' && !(Array.isArray(v) && v.length === 0)) {
      payload[f.prop] = v
    }
  })
  emit('search', payload)
}

function onReset() {
  props.fields.forEach((f) => {
    delete formValues[f.prop]
  })
  emit('reset')
}
</script>

<template>
  <div class="yzh-search-bar">
    <el-form :model="formValues" inline label-width="80px" size="default">
      <el-row :gutter="12" class="yzh-search-bar__row">
        <template v-for="field in visibleFields()" :key="field.prop">
          <el-col :span="24 / cols" class="yzh-search-bar__col">
            <el-form-item :label="field.label">
              <el-input
                v-if="!field.type || field.type === 'text'"
                v-model="formValues[field.prop]"
                :placeholder="field.placeholder || `请输入${field.label}`"
                clearable
                @keyup.enter="onSearch"
              />
              <el-input-number
                v-else-if="field.type === 'number'"
                v-model="formValues[field.prop]"
                :placeholder="field.placeholder || `请输入${field.label}`"
                style="width: 100%"
              />
              <el-select
                v-else-if="field.type === 'select'"
                v-model="formValues[field.prop]"
                :placeholder="field.placeholder || `请选择${field.label}`"
                clearable
                filterable
                style="width: 100%"
              >
                <el-option
                  v-for="opt in field.options || []"
                  :key="opt.value"
                  :label="opt.label"
                  :value="opt.value"
                />
              </el-select>
              <el-date-picker
                v-else-if="field.type === 'date'"
                v-model="formValues[field.prop]"
                type="date"
                :placeholder="field.placeholder || `请选择${field.label}`"
                value-format="YYYY-MM-DD"
                style="width: 100%"
              />
              <el-date-picker
                v-else-if="field.type === 'dateRange'"
                v-model="formValues[field.prop]"
                type="daterange"
                :placeholder="field.placeholder || `请选择${field.label}`"
                value-format="YYYY-MM-DD"
                range-separator="至"
                start-placeholder="开始日期"
                end-placeholder="结束日期"
                style="width: 100%"
              />
            </el-form-item>
          </el-col>
        </template>

        <el-col :span="24 / cols" class="yzh-search-bar__actions">
          <el-form-item>
            <el-button type="primary" @click="onSearch">
              <el-icon><Search /></el-icon>
              查询
            </el-button>
            <el-button @click="onReset">
              <el-icon><RefreshLeft /></el-icon>
              重置
            </el-button>
            <el-button
              v-if="collapsible && fields.length > cols"
              text
              @click="expanded = !expanded"
            >
              {{ expanded ? '收起' : '展开' }}
              <el-icon>
                <ArrowUp v-if="expanded" />
                <ArrowDown v-else />
              </el-icon>
            </el-button>
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>
  </div>
</template>

<style scoped>
.yzh-search-bar {
  padding: 16px 20px 0;
  background: #fff;
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}

.yzh-search-bar__row {
  margin: 0 !important;
}

.yzh-search-bar__col,
.yzh-search-bar__actions {
  padding-bottom: 16px;
}

.yzh-search-bar__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
}

.yzh-search-bar__actions :deep(.el-form-item) {
  margin: 0;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  justify-content: flex-end;
}
</style>
