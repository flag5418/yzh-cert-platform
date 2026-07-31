<!--
 *Author：CertPlatform
 *Date：2026-07-31
 *Contact：cert@platform.com
 *业务请在@/extension/cert/CertApplication.jsx或CertApplication.vue文件编写
 -->
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
        title="认证申请管理：管理企业提交的认证申请，跟踪从申请受理到证书颁发的完整流程（5个审核阶段）"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 10px"
      />
    </template>

    <template #btnLeft>
      <el-button size="small" type="primary" @click="handleCustomAction" :disabled="!selectedRow">
        自定义操作
      </el-button>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import extend from "@/extension/cert/CertApplication.jsx";
import viewOptions from './options.js';
import { ref, reactive, getCurrentInstance } from "vue";

const grid = ref(null);
const selectedRow = ref(null);
const { proxy } = getCurrentInstance();

const { 
  table, editFormFields, editFormOptions, searchFormFields, 
  searchFormOptions, columns, detail, details
} = reactive(viewOptions());

let gridRef;

const onInit = async ($vm) => { gridRef = $vm; };
const onInited = async () => {};
const searchBefore = async (param) => { return true; };
const addBefore = async (formData) => { return true; };
const updateBefore = async (formData) => { return true; };
const rowClick = ({ row, column, event }) => { selectedRow.value = row; };
const modelOpenBefore = async (row) => { return true; };
const modelOpenAfter = async (row) => {};

const handleCustomAction = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一行数据');
    return;
  }
  proxy.$message.success(`操作: ${selectedRow.value.application_no}`);
};

defineExpose({})
</script>

<style lang="less" scoped>
.el-alert { border-radius: 4px; }
</style>
