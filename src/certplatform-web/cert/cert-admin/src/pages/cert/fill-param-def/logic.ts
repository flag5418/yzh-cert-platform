/**
 * FillParamDefLogic — 企业资料参数（后台管理 · 左树右表）
 *
 * 后端：`FillParamDefController`（`YzhControllerBase<FillParamDef>`）@ `api/Admin/Cert/FillParamDef`
 * 表：`cert_fill_param_def`｜菜单 `MENU_00217`（2026-10-06 改名「企业资料参数」）｜路由 `/business/fill-param-def`
 *
 * ★ 数据模型绑定制（26 号 §3.1 裁决，2026-10-06 重定义）：
 *   左树 = **[通用] + 各ISO标准**（只读单层，数据源 = ISO 标准树，非本实体）
 *   右表 = 选中节点下参数（`StandardCode eq node.Code` 单条件 AND，走基类 `/filter`）
 *   关键字段不分机构：`OrgCode` / `StageCode` 服务端强制恒空串
 *
 * ★ 覆写清单（每条都注明为什么样板没有）：
 *   - `controllerName`        —— 三层同构路由段
 *   - `loadConfig`            —— ⛔ 内核默认 `/treepconfig` 只存在于 `TreeTableControllerBase`，
 *                                本控制器是 `YzhControllerBase`（树数据源是另一实体，不适配
 *                                TreeTableControllerBase）⇒ 改走 `/config` + 手工拼 TreeConfig
 *   - `loadTreeRoot`          —— 树数据源 = ISO 标准树（跨控制器）+ 前置「通用参数」根
 *   - `autoSelectFirstNode`   —— `NoSelectionBehavior='empty'` 时未选中右表恒空 ⇒ 载入即选中通用
 *   - `onPrepareAdd`          —— 新增行注入 `StandardCode` = 选中节点 Code（归属键）
 *   - `requireTreeSelectionMessage` —— 本页业务文案
 *   - `toolbarActions` + refresh handler —— 样板同款（内核默认工具栏无「刷新」）
 *
 * ★ 通用根节点为什么是 `Code=''` 的**真实节点**而不是 `NodeType:'virtual'`：
 *   内核 `isVirtualNode()` 判定 virtual 后 `shouldApplyTreeFilter()` 恒 false ⇒ 不注入树过滤
 *   ⇒ 右表变全量。「通用参数」是真实归属（`StandardCode=''`），必须走正常过滤路径。
 */
import {
  TreeTableCore,
  expectOk,
  type ApiResponse,
  type EntityConfigDto,
  type TreeItemDto,
  type TreeTableConfigDto,
  type YzhAction,
} from '@yzh-core'
import { getIsoStandardTree } from '@share/api/cert/fill-param-def'

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
    return '请先在左侧选择「通用参数」或标准'
  }

  /** 载入后自动选中第一个节点（= 「通用参数」）——未选中时右表按 empty 语义恒空 */
  protected override get autoSelectFirstNode(): boolean {
    return true
  }

  /**
   * 加载页面配置：改走 `/config` + 手工拼 `TreeTableConfigDto`。
   *
   * <p>TreeConfig 全关（左树只读）：AllowEdit / AllowDelete / AllowAddChild / AllowToggle=false
   * ⇒ `resolveTreeActions` 返回空数组，树上不渲染任何节点按钮。
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
        MaxLevel: 1,
        AllowDeleteWithChildren: false,
      },
    } as TreeTableConfigDto
  }

  /**
   * 树数据源 = ISO 标准树（跨控制器），前置「通用参数」根。
   * <p>⛔ 不能标 NodeType:'virtual'（见文件头注释）。</p>
   */
  override async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const standards = await getIsoStandardTree()
      const common: TreeItemDto = { Code: '', Name: '通用参数', IsLeaf: true, Level: 0 }
      this.treeSide.setNodes(
        [common, ...standards].map((dto) => this.dtoToNode(dto)),
      )
    } finally {
      this.treeSide.treeLoading.value = false
    }
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
