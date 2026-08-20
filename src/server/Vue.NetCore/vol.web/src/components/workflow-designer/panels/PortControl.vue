<template>
  <div class="port-control">
    <!-- ===== Enum 模式：字典下拉 ===== -->
    <el-select
      v-if="bindMode === 'Enum'"
      :model-value="modelValue"
      :placeholder="placeholder || '请选择'"
      style="width: 100%"
      @change="v => $emit('update:modelValue', v)"
    >
      <el-option
        v-for="opt in enumOptions"
        :key="opt.value"
        :label="opt.label"
        :value="opt.value"
      />
    </el-select>

    <!-- ===== Link 模式：仅连线，下拉选择画布节点 ===== -->
    <div v-else-if="bindMode === 'Link'" class="link-only">
      <el-select
        :model-value="modelValue"
        :placeholder="modelValue ? '' : '请在画布上连线或从下方选择'"
        style="width: 100%"
        @change="v => onLinkChange(v)"
      >
        <el-option
          v-for="n in linkableNodes"
          :key="n.id"
          :label="n.label"
          :value="n.id"
        />
        <template #empty>
          <span class="empty-hint">无可连接的节点，请先在画布上连线</span>
        </template>
      </el-select>
      <div v-if="modelValue && isNodeRef" class="link-badge">
        <el-icon><IconEdit /></el-icon>
        <span>已连线</span>
      </div>
    </div>

    <!-- ===== LinkOrConstant 模式：下拉/编辑切换 ===== -->
    <div v-else class="link-or-constant">
      <template v-if="!isEditing">
        <el-select
          :model-value="modelValue"
          placeholder="选择节点或点击编辑输入"
          style="flex: 1"
          @change="v => onLinkChange(v)"
        >
          <el-option
            v-for="n in linkableNodes"
            :key="n.id"
            :label="n.label"
            :value="n.id"
          />
        </el-select>
        <el-tooltip content="切换为手动输入" placement="top">
          <el-button size="small" @click="enterEditMode">
            <el-icon><IconEdit /></el-icon>
          </el-button>
        </el-tooltip>
      </template>
      <template v-else>
        <el-input
          :model-value="modelValue"
          :placeholder="inputPlaceholder"
          style="flex: 1"
          @change="v => onInputChange(v)"
        />
        <el-tooltip content="切换为节点选择" placement="top">
          <el-button size="small" @click="exitEditMode">
            <el-icon><IconEdit /></el-icon>
          </el-button>
        </el-tooltip>
      </template>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { IconEdit } from '@/yzh/icons'

const props = defineProps({
  /** 当前值（端口名 → 值） */
  modelValue: { type: [String, Number, Boolean], default: '' },
  /** 绑定模式：Link / LinkOrConstant / Enum */
  bindMode: { type: String, default: 'LinkOrConstant' },
  /** Enum 模式的字典编码 */
  enumSource: { type: String, default: '' },
  /** Enum 选项（外部传入，或从 enumSource 加载） */
  options: { type: Array, default: () => [] },
  /** 可连接的节点列表 */
  linkableNodes: { type: Array, default: () => [] },
  /** 手动输入时的 placeholder */
  inputPlaceholder: { type: String, default: '输入常量值或 {{n1.portName}}' },
  /** 选择器 placeholder */
  placeholder: { type: String, default: '' }
})

const emit = defineEmits(['update:modelValue', 'link-node'])

const isEditing = ref(false)

// 判断当前值是否为节点引用
const isNodeRef = computed(() => {
  const v = props.modelValue
  return typeof v === 'string' && v.includes('_n') && props.linkableNodes.some(n => n.id === v)
})

// Enum 选项：优先用外部传入的，否则用内置的
const enumOptions = computed(() => {
  if (props.options?.length) return props.options
  // 内置 compare_operator 选项
  if (props.enumSource === 'compare_operator') {
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
})

// 初始化编辑模式状态
watch(() => props.modelValue, (val) => {
  // 如果当前值是节点引用，不是编辑模式
  if (val && isNodeRef.value) {
    isEditing.value = false
  }
}, { immediate: true })

function onLinkChange(sourceNodeId) {
  if (!sourceNodeId) {
    emit('update:modelValue', '')
    emit('link-node', { sourceNodeId: null })
  } else {
    emit('update:modelValue', sourceNodeId)
    emit('link-node', { sourceNodeId })
  }
}

function onInputChange(val) {
  emit('update:modelValue', val)
  isEditing.value = false
}

function enterEditMode() {
  isEditing.value = true
}

function exitEditMode() {
  isEditing.value = false
}
</script>

<style scoped lang="less">
.port-control { width: 100%; }

.link-only { position: relative; }
.link-badge {
  display: inline-flex; align-items: center; gap: 4px;
  font-size: 11px; color: #67C23A; margin-top: 4px;
}

.link-or-constant {
  display: flex; gap: 4px; align-items: center;
}

.empty-hint { font-size: 12px; color: #c0c4cc; padding: 4px 8px; }
</style>
