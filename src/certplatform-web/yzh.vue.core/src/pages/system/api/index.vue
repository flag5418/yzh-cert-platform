<template>
  <div class="api-page">
    <!-- 同步结果提示 -->
    <el-alert
      v-if="logic.syncResult.value"
      type="success"
      :closable="true"
      @close="logic.syncResult.value = null"
      style="margin-bottom: 12px"
    >
      <template #title>
        同步完成：新增 {{ logic.syncResult.value.Added }} 个，更新
        {{ logic.syncResult.value.Updated }} 个，删除 {{ logic.syncResult.value.Deleted }} 个，
        当前共 {{ logic.syncResult.value.Total }} 个接口
      </template>
    </el-alert>

    <!-- 接口树表：分组节点 + 接口节点（配置驱动列/搜索/工具栏） -->
    <YzhTreeTable
      ref="tableRef"
      :columns="logic.columns"
      :data-loader="logic.dataLoader.bind(logic)"
      :search-fields="logic.searchFields"
      :toolbar-actions="logic.toolbarActions"
      :row-action-buttons="logic.rowActions"
      :search-max-fields="4"
      row-key="Code"
      default-expand-all
      @row-action="logic.onRowAction"
      @toolbar-action="logic.onToolbarAction"
    >
      <!-- 名称：分组 = 标签 + 计数；接口 = 名称 -->
      <template #column-Name="{ row }">
        <template v-if="row.NodeType === 'group'">
          <el-tag size="small" type="info" effect="plain">{{ row.Name }}</el-tag>
          <span class="api-page__group-count">{{ row.ApiCount }} 个接口</span>
        </template>
        <span v-else>{{ row.Name }}</span>
      </template>

      <!-- 方法：按 HTTP method 上色 -->
      <template #column-Method="{ row }">
        <el-tag v-if="row.Method" :type="methodTagType(row.Method)" size="small">
          {{ row.Method }}
        </el-tag>
      </template>

      <!-- 状态（EnableField="IsValid" 自动 slot） -->
      <template #column-IsValid="{ row }">
        <el-tag v-if="row.NodeType === 'api'" :type="row.IsValid ? 'success' : 'danger'" size="small">
          {{ row.IsValid ? '启用' : '禁用' }}
        </el-tag>
      </template>
    </YzhTreeTable>

    <!-- 底部统计 -->
    <div class="footer-stats">
      <span>共 {{ logic.apiList.value.length }} 个接口 / {{ logic.filteredCount.value }} 个匹配</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useSingleTable, YzhTreeTable } from '@yzh-core'
import { ApiPageLogic } from './logic'

const { logic, tableRef } = useSingleTable(ApiPageLogic)

function methodTagType(method: string): string {
  switch (method) {
    case 'GET':
      return 'success'
    case 'POST':
      return 'primary'
    case 'PUT':
      return 'warning'
    case 'DELETE':
      return 'danger'
    default:
      return 'info'
  }
}
</script>

<style scoped>
.api-page {
  height: 100%;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: #fff;
  padding: 16px;
}

.api-page__group-count {
  margin-left: 8px;
  font-size: 12px;
  color: #909399;
}

.footer-stats {
  padding-top: 12px;
  color: #909399;
  font-size: 13px;
  text-align: right;
  flex-shrink: 0;
}

:deep(.el-table) {
  flex: 1;
  min-height: 0;
}
</style>
