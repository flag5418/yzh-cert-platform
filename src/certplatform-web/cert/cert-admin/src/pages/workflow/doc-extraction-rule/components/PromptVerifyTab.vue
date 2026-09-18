<script setup lang="ts">
import { ref } from 'vue'
import { CopyDocument, Refresh } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import type { FieldDefDto, TableDefDto, ExtractionData } from '@share/api/workflow/doc-extraction-rule'

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
}>()

const emit = defineEmits<{
  'update:prompt': [val: string]
  generate: []
  verify: []
}>()

const verifyData = ref<ExtractionData | null>(null)

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

function getTableColumns(tableData: Record<string, unknown>[]): string[] {
  if (!tableData?.length) return []
  return Object.keys(tableData[0])
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
        :rows="18"
        placeholder="点击生成 Prompt 按钮自动生成，或手动输入"
        class="prompt-editor"
        @input="onInput"
      />
      <div class="prompt-toolbar">
        <el-button link size="small" :icon="CopyDocument" @click="copyPrompt">复制</el-button>
        <el-button link size="small" @click="emit('generate')" :loading="generating">重新生成</el-button>
        <span class="prompt-hint">您可以直接编辑生成的 Prompt</span>
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

      <div v-if="isValid && verifyData?.fields" class="extracted-fields">
        <h5>提取字段</h5>
        <el-descriptions :column="1" border size="small">
          <el-descriptions-item v-for="(val, key) in verifyData.fields" :key="String(key)" :label="String(key)">
            {{ val }}
          </el-descriptions-item>
        </el-descriptions>
      </div>

      <div v-if="isValid && verifyData?.tables" class="extracted-tables">
        <h5>提取表格</h5>
        <div v-for="(rows, tableKey) in verifyData.tables" :key="String(tableKey)" class="table-block">
          <span class="table-label">{{ tableKey }}</span>
          <el-table :data="rows" size="small" border style="width: 100%">
            <el-table-column
              v-for="col in getTableColumns(rows)"
              :key="col"
              :prop="col"
              :label="col"
              min-width="100"
              show-overflow-tooltip
            />
          </el-table>
        </div>
      </div>
    </div>

    <div class="verify-actions">
      <el-button
        type="success"
        size="small"
        :loading="verifying"
        :disabled="!prompt"
        @click="emit('verify')"
      >
        验证 Prompt
      </el-button>
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
.verify-section { margin-bottom: 16px; }
.extracted-fields, .extracted-tables { margin-bottom: 12px; }
.extracted-fields h5, .extracted-tables h5 { margin: 0 0 8px 0; font-weight: 500; }
.table-block { margin-bottom: 12px; }
.table-label { display: block; font-weight: 500; margin-bottom: 4px; }
.verify-actions { padding-top: 12px; border-top: 1px solid #ebeef5; }
</style>
