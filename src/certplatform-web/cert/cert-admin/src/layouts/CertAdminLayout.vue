<template>
  <YzhAppLayout v-bind="$props">
    <template #header-right>
      <MessageBell />
    </template>
  </YzhAppLayout>
</template>

<script setup lang="ts">
import YzhAppLayout from '@yzh-core/layouts/YzhAppLayout.vue'
import MessageBell from '@share/components/MessageBell/MessageBell.vue'

defineProps<{
  logoText?: string
  appTitle?: string
  menuTag?: string
}>()
</script>
