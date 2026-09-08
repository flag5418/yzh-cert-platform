/**
 * TreeTableLogic - 左树右表 Logic 基类（V1.1）
 *
 * 设计理念：
 * - 继承 CrudPageLogic<V> 获得全部单表 CRUD 能力
 * - 叠加树能力：加载根节点 / 懒加载子节点 / 树→表格联动
 * - 配置驱动：根据 TreeTableConfig 动态渲染 UI
 * - Split 数据方法实现树节点增量更新
 *
 * 使用方式：
 *   // index.ts
 *   export class DeptPageLogic extends TreeTableLogic<SysDepartment> {
 *     controllerName = 'Department'
 *
 *     // 覆盖钩子注入业务逻辑
 *     protected dtoToNode(dto: TreeItemDto, parent?: TreeNode): TreeNode {
 *       return {
 *         ...super.dtoToNode(dto, parent),
 *         // 自定义映射
 *       }
 *     }
 *   }
 *
 *   // index.vue
 *   const page = new DeptPageLogic()
 *   await page.init()
 */

import { ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import type {
  ApiResponse,
  PagedResult,
  FilterRequest,
  FilterItem,
  TreeItemDto,
  TreeBehaviorConfig,
  TreeTableConfigDto,
  EntityConfigDto,
  ColumnConfig
} from '../types/contracts'
import type { TreeNode } from '../types/tree'
import type { YzhFormField } from '@yzh-core/components/form'
import { CrudPageLogic } from './CrudPageLogic'

// ========================================================
// 工具函数
// ========================================================

/** PascalCase to camelCase */
function toCamelCase(str: string): string {
  if (!str) return str
  return str.charAt(0).toLowerCase() + str.slice(1)
}

/** camelCase → snake_case */
function toSnakeCase(str: string): string {
  return str.replace(/([A-Z])/g, '_$1').toLowerCase()
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

// ========================================================
// 主类
// ========================================================

export abstract class TreeTableLogic<V extends Record<string, any>> extends CrudPageLogic<V> {
  // ──── 树状态 ────

  /** 树数据 */
  treeData = ref<TreeNode[]>([])

  /** 树加载状态 */
  treeLoading = ref(false)

  /** 当前选中节点 */
  selectedNode = ref<TreeNode | null>(null)

  /** 完整树表配置 */
  treeTableConfig = ref<TreeTableConfigDto | null>(null)

  // ──── 树节点表单弹窗状态 ────

  /** 树节点弹窗可见性 */
  treeDialogVisible = ref(false)

  /** 树节点弹窗模式 */
  treeDialogMode = ref<'add' | 'edit'>('add')

  /** 树节点提交中状态 */
  treeSubmitting = ref(false)

  /** 树节点表单数据 */
  treeFormData = reactive<Record<string, any>>({})

  /** 当前新增节点的父节点（add 模式） */
  treeParentNode = ref<TreeNode | null>(null)

  /** 当前编辑的节点（edit 模式） */
  treeEditingNode = ref<TreeNode | null>(null)

  // ──── 树配置快捷访问 ────

  /** 树行为配置 */
  protected get treeConfig(): TreeBehaviorConfig | null {
    return this.treeTableConfig.value?.treeConfig ?? null
  }

  /** 树节点表单配置（从后端 TreeTableConfig.TreeFormConfig 获取） */
  protected get treeFormConfig(): EntityConfigDto | null {
    return this.treeTableConfig.value?.treeFormConfig ?? null
  }

  /** 未选中树节点时的表格行为 */
  protected get noSelectionBehavior(): 'empty' | 'all' {
    return this.treeConfig?.noSelectionBehavior ?? 'empty'
  }

  /** 关联字段名（camelCase） */
  protected get relateField(): string {
    return toCamelCase(this.treeConfig?.relateField ?? 'parentCode')
  }

  /** 树节点表单字段配置（从 treeFormConfig 派生，bcFlag=true 表示参与编辑） */
  get treeFormFields(): YzhFormField[] {
    const cols = this.treeFormConfig?.columns
    const schema = this.treeFormConfig?.schema
    if (!cols) return []
    const colSpan = 12 // 树表单默认 2 列
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
          span: c.colSpan > 1 ? 24 : colSpan,
          options: undefined,
          placeholder: c.type?.includes('Picker') ? `请选择${c.desName}` : `请输入${c.desName}`,
          defaultValue: c.mrz ? (c.type === 'Switch' ? Number(c.mrz) : c.mrz) : fieldSchema?.default,
          fieldSchema
        }
      })
  }

  // ========================================================
  // 配置加载（覆盖：获取 TreeTableConfig）
  // ========================================================

  /** 加载页面配置（覆盖：获取 TreeTableConfig） */
  protected async loadConfig(): Promise<void> {
    const res = await this.apiGet<ApiResponse<TreeTableConfigDto>>('/config')
    this.treeTableConfig.value = res.data
    // 同时将 tableConfig 赋值给 config，获得单表能力
    this.config.value = res.data.tableConfig
  }

  // ========================================================
  // 树加载
  // ========================================================

  /** 加载根节点（/api/{controller}/tree/root） */
  async loadTreeRoot(): Promise<void> {
    this.treeLoading.value = true
    try {
      const res = await this.apiPost<ApiResponse<TreeItemDto[]>>('/tree/root', {})
      this.treeData.value = res.data.map((dto) => this.dtoToNode(dto))
    } finally {
      this.treeLoading.value = false
    }
  }

  /** 懒加载子节点（/api/{controller}/tree/children） */
  async loadChildren(node: TreeNode): Promise<TreeNode[]> {
    const res = await this.apiPost<ApiResponse<TreeItemDto[]>>('/tree/children', {
      parentCode: node.code,
      level: (node.extra?.level as number) ?? 0
    })
    const children = res.data.map((dto) => this.dtoToNode(dto, node))
    // 更新节点的子节点（增量更新）
    node.children = children
    return children
  }

  // ========================================================
  // 树→表格联动（核心方法）
  // ========================================================

  /** 节点点击 → 加载该节点下表格数据 */
  async onNodeClick(node: TreeNode): Promise<void> {
    this.selectedNode.value = node

    // 检查是否只能选择叶子节点
    if (this.treeConfig?.onlyLeafSelectable && !node.isLeaf) {
      return
    }

    await this.loadPageWithTree(node.code)
  }

  /** 带树条件的分页查询 */
  async loadPageWithTree(treeCode: string): Promise<void> {
    this.loading.value = true
    try {
      const filters: FilterItem[] = [
        ...this.buildFilters(),
        { field: toSnakeCase(this.relateField), value: treeCode, operator: 'eq' }
      ]
      const request: FilterRequest = {
        page: this.pagination.page,
        pageSize: this.pagination.pageSize,
        sortField: this.sortField.value,
        sortOrder: this.sortOrder.value,
        filters
      }
      const res = await this.apiPost<ApiResponse<PagedResult<V>>>('/filter', request)
      this.rows.value = res.data.items
      this.pagination.total = res.data.total
    } finally {
      this.loading.value = false
    }
  }

  /** 无树条件的表格加载（未选中树节点时） */
  async loadPageWithoutTree(): Promise<void> {
    if (this.noSelectionBehavior === 'empty') {
      this.rows.value = []
      this.pagination.total = 0
    } else {
      // 'all' → 加载全部数据
      await this.loadPage()
    }
  }

  // ========================================================
  // 树节点 CRUD（增量更新）
  // ========================================================

  /** 新增树节点（/api/{controller}/tree/add） */
  async addTreeNode(parentNode: TreeNode | null, data: Record<string, any>): Promise<TreeNode | null> {
    const requestData = {
      ...data,
      [toSnakeCase(this.treeConfig?.parentCodeField ?? 'parentCode')]:
        parentNode?.code ?? this.treeConfig?.rootParentCode ?? null
    } as Record<string, any>
    const res = await this.apiPost<ApiResponse<TreeItemDto>>('/tree/add', requestData)
    const newNode = this.dtoToNode(res.data, parentNode ?? undefined)

    // 增量更新：添加到树
    if (parentNode) {
      parentNode.children = parentNode.children || []
      parentNode.children.push(newNode)
      parentNode.isLeaf = false
    } else {
      this.treeData.value.push(newNode)
    }

    ElMessage.success('创建成功')
    return newNode
  }

  /** 修改树节点（/api/{controller}/tree/update） */
  async updateTreeNode(
    node: TreeNode,
    newName: string,
    extra?: Record<string, any>
  ): Promise<void> {
    const requestData = {
      [toSnakeCase(this.treeConfig?.codeField ?? 'code')]: node.code,
      [toSnakeCase(this.treeConfig?.nameField ?? 'name')]: newName,
      ...extra
    }
    await this.apiPost<ApiResponse<TreeItemDto>>('/tree/update', requestData)

    // 增量更新：修改节点名称
    node.name = newName

    ElMessage.success('修改成功')
  }

  /** 删除树节点（/api/{controller}/tree/delete） */
  async deleteTreeNode(node: TreeNode): Promise<void> {
    // 检查是否允许删除含子节点的父节点
    if (!this.treeConfig?.allowDeleteWithChildren && node.children && node.children.length > 0) {
      ElMessage.warning('该节点包含子节点，无法删除')
      return
    }

    await ElMessageBox.confirm(
      `确定删除节点 "${node.name}"？`,
      '删除确认',
      { type: 'warning', confirmButtonText: '确定', cancelButtonText: '取消' }
    )

    await this.apiPost<ApiResponse<string>>('/tree/delete', [node.code])

    // 增量更新：从树中移除节点
    this.removeNodeFromTree(node.code)

    // 如果删除的是当前选中节点
    if (this.selectedNode.value?.code === node.code) {
      this.selectedNode.value = null
      await this.loadPageWithoutTree()
    }

    ElMessage.success('删除成功')
  }

  /** 树节点执行自定义操作（/api/{controller}/tree/action/{methodName}） */
  async executeTreeAction(methodName: string, node: TreeNode, extra?: Record<string, any>): Promise<any> {
    const res = await this.apiPost<ApiResponse<any>>(`/tree/action/${methodName}`, {
      [toSnakeCase(this.treeConfig?.codeField ?? 'code')]: node.code,
      ...extra
    })
    // 操作后刷新树
    await this.loadTreeRoot()
    return res.data
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
  // DTO → TreeNode 映射（子类可覆盖）
  // ========================================================

  /**
   * TreeItemDto → TreeNode 映射
   * 子类可覆盖此方法实现自定义映射
   */
  protected dtoToNode(dto: TreeItemDto, parent?: TreeNode): TreeNode {
    const level = (parent?.extra?.level as number ?? -1) + 1
    return {
      code: dto.code,
      name: dto.name,
      parentCode: dto.parentCode,
      nodeType: dto.nodeType,
      isLeaf: dto.isLeaf,
      extra: { ...dto.extra, level },
      children: []
    }
  }

  // ========================================================
  // 树节点 Split 方法（增量更新，不重新加载整棵树）
  // ========================================================

  /**
   * 从树中移除节点（递归查找）
   */
  protected removeNodeFromTree(code: string): void {
    const removeFromArray = (nodes: TreeNode[]): TreeNode[] => {
      return nodes.filter((n) => {
        if (n.code === code) return false
        if (n.children && n.children.length > 0) {
          n.children = removeFromArray(n.children)
        }
        return true
      })
    }
    this.treeData.value = removeFromArray(this.treeData.value)
  }

  /**
   * 在树中查找节点（递归查找）
   */
  protected findNode(code: string): TreeNode | null {
    const findInArray = (nodes: TreeNode[]): TreeNode | null => {
      for (const n of nodes) {
        if (n.code === code) return n
        if (n.children && n.children.length > 0) {
          const found = findInArray(n.children)
          if (found) return found
        }
      }
      return null
    }
    return findInArray(this.treeData.value)
  }

  /**
   * 替换树中的节点
   */
  protected replaceTreeNode(code: string, newNode: TreeNode): void {
    const replaceInArray = (nodes: TreeNode[]): boolean => {
      for (let i = 0; i < nodes.length; i++) {
        if (nodes[i].code === code) {
          nodes[i] = newNode
          return true
        }
        if (nodes[i].children && nodes[i].children.length > 0) {
          if (replaceInArray(nodes[i].children)) return true
        }
      }
      return false
    }
    replaceInArray(this.treeData.value)
  }

  /**
   * 刷新指定节点的子节点（懒加载场景）
   */
  async refreshChildren(node: TreeNode): Promise<void> {
    const children = await this.loadChildren(node)
    node.children = children
    node.isLeaf = children.length === 0
  }

  /**
   * 刷新整棵树
   */
  async refreshTree(): Promise<void> {
    await this.loadTreeRoot()
  }

  // ========================================================
  // 事件处理（树节点操作）
  // ========================================================

  /** 树节点勾选变更 */
  onCheckChange(_node: TreeNode, _checked: boolean): void {
    // 由子类或组件处理勾选逻辑
  }

  /** 添加根节点 */
  async addRootNode(data: Record<string, any>): Promise<TreeNode | null> {
    return this.addTreeNode(null, data)
  }

  /** 添加子节点 */
  async addChildNode(parentNode: TreeNode, data: Record<string, any>): Promise<TreeNode | null> {
    return this.addTreeNode(parentNode, data)
  }

  /** 重命名节点 */
  async renameNode(node: TreeNode, newName: string): Promise<void> {
    await this.updateTreeNode(node, newName)
  }
}

export default TreeTableLogic
