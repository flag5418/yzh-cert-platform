
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
      </div>
    </template>

    <template #btnLeft>
      <div>
        <el-button
          size="small"
          type="primary"
          @click="assignAuditor"
          :disabled="!selectedRow"
        >
          分配审核员
        </el-button>
      </div>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { getCurrentInstance, reactive, ref } from 'vue'
import viewOptions from './options.js'

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

const onInit = async ($vm) => { gridRef = $vm }
const onInited = async () => {}
const searchBefore = async (param) => { return true }
const addBefore = async (formData) => { return true }
const updateBefore = async (formData) => { return true }
const rowClick = ({ row, column, event }) => { selectedRow.value = row }

const assignAuditor = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一行数据')
    return
  }
  proxy.$message.success(`分配审核员：任务编号=${selectedRow.value.TaskNumber}`)
}
</script>
