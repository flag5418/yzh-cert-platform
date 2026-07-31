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
      <el-alert
        title="ISO 标准管理：管理各认证机构可开展认证的ISO标准（如 ISO 9001、ISO 13485 等）"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 10px"
      />
    </template>

    <template #btnLeft>
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
        :disabled="!selectedRow || selectedRow.status !== 'implemented'"
      >
        导入标准条款
      </el-button>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { ref, reactive, getCurrentInstance, onMounted, watch } from 'vue';
import { useRouter, useRoute } from 'vue-router';
import viewOptions from './options.js';

const router = useRouter();
const route = useRoute();
const grid = ref(null);
const selectedRow = ref(null);

const {
  proxy,
} = getCurrentInstance();

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
} = reactive(viewOptions());

let gridRef;

const onInit = async ($vm) => {
  gridRef = $vm;
  
  // 如果从路由参数传入 cb_code，自动筛选
  if (route.query.cb_code) {
    searchFormFields.cb_code = route.query.cb_code;
  }
};

const onInited = async () => {};

const searchBefore = async (param) => {
  if (searchFormFields.keyword) {
    param.wheres = [
      ...param.wheres,
      {
        name: 'keyword',
        value: searchFormFields.keyword.trim(),
        displayType: 'like',
      },
    ];
  }
  return true;
};

const addBefore = async (formData) => {
  return true;
};

const updateBefore = async (formData) => {
  return true;
};

const rowClick = async ({ row, column, event }) => {
  selectedRow.value = row;
};

/**
 * 查看该标准的条款列表
 */
const viewClauses = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个标准');
    return;
  }
  router.push({
    path: '/cert/iso-clause',
    query: { standard_code: selectedRow.value.code },
  });
};

/**
 * 导入标准条款（预留接口）
 */
const importClauses = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个标准');
    return;
  }
  proxy.$confirm(
    `确定要导入 ${selectedRow.value.standard_code} 的标准条款吗？`,
    '导入确认',
    {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'info',
    }
  )
    .then(async () => {
      // TODO: 调用导入接口
      proxy.$message.success('导入功能开发中...');
    })
    .catch(() => {});
};
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}
</style>
