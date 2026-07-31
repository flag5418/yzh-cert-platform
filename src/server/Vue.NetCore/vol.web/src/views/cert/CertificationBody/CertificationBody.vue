<!--
 *Author：CertPlatform
 *Date：2026-07-31
 *Contact：cert@platform.com
 *业务请在@/extension/cert/CertificationBody.jsx或CertificationBody.vue文件编写
 *新版本支持vue或【表.jsx】文件编写业务,文档见:https://v3.volcore.xyz/docs/view-grid、https://v3.volcore.xyz/docs/web
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
    :searchAfter="searchAfter"
    :addBefore="addBefore"
    :updateBefore="updateBefore"
    :rowClick="rowClick"
    :modelOpenBefore="modelOpenBefore"
    :modelOpenAfter="modelOpenAfter"
  >
    <!-- 自定义头部插槽 -->
    <template #gridHeader>
      <el-alert
        title="认证机构管理：管理所有ISO认证机构的基本信息，包括CNAS编号、联系方式等"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 10px"
      />
    </template>

    <!-- 自定义按钮区域 -->
    <template #btnLeft>
      <el-button size="small" type="success" @click="handleCustomAction" :disabled="!selectedRow">
        查看标准
      </el-button>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import extend from "@/extension/cert/CertificationBody.jsx";
import viewOptions from './options.js';
import { ref, reactive, getCurrentInstance } from "vue";

const grid = ref(null);
const selectedRow = ref(null);
const { proxy } = getCurrentInstance();

// 使用 reactive 包装 options（Vol 框架标准写法）
// 注意：options.js 返回的 extend 与 import 的 extend 冲突，使用 _extend 接收
const { 
  table, 
  editFormFields, 
  editFormOptions, 
  searchFormFields, 
  searchFormOptions, 
  columns, 
  detail, 
  details
} = reactive(viewOptions());

let gridRef;

/**
 * 页面初始化（ViewGrid 组件内部调用）
 * @param {Object} $vm - ViewGrid 组件实例
 */
const onInit = async ($vm) => {
  gridRef = $vm;
};

/**
 * 页面初始化完成后
 */
const onInited = async () => {
  // 可以在这里加载额外数据
};

/**
 * 查询前参数处理
 */
const searchBefore = async (param) => {
  return true;
};

/**
 * 查询后结果处理
 */
const searchAfter = async (result) => {
  return true;
};

/**
 * 新增前校验
 */
const addBefore = async (formData) => {
  return true;
};

/**
 * 编辑前校验
 */
const updateBefore = async (formData) => {
  return true;
};

/**
 * 表格行点击事件
 */
const rowClick = ({ row, column, event }) => {
  selectedRow.value = row;
};

/**
 * 弹窗打开前
 */
const modelOpenBefore = async (row) => {
  return true;
};

/**
 * 弹窗打开后
 */
const modelOpenAfter = async (row) => {
  // 可以在这里加载关联数据
};

/**
 * 自定义按钮操作
 */
const handleCustomAction = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一行数据');
    return;
  }
  proxy.$message.success(`操作: ${selectedRow.value.name}`);
};

// 对外暴露数据
defineExpose({})
</script>

<style lang="less" scoped>
.el-alert { border-radius: 4px; }
</style>
