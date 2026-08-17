<template>
  <div class="nc-config-page">
    <CertPageHeader title="NC 规则配置" :icon="IconSetting" />

    <div class="page-body">
      <!-- ===== 左栏：树 + NC 项列表 ===== -->
      <div class="left-panel">
        <el-card shadow="never" class="tree-card">
          <template #header>
            <div class="panel-header">
              <span>机构 / 标准 / 阶段</span>
              <el-button link size="small" @click="refreshTree"><el-icon><IconRefresh /></el-icon></el-button>
            </div>
          </template>
          <YzhStdTree ref="stdTreeRef" @select="handleTreeSelect" />
        </el-card>
        <el-card shadow="never" class="rule-list-card">
          <template #header>
            <div class="panel-header">
              <span>NC 检查项</span>
              <el-button link size="small" @click="loadRules"><el-icon><IconRefresh /></el-icon></el-button>
            </div>
          </template>
          <div class="rule-list" v-loading="ruleLoading">
            <div
              v-for="rule in ruleList"
              :key="rule.id"
              class="rule-item"
              :class="{ active: currentRule?.id === rule.id }"
              @click="selectRule(rule)"
            >
              <span class="rule-name">{{ rule.ruleName }}</span>
              <el-tag v-if="rule.clauseNumber" size="small" type="info" class="rule-clause">{{ rule.clauseNumber }}</el-tag>
            </div>
            <div v-if="!ruleLoading && !ruleList.length" class="rule-empty">
              {{ currentFilter.standardCode ? '该阶段暂无检查项' : '请先选择机构/标准/阶段' }}
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
            <el-button size="small" @click="validateGraph"><el-icon><IconCircleCheck /></el-icon> 校验</el-button>
            <el-button type="primary" size="small" :disabled="!currentRule" @click="handleSave">
              <el-icon><IconDownload /></el-icon> 保存工作流
            </el-button>
          </div>
        </div>
        <div ref="canvasRef" class="canvas-container"></div>
        <div class="canvas-status">
          <span>节点: {{ nodeCount }} | 边: {{ edgeCount }}</span>
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
          <NodePropertyForm :selected-node="selectedNode" :skills="skills" @update-node="handleUpdateNode" @delete-node="handleDeleteNode" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, onBeforeUnmount, getCurrentInstance } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import LogicFlow from '@logicflow/core'
import '@logicflow/core/dist/index.css'
import { CertPageHeader } from '@/certcore'
import { YzhStdTree } from '@/yzh'
import { IconSetting, IconRefresh, IconGrid, IconCircleCheck, IconDownload } from '@/yzh/icons'
import SkillPanel from '@/components/workflow-designer/SkillPanel.vue'
import NodePropertyForm from '@/components/workflow-designer/NodePropertyForm.vue'
import {
  compileToWorkflowConfig, decompileToGraphData, extractLayoutJson, nodeStyle, topologicalOrder
} from '@/components/workflow-designer/compiler'

const { proxy } = getCurrentInstance()
const canvasRef = ref(null)
const stdTreeRef = ref(null)
const diagram = ref(null)

// 左栏
const ruleList = ref([])
const ruleLoading = ref(false)
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

// ==================== 初始化 ====================

onMounted(async () => {
  await Promise.all([loadSkills(), loadCategories()])
  initDiagram()
})

onBeforeUnmount(() => { diagram.value?.destroy() })

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

// ==================== 画布 ====================

function initDiagram() {
  diagram.value = new LogicFlow({
    container: canvasRef.value,
    grid: { size: 20, visible: true, type: 'mesh' },
    background: '#fafbfc',
    behavior: { scroll: true, zoom: true, drag: true, canDragNode: true, canZoom: true },
    keyboard: { enabled: true }
  })

  diagram.value.on('node:click', ({ data }) => {
    selectedNode.value = {
      nodeId: data.id,
      nodeType: data.data?.nodeType || 'skill',
      title: data.data?.title || data.text || '',
      skillCode: data.data?.skillCode || '',
      config: data.data?.config || {},
      inputs: data.data?.inputs || {},
      outputs: data.data?.outputs || {}
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

// ==================== 添加 / 更新 / 删除节点 ====================

function defaultNodeData(item) {
  // 特殊节点
  if (item.nodeType === 'start') return { nodeType: 'start', title: '开始', config: {}, inputs: {}, outputs: {} }
  if (item.nodeType === 'end') return { nodeType: 'end', title: '结束', config: {}, inputs: {}, outputs: {} }
  if (item.nodeType === 'logic') return {
    nodeType: 'logic', title: '逻辑判断',
    config: { conditions: [{ valueA: '', operator: 'gte', valueB: '' }], conditionLogic: 'and' },
    inputs: {}, outputs: {}
  }
  // Skill 节点：按元数据预填输入默认值
  const meta = skills.value.find(s => s.skillCode === item.skillCode)
  const inputs = {}
  for (const input of meta?.inputs || []) {
    if (input.defaultValue) inputs[input.inputName] = input.defaultValue
  }
  return {
    nodeType: 'skill',
    title: item.skillName || meta?.skillName || item.skillCode,
    skillCode: item.skillCode,
    config: {},
    inputs,
    outputs: {}
  }
}

function handleAddNode(item) {
  const nodeId = `n${Date.now()}`
  const gd = diagram.value.getGraphData()
  const maxX = gd.nodes.reduce((m, n) => Math.max(m, n.x), 100)
  const maxY = gd.nodes.reduce((m, n) => Math.max(m, n.y), 80)
  const data = defaultNodeData(item)
  const category = item.category || skills.value.find(s => s.skillCode === item.skillCode)?.category || ''
  diagram.value.add([{
    id: nodeId,
    type: data.nodeType === 'start' || data.nodeType === 'end' ? 'circle'
      : data.nodeType === 'logic' ? 'diamond' : 'rect',
    x: 120 + (maxX % 600), y: 80 + (maxY % 400),
    text: data.title,
    style: nodeStyle(data.nodeType, data.skillCode, category),
    data
  }])
  refreshCount()
}

function handleUpdateNode(data) {
  if (!data.nodeId) return
  diagram.value.update(data.nodeId, {
    text: data.title || data.skillCode,
    data: {
      nodeType: data.nodeType,
      title: data.title,
      skillCode: data.skillCode,
      config: data.config,
      inputs: data.inputs,
      outputs: data.outputs
    }
  })
  selectedNode.value = data
}

function handleDeleteNode(nodeId) {
  diagram.value.delete([nodeId])
  selectedNode.value = null
  refreshCount()
}

// ==================== 树 / 规则加载 ====================

function handleTreeSelect({ orgCode, standardCode, phaseCode, phaseName }) {
  Object.assign(currentFilter, { orgCode: orgCode || '', standardCode: standardCode || '', phaseCode: phaseCode || '' })
  currentRule.value = null
  selectedNode.value = null
  clearCanvas()
  loadRules()
}

const refreshTree = () => {
  stdTreeRef.value?.reload()
  loadRules()
}

async function loadRules() {
  if (!currentFilter.standardCode) { ruleList.value = []; return }
  ruleLoading.value = true
  try {
    const res = await proxy.http.get('api/validation-rule/list', null, false, {
      params: { ...currentFilter }
    })
    if (res?.status) ruleList.value = res.data || []
  } catch (e) { console.error(e) } finally { ruleLoading.value = false }
}

function clearCanvas() {
  diagram.value?.render({ nodes: [], edges: [] })
  nodeCount.value = 0
  edgeCount.value = 0
}

async function selectRule(rule) {
  currentRule.value = rule
  savedTip.value = ''
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
  // 按拓扑序重排（模拟 dagre LR：列优先）
  const edgeMap = {}
  for (const e of gd.edges) {
    if (!edgeMap[e.sourceNodeId]) edgeMap[e.sourceNodeId] = []
    edgeMap[e.sourceNodeId].push(e.targetNodeId)
  }
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
  const depth = {}
  ordered.forEach((id, idx) => {
    const col = idx % 4, row = Math.floor(idx / 4)
    diagram.value.update(id, { x: 120 + col * 240, y: 80 + row * 140 })
  })
  refreshCount()
}

function validateGraph() {
  const gd = diagram.value.getGraphData()
  if (!gd.nodes.length) { ElMessage.warning('画布为空'); return }
  // 结构校验：无环
  const config = compileToWorkflowConfig(gd)
  const ordered = topologicalOrder(config)
  const hasCycle = ordered.length !== config.nodes.length
  if (hasCycle) { ElMessage.error('工作流存在循环依赖'); return }
  // 必填校验：skill 节点必须有 skillCode
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
.nc-config-page { padding: var(--yzh-space-5, 20px); }
.page-body { display: flex; gap: 12px; height: calc(100vh - 140px); }

/* 左栏 */
.left-panel { width: 280px; min-width: 280px; display: flex; flex-direction: column; gap: 12px; }
.tree-card { flex: 1.2; overflow: hidden; display: flex; flex-direction: column; }
.rule-list-card { flex: 1; overflow: hidden; display: flex; flex-direction: column; }
.panel-header { display: flex; align-items: center; justify-content: space-between; font-size: 13px; font-weight: 600; }
.rule-list { flex: 1; overflow-y: auto; }
.rule-item { display: flex; align-items: center; gap: 6px; padding: 8px 12px; cursor: pointer; font-size: 13px; color: #606266; transition: background .15s; }
.rule-item:hover { background: #f5f7fa; }
.rule-item.active { background: #ecf5ff; color: #409EFF; font-weight: 600; }
.rule-name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.rule-clause { flex-shrink: 0; }
.rule-empty { padding: 24px 12px; text-align: center; color: #c0c4cc; font-size: 12px; }

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
.right-panel { width: 300px; min-width: 300px; display: flex; flex-direction: column; gap: 12px; }
.skill-panel-wrapper { height: 45%; background: #fff; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,.06); overflow: hidden; }
.prop-panel-wrapper { flex: 1; background: #fff; border-radius: 4px; box-shadow: 0 1px 4px rgba(0,0,0,.06); overflow: hidden; }
</style>
