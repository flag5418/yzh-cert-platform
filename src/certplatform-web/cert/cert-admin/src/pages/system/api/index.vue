<template>
  <div class="api-page">
    <!-- 顶部操作栏 -->
    <div class="toolbar">
      <div class="toolbar-left">
        <el-button type="primary" :loading="logic.syncing.value" @click="logic.handleSync()">
          <el-icon><Refresh /></el-icon>
          更新接口
        </el-button>
        <el-button @click="logic.openSwagger()" title="打开 Swagger 首页">
          <el-icon><Link /></el-icon>
          Swagger 测试
        </el-button>
        <el-button @click="logic.toggleExpandAll()">
          {{ logic.expandAll.value ? '折叠全部' : '展开全部' }}
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

    <!-- 接口列表：按 模块 → 控制器 → 接口 分层的树形表格 -->
    <el-table
      :key="logic.tableKey.value"
      :data="logic.treeRows.value"
      v-loading="logic.loading.value"
      row-key="Code"
      :tree-props="{ children: 'children' }"
      :default-expand-all="logic.expandAll.value"
      stripe
      border
      style="width: 100%"
    >
      <el-table-column label="分组 / 接口名称" min-width="300">
        <template #default="{ row }">
          <template v-if="row.NodeType === 'group'">
            <el-tag size="small" type="info" effect="plain">{{ row.Name }}</el-tag>
            <span class="api-page__group-count">{{ row.ApiCount }} 个接口</span>
          </template>
          <span v-else>{{ row.Name }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="Method" label="方法" width="100">
        <template #default="{ row }">
          <el-tag v-if="row.Method" :type="methodTagType(row.Method)" size="small">
            {{ row.Method }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="Path" label="路径" min-width="300" show-overflow-tooltip>
        <template #default="{ row }">
          {{ row.Path || '-' }}
        </template>
      </el-table-column>
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag v-if="row.NodeType === 'api'" :type="row.Enable ? 'success' : 'danger'" size="small">
            {{ row.Enable ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="100" fixed="right">
        <template #default="{ row }">
          <el-button
            v-if="row.NodeType === 'api'"
            link
            type="primary"
            title="在 Swagger 中打开该接口"
            @click="logic.openSwagger(row)"
          >
            测试
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 底部统计 -->
    <div class="footer-stats">
      <span>
        共 {{ logic.groupCount.value }} 个接口 / {{ logic.filteredApis.value.length }} 个匹配
      </span>
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

.api-page__group-count {
  margin-left: 8px;
  font-size: 12px;
  color: #909399;
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
