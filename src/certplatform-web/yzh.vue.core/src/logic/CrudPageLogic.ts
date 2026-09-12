/**
 * CrudPageLogic - 单表 CRUD 页面基类（V1.2 - 对齐 YZH.Core.Stand）
 *
 * 数据访问规则（与 YZH.Core.Stand 严格一致）：
 * - res.data：ApiResponse 顶层（camelCase）
 * - res.data.Items / res.data.TotalCount：业务实体（PascalCase）
 * - formData：camelCase key（NewEntity 字典 key，反射 ToCamelCase）
 * - 表格行：PascalCase 访问（与后端实体属性一致）
 *
 * 设计理念：
 * - 80% 的单表 CRUD 逻辑由此基类封装
 * - 子类通过 override 钩子方法定制特殊逻辑（替代 C# 的 virtual/override）
 * - 自动从后端 EntityConfig 获取配置，前端无需维护 columns/fields
 * - 使用 /filter API（替代 /page），支持前端传过滤条件
 * - Split 数据方法实现增量更新，避免全量刷新
 */

import type { YzhFormField } from '@yzh-core/components/form'
import type { SearchField, YzhTableColumn } from '@yzh-core/components/table'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import type {
  ApiResponse,
  ColumnConfig,
  EntityConfigDto,
  FilterItem,
  FilterRequest,
  SearchFieldConfig,
  PagedData,
  ToolbarConfig,
  RowButtonConfig,
} from '../types/contracts'

// ========================================================
// 工具函数
// ========================================================

/** ColumnConfig.Type → YzhFormField.type */
function mapControlType(type: string): YzhFormField['type'] {
  const map: Record<string, YzhFormField['type']> = {
    TextBox: 'text',
    TextArea: 'textarea',
    NumberBox: 'number',
    DatePicker: 'date',
    DateTimePicker: 'datetime',
    ComboBox: 'select',
    DropDownList: 'select',
    RadioButtonList: 'radio',
    CheckBox: 'checkbox',
    Switch: 'switch',
    Upload: 'upload',
    TreeSelect: 'treeSelect',
    Cascader: 'cascader',
    PasswordBox: 'password',
    Memo: 'textarea',
  }
  return map[type] || 'text'
}

/** ColumnConfig.Type → SearchField.type */
function mapSearchType(type: string): SearchField['type'] {
  const map: Record<string, SearchField['type']> = {
    NumberBox: 'number',
    DatePicker: 'date',
    DateTimePicker: 'dateRange',
    ComboBox: 'select',
    DropDownList: 'select',
    RadioButtonList: 'select',
  }
  return map[type] || 'text'
}

// ========================================================
// 主类
// ========================================================

export abstract class CrudPageLogic<V extends Record<string, any> = any> {
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
   * - '0'：默认模式，所有 BcFlag=true 且 GroupIndex='0' 的字段可编辑
   * - '1'：仅 GroupIndex='1' 的字段可编辑，其余只读
   * - '99'：详情模式，所有字段只读（因为大部分字段 GroupIndex='0'）
   */
  formGroupIndex = ref<string>('0')

  /** 提交中状态 */
  submitting = ref(false)

  /**
   * 表单数据：camelCase key（NewEntity 字典 key，反射 ToCamelCase）
   * 例：{ code: "", userName: "", enable: 1 }
   */
  formData = reactive<Record<string, any>>({}) as Record<string, any>

  // ──── 表格引用（局部刷新） ────

  /** YzhTable 组件引用（由模板注入，用于局部数据操作） */
  private _tableRef: any = null

  /** 设置表格引用（模板中调用：logic.setTableRef(tableRef.value)） */
  setTableRef(ref: any) {
    this._tableRef = ref
  }

  // ========================================================
  // Computed: 从 config 派生 UI 结构
  // ========================================================

  /** 表格列配置（从 EntityConfig.Columns 映射，PascalCase 字段） */
  get columns(): YzhTableColumn<V>[] {
    const cols = this.config.value?.Columns
    if (!cols) return []
    return cols
      .filter((c) => c.XsFlag)
      .map((c) => {
        const col: any = {
          prop: c.FieldName, // PascalCase 字段名（与后端实体属性一致）
          label: c.DesName,
          width: Number(c.Width) || undefined,
          sortable: c.Sortable || undefined,
          fixed: (c.Fixed as 'left' | 'right') || undefined,
          align: (c.Align as 'left' | 'center' | 'right') || undefined,
          dictCode: c.DictCode || undefined,
        }
        if (c.Type === 'CustomSlot') {
          col.slot = c.FieldName
        }
        return col as YzhTableColumn<V>
      })
  }

  /** 表单布局列数（从后端 EntityConfig.FormCols 读取，0=自动：BcFlag字段≤10用1列，>10用2列） */
  get formLayoutCols(): number {
    const formCols = this.config.value?.FormCols
    if (formCols && formCols > 0) return formCols
    // 自动模式：统计 BcFlag=true 的字段数量
    const bcCount = this.config.value?.Columns?.filter((c) => c.BcFlag).length ?? 0
    return bcCount <= 10 ? 1 : 2
  }

  /** 表单字段配置（从 EntityConfig.Columns 映射） */
  get formFields(): YzhFormField[] {
    const cols = this.config.value?.Columns
    const schema = this.config.value?.Schema
    if (!cols) return []
    const layoutCols = this.formLayoutCols
    const colSpan = Math.floor(24 / layoutCols)
    const editMode = this.formGroupIndex.value
    return cols
      .filter((c) => c.BcFlag && c.Type !== 'Other')
      .map((c) => {
        const prop = c.FieldName // PascalCase 字段名
        // Schema 字典 key 是 camelCase（反射 ToCamelCase）→ 转 PascalCase 查表
        const camelKey = toCamelCase(prop)
        const fieldSchema = schema?.[camelKey]
        // GroupIndex 逻辑：editMode='0' 时全部可编辑；editMode!='0' 时仅匹配字段可编辑
        const fieldGroupIndex = c.GroupIndex || '0'
        const isDisabledByGroupIndex = editMode !== '0' && fieldGroupIndex !== editMode
        return {
          prop,
          label: c.DesName,
          type: mapControlType(c.Type),
          required: !c.Yxk,
          disabled: !c.Enable || isDisabledByGroupIndex,
          span: colSpan,
          dictCode: c.DictCode || undefined,
          options: undefined,
          placeholder: c.Type?.includes('Picker')
            ? `请选择${c.DesName}`
            : `请输入${c.DesName}`,
          fieldSchema,
        }
      })
  }

  /** 搜索栏字段配置 */
  get searchFields(): SearchField[] {
    const sf = this.config.value?.SearchFields
    if (sf && sf.length > 0) {
      return sf.map((s: SearchFieldConfig) => ({
        prop: s.Field, // PascalCase
        label: s.Label,
        type: this.mapSearchControlType(s.ControlType),
        placeholder: `请输入${s.Label}`,
        options: (s.Options as any) ?? undefined,
      }))
    }
    // fallback
    const cols = this.config.value?.Columns
    if (!cols) return []
    return cols
      .filter((c) => c.XsFlag && c.Type !== 'Other' && this.isSearchable(c))
      .slice(0, 4)
      .map((c) => ({
        prop: c.FieldName, // PascalCase
        label: c.DesName,
        type: mapSearchType(c.Type),
        placeholder: `请输入${c.DesName}`,
      }))
  }

  /** 工具栏按钮配置 */
  get toolbarButtons(): Array<{
    key: string
    text: string
    type: string
    action?: string
  }> {
    const tb: ToolbarConfig | undefined = this.config.value?.Toolbar
    if (!tb) return []
    const btns: Array<{
      key: string
      text: string
      type: string
      action?: string
    }> = []
    if (tb.Add !== false) btns.push({ key: 'add', text: '新增', type: 'primary' })
    if (tb.Delete !== false)
      btns.push({ key: 'delete', text: '批量删除', type: 'danger' })
    if (tb.Export !== false)
      btns.push({ key: 'export', text: '导出', type: 'success' })
    if (tb.Import !== false)
      btns.push({ key: 'import', text: '导入', type: 'warning' })
    if (tb.CustomButtons) {
      for (const [label, method] of Object.entries(tb.CustomButtons)) {
        btns.push({
          key: `custom:${method}`,
          text: label,
          type: 'info',
          action: method as string,
        })
      }
    }
    return btns
  }

  /** 行操作按钮配置 */
  get rowButtons(): Array<{
    key: string
    text: string
    type: string
    action?: string
  }> {
    const rb: RowButtonConfig | undefined = this.config.value?.RowButtons
    if (!rb) return []
    const btns: Array<{
      key: string
      text: string
      type: string
      action?: string
    }> = []
    if (rb.Edit !== false) btns.push({ key: 'edit', text: '编辑', type: 'primary' })
    if (rb.Delete !== false)
      btns.push({ key: 'delete', text: '删除', type: 'danger' })
    if (rb.CustomButtons) {
      for (const [label, method] of Object.entries(rb.CustomButtons)) {
        btns.push({
          key: `custom:${method}`,
          text: label,
          type: 'info',
          action: method as string,
        })
      }
    }
    return btns
  }

  /** 主键字段名（PascalCase），统一使用 Code */
  get primaryKey(): string {
    return 'Code'
  }

  // ========================================================
  // 初始化
  // ========================================================

  /** 初始化页面：加载配置 → 加载数据 */
  async init(): Promise<void> {
    await this.loadConfig()
    await this.loadPage()
  }

  /** 加载页面配置（/api/{controller}/config） */
  protected async loadConfig(): Promise<void> {
    const res = await this.apiGet<ApiResponse<EntityConfigDto>>('/config')
    this.config.value = res.data
  }

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
      const res = await this.apiPost<ApiResponse<PagedData<V>>>(
        '/filter',
        request,
      )
      const page = res.data
      if (page) {
        this.rows.value = page.Items ?? []
        this.pagination.total = page.TotalCount ?? 0
        this.onDataLoaded(this.rows.value)
      }
    } catch (e: any) {
      this.rows.value = []
      this.pagination.total = 0
    } finally {
      this.loading.value = false
    }
  }

  /** 构建过滤条件（从 searchParams + 额外条件） */
  protected buildFilters(extra?: Record<string, any>): FilterItem[] {
    const params = { ...this.searchParams, ...(extra || {}) }
    const sfMap = new Map<string, string>()
    if (this.config.value?.SearchFields) {
      for (const s of this.config.value.SearchFields) {
        if (s.Operator) sfMap.set(s.Field, s.Operator)
      }
    }
    return Object.entries(params)
      .filter(
        ([, v]) =>
          v != null && v !== '' && !(Array.isArray(v) && v.length === 0),
      )
      .map(([field, value]) => ({
        Field: field, // PascalCase，与后端实体属性一致
        Value: Array.isArray(value) ? value.join(',') : String(value),
        Operator: (sfMap.get(field) as any) || 'eq',
      }))
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
   * @param code 记录 Code
   * @returns 新的 IsValid 值，失败返回 null
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
   *
   * 业务页面可直接调用，无需自行实现确认弹窗和本地状态更新。
   * 基类统一处理：读取当前状态 → 弹窗确认 → 调用 API → 替换行数据。
   *
   * @param row 行数据
   * @param options.entityName 确认弹窗中显示的实体名称（如 "张三"），默认读 row.Name
   * @param options.field 有效标志字段名，默认 "IsValid"
   */
  async toggleRowIsValidWithConfirm(
    row: V,
    options?: { entityName?: string; field?: string },
  ): Promise<void> {
    const field = options?.field ?? 'IsValid'
    const currentVal = (row as any)[field] ?? 1
    const action = currentVal === 1 ? '禁用' : '启用'
    const name = options?.entityName ?? ''

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
    await this.apiPostAndDownload(
      '/export',
      request,
      `export_${Date.now()}.${format}`,
    )
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
  //
  // 优先转发到 YzhTable 组件（_tableRef），
  // 若无表格引用则回退到逻辑层 this.rows。
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
  // 钩子方法（子类可覆盖）
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
  // 事件处理
  // ========================================================

  onSearch(params: Record<string, any>) {
    this.resetObject(this.searchParams)
    Object.assign(this.searchParams, params)
    this.pagination.page = 1
    this.loadPage()
  }

  onPageChange(page: number) {
    this.pagination.page = page
    this.loadPage()
  }

  onSizeChange(size: number) {
    this.pagination.pageSize = size
    this.pagination.page = 1
    this.loadPage()
  }

  onSortChange(prop: string, order: 'asc' | 'desc') {
    this.sortField.value = prop // PascalCase
    this.sortOrder.value = order
    this.loadPage()
  }

  onSelectionChange(rows: V[]) {
    this.selectedRows.value = rows
  }

  async onToolbarClick(key: string) {
    switch (key) {
      case 'add':
        this.openAddDialog()
        break
      case 'delete':
        await this.confirmDelete()
        break
      case 'export':
        await this.exportData()
        break
      case 'import':
        break
      default:
        if (key.startsWith('custom:')) {
          const method = key.slice(7)
          await this.executeCustomToolbarAction(method)
        }
    }
  }

  async onRowClick(key: string, row: V) {
    switch (key) {
      case 'edit':
        this.openEditDialog(row)
        break
      case 'delete':
        await this.confirmDelete([row])
        break
      default:
        if (key.startsWith('custom:')) {
          const method = key.slice(7)
          await this.executeAction(method, row)
        }
    }
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
    this.initFormData(row)
    this.dialogVisible.value = true
  }

  /**
   * 打开详情弹窗（只读模式，formGroupIndex='99' → 所有字段只读）
   */
  openDetailDialog(row: V) {
    this.dialogMode.value = 'detail'
    this.formGroupIndex.value = '99'
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
      this.initFormData(row)
    } else {
      this.dialogMode.value = 'add'
      this.initFormData()
      this.onPrepareAdd(this.formData)
    }
    this.dialogVisible.value = true
  }

  /**
   * 初始化表单数据
   * - NewEntity 是 camelCase key（反射 ToCamelCase）
   * - 编辑模式时用 row 覆盖：row 是 PascalCase 字段，需要转 camelCase 写回 formData
   */
  private initFormData(row?: V): void {
    const baseEntity = this.config.value?.NewEntity || {}
    const initial: Record<string, any> = { ...baseEntity }
    if (row) {
      // row 是 PascalCase 字段，转 camelCase 写回 formData
      const rowCamel: Record<string, any> = {}
      for (const [k, v] of Object.entries(row as any)) {
        rowCamel[toCamelCase(k)] = v
      }
      Object.assign(initial, rowCamel)
    }
    this.resetObject(this.formData)
    Object.assign(this.formData, initial)
  }

  async cancelDialog() {
    this.dialogVisible.value = false
  }

  async submitForm() {
    this.submitting.value = true
    try {
      if (this.dialogMode.value === 'add') {
        this.onBeforeAdd(this.formData)
        // formData 是 camelCase key，转 PascalCase 提交给后端
        const submitData = pascalCaseFormData(this.formData)
        const saved = await this.add(submitData)
        this.onAfterAdd(this.formData)
        this.insertRow(saved as V)
      } else {
        this.onBeforeUpdate(this.formData)
        const submitData = pascalCaseFormData(this.formData)
        const saved = await this.update(submitData)
        this.onAfterUpdate(this.formData)
        const pk = this.primaryKey
        this.replaceRowByCode((saved as any)[pk], saved as V)
      }
      ElMessage.success('保存成功')
      this.dialogVisible.value = false
    } finally {
      this.submitting.value = false
    }
  }

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
    await ElMessageBox.confirm(
      `确定删除 ${codes.length} 条记录？`,
      '删除确认',
      {
        type: 'warning',
        confirmButtonText: '确定',
        cancelButtonText: '取消',
      },
    )
    await this.delete(codes)
    ElMessage.success('删除成功')
    for (const code of codes) {
      this.removeRowByCode(code)
    }
    this.onAfterDelete(codes)
  }

  protected async executeCustomToolbarAction(
    _methodName: string,
  ): Promise<void> {}

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

  private isSearchable(col: ColumnConfig): boolean {
    const excludeTypes: string[] = [
      'Upload',
      'TreeSelect',
      'Cascader',
      'CheckBox',
    ]
    return !excludeTypes.includes(col.Type) && col.BcFlag
  }

  private mapSearchControlType(ct: string): SearchField['type'] {
    const map: Record<string, SearchField['type']> = {
      input: 'text',
      select: 'select',
      date: 'date',
      cascader: 'cascader' as SearchField['type'],
    }
    return map[ct] || 'text'
  }

  private resetObject(obj: Record<string, any>) {
    Object.keys(obj).forEach((k) => delete obj[k])
  }
}

// ========================================================
// 公共工具函数（其他 Logic 可复用）
// ========================================================

/** PascalCase → camelCase */
export function toCamelCase(name: string): string {
  if (!name) return name
  if (name[0] >= 'a' && name[0] <= 'z') return name
  return name[0].toLowerCase() + name.slice(1)
}

/** camelCase → PascalCase */
export function toPascalCase(name: string): string {
  if (!name) return name
  if (name[0] >= 'A' && name[0] <= 'Z') return name
  return name[0].toUpperCase() + name.slice(1)
}

/** formData（camelCase key）→ 提交给后端（PascalCase key） */
export function pascalCaseFormData(
  formData: Record<string, any>,
): Record<string, any> {
  const result: Record<string, any> = {}
  for (const [k, v] of Object.entries(formData)) {
    result[toPascalCase(k)] = v
  }
  return result
}

/** 后端返回行（PascalCase）→ formData（camelCase） */
export function rowToFormData(row: Record<string, any>): Record<string, any> {
  const result: Record<string, any> = {}
  for (const [k, v] of Object.entries(row)) {
    result[toCamelCase(k)] = v
  }
  return result
}

export default CrudPageLogic
