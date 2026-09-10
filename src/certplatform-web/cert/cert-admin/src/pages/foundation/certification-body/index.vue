<script setup lang="ts">
import { YzhTable } from '@yzh-core'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/types'
import { getCertificationBodyPage } from '@share/api/cert/certification-body'
import type { CertificationBody } from '@share/types/cert'

const columns: YzhTableColumn<CertificationBody>[] = [
  { prop: 'orgCode', label: '机构编码', width: 150 },
  { prop: 'orgName', label: '机构名称', minWidth: 200 },
  { prop: 'orgShortName', label: '简称', width: 150 },
  { prop: 'orgStatus', label: '状态', width: 100, formatter: (v) => v === 1 ? '启用' : '禁用' }
]

const searchFields: SearchField[] = [
  { prop: 'orgName', label: '机构名称', type: 'text' },
  { prop: 'orgCode', label: '机构编码', type: 'text' }
]

async function loadData(params: PageParams) {
  return getCertificationBodyPage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
