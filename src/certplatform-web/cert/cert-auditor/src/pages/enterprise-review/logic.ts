/**
 * 企业资料审核 · 页面逻辑（专家端 · 路由 `/enterprise-review`）
 *
 * 规格：`docs/20-体系认证/03-详细设计/05-企业资料规范化/{54,55,60}`
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户裁决：本页是「第二个页面」——**企业资料审核**
 *
 * 用户原话：「第二个页面是左树右表，树的结构，企业 + 阶段 + 任务，
 * 右侧是勾选的所有的标准化文档，可以针对已经完成标准文档规范的进行审核」＋
 * 「可以按企业的阶段，统一导出所有的文档」。
 *
 * ⇒ 左树三级：企业 › 阶段 › 任务（`yzh_queue` 一条 `enterprise_normalize` 批次 = 一个任务）
 *   右表：当前选中范围内的**已规范化文档**
 *   导出：把当前范围内的产物打成 ZIP（⛔ 只导**已规范化**的，见 `packageNormalize`）
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 树为什么要**前端拼**（没有 `tree` 端点）
 *
 * 后端只有扁平的 `GET tasks`（一行 = 一个批次）。三级结构是**同一个字段的两次分组**
 * （企业 → 阶段 → 任务），没有任何额外事实 ⇒ 前端 `groupBy` 两趟即可，
 * ⛔ 不为它新增一个端点（多一个端点 = 多一处口径要同步）。
 *
 * ⚠️ `GET tasks` 单页上限 200 行（后端 `TaskListMaxRows`）—— 超出的批次在树里看不到。
 *    本页**显式提示**这件事（⛔ 不静默截断，否则用户以为「任务丢了」）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 「阶段」这一级的右表口径 = 该阶段**全部批次**的文档并集
 *
 * 用户要的是「按企业的阶段统一看/统一导出」。而同一个阶段历史上有多个批次
 * （每次重跑一个），每个批次各自的文档清单会重叠 ⇒
 * 按 `StandardFileCode` **去重、取 `FillTime` 最新的那一条**（= 该文档的最后一次结果）。
 */
import { computed, ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  fetchNormalizeTaskFiles,
  fetchNormalizeTasks,
  packageNormalize,
  TASK_STATUS_TEXT,
  type NormalizeTaskFileRow,
  type NormalizeTaskRow,
} from '@share/api/ent/enterprise-normalize'
// ★ 右表树形构建 = 纯函数模块（⛔ 本文件带 vue / element-plus，node 里跑不动，
//   树逻辑留在本文件就等于「只能靠肉眼看」，见 `tree.ts` 头部说明）
import { buildFileTree, type ReviewFileTreeNode } from './tree'

export { buildFileTree }
export type { ReviewFileTreeNode }

/** 树节点层级 */
export type ReviewLevel = 'enterprise' | 'stage' | 'task'

/**
 * 左树节点。
 *
 * ⚠️ 字段名必须叫 **`Code` / `Name` / `Children`** —— `YzhTree` 的 `YzhTreeNode`
 *   契约要求 `Code` + `Name` 两个必填字段，`labelField` 默认就是 `Name`。
 *   ⛔ 不要自己起 `Label`（那要靠 `label-field="Label"` 绕，且类型上过不去）。
 */
export interface ReviewTreeNode {
  /** ★ 唯一键（带层级前缀，避免跨层级撞 key） */
  Code: string
  Name: string
  Level: ReviewLevel
  /** 该节点向上归属（企业 / 阶段两级恒定有值） */
  EnterpriseCode: string
  EnterpriseName: string
  StageCode: string
  StageName: string
  /** 仅任务级有值 */
  QueueCode: string
  /** 仅任务级有值（`yzh_queue.Status`） */
  Status: string
  /**
   * `YzhTree` 的扩展位（**直接读节点数据，⛔ 无需透传 prop**）。
   *
   * ★ 只用 `badge`：它在节点行里**右对齐且 `flex-shrink: 0`** ⇒ **永不被截断**，
   *   所以适合放「状态」这种一旦被截断就没意义的信息。
   * ⚠️ `badge` 只有**小写**这一个 key（`YzhTree` 的 `nodeExtra(data).badge`
   *   不做 PascalCase 兜底，与 `Icon` 的双 Key 规则不同）—— 写成 `Badge` 会静默不显示。
   * ⛔ 不要往这里塞 `IsValid`：`statusField` 默认就认它，会多出一个「启用/禁用」徽标。
   */
  Extra?: Record<string, any>
  Children?: ReviewTreeNode[]
}

/** 当前选中范围 */
export interface ReviewSelection {
  level: ReviewLevel
  EnterpriseCode: string
  EnterpriseName: string
  StageCode: string
  StageName: string
  /** 仅任务级 */
  QueueCode: string
  /** ★ 任务级的人话标签（= 树节点 `Name`），右表范围条用它，⛔ 不再显示 QueueCode */
  TaskName: string
}

/** 树里最多拼多少个批次（对齐后端 `TaskListMaxRows`） */
const TREE_MAX_TASKS = 200

/** 「阶段」级右表最多聚合多少个批次的文档（防止历史批次很多时打一串请求） */
const STAGE_AGG_MAX_TASKS = 20

export class EnterpriseReviewLogic {
  // ══════════════ 一、左树（企业 › 阶段 › 任务） ══════════════

  treeNodes = ref<ReviewTreeNode[]>([])
  treeLoading = ref(false)
  /** 树被截断 / 拉取失败时的**持久**提示（一次性 toast 会丢掉原因） */
  treeHint = ref('')

  // ══════════════ 二、当前选中范围 ══════════════

  selection = ref<ReviewSelection | null>(null)

  /** 范围标题（右表头部显示「在看什么」） */
  selectionLabel = computed(() => {
    const s = this.selection.value
    if (!s) return ''
    const base = `${s.EnterpriseName} › ${s.StageName}`
    // ★ 任务级用**人话标签**（时间 + 任务名），⛔ 不显示 QueueCode —— 编码对审核员无意义
    return s.level === 'task' && s.TaskName ? `${base} › ${s.TaskName}` : base
  })

  /** 是否已选到可看文档的层级（企业级只是分组，没有直接文档） */
  canShowFiles = computed(
    () => this.selection.value?.level === 'stage' || this.selection.value?.level === 'task',
  )

  // ══════════════ 三、右表（已规范化文档） ══════════════

  fileRows = ref<NormalizeTaskFileRow[]>([])
  fileLoading = ref(false)
  /** 聚合了哪些批次（阶段级；任务级只有 1 个）—— 页面据此说明「这份清单从哪来」 */
  aggregatedQueueCodes = ref<string[]>([])
  /** 因上限而**未**参与聚合的批次数（>0 ⇒ 页面必须提示，⛔ 不静默） */
  skippedQueueCount = ref(0)

  /** 已产出产物的行（= 可审核 / 可导出的对象） */
  filledRows = computed(() => this.fileRows.value.filter((r) => !!r.OutputPath))

  /**
   * ★ 右表实际渲染的数据 = 把 `fileRows` 按目录层次折成树。
   *
   * ⛔ **导出仍用 `filledRows`（平铺）** —— 导出清单只认文档，⛔ 不能把文件夹
   *   节点混进去（后端按 `StandardFileCodes` 过滤，文件夹没有这个字段，
   *   混进去只会让后端多一次无效比对）。
   */
  treeRows = computed<ReviewFileTreeNode[]>(() => buildFileTree(this.fileRows.value))

  // ══════════════ 四、导出 ══════════════

  exporting = ref(false)

  // ══════════════════════════════════════════════════════════════════════
  // 初始化
  // ══════════════════════════════════════════════════════════════════════

  async init(queueCode?: string): Promise<ReviewTreeNode | null> {
    await this.loadTree()
    if (!queueCode) return null
    return await this.locateTask(queueCode)
  }

  /**
   * ★★ 2026-10-11 复核缺陷修复：把「审核」跳转带来的 `?queueCode=` 真正用起来。
   *
   * 【原来的错】`enterprise-normalize/logic.ts` 的行按钮早就写了
   *   `router.push({ name: 'EnterpriseReview', query: { queueCode } })`，
   *   注释也写着「带任务号定位」—— 但本页**从来没读过 `route.query`**
   *   （全目录搜不到 `useRoute`）⇒ 用户点完「审核」落在一张**未选中的空页**上，
   *   得从最多 200 条任务的树里自己再找一遍那个任务。
   *   路由与注释都承诺了、实现没接上 —— 这种「承诺在注释里」的缺口最容易长期没人发现。
   *
   * @returns 命中的任务节点（页面据此 `setCurrentNode` 高亮）；没命中返回 `null`
   */
  async locateTask(queueCode: string): Promise<ReviewTreeNode | null> {
    const want = queueCode.trim()
    if (!want) return null

    for (const ent of this.treeNodes.value) {
      for (const stage of ent.Children ?? []) {
        const hit = (stage.Children ?? []).find((t) => t.QueueCode === want)
        if (hit) {
          await this.onNodeClick(hit)
          return hit
        }
      }
    }

    // ⚠️ 找不到**必须说破**：树最多拼 `TREE_MAX_TASKS` 条，任务号完全可能落在范围外。
    //    ⛔ 静默不选 = 用户以为「点了审核没反应」，比报错更难查。
    this.treeHint.value =
      `没有找到任务 ${want} —— 它可能不在最近 ${TREE_MAX_TASKS} 个任务里，` +
      '请到「企业资料规范化」的任务列表里查看。'
    ElMessage.warning(this.treeHint.value)
    return null
  }

  /**
   * 拉全部批次并拼成「企业 › 阶段 › 任务」三级树。
   *
   * ⚠️ 三级 key 都必须唯一：企业 / 阶段可能同名（不同企业下的同名阶段），
   *   任务号本身唯一 ⇒ 分别加 `E:` / `ST:` / `T:` 前缀。
   */
  async loadTree(): Promise<void> {
    this.treeLoading.value = true
    try {
      const res = await fetchNormalizeTasks({ page: 1, rows: TREE_MAX_TASKS })
      const tasks: NormalizeTaskRow[] = res.Rows ?? []

      const entMap = new Map<string, ReviewTreeNode>()
      for (const t of tasks) {
        const entKey = t.EnterpriseCode || '?'
        let ent = entMap.get(entKey)
        if (!ent) {
          ent = {
            Code: `E:${entKey}`,
            Name: t.EnterpriseName || '未登记企业',
            Level: 'enterprise',
            EnterpriseCode: t.EnterpriseCode,
            EnterpriseName: t.EnterpriseName || '未登记企业',
            StageCode: '',
            StageName: '',
            QueueCode: '',
            Status: '',
            Children: [],
          }
          entMap.set(entKey, ent)
        }

        const stageKey = `${entKey}|${t.StageCode || '?'}`
        let stage = ent.Children!.find((c) => c.Code === `ST:${stageKey}`)
        if (!stage) {
          stage = {
            Code: `ST:${stageKey}`,
            Name: t.StageName || '未登记阶段',
            Level: 'stage',
            EnterpriseCode: t.EnterpriseCode,
            EnterpriseName: ent.EnterpriseName,
            StageCode: t.StageCode,
            StageName: t.StageName || '未登记阶段',
            QueueCode: '',
            Status: '',
            Children: [],
          }
          ent.Children!.push(stage)
        }

        stage.Children!.push({
          Code: `T:${t.QueueCode}`,
          // ★ 人话标签（时间 + 任务名）—— ⛔ 不再是 QueueCode
          Name: taskLabel(t),
          // ★ 状态走 `Extra.badge`（右对齐、永不被截断）
          Extra: { badge: TASK_STATUS_TEXT[t.Status] || '' },
          Level: 'task',
          EnterpriseCode: t.EnterpriseCode,
          EnterpriseName: ent.EnterpriseName,
          StageCode: t.StageCode,
          StageName: stage.StageName,
          QueueCode: t.QueueCode,
          Status: t.Status,
        })
      }

      // ★ 批次已按 `CreateTime desc` 返回 ⇒ 任务级天然「新的在上」；企业 / 阶段按名称排
      const nodes = [...entMap.values()].sort((a, b) => a.Name.localeCompare(b.Name, 'zh-CN'))
      for (const e of nodes) {
        e.Children = (e.Children ?? []).sort((a, b) => a.Name.localeCompare(b.Name, 'zh-CN'))
      }
      this.treeNodes.value = nodes

      this.treeHint.value =
        tasks.length >= TREE_MAX_TASKS
          ? `仅显示最近 ${TREE_MAX_TASKS} 个任务；更早的任务请到「企业资料规范化」的任务列表里查看。`
          : ''
    } catch (e) {
      this.treeNodes.value = []
      this.treeHint.value = (e as Error)?.message || '加载任务树失败'
      ElMessage.error(this.treeHint.value)
    } finally {
      this.treeLoading.value = false
    }
  }

  // ══════════════════════════════════════════════════════════════════════
  // 节点点击 → 载入右表
  // ══════════════════════════════════════════════════════════════════════

  async onNodeClick(node: ReviewTreeNode): Promise<void> {
    this.selection.value = {
      level: node.Level,
      EnterpriseCode: node.EnterpriseCode,
      EnterpriseName: node.EnterpriseName,
      StageCode: node.StageCode,
      StageName: node.StageName,
      QueueCode: node.QueueCode,
      TaskName: node.Level === 'task' ? node.Name : '',
    }
    this.fileRows.value = []
    this.aggregatedQueueCodes.value = []
    this.skippedQueueCount.value = 0

    // ★ 企业级只是分组：没有「企业级的文档」这回事 ⇒ ⛔ 不发请求，页面给明确空态
    if (node.Level === 'enterprise') return

    if (node.Level === 'task') {
      await this.loadFilesForQueues([node.QueueCode])
      return
    }

    // 阶段级：把该阶段下的批次都聚合进来（新的在前，取前 N 个）
    const all = (node.Children ?? []).map((c) => c.QueueCode)
    const picked = all.slice(0, STAGE_AGG_MAX_TASKS)
    this.skippedQueueCount.value = Math.max(0, all.length - picked.length)
    await this.loadFilesForQueues(picked)
  }

  /**
   * 载入若干批次的文档清单并**去重**。
   *
   * ★ 去重判据 = `StandardFileCode`，保留 `FillTime` 更新的那一条
   *   （= 该文档的最后一次规范化结果）。
   * ⚠️ `FillTime` 可能为 null（跑失败 / 还在跑）⇒ null 视为「最旧」。
   */
  private async loadFilesForQueues(queueCodes: string[]): Promise<void> {
    if (queueCodes.length === 0) return
    this.fileLoading.value = true
    try {
      const results = await Promise.all(
        queueCodes.map((q) => fetchNormalizeTaskFiles(q).catch(() => null)),
      )

      const byFile = new Map<string, NormalizeTaskFileRow>()
      for (const r of results) {
        for (const row of r?.Rows ?? []) {
          const prev = byFile.get(row.StandardFileCode)
          if (!prev || newer(row.FillTime, prev.FillTime)) byFile.set(row.StandardFileCode, row)
        }
      }

      // ★ 有产物的排前面（审核对象优先），其余按文档名排 —— ⛔ 不让失败行把成功行挤下去
      this.fileRows.value = [...byFile.values()].sort((a, b) => {
        const af = a.OutputPath ? 0 : 1
        const bf = b.OutputPath ? 0 : 1
        if (af !== bf) return af - bf
        return String(a.FileName ?? '').localeCompare(String(b.FileName ?? ''), 'zh-CN')
      })
      this.aggregatedQueueCodes.value = queueCodes

      // ⚠️ 某个批次查不到时 `Promise.all` 已被 catch 成 null ⇒ 必须说破「少了一批」
      const failed = results.filter((r) => r === null).length
      if (failed > 0) {
        ElMessage.warning(`${failed} 个批次的文档清单读取失败，下面的清单可能不完整`)
      }
    } catch (e) {
      this.fileRows.value = []
      ElMessage.error((e as Error)?.message || '读取文档清单失败')
    } finally {
      this.fileLoading.value = false
    }
  }

  /**
   * 重新载入当前范围的文档清单。
   *
   * ★ 用途：审核抽屉里改了账本（改值 / 钉住）之后，右表的「状态 / 待办数」
   *   可能已经变了 ⇒ 必须重拉，⛔ 不能让用户看着一份已经过时的清单。
   *   ⚠️ 复用 `aggregatedQueueCodes`（= 上次实际查过的批次），⛔ 不要重新按树算一遍 ——
   *     那会在「阶段级聚合被上限截断」时把范围悄悄改大。
   */
  async reloadFiles(): Promise<void> {
    if (this.aggregatedQueueCodes.value.length === 0) return
    await this.loadFilesForQueues(this.aggregatedQueueCodes.value)
  }

  // ══════════════════════════════════════════════════════════════════════
  // 统一导出（ZIP）
  // ══════════════════════════════════════════════════════════════════════

  /**
   * ★ 把当前范围内的**已规范化产物**打成 ZIP 下载。
   *
   * ⛔ 后端唯一判据 = `NormalizedTime IS NOT NULL`（不是「有没有 StoragePath」——
   *   企业上传的原件也有 StoragePath，会混进来）。
   *
   * ════════════════════════════════════════════════════════════════════════
   * ★★ 2026-10-11 复核缺陷修复：范围**一律由右表那份清单锁定**
   *
   * 【原来的错】任务级把 `s.StageCode` 也传给了后端，而后端 `package` 在
   *   `StageCode` 非空时会拿它去过滤**企业侧实例行**的 `StageCode`。
   *
   *   问题在于实例行是按 `(企业, StandardFileCode)` **唯一**的 —— 同一个标准文件
   *   在不同阶段跑过之后，实例行的 `StageCode` 是**最后一次跑用过的那个**（后写覆盖）。
   *
   *   ⇒ 一个「9 月跑的、阶段 A」的任务，今天又在阶段 B 跑了一次同一份文件，
   *     实例行的 `StageCode` 就变成 B 了 ⇒ 这个任务再点导出，带 A 去过滤必然**一条都匹配不到**。
   *
   *   真机实测（`Q-20261007-fc64aa205`，阶段 `29c1bcc3…`，实例行已是 `654c6c40…`）：
   *     · 带 `StageCode`   → `success:false`「该范围内还没有已规范化的文档可导出」❌
   *     · 不带 `StageCode` → 真 ZIP（16230 字节，条目 `1质量手册/附录三 程序文件清单.docx`）✅
   *   而右表明明列着这一份、导出按钮也可点 —— 正是用户最反感的「界面说一套、点击说另一套」。
   *
   * 【现在的口径】**两级都改成「传右表的清单」**：
   *   · 任务级 → `filledRows` 的 `StandardFileCode`
   *   · 阶段级 → 同样 `filledRows`（它已经是「该阶段全部批次去重后的并集」）
   *   · **`StageCode` 一律不传** —— 显式清单已经把范围锁死了，再叠一个会被覆盖的
   *     字段只会重新引入上面那个缺陷（而且两个条件叠加时，「谁更权威」也没人说得清）。
   *
   *   ⇒ 好处不只是修 bug：**「所见 = 所导」变成了结构上成立的事** ——
   *     用户看到几份就导几份，界面上的 `已规范化 N 份` 与 ZIP 里的条目数必然相等。
   *
   * ⚠️ 清单为空时**两级都要拦**（原实现只拦了任务级）：
   *   阶段级若传了空数组，后端 `picked.Count === 0` ⇒ 不加文件过滤 ⇒
   *   会把该企业**全部阶段**的产物都导出来（静默把范围放大，比报错危险得多）。
   */
  async exportZip(): Promise<void> {
    const s = this.selection.value
    if (!s || !s.EnterpriseCode) {
      ElMessage.warning('请先在左侧选择企业下的阶段或任务')
      return
    }

    // ★ 范围 = 右表里「已产出产物」的那些行（与界面上的 `已规范化 N 份` 同一个来源）
    const codes = this.filledRows.value.map((r) => r.StandardFileCode)

    if (codes.length === 0) {
      ElMessage.warning(
        s.level === 'task'
          ? '这个任务里还没有已生成的文档，没有可导出的内容'
          : '这个阶段里还没有已生成的文档，没有可导出的内容',
      )
      return
    }

    this.exporting.value = true
    try {
      const fileName = `${s.EnterpriseName}_${s.StageName}_规范化文档`
      await packageNormalize(
        {
          EnterpriseCode: s.EnterpriseCode,
          // ⛔ 恒为 null：范围已由 `StandardFileCodes` 锁定，见上方长注释
          StageCode: null,
          StandardFileCodes: codes,
        },
        fileName,
      )
      ElMessage.success('已开始下载')
    } catch (e) {
      // ★ 业务拒绝（该范围内还没有已规范化的文档）走这里 —— 后端 `err` 已是人话
      ElMessage.error((e as Error)?.message || '导出失败')
    } finally {
      this.exporting.value = false
    }
  }
}

/** `a` 是否比 `b` 更新（null 视为最旧；⛔ 不用 `new Date(x) - new Date(y)` 直接比，先挡空值） */
function newer(a?: string | null, b?: string | null): boolean {
  if (!a) return false
  if (!b) return true
  return new Date(a).getTime() > new Date(b).getTime()
}

/**
 * 任务节点的**人话标签**。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 2026-10-11 用户报障：「左侧的树应该是任务名称，光是显示任务编码，不容易理解」
 *
 * ⛔ 所以这里**不再放 `QueueCode`**（`Q-20261011-9d94066d2` 这种）。
 *
 * ════════════════════════════════════════════════════════════════════════
 * ★★ 为什么把**时间放最前**（这不是随手排的）
 *
 * 实测：同一阶段下 7 条批次的 `QueueName` **完全相同** —— 全部是
 * 「企业资料规范化 - 1 个文件」（它只编码了**文件数**，不含时间也不含阶段）。
 * ⇒ **唯一能区分这些任务的就是执行时间**，`QueueName` 单独用等于换了个编号。
 *
 * 而 `YzhTree` 的标签是 CSS **尾部**省略号（`text-overflow: ellipsis`）——
 * 把时间放在末尾，被截掉的正好是唯一有用的信息（「企业资料规范化 - 1 个…」）。
 * ⇒ 时间必须放最前，让它在任何宽度下都先出现。
 *    （完整文本仍可 hover 看 tooltip，`YzhTree` 自带。）
 *
 * ⛔ 不用 `formatDateTime`：它给 `2026/10/11 09:25:33`（19 字符），左树里放不下。
 */
function taskLabel(t: NormalizeTaskRow): string {
  const name = t.QueueName || t.QueueCode
  const when = shortTime(t.CreateTime)
  return when ? `${when} · ${name}` : name
}

/**
 * `MM-DD HH:mm`。
 *
 * ⚠️ `CreateTime` 是 **UTC**（带 `Z`），而 `StartTime`/`EndTime` 是**本地**时间
 *   （见 `NormalizeTaskRow` 上的注释）—— 本函数靠 `Date` 自己换算，只用于 UTC 那个字段。
 *   取 `getMonth/getHours` 等**本地**取值方法 ⇒ 展示的是本地时间，与用户看到的队列
 *   `StartTime` 一致（实测同一批次 `CreateTime=01:25:33Z` ↔ `StartTime=09:25:33`）。
 */
function shortTime(v?: string | null): string {
  if (!v) return ''
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return ''
  const p = (n: number) => String(n).padStart(2, '0')
  return `${p(d.getMonth() + 1)}-${p(d.getDate())} ${p(d.getHours())}:${p(d.getMinutes())}`
}

export default EnterpriseReviewLogic
