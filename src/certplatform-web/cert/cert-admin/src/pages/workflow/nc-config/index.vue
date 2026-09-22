<script setup lang="ts">
/**
 * NC 规则管理 — 左树右表（YZH 标准架构）
 *
 * 布局：
 * - 左侧：组织 → 标准 → 阶段 树（useFileTree composable）
 * - 右侧：NC 检查规则表格（YzhTable + YzhFormDialog + SingleTableCore）
 *
 * 架构：
 * - Logic 继承 SingleTableCore，自动从后端 EntityConfig 获取 columns / formFields / searchFields
 * - 选中阶段节点后，自动联动过滤 OrgCode + StandardCode + PhaseCode
 * - 行动作（编辑/删除/启停/复制）由内核 dispatch 派发，页面无手写 handler
 */
import { ref, onMounted, onUnmounted, watch, computed } from 'vue'
import { YzhTable, YzhFormDialog } from '@yzh-core'
import { Plus, RefreshRight, Search, FolderOpened, Document, Calendar } from '@element-plus/icons-vue'
import { NCConfigLogic } from './logic'
import { useFileTree, type TreeNode } from '@share/composables/useFileTree'

// ──── 实例化 Logic ────
const logic = new NCConfigLogic()

// ──── 表格引用（用于树节点切换后刷新） ────
const tableRef = ref()

// ──── 左树 ────
const {
  fileTreeData: treeData,
  loading: treeLoading,
  loadTree,
} = useFileTree()

// ──── 搜索栏占位（标准 TreeTable 可空白，由 YzhTable 接管搜索） ────
const treeFilter = ref('')

/**
 * 过滤树：只保留 organization → standard → stage 3 层
 * （使用 useFileTree 会预加载 folder/file 的占位节点，需要过滤掉）
 */
const filteredTreeData = computed(() => {
  function strip(node: TreeNode): TreeNode | null {
    if (node.Type === 'folder' || node.Type === 'file') return null
    if (!node.Children) return node
    const filteredChildren = node.Children.map(strip).filter(Boolean) as TreeNode[]
    return { ...node, Children: filteredChildren }
  }
  return treeData.value.map(strip).filter(Boolean) as TreeNode[]
})

// ========================================================
// 树→表格联动
// ========================================================

/** 树节点点击 → 注入联动过滤 → YzhTable 自动刷新 */
async function onTreeNodeClick(node: TreeNode) {
  if (node.Type !== 'stage') {
    // 非阶段节点：清空表格
    logic.setTreeFilter('', '', '')
    return
  }
  logic.setTreeFilter(
    node.OrgCode || '',
    node.StdCode || '',
    node.PhaseCode || '',
  )
  // 触发 YzhTable 刷新
  await tableRef.value?.refresh()
}

// ========================================================
// 初始化
// ========================================================

onMounted(async () => {
  await loadTree()
  await logic.init()
  if (tableRef.value) {
    logic.setTableRef(tableRef.value)
  }
  // 直接操作父容器：去掉 padding，改为 flex 列布局，使内容撑满视口
  const elMain = document.querySelector('.admin-layout__content') as HTMLElement | null
  if (elMain) {
    elMain.style.padding = '0'
    elMain.style.display = 'flex'
    elMain.style.flexDirection = 'column'
    elMain.style.overflow = 'hidden'
    elMain.style.background = '#fff'
  }
})

onUnmounted(() => {
  // 离开页面时恢复父容器原始样式（keep-alive 下不影响下次进入，但保险起见）
  const elMain = document.querySelector('.admin-layout__content') as HTMLElement | null
  if (elMain) {
    elMain.style.padding = ''
    elMain.style.display = ''
    elMain.style.flexDirection = ''
    elMain.style.overflow = ''
    elMain.style.background = ''
  }
})

// 监听 tableRef 注入
watch(tableRef, (el) => {
  if (el) logic.setTableRef(el)
})
</script>

<template>
  <div class="nc-config-page">
    <!-- 左侧：组织 → 标准 → 阶段 树 -->
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
        :data="filteredTreeData"
        v-loading="treeLoading"
        node-key="Code"
        default-expand-all
        highlight-current
        :expand-on-click-node="false"
        :filter-node-method="(value: string, data: TreeNode) => !value || data.Name.toLowerCase().includes(value.toLowerCase())"
        @node-click="onTreeNodeClick"
      >
        <template #default="{ data }">
          <span class="tree-node">
            <el-icon v-if="data.Type === 'organization'" class="node-icon"><FolderOpened /></el-icon>
            <el-icon v-else-if="data.Type === 'standard'" class="node-icon"><Document /></el-icon>
            <el-icon v-else class="node-icon"><Calendar /></el-icon>
            <span class="node-label">{{ data.Name }}</span>
          </span>
        </template>
      </el-tree>
    </div>

    <!-- 右侧：NC 检查规则表格 -->
    <div class="nc-config-page__content">
      <!-- 未选中阶段 -->
      <el-empty v-if="!logic.anySelected && !logic.loading.value" description="请在左侧选择阶段" :image-size="120" />

      <!-- 已选中阶段 → YzhTable 配置驱动 -->
      <YzhTable
        v-else
        ref="tableRef"
        :columns="logic.columns"
        :data-loader="logic.dataLoader.bind(logic)"
        :search-fields="logic.searchFields"
        :row-action-buttons="logic.rowActions"
        row-key="Code"
        @row-action="logic.onRowAction"
      >
        <!-- 工具栏左侧：新建检查项 + 刷新 -->
        <template #toolbar-left>
          <el-button type="primary" :icon="Plus" @click="logic.onToolbarAction('add')">新建检查项</el-button>
          <el-button :icon="RefreshRight" @click="tableRef?.refresh()">刷新</el-button>
        </template>
      </YzhTable>
    </div>

    <!-- 编辑弹窗 -->
    <YzhFormDialog
      v-model:visible="logic.dialogVisible.value"
      v-model="logic.formData"
      :mode="logic.dialogMode.value"
      entity-name="检查项"
      :title="logic.dialogMode.value === 'add' ? '新建检查项' : '编辑检查项'"
      :fields="logic.formFields"
      :loading="logic.submitting.value"
      :cols="2"
      width="640px"
      @submit="logic.submitForm()"
    />
  </div>
</template>

<style scoped>
:deep(.admin-layout__content) {
  padding: 0;
  background: #fff;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.nc-config-page {
  display: flex;
  flex: 1;
  min-height: 0;
  overflow: hidden;
  background: #fff;
  padding: 0;
}

/* ──── 左树 ──── */
.nc-config-page__tree {
  width: 280px;
  flex-shrink: 0;
  border-right: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: #fff;
}

.tree-header {
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
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

/* ──── 右内容区（YzhTable） ──── */
.nc-config-page__content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: #fff;
  padding: 0;
}

/* ──── YzhTable 内部布局调整 ──── */
.nc-config-page__content :deep(.yzh-table) {
  height: 100%;
}

.nc-config-page__content :deep(.el-table__inner-wrapper) {
  --el-table-header-bg-color: #f8fafc;
}

.nc-config-page__content :deep(.el-pagination) {
  padding: 12px 16px;
  justify-content: flex-end;
}
</style>
