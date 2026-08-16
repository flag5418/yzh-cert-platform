<template>
  <div class="report-def-page">
    <CertPageHeader title="报告章节定义" :icon="IconSetting" />

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
        <el-tree
          ref="treeRef"
          :data="treeData"
          :props="{ label: 'label', children: 'children' }"
          node-key="key"
          highlight-current
          default-expand-all
          @node-click="handleNodeClick"
        >
          <template #default="{ node, data }">
            <span class="tree-node">
              <el-icon class="tree-icon" :style="{ color: data.color || '#909399' }">
                <component :is="data.icon" />
              </el-icon>
              <span>{{ data.label }}</span>
              <el-tag v-if="data.tplCount" size="small" :type="data.tplCount > 0 ? 'success' : 'info'" class="node-count">
                {{ data.tplCount }}
              </el-tag>
            </span>
          </template>
        </el-tree>
      </el-card>

      <!-- 右侧内容区 -->
      <div class="content-area">
        <el-card shadow="never" class="filter-card">
          <el-form :inline="true">
            <el-form-item label="当前节点">
              <el-tag type="primary">{{ currentLabel || '全部报告' }}</el-tag>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" @click="loadTemplates">查询</el-button>
              <el-button @click="resetFilter">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <el-card shadow="never" class="table-card">
          <template #header>
            <div class="card-header">
              <span class="card-title">报告模板</span>
              <el-button type="primary" size="small" @click="openTplEdit(null)">
                <el-icon><IconAdd /></el-icon> 新建报告
              </el-button>
            </div>
          </template>

          <el-table :data="templateData" stripe border v-loading="tplLoading" style="width:100%">
            <el-table-column prop="code" label="编码" width="140" />
            <el-table-column prop="templateName" label="报告名称" width="200" />
            <el-table-column prop="standardCode" label="标准" width="100" />
            <el-table-column prop="phaseCode" label="阶段" width="80" />
            <el-table-column prop="templateFilePath" label="报告路径" width="200" show-overflow-tooltip />
            <el-table-column prop="isDefault" label="默认" width="60" align="center">
              <template #default="{ row }">
                <el-tag :type="row.isDefault ? 'success' : 'info'" size="small">{{ row.isDefault ? '是' : '否' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="220" fixed="right">
              <template #default="{ row }">
                <el-button type="primary" link size="small" @click="openSections(row)">章节配置</el-button>
                <el-button type="primary" link size="small" @click="openTplEdit(row)">编辑</el-button>
                <el-button type="danger" link size="small" @click="deleteTemplate(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>

        <!-- 章节列表（选中模板后显示） -->
        <el-card shadow="never" class="table-card" v-if="selectedTpl" style="flex:1">
          <template #header>
            <div class="card-header">
              <span class="card-title">报告章节 — {{ selectedTpl.templateName }}</span>
              <el-button type="primary" size="small" @click="openSecEdit(null)">
                <el-icon><IconAdd /></el-icon> 新建章节
              </el-button>
            </div>
          </template>
          <el-table :data="sectionData" stripe border v-loading="secLoading" style="width:100%">
            <el-table-column prop="sectionName" label="章节名称" width="180" />
            <el-table-column prop="sectionNameEn" label="英文名称" width="150" />
            <el-table-column prop="clauseCode" label="关联条款" width="100" />
            <el-table-column prop="workflowCode" label="工作流" width="140" />
            <el-table-column prop="sortOrder" label="排序" width="60" align="center" />
            <el-table-column prop="isActive" label="状态" width="70" align="center">
              <template #default="{ row }">
                <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '启用' : '禁用' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="220" fixed="right">
              <template #default="{ row }">
                <el-button type="primary" link size="small" @click="openSecEdit(row)">编辑</el-button>
                <el-button type="success" link size="small" @click="openSectionDesigner(row)">工作流</el-button>
                <el-button type="warning" link size="small" @click="copySection(row)">复制</el-button>
                <el-button type="danger" link size="small" @click="deleteSection(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
        </el-card>
      </div>
    </div>

    <!-- 模板编辑弹窗 -->
    <el-dialog v-model="tplDialogVisible" :title="tplForm.id ? '编辑报告' : '新建报告'" width="600px" destroy-on-close>
      <el-form :model="tplForm" label-width="100px">
        <el-form-item label="报告名称">
          <el-input v-model="tplForm.templateName" />
        </el-form-item>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="机构编码">
              <el-input v-model="tplForm.orgCode" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="标准编码">
              <el-input v-model="tplForm.standardCode" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="阶段编码">
              <el-input v-model="tplForm.phaseCode" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="认证机构">
              <el-input v-model="tplForm.cbCode" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="报告路径">
          <el-input v-model="tplForm.templateFilePath" placeholder="MinIO文件路径" />
        </el-form-item>
        <el-form-item label="是否默认">
          <el-switch v-model="tplForm.isDefault" active-value="1" inactive-value="0" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="tplForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="tplDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveTemplate">保存</el-button>
      </template>
    </el-dialog>

    <!-- 章节编辑弹窗 -->
    <el-dialog v-model="secDialogVisible" :title="secForm.id ? '编辑章节' : '新建章节'" width="650px" destroy-on-close>
      <el-form :model="secForm" label-width="100px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="章节名称">
              <el-input v-model="secForm.sectionName" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="英文名称">
              <el-input v-model="secForm.sectionNameEn" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="关联条款">
              <el-input v-model="secForm.clauseCode" placeholder="如 6.1" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="工作流编码">
              <el-input v-model="secForm.workflowCode" placeholder="绑定工作流" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="章节内容">
          <el-input v-model="secForm.content" type="textarea" :rows="3" placeholder="初始内容或占位文本" />
        </el-form-item>
        <el-form-item label="工作流DAG JSON">
          <el-input v-model="secForm.sectionJson" type="textarea" :rows="4" placeholder="DAG JSON配置" />
          <el-link type="primary" @click="openSectionDesigner(secForm)" style="margin-top:4px;display:inline-block">
            在工作流设计器中可视化编辑 →
          </el-link>
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="secForm.remark" type="textarea" :rows="2" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="secForm.sortOrder" :min="0" :max="999" />
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
import { ref, reactive, onMounted, getCurrentInstance } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import { CertPageHeader } from '@/certcore'
import { IconSetting, IconAdd, IconRefresh } from '@/yzh/icons'
import { OfficeBuilding, Folder, Document } from '@element-plus/icons-vue'

const router = useRouter()
const { proxy } = getCurrentInstance()
const tplLoading = ref(false)
const secLoading = ref(false)
const templateData = ref([])
const sectionData = ref([])
const selectedTpl = ref(null)
const treeData = ref([])
const currentLabel = ref('全部报告')
const currentFilter = reactive({ orgCode: '', standardCode: '', phaseCode: '' })

const tplDialogVisible = ref(false)
const secDialogVisible = ref(false)
const tplForm = reactive({ id: null, templateName: '', orgCode: '', standardCode: '', phaseCode: '', cbCode: '', templateFilePath: '', isDefault: 0, remark: '' })
const secForm = reactive({ id: null, reportCode: '', sectionName: '', sectionNameEn: '', clauseCode: '', workflowCode: '', sectionJson: '', content: '', remark: '', sortOrder: 0, isActive: true })

const loadTemplates = async () => {
  tplLoading.value = true
  try {
    const res = await proxy.http.post('api/report-definition/template/page', { Page: 1, Rows: 100, Sort: 'Id', Order: 'asc' }, true,
      { params: { ...currentFilter } })
    if (res?.status) templateData.value = res.data?.rows || []
  } catch (e) { console.error(e) } finally { tplLoading.value = false }
}

const resetFilter = () => {
  Object.assign(currentFilter, { orgCode: '', standardCode: '', phaseCode: '' })
  currentLabel.value = '全部报告'
  loadTemplates()
}

const loadTree = async () => {
  try {
    const res = await proxy.http.get('api/report-definition/template/list', null, false)
    if (res?.status) {
      const tpls = res.data || []
      const groups = {}
      for (const t of tpls) {
        const org = t.orgCode || '全局'
        const std = t.standardCode || '未分类'
        const phase = t.phaseCode || '未指定'
        if (!groups[org]) groups[org] = {}
        if (!groups[org][std]) groups[org][std] = {}
        if (!groups[org][std][phase]) groups[org][std][phase] = []
        groups[org][std][phase].push(t)
      }
      treeData.value = Object.entries(groups).map(([org, stds]) => ({
        key: `org_${org}`, label: org, icon: OfficeBuilding, color: '#409eff',
        tplCount: Object.values(stds).reduce((s, ps) => s + Object.values(ps).flat().length, 0),
        children: Object.entries(stds).map(([std, phases]) => ({
          key: `std_${org}_${std}`, label: std, icon: Folder, color: '#67c23a',
          children: Object.entries(phases).map(([phase, tls]) => ({
            key: `phase_${org}_${std}_${phase}`, label: phase, icon: Document, color: '#e6a23c',
            tplCount: tls.length, _filter: { orgCode: org, standardCode: std, phaseCode: phase }
          }))
        }))
      }))
    }
  } catch (e) { console.error(e) }
}

const handleNodeClick = (data) => {
  if (data._filter) {
    Object.assign(currentFilter, data._filter)
    currentLabel.value = data.label
  } else if (data.key?.startsWith('std_')) {
    const p = data.key.split('_')
    currentFilter.orgCode = p[1]; currentFilter.standardCode = p[2]; currentFilter.phaseCode = ''
    currentLabel.value = p[2]
  } else if (data.key?.startsWith('org_')) {
    currentFilter.orgCode = data.key.replace('org_', '')
    currentFilter.standardCode = ''; currentFilter.phaseCode = ''
    currentLabel.value = currentFilter.orgCode
  } else {
    Object.assign(currentFilter, { orgCode: '', standardCode: '', phaseCode: '' })
    currentLabel.value = '全部报告'
  }
  loadTemplates()
}

const refreshTree = () => { loadTree(); loadTemplates() }

const openTplEdit = (row) => {
  if (row) Object.assign(tplForm, row)
  else Object.assign(tplForm, { id: null, templateName: '', orgCode: currentFilter.orgCode, standardCode: currentFilter.standardCode, phaseCode: currentFilter.phaseCode, cbCode: '', templateFilePath: '', isDefault: 0, remark: '' })
  tplDialogVisible.value = true
}

const saveTemplate = async () => {
  try {
    const res = await proxy.http.post('api/report-definition/template', tplForm, true)
    if (res?.status) { ElMessage.success('保存成功'); tplDialogVisible.value = false; loadTemplates(); loadTree() }
    else ElMessage.error(res?.message || '保存失败')
  } catch (e) { ElMessage.error('保存失败') }
}

const deleteTemplate = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除报告「${row.templateName}」？`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/report-definition/template/delete/${row.id}`, null, true)
    if (res?.status) { ElMessage.success('删除成功'); loadTemplates(); loadTree() }
  } catch (e) { if (e !== 'cancel') ElMessage.error('删除失败') }
}

const openSections = async (row) => {
  selectedTpl.value = row
  sectionData.value = []
  secLoading.value = true
  try {
    const res = await proxy.http.get(`api/report-definition/section/${row.code}`, null, false)
    if (res?.status) sectionData.value = res.data || []
  } catch (e) { console.error(e) } finally { secLoading.value = false }
}

const openSecEdit = (row) => {
  if (row) Object.assign(secForm, row)
  else Object.assign(secForm, { id: null, reportCode: selectedTpl.value?.code || '', sectionName: '', sectionNameEn: '', clauseCode: '', workflowCode: '', sectionJson: '', content: '', remark: '', sortOrder: sectionData.value.length, isActive: true })
  secDialogVisible.value = true
}

const openSectionDesigner = (row) => {
  const params = { ...currentFilter }
  if (row?.workflowCode) params.id = row.workflowCode
  router.push({ path: '/CertPlatform/WorkflowDesigner/new', query: params })
}

const saveSection = async () => {
  try {
    const res = await proxy.http.post('api/report-definition/section', secForm, true)
    if (res?.status) { ElMessage.success('保存成功'); secDialogVisible.value = false; openSections(selectedTpl.value) }
    else ElMessage.error(res?.message || '保存失败')
  } catch (e) { ElMessage.error('保存失败') }
}

const copySection = async (row) => {
  try {
    const res = await proxy.http.post(`api/report-definition/section/copy/${row.id}`, null, true)
    if (res?.status) { ElMessage.success('复制成功'); openSections(selectedTpl.value) }
  } catch (e) { ElMessage.error('复制失败') }
}

const deleteSection = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除章节「${row.sectionName}」？`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/report-definition/section/delete/${row.id}`, null, true)
    if (res?.status) { ElMessage.success('删除成功'); openSections(selectedTpl.value) }
  } catch (e) { if (e !== 'cancel') ElMessage.error('删除失败') }
}

onMounted(() => { loadTree(); loadTemplates() })
</script>

<style scoped lang="less">
.report-def-page { padding: var(--yzh-space-5, 20px); }
.page-body { display: flex; gap: 16px; height: calc(100vh - 140px); }
.tree-card { width: 260px; min-width: 260px; display: flex; flex-direction: column; }
.tree-header { display: flex; align-items: center; justify-content: space-between; }
.tree-node { display: flex; align-items: center; gap: 4px; font-size: 13px; }
.tree-icon { font-size: 14px; }
.node-count { margin-left: 4px; }
.content-area { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.filter-card { margin-bottom: 12px; }
.table-card { flex: 1; overflow: hidden; display: flex; flex-direction: column; }
.card-header { display:flex; align-items:center; justify-content:space-between; }
.card-title { font-size:15px; font-weight:600; }
</style>
