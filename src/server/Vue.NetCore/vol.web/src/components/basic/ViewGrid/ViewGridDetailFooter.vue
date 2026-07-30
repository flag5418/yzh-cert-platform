<template>
  <div class="detail-content" :class="{ 'one-table-footer': tabsData.length == 1 }">
    <div v-if="tabsData.length == 1" style="margin-bottom: 5px;">
      <vol-title icon="Edit" :title="tabsData[0].cnName"></vol-title>
    </div>
    <el-tabs type="border-card" v-model="activeName" class="details-tabs" @tab-click="tabsClick">
      <el-tab-pane :lazy="false" v-for="item in tabsData" :key="item.table" :label="$ts(item.cnName)"
        :name="item.table">
        <template #label>
          <i style="margin-right: 2px" class="el-icon-date"></i>
          <span>{{ $ts(item.cnName) }}</span>
        </template>
        <vol-table :ref="(el) => setRefMap(el, item)" @loadBefore="
          (param, callBack) => {
            loadBefore(param, callBack, item);
          }
        " :url="item.url" :columns="item.columns" :height="tableHeight" :pagination-hide="false"
          :defaultLoadPage="false" :column-index="true" :ck="false"></vol-table>
      </el-tab-pane>
    </el-tabs>
  </div>
</template>
<script setup>
import { ref, reactive, getCurrentInstance, watch, onMounted } from "vue";
const props = defineProps({
  generic: {
    type: Boolean,
    default: false
  },
  height: {
    type: Number,
    default: 200
  },
  table: {
    //表的配置信息：主键、排序等
    type: Object,
    default: () => {
      return { sortName: '', key: '', newTabEdit: false }
    }
  },
  detail: {
    //从表明细配置
    type: Object,
    default: () => {
      return {
        columns: [], //从表列
        sortName: '' //从表排序字段
      }
    }
  },
  details: {
    //从表明细配置
    type: Array,
    default: () => {
      return []
    }
  },
  asyncApi: {
    type: Boolean,
    default: false
  }
})

const tableHeight = ref(100);
const tabsData = ref([]);
const initOptions = () => {
  const url = props.generic ? `api/generic/getDetailPage` : `api/${props.table.name}/getDetailPage${props.asyncApi?'Async':''}`;

  if (props.details?.length) {
    tabsData.value = props.details.map((x) => {
      x = { ...x };
      //配置自动加载明细表url接口
      x.url = url;
      x.columns = x.columns.map((col) => {
        col = { ...col };
        col.require = false;
        col.edit = null;
        return col;
      });
      return x;
    });
  } else if (props.detail?.columns?.length) {
    tabsData.value.push({
      table: props.detail.table,
      cnName: props.detail.cnName,
      url: url,
      columns: props.detail.columns.map((col) => {
        col = { ...col };
        col.require = false;
        col.edit = null;
        return col;
      })
    })
  }
}

initOptions();

tableHeight.value = props.height - (tabsData.value.length > 1 ? 92 : 77)

const activeName = ref();
if (tabsData.value.length) {
  activeName.value = tabsData.value[0].table;
}
const detailRefs = ref([]);
const setRefMap = (el, item) => {
  detailRefs.value.push(el);
};

const supplierRow = ref({});
//加载明细表数据
const loadViewGridFooterDetail = (row) => {
  supplierRow.value = row;
  detailRefs.value.forEach((tableRef) => {
    tableRef.load();
  });
};
//设置明细表查询条件
const loadBefore = (param, callBack, item) => {
  param.value = supplierRow.value[props.table.key];
  if (!param.value) {
    callBack(false);
    return
  }
  param.wheres = [{ name: props.table.key, value: param.value }]
  //指定要加载的明细表
  param.tableName = item.table;
  callBack(true);
};

const tabsClick = (data) => { };

defineExpose({ loadViewGridFooterDetail });
</script>

<style scoped lang="less">
.detail-content {
  padding: 15px;

  :deep(.el-tabs) {
    border: 0;
    border-top: 1px solid #dbdbdb;

    .el-tabs__nav-scroll {
      border-left: 1px solid #dbdbdb;
      border-right: 1px solid #dbdbdb;
    }

    .el-tabs__content {
      padding: 0;
      padding-top: 0px;
    }
  }

  :deep(.el-table__empty-block) {
    height: 100px !important;
  }
}

.one-table-footer {
  :deep(.el-tabs) {
    border-top: 0 !important;

    .el-tabs__header {
      display: none !important;
    }
  }
}
</style>
