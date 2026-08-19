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
                          :class="{ active: currentRule?.id === rule.id }"
                          @click="selectRule(rule, phase)"
                        >
                          <el-icon class="tree-toggle" style="visibility: hidden"><IconForward /></el-icon>
                          <el-icon class="tree-icon rule"><IconDocument /></el-icon>
                          <span class="tree-label">{{ rule.ruleName }}</span>
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
            <el-button type="primary" size="small" :disabled="!currentRule" @click="handleSave">
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
          <span>节点: {{ nodeCount }} | 边: {{ edgeCount }}</span>
          <span v-if="currentRule" class="rule-code-text">{{ currentRule.ruleCode }}</span>
          <span v-if="savedTip" class="saved-text">✓ {{ savedTip }}</span>
        </div>
      </div>

      <!-- ===== 右栏：节点库 + 属性面板 ===== -->
      <div class="right-panel">
        <!-- 文档选择（工作流配置前需先选择文档） -->
        <div class="doc-select-bar">
          <span class="doc-label">参考文档：</span>
          <el-select v-model="selectedDocRule" placeholder="选择已配置提取规则的文档" size="small" style="flex: 1" @change="onDocRuleChange">
            <el-option v-for="r in docRules" :key="r.ruleCode" :label="r.fileName || r.standardFileCode" :value="r.ruleCode" />
          </el-select>
        </div>
        <div class="skill-panel-wrapper">
          <SkillPanel :skills="skills" :categories="categories" @add-node="handleAddNode" />
        </div>
        <div class="prop-panel-wrapper">
          <NodePropertyForm
            :selected-node="selectedNode"
            :skills="skills"
            :doc-rules="docRules"
            :doc-fields="currentDocFields"
            :doc-tables="currentDocTables"
            @update-node="handleUpdateNode"
            @delete-node="handleDeleteNode"
            @load-doc-fields="onNodeDocChange"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, watch, onMounted, onBeforeUnmount, getCurrentInstance } from 'vue'
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
import {
  compileToWorkflowConfig, decompileToGraphData, extractLayoutJson, nodeStyle, topologicalOrder
} from '@/components/workflow-designer/compiler'

const { proxy } = getCurrentInstance()
const canvasRef = ref(null)
const diagram = ref(null)

// 左栏 - 统一4级树
const treeData = ref([])
const searchText = ref('')
const currentRule = ref(null)
const currentFilter = reactive({ orgCode: '', standardCode: '', phaseCode: '' })

// 中栏
const nodeCount = ref(0)
const edgeCount = ref(0)
const savedTip = ref('')

// 右栏
const skills = ref([])
const categories = ref([])
const selectedNode = ref(null)

// 文档选择（工作流配置前需先选择文档）
const docRules = ref([])
const selectedDocRule = ref(null)
const currentDocFields = ref([])
const currentDocTables = ref([])

// ==================== 初始化 ====================

onMounted(async () => {
  await Promise.all([loadSkills(), loadCategories(), loadTree(), loadDocRules()])
  initDiagram()
})

// LogicFlow 2.0 没有 destroy()，用 clearData() 清空 + 释放引用
onBeforeUnmount(() => {
  if (diagram.value) {
    diagram.value.clearData?.()
    diagram.value = null
  }
})

async function loadSkills() {
  try {
    const res = await proxy.http.get('api/skill/list-active', null, false)
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

/** 顶部栏选择文档时加载字段/表格 */
async function onDocRuleChange(ruleCode) {
  if (!ruleCode) {
    currentDocFields.value = []
    currentDocTables.value = []
    return
  }
  await loadFieldsAndTables(ruleCode)
}

/** 节点属性面板选择文档时加载字段/表格（docField/docTable 节点） */
async function onNodeDocChange(ruleCode) {
  if (!ruleCode) return
  await loadFieldsAndTables(ruleCode)
}

/** 通用：加载字段和表格定义 */
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
    keyboard: { enabled: true }
  })

  diagram.value.on('node:click', ({ data }) => {
    const props = data.properties || {}
    selectedNode.value = {
      nodeId: data.id,
      nodeType: props.nodeType || 'skill',
      title: props.title || data.text || '',
      skillCode: props.skillCode || '',
      config: props.config || {},
      inputs: props.inputs || {},
      outputs: props.outputs || {},
      inputPorts: props.inputPorts || [],
      outputPorts: props.outputPorts || []
    }
  })
  diagram.value.on('edge:click', () => { selectedNode.value = null })
  diagram.value.on('blank:click', () => { selectedNode.value = null })
}

function refreshCount() {
  const gd = diagram.value?.getGraphData()
  nodeCount.value = gd?.nodes?.length || 0
  edgeCount.value = gd?.edges?.length || 0
}

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
    addNodeAtPosition(item, x, y)
  } catch (e) {
    console.error('[NCConfig] 拖拽添加节点失败:', e)
  }
}

function addNodeAtPosition(item, x, y) {
  const nodeId = `n${Date.now()}`
  const data = defaultNodeData(item)
  const category = item.category || skills.value.find(s => s.skillCode === item.skillCode)?.category || ''
  diagram.value.addNode({
    id: nodeId,
    type: data.nodeType === 'start' || data.nodeType === 'end' ? 'circle'
      : data.nodeType === 'logic' ? 'diamond' : 'rect',
    x, y,
    text: data.title,
    style: nodeStyle(data.nodeType, data.skillCode, category),
    properties: data
  })
  refreshCount()
}

// ==================== 添加 / 更新 / 删除节点 ====================

function defaultNodeData(item) {
  const nodeType = item.nodeType || 'skill'
  const inputPorts = item.inputPorts || []
  const outputPorts = item.outputPorts || []
  const defaults = {}
  for (const port of inputPorts) {
    if (port.defaultValue) defaults[port.name] = port.defaultValue
  }
  const configMap = {
    start: {},
    end: { result: {} },
    logic: { conditions: [{ valueA: '', operator: 'gte', valueB: '' }], conditionLogic: 'and' },
    ai_node: { prompt: '' },
    loop: {},
    docField: { docCode: '', fieldCode: '' },
    docTable: { docCode: '', tableCode: '' },
    skill: {}
  }
  const titleMap = {
    start: '开始', end: '结束', logic: '逻辑判断',
    ai_node: 'AI 节点', loop: '循环节点',
    docField: '文档字段', docTable: '文档表格'
  }
  const title = titleMap[nodeType] || item.skillName || item.skillCode || nodeType
  return {
    nodeType,
    title,
    skillCode: item.skillCode || '',
    config: configMap[nodeType] || {},
    inputs: defaults,
    outputs: {},
    inputPorts,
    outputPorts
  }
}

function handleAddNode(item) {
  const nodeId = `n${Date.now()}`
  const gd = diagram.value.getGraphData()
  const maxX = gd.nodes.reduce((m, n) => Math.max(m, n.x), 100)
  const maxY = gd.nodes.reduce((m, n) => Math.max(m, n.y), 80)
  const data = defaultNodeData(item)
  const category = item.category || skills.value.find(s => s.skillCode === item.skillCode)?.category || ''
  diagram.value.addNode({
    id: nodeId,
    type: data.nodeType === 'start' || data.nodeType === 'end' ? 'circle'
      : data.nodeType === 'logic' ? 'diamond' : 'rect',
    x: 120 + (maxX % 600), y: 80 + (maxY % 400),
    text: data.title,
    style: nodeStyle(data.nodeType, data.skillCode, category),
    properties: data
  })
  refreshCount()
}

function handleUpdateNode(data) {
  if (!data.nodeId) return
  diagram.value.updateText(data.nodeId, data.title || data.skillCode || '')
  diagram.value.setProperties(data.nodeId, {
    nodeType: data.nodeType,
    title: data.title,
    skillCode: data.skillCode,
    config: data.config,
    inputs: data.inputs,
    outputs: data.outputs,
    inputPorts: data.inputPorts,
    outputPorts: data.outputPorts
  })
  selectedNode.value = data
}

function handleDeleteNode(nodeId) {
  diagram.value.deleteNode(nodeId)
  selectedNode.value = null
  refreshCount()
}

// ==================== 选中规则 → 加载工作流 ====================

function clearCanvas() {
  diagram.value?.render({ nodes: [], edges: [] })
  nodeCount.value = 0
  edgeCount.value = 0
}

function handleClearCanvas() {
  const gd = diagram.value?.getGraphData()
  if (!gd?.nodes?.length) { ElMessage.info('画布已为空'); return }
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
      currentRule.value = { ...rule, ...d }
      renderWorkflow(d.ruleJson, d.layoutJson)
    } else {
      clearCanvas()
    }
  } catch (e) { console.error('加载规则详情失败', e) }
}

function renderWorkflow(ruleJson, layoutJson) {
  if (!ruleJson) { clearCanvas(); return }
  try {
    const config = JSON.parse(ruleJson)
    const layout = layoutJson ? JSON.parse(layoutJson) : null
    const { graphData } = decompileToGraphData(config, layout)
    diagram.value.render(graphData)
    refreshCount()
  } catch (e) {
    console.error('工作流解析失败', e)
    ElMessage.error('工作流配置解析失败')
  }
}

// ==================== 布局 / 校验 / 保存 ====================

function autoLayout() {
  const gd = diagram.value.getGraphData()
  if (!gd.nodes.length) return
  const inDeg = {}, adj = {}
  gd.nodes.forEach(n => { inDeg[n.id] = 0; adj[n.id] = [] })
  gd.edges.forEach(e => { if (inDeg[e.targetNodeId] !== undefined) { inDeg[e.targetNodeId]++; adj[e.sourceNodeId].push(e.targetNodeId) } })
  const queue = gd.nodes.filter(n => inDeg[n.id] === 0).map(n => n.id)
  const ordered = []
  while (queue.length) {
    const c = queue.shift(); ordered.push(c)
    for (const n of adj[c]) { inDeg[n]--; if (inDeg[n] === 0) queue.push(n) }
  }
  gd.nodes.forEach(n => { if (!ordered.includes(n.id)) ordered.push(n.id) })

  const posMap = {}
  ordered.forEach((id, idx) => {
    const col = idx % 4, row = Math.floor(idx / 4)
    posMap[id] = { x: 120 + col * 240, y: 80 + row * 140 }
  })
  const newNodes = gd.nodes.map(n => ({ ...n, x: posMap[n.id]?.x ?? n.x, y: posMap[n.id]?.y ?? n.y }))
  const newGraphData = { nodes: newNodes, edges: gd.edges }
  diagram.value.render(newGraphData)
  refreshCount()
  ElMessage.success('自动布局完成')
}

function validateGraph() {
  const gd = diagram.value.getGraphData()
  if (!gd.nodes.length) { ElMessage.warning('画布为空'); return }
  const config = compileToWorkflowConfig(gd)
  const ordered = topologicalOrder(config)
  const hasCycle = ordered.length !== config.nodes.length
  if (hasCycle) { ElMessage.error('工作流存在循环依赖'); return }
  const missing = config.nodes.filter(n => n.nodeType === 'skill' && !n.skillCode)
  if (missing.length) { ElMessage.warning(`${missing.length} 个 Skill 节点未配置编码`); return }
  ElMessage.success(`结构校验通过：${config.nodes.length} 节点 / ${config.edges.length} 连线`)
}

async function handleSave() {
  if (!currentRule.value) { ElMessage.warning('请先选择 NC 检查项'); return }
  const gd = diagram.value.getGraphData()
  if (!gd.nodes.length) { ElMessage.warning('画布为空，请添加节点'); return }

  const config = compileToWorkflowConfig(gd, { workflowType: 'validation', version: 1 })
  const layoutJson = extractLayoutJson(gd)

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
      layoutJson: JSON.stringify(layoutJson),
      ncDescriptionTemplate: currentRule.value.ncDescriptionTemplate,
      isActive: currentRule.value.isActive !== false,
      remark: currentRule.value.remark
    }, true)
    if (res?.status) {
      savedTip.value = `${new Date().toLocaleTimeString()} 已保存`
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
.doc-select-bar { display: flex; align-items: center; gap: 6px; padding: 8px 10px; background: #f0f9eb; border-radius: 4px; border: 1px solid #e1f3d8; }
.doc-label { font-size: 12px; color: #606266; white-space: nowrap; }
.skill-panel-wrapper { height: 35%; background: #fff; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,.06); overflow: hidden; }
.prop-panel-wrapper { flex: 1; background: #fff; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,.06); overflow: hidden; }
</style>
