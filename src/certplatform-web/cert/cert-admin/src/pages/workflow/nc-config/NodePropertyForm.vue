<template>
  <div class="node-property-form">
    <div v-if="selectedNode" class="node-form">
      <div class="inspector-section">
        <div class="inspector-section-header">
          <span class="inspector-section-title">基础信息</span>
          <el-tooltip v-if="nodeDescription" :content="nodeDescription" placement="left"><el-icon class="help-icon"><Warning /></el-icon></el-tooltip>
        </div>
        <div class="inspector-field"><div class="inspector-label">节点名称</div><el-input v-model="form.title" size="small" @change="applyChanges" /></div>
        <div class="inspector-field"><div class="inspector-label">节点类型</div><div class="node-type-display"><span class="type-dot" :class="nodeTypeTag"></span><span class="type-name">{{ nodeTypeName }}</span></div></div>
      </div>

      <div v-if="visibleInputPorts.length > 0" class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">输入端口 (INPUTS)</span></div>
        <div v-for="port in visibleInputPorts" :key="port.name" class="inspector-field">
          <div class="inspector-label"><span>{{ port.label || port.name }}</span><el-tag v-if="port.required" size="small" type="danger" effect="plain">REQ</el-tag></div>
          <div v-if="port.description" class="port-desc">{{ port.description }}</div>
          <PortControl :key="`port_${port.name}_${form._updateTick || 0}`" :model-value="getInputValue(port.name)" :bind-mode="port.bindMode || 'LinkOrConstant'" :enum-source="port.enumSource" :linkable-nodes="linkableNodes.map((n: any) => ({ id: n.id, label: n.title || n.id }))" :input-type="getInputType(port.name)" :placeholder="getPortPlaceholder(port)" @update:model-value="(v: any) => setInputValue(port.name, v)" @update:input-type="(v: string) => setInputType(port.name, v)" @link-node="({ sourceNodeId }: any) => onLinkNode(port.name, sourceNodeId)" />
        </div>
      </div>

      <div v-if="form.nodeType === 'branch'" class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">分支逻辑</span></div>
        <div class="branch-hint"><el-icon><InfoFilled /></el-icon><span>连接上游比较节点的布尔输出。</span></div>
        <div class="inspector-field"><div class="inspector-label">成功分支目标</div><el-select :model-value="getBranchOutput('success')" placeholder="选择目标节点" size="small" style="width: 100%" @change="(v: string) => setBranchOutput('success', v)"><el-option v-for="n in branchTargetNodes" :key="n.id" :label="n.label" :value="n.id" /></el-select></div>
        <div class="inspector-field"><div class="inspector-label">失败分支目标</div><el-select :model-value="getBranchOutput('failure')" placeholder="选择目标节点" size="small" style="width: 100%" @change="(v: string) => setBranchOutput('failure', v)"><el-option v-for="n in branchTargetNodes" :key="n.id" :label="n.label" :value="n.id" /></el-select></div>
      </div>

      <div v-if="visiblePanelSchema.length > 0 && form.nodeType !== 'branch'" class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">参数配置</span></div>
        <template v-for="field in visiblePanelSchema" :key="field.field">
          <div v-if="field.type !== 'promptWithRef'" class="inspector-field">
            <div class="inspector-label">{{ field.label }}</div>
            <el-input v-if="field.type === 'textarea'" v-model="panelFieldValues[field.field]" type="textarea" :rows="field.rows || 3" size="small" @change="applyPanelField(field)" />
            <el-select v-else-if="field.type === 'doc-select'" v-model="panelFieldValues[field.field]" size="small" style="width: 100%" @change="(v: string) => onDocSelect(field.field, v)"><el-option v-for="d in docList" :key="d.ruleCode" :label="d.fileName || d.standardFileCode" :value="d.ruleCode" /></el-select>
            <el-select v-else-if="field.type === 'field-select'" v-model="panelFieldValues[field.field]" size="small" style="width: 100%" :disabled="!panelFieldValues['config.ruleCode']" @change="applyPanelFields"><el-option v-for="f in fieldList" :key="f.fieldCode" :label="f.fieldName" :value="f.fieldCode" /></el-select>
            <el-switch v-else-if="field.type === 'switch'" v-model="panelFieldValues[field.field]" @change="applyPanelFields" />
            <el-slider v-else-if="field.type === 'slider'" v-model="panelFieldValues[field.field]" :min="field.min || 0" :max="field.max || 1" :step="field.step || 0.1" @change="applyPanelFields" />
          </div>
          <div v-else class="inspector-field"><div class="inspector-label">{{ field.label }}</div><PromptRefEditor v-model="panelFieldValues[field.field]" :rows="field.rows || 6" :linkable-nodes="linkableNodes" @update:model-value="applyPanelField(field)" /></div>
        </template>
      </div>

      <div v-if="visibleOutputPorts.length > 0" class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">输出端口 (OUTPUTS)</span></div>
        <div v-for="out in visibleOutputPorts" :key="out.name" class="output-item"><span class="output-type">{{ out.type }}</span><span class="tree-label">{{ out.label || out.name }}</span></div>
      </div>

      <div class="inspector-section">
        <div class="inspector-section-header"><span class="inspector-section-title">操作</span></div>
        <div v-if="testResult" class="test-result-wrapper"><div :class="['test-result', testResult.success ? 'test-success' : 'test-fail']"><div class="test-header"><el-icon><CircleCheck v-if="testResult.success" /><Warning v-else /></el-icon><span>{{ testResult.success ? '执行成功' : '执行失败' }}</span></div><pre v-if="testResult.data" class="test-output-json">{{ JSON.stringify(testResult.data, null, 2) }}</pre></div></div>
        <div class="action-row">
          <el-button v-if="form.nodeType === 'docField' || form.nodeType === 'docTable'" type="success" size="small" :loading="testLoading" @click="testDocExtract">测试提取</el-button>
          <el-button v-else-if="testable && form.nodeType !== 'start' && form.nodeType !== 'end'" type="primary" size="small" :loading="testLoading" @click="testNode">运行测试</el-button>
          <el-button type="danger" size="small" plain @click="deleteNode">删除节点</el-button>
        </div>
      </div>
    </div>

    <div v-else class="form-empty">
      <div class="empty-icon"><el-icon :size="48"><InfoFilled /></el-icon></div>
      <p class="empty-title">点击画布节点配置属性</p>
      <p class="empty-hint">从左侧节点库拖动节点到画布，或点击节点查看和编辑属性</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { getSpecialNode } from '@share/composables/workflow/specialNodes'
import { CircleCheck, InfoFilled, Warning } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, ref, watch } from 'vue'
import PortControl from './panels/PortControl.vue'
import PromptRefEditor from './panels/PromptRefEditor.vue'

const props = defineProps<{
  selectedNode?: any; skills?: any[]; docRules?: any[]; docFields?: any[]; docTables?: any[]; canvasNodes?: any[]
}>()
const emit = defineEmits<{
  'update-node': [data: any]; 'delete-node': [nodeId: string]; 'load-doc-fields': [ruleCode: string]
  'link-node': [data: any]; 'test-node': [data: any]; 'test-workflow': [data: any]; 'test-doc-extract': [data: any]
}>()

const form = ref<any>({ nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {}, inputPorts: [], outputPorts: [], inputTypes: {} })
const inputValues = ref<Record<string, any>>({})
const inputTypes = ref<Record<string, string>>({})
const panelFieldValues = ref<Record<string, any>>({})
const docList = ref<any[]>([]); const fieldList = ref<any[]>([]); const tableList = ref<any[]>([])
const testResult = ref<any>(null); const testLoading = ref(false)

const specialMeta = computed(() => getSpecialNode(form.value.nodeType))
const nodeTypeName = computed(() => { if (specialMeta.value) return specialMeta.value.className; const sk = (props.skills || []).find((s: any) => s.skillCode === form.value.skillCode); return sk?.skillName || form.value.nodeType })
const nodeTypeTag = computed(() => ({ start: 'success', end: 'danger', branch: 'warning', skill: 'primary', ai_node: 'info', docField: 'success', docTable: 'warning' } as Record<string, string>)[form.value.nodeType] || 'info')
const nodeDescription = computed(() => { if (specialMeta.value) return specialMeta.value.description; return (props.skills || []).find((s: any) => s.skillCode === form.value.skillCode)?.description || '' })
const testable = computed(() => { if (specialMeta.value) return specialMeta.value.testable; return form.value.nodeType === 'skill' })
const visibleInputPorts = computed(() => (form.value.inputPorts || []).filter((p: any) => p.display !== 'hidden'))
const visibleOutputPorts = computed(() => (form.value.outputPorts || []).filter((p: any) => p.display !== 'hidden' && p.role !== 'anchor'))
const visiblePanelSchema = computed(() => specialMeta.value?.panelSchema || [])

const linkableNodes = computed(() => {
  const excludeTypes = new Set(['start', 'end', 'branch'])
  const getOutputPorts = (nodeType: string) => getSpecialNode(nodeType)?.outputPorts?.filter((p: any) => p.display !== 'hidden') || [{ name: 'result', label: '结果', type: 'string' }]
  return (props.canvasNodes || []).filter((n: any) => n && n.id !== form.value.nodeId && !excludeTypes.has(n.nodeType)).map((n: any) => ({ id: n.id, title: n.title || n.id, nodeType: n.nodeType, color: getSpecialNode(n.nodeType)?.color || '', outputPorts: getOutputPorts(n.nodeType) }))
})

const branchTargetNodes = computed(() => (props.canvasNodes || []).filter((n: any) => n.id !== form.value.nodeId).map((n: any) => ({ id: n.id, label: `${n.title || n.id}` })))

function getBranchOutput(handle: string) {
  if (form.value._branchEdges?.length) { const edge = form.value._branchEdges.find((e: any) => e.handle === handle); if (edge) return edge.targetId }
  return form.value.config?.[`_${handle}Target`] || ''
}
function setBranchOutput(handle: string, targetNodeId: string) {
  const config = { ...form.value.config }; config[`_${handle}Target`] = targetNodeId; form.value.config = config
  emit('link-node', { portName: handle, sourceNodeId: targetNodeId ? form.value.nodeId : null, targetNodeId: targetNodeId || form.value.nodeId, sourceHandle: handle })
  applyChanges()
}
function getInputValue(portName: string) { return inputValues.value[portName] ?? '' }
function getInputType(portName: string) {
  if (inputTypes.value[portName]) return inputTypes.value[portName]
  const val = inputValues.value[portName]
  if (val && linkableNodes.value.some((n: any) => n.id === val)) return 'link'
  if (val) return 'constant'
  return 'link'
}
function setInputType(portName: string, type: string) {
  const prevValue = inputValues.value[portName]; const prevType = inputTypes.value[portName]
  inputTypes.value[portName] = type; form.value.inputTypes = { ...inputTypes.value }
  if (prevType === 'link' && type === 'constant' && prevValue) emit('link-node', { portName, sourceNodeId: null, targetNodeId: form.value.nodeId })
  inputValues.value[portName] = ''; const inputs = { ...form.value.inputs }; delete inputs[portName]; form.value.inputs = inputs; applyChanges()
}
function setInputValue(portName: string, value: any) {
  inputValues.value[portName] = value; form.value.inputs = { ...form.value.inputs, [portName]: value }; applyChanges()
}
function onLinkNode(portName: string, sourceNodeId: string | null) {
  inputTypes.value[portName] = 'link'; form.value.inputTypes = { ...inputTypes.value }
  if (!sourceNodeId) { emit('link-node', { portName, sourceNodeId: null, targetNodeId: form.value.nodeId }); inputValues.value[portName] = ''; const inputs = { ...form.value.inputs }; delete inputs[portName]; form.value.inputs = inputs }
  else { emit('link-node', { portName, sourceNodeId, targetNodeId: form.value.nodeId }); inputValues.value[portName] = sourceNodeId; form.value.inputs = { ...form.value.inputs, [portName]: sourceNodeId } }
  applyChanges()
}
function getPortPlaceholder(port: any) { if (port.bindMode === 'Link') return '请在画布上连线'; if (port.bindMode === 'Enum') return '请选择'; return '输入常量或 {{n1.portName}}' }
function applyPanelField(field: any) {
  const config = { ...form.value.config }; const parts = field.field.split('.')
  if (parts.length === 2 && parts[0] === 'config') config[parts[1]] = panelFieldValues.value[field.field]; else config[field.field] = panelFieldValues.value[field.field]
  form.value.config = config; applyChanges()
}
function applyPanelFields() {
  const config = { ...form.value.config }
  for (const [key, val] of Object.entries(panelFieldValues.value)) { const parts = key.split('.'); if (parts.length === 2 && parts[0] === 'config') config[parts[1]] = val }
  form.value.config = config; applyChanges()
}
function onDocSelect(field: string, ruleCode: string) {
  const config = { ...form.value.config }; const parts = field.split('.'); const key = parts.length === 2 ? parts[1] : field; config[key] = ruleCode
  if (key === 'ruleCode' || key === 'docCode') { config.fieldCode = ''; config.tableCode = ''; panelFieldValues.value['config.fieldCode'] = ''; panelFieldValues.value['config.tableCode'] = ''; testResult.value = null }
  form.value.config = config; emit('load-doc-fields', ruleCode); applyChanges()
}
function testNode() {
  testLoading.value = true; testResult.value = null
  emit('test-node', { nodeId: form.value.nodeId, nodeType: form.value.nodeType, title: form.value.title, skillCode: form.value.skillCode, config: form.value.config, inputs: form.value.inputs, inputTypes: form.value.inputTypes, inputPorts: form.value.inputPorts, outputPorts: form.value.outputPorts,
    onSuccess: (data: any) => { testResult.value = { success: true, data }; testLoading.value = false },
    onError: (msg: string, errorData: any) => { testResult.value = { success: false, message: msg, data: errorData }; testLoading.value = false }
  })
}
function deleteNode() { if (form.value.nodeId) emit('delete-node', form.value.nodeId) }
function testDocExtract() {
  const nodeType = form.value.nodeType; const config = form.value.config
  if (!config.ruleCode) { ElMessage.warning('请先选择文档'); return }
  if (nodeType === 'docField' && !config.fieldCode) { ElMessage.warning('请先选择字段'); return }
  if (nodeType === 'docTable' && !config.tableCode) { ElMessage.warning('请先选择表格'); return }
  testLoading.value = true; testResult.value = null
  const body = nodeType === 'docField' ? { ruleCode: config.ruleCode, fieldCode: config.fieldCode, docType: config.docType || 'standard' } : { ruleCode: config.ruleCode, tableCode: config.tableCode, docType: config.docType || 'standard' }
  emit('test-doc-extract', { nodeType, body, onSuccess: (data: any) => { testResult.value = data; testLoading.value = false; ElMessage.success('测试完成') }, onError: (msg: string) => { testResult.value = { success: false, message: msg }; testLoading.value = false; ElMessage.error(msg) } })
}

let _lastNodeId: string | null = null; let _updateTick = 0
watch(() => props.selectedNode, (node: any) => {
  if (node) {
    const currentNodeId = node.nodeId || node.id
    if (_lastNodeId === currentNodeId && form.value.title === node.title) return
    _lastNodeId = currentNodeId; _updateTick++
    form.value = { nodeId: currentNodeId, nodeType: node.nodeType || 'skill', title: node.title || '', skillCode: node.skillCode || '', inputs: { ...(node.inputs || {}) }, outputs: { ...(node.outputs || {}) }, config: { ...(node.config || {}) }, inputPorts: node.inputPorts || [], outputPorts: node.outputPorts || [], inputTypes: { ...(node.inputTypes || {}) }, _branchEdges: node.branchEdges || [], _updateTick }
    const vals: Record<string, any> = {}; const types: Record<string, string> = {}
    for (const port of form.value.inputPorts) {
      vals[port.name] = form.value.inputs[port.name] ?? ''
      if (form.value.inputTypes[port.name]) types[port.name] = form.value.inputTypes[port.name]
      else { const val = vals[port.name]; if (val && (props.canvasNodes || []).some((n: any) => n.id === val)) types[port.name] = 'link'; else if (val) types[port.name] = 'constant'; else types[port.name] = 'link' }
    }
    inputValues.value = vals; inputTypes.value = types
    const pfv: Record<string, any> = {}; const schema = specialMeta.value?.panelSchema || []
    for (const field of schema) { const parts = field.field.split('.'); if (parts.length === 2 && parts[0] === 'config') pfv[field.field] = form.value.config[parts[1]] ?? field.defaultValue ?? ''; else pfv[field.field] = form.value.config[field.field] ?? field.defaultValue ?? '' }
    panelFieldValues.value = pfv; docList.value = props.docRules || []; fieldList.value = props.docFields || []; tableList.value = props.docTables || []
    if ((node.nodeType === 'docField' || node.nodeType === 'docTable') && (node.config?.ruleCode || node.config?.docCode)) emit('load-doc-fields', node.config.ruleCode || node.config.docCode)
  } else { _lastNodeId = null; form.value = { nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {}, inputPorts: [], outputPorts: [], inputTypes: {} }; inputValues.value = {}; inputTypes.value = {}; panelFieldValues.value = {} }
}, { immediate: true })

watch(() => props.docRules, (rules) => { docList.value = rules || [] }, { immediate: true })
watch(() => props.docFields, (fields) => { fieldList.value = fields || [] }, { immediate: true })
watch(() => props.docTables, (tables) => { tableList.value = tables || [] }, { immediate: true })

function applyChanges() {
  if (!form.value.nodeId) return
  emit('update-node', { nodeId: form.value.nodeId, nodeType: form.value.nodeType, classCode: form.value.nodeType, title: form.value.title, skillCode: form.value.skillCode, inputs: { ...inputValues.value }, inputTypes: { ...inputTypes.value }, outputs: form.value.outputs, config: form.value.config, inputPorts: form.value.inputPorts, outputPorts: form.value.outputPorts })
}
</script>

<style scoped lang="less">
.node-property-form { display: flex; flex-direction: column; height: 100%; overflow: hidden; background: #fff; }
.node-form { padding: 16px; overflow-y: auto; flex: 1; }
.inspector-section { margin-bottom: 16px; }
.inspector-section-header { display: flex; align-items: center; justify-content: space-between; margin-bottom: 8px; padding-bottom: 6px; border-bottom: 1px solid #f1f5f9; }
.inspector-section-title { font-size: 13px; font-weight: 700; color: #334155; }
.inspector-field { margin-bottom: 10px; }
.inspector-label { font-size: 12px; color: #64748b; margin-bottom: 4px; display: flex; align-items: center; gap: 6px; }
.node-type-display { display: flex; align-items: center; gap: 6px; }
.type-dot { width: 8px; height: 8px; border-radius: 50%; }
.type-dot.success { background: #67C23A; }
.type-dot.danger { background: #F56C6C; }
.type-dot.warning { background: #E6A23C; }
.type-dot.primary { background: #409EFF; }
.type-dot.info { background: #909399; }
.port-desc { font-size: 11px; color: #94a3b8; margin-bottom: 4px; }
.branch-hint { display: flex; gap: 8px; align-items: flex-start; padding: 12px; background: #fffbeb; border: 1px solid #fef3c7; border-radius: 2px; font-size: 12px; color: #92400e; line-height: 1.5; margin-bottom: 16px; .el-icon { margin-top: 2px; flex-shrink: 0; font-size: 14px; } }
.output-item { display: flex; align-items: center; gap: 8px; margin-bottom: 8px; background: #f8fafc; padding: 8px 12px; border: 1px dashed #e2e8f0; border-radius: 2px; }
.output-type { color: var(--yzh-color-primary); font-family: monospace; font-weight: 700; font-size: 11px; }
.help-icon { color: #94a3b8; cursor: pointer; font-size: 16px; &:hover { color: var(--yzh-color-primary); } }
.test-result-wrapper { margin-bottom: 12px; }
.test-result { padding: 12px; border-radius: 4px; font-size: 12px; border: 1px solid #e2e8f0; }
.test-result.test-success { background: #f0fdf4; border-color: #dcfce7; color: #166534; }
.test-result.test-fail { background: #fef2f2; border-color: #fee2e2; color: #991b1b; }
.test-header { display: flex; align-items: center; gap: 6px; font-weight: 700; margin-bottom: 8px; }
.test-output-json { background: #f5f7fa; padding: 8px; border-radius: 4px; font-size: 11px; font-family: monospace; max-height: 150px; overflow-y: auto; margin: 0; }
.action-row { display: flex; gap: 8px; margin-top: 12px; .el-button { flex: 1; height: 32px; border-radius: 2px; font-weight: 700; } }
.form-empty { display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100%; padding: 32px; text-align: center; }
.empty-icon { color: #e2e8f0; margin-bottom: 16px; }
.empty-title { font-size: 15px; color: #64748b; font-weight: 700; margin: 0 0 8px; }
.empty-hint { font-size: 12px; color: #94a3b8; margin: 0; line-height: 1.5; }
</style>
