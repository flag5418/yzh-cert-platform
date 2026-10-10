<script setup lang="ts">
/**
 * 干跑预览 + 入队回执卡
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ 为什么「干跑」必须显式呈现在页面上（而不是直接跑）
 *
 * 规范化是**覆盖性**操作 —— 重新生成会覆盖企业侧已有产物。
 * 而「已锁定 / 未配规则 / 无锚点」这三类会被**静默跳过** ⇒ 用户点完只看到数字对不上，
 * 却不知道差在哪、该找谁。
 *
 * ⇒ 所以本卡做两件事：
 *   ① **事前**：把「将做什么 / 将跳过什么 / 为什么跳过」逐条说清楚（`plan`）
 *   ② **事后**：把「已入队批次号 / 入队数 / 跳过数」回执出来（`run`）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 为什么「跳过」要分三类而不是一个数字
 *
 * 三种跳过的**处置人不同**：
 *   · `skip_locked`      → 审核员自己锁的，要他自己解锁
 *   · `skip_no_template` → 后台还没配规则，要实施人员去配
 *   · `skip_no_anchor`   → 模板配了但没锚点，是规则问题
 * 合成一个数字 ⇒ 用户只知道「有 3 个没跑」，**不知道该找谁**。
 */
import { Refresh } from '@element-plus/icons-vue'
import { YzhStatusBadge } from '@yzh-core'
import {
  PLAN_ACTION_TEXT,
  PLAN_ACTION_TYPE,
  type NormalizeBatchResult,
  type NormalizePlanResult,
  type NormalizeRunResult,
} from '@share/api/ent/enterprise-normalize'

defineProps<{
  /** 干跑结果（null = 还没预览过） */
  plan: NormalizePlanResult | null
  /** 入队回执（null = 还没执行过） */
  run: NormalizeRunResult | null
  /** ★ 批次进度快照（`batch/{queueCode}` 轮询结果；null = 还没查过） */
  batch: NormalizeBatchResult | null
  /** 干跑进行中 */
  planning: boolean
}>()

const emit = defineEmits<{
  (e: 'dismiss'): void
  /** 用户点「取消批次」—— 由 logic 做二次确认与请求 */
  (e: 'cancel'): void
}>()

/** 批次状态 → 徽标色（⛔ 不让前端按 Status 字符串自由发挥，五态收敛到四色） */
function batchStatusType(b: NormalizeBatchResult): 'success' | 'danger' | 'warning' | 'info' {
  if (b.Status === 'cancelled') return 'warning'
  if (b.Status === 'completed') return b.Failed > 0 ? 'warning' : 'success'
  if (b.Status === 'failed') return 'danger'
  return 'info'
}

function batchStatusText(b: NormalizeBatchResult): string {
  if (b.Status === 'cancelled') return '已取消'
  if (b.Status === 'completed') return b.Failed > 0 ? '完成（含失败）' : '已完成'
  if (b.Status === 'failed') return '执行失败'
  if (b.Status === 'running') return '执行中'
  return '排队中'
}

/** el-progress 只收 0~100 —— 后端声明是整数百分比，这里仍夹紧防越界 */
function batchPct(b: NormalizeBatchResult): number {
  return Math.min(100, Math.max(0, b.Progress))
}
</script>

<template>
  <div class="npc">
    <!-- ══════════ ① 入队回执（执行后） ══════════ -->
    <div v-if="run" class="npc__block npc__block--done">
      <div class="npc__head">
        <span class="npc__title">已入队</span>
        <YzhStatusBadge type="success" text="任务已交给后台队列" />
        <el-button type="default" size="small" @click="emit('dismiss')">收起</el-button>
      </div>
      <ul class="npc__facts">
        <li>批次号：<code>{{ run.QueueCode || '—' }}</code></li>
        <li>入队 {{ run.Queued }} 个文件 · 跳过 {{ run.Skipped }} 个</li>
        <li>
          规范化在后台队列执行（含 AI 取值 + 文档写入，单个文件可能到分钟级）。
          本页每 15 秒跟踪批次进度，完成后自动刷新产物与留痕。
        </li>
      </ul>

      <!--
        ★ 批次进度（`batch/{queueCode}` 轮询快照）
        运行中：进度条 + 处理中/排队数 + 取消入口；结束后：成功/失败汇总 + 失败明细。
        ⛔ 失败明细必须带「哪一份、为什么」—— 只回一个失败数等于把排查成本转嫁给用户。
      -->
      <div v-if="batch" class="npc__batch">
        <div class="npc__batch-head">
          <YzhStatusBadge :type="batchStatusType(batch)" :text="batchStatusText(batch)" />
          <span class="npc__batch-counts">
            已完成 {{ batch.Completed }}/{{ batch.Total }} · 失败 {{ batch.Failed }} · 取消
            {{ batch.Cancelled }}
          </span>
          <el-button
            v-if="!batch.IsFinished"
            type="danger"
            size="small"
            plain
            @click="emit('cancel')"
          >
            取消批次
          </el-button>
        </div>

        <el-progress
          v-if="!batch.IsFinished"
          :percentage="batchPct(batch)"
          :stroke-width="8"
          class="npc__batch-bar"
        />
        <div v-if="!batch.IsFinished" class="npc__sub">
          正在处理 {{ batch.Processing }} 个 · 排队等待 {{ batch.Pending }} 个
        </div>

        <ul v-if="batch.Failures.length > 0" class="npc__items">
          <li v-for="f in batch.Failures" :key="f.StandardFileCode" class="npc__item">
            <YzhStatusBadge type="danger" text="失败" />
            <span class="npc__item-name">{{ f.FileName || f.StandardFileCode }}</span>
            <span class="npc__item-reason">
              {{
                [
                  f.ErrorType || '执行失败',
                  f.ErrorMessage || '',
                  f.RetryCount > 0 ? `已重试 ${f.RetryCount} 次` : '',
                ]
                  .filter(Boolean)
                  .join('，')
              }}
            </span>
          </li>
        </ul>
        <div v-else-if="batch.IsFinished" class="npc__tip npc__tip--ok">
          批次已结束，全部文件执行成功；产物与留痕已反映在上方范围树。
        </div>
      </div>
    </div>

    <!-- ══════════ ② 干跑预览（执行前） ══════════ -->
    <div v-if="plan && !run" class="npc__block">
      <div class="npc__head">
        <span class="npc__title">干跑预览</span>
        <YzhStatusBadge type="info" text="还没执行，以下是预计结果" />
        <el-button type="default" size="small" @click="emit('dismiss')">收起</el-button>
      </div>

      <div class="npc__counts">
        <span class="npc__count npc__count--go">将规范化 {{ plan.Queued }}</span>
        <span class="npc__sep">·</span>
        <span class="npc__count">首次生成 {{ plan.WillFill }}</span>
        <span class="npc__sep">·</span>
        <span class="npc__count">覆盖重生成 {{ plan.WillRegenerate }}</span>
        <span class="npc__sep">·</span>
        <span class="npc__count npc__count--skip">
          跳过 {{ plan.SkipLocked + plan.SkipNoTemplate + plan.SkipNoAnchor }}
        </span>
      </div>

      <div class="npc__sub">
        跳过明细：已锁定 {{ plan.SkipLocked }} · 未配规则 {{ plan.SkipNoTemplate }} · 模板无锚点
        {{ plan.SkipNoAnchor }}
      </div>

      <ul v-if="plan.Items.length > 0" class="npc__items">
        <li v-for="it in plan.Items" :key="it.StandardFileCode" class="npc__item">
          <YzhStatusBadge
            :type="PLAN_ACTION_TYPE[it.Action] || 'info'"
            :text="PLAN_ACTION_TEXT[it.Action] || it.Action"
          />
          <span class="npc__item-name">{{ it.FileName }}</span>
          <span class="npc__item-reason">{{ it.Reason }}</span>
        </li>
      </ul>

      <div v-if="plan.Queued > 0" class="npc__tip npc__tip--ok">
        点下方「开始规范化」把这 {{ plan.Queued }} 个文件投进后台队列。
      </div>
      <div v-else class="npc__tip npc__tip--warn">
        所选文件全部会被跳过，没有可执行项。请按上面的原因处理后重试。
      </div>
    </div>

    <!-- ══════════ ③ 干跑进行中 ══════════ -->
    <div v-else-if="planning" class="npc__block npc__block--busy">
      <el-icon class="npc__busy-icon"><Refresh /></el-icon>
      <span>正在计算「将要做什么」……（只读，不会改动任何数据）</span>
    </div>
  </div>
</template>

<style scoped>
.npc {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
}

.npc__block {
  padding: var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border: 1px solid var(--yzh-color-border-light, #f1f5f9);
}

.npc__block--done {
  background: var(--yzh-color-success-light-9, #f0f9eb);
  border-color: var(--yzh-color-success-light-7, #c2e7b0);
}

.npc__block--busy {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.npc__busy-icon {
  animation: npc-spin 1.2s linear infinite;
}

@keyframes npc-spin {
  to {
    transform: rotate(360deg);
  }
}

.npc__head {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.npc__title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.npc__counts {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-primary, #303133);
}

.npc__count--go {
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-primary, #409eff);
}

.npc__count--skip {
  color: var(--yzh-color-text-tertiary, #909399);
}

.npc__sep {
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

.npc__sub {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.npc__items {
  margin: var(--yzh-space-2, 8px) 0 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
  max-height: 260px;
  overflow: auto;
}

.npc__item {
  display: flex;
  align-items: baseline;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  flex-wrap: wrap;
}

.npc__item-name {
  color: var(--yzh-color-text-primary, #303133);
  word-break: break-all;
}

.npc__item-reason {
  color: var(--yzh-color-text-tertiary, #909399);
  word-break: break-all;
}

.npc__tip {
  margin-top: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
}

.npc__tip--ok {
  color: var(--yzh-color-primary, #409eff);
}

.npc__tip--warn {
  color: var(--yzh-color-warning, #d97706);
}

.npc__facts {
  margin: 0;
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  color: var(--yzh-color-text-regular, #606266);
  word-break: break-all;
}

.npc__facts code {
  background: var(--yzh-color-bg-muted, #f3f4f6);
  border-radius: var(--yzh-radius-sm, 4px);
  padding: 0 var(--yzh-space-1, 4px);
}

/* ★ 批次进度段（按 S01/S03/S07 归一：颜色/字号/间距一律令牌 + 兜底） */
.npc__batch {
  margin-top: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-2, 8px);
  border-top: 1px dashed var(--yzh-color-border-light, #f1f5f9);
}

.npc__batch-head {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.npc__batch-counts {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
}

.npc__batch-bar {
  margin-top: var(--yzh-space-2, 8px);
}
</style>
