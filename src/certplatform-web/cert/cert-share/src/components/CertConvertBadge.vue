<template>
  <span class="cert-convert-badge" :class="badgeClass">
    {{ label }}
  </span>
</template>

<script setup lang="ts">
/**
 * 转换状态徽标（纯展示）
 *
 * ★ 2026-09-26 双产物链改造：本组件原先自带一份 4 值状态字典（pending/converting/completed/failed），
 *   后端新增 `unsupported`（图片/扫描件等能力边界，需人工填写）后**静默回落到「待转换」**——
 *   页面看起来正常，实际把「需人工填写」误标成「待转换」，用户会一直等一个永远不会发生的转换。
 *   ⇒ 文案统一委托给共享字典 `convertStatusLabel()`，本文件**只保留 CSS class 映射**。
 *   ⛔ 不要在本文件再新增任何 label 字典；后端加状态值只改 `utils/convertStatus.ts`。
 */
import { computed } from 'vue'
import { convertStatusLabel } from '../utils/convertStatus'

const props = defineProps({
  status: { type: String, default: '' }
})

/** 状态 → CSS class（仅样式，不含文案） */
const CLASS_MAP: Record<string, string> = {
  none: 'is-none',
  pending: 'is-pending',
  converting: 'is-converting',
  completed: 'is-completed',
  failed: 'is-failed',
  unsupported: 'is-unsupported'
}

const key = computed(() => String(props.status || '').toLowerCase())
const badgeClass = computed(() => `cert-convert-badge ${CLASS_MAP[key.value] || 'is-none'}`)
/** 未知状态原样透出（便于发现后端新增值未同步字典），与共享字典行为一致 */
const label = computed(() => convertStatusLabel(key.value) || '未知状态')
</script>

<style scoped>
.cert-convert-badge {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 11px;
  padding: 2px 6px;
  border-radius: 3px;
  white-space: nowrap;
}

.cert-convert-badge.is-none { color: var(--yzh-color-text-tertiary, #909399); background: var(--yzh-color-bg-muted, #f4f4f5); }
.cert-convert-badge.is-pending { color: var(--yzh-color-text-tertiary, #909399); background: var(--yzh-color-bg-muted, #f4f4f5); }
.cert-convert-badge.is-converting { color: var(--yzh-color-primary, #409eff); background: var(--el-color-primary-light-9, #ecf5ff); }
.cert-convert-badge.is-completed { color: var(--yzh-color-success, #67c23a); background: var(--yzh-color-success-light-9, #f0f9eb); }
.cert-convert-badge.is-failed { color: var(--yzh-color-danger, #f56c6c); background: var(--yzh-color-danger-light-9, #fef0f0); }
/* ★ unsupported = 能力边界（非故障）：橙色，提示「需人工填写」，与 failed 的红色区分开 */
.cert-convert-badge.is-unsupported { color: var(--yzh-color-warning, #e6a23c); background: var(--yzh-color-warning-light-9, #fdf6ec); }
</style>
