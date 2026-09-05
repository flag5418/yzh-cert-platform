<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { getDictPage } from '@share/api/system-dict'
import type { SysDict } from '@share/api/system-dict'

const columns: YzhTableColumn<SysDict>[] = [
  { prop: 'dictName', label: '字典名称', width: 200 },
  { prop: 'dictCode', label: '字典编码', width: 200 },
  { prop: 'enable', label: '状态', width: 80, formatter: (v) => v === 1 ? '启用' : '禁用' }
]

const searchFields: SearchField[] = [
  { prop: 'dictName', label: '字典名称', type: 'text' },
  { prop: 'dictCode', label: '字典编码', type: 'text' }
]

async function loadData(params: PageParams) {
  return getDictPage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
