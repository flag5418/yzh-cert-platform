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
  private _counters: Map<string, number>

  constructor() {
    this._counters = new Map()
  }

  /**
   * 生成下一个唯一节点 ID
   */
  next(classCode: string): string {
    const current = this._counters.get(classCode) || 0
    const nextNum = current + 1
    this._counters.set(classCode, nextNum)
    return `${classCode}_n${nextNum}`
  }

  /**
   * 从已有节点列表重置计数器（加载工作流时调用）
   */
  resetFromNodes(nodes: Array<{ id: string }>): void {
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
   */
  getCurrentMax(classCode: string): number {
    return this._counters.get(classCode) || 0
  }

  /**
   * 清空所有计数器
   */
  clear(): void {
    this._counters.clear()
  }

  /**
   * 获取当前所有计数器快照（调试用）
   */
  snapshot(): Record<string, number> {
    return Object.fromEntries(this._counters)
  }
}

/**
 * 检查节点 ID 是否为合法的 classCode_n{序号} 格式
 */
export function isValidNodeId(id: string): boolean {
  return /^[a-zA-Z_]+_n\d+$/.test(id)
}

/**
 * 从节点 ID 提取 classCode
 */
export function extractClassCode(id: string): string {
  const match = id.match(/^([a-zA-Z_]+)_n\d+$/)
  return match ? match[1] : id
}
