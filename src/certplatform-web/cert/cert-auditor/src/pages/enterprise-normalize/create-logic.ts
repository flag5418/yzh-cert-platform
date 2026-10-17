/**
 * 企业资料规范化 · **向导逻辑**（专家端 · 路由 `/enterprise-normalize/create`）
 *
 * 规格：`docs/20-体系认证/03-详细设计/05-企业资料规范化/{54,55,60}`
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户裁决：`/enterprise-normalize` **拆成两个页面**
 *
 * 用户原话：「这个页面，我发现了，应该将该页面做成 2 个，第一个是企业资料规范化，
 * 第二个是企业资料审核」＋「第一个页面我们按 `/tasks` 进行向导型的进行任务引导，
 * 核心是开启任务」。
 *
 * ⇒ 本文件是**向导页的 Logic**，旧 `logic.ts` 里与「开任务」无关的东西全部剥掉：
 *   ⛔ 左树（改由第 1 步的「企业 / 阶段」下拉承载）
 *   ⛔ 单文件同步执行（`立即跑` / `fill-one`）—— 用户点名的「奇怪按钮」
 *   ⛔ 批次轮询 + 取消（向导开完任务就回列表，进度与取消归**任务列表页**）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 四步（`tasks/create.vue` 是版式样板）
 *
 *   ① 选择企业 + 阶段
 *   ② 选择文档（整体 / 部分）
 *   ③ 预览将做什么（干跑，零副作用）
 *   ④ 确认并开始规范化 → 入队 → **回任务列表**
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 三条显示口径（本轮用户裁决收严，覆盖 2026-10-07 的旧口径）
 *
 * 用户原话：「我们当前就不用理会所有的标准文档，我们只考虑标准文档中已经设置规则的，
 * 可进行选择的也是又填入规则的标准文档，导出的也是已经规范化的文档」。
 *
 * ① **标准 Tab 只列「有可规范化文件」的标准** —— 空标准不再占一个 Tab。
 * ② **文件夹树剪掉空文件夹**（递归）—— 一个可规范化文件都没有的文件夹不出现。
 *    ⚠️ 旧版是「全显示 + 带『可规范化 N / 共 M 个文件』两个计数」，
 *       用户 2026-10-11 明确否掉：「我们不应该在界面上显示什么是标准化文件数量，
 *       哪些是已经定义了填写规则的文档数量，这样，让审核员非常奇怪，不理解」。
 * ③ **标准名一律走 `standardLabel()`**，⛔ 绝不显示裸 GUID（2026-10-09 报障）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 为什么「干跑」仍是强制前置
 *
 * 规范化是**覆盖性**操作（重新生成会覆盖企业侧已有产物）。
 * 没有干跑，用户点「开始规范化」就是盲签 —— 尤其「已锁定 / 未配规则 / 无锚点」
 * 这三类会被静默跳过，跑完只看到数字对不上。
 * ⇒ `canRun` 要求「已预览 且 范围没变过」⇒ 改了范围必须重新预览。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 选择为什么由本类持有（而不是交给某个组件）
 *
 * 选择要**跨标准 Tab 保持**（用户可能同时勾 9001 和食品的文件，一次入队），
 * 而每个 Tab 渲染的是独立的一棵 `el-tree` ⇒ 状态必须上提到这里。
 * `selectionToken` 用于「程序化改选择后强制重挂载树」（见 `NormalizeScopeTree` 文件头注释）。
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useAsyncAction } from '@yzh-core'
import { fetchStageTree } from '@share/api/ent/enterprise-original'
import {
  fetchNormalizeTaskFiles,
  fetchNormalizeTree,
  planNormalize,
  runNormalize,
  type NormalizeFileNode,
  type NormalizeFolderNode,
  type NormalizePlanResult,
  type NormalizeRunResult,
  type NormalizeStandardNode,
} from '@share/api/ent/enterprise-normalize'

/** 下拉选项（企业 / 阶段共用形状） */
export interface WizardOption {
  Code: string
  Name: string
}

/**
 * 范围树节点（`el-tree` 的数据形状）。
 *
 * ⚠️ 字段名是 `Name` / `Children` / `Disabled`（⛔ 不是后端原样字段）——
 *    这三个是 `el-tree` 的 `props` 映射目标，改名就要同步改 `TREE_PROPS`。
 */
export interface ScopeTreeNode {
  /** ★ 唯一键：文件夹 `F:{folderCode}`｜文件 `S:{standardFileCode}` */
  Key: string
  Name: string
  Type: 'folder' | 'file'
  /** 文件夹：无可规范化文件 ⇒ true（灰显且不可勾选）—— 剪枝后理论上不会出现，留作兜底 */
  Disabled: boolean
  /** 文件夹：可规范化文件数（含子文件夹）—— **仅供剪枝**，⛔ 不在界面显示 */
  FillableCount: number
  /** 文件节点专属 */
  File?: NormalizeFileNode
  Children?: ScopeTreeNode[]
}

/** 范围口径：整体（该阶段全部可规范化文档）/ 部分（勾选） */
export type ScopeMode = 'all' | 'partial'

// ════════════════════════════════════════════════════════════════════════
// 范围树 → el-tree 节点（纯函数，⛔ 不碰状态）
// ════════════════════════════════════════════════════════════════════════

function toFileNode(f: NormalizeFileNode): ScopeTreeNode {
  return {
    Key: `S:${f.StandardFileCode}`,
    Name: f.FileName,
    Type: 'file',
    Disabled: false,
    FillableCount: 0,
    File: f,
  }
}

/**
 * 文件夹 → 节点。
 *
 * ★★ 返回 `null` = **剪掉这个文件夹**（用户 2026-10-11 裁决：只呈现已配规则的文档）。
 *    判据 = 剪枝后的子节点为空 —— 子树里一个可规范化文件都没有，这个文件夹
 *    对「选择要规范化哪些文档」这件事没有任何信息量，留着只会让用户以为系统丢了数据。
 */
function toFolderNode(fo: NormalizeFolderNode): ScopeTreeNode | null {
  const children: ScopeTreeNode[] = [
    ...(fo.Children ?? [])
      .map(toFolderNode)
      .filter((x): x is ScopeTreeNode => x !== null),
    ...(fo.Files ?? []).map(toFileNode),
  ]
  if (children.length === 0) return null
  return {
    Key: `F:${fo.FolderCode}`,
    Name: fo.FolderName,
    Type: 'folder',
    Disabled: false,
    FillableCount: fo.FillableCount,
    Children: children,
  }
}

/** 递归取一个标准下的**全部可规范化文件**（含根级与各级子文件夹） */
function collectFiles(s: NormalizeStandardNode | null): NormalizeFileNode[] {
  if (!s) return []
  const out: NormalizeFileNode[] = [...(s.RootFiles ?? [])]
  const walk = (folders: NormalizeFolderNode[]): void => {
    for (const fo of folders ?? []) {
      out.push(...(fo.Files ?? []))
      walk(fo.Children ?? [])
    }
  }
  walk(s.Folders ?? [])
  return out
}

export class NormalizeCreateLogic {
  // ══════════════ 一、向导步骤 ══════════════

  /** 0 = 选企业阶段｜1 = 选文档｜2 = 预览｜3 = 确认并开始 */
  step = ref(0)

  // ══════════════ 二、第 1 步：企业 / 阶段 ══════════════
  //
  // ★ 数据源 = `/api/Auditor/EnterpriseOriginal/stage-tree`（⛔ 不复制第二份）
  //   一次调用同时拿到「企业列表」与「每个企业的阶段列表」，避免两个接口的时序问题。

  enterprises = ref<WizardOption[]>([])
  /** 当前企业下的阶段（切换企业时重填） */
  stages = ref<WizardOption[]>([])
  enterpriseLoading = ref(false)

  enterpriseCode = ref('')
  stageCode = ref('')

  /** ★ 阶段加载失败 / 该企业没有阶段时的**持久**提示（一次性 toast 会丢掉「该去哪儿修」） */
  stageHint = ref('')

  /** 缓存原始树 —— 切企业时不必再打一次接口 */
  private stageTreeCache: Array<{
    Code: string
    Name: string
    Children: WizardOption[]
  }> = []

  /** 第 1 步是否可继续 */
  canGoStep2 = computed(() => !!this.enterpriseCode.value && !!this.stageCode.value)

  /** 作用域是否已选到「阶段」这一级（范围加载的前置条件） */
  scopeReady = computed(() => !!this.enterpriseCode.value && !!this.stageCode.value)

  scopeLabel = computed(() => {
    const e = this.enterprises.value.find((x) => x.Code === this.enterpriseCode.value)?.Name ?? ''
    const s = this.stages.value.find((x) => x.Code === this.stageCode.value)?.Name ?? ''
    return e && s ? `${e} › ${s}` : ''
  })

  // ══════════════ 三、第 2 步：规范化范围（标准 › 文件夹 › 文件） ══════════════

  /** 后端返回的原始标准节点（含空标准 —— 由 `standardTabs` 过滤） */
  standards = ref<NormalizeStandardNode[]>([])

  loading = ref(false)
  /** 是否已经真正查过一次（用于区分「还没选阶段」与「查了但是空」） */
  loaded = ref(false)
  /**
   * ★ 范围是否以**业务拒绝**告终（`success=false`）。
   *
   * · `true`  = 后端**没能执行**这次查询 —— 例如全库还没有任何已发布的空白模板
   * · `false` = 后端执行成功，只是这个范围里没有可规范化的文档
   *
   * 用途：空态标题要说对话 —— 前置未就绪时写「这个阶段下没有可规范化的文档」是错的
   * （问题不在阶段，在后台还没发布模板）。
   */
  listBlocked = ref(false)
  /** 业务拒绝原因（⛔ 不是「空集」的说明 —— 空集走空态文案） */
  listMessage = ref('')

  /**
   * 范围口径：整体 / 部分。
   *
   * ★★ **它只是「一键全选」的 UI 便利，⛔ 不参与请求口径** ——
   *   真正跑什么，只看 `checkedCodes`（单一真源）。
   *   为什么不让它另算一套：两套口径必然漂移 ——
   *   用户选「整体」后手取消一个，若请求仍按「整体」算，界面说 5 个、实际跑 6 个，
   *   而且**两边都不报错**。所以选「整体」= 自动全勾；用户一改勾选就自动退回「部分」。
   */
  scopeMode = ref<ScopeMode>('all')

  /**
   * ★★ 界面真正渲染的标准 Tab —— **只列有可规范化文件的标准**。
   *
   * 用户 2026-10-11 裁决：「我们只考虑标准文档中已经设置规则的」⇒
   * 空标准（`FillableCount = 0`）⛔ 不再占一个 Tab（旧版全显示 + 徽标 + 说明条，
   * 用户判定为「让审核员非常奇怪，不理解」）。
   */
  standardTabs = computed(() => this.standards.value.filter((s) => (s.FillableCount ?? 0) > 0))

  /** 当前选中的标准 Tab（= `NormalizeStandardNode.StandardCode`） */
  activeStandardCode = ref('')

  activeStandard = computed<NormalizeStandardNode | null>(
    () => this.standardTabs.value.find((s) => s.StandardCode === this.activeStandardCode.value) ?? null,
  )

  /** 当前 Tab 的 `el-tree` 数据（空文件夹已剪掉） */
  activeTreeNodes = computed<ScopeTreeNode[]>(() => {
    const s = this.activeStandard.value
    if (!s) return []
    return [
      ...(s.Folders ?? [])
        .map(toFolderNode)
        .filter((x): x is ScopeTreeNode => x !== null),
      ...(s.RootFiles ?? []).map(toFileNode),
    ]
  })

  /** 当前 Tab 的空态文案 */
  activeEmptyText = computed(() => {
    const s = this.activeStandard.value
    if (!s) return '请先选择标准'
    return '该标准下暂无可规范化的文档'
  })

  // ══════════════ 四、选择（★ 跨 Tab 保持） ══════════════

  /** 已勾选的标准域行 Code（只含文件） */
  checkedCodes = ref<string[]>([])
  /** ★ 程序化改选择后 +1 ⇒ 换 `el-tree` 的 key 强制重挂载（`default-checked-keys` 才会生效） */
  selectionToken = ref(0)

  /** 本阶段全部可规范化文件（「全选本阶段」与「整体」口径共用） */
  allFillableCodes = computed<string[]>(() =>
    this.standardTabs.value.flatMap((s) => collectFiles(s).map((f) => f.StandardFileCode)),
  )

  /**
   * ★ **本次将要规范化的文件清单** —— 干跑与入队的**唯一口径**。
   *
   * ⛔ 不要在这里按 `scopeMode` 分支：「整体」是「全勾」的**结果**，不是另一套算法
   *   （见 `scopeMode` 的注释）。
   */
  effectiveCodes = computed<string[]>(() => this.checkedCodes.value)

  effectiveCount = computed(() => this.effectiveCodes.value.length)

  /**
   * ★ 当前 Tab 已勾选的文件数（仅「部分」模式有意义）。
   *
   * 用途：选择是**跨标准**的（一次可勾多个标准），但用户眼里只有当前 Tab ⇒
   * 当「已选总数 ≠ 当前标准已选」时必须**说破**，否则会看到
   * 「这个标准的树是空的，底部却说已选 3 个」——前后矛盾。
   */
  selectedInActiveStandard = computed(() => {
    const inActive = new Set(collectFiles(this.activeStandard.value).map((f) => f.StandardFileCode))
    return this.checkedCodes.value.filter((c) => inActive.has(c)).length
  })

  // ══════════════ 五、第 3 步：干跑 / 第 4 步：入队 ══════════════

  planResult = ref<NormalizePlanResult | null>(null)
  /** ★ 范围变过 ⇒ 上次干跑作废（否则会拿旧结论去执行新范围 —— 静默错配） */
  planStale = ref(false)

  /**
   * ★ 干跑预览（2026-10-16）—— 走 `useAsyncAction`：并发计数 + 全局等待提示。
   *
   * 【为什么这个要接】干跑要按范围把「模板 / 文件 / 已填值」全算一遍（多表 JOIN），
   *   页面此前只在预览卡里显示一行转圈；用户若在第 1→2 步自动干跑期间切走，
   *   回来时**看不出系统还在算**。
   * 【为什么不能用单 boolean】`planning` 之前是 `ref(false)`，而干跑有**两个触发点**
   *   （`nextStep()` 进第 3 步自动跑 + 卡片上「重新预览」按钮）⇒ 交叠时先结束的那次
   *   `finally` 会把它置回 `false` ⇒ 「正在计算」的提示提前消失、按钮提前可点。
   *
   * ⚠️ `useAsyncAction` 必须在 **setup 同步期**调用（内部要 `inject` 路由对象）
   *   ⇒ 放在类字段初始化里（`new NormalizeCreateLogic()` 发生在页面 setup 中）。
   */
  private planAction = useAsyncAction()

  /** 干跑进行中（模板按 `logic.planning.value` 读，`ComputedRef` 与 `ref` 用法一致） */
  planning = this.planAction.busy
  /** ★ 入队 = 队列任务（进度归 `/enterprise-normalize` 列表），按裁决⛔ 不接全局遮罩 */
  running = ref(false)

  /** ★ 只有「已预览 且 范围没变过 且 有可执行项」才允许真跑 */
  canRun = computed(
    () => !!this.planResult.value && !this.planStale.value && (this.planResult.value.Queued ?? 0) > 0,
  )

  // ══════════════ 六、初始化 ══════════════

  async init(): Promise<void> {
    await this.loadEnterprises()
  }

  /**
   * 加载「企业 › 阶段」树。
   *
   * ★ 树节点形状见 `@share/api/ent/enterprise-original` 的 `TreeNode`：
   *   顶层 = 企业（`IsEnterpriseNode: true`，`Code` = 企业 Code，**`Label` = 名称**），
   *   `Children` = 阶段（`Code` = `cert_cert_stage.Code`，⛔ 不是 slug）。
   *   ⚠️ 该类型的名称字段是 **`Label`**（⛔ 没有 `Name`）—— 写成 `n.Name` 编译期就报错。
   */
  async loadEnterprises(): Promise<void> {
    this.enterpriseLoading.value = true
    try {
      const nodes = await fetchStageTree()
      this.stageTreeCache = (nodes ?? []).map((n) => ({
        Code: String(n.Code ?? ''),
        Name: String(n.Label ?? ''),
        Children: ((n.Children as typeof nodes) ?? []).map((c) => ({
          Code: String(c.Code ?? ''),
          Name: String(c.Label ?? ''),
        })),
      }))
      this.enterprises.value = this.stageTreeCache.map(({ Code, Name }) => ({ Code, Name }))
    } catch (e) {
      this.stageTreeCache = []
      this.enterprises.value = []
      ElMessage.error((e as Error)?.message || '加载企业列表失败')
    } finally {
      this.enterpriseLoading.value = false
    }
  }

  /** 换企业：清阶段 + 清作用域 + 清范围 */
  onEnterpriseChange(): void {
    this.stageCode.value = ''
    const hit = this.stageTreeCache.find((x) => x.Code === this.enterpriseCode.value)
    this.stages.value = hit?.Children ?? []
    this.stageHint.value =
      hit && hit.Children.length === 0 ? '该企业还没有阶段档案，请先到「企业阶段」建档。' : ''
    this.resetScope()
  }

  /** 换阶段：范围与选择全部作废（否则会拿 A 阶段的选择去跑 B 阶段） */
  async onStageChange(): Promise<void> {
    this.resetScope()
    await this.loadRange()
  }

  private resetScope(): void {
    this.standards.value = []
    this.activeStandardCode.value = ''
    this.checkedCodes.value = []
    this.selectionToken.value += 1
    this.planResult.value = null
    this.planStale.value = true
    this.listMessage.value = ''
    this.listBlocked.value = false
    this.loaded.value = false
  }

  // ══════════════ 七、范围树 ══════════════

  /**
   * 加载范围树（标准 › 文件夹 › 文件）。
   *
   * ★ 驱动源 = `cert_doc_template`（**已发布**），⛔ 不是全部标准文件。
   *
   * ★★ 2026-10-06 用户裁决：后端在「全库没有已发布模板」时返回 `success=false` + `err`
   *   （业务拒绝），⛔ 不是 `success=true` + `data.message`（那是把错误藏在成功里）。
   *   ⇒ 本方法 catch 分支要**接住**这条业务拒绝：① 弹红 ② **同时**写进 `listMessage`
   *   让页面**持久**展示原因（toast 三秒就没了，而用户需要照着这句话去后台发布模板）。
   */
  async loadRange(): Promise<void> {
    if (!this.scopeReady.value) return
    this.loading.value = true
    try {
      const r = await fetchNormalizeTree(this.enterpriseCode.value, this.stageCode.value)
      const list = r.standards ?? []
      this.standards.value = list
      this.listMessage.value = ''
      this.listBlocked.value = false
      this.loaded.value = true

      // ★ 保持当前 Tab（刷新后停在原地）；它没了才回落到「第一个有可规范化文件的标准」
      const stillThere = this.standardTabs.value.some(
        (s) => s.StandardCode === this.activeStandardCode.value,
      )
      if (!stillThere) {
        this.activeStandardCode.value = this.standardTabs.value[0]?.StandardCode ?? ''
      }

      // ★ 剪掉已消失的勾选（刷新后模板可能被取消发布 ⇒ 那些文件不该留在选择里）
      const alive = new Set(this.allFillableCodes.value)
      const pruned = this.checkedCodes.value.filter((c) => alive.has(c))
      if (pruned.length !== this.checkedCodes.value.length) {
        this.checkedCodes.value = pruned
        this.selectionToken.value += 1
        this.planStale.value = true
      }
    } catch (e) {
      const msg = (e as Error).message
      this.standards.value = []
      this.activeStandardCode.value = ''
      // ★ 持久展示（页面空态会渲染它）—— 一次性 toast 会丢掉「该去哪儿修」这条信息
      this.listMessage.value = msg
      this.listBlocked.value = true
      this.loaded.value = true
      ElMessage.error(msg)
    } finally {
      this.loading.value = false
    }
  }

  // ══════════════ 八、选择 ══════════════

  /**
   * `el-tree` 勾选变化。
   *
   * ⚠️ 组件回传的只是**当前 Tab 这一棵树**里的勾选（`getCheckedKeys(true)` 的叶子）。
   *    所以这里必须**按当前标准做「局部替换」**，⛔ **不能整体覆盖** ——
   *    选择是跨标准 Tab 保持的（用户可能同时勾 9001 与食品的文件、一次入队），
   *    整体覆盖会把「另一个标准已勾的」静默冲掉：
   *    在 9001 勾 A → 切食品勾 B ⇒ `checkedCodes` 变成 `['B']`，**A 丢了**。
   *
   * 判据：`新的 = 不属于当前标准的旧勾选（保留） + 当前标准的新勾选`
   */
  onCheckedChange(codes: string[]): void {
    const inActive = new Set(
      collectFiles(this.activeStandard.value).map((f) => f.StandardFileCode),
    )
    const kept = this.checkedCodes.value.filter((c) => !inActive.has(c))
    this.checkedCodes.value = [...kept, ...codes]
    this.syncScopeModeWithSelection()
    this.markScopeDirty()
  }

  /** ★ 全选本阶段（跨全部标准 Tab 的可规范化文件） */
  selectAllStage(): void {
    this.checkedCodes.value = [...this.allFillableCodes.value]
    this.selectionToken.value += 1
    this.markScopeDirty()
  }

  clearSelection(): void {
    this.checkedCodes.value = []
    this.selectionToken.value += 1
    this.syncScopeModeWithSelection()
    this.markScopeDirty()
  }

  /**
   * 切换「整体 / 部分」。
   *
   * ★ 「整体」= 把当前全部可规范化文档**勾上**（⛔ 不是另开一套请求口径）；
   *   「部分」= 不动勾选，交给用户自己调。
   */
  setScopeMode(mode: ScopeMode): void {
    if (mode === 'all') {
      this.scopeMode.value = 'all'
      this.selectAllStage()
      return
    }
    this.scopeMode.value = 'partial'
  }

  /**
   * 勾选一变，就把口径标签**拉回与事实一致**。
   *
   * ⛔ 不能只改勾选不改标签：用户选了「整体」再取消一个，
   *   标签还写着「整体」⇒ 界面在骗人（而且没有任何报错）。
   */
  private syncScopeModeWithSelection(): void {
    if (this.scopeMode.value !== 'all') return
    if (this.checkedCodes.value.length !== this.allFillableCodes.value.length) {
      this.scopeMode.value = 'partial'
    }
  }

  /** 范围一变：上次干跑作废 */
  private markScopeDirty(): void {
    this.planStale.value = true
    this.planResult.value = null
  }

  // ══════════════ 九、干跑 / 入队 ══════════════

  private buildRequest() {
    return {
      EnterpriseCode: this.enterpriseCode.value,
      StageCode: this.stageCode.value,
      StandardFileCodes: [...this.effectiveCodes.value],
    }
  }

  /**
   * ★ **干跑预览** —— 回答「选中这批文件，真跑会发生什么」。
   * ⛔ 零副作用：不写库 / 不产文件 / 不调 LLM / 不入队。
   *
   * @returns 是否成功拿到预览（页面据此决定要不要进第 4 步）
   */
  async previewPlan(): Promise<boolean> {
    if (!this.scopeReady.value) {
      ElMessage.warning('请先选择企业与阶段')
      return false
    }
    if (this.effectiveCount.value === 0) {
      ElMessage.warning('当前范围里没有可规范化的文档')
      return false
    }

    return await this.planAction
      .run(async () => {
        const r = await planNormalize(this.buildRequest())
        this.planResult.value = r
        this.planStale.value = false
      }, '正在计算「将要做什么」（只读，不改动任何数据）')
      .then(() => true)
      .catch((e: unknown) => {
        this.planResult.value = null
        this.planStale.value = true
        ElMessage.error((e as Error).message)
        return false
      })
  }

  /**
   * ★ **开始规范化（整批入队）**。
   *
   * ⚠️ 服务端会**按当前事实重算一遍范围**（⛔ 不信任前端清单）：页面可能已停留很久，
   *   期间模板可能被取消发布、文件可能被锁定 ⇒ 若重算后无可执行项，后端返回**业务拒绝**。
   *
   * @returns 入队成功时返回批次号，否则 null（页面据此决定要不要回列表）
   */
  async executeRun(): Promise<string | null> {
    if (!this.canRun.value) {
      ElMessage.warning('请先点「预览将做什么」，确认预览结果后再执行')
      return null
    }

    this.running.value = true
    try {
      const r: NormalizeRunResult = await runNormalize(this.buildRequest())
      ElMessage.success(`已开启规范化任务（批次 ${r.QueueCode}，共 ${r.Queued} 个文档）`)
      // ★ 已入队 ⇒ 干跑预览立即作废（避免用户回退时看到过期结论）
      this.planResult.value = null
      this.planStale.value = true
      return r.QueueCode
    } catch (e) {
      ElMessage.error((e as Error).message)
      return null
    } finally {
      this.running.value = false
    }
  }

  // ══════════════ 十、「针对历史任务重新执行」预填 ══════════════

  /**
   * 按历史任务预填企业 / 阶段 / 已选文档。
   *
   * ★ 用户裁决（2026-10-11）：「第一个开启的时候，也可以开启一个全新任务，
   *   或针对历史任务，重新执行」⇒ 列表行按钮「重新执行」带 `?from={queueCode}` 进来。
   *
   * ⚠️ 文档清单**以当前事实为准**：`loadRange()` 会把「模板已取消发布 / 已删除」的行
   *    从勾选里剪掉（见 `loadRange` 的 prune 分支）⇒ 预填不会带进跑不了的行。
   *
   * @returns 是否预填成功
   */
  async prefillFromTask(queueCode: string): Promise<boolean> {
    const files = await fetchNormalizeTaskFiles(queueCode)
    if (!files.EnterpriseCode || !files.StageCode) return false

    this.enterpriseCode.value = files.EnterpriseCode
    const hit = this.stageTreeCache.find((x) => x.Code === files.EnterpriseCode)
    this.stages.value = hit?.Children ?? []
    this.stageCode.value = files.StageCode
    this.stageHint.value = ''

    this.resetScope()
    await this.loadRange()

    // ★ 只把「本次确实产出了文档」的行带进勾选（失败 / 未跑的行没意义）
    const wanted = new Set(
      files.Rows.filter((r) => !!r.OutputPath).map((r) => r.StandardFileCode),
    )
    const alive = new Set(this.allFillableCodes.value)
    const picked = [...wanted].filter((c) => alive.has(c))
    this.scopeMode.value = 'partial'
    this.checkedCodes.value = picked
    this.selectionToken.value += 1
    this.planStale.value = true

    if (picked.length === 0) {
      // ⛔ 不静默：预填失败要说明原因，否则用户以为「重新执行」坏了
      ElMessage.info(
        '原任务的文档在当前已不再可规范化（模板可能已取消发布），已改为「整体」范围，请重新选择。',
      )
      // ★ 走 setScopeMode（= 顺带全勾）—— ⛔ 不要只改标签，否则标签说「整体」而勾选是空的
      this.setScopeMode('all')
    }
    return true
  }

  // ══════════════ 十一、缓存复用时的复位 ══════════════

  /**
   * 把向导复位到第 1 步。
   *
   * ⚠️ 为什么需要它：`AuditorLayout` 用 `<keep-alive>` 缓存路由组件 ⇒ 第二次进入
   *    `/enterprise-normalize/create` 时 `onMounted` ⛔ 不再触发，页面上会**残留上次
   *    填的企业 / 阶段 / 勾选**（用户会以为「上次那个任务还在跑」）。
   *    同 `pages/tasks/create.vue` 的 `resetWizard`。
   */
  resetWizard(): void {
    this.step.value = 0
    this.enterpriseCode.value = ''
    this.stageCode.value = ''
    this.stages.value = []
    this.stageHint.value = ''
    this.scopeMode.value = 'all'
    this.resetScope()
  }
}

export default NormalizeCreateLogic
