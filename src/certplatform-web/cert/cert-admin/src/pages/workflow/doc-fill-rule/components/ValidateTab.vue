<script setup lang="ts">
/**
 * Tab4 · 校验结果 (ValidateTab)
 *
 * 【★ V6 重构】
 *   1. 深度对齐 V6 原型：红牌 (error) / 黄牌 (warning) / 通过 (pass) 的视觉表现；
 *   2. 样式回归：完全使用 V6 原型的 CSS 类名与变量，消除裸 hex；
 *   3. 联动逻辑：校验结果通过 emit('validated') 回传给父组件 index.vue 驱动底部保存条。
 */
import {
  CircleCheckFilled,
  CircleCloseFilled,
  InfoFilled,
  Pointer,
  RefreshRight,
  WarningFilled,
} from '@element-plus/icons-vue'
import { validateTemplate } from '@share/api/workflow/doc-fill-rule'
import { unwrapOk, YzhEmptyState, YzhStatusBadge } from '@yzh-core'
import { ElMessage } from 'element-plus'
import { computed, ref, watch } from 'vue'

const props = defineProps<{
  templateCode: string
  hasTemplate: boolean
}>()

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
const blockReason = ref('')
const anchorCount = ref(0)
const summary = ref('')

const statusMeta = computed(() => {
  const map: Record<string, { text: string; type: any }> = {
    draft: { text: '草稿（未扫描）', type: 'info' },
    scanned: { text: '已扫描（未校验/有红牌）', type: 'warning' },
    ready: { text: '校验通过，可发布', type: 'success' },
    published: { text: '已发布', type: 'success' },
  }
  return (
    map[publishStatus.value] || {
      text: publishStatus.value || '—',
      type: 'info',
    }
  )
})

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
    const data = unwrapOk(
      await validateTemplate(props.templateCode),
      '校验失败',
    )
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

watch(() => props.templateCode, reset)

defineExpose({ refresh })
</script>

<template>
  <div class="validate-tab" v-loading="loading">
    <YzhEmptyState
      v-if="!hasTemplate"
      :icon="Pointer"
      title="请先在左侧选择一个模板"
    />

    <template v-else>
      <!-- V6 头部统计与操作 -->
      <div class="head-v6">
        <div class="head-v6__counts">
          <div v-if="errors.length" class="count-badge d">
            <el-icon><CircleCloseFilled /></el-icon>
            <span>红牌 {{ errors.length }}</span>
          </div>
          <div v-else-if="validated" class="count-badge s">
            <el-icon><CircleCheckFilled /></el-icon>
            <span>无红牌通过</span>
          </div>
          <div v-if="warnings.length" class="count-badge w">
            <el-icon><WarningFilled /></el-icon>
            <span>黄牌 {{ warnings.length }}</span>
          </div>
        </div>
        <div class="sp"></div>
        <el-button
          type="primary"
          :icon="RefreshRight"
          size="default"
          :loading="loading"
          class="validate-btn"
          @click="refresh"
        >
          {{ validated ? '重新校验' : '开始校验' }}
        </el-button>
      </div>

      <!-- V6 状态摘要卡片 -->
      <div v-if="validated" class="status-box">
        <div class="line">
          <label>发布状态</label>
          <YzhStatusBadge :type="statusMeta.type" :text="statusMeta.text" />
        </div>
        <div class="line">
          <label>校验结论</label>
          <span :class="canPublish ? 't-suc' : 't-dan'">
            {{
              canPublish
                ? '通过 · 准予发布'
                : blockReason || '存在阻断项，禁止发布'
            }}
          </span>
        </div>
      </div>

      <YzhEmptyState
        v-if="!validated"
        title="尚未校验"
        description="系统将按必需项检查规则完整性，通过后即可发布。"
      />

      <div v-else class="results-v6">
        <!-- 🔴 红牌区域 -->
        <div v-if="errors.length" class="res-group d">
          <div class="res-group__hd">
            <el-icon><CircleCloseFilled /></el-icon>
            红牌 · 阻断发布
          </div>
          <div class="res-group__bd">
            <div v-for="v in errors" :key="v.Code + v.Anchor" class="v-card d">
              <div class="v-card__hd">
                <code class="mono">{{ v.Anchor || '(整模板)' }}</code>
                <span class="code">{{ v.Code }}</span>
              </div>
              <div class="v-card__msg">{{ v.Message }}</div>
            </div>
          </div>
        </div>

        <!-- 🟡 黄牌区域 -->
        <div v-if="warnings.length" class="res-group w">
          <div class="res-group__hd">
            <el-icon><WarningFilled /></el-icon>
            黄牌 · 进清单不阻断
          </div>
          <div class="res-group__bd">
            <div
              v-for="v in warnings"
              :key="v.Code + v.Anchor"
              class="v-card w"
            >
              <div class="v-card__hd">
                <code class="mono">{{ v.Anchor || '(整模板)' }}</code>
                <span class="code">{{ v.Code }}</span>
              </div>
              <div class="v-card__msg">{{ v.Message }}</div>
            </div>
          </div>
        </div>

        <!-- 🟢 全部通过 -->
        <div v-if="!errors.length && !warnings.length" class="res-pass">
          <el-icon class="pass-ico"><CircleCheckFilled /></el-icon>
          <div class="pass-msg">
            <b>校验全部通过</b>
            <p>该模板的所有必需项配置完整，未发现逻辑冲突，可以安全发布。</p>
          </div>
        </div>

        <!-- 脚注说明 -->
        <div class="v6-footnote">
          <el-icon><InfoFilled /></el-icon>
          <span>
            首版校验尚未覆盖「锚点有、字段定义无」与「跨 run
            未归一（W9）」等暂缓口径。
          </span>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.validate-tab {
  display: flex;
  flex-direction: column;
  gap: 14px;
  min-height: 0;
}

/* V6 头部回归 */
.head-v6 {
  display: flex;
  align-items: center;
  gap: 12px;
  padding-bottom: var(--yzh-space-3, 12px);
  border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5);
}
.head-v6__counts {
  display: flex;
  gap: 8px;
}
.count-badge {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: var(--yzh-space-1, 4px) var(--yzh-space-2, 10px);
  border-radius: 4px;
  font-size: var(--yzh-font-size-xs, 12px);
  font-weight: 600;
  border: 1px solid transparent;
}
.count-badge.d {
  background: var(--yzh-color-danger-light-9, #fef0f0);
  color: var(--yzh-color-danger, #f56c6c);
  border-color: var(--yzh-color-danger-light-8, #fde2e2);
}
.count-badge.w {
  background: var(--yzh-color-warning-light-9, #fdf6ec);
  color: var(--yzh-color-warning, #e6a23c);
  border-color: var(--yzh-color-warning-light-8, #faecd8);
}
.count-badge.s {
  background: var(--yzh-color-success-light-9, #f0f9eb);
  color: var(--yzh-color-success, #67c23a);
  border-color: var(--yzh-color-success-light-8, #b3e19d);
}

.sp {
  flex: 1;
}
.validate-btn {
  font-weight: 600;
}

/* 状态摘要卡片 */
.status-box {
  background: var(--yzh-color-bg-subtle, #fafafa);
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-md, 8px);
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.line {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: var(--yzh-font-size-sm, 13px);
}
.line label {
  color: var(--yzh-color-text-placeholder, #909399);
  width: 60px;
}
.t-dan {
  color: var(--yzh-color-danger, #f56c6c);
  font-weight: 600;
}
.t-suc {
  color: var(--yzh-color-success, #67c23a);
  font-weight: 600;
}

/* 结果区域 */
.results-v6 {
  display: flex;
  flex-direction: column;
  gap: 20px;
  overflow-y: auto;
  padding-right: var(--yzh-space-1, 4px);
}
.res-group {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.res-group__hd {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 6px;
  padding-left: var(--yzh-space-1, 2px);
}
.res-group.d .res-group__hd {
  color: var(--yzh-color-danger, #f56c6c);
}
.res-group.w .res-group__hd {
  color: var(--yzh-color-warning, #e6a23c);
}

.res-group__bd {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

/* 违规卡片 V6 */
.v-card {
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
  border-radius: var(--yzh-radius-md, 8px);
  padding: var(--yzh-space-2, 10px) var(--yzh-space-3, 12px);
  background: var(--yzh-color-bg-container, #fff);
  transition: all var(--yzh-transition-base, 200ms);
}
.v-card.d {
  border-left: 3px solid var(--yzh-color-danger, #f56c6c);
}
.v-card.w {
  border-left: 3px solid var(--yzh-color-warning, #e6a23c);
}
.v-card:hover {
  /* ⛔ 只加投影，**不要**改 `border-color`：`.v-card.d/.w` 的左侧 3px 语义色描边
     与 `.v-card:hover` 特异性相同（0,2,0）且本规则在后 ⇒ 一悬停红/黄描边会被刷成灰色 */
  box-shadow: var(--yzh-shadow-sm, 0 1px 3px rgba(0, 0, 0, 0.1));
}

.v-card__hd {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--yzh-space-2, 6px);
}
.mono {
  font-family: var(
    --yzh-font-family-mono,
    ui-monospace,
    SFMono-Regular,
    Menlo,
    Consolas,
    monospace
  );
  background: var(--yzh-color-bg-subtle, #fafafa);
  padding: var(--yzh-space-1, 2px) var(--yzh-space-2, 6px);
  border-radius: 3px;
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-primary, #303133);
  border: 1px solid var(--yzh-color-border-light, #e4e7ed);
}
.code {
  font-size: var(--yzh-font-size-xs, 11px);
  color: var(--yzh-color-text-placeholder, #c0c4cc);
}
.v-card__msg {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-regular, #606266);
  line-height: 1.6;
}

/* 通过状态 */
.res-pass {
  display: flex;
  gap: 16px;
  padding: var(--yzh-space-6, 24px);
  background: var(--yzh-color-success-light-9, #f0f9eb);
  border: 1px solid var(--yzh-color-success-light-8, #b3e19d);
  border-radius: var(--yzh-radius-md, 8px);
  align-items: center;
}
.pass-ico {
  font-size: var(--yzh-font-size-3xl, 32px);
  color: var(--yzh-color-success, #67c23a);
}
.pass-msg b {
  display: block;
  font-size: var(--yzh-font-size-md, 15px);
  color: var(--yzh-color-success, #67c23a);
  margin-bottom: var(--yzh-space-1, 4px);
}
.pass-msg p {
  margin: 0;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.v6-footnote {
  display: flex;
  gap: 8px;
  padding: var(--yzh-space-3, 12px);
  background: var(--yzh-color-bg-subtle, #fafafa);
  border-radius: var(--yzh-radius-sm, 4px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-text-placeholder, #909399);
  line-height: 1.6;
  border: 1px solid var(--yzh-color-border-light, #ebeef5);
}
.v6-footnote .el-icon {
  margin-top: var(--yzh-space-1, 2px);
}
</style>
