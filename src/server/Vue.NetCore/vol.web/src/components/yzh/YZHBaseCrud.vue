<!--
 * YZHBaseCrud.vue - YZH 基础 CRUD 窗体
 * 
 * 设计目标：
 * 1. 统一认证平台所有 CRUD 页面的基础行为
 * 2. 封装增量更新逻辑（新增/编辑/删除后不刷新整个列表）
 * 3. 统一行内操作按钮（编辑/删除在操作列，不在顶部工具栏）
 * 4. 标准化表单布局（统一 2 列布局，隐藏字段用 hidden 类型）
 * 5. 完整业务生命周期控制（钩子函数）
 * 
 * 使用方式：
 * <YZHBaseCrud
 *   :options="viewOptions"
 *   :module-name="'ISOStandard'"
 *   :description="'ISO 标准管理：管理各认证机构可开展认证的ISO标准'"
 *   @on-init="handleInit"
 * />
 * 
 * @author CertPlatform
 * @date 2026-07-31
 -->

<template>
  <view-grid
    ref="gridRef"
    :columns="finalColumns"
    :detail="detail"
    :details="details"
    :editFormFields="editFormFields"
    :editFormOptions="editFormOptions"
    :searchFormFields="searchFormFields"
    :searchFormOptions="searchFormOptions"
    :table="table"
    :extend="extend"
    :onInit="handleInit"
    :onInited="handleInited"
    :searchBefore="handleSearchBefore"
    :addBefore="handleAddBefore"
    :addAfter="handleAddAfter"
    :updateBefore="handleUpdateBefore"
    :updateAfter="handleUpdateAfter"
    :delBefore="handleDelBefore"
    :delAfter="handleDelAfter"
    :rowClick="handleRowClick"
    :modelOpenBefore="handleModelOpenBefore"
    :modelOpenAfter="handleModelOpenAfter"
  >
    <!-- 顶部描述信息 -->
    <template #gridHeader>
      <el-alert
        v-if="description"
        :title="description"
        type="info"
        :closable="false"
        show-icon
        style="margin-bottom: 10px"
      />
    </template>

    <!-- 左侧扩展插槽（可选） -->
    <template #btnLeft>
      <slot name="btnLeft"></slot>
    </template>
  </view-grid>
</template>

<script setup lang="jsx">
import { ref, reactive, computed, getCurrentInstance, watch, onMounted } from "vue";
import viewGridExtend from "./YZHBaseCrud.jsx";

const props = defineProps({
  /** options.js 导出的配置对象（必须） */
  options: {
    type: Object,
    required: true,
  },
  /** 模块名称，用于日志和提示 */
  moduleName: {
    type: String,
    default: "未命名模块",
  },
  /** 页面描述，显示在顶部 */
  description: {
    type: String,
    default: "",
  },
  /** 是否启用行内操作列（默认 true） */
  enableRowActions: {
    type: Boolean,
    default: true,
  },
  /** 行内操作列宽度 */
  rowActionWidth: {
    type: Number,
    default: 150,
  },
  /** 是否隐藏顶部编辑/删除按钮（默认 true，强制使用行内操作） */
  hideTopEditDelButtons: {
    type: Boolean,
    default: true,
  },
});

const emit = defineEmits([
  "on-init",
  "on-inited",
  "add-before",
  "add-after",
  "update-before",
  "update-after",
  "del-before",
  "del-after",
  "row-click",
]);

const gridRef = ref(null);
const { proxy } = getCurrentInstance();
let grid = null; // ViewGrid 内部引用

// 从 options 解构配置
const {
  table,
  editFormFields,
  editFormOptions,
  searchFormFields,
  searchFormOptions,
  columns: originalColumns,
  detail,
  details,
} = reactive(props.options());

// 扩展引用（兼容旧版 .jsx）
const extend = viewGridExtend;

/**
 * 最终列配置 - 自动添加操作列
 */
const finalColumns = computed(() => {
  let cols = [...originalColumns];

  // 如果启用行内操作，添加操作列
  if (props.enableRowEdit || props.enableRowDel) {
    const actionCol = {
      field: "_yzh_actions",
      title: "操作",
      width: props.rowActionWidth,
      fixed: "right",
      align: "center",
      render: (h, { row, index }) => {
        const actions = [];

        // 编辑按钮
        if (props.enableRowEdit !== false) {
          actions.push(
            h(
              "el-button",
              {
                size: "small",
                type: "primary",
                link: true,
                onClick: (e) => {
                  e.stopPropagation();
                  handleRowEdit(row, index);
                },
              },
              () => "编辑"
            )
          );
        }

        // 删除按钮
        if (props.enableRowDel !== false) {
          actions.push(
            h(
              "el-button",
              {
                size: "small",
                type: "danger",
                link: true,
                onClick: (e) => {
                  e.stopPropagation();
                  handleRowDel(row, index);
                },
              },
              () => "删除"
            )
          );
        }

        return h("div", { class: "yzh-row-actions" }, actions);
      },
    };
    cols.push(actionCol);
  }

  return cols;
});

// ==================== 业务生命周期钩子 ====================

/**
 * ViewGrid 初始化时调用
 * 在此可以修改 grid 属性、配置查询字段等
 */
const handleInit = async ($vm) => {
  grid = $vm;

  // 默认固定搜索栏
  grid.setFixedSearchForm(true);

  // 隐藏顶部编辑/删除按钮（如果配置要求）
  if (props.hideTopEditDelButtons) {
    // 通过配置隐藏框架默认的编辑/删除按钮
    grid.hideEditButton = true;
    grid.hideDelButton = true;
  }

  // 触发外部回调
  if (emit["on-init"]) {
    emit("on-init", $vm);
  }
};

/**
 * ViewGrid 初始化完成后调用
 * 可用于操作明细表配置等
 */
const handleInited = async () => {
  if (emit["on-inited"]) {
    emit("on-inited");
  }
};

/**
 * 查询前拦截
 * @param {Object} param 查询参数
 * @returns {boolean} false 阻止查询
 */
const handleSearchBefore = async (param) => {
  return true;
};

/**
 * 新增保存前拦截
 * @param {Object} formData 表单数据
 * @returns {boolean} false 阻止保存
 */
const handleAddBefore = async (formData) => {
  if (emit["add-before"]) {
    return await emit("add-before", formData);
  }
  return true;
};

/**
 * 新增保存后处理 - 增量更新表格
 * @param {Object} result 后端返回结果
 * @param {Object} formData 表单数据
 * @returns {boolean} false 阻止后续处理
 */
const handleAddAfter = async (result, formData) => {
  // 增量更新：新增成功后将新数据添加到表格末尾
  if (result?.status && result?.data) {
    let newRow = result.data;
    if (typeof newRow === "string") {
      try {
        newRow = JSON.parse(newRow);
      } catch (e) {
        console.error("解析新增返回数据失败:", e);
      }
    }
    if (newRow) {
      // 使用 Vol Table 的 load 方法进行增量加载
      // isAdd=true 表示新增模式，框架会自动处理
      addRowToTable(newRow);
    }
  }

  if (emit["add-after"]) {
    return await emit("add-after", result, formData);
  }
  return true;
};

/**
 * 编辑保存前拦截
 * @param {Object} formData 表单数据
 * @returns {boolean} false 阻止保存
 */
const handleUpdateBefore = async (formData) => {
  if (emit["update-before"]) {
    return await emit("update-before", formData);
  }
  return true;
};

/**
 * 编辑保存后处理 - 增量更新当前行
 * @param {Object} result 后端返回结果
 * @param {Object} formData 表单数据
 * @returns {boolean} false 阻止后续处理
 */
const handleUpdateAfter = async (result, formData) => {
  // 增量更新：编辑成功后只更新当前行
  if (result?.status && result?.data) {
    let updatedRow = result.data;
    if (typeof updatedRow === "string") {
      try {
        updatedRow = JSON.parse(updatedRow);
      } catch (e) {
        console.error("解析编辑返回数据失败:", e);
      }
    }
    if (updatedRow) {
      updateRowInTable(updatedRow);
    }
  }

  if (emit["update-after"]) {
    return await emit("update-after", result, formData);
  }
  return true;
};

/**
 * 删除前拦截
 * @param {Array} delKeys 要删除的主键数组
 * @param {Array} rows 要删除的行数组
 * @returns {boolean} false 阻止删除
 */
const handleDelBefore = async (delKeys, rows) => {
  if (emit["del-before"]) {
    return await emit("del-before", delKeys, rows);
  }
  return true;
};

/**
 * 删除后处理 - 增量移除行
 * @param {Object} result 后端返回结果
 * @param {Array} rows 被删除的行数组
 * @returns {boolean} false 阻止后续处理
 */
const handleDelAfter = async (result, rows) => {
  // 增量更新：删除成功后从表格中移除对应行
  if (result?.status && rows?.length > 0) {
    removeRowsFromTable(rows);
  }

  if (emit["del-after"]) {
    return await emit("del-after", result, rows);
  }
  return true;
};

/**
 * 行点击事件
 */
const handleRowClick = ({ row, column, event }) => {
  if (emit["row-click"]) {
    emit("row-click", { row, column, event });
  }
};

/**
 * 弹出框打开前
 */
const handleModelOpenBefore = async (row) => {
  return true;
};

/**
 * 弹出框打开后
 */
const handleModelOpenAfter = (row) => {
  // 可以在此设置表单默认值等
};

// ==================== 增量更新方法 ====================

/**
 * 新增行到表格末尾（不刷新整个列表）
 * @param {Object} newRow 新增的数据行
 */
const addRowToTable = (newRow) => {
  if (!grid) return;
  try {
    const tableRef = grid.getTable(true);
    if (tableRef && tableRef.rowData) {
      // 添加到末尾
      tableRef.rowData.push(newRow);
      // 更新表格显示（不重新请求后端）
      tableRef.load(null, true);
    }
  } catch (e) {
    console.warn("增量添加行失败，将使用刷新:", e);
    grid.search();
  }
};

/**
 * 更新表格中的当前行（不刷新整个列表）
 * @param {Object} updatedRow 更新后的数据行
 */
const updateRowInTable = (updatedRow) => {
  if (!grid) return;
  try {
    const tableRef = grid.getTable(true);
    if (tableRef && tableRef.rowData) {
      const keyField = table.key || "Id";
      const index = tableRef.rowData.findIndex(
        (r) => r[keyField] == updatedRow[keyField]
      );
      if (index !== -1) {
        // 合并更新当前行
        Object.assign(tableRef.rowData[index], updatedRow);
        // 更新表格显示
        tableRef.load(null, false);
      }
    }
  } catch (e) {
    console.warn("增量更新行失败，将使用刷新:", e);
    grid.search();
  }
};

/**
 * 从表格中移除指定行（不刷新整个列表）
 * @param {Array} rows 要移除的行数组
 */
const removeRowsFromTable = (rows) => {
  if (!grid) return;
  try {
    const tableRef = grid.getTable(true);
    if (tableRef && tableRef.rowData) {
      const keyField = table.key || "Id";
      const delKeys = rows.map((r) => r[keyField]);
      // 过滤掉被删除的行
      tableRef.rowData = tableRef.rowData.filter(
        (r) => !delKeys.includes(r[keyField])
      );
      // 更新表格显示
      tableRef.load(null, false);
    }
  } catch (e) {
    console.warn("增量移除行失败，将使用刷新:", e);
    grid.search();
  }
};

// ==================== 行内操作方法 ====================

/**
 * 触发行内编辑
 */
const handleRowEdit = (row, index) => {
  if (!grid) return;
  // 调用 ViewGrid 的编辑方法，传入当前行
  // 内部会触发 onEdit 流程
  grid.edit([row]);
};

/**
 * 触发行内删除
 */
const handleRowDel = (row, index) => {
  if (!grid) return;
  // 调用 ViewGrid 的删除方法，传入当前行
  // 内部会触发 onDelete 流程
  grid.del([row]);
};

// ==================== 对外暴露 ====================

defineExpose({
  /** 获取 grid 引用 */
  getGrid: () => grid,
  /** 手动刷新 */
  refresh: () => grid?.search(),
  /** 获取选中行 */
  getSelectedRows: () => grid?.getSelectRows(),
  /** 获取表格数据 */
  getTableData: () => grid?.getTable(true)?.rowData,
  /** 增量添加行 */
  addRow: addRowToTable,
  /** 增量更新行 */
  updateRow: updateRowInTable,
  /** 增量移除行 */
  removeRows: removeRowsFromTable,
});
</script>

<style lang="less" scoped>
.yzh-row-actions {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 4px;

  .el-button {
    margin: 0 !important;
  }
}

.el-alert {
  border-radius: 4px;
}
</style>
