/**
 * 专家任务系统 —— 前端展示用枚举/标签（**只放展示映射，不放业务判定**）
 *
 * ★ 铁律：**按钮可用性、状态流转、锁判定一律由后端算**（`ExecStatusLabel` / `CanSubmit` /
 *   `CanRetry` / `CanViewResult` / `ConclusionLabel` / `SourceLabel` / `GenerationMode` 都是
 *   服务端视图字段）。本文件只做「英文枚举 → 中文标签 / 标签配色」这一件事，
 *   ⛔ 不得在这里写 `if (status === 'xxx') 能不能点`。
 *
 * 常量值必须与后端 `CertPlatform.Auditor/Services/Expert/ExpertTaskConst.cs` **逐字一致**。
 */

/** 任务类型 */
export const TASK_TYPE_OPTIONS = [
  { value: 'NC_CHECK', label: 'NC 检查', desc: '按 NC 规则逐条判定，产出「符合 / 不符合」结论' },
  { value: 'REPORT_GENERATE', label: '报告生成', desc: '按报告章节逐节生成正文，产出章节内容' },
] as const

/** 任务来源（D25） */
export const TASK_SOURCE_OPTIONS = [
  { value: 'NEW', label: '全新任务', desc: '本阶段首次执行，全部检查项从零开始' },
  { value: 'REDO', label: '整体重执行', desc: '企业资料整体更新后重跑全部检查项（产生新一轮结果）' },
  { value: 'PATCH', label: '局部更新', desc: '只补跑指定检查项（其余沿用上一轮结论）' },
] as const

/** 执行范围 */
export const SCOPE_TYPE_OPTIONS = [
  { value: 'FULL', label: '全部检查项', desc: '该阶段下所有生效的规则/章节都执行' },
  { value: 'PARTIAL', label: '只跑勾选项', desc: '只执行第 3 步勾选的检查项' },
] as const

/** 执行状态（第 1 层 · 机器管；★ 唯一决定 D36 业务锁） */
export const EXEC_STATUS_MAP: Record<string, string> = {
  draft: '草稿',
  pending_run: '待启动',
  running: '执行中',
  completed: '已完成',
  failed: '有失败项',
}

export const EXEC_STATUS_TAG: Record<string, 'info' | 'primary' | 'warning' | 'success' | 'danger'> = {
  draft: 'info',
  pending_run: 'info',
  running: 'primary',
  completed: 'success',
  failed: 'danger',
}

/** 队列状态 */
export const QUEUE_STATUS_MAP: Record<string, string> = {
  pending: '待启动',
  running: '执行中',
  completed: '已完成',
  failed: '有失败项',
  cancelled: '已取消',
}

export const QUEUE_STATUS_TAG: Record<string, 'info' | 'primary' | 'warning' | 'success' | 'danger'> = {
  pending: 'info',
  running: 'primary',
  completed: 'success',
  failed: 'danger',
  cancelled: 'info',
}

/** 复核状态（第 2 层 · 专家管；★ 结论级，不影响任何逻辑） */
export const REVIEW_STATUS_MAP: Record<string, string> = {
  not_started: '未复核',
  pending_review: '待审批',
  reviewed: '已认可',
  modified: '已修改',
  skipped: '已跳过',
  frozen: '已冻结',
}

export const REVIEW_STATUS_TAG: Record<string, 'info' | 'primary' | 'warning' | 'success' | 'danger'> = {
  not_started: 'info',
  pending_review: 'warning',
  reviewed: 'success',
  modified: 'primary',
  skipped: 'info',
  frozen: 'danger',
}

/** 自动结果状态 */
export const AUTO_STATUS_MAP: Record<string, string> = {
  none: '未执行',
  ok: '符合',
  ng: '不符合',
  skipped: '已跳过',
  failed: '执行失败',
  degraded: '降级（模板示例）',
}

export const AUTO_STATUS_TAG: Record<string, 'info' | 'primary' | 'warning' | 'success' | 'danger'> = {
  none: 'info',
  ok: 'success',
  ng: 'danger',
  skipped: 'info',
  failed: 'danger',
  degraded: 'warning',
}

/**
 * ★ 跳过分类 —— 界面必须**完整展示原因**，否则专家会以为系统漏检。
 * 与后端 `ExpertTaskConst.SkipCategory` 逐字一致。
 */
export const SKIP_CATEGORY_MAP: Record<string, string> = {
  data_gap: '依赖的企业资料缺失',
  data_gap_skipped: '资料缺口已被跳过',
  no_rule: '未配置工作流（规则为空）',
  rule_disabled: '规则已停用',
  manual_mode: '人工判定项（机器不猜）',
  exec_failed: '执行失败',
}

/** 判定方式（`cert_validation_rule.JudgeMode`） */
export const JUDGE_MODE_MAP: Record<string, string> = {
  auto: '自动判定',
  semi: '半自动',
  manual: '人工判定',
}

/** 专家结论（NC） */
export const CONFORMITY_OPTIONS = [
  { value: 'conform', label: '符合' },
  { value: 'nonconform', label: '不符合' },
  { value: 'observation', label: '观察项' },
  { value: 'na', label: '不适用' },
] as const

export const CONFORMITY_MAP: Record<string, string> = {
  conform: '符合',
  nonconform: '不符合',
  observation: '观察项',
  na: '不适用',
}

export const CONFORMITY_TAG: Record<string, 'info' | 'primary' | 'warning' | 'success' | 'danger'> = {
  conform: 'success',
  nonconform: 'danger',
  observation: 'warning',
  na: 'info',
}

/** 严重度 */
export const SEVERITY_OPTIONS = [
  { value: 'major', label: '严重不符合' },
  { value: 'minor', label: '一般不符合' },
  { value: 'observation', label: '观察项' },
] as const

export const SEVERITY_MAP: Record<string, string> = {
  major: '严重不符合',
  minor: '一般不符合',
  observation: '观察项',
}

export const SEVERITY_TAG: Record<string, 'info' | 'warning' | 'danger'> = {
  major: 'danger',
  minor: 'warning',
  observation: 'info',
}

/** 报告生成方式三档（D-F）—— 决定专家审核时的心理预期 */
export const GENERATION_MODE_MAP: Record<string, string> = {
  自动生成: '自动生成',
  引用上轮结果: '引用上轮结果',
  人工撰写: '人工撰写',
  待人工撰写: '待人工撰写',
}

export const GENERATION_MODE_TAG: Record<string, 'info' | 'primary' | 'warning' | 'success'> = {
  自动生成: 'primary',
  引用上轮结果: 'info',
  人工撰写: 'success',
  待人工撰写: 'warning',
}

/** 日志级别 */
export const LOG_LEVEL_TAG: Record<string, 'info' | 'warning' | 'danger'> = {
  info: 'info',
  warn: 'warning',
  error: 'danger',
}

export const LOG_LEVEL_MAP: Record<string, string> = {
  info: '信息',
  warn: '警告',
  error: '错误',
}

/**
 * 日志动作（23 号 §四 埋点清单）。
 * 未登记的动作原样显示 —— 后端新增埋点时前端不必同步改（不会静默丢失）。
 */
export const LOG_ACTION_MAP: Record<string, string> = {
  'task.created': '任务创建',
  'task.lock.acquired': '业务锁获取',
  'task.submitted': '提交执行',
  'task.progress': '任务进度',
  'task.paused': '任务暂停',
  'task.resumed': '任务恢复',
  'task.finished': '任务结束',
  'task.cancelled': '任务取消',
  'task.archived': '任务归档',
  'scope.resolved': '范围解析',
  'item.derived': '检查项派生',
  'queue.created': '队列创建',
  'queue.started': '队列启动',
  'queue.paused': '队列暂停',
  'queue.progress': '队列进度',
  'queue.finished': '队列结束',
  'item.extract.ok': '资料提取成功',
  'item.extract.retry': '资料提取重试',
  'item.extract.fail': '资料提取失败',
  'item.judge.ok': '自动判定完成',
  'item.judge.nc': '自动判定发现不符合',
  'item.skip': '检查项跳过',
  'gap.created': '生成数据缺口',
  ACKNOWLEDGE: '批量认可',
  MODIFY: '人工修改',
  SKIP: '人工跳过',
  EXPORT: '导出',
  SKIP_ALL_GAPS: '跳过全部缺口',
}

// ══════════════════════════════════════════════════════════════════════
// 补录清单（★ 2026-09-30 裁决 J1：两张扁平表，不分文档）
// ══════════════════════════════════════════════════════════════════════

/** 缺口状态（逐字对齐后端 `ExpertTaskConst.GapStatus`） */
export const GAP_STATUS_MAP: Record<string, string> = {
  pending: '待补录',
  filled: '已补录',
  skipped: '已跳过',
}

export const GAP_STATUS_TAG: Record<string, 'warning' | 'success' | 'info'> = {
  pending: 'warning',
  filled: 'success',
  skipped: 'info',
}

/** 缺口类型 */
export const GAP_TYPE_MAP: Record<string, string> = {
  field: '字段',
  table: '表格',
  unknown: '⚠ 未识别节点',
}

/** ★ 值来源（`cert_extraction_result.ValueSource`，裁决 J2） */
export const VALUE_SOURCE_MAP: Record<string, string> = {
  auto: '自动提取',
  manual: '人工录入',
  ai_generated: 'AI 生成',
}

export const VALUE_SOURCE_TAG: Record<string, 'success' | 'warning' | 'info'> = {
  auto: 'success',
  manual: 'warning',
  ai_generated: 'info',
}

/** ★ 补录留痕动作（`ExtractionChangeLog` 常量） */
export const CHANGE_ACTION_MAP: Record<string, string> = {
  manual_edit: '人工补录',
  archive: '被重新提取覆盖',
  restore_auto: '恢复自动值',
  revoke: '作废',
}

export const CHANGE_ACTION_TAG: Record<string, 'primary' | 'danger' | 'info' | 'warning'> = {
  manual_edit: 'primary',
  archive: 'danger',
  restore_auto: 'info',
  revoke: 'warning',
}

/**
 * ★ 工作流失败分类码（`WorkflowErrorCodes`）
 *
 * <b>为什么 UI 必须区分</b>：缺陷 B1 未修时，「故意造的缺口」与「取数口径错配造成的缺口」
 * 报的是同一句「缺失必要数据」⇒ 补录夹具<b>没有信号</b>。
 * 分成两码后：`DATA_MISSING` 红色 ·「去补录」，`SYSTEM_ERROR` 灰色 ·「报 bug」。
 */
export const WF_ERROR_CODE_MAP: Record<string, string> = {
  DATA_MISSING: '缺失必要数据',
  SYSTEM_ERROR: '系统错误',
}

export const WF_ERROR_CODE_TAG: Record<string, 'danger' | 'info'> = {
  DATA_MISSING: 'danger',
  SYSTEM_ERROR: 'info',
}

