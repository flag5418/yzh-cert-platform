<script setup lang="ts">
/**
 * Tab4 · 校验结果（三色）
 *
 * 【判定口径】后端 `BuildViolationsAsync` 一处决定，本组件**只渲染不判定**。
 *   ⛔ 前端不再复算一遍 —— 两套判定必然分叉（本项目反复出现的缺陷模式）。
 *
 * 【三色】（`19` 号 §3.3，`37` 号 §3.4 Tab4）
 *   🔴 红牌 = 非法组合 ⇒ **阻断发布**
 *   🟡 黄牌 = 待补项（未配来源 / 孤儿锚点）⇒ 进清单，不阻断
 *   🟢 通过
 *
 * 【⚠️ 首版未覆盖的两条红牌（不是遗漏，是暂缓）】
 *   ①「锚点有、字段定义无」需要 `cert_doc_field_def` 有足够数据（当前仅 1 行）；
 *   ②「`{{Token}}` 跨 run 未归一（W9）」需要在扫描期记录 run 切分信息。
 *   两条都在后端注释里写明了，等口径落定再补 —— ⛔ 不用前端补一套假的。
 */
import { computed, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { RefreshRight, CircleCheck, WarningFilled, CircleClose, Pointer } from '@element-plus/icons-vue'
import { unwrapOk, YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import { validateTemplate } from '@share/api/workflow/doc-fill-rule'

const props = defineProps<{
  templateCode: string
  hasTemplate: boolean
}>()

/**
 * ★ 把校验结论抛给父页 —— 操作条的 `[发布]` 按钮要靠 `canPublish` 决定是否可点。
 *   ⛔ 父页不复算（口径唯一在后端），只搬运。
 */
const emit = defineEmits<{
  (
    e: 'validated',
    payload: { canPublish: boolean; blockReason: string; anchorCount: number },
  ): void
}>()

interface Violation {
  Code: string
  Level: 'error' | 'warning'
  Anchor: string
  Message: string
}

const loading = ref(false)
const errors = ref<Violation[]>([])
const warnings = ref<Violation[]>([])
const publishStatus = ref('')
const canPublish = ref(false)
const validated = ref(false)
/** 后端给出的「为什么不能发布」（无红牌时通常是「还没有锚点」） */
const blockReason = ref('')
const anchorCount = ref(0)
/** 后端返回的说明文案（「校验通过」/「发现 N 个红牌问题，不能发布」） */
const summary = ref('')

const statusMeta = computed(() => {
  const map: Record<string, { text: string; type: any }> = {
    draft: { text: '草稿（未扫描）', type: 'info' },
    scanned: { text: '已扫描（未校验/有红牌）', type: 'warning' },
    ready: { text: '校验通过，可发布', type: 'success' },
    published: { text: '已发布', type: 'success' },
  }
  return map[publishStatus.value] || { text: publishStatus.value || '—', type: 'info' }
})

/** 清空并同步告知父页「当前不可发布」 */
function reset() {
  errors.value = []
  warnings.value = []
  publishStatus.value = ''
  canPublish.value = false
  validated.value = false
  blockReason.value = ''
  anchorCount.value = 0
  emit('validated', { canPublish: false, blockReason: '', anchorCount: 0 })
}

async function refresh() {
  if (!props.templateCode) return
  loading.value = true
  try {
    const data = unwrapOk(await validateTemplate(props.templateCode), '校验失败')
    const all: Violation[] = (data?.Violations ?? []) as Violation[]
    errors.value = all.filter((v) => v.Level === 'error')
    warnings.value = all.filter((v) => v.Level === 'warning')
    publishStatus.value = data?.PublishStatus ?? ''
    canPublish.value = !!data?.CanPublish
    blockReason.value = data?.BlockReason ?? ''
    anchorCount.value = data?.AnchorCount ?? 0
    validated.value = true
    summary.value = ''
    emit('validated', {
      canPublish: canPublish.value,
      blockReason: blockReason.value,
      anchorCount: anchorCount.value,
    })
  } catch (e: any) {
    ElMessage.error(e?.message || '校验失败')
  } finally {
    loading.value = false
  }
}

/** 切模板时清空旧结果 —— 否则会把上一个模板的红牌记到新模板头上 */
watch(() => props.templateCode, reset)

defineExpose({ refresh })
</script>

<template>
  <div class="validate-tab" v-loading="loading">
    <YzhEmptyState v-if="!hasTemplate" :icon="Pointer" title="请先在左侧选择一个模板" />

    <template v-else>
      <div class="head">
        <div class="head__counts">
          <YzhStatusBadge
            v-if="errors.length"
            type="danger"
            :icon="CircleClose"
            :text="`红牌 ${errors.length}`"
          />
          <YzhStatusBadge
            v-else-if="validated"
            type="success"
            :icon="CircleCheck"
            text="无红牌"
          />
          <YzhStatusBadge
            v-if="warnings.length"
            type="warning"
            :text="`黄牌 ${warnings.length}`"
          />
        </div>
        <el-button
          type="default"
          :icon="RefreshRight"
          size="small"
          :loading="loading"
          @click="refresh"
        >
          重新校验
        </el-button>
      </div>

      <div v-if="validated" class="status-line">
        发布状态：<YzhStatusBadge :type="statusMeta.type" :text="statusMeta.text" />
        <span class="muted">
          锚点 {{ anchorCount }} 个 ·
          {{ canPublish ? '（可发布）' : `（${blockReason || '存在阻断项，不能发布'}）` }}
        </span>
      </div>

      <YzhEmptyState
        v-if="!validated"
        :icon="Pointer"
        title="尚未校验"
        description="点右上角「重新校验」开始"
      />

      <template v-else>
        <!-- 🔴 红牌 -->
        <section v-if="errors.length" class="group group--error">
          <div class="group__title">红牌 · 阻断发布</div>
          <div v-for="v in errors" :key="v.Code + v.Anchor" class="item">
            <el-icon class="item__icon item__icon--error"><CircleClose /></el-icon>
            <div class="item__body">
              <code class="mono">{{ v.Anchor || '(整模板)' }}</code>
              <span class="item__msg">{{ v.Message }}</span>
              <YzhStatusBadge type="danger" :text="v.Code" />
            </div>
          </div>
        </section>

        <!-- 🟡 黄牌 -->
        <section v-if="warnings.length" class="group group--warning">
          <div class="group__title">黄牌 · 进清单不阻断</div>
          <div v-for="v in warnings" :key="v.Code + v.Anchor" class="item">
            <el-icon class="item__icon item__icon--warning"><WarningFilled /></el-icon>
            <div class="item__body">
              <code class="mono">{{ v.Anchor || '(整模板)' }}</code>
              <span class="item__msg">{{ v.Message }}</span>
              <YzhStatusBadge type="warning" :text="v.Code" />
            </div>
          </div>
        </section>

        <!-- ★ 「0 红牌却仍不能发布」必须说清楚原因，否则与不可点的发布按钮自相矛盾 -->
        <div v-if="!errors.length && !warnings.length" class="all-pass">
          <el-icon class="all-pass__icon" :class="{ 'all-pass__icon--blocked': !canPublish }">
            <CircleCheck v-if="canPublish" />
            <WarningFilled v-else />
          </el-icon>
          <div class="all-pass__title">{{ canPublish ? '全部通过' : '无红黄牌，但还不能发布' }}</div>
          <div class="all-pass__desc">
            {{ canPublish ? '没有红牌也没有黄牌，可以发布。' : blockReason }}
          </div>
        </div>

        <div class="footnote">
          ⚠️ 首版校验<strong>尚未覆盖</strong>两条红牌：「锚点有、字段定义无」与「跨 run 未归一（W9）」
          —— 它们需要字段定义表有足够数据、以及扫描期记录 run 切分信息，属<strong>暂缓</strong>而非遗漏。
        </div>
      </template>
    </template>
  </div>
</template>

<style scoped>
.validate-tab {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
  min-height: 120px;
}

.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-2, 8px);
}
.head__counts {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  align-items: center;
}

.status-line {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1, 4px);
}

.group__title {
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  margin-bottom: var(--yzh-space-1, 4px);
}
.group--error .group__title {
  color: var(--yzh-color-danger, #dc2626);
}
.group--warning .group__title {
  color: var(--yzh-color-warning, #d97706);
}

.item {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-sm, 4px);
  border: 1px solid var(--yzh-color-border-light, #f1f5f9);
  margin-bottom: var(--yzh-space-1, 4px);
  background: var(--yzh-color-bg-container, #fff);
}
.item__icon {
  flex-shrink: 0;
  margin-top: var(--yzh-space-1, 4px);
}
.item__icon--error {
  color: var(--yzh-color-danger, #dc2626);
}
.item__icon--warning {
  color: var(--yzh-color-warning, #d97706);
}
.item__body {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-base, 1.6);
}
.item__msg {
  color: var(--yzh-color-text-regular, #606266);
}
.mono {
  font-family: var(--yzh-font-family-mono, 'Courier New', monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-primary, #303133);
}

.muted {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
}

.all-pass {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-8, 32px) 0;
}
.all-pass__icon {
  font-size: var(--yzh-font-size-3xl, 24px);
  color: var(--yzh-color-success, #16a34a);
}
.all-pass__icon--blocked {
  color: var(--yzh-color-warning, #d97706);
}
.all-pass__title {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
}
.all-pass__desc {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-secondary, #606266);
}

.footnote {
  margin-top: var(--yzh-space-1, 4px);
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.8);
  color: var(--yzh-color-text-secondary, #606266);
}
</style>
