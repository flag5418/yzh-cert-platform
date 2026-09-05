<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { getDeptPage } from '@share/api/system-dept'
import type { SysDept } from '@share/api/system-dept'

const columns: YzhTableColumn<SysDept>[] = [
  { prop: 'deptName', label: '部门名称', width: 200 },
  { prop: 'sort', label: '排序', width: 80 },
  { prop: 'enable', label: '状态', width: 80, formatter: (v) => v === 1 ? '启用' : '禁用' }
]

const searchFields: SearchField[] = [
  { prop: 'deptName', label: '部门名称', type: 'text' }
]

async function loadData(params: PageParams) {
  return getDeptPage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
