/**
 * composables/workflow/aiParams.ts — AI 节点「参数表」唯一口径（schema / 解析 / 强校验）
 *
 * 落实：docs/20-体系认证/03-详细设计/01-系统管理/工作流管理/04-设计器实施与验证/
 *       AI节点参数与审计字段统一-改造设计-V1.md §2.1 / §2.2 / §2.7
 *
 * 三条铁律（改本文件前先读）：
 *  ① **参数表存 `config.Params`**（用户裁决 ①：进 rule_json，零迁移）——⛔ 不要另起字段名。
 *  ② **参数即边**（用户裁决 ②）：`params[].nodeId → aiNode` 就是一条隐式依赖边，
 *     它只决定「执行顺序 / 预执行」，⛔ **不进路径枚举**（见 topology.ts 的 `implicitEdges`）。
 *  ③ **参数名 = 短 ASCII 标识符**（`p1`/`p2`），中文说明放 `label`。
 *     模型要完成「符号 → 值」的映射，中文参数名会显著提高弱模型（qwen-turbo）的映射错误率。
 *
 * 与后端的对应关系（⛔ 口径必须同步改，三套口径不一致是本次改造的病根）：
 *  - `PromptRenderer.Placeholder`（C#）= `PLACEHOLDER_PATTERN`（本文件）
 *  - `AiNodeExecutor.ParseDeclaredParams`（C#）= `parseAiParams`（本文件）
 *  - `AiNodeExecutor` 解析顺序：paramPool 已有 → 跳过；否则按 nodeId/title 双键查 sharedOutputs
 */

/** 参数来源类型：node = 画布节点输出；constant = 常量；skill = 方法预执行 */
export type AiParamSourceType = 'node' | 'constant' | 'skill'

/** 参数表单项（= `config.Params[]` 的元素） */
export interface AiParam {
  /** ★ 短 ASCII 标识符，提示词里写 `{{name}}`。必须匹配 `AI_PARAM_NAME_PATTERN` */
  name: string
  /** 中文说明 —— 给人看，也给模型看（注释式参数区里会原样带上） */
  label?: string
  sourceType?: AiParamSourceType
  /** ★★ 依赖边的 From：sourceType='node' 时必填，且必须是真实存在的节点 ID */
  nodeId?: string
  /** 源节点输出端口，缺省 `result` */
  portName?: string
  description?: string
}

/** §2.7.3：参数名合法形态 */
export const AI_PARAM_NAME_PATTERN = /^[a-zA-Z_][a-zA-Z0-9_]*$/

/**
 * `{{name}}` 占位符正则 —— **必须与后端 `PromptRenderer.Placeholder` 逐字同口径**。
 *
 * 允许：中文、字母、数字、下划线、点号 ⇒ 同时覆盖三种写法（§7 影响表）：
 *   `{{p1}}`（参数名，优先）｜`{{文档字段.result}}`（中文标题，兜底）｜`{{docField_n2.result}}`（nodeId，兜底）
 */
export const PLACEHOLDER_PATTERN = /\{\{([\w\u4e00-\u9fff][\w\u4e00-\u9fff.]*)\}\}/g

/** 从提示词里取出全部占位符 key（完整 key，含点号端口） */
export function extractPlaceholders(prompt?: string | null): string[] {
  if (!prompt) return []
  const out: string[] = []
  const re = new RegExp(PLACEHOLDER_PATTERN.source, PLACEHOLDER_PATTERN.flags)
  let m: RegExpExecArray | null
  while ((m = re.exec(prompt)) !== null) {
    if (!out.includes(m[1])) out.push(m[1])
  }
  return out
}

/** 拆 `文档字段.result` → { refKey, portName }；无点号则 portName 为空 */
export function splitRef(fullKey: string): { refKey: string; portName: string } {
  const lastDot = fullKey.lastIndexOf('.')
  if (lastDot < 0) return { refKey: fullKey, portName: '' }
  return { refKey: fullKey.slice(0, lastDot), portName: fullKey.slice(lastDot + 1) }
}

/**
 * 读 `config.Params`（数组或字符串化 JSON 均可 —— 落库前后两种形态都可能出现）。
 * ⚠️ 解析失败**静默返回空**：本函数供校验/拓扑等多处调用，⛔ 不许在这里抛错打断主流程。
 */
export function parseAiParams(config?: Record<string, any> | null): AiParam[] {
  const raw = config?.Params
  if (!raw) return []
  try {
    const arr = typeof raw === 'string' ? JSON.parse(raw) : raw
    if (!Array.isArray(arr)) return []
    return arr.filter((p: any) => p && typeof p === 'object')
  } catch {
    return []
  }
}

/** 节点是否声明了参数表（有 = 新方案，校验走 params；无 = 正则兜底） */
export function hasDeclaredParams(node?: { config?: Record<string, any> } | null): boolean {
  return parseAiParams(node?.config).length > 0
}

/** 提示词模板（读 `config.PromptTemplate` —— ⛔ 不是 `config.prompt`，那是 M1 踩过的坑） */
export function readPromptTemplate(node?: { config?: Record<string, any> } | null): string {
  const v = node?.config?.PromptTemplate
  return typeof v === 'string' ? v : ''
}

/**
 * ★ §2.7 配置期强校验 —— 保存时调用，**任一条不过即拒绝保存**。
 *
 * @param node   AI 节点（含 config.PromptTemplate / config.Params）
 * @param allNodes 画布全部节点（用于「nodeId 必须真实存在」「端口必须产出」）
 * @returns 错误文案列表；空数组 = 通过
 */
export function validateAiNodeParams(
  node: { nodeId?: string; id?: string; title?: string; nodeType?: string; config?: Record<string, any> },
  allNodes: Array<{ nodeId?: string; id?: string; nodeType?: string; classCode?: string; title?: string; outputPorts?: any[]; config?: Record<string, any> }>,
): string[] {
  const errors: string[] = []
  if (node?.nodeType && node.nodeType !== 'ai_node') return errors

  const prompt = readPromptTemplate(node)
  const params = parseAiParams(node.config)
  const nodeIdOf = (n: any) => n?.nodeId || n?.id || ''
  const selfId = nodeIdOf(node)

  // ① params 缺失时的兼容策略（§2.1）：从 promptTemplate 正则兜底解析，
  //    但解析出的 nodeId 必须能命中真实节点，否则配置期报错。
  if (params.length === 0) {
    for (const key of extractPlaceholders(prompt)) {
      const { refKey } = splitRef(key)
      const hit = allNodes.find((n) => nodeIdOf(n) === refKey || n?.title === refKey)
      if (!hit) {
        errors.push(
          `提示词占位符 {{${key}}} 既不是已登记的参数名，也没命中任何节点（节点 ID / 节点标题都查不到）`,
        )
      }
    }
    return errors
  }

  const paramNames = new Set<string>()
  for (const p of params) {
    const name = (p.name || '').trim()

    // §2.7.3：name 唯一 + 形态合法
    if (!name) {
      errors.push('存在未命名的参数 —— 每个参数都必须有 name')
      continue
    }
    if (!AI_PARAM_NAME_PATTERN.test(name)) {
      errors.push(`参数名「${name}」不合法 —— 只允许字母/数字/下划线，且不能以数字开头（如 p1）`)
      continue
    }
    if (paramNames.has(name)) errors.push(`参数名「${name}」重复 —— 同一节点内必须唯一`)
    paramNames.add(name)

    const sourceType = p.sourceType || 'node'

    // §2.7.2：params[].nodeId 必须是真实存在的节点 ID
    if (sourceType === 'node') {
      const srcId = p.nodeId || ''
      const src = allNodes.find((n) => nodeIdOf(n) === srcId)
      if (!srcId) {
        errors.push(`参数「${name}」未选择来源节点`)
        continue
      }
      if (!src) {
        errors.push(`参数「${name}」引用的节点「${srcId}」在画布上不存在 —— 保存被拒绝`)
        continue
      }
      if (nodeIdOf(src) === selfId) {
        errors.push(`参数「${name}」引用了 AI 节点自己 —— 会造成循环依赖`)
        continue
      }
      // §2.7.4：被引用节点类型必须产出该端口
      const port = p.portName || 'result'
      const ports = src.outputPorts || []
      if (ports.length > 0 && !ports.some((x: any) => x?.name === port)) {
        errors.push(
          `参数「${name}」引用的节点「${src.title || srcId}」不产出端口「${port}」（可选：${ports.map((x: any) => x?.name).join('、')}）`,
        )
      }
    }
  }

  // §2.7.1：promptTemplate 里每个 {{name}} 都能在 params 中找到，**或**能兜底命中真实节点
  // （后者是老写法 `{{文档字段.result}}` 的兼容通道 —— 设计 §2.1「兼容策略」明文允许）
  for (const key of extractPlaceholders(prompt)) {
    const { refKey } = splitRef(key)
    if (paramNames.has(refKey)) continue
    const hit = allNodes.find((n) => nodeIdOf(n) === refKey || n?.title === refKey)
    if (!hit) {
      errors.push(`提示词占位符 {{${key}}} 在参数表里没有对应参数，也没命中任何节点（节点 ID / 节点标题都查不到）`)
    }
  }

  return errors
}
