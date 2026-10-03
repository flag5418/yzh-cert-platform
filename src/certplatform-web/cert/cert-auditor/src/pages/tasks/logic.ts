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
 *   ① 把 4 个自定义行按钮 + 2 个自定义工具栏按钮注册成「跳转 / 调接口」处理器；
 *   ② 按**后端视图字段**收敛行按钮显隐（`CanSubmit` / `CanRetry` / `CanViewResult`）；
 *   ③ 声明新增默认值（本页 `Add:false`，仅为 formFields 完整保留）。
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
 */

import { ElMessage } from 'element-plus'
import { SingleTableCore, confirmOrFalse, toRowActions } from '@yzh-core'
import type { YzhAction } from '@yzh-core'
import router from '@/router'
import {
  retryFailed,
  submitTask,
  type ExpertTaskRow,
} from '@share/api/auditor/expert-task'

export class ExpertTaskLogic extends SingleTableCore<ExpertTaskRow> {
  controllerName = 'Auditor/ExpertTask'

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
   * 行按钮：由 JSON 声明（detail / submit / retry / result），
   * 但**显隐按后端视图字段收敛** ——
   * 「草稿」才能提交、「有失败项」才能重试、「跑过」才有结果可看。
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
        case 'custom:result':
          return { ...a, type: 'default', visible: a.visible !== false }
        default:
          return a
      }
    }

    return (row: ExpertTaskRow): YzhAction[] =>
      declared.map(decorate).map((a) => {
        if (a.key === 'custom:submit') return { ...a, visible: row.CanSubmit === true }
        if (a.key === 'custom:retry') return { ...a, visible: row.CanRetry === true }
        if (a.key === 'custom:result') return { ...a, visible: row.CanViewResult === true }
        return a
      })
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

    // ── 行：看结果（按任务类型落到对应结果菜单）──
    this.registerHandler('custom:result', (row) => {
      if (!row) return
      const name = row.TaskType === 'REPORT_GENERATE' ? 'ReportResults' : 'NcResults'
      void router.push({ name, query: { task: row.Code } })
    })

    // ── 行：提交执行（生成队列；幂等，可重复点）──
    this.registerHandler('custom:submit', async (row) => {
      if (!row) return
      const ok = await confirmOrFalse(
        `提交后将为「${row.StandardCount} 个标准」生成执行队列，` +
          `共 ${row.TotalItemCount} 个检查项。\n` +
          '生成后需到「详情 → 执行队列」逐个启动（可分批跑）。\n\n确定提交？',
        '提交执行',
        { confirmButtonText: '确定提交' },
      )
      if (!ok) return
      await submitTask(row.Code)
      ElMessage.success('已生成执行队列，请到详情页启动')
      await this.refresh()
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
      ElMessage.success('失败项已重置，请到详情页启动队列')
      await this.refresh()
    })
  }
}

export default ExpertTaskLogic
