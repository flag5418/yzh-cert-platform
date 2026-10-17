<template>
  <Transition name="yzh-pending">
    <div v-if="items.length > 0" class="yzh-pending" role="status" aria-live="polite">
      <div class="yzh-pending__panel">
        <el-icon class="yzh-pending__icon is-loading"><Loading /></el-icon>
        <p class="yzh-pending__label">{{ items[0].label }}</p>
        <p v-if="items.length > 1" class="yzh-pending__more">
          另有 {{ items.length - 1 }} 项任务进行中
        </p>
        <el-progress
          v-if="progress !== undefined"
          :percentage="progress"
          :stroke-width="6"
          :show-text="false"
          class="yzh-pending__progress"
        />
      </div>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { Loading } from '@element-plus/icons-vue'
import { usePendingByScope } from '../../composables/useGlobalPending'

/**
 * 全局等待遮罩 —— 挂在**内容区**（⛔ 不盖侧边栏 / 顶栏 / 标签栏）
 *
 * 【为什么不锁界面】同步 AI 分析要 15~25 秒；若遮住整屏，用户什么都干不了。
 * 遮罩只覆盖内容区 ⇒ 期间**仍可切换菜单与标签**（用户 2026-10-16 裁决①）。
 *
 * 【只显示当前页面】`scope === route.path`：
 *   在 A 页发起、切到 B 页 ⇒ B 页无等待 ⇒ ⛔ 不显示（不打扰）；
 *   切回 A 页 ⇒ 等待仍在 ⇒ 又显示（页面状态由 `keep-alive` 保留）。
 *
 * 【为什么在 layouts/components/ 而非 components/layout/】
 *   本组件依赖 `vue-router`（读 route.path），属**布局内部件**；
 *   守卫 R1 要求 `components/` 下原子组件零领域依赖 ⇒ 与 `YzhMenuNode.vue` 同处。
 */
const route = useRoute()
const items = usePendingByScope(() => route.path)

/** 只有「恰好 1 项且有进度」才画进度条 —— 多项时单一进度无意义 */
const progress = computed(() =>
  items.value.length === 1 ? items.value[0].progress : undefined,
)
</script>

<style scoped>
.yzh-pending {
  position: absolute;
  inset: 0;
  z-index: 2000;
  display: flex;
  align-items: center;
  justify-content: center;
}

/* 半透明底：用伪元素承载，⛔ 不影响面板本身的不透明度（避免文字发虚） */
.yzh-pending::before {
  content: '';
  position: absolute;
  inset: 0;
  background: var(--yzh-color-bg-page);
  opacity: 0.86;
}

.yzh-pending__panel {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: var(--yzh-space-3);
  min-width: 240px;
  padding: var(--yzh-space-6) var(--yzh-space-8);
  background: var(--yzh-color-bg-card);
  border: 1px solid var(--yzh-color-border-light);
  border-radius: var(--yzh-radius-lg);
  box-shadow: var(--yzh-shadow-lg);
}

.yzh-pending__icon {
  font-size: var(--yzh-font-size-2xl);
  color: var(--yzh-color-primary);
}

.yzh-pending__label {
  margin: 0;
  font-size: var(--yzh-font-size-md);
  font-weight: var(--yzh-font-weight-medium);
  color: var(--yzh-color-text-primary);
  text-align: center;
}

.yzh-pending__more {
  margin: 0;
  font-size: var(--yzh-font-size-sm);
  color: var(--yzh-color-text-muted);
}

.yzh-pending__progress {
  width: 100%;
}

.yzh-pending-enter-active,
.yzh-pending-leave-active {
  transition: opacity var(--yzh-transition-fast);
}

.yzh-pending-enter-from,
.yzh-pending-leave-to {
  opacity: 0;
}
</style>
