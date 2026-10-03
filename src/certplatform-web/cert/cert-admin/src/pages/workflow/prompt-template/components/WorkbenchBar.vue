<script setup lang="ts">
/**
 * 工作台共享操作条（2026-10-02）
 *
 * ★ 为什么抽这一层：重构前「作用域」显示 3 处（中栏头部 / 上传区 / 面包屑），
 *   「类型切换」在中栏一个独立 el-tabs 里，而「AI生成 / 保存」又在编辑器底栏 ——
 *   用户要在三个地方拼出"我现在在编辑哪条、动作作用于谁"。本组件把它们并成一行。
 *
 * ⛔ 不显示 AI 模型：模型由 `cert_sys_config` 统一固定（D.2-1 / Q3=a），UI 不可选也不展示。
 */
import { MagicStick, RefreshLeft, DocumentChecked, Fold, Expand, ArrowRight } from '@element-plus/icons-vue'
import type { PromptTypeDef } from '../logic'

defineProps<{
  /** 作用域面包屑：根（全部标准） */
  rootLabel: string
  /** 作用域面包屑：当前节点（空 = 就是根） */
  nodeLabel?: string
  /** 作用域提示（保存会新建标准版本等） */
  scopeHint?: string
  types: PromptTypeDef[]
  activeType: string
  dirty: boolean
  /**
   * 正文是否有内容（★ 2026-10-03）。
   *
   * ⚠️ 不能再用 `dirty` 决定按钮文案：`dirty` 的语义是「与库内不一致」，
   *   而 `onGenerate` 走「生成」还是「优化」取决于 `!isBlankTemplate(正文)`。
   *    正文已保存（dirty=false）但非空时，按钮显示「AI 生成」、实际执行的却是「优化」——
   *    文案与行为不符，用户点了才发现是「优化方向」输入框。
   */
  hasContent: boolean
  /**
   * 保存状态（★ 2026-10-03）：
   *   saved   = 与库内一致
   *   unsaved = 有改动，正在自动暂存为草稿
   *   draft   = 本次载入恢复了一份未保存草稿
   */
  saveState: 'saved' | 'unsaved' | 'draft'
  generating: boolean
  saving: boolean
  saveBlockedReason?: string
  /** 专注模式：隐藏左树与结果区 */
  focused: boolean
}>()

const emit = defineEmits<{
  (e: 'update:activeType', v: string): void
  (e: 'generate'): void
  (e: 'save'): void
  (e: 'reset'): void
  (e: 'toggleFocus'): void
}>()
</script>

<template>
  <div class="wb-bar">
    <!-- 左：页面标识 + 作用域面包屑 -->
    <div class="wb-bar__left">
      <span class="wb-bar__title">文档语义规则</span>
      <el-icon class="wb-bar__sep"><ArrowRight /></el-icon>
      <el-breadcrumb separator="/">
        <el-breadcrumb-item>{{ rootLabel }}</el-breadcrumb-item>
        <el-breadcrumb-item v-if="nodeLabel">{{ nodeLabel }}</el-breadcrumb-item>
      </el-breadcrumb>
      <el-tag v-if="scopeHint" size="small" type="warning" effect="light" class="wb-bar__hint">
        {{ scopeHint }}
      </el-tag>

      <!-- ★ 保存状态（2026-10-03）：用户要求「有改动，立刻提示」——
           不能等用户切走才发现内容没了。unsaved / draft 两种都带「已自动暂存」，
           明确告诉用户「东西不会丢」，否则他仍会以为必须马上点保存。 -->
      <span
        class="wb-bar__save-state"
        :class="`wb-bar__save-state--${saveState}`"
        :title="
          saveState === 'saved'
            ? '当前正文与已保存内容一致'
            : '改动已自动暂存到本机，切换作用域或类型不会丢失；点「保存」才写入服务端'
        "
      >
        <i class="wb-bar__dot" />
        {{
          saveState === 'saved'
            ? '已保存'
            : saveState === 'draft'
              ? '未保存 · 已恢复草稿'
              : '未保存 · 已自动暂存'
        }}
      </span>
    </div>

    <!-- 中：类型切换（两类提示词共用同一套作用域与保存语义） -->
    <!-- ★ 不写 size="small"：按钮/单选一律用框架默认尺寸，
         与「技能管理」等页面的工具栏按钮同高，避免本页显得「小气」。 -->
    <el-radio-group
      :model-value="activeType"
      class="wb-bar__types"
      @update:model-value="(v: any) => emit('update:activeType', String(v))"
    >
      <el-radio-button v-for="t in types" :key="t.type" :value="t.type">
        {{ t.label }}
      </el-radio-button>
    </el-radio-group>

    <!-- 右：动作组 -->
    <div class="wb-bar__actions">
      <el-button
        class="wb-bar__ai"
        type="primary"
        plain
        :loading="generating"
        :icon="MagicStick"
        @click="emit('generate')"
      >
        {{ hasContent ? 'AI 优化' : 'AI 生成' }}
      </el-button>
      <el-button :icon="RefreshLeft" :disabled="!dirty" @click="emit('reset')"> 恢复 </el-button>
      <el-tooltip
        :content="saveBlockedReason ? `${saveBlockedReason} —— 仍可保存，会先弹确认` : '保存到服务端'"
        placement="bottom"
        :disabled="!saveBlockedReason"
      >
        <span>
          <!-- ★ 2026-10-03：⛔ 不再 `:disabled`。
               原「未测试就禁用保存」把用户卡死（想留个草稿都做不到），
               且与「AI 生成后自动保存」直接冲突。改为软提示（点保存时弹确认）。 -->
          <el-button
            type="primary"
            :icon="DocumentChecked"
            :loading="saving"
            @click="emit('save')"
          >
            保存
          </el-button>
        </span>
      </el-tooltip>
      <el-tooltip :content="focused ? '退出专注' : '专注模式（隐藏左树与结果区）'" placement="bottom">
        <el-button :icon="focused ? Expand : Fold" @click="emit('toggleFocus')" />
      </el-tooltip>
    </div>
  </div>
</template>

<style scoped>
/* ★ 本组件只提供「行内排布」，⛔ 不再自造高度 / 内边距 / 背景 / 下边框 ——
   这些由 `YzhPageLayout` 的标准工具栏统一提供（padding 12px 20px），
   否则本页会比全站其他页面矮 12px、左缩进少 6px，形成尺寸漂移。 */
.wb-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--yzh-space-4);
  width: 100%;
  min-width: 0;
}
.wb-bar__left {
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2);
  min-width: 0;
  flex: 1;
}
.wb-bar__title {
  font-size: var(--yzh-font-size-md);
  font-weight: var(--yzh-font-weight-semibold);
  color: var(--yzh-color-text-primary);
  white-space: nowrap;
}
.wb-bar__sep {
  color: var(--yzh-color-text-placeholder);
  font-size: var(--yzh-font-size-xs);
}
.wb-bar__left :deep(.el-breadcrumb) {
  font-size: var(--yzh-font-size-sm);
  white-space: nowrap;
  overflow: hidden;
}
.wb-bar__hint {
  flex-shrink: 0;
}
.wb-bar__save-state {
  flex-shrink: 0;
  display: inline-flex;
  align-items: center;
  gap: var(--yzh-space-1);
  font-size: var(--yzh-font-size-xs);
  white-space: nowrap;
  color: var(--yzh-color-text-tertiary);
}
.wb-bar__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: currentColor;
  flex-shrink: 0;
}
.wb-bar__save-state--saved {
  color: var(--yzh-color-success);
}
.wb-bar__save-state--unsaved,
.wb-bar__save-state--draft {
  color: var(--yzh-color-warning);
}
.wb-bar__types {
  flex-shrink: 0;
}
.wb-bar__actions {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: var(--yzh-space-2);
}
</style>
