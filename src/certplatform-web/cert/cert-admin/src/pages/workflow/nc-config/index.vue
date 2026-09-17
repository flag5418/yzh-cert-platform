<script setup lang="ts">
/**
 * NC 规则设计（左树右表）
 *
 * 布局：
 * - 左侧：组织 → 标准 → 阶段 树（简化版，不含文件夹）
 * - 右侧：NC 检查规则表格 + 编辑弹窗
 */
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { Plus, RefreshRight, Search, FolderOpened, Document, Calendar } from '@element-plus/icons-vue'
import { getOrganizationTree } from '@share/composables/useDirectoryApi'
import { NCConfigLogic } from './logic'
import type { TreeNode } from '@share/composables/useFileTree'

// ──── 实例化 ────
const router = useRouter()
const logic = new NCConfigLogic()

// ──── 树状态（简化版：只加载 org→standard→stage，不含文件夹） ────
const fileTreeData = ref<TreeNode[]>([])
const treeLoading = ref(false)
const treeFilter = ref('')

/** 加载组织树（仅 org→standard→stage，不预加载文件夹） */
async function loadTree() {
  treeLoading.value = true
  try {
    const orgTree = await getOrganizationTree()
    fileTreeData.value = (orgTree || []).map((org: any) => ({
      id: org.id,
      name: org.label || org.name,
      type: 'organization' as const,
      orgCode: org.cbCode,
      children: (org.children || []).map((std: any) => ({
        id: std.id,
        name: std.label || std.name,
        type: 'standard' as const,
        orgCode: std.cbCode,
        stdCode: std.stdCode,
        standardCode: std.standardCode,
        children: (std.children || []).map((phase: any) => ({
          id: phase.id,
          name: phase.label || phase.name,
          type: 'stage' as const,
          orgCode: phase.cbCode,
          stdCode: phase.stdCode,
          standardCode: phase.standardCode,
          phaseCode: phase.phaseCode,
          phaseDefinitionCode: phase.phaseDefinitionCode || '',
          children: undefined,
        }))
      }))
    }))
  } catch (e: any) {
    console.error('加载目录树失败', e)
    fileTreeData.value = []
  } finally {
    treeLoading.value = false
  }
}

// ========================================================
// 树操作
// ========================================================

async function handleNodeClick(data: TreeNode) {
  await logic.handleNodeClick(data)
}

function filterTreeNode(value: string, data: TreeNode) {
  if (!value) return true
  return data.name.toLowerCase().includes(value.toLowerCase())
}

/** 条款树筛选：按编号/标题匹配（含英文标题） */
function filterClauseNode(value: string, data: any) {
  if (!value) return true
  const v = value.toLowerCase()
  return (
    (data.ClauseNumber || '').toLowerCase().includes(v) ||
    (data.Title || '').toLowerCase().includes(v) ||
    (data.Label || '').toLowerCase().includes(v)
  )
}

// ========================================================
// 弹窗提交
// ========================================================

async function handleSubmit() {
  await logic.handleSubmit()
}

onMounted(() => {
  loadTree()
})
</script>

<template>
  <div class="nc-config-page">
    <!-- 左侧：树 -->
    <div class="nc-config-page__tree">
      <div class="tree-header">
        <span class="tree-title">组织 → 标准 → 阶段</span>
      </div>
      <el-input
        v-model="treeFilter"
        placeholder="搜索节点"
        clearable
        class="tree-search"
      >
        <template #prefix>
          <el-icon><Search /></el-icon>
        </template>
      </el-input>
      <el-tree
        :data="fileTreeData"
        v-loading="treeLoading"
        node-key="id"
        default-expand-all
        highlight-current
        :expand-on-click-node="false"
        :filter-node-method="(value: string, data: TreeNode) => filterTreeNode(value, data)"
        @node-click="handleNodeClick"
      >
        <template #default="{ data }">
          <span class="tree-node">
            <el-icon v-if="data.type === 'organization'" class="node-icon"><FolderOpened /></el-icon>
            <el-icon v-else-if="data.type === 'standard'" class="node-icon"><Document /></el-icon>
            <el-icon v-else class="node-icon"><Calendar /></el-icon>
            <span class="node-label">{{ data.name }}</span>
          </span>
        </template>
      </el-tree>
    </div>

    <!-- 右侧：表格 -->
    <div class="nc-config-page__content">
      <!-- 未选中阶段 -->
      <el-empty v-if="!logic.selectedPhase.value" description="请在左侧选择阶段" :image-size="120" />

      <!-- 已选中阶段 -->
      <template v-else>
        <!-- 筛选栏 -->
        <div class="content-filter">
          <el-input
            v-model="logic.keyword.value"
            placeholder="搜索规则名称"
            clearable
            style="width: 200px"
            @keyup.enter="logic.handleSearch()"
          />
          <el-button type="primary" :icon="Search" @click="logic.handleSearch()">查询</el-button>
          <el-button @click="logic.handleReset()">重置</el-button>
        </div>

        <!-- 工具栏 -->
        <div class="content-toolbar">
          <el-button type="primary" :icon="Plus" @click="logic.openAddDialog()">
            新建检查项
          </el-button>
          <el-button :icon="RefreshRight" @click="logic.loadTable()">
            刷新
          </el-button>
          <el-button type="success" plain @click="router.push('/business/nc-config')">
            工作流设计器
          </el-button>
        </div>

        <!-- 表格 -->
        <el-table
          :data="logic.tableData.value"
          stripe
          border
          v-loading="logic.loading.value"
          row-key="Code"
          style="flex: 1"
        >
          <el-table-column prop="RuleCode" label="规则编号" width="150" />
          <el-table-column prop="RuleName" label="规则名称" min-width="200" />
          <el-table-column prop="RuleNameEn" label="英文名称" width="150" show-overflow-tooltip />
          <el-table-column label="关联条款" min-width="220">
            <template #default="{ row }">
              <div v-if="row.ClauseNumber" class="clause-cell">
                <el-tag size="small" effect="plain" class="clause-tag">{{ row.ClauseNumber }}</el-tag>
                <span class="clause-title">{{ row.ClauseTitle }}</span>
              </div>
              <span v-else class="empty-text">-</span>
            </template>
          </el-table-column>
          <el-table-column label="启用" width="80" align="center">
            <template #default="{ row }">
              <el-switch
                :model-value="row.IsActive"
                @change="logic.handleToggleActive(row)"
              />
            </template>
          </el-table-column>
          <el-table-column prop="Remark" label="备注" min-width="150" show-overflow-tooltip />
          <el-table-column label="操作" width="150" fixed="right">
            <template #default="{ row }">
              <el-button link type="primary" size="small" @click="logic.openEditDialog(row)">编辑</el-button>
              <el-button link type="primary" size="small" @click="logic.handleCopy(row)">复制</el-button>
              <el-button link type="danger" size="small" @click="logic.handleDelete(row)">删除</el-button>
            </template>
          </el-table-column>
        </el-table>

        <!-- 分页 -->
        <el-pagination
          v-model:current-page="logic.page.value"
          :page-size="logic.pageSize.value"
          :total="logic.total.value"
          layout="total, prev, pager, next"
          style="justify-content: flex-end"
          @current-change="logic.handlePageChange"
        />
      </template>
    </div>

    <!-- 编辑弹窗 -->
    <el-dialog
      v-model="logic.dialogVisible.value"
      :title="logic.dialogMode.value === 'add' ? '新建检查项' : '编辑检查项'"
      width="600px"
      :close-on-click-modal="false"
      destroy-on-close
    >
      <el-form :model="logic.formData" label-width="100px">
        <el-form-item label="规则名称" required>
          <el-input v-model="logic.formData.RuleName" placeholder="如：资源提供检查" />
        </el-form-item>
        <el-form-item label="英文名称">
          <el-input v-model="logic.formData.RuleNameEn" placeholder="如：Resource Provision" />
        </el-form-item>
        <el-form-item label="关联条款" required>
          <el-tree-select
            v-model="logic.formData.ClauseCode"
            :data="logic.clauseTreeData.value"
            node-key="Code"
            :props="{ label: 'Label', value: 'Code', children: 'Children' }"
            filterable
            check-strictly
            :filter-node-method="filterClauseNode"
            placeholder="选择关联条款"
            style="width: 100%"
            :loading="logic.clauseLoading.value"
          >
            <template #default="{ data }">
              <span>{{ data.Label || `${data.ClauseNumber} ${data.Title}` }}</span>
            </template>
          </el-tree-select>
        </el-form-item>
        <el-form-item label="是否启用">
          <el-switch v-model="logic.formData.IsActive" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="logic.formData.Remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="logic.dialogVisible.value = false">取消</el-button>
        <el-button type="primary" @click="handleSubmit" :loading="logic.submitting.value">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.nc-config-page {
  height: 100%;
  display: flex;
  gap: 16px;
}

.nc-config-page__tree {
  width: 300px;
  flex-shrink: 0;
  background: #fff;
  border-radius: 4px;
  border: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.tree-header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.tree-title {
  font-weight: 600;
  font-size: 14px;
}

.tree-search {
  padding: 8px 12px;
}

.nc-config-page__tree :deep(.el-tree) {
  flex: 1;
  overflow-y: auto;
  padding: 4px 0;
}

.tree-node {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
}

.node-icon {
  color: #909399;
  font-size: 14px;
}

.node-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.nc-config-page__content {
  flex: 1;
  min-width: 0;
  background: #fff;
  border-radius: 4px;
  border: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  padding: 16px;
  overflow: hidden;
}

.content-filter {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.content-toolbar {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.clause-cell {
  display: flex;
  align-items: center;
  gap: 8px;
}

.clause-tag {
  flex-shrink: 0;
  font-weight: 700;
  background: #f1f5f9;
  border: none;
  color: #475569;
}

.clause-title {
  font-size: 14px;
  color: #64748b;
  line-height: 1.4;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.empty-text {
  color: #909399;
}
</style>
