/**
 * useWorkflowStore.ts — 操作层（唯一变更入口）
 *
 * 设计规则（V2 §4.8）：
 * - 所有变更只能走操作层方法，禁止绕过
 * - 每个操作统一四步：前置校验 → 改 model → 触发副作用 → 记日志+置脏
 * - 收益：undo/redo 顺带实现；操作日志天然存在；一致性由架构强制
 */

import { reactive } from 'vue'
import { NodeIdGenerator } from '@share/composables/workflow/nodeIdGenerator'
import { getSpecialNode, type SpecialNodeMeta, type PortDef } from '@share/composables/workflow/specialNodes'

export interface WorkflowNode {
  id: string
  classCode: string
  nodeType: string
  title: string
  skillCode: string
  x: number
  y: number
  config: Record<string, any>
  inputs: Record<string, any>
  inputTypes?: Record<string, string>
  outputs: Record<string, any>
  inputPorts: PortDef[]
  outputPorts: PortDef[]
}

export interface WorkflowEdge {
  id: string
  source: string
  target: string
  sourceHandle?: string | null
  targetHandle?: string | null
}

export interface WorkflowState {
  nodes: WorkflowNode[]
  edges: WorkflowEdge[]
  dirty: boolean
  operationLog: Array<{ type: string; detail: any; timestamp: number }>
  idGenerator: NodeIdGenerator
  selectedNodeId: string | null
}

export interface WorkflowStore {
  state: WorkflowState
  addNode: (item: any, x: number, y: number) => WorkflowNode | null
  removeNode: (nodeId: string) => boolean
  renameNode: (nodeId: string, newTitle: string) => boolean
  moveNode: (nodeId: string, x: number, y: number) => void
  connect: (source: string, target: string, sourceHandle?: string | null, targetHandle?: string | null) => WorkflowEdge | null
  disconnect: (edgeId: string) => boolean
  setInputValue: (nodeId: string, portName: string, value: any) => void
  setConfig: (nodeId: string, config: Record<string, any>) => void
  updateNode: (nodeId: string, updates: Partial<WorkflowNode>) => boolean
  clearAll: () => void
  loadFromData: (nodes: WorkflowNode[], edges: WorkflowEdge[]) => void
  markDirty: () => void
  markClean: () => void
  getNodeById: (id: string) => WorkflowNode | null
  getEdgesByNode: (nodeId: string) => WorkflowEdge[]
  getIncomingEdges: (nodeId: string) => WorkflowEdge[]
  getOutgoingEdges: (nodeId: string) => WorkflowEdge[]
  isNameUnique: (name: string, excludeNodeId?: string | null) => boolean
  generateUniqueName: (baseName: string) => string
}

export function useWorkflowStore(): WorkflowStore {
  // @ts-ignore - reactive() inference false positive with class instances
  const state: WorkflowState = reactive({
    nodes: [],
    edges: [],
    dirty: false,
    operationLog: [],
    idGenerator: new NodeIdGenerator(),
    selectedNodeId: null
  })

  function logOperation(type: string, detail: any): void {
    state.operationLog.push({ type, detail, timestamp: Date.now() })
  }

  function markDirty(): void { state.dirty = true }
  function markClean(): void { state.dirty = false }

  function addNode(item: any, x: number, y: number): WorkflowNode | null {
    const classCode = item.classCode || item.nodeType || item.skillCode || 'skill'
    const meta = getSpecialNode(classCode) as SpecialNodeMeta | null

    if (meta?.singleton) {
      const exists = state.nodes.some(n => n.classCode === classCode)
      if (exists) { console.warn(`[Store] 节点 ${meta.className} 已存在，不允许重复添加`); return null }
    }

    const nodeId = state.idGenerator.next(classCode)
    const title = generateUniqueName(meta?.className || item.skillName || item.skillCode || classCode)
    const inputPorts = item.inputPorts?.length ? item.inputPorts : (meta?.inputPorts || [])
    const outputPorts = item.outputPorts?.length ? item.outputPorts : (meta?.outputPorts || [])

    const node: WorkflowNode = {
      id: nodeId, classCode, nodeType: meta ? classCode : 'skill', title,
      skillCode: item.skillCode || '', x, y,
      config: buildDefaultConfig(classCode),
      inputs: buildDefaultInputs(inputPorts),
      inputTypes: {}, outputs: {}, inputPorts, outputPorts
    }

    state.nodes.push(node)
    markDirty()
    logOperation('addNode', { nodeId, classCode, title })
    return node
  }

  function removeNode(nodeId: string): boolean {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return false
    if (node.classCode === 'start' || node.nodeType === 'start') { console.warn('[Store] 开始节点不可删除'); return false }

    state.edges = state.edges.filter(e => e.source !== nodeId && e.target !== nodeId)

    for (const other of state.nodes) {
      for (const [port, val] of Object.entries(other.inputs)) {
        if (val === nodeId) { other.inputs[port] = ''; if (other.inputTypes) delete other.inputTypes[port] }
      }
    }

    state.nodes = state.nodes.filter(n => n.id !== nodeId)
    if (state.selectedNodeId === nodeId) state.selectedNodeId = null
    markDirty()
    logOperation('removeNode', { nodeId })
    return true
  }

  function renameNode(nodeId: string, newTitle: string): boolean {
    if (!newTitle?.trim()) return false
    if (!isNameUnique(newTitle, nodeId)) { console.warn(`[Store] 节点名称「${newTitle}」已存在`); return false }
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return false
    const oldTitle = node.title
    node.title = newTitle.trim()
    markDirty()
    logOperation('renameNode', { nodeId, oldTitle, newTitle: node.title })
    return true
  }

  function moveNode(nodeId: string, x: number, y: number): void {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return
    node.x = x; node.y = y
    markDirty()
  }

  function connect(source: string, target: string, sourceHandle: string | null = null, targetHandle: string | null = null): WorkflowEdge | null {
    if (source === target) { console.warn('[Store] 禁止自连'); return null }

    const exists = state.edges.some(e => e.source === source && e.target === target && e.sourceHandle === sourceHandle && e.targetHandle === targetHandle)
    if (exists) { console.warn('[Store] 重复连线'); return null }

    if (targetHandle) {
      const targetNode = state.nodes.find(n => n.id === target)
      const isEndConvergence = targetNode?.classCode === 'end' && targetHandle === 'result'
      if (!isEndConvergence) {
        const incomingCount = state.edges.filter(e => e.target === target && e.targetHandle === targetHandle).length
        if (incomingCount >= 1) { console.warn(`[Store] 端口 ${targetHandle} 已有入边`); return null }
      }
    }

    state.edges = state.edges.filter(e => !(e.target === target && e.targetHandle === targetHandle))

    const edgeId = `e-${source}-${target}-${Date.now()}`
    const edge: WorkflowEdge = { id: edgeId, source, target, sourceHandle, targetHandle }
    state.edges.push(edge)

    if (targetHandle) {
      const targetNode = state.nodes.find(n => n.id === target)
      if (targetNode) {
        targetNode.inputs[targetHandle] = source
        if (!targetNode.inputTypes) targetNode.inputTypes = {}
        targetNode.inputTypes[targetHandle] = 'link'
      }
    }

    markDirty()
    logOperation('connect', { source, target, sourceHandle, targetHandle })
    return edge
  }

  function disconnect(edgeId: string): boolean {
    const edge = state.edges.find(e => e.id === edgeId)
    if (!edge) return false

    if (edge.targetHandle) {
      const targetNode = state.nodes.find(n => n.id === edge.target)
      if (targetNode && targetNode.inputs[edge.targetHandle] === edge.source) {
        targetNode.inputs[edge.targetHandle] = ''
        if (targetNode.inputTypes) delete targetNode.inputTypes[edge.targetHandle]
      }
    }

    state.edges = state.edges.filter(e => e.id !== edgeId)
    markDirty()
    logOperation('disconnect', { edgeId, source: edge.source, target: edge.target })
    return true
  }

  function setInputValue(nodeId: string, portName: string, value: any): void {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return
    node.inputs[portName] = value
    markDirty()
  }

  function setConfig(nodeId: string, config: Record<string, any>): void {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return
    node.config = { ...node.config, ...config }
    markDirty()
  }

  function updateNode(nodeId: string, updates: Partial<WorkflowNode>): boolean {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return false
    if (updates.title && !isNameUnique(updates.title, nodeId)) { console.warn(`[Store] 节点名称「${updates.title}」已存在`); return false }
    Object.assign(node, updates)
    markDirty()
    logOperation('updateNode', { nodeId, keys: Object.keys(updates) })
    return true
  }

  function clearAll(): void {
    state.nodes = []; state.edges = []; state.idGenerator.clear()
    state.selectedNodeId = null; state.dirty = false; state.operationLog = []
    logOperation('clearAll', {})
  }

  function loadFromData(nodes: WorkflowNode[], edges: WorkflowEdge[]): void {
    state.nodes = nodes; state.edges = edges
    state.idGenerator.resetFromNodes(nodes)
    state.dirty = false; state.operationLog = []
    logOperation('loadFromData', { nodeCount: nodes.length, edgeCount: edges.length })
  }

  function generateUniqueName(baseName: string): string {
    const existing = new Set(state.nodes.map(n => n.title || ''))
    if (!existing.has(baseName)) return baseName
    let n = 2
    while (existing.has(`${baseName} ${n}`)) n++
    return `${baseName} ${n}`
  }

  function isNameUnique(name: string, excludeNodeId: string | null = null): boolean {
    return !state.nodes.some(n => n.id !== excludeNodeId && (n.title || '') === name)
  }

  function buildDefaultConfig(classCode: string): Record<string, any> {
    const config: Record<string, any> = {}
    if (classCode === 'docField') { config.docCode = ''; config.fieldCode = '' }
    if (classCode === 'docTable') { config.docCode = ''; config.tableCode = '' }
    if (classCode === 'ai_node') { config.prompt = ''; config.jsonMode = true }
    return config
  }

  function buildDefaultInputs(inputPorts: PortDef[]): Record<string, any> {
    const defaults: Record<string, any> = {}
    for (const port of (inputPorts || [])) {
      if (port.defaultValue !== undefined && port.defaultValue !== '') defaults[port.name] = port.defaultValue
    }
    return defaults
  }

  function getNodeById(id: string): WorkflowNode | null { return state.nodes.find(n => n.id === id) || null }
  function getEdgesByNode(nodeId: string): WorkflowEdge[] { return state.edges.filter(e => e.source === nodeId || e.target === nodeId) }
  function getIncomingEdges(nodeId: string): WorkflowEdge[] { return state.edges.filter(e => e.target === nodeId) }
  function getOutgoingEdges(nodeId: string): WorkflowEdge[] { return state.edges.filter(e => e.source === nodeId) }

  return {
    state, addNode, removeNode, renameNode, moveNode, connect, disconnect,
    setInputValue, setConfig, updateNode, clearAll, loadFromData,
    markDirty, markClean, getNodeById, getEdgesByNode, getIncomingEdges, getOutgoingEdges,
    isNameUnique, generateUniqueName
  }
}
