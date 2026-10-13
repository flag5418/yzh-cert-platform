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
import { YzhStatusBadge, resolveStatusBadge } from '@yzh-core'
import { ref, computed, onMounted } from 'vue'
import { Collection, Folder, Files } from '@element-plus/icons-vue'
import { OrgStandardStageTree, type StdStageTreeNode } from '@share/components'
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

const shellRef = ref<InstanceType<typeof OrgStandardStageTree>>()

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
  Children?: TreeNode[]
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
  // ⚠️ 值类型必须与 byCatFam 一致（运行期两个分支存的都是 Map<string, StandardOptionDto[]>）——
  //   原注解写成 StandardOptionDto[] 是**说谎的类型**：vue-tsc 按它推断 target 为联合类型
  //   ⇒ .has/.set/.get 全部 TS2339，且 buildFamilyNodes(famGroup) 收到错误静态类型。
  const unrecognizedCat = new Map<string, Map<string, StandardOptionDto[]>>()   // 字典外的 Category
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

// ──── 转换为壳节点（OrgStandardStageTree.StdStageTreeNode） ────
/**
 * level → 壳的 Type：
 *   0=根「全部标准」→ root / 1=体系→category / 2=族→family / 3=版本叶子→standard
 * 只有 Type='standard' 触发壳的 @select（leaf-types=['standard']），等价原 onNodeClick 的 level===3 闸。
 */
const shellNodes = computed<StdStageTreeNode[]>(() => {
  // 数据全空（standards 与体系字典都没加载到内容）→ 交给壳的空态
  if (dataReady.value && !props.standards.length && !categories.value.length) return []
  return treeData.value.map(toShellNode)
})

function toShellNode(raw: TreeNode): StdStageTreeNode {
  const type = raw.level === 0 ? 'root' : raw.level === 1 ? 'category' : raw.level === 2 ? 'family' : 'standard'
  return {
    id: raw.id,
    Code: raw.id,
    Name: raw.label,
    Type: type,
    IsLeaf: raw.isLeaf,
    level: raw.level,
    isValid: raw.isValid,
    standardCode: raw.standardCode,
    Children: raw.children?.length ? raw.children.map(toShellNode) : undefined,
  }
}

function onShellSelect(data: StdStageTreeNode) {
  emit('update:modelValue', String(data.id ?? data.Code))
  emit('select', { code: String(data.id ?? data.Code), label: data.Name ?? '' })
}

// ──── 高亮回拨（页面切换被拒时调用） ────
function resync() {
  shellRef.value?.setCurrentKey?.(props.modelValue || ROOT_SCOPE.code)
}
defineExpose({ resync })
</script>

<template>
  <OrgStandardStageTree
    ref="shellRef"
    :data="shellNodes"
    node-key="id"
    title="适用标准"
    :filterable="false"
    :default-expand-all="true"
    :leaf-types="['standard']"
    :icon-map="{ root: Files, category: Collection, family: Folder }"
    :current-key="modelValue || ROOT_SCOPE.code"
    @select="onShellSelect"
  >
    <template #node-extra="{ node }">
      <YzhStatusBadge
        v-if="resolveStatusBadge(node, 'IsValid')"
        class="std-tree__status"
        :type="resolveStatusBadge(node, 'IsValid')?.type"
        :text="resolveStatusBadge(node, 'IsValid')?.text"
        size="small"
      />
    </template>
  </OrgStandardStageTree>
</template>

<style scoped>
/* ★ 右侧状态徽章：固定收缩（壳节点行已是 flex + label flex:1 推齐右列） */
.std-tree__status {
  flex-shrink: 0;
  margin-left: auto;
}
</style>

<style>
/* 非 scoped：tooltip popper 挂到 body 下，scoped 选择器够不到 */
.std-tree-label-tip {
  max-width: 480px;
  word-break: break-all;
}
</style>
