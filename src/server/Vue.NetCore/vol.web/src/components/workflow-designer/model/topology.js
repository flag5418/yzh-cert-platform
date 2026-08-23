/**
 * workflow-designer/model/topology.js
 * 工作流拓扑分析引擎 —— 核心算法层
 *
 * 职责：
 * 1. 从 workflow_config JSON 构建依赖图（边 ∪ AI提示词引用）
 * 2. 拓扑排序（Kahn 算法）
 * 3. 提取从 start 到所有 end 的完整路径
 * 4. 依赖环检测
 * 5. 拓扑完整性校验
 *
 * 详细日志输出：每个关键步骤都输出结构化日志，便于调试和排障
 *
 * 关联文档：
 * - docs/80-功能设计/01-系统管理/工作流管理/01-核心引擎/自定义工作流引擎-功能设计-V1.md §3.4
 * - docs/80-功能设计/01-系统管理/工作流管理/01-核心引擎/AI提示词规则-功能设计-V1.md §六
 */

// ==================== 日志系统 ====================

/** 日志级别 */
const LOG_LEVELS = { DEBUG: 0, INFO: 1, WARN: 2, ERROR: 3 }

/** 当前日志级别（生产环境可改为 INFO） */
let currentLogLevel = LOG_LEVELS.DEBUG

/** 日志输出开关 */
let logEnabled = true

/**
 * 设置日志级别
 * @param {'DEBUG'|'INFO'|'WARN'|'ERROR'} level
 */
export function setLogLevel(level) {
  currentLogLevel = LOG_LEVELS[level] ?? LOG_LEVELS.DEBUG
}

/**
 * 启用/禁用日志
 * @param {boolean} enabled
 */
export function setLogEnabled(enabled) {
  logEnabled = enabled
}

/**
 * 输出日志（带时间戳、级别、模块前缀）
 * @param {'DEBUG'|'INFO'|'WARN'|'ERROR'} level
 * @param {string} message
 * @param {Object} [data] - 附加数据
 */
function log(level, message, data) {
  if (!logEnabled || LOG_LEVELS[level] < currentLogLevel) return
  const timestamp = new Date().toISOString().substr(11, 12)
  const prefix = `[Topology:${timestamp}]`
  const fullMessage = `${prefix} [${level}] ${message}`
  switch (level) {
    case 'ERROR':
      console.error(fullMessage, data ?? '')
      break
    case 'WARN':
      console.warn(fullMessage, data ?? '')
      break
    case 'INFO':
      console.info(fullMessage, data ?? '')
      break
    default:
      console.log(fullMessage, data ?? '')
  }
}

// ==================== 常量定义 ====================

/** 特殊节点类型（引擎内置，不落 wf_skill 表） */
export const SPECIAL_NODE_TYPES = ['start', 'end', 'branch', 'ai_node', 'docField', 'docTable']

/** 分支节点锚点 */
export const BRANCH_ANCHORS = ['success', 'failure']

/** 引用语法正则 */
// 精确引用：nX.port（如 docTable_n1.rows）
const REF_PATTERN = /^([a-zA-Z_]+_n\d+)\.([a-zA-Z_][a-zA-Z0-9_]*)$/
// 宽松引用：纯 nX（如 docTable_n1，默认引用主输出端口）
const LOOSE_REF_PATTERN = /^([a-zA-Z_]+_n\d+)$/
const CTX_PATTERN = /^ctx\.([a-zA-Z_][a-zA-Z0-9_.]*)$/
const PROMPT_REF_PATTERN = /\{\{([a-zA-Z_]+_n\d+)(?:\.([a-zA-Z_][a-zA-Z0-9_]*))?\}\}/g

// ==================== 核心数据结构 ====================

/**
 * 依赖图结构
 * @typedef {Object} DependencyGraph
 * @property {Map<string, string[]>} adjacency - 邻接表（source → [target1, target2, ...]）
 * @property {Map<string, string[]>} reverseAdj - 反向邻接表（target → [source1, source2, ...]）
 * @property {Map<string, Object>} nodeMap - 节点映射（nodeId → node）
 * @property {Array<{source: string, target: string, type: 'edge'|'reference'}>} dependencies - 所有依赖关系
 * @property {Object} stats - 统计信息
 */

// ==================== 1. 构建依赖图 ====================

/**
 * 从 workflow_config 构建依赖图
 *
 * 依赖图 = 显式边 ∪ 隐式引用（命名引用）
 * - 显式边：edges[].source → edges[].target
 * - 隐式引用：扫描每个节点 inputs[].value 中的 "nX.port" 模式
 * - AI提示词引用：扫描 ai_node.config.prompt 中的 {{nX.port}} 模式
 *
 * @param {Object} config - workflow_config JSON
 * @returns {DependencyGraph} 依赖图
 */
export function buildDependencyGraph(config) {
  log('INFO', '=== 开始构建依赖图 ===')
  log('DEBUG', '输入 config', { nodeCount: config?.nodes?.length, edgeCount: config?.edges?.length })

  const graph = {
    adjacency: new Map(),
    reverseAdj: new Map(),
    nodeMap: new Map(),
    dependencies: [],
    stats: {
      totalNodes: 0,
      totalEdges: 0,
      explicitEdges: 0,
      implicitReferences: 0,
      aiPromptReferences: 0,
      startNodes: [],
      endNodes: [],
      branchNodes: []
    }
  }

  if (!config || !config.nodes) {
    log('WARN', 'config 为空或缺少 nodes')
    return graph
  }

  // 步骤 1：初始化邻接表和节点映射
  log('DEBUG', '步骤 1：初始化节点映射')
  for (const node of config.nodes) {
    const nodeId = node.nodeId || node.id
    if (!nodeId) {
      log('WARN', '节点缺少 nodeId，跳过', node)
      continue
    }
    graph.adjacency.set(nodeId, [])
    graph.reverseAdj.set(nodeId, [])
    graph.nodeMap.set(nodeId, node)
    graph.stats.totalNodes++

    // 分类统计
    const nodeType = node.nodeType || node.classCode
    if (nodeType === 'start') graph.stats.startNodes.push(nodeId)
    if (nodeType === 'end') graph.stats.endNodes.push(nodeId)
    if (nodeType === 'branch') graph.stats.branchNodes.push(nodeId)

    log('DEBUG', `  注册节点: ${nodeId} (type=${nodeType}, title=${node.title || '-'})`)
  }

  // 步骤 2：处理显式边
  log('DEBUG', '步骤 2：处理显式边')
  for (const edge of (config.edges || [])) {
    const source = edge.source
    const target = edge.target
    if (!source || !target) {
      log('WARN', '边缺少 source 或 target', edge)
      continue
    }
    if (!graph.nodeMap.has(source) || !graph.nodeMap.has(target)) {
      log('WARN', `边引用了不存在的节点: ${source} → ${target}`)
      continue
    }
    graph.adjacency.get(source).push(target)
    graph.reverseAdj.get(target).push(source)
    graph.dependencies.push({ source, target, type: 'edge', handle: edge.sourceHandle || null })
    graph.stats.explicitEdges++
    log('DEBUG', `  显式边: ${source} → ${target}${edge.sourceHandle ? ` [${edge.sourceHandle}]` : ''}`)
  }

  // 步骤 3：扫描隐式引用（inputs 中的 nX.port 或 nX 引用）
  log('DEBUG', '步骤 3：扫描隐式引用（inputs）')
  graph.referencedNodes = new Set() // 被其他节点引用的节点集合（用于后续可达性判定）
  for (const node of config.nodes) {
    const nodeId = node.nodeId || node.id
    const inputs = node.inputs || {}
    for (const [portName, value] of Object.entries(inputs)) {
      if (typeof value !== 'string') continue
      // 先尝试精确匹配 nX.port
      let match = value.match(REF_PATTERN)
      if (match) {
        const refNodeId = match[1]
        const refPort = match[2]
        if (graph.nodeMap.has(refNodeId) && refNodeId !== nodeId) {
          // 添加隐式依赖边
          if (!graph.adjacency.get(refNodeId).includes(nodeId)) {
            graph.adjacency.get(refNodeId).push(nodeId)
            graph.reverseAdj.get(nodeId).push(refNodeId)
            graph.dependencies.push({
              source: refNodeId,
              target: nodeId,
              type: 'reference',
              refPort,
              fromPort: portName
            })
            graph.stats.implicitReferences++
            graph.referencedNodes.add(refNodeId)
            log('DEBUG', `  隐式引用(精确): ${refNodeId}.${refPort} → ${nodeId}.${portName}`)
          }
        }
        continue
      }
      // 再尝试宽松匹配 nX（纯节点 ID，无端口）
      match = value.match(LOOSE_REF_PATTERN)
      if (match) {
        const refNodeId = match[1]
        if (graph.nodeMap.has(refNodeId) && refNodeId !== nodeId) {
          if (!graph.adjacency.get(refNodeId).includes(nodeId)) {
            graph.adjacency.get(refNodeId).push(nodeId)
            graph.reverseAdj.get(nodeId).push(refNodeId)
            graph.dependencies.push({
              source: refNodeId,
              target: nodeId,
              type: 'reference',
              refPort: 'result',
              fromPort: portName,
              loose: true
            })
            graph.stats.implicitReferences++
            graph.referencedNodes.add(refNodeId)
            log('DEBUG', `  隐式引用(宽松): ${refNodeId} → ${nodeId}.${portName} (默认result端口)`)
          }
        }
      }
    }
  }

  // 步骤 4：扫描 AI 提示词引用
  log('DEBUG', '步骤 4：扫描 AI 提示词引用')
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
        if (!graph.adjacency.get(refNodeId).includes(nodeId)) {
          graph.adjacency.get(refNodeId).push(nodeId)
          graph.reverseAdj.get(nodeId).push(refNodeId)
          graph.dependencies.push({
            source: refNodeId,
            target: nodeId,
            type: 'ai_reference',
            refPort,
            raw: match[0]
          })
          graph.stats.aiPromptReferences++
          graph.referencedNodes.add(refNodeId)
          log('DEBUG', `  AI提示词引用: ${refNodeId}.${refPort} → ${nodeId} (raw: ${match[0]})`)
        }
      }
    }
  }

  // 步骤 5：扫描 outputConfig 引用
  log('DEBUG', '步骤 5：扫描 outputConfig 引用')
  const outputConfig = config.outputConfig || {}
  for (const [key, val] of Object.entries(outputConfig)) {
    if (typeof val === 'string') {
      const match = val.match(REF_PATTERN)
      if (match) {
        const refNodeId = match[1]
        if (graph.nodeMap.has(refNodeId)) {
          log('DEBUG', `  outputConfig 引用: ${val} (key=${key})`)
        }
      }
    } else if (val && typeof val === 'object' && val.ref) {
      const match = val.ref.match(REF_PATTERN)
      if (match) {
        const refNodeId = match[1]
        if (graph.nodeMap.has(refNodeId)) {
          log('DEBUG', `  outputConfig.ref: ${val.ref} (key=${key}, default=${val.default})`)
        }
      }
    }
  }

  // 汇总日志
  log('INFO', '=== 依赖图构建完成 ===')
  log('INFO', '统计', {
    节点数: graph.stats.totalNodes,
    显式边: graph.stats.explicitEdges,
    隐式引用: graph.stats.implicitReferences,
    AI提示词引用: graph.stats.aiPromptReferences,
    总依赖: graph.dependencies.length,
    start节点: graph.stats.startNodes,
    end节点: graph.stats.endNodes,
    branch节点: graph.stats.branchNodes
  })

  return graph
}

// ==================== 2. 拓扑排序 ====================

/**
 * 拓扑排序（Kahn 算法）
 *
 * @param {DependencyGraph} graph - 依赖图
 * @returns {{ order: string[], hasCycle: boolean, cycles: string[][], executed: string[], remaining: string[] }}
 */
export function topologicalSort(graph) {
  log('INFO', '=== 开始拓扑排序（Kahn 算法） ===')

  const nodeIds = Array.from(graph.nodeMap.keys())
  const inDegree = new Map()
  const adj = new Map()

  // 初始化入度和邻接表
  for (const id of nodeIds) {
    inDegree.set(id, 0)
    adj.set(id, [])
  }
  for (const dep of graph.dependencies) {
    if (graph.nodeMap.has(dep.source) && graph.nodeMap.has(dep.target)) {
      inDegree.set(dep.target, (inDegree.get(dep.target) || 0) + 1)
      adj.get(dep.source).push(dep.target)
    }
  }

  log('DEBUG', '初始入度', Object.fromEntries(inDegree))

  // 找到所有入度为 0 的节点作为起始
  const queue = []
  for (const [id, deg] of inDegree.entries()) {
    if (deg === 0) queue.push(id)
  }

  // start 节点排在最前面
  queue.sort((a, b) => {
    const aIsStart = graph.nodeMap.get(a)?.nodeType === 'start' ? 0 : 1
    const bIsStart = graph.nodeMap.get(b)?.nodeType === 'start' ? 0 : 1
    return aIsStart - bIsStart
  })

  log('DEBUG', '初始队列（入度=0）', queue)

  const order = []
  let step = 0
  while (queue.length > 0) {
    const current = queue.shift()
    order.push(current)
    step++
    log('DEBUG', `  步骤 ${step}: 执行 ${current} (出队)`)

    for (const next of (adj.get(current) || [])) {
      const newDeg = inDegree.get(next) - 1
      inDegree.set(next, newDeg)
      log('DEBUG', `    → ${next} 入度减为 ${newDeg}`)
      if (newDeg === 0) {
        queue.push(next)
        log('DEBUG', `    → ${next} 入队`)
      }
    }
  }

  // 检测环
  const hasCycle = order.length < nodeIds.length
  const remaining = nodeIds.filter(id => !order.includes(id))
  const cycles = hasCycle ? findCycles(graph, remaining) : []

  if (hasCycle) {
    log('ERROR', '检测到依赖环！', { 已排序: order.length, 剩余: remaining, 环: cycles })
  } else {
    log('INFO', '拓扑排序完成（无环）', { order })
  }

  return {
    order,
    hasCycle,
    cycles,
    executed: order,
    remaining
  }
}

// ==================== 3. 路径提取 ====================

/**
 * 提取从 start 到所有 end 的完整拓扑线路
 *
 * 使用 DFS 遍历所有可能的路径，标注分支决策
 *
 * @param {DependencyGraph} graph - 依赖图
 * @returns {Array<PathResult>} 路径列表
 */
export function extractAllPaths(graph) {
  log('INFO', '=== 开始提取拓扑路径 ===')

  const paths = []
  const startNodes = graph.stats.startNodes
  const endNodes = new Set(graph.stats.endNodes)

  if (startNodes.length === 0) {
    log('ERROR', '没有 start 节点，无法提取路径')
    return paths
  }
  if (endNodes.size === 0) {
    log('ERROR', '没有 end 节点，无法提取路径')
    return paths
  }

  log('DEBUG', `start 节点: ${startNodes.join(', ')}`)
  log('DEBUG', `end 节点: ${Array.from(endNodes).join(', ')}`)

  let pathId = 0

  /**
   * DFS 遍历
   * @param {string} nodeId - 当前节点
   * @param {string[]} pathNodes - 当前路径上的节点序列
   * @param {string[]} pathEdges - 当前路径上的边序列
   * @param {Array} branchDecisions - 分支决策记录
   * @param {Set<string>} visited - 当前路径上的已访问节点（用于环检测）
   */
  function dfs(nodeId, pathNodes, pathEdges, branchDecisions, visited) {
    const node = graph.nodeMap.get(nodeId)
    if (!node) return

    // 环检测
    if (visited.has(nodeId)) {
      log('WARN', `路径中检测到环: ${pathNodes.join(' → ')} → ${nodeId}`)
      return
    }

    const newVisited = new Set(visited)
    newVisited.add(nodeId)

    // 到达 end 节点 → 记录路径
    if (endNodes.has(nodeId)) {
      pathId++
      const pathResult = {
        id: `path-${pathId}`,
        nodes: [...pathNodes, nodeId],
        edges: [...pathEdges],
        branchDecisions: [...branchDecisions],
        endpoint: { start: pathNodes[0] || nodeId, end: nodeId },
        nodeTypes: [...pathNodes, nodeId].map(id => graph.nodeMap.get(id)?.nodeType || 'unknown')
      }
      paths.push(pathResult)
      log('INFO', `  发现路径 #${pathId}: ${pathResult.nodes.join(' → ')}`)
      return
    }

    // 获取出边
    const outNodes = graph.adjacency.get(nodeId) || []
    if (outNodes.length === 0) {
      // 死路（未连接到 end）
      log('WARN', `死路: ${[...pathNodes, nodeId].join(' → ')} (无出边)`)
      return
    }

    const nodeType = node.nodeType || node.classCode
    if (nodeType === 'branch') {
      // 分支节点：分别遍历 success 和 failure 分支
      const branchEdges = graph.dependencies.filter(
        d => d.source === nodeId && d.type === 'edge'
      )
      const successEdges = branchEdges.filter(e => e.handle === 'success')
      const failureEdges = branchEdges.filter(e => e.handle === 'failure')

      log('DEBUG', `  分支节点 ${nodeId}: success=${successEdges.length}条, failure=${failureEdges.length}条`)

      for (const edge of successEdges) {
        const newDecisions = [...branchDecisions, { at: nodeId, choice: 'success', port: edge.refPort }]
        dfs(edge.target, [...pathNodes, nodeId], [...pathEdges, `${nodeId}→${edge.target}`], newDecisions, newVisited)
      }
      for (const edge of failureEdges) {
        const newDecisions = [...branchDecisions, { at: nodeId, choice: 'failure', port: edge.refPort }]
        dfs(edge.target, [...pathNodes, nodeId], [...pathEdges, `${nodeId}→${edge.target}`], newDecisions, newVisited)
      }

      // 处理未连线的情况（隐式提前终止）
      if (successEdges.length === 0) {
        log('WARN', `分支节点 ${nodeId} 的 success 锚点未连线（隐式提前终止）`)
      }
      if (failureEdges.length === 0) {
        log('WARN', `分支节点 ${nodeId} 的 failure 锚点未连线（隐式提前终止）`)
      }
    } else {
      // 非分支节点：遍历所有出边
      for (const nextId of outNodes) {
        const edgeKey = `${nodeId}→${nextId}`
        dfs(nextId, [...pathNodes, nodeId], [...pathEdges, edgeKey], branchDecisions, newVisited)
      }
    }
  }

  // 从每个 start 节点开始 DFS
  for (const startId of startNodes) {
    log('DEBUG', `从 start 节点 ${startId} 开始 DFS`)
    dfs(startId, [], [], [], new Set())
  }

  // 汇总
  log('INFO', '=== 路径提取完成 ===')
  log('INFO', `共发现 ${paths.length} 条路径`)
  for (const p of paths) {
    log('INFO', `  路径 ${p.id}: ${p.nodes.length} 节点, ${p.branchDecisions.length} 个分支决策`)
  }

  return paths
}

// ==================== 4. 环检测 ====================

/**
 * 依赖环检测（基于 DFS 的 Tarjan 简化版）
 *
 * @param {DependencyGraph} graph - 依赖图
 * @param {string[]} [suspects] - 疑似环节点（拓扑排序剩余节点）
 * @returns {Array<{ cycle: string[], type: 'hard'|'soft' }>} 环列表
 */
export function detectCycles(graph, suspects = null) {
  log('INFO', '=== 开始环检测 ===')

  const cycles = []
  const visited = new Set()
  const recursionStack = new Set()
  const path = []

  function dfs(nodeId) {
    visited.add(nodeId)
    recursionStack.add(nodeId)
    path.push(nodeId)

    for (const next of (graph.adjacency.get(nodeId) || [])) {
      if (!visited.has(next)) {
        dfs(next)
      } else if (recursionStack.has(next)) {
        // 发现环
        const cycleStart = path.indexOf(next)
        const cycle = path.slice(cycleStart)
        cycles.push({
          cycle: [...cycle, next],
          type: 'hard'
        })
        log('ERROR', `检测到硬环: ${cycle.join(' → ')} → ${next}`)
      }
    }

    path.pop()
    recursionStack.delete(nodeId)
  }

  // 如果提供了疑似节点，只检测这些节点
  const nodesToCheck = suspects && suspects.length > 0 ? suspects : Array.from(graph.nodeMap.keys())
  for (const id of nodesToCheck) {
    if (!visited.has(id)) {
      dfs(id)
    }
  }

  log('INFO', `环检测完成: 发现 ${cycles.length} 个环`)
  return cycles
}

/**
 * 查找具体环（辅助函数）
 * @param {DependencyGraph} graph
 * @param {string[]} remaining - 拓扑排序剩余节点
 * @returns {string[][]}
 */
function findCycles(graph, remaining) {
  const cycles = []
  if (!remaining || remaining.length === 0) return cycles

  const visited = new Set()
  for (const start of remaining) {
    if (visited.has(start)) continue
    const path = []
    const pathSet = new Set()

    function dfs(node) {
      if (pathSet.has(node)) {
        const cycleStart = path.indexOf(node)
        cycles.push(path.slice(cycleStart))
        return
      }
      if (visited.has(node)) return
      visited.add(node)
      path.push(node)
      pathSet.add(node)
      for (const next of (graph.adjacency.get(node) || [])) {
        if (remaining.includes(next)) dfs(next)
      }
      path.pop()
      pathSet.delete(node)
    }

    dfs(start)
  }

  return cycles
}

// ==================== 5. 拓扑完整性校验 ====================

/**
 * 拓扑完整性校验（九项）
 *
 * 1. 结构合法：JSON 可解析，nodes/edges 结构完整
 * 2. start 节点存在且唯一
 * 3. end 节点存在
 * 4. 节点 Skill 存在（功能节点有 skillCode）
 * 5. 端口引用可解析：nX.port 目标存在
 * 6. 无环：依赖图拓扑排序不失败
 * 7. 数据源存在：引用 fieldCode/tableCode 有效
 * 8. 输出完整：outputConfig 引用的端口存在
 * 9. logic/branch 双分支完整性：success/failure 锚点各至少一条出边
 *
 * @param {DependencyGraph} graph - 依赖图
 * @returns {TopologyValidation} 校验结果
 */
export function validateTopology(graph) {
  log('INFO', '=== 开始拓扑完整性校验 ===')

  const result = {
    valid: true,
    errors: [],
    warnings: [],
    stats: graph.stats
  }

  // 校验 1：start 节点
  log('DEBUG', '校验 1：start 节点检查')
  if (graph.stats.startNodes.length === 0) {
    result.valid = false
    result.errors.push({ code: 'NO_START', severity: 'error', message: '缺少开始节点（start）' })
    log('ERROR', '  ✗ 缺少 start 节点')
  } else if (graph.stats.startNodes.length > 1) {
    result.warnings.push({ code: 'MULTIPLE_STARTS', severity: 'warning', message: `存在 ${graph.stats.startNodes.length} 个 start 节点`, nodes: graph.stats.startNodes })
    log('WARN', `  ⚠ 多个 start 节点: ${graph.stats.startNodes.join(', ')}`)
  } else {
    log('INFO', `  ✓ start 节点: ${graph.stats.startNodes[0]}`)
  }

  // 校验 2：end 节点
  log('DEBUG', '校验 2：end 节点检查')
  if (graph.stats.endNodes.length === 0) {
    result.valid = false
    result.errors.push({ code: 'NO_END', severity: 'error', message: '缺少结束节点（end）' })
    log('ERROR', '  ✗ 缺少 end 节点')
  } else {
    log('INFO', `  ✓ end 节点: ${graph.stats.endNodes.join(', ')} (${graph.stats.endNodes.length}个)`)
  }

  // 校验 3：branch 双分支完整性
  log('DEBUG', '校验 3：branch 双分支完整性')
  for (const branchId of graph.stats.branchNodes) {
    const outEdges = graph.dependencies.filter(d => d.source === branchId && d.type === 'edge')
    const hasSuccess = outEdges.some(e => e.handle === 'success')
    const hasFailure = outEdges.some(e => e.handle === 'failure')

    if (!hasSuccess) {
      result.valid = false
      result.errors.push({ code: 'BRANCH_SUCCESS_MISSING', severity: 'error', node: branchId, message: `分支节点 ${branchId} 的 success 锚点未连线` })
      log('ERROR', `  ✗ ${branchId} success 锚点未连线`)
    }
    if (!hasFailure) {
      result.valid = false
      result.errors.push({ code: 'BRANCH_FAILURE_MISSING', severity: 'error', node: branchId, message: `分支节点 ${branchId} 的 failure 锚点未连线` })
      log('ERROR', `  ✗ ${branchId} failure 锚点未连线`)
    }
    if (hasSuccess && hasFailure) {
      log('INFO', `  ✓ ${branchId} 双分支完整`)
    }
  }

  // 校验 4：无环
  log('DEBUG', '校验 4：依赖环检测')
  const cycles = detectCycles(graph)
  if (cycles.length > 0) {
    result.valid = false
    for (const c of cycles) {
      result.errors.push({ code: 'CYCLE_DETECTED', severity: 'error', cycle: c.cycle, message: `依赖环: ${c.cycle.join(' → ')}` })
    }
  } else {
    log('INFO', '  ✓ 无依赖环')
  }

  // 校验 5：所有节点可达性（从 start 可达 + 能到达 end）
  // 注意：被其他节点引用但无入边的节点是合法的"孤立数据源"，不算不可达
  log('DEBUG', '校验 5：节点可达性分析')
  const reachableFromStart = bfsReachable(graph, graph.stats.startNodes, false)
  const canReachEnd = bfsReachable(graph, graph.stats.endNodes, true)
  const referencedNodes = graph.referencedNodes || new Set()

  log('DEBUG', '  被引用的数据源节点', Array.from(referencedNodes))

  for (const nodeId of graph.nodeMap.keys()) {
    const node = graph.nodeMap.get(nodeId)
    const nodeType = node?.nodeType || node?.classCode
    if (nodeType === 'start' || nodeType === 'end') continue

    const fromStart = reachableFromStart.has(nodeId)
    const toEnd = canReachEnd.has(nodeId)
    const isReferenced = referencedNodes.has(nodeId)

    if (!fromStart && !toEnd && !isReferenced) {
      // 真正的孤立节点：无入边、无出边、无引用
      result.warnings.push({ code: 'ORPHAN_NODE', severity: 'warning', node: nodeId, message: `孤立节点 ${nodeId}（无输入无输出）` })
      log('WARN', `  ⚠ 孤立节点: ${nodeId}`)
    } else if (!fromStart && isReferenced) {
      // 孤立数据源：被引用但无入边 → 合法（运行时直接读取其输出）
      result.warnings.push({ code: 'DATASOURCE_NO_INFLOW', severity: 'info', node: nodeId, message: `数据源节点 ${nodeId}（被引用但无运行时入流，引擎直接读取其输出）` })
      log('INFO', `  ℹ 数据源节点(无入流): ${nodeId}（合法，被其他节点引用）`)
    } else if (!fromStart && !toEnd) {
      // 未引用也无法从 start 到达
      result.warnings.push({ code: 'UNREACHABLE_FROM_START', severity: 'warning', node: nodeId, message: `节点 ${nodeId} 从 start 不可达` })
      log('WARN', `  ⚠ 从 start 不可达: ${nodeId}`)
    } else if (!toEnd) {
      result.valid = false
      result.errors.push({ code: 'CANNOT_REACH_END', severity: 'error', node: nodeId, message: `节点 ${nodeId} 无法到达任何 end 节点（死路）` })
      log('ERROR', `  ✗ 无法到达 end: ${nodeId}`)
    } else {
      log('DEBUG', `  ✓ ${nodeId} 可达且可至 end`)
    }
  }

  // 校验 6：end 节点可达性
  log('DEBUG', '校验 6：end 节点可达性')
  for (const endId of graph.stats.endNodes) {
    if (!reachableFromStart.has(endId)) {
      result.valid = false
      result.errors.push({ code: 'END_UNREACHABLE', severity: 'error', node: endId, message: `结束节点 ${endId} 从 start 不可达` })
      log('ERROR', `  ✗ end 节点不可达: ${endId}`)
    } else {
      log('INFO', `  ✓ end 节点可达: ${endId}`)
    }
  }

  // 校验 7：功能节点 skillCode 检查
  log('DEBUG', '校验 7：功能节点 skillCode 检查')
  for (const [nodeId, node] of graph.nodeMap.entries()) {
    const nodeType = node.nodeType || node.classCode
    if (nodeType === 'skill' && !node.skillCode) {
      result.warnings.push({ code: 'MISSING_SKILL_CODE', severity: 'warning', node: nodeId, message: `功能节点 ${nodeId} 缺少 skillCode` })
      log('WARN', `  ⚠ 缺少 skillCode: ${nodeId}`)
    }
  }

  // 汇总
  log('INFO', '=== 拓扑校验完成 ===')
  log('INFO', `结果: ${result.valid ? '✓ 通过' : '✗ 失败'}`)
  log('INFO', `错误: ${result.errors.length} 个, 警告: ${result.warnings.length} 个`)

  if (result.errors.length > 0) {
    log('ERROR', '错误明细:', result.errors)
  }
  if (result.warnings.length > 0) {
    log('WARN', '警告明细:', result.warnings)
  }

  return result
}

/**
 * BFS 可达性分析
 * @param {DependencyGraph} graph
 * @param {string[]} startNodes - 起始节点
 * @param {boolean} reverse - 是否反向搜索
 * @returns {Set<string>}
 */
function bfsReachable(graph, startNodes, reverse = false) {
  const visited = new Set()
  const queue = [...startNodes]

  for (const s of startNodes) visited.add(s)

  while (queue.length > 0) {
    const current = queue.shift()
    const adj = reverse ? graph.reverseAdj.get(current) : graph.adjacency.get(current)
    for (const next of (adj || [])) {
      if (!visited.has(next)) {
        visited.add(next)
        queue.push(next)
      }
    }
  }

  return visited
}

// ==================== 6. 查找不可达节点 ====================

/**
 * 查找不可达节点（从 start 不可达 + 无法到达 end）
 *
 * @param {DependencyGraph} graph - 依赖图
 * @returns {{ fromStart: Set<string>, toEnd: Set<string>, unreachable: string[] }}
 */
export function findUnreachableNodes(graph) {
  log('INFO', '=== 查找不可达节点 ===')

  const fromStart = bfsReachable(graph, graph.stats.startNodes, false)
  const toEnd = bfsReachable(graph, graph.stats.endNodes, true)

  const unreachable = []
  for (const nodeId of graph.nodeMap.keys()) {
    if (!fromStart.has(nodeId) || !toEnd.has(nodeId)) {
      unreachable.push(nodeId)
    }
  }

  log('INFO', `从 start 可达: ${fromStart.size} 个节点`)
  log('INFO', `可到达 end: ${toEnd.size} 个节点`)
  log('INFO', `不可达节点: ${unreachable.length} 个 ${unreachable.length > 0 ? '[' + unreachable.join(', ') + ']' : ''}`)

  return { fromStart, toEnd, unreachable }
}

// ==================== 7. 综合拓扑分析（一站式） ====================

/**
 * 综合拓扑分析（一站式入口）
 *
 * 执行完整的拓扑分析并返回所有结果
 *
 * @param {Object} config - workflow_config JSON
 * @returns {TopologyAnalysis}
 */
export function analyzeTopology(config) {
  log('INFO', '╔════════════════════════════════════════════════════════════╗')
  log('INFO', '║          工作流拓扑分析（完整）                              ║')
  log('INFO', '╚════════════════════════════════════════════════════════════╝')

  const startTime = performance.now()

  // 步骤 1：构建依赖图
  const graph = buildDependencyGraph(config)

  // 步骤 2：拓扑排序
  const sortResult = topologicalSort(graph)

  // 步骤 3：提取路径
  const paths = extractAllPaths(graph)

  // 步骤 4：校验
  const validation = validateTopology(graph)

  // 步骤 5：不可达节点
  const unreachable = findUnreachableNodes(graph)

  const duration = (performance.now() - startTime).toFixed(2)

  const analysis = {
    graph,
    sortResult,
    paths,
    validation,
    unreachable,
    summary: {
      totalNodes: graph.stats.totalNodes,
      totalEdges: graph.stats.explicitEdges,
      totalDependencies: graph.dependencies.length,
      totalPaths: paths.length,
      startNodes: graph.stats.startNodes,
      endNodes: graph.stats.endNodes,
      branchNodes: graph.stats.branchNodes,
      hasCycle: sortResult.hasCycle,
      isValid: validation.valid,
      errorCount: validation.errors.length,
      warningCount: validation.warnings.length,
      durationMs: parseFloat(duration)
    }
  }

  log('INFO', '╔════════════════════════════════════════════════════════════╗')
  log('INFO', '║          拓扑分析完成                                        ║')
  log('INFO', '╚════════════════════════════════════════════════════════════╝')
  log('INFO', '汇总', analysis.summary)

  return analysis
}

// ==================== 8. 路径格式化（用于 UI 展示） ====================

/**
 * 格式化路径为可读字符串
 * @param {PathResult} path
 * @param {Map<string, Object>} nodeMap
 * @returns {string}
 */
export function formatPath(path, nodeMap) {
  return path.nodes.map((nodeId, idx) => {
    const node = nodeMap.get(nodeId)
    const title = node?.title || nodeId
    const type = node?.nodeType || node?.classCode || ''
    const typeLabel = type === 'start' ? '🟢' : type === 'end' ? '🔴' : type === 'branch' ? '🔀' : type === 'ai_node' ? '🤖' : ''
    let str = `${typeLabel} ${title}`
    // 标注分支决策
    const decision = path.branchDecisions.find(d => d.at === nodeId)
    if (decision) {
      str += ` [${decision.choice === 'success' ? '✓ 成功' : '✗ 失败'}]`
    }
    return str
  }).join(' → ')
}

/**
 * 生成路径摘要（用于工具栏显示）
 * @param {Array<PathResult>} paths
 * @returns {string}
 */
export function summarizePaths(paths) {
  if (paths.length === 0) return '无有效路径'
  if (paths.length === 1) return '1 条路径'
  return `${paths.length} 条路径（含分支）`
}

// ==================== 导出 ====================

export default {
  buildDependencyGraph,
  topologicalSort,
  extractAllPaths,
  detectCycles,
  validateTopology,
  findUnreachableNodes,
  analyzeTopology,
  formatPath,
  summarizePaths,
  setLogLevel,
  setLogEnabled,
  SPECIAL_NODE_TYPES,
  BRANCH_ANCHORS
}
