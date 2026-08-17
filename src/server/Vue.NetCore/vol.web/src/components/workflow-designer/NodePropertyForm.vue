<template>
  <div class="node-property-form">
    <div v-if="selectedNode" class="node-form">
      <div class="form-title">
        <el-tag :type="nodeTypeTag" size="small">{{ nodeTypeName }}</el-tag>
        <span class="node-id">{{ selectedNode.nodeId }}</span>
      </div>

      <el-form label-width="80px" size="small">
        <el-form-item label="节点标题">
          <el-input v-model="form.title" placeholder="节点作用说明" @change="applyChanges" />
        </el-form-item>
        <el-form-item v-if="isSkillNode" label="Skill 编码">
          <el-input :model-value="form.skillCode" disabled />
        </el-form-item>
        <el-form-item v-if="isSkillNode" label="作用说明">
          <el-input :model-value="skillDesc" type="textarea" :rows="2" disabled />
        </el-form-item>

        <!-- 输入参数（命名引用，V1.2：字面量/{{nX.port}}/{{ctx.xxx}}） -->
        <template v-if="!isStartEndNode">
          <el-divider content-position="left">输入参数 (inputs)</el-divider>
          <div v-for="(item, idx) in inputList" :key="idx" class="input-item">
            <el-input v-model="item.key" placeholder="参数名" size="small" style="width: 90px" @change="applyChanges" />
            <el-input v-model="item.value" placeholder='值 / {{n1.fieldValue}} / {{ctx.enterpriseCode}}' size="small" @change="applyChanges" />
            <el-button type="danger" link size="small" @click="removeInput(idx)">删</el-button>
          </div>
          <el-button link type="primary" size="small" @click="addInput">+ 添加参数</el-button>
        </template>

        <!-- 输出端口（只读，来自 Skill 元数据输出声明） -->
        <template v-if="isSkillNode && outputList.length">
          <el-divider content-position="left">输出端口 (outputs)</el-divider>
          <div v-for="out in outputList" :key="out.outputName" class="output-item">
            <el-tag size="small" type="info">{{ out.outputName }}</el-tag>
            <span class="output-type">{{ out.outputType }}</span>
            <span class="output-desc">{{ out.outputPrompt || out.description || '' }}</span>
          </div>
        </template>

        <!-- 静态配置 (config) -->
        <el-divider content-position="left">静态配置 (config)</el-divider>
        <el-input
          v-model="formConfigJson"
          type="textarea"
          :rows="4"
          placeholder='{"key": "value"}'
          @change="onConfigChange"
        />

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
  /** 选中节点 { nodeId, nodeType, title, skillCode, config, inputs, outputs } */
  selectedNode: { type: Object, default: null },
  /** 启用 Skill 元数据（api/skill/list-active，含 inputs/outputs 声明） */
  skills: { type: Array, default: () => [] }
})

const emit = defineEmits(['update-node', 'delete-node'])

const form = ref({ nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {} })
const inputList = ref([])
const formConfigJson = ref('{}')

const isSkillNode = computed(() => form.value.nodeType === 'skill')
const isStartEndNode = computed(() => form.value.nodeType === 'start' || form.value.nodeType === 'end')

const nodeTypeName = computed(() => ({
  start: '开始', end: '结束', logic: '逻辑判断', skill: 'Skill 节点'
}[form.value.nodeType] || form.value.nodeType))

const nodeTypeTag = computed(() => ({
  start: 'success', end: 'danger', logic: 'warning', skill: 'primary'
}[form.value.nodeType] || 'info'))

// 当前 Skill 元数据（输入/输出声明提示）
const skillMeta = computed(() => props.skills.find(s => s.skillCode === form.value.skillCode) || null)

const skillDesc = computed(() => skillMeta.value?.description || '')

const outputList = computed(() => {
  if (!isSkillNode.value) return []
  // 优先节点已配置的输出，其次 Skill 元数据输出声明
  const declared = skillMeta.value?.outputs || []
  if (declared.length) return declared
  return Object.keys(form.value.outputs || {}).map(k => ({ outputName: k, outputType: form.value.outputs[k] }))
})

function objToInputList(inputs) {
  return Object.entries(inputs || {}).map(([key, value]) => ({ key, value: value == null ? '' : String(value) }))
}

watch(() => props.selectedNode, (node) => {
  if (node) {
    form.value = {
      nodeId: node.nodeId || node.id,
      nodeType: node.nodeType || node.data?.nodeType || 'skill',
      title: node.title || node.data?.title || '',
      skillCode: node.skillCode || node.data?.skillCode || '',
      inputs: { ...(node.inputs || node.data?.inputs || {}) },
      outputs: { ...(node.outputs || node.data?.outputs || {}) },
      config: { ...(node.config || node.data?.config || {}) }
    }
    inputList.value = objToInputList(form.value.inputs)
    formConfigJson.value = JSON.stringify(form.value.config, null, 2)
  } else {
    form.value = { nodeId: '', nodeType: 'skill', title: '', skillCode: '', inputs: {}, outputs: {}, config: {} }
    inputList.value = []
    formConfigJson.value = '{}'
  }
}, { immediate: true })

function addInput() {
  inputList.value.push({ key: '', value: '' })
}

function removeInput(idx) {
  inputList.value.splice(idx, 1)
  applyChanges()
}

function onConfigChange() {
  try {
    form.value.config = JSON.parse(formConfigJson.value || '{}')
    applyChanges()
  } catch (e) {
    // JSON 未合法时暂不提交，等待修正
  }
}

function applyChanges() {
  if (!form.value.nodeId) return
  // inputs 从键值数组转对象（跳过空 key）
  const inputs = {}
  for (const item of inputList.value) {
    if (item.key) inputs[item.key] = item.value
  }
  emit('update-node', {
    nodeId: form.value.nodeId,
    nodeType: form.value.nodeType,
    title: form.value.title,
    skillCode: form.value.skillCode,
    inputs,
    outputs: form.value.outputs,
    config: form.value.config
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
.input-item { display: flex; gap: 6px; margin-bottom: 6px; align-items: center; }
.output-item { display: flex; align-items: center; gap: 6px; margin-bottom: 6px; font-size: 12px; }
.output-type { color: #409EFF; }
.output-desc { color: #909399; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.form-empty { padding: 40px 12px; text-align: center; color: #909399; font-size: 13px; }
.hint { font-size: 12px; color: #c0c4cc; margin-top: 4px; }
</style>
