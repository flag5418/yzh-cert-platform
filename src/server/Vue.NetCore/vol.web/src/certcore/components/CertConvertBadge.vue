<template>
  <YzhStatusBadge v-if="info" :type="info.badge" :icon="iconComponent" :text="info.label" size="small" />
</template>

<script setup>
/**
 * CertConvertBadge —— 转换状态徽标
 * 基于 YzhStatusBadge，统一 pending/converting/converted/failed 表达
 */
import { computed } from 'vue'
import { YzhStatusBadge } from '@/yzh'
import { convertStatusInfo } from '../utils/convertStatus'
import { IconLoading } from '@/yzh'

const props = defineProps({
  status: { type: String, default: '' }
})

const info = computed(() => convertStatusInfo(props.status))

const iconComponent = computed(() => {
  if (props.status === 'converting') return IconLoading
  return null // 其余状态用 YzhStatusBadge 默认语义图标
})
</script>
