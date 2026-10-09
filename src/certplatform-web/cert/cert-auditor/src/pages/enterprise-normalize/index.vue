<script setup lang="ts">
/**
 * 企业资料规范化（专家端）
 *
 * 菜单 `MENU_AUD_12`｜路由 `/enterprise-normalize`
 * 规格：`docs/20-体系认证/03-详细设计/05-企业资料规范化/{54,55,60}`
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-07 用户裁决（**推翻了本页此前的形态**）
 *
 * 用户原话：「我们点击一个企业的阶段，**不是应该按标准显示 tab 页面，针对不同的标准，
 * 不同的文件夹，文件，或针对该阶段，选择需要进行规范化的文件，进行规范化处理吗**，
 * 当前这个页面和我设想的差异较大」。
 *
 * ⇒ 本页现在的形态 = 左树（企业›阶段）+ 右区（统计条 → 标准 Tab → 文件夹/文件勾选树
 *   → 干跑预览 → 底部操作条）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 三条显示口径（用户裁决 + 2026-10-09 补第三条）
 *
 * ① **未配填写规则的标准文件 → 不显示**（不是灰显、不是折叠）
 * ② **标准 → 右区 Tab**：企业该阶段挂载的标准**全部显示**（含没有可规范化文件的），
 *    让「食品标准还没配规则」这件事**看得见**，⛔ 不是静默消失。
 *    ⚠️ 文件夹**全显示**并带「可规范化 N / 共 M 个文件」两个计数 ——
 *       否则用户会以为系统丢了数据（实测：167 份标准域文件里只有 1 份可规范化）。
 * ③ **标准名一律走 `standardLabel()`，⛔ 绝不显示裸 GUID**（2026-10-09 用户报障）。
 *    `cert_iso_standard` 的主数据被删、而 `cert_enterprise_stage` 的关联还在时，
 *    后端此前 `StandardName = iso?.StandardName ?? code` ⇒ Tab 上直接显示
 *    `475da4fe-8f50-4bf7-bf2b-b39869d5ddf7`。现在：显示「未登记标准（475da4fe）」
 *    + `未登记` 徽标 + 一条 danger 说明条（说清「为什么永远不会有文件、去哪儿修」）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 「干跑 → 确认 → 入队」三段式 + 入队后自动刷新
 *
 * 规范化是**覆盖性**操作，且「已锁定 / 未配规则 / 无锚点」三类会被静默跳过 ⇒
 * **必须先预览再执行**。`logic.canRun` 强制这一点：改了勾选就要重新预览。
 *
 * 入队后 `logic` 会启动 15s 轮询（见 `logic.startAutoRefresh`）——
 * 后台是串行跑，用户不该靠反复点「刷新」来猜进度；跑完/超时会自动停，也能手动停。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ⛔ 本页**不使用** `useSingleTable` / `useTreeTable`：右区是「范围树 + 干跑预览」，不是单表。
 * ⛔ 本页**不再有**「本期还没做（P2）」卡：其中的 `plan` / `run` / 五级范围展开本轮已实现。
 */
import { onMounted, onUnmounted } from 'vue'
import { MagicStick } from '@element-plus/icons-vue'
import { YzhEmptyState, YzhStatusBadge, YzhTreeTableLayout, type YzhTreeNode } from '@yzh-core'
import {
  FILL_STATUS_TEXT,
  FILL_STATUS_TYPE,
  standardLabel,
  toPercent,
} from '@share/api/ent/enterprise-normalize'
import { EnterpriseNormalizeLogic } from './logic'
import NormalizeScopeTree from './components/NormalizeScopeTree.vue'
import NormalizePlanCard from './components/NormalizePlanCard.vue'

const logic = new EnterpriseNormalizeLogic()

onMounted(() => logic.init())

// ★ 自动刷新用的是 `setInterval` ⇒ 组件卸载**必须**停，否则路由切走后定时器还在打接口
onUnmounted(() => logic.stopAutoRefresh())

function handleNodeClick(node: YzhTreeNode): void {
  void logic.onNodeClick(node)
}

/** ★ 换 `treeKey` = 换 `el-tree` 的挂载实例 —— 程序化改选择后 `default-checked-keys` 才会生效 */
function treeKey(): string {
  return `${logic.activeStandardCode.value}#${logic.selectionToken.value}`
}

function runButtonText(): string {
  if (logic.canRun.value) return `开始规范化（${logic.planResult.value?.Queued ?? 0}）`
  return '开始规范化'
}
</script>

<template>
  <div class="en-page">
    <YzhTreeTableLayout
      :tree-data="logic.treeNodes.value"
      :tree-width="280"
      label-field="Label"
      :tree-toolbar="true"
      :tree-searchable="true"
      :tree-lazy="false"
      :tree-default-expand-all="true"
      @tree-node-click="handleNodeClick"
    >
      <!-- 树底部：空树 / 加载失败时的兜底提示 -->
      <template #treeFooter>
        <div v-if="logic.treeHint.value" class="en-tree-hint">{{ logic.treeHint.value }}</div>
      </template>

      <template #default>
        <div class="en-main">
          <!-- ══════════ 作用域条 ══════════ -->
          <div class="en-scopebar">
            <div class="en-scopebar__left">
              <span class="en-scopebar__label">当前作用域</span>
              <span class="en-scopebar__value">{{ logic.scopeLabel.value || '未选择' }}</span>
            </div>
            <el-button
              type="default"
              size="small"
              :disabled="!logic.scopeReady.value"
              :loading="logic.loading.value"
              @click="logic.loadRange()"
            >
              刷新
            </el-button>
          </div>

          <!-- ══════════ ① 还没选到阶段 ══════════ -->
          <YzhEmptyState
            v-if="!logic.scopeReady.value"
            :icon="MagicStick"
            title="请先在左侧选择「企业 › 阶段」"
            description="规范化范围按阶段归属 —— 它由「配了填写规则且已发布」的空白文档决定，不是全部标准文件。"
          />

          <!-- ══════════ ② 后端没能执行（业务拒绝） ══════════
               ★ 与「正常空集」严格区分：问题**不在这个阶段**，说「这个阶段下没有…」会把人带偏 -->
          <YzhEmptyState
            v-else-if="logic.listBlocked.value"
            title="暂时无法开始规范化：还没有已发布的空白文档"
            :description="logic.listMessage.value"
          />

          <!-- ══════════ ③ 查过了，但这个阶段没有可规范化的文档 ══════════ -->
          <YzhEmptyState
            v-else-if="logic.loaded.value && logic.standards.value.length === 0"
            title="这个阶段下没有可规范化的文档"
            description="规范化范围 = 配了填写规则且已发布的空白文档。请先到后台「标准文档标准化」页配好规则并发布模板。"
          />

          <!-- ══════════ ④ 主体 ══════════ -->
          <template v-else>
            <!-- 统计条 —— 直接回答「为什么只有这几个」 -->
            <div class="en-stats">
              <span class="en-stats__item">
                本阶段可规范化 <b class="en-stats__num">{{ logic.totalFillable.value }}</b> 份
              </span>
              <span class="en-stats__item">标准域文件共 {{ logic.totalFiles.value }} 份</span>
              <span class="en-stats__hint">
                范围 = 已配填写规则且已发布的空白文档，⛔ 不是全部标准文件
              </span>
            </div>

            <!--
              ══════════ 自动刷新提示条（入队成功后出现） ══════════

              ★ 为什么要有：`run` 只把任务投进队列就返回，真正执行在后台串行跑（单个文件可能到分钟级）。
              此前用户必须**自己反复点「刷新」**才知道跑完没有 —— 而「不知道跑没跑完」
              正是最容易被误判成「点了没反应 / 系统卡死」的状态。
              ⛔ 不显示「预计还要多久」：后端不给进度，编时间就是骗人。
            -->
            <div v-if="logic.polling.value" class="en-poll">
              <span class="en-poll__text">
                后台正在执行规范化，本页每 15 秒自动刷新一次<template
                  v-if="logic.pollCount.value > 0"
                >
                  （已刷新 {{ logic.pollCount.value }} 次<template v-if="logic.lastRefreshAt.value"
                    >，最近 {{ logic.lastRefreshAt.value }}</template
                  >）</template
                >；全部跑完会自动停。
              </span>
              <el-button type="default" size="small" @click="logic.stopAutoRefresh()">
                停止自动刷新
              </el-button>
            </div>

            <!-- 标准 Tab（★ 全部显示，含没有可规范化文件的 —— 让「还没配规则」看得见） -->
            <el-tabs v-model="logic.activeStandardCode.value" class="en-tabs">
              <el-tab-pane
                v-for="s in logic.standards.value"
                :key="s.StandardCode"
                :name="s.StandardCode"
              >
                <template #label>
                  <span class="en-tab">
                    <!--
                      ★★ 标准名一律走 `standardLabel()` —— ⛔ 绝不显示裸 GUID（2026-10-09 用户报障）。

                      主数据被删时后端回传的是**空串** + `StandardRegistered=false`
                      （⛔ 不再 `?? code` 回退），这里显示「未登记标准（475da4fe）」并挂 danger 徽标。
                    -->
                    <span class="en-tab__name">{{ standardLabel(s) }}</span>
                    <YzhStatusBadge
                      v-if="s.StandardRegistered === false"
                      type="danger"
                      text="未登记"
                    />
                    <YzhStatusBadge v-else-if="!s.Mounted" type="info" text="未挂载" />
                    <span
                      class="en-tab__count"
                      :class="{ 'en-tab__count--zero': s.FillableCount === 0 }"
                    >
                      {{ s.FillableCount }}
                    </span>
                  </span>
                </template>
              </el-tab-pane>
            </el-tabs>

            <!--
              ══════════ 「为什么这里是空的」说明条 ══════════

              ★ 为什么必须有：Tab 上的徽标只能提示「有问题」，说不清「是什么问题、去哪儿修」。
              三种原因指向**三种不同处置**：
                · 未登记 → 标准主数据被删但关联还在 ⇒ 清关联 / 补主数据（找管理员）
                · 未挂载 → 只是配了模板但企业阶段没勾该标准 ⇒ 去企业阶段关联处补勾
                · 未配规则 → 该标准下还没有已发布的空白文档 ⇒ 去后台「标准文档标准化」发布
              ⛔ 合成一句「暂无数据」= 让人四处乱找。
            -->
            <div
              v-if="logic.unregisteredStandards.value.length > 0"
              class="en-notice en-notice--danger"
            >
              <span class="en-notice__title">
                本阶段有 {{ logic.unregisteredStandards.value.length }} 个标准「未登记」
              </span>
              <span class="en-notice__body">
                {{
                  logic.unregisteredStandards.value.map((s) => standardLabel(s)).join('、')
                }}：企业阶段的关联还在，但基础数据「ISO 标准」里已经没有这个标准了（可能已被删除）
                ⇒ 这些标准<b>永远不会有</b>可规范化的文件。请到「机构-标准关联」清理关联，
                或先在基础数据里补回该标准。
              </span>
            </div>

            <div
              v-else-if="logic.activeStandardMounted.value === false"
              class="en-notice en-notice--info"
            >
              <span class="en-notice__title">该标准未挂到本企业阶段</span>
              <span class="en-notice__body">
                它只是因为存在「已发布的填写规则模板」才显示 —— 文件能规范化，但企业侧还没认领这个标准。
                若它本应属于本阶段，请到企业阶段关联处补勾。
              </span>
            </div>

            <!-- 文件夹 / 文件勾选树（文件夹可整选） -->
            <!--
              ⚠️⚠️ 必须写成**箭头函数**（调用表达式），⛔ 不能写 `@update:checked="logic.onCheckedChange"`！
              Vue 3 **不会**为事件处理器自动绑定 `this`（那是 Vue 2 的行为）⇒ 类方法被当函数引用传递时
              `this` 是 `undefined` ⇒ 一进函数体就 `TypeError`，且**勾选/「立即跑」全部静默失效**
              （真机实测：checkbox 能勾上、`getCheckedKeys` 返回正确，但 `checkedCodes` 恒为空）。
            -->
            <NormalizeScopeTree
              :tree-key="treeKey()"
              :nodes="logic.activeTreeNodes.value"
              :checked-keys="logic.checkedCodes.value"
              :loading="logic.loading.value"
              :empty-text="logic.activeEmptyText.value"
              @update:checked="(codes) => logic.onCheckedChange(codes)"
              @run-one="(file) => logic.runOne(file)"
            />

            <!-- 干跑预览 / 入队回执 -->
            <NormalizePlanCard
              class="en-plan"
              :plan="logic.planResult.value"
              :run="logic.runResult.value"
              :planning="logic.planning.value"
              @dismiss="logic.dismissCard()"
            />

            <!-- 单文件同步执行结果（「立即跑」的证据，⛔ 不美化） -->
            <div v-if="logic.recentResults.value.length > 0" class="en-single">
              <div class="en-single__head">
                <span class="en-single__title">单文件执行结果</span>
                <span class="en-single__sub">「立即跑」是同步执行，用于单个文件的快速验证</span>
              </div>

              <div
                v-for="r in logic.recentResults.value"
                :key="r.StandardFileCode"
                class="en-single__row"
              >
                <div class="en-single__row-head">
                  <span class="en-single__name">{{ r.FileName }}</span>
                  <YzhStatusBadge
                    :type="FILL_STATUS_TYPE[r.Result.Status] || 'info'"
                    :text="FILL_STATUS_TEXT[r.Result.Status] || r.Result.Status"
                  />
                  <el-button
                    type="default"
                    size="small"
                    @click="logic.dismissResult(r.StandardFileCode)"
                  >
                    收起
                  </el-button>
                </div>

                <ul class="en-single__list">
                  <li>说明：{{ r.Result.Message || '—' }}</li>
                  <li>
                    完成率 {{ toPercent(r.Result.Completion) }}% · 可信度
                    {{ toPercent(r.Result.Confidence) }}%
                  </li>
                  <li>
                    锚点 {{ r.Result.AnchorCount }} 个 · 待办 {{ r.Result.PendingCount }} 个{{
                      r.Result.Pendings.length > 0 ? '（原因见下）' : ''
                    }}
                  </li>
                  <li>
                    自验收：{{
                      r.Result.Verified
                        ? '通过（无残留锚点 / 残留标记）'
                        : '未通过 —— 文档里可能还有没填上的锚点，需人工看一眼'
                    }}
                  </li>
                  <li>产物：{{ r.Result.OutputPath || '（本次未生成产物）' }}</li>
                  <li>留痕 Code：{{ r.Result.FillLogCode || '—' }}</li>
                </ul>

                <!--
                  ★ 待办明细 —— ⛔ 只报「待办 N 个」等于没说。
                  同一个数字背后有四种原因，指向四个**不同**的修复动作
                  （模板没配数据源 / 参数未在后台定义 / 企业没填值 / 企业档案字段为空），
                  所以必须把后端的归因原文列出来。
                -->
                <div v-if="r.Result.Pendings.length > 0" class="en-single__pending">
                  <div class="en-single__pending-title">
                    待办 {{ r.Result.Pendings.length }} 处 —— 每条都写清了「去哪儿修」
                  </div>
                  <ul class="en-single__list">
                    <li v-for="(p, i) in r.Result.Pendings" :key="i" class="en-single__pending-item">
                      <span class="en-single__token">{{ p.Token }}</span>
                      <span v-if="p.Location" class="en-single__loc">（{{ p.Location }}）</span>
                      <span class="en-single__why">{{ p.Reason }}</span>
                    </li>
                  </ul>
                </div>

                <div v-if="r.Result.Warnings.length > 0" class="en-single__warn">
                  <div class="en-single__warn-title">
                    警告 {{ r.Result.Warnings.length }} 条（不致命，但要看一眼）
                  </div>
                  <ul class="en-single__list">
                    <li v-for="(w, i) in r.Result.Warnings" :key="i">{{ w }}</li>
                  </ul>
                </div>
              </div>
            </div>

            <!-- ══════════ 底部操作条（sticky，滚动时始终可见） ══════════ -->
            <div class="en-actionbar">
              <div class="en-actionbar__left">
                <el-button type="default" size="small" @click="logic.selectAllStage()">
                  全选本阶段（{{ logic.allFillableCodes.value.length }}）
                </el-button>
                <el-button
                  type="default"
                  size="small"
                  :disabled="logic.selectedCount.value === 0"
                  @click="logic.clearSelection()"
                >
                  清空选择
                </el-button>
                <span class="en-actionbar__count">
                  已选 {{ logic.selectedCount.value }} 个文件
                  <!-- ★ 选择跨标准保持 ⇒ 当前 Tab 可能一个都没勾，必须说破，否则「空树 + 已选 3 个」自相矛盾 -->
                  <span
                    v-if="logic.selectedCount.value !== logic.selectedInActiveStandard.value"
                    class="en-actionbar__cross"
                  >
                    （当前标准 {{ logic.selectedInActiveStandard.value }} 个）
                  </span>
                </span>
                <span
                  v-if="logic.planStale.value && logic.planResult.value"
                  class="en-actionbar__hint"
                >
                  选择已变，请重新预览
                </span>
              </div>

              <div class="en-actionbar__right">
                <el-button
                  type="primary"
                  plain
                  :loading="logic.planning.value"
                  :disabled="logic.selectedCount.value === 0"
                  @click="logic.previewPlan()"
                >
                  预览将做什么
                </el-button>
                <el-button
                  type="primary"
                  :loading="logic.running.value"
                  :disabled="!logic.canRun.value"
                  @click="logic.executeRun()"
                >
                  {{ runButtonText() }}
                </el-button>
              </div>
            </div>
          </template>
        </div>
      </template>
    </YzhTreeTableLayout>
  </div>
</template>

<style scoped>
/*
 * ⛔ 不要动这两个类 —— `YzhTreeTableLayout` 的右面板是 `overflow:hidden`，
 *    页面必须自己给滚动容器，否则内容被裁到面板高且**整页滚不动**
 *    （症状会伪装成「XX 展不开 / 点了没反应 / 下面的看不到」）。
 */
.en-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  overflow: hidden;
}

.en-main {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px) 0;
  box-sizing: border-box;
}

.en-tree-hint {
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.6;
  color: var(--yzh-color-warning, #d97706);
}

/* ── 作用域条 ── */
.en-scopebar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  padding-bottom: var(--yzh-space-2, 8px);
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
  flex-wrap: wrap;
}

.en-scopebar__left {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  min-width: 0;
}

.en-scopebar__label {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-tertiary, #909399);
  flex-shrink: 0;
}

.en-scopebar__value {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── 统计条 ── */
.en-stats {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-4, 16px);
  margin-top: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
  flex-wrap: wrap;
}

.en-stats__item {
  flex: none;
}

.en-stats__num {
  font-size: var(--yzh-font-size-md, 14px);
  color: var(--yzh-color-primary, #409eff);
}

.en-stats__hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

/* ── 标准 Tab ── */
.en-tabs {
  margin-top: var(--yzh-space-2, 8px);
}

.en-tab {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
}

.en-tab__name {
  font-size: var(--yzh-font-size-sm, 13px);
}

.en-tab__count {
  min-width: 18px;
  padding: 0 var(--yzh-space-1, 4px);
  border-radius: 9px;
  background: var(--yzh-color-primary-light-9, #ecf5ff);
  color: var(--yzh-color-primary, #409eff);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 18px;
  text-align: center;
}

.en-tab__count--zero {
  background: var(--yzh-color-bg-muted, #f3f4f6);
  color: var(--yzh-color-text-placeholder, #a8abb2);
}

.en-plan {
  margin-top: var(--yzh-space-3, 12px);
}

/* ── 「为什么这里是空的」说明条 ──
   两种语义：danger = 数据有问题（未登记，需人工介入）；info = 只是没挂载，不算错 */
.en-notice {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: var(--yzh-space-2, 8px);
  margin-top: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-base, 1.6);
}

.en-notice--danger {
  background: var(--yzh-color-danger-light-9, #fef0f0);
  border: 1px solid var(--yzh-color-danger-light-7, #fde2e2);
}

.en-notice--info {
  background: var(--yzh-color-info-light-9, #f4f4f5);
  border: 1px solid var(--yzh-color-info-light-7, #e9e9eb);
}

.en-notice__title {
  flex: none;
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.en-notice--danger .en-notice__title {
  color: var(--yzh-color-danger, #dc2626);
}

.en-notice__body {
  color: var(--yzh-color-text-regular, #606266);
  word-break: break-all;
}

/* ── 自动刷新提示条 ── */
.en-poll {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: var(--yzh-space-2, 8px);
  margin-top: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-primary-light-9, #ecf5ff);
  border: 1px solid var(--yzh-color-primary-light-8, #d9ecff);
  font-size: var(--yzh-font-size-xs, 12px);
}

.en-poll__text {
  color: var(--yzh-color-text-regular, #606266);
}

/* ── 单文件同步结果 ── */
.en-single {
  margin-top: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
}

.en-single__head {
  display: flex;
  align-items: baseline;
  gap: var(--yzh-space-2, 8px);
  margin-bottom: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.en-single__title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.en-single__sub {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.en-single__row + .en-single__row {
  margin-top: var(--yzh-space-3, 12px);
  padding-top: var(--yzh-space-3, 12px);
  border-top: 1px dashed var(--yzh-color-border, #e2e8f0);
}

.en-single__row-head {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.en-single__name {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  word-break: break-all;
}

.en-single__list {
  margin: var(--yzh-space-1, 4px) 0 0;
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

.en-single__warn {
  margin-top: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-2, 8px);
  border-top: 1px dashed var(--yzh-color-border, #e2e8f0);
}

.en-single__warn-title {
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-warning, #d97706);
  margin-bottom: var(--yzh-space-1, 4px);
}

/* ── 待办明细（含归因）── */
.en-single__pending {
  margin-top: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-2, 8px);
  border-top: 1px dashed var(--yzh-color-border, #e2e8f0);
}

.en-single__pending-title {
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  margin-bottom: var(--yzh-space-1, 4px);
}

.en-single__pending-item {
  display: flex;
  flex-wrap: wrap;
  gap: var(--yzh-space-1, 4px);
}

.en-single__token {
  color: var(--yzh-color-primary, #2563eb);
  word-break: break-all;
}

.en-single__loc {
  color: var(--yzh-color-text-tertiary, #909399);
}

.en-single__why {
  color: var(--yzh-color-text-regular, #606266);
  word-break: break-all;
}

/* ── 底部操作条（sticky 贴住滚动容器底边） ── */
.en-actionbar {
  position: sticky;
  bottom: 0;
  z-index: 2;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-3, 12px);
  margin-top: var(--yzh-space-4, 16px);
  padding: var(--yzh-space-3, 12px) 0;
  background: var(--yzh-color-bg-container, #ffffff);
  border-top: 1px solid var(--yzh-color-border-light, #f1f5f9);
  flex-wrap: wrap;
}

.en-actionbar__left {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
}

.en-actionbar__count {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

/* 「已选总数 ≠ 当前标准已选」时的补充说明 */
.en-actionbar__cross {
  color: var(--yzh-color-text-tertiary, #909399);
}

.en-actionbar__hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-warning, #d97706);
}

.en-actionbar__right {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex: none;
}
</style>
