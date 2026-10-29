/**
 * ClauseAttrLogic — 条款属性配置（L1 · 左树 + 条款工作台）
 *
 * 后端：`ClauseAttrController`（`YzhControllerBase<ClauseAttr>`）
 * 路由：`api/Admin/Cert/ClauseAttr` ｜ 表：`cert_clause_attr`
 *
 * ★ 页面形态（2026-10-29 改）：
 *   左树 = 标准 → 章 → 节 → 叶子条款（**完整层次**，章节是真正的树节点，不是扁平列表）
 *   右侧 = **条款工作台**：条款详情（编号 / 标题 / 路径 / 原文）+ 该条款的属性清单
 *
 * ★ 为什么右侧不再是「只列已配行」的表格：
 *   一条款真正要配的是属性池里的那几条（当前 8 条，后续 12 条）。用表格时每配一条
 *   都要「新增 → 下拉选属性 → 填值」重复一遍，条款一多就没法用；而且"没配"与
 *   "配了但值为空"在表格里长得一样。改成清单后：池子里每条属性都在，勾选即配置、
 *   直接填值、一次保存。
 *
 * ★★ 一个条款 = 两份要求（本页最重要的业务约束）：
 *   ① `Artifacts`  —— 企业侧「应提供哪些资料」（可公开给企业）
 *   ② `EvidenceChain` —— 审核员侧「怎么取证」（**取证剧本，不提前给企业**）
 *   合成一条 ⇒ 企业照着剧本准备 ⇒ 审核失效。⇒ 池子里是两条独立属性，⛔ 不合并。
 *
 * ★ 提示词就是值（2026-10-29 用户裁决）：
 *   `ValueType = prompt` 的属性（Artifacts / EvidenceChain）值本身是提示词正文，
 *   **在属性行里直接编辑**（textarea），⛔ 不再有独立的「编辑提示词」按钮 / 抽屉。
 *
 * ★ `ValueSource` 只在这里定、只两档：
 *   `fixed`（条款已定，存值本身）/ `reference`（条款只给期望，存期望）。
 *   同一属性在不同条款下来源不同（`Artifacts` 在 7.1.5.2 = reference、在 7.3 = fixed 且值为空）
 *   ⇒ ⛔ 绝不能把它挪回属性池（L0）。
 *
 * ★ 覆写清单（本页后端是 `YzhControllerBase`，无 `/treepconfig`，树也不是本实体树）：
 *   - `loadTreeRoot`   —— 树数据源 = `POST /tree/root`（标准→章→节→条款，嵌套结构需递归映射）
 *   - `onNodeClick`    —— 点任意节点只刷新「条款详情 + 属性清单」，不查本实体分页
 *   - `afterTreeLoaded`—— 载入即拉属性池选项（清单的左侧名称 / 控件类型都依赖它）
 *   - `sheet*`         —— 属性清单的加载 / 保存（新增 / 更新 / 取消配置）
 */
import { TreeTableCore, confirmOrFalse, expectOk, type ApiResponse, type EntityConfigDto, type TreeItemDto, type TreeTableConfigDto, type TreeNode } from '@yzh-core'
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  createClauseAttr,
  deleteClauseAttrs,
  generateClauseAttrs,
  getClauseAttrOptions,
  getClauseAttrTree,
  getDictItems,
  listClauseAttrs,
  updateClauseAttr,
  type ClauseAttrGenerateItem,
  type ClauseAttrGenerateResult,
  type ClauseAttrOption,
  type ClauseAttrRow,
} from '@share/api/cert/clause-attr'

/** 属性清单一行（= 属性池某条属性 × 当前条款） */
export interface AttrSheetRow {
  /** 属性码（属性池 Code，如 Artifacts） */
  AttrCode: string
  /** 属性中文名 */
  AttrName: string
  /** 值类型：text / dict / bool / json / prompt */
  ValueType: string
  /** 字典编码（dict / json 用） */
  DictCode: string
  /** 属性说明（配置者看的"这条属性参与哪一步"） */
  Remark: string
  /** 本条属性是否已配置（勾选 = 保存时写入；取消勾选 = 保存时删除） */
  Configured: boolean
  /** 已配置时的原始行（更新必须整行回传，三键不可缺） */
  Raw: ClauseAttrRow | null
  /** 草稿值 */
  AttrValue: string
  /** 值来源：fixed / reference */
  ValueSource: string
  /** 启用状态（1 / 0） */
  IsValid: number
  /** 排序（继承已配置行的值；新增时后端按 max+10 补） */
  Sort: number
}

/** 字典下拉选项（组件层 camelCase） */
export interface DictOption {
  label: string
  value: string
}

export class ClauseAttrLogic extends TreeTableCore<any> {
  controllerName = 'Admin/Cert/ClauseAttr'

  // ========================================================
  // 状态
  // ========================================================

  /** 属性池选项（清单行、控件类型、字典编码一次加载全页复用） */
  attrOptions = ref<ClauseAttrOption[]>([])

  /** 当前条款的属性清单（= 属性池全集；`Configured` 标记是否已配） */
  sheet = ref<AttrSheetRow[]>([])

  /** 清单加载中 */
  sheetLoading = ref(false)

  /** 清单保存中 */
  sheetSaving = ref(false)

  /** 提示词编辑抽屉可见 */
  promptDrawerVisible = ref(false)

  /** 字典项缓存：dicNo → options */
  dicts = ref<Record<string, DictOption[]>>({})

  /** 清单初始快照（AttrCode → 签名），用于判断"有没有改动" */
  private original = new Map<string, string>()

  // ========================================================
  // 配置 / 树加载
  // ========================================================

  /**
   * 载入页面配置。
   *
   * <p>⚠️ 不能走内核 `/treepconfig`：本页后端是 `YzhControllerBase`，没有这个端点（会 404），
   * 而且树的数据源是 `cert_iso_clause` 不是本实体。这里取 `/config` 拿实体元信息，
   * 再手工拼一个最小 `TreeConfig`（右表已不存在，只保留 `RelateField` 语义给内核兜底）。</p>
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
        MaxLevel: 4,
        AllowDeleteWithChildren: false,
      },
    } as TreeTableConfigDto

    // 属性池选项与配置同批加载（清单每行都来自它）
    await this.loadOptions()
  }

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
   * 本页一次性返回「标准 → 章 → 节 → 条款」，必须自己递归，否则树只剩 4 个标准。</p>
   */
  private dtoToNodeDeep(dto: TreeItemDto, parent?: TreeNode | null): TreeNode {
    const node = this.dtoToNode(dto, parent)
    if (dto.Children?.length) {
      node.Children = dto.Children.map((child) => this.dtoToNodeDeep(child, node))
    }
    return node
  }

  /** 载入后：属性池选项必须就绪（清单的每一行都来自它） */
  protected override async afterTreeLoaded(): Promise<void> {
    if (this.attrOptions.value.length === 0) await this.loadOptions()
  }

  /**
   * 点树节点：只切换选中 + 刷新工作台。
   *
   * <p>⛔ 不走内核的 `onNodeClick`：那里会触发右表 `/filter`（本页已无右表），
   * 而且选中"章 / 节"时还会发一次无用查询。</p>
   */
  override onNodeClick = async (node: TreeNode): Promise<void> => {
    this.selectedNode = node
    await this.loadSheet()
  }

  /** 默认展开：第一个标准 + 它下面的第一个章（让用户一眼看到层次） */
  get expandedKeys(): string[] {
    const result: string[] = []
    const first = this.treeData[0]
    if (first) {
      result.push(first.Code)
      const firstChapter = first.Children?.[0]
      if (firstChapter) result.push(firstChapter.Code)
    }
    return result
  }

  // ========================================================
  // 属性池选项 / 字典
  // ========================================================

  /** 加载属性池选项（只含启用的；失败降级为空并提示，不阻断整页） */
  async loadOptions(): Promise<void> {
    try {
      this.attrOptions.value = await getClauseAttrOptions()
    } catch {
      this.attrOptions.value = []
      ElMessage.warning('属性池选项加载失败，条款属性清单将为空')
    }
  }

  /** 懒加载字典项（同一 dicNo 只请求一次；失败静默降级为空下拉） */
  async ensureDict(dicNo: string): Promise<void> {
    if (!dicNo || this.dicts.value[dicNo]) return
    try {
      const items = await getDictItems(dicNo)
      this.dicts.value = {
        ...this.dicts.value,
        [dicNo]: items.map((i) => ({ label: i.Label, value: i.Value })),
      }
    } catch {
      this.dicts.value = { ...this.dicts.value, [dicNo]: [] }
    }
  }

  /** 确保清单里所有 dict / json 属性的字典都已加载 */
  private async ensureDicts(): Promise<void> {
    const codes = [...new Set(this.sheet.value.map((r) => r.DictCode).filter(Boolean))]
    await Promise.all(codes.map((c) => this.ensureDict(c)))
  }

  /** 某行的字典下拉选项 */
  dictOptionsOf(row: AttrSheetRow): DictOption[] {
    return row.DictCode ? (this.dicts.value[row.DictCode] ?? []) : []
  }

  // ========================================================
  // 条款详情（工作台头部）
  // ========================================================

  /** 当前选中的是不是叶子条款（只有叶子能配属性） */
  get isClauseNode(): boolean {
    return this.selectedNode?.NodeType === 'clause'
  }

  /** 节点类型文案 */
  get nodeTypeLabel(): string {
    switch (this.selectedNode?.NodeType) {
      case 'standard':
        return '标准'
      case 'chapter':
        return '章'
      case 'section':
        return '节'
      case 'clause':
        return '条款'
      default:
        return ''
    }
  }

  /** 节点类型徽章颜色（S08：状态色只在徽章） */
  get nodeBadgeType(): 'success' | 'warning' | 'info' {
    if (this.selectedNode?.NodeType === 'clause') return 'success'
    if (this.selectedNode?.NodeType === 'standard') return 'info'
    return 'warning'
  }

  /** 条款号（标准节点没有） */
  get selectedNumber(): string {
    return String(this.selectedNode?.Extra?.ClauseNumber ?? '')
  }

  /** 标题（标准节点回落到 Name） */
  get selectedTitle(): string {
    const title = this.selectedNode?.Extra?.Title
    return title ? String(title) : (this.selectedNode?.Name ?? '')
  }

  /** 当前标准短码（iso9001…） */
  get standardCode(): string {
    return String(this.selectedNode?.Extra?.StandardCode ?? '')
  }

  /** 祖先路径：`[iso9001] › 7 支持 › 7.1 资源`（不含自身） */
  get nodePath(): string {
    const node = this.selectedNode
    if (!node) return ''
    const parts: string[] = []
    let cur: TreeNode | null = this.treeSide.findParent(node.Code)
    while (cur) {
      if (cur.NodeType === 'standard') {
        parts.unshift(`[${cur.Code}]`)
      } else {
        const number = String(cur.Extra?.ClauseNumber ?? '')
        const title = String(cur.Extra?.Title ?? '')
        parts.unshift(title ? `${number} ${title}` : (number || cur.Name))
      }
      cur = this.treeSide.findParent(cur.Code)
    }
    return parts.join(' › ')
  }

  /** 条款原文（章 / 节可能为空） */
  get clauseDescription(): string {
    return String(this.selectedNode?.Extra?.Description ?? '')
  }

  /** 无正文时的说明（区分"章节汇总节点"与"条款本身没录入正文"） */
  get noDescriptionTip(): string {
    if (!this.selectedNode) return ''
    if (this.isClauseNode) return '该条款暂未录入原文。'
    return '章 / 节是汇总节点，本身没有独立要求；展开后选择具体条款查看正文与属性。'
  }

  /** 子节点数（章 / 节用） */
  get childCount(): number {
    return this.selectedNode?.Children?.length ?? 0
  }

  /** 属性清单里已配置的条数 */
  get configuredCount(): number {
    return this.sheet.value.filter((r) => r.Configured).length
  }

  /** 当前节点的所有后代叶子条款数（标准 / 章 / 节用） */
  get leafCountUnder(): number {
    const count = (nodes: TreeNode[] | undefined): number =>
      (nodes ?? []).reduce((sum, n) => sum + (n.IsLeaf ? 1 : count(n.Children)), 0)
    return count(this.selectedNode?.Children)
  }

  // ========================================================
  // 属性清单：加载 / 编辑 / 保存
  // ========================================================

  /** 当前行签名（判断是否改动） */
  private sig(row: AttrSheetRow): string {
    return JSON.stringify([
      row.Configured,
      row.AttrValue ?? '',
      row.ValueSource ?? '',
      row.IsValid ?? 1,
    ])
  }

  /** 有改动的行数（保存按钮的禁用依据 + 头部提示） */
  get dirtyCount(): number {
    return this.sheet.value.filter((r) => this.sig(r) !== this.original.get(r.AttrCode)).length
  }

  /** 加载当前条款的属性清单（属性池全集 × 已配行） */
  async loadSheet(): Promise<void> {
    this.sheet.value = []
    this.original.clear()
    const node = this.selectedNode
    if (!node || node.NodeType !== 'clause') return

    this.sheetLoading.value = true
    try {
      const rows = await listClauseAttrs({
        ClauseCode: node.Code,
        StandardCode: this.standardCode,
      })
      const byAttr = new Map(rows.map((r) => [String(r.AttrCode), r]))

      this.sheet.value = this.attrOptions.value.map((def) => {
        const row = byAttr.get(def.Code) ?? null
        return {
          AttrCode: def.Code,
          AttrName: def.AttrName,
          ValueType: String(def.ValueType ?? 'text').toLowerCase(),
          DictCode: String(def.DictCode ?? ''),
          Remark: String(def.Remark ?? ''),
          Configured: !!row,
          Raw: row,
          AttrValue: String(row?.AttrValue ?? ''),
          ValueSource: String(row?.ValueSource ?? 'reference'),
          IsValid: row?.IsValid ?? 1,
          Sort: row?.Sort ?? 0,
        }
      })

      for (const row of this.sheet.value) this.original.set(row.AttrCode, this.sig(row))
      await this.ensureDicts()
    } catch (e) {
      this.reportError(e, '加载条款属性失败')
    } finally {
      this.sheetLoading.value = false
    }
  }

  /** 用户一填值就视为"要配置这条属性"（省掉先勾选的步骤） */
  markConfigured(row: AttrSheetRow): void {
    if (!row.Configured) row.Configured = true
  }

  /** 勾选 / 取消勾选某条属性（取消勾选 = 保存时删除该行） */
  toggleConfigured(row: AttrSheetRow, checked: unknown): void {
    row.Configured = checked === true
    if (!row.Configured) return
    // 首次勾选且没有原始行 ⇒ 给一个合理默认：条款已明确写死的属性用固定值，
    // 其余（尤其 prompt / json 清单）默认参考值 —— 与 24 号 §六 的口径一致。
    if (!row.Raw && !row.AttrValue) {
      row.ValueSource = row.ValueType === 'dict' || row.ValueType === 'bool' ? 'fixed' : 'reference'
    }
  }

  /** text / prompt 属性写值（输入即视为要配置） */
  setValue(row: AttrSheetRow, value: unknown): void {
    row.AttrValue = value === null || value === undefined ? '' : String(value)
    this.markConfigured(row)
  }

  /** json 属性的数组值（存的是 JSON 字符串） */
  jsonValueOf(row: AttrSheetRow): string[] {
    const raw = row.AttrValue
    if (!raw) return []
    try {
      const parsed = JSON.parse(raw)
      return Array.isArray(parsed) ? parsed.map(String) : []
    } catch {
      return []
    }
  }

  /** json 属性写回（统一存 JSON 字符串） */
  setJsonValue(row: AttrSheetRow, value: string[]): void {
    row.AttrValue = JSON.stringify(value ?? [])
    this.markConfigured(row)
  }

  /** bool 属性的布尔值 */
  boolValueOf(row: AttrSheetRow): boolean {
    return row.AttrValue === 'true' || row.AttrValue === '1'
  }

  /** bool 属性写回 */
  setBoolValue(row: AttrSheetRow, value: boolean): void {
    row.AttrValue = value ? 'true' : 'false'
    this.markConfigured(row)
  }

  /** 放弃改动：重新按库里的值铺清单 */
  async resetSheet(): Promise<void> {
    await this.loadSheet()
    ElMessage.info('已恢复为库里的值')
  }

  /**
   * 保存本条款属性（一次提交全部改动）。
   *
   * <p>三种情况：</p>
   * <p>① 勾选且无 Raw ⇒ 新增；② 勾选且有 Raw ⇒ 更新（整行回传）；③ 取消勾选且有 Raw ⇒ 删除。</p>
   */
  async saveSheet(): Promise<void> {
    const node = this.selectedNode
    if (!node || node.NodeType !== 'clause') {
      ElMessage.warning('请先在左侧选择具体条款')
      return
    }

    const dirty = this.sheet.value.filter(
      (r) => this.sig(r) !== this.original.get(r.AttrCode),
    )
    if (dirty.length === 0) {
      ElMessage.info('没有需要保存的修改')
      return
    }

    const toDelete = dirty
      .filter((r) => !r.Configured && r.Raw?.Code)
      .map((r) => r.Raw!.Code)
    const toSave = dirty.filter((r) => r.Configured)

    if (toDelete.length > 0) {
      const ok = await confirmOrFalse(
        `有 ${toDelete.length} 条属性被取消勾选，保存后会删除它们在当前条款下的配置，是否继续？`,
        '取消属性配置',
        { type: 'warning', confirmButtonText: '继续保存', cancelButtonText: '返回修改' },
      )
      if (!ok) return
    }

    this.sheetSaving.value = true
    let added = 0
    let updated = 0
    let removed = 0
    try {
      if (toDelete.length > 0) {
        await deleteClauseAttrs(toDelete)
        removed = toDelete.length
      }

      for (const row of toSave) {
        if (!row.Raw?.Code) {
          await createClauseAttr({
            StandardCode: this.standardCode,
            ClauseCode: node.Code,
            AttrCode: row.AttrCode,
            AttrValue: row.AttrValue ?? '',
            ValueSource: row.ValueSource,
            Sort: row.Sort || 0,
            IsValid: row.IsValid,
          })
          added += 1
        } else {
          await updateClauseAttr({
            ...row.Raw,
            AttrValue: row.AttrValue ?? '',
            ValueSource: row.ValueSource,
            IsValid: row.IsValid,
          })
          updated += 1
        }
      }

      const parts = [`新增 ${added}`, `更新 ${updated}`]
      if (removed > 0) parts.push(`删除 ${removed}`)
      ElMessage.success(`已保存：${parts.join(' · ')}`)
      await this.loadSheet()
    } catch (e) {
      this.reportError(e, '保存条款属性失败')
    } finally {
      this.sheetSaving.value = false
    }
  }

  /** 刷新：重载树 + 清空选中（避免拿着旧节点对象操作已重建的树） */
  async refreshTree(): Promise<void> {
    this.selectedNode = null
    this.sheet.value = []
    this.original.clear()
    await this.loadTreeRoot()
  }

  /** 提示词抽屉关闭后的回调（提示词影响下次生成，无需重载工作台） */
  onPromptSaved(): void {
    // 提示词生成是后台行为，当前工作台已加载的属性无需刷新；
    // 关闭抽屉即可，让用户继续当前工作
    this.promptDrawerVisible.value = false
  }

  // ========================================================
  // AI 预生成（P4）
  // ========================================================

  /** 预生成弹窗可见 */
  genVisible = ref(false)
  /** 正在跑（服务端串行，单批 50 条可能要几分钟） */
  genRunning = ref(false)
  /** 目标标准短码（默认 = 当前选中条款所属标准） */
  genStandardCode = ref('')
  /** 单次条数（服务端硬上限 50） */
  genCount = ref(5)
  /** 是否覆盖已配属性 */
  genOverwrite = ref(false)
  /** 最近一次结果（逐条列表，失败原因直接展示给用户） */
  genResult = ref<ClauseAttrGenerateResult | null>(null)
  /**
   * 逐条结果的**独立镜像数组**（喂给 `YzhTable` 的 `:data` 静态通道）。
   *
   * <p>⚠️ 不能直接写 `:data="logic.genResult.value.Items"`：`YzhTable` 只认**引用变化**，
   * 而 `genResult` 为 null 时 `?.Items ?? []` 每次渲染都造一个新数组 ⇒ 每次都触发重载。
   * 用独立 ref 只在请求结束时整体替换一次。</p>
   */
  genItems = ref<ClauseAttrGenerateItem[]>([])
  /** 已跑秒数（服务端串行，单条数秒 ⇒ 必须让用户看见"还在动"） */
  genElapsed = ref(0)
  /** 指定条款 Code 集合（undefined = 全部） */
  genClauseCodes = ref<string[] | undefined>(undefined)

  /** 可选标准（树根节点 Code 就是标准短码，Name 带叶子数） */
  get standardOptions(): Array<{ label: string; value: string }> {
    return this.treeData.map((n) => ({ label: n.Name, value: n.Code }))
  }

  /** 打开预生成弹窗（默认瞄准"当前选中条款所属标准"，没选就用第一个） */
  openGenerateDialog(): void {
    const std = this.standardCode || this.treeData[0]?.Code || ''
    this.genStandardCode.value = std
    this.genClauseCodes.value = undefined
    this.genResult.value = null
    this.genItems.value = []
    this.genElapsed.value = 0
    this.genVisible.value = true
  }

  /** 只重跑当前条款 */
  openGenerateForCurrent(): void {
    const node = this.selectedNode
    if (!node || node.NodeType !== 'clause') {
      ElMessage.warning('请先在左侧选择一个具体条款')
      return
    }
    this.openGenerateDialog()
    this.genStandardCode.value = this.standardCode || this.treeData[0]?.Code || ''
    this.genClauseCodes.value = [node.Code]
  }

  /** 计时器句柄（`⛔` 不用 ref：它不参与渲染，只作清理用） */
  private genTimer: ReturnType<typeof setInterval> | null = null

  private startGenTimer(): void {
    this.stopGenTimer()
    this.genElapsed.value = 0
    this.genTimer = setInterval(() => {
      this.genElapsed.value += 1
    }, 1000)
  }

  private stopGenTimer(): void {
    if (this.genTimer !== null) {
      clearInterval(this.genTimer)
      this.genTimer = null
    }
  }

  /** 执行预生成（服务端逐条串行，期间锁按钮并显示已跑秒数） */
  async runGenerate(): Promise<void> {
    if (!this.genStandardCode.value) {
      ElMessage.warning('请先选择标准')
      return
    }
    if (this.genRunning.value) return
    const count = Math.max(1, Math.min(Number(this.genCount.value) || 5, 50))
    this.genRunning.value = true
    this.genResult.value = null
    this.genItems.value = []
    this.startGenTimer()
    try {
      const result = await generateClauseAttrs({
        StandardCode: this.genStandardCode.value,
        MaxCount: count,
        Overwrite: this.genOverwrite.value,
        ClauseCodes: this.genClauseCodes.value,
      })
      this.genResult.value = result
      this.genItems.value = result.Items ?? []
      if (result.Failed > 0) ElMessage.warning(result.Message)
      else ElMessage.success(result.Message)
      await this.loadSheet()
    } catch (e) {
      this.reportError(e, 'AI 预生成失败')
    } finally {
      this.stopGenTimer()
      this.genRunning.value = false
    }
  }

  /** 结果表列（★ 走 `YzhTable`，守卫 `R6` 禁页面内联 `<el-table>`） */
  get genColumns() {
    return [
      { prop: 'ClauseNumber', label: '条款号', width: 110 },
      { prop: 'Title', label: '标题', minWidth: 180, showOverflowTooltip: true },
      { label: '结果', width: 90, slotKey: 'status', slot: true },
      { prop: 'Message', label: '说明', minWidth: 160, showOverflowTooltip: true },
    ]
  }

  constructor() {
    super()
    this.registerHandler('generate', () => {
      this.openGenerateDialog()
    })
  }
}

export default ClauseAttrLogic
