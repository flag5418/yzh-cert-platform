/**
 * ISOClauseLogic — 条款管理页（左树：体系 → 族 → 标准；右表：条款树）
 *
 * ★ 布局裁决（2026-11-05）：与 iso-standard（/cert/iso-standard）同构 ——
 *   左树选中标准节点 → 右表展示该标准下的条款树（增 / 改 / 删 / 批量删除 + 行内新增下级）。
 *   （旧版 iso-clause 为 24 行简单列表页，b7862bd6 已删；今日「顶部下拉 + 条款树」版本
 *   布局不符，回归「左树标准树 + 右表条款」。）
 *
 * 数据访问规则：
 * - res.data：ApiResponse 顶层（camelCase）
 * - 业务行 r：PascalCase 字段（r.Code, r.StandardCode, r.IsValid）
 * - TreeNode：统一 PascalCase（node.Code / node.Name / node.Extra，见 @yzh-core types/tree）
 * - formData：PascalCase key（YzhForm 双向绑定）
 *
 * 架构：
 * - 左树：**体系（iso_category 字典） → 族（cert_standard_family） → 标准** 三层，
 *   全只读（不可增删改），视图层重组（见 ./group.ts）
 * - 右表：条款树形表格（整树加载、默认全展开、行内新增下级）
 * - 后端：左树（/tree/root）与条款 CRUD（/add /update /delete）走 ISOStandardTreeTable；
 *   条款树数据（扁平 → 本地组树）走 ISOClause/getTree
 *
 * 继承 TreeTableLogic 获得全套左树右表能力。
 * 菜单：MENU_ISO_CLAUSE（/cert/iso-clause，位于「基础资料」下）
 *
 * 根节点语义：条款 ParentCode = null（非菜单的 '0'）
 */
import { yzhApi } from '@yzh-core/api/client'
import { getCertStandardFamilyList } from '@share/api/cert/cert-standard-family'
import {
  TreeTableLogic,
  confirmOrFalse,
  type ApiResponse,
  type TreeNode,
  type YzhAction,
  type YzhFormField,
} from '@yzh-core'
import { ElMessage } from 'element-plus'
import { ref } from 'vue'
import {
  buildCategoryFamilyStandardTree,
  type CategoryItem,
  type FamilyItem,
} from './group'

// ========================================================
// 模块级工具：扁平条款 → 树 / 前端过滤
// ========================================================

type ClauseRow = Record<string, any>

function byClauseNumber(a: ClauseRow, b: ClauseRow): number {
  return String(a.ClauseNumber || '').localeCompare(String(b.ClauseNumber || ''), undefined, {
    numeric: true,
  })
}

/** 扁平列表 → 树（Children：PascalCase，与 TreeNode 规范一致；根 = ParentCode 为空；同级按条款编号数字序） */
function buildClauseTree(flat: ClauseRow[]): ClauseRow[] {
  const map = new Map<string, ClauseRow>()
  flat.forEach((c) => {
    if (c?.Code) map.set(c.Code, { ...c, Children: [] })
  })
  const roots: ClauseRow[] = []
  flat.forEach((c) => {
    const node = map.get(c.Code)
    if (!node) return
    const parent = c.ParentCode ? map.get(c.ParentCode) : undefined
    if (parent) parent.Children.push(node)
    else roots.push(node)
  })
  const sortRec = (nodes: ClauseRow[]) => {
    nodes.sort(byClauseNumber)
    nodes.forEach((n) => sortRec(n.Children || []))
  }
  sortRec(roots)
  return roots
}

function countNodes(nodes: ClauseRow[]): number {
  return nodes.reduce((sum, n) => sum + 1 + countNodes(n.Children || []), 0)
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

/** 扁平 → TreeSelect 选项树（value=Code，label=编号+标题；可选排除子树）
 *  Children：PascalCase，与树表 / TreeNode 规范一致；字段映射经 formFields 的 `props` 声明 */
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
      Children: [] as any[],
    })
  })
  const roots: any[] = []
  available.forEach((c) => {
    const node = map.get(c.Code)
    if (!node) return
    const parent = c.ParentCode ? map.get(c.ParentCode) : undefined
    if (parent) parent.Children.push(node)
    else roots.push(node)
  })
  const sortRec = (nodes: any[]) => {
    nodes.sort((a, b) =>
      String(a.ClauseNumber || '').localeCompare(String(b.ClauseNumber || ''), undefined, {
        numeric: true,
      }),
    )
    nodes.forEach((n) => sortRec(n.Children || []))
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
    const children = filterClauseTree(node.Children || [], searchValues)
    if (children.length > 0 || matchesSearchValues(node, searchValues)) {
      result.push({ ...node, Children: children })
    }
  }
  return result
}

export class ISOClauseLogic extends TreeTableLogic<any> {
  controllerName = 'Admin/Foundation/ISOStandardTreeTable'

  // ──── 上级条款 TreeSelect 选项（编辑/新增时按当前标准加载） ────
  parentOptions = ref<any[]>([])

  // ──── 确认弹窗显示名：条款编号 + 标题 ────
  protected override entityName(row: any): string {
    const num = row?.ClauseNumber ?? ''
    const title = row?.Title ?? ''
    return `${num} ${title}`.trim()
  }

  // ========================================================
  // 左树：体系（分类） → 族 → 标准 分组（视图层，后端仍返回扁平）
  // ========================================================

  /**
   * 节点是否为「标准」节点（唯一可选中、可维护条款的节点）。
   *
   * 左树三层：体系(`NodeType='virtual'`) → 族(`NodeType='family'`) → 标准(`NodeType` 空)。
   * 判据用「非 virtual 且非 family」而非 `level===2`：字典为空的降级模式下
   * `buildCategoryFamilyStandardTree` 直接返回扁平标准（`level` 缺省），按 level 判会被全拦。
   */
  private isStandardNode(node: TreeNode | null | undefined): boolean {
    return !!node && node.NodeType !== 'virtual' && node.NodeType !== 'family'
  }

  /**
   * 节点是否可选中并加载条款：左树只有标准节点可选（体系 / 族都不可选）。
   *
   * ★ index.vue 的**点击守卫**与**空态 v-if 必须共用本谓词** —— 两处各写一遍
   *   曾因读了不存在的 `Extra._level`（数据源写的是 `level`）导致守卫恒 early return、
   *   右表永不挂载、条款接口一次都不发（2026-10-09 事故）。
   */
  isSelectableNode(node: TreeNode | null | undefined): boolean {
    return this.isStandardNode(node)
  }

  /**
   * 覆盖树根加载：先走内核取**扁平**标准，再按 `iso_category` 字典 + `cert_standard_family`
   * 列表重组成「体系 → 族 → 标准」三层。
   *
   * ⚠️ 必须用 `treeSide.setNodes()`（内部会 `rebuildIndex()`）——
   * 直接赋 `treeData` 不重建索引，`updateTreeNode`/`deleteTreeNode` 会找不到节点。
   */
  override async loadTreeRoot(): Promise<void> {
    await super.loadTreeRoot()
    let categories: CategoryItem[] = []
    let families: FamilyItem[] = []
    try {
      const res = await yzhApi.get<ApiResponse<CategoryItem[]>>(
        '/api/System/Dictionary/items/by-no/iso_category',
      )
      categories = res?.data ?? []
    } catch {
      categories = []
    }
    try {
      const res = await getCertStandardFamilyList()
      families = (res?.data?.Items ?? []) as unknown as FamilyItem[]
    } catch {
      families = []
    }
    if (!categories.length) return
    this.treeSide.setNodes(
      buildCategoryFamilyStandardTree(this.treeData, categories, families),
    )
  }

  /**
   * 覆盖树节点操作：体系 / 族 / 标准节点都**不挂操作按钮** ——
   * 左树全只读（标准的增删改在「标准管理」/cert/standard-manage 完成）。
   */
  override get nodeActions(): (node: TreeNode) => YzhAction[] {
    return () => []
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
            // el-tree-select 字段映射（选项树 Children 为 PascalCase，⛔ 非默认 'children'）
            props: { label: 'label', value: 'value', children: 'Children' },
          },
        }
      }
      return f
    })
  }

  // ──── 行操作：菜单模式（新增下级 + 基类 edit/delete/toggle-valid） ────
  override get rowActions(): YzhAction[] | ((row: any) => YzhAction[]) {
    const base = super.rowActions
    const addChild: YzhAction = { key: 'add-child', text: '新增下级' }
    // 基类可能是函数式（按行状态二选一的「禁用/启用」）→ 必须按行组合，
    // 用 Array.isArray 兜空会让全部行按钮静默消失
    if (typeof base === 'function') {
      return (row: any) => [addChild, ...base(row)]
    }
    return [addChild, ...base]
  }

  /** 拉取当前标准全量条款并构建上级候选（编辑时排除自身及子孙） */
  private async loadParentOptions(excludeCode?: string): Promise<void> {
    // 体系/族节点无条款语义：不发请求（standardCode 传字典项/族 Code 会查出空/脏数据）
    if (!this.selectedNode || !this.isStandardNode(this.selectedNode)) {
      this.parentOptions.value = []
      return
    }
    try {
      const res = await yzhApi.get<ApiResponse<ClauseRow[]>>(
        '/api/Admin/Foundation/ISOClause/getTree',
        { standardCode: this.selectedNode.Code, includeDisabled: 'true' },
      )
      const flat = res?.data ?? []
      this.parentOptions.value = buildParentOptions(flat, excludeCode)
    } catch {
      this.parentOptions.value = []
    }
  }

  // ========================================================
  // 数据加载（供 YzhTreeTable data-loader 使用 · 整树 + 前端过滤）
  // ========================================================

  /**
   * YzhTreeTable 数据加载器：返回条款树根节点数组。
   * - 未选中标准 / 选中**体系/族节点** → 空（★ `onNodeClick` 直接调 `_tableRef.refresh()`
   *   走的是本方法，**不经过** `loadPageWithoutTree`，所以必须自己挡）
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
    if (!this.selectedNode || !this.isStandardNode(this.selectedNode)) {
      return { rows: [], total: 0 }
    }
    const standardCode = this.selectedNode.Code
    try {
      const res = await yzhApi.get<ApiResponse<ClauseRow[]>>(
        '/api/Admin/Foundation/ISOClause/getTree',
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
    if (!this.selectedNode || !this.isStandardNode(this.selectedNode)) {
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
      Sort: 0,
      IsValid: 1,
    })
    await this.loadParentOptions()
    this.dialogVisible.value = true
    return true
  }

  /** 打开「新增下级」弹窗（预填 ParentCode = row.Code，仍可改上级） */
  async openAddClauseChild(row: ClauseRow): Promise<boolean> {
    if (!this.selectedNode || !this.isStandardNode(this.selectedNode)) {
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
      Sort: 0,
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
    const { Children: _children, ...rest } = row
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
      delete submitData.Children
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
  // 页面动作收口（模板只允许出现 `logic.xxx` 与 `$event`）
  // ========================================================

  /** 新增顶级条款：未选标准时给提示（原来在页面上判断） */
  async addClauseWithToast(): Promise<void> {
    const ok = await this.openAddClauseDialog()
    if (!ok) ElMessage.warning('请先选择标准')
  }

  /** 新增下级条款（行内 add-child） */
  async addClauseChildWithToast(row: ClauseRow): Promise<void> {
    const ok = await this.openAddClauseChild(row)
    if (!ok) ElMessage.warning('请先选择标准')
  }

  /** 删除条款：确认 → 删除 → 提示（失败只提示，不把异常抛给模板） */
  async deleteClauseWithConfirm(row: ClauseRow): Promise<void> {
    const name = this.entityName(row) || '该条款'
    const ok = await confirmOrFalse(`确定删除条款【${name}】？`, '删除确认', {
      type: 'warning',
    })
    if (!ok) return
    try {
      await this.deleteClause(row)
      ElMessage.success('删除成功')
    } catch (e) {
      this.reportError(e, '删除失败')
    }
  }

  /**
   * 批量删除：空选校验 → 确认 → 整批删除 → 清选中 → 提示。
   *
   * ★ 选中集合直接用基类 `selectedRows`（模板 `@selection-change="logic.onSelectionChange"` 写入），
   *   ⛔ 不再由页面自己维护一份 ref —— 两份状态迟早不一致。
   */
  async batchDeleteClausesWithConfirm(): Promise<void> {
    const rows = this.selectedRows.value
    if (rows.length === 0) {
      ElMessage.warning('请先选择要删除的条款')
      return
    }
    const ok = await confirmOrFalse(`确定删除选中的 ${rows.length} 个条款？`, '批量删除', {
      type: 'warning',
    })
    if (!ok) return
    try {
      await this.batchDeleteClauses(rows)
      this.selectedRows.value = []
      this._tableRef?.clearSelection?.()
      ElMessage.success('批量删除成功')
    } catch (e) {
      this.reportError(e, '批量删除失败')
    }
  }

  /** 提交条款表单：成败提示（树模式提交后整树 refresh，见 `submitClauseForm`） */
  async submitClauseFormWithToast(): Promise<void> {
    try {
      await this.submitClauseForm()
      ElMessage.success(this.dialogMode.value === 'add' ? '新增成功' : '修改成功')
    } catch (e) {
      this.reportError(e, '保存失败')
    }
  }

  /** 启停条款：确认 → 接口 → 整树 refresh（树表不靠局部改行） */
  async toggleClauseIsValid(row: ClauseRow): Promise<void> {
    await this.toggleRowIsValidWithConfirm(row)
    await this.refresh()
  }

  /**
   * 行操作统一入口（`YzhTreeTable @row-action`）。
   *
   * ★ 必须写成**箭头函数属性**：模板里 `@row-action="logic.onRowAction"` 传的是函数引用，
   *   普通方法会丢 `this`。
   */
  onRowAction = async (action: string, row: ClauseRow): Promise<void> => {
    if (action === 'add-child') {
      await this.addClauseChildWithToast(row)
    } else if (action === 'edit') {
      await this.openEditClauseDialog(row)
    } else if (action === 'delete') {
      await this.deleteClauseWithConfirm(row)
    } else if (action === 'toggle-valid') {
      await this.toggleClauseIsValid(row)
    }
  }
}

export default ISOClauseLogic
