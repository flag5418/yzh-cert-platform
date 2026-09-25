<script setup lang="ts">
/**
 * 报告章节定义 — 左侧组织树 + 右侧模板表单 + 章节表格
 * 迁移自旧项目 ReportDefinition.vue，适配 YZH.Core 新架构
 */
import { ref, reactive, computed, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { Plus, Upload } from '@element-plus/icons-vue'
import { YzhPageLayout, YzhForm, type YzhFormField } from '@yzh-core'
import { expectOk } from '@yzh-core/utils/apiResponse'
import { CertBizTree } from '@share/components'
import { useFileTree, type TreeNode } from '@share/composables/useFileTree'
import {
  getTemplateByContext,
  saveTemplate,
  uploadTemplateFile,
  deleteTemplate,
  getSectionList,
  saveSection,
  deleteSection,
} from '@share/api/workflow/report-rule'
import type { ReportTemplate, ReportSection } from '@share/types/cert'

// ── 树 ──
const { loadTree } = useFileTree()

// ── 右侧面板状态 ──
const selectedPhase = ref<TreeNode | null>(null)
const currentTemplate = ref<ReportTemplate | null>(null)
const templateLoading = ref(false)
const sectionLoading = ref(false)
const sectionData = ref<ReportSection[]>([])
const saving = ref(false)

// ── 模板表单（PascalCase，与后端 JSON 序列化一致）──
const templateForm = reactive({
  TemplateName: '',
  Remark: '',
  IsDefault: false,
})

// ── 章节编辑 ──
const sectionDialogVisible = ref(false)
const sectionForm = reactive<Partial<ReportSection>>({
  SectionName: '',
  SectionNameEn: '',
  ClauseCode: '',
  SortOrder: 0,
  IsActive: 1,
  Remark: '',
})
const sectionFormMode = ref<'add' | 'edit'>('add')

// ── 章节表单字段定义 ──
const sectionFormFields: YzhFormField[] = [
  { prop: 'SectionName', label: '章节名称', type: 'text', required: true, span: 24, placeholder: '如：审核发现' },
  { prop: 'SectionNameEn', label: '英文名称', type: 'text', span: 24, placeholder: '如：Audit Findings' },
  { prop: 'ClauseCode', label: '条款编号', type: 'text', span: 24, placeholder: '如：ISO9001:2015 §8.2' },
  { prop: 'SortOrder', label: '排序号', type: 'number', span: 24 },
  { prop: 'IsActive', label: '是否启用', type: 'switch', span: 24 },
  { prop: 'Remark', label: '备注', type: 'textarea', span: 24 },
]

// ── 计算属性 ──
const hasPhaseSelected = computed(() => !!selectedPhase.value)
const hasTemplate = computed(() => !!currentTemplate.value)
const canEdit = computed(() => hasPhaseSelected.value)

// ── 树节点点击 ──
async function handleNodeClick(node: TreeNode) {
  if (node.Type !== 'stage') {
    // 机构/标准节点：提示用户在左侧选择叶子阶段
    ElMessage.info('请在左侧选择阶段（叶子节点）以查看报告模板和章节')
    return
  }

  selectedPhase.value = node
  currentTemplate.value = null
  sectionData.value = []

  await loadTemplate()
}

// ── 加载模板 ──
async function loadTemplate() {
  const phase = selectedPhase.value
  if (!phase || !phase.OrgCode || !phase.StdCode || !phase.PhaseCode) return

  templateLoading.value = true
  try {
    const res = await getTemplateByContext({
      orgCode: phase.OrgCode,
      standardCode: phase.StdCode,
      phaseCode: phase.PhaseDefinitionCode || phase.PhaseCode,
    })
    expectOk(res as any, '加载报告模板失败')
    const template = (res as any)?.data ?? res
    // 准则 A：存在性看业务键 Code（禁止 Id 判定）
    if (template && template.Code) {
      currentTemplate.value = template
      Object.assign(templateForm, {
        TemplateName: template.TemplateName || '',
        Remark: template.Remark || '',
        IsDefault: template.IsDefault || false,
      })
      await loadSections()
    } else {
      currentTemplate.value = null
      Object.assign(templateForm, { TemplateName: '', Remark: '', IsDefault: false })
    }
  } catch (e: any) {
    ElMessage.error(e?.err || e?.message || '加载报告模板失败')
    currentTemplate.value = null
  } finally {
    templateLoading.value = false
  }
}

// ── 保存模板 ──
async function handleSaveTemplate() {
  const phase = selectedPhase.value
  if (!phase) return ElMessage.warning('请先选择阶段')
  if (!templateForm.TemplateName) return ElMessage.warning('请输入模板名称')

  saving.value = true
  try {
    const payload: Record<string, any> = {
      TemplateName: templateForm.TemplateName,
      Remark: templateForm.Remark,
      IsDefault: templateForm.IsDefault,
      OrgCode: phase.OrgCode,
      StandardCode: phase.StdCode,
      PhaseCode: phase.PhaseDefinitionCode || phase.PhaseCode,
    }
    if (currentTemplate.value?.Code) {
      payload.Code = currentTemplate.value.Code
    }
    const res = await saveTemplate(payload)
    expectOk(res as any, '保存模板失败')
    const data = (res as any)?.data ?? res
    if (data?.Code) currentTemplate.value = data
    ElMessage.success('保存成功')
    await loadSections()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

// ── 上传文件 ──
async function handleUploadFile(options: any) {
  const phase = selectedPhase.value
  if (!phase) return ElMessage.warning('请先选择阶段')

  try {
    const res = await uploadTemplateFile(options.file, {
      orgCode: phase.OrgCode!,
      standardCode: phase.StdCode!,
      phaseCode: phase.PhaseDefinitionCode || phase.PhaseCode!,
    })
    expectOk(res as any, '上传模板文件失败')
    const data = (res as any)?.data ?? res
    ElMessage.success(`上传成功：${data?.fileName}`)
    options.onSuccess?.(data)
  } catch (e: any) {
    ElMessage.error(e?.message || '上传失败')
    options.onError?.(e)
  }
}

// ── 删除模板 ──
async function handleDeleteTemplate() {
  if (!currentTemplate.value) return
  try {
    await ElMessageBox.confirm(
      `确定删除模板「${currentTemplate.value.TemplateName}」及其所有章节？`,
      '确认删除',
      { type: 'warning' }
    )
  } catch { return }

  try {
    expectOk((await deleteTemplate(currentTemplate.value.Code)) as any, '删除模板失败')
  } catch (e: any) {
    ElMessage.error(e?.err || e?.message || '删除模板失败')
    return
  }
  ElMessage.success('删除成功')
  currentTemplate.value = null
  sectionData.value = []
}

// ── 加载章节 ──
async function loadSections() {
  if (!currentTemplate.value?.Code) return
  sectionLoading.value = true
  try {
    const res = await getSectionList(currentTemplate.value.Code)
    const data = (res as any)?.data ?? res
    sectionData.value = Array.isArray(data) ? data : []
  } catch (e: any) {
    // F-10：加载失败保留上次数据；F-3：谁 catch 谁弹
    ElMessage.error(e?.err || e?.message || '加载章节失败')
  } finally {
    sectionLoading.value = false
  }
}

// ── 章节操作 ──
function openAddSection() {
  sectionFormMode.value = 'add'
  Object.assign(sectionForm, {
    Code: undefined,
    SectionName: '',
    SectionNameEn: '',
    ClauseCode: '',
    SortOrder: sectionData.value.length + 1,
    IsActive: 1,
    Remark: '',
  })
  sectionDialogVisible.value = true
}

function openEditSection(row: ReportSection) {
  sectionFormMode.value = 'edit'
  Object.assign(sectionForm, { ...row })
  sectionDialogVisible.value = true
}

async function handleSaveSection() {
  if (!sectionForm.SectionName) return ElMessage.warning('请输入章节名称')
  if (!currentTemplate.value?.Code) return ElMessage.warning('请先保存模板')

  saving.value = true
  try {
    const payload: Partial<ReportSection> = {
      ...sectionForm,
      ReportCode: currentTemplate.value.Code,
    }
    expectOk((await saveSection(payload)) as any, '保存章节失败')
    ElMessage.success('保存成功')
    sectionDialogVisible.value = false
    await loadSections()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    saving.value = false
  }
}

async function handleDeleteSection(row: ReportSection) {
  try {
    await ElMessageBox.confirm(`确定删除章节「${row.SectionName}」？`, '确认删除', { type: 'warning' })
  } catch { return }

  try {
    expectOk((await deleteSection(row.Code)) as any, '删除章节失败')
  } catch (e: any) {
    ElMessage.error(e?.err || e?.message || '删除章节失败')
    return
  }
  ElMessage.success('删除成功')
  await loadSections()
}

// ── 初始化 ──
onMounted(() => {
  loadTree()
})
</script>

<template>
  <YzhPageLayout title="报告章节定义" no-padding hide-toolbar>
    <el-row :gutter="16" class="report-rule-layout">
      <!-- 左侧：组织 → 标准 → 阶段 树 -->
      <el-col :span="6" class="tree-panel">
        <CertBizTree
          title="组织 → 标准 → 阶段"
          :node-types="['organization', 'standard', 'stage']"
          @select="handleNodeClick"
        />
      </el-col>

      <!-- 右侧：模板 + 章节 -->
      <el-col :span="18" class="content-panel">
        <!-- 未选中阶段 -->
        <el-empty v-if="!hasPhaseSelected" description="请在左侧选择阶段" :image-size="120" />

        <!-- 加载中 -->
        <div v-else-if="templateLoading" v-loading="true" class="loading-container" />

        <!-- 已选中阶段 -->
        <template v-else>
          <!-- 模板区域 -->
          <el-card shadow="never" class="template-card">
            <template #header>
              <div class="card-header">                  <span class="card-title">模板 — {{ selectedPhase?.Name || '' }}</span>
                <div class="card-actions">
                  <el-button v-if="hasTemplate" type="primary" size="small" @click="handleSaveTemplate" :loading="saving" :disabled="!canEdit">
                    更新模板
                  </el-button>
                  <el-button v-else type="primary" size="small" @click="handleSaveTemplate" :loading="saving" :disabled="!canEdit">
                    创建模板
                  </el-button>
                  <el-button v-if="hasTemplate" type="danger" size="small" text @click="handleDeleteTemplate">
                    删除模板
                  </el-button>
                </div>
              </div>
            </template>

            <el-form label-width="80px" class="template-form" :inline="true">
              <el-form-item label="模板名称" required>
                <el-input v-model="templateForm.TemplateName" placeholder="如：ISO9001审核报告" :disabled="!canEdit" style="width: 200px" />
              </el-form-item>
              <el-form-item label="上传文件">
                <el-upload
                  :http-request="handleUploadFile"
                  :show-file-list="false"
                  accept=".docx,.xlsx,.pdf,.doc,.xls"
                  :disabled="!canEdit"
                >
                  <el-button size="small" :icon="Upload" :disabled="!canEdit">上传模板文件</el-button>
                </el-upload>
                <span class="el-upload__tip" style="margin-left: 8px">支持 .docx / .xlsx / .pdf，最大 100MB</span>
              </el-form-item>
              <el-form-item label="备注">
                <el-input v-model="templateForm.Remark" :disabled="!canEdit" style="width: 240px" />
              </el-form-item>
            </el-form>
          </el-card>

          <!-- 章节区域（仅模板存在时显示） -->
          <el-card shadow="never" class="section-card" v-if="hasTemplate">
            <template #header>
              <div class="card-header">
                <span class="card-title">章节列表</span>
                <el-button type="primary" size="small" :icon="Plus" @click="openAddSection">
                  新建章节
                </el-button>
              </div>
            </template>

            <el-table :data="sectionData" stripe border v-loading="sectionLoading" row-key="Id">
              <el-table-column prop="SectionName" label="章节名称" min-width="180" />
              <el-table-column prop="SectionNameEn" label="英文名称" width="160" show-overflow-tooltip />
              <el-table-column prop="ClauseCode" label="条款" width="140" show-overflow-tooltip />
              <el-table-column prop="SortOrder" label="排序" width="80" align="center" />
              <el-table-column label="启用" width="80" align="center">
                <template #default="{ row }">
                  <el-switch :model-value="row.IsActive === 1" disabled size="small" />
                </template>
              </el-table-column>
              <el-table-column label="操作" width="120" fixed="right">
                <template #default="{ row }">
                  <el-button link type="primary" size="small" @click="openEditSection(row)">编辑</el-button>
                  <el-button link type="danger" size="small" @click="handleDeleteSection(row)">删除</el-button>
                </template>
              </el-table-column>
            </el-table>
          </el-card>
        </template>
      </el-col>
    </el-row>

    <!-- 章节编辑弹窗 -->
    <el-dialog
      v-model="sectionDialogVisible"
      :title="sectionFormMode === 'add' ? '新建章节' : '编辑章节'"
      width="520px"
      destroy-on-close
    >
      <YzhForm
        v-model="sectionForm"
        :fields="sectionFormFields"
        :loading="saving"
        @submit="handleSaveSection"
        @reset="sectionDialogVisible = false"
      />
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
.report-rule-layout {
  height: 100%;
  overflow: hidden;
}

.tree-panel {
  height: 100%;
  overflow: hidden;
}

.content-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 16px;
  overflow: hidden;
}

.tree-card {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.tree-card :deep(.el-card__body) {
  flex: 1;
  overflow-y: auto;
}

.template-card {
  flex-shrink: 0;
}

.template-card :deep(.el-card__body) {
  padding: 16px 20px;
}

.section-card {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.section-card :deep(.el-card__body) {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.section-card :deep(.el-table) {
  flex: 1;
}

.card-title {
  font-weight: 600;
  font-size: 14px;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.card-actions {
  display: flex;
  gap: 8px;
}

.tree-node {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 13px;
}

.node-icon {
  color: #909399;
  font-size: 14px;
}

.node-label {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.template-form {
  max-width: 600px;
}

.template-form :deep(.el-form-item) {
  margin-bottom: 16px;
}

.loading-container {
  height: 200px;
}
</style>
