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
            <span class="ref-dot" :style="{ background: node.color || 'var(--yzh-color-text-tertiary, #909399)' }"></span>
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
      <el-input
        ref="textareaRef"
        :model-value="localValue"
        type="textarea"
        :rows="rows"
        :style="{ '--rows': rows }"
        placeholder="编写提示词... 从上方选择节点自动插入 {{节点名.result}}"
        @input="onInput"
        @change="commit"
      />
      <div v-if="extractedRefs.length > 0" class="pre-ref-inline">
        <span v-for="(r, idx) in extractedRefs" :key="idx" class="ref-chip" @click="removeRef(r)">
          <code>{{ wrapRef(r) }}</code><span class="ref-chip-close">×</span>
        </span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { Link, Close } from '@element-plus/icons-vue'

const props = withDefaults(defineProps<{
  modelValue?: string
  rows?: number
  linkableNodes?: Array<any>
}>(), { modelValue: '', rows: 6, linkableNodes: () => [] })

const emit = defineEmits<{ 'update:modelValue': [value: string] }>()
const textareaRef = ref<any>(null)
const showRefPanel = ref(true)

/**
 * ★ 2026-10-15：编辑器内部持有 `localValue`，**输入过程零外部写入**，
 * 仅在「失焦（`change`）」或「父组件显式 `commit()`（切节点/换选中前）」两个出口向上提交。
 *
 * <para><b>原实现的缺陷</b>：`@update:model-value.lazy` 里的 `.lazy` 对**组件自定义事件无效** ——
 * Vue 的 `modifierGuards` 只认 `stop/prevent/self/ctrl/shift/alt/meta/左右中键/exact`，
 * `lazy` 不在表内 ⇒ 修饰符被**静默丢弃**，退化成「每敲一个字符都 emit」。
 * 而父链路上每一次 emit 都会写 store + 调 `LogicFlow.setProperties` 重绘画布，
 * 早期版本还会连带重建整个属性面板（`:key` 已摘除），
 * 用户侧表现为**「输入时滚动条自动跳、基本没法输入」**。</para>
 */
const localValue = ref(props.modelValue || '')

/** 外部值变化（切节点、撤销、程序化回灌）→ 覆盖本地缓冲；⛔ 仅在有真实差异时覆盖，避免打断正在进行的输入 */
watch(() => props.modelValue, (v) => {
  const next = v ?? ''
  if (next !== localValue.value) localValue.value = next
})

function onInput(v: string) { localValue.value = v }

/** 提交本地缓冲 —— 幂等：无差异时不 emit，避免「切节点顺带写库」这类无谓写入 */
function commit() {
  if (localValue.value !== (props.modelValue ?? '')) emit('update:modelValue', localValue.value)
}
// ★ 供父组件在「切换选中节点」前显式排空缓冲（LogicFlow 的节点点击可能早于 textarea 的 blur，
//   只靠 `change` 会丢掉最后一次未失焦的编辑）。
defineExpose({ commit })

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

/** 引用芯片跟随**本地**缓冲 —— 边打边显示，不必等失焦 */
const extractedRefs = computed(() => {
  const text = localValue.value || ''
  if (!text) return []
  const regex = /\{\{([^}]+)\}\}/g; const refs: string[] = []; let match
  while ((match = regex.exec(text)) !== null) { if (match[1] && !refs.includes(match[1])) refs.push(match[1]) }
  return refs
})

function quickInsert(nodeName: string) {
  const syntax = `{{${nodeName}.result}}`
  const el = textareaRef.value?.$el?.querySelector('textarea')
  if (!el) { localValue.value = (localValue.value || '') + syntax; commit(); return }
  const start = el.selectionStart, end = el.selectionEnd, text = localValue.value || ''
  localValue.value = text.substring(0, start) + syntax + text.substring(end)
  // 按钮点击 = 显式动作，立即提交（不必等失焦）
  commit()
  setTimeout(() => { el.focus(); try { el.setSelectionRange(start + syntax.length, start + syntax.length) } catch (_) {} }, 20)
}

function removeRef(refKey: string) {
  localValue.value = (localValue.value || '').split(`{{${refKey}}}`).join('')
  commit()
}

function wrapRef(r: string) { return `{{${r}}}` }
</script>

<style scoped lang="less">
.pre { display: flex; flex-direction: column; border: 1px solid var(--yzh-color-border, #e4e7ed); border-radius: 6px; background: var(--yzh-color-bg-container, #fff); overflow: hidden; }
.ref-bar { border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5); background: var(--yzh-color-bg-subtle, #fafbfc); flex-shrink: 0; }
.ref-bar-head { display: flex; align-items: center; justify-content: space-between; padding: 5px 10px; background: #fdf2fb; border-bottom: 1px solid #f0f0f0; }
.ref-bar-title { font-size: 11px; font-weight: 600; color: #9C27B0; }
.ref-empty { padding: 8px 12px; text-align: center; font-size: 11px; color: var(--yzh-color-text-disabled, #c0c4cc); }
.ref-rows { padding: 4px 8px; display: flex; flex-direction: column; gap: 2px; }
.ref-row { display: flex; align-items: center; gap: 6px; padding: 4px 6px; border-radius: 4px; transition: background 0.1s; &:hover { background: var(--yzh-color-bg-page, #f5f7fa); } }
.ref-dot { width: 6px; height: 6px; border-radius: 50%; flex-shrink: 0; }
.ref-name { font-size: 12px; color: var(--yzh-color-text-primary, #303133); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; flex: 1; min-width: 0; }
.ref-type-tag { font-size: 10px; color: var(--yzh-color-text-tertiary, #909399); background: #f0f0f0; padding: 1px 6px; border-radius: 8px; flex-shrink: 0; white-space: nowrap; }
.pre-editor-wrap { flex-shrink: 0; display: flex; flex-direction: column; }
.pre-editor-header { display: flex; align-items: center; justify-content: space-between; padding: 5px 10px; background: var(--yzh-color-bg-container, #fff); border-bottom: 1px solid var(--yzh-color-border-light, #ebeef5); flex-shrink: 0; }
.pre-editor-label { font-size: 12px; font-weight: 600; color: var(--yzh-color-text-primary, #303133); }
.ref-count { font-size: 10px; color: #9C27B0; margin-left: 2px; }
:deep(.el-textarea__inner) { border: none; box-shadow: none; padding: 10px; font-family: inherit; line-height: 1.6; height: calc(var(--rows, 6) * 24px + 24px); min-height: 0; max-height: none; resize: vertical; overflow-anchor: none; &:focus { box-shadow: none; } }
.pre-ref-inline { display: flex; flex-wrap: wrap; gap: 4px; padding: 4px 10px; background: var(--yzh-color-bg-subtle, #fafbfc); border-top: 1px solid var(--yzh-color-border-light, #ebeef5); flex-shrink: 0; }
.ref-chip { display: inline-flex; align-items: center; gap: 3px; padding: 1px 6px 1px 8px; background: #f0e6ff; color: #722ed1; border-radius: 10px; font-size: 11px; cursor: pointer; transition: background 0.15s; code { font-family: monospace; color: inherit; } &:hover { background: #d3adf7; } }
.ref-chip-close { font-size: 14px; line-height: 1; color: #b37feb; opacity: 0.5; .ref-chip:hover & { opacity: 1; } }
.slide-fade-enter-active { transition: all 0.18s ease-out; }
.slide-fade-leave-active { transition: all 0.12s ease-in; }
.slide-fade-enter-from, .slide-fade-leave-to { max-height: 0; opacity: 0; overflow: hidden; }
</style>
