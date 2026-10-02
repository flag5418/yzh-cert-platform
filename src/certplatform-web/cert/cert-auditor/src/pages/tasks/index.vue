<script setup lang="ts">
/**
 * 任务中心（专家端 · 任务系统第 1 页 · 路由 `/tasks`）
 *
 * 定位：**任务列表 + 入口**。这一页只回答「我有哪些任务、各自跑到哪一步了」，
 * 以及「开新任务 / 看详情 / 提交执行 / 重试失败 / 看结果」五个动作。
 *
 * ★ 零手写 CRUD：列 / 搜索 / 按钮由后端 EntityConfig 驱动
 *   （`CertPlatform.Auditor/Assets/EntityConfigs/Expert/CertExpertTask.json`），
 *   按钮动作在 `./logic.ts` 注册处理器，本文件只负责版式与插槽。
 *
 * ★ 用户裁决（2026-09-30）在本页的落点：
 *   · 按**创建时间倒序**（最新在最顶上）→ `:default-sort`
 *   · **任务查询不是重点** → 只留 2 个筛选（任务名称 / 任务编号）
 *   · **分页处理** → `:page-size="10"`
 *   · **不导出任务清单** → JSON 里 `Toolbar.Export = false`
 *
 * ★ 状态列说明（D27 三层分离）：
 *   只展示**执行状态**（机器管，决定业务锁）。**复核状态不进本页** ——
 *   它是结论级的，在「NC 检查结果 / 报告结论」菜单里看。
 */
import { ElProgress, ElTag } from 'element-plus'
import { YzhTable, useSingleTable } from '@yzh-core'
import { EXEC_STATUS_TAG } from '@share/constants/expert-task'
import { ExpertTaskLogic } from './logic'

const { logic, tableRef } = useSingleTable(ExpertTaskLogic)
</script>

<template>
  <div class="task-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      :search-max-fields="2"
      :page-size="10"
      :default-sort="{ prop: 'CreateTime', order: 'desc' }"
      select-mode="multiple"
      empty-text="还没有任务。点右上角「创建新任务」开始。"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    >
      <!-- 执行状态：中文标签（标签文案来自服务端视图字段，前端不硬编码枚举） -->
      <template #column-ExecStatusLabel="{ row }">
        <el-tag :type="EXEC_STATUS_TAG[row.ExecStatus] ?? 'info'" size="small" disable-transitions>
          {{ row.ExecStatusLabel }}
        </el-tag>
        <el-tag
          v-if="row.IsBlocking"
          class="task-page__lock"
          type="warning"
          size="small"
          effect="plain"
          disable-transitions
        >
          占锁
        </el-tag>
      </template>

      <!-- 进度：队列执行进度（非认可进度） -->
      <template #column-Progress="{ row }">
        <el-progress
          :percentage="Math.min(100, Math.max(0, Number(row.Progress ?? 0)))"
          :stroke-width="12"
          :status="row.ExecStatus === 'failed' ? 'exception' : undefined"
        />
      </template>

      <template #toolbar-right>
        <div class="task-page__hint">
          <span>共 {{ logic.pagination.total }} 个任务</span>
        </div>
      </template>
    </YzhTable>
  </div>
</template>

<style scoped>
.task-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.task-page__lock {
  margin-left: 6px;
}

.task-page__hint {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}
</style>
