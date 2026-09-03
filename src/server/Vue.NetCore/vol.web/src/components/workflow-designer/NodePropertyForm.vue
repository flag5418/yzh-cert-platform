<template>
  <div class="node-property-form">
    <div v-if="selectedNode" class="node-form">
      <!-- ===== 1. 基础信息 Section ===== -->
      <div class="inspector-section">
        <div class="inspector-section-header">
          <span class="inspector-section-title">基础信息</span>
          <el-tooltip v-if="nodeDescription" :content="nodeDescription" placement="left">
            <el-icon class="help-icon"><IconWarning /></el-icon>
          </el-tooltip>
        </div>
        <div class="inspector-field">
          <div class="inspector-label">节点名称</div>
          <el-input v-model="form.title" size="small" @change="applyChanges" />
        </div>
        <div class="inspector-field">
          <div class="inspector-label">节点类型</div>
          <div class="node-type-display">
            <span class="type-dot" :class="nodeTypeTag"></span>
            <span class="type-name">{{ nodeTypeName }}</span>
          </div>
        </div>
      </div>

      <!-- ===== 2. 输入配置 Section ===== -->
      <div v-if="visibleInputPorts.length > 0" class="inspector-section">
        <div class="inspector-section-header">
          <span class="inspector-section-title">输入端口 (INPUTS)</span>
        </div>
        <div v-for="port in visibleInputPorts" :key="port.name" class="inspector-field">
          <div class="inspector-label">
            <span>{{ port.label || port.name }}</span>
            <el-tag v-if="port.required" size="small" type="danger" effect="plain">REQ</el-tag>
          </div>
          <div v-if="port.description" class="port-desc">{{ port.description }}</div>
          <PortControl
            :key="`port_${port.name}_${form._updateTick || 0}`"
            :model-value="getInputValue(port.name)"
            :bind-mode="port.bindMode || 'LinkOrConstant'"
            :enum-source="port.enumSource"
            :linkable-nodes="linkableNodes"
            :input-type="getInputType(port.name)"
            :placeholder="getPortPlaceholder(port)"
            @update:model-value="(v) => setInputValue(port.name, v)"
            @update:input-type="(v) => setInputType(port.name, v)"
            @link-node="({ sourceNodeId }) => onLinkNode(port.name, sourceNodeId)"
          />
        </div>
      </div>

      <!-- ===== 3. 分支逻辑 Section (仅 branch) ===== -->
      <div v-if="form.nodeType === 'branch'" class="inspector-section">
        <div class="inspector-section-header">
          <span class="inspector-section-title">分支逻辑</span>
        </div>
        <div class="branch-hint">
          <el-icon><IconInfo /></el-icon>
          <span>连接上游比较节点的布尔输出。</span>
        </div>
        <div class="inspector-field">
          <div class="inspector-label">成功分支目标</div>
          <el-select
            :model-value="getBranchOutput('success')"
            placeholder="选择目标节点"
            size="small"
            style="width: 100%"
            @change="(v) => setBranchOutput('success', v)"
          >
            <el-option v-for="n in branchTargetNodes" :key="n.id" :label="n.label" :value="n.id" />
          </el-select>
        </div>
        <div class="inspector-field">
          <div class="inspector-label">失败分支目标</div>
          <el-select
            :model-value="getBranchOutput('failure')"
            placeholder="选择目标节点"
            size="small"
            style="width: 100%"
            @change="(v) => setBranchOutput('failure', v)"
          >
            <el-option v-for="n in branchTargetNodes" :key="n.id" :label="n.label" :value="n.id" />
          </el-select>
        </div>
      </div>

      <!-- ===== 4. 节点特定配置 Section ===== -->
      <div
        v-if="visiblePanelSchema.length > 0 && form.nodeType !== 'branch'"
        class="inspector-section"
      >
        <div class="inspector-section-header">
          <span class="inspector-section-title">参数配置</span>
        </div>
        <template v-for="field in visiblePanelSchema" :key="field.field">
          <div v-if="field.type !== 'promptWithRef'" class="inspector-field">
            <div class="inspector-label">{{ field.label }}</div>
            <!-- 不同类型的渲染 -->
            <el-input
              v-if="field.type === 'textarea'"
              v-model="panelFieldValues[field.field]"
              type="textarea"
              :rows="field.rows || 3"
              size="small"
              @change="applyPanelField(field)"
            />
            <el-select
              v-else-if="field.type === 'doc-select'"
              v-model="panelFieldValues[field.field]"
              size="small"
              style="width: 100%"
              @change="(v) => onDocSelect(field.field, v)"
            >
              <el-option
                v-for="d in docList"
                :key="d.ruleCode"
                :label="d.fileName || d.standardFileCode"
                :value="d.ruleCode"
              />
            </el-select>
            <el-select
              v-else-if="field.type === 'field-select'"
              v-model="panelFieldValues[field.field]"
              size="small"
              style="width: 100%"
              :disabled="!panelFieldValues['config.ruleCode']"
              @change="applyPanelFields"
            >
              <el-option
                v-for="f in fieldList"
                :key="f.fieldCode"
                :label="f.fieldName"
                :value="f.fieldCode"
              />
            </el-select>
            <el-switch
              v-else-if="field.type === 'switch'"
              v-model="panelFieldValues[field.field]"
              @change="applyPanelFields"
            />
            <el-slider
              v-else-if="field.type === 'slider'"
              v-model="panelFieldValues[field.field]"
              :min="field.min || 0"
              :max="field.max || 1"
              :step="field.step || 0.1"
              @change="applyPanelFields"
            />
          </div>
          <div v-else class="inspector-field">
            <div class="inspector-label">{{ field.label }}</div>
            <PromptRefEditor
              v-model="panelFieldValues[field.field]"
              :rows="field.rows || 6"
              :linkable-nodes="linkableNodes"
              @update:model-value="applyPanelField(field)"
            />
          </div>
        </template>
      </div>

      <!-- ===== 5. 输出预览 Section ===== -->
      <div v-if="visibleOutputPorts.length > 0" class="inspector-section">
        <div class="inspector-section-header">
          <span class="inspector-section-title">输出端口 (OUTPUTS)</span>
        </div>
        <div v-for="out in visibleOutputPorts" :key="out.name" class="output-item">
          <span class="output-type">{{ out.type }}</span>
          <span class="tree-label">{{ out.label || out.name }}</span>
        </div>
      </div>

      <!-- ===== 6. 测试与操作 Section ===== -->
      <div class="inspector-section">
        <div class="inspector-section-header">
          <span class="inspector-section-title">操作</span>
        </div>

        <!-- 测试结果显示 -->
        <div v-if="testResult" class="test-result-wrapper">
          <div :class="['test-result', testResult.success ? 'test-success' : 'test-fail']">
            <div class="test-header">
              <el-icon><IconCircleCheck v-if="testResult.success" /><IconWarning v-else /></el-icon>
              <span>{{ testResult.success ? '执行成功' : '执行失败' }}</span>
            </div>
            <pre v-if="testResult.data" class="test-output-json">{{
              JSON.stringify(testResult.data, null, 2)
            }}</pre>
          </div>
        </div>

        <div class="action-row">
          <el-button
            v-if="form.nodeType === 'docField' || form.nodeType === 'docTable'"
            type="success"
            size="small"
            :loading="testLoading"
            @click="testDocExtract"
            >测试提取</el-button
          >
          <el-button
            v-else-if="testable && form.nodeType !== 'start' && form.nodeType !== 'end'"
            type="primary"
            size="small"
            :loading="testLoading"
            @click="testNode"
            >运行测试</el-button
          >
          <el-button type="danger" size="small" plain @click="deleteNode">删除节点</el-button>
        </div>
      </div>
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
import { getSpecialNode } from '@/views/cert/Standard/WorkflowDesigner/specialNodes.js'
import { IconCircleCheck, IconInfo, IconWarning } from '@/yzh/icons'
import { ElMessage } from 'element-plus'
import { computed, ref, watch } from 'vue'
import PortControl from './panels/PortControl.vue'
import PromptRefEditor from './panels/PromptRefEditor.vue'

const props = defineProps({
  selectedNode: { type: Object, default: null },
  skills: { type: Array, default: () => [] },
  docRules: { type: Array, default: () => [] },
  docFields: { type: Array, default: () => [] },
  docTables: { type: Array, default: () => [] },
  canvasNodes: { type: Array, default: () => [] }
})

const emit = defineEmits([
  'update-node',
  'delete-node',
  'load-doc-fields',
  'link-node',
  'test-node',
  'test-workflow',
  'test-doc-extract'
])

const form = ref({
  nodeId: '',
  nodeType: 'skill',
  title: '',
  skillCode: '',
  inputs: {},
  outputs: {},
  config: {},
  inputPorts: [],
  outputPorts: [],
  inputTypes: {}
})
const inputValues = ref({})
const inputTypes = ref({})
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
  const sk = props.skills.find((s) => s.skillCode === form.value.skillCode)
  return sk?.skillName || form.value.nodeType
})

const nodeTypeTag = computed(
  () =>
    ({
      start: 'success',
      end: 'danger',
      branch: 'warning',
      skill: 'primary',
      ai_node: 'info',
      loop: 'info',
      docField: 'success',
      docTable: 'warning'
    })[form.value.nodeType] || 'info'
)

const nodeDescription = computed(() => {
  if (specialMeta.value) return specialMeta.value.description
  const sk = props.skills.find((s) => s.skillCode === form.value.skillCode)
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
  return ports.filter((p) => p.display !== 'hidden')
})

const visibleOutputPorts = computed(() => {
  const ports = form.value.outputPorts || []
  return ports.filter((p) => p.display !== 'hidden' && p.role !== 'anchor')
})

const visiblePanelSchema = computed(() => {
  if (specialMeta.value) return specialMeta.value.panelSchema || []
  return []
})

const skillDesc = computed(() => {
  const sk = props.skills.find((s) => s.skillCode === form.value.skillCode)
  return sk?.description || ''
})

const linkableNodes = computed(() => {
  // 排除控制节点（start/end/branch）和自身
  const excludeTypes = new Set(['start', 'end', 'branch'])
  const getOutputPorts = (nodeType) => {
    const meta = getSpecialNode(nodeType)
    return (
      meta?.outputPorts?.filter((p) => p.display !== 'hidden') || [
        { name: 'result', label: '结果', type: 'string' }
      ]
    )
  }

  return props.canvasNodes
    .filter((n) => n && n.id !== form.value.nodeId && !excludeTypes.has(n.nodeType))
    .map((n) => ({
      id: n.id,
      title: n.title || n.id,
      nodeType: n.nodeType,
      color: getSpecialNode(n.nodeType)?.color || '',
      outputPorts: getOutputPorts(n.nodeType)
    }))
})

/** branch 节点可选的目标节点（排除自身） */
const branchTargetNodes = computed(() => {
  return props.canvasNodes
    .filter((n) => n.id !== form.value.nodeId)
    .map((n) => ({
      id: n.id,
      label: `${n.title || n.id}`
    }))
})

/** 获取 branch 指定输出分支的目标节点 */
function getBranchOutput(handle) {
  // 优先从 selectedNode.branchEdges 中读取
  if (form.value._branchEdges?.length) {
    const edge = form.value._branchEdges.find((e) => e.handle === handle)
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

function getInputType(portName) {
  // 优先从 inputTypes 读取
  if (inputTypes.value[portName]) return inputTypes.value[portName]
  // 兼容旧数据：如果当前值是节点 ID → link，否则根据值是否存在判断
  const val = inputValues.value[portName]
  if (val && linkableNodes.value.some((n) => n.id === val)) return 'link'
  if (val) return 'constant'
  return 'link'
}

function setInputType(portName, type) {
  const prevValue = inputValues.value[portName]
  const prevType = inputTypes.value[portName]

  inputTypes.value[portName] = type
  form.value.inputTypes = { ...inputTypes.value }

  // 如果从 link 切到 constant，且之前有连线值 → 通知父组件断开画布连线
  if (prevType === 'link' && type === 'constant' && prevValue) {
    // emit link-node 让父组件删除画布上的边（sourceNodeId 是之前的源节点）
    // 这里传 null 表示断开，父组件用 targetNodeId + portName 找到对应边删除
    emit('link-node', { portName, sourceNodeId: null, targetNodeId: form.value.nodeId })
  }

  // 切换类型时清空当前值
  inputValues.value[portName] = ''
  const inputs = { ...form.value.inputs }
  delete inputs[portName]
  form.value.inputs = inputs
  applyChanges()
}

function setInputValue(portName, value) {
  inputValues.value[portName] = value
  const inputs = { ...form.value.inputs, [portName]: value }
  form.value.inputs = inputs
  applyChanges()
}

function onLinkNode(portName, sourceNodeId) {
  // 连线操作自动设置 inputType 为 link
  inputTypes.value[portName] = 'link'
  form.value.inputTypes = { ...inputTypes.value }
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
  const node = props.canvasNodes.find((n) => n.id === nodeId)
  return node ? node.title || node.id : nodeId
}

// ===== 绑定模式标签 =====

function getBindModeLabel(mode) {
  return { Link: '仅连线', LinkOrConstant: '可连线', Enum: '字典' }[mode] || '可连线'
}

function getBindModeTagType(mode) {
  return { Link: 'danger', LinkOrConstant: 'info', Enum: 'success' }[mode] || 'info'
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
  testLoading.value = true
  testResult.value = null

  emit('test-node', {
    nodeId: form.value.nodeId,
    nodeType: form.value.nodeType,
    title: form.value.title,
    skillCode: form.value.skillCode,
    config: form.value.config,
    inputs: form.value.inputs,
    inputTypes: form.value.inputTypes,
    inputPorts: form.value.inputPorts,
    outputPorts: form.value.outputPorts,
    onSuccess: (data) => {
      testResult.value = { success: true, data }
      testLoading.value = false
    },
    onError: (msg, errorData) => {
      testResult.value = { success: false, message: msg, data: errorData }
      testLoading.value = false
    }
  })
}

function testWorkflow() {
  testLoading.value = true
  testResult.value = null

  emit('test-workflow', {
    nodeId: form.value.nodeId,
    title: form.value.title,
    onSuccess: (data) => {
      testResult.value = { success: true, data }
      testLoading.value = false
    },
    onError: (msg, errorData) => {
      testResult.value = { success: false, message: msg, data: errorData }
      testLoading.value = false
    }
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

  const body =
    nodeType === 'docField'
      ? {
          ruleCode: config.ruleCode,
          fieldCode: config.fieldCode,
          docType: config.docType || 'standard'
        }
      : {
          ruleCode: config.ruleCode,
          tableCode: config.tableCode,
          docType: config.docType || 'standard'
        }

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

watch(
  () => props.docRules,
  (rules) => {
    docList.value = rules || []
  },
  { immediate: true }
)
watch(
  () => props.docFields,
  (fields) => {
    fieldList.value = fields || []
  },
  { immediate: true }
)
watch(
  () => props.docTables,
  (tables) => {
    tableList.value = tables || []
  },
  { immediate: true }
)

// 记录上次处理的 nodeId，用于判断是否是同一个节点的更新
let _lastNodeId = null
let _updateTick = 0

watch(
  () => props.selectedNode,
  (node) => {
    if (node) {
      const currentNodeId = node.nodeId || node.id
      if (_lastNodeId === currentNodeId && form.value.title === node.title) {
        return
      }
      _lastNodeId = currentNodeId
      _updateTick++

      form.value = {
        nodeId: currentNodeId,
        nodeType: node.nodeType || 'skill',
        title: node.title || '',
        skillCode: node.skillCode || '',
        inputs: { ...(node.inputs || {}) },
        outputs: { ...(node.outputs || {}) },
        config: { ...(node.config || {}) },
        inputPorts: node.inputPorts || [],
        outputPorts: node.outputPorts || [],
        inputTypes: { ...(node.inputTypes || {}) },
        _branchEdges: node.branchEdges || [],
        _updateTick
      }
      const vals = {}
      const types = {}
      for (const port of form.value.inputPorts) {
        vals[port.name] = form.value.inputs[port.name] ?? ''
        // 推断 inputType：优先从已有 inputTypes 读取
        if (form.value.inputTypes[port.name]) {
          types[port.name] = form.value.inputTypes[port.name]
        } else {
          // 兼容旧数据：推断类型
          const val = vals[port.name]
          if (val && props.canvasNodes.some((n) => n.id === val)) {
            types[port.name] = 'link'
          } else if (val) {
            types[port.name] = 'constant'
          } else {
            types[port.name] = 'link'
          }
        }
      }
      inputValues.value = vals
      inputTypes.value = types

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

      if (
        (node.nodeType === 'docField' || node.nodeType === 'docTable') &&
        (node.config?.ruleCode || node.config?.docCode)
      ) {
        emit('load-doc-fields', node.config.ruleCode || node.config.docCode)
      }
    } else {
      _lastNodeId = null
      form.value = {
        nodeId: '',
        nodeType: 'skill',
        title: '',
        skillCode: '',
        inputs: {},
        outputs: {},
        config: {},
        inputPorts: [],
        outputPorts: [],
        inputTypes: {}
      }
      inputValues.value = {}
      inputTypes.value = {}
      panelFieldValues.value = {}
    }
  },
  { immediate: true }
)

function applyChanges() {
  if (!form.value.nodeId) return
  const inputs = { ...inputValues.value }
  const types = { ...inputTypes.value }
  // 始终从 form.value.title 读取（watch 已确保与 store 同步）
  emit('update-node', {
    nodeId: form.value.nodeId,
    nodeType: form.value.nodeType,
    classCode: form.value.nodeType,
    title: form.value.title,
    skillCode: form.value.skillCode,
    inputs,
    inputTypes: types,
    outputs: form.value.outputs,
    config: form.value.config,
    inputPorts: form.value.inputPorts,
    outputPorts: form.value.outputPorts
  })
}
</script>

<style scoped lang="less">
.node-property-form {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
  background: #fff;
}
.node-form {
  padding: 16px;
  overflow-y: auto;
  flex: 1;
}

.form-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24px;
  padding-bottom: 12px;
  border-bottom: 1px solid #f1f5f9;
}
.title-main {
  display: flex;
  align-items: center;
  gap: 8px;
}
.node-title-display {
  font-size: 15px;
  font-weight: 700;
  color: #1e293b;
  flex: 1;
}
.help-icon {
  color: #94a3b8;
  cursor: pointer;
  font-size: 16px;
  &:hover {
    color: var(--yzh-color-primary);
  }
}

.port-row {
  margin-bottom: 16px;
  padding: 12px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 2px;
  transition: all 0.2s;
  &:hover {
    border-color: var(--yzh-color-primary);
  }
}
.port-label {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 6px;
}
.port-name {
  font-family: 'JetBrains Mono', monospace;
  font-size: 12px;
  font-weight: 700;
  color: #475569;
}
.port-tags {
  display: flex;
  gap: 4px;
}
.port-desc {
  font-size: 11px;
  color: #94a3b8;
  margin-bottom: 8px;
  line-height: 1.4;
}

/* branch 条件提示 */
.branch-hint {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  padding: 12px;
  background: #fffbeb;
  border: 1px solid #fef3c7;
  border-radius: 2px;
  font-size: 12px;
  color: #92400e;
  line-height: 1.5;
  margin-bottom: 16px;
  .el-icon {
    margin-top: 2px;
    flex-shrink: 0;
    font-size: 14px;
  }
}
.branch-source {
  margin: 12px 0;
}
.branch-warning {
  display: flex;
  gap: 6px;
  align-items: center;
  margin: 12px 0;
  font-size: 12px;
  color: #ef4444;
  font-weight: 600;
}
.branch-output-row {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.branch-output-item {
  display: flex;
  align-items: center;
  gap: 8px;
  background: #f8fafc;
  padding: 8px;
  border: 1px solid #e2e8f0;
  border-radius: 2px;
}

.field-hint {
  font-size: 11px;
  color: #94a3b8;
  margin-top: 4px;
  font-style: italic;
}
.field-hint-inline {
  font-size: 11px;
  color: #94a3b8;
  margin-left: 8px;
}

.output-item {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
  background: #f8fafc;
  padding: 8px 12px;
  border: 1px dashed #e2e8f0;
  border-radius: 2px;
}
.output-type {
  color: var(--yzh-color-primary);
  font-family: monospace;
  font-weight: 700;
  font-size: 11px;
}
.output-desc {
  color: #94a3b8;
  font-size: 11px;
}

.ai-output-hint {
  font-size: 11px;
  color: #7c3aed;
  background: #fdf4ff;
  padding: 10px 12px;
  border: 1px solid #fae8ff;
  border-radius: 2px;
  margin: 16px 0;
  line-height: 1.5;
}
.start-hint,
.end-hint {
  display: flex;
  gap: 8px;
  align-items: flex-start;
  padding: 12px;
  border-radius: 2px;
  font-size: 12px;
  line-height: 1.5;
  margin: 16px 0;
  .el-icon {
    margin-top: 2px;
    flex-shrink: 0;
    font-size: 14px;
  }
}
.start-hint {
  background: #f0fdf4;
  color: #166534;
  border: 1px solid #dcfce7;
}
.end-hint {
  background: #fef2f2;
  color: #991b1b;
  border: 1px solid #fee2e2;
}

.test-result-wrapper {
  margin-bottom: 12px;
}
.test-result {
  padding: 12px;
  border-radius: 4px;
  font-size: 12px;
  border: 1px solid #e2e8f0;
}
.test-result.test-success {
  background: #f0fdf4;
  border-color: #dcfce7;
  color: #166534;
}
.test-result.test-fail {
  background: #fef2f2;
  border-color: #fee2e2;
  color: #991b1b;
}
.test-header {
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 700;
  margin-bottom: 8px;
}

.action-row {
  display: flex;
  gap: 8px;
  margin-top: 12px;
}
.action-row .el-button {
  flex: 1;
  height: 32px;
  border-radius: 2px;
  font-weight: 700;
}

/* 空状态 */
.form-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  padding: 32px;
  text-align: center;
}
.empty-icon {
  color: #e2e8f0;
  margin-bottom: 16px;
}
.empty-title {
  font-size: 15px;
  color: #64748b;
  font-weight: 700;
  margin: 0 0 8px;
}
.empty-hint {
  font-size: 12px;
  color: #94a3b8;
  margin: 0;
  line-height: 1.5;
}
</style>
