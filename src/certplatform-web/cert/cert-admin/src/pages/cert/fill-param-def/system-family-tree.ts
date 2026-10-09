/**
 * 企业资料参数左树：[通用] + 体系(系统) → 族 → 标准/版本 三层结构
 *
 * 数据源：
 * - 体系 = `iso_category` 字典（`GET /api/System/Dictionary/items/by-no/iso_category`）
 * - 族   = `cert_standard_family` 表（`POST /api/Admin/Foundation/CertStandardFamily/filter`）
 * - 标准 = `ISOStandardTreeTable/tree/root`（扁平，Extra 含 `Category` / `FamilyCode`）
 *
 * 节点语义：
 * - 通用参数：真实根节点（`Code=''`），可点击 → 右表过滤 `StandardCode=''`
 * - 体系/系统：`NodeType='virtual'`，只展开/折叠不触发过滤
 * - 族：`NodeType='virtual'`，只展开/折叠不触发过滤
 * - 标准：真实叶子节点，可点击 → 右表过滤 `StandardCode = 节点 Code`（ISO GUID）
 */
import type { TreeNode } from '@yzh-core'
import type { CategoryItem } from '../../foundation/iso-standard/group'

/** 体系字典项（CategoryItem）已在本路径导出，此处用同构形状 */

/** 标准节点 Extra（来自 ISOStandardTreeTable/tree/root 的 MapToTreeItem） */
export interface StandardExtra {
  StandardCode: string // 人读编号（如 iso9001）
  VersionYear: number
  Category: string // iso_category DicValue
  FamilyCode: string // 族 GUID（可能为空串）
  StandardName: string
  Description?: string
  [key: string]: any
}

/** 族 DTO（CertStandardFamily 行） */
export interface FamilyDto {
  Code: string
  FamilyNo: string
  FamilyName: string
  Category: string
  Sort?: number
  IsValid?: number
  Description?: string
}

/**
 * 三层树构造：[通用] + 体系 → 族 → 标准
 *
 * @param standards  扁平标准列表（`/tree/root` 已映射为 TreeNode，`Name=StandardName`）
 * @param families   全量族列表（`/filter` 返回的 Items）
 * @param categories iso_category 字典项
 */
export function buildSystemFamilyTree(
  standards: TreeNode[],
  families: FamilyDto[],
  categories: CategoryItem[],
): TreeNode[] {
  // ── 索引：体系字典 ──
  const catMap = new Map<string, CategoryItem>()
  const catSeen = new Set<string>()
  for (const c of categories ?? []) {
    if (!c?.Code || !c?.Value || catSeen.has(c.Code)) continue
    catSeen.add(c.Code)
    catMap.set(c.Value, c)
  }

  // ── 索引：按体系分族的 bucket ──
  const famByCat = new Map<string, FamilyDto[]>()
  for (const f of families ?? []) {
    if (!f?.Code) continue
    const arr = famByCat.get(f.Category) ?? []
    arr.push(f)
    famByCat.set(f.Category, arr)
  }

  // ── 索引：按族分标准的 bucket ──
  const stdByFam = new Map<string, TreeNode[]>()
  const stdOrphan: TreeNode[] = [] // FamilyCode 为空 → 归入「未归族」
  for (const s of standards ?? []) {
    if (!s?.Code || !s.Extra) continue
    const famCode = String((s.Extra as StandardExtra).FamilyCode ?? '').trim()
    if (famCode) {
      const arr = stdByFam.get(famCode) ?? []
      arr.push(s)
      stdByFam.set(famCode, arr)
    } else {
      stdOrphan.push(s)
    }
  }

  const sortBySort =
    (getSort: (x: any) => number) =>
    (a: any, b: any) =>
      (getSort(a) ?? 0) - (getSort(b) ?? 0)

  // ── 构造子树 ──
  const buildFamilyNode = (f: FamilyDto, sysCode: string): TreeNode => {
    const stds = (stdByFam.get(f.Code) ?? []).sort(
      sortBySort((s: TreeNode) => Number(s.Extra?.Sort ?? 0)),
    )
    return {
      Code: f.Code,
      Name: `${f.FamilyNo} ${f.FamilyName}`.trim(),
      ParentCode: sysCode,
      NodeType: 'virtual',
      IsLeaf: stds.length === 0,
      IsValid: f.IsValid ?? 1,
      Extra: {
        _level: 1,
        Category: f.Category,
        FamilyNo: f.FamilyNo,
        FamilyName: f.FamilyName,
        Sort: f.Sort,
      },
      Children: stds.map((s) => ({
        ...s,
        ParentCode: f.Code,
        NodeType: undefined,
        IsLeaf: true,
        Extra: {
          ...(s.Extra ?? {}),
          level: 2,
        },
        Children: [],
      })),
    }
  }

  const result: TreeNode[] = []

  // ① 真实根节点：通用参数（Code=''，可点击 → 右表 StandardCode=''）
  result.push({
    Code: '',
    Name: '通用参数',
    ParentCode: null,
    NodeType: undefined,
    IsLeaf: true,
    Extra: { level: -1 },
    Children: [],
  })

  // ② 体系 → 族 → 标准
  for (const c of categories ?? []) {
    if (!catSeen.has(c.Code)) continue
    const fams = [...(famByCat.get(c.Value) ?? [])].sort(
      sortBySort((f: FamilyDto) => f.Sort ?? 0),
    )

    // 该体系下「未归族」的标准（FamilyCode 空，但 Category 匹配）
    const catOrphans = stdOrphan.filter(
      (s) => String((s.Extra as StandardExtra).Category ?? '') === c.Value,
    )

    const children: TreeNode[] = fams.map((f) => buildFamilyNode(f, c.Code))

    // 未归族的标准作为体系的直接子节点（仍真实可点，但层级 1）
    if (catOrphans.length) {
      children.push({
        Code: `__ungrouped__${c.Value}`,
        Name: '未归族',
        ParentCode: c.Code,
        NodeType: 'virtual',
        IsLeaf: false,
        Extra: { _level: 0.5, Category: c.Value },
        Children: catOrphans
          .sort(sortBySort((s: TreeNode) => Number(s.Extra?.Sort ?? 0)))
          .map((s) => ({
            ...s,
            ParentCode: `__ungrouped__${c.Value}`,
            NodeType: undefined,
            IsLeaf: true,
            Extra: { ...(s.Extra ?? {}), level: 1 },
            Children: [],
          })),
      })
    }

    result.push({
      Code: c.Code,
      Name: c.Label,
      ParentCode: null,
      NodeType: 'virtual',
      IsLeaf: children.length === 0,
      Extra: { _level: 0, Category: c.Value },
      Children: children,
    })
  }

  return result
}
