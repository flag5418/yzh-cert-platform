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
          title="标准条款管理：管理 ISO 标准的具体条款内容，用于生成检查表和审核发现"
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
          @click="generateChecklist"
          :disabled="!selectedRow"
        >
          生成检查表项
        </el-button>
      </div>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { getCurrentInstance, reactive, ref } from 'vue'
import { useRoute } from 'vue-router'
import viewOptions from './options.js'

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

const onInit = async ($vm) => {
  gridRef = $vm

  // 如果从标准列表跳转过来，自动筛选
  if (route.query.StandardCode) {
    searchFormFields.StandardCode = route.query.StandardCode
  }
}

const onInited = async () => {}

const searchBefore = async (param) => {
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
 * 根据条款生成检查表项（预留功能）
 */
const generateChecklist = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个条款')
    return
  }
  proxy.$message.success('检查表项生成功能开发中...')
}
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}
</style>
