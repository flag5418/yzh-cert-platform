<script setup lang="ts">
/**
 * 提示词编辑器（2026-10-02 重构：只管正文）
 *
 * ★ 作用域标题与 AI生成/恢复/保存 已上移到 `WorkbenchBar`（共享操作条）——
 *   重构前它们散在中栏头部、底部两处，而类型切换又在另一个 el-tabs 里，
 *   用户要三个地方才能拼出"我在编辑哪条、动作作用于谁"。
 * ★ 占位符以「无花括号」形式列出，点击插入时才补 `{{ }}`：
 *   后端 PromptRenderer 是**单趟** Regex.Replace，正文里写错一次就永远替换不掉。
 */
import { ref, nextTick } from 'vue'
import type { PromptTypeDef } from '../logic'
import { describePlaceholder } from '../logic'

const props = defineProps<{
  modelValue: string
  typeDef: PromptTypeDef
  dirty: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', v: string): void
}>()

const taRef = ref<HTMLTextAreaElement>()

function onInput(e: Event) {
  emit('update:modelValue', (e.target as HTMLTextAreaElement).value)
}

/** 点占位符 → 在光标处插入 `{{名称}}` */
function insertPlaceholder(name: string) {
  const el = taRef.value
  const text = `{{${name}}}`
  if (!el) {
    emit('update:modelValue', props.modelValue + text)
    return
  }
  const start = el.selectionStart ?? props.modelValue.length
  const end = el.selectionEnd ?? start
  const next = props.modelValue.slice(0, start) + text + props.modelValue.slice(end)
  emit('update:modelValue', next)
  nextTick(() => {
    el.focus()
    const pos = start + text.length
    el.setSelectionRange(pos, pos)
  })
}

/** ⌘/Ctrl + Enter 插入换行（textarea 默认行为），Tab 插入缩进 */
function onKeydown(e: KeyboardEvent) {
  if (e.key !== 'Tab') return
  e.preventDefault()
  const el = e.target as HTMLTextAreaElement
  const start = el.selectionStart ?? props.modelValue.length
  const end = el.selectionEnd ?? start
  emit('update:modelValue', props.modelValue.slice(0, start) + '  ' + props.modelValue.slice(end))
  nextTick(() => el.setSelectionRange(start + 2, start + 2))
}
</script>

<template>
  <div class="prompt-editor">
    <!-- 占位符条（★ 每个占位符带 tooltip 说明「运行时会被替换成什么」，
         让实施人员不必靠猜，也不必回来找开发确认） -->
    <div class="prompt-editor__chips">
      <span class="prompt-editor__chips-label">占位符</span>
      <el-tooltip
        v-for="p in typeDef.placeholders"
        :key="p"
        placement="bottom"
        :show-after="120"
        effect="dark"
        :content="describePlaceholder(p)"
      >
        <el-tag
          size="small"
          effect="plain"
          class="prompt-editor__chip"
          @click="insertPlaceholder(p)"
        >
          + {{ p }}
        </el-tag>
      </el-tooltip>
    </div>

    <!-- 正文 -->
    <textarea
      ref="taRef"
      class="prompt-editor__ta"
      :value="modelValue"
      spellcheck="false"
      placeholder="正文为空"
      @input="onInput"
      @keydown="onKeydown"
    />

    <!-- 计数（右下角，不与操作区抢位置） -->
    <div class="prompt-editor__foot">
      <span class="prompt-editor__stat">{{ modelValue.length }} 字符</span>
      <em v-if="dirty" class="prompt-editor__dirty">未保存</em>
    </div>
  </div>
</template>

<style scoped>
.prompt-editor {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  background: var(--yzh-color-bg-container, #fff);
}

.prompt-editor__chips {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--yzh-space-1) var(--yzh-space-2);
  padding: var(--yzh-space-2) var(--yzh-space-5);
  background: var(--yzh-color-bg-subtle);
  border-bottom: 1px solid var(--yzh-color-border-light);
}
.prompt-editor__chips-label {
  font-size: var(--yzh-font-size-xs);
  color: var(--yzh-color-text-tertiary);
  margin-right: var(--yzh-space-1);
}
.prompt-editor__chip {
  cursor: pointer;
  font-family: 'SFMono-Regular', Consolas, monospace;
}
.prompt-editor__chip:hover {
  color: var(--yzh-color-primary);
  border-color: var(--yzh-color-primary);
}

.prompt-editor__ta {
  flex: 1;
  min-height: 0;
  width: 100%;
  box-sizing: border-box;
  padding: var(--yzh-space-4) var(--yzh-space-5);
  border: none;
  outline: none;
  resize: none;
  background: var(--yzh-color-bg-container);
  font-family: 'SFMono-Regular', Consolas, Monaco, 'Courier New', monospace;
  font-size: var(--yzh-font-size-sm);
  line-height: var(--yzh-line-height-relaxed);
  color: var(--yzh-color-text-primary);
}
.prompt-editor__ta::placeholder {
  color: var(--yzh-color-text-placeholder);
}

.prompt-editor__foot {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--yzh-space-2);
  padding: var(--yzh-space-1) var(--yzh-space-5);
  border-top: 1px solid var(--yzh-color-border-light);
  background: var(--yzh-color-bg-container);
}
.prompt-editor__stat {
  font-size: var(--yzh-font-size-xs);
  color: var(--yzh-color-text-tertiary);
  font-variant-numeric: tabular-nums;
}
.prompt-editor__dirty {
  font-style: normal;
  font-size: var(--yzh-font-size-xs);
  color: var(--yzh-color-warning);
}
</style>
