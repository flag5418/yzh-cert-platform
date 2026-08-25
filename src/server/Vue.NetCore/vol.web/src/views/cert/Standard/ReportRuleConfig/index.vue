<template>
  <div class="report-rule-page">
    <CertPageHeader title="报告规则配置" :icon="IconSetting" />

    <div class="page-body">
      <!-- ===== 左栏：统一4级树 ===== -->
      <div class="left-panel">
        <el-card shadow="never" class="tree-card">
          <template #header>
            <div class="panel-header">
              <span>机构 / 标准 / 阶段 / 报告章节</span>
              <el-button link size="small" @click="refreshTree"><el-icon><IconRefresh /></el-icon></el-button>
            </div>
          </template>
          <div class="tree-search">
            <el-input
              v-model="searchText"
              placeholder="搜索..."
              size="small"
              clearable
              :prefix-icon="IconSearch"
            />
          </div>
          <div class="tree-body">
            <template v-for="org in treeData" :key="org.id">
            <div v-if="org.visible" class="tree-group">
              <!-- 机构 -->
              <div class="tree-node level-0" @click="toggleExpand(org)">
                <el-icon class="tree-toggle" :class="{ expanded: org.expanded }"><IconForward /></el-icon>
                <el-icon class="tree-icon org"><IconOfficeBuilding /></el-icon>
                <span class="tree-label">{{ org.label }}</span>
                <el-badge v-if="org.children?.length" :value="org.children.length" type="info" />
              </div>

              <!-- 标准 -->
              <template v-if="org.expanded && org.children">
                <template v-for="std in org.children" :key="std.id">
                  <div v-if="std.visible" class="tree-node level-1" @click="toggleExpand(std)">
                    <el-icon class="tree-toggle" :class="{ expanded: std.expanded }"><IconForward /></el-icon>
                    <el-icon class="tree-icon standard"><IconFile /></el-icon>
                    <span class="tree-label">{{ std.label }}</span>
                    <el-badge v-if="std.children?.length" :value="std.children.length" type="info" />
                  </div>

                  <!-- 阶段 -->
                  <template v-if="std.expanded && std.children && std.visible">
                    <template v-for="phase in std.children" :key="phase.id">
                      <div v-if="phase.visible" class="tree-node level-2" @click="togglePhase(phase, std, org)">
                        <el-icon class="tree-toggle" :class="{ expanded: phase.expanded }"><IconForward /></el-icon>
                        <el-icon class="tree-icon phase"><IconCalendar /></el-icon>
                        <span class="tree-label">{{ phase.label }}</span>
                        <el-badge v-if="phase.children?.length" :value="phase.children.length" type="info" />
                      </div>

                      <!-- 报告章节 -->
                      <div v-if="phase.visible && phase.expanded && phase.sectionLoaded">
                        <div
                          v-for="section in phase.children"
                          :key="section.id"
                          class="tree-node level-3"
                          :class="{ active: currentSection?.id === section.id, configured: !!section.workflowConfig }"
                          @click="selectSection(section, phase)"
                        >
                          <el-icon class="tree-toggle" style="visibility: hidden"><IconForward /></el-icon>
                          <el-icon class="tree-icon rule" :class="{ configured: !!section.workflowConfig }">
                            <IconCircleCheck v-if="section.workflowConfig" />
                            <IconDocument v-else />
                          </el-icon>
                          <span class="tree-label">{{ section.sectionName }}</span>
                          <span v-if="section.workflowConfig" class="config-dot"></span>
                          <el-tag v-if="section.sortOrder !== undefined" size="small" type="info" class="node-badge">{{ section.sortOrder }}</el-tag>
                        </div>
                        <div v-if="!phase.children.length" class="rule-empty">该阶段暂无报告章节</div>
                      </div>
                      <div v-if="phase.visible && phase.expanded && phase.sectionLoading" class="rule-loading">
                        <el-icon class="is-loading"><IconLoading /></el-icon>
                        <span>加载中...</span>
                      </div>
                    </template>
                  </template>
                </template>
              </template>
            </div>
            </template>
            <div v-if="!treeData.length" class="tree-empty">
              <el-empty description="暂无数据" :image-size="60" />
            </div>
          </div>
        </el-card>
      </div>

      <!-- ===== 中栏：LogicFlow 画布 ===== -->
      <div class="canvas-panel">
        <div class="canvas-toolbar">
          <span class="canvas-title">{{ currentSection ? `工作流：${currentSection.sectionName}` : '请选择报告章节' }}</span>
          <div class="toolbar-actions">
            <el-button size="small" @click="autoLayout"><el-icon><IconGrid /></el-icon> 自动布局</el-button>
            <el-button size="small" type="danger" plain @click="handleClearCanvas"><el-icon><IconDelete /></el-icon> 清空画布</el-button>
            <el-button size="small" @click="validateGraph"><el-icon><IconCircleCheck /></el-icon> 校验</el-button>
            <el-button type="primary" size="small" :disabled="!currentSection || !store.state.dirty" @click="handleSave">
              <el-icon><IconDownload /></el-icon> 保存工作流
            </el-button>
          </div>
        </div>
        <div
          ref="canvasRef"
          class="canvas-container"
          @dragover.prevent="onCanvasDragOver"
          @drop.prevent="onCanvasDrop"
        ></div>
        <div class="canvas-status">
          <span>节点: {{ store.state.nodes.length }} | 边: {{ store.state.edges.length }} | 脏标记: {{ store.state.dirty ? '是' : '否' }}</span>
          <span v-if="currentSection" class="rule-code-text">{{ currentSection.code }}</span>
          <span v-if="savedTip" class="saved-text">✓ {{ savedTip }}</span>
        </div>
      </div>

      <!-- ===== 右栏：节点库 + 属性面板 ===== -->
      <div class="right-panel">
        <div class="skill-panel-wrapper">
          <SkillPanel :skills="skills" :categories="categories" @add-node="handleAddNode" />
        </div>
        <div class="prop-panel-wrapper">
          <NodePropertyForm
            :key="`panel_${forceRefreshTick}`"
            :selected-node="selectedNode"
            :skills="skills"
            :doc-rules="docRules"
            :doc-fields="currentDocFields"
            :doc-tables="currentDocTables"
            :canvas-nodes="canvasNodesForPanel"
            @update-node="handleUpdateNode"
            @delete-node="handleDeleteNode"
            @load-doc-fields="onNodeDocChange"
            @link-node="handleLinkNode"
            @test-node="handleTestNode"
            @test-workflow="handleTestWorkflow"
            @test-doc-extract="handleTestDocExtract"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, watch, onMounted, onActivated, onBeforeUnmount, getCurrentInstance, nextTick } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import LogicFlow from '@logicflow/core'
import '@logicflow/core/dist/index.css'
import { CertPageHeader } from '@/certcore'
import {
  IconSetting, IconRefresh, IconGrid, IconCircleCheck, IconDownload,
  IconForward, IconSearch, IconFile, IconCalendar, IconOfficeBuilding,
  IconDocument, IconLoading, IconDelete
} from '@/yzh/icons'
import SkillPanel from '@/components/workflow-designer/SkillPanel.vue'
import NodePropertyForm from '@/components/workflow-designer/NodePropertyForm.vue'
import { nodeStyle } from '@/components/workflow-designer/compiler'
import { useWorkflowStore } from '@/components/workflow-designer/store/useWorkflowStore.js'
import { deserialize, serialize, extractLayout } from '@/components/workflow-designer/model/serializer.js'
import { analyzeWorkflowTopology, setLogLevel, setLogEnabled, summarizePaths, formatWorkflowPath } from '@/components/workflow-designer/compiler'

// 开启详细日志（生产环境可关闭）
setLogEnabled(true)
setLogLevel('INFO')


const { proxy } = getCurrentInstance()
const canvasRef = ref(null)
const diagram = ref(null)
let _resizeObserver = null

// ===== 操作层 store（唯一变更入口） =====
const store = useWorkflowStore()

// 左栏 - 统一4级树
const treeData = ref([])
const searchText = ref('')
const currentSection = ref(null)
const currentFilter = reactive({ orgCode: '', standardCode: '', phaseCode: '' })

// 右栏
const skills = ref([])
const categories = ref([])
const selectedNode = ref(null)

// 强制刷新计数器（点确定后自增，NodePropertyForm watch 它来强制重建内部状态）
const forceRefreshTick = ref(0)

// 文档字段/表格
const docRules = ref([])
const currentDocFields = ref([])
const currentDocTables = ref([])

// 保存提示
const savedTip = ref('')

// 执行验证（报告规则暂不启用）
// const executing = ref(false)
// const executionResult = ref(null)

// 选中的边（供键盘删除）
const selectedEdgeId = ref(null)

// ==================== 初始化 ====================

onMounted(async () => {
  await Promise.all([loadSkills(), loadCategories(), loadTree(), loadDocRules()])
  // 等待 DOM 完全渲染后再初始化 LogicFlow（确保容器有尺寸，避免 resize 错误）
  await nextTick()
  initDiagram()
})

onActivated(() => {
  loadSkills()
})

onBeforeUnmount(() => {
  document.removeEventListener('keydown', handleKeyDown)
  // 断开 ResizeObserver
  if (_resizeObserver) {
    _resizeObserver.disconnect()
    _resizeObserver = null
  }
  if (diagram.value) {
    diagram.value.clearData?.()
    diagram.value = null
  }
})

async function loadSkills() {
  try {
    const res = await proxy.http.get('api/skill/query-nodes', null, false)
    if (res?.status) skills.value = res.data || []
  } catch (e) { console.error('加载 Skill 失败', e) }
}

async function loadCategories() {
  try {
    const res = await proxy.http.get('api/skill-category/list', null, false)
    if (res?.status) categories.value = res.data || []
  } catch (e) { console.error('加载分类失败', e) }
}

async function loadDocRules() {
  try {
    const res = await proxy.http.get('api/DocExtractionRule/configured-rules', null, false)
    if (res?.status) docRules.value = res.data || []
  } catch (e) { console.error('加载文档规则失败', e) }
}

async function onNodeDocChange(ruleCode) {
  if (!ruleCode) return
  await loadFieldsAndTables(ruleCode)
}

async function loadFieldsAndTables(ruleCode) {
  try {
    const res = await proxy.http.get(`api/DocExtractionRule/${ruleCode}/fields-tables`, null, false)
    if (res?.status && res.data) {
      currentDocFields.value = res.data.fields || []
      currentDocTables.value = res.data.tables || []
    }
  } catch (e) { console.error('加载文档字段/表格失败', e) }
}

// ==================== 树数据 ====================

async function loadTree() {
  try {
    const res = await proxy.http.get('/api/standard-directory/organization-tree', null, false)
    const raw = res?.Data || res?.data || []
    treeData.value = raw.map(org => ({
      ...org,
      expanded: true,
      visible: true,
      children: (org.children || []).map(std => ({
        ...std,
        expanded: false,
        visible: true,
        children: (std.children || []).map(phase => ({
          ...phase,
          expanded: false,
          visible: true,
          children: [],
          sectionLoading: false,
          sectionLoaded: false
        }))
      }))
    }))
    applySearchFilter()
  } catch (e) {
    console.error('[ReportRuleConfig] 加载树失败:', e)
  }
}

function applySearchFilter() {
  const kw = searchText.value?.toLowerCase() || ''
  if (!kw) {
    treeData.value.forEach(org => {
      org.visible = true
      ;(org.children || []).forEach(std => {
        std.visible = true
        ;(std.children || []).forEach(p => { p.visible = true })
      })
    })
    return
  }
  treeData.value.forEach(org => {
    let orgHasMatch = org.label?.toLowerCase().includes(kw)
    ;(org.children || []).forEach(std => {
      let stdHasMatch = std.label?.toLowerCase().includes(kw)
      ;(std.children || []).forEach(p => {
        p.visible = p.label?.toLowerCase().includes(kw) || stdHasMatch
        if (p.visible) stdHasMatch = true
      })
      std.visible = stdHasMatch || orgHasMatch
      if (std.visible) orgHasMatch = true
    })
    org.visible = orgHasMatch
  })
}

watch(searchText, () => applySearchFilter())

function toggleExpand(node) {
  node.expanded = !node.expanded
}

async function togglePhase(phase, std, org) {
  phase.expanded = !phase.expanded
  Object.assign(currentFilter, {
    orgCode: org.cbCode || org.id,
    standardCode: std.stdCode || phase.stdCode || std.standardCode,
    phaseCode: phase.phaseCode
  })
  currentSection.value = null
  selectedNode.value = null
  clearCanvas()
  if (phase.expanded && !phase.sectionLoaded) {
    await loadSectionsForPhase(phase)
  }
}

async function loadSectionsForPhase(phase) {
  phase.sectionLoading = true
  try {
    const params = `orgCode=${encodeURIComponent(currentFilter.orgCode)}&standardCode=${encodeURIComponent(currentFilter.standardCode)}&phaseCode=${encodeURIComponent(currentFilter.phaseCode)}`
    const res = await proxy.http.get(`api/report-definition/section/by-context?${params}`, null, false)
    if (res?.status) {
      phase.children = (res.data || []).map(s => ({ ...s, id: s.id || s.code }))
    } else {
      phase.children = []
    }
    phase.sectionLoaded = true
  } catch (e) {
    console.error('[ReportRuleConfig] 加载报告章节失败:', e)
    phase.children = []
  } finally {
    phase.sectionLoading = false
  }
}

const refreshTree = () => { loadTree() }

// ==================== 画布 ====================

function initDiagram() {
  diagram.value = new LogicFlow({
    container: canvasRef.value,
    grid: { size: 20, visible: true, type: 'mesh' },
    background: '#fafbfc',
    edgeType: 'polyline',
    allowResize: true,
    allowRotate: false,
    isSilentMode: false,
    stopScrollGraph: false,
    stopZoomGraph: false,
    stopMoveGraph: false,
    snapline: true,
    keyboard: { enabled: true },
    // 禁用 LogicFlow 内置的节点文本编辑功能（双击节点变成输入框的那个）
    // 我们用自己的 promptEditNodeName 弹窗来处理改名
    textEditMode: false
  })



  diagram.value.on('node:click', ({ data }) => {
    const props = data.properties || {}
    // 始终从 store 读取 title（确保改名后显示最新值）
    const storeNode = store.getNodeById(data.id)

    // 根据画布连线实时计算 inputs（解决连线后未选中节点 inputs 不更新问题）
    const { inputs: syncedInputs, inputTypes: syncedInputTypes } = computeNodeInputsFromEdges(
      data.id,
      storeNode?.inputs || props.inputs || {},
      storeNode?.inputTypes || props.inputTypes || {}
    )

    // 如果计算结果与 store 不同，更新 store
    if (JSON.stringify(syncedInputs) !== JSON.stringify(storeNode?.inputs || {}) ||
        JSON.stringify(syncedInputTypes) !== JSON.stringify(storeNode?.inputTypes || {})) {
      store.setInputValue(data.id, null, null)
      if (storeNode) {
        storeNode.inputs = syncedInputs
        storeNode.inputTypes = syncedInputTypes
      }
    }

    const nodeData = {
      nodeId: data.id,
      nodeType: props.nodeType || 'skill',
      classCode: props.classCode || props.nodeType || 'skill',
      title: storeNode?.title || props.title || data.text || '',
      skillCode: props.skillCode || '',
      config: storeNode?.config || props.config || {},
      inputs: syncedInputs,
      inputTypes: syncedInputTypes,
      outputs: storeNode?.outputs || props.outputs || {},
      inputPorts: storeNode?.inputPorts || props.inputPorts || [],
      outputPorts: storeNode?.outputPorts || props.outputPorts || []
    }
    // branch 节点：传递已有出边信息
    if (nodeData.nodeType === 'branch') {
      const gd = diagram.value?.getGraphData()
      const outEdges = (gd.edges || []).filter(e => e.sourceNodeId === data.id)
      nodeData.branchEdges = outEdges.map(e => ({
        handle: e.properties?.sourceHandle || '',
        targetId: e.targetNodeId,
        edgeId: e.id
      }))
    }
    selectedNode.value = nodeData
  })

  diagram.value.on('node:dbclick', ({ data }) => {
    const nodeType = data.properties?.nodeType || data.properties?.classCode
    if (nodeType === 'start') return
    promptEditNodeName(data.id, data.properties?.title || data.text || '')
  })

  diagram.value.on('edge:click', ({ data }) => {
    // 改名过程中不清空 selectedNode（防止 ElMessageBox 关闭时误触发）
    if (_renamingNodeId) return
    selectedNode.value = null
    // 记录当前选中的边（供键盘删除）
    selectedEdgeId.value = data.id
  })
  diagram.value.on('blank:click', () => {
    if (_renamingNodeId && (Date.now() - _renamingTimestamp) < RENAMING_GUARD_MS) return
    selectedNode.value = null
    selectedEdgeId.value = null
  })

  // 连线即数据绑定：edge:add / edge:delete 事件同步 inputs
  diagram.value.on('edge:add', ({ data }) => {
    // branch 节点自动设置 sourceHandle（success/failure）
    autoSetBranchHandle(data)
    onEdgeChange()
  })
  diagram.value.on('edge:delete', ({ data }) => {
    onEdgeChange()
  })

  // 键盘事件：Delete 删除选中的边或节点
  document.addEventListener('keydown', handleKeyDown)

  // ResizeObserver：监听容器尺寸变化时通知 LogicFlow resize
  // 解决初始化时容器尺寸为 0 导致的「无法获取画布宽高」错误
  _resizeObserver = new ResizeObserver(() => {
    if (diagram.value && canvasRef.value) {
      const { clientWidth, clientHeight } = canvasRef.value
      if (clientWidth > 0 && clientHeight > 0) {
        diagram.value.resize(clientWidth, clientHeight)
      }
    }
  })
  _resizeObserver.observe(canvasRef.value)
}

function handleKeyDown(e) {
  // 不在输入框中时才响应
  if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return
  if (e.key !== 'Delete' && e.key !== 'Backspace') return
  e.preventDefault()

  // 删除选中的边
  if (selectedEdgeId.value && diagram.value) {
    diagram.value.deleteEdge(selectedEdgeId.value)
    selectedEdgeId.value = null
    return
  }
  // 删除选中的节点
  if (selectedNode.value?.nodeId && diagram.value) {
    handleDeleteNode(selectedNode.value.nodeId)
  }
}

/**
 * 根据画布连线计算指定节点的 inputs/inputTypes
 * @param {string} nodeId - 目标节点 ID
 * @param {Object} [currentInputs] - 当前 inputs（用于保留常量值）
 * @param {Object} [currentInputTypes] - 当前 inputTypes
 * @returns {{ inputs: Object, inputTypes: Object }} 计算后的 inputs 和 inputTypes
 */
function computeNodeInputsFromEdges(nodeId, currentInputs = {}, currentInputTypes = {}) {
  if (!diagram.value) return { inputs: currentInputs, inputTypes: currentInputTypes }
  const gd = diagram.value.getGraphData()
  const inEdges = (gd.edges || []).filter(e => e.targetNodeId === nodeId)
  const nodeProps = gd.nodes.find(n => n.id === nodeId)?.properties || {}
  const inputPorts = nodeProps.inputPorts || []
  const newInputs = {}
  const newTypes = {}

  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const edge = inEdges.find(e => {
      const handle = e.properties?.targetHandle
      return !handle || handle === port.name
    })
    if (edge) {
      // 如果当前值是常量类型，不覆盖（保留用户手动输入的常量）
      if (currentInputTypes[port.name] === 'constant' && currentInputs[port.name]) {
        continue
      }
      newInputs[port.name] = edge.sourceNodeId
      newTypes[port.name] = 'link'
    }
  }

  const merged = { ...currentInputs, ...newInputs }
  const mergedTypes = { ...currentInputTypes, ...newTypes }
  // 删除已断开的节点引用
  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const val = merged[port.name]
    if (val && gd.nodes.some(n => n.id === val)) {
      const stillConnected = inEdges.some(e => e.sourceNodeId === val)
      if (!stillConnected) {
        delete merged[port.name]
        delete mergedTypes[port.name]
      }
    }
  }

  return { inputs: merged, inputTypes: mergedTypes }
}

/** 边变化后同步 selectedNode 的 inputs */
function syncSelectedNodeInputs() {
  if (!selectedNode.value || !diagram.value) return
  const nodeId = selectedNode.value.nodeId
  const { inputs: merged, inputTypes: mergedTypes } = computeNodeInputsFromEdges(
    nodeId,
    selectedNode.value.inputs || {},
    selectedNode.value.inputTypes || {}
  )

  if (JSON.stringify(merged) !== JSON.stringify(selectedNode.value.inputs) ||
      JSON.stringify(mergedTypes) !== JSON.stringify(selectedNode.value.inputTypes || {})) {
    diagram.value.setProperties(nodeId, { inputs: merged, inputTypes: mergedTypes })
    selectedNode.value = { ...selectedNode.value, inputs: merged, inputTypes: mergedTypes }
    // 同步 store
    store.setInputValue(nodeId, null, null) // 标记脏
    const storeNode = store.getNodeById(nodeId)
    if (storeNode) {
      storeNode.inputs = merged
      storeNode.inputTypes = mergedTypes
    }
  }
}

/** branch 节点自动设置 sourceHandle（success/failure） */
function autoSetBranchHandle(edgeData) {
  if (!edgeData?.sourceNodeId || !edgeData?.targetNodeId) return
  const gd = diagram.value?.getGraphData()
  if (!gd) return
  const sourceNode = gd.nodes.find(n => n.id === edgeData.sourceNodeId)
  if (!sourceNode) return
  const sourceType = sourceNode.properties?.nodeType || sourceNode.properties?.classCode
  if (sourceType !== 'branch') return

  // 已有 sourceHandle 则跳过
  const existingProps = edgeData.properties || {}
  if (existingProps.sourceHandle) return

  // 统计 branch 节点已有的出边
  const existingEdges = (gd.edges || []).filter(e =>
    e.sourceNodeId === edgeData.sourceNodeId && e.id !== edgeData.id
  )
  const hasSuccess = existingEdges.some(e => e.properties?.sourceHandle === 'success')

  // 自动分配：第一个出边 = success，第二个 = failure
  const handle = hasSuccess ? 'failure' : 'success'
  const label = handle === 'success' ? '成功' : '失败'
  const color = handle === 'success' ? '#67C23A' : '#F56C6C'

  // 更新边属性
  diagram.value.setProperties(edgeData.id, { sourceHandle: handle })
  diagram.value.updateText(edgeData.id, label)

  // 更新边样式
  const edge = gd.edges.find(e => e.id === edgeData.id)
  if (edge) {
    edge.properties = { ...edge.properties, sourceHandle: handle }
    edge.text = label
    edge.style = { stroke: color, strokeWidth: 2 }
  }
}

/** 边变化回调：同步 inputs + store */
function onEdgeChange() {
  syncSelectedNodeInputs()
  // 同步 store edges
  const gd = diagram.value?.getGraphData()
  if (gd) {
    store.state.edges = (gd.edges || []).map(e => ({
      id: e.id,
      source: e.sourceNodeId,
      target: e.targetNodeId,
      sourceHandle: e.properties?.sourceHandle || null,
      targetHandle: e.properties?.targetHandle || null
    }))
  }
}

/** 画布上所有节点（供面板 Link 模式下拉选择） */
const canvasNodesForPanel = computed(() => {
  return store.state.nodes.map(n => {
    const _title = n.title
    return {
      id: n.id,
      title: _title || n.id,
      text: _title || n.id,
      nodeType: n.nodeType
    }
  })
})

// ==================== 拖拽到画布 ====================

function onCanvasDragOver(event) {
  event.dataTransfer.dropEffect = 'copy'
}

function onCanvasDrop(event) {
  try {
    const raw = event.dataTransfer.getData('nodeData')
    if (!raw) return
    const item = JSON.parse(raw)
    const lfPoint = diagram.value.getPointByClient(event.clientX, event.clientY)
    const pos = lfPoint?.canvasOverlayPosition || lfPoint
    const x = pos?.x ?? 200
    const y = pos?.y ?? 150

    // 通过 store 添加节点（自动生成 classCode_n{序号} ID）
    const node = store.addNode(item, x, y)
    if (!node) return

    // 同步到 LogicFlow 画布
    const category = item.category || skills.value.find(s => s.skillCode === item.skillCode)?.category || ''
    const props = {
      classCode: node.classCode,
      nodeType: node.nodeType,
      title: node.title,
      skillCode: node.skillCode,
      config: node.config,
      inputs: node.inputs,
      inputTypes: node.inputTypes,
      outputs: node.outputs,
      inputPorts: node.inputPorts,
      outputPorts: node.outputPorts
    }
    // branch 节点使用三角形
    if (node.nodeType === 'branch') {
      props.points = [[0, -30], [50, 0], [0, 30]]
    }
    diagram.value.addNode({
      id: node.id,
      type: lfShapeType(node.nodeType),
      x, y,
      text: node.title,
      style: nodeStyle(node.nodeType, node.skillCode, category),
      properties: props
    })
  } catch (e) {
    console.error('[ReportRuleConfig] 拖拽添加节点失败:', e)
  }
}

/** LogicFlow 图形类型映射 */
function lfShapeType(nodeType) {
  if (nodeType === 'start' || nodeType === 'end') return 'circle'
  if (nodeType === 'branch') return 'polygon'
  return 'rect'
}

function handleAddNode(item) {
  // 通过 store 添加节点
  const maxX = store.state.nodes.reduce((m, n) => Math.max(m, n.x || 0), 100)
  const maxY = store.state.nodes.reduce((m, n) => Math.max(m, n.y || 0), 80)
  const node = store.addNode(item, 120 + (maxX % 600), 80 + (maxY % 400))
  if (!node) return

  const category = item.category || skills.value.find(s => s.skillCode === item.skillCode)?.category || ''
  const addProps = {
    classCode: node.classCode,
    nodeType: node.nodeType,
    title: node.title,
    skillCode: node.skillCode,
    config: node.config,
    inputs: node.inputs,
    inputTypes: node.inputTypes,
    outputs: node.outputs,
    inputPorts: node.inputPorts,
    outputPorts: node.outputPorts
  }
  if (node.nodeType === 'branch') addProps.points = [[0, -30], [50, 0], [0, 30]]
  diagram.value.addNode({
    id: node.id,
    type: lfShapeType(node.nodeType),
    x: node.x, y: node.y,
    text: node.title,
    style: nodeStyle(node.nodeType, node.skillCode, category),
    properties: addProps
  })
}

function handleUpdateNode(data) {
  if (!data.nodeId) return

  // 始终从 store 读取最新 title（防止旧值覆盖）
  const storeNode = store.getNodeById(data.nodeId)
  const latestTitle = storeNode?.title || data.title

  // 通过 store 更新（含名称唯一性校验）
  const success = store.updateNode(data.nodeId, {
    title: latestTitle,
    classCode: data.classCode || data.nodeType,
    nodeType: data.nodeType,
    skillCode: data.skillCode,
    config: data.config,
    inputs: data.inputs,
    inputTypes: data.inputTypes,
    inputPorts: data.inputPorts,
    outputPorts: data.outputPorts
  })
  if (!success) {
    ElMessage.warning(`节点名称「${latestTitle}」已存在，请使用其他名称`)
    return
  }

  // 同步到 LogicFlow 画布
  diagram.value.updateText(data.nodeId, latestTitle || data.skillCode || '')
  diagram.value.setProperties(data.nodeId, {
    classCode: data.classCode || data.nodeType,
    nodeType: data.nodeType,
    title: latestTitle,
    skillCode: data.skillCode,
    config: data.config,
    inputs: data.inputs,
    inputTypes: data.inputTypes,
    inputPorts: data.inputPorts,
    outputPorts: data.outputPorts
  })

  // 延迟重建 selectedNode（等 NodePropertyForm 的 applyChanges 完成后）
  nextTick(() => {
    const sn = store.getNodeById(data.nodeId)
    if (sn) {
      // branch 节点：传递已有出边信息
      let branchEdges = undefined
      if (sn.nodeType === 'branch' || sn.classCode === 'branch') {
        const gd = diagram.value?.getGraphData()
        const outEdges = (gd?.edges || []).filter(e => e.sourceNodeId === data.nodeId)
        branchEdges = outEdges.map(e => ({
          handle: e.properties?.sourceHandle || '',
          targetId: e.targetNodeId,
          edgeId: e.id
        }))
      }
      selectedNode.value = {
        nodeId: sn.id,
        nodeType: sn.nodeType,
        classCode: sn.classCode,
        title: sn.title,
        skillCode: sn.skillCode,
        config: { ...sn.config },
        inputs: { ...sn.inputs },
        inputTypes: { ...(sn.inputTypes || {}) },
        outputs: { ...sn.outputs },
        inputPorts: sn.inputPorts || [],
        outputPorts: sn.outputPorts || [],
        branchEdges
      }
    }
  })
}

/** 正在改名的节点 ID 和时间戳（防止 blank:click 误清 selectedNode） */
let _renamingNodeId = null
let _renamingTimestamp = 0
const RENAMING_GUARD_MS = 1000 // 改名保护窗口：1秒内不允许 blank:click 清空

/** 双击节点 → 编辑名称 */
function promptEditNodeName(nodeId, currentName) {
  _renamingNodeId = nodeId
  _renamingTimestamp = Date.now()

  ElMessageBox.prompt('请输入节点名称', '编辑节点名称', {
    inputValue: currentName,
    inputPattern: /.+/,
    inputErrorMessage: '名称不能为空',
    confirmButtonText: '确定',
    cancelButtonText: '取消'
  }).then(({ value }) => {
    const name = (value || '').trim()
    if (!name) {
      _renamingNodeId = null
      _renamingTimestamp = 0
      return
    }

    if (!store.renameNode(nodeId, name)) {
      ElMessage.warning(`节点名称「${name}」已存在，请使用其他名称`)
      _renamingNodeId = null
      _renamingTimestamp = 0
      return
    }

    // 同步更新画布
    diagram.value.updateText(nodeId, name)
    diagram.value.setProperties(nodeId, { title: name })

    setTimeout(() => {
      const storeNode = store.getNodeById(nodeId)
      if (storeNode) {
        let branchEdges = undefined
        if (storeNode.nodeType === 'branch' || storeNode.classCode === 'branch') {
          const gd = diagram.value?.getGraphData()
          const outEdges = (gd?.edges || []).filter(e => e.sourceNodeId === nodeId)
          branchEdges = outEdges.map(e => ({
            handle: e.properties?.sourceHandle || '',
            targetId: e.targetNodeId,
            edgeId: e.id
          }))
        }
        selectedNode.value = {
          nodeId: storeNode.id,
          nodeType: storeNode.nodeType,
          classCode: storeNode.classCode,
          title: storeNode.title,
          skillCode: storeNode.skillCode,
          config: { ...storeNode.config },
          inputs: { ...storeNode.inputs },
          inputTypes: { ...(storeNode.inputTypes || {}) },
          outputs: { ...storeNode.outputs },
          inputPorts: storeNode.inputPorts || [],
          outputPorts: storeNode.outputPorts || [],
          branchEdges
        }
        forceRefreshTick.value++
      }
      _renamingNodeId = null
      _renamingTimestamp = 0
    }, 300)
    ElMessage.success('节点名称已更新')
  }).catch(() => {
    _renamingNodeId = null
    _renamingTimestamp = 0
  })
}

function handleDeleteNode(nodeId) {
  if (!store.removeNode(nodeId)) {
    ElMessage.warning('开始节点不可删除')
    return
  }
  diagram.value.deleteNode(nodeId)
  selectedNode.value = null
}

// ==================== 面板 → 画布连线操作 ====================

function handleLinkNode({ portName, sourceNodeId, targetNodeId, sourceHandle }) {
  if (!diagram.value) return
  const gd = diagram.value.getGraphData()

  // branch 输出选择模式：portName 是 success/failure，sourceNodeId 是当前节点，targetNodeId 是目标
  if (sourceHandle && portName === sourceHandle) {
    // 先删除该 handle 已有的所有边
    const toDelete = (gd.edges || []).filter(e =>
      e.sourceNodeId === sourceNodeId && e.properties?.sourceHandle === sourceHandle
    )
    for (const existing of toDelete) {
      diagram.value.deleteEdge(existing.id)
    }
    // 如果当前选中的边就是被删的，清空选中
    if (selectedEdgeId.value && toDelete.some(e => e.id === selectedEdgeId.value)) {
      selectedEdgeId.value = null
    }
    // 创建新边
    if (targetNodeId) {
      const color = sourceHandle === 'success' ? '#67C23A' : '#F56C6C'
      const label = sourceHandle === 'success' ? '成功' : '失败'
      diagram.value.addEdge({
        id: `e-${sourceNodeId}-${targetNodeId}-${Date.now()}`,
        type: 'polyline',
        sourceNodeId,
        targetNodeId,
        text: label,
        style: { stroke: color, strokeWidth: 2 },
        properties: { sourceHandle, targetHandle: null }
      })
    }
    return
  }

  if (!targetNodeId) return

  // sourceNodeId 为 null → 断开连线模式（从面板切换类型时触发）
  if (!sourceNodeId) {
    // 删除 target 端口上已有的入边
    const toDelete = (gd.edges || []).filter(e =>
      e.targetNodeId === targetNodeId &&
      (e.properties?.targetHandle === portName || (!e.properties?.targetHandle && !portName))
    )
    for (const existing of toDelete) {
      diagram.value.deleteEdge(existing.id)
    }
    // 同步 store
    const storeNode = store.getNodeById(targetNodeId)
    if (storeNode && storeNode.inputs) {
      delete storeNode.inputs[portName]
    }
    if (storeNode && storeNode.inputTypes) {
      delete storeNode.inputTypes[portName]
    }
    onEdgeChange()
    return
  }

  // 普通连线模式
  const edge = store.connect(sourceNodeId, targetNodeId, null, portName)
  if (!edge) return

  // 同步到 LogicFlow 画布
  const existingEdges = (gd.edges || []).filter(e =>
    e.targetNodeId === targetNodeId &&
    (e.properties?.targetHandle === portName || (!e.properties?.targetHandle && !portName))
  )
  for (const existing of existingEdges) {
    diagram.value.deleteEdge(existing.id)
  }

  const sourceNode = gd.nodes.find(n => n.id === sourceNodeId)
  const sourceType = sourceNode?.properties?.nodeType || sourceNode?.properties?.classCode
  let autoHandle = null
  if (sourceType === 'branch') {
    const branchEdges = (gd.edges || []).filter(e => e.sourceNodeId === sourceNodeId)
    const hasSuccess = branchEdges.some(e => e.properties?.sourceHandle === 'success')
    autoHandle = hasSuccess ? 'failure' : 'success'
  }

  diagram.value.addEdge({
    id: edge.id,
    type: 'polyline',
    sourceNodeId,
    targetNodeId,
    properties: {
      sourceHandle: autoHandle,
      targetHandle: portName || null
    }
  })
}

// ==================== 节点测试 ====================

async function handleTestNode(nodeData) {
  try {
    // AI 节点走专用测试接口
    if (nodeData.nodeType === 'ai_node') {
      await handleTestAiNode(nodeData)
      return
    }

    const res = await proxy.http.post('api/workflow/test/node', {
      nodeId: nodeData.nodeId,
      nodeType: nodeData.nodeType,
      title: nodeData.title,
      skillCode: nodeData.skillCode,
      config: nodeData.config,
      inputs: nodeData.inputs,
      inputTypes: nodeData.inputTypes,
      inputPorts: nodeData.inputPorts,
      outputPorts: nodeData.outputPorts
    }, false)

    if (res?.success && res.data) {
      nodeData.onSuccess(res.data)
    } else {
      nodeData.onError(res?.error || '测试失败', null)
    }
  } catch (e) {
    console.error('[ReportRuleConfig] 节点测试失败:', e)
    nodeData.onError('请求失败: ' + (e.message || e), null)
  }
}

/** AI 节点测试 — 需要完整工作流上下文 */
async function handleTestAiNode(nodeData) {
  try {
    // 收集上游节点的输出作为 mockOutputs
    const mockOutputs = {}
    const nodes = store.state.nodes
    const edges = store.state.edges
    const customParams = nodeData.config?.customParams || []

    // 解析 customParams JSON 如果是字符串
    let parsedCustomParams = customParams
    if (typeof customParams === 'string') {
      try {
        parsedCustomParams = JSON.parse(customParams)
      } catch { parsedCustomParams = [] }
    }

    // 为每个 link 类型的参数，从连线中找到对应的上游节点
    for (const param of parsedCustomParams) {
      if (param?.sourceType === 'link' && param?.sourceConfig?.nodeId) {
        const sourceNodeId = param.sourceConfig.nodeId
        // 从 store 中找到该节点的输出信息
        const sourceNode = nodes.find(n => n.id === sourceNodeId || n.nodeId === sourceNodeId)
        if (sourceNode) {
          // 使用节点ID作为 key，输出 result 默认值
          mockOutputs[sourceNodeId] = {
            result: `[${sourceNode.title || sourceNodeId} 的测试输出]`,
            success: true
          }
        }
      }
    }

    // 序列化当前工作流作为 ruleJson
    let ruleJson = ''
    try {
      const serialized = serialize(nodes, edges, { version: 1, workflowType: 'report' })
      ruleJson = JSON.stringify(serialized)
    } catch { ruleJson = '' }

    const testBody = {
      nodeId: nodeData.nodeId,
      nodeType: 'ai_node',
      title: nodeData.title,
      config: {
        ...(nodeData.config || {}),
        customParams: JSON.stringify(parsedCustomParams)
      },
      inputs: nodeData.inputs || {},
      inputTypes: nodeData.inputTypes || {},
      inputPorts: nodeData.inputPorts || [],
      outputPorts: nodeData.outputPorts || [],
      workflowContext: {
        ruleJson,
        contextParams: currentSection.value ? {
          enterpriseCode: 'YZH-STD-ENT',
          standardCode: currentFilter.standardCode,
          phaseCode: currentFilter.phaseCode
        } : {},
        mockOutputs
      }
    }

    const res = await proxy.http.post('api/workflow/test/ai-node', testBody, false)

    if (res?.success && res.data) {
      nodeData.onSuccess(res.data)
    } else {
      nodeData.onError(res?.error || 'AI 节点测试失败', null)
    }
  } catch (e) {
    console.error('[ReportRuleConfig] AI 节点测试失败:', e)
    nodeData.onError('请求失败: ' + (e.message || e), null)
  }
}

/** 结束节点测试 — 执行整条工作流 */
async function handleTestWorkflow(nodeData) {
  if (!currentSection.value) {
    nodeData.onError('请先选择报告章节', null)
    return
  }
  if (!store.state.nodes.length) {
    nodeData.onError('画布为空，请添加节点', null)
    return
  }

  try {
    // 序列化当前画布为工作流配置
    const config = serialize(store.state.nodes, store.state.edges, {
      version: 1,
      workflowType: 'report'
    })

    const res = await proxy.http.post('api/workflow/test/run', {
      taskType: 'TEST',
      ruleCode: currentSection.value.code,
      enterpriseCode: 'YZH-STD-ENT',
      standardCode: currentFilter.standardCode,
      phaseCode: currentFilter.phaseCode,
      configJson: JSON.stringify(config)
    }, false)

    if (res?.success && res.data) {
      nodeData.onSuccess(res.data)
    } else {
      const errorMsg = res?.error || res?.message || '测试失败'
      nodeData.onError(errorMsg, null)
    }
  } catch (e) {
    console.error('[ReportRuleConfig] 流程测试失败:', e)
    nodeData.onError('请求失败: ' + (e.message || e), null)
  }
}

/** docField/docTable 测试提取 */
async function handleTestDocExtract({ nodeType, body, onSuccess, onError }) {
  try {
    const url = nodeType === 'docField' ? 'api/DocExtractionRule/test-field' : 'api/DocExtractionRule/test-table'
    const res = await proxy.http.post(url, body, false)
    if (res?.status && res.data) {
      onSuccess(res.data)
    } else {
      onError(res?.message || '测试失败')
    }
  } catch (e) {
    console.error('测试提取失败:', e)
    onError('请求失败: ' + (e.message || e))
  }
}

// ==================== 选中规则 → 加载工作流 ====================

function clearCanvas() {
  diagram.value?.render({ nodes: [], edges: [] })
  store.clearAll()
}

function handleClearCanvas() {
  if (!store.state.nodes.length) { ElMessage.info('画布已为空'); return }
  ElMessageBox.confirm('确认清空画布上的所有节点和连线？', '清空确认', { type: 'warning' })
    .then(() => {
      clearCanvas()
      selectedNode.value = null
      ensureStartNode()
      ElMessage.success('画布已清空')
    })
    .catch(() => {})
}

function selectSection(section, phase) {
  currentSection.value = section
  savedTip.value = ''
  currentFilter.orgCode = phase.cbCode || currentFilter.orgCode
  currentFilter.standardCode = phase.stdCode || phase.standardCode || currentFilter.standardCode
  currentFilter.phaseCode = phase.phaseCode || currentFilter.phaseCode

  // 章节数据已在第4级加载时返回，直接渲染工作流
  renderWorkflow(section.workflowConfig, section.layoutJson)
}

/** 确保画布有 start 节点（空画布自动放置） */
function ensureStartNode() {
  if (!diagram.value) return
  const hasStart = store.state.nodes.some(n => n.classCode === 'start' || n.nodeType === 'start')
  if (!hasStart) {
    const startItem = { classCode: 'start', className: '开始' }
    const node = store.addNode(startItem, 100, 150)
    if (node) {
      diagram.value.addNode({
        id: node.id,
        type: 'circle',
        x: 100, y: 150,
        text: node.title,
        style: nodeStyle('start'),
        properties: {
          classCode: 'start',
          nodeType: 'start',
          title: node.title,
          skillCode: '',
          config: {},
          inputs: {},
          outputs: {},
          inputPorts: [],
          outputPorts: []
        }
      })
    }
  }
}

function renderWorkflow(workflowConfig, layoutJson) {
  if (!workflowConfig) {
    clearCanvas()
    ensureStartNode()
    return
  }
  try {
    const config = JSON.parse(workflowConfig)
    const layout = layoutJson ? JSON.parse(layoutJson) : null

    // 使用新的 deserialize（含旧数据迁移）
    const { nodes, edges, idGenerator, migrated } = deserialize(config, layout)

    // 加载到 store
    store.loadFromData(nodes, edges)
    store.idGenerator = idGenerator

    // 渲染到 LogicFlow 画布
    const lfNodes = nodes.map(n => {
      const nodeProps = {
        classCode: n.classCode,
        nodeType: n.nodeType,
        title: n.title,
        skillCode: n.skillCode,
        config: n.config,
        inputs: n.inputs,
        inputTypes: n.inputTypes || {},
        outputs: n.outputs,
        inputPorts: n.inputPorts,
        outputPorts: n.outputPorts
      }
      if (n.nodeType === 'branch') nodeProps.points = [[0, -30], [50, 0], [0, 30]]
      return {
        id: n.id,
        type: lfShapeType(n.nodeType),
        x: n.x,
        y: n.y,
        text: n.title || n.skillCode || n.id,
        style: nodeStyle(n.nodeType, n.skillCode),
        properties: nodeProps
      }
    })

    const lfEdges = edges.map(e => {
      const isBranchAnchor = e.sourceHandle === 'success' || e.sourceHandle === 'failure'
      return {
        id: e.id,
        type: 'polyline',
        sourceNodeId: e.source,
        targetNodeId: e.target,
        text: isBranchAnchor ? (e.sourceHandle === 'success' ? '成功' : '失败') : '',
        style: isBranchAnchor
          ? { stroke: e.sourceHandle === 'success' ? '#67C23A' : '#F56C6C', strokeWidth: 2 }
          : { stroke: '#5B8FF9', strokeWidth: 2 },
        properties: {
          sourceHandle: e.sourceHandle || null,
          targetHandle: e.targetHandle || null
        }
      }
    })

    diagram.value.render({ nodes: lfNodes, edges: lfEdges })
    if (migrated && !sessionStorage.getItem('_wf_migration_tip_shown')) {
        ElMessage.info('旧格式节点 ID 已自动迁移重编号')
        sessionStorage.setItem('_wf_migration_tip_shown', '1')
      }
  } catch (e) {
    console.error('工作流解析失败', e)
    ElMessage.error('工作流配置解析失败')
  }
}

// ==================== 布局 / 校验 / 保存 ====================

function autoLayout() {
  const nodes = store.state.nodes
  if (!nodes.length) return

  // 拓扑排序
  const inDeg = {}, adj = {}
  nodes.forEach(n => { inDeg[n.id] = 0; adj[n.id] = [] })
  store.state.edges.forEach(e => {
    if (inDeg[e.target] !== undefined) { inDeg[e.target]++; adj[e.source].push(e.target) }
  })
  const queue = nodes.filter(n => inDeg[n.id] === 0).map(n => n.id)
  const ordered = []
  while (queue.length) {
    const c = queue.shift(); ordered.push(c)
    for (const n of (adj[c] || [])) { inDeg[n]--; if (inDeg[n] === 0) queue.push(n) }
  }
  nodes.forEach(n => { if (!ordered.includes(n.id)) ordered.push(n.id) })

  // 只更新坐标（两层模型：改 model → 重派生画布）
  const posMap = {}
  ordered.forEach((id, idx) => {
    const col = idx % 4, row = Math.floor(idx / 4)
    posMap[id] = { x: 120 + col * 240, y: 80 + row * 140 }
  })

  // 更新 store 中的坐标
  for (const n of nodes) {
    if (posMap[n.id]) {
      n.x = posMap[n.id].x
      n.y = posMap[n.id].y
    }
  }
  store.markDirty()

  // 只更新 LogicFlow 节点坐标（不碰边 → 连线不断开）
  for (const n of nodes) {
    if (posMap[n.id]) {
      diagram.value.setProperties(n.id, {
        x: posMap[n.id].x,
        y: posMap[n.id].y
      })
    }
  }

  ElMessage.success('自动布局完成')
}

function validateGraph() {
  const nodes = store.state.nodes
  const edges = store.state.edges
  if (!nodes.length) { ElMessage.warning('画布为空'); return }

  // 转换为 workflow_config 格式
  const config = serialize(nodes, edges, {
    version: 1,
    workflowType: 'report'
  })

  // 执行综合拓扑分析（含详细日志输出到控制台）
  console.group('[ReportRuleConfig] 🔍 拓扑校验')
  const analysis = analyzeWorkflowTopology(config)
  console.groupEnd()

  const { validation, sortResult, paths, unreachable, summary } = analysis

  // 构建节点 title 映射（用于友好显示）
  const nodeTitleMap = {}
  for (const n of nodes) { nodeTitleMap[n.id] = n.title || n.id }

  // 格式化路径显示
  const pathSummary = summarizePaths(paths)
  const pathDetails = paths.map((p, idx) => {
    const nodeNames = p.nodes.map(id => nodeTitleMap[id] || id)
    const branchInfo = p.branchDecisions.length > 0
      ? ` (${p.branchDecisions.map(d => `${nodeTitleMap[d.at]||d.at}:${d.choice==='success'?'✓':'✗'}`).join(', ')})`
      : ''
    return `  路径${idx + 1}: ${nodeNames.join(' → ')}${branchInfo}`
  }).join('\n')

  // 有错误 → 显示错误信息
  if (!validation.valid) {
    const errorMessages = validation.errors.map(e => `  ✗ ${e.message}`).join('\n')
    const warnMessages = validation.warnings.length > 0
      ? '\n\n' + validation.warnings.map(w => `  ⚠ ${w.message}`).join('\n')
      : ''

    // 路径信息
    const pathInfo = paths.length > 0 ? `\n\n📋 拓扑路径 (${pathSummary}):\n${pathDetails}` : '\n\n📋 无有效路径'

    ElMessageBox.alert(
      `校验失败！发现 ${validation.errors.length} 个错误：\n\n${errorMessages}${warnMessages}${pathInfo}`,
      '拓扑校验结果',
      { type: 'error', confirmButtonText: '确定' }
    )
    return false
  }

  // 仅有警告
  if (validation.warnings.length > 0) {
    const warnMessages = validation.warnings.map(w => `  ⚠ ${w.message}`).join('\n')
    const pathInfo = paths.length > 0 ? `\n\n📋 拓扑路径 (${pathSummary}):\n${pathDetails}` : '\n\n📋 无有效路径'

    ElMessageBox.alert(
      `校验通过（有警告）：\n\n${warnMessages}${pathInfo}`,
      '拓扑校验结果',
      { type: 'warning', confirmButtonText: '确定' }
    )
    return true
  }

  // 完全通过
  const pathInfo = paths.length > 0 ? `\n\n📋 拓扑路径 (${pathSummary}):\n${pathDetails}` : ''
  ElMessageBox.alert(
    `✓ 校验通过！\n\n节点: ${summary.totalNodes} | 边: ${summary.totalEdges} | 路径: ${summary.totalPaths}${pathInfo}`,
    '拓扑校验结果',
    { type: 'success', confirmButtonText: '确定' }
  )
  return true
}

// ==================== 保存工作流（到报告章节） ====================

async function handleSave() {
  if (!currentSection.value) { ElMessage.warning('请先选择报告章节'); return }
  if (!store.state.nodes.length) { ElMessage.warning('画布为空，请添加节点'); return }

  // 通过 serialize 生成落库 JSON
  const config = serialize(store.state.nodes, store.state.edges, {
    version: 1,
    workflowType: 'report'
  })
  const layout = extractLayout(store.state.nodes)

  try {
    await ElMessageBox.confirm(`保存工作流到章节「${currentSection.value.sectionName}」？`, '保存确认', { type: 'info' })
    const res = await proxy.http.post('api/report-definition/section', {
      id: currentSection.value.id,
      code: currentSection.value.code,
      orgCode: currentFilter.orgCode,
      reportCode: currentSection.value.reportCode,
      sectionName: currentSection.value.sectionName,
      sectionNameEn: currentSection.value.sectionNameEn,
      sortOrder: currentSection.value.sortOrder,
      isActive: currentSection.value.isActive !== false,
      workflowConfig: JSON.stringify(config),
      layoutJson: JSON.stringify(layout),
      remark: currentSection.value.remark
    }, true)
    if (res?.status) {
      savedTip.value = `${new Date().toLocaleTimeString()} 已保存`
      currentSection.value.workflowConfig = JSON.stringify(config)
      currentSection.value.layoutJson = JSON.stringify(layout)
      store.markClean()
      ElMessage.success('工作流保存成功')
    } else ElMessage.error(res?.message || '保存失败')
  } catch (e) { if (e !== 'cancel') ElMessage.error('保存失败') }
}
</script>

<style scoped lang="less">
.report-rule-page { padding: 16px; height: 100%; display: flex; flex-direction: column; overflow: hidden; box-sizing: border-box; }
.page-body { display: flex; gap: 12px; flex: 1; min-height: 0; }

/* 左栏 */
.left-panel { width: 300px; min-width: 300px; }
.tree-card { height: 100%; overflow: hidden; display: flex; flex-direction: column; }
:deep(.el-card__body) { flex: 1; overflow: hidden; display: flex; flex-direction: column; padding: 0; }
.panel-header { display: flex; align-items: center; justify-content: space-between; font-size: 13px; font-weight: 600; }
.tree-search { padding: 8px 12px; border-bottom: 1px solid #f0f0f0; }
.tree-body { flex: 1; overflow-y: auto; padding: 4px 0; }
.tree-group { margin-bottom: 2px; }
.tree-node {
  display: flex; align-items: center; gap: 6px; padding: 6px 12px;
  cursor: pointer; font-size: 13px; transition: background 0.2s; user-select: none;
  &:hover { background: #f5f7fa; }
  &.level-0 { font-weight: 600; color: #303133; }
  &.level-1 { padding-left: 28px; font-weight: 500; color: #606266; }
  &.level-2 { padding-left: 52px; color: #606266; }
  &.level-3 {
    padding-left: 76px; color: #909399; font-size: 12px;
    &.active { background: #ecf5ff; color: #409eff; border-right: 3px solid #409eff; }
  }
}
.tree-toggle { font-size: 12px; color: #c0c4cc; transition: transform 0.2s; &.expanded { transform: rotate(90deg); } }
.tree-icon { font-size: 14px; flex-shrink: 0;
  &.org { color: #409eff; }
  &.standard { color: #67c23a; }
  &.phase { color: #e6a23c; }
  &.rule { color: #909399; }
}
.tree-label { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.node-badge { margin-left: 4px; transform: scale(0.85); }
.config-dot { width: 6px; height: 6px; border-radius: 50%; background: #67C23A; flex-shrink: 0; }
.tree-icon.rule.configured { color: #67C23A; }
.tree-node.level-3.configured { color: #606266; }
.rule-empty { padding: 12px 12px 12px 76px; text-align: center; color: #c0c4cc; font-size: 12px; }
.rule-loading { display: flex; align-items: center; gap: 6px; padding: 8px 12px 8px 76px; color: #c0c4cc; font-size: 12px; }
.tree-empty { display: flex; justify-content: center; padding: 20px 0; }

/* 中栏 */
.canvas-panel { flex: 1; display: flex; flex-direction: column; background: #fff; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,.06); overflow: hidden; }
.canvas-toolbar { display: flex; align-items: center; justify-content: space-between; padding: 8px 12px; border-bottom: 1px solid #f0f0f0; }
.canvas-title { font-size: 14px; font-weight: 600; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.toolbar-actions { display: flex; align-items: center; gap: 4px; flex-shrink: 0; }
.canvas-container { flex: 1; min-height: 0; }
.canvas-status { display: flex; align-items: center; gap: 12px; padding: 6px 12px; font-size: 12px; color: #909399; border-top: 1px solid #f0f0f0; }
.rule-code-text { margin-left: auto; }
.saved-text { color: #67C23A; }

/* 右栏 */
.right-panel { width: 300px; min-width: 300px; display: flex; flex-direction: column; gap: 8px; }
.skill-panel-wrapper { flex: 1; min-height: 0; background: #fff; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,.06); overflow: hidden; }
.prop-panel-wrapper { flex: 1; min-height: 0; background: #fff; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,.06); overflow: hidden; }
</style>
