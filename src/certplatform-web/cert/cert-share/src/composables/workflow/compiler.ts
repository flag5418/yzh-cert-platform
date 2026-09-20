/**
 * compiler.ts — LogicFlow getGraphData() ⇄ workflow_config JSON
 */

import { getSpecialNodeStyle } from '@share/composables/workflow/specialNodes'
import {
  analyzeTopology, validateTopology, topologicalSort, extractAllPaths,
  detectCycles, buildDependencyGraph, findUnreachableNodes, formatPath, summarizePaths,
  setLogLevel, setLogEnabled, type PathResult, type TopologyValidation, type TopologyAnalysis
} from '@share/composables/workflow/topology'

export interface GraphNode {
  id: string
  type: string
  x: number
  y: number
  text?: string
  style?: any
  properties: Record<string, any>
}

export interface GraphEdge {
  id: string
  type: string
  sourceNodeId: string
  targetNodeId: string
  text?: string
  style?: any
  properties: Record<string, any>
}

export interface GraphData {
  nodes: GraphNode[]
  edges: GraphEdge[]
  transforms?: { x: number; y: number; zoom: number }
  meta?: Record<string, any>
}

export interface WorkflowConfig {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  version?: number
  workflowType: string
  nodes: Array<Record<string, any>>
  edges: Array<{ source: string; target: string; sourceHandle?: string; targetHandle?: string }>
  outputConfig: Record<string, any>
  glossary: string
}

export function compileToWorkflowConfig(graphData: GraphData, meta: Record<string, any> = {}): WorkflowConfig {
  const nodes = graphData.nodes.map(n => {
    const d = n.properties || {}
    const node: Record<string, any> = { nodeId: n.id, nodeType: d.nodeType || 'skill', title: d.title || n.text || d.skillCode || n.id }
    if (d.skillCode) node.skillCode = d.skillCode
    if (d.config && Object.keys(d.config).length > 0) node.config = d.config
    if (d.inputs && Object.keys(d.inputs).length > 0) node.inputs = d.inputs
    if (d.outputs && Object.keys(d.outputs).length > 0) node.outputs = d.outputs
    if (d.inputPorts && d.inputPorts.length > 0) node.inputPorts = d.inputPorts
    if (d.outputPorts && d.outputPorts.length > 0) node.outputPorts = d.outputPorts
    return node
  })

  const edges = graphData.edges.map(e => {
    const edge: { source: string; target: string; sourceHandle?: string; targetHandle?: string } = { source: e.sourceNodeId, target: e.targetNodeId }
    const ed = e.properties || {}
    if (ed.sourceHandle) edge.sourceHandle = ed.sourceHandle
    if (ed.targetHandle) edge.targetHandle = ed.targetHandle
    return edge
  })

  return {
    version: meta.version ?? graphData.meta?.version ?? 1,
    workflowType: meta.workflowType ?? graphData.meta?.workflowType ?? 'validation',
    nodes, edges,
    outputConfig: meta.outputConfig ?? graphData.meta?.outputConfig ?? {},
    glossary: meta.glossary ?? graphData.meta?.glossary ?? ''
  }
}

export function decompileToGraphData(config: WorkflowConfig, layoutJson?: any): { graphData: GraphData; nodeMap: Record<string, GraphNode> } {
  const ordered = topologicalOrder(config)
  const nodeMap: Record<string, GraphNode> = {}
  const lfNodes: GraphNode[] = []
  const lfEdges: GraphEdge[] = []

  const positions = layoutJson?.nodePositions || {}

  ordered.forEach((nodeId: string, idx: number) => {
    const node = config.nodes.find(n => n.nodeId === nodeId)
    if (!node) return
    const pos = positions[nodeId]
    const col = idx % 4
    const row = Math.floor(idx / 4)
    const x = pos?.x ?? 100 + col * 220
    const y = pos?.y ?? 80 + row * 130
    const type = node.nodeType || 'skill'

    const nodeProps: Record<string, any> = {
      ...(node.config || {}), nodeType: type, title: node.title || '',
      skillCode: node.skillCode || '', config: node.config || {},
      inputs: node.inputs || {}, outputs: node.outputs || {},
      inputPorts: node.inputPorts || [], outputPorts: node.outputPorts || []
    }
    if (type === 'branch') nodeProps.points = [[0, -30], [50, 0], [0, 30]]

    const lfNode: GraphNode = {
      id: nodeId, type: lfNodeType(type), x, y,
      text: node.title || node.skillCode || nodeId,
      style: nodeStyle(type, node.skillCode),
      properties: nodeProps
    }
    lfNodes.push(lfNode)
    nodeMap[nodeId] = lfNode
  })

  for (const e of config.edges || []) {
    if (!nodeMap[e.source] || !nodeMap[e.target]) continue
    const isBranchAnchor = e.sourceHandle === 'success' || e.sourceHandle === 'failure'
    lfEdges.push({
      id: `${e.source}-->${e.target}${e.sourceHandle ? '-' + e.sourceHandle : ''}`,
      type: 'polyline',
      sourceNodeId: e.source, targetNodeId: e.target,
      text: isBranchAnchor ? (e.sourceHandle === 'success' ? '成功' : '失败') : '',
      style: isBranchAnchor
        ? { stroke: e.sourceHandle === 'success' ? '#67C23A' : '#F56C6C', strokeWidth: 2 }
        : { stroke: '#5B8FF9', strokeWidth: 2 },
      properties: { sourceHandle: e.sourceHandle || null, targetHandle: e.targetHandle || null }
    })
  }

  return {
    graphData: {
      nodes: lfNodes, edges: lfEdges,
      transforms: layoutJson?.transforms || { x: 0, y: 0, zoom: 1 },
      meta: { version: config.version || 1, workflowType: config.workflowType || 'validation', outputConfig: config.outputConfig || {}, glossary: config.glossary || '' }
    },
    nodeMap
  }
}

export function extractLayoutJson(graphData: GraphData): { nodePositions: Record<string, { x: number; y: number }>; transforms: any } {
  const nodePositions: Record<string, { x: number; y: number }> = {}
  for (const n of graphData.nodes || []) { nodePositions[n.id] = { x: n.x, y: n.y } }
  return { nodePositions, transforms: graphData.transforms || { x: 0, y: 0, zoom: 1 } }
}

export function extractTopologicalPaths(config: WorkflowConfig): PathResult[] {
  const graph = buildDependencyGraph(config)
  return extractAllPaths(graph)
}

export function extractTopologicalPathsFromGraphData(graphData: GraphData): PathResult[] {
  const config = compileToWorkflowConfig(graphData)
  return extractTopologicalPaths(config)
}

export function analyzeWorkflowTopology(config: WorkflowConfig): TopologyAnalysis { return analyzeTopology(config) }
export function validateWorkflowTopology(config: WorkflowConfig): TopologyValidation { return validateTopology(buildDependencyGraph(config)) }

export function formatWorkflowPath(path: PathResult, nodeMap: Map<string, any> | Record<string, any>): string {
  const getNode = (id: string) => nodeMap instanceof Map ? nodeMap.get(id) : nodeMap[id]
  return formatPath(path, { get: getNode } as any)
}

export { setLogLevel, setLogEnabled, topologicalSort, detectCycles, findUnreachableNodes, buildDependencyGraph, summarizePaths, extractAllPaths, validateTopology, analyzeTopology }

function lfNodeType(nodeType: string): string {
  switch (nodeType) {
    case 'start': return 'circle'
    case 'end': return 'circle'
    case 'branch': return 'polygon'
    default: return 'rect'
  }
}

export function nodeStyle(nodeType: string, _skillCode?: string, category?: string): any {
  const specialStyle = getSpecialNodeStyle(nodeType)
  if (specialStyle) { const radius = (nodeType === 'start' || nodeType === 'end') ? 24 : 8; return { ...specialStyle, radius } }
  const catColors: Record<string, any> = {
    data_access: { fill: '#E3F2FD', stroke: '#1565C0' }, data_process: { fill: '#E8F5E9', stroke: '#2E7D32' },
    ai_judge: { fill: '#FFF3E0', stroke: '#E65100' }, ai_generate: { fill: '#FCE4EC', stroke: '#880E4F' },
    output: { fill: '#F3E5F5', stroke: '#6A1B9A' }
  }
  const c = catColors[category || ''] || { fill: '#F5F5F5', stroke: '#9E9E9E' }
  return { fill: c.fill, stroke: c.stroke, strokeWidth: 2, radius: 8 }
}

export function topologicalOrder(config: WorkflowConfig): string[] {
  const nodeIds = new Set(config.nodes.map(n => n.nodeId))
  const inDegree: Record<string, number> = {}
  const adj: Record<string, string[]> = {}
  nodeIds.forEach(id => { inDegree[id] = 0; adj[id] = [] })
  for (const e of config.edges || []) {
    if (nodeIds.has(e.source) && nodeIds.has(e.target)) { inDegree[e.target]++; adj[e.source].push(e.target) }
  }
  const queue: string[] = []
  nodeIds.forEach(id => { if (inDegree[id] === 0) queue.push(id) })
  const result: string[] = []
  while (queue.length > 0) { const curr = queue.shift()!; result.push(curr); for (const next of adj[curr]) { inDegree[next]--; if (inDegree[next] === 0) queue.push(next) } }
  nodeIds.forEach(id => { if (!result.includes(id)) result.push(id) })
  return result
}
