<script setup lang="ts">
/**
 * 左栏：标准树
 *
 * 结构（2026-10-02 裁决 ①）：
 *   全部标准（固定根，StandardCode = ''，承载**平台级默认规则**）
 *     └─ 各具体标准（getStandardOptions()）
 *
 * 每个节点下方标注该作用域下已有的提示词：`[分类]` `[作用]`。
 */
import { YzhEmptyState, YzhStatusBadge, resolveStatusBadge } from '@yzh-core'
import { ref, computed, watch, nextTick } from 'vue'
import { Document, Files } from '@element-plus/icons-vue'
import type { StandardOptionDto, PromptTemplateDto } from '@share/api/workflow/prompt-workbench'
import { PROMPT_TYPES, ROOT_SCOPE, hasPrompt, normScope, type StandardScope } from '../logic'

const props = defineProps<{
  standards: StandardOptionDto[]
  /** 两类提示词的全量行（用于计算节点徽章） */
  rows: PromptTemplateDto[]
  /** 当前选中的作用域 code（'' = 全部标准） */
  modelValue: string
}>()

const emit = defineEmits<{ (e: 'update:modelValue', v: string): void; (e: 'select', v: StandardScope): void }>()

const treeRef = ref()

interface TreeNode {
  id: string
  label: string
  isPlatform: boolean
  badges: string[]
  /** 启用状态（0/1）—— 标准节点有，根节点无（徽章判据走 resolveStatusBadge） */
  isValid?: number
  children?: TreeNode[]
}

function badgesFor(scope: string): string[] {
  return PROMPT_TYPES.filter((t) => hasPrompt(props.rows, t.type, scope)).map((t) => t.badge)
}

const treeData = computed<TreeNode[]>(() => [
  {
    id: ROOT_SCOPE.code,
    label: ROOT_SCOPE.label,
    isPlatform: true,
    badges: badgesFor(ROOT_SCOPE.code),
    children: props.standards.map((s) => {
      // ★ 节点 id 必须用 `code`（GUID）—— `wf_prompt_template.StandardCode` 存的就是它。
      //   `standardCode` 是人类可读 slug（如 iso9001，不含年份），拿它去 resolve 永远匹配不到。
      const code = normScope(s.code) || normScope(s.standardCode)
      return {
        id: code,
        label: s.standardName || s.display || code,
        isPlatform: false,
        badges: badgesFor(code),
        isValid: s.isValid
      }
    })
  }
])

function onNodeClick(data: TreeNode) {
  emit('update:modelValue', data.id)
  emit('select', { code: data.id, label: data.label })
}

/**
 * 把高亮拨回 `modelValue` 指向的节点。
 *
 * ★ 为什么需要：`el-tree` 点击时会**立即**移动自己的 current 高亮，而「要不要真的切」
 *   由父组件决定（未保存改动时可能拒绝切换）。若父组件拒绝而不回拨，
 *   就会出现「界面高亮已跳到新标准、右侧内容还是旧的」—— 界面在说谎。
 */
function resync() {
  treeRef.value?.setCurrentKey?.(props.modelValue || ROOT_SCOPE.code)
}
defineExpose({ resync })

// 选中态跟随 modelValue（首次进入 / 父组件重置）
watch(
  () => props.modelValue,
  async (v) => {
    await nextTick()
    treeRef.value?.setCurrentKey?.(v || ROOT_SCOPE.code)
  },
  { immediate: true }
)
</script>

<template>
  <div class="std-tree">
    <div class="std-tree__header">
      <span class="std-tree__title">适用标准</span>
    </div>

    <div class="std-tree__body">
      <el-tree
        ref="treeRef"
        :data="treeData"
        node-key="id"
        highlight-current
        :expand-on-click-node="false"
        :default-expanded-keys="[ROOT_SCOPE.code]"
        @node-click="onNodeClick"
      >
        <template #default="{ data }">
          <span class="std-tree__node">
            <el-icon v-if="data.isPlatform" class="std-tree__icon"><Files /></el-icon>
            <el-icon v-else class="std-tree__icon"><Document /></el-icon>
            <span class="std-tree__label" :title="data.label">{{ data.label }}</span>
            <span v-if="data.badges.length" class="std-tree__badges">
              <em
                v-for="b in data.badges"
                :key="b"
                class="std-tree__badge"
                :class="b === '作用' ? 'std-tree__badge--purpose' : 'std-tree__badge--group'"
              >[{{ b }}]</em>
            </span>
            <!-- 启用/禁用徽章（判据 = @yzh-core resolveStatusBadge，全站同源） -->
            <YzhStatusBadge
              v-if="resolveStatusBadge(data, 'IsValid')"
              class="std-tree__status"
              :type="resolveStatusBadge(data, 'IsValid')?.type"
              :text="resolveStatusBadge(data, 'IsValid')?.text"
              size="small"
            />
          </span>
        </template>
      </el-tree>

      <YzhEmptyState v-if="!standards.length" title="暂无标准" />
    </div>
  </div>
</template>

<style scoped>
.std-tree {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.std-tree__header {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-1);
  padding: var(--yzh-space-3) var(--yzh-space-4);
  border-bottom: 1px solid var(--yzh-color-border-light);
}
.std-tree__title {
  font-size: var(--yzh-font-size-sm);
  font-weight: var(--yzh-font-weight-semibold);
  color: var(--yzh-color-text-primary);
}

.std-tree__body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: var(--yzh-space-2) var(--yzh-space-1);
}

.std-tree__node {
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1);
  padding-right: var(--yzh-space-2);
  min-width: 0;
}
.std-tree__icon {
  color: var(--yzh-color-primary);
  flex-shrink: 0;
}
.std-tree__label {
  font-size: var(--yzh-font-size-sm);
  color: var(--yzh-color-text-regular);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 150px;
}
.std-tree__badges {
  display: inline-flex;
  gap: 3px;
  flex-shrink: 0;
}
.std-tree__badge {
  font-style: normal;
  font-size: var(--yzh-font-size-xs);
  line-height: 1;
  padding: 2px 3px;
  border-radius: 3px;
}
.std-tree__badge--group {
  background: var(--el-color-primary-light-9);
  color: var(--yzh-color-primary);
}
.std-tree__badge--purpose {
  background: var(--el-color-success-light-9);
  color: var(--yzh-color-success);
}
</style>
