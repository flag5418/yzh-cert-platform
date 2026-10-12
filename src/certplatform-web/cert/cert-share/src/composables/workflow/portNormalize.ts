/**
 * 端口字段名归一化 —— 后端 PascalCase → 前端 PortDef 小写（★ 2026-10-11）
 *
 * ── 背景（真缺陷，非猜测）──────────────────────────────────────────────
 * `GET /api/Admin/Workflow/WfSkill/metadata` 返回的 `InputPorts` 是 **PascalCase**：
 *   { "Name": "field_code", "Label": "字段编码，如 …", "Type": "string",
 *     "Required": true, "Description": "…", "BindMode": "ConstantOnly" }
 * 而前端 `PortDef`（specialNodes.ts）与全部消费方（NodePropertyForm / PortControl）
 * 读的是**小写**：`port.name` / `port.label` / `port.bindMode` / `port.required` …
 *
 * 后端 DTO 注释写着「与前端 PortDef 字段对齐」—— **只对齐了 C# 属性名，没对齐 JSON**。
 * 本项目铁律「响应恒 PascalCase」，所以序列化出来必然是大写开头。
 *
 * ── 症状（用户实测：拖「获取字段值」节点到画布）────────────────────────
 *   · 端口标题**整片空白**（`port.label || port.name` 双双 undefined）
 *   · `bindMode` 失效 ⇒ 一律回落 `LinkOrConstant`
 *     ⇒ **本该「仅常量输入」的参数被渲染成「选择上游节点」下拉**（用户看到的 3 个下拉）
 *   · `required` 失效 ⇒ REQ 标签不显示
 *   · `description` 失效 ⇒ 端口说明不显示
 *   · `enumSource` 失效 ⇒ compare 节点的「运算符」枚举下拉出不来
 *   · `port.name` 为空还会连带 `:key` 重复、`inputs` 写进 `undefined` 键
 *
 * 特殊节点（start / end / branch / ai_node / constant / loop / docField / docTable）的端口
 * 来自前端 `specialNodes.ts`（本来就是小写）⇒ **不受影响**。
 * 这正是用户所说「很多节点拖到画布都有这个问题，但不是全部」的原因。
 *
 * ── 为什么不在后端改 ──────────────────────────────────────────────────
 * 本项目铁律「**响应恒 PascalCase**」，全仓前端都按 PascalCase 读（`s.SkillCode`、`c.Value`…）。
 * 把 DTO 改成 camelCase 会破坏全仓约定。
 * ⇒ 正确修法 = 在**数据进入前端模型的边界**做一次归一化。
 *
 * ── 用法 ─────────────────────────────────────────────────────────────
 *   const ports = normalizePorts(raw.InputPorts)   // 幂等，可重复调用
 *
 * 幂等保证：已是小写（或大小写混合）时保留小写值、只删多余的大写键；
 * 只映射已知键，其余字段原样透传。
 */

/** PascalCase → 前端 PortDef 小写字段名 */
const PORT_KEY_MAP: Record<string, string> = {
  Name: 'name',
  Label: 'label',
  Type: 'type',
  Required: 'required',
  AllowEmpty: 'allowEmpty',
  DefaultValue: 'defaultValue',
  Description: 'description',
  BindMode: 'bindMode',
  EnumSource: 'enumSource',
  // 以下三个后端 SkillPortDto 目前没有，前端 PortDef 有 —— 一并映射，防将来后端补上
  Display: 'display',
  Role: 'role',
  MaxIn: 'maxIn',
}

/**
 * 单个端口归一化（幂等）。
 * 非对象（null / 字符串 / 数组）原样返回 —— 调用方不必先做类型判断。
 */
export function normalizePort(raw: any): any {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return raw
  const out: any = { ...raw }
  for (const pascal of Object.keys(PORT_KEY_MAP)) {
    if (!(pascal in out)) continue
    const camel = PORT_KEY_MAP[pascal]
    // 小写优先：两者并存说明已经归一化过，不覆盖
    if (out[camel] === undefined || out[camel] === null) out[camel] = out[pascal]
    delete out[pascal]
  }
  return out
}

/** 端口数组归一化。非数组返回 `[]`（调用方拿到的一定是可迭代的数组）。 */
export function normalizePorts(raw: any): any[] {
  if (!Array.isArray(raw)) return []
  return raw.map(normalizePort)
}
