/**
 * TreeTableLogic - 左树右表 Logic 基类（V1.2 - 对齐 YZH.Core.Stand）
 *
 * 数据访问规则（与 YZH.Core.Stand 严格一致）：
 * - res.data：ApiResponse 顶层（camelCase）
 * - res.data.Items / res.data.TotalCount：业务实体（PascalCase）
 * - TreeItemDto 字段：PascalCase
 * - formData：camelCase key（NewEntity 字典 key，反射 ToCamelCase）
 * - TreeNode 字段：PascalCase（与后端 DTO 保持一致）
 *
 * 架构：
 * - 继承 CrudPageLogic<V> 获得全部单表 CRUD 能力
 * - 叠加树能力：加载根节点 / 懒加载子节点 / 树→表格联动
 * - 配置驱动：根据 TreeTableConfig 动态渲染 UI
 * - Split 数据方法实现树节点增量更新
 */

import type { YzhFormField } from '@yzh-core/components/form'
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
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
import {
  CrudPageLogic,
  pascalCaseFormData,
  rowToFormData,
  toCamelCase,
  toPascalCase,
} from './CrudPageLogic'

// ========================================================
// 工具函数
// ========================================================

/** ColumnConfig.Type → YzhFormField.type */
function mapControlType(type: string): YzhFormField['type'] {
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

// ========================================================
// 主类
// ========================================================

export abstract class TreeTableLogic<
  V extends Record<string, any> = any,
> extends CrudPageLogic<V> {
  // ──── 树状态 ────

  /** 树数据（PascalCase，与后端 DTO 保持一致） */
  treeData = ref<TreeNode[]>([])

  /** 树加载状态 */
  treeLoading = ref(false)

  /** 当前选中节点 */
  selectedNode = ref<TreeNode | null>(null)

  /** 完整树表配置（PascalCase，YZH.Core.Stand/TreeTableConfigDto） */
  treeTableConfig = ref<TreeTableConfigDto | null>(null)

  // ──── 树节点表单弹窗状态 ────

  treeDialogVisible = ref(false)
  treeDialogMode = ref<'add' | 'edit'>('add')
  treeSubmitting = ref(false)
  /**
   * 树节点表单数据：camelCase key（NewEntity 字典 key）
   */
  treeFormData = reactive<Record<string, any>>({})

  /** 当前新增节点的父节点 */
  treeParentNode = ref<TreeNode | null>(null)

  /** 当前编辑的节点 */
  treeEditingNode = ref<TreeNode | null>(null)

  // ──── 树表组件引用（用于 appendNode 等直接操作） ────
  private _treeTableRef: any = null

  /** 设置树表组件引用（模板中调用：logic.setTreeTableRef(treeTableRef.value)） */
  setTreeTableRef(ref: any) {
    this._treeTableRef = ref
  }

  // ──── 树配置快捷访问 ────

  /** 树行为配置（YZH.Core.Stand/TreeBehaviorConfigDto） */
  protected get treeConfig(): TreeBehaviorConfig | null {
    return this.treeTableConfig.value?.TreeConfig ?? null
  }

  /** 启用/禁用字段名（优先从 TreeConfig.EnableField 读取，fallback 到 TableConfig.EnableField） */
  get enableField(): string | null {
    return this.treeConfig?.EnableField ?? this.config.value?.EnableField ?? null
  }

  /**
   * 表格行操作按钮（覆盖基类：自动注入 toggle-valid）
   *
   * 根据 rb.Enable === true && enableField 自动添加「禁用/启用」按钮
   */
  get rowActionButtons(): Record<string, string> {
    const rb = this.config.value?.RowButtons
    const buttons: Record<string, string> = {}
    if (rb?.Edit !== false) buttons['edit'] = '编辑'
    if (rb?.Delete !== false) buttons['delete'] = '删除'
    if (rb?.Enable === true && this.enableField) {
      buttons['toggle-valid'] = '禁用/启用'
    }
    // 合入自定义按钮
    if (rb?.CustomButtons) {
      for (const [label, method] of Object.entries(rb.CustomButtons)) {
        buttons[method] = label
      }
    }
    return buttons
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

  // ──── 自动注入的操作按钮（来自后端 /config） ────

  /** 树节点自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  get treeCustomActions(): Record<string, string> {
    return this.treeConfig?.CustomActions ?? {}
  }

  /** 表格行自定义操作按钮：{ 方法名: 显示文字 }（后端自动注入） */
  get rowCustomButtons(): Record<string, string> {
    return this.config.value?.RowButtons?.CustomButtons ?? {}
  }

  /**
   * 树节点操作按钮（默认实现，子类可覆写）
   *
   * 根据 treeConfig.AllowEdit / AllowDelete / enableField 动态生成：
   * - AllowEdit → edit + add-child
   * - AllowDelete → delete
   * - enableField → toggle-valid
   */
  get nodeActions(): Record<string, string> {
    const actions: Record<string, string> = {}
    const tc = this.treeConfig
    if (!tc) return actions
    if (tc.AllowEdit) {
      actions['add-child'] = '新增下级'
      actions['edit'] = '编辑'
    }
    if (tc.AllowDelete) {
      actions['delete'] = '删除'
    }
    if (this.enableField) {
      actions['toggle-valid'] = '禁用/启用'
    }
    return actions
  }

  /**
   * 获取树节点操作按钮的显示文字（默认实现，子类可覆写）
   *
   * toggle-valid 按钮根据当前节点状态动态显示「禁用」或「启用」
   */
  getNodeActionLabel(action: string, node: TreeNode): string {
    if (action === 'toggle-valid') {
      const field = this.enableField ?? 'IsValid'
      const extra = (node.Extra as any) || {}
      const val = extra[field] ?? 1
      return val === 1 ? '禁用' : '启用'
    }
    return this.nodeActions[action] || action
  }

  /** 树节点表单字段配置 */
  get treeFormFields(): YzhFormField[] {
    const cols = this.treeFormConfig?.Columns
    const schema = this.treeFormConfig?.Schema
    if (!cols) return []
    const colSpan = 12
    return cols
      .filter((c) => c.BcFlag && c.Type !== 'Other')
      .map((c) => {
        const prop = c.FieldName // PascalCase
        // Schema 字典 key 是 camelCase
        const camelKey = toCamelCase(prop)
        const fieldSchema = schema?.[camelKey]
        return {
          prop,
          label: c.DesName,
          type: mapControlType(c.Type),
          required: !c.Yxk,
          disabled: !c.Enable,
          span: ((c as any).ColSpan ?? 0) > 1 ? 24 : colSpan,
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
  // 配置加载（覆盖：获取 TreeTableConfig）
  // ========================================================

  /** 加载页面配置（覆盖：获取 TreeTableConfig） */
  protected async loadConfig(): Promise<void> {
    const res = await this.apiGet<ApiResponse<TreeTableConfigDto>>('/treepconfig')
    this.treeTableConfig.value = res.data
    // 同时将 TableConfig 赋值给 config，获得单表能力
    this.config.value = res.data.TableConfig
  }

  // ========================================================
  // 树加载
  // ========================================================

  /** 加载根节点（/api/{controller}/tree/root） */
  async loadTreeRoot(): Promise<void> {
    this.treeLoading.value = true
    try {
      const res = await this.apiPost<ApiResponse<TreeItemDto[]>>(
        '/tree/root',
        {},
      )
      const items = res.data ?? []
      this.treeData.value = items.map((dto) => this.dtoToNode(dto))
    } finally {
      this.treeLoading.value = false
    }
  }

  /**
   * 懒加载子节点（/api/{controller}/tree/children）
   */
  async loadChildren(
    node: any,
    resolve?: (data: TreeNode[]) => void,
  ): Promise<TreeNode[]> {
    const isEmptyArray = Array.isArray(node?.data) && node.data.length === 0
    const data = isEmptyArray ? node : (node?.data ?? node)
    const code = data?.Code
    const level = (data?.Extra?.level as number) ?? 0

    // 防御性：code 为空时不调用 API，直接返回空数组
    if (!code) {
      if (resolve) resolve([])
      return []
    }

    const res = await this.apiPost<ApiResponse<TreeItemDto[]>>(
      '/tree/children',
      {
        ParentCode: code,
        Level: level,
      },
    )
    const items = res.data ?? []
    const children = items.map((dto: TreeItemDto) => this.dtoToNode(dto, data))

    if (resolve) {
      resolve(children)
    }

    if (data && typeof data === 'object') {
      data.children = children
    }
    return children
  }

  // ========================================================
  // 树→表格联动
  // ========================================================

  /** 节点点击 → 加载该节点下表格数据 */
  async onNodeClick(node: TreeNode): Promise<void> {
    this.selectedNode.value = node
    this.pagination.page = 1
    if ((this.treeConfig as any)?.OnlyLeafSelectable && !node.IsLeaf) {
      return
    }
    await (this as any)._tableRef?.refresh()
  }

  /** 带树条件的分页查询 */
  async loadPageWithTree(treeCode: string): Promise<void> {
    this.loading.value = true
    try {
      const filters: FilterItem[] = [
        ...this.buildFilters(),
        { Field: this.relateField, Value: treeCode, Operator: 'eq' },
      ]
      const request: FilterRequest = {
        Page: this.pagination.page,
        PageSize: this.pagination.pageSize,
        SortField: this.sortField.value,
        SortOrder: this.sortOrder.value,
        Filters: filters,
      }
      const res = await this.apiPost<ApiResponse<PagedData<V>>>(
        '/filter',
        request,
      )
      const page = res.data
      if (page) {
        this.rows.value = page.Items ?? []
        this.pagination.total = page.TotalCount ?? 0
      }
    } catch (e: any) {
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

  // ========================================================
  // 树节点 CRUD
  // ========================================================

  /** 新增树节点（/api/{controller}/tree/add） */
  async addTreeNode(
    parentNode: TreeNode | null,
    data: Record<string, any>,
  ): Promise<TreeNode | null> {
    // data 是 camelCase key（formData），转 PascalCase 提交后端
    const pascalData = pascalCaseFormData(data)
    const requestData = {
      ...pascalData,
      [this.treeConfig?.ParentCodeField ?? 'ParentCode']:
        parentNode?.Code ?? this.treeConfig?.RootParentCode ?? null,
    } as Record<string, any>
    const res = await this.apiPost<ApiResponse<TreeItemDto>>(
      '/tree/add',
      requestData,
    )
    const newNode = this.dtoToNode(res.data, parentNode ?? undefined)

    // 通过 el-tree API 直接追加节点（不 reload，不重复）
    if (this._treeTableRef) {
      this._treeTableRef.appendNode(parentNode?.Code ?? null, newNode)
    } else {
      // fallback：无表格引用时直接操作 treeData
      if (parentNode) {
        parentNode.Children = parentNode.Children || []
        parentNode.Children.push(newNode)
        parentNode.IsLeaf = false
      } else {
        this.treeData.value.push(newNode)
      }
    }

    ElMessage.success('创建成功')
    return newNode
  }

  /** 修改树节点（/api/{controller}/tree/update） */
  async updateTreeNode(
    node: TreeNode,
    newName: string,
    extra?: Record<string, any>,
  ): Promise<void> {
    // extra 是 camelCase key，转 PascalCase
    const pascalExtra = extra ? pascalCaseFormData(extra) : {}
    const requestData = {
      [this.treeConfig?.CodeField ?? 'Code']: node.Code,
      [this.treeConfig?.NameField ?? 'Name']: newName,
      ...pascalExtra,
    }
    await this.apiPost<ApiResponse<TreeItemDto>>('/tree/update', requestData)

    node.Name = newName

    ElMessage.success('修改成功')
  }

  /** 删除树节点（/api/{controller}/tree/delete）
   * @param skipConfirm 跳过二次确认（由调用方自行处理确认弹窗）
   */
  async deleteTreeNode(node: TreeNode, skipConfirm = false): Promise<void> {
    // 前端预检：非级联删除模式下，本地有子节点则直接拦截（减少无效请求）
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

    // 调用后端 API（后端会校验：有子机构/有用户等场景，拒绝删除并返回错误信息）
    const res = await this.apiPost<ApiResponse<string>>('/tree/delete', [node.Code])
    if (!res.success) {
      ElMessage.error(res.message || '删除失败')
      return
    }

    this.removeNodeFromTree(node.Code)

    if (this.selectedNode.value?.Code === node.Code) {
      this.selectedNode.value = null
      await this.loadPageWithoutTree()
    }

    ElMessage.success('删除成功')
  }

  /** 树节点执行自定义操作 */
  async executeTreeAction(
    methodName: string,
    node: TreeNode,
    extra?: Record<string, any>,
  ): Promise<any> {
    const pascalExtra = extra ? pascalCaseFormData(extra) : {}
    const res = await this.apiPost<ApiResponse<any>>(
      `/tree/action/${methodName}`,
      {
        [this.treeConfig?.CodeField ?? 'Code']: node.Code,
        ...pascalExtra,
      },
    )
    await this.loadTreeRoot()
    return res.data
  }

  /**
   * 切换树节点有效标志（0 ↔ 1）
   *
   * 自动更新 node.Extra[enableField]，业务页面无需手动同步状态。
   *
   * @param node 树节点
   * @returns 新的值，失败返回 null
   */
  async toggleTreeNodeIsValid(node: TreeNode): Promise<{ Code: string; IsValid: number } | null> {
    const field = this.enableField ?? 'IsValid'
    const res = await this.apiPost<ApiResponse<{ Code: string; IsValid: number }>>(
      '/tree/toggle-valid',
      { [this.treeConfig?.CodeField ?? 'Code']: node.Code },
    )
    if (res.success) {
      // 自动更新 node.Extra 中的启用字段
      const extra = (node.Extra as any) || {}
      extra[field] = res.data.IsValid
      node.Extra = { ...extra }
      ElMessage.success(res.data.IsValid === 1 ? '已启用' : '已禁用')
      return res.data
    }
    return null
  }

  /**
   * 切换树节点有效标志（完整流程：确认弹窗 → API → 本地更新）
   *
   * 业务页面可直接调用，无需自行实现确认弹窗。
   * 基类统一处理：读取当前状态 → 弹窗确认 → 调用 API → 更新 node.Extra。
   *
   * @param node 树节点
   * @param options.entityName 确认弹窗中显示的实体名称，默认读 node.Name
   */
  async toggleTreeNodeWithConfirm(
    node: TreeNode,
    options?: { entityName?: string },
  ): Promise<void> {
    const field = this.enableField ?? 'IsValid'
    const extra = (node.Extra as any) || {}
    const currentVal = extra[field] ?? 1
    const action = currentVal === 1 ? '禁用' : '启用'
    const name = options?.entityName ?? node.Name

    await ElMessageBox.confirm(
      `确定${action}【${name}】？`,
      `${action}确认`,
      {
        type: 'warning',
        confirmButtonText: `确定${action}`,
        cancelButtonText: '取消',
      },
    )

    await this.toggleTreeNodeIsValid(node)
  }

  // ========================================================
  // 页面初始化
  // ========================================================

  /** 初始化页面：配置 → 树 → 表格 */
  async init(): Promise<void> {
    await this.loadConfig()
    await this.loadTreeRoot()
    await this.loadPageWithoutTree()
  }

  // ========================================================
  // DTO → TreeNode 映射
  // ========================================================

  /**
   * TreeItemDto → TreeNode 映射
   * 后端 TreeItemDto 是 PascalCase，前端 TreeNode 也统一使用 PascalCase
   */
  protected dtoToNode(dto: TreeItemDto, parent?: TreeNode): TreeNode {
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
  // 树节点 Split 方法
  // ========================================================

  protected removeNodeFromTree(code: string): void {
    const removeFromArray = (nodes: TreeNode[]): TreeNode[] => {
      return nodes.filter((n) => {
        if (n.Code === code) return false
        if (n.Children && n.Children.length > 0) {
          n.Children = removeFromArray(n.Children)
        }
        return true
      })
    }
    this.treeData.value = removeFromArray(this.treeData.value)
  }

  protected findNode(code: string): TreeNode | null {
    const findInArray = (nodes: TreeNode[]): TreeNode | null => {
      for (const n of nodes) {
        if (n.Code === code) return n
        if (n.Children && n.Children.length > 0) {
          const found = findInArray(n.Children)
          if (found) return found
        }
      }
      return null
    }
    return findInArray(this.treeData.value)
  }

  protected replaceTreeNode(code: string, newNode: TreeNode): void {
    const replaceInArray = (nodes: TreeNode[]): boolean => {
      for (let i = 0; i < nodes.length; i++) {
        if (nodes[i].Code === code) {
          nodes[i] = newNode
          return true
        }
        if (nodes[i].Children && nodes[i].Children.length > 0) {
          if (replaceInArray(nodes[i].Children)) return true
        }
      }
      return false
    }
    replaceInArray(this.treeData.value)
  }

  async refreshChildren(node: TreeNode): Promise<void> {
    // 直接从 API 加载子节点，不经过 loadChildren（避免 el-tree node/TreeNode 类型差异）
    const res = await this.apiPost<ApiResponse<TreeItemDto[]>>(
      '/tree/children',
      {
        ParentCode: node.Code,
        Level: (node.Extra?.level as number) ?? 0,
      },
    )
    const items = res.data ?? []
    const children = items.map((dto) => this.dtoToNode(dto, node))
    // 直接赋值触发 Vue 响应式 → el-tree 重新渲染
    node.Children = children
    node.IsLeaf = children.length === 0
  }

  async refreshTree(): Promise<void> {
    await this.loadTreeRoot()
  }

  /**
   * 刷新右侧表格（保持当前选中节点）
   *
   * 子类如有特殊过滤逻辑（如 ShowDisabled），可覆盖此方法。
   */
  async refreshTable(): Promise<void> {
    if (this.selectedNode.value) {
      await this.loadPageWithTree(this.selectedNode.value.Code)
    } else {
      await this.loadPageWithoutTree()
    }
  }

  // ========================================================
  // 事件处理（树节点操作）
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

// 工具函数重新导出（方便业务 Logic 直接使用）
export { toCamelCase, toPascalCase, pascalCaseFormData, rowToFormData }

export default TreeTableLogic
