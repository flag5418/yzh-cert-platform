/**
 * useWorkflowStore.js — 操作层（唯一变更入口）
 *
 * 设计规则（V2 §4.8）：
 * - 所有变更只能走操作层方法，禁止绕过
 * - 每个操作统一四步：前置校验 → 改 model → 触发副作用 → 记日志+置脏
 * - 收益：undo/redo 顺带实现；操作日志天然存在；一致性由架构强制
 *
 * 关联文档：V2 §4.8 操作层封闭集合
 */

import { reactive } from 'vue'
import { NodeIdGenerator, isValidNodeId } from '../model/nodeIdGenerator.js'
import { getSpecialNode } from '@/views/cert/Standard/WorkflowDesigner/specialNodes.js'

/**
 * 创建工作流操作层 store
 * @returns {Object} store 实例
 */
export function useWorkflowStore() {
  const state = reactive({
    /** @type {Array<Object>} 业务层节点数组 */
    nodes: [],
    /** @type {Array<Object>} 业务层边数组 */
    edges: [],
    /** @type {boolean} 脏标记 */
    dirty: false,
    /** @type {Array<Object>} 操作日志 */
    operationLog: [],
    /** @type {NodeIdGenerator} */
    idGenerator: new NodeIdGenerator(),
    /** @type {string|null} 当前选中节点 ID */
    selectedNodeId: null
  })

  // ── 操作日志 ──

  function logOperation(type, detail) {
    state.operationLog.push({
      type,
      detail,
      timestamp: Date.now()
    })
  }

  // ── 脏标记 ──

  function markDirty() {
    state.dirty = true
  }

  function markClean() {
    state.dirty = false
  }

  // ── 核心操作（封闭集合） ──

  /**
   * 添加节点
   * @param {Object} item - 节点元数据（来自 SkillPanel 或 specialNodes.js）
   * @param {number} x - 画布 X 坐标
   * @param {number} y - 画布 Y 坐标
   * @returns {Object|null} 新节点数据，失败返回 null
   */
  function addNode(item, x, y) {
    const classCode = item.classCode || item.nodeType || item.skillCode || 'skill'
    const meta = getSpecialNode(classCode)

    // 前置校验：singleton 检查
    if (meta?.singleton) {
      const exists = state.nodes.some(n => n.classCode === classCode)
      if (exists) {
        console.warn(`[Store] 节点 ${meta.className} 已存在，不允许重复添加`)
        return null
      }
    }

    // 生成唯一 ID
    const nodeId = state.idGenerator.next(classCode)

    // 构建节点数据
    const title = generateUniqueName(meta?.className || item.skillName || item.skillCode || classCode)
    const inputPorts = item.inputPorts?.length
      ? item.inputPorts
      : (meta?.inputPorts || [])
    const outputPorts = item.outputPorts?.length
      ? item.outputPorts
      : (meta?.outputPorts || [])

    const node = {
      id: nodeId,
      classCode,
      nodeType: meta ? classCode : 'skill',
      title,
      skillCode: item.skillCode || '',
      x,
      y,
      config: buildDefaultConfig(classCode),
      inputs: buildDefaultInputs(inputPorts),
      outputs: {},
      inputPorts,
      outputPorts
    }

    // 改 model
    state.nodes.push(node)
    markDirty()
    logOperation('addNode', { nodeId, classCode, title })

    return node
  }

  /**
   * 删除节点（级联删除关联边 + 输入引用回退）
   * @param {string} nodeId
   * @returns {boolean} 是否成功
   */
  function removeNode(nodeId) {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return false

    // 前置校验：start 不可删除
    if (node.classCode === 'start' || node.nodeType === 'start') {
      console.warn('[Store] 开始节点不可删除')
      return false
    }

    // 级联删除关联边
    const removedEdges = state.edges.filter(
      e => e.source === nodeId || e.target === nodeId
    )
    state.edges = state.edges.filter(
      e => e.source !== nodeId && e.target !== nodeId
    )

    // 回退其他节点的 inputs 引用
    for (const other of state.nodes) {
      for (const [port, val] of Object.entries(other.inputs)) {
        if (val === nodeId) {
          other.inputs[port] = ''
        }
      }
    }

    // 改 model
    state.nodes = state.nodes.filter(n => n.id !== nodeId)
    if (state.selectedNodeId === nodeId) {
      state.selectedNodeId = null
    }
    markDirty()
    logOperation('removeNode', { nodeId, removedEdgesCount: removedEdges.length })

    return true
  }

  /**
   * 重命名节点
   * @param {string} nodeId
   * @param {string} newTitle
   * @returns {boolean}
   */
  function renameNode(nodeId, newTitle) {
    if (!newTitle?.trim()) return false

    // 前置校验：画布内唯一
    if (!isNameUnique(newTitle, nodeId)) {
      console.warn(`[Store] 节点名称「${newTitle}」已存在`)
      return false
    }

    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return false

    const oldTitle = node.title
    node.title = newTitle.trim()
    markDirty()
    logOperation('renameNode', { nodeId, oldTitle, newTitle: node.title })

    return true
  }

  /**
   * 移动节点（坐标变化）
   * @param {string} nodeId
   * @param {number} x
   * @param {number} y
   */
  function moveNode(nodeId, x, y) {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return

    node.x = x
    node.y = y
    markDirty()
    // 坐标移动不记日志（高频操作）
  }

  /**
   * 连线（端口级）
   * @param {string} source - 源节点 ID
   * @param {string} target - 目标节点 ID
   * @param {string|null} sourceHandle - 源输出端口
   * @param {string|null} targetHandle - 目标输入端口
   * @returns {Object|null} 新边数据
   */
  function connect(source, target, sourceHandle = null, targetHandle = null) {
    // 前置校验
    if (source === target) {
      console.warn('[Store] 禁止自连')
      return null
    }

    // 重复边检查
    const exists = state.edges.some(
      e => e.source === source && e.target === target &&
           e.sourceHandle === sourceHandle && e.targetHandle === targetHandle
    )
    if (exists) {
      console.warn('[Store] 重复连线')
      return null
    }

    // maxIn 检查（目标端口至多 1 条入边，end 的 result 端口例外）
    if (targetHandle) {
      const targetNode = state.nodes.find(n => n.id === target)
      const isEnd汇聚 = targetNode?.classCode === 'end' && targetHandle === 'result'
      if (!isEnd汇聚) {
        const incomingCount = state.edges.filter(
          e => e.target === target && e.targetHandle === targetHandle
        ).length
        if (incomingCount >= 1) {
          console.warn(`[Store] 端口 ${targetHandle} 已有入边`)
          return null
        }
      }
    }

    // 删除目标端口已有的边（替换连线）
    state.edges = state.edges.filter(
      e => !(e.target === target && e.targetHandle === targetHandle)
    )

    // 创建新边
    const edgeId = `e-${source}-${target}-${Date.now()}`
    const edge = {
      id: edgeId,
      source,
      target,
      sourceHandle,
      targetHandle
    }
    state.edges.push(edge)

    // 自动绑定目标端口输入
    if (targetHandle) {
      const targetNode = state.nodes.find(n => n.id === target)
      if (targetNode) {
        targetNode.inputs[targetHandle] = source
      }
    }

    markDirty()
    logOperation('connect', { source, target, sourceHandle, targetHandle })

    return edge
  }

  /**
   * 断开连线
   * @param {string} edgeId
   * @returns {boolean}
   */
  function disconnect(edgeId) {
    const edge = state.edges.find(e => e.id === edgeId)
    if (!edge) return false

    // 回退目标端口输入
    if (edge.targetHandle) {
      const targetNode = state.nodes.find(n => n.id === edge.target)
      if (targetNode && targetNode.inputs[edge.targetHandle] === edge.source) {
        targetNode.inputs[edge.targetHandle] = ''
      }
    }

    state.edges = state.edges.filter(e => e.id !== edgeId)
    markDirty()
    logOperation('disconnect', { edgeId, source: edge.source, target: edge.target })

    return true
  }

  /**
   * 设置输入参数值（面板编辑）
   * @param {string} nodeId
   * @param {string} portName
   * @param {any} value
   */
  function setInputValue(nodeId, portName, value) {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return

    node.inputs[portName] = value
    markDirty()
  }

  /**
   * 设置节点配置（面板编辑）
   * @param {string} nodeId
   * @param {Object} config
   */
  function setConfig(nodeId, config) {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return

    node.config = { ...node.config, ...config }
    markDirty()
  }

  /**
   * 更新节点全部属性（来自 NodePropertyForm applyChanges）
   * @param {string} nodeId
   * @param {Object} updates - { title, config, inputs, inputPorts, outputPorts, ... }
   */
  function updateNode(nodeId, updates) {
    const node = state.nodes.find(n => n.id === nodeId)
    if (!node) return

    if (updates.title && !isNameUnique(updates.title, nodeId)) {
      console.warn(`[Store] 节点名称「${updates.title}」已存在`)
      return false
    }

    Object.assign(node, updates)
    markDirty()
    logOperation('updateNode', { nodeId, keys: Object.keys(updates) })

    return true
  }

  /**
   * 清空画布（二次确认由 UI 层处理）
   */
  function clearAll() {
    state.nodes = []
    state.edges = []
    state.idGenerator.clear()
    state.selectedNodeId = null
    state.dirty = false
    state.operationLog = []
    logOperation('clearAll', {})
  }

  /**
   * 加载配置（含旧数据迁移）
   * @param {Array} nodes
   * @param {Array} edges
   */
  function loadFromData(nodes, edges) {
    state.nodes = nodes
    state.edges = edges
    state.idGenerator.resetFromNodes(nodes)
    state.dirty = false
    state.operationLog = []
    logOperation('loadFromData', { nodeCount: nodes.length, edgeCount: edges.length })
  }

  // ── 辅助方法 ──

  function generateUniqueName(baseName) {
    const existing = new Set(state.nodes.map(n => n.title || ''))
    if (!existing.has(baseName)) return baseName
    let n = 2
    while (existing.has(`${baseName} ${n}`)) n++
    return `${baseName} ${n}`
  }

  function isNameUnique(name, excludeNodeId = null) {
    return !state.nodes.some(n =>
      n.id !== excludeNodeId && (n.title || '') === name
    )
  }

  function buildDefaultConfig(classCode) {
    const config = {}
    if (classCode === 'docField') { config.docCode = ''; config.fieldCode = '' }
    if (classCode === 'docTable') { config.docCode = ''; config.tableCode = '' }
    if (classCode === 'ai_node') { config.prompt = ''; config.jsonMode = true }
    return config
  }

  function buildDefaultInputs(inputPorts) {
    const defaults = {}
    for (const port of (inputPorts || [])) {
      if (port.defaultValue !== undefined && port.defaultValue !== '') {
        defaults[port.name] = port.defaultValue
      }
    }
    return defaults
  }

  // ── 查询方法 ──

  function getNodeById(id) {
    return state.nodes.find(n => n.id === id) || null
  }

  function getEdgesByNode(nodeId) {
    return state.edges.filter(e => e.source === nodeId || e.target === nodeId)
  }

  function getIncomingEdges(nodeId) {
    return state.edges.filter(e => e.target === nodeId)
  }

  function getOutgoingEdges(nodeId) {
    return state.edges.filter(e => e.source === nodeId)
  }

  // ── 返回 store ──

  return {
    state,
    // 操作
    addNode,
    removeNode,
    renameNode,
    moveNode,
    connect,
    disconnect,
    setInputValue,
    setConfig,
    updateNode,
    clearAll,
    loadFromData,
    markClean,
    // 查询
    getNodeById,
    getEdgesByNode,
    getIncomingEdges,
    getOutgoingEdges,
    isNameUnique,
    // 辅助
    generateUniqueName
  }
}
