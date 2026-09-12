<script setup lang="ts">
import { onMounted } from 'vue'
import { ElTable, ElTableColumn, ElButton, ElIcon } from 'element-plus'
import { Plus, Delete } from '@element-plus/icons-vue'
import IconPicker from '@/components/IconPicker.vue'
import MenuFormDialog from '@/components/MenuFormDialog.vue'
import { useMenuLogic } from './logic'
import type { SysMenu } from '@/api/system/menu'

const {
  tableData,
  loading,
  selectedRows,
  dialogVisible,
  dialogTitle,
  isEdit,
  formData,
  submitting,
  loadData,
  handleAddRoot,
  handleAddChild,
  handleEdit,
  handleDelete,
  handleBatchDelete,
  handleToggleEnable,
  handleClose,
  handleSubmit
} = useMenuLogic()

const columns = [
  { prop: 'menuName', label: '名称', minWidth: 200 },
  { prop: 'url', label: '路由', minWidth: 200 },
  { prop: 'icon', label: '图标', width: 120 },
  { prop: 'orderNo', label: '排序', width: 80 },
  { prop: 'enable', label: '状态', width: 80 },
  { prop: 'actions', label: '操作', width: 280, fixed: 'right' }
]

onMounted(() => loadData())
</script>

<template>
  <div class="menu-manage">
    <!-- 工具栏 -->
    <div class="toolbar">
      <el-button type="primary" @click="handleAddRoot">
        <el-icon><Plus /></el-icon> 新增根菜单
      </el-button>
      <el-button
        type="danger"
        :disabled="!selectedRows.length"
        @click="handleBatchDelete"
      >
        <el-icon><Delete /></el-icon> 批量删除
      </el-button>
    </div>

    <!-- 树形表格 -->
    <el-table
      v-loading="loading"
      :data="tableData"
      row-key="code"
      default-expand-all
      @selection-change="selectedRows = $event"
    >
      <el-table-column type="selection" width="50" />
      <el-table-column prop="menuName" label="名称" min-width="200" />
      <el-table-column prop="url" label="路由" min-width="200" />
      <el-table-column prop="icon" label="图标" width="120">
        <template #default="{ row }">
          <el-icon v-if="row.icon"><component :is="row.icon" /></el-icon>
          <span v-else class="text-muted">-</span>
        </template>
      </el-table-column>
      <el-table-column prop="orderNo" label="排序" width="80" />
      <el-table-column prop="enable" label="状态" width="80">
        <template #default="{ row }">
          <el-tag :type="row.enable === 1 ? 'success' : 'danger'">
            {{ row.enable === 1 ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="280" fixed="right">
        <template #default="{ row }">
          <el-button size="small" @click="handleAddChild(row)">新增下级</el-button>
          <el-button size="small" @click="handleEdit(row)">修改</el-button>
          <el-button size="small" type="danger" @click="handleDelete(row)">删除</el-button>
          <el-button
            size="small"
            :type="row.enable === 1 ? 'warning' : 'success'"
            @click="handleToggleEnable(row)"
          >
            {{ row.enable === 1 ? '禁用' : '启用' }}
          </el-button>
        </template>
      </el-table-column>
    </el-table>

    <!-- 新增/编辑对话框 -->
    <MenuFormDialog
      v-model="dialogVisible"
      :title="dialogTitle"
      :data="formData"
      :is-edit="isEdit"
      @submit="handleSubmit"
    />
  </div>
</template>

<style scoped>
.menu-manage {
  padding: 20px;
}

.toolbar {
  margin-bottom: 16px;
}

.text-muted {
  color: #999;
}
</style>
