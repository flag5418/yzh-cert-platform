
export const initReadonly = (proxy, props, dataConfig) => {
  props.editFormOptions.forEach((ops) => {
    ops.forEach((x) => {
      if (x.readonly || x.disabled) {
        dataConfig.defaultFormReadonlyFields.value.push(x.field);
      }
    });
  });

  // 明细列默认只读字段，key 格式: "tableKey:field"
  const defaultDetailReadonlyFields = new Set();

  // detailOptions.columns 在第一次 setFormReadonly 调用时才有数据，懒初始化
  let detailOptionsInited = false;
  
 //设置表单只读
  const setFormReadonly = (readonly) => {
    props.editFormOptions.flat().forEach((ops) => {
      if (!dataConfig.defaultFormReadonlyFields.value.includes(ops.field)) {
        ops.readonly = !!readonly;
      }
    });
    // 懒初始化 detailOptions 的默认只读字段
    if (!detailOptionsInited) {
      dataConfig.detailOptions.columns?.forEach((col) => {
        if (col.readonly) {
          defaultDetailReadonlyFields.add(`__detailOptions__:${col.field}`);
        }
      });
      detailOptionsInited = true;
    }
    dataConfig.detailOptions.columns.forEach((x) => {
      if (!defaultDetailReadonlyFields.has(`__detailOptions__:${x.field}`)) {
        x.readonly = !!readonly;
      }
    });
    dataConfig.detailOptions.buttons.forEach((x) => {
      x.hidden = !!readonly;
    });
  };
  return { setFormReadonly };
};
//审批表单只读
export const setFormAuditReadonly = (
  tableInfo,
  editFormFields,
  editFormOptions,
  dataConfig
) => {
  
  return false;
};
