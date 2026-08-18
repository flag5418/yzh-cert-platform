<template>
  <div class="workflow-rules-page">
    <CertPageHeader title="NC检查项配置" :icon="IconSetting" />

    <div class="page-body">
      <!-- 左侧树形导航：使用公共组件 -->
      <el-card shadow="never" class="tree-card">
        <template #header>
          <div class="tree-header">
            <span>机构 / 标准 / 阶段</span>
            <el-button link size="small" @click="refreshTree">
              <el-icon><IconRefresh /></el-icon>
            </el-button>
          </div>
        </template>
        <YzhStdTree
          ref="stdTreeRef"
          badge-field="ruleCount"
          @select="handleTreeSelect"
          @loaded="onTreeLoaded"
        />
      </el-card>

      <!-- 右侧内容区 -->
      <div class="content-area">
        <!-- 筛选栏 -->
        <el-card shadow="never" class="filter-card">
          <el-form :inline="true" class="filter-form">
            <el-form-item label="当前节点">
              <el-tag type="primary">{{ currentLabel || '全部检查项' }}</el-tag>
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
              <span class="card-title">NC检查项列表</span>
              <div class="card-actions">
                <el-button type="primary" size="small" @click="openEdit(null)" :disabled="!currentFilter.standardCode">
                  <el-icon><IconAdd /></el-icon> 新建检查项
                </el-button>
              </div>
            </div>
          </template>

          <el-table :data="tableData" stripe border v-loading="loading" style="width:100%">
            <el-table-column prop="ruleName" label="中文名称" width="200" />
            <el-table-column prop="ruleNameEn" label="英文名称" width="150" show-overflow-tooltip />
            <el-table-column label="关联条款" width="120">
              <template #default="{ row }">
                <span v-if="row.clauseNumber">{{ row.clauseNumber }} {{ row.clauseTitle }}</span>
                <span v-else>-</span>
              </template>
            </el-table-column>
            <el-table-column label="启用" width="80" align="center">
              <template #default="{ row }">
                <el-tag :type="row.isActive ? 'success' : 'info'" size="small">{{ row.isActive ? '是' : '否' }}</el-tag>
              </template>
            </el-table-column>
            <el-table-column label="操作" width="140" fixed="right">
              <template #default="{ row }">
                <div class="row-actions">
                  <el-button type="primary" link size="small" @click="openEdit(row)">编辑</el-button>
                  <el-button type="danger" link size="small" @click="handleDelete(row)">删除</el-button>
                </div>
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

    <!-- 编辑弹窗（极简 5 字段） -->
    <el-dialog v-model="dialogVisible" :title="editForm.id ? '编辑检查项' : '新建检查项'" width="500px" destroy-on-close>
      <el-form :model="editForm" label-width="100px" ref="formRef">
        <el-form-item label="中文名称" prop="ruleName" :rules="[{ required: true, message: '请输入中文名称' }]">
          <el-input v-model="editForm.ruleName" placeholder="如：资源提供检查" />
        </el-form-item>
        <el-form-item label="英文名称">
          <el-input v-model="editForm.ruleNameEn" placeholder="English name" />
        </el-form-item>
        <el-form-item label="关联条款" prop="clauseCode" :rules="[{ required: true, message: '请选择关联条款' }]">
          <el-tree-select
            v-model="editForm.clauseCode"
            :data="clauseTreeData"
            :props="{ label: 'label', value: 'code', children: 'children' }"
            filterable
            check-strictly
            placeholder="选择ISO条款"
            style="width: 100%"
          />
        </el-form-item>
        <el-form-item label="是否启用">
          <el-switch v-model="editForm.isActive" />
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
import { ElMessage, ElMessageBox } from 'element-plus'
import { CertPageHeader } from '@/certcore'
import { YzhStdTree } from '@/yzh'
import { IconSetting, IconAdd, IconRefresh } from '@/yzh/icons'

const { proxy } = getCurrentInstance()
const loading = ref(false)
const tableData = ref([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const dialogVisible = ref(false)
const formRef = ref(null)
const stdTreeRef = ref(null)
const currentLabel = ref('全部检查项')
const currentFilter = reactive({ orgCode: '', standardCode: '', phaseCode: '' })
const clauseTreeData = ref([])
const editForm = reactive({
  id: null,
  code: '',
  orgCode: '',
  standardCode: '',
  phaseCode: '',
  clauseCode: '',
  workflowCode: '',
  ruleCode: '',
  ruleName: '',
  ruleNameEn: '',
  severityIfViolated: '',
  ncDescriptionTemplate: '',
  ruleJson: '',
  remark: '',
  isActive: true
})

// ── 公共树组件事件 ──

function handleTreeSelect({ phase, standard, org, orgCode, stdCode, standardCode, phaseCode, phaseName }) {
  Object.assign(currentFilter, { orgCode, standardCode: stdCode || standardCode, phaseCode })
  currentLabel.value = `${standard?.label || ''} / ${phase.label}`
  page.value = 1
  loadData()
  if (stdCode || standardCode) loadClauseTree(stdCode || standardCode)
}

function onTreeLoaded(treeData) {
  // 树加载完成，可以在这里做额外处理
}

const refreshTree = () => {
  stdTreeRef.value?.reload()
  loadData()
}

// ── 条款树 ──

async function loadClauseTree(stdCode) {
  const code = stdCode || currentFilter.standardCode
  if (!code) {
    clauseTreeData.value = []
    return
  }
  try {
    const res = await proxy.http.get(
      `api/iso-clause/tree?standardCode=${code}`, null, false
    )
    if (res?.status) {
      clauseTreeData.value = res.data || []
    }
  } catch (e) { console.error('加载条款树失败', e) }
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
  currentLabel.value = '全部检查项'
  stdTreeRef.value?.clearSelection()
  page.value = 1
  loadData()
}

const openEdit = (row) => {
  if (row) {
    Object.assign(editForm, {
      id: row.id,
      code: row.code || '',
      orgCode: row.orgCode || currentFilter.orgCode,
      standardCode: row.standardCode || currentFilter.standardCode,
      phaseCode: row.phaseCode || currentFilter.phaseCode,
      clauseCode: row.clauseCode || '',
      workflowCode: row.workflowCode || '',
      ruleCode: row.ruleCode || '',
      ruleName: row.ruleName || '',
      ruleNameEn: row.ruleNameEn || '',
      severityIfViolated: row.severityIfViolated || '',
      ncDescriptionTemplate: row.ncDescriptionTemplate || '',
      ruleJson: row.ruleJson || '',
      remark: row.remark || '',
      isActive: row.isActive !== false
    })
  } else {
    Object.assign(editForm, {
      id: null, code: '', ruleCode: '',
      orgCode: currentFilter.orgCode,
      standardCode: currentFilter.standardCode,
      phaseCode: currentFilter.phaseCode,
      clauseCode: '', workflowCode: '',
      ruleName: '', ruleNameEn: '',
      severityIfViolated: '', ncDescriptionTemplate: '',
      ruleJson: '', remark: '', isActive: true
    })
  }
  // 确保条款树已加载
  if (currentFilter.standardCode && clauseTreeData.value.length === 0) {
    loadClauseTree(currentFilter.standardCode)
  }
  dialogVisible.value = true
}

const handleSave = async () => {
  if (!formRef.value) return
  await formRef.value.validate(async (valid) => {
    if (!valid) return
    try {
      const res = await proxy.http.post('api/validation-rule', editForm, true)
      if (res?.status) { ElMessage.success('保存成功'); dialogVisible.value = false; loadData() }
      else ElMessage.error(res?.message || '保存失败')
    } catch (e) { ElMessage.error('保存失败') }
  })
}

const handleDelete = async (row) => {
  try {
    await ElMessageBox.confirm(`确认删除检查项「${row.ruleName}」？`, '确认', { type: 'warning' })
    const res = await proxy.http.post(`api/validation-rule/delete/${row.id}`, null, true)
    if (res?.status) { ElMessage.success('删除成功'); loadData() }
  } catch (e) { if (e !== 'cancel') ElMessage.error('删除失败') }
}

onMounted(() => { loadData() })
</script>

<style scoped lang="less">
.workflow-rules-page { padding: 16px; height: 100%; display: flex; flex-direction: column; overflow: hidden; box-sizing: border-box; }
.page-body { display: flex; gap: 16px; flex: 1; min-height: 0; }
.tree-card { width: 260px; min-width: 260px; display: flex; flex-direction: column; }
.tree-header { display: flex; align-items: center; justify-content: space-between; }
.content-area { flex: 1; display: flex; flex-direction: column; overflow: hidden; }
.filter-card { margin-bottom: 12px; }
.table-card { flex: 1; overflow: hidden; display: flex; flex-direction: column; }
.card-header { display:flex; align-items:center; justify-content:space-between; }
.card-title { font-size:15px; font-weight:600; }
.row-actions { display: flex; gap: 4px; white-space: nowrap; }
</style>
