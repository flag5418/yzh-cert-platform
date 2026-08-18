/**
 * workflow-designer/compiler.js
 * LogicFlow getGraphData() ⇄ workflow_config JSON（自定义工作流引擎 V1.2 §5.7 节点模型）
 *
 * 节点模型（V1.2）：
 *   { nodeId, nodeType: 'start'|'skill'|'logic'|'end', title, skillCode?, config?, inputs?, outputs? }
 * 边模型：
 *   { source, target, sourceHandle? (anchor: success/failure), targetHandle? }
 */

/**
 * 编译 LogicFlow 画布数据 → workflow_config
 */
export function compileToWorkflowConfig(graphData, meta = {}) {
  const nodes = graphData.nodes.map(n => {
    // LogicFlow 2.0: 自定义数据在 n.properties 中
    const d = n.properties || n.data || {}
    const node = {
      nodeId: n.id,
      nodeType: d.nodeType || 'skill',
      title: d.title || n.text || d.skillCode || n.id
    }
    if (d.skillCode) node.skillCode = d.skillCode
    if (d.config && Object.keys(d.config).length > 0) node.config = d.config
    if (d.inputs && Object.keys(d.inputs).length > 0) node.inputs = d.inputs
    if (d.outputs && Object.keys(d.outputs).length > 0) node.outputs = d.outputs
    return node
  })

  const edges = graphData.edges.map(e => {
    const edge = { source: e.sourceNodeId, target: e.targetNodeId }
    // LogicFlow 2.0: 边的自定义数据在 properties 中
    const ed = e.properties || e.data || {}
    if (ed.sourceHandle) edge.sourceHandle = ed.sourceHandle
    if (ed.targetHandle) edge.targetHandle = ed.targetHandle
    return edge
  })

  return {
    version: meta.version ?? graphData.meta?.version ?? 1,
    workflowType: meta.workflowType ?? graphData.meta?.workflowType ?? 'validation',
    nodes,
    edges,
    outputConfig: meta.outputConfig ?? graphData.meta?.outputConfig ?? {},
    glossary: meta.glossary ?? graphData.meta?.glossary ?? ''
  }
}

/**
 * 反编译 workflow_config → LogicFlow 渲染数据（自动布局：拓扑序）
 */
export function decompileToGraphData(config, layoutJson = null) {
  const ordered = topologicalOrder(config)
  const nodeMap = {}
  const lfNodes = []
  const lfEdges = []

  // 节点布局：优先使用 layoutJson（画布恢复），否则拓扑自动布局
  const positions = layoutJson?.nodePositions || {}

  ordered.forEach((nodeId, idx) => {
    const node = config.nodes.find(n => n.nodeId === nodeId)
    if (!node) return
    const pos = positions[nodeId]
    const col = idx % 4
    const row = Math.floor(idx / 4)
    const x = pos?.x ?? 100 + col * 220
    const y = pos?.y ?? 80 + row * 130
    const type = node.nodeType || 'skill'

    const lfNode = {
      id: nodeId,
      type: lfNodeType(type),
      x,
      y,
      text: node.title || node.skillCode || nodeId,
      style: nodeStyle(type, node.skillCode),
      // LogicFlow 2.0: 自定义数据通过 properties 传入
      properties: {
        ...(node.config || {}),
        nodeType: type,
        title: node.title || '',
        skillCode: node.skillCode || '',
        config: node.config || {},
        inputs: node.inputs || {},
        outputs: node.outputs || {}
      }
    }
    lfNodes.push(lfNode)
    nodeMap[nodeId] = lfNode
  })

  for (const e of config.edges || []) {
    if (!nodeMap[e.source] || !nodeMap[e.target]) continue
    const isLogicBranch = e.sourceHandle === 'success' || e.sourceHandle === 'failure'
    lfEdges.push({
      id: `${e.source}-->${e.target}${e.sourceHandle ? '-' + e.sourceHandle : ''}`,
      type: 'polyline',
      sourceNodeId: e.source,
      targetNodeId: e.target,
      text: isLogicBranch ? (e.sourceHandle === 'success' ? '成功' : '失败') : '',
      style: isLogicBranch
        ? { stroke: e.sourceHandle === 'success' ? '#67C23A' : '#F56C6C', strokeWidth: 2 }
        : { stroke: '#5B8FF9', strokeWidth: 2 },
      // LogicFlow 2.0: 边的自定义数据通过 properties 传入
      properties: {
        sourceHandle: e.sourceHandle || null,
        targetHandle: e.targetHandle || null
      }
    })
  }

  return {
    graphData: {
      nodes: lfNodes,
      edges: lfEdges,
      transforms: layoutJson?.transforms || { x: 0, y: 0, zoom: 1 },
      meta: {
        version: config.version || 1,
        workflowType: config.workflowType || 'validation',
        outputConfig: config.outputConfig || {},
        glossary: config.glossary || ''
      }
    },
    nodeMap
  }
}

/** 从画布 getGraphData 提取 layoutJson（节点坐标 + 画布变换） */
export function extractLayoutJson(graphData) {
  const nodePositions = {}
  for (const n of graphData.nodes || []) {
    nodePositions[n.id] = { x: n.x, y: n.y }
  }
  return {
    nodePositions,
    transforms: graphData.transforms || { x: 0, y: 0, zoom: 1 }
  }
}

// ==================== 节点类型映射 ====================

function lfNodeType(nodeType) {
  switch (nodeType) {
    case 'start': return 'circle'
    case 'end': return 'circle'
    case 'logic': return 'diamond'
    default: return 'rect'
  }
}

/** 节点配色：按 nodeType / skill category */
export function nodeStyle(nodeType, skillCode, category = '') {
  const catColors = {
    data_access: { fill: '#E3F2FD', stroke: '#1565C0' },
    data_process: { fill: '#E8F5E9', stroke: '#2E7D32' },
    ai_judge: { fill: '#FFF3E0', stroke: '#E65100' },
    ai_generate: { fill: '#FCE4EC', stroke: '#880E4F' },
    output: { fill: '#F3E5F5', stroke: '#6A1B9A' }
  }
  if (nodeType === 'start') return { fill: '#E8F5E9', stroke: '#2E7D32', strokeWidth: 2, radius: 24 }
  if (nodeType === 'end') return { fill: '#FFEBEE', stroke: '#C62828', strokeWidth: 2, radius: 24 }
  if (nodeType === 'logic') return { fill: '#FFF8E1', stroke: '#F57F17', strokeWidth: 2 }
  const c = catColors[category] || { fill: '#F5F5F5', stroke: '#9E9E9E' }
  return { fill: c.fill, stroke: c.stroke, strokeWidth: 2, radius: 8 }
}

// ==================== 辅助 ====================

export function topologicalOrder(config) {
  const nodeIds = new Set(config.nodes.map(n => n.nodeId))
  const inDegree = {}
  const adj = {}
  nodeIds.forEach(id => { inDegree[id] = 0; adj[id] = [] })
  for (const e of config.edges || []) {
    if (nodeIds.has(e.source) && nodeIds.has(e.target)) {
      inDegree[e.target]++
      adj[e.source].push(e.target)
    }
  }
  const queue = []
  nodeIds.forEach(id => { if (inDegree[id] === 0) queue.push(id) })
  const result = []
  while (queue.length > 0) {
    const curr = queue.shift()
    result.push(curr)
    for (const next of adj[curr]) {
      inDegree[next]--
      if (inDegree[next] === 0) queue.push(next)
    }
  }
  nodeIds.forEach(id => { if (!result.includes(id)) result.push(id) })
  return result
}
