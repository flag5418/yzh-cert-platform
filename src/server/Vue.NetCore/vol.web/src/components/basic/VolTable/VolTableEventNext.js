import { inputType, changeType } from '../VolForm/VolFormEventNext.js'
import { addTableRow } from './VolTableProvider.js'

const focusTableCell = (proxy, nextTick, field, rowIndex, delay = 100) => {
  nextTick(() => {
    setTimeout(() => {
      const inputRef = proxy.$refs[field + rowIndex];
      if (inputRef && inputRef[0]) {
        inputRef[0].focus();
      }
    }, delay);
  });
};

export const getNextTableCell = (proxy, props, tableData, edit, nextTick, row, column) => {
  const editableColumns = props.columns.filter((col) => {
    return col.edit && !col.hidden && !col.readonly;
  });

  if (editableColumns.length === 0) return;

  const currentIndex = editableColumns.findIndex((col) => {
    return col.field === column.field;
  });

  if (currentIndex === -1) return;

  const rowIndex = row.elementIndex;

  // 当前已经是最后一个可编辑列时，优先跳到下一行首列。
  // 如果当前行本身就是最后一行，则先自动新增一行，再聚焦到新行首列。
  if (currentIndex === editableColumns.length - 1) {
    //最后一行时添加新行
    if (!proxy.$global.tale.eventNewRow) {
        return;
    }
    let newRowIndex = rowIndex + 1;
    if (rowIndex === tableData.length - 1) {
      addTableRow(proxy, props, tableData, {});
      newRowIndex = tableData.length - 1;
    }
    edit.rowIndex = newRowIndex;
    focusTableCell(proxy, nextTick, editableColumns[0].field, newRowIndex, 300);
    return;
  }

  const nextColumn = editableColumns[currentIndex + 1];

  if (!nextColumn) return;

  // 非最后一列时，继续聚焦当前行的下一个可编辑单元格。
  focusTableCell(proxy, nextTick, nextColumn.field, rowIndex);
};

export const regTableEventNext = (proxy, props, tableData, edit, nextTick) => {
  if (!props.eventNext) {
    return;
  }
  // 全局绑定编辑输入跳转到下一个字段
  props.columns.forEach((option) => {
    if (!option.edit || option.hidden || option.render) {
      return;
    }
    if (
      !option.onKeyPress &&
      !option.onKeypress&&
      !option.render &&
      (inputType.includes(option.edit.type) || !option.edit.type)
    ) {
      option.onKeyPress = (row, column, $event) => {
        if ($event && $event.keyCode === 13) {
          getNextTableCell(proxy, props, tableData, edit, nextTick, row, option);
        }
      };
    } else if (changeType.includes(option.edit.type) && !option.onChange) {
      option.onChange = (row, val, event) => {
        getNextTableCell(proxy, props, tableData, edit, nextTick, row, option);
      };
    }
  });
};
