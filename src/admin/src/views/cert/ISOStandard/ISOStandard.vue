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
          type="primary"
          @click="viewClauses"
          :disabled="!selectedRow"
        >
          查看条款
        </el-button>
        <el-button
          size="small"
          type="success"
          @click="importClauses"
          :disabled="!selectedRow || selectedRow.Status !== 'implemented'"
        >
          导入标准条款
        </el-button>
      </div>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { getCurrentInstance, reactive, ref } from 'vue'
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

const onInit = async ($vm) => {
  gridRef = $vm

  // 如果从路由参数传入 cb_code，自动筛选
  if (route.query.CbCode) {
    searchFormFields.CbCode = route.query.CbCode
  }
}

const onInited = async () => {}

const searchBefore = async (param) => {
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
 * 查看该标准的条款列表
 */
const viewClauses = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个标准')
    return
  }
  router.push({
    path: '/cert/iso-clause',
    query: { StandardCode: selectedRow.value.Code },
  })
}

/**
 * 导入标准条款（预留接口）
 */
const importClauses = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个标准')
    return
  }
  proxy
    .$confirm(
      `确定要导入 ${selectedRow.value.StandardCode} 的标准条款吗？`,
      '导入确认',
      {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'info',
      },
    )
    .then(async () => {
      // TODO: 调用导入接口
      proxy.$message.success('导入功能开发中...')
    })
    .catch(() => {})
}
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}
</style>
