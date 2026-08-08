<template>
  <div v-if="visible" class="yzh-search-bar" :class="[`yzh-search-bar--${mode}`]">
    <el-form
      ref="formRef"
      :model="searchForm"
      inline
      label-width="auto"
      :size="size"
      class="yzh-search-bar__form"
    >
      <!-- 搜索字段（根据配置动态渲染） -->
      <el-form-item
        v-for="field in searchFields"
        :key="field.fieldAlias || field.fieldName"
        :label="field.searchTitle || field.formTitle"
        :style="{ marginBottom: '8px' }"
      >
        <!-- 下拉选择 -->
        <el-select
          v-if="fieldControlType(field) === 'select'"
          v-model="searchForm[field.fieldName]"
          :placeholder="field.searchPlaceholder || `请选择${field.searchTitle || field.formTitle}`"
          clearable
          filterable
          :style="{ width: (field.searchWidth || 180) + 'px' }"
          @change="onFieldChange(field.fieldName, $event)"
        >
          <el-option
            v-for="opt in getDictOptions(field)"
            :key="opt.value"
            :label="opt.label"
            :value="opt.value"
          />
        </el-select>

        <!-- 日期选择 -->
        <el-date-picker
          v-else-if="fieldControlType(field) === 'date'"
          v-model="searchForm[field.fieldName]"
          type="date"
          :placeholder="field.searchPlaceholder || '请选择日期'"
          value-format="YYYY-MM-DD"
          :style="{ width: (field.searchWidth || 180) + 'px' }"
          @change="onFieldChange(field.fieldName, $event)"
        />

        <!-- 数字输入 -->
        <el-input-number
          v-else-if="fieldControlType(field) === 'number' || fieldControlType(field) === 'decimal'"
          v-model="searchForm[field.fieldName]"
          :placeholder="field.searchPlaceholder"
          :controls="false"
          :style="{ width: (field.searchWidth || 150) + 'px' }"
          @change="onFieldChange(field.fieldName, $event)"
        />

        <!-- 默认文本输入 -->
        <el-input
          v-else
          v-model="searchForm[field.fieldName]"
          :placeholder="field.searchPlaceholder || `请输入${field.searchTitle || field.formTitle}`"
          clearable
          :style="{ width: (field.searchWidth || 200) + 'px' }"
          @keyup.enter="handleSearch"
          @change="onFieldChange(field.fieldName, $event)"
        />
      </el-form-item>

      <!-- 操作按钮 -->
      <el-form-item :style="{ marginBottom: '8px' }">
        <el-button type="primary" icon="Search" @click="handleSearch">查询</el-button>
        <el-button icon="RefreshRight" @click="handleReset">重置</el-button>
        <el-button
          v-if="mode === 'togglable'"
          link
          type="info"
          @click="expanded = !expanded"
        >
          {{ expanded ? '收起' : '展开' }}
          <el-icon>
            <ArrowUp v-if="expanded" />
            <ArrowDown v-else />
          </el-icon>
        </el-button>

        <!-- 自定义按钮插槽 -->
        <slot name="actions" />
      </el-form-item>
    </el-form>

    <!-- 自定义底部区域 -->
    <div v-if="$slots.extra" class="yzh-search-bar__extra">
      <slot name="extra" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch, onMounted } from 'vue'
import { ArrowUp, ArrowDown } from '@element-plus/icons-vue'
import type { IYzhFieldConfig, YzhControlType, YzhSearchMode } from '../types/YZHV3Config'

// ====== Props ======
const props = withDefaults(defineProps<{
  /** 字段配置列表 */
  fields: IYzhFieldConfig[]
  /** 搜索模式 */
  mode?: YzhSearchMode
  /** 表单尺寸 */
  size?: 'large' | 'default' | 'small'
  /** 初始搜索值 */
  initialValues?: Record<string, any>
}>(), {
  mode: 'fixed',
  size: 'default',
  initialValues: () => ({}),
})

// ====== Emits ======
const emit = defineEmits<{
  (e: 'search', params: Record<string, any>): void
  (e: 'reset'): void
  (e: 'fieldChange', fieldName: string, value: any): void
  (e: 'ready', instance: any): void
}>()

// ====== 状态 ======
const formRef = ref()
const expanded = ref(true)
const searchForm = reactive<Record<string, any>>({})
const dictCache = ref<Record<string, Array<{ value: string; label: string }>>>({})

// ====== 计算属性 ======

/** 是否可见 */
const visible = computed(() => props.mode !== 'hidden')

/** 过滤可搜索的字段（searchFlag=true） */
const searchFields = computed(() => {
  return props.fields.filter(f => f.searchFlag && f.controlType !== 'hidden')
})

/**
 * 获取搜索字段的控件类型
 * 优先使用 searchControlType，回退到 controlType
 */
function fieldControlType(field: IYzhFieldConfig): YzhControlType {
  return (field.searchControlType || field.controlType) as YzhControlType
}

// ====== 方法 ======

/** 初始化搜索表单 */
function initSearchForm() {
  // 清空现有数据
  Object.keys(searchForm).forEach(k => delete searchForm[k])
  // 用初始值填充
  Object.keys(props.initialValues).forEach(k => {
    searchForm[k] = props.initialValues[k]
  })
  // 为每个搜索字段设置默认空值
  searchFields.value.forEach(f => {
    if (!(f.fieldName in searchForm)) {
      searchForm[f.fieldName] = ''
    }
  })
}

/** 查询按钮 */
function handleSearch() {
  const params = getSearchParams()
  emit('search', params)
}

/** 重置按钮 */
function handleReset() {
  initSearchForm()
  emit('reset')
  // 重置后自动查询
  nextTick(() => {
    const params = getSearchParams()
    emit('search', params)
  })
}

/** 字段值变化 */
function onFieldChange(fieldName: string, value: any) {
  emit('fieldChange', fieldName, value)
}

/**
 * 获取当前搜索参数（排除空值）
 */
function getSearchParams(): Record<string, any> {
  const params: Record<string, any> = {}
  Object.keys(searchForm).forEach(key => {
    const val = searchForm[key]
    if (val !== undefined && val !== null && val !== '') {
      params[key] = val
    }
  })
  return params
}

/**
 * 获取字典选项
 * 支持从缓存读取或远程加载
 */
async function loadDictOptions(field: IYzhFieldConfig): Promise<void> {
  const dataKey = field.dataKey
  if (!dataKey) return

  // 已有缓存则跳过
  if (dictCache.value[dataKey]) return

  try {
    const response = await fetch('/api/Sys_Dictionary/GetVueDictionary', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify([dataKey])
    })

    if (response.ok) {
      const result = await response.json()
      const dataToApply = Array.isArray(result) ? result : (result?.data ?? null)
      if (dataToApply?.length) {
        // Vol 字典格式：[{ dicNo, data: [{ key, value }] }]
        const dictItem = dataToApply.find((d: any) => d.dicNo === dataKey)
        if (dictItem?.data) {
          dictCache.value[dataKey] = dictItem.data.map((d: any) => ({
            value: String(d.key ?? ''),
            label: String(d.value ?? ''),
          }))
        }
      }
    }
  } catch (err) {
    console.warn(`[YzhSearchBar] 字典加载失败 (${dataKey}):`, err)
  }
}

/** 同步版本的字典获取 */
function getDictOptions(field: IYzhFieldConfig): Array<{ value: string; label: string }> {
  const dataKey = field.dataKey
  if (!dataKey) return []
  return dictCache.value[dataKey] || []
}

// ====== 生命周期 ======
import { nextTick } from 'vue'

onMounted(async () => {
  initSearchForm()
  // 预加载所有搜索字段的字典
  const dictKeys = [...new Set(searchFields.value.map(f => f.dataKey).filter(Boolean))]
  for (const key of dictKeys) {
    const field = searchFields.value.find(f => f.dataKey === key)
    if (field) await loadDictOptions(field)
  }
  // 暴露实例
  emit('ready', exposedApi)
})

// 监听初始值变化重新初始化
watch(() => props.initialValues, () => {
  initSearchForm()
}, { deep: true })

// ====== 暴露实例方法 ======
const exposedApi = {
  get form() { return searchForm },
  getParams: getSearchParams,
  reset: handleReset,
  search: handleSearch,
  setFieldValue(fieldName: string, value: any) {
    searchForm[fieldName] = value
  },
}

defineExpose(exposedApi)
</script>

<style scoped lang="scss">
.yzh-search-bar {
  padding: 12px 16px;
  background: #fafafa;
  border: 1px solid #ebeef5;
  border-radius: 4px;
  margin-bottom: 12px;

  &--togglable {
    // 可展开/收起模式
  }

  &__form {
    .el-form-item {
      margin-bottom: 0;
    }
  }

  &__extra {
    margin-top: 8px;
    padding-top: 8px;
    border-top: 1px dashed #ebeef5;
  }
}
</style>
