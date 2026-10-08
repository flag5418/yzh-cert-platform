/**
 * CertStageLogic — 认证阶段管理（后台管理 · 左树右表）
 *
 * 后端：`CertStageController`（`YzhControllerBase<CertStageView>`）@ `api/Admin/Foundation/CertStage`
 * T+V：写 `cert_cert_stage` ｜ 读 `v_cert_stage`（含 CategoryName / StatusName 中文）
 *
 * ★ 数据模型绑定制（2026-10-08 用户裁决）：
 *   左树 = **字典 `stage_category` 的 3 个分类**（流程阶段 / 审核阶段 / 证后阶段，只读单层）
 *   右表 = 选中分类下阶段（`Category eq node.Code` 单条件 AND，走基类 `/filter`）
 *   节点 `Code` = 字典 `DicValue`（`process` / `audit` / `post`）= 阶段行 `Category` 原值
 *
 * ★ 覆写清单（与 `cert/fill-param-def` 同款，为什么见其文件头注释）：
 *   - `controllerName`              —— 三层同构路由段
 *   - `loadConfig`                  —— 内核默认 `/treepconfig` 只存在于 `TreeTableControllerBase`，
 *                                      本控制器是 `YzhControllerBase`（树是字典数据源，不适配）
 *                                      ⇒ 改走 `/config` + 手工拼 TreeConfig
 *   - `loadTreeRoot`                —— 树数据源 = 字典 `stage_category`（跨控制器）
 *   - `autoSelectFirstNode`         —— `NoSelectionBehavior='empty'` 时未选中右表恒空 ⇒ 载入即选中首个分类
 *   - `onPrepareAdd`                —— 新增行注入 `Category` = 选中节点 `Code`（归属键，表单不出现该字段）
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
  type YzhAction,
} from '@yzh-core'

/** 字典项 DTO（`GET /api/System/Dictionary/items/by-no/{dicNo}`） */
type DictItem = { Value: string; Label: string; Code?: string; Color?: string | null }

export class CertStageLogic extends TreeTableCore<any> {
  controllerName = 'Admin/Foundation/CertStage'

  /** 删除确认显示阶段名称 */
  protected override get entityNameField(): string {
    return 'StageName'
  }

  /** 新增行默认值（归属分类由 onPrepareAdd 按选中节点注入） */
  protected override get defaultValues(): Record<string, any> {
    return {
      IsValid: 1,
      SortOrder: 0,
    }
  }

  /** 未选节点提示 */
  protected override get requireTreeSelectionMessage(): string {
    return '请先在左侧选择阶段分类'
  }

  /** 载入后自动选中第一个分类（= 流程阶段）——未选中时右表按 empty 语义恒空 */
  protected override get autoSelectFirstNode(): boolean {
    return true
  }

  /**
   * 加载页面配置：改走 `/config` + 手工拼 `TreeTableConfigDto`。
   *
   * <p>TreeConfig 全关（左树只读）：AllowEdit / AllowDelete / AllowAddChild / AllowToggle=false
   * ⇒ `resolveTreeActions` 返回空数组，树上不渲染任何节点按钮。
   * RelateField='Category'（右表过滤字段）；NoSelectionBehavior='empty'。</p>
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
        RelateField: 'Category',
        NoSelectionBehavior: 'empty',
        MaxLevel: 1,
        AllowDeleteWithChildren: false,
      },
    } as TreeTableConfigDto
  }

  /**
   * 树数据源 = 字典 `stage_category`（跨控制器）。
   * <p>节点 Code = DicValue（process/audit/post）= 右表 `Category` 过滤值。</p>
   */
  override async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const res = await yzhApi.get<ApiResponse<DictItem[]>>(
        '/api/System/Dictionary/items/by-no/stage_category',
      )
      expectOk(res, '加载阶段分类失败')
      const items: TreeItemDto[] = (res.data ?? []).map((dto) => ({
        Code: dto.Value,
        Name: dto.Label,
        IsLeaf: true,
        Level: 0,
      }))
      this.treeSide.setNodes(items.map((dto) => this.dtoToNode(dto)))
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  /** 新增行注入归属分类（Code 由后端生成；Category = 选中节点 Code） */
  protected override onPrepareAdd(entity: Record<string, any>): void {
    entity.Category = this.selectedNode?.Code ?? 'process'
  }

  /** 工具栏：新增阶段 + 批量删除 + 刷新（内核默认无 refresh 动作） */
  override get toolbarActions(): YzhAction[] {
    return [
      { key: 'add', text: '新增阶段', type: 'primary' },
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

export default CertStageLogic
