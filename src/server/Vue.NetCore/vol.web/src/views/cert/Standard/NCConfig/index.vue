<template>
  <div class="nc-config-page">
    <CertPageHeader title="NC 规则配置" :icon="IconSetting" />

    <div class="page-body">
      <!-- ===== 左栏：统一4级树 ===== -->
      <div class="left-panel">
        <el-card shadow="never" class="tree-card">
          <template #header>
            <div class="panel-header">
              <span>机构 / 标准 / 阶段 / NC检查项</span>
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

                      <!-- NC检查项 -->
                      <div v-if="phase.visible && phase.expanded && phase.ruleLoaded">
                        <div
                          v-for="rule in phase.children"
                          :key="rule.id"
                          class="tree-node level-3"
                          :class="{ active: currentRule?.id === rule.id, configured: !!rule.ruleJson }"
                          @click="selectRule(rule, phase)"
                        >
                          <el-icon class="tree-toggle" style="visibility: hidden"><IconForward /></el-icon>
                          <el-icon class="tree-icon rule" :class="{ configured: !!rule.ruleJson }">
                            <IconCircleCheck v-if="rule.ruleJson" />
                            <IconDocument v-else />
                          </el-icon>
                          <span class="tree-label">{{ rule.ruleName }}</span>
                          <span v-if="rule.ruleJson" class="config-dot"></span>
                          <el-tag v-if="rule.clauseNumber" size="small" type="info" class="node-badge">{{ rule.clauseNumber }}</el-tag>
                        </div>
                        <div v-if="!phase.children.length" class="rule-empty">该阶段暂无检查项</div>
                      </div>
                      <div v-if="phase.visible && phase.expanded && phase.ruleLoading" class="rule-loading">
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
          <span class="canvas-title">{{ currentRule ? `工作流：${currentRule.ruleName}` : '请选择 NC 检查项' }}</span>
          <div class="toolbar-actions">
            <el-button size="small" @click="autoLayout"><el-icon><IconGrid /></el-icon> 自动布局</el-button>
            <el-button size="small" type="danger" plain @click="handleClearCanvas"><el-icon><IconDelete /></el-icon> 清空画布</el-button>
            <el-button size="small" @click="validateGraph"><el-icon><IconCircleCheck /></el-icon> 校验</el-button>
            <el-button type="primary" size="small" :disabled="!currentRule || !store.state.dirty" @click="handleSave">
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
          <span v-if="currentRule" class="rule-code-text">{{ currentRule.ruleCode }}</span>
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
            @test-doc-extract="handleTestDocExtract"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
console.log('[NCConfig] 🔥🔥🔥 index.vue 代码已加载!!! 版本: 2026-08-20-fix-rename')
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


const { proxy } = getCurrentInstance()
const canvasRef = ref(null)
const diagram = ref(null)

// ===== 操作层 store（唯一变更入口） =====
const store = useWorkflowStore()

// 左栏 - 统一4级树
const treeData = ref([])
const searchText = ref('')
const currentRule = ref(null)
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

// 选中的边（供键盘删除）
const selectedEdgeId = ref(null)

// ==================== 初始化 ====================

onMounted(async () => {
  await Promise.all([loadSkills(), loadCategories(), loadTree(), loadDocRules()])
  initDiagram()
})

onActivated(() => {
  loadSkills()
})

onBeforeUnmount(() => {
  document.removeEventListener('keydown', handleKeyDown)
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
          ruleLoading: false,
          ruleLoaded: false
        }))
      }))
    }))
    applySearchFilter()
  } catch (e) {
    console.error('[NCConfig] 加载树失败:', e)
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
  currentRule.value = null
  selectedNode.value = null
  clearCanvas()
  if (phase.expanded && !phase.ruleLoaded) {
    await loadRulesForPhase(phase)
  }
}

async function loadRulesForPhase(phase) {
  phase.ruleLoading = true
  try {
    const params = `orgCode=${encodeURIComponent(currentFilter.orgCode)}&standardCode=${encodeURIComponent(currentFilter.standardCode)}&phaseCode=${encodeURIComponent(currentFilter.phaseCode)}`
    const res = await proxy.http.get(`api/validation-rule/list?${params}`, null, false)
    if (res?.status) {
      phase.children = (res.data || []).map(r => ({ ...r, id: r.id || r.ruleCode }))
    } else {
      phase.children = []
    }
    phase.ruleLoaded = true
  } catch (e) {
    console.error('[NCConfig] 加载NC检查项失败:', e)
    phase.children = []
  } finally {
    phase.ruleLoading = false
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
    const nodeData = {
      nodeId: data.id,
      nodeType: props.nodeType || 'skill',
      classCode: props.classCode || props.nodeType || 'skill',
      title: storeNode?.title || props.title || data.text || '',
      skillCode: props.skillCode || '',
      config: storeNode?.config || props.config || {},
      inputs: storeNode?.inputs || props.inputs || {},
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

  diagram.value.on('node:dblclick', ({ data }) => {
    const nodeType = data.properties?.nodeType || data.properties?.classCode
    console.log('[NCConfig] node:dblclick 触发, id:', data.id, 'type:', nodeType)
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
    // 改名过程中不清空 selectedNode（防止 ElMessageBox 关闭时误触发）
    // 使用时间戳判断：在改名保护窗口内不执行清空
    if (_renamingNodeId && (Date.now() - _renamingTimestamp) < RENAMING_GUARD_MS) {
      console.log('[NCConfig] blank:click 被拦截 (时间戳保护), _renamingNodeId:', _renamingNodeId, '剩余:', (RENAMING_GUARD_MS - (Date.now() - _renamingTimestamp)) + 'ms')
      return
    }
    console.log('[NCConfig] blank:click 执行, 清空 selectedNode')
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

/** 边变化后同步 selectedNode 的 inputs */
function syncSelectedNodeInputs() {
  if (!selectedNode.value || !diagram.value) return
  const gd = diagram.value.getGraphData()
  const nodeId = selectedNode.value.nodeId
  const inEdges = (gd.edges || []).filter(e => e.targetNodeId === nodeId)
  const nodeProps = gd.nodes.find(n => n.id === nodeId)?.properties || {}
  const inputPorts = nodeProps.inputPorts || selectedNode.value.inputPorts || []
  const newInputs = {}

  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const edge = inEdges.find(e => {
      const handle = e.properties?.targetHandle
      return !handle || handle === port.name
    })
    if (edge) {
      const existing = selectedNode.value.inputs?.[port.name]
      if (existing && !gd.nodes.find(n => n.id === existing)) {
        continue // 当前值是常量，不覆盖
      }
      newInputs[port.name] = edge.sourceNodeId
    }
  }

  const merged = { ...selectedNode.value.inputs, ...newInputs }
  // 删除已断开的节点引用
  for (const port of inputPorts) {
    if (port.bindMode !== 'Link' && port.bindMode !== 'LinkOrConstant') continue
    const val = merged[port.name]
    if (val && gd.nodes.find(n => n.id === val)) {
      const stillConnected = inEdges.some(e => e.sourceNodeId === val)
      if (!stillConnected) delete merged[port.name]
    }
  }

  if (JSON.stringify(merged) !== JSON.stringify(selectedNode.value.inputs)) {
    diagram.value.setProperties(nodeId, { inputs: merged })
    selectedNode.value = { ...selectedNode.value, inputs: merged }
    // 同步 store
    store.setInputValue(nodeId, null, null) // 标记脏
    const storeNode = store.getNodeById(nodeId)
    if (storeNode) {
      storeNode.inputs = merged
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
  return store.state.nodes.map(n => ({
    id: n.id,
    title: n.title || n.id,
    text: n.title || n.id,
    nodeType: n.nodeType
  }))
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
    console.error('[NCConfig] 拖拽添加节点失败:', e)
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
  console.log('[NCConfig] promptEditNodeName 开始, nodeId:', nodeId, '当前名称:', currentName)

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

    console.log('[NCConfig] 用户输入新名称:', name)

    if (!store.renameNode(nodeId, name)) {
      ElMessage.warning(`节点名称「${name}」已存在，请使用其他名称`)
      _renamingNodeId = null
      _renamingTimestamp = 0
      return
    }

    // 同步更新画布
    diagram.value.updateText(nodeId, name)
    diagram.value.setProperties(nodeId, { title: name })

    // 用 setTimeout 延迟刷新 selectedNode（比 nextTick 更长延迟，确保在 blank:click 之后执行）
    setTimeout(() => {
      const storeNode = store.getNodeById(nodeId)
      console.log('[NCConfig] setTimeout 300ms 执行, storeNode:', storeNode?.title, '_renamingNodeId:', _renamingNodeId)
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
        const newNodeVal = {
          nodeId: storeNode.id,
          nodeType: storeNode.nodeType,
          classCode: storeNode.classCode,
          title: storeNode.title,
          skillCode: storeNode.skillCode,
          config: { ...storeNode.config },
          inputs: { ...storeNode.inputs },
          outputs: { ...storeNode.outputs },
          inputPorts: storeNode.inputPorts || [],
          outputPorts: storeNode.outputPorts || [],
          branchEdges
        }
        console.log('[NCConfig] ✅ 设置 selectedNode 成功, title:', newNodeVal.title)
        selectedNode.value = newNodeVal
        // 强制自增刷新计数器 → 触发 NodePropertyForm 的 :key 变化 → 组件强制重建
        forceRefreshTick.value++
      } else {
        console.warn('[NCConfig] ❌ storeNode 为 null!')
      }
      _renamingNodeId = null
      _renamingTimestamp = 0
    }, 300) // 300ms 延迟，确保在 ElMessageBox 关闭触发的 blank:click 之后
    ElMessage.success('节点名称已更新')
  }).catch(() => {
    _renamingNodeId = null
    _renamingTimestamp = 0
    console.log('[NCConfig] 用户取消改名')
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

function handleTestNode(nodeData) {
  ElMessage.info(`节点「${nodeData.title}」测试功能开发中...`)
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
      ElMessage.success('画布已清空')
    })
    .catch(() => {})
}

async function selectRule(rule, phase) {
  currentRule.value = rule
  savedTip.value = ''
  currentFilter.orgCode = phase.cbCode || currentFilter.orgCode
  currentFilter.standardCode = phase.stdCode || phase.standardCode || currentFilter.standardCode
  currentFilter.phaseCode = phase.phaseCode || currentFilter.phaseCode

  try {
    const res = await proxy.http.get(`api/validation-rule/${rule.ruleCode}`, null, false)
    if (res?.status && res.data) {
      const d = res.data
      Object.assign(rule, d)
      currentRule.value = rule
      renderWorkflow(d.ruleJson, d.layoutJson)
    } else {
      clearCanvas()
      ensureStartNode()
    }
  } catch (e) { console.error('加载规则详情失败', e) }
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

function renderWorkflow(ruleJson, layoutJson) {
  if (!ruleJson) {
    clearCanvas()
    ensureStartNode()
    return
  }
  try {
    const config = JSON.parse(ruleJson)
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
  if (!nodes.length) { ElMessage.warning('画布为空'); return }

  // 1. 必须有 start 节点
  if (!nodes.some(n => n.classCode === 'start')) { ElMessage.error('缺少开始节点'); return }

  // 2. 必须有 end 节点
  if (!nodes.some(n => n.classCode === 'end')) { ElMessage.error('缺少结束节点'); return }

  // 3. 名称唯一性校验
  const nameMap = {}
  const duplicates = []
  for (const n of nodes) {
    const name = n.title || n.id
    if (nameMap[name]) duplicates.push(name)
    else nameMap[name] = true
  }
  if (duplicates.length) {
    ElMessage.error(`存在重复节点名称：${duplicates.join('、')}`)
    return
  }

  // 4. Skill 节点编码检查
  const missingSkill = nodes.filter(n => n.nodeType === 'skill' && !n.skillCode)
  if (missingSkill.length) {
    ElMessage.warning(`${missingSkill.length} 个 Skill 节点未配置编码`)
    return
  }

  // 5. 输出端口存在性检查
  for (const n of nodes) {
    if (n.classCode === 'branch') {
      const hasCondition = Object.values(n.inputs || {}).some(v => v && v !== '')
      if (!hasCondition) {
        ElMessage.warning(`分支节点「${n.title}」未绑定条件输入`)
        return
      }
    }
  }

  ElMessage.success(`校验通过：${nodes.length} 节点 / ${store.state.edges.length} 连线`)
}

async function handleSave() {
  if (!currentRule.value) { ElMessage.warning('请先选择 NC 检查项'); return }
  if (!store.state.nodes.length) { ElMessage.warning('画布为空，请添加节点'); return }

  // 通过 serialize 生成落库 JSON
  const config = serialize(store.state.nodes, store.state.edges, {
    version: 1,
    workflowType: 'validation'
  })
  const layout = extractLayout(store.state.nodes)

  try {
    await ElMessageBox.confirm(`保存工作流到检查项「${currentRule.value.ruleName}」？`, '保存确认', { type: 'info' })
    const res = await proxy.http.post('api/validation-rule', {
      id: currentRule.value.id,
      orgCode: currentRule.value.orgCode || currentFilter.orgCode,
      standardCode: currentRule.value.standardCode || currentFilter.standardCode,
      phaseCode: currentRule.value.phaseCode || currentFilter.phaseCode,
      clauseCode: currentRule.value.clauseCode,
      workflowCode: currentRule.value.workflowCode,
      ruleCode: currentRule.value.ruleCode,
      ruleName: currentRule.value.ruleName,
      ruleNameEn: currentRule.value.ruleNameEn,
      severityIfViolated: currentRule.value.severityIfViolated || 'minor',
      ruleJson: JSON.stringify(config),
      layoutJson: JSON.stringify(layout),
      ncDescriptionTemplate: currentRule.value.ncDescriptionTemplate,
      isActive: currentRule.value.isActive !== false,
      remark: currentRule.value.remark
    }, true)
    if (res?.status) {
      savedTip.value = `${new Date().toLocaleTimeString()} 已保存`
      currentRule.value.ruleJson = JSON.stringify(config)
      currentRule.value.layoutJson = JSON.stringify(layout)
      store.markClean()
      ElMessage.success('工作流保存成功')
    } else ElMessage.error(res?.message || '保存失败')
  } catch (e) { if (e !== 'cancel') ElMessage.error('保存失败') }
}
</script>

<style scoped lang="less">
.nc-config-page { padding: 16px; height: 100%; display: flex; flex-direction: column; overflow: hidden; box-sizing: border-box; }
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
