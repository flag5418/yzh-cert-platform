/**
 * workflow-designer/compiler.js
 * 将 LogicFlow getGraphData() 结果编译为 workflow_config JSON（发布态）
 */

/**
 * 编译 LogicFlow 画布数据 → workflow_config
 */
export function compileToWorkflowConfig(graphData) {
  const nodes = graphData.nodes.map(n => ({
    nodeId: n.id,
    skillCode: n.data?.skillCode || '',
    config: n.data?.config || {},
    inputs: n.data?.inputs || {},
    outputs: n.data?.outputs || {}
  }))

  const normalEdges = []
  const branches = []

  for (const e of graphData.edges) {
    const condition = e.data?.condition || null
    if (condition) {
      branches.push({
        from: e.sourceNodeId,
        condition,
        then: [{
          nodeId: e.targetNodeId,
          skillCode: nodes.find(n => n.nodeId === e.targetNodeId)?.skillCode || '',
          inputs: nodes.find(n => n.nodeId === e.targetNodeId)?.inputs || {},
          outputs: nodes.find(n => n.nodeId === e.targetNodeId)?.outputs || {}
        }]
      })
    } else {
      normalEdges.push({
        source: e.sourceNodeId,
        target: e.targetNodeId,
        sourceHandle: e.data?.sourceHandle || null,
        targetHandle: e.data?.targetHandle || null
      })
    }
  }

  return {
    version: graphData.meta?.version ?? 1,
    workflowType: graphData.meta?.workflowType ?? 'validation',
    nodes,
    edges: normalEdges,
    branches,
    outputConfig: graphData.meta?.outputConfig || {}
  }
}

/**
 * 反编译 workflow_config → LogicFlow getGraphData() 格式
 */
export function decompileToGraphData(config, nodes = [], xStep = 220, yStep = 120) {
  const nodeMap = {}
  const lfNodes = []
  const lfEdges = []

  // 按拓扑序放置节点
  const ordered = topologicalOrder(config)
  ordered.forEach((nodeId, idx) => {
    const node = config.nodes.find(n => n.nodeId === nodeId)
    if (!node) return
    const col = idx % 4
    const row = Math.floor(idx / 4)
    const x = 100 + col * xStep
    const y = 80 + row * yStep

    const lfNode = {
      id: nodeId,
      type: 'rect',
      x,
      y,
      text: node.skillCode,
      style: {
        fill: skillNodeColor(node.skillCode),
        stroke: skillNodeStroke(node.skillCode),
        strokeWidth: 2
      },
      data: {
        skillCode: node.skillCode,
        config: node.config || {},
        inputs: node.inputs || {},
        outputs: node.outputs || {},
        condition: null
      }
    }
    lfNodes.push(lfNode)
    nodeMap[nodeId] = lfNode
  })

  // 普通边
  for (const e of config.edges || []) {
    const src = nodeMap[e.source]
    const tgt = nodeMap[e.target]
    if (!src || !tgt) continue
    lfEdges.push({
      id: `${e.source}-->${e.target}`,
      type: 'smooth',
      sourceNodeId: e.source,
      targetNodeId: e.target,
      style: { stroke: '#5B8FF9', strokeWidth: 2 },
      data: {
        sourceHandle: e.sourceHandle,
        targetHandle: e.targetHandle,
        condition: null
      }
    })
  }

  // 条件分支边（橙色虚线）
  for (const b of config.branches || []) {
    const src = nodeMap[b.from]
    for (const thenNode of b.then || []) {
      const tgt = nodeMap[thenNode.nodeId]
      if (!src || !tgt) continue
      lfEdges.push({
        id: `${b.from}-->${thenNode.nodeId}-branch`,
        type: 'polyline',
        sourceNodeId: b.from,
        targetNodeId: thenNode.nodeId,
        style: { stroke: '#F5A623', strokeWidth: 2, strokeDasharray: '5,5' },
        data: {
          sourceHandle: null,
          targetHandle: null,
          condition: b.condition
        }
      })
    }
  }

  return {
    graphData: {
      nodes: lfNodes,
      edges: lfEdges,
      transforms: { x: 0, y: 0, zoom: 1 },
      meta: {
        version: config.version,
        workflowType: config.workflowType,
        outputConfig: config.outputConfig
      }
    },
    nodeMap
  }
}

// ── 辅助函数 ──

function skillNodeColor(skillCode) {
  const map = {
    get_field: '#E3F2FD', get_table: '#E3F2FD',
    compare: '#E8F5E9', date_diff: '#E8F5E9', text_merge: '#E8F5E9',
    llm_judge: '#FFF3E0', llm_generate: '#FCE4EC',
    create_nc: '#F3E5F5', save_result: '#F3E5F5', assemble_text: '#F3E5F5'
  }
  return map[skillCode] ?? '#F5F5F5'
}

function skillNodeStroke(skillCode) {
  const map = {
    get_field: '#1565C0', get_table: '#1565C0',
    compare: '#2E7D32', date_diff: '#2E7D32', text_merge: '#2E7D32',
    llm_judge: '#E65100', llm_generate: '#880E4F',
    create_nc: '#6A1B9A', save_result: '#6A1B9A', assemble_text: '#6A1B9A'
  }
  return map[skillCode] ?? '#9E9E9E'
}

function topologicalOrder(config) {
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
