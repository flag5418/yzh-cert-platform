<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { YzhPageLayout } from '@yzh-core/components/layout'
import {
  getNCRulePage,
  saveNCRule,
  deleteNCRule,
  toggleNCRuleActive,
  getISOClauseTree,
  type NCRule,
  type ISOClause
} from '@share/api/nc-rule'

const loading = ref(false)
const tableData = ref<NCRule[]>([])
const page = ref(1)
const pageSize = ref(20)
const total = ref(0)
const keyword = ref('')
const orgCode = ref('')
const standardCode = ref('')
const phaseCode = ref('')

// 编辑弹窗
const dialogVisible = ref(false)
const editFormRef = ref()
const editForm = reactive<Partial<NCRule>>({
  ruleName: '',
  ruleNameEn: '',
  clauseCode: '',
  isActive: true,
  remark: ''
})
const submitting = ref(false)

// 条款树
const clauseTreeData = ref<ISOClause[]>([])
const clauseLoading = ref(false)

const columns = [
  { prop: 'ruleName', label: '中文名称', width: 200 },
  { prop: 'ruleNameEn', label: '英文名称', width: 150 },
  { prop: 'clauseNumber', label: '关联条款', width: 150 },
  { prop: 'isActive', label: '启用', width: 80, align: 'center' },
  { prop: 'remark', label: '备注', minWidth: 150, showOverflowTooltip: true },
  { prop: 'actions', label: '操作', width: 120, fixed: 'right' }
]

async function loadData() {
  loading.value = true
  try {
    const res = await getNCRulePage({ page: page.value, rows: pageSize.value }, {
      keyword: keyword.value || null,
      orgCode: orgCode.value || null,
      standardCode: standardCode.value || null,
      phaseCode: phaseCode.value || null
    })
    tableData.value = res?.rows || []
    total.value = res?.total || 0
  } catch (e: any) {
    ElMessage.error(e?.message || '加载失败')
  } finally {
    loading.value = false
  }
}

async function loadClauseTree() {
  if (!standardCode.value) {
    clauseTreeData.value = []
    return
  }
  clauseLoading.value = true
  try {
    clauseTreeData.value = await getISOClauseTree(standardCode.value)
  } catch (e: any) {
    ElMessage.error('加载条款失败')
  } finally {
    clauseLoading.value = false
  }
}

function openEdit(row: NCRule | null) {
  if (row) {
    Object.assign(editForm, { ...row })
  } else {
    Object.assign(editForm, {
      id: undefined,
      ruleName: '',
      ruleNameEn: '',
      clauseCode: '',
      isActive: true,
      remark: ''
    })
  }
  dialogVisible.value = true
}

async function doSave() {
  if (!editForm.ruleName) {
    ElMessage.warning('请输入中文名称')
    return
  }
  if (!editForm.clauseCode) {
    ElMessage.warning('请选择关联条款')
    return
  }
  submitting.value = true
  try {
    await saveNCRule(editForm as NCRule)
    ElMessage.success('保存成功')
    dialogVisible.value = false
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '保存失败')
  } finally {
    submitting.value = false
  }
}

async function doDelete(row: NCRule) {
  try {
    await ElMessageBox.confirm(`确定删除检查项「${row.ruleName}」？`, '确认删除', { type: 'warning' })
  } catch { return }
  try {
    await deleteNCRule(row.id!)
    ElMessage.success('删除成功')
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '删除失败')
  }
}

async function toggleActive(row: NCRule) {
  try {
    await toggleNCRuleActive(row.id!)
    ElMessage.success('操作成功')
    loadData()
  } catch (e: any) {
    ElMessage.error(e?.message || '操作失败')
  }
}

function onStandardChange() {
  loadClauseTree()
}

onMounted(loadData)
</script>

<template>
  <YzhPageLayout title="NC 规则配置">
    <template #toolbar>
      <el-button type="primary" @click="openEdit(null)">
        <el-icon><Plus /></el-icon> 新建检查项
      </el-button>
    </template>

    <el-card shadow="never">
      <!-- 筛选 -->
      <el-form :inline="true" :model="{ keyword, orgCode, standardCode, phaseCode }" style="margin-bottom: 16px">
        <el-form-item label="检查项名称">
          <el-input v-model="keyword" placeholder="搜索名称" clearable style="width: 160px" @keyup.enter="loadData" />
        </el-form-item>
        <el-form-item label="机构">
          <el-select v-model="orgCode" placeholder="选择机构" clearable style="width: 140px">
            <el-option label="CB-001 北京认证中心" value="CB-001" />
          </el-select>
        </el-form-item>
        <el-form-item label="标准">
          <el-select v-model="standardCode" placeholder="选择标准" clearable style="width: 160px" @change="onStandardChange">
            <el-option label="ISO 9001:2015" value="ISO9001" />
            <el-option label="ISO 14001:2015" value="ISO14001" />
            <el-option label="ISO 45001:2018" value="ISO45001" />
          </el-select>
        </el-form-item>
        <el-form-item label="阶段">
          <el-select v-model="phaseCode" placeholder="选择阶段" clearable style="width: 120px">
            <el-option label="一阶段" value="phase1" />
            <el-option label="二阶段" value="phase2" />
          </el-select>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" @click="loadData">查询</el-button>
          <el-button @click="keyword=''; orgCode=''; standardCode=''; phaseCode=''; loadData({})">重置</el-button>
        </el-form-item>
      </el-form>

      <!-- 列表 -->
      <el-table :data="tableData" stripe border v-loading="loading">
        <el-table-column prop="ruleName" label="中文名称" width="200" />
        <el-table-column prop="ruleNameEn" label="英文名称" width="150" show-overflow-tooltip />
        <el-table-column prop="clauseNumber" label="关联条款" width="150" />
        <el-table-column label="启用" width="80" align="center">
          <template #default="{ row }">
            <el-switch :model-value="row.isActive" @change="toggleActive(row)" />
          </template>
        </el-table-column>
        <el-table-column prop="remark" label="备注" min-width="150" show-overflow-tooltip />
        <el-table-column label="操作" width="120" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" size="small" @click="openEdit(row)">编辑</el-button>
            <el-button link type="danger" size="small" @click="doDelete(row)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>

      <el-pagination
        v-model:current-page="page"
        :page-size="pageSize"
        :total="total"
        layout="total, prev, pager, next"
        style="margin-top: 16px; justify-content: flex-end"
        @current-change="loadData"
      />
    </el-card>

    <!-- 编辑弹窗 -->
    <el-dialog v-model="dialogVisible" :title="editForm.id ? '编辑检查项' : '新建检查项'" width="600px">
      <el-form ref="editFormRef" :model="editForm" label-width="100px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="中文名称" required>
              <el-input v-model="editForm.ruleName" placeholder="如：资源提供检查" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="英文名称">
              <el-input v-model="editForm.ruleNameEn" placeholder="如：Resource Provision" />
            </el-form-item>
          </el-col>
        </el-row>
        <el-form-item label="关联条款" required>
          <el-tree-select
            v-model="editForm.clauseCode"
            :data="clauseTreeData"
            :props="{ label: 'clauseLabel', value: 'code' }"
            filterable
            check-strictly
            placeholder="选择关联条款"
            style="width: 100%"
            :loading="clauseLoading"
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
        <el-button type="primary" @click="doSave" :loading="submitting">保存</el-button>
      </template>
    </el-dialog>
  </YzhPageLayout>
</template>

<style scoped>
</style>
