<script setup lang="ts">
/**
 * 企业资料规范化 · **任务列表页**（专家端 · 路由 `/enterprise-normalize`）
 *
 * 菜单 `MENU_AUD_12`｜规格 `docs/20-体系认证/03-详细设计/05-企业资料规范化/{54,55,60}`
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户裁决：`/enterprise-normalize` 拆成两个页面
 *
 * 用户原话：「这个页面，我发现了，应该将该页面做成 2 个，第一个是企业资料规范化，
 * 第二个是企业资料审核」＋「第一个页面我们按 `/tasks` 进行向导型的进行任务引导，
 * 核心是开启任务」。
 *
 * ⇒ 本页只回答「我开过哪些规范化任务、各自跑到哪一步了」，以及四个动作：
 *   开新任务 / 审核 / 重新执行 / 取消。
 *   ⛔ 范围勾选与干跑预览**不在本页** —— 走向导 `/enterprise-normalize/create`。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 显示口径（用户原话）
 *
 * 「我们当前就不用理会所有的标准文档，我们只考虑标准文档中已经设置规则的」＋
 * 「我们不应该在界面上显示什么是标准化文件数量，哪些是已经定义了填写规则的
 * 文档数量，这样，让审核员非常奇怪，不理解」。
 *
 * ⇒ 本页**只呈现任务结果**（任务号 / 企业 / 阶段 / 状态 / 进度 / 时间 / 操作人），
 *   ⛔ 不出现「标准域共 N 份 / 其中 M 份配了规则」这类内部计数。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ 时间口径（⛔ 两列基准不同，别当成一个）
 *
 * · `CreateTime`（入队时间）= **UTC**（`BaseEntity.CreateTime` 走 `DateTime.UtcNow`）
 *   ⇒ 后端已补 `Z`，`formatDateTime` 会正确换算成本地时间
 * · `StartTime` / `EndTime` = **本地时间**（`QueueManager` 显式 `DateTime.Now`）
 *   ⇒ JSON **不带 `Z`**，JS 按本地解析 —— 正是我们要的结果，⛔ 不要手动补 `Z`
 */
import { onActivated, onDeactivated, onUnmounted } from 'vue'
import { YzhTable, useSingleTable, YzhStatusBadge } from '@yzh-core'
import { formatDateTime } from '@share/utils/format'
import { TASK_STATUS_TEXT, TASK_STATUS_TYPE } from '@share/api/ent/enterprise-normalize'
import { NormalizeTaskListLogic } from './logic'

const { logic, tableRef } = useSingleTable(NormalizeTaskListLogic)

/**
 * 回到本页时强制刷新。
 *
 * ⚠️ 为什么需要它：`AuditorLayout` 用 `<keep-alive>` 缓存路由组件，
 *    从向导 `/enterprise-normalize/create` 返回时组件被**复用**，`onMounted`
 *    ⛔ 不会再跑，而内核全目录**没有** `onActivated` 兜底 ⇒ 列表永远是旧数据
 *    （= 用户报的「创建完任务返回列表，列表没刷新」）。
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

// ★ 自动刷新用的是 `setInterval` ⇒ 路由切走 / 组件卸载**都必须**停，
//   否则定时器留在后台一直打接口（最长要打到 10 分钟的封顶才自己停）。
//
// ⚠️⚠️ 光有 `onUnmounted` **不够**（2026-10-11 复核缺陷 #3）：
//   `AuditorLayout` 是 `<keep-alive :max="12">` ⇒ 路由切走**不触发 unmount**，
//   组件只是被**缓存**了 ⇒ `onUnmounted` 永远不跑、定时器一直活着，
//   与该行注释声明的意图正好相反。
//   ⇒ 必须用 `onDeactivated`（keep-alive 下「离开页面」真正对应的钩子）。
//   `onUnmounted` 保留：万一将来去掉 keep-alive，它才是唯一兜底。
//
// ★ 回到本页时定时器**不用手动重挂** —— 上面 `onActivated` 里的 `refresh()`
//   会重跑 `dataLoader()`，而 `dataLoader` 末尾就调 `scheduleAutoRefresh()`。
//   ⛔ 在这里再挂一次会多出一个定时器（`scheduleAutoRefresh` 只能防同一次内的重复挂）。
onDeactivated(() => logic.stopAutoRefresh())
onUnmounted(() => logic.stopAutoRefresh())

/** 时间列的空值兜底（⛔ 不渲染 `Invalid Date` / 空白） */
function fmt(v?: string | null): string {
  return v ? formatDateTime(v) : '—'
}
</script>

<template>
  <div class="ntl">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      :search-max-fields="2"
      :page-size="20"
      row-key="QueueCode"
      empty-text="还没有规范化任务。点右上角「创建规范化任务」开始。"
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    >
      <!-- 状态：中文标签（文案与语义色均来自共享映射，⛔ 前端不硬编码枚举） -->
      <template #column-Status="{ row }">
        <YzhStatusBadge
          :type="TASK_STATUS_TYPE[row.Status] || 'info'"
          :text="TASK_STATUS_TEXT[row.Status] || row.Status"
        />
      </template>

      <!-- 进度：队列执行进度（⛔ 不是「认可进度」） -->
      <template #column-Progress="{ row }">
        <div class="ntl-progress">
          <el-progress
            :percentage="Math.min(100, Math.max(0, Number(row.Progress ?? 0)))"
            :stroke-width="10"
            :status="row.Status === 'failed' ? 'exception' : undefined"
          />
          <div class="ntl-progress__counts">
            已完成 {{ row.Completed }}/{{ row.Total }}
            <template v-if="row.Failed > 0"> · 失败 {{ row.Failed }}</template>
            <template v-if="row.Cancelled > 0"> · 取消 {{ row.Cancelled }}</template>
          </div>
        </div>
      </template>

      <!-- 入队 / 起止时间（★ 起止是本地时间，入队是 UTC —— 见文件头注释） -->
      <template #column-CreateTime="{ row }">
        <div class="ntl-time">
          <div>入队 {{ fmt(row.CreateTime) }}</div>
          <div v-if="row.StartTime" class="ntl-time__sub">开始 {{ fmt(row.StartTime) }}</div>
          <div v-if="row.EndTime" class="ntl-time__sub">结束 {{ fmt(row.EndTime) }}</div>
        </div>
      </template>

      <template #toolbar-right>
        <div class="ntl-hint">
          <span v-if="logic.autoRefreshing.value" class="ntl-hint__auto">
            有任务正在执行，本页每 15 秒自动刷新
          </span>
          <span>共 {{ logic.pagination.total }} 个任务</span>
          <el-button
            v-if="logic.autoRefreshing.value"
            type="default"
            size="small"
            @click="logic.stopAutoRefresh()"
          >
            停止自动刷新
          </el-button>
        </div>
      </template>
    </YzhTable>
  </div>
</template>

<style scoped>
/*
 * ⛔ 保留高度/滚动约定：本页由 `AuditorLayout` 的内容区承载，
 *    与 `pages/tasks/index.vue` 同形（`height:100%` + `overflow:auto`）。
 */
.ntl {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}

.ntl-progress__counts {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
}

.ntl-time {
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-base, 1.6);
  color: var(--yzh-color-text-regular, #606266);
}

.ntl-time__sub {
  color: var(--yzh-color-text-tertiary, #909399);
}

.ntl-hint {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
}

.ntl-hint__auto {
  color: var(--yzh-color-primary, #409eff);
}
</style>
