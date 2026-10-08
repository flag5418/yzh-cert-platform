<script setup lang="ts">
/**
 * 机构-阶段关联管理（勾选分配模式）
 *
 * 左树：认证机构（扁平）
 * 右树：认证阶段（按 Category 分组，checkbox 勾选即关联）
 */
import { ref, onMounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhTree, type ApiResponse } from '@yzh-core'
import { yzhApi } from '@yzh-core/api/client'
import { certOrgStageApi } from '@share/api/cert/cert-org-stage'
import type { CertOrgStageItem } from '@share/types/cert'

// ──── 分类字典（英文 DicValue → 中文 DicName）────
interface CategoryDictItem {
  Value: string
  Label: string
}
const categoryLabel = ref(new Map<string, string>())

async function loadCategoryDict() {
  try {
    const res = await yzhApi.get<ApiResponse<CategoryDictItem[]>>(
      '/api/System/Dictionary/items/by-no/stage_category',
    )
    categoryLabel.value = new Map(
      (res?.data ?? []).filter((i) => i?.Value).map((i) => [i.Value, i.Label]),
    )
  } catch {
    // 字典加载失败不影响主流程
  }
}

// ──── 左树 ────
interface OrgTreeNode {
  Code: string
  Name: string
  Extra?: Record<string, any>
  Children?: OrgTreeNode[]
}
const orgTreeData = ref<OrgTreeNode[]>([])
const selectedOrgCode = ref<string>('')
const orgTreeLoading = ref(false)

// ──── 右树：阶段树（按 Category 分组）────
interface StageTreeNode {
  id: string
  label: string
  code?: string       // 阶段 Code（GUID），仅叶子节点有值
  category?: string   // Category 值（process/audit/post）
  categoryName?: string // Category 中文名
  phaseCode?: string  // 阶段业务码（如 AP/S1）
  phaseName?: string  // 阶段名称
  sortOrder?: number
  linked: boolean       // 是否已关联
  Children?: StageTreeNode[]
  isLeaf?: boolean
}
const stageTreeData = ref<StageTreeNode[]>([])
const stageTreeLoading = ref(false)
const stageTreeRef = ref()
const searchKey = ref('')

// ──── 抑制初始化时的 check-change 事件 ────
let suppressCheckChange = false

// ──── 将后端扁平列表重组为按 Category 分组的树 ────
function buildStageTree(items: CertOrgStageItem[]): StageTreeNode[] {
  // 按 Category 分组
  const groupMap = new Map<string, CertOrgStageItem[]>()
  for (const item of items) {
    const cat = item.Category || 'other'
    if (!groupMap.has(cat)) groupMap.set(cat, [])
    groupMap.get(cat)!.push(item)
  }

  // 按 SortOrder 排序每组内阶段
  const sortItems = (list: CertOrgStageItem[]) =>
    [...list].sort((a, b) => (a.SortOrder ?? 0) - (b.SortOrder ?? 0))

  // 构建树节点
  const result: StageTreeNode[] = []
  for (const [cat, stageList] of groupMap) {
    const sorted = sortItems(stageList)
    const catLabel = categoryLabel.value.get(cat) || cat
    const Children: StageTreeNode[] = sorted.map((s) => ({
      id: s.Code,
      label: `${s.PhaseCode} · ${s.PhaseName}`,
      code: s.Code,
      category: cat,
      categoryName: catLabel,
      phaseCode: s.PhaseCode,
      phaseName: s.PhaseName,
      sortOrder: s.SortOrder,
      linked: s.Linked,
      isLeaf: true,
    }))
    result.push({
      id: `cat_${cat}`,
      label: `${catLabel}（${Children.length}）`,
      category: cat,
      categoryName: catLabel,
      Children,
    } as StageTreeNode)
  }

  // 按字典顺序排列分类
  const catOrder = ['process', 'audit', 'post']
  result.sort((a, b) => {
    const ia = catOrder.indexOf(a.category || '')
    const ib = catOrder.indexOf(b.category || '')
    if (ia !== ib) return ia - ib
    return (a.categoryName || '').localeCompare(b.categoryName || '')
  })

  return result
}

// ──── 前端过滤：保留命中的叶子节点及其祖先 ────
function filterStageTree(nodes: StageTreeNode[], key: string): StageTreeNode[] {
  if (!key) return nodes
  const lower = key.toLowerCase()
  const result: StageTreeNode[] = []
  for (const node of nodes) {
    const filteredChildren = node.Children
      ? filterStageTree(node.Children, key)
      : []
    const nodeMatch =
      node.phaseCode?.toLowerCase().includes(lower) ||
      node.phaseName?.toLowerCase().includes(lower) ||
      node.label.toLowerCase().includes(lower)
    if (filteredChildren.length > 0 || nodeMatch) {
      result.push({ ...node, Children: filteredChildren.length > 0 ? filteredChildren : node.Children })
    }
  }
  return result
}

// ========================================================
// 左树
// ========================================================

async function loadOrgTree() {
  orgTreeLoading.value = true
  try {
    const res = await certOrgStageApi.treeRoot()
    orgTreeData.value = (res.data || []).map((item: any) => ({
      Code: item.Code,
      Name: item.Name,
      Extra: item.Extra,
    }))
  } finally {
    orgTreeLoading.value = false
  }
}

async function handleOrgNodeClick(node: OrgTreeNode) {
  selectedOrgCode.value = node.Code
  await loadStageTree(node.Code)
}

// ========================================================
// 右树
// ========================================================

async function loadStageTree(orgCode: string) {
  stageTreeLoading.value = true
  suppressCheckChange = true
  try {
    stageTreeRef.value?.setCheckedKeys([])

    const res = await certOrgStageApi.list(orgCode)
    const items = res.data || []
    stageTreeData.value = buildStageTree(items)

    // 等 DOM 更新后勾选已关联的节点
    await nextTick()
    const linkedKeys = items
      .filter((item: CertOrgStageItem) => item.Linked)
      .map((item: CertOrgStageItem) => item.Code)
    stageTreeRef.value?.setCheckedKeys(linkedKeys, false)
  } finally {
    suppressCheckChange = false
    stageTreeLoading.value = false
  }
}

/** checkbox 勾选/取消 → 立即保存（仅叶子节点触发） */
function handleStageCheckChange(
  data: StageTreeNode,
  isChecked: boolean,
) {
  if (suppressCheckChange) return
  // 分类父节点不触发保存
  if (!data.isLeaf) return
  if (!selectedOrgCode.value) return

  const prevLinked = data.linked
  // 乐观更新本地状态
  data.linked = isChecked

  if (isChecked && !prevLinked) {
    // 新勾选 → 关联
    certOrgStageApi.save({
      OrgCode: selectedOrgCode.value,
      StageCode: data.code!,
      Linked: true,
    }).catch(() => {
      ElMessage.error(`关联阶段【${data.phaseName}】失败`)
      data.linked = false
    })
  } else if (!isChecked && prevLinked) {
    // 取消勾选 → 取消关联
    certOrgStageApi.save({
      OrgCode: selectedOrgCode.value,
      StageCode: data.code!,
      Linked: false,
    }).catch(() => {
      ElMessage.error(`取消关联【${data.phaseName}】失败`)
      data.linked = true
    })
  }
}

/** 展开/收起全部节点 */
function toggleTreeExpand(expand: boolean) {
  if (!stageTreeRef.value) return
  const setExpanded = (nodes: StageTreeNode[], expanded: boolean) => {
    for (const node of nodes) {
      const mapNode = stageTreeRef.value!.store.nodesMap[node.id]
      if (mapNode) mapNode.expanded = expanded
      if (node.Children) setExpanded(node.Children, expanded)
    }
  }
  setExpanded(stageTreeData.value, expand)
}

// ========================================================
// 初始化
// ========================================================

onMounted(() => {
  loadOrgTree()
  loadCategoryDict()
})
</script>

<template>
  <div class="link-page">
    <!-- 左树：认证机构 -->
    <div class="link-page__tree">
      <div class="link-page__tree-title">认证机构</div>
      <YzhTree
        :data="orgTreeData"
        node-key="Code"
        :loading="orgTreeLoading"
        @node-click="handleOrgNodeClick"
      />
    </div>

    <!-- 右树：认证阶段（按分类分组） -->
    <div class="link-page__content">
      <div class="link-page__header">
        <span class="link-page__header-title">
          {{ selectedOrgCode ? '认证阶段（勾选即关联）' : '请先选择左侧机构' }}
        </span>
        <div class="link-page__header-actions">
          <el-button
            type="primary"
            link
            size="small"
            @click="toggleTreeExpand(true)"
            :disabled="!selectedOrgCode"
          >
            全部展开
          </el-button>
          <el-button
            type="primary"
            link
            size="small"
            @click="toggleTreeExpand(false)"
            :disabled="!selectedOrgCode"
          >
            全部收起
          </el-button>
          <el-input
            v-model="searchKey"
            placeholder="搜索阶段编号/名称"
            clearable
            style="width: 200px"
            :disabled="!selectedOrgCode"
          />
        </div>
      </div>

      <el-tree
        ref="stageTreeRef"
        :data="searchKey ? filterStageTree(stageTreeData, searchKey) : stageTreeData"
        :props="{ label: 'label', children: 'Children' }"
        node-key="id"
        show-checkbox
        :check-strictly="false"
        :default-expand-all="true"
        :expand-on-click-node="true"
        :check-on-click-node="true"
        :loading="stageTreeLoading"
        @check-change="handleStageCheckChange"
        class="link-page__stage-tree"
      >
        <template #default="{ data }">
          <span class="link-page__tree-node">
            <!-- 叶子节点（阶段）：显示编号 + 名称 -->
            <span v-if="data.isLeaf" class="link-page__stage-label">
              <span class="link-page__stage-code">{{ data.phaseCode }}</span>
              <span class="link-page__stage-name">{{ data.phaseName }}</span>
            </span>
            <!-- 父节点（分类）：显示分类名 + 数量 -->
            <span v-else class="link-page__category-label">
              {{ data.label }}
            </span>
          </span>
        </template>
      </el-tree>
    </div>
  </div>
</template>

<style scoped>
.link-page {
  display: flex;
  height: 100%;
  overflow: hidden;
  background: var(--yzh-color-bg-container, #fff);
}

/* ── 左树 ── */
.link-page__tree {
  width: 280px;
  flex-shrink: 0;
  border-right: 1px solid var(--el-border-color-lighter);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--yzh-color-bg-container, #fff);
}

.link-page__tree-title {
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  font-weight: 600;
  font-size: var(--yzh-font-size-md, 14px);
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--yzh-color-bg-container, #fff);
}

.link-page__tree :deep(.el-tree) {
  flex: 1;
  overflow-y: auto;
}

/* ── 右树区域 ── */
.link-page__content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  background: var(--yzh-color-bg-container, #fff);
}

.link-page__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--yzh-color-bg-container, #fff);
}

.link-page__header-title {
  font-weight: 600;
  font-size: var(--yzh-font-size-md, 14px);
}

.link-page__header-actions {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-3, 12px);
}

.link-page__stage-tree {
  flex: 1;
  overflow-y: auto;
  padding: var(--yzh-space-2, 8px) 0;
}

/* ── 阶段节点标签 ── */
.link-page__stage-label {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-2, 8px);
  min-width: 0;
}

.link-page__stage-code {
  display: inline-block;
  flex-shrink: 0;
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-secondary, #909399);
  font-family: monospace;
}

.link-page__stage-name {
  font-size: var(--yzh-font-size, 14px);
  color: var(--el-text-color-regular, #303133);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* ── 分类节点标签 ── */
.link-page__category-label {
  font-size: var(--yzh-font-size, 14px);
  color: var(--el-text-color-primary, #303133);
  font-weight: 500;
}

/* 分类节点取消勾选框显示 */
.link-page__stage-tree :deep(.el-tree-node__content) {
  height: var(--yzh-space-8, 32px);
}

/* 分类节点不显示 Checkbox */
.link-page__stage-tree :deep(.el-tree-node:not(:last-child) > .el-tree-node__content > .el-tree-node__expand-icon) {
  visibility: visible;
}

.link-page__stage-tree :deep(.el-tree-node:not(:last-child) > .el-tree-node__content > .el-tree-node__expand-icon.is-leaf) {
  visibility: hidden;
}
</style>
