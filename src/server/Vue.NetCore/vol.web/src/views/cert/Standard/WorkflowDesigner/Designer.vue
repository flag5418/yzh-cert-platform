<template>
  <div class="workflow-designer-page">
    <!-- 顶部工具栏 -->
    <el-card shadow="never" class="toolbar-card">
      <div class="toolbar">
        <div class="toolbar-left">
          <el-button @click="$router.back()">
            <el-icon><IconBack /></el-icon> 返回
          </el-button>
          <el-divider direction="vertical" />
          <span class="page-title">{{ pageTitle }}</span>
          <el-tag v-if="workflowCode" type="info" size="small" style="margin-left:8px">{{ workflowCode }}</el-tag>
        </div>
        <div class="toolbar-right">
          <el-button size="small" @click="autoLayout">
            <el-icon><IconGrid /></el-icon> 自动布局
          </el-button>
          <el-button size="small" @click="validateGraph">
            <el-icon><IconCircleCheck /></el-icon> 校验
          </el-button>
          <el-button type="success" size="small" @click="runTest">
            <el-icon><IconPlay /></el-icon> 测试
          </el-button>
          <el-button type="primary" size="small" @click="handleSave">
            <el-icon><IconDownload /></el-icon> 保存
          </el-button>
        </div>
      </div>
    </el-card>

    <!-- 主体三栏布局 -->
    <div class="designer-body">
      <!-- 左侧：Skill 面板 -->
      <div class="skill-panel-wrapper">
        <SkillPanel :skills="skillList" @add-node="handleAddNode" />
      </div>

      <!-- 中间：LogicFlow 画布 -->
      <div class="canvas-area">
        <div ref="canvasRef" class="canvas-container"></div>
        <div class="canvas-status">
          <span>节点: {{ nodeCount }} | 边: {{ edgeCount }}</span>
          <span v-if="graphValid" class="valid-badge">✓ 结构有效</span>
          <span v-else class="invalid-badge">✗ 请检查连接</span>
        </div>
      </div>

      <!-- 右侧：属性面板 -->
      <div class="property-panel-wrapper">
        <NodePropertyForm
          :selected-node="selectedNode"
          :skills="skillList"
          @update-node="handleUpdateNode"
          @delete-node="handleDeleteNode"
        />
      </div>
    </div>

    <!-- 保存确认弹窗 -->
    <el-dialog v-model="saveDialogVisible" title="保存工作流" width="500px">
      <el-form :model="saveForm" label-width="90px">
        <el-form-item label="工作流类型">
          <el-select v-model="saveForm.workflowType" style="width:100%">
            <el-option label="NC校验 (validation)" value="validation" />
            <el-option label="报告生成 (report)" value="report" />
            <el-option label="文档提取 (extraction)" value="extraction" />
          </el-select>
        </el-form-item>
        <el-form-item label="版本">
          <el-input-number v-model="saveForm.version" :min="1" :max="999" />
        </el-form-item>
        <el-form-item label="输出键名">
          <el-input v-model="saveForm.outputKey" placeholder="如 result" size="small" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="saveDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="confirmSave">确认保存</el-button>
      </template>
    </el-dialog>

    <!-- 测试沙箱弹窗 -->
    <el-dialog v-model="testDialogVisible" title="工作流测试沙箱" width="700px" destroy-on-close>
      <div class="test-sandbox">
        <el-form :model="testForm" label-width="100px">
          <el-form-item label="企业编码">
            <el-input v-model="testForm.enterpriseCode" placeholder="运行期注入的企业编码" />
          </el-form-item>
          <el-form-item label="阶段编码">
            <el-input v-model="testForm.phaseCode" placeholder="运行期注入的阶段编码" />
          </el-form-item>
        </el-form>
        <div class="test-result" v-loading="testLoading">
          <div class="result-header">
            <span>执行结果</span>
            <el-button size="small" type="primary" @click="runTest" :loading="testLoading">运行测试</el-button>
          </div>
          <pre class="result-json" v-if="testResult">{{ testResult }}</pre>
          <div v-else class="result-empty">点击"运行测试"执行工作流</div>
        </div>
      </div>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted, onBeforeUnmount, getCurrentInstance } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElNotification } from 'element-plus'
import LogicFlow from '@logicflow/core'
import '@logicflow/core/dist/index.css'
import '@logicflow/extension/dist/index.css'
import { IconBack, IconGrid, IconCircleCheck, IconDownload, IconPlay } from '@/yzh/icons'
import SkillPanel from '@/components/workflow-designer/SkillPanel.vue'
import NodePropertyForm from '@/components/workflow-designer/NodePropertyForm.vue'
import { compileToWorkflowConfig, decompileToGraphData } from '@/components/workflow-designer/compiler'

const route = useRoute()
const router = useRouter()
const { proxy } = getCurrentInstance()

const canvasRef = ref(null)
const diagram = ref(null)
const skillList = ref([])
const selectedNode = ref(null)
const nodeCount = ref(0)
const edgeCount = ref(0)
const graphValid = ref(true)
const saveDialogVisible = ref(false)
const testDialogVisible = ref(false)
const testLoading = ref(false)
const testResult = ref(null)
const workflowCode = ref('')

const saveForm = reactive({ workflowType: 'validation', version: 1, outputKey: 'result' })
const testForm = reactive({ enterpriseCode: '', phaseCode: '' })
const pageTitle = ref('新建工作流')

onMounted(async () => {
  await loadSkills()
  initDiagram()
  const id = route.params.id
  if (id && id !== 'new') {
    pageTitle.value = '编辑工作流'
    await loadWorkflow(id)
  }
})

onBeforeUnmount(() => { diagram.value?.destroy() })

async function loadSkills() {
  try {
    const res = await proxy.http.get('api/skill/list-active', null, false)
    if (res?.status) skillList.value = res.data || []
  } catch (e) { skillList.value = getDefaultSkills() }
}

function getDefaultSkills() {
  return [
    { skillCode: 'get_field', skillName: '获取字段值', skillType: 'data_access' },
    { skillCode: 'get_table', skillName: '获取表格数据', skillType: 'data_access' },
    { skillCode: 'compare', skillName: '值比较', skillType: 'data_process' },
    { skillCode: 'date_diff', skillName: '日期差', skillType: 'data_process' },
    { skillCode: 'text_merge', skillName: '文本合并', skillType: 'data_process' },
    { skillCode: 'llm_judge', skillName: 'AI语义判断', skillType: 'ai_judge' },
    { skillCode: 'llm_generate', skillName: 'AI内容生成', skillType: 'ai_generate' },
    { skillCode: 'create_nc', skillName: '创建不符合项', skillType: 'output' },
    { skillCode: 'save_result', skillName: '保存审核结果', skillType: 'output' },
    { skillCode: 'assemble_text', skillName: '组装报告文本', skillType: 'output' }
  ]
}

function initDiagram() {
  diagram.value = new LogicFlow({
    container: canvasRef.value,
    grid: { size: 20, visible: true, type: 'mesh' },
    background: '#fafbfc',
    plugins: [],
    behavior: { scroll: true, zoom: true, drag: true, canDragNode: true, canZoom: true },
    keyboard: { enabled: true }
  })

  diagram.value.on('node:click', ({ data }) => {
    selectedNode.value = {
      nodeId: data.id,
      skillCode: data.data?.skillCode || '',
      inputs: data.data?.inputs || {},
      outputs: data.data?.outputs || {},
      config: data.data?.config || {}
    }
  })
  diagram.value.on('edge:click', () => { selectedNode.value = null })
  diagram.value.on('blank:click', () => { selectedNode.value = null })
}

function handleAddNode(skill) {
  const nodeId = `n${Date.now()}`
  const gd = diagram.value.getGraphData()
  const maxX = gd.nodes.reduce((m, n) => Math.max(m, n.x), 100)
  const maxY = gd.nodes.reduce((m, n) => Math.max(m, n.y), 80)
  diagram.value.add([{
    id: nodeId, type: 'rect',
    x: 100 + (maxX % 600), y: 80 + (maxY % 400),
    text: skill.skillName,
    style: { fill: skillNodeFill(skill.skillCode), stroke: skillNodeStroke(skill.skillCode), strokeWidth: 2 },
    data: { skillCode: skill.skillCode, inputs: {}, outputs: {}, config: {}, condition: null }
  }])
  markDirty()
}

function handleUpdateNode(data) {
  if (!data.nodeId) return
  diagram.value.update(data.nodeId, {
    text: data.skillCode,
    style: { fill: skillNodeFill(data.skillCode), stroke: skillNodeStroke(data.skillCode) },
    data: { skillCode: data.skillCode, inputs: data.inputs, outputs: data.outputs, config: data.config, condition: null }
  })
  selectedNode.value = data
  markDirty()
}

function handleDeleteNode(nodeId) {
  diagram.value.delete([nodeId])
  selectedNode.value = null
  markDirty()
}

function autoLayout() {
  const gd = diagram.value.getGraphData()
  const ordered = topologicalSort(gd.nodes, gd.edges)
  const cols = 4
  ordered.forEach((nodeId, idx) => {
    const col = idx % cols, row = Math.floor(idx / cols)
    diagram.value.update(nodeId, { x: 120 + col * 220, y: 80 + row * 130 })
  })
  markDirty()
}

function validateGraph() {
  const gd = diagram.value.getGraphData()
  const ordered = topologicalSort(gd.nodes, gd.edges)
  const hasCycle = ordered.length !== gd.nodes.length
  if (hasCycle) { ElMessage.error('工作流存在循环依赖'); graphValid.value = false; return }
  const missingSkill = gd.nodes.filter(n => !n.data?.skillCode)
  if (missingSkill.length > 0) { ElMessage.warning(`${missingSkill.length} 个节点未配置 Skill`); graphValid.value = false; return }
  graphValid.value = true
  ElMessage.success('工作流结构校验通过')
}

function markDirty() {
  const gd = diagram.value.getGraphData()
  nodeCount.value = gd.nodes.length
  edgeCount.value = gd.edges.length
}

function exportConfig() {
  const gd = diagram.value.getGraphData()
  if (!gd || gd.nodes.length === 0) return null
  const nodes = gd.nodes.map(n => ({
    nodeId: n.id, skillCode: n.data?.skillCode || '',
    config: n.data?.config || {}, inputs: n.data?.inputs || {}, outputs: n.data?.outputs || {}
  }))
  const edges = gd.edges.map(e => ({
    source: e.sourceNodeId, target: e.targetNodeId,
    sourceHandle: e.data?.sourceHandle || null, targetHandle: e.data?.targetHandle || null
  }))
  const branches = gd.edges.filter(e => e.data?.condition).map(e => ({
    from: e.sourceNodeId, condition: e.data.condition,
    then: [{ nodeId: e.targetNodeId, skillCode: nodes.find(n => n.nodeId === e.targetNodeId)?.skillCode || '',
      inputs: nodes.find(n => n.nodeId === e.targetNodeId)?.inputs || {}, outputs: nodes.find(n => n.nodeId === e.targetNodeId)?.outputs || {} }]
  }))
  return {
    version: saveForm.version, workflowType: saveForm.workflowType,
    nodes, edges, branches,
    outputConfig: { result_key: saveForm.outputKey }
  }
}

async function handleSave() {
  const config = exportConfig()
  if (!config) { ElMessage.warning('画布为空，请先添加节点'); return }
  saveForm.workflowType = config.workflowType || 'validation'
  saveForm.version = (config.version || 1) + 1
  saveDialogVisible.value = true
}

async function confirmSave() {
  const config = exportConfig()
  if (!config) return
  const ruleCode = route.params.id !== 'new' ? route.params.id : `WF-${Date.now()}`
  try {
    const res = await proxy.http.post('api/workflow-definition', {
      ruleCode, ruleName: `工作流-${saveForm.workflowType}`,
      workflowCode: ruleCode,
      ruleJson: JSON.stringify(config),
      severityIfViolated: 'minor',
      standardCode: route.query.standardCode || '',
      phaseCode: route.query.phaseCode || '',
      orgCode: route.query.orgCode || ''
    }, true)
    if (res?.status) {
      ElMessage.success('工作流已保存')
      saveDialogVisible.value = false
      router.push('/CertPlatform/WorkflowRules/Rules')
    } else ElMessage.error(res?.message || '保存失败')
  } catch (e) { ElMessage.error('保存失败') }
}

async function runTest() {
  const config = exportConfig()
  if (!config) { ElMessage.warning('请先配置工作流'); return }
  testLoading.value = true
  testResult.value = null
  try {
    // 调用后端测试接口
    const res = await proxy.http.post('api/validation-rule/test', {
      workflowConfig: config,
      enterpriseCode: testForm.enterpriseCode || route.query.orgCode || '',
      phaseCode: testForm.phaseCode || route.query.phaseCode || ''
    }, true)
    if (res?.status) {
      testResult.value = JSON.stringify(res.data, null, 2)
      ElNotification({ title: '测试完成', message: '工作流执行成功', type: 'success' })
    } else {
      testResult.value = JSON.stringify({ error: res?.message }, null, 2)
      ElNotification({ title: '测试失败', message: res?.message, type: 'error' })
    }
  } catch (e) {
    testResult.value = JSON.stringify({ error: e.message || '执行异常' }, null, 2)
    ElNotification({ title: '测试失败', message: String(e), type: 'error' })
  } finally {
    testLoading.value = false
  }
}

async function loadWorkflow(id) {
  try {
    const res = await proxy.http.get(`api/validation-rule/${id}`, null, false)
    if (res?.status && res.data) {
      workflowCode.value = res.data.workflowCode
      pageTitle.value = `编辑: ${res.data.ruleName}`
      if (res.data.workflowConfig) {
        const config = JSON.parse(res.data.workflowConfig)
        const { graphData } = decompileToGraphData(config)
        diagram.value.render(graphData)
        markDirty()
      }
    }
  } catch (e) { ElMessage.error('加载工作流失败') }
}

function skillNodeFill(skillCode) {
  const map = { get_field: '#E3F2FD', get_table: '#E3F2FD', compare: '#E8F5E9', date_diff: '#E8F5E9',
    text_merge: '#E8F5E9', llm_judge: '#FFF3E0', llm_generate: '#FCE4EC',
    create_nc: '#F3E5F5', save_result: '#F3E5F5', assemble_text: '#F3E5F5' }
  return map[skillCode] ?? '#F5F5F5'
}

function skillNodeStroke(skillCode) {
  const map = { get_field: '#1565C0', get_table: '#1565C0', compare: '#2E7D32', date_diff: '#2E7D32',
    text_merge: '#2E7D32', llm_judge: '#E65100', llm_generate: '#880E4F',
    create_nc: '#6A1B9A', save_result: '#6A1B9A', assemble_text: '#6A1B9A' }
  return map[skillCode] ?? '#9E9E9E'
}

function topologicalSort(nodes, edges) {
  const ids = new Set(nodes.map(n => n.id))
  const inDeg = {}, adj = {}
  ids.forEach(id => { inDeg[id] = 0; adj[id] = [] })
  for (const e of edges) {
    if (ids.has(e.sourceNodeId) && ids.has(e.targetNodeId)) { inDeg[e.targetNodeId]++; adj[e.sourceNodeId].push(e.targetNodeId) }
  }
  const q = [], result = []
  ids.forEach(id => { if (inDeg[id] === 0) q.push(id) })
  while (q.length) {
    const c = q.shift(); result.push(c)
    for (const n of adj[c]) { inDeg[n]--; if (inDeg[n] === 0) q.push(n) }
  }
  ids.forEach(id => { if (!result.includes(id)) result.push(id) })
  return result
}
</script>

<style scoped lang="less">
.workflow-designer-page { display:flex; flex-direction:column; height:calc(100vh - 60px); overflow:hidden; }
.toolbar-card { margin:0; border-radius:0; }
.toolbar { display:flex; align-items:center; justify-content:space-between; padding:4px 0; }
.toolbar-left { display:flex; align-items:center; gap:8px; }
.page-title { font-size:15px; font-weight:600; color:var(--el-text-color-primary); }
.designer-body { display:flex; flex:1; overflow:hidden; }
.skill-panel-wrapper { width:220px; min-width:220px; }
.canvas-area { flex:1; display:flex; flex-direction:column; overflow:hidden; }
.canvas-container { flex:1; min-height:0; }
.canvas-status { display:flex; align-items:center; gap:12px; padding:6px 12px; font-size:12px; color:var(--el-text-color-secondary); border-top:1px solid var(--el-border-color-light); background:var(--el-bg-color); }
.valid-badge { color:#67c23a; }
.invalid-badge { color:#f56c6c; }
.property-panel-wrapper { width:300px; min-width:300px; }
.test-sandbox { padding:8px 0; }
.result-header { display:flex; align-items:center; justify-content:space-between; margin-bottom:8px; }
.result-json { background:#f5f7fa; padding:12px; border-radius:4px; font-size:12px; max-height:400px; overflow:auto; white-space:pre-wrap; }
.result-empty { color:var(--el-text-color-muted); text-align:center; padding:40px 0; }
</style>
