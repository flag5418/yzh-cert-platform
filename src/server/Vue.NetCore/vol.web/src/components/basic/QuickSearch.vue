<template>
  <div class="qk-search" :class="[formLen == 1 ? 'form-1' : '', hasDate ? 'date-form' : 'qk-form']">
    <vol-form ref="searchForm" labelPosition="left" :load-key="false" :label-width="labelWidth" :formRules="formOptions"
      :eventNext="false"  :formFields="searchFormFields" :select2Count="select2Count">
    </vol-form>
  </div>
</template>
<script setup>
import { ref } from 'vue';

// 定义组件props
const props = defineProps({
  select2Count: {
    type: Number,
    default: 2000,
  },
  labelWidth: {
    type: Number,
    default: 100,
  },
  searchFormOptions: {
    type: Array,
    default: () => [],
  },
  searchFormFields: {
    type: Object,
    default: () => ({}),
  },
  tiggerPress: {
    type: Function,
    default: () => { },
  },
  queryFields: {
    // 快捷查询的字段
    type: Array,
    default: () => [],
  },
});

// 组件状态
const formLen = ref(1);
const formOptions = ref([]);
const hasDate = ref(false);

// 初始化表单方法
const initForm = () => {
  formOptions.value.splice(0);
  let ops = [];

  if (props.queryFields.length) {
    for (let index = 0; index < props.queryFields.length; index++) {
      const field = props.queryFields[index];
      props.searchFormOptions.forEach((options) => {
        options.forEach((x) => {
          if (field === x.field) {
            ops.push(x);
          }
        });
      });
    }
    formLen.value = ops.length;
  }

  if (!ops.length && props.searchFormOptions.length) {
    ops.push(props.searchFormOptions[0][0]);
  }

  ops.forEach((x) => {
    if (!x.onKeyPress) {
      x.onKeyPress = ($event) => {
        if ($event && $event.keyCode === 13) {
          props.tiggerPress($event);
        }
      };
    }
  });

  formOptions.value.push(ops);

  hasDate.value = ops.some((x) => {
    return (x.type === "date" || x.type === "datetime") && x.range === true;
  });
};
initForm();
defineExpose({
  initForm
})
</script>

<style lang="less" scoped>
.qk-search ::v-deep(.el-form-item__label) {
  // display: none;
  width: auto !important;
  margin-left: 10px;
}

.form-1::v-deep(.el-form-item__label) {
  display: none;
}

.qk-search {
  overflow: hidden;
  height: 38px;
  position: relative;
  // top: -4px;
  // right: -21px;
}

.qk-search ::v-deep(.vol-form-item) {
  margin-top: 0 !important;
  .el-form-item {
    width: auto !important;
   margin-bottom: 0 !important;
    .el-form-item__content {
      width: 130px;
    }

    .el-cascader {
      height: 29px;
    }

    .v-date-range {
      max-width: 240px;
      margin-top: 1px;
    }
  }

  .vol-form-item-datetime,
  .vol-form-item-date,
  .vol-form-item-month,
  .vol-form-item-radio,
  .vol-form-item-checkbox {
    width: auto !important;

    .el-form-item__content {
      width: auto;
    }
  }
}

.qk-search ::v-deep(.vol-form-item-select) {

  .el-form-item__content {}

  // .date-form ::v-deep(),
  // .qk-search :v-deep(.vol-form-item-month .v-date-range) {

}
</style>
