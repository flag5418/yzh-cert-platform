<template>
  <div class="report-def-page">
    <CertPageHeader title="报告内容配置" :icon="IconSetting" />

    <div class="page-body">
      <!-- 左侧树形导航 -->
      <el-card shadow="never" class="tree-card">
        <template #header>
          <div class="tree-header">
            <span>机构 / 标准 / 阶段</span>
            <el-button link size="small" @click="refreshTree">
              <el-icon><IconRefresh /></el-icon>
            </el-button>
          </div>
        </template>
        <YzhStdTree ref="stdTreeRef" @select="handleTreeSelect" />
      </el-card>

      <!-- 右侧内容区 -->
      <div class="content-area">
        <template v-if="currentFilter.standardCode">
          <!-- 报告主表（单条：org+std+phase 唯一） -->
          <el-card shadow="never" class="template-card">
            <template #header>
              <div class="card-header">
                <span class="card-title">报告模板</span>
                <el-tag v-if="templateForm.id" size="small" type="success">已创建</el-tag>
                <el-tag v-else size="small" type="warning">未创建</el-tag>
              </div>
            </template>

            <el-form :model="templateForm" label-width="100px" class="template-form" @submit.prevent>
              <el-row :gutter="16">
                <el-col :span="12">
                  <el-form-item label="报告名称" prop="templateName" :rules="[{ required: true, message: '请输入报告名称' }]">
                    <el-input v-model="templateForm.templateName" placeholder="如：ISO13485第一阶段审核报告" />
                  </el-form-item>
                </el-col>
                <el-col :span="12">
                  <el-form-item label="备注">
                    <el-input v-model="templateForm.remark" placeholder="备注信息" />
                  </el-form-item>
                </el-col>
              </el-row>
              <el-form-item label="模板文件">
                <div class="file-row">
                  <el-input
                    v-if="templateForm.templateFilePath"
                    :model-value="getFileName(templateForm.templateFilePath)"
                    readonly
                    style="flex:1"
                  >
                    <template #append>
                      <el-button link @click="templateForm.templateFilePath = ''">清除</el-button>
                    </template>
                  </el-input>
                  <el-text v-else type="info" style="flex:1">尚未上传模板文件</el-text>
                  <el-upload
                    ref="uploadRef"
                    :action="uploadUrl"
                    :headers="uploadHeaders"
                    :data="uploadData"
                    :on-success="onUploadSuccess"
                    :on-error="onUploadError"
                    :before-upload="beforeUpload"
                    :limit="1"
                    :auto-upload="true"
                    :show-file-list="false"
                    style="margin-left:8px"
                  >
                    <el-button type="primary" size="small">
                      <el-icon><IconUpload /></el-icon> 上传文件
                    </el-button>
                  </el-upload>
                </div>
              </el-form-item>
              <div class="form-footer">
                <el-button type="primary" @click="saveTemplate" :loading="saving">
                  {{ templateForm.id ? '保存' : '创建' }}
                </el-button>
              </div>
            </el-form>
          </el-card>

          <!-- 报告章节（依赖主表已保存） -->
          <el-card shadow="never" class="section-card">
            <template #header>
              <div class="card-header">
                <span class="card-title">报告章节</span>
                <el-button type="primary" size="small" @click="openSecEdit(null)" :disabled="!templateForm.id">
                  <el-icon><IconAdd /></el-icon> 新建章节
                </el-button>
              </div>
            </template>

            <el-table v-if="templateForm.id" :data="sectionData" stripe border v-loading="secLoading" style="width:100%">
              <el-table-column prop="sectionName" label="章节名称" min-width="180" />
              <el-table-column prop="sectionNameEn" label="英文名称" width="150" />
              <el-table-column prop="sortOrder" label="排序" width="80" align="center" />
              <el-table-column label="启用" width="80" align="center">
                <template #default="{ row }">
                  <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '是' : '否' }}</el-tag>
                </template>
              </el-table-column>
              <el-table-column label="操作" width="140" fixed="right">
                <template #default="{ row }">
                  <div class="row-actions">
                    <el-button type="primary" link size="small" @click="openSecEdit(row)">编辑</el-button>
                    <el-button type="danger" link size="small" @click="deleteSection(row)">删除</el-button>
                  </div>
                </template>
              </el-table-column>
            </el-table>
            <el-empty v-else description="请先创建报告模板，再编辑章节" :image-size="60" />
          </el-card>
        </template>

        <!-- 未选择标准时的空状态 -->
        <el-empty v-else description="请先在左侧选择机构 → 标准 → 阶段" :image-size="100" style="margin:auto" />
      </div>
    </div>

    <!-- 章节编辑弹窗 -->
    <el-dialog v-model="secDialogVisible" :title="secForm.id ? '编辑章节' : '新建章节'" width="500px" destroy-on-close>
      <el-form :model="secForm" label-width="100px" ref="secFormRef">
        <el-form-item label="章节名称" prop="sectionName" :rules="[{ required: true, message: '请输入章节名称' }]">
          <el-input v-model="secForm.sectionName" placeholder="如：审核发现" />
        </el-form-item>
        <el-form-item label="英文名称">
          <el-input v-model="secForm.sectionNameEn" placeholder="English name" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="secForm.sortOrder" :min="0" :max="999" />
        </el-form-item>
        <el-form-item label="是否启用">
          <el-switch v-model="secForm.isActive" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="secForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="secDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveSection">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, reactive, computed, getCurrentInstance } from 'vue'
import { useStore } from 'vuex'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CertPageHeader } from '@/certcore'
import { YzhStdTree } from '@/yzh'
import { IconSetting, IconAdd, IconRefresh, IconUpload } from '@/yzh/icons'

const store = useStore()
const { proxy } = getCurrentInstance()
const saving = ref(false)
const secLoading = ref(false)
const sectionData = ref([])
const stdTreeRef = ref(null)
const uploadRef = ref(null)

// 当前选中的上下文（统一用 standardCode 即标准编码如 ISO13485-CODE）
const currentFilter = reactive({
  orgCode: '',
  standardCode: '',  // 如 ISO13485-CODE
  phaseCode: ''
})

// 报告模板表单（单条记录：org+std+phase 唯一）
const templateForm = reactive({
  id: null,
  code: '',
  templateName: '',
  orgCode: '',
  standardCode: '',
  phaseCode: '',
  cbCode: '',
  templateFilePath: '',
  remark: ''
})

// 章节弹窗
const secDialogVisible = ref(false)
const secFormRef = ref(null)
const secForm = reactive({ id: null, code: '', reportCode: '', orgCode: '', sectionName: '', sectionNameEn: '', sortOrder: 0, isActive: true, remark: '' })

// ── 工具函数 ──

function getFileName(path) {
  if (!path) return ''
  return path.split('/').pop() || path
}

// ── 公共树组件事件 ──

function handleTreeSelect({ orgCode, standardCode, phaseCode }) {
  // 统一用 standardCode（标准编码如 ISO13485-CODE），不用 stdCode（UUID）
  Object.assign(currentFilter, { orgCode, standardCode, phaseCode })
  loadTemplate()
}

const refreshTree = () => {
  stdTreeRef.value?.reload()
  loadTemplate()
}

// ── 模板操作（单条 upsert 模式） ──

const loadTemplate = async () => {
  if (!currentFilter.standardCode || !currentFilter.phaseCode) {
    resetTemplateForm()
    return
  }
  try {
    // 注意：proxy.http.get 的第二个 param 参数不会自动拼接为 query string
    // 必须手动拼接到 URL
    const query = `orgCode=${encodeURIComponent(currentFilter.orgCode)}&standardCode=${encodeURIComponent(currentFilter.standardCode)}&phaseCode=${encodeURIComponent(currentFilter.phaseCode)}`
    const res = await proxy.http.get(`api/report-definition/template/context?${query}`, null, false)
    if (res?.status && res.data) {
      Object.assign(templateForm, {
        id: res.data.id,
        code: res.data.code || '',
        templateName: res.data.templateName || '',
        orgCode: res.data.orgCode || currentFilter.orgCode,
        standardCode: res.data.standardCode || currentFilter.standardCode,
        phaseCode: res.data.phaseCode || currentFilter.phaseCode,
        cbCode: res.data.cbCode || currentFilter.orgCode,
        templateFilePath: res.data.templateFilePath || '',
        remark: res.data.remark || ''
      })
      loadSections()
    } else {
      resetTemplateForm()
    }
  } catch (e) { console.error('加载模板失败', e) }
}

function resetTemplateForm() {
  Object.assign(templateForm, {
    id: null, code: '', templateName: '', templateFilePath: '', remark: '',
    orgCode: currentFilter.orgCode,
    standardCode: currentFilter.standardCode,
    phaseCode: currentFilter.phaseCode,
    cbCode: currentFilter.orgCode
  })
  sectionData.value = []
}

const saveTemplate = async () => {
  if (!templateForm.templateName?.trim()) {
    ElMessage.warning('请输入报告名称')
    return
  }
  saving.value = true
  try {
    // 确保上下文字段正确
    templateForm.orgCode = currentFilter.orgCode
    templateForm.standardCode = currentFilter.standardCode
    templateForm.phaseCode = currentFilter.phaseCode
    templateForm.cbCode = currentFilter.orgCode

    const res = await proxy.http.post('api/report-definition/template', { ...templateForm }, true)
    if (res?.status) {
      ElMessage.success(res.message || '保存成功')
      // 用返回的实体直接更新表单状态
      if (res.data) {
        Object.assign(templateForm, {
          id: res.data.id,
          code: res.data.code || '',
          templateName: res.data.templateName || templateForm.templateName,
          templateFilePath: res.data.templateFilePath || '',
          remark: res.data.remark || ''
        })
      }
      loadSections()
    } else {
      ElMessage.error(res?.message || '保存失败')
    }
  } catch (e) {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}

// ── 文件上传 ──

const uploadUrl = computed(() => '/api/report-definition/template/upload')
const uploadHeaders = computed(() => {
  // Vol 框架 token 存在 Vuex store 中，不在 localStorage
  const token = store.getters.getToken()
  return { Authorization: token || '' }
})
const uploadData = computed(() => ({
  orgCode: currentFilter.orgCode,
  standardCode: currentFilter.standardCode,
  phaseCode: currentFilter.phaseCode,
}))

const beforeUpload = (file) => {
  const allowed = ['.docx', '.xlsx', '.pdf', '.doc', '.xls']
  const ext = '.' + (file.name.split('.').pop() || '').toLowerCase()
  if (!allowed.includes(ext)) {
    ElMessage.error('仅支持 .docx / .xlsx / .pdf 格式')
    return false
  }
  if (file.size > 100 * 1024 * 1024) {
    ElMessage.error('文件大小不能超过 100MB')
    return false
  }
  return true
}

const onUploadSuccess = async (response) => {
  if (response?.status && response.data?.path) {
    ElMessage.success('文件上传成功')
    templateForm.templateFilePath = response.data.path
    // 如果主表已存在，自动保存路径
    if (templateForm.id) {
      await proxy.http.post('api/report-definition/template', { ...templateForm }, true)
    }
  } else {
    ElMessage.error(response?.message || '上传失败')
  }
}

const onUploadError = () => {
  ElMessage.error('上传失败，请重试')
}

// ── 章节操作 ──

const loadSections = async () => {
  if (!templateForm.code) { sectionData.value = []; return }
  secLoading.value = true
  try {
    const res = await proxy.http.get(`api/report-definition/section/${templateForm.code}`, null, false)
    if (res?.status) sectionData.value = res.data || []
  } catch (e) { console.error(e) } finally { secLoading.value = false }
}

const openSecEdit = (row) => {
  if (!templateForm.id) {
    ElMessage.warning('请先创建报告模板')
    return
  }
  if (row) {
    Object.assign(secForm, {
      id: row.id, code: row.code || '',
      reportCode: row.reportCode || templateForm.code || '',
      orgCode: row.orgCode || currentFilter.orgCode,
      sectionName: row.sectionName || '',
      sectionNameEn: row.sectionNameEn || '',
      sortOrder: row.sortOrder ?? 0,
      isActive: row.isActive !== false,
      remark: row.remark || ''
    })
  } else {
    Object.assign(secForm, {
      id: null, code: '',
      reportCode: templateForm.code || '',
      orgCode: currentFilter.orgCode,
      sectionName: '', sectionNameEn: '',
      sortOrder: sectionData.value.length,
      isActive: true, remark: ''
    })
  }
  secDialogVisible.value = true
}

const saveSection = async () => {
  if (!secFormRef.value) return
  await secFormRef.value.validate(async (valid) => {
    if (!valid) return
    try {
      const res = await proxy.http.post('api/report-definition/section', secForm, true)
      if (res?.status) {
        ElMessage.success('保存成功')
        secDialogVisible.value = false
        await loadSections()
      } else {
        ElMessage.error(res?.message || '保存失败')
      }
    } catch (e) { ElMessage.error('保存失败') }
  })
}

const deleteSection = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除章节「${row.sectionName}」？`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/report-definition/section/delete/${row.id}`, null, true)
    if (res?.status) {
      ElMessage.success('删除成功')
      await loadSections()
    }
  } catch (e) { if (e !== 'cancel') ElMessage.error('删除失败') }
}
</script>

<style scoped lang="less">
.report-def-page { padding: var(--yzh-space-5, 20px); }
.page-body { display: flex; gap: 16px; height: calc(100vh - 140px); }
.tree-card { width: 260px; min-width: 260px; display: flex; flex-direction: column; }
.tree-header { display: flex; align-items: center; justify-content: space-between; }
.content-area { flex: 1; display: flex; flex-direction: column; overflow: hidden; gap: 16px; }
.template-card { flex-shrink: 0; }
.template-form { padding: 8px 0; }
.section-card { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.section-card :deep(.el-card__body) { flex: 1; overflow-y: auto; }
.card-header { display:flex; align-items:center; justify-content:space-between; gap: 8px; }
.card-title { font-size:15px; font-weight:600; }
.row-actions { display: flex; gap: 4px; white-space: nowrap; }
.form-footer { text-align: right; padding-top: 8px; }
.file-row { display: flex; align-items: center; width: 100%; }
</style>
