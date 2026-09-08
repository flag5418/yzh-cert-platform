/**
 * CrudPageLogic - 单表 CRUD 页面基类（V1.1）
 *
 * 设计理念：
 * - 80% 的单表 CRUD 逻辑由此基类封装
 * - 子类通过 override 钩子方法定制特殊逻辑（替代 C# 的 virtual/override）
 * - 自动从后端 EntityConfig 获取配置，前端无需维护 columns/fields
 * - 使用 /filter API（替代 /page），支持前端传过滤条件
 * - Split 数据方法实现增量更新，避免全量刷新
 *
 * 使用方式：
 *   // index.ts
 *   export class UserPageLogic extends CrudPageLogic<SysUser> {
 *     controllerName = 'SysUser'
 *
 *     // 覆盖钩子注入业务逻辑
 *     protected onBeforeAdd(entity: Partial<SysUser>) {
 *       entity.enable = 1
 *     }
 *   }
 *
 *   // index.vue
 *   const page = new UserPageLogic()
 *   await page.init()
 */

import { reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type {
  ApiResponse,
  PagedResult,
  FilterRequest,
  FilterItem,
  ExportRequest,
  ImportResult,
  EntityConfigDto,
  ColumnConfig,
  SearchFieldConfig
} from '../types/contracts'
import type { YzhTableColumn, SearchField } from '@yzh-core/components/table'
import type { YzhFormField } from '@yzh-core/components/form'

// ========================================================
// 工具函数
// ========================================================

/** PascalCase to camelCase */
function toCamelCase(str: string): string {
  if (!str) return str
  return str.charAt(0).toLowerCase() + str.slice(1)
}

/** ColumnConfig.type → YzhFormField.type */
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
    Cascader: 'cascader'
  }
  return map[type] || 'text'
}

/** ColumnConfig.type → SearchField.type */
function mapSearchType(type: string): SearchField['type'] {
  const map: Record<string, SearchField['type']> = {
    NumberBox: 'number',
    DatePicker: 'date',
    DateTimePicker: 'dateRange',
    ComboBox: 'select',
    DropDownList: 'select',
    RadioButtonList: 'select'
  }
  return map[type] || 'text'
}

// ========================================================
// 主类
// ========================================================

export abstract class CrudPageLogic<V extends Record<string, any>> {
  // ──── 页面标识 ────

  /** 控制器名称（子类必须设置） */
  abstract controllerName: string

  // ──── 后端配置 ────

  /** 后端页面配置（工具栏+表格+表单+搜索栏） */
  config = ref<EntityConfigDto | null>(null)

  // ──── 表格状态 ────

  /** 表格数据行 */
  rows = ref<V[]>([])

  /** 表格加载状态 */
  loading = ref(false)

  /** 选中行集合 */
  selectedRows = ref<V[]>([])

  // ──── 分页状态 ────

  /** 分页参数 */
  pagination = reactive({ page: 1, pageSize: 20, total: 0 })

  // ──── 搜索过滤状态 ────

  /** 搜索参数（搜索栏绑定） */
  searchParams = reactive<Record<string, any>>({})

  // ──── 排序状态 ────

  /** 排序字段 */
  sortField = ref<string | undefined>()

  /** 排序方向 */
  sortOrder = ref<'asc' | 'desc' | undefined>()

  // ──── 弹窗状态 ────

  /** 弹窗可见性 */
  dialogVisible = ref(false)

  /** 弹窗模式 */
  dialogMode = ref<'add' | 'edit'>('add')

  /** 提交中状态 */
  submitting = ref(false)

  /** 表单数据 */
  formData = reactive<Record<string, any>>({}) as Partial<V>

  // ========================================================
  // Computed: 从 config 派生 UI 结构
  // ========================================================

  /** 表格列配置（从 EntityConfig.Columns 映射） */
  get columns(): YzhTableColumn<V>[] {
    const cols = this.config.value?.columns
    if (!cols) return []
    return cols
      .filter((c) => c.xsFlag)
      .map((c) => {
        const col: any = {
          prop: toCamelCase(c.fieldName),
          label: c.desName,
          width: Number(c.width) || undefined,
          sortable: c.sortable || undefined,
          fixed: (c.fixed as 'left' | 'right') || undefined,
          align: (c.align as 'left' | 'center' | 'right') || undefined,
          dictCode: c.dictCode || undefined
        }
        if (c.type === 'CustomSlot') {
          col.slot = toCamelCase(c.fieldName)
        }
        return col as YzhTableColumn<V>
      })
  }

  /** 表单字段配置（从 EntityConfig.Columns 映射，bcFlag=true 表示参与编辑） */
  get formFields(): YzhFormField[] {
    const cols = this.config.value?.columns
    const schema = this.config.value?.schema
    if (!cols) return []
    const layoutCols = 2 // 默认 2 列布局
    const colSpan = Math.floor(24 / layoutCols)
    return cols
      .filter((c) => c.bcFlag)
      .map((c) => {
        const prop = toCamelCase(c.fieldName)
        const fieldSchema = schema?.[prop]
        return {
          prop,
          label: c.desName,
          type: mapControlType(c.type),
          required: !c.yxk,
          disabled: !c.enable,
          span: colSpan,
          dictCode: c.dictCode || undefined,
          options: undefined, // 可由子类填充或从字典加载
          placeholder: c.type?.includes('Picker') ? `请选择${c.desName}` : `请输入${c.desName}`,
          // 字段级 schema（类型、默认值、可选）
          fieldSchema
        }
      })
  }

  /** 搜索栏字段配置（从 EntityConfig.SearchFields 映射，优先；否则从 Columns 推导） */
  get searchFields(): SearchField[] {
    const sf = this.config.value?.searchFields
    if (sf && sf.length > 0) {
      return sf.map((s) => ({
        prop: toCamelCase(s.field),
        label: s.label,
        type: this.mapSearchControlType(s.controlType),
        placeholder: `请输入${s.label}`,
        options: s.options
      }))
    }
    // fallback：从 Columns 推导搜索字段
    const cols = this.config.value?.columns
    if (!cols) return []
    return cols
      .filter((c) => c.xsFlag && this.isSearchable(c))
      .slice(0, 4)
      .map((c) => ({
        prop: toCamelCase(c.fieldName),
        label: c.desName,
        type: mapSearchType(c.type),
        placeholder: `请输入${c.desName}`
      }))
  }

  /** 工具栏按钮配置（从 ToolbarConfig 派生） */
  get toolbarButtons(): Array<{ key: string; text: string; type: string; action?: string }> {
    const tb = this.config.value?.toolbar
    if (!tb) return []
    const btns: Array<{ key: string; text: string; type: string; action?: string }> = []
    if (tb.add !== false) btns.push({ key: 'add', text: '新增', type: 'primary' })
    if (tb.delete !== false) btns.push({ key: 'delete', text: '批量删除', type: 'danger' })
    if (tb.export !== false) btns.push({ key: 'export', text: '导出', type: 'success' })
    if (tb.import !== false) btns.push({ key: 'import', text: '导入', type: 'warning' })
    // 自定义按钮
    if (tb.customButtons) {
      for (const [label, method] of Object.entries(tb.customButtons)) {
        btns.push({ key: `custom:${method}`, text: label, type: 'info', action: method })
      }
    }
    return btns
  }

  /** 行操作按钮配置（从 RowButtonConfig 派生） */
  get rowButtons(): Array<{ key: string; text: string; type: string; action?: string }> {
    const rb = this.config.value?.rowButtons
    if (!rb) return []
    const btns: Array<{ key: string; text: string; type: string; action?: string }> = []
    if (rb.edit !== false) btns.push({ key: 'edit', text: '编辑', type: 'primary' })
    if (rb.delete !== false) btns.push({ key: 'delete', text: '删除', type: 'danger' })
    // 自定义行按钮
    if (rb.customButtons) {
      for (const [label, method] of Object.entries(rb.customButtons)) {
        btns.push({ key: `custom:${method}`, text: label, type: 'info', action: method })
      }
    }
    return btns
  }

  /** 主键字段名（camelCase），统一使用 code */
  get primaryKey(): string {
    return 'code'
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

  /**
   * 分页查询（/filter API，后端自动拼 SQL）
   * 优先使用 /filter，逐步废弃 /page
   */
  async loadPage(): Promise<void> {
    this.loading.value = true
    try {
      const request: FilterRequest = {
        page: this.pagination.page,
        pageSize: this.pagination.pageSize,
        sortField: this.sortField.value,
        sortOrder: this.sortOrder.value,
        filters: this.buildFilters()
      }
      const res = await this.apiPost<ApiResponse<PagedResult<V>>>('/filter', request)
      this.rows.value = res.data.items
      this.pagination.total = res.data.total
      this.onDataLoaded(res.data.items)
    } finally {
      this.loading.value = false
    }
  }

  /**
   * 构建过滤条件（从 searchParams + 额外条件）
   */
  protected buildFilters(extra?: Record<string, any>): FilterItem[] {
    const params = { ...this.searchParams, ...(extra || {}) }
    // 如果后端配置了 SearchFields，使用配置中的 operator
    const sfMap = new Map<string, string>()
    if (this.config.value?.searchFields) {
      for (const s of this.config.value.searchFields) {
        sfMap.set(toCamelCase(s.field), s.operator)
      }
    }
    return Object.entries(params)
      .filter(([, v]) => v != null && v !== '' && !(Array.isArray(v) && v.length === 0))
      .map(([field, value]) => ({
        field: this.toSnakeCase(field),
        value: Array.isArray(value) ? value.join(',') : String(value),
        operator: (sfMap.get(field) as FilterItem['operator']) || 'eq'
      }))
  }

  // ========================================================
  // 写入操作（add / update / delete / action）
  // ========================================================

  /** 新增实体（/api/{controller}/add） */
  protected async add(entity: Partial<V>): Promise<V> {
    const res = await this.apiPost<ApiResponse<V>>('/add', entity)
    return res.data
  }

  /** 修改实体（/api/{controller}/update） */
  protected async update(entity: Partial<V>): Promise<V> {
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

  // ========================================================
  // 导出导入
  // ========================================================

  /** 导出（/api/{controller}/export） */
  async exportData(format: 'excel' | 'csv' = 'excel', fields?: string[]): Promise<void> {
    const request: ExportRequest = {
      filters: this.buildFilters(),
      format,
      fields
    }
    // 导出为文件下载
    await this.apiPostAndDownload('/export', request, `export_${Date.now()}.${format}`)
  }

  /** 导入（/api/{controller}/import） */
  async importData(file: File): Promise<ImportResult> {
    const formData = new FormData()
    formData.append('file', file)
    const res = await this.apiUpload<ApiResponse<ImportResult>>('/import', formData)
    return res.data
  }

  /** 下载导入模板（/api/{controller}/import/template） */
  async downloadImportTemplate(): Promise<void> {
    await this.apiGetAndDownload('/import/template', 'import_template.xlsx')
  }

  // ========================================================
  // Split 数据方法（增量更新，不重新请求）
  // ========================================================

  /**
   * 删除行（增量更新）
   * @param code 要删除的行 code
   */
  protected removeRowByCode(code: string): void {
    const idx = this.rows.value.findIndex(r => (r as any).code === code)
    if (idx >= 0) {
      this.rows.value.splice(idx, 1)
      this.pagination.total = Math.max(0, this.pagination.total - 1)
    }
  }

  /**
   * 替换行（增量更新）
   * @param code 要替换的行 code
   * @param row 新行数据
   */
  protected replaceRowByCode(code: string, row: V): void {
    const idx = this.rows.value.findIndex(r => (r as any).code === code)
    if (idx >= 0) this.rows.value.splice(idx, 1, row as any)
  }

  /**
   * 插入行（增量更新，通常插入到顶部）
   * @param row 新行数据
   */
  protected insertRow(row: V, position: 'top' | 'bottom' = 'top'): void {
    if (position === 'top') {
      this.rows.value.unshift(row as any)
    } else {
      this.rows.value.push(row as any)
    }
    this.pagination.total++
  }

  // ========================================================
  // 钩子方法（子类可覆盖）
  // ========================================================

  /** 数据加载后钩子 */
  protected onDataLoaded(_rows: V[]) {}

  /** 新增前钩子（可修改待提交实体） */
  protected onBeforeAdd(_entity: Partial<V>) {}

  /** 新增后钩子（数据已入库） */
  protected onAfterAdd(_entity: Partial<V>) {}

  /** 修改前钩子（可修改待提交实体） */
  protected onBeforeUpdate(_entity: Partial<V>) {}

  /** 修改后钩子（数据已入库） */
  protected onAfterUpdate(_entity: Partial<V>) {}

  /** 删除前钩子（返回 false 取消删除） */
  protected onDelete(_codes: string[]): boolean | Promise<boolean> {
    return true
  }

  /** 删除后钩子 */
  protected onAfterDelete(_codes: string[]) {}

  /** 准备新增数据钩子（设置默认值等） */
  protected onPrepareAdd(_entity: Partial<V>) {}

  // ========================================================
  // 事件处理
  // ========================================================

  /** 搜索事件 */
  onSearch(params: Record<string, any>) {
    this.resetObject(this.searchParams)
    Object.assign(this.searchParams, params)
    this.pagination.page = 1
    this.loadPage()
  }

  /** 分页变更 */
  onPageChange(page: number) {
    this.pagination.page = page
    this.loadPage()
  }

  /** 每页条数变更 */
  onSizeChange(size: number) {
    this.pagination.pageSize = size
    this.pagination.page = 1
    this.loadPage()
  }

  /** 排序变更 */
  onSortChange(prop: string, order: 'asc' | 'desc') {
    this.sortField.value = this.toSnakeCase(prop)
    this.sortOrder.value = order
    this.loadPage()
  }

  /** 选中行变更 */
  onSelectionChange(rows: V[]) {
    this.selectedRows.value = rows
  }

  /** 工具栏按钮点击 */
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
        // 触发文件选择
        break
      default:
        if (key.startsWith('custom:')) {
          const method = key.slice(7)
          await this.executeCustomToolbarAction(method)
        }
    }
  }

  /** 行按钮点击 */
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

  /** 打开新增弹窗（使用 config.newEntity 自动初始化） */
  openAddDialog() {
    this.dialogMode.value = 'add'
    this.initFormData()
    this.onPrepareAdd(this.formData)
    this.dialogVisible.value = true
  }

  /** 打开编辑弹窗（使用 config.newEntity 初始化 + row 覆盖） */
  openEditDialog(row: V) {
    this.dialogMode.value = 'edit'
    this.initFormData(row)
    this.dialogVisible.value = true
  }

  /**
   * 初始化表单数据
   * - 优先使用后端反射生成的 config.newEntity（自动包含所有字段名和默认值）
   * - 编辑模式时，用 row 中的实际值覆盖默认值
   * 前端不再需要手动维护字段列表和默认值
   */
  private initFormData(row?: V): void {
    const baseEntity = this.config.value?.newEntity || {}
    const initial: Record<string, any> = { ...baseEntity }
    if (row) {
      // 编辑模式：用行数据覆盖默认值
      Object.assign(initial, row)
    }
    this.resetObject(this.formData)
    Object.assign(this.formData, initial)
  }

  /** 取消弹窗 */
  async cancelDialog() {
    this.dialogVisible.value = false
  }

  /** 提交表单 */
  async submitForm() {
    this.submitting.value = true
    try {
      if (this.dialogMode.value === 'add') {
        this.onBeforeAdd(this.formData)
        const saved = await this.add(this.formData)
        this.onAfterAdd(this.formData)
        // 使用 Split 方法增量更新
        this.insertRow(saved as V)
      } else {
        this.onBeforeUpdate(this.formData)
        const saved = await this.update(this.formData)
        this.onAfterUpdate(this.formData)
        // 使用 Split 方法增量更新
        const pk = this.primaryKey
        this.replaceRowByCode((saved as any)[pk], saved as V)
      }
      ElMessage.success('保存成功')
      this.dialogVisible.value = false
    } finally {
      this.submitting.value = false
    }
  }

  /** 确认删除 */
  async confirmDelete(rows?: V[]) {
    const targets = rows || this.selectedRows.value
    if (targets.length === 0) {
      ElMessage.warning('请先选择要删除的记录')
      return
    }
    const pk = 'code' // 统一使用 code 作为业务主键
    const codes = targets.map((r) => String((r as any)[pk] || '')).filter(Boolean)
    const canDelete = await this.onDelete(codes)
    if (!canDelete) return
    await ElMessageBox.confirm(`确定删除 ${codes.length} 条记录？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消'
    })
    await this.delete(codes)
    ElMessage.success('删除成功')
    // 使用 Split 方法增量更新
    for (const code of codes) {
      this.removeRowByCode(code)
    }
    this.onAfterDelete(codes)
  }

  /** 自定义工具栏动作（子类可覆盖） */
  protected async executeCustomToolbarAction(_methodName: string): Promise<void> {
    // 默认空实现，子类覆盖
  }

  // ========================================================
  // API 调用
  // ========================================================

  /** GET 请求 */
  protected async apiGet<R = any>(path: string): Promise<R> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.get<R>(url)
  }

  /** POST 请求 */
  protected async apiPost<R = any>(path: string, body?: any): Promise<R> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.post<R>(url, body)
  }

  /** POST 请求并下载文件 */
  protected async apiPostAndDownload(path: string, body: any, filename: string): Promise<void> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.download(url, body, filename)
  }

  /** GET 请求并下载文件 */
  protected async apiGetAndDownload(path: string, filename: string): Promise<void> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.downloadGet(url, filename)
  }

  /** 上传文件 */
  protected async apiUpload<R = any>(path: string, formData: FormData): Promise<R> {
    const url = `/api/${this.controllerName}${path}`
    const { yzhApi } = await import('@yzh-core/api/client')
    return yzhApi.upload<R>(url, formData)
  }

  // ========================================================
  // 私有工具方法
  // ========================================================

  /** camelCase → snake_case */
  private toSnakeCase(str: string): string {
    return str.replace(/([A-Z])/g, '_$1').toLowerCase()
  }

  /** 判断列是否可搜索 */
  private isSearchable(col: ColumnConfig): boolean {
    const excludeTypes: string[] = ['Upload', 'TreeSelect', 'Cascader', 'CheckBox']
    return !excludeTypes.includes(col.type) && col.bcFlag
  }

  /** SearchFieldConfig.controlType → SearchField.type */
  private mapSearchControlType(ct: SearchFieldConfig['controlType']): SearchField['type'] {
    const map: Record<string, SearchField['type']> = {
      input: 'text',
      select: 'select',
      date: 'date',
      cascader: 'cascader' as SearchField['type']
    }
    return map[ct] || 'text'
  }

  /** 重置对象 */
  private resetObject(obj: Record<string, any>) {
    Object.keys(obj).forEach((k) => delete obj[k])
  }
}

export default CrudPageLogic
