<template>
  <el-dialog
    v-model="visible"
    :title="dialogTitle"
    :width="width"
    :close-on-click-modal="false"
    :destroy-on-close="destroyOnClose"
    :close-on-press-escape="!loading"
    :show-close="!loading"
    @closed="handleClosed"
  >
    <slot name="default">
      <slot name="prepend" />
      <YzhForm
        ref="formRef"
        v-model="innerModel"
        :fields="fields"
        :loading="loading"
        :cols="cols"
        :label-width="labelWidth"
        :show-actions="false"
        @submit="handleSubmit"
        @reset="handleCancel"
        @validate="handleValidate"
      >
        <!-- 字段级 slot 透传：#Icon / #field-xxx 等穿透弹窗层落到内层 YzhForm -->
        <template v-for="name in fieldSlotNames" :key="name" #[name]="slotProps">
          <slot :name="name" v-bind="slotProps ?? {}" />
        </template>
      </YzhForm>
    </slot>

    <template #footer>
      <slot name="footer">
        <el-button :disabled="loading" @click="handleCancel">
          {{ mode === 'detail' ? '关闭' : '取消' }}
        </el-button>
        <!-- F4：detail 模式只读 → 主按钮禁用，避免「详情」误触发保存 -->
        <el-button
          type="primary"
          :loading="loading"
          :disabled="mode === 'detail'"
          @click="handleSubmit"
        >
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
import { computed, ref, useSlots } from 'vue'
import { ElMessage } from 'element-plus'
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
    /**
     * 表单校验失败时是否额外弹一条 warning（D3：**默认关**）
     * 校验不通过时 el-form 已就地标红，重复弹窗属于噪音；需要显式提示的页面再开。
     */
    showValidateMessage?: boolean
    /** 校验失败提示文案（showValidateMessage=true 时生效） */
    validateMessage?: string
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
    destroyOnClose: true,
    showValidateMessage: false,
    validateMessage: '表单校验未通过，请检查标红字段'
  }
)

const emit = defineEmits<{
  (e: 'update:visible', value: boolean): void
  (e: 'update:modelValue', value: Record<string, any>): void
  (e: 'submit'): void
  (e: 'cancel'): void
  (e: 'closed'): void
  (e: 'validate', valid: boolean, fields?: any): void
}>()

// 弹窗自身保留的 slot（不转发给 YzhForm）
const RESERVED_SLOTS = new Set(['default', 'prepend', 'footer'])
const slots = useSlots()
/** 字段级 slot 名（#Icon、#field-xxx 等）→ 转发到内层 YzhForm */
const fieldSlotNames = computed(() =>
  Object.keys(slots).filter((name) => !RESERVED_SLOTS.has(name) && typeof slots[name] === 'function')
)

/** 内层 YzhForm 实例（F5：关窗后 resetFields 清残留校验态） */
const formRef = ref<any>(null)

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

/**
 * F-12：页脚「保存」必须先跑内层 YzhForm 校验。
 * 否则校验失败也会发请求（实测：空表单点保存 → 照样 POST /add，红字要等 blur 才出现）。
 * 校验失败 → 只发 validate 事件（D3 可选提示），**不 emit submit、不发任何请求**。
 * `#default` 被覆盖（无内层 YzhForm）时 formRef 为空 → 跳过校验，保持原行为。
 */
async function handleSubmit() {
  // 提交进行中禁止重复触发（避免并发两次 add/update）
  if (props.loading) return
  const form = formRef.value
  if (form?.validate) {
    try {
      await form.validate()
    } catch (fields: any) {
      handleValidate(false, fields)
      return
    }
    emit('validate', true)
  }
  emit('submit')
}

function handleCancel() {
  // 提交进行中不响应取消（含 YzhForm 的 @reset 入口）
  if (props.loading) return
  emit('cancel')
  emit('update:visible', false)
}

/** 内层 YzhForm 校验结果透传（YzhFormDialog 未做任何拦截，只做 D3 可选提示） */
function handleValidate(valid: boolean, fields?: any) {
  if (!valid && props.showValidateMessage) {
    ElMessage.warning(props.validateMessage)
  }
  emit('validate', valid, fields)
}

/**
 * F5：关窗后清残留校验态（红字 / validate 状态）。
 * 字段**值**的重置由宿主 `initFormData`（ST-3）+ YzhForm 的 `props.modelValue` watcher 负责，
 * 这里只负责 el-form 自身的校验状态，避免下次打开时旧红字残留。
 * `destroyOnClose=true`（默认）时内层已销毁 → formRef 为 null，安全 no-op。
 */
function handleClosed() {
  formRef.value?.resetFields?.()
  emit('closed')
}
</script>
