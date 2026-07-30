
const fileType = ["file", "img", "excel"];
import {h} from 'vue'
import { getCellColor, formatDate, cellFormatter } from "./VolTableFormat.js";
import { getDateOptions, getDateFormat } from "./VolTableDate.js";
import VolTag from "@/components/basic/VolTag/VolTag.vue";
import {
  selectChange,
  switchChange,
  extraClick,
} from "./VolTableEvent.js"
import {
  isEmptyTag,
  initColumnDisabled,
} from "./VolTableProvider.js";
const initRender = (proxy, props, tableData, edit, fn) => {
  for (let index = 0; index < props.columns.length; index++) {
    const column = props.columns[index];
    if (column.hidden || column.cellRenderer) continue;
    let cellRenderer;
    if (column.render) {
      cellRenderer = ({ rowData }) => {
        return column.render(h, {
          row: rowData,
          column,
          index: rowData.elementIndex,
        });
      };
    } else {
      cellRenderer = ({ rowData }) => {
        return getRender(proxy, props, rowData, column, edit, fn);
      };
    }
    column.cellRenderer = ({ rowData }) => {
      const style = column.cellStyle?.(rowData, rowData.elementIndex, index);
      return (
        <div class="vol-row-cell" style={style}>
          {cellRenderer({ rowData })}
        </div>
      );
    };
  }
  initColumnIndex(proxy, props, tableData);
  initCheckbox(proxy, props, tableData);
  renderHeader(proxy, props, tableData);
  //headerCellRenderer
  return props.columns;
};

// 拖动改变列宽相关逻辑
const columnRefresh = (columns, x, field) => {
  let columnItem = columns.find((item) => item.field === field) || {};
  columnItem.width = columnItem.width + x;
};
let x1, x2;
var dragStart = (columns, e) => {
  x1 = e.x;
};
var dragEnd = (columns, e, field) => {
  x2 = e.x;
  let x = x2 - x1;
  columnRefresh(columns, x, field);
};

//自定义表头
const renderHeader = (proxy, props, tableData) => {
  // renderHeader
  props.columns.forEach((column) => {
    // if (column.renderHeader) {
    //   column.headerCellRenderer = ({}) => {
    //     return column.renderHeader(h, {
    //       column,
    //     });
    //   };
    //   return;
    // }
    if (column.dataKey == "selection" || column.dataKey == "column-index") {
      return;
    }
    column.headerCellRenderer = () => (
      <div
        class="table-v2-header-box"
        ondragenter={(e) => e.preventDefault()}
        ondragover={(e) => e.preventDefault()}
      >
        <div>
          {column.renderHeader
            ? column.renderHeader(h, { column })
            : proxy.$ts(column.title)}
        </div>
        <div
          class="table-v2-drag-line"
          draggable
          ondragstart={(e) => dragStart(props.columns, e)}
          ondragend={(e) => dragEnd(props.columns, e, column.field)}
        ></div>
      </div>
    );
  });
};

const getRender = (proxy, props, rowData, column, edit, fn) => {
  if (!column) return "";
  //文件上传
  if (column.edit && !column.readonly && fileType.includes(column.edit.type)) {
    return cellUploadRender(proxy, props, rowData, column, edit, fn);
  }
  //编辑
  const isCurrentEditRow =
    column.edit &&
    !column.readonly &&
    (column.edit.keep || edit.rowIndex == rowData.elementIndex) &&
    (!column.elCheckedit ||
      column.elCheckedit(rowData, column, rowData.elementIndex));
  //编辑表格
  if (isCurrentEditRow) {
    return (
      <div
        class="e-item"
        style="width: 100%;"
        onClick={(e) => {
          e?.stopPropagation();
        }}
      >
        <div>{getEditRender(proxy, props, rowData, column, edit, fn)}</div>
        {column.extra && edit.rowIndex == rowData.elementIndex ? (
          <div class="extra">
            <a
              style={column.extra.style}
              onClick={() => {
                extraClick(rowData, column, tableData);
              }}
            >
              <i class={column.extra.icon} />
              {column.extra.text}
            </a>
          </div>
        ) : null}
      </div>
    );
  }
  //只读表格
  let cellRender = getReadonlyCellRender(
    proxy,
    props,
    rowData,
    column,
    edit,
    fn,
  );
  //增加超出提示
  if (column.showOverflowTooltip) {
    return (
      <el-tooltip
        class="box-item"
        effect="dark"
        content={rowData[column.field]}
        placement="top"
      >
        {cellRender}
      </el-tooltip>
    );
  }
  return cellRender;
};

//文件上传
const cellUploadRender = (proxy, props, rowData, column, edit, fn) => {
  return (
    <div
      style="display: flex; align-items: center"
      onClick={(e) => {
        e?.stopPropagation();
      }}
    >
      <i
        style=" padding: 3px; margin-right: 10px;color: #8f9293;  cursor: pointer; "
        onClick={() => {
          fn.showUpload(rowData, column);
        }}
        class="el-icon-upload"
      ></i>

      {fn.getFilePath(rowData[column.field], column).map((file, imgIndex) => {
        return column.edit?.type == "img" ? (
          <img
            key={imgIndex}
            onError={(e) => {
              fn.handleImageError(e);
            }}
            onClick={($event) => {
              fn.viewImg(rowData, column, file.path, $event, imgIndex);
            }}
            class="table-img"
            src={file.path + fn.access_token}
          />
        ) : (
          <a
            style="margin-right: 8px"
            class="t-file"
            key={imgIndex}
            onClick={($event) => {
              fn.dowloadFile(file, column, fIndex,rowData);
            }}
          >
            {file.name}
          </a>
        );
      })}
    </div>
  );
};
const dateType = ["date", "datetime", "month"];
const colorpicker = [
  "#ff4500",
  "#ff8c00",
  "#ffd700",
  "#90ee90",
  "#00ced1",
  "#1e90ff",
  "#c71585",
];
//编辑表格
const getEditRender = (proxy, props, rowData, column, edit, fn) => {
  if (dateType.includes(column.edit.type)) {
    return (
      <el-date-picker
        clearable
        size="default"
        style="width: 100%"
        ref={column.field + rowData.elementIndex}
        v-model={rowData[column.field]}
        onChange={(val) => {
          fn.dateChange?.(rowData, column, val);
        }}
        type={column.edit.type}
        placeholder={proxy.$ts(column.placeholder || column.title)}
        disabledDate={(val) => getDateOptions(val, column)}
        value-format={getDateFormat(column)}
        disabled={initColumnDisabled(rowData, column)}
        onVisibleChange={(show) => {
          fn.dateVisibleChang?.(show);
        }}
      ></el-date-picker>
    );
  }

  // time字段：数据库字段要用varhcar类型
  //如果使用的是date/datetime类型,需要设置表单配置的字段属性edit.valueFormat='YYYY-MM-DD HH:mm'
  if (column.edit.type == "time") {
    return (
      <el-time-picker
        clearable
        size="default"
        style="width: 100%"
        v-model={rowData[column.field]}
        onChange={(val) => {
          column?.onChange(rowData, column, val);
        }}
        placeholder={proxy.$ts(column.placeholder || column.title)}
        value-format={column.format || "HH:mm:ss"}
        disabled={initColumnDisabled(rowData, column)}
      />
    );
  }
  if (column.edit.type == "color") {
    //   @show="isDateChange = true"
    // @hide="isDateChange = false"
    return (
      <div>
        {rowData[column.field]}
        <el-color-picker
          show-alpha
          teleported={true}
          predefine={colorpicker}
          v-model={rowData[column.field]}
          onChange={(val) => {
            column?.onChange(rowData, column, val);
          }}
        />
      </div>
    );
  }
  if (column.edit.type == "switch") {
    return (
      <el-switch
        v-model={rowData[column.field]}
        active-color="#0f84ff"
        inactive-color="rgb(194 194 194)"
        active-text={proxy.$ts(column.activeText)}
        inactive-text={proxy.$ts(column.inactiveText)}
        onChange={(val) => switchChange(val, rowData, column)}
        active-value={
          typeof rowData[column.field] === "boolean"
            ? true
            : typeof rowData[column.field] === "string"
              ? "1"
              : 1
        }
        inactive-value={
          typeof rowData[column.field] === "boolean"
            ? false
            : typeof rowData[column.field] === "string"
              ? "0"
              : 0
        }
        disabled={initColumnDisabled(rowData, column)}
      />
    );
  }

  if (column.edit.type == "selectTable") {
    return (
      <vol-select-table
        v-model={rowData[column.field]}
        field={column.field}
        ref={column.field + rowData.elementIndex}
        onSelect={(rows) => {
          column?.onSelect(rowData, rows);
        }}
        textInline={column.textInline}
        paginationHide={column.paginationHide}
        pagination={column.pagination}
        loadBefore={(params, callBack) => {
          column?.loadBefore(rowData, params, callBack);
        }}
        loadAfter={(rows, callBack, result) => {
          column?.loadAfter(rowData, rows, callBack, result);
        }}
        onOnKeyPress={(value, $event) => {
          column?.onKeyPress(value, $event, rowData);
        }}
        url={column.url}
        columns={column.columns}
        defaultLoadPage={column.load}
        single={column.single}
        height={column.height}
        inputReadonly={column.inputReadonly}
        width={column.selectWidth}
      />
    );
  }

  if (["select", "selectList"].includes(column.edit.type)) {
    if (column.bind.data.length >= props.select2Count) {
      return (
        <el-select-v2
          ref={column.field + rowData.elementIndex}
          style={{ width: "100%" }}
          size="default"
          props={{ label: "value", value: "key" }}
          v-model={rowData[column.field]}
          filterable={column.filter === undefined ? true : column.filter}
          multiple={column.edit.type === "select" ? false : true}
          placeholder={proxy.$ts(column.placeholder || column.title)}
          allow-create={column.autocomplete}
          options={column.bind.data}
          onChange={() => {
            column.onChange?.(rowData, column);
          }}
          clearable={true}
          disabled={initColumnDisabled(rowData, column)}
        >
          {{
            default: ({ item }) => {
              return item.label || item.value;
            },
          }}
        </el-select-v2>
      );
    }
    return (
      <el-select
        ref={column.field + rowData.elementIndex}
        size="default"
        style={{ width: "100%" }}
        v-model={rowData[column.field]}
        filterable={column.filter === undefined ? true : column.filter}
        reserve-keyword={false}
        multiple={column.edit.type === "select" ? false : true}
        placeholder={proxy.$ts(column.placeholder || column.title)} // setup 语法去掉 this
        allow-create={column.autocomplete}
        onChange={(val) => {
          selectChange(rowData, column, val);
        }}
        onClear={(val) => {
          selectChange(rowData, column, val, true);
        }}
        clearable={true}
        disabled={initColumnDisabled(rowData, column)}
      >
        {column.bind.data.map((item) => (
          <el-option
            key={item.key}
            v-show={!item.hidden} // 对应原模板的 v-show
            disabled={item.disabled}
            label={proxy.$ts(item.value)} // setup 语法去掉 this
            value={item.key}
          />
        ))}
      </el-select>
    );
  }

  if (["treeSelect", "cascader"].includes(column.edit.type)) {
    return (
      <el-tree-select
        ref={column.field + rowData.elementIndex}
        style={{ width: "100%" }}
        v-model={rowData[column.field]}
        data={column.bind.data}
        multiple={column.multiple === undefined ? true : column.multiple}
        render-after-expand={false}
        show-checkbox={true}
        check-strictly={
          column.checkCtrictly === undefined ? true : column.checkCtrictly
        }
        check-on-click-node={true}
        node-key="key"
        onChange={() => {
          column?.onChange(rowData, column);
        }}
        props={{ label: "label" }}
      >
        {{
          default: ({ data }) => {
            return proxy.$ts(data.label);
          },
        }}
      </el-tree-select>
    );
  }
  if (column.edit.type == "textarea") {
    return (
      <el-input
        ref={column.field + rowData.elementIndex}
        type="textarea"
        placeholder={proxy.$ts(column.placeholder || column.title)}
        v-model={rowData[column.field]}
        disabled={initColumnDisabled(rowData, column)}
        autosize={{
          minRows: column.minRows || 2,
          maxRows: column.maxRows || 10,
        }}
      />
    );
  }
  if (column.edit.type === "number" || column.edit.type === "decimal") {
    return (
      <el-input-number
        ref={column.field + rowData.elementIndex}
        style={{ width: "100%" }}
        v-model={rowData[column.field]}
        precision={column.edit.type === "number" ? 0 : column.precision}
        min={column.min}
        max={column.max}
        disabled={column.readonly || column.disabled}
        controls-position="right"
        onFocus={(event) => fn.onFocus(rowData, column, event)}
        onBlur={(event) => fn.onBlur(rowData, column, event)}
        onKeyupDelete={(event) => fn.inputKeypress(rowData, column, event)}
        onChange={(event) => fn.inputKeypress(rowData, column, event)}
        onKeypress={(event) => {
          fn.inputKeypress(rowData, column, event);
        }}
      />
    );
  }
  if (!column.summary && !column.onKeyPress) {
    return (
      <input
        ref={column.field + rowData.elementIndex}
        class="table-input"
        v-model_lazy={rowData[column.field]}
        placeholder={proxy.$ts(column.placeholder || column.title)}
        disabled={initColumnDisabled(rowData, column)}
        onInput={(event) => fn.inputKeypress(rowData, column, event)}
        onFocus={(event) => fn.onFocus(rowData, column, event)}
        onBlur={(event) => fn.onBlur(rowData, column, event)}
      />
    );
  }

  return (
    <el-input
      ref={column.field + rowData.elementIndex}
      onChange={(event) => fn.inputKeypress(rowData, column, event)}
      onInput={(event) => fn.inputKeypress(rowData, column, event)}
      onKeyupEnter={(event) => fn.inputKeypress(rowData, column, event)}
      size="default"
      v-model={rowData[column.field]}
      placeholder={proxy.$ts(column.placeholder || column.title)}
      disabled={initColumnDisabled(rowData, column)}
      onBlur={(event) => fn.onBlur(rowData, column, event)}
    />
  );
};

//只读表格
const getReadonlyCellRender = (proxy, props, rowData, column, edit, fn) => {
  if (typeof column.render == "function") {
    return (
      <table-render
        row={rowData}
        key="rd-01"
        index={rowData.elementIndex}
        column={column}
        render={column.render}
      ></table-render>
    );
  }

  if (column.link) {
    return (
      <a
        href="javascript:void(0)"
        style="text-decoration: none"
        onClick={($event) => {
          linkClick(props, rowData, column, $event);
        }}
        v-text={cellFormatter(proxy, rowData, column, true)}
      ></a>
    );
  }

  if (column.type == "img" || column.type == "file" || column.type == "excel") {
    return (
      <div
        style="display: flex;"
        onClick={(e) => {
          e?.stopPropagation();
        }}
      >
        {fn.getFilePath(rowData[column.field], column).map((file, imgIndex) => {
          return column.type == "img" || column.edit?.type == "img" ? (
            <img
              key={imgIndex}
              onError={(e) => {
                fn.handleImageError(e);
              }}
              onClick={($event) => {
                fn.viewImg(rowData, column, file.path, $event, imgIndex);
              }}
              class="table-img"
              style={{
                height: (column.imgHeight || props.rowHeight || 40) + "px",
                width: (column.imgWidth || props.rowHeight || 40) + "px",
              }}
              src={file.path + (fn.access_token || "")}
            />
          ) : (
            <a
              style="margin-right: 8px"
              class="t-file"
              key={imgIndex}
              onClick={($event) => {
                fn.dowloadFile(file, column, imgIndex,rowData);
              }}
            >
              {file.name}
            </a>
          );
        })}
      </div>
    );
  }

  if (column.type == "date") {
    return formatDate(rowData, column);
  }
  if (column.type == "month") {
    return (rowData[column.field] || "").substr(0, 7);
  }
  if (column.formatter) {
    return (
      <div
        onClick={($event) => {
          fn.formatterClick(rowData, column, $event);
        }}
        v-html={column.formatter(rowData, column)}
      ></div>
    );
  }
  if (column.bind && (column.normal || column.edit)) {
    return (
      <div
        onClick={($event) => {
          fn.formatterClick(rowData, column, $event);
        }}
      >
        <span style={column.getStyle?.(rowData, column)}>
          {cellFormatter(proxy, rowData, column, true)}
        </span>
      </div>
    );
  }
  if (column.click && !column.bind) {
    return (
      <div
        onClick={($event) => {
          fn.formatterClick(rowData, column, $event);
        }}
      >
        {rowData[column.field]}
      </div>
    );
  }
  if (column.bind) {
    return (
      <div
        onClick={($event) => {
          column.click && fn.formatterClick(rowData, column, $event);
        }}
      >
        {fn.useTag.value && !isEmptyTag(rowData, column) && column.type != "cascader" ? (
          <VolTag
            class={["cell-tag"]}
            type={fn.getColor?.(rowData, column)}
            effect={column.effect}
          >
            {cellFormatter(proxy, rowData, column, true)}
          </VolTag>
        ) : (
          <span>{cellFormatter(proxy, rowData, column, true)}</span>
        )}
      </div>
    );
  }
  return cellFormatter(proxy, rowData, column, true);
};


const selectionChange = (proxy, props, rowData) => {
  const selection = rowData().filter(x => { return x.elChecked });
  if (props.single) {
    if (selection.length == 1) {
      proxy.$emit('rowChange', selection[0])
    }
  } else {
    proxy.$emit('rowChange', selection)
  }
  // 将selectionchange暴露出去
  proxy.$emit('selectionChange', selection)
}
//checkbox
const initCheckbox = (proxy, props, tableData) => {
  if (!props.ck) return;
  if (
    props.columns.some((x) => {
      return x.dataKey == "selection";
    })
  ) {
    return;
  }
  props.columns.unshift({
    key: "selection",
    width: 50,
    fixed: true,
    align: "center",
    dataKey: "selection",
    key: "selection",
    ck: true,
    cellRenderer: ({ rowData }) => {
      const onChange = (value) => {
        //单选
        if (props.single) {
          tableData().forEach((row) => {
            row.elChecked = false;
          });
        }
        rowData.elChecked = value;
        selectionChange(proxy, props, tableData)
      };
      if (!rowData.elChecked) {
        rowData.elChecked = false;
      }
      // const style= column.cellStyle?.(rowData, rowData.elementIndex, index);
      return (
        <div
          style="width: 100%;height: 100%;align-items: center; justify-content: center; display: flex;"
          onClick={($e) => {
            $e?.stopPropagation();
          }}
        >
          <ElCheckbox
            onChange={(value) => {
              onChange(value);
            }}
            modelValue={rowData.elChecked}
          />
        </div>
      );
    },
    headerCellRenderer: () => {
      const onChange = (value) => {
        //单选
        if (props.single) {
          return;
        }
        // tableData.value = 
        tableData().forEach((row) => {
          row.elChecked = value;
          //  return row;
        });
        selectionChange(proxy, props, tableData)
      };
      const allSelected =
         tableData().every((row) => row.elChecked);
      const containsChecked = tableData().some((row) => row.elChecked);
      return (
        <ElCheckbox
          onChange={(value) => {
            onChange(value);
          }}
          modelValue={allSelected}
          intermediate={containsChecked && !allSelected}
        />
      );
    },
  });
};
//显示行号
const initColumnIndex = (proxy, props, rows) => {
  if (!props.columnIndex) {
    return;
  }
  if (
    props.columns.some((x) => {
      return x.dataKey == "column-index";
    })
  ) {
    return;
  }

  props.columns.unshift({
    width: 50,
    fixed: true,
    field: "vol-column-index",
    align: "center",
    dataKey: "column-index",
    key: "column-index",
    columnIndex: true,
    title: proxy.$ts("序号"),
    cellRenderer: ({ rowData }) => {
      return (
        <div
          style="width: 100%;height: 100%;align-items: center; justify-content: center; display: flex;"
          onClick={($e) => {
            $e?.stopPropagation();
          }}
        >
          {rowData.elementIndex + 1}
        </div>
      );
    },
  });
};

const linkClick = (props, row, column, $e) => {
  $e?.stopPropagation();
  props.linkView(row, column);
};

/**
 * 重新计算列宽度，当可见列总宽度小于目标宽度时，按比例分配剩余宽度
 * @param {Array} columns - 列配置数组，格式[{field:"",title:"",width:Number,hidden:Boolean}]
 * @param {Number} width - 目标总宽度
 * @returns {Array} - 调整后的列配置数组（原数组浅拷贝，避免修改原数据）
 */
const initMinWidth = (columns, width) => {
  const newColumns = [...columns];

  let widthCS = 0;
  const visibleColumns = newColumns.filter((col) => {
    //不计算render与chekxbox、index列
    if (
      !col.hidden &&
      (col.render || col.key == "selection" || col.key == "column-index")
    ) {
      widthCS += col.width || 90;
    }
    return (
      !col.hidden &&
      !col.render &&
      col.key != "selection" &&
      col.key != "column-index"
    );
  });
  width = width - widthCS;

  const originalTotalWidth = visibleColumns.reduce(
    (sum, col) => sum + (col.width || 0),
    0,
  );

  if (originalTotalWidth >= width || visibleColumns.length === 0) {
    return;
  }

  const surplusWidth = width - originalTotalWidth;

  visibleColumns.forEach((col) => {
    const ratio = (col.width || 0) / originalTotalWidth;
    const addWidth = surplusWidth * ratio;
    col.width = (col.width || 0) + addWidth;
  });
};

const getRowClassV2 = ({ columns, rowData, rowIndex }) => {
  return rowData.elChecked ? "table-v2-row-checked" : "";
};

export { initRender, initMinWidth, getRowClassV2 };
