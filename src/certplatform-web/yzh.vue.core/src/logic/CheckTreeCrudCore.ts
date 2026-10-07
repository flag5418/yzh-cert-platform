/**
 * CheckTreeCrudCore - 勾选授权内核（AS-2）+ 树节点增删改（TT-5 子集）
 *
 * 为什么存在：
 * `/system/role-user`（以及后续 role-menu / role-api）都是「左树 + 右侧勾选树」，
 * 走 `CheckTreeCore` 系；但角色本身需要能增删改。它们既不能换成 `TreeTableCore`
 * （右侧不是分页表格，且依赖 SingleTableCore 的 controllerName/config 一整套），
 * 也不能在页面里手写 CRUD（铁律：禁止手写 handleAdd/handleSubmit）。本内核把
 * `TreeTableCore` 的**树侧**能力抽成 `CheckTreeCore` 的扩展。
 *
 * 端点仍由页面 api 模块注入（沿用 `AssociationTreeCore`「内核不拼端点」约定）：
 * `CheckTreeCrudApi` = `AssociationApi` + treepconfig / tree/add / update / delete。
 *
 * 能力：
 * - 配置驱动：`/treepconfig` → `treeTableConfig`
 *   · `TreeConfig` → 节点动作（经 `toTreeActions` 纯函数，前端零硬编码按钮）
 *   · `TreeFormConfig` → 弹窗字段（经 `toFormFields` 纯函数）
 * - 泛型流：`openTreeNodeDialog` / `submitTreeNodeForm` / `deleteTreeNodeWithConfirm`
 * - 派发：`onNodeAction`（add-child / add-root / edit / delete / custom:*）
 * - 本地增量：加节点走 `YzhTree.appendNode`、改走原位 `Object.assign`、
 *   删走 `YzhTree.removeNode` —— **不整树 reload**，保留展开/懒加载态
 *
 * 铁律：
 * - F-1 判定唯一 / F-2 失败必抛 / F-3 谁 catch 谁弹 / F-4 取消静默
 * - 准则 A：定位、删除、更新只用 `Code`，提交体永不带 `Id`
 */

import { reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { toFormFields, toFormLayoutCols, toTreeActions } from '../adapters/entityAdapters'
import type { YzhFormField } from '../components/form'
import type { YzhAction } from '../components/table/types'
import type {
  EntityConfigDto,
  TreeBehaviorConfig,
  TreeItemDto,
  TreeTableConfigDto,
} from '../types/contracts'
import type { TreeNode } from '../types/tree'
import { pascalCaseFormData } from '../utils/case'
import { confirmOrFalse } from '../utils/confirm'
import { CheckTreeCore, type AssociationApi } from './CheckTreeCore'

// ========================================================
// API 注入约定
// ========================================================

/**
 * 树 CRUD 端点注入（页面 api 模块实现）。
 * 之所以是**接口扩展**而非直接写进 `AssociationApi`：
 * role-menu / role-api 目前仍是纯勾选（用 `CheckTreeCore`），不受影响。
 */
export interface CheckTreeCrudApi extends AssociationApi {
  /** `GET /api/{controller}/treepconfig` → TreeTableConfigDto（TreeConfig + TreeFormConfig） */
  getTreePageConfig: () => Promise<TreeTableConfigDto>
  /** `POST /api/{controller}/tree/add` → 新节点 DTO（失败抛 BizError） */
  addTreeNode: (body: Record<string, any>) => Promise<TreeItemDto | null>
  /** `POST /api/{controller}/tree/update` → 修改后 DTO（按 Code 定位，失败抛 BizError） */
  updateTreeNode: (body: Record<string, any>) => Promise<TreeItemDto | null>
  /** `POST /api/{controller}/tree/delete`，body = Code[]（失败抛 BizError） */
  deleteTreeNodes: (codes: string[]) => Promise<void>
}

// ========================================================
// 内核
// ========================================================

export abstract class CheckTreeCrudCore extends CheckTreeCore {
  /** 树 CRUD 端点（与 `api` 同源，但收窄到 CheckTreeCrudApi） */
  protected readonly crudApi: CheckTreeCrudApi

  // ──── 配置（/treepconfig） ────

  /** 完整树页配置（PascalCase，YZH.Core.Stand/TreeTableConfigDto） */
  treeTableConfig = ref<TreeTableConfigDto | null>(null)

  // ──── 树节点表单弹窗状态（与 YzhFormDialog 直绑） ────

  treeDialogVisible = ref(false)
  treeDialogMode = ref<'add' | 'edit'>('add')
  treeSubmitting = ref(false)
  /** 表单数据 key = `treeFormFields[].prop`（PascalCase，与后端实体属性一致） */
  treeFormData = reactive<Record<string, any>>({})
  /** 新增时的父节点 */
  treeParentNode = ref<TreeNode | null>(null)
  /** 编辑中的节点 */
  treeEditingNode = ref<TreeNode | null>(null)

  // ──── 树组件引用（appendNode / removeNode 需要） ────

  private _treeRef: any = null

  constructor(api: CheckTreeCrudApi) {
    super(api)
    this.crudApi = api
  }

  /** 模板 onMounted 注入（`useCheckTree` 不注入 ref，由页面自己调） */
  setTreeRef(ref: any) {
    this._treeRef = ref
  }

  // ========================================================
  // 配置
  // ========================================================

  override async init(): Promise<void> {
    try {
      // 先配置后树：节点动作/弹窗字段依赖它
      this.treeTableConfig.value = await this.crudApi.getTreePageConfig()
      await super.init()
    } catch (e) {
      // 读路径最外层兜底（F-3）：失败保留现状 + 只弹一次
      this.reportError(e, '页面初始化失败')
    }
  }

  /** 树行为配置（驱动节点动作） */
  protected get treeConfig(): TreeBehaviorConfig | null {
    return this.treeTableConfig.value?.TreeConfig ?? null
  }

  /** 树节点表单配置（驱动弹窗字段；后端未设 TreeFormConfigName 时为 null） */
  protected get treeFormConfig(): EntityConfigDto | null {
    return this.treeTableConfig.value?.TreeFormConfig ?? null
  }

  protected get treeCodeField(): string {
    return this.treeConfig?.CodeField ?? 'Code'
  }

  protected get treeNameField(): string {
    return this.treeConfig?.NameField ?? 'Name'
  }

  protected get treeParentCodeField(): string {
    return this.treeConfig?.ParentCodeField ?? 'ParentCode'
  }

  /**
   * 树节点状态字段名（供 `YzhTreeTableLayout` 的 `:status-field` 绑定，渲染启用/停用徽章）。
   *
   * <para>取后端 `TreeConfig.EnableField`（与节点「禁用/启用」动作同一字段，保证徽章与
   * 动作判定永远一致；后端 `TreeMapper` 已把该字段写进每个节点的 `Extra`）。
   * 后端未声明 → 返回 `undefined` → 不渲染徽章，不影响页面。</para>
   * <para>与 `TreeTableCore.treeStatusField` 同语义（本内核不读 TableConfig，故无第二级回落）。</para>
   */
  get treeStatusField(): string | undefined {
    return this.treeConfig?.EnableField
  }

  /** 「是否允许新增下级」节点动作（扁平树由后端配置 false） */
  protected get allowAddChild(): boolean {
    return this.treeConfig?.AllowAddChild ?? true
  }

  /** 根级新增是否要求先选中节点（角色可多根 → 子类关掉） */
  protected get requireTreeSelectionForAdd(): boolean {
    return true
  }

  protected get requireTreeSelectionMessage(): string {
    return '请先在左侧选择角色'
  }

  /** 新增默认值（合并在 TreeFormConfig.NewEntity 之后） */
  protected get defaultTreeValues(): Record<string, any> {
    return {}
  }

  /** 弹窗「名称」字段名（后端 TreeConfig.NameField，如 RoleName） */
  protected get treeEntityNameField(): string {
    return this.treeConfig?.NameField ?? 'Name'
  }

  /** 树节点表单布局列数 */
  get treeFormLayoutCols(): number {
    return toFormLayoutCols(this.treeFormConfig)
  }

  /** 树节点表单字段（配置驱动，零硬编码） */
  get treeFormFields(): YzhFormField[] {
    return toFormFields(this.treeFormConfig, '0')
  }

  /** 弹窗「上级角色」展示名（仅新增态展示；编辑态展示角色编码，避免显示未知父级） */
  get treeParentName(): string {
    return this.treeParentNode.value?.Name ?? '根级'
  }

  /**
   * 节点动作（`YzhTree` 的 `:node-actions`，逐节点调用）
   * 全部由后端 `TreeConfig` 驱动（`AllowToggle=false` 时只有 新增下级/编辑/删除）。
   */
  get nodeActions(): (node: TreeNode) => YzhAction[] {
    return (node: TreeNode) => this.resolveTreeActions(node)
  }

  protected resolveTreeActions(node: TreeNode): YzhAction[] {
    return toTreeActions(this.treeConfig, this.treeConfig?.EnableField, {
      allowAddChild: this.allowAddChild,
      node: node as Record<string, any>,
    })
  }

  // ========================================================
  // 树节点 CRUD 泛型流
  // ========================================================

  /**
   * 打开树节点弹窗
   * @param node 编辑目标（null = 新增）
   * @param parent 新增时的父节点（缺省取当前选中节点）
   */
  openTreeNodeDialog(node: TreeNode | null = null, parent: TreeNode | null = null): boolean {
    if (node) {
      this.treeDialogMode.value = 'edit'
      this.treeEditingNode.value = node
      this.treeParentNode.value = null
      this.resetObject(this.treeFormData)

      const tmpl = (this.treeFormConfig?.NewEntity as Record<string, any>) || {}
      const extra = (node.Extra as Record<string, any>) || {}
      // 按声明的表单字段白名单从 Extra 取回填值（IsValid/OrderNo 都在 Extra 里）
      const picked: Record<string, any> = {}
      for (const f of this.treeFormFields) {
        if (f.prop in extra) picked[f.prop as string] = extra[f.prop as string]
      }
      Object.assign(this.treeFormData, tmpl, picked, {
        [this.treeCodeField]: node.Code,
        [this.treeParentCodeField]: node.ParentCode ?? null,
        [this.treeEntityNameField]: node.Name,
      })
      this.treeDialogVisible.value = true
      return true
    }

    // 新增
    const targetParent = parent ?? this.selectedNode.value
    if (this.requireTreeSelectionForAdd && !targetParent) {
      ElMessage.warning(this.requireTreeSelectionMessage)
      return false
    }
    this.treeDialogMode.value = 'add'
    this.treeEditingNode.value = null
    this.treeParentNode.value = targetParent
    this.resetObject(this.treeFormData)
    const tmpl = (this.treeFormConfig?.NewEntity as Record<string, any>) || {}
    Object.assign(this.treeFormData, tmpl, this.defaultTreeValues, {
      [this.treeEntityNameField]: '',
      [this.treeParentCodeField]: targetParent?.Code ?? this.treeConfig?.RootParentCode ?? null,
    })
    this.treeDialogVisible.value = true
    return true
  }

  /**
   * 提交树节点表单。
   * F-1：只有 `expectOk`（api 内已抛）通过才关弹窗、提示、重置编辑态；
   * 失败 → 弹窗保持打开且 `treeDialogMode` 不重置（否则下次提交会被当新增）。
   *
   * @returns true = 保存成功
   */
  async submitTreeNodeForm(): Promise<boolean> {
    this.treeSubmitting.value = true
    try {
      const payload = pascalCaseFormData({ ...this.treeFormData })
      const isAdd = this.treeDialogMode.value === 'add'
      if (isAdd) {
        this.onBeforeAddTree(payload, this.treeParentNode.value)
        const created = await this.addTreeNode(this.treeParentNode.value, payload)
        if (created) this.onAfterAddTree(created, this.treeParentNode.value)
      } else {
        const node = this.treeEditingNode.value!
        this.onBeforeUpdateTree(node, payload)
        await this.updateTreeNode(node, payload[this.treeEntityNameField] ?? '', payload)
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

  /** 新增树节点（本地增量，不 reload） */
  async addTreeNode(
    parent: TreeNode | null,
    data: Record<string, any>,
  ): Promise<TreeNode | null> {
    const requestData: Record<string, any> = {
      ...pascalCaseFormData(data),
      [this.treeParentCodeField]: parent?.Code ?? this.treeConfig?.RootParentCode ?? null,
    }
    // 准则 A：提交体不带 Id（编辑靠 Code 定位）
    delete requestData['Id']

    const dto = await this.crudApi.addTreeNode(requestData)
    const newNode = this.dtoToNode(
      dto ?? {
        Code: String(requestData[this.treeCodeField] ?? ''),
        Name: String(requestData[this.treeNameField] ?? ''),
        ParentCode: (requestData[this.treeParentCodeField] as string) ?? null,
        IsLeaf: false,
        Level: 0,
      },
      parent,
    )
    // 新节点必无子级 → 一律末端。后端未回填 IsLeaf 时 dto 会给 false，
    // 会让该节点被误判为「非末端」而无法再做末端相关操作。
    newNode.IsLeaf = true

    if (this._treeRef?.appendNode) {
      this._treeRef.appendNode(parent?.Code ?? null, newNode)
    } else {
      this.insertIntoTreeData(parent?.Code ?? null, newNode)
    }
    // ⚠️ 父节点从「末端」变「非末端」：不改会让 el-tree 继续按叶子渲染 ⇒ 刚加的子节点看不见
    if (parent) {
      const liveParent = this.findNodeByCode(this.treeData.value, parent.Code)
      if (liveParent) liveParent.IsLeaf = false
    }
    return newNode
  }

  /** 修改树节点（原位 `Object.assign`，保留 el-tree 节点对象引用） */
  async updateTreeNode(
    node: TreeNode,
    newName: string,
    extra?: Record<string, any>,
  ): Promise<void> {
    const requestData: Record<string, any> = pascalCaseFormData({ ...(extra ?? {}) })
    // 准则 A：定位只用 Code；提交体不带 Id（Id 恒为 0 会被误判）
    delete requestData['Id']
    requestData[this.treeCodeField] = node.Code
    requestData[this.treeNameField] = newName

    const dto = await this.crudApi.updateTreeNode(requestData)

    const picked: Record<string, any> = {}
    for (const f of this.treeFormFields) {
      if (f.prop in requestData) picked[f.prop as string] = requestData[f.prop as string]
    }

    const live = this.findNodeByCode(this.treeData.value, node.Code) ?? node
    const extraBefore = { ...((live.Extra as Record<string, any>) || {}) }
    // ⚠️ 必须原位赋值：整体换新对象会让 el-tree 残留旧节点，且会吞掉子节点引用。
    // ⚠️ 节点显示字段恒为 `Name`（TreeNode/TreeItemDto 契约）；
    //    `treeNameField`（= TreeConfig.NameField，如 RoleName）只用于**提交体**，
    //    写到节点上会让树上名字永远不更新（改名无效且无报错）。
    Object.assign(live, {
      Name: dto?.Name || newName,
      [this.treeParentCodeField]:
        (requestData[this.treeParentCodeField] as string | null | undefined) ??
        node.ParentCode ??
        null,
      Extra: { ...extraBefore, ...picked },
    })
  }

  /**
   * 删除树节点（确认 → API → 本地摘除）
   * @returns true = 已删除；false = 用户取消 / 前端预检拦截
   */
  async deleteTreeNode(node: TreeNode, skipConfirm = false): Promise<boolean> {
    if (!this.treeConfig?.AllowDeleteWithChildren && node.Children?.length) {
      ElMessage.warning('该角色下存在子角色，请先删除子角色')
      return false
    }
    if (!skipConfirm) {
      // F-4：取消静默
      const ok = await confirmOrFalse(`确定删除【${node.Name}】？`, '删除确认', {
        confirmButtonText: '确定删除',
        cancelButtonText: '取消',
      })
      if (!ok) return false
    }

    // F-1：失败在此抛出 → 不摘节点、不弹成功
    await this.crudApi.deleteTreeNodes([node.Code])

    if (this._treeRef?.removeNode) {
      this._treeRef.removeNode(null, node.Code)
    }
    this.removeFromTreeData(node.Code)

    if (this.selectedNode.value?.Code === node.Code) {
      this.selectedNode.value = null
      this.associationData.value = []
    }
    return true
  }

  /** 删除（带确认 + 统一错误出口） */
  async deleteTreeNodeWithConfirm(node: TreeNode): Promise<boolean> {
    try {
      const deleted = await this.deleteTreeNode(node)
      if (deleted) ElMessage.success('已删除')
      return deleted
    } catch (e) {
      this.reportError(e, '删除失败')
      return false
    }
  }

  // ========================================================
  // 动作派发
  // ========================================================

  /** 节点动作入口（绑定 `@node-action="logic.onNodeAction"`；箭头属性自动绑定 this） */
  onNodeAction = async (key: string, node: TreeNode): Promise<void> => {
    try {
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
        default:
          // `custom:*`（后端 RegisterTreeAction 注册的 /tree/action/{method}）
          // 本内核暂不支持：RoleController 未注册任何树动作，`TreeConfig.CustomActions`
          // 为 null ⇒ `toTreeActions` 不会产出该 key。需要时子类覆盖本方法。
          return
      }
    } catch (e) {
      // 顶层兜底（F-3）：失败只弹一次，不刷新、不弹成功
      this.reportError(e, '操作失败')
    }
  }

  // ========================================================
  // 生命周期钩子（子类可覆盖）
  // ========================================================

  protected onBeforeAddTree(_data: Record<string, any>, _parent: TreeNode | null) {}
  protected onAfterAddTree(_node: TreeNode, _parent: TreeNode | null) {}
  protected onBeforeUpdateTree(_node: TreeNode, _data: Record<string, any>) {}
  protected onAfterUpdateTree(_node: TreeNode, _data: Record<string, any>) {}

  // ========================================================
  // 本地树增量（YzhTree 不可用时的兜底）
  // ========================================================

  protected dtoToNode(dto: TreeItemDto, parent?: TreeNode | null): TreeNode {
    const level = ((parent?.Extra?.level as number) ?? -1) + 1
    return {
      Code: dto.Code,
      Name: dto.Name,
      // 后端未回传 ParentCode 时以本地插入位置为准（否则节点在树里错挂到根）
      ParentCode: dto.ParentCode ?? parent?.Code ?? null,
      NodeType: dto.NodeType,
      IsLeaf: dto.IsLeaf,
      Extra: { ...dto.Extra, level },
      Children: [],
    }
  }

  /** 递归按 Code 摘除（不依赖 YzhTree；找不到静默返回） */
  protected removeFromTreeData(code: string): boolean {
    const walk = (nodes: TreeNode[]): boolean => {
      for (let i = 0; i < nodes.length; i++) {
        if (String(nodes[i].Code) === String(code)) {
          nodes.splice(i, 1)
          return true
        }
        if (nodes[i].Children?.length && walk(nodes[i].Children!)) return true
      }
      return false
    }
    return walk(this.treeData.value)
  }

  /** 递归插入（不依赖 YzhTree；根级直接 push） */
  protected insertIntoTreeData(parentCode: string | null, newNode: TreeNode): boolean {
    if (!parentCode) {
      this.treeData.value.push(newNode)
      return true
    }
    const parent = this.findNodeByCode(this.treeData.value, parentCode)
    if (!parent) return false
    if (!parent.Children) parent.Children = []
    parent.Children.push(newNode)
    parent.IsLeaf = false
    return true
  }

  // ========================================================
  // 工具
  // ========================================================

  /** 同 SingleTableCore.resetObject：清空 reactive 表单对象（保引用） */
  protected resetObject(obj: Record<string, any>): void {
    Object.keys(obj).forEach((k) => delete obj[k])
  }

  /** 统一错误出口（F-3：谁 catch 谁弹，只弹一次） */
  protected reportError(e: unknown, fallback: string): void {
    const err = e as { handled?: boolean; message?: string } | null | undefined
    if (err && typeof err === 'object' && err.handled) return
    if (err && typeof err === 'object') err.handled = true
    ElMessage.error(err?.message || fallback)
  }
}

export default CheckTreeCrudCore
