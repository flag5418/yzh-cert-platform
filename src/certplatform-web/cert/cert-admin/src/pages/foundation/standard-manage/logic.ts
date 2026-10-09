/**
 * StandardManageLogic — 标准管理页（体系 → 族 → 版本/条款）
 *
 * 架构：
 * - 左树：体系(字典固定) → 族(cert_standard_family)
 *         双击族节点 → 右表加载该族下的版本列表（dataLoader 注入 FamilyCode 过滤）
 * - 点击版本节点 → 右侧切换条款树（复用 ISOClause getTree）
 *
 * 设计约束（2026-10-08 三层体系裁决）：
 * - 版本增删改在本页右表完成（2026-10-09 用户裁决：添加编辑/删除/启用禁用，移除「查看条款」）
 * - 左树无增删改按钮（nodeActions = []）
 * - 族 CRUD 通过弹窗完成，不走内核 add/edit/delete（独立实现）
 */
import {
  TreeTableCore,
  type ApiResponse,
  type TreeNode,
  yzhApi,
} from '@yzh-core'
import { ref } from 'vue'
import type { CategoryItem } from '../iso-standard/group'
import {
  getCertStandardFamilyList,
  addCertStandardFamily,
  updateCertStandardFamily,
  deleteCertStandardFamily,
  toggleCertStandardFamilyValid,
} from '@share/api/cert/cert-standard-family'
import {
  updateISOStandard,
  deleteISOStandard,
  toggleISOStandardValid,
} from '@share/api/cert/iso-standard'
import { ElMessage } from 'element-plus'
import { confirmOrFalse } from '@yzh-core'

// ──── 类型 ──────────────────────────────────────────────────────────────────────

export interface FamilyRow {
  Code: string
  FamilyNo: string
  FamilyName: string
  Category: string
  Description?: string
  Sort?: number
  IsValid: number
}

export interface VersionRow {
  Code: string
  StandardCode: string
  StandardName: string
  VersionYear: number
  Category: string
  FamilyCode: string | null
  Sort?: number
  IsValid: number
}

type PanelMode = 'family' | 'version'

// ──── Logic 类 ──────────────────────────────────────────────────────────────────

export class StandardManageLogic extends TreeTableCore<any> {
  controllerName = 'Admin/Foundation/CertStandardFamily'

  // ── 自定义状态 ──
  /** 族列表（族节点选中时加载） */
  versions = ref<VersionRow[]>([])
  /** 条款树（版本节点选中时加载） */
  clauses = ref<any[]>([])
  /** 条款树加载态 */
  clausesLoading = ref(false)
  /** 当前面板模式：'family'=族下版本列表 / 'version'=条款树 */
  panelMode = ref<PanelMode | null>(null)
  /** 当前选中族行 */
  currentFamily = ref<FamilyRow | null>(null)
  /** 当前选中版本行 */
  currentVersion = ref<VersionRow | null>(null)
  /** 体系字典映射 */
  categoryMap = ref<Map<string, string>>(new Map())

  // ── 族表单 ──
  formVisible = ref(false)
  formMode = ref<'add' | 'edit'>('add')
  formSubmitting = ref(false)
  formData = ref<Record<string, any>>({})
  editingFamily = ref<FamilyRow | null>(null)

  // ── 版本表单 ──
  versionDialogVisible = ref(false)
  versionMode = ref<'add' | 'edit'>('add')
  versionSubmitting = ref(false)
  versionFormData = ref<Record<string, any>>({})
  editingVersion = ref<VersionRow | null>(null)

  // ── 条款表单 ──
  clauseDialogVisible = ref(false)
  clauseMode = ref<'add' | 'edit'>('add')
  clauseSubmitting = ref(false)
  clauseFormData = ref<Record<string, any>>({})
  editingClause = ref<any>(null)
  parentOptions = ref<any[]>([])

  // ── 覆盖 loadConfig：自定义树配置 ──
  //
  // ⚠️ 本页后端是**单表控制器**（YzhControllerBase<CertStandardFamily>）—— 只有 /config，
  //    没有 TreeTableControllerBase 的 /treepconfig。因此不能沿用 core 默认实现。
  //    历史缺陷（2026-10-08 修复）：旧实现只写 config.value、不写 treeTableConfig.value，
  //    而 `if (this.treeTableConfig.value)` 恒为假 ⇒ 下面的 TreeConfig 覆盖是**死代码** ⇒
  //    core `relateField` 回落 'ParentCode' ⇒ 点族节点发 `CertStandardFamily/filter`
  //    `Filters:[{Field:'ParentCode'}]` ⇒ 400 `Unknown column 'ParentCode' in 'where clause'`
  //    （cert_standard_family 无 ParentCode 列）。
  override async loadConfig(): Promise<void> {
    const res = await this.apiGet<ApiResponse<any>>('/config')
    if (!res?.success) return
    this.config.value = res.data
    // 显式构造 TreeConfig（不能依赖 `if (treeTableConfig.value)` 的真值判断）
    this.treeTableConfig.value = {
      ...(this.treeTableConfig.value ?? {}),
      TableConfig: res.data,
      TreeConfig: {
        ...(this.treeTableConfig.value?.TreeConfig ?? {}),
        // 左树只读：族节点不可编辑/删除/新增子节点
        AllowEdit: false,
        AllowDelete: false,
        AllowAddChild: false,
        AllowRename: false,
        AllowToggle: true,
        // 版本表按 FamilyCode 关联族，非 ParentCode
        RelateField: 'FamilyCode',
      },
    } as any
  }

  // ── 覆盖 refreshTable：右表是自定义 YzhTable（ISOStandard），不走内核 /filter ──
  //
  // ⚠️ core 的 refreshTable → loadPageWithTree 会对**本控制器**（CertStandardFamily）
  //    发 /filter + RelateField 过滤 —— 这与右表真实数据源（ISOStandard）不符。
  //    本页右表由 YzhTable 的 dataLoader 驱动（ISOStandard/filter?FamilyCode=xxx），
  //    因此把内核刷新**重定向到真实表格引用**；引用缺失时（表格尚未挂载）不应发请求 ——
  //    此时表格挂载后自己的 dataLoader 会加载数据。
  override async refreshTable(): Promise<void> {
    if (this.panelMode.value === 'family' && this._tableRef) {
      await this._tableRef.refresh()
    }
  }

  // ── 覆盖 loadTreeRoot：字典 + 族数据组装 ──
  override async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const [dictRes, famRes] = await Promise.all([
        yzhApi
          .get<ApiResponse<CategoryItem[]>>(
            '/api/System/Dictionary/items/by-no/iso_category',
          )
          .catch(() => ({ success: false, data: [] })),
        getCertStandardFamilyList().catch(() => ({ success: false, data: { Items: [] } })),
      ])
      const categories: CategoryItem[] = dictRes?.success ? dictRes.data ?? [] : []
      const families: FamilyRow[] = (famRes as any)?.data?.Items ?? []

      this.categoryMap.value = new Map(
        categories.filter(c => c?.Value).map(c => [c.Value, c.Label]),
      )
      const nodes = buildCategoryFamilyTree(categories, families)
      this.treeSide.setNodes(nodes)
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  // ── 树节点点击：族节点 → 设置面板模式 → 触发 core 刷新表 ──
  override onNodeClick = async (node: TreeNode): Promise<void> => {
    const extra: Record<string, any> = node.Extra ?? {}
    const level = extra._level

    if (level === 1) {
      // 族节点
      this.panelMode.value = 'family'
      this.currentFamily.value = {
        Code: node.Code,
        FamilyNo: extra.FamilyNo ?? '',
        FamilyName: extra.FamilyName ?? '',
        Category: extra.Category ?? '',
        Sort: extra.Sort,
        IsValid: extra.IsValid ?? 1,
      }
      this.currentVersion.value = null
      // 交给 core：设置 selectedNode + 刷新表（→ refreshTable 已重定向到真实版本表）
      this.treeSide.selectedNode.value = node
      this.pagination.page = 1
      await this.refreshTable()
    } else if (level === 2) {
      // 版本节点：加载条款树，不走 core 刷新
      this.panelMode.value = 'version'
      this.currentVersion.value = {
        Code: node.Code,
        StandardCode: extra.StandardCode ?? '',
        StandardName: extra.StandardName ?? '',
        VersionYear: extra.VersionYear ?? 0,
        Category: extra.Category ?? '',
        FamilyCode: extra.FamilyCode ?? null,
        IsValid: extra.IsValid ?? 1,
      }
      this.currentFamily.value = null
      // 条款归属键 = 标准 **Code（GUID）**，不是人读编号 StandardCode（'iso9001'）——
      // cert_iso_clause.StandardCode 存的是 cert_iso_standard.Code
      await this.loadClauses(this.currentVersion.value.Code)
    } else {
      // 体系节点：清空
      this.panelMode.value = null
      this.currentFamily.value = null
      this.currentVersion.value = null
    }
  }

  // ── 数据加载器：族选中态返回版本列表 ──
  override async dataLoader(_params: any): Promise<{ rows: any[]; total: number }> {
    // 仅在族选中态（panelMode=family）下返回版本列表
    if (this.panelMode.value !== 'family' || !this.currentFamily.value) {
      return { rows: [], total: 0 }
    }
    const familyCode = this.currentFamily.value.Code
    try {
      const res = await yzhApi.post<ApiResponse<any>>(
        '/api/Admin/Foundation/ISOStandard/filter',
        {
          Page: 1,
          PageSize: 1000,
          Filters: [{ Field: 'FamilyCode', Operator: 'eq', Value: familyCode }],
        },
      )
      const items = (res as any).data?.Items ?? []
      this.versions.value = (items as any[]).map((r: any) => ({
        Code: r.Code,
        StandardCode: r.StandardCode ?? '',
        StandardName: r.StandardName ?? '',
        VersionYear: Number(r.VersionYear) || 0,
        Category: r.Category ?? '',
        FamilyCode: r.FamilyCode ?? null,
        Sort: r.Sort ?? 0,
        IsValid: r.IsValid ?? 1,
      }))
      return { rows: this.versions.value, total: this.versions.value.length }
    } catch {
      this.versions.value = []
      return { rows: [], total: 0 }
    }
  }

  // ── 族 CRUD ──
  openAddFamily(): void {
    this.formMode.value = 'add'
    this.editingFamily.value = null
    this.formData.value = { IsValid: 1, Sort: 0, Category: 'quality' }
    this.formVisible.value = true
  }

  openEditFamily(family: FamilyRow): void {
    this.formMode.value = 'edit'
    this.editingFamily.value = family
    this.formData.value = {
      Code: family.Code,
      FamilyNo: family.FamilyNo,
      FamilyName: family.FamilyName,
      Category: family.Category,
      Sort: family.Sort ?? 0,
      Description: family.Description ?? '',
      IsValid: family.IsValid ?? 1,
    }
    this.formVisible.value = true
  }

  async submitFamilyForm(): Promise<void> {
    this.formSubmitting.value = true
    try {
      const payload = { ...this.formData.value }
      if (this.formMode.value === 'add') {
        await addCertStandardFamily(payload)
      } else {
        await updateCertStandardFamily(payload)
      }
      this.formVisible.value = false
      ElMessage.success(this.formMode.value === 'add' ? '新增成功' : '修改成功')
      await this.loadTreeRoot()
      // 若当前有选中的族，刷新其版本列表
      if (this.currentFamily.value) {
        await this.loadVersions(this.currentFamily.value.Code)
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '保存失败')
    } finally {
      this.formSubmitting.value = false
    }
  }

  async deleteFamily(family: FamilyRow): Promise<void> {
    const ok = await confirmOrFalse(
      `确定删除族【${family.FamilyNo} ${family.FamilyName}】？\n关联版本将失去族归属（FamilyCode 置空）。`,
      '删除确认',
      { type: 'warning' },
    )
    if (!ok) return
    try {
      await deleteCertStandardFamily([family.Code])
      ElMessage.success('已删除')
      await this.loadTreeRoot()
      if (this.currentFamily.value?.Code === family.Code) {
        this.currentFamily.value = null
        this.panelMode.value = null
        this.versions.value = []
        this.clauses.value = []
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '删除失败')
    }
  }

  async toggleFamilyValid(family: FamilyRow): Promise<void> {
    const action = family.IsValid === 1 ? '禁用' : '启用'
    const ok = await confirmOrFalse(
      `确定${action}族【${family.FamilyNo} ${family.FamilyName}】？`,
      `${action}确认`,
      { type: 'warning' },
    )
    if (!ok) return
    try {
      const res = await toggleCertStandardFamilyValid(family.Code)
      ElMessage.success(res?.data?.IsValid === 1 ? '已启用' : '已禁用')
      await this.loadTreeRoot()
    } catch (e: any) {
      ElMessage.error(e?.message ?? '操作失败')
    }
  }

  // ── 版本表单 CRUD ──
  openAddVersionDialog(): void {
    if (!this.currentFamily.value) return
    this.versionMode.value = 'add'
    this.editingVersion.value = null
    this.versionFormData.value = {
      StandardCode: '',
      StandardName: '',
      VersionYear: new Date().getFullYear(),
      Category: this.currentFamily.value.Category,
      FamilyCode: this.currentFamily.value.Code,
      IsValid: 1,
      Sort: 0,
    }
    this.versionDialogVisible.value = true
  }

  openEditVersionDialog(row: VersionRow): void {
    this.versionMode.value = 'edit'
    this.editingVersion.value = row
    this.versionFormData.value = {
      Code: row.Code,
      StandardCode: row.StandardCode,
      StandardName: row.StandardName,
      VersionYear: row.VersionYear,
      Category: row.Category,
      FamilyCode: row.FamilyCode,
      IsValid: row.IsValid ?? 1,
      Sort: row.Sort ?? 0,
    }
    this.versionDialogVisible.value = true
  }

  async submitVersionForm(): Promise<void> {
    this.versionSubmitting.value = true
    try {
      if (this.versionMode.value === 'add') {
        await yzhApi.post('/api/Admin/Foundation/ISOStandard/add', this.versionFormData.value)
      } else {
        await updateISOStandard(this.versionFormData.value)
      }
      this.versionDialogVisible.value = false
      ElMessage.success(this.versionMode.value === 'add' ? '新增版本成功' : '修改版本成功')
      if (this.currentFamily.value) {
        await this.loadVersions(this.currentFamily.value.Code)
        await this.refreshTable()
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '保存失败')
    } finally {
      this.versionSubmitting.value = false
    }
  }

  async deleteVersion(row: VersionRow): Promise<void> {
    const ok = await confirmOrFalse(
      `确定删除版本【${row.StandardCode} ${row.StandardName} ${row.VersionYear}】？\n关联条款将不可见。`,
      '删除确认',
      { type: 'warning' },
    )
    if (!ok) return
    try {
      await deleteISOStandard([row.Code])
      ElMessage.success('删除成功')
      if (this.currentFamily.value) {
        await this.loadVersions(this.currentFamily.value.Code)
        await this.refreshTable()
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '删除失败')
    }
  }

  async toggleVersionValid(row: VersionRow): Promise<void> {
    const action = row.IsValid === 1 ? '禁用' : '启用'
    const ok = await confirmOrFalse(
      `确定${action}版本【${row.StandardCode} ${row.StandardName} ${row.VersionYear}】？`,
      `${action}确认`,
      { type: 'warning' },
    )
    if (!ok) return
    try {
      const res = await toggleISOStandardValid(row.Code)
      ElMessage.success(res?.data?.IsValid === 1 ? '已启用' : '已禁用')
      if (this.currentFamily.value) {
        await this.loadVersions(this.currentFamily.value.Code)
        await this.refreshTable()
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '操作失败')
    }
  }

  async batchDeleteVersions(rows: VersionRow[]): Promise<void> {
    if (rows.length === 0) return
    const ok = await confirmOrFalse(
      `确定删除选中的 ${rows.length} 个版本？\n关联条款将不可见。`,
      '批量删除',
      { type: 'warning' },
    )
    if (!ok) return
    try {
      await deleteISOStandard(rows.map(r => r.Code))
      ElMessage.success('批量删除成功')
      if (this.currentFamily.value) {
        await this.loadVersions(this.currentFamily.value.Code)
        await this.refreshTable()
      }
    } catch (e: any) {
      ElMessage.error(e?.message ?? '删除失败')
    }
  }

  // ── 版本加载 ──
  async loadVersions(familyCode: string): Promise<void> {
    try {
      const res = await yzhApi.post<ApiResponse<any>>(
        '/api/Admin/Foundation/ISOStandard/filter',
        {
          Page: 1,
          PageSize: 1000,
          Filters: [{ Field: 'FamilyCode', Operator: 'eq', Value: familyCode }],
        },
      )
      const items = (res as any).data?.Items ?? []
      this.versions.value = (items as any[]).map((r: any) => ({
        Code: r.Code,
        StandardCode: r.StandardCode ?? '',
        StandardName: r.StandardName ?? '',
        VersionYear: Number(r.VersionYear) || 0,
        Category: r.Category ?? '',
        FamilyCode: r.FamilyCode ?? null,
        Sort: r.Sort ?? 0,
        IsValid: r.IsValid ?? 1,
      }))
    } catch {
      this.versions.value = []
    }
  }

  // ── 条款加载 ──
  async loadClauses(standardCode: string): Promise<void> {
    this.clausesLoading.value = true
    try {
      const res = await yzhApi.get<ApiResponse<any[]>>(
        '/api/Admin/Foundation/ISOClause/getTree',
        { standardCode, includeDisabled: 'false' },
      )
      const flat = (res as any).data ?? []
      this.clauses.value = buildClauseTree(flat)
    } catch {
      this.clauses.value = []
    } finally {
      this.clausesLoading.value = false
    }
  }

  // ── 条款表单 ──
  async openAddClause(): Promise<boolean> {
    if (this.panelMode.value !== 'version' || !this.currentVersion.value) return false
    this.clauseMode.value = 'add'
    this.editingClause.value = null
    const stdCode = this.currentVersion.value.Code
    this.clauseFormData.value = {
      StandardCode: stdCode,
      ParentCode: null,
      SortOrder: 0,
      IsValid: 1,
    }
    await this.loadParentOptions()
    this.clauseDialogVisible.value = true
    return true
  }

  async openEditClause(row: any): Promise<void> {
    this.clauseMode.value = 'edit'
    this.editingClause.value = row
    this.clauseFormData.value = { ...row, ParentCode: row.ParentCode ?? null }
    await this.loadParentOptions(row.Code)
    this.clauseDialogVisible.value = true
  }

  async submitClauseForm(): Promise<void> {
    this.clauseSubmitting.value = true
    try {
      const d = { ...this.clauseFormData.value }
      if (d.ParentCode === '' || d.ParentCode === undefined) d.ParentCode = null
      delete d.children
      const stdCode = this.currentVersion.value?.Code || ''
      if (this.clauseMode.value === 'add') {
        await yzhApi.post('/api/Admin/Foundation/ISOClause/add', d)
      } else {
        await yzhApi.post('/api/Admin/Foundation/ISOClause/update', d)
      }
      this.clauseDialogVisible.value = false
      ElMessage.success(this.clauseMode.value === 'add' ? '新增成功' : '修改成功')
      await this.loadClauses(stdCode)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '保存失败')
    } finally {
      this.clauseSubmitting.value = false
    }
  }

  async deleteClause(row: any): Promise<void> {
    const ok = await confirmOrFalse(
      `确定删除条款【${row.ClauseNumber} ${row.Title}】？`,
      '删除确认',
      { type: 'warning' },
    )
    if (!ok) return
    try {
      await yzhApi.post('/api/Admin/Foundation/ISOClause/delete', [row.Code])
      ElMessage.success('删除成功')
      const stdCode = this.currentVersion.value?.Code || ''
      await this.loadClauses(stdCode)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '删除失败')
    }
  }

  async batchDeleteClauses(rows: any[]): Promise<void> {
    if (rows.length === 0) return
    const ok = await confirmOrFalse(
      `确定删除选中的 ${rows.length} 个条款？`,
      '批量删除',
      { type: 'warning' },
    )
    if (!ok) return
    try {
      await yzhApi.post('/api/Admin/Foundation/ISOClause/delete', rows.map(r => r.Code))
      ElMessage.success('批量删除成功')
      const stdCode = this.currentVersion.value?.Code || ''
      await this.loadClauses(stdCode)
    } catch (e: any) {
      ElMessage.error(e?.message ?? '删除失败')
    }
  }

  private async loadParentOptions(excludeCode?: string): Promise<void> {
    const stdCode = this.currentVersion.value?.Code || ''
    if (!stdCode) { this.parentOptions.value = []; return }
    try {
      const res = await yzhApi.get<ApiResponse<any[]>>(
        '/api/Admin/Foundation/ISOClause/getTree',
        { standardCode: stdCode, includeDisabled: 'true' },
      )
      const flat = (res as any).data ?? []
      this.parentOptions.value = buildParentOptions(flat, excludeCode)
    } catch {
      this.parentOptions.value = []
    }
  }
}

// ──── 工具函数 ──────────────────────────────────────────────────────────────────

function buildCategoryFamilyTree(
  categories: CategoryItem[],
  families: FamilyRow[],
): TreeNode[] {
  const catMap = new Map<string, CategoryItem>()
  const seen = new Set<string>()
  for (const c of categories ?? []) {
    if (!c?.Code || !c?.Value || seen.has(c.Code)) continue
    seen.add(c.Code)
    catMap.set(c.Value, c)
  }
  const byCategory = new Map<string, FamilyRow[]>()
  for (const f of families ?? []) {
    if (!f?.Code) continue
    const arr = byCategory.get(f.Category) ?? []
    arr.push(f)
    byCategory.set(f.Category, arr)
  }
  const sortByNo = (a: FamilyRow, b: FamilyRow) =>
    (a.Sort ?? 0) - (b.Sort ?? 0) ||
    String(a.FamilyNo ?? '').localeCompare(String(b.FamilyNo ?? ''))

  const result: TreeNode[] = []
  const pushed = new Set<string>()
  for (const c of categories ?? []) {
    if (!seen.has(c.Code) || pushed.has(c.Code)) continue
    pushed.add(c.Code)
    const kids = [...(byCategory.get(c.Value) ?? [])].sort(sortByNo)
    result.push({
      Code: c.Code,
      Name: c.Label,
      ParentCode: null,
      NodeType: 'virtual',
      IsLeaf: kids.length === 0,
      Extra: { _level: 0, Category: c.Value },
      Children: kids.map(f => ({
        Code: f.Code,
        Name: `${f.FamilyNo} ${f.FamilyName}`.trim(),
        ParentCode: c.Code,
        NodeType: 'family',
        IsLeaf: true,
        IsValid: f.IsValid ?? 1,
        Extra: {
          _level: 1,
          Category: f.Category,
          FamilyNo: f.FamilyNo,
          FamilyName: f.FamilyName,
          Sort: f.Sort,
        },
        Children: [],
      })),
    })
  }
  return result
}

function buildClauseTree(flat: any[]): any[] {
  const map = new Map<string, any>()
  flat.forEach(c => {
    if (!c?.Code) return
    map.set(c.Code, { ...c, Children: [] })
  })
  const roots: any[] = []
  flat.forEach(c => {
    if (!c?.Code) return
    const node = map.get(c.Code)
    if (!node) return
    const parent = c.ParentCode ? map.get(c.ParentCode) : null
    if (parent) parent.Children.push(node)
    else roots.push(node)
  })
  const sortRec = (nodes: any[]) => {
    nodes.sort((a, b) =>
      String(a.ClauseNumber ?? '').localeCompare(String(b.ClauseNumber ?? ''), undefined, { numeric: true }),
    )
    nodes.forEach(n => sortRec(n.Children ?? []))
  }
  sortRec(roots)
  return roots
}

function buildParentOptions(flat: any[], excludeCode?: string): any[] {
  const exclude = excludeCode ? new Set<string>([excludeCode]) : null
  const available = exclude ? flat.filter(c => !exclude!.has(c.Code)) : flat
  const map = new Map<string, any>()
  available.forEach(c => {
    if (!c?.Code) return
    map.set(c.Code, {
      value: c.Code,
      label: `${c.ClauseNumber ?? ''} ${c.Title ?? ''}`.trim() || c.Code,
      Children: [] as any[],
    })
  })
  const roots: any[] = []
  available.forEach(c => {
    const node = map.get(c.Code)
    if (!node) return
    const parent = c.ParentCode ? map.get(c.ParentCode) : null
    if (parent) parent.Children.push(node)
    else roots.push(node)
  })
  const sortRec = (nodes: any[]) => {
    nodes.sort((a, b) =>
      String(a.ClauseNumber ?? '').localeCompare(String(b.ClauseNumber ?? ''), undefined, { numeric: true }),
    )
    nodes.forEach(n => sortRec(n.Children ?? []))
  }
  sortRec(roots)
  return roots
}

export default StandardManageLogic
