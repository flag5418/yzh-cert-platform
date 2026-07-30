<template>
  <div>
    <div class="ef-node-form">
      <div class="ef-node-pmenu-item">
        <div style="flex: 1">
          <span class="name"><i class="el-icon-news"></i>节点属性</span>
          <!-- <span @click="nameClick(1)" :class="{ active: index === 1 }" class="name">审批条件</span> -->
        </div>
        <!-- <div><el-button link size="small" type="primary" @click="save"><i class="el-icon-check"></i>
                        保存配置</el-button></div> -->
        <div class="link-btn">
          <el-button @click="showLink" link type="primary">
            <el-icon>
              <Position />
            </el-icon>{{ $ts('页面跳转') }}
            <span v-if="Array.isArray(formFields.LinkMenu) && formFields.LinkMenu.length" class="btn-dot"></span>
          </el-button>
          <el-button link type="primary" @click="showEdit">
            <el-icon>
              <Edit />
            </el-icon>{{ $ts('编辑表单') }}
            <span v-if="Array.isArray(formFields.Config) && formFields.Config.length" class="btn-dot"></span>
          </el-button>
        </div>
      </div>
      <div class="ef-node-form-body">
        <div class="form-info" :style="{
          'padding-left': $global.labelPosition == 'top' ? '0' : '10px',
        }">
          <vol-form ref="form" :select2-count="select2Count" labelPosition="top" :label-width="130" :loadKey="false"
            :formFields="formFields" :formRules="formRules">
          </vol-form>
        </div>

        <div v-show="showFilter">
          <node-filter :filters="formFields.filters" :tableName="tableName" ref="filter">
          </node-filter>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="jsx">
import { ref, reactive, onMounted, nextTick, getCurrentInstance } from "vue";
import nodeFilter from "./node_filter.vue";

const emit = defineEmits(["delNode", "setLineLabel", "repaintEverything"]);

const { proxy } = getCurrentInstance();

const form = ref();
const filter = ref();
const linkRef = ref();
const editRef = ref();

const linkInit = ref(false);
const select2Count = ref(2000);
const showFilter = ref(true);
const tableName = ref("");
const index = ref(1);
const visible = ref(true);
// node 或 line
const type = ref("node");
const line = ref({});
const data = ref({});

const formFields = ref({
  name: "",
  auditType: 1, //审核类型
  userId: [],
  roleId: [],
  deptId: [],
  auditRefuse: null, //审核未通过
  auditBack: null, //驳回
  auditMethod: 0, //审批方式(会签)
  sendMail: 0,
  filters: [],
  AllowUpload: 0,
  AttachType: null,
  AttachQty: null,
  LinkMenu: [],
  Config: [],
});

function nodeTypeChange(value) {
  formRules.value.forEach((options) => {
    options.forEach((option) => {
      if (
        (value == 4 || value == 5 || value == 6 || value == 7) &&
        ["userId", "roleId", "deptId"].indexOf(option.field) != -1
      ) {
        option.required = false;
        option.hidden = true;
        return;
      }
      option.required = true;
      if (
        option.field == "auditRefuse" ||
        option.field == "auditBack" ||
        option.field == "AttachQty" ||
        option.field == "auditMethod"
      ) {
        option.required = false;
      }
      if (option.field == "userId") {
        option.hidden = value != 1;
      } else if (option.field == "roleId") {
        option.hidden = value != 2;
      } else if (option.field == "deptId") {
        option.hidden = value != 3;
      }
    });
  });
}

const formRules = ref([
  [
    {
      title: "节点名称",
      field: "name",
      required: true,
      colSize: 12,
    },
  ],
  [
    {
      dataKey: "",
      title: "审批类型",
      required: true,
      hidden: false,
      field: "auditType",
      data: [
        { key: 1, value: "按用户审批" },
        { key: 2, value: "按角色审批" },
        { key: 3, value: "按部门审批" },
        { key: 4, value: "提交人上级部门审批" },
        { key: 5, value: "提交人上级角色审批" },
        // { key: 7, value: "提交人部门对应角色" },
        // { key: 6, value: "提交人自己" },
      ],
      type: "select",
      onChange: nodeTypeChange,
    },
    {
      dataKey: "users",
      hidden: false,
      title: "用户信息",
      required: true,
      field: "userId",
      data: [],
      type: "selectList",
    },
    {
      dataKey: "roles",
      hidden: true,
      title: "角色信息",
      required: true,
      field: "roleId",
      data: [],
      type: "selectList",
    },
    {
      dataKey: "dept",
      hidden: true,
      title: "部门信息",
      required: true,
      field: "deptId",
      data: [],
      type: "selectList",
    },
  ],
  [
    {
      dataKey: "",
      title: "审批未通过",
      required: false,
      field: "auditRefuse",
      hidden: false,
      data: [
        { key: 1, value: "返回上一节点" },
        { key: 2, value: "流程重新开始" },
        { key: 0, value: "流程结束" },
      ],
      type: "select",
      colSize: 6,
    },
    {
      dataKey: "",
      title: "审批驳回",
      required: false,
      hidden: false,
      field: "auditBack",
      data: [
        { key: 1, value: "返回上一节点" },
        { key: 2, value: "流程重新开始" },
        { key: 0, value: "流程结束" },
      ],
      type: "select",
      colSize: 6,
    },
  ],
  [
    {
      dataKey: "",
      title: "审核后发送邮件通知",
      required: false,
      hidden: false,
      field: "sendMail",
      data: [
        { key: 1, value: "是" },
        { key: 0, value: "否" },
      ],
      type: "switch",
    },
    {
      dataKey: "",
      title: "启用并签(或签)",
      required: false,
      hidden: false,
      field: "auditMethod",
      data: [
        { key: 1, value: "是" },
        { key: 0, value: "否" },
      ],
      type: "radio",
      extra: {
        render: () => {
          return (
            <el-popover
              placement="top-start"
              title="提示"
              width="200"
              trigger="hover"
              content="角色、用户、部门多选时：选择【是】多人同时审批通过后才能进入下一个节点，：选择【否】任意一个人审批通过即进入一节点"
            >
              {{
                reference: () => {
                  return (
                    <i
                      style="color:#3F51B5;font-size:12px;position:absolute;top:-15px;width:42px;right:-4px;"
                      class="el-icon-warning-outline"
                    >
                      说明
                    </i>
                  );
                },
              }}
            </el-popover>
          );
        },
      },
    },
  ],
]);

const showEdit = () => {
  editRef.value?.showEdit?.(node, tableName.value);
};

const linkSave = (linkVal) => {
  node.LinkMenu = linkVal;
};

const showLink = () => {
  linkInit.value = true;
  nextTick(() => {
    linkRef.value?.open?.(node.LinkMenu);
  });
};

const nameClick = (val) => {
  index.value = val;
};

// 对外：初始化节点
const nodeInit = (wfData, id, tbName = "") => {
  tableName.value = tbName || "";
  type.value = "node";
  data.value = wfData;
  const isCC = wfData.nodeList.some((x) => x.id == id && x.type == "cc");
  showFilter.value = !isCC;

  // 切换标题文案
  formRules.value.forEach((options) => {
    options.forEach((c) => {
      if (c.field == "auditType") {
        c.title = isCC ? "抄送对象类型" : "审批类型";
        c.data?.forEach?.((x) => {
          x.value = isCC
            ? x.value.replace("审批", "抄送")
            : x.value.replace("抄送", "审批");
        });
      }
    });
  });

  wfData.nodeList.forEach((n) => {
    if (n.id !== id) return;
    formRules.value.forEach((options) => {
      options.forEach((c) => {
        if (c.field != "name") {
          c.hidden = n.type == "start" || n.type == "end";
        }
      });
    });
    if (!n.filters) n.filters = [];

    if (!Array.isArray(n.userId)) {
      n.userId = [n.userId || ""]
        .filter((x) => x)
        .map((x) => x * 1);
    }
    if (!Array.isArray(n.roleId)) {
      n.roleId = [n.roleId || ""]
        .filter((x) => x)
        .map((x) => x * 1);
    }
    if (!Array.isArray(n.deptId)) {
      n.deptId = [n.deptId || ""]
        .filter((x) => x)
        .map((x) => x);
    }

    formFields.value = n;

    if (n.type != "start" && n.type != "end") {
      nodeTypeChange(n.auditType);
    }
  });

  // 抄送节点仅显示部分字段
  const showFields = ["name", "auditType", "userId", "roleId", "deptId"];
  if (isCC) {
    formRules.value.forEach((options) => {
      options.forEach((c) => {
        if (!showFields.includes(c.field)) {
          c.hidden = true;
        }
      });
    });
  }
};

// 对外：初始化连线
const lineInit = (l) => {
  type.value = "line";
  line.value = l;
};

const saveLine = () => {
  emit("setLineLabel", line.value.from, line.value.to, line.value.label);
};

const save = () => {
  const wfData = data.value;
  if (!wfData?.nodeList) return;
  wfData.nodeList.forEach((n) => {
    if (n.id === formFields.value.id) {
      n.name = formFields.value.name;
      n.left = formFields.value.left;
      n.top = formFields.value.top;
      n.ico = formFields.value.ico;
      n.state = formFields.value.state;
      n.stepValue = formFields.value.stepValue;
      emit("repaintEverything", node);
    }
  });
  proxy.$message.success("保存成功");
};

const initOptions = async () => {

  // 审核类型扩展
  if (
    proxy.$global.audit &&
    Array.isArray(proxy.$global.audit.auditType) &&
    proxy.$global.audit.auditType.length
  ) {
    formRules.value.forEach((options) => {
      options.forEach((option) => {
        if (option.field == "auditType") {
          option.data.push(...proxy.$global.audit.auditType);
        }
      });
    });
  }
  // 字典数据
  const result = await proxy.http.get("api/Sys_WorkFlow/getNodeDic");
  formRules.value.forEach((options) => {
    options.forEach((option) => {
      if (option.dataKey && !option.data.length) {
        let dic = result[option.dataKey] || [];
        if (dic.length > select2Count.value) {
          dic = dic.map((c) => {
            return { key: c.key, label: c.value, value: c.key };
          });
        }
        option.data = dic;
      }
    });
  });
}
initOptions();

defineExpose({
  nodeInit,
  lineInit,
  saveLine,
  save
});
</script>

<style lang="less" scoped>
@import "./index.css";

.el-node-form-tag {
  position: absolute;
  top: 50%;
  margin-left: -15px;
  height: 40px;
  width: 15px;
  background-color: #fbfbfb;
  border: 1px solid rgb(220, 227, 232);
  border-right: none;
  z-index: 0;
}

.btns {
  text-align: center;
  padding: 10px;

  buttton {
    flex: 1;
  }
}

.form-info {
  padding-left: 10px;

  :deep(.el-select__selection) {
    flex-wrap: nowrap !important;
  }
}

.link-btn {
  padding: 5px 0 0 8px;

  button {
    font-weight: 400;
    color: #3b59ff;
    font-size: 13px !important;
    position: relative;

    .btn-dot {
      position: absolute;
      padding: 3px;
      background: #ff5346;
      right: -3px;
      border-radius: 50%;
      top: -5px;
    }
  }
}

.ef-node-pmenu-item {
  display: flex;
  align-items: center;
}
</style>
