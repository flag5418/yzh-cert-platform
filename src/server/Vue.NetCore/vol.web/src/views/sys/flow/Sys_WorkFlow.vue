<!--
 *Author：jxx
 *Date：{Date}
 *Contact：283591387@qq.com
 *业务请在@/extension/sys/flow/Sys_WorkFlow.jsx或Sys_WorkFlow.vue文件编写
 *新版本支持vue或【表.jsx]文件编写业务,文档见:https://doc.volcore.xyz/docs/view-grid、https://doc.volcore.xyz/docs/web
 -->
<template>
  <view-grid ref="grid" :columns="columns" :detail="detail" :details="details" :editFormFields="editFormFields"
    :editFormOptions="editFormOptions" :searchFormFields="searchFormFields" :searchFormOptions="searchFormOptions"
    :table="table" :extend="extend" :onInit="onInit" :onInited="onInited" :searchBefore="searchBefore"
    :searchAfter="searchAfter" :addBefore="addBefore" :updateBefore="updateBefore" :rowClick="rowClick"
    :modelOpenBefore="modelOpenBefore" :modelOpenAfter="modelOpenAfter" :modelOpenBeforeAsync="modelOpenBeforeAsync">
    <!-- 自定义组件数据槽扩展，更多数据槽slot见文档 -->
    <template #gridHeader>
      <grid-header @parentCall="parentCall" ref="headerRef"></grid-header>
    </template>
  </view-grid>
</template>
<script setup lang="jsx">
import extend from "@/extension/sys/flow/Sys_WorkFlow.jsx";
import viewOptions from "./Sys_WorkFlow/options.js";
import gridHeader from './Sys_WorkFlow/WorkFlowGridHeader.vue';
import { ref, reactive, getCurrentInstance, watch, onMounted } from "vue";
const grid = ref(null);
const { proxy } = getCurrentInstance();
const headerRef = ref(null)
//http请求，proxy.http.post/get
const {
  table,
  editFormFields,
  editFormOptions,
  searchFormFields,
  searchFormOptions,
  columns,
  detail,
  details,
} = reactive(viewOptions());

let gridRef; //对应[表.jsx]文件中this.使用方式一样
//生成对象属性初始化
const onInit = async ($vm) => {
  gridRef = $vm;
  gridRef.queryFields = ['WorkName', 'WorkTableName']
  columns.forEach((x) => {
    if (x.field == "TitleTemplate") {
      //开启表格内容超出提示信息
      x.showOverflowTooltip = true;
    }
  });
};
//生成对象属性初始化后,操作明细表配置用到
const onInited = async () => { };
const searchBefore = async (param) => {
  //界面查询前,可以给param.wheres添加查询参数
  //返回false，则不会执行查询
  return true;
};
const searchAfter = async (rows, result) => {
  return true;
};
const addBefore = async (formData) => {
  //新建保存前formData为对象，包括明细表，可以给给表单设置值，自己输出看formData的值
  return true;
};
const updateBefore = async (formData) => {
  //编辑保存前formData为对象，包括明细表、删除行的Id
  return true;
};
const rowClick = ({ row, column, event }) => {
  //查询界面点击行事件
  // grid.value.toggleRowSelection(row); //单击行时选中当前行;
};
const modelOpenBefore = async (row) => {
  //弹出框打开后方法
  return true; //返回false，不会打开弹出框
};
const modelOpenAfter = (row, currentAction, isCopyClick) => {
  // if (isCopyClick) {
  //    editFormFields.NodeConfig=row.NodeConfig;
  //         editFormFields.LineConfig=row.LineConfig;
  // }
  const isAdd = currentAction == 'Add';//判断是否为新建操作
  //弹出框打开后方法，这可以设置表单默认
  //editFormFields.字段=值 ;//更多默认值配置见【前端开发->编辑表单只读与默认值、必填】
}
const parentCall = () => {
  gridRef.search();
}
const modelOpenBeforeAsync = (row,currentAction,isCopyClick) => {
  //点击编辑/新建按钮弹出框前，可以在此处写逻辑，如，从后台获取数据
  console.log(isCopyClick)
  if (isCopyClick) {
    row=Object.assign({},row)
    row.WorkFlow_Id=undefined;
  }
  headerRef.value.open(row,isCopyClick);
  return false;
}
//监听表单输入，做实时计算
//watch(() => editFormFields.字段,(newValue, oldValue) => {	})
//对外暴露数据
defineExpose({});
</script>
