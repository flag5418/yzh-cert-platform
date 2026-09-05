<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { getRolePage } from '@share/api/system-role'
import type { SysRole } from '@share/api/system-role'

const columns: YzhTableColumn<SysRole>[] = [
  { prop: 'roleName', label: '角色名称', width: 150 },
  { prop: 'roleMark', label: '角色描述', minWidth: 200 },
  { prop: 'enable', label: '状态', width: 80, formatter: (v) => v === 1 ? '启用' : '禁用' },
  { prop: 'createDate', label: '创建时间', width: 180 }
]

const searchFields: SearchField[] = [
  { prop: 'roleName', label: '角色名称', type: 'text' }
]

async function loadData(params: PageParams) {
  return getRolePage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
