<template>
  <div class="port-control">
    <el-select
      v-if="bindMode === 'Enum'"
      :model-value="modelValue"
      :placeholder="placeholder || '请选择'"
      style="width: 100%"
      @change="(v: any) => $emit('update:modelValue', v)"
    >
      <el-option v-for="opt in enumOptions" :key="opt.value" :label="opt.label" :value="opt.value" />
    </el-select>

    <div v-else-if="bindMode === 'Link'" class="link-only">
      <el-select
      :model-value="modelValue"
      :placeholder="modelValue ? '' : '请在画布上连线或从下方选择'"
      style="width: 100%"
      @change="(v: any) => onLinkChange(v)"
    >
        <el-option v-for="n in linkableNodes" :key="n.id" :label="n.label" :value="n.id" />
        <template #empty><span class="empty-hint">无可连接的节点，请先在画布上连线</span></template>
      </el-select>
      <div v-if="modelValue && isNodeRef" class="link-badge">
        <el-icon><Edit /></el-icon><span>已连线</span>
      </div>
    </div>

    <div v-else class="link-or-constant">
      <div class="input-type-switch">
        <el-radio-group :model-value="inputType" size="small" @change="(v: any) => onInputTypeChange(v)">
          <el-radio-button value="link">连线</el-radio-button>
          <el-radio-button value="constant">常量</el-radio-button>
        </el-radio-group>
      </div>
      <div v-if="inputType === 'link'" class="link-input-area">
        <el-select :model-value="modelValue" placeholder="选择上游节点" style="width: 100%" @change="(v: any) => onLinkChange(v)">
          <el-option v-for="n in linkableNodes" :key="n.id" :label="n.label" :value="n.id" />
          <template #empty><span class="empty-hint">无可连接的节点</span></template>
        </el-select>
        <div v-if="modelValue && isNodeRef" class="link-badge">
          <el-icon><Edit /></el-icon><span>已连线</span>
        </div>
      </div>
      <div v-else class="constant-input-area">
        <el-input :model-value="modelValue" :placeholder="inputPlaceholder" style="width: 100%" @input="(v: any) => onConstantInput(v)" />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { Edit } from '@element-plus/icons-vue'

const props = withDefaults(defineProps<{
  modelValue?: string | number | boolean
  bindMode?: string
  enumSource?: string
  options?: Array<{ label: string; value: any }>
  linkableNodes?: Array<{ id: string; label: string }>
  inputPlaceholder?: string
  placeholder?: string
  inputType?: string
}>(), { modelValue: '', bindMode: 'LinkOrConstant', enumSource: '', options: () => [], linkableNodes: () => [], inputPlaceholder: '输入常量值或 {{n1.portName}}', placeholder: '', inputType: 'link' })

const emit = defineEmits<{
  'update:modelValue': [value: any]
  'link-node': [payload: { sourceNodeId: string | null }]
  'update:inputType': [type: string]
}>()

const isNodeRef = computed(() => {
  const v = props.modelValue
  return typeof v === 'string' && v.length > 0 && (props.linkableNodes || []).some(n => n.id === v)
})

const enumOptions = computed(() => {
  if (props.options?.length) return props.options
  if (props.enumSource === 'compare_operator') {
    return [
      { label: '大于 (>)', value: '>' }, { label: '大于等于 (>=)', value: '>=' },
      { label: '小于 (<)', value: '<' }, { label: '小于等于 (<=)', value: '<=' },
      { label: '等于 (==)', value: '==' }, { label: '不等于 (!=)', value: '!=' }
    ]
  }
  return []
})

function onLinkChange(sourceNodeId: string) {
  emit('update:modelValue', sourceNodeId || '')
  emit('link-node', { sourceNodeId: sourceNodeId || null })
}
function onConstantInput(val: string) { emit('update:modelValue', val) }
function onInputTypeChange(newType: string) {
  emit('update:inputType', newType)
  emit('update:modelValue', '')
}
</script>

<style scoped lang="less">
.port-control { width: 100%; }
.link-badge {
  display: inline-flex; align-items: center; gap: 4px;
  font-size: 10px; color: #10b981; margin-top: 4px; font-weight: 700; text-transform: uppercase;
}
.input-type-switch { margin-bottom: 8px; display: flex; justify-content: flex-end; }
:deep(.el-radio-button--small .el-radio-button__inner) { padding: 4px 8px !important; font-size: 10px !important; border-radius: 2px !important; }
.link-input-area, .constant-input-area { width: 100%; }
.empty-hint { font-size: 11px; color: #94a3b8; padding: 8px; text-align: center; }
</style>
