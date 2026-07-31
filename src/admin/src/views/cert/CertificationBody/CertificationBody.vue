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
      <el-button
        size="small"
        type="success"
        @click="viewStandards"
        :disabled="!selectedRow"
      >
        查看标准
      </el-button>
      <el-button
        size="small"
        type="warning"
        @click="viewEnterprises"
        :disabled="!selectedRow"
      >
        查看企业
      </el-button>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { ref, reactive, getCurrentInstance, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import viewOptions from './options.js';

const router = useRouter();
const grid = ref(null);
const selectedRow = ref(null);

const {
  proxy,
} = getCurrentInstance();

// 解构配置
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

/**
 * 初始化配置
 */
const onInit = async ($vm) => {
  gridRef = $vm;
  
  // 设置默认排序
  gridRef.sortName = 'id';
  
  // 自定义查询字段（支持模糊搜索）
  gridRef.searchFormFields.keyword.extra = true;
};

/**
 * 初始化完成后
 */
const onInited = async () => {
  // 可以在这里加载额外数据
};

/**
 * 查询前处理
 * 支持关键词多字段搜索
 */
const searchBefore = async (param) => {
  // 如果输入了关键词，添加自定义查询条件
  if (searchFormFields.keyword) {
    const keyword = searchFormFields.keyword.trim();
    if (keyword) {
      param.wheres = [
        ...param.wheres,
        {
          name: 'keyword',
          value: keyword,
          displayType: 'like',
        },
      ];
    }
  }
  return true;
};

/**
 * 查询后处理
 */
const searchAfter = async (result) => {
  return true;
};

/**
 * 新增前校验
 */
const addBefore = async (formData) => {
  // 自动生成 CNAS 编号（如果未填写）
  if (!formData.cb_code) {
    const count = await proxy.http.post('/api/CertificationBody/GetMaxId', {});
    formData.cb_code = `CB${String(count + 1).padStart(3, '0')}`;
  }
  return true;
};

/**
 * 编辑前校验
 */
const updateBefore = async (formData) => {
  return true;
};

/**
 * 行点击事件
 */
const rowClick = async ({ row, column, event }) => {
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
 * 查看该机构的标准列表
 */
const viewStandards = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个机构');
    return;
  }
  router.push({
    path: '/cert/iso-standard',
    query: { cb_code: selectedRow.value.code },
  });
};

/**
 * 查看该机构的企业列表
 */
const viewEnterprises = () => {
  if (!selectedRow.value) {
    proxy.$message.warning('请先选择一个机构');
    return;
  }
  router.push({
    path: '/cert/enterprise',
    query: { cb_code: selectedRow.value.code },
  });
};
</script>

<style lang="less" scoped>
.el-alert {
  border-radius: 4px;
}
</style>
