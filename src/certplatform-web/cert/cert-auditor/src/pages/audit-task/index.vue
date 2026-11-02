<script setup lang="ts">
/**
 * 认证任务管理（专家端 · V2 独立认证检查闭环 · 路由 `/audit-task`）
 *
 * ★ 与 `/tasks`（V1 专家任务系统）是两套独立系统：
 *   · V1：cert_expert_task（NC_CHECK / REPORT_GENERATE 队列编排）
 *   · V2：cert_task + cert_task_item（独立认证检查 + LLM 符合性分析）
 *
 * 本页展示 cert_task 列表，支持：启动/暂停、逐项分析。
 */
import { onMounted } from 'vue'
import { ElButton, ElInput, ElProgress, ElSelect, ElTag, ElOption } from 'element-plus'
import { useCertTaskList } from './logic'

const { loading, rows, query, loadList, start, pause, goAnalyze, statusLabel, statusTag, taskKindLabel } = useCertTaskList()

onMounted(loadList)
</script>

<template>
  <div class="audit-task-page">
    <!-- 筛选 -->
    <div class="audit-task-page__filters">
      <el-input v-model="query.keyword" placeholder="任务编号 / 关键词" clearable style="width: 220px" @keyup.enter="loadList" />
      <el-select v-model="query.status" placeholder="状态" clearable style="width: 140px">
        <el-option label="待启动" value="pending" />
        <el-option label="运行中" value="running" />
        <el-option label="已暂停" value="paused" />
        <el-option label="已完成" value="completed" />
        <el-option label="失败" value="failed" />
      </el-select>
      <el-select v-model="query.taskKind" placeholder="方向" clearable style="width: 120px">
        <el-option label="准备" value="prep" />
        <el-option label="现场" value="onsite" />
      </el-select>
      <el-button type="primary" @click="loadList">检索</el-button>
      <el-button @click="loadList">刷新</el-button>
    </div>

    <!-- 列表 -->
    <el-table v-loading="loading" :data="rows" border stripe>
      <el-table-column prop="TaskNo" label="任务编号" min-width="180" />
      <el-table-column prop="TaskKind" label="方向" width="80">
        <template #default="{ row }">
          <el-tag size="small" effect="plain" disable-transitions>{{ taskKindLabel(row.TaskKind) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="Status" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="statusTag(row.Status)" size="small" disable-transitions>{{ statusLabel(row.Status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="进度" min-width="180">
        <template #default="{ row }">
          <el-progress :percentage="row.ItemTotal > 0 ? Math.round((row.ItemDone / row.ItemTotal) * 100) : 0" :stroke-width="10" />
          <span class="audit-task-page__progress-text">{{ row.ItemDone }}/{{ row.ItemTotal }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="CreateTime" label="创建时间" width="170" />
      <el-table-column label="操作" width="260" fixed="right">
        <template #default="{ row }">
          <el-button v-if="row.Status === 'pending' || row.Status === 'paused'" type="primary" size="small" @click="start(row.Code)">启动</el-button>
          <el-button v-if="row.Status === 'running'" type="warning" size="small" @click="pause(row.Code)">暂停</el-button>
          <el-button type="success" size="small" @click="goAnalyze(row.Code)">逐项分析</el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.audit-task-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
  padding: var(--yzh-space-4, 16px);
}
.audit-task-page__filters {
  display: flex;
  gap: var(--yzh-space-3, 12px);
  margin-bottom: var(--yzh-space-4, 16px);
}
.audit-task-page__progress-text {
  margin-left: 8px;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}
</style>
