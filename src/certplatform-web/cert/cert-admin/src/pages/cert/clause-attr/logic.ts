/**
 * ClauseAttrLogic — 条款属性配置（L1）Logic（TreeTableCore 架构）
 *
 * 后端：`ClauseAttrController`（`YzhControllerBase<ClauseAttr>`）
 * 路由：`api/Admin/Cert/ClauseAttr` ｜ 表：`cert_clause_attr`
 *
 * ★ 业务定位（`docs/70-V2设计/24-条款标准化-属性池与条款配置-V2.md` §四）：
 *   一行 = **某条款上的某个属性**。没配 = 没行（稀疏），⛔ 没有「适用条款」列。
 *   左树 = 标准 → **叶子条款**（208 条；只列叶子 —— 非叶子条款不产生检查要求）；
 *   右表 = 选中条款已配的属性。
 *
 * ★★ 一个条款 = 两份要求（本页最重要的业务约束）：
 *   ① `Artifacts`  —— 企业侧「应提供哪些资料」（可公开给企业）
 *   ② `EvidenceChain` —— 审核员侧「怎么取证」（**取证剧本，不提前给企业**）
 *   合成一条 ⇒ 企业照着剧本准备 ⇒ 审核失效。⇒ 池子里是两条独立属性，⛔ 不合并。
 *
 * ★ `ValueSource` 只在这里定、只两档：
 *   `fixed`（条款已定，存值本身）/ `reference`（条款只给期望，存期望）。
 *   同一属性在不同条款下来源不同（`Artifacts` 在 7.1.5.2 = reference、在 7.3 = fixed 且值为空）
 *   ⇒ ⛔ 绝不能把它挪回属性池（L0）。
 *
 * ★ 覆写清单（本页后端是 `YzhControllerBase`，无 `/treepconfig`，树也不是本实体树）：
 *   - `loadConfig`       —— 改走 `/config` + 手工拼 `TreeTableConfigDto`（RelateField='ClauseCode'）
 *   - `loadTreeRoot`     —— 树数据源 = `POST /tree/root`（标准→叶子条款，嵌套结构需递归映射）
 *   - `shouldApplyTreeFilter` / `relatedValue` —— 只有「条款」节点参与右表过滤
 *   - `afterTreeLoaded`  —— 载入即选中第一条叶子条款（否则右表恒空，用户以为坏了）
 *   - `onPrepareAdd`     —— 新增行注入 `StandardCode` + `ClauseCode`（三键中的两个，表单不出现）
 *   - `formFields`       —— `AttrCode` 改成属性池下拉；`AttrValue` 按值类型给提示
 *   - `columns`          —— `AttrCode` 列改走插槽（表格显示中文属性名，⛔ 不显示裸 Code）
 *   - `rowActions`       —— 追加「编辑提示词」抽屉入口（`prompt` 类型才出现）
 */
import {
  TreeTableCore,
  expectOk,
  type ApiResponse,
  type EntityConfigDto,
  type FilterItem,
  type TreeItemDto,
  type TreeTableConfigDto,
  type TreeNode,
  type YzhAction,
  type YzhFormField,
  type YzhTableColumn,
} from '@yzh-core'
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getClauseAttrOptions,
  getClauseAttrTree,
  type ClauseAttrOption,
} from '@share/api/cert/clause-attr'

/** 属性值类型 = prompt 时，AttrValue 里装的是提示词正文（不是普通文本） */
const PROMPT_TYPE = 'prompt'

/** 抽屉行按钮的 key（与 `registerHandler` 注册名一致） */
const ACTION_EDIT_PROMPT = 'edit-prompt'

export class ClauseAttrLogic extends TreeTableCore<any> {
  controllerName = 'Admin/Cert/ClauseAttr'

  /** 属性池选项（下拉 + 表格中文名 + 值类型判定，一次加载全页复用） */
  attrOptions = ref<ClauseAttrOption[]>([])

  /** 提示词抽屉 */
  promptDrawerVisible = ref(false)
  promptSaving = ref(false)
  promptDraft = ref('')
  /** 正在编辑提示词的行（保存时整行回传 —— `UpdateCore` 会覆盖所有 BcFlag 列） */
  promptRow = ref<Record<string, any> | null>(null)

  /** 删除确认里显示的实体名称（本实体无单一名，用「条款 + 属性」拼） */
  protected override get entityNameField(): string {
    return 'AttrCode'
  }

  /** 新增默认值：值来源默认「参考」（条款只给期望，值由企业/证据决定） */
  protected override get defaultValues(): Record<string, any> {
    return {
      ValueSource: 'reference',
      IsValid: 1,
      Sort: 0,
    }
  }

  /** 未选条款时的新增提示 */
  protected override get requireTreeSelectionMessage(): string {
    return '请先在左侧选择条款'
  }

  // ========================================================
  // 配置加载（后端无 /treepconfig）
  // ========================================================

  /**
   * `/config` + 手工拼 TreeConfig。
   *
   * <p>TreeConfig 全关（左树只读）：标准与条款的维护在「体系标准管理」页，本页只作筛选维度。
   * `RelateField='ClauseCode'` ⇒ 内核 `buildFilters` 自动注入 `ClauseCode eq node.Code`；
   * `NoSelectionBehavior='empty'` ⇒ 未选条款不发请求、右表空。</p>
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
        // ★ 必须显式 false：否则 resolveTreeActions 回落到 config.EnableField(IsValid)，
        //   在「标准/条款」节点上吐出「启用/禁用」按钮 —— 树节点没有启停语义
        AllowToggle: false,
        NameField: 'Name',
        CodeField: 'Code',
        ParentCodeField: 'ParentCode',
        RelateField: 'ClauseCode',
        NoSelectionBehavior: 'empty',
        MaxLevel: 2,
        AllowDeleteWithChildren: false,
      },
    } as TreeTableConfigDto

    // 属性池选项与配置同批加载（表格中文名 / 下拉 / 值类型判定都依赖它）
    await this.loadOptions()
  }

  /** 加载属性池选项（只含启用的；失败降级为空并提示，不阻断整页） */
  async loadOptions(): Promise<void> {
    try {
      this.attrOptions.value = await getClauseAttrOptions()
    } catch {
      this.attrOptions.value = []
      ElMessage.warning('属性池选项加载失败，属性下拉将为空')
    }
  }

  // ========================================================
  // 树加载（标准 → 叶子条款）
  // ========================================================

  override async loadTreeRoot(): Promise<void> {
    this.treeSide.treeLoading.value = true
    try {
      const items = await getClauseAttrTree()
      this.treeSide.setNodes(items.map((dto) => this.dtoToNodeDeep(dto)))
    } finally {
      this.treeSide.treeLoading.value = false
    }
  }

  /**
   * 嵌套 DTO → 嵌套 TreeNode。
   *
   * <p>⚠️ 内核 `dtoToNode` **只映射当前节点**（`Children` 恒返回 `[]`）——
   * 它服务的是懒加载场景（子节点由 `/tree/children` 逐层补）。
   * 本页一次性返回「标准 → 条款」两层，必须自己递归，否则树只剩 4 个标准、208 条条款全丢。</p>
   */
  private dtoToNodeDeep(dto: TreeItemDto, parent?: TreeNode | null): TreeNode {
    const node = this.dtoToNode(dto, parent)
    if (dto.Children?.length) {
      node.Children = dto.Children.map((child) => this.dtoToNodeDeep(child, node))
    }
    return node
  }

  /** 载入后展开第一个标准（⛔ 不自动选中条款：208 条叶子随便挑一条会让右表"看起来有数据"，
   *  但用户并不知道自己在看哪条；`NoSelectionBehavior='empty'` + 空态文案更诚实） */
  protected override async afterTreeLoaded(): Promise<void> {
    // 仅做一次选项兜底：树已加载但选项为空（loadConfig 里失败）时再试一次
    if (this.attrOptions.value.length === 0) await this.loadOptions()
  }

  /** 右表空态文案（区分「没选条款」与「选了但没配」） */
  get emptyText(): string {
    return this.shouldApplyTreeFilter()
      ? '该条款还没配置属性，点「新增属性」开始'
      : '请在左侧选择一个条款'
  }

  /** 默认展开的标准（只展开第一个，⛔ 不 `treeDefaultExpandAll` —— 208 条叶子一次铺开太长） */
  get expandedKeys(): string[] {
    const first = this.treeData[0]
    return first ? [first.Code] : []
  }

  // ========================================================
  // 树 → 表格联动
  // ========================================================

  /** 只有「条款」节点参与右表过滤；点标准节点 ⇒ 右表空（该标准有 200+ 条款，全列无意义） */
  protected override shouldApplyTreeFilter(): boolean {
    return this.selectedNode?.NodeType === 'clause'
  }

  /** 关联值 = 条款节点的 Code（= `cert_iso_clause.Code` = 右表 `ClauseCode`） */
  protected override relatedValue(): string | null {
    return this.shouldApplyTreeFilter() ? (this.selectedNode?.Code ?? null) : null
  }

  /**
   * 追加「标准」过滤：条款 Code 是 GUID 已全局唯一，理论上单条件够；
   * 但 `cert_clause_attr` 的唯一键含 `StandardCode`，多带一个条件可让索引更稳、也防脏数据。
   */
  protected override buildFilters(extra?: Record<string, any>): FilterItem[] {
    const base = super.buildFilters(extra)
    const stdCode = this.selectedNode?.Extra?.StandardCode
    if (this.shouldApplyTreeFilter() && stdCode) {
      base.push({ Field: 'StandardCode', Value: stdCode, Operator: 'eq' })
    }
    return base
  }

  /** 新增行注入三键中的两个（`AttrCode` 由表单选，另两键来自树节点） */
  protected override onPrepareAdd(entity: Record<string, any>): void {
    const node = this.selectedNode
    entity.ClauseCode = node?.NodeType === 'clause' ? node.Code : ''
    entity.StandardCode = (node?.Extra?.StandardCode as string) ?? ''
  }

  // ========================================================
  // 表格列 / 表单字段
  // ========================================================

  /** `AttrCode` 列改走插槽：表格显示中文属性名，裸 Code 放副行（⛔ 不让人对着 `Artifacts` 猜） */
  override get columns(): YzhTableColumn<any>[] {
    return super.columns.map((col: any) =>
      col.prop === 'AttrCode' ? { ...col, slot: 'AttrCode' } : col,
    )
  }

  /** 属性中文名（找不到时回落 Code 本身，⛔ 不显示空白） */
  attrNameOf(code: string): string {
    return this.attrOptions.value.find((o) => o.Code === code)?.AttrName || code || '—'
  }

  /** 该属性码是否为提示词类型 */
  isPromptAttr(code: string): boolean {
    return this.attrOptions.value.find((o) => o.Code === code)?.ValueType === PROMPT_TYPE
  }

  /**
   * 表单里当前选中的属性码（★ 独立镜像 ref，⛔ 不读 `this.formData.AttrCode`）。
   *
   * <p>同 `ClauseAttrDefLogic.formValueType` 的坑：`YzhFormDialog` 的 `v-model` 回写是
   * `logic.formData = $event`，而逻辑实例不是 `reactive` 的 ⇒ reactive 的 `formData`
   * 被替换成普通对象，之后所有 `this.formData.X` 读取都不再建立依赖，
   * 依赖它的 getter 永远不重算（实测：切换属性后「值」的提示语不跟着变）。</p>
   */
  private formAttrCode = ref('')

  /** 属性下拉变更 → 同步镜像 */
  onAttrCodeChange(value: unknown): void {
    this.formAttrCode.value = String(value ?? '')
  }

  /** 新增：镜像清空（属性还没选） */
  override openAddDialog(): void {
    this.formAttrCode.value = ''
    super.openAddDialog()
  }

  /** 编辑：镜像取行上的属性码 */
  override openEditDialog(row: any): void {
    this.formAttrCode.value = String(row?.AttrCode ?? '')
    super.openEditDialog(row)
  }

  /** 属性下拉选项（值 = AttrCode；标签带中文名，便于按语义找） */
  private get attrSelectOptions(): Array<{ label: string; value: string }> {
    return this.attrOptions.value.map((o) => ({
      label: `${o.AttrName}（${o.Code}）`,
      value: o.Code,
    }))
  }

  override get formFields(): YzhFormField[] {
    const isEdit = this.dialogMode.value === 'edit'
    return super.formFields.map((f) => {
      if (f.prop === 'AttrCode') {
        return {
          ...f,
          type: 'select' as any,
          options: this.attrSelectOptions,
          // 三键（StandardCode / ClauseCode / AttrCode）是唯一索引成员，后端禁止修改
          disabled: isEdit,
          placeholder: '从属性池选择（只列启用的）',
          fieldProps: {
            ...(f.fieldProps as any),
            onChange: (v: unknown) => this.onAttrCodeChange(v),
          },
        }
      }
      if (f.prop === 'AttrValue' && this.isPromptAttr(this.formAttrCode.value)) {
        return {
          ...f,
          placeholder: '该属性是「提示词」类型：短句可在此写，长文本请用行内「编辑提示词」抽屉',
        }
      }
      return f
    })
  }

  // ========================================================
  // 行按钮：追加「编辑提示词」（prompt 类型专供）
  // ========================================================

  override get rowActions(): YzhAction[] | ((row: any) => YzhAction[]) {
    const base = super.rowActions
    return (row: any): YzhAction[] => {
      const list = typeof base === 'function' ? base(row) : base
      if (!this.isPromptAttr(String(row?.AttrCode ?? ''))) return list
      return [
        { key: ACTION_EDIT_PROMPT, text: '编辑提示词', type: 'primary' },
        ...list,
      ]
    }
  }

  // ========================================================
  // 提示词抽屉（长文本专用 —— ⛔ 不在表格里塞 textarea）
  // ========================================================

  /** 打开提示词抽屉 */
  openPromptDrawer(row: Record<string, any>): void {
    this.promptRow.value = { ...row }
    this.promptDraft.value = String(row?.AttrValue ?? '')
    this.promptDrawerVisible.value = true
  }

  /** 抽屉标题：当前编辑属性的中文名（模板里 `promptRow` 是 Ref，⛔ 不能直接取属性） */
  get promptAttrName(): string {
    return this.attrNameOf(this.promptAttrCode)
  }

  /** 抽屉标题：当前编辑属性的裸 Code */
  get promptAttrCode(): string {
    return String(this.promptRow.value?.AttrCode ?? '')
  }

  /** 保存提示词（整行回传：`UpdateCore` 覆盖所有 BcFlag 列，只传 AttrValue 会把别的列清空） */
  async savePrompt(): Promise<void> {
    const row = this.promptRow.value
    if (!row?.Code) {
      ElMessage.warning('缺少行标识，无法保存')
      return
    }
    this.promptSaving.value = true
    try {
      await this.update({ ...row, AttrValue: this.promptDraft.value })
      ElMessage.success('提示词已保存')
      this.promptDrawerVisible.value = false
      await this.refresh()
    } catch (e) {
      this.reportError(e, '保存提示词失败')
    } finally {
      this.promptSaving.value = false
    }
  }

  // ========================================================
  // 工具栏
  // ========================================================

  override get toolbarActions(): YzhAction[] {
    return [
      { key: 'add', text: '新增属性', type: 'primary' },
      { key: 'delete', text: '批量删除', type: 'danger' },
      { key: 'refresh', text: '刷新', type: 'default' },
    ]
  }

  constructor() {
    super()
    this.registerHandler(ACTION_EDIT_PROMPT, (row) => {
      if (row) this.openPromptDrawer(row)
    })
    this.registerHandler('refresh', async () => {
      await this.refresh()
    })
  }
}

export default ClauseAttrLogic
