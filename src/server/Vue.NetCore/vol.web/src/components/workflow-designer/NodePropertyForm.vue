<template>
  <div class="node-property-form">
    <div v-if="selectedNode" class="node-form">
      <!-- ===== 标题栏：类型标签 + 名称 + 帮助 ===== -->
      <div class="form-title">
        <el-tag :type="nodeTypeTag" size="small">{{ nodeTypeName }}</el-tag>
        <span class="node-title-display">{{ props.selectedNode?.title || form.title }}</span>
        <el-tooltip v-if="nodeDescription" :content="nodeDescription" placement="left">
          <el-icon class="help-icon"><IconWarning /></el-icon>
        </el-tooltip>
      </div>

      <el-form label-width="80px" size="small">
        <!-- ===== 功能节点：Skill 编码 + 说明 ===== -->
        <template v-if="isSkillNode">
          <el-form-item label="Skill">
            <el-input :model-value="form.skillCode" disabled />
          </el-form-item>
          <el-form-item label="说明">
            <el-input :model-value="skillDesc" type="textarea" :rows="2" disabled />
          </el-form-item>
        </template>

        <!-- ===== 输入端口（按 bindMode 统一渲染） ===== -->
        <template v-if="visibleInputPorts.length > 0">
          <el-divider content-position="left">输入端口</el-divider>
          <div v-for="port in visibleInputPorts" :key="port.name" class="port-row">
            <div class="port-label">
              <span class="port-name">{{ port.label || port.name }}</span>
              <el-tag v-if="port.required" size="small" type="danger">必填</el-tag>
              <el-tag size="small" :type="getBindModeTagType(port.bindMode)">{{ getBindModeLabel(port.bindMode) }}</el-tag>
            </div>
            <div v-if="port.description" class="port-desc">{{ port.description }}</div>
            <div class="port-value">
              <PortControl
                :model-value="getInputValue(port.name)"
                :bind-mode="port.bindMode || 'LinkOrConstant'"
                :enum-source="port.enumSource"
                :linkable-nodes="linkableNodes"
                :placeholder="getPortPlaceholder(port)"
                @update:model-value="v => setInputValue(port.name, v)"
                @link-node="({ sourceNodeId }) => onLinkNode(port.name, sourceNodeId)"
              />
            </div>
          </div>
        </template>

        <!-- ===== branch 条件编辑器 ===== -->
        <template v-if="form.nodeType === 'branch'">
          <el-divider content-position="left">分支条件</el-divider>
          <div class="branch-hint">
            <el-icon><IconInfo /></el-icon>
            <span>条件分支节点不包含比较逻辑。请将上游 <strong>compare</strong> 节点的布尔输出连接到此节点的「条件值」输入端口。</span>
          </div>
          <div v-if="getInputValue('condition')" class="branch-source">
            <el-tag type="success" size="small">
              <el-icon><IconEdit /></el-icon>
              条件来源：{{ getLinkedNodeTitle(getInputValue('condition')) }}
            </el-tag>
          </div>
          <div v-else class="branch-warning">
            <el-icon><IconWarning /></el-icon>
            <span>尚未绑定条件输入，请在画布上连线或从下拉选择</span>
          </div>

          <!-- 输出分支选择 -->
          <el-divider content-position="left">输出分支</el-divider>
          <div class="branch-output-row">
            <div class="branch-output-item">
              <el-tag type="success" size="small">✅ 成功</el-tag>
              <el-select
                :model-value="getBranchOutput('success')"
                placeholder="选择成功分支目标"
                style="flex: 1"
                @change="v => setBranchOutput('success', v)"
              >
                <el-option v-for="n in branchTargetNodes" :key="n.id" :label="n.label" :value="n.id" />
              </el-select>
            </div>
            <div class="branch-output-item">
              <el-tag type="danger" size="small">❌ 失败</el-tag>
              <el-select
                :model-value="getBranchOutput('failure')"
                placeholder="选择失败分支目标"
                style="flex: 1"
                @change="v => setBranchOutput('failure', v)"
              >
                <el-option v-for="n in branchTargetNodes" :key="n.id" :label="n.label" :value="n.id" />
              </el-select>
            </div>
          </div>
        </template>

        <!-- ===== 节点特有配置（panelSchema） ===== -->
        <template v-if="visiblePanelSchema.length > 0 && form.nodeType !== 'branch'">
          <el-divider content-position="left">节点配置</el-divider>
          <template v-for="field in visiblePanelSchema" :key="field.field">
            <!-- textarea（AI prompt 等） -->
            <el-form-item v-if="field.type === 'textarea'" :label="field.label">
              <el-input
                v-model="panelFieldValues[field.field]"
                type="textarea"
                :rows="6"
                :placeholder="field.description || ''"
                @change="applyPanelField(field)"
              />
              <div v-if="field.description" class="field-hint">{{ field.description }}</div>
            </el-form-item>
            <!-- 文档选择 -->
            <el-form-item v-else-if="field.type === 'doc-select'" :label="field.label">
              <el-select
                v-model="panelFieldValues[field.field]"
                placeholder="选择文档"
                style="width: 100%"
                @change="v => onDocSelect(field.field, v)"
              >
                <el-option v-for="d in docList" :key="d.ruleCode" :label="d.fileName || d.standardFileCode" :value="d.ruleCode" />
              </el-select>
            </el-form-item>
            <!-- 字段选择 -->
            <el-form-item v-else-if="field.type === 'field-select'" :label="field.label">
              <el-select
                v-model="panelFieldValues[field.field]"
                placeholder="选择字段"
                style="width: 100%"
                :disabled="!panelFieldValues['config.ruleCode']"
                @change="applyPanelFields"
              >
                <el-option v-for="f in fieldList" :key="f.fieldCode" :label="`${f.fieldName} (${f.fieldCode})`" :value="f.fieldCode" />
              </el-select>
            </el-form-item>
            <!-- 表格选择 -->
            <el-form-item v-else-if="field.type === 'table-select'" :label="field.label">
              <el-select
                v-model="panelFieldValues[field.field]"
                placeholder="选择表格"
                style="width: 100%"
                :disabled="!panelFieldValues['config.ruleCode']"
                @change="applyPanelFields"
              >
                <el-option v-for="t in tableList" :key="t.tableCode" :label="`${t.tableName} (${t.tableCode})`" :value="t.tableCode" />
              </el-select>
            </el-form-item>
            <!-- select -->
            <el-form-item v-else-if="field.type === 'select'" :label="field.label">
              <el-select
                v-model="panelFieldValues[field.field]"
                style="width: 100%"
                @change="applyPanelFields"
              >
                <el-option v-for="opt in field.options" :key="opt.value" :label="opt.label" :value="opt.value" />
              </el-select>
              <div v-if="field.description" class="field-hint">{{ field.description }}</div>
            </el-form-item>
            <!-- switch -->
            <el-form-item v-else-if="field.type === 'switch'" :label="field.label">
              <el-switch v-model="panelFieldValues[field.field]" @change="applyPanelFields" />
              <span v-if="field.description" class="field-hint-inline">{{ field.description }}</span>
            </el-form-item>
          </template>
        </template>

        <!-- ===== 输出端口（只读提示） ===== -->
        <template v-if="visibleOutputPorts.length > 0">
          <el-divider content-position="left">输出端口</el-divider>
          <div v-for="out in visibleOutputPorts" :key="out.name" class="output-item">
            <el-tag size="small" type="info">{{ out.label || out.name }}</el-tag>
            <span class="output-type">{{ out.type }}</span>
            <span v-if="out.description" class="output-desc">{{ out.description }}</span>
          </div>
        </template>

        <!-- ===== AI 节点底部提示 ===== -->
        <div v-if="form.nodeType === 'ai_node'" class="ai-output-hint">
          执行后自动输出结果，下游节点可通过 <code>{{ aiOutputRefSyntax }}</code> 引用
        </div>

        <!-- ===== start 节点帮助信息 ===== -->
        <div v-if="form.nodeType === 'start'" class="start-hint">
          <el-icon><IconInfo /></el-icon>
          <span>开始节点是工作流起点。运行时引擎自动注入上下文参数（企业编码、标准编码、阶段编码、文件编码），无需手动配置。</span>
        </div>

        <!-- ===== end 节点帮助信息 ===== -->
        <div v-if="form.nodeType === 'end'" class="end-hint">
          <el-icon><IconInfo /></el-icon>
          <span>结束节点汇聚上游路径结果。可允许多个上游连线（多分支汇聚），执行到此节点时输出上游节点结果作为最终输出。</span>
        </div>

        <!-- ===== docField/docTable 测试结果 ===== -->
        <template v-if="form.nodeType === 'docField' || form.nodeType === 'docTable'">
          <el-divider content-position="left">测试结果</el-divider>
          <div v-if="testLoading" class="test-loading">
            <el-icon class="is-loading"><IconPlay /></el-icon>
            <span>正在测试提取...</span>
          </div>
          <div v-else-if="testResult" class="test-result" :class="{ 'test-success': testResult.success, 'test-fail': !testResult.success }">
            <div v-if="testResult.success && testResult.data" class="test-data">
              <div class="test-field-row">
                <span class="test-label">{{ form.nodeType === 'docField' ? '字段值' : '表格数据' }}：</span>
                <span v-if="form.nodeType === 'docField'" class="test-value">{{ testResult.data.fieldValue }}</span>
                <pre v-else class="test-table-data">{{ JSON.stringify(testResult.data.rows, null, 2) }}</pre>
              </div>
              <div v-if="testResult.data.confidence !== undefined" class="test-field-row">
                <span class="test-label">置信度：</span>
                <span class="test-value">{{ (testResult.data.confidence * 100).toFixed(0) }}%</span>
              </div>
              <div v-if="testResult.data.source" class="test-field-row">
                <span class="test-label">来源：</span>
                <span class="test-value test-source">{{ testResult.data.source }}</span>
              </div>
            </div>
            <div v-else-if="!testResult.success" class="test-error">
              <el-icon><IconWarning /></el-icon>
              <span>{{ testResult.message || '测试失败' }}</span>
            </div>
          </div>
          <div v-else class="test-empty">
            <span>配置完成后点击下方按钮测试提取结果</span>
          </div>
        </template>

        <!-- ===== 操作区 ===== -->
        <el-divider content-position="left">操作</el-divider>
        <div class="action-row">
          <el-button
            v-if="(form.nodeType === 'docField' || form.nodeType === 'docTable')"
            type="success" size="small"
            :loading="testLoading"
            @click="testDocExtract"
          >
            <el-icon v-if="!testLoading"><IconPlay /></el-icon> 测试提取
          </el-button>
          <el-button v-else-if="testable && form.nodeType !== 'start' && form.nodeType !== 'end'" type="primary" size="small" @click="testNode">
            <el-icon><IconPlay /></el-icon> 测试节点
          </el-button>
          <el-button
            v-if="form.nodeType !== 'start'"
            type="danger"
            size="small"
            @click="deleteNode"
          >
            <el-icon><IconDelete /></el-icon> 删除节点
          </el-button>
        </div>
      </el-form>
    </div>

    <!-- ===== 空状态 ===== -->
    <div v-else class="form-empty">
      <div class="empty-icon">
        <el-icon :size="48"><IconInfo /></el-icon>
      </div>
      <p class="empty-title">点击画布节点配置属性</p>
      <p class="empty-hint">从左侧节点库拖动节点到画布，或点击节点查看和编辑属性</p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { ElMessage } from 'element-plus'
import { IconWarning, IconEdit, IconPlay, IconDelete, IconInfo } from '@/yzh/icons'
import { getSpecialNode } from '@/views/cert/Standard/WorkflowDesigner/specialNodes.js'
import PortControl from './panels/PortControl.vue'

const props = defineProps({
  selectedNode: { type: Object, default: null },
  skills: { type: Array, default: () => [] },
  docRules: { type: Array, default: () => [] },
  docFields: { type: Array, default: () => [] },
  docTables: { type: Array, default: () => [] },
  canvasNodes: { type: Array, default: () => [] }
})

const emit = defineEmits(['update-node', 'delete-node', 'load-doc-fields', 'link-node', 'test-node', 'test-doc-extract'])

const form = ref({ nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {}, inputPorts: [], outputPorts: [] })
const inputValues = ref({})
const panelFieldValues = ref({})
const docList = ref([])
const fieldList = ref([])
const tableList = ref([])

// docField/docTable 测试结果
const testResult = ref(null)
const testLoading = ref(false)

// ===== 计算属性 =====

const isSkillNode = computed(() => form.value.nodeType === 'skill')

const specialMeta = computed(() => getSpecialNode(form.value.nodeType))

const nodeTypeName = computed(() => {
  if (specialMeta.value) return specialMeta.value.className
  const sk = props.skills.find(s => s.skillCode === form.value.skillCode)
  return sk?.skillName || form.value.nodeType
})

const nodeTypeTag = computed(() => ({
  start: 'success', end: 'danger', branch: 'warning', skill: 'primary',
  ai_node: 'info', loop: 'info', docField: 'success', docTable: 'warning'
}[form.value.nodeType] || 'info'))

const nodeDescription = computed(() => {
  if (specialMeta.value) return specialMeta.value.description
  const sk = props.skills.find(s => s.skillCode === form.value.skillCode)
  return sk?.description || ''
})

const testable = computed(() => {
  if (specialMeta.value) return specialMeta.value.testable
  return form.value.nodeType === 'skill'
})

const aiOutputRefSyntax = computed(() => {
  const title = form.value.title || 'ai'
  return `{{${title}.content}}`
})

const visibleInputPorts = computed(() => {
  const ports = form.value.inputPorts || []
  return ports.filter(p => p.display !== 'hidden')
})

const visibleOutputPorts = computed(() => {
  const ports = form.value.outputPorts || []
  return ports.filter(p => p.display !== 'hidden' && p.role !== 'anchor')
})

const visiblePanelSchema = computed(() => {
  if (specialMeta.value) return specialMeta.value.panelSchema || []
  return []
})

const skillDesc = computed(() => {
  const sk = props.skills.find(s => s.skillCode === form.value.skillCode)
  return sk?.description || ''
})

const linkableNodes = computed(() => {
  return props.canvasNodes
    .filter(n => n.id !== form.value.nodeId)
    .map(n => ({
      id: n.id,
      label: `${n.title || n.id}${n.nodeType ? ' (' + n.nodeType + ')' : ''}`
    }))
})

/** branch 节点可选的目标节点（排除自身） */
const branchTargetNodes = computed(() => {
  return props.canvasNodes
    .filter(n => n.id !== form.value.nodeId)
    .map(n => ({
      id: n.id,
      label: `${n.title || n.id}`
    }))
})

/** 获取 branch 指定输出分支的目标节点 */
function getBranchOutput(handle) {
  // 优先从 selectedNode.branchEdges 中读取
  if (form.value._branchEdges?.length) {
    const edge = form.value._branchEdges.find(e => e.handle === handle)
    if (edge) return edge.targetId
  }
  return form.value.config?.[`_${handle}Target`] || ''
}

/** 设置 branch 输出分支目标 → emit link-node 事件 */
function setBranchOutput(handle, targetNodeId) {
  // 保存到 config（用于恢复）
  const config = { ...form.value.config }
  config[`_${handle}Target`] = targetNodeId
  form.value.config = config
  // emit 到父组件创建/更新边
  emit('link-node', {
    portName: handle,
    sourceNodeId: targetNodeId ? form.value.nodeId : null,
    targetNodeId: targetNodeId || form.value.nodeId,
    sourceHandle: handle
  })
  applyChanges()
}

// ===== 输入端口值管理 =====

function getInputValue(portName) {
  return inputValues.value[portName] ?? ''
}

function setInputValue(portName, value) {
  inputValues.value[portName] = value
  const inputs = { ...form.value.inputs, [portName]: value }
  form.value.inputs = inputs
  applyChanges()
}

function onLinkNode(portName, sourceNodeId) {
  if (!sourceNodeId) {
    emit('link-node', { portName, sourceNodeId: null, targetNodeId: form.value.nodeId })
    inputValues.value[portName] = ''
    const inputs = { ...form.value.inputs }
    delete inputs[portName]
    form.value.inputs = inputs
    applyChanges()
  } else {
    emit('link-node', { portName, sourceNodeId, targetNodeId: form.value.nodeId })
    inputValues.value[portName] = sourceNodeId
    const inputs = { ...form.value.inputs, [portName]: sourceNodeId }
    form.value.inputs = inputs
    applyChanges()
  }
}

function getPortPlaceholder(port) {
  if (port.bindMode === 'Link') return '请在画布上连线'
  if (port.bindMode === 'Enum') return '请选择'
  return '输入常量或 {{n1.portName}}'
}

/** 获取已连线节点的标题 */
function getLinkedNodeTitle(nodeId) {
  if (!nodeId) return ''
  const node = props.canvasNodes.find(n => n.id === nodeId)
  return node ? (node.title || node.id) : nodeId
}

// ===== 绑定模式标签 =====

function getBindModeLabel(mode) {
  return { Link: '仅连线', LinkOrConstant: '可连线', Enum: '字典' }[mode] || '可连线'
}

function getBindModeTagType(mode) {
  return { Link: 'danger', LinkOrConstant: '', Enum: 'success' }[mode] || ''
}

// ===== panelSchema 字段 =====

function applyPanelField(field) {
  const config = { ...form.value.config }
  const parts = field.field.split('.')
  if (parts.length === 2 && parts[0] === 'config') {
    config[parts[1]] = panelFieldValues.value[field.field]
  } else {
    config[field.field] = panelFieldValues.value[field.field]
  }
  form.value.config = config
  applyChanges()
}

function applyPanelFields() {
  const config = { ...form.value.config }
  for (const [key, val] of Object.entries(panelFieldValues.value)) {
    const parts = key.split('.')
    if (parts.length === 2 && parts[0] === 'config') {
      config[parts[1]] = val
    }
  }
  form.value.config = config
  applyChanges()
}

function onDocSelect(field, ruleCode) {
  const config = { ...form.value.config }
  const parts = field.split('.')
  const key = parts.length === 2 ? parts[1] : field
  config[key] = ruleCode
  // 选择新文档时清空字段/表格
  if (key === 'ruleCode' || key === 'docCode') {
    config.fieldCode = ''
    config.tableCode = ''
    panelFieldValues.value['config.fieldCode'] = ''
    panelFieldValues.value['config.tableCode'] = ''
    testResult.value = null // 清空测试结果
  }
  form.value.config = config
  emit('load-doc-fields', ruleCode)
  applyChanges()
}

// ===== 测试/删除 =====

function testNode() {
  emit('test-node', {
    nodeId: form.value.nodeId,
    nodeType: form.value.nodeType,
    title: form.value.title,
    skillCode: form.value.skillCode,
    config: form.value.config,
    inputs: form.value.inputs
  })
}

function deleteNode() {
  if (form.value.nodeId) emit('delete-node', form.value.nodeId)
}

// ===== docField/docTable 测试 =====

function testDocExtract() {
  const nodeType = form.value.nodeType
  const config = form.value.config
  if (!config.ruleCode) {
    ElMessage.warning('请先选择文档')
    return
  }
  if (nodeType === 'docField' && !config.fieldCode) {
    ElMessage.warning('请先选择字段')
    return
  }
  if (nodeType === 'docTable' && !config.tableCode) {
    ElMessage.warning('请先选择表格')
    return
  }

  testLoading.value = true
  testResult.value = null

  const body = nodeType === 'docField'
    ? { ruleCode: config.ruleCode, fieldCode: config.fieldCode, docType: config.docType || 'standard' }
    : { ruleCode: config.ruleCode, tableCode: config.tableCode, docType: config.docType || 'standard' }

  // emit 到父组件处理 API 调用（父组件有 proxy.http）
  emit('test-doc-extract', {
    nodeType,
    body,
    onSuccess: (data) => {
      testResult.value = data
      testLoading.value = false
      ElMessage.success('测试完成')
    },
    onError: (msg) => {
      testResult.value = { success: false, message: msg }
      testLoading.value = false
      ElMessage.error(msg)
    }
  })
}

// ===== watch =====

watch(() => props.docRules, (rules) => { docList.value = rules || [] }, { immediate: true })
watch(() => props.docFields, (fields) => { fieldList.value = fields || [] }, { immediate: true })
watch(() => props.docTables, (tables) => { tableList.value = tables || [] }, { immediate: true })

// 记录上次处理的 nodeId + title 快照，避免完全相同的重复通知
let _lastSnapshot = null

watch(() => props.selectedNode, (node) => {
  if (node) {
    // 生成快照用于去重比较（只对比关键字段）
    const snap = `${node.nodeId || node.id}|${node.title || ''}|${node.nodeType || ''}`
    if (_lastSnapshot === snap) return
    _lastSnapshot = snap
    form.value = {
      nodeId: node.nodeId || node.id,
      nodeType: node.nodeType || 'skill',
      title: node.title || '',
      skillCode: node.skillCode || '',
      inputs: { ...(node.inputs || {}) },
      outputs: { ...(node.outputs || {}) },
      config: { ...(node.config || {}) },
      inputPorts: node.inputPorts || [],
      outputPorts: node.outputPorts || [],
      _branchEdges: node.branchEdges || []
    }
    // 触发 form 内部响应式更新（确保 title 等字段被模板正确读取）
    const vals = {}
    for (const port of form.value.inputPorts) {
      vals[port.name] = form.value.inputs[port.name] ?? ''
    }
    inputValues.value = vals

    // panelSchema 字段值
    const pfv = {}
    const schema = specialMeta.value?.panelSchema || []
    for (const field of schema) {
      const parts = field.field.split('.')
      if (parts.length === 2 && parts[0] === 'config') {
        pfv[field.field] = form.value.config[parts[1]] ?? field.defaultValue ?? ''
      } else {
        pfv[field.field] = form.value.config[field.field] ?? field.defaultValue ?? ''
      }
    }
    panelFieldValues.value = pfv

    docList.value = props.docRules || []
    fieldList.value = props.docFields || []
    tableList.value = props.docTables || []

    if ((node.nodeType === 'docField' || node.nodeType === 'docTable') && (node.config?.ruleCode || node.config?.docCode)) {
      emit('load-doc-fields', node.config.ruleCode || node.config.docCode)
    }
  } else {
    form.value = { nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {}, inputPorts: [], outputPorts: [] }
    inputValues.value = {}
    panelFieldValues.value = {}
  }
}, { immediate: true, deep: true })

function applyChanges() {
  if (!form.value.nodeId) return
  const inputs = { ...inputValues.value }
  // 始终从 form.value.title 读取（watch 已确保与 store 同步）
  emit('update-node', {
    nodeId: form.value.nodeId,
    nodeType: form.value.nodeType,
    classCode: form.value.nodeType,
    title: form.value.title,
    skillCode: form.value.skillCode,
    inputs,
    outputs: form.value.outputs,
    config: form.value.config,
    inputPorts: form.value.inputPorts,
    outputPorts: form.value.outputPorts
  })
}
</script>

<style scoped lang="less">
.node-property-form { display: flex; flex-direction: column; height: 100%; overflow: hidden; }
.node-form { padding: 12px; overflow-y: auto; flex: 1; }

.form-title { display: flex; align-items: center; gap: 8px; margin-bottom: 12px; }
.node-title-display { font-size: 14px; font-weight: 600; color: #303133; flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.help-icon { color: #909399; cursor: pointer; flex-shrink: 0; }

.port-row {
  margin-bottom: 12px; padding: 8px; background: #f9fafc; border-radius: 6px; border: 1px solid #ebeef5;
  &:hover { border-color: #d0d7de; }
}
.port-label { display: flex; align-items: center; gap: 6px; margin-bottom: 4px; }
.port-name { font-family: 'SF Mono', Monaco, monospace; font-size: 13px; font-weight: 600; color: #303133; }
.port-desc { font-size: 12px; color: #909399; margin-bottom: 6px; }

/* branch 条件提示 */
.branch-hint {
  display: flex; gap: 6px; align-items: flex-start; padding: 8px 10px;
  background: #FDF6EC; border-radius: 4px; font-size: 12px; color: #E6A23C; line-height: 1.5;
  .el-icon { margin-top: 2px; flex-shrink: 0; }
}
.branch-source { margin-top: 8px; }
.branch-warning {
  display: flex; gap: 6px; align-items: center; margin-top: 8px;
  font-size: 12px; color: #F56C6C;
}
.branch-output-row { display: flex; flex-direction: column; gap: 8px; }
.branch-output-item { display: flex; align-items: center; gap: 8px; }

.field-hint { font-size: 11px; color: #c0c4cc; margin-top: 4px; }
.field-hint-inline { font-size: 11px; color: #c0c4cc; margin-left: 8px; }

.output-item { display: flex; align-items: center; gap: 6px; margin-bottom: 6px; font-size: 12px; }
.output-type { color: #409EFF; font-family: monospace; }
.output-desc { color: #909399; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.ai-output-hint { font-size: 11px; color: #9C27B0; background: #F3E5F5; padding: 6px 8px; border-radius: 4px; margin: 8px 0; }
.start-hint, .end-hint {
  display: flex; gap: 6px; align-items: flex-start; padding: 8px 10px;
  border-radius: 4px; font-size: 12px; line-height: 1.5; margin: 8px 0;
  .el-icon { margin-top: 2px; flex-shrink: 0; }
}
.start-hint { background: #F0F9EB; color: #67C23A; }
.end-hint { background: #FEF0F0; color: #F56C6C; }

/* 测试结果 */
.test-loading { display: flex; align-items: center; gap: 6px; padding: 8px; color: #409EFF; font-size: 12px; }
.test-result { padding: 8px; border-radius: 6px; font-size: 12px; }
.test-result.test-success { background: #F0F9EB; border: 1px solid #C2E7B0; }
.test-result.test-fail { background: #FEF0F0; border: 1px solid #FBC4C4; }
.test-data { }
.test-field-row { display: flex; align-items: flex-start; gap: 6px; margin-bottom: 4px; }
.test-label { color: #606266; font-weight: 500; flex-shrink: 0; }
.test-value { color: #303133; word-break: break-all; }
.test-source { color: #67C23A; font-style: italic; }
.test-table-data { margin: 0; padding: 6px; background: #fff; border-radius: 4px; font-size: 11px; max-height: 150px; overflow-y: auto; white-space: pre-wrap; word-break: break-all; }
.test-error { display: flex; align-items: center; gap: 6px; color: #F56C6C; }
.test-empty { padding: 8px; color: #c0c4cc; font-size: 12px; text-align: center; }

.action-row { display: flex; gap: 8px; }

/* 空状态 */
.form-empty {
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  height: 100%; padding: 20px; text-align: center;
}
.empty-icon { color: #dcdfe6; margin-bottom: 12px; }
.empty-title { font-size: 14px; color: #909399; font-weight: 500; margin: 0 0 6px; }
.empty-hint { font-size: 12px; color: #c0c4cc; margin: 0; }
</style>
