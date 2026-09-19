<script setup lang="ts">
/**
 * 机构-阶段关联管理（勾选分配模式）
 *
 * 左树：认证机构（扁平）
 * 右表：所有有效认证阶段，checkbox 勾选即关联
 */
import { ref, onMounted, watch, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { certOrgStageApi } from '@share/api/cert/cert-org-stage'
import type { CertOrgStageItem } from '@share/types/cert'

// ──── 左树 ────
interface TreeNode {
  Code: string
  Name: string
  Children?: TreeNode[]
}
const treeData = ref<TreeNode[]>([])
const selectedOrgCode = ref<string>('')
const treeLoading = ref(false)

// ──── 右表 ────
const tableData = ref<CertOrgStageItem[]>([])
const tableLoading = ref(false)
const searchKey = ref('')
const tableRef = ref()

// ──── 过滤后的表格数据 ────
const filteredData = ref<CertOrgStageItem[]>([])

// ──── 抑制 selection-change 事件（loadTable 初始化勾选时） ────
let suppressSelectionChange = false

watch([tableData, searchKey], () => {
  const key = searchKey.value.toLowerCase()
  if (!key) {
    filteredData.value = tableData.value
  } else {
    filteredData.value = tableData.value.filter(
      (item) =>
        item.PhaseCode.toLowerCase().includes(key) ||
        item.PhaseName.toLowerCase().includes(key),
    )
  }
}, { immediate: true })

// ========================================================
// 左树
// ========================================================

async function loadTree() {
  treeLoading.value = true
  try {
    const res = await certOrgStageApi.treeRoot()
    treeData.value = (res.data || []).map((item: any) => ({
      Code: item.Code,
      Name: item.Name,
    }))
  } finally {
    treeLoading.value = false
  }
}

async function handleNodeClick(node: TreeNode) {
  selectedOrgCode.value = node.Code
  await loadTable(node.Code)
}

// ========================================================
// 右表
// ========================================================

async function loadTable(orgCode: string) {
  tableLoading.value = true
  suppressSelectionChange = true
  try {
    // 先清空勾选
    tableRef.value?.clearSelection()

    const res = await certOrgStageApi.list(orgCode)
    tableData.value = res.data || []

    // 等 DOM 更新后勾选已关联的行
    await nextTick()
    for (const item of tableData.value) {
      if (item.Linked) {
        tableRef.value?.toggleRowSelection(item, true)
      }
    }
  } finally {
    suppressSelectionChange = false
    tableLoading.value = false
  }
}

/** checkbox 勾选/取消 → 立即保存 */
function handleSelectionChange(selection: CertOrgStageItem[]) {
  if (suppressSelectionChange) return
  if (!selectedOrgCode.value) return

  const currentCodes = new Set(selection.map((s) => s.Code))
  const prevCodes = new Set(tableData.value.filter((t) => t.Linked).map((t) => t.Code))

  // 新勾选
  for (const item of selection) {
    if (!prevCodes.has(item.Code)) {
      certOrgStageApi.save({
        OrgCode: selectedOrgCode.value,
        StageCode: item.PhaseCode,
        Linked: true,
      }).catch(() => ElMessage.error(`关联阶段【${item.PhaseName}】失败`))
    }
  }

  // 取消勾选
  for (const item of tableData.value) {
    if (prevCodes.has(item.Code) && !currentCodes.has(item.Code)) {
      certOrgStageApi.save({
        OrgCode: selectedOrgCode.value,
        StageCode: item.PhaseCode,
        Linked: false,
      }).catch(() => ElMessage.error(`取消关联【${item.PhaseName}】失败`))
    }
  }

  // 更新本地 Linked 状态
  tableData.value.forEach((item) => {
    item.Linked = currentCodes.has(item.Code)
  })
}

// ========================================================
// 初始化
// ========================================================

onMounted(() => {
  loadTree()
})
</script>

<template>
  <div class="link-page">
    <!-- 左树 -->
    <div class="link-page__tree">
      <div class="link-page__tree-title">认证机构</div>
      <el-tree
        :data="treeData"
        :props="{ label: 'Name', children: 'Children' }"
        node-key="Code"
        highlight-current
        :expand-on-click-node="false"
        :loading="treeLoading"
        @node-click="handleNodeClick"
      />
    </div>

    <!-- 右表 -->
    <div class="link-page__content">
      <div class="link-page__header">
        <span class="link-page__header-title">
          {{ selectedOrgCode ? '认证阶段（勾选即关联）' : '请先选择左侧机构' }}
        </span>
        <el-input
          v-model="searchKey"
          placeholder="搜索阶段编号/名称"
          clearable
          style="width: 240px"
          :disabled="!selectedOrgCode"
        />
      </div>

      <el-table
        ref="tableRef"
        :data="filteredData"
        v-loading="tableLoading"
        :row-key="(row: CertOrgStageItem) => row.Code"
        @selection-change="handleSelectionChange"
        stripe
        style="width: 100%"
      >
        <el-table-column type="selection" width="50" />
        <el-table-column prop="PhaseCode" label="阶段编号" min-width="120" />
        <el-table-column prop="PhaseName" label="阶段名称" min-width="160" />
        <el-table-column prop="SortOrder" label="排序" width="80" />
      </el-table>
    </div>
  </div>
</template>

<style scoped>
.link-page {
  display: flex;
  height: 100%;
  overflow: hidden;
  background: #fff;
}

.link-page__tree {
  width: 280px;
  flex-shrink: 0;
  border-right: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: #fff;
}

.link-page__tree-title {
  padding: 12px 16px;
  font-weight: 600;
  font-size: 14px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
}

.link-page__tree :deep(.el-tree) {
  flex: 1;
  overflow-y: auto;
}

.link-page__content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: #fff;
}

.link-page__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fff;
}

.link-page__header-title {
  font-weight: 600;
  font-size: 14px;
}

.link-page__content :deep(.el-table) {
  flex: 1;
  overflow-y: auto;
}
</style>
