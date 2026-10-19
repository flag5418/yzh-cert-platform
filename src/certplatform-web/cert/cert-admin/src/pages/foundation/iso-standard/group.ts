/**
 * ISO 标准左树「体系（分类） → 族 → 版本（标准）」分组（**视图层重组，纯前端**）
 *
 * 三层体系（2026-10-08 裁决「统一，都支持」）：
 *   体系 = `iso_category` 字典（不建表）
 *   族   = `cert_standard_family`（`FamilyCode` 关联）
 *   版本 = `cert_iso_standard`（本页左树的「标准」节点，可维护条款）
 *
 * 设计约束：
 * 1. **不改表结构、后端不分组** —— `/tree/root` 仍返回扁平标准（`ParentCode=null`、`MaxLevel=1`）。
 *    本函数在视图层按 `iso_category` 字典 + `cert_standard_family` 列表重组三层。
 * 2. **分类不是数据，是字典** —— 来源 `GET /api/System/Dictionary/items/by-no/iso_category`。
 * 3. **分类节点 `Code` = 字典项 Code（GUID）**、**族节点 `Code` = 族 Code（GUID）** ——
 *    ⛔ 不造 `__cat_*` 之类合成码（本项目「关联一律走 Code」）。
 *    分类节点 `NodeType='virtual'`、族节点 `NodeType='family'`，两者都**不可选**；
 *    只有标准节点（`NodeType` 为空）可选中以加载/维护条款。
 * 4. **`Extra.Category` 保持 `DicValue`**（`quality`）—— 与 `ISOStandard.Category` 同口径。
 * 5. **无族标准 / 未知体系** 走兜底节点（`未归族` / `未分类`），保证不丢节点。
 *
 * 节点标签：体系 = 字典名；族 = `族编号 族名`；标准 = `编号:年份 名称`（如 `iso9001:2015 9001标准`）。
 * ★ 标准节点的 `Name` 被改写成标签后，**真名挪到 `Extra.StandardName`** ——
 *   `openEditStdDialog` 靠它回填，否则会把标签当成标准名存回库。
 */
import type { TreeNode } from '@yzh-core'

/** 分类字典项（`items/by-no/{dicNo}` 返回形态，PascalCase 字段） */
export interface CategoryItem {
  /** `DicValue` 业务值 —— 与 `ISOStandard.Category` 对齐 */
  Value: string
  /** `DicName` 展示文本 */
  Label: string
  /** 字典项 `Code`（GUID）—— 分类节点的身份键 */
  Code: string
}

/** 标准族（`cert_standard_family`，左树「族」层） */
export interface FamilyItem {
  /** 族 Code（GUID）—— 族节点身份键 + `ISOStandard.FamilyCode` 关联值 */
  Code: string
  /** 族人读编号（如 iso9000） */
  FamilyNo: string
  /** 族中文名 */
  FamilyName: string
  /** 所属体系（`iso_category` 字典 DicValue） */
  Category: string
  /** 排序号 */
  Sort?: number
  /** 启用标志（1=启用 0=停用） */
  IsValid?: number
}

/**
 * 标准节点标签：`编号:年份 名称`
 *
 * - 有编号有年份 → `iso9001:2015 9001标准`
 * - 缺年份      → `iso9001 9001标准`
 * - 缺编号      → `9001标准`（退化为纯名称）
 */
export function formatStdLabel(
  extra: Record<string, any> | undefined | null,
  name: string,
): string {
  const e = extra ?? {}
  const code = String(e.StandardCode ?? '').trim()
  const year = Number(e.VersionYear)
  const hasYear = Number.isFinite(year) && year > 0
  const prefix = code && hasYear ? `${code}:${year}` : code
  return prefix ? `${prefix} ${name}` : name
}

/** 族节点标签：`族编号 族名`（缺一退化） */
export function formatFamilyLabel(family: FamilyItem): string {
  const no = String(family.FamilyNo ?? '').trim()
  const name = String(family.FamilyName ?? '').trim()
  return `${no} ${name}`.trim() || family.Code
}

/** 「未分类」兜底体系节点的 Code（仅当存在字典外体系时才出现） */
export const UNCATEGORIZED_CODE = '__uncategorized__'

/** 未分类兜底的字典外取值标识（虚拟节点，不会写入任何关联字段） */
export const UNCATEGORIZED_LABEL = '未分类'

/** 「未归族」兜底族节点标签（标准 FamilyCode 为空 / 指向已删除族时） */
export const UNFAMILIED_LABEL = '未归族'

/** 「未归族」兜底族节点 Code 前缀（必须按体系拼后缀，保证 node-key 唯一） */
const UNFAMILIED_CODE_PREFIX = '__unfamilied__'

const sortByStandardCode = (a: TreeNode, b: TreeNode) =>
  String(a.Extra?.StandardCode ?? '').localeCompare(
    String(b.Extra?.StandardCode ?? ''),
    undefined,
    { numeric: true },
  )

const sortFamily = (a: FamilyItem, b: FamilyItem) =>
  (a.Sort ?? 0) - (b.Sort ?? 0) ||
  String(a.FamilyNo ?? '').localeCompare(String(b.FamilyNo ?? ''), undefined, {
    numeric: true,
  })

/**
 * 扁平标准 → 「体系 → 族 → 标准」三层树
 *
 * @param standards  后端 `/tree/root` 返回的**扁平**标准节点（`Name` = `StandardName`）
 * @param categories `iso_category` 字典项，顺序即展示顺序（接口已按 `OrderNo` 升序）
 * @param families   `cert_standard_family` 列表（可空数组）
 *
 * 规则：
 * - 体系按字典顺序**全部显示**（含空的体系）
 * - 族按 `Category` 归位；字典外体系的族归入「未分类」（有才显示）
 * - 标准按 `Extra.FamilyCode` 挂到族下；无族 / 族已删的标准归入该体系下的「未归族」（有才显示）
 * - 每层内：族按 `Sort` → `FamilyNo`，标准按 `StandardCode` 数字序
 * - 字典为空 = 分组无意义 → 降级返回原扁平结构（页面照常可用）
 */
export function buildCategoryFamilyStandardTree(
  standards: TreeNode[],
  categories: CategoryItem[],
  families: FamilyItem[],
): TreeNode[] {
  const flat = standards ?? []
  // 字典为空 = 分组无意义：保持原扁平结构（降级模式，页面照常可用）
  if (!categories?.length) return flat

  // ── 体系索引（去重，字典项 Code 是 UNIQUE，防御性去重） ──
  const seenCatCode = new Set<string>()
  const catByValue = new Map<string, CategoryItem>()
  for (const c of categories) {
    if (!c?.Code || !c?.Value || seenCatCode.has(c.Code)) continue
    seenCatCode.add(c.Code)
    catByValue.set(c.Value, c)
  }

  // ── 族索引（族 Code 唯一，防御性去重） ──
  const familyByCode = new Map<string, FamilyItem>()
  for (const f of families ?? []) {
    if (!f?.Code || familyByCode.has(f.Code)) continue
    familyByCode.set(f.Code, f)
  }

  // ── 标准分类：挂族 / 无族 ──
  const stdsByFamily = new Map<string, TreeNode[]>()
  /** 无族标准（FamilyCode 为空或指向已删除族），按体系 Code 归桶 */
  const orphansByCategory = new Map<string, TreeNode[]>()

  for (const s of flat) {
    if (!s?.Code) continue
    const extra = { ...(s.Extra ?? {}) }
    // 真名：后端 `MapToTreeItem` 的 `Name` 就是 `StandardName`；Extra 里没有，必须自己留档
    const stdName = String(extra.StandardName ?? s.Name ?? '')
    const familyCode = String(extra.FamilyCode ?? '')

    const node: TreeNode = {
      ...s,
      Name: formatStdLabel(extra, stdName),
      ParentCode: null, // 下面按族/体系回填
      NodeType: undefined, // 标准节点：非 virtual / 非 family ⇒ 可选，可维护条款
      IsLeaf: true,
      Extra: { ...extra, StandardName: stdName, level: 2 },
      Children: [],
    }

    const family = familyCode ? familyByCode.get(familyCode) : undefined
    if (family) {
      const arr = stdsByFamily.get(family.Code) ?? []
      arr.push(node)
      stdsByFamily.set(family.Code, arr)
    } else {
      // 无族标准 → 归到「标准自身体系」下的「未归族」桶（字典外体系统一落空串桶 → 未分类）
      const category = String(extra.Category ?? '')
      const key = catByValue.has(category) ? category : ''
      const arr = orphansByCategory.get(key) ?? []
      arr.push(node)
      orphansByCategory.set(key, arr)
    }
  }

  // ── 族按体系归桶（字典外体系 → 归入「未分类」） ──
  const familiesByCategory = new Map<string, FamilyItem[]>()
  for (const f of familyByCode.values()) {
    const category = catByValue.has(String(f.Category ?? '')) ? String(f.Category) : ''
    const arr = familiesByCategory.get(category) ?? []
    arr.push(f)
    familiesByCategory.set(category, arr)
  }

  const buildFamilyNode = (family: FamilyItem): TreeNode => {
    const stds = (stdsByFamily.get(family.Code) ?? []).sort(sortByStandardCode)
    for (const s of stds) s.ParentCode = family.Code
    return {
      Code: family.Code,
      Name: formatFamilyLabel(family),
      ParentCode: null, // 下面按体系回填
      NodeType: 'family',
      IsLeaf: stds.length === 0,
      IsValid: family.IsValid ?? 1,
      Extra: {
        level: 1,
        Category: family.Category ?? '',
        FamilyNo: family.FamilyNo ?? '',
        FamilyName: family.FamilyName ?? '',
        Sort: family.Sort,
        IsValid: family.IsValid ?? 1,
      },
      Children: stds,
    }
  }

  /** 构建体系节点：族人 → 族节点，无族标准 → 「未归族」兜底族节点 */
  const buildCategoryNode = (
    code: string,
    name: string,
    categoryValue: string,
    familyList: FamilyItem[],
    orphanStandards: TreeNode[],
  ): TreeNode => {
    const children: TreeNode[] = []
    for (const family of [...familyList].sort(sortFamily)) {
      children.push(buildFamilyNode(family))
    }
    if (orphanStandards.length > 0) {
      const orphanCode = `${UNFAMILIED_CODE_PREFIX}${code}`
      const sortedOrphans = [...orphanStandards].sort(sortByStandardCode)
      for (const s of sortedOrphans) s.ParentCode = orphanCode
      children.push({
        Code: orphanCode,
        Name: UNFAMILIED_LABEL,
        ParentCode: null,
        NodeType: 'family',
        IsLeaf: false,
        Extra: { level: 1, Category: categoryValue, IsValid: 1 },
        Children: sortedOrphans,
      })
    }
    for (const child of children) child.ParentCode = code
    return {
      Code: code,
      Name: name,
      ParentCode: null,
      NodeType: 'virtual',
      IsLeaf: children.length === 0,
      Extra: { level: 0, Category: categoryValue },
      Children: children,
    }
  }

  const result: TreeNode[] = []
  const pushed = new Set<string>()
  for (const c of categories) {
    // 重复字典项（同 Code）只出一个节点，否则会生成两棵同名体系
    if (!seenCatCode.has(c.Code) || pushed.has(c.Code)) continue
    pushed.add(c.Code)
    result.push(
      buildCategoryNode(
        c.Code,
        c.Label,
        c.Value,
        familiesByCategory.get(c.Value) ?? [],
        orphansByCategory.get(c.Value) ?? [],
      ),
    )
  }

  // 字典外体系：族 / 无族标准 有才出现的「未分类」兜底
  const orphanFamilies = familiesByCategory.get('') ?? []
  const orphanStds = orphansByCategory.get('') ?? []
  if (orphanFamilies.length > 0 || orphanStds.length > 0) {
    result.push(
      buildCategoryNode(UNCATEGORIZED_CODE, UNCATEGORIZED_LABEL, '', orphanFamilies, orphanStds),
    )
  }

  return result
}
