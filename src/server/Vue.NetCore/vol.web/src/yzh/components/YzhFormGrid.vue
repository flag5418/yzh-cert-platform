<template>
  <div
    class="yzh-form-grid"
    :style="gridContainerStyle"
  >
    <YzhFormField
      v-for="field in visibleFields"
      :key="field.fieldAlias"
      :ref="(el: any) => setFieldRef(field.fieldAlias, el)"
      :config="field"
      :model-value="formData[field.fieldName]"
      :current-phase="currentPhase"
      :label-width="labelWidth + 'px'"
      @update:model-value="onFieldValueChange(field.fieldName, $event)"
      @change="onFieldChange(field, $event)"
      @ready="onFieldReady(field.fieldAlias, $event)"
    />

    <!-- 自定义插槽：允许业务页面插入额外字段 -->
    <slot name="extra" :form-data="formData" />
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import YzhFormField from './YzhFormField.vue'
import type { IYzhFieldConfig } from '../types/YZHV3Config'

// ====== Props ======
const props = withDefaults(defineProps<{
  /** 字段配置列表 */
  fields: IYzhFieldConfig[]
  /** 表单数据对象 */
  formData: Record<string, any>
  /** 当前工作流阶段 */
  currentPhase?: number
  /** 列数（Grid 模板列数） */
  columns?: number
  /** 标签宽度 */
  labelWidth?: number
}>(), {
  currentPhase: 0,
  columns: 2,
  labelWidth: 120
})

// ====== Emits ======
const emit = defineEmits<{
  (e: 'update:formData', data: Record<string, any>): void
  (e: 'field-change', fieldName: string, value: any): void
  (e: 'field-ready', alias: string, instance: any): void
}>()

// ====== 过滤可见字段（非 hidden 且在当前阶段）=====
const visibleFields = computed(() => {
  return props.fields.filter(f => {
    if (f.controlType === 'hidden') return false
    if (f.groupIndex === 9) return false // 系统字段
    if (props.currentPhase && f.groupIndex > 0 && f.groupIndex !== props.currentPhase) return false
    return true
  })
})

// ====== Grid 容器样式 ======
const gridContainerStyle = computed(() => ({
  display: 'grid',
  gridTemplateColumns: `repeat(${props.columns}, minmax(200px, 1fr))`,
  gap: '16px',
  alignItems: 'start'
}))

// ====== 字段实例注册表 ======
const fieldRefs = reactive(new Map<string, InstanceType<typeof YzhFormField>>())

function setFieldRef(alias: string, el: any) {
  if (el) {
    fieldRefs.set(alias, el)
  }
}

function onFieldReady(alias: string, instance: any) {
  emit('field-ready', alias, instance)
}

function onFieldValueChange(fieldName: string, value: any) {
  const newFormData = { ...props.formData, [fieldName]: value }
  emit('update:formData', newFormData)
}

function onFieldChange(field: IYzhFieldConfig, event: any) {
  emit('field-change', field.fieldName, event)
}

/**
 * 获取字段实例（精确控制）
 */
function getField(alias: string): any {
  return fieldRefs.get(alias)
}

/**
 * 监听字段值变化
 */
function watchField(alias: string, callback: (value: any) => void) {
  const field = props.fields.find(f => f.fieldAlias === alias)
  if (!field) return

  const unwatch = () => {}
  return { unwatch }
}

defineExpose({
  getField,
  watchField,
  visibleFields,
  fieldRefs
})
</script>

<style scoped lang="scss">
.yzh-form-grid {
  width: 100%;
}
</style>
