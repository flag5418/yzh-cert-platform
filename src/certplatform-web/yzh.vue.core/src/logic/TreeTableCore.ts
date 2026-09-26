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

import { ElMessage } from 'element-plus'
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
import { expectOk } from '../utils/apiResponse'
import { confirmOrFalse } from '../utils/confirm'
import { isRowEnabled, toFormLayoutCols } from '../adapters/entityAdapters'
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

  /**
   * 树动作解析（子类可覆盖以追加自定义动作）
   *
   * AllowToggle 三态（对齐 TreeConfig.AllowToggle）：
   * - false   → 不出内置 toggle，按 CustomActions 状态二选一（disable/enable）
   * - true    → EnableField 存在则出 toggle；无 EnableField 再看 CustomActions
   * - 未设置  → 旧行为：EnableField 存在则出 toggle；否则 CustomActions 全显
   */
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

    const field = tc.EnableField ?? this.enableField
    const allowToggle = tc.AllowToggle !== false

    if (allowToggle && field) {
      const extra = (node.Extra as any) || {}
      const camel = field.charAt(0).toLowerCase() + field.slice(1)
      const val = extra[field] ?? extra[camel] ?? 1
      if (isRowEnabled(val)) {
        actions.push({ key: 'toggle-disable', text: '禁用', type: 'warning' })
      } else {
        actions.push({ key: 'toggle-enable', text: '启用', type: 'success' })
      }
    } else if (tc.CustomActions) {
      if (!allowToggle) {
        const extra = (node.Extra as any) || {}
        const statusField = field ?? 'IsValid'
        const camel = statusField.charAt(0).toLowerCase() + statusField.slice(1)
        const val = extra[statusField] ?? extra[camel] ?? 1
        for (const [method, label] of Object.entries(tc.CustomActions)) {
          if (method === 'disable') {
            if (isRowEnabled(val)) actions.push({ key: `custom:${method}`, text: label, type: 'warning' })
          } else if (method === 'enable') {
            if (!isRowEnabled(val)) actions.push({ key: `custom:${method}`, text: label, type: 'success' })
          } else {
            actions.push({ key: `custom:${method}`, text: label, type: 'info' })
          }
        }
      } else {
        for (const [method, label] of Object.entries(tc.CustomActions)) {
          actions.push({ key: `custom:${method}`, text: label, type: 'info' })
        }
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

  /** 未选中树节点时的提示文案（organization=请先选择机构） */
  protected get requireTreeSelectionMessage(): string {
    return '请先在左侧选择节点'
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
    // F-1：失败必抛；先解包再取子字段，避免 res.data 为 undefined 时 TypeError
    expectOk(res, '加载页面配置失败')
    this.treeTableConfig.value = res.data
    // 同时将 TableConfig 赋值给 config，获得单表能力
    this.config.value = res.data.TableConfig
  }

  // ========================================================
  // 生命周期（TT-3）
  // ========================================================

  /** 初始化：配置 → 树 → afterTreeLoaded → 自动选中 → onAfterInit */
  override async init(): Promise<void> {
    try {
      await this.loadConfig()
      await this.loadTreeRoot()
      await this.afterTreeLoaded()
      if (this.autoSelectFirstNode && !this.selectedNode) {
        const first = this.treeData[0]
        if (first) await this.onNodeClick(first)
      }
      await this.onAfterInit()
    } catch (e) {
      // 读路径最外层兜底：失败保留现状（空态/旧数据）+ 一条错误提示
      this.reportError(e, '页面初始化失败')
    }
  }

  // ========================================================
  // 树加载
  // ========================================================

  /** 加载根节点（/api/{controller}/tree/root） */
  async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const res = await this.apiPost<ApiResponse<TreeItemDto[]>>('/tree/root', {})
      // F-1：失败必抛（不再 `res.data ?? []` 静默变空树）
      expectOk(res, '加载树失败')
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

    // ★ 已登记例外 E8（22 §九）：el-tree 懒加载回调内抛错会卡住 loading 且无法自愈，
    //   故此路径**不向调用方 throw**，改为就地提示 + 返回空，用户可重新展开重试。
    let res: ApiResponse<TreeItemDto[]>
    try {
      res = await this.apiPost<ApiResponse<TreeItemDto[]>>('/tree/children', {
        ParentCode: code,
        Level: level,
      })
      expectOk(res, '加载子节点失败')
    } catch (e) {
      this.reportError(e, '加载子节点失败')
      if (resolve) resolve([])
      return []
    }

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
    try {
      if (this._tableRef) {
        await this._tableRef.refresh()
      } else {
        await this.refreshTable()
      }
    } catch (e) {
      this.reportError(e, '查询失败')
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
      expectOk(res, '查询失败')
      const page = res.data
      if (page) {
        this.rows.value = this.postprocessRows((page.Items ?? []) as V[])
        this.pagination.total = page.TotalCount ?? 0
      }
    } catch (e) {
      // D1：失败保留上次数据，只提示
      this.reportError(e, '查询失败')
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
        ElMessage.warning(this.requireTreeSelectionMessage)
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
      ElMessage.warning(this.requireTreeSelectionMessage)
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

  /**
   * 提交树节点表单（F-1 / F-3）
   *
   * 只有 expectOk 通过才执行 onAfter* 钩子、成功提示、关弹窗、重置编辑态。
   * 失败 → 只提示，**弹窗保持打开且 treeDialogMode 不重置**（否则下次提交会被当新增）。
   *
   * @returns true=保存成功
   */
  async submitTreeNodeForm(): Promise<boolean> {
    this.treeSubmitting.value = true
    try {
      const payload = this.normalizeBeforeSubmit({ ...this.treeFormData })
      const isAdd = this.treeDialogMode.value === 'add'
      if (isAdd) {
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
      ElMessage.success(isAdd ? '创建成功' : '修改成功')
      // 成功才收尾：重置编辑态供下次新增
      this.treeDialogMode.value = 'add'
      this.treeEditingNode.value = null
      return true
    } catch (e) {
      this.reportError(e, '保存失败')
      return false
    } finally {
      this.treeSubmitting.value = false
    }
  }

  /**
   * 删除树节点（完整流程：确认弹窗 → API → 本地更新 → 表格联动）
   *
   * F-1：删除成功才调 onAfterDeleteTree + 弹提示。
   * F-4：取消静默。
   *
   * @returns true=删除成功；false=被预检拦截 / 业务失败（D2 签名变更）
   */
  async deleteTreeNodeWithConfirm(node: TreeNode): Promise<boolean> {
    const name = node.Name
    const ok = await this.onBeforeDeleteTree(node)
    if (!ok) return false

    const confirmed = await confirmOrFalse(`确定删除【${name}】？`, '删除确认', {
      confirmButtonText: '确定删除',
      cancelButtonText: '取消',
    })
    if (!confirmed) return false

    let deleted = false
    try {
      deleted = await this.deleteTreeNode(node, true)
    } catch (e) {
      // 失败：节点不动、不调 onAfterDeleteTree、不弹「已删除」
      this.reportError(e, '删除失败')
      return false
    }
    if (!deleted) return false

    this.onAfterDeleteTree(node)
    ElMessage.success('已删除')
    return true
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
    // F-1：失败必抛 —— 绝不在 success:false 时用请求体兜底造「幽灵节点」
    expectOk(res, '新增节点失败')
    // 支持前端预分配 Code：仅在 success:true 且后端未回传 data 时用请求中的 Code 构造本地节点
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
    // F-1：失败必抛 —— 不在业务被拒时改本地 Name（否则本地假更新）
    expectOk(res, '修改节点失败')

    // O(1) 原位更新：**保留节点对象引用**。el-tree store 持有的是同一 data 对象，
    // 整体换新对象会残留旧节点（Element Plus 对同引用数组 setData 短路 → 界面上新旧节点并存），
    // 且 dtoToNode 固定返回 Children:[] 会就地吞掉子节点。
    const newNode = this.dtoToNode(
      res.data ?? ({ ...node, Name: newName } as TreeItemDto),
      this.treeSide.findParent(node.Code),
    )
    const live = this.treeSide.findNode(node.Code)
    if (live) {
      const children = live.Children
      Object.assign(live, newNode)
      live.Children = children
    } else {
      node.Name = newName
    }
  }

  /**
   * 删除树节点（skipConfirm=true 时由调用方负责确认）
   *
   * D2 签名变更：`Promise<void>` → `Promise<boolean>`
   *   true  = 已删除并完成本地清理
   *   false = 前端预检拦截（有子节点）/ 用户取消（未发起请求）
   *   @throws BizError 业务失败（调用方 catch，不改本地）
   */
  async deleteTreeNode(node: TreeNode, skipConfirm = false): Promise<boolean> {
    // 前端预检：非级联删除模式下，本地有子节点则直接拦截
    if (
      !this.treeConfig?.AllowDeleteWithChildren &&
      node.Children &&
      node.Children.length > 0
    ) {
      ElMessage.warning('该节点包含子节点，请先删除子节点')
      return false
    }

    if (!skipConfirm) {
      const confirmed = await confirmOrFalse(`确定删除节点 "${node.Name}"？`, '删除确认', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
      })
      if (!confirmed) return false
    }

    const res = await this.apiPost<ApiResponse<string>>('/tree/delete', [node.Code])
    // F-1：失败必抛（不再 return 静默 —— 那正是假删除的根源）
    expectOk(res, '删除失败')

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
    return true
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
    // F-1：判定必须在刷新之前 —— 失败不再用 loadTreeRoot 掩盖（失败不刷新）
    expectOk(res, '操作失败')
    await this.loadTreeRoot()
    return res.data
  }

  /**
   * 切换树节点有效标志（自动更新 node.Extra[enableField]）
   * @throws BizError 业务失败（D4：不再静默 return null）
   */
  async toggleTreeNodeIsValid(node: TreeNode): Promise<{ Code: string; IsValid: number }> {
    const field = this.enableField ?? 'IsValid'
    const res = await this.apiPost<ApiResponse<{ Code: string; IsValid: number }>>(
      '/tree/toggle-valid',
      { [this.treeConfig?.CodeField ?? 'Code']: node.Code },
    )
    expectOk(res, '切换状态失败')
    const extra = (node.Extra as any) || {}
    // 双 Key 写入：PascalCase（业务 Controller override）+ camelCase（TreeMapper 默认）
    extra[field] = res.data.IsValid
    const camelField = field.charAt(0).toLowerCase() + field.slice(1)
    if (camelField !== field) extra[camelField] = res.data.IsValid
    node.Extra = { ...extra }
    ElMessage.success(res.data.IsValid === 1 ? '已启用' : '已禁用')
    return res.data
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
    const action = isRowEnabled(currentVal) ? '禁用' : '启用'
    const name = options?.entityName ?? node.Name

    // F-4：取消静默
    const confirmed = await confirmOrFalse(`确定${action}【${name}】？`, `${action}确认`, {
      confirmButtonText: `确定${action}`,
      cancelButtonText: '取消',
    })
    if (!confirmed) return

    try {
      await this.toggleTreeNodeIsValid(node)
    } catch (e) {
      // F-3：流程方法统一提示；本地 Extra 不更新
      this.reportError(e, `${action}失败`)
    }
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
    try {
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
        case 'toggle-disable':
        case 'toggle-enable':
          await this.toggleTreeNodeWithConfirm(node)
          return
        default:
          if (key.startsWith('custom:')) {
            const method = key.slice(7)
            const confirmMsg = this.confirmTreeActionMessage(method, node)
            if (confirmMsg) {
              // F-4：取消静默
              const confirmed = await confirmOrFalse(confirmMsg, '操作确认', {
                confirmButtonText: '确定',
                cancelButtonText: '取消',
              })
              if (!confirmed) return
            }
            // F-1：executeTreeAction 内部 expectOk，失败在此抛出 → 不刷新、不弹成功
            const result = await this.executeTreeAction(method, node)
            try {
              await this.refreshTable()
            } catch (e) {
              // 动作已成功，仅列表刷新失败：单独提示，不覆盖成功反馈
              this.reportError(e, '刷新失败')
            }
            if (typeof result === 'string' && result) {
              ElMessage.success(result)
            }
          }
      }
    } catch (e) {
      // 顶层兜底：动作失败 → 只弹错误，不刷树、不弹成功
      this.reportError(e, '操作失败')
    }
  }

  /**
   * 树自定义动作确认文案（返回 null = 不弹确认）。
   * organization 可覆写为级联禁用提示等。
   */
  protected confirmTreeActionMessage(method: string, node: TreeNode): string | null {
    if (method === 'disable') return `确定禁用【${node.Name}】？`
    if (method === 'enable') return `确定启用【${node.Name}】？`
    return null
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
    // F-1：失败必抛 —— 不在 success:false 时把 node.Children 清空
    expectOk(res, '刷新子节点失败')
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
