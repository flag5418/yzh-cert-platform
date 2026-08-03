<template>
  <view-grid
    ref="grid"
    :columns="columns"
    :detail="detail"
    :details="details"
    :editFormFields="editFormFields"
    :editFormOptions="editFormOptions"
    :searchFormFields="searchFormFields"
    :searchFormOptions="searchFormOptions"
    :table="table"
    :extend="extend"
    :onInit="onInit"
    :onInited="onInited"
    :searchBefore="searchBefore"
    :addBefore="addBefore"
    :updateBefore="updateBefore"
    :rowClick="rowClick"
  >
    <template #gridHeader>
      <div>
        <el-alert
          title="认证申请管理：管理企业提交的认证申请，跟踪从申请受理到证书颁发的完整流程（5个审核阶段）"
          type="info"
          :closable="false"
          show-icon
          style="margin-bottom: 10px"
        />
        <!-- 审核阶段进度条 -->
        <div v-if="selectedRow" class="audit-progress">
          <h4>当前申请审核进度</h4>
          <el-steps
            :active="currentPhaseIndex"
            finish-status="success"
            align-center
          >
            <el-step
              v-for="(phase, index) in auditPhases"
              :key="phase.Code"
              :title="phase.name"
              :description="phase.description"
            ></el-step>
          </el-steps>
        </div>
      </div>
    </template>

    <template #btnLeft>
      <div>
        <el-button
          size="small"
          type="primary"
          @click="viewAuditProject"
          :disabled="!selectedRow"
        >
          查看审核项目
        </el-button>
        <el-button
          size="small"
          type="success"
          @click="submitApplication"
          :disabled="!selectedRow || selectedRow.Status !== 'draft'"
        >
          提交申请
        </el-button>
        <el-button
          size="small"
          type="warning"
          @click="acceptApplication"
          :disabled="!selectedRow || selectedRow.Status !== 'submitted'"
        >
          受理
        </el-button>
      </div>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { computed, getCurrentInstance, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import viewOptions from './options.js'

const router = useRouter()
const route = useRoute()
const grid = ref(null)
const selectedRow = ref(null)

const { proxy } = getCurrentInstance()

const {
  table,
  editFormFields,
  editFormOptions,
  searchFormFields,
  searchFormOptions,
  columns,
  detail,
  details,
  extend,
} = reactive(viewOptions())

let gridRef

// 审核阶段定义
const auditPhases = [
  {
    Code: 'application_review',
    name: '申请受理',
    description: '检查材料完整性',
  },
  { Code: 'document_review', name: '文件评审', description: '评审体系文件' },
  { Code: 'stage1_audit', name: '一阶段审核', description: '现场初审' },
  { Code: 'stage2_audit', name: '二阶段审核', description: '现场终审' },
  { Code: 'certification_decision', name: '认证决定', description: '综合评定' },
]

// 计算当前阶段索引
const currentPhaseIndex = computed(() => {
  if (!selectedRow.value) return -1

  const phaseOrder = [
    'draft',
    'submitted',
    'accepted',
    'doc_reviewing',
    'auditing',
    'completed_pass',
    'completed_fail',
  ]

  const status = selectedRow.value.Status
  return phaseOrder.indexOf(status)
})

/**
 * 初始化配置
 */
const onInit = async ($vm) => {
  gridRef = $vm

  // 处理企业列表跳转过来的筛选
  if (route.query.EnterpriseCode) {
    searchFormFields.EnterpriseCode = route.query.EnterpriseCode
  }
}

const onInited = async () => {}

/**
 * 查询前处理
 */
const searchBefore = async (param) => {
  // 关键词搜索
  if (searchFormFields.keyword) {
    param.wheres = [
      ...param.wheres,
      {
        name: 'keyword',
        value: searchFormFields.keyword.trim(),
        displayType: 'like',
      },
    ]
  }

  // 机构筛选
  if (searchFormFields.CbCode) {
    param.wheres.push({
      name: 'CbCode',
      value: searchFormFields.CbCode,
      displayType: 'equal',
    })
  }

  // 企业筛选
  if (searchFormFields.EnterpriseCode) {
    param.wheres.push({
      name: 'EnterpriseCode',
      value: searchFormFields.EnterpriseCode,
      displayType: 'equal',
    })
  }

  // 日期范围查询
  if (searchFormFields.dateRange && searchFormFields.dateRange.length === 2) {
    param.wheres.push({
      name: 'CreateDate',
      value: searchFormFields.dateRange[0],
      displayType: 'greaterThanOrEqual',
    })
    param.wheres.push({
      name: 'CreateDate',
      value: searchFormFields.dateRange[1],
      displayType: 'lessThanOrEqual',
    })
  }

  return true
}

/**
 * 新增前处理：自动生成申请编号
 */
const addBefore = async (formData) => {
  if (!formData.ApplicationNo) {
    const year = new Date().getFullYear()
    const random = String(Math.floor(Math.random() * 10000)).padStart(4, '0')
    formData.ApplicationNo = `${year}-APP-${random}`
  }
  return true
}

const updateBefore = async (formData) => {
  return true
}

const rowClick = async ({ row, column, event }) => {
  selectedRow.value = row
}

/**
 * 查看审核项目详情
 */
const viewAuditProject = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个申请')
    return
  }
  router.push({
    path: '/cert/audit-project',
    query: { ApplicationCode: selectedRow.value.Code },
  })
}

/**
 * 提交申请
 */
const submitApplication = () => {
  if (!selectedRow.value) return

  proxy
    .$confirm(
      `确定要提交申请 ${selectedRow.value.ApplicationNo} 吗？`,
      '确认提交',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'info' },
    )
    .then(async () => {
      await proxy.http.post('/api/CertApplication/Submit', {
        Code: selectedRow.value.Code,
      })
      proxy.$message.success('申请已提交')
      gridRef.refresh()
    })
    .catch(() => {})
}

/**
 * 受理申请
 */
const acceptApplication = () => {
  if (!selectedRow.value) return

  proxy
    .$confirm(
      `确定要受理申请 ${selectedRow.value.ApplicationNo} 吗？`,
      '确认受理',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' },
    )
    .then(async () => {
      await proxy.http.post('/api/CertApplication/Accept', {
        Code: selectedRow.value.Code,
      })
      proxy.$message.success('申请已受理，将进入文件评审阶段')
      gridRef.refresh()
    })
    .catch(() => {})
}
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}

.audit-progress {
  margin-top: 15px;
  padding: 15px;
  background: #f5f7fa;
  border-radius: 4px;

  h4 {
    margin: 0 0 15px 0;
    color: #303133;
    font-size: 14px;
  }
}
</style>
