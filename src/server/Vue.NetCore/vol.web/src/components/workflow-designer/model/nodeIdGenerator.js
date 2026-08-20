/**
 * 节点编号生成器
 *
 * 设计规则（V2 §4.2）：
 * - id = ${classCode}_n{同类序号}，如 compare_n1、ai_node_n2
 * - 同类唯一编码，永远以同类最大编号 +1
 * - 删除不复用（删除 compare_n2 后新增为 compare_n3）
 * - 加载时按类扫描取最大序号重置计数器
 */

export class NodeIdGenerator {
  constructor() {
    /** @type {Map<string, number>} classCode → 当前最大序号 */
    this._counters = new Map()
  }

  /**
   * 生成下一个唯一节点 ID
   * @param {string} classCode - 节点类型编码（如 'compare'、'ai_node'、'start'）
   * @returns {string} 如 'compare_n1'
   */
  next(classCode) {
    const current = this._counters.get(classCode) || 0
    const nextNum = current + 1
    this._counters.set(classCode, nextNum)
    return `${classCode}_n${nextNum}`
  }

  /**
   * 从已有节点列表重置计数器（加载工作流时调用）
   * @param {Array<{id: string}>} nodes - 画布上的节点列表
   */
  resetFromNodes(nodes) {
    this._counters.clear()
    for (const node of nodes) {
      const id = node.id || ''
      const match = id.match(/^([a-zA-Z_]+)_n(\d+)$/)
      if (match) {
        const classCode = match[1]
        const num = parseInt(match[2], 10)
        const current = this._counters.get(classCode) || 0
        if (num > current) {
          this._counters.set(classCode, num)
        }
      }
    }
  }

  /**
   * 获取指定 classCode 的当前最大序号
   * @param {string} classCode
   * @returns {number}
   */
  getCurrentMax(classCode) {
    return this._counters.get(classCode) || 0
  }

  /**
   * 清空所有计数器
   */
  clear() {
    this._counters.clear()
  }

  /**
   * 获取当前所有计数器快照（调试用）
   * @returns {Object}
   */
  snapshot() {
    return Object.fromEntries(this._counters)
  }
}

/**
 * 检查节点 ID 是否为合法的 classCode_n{序号} 格式
 * @param {string} id
 * @returns {boolean}
 */
export function isValidNodeId(id) {
  return /^[a-zA-Z_]+_n\d+$/.test(id)
}

/**
 * 从节点 ID 提取 classCode
 * @param {string} id - 如 'compare_n1'
 * @returns {string} 如 'compare'
 */
export function extractClassCode(id) {
  const match = id.match(/^([a-zA-Z_]+)_n\d+$/)
  return match ? match[1] : id
}
