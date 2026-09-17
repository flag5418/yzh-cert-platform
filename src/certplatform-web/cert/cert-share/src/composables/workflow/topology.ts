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
const PROMPT_REF_PATTERN = /\{\{([a-zA-Z_]+_n\d+)(?:\.([a-zA-Z_][a-zA-Z0-9_]*))?\}\}/g

// ==================== 核心数据结构 ====================

export interface DependencyGraph {
  adjacency: Map<string, string[]>
  reverseAdj: Map<string, string[]>
  nodeMap: Map<string, any>
  dependencies: Array<{ source: string; target: string; type: string; handle?: string | null; refPort?: string; fromPort?: string; raw?: string; loose?: boolean }>
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
    stats: { totalNodes: 0, totalEdges: 0, explicitEdges: 0, implicitReferences: 0, aiPromptReferences: 0, startNodes: [], endNodes: [], branchNodes: [] }
  }

  if (!config || !config.nodes) { log('WARN', 'config 为空或缺少 nodes'); return graph }

  for (const node of config.nodes) {
    const nodeId = node.nodeId || node.id
    if (!nodeId) { log('WARN', '节点缺少 nodeId，跳过', node); continue }
    graph.adjacency.set(nodeId, [])
    graph.reverseAdj.set(nodeId, [])
    graph.nodeMap.set(nodeId, node)
    graph.stats.totalNodes++
    const nodeType = node.nodeType || node.classCode
    if (nodeType === 'start') graph.stats.startNodes.push(nodeId)
    if (nodeType === 'end') graph.stats.endNodes.push(nodeId)
    if (nodeType === 'branch') graph.stats.branchNodes.push(nodeId)
  }

  for (const edge of (config.edges || [])) {
    const source = edge.source, target = edge.target
    if (!source || !target) continue
    if (!graph.nodeMap.has(source) || !graph.nodeMap.has(target)) continue
    graph.adjacency.get(source)!.push(target)
    graph.reverseAdj.get(target)!.push(source)
    graph.dependencies.push({ source, target, type: 'edge', handle: edge.sourceHandle || null })
    graph.stats.explicitEdges++
  }

  graph.referencedNodes = new Set()
  for (const node of config.nodes) {
    const nodeId = node.nodeId || node.id
    const inputs = node.inputs || {}
    for (const [portName, value] of Object.entries(inputs)) {
      if (typeof value !== 'string') continue
      let match = value.match(REF_PATTERN)
      if (match) {
        const refNodeId = match[1]
        if (graph.nodeMap.has(refNodeId) && refNodeId !== nodeId) {
          if (!graph.adjacency.get(refNodeId)!.includes(nodeId)) {
            graph.adjacency.get(refNodeId)!.push(nodeId)
            graph.reverseAdj.get(nodeId)!.push(refNodeId)
            graph.dependencies.push({ source: refNodeId, target: nodeId, type: 'reference', refPort: match[2], fromPort: portName })
            graph.stats.implicitReferences++
            graph.referencedNodes.add(refNodeId)
          }
        }
        continue
      }
      match = value.match(LOOSE_REF_PATTERN)
      if (match) {
        const refNodeId = match[1]
        if (graph.nodeMap.has(refNodeId) && refNodeId !== nodeId) {
          if (!graph.adjacency.get(refNodeId)!.includes(nodeId)) {
            graph.adjacency.get(refNodeId)!.push(nodeId)
            graph.reverseAdj.get(nodeId)!.push(refNodeId)
            graph.dependencies.push({ source: refNodeId, target: nodeId, type: 'reference', refPort: 'result', fromPort: portName, loose: true })
            graph.stats.implicitReferences++
            graph.referencedNodes.add(refNodeId)
          }
        }
      }
    }
  }

  for (const node of config.nodes) {
    const nodeId = node.nodeId || node.id
    const nodeType = node.nodeType || node.classCode
    if (nodeType !== 'ai_node') continue
    const prompt = node.config?.prompt || ''
    if (!prompt) continue
    let match
    const regex = new RegExp(PROMPT_REF_PATTERN.source, PROMPT_REF_PATTERN.flags)
    while ((match = regex.exec(prompt)) !== null) {
      const refNodeId = match[1]
      const refPort = match[2] || 'result'
      if (graph.nodeMap.has(refNodeId) && refNodeId !== nodeId) {
        if (!graph.adjacency.get(refNodeId)!.includes(nodeId)) {
          graph.adjacency.get(refNodeId)!.push(nodeId)
          graph.reverseAdj.get(nodeId)!.push(refNodeId)
          graph.dependencies.push({ source: refNodeId, target: nodeId, type: 'ai_reference', refPort, raw: match[0] })
          graph.stats.aiPromptReferences++
          graph.referencedNodes.add(refNodeId)
        }
      }
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
        nodeTypes: [...pathNodes, nodeId].map(id => graph.nodeMap.get(id)?.nodeType || 'unknown')
      })
      return
    }

    const outNodes = graph.adjacency.get(nodeId) || []
    if (outNodes.length === 0) return

    const nodeType = node.nodeType || node.classCode
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

export function detectCycles(graph: DependencyGraph, suspects: string[] | null = null): Array<{ cycle: string[]; type: string }> {
  const cycles: Array<{ cycle: string[]; type: string }> = []
  const visited = new Set<string>()
  const recursionStack = new Set<string>()
  const path: string[] = []

  function dfs(nodeId: string) {
    visited.add(nodeId)
    recursionStack.add(nodeId)
    path.push(nodeId)
    for (const next of (graph.adjacency.get(nodeId) || [])) {
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
      for (const next of (graph.adjacency.get(node) || [])) { if (remaining.includes(next)) dfs(next) }
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
    const nodeType = node?.nodeType || node?.classCode
    if (nodeType === 'start' || nodeType === 'end') continue

    const fromStart = reachableFromStart.has(nodeId)
    const toEnd = canReachEnd.has(nodeId)
    const isReferenced = referencedNodes.has(nodeId)

    if (!fromStart && !toEnd && !isReferenced) {
      result.warnings.push({ code: 'ORPHAN_NODE', severity: 'warning', node: nodeId, message: `孤立节点 ${nodeId}（无输入无输出）` })
    } else if (!toEnd) {
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
    const nodeType = node.nodeType || node.classCode
    if (nodeType === 'skill' && !node.skillCode) {
      result.warnings.push({ code: 'MISSING_SKILL_CODE', severity: 'warning', node: nodeId, message: `功能节点 ${nodeId} 缺少 skillCode` })
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
    const title = node?.title || nodeId
    const type = node?.nodeType || node?.classCode || ''
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
