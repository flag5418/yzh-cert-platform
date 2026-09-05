<script setup lang="ts">
/**
 * YzhPagination - 紧凑分页
 * 直接基于 Element Plus el-pagination 包装，业务侧通过 v-model:page/v-model:page-size 双向绑定
 */
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{
    page: number
    pageSize: number
    total: number
    pageSizes?: number[]
    layout?: string
    background?: boolean
    size?: 'large' | 'default' | 'small'
  }>(),
  {
    pageSizes: () => [10, 20, 50, 100],
    layout: 'total, sizes, prev, pager, next, jumper',
    background: true,
    size: 'default'
  }
)

const emit = defineEmits<{
  (e: 'update:page', page: number): void
  (e: 'update:pageSize', pageSize: number): void
}>()

const currentPageModel = computed({
  get: () => props.page,
  set: (v) => emit('update:page', v)
})

const pageSizeModel = computed({
  get: () => props.pageSize,
  set: (v) => emit('update:pageSize', v)
})
</script>

<template>
  <el-pagination
    v-model:current-page="currentPageModel"
    v-model:page-size="pageSizeModel"
    :total="total"
    :page-sizes="pageSizes"
    :layout="layout"
    :background="background"
    :size="size"
  />
</template>

<style scoped>
:deep(.el-pagination) {
  --el-pagination-bg-color: transparent;
  --el-pagination-button-bg-color: transparent;
  --el-pagination-button-color: #606266;
  --el-pagination-button-disabled-bg-color: transparent;
  --el-pagination-hover-color: var(--yzh-color-primary, #409eff);
}

:deep(.el-pager li) {
  background: transparent !important;
  font-weight: 500;
}

:deep(.el-pager li.is-active) {
  background: var(--yzh-color-primary, #409eff) !important;
  color: #fff !important;
}
</style>
