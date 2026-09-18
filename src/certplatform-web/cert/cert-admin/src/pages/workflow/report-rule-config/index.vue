<template>
  <div class="report-rule-config-page studio-layout">
    <div class="page-header">
      <div class="header-left">
        <el-icon><Setting /></el-icon>
        <span class="header-title">报告内容设计</span>
      </div>
    </div>

    <div class="page-body">
      <!-- ===== 左栏：统一4级树 ===== -->
      <aside class="left-panel">
        <div class="panel-header">
          <span>机构 / 标准 / 阶段 / 报告章节</span>
          <el-button link size="small" @click="refreshTree"
            ><el-icon><Refresh /></el-icon
          ></el-button>
        </div>
        <div class="tree-search">
          <el-input
            v-model="searchText"
            placeholder="搜索..."
            size="small"
            clearable
            :prefix-icon="Search"
          />
        </div>
        <div class="tree-container">
          <template v-for="org in treeData" :key="org.id">
            <div v-if="org.visible" class="tree-group">
              <!-- 机构 -->
              <div
                class="tree-node level-0"
                :class="{ expanded: org.expanded }"
                @click="toggleExpand(org)"
              >
                <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                <el-icon class="type-icon org"><OfficeBuilding /></el-icon>
                <span class="tree-label">{{ org.label }}</span>
                <el-badge v-if="org.children?.length" :value="org.children.length" type="info" />
              </div>

              <!-- 标准 -->
              <template v-if="org.expanded && org.children">
                <template v-for="std in org.children" :key="std.id">
                  <div
                    v-if="std.visible"
                    class="tree-node level-1"
                    :class="{ expanded: std.expanded }"
                    @click="toggleExpand(std)"
                  >
                    <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                    <el-icon class="type-icon standard"><Document /></el-icon>
                    <span class="tree-label">{{ std.label }}</span>
                    <el-badge
                      v-if="std.children?.length"
                      :value="std.children.length"
                      type="info"
                    />
                  </div>

                  <!-- 阶段 -->
                  <template v-if="std.expanded && std.children && std.visible">
                    <template v-for="phase in std.children" :key="phase.id">
                      <div
                        v-if="phase.visible"
                        class="tree-node level-2"
                        :class="{ expanded: phase.expanded }"
                        @click="togglePhase(phase, std, org)"
                      >
                        <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                        <el-icon class="type-icon phase"><Calendar /></el-icon>
                        <span class="tree-label">{{ phase.label }}</span>
                        <el-badge
                          v-if="phase.children?.length"
                          :value="phase.children.length"
                          type="info"
                        />
                      </div>

                      <!-- 报告章节 -->
                      <div v-if="phase.visible && phase.expanded && phase.sectionLoaded">
                        <div
                          v-for="section in phase.children"
                          :key="section.id"
                          class="tree-node level-3"
                          :class="{
                            active: currentSection?.id === section.id,
                            configured: !!section.workflowConfig
                          }"
                          @click="selectSection(section, phase)"
                        >
                          <el-icon class="type-icon rule" :class="{ configured: !!section.workflowConfig }">
                            <CircleCheck v-if="section.workflowConfig" />
                            <Document v-else />
                          </el-icon>
                          <span class="tree-label">{{ section.sectionName }}</span>
                        </div>
                      </div>
                    </template>
                  </template>
                </template>
              </template>
            </div>
          </template>
        </div>
      </aside>

      <!-- ===== 中栏：LogicFlow 画布 ===== -->
      <main class="main-content">
        <div class="canvas-toolbar">
          <span class="canvas-title">{{
            currentSection ? `工作流：${currentSection.sectionName || currentSection.code || '未命名'}` : '请选择报告章节'
          }}</span>
          <div class="toolbar-actions">
            <el-button size="small" @click="autoLayout"
              ><el-icon><Grid /></el-icon> 布局</el-button
            >
            <el-button size="small" type="danger" plain @click="handleClearCanvas"
              ><el-icon><Delete /></el-icon> 清空</el-button
            >
            <el-button size="small" @click="validateGraph"
              ><el-icon><CircleCheck /></el-icon> 校验</el-button
            >
            <el-button
              type="primary"
              size="small"
              :disabled="!currentSection || !store.state.dirty"
              @click="handleSave"
            >
              <el-icon><Download /></el-icon> 保存
            </el-button>
          </div>
        </div>
        <div
          ref="canvasRef"
          class="canvas-container"
          @dragover.prevent="onCanvasDragOver"
          @drop.prevent="onCanvasDrop"
        ></div>
        <div class="canvas-footer">
          <span
            >节点: {{ store.state.nodes.length }} | 边: {{ store.state.edges.length }} | 状态:
            {{ store.state.dirty ? '未保存' : '已保存' }}</span
          >
          <span v-if="currentSection" class="rule-code-text">{{ currentSection.code }}</span>
        </div>
      </main>

      <!-- ===== 右栏：节点库 + 属性面板 ===== -->
      <aside class="right-panel">
        <el-tabs v-model="activeRightTab" class="studio-tabs">
          <el-tab-pane label="节点库" name="nodes">
            <div class="nodes-panel">
              <SkillPanel :skills="skills" :categories="categories" @add-node="handleAddNode" />
            </div>
          </el-tab-pane>
          <el-tab-pane label="属性" name="props">
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
          </el-tab-pane>
        </el-tabs>
      </aside>
    </div>
  </div>
</template>

<script setup>
import { yzhApi } from '@yzh-core/api/client'
import NodePropertyForm from '../nc-config/NodePropertyForm.vue'
import SkillPanel from '../nc-config/SkillPanel.vue'
import {
  analyzeWorkflowTopology,
  nodeStyle,
  setLogEnabled,
  setLogLevel,
  summarizePaths
} from '../nc-config/composables/compiler'
import {
  deserialize,
  extractLayout,
  serialize
} from '@share/composables/workflow/serializer'
import { useWorkflowStore } from '../nc-config/composables/useWorkflowStore'
import { installLogicFlowPatch } from '@share/utils/logicflow-patch'
installLogicFlowPatch()
import {
  Calendar,
  CircleCheck,
  Delete,
  Document,
  Download,
  Grid,
  OfficeBuilding,
  Refresh,
  Search,
  Setting,
  ArrowRight
} from '@element-plus/icons-vue'
import LogicFlow from '@logicflow/core'
import '@logicflow/core/dist/index.css'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  computed,
  nextTick,
  onBeforeUnmount,
  onMounted,
  reactive,
  ref,
  watch
} from 'vue'

setLogEnabled(true)
setLogLevel('INFO')

const activeRightTab = ref('nodes')
const canvasRef = ref(null)
const diagram = ref(null)
let _resizeObserver = null

const store = useWorkflowStore()

const treeData = ref([])
const searchText = ref('')
const currentSection = ref(null)
const currentFilter = reactive({ orgCode: '', standardCode: '', phaseCode: '' })

const skills = ref([])
const categories = ref([])
const selectedNode = ref(null)

const forceRefreshTick = ref(0)

const docRules = ref([])
const currentDocFields = ref([])
const currentDocTables = ref([])

const savedTip = ref('')

const executing = ref(false)

const selectedEdgeId = ref(null)

// ==================== 初始化 ====================

onMounted(async () => {
  await Promise.all([loadSkills(), loadCategories(), loadTree(), loadDocRules()])
  await nextTick()
  initDiagram()
})

onBeforeUnmount(() => {
  document.removeEventListener('keydown', handleKeyDown)
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
    const res = await yzhApi.post('/api/Workflow/WfSkill/filter', { Page: 1, PageSize: 200, Filters: [] })
    const items = res?.data?.Items || res?.data?.items || []
    skills.value = items.map((s) => ({
      ...s,
      skillCode: s.Code || s.skillCode,
      skillName: s.Name || s.skillName,
      category: s.CategoryCode || s.category || '_default',
      skillType: s.SkillType || s.skillType || 'manual'
    }))
  } catch (e) { /* Skill 尚未初始化，静默 */ }
}

async function loadCategories() {
  try {
    const res = await yzhApi.post('/api/Workflow/WfSkillCategory/filter', { Page: 1, PageSize: 200, Filters: [] })
    const items = res?.data?.Items || res?.data?.items || []
    categories.value = items.map((c) => ({
      ...c,
      categoryCode: c.Code || c.categoryCode,
      categoryName: c.Name || c.categoryName,
      color: c.Color || c.color || '#409EFF',
      sortOrder: c.SortOrder ?? c.sortOrder ?? 99
    }))
  } catch (e) { /* 分类尚未初始化，静默 */ }
}

async function loadDocRules() {
  try {
    const res = await yzhApi.get('/api/Workflow/DocExtractionRule/configured-rules')
    docRules.value = res?.data || res?.Data || []
  } catch (e) { /* 文档规则接口尚未就绪，静默 */ }
}

async function onNodeDocChange(ruleCode) {
  if (!ruleCode) return
  await loadFieldsAndTables(ruleCode)
}

async function loadFieldsAndTables(ruleCode) {
  try {
    const res = await yzhApi.get(`/api/Workflow/DocExtractionRule/${ruleCode}/fields-tables`)
    const d = res?.data || res?.Data
    if (d) {
      currentDocFields.value = d.fields || d.Fields || []
      currentDocTables.value = d.tables || d.Tables || []
    }
  } catch (e) { /* 字段/表格接口尚未就绪，静默 */ }
}

// ==================== 树数据 ====================

async function loadTree() {
  try {
    const res = await yzhApi.get('/api/Workflow/StandardDirectory/organization-tree')
    const raw = res?.Data || res?.data || []
    treeData.value = raw.map((org) => ({
      ...org,
      expanded: true,
      visible: true,
      children: (org.children || []).map((std) => ({
        ...std,
        expanded: false,
        visible: true,
        children: (std.children || []).map((phase) => ({
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
  } catch (e) {}
}

function applySearchFilter() {
  const kw = searchText.value?.toLowerCase() || ''
  if (!kw) {
    treeData.value.forEach((org) => {
      org.visible = true
      ;(org.children || []).forEach((std) => {
        std.visible = true
        ;(std.children || []).forEach((p) => {
          p.visible = true
        })
      })
    })
    return
  }
  treeData.value.forEach((org) => {
    let orgHasMatch = org.label?.toLowerCase().includes(kw)
    ;(org.children || []).forEach((std) => {
      let stdHasMatch = std.label?.toLowerCase().includes(kw)
      ;(std.children || []).forEach((p) => {
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
    const orgCode = currentFilter.orgCode
    const standardCode = currentFilter.standardCode
    const phaseCode = currentFilter.phaseCode
    const res = await yzhApi.get('/api/report-definition/section/by-context', {
      params: { orgCode, standardCode, phaseCode }
    })
    const items = res?.data || res?.Data || []
    phase.children = items.map((s) => ({
      ...s,
      id: s.Code || s.code,
      sectionName: s.SectionName || s.sectionName
    }))
    phase.sectionLoaded = true
  } catch (e) {
    phase.children = []
  } finally {
    phase.sectionLoading = false
  }
}

const refreshTree = () => {
  loadTree()
}

// ==================== 画布 ====================

function initDiagram() {
  diagram.value = new LogicFlow({
    container: canvasRef.value,
    grid: { size: 20, visible: true, type: 'mesh' },
    // ⚠️ LogicFlow v2 的 background 必须用对象形式；字符串会被按字符索引展开，
    // 触发 SVG CSSStyleDeclaration indexed setter 异常导致画布不渲染。
    background: { backgroundColor: '#fafbfc' },
    edgeType: 'polyline',
    allowResize: true,
    allowRotate: false,
    isSilentMode: false,
    stopScrollGraph: false,
    stopZoomGraph: false,
    stopMoveGraph: false,
    snapline: false,
    keyboard: { enabled: false },
    textEditMode: false
  })

  diagram.value.on('node:click', ({ data }) => {
    const props = data.properties || {}
    const storeNode = store.getNodeById(data.id)

    const { inputs: syncedInputs, inputTypes: syncedInputTypes } = computeNodeInputsFromEdges(
      data.id,
      storeNode?.inputs || props.inputs || {},
      storeNode?.inputTypes || props.inputTypes || {}
    )

    if (
      JSON.stringify(syncedInputs) !== JSON.stringify(storeNode?.inputs || {}) ||
      JSON.stringify(syncedInputTypes) !== JSON.stringify(storeNode?.inputTypes || {})
    ) {
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
    if (nodeData.nodeType === 'branch') {
      const gd = diagram.value?.getGraphData()
      const outEdges = (gd.edges || []).filter((e) => e.sourceNodeId === data.id)
      nodeData.branchEdges = outEdges.map((e) => ({
        handle: e.properties?.sourceHandle || '',
        targetId: e.targetNodeId,
        edgeId: e.id
      }))
    }
    selectedNode.value = nodeData
    activeRightTab.value = 'props'
  })

  diagram.value.on('node:dbclick', ({ data }) => {
    const nodeType = data.properties?.nodeType || data.properties?.classCode
    if (nodeType === 'start') return
    promptEditNodeName(data.id, data.properties?.title || data.text || '')
  })

  diagram.value.on('edge:click', ({ data }) => {
    if (_renamingNodeId) return
    selectedNode.value = null
    selectedEdgeId.value = data.id
  })
  diagram.value.on('blank:click', () => {
    if (_renamingNodeId && Date.now() - _renamingTimestamp < RENAMING_GUARD_MS) return
    selectedNode.value = null
    selectedEdgeId.value = null
  })

  diagram.value.on('edge:add', ({ data }) => {
    autoSetBranchHandle(data)
    onEdgeChange()
  })
  diagram.value.on('edge:delete', ({ data }) => {
    onEdgeChange()
  })

  document.addEventListener('keydown', handleKeyDown)

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
  if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA') return
  if (e.key !== 'Delete' && e.key !== 'Backspace') return
  e.preventDefault()

  if (selectedEdgeId.value && diagram.value) {
    diagram.value.deleteEdge(selectedEdgeId.value)
    selectedEdgeId.value = null
    return
  }
  if (selectedNode.value?.nodeId && diagram.value) {
    handleDeleteNode(selectedNode.value.nodeId)
  }
}

function computeNodeInputsFromEdges(nodeId, currentInputs = {}, currentInputTypes = {}) {
  if (!diagram.value) return { inputs: currentInputs, inputTypes: currentInputTypes }
  const gd = diagram.value.getGraphData()
  const inEdges = (gd.edges || []).filter((e) => e.targetNodeId === nodeId)
  const nodeProps = gd.nodes.find((n) => n.id === nodeId)?.properties || {}
  const inputPorts = nodeProps.inputPorts || []
  const newInputs = {}
  const newTypes = {}

  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const edge = inEdges.find((e) => {
      const handle = e.properties?.targetHandle
      return !handle || handle === port.name
    })
    if (edge) {
      if (currentInputTypes[port.name] === 'constant' && currentInputs[port.name]) {
        continue
      }
      newInputs[port.name] = edge.sourceNodeId
      newTypes[port.name] = 'link'
    }
  }

  const merged = { ...currentInputs, ...newInputs }
  const mergedTypes = { ...currentInputTypes, ...newTypes }
  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const val = merged[port.name]
    if (val && gd.nodes.some((n) => n.id === val)) {
      const stillConnected = inEdges.some((e) => e.sourceNodeId === val)
      if (!stillConnected) {
        delete merged[port.name]
        delete mergedTypes[port.name]
      }
    }
  }

  return { inputs: merged, inputTypes: mergedTypes }
}

function syncSelectedNodeInputs() {
  if (!selectedNode.value || !diagram.value) return
  const nodeId = selectedNode.value.nodeId
  const { inputs: merged, inputTypes: mergedTypes } = computeNodeInputsFromEdges(
    nodeId,
    selectedNode.value.inputs || {},
    selectedNode.value.inputTypes || {}
  )

  if (
    JSON.stringify(merged) !== JSON.stringify(selectedNode.value.inputs) ||
    JSON.stringify(mergedTypes) !== JSON.stringify(selectedNode.value.inputTypes || {})
  ) {
    diagram.value.setProperties(nodeId, { inputs: merged, inputTypes: mergedTypes })
    selectedNode.value = { ...selectedNode.value, inputs: merged, inputTypes: mergedTypes }
    store.setInputValue(nodeId, null, null)
    const storeNode = store.getNodeById(nodeId)
    if (storeNode) {
      storeNode.inputs = merged
      storeNode.inputTypes = mergedTypes
    }
  }
}

function autoSetBranchHandle(edgeData) {
  if (!edgeData?.sourceNodeId || !edgeData?.targetNodeId) return
  const gd = diagram.value?.getGraphData()
  if (!gd) return
  const sourceNode = gd.nodes.find((n) => n.id === edgeData.sourceNodeId)
  if (!sourceNode) return
  const sourceType = sourceNode.properties?.nodeType || sourceNode.properties?.classCode
  if (sourceType !== 'branch') return

  const existingProps = edgeData.properties || {}
  if (existingProps.sourceHandle) return

  const existingEdges = (gd.edges || []).filter(
    (e) => e.sourceNodeId === edgeData.sourceNodeId && e.id !== edgeData.id
  )
  const hasSuccess = existingEdges.some((e) => e.properties?.sourceHandle === 'success')

  const handle = hasSuccess ? 'failure' : 'success'
  const label = handle === 'success' ? '成功' : '失败'
  const color = handle === 'success' ? '#67C23A' : '#F56C6C'

  diagram.value.setProperties(edgeData.id, { sourceHandle: handle })
  diagram.value.updateText(edgeData.id, label)

  const edge = gd.edges.find((e) => e.id === edgeData.id)
  if (edge) {
    edge.properties = { ...edge.properties, sourceHandle: handle }
    edge.text = label
    edge.style = { stroke: color, strokeWidth: 2 }
  }
}

function onEdgeChange() {
  syncSelectedNodeInputs()
  const gd = diagram.value?.getGraphData()
  if (gd) {
    store.state.edges = (gd.edges || []).map((e) => ({
      id: e.id,
      source: e.sourceNodeId,
      target: e.targetNodeId,
      sourceHandle: e.properties?.sourceHandle || null,
      targetHandle: e.properties?.targetHandle || null
    }))
  }
}

const canvasNodesForPanel = computed(() => {
  return store.state.nodes.map((n) => {
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

    const node = store.addNode(item, x, y)
    if (!node) return

    const category =
      item.category || skills.value.find((s) => s.skillCode === item.skillCode)?.category || ''
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
    if (node.nodeType === 'branch') {
      props.points = [
        [0, -30],
        [50, 0],
        [0, 30]
      ]
    }
    diagram.value.addNode({
      id: node.id,
      type: lfShapeType(node.nodeType),
      x,
      y,
      text: node.title,
      style: nodeStyle(node.nodeType, node.skillCode, category),
      properties: props
    })
  } catch (e) {}
}

function lfShapeType(nodeType) {
  if (nodeType === 'start' || nodeType === 'end') return 'circle'
  if (nodeType === 'branch') return 'polygon'
  return 'rect'
}

function handleAddNode(item) {
  const maxX = store.state.nodes.reduce((m, n) => Math.max(m, n.x || 0), 100)
  const maxY = store.state.nodes.reduce((m, n) => Math.max(m, n.y || 0), 80)
  const node = store.addNode(item, 120 + (maxX % 600), 80 + (maxY % 400))
  if (!node) return

  const category =
    item.category || skills.value.find((s) => s.skillCode === item.skillCode)?.category || ''
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
  if (node.nodeType === 'branch')
    addProps.points = [
      [0, -30],
      [50, 0],
      [0, 30]
    ]
  diagram.value.addNode({
    id: node.id,
    type: lfShapeType(node.nodeType),
    x: node.x,
    y: node.y,
    text: node.title,
    style: nodeStyle(node.nodeType, node.skillCode, category),
    properties: addProps
  })
}

function handleUpdateNode(data) {
  if (!data.nodeId) return

  const storeNode = store.getNodeById(data.nodeId)
  const latestTitle = storeNode?.title || data.title

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

  nextTick(() => {
    const sn = store.getNodeById(data.nodeId)
    if (sn) {
      let branchEdges = undefined
      if (sn.nodeType === 'branch' || sn.classCode === 'branch') {
        const gd = diagram.value?.getGraphData()
        const outEdges = (gd?.edges || []).filter((e) => e.sourceNodeId === data.nodeId)
        branchEdges = outEdges.map((e) => ({
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

let _renamingNodeId = null
let _renamingTimestamp = 0
const RENAMING_GUARD_MS = 1000

function promptEditNodeName(nodeId, currentName) {
  _renamingNodeId = nodeId
  _renamingTimestamp = Date.now()

  ElMessageBox.prompt('请输入节点名称', '编辑节点名称', {
    inputValue: currentName,
    inputPattern: /.+/,
    inputErrorMessage: '名称不能为空',
    confirmButtonText: '确定',
    cancelButtonText: '取消'
  })
    .then(({ value }) => {
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

      diagram.value.updateText(nodeId, name)
      diagram.value.setProperties(nodeId, { title: name })

      setTimeout(() => {
        const storeNode = store.getNodeById(nodeId)
        if (storeNode) {
          let branchEdges = undefined
          if (storeNode.nodeType === 'branch' || storeNode.classCode === 'branch') {
            const gd = diagram.value?.getGraphData()
            const outEdges = (gd?.edges || []).filter((e) => e.sourceNodeId === nodeId)
            branchEdges = outEdges.map((e) => ({
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
    })
    .catch(() => {
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

  if (sourceHandle && portName === sourceHandle) {
    const toDelete = (gd.edges || []).filter(
      (e) => e.sourceNodeId === sourceNodeId && e.properties?.sourceHandle === sourceHandle
    )
    for (const existing of toDelete) {
      diagram.value.deleteEdge(existing.id)
    }
    if (selectedEdgeId.value && toDelete.some((e) => e.id === selectedEdgeId.value)) {
      selectedEdgeId.value = null
    }
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

  if (!sourceNodeId) {
    const toDelete = (gd.edges || []).filter(
      (e) =>
        e.targetNodeId === targetNodeId &&
        (e.properties?.targetHandle === portName || (!e.properties?.targetHandle && !portName))
    )
    for (const existing of toDelete) {
      diagram.value.deleteEdge(existing.id)
    }
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

  const edge = store.connect(sourceNodeId, targetNodeId, null, portName)
  if (!edge) return

  const existingEdges = (gd.edges || []).filter(
    (e) =>
      e.targetNodeId === targetNodeId &&
      (e.properties?.targetHandle === portName || (!e.properties?.targetHandle && !portName))
  )
  for (const existing of existingEdges) {
    diagram.value.deleteEdge(existing.id)
  }

  const sourceNode = gd.nodes.find((n) => n.id === sourceNodeId)
  const sourceType = sourceNode?.properties?.nodeType || sourceNode?.properties?.classCode
  let autoHandle = null
  if (sourceType === 'branch') {
    const branchEdges = (gd.edges || []).filter((e) => e.sourceNodeId === sourceNodeId)
    const hasSuccess = branchEdges.some((e) => e.properties?.sourceHandle === 'success')
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
    if (nodeData.nodeType === 'ai_node') {
      await handleTestAiNode(nodeData)
      return
    }

    const res = await yzhApi.post('/api/Workflow/test/node', {
      nodeId: nodeData.nodeId,
      nodeType: nodeData.nodeType,
      title: nodeData.title,
      skillCode: nodeData.skillCode,
      config: nodeData.config,
      inputs: nodeData.inputs,
      inputTypes: nodeData.inputTypes,
      inputPorts: nodeData.inputPorts,
      outputPorts: nodeData.outputPorts
    })

    if (res?.success && res.data) {
      nodeData.onSuccess(res.data)
    } else {
      nodeData.onError(res?.error || '测试失败', null)
    }
  } catch (e) {
    nodeData.onError('请求失败: ' + (e.message || e), null)
  }
}

async function handleTestAiNode(nodeData) {
  try {
    const mockOutputs = {}
    const nodes = store.state.nodes
    const edges = store.state.edges
    const customParams = nodeData.config?.customParams || []

    let parsedCustomParams = customParams
    if (typeof customParams === 'string') {
      try {
        parsedCustomParams = JSON.parse(customParams)
      } catch {
        parsedCustomParams = []
      }
    }

    for (const param of parsedCustomParams) {
      if (param?.sourceType === 'link' && param?.sourceConfig?.nodeId) {
        const sourceNodeId = param.sourceConfig.nodeId
        const sourceNode = nodes.find((n) => n.id === sourceNodeId || n.nodeId === sourceNodeId)
        if (sourceNode) {
          mockOutputs[sourceNodeId] = {
            result: `[${sourceNode.title || sourceNodeId} 的测试输出]`,
            success: true
          }
        }
      }
    }

    let ruleJson = ''
    try {
      const serialized = serialize(nodes, edges, { version: 1, workflowType: 'validation' })
      ruleJson = JSON.stringify(serialized)
    } catch {
      ruleJson = ''
    }

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
        contextParams: currentSection.value
          ? {
              enterpriseCode: currentSection.value.enterpriseCode || 'YZH-STD-ENT',
              standardCode: currentSection.value.standardCode,
              phaseCode: currentSection.value.phaseCode
            }
          : {},
        mockOutputs
      }
    }

    const res = await yzhApi.post('/api/Workflow/test/ai-node', testBody)

    if (res?.success && res.data) {
      nodeData.onSuccess(res.data)
    } else {
      nodeData.onError(res?.error || 'AI 节点测试失败', null)
    }
  } catch (e) {
    nodeData.onError('请求失败: ' + (e.message || e), null)
  }
}

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
    const config = serialize(store.state.nodes, store.state.edges, {
      version: 1,
      workflowType: 'validation'
    })

    const res = await yzhApi.post('/api/Workflow/test/run', {
      taskType: 'TEST',
      ruleCode: currentSection.value.code,
      enterpriseCode: currentSection.value.enterpriseCode || 'YZH-STD-ENT',
      standardCode: currentSection.value.standardCode || currentFilter.standardCode,
      phaseCode: currentSection.value.phaseCode || currentFilter.phaseCode,
      configJson: JSON.stringify(config)
    })

    if (res?.success && res.data) {
      nodeData.onSuccess(res.data)
    } else {
      const errorMsg = res?.error || res?.message || '测试失败'
      nodeData.onError(errorMsg, null)
    }
  } catch (e) {
    nodeData.onError('请求失败: ' + (e.message || e), null)
  }
}

async function handleTestDocExtract({ nodeType, body, onSuccess, onError }) {
  try {
    const url =
      nodeType === 'docField'
        ? '/api/DocExtractionRule/test-field'
        : '/api/DocExtractionRule/test-table'
    const res = await yzhApi.post(url, body)
    if (res?.status && res.data) {
      onSuccess(res.data)
    } else {
      onError(res?.message || '测试失败')
    }
  } catch (e) {
    onError('请求失败: ' + (e.message || e))
  }
}

// ==================== 选中章节 → 加载工作流 ====================

function clearCanvas() {
  if (!diagram.value) return
  try {
    const gm = diagram.value.graphModel
    if (gm) {
      gm.edges = []
      gm.nodes = []
    } else {
      diagram.value.render({ nodes: [], edges: [] })
    }
  } catch (e) {
    // 降级：静默忽略
  }
  store.clearAll()
}

function handleClearCanvas() {
  if (!store.state.nodes.length) {
    ElMessage.info('画布已为空')
    return
  }
  ElMessageBox.confirm('确认清空画布上的所有节点和连线？', '清空确认', { type: 'warning' })
    .then(() => {
      clearCanvas()
      selectedNode.value = null
      ensureStartNode()
      ElMessage.success('画布已清空')
    })
    .catch(() => {})
}

async function selectSection(section, phase) {
  currentSection.value = { ...section }
  savedTip.value = ''
  currentFilter.orgCode = phase.cbCode || currentFilter.orgCode
  currentFilter.standardCode = phase.stdCode || phase.standardCode || currentFilter.standardCode
  currentFilter.phaseCode = phase.phaseCode || currentFilter.phaseCode

  try {
    const workflowConfig = section.workflowConfig || section.WorkflowConfig
    const layoutJson = section.layoutJson || section.LayoutJson
    if (workflowConfig) {
      renderWorkflow(workflowConfig, layoutJson)
    } else {
      clearCanvas()
      ensureStartNode()
    }
  } catch (e) {
    ElMessage.error('加载章节工作流失败')
  }
}

function ensureStartNode() {
  if (!diagram.value) return
  const hasStart = store.state.nodes.some((n) => n.classCode === 'start' || n.nodeType === 'start')
  if (!hasStart) {
    const startItem = { classCode: 'start', className: '开始' }
    const node = store.addNode(startItem, 100, 150)
    if (node) {
      diagram.value.addNode({
        id: node.id,
        type: 'circle',
        x: 100,
        y: 150,
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
    const config = typeof workflowConfig === 'string' ? JSON.parse(workflowConfig) : workflowConfig
    const layout = layoutJson ? (typeof layoutJson === 'string' ? JSON.parse(layoutJson) : layoutJson) : null

    const { nodes, edges, idGenerator, migrated } = deserialize(config, layout)

    store.loadFromData(nodes, edges)
    store.idGenerator = idGenerator

    const lfNodes = nodes.map((n) => {
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
      if (n.nodeType === 'branch')
        nodeProps.points = [
          [0, -30],
          [50, 0],
          [0, 30]
        ]
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

    const lfEdges = edges.map((e) => {
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
    ElMessage.error('工作流配置解析失败')
  }
}

// ==================== 布局 / 校验 / 保存 ====================

function autoLayout() {
  const nodes = store.state.nodes
  if (!nodes.length) return

  const inDeg = {},
    adj = {}
  nodes.forEach((n) => {
    inDeg[n.id] = 0
    adj[n.id] = []
  })
  store.state.edges.forEach((e) => {
    if (inDeg[e.target] !== undefined) {
      inDeg[e.target]++
      adj[e.source].push(e.target)
    }
  })
  const queue = nodes.filter((n) => inDeg[n.id] === 0).map((n) => n.id)
  const ordered = []
  while (queue.length) {
    const c = queue.shift()
    ordered.push(c)
    for (const n of adj[c] || []) {
      inDeg[n]--
      if (inDeg[n] === 0) queue.push(n)
    }
  }
  nodes.forEach((n) => {
    if (!ordered.includes(n.id)) ordered.push(n.id)
  })

  const posMap = {}
  ordered.forEach((id, idx) => {
    const col = idx % 4,
      row = Math.floor(idx / 4)
    posMap[id] = { x: 120 + col * 240, y: 80 + row * 140 }
  })

  for (const n of nodes) {
    if (posMap[n.id]) {
      n.x = posMap[n.id].x
      n.y = posMap[n.id].y
    }
  }
  store.markDirty()

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
  if (!nodes.length) {
    ElMessage.warning('画布为空')
    return
  }

  const config = serialize(nodes, edges, {
    version: 1,
    workflowType: 'validation'
  })

  console.group('[ReportRuleConfig] 拓扑校验')
  const analysis = analyzeWorkflowTopology(config)
  console.groupEnd()

  const { validation, sortResult, paths, unreachable, summary } = analysis

  const nodeTitleMap = {}
  for (const n of nodes) {
    nodeTitleMap[n.id] = n.title || n.id
  }

  const pathSummary = summarizePaths(paths)
  const pathDetails = paths
    .map((p, idx) => {
      const nodeNames = p.nodes.map((id) => nodeTitleMap[id] || id)
      const branchInfo =
        p.branchDecisions.length > 0
          ? ` (${p.branchDecisions.map((d) => `${nodeTitleMap[d.at] || d.at}:${d.choice === 'success' ? '✓' : '✗'}`).join(', ')})`
          : ''
      return `  路径${idx + 1}: ${nodeNames.join(' → ')}${branchInfo}`
    })
    .join('\n')

  if (!validation.valid) {
    const errorMessages = validation.errors.map((e) => `  ✗ ${e.message}`).join('\n')
    const warnMessages =
      validation.warnings.length > 0
        ? '\n\n' + validation.warnings.map((w) => `  ⚠ ${w.message}`).join('\n')
        : ''

    const pathInfo =
      paths.length > 0 ? `\n\n📋 拓扑路径 (${pathSummary}):\n${pathDetails}` : '\n\n📋 无有效路径'

    ElMessageBox.alert(
      `校验失败！发现 ${validation.errors.length} 个错误：\n\n${errorMessages}${warnMessages}${pathInfo}`,
      '拓扑校验结果',
      { type: 'error', confirmButtonText: '确定' }
    )
    return false
  }

  if (validation.warnings.length > 0) {
    const warnMessages = validation.warnings.map((w) => `  ⚠ ${w.message}`).join('\n')
    const pathInfo =
      paths.length > 0 ? `\n\n📋 拓扑路径 (${pathSummary}):\n${pathDetails}` : '\n\n📋 无有效路径'

    ElMessageBox.alert(`校验通过（有警告）：\n\n${warnMessages}${pathInfo}`, '拓扑校验结果', {
      type: 'warning',
      confirmButtonText: '确定'
    })
    return true
  }

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
  if (!currentSection.value) {
    ElMessage.warning('请先选择报告章节')
    return
  }
  if (!store.state.nodes.length) {
    ElMessage.warning('画布为空，请添加节点')
    return
  }

  const config = serialize(store.state.nodes, store.state.edges, {
    version: 1,
    workflowType: 'validation'
  })
  const layout = extractLayout(store.state.nodes)

  try {
    await ElMessageBox.confirm(
      `保存工作流到章节「${currentSection.value.sectionName}」？`,
      '保存确认',
      { type: 'info' }
    )
    const savePayload = {
      code: currentSection.value.code || currentSection.value.Code,
      workflowConfig: JSON.stringify(config),
      layoutJson: JSON.stringify(layout)
    }
    const res = await yzhApi.post('/api/report-definition/section/save', savePayload)
    if (res?.success !== false) {
      savedTip.value = `${new Date().toLocaleTimeString()} 已保存`
      currentSection.value.workflowConfig = JSON.stringify(config)
      currentSection.value.layoutJson = JSON.stringify(layout)
      store.markClean()
      ElMessage.success('工作流保存成功')
    } else ElMessage.error(res?.message || '保存失败')
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('保存失败')
  }
}
</script>

<style scoped lang="less">
:deep(.admin-layout__content) {
  padding: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.studio-layout { display: flex; flex-direction: column; flex: 1; min-height: 0; background: #f1f5f9; }
.page-header { display: flex; align-items: center; justify-content: space-between; padding: 12px 20px; background: #fff; border-bottom: 1px solid #e2e8f0; flex-shrink: 0; }
.header-left { display: flex; align-items: center; gap: 8px; }
.header-title { font-size: 16px; font-weight: 700; color: #1e293b; }
.page-body { display: flex; flex: 1; min-height: 0; }
.left-panel { width: 280px; background: #fff; border-right: 1px solid #e2e8f0; display: flex; flex-direction: column; flex-shrink: 0; }
.panel-header { display: flex; align-items: center; justify-content: space-between; padding: 12px 16px; font-size: 13px; font-weight: 700; color: #334155; border-bottom: 1px solid #f1f5f9; }
.tree-search { padding: 8px 12px; border-bottom: 1px solid #f1f5f9; }
.tree-container { flex: 1; overflow-y: auto; padding: 8px; }
.main-content { flex: 1; display: flex; flex-direction: column; min-width: 0; }
.canvas-toolbar { display: flex; align-items: center; justify-content: space-between; padding: 8px 16px; background: #fff; border-bottom: 1px solid #e2e8f0; }
.canvas-title { font-size: 13px; font-weight: 600; color: #475569; }
.toolbar-actions { display: flex; gap: 8px; }
.canvas-container { flex: 1; min-height: 0; background: #f8fafc; }
.canvas-footer { padding: 6px 16px; font-size: 12px; color: #94a3b8; background: #fff; border-top: 1px solid #e2e8f0; }
.right-panel { width: 360px; background: #fff; border-left: 1px solid #e2e8f0; display: flex; flex-direction: column; flex-shrink: 0; }
.studio-tabs { height: 100%; display: flex; flex-direction: column; }
:deep(.el-tabs__header) { margin: 0; padding: 0 16px; background: #fafbfc; border-bottom: 1px solid #e2e8f0; }
:deep(.el-tabs__content) { flex: 1; overflow: hidden; }
:deep(.el-tab-pane) { height: 100%; overflow-y: auto; }
.nodes-panel, .prop-panel-wrapper { height: 100%; }

.tree-node {
  display: flex;
  align-items: center;
  padding: 6px 8px;
  cursor: pointer;
  border-radius: 4px;
  transition: background 0.15s;
  &:hover { background: #f1f5f9; }
  &.active { background: #eff6ff; color: #2563eb; }
}
.tree-node .toggle-icon { margin-right: 4px; transition: transform 0.2s; }
.tree-node.expanded > .toggle-icon { transform: rotate(90deg); }
.tree-node .type-icon { margin-right: 6px; font-size: 16px; }
.tree-node .type-icon.org { color: #3b82f6; }
.tree-node .type-icon.standard { color: #8b5cf6; }
.tree-node .type-icon.phase { color: #f59e0b; }
.tree-node .type-icon.rule { color: #94a3b8; }
.tree-node .type-icon.rule.configured { color: #10b981; }
.tree-node .tree-label {
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.tree-node .type-icon.configured {
  color: #10b981 !important;
}
.level-1 { padding-left: 20px; }
.level-2 { padding-left: 40px; }
.level-3 { padding-left: 60px; }

.rule-code-text {
  font-family: 'JetBrains Mono', monospace;
  background: #f1f5f9;
  padding: 2px 6px;
  border-radius: 2px;
  color: #64748b;
}
</style>
