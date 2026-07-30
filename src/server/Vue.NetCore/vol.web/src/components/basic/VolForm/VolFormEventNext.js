export const inputType = [
  "text",
  "like",
  "likeStart",
  "likeEnd",
  "input",
  "textarea",
  "number",
  "decimal",
];

export const changeType = [
  "date",
  "datetime",
  "select",
  "selectList",
  "year",
  "month",
]; //,'cascader'

const getNextField = (proxy, formOptions, field) => {
  const flatOptions = formOptions.flat().filter((x) => {
    return !x.hidden && !x.render && !isReadonly(x);
  });
  //.filter((x) => !x.type || (!x.hidden && inputType.includes(x.type)));
  const index = flatOptions.findIndex((x) => {
    return x.field === field;
  });
  if (index == -1) {
    return;
  }
  const item = flatOptions[index + 1];
  if (!item) {
    return;
  }
  //跳转到下一个标签
  const ops = proxy.$refs[item.field]; //.focus()
  if (!ops) {
    return;
  }
  if (Array.isArray(ops)) {
    ops[0]?.focus?.();
  } else {
    ops?.focus?.();
  }
};
const isReadonly = (option) => {
  return option.readonly || option.disabled;
};

export const regEventNext = (proxy, formOptions) => {
  //全局绑定编辑输入跳转到下一个字段
  formOptions.forEach((x) => {
    x.forEach((option) => {
      if (
        !option.onKeyPress &&
        !option.render &&
        (inputType.includes(option.type) || !option.type)
      ) {
        option.onKeyPress = ($event) => {
          if ($event && $event.keyCode === 13) {
            getNextField(proxy, formOptions, option.field);
          }
        };
      } else if (changeType.includes(option.type) && !option.onChange) {
        option.onChange = ($event) => {
          getNextField(proxy, formOptions, option.field);
        };
      }
    });
  });
};
