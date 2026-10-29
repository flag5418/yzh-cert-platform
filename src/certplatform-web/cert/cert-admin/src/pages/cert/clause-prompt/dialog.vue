<script setup lang="ts">
/**
 * 提示词编辑弹窗（新增 / 编辑 共用）
 *
 * - 表单字段由 Logic `formFields` 驱动
 - 顶部：新增时给出编码命名建议；编辑时显示行信息
 * - 中部：提示词正文 textarea（带 placeholder 表格）
 * - 底部：AI 生成草稿按钮（可选）
 *
 * ★ YzhDrawer 比 YzhDialog 更适合：提示词正文动辄 1000+ 字，需要大宽度。
 */
import { ref, computed } from 'vue'
import { ElDrawer, ElForm, ElFormItem, ElInput, ElSelect, ElOption, ElButton } from 'element-plus'
import { CLAUSE_PROMPT_PLACEHOLDERS } from './logic'
import type { ClausePromptLogic } from './logic'

const props = defineProps<{
  logic: ClausePromptLogic
}>()

const emit = defineEmits<{
  saved: []
}>()

/** 表单数据 */
const form = ref<Record<string, any>>({ ...props.logic.formDefaults })
const submitting = ref(false)
const showPlaceholders = ref(false)

/** 是否为编辑模式 */
const isEdit = computed(() => props.logic.editingRow.value != null)
const drawerTitle = computed(() =>
  isEdit.value ? `编辑提示词：${props.logic.editingRow.value?.promptName}` : '新增条款属性提示词',
)

/** AI 生成额外要求 */
const requirement = ref('')
const generating = ref(false)

/** 提交 */
async function handleSubmit() {
  if (!form.value.promptCode?.trim()) {
    return
  }
  if (!form.value.promptName?.trim()) {
    return
  }
  if (!form.value.template?.trim()) {
    return
  }
  submitting.value = true
  try {
    await props.logic.savePrompt({ ...form.value })
    emit('saved')
  } finally {
    submitting.value = false
  }
}

/** AI 生成草稿 */
async function handleGenerate() {
  generating.value = true
  try {
    await props.logic.generateDraft(form.value.template || '')
  } finally {
    generating.value = false
  }
}

/** 写入 AI 草稿到表单 */
function applyDraft() {
  if (props.logic.aiDraft.value) {
    form.value.template = props.logic.aiDraft.value
    props.logic.aiDraft.value = ''
  }
}
</script>

<template>
  <ElDrawer
    :model-value="true"
    :title="drawerTitle"
    direction="rtl"
    size="820px"
    :before-close="() => props.logic.dialogVisible.value = false"
    :close-on-click-modal="false"
    :show-close="true"
    append-to-body
  >
    <div class="clause-prompt-dialog">
      <ElForm :model="form" label-width="100px" label-position="right">
        <!-- 编辑时只读展示 PromptCode -->
        <ElFormItem label="提示词编码" required>
          <ElInput
            v-if="props.logic.isCodeEditable"
            v-model="form.promptCode"
            placeholder="如 clause_attr_platform / clause_attr_iso9001"
          />
          <ElInput v-else :model-value="form.promptCode" disabled />
        </ElFormItem>

        <ElFormItem label="名称" required>
          <ElInput v-model="form.promptName" placeholder="如「条款属性预生成提示词（平台级）」" />
        </ElFormItem>

        <ElFormItem label="适用标准">
          <ElSelect v-model="form.standardCode" placeholder="留空 = 平台级默认（对所有标准生效）" style="width: 100%">
            <ElOption label="平台级（全部标准）" value="" />
            <ElOption
              v-for="s in props.logic.standardOptions.value"
              :key="s.code"
              :label="s.display"
              :value="s.code"
            />
          </ElSelect>
        </ElFormItem>

        <ElFormItem label="提示词正文" required>
          <div class="clause-prompt-dialog__template">
            <ElInput
              v-model="form.template"
              type="textarea"
              :rows="20"
              placeholder="提示词正文（支持 {{standard_name}} 等占位符）"
              resize="vertical"
            />
            <div class="clause-prompt-dialog__actions">
              <ElButton text size="small" @click="showPlaceholders = !showPlaceholders">
                {{ showPlaceholders ? '隐藏' : '查看' }}可用占位符
              </ElButton>
            </div>
            <div v-if="showPlaceholders" class="clause-prompt-dialog__placeholders">
              <div class="clause-prompt-dialog__placeholders-title">可用占位符（{{ }} 由后端渲染注入）：</div>
              <div
                v-for="p in CLAUSE_PROMPT_PLACEHOLDERS"
                :key="p.token"
                class="clause-prompt-dialog__placeholder"
              >
                <code class="clause-prompt-dialog__token">{{ p.token }}</code>
                <span class="clause-prompt-dialog__desc">{{ p.desc }}</span>
              </div>
            </div>

            <!-- AI 生成草稿 -->
            <div class="clause-prompt-dialog__ai">
              <div class="clause-prompt-dialog__ai-title">AI 辅助生成 / 优化</div>
              <ElInput
                v-model="requirement"
                type="textarea"
                :rows="3"
                placeholder="可选：告诉 AI 额外要求（如「强调 Artifacts 要区分 mandatory / optional」）"
                resize="vertical"
              />
              <div class="clause-prompt-dialog__ai-buttons">
                <ElButton
                  type="primary"
                  :loading="generating"
                  @click="handleGenerate"
                >
                  {{ form.template ? '基于现有内容优化' : '从零生成草稿' }}
                </ElButton>
                <ElButton
                  v-if="props.logic.aiDraft.value"
                  @click="applyDraft"
                >
                  写入草稿到正文
                </ElButton>
              </div>
              <div class="clause-prompt-dialog__ai-hint">
                 AI 生成的草稿不会直接保存，请您检查确认后再点底部「保存」。
              </div>
            </div>
          </div>
        </ElFormItem>

        <ElFormItem label="说明">
          <ElInput
            v-model="form.description"
            type="textarea"
            :rows="3"
            placeholder="可选：这条提示词适用于什么场景、有什么限制"
            resize="vertical"
          />
        </ElFormItem>
      </ElForm>

      <div class="clause-prompt-dialog__footer">
        <ElButton @click="props.logic.dialogVisible.value = false">取消</ElButton>
        <ElButton type="primary" :loading="submitting" @click="handleSubmit">保存</ElButton>
      </div>
    </div>
  </ElDrawer>
</template>

<style scoped>
.clause-prompt-dialog {
  display: flex;
  flex-direction: column;
  height: 100%;
  box-sizing: border-box;
  overflow-y: auto;
}

.clause-prompt-dialog__template {
  display: flex;
  flex-direction: column;
  gap: 8px;
  width: 100%;
}

.clause-prompt-dialog__actions {
  display: flex;
  justify-content: flex-end;
}

.clause-prompt-dialog__placeholders {
  padding: var(--yzh-space-2, 8px) var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-md, 6px);
  background: var(--el-fill-color-light);
  border-left: 3px solid var(--el-color-primary);
}

.clause-prompt-dialog__placeholders-title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: 600;
  color: var(--el-text-color-primary);
  margin-bottom: var(--yzh-space-2, 8px);
}

.clause-prompt-dialog__placeholder {
  display: flex;
  align-items: baseline;
  gap: 8px;
  padding: var(--yzh-space-1, 4px) 0;
  font-size: var(--yzh-font-size-sm, 13px);
  line-height: 1.6;
}

.clause-prompt-dialog__token {
  font-family: var(--yzh-font-family-mono, monospace);
  color: var(--el-color-primary);
  font-weight: 600;
  flex-shrink: 0;
}

.clause-prompt-dialog__desc {
  color: var(--el-text-color-regular);
}

.clause-prompt-dialog__ai {
  margin-top: var(--yzh-space-3, 12px);
  padding: var(--yzh-space-3, 12px);
  border-radius: var(--yzh-radius-md, 6px);
  background: var(--el-fill-color-blank, #fafbfc);
  border: 1px dashed var(--el-border-color);
}

.clause-prompt-dialog__ai-title {
  font-size: var(--yzh-font-size-sm, 13px);
  font-weight: 600;
  color: var(--el-color-primary);
  margin-bottom: var(--yzh-space-2, 8px);
}

.clause-prompt-dialog__ai-buttons {
  display: flex;
  gap: var(--yzh-space-2, 8px);
  margin-top: var(--yzh-space-2, 8px);
}

.clause-prompt-dialog__ai-hint {
  margin-top: var(--yzh-space-2, 8px);
  font-size: var(--yzh-font-size-xs, 12px);
  color: var(--el-text-color-secondary);
}

.clause-prompt-dialog__footer {
  display: flex;
  justify-content: flex-end;
  gap: var(--yzh-space-2, 8px);
  padding-top: var(--yzh-space-3, 12px);
  border-top: 1px solid var(--el-border-color-lighter);
  margin-top: var(--yzh-space-3, 12px);
}
</style>
