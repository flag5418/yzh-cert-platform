<script setup lang="ts">
/**
 * 任务中心（专家端 · 任务系统第 1 页 · 路由 `/tasks`）
 *
 * 定位：**任务列表 + 入口**。这一页只回答「我有哪些任务、各自跑到哪一步了」，
 * 以及「开新任务 / 看详情 / 启动任务 / 重试失败」四个动作。
 * ★ 结论级结果（NC / 报告）**不在本页** —— 走左侧对应菜单，⛔ 行内不再放「看结果」。
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
 *
 * ★ 用户裁决（2026-10-07）：
 *   · 行按钮「提交执行」→「启动任务」：点一次就**校验 + 补录 + 提交 + 启动**，
 *     ⛔ 不再让审核员自己跑去详情页找「启动」。
 *   · 缺关键信息 ⇒ 弹「关键信息补录」抽屉（2 Tab：关键字段 / 关键表格信息），
 *     补齐后自动继续；也可「运行跳过」，未执行项在详情「未执行清单」留痕。
 *   · **本页必须刷新**：`AuditorLayout` 的 `<keep-alive>` 会复用组件、`onMounted`
 *     不再触发（= 用户报的「创建完任务返回列表，列表没刷新」）⇒ 用 `onActivated` 兜底。
 */
import { onActivated } from 'vue'
import { ElProgress, ElTag } from 'element-plus'
import { YzhTable, useSingleTable } from '@yzh-core'
import { EXEC_STATUS_TAG } from '@share/constants/expert-task'
import { ExpertTaskLogic } from './logic'
import KeyInfoFillDrawer from './components/KeyInfoFillDrawer.vue'

const { logic, tableRef } = useSingleTable(ExpertTaskLogic)

/**
 * 回到本页时强制刷新。
 *
 * ⚠️ 为什么需要它：`AuditorLayout` 用 `<keep-alive>` 缓存路由组件，
 *    从 `/tasks/create` 返回时组件被**复用**，`onMounted` ⛔ 不会再跑，
 *    而内核 `yzh.vue.core` 全目录**没有** `onActivated` 兜底 ⇒ 列表永远是旧数据。
 *
 * ⚠️ 第一次 `onActivated` 与 `onMounted` 同时发生（首载已经拉过数据），
 *    用标记跳过，避免刚进页面就发两次请求。
 */
let activatedOnce = false
onActivated(() => {
  if (!activatedOnce) {
    activatedOnce = true
    return
  }
  void tableRef.value?.refresh?.()
})
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
          <el-button
            type="default"
            size="small"
            @click="tableRef?.refresh?.()"
          >
            刷新
          </el-button>
        </div>
      </template>
    </YzhTable>

    <!-- 「关键信息补录」抽屉（★ 2026-10-07 裁决 1/3） -->
    <KeyInfoFillDrawer
      v-model="logic.fill.visible"
      :task-name="logic.fill.taskName"
      :gap-list="logic.fill.gapList"
      :submitting="logic.fill.submitting"
      @fill="logic.onFillConfirm"
      @skip="logic.onFillSkip"
    />
  </div>
</template>

<style scoped>
.task-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.task-page__lock {
  margin-left: var(--yzh-space-1, 4px);
}

.task-page__hint {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}
</style>
