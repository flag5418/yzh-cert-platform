<template>
  <el-dialog
    v-model="visible"
    :title="dialogTitle"
    :width="width"
    :close-on-click-modal="false"
    :destroy-on-close="destroyOnClose"
    @closed="emit('closed')"
  >
    <slot name="default">
      <slot name="prepend" />
      <YzhForm
        v-model="innerModel"
        :fields="fields"
        :loading="loading"
        :cols="cols"
        :label-width="labelWidth"
        :show-actions="false"
        @submit="handleSubmit"
        @reset="handleCancel"
      >
        <!-- 字段级 slot 透传：#Icon / #field-xxx 等穿透弹窗层落到内层 YzhForm -->
        <template v-for="name in fieldSlotNames" :key="name" #[name]="slotProps">
          <slot :name="name" v-bind="slotProps ?? {}" />
        </template>
      </YzhForm>
    </slot>

    <template #footer>
      <slot name="footer">
        <el-button @click="handleCancel">取消</el-button>
        <el-button type="primary" :loading="loading" @click="handleSubmit">
          {{ submitText }}
        </el-button>
      </slot>
    </template>
  </el-dialog>
</template>

<script setup lang="ts">
/**
 * YzhFormDialog - 表单弹窗（C-F1..F3）
 *
 * 组合 YzhDialog（el-dialog）+ YzhForm：
 * - v-model:visible 控制显隐
 * - mode: add | edit | detail（标题自动派生，可用 title 覆盖）
 * - 页脚提交/取消 + loading
 * - slot：#default（替换表单）、#footer（替换页脚）
 *
 * 用法：
 *   <YzhFormDialog
 *     v-model:visible="logic.dialogVisible.value"
 *     v-model="logic.formData"
 *     :mode="logic.dialogMode.value"
 *     :fields="logic.formFields"
 *     entity-name="用户"
 *     :loading="logic.submitting.value"
 *     @submit="logic.submitForm()"
 *   />
 */
import { computed, useSlots } from 'vue'
import YzhForm from './YzhForm.vue'
import type { YzhFormField } from './YzhForm.vue'

const props = withDefaults(
  defineProps<{
    /** 弹窗可见性 */
    visible: boolean
    /** 弹窗模式 */
    mode?: 'add' | 'edit' | 'detail'
    /** 实体名称（标题自动派生：新增{entityName} / 编辑{entityName} / {entityName}详情） */
    entityName?: string
    /** 标题（覆盖自动派生） */
    title?: string
    /** 宽度 */
    width?: string | number
    /** 表单字段 */
    fields?: YzhFormField[]
    /** 表单数据（v-model） */
    modelValue?: Record<string, any>
    /** 提交 loading */
    loading?: boolean
    /** 布局列数 */
    cols?: 1 | 2 | 3 | 4
    /** 标签宽度 */
    labelWidth?: string
    /** 提交按钮文字 */
    submitText?: string
    /** 是否销毁内容 */
    destroyOnClose?: boolean
  }>(),
  {
    visible: false,
    mode: 'add',
    entityName: '',
    title: undefined,
    width: '640px',
    fields: () => [],
    modelValue: () => ({}),
    loading: false,
    cols: 2,
    labelWidth: '100px',
    submitText: undefined,
    destroyOnClose: true
  }
)

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'update:modelValue', value: Record<string, any>): void
  (e: 'submit'): void
  (e: 'cancel'): void
  (e: 'closed'): void
}>()

// 弹窗自身保留的 slot（不转发给 YzhForm）
const RESERVED_SLOTS = new Set(['default', 'prepend', 'footer'])
const slots = useSlots()
/** 字段级 slot 名（#Icon、#field-xxx 等）→ 转发到内层 YzhForm */
const fieldSlotNames = computed(() =>
  Object.keys(slots).filter((name) => !RESERVED_SLOTS.has(name) && typeof slots[name] === 'function')
)

const visible = computed({
  get: () => props.visible,
  set: (v: boolean) => emit('update:visible', v)
})

const innerModel = computed({
  get: () => props.modelValue,
  set: (v: Record<string, any>) => emit('update:modelValue', v)
})

/** 标题：title > mode + entityName 自动派生 */
const dialogTitle = computed(() => {
  if (props.title) return props.title
  const name = props.entityName || ''
  if (props.mode === 'add') return name ? `新增${name}` : '新增'
  if (props.mode === 'detail') return name ? `${name}详情` : '详情'
  return name ? `编辑${name}` : '编辑'
})

const submitText = computed(() => props.submitText ?? (props.mode === 'detail' ? '关闭' : '保存'))

function handleSubmit() {
  emit('submit')
}

function handleCancel() {
  emit('cancel')
  emit('update:visible', false)
}
</script>
