/**
 * ISO 标准左树「类别 → 标准」分组（**视图层重组，纯前端**）
 *
 * 设计约束（2026-10-08 裁决）：
 * 1. **不改表结构、后端不分组** —— `/tree/root` 仍返回扁平标准（`ParentCode=null`、`MaxLevel=1`）。
 *    ⛔ 后端分组会破坏 `cert/fill-param-def` 独立覆写 `loadTreeRoot` 的调用方。
 * 2. **分类不是数据，是字典** —— 来源 `GET /api/System/Dictionary/items/by-no/iso_category`。
 * 3. **分类节点的 `Code` = 字典项 Code（GUID）**，⛔ 不造 `__cat_*` 之类合成码 ——
 *    本项目「关联一律走 Code」，分类将来若被任何表关联，必须能直接用 `selectedNode.Code`。
 *    分类节点同时 `NodeType='virtual'`，因此 `relatedValue()`/`shouldApplyTreeFilter()`
 *    自动归零，**永远不会**被写进 `StandardCode` 之类的关联字段。
 * 4. **`Extra.Category` 保持 `DicValue`**（`quality`）—— 与 `ISOStandard.Category` 同口径，
 *    表单预填、`v_iso_standard` 的 join、字典下拉 value 三处都依赖它。
 *
 * 节点标签 = `编号:年份 名称`（如 `iso9001:2015 9001标准`）。
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

/** 「未分类」兜底节点的 Code（仅当存在字典外类别时才出现） */
export const UNCATEGORIZED_CODE = '__uncategorized__'

/** 未分类兜底的字典外取值标识（虚拟节点，不会写入任何关联字段） */
export const UNCATEGORIZED_LABEL = '未分类'

/**
 * 扁平标准树 → 「类别 → 标准」两层树
 *
 * @param standards  后端 `/tree/root` 返回的**扁平**标准节点（`Name` = `StandardName`）
 * @param categories `iso_category` 字典项，顺序即展示顺序（接口已按 `OrderNo` 升序）
 *
 * 规则：
 * - 分类按字典顺序**全部显示**（含无标准的空分类）
 * - 标准按 `Extra.Category` 归位；字典外的类别归入「未分类」（有才显示）
 * - 每层内标准按 `StandardCode` 升序
 */
export function buildCategoryTree(
  standards: TreeNode[],
  categories: CategoryItem[],
): TreeNode[] {
  const flat = standards ?? []
  // 字典为空 = 分组无意义：保持原扁平结构（降级模式，页面照常可用）
  if (!categories?.length) return flat

  const buckets = new Map<string, TreeNode[]>()
  const catByValue = new Map<string, CategoryItem>()
  const seenCode = new Set<string>()

  // 去重 + 建 value 索引（字典项 Code 是 UNIQUE，但防御性去重）
  for (const c of categories) {
    if (!c?.Code || !c?.Value || seenCode.has(c.Code)) continue
    seenCode.add(c.Code)
    catByValue.set(c.Value, c)
    buckets.set(c.Value, [])
  }

  const orphan: TreeNode[] = []

  for (const s of flat) {
    if (!s?.Code) continue
    const extra = { ...(s.Extra ?? {}) }
    // 真名：后端 `MapToTreeItem` 的 `Name` 就是 `StandardName`；Extra 里没有，必须自己留档
    const stdName = String(extra.StandardName ?? s.Name ?? '')
    const category = String(extra.Category ?? '')

    const child: TreeNode = {
      ...s,
      Name: formatStdLabel(extra, stdName),
      ParentCode: null, // 下面按分类回填
      NodeType: undefined,
      IsLeaf: true,
      Extra: { ...extra, StandardName: stdName, level: 1 },
      Children: [],
    }

    if (catByValue.has(category)) {
      const parentCode = catByValue.get(category)!.Code
      child.ParentCode = parentCode
      buckets.get(category)!.push(child)
    } else {
      orphan.push(child)
    }
  }

  const sortByCode = (a: TreeNode, b: TreeNode) =>
    String(a.Extra?.StandardCode ?? '').localeCompare(
      String(b.Extra?.StandardCode ?? ''),
      undefined,
      { numeric: true },
    )

  const result: TreeNode[] = []
  const pushed = new Set<string>()
  for (const c of categories) {
    // 重复字典项（同 Code）只出一个节点，否则会生成两棵同名分类
    if (!seenCode.has(c.Code) || pushed.has(c.Code)) continue
    pushed.add(c.Code)
    const kids = (buckets.get(c.Value) ?? []).sort(sortByCode)
    result.push({
      Code: c.Code,
      Name: c.Label,
      ParentCode: null,
      NodeType: 'virtual',
      IsLeaf: kids.length === 0,
      Extra: { level: 0, Category: c.Value },
      Children: kids,
    })
  }

  if (orphan.length) {
    result.push({
      Code: UNCATEGORIZED_CODE,
      Name: UNCATEGORIZED_LABEL,
      ParentCode: null,
      NodeType: 'virtual',
      IsLeaf: false,
      Extra: { level: 0, Category: '' },
      Children: orphan.sort(sortByCode),
    })
  }

  return result
}
