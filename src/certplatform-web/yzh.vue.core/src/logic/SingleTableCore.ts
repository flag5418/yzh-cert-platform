/**
 * SingleTableCore - 单表 CRUD 内核（ST 阶段，由 CrudPageLogic 演进改名）
 *
 * 数据访问规则（与 YZH.Core.Stand 严格一致）：
 * - res.data：ApiResponse 顶层（camelCase）
 * - res.data.Items / res.data.TotalCount：业务实体（PascalCase）
 * - formData / 表格行 / formFields[].prop：统一 PascalCase（架构铁律：JSON 字段名 = 实体属性名 = 列名）
 * - 组件内部（YzhTable/YzhForm）直接按 prop 取值，不做大小写转换
 *
 * 设计理念：
 * - 80% 的单表 CRUD 逻辑由此内核封装
 * - 子类通过覆盖钩子方法/getter 定制特殊逻辑（等价 C# 的 virtual/override）
 * - 配置适配走 adapters/（纯函数），内核与组件零业务字段硬编码（ST-11）
 * - 按钮统一：rowActions（YzhAction[]，主）/ rowActionButtons（字典，兼容）/ rowButtons（派生数组，兼容）
 * - 动作统一：dispatch(key, target) + registerHandler(key, fn)（ST-7）
 *
 * 生命周期（ST-5 / ST-13）：
 *   init() → loadConfig() → onAfterInit()
 *   行写入：onBeforeAdd → add → onAfterAdd / onBeforeUpdate → update → onAfterUpdate
 *   删除：onDelete → delete → onAfterDelete
 */

import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  toFormFields,
  toFormLayoutCols,
  toRowActions,
  toSearchFields,
  toTableColumns,
  toToolbarActions,
} from '../adapters/entityAdapters'
import type { YzhFormField } from '../components/form'
import type {
  Page,
  PageParams,
  SearchField,
  YzhAction,
  YzhTableColumn,
} from '../components/table/types'
import {
  pascalCaseFormData,
  rowToFormData,
  toCamelCase,
  toPascalCase,
} from '../utils/case'
import type {
  ApiResponse,
  EntityConfigDto,
  FilterItem,
  FilterRequest,
  PagedData,
} from '../types/contracts'

/** 动作处理器签名（registerHandler 用） */
export type ActionHandler<V = any> = (
  target: V | undefined,
  action: YzhAction | undefined,
) => void | Promise<void>

export abstract class SingleTableCore<V extends Record<string, any> = any> {
  // ──── 页面标识 ────

  /** 控制器名称（子类必须设置） */
  abstract controllerName: string

  // ──── 后端配置 ────

  /** 后端页面配置（工具栏+表格+表单+搜索栏） */
  config = ref<EntityConfigDto | null>(null)

  // ──── 表格状态 ────

  /** 表格数据行（PascalCase 字段） */
  rows = ref<V[]>([])

  /** 表格加载状态 */
  loading = ref(false)

  /** 选中行集合 */
  selectedRows = ref<V[]>([])

  // ──── 分页状态 ────

  /** 分页参数 */
  pagination = reactive({ page: 1, pageSize: 20, total: 0 })

  // ──── 搜索过滤状态 ────

  /** 搜索参数（PascalCase key，与业务实体字段名一致） */
  searchParams = reactive<Record<string, any>>({})

  // ──── 排序状态 ────

  /** 排序字段（PascalCase） */
  sortField = ref<string | undefined>()

  /** 排序方向 */
  sortOrder = ref<'asc' | 'desc' | undefined>()

  // ──── 弹窗状态 ────

  /** 弹窗可见性 */
  dialogVisible = ref(false)

  /** 弹窗模式 */
  dialogMode = ref<'add' | 'edit' | 'detail'>('add')

  /**
   * 表单编辑模式（GroupIndex 控制）
   *
   * - '0'：新增/编辑模式，GroupIndex="0" 的字段可编辑
   * - '99'：详情模式，仅 GroupIndex="99" 的字段可编辑（JSON 通常不配 → 全部只读）
   */
  formGroupIndex = ref<string>('0')

  /** 提交中状态 */
  submitting = ref(false)

  // ──── ShowDisabled 开关（基类统一管理，子类无需手动实现） ────

  /** 显示已禁用记录开关 */
  showDisabled = ref(false)

  /** 切换 ShowDisabled 并刷新表格 */
  async toggleShowDisabled(): Promise<void> {
    this.showDisabled.value = !this.showDisabled.value
    await this.refresh()
  }

  /**
   * 表单数据：PascalCase key（与 formFields[].prop、NewEntity、实体属性名一致）
   * 例：{ Code: "", UserName: "", Enable: 1 }
   */
  formData = reactive<Record<string, any>>({}) as Record<string, any>

  // ──── 表格引用（局部刷新） ────

  protected _tableRef: any = null

  /** 设置表格引用（模板中调用，或由 useSingleTable 注入） */
  setTableRef(ref: any) {
    this._tableRef = ref
  }

  /** 刷新表格数据（触发 dataLoader 重新加载） */
  async refresh(): Promise<void> {
    await this._tableRef?.refresh()
  }

  // ========================================================
  // Computed: 从 config 派生 UI 结构（经 adapters/，业务字段不出内核）
  // ========================================================

  /** 表格列配置（AD-1） */
  get columns(): YzhTableColumn<V>[] {
    return toTableColumns(this.config.value) as YzhTableColumn<V>[]
  }

  /** 表单布局列数（从后端 EntityConfig.FormCols 读取，0=自动） */
  get formLayoutCols(): number {
    return toFormLayoutCols(this.config.value)
  }

  /** 表单字段配置（AD-2） */
  get formFields(): YzhFormField[] {
    return toFormFields(this.config.value, this.formGroupIndex.value)
  }

  /** 搜索栏字段（config.SearchFields 优先；为空时走 fallbackSearchFields 钩子再走列推导） */
  get searchFields(): SearchField[] {
    const sf = this.config.value?.SearchFields
    if (sf && sf.length > 0) {
      return toSearchFields(this.config.value)
    }
    const fallback = this.fallbackSearchFields
    if (fallback.length > 0) return fallback
    return toSearchFields(this.config.value)
  }

  /**
   * 后端 SearchFields 缺失时的业务兜底（如 treepconfig 未映射历史的场景）
   * 子类可覆盖；默认空（走列推导）
   */
  protected get fallbackSearchFields(): SearchField[] {
    return []
  }

  /** 工具栏按钮（YzhAction[]，声明式，绑定 :toolbar-actions + @toolbar-action） */
  get toolbarActions(): YzhAction[] {
    return toToolbarActions(this.config.value)
  }

  /** @deprecated 兼容旧形状（对象数组），等价 toolbarActions 的字段子集 */
  get toolbarButtons(): Array<{ key: string; text: string; type: string; action?: string }> {
    return this.toolbarActions.map((a) => ({
      key: a.key,
      text: a.text,
      type: a.type ?? 'primary',
    }))
  }

  /**
   * 行操作按钮（YzhAction[] 或按行解析函数 —— 主形状，绑定 :row-action-buttons）
   *
   * 子类可覆盖为函数式：(row) => YzhAction[]（按行状态动态显隐/禁用）
   */
  get rowActions(): YzhAction[] | ((row: V) => YzhAction[]) {
    return toRowActions(this.config.value, this.enableField)
  }

  /** 行操作按钮字典（兼容旧 Record 消费方，由 rowActions 派生） */
  get rowActionButtons(): Record<string, string> {
    const actions =
      typeof this.rowActions === 'function' ? this.rowActions({} as V) : this.rowActions
    const dict: Record<string, string> = {}
    for (const a of actions) dict[a.key] = a.text
    return dict
  }

  /** @deprecated 兼容旧形状（数组），由 rowActions 派生 */
  get rowButtons(): Array<{ key: string; text: string; type: string }> {
    const actions =
      typeof this.rowActions === 'function' ? this.rowActions({} as V) : this.rowActions
    return actions.map((a) => ({ key: a.key, text: a.text, type: a.type ?? 'primary' }))
  }

  /** 启用/禁用字段名（从 EntityConfig.EnableField 读取，null 表示不支持启用/禁用） */
  get enableField(): string | null {
    return this.config.value?.EnableField || null
  }

  /** 主键字段名（PascalCase），统一使用 Code */
  get primaryKey(): string {
    return 'Code'
  }

  // ========================================================
  // 覆盖点（ST-3/ST-4/ST-9）
  // ========================================================

  /** 新增默认值（合并到 NewEntity 之后；PascalCase key） */
  protected get defaultValues(): Record<string, any> {
    return {}
  }

  /** 确认弹窗中显示的实体名称字段（默认 Name；子类覆盖如 'RoleName'） */
  protected get entityNameField(): string {
    return 'Name'
  }

  /** 读取行显示名称（需要拼接多个字段的页面覆盖此方法） */
  protected entityName(row: V): string {
    const v = (row as any)?.[this.entityNameField]
    return v == null ? '' : String(v)
  }

  /** 提交前归一化钩子（如 Decimal 字符串→数值） */
  protected normalizeBeforeSubmit(payload: Record<string, any>): Record<string, any> {
    return payload
  }

  /** 数据加载后处理钩子（如编码→名称翻译） */
  protected postprocessRows(rows: V[]): V[] {
    return rows
  }

  // ========================================================
  // 初始化（ST-5）
  // ========================================================

  /** 初始化页面：加载配置 → onAfterInit（表格数据由 YzhTable dataLoader 自行加载） */
  async init(): Promise<void> {
    await this.loadConfig()
    await this.onAfterInit()
  }

  /** 加载页面配置（/api/{controller}/config） */
  protected async loadConfig(): Promise<void> {
    const res = await this.apiGet<ApiResponse<EntityConfigDto>>('/config')
    this.config.value = res.data
  }

  /** 配置加载完成后的钩子（子类在此做额外初始化，不再覆盖 init） */
  protected async onAfterInit(): Promise<void> {}

  // ========================================================
  // 数据查询（/filter API）
  // ========================================================

  /** 分页查询（/filter API） */
  async loadPage(): Promise<void> {
    this.loading.value = true
    try {
      const request: FilterRequest = {
        Page: this.pagination.page,
        PageSize: this.pagination.pageSize,
        SortField: this.sortField.value,
        SortOrder: this.sortOrder.value,
        Filters: this.buildFilters(),
      }
      const res = await this.apiPost<ApiResponse<PagedData<V>>>('/filter', request)
      const page = res.data
      if (page) {
        this.rows.value = this.postprocessRows((page.Items ?? []) as V[])
        this.pagination.total = page.TotalCount ?? 0
        this.onDataLoaded(this.rows.value as V[])
      }
    } catch {
      this.rows.value = []
      this.pagination.total = 0
    } finally {
      this.loading.value = false
    }
  }

  /**
   * YzhTable 数据加载器（页面直接绑定：`:data-loader="logic.dataLoader.bind(logic)"`）
   *
   * 入参由 YzhTable 传入：{ page, rows, sort, order, ...搜索条件 }
   * 搜索条件的 key = EntityConfig.SearchFields[].Field（PascalCase）
   * Operator 取自 SearchFields 配置（未配置时默认 eq）
   */
  async dataLoader(params: PageParams): Promise<Page<V>> {
    const {
      page = 1,
      rows = this.pagination.pageSize,
      sort,
      order,
      ...searchValues
    } = params
    this.loading.value = true
    try {
      const request: FilterRequest = {
        Page: page,
        PageSize: rows,
        SortField: sort,
        SortOrder: order as 'asc' | 'desc' | undefined,
        Filters: this.buildFilters(searchValues as Record<string, any>),
      }
      const res = await this.apiPost<ApiResponse<PagedData<V>>>('/filter', request)
      const data = res?.data
      const items = this.postprocessRows(((data?.Items ?? []) as V[]).slice())
      this.pagination.page = page
      this.pagination.pageSize = rows
      this.pagination.total = data?.TotalCount ?? 0
      this.rows.value = items
      this.onDataLoaded(items)
      return { rows: items, total: this.pagination.total }
    } finally {
      this.loading.value = false
    }
  }

  /** 构建过滤条件（从 searchParams + 额外条件 + 自动 ShowDisabled） */
  protected buildFilters(extra?: Record<string, any>): FilterItem[] {
    const params = { ...this.searchParams, ...(extra || {}) }
    const sfMap = new Map<string, string>()
    if (this.config.value?.SearchFields) {
      for (const s of this.config.value.SearchFields) {
        if (s.Operator) sfMap.set(s.Field, s.Operator)
      }
    }
    const filters = Object.entries(params)
      .filter(
        ([, v]) =>
          v != null && v !== '' && !(Array.isArray(v) && v.length === 0),
      )
      .map(([field, value]) => ({
        Field: field,
        Value: Array.isArray(value) ? value.join(',') : String(value),
        Operator: (sfMap.get(field) as any) || 'eq',
      }))

    // 自动注入 ShowDisabled（当 config 有 EnableField 时）
    if (this.config.value?.EnableField && this.showDisabled.value) {
      filters.push({ Field: 'ShowDisabled', Value: 'true', Operator: 'eq' })
    }

    return filters
  }

  // ========================================================
  // 写入操作
  // ========================================================

  /** 新增实体（/api/{controller}/add） */
  protected async add(entity: Record<string, any>): Promise<V> {
    const res = await this.apiPost<ApiResponse<V>>('/add', entity)
    return res.data
  }

  /** 修改实体（/api/{controller}/update） */
  protected async update(entity: Record<string, any>): Promise<V> {
    const res = await this.apiPost<ApiResponse<V>>('/update', entity)
    return res.data
  }

  /** 批量删除（/api/{controller}/delete） */
  protected async delete(codes: string[]): Promise<void> {
    await this.apiPost<ApiResponse<string>>('/delete', codes)
  }

  /** 行操作（/api/{controller}/action/{methodName}） */
  async executeAction(methodName: string, row: V): Promise<void> {
    await this.apiPost<ApiResponse<any>>(`/action/${methodName}`, row)
    await this.loadPage()
  }

  /**
   * 切换有效标志（IsValid: 0 ↔ 1）
   */
  async toggleIsValid(code: string): Promise<{ Code: string; IsValid: number } | null> {
    const res = await this.apiPost<ApiResponse<{ Code: string; IsValid: number }>>(
      '/toggle-valid',
      { Code: code },
    )
    if (res.success) {
      ElMessage.success(res.data.IsValid === 1 ? '已启用' : '已禁用')
      return res.data
    }
    return null
  }

  /**
   * 切换行有效标志（完整流程：确认弹窗 → API → 本地更新）
   */
  async toggleRowIsValidWithConfirm(
    row: V,
    options?: { entityName?: string; field?: string },
  ): Promise<void> {
    const field = options?.field ?? this.enableField ?? 'IsValid'
    const currentVal = (row as any)[field] ?? 1
    const action = currentVal === 1 ? '禁用' : '启用'
    const name = options?.entityName ?? this.entityName(row)

    await ElMessageBox.confirm(
      name ? `确定${action}【${name}】？` : `确定${action}该记录？`,
      `${action}确认`,
      {
        type: 'warning',
        confirmButtonText: `确定${action}`,
        cancelButtonText: '取消',
      },
    )

    const result = await this.toggleIsValid((row as any).Code)
    if (result) {
      this.replaceRowByCode((row as any).Code, { ...row, [field]: result.IsValid } as V)
    }
  }

  // ========================================================
  // 导出导入
  // ========================================================

  /** 导出 */
  async exportData(
    format: 'excel' | 'csv' = 'excel',
    fields?: string[],
  ): Promise<void> {
    const request = {
      Filters: this.buildFilters(),
      format,
      fields,
    } as any
    await this.apiPostAndDownload('/export', request, `export_${Date.now()}.${format}`)
  }

  /** 导入 */
  async importData(file: File): Promise<any> {
    const formData = new FormData()
    formData.append('file', file)
    const res = await this.apiUpload<ApiResponse<any>>('/import', formData)
    return res.data
  }

  /** 下载导入模板 */
  async downloadImportTemplate(): Promise<void> {
    await this.apiGetAndDownload('/import/template', 'import_template.xlsx')
  }

  // ========================================================
  // Split 数据方法（增量更新，不重新请求）
  // ========================================================

  /** 删除行（按主键 Code） */
  protected removeRowByCode(code: string): void {
    if (this._tableRef) {
      this._tableRef.removeRow((r: any) => String(r.Code) === String(code))
    } else {
      const idx = this.rows.value.findIndex((r) => (r as any).Code === code)
      if (idx >= 0) {
        this.rows.value.splice(idx, 1)
        this.pagination.total = Math.max(0, this.pagination.total - 1)
      }
    }
  }

  /** 替换行（按主键 Code） */
  protected replaceRowByCode(code: string, row: V): void {
    if (this._tableRef) {
      this._tableRef.replaceRow((r: any) => String(r.Code) === String(code), row)
    } else {
      const idx = this.rows.value.findIndex((r) => (r as any).Code === code)
      if (idx >= 0) this.rows.value.splice(idx, 1, row as any)
    }
  }

  /** 插入行 */
  protected insertRow(row: V, position: 'top' | 'bottom' = 'top'): void {
    if (this._tableRef) {
      this._tableRef.insertRow(row, position)
    } else {
      if (position === 'top') {
        this.rows.value.unshift(row as any)
      } else {
        this.rows.value.push(row as any)
      }
      this.pagination.total++
    }
  }

  // ========================================================
  // 钩子方法（子类可覆盖；与后端 OnBeforeAdd/OnAfterAdd/… 对齐）
  // ========================================================

  protected onDataLoaded(_rows: V[]) {}
  protected onBeforeAdd(_entity: Record<string, any>) {}
  protected onAfterAdd(_entity: Record<string, any>) {}
  protected onBeforeUpdate(_entity: Record<string, any>) {}
  protected onAfterUpdate(_entity: Record<string, any>) {}
  protected onDelete(_codes: string[]): boolean | Promise<boolean> {
    return true
  }
  protected onAfterDelete(_codes: string[]) {}
  protected onPrepareAdd(_entity: Record<string, any>) {}

  // ========================================================
  // 动作统一（ST-7 dispatch + registerHandler）
  // ========================================================

  private handlers = new Map<string, ActionHandler<V>>()

  /** 注册自定义动作处理器（覆盖内置同名动作） */
  registerHandler(key: string, fn: ActionHandler<V>): void {
    this.handlers.set(key, fn)
  }

  /**
   * 动作统一入口：行按钮 / 工具栏按钮 / 树节点动作都汇聚到这里。
   *
   * 内置分支：add / edit / delete / toggle-valid / export / import / batch-delete / custom:{method}
   */
  async dispatch(key: string, target?: V, action?: YzhAction): Promise<void> {
    const handler = this.handlers.get(key)
    if (handler) {
      await handler(target, action)
      return
    }

    switch (key) {
      case 'add':
        this.openAddDialog()
        return
      case 'edit':
        if (target) this.openEditDialog(target)
        return
      case 'detail':
        if (target) this.openDetailDialog(target)
        return
      case 'delete':
        // 行上下文（有 target）删单行；工具栏上下文删选中行
        await this.confirmDelete(target ? [target] : undefined)
        return
      case 'batch-delete':
        await this.confirmDelete()
        return
      case 'toggle-valid':
        if (target) await this.toggleRowIsValidWithConfirm(target)
        return
      case 'export':
        await this.exportData()
        return
      case 'import':
        return
      default:
        if (key.startsWith('custom:')) {
          const method = key.slice(7)
          if (target) {
            await this.executeAction(method, target)
          } else {
            await this.executeCustomToolbarAction(method)
          }
        }
    }
  }

  /** 行动作入口（绑定 @row-action="logic.onRowAction"；箭头属性自动绑定 this，模板引用式传参不丢上下文） */
  onRowAction = async (key: string, row: V, action?: YzhAction): Promise<void> => {
    await this.dispatch(key, row, action)
  }

  /** 工具栏动作入口（绑定 @toolbar-action="logic.onToolbarAction"） */
  onToolbarAction = async (key: string, action?: YzhAction): Promise<void> => {
    await this.dispatch(key, undefined, action)
  }

  /** @deprecated 兼容旧命名，等价 onToolbarAction */
  onToolbarClick = async (key: string): Promise<void> => {
    await this.dispatch(key)
  }

  /** @deprecated 兼容旧命名，等价 onRowAction */
  onRowClick = async (key: string, row: V): Promise<void> => {
    await this.dispatch(key, row)
  }

  // ========================================================
  // 事件处理（表格原生事件；箭头属性自动绑定 this，供模板引用式绑定）
  // ========================================================

  onSearch = async (params: Record<string, any>): Promise<void> => {
    this.resetObject(this.searchParams)
    Object.assign(this.searchParams, params)
    this.pagination.page = 1
    await this.loadPage()
  }

  onPageChange = async (page: number): Promise<void> => {
    this.pagination.page = page
    await this.loadPage()
  }

  onSizeChange = async (size: number): Promise<void> => {
    this.pagination.pageSize = size
    this.pagination.page = 1
    await this.loadPage()
  }

  onSortChange = async (prop: string, order: 'asc' | 'desc'): Promise<void> => {
    this.sortField.value = prop
    this.sortOrder.value = order
    await this.loadPage()
  }

  onSelectionChange = (rows: V[]): void => {
    this.selectedRows.value = rows
  }

  // ========================================================
  // 弹窗操作
  // ========================================================

  openAddDialog() {
    this.dialogMode.value = 'add'
    this.formGroupIndex.value = '0'
    this.initFormData()
    this.onPrepareAdd(this.formData)
    this.dialogVisible.value = true
  }

  openEditDialog(row: V) {
    this.dialogMode.value = 'edit'
    this.formGroupIndex.value = '0'
    this.editingRow.value = row
    this.initFormData(row)
    this.dialogVisible.value = true
  }

  /** 打开详情弹窗（只读模式，formGroupIndex='99' → 所有字段只读） */
  openDetailDialog(row: V) {
    this.dialogMode.value = 'detail'
    this.formGroupIndex.value = '99'
    this.editingRow.value = row
    this.initFormData(row)
    this.dialogVisible.value = true
  }

  /**
   * 打开指定编辑模式的弹窗
   * @param row 行数据（null=新增）
   * @param groupIndex 编辑模式：'0'=全部可编辑, '1'=仅 GroupIndex=1 字段可编辑, '99'=全部只读
   */
  openDialogWithMode(row: V | null, groupIndex: string) {
    this.formGroupIndex.value = groupIndex
    if (row) {
      this.dialogMode.value = groupIndex === '99' ? 'detail' : 'edit'
      this.editingRow.value = row
      this.initFormData(row)
    } else {
      this.dialogMode.value = 'add'
      this.initFormData()
      this.onPrepareAdd(this.formData)
    }
    this.dialogVisible.value = true
  }

  /**
   * 初始化表单数据（ST-3：NewEntity → defaultValues / 编辑行）
   */
  protected initFormData(row?: V): void {
    const baseEntity = this.config.value?.NewEntity || {}
    const initial: Record<string, any> = { ...baseEntity }
    if (row) {
      Object.assign(initial, row as Record<string, any>)
    } else {
      Object.assign(initial, this.defaultValues)
    }
    this.resetObject(this.formData)
    Object.assign(this.formData, initial)
  }

  /** 当前编辑行（ST-8：提交后与后端返回合并，避免表格行丢字段） */
  protected editingRow = ref<V | null>(null)

  async cancelDialog() {
    this.dialogVisible.value = false
  }

  async submitForm() {
    this.submitting.value = true
    try {
      if (this.dialogMode.value === 'add') {
        this.onBeforeAdd(this.formData)
        const submitData = this.normalizeBeforeSubmit({ ...this.formData })
        const saved = await this.add(submitData)
        this.onAfterAdd(this.formData)
        this.insertRow(saved as V)
      } else {
        this.onBeforeUpdate(this.formData)
        const submitData = this.normalizeBeforeSubmit({ ...this.formData })
        const saved = await this.update(submitData)
        this.onAfterUpdate(this.formData)
        const pk = this.primaryKey
        // 与编辑行快照合并，避免后端未回传的字段在表格行上丢失
        this.replaceRowByCode(
          (saved as any)[pk],
          { ...((this.editingRow.value || {}) as any), ...(saved as any) } as V,
        )
      }
      ElMessage.success('保存成功')
      this.dialogVisible.value = false
    } finally {
      this.submitting.value = false
    }
  }

  /**
   * 删除确认（ST-10：逐行名称）
   * @param rows 待删行（缺省取选中行）
   */
  async confirmDelete(rows?: V[]) {
    const targets = rows || this.selectedRows.value
    if (targets.length === 0) {
      ElMessage.warning('请先选择要删除的记录')
      return
    }
    const pk = this.primaryKey
    const codes = targets
      .map((r) => String((r as any)[pk] || ''))
      .filter(Boolean)
    const canDelete = await this.onDelete(codes)
    if (!canDelete) return

    const names = targets.map((r) => this.entityName(r as V)).filter(Boolean)
    let message: string
    if (names.length === 1) {
      message = `确定删除【${names[0]}】？`
    } else if (names.length > 1 && names.length <= 3) {
      message = `确定删除 ${names.length} 条记录（${names.join('、')}）？`
    } else {
      message = `确定删除 ${codes.length} 条记录？`
    }
    await ElMessageBox.confirm(message, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
    })
    await this.delete(codes)
    ElMessage.success('删除成功')
    for (const code of codes) {
      this.removeRowByCode(code)
    }
    this.selectedRows.value = []
    this.onAfterDelete(codes)
  }

  protected async executeCustomToolbarAction(_methodName: string): Promise<void> {}

  // ========================================================
  // API 调用
  // ========================================================

  protected async apiGet<R = any>(path: string): Promise<R> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.get<R>(url)
  }

  protected async apiPost<R = any>(path: string, body?: any): Promise<R> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.post<R>(url, body)
  }

  protected async apiPostAndDownload(
    path: string,
    body: any,
    filename: string,
  ): Promise<void> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.download(url, body, filename)
  }

  protected async apiGetAndDownload(
    path: string,
    filename: string,
  ): Promise<void> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.downloadGet(url, filename)
  }

  protected async apiUpload<R = any>(
    path: string,
    formData: FormData,
  ): Promise<R> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.upload<R>(url, formData)
  }

  // ========================================================
  // 私有工具方法
  // ========================================================

  protected resetObject(obj: Record<string, any>) {
    Object.keys(obj).forEach((k) => delete obj[k])
  }
}

// ========================================================
// 公共工具函数（兼容再导出；新代码从 utils/case 引入）
// ========================================================

export {
  toCamelCase,
  toPascalCase,
  pascalCaseFormData,
  rowToFormData,
}

export default SingleTableCore
