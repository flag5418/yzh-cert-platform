<template>
  <div class="role-user-page">
    <YzhTreeTableSelector
      ref="selectorRef"
      :tree-data="treeData"
      :tree-width="260"
      :tree-searchable="true"
      :tree-lazy="true"
      :tree-load-data="loadTreeChildren"
      :table-columns="tableColumns"
      :load-table-data="loadTableData"
      :show-pagination="false"
      @update:checked-tree-nodes="handleTreeNodesChange"
      @update:checked-table-rows="handleTableRowsChange"
    >
      <template #treeFooter>
        <div class="role-user-page__tree-info">
          <p>已选 <strong>{{ checkedTreeNodes.length }}</strong> 个角色</p>
        </div>
      </template>
    </YzhTreeTableSelector>

    <!-- 底部操作栏 -->
    <div class="role-user-page__footer">
      <div class="role-user-page__summary">
        已选择 <strong>{{ checkedTableRows.length }}</strong> 个用户
      </div>
      <div class="role-user-page__actions">
        <el-button @click="handleCancel">取消</el-button>
        <el-button type="primary" @click="handleConfirm" :disabled="checkedTableRows.length === 0">
          确认选择
        </el-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
/**
 * 角色-用户选择页面（Demo）
 *
 * 使用 YzhTreeTableSelector 组件实现：
 * - 左侧：组织机构树（带 checkbox）
 * - 右侧：用户表格（带 checkbox）
 * - 勾选组织机构 → 自动加载该机构下的用户并勾选
 *
 * 注意：当前使用 Organization 树作为演示，实际角色树需要 RoleController 提供树端点
 */
import { ref, onMounted } from 'vue'
import { YzhTreeTableSelector } from '@yzh-core'
import { yzhApi } from '@yzh-core/api/client'
import type { TreeNode } from '@yzh-core/types/tree'
import type { YzhTableColumn } from '@yzh-core'
import { ElMessage } from 'element-plus'

// ========================================================
// 状态
// ========================================================

const selectorRef = ref<InstanceType<typeof YzhTreeTableSelector>>()
const treeData = ref<TreeNode[]>([])
const checkedTreeNodes = ref<TreeNode[]>([])
const checkedTableRows = ref<any[]>([])

// ========================================================
// 表格列配置
// ========================================================

const tableColumns: YzhTableColumn[] = [
  { prop: 'Code', label: '用户编码', width: 120 },
  { prop: 'UserName', label: '账号', width: 120 },
  { prop: 'UserTrueName', label: '姓名', width: 120 },
  { prop: 'OrgName', label: '所属机构', width: 150 },
  { prop: 'RoleName', label: '角色', width: 120 },
  { prop: 'PhoneNo', label: '手机号', width: 130 },
  { prop: 'EnableDesc', label: '状态', width: 80 },
]

// ========================================================
// 树数据加载
// ========================================================

async function loadTreeRoot() {
  try {
    const res = await yzhApi.post('/api/Organization/tree/root', {})
    const items = res.data ?? []
    treeData.value = items.map((dto: any) => ({
      code: dto.Code,
      name: dto.Name,
      parentCode: dto.ParentCode ?? null,
      nodeType: dto.NodeType,
      isLeaf: dto.IsLeaf,
      extra: dto.Extra ?? {},
      children: [],
    }))
  } catch (e: any) {
    ElMessage.error(e.message || '加载树数据失败')
  }
}

async function loadTreeChildren(node: any, resolve: (data: TreeNode[]) => void) {
  try {
    const res = await yzhApi.post('/api/Organization/tree/children', {
      Code: node.data.code,
    })
    const items = res.data ?? []
    const children = items.map((dto: any) => ({
      code: dto.Code,
      name: dto.Name,
      parentCode: dto.ParentCode ?? null,
      nodeType: dto.NodeType,
      isLeaf: dto.IsLeaf,
      extra: dto.Extra ?? {},
      children: [],
    }))
    resolve(children)
  } catch (e: any) {
    ElMessage.error(e.message || '加载子节点失败')
    resolve([])
  }
}

// ========================================================
// 表格数据加载
// ========================================================

async function loadTableData(treeCode: string) {
  try {
    const res = await yzhApi.post('/api/Organization/filter', {
      Page: 1,
      PageSize: 100,
      Filters: [
        { Field: 'OrgCode', Operator: 'eq', Value: treeCode },
      ],
    })
    const data = res.data
    return {
      rows: data?.Items ?? [],
      total: data?.TotalCount ?? 0,
    }
  } catch (e: any) {
    ElMessage.error(e.message || '加载用户数据失败')
    return { rows: [], total: 0 }
  }
}

// ========================================================
// 事件处理
// ========================================================

function handleTreeNodesChange(nodes: TreeNode[]) {
  checkedTreeNodes.value = nodes
}

function handleTableRowsChange(rows: any[]) {
  checkedTableRows.value = rows
}

function handleCancel() {
  selectorRef.value?.clearSelection()
  ElMessage.info('已取消选择')
}

function handleConfirm() {
  const userCodes = checkedTableRows.value.map((r) => r.Code)
  ElMessage.success(`已选择 ${userCodes.length} 个用户：${userCodes.join(', ')}`)
  // TODO: 实际业务逻辑，如保存角色-用户关联
}

// ========================================================
// 初始化
// ========================================================

onMounted(() => {
  loadTreeRoot()
})
</script>

<style scoped>
.role-user-page {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.role-user-page__tree-info {
  text-align: center;
  font-size: 13px;
  color: var(--el-text-color-secondary);
}

.role-user-page__footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: #fff;
  flex-shrink: 0;
}

.role-user-page__summary {
  font-size: 14px;
  color: var(--el-text-color-regular);
}

.role-user-page__actions {
  display: flex;
  gap: 8px;
}
</style>
