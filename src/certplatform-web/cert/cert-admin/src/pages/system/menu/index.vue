<script setup lang="ts">
import { YzhTable } from '@yzh-core'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/types'
import { getMenuPage } from '@/api/system/menu'
import type { SysMenu } from '@/api/system/menu'

const columns: YzhTableColumn<SysMenu>[] = [
  { prop: 'menuName', label: '菜单名称', width: 200 },
  { prop: 'menuUrl', label: '路由地址', width: 200 },
  { prop: 'menuIcon', label: '图标', width: 100 },
  { prop: 'sort', label: '排序', width: 80 },
  { prop: 'enable', label: '状态', width: 80, formatter: (v) => v === 1 ? '启用' : '禁用' }
]

const searchFields: SearchField[] = [
  { prop: 'menuName', label: '菜单名称', type: 'text' }
]

async function loadData(params: PageParams) {
  return getMenuPage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
