<script setup lang="ts">
/**
 * YzhSearchBar - 单行 Grid 布局搜索栏
 * 特点：
 * - 单行布局，查询和重置按钮右对齐
 * - 使用 CSS Grid 实现响应式
 * - 与 YzhTable 联动通过 v-model:default-values + @search/@reset
 */
import { reactive, watch } from 'vue'
import type { SearchField } from '../table/types'

const props = withDefaults(
  defineProps<{
    fields: SearchField[]
    defaultValues?: Record<string, any>
    cols?: number
  }>(),
  { cols: 4 }
)

const emit = defineEmits<{
  (e: 'search', values: Record<string, any>): void
  (e: 'reset'): void
}>()

const formValues = reactive<Record<string, any>>({})

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
    <div class="yzh-search-bar__grid">
      <div 
        v-for="field in fields" 
        :key="field.prop" 
        class="yzh-search-bar__item"
      >
        <div class="yzh-search-bar__field">
          <label class="yzh-search-bar__label">{{ field.label }}</label>
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
        </div>
      </div>
      
      <!-- 查询和重置按钮 -->
      <div class="yzh-search-bar__actions">
        <el-button type="primary" @click="onSearch">
          <i class="bi bi-search"></i>
          查询
        </el-button>
        <el-button @click="onReset">
          <i class="bi bi-arrow-counterclockwise"></i>
          重置
        </el-button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.yzh-search-bar {
  width: 100%;
}

.yzh-search-bar__grid {
  display: grid;
  grid-template-columns: repeat(v-bind('props.cols'), 1fr);
  gap: 12px;
  align-items: end;
}

.yzh-search-bar__item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.yzh-search-bar__field {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.yzh-search-bar__label {
  font-size: 13px;
  color: var(--yzh-color-text-secondary, #64748b);
  line-height: 1.5;
}

.yzh-search-bar__actions {
  display: flex;
  align-items: flex-end;
  justify-content: flex-end;
  gap: 8px;
  padding-bottom: 2px;
  grid-column: span 1;
}

@media (max-width: 768px) {
  .yzh-search-bar__grid {
    grid-template-columns: 1fr;
  }
  
  .yzh-search-bar__actions {
    grid-column: 1 / -1;
    justify-content: flex-start;
  }
}
</style>
