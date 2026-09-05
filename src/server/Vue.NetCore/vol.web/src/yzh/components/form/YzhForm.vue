<script setup lang="ts" generic="T extends Record<string, any> = any">
/**
 * YzhForm - 声明式表单组件（替代 vol 的 edit.vue grid 配置）
 *
 * 特点：
 * - 通过 fields 描述表单项
 * - 支持 text/number/select/switch/date/textarea 等
 * - 支持 Grid 布局（span）和 label-width 统一
 * - 支持 async validator
 * - 内置提交/重置/校验
 */
import type { FormInstance, FormRules } from 'element-plus'
import { computed, reactive, ref, watch } from 'vue'

export type YzhFieldType =
  | 'text'
  | 'number'
  | 'textarea'
  | 'password'
  | 'select'
  | 'radio'
  | 'checkbox'
  | 'switch'
  | 'date'
  | 'datetime'
  | 'dateRange'
  | 'treeSelect'
  | 'cascader'
  | 'upload'
  | 'custom'

export interface YzhFormField {
  prop: string
  label: string
  type?: YzhFieldType
  /** 必填 */
  required?: boolean
  /** Grid 占位（默认 12） */
  span?: number
  /** 提示 */
  placeholder?: string
  /** 选项（select/radio/checkbox） */
  options?: Array<{ label: string; value: any; disabled?: boolean }>
  /** 异步加载选项（select） */
  loadOptions?: () => Promise<Array<{ label: string; value: any }>>
  /** select 是否多选 */
  multiple?: boolean
  /** select 是否可筛选 */
  filterable?: boolean
  /** 是否禁用 */
  disabled?: boolean
  /** 是否隐藏 */
  hidden?: boolean
  /** 自定义插槽名 */
  slot?: string
  /** 额外 props 透传 */
  fieldProps?: Record<string, any>
  /** 自定义校验 */
  validator?: (rule: any, value: any, callback: any) => void
  /** 触发校验的时机 */
  trigger?: string | string[]
  /** 默认值 */
  defaultValue?: any
}

const props = withDefaults(
  defineProps<{
    modelValue: T
    fields: YzhFormField[]
    rules?: FormRules
    labelWidth?: string
    labelPosition?: 'left' | 'right' | 'top'
    size?: 'large' | 'default' | 'small'
    /** 显示提交/重置按钮 */
    showActions?: boolean
    /** 自定义列数（默认 2 列：每列 12 栅格，span 决定占比） */
    cols?: 1 | 2 | 3 | 4
    /** 提交按钮文字 */
    submitText?: string
    /** 重置按钮文字 */
    resetText?: string
    /** 加载态 */
    loading?: boolean
  }>(),
  {
    labelWidth: '100px',
    labelPosition: 'right',
    size: 'default',
    showActions: true,
    cols: 2,
    submitText: '保存',
    resetText: '取消',
    loading: false
  }
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: T): void
  (e: 'submit', value: T): void
  (e: 'reset'): void
  (e: 'validate', valid: boolean, fields?: any): void
}>()

const formRef = ref<FormInstance>()

// 计算列的 span
const colSpan = computed(() => 24 / props.cols)

// 自动构建 rules
const computedRules = computed<FormRules>(() => {
  if (props.rules) return props.rules
  const result: FormRules = {}
  props.fields.forEach((f) => {
    if (f.hidden) return
    const rulesArr: any[] = []
    if (f.required) {
      rulesArr.push({
        required: true,
        message: `请${f.type === 'select' || f.type === 'radio' || f.type === 'switch' ? '选择' : '输入'}${f.label}`,
        trigger: f.trigger || (f.type === 'select' || f.type === 'switch' ? 'change' : 'blur')
      })
    }
    if (f.validator) {
      rulesArr.push({ validator: f.validator, trigger: f.trigger || 'blur' })
    }
    if (rulesArr.length) {
      result[f.prop] = rulesArr
    }
  })
  return result
})

// 异步加载的选项缓存
const asyncOptions = reactive<Record<string, any[]>>({})

async function ensureOptions(field: YzhFormField) {
  if (field.options) return field.options
  if (!field.loadOptions) return []
  if (asyncOptions[field.prop]) return asyncOptions[field.prop]
  const opts = await field.loadOptions()
  asyncOptions[field.prop] = opts
  return opts
}

// 初始化时异步加载所有 loadOptions 字段
;(async () => {
  for (const f of props.fields) {
    if (f.loadOptions && !f.options) {
      await ensureOptions(f)
    }
  }
})()

// 双向绑定
const formData = reactive<any>({})

function syncFromProps() {
  Object.keys(formData).forEach((k) => delete formData[k])
  Object.assign(formData, props.modelValue || {})
  props.fields.forEach((f) => {
    if (formData[f.prop] === undefined && f.defaultValue !== undefined) {
      formData[f.prop] = f.defaultValue
    }
  })
}

syncFromProps()

// 监听外部值变化
watch(
  () => props.modelValue,
  () => syncFromProps(),
  { deep: true }
)

// 监听内部变化，向上 emit
watch(
  formData,
  (val) => {
    emit('update:modelValue', { ...val } as T)
  },
  { deep: true }
)

async function onSubmit() {
  if (!formRef.value) return
  try {
    await formRef.value.validate()
    emit('submit', { ...formData } as T)
    emit('validate', true)
  } catch (fields: any) {
    emit('validate', false, fields)
  }
}

function onReset() {
  syncFromProps()
  formRef.value?.clearValidate()
  emit('reset')
}

async function validate() {
  return formRef.value?.validate()
}

async function resetFields() {
  formRef.value?.resetFields()
}

defineExpose({ validate, resetFields, formRef })
</script>

<template>
  <el-form
    ref="formRef"
    :model="formData"
    :rules="computedRules"
    :label-width="labelWidth"
    :label-position="labelPosition"
    :size="size"
    class="yzh-form"
  >
    <el-row :gutter="20">
      <template v-for="field in fields" :key="field.prop">
        <el-col v-if="!field.hidden" :span="field.span || colSpan">
          <el-form-item :label="field.label" :prop="field.prop">
            <!-- text / textarea / password -->
            <el-input
              v-if="
                !field.type ||
                field.type === 'text' ||
                field.type === 'textarea' ||
                field.type === 'password'
              "
              v-model="formData[field.prop]"
              :type="
                field.type === 'textarea'
                  ? 'textarea'
                  : field.type === 'password'
                    ? 'password'
                    : 'text'
              "
              :placeholder="field.placeholder || `请输入${field.label}`"
              :disabled="field.disabled"
              :rows="field.type === 'textarea' ? 3 : undefined"
              v-bind="field.fieldProps"
            />

            <!-- number -->
            <el-input-number
              v-else-if="field.type === 'number'"
              v-model="formData[field.prop]"
              :placeholder="field.placeholder"
              :disabled="field.disabled"
              style="width: 100%"
              v-bind="field.fieldProps"
            />

            <!-- select -->
            <el-select
              v-else-if="field.type === 'select'"
              v-model="formData[field.prop]"
              :placeholder="field.placeholder || `请选择${field.label}`"
              :multiple="field.multiple"
              :filterable="field.filterable"
              :disabled="field.disabled"
              style="width: 100%"
              v-bind="field.fieldProps"
            >
              <el-option
                v-for="opt in field.options || asyncOptions[field.prop] || []"
                :key="opt.value"
                :label="opt.label"
                :value="opt.value"
                :disabled="opt.disabled"
              />
            </el-select>

            <!-- radio -->
            <el-radio-group
              v-else-if="field.type === 'radio'"
              v-model="formData[field.prop]"
              :disabled="field.disabled"
            >
              <el-radio v-for="opt in field.options || []" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </el-radio>
            </el-radio-group>

            <!-- checkbox -->
            <el-checkbox-group
              v-else-if="field.type === 'checkbox'"
              v-model="formData[field.prop]"
              :disabled="field.disabled"
            >
              <el-checkbox v-for="opt in field.options || []" :key="opt.value" :value="opt.value">
                {{ opt.label }}
              </el-checkbox>
            </el-checkbox-group>

            <!-- switch -->
            <el-switch
              v-else-if="field.type === 'switch'"
              v-model="formData[field.prop]"
              :disabled="field.disabled"
              v-bind="field.fieldProps"
            />

            <!-- date -->
            <el-date-picker
              v-else-if="field.type === 'date'"
              v-model="formData[field.prop]"
              type="date"
              :placeholder="field.placeholder || `请选择${field.label}`"
              :disabled="field.disabled"
              value-format="YYYY-MM-DD"
              style="width: 100%"
              v-bind="field.fieldProps"
            />

            <!-- datetime -->
            <el-date-picker
              v-else-if="field.type === 'datetime'"
              v-model="formData[field.prop]"
              type="datetime"
              :placeholder="field.placeholder || `请选择${field.label}`"
              :disabled="field.disabled"
              value-format="YYYY-MM-DD HH:mm:ss"
              style="width: 100%"
              v-bind="field.fieldProps"
            />

            <!-- dateRange -->
            <el-date-picker
              v-else-if="field.type === 'dateRange'"
              v-model="formData[field.prop]"
              type="daterange"
              :placeholder="field.placeholder || `请选择${field.label}`"
              :disabled="field.disabled"
              value-format="YYYY-MM-DD"
              range-separator="至"
              start-placeholder="开始日期"
              end-placeholder="结束日期"
              style="width: 100%"
              v-bind="field.fieldProps"
            />

            <!-- treeSelect -->
            <el-tree-select
              v-else-if="field.type === 'treeSelect'"
              v-model="formData[field.prop]"
              :data="field.options || []"
              :placeholder="field.placeholder || `请选择${field.label}`"
              :disabled="field.disabled"
              check-strictly
              clearable
              style="width: 100%"
              v-bind="field.fieldProps"
            />

            <!-- cascader -->
            <el-cascader
              v-else-if="field.type === 'cascader'"
              v-model="formData[field.prop]"
              :options="field.options || []"
              :placeholder="field.placeholder || `请选择${field.label}`"
              :disabled="field.disabled"
              style="width: 100%"
              v-bind="field.fieldProps"
            />

            <!-- custom slot -->
            <slot
              v-else-if="field.type === 'custom' && field.slot"
              :name="field.slot"
              :value="formData[field.prop]"
              :field="field"
              :data="formData"
            />

            <slot
              v-else
              :name="`field-${field.prop}`"
              :value="formData[field.prop]"
              :field="field"
              :data="formData"
            />
          </el-form-item>
        </el-col>
      </template>
    </el-row>

    <div v-if="showActions" class="yzh-form__actions">
      <slot name="actions" :submit="onSubmit" :reset="onReset">
        <el-button @click="onReset">{{ resetText }}</el-button>
        <el-button type="primary" :loading="loading" @click="onSubmit">
          {{ submitText }}
        </el-button>
      </slot>
    </div>
  </el-form>
</template>

<style scoped>
.yzh-form {
  width: 100%;
}

.yzh-form__actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 16px 0 0;
  border-top: 1px solid var(--yzh-color-border-light, #ebeef5);
  margin-top: 20px;
}

:deep(.el-form-item__label) {
  color: var(--yzh-color-text-regular, #606266);
  font-weight: 500;
}
</style>
