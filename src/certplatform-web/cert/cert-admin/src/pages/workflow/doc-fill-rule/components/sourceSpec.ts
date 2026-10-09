/**
 * 取值来源（`DocTemplateAnchor.SourceSpec`）的**唯一前端模型**
 *
 * 【★ 2026-10-09 最终口径 —— 推翻 `22` 号 §三 的「来源链」设计】
 *   用户裁定：**一个锚点 = 一个来源**（原话：「一个锚点有且只有一个来源，
 *   如果一个锚点需要多个企业资料，那就是 AI 字段，通过提示词来组织」）。
 *   ⇒ ① **删掉「组合方式」整层**（`combine`：顺序回退 / 拼接 / 模板套用）——
 *        用户原话：「没有顺序回退 / 拼接 / 模板套用这种选项，我们的规则是必须正确的，
 *        没有想当然的，有问题我们改程序」；
 *     ② **删掉「企业资料画像」来源** —— 用户从未要求过该概念；
 *     ③ 需要多个企业资料 ⇒ 选 **AI 字段**，由提示词组织。
 *
 * 【★ 2026-10-09 来源收敛为 5 种】
 *   全局参数 / AI 语义生成 / AI 字段 / AI 表格 / 人工填写。
 *   AI 三类在前端是**语义标签**，后端 `ResolveByEntryAsync` 统一按「AI 认领」处理。
 *
 * 【与后端 JSON 的兼容（⛔ 改这个文件前必读）】
 *   存储仍写 `{ "sources": [ 单个条目 ] }` —— 后端 `SourceSpecModel.Sources` 是数组，
 *   编排器 `foreach (var entry in spec.Sources)` **取首个命中即停**（`DocumentFillOrchestrator.cs`）。
 *   ⇒ 数组长度为 1 时行为与「单来源」完全一致，**后端不需要为新格式改代码**。
 *   ⛔ 不再写 `combine` / `separator` / `expr` / `onMissing` —— 后端这几项都有默认值
 *   （`Combine = "firstHit"` / `OnMissing = "next"`），不写不会解析失败。
 */

/** 一个来源条目（**字段名与后端 JSON 大小写无关**：C# 侧 `PropertyNameCaseInsensitive = true`） */
export interface SourceSpecEntry {
  /** `global` / `ai_semantic` / `ai_field` / `ai_table` / `manual` */
  kind: string
  /** `global` 专用 = 参数编码；`ai_*` 且 `hasParam=true` 时的**单参数老写法**（新写法用 params） */
  ref?: string
  /** ★ `ai_*` 专用：① 有无参数（有 ⇒ 就是全局参数） */
  hasParam?: boolean
  /**
   * ★ `ai_*` 专用：④ **引用的全局参数（多选）** —— 「带参数」的落地。
   *
   * 依据：48 §2.2 ① / 61 S-1 / 原型 V4 ——「勾哪些，提示词里 `{{参数}}` 引用」。
   * ⚠️ 兼容：为空但 `hasParam=true` 时回落单值 `ref`（见 `aiParamRefs`）。
   */
  params?: string[]
  /** ★ `ai_*` 专用：② 是否依赖企业资料（不依赖 ⇒ 只靠「参数 + 提示词」生成） */
  dependsOnEnterprise?: boolean
  /** ★ `ai_*` 专用：③ 提示词（其中可用 `{{参数}}` 引用 params 里的全局参数） */
  prompt?: string
}

/** 整条来源 —— ★ 单来源（`source.kind === ''` = 还没选） */
export interface SourceSpecModel {
  source: SourceSpecEntry
}

/**
 * ★ 来源类别（**6 种**，⛔ 不可再增删而不改后端）。
 *
 * 【顺序即 UI 顺序】抽屉 ① 段按此顺序渲染。
 * 【`implemented`】三类 AI 在后端 `ResolveByEntryAsync` 里统一走「AI 认领」分支，
 *   ⛔ 不存在「配了必然不生效」的来源 ⇒ 6 种全部可配。
 *
 * 【★ 2026-10-09 用户裁决】「文档信息」是**第 6 种来源**（原话：「可以」）。
 *   没有它的话，`{{阶段名称}}` / `{{标准号}}` 这类锚点在本页**根本配不出来**。
 */
export const SOURCE_KINDS: {
  value: string
  label: string
  hint: string
}[] = [
  {
    value: 'global',
    label: '全局参数',
    hint: '取参数值 —— 含企业基本信息 / 机构·系统变量（只读）与后台定义的全局参数（企业可覆盖）',
  },
  {
    value: 'headerFooter',
    label: '文档信息',
    hint: '文档编号 / 文档名称 / 版本号 / 标准编号 / 认证阶段 / 页码 —— 页眉页脚最常用',
  },
  {
    value: 'ai_semantic',
    label: 'AI 语义生成',
    hint: '由模型按提示词生成一段文字（如「质量方针」这类成段内容）',
  },
  {
    value: 'ai_field',
    label: 'AI 字段',
    hint: '★ 需要多个企业资料拼成一句话时选它 —— 由提示词组织，如「{{企业全称}}（{{统一社会信用代码}}）」',
  },
  {
    value: 'ai_table',
    label: 'AI 表格',
    hint: '生成一整张表（多行多列），由提示词描述要哪些列',
  },
  {
    value: 'manual',
    label: '人工填写',
    hint: '运行期挂人工待办，由审核专家填写（语义分析取不到的字段走这条）',
  },
]

/* ══════════════════════════════════════════════════════════════════
 * ★ 「全局参数」卡片里的具体项（4 组）
 *
 * 【为什么 ①② 是**静态硬编码**】
 *   它们对应引擎 `ReplaceResolver` 的能力 —— 属性清单**硬编码在代码里**
 *   （`EnterpriseAttrLabels` / `OrgInfo.Get` / `ReplaceResolver.FromSystem`），
 *   ⛔ 不是数据库里的参数行。所以前端也必须硬编码同一份清单，
 *   ⚠️ 改这里必须同步改 `ReplaceResolver`（否则「选得到但取不到」）。
 *
 * 【为什么 ③④ 是**动态**】
 *   它们来自 `cert_fill_param_def` 表（后台可维护）⇒ 由 `listFillParamDefs()` 拉取。
 *
 * 【★ 只读 vs 可覆盖 —— 本项目最易搞错的一处】
 *   判据：**这个位置是否允许企业填一个和企业档案不同的值？**
 *     不允许 → `replace`（①②，**只读**，直读企业档案/机构/系统，企业改了也不生效）
 *     允许   → `global`（③④，**可覆盖**，读企业填的 `cert_fill_param_value`）
 *   ⚠️ 选错的后果：**配了不生效且两边都不报错**。
 * ══════════════════════════════════════════════════════════════════ */

/** 组① 企业基本信息 —— 16 项，`replace` 只读（= `ReplaceResolver.EnterpriseAttrLabels`） */
export const ENTERPRISE_ATTRS: [string, string][] = [
  ['enterprise.Code', '企业编码'],
  ['enterprise.Name', '企业全称'],
  ['enterprise.ShortName', '企业简称'],
  ['enterprise.CreditCode', '统一社会信用代码'],
  ['enterprise.LegalPerson', '法定代表人'],
  ['enterprise.Province', '省份'],
  ['enterprise.City', '城市'],
  ['enterprise.Address', '企业地址'],
  ['enterprise.IndustryType', '行业类型'],
  ['enterprise.EmployeeCount', '员工人数'],
  ['enterprise.CertScope', '认证范围'],
  ['enterprise.ContactName', '体系对接人'],
  ['enterprise.ContactPhone', '联系电话'],
  ['enterprise.ContactEmail', '联系邮箱'],
  ['enterprise.EnterpriseNo', '企业编号'],
  ['enterprise.ArchiveDate', '归档日期'],
]

/** 组② 机构 / 系统变量 —— 12 项，`replace` 只读 */
export const ORG_SYS_ATTRS: [string, string][] = [
  ['org.Code', '机构编码'],
  ['org.Name', '机构名称'],
  ['org.ShortName', '机构简称'],
  ['org.Address', '机构地址'],
  ['org.ContactName', '机构联系人'],
  ['org.ContactPhone', '机构电话'],
  ['system.date', '制表日期 yyyy-MM-dd'],
  ['system.datetime', '日期时间'],
  ['system.date_cn', '中文日期'],
  ['system.year', '年'],
  ['system.month', '月'],
  ['system.day', '日'],
]

/** ★ 「文档信息」卡片的 6 项 —— 走引擎 `headerFooter` 能力（token 带 `@` 前缀） */
export const DOC_INFO_ITEMS: [string, string][] = [
  ['@doc_no', '文档编号'],
  ['@doc_title', '文档名称'],
  ['@version', '版本号'],
  ['@standard_no', '标准编号'],
  ['@stage_name', '认证阶段'],
  ['@page', '页码'],
]

/**
 * ★ 由 `ref` 反推后端 `kind`（⛔ 唯一口径，不要在组件里再判一次）。
 *
 * 【为什么要反推而不是让用户选】
 *   用户在 ① 段只选「全局参数」这一张卡片，在 ② 段挑具体项 ——
 *   「挑到的是只读引用还是可覆盖参数」是**程序该算出来的**，
 *   ⛔ 不该再让用户理解一遍 `replace` / `global` 的区别（用户视角铁律）。
 */
export function kindOfRef(ref?: string | null): string {
  const r = (ref ?? '').trim()
  if (!r) return 'global'
  if (r.startsWith('@')) return 'headerFooter'
  if (
    r.startsWith('enterprise.') ||
    r.startsWith('org.') ||
    r.startsWith('system.')
  )
    return 'replace'
  return 'global'
}

/**
 * ★ 存储 `kind` → UI 卡片。
 *
 * `replace` 在界面上**归在「全局参数」卡片下**（用户裁决「需要合并」）——
 * 所以卡片高亮判据必须用它，⛔ 不能直接用 `source.kind`。
 */
export function cardOf(kind?: string | null): string {
  return kind === 'replace' ? 'global' : (kind ?? '')
}

/** 在「全局参数」卡片内查一项（含静态 ①② 与动态 ③④） */
export function findParam(
  ref?: string | null,
  dynamic: { value: string; label: string }[] = [],
): { ref: string; label: string; kind: string; ro: boolean } | null {
  const r = (ref ?? '').trim()
  if (!r) return null
  const hit =
    ENTERPRISE_ATTRS.find((p) => p[0] === r) ??
    ORG_SYS_ATTRS.find((p) => p[0] === r)
  if (hit) return { ref: hit[0], label: hit[1], kind: 'replace', ro: true }
  const dyn = dynamic.find((p) => p.value === r)
  if (dyn) return { ref: dyn.value, label: dyn.label, kind: 'global', ro: false }
  return { ref: r, label: `${r}（库里没有）`, kind: 'global', ro: false }
}

/** 「只读 · replace」/「可覆盖 · global」徽标文案 */
export function paramCapLabel(ref?: string | null): string {
  return kindOfRef(ref) === 'replace' ? '只读 · replace' : '可覆盖 · global'
}

/**
 * ★ `ref` → 人类可读名（**⛔ 不判断「库里有没有」**）。
 *
 * 【与 `findParam` 的分工 —— 混用会出假警报】
 *   `findParam` 供 ② 段下拉使用：那里**有**动态清单，能真判「库里没有」。
 *   本函数供**列表列 / 人话预览**使用：那里**拿不到**动态清单，
 *   若也去判「库里没有」，会把**每一个真实存在的全局参数**都标成「库里没有」。
 */
export function labelOfRef(ref?: string | null): string {
  const r = (ref ?? '').trim()
  if (!r) return ''
  const hit =
    ENTERPRISE_ATTRS.find((p) => p[0] === r) ??
    ORG_SYS_ATTRS.find((p) => p[0] === r) ??
    DOC_INFO_ITEMS.find((p) => p[0] === r)
  return hit ? hit[1] : r
}

/**
 * ★ ③ 填写规则 —— 「值怎么写回文档」。
 *
 * 【为什么这两个值能直接用后端既有受控值】
 *   `DocTemplateAnchor.WriteMode` 的受控值集合就是
 *   `{ replace, overwrite, append, remove }`（`DocTemplateAnchorController.WriteModes`）
 *   ⇒ 「覆盖 / 填充」**不需要改后端枚举**，只需前端映射到其中两个。
 *
 * 【用户 2026-10-09 的原话（这就是这两个选项的由来）】
 *   「我们很多时候，一个单元格并不是一个字段，一般是一句话，中间有 {{}}，
 *    我们如果用覆盖，就将 cell 全部填充成新的内容了，只能替换当前单元格的 {{}}」
 */
export const WRITE_MODES: {
  value: string
  label: string
  hint: string
}[] = [
  {
    value: 'overwrite',
    label: '覆盖',
    hint: '整格替换成取值 —— 格子里的其它文字**一并被替换掉**',
  },
  {
    value: 'replace',
    label: '填充',
    hint: '只替换格子里的 {{token}}，其余文字**原样保留**（一句话中间带 {{}} 时用这个）',
  },
]

export const DEFAULT_WRITE_MODE = 'overwrite'

export function writeModeLabel(v?: string | null): string {
  return WRITE_MODES.find((m) => m.value === v)?.label ?? v ?? '覆盖'
}

export function kindLabel(kind: string): string {
  return SOURCE_KINDS.find((k) => k.value === kind)?.label ?? kind
}

/** 是否 AI 类来源（三类 AI 共用「参数 / 依赖企业资料 / 提示词」三属性） */
export function isAiKind(kind: string): boolean {
  return kind === 'ai_semantic' || kind === 'ai_field' || kind === 'ai_table'
}

/** 空模型（新增时起点）—— ★ 单来源，`kind === ''` 表示「还没选」 */
export function emptySourceSpec(): SourceSpecModel {
  return { source: { kind: '' } }
}

/** 未选来源的判据（⛔ 唯一口径，不要在组件里写 `=== null`） */
export function isBlankSource(model: SourceSpecModel): boolean {
  return !model.source || !model.source.kind
}

/**
 * ★ 生效的**参数引用列表**（去空、去重保序）—— **唯一判据**。
 *
 * 与后端 `SourceSpecEntry.AiParamRefs()` 同口径：`params` 优先，为空时回落单值 `ref`
 * （2026-10-09 之前 UI 只能单选一个参数）。⛔ 两处必须一致，否则会出现
 * 「页面显示有参数、后端取到 0 个」这类静默失效。
 */
export function aiParamRefs(e?: SourceSpecEntry | null): string[] {
  if (!e) return []
  const list = Array.isArray(e.params)
    ? e.params.map((x) => String(x ?? '').trim()).filter((x) => x !== '')
    : []
  const uniq = [...new Set(list)]
  if (uniq.length === 0 && e.hasParam === true && e.ref)
    return [String(e.ref).trim()]
  return uniq
}

/** 新建一个来源条目（选中某个 kind 时用） */
export function newEntry(kind: string): SourceSpecEntry {
  const e: SourceSpecEntry = { kind }
  if (isAiKind(kind)) {
    e.hasParam = false
    e.params = []
    e.dependsOnEnterprise = true
    e.prompt = ''
  }
  return e
}

/**
 * 解析 `SourceSpec` 字符串。
 *
 * ⚠️ **绝不抛异常**：这一列是人工/历史数据，可能是空串、可能是半截 JSON。
 * 抛异常会让整个侧边栏打不开，用户连「改回来」的机会都没有。
 * 解析失败 ⇒ 返回空模型 + `parseError=true`，由界面提示「原值无法解析，保存将覆盖」。
 *
 * ⚠️ **旧数据兼容**：`22` 号时代的配置是**多来源链**（`sources` 长度 > 1）。
 * 新模型只支持 1 个来源 ⇒ 取**第 1 个**，并把 `legacyMultiSource=true` 报给界面，
 * 由界面提示「原配置有 N 个来源，已保留第 1 个，保存将丢弃其余」。
 * ⛔ 绝不静默丢弃 —— 静默丢弃 = 用户以为「只是打开看了一眼」，其实配置已经被砍。
 */
export function parseSourceSpec(raw?: string | null): {
  model: SourceSpecModel
  parseError: boolean
  /** 旧数据：原配置有多个来源，新模型只保留第 1 个 */
  legacyMultiSource: boolean
  /** 旧数据里被丢弃的来源条数（0 = 无） */
  droppedCount: number
} {
  const text = (raw ?? '').trim()
  if (!text)
    return {
      model: emptySourceSpec(),
      parseError: false,
      legacyMultiSource: false,
      droppedCount: 0,
    }

  try {
    const obj = JSON.parse(text)
    if (obj == null || typeof obj !== 'object')
      return {
        model: emptySourceSpec(),
        parseError: true,
        legacyMultiSource: false,
        droppedCount: 0,
      }

    const arr: any[] = Array.isArray(obj.sources) ? obj.sources : []
    const usable = arr.filter((s) => s && typeof s === 'object')
    if (usable.length === 0)
      return {
        model: emptySourceSpec(),
        parseError: false,
        legacyMultiSource: false,
        droppedCount: 0,
      }

    const first = usable[0]
    const entry: SourceSpecEntry = {
      kind: String(first.kind ?? 'global'),
    }
    if (first.ref != null) entry.ref = String(first.ref)
    if (typeof first.hasParam === 'boolean') entry.hasParam = first.hasParam
    if (typeof first.dependsOnEnterprise === 'boolean')
      entry.dependsOnEnterprise = first.dependsOnEnterprise
    if (first.prompt != null) entry.prompt = String(first.prompt)

    // ★ 多参数（新写法）
    if (Array.isArray(first.params)) {
      const list: string[] = []
      for (const x of first.params as any[]) {
        const s = String(x ?? '').trim()
        if (s !== '' && !list.includes(s)) list.push(s)
      }
      if (list.length) entry.params = list
    }
    // ★ 老数据兼容：单参数只存在 `ref`（2026-10-09 之前 UI 只能单选）⇒ 迁移成 params[0]
    if (
      (!entry.params || entry.params.length === 0) &&
      entry.hasParam === true &&
      entry.ref
    )
      entry.params = [String(entry.ref)]

    return {
      model: { source: entry },
      parseError: false,
      legacyMultiSource: usable.length > 1,
      droppedCount: Math.max(0, usable.length - 1),
    }
  } catch {
    return {
      model: emptySourceSpec(),
      parseError: true,
      legacyMultiSource: false,
      droppedCount: 0,
    }
  }
}

/**
 * 生成 `SourceSpec` 字符串。
 *
 * 规则：
 * - 无来源 ⇒ 返回 `null`（**清空该列**，而不是存 `{"sources":[]}`）——
 *   校验页把「未配来源」判为黄牌，靠的就是这一列是空。
 * - 只写与当前 `kind` 相关的字段（⛔ 不写无关字段，避免两处口径）。
 * - ⛔ 不写 `combine` / `onMissing` —— 后端有默认值。
 */
export function stringifySourceSpec(model: SourceSpecModel): string | null {
  const s = model.source
  if (!s.kind) return null

  const card = cardOf(s.kind)
  // ★ 「全局参数」卡片：后端 kind **由 ref 反推** ——
  //   ①②（enterprise.* / org.* / system.*）⇒ `replace`（只读）
  //   ③④（cert_fill_param_def 的参数）⇒ `global`（可覆盖）
  const kind = card === 'global' ? kindOfRef(s.ref) : s.kind

  const e: Record<string, any> = { kind }
  if ((card === 'global' || card === 'headerFooter') && s.ref) e.ref = s.ref
  if (isAiKind(s.kind)) {
    e.hasParam = !!s.hasParam
    // ⛔ 只写 params（多参数），不再写单值 ref —— 避免同一份配置有两个参数真相源
    if (s.hasParam) {
      const refs = aiParamRefs(s)
      if (refs.length) e.params = refs
    }
    e.dependsOnEnterprise = !!s.dependsOnEnterprise
    if (s.prompt) e.prompt = s.prompt
  }

  return JSON.stringify({ sources: [e] })
}

/** 单条来源的一行摘要（列表「来源」列用） */
export function entrySummary(e: SourceSpecEntry): string {
  if (!e || !e.kind) return ''
  switch (e.kind) {
    case 'replace':
    case 'global': {
      const label = labelOfRef(e.ref)
      return `全局参数：${label || '（未选参数）'}${e.kind === 'replace' ? ' · 只读' : ''}`
    }
    case 'headerFooter': {
      const d = DOC_INFO_ITEMS.find((i) => i[0] === e.ref)
      return `文档信息：${d ? d[1] : e.ref || '（未选项）'}`
    }
    case 'manual':
      return '人工填写'
    default:
      if (isAiKind(e.kind)) {
        const bits: string[] = []
        const refs = aiParamRefs(e)
        if (refs.length) bits.push(`参数 ${refs.join('、')}`)
        if (e.dependsOnEnterprise) bits.push('依赖企业资料')
        return `${kindLabel(e.kind)}${bits.length ? `（${bits.join(' · ')}）` : ''}`
      }
      return e.kind
  }
}

/** 整条来源的一行摘要（列表列 + 抽屉标题用） */
export function summarizeSourceSpec(model: SourceSpecModel): string {
  return entrySummary(model.source)
}

/**
 * ★ 底部实时人话预览（`37` 号 §3.5 —— 「这是防错的关键」）。
 *
 * 目的不是复述配置，而是让实施人员**用业务语言确认逻辑对不对**。
 */
export function humanPreview(model: SourceSpecModel): string {
  const s = model.source
  if (!s.kind) return '尚未配置来源 —— 运行期该锚点将留空并挂人工待办。'

  switch (s.kind) {
    case 'replace':
    case 'global': {
      if (!s.ref) return '还没选具体参数 —— 请在上方「来源属性」里选一个。'
      const name = labelOfRef(s.ref)
      return s.kind === 'replace'
        ? `取「${name}」（**只读** —— 直读企业档案 / 机构 / 系统，企业改档案即改文档）。取不到就留空并挂人工待办。`
        : `取全局参数「${name}」的值（企业可在「企业资料参数」页覆盖）。取不到就留空并挂人工待办。`
    }

    case 'headerFooter': {
      const d = DOC_INFO_ITEMS.find((i) => i[0] === s.ref)
      return d
        ? `取文档信息「${d[1]}」；取不到就留空并挂人工待办。`
        : '还没选文档信息项 —— 请在上方「来源属性」里选一个。'
    }

    case 'manual':
      return '运行期挂人工待办，由审核专家填写。'

    default:
      if (isAiKind(s.kind)) {
        const src: string[] = []
        const refs = aiParamRefs(s)
        if (refs.length) src.push(`全局参数「${refs.join('、')}」`)
        if (s.dependsOnEnterprise) src.push('企业资料')
        const from = src.length ? `，输入是 ${src.join(' + ')}` : '，⚠️ 当前没有任何输入'
        return `由 AI 按提示词生成${from}；结果进「建议池」待人工确认后才生效。`
      }
      return `来源「${s.kind}」暂无预览。`
  }
}

/**
 * ★ 来源配置的校验（返回空数组 = 通过）。
 *
 * 【⚠️ 校验顺序 = 用户体验】多条规则并存时，**先报「你该做什么」，后报「为什么」**。
 *   反例（V3 原型实测踩过）：先报语义规则「参数=无 且 不依赖企业资料 ⇒ 提示词不能为空」，
 *   用户看到的是「为什么不行」，而真正该做的是「先把提示词填上」。
 */
export function validateSource(model: SourceSpecModel): string[] {
  const issues: string[] = []
  const s = model.source

  // ★ 未选来源**不阻塞保存** —— 清空配置是合法操作（列表页会用「未配来源」徽标提示）。
  //   阻塞的只有「选了来源但没配全」。
  if (!s.kind) return []

  const card = cardOf(s.kind)
  if (card === 'global' && !s.ref) issues.push('请选择具体的参数')
  if (card === 'headerFooter' && !s.ref) issues.push('请选择具体的文档信息项')

  if (isAiKind(s.kind)) {
    // ★ 直接规则在前（先告诉用户「该做什么」）
    if (!s.prompt || !s.prompt.trim()) issues.push('请填写提示词')
    // 语义规则在后（再解释「为什么」）
    else if (!s.hasParam && !s.dependsOnEnterprise)
      issues.push(
        '参数选了「无」且不依赖企业资料 —— 提示词将没有任何输入，请至少指定一个参数或勾选「依赖企业资料」',
      )
    if (s.hasParam && aiParamRefs(s).length === 0)
      issues.push('参数选了「有」—— 请指定至少一个全局参数')
  }

  return issues
}
