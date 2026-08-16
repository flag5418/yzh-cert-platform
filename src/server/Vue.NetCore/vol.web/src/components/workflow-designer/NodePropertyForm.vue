<template>
  <div class="node-property-form" v-loading="loading">
    <div class="form-title" v-if="selectedNode">
      <el-tag :type="nodeTypeTag(selectedNode.skillCode)" size="small">{{ nodeTypeName(selectedNode.skillCode) }}</el-tag>
      <span class="node-id">节点: {{ selectedNode.nodeId }}</span>
    </div>
    <div class="form-title" v-else>
      <span style="color:var(--el-text-color-muted)">点击画布节点配置属性</span>
    </div>

    <el-form v-if="selectedNode" :model="form" label-width="90px" size="small">
      <!-- 基础信息 -->
      <el-divider content-position="left">基础信息</el-divider>
      <el-form-item label="Skill 编码">
        <el-input v-model="form.skillCode" disabled />
      </el-form-item>
      <el-form-item label="节点 ID">
        <el-input v-model="form.nodeId" disabled />
      </el-form-item>

      <!-- 输入参数 -->
      <el-divider content-position="left">输入参数 (inputs)</el-divider>
      <div v-for="(val, key) in form.inputs" :key="key" class="input-item">
        <el-input v-model="form.inputs[key]" placeholder="值或 {{nX.port}} 引用" size="small" />
      </div>
      <el-button link type="primary" size="small" @click="addInput">+ 添加参数</el-button>

      <!-- 输出声明 -->
      <el-divider content-position="left">输出端口 (outputs)</el-divider>
      <div v-for="(type, key) in form.outputs" :key="key" class="output-item">
        <el-input v-model="form.outputs[key]" placeholder="数据类型" size="small" style="width:100px" />
      </div>
      <el-button link type="primary" size="small" @click="addOutput">+ 添加端口</el-button>

      <!-- 配置 -->
      <el-divider content-position="left">静态配置 (config)</el-divider>
      <el-input
        v-model="formConfigJson"
        type="textarea"
        :rows="4"
        placeholder='{"key": "value"}'
        @change="onConfigChange"
      />

      <!-- 条件分支配置 -->
      <el-divider content-position="left" v-if="isBranchNode">条件分支配置</el-divider>
      <template v-if="isBranchNode">
        <el-form-item label="判定字段">
          <el-input v-model="branchCondition.field" placeholder="如 is_violation" size="small" />
        </el-form-item>
        <el-form-item label="操作符">
          <el-select v-model="branchCondition.op" size="small" style="width:100%">
            <el-option label="等于 (equals)" value="equals" />
            <el-option label="不等于 (not_equals)" value="not_equals" />
            <el-option label="大于 (gt)" value="gt" />
            <el-option label="大于等于 (gte)" value="gte" />
            <el-option label="小于 (lt)" value="lt" />
            <el-option label="小于等于 (lte)" value="lte" />
            <el-option label="Truthy (truthy)" value="truthy" />
          </el-select>
        </el-form-item>
        <el-form-item label="比较值">
          <el-input v-model="branchCondition.value" size="small" />
        </el-form-item>
      </template>

      <!-- 操作 -->
      <el-divider content-position="left">操作</el-divider>
      <el-button type="danger" size="small" @click="deleteNode">删除节点</el-button>
    </el-form>
  </div>
</template>

<script setup>
import { ref, computed, watch, getCurrentInstance } from 'vue'
import { ElMessage } from 'element-plus'

const props = defineProps({
  selectedNode: { type: Object, default: null },
  skills: { type: Array, default: () => [] }
})

const emit = defineEmits(['update-node', 'delete-node'])
const { proxy } = getCurrentInstance()

const loading = ref(false)
const form = ref({ nodeId: '', skillCode: '', inputs: {}, outputs: {}, config: {} })
const branchCondition = ref({ field: '', op: 'equals', value: '' })
const formConfigJson = ref('{}')

// 是否为分支源节点（有 outgoing 条件边）
const isBranchNode = computed(() => {
  if (!props.selectedNode) return false
  return false // 简化：分支条件通过边配置
})

watch(() => props.selectedNode, (node) => {
  if (node) {
    form.value = {
      nodeId: node.nodeId || node.id,
      skillCode: node.skillCode || node.data?.skillCode || '',
      inputs: { ...node.data?.inputs },
      outputs: { ...node.data?.outputs },
      config: { ...node.data?.config }
    }
    formConfigJson.value = JSON.stringify(node.data?.config || {}, null, 2)
  } else {
    form.value = { nodeId: '', skillCode: '', inputs: {}, outputs: {}, config: {} }
    formConfigJson.value = '{}'
  }
}, { immediate: true })

function nodeTypeName(skillCode) {
  const names = {
    get_field: '数据获取-字段', get_table: '数据获取-表格',
    compare: '数值比较', date_diff: '日期差', text_merge: '文本合并',
    llm_judge: 'AI判断', llm_generate: 'AI生成',
    create_nc: '创建NC', save_result: '保存结果', assemble_text: '组装文本'
  }
  return names[skillCode] || skillCode
}

function nodeTypeTag(skillCode) {
  const map = {
    data_access: 'success', data_process: '', ai_judge: 'warning',
    ai_generate: 'danger', output: 'info'
  }
  const cat = skillCategory(skillCode)
  return map[cat] || 'info'
}

function skillCategory(skillCode) {
  if (['get_field', 'get_table'].includes(skillCode)) return 'data_access'
  if (['compare', 'date_diff', 'text_merge'].includes(skillCode)) return 'data_process'
  if (skillCode === 'llm_judge') return 'ai_judge'
  if (skillCode === 'llm_generate') return 'ai_generate'
  if (['create_nc', 'save_result', 'assemble_text'].includes(skillCode)) return 'output'
  return 'data_process'
}

function addInput() {
  const key = `param_${Object.keys(form.value.inputs).length + 1}`
  form.value.inputs[key] = ''
  applyChanges()
}

function addOutput() {
  const key = `out_${Object.keys(form.value.outputs).length + 1}`
  form.value.outputs[key] = 'string'
  applyChanges()
}

function onConfigChange() {
  try {
    form.value.config = JSON.parse(formConfigJson.value)
    applyChanges()
  } catch (e) {
    ElMessage.warning('JSON 格式错误')
  }
}

function applyChanges() {
  emit('update-node', {
    nodeId: form.value.nodeId,
    skillCode: form.value.skillCode,
    inputs: form.value.inputs,
    outputs: form.value.outputs,
    config: form.value.config
  })
}

function deleteNode() {
  if (form.value.nodeId) {
    emit('delete-node', form.value.nodeId)
  }
}
</script>


