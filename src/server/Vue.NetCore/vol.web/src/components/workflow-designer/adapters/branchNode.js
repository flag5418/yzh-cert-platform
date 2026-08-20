/**
 * LogicFlow 自定义 branch 节点
 *
 * 菱形外观 + 双输出锚点（success 右上 / failure 右下）
 * 用户从不同锚点拖线即绑定分支
 */

/**
 * 注册 branch 自定义节点到 LogicFlow 实例
 * @param {import('@logicflow/core').default} lf - LogicFlow 实例
 */
export function registerBranchNode(lf) {
  // 定义 branch 节点的 SVG 外观
  class BranchNode extends lf.graph.BaseNode {
    static extendKey = 'BranchNode'

    // 节点尺寸
    width = 120
    height = 80

    // 获取外观模型
    getShape() {
      const { x, y, width, height, id } = this.props.model
      const style = this.props.model.getEdgeStyle() || {}
      const { fill = '#FDF6EC', stroke = '#E6A23C', strokeWidth = 2 } = this.props.model.getNodeStyle() || {}

      // 菱形四个顶点
      const left = x - width / 2
      const right = x + width / 2
      const top = y - height / 2
      const bottom = y + height / 2
      const midX = x
      const midY = y

      return {
        group: true,
        children: [
          // 菱形主体
          {
            tag: 'polygon',
            attributes: {
              points: `${midX},${top} ${right},${midY} ${midX},${bottom} ${left},${midY}`,
              fill,
              stroke,
              'stroke-width': strokeWidth
            }
          },
          // 成功锚点（右上）
          {
            tag: 'circle',
            attributes: {
              cx: right,
              cy: midY - 15,
              r: 5,
              fill: '#67C23A',
              stroke: '#67C23A',
              'stroke-width': 1
            },
            class: 'branch-anchor-success'
          },
          // 失败锚点（右下）
          {
            tag: 'circle',
            attributes: {
              cx: right,
              cy: midY + 15,
              r: 5,
              fill: '#F56C6C',
              stroke: '#F56C6C',
              'stroke-width': 1
            },
            class: 'branch-anchor-failure'
          },
          // 成功标签
          {
            tag: 'text',
            attributes: {
              x: right + 8,
              y: midY - 12,
              fill: '#67C23A',
              'font-size': '10',
              'font-weight': '600'
            },
            children: [{ tag: 'text', text: 'T', value: 'T' }]
          },
          // 失败标签
          {
            tag: 'text',
            attributes: {
              x: right + 8,
              y: midY + 20,
              fill: '#F56C6C',
              'font-size': '10',
              'font-weight': '600'
            },
            children: [{ tag: 'text', text: 'F', value: 'F' }]
          },
          // 输入锚点（左侧）
          {
            tag: 'circle',
            attributes: {
              cx: left,
              cy: midY,
              r: 5,
              fill: '#fff',
              stroke: '#E6A23C',
              'stroke-width': 2
            }
          }
        ]
      }
    }
  }

  // 定义 branch 节点的锚点配置
  class BranchNodeModel extends lf.graph.BaseNode.model {
    static extendKey = 'BranchNodeModel'

    // 锚点定义
    initNodeData(data) {
      super.initNodeData(data)
      // 不使用默认锚点
      this.anchors = []
    }

    // 自定义锚点位置
    getAnchors() {
      const { x, y, width, height } = this
      const left = x - width / 2
      const right = x + width / 2
      const midY = y

      return [
        // 左侧输入锚点
        { x: left, y: midY, id: 'condition', type: 'input' },
        // 右上成功输出锚点
        { x: right, y: midY - 15, id: 'success', type: 'output' },
        // 右下失败输出锚点
        { x: right, y: midY + 15, id: 'failure', type: 'output' }
      ]
    }
  }

  // 注册节点类型
  lf.register({
    type: 'branch',
    view: BranchNode,
    model: BranchNodeModel
  })
}
