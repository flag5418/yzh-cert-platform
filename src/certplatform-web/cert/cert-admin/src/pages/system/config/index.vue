<script setup lang="ts">
/**
 * 系统参数配置（配置驱动 CRUD 页面）
 *
 * 布局结构：
 * - 顶部：搜索栏（从 EntityConfig.SearchFields 自动派生）
 * - 工具栏：按钮由后端 EntityConfig.Toolbar 配置驱动
 * - 表格：列由后端 EntityConfig.Columns 配置驱动
 * - 操作列：行按钮由后端 EntityConfig.RowButtons 配置驱动 → 必须有 @row-action 监听
 *
 * 设计原则：
 * - 前端不硬编码任何按钮/列/字段
 * - 全部 UI 结构由后端 EntityConfig JSON 配置驱动
 * - 分页/排序/搜索条件由 YzhTable 传入 → CrudPageLogic.dataLoader（不再页面内手写 /filter）
 * - 编辑/删除复用基类逻辑（删除自带二次确认）
 */
import { YzhForm, YzhTable } from '@yzh-core'
import { ElMessage, ElTag } from 'element-plus'
import { computed, onMounted, nextTick, ref } from 'vue'
import { ConfigLogic } from './logic'

// 实例化 Logic
const logic = new ConfigLogic()

// 本地状态
const tableRef = ref()

// 行操作按钮：从 config.RowButtons 自动派生（edit/delete/toggle-valid）→ YzhTable 要求 Record 格式
const rowActionButtons = computed(() => {
  const btns: Record<string, string> = {}
  for (const b of logic.rowButtons) {
    btns[b.key] = b.text
  }
  return btns
})

// 工具栏按钮（从 config.Toolbar 自动派生）
const toolbarConfig = computed(() => {
  return (logic.config.value as any)?.Toolbar || {}
})

// ========================================================
// 表格数据加载
// ========================================================

/**
 * 数据加载：分页 / 排序 / 搜索条件全部由 YzhTable 传入，
 * 交给基类 dataLoader 处理（搜索条件的 Operator 取自 EntityConfig.SearchFields）
 */
function loadTableData(params: any) {
  return logic.dataLoader(params)
}

// ========================================================
// 操作事件
// ========================================================

/** 新增 */
function handleAdd() {
  logic.openAddDialog()
}

/** 批量删除（基类确认框 → /delete → 本地移除） */
async function handleBatchDelete() {
  await logic.confirmDelete()
}

/** 行操作：编辑 / 删除 / toggle-valid（与后端 RowButtons 的 key 对应） */
async function handleRowAction(action: string, row: any) {
  if (action === 'edit') {
    logic.openEditDialog(row)
  } else if (action === 'delete') {
    await logic.confirmDelete([row])
  } else if (action === 'toggle-valid') {
    await logic.toggleRowIsValidWithConfirm(row, { entityName: row.ConfigKey })
  }
}

/** 提交表单 */
async function handleSubmit() {
  try {
    await logic.submitForm()
  } catch (e: any) {
    ElMessage.error(e.message || '保存失败')
  }
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await logic.init()
  await nextTick()
  logic.setTableRef(tableRef.value)
})
</script>

<template>
  <div class="config-page">
    <YzhTable
      ref="tableRef"
      :columns="logic.columns as any"
      :data-loader="loadTableData"
      :search-fields="logic.searchFields as any"
      :selectable="true"
      :row-action-buttons="rowActionButtons"
      row-key="Code"
      @selection-change="logic.onSelectionChange($event)"
      @row-action="handleRowAction"
    >
      <!-- 状态列：IsValid → 启用/禁用标签 -->
      <template #column-IsValid="{ row }">
        <el-tag :type="row.IsValid === 1 ? 'success' : 'info'" size="small">
          {{ row.IsValid === 1 ? '启用' : '禁用' }}
        </el-tag>
      </template>

      <!-- 工具栏左侧：由后端 Toolbar 配置驱动 -->
      <template #toolbar-left>
        <el-button
          v-if="toolbarConfig.Add !== false"
          type="primary"
          @click="handleAdd"
          >新增</el-button
        >
        <el-button
          v-if="toolbarConfig.Delete !== false"
          type="danger"
          @click="handleBatchDelete"
          >删除</el-button
        >
      </template>
    </YzhTable>

    <!-- 新增/编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新增参数' : '编辑参数'"
      width="640px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <YzhForm
        v-model="logic.formData"
        :fields="logic.formFields as any"
        :loading="logic.submitting.value"
        :cols="logic.formLayoutCols as any"
        @submit="handleSubmit"
        @reset="logic.dialogVisible.value = false"
      />
    </el-dialog>
  </div>
</template>

<style scoped>
.config-page {
  height: 100%;
  box-sizing: border-box;
  overflow: auto;
}
</style>
