/**
 * workflow-designer/model/topology.ts
 * 工作流拓扑分析引擎 —— 核心算法层
 *
 * 职责：
 * 1. 从 workflow_config JSON 构建依赖图（边 ∪ AI提示词引用）
 * 2. 拓扑排序（Kahn 算法）
 * 3. 提取从 start 到所有 end 的完整路径
 * 4. 依赖环检测
 * 5. 拓扑完整性校验
 */

// ==================== 日志系统 ====================
import { parseAiParams, validateAiNodeParams } from './aiParams'

type LogLevel = 'DEBUG' | 'INFO' | 'WARN' | 'ERROR'

const LOG_LEVELS: Record<LogLevel, number> = { DEBUG: 0, INFO: 1, WARN: 2, ERROR: 3 }

let currentLogLevel = LOG_LEVELS.DEBUG
let logEnabled = true

export function setLogLevel(level: LogLevel): void {
  currentLogLevel = LOG_LEVELS[level] ?? LOG_LEVELS.DEBUG
}

export function setLogEnabled(enabled: boolean): void {
  logEnabled = enabled
}

function log(level: LogLevel, message: string, data?: any): void {
  if (!logEnabled || LOG_LEVELS[level] < currentLogLevel) return
  const timestamp = new Date().toISOString().substr(11, 12)
  const prefix = `[Topology:${timestamp}]`
  const fullMessage = `${prefix} [${level}] ${message}`
  switch (level) {
    case 'ERROR': console.error(fullMessage, data ?? ''); break
    case 'WARN': console.warn(fullMessage, data ?? ''); break
    case 'INFO': console.info(fullMessage, data ?? ''); break
    default: console.log(fullMessage, data ?? '')
  }
}

// ==================== 常量定义 ====================

export const SPECIAL_NODE_TYPES = ['start', 'end', 'branch', 'ai_node', 'docField', 'docTable']
export const BRANCH_ANCHORS = ['success', 'failure']

const REF_PATTERN = /^([a-zA-Z_]+_n\d+)\.([a-zA-Z_][a-zA-Z0-9_]*)$/
const LOOSE_REF_PATTERN = /^([a-zA-Z_]+_n\d+)$/

/**
 * ★ 2026-10-14 修 M2：占位符正则放开中文标题与短参数名。
 *
 * <p>修复前：`/\{\{([a-zA-Z_]+_n\d+)...\}\}/g` **只认 nodeId 形态** ⇒ 用户实际写法
 * `{{文档字段.result}}` 与新方案 `{{p1}}` **都不匹配** ⇒ 隐式边永不生成、参数表形同虚设。</p>
 *
 * <p>口径必须与后端 `PromptRenderer.Placeholder`（C#）以及 `aiParams.ts` 的
 * `PLACEHOLDER_PATTERN` **三处逐字一致** —— 本次改造的病根就是「三套口径互不一致」。</p>
 */
const PROMPT_REF_PATTERN = /\{\{([\w\u4e00-\u9fff][\w\u4e00-\u9fff.]*)\}\}/g

// ==================== 核心数据结构 ====================

export interface DependencyGraph {
  adjacency: Map<string, string[]>
  reverseAdj: Map<string, string[]>
  nodeMap: Map<string, any>
  dependencies: Array<{ source: string; target: string; type: string; handle?: string | null; refPort?: string; fromPort?: string; raw?: string; loose?: boolean }>
  /**
   * ★ 2026-10-14（设计 §2.2「参数即边」关键约束）：**隐式边独立成集**。
   *
   * <p>隐式边 = 端口引用（`inputs` 里的 `nodeId.port`）+ AI 提示词/参数表引用。
   * 它们的语义是「**执行顺序 / 预执行前置**」，⛔ <b>不是</b>流程连线。</p>
   *
   * <p>修复前它们被直接 push 进 `adjacency`，而 `adjacency` 同时是
   * `extractAllPaths` 的路径枚举依据 ⇒ 产生**伪路径**（如 `start → docField_n2 → ai_n6`），
   * 让「执行了几条路径」失真。现在：<b>拓扑排序仍认</b>（走 `dependencies`），
   * <b>路径枚举不认</b>（只走 `adjacency`）。</p>
   */
  implicitEdges: Array<{ source: string; target: string; type: string; refPort?: string; fromPort?: string; raw?: string; paramName?: string }>
  stats: {
    totalNodes: number
    totalEdges: number
    explicitEdges: number
    implicitReferences: number
    aiPromptReferences: number
    startNodes: string[]
    endNodes: string[]
    branchNodes: string[]
  }
  referencedNodes?: Set<string>
  /**
   * ★ 2026-10-15：重复 nodeId 列表（坏数据，含 `ai_node_n1` 被生成两次这类）。
   *   `nodeMap` 是 Map，重复项会静默覆盖 ⇒ 单看 nodeMap 发现不了，必须在建图时留痕。
   *   落库后后端 `ToDictionary(n => n.NodeId)` 会抛
   *   "An item with the same key has already been added"（AI 节点测试 400 事故根因）。
   */
  duplicateNodeIds?: string[]
}

export interface PathResult {
  id: string
  nodes: string[]
  edges: string[]
  branchDecisions: Array<{ at: string; choice: string; port?: string }>
  endpoint: { start: string; end: string }
  nodeTypes: string[]
}

export interface TopologyValidation {
  valid: boolean
  errors: Array<{ code: string; severity: string; message: string; node?: string; cycle?: string[]; nodes?: string[] }>
  warnings: Array<{ code: string; severity: string; message: string; node?: string; nodes?: string[] }>
  stats: DependencyGraph['stats']
}

export interface TopologyAnalysis {
  graph: DependencyGraph
  sortResult: { order: string[]; hasCycle: boolean; cycles: string[][]; executed: string[]; remaining: string[] }
  paths: PathResult[]
  validation: TopologyValidation
  unreachable: { fromStart: Set<string>; toEnd: Set<string>; unreachable: string[] }
  summary: {
    totalNodes: number; totalEdges: number; totalDependencies: number; totalPaths: number
    startNodes: string[]; endNodes: string[]; branchNodes: string[]
    hasCycle: boolean; isValid: boolean; errorCount: number; warningCount: number; durationMs: number
  }
}

// ==================== 1. 构建依赖图 ====================

export function buildDependencyGraph(config: any): DependencyGraph {
  log('INFO', '=== 开始构建依赖图 ===')

  const graph: DependencyGraph = {
    adjacency: new Map(),
    reverseAdj: new Map(),
    nodeMap: new Map(),
    dependencies: [],
    implicitEdges: [],
    duplicateNodeIds: [],
    stats: { totalNodes: 0, totalEdges: 0, explicitEdges: 0, implicitReferences: 0, aiPromptReferences: 0, startNodes: [], endNodes: [], branchNodes: [] }
  }

  const configNodes = config?.Nodes || config?.nodes
  const configEdges = config?.Edges || config?.edges || []
  if (!config || !configNodes) { log('WARN', 'config 为空或缺少 nodes'); return graph }

  // ⚠️ 必须在任何 addImplicit 调用之前初始化（隐式边要往里登记「被引用的数据源」）
  graph.referencedNodes = new Set()

  for (const node of configNodes) {
    const nodeId = node.NodeId || node.nodeId || node.id
    if (!nodeId) { log('WARN', '节点缺少 nodeId，跳过', node); continue }
    graph.stats.totalNodes++
    if (graph.nodeMap.has(nodeId)) {
      // ★ 重复 nodeId：留痕给 validateTopology 报错（Map 会静默覆盖，这里不 set，保住首个）
      if (!graph.duplicateNodeIds!.includes(nodeId)) graph.duplicateNodeIds!.push(nodeId)
      log('ERROR', `检测到重复节点 ID: ${nodeId}`, node)
      continue
    }
    graph.adjacency.set(nodeId, [])
    graph.reverseAdj.set(nodeId, [])
    graph.nodeMap.set(nodeId, node)
    const nodeType = node.NodeType || node.nodeType || node.classCode
    if (nodeType === 'start') graph.stats.startNodes.push(nodeId)
    if (nodeType === 'end') graph.stats.endNodes.push(nodeId)
    if (nodeType === 'branch') graph.stats.branchNodes.push(nodeId)
  }

  /**
   * ★ 2026-10-14（设计 §2.2）：**隐式边唯一入口** —— 进 `implicitEdges` + `dependencies`，
   *   ⛔ **不进 `adjacency` / `reverseAdj`**。
   *
   *   - `dependencies`：拓扑排序 / 环检测用它 ⇒ 「先跑数据源、再跑 AI 节点」的执行顺序仍然成立；
   *   - `adjacency`：路径枚举 `extractAllPaths` 用它 ⇒ 不再产生 `start → docField → ai` 伪路径；
   *   - `referencedNodes`：孤立节点检测用它 ⇒ 数据源被引用即视为「已接线」，不再告警（§2.7.5）。
   */
  const addImplicit = (source: string, target: string, type: string, extra: Record<string, any> = {}) => {
    if (!graph.nodeMap.has(source) || !graph.nodeMap.has(target)) return
    if (source === target) return
    const dup = graph.implicitEdges.some((e) => e.source === source && e.target === target && e.type === type && e.raw === extra.raw && e.paramName === extra.paramName)
    if (dup) return
    graph.implicitEdges.push({ source, target, type, ...extra })
    graph.dependencies.push({ source, target, type, ...extra })
    graph.referencedNodes!.add(source)
  }

  for (const edge of configEdges) {
    const source = edge.Source ?? edge.source, target = edge.Target ?? edge.target
    if (!source || !target) continue
    if (!graph.nodeMap.has(source) || !graph.nodeMap.has(target)) continue
    graph.adjacency.get(source)!.push(target)
    graph.reverseAdj.get(target)!.push(source)
    graph.dependencies.push({ source, target, type: 'edge', handle: (edge.SourceHandle ?? edge.sourceHandle) || null })
    graph.stats.explicitEdges++
  }

  for (const node of configNodes) {
    const nodeId = node.NodeId || node.nodeId || node.id
    const inputs = node.Inputs || node.inputs || {}
    for (const [portName, value] of Object.entries(inputs)) {
      if (typeof value !== 'string') continue
      let match = value.match(REF_PATTERN)
      if (match) {
        const refNodeId = match[1]
        const before = graph.implicitEdges.length
        addImplicit(refNodeId, nodeId, 'reference', { refPort: match[2], fromPort: portName })
        if (graph.implicitEdges.length > before) graph.stats.implicitReferences++
        continue
      }
      match = value.match(LOOSE_REF_PATTERN)
      if (match) {
        const refNodeId = match[1]
        const before = graph.implicitEdges.length
        addImplicit(refNodeId, nodeId, 'reference', { refPort: 'result', fromPort: portName })
        if (graph.implicitEdges.length > before) graph.stats.implicitReferences++
      }
    }
  }

  // ── AI 节点引用：参数表优先（params），提示词正则兜底 ──
  //
  // ★ M1 修复 + P5：读 `config.PromptTemplate`（面板字段名），⛔ 不是 `config.prompt` ——
  //   读错字段 ⇒ `aiPromptReferences` 恒 0，整套隐式依赖静默失效。
  //
  // ★ 双键解析：占位符 refKey 可能是 **nodeId**（`docField_n2`）也可能是 **中文标题**（`文档字段`），
  //   与后端 `AiNodeExecutor.ResolveTemplateRefs` 的 nodeId/Title 双键口径保持一致。
  const resolveRefNodeId = (refKey: string): string | null => {
    if (graph.nodeMap.has(refKey)) return refKey
    for (const [id, n] of graph.nodeMap.entries()) {
      const t = n?.Title || n?.title
      if (t && t === refKey) return id
    }
    return null
  }

  for (const node of configNodes) {
    const nodeId = node.NodeId || node.nodeId || node.id
    const nodeType = node.NodeType || node.nodeType || node.classCode
    if (nodeType !== 'ai_node') continue

    const declared = parseAiParams(node.Config || node.config)
    if (declared.length > 0) {
      // ① 参数表（§2.1 / 用户裁决 ①②：`params[].nodeId` 即依赖边）
      for (const p of declared) {
        if ((p.sourceType || 'node') !== 'node') continue
        const srcId = p.nodeId || ''
        if (!srcId || srcId === nodeId) continue
        const before = graph.implicitEdges.length
        addImplicit(srcId, nodeId, 'ai_reference', { refPort: p.portName || 'result', paramName: p.name })
        if (graph.implicitEdges.length > before) graph.stats.aiPromptReferences++
      }
      continue
    }

    // ② 正则兜底（params 缺失 ⇒ 保留旧行为：从 {{引用}} 扫）
    const _aiCfg = node.Config || node.config
    const prompt = typeof _aiCfg?.PromptTemplate === 'string' ? _aiCfg.PromptTemplate : ''
    if (!prompt) continue
    let match
    const regex = new RegExp(PROMPT_REF_PATTERN.source, PROMPT_REF_PATTERN.flags)
    while ((match = regex.exec(prompt)) !== null) {
      const fullKey = match[1]
      const lastDot = fullKey.lastIndexOf('.')
      const refKey = lastDot >= 0 ? fullKey.slice(0, lastDot) : fullKey
      const refPort = lastDot >= 0 ? fullKey.slice(lastDot + 1) : 'result'
      const refNodeId = resolveRefNodeId(refKey)
      if (!refNodeId || refNodeId === nodeId) continue
      const before = graph.implicitEdges.length
      addImplicit(refNodeId, nodeId, 'ai_reference', { refPort, raw: match[0] })
      if (graph.implicitEdges.length > before) graph.stats.aiPromptReferences++
    }
  }

  log('INFO', '=== 依赖图构建完成 ===')
  return graph
}

// ==================== 2. 拓扑排序 ====================

export function topologicalSort(graph: DependencyGraph): { order: string[]; hasCycle: boolean; cycles: string[][]; executed: string[]; remaining: string[] } {
  log('INFO', '=== 开始拓扑排序（Kahn 算法） ===')
  const nodeIds = Array.from(graph.nodeMap.keys())
  const inDegree = new Map<string, number>()
  const adj = new Map<string, string[]>()

  for (const id of nodeIds) { inDegree.set(id, 0); adj.set(id, []) }
  for (const dep of graph.dependencies) {
    if (graph.nodeMap.has(dep.source) && graph.nodeMap.has(dep.target)) {
      inDegree.set(dep.target, (inDegree.get(dep.target) || 0) + 1)
      adj.get(dep.source)!.push(dep.target)
    }
  }

  const queue: string[] = []
  for (const [id, deg] of inDegree.entries()) { if (deg === 0) queue.push(id) }
  queue.sort((a, b) => {
    const aIsStart = graph.nodeMap.get(a)?.nodeType === 'start' ? 0 : 1
    const bIsStart = graph.nodeMap.get(b)?.nodeType === 'start' ? 0 : 1
    return aIsStart - bIsStart
  })

  const order: string[] = []
  while (queue.length > 0) {
    const current = queue.shift()!
    order.push(current)
    for (const next of (adj.get(current) || [])) {
      const newDeg = inDegree.get(next)! - 1
      inDegree.set(next, newDeg)
      if (newDeg === 0) queue.push(next)
    }
  }

  const hasCycle = order.length < nodeIds.length
  const remaining = nodeIds.filter(id => !order.includes(id))
  const cycles = hasCycle ? findCycles(graph, remaining) : []

  return { order, hasCycle, cycles, executed: order, remaining }
}

// ==================== 3. 路径提取 ====================

export function extractAllPaths(graph: DependencyGraph): PathResult[] {
  log('INFO', '=== 开始提取拓扑路径 ===')
  const paths: PathResult[] = []
  const startNodes = graph.stats.startNodes
  const endNodes = new Set(graph.stats.endNodes)

  if (startNodes.length === 0 || endNodes.size === 0) return paths

  let pathId = 0

  function dfs(nodeId: string, pathNodes: string[], pathEdges: string[], branchDecisions: PathResult['branchDecisions'], visited: Set<string>) {
    const node = graph.nodeMap.get(nodeId)
    if (!node) return
    if (visited.has(nodeId)) return

    const newVisited = new Set(visited)
    newVisited.add(nodeId)

    if (endNodes.has(nodeId)) {
      pathId++
      paths.push({
        id: `path-${pathId}`,
        nodes: [...pathNodes, nodeId],
        edges: [...pathEdges],
        branchDecisions: [...branchDecisions],
        endpoint: { start: pathNodes[0] || nodeId, end: nodeId },
        nodeTypes: [...pathNodes, nodeId].map(id => { const n = graph.nodeMap.get(id); return n?.NodeType || n?.nodeType || 'unknown' })
      })
      return
    }

    const outNodes = graph.adjacency.get(nodeId) || []
    if (outNodes.length === 0) return

    const nodeType = node?.NodeType || node?.nodeType || node?.classCode
    if (nodeType === 'branch') {
      const branchEdges = graph.dependencies.filter(d => d.source === nodeId && d.type === 'edge')
      const successEdges = branchEdges.filter(e => e.handle === 'success')
      const failureEdges = branchEdges.filter(e => e.handle === 'failure')

      for (const edge of successEdges) {
        dfs(edge.target, [...pathNodes, nodeId], [...pathEdges, `${nodeId}→${edge.target}`], [...branchDecisions, { at: nodeId, choice: 'success', port: edge.refPort }], newVisited)
      }
      for (const edge of failureEdges) {
        dfs(edge.target, [...pathNodes, nodeId], [...pathEdges, `${nodeId}→${edge.target}`], [...branchDecisions, { at: nodeId, choice: 'failure', port: edge.refPort }], newVisited)
      }
    } else {
      for (const nextId of outNodes) {
        dfs(nextId, [...pathNodes, nodeId], [...pathEdges, `${nodeId}→${nextId}`], branchDecisions, newVisited)
      }
    }
  }

  for (const startId of startNodes) { dfs(startId, [], [], [], new Set()) }
  log('INFO', `共发现 ${paths.length} 条路径`)
  return paths
}

// ==================== 4. 环检测 ====================

/**
 * ★ 2026-10-14：环检测改用 **`dependencies`（物理边 ∪ 隐式边）** 而不是 `adjacency`。
 *
 * <p>原因：隐式边（端口引用 / AI 提示词引用）已按 §2.2 移出 `adjacency`。
 * 若环检测仍只看 `adjacency`，「A 的提示词引用 B、B 又引用 A」这类**真环会被漏掉**
 * —— 拓扑排序（走 `dependencies`）会判定 `hasCycle=true`、但 `cycles` 却是空数组，
 * 校验面板一句环都不报，用户无从下手。</p>
 *
 * <p>改造前后**环的识别集合不变**（改造前 `adjacency` 的内容恰好等于现在的 `dependencies`），
 * 只是把口径对齐到「依赖」这一层。</p>
 */
function depAdjacency(graph: DependencyGraph): Map<string, string[]> {
  const adj = new Map<string, string[]>()
  for (const id of graph.nodeMap.keys()) adj.set(id, [])
  for (const dep of graph.dependencies) {
    if (!graph.nodeMap.has(dep.source) || !graph.nodeMap.has(dep.target)) continue
    adj.get(dep.source)!.push(dep.target)
  }
  return adj
}

export function detectCycles(graph: DependencyGraph, suspects: string[] | null = null): Array<{ cycle: string[]; type: string }> {
  const cycles: Array<{ cycle: string[]; type: string }> = []
  const visited = new Set<string>()
  const recursionStack = new Set<string>()
  const path: string[] = []
  const adjacency = depAdjacency(graph)

  function dfs(nodeId: string) {
    visited.add(nodeId)
    recursionStack.add(nodeId)
    path.push(nodeId)
    for (const next of (adjacency.get(nodeId) || [])) {
      if (!visited.has(next)) { dfs(next) }
      else if (recursionStack.has(next)) {
        const cycleStart = path.indexOf(next)
        cycles.push({ cycle: [...path.slice(cycleStart), next], type: 'hard' })
      }
    }
    path.pop()
    recursionStack.delete(nodeId)
  }

  const nodesToCheck = suspects && suspects.length > 0 ? suspects : Array.from(graph.nodeMap.keys())
  for (const id of nodesToCheck) { if (!visited.has(id)) dfs(id) }
  return cycles
}

function findCycles(graph: DependencyGraph, remaining: string[]): string[][] {
  const cycles: string[][] = []
  if (!remaining || remaining.length === 0) return cycles
  const visited = new Set<string>()
  const adjacency = depAdjacency(graph)

  for (const start of remaining) {
    if (visited.has(start)) continue
    const path: string[] = []
    const pathSet = new Set<string>()

    function dfs(node: string) {
      if (pathSet.has(node)) { cycles.push(path.slice(path.indexOf(node))); return }
      if (visited.has(node)) return
      visited.add(node)
      path.push(node)
      pathSet.add(node)
      for (const next of (adjacency.get(node) || [])) { if (remaining.includes(next)) dfs(next) }
      path.pop()
      pathSet.delete(node)
    }
    dfs(start)
  }
  return cycles
}

// ==================== 5. 拓扑完整性校验 ====================

export function validateTopology(graph: DependencyGraph): TopologyValidation {
  log('INFO', '=== 开始拓扑完整性校验 ===')
  const result: TopologyValidation = { valid: true, errors: [], warnings: [], stats: graph.stats }

  if (graph.stats.startNodes.length === 0) {
    result.valid = false
    result.errors.push({ code: 'NO_START', severity: 'error', message: '缺少开始节点（start）' })
  }
  if (graph.stats.endNodes.length === 0) {
    result.valid = false
    result.errors.push({ code: 'NO_END', severity: 'error', message: '缺少结束节点（end）' })
  }

  // ★ 重复 nodeId（2026-10-15）：落库会让后端 ToDictionary 抛
  //   "An item with the same key has already been added" ⇒ 保存/测试直接 400。
  for (const dupId of graph.duplicateNodeIds || []) {
    result.valid = false
    result.errors.push({ code: 'DUPLICATE_NODE_ID', severity: 'error', node: dupId, message: `重复节点 ID：${dupId}（存在两个同 ID 节点，后端会拒绝执行，请删除多余的一个）` })
  }

  for (const branchId of graph.stats.branchNodes) {
    const outEdges = graph.dependencies.filter(d => d.source === branchId && d.type === 'edge')
    if (!outEdges.some(e => e.handle === 'success')) {
      result.valid = false
      result.errors.push({ code: 'BRANCH_SUCCESS_MISSING', severity: 'error', node: branchId, message: `分支节点 ${branchId} 的 success 锚点未连线` })
    }
    if (!outEdges.some(e => e.handle === 'failure')) {
      result.valid = false
      result.errors.push({ code: 'BRANCH_FAILURE_MISSING', severity: 'error', node: branchId, message: `分支节点 ${branchId} 的 failure 锚点未连线` })
    }
  }

  const cycles = detectCycles(graph)
  if (cycles.length > 0) {
    result.valid = false
    for (const c of cycles) {
      result.errors.push({ code: 'CYCLE_DETECTED', severity: 'error', cycle: c.cycle, message: `依赖环: ${c.cycle.join(' → ')}` })
    }
  }

  const reachableFromStart = bfsReachable(graph, graph.stats.startNodes, false)
  const canReachEnd = bfsReachable(graph, graph.stats.endNodes, true)
  const referencedNodes = graph.referencedNodes || new Set<string>()

  for (const nodeId of graph.nodeMap.keys()) {
    const node = graph.nodeMap.get(nodeId)
    const nodeType = node?.NodeType || node?.nodeType || node?.classCode
    if (nodeType === 'start' || nodeType === 'end') continue

    const fromStart = reachableFromStart.has(nodeId)
    const toEnd = canReachEnd.has(nodeId)
    const isReferenced = referencedNodes.has(nodeId)

    if (!fromStart && !toEnd && !isReferenced) {
      result.warnings.push({ code: 'ORPHAN_NODE', severity: 'warning', node: nodeId, message: `孤立节点 ${nodeId}（无输入无输出）` })
      continue
    }

    // ★ 2026-10-14（§2.7.5 / §2.2）：**被引用的数据源节点不再判「死路」**。
    //
    //   `docField` / `docTable` 是**数据源**，不是流程步骤：它没有物理出边、本来就不该到 end。
    //   隐式边移出 `adjacency` 之后，它的可达性只体现在 `referencedNodes` 上 ——
    //   若仍按「到不了 end = 错误」判，每个合规的 AI 分支都会被误报成校验失败。
    //   判据：**没有物理出边 + 被引用** ⇒ 数据源，放行；有物理出边却到不了 end ⇒ 真死路，照报。
    if (!toEnd) {
      const hasPhysicalOut = (graph.adjacency.get(nodeId) || []).length > 0
      if (isReferenced && !hasPhysicalOut) continue
      result.valid = false
      result.errors.push({ code: 'CANNOT_REACH_END', severity: 'error', node: nodeId, message: `节点 ${nodeId} 无法到达任何 end 节点（死路）` })
    }
  }

  for (const endId of graph.stats.endNodes) {
    if (!reachableFromStart.has(endId)) {
      result.valid = false
      result.errors.push({ code: 'END_UNREACHABLE', severity: 'error', node: endId, message: `结束节点 ${endId} 从 start 不可达` })
    }
  }

  for (const [nodeId, node] of graph.nodeMap.entries()) {
    const nodeType = node.NodeType || node.nodeType || node.classCode
    if (nodeType === 'skill' && !(node.SkillCode || node.skillCode)) {
      result.warnings.push({ code: 'MISSING_SKILL_CODE', severity: 'warning', node: nodeId, message: `功能节点 ${nodeId} 缺少 skillCode` })
    }

    // ★ §2.7 配置期强校验（AI 节点参数表）—— 任一不过 ⇒ 置 valid=false，
    //   于是「校验」按钮与「执行验证」都会被挡住；保存拦截见 WorkflowDesigner.handleSave。
    if (nodeType === 'ai_node') {
      for (const msg of validateAiNodeParams(node, Array.from(graph.nodeMap.values()))) {
        result.valid = false
        result.errors.push({ code: 'AI_PARAM_INVALID', severity: 'error', node: nodeId, message: `AI 节点「${node.Title || node.title || nodeId}」：${msg}` })
      }
    }
  }

  return result
}

function bfsReachable(graph: DependencyGraph, startNodes: string[], reverse: boolean): Set<string> {
  const visited = new Set<string>()
  const queue = [...startNodes]
  for (const s of startNodes) visited.add(s)
  while (queue.length > 0) {
    const current = queue.shift()!
    const adj = reverse ? graph.reverseAdj.get(current) : graph.adjacency.get(current)
    for (const next of (adj || [])) {
      if (!visited.has(next)) { visited.add(next); queue.push(next) }
    }
  }
  return visited
}

// ==================== 6. 查找不可达节点 ====================

export function findUnreachableNodes(graph: DependencyGraph): { fromStart: Set<string>; toEnd: Set<string>; unreachable: string[] } {
  const fromStart = bfsReachable(graph, graph.stats.startNodes, false)
  const toEnd = bfsReachable(graph, graph.stats.endNodes, true)
  const unreachable: string[] = []
  for (const nodeId of graph.nodeMap.keys()) {
    if (!fromStart.has(nodeId) || !toEnd.has(nodeId)) unreachable.push(nodeId)
  }
  return { fromStart, toEnd, unreachable }
}

// ==================== 7. 综合拓扑分析（一站式） ====================

export function analyzeTopology(config: any): TopologyAnalysis {
  const startTime = performance.now()
  const graph = buildDependencyGraph(config)
  const sortResult = topologicalSort(graph)
  const paths = extractAllPaths(graph)
  const validation = validateTopology(graph)
  const unreachable = findUnreachableNodes(graph)
  const duration = (performance.now() - startTime).toFixed(2)

  return {
    graph, sortResult, paths, validation, unreachable,
    summary: {
      totalNodes: graph.stats.totalNodes, totalEdges: graph.stats.explicitEdges,
      totalDependencies: graph.dependencies.length, totalPaths: paths.length,
      startNodes: graph.stats.startNodes, endNodes: graph.stats.endNodes, branchNodes: graph.stats.branchNodes,
      hasCycle: sortResult.hasCycle, isValid: validation.valid,
      errorCount: validation.errors.length, warningCount: validation.warnings.length,
      durationMs: parseFloat(duration)
    }
  }
}

// ==================== 8. 路径格式化 ====================

export function formatPath(path: PathResult, nodeMap: Map<string, any>): string {
  return path.nodes.map((nodeId) => {
    const node = nodeMap.get(nodeId)
    const title = node?.Title || node?.title || nodeId
    const type = node?.NodeType || node?.nodeType || node?.classCode || ''
    const typeLabel = type === 'start' ? '🟢' : type === 'end' ? '🔴' : type === 'branch' ? '🔀' : type === 'ai_node' ? '🤖' : ''
    let str = `${typeLabel} ${title}`
    const decision = path.branchDecisions.find(d => d.at === nodeId)
    if (decision) str += ` [${decision.choice === 'success' ? '✓ 成功' : '✗ 失败'}]`
    return str
  }).join(' → ')
}

export function summarizePaths(paths: PathResult[]): string {
  if (paths.length === 0) return '无有效路径'
  if (paths.length === 1) return '1 条路径'
  return `${paths.length} 条路径（含分支）`
}
