<template>
  <YzhCrudV3
    entity-name="CertificationBody"
    :show-column-setting="true"
    :grid-columns-count="2"
    @ready="onReady"
    @row-click="onRowClick"
  >
    <!-- 自定义工具栏按钮 -->
    <template #btnLeft="{ selectedRow }">
      <!-- 可在此添加自定义按钮 -->
    </template>

    <!-- 自定义列渲染 -->
    <template #cell-Name="{ row, value }">
      <span style="font-weight: 500;">{{ value }}</span>
    </template>

    <!-- 自定义操作列 -->
    <template #action="{ row, index }">
      <!-- 使用默认操作列 -->
    </template>
  </YzhCrudV3>
</template>

<script setup lang="ts">
/**
 * 认证机构管理页面
 * 使用配置驱动模式：通过 entity-name="CertificationBody" 自动加载后端实体特性配置
 */
import { ref } from 'vue'
import YzhCrudV3 from '@/yzh/components/YzhCrudV3.vue'

const crudRef = ref()

function onReady(instance: any) {
  console.log('CertificationBody 页面就绪', instance)
}

function onRowClick(row: any) {
  console.log('选中行', row)
}
</script>
