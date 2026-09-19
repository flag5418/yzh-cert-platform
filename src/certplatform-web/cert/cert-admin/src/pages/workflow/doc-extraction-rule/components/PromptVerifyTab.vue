<script setup lang="ts">
import { computed } from 'vue'
import { CopyDocument, Refresh, Warning } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import type { FieldDefDto, TableDefDto, ExtractionData } from '@share/api/workflow/doc-extraction-rule'
import { buildExtractionView } from '@share/utils/extractionView'

const props = defineProps<{
  fields: FieldDefDto[]
  tables: TableDefDto[]
  prompt: string
  /**
   * 验证结论（三态）：null = 尚未验证、true = 通过、false = 失败。
   * 模板里用 `isValid !== null` 判定是否展示验证结果区，因此必须允许 null；
   * 声明为 `boolean` 会触发 Vue 运行时 prop 类型告警（Expected Boolean, got Null）。
   */
  isValid: boolean | null
  verifying: boolean
  generating: boolean
  /** 验证返回的原始提取结果（null = 无结果） */
  extractionData?: ExtractionData | null
  /** 是否使用了「固定提示词」（提示词留空时后端自动按定义生成） */
  usedFixedPrompt?: boolean
}>()

const emit = defineEmits<{
  'update:prompt': [val: string]
  generate: []
  verify: []
}>()

/**
 * 展示以规则定义（中文）为准：
 *   · 字段：中文名 + 编码 + 提取值（未提取到显式标注）
 *   · 表格：中文表名 + 中文列头（行键已归一为列编码）
 *   · AI 多返回的条目单列，避免与定义混在一起
 */
const view = computed(() => buildExtractionView(props.fields, props.tables, props.extractionData || null))

function onInput(val: string) {
  emit('update:prompt', val)
}

async function copyPrompt() {
  try {
    await navigator.clipboard.writeText(props.prompt)
    ElMessage.success('已复制')
  } catch {
    ElMessage.error('复制失败')
  }
}

function cellText(v: unknown): string {
  if (v === null || v === undefined) return ''
  if (typeof v === 'object') return JSON.stringify(v)
  return String(v)
}
</script>

<template>
  <div class="prompt-verify-tab">
    <div class="section-header">
      <h4>Prompt 生成与验证</h4>
      <el-button type="primary" size="small" :loading="generating" @click="emit('generate')">
        <el-icon><Refresh /></el-icon> 生成 Prompt
      </el-button>
    </div>

    <div class="prompt-editor-section">
      <el-input
        :model-value="prompt"
        type="textarea"
        :rows="14"
        placeholder="留空即使用「固定提示词」：由本文件已配置的字段/表格清单自动生成（字段与表格结构性分离，推荐）；也可点击生成 Prompt 或手动输入"
        class="prompt-editor"
        @input="onInput"
      />
      <div class="prompt-toolbar">
        <el-button link size="small" :icon="CopyDocument" @click="copyPrompt">复制</el-button>
        <el-button link size="small" @click="emit('generate')" :loading="generating">重新生成</el-button>
        <span class="prompt-hint">
          {{ prompt ? '您可以直接编辑生成的 Prompt' : '当前为空：验证时使用固定提示词' }}
        </span>
      </div>
      <div class="defs-brief">
        已配置：
        <el-tag size="small" type="info" effect="plain">字段 {{ fields.length }}</el-tag>
        <el-tag size="small" type="info" effect="plain">表格 {{ tables.length }}</el-tag>
        <span class="defs-tip">（固定提示词与结果展示均以此定义为准）</span>
      </div>
    </div>

    <div v-if="isValid !== null" class="verify-section">
      <el-alert
        :type="isValid ? 'success' : 'warning'"
        :title="isValid ? '验证通过' : '验证失败'"
        :closable="false"
        show-icon
        style="margin-bottom: 12px"
      />

      <!-- 提取字段：按定义（中文）逐项展示 -->
      <div class="result-block">
        <h5>
          提取字段
          <span class="count">{{ view.extractedFieldCount }} / {{ view.fields.length }} 已提取</span>
        </h5>
        <el-table v-if="view.fields.length" :data="view.fields" size="small" border style="width: 100%">
          <el-table-column label="字段名称" min-width="120">
            <template #default="{ row }">{{ row.name }}</template>
          </el-table-column>
          <el-table-column label="编码" min-width="120">
            <template #default="{ row }"><code>{{ row.code }}</code></template>
          </el-table-column>
          <el-table-column label="提取值" min-width="160">
            <template #default="{ row }">
              <span v-if="row.extracted">{{ cellText(row.value) }}</span>
              <span v-else class="missing">未提取到</span>
            </template>
          </el-table-column>
        </el-table>
        <el-empty v-else description="该文件尚未配置字段定义" :image-size="60" />
      </div>

      <!-- 提取表格：中文表名 + 中文列头 -->
      <div class="result-block">
        <h5>
          提取表格
          <span class="count">{{ view.extractedTableCount }} / {{ view.tables.length }} 已提取，共 {{ view.extractedRowCount }} 行</span>
        </h5>
        <el-empty v-if="!view.tables.length" description="该文件尚未配置表格定义" :image-size="60" />
        <div v-for="t in view.tables" :key="t.code" class="table-block">
          <div class="table-head">
            <span class="table-label">{{ t.name }}</span>
            <code class="table-code">{{ t.code }}</code>
            <el-tag v-if="t.extracted" size="small" type="success" effect="plain">{{ t.rows.length }} 行</el-tag>
            <el-tag v-else size="small" type="info" effect="plain">未提取到数据</el-tag>
          </div>
          <el-table v-if="t.extracted" :data="t.rows" size="small" border max-height="300" style="width: 100%">
            <el-table-column
              v-for="col in t.columns"
              :key="col.key"
              :prop="col.key"
              :label="col.label"
              min-width="110"
              show-overflow-tooltip
            />
          </el-table>
        </div>
      </div>

      <!-- AI 多返回、定义中没有的条目 -->
      <div
        v-if="view.extraFields.length || view.extraTables.length"
        class="result-block extra-block"
      >
        <h5>
          <el-icon><Warning /></el-icon>
          AI 额外返回（不在规则定义内，不会保存）
        </h5>
        <div v-if="view.extraFields.length" class="extra-line">
          <span class="extra-label">字段：</span>
          <el-tag v-for="e in view.extraFields" :key="e.key" size="small" type="warning" effect="plain" class="extra-tag">
            {{ e.key }}
          </el-tag>
        </div>
        <div v-if="view.extraTables.length" class="extra-line">
          <span class="extra-label">表格：</span>
          <el-tag v-for="e in view.extraTables" :key="e.key" size="small" type="warning" effect="plain" class="extra-tag">
            {{ e.key }}
          </el-tag>
        </div>
        <div class="extra-tip">若这些内容本应是正式字段/表格，请回到「自动分析」页签补充定义后重新验证。</div>
      </div>
    </div>

    <div class="verify-actions">
      <el-button type="success" size="small" :loading="verifying" @click="emit('verify')">
        验证 Prompt
      </el-button>
      <span class="verify-hint">{{ prompt ? '按上方提示词提取' : '按固定提示词（字段/表格定义）提取' }}</span>
    </div>
  </div>
</template>

<style scoped>
.prompt-verify-tab { padding: 0; }
.section-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
.prompt-editor-section { margin-bottom: 16px; }
.prompt-editor :deep(.el-textarea__inner) {
  background: #1e1e1e;
  color: #d4d4d4;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  border-radius: 4px;
}
.prompt-toolbar { display: flex; align-items: center; gap: 8px; margin-top: 8px; }
.prompt-hint { color: #909399; font-size: 12px; margin-left: auto; }
.defs-brief { margin-top: 8px; font-size: 12px; color: #606266; display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
.defs-tip { color: #909399; }
.verify-section { margin-bottom: 16px; }
.result-block { margin-bottom: 16px; }
.result-block h5 {
  margin: 0 0 8px 0;
  font-weight: 600;
  font-size: 13px;
  color: #303133;
  display: flex;
  align-items: center;
  gap: 8px;
}
.count { font-weight: 400; font-size: 12px; color: #909399; }
.missing { color: #c0c4cc; font-style: italic; }
.table-block { margin-bottom: 14px; }
.table-head { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; }
.table-label { font-weight: 600; color: #303133; }
.table-code { color: #909399; font-size: 12px; }
.extra-block { background: #fdf6ec; border: 1px solid #f5dab1; border-radius: 4px; padding: 10px 12px; }
.extra-block h5 { color: #b88230; }
.extra-line { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; margin-bottom: 6px; }
.extra-label { font-size: 12px; color: #909399; }
.extra-tag { margin-right: 4px; }
.extra-tip { font-size: 12px; color: #b88230; }
.verify-actions { padding-top: 12px; border-top: 1px solid #ebeef5; display: flex; align-items: center; gap: 10px; }
.verify-hint { font-size: 12px; color: #909399; }
</style>
