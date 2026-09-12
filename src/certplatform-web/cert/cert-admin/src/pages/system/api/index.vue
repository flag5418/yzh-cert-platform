<template>
  <div class="api-page">
    <!-- 顶部操作栏 -->
    <div class="toolbar">
      <div class="toolbar-left">
        <el-button type="primary" :loading="logic.syncing.value" @click="logic.handleSync()">
          <el-icon><Refresh /></el-icon>
          更新接口
        </el-button>
        <el-button @click="logic.openSwagger()">
          <el-icon><Link /></el-icon>
          Swagger 测试
        </el-button>
      </div>
      <div class="toolbar-right">
        <el-select
          v-model="logic.filterMethod.value"
          placeholder="请求方法"
          clearable
          style="width: 130px; margin-right: 8px"
        >
          <el-option label="GET" value="GET" />
          <el-option label="POST" value="POST" />
          <el-option label="PUT" value="PUT" />
          <el-option label="DELETE" value="DELETE" />
        </el-select>
        <el-input
          v-model="logic.searchKeyword.value"
          placeholder="搜索接口名称/路径/分组"
          clearable
          style="width: 260px"
          prefix-icon="Search"
        />
      </div>
    </div>

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

    <!-- 分组统计 -->
    <div class="group-stats">
      <el-tag
        v-for="[group, count] in logic.groupStats.value"
        :key="group"
        size="small"
        style="margin-right: 8px; margin-bottom: 4px"
      >
        {{ group }} ({{ count }})
      </el-tag>
    </div>

    <!-- 接口列表 -->
    <el-table
      :data="logic.filteredApis.value"
      v-loading="logic.loading.value"
      stripe
      border
      style="width: 100%"
      :default-sort="{ prop: 'GroupPath', order: 'ascending' }"
    >
      <el-table-column prop="GroupPath" label="分组" width="180" sortable>
        <template #default="{ row }">
          <el-tag size="small" type="info">{{ row.GroupPath }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="Method" label="方法" width="90" sortable>
        <template #default="{ row }">
          <el-tag :type="methodTagType(row.Method)" size="small">
            {{ row.Method }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="Path" label="路径" min-width="250" show-overflow-tooltip />
      <el-table-column prop="Name" label="接口名称" min-width="200" show-overflow-tooltip />
      <el-table-column prop="Author" label="负责人" width="100">
        <template #default="{ row }">
          {{ row.Author || '-' }}
        </template>
      </el-table-column>
      <el-table-column prop="Enable" label="状态" width="80">
        <template #default="{ row }">
          <el-tag :type="row.Enable ? 'success' : 'danger'" size="small">
            {{ row.Enable ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
    </el-table>

    <!-- 底部统计 -->
    <div class="footer-stats">
      <span>共 {{ logic.filteredApis.value.length }} 个接口</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { Refresh, Link } from '@element-plus/icons-vue'
import { ApiLogic } from './logic'

const logic = new ApiLogic()

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

onMounted(async () => {
  await logic.loadApiList()
})
</script>

<style scoped>
.api-page {
  height: 100%;
  padding: 16px;
  box-sizing: border-box;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  flex-shrink: 0;
}

.toolbar-left {
  display: flex;
  gap: 8px;
}

.toolbar-right {
  display: flex;
  align-items: center;
}

.group-stats {
  margin-bottom: 12px;
  flex-shrink: 0;
}

.footer-stats {
  margin-top: 12px;
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
