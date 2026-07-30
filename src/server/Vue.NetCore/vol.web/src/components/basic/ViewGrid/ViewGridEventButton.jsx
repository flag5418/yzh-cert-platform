import {
  getUrl,
  initBox,
  resetAdd,
  resetEditForm,
  initOntinueAdd,
  modelOpenProcess,
  getRemoteFormDefaultKeyValue,
} from "./ViewGridProvider.jsx";
import { initImportOptions } from "./ViewGridInitButtonsAuthFields.jsx";

// import {} './ViewGridProviderDetail.jsx'
import action from "./Action.js";

// 新建
export const onAdd = async (proxy, props, dataConfig) => {
  const boxOptions = dataConfig.boxOptions;
  boxOptions.title =
    proxy.$ts(props.table.cnName) + "(" + proxy.$ts("新建") + ")";
  dataConfig.currentAction.value = action.ADD;
  dataConfig.currentRow.value = {};
  if (!(await initBox(proxy, props, dataConfig))) return;
  resetAdd(proxy, props, dataConfig);
  initOntinueAdd(proxy, props, dataConfig, true);
  dataConfig.boxModel.value = true;
  modelOpenProcess(proxy, props, dataConfig);
};
//编辑
export const onEdit = async (proxy, props, dataConfig, rows) => {

  if (rows) {
    if (!Array.isArray(rows)) {
      rows = [rows];
    }
  } else {
    rows = proxy.getSelected();
  }
  if (rows.length == 0) {
    return proxy.$error(proxy.$ts("请选择要编辑的行!"));
  }
  if (rows.length != 1) {
    return proxy.$error(proxy.$ts("只能选择一行数据进行编辑!"));
  }
  dataConfig.boxOptions.title =
    proxy.$ts(props.table.cnName) + "(" + proxy.$ts("编辑") + ")";

  //编辑
  if (dataConfig.currentAction.value != 'View') {
    dataConfig.currentAction.value = action.EDIT;
  }

  //记录当前编辑的行
  dataConfig.currentRow.value = rows[0];
  //初始化弹出框
  if (!(await initBox(proxy, props, dataConfig))) return;
  initOntinueAdd(proxy, props, dataConfig, false);
  dataConfig.boxModel.value = true;
  //重置表单
  //resetDetailTable(proxy,props,dataConfig, rows[0],null)

  //重新表单与明细表数据
  resetEditForm(proxy, props, dataConfig, rows[0]);
  //设置远程查询表单的默认key/value
  getRemoteFormDefaultKeyValue(proxy, props, dataConfig);
  //点击编辑按钮弹出框后，可以在此处写逻辑，如，从后台获取数据
  modelOpenProcess(proxy, props, dataConfig, rows[0]);
};

export const onDelete = async (proxy, props, rows, dataConfig) => {
  if (rows) {
    if (!Array.isArray(rows)) {
      rows = [rows];
    }
  } else {
    rows = proxy.getSelectRows(); //  proxy.$refs.table.getSelected()
  }
  if (!rows || rows.length === 0)
    return proxy.$error(proxy.$ts("请选择要删除的行!"));
  let delKeys = rows.map((x) => {
    return x[props.table.key];
  });



  if (!delKeys || delKeys.length === 0)
    return proxy.$error(proxy.$ts("没有获取要删除的行数据!"));
  if (
    !(await props.delBefore(delKeys, rows)) ||
    !(await proxy.delBefore.call(proxy, delKeys, rows))
  ) {
    return;
  }
  if (
    !(await props.delBeforeAsync(delKeys, rows)) ||
    !(await proxy.delBeforeAsync.call(proxy, delKeys, rows))
  ) {
    return;
  }

  const delMsg =
    proxy.getDelMessage.call(proxy, rows) ||
    props.getDelMessage(rows) ||
    proxy.$ts("确认要删除选择的数据吗?");
  let tigger = false;
  proxy
    .$confirm(delMsg, proxy.$ts("警告"), {
      confirmButtonText: proxy.$ts("确定"),
      cancelButtonText: proxy.$ts("取消"),
      dangerouslyUseHTMLString: true,
      type: "warning",
      center: true,
    })
    .then(() => {
      if (tigger) return;
      tigger = true;
      let url = getUrl(action.DEL, null, props.table, props.dyScript, props, dataConfig.asyncApi.value);
      if (props.generic) {
        delKeys = {
          tableName: props.table.name,
          delKeys: delKeys
        }
      }
      proxy.http
        .post(url, delKeys, proxy.$ts("正在删除数据") + "....")
        .then((x) => {
          if (!x.status) return proxy.$error(x.message);
          proxy.$success(x.message);
          if (!proxy.delAfter.call(proxy, x, rows)) {
            return;
          }
          if (!props.delAfter(x, rows)) {
            return;
          }
          proxy.search();
        });
    })
    .catch((action) => {
      if (action !== "cancel") {
        console.log(action);
        proxy.$error(action);
      }
    });
};

//保存
export const saveClick = (proxy, props, dataConfig) => {
  proxy.$refs.form.validate((result) => {
    if (!result) return;
    saveExecute(proxy, props, dataConfig);
  });
};
//保存前确认操作
const saveExecuteConfirm = (proxy, props, formData, dataConfig, callback) => {
  const isAdd = dataConfig.currentAction.value == "Add";
  proxy.saveConfirm.call(
    proxy,
    (res) => {
      props.saveConfirm(
        (res) => {
          callback();
        },
        formData,
        isAdd,
      );
    },
    formData,
    isAdd,
  );
};
const saveExecute = async (proxy, props, dataConfig) => {
  let editFormFields = proxy.base.getFormValues(
    props.editFormFields,
    props.editFormOptions,
  );

  const currentAction = dataConfig.currentAction.value;
  const currentRow = dataConfig.currentRow.value || {};
  const hiddenFields = dataConfig.hiddenFields.value || [];
  if (currentAction !== action.ADD && hiddenFields.length) {
    for (const key in editFormFields) {
      if (hiddenFields.indexOf(key) !== -1) {
        editFormFields[key] = undefined;
      }
    }
  }

  let formData = {
    mainData: editFormFields,
    detailData: null,
    delKeys: null,
  };
  if (props.generic) {
    formData.tableName = props.table.name;
  }

  if (dataConfig.hasDetail.value) {
    if (!proxy.getTable().validate()) {
      return;
    }
    //获取明细表数据
    const rows = proxy.getTable().rowData;
    formData.detailData = convertDetailSubmitData(rows, props.detail.columns);
  }

  const detailOptions = dataConfig.detailOptions;
  if (detailOptions.delKeys.length > 0) {
    formData.delKeys = detailOptions.delKeys;
  }


  const isCopyClick = dataConfig.isCopyClick.value;

  let isAdd = currentAction === action.ADD;
  if (
    !(await props.submitBefore(formData, isAdd, isCopyClick)) ||
    !(await proxy.submitBefore.call(proxy, formData, isAdd, isCopyClick))
  )
    return;

  if (isAdd) {
    if (
      !(await proxy.addBefore.call(proxy, formData, isCopyClick)) ||
      !(await proxy.addBeforeAsync.call(proxy, formData, isCopyClick))
    )
      return;
    if (
      !(await props.addBefore(formData, isCopyClick)) ||
      !(await props.addBeforeAsync(formData, isCopyClick))
    )
      return;
  } else {
    if (
      !(await proxy.updateBefore.call(proxy, formData)) ||
      !(await proxy.updateBeforeAsync.call(proxy, formData))
    )
      return;
    if (
      !(await props.updateBefore(formData)) ||
      !(await props.updateBeforeAsync(formData))
    )
      return;
  }
  let url = getUrl(isAdd ? "add" : "update", null, props.table, null, props, dataConfig.asyncApi.value);
  // resetAdd(proxy, props, dataConfig);
  // proxy.$refs.form.$refs.volform.clearValidate()
  // return;
  saveExecuteConfirm(proxy, props, formData, dataConfig, () => {
    proxy.http.post(url, formData, true).then((x) => {
      if (
        !props.submitAfter(x, formData, isAdd, isCopyClick) ||
        !proxy.submitAfter.call(proxy, x, formData, isAdd, isCopyClick)
      )
        return;

      if (isAdd) {
        if (!proxy.addAfter.call(proxy, x, formData)) return;
        if (!props.addAfter(x, formData)) return;
        if (dataConfig.dyScript.addAfter?.(x, formData) === false) return;
        //连续添加
        if (dataConfig.continueAdd.value && x.status) {
          proxy.$success(x.message);
          dataConfig.currentAction.value = action.ADD;
          let _formFields;
          if (proxy.continueAddAfter) {
            _formFields = JSON.parse(JSON.stringify(editFormFields));
          }
          dataConfig.currentRow.value = {};
          proxy.$refs.form.$refs.volform.clearValidate();
          resetAdd(proxy, props, dataConfig);
          proxy.search();
          proxy.continueAddAfter.call(proxy, _formFields, formData, x);
          props.continueAddAfter.call(_formFields, formData, x);
          return;
        }
      } else {
        if (!proxy.updateAfter.call(proxy, x, formData)) return;
        if (!props.updateAfter(x, formData)) return;
        if (dataConfig.dyScript.updateAfter?.(x, formData) === false) return;
      }
      if (!x.status) return proxy.$error(x.message);
      proxy.$success(x.message || proxy.$ts("保存成功"));
      if (dataConfig.boxOptions.saveClose) {
        dataConfig.boxModel.value = false;
        //$refs.table.load(null, isAdd)
        proxy.getTable(true).load(null, isAdd);
        return;
      }
      let resultRow;
      if (typeof x.data === "string" && x.data !== "") {
        resultRow = JSON.parse(x.data);
      } else {
        resultRow = x.data;
      }

      if (currentAction === action.ADD) {
        props.editFormFields[props.table.key] = "";
        dataConfig.currentAction.value = action.EDIT;
        dataConfig.currentRow.value = resultRow.data;
      }
      resetEditForm(proxy, props, dataConfig, resultRow.data);
      if (dataConfig.hasDetail.value) {
        dataConfig.detailOptions.delKeys = [];
        if (resultRow.list) {
          proxy.getTable(true).rowData.push(...resultRow.list);
          // $refs.detail.rowData.push(...resultRow.list)
        }
      }
      proxy.getTable(true).load(null, isAdd);
      //$refs.table.load(null, isAdd)
    });
  });
};

const convertDetailSubmitData = (detailData, columns) => {
  const numberFields = [];
  const types = ["selectList", "cascader", "treeSelect", "decimal"];
  let _fields = columns
    .filter((c) => {
      const b = types.includes(c.type) || types.includes(c.edit && c.edit.type);
      if (b) {
        numberFields.push(c.field);
      }
      return b;
    })
    .map((c) => {
      return c.field;
    });
  if (_fields.length) {
    detailData = JSON.parse(JSON.stringify(detailData));
    detailData.forEach((row) => {
      for (let index = 0; index < _fields.length; index++) {
        const _field = _fields[index];
        if (Array.isArray(row[_field])) {
          row[_field] = row[_field].join(",");
        } else if (
          numberFields.includes(_field) &&
          row[_field] &&
          typeof (row[_field] == "number")
        ) {
          //增加明细表小数长度类型转换
          row[_field] = row[_field] + "";
        }
      }
    });
  }
  return detailData;
};

export const onPrintClick = async (proxy, props, dataConfig, rows) => {

};
//导入
export const importData = async (proxy, props, dataConfig, isDetail) => {
  const upload = dataConfig.upload;
  if (!upload.url) {
    initImportOptions(proxy, props, dataConfig);
  }
  upload.excel = true;
  proxy.$refs.upload_excel?.reset();
};

//导出
export const exportData = async (proxy, props, dataConfig, isDetail) => {
  let detailTable = isDetail && typeof isDetail != "boolean" ? isDetail : {};
  //导出
  let url, wheres, param;
  if (isDetail) {
    //明细表导出时如果是新建状态，禁止导出
    if (dataConfig.currentAction.value === "Add") {
      return;
    }

    url = `api/${detailTable?.table || props.detail.table}/${action.EXPORT}${dataConfig.asyncApi.value}`;

    const value = props.editFormFields[props.table.key];
    const keyField = detailTable?.mainKeyField || props.detail.mainKeyField || props.table.key;
    param = {
      value: value,
      wheres: [
        { name: keyField, value: value },
      ],
    };
  } else {
    //主表导出
    url = getUrl(action.EXPORT, null, props.table, null, props, dataConfig.asyncApi.value);
    wheres = proxy.base.getSearchParameters(
      proxy,
      props.searchFormFields,
      props.searchFormOptions,
    );
    param = {
      order: proxy.$refs.table.paginations.order,
      sort: proxy.$refs.table.paginations.sort,
      wheres: wheres || [],
    };
    if (
      !param.wheres.some((x) => {
        return x.name === props.table.key;
      })
    ) {
      let ids = proxy
        .getSelectRows()
        .map((x) => {
          return x[props.table.key];
        })
        .join(",");
      //2024.01.13增加默认导出勾选的数据
      if (ids) {
        param.wheres.push({
          name: props.table.key,
          value: ids,
          displayType: "selectList",
        });
      }
    }
    let _columns = [];
    props.columns.forEach((col) => {
      if (!col.hidden && !col.render) {
        if (col.children) {
          _columns.push(
            ...col.children
              .filter((c) => {
                return !c.hidden;
              })
              .map((m) => {
                return m.field;
              }),
          );
        } else {
          _columns.push(col.field);
        }
      }
    });
    if (_columns.length) {
      param.columns = _columns.filter(x => { return x });
    }
  }
  if (props.generic) {
    param.tableName = isDetail ? (detailTable?.table || props.detail.table) : props.table.name;
  }
  url = param.url || url;
  if (!isDetail) {
    if (!(await props.exportBefore(param, detailTable.table))) {
      return;
    }
    if (!(await proxy.exportBefore.call(proxy, param, detailTable.table))) {
      return;
    }
  }

  if (param.wheres && typeof param.wheres === "object") {
    param.wheres = JSON.stringify(param.wheres);
  }
  let fileName = dataConfig.downloadFileName.value;
  if (!fileName) {
    fileName =
      props.getFileName(isDetail) || proxy.getFileName.call(proxy, isDetail);
  }
  if (!fileName) {
    if (isDetail) {
      fileName =
        proxy.$ts(detailTable?.cnName || props.detail.cnName) + ".xlsx";
    } else {
      fileName = proxy.$ts(detailTable?.cnName || props.table.cnName) + ".xlsx";
    }
  }
  //新建禁止导出
  if (dataConfig.currentAction == "Add") {
    return;
  }
  //url, params, fileName, loading
  proxy.http.download(url, param, fileName, "loading....", (res) => {
    if (!props.exportAfter(res, param, detailTable)) {
      return;
    }
    if (!proxy.exportAfter.call(proxy, res, param)) {
      return;
    }
  });
};

export const onImportExcelAfter = (proxy, props, dataConfig, data) => {
  //2022.01.08增加明细表导入后方法判断
  if (!data.status) {
    return; // this.$message.error(data.message);
  }
  if (data.data && typeof data.data === "string") {
    data.data = JSON.parse(data.data);
  }

  //明细表导入
  if (dataConfig.boxModel.value) {
    if (!Array.isArray(data.data)) {
      data.data = [];
    }
    const detailItem = dataConfig.upload.currentDetail;

    data.data.forEach((x) => {
      if (detailItem) {
        x[detailItem.key] = undefined;
      } else {
        x[props.detail.key] = undefined;
      }
      x[props.table.key] = undefined;
    });

    //增加明细表导入后处理
    if (!proxy.importDetailAfter.call(proxy, data, detailItem, dataConfig?.currentAction == 'Add')) {
      return;
    }
    if (!props.importDetailAfter(data, detailItem, dataConfig?.currentAction == 'Add')) {
      return;
    }
    //无代码明细表导入
    if (props.generic) {
      proxy.getTable(detailItem?.table).load();
    } else {
      if (detailItem) {
        //三级明细表导入
        proxy.getTable(detailItem.table).rowData.unshift(...data.data);
        if (detailItem.secondTable) {
          proxy.getTable(detailItem.secondTable).getSelected()[0][
            detailItem.table
          ] = proxy.getTable(detailItem.table).rowData;
        }
      } else {
        proxy.getTable().rowData.unshift(...data.data);
      }
    }

    proxy.$message.success(proxy.$ts(data.message || '导入成功'));
    dataConfig.upload.excel = false;
    //刷新明细表proxy.upd
    return;
  }
  //主表导入
  if (!proxy.importAfter.call(proxy, data)) {
    return;
  }
  if (!props.importAfter(data)) {
    return;
  }
  proxy.$message.success(proxy.$ts("上传成功"));
  //刷新主表导入信息
  proxy.search();
};
