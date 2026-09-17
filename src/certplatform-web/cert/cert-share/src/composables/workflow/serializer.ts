/**
 * workflow-config/serializer.ts
 *
 * configJSON ⇄ 业务层数据模型转换（含旧数据迁移）
 *
 * 设计规则（V2 §5.2）：
 * - configJSON 落库格式：{ version, workflowType, nodes[], edges[], outputConfig, glossary }
 * - 业务层模型：{ id, classCode, title, x, y, config, inputs, inputPorts, outputPorts }
 * - 旧数据（n${Date.now()} 时间戳 ID）加载时迁移重编号为 {classCode}_n{序号}
 */

import { NodeIdGenerator, isValidNodeId } from './nodeIdGenerator'
import { getSpecialNode } from './specialNodes'

export interface WorkflowNode {
  nodeId?: string
  id?: string
  nodeType?: string
  classCode?: string
  skillCode?: string
  title?: string
  config?: Record<string, any>
  inputs?: Record<string, any>
  inputTypes?: Record<string, string>
  outputs?: Record<string, any>
  inputPorts?: any[]
  outputPorts?: any[]
  x?: number
  y?: number
}

export interface WorkflowEdge {
  source: string
  target: string
  sourceHandle?: string | null
  targetHandle?: string | null
}

export interface WorkflowConfig {
  version?: number
  workflowType?: string
  nodes?: WorkflowNode[]
  edges?: WorkflowEdge[]
  outputConfig?: Record<string, any>
  glossary?: string
}

export interface LayoutJson {
  nodePositions?: Record<string, { x: number; y: number }>
}

export interface SerializedNode {
  nodeId: string
  nodeType: string
  title: string
  skillCode?: string
  config?: Record<string, any>
  inputs?: Record<string, any>
  inputTypes?: Record<string, string>
  outputs?: Record<string, any>
  inputPorts?: any[]
  outputPorts?: any[]
}

export interface SerializedEdge {
  source: string
  target: string
  sourceHandle?: string | null
  targetHandle?: string | null
}

export interface DeserializeResult {
  nodes: Array<{
    id: string
    classCode: string
    nodeType: string
    title: string
    skillCode: string
    x: number
    y: number
    config: Record<string, any>
    inputs: Record<string, any>
    inputTypes: Record<string, string>
    outputs: Record<string, any>
    inputPorts: any[]
    outputPorts: any[]
  }>
  edges: Array<{
    id: string
    source: string
    target: string
    sourceHandle: string | null
    targetHandle: string | null
  }>
  idGenerator: NodeIdGenerator
  migrated: boolean
  oldToNew: Record<string, string>
}

/**
 * 反编译 workflow_config JSON → 业务层节点/边数组
 */
export function deserialize(config: WorkflowConfig, layoutJson: LayoutJson | null = null): DeserializeResult {
  if (!config || !config.nodes) {
    return { nodes: [], edges: [], idGenerator: new NodeIdGenerator(), migrated: false, oldToNew: {} }
  }

  const positions = layoutJson?.nodePositions || {}
  let migrated = false
  const idGenerator = new NodeIdGenerator()
  const oldToNew: Record<string, string> = {}

  // 第一遍：检测并迁移旧 ID（n${Date.now()} 格式）
  const nodes = config.nodes.map((n, idx) => {
    const nodeId = n.nodeId || n.id || `n${idx}`
    let newId = nodeId

    if (!isValidNodeId(nodeId)) {
      const classCode = n.nodeType || n.classCode || n.skillCode || 'skill'
      newId = idGenerator.next(classCode)
      oldToNew[nodeId] = newId
      migrated = true
    } else {
      idGenerator.resetFromNodes([{ id: newId }])
    }

    const pos = positions[nodeId] || positions[newId] || {}
    const nodeType = n.nodeType || n.classCode || (n.skillCode ? 'skill' : 'skill')
    const classCode = nodeType

    const specialMeta = getSpecialNode(classCode)
    const inputPorts = n.inputPorts?.length ? n.inputPorts : (specialMeta?.inputPorts || [])
    const outputPorts = n.outputPorts?.length ? n.outputPorts : (specialMeta?.outputPorts || [])

    return {
      id: newId,
      classCode,
      nodeType,
      title: n.title || '',
      skillCode: n.skillCode || '',
      x: pos.x ?? 0,
      y: pos.y ?? 0,
      config: n.config || {},
      inputs: n.inputs || {},
      inputTypes: n.inputTypes || {},
      outputs: n.outputs || {},
      inputPorts,
      outputPorts
    }
  })

  // 第二遍：迁移边中的 ID 引用
  const edges = (config.edges || []).map(e => {
    const src = oldToNew[e.source] || e.source
    const tgt = oldToNew[e.target] || e.target
    return {
      id: `e-${src}-${tgt}`,
      source: src,
      target: tgt,
      sourceHandle: e.sourceHandle || null,
      targetHandle: e.targetHandle || null
    }
  }).filter(e => {
    return nodes.some(n => n.id === e.source) && nodes.some(n => n.id === e.target)
  })

  // 迁移 inputs 中的旧 ID 引用
  if (migrated) {
    for (const node of nodes) {
      for (const [port, val] of Object.entries(node.inputs)) {
        if (typeof val === 'string' && oldToNew[val]) {
          node.inputs[port] = oldToNew[val]
        }
      }
    }
  }

  return { nodes, edges, idGenerator, migrated, oldToNew }
}

/**
 * 序列化业务层节点/边数组 → workflow_config JSON（落库格式）
 */
export function serialize(
  nodes: Array<{ id: string; classCode: string; title: string; skillCode?: string; config?: any; inputs?: any; inputTypes?: any; outputs?: any; inputPorts?: any[]; outputPorts?: any[] }>,
  edges: Array<{ source: string; target: string; sourceHandle?: string | null; targetHandle?: string | null }>,
  meta: { version?: number; workflowType?: string; outputConfig?: any; glossary?: string } = {}
): WorkflowConfig {
  const configNodes: SerializedNode[] = nodes.map(n => {
    const node: SerializedNode = {
      nodeId: n.id,
      nodeType: n.classCode || 'skill',
      title: n.title || ''
    }
    if (n.skillCode) node.skillCode = n.skillCode
    if (n.config && Object.keys(n.config).length > 0) node.config = n.config
    if (n.inputs && Object.keys(n.inputs).length > 0) node.inputs = n.inputs
    if (n.inputTypes && Object.keys(n.inputTypes).length > 0) node.inputTypes = n.inputTypes
    if (n.outputs && Object.keys(n.outputs).length > 0) node.outputs = n.outputs
    if (n.inputPorts?.length) node.inputPorts = n.inputPorts
    if (n.outputPorts?.length) node.outputPorts = n.outputPorts
    return node
  })

  const configEdges: SerializedEdge[] = edges.map(e => {
    const edge: SerializedEdge = { source: e.source, target: e.target }
    if (e.sourceHandle) edge.sourceHandle = e.sourceHandle
    if (e.targetHandle) edge.targetHandle = e.targetHandle
    return edge
  })

  return {
    version: meta.version ?? 1,
    workflowType: meta.workflowType ?? 'validation',
    nodes: configNodes,
    edges: configEdges,
    outputConfig: meta.outputConfig ?? {},
    glossary: meta.glossary ?? ''
  }
}

/**
 * 从业务层节点提取 layoutJson
 */
export function extractLayout(nodes: Array<{ id: string; x?: number; y?: number }>): LayoutJson {
  const nodePositions: Record<string, { x: number; y: number }> = {}
  for (const n of nodes) {
    nodePositions[n.id] = { x: n.x || 0, y: n.y || 0 }
  }
  return { nodePositions }
}
