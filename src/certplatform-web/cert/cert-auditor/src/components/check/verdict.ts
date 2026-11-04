/**
 * 检查项结论词表 —— 认证准备 / 认证检查 共用的唯一出口
 *
 * ★ 为什么单独一个文件（而不是各页面自己写 Record）：
 *   后端有**四套状态词表**同时在跑，页面上一旦混用，审核员看到的字就自相矛盾：
 *     ① PrepState        准备分支五态  found / partial / missing / unknown / na
 *     ② Conclusion       检查分支四态  pass / ofi / nc / na
 *     ③ LLM 原始结论      conformant / observation / minor_nc / major_nc（仅 LLM 输出，落库前归一为 ②）
 *     ④ DimensionConformity  conform / nonconform / na / unverifiable（维度级，二期）
 *   后端已用 `CertVerdictMapper` 把 ③ 归一化到 ②，前端在这里把 ② / ① 翻成中文。
 *
 * ⚠️ 词表改动**必须**同步后端 `CertVerdictMapper.cs`，否则两边会漂。
 */

export type TagType = 'primary' | 'success' | 'warning' | 'info' | 'danger'

// ══════════════════════════════════════════════════════════════════════
// 一、准备分支：PrepState（资料状态）
// ══════════════════════════════════════════════════════════════════════

export const PREP_STATE_LABELS: Record<string, string> = {
  found: '资料齐全',
  partial: '资料部分',
  missing: '资料缺失',
  unknown: '待确认',
  na: '不适用',
}

export const PREP_STATE_TAGS: Record<string, TagType> = {
  found: 'success',
  partial: 'warning',
  missing: 'danger',
  unknown: 'info',
  na: 'info',
}

// ══════════════════════════════════════════════════════════════════════
// 二、检查分支：Conclusion（判定结论）
// ══════════════════════════════════════════════════════════════════════

export const CONCLUSION_LABELS: Record<string, string> = {
  pass: '符合',
  ofi: '观察项',
  nc: '不符合',
  na: '不适用',
}

export const CONCLUSION_TAGS: Record<string, TagType> = {
  pass: 'success',
  ofi: 'warning',
  nc: 'danger',
  na: 'info',
}

// ══════════════════════════════════════════════════════════════════════
// 三、严重程度
// ══════════════════════════════════════════════════════════════════════

export const SEVERITY_LABELS: Record<string, string> = {
  critical: '严重不符合',
  major: '重大不符合',
  minor: '一般不符合',
  observation: '观察项',
  none: '无',
}

export const SEVERITY_TAGS: Record<string, TagType> = {
  critical: 'danger',
  major: 'danger',
  minor: 'warning',
  observation: 'info',
  none: 'info',
}

// ══════════════════════════════════════════════════════════════════════
// 四、执行实例状态（cert_task.Status 归一化后）
// ══════════════════════════════════════════════════════════════════════

export const STAGE_STATE_LABELS: Record<string, string> = {
  waiting: '待启动',
  running: '执行中',
  paused: '已暂停',
  done: '已完成',
  partial: '部分完成',
  failed: '失败',
  cancelled: '已取消',
}

export const STAGE_STATE_TAGS: Record<string, TagType> = {
  waiting: 'info',
  running: 'primary',
  paused: 'warning',
  done: 'success',
  partial: 'warning',
  failed: 'danger',
  cancelled: 'info',
}

// ══════════════════════════════════════════════════════════════════════
// 五、检查项综合状态
// ══════════════════════════════════════════════════════════════════════

export const ITEM_STATE_LABELS: Record<string, string> = {
  waiting: '未分析',
  analyzed: '已分析',
  locked: '已锁定',
}

export const ITEM_STATE_TAGS: Record<string, TagType> = {
  waiting: 'info',
  analyzed: 'primary',
  locked: 'success',
}

// ══════════════════════════════════════════════════════════════════════
// 六、分支元信息
// ══════════════════════════════════════════════════════════════════════

export type StageKind = 'prep' | 'onsite'

export interface StageMeta {
  /** 菜单名 */
  title: string
  /** 一句话说明「这个分支产出什么」 */
  subtitle: string
  /** 详情页步骤条 */
  steps: string[]
  /** 状态列的语义：准备看资料状态，检查看结论 */
  stateLabel: string
}

export const STAGE_META: Record<StageKind, StageMeta> = {
  prep: {
    title: '认证准备',
    subtitle:
      '由「认证计划」派生。系统先按企业资料初判每个检查项的资料齐不齐，审核员再补证据、定审核员、排入场计划。产出是入场计划，不签发 NC。',
    steps: ['资料初判', '补充证据', '判定入场', '锁定', '导出入场计划'],
    stateLabel: '资料状态',
  },
  onsite: {
    title: '认证检查',
    subtitle:
      '由「认证计划」派生。审核员逐项挂接证据，系统结合条款模型与企业文档推理出检查证据和 NC 疑点，审核员复核确认后逐条签发 NC。产出是认证报告。',
    steps: ['挂接证据', '分析', '签发 NC', '锁定', '生成报告'],
    stateLabel: '判定结论',
  },
}

// ══════════════════════════════════════════════════════════════════════
// 七、取值助手（缺值统一显示 —，不要显示 undefined / null）
// ══════════════════════════════════════════════════════════════════════

export const labelOf = (map: Record<string, string>, key?: string | null): string =>
  key ? map[key] ?? key : '—'

export const tagOf = (map: Record<string, TagType>, key?: string | null): TagType =>
  (key ? map[key] : undefined) ?? 'info'

/** 进度百分比（0–100，整数） */
export const percent = (done: number, total: number): number =>
  total > 0 ? Math.round((done / total) * 100) : 0

/**
 * 进度条颜色。
 * ⚠️ 一律用主题变量，⛔ 不写裸 hex —— 法条 S13 要求明暗双主题都能自适应，
 * 裸色值在暗色主题下会「亮得刺眼」且不随主题走。
 */
export const progressColor = (p: number): string =>
  p >= 100
    ? 'var(--el-color-success)'
    : p >= 50
      ? 'var(--el-color-primary)'
      : p > 0
        ? 'var(--el-color-warning)'
        : 'var(--el-border-color)'
