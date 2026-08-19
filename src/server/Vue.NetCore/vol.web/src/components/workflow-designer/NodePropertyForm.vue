<template>
  <div class="node-property-form">
    <div v-if="selectedNode" class="node-form">
      <div class="form-title">
        <el-tag :type="nodeTypeTag" size="small">{{ nodeTypeName }}</el-tag>
        <span class="node-id">{{ selectedNode.nodeId }}</span>
      </div>

      <el-form label-width="80px" size="small">
        <!-- 通用：节点标题 -->
        <el-form-item label="节点标题">
          <el-input v-model="form.title" placeholder="节点作用说明" @change="applyChanges" />
        </el-form-item>

        <!-- 开始节点：说明运行时注入的参数 -->
        <template v-if="form.nodeType === 'start'">
          <el-alert type="info" :closable="false" show-icon style="margin-bottom: 12px">
            <template #title>运行时注入</template>
            <template #default>以下参数由引擎在执行时自动注入，无需手动配置</template>
          </el-alert>
          <div v-for="port in (form.outputPorts || [])" :key="port.name" class="runtime-param">
            <el-tag size="small" type="success">{{ port.name }}</el-tag>
            <span class="runtime-type">{{ port.type }}</span>
            <span class="runtime-desc">{{ port.description }}</span>
          </div>
        </template>

        <!-- 功能节点：Skill 编码 + 描述 -->
        <template v-if="isSkillNode">
          <el-form-item label="Skill">
            <el-input :model-value="form.skillCode" disabled />
          </el-form-item>
          <el-form-item label="说明">
            <el-input :model-value="skillDesc" type="textarea" :rows="2" disabled />
          </el-form-item>
        </template>

        <!-- ===== 输入端口（从 inputPorts 声明渲染） ===== -->
        <template v-if="inputPorts.length > 0">
          <el-divider content-position="left">输入端口</el-divider>
          <div v-for="port in inputPorts" :key="port.name" class="port-row">
            <div class="port-label">
              <span class="port-name">{{ port.name }}</span>
              <el-tag v-if="port.required" size="small" type="danger">必填</el-tag>
              <el-tag size="small" :type="getBindModeTagType(port.bindMode)">{{ getBindModeLabel(port.bindMode) }}</el-tag>
            </div>
            <div class="port-desc">{{ port.description }}</div>
            <div class="port-value">
              <!-- Enum 模式：下拉选择 -->
              <el-select
                v-if="port.bindMode === 'Enum'"
                :model-value="getInputValue(port.name)"
                placeholder="请选择"
                style="width: 100%"
                @change="v => setInputValue(port.name, v)"
              >
                <el-option v-for="opt in getEnumOptions(port.enumSource)" :key="opt.value" :label="opt.label" :value="opt.value" />
              </el-select>
              <!-- Link 模式：仅连线提示 -->
              <div v-else-if="port.bindMode === 'Link'" class="link-only-hint">
                <el-icon><Link /></el-icon> 通过连线绑定
              </div>
              <!-- LinkOrConstant 模式：文本输入 -->
              <el-input
                v-else
                :model-value="getInputValue(port.name)"
                :placeholder="getInputPlaceholder(port)"
                @change="v => setInputValue(port.name, v)"
              />
            </div>
          </div>
        </template>

        <!-- ===== 特殊节点：logic 条件编辑 ===== -->
        <template v-if="form.nodeType === 'logic'">
          <el-divider content-position="left">判断条件</el-divider>
          <div v-for="(cond, idx) in conditions" :key="idx" class="condition-row">
            <el-input v-model="cond.valueA" placeholder="值 A（如 n1.fieldValue）" size="small" @change="applyConditions" />
            <el-select v-model="cond.operator" size="small" style="width: 100px" @change="applyConditions">
              <el-option v-for="op in logicOperators" :key="op.value" :label="op.label" :value="op.value" />
            </el-select>
            <el-input v-model="cond.valueB" placeholder="值 B（如 60）" size="small" @change="applyConditions" />
            <el-button type="danger" link size="small" @click="removeCondition(idx)">删</el-button>
          </div>
          <div style="display: flex; gap: 8px; align-items: center">
            <el-button link type="primary" size="small" @click="addCondition">+ 添加条件</el-button>
            <el-select v-model="form.config.conditionLogic" size="small" style="width: 80px" @change="applyConditions">
              <el-option label="且(and)" value="and" />
              <el-option label="或(or)" value="or" />
            </el-select>
          </div>
        </template>

        <!-- ===== 特殊节点：ai_node 提示词 ===== -->
        <template v-if="form.nodeType === 'ai_node'">
          <el-divider content-position="left">提示词 (prompt)</el-divider>
          <el-input
            v-model="aiPrompt"
            type="textarea"
            :rows="6"
            placeholder="输入提示词，用 {{n1.fieldValue}} 引用上游节点输出"
            @change="applyAiPrompt"
          />
          <div class="port-hint">引用语法：{{nX.portName}} 引用上游节点输出</div>
        </template>

        <!-- ===== 特殊节点：end 输出结论 ===== -->
        <template v-if="form.nodeType === 'end'">
          <el-divider content-position="left">输出结论</el-divider>
          <el-input
            v-model="endResultJson"
            type="textarea"
            :rows="4"
            placeholder='{"isViolated": true, "conclusion": "不合格"}'
            @change="applyEndResult"
          />
        </template>

        <!-- ===== 特殊节点：docField / docTable 选择 ===== -->
        <template v-if="form.nodeType === 'docField'">
          <el-divider content-position="left">字段选择</el-divider>
          <el-form-item label="文档">
            <el-select v-model="form.config.docCode" placeholder="选择文档" style="width: 100%" @change="onDocFieldChange">
              <el-option v-for="d in docList" :key="d.ruleCode" :label="d.fileName || d.standardFileCode" :value="d.ruleCode" />
            </el-select>
          </el-form-item>
          <el-form-item label="字段">
            <el-select v-model="form.config.fieldCode" placeholder="选择字段" style="width: 100%" @change="applyChanges" :disabled="!form.config.docCode">
              <el-option v-for="f in fieldList" :key="f.fieldCode" :label="`${f.fieldName} (${f.fieldCode})`" :value="f.fieldCode" />
            </el-select>
          </el-form-item>
        </template>

        <template v-if="form.nodeType === 'docTable'">
          <el-divider content-position="left">表格选择</el-divider>
          <el-form-item label="文档">
            <el-select v-model="form.config.docCode" placeholder="选择文档" style="width: 100%" @change="onDocTableChange">
              <el-option v-for="d in docList" :key="d.ruleCode" :label="d.fileName || d.standardFileCode" :value="d.ruleCode" />
            </el-select>
          </el-form-item>
          <el-form-item label="表格">
            <el-select v-model="form.config.tableCode" placeholder="选择表格" style="width: 100%" @change="applyChanges" :disabled="!form.config.docCode">
              <el-option v-for="t in tableList" :key="t.tableCode" :label="`${t.tableName} (${t.tableCode})`" :value="t.tableCode" />
            </el-select>
          </el-form-item>
        </template>

        <!-- ===== 输出端口（只读，start 节点不展示） ===== -->
        <template v-if="form.nodeType !== 'start' && outputList.length">
          <el-divider content-position="left">输出端口</el-divider>
          <div v-for="out in outputList" :key="out.outputName" class="output-item">
            <el-tag size="small" type="info">{{ out.outputName }}</el-tag>
            <span class="output-type">{{ out.outputType }}</span>
            <span class="output-desc">{{ out.description || '' }}</span>
          </div>
        </template>

        <el-divider content-position="left">操作</el-divider>
        <el-button type="danger" size="small" @click="deleteNode">删除节点</el-button>
      </el-form>
    </div>
    <div v-else class="form-empty">
      <p>点击画布节点配置属性</p>
      <p class="hint">拖动左侧节点到画布，或点击添加</p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'

const props = defineProps({
  selectedNode: { type: Object, default: null },
  skills: { type: Array, default: () => [] },
  /** 已加载的文档提取规则列表 */
  docRules: { type: Array, default: () => [] },
  /** 当前选中文档的字段列表 */
  docFields: { type: Array, default: () => [] },
  /** 当前选中文档的表格列表 */
  docTables: { type: Array, default: () => [] }
})

const emit = defineEmits(['update-node', 'delete-node', 'load-doc-fields'])

const form = ref({ nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {}, inputPorts: [], outputPorts: [] })
const inputValues = ref({})
const conditions = ref([])
const aiPrompt = ref('')
const endResultJson = ref('{}')
const docList = ref([])
const fieldList = ref([])
const tableList = ref([])

const logicOperators = [
  { label: '等于', value: 'eq' },
  { label: '不等于', value: 'neq' },
  { label: '大于', value: 'gt' },
  { label: '大于等于', value: 'gte' },
  { label: '小于', value: 'lt' },
  { label: '小于等于', value: 'lte' },
  { label: '包含', value: 'contains' },
  { label: '不为空', value: 'notEmpty' }
]

const isSkillNode = computed(() => form.value.nodeType === 'skill')
const inputPorts = computed(() => form.value.inputPorts || [])

const nodeTypeName = computed(() => ({
  start: '开始', end: '结束', logic: '逻辑判断', skill: 'Skill 节点',
  ai_node: 'AI 节点', loop: '循环节点', docField: '文档字段', docTable: '文档表格'
}[form.value.nodeType] || form.value.nodeType))

const nodeTypeTag = computed(() => ({
  start: 'success', end: 'danger', logic: 'warning', skill: 'primary',
  ai_node: '', loop: 'info', docField: 'success', docTable: 'warning'
}[form.value.nodeType] || 'info'))

const skillMeta = computed(() => props.skills.find(s => s.skillCode === form.value.skillCode) || null)
const skillDesc = computed(() => skillMeta.value?.description || '')

const outputList = computed(() => {
  if (form.value.nodeType === 'start') return []  // start 不展示输出
  const nodePorts = form.value.outputPorts || []
  if (nodePorts.length) {
    return nodePorts.map(p => ({
      outputName: p.name || p.outputName,
      outputType: p.type || p.outputType,
      description: p.description || ''
    }))
  }
  if (isSkillNode.value) {
    const declared = skillMeta.value?.outputs || []
    if (declared.length) return declared
  }
  return Object.keys(form.value.outputs || {}).map(k => ({ outputName: k, outputType: form.value.outputs[k] }))
})

// --- 输入端口值管理 ---

function getInputValue(portName) {
  return inputValues.value[portName] ?? ''
}

function setInputValue(portName, value) {
  inputValues.value[portName] = value
  const inputs = { ...form.value.inputs, [portName]: value }
  form.value.inputs = inputs
  applyChanges()
}

function getInputPlaceholder(port) {
  if (port.bindMode === 'Link') return '连线引用'
  return `常量 / {{n1.portName}} / ctx.xxx`
}

function getEnumOptions(enumSource) {
  if (enumSource === 'compare_operator') {
    return [
      { label: '大于 (>)', value: '>' },
      { label: '大于等于 (>=)', value: '>=' },
      { label: '小于 (<)', value: '<' },
      { label: '小于等于 (<=)', value: '<=' },
      { label: '等于 (==)', value: '==' },
      { label: '不等于 (!=)', value: '!=' }
    ]
  }
  return []
}

function getBindModeLabel(mode) {
  const map = { Link: '仅连线', LinkOrConstant: '可连线', Enum: '字典选择' }
  return map[mode] || '可连线'
}

function getBindModeTagType(mode) {
  const map = { Link: 'danger', LinkOrConstant: '', Enum: 'success' }
  return map[mode] || ''
}

// --- Logic 条件 ---

function addCondition() {
  conditions.value.push({ valueA: '', operator: 'gte', valueB: '' })
  applyConditions()
}

function removeCondition(idx) {
  conditions.value.splice(idx, 1)
  applyConditions()
}

function applyConditions() {
  form.value.config = { ...form.value.config, conditions: conditions.value, conditionLogic: form.value.config.conditionLogic || 'and' }
  applyChanges()
}

// --- AI 提示词 ---

function applyAiPrompt() {
  form.value.config = { ...form.value.config, prompt: aiPrompt.value }
  applyChanges()
}

// --- End 结论 ---

function applyEndResult() {
  try {
    form.value.config = { ...form.value.config, result: JSON.parse(endResultJson.value || '{}') }
    applyChanges()
  } catch (e) { /* JSON 不合法时暂不提交 */ }
}

// --- docField / docTable 文档选择 ---

function onDocFieldChange(ruleCode) {
  form.value.config = { ...form.value.config, docCode: ruleCode, fieldCode: '' }
  // 通知父组件加载字段/表格
  emit('load-doc-fields', ruleCode)
  applyChanges()
}

function onDocTableChange(ruleCode) {
  form.value.config = { ...form.value.config, docCode: ruleCode, tableCode: '' }
  // 通知父组件加载字段/表格
  emit('load-doc-fields', ruleCode)
  applyChanges()
}

// --- watch ---

// 监听 docRules 变化，同步到 docList
watch(() => props.docRules, (rules) => {
  docList.value = rules || []
}, { immediate: true })

// 监听 docFields/docTables 变化
watch(() => props.docFields, (fields) => {
  fieldList.value = fields || []
}, { immediate: true })

watch(() => props.docTables, (tables) => {
  tableList.value = tables || []
}, { immediate: true })

watch(() => props.selectedNode, (node) => {
  if (node) {
    form.value = {
      nodeId: node.nodeId || node.id,
      nodeType: node.nodeType || 'skill',
      title: node.title || '',
      skillCode: node.skillCode || '',
      inputs: { ...(node.inputs || {}) },
      outputs: { ...(node.outputs || {}) },
      config: { ...(node.config || {}) },
      inputPorts: node.inputPorts || [],
      outputPorts: node.outputPorts || []
    }
    const vals = {}
    for (const port of form.value.inputPorts) {
      vals[port.name] = form.value.inputs[port.name] ?? ''
    }
    inputValues.value = vals
    conditions.value = form.value.config.conditions || [{ valueA: '', operator: 'gte', valueB: '' }]
    aiPrompt.value = form.value.config.prompt || ''
    endResultJson.value = JSON.stringify(form.value.config.result || {}, null, 2)
    // docField/docTable：同步文档列表和字段/表格
    docList.value = props.docRules || []
    fieldList.value = props.docFields || []
    tableList.value = props.docTables || []
    // 如果 docField/docTable 节点已有 docCode，通知父组件加载字段/表格
    if ((node.nodeType === 'docField' || node.nodeType === 'docTable') && node.config?.docCode) {
      emit('load-doc-fields', node.config.docCode)
    }
  } else {
    form.value = { nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {}, inputPorts: [], outputPorts: [] }
    inputValues.value = {}
    conditions.value = []
    aiPrompt.value = ''
    endResultJson.value = '{}'
  }
}, { immediate: true })

function applyChanges() {
  if (!form.value.nodeId) return
  const inputs = { ...inputValues.value }
  emit('update-node', {
    nodeId: form.value.nodeId,
    nodeType: form.value.nodeType,
    title: form.value.title,
    skillCode: form.value.skillCode,
    inputs,
    outputs: form.value.outputs,
    config: form.value.config,
    inputPorts: form.value.inputPorts,
    outputPorts: form.value.outputPorts
  })
}

function deleteNode() {
  if (form.value.nodeId) emit('delete-node', form.value.nodeId)
}
</script>

<style scoped lang="less">
.node-property-form { display: flex; flex-direction: column; height: 100%; overflow: hidden; }
.node-form { padding: 12px; overflow-y: auto; flex: 1; }
.form-title { display: flex; align-items: center; gap: 8px; margin-bottom: 12px; }
.node-id { font-size: 12px; color: #909399; }

.runtime-param { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; padding: 6px 8px; background: #f0f9eb; border-radius: 4px; }
.runtime-type { font-family: monospace; font-size: 12px; color: #67C23A; }
.runtime-desc { font-size: 12px; color: #909399; }

.port-row {
  margin-bottom: 12px; padding: 8px; background: #f9fafc; border-radius: 6px; border: 1px solid #ebeef5;
}
.port-label { display: flex; align-items: center; gap: 6px; margin-bottom: 4px; }
.port-name { font-family: 'SF Mono', Monaco, monospace; font-size: 13px; font-weight: 600; color: #303133; }
.port-desc { font-size: 12px; color: #909399; margin-bottom: 6px; }
.port-value { }
.port-hint { font-size: 11px; color: #c0c4cc; margin-top: 4px; }
.link-only-hint { font-size: 12px; color: #909399; padding: 6px 0; display: flex; align-items: center; gap: 4px; }

.condition-row { display: flex; gap: 6px; margin-bottom: 8px; align-items: center; }

.output-item { display: flex; align-items: center; gap: 6px; margin-bottom: 6px; font-size: 12px; }
.output-type { color: #409EFF; font-family: monospace; }
.output-desc { color: #909399; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

.form-empty { padding: 40px 12px; text-align: center; color: #909399; font-size: 13px; }
.hint { font-size: 12px; color: #c0c4cc; margin-top: 4px; }
</style>
