<script setup lang="ts">
/**
 * 任务中心 - 跨企业任务视角（V4 新架构页面）
 *
 * 由原 `pages/workspace/index.vue` 归位而来 —— 该页内容本就是任务列表，
 * 与菜单「任务中心」（`/tasks`）对应，而非独立的「工作台」。
 */
import { YzhTable } from '@yzh-core/components/table'
import type { PageParams, SearchField, YzhTableColumn } from '@yzh-core/components/table/types'

interface AuditTask {
  id: number
  taskNo: string
  entName: string
  standardName: string
  stageName: string
  auditorName: string
  taskStatus: number
  auditDate: string
}

const columns: YzhTableColumn<AuditTask>[] = [
  { prop: 'taskNo', label: '任务编号', width: 150, sortable: true },
  { prop: 'entName', label: '企业名称', minWidth: 200 },
  { prop: 'standardName', label: '标准名称', width: 150 },
  { prop: 'stageName', label: '阶段', width: 100 },
  { prop: 'auditorName', label: '审核员', width: 120 },
  { prop: 'auditDate', label: '审核日期', width: 150 },
  { prop: 'taskStatus', label: '状态', width: 100, formatter: (v) => v === 1 ? '进行中' : '已完成' }
]

const searchFields: SearchField[] = [
  { prop: 'taskNo', label: '任务编号', type: 'text' },
  { prop: 'entName', label: '企业名称', type: 'text' }
]

async function loadTasks(_params: PageParams) {
  // 模拟数据
  return {
    rows: [],
    total: 0
  }
}
</script>

<template>
  <YzhTable
    :columns="columns"
    :data-loader="loadTasks"
    :search-fields="searchFields"
  />
</template>
