<template>
  <div
    class="yzh-form-field"
    :class="[
      `yzh-form-field--${config.controlType}`,
      { 'yzh-form-field--required': config.required },
      { 'yzh-form-field--hidden': isHidden }
    ]"
    :style="gridStyle"
  >
    <!-- 标签 -->
    <label v-if="config.controlType !== 'hidden'" class="yzh-form-field__label" :style="{ width: labelWidth }">
      {{ config.formTitle }}
      <span v-if="config.required" class="yzh-form-field__required">*</span>
    </label>

    <!-- 控件容器 -->
    <div class="yzh-form-field__control">
      <!-- 文本输入 -->
      <el-input
        v-if="config.controlType === 'input'"
        :model-value="modelValue"
        :placeholder="config.placeholder"
        :maxlength="config.maxLength > 0 ? config.maxLength : undefined"
        :show-word-limit="config.maxLength > 0"
        :disabled="computedDisabled || config.disabled"
        :readonly="config.readonly"
        clearable
        @update:model-value="$emit('update:modelValue', $event)"
        @change="$emit('change', $event)"
      />

      <!-- 多行文本 -->
      <el-input
        v-else-if="config.controlType === 'textarea'"
        type="textarea"
        :model-value="modelValue"
        :placeholder="config.placeholder"
        :maxlength="config.maxLength > 0 ? config.maxLength : undefined"
        :show-word-limit="config.maxLength > 0"
        :rows="config.textareaRows"
        :disabled="computedDisabled || config.disabled"
        :readonly="config.readonly"
        @update:model-value="$emit('update:modelValue', $event)"
        @change="$emit('change', $event)"
      />

      <!-- 数字 -->
      <el-input-number
        v-else-if="config.controlType === 'number'"
        :model-value="modelValue"
        :placeholder="config.placeholder"
        :disabled="computedDisabled || config.disabled"
        :controls="false"
        :precision="0"
        :min="config.minVal ?? undefined"
        :max="config.maxVal ?? undefined"
        style="width: 100%"
        @change="$emit('update:modelValue', $event); $emit('change', $event)"
      />

      <!-- 小数 -->
      <el-input-number
        v-else-if="config.controlType === 'decimal'"
        :model-value="modelValue"
        :placeholder="config.placeholder"
        :disabled="computedDisabled || config.disabled"
        :controls="false"
        :precision="config.precision ?? 2"
        :min="config.minVal ?? undefined"
        :max="config.maxVal ?? undefined"
        style="width: 100%"
        @change="$emit('update:modelValue', $event); $emit('change', $event)"
      />

      <!-- 下拉选择 (字典) -->
      <el-select
        v-else-if="config.controlType === 'select'"
        :model-value="modelValue"
        :placeholder="config.placeholder || `请选择${config.formTitle}`"
        :disabled="computedDisabled || config.disabled"
        clearable
        filterable
        style="width: 100%"
        @change="handleSelectChange"
      >
        <el-option
          v-for="opt in dictOptions"
          :key="opt.value"
          :label="opt.label"
          :value="opt.value"
        />
      </el-select>

      <!-- 日期选择 -->
      <el-date-picker
        v-else-if="config.controlType === 'date'"
        :model-value="modelValue"
        type="date"
        :placeholder="config.placeholder || '请选择日期'"
        :disabled="computedDisabled || config.disabled"
        value-format="YYYY-MM-DD"
        style="width: 100%"
        @change="$emit('update:modelValue', $event); $emit('change', $event)"
      />

      <!-- 开关 -->
      <el-switch
        v-else-if="config.controlType === 'switch'"
        :model-value="modelValue"
        :disabled="computedDisabled || config.disabled"
        inline-prompt
        active-text="是"
        inactive-text="否"
        @change="$emit('update:modelValue', $event); $emit('change', $event)"
      />

      <!-- 级联选择 -->
      <el-cascader
        v-else-if="config.controlType === 'cascader'"
        :model-value="modelValue"
        :options="cascaderData"
        :placeholder="config.placeholder"
        :disabled="computedDisabled || config.disabled"
        clearable
        filterable
        style="width: 100%"
        @change="$emit('update:modelValue', $event); $emit('change', $event)"
      />

      <!-- 树形选择 -->
      <el-tree-select
        v-else-if="config.controlType === 'treeSelect'"
        :model-value="modelValue"
        :data="treeData"
        :props="{ label: 'label', value: 'value', children: 'children' }"
        :placeholder="config.placeholder"
        :disabled="computedDisabled || config.disabled"
        clearable
        filterable
        check-strictly
        style="width: 100%"
        @change="$emit('update:modelValue', $event); $emit('change', $event)"
      />

      <!-- 自定义插槽 -->
      <slot v-else-if="config.controlType === 'slot'" :name="fieldAlias" :field="config" :value="modelValue" />

      <!-- hidden 不渲染任何内容 -->
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, ref, watch } from 'vue'
import type { IYzhFieldConfig } from '../types/YZHV3Config'

// ====== Props ======
const props = withDefaults(defineProps<{
  /** 字段配置 */
  config: IYzhFieldConfig
  /** 双向绑定值 */
  modelValue: any
  /** 当前工作流阶段（控制 GroupIndex 显隐） */
  currentPhase?: number
  /** 表单标签宽度 */
  labelWidth?: string
}>(), {
  currentPhase: 0,
  labelWidth: '120px'
})

// ====== Emits ======
const emit = defineEmits<{
  (e: 'update:modelValue', value: any): void
  (e: 'change', event: any): void
  (e: 'ready', instance: any): void
}>()

// ====== 别名 ======
const fieldAlias = computed(() => props.config.fieldAlias)

// ====== Grid 布局样式 ======
const gridStyle = computed(() => {
  const { gridRow, gridCol, gridRowSpan, gridColSpan } = props.config
  return {
    gridColumn: `${gridCol + 1} / span ${gridColSpan}`,
    gridRow: `${gridRow + 1} / span ${gridRowSpan}`
  }
})

// ====== 隐藏判断 ======
const isHidden = computed(() => {
  if (props.config.controlType === 'hidden') return true

  // GroupIndex 控制：groupIndex > 0 且不等于当前阶段则隐藏
  if (props.currentPhase && props.config.groupIndex > 0) {
    return props.config.groupIndex !== props.currentPhase
  }

  // groupIndex == 9 为系统字段，始终隐藏
  if (props.config.groupIndex === 9) return true

  return false
})

// ====== 禁用计算 ======
const computedDisabled = computed(() => {
  if (props.config.disabled) return true
  // GroupIndex 阶段控制：不在当前阶段时禁用但不隐藏
  if (props.currentPhase && props.config.groupIndex > 0 && props.config.groupIndex !== props.currentPhase) {
    return true
  }
  return false
})

// ====== 字典数据 ======
const dictOptions = ref<Array<{ value: string; label: string; color?: string }>>([])

async function loadDictData() {
  if (!props.config.dataKey) return

  try {
    // 调用 Vol 字典 API
    const dictKeys = [props.config.dataKey]
    const response = await fetch('/api/Sys_Dictionary/GetVueDictionary', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(dictKeys)
    })

    if (response.ok) {
      const result = await response.json()
      if (result?.data?.[props.config.dataKey]) {
        const dictData = result.data[props.config.dataKey]
        // Vol 字典格式: [{ value, label, color }] 或 data 数组
        dictOptions.value = Array.isArray(dictData) ? dictData : []
      }
    }
  } catch (err) {
    console.warn(`[YzhFormField] 字典加载失败 (${props.config.dataKey}):`, err)
  }
}

function handleSelectChange(val: any) {
  emit('update:modelValue', val)
  emit('change', val)
}

// ====== 级联/树形数据占位（后续实现远程加载）=====
const cascaderData = ref<any[]>([])
const treeData = ref<any[]>([])

// ====== 暴露实例方法 ======
const fieldInstance = {
  get fieldAlias() { return props.config.fieldAlias },
  get value() { return props.modelValue },
  set value(v: any) { emit('update:modelValue', v) },
  get disabled() { return computedDisabled.value },
  get visible() { return !isHidden.value },
  async validate() {
    if (props.config.required && (props.modelValue === null || props.modelValue === undefined || props.modelValue === '')) {
      return false
    }
    return true
  },
  focus() { /* TODO: 聚焦 */ },
  reset() { emit('update:modelValue', props.config.defaultValue ?? null) }
}

onMounted(() => {
  emit('ready', fieldInstance)

  // 加载字典数据
  if (props.config.dataKey) {
    loadDictData()
  }

  // 设置默认值
  if (props.config.defaultValue !== undefined && props.config.defaultValue !== '' && (props.modelValue === null || props.modelValue === undefined)) {
    emit('update:modelValue', props.config.defaultValue)
  }
})

defineExpose(fieldInstance)
</script>

<style scoped lang="scss">
.yzh-form-field {
  display: contents; /* 让 Grid 容器控制布局 */

  &--hidden {
    display: none;
  }

  &__label {
    display: flex;
    align-items: center;
    justify-content: flex-end;
    padding-right: 12px;
    font-size: 14px;
    color: #606266;
    line-height: 32px;
    white-space: nowrap;

    &::after {
      content: ':';
      margin-right: 4px;
    }
  }

  &__required {
    color: #f56c6c;
    margin-left: 4px;
  }

  &__control {
    flex: 1;
    min-width: 0;

    :deep(.el-input),
    :deep(.el-select),
    :deep(.el-date-editor),
    :deep(.el-input-number) {
      width: 100%;
    }
  }
}
</style>
