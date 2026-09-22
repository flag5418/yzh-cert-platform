/**
 * TreeTableCore - 左树右表内核
 *
 * 数据访问规则（与 YZH.Core.Stand 严格一致）：
 * - res.data：ApiResponse 顶层（camelCase）
 * - res.data.Items / res.data.TotalCount：业务实体（PascalCase）
 * - TreeItemDto / TreeNode 字段：PascalCase
 * - formData / treeFormData：PascalCase key（与 formFields/treeFormFields prop 一致）
 *
 * 架构：
 * - 继承 SingleTableCore<V> 获得全部单表 CRUD 能力
 * - 树能力抽 TreeSide 混入（TT-2）：状态/索引/增量变更
 * - 统一 dataLoader（TT-6）：buildFilters 自动注入 RelateField；shouldApplyTreeFilter / isVirtualNode
 * - 行 CRUD 泛型流（TT-4）：openRowDialog / submitRowForm / deleteRow / batchDeleteRows
 * - 树 CRUD 泛型流（TT-5）：openTreeNodeDialog / submitTreeNodeForm / deleteTreeNodeWithConfirm
 * - 覆盖点（TT-8）：defaultTreeValues / treeEntityNameField / requireTreeSelectionForAdd /
 *   canAddUnderNode / relatedValue / normalizeBeforeSubmit / postprocessRows / afterTreeLoaded /
 *   autoSelectFirstNode / allowAddChild
 * - 生命周期（TT-3）：init() = loadConfig → loadTreeRoot → afterTreeLoaded → autoSelectFirstNode → onAfterInit
 */

import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import type { YzhFormField } from '../components/form'
import type { YzhAction } from '../components/table/types'
import type {
  ApiResponse,
  EntityConfigDto,
  FilterItem,
  FilterRequest,
  PagedData,
  TreeBehaviorConfig,
  TreeItemDto,
  TreeTableConfigDto,
} from '../types/contracts'
import type { TreeNode } from '../types/tree'
import { toCamelCase } from '../utils/case'
import { pascalCaseFormData } from '../utils/case'
import { toFormLayoutCols } from '../adapters/entityAdapters'
import { SingleTableCore } from './SingleTableCore'
import { TreeSide } from './TreeSide'

// ========================================================
// 树节点表单字段映射（保留 TreeFormConfig 的专用布局规则）
// ========================================================

function mapTreeControlType(type: string): YzhFormField['type'] {
  const map: Record<string, YzhFormField['type']> = {
    TextBox: 'text',
    TextArea: 'textarea',
    NumberBox: 'number',
    Decimal: 'number',
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

export abstract class TreeTableCore<
  V extends Record<string, any> = any,
> extends SingleTableCore<V> {
  // ──── 树能力混入（TT-2：状态 + 索引 + 增量变更） ────
  protected readonly treeSide = new TreeSide()

  /** 树数据（PascalCase） */
  get treeData(): TreeNode[] {
    return this.treeSide.treeData.value
  }
  set treeData(v: TreeNode[]) {
    this.treeSide.treeData.value = v
  }

  /** 树加载状态 */
  get treeLoading() {
    return this.treeSide.treeLoading
  }

  /** 当前选中节点 */
  get selectedNode(): TreeNode | null {
    return this.treeSide.selectedNode.value
  }
  set selectedNode(v: TreeNode | null) {
    this.treeSide.selectedNode.value = v
  }

  /** 完整树表配置（PascalCase，YZH.Core.Stand/TreeTableConfigDto） */
  treeTableConfig = ref<TreeTableConfigDto | null>(null)

  // ──── 树节点表单弹窗状态 ────

  treeDialogVisible = ref(false)
  treeDialogMode = ref<'add' | 'edit'>('add')
  treeSubmitting = ref(false)
  /** 树节点表单数据：PascalCase key（与 treeFormFields prop 一致） */
  treeFormData = reactive<Record<string, any>>({})

  /** 当前新增节点的父节点 */
  treeParentNode = ref<TreeNode | null>(null)

  /** 当前编辑的节点 */
  treeEditingNode = ref<TreeNode | null>(null)

  // ──── 树表组件引用（用于 appendNode 等直接操作） ────
  private _treeTableRef: any = null

  /** 设置树表组件引用（模板中调用，或由 useTreeTable 注入） */
  setTreeTableRef(ref: any) {
    this._treeTableRef = ref
  }

  // ──── 树配置快捷访问 ────

  /** 树行为配置（YZH.Core.Stand/TreeBehaviorConfigDto） */
  protected get treeConfig(): TreeBehaviorConfig | null {
    return this.treeTableConfig.value?.TreeConfig ?? null
  }

  /** 启用/禁用字段名（优先 TreeConfig.EnableField，fallback TableConfig.EnableField） */
  override get enableField(): string | null {
    return this.treeConfig?.EnableField ?? this.config.value?.EnableField ?? null
  }

  /** 树节点表单配置（EntityConfigDto） */
  protected get treeFormConfig(): EntityConfigDto | null {
    return this.treeTableConfig.value?.TreeFormConfig ?? null
  }

  /** 未选中树节点时的表格行为 */
  protected get noSelectionBehavior(): 'empty' | 'all' {
    return this.treeConfig?.NoSelectionBehavior ?? 'empty'
  }

  /** 关联字段名（PascalCase） */
  protected get relateField(): string {
    return this.treeConfig?.RelateField || 'ParentCode'
  }

  // ──── 自动注入的操作按钮（来自后端 /treepconfig） ────

  /** 树节点自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  get treeCustomActions(): Record<string, string> {
    return this.treeConfig?.CustomActions ?? {}
  }

  /** 表格行自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  get rowCustomButtons(): Record<string, string> {
    return this.config.value?.RowButtons?.CustomButtons ?? {}
  }

  /**
   * 树节点操作按钮（YzhAction[]，TT-9：完全由后端 TreeConfig 配置驱动）
   *
   * AllowEdit → 编辑（+ 新增下级，取决于 AllowAddChild）；AllowDelete → 删除；
   * EnableField → 禁用/启用（按节点状态动态显示单个）；CustomActions → 自定义动作。前端零硬编码。
   */
  get nodeActions(): (node: TreeNode) => YzhAction[] {
    return (node: TreeNode) => this.resolveTreeActions(node)
  }

  /** 树动作解析（子类可覆盖以追加自定义动作） */
  protected resolveTreeActions(node: TreeNode): YzhAction[] {
    const actions: YzhAction[] = []
    const tc = this.treeConfig
    if (!tc) return actions
    if (tc.AllowEdit) {
      if (this.allowAddChild) actions.push({ key: 'add-child', text: '新增下级' })
      actions.push({ key: 'edit', text: '编辑' })
    }
    if (tc.AllowDelete) {
      actions.push({ key: 'delete', text: '删除', type: 'danger', danger: true })
    }
    // 禁用/启用按钮：根据节点状态动态显示（只显示一个）
    if (tc.EnableField || this.enableField) {
      const field = tc.EnableField ?? this.enableField
      const extra = (node.Extra as any) || {}
      const val = extra[field] ?? extra[field.charAt(0).toLowerCase() + field.slice(1)] ?? 1
      if (val === 1) {
        // 已启用 → 只显示禁用
        actions.push({ key: 'toggle-disable', text: '禁用', type: 'warning' })
      } else {
        // 已禁用 → 只显示启用
        actions.push({ key: 'toggle-enable', text: '启用', type: 'warning' })
      }
    }
    // CustomActions：仅当无内置 toggle 时才显示（避免重复）
    if (!tc.EnableField && !this.enableField && tc.CustomActions) {
      for (const [method, label] of Object.entries(tc.CustomActions)) {
        actions.push({ key: `custom:${method}`, text: label, type: 'info' })
      }
    }
    return actions
  }

  /**
   * 获取树节点操作按钮的显示文字（toggle 按节点状态动态显示）
   */
  getNodeActionLabel(action: string, node: TreeNode): string {
    // 动态计算的 toggle 动作
    if (action === 'toggle-disable' || action === 'toggle-enable') {
      return action === 'toggle-disable' ? '禁用' : '启用'
    }
    const found = this.nodeActions(node).find((a) => a.key === action)
    return found?.text ?? action
  }

  /** 树节点表单布局列数（从 TreeFormConfig.FormCols 读取） */
  get treeFormLayoutCols(): number {
    return toFormLayoutCols(this.treeFormConfig)
  }

  /** 树节点表单字段配置（保留 TreeFormConfig 专用布局规则：ColSpan>1 占满整行） */
  get treeFormFields(): YzhFormField[] {
    const cols = this.treeFormConfig?.Columns
    const schema = this.treeFormConfig?.Schema
    if (!cols) return []
    const layoutCols = this.treeFormLayoutCols
    const baseSpan = Math.floor(24 / layoutCols)
    return cols
      .filter((c) => c.BcFlag && c.Type !== 'Other')
      .map((c) => {
        const prop = c.FieldName
        const camelKey = toCamelCase(prop)
        const fieldSchema = schema?.[camelKey]
        return {
          prop,
          label: c.DesName,
          type: mapTreeControlType(c.Type),
          required: !c.Yxk,
          disabled: c.Enable === false,
          span: ((c as any).ColSpan ?? 0) > 1 ? 24 : baseSpan,
          dictCode: c.DictCode || undefined,
          options: undefined,
          placeholder: c.Type?.includes('Picker')
            ? `请选择${c.DesName}`
            : `请输入${c.DesName}`,
          defaultValue: c.Mrz
            ? c.Type === 'Switch'
              ? Number(c.Mrz)
              : c.Mrz
            : (fieldSchema as any)?.Default,
          fieldSchema,
        }
      })
  }

  // ========================================================
  // 覆盖点（TT-8）
  // ========================================================

  /** 是否允许「新增下级」：默认读后端 TreeConfig.AllowAddChild（ISO-9，扁平树由后端配置 false） */
  protected get allowAddChild(): boolean {
    return this.treeConfig?.AllowAddChild ?? true
  }

  /** 新增行是否要求先选中树节点（无层级树可覆盖为 false，ROL-2） */
  protected get requireTreeSelectionForAdd(): boolean {
    return true
  }

  /** 是否允许在指定节点下新增（organization=仅叶子；返回 false 时给出提示） */
  protected canAddUnderNode(_node: TreeNode): boolean {
    return true
  }

  /** 指定节点不可新增时的提示文案 */
  protected canAddUnderNodeMessage(_node: TreeNode): string {
    return '该节点不允许新增'
  }

  /** 树节点新增默认值（合并到 TreeFormConfig.NewEntity 之后） */
  protected get defaultTreeValues(): Record<string, any> {
    return {}
  }

  /** 树节点确认弹窗名称字段（organization=OrgName 等） */
  protected get treeEntityNameField(): string {
    return this.treeConfig?.NameField ?? 'Name'
  }

  /** 关联过滤值：虚拟节点返回 null，其余返回选中节点 Code */
  protected relatedValue(): string | null {
    const node = this.selectedNode
    if (!node || this.isVirtualNode(node)) return null
    return node.Code
  }

  /** 是否对表格查询应用树过滤（dictionary 全量模式等可覆盖） */
  protected shouldApplyTreeFilter(): boolean {
    return !!this.selectedNode && !this.isVirtualNode(this.selectedNode)
  }

  /** 虚拟节点判定（skill-manage 的 __all__ 等） */
  protected isVirtualNode(node: TreeNode): boolean {
    return node.NodeType === 'virtual'
  }

  /** 树加载完成后钩子（skill-manage 注入"全部"虚拟节点） */
  protected async afterTreeLoaded(): Promise<void> {}

  /** 树加载后自动选中第一个节点（skill-manage=true） */
  protected get autoSelectFirstNode(): boolean {
    return false
  }

  // ──── 树节点生命周期钩子（与后端 OnBeforeAddTree/… 对齐，BE-5） ────

  protected onBeforeAddTree(_data: Record<string, any>, _parent: TreeNode | null) {}
  protected onAfterAddTree(_node: TreeNode, _parent: TreeNode | null) {}
  protected onBeforeUpdateTree(_node: TreeNode, _data: Record<string, any>) {}
  protected onAfterUpdateTree(_node: TreeNode, _data: Record<string, any>) {}
  protected onBeforeDeleteTree(_node: TreeNode): boolean | Promise<boolean> {
    return true
  }
  protected onAfterDeleteTree(_node: TreeNode) {}

  // ========================================================
  // 配置加载（覆盖：获取 TreeTableConfig）
  // ========================================================

  override async loadConfig(): Promise<void> {
    const res = await this.apiGet<ApiResponse<TreeTableConfigDto>>('/treepconfig')
    this.treeTableConfig.value = res.data
    // 同时将 TableConfig 赋值给 config，获得单表能力
    this.config.value = res.data.TableConfig
  }

  // ========================================================
  // 生命周期（TT-3）
  // ========================================================

  /** 初始化：配置 → 树 → afterTreeLoaded → 自动选中 → onAfterInit */
  override async init(): Promise<void> {
    await this.loadConfig()
    await this.loadTreeRoot()
    await this.afterTreeLoaded()
    if (this.autoSelectFirstNode && !this.selectedNode) {
      const first = this.treeData[0]
      if (first) await this.onNodeClick(first)
    }
    await this.onAfterInit()
  }

  // ========================================================
  // 树加载
  // ========================================================

  /** 加载根节点（/api/{controller}/tree/root） */
  async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const res = await this.apiPost<ApiResponse<TreeItemDto[]>>('/tree/root', {})
      const items = res.data ?? []
      this.treeSide.setNodes(items.map((dto) => this.dtoToNode(dto)))
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  /** 懒加载子节点（/api/{controller}/tree/children） */
  async loadChildren(
    node: any,
    resolve?: (data: TreeNode[]) => void,
  ): Promise<TreeNode[]> {
    const isEmptyArray = Array.isArray(node?.data) && node.data.length === 0
    const data = isEmptyArray ? node : (node?.data ?? node)
    const code = data?.Code
    const level = (data?.Extra?.level as number) ?? 0

    if (!code) {
      if (resolve) resolve([])
      return []
    }

    const res = await this.apiPost<ApiResponse<TreeItemDto[]>>('/tree/children', {
      ParentCode: code,
      Level: level,
    })
    const items = res.data ?? []
    const children = items.map((dto: TreeItemDto) => this.dtoToNode(dto, data))

    for (const child of children) this.treeSide.register(child, data)
    if (resolve) resolve(children)
    if (data && typeof data === 'object') {
      data.children = children
    }
    return children
  }

  // ========================================================
  // 树→表格联动（TT-6/TT-7）
  // ========================================================

  /** 节点点击 → 表格联动刷新（dataLoader 已自动注入 RelateField；箭头属性自动绑定 this） */
  onNodeClick = async (node: TreeNode): Promise<void> => {
    this.treeSide.selectedNode.value = node
    this.pagination.page = 1
    if ((this.treeConfig as any)?.OnlyLeafSelectable && !node.IsLeaf) {
      return
    }
    if (this._tableRef) {
      await this._tableRef.refresh()
    } else {
      await this.refreshTable()
    }
  }

  /**
   * 覆盖 buildFilters：自动注入 RelateField 树过滤（TT-6）
   */
  protected override buildFilters(extra?: Record<string, any>): FilterItem[] {
    const base = super.buildFilters(extra)
    if (this.shouldApplyTreeFilter()) {
      base.push({
        Field: this.relateField,
        Value: this.relatedValue(),
        Operator: 'eq',
      })
    }
    return base
  }

  /**
   * 覆盖 dataLoader：未选中节点且 NoSelectionBehavior='empty' 时不发请求
   */
  override async dataLoader(params: any): Promise<{ rows: V[]; total: number }> {
    if (!this.shouldApplyTreeFilter() && this.noSelectionBehavior === 'empty') {
      this.pagination.total = 0
      return { rows: [], total: 0 }
    }
    return super.dataLoader(params)
  }

  /** 带树条件的分页查询（兼容保留；新代码走统一 dataLoader） */
  async loadPageWithTree(treeCode: string): Promise<void> {
    this.loading.value = true
    try {
      const filters: FilterItem[] = [
        ...super.buildFilters(),
        { Field: this.relateField, Value: treeCode, Operator: 'eq' },
      ]
      const request: FilterRequest = {
        Page: this.pagination.page,
        PageSize: this.pagination.pageSize,
        SortField: this.sortField.value,
        SortOrder: this.sortOrder.value,
        Filters: filters,
      }
      const res = await this.apiPost<ApiResponse<PagedData<V>>>('/filter', request)
      const page = res.data
      if (page) {
        this.rows.value = this.postprocessRows((page.Items ?? []) as V[])
        this.pagination.total = page.TotalCount ?? 0
      }
    } catch {
      this.rows.value = []
      this.pagination.total = 0
    } finally {
      this.loading.value = false
    }
  }

  /** 无树条件的表格加载 */
  async loadPageWithoutTree(): Promise<void> {
    if (this.noSelectionBehavior === 'empty') {
      this.rows.value = []
      this.pagination.total = 0
    } else {
      await this.loadPage()
    }
  }

  /** 刷新右侧表格（保持当前选中节点） */
  async refreshTable(): Promise<void> {
    if (this.selectedNode && this.shouldApplyTreeFilter()) {
      if (this._tableRef) {
        await this._tableRef.refresh()
      } else {
        await this.loadPageWithTree(this.selectedNode.Code)
      }
    } else {
      await this.loadPageWithoutTree()
    }
  }

  // ========================================================
  // 行 CRUD 泛型流（TT-4）
  // ========================================================

  /**
   * 打开行弹窗（新增需满足树选中/叶子约束）
   * @returns 是否成功打开（失败时已给出提示）
   */
  openRowDialog(row?: V | null): boolean {
    if (!row) {
      const node = this.selectedNode
      if (this.requireTreeSelectionForAdd && !node) {
        ElMessage.warning('请先在左侧选择节点')
        return false
      }
      if (node && !this.isVirtualNode(node) && !this.canAddUnderNode(node)) {
        ElMessage.warning(this.canAddUnderNodeMessage(node))
        return false
      }
      this.dialogMode.value = 'add'
      this.formGroupIndex.value = '0'
      this.initFormData()
      this.onPrepareAdd(this.formData)
      this.dialogVisible.value = true
      return true
    }
    this.openEditDialog(row)
    return true
  }

  /** 提交行表单（= submitForm 别名，语义化入口） */
  async submitRowForm(): Promise<void> {
    await this.submitForm()
  }

  /** 删除单行（带确认，名称取 entityName） */
  async deleteRow(row: V): Promise<void> {
    await this.confirmDelete([row])
  }

  /** 批量删除选中行（带确认） */
  async batchDeleteRows(rows?: V[]): Promise<void> {
    await this.confirmDelete(rows)
  }

  // ========================================================
  // 树节点 CRUD 泛型流（TT-5）
  // ========================================================

  /**
   * 打开树节点弹窗
   * @param node 编辑目标（null=新增）
   * @param parent 新增时的父节点（缺省取当前选中节点）
   */
  openTreeNodeDialog(node: TreeNode | null = null, parent: TreeNode | null = null): boolean {
    if (node) {
      this.treeDialogMode.value = 'edit'
      this.treeEditingNode.value = node
      this.treeParentNode.value = null
      this.resetObject(this.treeFormData)

      const tmpl = (this.treeFormConfig?.NewEntity as any) || {}
      const extra = (node.Extra as any) || {}
      // 按声明的表单字段白名单从 Extra 取回填值（DIC-4 上移基类）
      const picked: Record<string, any> = {}
      for (const f of this.treeFormFields) {
        if (f.prop in extra) picked[f.prop as string] = extra[f.prop as string]
      }
      Object.assign(this.treeFormData, tmpl, picked, {
        Code: node.Code,
        ParentCode: node.ParentCode,
        [this.treeEntityNameField]: node.Name,
      })
      this.treeDialogVisible.value = true
      return true
    }

    // 新增
    const targetParent = parent ?? this.selectedNode
    if (this.requireTreeSelectionForAdd && !targetParent) {
      ElMessage.warning('请先在左侧选择节点')
      return false
    }
    if (targetParent && !this.canAddUnderNode(targetParent)) {
      ElMessage.warning(this.canAddUnderNodeMessage(targetParent))
      return false
    }
    this.treeDialogMode.value = 'add'
    this.treeEditingNode.value = null
    this.treeParentNode.value = targetParent
    this.resetObject(this.treeFormData)
    const tmpl = (this.treeFormConfig?.NewEntity as any) || {}
    Object.assign(this.treeFormData, tmpl, this.defaultTreeValues, {
      [this.treeEntityNameField]: '',
      ParentCode: targetParent?.Code ?? this.treeConfig?.RootParentCode ?? null,
    })
    this.treeDialogVisible.value = true
    return true
  }

  /** 提交树节点表单 */
  async submitTreeNodeForm(): Promise<void> {
    this.treeSubmitting.value = true
    try {
      const payload = this.normalizeBeforeSubmit({ ...this.treeFormData })
      if (this.treeDialogMode.value === 'add') {
        this.onBeforeAddTree(payload, this.treeParentNode.value)
        const created = await this.addTreeNode(this.treeParentNode.value, payload)
        if (created) this.onAfterAddTree(created, this.treeParentNode.value)
      } else {
        const node = this.treeEditingNode.value!
        this.onBeforeUpdateTree(node, payload)
        await this.updateTreeNode(
          node,
          payload[this.treeEntityNameField] ?? '',
          payload,
        )
        this.onAfterUpdateTree(node, payload)
      }
      this.treeDialogVisible.value = false
      ElMessage.success(this.treeDialogMode.value === 'add' ? '创建成功' : '修改成功')
    } finally {
      this.treeDialogMode.value = 'add'
      this.treeEditingNode.value = null
      this.treeSubmitting.value = false
    }
  }

  /** 删除树节点（完整流程：确认弹窗 → API → 本地更新 → 表格联动） */
  async deleteTreeNodeWithConfirm(node: TreeNode): Promise<void> {
    const name = node.Name
    const ok = await this.onBeforeDeleteTree(node)
    if (!ok) return
    await ElMessageBox.confirm(`确定删除【${name}】？`, '删除确认', {
      type: 'warning',
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
    })
    await this.deleteTreeNode(node, true)
    this.onAfterDeleteTree(node)
    ElMessage.success('已删除')
  }

  // ========================================================
  // 树节点底层操作（兼容保留）
  // ========================================================

  /** 新增树节点（/api/{controller}/tree/add） */
  async addTreeNode(
    parentNode: TreeNode | null,
    data: Record<string, any>,
  ): Promise<TreeNode | null> {
    const codeField = this.treeConfig?.CodeField ?? 'Code'
    const requestData = {
      ...pascalCaseFormData(data),
      [this.treeConfig?.ParentCodeField ?? 'ParentCode']:
        parentNode?.Code ?? this.treeConfig?.RootParentCode ?? null,
    } as Record<string, any>
    const res = await this.apiPost<ApiResponse<TreeItemDto>>('/tree/add', requestData)
    // 支持前端预分配 Code：后端未返回时使用请求中的 Code 构造本地节点
    const backendCode = (res.data as any)?.[codeField] ?? ''
    const localCode = backendCode || requestData[codeField]
    const newNode = this.dtoToNode(
      res.data ?? { Code: localCode, Name: requestData['Name'] ?? '', ParentCode: parentNode?.Code ?? null } as TreeItemDto,
      parentNode ?? undefined,
    )
    if (localCode && !res.data) {
      newNode.Code = localCode
    }

    // 通过 el-tree API 直接追加节点（不 reload，不重复）
    if (this._treeTableRef) {
      this._treeTableRef.appendNode(parentNode?.Code ?? null, newNode)
      this.treeSide.register(newNode, parentNode)
    } else {
      this.treeSide.appendChild(parentNode?.Code ?? null, newNode)
    }

    return newNode
  }

  /** 修改树节点（/api/{controller}/tree/update） */
  async updateTreeNode(
    node: TreeNode,
    newName: string,
    extra?: Record<string, any>,
  ): Promise<void> {
    const pascalExtra = extra ? pascalCaseFormData(extra) : {}
    const requestData = {
      [this.treeConfig?.CodeField ?? 'Code']: node.Code,
      [this.treeConfig?.NameField ?? 'Name']: newName,
      ...pascalExtra,
    }
    const res = await this.apiPost<ApiResponse<TreeItemDto>>('/tree/update', requestData)

    // O(1) 原位替换（保持展开状态）
    const newNode = this.dtoToNode(
      res.data ?? ({ ...node, Name: newName } as TreeItemDto),
      this.treeSide.findParent(node.Code),
    )
    if (!this.treeSide.replaceNode(node.Code, newNode)) {
      node.Name = newName
    }
  }

  /** 删除树节点（skipConfirm=true 时由调用方负责确认） */
  async deleteTreeNode(node: TreeNode, skipConfirm = false): Promise<void> {
    // 前端预检：非级联删除模式下，本地有子节点则直接拦截
    if (
      !this.treeConfig?.AllowDeleteWithChildren &&
      node.Children &&
      node.Children.length > 0
    ) {
      ElMessage.warning('该节点包含子节点，请先删除子节点')
      return
    }

    if (!skipConfirm) {
      await ElMessageBox.confirm(`确定删除节点 "${node.Name}"？`, '删除确认', {
        type: 'warning',
        confirmButtonText: '确定',
        cancelButtonText: '取消',
      })
    }

    const res = await this.apiPost<ApiResponse<string>>('/tree/delete', [node.Code])
    if (!res.success) {
      ElMessage.error(res.message || '删除失败')
      return
    }

    this.treeSide.removeNode(node.Code)

    // 尝试通过 el-tree API 局部删除（保留展开状态）；失败则 reload 树根
    if (this._treeTableRef?.removeNode) {
      try {
        this._treeTableRef.removeNode(null, node.Code)
      } catch {
        await this.loadTreeRoot()
      }
    } else {
      await this.loadTreeRoot()
    }

    if (this.selectedNode?.Code === node.Code) {
      this.treeSide.selectedNode.value = null
      await this.loadPageWithoutTree()
    }
  }

  /** 树节点执行自定义操作 */
  async executeTreeAction(
    methodName: string,
    node: TreeNode,
    extra?: Record<string, any>,
  ): Promise<any> {
    const pascalExtra = extra ? pascalCaseFormData(extra) : {}
    const res = await this.apiPost<ApiResponse<any>>(`/tree/action/${methodName}`, {
      [this.treeConfig?.CodeField ?? 'Code']: node.Code,
      ...pascalExtra,
    })
    await this.loadTreeRoot()
    return res.data
  }

  /** 切换树节点有效标志（自动更新 node.Extra[enableField]） */
  async toggleTreeNodeIsValid(
    node: TreeNode,
  ): Promise<{ Code: string; IsValid: number } | null> {
    const field = this.enableField ?? 'IsValid'
    const res = await this.apiPost<ApiResponse<{ Code: string; IsValid: number }>>(
      '/tree/toggle-valid',
      { [this.treeConfig?.CodeField ?? 'Code']: node.Code },
    )
    if (res.success) {
      const extra = (node.Extra as any) || {}
      // 双 Key 写入：PascalCase（业务 Controller override）+ camelCase（TreeMapper 默认）
      extra[field] = res.data.IsValid
      const camelField = field.charAt(0).toLowerCase() + field.slice(1)
      if (camelField !== field) extra[camelField] = res.data.IsValid
      node.Extra = { ...extra }
      ElMessage.success(res.data.IsValid === 1 ? '已启用' : '已禁用')
      return res.data
    }
    return null
  }

  /** 切换树节点有效标志（完整流程：确认弹窗 → API → 本地更新） */
  async toggleTreeNodeWithConfirm(
    node: TreeNode,
    options?: { entityName?: string },
  ): Promise<void> {
    const field = this.enableField ?? 'IsValid'
    const extra = (node.Extra as any) || {}
    // 双 Key 读取：PascalCase（业务 Controller override）+ camelCase（TreeMapper 默认）
    const camelField = field.charAt(0).toLowerCase() + field.slice(1)
    const currentVal = extra[field] ?? extra[camelField] ?? 1
    const action = currentVal === 1 ? '禁用' : '启用'
    const name = options?.entityName ?? node.Name

    await ElMessageBox.confirm(`确定${action}【${name}】？`, `${action}确认`, {
      type: 'warning',
      confirmButtonText: `确定${action}`,
      cancelButtonText: '取消',
    })

    await this.toggleTreeNodeIsValid(node)
  }

  // ========================================================
  // DTO → TreeNode 映射（AD-5）
  // ========================================================

  /** TreeItemDto → TreeNode（PascalCase，附 level 计算并注册索引） */
  protected dtoToNode(dto: TreeItemDto, parent?: TreeNode | null): TreeNode {
    const level = ((parent?.Extra?.level as number) ?? -1) + 1
    return {
      Code: dto.Code,
      Name: dto.Name,
      ParentCode: dto.ParentCode ?? null,
      NodeType: dto.NodeType,
      IsLeaf: dto.IsLeaf,
      Extra: { ...dto.Extra, level },
      Children: [],
    }
  }

  // ========================================================
  // dispatch 扩展（TT-10）：树节点动作路由
  // ========================================================

  /** 树节点动作入口（绑定 @tree-node-action="logic.onNodeAction"；箭头属性自动绑定 this） */
  onNodeAction = async (key: string, node: TreeNode): Promise<void> => {
    // 命中自定义处理器优先
    const handler = (this as any).handlers?.get?.(key)
    if (handler) {
      await handler(node, undefined)
      return
    }
    switch (key) {
      case 'add-child':
        this.openTreeNodeDialog(null, node)
        return
      case 'add-root':
        this.openTreeNodeDialog(null, null)
        return
      case 'edit':
      case 'node-edit':
        this.openTreeNodeDialog(node)
        return
      case 'delete':
      case 'node-delete':
        await this.deleteTreeNodeWithConfirm(node)
        return
      case 'toggle-valid':
      case 'node-toggle-valid':
        await this.toggleTreeNodeWithConfirm(node)
        return
      default:
        if (key.startsWith('custom:')) {
          await this.executeTreeAction(key.slice(7), node)
        }
    }
  }

  // ========================================================
  // 树 Split 方法（O(1)，基于 TreeSide 索引）
  // ========================================================

  /** @deprecated 兼容旧命名，等价 treeSide.removeNode */
  protected removeNodeFromTree(code: string): void {
    this.treeSide.removeNode(code)
  }

  /** @deprecated 兼容旧命名，等价 treeSide.findNode（O(1)） */
  protected findNode(code: string): TreeNode | null {
    return this.treeSide.findNode(code)
  }

  /** @deprecated 兼容旧命名，等价 treeSide.replaceNode */
  protected replaceTreeNode(code: string, newNode: TreeNode): void {
    this.treeSide.replaceNode(code, newNode)
  }

  async refreshChildren(node: TreeNode): Promise<void> {
    const res = await this.apiPost<ApiResponse<TreeItemDto[]>>('/tree/children', {
      ParentCode: node.Code,
      Level: (node.Extra?.level as number) ?? 0,
    })
    const items = res.data ?? []
    const children = items.map((dto) => this.dtoToNode(dto, node))
    for (const child of children) this.treeSide.register(child, node)
    node.Children = children
    node.IsLeaf = children.length === 0
  }

  async refreshTree(): Promise<void> {
    await this.loadTreeRoot()
  }

  // ========================================================
  // 兼容便捷方法
  // ========================================================

  onCheckChange(_node: TreeNode, _checked: boolean): void {}

  async addRootNode(data: Record<string, any>): Promise<TreeNode | null> {
    return this.addTreeNode(null, data)
  }

  async addChildNode(
    parentNode: TreeNode,
    data: Record<string, any>,
  ): Promise<TreeNode | null> {
    return this.addTreeNode(parentNode, data)
  }

  async renameNode(node: TreeNode, newName: string): Promise<void> {
    await this.updateTreeNode(node, newName)
  }
}

export default TreeTableCore
