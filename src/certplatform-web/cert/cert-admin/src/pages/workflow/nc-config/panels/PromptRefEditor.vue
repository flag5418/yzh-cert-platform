<template>
  <div class="pre">
    <transition name="slide-fade">
      <div v-if="showRefPanel" class="ref-bar">
        <div class="ref-bar-head">
          <span class="ref-bar-title">可引用节点</span>
          <el-button size="small" text @click="showRefPanel = false"><el-icon><Close /></el-icon></el-button>
        </div>
        <div v-if="!refNodes || refNodes.length === 0" class="ref-empty">画布上暂无可用节点</div>
        <div v-else class="ref-rows">
          <div v-for="node in refNodes" :key="node.id" class="ref-row">
            <span class="ref-dot" :style="{ background: node.color || '#909399' }"></span>
            <span class="ref-name" :title="node.label">{{ node.label }}</span>
            <span class="ref-type-tag">{{ nodeTypeLabel(node.nodeType) }}</span>
            <el-button type="primary" size="small" plain @click="quickInsert(node.label)">+ 插入</el-button>
          </div>
        </div>
      </div>
    </transition>
    <div class="pre-editor-wrap">
      <div class="pre-editor-header">
        <span class="pre-editor-label">提示词模板</span>
        <el-button type="primary" size="small" link @click="showRefPanel = !showRefPanel">
          <el-icon><Link /></el-icon> {{ showRefPanel ? '收起节点' : '选择节点' }}
          <span v-if="!showRefPanel && refNodes.length > 0" class="ref-count">({{ refNodes.length }})</span>
        </el-button>
      </div>
      <el-input ref="textareaRef" :model-value="modelValue" type="textarea" :rows="rows" placeholder="编写提示词... 从上方选择节点自动插入 {{节点名.result}}" @update:model-value="$emit('update:modelValue', $event)" />
      <div v-if="extractedRefs.length > 0" class="pre-ref-inline">
        <span v-for="(r, idx) in extractedRefs" :key="idx" class="ref-chip" @click="removeRef(r)">
          <code>{{ wrapRef(r) }}</code><span class="ref-chip-close">×</span>
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { Link, Close } from '@element-plus/icons-vue'

const props = withDefaults(defineProps<{
  modelValue?: string
  rows?: number
  linkableNodes?: Array<any>
}>(), { modelValue: '', rows: 6, linkableNodes: () => [] })

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const textareaRef = ref<any>(null)
const showRefPanel = ref(true)

function nodeTypeLabel(type: string) {
  const map: Record<string, string> = { start: '开始', end: '结束', branch: '分支', ai_node: 'AI', loop: '循环', docField: '字段提取', docTable: '表格提取', compare: '值比较', assemble: '文本拼接', constant: '常量', skill: '功能' }
  return map[type] || type || ''
}

const refNodes = computed(() => {
  if (!props.linkableNodes || !Array.isArray(props.linkableNodes)) return []
  return props.linkableNodes.filter(n => n && n.id).map(node => ({
    id: node.id, label: node.title || node.label || '未命名', nodeType: node.nodeType || '',
    color: node.color || '', ports: (node.outputPorts?.length > 0) ? node.outputPorts.filter((p: any) => p?.name) : [{ name: 'result', label: '结果', type: 'string' }]
  }))
})

const extractedRefs = computed(() => {
  const text = props.modelValue || ''
  if (!text) return []
  const regex = /\{\{([^}]+)\}\}/g; const refs: string[] = []; let match
  while ((match = regex.exec(text)) !== null) { if (match[1] && !refs.includes(match[1])) refs.push(match[1]) }
  return refs
})

function quickInsert(nodeName: string) {
  const syntax = `{{${nodeName}.result}}`
  const el = textareaRef.value?.$el?.querySelector('textarea')
  if (!el) { emit('update:modelValue', (props.modelValue || '') + syntax); return }
  const start = el.selectionStart, end = el.selectionEnd, text = props.modelValue || ''
  emit('update:modelValue', text.substring(0, start) + syntax + text.substring(end))
  setTimeout(() => { el.focus(); try { el.setSelectionRange(start + syntax.length, start + syntax.length) } catch (_) {} }, 20)
}

function removeRef(refKey: string) { emit('update:modelValue', (props.modelValue || '').split(`{{${refKey}}}`).join('')) }

function wrapRef(r: string) { return `{{${r}}}` }
</script>

<style scoped lang="less">
.pre { display: flex; flex-direction: column; border: 1px solid #e4e7ed; border-radius: 6px; background: #fff; overflow: hidden; }
.ref-bar { border-bottom: 1px solid #ebeef5; background: #fafbfc; flex-shrink: 0; }
.ref-bar-head { display: flex; align-items: center; justify-content: space-between; padding: 5px 10px; background: #fdf2fb; border-bottom: 1px solid #f0f0f0; }
.ref-bar-title { font-size: 11px; font-weight: 600; color: #9C27B0; }
.ref-empty { padding: 8px 12px; text-align: center; font-size: 11px; color: #c0c4cc; }
.ref-rows { padding: 4px 8px; display: flex; flex-direction: column; gap: 2px; }
.ref-row { display: flex; align-items: center; gap: 6px; padding: 4px 6px; border-radius: 4px; transition: background 0.1s; &:hover { background: #f5f7fa; } }
.ref-dot { width: 6px; height: 6px; border-radius: 50%; flex-shrink: 0; }
.ref-name { font-size: 12px; color: #303133; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1; min-width: 0; }
.ref-type-tag { font-size: 10px; color: #909399; background: #f0f0f0; padding: 1px 6px; border-radius: 8px; flex-shrink: 0; white-space: nowrap; }
.pre-editor-wrap { flex: 1; display: flex; flex-direction: column; min-height: 0; }
.pre-editor-header { display: flex; align-items: center; justify-content: space-between; padding: 5px 10px; background: #fff; border-bottom: 1px solid #ebeef5; flex-shrink: 0; }
.pre-editor-label { font-size: 12px; font-weight: 600; color: #303133; }
.ref-count { font-size: 10px; color: #9C27B0; margin-left: 2px; }
:deep(.el-textarea__inner) { border: none; box-shadow: none; padding: 10px; font-family: inherit; line-height: 1.6; flex: 1; min-height: 100px; resize: vertical; &:focus { box-shadow: none; } }
.pre-ref-inline { display: flex; flex-wrap: wrap; gap: 4px; padding: 4px 10px; background: #fafbfc; border-top: 1px solid #ebeef5; flex-shrink: 0; }
.ref-chip { display: inline-flex; align-items: center; gap: 3px; padding: 1px 6px 1px 8px; background: #f0e6ff; color: #722ed1; border-radius: 10px; font-size: 11px; cursor: pointer; transition: background 0.15s; code { font-family: monospace; color: inherit; } &:hover { background: #d3adf7; } }
.ref-chip-close { font-size: 14px; line-height: 1; color: #b37feb; opacity: 0.5; .ref-chip:hover & { opacity: 1; } }
.slide-fade-enter-active { transition: all 0.18s ease-out; }
.slide-fade-leave-active { transition: all 0.12s ease-in; }
.slide-fade-enter-from, .slide-fade-leave-to { max-height: 0; opacity: 0; overflow: hidden; }
</style>
