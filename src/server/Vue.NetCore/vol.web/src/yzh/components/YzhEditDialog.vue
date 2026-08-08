<template>
  <el-dialog
    v-model="visible"
    :title="dialogTitle"
    :width="dialogWidth + 'px'"
    :close-on-click-modal="false"
    :close-on-press-escape="false"
    destroy-on-close
    @open="onOpen"
    @close="onClose"
  >
    <!-- 弹窗内容区（支持滚动） -->
    <div class="yzh-edit-dialog__body" :style="{ maxHeight: dialogMaxHeight, overflowY: 'auto' }">
      <!-- 使用 YzhFormGrid 渲染表单字段 -->
      <YzhFormGrid
        ref="formGridRef"
        :fields="formFields"
        :form-data="formData"
        :current-phase="currentPhase"
        :columns="gridColumns"
        :label-width="labelWidth"
        @update:form-data="onFormDataChange"
        @field-change="onFieldChange"
        @field-ready="onFieldReady"
      >
        <!-- 额外插槽：业务页面可插入自定义组件 -->
        <template #extra="{ formData: currentData }">
          <slot name="extra" :form-data="currentData" />
        </template>
      </YzhFormGrid>
    </div>

    <!-- 底部按钮 -->
    <template #footer>
      <div class="yzh-edit-dialog__footer">
        <!-- 左侧自定义按钮区域 -->
        <div class="yzh-edit-dialog__footer-left">
          <slot name="footerLeft" :form-data="formData" />
        </div>

        <!-- 右侧操作按钮 -->
        <div class="yzh-edit-dialog__footer-right">
          <slot name="footerRight" :form-data="formData">
            <el-button @click="handleCancel">取消</el-button>
            <el-button type="primary" :loading="saving" @click="handleSave">保存</el-button>
          </slot>
        </div>
      </div>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch, nextTick } from 'vue'
import YzhFormGrid from './YzhFormGrid.vue'
import type { IYzhFieldConfig } from '../types/YZHV3Config'

// ====== Props ======
const props = withDefaults(defineProps<{
  /** 弹窗是否可见 (v-model) */
  modelValue: boolean
  /** 弹窗标题 */
  title?: string
  /** 模式：add / edit */
  mode?: 'add' | 'edit'
  /** 字段配置列表（从 yzh_field_config 筛选） */
  fields: IYzhFieldConfig[]
  /** 表单数据对象 */
  formData: Record<string, any>
  /** 当前工作流阶段 */
  currentPhase?: number
  /** 弹窗宽度 (px) */
  dialogWidth?: number
  /** 弹窗最大高度 */
  dialogMaxHeight?: string
  /** Grid 列数 */
  gridColumns?: number
  /** 标签宽度 (px) */
  labelWidth?: number
  /** 是否正在保存 */
  saving?: boolean
}>(), {
  title: '编辑',
  mode: 'add',
  currentPhase: 0,
  dialogWidth: 800,
  dialogMaxHeight: '60vh',
  gridColumns: 2,
  labelWidth: 120,
  saving: false,
})

// ====== Emits ======
const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'save', formData: Record<string, any>): void
  (e: 'cancel'): void
  (e: 'open'): void
  (e: 'close'): void
  (e: 'formDataChange', data: Record<string, any>): void
  (e: 'fieldChange', fieldName: string, value: any): void
  (e: 'fieldReady', alias: string, instance: any): void
}>()

// ====== Refs ======
const formGridRef = ref()

// ====== 计算属性 ======

/** 双向绑定 visible */
const visible = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val),
})

/** 动态弹窗标题 */
const dialogTitle = computed(() => {
  const prefix = props.mode === 'add' ? '新增' : '编辑'
  return `${prefix}${props.title}`
})

/** 过滤表单字段（排除 hidden，按 groupIndex 过滤） */
const formFields = computed(() => {
  return props.fields.filter(f => {
    if (f.controlType === 'hidden') return true // hidden 也保留但不显示
    if (f.groupIndex === 9) return false // 系统字段不展示
    if (props.currentPhase && f.groupIndex > 0 && f.groupIndex !== props.currentPhase) {
      return false // 不在当前阶段的字段隐藏
    }
    return true
  })
})

// ====== 方法 ======

/** 弹窗打开回调 */
function onOpen() {
  emit('open')
}

/** 弹窗关闭回调 */
function onClose() {
  emit('close')
}

/** 表单数据变化 */
function onFormDataChange(data: Record<string, any>) {
  emit('formDataChange', data)
}

/** 单个字段值变化 */
function onFieldChange(fieldName: string, value: any) {
  emit('fieldChange', fieldName, value)
}

/** 字段实例就绪 */
function onFieldReady(alias: string, instance: any) {
  emit('fieldReady', alias, instance)
}

/** 取消按钮 */
function handleCancel() {
  visible.value = false
  emit('cancel')
}

/** 保存按钮 */
async function handleSave() {
  // 触发表单校验
  const valid = await validate()
  if (!valid) return

  emit('save', { ...props.formData })
}

/**
 * 校验所有必填字段
 * @returns 是否校验通过
 */
async function validate(): Promise<boolean> {
  if (!formGridRef.value) return true

  const fields = formFields.value.filter(f => f.required)
  const errors: string[] = []

  for (const field of fields) {
    const value = props.formData[field.fieldName]
    if (value === null || value === undefined || value === '') {
      errors.push(`${field.formTitle}不能为空`)
    }
  }

  if (errors.length > 0) {
    console.warn('[YzhEditDialog] 校验失败:', errors)
    return false
  }

  return true
}

/**
 * 获取指定字段的实例
 */
function getFieldInstance(alias: string): any {
  return formGridRef.value?.getField(alias)
}

/**
 * 重置表单为默认值
 */
function resetForm() {
  formFields.value.forEach(field => {
    if (field.defaultValue !== undefined && field.defaultValue !== '') {
      props.formData[field.fieldName] = field.defaultValue
    }
  })
}

// ====== 暴露实例方法 ======
defineExpose({
  validate,
  getFieldInstance,
  resetForm,
  get formGrid() { return formGridRef.value },
})
</script>

<style lang="scss">
/* 注意：不使用 scoped！el-dialog 渲染在 body 层级 */

.yzh-edit-dialog {
  &__body {
    padding-right: 8px; /* 为滚动条留空间 */
  }

  &__footer {
    display: flex;
    justify-content: space-between;
    align-items: center;

    &-left {
      display: flex;
      gap: 8px;
    }

    &-right {
      display: flex;
      gap: 8px;
    }
  }
}
</style>
