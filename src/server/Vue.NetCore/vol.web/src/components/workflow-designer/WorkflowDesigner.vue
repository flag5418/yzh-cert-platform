import { Diagram } from '@logicflow/core'
import { HtmlPlugin } from '@logicflow/extension'
import '@logicflow/core/dist/index.css'
import '@logicflow/extension/dist/index.css'

/**
 * WorkflowDesigner.vue
 * LogicFlow 工作流设计器容器组件
 * 
 * 用法：
 * <WorkflowDesigner
 *   ref="designer"
 *   :initial-config="workflowConfig"
 *   :skills="skillList"
 *   @save="onSave"
 * />
 */
export default {
  name: 'WorkflowDesigner',
  props: {
    // 初始 workflow_config JSON 对象（编辑已有工作流时传入）
    initialConfig: {
      type: Object,
      default: () => ({ nodes: [], edges: [], branches: [], version: 1, workflowType: 'validation', outputConfig: {} })
    },
    // 可用 Skill 列表
    skills: {
      type: Array,
      default: () => []
    },
    // 当前选中的节点 ID
    selectedNodeId: {
      type: String,
      default: null
    }
  },
  emits: ['node-selected', 'config-change', 'save'],
  setup(props, { emit }) {
    // 由父组件通过 ref 调用
    return {}
  },
  mounted() {
    this.initDiagram()
  },
  beforeUnmount() {
    this.diagram?.destroy()
  },
  methods: {
    initDiagram() {
      this.diagram = new Diagram({
        container: this.$refs.canvas,
        grid: true,
        background: '#fafafa',
        plugins: [HtmlPlugin],
        behavior: {
          scroll: true,
          zoom: true,
          drag: true
        }
      })

      // 注册自定义 Skill 节点
      this.registerSkillNode()

      // 加载初始数据
      if (props.initialConfig?.nodes?.length > 0) {
        const { decompileToGraphData } = require('./compiler')
        const { graphData } = decompileToGraphData(props.initialConfig)
        this.diagram.render(graphData)
      }

      // 节点选中事件
      this.diagram.on('node:click', ({ data }) => {
        emit('node-selected', data.id, data)
      })

      this.diagram.on('edge:click', ({ data }) => {
        emit('node-selected', null)
      })

      this.diagram.on('blank:click', () => {
        emit('node-selected', null)
      })
    },

    registerSkillNode() {
      const { Node, Path } = window.lf || require('@logicflow/core')
      // LogicFlow v2 通过 extend 注册
      // 简化处理：使用内置 rect 节点，通过 data.skillCode 区分
    },

    /**
     * 导出 workflow_config JSON
     */
    exportConfig() {
      const { compileToWorkflowConfig } = require('./compiler')
      const graphData = this.diagram?.getGraphData()
      if (!graphData) return null
      return compileToWorkflowConfig(graphData)
    },

    /**
     * 导入 workflow_config JSON
     */
    importConfig(config) {
      const { decompileToGraphData } = require('./compiler')
      const { graphData } = decompileToGraphData(config)
      this.diagram?.render(graphData)
    },

    /**
     * 清空画布
     */
    clear() {
      this.diagram?.render({ nodes: [], edges: [] })
    },

    /**
     * 自动布局
     */
    autoLayout() {
      // LogicFlow 内置 DAG 布局
      if (this.diagram?.layout) {
        this.diagram.layout({ type: 'dagre', rankdir: 'LR' })
      }
    }
  },
  template: `<div ref="canvas" style="width:100%;height:100%;min-height:500px"></div>`
}
