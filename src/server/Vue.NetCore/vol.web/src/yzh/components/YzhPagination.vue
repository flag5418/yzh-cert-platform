<template>
  <div class="yzh-pagination" :class="{ 'yzh-pagination--hidden': hideOnSinglePage && total <= pageSize }">
    <el-pagination
      v-model:current-page="currentPageModel"
      v-model:page-size="pageSizeModel"
      :total="total"
      :page-sizes="pageSizes"
      :layout="layout"
      :background="background"
      :size="size"
      :disabled="disabled"
      @size-change="onSizeChange"
      @current-change="onCurrentChange"
    />

    <!-- 自定义信息区域（如：显示当前范围） -->
    <div v-if="$slots.info || showRangeInfo" class="yzh-pagination__info">
      <slot name="info" :from="rangeFrom" :to="rangeTo" :total="total">
        <span v-if="total > 0" class="yzh-pagination__range">
          第 {{ rangeFrom }}-{{ rangeTo }} 条 / 共 {{ total }} 条
        </span>
      </slot>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

// ====== Props ======
const props = withDefaults(defineProps<{
  /** 当前页码 (v-model) */
  currentPage?: number
  /** 每页条数 (v-model) */
  pageSize?: number
  /** 总条数 */
  total: number
  /** 每页条数选项 */
  pageSizes?: number[]
  /** 布局配置 */
  layout?: string
  /** 是否带背景色 */
  background?: boolean
  /** 尺寸 */
  size?: 'large' | 'default' | 'small'
  /** 是否禁用 */
  disabled?: boolean
  /** 仅一页时是否隐藏 */
  hideOnSinglePage?: boolean
  /** 是否显示范围信息 */
  showRangeInfo?: boolean
}>(), {
  currentPage: 1,
  pageSize: 20,
  pageSizes: () => [10, 20, 50, 100],
  layout: 'total, sizes, prev, pager, next, jumper',
  background: true,
  size: 'default',
  disabled: false,
  hideOnSinglePage: false,
  showRangeInfo: false,
})

// ====== Emits ======
const emit = defineEmits<{
  (e: 'update:currentPage', page: number): void
  (e: 'update:pageSize', size: number): void
  (e: 'sizeChange', size: number): void
  (e: 'currentChange', page: number): void
}>()

// ====== 双向绑定 ======

const currentPageModel = computed({
  get: () => props.currentPage,
  set: (val) => emit('update:currentPage', val),
})

const pageSizeModel = computed({
  get: () => props.pageSize,
  set: (val) => emit('update:pageSize', val),
})

// ====== 计算属性 ======

/** 范围起始 */
const rangeFrom = computed(() => {
  if (props.total === 0) return 0
  return (props.currentPage - 1) * props.pageSize + 1
})

/** 范围结束 */
const rangeTo = computed(() => {
  const end = props.currentPage * props.pageSize
  return end > props.total ? props.total : end
})

// ====== 方法 ======

function onSizeChange(size: number) {
  emit('update:pageSize', size)
  emit('sizeChange', size)
  // 切换每页条数时回到第一页
  emit('update:currentPage', 1)
  emit('currentChange', 1)
}

function onCurrentChange(page: number) {
  emit('update:currentPage', page)
  emit('currentChange', page)
}
</script>

<style scoped lang="scss">
.yzh-pagination {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 0 0;
  flex-wrap: wrap;
  gap: 8px;

  &--hidden {
    display: none;
  }

  &__info {
    color: #909399;
    font-size: 13px;
    flex-shrink: 0;
  }

  &__range {
    white-space: nowrap;
  }
}
</style>
