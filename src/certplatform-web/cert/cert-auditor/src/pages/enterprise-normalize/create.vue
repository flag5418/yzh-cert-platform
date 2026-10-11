<script setup lang="ts">
/**
 * 企业资料规范化 · **向导页**（专家端 · 路由 `/enterprise-normalize/create`）
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
 * ⇒ 本页 = **开任务的向导**（版式样板 = `pages/tasks/create.vue`），四步：
 *   ① 选择企业 + 阶段
 *   ② 选择文档（整体 / 部分）
 *   ③ 预览将做什么（干跑，零副作用）
 *   ④ 确认并开始规范化 → 入队 → **回任务列表**
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 两条「不转嫁负担」的设计（用户视角铁律）
 *
 * ① **进入第 3 步就自动干跑** —— ⛔ 不让用户先看到一张空白卡再自己找按钮。
 *    干跑是只读的，多跑一次没有任何副作用；失败时留在第 3 步并给「重新预览」。
 *
 * ② **第 4 步点完就走** —— 入队后立刻回任务列表（进度、取消都在那儿）。
 *    ⛔ 不在向导里挂轮询：用户已经离开这个页面了，定时器只是在白打接口。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★ 本页**不使用** `useSingleTable` / `useTreeTable`：主体是「范围勾选 + 干跑预览」，
 *   不是可增删改的单表，硬套内核只会让两边都变形。
 */
import { computed, onActivated, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import { standardLabel } from '@share/api/ent/enterprise-normalize'
import { NormalizeCreateLogic } from './create-logic'
import NormalizeScopeTree from './components/NormalizeScopeTree.vue'
import NormalizePlanCard from './components/NormalizePlanCard.vue'

const route = useRoute()
const router = useRouter()
const logic = new NormalizeCreateLogic()

/** ★ 换 `treeKey` = 换 `el-tree` 的挂载实例 —— 程序化改选择后 `default-checked-keys` 才会生效 */
function treeKey(): string {
  return `${logic.activeStandardCode.value}#${logic.selectionToken.value}`
}

/** 当前 Tab 是否还有文档没勾上（用于「全选本阶段」按钮的显隐判断） */
const allPicked = computed(
  () => logic.checkedCodes.value.length === logic.allFillableCodes.value.length,
)

/**
 * 步骤判断提成计算属性 —— ⛔ **不要在按钮的 `v-if` 里直接写 `>` 比较**。
 *
 * ⚠️ 守卫 `S04`（按钮必须显式 type）截取标签的方式是「从按钮标签名起、到**第一个**
 *    `>` 止」：模板表达式里出现 `>`（如 `v-if="step > 0"`）会把标签**提前截断**，
 *    于是「这个按钮有没有 type」判不出来 ⇒ 误报违规。
 *    提到 `computed` 里既避开这个坑，也让模板更好读。
 * ⚠️ 同理：**本文件注释里也⛔不要写出按钮标签的字面量开头**（含 `<` 与标签名）——
 *    守卫扫的是整份文本，注释里的字面量一样会被当成标签。这条注释本身就是踩过坑之后改写的。
 */
const canPrevStep = computed(() => logic.step.value > 0)
const isBeforePreview = computed(() => logic.step.value < 2)
const isPreviewStep = computed(() => logic.step.value === 2)

// ══════════════════════════════════════════════════════════════════════
// 步骤导航
// ══════════════════════════════════════════════════════════════════════

async function nextStep(): Promise<void> {
  if (logic.step.value === 0) {
    if (!logic.canGoStep2.value) {
      ElMessage.warning('请先选择企业与认证阶段')
      return
    }
    logic.step.value = 1
    return
  }

  if (logic.step.value === 1) {
    if (logic.effectiveCount.value === 0) {
      ElMessage.warning(
        '当前范围里没有可规范化的文档。规范化范围 = 后台已配填写规则且已发布的空白文档，请先到「标准文档标准化」配置并发布。',
      )
      return
    }
    logic.step.value = 2
    // ★ 进第 3 步就自动干跑（只读，零副作用）—— ⛔ 不让用户对着空卡自己找按钮
    await logic.previewPlan()
    return
  }

  if (logic.step.value === 2) {
    if (!logic.canRun.value) {
      ElMessage.warning('预览里没有可执行的文档，请按上面写的原因处理后重新预览')
      return
    }
    logic.step.value = 3
  }
}

function prevStep(): void {
  if (logic.step.value > 0) logic.step.value -= 1
}

/**
 * 「整体 / 部分」切换。
 *
 * ⚠️ 不要在模板里写内联箭头 + 类型断言（`(v) => logic.setScopeMode(v as ...)`）——
 *   `vue-tsc` 会把内联箭头参数判成 `implicitly any`（TS7006）。
 *   提到具名函数里，参数类型也就顺带收紧了。
 */
function onScopeModeChange(v: unknown): void {
  logic.setScopeMode(v === 'partial' ? 'partial' : 'all')
}

/** 第 4 步：开始规范化 → 入队成功即回任务列表（★ 用户裁决） */
async function doRun(): Promise<void> {
  const queueCode = await logic.executeRun()
  if (!queueCode) return
  await goList()
}

function goList(): void {
  void router.push({ name: 'EnterpriseNormalize' })
}

// ══════════════════════════════════════════════════════════════════════
// 初始化 / 复位
// ══════════════════════════════════════════════════════════════════════

onMounted(async () => {
  await logic.init()

  // ★ 用户裁决：「也可以开启一个全新任务，或针对历史任务，重新执行」
  //   列表行按钮「重新执行」带 `?from={queueCode}` 进来
  const from = route.query.from
  if (typeof from === 'string' && from.length > 0) {
    try {
      const ok = await logic.prefillFromTask(from)
      if (ok) ElMessage.info('已按所选任务预填企业与阶段，请确认范围后继续')
      else ElMessage.warning('该任务缺少企业 / 阶段信息，无法预填，请手动选择')
    } catch (e) {
      ElMessage.warning((e as Error)?.message || '预填失败，请手动选择企业与阶段')
    }
  }
})

/**
 * 回到本页时把向导复位。
 *
 * ⚠️ 为什么需要它：`AuditorLayout` 用 `<keep-alive>` 缓存路由组件 ⇒ 第二次进入
 *   本页时 `onMounted` ⛔ 不再触发，页面上会**残留上次填的企业 / 阶段 / 勾选**。
 * ⚠️ 第一次 `onActivated` 与 `onMounted` 同时发生（首载已经初始化过），
 *   用标记跳过 —— 否则会把 `onMounted` 里的「重新执行」预填冲掉。
 */
let activatedOnce = false
onActivated(() => {
  if (!activatedOnce) {
    activatedOnce = true
    return
  }
  logic.resetWizard()
})
</script>

<template>
  <div class="nwiz">
    <!-- ════ 顶部：标题 + 步骤条 ════ -->
    <div class="nwiz__head">
      <div class="nwiz__title">开启规范化任务</div>
      <el-steps :active="logic.step.value" align-center finish-status="success" class="nwiz__steps">
        <el-step title="选择企业与阶段" description="任务的作用域" />
        <el-step title="选择文档" description="整体 / 部分" />
        <el-step title="预览将做什么" description="只读，不改数据" />
        <el-step title="确认并开始" description="入队后台执行" />
      </el-steps>
    </div>

    <!-- ════ 主体 ════ -->
    <div class="nwiz__body">
      <!-- ────────── 步骤 1：企业与阶段 ────────── -->
      <div v-if="logic.step.value === 0" class="nwiz__pane">
        <el-form label-width="110px" class="nwiz__form">
          <el-form-item label="企业" required>
            <el-select
              v-model="logic.enterpriseCode.value"
              :loading="logic.enterpriseLoading.value"
              filterable
              placeholder="请选择企业（本工作区）"
              class="nwiz__select"
              @change="logic.onEnterpriseChange()"
            >
              <el-option
                v-for="e in logic.enterprises.value"
                :key="e.Code"
                :label="e.Name"
                :value="e.Code"
              />
            </el-select>
            <div
              v-if="logic.enterprises.value.length === 0 && !logic.enterpriseLoading.value"
              class="nwiz__hint nwiz__hint--warn"
            >
              本工作区还没有企业 —— 请先到「企业管理」建档。
            </div>
          </el-form-item>

          <el-form-item label="认证阶段" required>
            <el-select
              v-model="logic.stageCode.value"
              :disabled="!logic.enterpriseCode.value"
              placeholder="请先选择企业"
              class="nwiz__select"
              @change="logic.onStageChange()"
            >
              <el-option
                v-for="s in logic.stages.value"
                :key="s.Code"
                :label="s.Name"
                :value="s.Code"
              />
            </el-select>
            <div v-if="logic.stageHint.value" class="nwiz__hint nwiz__hint--warn">
              {{ logic.stageHint.value }}
            </div>
          </el-form-item>
        </el-form>

        <el-alert type="info" :closable="false" show-icon class="nwiz__tip">
          <template #title>规范化范围由「已配填写规则且已发布」的空白文档决定</template>
          <div>
            所以同一个阶段下，能规范化的文档可能只是标准文档里的一小部分。
            没有配置规则的文档不会出现在下一步，也不会被处理。
          </div>
        </el-alert>
      </div>

      <!-- ────────── 步骤 2：选择文档 ────────── -->
      <div v-else-if="logic.step.value === 1" class="nwiz__pane nwiz__pane--wide">
        <!-- 后端没能执行（业务拒绝）—— ★ 与「正常空集」严格区分 -->
        <YzhEmptyState
          v-if="logic.listBlocked.value"
          title="暂时无法开始规范化：还没有已发布的空白文档"
          :description="logic.listMessage.value"
        />

        <!-- 查过了，但这个阶段没有可规范化的文档 -->
        <YzhEmptyState
          v-else-if="logic.loaded.value && logic.standardTabs.value.length === 0"
          title="这个阶段下没有可规范化的文档"
          description="规范化范围 = 已配填写规则且已发布的空白文档。请先到后台「标准文档标准化」配好规则并发布模板。"
        />

        <template v-else>
          <div class="nwiz__section-title">① 范围</div>
          <el-radio-group
            :model-value="logic.scopeMode.value"
            @update:model-value="onScopeModeChange"
          >
            <el-radio value="all">整体（本阶段全部可规范化文档）</el-radio>
            <el-radio value="partial">部分（我自己勾选）</el-radio>
          </el-radio-group>

          <div class="nwiz__section-title">
            ② 文档
            <span class="nwiz__section-count">
              已选 <b>{{ logic.effectiveCount.value }}</b> 个
              <template v-if="logic.scopeMode.value === 'partial'">
                <template
                  v-if="logic.effectiveCount.value !== logic.selectedInActiveStandard.value"
                >
                  （当前标准 {{ logic.selectedInActiveStandard.value }} 个）
                </template>
              </template>
            </span>
          </div>

          <div class="nwiz__tools">
            <el-button type="default" size="small" :disabled="allPicked" @click="logic.selectAllStage()">
              全选本阶段
            </el-button>
            <el-button
              type="default"
              size="small"
              :disabled="logic.effectiveCount.value === 0"
              @click="logic.clearSelection()"
            >
              清空选择
            </el-button>
            <span class="nwiz__tools-hint">选择跨标准保持 —— 可以同时勾多个标准下的文档，一次入队。</span>
          </div>

          <!-- 标准 Tab（★ 只列「有可规范化文档」的标准） -->
          <el-tabs v-model="logic.activeStandardCode.value" class="nwiz__tabs">
            <el-tab-pane
              v-for="s in logic.standardTabs.value"
              :key="s.StandardCode"
              :name="s.StandardCode"
            >
              <template #label>
                <span class="nwiz__tab">
                  <span>{{ standardLabel(s) }}</span>
                  <!-- ★ 标准主数据被删（关联还在）⇒ 必须说破，⛔ 不显示裸 GUID -->
                  <YzhStatusBadge
                    v-if="s.StandardRegistered === false"
                    type="danger"
                    text="未登记"
                  />
                </span>
              </template>
            </el-tab-pane>
          </el-tabs>

          <!--
            ⚠️⚠️ 必须写成**箭头函数**（调用表达式），⛔ 不能写 `@update:checked="logic.onCheckedChange"`！
            Vue 3 **不会**为事件处理器自动绑定 `this`（那是 Vue 2 的行为）⇒ 类方法被当函数引用传递时
            `this` 是 `undefined` ⇒ 一进函数体就 `TypeError`，且**勾选全部静默失效**。
          -->
          <NormalizeScopeTree
            :tree-key="treeKey()"
            :nodes="logic.activeTreeNodes.value"
            :checked-keys="logic.checkedCodes.value"
            :loading="logic.loading.value"
            :empty-text="logic.activeEmptyText.value"
            @update:checked="(codes) => logic.onCheckedChange(codes)"
          />
        </template>
      </div>

      <!-- ────────── 步骤 3：预览 ────────── -->
      <div v-else-if="logic.step.value === 2" class="nwiz__pane nwiz__pane--wide">
        <div class="nwiz__section-title">将要做什么</div>
        <div class="nwiz__hint">
          这是<b>干跑</b>结果：只读计算，不会写库、不会生成文件、不会调 AI。
          下面会把「将处理哪些文档、将跳过哪些、为什么跳过」逐条列出来。
        </div>

        <NormalizePlanCard
          class="nwiz__plan"
          :plan="logic.planResult.value"
          :run="null"
          :batch="null"
          :planning="logic.planning.value"
          :closable="false"
          @dismiss="() => undefined"
          @cancel="() => undefined"
        />

        <!-- 干跑失败 / 没跑成 ⇒ 给一条明确的出路，⛔ 不给用户一张空白卡 -->
        <div
          v-if="!logic.planning.value && !logic.planResult.value"
          class="nwiz__retry"
        >
          <span class="nwiz__retry-text">还没有拿到预览结果。</span>
          <el-button type="primary" plain @click="logic.previewPlan()">重新预览</el-button>
        </div>
      </div>

      <!-- ────────── 步骤 4：确认并开始 ────────── -->
      <div v-else class="nwiz__pane">
        <div class="nwiz__section-title">请核对</div>
        <el-descriptions :column="2" border class="nwiz__desc">
          <el-descriptions-item label="企业 / 阶段" :span="2">
            {{ logic.scopeLabel.value || '—' }}
          </el-descriptions-item>
          <el-descriptions-item label="范围口径">
            {{ logic.scopeMode.value === 'all' ? '整体（全部可规范化文档）' : '部分（勾选）' }}
          </el-descriptions-item>
          <el-descriptions-item label="将规范化">
            {{ logic.planResult.value?.Queued ?? 0 }} 个文档
          </el-descriptions-item>
          <el-descriptions-item label="首次生成">
            {{ logic.planResult.value?.WillFill ?? 0 }}
          </el-descriptions-item>
          <el-descriptions-item label="覆盖重生成">
            {{ logic.planResult.value?.WillRegenerate ?? 0 }}
          </el-descriptions-item>
          <el-descriptions-item label="会被跳过" :span="2">
            已锁定 {{ logic.planResult.value?.SkipLocked ?? 0 }} ·
            未配规则 {{ logic.planResult.value?.SkipNoTemplate ?? 0 }} ·
            模板无锚点 {{ logic.planResult.value?.SkipNoAnchor ?? 0 }}
          </el-descriptions-item>
        </el-descriptions>

        <el-alert type="info" :closable="false" show-icon class="nwiz__tip">
          <template #title>开始后会回任务列表</template>
          <div>
            规范化在后台队列执行（含 AI 取值 + 文档写入，单个文档可能到分钟级）。
            任务列表里能看到进度、也能取消；跑完后到「企业资料审核」逐份审核并统一导出。
          </div>
        </el-alert>
      </div>
    </div>

    <!-- ════ 底部：导航 ════ -->
    <div class="nwiz__foot">
      <el-button type="default" @click="goList()">取消</el-button>
      <div class="nwiz__foot-right">
        <el-button v-if="canPrevStep" type="default" @click="prevStep()">上一步</el-button>
        <el-button v-if="isBeforePreview" type="primary" @click="nextStep()">下一步</el-button>
        <el-button
          v-else-if="isPreviewStep"
          type="primary"
          :loading="logic.planning.value"
          :disabled="!logic.canRun.value"
          @click="nextStep()"
        >
          下一步
        </el-button>
        <el-button
          v-else
          type="primary"
          :loading="logic.running.value"
          :disabled="!logic.canRun.value"
          @click="doRun()"
        >
          开始规范化（{{ logic.planResult.value?.Queued ?? 0 }}）
        </el-button>
      </div>
    </div>
  </div>
</template>

<style scoped>
/*
 * ★ 纵向壳：头（步骤条）/ 主体（可滚动）/ 脚（导航）三段。
 *   ⛔ 主体必须自己给 `overflow: auto` —— 否则内容超屏时**整页滚不动**。
 */
.nwiz {
  height: 100%;
  display: flex;
  flex-direction: column;
  box-sizing: border-box;
  background: var(--yzh-color-bg-container, #ffffff);
  overflow: hidden;
}

.nwiz__head {
  flex-shrink: 0;
  padding: var(--yzh-space-4, 16px) var(--yzh-space-5, 24px) 0;
  border-bottom: 1px solid var(--yzh-color-border-light, #f1f5f9);
}

.nwiz__title {
  font-size: var(--yzh-font-size-lg, 16px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  margin-bottom: var(--yzh-space-3, 12px);
}

.nwiz__steps {
  padding-bottom: var(--yzh-space-3, 12px);
}

.nwiz__body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-5, 20px) var(--yzh-space-5, 24px);
}

.nwiz__pane {
  max-width: 900px;
}

.nwiz__pane--wide {
  max-width: none;
}

.nwiz__form {
  max-width: 720px;
}

.nwiz__select {
  width: 420px;
}

.nwiz__hint {
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: 1.7;
  margin-top: var(--yzh-space-1, 4px);
  color: var(--yzh-color-text-secondary, #909399);
}

.nwiz__hint--warn {
  color: var(--yzh-color-warning, #d97706);
}

.nwiz__tip {
  margin-top: var(--yzh-space-2, 8px);
}

.nwiz__section-title {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  margin: var(--yzh-space-4, 18px) 0 var(--yzh-space-2, 8px);
}

.nwiz__section-title:first-child {
  margin-top: 0;
}

.nwiz__section-count {
  font-weight: var(--yzh-font-weight-normal, 400);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #909399);
  margin-left: var(--yzh-space-2, 8px);
}

.nwiz__tools {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  flex-wrap: wrap;
  margin-bottom: var(--yzh-space-2, 8px);
}

.nwiz__tools-hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-tertiary, #909399);
}

.nwiz__tabs {
  margin-top: var(--yzh-space-1, 4px);
}

.nwiz__tab {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
}

.nwiz__plan {
  margin-top: var(--yzh-space-2, 8px);
}

.nwiz__retry {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  margin-top: var(--yzh-space-3, 12px);
}

.nwiz__retry-text {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-secondary, #909399);
}

.nwiz__desc {
  margin-bottom: var(--yzh-space-4, 16px);
}

.nwiz__foot {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-5, 24px);
  border-top: 1px solid var(--yzh-color-border-light, #f1f5f9);
  background: var(--yzh-color-bg-container, #ffffff);
}

.nwiz__foot-right {
  display: flex;
  gap: var(--yzh-space-2, 8px);
}
</style>
