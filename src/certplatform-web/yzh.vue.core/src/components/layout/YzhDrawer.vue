<script setup lang="ts">
/**
 * YzhDrawer - 通用抽屉组件
 *
 * 与 `YzhDialog` **同一套 props 语义**（`size` 代替 `width`、多一个 `direction`），
 * 目的只有一个：让「点一行 → 在右侧抽屉里配置」成为项目里的**统一写法**，
 * ⛔ 不允许各页面直接写裸 `<el-drawer>`（法条 S07）。
 *
 * 特性：
 * - v-model 双向绑定显示状态
 * - 支持自定义标题、宽度、方向（默认右侧 rtl）
 * - 内置确认/取消按钮，支持自定义底部（`#footer` 作用域插槽拿到 confirm/cancel）
 * - 支持 loading / disabled 状态（确认按钮）
 * - `destroyOnClose` 默认关闭：抽屉内容常带未保存状态，反复销毁重建会丢焦点
 *
 * 为什么默认 `appendToBody = true`：
 *   抽屉的宿主常常是 `overflow: hidden` 的三栏工作区（本项目的规则页即是），
 *   不挂到 body 时会被祖先的裁剪/层叠上下文影响（遮罩盖不住、层级被压）。
 */
import { computed, watch } from 'vue'

const props = withDefaults(
  defineProps<{
    /** v-model 显示状态 */
    modelValue: boolean
    /** 抽屉标题（`withHeader` 为 false 时不渲染） */
    title?: string
    /** 抽屉尺寸：rtl/ltr 是宽度，ttb/btt 是高度（默认 680px） */
    size?: string | number
    /** 打开方向（默认 rtl 右侧） */
    direction?: 'rtl' | 'ltr' | 'ttb' | 'btt'
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
    /** 是否显示右上角关闭按钮 */
    showClose?: boolean
    /** 是否显示标题栏 */
    withHeader?: boolean
    /** 抽屉层级 */
    zIndex?: number
    /** 自定义 class */
    customClass?: string
    /** 是否在关闭时销毁内容 */
    destroyOnClose?: boolean
    /** 是否挂到 body（默认 true） */
    appendToBody?: boolean
  }>(),
  {
    title: '',
    size: '680px',
    direction: 'rtl',
    showFooter: true,
    confirmText: '保存',
    cancelText: '取消',
    confirmType: 'primary',
    confirmDisabled: false,
    confirmLoading: false,
    closeOnClickModal: false,
    showClose: true,
    withHeader: true,
    customClass: '',
    destroyOnClose: false,
    appendToBody: true,
  },
)

const emit = defineEmits<{
  (e: 'update:modelValue', value: boolean): void
  (e: 'confirm'): void
  (e: 'cancel'): void
  (e: 'open'): void
  (e: 'close'): void
  /** 离场动画**结束后**才触发 —— 需要「关干净再清理状态」时用它（`close` 是立刻触发） */
  (e: 'closed'): void
}>()

/** 计算尺寸样式（数字 ⇒ px） */
const computedSize = computed(() => {
  if (typeof props.size === 'number') return `${props.size}px`
  return props.size
})

/** 关闭抽屉 */
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
  },
)
</script>

<template>
  <el-drawer
    :model-value="modelValue"
    :title="title"
    :size="computedSize"
    :direction="direction"
    :show-close="showClose"
    :with-header="withHeader"
    :close-on-click-modal="closeOnClickModal"
    :z-index="zIndex"
    :class="customClass"
    :destroy-on-close="destroyOnClose"
    :append-to-body="appendToBody"
    @update:model-value="(v: boolean) => emit('update:modelValue', v)"
    @closed="emit('closed')"
  >
    <!-- 默认插槽：内容区域（撑满抽屉高度，供内部 flex 布局使用） -->
    <div class="yzh-drawer__body">
      <slot />
    </div>

    <!-- 底部按钮 -->
    <template v-if="showFooter" #footer>
      <slot name="footer" :confirm="onConfirm" :cancel="onCancel">
        <div class="yzh-drawer__footer">
          <el-button type="default" @click="onCancel">{{ cancelText }}</el-button>
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
  </el-drawer>
</template>

<style scoped>
/*
 * `height: 100%` 是必需的：`.el-drawer__body` 是 `flex:1` 的滚动容器，
 * 不撑满时内部「上固定 + 下自适应」的布局（素材区 + textarea）算不出剩余高度。
 */
.yzh-drawer__body {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.yzh-drawer__footer {
  display: flex;
  justify-content: flex-end;
  gap: var(--yzh-space-2, 8px);
}
</style>
