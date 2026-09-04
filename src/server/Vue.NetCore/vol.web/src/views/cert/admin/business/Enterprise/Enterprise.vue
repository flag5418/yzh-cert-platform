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
          title="企业管理：维护所有申请认证的企业信息，包括基本资料、信用代码、联系人等"
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
          @click="viewApplications"
          :disabled="!selectedRow"
        >
          查看申请
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

const onInit = async ($vm) => {
  gridRef = $vm
}

const onInited = async () => {}

const searchBefore = async (param) => {
  if (searchFormFields.Name) {
    param.wheres = [
      ...param.wheres,
      {
        name: 'Name',
        value: searchFormFields.Name.trim(),
        displayType: 'like',
      },
    ]
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
 * 查看该企业的认证申请列表
 */
const viewApplications = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个企业')
    return
  }
  router.push({
    path: '/cert/cert-application',
    query: { EnterpriseCode: selectedRow.value.Code },
  })
}
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}
</style>
