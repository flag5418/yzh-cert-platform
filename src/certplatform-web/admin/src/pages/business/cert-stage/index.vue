<script setup lang="ts">
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'
import { getCertStagePage } from '@share/api/cert-stage'
import type { CertStage } from '@share/types/cert'

const columns: YzhTableColumn<CertStage>[] = [
  { prop: 'stageNo', label: '阶段编号', width: 100 },
  { prop: 'stageName', label: '阶段名称', width: 150 },
  { prop: 'standardId', label: '标准ID', width: 100 }
]

const searchFields: SearchField[] = []

async function loadData(params: PageParams) {
  return getCertStagePage(params)
}
</script>

<template>
  <YzhTable :columns="columns" :data-loader="loadData" :search-fields="searchFields" />
</template>
