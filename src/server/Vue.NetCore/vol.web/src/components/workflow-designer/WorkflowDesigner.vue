<template>
  <div ref="canvas" class="workflow-designer-canvas"></div>
</template>

<script setup>
import { Diagram } from '@logicflow/core'
import '@logicflow/core/dist/index.css'
import { HtmlPlugin } from '@logicflow/extension'
import '@logicflow/extension/dist/index.css'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { compileToWorkflowConfig, decompileToGraphData } from './compiler'

const props = defineProps({
  // 初始 workflow_config JSON 对象
  initialConfig: {
    type: Object,
    default: () => ({
      nodes: [],
      edges: [],
      branches: [],
      version: 1,
      workflowType: 'validation',
      outputConfig: {}
    })
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
})

const emit = defineEmits(['node-selected', 'config-change', 'save'])

const canvas = ref(null)
const diagram = ref(null)

const initDiagram = () => {
  diagram.value = new Diagram({
    container: canvas.value,
    grid: {
      size: 20,
      visible: true,
      type: 'dot',
      config: {
        color: '#cbd5e1',
        thickness: 2
      }
    },
    background: {
      color: '#f8fafc'
    },
    plugins: [HtmlPlugin],
    behavior: {
      scroll: true,
      zoom: true,
      drag: true
    },
    style: {
      rect: {
        radius: 16,
        strokeWidth: 2,
        stroke: '#cbd5e1',
        fill: '#ffffff'
      },
      circle: {
        strokeWidth: 2,
        stroke: '#cbd5e1',
        fill: '#ffffff'
      },
      polyline: {
        strokeWidth: 3,
        stroke: '#94a3b8',
        outlineColor: '#f8fafc',
        hoverStroke: 'var(--yzh-color-primary)',
        selectedStroke: 'var(--yzh-color-primary)'
      },
      edgeText: {
        background: {
          fill: '#fff'
        },
        fontSize: 14,
        fontWeight: 600
      },
      nodeText: {
        fontSize: 15,
        fontWeight: 600,
        color: '#0f172a'
      }
    }
  })

  // 监听画布尺寸变化，确保点阵网格正确渲染
  const resizeObserver = new ResizeObserver(() => {
    if (diagram.value) {
      diagram.value.resize()
    }
  })
  if (canvas.value) {
    resizeObserver.observe(canvas.value)
  }

  // 加载初始数据
  if (props.initialConfig?.nodes?.length > 0) {
    const { graphData } = decompileToGraphData(props.initialConfig)
    diagram.value.render(graphData)
  }

  // 节点选中事件
  diagram.value.on('node:click', ({ data }) => {
    emit('node-selected', data.id, data)
  })

  diagram.value.on('edge:click', () => {
    emit('node-selected', null)
  })

  diagram.value.on('blank:click', () => {
    emit('node-selected', null)
  })
}

onMounted(() => {
  initDiagram()
})

onBeforeUnmount(() => {
  if (diagram.value) {
    diagram.value.clearData?.()
    diagram.value = null
  }
})

// 暴露方法给父组件
defineExpose({
  exportConfig: () => {
    const graphData = diagram.value?.getGraphData()
    if (!graphData) return null
    return compileToWorkflowConfig(graphData)
  },
  importConfig: (config) => {
    const { graphData } = decompileToGraphData(config)
    diagram.value?.render(graphData)
  },
  clear: () => {
    diagram.value?.render({ nodes: [], edges: [] })
  },
  autoLayout: () => {
    if (diagram.value?.layout) {
      diagram.value.layout({ type: 'dagre', rankdir: 'LR' })
    }
  }
})
</script>

<style scoped lang="less">
.workflow-designer-canvas {
  width: 100%;
  height: 100%;
  min-height: 500px;
  background-color: #f8fafc;
}
</style>
