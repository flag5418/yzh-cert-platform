<template>
  <div class="coder-v2-table">
    <!-- <VolHeader icon="md-podium" style="border-bottom: 0" text="表结构">
      <template #content>
        <div style="color: red; font-size: 13px">
          数据库表结构发生变化时请点【同步表结构】
        </div>
      </template>

<div class="action">
  <span @click="$emit('delRow')" class="ivu-icon ivu-icon-md-close">删除行数据</span>
  <span @click="$emit('syncTable')" class="ivu-icon ivu-icon-md-sync">同步表结构</span>
</div>
</VolHeader> -->
    <div class="fx table-structure-toolbar">
      <el-radio-group
        v-model="tableStructureTab"
        size="small"
        text-color="#fff"
        fill="#6c6cff"
        class="structure-radio-group"
        @change="applyTableStructureColumnVisibility"
      >
        <el-radio-button value="structure">表结构信息</el-radio-button>
        <el-radio-button value="form">查询、新建、编辑表单</el-radio-button>
        <el-radio-button value="app">app列</el-radio-button>
        <el-radio-button value="advanced">高级属性</el-radio-button>
      </el-radio-group>
      <div class="desc">
        <span style="color: #0247de; font-size: 13px" class="el-icon-info">
          数据库修改表字段后,点击[同步表结构]->生成model、页面
        </span>
      </div>
      <div class="btns">
        <!-- <el-button link @click="$emit('delRow')"><i class="el-icon-delete"></i> 删除行数据</el-button> -->
        <el-button link @click="$emit('syncTable')">
          <i class="el-icon-refresh"></i>同步表结构</el-button
        >
      </div>
    </div>
    <div class="grid-container">
      <vol-table
        :sortable="true"
        ref="tableRef"
        :paginationHide="true"
        @onSortEnd="(rows) => $emit('onSortEnd', rows)"
        :tableData="tableData"
        :column-index="false"
        :ck="false"
        :height="height"
        :columns="columns"
        :color="false"
      ></vol-table>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, nextTick } from "vue";

defineOptions({ name: "coderV2Table" });

const props = defineProps({
  tableData: {
    type: Array,
    default: () => [],
  },
  columns: {
    type: Array,
    required: true,
  },
});
const tableStructureTab = ref("structure");
defineEmits(["delRow", "syncTable", "onSortEnd"]);
const height = ref(document.documentElement.clientHeight - 420);
if (height.value < 200) {
  height.value = 200;
}

const showZoom = ref(document.documentElement.clientWidth < 1550);
const FIXED_FIELDS = new Set(["columnCnName", "columnName", "coderRowIndex"]);

const TAB_VISIBLE_FIELDS = {
  /** 表结构信息：主键～可为空 */
  structure: new Set([
    "isKey",
    "isDisplay",
    "isImage",
    "columnWidth",
    "orderNo",
    "sortable",
    "maxlength",
    "columnType",
    "isNull",
  ]),
  /** 新建、编辑表单：查询行～编辑宽度 */
  form: new Set([
    "searchRowNo",
    "searchColNo",
    "searchType",
    "editRowNo",
    "editColNo",
    "editType",
    "dropNo",
    "isReadDataset",
    // "placeholder",
    "colSize",
  ]),
  /** app：app列～数据源 */
  app: new Set([
    "enable",
    "searchRowNo",
    "searchColNo",
    "searchType",
    "editRowNo",
    "editColNo",
    "editType",
    "dropNo",
  ]),
  /** 高级属性：表头筛选～列计算，另含是否显示、列宽度 */
  advanced: new Set([
    "isDisplay",
    "columnWidth",
    "headerFilter",
    "summaryType",
    "isUnique",
    "textAlign",
    "showOverflowTooltip",
    "fixedColumn",
    "calcColumn",
  ]),
};

const tableRef = ref(null);

const delRow = () => {
  tableRef.value?.delRow();
};

const flushTableLayout = () => {
  nextTick(() => {
    nextTick(() => {
      const elTable = tableRef.value?.getTable?.();
      if (!elTable) {
        return;
      }
      // elTable.setScrollLeft?.(0);
      elTable.doLayout?.();
    });
  });
};

const applyTableStructureColumnVisibility = () => {
  const list = props.columns;
  if (!list?.length) {
    return;
  }
  const tab = tableStructureTab.value;
  const visible = TAB_VISIBLE_FIELDS[tab] ?? TAB_VISIBLE_FIELDS.structure;
  list.forEach((col) => {
    const field = col.field;
    if (!field) {
      return;
    }
    col.hidden = !(FIXED_FIELDS.has(field) || visible.has(field));
  });
  flushTableLayout();
};

onMounted(() => {
  applyTableStructureColumnVisibility();
});

defineExpose({
  delRow,
});
</script>

<style scoped>
.coder-v2-table {
  background: #fff;
  border: 1px solid #efefef;
  border-radius: 5px;
  box-sizing: border-box;
}

.action {
  text-align: right;
  line-height: 33px;
  padding-right: 26px;
}

.action i {
  top: 0px;
  position: relative;
}

.action > span {
  padding: 0px 6px;
  font-size: 12px;
  letter-spacing: 1px;
  color: #5a5f5e;
}

.action > span:hover {
  cursor: pointer;
  color: black;
}

.table-structure-toolbar {
  align-items: center;
  flex-wrap: wrap;
  display: flex;
  padding: 5px;
  gap: 8px 12px;

  .desc {
    flex: 1;
  }

  :deep(.el-radio-button__inner) {
    padding: 7px 12px;
  }
}

/* 表内布尔勾选：未选中边框更清晰，hover/focus 符合现代中性色 + 轻强调色 */
.grid-container {
  --coder-checkbox-border: #94a3b8;
  --coder-checkbox-border-hover: #64748b;
  --coder-checkbox-focus: #6366f1;
}

.grid-container :deep(.el-checkbox .el-checkbox__inner) {
  border-width: 1px;
  border-style: solid;
  border-color: var(--coder-checkbox-border);
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}

.grid-container :deep(.el-checkbox:not(.is-disabled):hover .el-checkbox__inner) {
  border-color: var(--coder-checkbox-border-hover);
}

.grid-container :deep(.el-checkbox__input.is-focus:not(.is-checked) .el-checkbox__inner) {
  border-color: var(--coder-checkbox-focus);
  box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.18);
}

.grid-container :deep(.el-checkbox__input.is-checked .el-checkbox__inner),
.grid-container :deep(.el-checkbox__input.is-indeterminate .el-checkbox__inner) {
  border-color: var(--el-color-primary, #409eff);
  box-shadow: none;
}
.grid-container :deep(.el-table__body-wrapper .el-table__cell){
  height: 37px !important;
}
</style>
