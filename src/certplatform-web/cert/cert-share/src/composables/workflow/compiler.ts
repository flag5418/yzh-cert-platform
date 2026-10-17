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

/** 落库格式（rule_json）—— 外层键 PascalCase（P4）；保留 camelCase 仅用于容忍旧数据读取 */
export interface WorkflowConfig {
  Version?: number
  WorkflowType?: string
  Nodes?: Array<Record<string, any>>
  Edges?: Array<Record<string, any>>
  OutputConfig?: Record<string, any>
  Glossary?: string
  /** ↓ 旧数据兼容 */
  version?: number
  workflowType?: string
  nodes?: Array<Record<string, any>>
  edges?: Array<Record<string, any>>
  outputConfig?: Record<string, any>
  glossary?: string
}

export function compileToWorkflowConfig(graphData: GraphData, meta: Record<string, any> = {}): WorkflowConfig {
  const nodes = graphData.nodes.map(n => {
    const d = n.properties || {}
    const node: Record<string, any> = { NodeId: n.id, NodeType: d.nodeType || 'skill', Title: d.title || n.text || d.skillCode || n.id }
    if (d.skillCode) node.SkillCode = d.skillCode
    if (d.config && Object.keys(d.config).length > 0) node.Config = d.config
    if (d.inputs && Object.keys(d.inputs).length > 0) node.Inputs = d.inputs
    if (d.outputs && Object.keys(d.outputs).length > 0) node.Outputs = d.outputs
    if (d.inputPorts && d.inputPorts.length > 0) node.InputPorts = d.inputPorts
    if (d.outputPorts && d.outputPorts.length > 0) node.OutputPorts = d.outputPorts
    return node
  })

  const edges = graphData.edges.map(e => {
    const edge: { Source: string; Target: string; SourceHandle?: string; TargetHandle?: string } = { Source: e.sourceNodeId, Target: e.targetNodeId }
    const ed = e.properties || {}
    if (ed.sourceHandle) edge.SourceHandle = ed.sourceHandle
    if (ed.targetHandle) edge.TargetHandle = ed.targetHandle
    return edge
  })

  return {
    Version: meta.version ?? graphData.meta?.version ?? 1,
    WorkflowType: meta.workflowType ?? graphData.meta?.workflowType ?? 'validation',
    Nodes: nodes, Edges: edges,
    OutputConfig: meta.outputConfig ?? graphData.meta?.outputConfig ?? {},
    Glossary: meta.glossary ?? graphData.meta?.glossary ?? ''
  }
}

export function decompileToGraphData(config: WorkflowConfig, layoutJson?: any): { graphData: GraphData; nodeMap: Record<string, GraphNode> } {
  const ordered = topologicalOrder(config)
  const nodeMap: Record<string, GraphNode> = {}
  const lfNodes: GraphNode[] = []
  const lfEdges: GraphEdge[] = []

  const positions = layoutJson?.nodePositions || {}
  const configNodes = config.Nodes || config.nodes || []
  const configEdges = config.Edges || config.edges || []

  ordered.forEach((nodeId: string, idx: number) => {
    const node = configNodes.find(n => (n.NodeId || n.nodeId) === nodeId)
    if (!node) return
    const pos = positions[nodeId]
    const col = idx % 4
    const row = Math.floor(idx / 4)
    const x = pos?.x ?? 100 + col * 220
    const y = pos?.y ?? 80 + row * 130
    const type = node.NodeType || node.nodeType || 'skill'
    const skillCode = node.SkillCode || node.skillCode
    const nodeCfg = node.Config || node.config

    const nodeProps: Record<string, any> = {
      ...(nodeCfg || {}), nodeType: type, title: node.Title || node.title || '',
      skillCode: skillCode || '', config: nodeCfg || {},
      inputs: node.Inputs || node.inputs || {}, outputs: node.Outputs || node.outputs || {},
      inputPorts: node.InputPorts || node.inputPorts || [], outputPorts: node.OutputPorts || node.outputPorts || []
    }
    if (type === 'branch') nodeProps.points = [[0, -30], [50, 0], [0, 30]]

    const lfNode: GraphNode = {
      id: nodeId, type: lfNodeType(type), x, y,
      text: node.Title || node.title || skillCode || nodeId,
      style: nodeStyle(type, skillCode),
      properties: nodeProps
    }
    lfNodes.push(lfNode)
    nodeMap[nodeId] = lfNode
  })

  for (const e of configEdges) {
    const src = e.Source ?? e.source
    const tgt = e.Target ?? e.target
    const srcHandle = e.SourceHandle ?? e.sourceHandle
    const tgtHandle = e.TargetHandle ?? e.targetHandle
    if (!nodeMap[src] || !nodeMap[tgt]) continue
    const isBranchAnchor = srcHandle === 'success' || srcHandle === 'failure'
    lfEdges.push({
      id: `${src}-->${tgt}${srcHandle ? '-' + srcHandle : ''}`,
      type: 'polyline',
      sourceNodeId: src, targetNodeId: tgt,
      text: isBranchAnchor ? (srcHandle === 'success' ? '成功' : '失败') : '',
      style: isBranchAnchor
        ? { stroke: srcHandle === 'success' ? '#67C23A' : '#F56C6C', strokeWidth: 2 }
        : { stroke: '#5B8FF9', strokeWidth: 2 },
      properties: { sourceHandle: srcHandle || null, targetHandle: tgtHandle || null }
    })
  }

  return {
    graphData: {
      nodes: lfNodes, edges: lfEdges,
      transforms: layoutJson?.transforms || { x: 0, y: 0, zoom: 1 },
      meta: { version: config.Version ?? config.version ?? 1, workflowType: config.WorkflowType || config.workflowType || 'validation', outputConfig: config.OutputConfig || config.outputConfig || {}, glossary: config.Glossary || config.glossary || '' }
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
  const configNodes = config.Nodes || config.nodes || []
  const configEdges = config.Edges || config.edges || []
  const nodeIds = new Set(configNodes.map(n => n.NodeId || n.nodeId))
  const inDegree: Record<string, number> = {}
  const adj: Record<string, string[]> = {}
  nodeIds.forEach(id => { inDegree[id] = 0; adj[id] = [] })
  for (const e of configEdges) {
    const src = e.Source ?? (e as any).source
    const tgt = e.Target ?? (e as any).target
    if (nodeIds.has(src) && nodeIds.has(tgt)) { inDegree[tgt]++; adj[src].push(tgt) }
  }
  const queue: string[] = []
  nodeIds.forEach(id => { if (inDegree[id] === 0) queue.push(id) })
  const result: string[] = []
  while (queue.length > 0) { const curr = queue.shift()!; result.push(curr); for (const next of adj[curr]) { inDegree[next]--; if (inDegree[next] === 0) queue.push(next) } }
  nodeIds.forEach(id => { if (!result.includes(id)) result.push(id) })
  return result
}
