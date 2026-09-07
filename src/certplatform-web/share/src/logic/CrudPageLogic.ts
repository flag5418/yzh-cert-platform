/**
 * CrudPageLogic - 单表 CRUD 页面基类
 *
 * 设计理念：
 * - 80% 的单表 CRUD 逻辑由此基类封装
 * - 子类通过 override 钩子方法定制特殊逻辑（替代 C# 的 virtual/override）
 * - 自动从后端 EntityConfig 获取配置，前端无需维护 columns/fields
 * - 所有组件 ID 由基类动态生成，支持多实例和级联
 *
 * 使用方式：
 *   // index.ts
 *   export class UserPageLogic extends CrudPageLogic<SysUser> {
 *     override onPrepareAdd(entity: Partial<SysUser>) {
 *       entity.enable = 1
 *     }
 *   }
 *
 *   // index.vue
 *   const page = new UserPageLogic('Sys_User')
 *   await page.init()
 */

import { reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type { DefineColumn, EntityConfig, PageRequest, PageResult } from '../types/grid'
import type { YzhTableColumn, SearchField } from '@yzh-core/components/table'
import type { YzhFormField } from '@yzh-core/components/form'

/** PascalCase to camelCase */
function toCamelCase(str: string): string {
  if (!str) return str
  return str.charAt(0).toLowerCase() + str.slice(1)
}

/** ControlType map to YzhFormField.type */
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

/** ControlType for search */
type ControlType = DefineColumn['type']

export class CrudPageLogic<T extends Record<string, any>> {
  // ==================== Page Identity ====================
  readonly controllerName: string
  readonly pageId: string

  // ==================== Backend Config ====================
  config = ref<EntityConfig | null>(null)

  // ==================== Table State ====================
  rows = ref<T[]>([])
  loading = ref(false)
  selectedRows = ref<T[]>([])

  // ==================== Pagination State ====================
  pagination = reactive({ page: 1, pageSize: 20, total: 0 })

  // ==================== Search State ====================
  searchParams = reactive<Record<string, any>>({})

  // ==================== Sort State ====================
  sortField = ref<string | undefined>()
  sortOrder = ref<'asc' | 'desc' | undefined>()

  // ==================== Dialog State ====================
  dialogVisible = ref(false)
  dialogMode = ref<'add' | 'edit'>('add')
  submitting = ref(false)
  formData = reactive<Record<string, any>>({}) as Partial<T>

  // ==================== Data Loader ====================
  dataLoader(params: any): Promise<{ rows: T[]; total: number }> {
    return this.doLoadData(params)
  }

  private async doLoadData(params: any): Promise<{ rows: T[]; total: number }> {
    const request: PageRequest = {
      page: params.page || this.pagination.page,
      rows: params.rows || this.pagination.pageSize,
      sort: params.sort || this.sortField.value,
      order: params.order || this.sortOrder.value,
      conditions: this.buildConditions(params)
    }
    const res = await this.apiPost<PageResult<T>>('/page', request)
    return { rows: res.rows, total: res.total }
  }

  // ==================== Computed ====================

  get columns(): YzhTableColumn<T>[] {
    const cols = this.config.value?.columns
    if (!cols) return []
    return cols
      .filter((c) => c.xsFlag)
      .map((c) => ({
        prop: toCamelCase(c.fieldName),
        label: c.desName,
        width: Number(c.width) || undefined,
        sortable: c.sortable || undefined,
        fixed: (c.fixed as 'left' | 'right') || undefined,
        align: (c.align as 'left' | 'center' | 'right') || undefined,
        dictCode: c.dictCode || undefined,
        slot: (c.type as string) === 'CustomSlot' ? toCamelCase(c.fieldName) : undefined
      }))
  }

  get formFields(): YzhFormField[] {
    const cols = this.config.value?.columns
    if (!cols) return []
    const layoutCols = this.config.value?.layoutColumns || 2
    const colSpan = Math.floor(24 / layoutCols)
    return cols
      .filter((c) => c.bcFlag)
      .map((c) => ({
        prop: toCamelCase(c.fieldName),
        label: c.desName,
        type: mapControlType(c.type),
        required: !c.yxk,
        disabled: !c.enable,
        span: colSpan,
        dictCode: c.dictCode || undefined,
        options: c.options && c.options.length > 0 ? c.options : undefined,
        placeholder: c.type?.includes('Picker') ? `请选择${c.desName}` : `请输入${c.desName}`,
        fieldProps: {
          id: this.fieldId(c.fieldName)
        }
      }))
  }

  get searchFields(): SearchField[] {
    const cols = this.config.value?.columns
    if (!cols) return []
    return cols
      .filter((c) => c.xsFlag && this.isSearchable(c))
      .slice(0, 4)
      .map((c) => ({
        prop: toCamelCase(c.fieldName),
        label: c.desName,
        type: this.mapSearchType(c.type),
        placeholder: `请输入${c.desName}`,
        options: c.options && c.options.length > 0 ? c.options : undefined
      }))
  }

  get toolbarButtons(): Array<{ key: string; text: string; type: string }> {
    const tb = this.config.value?.toolbar
    if (!tb) return []
    const btns: Array<{ key: string; text: string; type: string }> = []
    if (tb.add !== false) btns.push({ key: 'add', text: '新增', type: 'primary' })
    if (tb.delete !== false) btns.push({ key: 'delete', text: '批量删除', type: 'danger' })
    if (tb.export !== false) btns.push({ key: 'export', text: '导出', type: 'success' })
    return btns
  }

  // ==================== Constructor ====================

  constructor(controllerName: string) {
    this.controllerName = controllerName
    this.pageId = this.generatePageId(controllerName)
  }

  // ==================== Init ====================

  async init() {
    await this.loadConfig()
    await this.loadData()
  }

  protected async loadConfig() {
    this.config.value = await this.apiGet<EntityConfig>('/config')
  }

  // ==================== Data Operations ====================

  async loadData() {
    this.loading.value = true
    try {
      const request: PageRequest = {
        page: this.pagination.page,
        rows: this.pagination.pageSize,
        sort: this.sortField.value,
        order: this.sortOrder.value,
        conditions: this.buildConditions()
      }
      const res = await this.apiPost<PageResult<T>>('/page', request)
      this.rows.value = res.rows
      this.pagination.total = res.total
      this.onDataLoaded(res.rows)
    } finally {
      this.loading.value = false
    }
  }

  protected async add(entity: Partial<T>): Promise<void> {
    await this.apiPost('/add', entity)
  }

  protected async update(entity: Partial<T>): Promise<void> {
    await this.apiPost('/update', entity)
  }

  protected async delete(codes: string[]): Promise<void> {
    await this.apiPost('/delete', codes)
  }

  async executeAction(actionName: string, row: T) {
    await this.apiPost(`/action/${actionName}`, row)
    await this.loadData()
  }

  // ==================== Hooks ====================

  protected onDataLoaded(_rows: T[]) {}
  protected onBeforeAdd(_entity: Partial<T>) {}
  protected onAfterAdd(_entity: Partial<T>) {}
  protected onBeforeUpdate(_entity: Partial<T>) {}
  protected onPrepareAdd(_entity: Partial<T>) {}
  protected onDelete(_codes: string[]): boolean | Promise<boolean> {
    return true
  }

  // ==================== Search ====================

  protected buildConditions(extra?: Record<string, any>) {
    const params = { ...this.searchParams, ...(extra || {}) }
    return Object.entries(params)
      .filter(([, v]) => v != null && v !== '' && !(Array.isArray(v) && v.length === 0))
      .map(([field, value]) => ({ field, value, operator: 'eq' as const }))
  }

  // ==================== Events ====================

  onSearch(params: Record<string, any>) {
    this.resetObject(this.searchParams)
    Object.assign(this.searchParams, params)
    this.pagination.page = 1
    this.loadData()
  }

  onPageChange(page: number) {
    this.pagination.page = page
    this.loadData()
  }

  onSizeChange(size: number) {
    this.pagination.pageSize = size
    this.pagination.page = 1
    this.loadData()
  }

  onSortChange(prop: string, order: 'asc' | 'desc') {
    this.sortField.value = prop
    this.sortOrder.value = order
    this.loadData()
  }

  onSelectionChange(rows: T[]) {
    this.selectedRows.value = rows
  }

  // ==================== Dialog ====================

  openAddDialog() {
    this.dialogMode.value = 'add'
    this.resetObject(this.formData)
    this.onPrepareAdd(this.formData)
    this.dialogVisible.value = true
  }

  openEditDialog(row: T) {
    this.dialogMode.value = 'edit'
    this.resetObject(this.formData)
    Object.assign(this.formData, row)
    this.dialogVisible.value = true
  }

  async cancelDialog() {
    this.dialogVisible.value = false
  }

  async submitForm() {
    this.submitting.value = true
    try {
      if (this.dialogMode.value === 'add') {
        this.onBeforeAdd(this.formData)
        await this.add(this.formData)
        this.onAfterAdd(this.formData)
      } else {
        this.onBeforeUpdate(this.formData)
        await this.update(this.formData)
      }
      ElMessage.success('保存成功')
      this.dialogVisible.value = false
      await this.loadData()
    } finally {
      this.submitting.value = false
    }
  }

  async confirmDelete(rows?: T[]) {
    const targets = rows || this.selectedRows.value
    if (targets.length === 0) {
      ElMessage.warning('请先选择要删除的记录')
      return
    }
    const pk = toCamelCase(this.config.value?.primaryKey || 'code')
    const codes = targets.map((r) => String(r[pk] || '')).filter(Boolean)
    const canDelete = await this.onDelete(codes)
    if (!canDelete) return
    await ElMessageBox.confirm(`确定删除 ${codes.length} 条记录？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定',
      cancelButtonText: '取消'
    })
    await this.delete(codes)
    ElMessage.success('删除成功')
    await this.loadData()
  }

  // ==================== ID Generator ====================

  id(type: string, suffix?: string): string {
    return suffix ? `${this.pageId}_${type}_${suffix}` : `${this.pageId}_${type}`
  }

  fieldId(fieldName: string): string {
    return `${this.pageId}_field_${fieldName.toLowerCase()}`
  }

  get tableId(): string { return this.id('table') }
  get searchBarId(): string { return this.id('searchbar') }
  get formId(): string { return this.id('form') }
  get dialogId(): string { return this.id('dialog') }
  get paginationId(): string { return this.id('pagination') }
  get toolbarId(): string { return this.id('toolbar') }

  // ==================== API ====================

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

  // ==================== Private ====================

  private generatePageId(controllerName: string): string {
    const prefix = controllerName.replace(/Controller$/, '').toLowerCase()
    const random = Math.random().toString(36).slice(2, 8)
    return `${prefix}_${random}`
  }

  private isSearchable(col: DefineColumn): boolean {
    const excludeTypes: string[] = ['Upload', 'TreeSelect', 'Cascader', 'CheckBox']
    return !excludeTypes.includes(col.type) && col.bcFlag
  }

  private mapSearchType(type: ControlType): SearchField['type'] {
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

  private resetObject(obj: Record<string, any>) {
    Object.keys(obj).forEach((k) => delete obj[k])
  }
}
