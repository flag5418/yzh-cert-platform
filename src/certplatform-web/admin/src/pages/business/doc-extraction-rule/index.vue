<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { YzhPageLayout } from '@yzh-core/components/layout'
import { CertDirectoryTree } from '@share/components'
import {
  getDocRuleList,
  createDocRule,
  analyzeDocWithAI,
  saveDocRule,
  type DocExtractionRule,
  type DocFieldDef,
  type DocTableDef
} from '@share/api/doc-extraction'

const selectedFile = ref<any>(null)
const activeTab = ref('analysis')
const rule = ref<DocExtractionRule | null>(null)
const fields = ref<DocFieldDef[]>([])
const tables = ref<DocTableDef[]>([])
const saving = ref(false)
const analyzing = ref(false)

// AI 分析结果
const analysisResult = ref<{ fields: any[]; tables: any[] } | null>(null)

async function handleNodeClick(data: any) {
  selectedFile.value = data
  // 加载该文件的提取规则
  await loadRule(data.code)
}

async function loadRule(fileCode: string) {
  try {
    rule.value = await getDocRuleList({ fileCode })
    if (rule.value) {
      fields.value = rule.value.fields || []
      tables.value = rule.value.tables || []
    } else {
      fields.value = []
      tables.value = []
    }
  } catch (e: any) {
    ElMessage.error('加载规则失败')
  }
}

async function handleAnalyze() {
  if (!selectedFile.value?.code) {
    ElMessage.warning('请先选择文件')
    return
  }
  analyzing.value = true
  try {
    analysisResult.value = await analyzeDocWithAI(selectedFile.value.code, selectedFile.value.type)
    if (analysisResult.value) {
      fields.value = analysisResult.value.fields || []
      tables.value = analysisResult.value.tables || []
      ElMessage.success('AI 分析完成')
    }
  } catch (e: any) {
    ElMessage.error(e?.message || 'AI 分析失败')
  } finally {
    analyzing.value = false
  }
}

async function handleSave() {
  if (!selectedFile.value?.code) return
  saving.value = true
  try {
    await saveDocRule({
      fileCode: selectedFile.value.code,
      fields: fields.value,
      tables: tables.value
    })
    ElMessage.success('保存成功')
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

function addField() {
  fields.value.push({
    fieldCode: '',
    fieldName: '',
    dataType: 'string',
    isRequired: false,
    sortOrder: fields.value.length
  })
}

function removeField(index: number) {
  fields.value.splice(index, 1)
}

function addTable() {
  tables.value.push({
    tableCode: '',
    tableName: '',
    fields: []
  })
}

function removeTable(index: number) {
  tables.value.splice(index, 1)
}

onMounted(() => {
  // 目录树将在后续后端就绪后填充数据
})
</script>

<template>
  <YzhPageLayout title="文档提取规则">
    <template #default>
      <el-row :gutter="16">
        <!-- 左侧文件树 -->
        <el-col :span="5">
          <el-card shadow="never">
            <template #header><span>文件目录</span></template>
            <div style="min-height: 500px; overflow-y: auto">
              <CertDirectoryTree @select="handleNodeClick" />
            </div>
          </el-card>
        </el-col>

        <!-- 右侧内容区 -->
        <el-col :span="19">
          <el-card shadow="never">
            <template #header>
              <div class="card-header">
                <span v-if="selectedFile">{{ selectedFile.name }}</span>
                <span v-else>请选择文件</span>
                <el-tabs v-model="activeTab" style="margin-bottom: 0">
                  <el-tab-pane label="AI 分析" name="analysis" />
                  <el-tab-pane label="字段定义" name="fields" />
                  <el-tab-pane label="表格定义" name="tables" />
                  <el-tab-pane label="提示词" name="prompt" />
                </el-tabs>
              </div>
            </template>

            <!-- AI 分析 Tab -->
            <div v-if="activeTab === 'analysis'" class="tab-content">
              <el-alert type="info" :closable="false" style="margin-bottom: 16px">
                <template #title>点击"AI 分析"按钮，系统将自动识别文档中的字段和表格</template>
              </el-alert>
              <div style="text-align: center; padding: 40px 0">
                <el-button type="primary" size="large" :loading="analyzing" @click="handleAnalyze">
                  <el-icon><MagicStick /></el-icon> AI 自动分析
                </el-button>
              </div>
              <div v-if="analysisResult" class="analysis-result">
                <el-divider>分析结果</el-divider>
                <el-row :gutter="16">
                  <el-col :span="12">
                    <h4>识别字段（{{ analysisResult.fields?.length || 0 }}）</h4>
                    <el-table :data="analysisResult.fields" size="small" border>
                      <el-table-column prop="fieldCode" label="编码" width="150" />
                      <el-table-column prop="fieldName" label="名称" width="150" />
                      <el-table-column prop="dataType" label="类型" width="100" />
                      <el-table-column label="必填" width="60" align="center">
                        <template #default="{ row }">
                          <el-tag :type="row.isRequired ? 'danger' : 'info'" size="small">{{ row.isRequired ? '是' : '否' }}</el-tag>
                        </template>
                      </el-table-column>
                    </el-table>
                  </el-col>
                  <el-col :span="12">
                    <h4>识别表格（{{ analysisResult.tables?.length || 0 }}）</h4>
                    <el-table :data="analysisResult.tables" size="small" border>
                      <el-table-column prop="tableCode" label="编码" width="150" />
                      <el-table-column prop="tableName" label="名称" width="150" />
                      <el-table-column prop="description" label="描述" min-width="150" show-overflow-tooltip />
                    </el-table>
                  </el-col>
                </el-row>
              </div>
            </div>

            <!-- 字段定义 Tab -->
            <div v-else-if="activeTab === 'fields'" class="tab-content">
              <div style="margin-bottom: 16px">
                <el-button type="primary" size="small" @click="addField">
                  <el-icon><Plus /></el-icon> 添加字段
                </el-button>
              </div>
              <el-table :data="fields" size="small" border>
                <el-table-column label="排序" width="60" align="center">
                  <template #default="{ $index }">{{ $index + 1 }}</template>
                </el-table-column>
                <el-table-column label="字段编码" width="150">
                  <template #default="{ row, $index }">
                    <el-input v-model="row.fieldCode" size="small" placeholder="如 companyName" />
                  </template>
                </el-table-column>
                <el-table-column label="字段名称" width="150">
                  <template #default="{ row, $index }">
                    <el-input v-model="row.fieldName" size="small" placeholder="如 企业名称" />
                  </template>
                </el-table-column>
                <el-table-column label="类型" width="100">
                  <template #default="{ row, $index }">
                    <el-select v-model="row.dataType" size="small" style="width: 100%">
                      <el-option label="string" value="string" />
                      <el-option label="number" value="number" />
                      <el-option label="date" value="date" />
                      <el-option label="boolean" value="boolean" />
                      <el-option label="money" value="money" />
                      <el-option label="percent" value="percent" />
                    </el-select>
                  </template>
                </el-table-column>
                <el-table-column label="必填" width="60" align="center">
                  <template #default="{ row, $index }">
                    <el-switch v-model="row.isRequired" size="small" />
                  </template>
                </el-table-column>
                <el-table-column label="操作" width="60" align="center">
                  <template #default="{ $index }">
                    <el-button link type="danger" size="small" @click="removeField($index)">删除</el-button>
                  </template>
                </el-table-column>
              </el-table>
            </div>

            <!-- 表格定义 Tab -->
            <div v-else-if="activeTab === 'tables'" class="tab-content">
              <div style="margin-bottom: 16px">
                <el-button type="primary" size="small" @click="addTable">
                  <el-icon><Plus /></el-icon> 添加表格
                </el-button>
              </div>
              <el-collapse v-model="activeTable">
                <el-collapse-item v-for="(table, idx) in tables" :key="idx" :title="table.tableName || '未命名表格'" :name="idx">
                  <template #title>
                    <span class="table-title">
                      <el-icon><Document /></el-icon>
                      {{ table.tableName || `表格 ${idx + 1}` }}
                      <span class="table-code">{{ table.tableCode }}</span>
                    </span>
                  </template>
                  <el-form :model="table" label-width="80px" size="small">
                    <el-form-item label="表格编码">
                      <el-input v-model="table.tableCode" placeholder="如 shareholderInfo" />
                    </el-form-item>
                    <el-form-item label="表格名称">
                      <el-input v-model="table.tableName" placeholder="如 股东信息表" />
                    </el-form-item>
                    <el-form-item label="描述">
                      <el-input v-model="table.description" type="textarea" :rows="2" />
                    </el-form-item>
                    <el-divider>表格字段</el-divider>
                    <el-table :data="table.fields" size="small" border style="width: 100%">
                      <el-table-column label="字段编码" width="150">
                        <template #default="{ row }">
                          <el-input v-model="row.fieldCode" size="small" />
                        </template>
                      </el-table-column>
                      <el-table-column label="字段名称" width="150">
                        <template #default="{ row }">
                          <el-input v-model="row.fieldName" size="small" />
                        </template>
                      </el-table-column>
                      <el-table-column label="类型" width="100">
                        <template #default="{ row }">
                          <el-select v-model="row.dataType" size="small" style="width: 100%">
                            <el-option label="string" value="string" />
                            <el-option label="number" value="number" />
                            <el-option label="date" value="date" />
                            <el-option label="boolean" value="boolean" />
                            <el-option label="money" value="money" />
                            <el-option label="percent" value="percent" />
                          </el-select>
                        </template>
                      </el-table-column>
                      <el-table-column label="必填" width="60" align="center">
                        <template #default="{ row }">
                          <el-switch v-model="row.isRequired" size="small" />
                        </template>
                      </el-table-column>
                      <el-table-column label="操作" width="60" align="center">
                        <template #default="{ $index }">
                          <el-button link type="danger" size="small" @click="table.fields.splice($index, 1)">删除</el-button>
                        </template>
                      </el-table-column>
                    </el-table>
                    <el-button link type="primary" size="small" @click="table.fields.push({ fieldCode: '', fieldName: '', dataType: 'string', isRequired: false })">
                      + 添加字段
                    </el-button>
                  </el-form>
                </el-collapse>
              </el-collapse>
            </div>

            <!-- 提示词 Tab -->
            <div v-else-if="activeTab === 'prompt'" class="tab-content">
              <div class="prompt-actions" style="margin-bottom: 16px">
                <el-button type="primary" size="small" @click="handleSave" :loading="saving">保存规则</el-button>
              </div>
              <el-alert type="warning" :closable="false" style="margin-bottom: 16px">
                <template #title>提示词将根据字段和表格定义自动生成，请确保字段定义完整后再保存</template>
              </el-alert>
              <el-form label-width="80px">
                <el-form-item label="字段数">
                  <span>{{ fields.length }} 个</span>
                </el-form-item>
                <el-form-item label="表格数">
                  <span>{{ tables.length }} 个</span>
                </el-form-item>
                <el-form-item label="提示词">
                  <div class="prompt-preview">
                    <pre class="prompt-text">你是一位专业的文档信息提取助手。

【任务】
从以下文档中提取指定字段和表格信息。

【需要提取的字段】
{{ fields.length > 0 ? fields.map(f => `${f.fieldCode}（${f.dataType}）${f.isRequired ? '（必须）' : ''}: ${f.fieldName}`).join('\n') : '（请先在"字段定义"中配置字段）' }}

【需要提取的表格】
{{ tables.length > 0 ? tables.map(t => `表格: ${t.tableCode}（${t.tableName}）\n  字段: ${t.fields.map(f => f.fieldCode).join(', ')}`).join('\n') : '（请先在"表格定义"中配置表格）' }}

【输出格式】
返回 JSON 格式</pre>
                  </div>
                </el-form-item>
              </el-form>
            </div>
          </el-card>
        </el-col>
      </el-row>
    </template>
  </YzhPageLayout>
</template>

<style scoped>
.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.tab-content {
  min-height: 400px;
  padding: 16px;
}
.analysis-result {
  margin-top: 16px;
}
.table-title {
  display: flex;
  align-items: center;
  gap: 8px;
}
.table-code {
  font-size: 12px;
  color: #909399;
  margin-left: 8px;
}
.prompt-preview {
  background: #f5f7fa;
  border: 1px solid #e4e7ed;
  border-radius: 4px;
  padding: 12px;
}
.prompt-text {
  white-space: pre-wrap;
  word-break: break-all;
  font-family: 'Courier New', monospace;
  font-size: 13px;
  line-height: 1.6;
  margin: 0;
}
</style>
