/**
 * ISOStandardTreeTableLogic — ISO 标准 → 条款 左树右表 Logic（TreeTable 架构）
 *
 * 数据访问规则：
 * - res.data：ApiResponse 顶层（camelCase）
 * - 业务行 r：PascalCase 字段（r.Code, r.StandardCode, r.Enable）
 * - TreeNode：统一 PascalCase（node.Code / node.Name / node.Extra，见 @yzh-core types/tree）
 * - formData：PascalCase key（YzhForm 双向绑定）
 *
 * 架构：
 * - 左侧：标准树（扁平，所有标准为根节点，不允许增加下级）
 * - 右侧：条款树形表格（菜单模式：整树加载、默认全展开、行内新增下级）
 * - 继承 TreeTableLogic 获得全套左树右表能力
 *
 * 根节点语义：条款 ParentCode = null（非菜单的 '0'）
 */

import { yzhApi } from '@yzh-core/api/client'
import {
  TreeTableLogic,
  type ApiResponse,
  type TreeNode,
  type YzhAction,
  type YzhFormField,
} from '@yzh-core'
import { reactive, ref } from 'vue'

// ========================================================
// 模块级工具：扁平条款 → 树 / 前端过滤
// ========================================================

type ClauseRow = Record<string, any>

function byClauseNumber(a: ClauseRow, b: ClauseRow): number {
  return String(a.ClauseNumber || '').localeCompare(String(b.ClauseNumber || ''), undefined, {
    numeric: true,
  })
}

/** 扁平列表 → 树（children；根 = ParentCode 为空；同级按条款编号数字序） */
function buildClauseTree(flat: ClauseRow[]): ClauseRow[] {
  const map = new Map<string, ClauseRow>()
  flat.forEach((c) => {
    if (c?.Code) map.set(c.Code, { ...c, children: [] })
  })
  const roots: ClauseRow[] = []
  flat.forEach((c) => {
    const node = map.get(c.Code)
    if (!node) return
    const parent = c.ParentCode ? map.get(c.ParentCode) : undefined
    if (parent) parent.children.push(node)
    else roots.push(node)
  })
  const sortRec = (nodes: ClauseRow[]) => {
    nodes.sort(byClauseNumber)
    nodes.forEach((n) => sortRec(n.children || []))
  }
  sortRec(roots)
  return roots
}

function countNodes(nodes: ClauseRow[]): number {
  return nodes.reduce((sum, n) => sum + 1 + countNodes(n.children || []), 0)
}

/** 收集 code 及全部子孙 Code（编辑时从上级候选中排除，防自环） */
function collectSubtreeCodes(flat: ClauseRow[], rootCode: string): Set<string> {
  const exclude = new Set<string>([rootCode])
  let grew = true
  while (grew) {
    grew = false
    for (const c of flat) {
      if (c.ParentCode && exclude.has(c.ParentCode) && !exclude.has(c.Code)) {
        exclude.add(c.Code)
        grew = true
      }
    }
  }
  return exclude
}

/** 扁平 → TreeSelect 选项树（value=Code，label=编号+标题；可选排除子树） */
function buildParentOptions(flat: ClauseRow[], excludeCode?: string): any[] {
  const exclude = excludeCode ? collectSubtreeCodes(flat, excludeCode) : null
  const available = exclude ? flat.filter((c) => !exclude.has(c.Code)) : flat
  const map = new Map<string, any>()
  available.forEach((c) => {
    if (!c?.Code) return
    map.set(c.Code, {
      value: c.Code,
      label: `${c.ClauseNumber ?? ''} ${c.Title ?? ''}`.trim() || c.Code,
      ClauseNumber: c.ClauseNumber ?? '',
      children: [] as any[],
    })
  })
  const roots: any[] = []
  available.forEach((c) => {
    const node = map.get(c.Code)
    if (!node) return
    const parent = c.ParentCode ? map.get(c.ParentCode) : undefined
    if (parent) parent.children.push(node)
    else roots.push(node)
  })
  const sortRec = (nodes: any[]) => {
    nodes.sort((a, b) =>
      String(a.ClauseNumber || '').localeCompare(String(b.ClauseNumber || ''), undefined, {
        numeric: true,
      }),
    )
    nodes.forEach((n) => sortRec(n.children || []))
  }
  sortRec(roots)
  return roots
}

/** 节点是否命中搜索（多条件 AND，字段子串、大小写不敏感） */
function matchesSearchValues(node: ClauseRow, searchValues: Record<string, any>): boolean {
  const fields = ['ClauseNumber', 'Title']
  for (const field of fields) {
    const value = searchValues[field]
    if (value == null || value === '') continue
    const text = String(node[field] ?? '').toLowerCase()
    if (!text.includes(String(value).toLowerCase())) return false
  }
  return true
}

/**
 * 树前端过滤：保留命中节点及其祖先（保证层级上下文）
 * 不命中且无命中后代的子树整支裁掉
 */
function filterClauseTree(nodes: ClauseRow[], searchValues: Record<string, any>): ClauseRow[] {
  const hasCriteria = ['ClauseNumber', 'Title'].some((f) => {
    const v = searchValues[f]
    return v != null && v !== ''
  })
  if (!hasCriteria) return nodes

  const result: ClauseRow[] = []
  for (const node of nodes) {
    const children = filterClauseTree(node.children || [], searchValues)
    if (children.length > 0 || matchesSearchValues(node, searchValues)) {
      result.push({ ...node, children })
    }
  }
  return result
}

export class ISOStandardTreeTableLogic extends TreeTableLogic<any> {
  controllerName = 'Foundation/ISOStandardTreeTable'

  // ──── 标准弹窗状态（树节点表单，与基类行表单弹窗分开） ────
  stdDialogVisible = ref(false)
  stdDialogMode = ref<'add' | 'edit'>('add')
  stdSubmitting = ref(false)
  stdFormData = reactive<Record<string, any>>({})
  stdEditingNode = ref<TreeNode | null>(null)

  // ──── 上级条款 TreeSelect 选项（编辑/新增时按当前标准加载） ────
  parentOptions = ref<any[]>([])

  // ──── 确认弹窗显示名：条款编号 + 标题 ────
  protected override entityName(row: any): string {
    const num = row?.ClauseNumber ?? ''
    const title = row?.Title ?? ''
    return `${num} ${title}`.trim()
  }

  // ──── 表单字段：ParentCode → treeSelect（可改上级 / 层级调整） ────
  override get formFields(): YzhFormField[] {
    return super.formFields.map((f) => {
      if (f.prop === 'ParentCode') {
        return {
          ...f,
          type: 'treeSelect',
          options: this.parentOptions.value,
          placeholder: '选择上级条款，留空为顶级',
          fieldProps: {
            'value-key': 'value',
            'check-strictly': true,
            filterable: true,
            clearable: true,
            'render-after-expand': false,
          },
        }
      }
      return f
    })
  }

  // ──── 行操作：菜单模式（新增下级 + 基类 edit/delete/toggle-valid） ────
  override get rowActions(): YzhAction[] {
    const base = super.rowActions
    const list = Array.isArray(base) ? base : []
    return [{ key: 'add-child', text: '新增下级' }, ...list]
  }

  /** 拉取当前标准全量条款并构建上级候选（编辑时排除自身及子孙） */
  private async loadParentOptions(excludeCode?: string): Promise<void> {
    if (!this.selectedNode) {
      this.parentOptions.value = []
      return
    }
    try {
      const res = await yzhApi.get<ApiResponse<ClauseRow[]>>(
        '/api/Foundation/ISOClause/getTree',
        { standardCode: this.selectedNode.Code, includeDisabled: 'true' },
      )
      const flat = res?.data ?? []
      this.parentOptions.value = buildParentOptions(flat, excludeCode)
    } catch {
      this.parentOptions.value = []
    }
  }

  // ========================================================
  // 数据加载（供 YzhTable data-loader 使用 · 整树 + 前端过滤）
  // ========================================================

  /**
   * YzhTable 数据加载器：返回条款树根节点数组。
   * - 未选中标准 → 空
   * - GET ISOClause/getTree 取扁平 → 本地组树
   * - 条款编号/标题 → 前端关键字过滤（保留命中节点的祖先链）
   * - 关分页：total = 树中可见节点数（仅展示用）
   */
  async dataLoader(params: {
    page?: number
    rows?: number
    sort?: string
    order?: string
    ClauseNumber?: string
    Title?: string
    [key: string]: any
  }): Promise<{ rows: any[]; total: number }> {
    if (!this.selectedNode) {
      return { rows: [], total: 0 }
    }
    const standardCode = this.selectedNode.Code
    try {
      const res = await yzhApi.get<ApiResponse<ClauseRow[]>>(
        '/api/Foundation/ISOClause/getTree',
        {
          standardCode,
          includeDisabled: this.showDisabled.value ? 'true' : 'false',
        },
      )
      let flat = res?.data ?? []
      if (!this.showDisabled.value) {
        flat = flat.filter((c) => (c.IsValid ?? 1) === 1)
      }
      const roots = buildClauseTree(flat)
      const { page: _p, rows: _r, sort: _s, order: _o, ...searchValues } = params || {}
      const filtered = filterClauseTree(roots, searchValues)
      return { rows: filtered, total: countNodes(filtered) }
    } catch (_e: any) {
      return { rows: [], total: 0 }
    }
  }

  // ========================================================
  // 条款 CRUD（复用基类 dialogVisible / formData / submitting）
  // ========================================================

  /** 打开新增顶级条款弹窗（ParentCode = null，可再选上级） */
  async openAddClauseDialog(): Promise<boolean> {
    if (!this.selectedNode) {
      return false
    }
    this.dialogMode.value = 'add'
    this.formGroupIndex.value = '0'
    this.resetObject(this.formData)
    const tmpl = (this.config.value as any)?.NewEntity || {}
    Object.assign(this.formData, {
      ...tmpl,
      StandardCode: this.selectedNode.Code,
      ParentCode: null,
      SortOrder: 0,
      IsValid: 1,
    })
    await this.loadParentOptions()
    this.dialogVisible.value = true
    return true
  }

  /**
   * 打开「新增下级」弹窗（预填 ParentCode = row.Code，仍可改上级）
   */
  async openAddClauseChild(row: ClauseRow): Promise<boolean> {
    if (!this.selectedNode) {
      return false
    }
    this.dialogMode.value = 'add'
    this.formGroupIndex.value = '0'
    this.resetObject(this.formData)
    const tmpl = (this.config.value as any)?.NewEntity || {}
    Object.assign(this.formData, {
      ...tmpl,
      StandardCode: this.selectedNode.Code,
      ParentCode: row.Code,
      SortOrder: 0,
      IsValid: 1,
    })
    await this.loadParentOptions()
    this.dialogVisible.value = true
    return true
  }

  /** 打开编辑条款弹窗（可修改上级以调整层级；排除自身及子孙防环） */
  async openEditClauseDialog(row: ClauseRow): Promise<void> {
    this.dialogMode.value = 'edit'
    this.formGroupIndex.value = '0'
    this.resetObject(this.formData)
    const { children: _children, ...rest } = row
    Object.assign(this.formData, rest)
    this.formData.ParentCode = row.ParentCode ?? null
    await this.loadParentOptions(row.Code)
    this.dialogVisible.value = true
  }

  /** 提交条款表单（树模式：成功后整树 refresh，不用 insertRow 局部插行） */
  async submitClauseForm(): Promise<void> {
    this.submitting.value = true
    try {
      const submitData: Record<string, any> = { ...this.formData }
      if (submitData.ParentCode === '' || submitData.ParentCode === undefined) {
        submitData.ParentCode = null
      }
      delete submitData.children
      if (this.dialogMode.value === 'add') {
        await this.add(submitData)
      } else {
        await this.update(submitData)
      }
      this.dialogVisible.value = false
      await this.refresh()
    } finally {
      this.submitting.value = false
    }
  }

  /** 删除条款（树模式：整树 refresh） */
  async deleteClause(row: ClauseRow): Promise<void> {
    await this.delete([row.Code])
    await this.refresh()
  }

  /**
   * 批量删除条款：整批提交。
   * 父子同批时后端允许（子在删除集合内）；仅勾父不勾子时后端有子禁删拒绝。
   */
  async batchDeleteClauses(rows: ClauseRow[]): Promise<void> {
    const codes = rows.map((r) => r.Code).filter(Boolean)
    if (codes.length === 0) return
    await this.delete(codes)
    await this.refresh()
  }

  // ========================================================
  // 标准（树节点）CRUD
  // ========================================================

  /** 打开新增标准弹窗 */
  openAddStdDialog(): void {
    this.stdDialogMode.value = 'add'
    this.stdEditingNode.value = null
    this.resetObject(this.stdFormData)
    const tmpl = (this.treeFormConfig as any)?.NewEntity || {}
    Object.assign(this.stdFormData, {
      ...tmpl,
      Code: crypto.randomUUID?.() || `${Date.now()}`,
    })
    this.stdDialogVisible.value = true
  }

  /** 打开编辑标准弹窗 */
  openEditStdDialog(node: TreeNode): void {
    this.stdDialogMode.value = 'edit'
    this.stdEditingNode.value = node
    this.resetObject(this.stdFormData)
    const extra = (node.Extra as any) || {}
    Object.assign(this.stdFormData, {
      Code: node.Code,
      StandardName: node.Name,
      ...extra,
    })
    this.stdDialogVisible.value = true
  }

  /** 提交标准表单 */
  async submitStdForm(): Promise<void> {
    this.stdSubmitting.value = true
    try {
      if (this.stdDialogMode.value === 'add') {
        await this.addRootNode(this.stdFormData)
      } else {
        await this.updateTreeNode(
          this.stdEditingNode.value!,
          this.stdFormData.StandardName ?? '',
          this.stdFormData,
        )
      }
      this.stdDialogVisible.value = false
    } finally {
      this.stdSubmitting.value = false
    }
  }

  /** 删除标准（skipConfirm=true，由页面确认弹窗后再调用） */
  async deleteStd(node: TreeNode): Promise<void> {
    await this.deleteTreeNode(node, true)
    // 右表走 YzhTable dataLoader 刷新（selectedNode 可能已被清空）
    await this.refresh()
  }
}

export default ISOStandardTreeTableLogic
