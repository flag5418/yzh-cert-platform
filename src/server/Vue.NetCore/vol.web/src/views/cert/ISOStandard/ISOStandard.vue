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
          title="ISO 标准管理：管理各认证机构可开展认证的ISO标准（如 ISO 9001、ISO 13485 等）"
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
          type="success"
          @click="viewClauses"
          :disabled="!selectedRow"
        >
          查看条款
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
const modelOpenBefore = async (row) => { return true }
const modelOpenAfter = async (row) => {}

const viewClauses = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一行数据')
    return
  }
  router.push({
    path: '/cert/ISOClause',
    query: { StandardCode: selectedRow.value.StandardCode },
  })
}
</script>
