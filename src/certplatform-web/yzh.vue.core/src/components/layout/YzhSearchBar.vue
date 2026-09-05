<script setup lang="ts">
/**
 * YzhSearchBar - 搜索栏组件
 * 布局：左侧查询条件（一行显示，label+input），右侧按钮
 * - 查询条件采用 flex 布局，label 和 input 在一行
 * - 查询条件之间用 flex:1 自动填充空白
 * - 查询和重置按钮固定在右侧
 */
import { reactive, watch } from 'vue'
import type { SearchField } from '../table/types'

const props = withDefaults(
  defineProps<{
    fields: SearchField[]
    defaultValues?: Record<string, any>
    cols?: number
    maxFields?: number
    inputWidth?: string | number
  }>(),
  { cols: 2, maxFields: 2, inputWidth: '200px' }
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

// 只取前 maxFields 个字段作为查询条件
const searchFields = props.fields.slice(0, props.maxFields)

function onSearch() {
  const payload: Record<string, any> = {}
  searchFields.forEach((f) => {
    const v = formValues[f.prop]
    if (v !== undefined && v !== '' && !(Array.isArray(v) && v.length === 0)) {
      payload[f.prop] = v
    }
  })
  emit('search', payload)
}

function onReset() {
  searchFields.forEach((f) => {
    delete formValues[f.prop]
  })
  emit('reset')
}
</script>

<template>
  <div class="yzh-search-bar">
    <div class="yzh-search-bar__inner">
      <!-- 查询条件区域 -->
      <div class="yzh-search-bar__fields">
        <div
          v-for="field in searchFields"
          :key="field.prop"
          class="yzh-search-bar__field"
        >
          <div class="yzh-search-bar__field-row">
            <label class="yzh-search-bar__label">{{ field.label }}</label>
            <div class="yzh-search-bar__input-wrap" :style="{ width: inputWidth }">
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
              />
              <el-select
                v-else-if="field.type === 'select'"
                v-model="formValues[field.prop]"
                :placeholder="field.placeholder || `请选择${field.label}`"
                clearable
                filterable
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
              />
            </div>
          </div>
        </div>
        <!-- 自动填充空白 -->
        <div class="yzh-search-bar__spacer" />
      </div>

      <!-- 右侧按钮 -->
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

.yzh-search-bar__inner {
  display: flex;
  align-items: center;
  gap: 16px;
}

.yzh-search-bar__fields {
  display: flex;
  align-items: center;
  gap: 16px;
  flex: 1;
}

.yzh-search-bar__field {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.yzh-search-bar__field-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.yzh-search-bar__label {
  font-size: 14px;
  color: var(--yzh-color-text-secondary, #64748b);
  white-space: nowrap;
}

.yzh-search-bar__input-wrap {
  flex-shrink: 0;
}

.yzh-search-bar__spacer {
  flex: 1;
  min-width: 16px;
}

.yzh-search-bar__actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

@media (max-width: 768px) {
  .yzh-search-bar__inner {
    flex-direction: column;
    align-items: stretch;
  }

  .yzh-search-bar__fields {
    flex-direction: column;
    gap: 12px;
  }

  .yzh-search-bar__spacer {
    display: none;
  }

  .yzh-search-bar__actions {
    justify-content: flex-end;
  }
}
</style>
