<!--
  WorkflowDesigner.vue — 通用工作流设计器组件

  Props:
    - title: 页面标题（如 "NC 规则配置" / "报告内容设计"）
    - treeConfig: 树加载配置
    - saveConfig: 保存配置
    - workflowType: 'validation' | 'report'
    - executeConfig: 执行验证配置（enabled=false 时不显示运行按钮）

  Emits:
    - save-success: 保存成功后触发
    - execute-success: 执行成功后触发（含结果数据）
-->
<template>
  <div class="workflow-designer-page studio-layout">
    <div class="page-header">
      <div class="header-left">
        <el-icon><Setting /></el-icon>
        <span class="header-title">{{ title }}</span>
      </div>
    </div>

    <div class="page-body">
      <!-- ===== 左栏：统一4级树（可折叠） ===== -->
      <aside class="left-panel" v-show="leftPanelVisible">
        <div class="panel-header">
          <span>{{ treeLabel }}</span>
          <el-button link size="small" @click="refreshTree"><el-icon><Refresh /></el-icon></el-button>
        </div>
        <div class="tree-search">
          <el-input v-model="searchText" placeholder="搜索..." size="small" clearable :prefix-icon="Search" />
        </div>
        <div class="tree-container">
          <template v-for="org in treeData" :key="org.id">
            <div v-if="org.visible" class="tree-group">
              <div class="tree-node level-0" :class="{ expanded: org.expanded }" @click="toggleExpand(org)">
                <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                <el-icon class="type-icon org"><OfficeBuilding /></el-icon>
                <span class="tree-label">{{ org.label }}</span>
                <el-badge v-if="org.children?.length" :value="org.children.length" type="info" />
              </div>
              <template v-if="org.expanded && org.children">
                <template v-for="std in org.children" :key="std.id">
                  <div class="tree-node level-1" :class="{ expanded: std.expanded }" @click="toggleExpand(std)">
                    <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                    <el-icon class="type-icon standard"><Document /></el-icon>
                    <span class="tree-label">{{ std.label }}</span>
                    <el-badge v-if="std.children?.length" :value="std.children.length" type="info" />
                  </div>
                  <template v-if="std.expanded && std.children && std.visible">
                    <template v-for="phase in std.children" :key="phase.id">
                      <div class="tree-node level-2" :class="{ expanded: phase.expanded }" @click="togglePhase(phase, std, org)">
                        <el-icon class="toggle-icon"><ArrowRight /></el-icon>
                        <el-icon class="type-icon phase"><Calendar /></el-icon>
                        <span class="tree-label">{{ phase.label }}</span>
                        <el-badge v-if="phase.children?.length" :value="phase.children.length" type="info" />
                      </div>
                      <div v-if="phase.visible && phase.expanded && phase.leafLoaded">
                        <div
                          v-for="leaf in phase.children"
                          :key="leaf.id"
                          class="tree-node level-3"
                          :class="{ active: currentLeaf?.id === leaf.id, configured: !!leaf.workflowConfig }"
                          @click="selectLeaf(leaf, phase)"
                        >
                          <el-icon class="type-icon rule" :class="{ configured: !!leaf.workflowConfig }">
                            <CircleCheck v-if="leaf.workflowConfig" /><Document v-else />
                          </el-icon>
                          <span class="tree-label">{{ leaf._label || leaf[treeConfig.textField] || leaf.label }}</span>
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

      <div class="panel-toggle left-toggle" @click="toggleLeftPanel" :title="leftPanelVisible ? '收起规则树' : '展开规则树'">
        <el-icon><ArrowRight /></el-icon>
      </div>

      <main class="main-content">
        <div class="canvas-toolbar">
          <span class="canvas-title">{{ currentLeaf ? `工作流：${currentLeaf[treeConfig.textField] || currentLeaf.label || '未命名'}` : '请选择项目' }}</span>
          <div class="toolbar-actions">
            <el-button size="small" @click="autoLayout"><el-icon><Grid /></el-icon> 布局</el-button>
            <el-button size="small" type="danger" plain @click="handleClearCanvas"><el-icon><Delete /></el-icon> 清空</el-button>
            <el-button size="small" @click="validateGraph"><el-icon><CircleCheck /></el-icon> 校验</el-button>
            <el-button v-if="executeConfig.enabled" type="success" size="small" :loading="executing" :disabled="!currentLeaf" @click="handleExecuteTest">
              <el-icon><VideoPlay /></el-icon> 运行
            </el-button>
            <el-button type="primary" size="small" :disabled="!currentLeaf || !store.state.dirty" @click="handleSave">
              <el-icon><Download /></el-icon> 保存
            </el-button>
          </div>
        </div>
        <ExecutionResultPanel v-if="executionResult" :result="executionResult" @close="executionResult = null" />
        <div ref="canvasRef" class="canvas-container" @dragover.prevent="onCanvasDragOver" @drop.prevent="onCanvasDrop"></div>
        <div class="canvas-footer">
          <span>节点: {{ store.state.nodes.length }} | 边: {{ store.state.edges.length }} | 状态: {{ store.state.dirty ? '未保存' : '已保存' }}</span>
          <span v-if="currentLeaf" class="rule-code-text">{{ currentLeaf[treeConfig.codeField] || currentLeaf.code || currentLeaf.ruleCode }}</span>
        </div>
      </main>

      <div class="panel-toggle right-toggle" @click="toggleRightPanel" :title="rightPanelVisible ? '收起节点库/属性' : '展开节点库/属性'">
        <el-icon><ArrowRight /></el-icon>
      </div>

      <aside class="right-panel" v-show="rightPanelVisible">
        <el-tabs v-model="activeRightTab" class="studio-tabs">
          <el-tab-pane label="节点库" name="nodes">
            <div class="nodes-panel"><SkillPanel :skills="skills" :categories="categories" @add-node="handleAddNode" /></div>
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

<script setup lang="ts">
import { yzhApi } from '@yzh-core/api/client'
import ExecutionResultPanel from './ExecutionResultPanel.vue'
import NodePropertyForm from './NodePropertyForm.vue'
import SkillPanel from './SkillPanel.vue'
import { analyzeWorkflowTopology, nodeStyle, setLogEnabled, setLogLevel } from '@share/composables/workflow/compiler'
import { deserialize, extractLayout, serialize } from '@share/composables/workflow/serializer'
import { useWorkflowStore } from '@share/composables/workflow/useWorkflowStore'
import { installLogicFlowPatch } from '@share/utils/logicflow-patch'
import {
  Calendar, CircleCheck, Delete, Document, Download, Grid,
  OfficeBuilding, Refresh, Search, Setting, VideoPlay, ArrowRight
} from '@element-plus/icons-vue'
import LogicFlow from '@logicflow/core'
import '@logicflow/core/dist/index.css'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'

installLogicFlowPatch()
setLogEnabled(true)
setLogLevel('INFO')

// ==================== Props ====================
interface TreeConfig {
  loadApi: string
  loadMethod?: 'get' | 'post'
  loadBodyBuilder?: (filter: any) => Record<string, any>
  childrenApi?: string
  textField: string
  codeField: string
}
interface SaveConfig {
  api: string
  getUrl?: (ctx: any) => string
  buildPayload?: (ctx: any) => Record<string, any>
}
interface ExecuteConfig {
  enabled?: boolean
  runApi?: string
}
interface Props {
  title: string
  treeConfig: TreeConfig
  saveConfig: SaveConfig
  workflowType?: string
  executeConfig?: ExecuteConfig
}
const props = withDefaults(defineProps<Props>(), {
  title: '工作流设计',
  workflowType: () => 'validation',
  executeConfig: () => ({ enabled: true, runApi: '/api/Workflow/test/run' })
})
const emit = defineEmits<{
  'save-success': [result: any]
  'execute-success': [result: any]
}>()

// ==================== State ====================
const activeRightTab = ref('nodes')
const canvasRef = ref<HTMLDivElement | null>(null)
const diagram = ref<any>(null)
let _resizeObserver: ResizeObserver | null = null
const leftPanelVisible = ref(true)
const rightPanelVisible = ref(true)
const store = useWorkflowStore() as any
const treeData = ref<any[]>([])
const searchText = ref('')
const currentLeaf = ref<any>(null)
const currentFilter = reactive({ orgCode: '', standardCode: '', phaseCode: '' })
const skills = ref<any[]>([])
const categories = ref<any[]>([])
const selectedNode = ref<any>(null)
const forceRefreshTick = ref(0)
const docRules = ref<any[]>([])
const currentDocFields = ref<any[]>([])
const currentDocTables = ref<any[]>([])
const savedTip = ref('')
const executing = ref(false)
const executionResult = ref<any>(null)
const selectedEdgeId = ref<string | null>(null)
const _renamingNodeId = ref<string | null>(null)
const _renamingTimestamp = ref(0)
const RENAMING_GUARD_MS = 1000

const treeLabel = computed(() => {
  const field = props.treeConfig.textField
  return field === 'SectionName' ? '机构 / 标准 / 阶段 / 报告章节' : '机构 / 标准 / 阶段 / NC检查项'
})
const canvasNodesForPanel = computed(() =>
  store.state.nodes.map((n: any) => ({ id: n.id, title: n.title || n.id, text: n.title || n.id, nodeType: n.nodeType }))
)

// ==================== Lifecycle ====================
onMounted(async () => {
  await Promise.all([loadSkills(), loadCategories(), loadTree(), loadDocRules()])
  await nextTick()
  initDiagram()
})
onBeforeUnmount(() => {
  document.removeEventListener('keydown', handleKeyDown)
  _resizeObserver?.disconnect()
  diagram.value?.clearData?.()
  diagram.value = null
})

// ==================== Data Loading ====================
async function loadSkills() {
  try {
    const res = await yzhApi.post('/api/Workflow/WfSkill/filter', { Page: 1, PageSize: 200, Filters: [] })
    const items = res?.data?.Items || res?.data?.items || []
    skills.value = items.map((s: any) => ({ ...s, skillCode: s.Code || s.skillCode, skillName: s.Name || s.skillName, category: s.CategoryCode || s.category || '_default', skillType: s.SkillType || s.skillType || 'manual' }))
  } catch {}
}
async function loadCategories() {
  try {
    const res = await yzhApi.post('/api/Workflow/WfSkillCategory/filter', { Page: 1, PageSize: 200, Filters: [] })
    const items = res?.data?.Items || res?.data?.items || []
    categories.value = items.map((c: any) => ({ ...c, categoryCode: c.Code || c.categoryCode, categoryName: c.Name || c.categoryName, color: c.Color || c.color || '#409EFF', sortOrder: c.SortOrder ?? c.sortOrder ?? 99 }))
  } catch {}
}
async function loadTree() {
  try {
    const res = await yzhApi.get('/api/Workflow/StandardDirectory/organization-tree')
    const raw = res?.data?.Data || res?.data || res?.Data || []
    treeData.value = (raw as any[]).map((org: any) => ({
      ...org, expanded: true, visible: true,
      children: (org.children || []).map((std: any) => ({
        ...std, expanded: false, visible: true,
        children: (std.children || []).map((phase: any) => ({ ...phase, expanded: false, visible: true, children: [], leafLoading: false, leafLoaded: false }))
      }))
    }))
    applySearchFilter()
  } catch {}
}
async function loadDocRules() {
  try {
    const res = await yzhApi.get('/api/Workflow/DocExtractionRule/configured-rules')
    docRules.value = res?.data || res?.Data || []
  } catch {}
}
async function loadFieldsAndTables(ruleCode: string) {
  if (!ruleCode) return
  try {
    const res = await yzhApi.get(`/api/Workflow/DocExtractionRule/${ruleCode}/fields-tables`)
    const d = res?.data || res?.Data
    if (d) {
      currentDocFields.value = d.fields || d.Fields || []
      currentDocTables.value = d.tables || d.Tables || []
    }
  } catch {}
}

// ==================== Tree Operations ====================
function applySearchFilter() {
  const kw = searchText.value?.toLowerCase() || ''
  if (!kw) {
    treeData.value.forEach((org: any) => {
      org.visible = true
      ;(org.children || []).forEach((std: any) => { std.visible = true; (std.children || []).forEach((p: any) => { p.visible = true }) })
    })
    return
  }
  treeData.value.forEach((org: any) => {
    let orgHasMatch = org.label?.toLowerCase().includes(kw)
    ;(org.children || []).forEach((std: any) => {
      let stdHasMatch = std.label?.toLowerCase().includes(kw)
      ;(std.children || []).forEach((p: any) => {
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
function toggleExpand(node: any) { node.expanded = !node.expanded }
function toggleLeftPanel() { leftPanelVisible.value = !leftPanelVisible.value }
function toggleRightPanel() { rightPanelVisible.value = !rightPanelVisible.value }
async function togglePhase(phase: any, std: any, org: any) {
  phase.expanded = !phase.expanded
  Object.assign(currentFilter, { orgCode: org.cbCode || org.id, standardCode: std.stdCode || phase.stdCode || std.standardCode, phaseCode: phase.phaseCode })
  currentLeaf.value = null
  selectedNode.value = null
  clearCanvas()
  if (phase.expanded && !phase.leafLoaded) await loadLeavesForPhase(phase)
}
async function loadLeavesForPhase(phase: any) {
  phase.leafLoading = true
  try {
    const filter = currentFilter
    let res: any
    if (props.treeConfig.loadMethod === 'post') {
      const body = props.treeConfig.loadBodyBuilder ? props.treeConfig.loadBodyBuilder(filter) : { Page: 1, PageSize: 200, Filters: [] }
      res = await yzhApi.post(props.treeConfig.loadApi, body)
    } else {
      const params = new URLSearchParams({ orgCode: filter.orgCode, standardCode: filter.standardCode, phaseCode: filter.phaseCode }).toString()
      res = await yzhApi.get(`${props.treeConfig.loadApi}?${params}`)
    }
    const items = res?.data?.Items || res?.data?.items || res?.data || res?.Data || []
    const textField = props.treeConfig.textField
    phase.children = items.map((item: any) => ({ ...item, id: item.Code || item.code || item.RuleCode || item.ruleCode, _label: item[textField] || item.label }))
    phase.leafLoaded = true
  } catch { phase.children = [] } finally { phase.leafLoading = false }
}
const refreshTree = () => { loadTree() }

async function selectLeaf(leaf: any, phase: any) {
  currentLeaf.value = { ...leaf }
  savedTip.value = ''
  currentFilter.orgCode = phase.cbCode || currentFilter.orgCode
  currentFilter.standardCode = phase.stdCode || phase.standardCode || currentFilter.standardCode
  currentFilter.phaseCode = phase.phaseCode || currentFilter.phaseCode
  const code = leaf.Code || leaf.code || leaf.RuleCode || leaf.ruleCode
  if (props.treeConfig.childrenApi && code) {
    try {
      const res = await yzhApi.get(`${props.treeConfig.childrenApi}/${code}`)
      const d = res?.data || res?.Data || {}
      Object.assign(leaf, d)
      currentLeaf.value = leaf
      renderWorkflow(d.workflowConfig || d.WorkflowConfig || d.ruleJson || d.RuleJson, d.layoutJson || d.LayoutJson || null)
      return
    } catch {}
  }
  renderWorkflow(leaf.workflowConfig || leaf.WorkflowConfig || leaf.ruleJson || leaf.RuleJson, leaf.layoutJson || leaf.LayoutJson || null)
}

// ==================== Diagram Initialization ====================
function initDiagram() {
  const ensureSizeThenInit = (attempt = 0) => {
    const el = canvasRef.value
    if (el && el.clientWidth > 50 && el.clientHeight > 50) { createLogicFlowInstance(); return }
    if (attempt >= 20) { console.warn('[WorkflowDesigner] 画布容器尺寸异常'); createLogicFlowInstance(); return }
    setTimeout(() => ensureSizeThenInit(attempt + 1), 100)
  }
  ensureSizeThenInit()
}
function createLogicFlowInstance() {
  const container = canvasRef.value
  if (!container) return
  diagram.value = new LogicFlow({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    container: container as any, grid: { size: 20, visible: true, type: 'mesh' }, background: { backgroundColor: '#fafbfc' },
    edgeType: 'polyline', allowResize: true, allowRotate: false, isSilentMode: false,
    stopScrollGraph: false, stopZoomGraph: false, stopMoveGraph: false, snapline: false,
    keyboard: { enabled: false }, textEditMode: false
  })
  try { diagram.value.render({ nodes: [], edges: [] }) } catch (e) { console.warn('[WorkflowDesigner] 初始空图渲染失败:', e) }

  diagram.value.on('node:click', ({ data }: any) => {
    const props_ = data.properties || {}
    const storeNode = store.getNodeById(data.id)
    const { inputs: syncedInputs, inputTypes: syncedInputTypes } = computeNodeInputsFromEdges(data.id, storeNode?.inputs || props_.inputs || {}, storeNode?.inputTypes || props_.inputTypes || {})
    if (JSON.stringify(syncedInputs) !== JSON.stringify(storeNode?.inputs || {}) || JSON.stringify(syncedInputTypes) !== JSON.stringify(storeNode?.inputTypes || {})) {
      store.setInputValue(data.id, null, null)
      if (storeNode) { storeNode.inputs = syncedInputs; storeNode.inputTypes = syncedInputTypes }
    }
    const nodeData: any = {
      nodeId: data.id, nodeType: props_.nodeType || 'skill', classCode: props_.classCode || props_.nodeType || 'skill',
      title: storeNode?.title || props_.title || data.text || '', skillCode: props_.skillCode || '',
      config: storeNode?.config || props_.config || {}, inputs: syncedInputs, inputTypes: syncedInputTypes,
      outputs: storeNode?.outputs || props_.outputs || {}, inputPorts: storeNode?.inputPorts || props_.inputPorts || [],
      outputPorts: storeNode?.outputPorts || props_.outputPorts || []
    }
    if (nodeData.nodeType === 'branch') {
      const gd = diagram.value?.getGraphData()
      const outEdges = (gd?.edges || []).filter((e: any) => e.sourceNodeId === data.id)
      nodeData.branchEdges = outEdges.map((e: any) => ({ handle: e.properties?.sourceHandle || '', targetId: e.targetNodeId, edgeId: e.id }))
    }
    selectedNode.value = nodeData
    activeRightTab.value = 'props'
  })

  diagram.value.on('node:dbclick', ({ data }: any) => {
    const nodeType = data.properties?.nodeType || data.properties?.classCode
    if (nodeType === 'start') return
    promptEditNodeName(data.id, data.properties?.title || data.text || '')
  })

  diagram.value.on('edge:click', ({ data }: any) => {
    if (_renamingNodeId.value) return
    selectedNode.value = null
    selectedEdgeId.value = data.id
  })
  diagram.value.on('blank:click', () => {
    if (_renamingNodeId.value && Date.now() - _renamingTimestamp.value < RENAMING_GUARD_MS) return
    selectedNode.value = null
    selectedEdgeId.value = null
  })
  diagram.value.on('edge:add', (_event: any) => { autoSetBranchHandle(_event.data); onEdgeChange() })
  diagram.value.on('edge:delete', (_event: any) => { onEdgeChange() })
  document.addEventListener('keydown', handleKeyDown)

  _resizeObserver = new ResizeObserver(() => {
    if (diagram.value && canvasRef.value) {
      const { clientWidth, clientHeight } = canvasRef.value
      if (clientWidth > 0 && clientHeight > 0) diagram.value.resize(clientWidth, clientHeight)
    }
  })
  if (canvasRef.value) _resizeObserver.observe(canvasRef.value)

  nextTick(() => {
    if (diagram.value && canvasRef.value) {
      const { clientWidth, clientHeight } = canvasRef.value
      if (clientWidth > 0 && clientHeight > 0) diagram.value.resize(clientWidth, clientHeight)
    }
  })
}

function handleKeyDown(e: KeyboardEvent) {
  const target = e.target as HTMLElement | null
  if (target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA') return
  if (e.key !== 'Delete' && e.key !== 'Backspace') return
  e.preventDefault()
  if (selectedEdgeId.value && diagram.value) { diagram.value.deleteEdge(selectedEdgeId.value); selectedEdgeId.value = null; return }
  if (selectedNode.value?.nodeId && diagram.value) handleDeleteNode(selectedNode.value.nodeId)
}

function computeNodeInputsFromEdges(nodeId: string, currentInputs: any = {}, currentInputTypes: any = {}) {
  if (!diagram.value) return { inputs: currentInputs, inputTypes: currentInputTypes }
  const gd = diagram.value.getGraphData()
  const inEdges = (gd.edges || []).filter((e: any) => e.targetNodeId === nodeId)
  const nodeProps = gd.nodes.find((n: any) => n.id === nodeId)?.properties || {}
  const inputPorts = nodeProps.inputPorts || []
  const newInputs: any = {}
  const newTypes: any = {}
  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const edge = inEdges.find((e: any) => { const h = e.properties?.targetHandle; return !h || h === port.name })
    if (edge) {
      if (currentInputTypes[port.name] === 'constant' && currentInputs[port.name]) continue
      newInputs[port.name] = edge.sourceNodeId
      newTypes[port.name] = 'link'
    }
  }
  const merged = { ...currentInputs, ...newInputs }
  const mergedTypes = { ...currentInputTypes, ...newTypes }
  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const val = merged[port.name]
    if (val && gd.nodes.some((n: any) => n.id === val)) {
      const stillConnected = inEdges.some((e: any) => e.sourceNodeId === val)
      if (!stillConnected) { delete merged[port.name]; delete mergedTypes[port.name] }
    }
  }
  return { inputs: merged, inputTypes: mergedTypes }
}

function syncSelectedNodeInputs() {
  if (!selectedNode.value || !diagram.value) return
  const nodeId = selectedNode.value.nodeId
  const { inputs: merged, inputTypes: mergedTypes } = computeNodeInputsFromEdges(nodeId, selectedNode.value.inputs || {}, selectedNode.value.inputTypes || {})
  if (JSON.stringify(merged) !== JSON.stringify(selectedNode.value.inputs) || JSON.stringify(mergedTypes) !== JSON.stringify(selectedNode.value.inputTypes || {})) {
    diagram.value.setProperties(nodeId, { inputs: merged, inputTypes: mergedTypes })
    selectedNode.value = { ...selectedNode.value, inputs: merged, inputTypes: mergedTypes }
    store.setInputValue(nodeId, null, null)
    const storeNode = store.getNodeById(nodeId)
    if (storeNode) { storeNode.inputs = merged; storeNode.inputTypes = mergedTypes }
  }
}

function autoSetBranchHandle(edgeData: any) {
  if (!edgeData?.sourceNodeId || !edgeData?.targetNodeId) return
  const gd = diagram.value?.getGraphData()
  if (!gd) return
  const sourceNode = gd.nodes.find((n: any) => n.id === edgeData.sourceNodeId)
  if (!sourceNode) return
  if (sourceNode.properties?.nodeType !== 'branch' && sourceNode.properties?.classCode !== 'branch') return
  if (edgeData.properties?.sourceHandle) return
  const existingEdges = (gd.edges || []).filter((e: any) => e.sourceNodeId === edgeData.sourceNodeId && e.id !== edgeData.id)
  const hasSuccess = existingEdges.some((e: any) => e.properties?.sourceHandle === 'success')
  const handle = hasSuccess ? 'failure' : 'success'
  const label = handle === 'success' ? '成功' : '失败'
  const color = handle === 'success' ? '#67C23A' : '#F56C6C'
  diagram.value.setProperties(edgeData.id, { sourceHandle: handle })
  diagram.value.updateText(edgeData.id, label)
  const edge = gd.edges.find((e: any) => e.id === edgeData.id)
  if (edge) { edge.properties = { ...edge.properties, sourceHandle: handle }; edge.text = label; edge.style = { stroke: color, strokeWidth: 2 } }
}

function onEdgeChange() { syncSelectedNodeInputs(); const gd = diagram.value?.getGraphData(); if (gd) store.state.edges = (gd.edges || []).map((e: any) => ({ id: e.id, source: e.sourceNodeId, target: e.targetNodeId, sourceHandle: e.properties?.sourceHandle || null, targetHandle: e.properties?.targetHandle || null })) }

// ==================== Drag & Drop ====================
function onCanvasDragOver(event: DragEvent) { event.preventDefault(); event.dataTransfer!.dropEffect = 'copy' }
function onCanvasDrop(event: DragEvent) {
  event.preventDefault()
  if (!event.dataTransfer) return
  try {
    const raw = event.dataTransfer.getData('nodeData')
    if (!raw) return
    const item = JSON.parse(raw)
    const container = canvasRef.value
    if (diagram.value && container) {
      const hasSvg = !!container.querySelector('svg')
      if (!hasSvg) { const { clientWidth, clientHeight } = container; if (clientWidth > 0 && clientHeight > 0) diagram.value.resize(clientWidth, clientHeight) }
    }
    if (!diagram.value) { ElMessage.error('画布尚未初始化完成'); return }
    const lfPoint = diagram.value.getPointByClient(event.clientX, event.clientY)
    const pos = lfPoint?.canvasOverlayPosition || lfPoint
    const node = store.addNode(item, pos?.x ?? 200, pos?.y ?? 150)
    if (!node) return
    const category = item.category || skills.value.find((s: any) => s.skillCode === item.skillCode)?.category || ''
    const props_ = { classCode: node.classCode, nodeType: node.nodeType, title: node.title, skillCode: node.skillCode, config: node.config, inputs: node.inputs, inputTypes: node.inputTypes, outputs: node.outputs, inputPorts: node.inputPorts, outputPorts: node.outputPorts }
    if (node.nodeType === 'branch') (props_ as any).points = [[0, -30], [50, 0], [0, 30]]
    diagram.value.addNode({ id: node.id, type: lfShapeType(node.nodeType), x: pos?.x ?? 200, y: pos?.y ?? 150, text: node.title, style: nodeStyle(node.nodeType, node.skillCode, category), properties: props_ })
  } catch {}
}
function lfShapeType(nodeType: string) {
  if (nodeType === 'start' || nodeType === 'end') return 'circle'
  if (nodeType === 'branch') return 'polygon'
  return 'rect'
}

// ==================== Node Operations ====================
function handleAddNode(item: any) {
  const maxX = store.state.nodes.reduce((m: number, n: any) => Math.max(m, n.x || 0), 100)
  const maxY = store.state.nodes.reduce((m: number, n: any) => Math.max(m, n.y || 0), 80)
  const node = store.addNode(item, 120 + (maxX % 600), 80 + (maxY % 400))
  if (!node) return
  const category = item.category || skills.value.find((s: any) => s.skillCode === item.skillCode)?.category || ''
  const addProps = { classCode: node.classCode, nodeType: node.nodeType, title: node.title, skillCode: node.skillCode, config: node.config, inputs: node.inputs, inputTypes: node.inputTypes, outputs: node.outputs, inputPorts: node.inputPorts, outputPorts: node.outputPorts }
  if (node.nodeType === 'branch') (addProps as any).points = [[0, -30], [50, 0], [0, 30]]
  diagram.value.addNode({ id: node.id, type: lfShapeType(node.nodeType), x: node.x, y: node.y, text: node.title, style: nodeStyle(node.nodeType, node.skillCode, category), properties: addProps })
}

function handleUpdateNode(data: any) {
  if (!data.nodeId) return
  const storeNode = store.getNodeById(data.nodeId)
  const latestTitle = storeNode?.title || data.title
  const success = store.updateNode(data.nodeId, { title: latestTitle, classCode: data.classCode || data.nodeType, nodeType: data.nodeType, skillCode: data.skillCode, config: data.config, inputs: data.inputs, inputTypes: data.inputTypes, inputPorts: data.inputPorts, outputPorts: data.outputPorts })
  if (!success) { ElMessage.warning(`节点名称「${latestTitle}」已存在，请使用其他名称`); return }
  diagram.value.updateText(data.nodeId, latestTitle || data.skillCode || '')
  diagram.value.setProperties(data.nodeId, { classCode: data.classCode || data.nodeType, nodeType: data.nodeType, title: latestTitle, skillCode: data.skillCode, config: data.config, inputs: data.inputs, inputTypes: data.inputTypes, inputPorts: data.inputPorts, outputPorts: data.outputPorts })
  nextTick(() => {
    const sn = store.getNodeById(data.nodeId)
    if (sn) {
      let branchEdges: any = undefined
      if (sn.nodeType === 'branch' || sn.classCode === 'branch') {
        const gd = diagram.value?.getGraphData()
        const outEdges = (gd?.edges || []).filter((e: any) => e.sourceNodeId === data.nodeId)
        branchEdges = outEdges.map((e: any) => ({ handle: e.properties?.sourceHandle || '', targetId: e.targetNodeId, edgeId: e.id }))
      }
      selectedNode.value = { nodeId: sn.id, nodeType: sn.nodeType, classCode: sn.classCode, title: sn.title, skillCode: sn.skillCode, config: { ...sn.config }, inputs: { ...sn.inputs }, inputTypes: { ...(sn.inputTypes || {}) }, outputs: { ...sn.outputs }, inputPorts: sn.inputPorts || [], outputPorts: sn.outputPorts || [], branchEdges }
      forceRefreshTick.value++
    }
  })
}

function promptEditNodeName(nodeId: string, currentName: string) {
  _renamingNodeId.value = nodeId
  _renamingTimestamp.value = Date.now()
  ElMessageBox.prompt('请输入节点名称', '编辑节点名称', { inputValue: currentName, inputPattern: /.+/, inputErrorMessage: '名称不能为空', confirmButtonText: '确定', cancelButtonText: '取消' })
    .then(({ value }: any) => {
      const name = (value || '').trim()
      if (!name) { _renamingNodeId.value = null; _renamingTimestamp.value = 0; return }
      if (!store.renameNode(nodeId, name)) { ElMessage.warning(`节点名称「${name}」已存在，请使用其他名称`); _renamingNodeId.value = null; _renamingTimestamp.value = 0; return }
      diagram.value.updateText(nodeId, name)
      diagram.value.setProperties(nodeId, { title: name })
      setTimeout(() => {
        const storeNode = store.getNodeById(nodeId)
        if (storeNode) {
          let branchEdges: any = undefined
          if (storeNode.nodeType === 'branch' || storeNode.classCode === 'branch') {
            const gd = diagram.value?.getGraphData()
            const outEdges = (gd?.edges || []).filter((e: any) => e.sourceNodeId === nodeId)
            branchEdges = outEdges.map((e: any) => ({ handle: e.properties?.sourceHandle || '', targetId: e.targetNodeId, edgeId: e.id }))
          }
          selectedNode.value = { nodeId: storeNode.id, nodeType: storeNode.nodeType, classCode: storeNode.classCode, title: storeNode.title, skillCode: storeNode.skillCode, config: { ...storeNode.config }, inputs: { ...storeNode.inputs }, inputTypes: { ...(storeNode.inputTypes || {}) }, outputs: { ...storeNode.outputs }, inputPorts: storeNode.inputPorts || [], outputPorts: storeNode.outputPorts || [], branchEdges }
          forceRefreshTick.value++
        }
        _renamingNodeId.value = null; _renamingTimestamp.value = 0
      }, 300)
      ElMessage.success('节点名称已更新')
    })
    .catch(() => { _renamingNodeId.value = null; _renamingTimestamp.value = 0 })
}

function handleDeleteNode(nodeId: string) {
  if (!store.removeNode(nodeId)) { ElMessage.warning('开始节点不可删除'); return }
  diagram.value.deleteNode(nodeId)
  selectedNode.value = null
}

// ==================== Edge Operations ====================
function handleLinkNode({ portName, sourceNodeId, targetNodeId, sourceHandle }: any) {
  if (!diagram.value) return
  const gd = diagram.value.getGraphData()
  if (sourceHandle && portName === sourceHandle) {
    const toDelete = (gd.edges || []).filter((e: any) => e.sourceNodeId === sourceNodeId && e.properties?.sourceHandle === sourceHandle)
    for (const existing of toDelete) diagram.value.deleteEdge(existing.id)
    if (selectedEdgeId.value && toDelete.some((e: any) => e.id === selectedEdgeId.value)) selectedEdgeId.value = null
    if (targetNodeId) {
      const color = sourceHandle === 'success' ? '#67C23A' : '#F56C6C'
      const label = sourceHandle === 'success' ? '成功' : '失败'
      diagram.value.addEdge({ id: `e-${sourceNodeId}-${targetNodeId}-${Date.now()}`, type: 'polyline', sourceNodeId, targetNodeId, text: label, style: { stroke: color, strokeWidth: 2 }, properties: { sourceHandle, targetHandle: null } })
    }
    return
  }
  if (!targetNodeId) {
    const toDelete = (gd.edges || []).filter((e: any) => e.targetNodeId === targetNodeId && (e.properties?.targetHandle === portName || (!e.properties?.targetHandle && !portName)))
    for (const existing of toDelete) diagram.value.deleteEdge(existing.id)
    const storeNode = store.getNodeById(targetNodeId)
    if (storeNode && storeNode.inputs) delete storeNode.inputs[portName]
    if (storeNode && storeNode.inputTypes) delete storeNode.inputTypes[portName]
    onEdgeChange()
    return
  }
  const edge = store.connect(sourceNodeId, targetNodeId, null, portName)
  if (!edge) return
  const existingEdges = (gd.edges || []).filter((e: any) => e.targetNodeId === targetNodeId && (e.properties?.targetHandle === portName || (!e.properties?.targetHandle && !portName)))
  for (const existing of existingEdges) diagram.value.deleteEdge(existing.id)
  const sourceNode = gd.nodes.find((n: any) => n.id === sourceNodeId)
  const sourceType = sourceNode?.properties?.nodeType || sourceNode?.properties?.classCode
  let autoHandle: string | null = null
  if (sourceType === 'branch') {
    const branchEdges = (gd.edges || []).filter((e: any) => e.sourceNodeId === sourceNodeId)
    const hasSuccess = branchEdges.some((e: any) => e.properties?.sourceHandle === 'success')
    autoHandle = hasSuccess ? 'failure' : 'success'
  }
  diagram.value.addEdge({ id: edge.id, type: 'polyline', sourceNodeId, targetNodeId, properties: { sourceHandle: autoHandle, targetHandle: portName || null } })
}

// ==================== Canvas Operations ====================
function clearCanvas() {
  if (!diagram.value) return
  try { const gm = diagram.value.graphModel; if (gm) { gm.edges = []; gm.nodes = [] } else { diagram.value.render({ nodes: [], edges: [] }) } } catch {}
  store.clearAll()
}
function handleClearCanvas() {
  if (!store.state.nodes.length) { ElMessage.info('画布已为空'); return }
  ElMessageBox.confirm('确认清空画布上的所有节点和连线？', '清空确认', { type: 'warning' })
    .then(() => { clearCanvas(); selectedNode.value = null; ensureStartNode(); ElMessage.success('画布已清空') })
    .catch(() => {})
}
function ensureStartNode() {
  if (!diagram.value) return
  const hasStart = store.state.nodes.some((n: any) => n.classCode === 'start' || n.nodeType === 'start')
  if (!hasStart) {
    const startItem = { classCode: 'start', className: '开始' }
    const node = store.addNode(startItem, 100, 150)
    if (node) diagram.value.addNode({ id: node.id, type: 'circle', x: 100, y: 150, text: node.title, style: nodeStyle('start'), properties: { classCode: 'start', nodeType: 'start', title: node.title, skillCode: '', config: {}, inputs: {}, outputs: {}, inputPorts: [], outputPorts: [] } })
  }
}
function renderWorkflow(workflowConfig: any, layoutJson: any) {
  if (!workflowConfig) { clearCanvas(); ensureStartNode(); return }
  try {
    const config = typeof workflowConfig === 'string' ? JSON.parse(workflowConfig) : workflowConfig
    const layout = layoutJson ? (typeof layoutJson === 'string' ? JSON.parse(layoutJson) : layoutJson) : null
    const { nodes, edges, idGenerator, migrated } = deserialize(config, layout)
    store.loadFromData(nodes, edges)
    // @ts-ignore - idGenerator needs to be set on state
    store.state.idGenerator = idGenerator
    store.markClean()
    const lfNodes = nodes.map((n: any) => {
      const nodeProps = { classCode: n.classCode, nodeType: n.nodeType, title: n.title, skillCode: n.skillCode, config: n.config, inputs: n.inputs, inputTypes: n.inputTypes || {}, outputs: n.outputs, inputPorts: n.inputPorts, outputPorts: n.outputPorts }
      if (n.nodeType === 'branch') (nodeProps as any).points = [[0, -30], [50, 0], [0, 30]]
      return { id: n.id, type: lfShapeType(n.nodeType), x: n.x, y: n.y, text: n.title || n.skillCode || n.id, style: nodeStyle(n.nodeType, n.skillCode), properties: nodeProps }
    })
    const lfEdges = edges.map((e: any) => {
      const isBranchAnchor = e.sourceHandle === 'success' || e.sourceHandle === 'failure'
      return { id: e.id, type: 'polyline', sourceNodeId: e.source, targetNodeId: e.target, text: isBranchAnchor ? (e.sourceHandle === 'success' ? '成功' : '失败') : '', style: isBranchAnchor ? { stroke: e.sourceHandle === 'success' ? '#67C23A' : '#F56C6C', strokeWidth: 2 } : { stroke: '#5B8FF9', strokeWidth: 2 }, properties: { sourceHandle: e.sourceHandle || null, targetHandle: e.targetHandle || null } }
    })
    diagram.value.render({ nodes: lfNodes, edges: lfEdges })
    if (migrated && !sessionStorage.getItem('_wf_migration_tip_shown')) { ElMessage.info('旧格式节点 ID 已自动迁移重编号'); sessionStorage.setItem('_wf_migration_tip_shown', '1') }
  } catch (e) { ElMessage.error('工作流配置解析失败') }
}

// ==================== Layout / Validate / Save ====================
function autoLayout() {
  const nodes = store.state.nodes
  if (!nodes.length) return
  const inDeg: any = {}, adj: any = {}
  nodes.forEach((n: any) => { inDeg[n.id] = 0; adj[n.id] = [] })
  store.state.edges.forEach((e: any) => { if (inDeg[e.target] !== undefined) { inDeg[e.target]++; adj[e.source].push(e.target) } })
  const queue = nodes.filter((n: any) => inDeg[n.id] === 0).map((n: any) => n.id)
  const ordered: string[] = []
  while (queue.length) { const c = queue.shift(); ordered.push(c!); for (const n of adj[c] || []) { inDeg[n]--; if (inDeg[n] === 0) queue.push(n) } }
  nodes.forEach((n: any) => { if (!ordered.includes(n.id)) ordered.push(n.id) })
  const posMap: any = {}
  ordered.forEach((id: string, idx: number) => { posMap[id] = { x: 120 + (idx % 4) * 240, y: 80 + Math.floor(idx / 4) * 140 } })
  for (const n of nodes) { if (posMap[n.id]) { n.x = posMap[n.id].x; n.y = posMap[n.id].y } }
  store.markDirty()
  for (const n of nodes) { if (posMap[n.id]) diagram.value.setProperties(n.id, { x: posMap[n.id].x, y: posMap[n.id].y }) }
  ElMessage.success('自动布局完成')
}

function validateGraph() {
  const nodes = store.state.nodes
  const edges = store.state.edges
  if (!nodes.length) { ElMessage.warning('画布为空'); return }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const config = serialize(nodes, edges, { version: 1 as any, workflowType: props.workflowType } as any) as any
  const analysis = analyzeWorkflowTopology(config)
  const { validation, paths, summary } = analysis
  const nodeTitleMap: any = {}
  for (const n of nodes) nodeTitleMap[n.id] = n.title || n.id
  const pathDetails = paths.map((p: any, idx: number) => {
    const nodeNames = p.nodes.map((id: string) => nodeTitleMap[id] || id)
    const branchInfo = p.branchDecisions.length > 0 ? ` (${p.branchDecisions.map((d: any) => `${nodeTitleMap[d.at] || d.at}:${d.choice === 'success' ? '✓' : '✗'}`).join(', ')})` : ''
    return `  路径${idx + 1}: ${nodeNames.join(' → ')}${branchInfo}`
  }).join('\n')
  const pathInfo = paths.length > 0 ? `\n\n📋 拓扑路径 (${summary.totalPaths}): ${pathDetails}` : '\n\n📋 无有效路径'
  if (!validation.valid) {
    const errorMessages = validation.errors.map((e: any) => `  ✗ ${e.message}`).join('\n')
    const warnMessages = validation.warnings.length > 0 ? '\n\n' + validation.warnings.map((w: any) => `  ⚠ ${w.message}`).join('\n') : ''
    ElMessageBox.alert(`校验失败！发现 ${validation.errors.length} 个错误：\n\n${errorMessages}${warnMessages}${pathInfo}`, '拓扑校验结果', { type: 'error', confirmButtonText: '确定' })
    return false
  }
  if (validation.warnings.length > 0) {
    const warnMessages = validation.warnings.map((w: any) => `  ⚠ ${w.message}`).join('\n')
    ElMessageBox.alert(`校验通过（有警告）：\n\n${warnMessages}${pathInfo}`, '拓扑校验结果', { type: 'warning', confirmButtonText: '确定' })
    return true
  }
  ElMessageBox.alert(`✓ 校验通过！\n\n节点: ${summary.totalNodes} | 边: ${summary.totalEdges} | 路径: ${summary.totalPaths}${pathInfo}`, '拓扑校验结果', { type: 'success', confirmButtonText: '确定' })
  return true
}

async function handleSave() {
  if (!currentLeaf.value) { ElMessage.warning('请先选择项目'); return }
  if (!store.state.nodes.length) { ElMessage.warning('画布为空，请添加节点'); return }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const config = serialize(store.state.nodes, store.state.edges, { version: 1 as any, workflowType: props.workflowType } as any) as any
  const layout = extractLayout(store.state.nodes)
  try {
    await ElMessageBox.confirm(`保存工作流到「${currentLeaf.value[props.treeConfig.textField] || currentLeaf.value.label}」？`, '保存确认', { type: 'info' })
    const ctx = { leaf: currentLeaf.value, filter: currentFilter, config, layout }
    const url = props.saveConfig.getUrl ? props.saveConfig.getUrl(ctx) : props.saveConfig.api
    const payload = props.saveConfig.buildPayload ? props.saveConfig.buildPayload(ctx) : { code: currentLeaf.value.Code || currentLeaf.value.code, workflowConfig: JSON.stringify(config), layoutJson: JSON.stringify(layout) }
    const res = await yzhApi.post(url, payload)
    if (res?.success !== false) {
      savedTip.value = `${new Date().toLocaleTimeString()} 已保存`
      currentLeaf.value.workflowConfig = JSON.stringify(config)
      currentLeaf.value.layoutJson = JSON.stringify(layout)
      store.markClean()
      emit('save-success', res)
      ElMessage.success('工作流保存成功')
    } else ElMessage.error(res?.message || '保存失败')
  } catch (e: any) { if (e?.message !== 'cancel') ElMessage.error('保存失败') }
}

// ==================== Execution ====================
async function handleExecuteTest() {
  if (!currentLeaf.value) { ElMessage.warning('请先选择项目'); return }
  if (!store.state.nodes.length) { ElMessage.warning('画布为空，请添加节点'); return }
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const config = serialize(store.state.nodes, store.state.edges, { version: 1 as any, workflowType: props.workflowType } as any) as any
  const analysis = analyzeWorkflowTopology(config)
  if (!analysis.validation.valid) {
    const errorMessages = analysis.validation.errors.map((e: any) => `  ✗ ${e.message}`).join('\n')
    ElMessageBox.alert(`拓扑校验失败，请先修复以下问题后再执行验证：\n\n${errorMessages}`, '执行验证 - 拓扑校验失败', { type: 'error', confirmButtonText: '确定' })
    return
  }
  executing.value = true
  executionResult.value = null
  try {
    const runApi = props.executeConfig?.runApi || '/api/Workflow/test/run'
    const res = await yzhApi.post(runApi, {
      taskType: 'TEST',
      ruleCode: currentLeaf.value.Code || currentLeaf.value.code || currentLeaf.value.RuleCode || currentLeaf.value.ruleCode,
      enterpriseCode: currentLeaf.value.enterpriseCode || 'YZH-STD-ENT',
      standardCode: currentLeaf.value.standardCode || currentFilter.standardCode,
      phaseCode: currentLeaf.value.phaseCode || currentFilter.phaseCode,
      configJson: JSON.stringify(config)
    })
    if (res?.success !== false && res?.data) {
      executionResult.value = res.data
      if (res.data.status === 'completed' && res.data.isSuccess) {
        ElMessage.success(`执行完成：${res.data.status} (${res.data.durationMs}ms)`)
        emit('execute-success', res.data)
      } else {
        const errorMsg = res.data.ncResult?.error || res.data.status || '执行失败'
        ElMessageBox.alert(`工作流执行失败：\n\n${errorMsg}`, '执行验证 - 执行失败', { type: 'error', confirmButtonText: '确定' })
      }
    } else {
      const errorMsg = res?.error || res?.message || '执行失败'
      ElMessageBox.alert(`工作流执行失败：\n\n${errorMsg}`, '执行验证 - 执行失败', { type: 'error', confirmButtonText: '确定' })
    }
  } catch (e: any) { ElMessage.error('执行验证失败: ' + (e?.message || e)) }
  finally { executing.value = false }
}

// ==================== Node Test ====================
async function handleTestNode(nodeData: any) {
  try {
    if (nodeData.nodeType === 'ai_node') {
      const mockOutputs: any = {}
      const nodes = store.state.nodes
      const customParams = nodeData.config?.customParams || []
      let parsedCustomParams = customParams
      if (typeof customParams === 'string') { try { parsedCustomParams = JSON.parse(customParams) } catch { parsedCustomParams = [] } }
      for (const param of parsedCustomParams) {
        if (param?.sourceType === 'link' && param?.sourceConfig?.nodeId) {
          const sourceNode = nodes.find((n: any) => n.id === param.sourceConfig.nodeId)
          if (sourceNode) mockOutputs[param.sourceConfig.nodeId] = { result: `[${sourceNode.title || param.sourceConfig.nodeId}]`, success: true }
        }
      }
      let ruleJson = ''
      try { ruleJson = JSON.stringify(serialize(nodes, store.state.edges, { version: 1, workflowType: props.workflowType })) } catch { ruleJson = '' }
      const testBody = {
        nodeId: nodeData.nodeId, nodeType: 'ai_node', title: nodeData.title,
        config: { ...nodeData.config, customParams: JSON.stringify(parsedCustomParams) },
        inputs: nodeData.inputs || {}, inputTypes: nodeData.inputTypes || {},
        inputPorts: nodeData.inputPorts || [], outputPorts: nodeData.outputPorts || [],
        workflowContext: {
          ruleJson,
          contextParams: currentLeaf.value ? { enterpriseCode: currentLeaf.value.enterpriseCode || 'YZH-STD-ENT', standardCode: currentLeaf.value.standardCode, phaseCode: currentLeaf.value.phaseCode } : {},
          mockOutputs
        }
      }
      const res = await yzhApi.post('/api/Workflow/test/ai-node', testBody)
      if (res?.success && res.data) nodeData.onSuccess(res.data)
      else nodeData.onError(res?.error || 'AI 节点测试失败', null)
      return
    }
    const res = await yzhApi.post('/api/Workflow/test/node', {
      nodeId: nodeData.nodeId, nodeType: nodeData.nodeType, title: nodeData.title,
      skillCode: nodeData.skillCode, config: nodeData.config, inputs: nodeData.inputs,
      inputTypes: nodeData.inputTypes, inputPorts: nodeData.inputPorts, outputPorts: nodeData.outputPorts
    })
    if (res?.success && res.data) nodeData.onSuccess(res.data)
    else nodeData.onError(res?.error || '测试失败', null)
  } catch (e: any) { nodeData.onError('请求失败: ' + (e?.message || e), null) }
}

async function handleTestWorkflow(nodeData: any) {
  if (!currentLeaf.value) { nodeData.onError('请先选择项目', null); return }
  if (!store.state.nodes.length) { nodeData.onError('画布为空', null); return }
  try {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const config = serialize(store.state.nodes, store.state.edges, { version: 1 as any, workflowType: props.workflowType } as any) as any
    const res = await yzhApi.post('/api/Workflow/test/run', {
      taskType: 'TEST',
      ruleCode: currentLeaf.value.Code || currentLeaf.value.code,
      enterpriseCode: currentLeaf.value.enterpriseCode || 'YZH-STD-ENT',
      standardCode: currentLeaf.value.standardCode || currentFilter.standardCode,
      phaseCode: currentLeaf.value.phaseCode || currentFilter.phaseCode,
      configJson: JSON.stringify(config)
    })
    if (res?.success && res.data) nodeData.onSuccess(res.data)
    else nodeData.onError(res?.error || '测试失败', null)
  } catch (e: any) { nodeData.onError('请求失败: ' + (e?.message || e), null) }
}

async function handleTestDocExtract({ nodeType, body, onSuccess, onError }: any) {
  try {
    const url = nodeType === 'docField' ? '/api/Workflow/DocExtractionRule/test-field' : '/api/Workflow/DocExtractionRule/test-table'
    const res = await yzhApi.post(url, body)
    if (res?.status && res.data) onSuccess(res.data)
    else onError(res?.message || '测试失败')
  } catch (e: any) { onError('请求失败: ' + (e?.message || e)) }
}

function onNodeDocChange(ruleCode: string) { if (ruleCode) loadFieldsAndTables(ruleCode) }
</script>

<style scoped lang="less">
.studio-layout { display: flex; flex-direction: column; height: 100%; background: #f1f5f9; }
.page-header { display: flex; align-items: center; padding: 12px 20px; background: #fff; border-bottom: 1px solid #e2e8f0; flex-shrink: 0; }
.header-left { display: flex; align-items: center; gap: 8px; }
.header-title { font-size: 16px; font-weight: 700; color: #1e293b; }
.page-body { display: flex; flex: 1; min-height: 0; min-width: 0; overflow: hidden; }
.left-panel { width: 280px; min-width: 200px; flex-shrink: 1; flex-basis: 280px; background: #fff; border-right: 1px solid #e2e8f0; display: flex; flex-direction: column; overflow: hidden; }
.right-panel { width: 360px; min-width: 260px; flex-shrink: 1; flex-basis: 360px; background: #fff; border-left: 1px solid #e2e8f0; display: flex; flex-direction: column; overflow: hidden; }
.panel-toggle { width: 14px; flex-shrink: 0; display: flex; align-items: center; justify-content: center; cursor: pointer; background: #fff; color: #94a3b8; transition: all 0.2s; }
.panel-toggle:hover { background: #f1f5f9; color: #2563eb; }
.panel-toggle .el-icon { transform: rotate(180deg); }
.panel-toggle.left-toggle { border-right: 1px solid #e2e8f0; }
.panel-toggle.left-toggle:hover .el-icon { transform: rotate(0deg); }
.panel-toggle.right-toggle { border-left: 1px solid #e2e8f0; }
.panel-toggle.right-toggle .el-icon { transform: rotate(0deg); }
.panel-toggle.right-toggle:hover .el-icon { transform: rotate(180deg); }
.panel-header { display: flex; align-items: center; justify-content: space-between; padding: 12px 16px; font-size: 13px; font-weight: 700; color: #334155; border-bottom: 1px solid #f1f5f9; }
.tree-search { padding: 8px 12px; border-bottom: 1px solid #f1f5f9; }
.tree-container { flex: 1; overflow-y: auto; padding: 8px; }
.main-content { flex: 1; display: flex; flex-direction: column; min-width: 0; }
.canvas-toolbar { display: flex; align-items: center; justify-content: space-between; gap: 8px; padding: 8px 16px; background: #fff; border-bottom: 1px solid #e2e8f0; overflow: hidden; }
.canvas-title { font-size: 13px; font-weight: 600; color: #475569; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; flex-shrink: 1; }
.toolbar-actions { display: flex; gap: 4px; flex-shrink: 0; }
.canvas-container { flex: 1; min-height: 0; background: #f8fafc; min-width: 240px; }
.canvas-footer { padding: 6px 16px; font-size: 12px; color: #94a3b8; background: #fff; border-top: 1px solid #e2e8f0; }
.studio-tabs { height: 100%; display: flex; flex-direction: column; }
:deep(.el-tabs__header) { margin: 0; padding: 0 16px; background: #fafbfc; border-bottom: 1px solid #e2e8f0; }
:deep(.el-tabs__content) { flex: 1; overflow: hidden; }
:deep(.el-tab-pane) { height: 100%; overflow-y: auto; }
.nodes-panel, .prop-panel-wrapper { height: 100%; }
.tree-node { display: flex; align-items: center; padding: 6px 8px; cursor: pointer; border-radius: 4px; transition: background 0.15s; }
.tree-node:hover { background: #f1f5f9; }
.tree-node.active { background: #eff6ff; color: #2563eb; }
.tree-node .toggle-icon { margin-right: 4px; transition: transform 0.2s; }
.tree-node.expanded > .toggle-icon { transform: rotate(90deg); }
.tree-node .type-icon { margin-right: 6px; font-size: 16px; }
.tree-node .type-icon.org { color: #3b82f6; }
.tree-node .type-icon.standard { color: #8b5cf6; }
.tree-node .type-icon.phase { color: #f59e0b; }
.tree-node .type-icon.rule { color: #94a3b8; }
.tree-node .type-icon.rule.configured { color: #10b981; }
.tree-node .tree-label { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.level-1 { padding-left: 20px; }
.level-2 { padding-left: 40px; }
.level-3 { padding-left: 60px; }
.rule-code-text { font-family: 'JetBrains Mono', monospace; background: #f1f5f9; padding: 2px 6px; border-radius: 2px; color: #64748b; }
</style>
