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
    <template #gridHeader>
      <div class="audit-content" v-if="isFlow()">
        <el-tabs v-model="model" @tab-click="handleClick">
          <el-tab-pane v-for="(item, index) in list" :name="item.name" :key="index">
            <template #label>
              <div class="custom-tabs-label">
                <span
                  ><span class="el-icon-date" style="margin-right: 2px"></span>
                  {{ $ts(item.value) }}</span
                >
              </div>
            </template>
          </el-tab-pane>
        </el-tabs>
      </div>
    </template>
    <!-- 自定义组件数据槽扩展，更多数据槽slot见文档 -->
    <template #gridBody>
      <ViewGridAudit ref="audit">
        <template #auditContent>
          <!-- auditContent -->
        </template>
      </ViewGridAudit>
    </template>
  </view-grid>
</template>
<script setup lang="jsx">
import extend from "@/extension/sys/flow/Sys_WorkFlowTable.jsx";
import viewOptions from "./Sys_WorkFlowTable/options.js";
import ViewGridAudit from "@/components/basic/ViewGrid/ViewGridAudit.vue";
import { ref, reactive, getCurrentInstance, watch, onMounted, onActivated } from "vue";
const grid = ref(null);
const { proxy } = getCurrentInstance();

const props = defineProps({
  id: {
    type: String,
    default: "",
  },
});

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


let AuditStatus = 0;
const audit = ref();
const list = ref([
  {
    key: "0",
    value: "待审核",
  },
  {
    key: "1",
    value: "审核通过",
  },

  {
    key: "3",
    value: "审核未通过",
  },
  {
    key: "4",
    value: "驳回",
  },
  {
    key: "40",
    value: "我的审核",
  },
  {
    key: "50",
    value: "我的提交",
  },
  {
    key: "-1",
    value: "全部",
  },
]);
const model = ref("0");

const handleClick = (item) => {
  model.value = list.value[item.index].key;
  AuditStatus = model.value;
  gridRef.search();
};
// const onChange = (val) => {
//   AuditStatus = val;
//   gridRef.search();
// }

const viewData = (row) => {
  proxy.$tabs.open({
    text: row.WorkTableName || row.WorkName,
    path: "/" + row.WorkTable,
    query: { id: row.WorkTableKey, viewflow: 1 },
  });
};

let gridRef; //对应[表.jsx]文件中this.使用方式一样
//生成对象属性初始化
const onInit = async ($vm) => {
  gridRef = $vm;
  gridRef.queryFields = ["WorkName","WorkTableName"];

  //不显示自带的查看流程按钮
  gridRef.showTableAudit = false;
  columns.forEach((x) => {
    //开启表格内容超出提示信息
    if (x.field == "TitleTemplate") {
      x.showOverflowTooltip = true;
    }
  });
  let width = 120;
  //表格上添加自定义按钮
  columns.push({
    title: "操作",
    field: "操作",
    width: width,
    fixed: "right",
    align: "center",
    render: (h, { row, column, index }) => {
      // const isAudit=row.
      const btns = proxy.base.getAuthButtons(row.WorkTable);
      return (
        <div>
          <el-button
            link
            onClick={($e) => {
              audit.value.open([row], true);
            }}
            color="#0425dd"
            plain
            size="small"
            style="padding: 5px !important;margin: 0;"
          >
            <i style="font-size:13px" class="el-icon-edit-outline"></i>
            {proxy.$ts("审核")}
          </el-button>
          <el-button
            link
            onClick={($e) => {
              viewData(row);
            }}
            color="#0425dd"
            plain
            size="small"
            style="padding: 5px !important;margin: 0;"
          >
            <i style="font-size:13px" class="el-icon-document"></i>
            {proxy.$ts("查看")}
          </el-button>
        </div>
      );
    },
  });
};
onActivated(() => {
  gridRef?.search();
});

const isFlow = () => {
  return proxy.$route.path == "/Sys_WorkFlowTable";
};

//生成对象属性初始化后,操作明细表配置用到
const onInited = () => {
  gridRef.showCustom = false;
  const btn = gridRef.buttons.find((x) => {
    return x.name == "高级查询";
  });
  if (btn) {
    btn.hidden = true;
  }
  //框架初始化配置后
  //如果要配置明细表,在此方法操作
  //this.detailOptions.columns.forEach(column=>{ });
  if (isFlow()) {
    gridRef.height = gridRef.height - 45;
  } else {
    list.value.forEach((x) => {
      if (x.key === props.id) {
        gridRef.table.cnName = `${proxy.$ts("审批流程")}(${proxy.$ts(x.value)})`;
      }
    });
  }
};
const searchBefore = async (param) => {
  //界面查询前,可以给param.wheres添加查询参数
  //返回false，则不会执行查询
  param.value = isFlow() ? AuditStatus : props.id;
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
const modelOpenAfter = (row) => {
  //弹出框打开后方法,设置表单默认值,按钮操作等
};

//监听表单输入，做实时计算
//watch(() => editFormFields.字段,(newValue, oldValue) => {	})
//对外暴露数据
defineExpose({});
</script>
<style scoped lang="less">
.audit-content {
  padding: 8px 0px 6px 15px;
  margin-bottom: -10px;
  position: relative;
  z-index: 999;
  // border-bottom: 1px solid rgb(238, 238, 238);
  // display: flex;
  align-items: center;
  font-size: 13px;
}

.audit-content ::v-deep(.el-tabs__nav-wrap:after),
.audit-content ::v-deep(.el-tabs__active-bar) {
  height: 1px;
}

.audit-content ::v-deep(.el-tabs__header) {
  margin: 0px;
}

.audit-content ::v-deep(.el-tabs__item) {
  padding-right: 10px !important;
}

// .audit-content ::v-deep(.el-tabs__item:first-child) {
//   padding-left: 0 !important;
// }
</style>
