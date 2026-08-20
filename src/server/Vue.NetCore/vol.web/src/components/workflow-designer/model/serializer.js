/**
 * workflow-config/serializer.js
 *
 * configJSON ⇄ 业务层数据模型转换（含旧数据迁移）
 *
 * 设计规则（V2 §5.2）：
 * - configJSON 落库格式：{ version, workflowType, nodes[], edges[], outputConfig, glossary }
 * - 业务层模型：{ id, classCode, title, x, y, config, inputs, inputPorts, outputPorts }
 * - 旧数据（n${Date.now()} 时间戳 ID）加载时迁移重编号为 {classCode}_n{序号}
 *
 * 关联文档：V2 §5.2 workflow_config JSON 规范
 */

import { NodeIdGenerator, isValidNodeId } from './nodeIdGenerator.js'
import { getSpecialNode, SPECIAL_NODE_CODES } from '@/views/cert/Standard/WorkflowDesigner/specialNodes.js'

/**
 * 反编译 workflow_config JSON → 业务层节点/边数组
 *
 * @param {Object} config - workflow_config JSON（落库格式）
 * @param {Object|null} layoutJson - 布局数据 { nodePositions: { id: { x, y } } }
 * @returns {{ nodes: Array, edges: Array, idGenerator: NodeIdGenerator, migrated: boolean }}
 */
export function deserialize(config, layoutJson = null) {
  if (!config || !config.nodes) {
    return { nodes: [], edges: [], idGenerator: new NodeIdGenerator(), migrated: false }
  }

  const positions = layoutJson?.nodePositions || {}
  let migrated = false
  const idGenerator = new NodeIdGenerator()
  const oldToNew = {} // 旧 ID → 新 ID 映射

  // 第一遍：检测并迁移旧 ID（n${Date.now()} 格式）
  const nodes = config.nodes.map((n, idx) => {
    const nodeId = n.nodeId || n.id || `n${idx}`
    let newId = nodeId

    if (!isValidNodeId(nodeId)) {
      // 旧格式：n${timestamp} → 迁移为 classCode_n{序号}
      const classCode = n.nodeType || n.classCode || n.skillCode || 'skill'
      newId = idGenerator.next(classCode)
      oldToNew[nodeId] = newId
      migrated = true
    } else {
      // 新格式：更新计数器
      idGenerator.resetFromNodes([{ id: newId }])
    }

    const pos = positions[nodeId] || positions[newId] || {}
    const nodeType = n.nodeType || n.classCode || (n.skillCode ? 'skill' : 'skill')
    const classCode = nodeType

    // 获取端口声明（特殊节点从元数据，功能节点从 JSON）
    const specialMeta = getSpecialNode(classCode)
    const inputPorts = n.inputPorts?.length
      ? n.inputPorts
      : (specialMeta?.inputPorts || [])
    const outputPorts = n.outputPorts?.length
      ? n.outputPorts
      : (specialMeta?.outputPorts || [])

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
    // 过滤掉引用不存在节点的边
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
 *
 * @param {Array} nodes - 业务层节点数组
 * @param {Array} edges - 业务层边数组
 * @param {Object} meta - 元数据 { version, workflowType, outputConfig, glossary }
 * @returns {Object} workflow_config JSON
 */
export function serialize(nodes, edges, meta = {}) {
  const configNodes = nodes.map(n => {
    const node = {
      nodeId: n.id,
      nodeType: n.classCode || n.nodeType || 'skill',
      title: n.title || ''
    }
    if (n.skillCode) node.skillCode = n.skillCode
    if (n.config && Object.keys(n.config).length > 0) node.config = n.config
    if (n.inputs && Object.keys(n.inputs).length > 0) node.inputs = n.inputs
    if (n.outputs && Object.keys(n.outputs).length > 0) node.outputs = n.outputs
    // 端口声明进 JSON（供引擎和校验使用）
    if (n.inputPorts?.length) node.inputPorts = n.inputPorts
    if (n.outputPorts?.length) node.outputPorts = n.outputPorts
    return node
  })

  const configEdges = edges.map(e => {
    const edge = { source: e.source, target: e.target }
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
 * @param {Array} nodes
 * @returns {Object} { nodePositions: { id: { x, y } } }
 */
export function extractLayout(nodes) {
  const nodePositions = {}
  for (const n of nodes) {
    nodePositions[n.id] = { x: n.x || 0, y: n.y || 0 }
  }
  return { nodePositions }
}
