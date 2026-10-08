<script setup lang="ts">
/**
 * 机构-标准关联管理（勾选分配模式）
 *
 * 左树：认证机构（扁平）
 * 右树：标准树（体系 → 族 → 版本，三级；checkbox 在版本叶子节点）
 *
 * ★ 参考 standard-manage 左树结构：体系（字典）+ 族（独立列表）+ 标准（关联列表）
 */
import { ref, onMounted, nextTick } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhTree, type ApiResponse } from '@yzh-core'
import { yzhApi } from '@yzh-core/api/client'
import { certOrgStandardApi } from '@share/api/cert/cert-org-standard'
import { getCertStandardFamilyList } from '@share/api/cert/cert-standard-family'
import type { CertOrgStandardItem, CertStandardFamily } from '@share/types/cert'

// ──── 分类字典（英文 DicValue → 中文 DicName）────
interface CategoryDictItem {
  Value: string
  Label: string
  Code: string
}
const categoryLabel = ref(new Map<string, string>())
const categoryList = ref<CategoryDictItem[]>([])

async function loadCategoryDict() {
  try {
    const res = await yzhApi.get<ApiResponse<CategoryDictItem[]>>(
      '/api/System/Dictionary/items/by-no/iso_category',
    )
    const items = (res?.data ?? []).filter((i) => i?.Value)
    categoryList.value = items
    categoryLabel.value = new Map(items.map((i) => [i.Value, i.Label]))
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

// ──── 右树：三级树 体系 → 族 → 版本 ────
interface StdTreeNode {
  id: string
  label: string
  category?: string       // 体系（仅根节点）
  categoryName?: string   // 体系中文名
  familyCode?: string     // 族 GUID（仅族节点和版本节点）
  familyName?: string     // 族人读名（如 "iso9000 · ISO 9000 质量管理体系族"）
  standardCode?: string   // 标准编号（仅叶子）
  standardName?: string   // 标准名称（仅叶子）
  versionYear?: number    // 年份（仅叶子）
  linked?: boolean        // 是否已关联（仅叶子有意义）
  Children?: StdTreeNode[]
  IsLeaf?: boolean
}
const stdTreeData = ref<StdTreeNode[]>([])
const stdTreeLoading = ref(false)
const stdTreeRef = ref()
const searchKey = ref('')

let suppressCheckChange = false

// ──── 按字典顺序排列分类 ────
const catOrder = ['quality', 'environment', 'safety', 'food', 'medical', 'energy', 'info']

// ──── 将字典 + 族列表 + 标准列表组装为三级树 ────
function buildStdTree(
  standards: CertOrgStandardItem[],
  families: CertStandardFamily[],
  categories: CategoryDictItem[],
): StdTreeNode[] {
  // 构建族查找表
  const famMap = new Map<string, CertStandardFamily>()
  for (const f of families) {
    if (f?.Code) famMap.set(f.Code, f)
  }

  // 按 Category → FamilyCode 分组（过滤空 Code/Name 的脏数据）
  const byCatFam = new Map<string, Map<string, CertOrgStandardItem[]>>()
  for (const s of standards) {
    // ★ 过滤：Code 或 StandardCode 为空的标准不入树（防止渲染不可勾选的空白节点）
    if (!s.Code?.trim() || !s.StandardCode?.trim()) continue
    const cat = s.Category || 'other'
    if (!byCatFam.has(cat)) byCatFam.set(cat, new Map())
    const famGroup = byCatFam.get(cat)!
    const famKey = s.FamilyCode ?? '__none__'
    const famKeyStr = String(famKey)
    if (!famGroup.has(famKeyStr)) famGroup.set(famKeyStr, [])
    famGroup.get(famKeyStr)!.push(s)
  }

  // 按字典顺序构建体系节点
  const result: StdTreeNode[] = []
  const pushedCats = new Set<string>()

  // 先按字典顺序遍历分类
  for (const cat of categories) {
    if (pushedCats.has(cat.Value)) continue
    pushedCats.add(cat.Value)
    const famGroup = byCatFam.get(cat.Value)
    const catNode = buildCategoryNode(cat, famGroup, famMap)
    result.push(catNode)
  }

  // 再处理字典之外但标准中存在的分类
  for (const [cat, famGroup] of byCatFam) {
    if (pushedCats.has(cat)) continue
    pushedCats.add(cat)
    const catLabel = categoryLabel.value.get(cat) || cat
    const catNode: StdTreeNode = {
      id: `cat_${cat}`,
      label: `${catLabel}（${countGroupLeaves(famGroup)}）`,
      category: cat,
      categoryName: catLabel,
      IsLeaf: false,
      Children: buildFamilyNodes(famGroup, famMap, cat, catLabel),
    }
    result.push(catNode)
  }

  result.sort((a, b) => {
    const ia = catOrder.indexOf(a.category || '')
    const ib = catOrder.indexOf(b.category || '')
    if (ia !== ib) return ia - ib
    return (a.categoryName || '').localeCompare(b.categoryName || '')
  })

  return result
}

/** 构建体系节点 */
function buildCategoryNode(
  cat: CategoryDictItem,
  famGroup: Map<string, CertOrgStandardItem[]> | undefined,
  famMap: Map<string, CertStandardFamily>,
): StdTreeNode {
  const Children = famGroup
    ? buildFamilyNodes(famGroup, famMap, cat.Value, cat.Label)
    : []
  return {
    id: `cat_${cat.Value}`,
    label: `${cat.Label}（${Children.reduce((s, c) => s + countLeaves(c), 0)}）`,
    category: cat.Value,
    categoryName: cat.Label,
    // ★ 修复：体系节点永远不是叶子节点，即使无子节点也应显示为可折叠父节点
    //   （el-tree 的 show-checkbox 会让所有节点都有复选框，
    //   若空体系设为 IsLeaf=true，用户勾选会当作标准保存，导致唯一约束冲突）
    IsLeaf: false,
    Children,
  }
}

/** 构建体系下的族节点列表 */
function buildFamilyNodes(
  famGroup: Map<string, CertOrgStandardItem[]>,
  famMap: Map<string, CertStandardFamily>,
  cat: string,
  catLabel: string,
): StdTreeNode[] {
  const nodes: StdTreeNode[] = []

  // 未归族的版本（FamilyCode = null）
  if (famGroup.has('__none__')) {
    const ungrouped = [...famGroup.get('__none__')!]
      .sort((a, b) => b.VersionYear - a.VersionYear || (a.StandardCode ?? '').localeCompare(b.StandardCode ?? ''))
      .map(s => buildLeafNode(s))
    if (ungrouped.length > 0) {
      nodes.push({
        id: `cat_${cat}_none`,
        label: `未归族（${ungrouped.length}）`,
        category: cat,
        categoryName: catLabel,
        IsLeaf: false,
        Children: ungrouped,
      })
    }
  }

  // 各族的版本（按族排序）
  const famEntries: [string, CertOrgStandardItem[]][] = []
  for (const [famCode, versions] of famGroup) {
    if (famCode === '__none__') continue
    famEntries.push([famCode, versions])
  }
  famEntries.sort(([a], [b]) => {
    const fa = famMap.get(a)
    const fb = famMap.get(b)
    const sa = fa?.Sort ?? 0
    const sb = fb?.Sort ?? 0
    if (sa !== sb) return sa - sb
    return (fa?.FamilyNo ?? a).localeCompare(fb?.FamilyNo ?? b)
  })

  for (const [famCode, versions] of famEntries) {
    const fam = famMap.get(famCode)
    const famName = fam ? `${fam.FamilyNo} ${fam.FamilyName}`.trim() : famCode
    const sorted = [...versions]
      .sort((a, b) => b.VersionYear - a.VersionYear || (a.StandardCode ?? '').localeCompare(b.StandardCode ?? ''))
    const leafNodes = sorted.map(s => buildLeafNode(s))
    nodes.push({
      id: `cat_${cat}_${famCode}`,
      label: famName,
      category: cat,
      categoryName: catLabel,
      familyCode: famCode,
      familyName: famName,
      IsLeaf: false,
      Children: leafNodes,
    })
  }

  return nodes
}

function buildLeafNode(item: CertOrgStandardItem): StdTreeNode {
  return {
    id: item.Code,
    label: `${item.StandardCode} · ${item.StandardName}`,
    category: item.Category,
    categoryName: categoryLabel.value.get(item.Category) || item.Category,
    familyCode: item.FamilyCode ?? undefined,
    familyName: item.FamilyName || undefined,
    standardCode: item.StandardCode,
    standardName: item.StandardName,
    versionYear: item.VersionYear,
    linked: item.Linked,
    IsLeaf: true,
  }
}

function countLeaves(node: StdTreeNode): number {
  if (!node.Children) return node.IsLeaf ? 1 : 0
  return node.Children.reduce((s, c) => s + countLeaves(c), 0)
}

function countGroupLeaves(famGroup: Map<string, CertOrgStandardItem[]>): number {
  let count = 0
  for (const versions of famGroup.values()) {
    count += versions.length
  }
  return count
}

// ──── 前端过滤：保留命中的叶子节点及其祖先 ────
function filterStdTree(nodes: StdTreeNode[], key: string): StdTreeNode[] {
  if (!key) return nodes
  const lower = key.toLowerCase()
  const result: StdTreeNode[] = []
  for (const node of nodes) {
    const filteredChildren = node.Children
      ? filterStdTree(node.Children, key)
      : []
    const nodeMatch =
      node.standardCode?.toLowerCase().includes(lower) ||
      node.standardName?.toLowerCase().includes(lower) ||
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
    const res = await certOrgStandardApi.treeRoot()
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
  await loadStdTree(node.Code)
}

// ========================================================
// 右树
// ========================================================

async function loadStdTree(orgCode: string) {
  stdTreeLoading.value = true
  suppressCheckChange = true
  try {
    stdTreeRef.value?.setCheckedKeys([])

    // 并行获取标准列表 + 族列表（字典已在初始化时加载）
    const [res, famRes] = await Promise.all([
      certOrgStandardApi.list(orgCode),
      getCertStandardFamilyList({ Page: 1, PageSize: 1000 }),
    ])
    const items = res.data || []
    const families = (famRes as any)?.data?.Items ?? []
    stdTreeData.value = buildStdTree(items, families, categoryList.value)

    await nextTick()
    const linkedKeys = items
      .filter((item: CertOrgStandardItem) => item.Linked)
      .map((item: CertOrgStandardItem) => item.Code)
    stdTreeRef.value?.setCheckedKeys(linkedKeys, false)
  } finally {
    suppressCheckChange = false
    stdTreeLoading.value = false
  }
}

/** checkbox 勾选/取消 → 立即保存（仅叶子节点触发） */
function handleStdCheckChange(data: StdTreeNode, isChecked: boolean) {
  if (suppressCheckChange) return
  if (!data.IsLeaf) return
  if (!selectedOrgCode.value) return
  // ★ 守卫：仅处理有有效 standardCode 的标准节点
  //   （空体系/族节点即使 IsLeaf=true 也没有 standardCode，不应触发保存）
  if (!data.standardCode) return

  const prevLinked = data.linked
  data.linked = isChecked

  if (isChecked && !prevLinked) {
    certOrgStandardApi.save({
      OrgCode: selectedOrgCode.value,
      StandardCode: data.standardCode,
      Linked: true,
    }).catch(() => {
      ElMessage.error(`关联标准【${data.standardName}】失败`)
      data.linked = false
    })
  } else if (!isChecked && prevLinked) {
    certOrgStandardApi.save({
      OrgCode: selectedOrgCode.value,
      StandardCode: data.standardCode,
      Linked: false,
    }).catch(() => {
      ElMessage.error(`取消关联【${data.standardName}】失败`)
      data.linked = true
    })
  }
}

/** 展开/收起全部节点 */
function toggleTreeExpand(expand: boolean) {
  if (!stdTreeRef.value) return
  const setExpanded = (nodes: StdTreeNode[], expanded: boolean) => {
    for (const node of nodes) {
      const mapNode = stdTreeRef.value!.store.nodesMap[node.id]
      if (mapNode) mapNode.expanded = expanded
      if (node.Children) setExpanded(node.Children, expanded)
    }
  }
  setExpanded(stdTreeData.value, expand)
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

    <!-- 右树：标准树（体系 → 族 → 版本） -->
    <div class="link-page__content">
      <div class="link-page__header">
        <span class="link-page__header-title">
          {{ selectedOrgCode ? 'ISO 标准（勾选即关联）' : '请先选择左侧机构' }}
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
            placeholder="搜索标准编号/名称"
            clearable
            style="width: 200px"
            :disabled="!selectedOrgCode"
          />
        </div>
      </div>

      <el-tree
        ref="stdTreeRef"
        :data="searchKey ? filterStdTree(stdTreeData, searchKey) : stdTreeData"
        :props="{ label: 'label', children: 'Children' }"
        node-key="id"
        show-checkbox
        :check-strictly="false"
        :default-expand-all="true"
        :expand-on-click-node="true"
        :check-on-click-node="true"
        :loading="stdTreeLoading"
        @check-change="handleStdCheckChange"
        class="link-page__stage-tree"
      >
        <template #default="{ data }">
          <span class="link-page__tree-node">
            <!-- 叶子节点（版本） -->
            <span v-if="data.IsLeaf" class="link-page__stage-label">
              <span class="link-page__stage-code">{{ data.standardCode }}</span>
              <span class="link-page__stage-name">{{ data.standardName }}</span>
              <span v-if="data.versionYear" class="link-page__stage-year">{{ data.versionYear }}</span>
            </span>
            <!-- 族节点 -->
            <span v-else-if="data.familyCode" class="link-page__family-label">
              {{ data.label }}
            </span>
            <!-- 体系节点 -->
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

/* ── 版本节点标签 ── */
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

.link-page__stage-year {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--el-text-color-placeholder, #c0c4cc);
  flex-shrink: 0;
}

/* ── 族节点标签 ── */
.link-page__family-label {
  font-size: var(--yzh-font-size, 14px);
  color: var(--el-text-color-regular, #303133);
  padding-left: var(--yzh-space-2, 8px);
}

/* ── 体系节点标签 ── */
.link-page__category-label {
  font-size: var(--yzh-font-size, 14px);
  color: var(--el-text-color-primary, #303133);
  font-weight: 600;
}

.link-page__stage-tree :deep(.el-tree-node__content) {
  height: var(--yzh-space-8, 32px);
}
</style>
