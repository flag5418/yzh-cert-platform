/**
 * workflow-config/serializer.ts
 *
 * configJSON ⇄ 业务层数据模型转换（含旧数据迁移）
 *
 * 设计规则（V2 §5.2）：
 * - configJSON 落库格式（★ 2026-10-16 P4：外层键一律 PascalCase）：
 *   { Version, WorkflowType, Nodes[], Edges[], OutputConfig, Glossary }
 * - 业务层模型（前端内部 camelCase）：{ id, classCode, title, x, y, config, inputs, inputPorts, outputPorts }
 * - 旧数据（n${Date.now()} 时间戳 ID）加载时迁移重编号为 {classCode}_n{序号}
 *
 * ⚠️ 边界 F（LayoutJson `{nodePositions}`）**不翻**（项目自有布局格式，见方案 §2.2）。
 */

import { NodeIdGenerator, isValidNodeId } from './nodeIdGenerator'
import { getSpecialNode } from './specialNodes'

/** 落库节点（rule_json.nodes[]）—— 外层键 PascalCase；保留 camelCase 只用于容忍旧数据读取 */
export interface WorkflowNode {
  NodeId?: string
  NodeType?: string
  Title?: string
  /** 该节点在此工作流中的具体作用描述（专家可见） */
  Description?: string
  SkillCode?: string
  Config?: Record<string, any>
  Inputs?: Record<string, any>
  InputTypes?: Record<string, string>
  Outputs?: Record<string, any>
  InputPorts?: any[]
  OutputPorts?: any[]
  /** ↓ 旧数据兼容（P4 前落库为 camelCase） */
  nodeId?: string
  id?: string
  nodeType?: string
  classCode?: string
  skillCode?: string
  title?: string
  description?: string
  config?: Record<string, any>
  inputs?: Record<string, any>
  inputTypes?: Record<string, string>
  outputs?: Record<string, any>
  inputPorts?: any[]
  outputPorts?: any[]
  x?: number
  y?: number
}

/** 落库边（rule_json.edges[]）—— 外层键 PascalCase；保留 camelCase 兼容旧数据 */
export interface WorkflowEdge {
  Source?: string
  Target?: string
  SourceHandle?: string | null
  TargetHandle?: string | null
  source?: string
  target?: string
  sourceHandle?: string | null
  targetHandle?: string | null
}

/** 落库顶层（rule_json）—— 键 PascalCase */
export interface WorkflowConfig {
  Version?: number
  WorkflowType?: string
  Nodes?: WorkflowNode[]
  Edges?: WorkflowEdge[]
  OutputConfig?: Record<string, any>
  Glossary?: string
  /** ↓ 旧数据兼容 */
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
  NodeId: string
  NodeType: string
  Title: string
  Description?: string
  SkillCode?: string
  Config?: Record<string, any>
  Inputs?: Record<string, any>
  InputTypes?: Record<string, string>
  Outputs?: Record<string, any>
  InputPorts?: any[]
  OutputPorts?: any[]
}

export interface SerializedEdge {
  Source: string
  Target: string
  SourceHandle?: string | null
  TargetHandle?: string | null
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
 * <para>★ 落库键为 PascalCase（P4）；读取时容忍 camelCase（旧数据）。</para>
 */
export function deserialize(config: WorkflowConfig, layoutJson: LayoutJson | null = null): DeserializeResult {
  const rawNodes = config?.Nodes ?? config?.nodes
  if (!config || !rawNodes) {
    return { nodes: [], edges: [], idGenerator: new NodeIdGenerator(), migrated: false, oldToNew: {} }
  }

  const rawEdges = config.Edges ?? config.edges ?? []

  const positions = layoutJson?.nodePositions || {}
  let migrated = false
  const idGenerator = new NodeIdGenerator()
  const oldToNew: Record<string, string> = {}

  // 第 0 遍：把全部合法 ID **一次性**喂给计数器。
  // ⛔ 不可在下面 map 里逐节点 resetFromNodes：该方法先 clear 全表，逐节点调用后
  //   计数器只剩「最后一个合法节点」那一档 —— 之后 next('ai_node') 会从 0 起算，
  //   撞出已存在的 ai_node_n1（2026-10-15「AI 节点执行失败：same key already added.
  //   Key: ai_node_n1」事故根因：重复节点 ID 落库 ⇒ 后端 ToDictionary 直接抛）。
  idGenerator.resetFromNodes(
    rawNodes
      .map(n => ({ id: n.NodeId || n.nodeId || n.id || '' }))
      .filter(n => isValidNodeId(n.id))
  )

  // 第一遍：检测并迁移旧 ID（n${Date.now()} 格式）
  const nodes = rawNodes.map((n, idx) => {
    const nodeId = n.NodeId || n.nodeId || n.id || `n${idx}`
    let newId = nodeId

    if (!isValidNodeId(nodeId)) {
      const classCode = n.NodeType || n.nodeType || n.classCode || n.SkillCode || n.skillCode || 'skill'
      newId = idGenerator.next(classCode)
      oldToNew[nodeId] = newId
      migrated = true
    }

    const pos = positions[nodeId] || positions[newId] || {}
    const nodeType = n.NodeType || n.nodeType || n.classCode || (n.SkillCode || n.skillCode ? 'skill' : 'skill')
    const classCode = nodeType

    const specialMeta = getSpecialNode(classCode)
    // ★ 端口字段一律 PascalCase（2026-10-16 P3）：后端 metadata / rule_json / PortDef 三处同名，
    //   ⛔ 不再需要归一化层（portNormalize 已删除，见守卫 G3）。
    const inPorts = n.InputPorts || n.inputPorts
    const outPorts = n.OutputPorts || n.outputPorts
    const inputPorts: any[] = inPorts && inPorts.length ? inPorts : (specialMeta?.inputPorts || [])
    const outputPorts: any[] = outPorts && outPorts.length ? outPorts : (specialMeta?.outputPorts || [])

    return {
      id: newId,
      classCode,
      nodeType,
      title: n.Title || n.title || '',
      description: n.Description || n.description || '',
      skillCode: n.SkillCode || n.skillCode || '',
      x: pos.x ?? 0,
      y: pos.y ?? 0,
      config: n.Config || n.config || {},
      inputs: n.Inputs || n.inputs || {},
      inputTypes: n.InputTypes || n.inputTypes || {},
      outputs: n.Outputs || n.outputs || {},
      inputPorts,
      outputPorts
    }
  })

  // 第二遍：迁移边中的 ID 引用
  const edges = rawEdges.map(e => {
    const src = e.Source ?? e.source ?? ''
    const tgt = e.Target ?? e.target ?? ''
    const nSrc = oldToNew[src] || src
    const nTgt = oldToNew[tgt] || tgt
    return {
      id: `e-${nSrc}-${nTgt}`,
      source: nSrc,
      target: nTgt,
      sourceHandle: e.SourceHandle ?? e.sourceHandle ?? null,
      targetHandle: e.TargetHandle ?? e.targetHandle ?? null
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
 * 序列化业务层节点/边数组 → workflow_config JSON（落库格式，★ 外层键 PascalCase）
 */
export function serialize(
  nodes: Array<{ id: string; classCode: string; title: string; description?: string; skillCode?: string; config?: any; inputs?: any; inputTypes?: any; outputs?: any; inputPorts?: any[]; outputPorts?: any[] }>,
  edges: Array<{ source: string; target: string; sourceHandle?: string | null; targetHandle?: string | null }>,
  meta: { version?: number; workflowType?: string; outputConfig?: any; glossary?: string } = {}
): WorkflowConfig {
  const configNodes: SerializedNode[] = nodes.map(n => {
    const node: SerializedNode = {
      NodeId: n.id,
      NodeType: n.classCode || 'skill',
      Title: n.title || ''
    }
    if (n.description) node.Description = n.description
    if (n.skillCode) node.SkillCode = n.skillCode
    if (n.config && Object.keys(n.config).length > 0) node.Config = n.config
    if (n.inputs && Object.keys(n.inputs).length > 0) node.Inputs = n.inputs
    if (n.inputTypes && Object.keys(n.inputTypes).length > 0) node.InputTypes = n.inputTypes
    if (n.outputs && Object.keys(n.outputs).length > 0) node.Outputs = n.outputs
    if (n.inputPorts?.length) node.InputPorts = n.inputPorts
    if (n.outputPorts?.length) node.OutputPorts = n.outputPorts
    return node
  })

  const configEdges: SerializedEdge[] = edges.map(e => {
    const edge: SerializedEdge = { Source: e.source, Target: e.target }
    if (e.sourceHandle) edge.SourceHandle = e.sourceHandle
    if (e.targetHandle) edge.TargetHandle = e.targetHandle
    return edge
  })

  return {
    Version: meta.version ?? 1,
    WorkflowType: meta.workflowType ?? 'validation',
    Nodes: configNodes,
    Edges: configEdges,
    OutputConfig: meta.outputConfig ?? {},
    Glossary: meta.glossary ?? ''
  }
}

/**
 * 从业务层节点提取 layoutJson（★ 边界 F，**不翻** PascalCase）
 */
export function extractLayout(nodes: Array<{ id: string; x?: number; y?: number }>): LayoutJson {
  const nodePositions: Record<string, { x: number; y: number }> = {}
  for (const n of nodes) {
    nodePositions[n.id] = { x: n.x || 0, y: n.y || 0 }
  }
  return { nodePositions }
}
