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
          title="审核任务管理：管理各审核阶段的具体任务，包括任务分配、进度跟踪、结果记录（5个核心阶段）"
          type="info"
          :closable="false"
          show-icon
          style="margin-bottom: 10px"
        />

        <!-- 阶段筛选标签 -->
        <div class="phase-tabs">
          <el-radio-group
            v-model="selectedPhase"
            size="small"
            @change="filterByPhase"
          >
            <el-radio-button label="">全部</el-radio-button>
            <el-radio-button
              v-for="phase in auditPhases"
              :key="phase.Code"
              :label="phase.Code"
            >
              {{ phase.name }}
            </el-radio-button>
          </el-radio-group>
        </div>
      </div>
    </template>

    <template #btnLeft>
      <div>
        <el-button
          size="small"
          type="primary"
          @click="assignAuditor"
          :disabled="
            !selectedRow || selectedRow.Status !== 'pending_assignment'
          "
        >
          分配审核员
        </el-button>
        <el-button
          size="small"
          type="success"
          @click="startTask"
          :disabled="!selectedRow || selectedRow.Status !== 'pending_start'"
        >
          开始执行
        </el-button>
        <el-button
          size="small"
          type="warning"
          @click="completeTask"
          :disabled="!selectedRow || selectedRow.Status !== 'in_progress'"
        >
          完成任务
        </el-button>
        <el-button
          size="small"
          type="info"
          @click="viewChecklist"
          :disabled="!selectedRow"
        >
          查看检查表
        </el-button>
      </div>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { getCurrentInstance, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import viewOptions from './options.js'

const router = useRouter()
const grid = ref(null)
const selectedRow = ref(null)
const selectedPhase = ref('')

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
  { Code: 'application_review', name: '申请受理' },
  { Code: 'document_review', name: '文件评审' },
  { Code: 'stage1_audit', name: '一阶段审核' },
  { Code: 'stage2_audit', name: '二阶段审核' },
  { Code: 'certification_decision', name: '认证决定' },
]

/**
 * 初始化配置
 */
const onInit = async ($vm) => {
  gridRef = $vm
}

const onInited = async () => {}

/**
 * 按阶段筛选
 */
const filterByPhase = (value) => {
  if (value) {
    // 添加阶段筛选条件
    gridRef.searchFormFields.PhaseCode = value
  } else {
    delete gridRef.searchFormFields.PhaseCode
  }
  gridRef.refresh()
}

/**
 * 查询前处理
 */
const searchBefore = async (param) => {
  if (searchFormFields.keyword) {
    param.wheres = [
      ...param.wheres,
      {
        name: 'TaskNumber',
        value: searchFormFields.keyword.trim(),
        displayType: 'like',
      },
    ]
  }

  // 如果选择了阶段筛选
  if (selectedPhase.value) {
    param.wheres.push({
      name: 'PhaseCode',
      value: selectedPhase.value,
      displayType: 'equal',
    })
  }

  return true
}

const addBefore = async (formData) => {
  return true
}

const updateBefore = async (formData) => {
  return true
}

const rowClick = async ({ row, column, event }) => {
  selectedRow.value = row
}

/**
 * 分配审核员
 */
const assignAuditor = () => {
  if (!selectedRow.value) return

  proxy
    .$prompt('请输入或选择审核员', '分配审核员', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      inputPlaceholder: '选择审核员',
    })
    .then(async ({ value }) => {
      await proxy.http.post('/api/AuditTask/AssignAuditor', {
        Code: selectedRow.value.Code,
        auditorId: value,
      })
      proxy.$message.success('审核员已分配')
      gridRef.refresh()
    })
    .catch(() => {})
}

/**
 * 开始执行任务
 */
const startTask = () => {
  if (!selectedRow.value) return

  proxy
    .$confirm(
      `确定要开始任务 ${selectedRow.value.TaskNumber} 吗？`,
      '确认开始',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'info' },
    )
    .then(async () => {
      await proxy.http.post('/api/AuditTask/Start', {
        Code: selectedRow.value.Code,
      })
      proxy.$message.success('任务已开始')
      gridRef.refresh()
    })
    .catch(() => {})
}

/**
 * 完成任务
 */
const completeTask = () => {
  if (!selectedRow.value) return

  proxy
    .$confirm(
      `确定要完成任务 ${selectedRow.value.TaskNumber} 吗？`,
      '确认完成',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'success' },
    )
    .then(async () => {
      await proxy.http.post('/api/AuditTask/Complete', {
        Code: selectedRow.value.Code,
      })
      proxy.$message.success('任务已完成')
      gridRef.refresh()
    })
    .catch(() => {})
}

/**
 * 查看检查表
 */
const viewChecklist = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个任务')
    return
  }
  router.push({
    path: '/cert/checklist-item',
    query: { Code: selectedRow.value.Code },
  })
}
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}

.phase-tabs {
  margin-top: 15px;
  padding: 10px;
  background: #fafafa;
  border-radius: 4px;
}
</style>
