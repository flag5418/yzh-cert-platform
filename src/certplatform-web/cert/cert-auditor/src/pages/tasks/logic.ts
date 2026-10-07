/**
 * ExpertTaskLogic —— 任务中心（列表页）Logic · SingleTableCore 架构
 *
 * 后端：`CertPlatform.Auditor/Controllers/ExpertTaskController.cs`（`YzhControllerBase<CertExpertTask>`）
 * 路由：`/api/Auditor/ExpertTask`
 *
 * ★ 本页**零手写 CRUD**：列 / 搜索 / 工具栏 / 行按钮全部由后端 EntityConfig 驱动
 *   （`CertPlatform.Auditor/Assets/EntityConfigs/Expert/CertExpertTask.json`）。
 *   工作区隔离（`OnBuildingFilter` 按 `OrgCode` 等值收敛）也在后端，前端不传隔离参数。
 *
 * ★ 本文件只做三件事（其余交给内核）：
 *   ① 把 3 个自定义行按钮 + 2 个自定义工具栏按钮注册成「跳转 / 调接口」处理器；
 *   ② 按**后端视图字段**收敛行按钮显隐（`CanSubmit` / `CanRetry`）；
 *   ③ 编排「启动任务」的前置校验 → 关键信息补录 → 提交 → 启动队列。
 *
 * ⛔ 不要在这里写 `handleAdd` / `handleSubmit` / `handleRowAction` —— 内核已派发。
 * ⛔ 不要自己判「能不能提交」：判据是后端给的 `row.CanSubmit`，
 *   前端复制一遍状态机会在枚举变更时静默失配。
 *
 * 用户裁决（2026-09-30）：
 *   · 任务按**创建时间倒序**（最新在最顶上）—— 见 `index.vue` 的 `default-sort`
 *   · **任务查询不是重点**（只留 2 个筛选：任务名称 / 任务编号）
 *   · **分页处理**，每页 10 条
 *   · **不需要导出任务清单**（对专家没意义）
 *
 * 用户裁决（2026-10-07 · 本轮）：
 *   · **取消行内「看结果」按钮** —— 结论级结果走左侧菜单（NC 检查结果 / 报告结论）。
 *   · **「提交执行」→「启动任务」**：一次点击 = 前置校验 + 提交 + 启动全部队列。
 *     缺关键信息 ⇒ ⛔ **不启动**，弹「关键信息补录」抽屉当场补，补齐后自动继续。
 *   · **「运行跳过」**：允许不补录直接跑，但未执行项必须在任务详情「未执行清单」逐条留痕。
 *
 * ★ 这条流程修正的是**定位错位**（不是 3 个独立 bug）：
 *   旧流程把「跑之前必须备齐资料」这件系统自己该负责的事，拆成
 *   「提交 → 跳详情 → 再点启动 → 去第 4 个 Tab 找缺什么」四步**推给了审核员**。
 */

import { reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { SingleTableCore, confirmOrFalse, toRowActions } from '@yzh-core'
import type { YzhAction } from '@yzh-core'
import router from '@/router'
import {
  batchFillGaps,
  getTaskDetail,
  precheckGaps,
  retryFailed,
  skipAllGaps,
  startQueue,
  submitTask,
  type ExpertTaskRow,
  type TaskGapList,
} from '@share/api/auditor/expert-task'

export class ExpertTaskLogic extends SingleTableCore<ExpertTaskRow> {
  controllerName = 'Auditor/ExpertTask'

  /**
   * 「关键信息补录」抽屉状态（`index.vue` 渲染本状态）。
   *
   * ★ 为什么视图状态放 Logic 而不是页面：内核的 `dialogVisible` / `dialogMode` 就是这个
   *   惯例 —— 页面只负责版式，状态与编排都归 Logic。
   */
  readonly fill = reactive({
    visible: false,
    taskCode: '',
    taskName: '',
    gapList: null as TaskGapList | null,
    submitting: false,
  })

  /** 正在启动的任务 Code（防重复点击） */
  readonly starting = ref('')

  /**
   * 新增默认值。
   * ⚠️ 只放**表单里真实存在**的字段（否则是「提交了看不见的值」）。
   * 本页工具栏 `Add:false` —— 任务一律走向导创建，这里的值只在
   * `initFormData` 时兜底，保证 `formData` 结构完整。
   */
  protected override get defaultValues(): Record<string, any> {
    return { ScopeType: 'FULL', TaskSource: 'NEW' }
  }

  /** 确认框里显示的名称字段（默认 `Name`，任务表是 `TaskName`） */
  protected override get entityNameField(): string {
    return 'TaskName'
  }

  /**
   * 行按钮：由 JSON 声明（detail / submit / retry），
   * 但**显隐按后端视图字段收敛** ——
   * 「草稿」才能启动、「有失败项」才能重试。
   */
  override get rowActions(): YzhAction[] | ((row: ExpertTaskRow) => YzhAction[]) {
    const base = toRowActions(this.config.value, this.enableField)
    const declared = Array.isArray(base) ? base : base({} as ExpertTaskRow)

    const decorate = (a: YzhAction): YzhAction => {
      switch (a.key) {
        case 'custom:submit':
          return { ...a, type: 'primary', visible: a.visible !== false }
        case 'custom:retry':
          return { ...a, type: 'default', visible: a.visible !== false }
        default:
          return a
      }
    }

    return (row: ExpertTaskRow): YzhAction[] =>
      declared.map(decorate).map((a) => {
        if (a.key === 'custom:submit') return { ...a, visible: row.CanSubmit === true }
        if (a.key === 'custom:retry') return { ...a, visible: row.CanRetry === true }
        return a
      })
  }

  // ══════════════════════════════════════════════════════════════════════
  // 「启动任务」编排（★ 2026-10-07 用户裁决）
  // ══════════════════════════════════════════════════════════════════════

  /**
   * 提交执行 + 启动全部待启动队列。
   *
   * ⚠️ 两步必须都在：`POST /submit` 只把范围**物化**成队列（`pending`），
   *    真正开跑是 `POST /queue/start`。旧的「提交完让用户自己去详情页点启动」
   *    正是被用户批评的「反人直觉」—— 所以这里一次做完。
   */
  private readonly submitAndRun = async (taskCode: string): Promise<void> => {
    const sub = await submitTask(taskCode)
    const detail = await getTaskDetail(taskCode)
    const pending = detail.Queues.filter((q) => q.CanStart)

    let failed = 0
    for (const q of pending) {
      try {
        await startQueue(q.Code)
      } catch {
        failed += 1
      }
    }

    const started = pending.length - failed
    if (started > 0) ElMessage.success(`已启动 ${started} 个队列，任务开始执行`)
    if (failed > 0) ElMessage.warning(`${failed} 个队列启动失败，请到任务详情查看原因`)

    if (sub.PendingGapCount > 0) {
      // 未补录的项会在执行期被标「数据不足，未检查」⇒ 必须显式告知，⛔ 不静默
      ElMessageBox.alert(
        `${sub.Warning ?? `还有 ${sub.PendingGapCount} 项关键信息未补录`}\n\n` +
          `这些检查项会标记为「数据不足，未检查」，不会产生审核结果。\n` +
          `任务跑完后，可在任务详情的「未执行清单」里逐条看到是哪些规则 / 条款没执行、为什么。`,
        '任务已启动，但有检查项不会执行',
        { confirmButtonText: '知道了' },
      ).catch(() => undefined)
    } else if (started === 0 && failed === 0) {
      ElMessage.info('没有待启动的队列（可能已在执行中）')
    }

    await this.refresh()
  }

  /**
   * 启动前的**实时**缺口预检 → 无缺口直接跑；有缺口弹「关键信息补录」。
   *
   * ★ 为什么必须「实时重算」而不是读 `cert_expert_task_data_gap` 的存量行：
   *   缺口清单是**派生数据**（依赖当前资料 + 当前规则 DAG），存量行会随资料/配置变动漂移。
   *   后端 `precheck` 端点复用 `GapDetector.GenerateGapsAsync` 现算，避免「列表说有、其实已补」。
   */
  private readonly beginStart = async (row: ExpertTaskRow): Promise<void> => {
    if (this.starting.value) return
    const ok = await confirmOrFalse(
      `即将启动【${row.TaskName}】\n\n` +
        `· 涉及 ${row.StandardCount} 个标准 / ${row.TotalItemCount} 个检查项\n` +
        `· 启动前会先核对关键资料是否齐全，缺什么会当场弹窗让你补\n` +
        `· 补录后立即开始执行；不补录也可以选择「运行跳过」直接跑\n\n确定启动？`,
      '启动任务',
      { confirmButtonText: '启动' },
    )
    if (!ok) return

    this.starting.value = row.Code
    try {
      const pre = await precheckGaps(row.Code)
      if (pre.PendingCount > 0) {
        // ⛔ 不启动：先把缺口摊开让用户当场补（这是本轮最核心的改动）
        this.fill.taskCode = row.Code
        this.fill.taskName = row.TaskName
        this.fill.gapList = pre
        this.fill.visible = true
        return
      }
      await this.submitAndRun(row.Code)
    } catch (e) {
      ElMessage.error((e as Error)?.message || '启动前校验失败')
    } finally {
      this.starting.value = ''
    }
  }

  /** 抽屉「补齐并启动」：先批量补录，再提交 + 启动 */
  readonly onFillConfirm = async (items: { GapCode: string; Value: string }[]): Promise<void> => {
    const taskCode = this.fill.taskCode
    if (!taskCode) return
    this.fill.submitting = true
    try {
      if (items.length > 0) await batchFillGaps(items, '启动任务前补录')
      this.fill.visible = false
      await this.submitAndRun(taskCode)
    } catch (e) {
      ElMessage.error((e as Error)?.message || '补录失败')
    } finally {
      this.fill.submitting = false
    }
  }

  /**
   * 抽屉「运行跳过」：全部未补录项标跳过，然后照常提交 + 启动。
   *
   * ★ 用户裁决原文：「运行跳过，针对工作流缺失关键信息的，该工作流不执行，
   *   再队列完成后，详细记录，哪些规则或条款未执行成功，什么原因」
   *   ⇒ 允许跑，但**必须留痕**（`skipAllGaps` 写 `SkipReason`，
   *     任务详情「未执行清单」按规则/条款聚合展示）。
   */
  readonly onFillSkip = async (): Promise<void> => {
    const taskCode = this.fill.taskCode
    if (!taskCode) return
    const n = this.fill.gapList?.PendingCount ?? 0
    const typed = this.fill.gapList
      ? (this.fill.gapList.Fields.length + this.fill.gapList.Tables.length) -
        this.fill.gapList.PendingCount
      : 0

    const ok = await confirmOrFalse(
      `将跳过 ${n} 项未补录的关键信息，并直接启动任务。\n\n` +
        (typed > 0 ? `⚠ 已填写的内容不会被保存（本操作不补录，只跳过）。\n\n` : '') +
        `跳过后，依赖这些数据的检查项会标记为「数据不足，未检查」，` +
        `不会产生审核结果，也**不计入「符合」数量**。\n\n` +
        `任务跑完后，可在任务详情「未执行清单」里逐条看到是哪些规则 / 条款没执行、为什么。\n\n` +
        `确定跳过并启动？`,
      '运行跳过',
      { confirmButtonText: '跳过并启动', type: 'warning' },
    )
    if (!ok) return

    this.fill.submitting = true
    try {
      await skipAllGaps(taskCode, '启动任务时选择跳过')
      this.fill.visible = false
      await this.submitAndRun(taskCode)
    } catch (e) {
      ElMessage.error((e as Error)?.message || '跳过失败')
    } finally {
      this.fill.submitting = false
    }
  }

  constructor() {
    super()

    // ── 工具栏：创建新任务 ──
    this.registerHandler('custom:create', () => {
      void router.push({ name: 'TaskCreate' })
    })

    // ── 工具栏：从已有任务出发（基于选中行预填企业/阶段/分类）──
    this.registerHandler('custom:createFrom', (row) => {
      const t = row ?? this.selectedRows.value[0]
      if (!t) {
        ElMessage.warning('请先勾选一条已有任务，再点「从已有任务出发」')
        return
      }
      void router.push({ name: 'TaskCreate', query: { from: t.Code } })
    })

    // ── 行：详情 ──
    this.registerHandler('custom:detail', (row) => {
      if (!row) return
      void router.push({ name: 'TaskDetail', params: { code: row.Code } })
    })

    // ── 行：启动任务（★ 前置校验 → 缺资料弹补录抽屉 → 补齐后自动开跑）──
    this.registerHandler('custom:submit', async (row) => {
      if (!row) return
      await this.beginStart(row)
    })

    // ── 行：重试失败项（把 failed 重置为 pending）──
    this.registerHandler('custom:retry', async (row) => {
      if (!row) return
      const ok = await confirmOrFalse(
        `将把【${row.TaskName}】的失败项重置为「待执行」，` +
          '已成功/已跳过的项不受影响。\n\n确定重试？',
        '重试失败项',
        { confirmButtonText: '确定重试' },
      )
      if (!ok) return
      await retryFailed(row.Code)
      ElMessage.success('失败项已重置，可再次点「启动任务」')
      await this.refresh()
    })
  }
}

export default ExpertTaskLogic
