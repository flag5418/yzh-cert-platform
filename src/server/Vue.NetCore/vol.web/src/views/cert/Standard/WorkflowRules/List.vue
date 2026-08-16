<template>
  <div class="workflow-rules-page">
    <CertPageHeader title="审核规则库" :icon="IconSetting" />

    <div class="page-body">
      <!-- 左侧树形导航 -->
      <el-card shadow="never" class="tree-card">
        <template #header>
          <div class="tree-header">
            <span>机构 / 标准 / 阶段</span>
            <el-button link size="small" @click="loadTree">
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
          :load="loadTreeNodes"
          lazy
          @node-click="handleNodeClick"
        >
          <template #default="{ node, data }">
            <span class="tree-node">
              <el-icon class="tree-icon" :style="{ color: data.color || '#909399' }">
                <component :is="data.icon" />
              </el-icon>
              <span>{{ data.label }}</span>
              <el-tag v-if="data.ruleCount > 0" size="small" type="success" class="node-count">
                {{ data.ruleCount }}
              </el-tag>
            </span>
          </template>
        </el-tree>
      </el-card>

      <!-- 右侧内容区 -->
      <div class="content-area">
        <!-- 筛选栏 -->
        <el-card shadow="never" class="filter-card">
          <el-form :inline="true" class="filter-form">
            <el-form-item label="当前节点">
              <el-tag type="primary">{{ currentLabel || '全部规则' }}</el-tag>
            </el-form-item>
            <el-form-item>
              <el-button type="primary" @click="loadData">查询</el-button>
              <el-button @click="resetFilter">重置</el-button>
            </el-form-item>
          </el-form>
        </el-card>

        <!-- 列表 -->
        <el-card shadow="never" class="table-card">
          <template #header>
            <div class="card-header">
              <span class="card-title">NC检查规则列表</span>
              <div class="card-actions">
                <el-button type="primary" size="small" @click="openEdit(null)">
                  <el-icon><IconAdd /></el-icon> 新建规则
                </el-button>
              </div>
            </div>
          </template>

          <el-table :data="tableData" stripe border v-loading="loading" style="width:100%">
            <el-table-column prop="ruleCode" label="规则编码" width="140" />
            <el-table-column prop="ruleName" label="检查项名称" width="180" />
            <el-table-column prop="ruleNameEn" label="英文名称" width="150" show-overflow-tooltip />
            <el-table-column prop="standardCode" label="标准" width="100" />
            <el-table-column prop="phaseCode" label="阶段" width="80" />
            <el-table-column prop="clauseCode" label="条款" width="100" />
            <el-table-column prop="workflowCode" label="工作流" width="120" />
            <el-table-column prop="severityIfViolated" label="违规等级" width="100">
              <template #default="{ row }">
                <el-tag :type="severityTag(row.severityIfViolated)" size="small">{{ severityLabel(row.severityIfViolated) }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column prop="isActive" label="状态" width="70" align="center">
              <template #default="{ row }">
                <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '启用' : '禁用' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="260" fixed="right">
              <template #default="{ row }">
                <el-button type="primary" link size="small" @click="openEdit(row)">编辑</el-button>
                <el-button type="success" link size="small" @click="openDesigner(row)">工作流</el-button>
                <el-button type="warning" link size="small" @click="handleCopy(row)">复制</el-button>
                <el-button type="danger" link size="small" @click="handleDelete(row)">删除</el-button>
              </template>
            </el-table-column>
          </el-table>
          <el-pagination
            v-model:current-page="page"
            :page-size="pageSize"
            :total="total"
            layout="total, prev, pager, next"
            style="margin-top:16px;justify-content:flex-end"
            @current-change="loadData"
          />
        </el-card>
      </div>
    </div>

    <!-- 编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editForm.id ? '编辑规则' : '新建规则'" width="700px" destroy-on-close>
      <el-form :model="editForm" label-width="110px" ref="formRef">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="规则编码" prop="ruleCode">
              <el-input v-model="editForm.ruleCode" :disabled="!!editForm.id" placeholder="唯一编码" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="检查项名称" prop="ruleName">
              <el-input v-model="editForm.ruleName" placeholder="中文名称" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="英文名称">
              <el-input v-model="editForm.ruleNameEn" placeholder="English name" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="机构编码">
              <el-input v-model="editForm.orgCode" disabled placeholder="由树形导航自动带入" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="8">
            <el-form-item label="标准编码">
              <el-input v-model="editForm.standardCode" disabled placeholder="由树形导航自动带入" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="阶段编码">
              <el-input v-model="editForm.phaseCode" disabled placeholder="由树形导航自动带入" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="条款编码">
              <el-input v-model="editForm.clauseCode" placeholder="如 6.1" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="工作流编码">
              <el-select v-model="editForm.workflowCode" filterable allow-create placeholder="选择或新建工作流" style="width:100%">
                <el-option v-for="w in workflowList" :key="w.workflowCode" :label="w.workflowName" :value="w.workflowCode" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="违规等级">
              <el-select v-model="editForm.severityIfViolated" style="width:100%">
                <el-option label="符合" value="conformant" />
                <el-option label="观察项" value="observation" />
                <el-option label="轻微不符合" value="minor" />
                <el-option label="严重不符合" value="major" />
              </el-select>
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="NC描述模板">
          <el-input v-model="editForm.ncDescriptionTemplate" type="textarea" :rows="2" placeholder="不符合项描述模板" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="editForm.remark" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="handleSave">保存</el-button>
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
const loading = ref(false)
const tableData = ref([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const dialogVisible = ref(false)
const formRef = ref(null)
const treeRef = ref(null)
const currentLabel = ref('全部规则')
const currentFilter = reactive({ orgCode: '', standardCode: '', phaseCode: '' })
const treeData = ref([])
const workflowList = ref([])
const editForm = reactive({
  id: null, ruleCode: '', ruleName: '', ruleNameEn: '', orgCode: '',
  standardCode: '', phaseCode: '', clauseCode: '', workflowCode: '',
  severityIfViolated: 'minor', ncDescriptionTemplate: '', remark: '', ruleJson: ''
})

const severityTag = (v) => ({ conformant: 'success', observation: 'warning', minor: 'danger', major: 'danger' }[v] ?? 'info')
const severityLabel = (v) => ({ conformant: '符合', observation: '观察项', minor: '轻微', major: '严重' }[v] ?? v)

// ── 树形导航（懒加载） ──

async function loadTree() {
  try {
    const res = await proxy.http.get('api/cert-platform/tree', null, false)
    if (res?.status) {
      treeData.value = (res.data || []).map(org => ({
        ...org,
        isLeaf: false
      }))
    }
  } catch (e) { console.error('加载树失败', e) }
}

async function loadTreeNodes(node, resolve) {
  if (node.level === 0) {
    await loadTree()
    resolve(treeData.value)
    return
  }
  // 标准节点：展开阶段子节点
  if (node.level === 1) {
    const std = node.data
    const phases = await loadPhasesForStandard(std.key)
    resolve(phases)
    return
  }
  resolve([])
}

async function loadPhasesForStandard(stdKey) {
  const parts = stdKey.split('_')
  const orgCode = parts[1]
  const stdCode = parts[2]
  try {
    const res = await proxy.http.get('api/cert-platform/phases', null, false)
    if (!res?.status) return []
    return (res.data || []).map(phase => ({
      key: `phase_${orgCode}_${stdCode}_${phase.phaseCode}`,
      label: `${phase.phaseCode} ${phase.phaseName}`,
      icon: Document,
      color: '#e6a23c',
      ruleCount: 0,
      isLeaf: true,
      filter: { orgCode, standardCode: stdCode, phaseCode: phase.phaseCode }
    }))
  } catch { return [] }
}

function handleNodeClick(data) {
  if (data.filter) {
    Object.assign(currentFilter, data.filter)
    currentLabel.value = `${data.label}`
  } else if (data.key?.startsWith('std_')) {
    const parts = data.key.split('_')
    Object.assign(currentFilter, { orgCode: parts[1], standardCode: parts[2], phaseCode: '' })
    currentLabel.value = data.label
  } else if (data.key?.startsWith('org_')) {
    Object.assign(currentFilter, { orgCode: data.key.replace('org_', ''), standardCode: '', phaseCode: '' })
    currentLabel.value = data.label
  } else {
    Object.assign(currentFilter, { orgCode: '', standardCode: '', phaseCode: '' })
    currentLabel.value = '全部规则'
  }
  page.value = 1
  loadData()
}

// ── 数据操作 ──

async function loadData() {
  loading.value = true
  try {
    const res = await proxy.http.post('api/validation-rule/page', {
      Page: page.value, Rows: pageSize.value, Sort: 'Id', Order: 'desc'
    }, true, { params: { ...currentFilter } })
    if (res?.status) {
      tableData.value = res.data?.rows || []
      total.value = res.data?.total || 0
    }
  } catch (e) { console.error(e) } finally { loading.value = false }
}

const resetFilter = () => {
  Object.assign(currentFilter, { orgCode: '', standardCode: '', phaseCode: '' })
  currentLabel.value = '全部规则'
  page.value = 1
  loadData()
}

const openEdit = (row) => {
  if (row) Object.assign(editForm, row)
  else Object.assign(editForm, {
    id: null, ruleCode: '', ruleName: '', ruleNameEn: '',
    orgCode: currentFilter.orgCode, standardCode: currentFilter.standardCode,
    phaseCode: currentFilter.phaseCode, clauseCode: '', workflowCode: '',
    severityIfViolated: 'minor', ncDescriptionTemplate: '', remark: '', ruleJson: ''
  })
  dialogVisible.value = true
  loadWorkflowList()
}

async function loadWorkflowList() {
  try {
    const res = await proxy.http.get('api/workflow-definition/list?workflowType=validation&isActive=true', null, false)
    if (res?.status) workflowList.value = res.data || []
  } catch {}
}

const openDesigner = (row) => {
  const params = { ...currentFilter }
  if (row?.workflowCode) params.id = row.workflowCode
  router.push({ path: '/CertPlatform/WorkflowDesigner/new', query: params })
}

const handleSave = async () => {
  try {
    const res = await proxy.http.post('api/validation-rule', editForm, true)
    if (res?.status) { ElMessage.success('保存成功'); dialogVisible.value = false; loadData() }
    else ElMessage.error(res?.message || '保存失败')
  } catch (e) { ElMessage.error('保存失败') }
}

const handleCopy = async (row) => {
  try {
    const res = await proxy.http.post(`api/validation-rule/copy/${row.ruleCode}`, null, true)
    if (res?.status) { ElMessage.success('复制成功'); loadData() }
  } catch (e) { ElMessage.error('复制失败') }
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除规则「${row.ruleName}」？`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/validation-rule/delete/${row.id}`, null, true)
    if (res?.status) { ElMessage.success('删除成功'); loadData() }
  } catch (e) { if (e !== 'cancel') ElMessage.error('删除失败') }
}

onMounted(() => { loadData(); loadWorkflowList() })
</script>

<style scoped lang="less">
.workflow-rules-page { padding: var(--yzh-space-5, 20px); }
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
