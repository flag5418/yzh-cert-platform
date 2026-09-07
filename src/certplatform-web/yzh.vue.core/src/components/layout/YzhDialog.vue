<script setup lang="ts">
/**
 * YzhDialog - 通用弹窗组件
 *
 * 特性：
 * - v-model 双向绑定显示状态
 * - 支持自定义标题、宽度、全屏
 * - 内置确认/取消按钮，支持自定义底部
 * - 支持 loading 状态（确认按钮）
 * - 打开/关闭动画
 * - 嵌套弹窗支持
 */
import { computed, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    /** v-model 显示状态 */
    modelValue: boolean
    /** 弹窗标题 */
    title?: string
    /** 弹窗宽度（默认 600px） */
    width?: string | number
    /** 是否全屏 */
    fullscreen?: boolean
    /** 是否显示底部按钮（默认 true） */
    showFooter?: boolean
    /** 确认按钮文字 */
    confirmText?: string
    /** 取消按钮文字 */
    cancelText?: string
    /** 确认按钮类型 */
    confirmType?: 'primary' | 'success' | 'warning' | 'danger' | 'info'
    /** 是否禁用确认按钮 */
    confirmDisabled?: boolean
    /** 确认按钮 loading */
    confirmLoading?: boolean
    /** 是否可点击遮罩关闭 */
    closeOnClickModal?: boolean
    /** 是否显示关闭按钮 */
    showClose?: boolean
    /** 弹窗层级 */
    zIndex?: number
    /** 弹窗自定义 class */
    customClass?: string
    /** 是否在关闭时销毁内容 */
    destroyOnClose?: boolean
    /** 顶部距离（默认 15vh） */
    top?: string
  }>(),
  {
    title: '提示',
    width: '600px',
    fullscreen: false,
    showFooter: true,
    confirmText: '确定',
    cancelText: '取消',
    confirmType: 'primary',
    confirmDisabled: false,
    confirmLoading: false,
    closeOnClickModal: false,
    showClose: true,
    customClass: '',
    destroyOnClose: false,
    top: '15vh'
  }
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm'): void
  (e: 'cancel'): void
  (e: 'open'): void
  (e: 'close'): void
}>()

/** 计算宽度样式 */
const computedWidth = computed(() => {
  if (typeof props.width === 'number') return `${props.width}px`
  return props.width
})

/** 关闭弹窗 */
function close() {
  emit('update:modelValue', false)
  emit('close')
}

/** 确认 */
function onConfirm() {
  if (props.confirmDisabled || props.confirmLoading) return
  emit('confirm')
}

/** 取消 */
function onCancel() {
  emit('cancel')
  close()
}

/** 监听打开 */
watch(
  () => props.modelValue,
  (val) => {
    if (val) emit('open')
  }
)
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="title"
    :width="fullscreen ? '100%' : computedWidth"
    :fullscreen="fullscreen"
    :show-close="showClose"
    :close-on-click-modal="closeOnClickModal"
    :z-index="zIndex"
    :class="customClass"
    :top="fullscreen ? '0' : top"
    :destroy-on-close="destroyOnClose"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
  >
    <!-- 默认插槽：内容区域 -->
    <div class="yzh-dialog__body">
      <slot />
    </div>

    <!-- 底部按钮 -->
    <template v-if="showFooter" #footer>
      <slot name="footer" :confirm="onConfirm" :cancel="onCancel">
        <div class="yzh-dialog__footer">
          <el-button @click="onCancel">{{ cancelText }}</el-button>
          <el-button
            :type="confirmType"
            :disabled="confirmDisabled"
            :loading="confirmLoading"
            @click="onConfirm"
          >
            {{ confirmText }}
          </el-button>
        </div>
      </slot>
    </template>
  </el-dialog>
</template>

<style scoped>
.yzh-dialog__body {
  max-height: 60vh;
  overflow-y: auto;
  padding: 0;
}

.yzh-dialog__footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

/* 全屏弹窗样式 */
:deep(.el-dialog--fullscreen) {
  display: flex;
  flex-direction: column;
}

:deep(.el-dialog--fullscreen .el-dialog__body) {
  flex: 1;
  overflow-y: auto;
}
</style>
