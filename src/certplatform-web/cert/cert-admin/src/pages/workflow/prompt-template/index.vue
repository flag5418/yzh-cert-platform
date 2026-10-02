<script setup lang="ts">
/**
 * 提示词工作台（2026-10-02）
 *
 * 用户口径（逐字）：
 *   「一个标准来生成不同的分组提示词和文件内容提示词」
 *   「选择一个分类的时候，针对分类和作用都有一个 ai 自动生成（这个就是按体系认证的全局规则，自动形成的）」
 *   「我们可以针对标题提示词和作用提示词进行修改，我们还可以在该界面上传文件进行测试
 *     （上传的时候，自动转 markdown），调用提示词得到结果，后直接删除该文件」
 *   「我支持在控制成本情况下合理切换模型，并设置模型参数」
 *
 * 布局（三栏）：
 *   左 = 提示词列表（按「标准 + 类型」筛选，含平台级）
 *   中 = 编辑器（正文 + 模型参数；未保存内容可直接拿去试跑）
 *   右 = 试跑（上传 → 转 Markdown → 调提示词 → 看结果；文件即弃）
 *
 * ★ 契约：信封 camelCase（`res.success` 唯一判据）；DTO 载荷 camelCase；
 *   但**保存/试跑的请求体是 PascalCase**（见 @share/api/workflow/prompt-workbench 的注释）。
 */
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { MagicStick, Plus, Refresh, Check } from '@element-plus/icons-vue'
import { YzhPageLayout } from '@yzh-core'
import {
  PROMPT_TYPE,
  activatePrompt,
  deletePrompt,
  generatePromptDraft,
  getStandardOptions,
  listPrompts,
  savePrompt,
  type PromptTemplateDto,
  type StandardOptionDto
} from '@share/api/workflow/prompt-workbench'
import PromptTestPanel from './components/PromptTestPanel.vue'

/* ============ 顶部筛选 ============ */
const standards = ref<StandardOptionDto[]>([])
/** 空串 = 平台级（不限标准）；★ 存的是 cert_iso_standard.Code（GUID） */
const standardCode = ref<string>('')
const promptType = ref<string>(PROMPT_TYPE.Group)

const TYPE_OPTIONS = [
  { label: '分类提示词', value: PROMPT_TYPE.Group },
  { label: '作用提示词', value: PROMPT_TYPE.Content }
]

const currentStandard = computed(() => standards.value.find((s) => s.code === standardCode.value) || null)
const typeLabel = computed(
  () => TYPE_OPTIONS.find((t) => t.value === promptType.value)?.label || promptType.value
)

/* ============ 列表 ============ */
const prompts = ref<PromptTemplateDto[]>([])
const listLoading = ref(false)
const selectedCode = ref<string>('')

/* ============ 编辑器 ============ */
const form = reactive({
  promptCode: '',
  promptName: '',
  template: '',
  description: '',
  modelName: '',
  maxTokens: undefined as number | undefined,
  temperature: undefined as number | undefined
})
/** 编辑器内容是否与「选中行」不一致（用于提示未保存） */
const dirty = ref(false)
const saving = ref(false)
const isNew = ref(false)

/* ============ AI 生成 ============ */
const genVisible = ref(false)
const genLoading = ref(false)
const genRequirement = ref('')
const genPrompt = ref('')
const genMeta = ref<{ durationMs: number; promptTokens?: number | null; completionTokens?: number | null } | null>(null)

const MODEL_OPTIONS = [
  { label: '（跟随系统默认）', value: '' },
  { label: 'qwen-flash — 最便宜，输出上限 131K', value: 'qwen-flash' },
  { label: 'qwen-turbo — 快、便宜，输出上限 16K', value: 'qwen-turbo' },
  { label: 'qwen-plus — 质量最好，贵约 10 倍', value: 'qwen-plus' }
]

/* ============ 生命周期 ============ */
onMounted(async () => {
  await loadStandards()
  await loadList()
})

async function loadStandards() {
  try {
    standards.value = await getStandardOptions()
  } catch (e: any) {
    ElMessage.error(e?.message || '加载标准列表失败')
  }
}

async function loadList() {
  listLoading.value = true
  try {
    prompts.value = await listPrompts(promptType.value, standardCode.value || null)
    // 选中项已被筛掉 → 清空编辑器
    if (selectedCode.value && !prompts.value.some((p) => p.promptCode === selectedCode.value)) {
      clearEditor()
    }
  } catch (e: any) {
    ElMessage.error(e?.message || '加载提示词列表失败')
  } finally {
    listLoading.value = false
  }
}

function onTypeChange() {
  selectedCode.value = ''
  clearEditor()
  void loadList()
}

function onStandardChange() {
  // el-select clearable 时可能 emit undefined；兜底空串
  standardCode.value = standardCode.value || ''
  selectedCode.value = ''
  clearEditor()
  void loadList()
}

/* ============ 编辑器操作 ============ */
function clearEditor() {
  form.promptCode = ''
  form.promptName = ''
  form.template = ''
  form.description = ''
  form.modelName = ''
  form.maxTokens = undefined
  form.temperature = undefined
  isNew.value = false
  dirty.value = false
}

function selectPrompt(row: PromptTemplateDto) {
  if (dirty.value && selectedCode.value && selectedCode.value !== row.promptCode) {
    void ElMessageBox.confirm('当前编辑内容尚未保存，切换后将丢失。确定继续吗？', '未保存的修改', {
      type: 'warning',
      confirmButtonText: '放弃修改',
      cancelButtonText: '留在本页'
    })
      .then(() => applyRow(row))
      .catch(() => undefined)
    return
  }
  applyRow(row)
}

function applyRow(row: PromptTemplateDto) {
  selectedCode.value = row.promptCode
  form.promptCode = row.promptCode
  form.promptName = row.promptName || ''
  form.template = row.template || ''
  form.description = row.description || ''
  form.modelName = row.modelName || ''
  form.maxTokens = row.maxTokens ?? undefined
  form.temperature = row.temperature ?? undefined
  isNew.value = false
  dirty.value = false
}

/** 新建：编码按「类型 + 标准可读号」给一个建议值，用户可改 */
function onNew() {
  const stdNo = currentStandard.value?.standardCode || 'default'
  clearEditor()
  isNew.value = true
  form.promptCode =
    promptType.value === PROMPT_TYPE.Group ? `doc_group_${stdNo}` : `doc_content_${stdNo}`
  form.promptName =
    promptType.value === PROMPT_TYPE.Group
      ? `${currentStandard.value?.standardName || '通用'} 资料分类提示词`
      : `${currentStandard.value?.standardName || '通用'} 文件作用提示词`
  selectedCode.value = ''
}

function onEditorInput() {
  dirty.value = true
}

async function onSave() {
  if (!form.promptCode.trim()) {
    ElMessage.warning('提示词编码（PromptCode）不能为空')
    return
  }
  if (!form.template.trim()) {
    ElMessage.warning('提示词内容不能为空')
    return
  }

  saving.value = true
  try {
    await savePrompt({
      promptCode: form.promptCode.trim(),
      promptName: form.promptName.trim() || form.promptCode.trim(),
      promptType: promptType.value,
      standardCode: standardCode.value || null,
      template: form.template,
      description: form.description || null,
      modelName: form.modelName || null,
      maxTokens: form.maxTokens,
      temperature: form.temperature
    })
    ElMessage.success('保存成功（已置为生效，版本号 +1）')
    dirty.value = false
    isNew.value = false
    selectedCode.value = form.promptCode.trim()
    await loadList()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

async function onActivate(row: PromptTemplateDto) {
  try {
    await activatePrompt(row.promptCode)
    ElMessage.success(`已将「${row.promptName || row.promptCode}」设为生效`)
    await loadList()
  } catch (e: any) {
    ElMessage.error(e?.message || '操作失败')
  }
}

async function onDelete(row: PromptTemplateDto) {
  try {
    await ElMessageBox.confirm(
      `确定删除提示词「${row.promptName || row.promptCode}」吗？（逻辑禁用，可在数据库中恢复）`,
      '删除提示词',
      { type: 'warning', confirmButtonText: '删除', cancelButtonText: '取消' }
    )
  } catch {
    return
  }
  try {
    await deletePrompt(row.promptCode)
    ElMessage.success('已删除')
    if (selectedCode.value === row.promptCode) clearEditor()
    await loadList()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

/* ============ AI 生成 ============ */
function openGenerate() {
  genPrompt.value = ''
  genMeta.value = null
  genRequirement.value = ''
  genVisible.value = true
}

async function onGenerate() {
  genLoading.value = true
  try {
    const r = await generatePromptDraft({
      promptType: promptType.value,
      standardCode: standardCode.value || null,
      extraRequirement: genRequirement.value || null
    })
    genPrompt.value = r.prompt || ''
    genMeta.value = {
      durationMs: r.durationMs,
      promptTokens: r.promptTokens,
      completionTokens: r.completionTokens
    }
    if (!genPrompt.value) ElMessage.warning('生成结果为空，可补充「额外要求」后重试')
  } catch (e: any) {
    ElMessage.error(e?.message || 'AI 生成失败')
  } finally {
    genLoading.value = false
  }
}

function applyGenerated() {
  if (!genPrompt.value) return
  form.template = genPrompt.value
  dirty.value = true
  genVisible.value = false
  ElMessage.success('已填入编辑器，确认或修改后点「保存」')
}

async function copyGenerated() {
  try {
    await navigator.clipboard.writeText(genPrompt.value)
    ElMessage.success('已复制')
  } catch {
    ElMessage.error('复制失败')
  }
}
</script>

<template>
  <YzhPageLayout page-title="提示词工作台" no-padding>
    <template #toolbar-left>
      <el-select
        v-model="standardCode"
        placeholder="平台级（不限标准）"
        clearable
        filterable
        style="width: 260px"
        @change="onStandardChange"
      >
        <el-option label="平台级（对所有标准生效）" value="" />
        <el-option
          v-for="s in standards"
          :key="s.code"
          :label="s.display"
          :value="s.code"
        />
      </el-select>

      <el-radio-group v-model="promptType" @change="onTypeChange">
        <el-radio-button v-for="t in TYPE_OPTIONS" :key="t.value" :value="t.value">
          {{ t.label }}
        </el-radio-button>
      </el-radio-group>

      <el-button :icon="Refresh" :loading="listLoading" @click="loadList">刷新</el-button>
    </template>

    <template #toolbar-right>
      <el-button :icon="MagicStick" @click="openGenerate">AI 生成草稿</el-button>
      <el-button :icon="Plus" @click="onNew">新建</el-button>
      <el-button type="primary" :loading="saving" :disabled="!dirty && !isNew" @click="onSave">
        保存
      </el-button>
    </template>

    <div class="workbench">
      <!-- ── 左：提示词列表 ───────────────────────────────── -->
      <aside class="col col-list">
        <div class="col-head">
          <span>提示词（{{ prompts.length }}）</span>
          <el-tag size="small" type="info" effect="plain">
            {{ standardCode ? currentStandard?.standardCode || '指定标准' : '平台级' }}
          </el-tag>
        </div>

        <div v-loading="listLoading" class="list-body">
          <div
            v-for="p in prompts"
            :key="p.promptCode"
            class="prompt-card"
            :class="{ active: selectedCode === p.promptCode }"
            @click="selectPrompt(p)"
          >
            <div class="card-top">
              <span class="card-name">{{ p.promptName || p.promptCode }}</span>
              <el-tag v-if="p.isActive" size="small" type="success" effect="dark">生效</el-tag>
              <el-tag v-else size="small" type="info" effect="plain">未生效</el-tag>
            </div>
            <div class="card-code">{{ p.promptCode }}</div>
            <div class="card-meta">
              <el-tag size="small" effect="plain" type="info">v{{ p.version }}</el-tag>
              <el-tag v-if="p.modelName" size="small" effect="plain">{{ p.modelName }}</el-tag>
              <el-tag v-else size="small" effect="plain" type="warning">跟随默认模型</el-tag>
              <el-tag v-if="!p.standardCode" size="small" effect="plain" type="info">平台级</el-tag>
            </div>
            <div class="card-actions" @click.stop>
              <el-button v-if="!p.isActive" link size="small" :icon="Check" @click="onActivate(p)">
                设为生效
              </el-button>
              <el-button link size="small" type="danger" @click="onDelete(p)">删除</el-button>
            </div>
          </div>

          <el-empty
            v-if="!listLoading && !prompts.length"
            description="该标准下还没有提示词，点右上角「AI 生成草稿」或「新建」"
            :image-size="70"
          />
        </div>
      </aside>

      <!-- ── 中：编辑器 ──────────────────────────────────── -->
      <section class="col col-editor">
        <div class="col-head">
          <span>
            编辑 · {{ typeLabel }}
            <el-tag v-if="isNew" size="small" type="warning" effect="plain">新建</el-tag>
            <el-tag v-else-if="dirty" size="small" type="warning" effect="plain">未保存</el-tag>
          </span>
        </div>

        <div class="editor-body">
          <div class="field-grid">
            <div class="field">
              <label>提示词编码 <span class="req">*</span></label>
              <el-input v-model="form.promptCode" placeholder="唯一编码，如 doc_group_iso9001" />
            </div>
            <div class="field">
              <label>名称</label>
              <el-input v-model="form.promptName" placeholder="界面展示名称" />
            </div>
            <div class="field">
              <label>模型</label>
              <el-select v-model="form.modelName" style="width: 100%">
                <el-option
                  v-for="m in MODEL_OPTIONS"
                  :key="m.value"
                  :label="m.label"
                  :value="m.value"
                />
              </el-select>
            </div>
            <div class="field">
              <label>最大输出 tokens</label>
              <el-input-number
                v-model="form.maxTokens"
                :min="256"
                :max="131072"
                :step="1024"
                controls-position="right"
                placeholder="留空 = 系统默认"
                style="width: 100%"
              />
            </div>
            <div class="field">
              <label>温度</label>
              <el-input-number
                v-model="form.temperature"
                :min="0"
                :max="2"
                :step="0.1"
                :precision="2"
                controls-position="right"
                placeholder="留空 = 系统默认"
                style="width: 100%"
              />
            </div>
            <div class="field field-wide">
              <label>说明</label>
              <el-input v-model="form.description" placeholder="这条提示词做什么、怎么判定" />
            </div>
          </div>

          <div class="field field-template">
            <label>
              提示词正文
              <span class="hint">
                占位符：
                <code v-if="promptType === PROMPT_TYPE.Group">{{ '{{file_list}}' }}</code>
                <code v-else>{{ '{{document_content}}' }}</code>
                —— 试跑时会被自动替换
              </span>
            </label>
            <el-input
              v-model="form.template"
              type="textarea"
              :rows="20"
              resize="none"
              class="template-input"
              placeholder="在这里编写提示词；也可点右上角「AI 生成草稿」自动生成后修改"
              @input="onEditorInput"
            />
          </div>

          <div class="editor-foot">
            <span class="hint">
              {{
                isNew
                  ? '新提示词保存后将自动置为生效'
                  : '保存后版本号 +1 并置为生效（同类型其他提示词自动失效）'
              }}
            </span>
            <el-button type="primary" :loading="saving" @click="onSave">保存</el-button>
          </div>
        </div>
      </section>

      <!-- ── 右：试跑 ────────────────────────────────────── -->
      <aside class="col col-test">
        <PromptTestPanel
          :prompt-type="promptType"
          :standard-code="standardCode || null"
          :template="form.template || null"
        />
      </aside>
    </div>

    <!-- ── AI 生成草稿对话框 ──────────────────────────────── -->
    <el-dialog v-model="genVisible" title="AI 生成提示词草稿" width="820px" top="6vh">
      <el-alert
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 12px"
        title="生成的是「提示词正文」草稿，不会自动保存 —— 确认或修改后点「填入编辑器」再保存。"
      />

      <div class="gen-row">
        <span class="gen-label">生成类型</span>
        <el-tag type="primary" effect="plain">{{ typeLabel }}</el-tag>
        <span class="gen-label">适用标准</span>
        <el-tag type="info" effect="plain">
          {{ standardCode ? currentStandard?.display || standardCode : '平台级（未指定标准）' }}
        </el-tag>
      </div>

      <div class="gen-row">
        <span class="gen-label">额外要求</span>
        <el-input
          v-model="genRequirement"
          type="textarea"
          :rows="2"
          placeholder="可选。例如「我们是建筑工程企业，资料里会有大量施工记录和检验批表格，请把这类单独归为一类」"
        />
      </div>

      <div class="gen-actions">
        <el-button type="primary" :icon="MagicStick" :loading="genLoading" @click="onGenerate">
          {{ genPrompt ? '重新生成' : '开始生成' }}
        </el-button>
        <span v-if="genMeta" class="hint">
          耗时 {{ genMeta.durationMs }} ms
          <template v-if="genMeta.promptTokens != null">
            · in {{ genMeta.promptTokens }} / out {{ genMeta.completionTokens ?? 0 }} tokens
          </template>
        </span>
      </div>

      <el-input
        v-model="genPrompt"
        type="textarea"
        :rows="18"
        resize="none"
        class="gen-output"
        placeholder="生成结果会出现在这里，可直接编辑"
      />

      <template #footer>
        <el-button @click="genVisible = false">取消</el-button>
        <el-button :disabled="!genPrompt" @click="copyGenerated">复制</el-button>
        <el-button type="primary" :disabled="!genPrompt" @click="applyGenerated">
          填入编辑器
        </el-button>
      </template>
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
.workbench {
  display: flex;
  flex: 1;
  min-height: 0;
  height: 100%;
  background: #fff;
}

.col {
  display: flex;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
}

.col-head {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  height: 40px;
  padding: 0 14px;
  font-size: 13px;
  font-weight: 600;
  color: #303133;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: #fafafa;
}

/* 左栏 */
.col-list {
  width: 300px;
  flex-shrink: 0;
  border-right: 1px solid var(--el-border-color-lighter);
}
.list-body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 10px;
}

.prompt-card {
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
  padding: 10px 12px;
  margin-bottom: 8px;
  cursor: pointer;
  transition: all 0.15s;
  background: #fff;
}
.prompt-card:hover {
  border-color: var(--el-color-primary-light-5);
  box-shadow: 0 1px 6px rgba(64, 158, 255, 0.12);
}
.prompt-card.active {
  border-color: var(--el-color-primary);
  background: var(--el-color-primary-light-9);
}

.card-top {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-bottom: 4px;
}
.card-name {
  flex: 1;
  min-width: 0;
  font-size: 13px;
  font-weight: 600;
  color: #303133;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.card-code {
  font-family: 'Courier New', monospace;
  font-size: 12px;
  color: #909399;
  margin-bottom: 6px;
  word-break: break-all;
}
.card-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}
.card-actions {
  display: flex;
  justify-content: flex-end;
  gap: 4px;
  margin-top: 6px;
  padding-top: 6px;
  border-top: 1px dashed var(--el-border-color-lighter);
}

/* 中栏 */
.col-editor {
  flex: 1;
  min-width: 0;
}
.editor-body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: 14px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.field-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 10px 14px;
}
.field {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}
.field-wide {
  grid-column: 1 / -1;
}
.field label {
  font-size: 12px;
  color: #606266;
  display: flex;
  align-items: center;
  gap: 6px;
}
.req {
  color: var(--el-color-danger);
}
.field .hint {
  font-weight: 400;
  color: #909399;
}
.field .hint code {
  background: #f5f7fa;
  border: 1px solid #e4e7ed;
  border-radius: 3px;
  padding: 0 4px;
  font-family: 'Courier New', monospace;
}

.field-template {
  flex: 1;
  min-height: 0;
}
.template-input :deep(.el-textarea__inner) {
  background: #1e1e1e;
  color: #d4d4d4;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.65;
  border-radius: 4px;
}

.editor-foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding-top: 10px;
  border-top: 1px solid var(--el-border-color-lighter);
}
.editor-foot .hint {
  font-size: 12px;
  color: #909399;
}

/* 右栏 */
.col-test {
  width: 440px;
  flex-shrink: 0;
  border-left: 1px solid var(--el-border-color-lighter);
  background: #fafafa;
}

/* 生成对话框 */
.gen-row {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
}
.gen-label {
  font-size: 12px;
  color: #606266;
  flex-shrink: 0;
}
.gen-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  margin: 4px 0 10px;
}
.gen-actions .hint {
  font-size: 12px;
  color: #909399;
}
.gen-output :deep(.el-textarea__inner) {
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.65;
}
</style>
