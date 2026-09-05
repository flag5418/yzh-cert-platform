<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { getDictList } from '@share/api/system-dict'
import type { SysDictList } from '@share/api/system-dict'
import { ref } from 'vue'

const props = defineProps<{ dictId: number }>()
const dictList: SysDictList[] = []

const columns: YzhTableColumn<SysDictList>[] = [
  { prop: 'listText', label: '显示文本', width: 200 },
  { prop: 'listValue', label: '值', width: 150 },
  { prop: 'listSort', label: '排序', width: 80 },
  { prop: 'enable', label: '状态', width: 80, formatter: (v) => v === 1 ? '启用' : '禁用' }
]

const searchFields: SearchField[] = []

async function loadData(params: PageParams) {
  const list = await getDictList(props.dictId)
  return { rows: list, total: list.length }
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
