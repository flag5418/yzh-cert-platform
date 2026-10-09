<script setup lang="ts">
/**
 * 左栏：全部标准 → 体系 → 族 → 标准（版本）四层分组树
 *
 * ★ 结构对齐 /cert/link-org-standard 的右树（体系 → 族 → 版本三层 + 本页面多一层「全部标准」根）。
 *
 *  全部标准（固定根，scope = ''，承载平台级默认，不可点击）
 *    └─ 体系节点（iso_category 字典，如「质量管理体系（3）」）—— 不可点击
 *         └─ 「未归族（N）」节点（仅当该体系有 FamilyCode=null 的标准才出现）—— 不可点击
 *              └─ 版本叶子（scope = 标准GUID，可点击）
 *         └─ 族节点（cert_standard_family）—— 不可点击
 *              └─ 版本叶子（scope = 标准GUID，可点击）
 *
 * 强调：只有 level=3（版本叶子）可点击选择；体系和族仅作分组。
 */
import { YzhEmptyState, YzhStatusBadge, resolveStatusBadge } from '@yzh-core'
import { ref, computed, watch, nextTick, onMounted } from 'vue'
import { Collection, Folder, Document, Files } from '@element-plus/icons-vue'
import { yzhApi } from '@yzh-core/api/client'
import type { StandardOptionDto } from '@share/api/workflow/prompt-workbench'
import { ROOT_SCOPE, normScope, type StandardScope } from '../logic'
import type { CategoryItem } from '../../../foundation/iso-standard/group'
import type { CertStandardFamily } from '@share/types/cert'

// ──── 属性 ────
const props = defineProps<{
  standards: StandardOptionDto[]
  modelValue: string
}>()

const emit = defineEmits<{ (e: 'update:modelValue', v: string): void; (e: 'select', v: StandardScope): void }>()

const treeRef = ref()

// ──── 数据：体系字典 + 族列表 ────
const categories = ref<CategoryItem[]>([])
const families = ref<CertStandardFamily[]>([])
const dataReady = ref(false)

async function loadTreeData() {
  const [dictRes, famRes] = await Promise.all([
    yzhApi.get<{ success: boolean; data: CategoryItem[] }>(
      '/api/System/Dictionary/items/by-no/iso_category',
    ).catch(() => ({ success: false, data: [] })),
    yzhApi.post<{ success: boolean; data: { Items: CertStandardFamily[] } }>(
      '/api/Admin/Foundation/CertStandardFamily/filter',
      { Page: 1, PageSize: 1000 },
    ).catch(() => ({ success: false, data: { Items: [] } })),
  ])
  categories.value = dictRes?.success ? (dictRes.data ?? []) : []
  families.value = ((famRes as any)?.data?.Items ?? [])
  dataReady.value = true
}

onMounted(() => {
  loadTreeData()
})

// ──── 树节点类型 ────
interface TreeNode {
  id: string
  label: string
  /** 0=根「全部标准」/ 1=体系 / 2=族 / 3=版本(标准) */
  level: 0 | 1 | 2 | 3
  isValid?: number
  /** 标准节点的真实 Code（GUID），level=3 时才有 */
  standardCode?: string
  children?: TreeNode[]
  /** 是否叶子（影响图标与样式） */
  isLeaf?: boolean
}

// ──── 构建树 ────
const treeData = computed<TreeNode[]>(() => {
  const root: TreeNode = {
    id: ROOT_SCOPE.code,
    label: ROOT_SCOPE.label,
    level: 0,
    children: [],
  }

  if (!dataReady.value) return [root]

  // 无体系字典 → 退化：根下直接挂标准
  if (!categories.value.length) {
    root.children = props.standards.map(stdToNode)
    return [root]
  }

  // 索引族
  const famByCode = new Map<string, CertStandardFamily>()
  for (const f of families.value) {
    if (f?.Code) famByCode.set(f.Code, f)
  }

  // 按 Category → FamilyCode 分组标准
  const byCatFam = new Map<string, Map<string, StandardOptionDto[]>>()
  const unrecognizedCat = new Map<string, StandardOptionDto[]>()   // 字典外的 Category
  const uncategorized: StandardOptionDto[] = []                     // Category 为空

  for (const s of props.standards) {
    const code = normScope(s.code) || ''
    if (!code) continue
    if (!s.category) {
      uncategorized.push(s)
      continue
    }
    const cat = s.category
    const famKey = s.familyCode ?? '__none__'
    const target = categoryByValue(cat) ? byCatFam : unrecognizedCat
    if (!target.has(cat)) target.set(cat, new Map())
    const famGroup = target.get(cat)!
    if (!famGroup.has(famKey)) famGroup.set(famKey, [])
    famGroup.get(famKey)!.push(s)
  }

  // 遍历字典构建体系
  for (const cat of categories.value) {
    if (!cat?.Code || !cat?.Value) continue
    const famGroup = byCatFam.get(cat.Value)
    if (!famGroup) continue  // 该体系下无标准 → 不展示
    const famNodes = buildFamilyNodes(famGroup, famByCode)
    if (famNodes.length === 0) continue
    root.children!.push({
      id: cat.Code,
      label: `${cat.Label}（${countLeaves(famNodes)}）`,
      level: 1,
      isLeaf: false,
      children: famNodes,
    })
  }

  // 字典外的 Category（兜底）
  for (const [cat, famGroup] of unrecognizedCat) {
    const famNodes = buildFamilyNodes(famGroup, famByCode)
    if (famNodes.length === 0) continue
    root.children!.push({
      id: `cat_${cat}`,
      label: `${cat}（${countLeaves(famNodes)}）`,
      level: 1,
      isLeaf: false,
      children: famNodes,
    })
  }

  // 完全无 Category 的标准 → 挂在「未分类」体系节点
  if (uncategorized.length) {
    root.children!.push({
      id: '__uncategorized__',
      label: `未分类（${uncategorized.length}）`,
      level: 1,
      isLeaf: false,
      children: [buildNoneGroup(uncategorized)],
    })
  }

  return [root]
})

// ──── 工具函数 ────

function categoryByValue(v: string): CategoryItem | undefined {
  return categories.value.find(c => c.Value === v)
}

/** 构建体系下的族列表 + 未归族节点 */
function buildFamilyNodes(
  famGroup: Map<string, StandardOptionDto[]>,
  famByCode: Map<string, CertStandardFamily>,
): TreeNode[] {
  const nodes: TreeNode[] = []

  // ① 未归族（FamilyCode=null）
  if (famGroup.has('__none__')) {
    const noneKids = [...(famGroup.get('__none__') ?? [])].sort(sortByStdCode).map(stdToNode)
    if (noneKids.length) {
      nodes.push({
        id: `__none__`,
        label: `未归族（${noneKids.length}）`,
        level: 2,
        isLeaf: false,
        children: noneKids,
      })
    }
  }

  // ② 各族（按 Sort + FamilyNo 排序）
  const famEntries: [string, StandardOptionDto[]][] = []
  for (const [famCode, versions] of famGroup) {
    if (famCode === '__none__') continue
    famEntries.push([famCode, versions])
  }
  famEntries.sort(([a], [b]) => {
    const fa = famByCode.get(a)
    const fb = famByCode.get(b)
    const sa = fa?.Sort ?? 0
    const sb = fb?.Sort ?? 0
    if (sa !== sb) return sa - sb
    return (fa?.FamilyNo ?? a).localeCompare(fb?.FamilyNo ?? b)
  })

  for (const [famCode, versions] of famEntries) {
    const fam = famByCode.get(famCode)
    const label = fam ? `${fam.FamilyNo} ${fam.FamilyName}`.trim() : famCode
    const kids = [...versions].sort(sortByStdCode).map(stdToNode)
    if (kids.length === 0) continue
    nodes.push({
      id: `fam_${famCode}`,
      label,
      level: 2,
      isValid: fam?.IsValid ?? 1,
      isLeaf: false,
      children: kids,
    })
  }

  return nodes
}

/** 构建完全无 Category 的标准下的节点（仅一层：未归族 / 各族）*/
function buildNoneGroup(standards: StandardOptionDto[]): TreeNode {
  // 有 FamilyCode → 按族分组
  const byFam = new Map<string, StandardOptionDto[]>()
  const noneKids: StandardOptionDto[] = []
  for (const s of standards) {
    if (s?.familyCode) {
      const arr = byFam.get(s.familyCode!) ?? []
      arr.push(s)
      byFam.set(s.familyCode!, arr)
    } else {
      noneKids.push(s)
    }
  }

  const children: TreeNode[] = []
  if (noneKids.length) {
    children.push({
      id: '__none__',
      label: `未归族（${noneKids.length}）`,
      level: 2,
      isLeaf: false,
      children: noneKids.sort(sortByStdCode).map(stdToNode),
    })
  }
  for (const [famCode, versions] of byFam) {
    children.push({
      id: `fam_${famCode}`,
      label: famCode,
      level: 2,
      isLeaf: false,
      children: versions.sort(sortByStdCode).map(stdToNode),
    })
  }
  return {
    id: '__none_top__',
    label: `未归族（${children.reduce((s, c) => s + (c.children?.length ?? 0), 0)}）`,
    level: 2,
    isLeaf: false,
    children,
  }
}

function stdToNode(s: StandardOptionDto): TreeNode {
  const code = normScope(s.code) || ''
  return {
    id: code,
    label: `${s.standardCode ?? ''} · ${s.standardName ?? ''}`.trim(),
    level: 3,
    isValid: s.isValid ?? 1,
    standardCode: code,
    isLeaf: true,
  }
}

function sortByStdCode(a: StandardOptionDto, b: StandardOptionDto): number {
  return (a.standardCode ?? '').localeCompare(b.standardCode ?? '', undefined, { numeric: true })
}

function countLeaves(nodes: TreeNode[]): number {
  return nodes.reduce((s, c) => s + (c.children ? countLeaves(c.children) : (c.isLeaf ? 1 : 0)), 0)
}

// ──── 节点点击 ────
function onNodeClick(data: TreeNode) {
  if (data.level !== 3 || !data.standardCode) return
  emit('update:modelValue', data.id)
  emit('select', { code: data.id, label: data.label })
}

// ──── 高亮同步 ────
function resync() {
  treeRef.value?.setCurrentKey?.(props.modelValue || ROOT_SCOPE.code)
}
defineExpose({ resync })

watch(
  () => props.modelValue,
  async (v) => {
    await nextTick()
    treeRef.value?.setCurrentKey?.(v || ROOT_SCOPE.code)
  },
  { immediate: true },
)
</script>

<template>
  <div class="std-tree">
    <div class="std-tree__header">
      <span class="std-tree__title">适用标准</span>
    </div>

    <div class="std-tree__body">
      <el-tree
        ref="treeRef"
        :data="treeData"
        node-key="id"
        highlight-current
        :expand-on-click-node="false"
        :default-expanded-keys="[ROOT_SCOPE.code]"
        @node-click="onNodeClick"
      >
        <template #default="{ data }">
          <span
            class="std-tree__node"
            :class="{
              'std-tree__node--group': data.level === 1 || data.level === 2,
              'std-tree__node--clickable': data.level === 3,
            }"
          >
            <!-- 图标 -->
            <el-icon v-if="data.level === 0" class="std-tree__icon"><Files /></el-icon>
            <el-icon v-else-if="data.level === 1" class="std-tree__icon"><Collection /></el-icon>
            <el-icon v-else-if="data.level === 2" class="std-tree__icon"><Folder /></el-icon>
            <el-icon v-else class="std-tree__icon"><Document /></el-icon>

            <!-- 标签 -->
            <span class="std-tree__label" :title="data.label">{{ data.label }}</span>

            <!-- 启用/禁用徽章 -->
            <YzhStatusBadge
              v-if="resolveStatusBadge(data, 'IsValid')"
              class="std-tree__status"
              :type="resolveStatusBadge(data, 'IsValid')?.type"
              :text="resolveStatusBadge(data, 'IsValid')?.text"
              size="small"
            />
          </span>
        </template>
      </el-tree>

      <YzhEmptyState v-if="dataReady && !standards.length && !categories.length" title="暂无标准" />
    </div>
  </div>
</template>

<style scoped>
.std-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.std-tree__header {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1);
  padding: var(--yzh-space-3) var(--yzh-space-4);
  border-bottom: 1px solid var(--yzh-color-border-light);
}
.std-tree__title {
  font-size: var(--yzh-font-size-sm);
  font-weight: var(--yzh-font-weight-semibold);
  color: var(--yzh-color-text-primary);
}

.std-tree__body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-2) var(--yzh-space-1);
}

/*
 * ★ 对齐关键（对齐 YzhTree 的 yzh-tree__node 机制）：
 *   容器 `display:flex; flex:1` 撑满节点整行宽度；
 *   label `flex:1` 吸收剩余空间，把右侧 badges/status 在同一垂直列对齐。
 *   ⚠️ 不要用 inline-flex —— inline-flex 宽度由内容决定，status 必然参差不齐。
 */
.std-tree__node {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1);
  flex: 1;
  min-width: 0;
  padding-right: var(--yzh-space-2);
}
.std-tree__node--group {
  cursor: default;
}
.std-tree__node--clickable {
  cursor: pointer;
}
.std-tree__icon {
  color: var(--yzh-color-primary);
  flex-shrink: 0;
}
/*
 * ★ 弹性宽度：撑开填满 node 中段，把所有右侧元素（badges / status）推到同一列。
 *   label 长短不重要了——右边界永远齐平（同 YzhTree yzh-tree__label）。
 */
.std-tree__label {
  flex: 1;
  font-size: var(--yzh-font-size-sm);
  color: var(--yzh-color-text-regular);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  min-width: 0;
}
/* ★ 右侧状态徽章：固定收缩 + auto 推至最右列（同 YzhTree yzh-tree__status） */
.std-tree__status {
  flex-shrink: 0;
  margin-left: auto;
}
</style>
