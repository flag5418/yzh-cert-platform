//明细表有些方法需要对外开放，内部使用可能需要变更名称，否则会出现原有代码无法访问方法

import { getGridTableRef, getDetailTableRef } from "./ViewGridRef.js";
import action from "./Action.js";
import { getUrl } from "./ViewGridProvider.jsx";

const checkRowsFalse = (rows) => {
  return rows === false;
};

export const initDetailOptions = (proxy, props, dataConfig) => {
  //明细表排序
  const detailOnSortEnd = (rows, newIndex, oldIndex, table) => {
    proxy.detailSortEnd.call(proxy, rows, newIndex, oldIndex, table);
    props.detailSortEnd(rows, newIndex, oldIndex, table);
    dataConfig.dyScript.detailSortEnd?.(rows, newIndex, oldIndex, table);
  };
  const detailAddRowBefore = (table, item) => {
    const rows = [];
    let row = proxy.detailAddRowBefore.call(proxy, table, item);
    if (checkRowsFalse(row)) {
      return false;
    }
    if (row) {
      if (Array.isArray(row)) {
        rows.push(...row);
      } else {
        rows.push(row);
      }
    }
    row = props.detailAddRowBefore(table, item);
    if (dataConfig.dyScript.detailAddRowBefore) {
      row = dataConfig.dyScript.detailAddRowBefore?.(table, item);
    }
    if (checkRowsFalse(row)) {
      return false;
    }
    if (row) {
      if (Array.isArray(row)) {
        rows.push(...row);
      } else {
        rows.push(row);
      }
    }
    return rows;
  };
  //添加行
  const addRow = () => {
    const tableRef = proxy.getTable();
    const rows = detailAddRowBefore();
    if (!rows) {
      return;
    }
    if (!rows.length) {
      rows.push({});
    }
    tableRef.addRow(rows);
    tableRef.edit.rowIndex = -1;
    updateTableSummaryTotal(proxy, props, dataConfig);
  };


  //明细表删除
  const delRow = async (table, item, index, isSubDetail) => {
    //一对多明细的添加行

    let rows;
    if (isSubDetail) {
      rows = proxy.getTable(table).getSelected();
    } else if (typeof table == "string" && dataConfig.isMultiple.value) {
      rows = proxy.getTable(table).getSelected();
    } else {
      rows = proxy.getTable().getSelected();
    }

    if (!rows || rows.length == 0) {
      return proxy.$error(proxy.$ts("请选择要删除的行!"));
    }
    if (!(await proxy.delDetailRow.call(proxy, rows, table))) {
      return;
    }
    if (!(await props.delDetailRow(rows, table))) {
      return;
    }
    if (!(await proxy.delRowBefore.call(proxy, rows, table, item, index))) {
      return;
    }
    if (!(await props.delRowBefore(rows, table, item, index))) {
      return;
    }
    if ((await dataConfig.dyScript.delRowBefore?.(rows, table, item, index) === false)) {
      return;
    }
    let tigger = false;
    proxy
      .$confirm(
        proxy.$ts(proxy.$global.detailDelMsg || "确认要删除选择的数据吗?"),
        proxy.$ts("警告"),
        {
          confirmButtonText: proxy.$ts("确定"),
          cancelButtonText: proxy.$ts("取消"),
          type: "warning",
          center: true,
        },
      )
      .then(() => {
        if (tigger) return;
        tigger = true;
  
        rows = proxy.getTable().delRow();

        if (!delRowAfter(proxy, props, rows, table, item, index, dataConfig)) {
          return;
        }
        let key = dataConfig.detailOptions.key;
        //记录删除的行数据
        rows.forEach((x) => {
          if (x.hasOwnProperty(key) && x[key]) {
            dataConfig.detailOptions.delKeys.push(x[key]);
          }
        });
        updateTableSummaryTotal(proxy, props, dataConfig, rows.length);
      });
  };

  //刷新明细表
  const refreshRow = (table) => {
    reloadDetail(table);
  };
  //刷新明细表
  const reloadDetail = (table) => {
    resetDetailTable(
      proxy,
      props,
      dataConfig,
      null,
      dataConfig.currentAction.value,
      table,
    );
  };

  //一对明细表tabs点击事件
  const detailTabsOnClick = (table) => {
    let obj = props.details.find((x) => {
      return x.table == table;
    });
    props.detailTabsClick(table);
    proxy.detailTabsClick.call(proxy, table);
    //设置三级明细表选中
    if (obj) {
      const subDetailsRef = proxy.$refs.subDetailsRef;
      if (subDetailsRef) {
        if (obj.detail) {
          subDetailsRef.setTable(obj.detail.table);
        } else {
          subDetailsRef.setTable("");
        }
      }
      return;
    }
    obj = dataConfig.subDetails.value.find((x) => {
      return x.table == table;
    });
  };

  //明细表加载前方法
  const loadDetailTableBefore = async (param, callBack, table, item) => {
    //加载明细表数据之前,需要设定查询的主表的ID
    //每次只要加载明细表格数据就重置删除明细的值
    const detailOptions = dataConfig.detailOptions;
    if (detailOptions.delKeys.length > 0) {
      detailOptions.delKeys = [];
    }
    const currentRow = dataConfig.currentRow.value;
    let key = props.table.key;
    if (currentRow && currentRow.hasOwnProperty(key)) {
      param.value = currentRow[key];
    }
    if (dataConfig.isMultiple.value) {
      if (!param.tableName) {
        param.value = currentRow[props.table.key];
        param.tableName = table;
      }
    } else { //主从表
      if (props.generic) {
        param.tableName = props.detail.table;
      }
    }

    param.wheres = [{ name: props.table.key, value: param.value }]
    //明细查询前
    //新建时禁止加载明细
    if (dataConfig.currentAction.value == action.ADD && !param.isCopyClick) {
      await callBack(false);
      return false;
    }
    let status = await proxy.searchDetailBefore.call(proxy, param, table, item);
    if (status) {
      status = await props.searchDetailBefore(param, table, item);
    }
    callBack(status);
    return status;
  };
  //明细查询后
  const loadDetailTableAfter = async (data, callBack, table, item) => {
    let status = await proxy.searchDetailAfter.call(proxy, data, table, item);
    if (status) {
      status = await props.searchDetailAfter(data, table, item);
    }
    if (dataConfig.currentAction.value == action.ADD) {
      data.forEach((row) => {
        row[props.table.key] = undefined;
        if (!item) {
          row[dataConfig.detailOptions.key] = undefined;
        } else {
          row[item.key] = undefined;
        }
      });
    }
    callBack(status);
  };
  //明细表复选框选择事件
  const detailRowOnChange = (row, item) => {
    //获取三级明细子表
    if (!item || !item.detail) {
      return;
    }
    proxy.detailRowChange.call(proxy, row, item);
    props.detailRowChange(row, item);

  };

  //明细表行点击事件
  const detailRowOnClick = ({ row, column, event, item }) => {
    proxy.detailRowClick.call(proxy, { row, column, event, item });
    props.detailRowClick({ row, column, event, item });
    dataConfig && dataConfig.dyScript.detailRowClick?.({ row, column, event, item });
  };
  //明细表是否可以选中
  const detailOnSelectable = (row, index, item) => {
    if (!proxy.detailSelectable.call(proxy, row, index, item)) {
      return false;
    }
    if (dataConfig && dataConfig.dyScript.detailSelectable) {
      return dataConfig.dyScript.detailSelectable(row, index, item)
    }
    return props.detailSelectable(row, index, item);
  };

  return {
    refreshRow,
    reloadDetail,
    addRow,
    delRow,
    loadDetailTableBefore,
    loadDetailTableAfter,
    detailRowOnChange,
    detailRowOnClick,
    detailOnSelectable,
    detailOnSortEnd,
    detailTabsOnClick
  };
};
//重置明细表
export const resetDetailTable = (
  proxy,
  props,
  dataConfig,
  row,
  isAdd,
  table,
) => {
  //刷新指定明细表(编辑时有效)
  if (table) {
    const tableRef = getDetailTableRef(proxy, props, table);
    tableRef.reset();
    detailTableLoad(props, dataConfig, row, tableRef);
    return;
  }
  //新建或编辑时重置明细表
  const detailOptions = dataConfig.detailOptions;

  //编辑和查看明细时重置从表数据
  if (!detailOptions.columns || detailOptions.columns.length == 0) {
    return;
  }

  const isCopyClick = dataConfig.isCopyClick.value;
  proxy.$nextTick(() => {
    const detailRef = getDetailTableRef(proxy, props);
    if (detailRef) {
      detailRef.reset();
      //$refs.detail.load(query);
      detailTableLoad(props, dataConfig, row, detailRef, null, isCopyClick);
    }
  });
};

const detailTableLoad = (
  props,
  dataConfig,
  row,
  refTable,
  table,
  isCopyClick,
) => {
  //一对多明细表加载数据
  if (refTable) {
    let query = {
      value: row
        ? row[props.table.key]
        : dataConfig.currentRow.value[props.table.key],
      tableName: table,
      isCopyClick: isCopyClick,
    };
    refTable.load(query);
  }
};

const updateTableSummaryTotal = (proxy, props, dataConfig, delTotal) => {

  const detailRef = proxy.$refs.detail;
  if (!detailRef) return;
  //删除或新增行时重新设置显示的总行数
  //第1页后的数据删除不重置分页总数，否则不能删除数据
  if (detailRef.paginations.page <= 1) {
    if (delTotal && detailRef.paginations.total - delTotal >= 0) {
      detailRef.paginations.total = detailRef.paginations.total - delTotal;
    } else {
      detailRef.paginations.total = detailRef.rowData.length;
    }
  }
  //重新设置合计
  if (detailRef.summary) {
    detailRef.columns.forEach((column) => {
      if (column.summary) {
        detailRef.getInputSummaries(null, null, null, column);
      }
    });
  }
};

const getCurrentDetailSelectRows = (proxy, props, dataConfig, table) => {
  let rows = proxy.getTableRef(table).getSelected();
  if (!rows.length) {
    proxy.$error(
      proxy.$ts("请选中明细表数据") +
      ":【" +
      proxy.$ts(getTableName(props, dataConfig, table)) +
      "】",
    );
    return null;
  }
  return rows;
};
//获取二级或者三级明细表中文名称
const getTableName = (props, dataConfig, table) => {

  let ops = props.details.find((x) => {
    return x.table == table;
  });
  if (ops) {
    return ops.cnName;
  }

  return dataConfig.subDetails.value.find((x) => {
    return x.table == table;
  }).cnName;
};

const delRowAfter = (proxy, props, rows, table, item, index, dataConfig) => {
  if (!proxy.delRowAfter.call(proxy, rows, table, item, index)) {
    return;
  }
  if (!props.delRowAfter(rows, table, item, index)) {
    return;
  }
  return true;
};
