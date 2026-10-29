<script setup lang="ts">
/**
 * PromptDrawer — 条款属性预生成提示词编辑（嵌入 clause-attr 页的右侧抽屉）
 *
 * ★ 业务定位：
 *   这条提示词对**全部标准、全部条款**生效。写好后点「AI 预生成」时，
 *   后端把 {{attr_pool}} 替换成**当前属性池**实时渲染的属性清单段 ——
 *   属性池增删改 ⇒ 提示词自动跟上，⛔ 不需要再改正文。
 *
 * ★ 为什么是属性池驱动（不是硬编码属性名在正文里）：
 *   24 号设计 §六：属性池现在 8 条，规划 30-50 条。
 *   把判据写死在正文里 ⇒ 池子一变模型就不产新属性，挂接静默缺失且不报错。
 *   有 {{attr_pool}} 占位符 → 属性池变化自动反映到提示词 → 属性挂接和值的自动生成永远与池同步。
 *
 * ★ 抽屉布局：
 *   ① 顶部：说明（这条提示词怎样被使用）
 *   ② 中部：编辑器（textarea + 占位符辅助按钮）
 *   ③ 属性池注入段预览（只读、实时与属性池同步）
 *   ④ 保存按钮
 */
import { ref, computed, watch } from 'vue'
import { ElDrawer, ElButton, ElInput, ElAlert, ElTag, ElDivider, ElMessage } from 'element-plus'
import {
  getClausePrompt,
  saveClausePrompt,
  CLAUSE_ATTR_PROMPT_PLACEHOLDERS,
  type ClauseAttrPromptDto,
} from '@share/api/cert/clause-attr'

// ============================================================
// Props / Emits
// ============================================================

const props = defineProps<{
  /** 抽屉可见性（由父组件 v-model 控制） */
  modelValue: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  saved: []
}>()

// ============================================================
// 状态
// ============================================================

/** 提示词完整数据 */
const prompt = ref<ClauseAttrPromptDto | null>(null)

/** 编辑器中的模板（本地草稿） */
const draft = ref('')

/** 加载中 */
const loading = ref(false)

/** 保存中 */
const saving = ref(false)

/** 是否显示占位符辅助面板 */
const showPlaceholders = ref(false)

/** 是否有尚未保存的改动 */
const isDirty = computed(() => prompt.value != null && draft.value !== prompt.value.Template)

/** 正文是否包含 {{attr_pool}} 占位符 */
const hasPoolPlaceholder = computed(() => draft.value.includes(ATTR_POOL_TOKEN))

/** 属性池占位符字面量（避免 Vue 模板中 {{ 被解析为插值） */
const ATTR_POOL_TOKEN = '{{attr_pool}}'

// ============================================================
// 方法
// ============================================================

/** 载入提示词 */
async function fetchPrompt(): Promise<void> {
  loading.value = true
  try {
    prompt.value = await getClausePrompt()
    draft.value = prompt.value.Template
  } catch (e) {
    ElMessage.error(`加载提示词失败：${(e as Error).message || e}`)
  } finally {
    loading.value = false
  }
}

/** 保存提示词 */
async function handleSave(): Promise<void> {
  if (!draft.value.trim()) {
    ElMessage.warning('提示词正文不能为空')
    return
  }
  saving.value = true
  try {
    await saveClausePrompt(draft.value)
    ElMessage.success('提示词已保存（下次 AI 预生成时生效）')
    emit('saved')
    prompt.value = { ...prompt.value!, Template: draft.value, Stored: true }
  } catch (e) {
    ElMessage.error(`保存失败：${(e as Error).message || e}`)
  } finally {
    saving.value = false
  }
}

/** 关闭抽屉 */
function handleClose(): void {
  emit('update:modelValue', false)
}

/** 在光标处插入占位符 */
function insertPlaceholder(token: string): void {
  // textarea 插入：尽量用当前选区，否则追加到末尾
  draft.value = draft.value + (draft.value.endsWith('\n') || draft.value.length === 0 ? '' : '\n') + token
}

/** 打开抽屉即拉取最新提示词（watch 而非 onMounted：组件挂载时 drawer 还是隐藏的） */
watch(
  () => props.modelValue,
  (visible) => {
    if (visible) fetchPrompt()
  },
)
</script>

<template>
  <ElDrawer
    :model-value="props.modelValue"
    title="编辑提示词（条款属性预生成）"
    direction="rtl"
    size="640px"
    :before-close="handleClose"
    :close-on-click-modal="false"
    :show-close="true"
    append-to-body
    class="prompt-drawer"
  >
    <!-- 用单层 flex 容器撑满抽屉内容区，footer 固定底部，body 滚动 -->
    <div class="prompt-drawer__layout">
      <!-- 滚动区 -->
      <div v-loading="loading" class="prompt-drawer__body">
      <!-- 顶部说明 -->
      <ElAlert
        type="info"
        :closable="false"
        show-icon
        class="prompt-drawer__alert"
      >
        <template #title>
          这条提示词对<b>全部标准、全部条款</b>生效。下次点「AI 预生成」时使用。
        </template>
        <template #default>
          <div class="prompt-drawer__alert-body">
属性池（当前 {{ prompt?.AttrCount ?? '?' }} 条启用）通过 <code>{{ ATTR_POOL_TOKEN }}</code>
占位符<b>实时注入</b>
          </div>
        </template>
      </ElAlert>

      <!-- 未落库提示 -->
      <ElAlert
        v-if="prompt && !prompt.Stored"
        type="warning"
        :closable="false"
        show-icon
        class="prompt-drawer__alert"
      >
        <template #title>当前使用的是系统兜底正文（尚未保存到库）</template>
        <template #default>
          保存后即以您编辑的版本为准；后续可通过 {{ ATTR_POOL_TOKEN }} 占位符自动跟随属性池变化。
        </template>
      </ElAlert>

      <!-- 编辑器 -->
      <div class="prompt-drawer__editor">
        <div class="prompt-drawer__editor-head">
          <span class="prompt-drawer__editor-title">提示词正文</span>
          <ElButton text size="small" @click="showPlaceholders = !showPlaceholders">
            {{ showPlaceholders ? '隐藏' : '占位符' }}辅助
          </ElButton>
        </div>

        <!-- 占位符辅助面板 -->
        <div v-if="showPlaceholders" class="prompt-drawer__placeholders">
          <div class="prompt-drawer__placeholders-title">点击插入占位符（后端渲染注入）：</div>
          <div
            v-for="p in CLAUSE_ATTR_PROMPT_PLACEHOLDERS"
            :key="p.token"
            class="prompt-drawer__placeholder-row"
          >
            <ElTag
              size="small"
              type="primary"
              class="prompt-drawer__placeholder-tag"
              @click="insertPlaceholder(p.token)"
            >
              {{ p.token }}
            </ElTag>
            <span class="prompt-drawer__placeholder-desc">{{ p.desc }}</span>
          </div>
        </div>

        <ElInput
          v-model="draft"
          type="textarea"
          :rows="18"
          placeholder="请填写提示词正文（可使用 {{standard_name}} / {{clause_number}} / {{clause_title}} / {{clause_content}} / {{attr_pool}} 占位符）"
          resize="vertical"
          class="prompt-drawer__textarea"
        />

        <!-- 缺 {{attr_pool}} 警告 -->
        <ElAlert
          v-if="!hasPoolPlaceholder"
          type="warning"
          :closable="false"
          show-icon
          class="prompt-drawer__alert"
        >
<template #title>正文缺少 {{ ATTR_POOL_TOKEN }} 占位符</template>
<template #default>
属性池注入段会被<b>追加到末尾</b>，但建议显式放置占位符以控制位置；属性池变化时注入段自动更新。
</template>
        </ElAlert>
      </div>

      <ElDivider class="prompt-drawer__divider" />

      <!-- 属性池注入段预览 -->
      <div class="prompt-drawer__pool">
        <div class="prompt-drawer__pool-head">
          <span class="prompt-drawer__pool-title">属性池注入段预览（只读 · 实时与属性池同步）</span>
          <ElTag size="small" type="success">{{ prompt?.AttrCount ?? 0 }} 条已启用</ElTag>
        </div>
        <pre class="prompt-drawer__pool-content">{{ prompt?.AttrPoolSection || '（属性池为空 — 请先到「条款属性池」维护属性定义）' }}</pre>
      </div>
      </div><!-- end body -->

      <!-- 底部操作（固定在抽屉底部） -->
      <div class="prompt-drawer__footer">
        <span v-if="isDirty" class="prompt-drawer__dirty-hint">有未保存的改动</span>
        <div class="prompt-drawer__footer-actions">
          <ElButton @click="handleClose">取消</ElButton>
          <ElButton
            type="primary"
            :loading="saving"
            :disabled="!isDirty"
            @click="handleSave"
          >
            保存
          </ElButton>
        </div>
      </div>
    </div><!-- end layout -->
  </ElDrawer>
</template>

<style scoped>
.prompt-drawer {
  /* ★ 继承抽屉默认变量，不硬编码 */
}

:deep(.el-drawer__body) {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 0;
  overflow: hidden;
}

.prompt-drawer__layout {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
}

.prompt-drawer__body {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-3, 12px);
  flex: 1;
  min-height: 0;
  box-sizing: border-box;
  overflow-y: auto;
  padding: var(--yzh-space-4, 16px);
  padding-bottom: var(--yzh-space-3, 12px);
}

.prompt-drawer__alert {
  flex: none;
}

.prompt-drawer__alert-body {
  margin-top: var(--yzh-space-1, 4px);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-base, 1.6);
}

.prompt-drawer__alert-body code {
  padding: 0 var(--yzh-space-1, 4px);
  border-radius: var(--yzh-radius-sm, 4px);
  background: var(--yzh-color-bg-muted, #f3f4f6);
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
}

/* ---------- 编辑器 ---------- */

.prompt-drawer__editor {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  flex: 1;
  min-height: 0;
}

.prompt-drawer__textarea :deep(.el-textarea) {
  height: 100%;
}

.prompt-drawer__editor-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.prompt-drawer__editor-title {
  font-size: var(--yzh-font-size-md, 14px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
}

.prompt-drawer__placeholders {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-md, 6px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border-left: 3px solid var(--yzh-color-primary, #1e3a8a);
}

.prompt-drawer__placeholders-title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-primary, #303133);
  margin-bottom: var(--yzh-space-2, 8px);
}

.prompt-drawer__placeholder-row {
  display: flex;
  align-items: baseline;
  gap: var(--yzh-space-2, 8px);
  padding: var(--yzh-space-1, 4px) 0;
}

.prompt-drawer__placeholder-tag {
  cursor: pointer;
  flex-shrink: 0;
  font-family: var(--yzh-font-family-mono, monospace);
}

.prompt-drawer__placeholder-tag:hover {
  opacity: 0.8;
}

.prompt-drawer__placeholder-desc {
  font-size: var(--yzh-font-size-sm, 13px);
  color: var(--yzh-color-text-regular, #606266);
}

.prompt-drawer__textarea {
  width: 100%;
  flex: 1;
  min-height: 0;
}

.prompt-drawer__textarea :deep(.el-textarea__inner) {
  height: 100%;
  resize: none;
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: var(--yzh-line-height-relaxed, 1.8);
}

/* ---------- 分隔线 ---------- */

.prompt-drawer__divider {
  margin: var(--yzh-space-2, 8px) 0;
}

/* ---------- 属性池预览 ---------- */

.prompt-drawer__pool {
  display: flex;
  flex-direction: column;
  gap: var(--yzh-space-2, 8px);
  flex: none;
}

.prompt-drawer__pool-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.prompt-drawer__pool-title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: var(--yzh-font-weight-semibold, 600);
  color: var(--yzh-color-text-secondary, #606266);
}

.prompt-drawer__pool-content {
  max-height: 240px;
  overflow: auto;
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-md, 6px);
  background: var(--yzh-color-bg-subtle, #f9fafb);
  border: 1px solid var(--yzh-color-border-light, #f1f5f9);
  font-family: var(--yzh-font-family-mono, monospace);
  font-size: var(--yzh-font-size-xs, 12px);
  line-height: var(--yzh-line-height-relaxed, 1.8);
  color: var(--yzh-color-text-body, #334155);
  white-space: pre-wrap;
  word-break: break-word;
}

/* ---------- 底部 ---------- */

.prompt-drawer__footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex: none;
  padding: var(--yzh-space-3, 12px) var(--yzh-space-4, 16px);
  border-top: 1px solid var(--yzh-color-border-light, #f1f5f9);
  background: var(--yzh-color-bg-container, #ffffff);
}

.prompt-drawer__dirty-hint {
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--yzh-color-warning, #d97706);
}

.prompt-drawer__footer-actions {
  display: flex;
  gap: var(--yzh-space-2, 8px);
}
</style>
