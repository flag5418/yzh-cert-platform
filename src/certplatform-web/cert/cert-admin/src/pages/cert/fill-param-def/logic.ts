/**
 * FillParamDefLogic — 企业资料参数（后台管理 · 左树右表）
 *
 * 后端：`FillParamDefController`（`YzhControllerBase<FillParamDef>`）@ `api/Admin/Cert/FillParamDef`
 * 表：`cert_fill_param_def`｜菜单 `MENU_00217`（2026-10-06 改名「企业资料参数」）｜路由 `/business/fill-param-def`
 *
 * ★ 数据模型绑定制（2026-10-09 更新）：
 *   左树 = **[通用] + 体系(系统) → 族 → 标准** 三层
 *       - 通用参数：真实根节点（StandardCode=''）→ 右表过滤「不限标准」的参数
 *       - 体系/系统：iso_category 字典 → virtual，只展开/折叠
 *       - 族：cert_standard_family → virtual，只展开/折叠
 *       - 标准：真实叶子 → 右表过滤 StandardCode=节点Code（ISO GUID）
 *   右表 = 选中节点下参数（走基类 `/filter` + RelateField='StandardCode' 自动注入）
 *   关键字段不分机构：`OrgCode` / `StageCode` 服务端强制恒空串
 *
 * ★ 覆写清单（每条都注明为什么样板没有）：
 *   - `controllerName`        —— 三层同构路由段
 *   - `loadConfig`            —— ⛔ 内核默认 `/treepconfig` 只存在于 `TreeTableControllerBase`，
 *                                本控制器是 `YzhControllerBase`（树数据源是另一实体，不适配
 *                                TreeTableControllerBase）⇒ 改走 `/config` + 手工拼 TreeConfig
 *   - `loadTreeRoot`          —— 树数据源 = 字典 + 族 + ISO 标准（跨控制器拼接）
 *   - `autoSelectFirstNode`   —— 自动选中第一个**可点击**节点（通用参数或首个标准），
 *                                而非第一个 virtual 体系节点
 *   - `isSelectableNode`      —— 区分 virtual/真实节点（virtual 不触发过滤）
 *   - `onPrepareAdd`          —— 新增行注入 `StandardCode` = 选中节点 Code（归属键）
 *   - `requireTreeSelectionMessage` —— 本页业务文案
 *   - `toolbarActions` + refresh handler —— 样板同款（内核默认工具栏无「刷新」）
 */
import {
  TreeTableCore,
  expectOk,
  yzhApi,
  type ApiResponse,
  type EntityConfigDto,
  type TreeItemDto,
  type TreeTableConfigDto,
  type TreeNode,
  type YzhAction,
} from '@yzh-core'
import { ElMessage } from 'element-plus'
import { getIsoStandardTree } from '@share/api/cert/fill-param-def'
import { getCertStandardFamilyList } from '@share/api/cert/cert-standard-family'
import { buildSystemFamilyTree } from './system-family-tree'
import type { CategoryItem } from '../../foundation/iso-standard/group'

export class FillParamDefLogic extends TreeTableCore<any> {
  controllerName = 'Admin/Cert/FillParamDef'

  /** 删除确认显示参数名称 */
  protected override get entityNameField(): string {
    return 'ParamName'
  }

  /** 新增行默认值（服务端 OnBeforeAdd 会再次强制归一，此处只是表单初值） */
  protected override get defaultValues(): Record<string, any> {
    return { IsValid: 1, ValueType: 'text' }
  }

  /** 未选节点提示（内核默认「请先在左侧选择节点」） */
  protected override get requireTreeSelectionMessage(): string {
    return '请先在左侧选择「通用参数」或某个标准'
  }

  /**
   * 是否可点击（仅真实节点可触发过滤）：
   * - 通用参数（Code=''）⇒ true
   * - 体系/族（NodeType='virtual'）⇒ false
   * - 标准（NodeType 非 virtual）⇒ true
   */
  isSelectableNode(node: TreeNode | null | undefined): boolean {
    if (!node) return false
    // virtual 节点：只用于导航，不触发过滤
    if (node.NodeType === 'virtual') return false
    return true
  }

  /**
   * 关闭内核默认的 `treeData[0]` 自动选中（它会选到 virtual 体系节点，
   * 导致 selectedNode 被 virtual 占据、右表 empty）。
   * 改由 `onAfterInit` 深度优先找第一个**可点击**节点（通用参数）。
   */
  protected override get autoSelectFirstNode(): boolean {
    return false
  }

  /**
   * 树加载 + afterTreeLoaded 之后：自动选中第一个可点击节点（通用参数），
   * 触发右表加载该归属下的参数列表。
   *
   * <p>为什么不交给内核 `autoSelectFirstNode`：
   * 内核直接取 `treeData[0]`（第一个 virtual 体系节点），`onNodeClick` 后
   * `selectedNode` 被 virtual 占据 → `shouldApplyTreeFilter()=false` → right empty。</p>
   */
  protected override async onAfterInit(): Promise<void> {
    const first = this.findFirstSelectable(this.treeData)
    if (first) {
      this.treeSide.selectedNode.value = first
      await this.refreshTable()
    }
  }

  /** 深度优先找第一个可点击节点 */
  private findFirstSelectable(nodes: TreeNode[]): TreeNode | null {
    for (const n of nodes) {
      if (this.isSelectableNode(n)) return n
      if (n.Children?.length) {
        const child = this.findFirstSelectable(n.Children)
        if (child) return child
      }
    }
    return null
  }

  /**
   * 加载页面配置：改走 `/config` + 手工拼 `TreeTableConfigDto`。
   *
   * <p>TreeConfig 全关（左树只读：体系/族/标准均不可编辑）：AllowEdit/AllowDelete/
   * AllowAddChild/AllowToggle=false ⇒ `resolveTreeActions` 返回空数组。
   * RelateField='StandardCode'（右表过滤字段）；NoSelectionBehavior='empty'。</p>
   */
  override async loadConfig(): Promise<void> {
    const res = await this.apiGet<ApiResponse<EntityConfigDto>>('/config')
    expectOk(res, '加载页面配置失败')
    this.config.value = res.data
    this.treeTableConfig.value = {
      TableConfig: res.data,
      TreeConfig: {
        Lazy: false,
        AllowEdit: false,
        AllowAddChild: false,
        AllowDelete: false,
        AllowRename: false,
        AllowToggle: false,
        NameField: 'Name',
        CodeField: 'Code',
        ParentCodeField: 'ParentCode',
        RelateField: 'StandardCode',
        NoSelectionBehavior: 'empty',
        MaxLevel: 3,
        AllowDeleteWithChildren: false,
      },
    } as TreeTableConfigDto
  }

  /**
   * 树数据源 = 字典 + 族 + ISO 标准三层拼接。
   *
   * 并行拉取三项数据，用 `buildSystemFamilyTree` 组装成 [通用] → 体系(virtual) → 族(virtual) → 标准。
   */
  override async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const [stdRes, famRes, dictRes] = await Promise.all([
        getIsoStandardTree().catch(() => [] as TreeItemDto[]),
        getCertStandardFamilyList().catch(
          () => ({ success: false, data: { Items: [] } }) as any,
        ),
        yzhApi
          .get<ApiResponse<CategoryItem[]>>(
            '/api/System/Dictionary/items/by-no/iso_category',
          )
          .catch(() => ({ success: false, data: [] }) as any),
      ])

      const standards = (stdRes ?? []).map((dto) => this.dtoToNode(dto))
      const families: any[] =
        (famRes as any)?.data?.Items ??
        (famRes as any)?.Items ??
        []
      const categories: CategoryItem[] = (dictRes as any)?.success
        ? (dictRes.data ?? [])
        : (dictRes as any)?.data ?? []

      const tree = buildSystemFamilyTree(standards, families, categories)
      this.treeSide.setNodes(tree)
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  /**
   * 覆盖新增入口：virtual 节点（体系/族）不允许新增参数。
   *
   * ＜p>内核默认 `openRowDialog` 对 virtual 跳过 `canAddUnderNode` 检查直接放行 ——
   * 若选中体系/族节点后点「新增参数」，`onPrepareAdd` 会把族 GUID 写入 StandardCode
   * ⇒ 参数挂到了实体中不存在的族上，且右表按 ISO GUID 过滤永不见影。</p>
   */
  override openRowDialog(row?: any | null): boolean {
    if (!row) {
      const node = this.selectedNode
      if (node && this.isVirtualNode(node)) {
        ElMessage.warning('请先在左侧选择「通用参数」或某个标准作为归属')
        return false
      }
    }
    return super.openRowDialog(row)
  }

  /** 新增行注入归属标准（Code 由后端生成；StandardCode = 选中节点 Code，通用=''） */
  protected override onPrepareAdd(entity: Record<string, any>): void {
    entity.StandardCode = this.selectedNode?.Code ?? ''
  }

  /** 工具栏：新增参数 + 批量删除 + 刷新（内核默认无 refresh 动作） */
  override get toolbarActions(): YzhAction[] {
    return [
      { key: 'add', text: '新增参数', type: 'primary' },
      { key: 'delete', text: '批量删除', type: 'danger' },
      { key: 'refresh', text: '刷新', type: 'default' },
    ]
  }

  constructor() {
    super()
    this.registerHandler('refresh', async () => {
      await this.refreshTable()
    })
  }
}

export default FillParamDefLogic
