/**
 * LogicFlow v2 自定义 branch 节点
 *
 * 菱形外观 + 双输出锚点（success 右上 / failure 右下）
 */

import { createElement as h } from 'preact/compat'
import { BaseNode, BaseNodeModel } from '@logicflow/core'

class BranchNode extends BaseNode {
  static extendKey = 'BranchNode'
  width = 120
  height = 80

  getShape() {
    const { x, y, width, height } = this.props.model
    const style = this.props.model.getNodeStyle() || {}
    const fill = style.fill || '#FDF6EC'
    const stroke = style.stroke || '#E6A23C'
    const strokeWidth = style.strokeWidth || 2
    const left = x - width / 2, right = x + width / 2
    const top = y - height / 2, bottom = y + height / 2
    const midX = x, midY = y

    return h('g', null,
      h('polygon', {
        points: `${midX},${top} ${right},${midY} ${midX},${bottom} ${left},${midY}`,
        fill,
        stroke,
        strokeWidth
      }),
      h('circle', { cx: right, cy: midY - 15, r: 5, fill: '#67C23A', stroke: '#67C23A', strokeWidth: 1, className: 'branch-anchor-success' }),
      h('circle', { cx: right, cy: midY + 15, r: 5, fill: '#F56C6C', stroke: '#F56C6C', strokeWidth: 1, className: 'branch-anchor-failure' }),
      h('text', { x: right + 8, y: midY - 12, fill: '#67C23A', fontSize: 10, fontWeight: 600 }, 'T'),
      h('text', { x: right + 8, y: midY + 20, fill: '#F56C6C', fontSize: 10, fontWeight: 600 }, 'F'),
      h('circle', { cx: left, cy: midY, r: 5, fill: '#fff', stroke: '#E6A23C', strokeWidth: 2 })
    )
  }
}

class BranchNodeModel extends BaseNodeModel {
  static extendKey = 'BranchNodeModel'

  initNodeData(data: any) {
    super.initNodeData(data)
  }

  getAnchors() {
    const { x, y, width } = this
    const left = x - width / 2, right = x + width / 2, midY = y
    return [
      { x: left, y: midY, id: 'condition', type: 'input' as const },
      { x: right, y: midY - 15, id: 'success', type: 'output' as const },
      { x: right, y: midY + 15, id: 'failure', type: 'output' as const }
    ]
  }
}

export function registerBranchNode(lf: any): void {
  lf.register({ type: 'branch', view: BranchNode, model: BranchNodeModel })
}
