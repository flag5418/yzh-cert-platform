import common from '@/uitils/common.js'
import { compressImage } from './VolImgCompress.js'
import store from '@/store/index'
const getImgUrls = (imgs) => {
  return imgs
    .filter((x) => {
      return x.path || x.orginUrl
    })
    .map((m) => {
      return m.path || m.orginUrl
    })
    .join(',')
}

//将表volform表单数据转换为json对象提交
const getFormValues = (formFields, formOptions) => {
  if (formFields.value && !formFields.hasOwnProperty('value')) {
    formFields = formFields.value
  }
  if (formOptions.value && Array.isArray(formOptions.value)) {
    formOptions = formOptions.value
  }
  //将数组转换成string
  const formValues = {}
  for (const key in formFields) {
    let val = formFields[key]
    const option = getFormOption(formOptions, key, false) || {}
    if (typeof val == 'function') {
      formValues[key] = formFields[key]()
      continue
    } else if (typeof val == 'string' && val) {
      val = val.trim();
    }
    //解决下拉框清除后不能保存数据的问题
    if (val === undefined) {
      val = null
    }
    if (Array.isArray(val)) {
      //上传的图片
      if (['img', 'file', 'excel'].includes(option.type)) {
        formValues[key] = getImgUrls(val)
      } else if (option.type == 'cascader') {
        formValues[key] = formFields[key][formFields[key].length - 1] || null
      } else {
        formValues[key] = val.join(',')
      }
      continue
    }
    if (typeof val == 'boolean') {
      val = val ? 1 : 0;
    }
    formValues[key] = val
  }
  return formValues
}
//重置表单,//"vue": "^3.5.13",
const resetForm = (formFields, formOptions, data) => {
  //console.log('66')
  if (formFields.value && !formFields.hasOwnProperty('value')) {
    formFields = formFields.value
  }
  if (formOptions.value && Array.isArray(formOptions.value)) {
    formOptions = formOptions.value
  }
  if (!data) {
    data = {}
  }
  for (const key in formFields) {
    if (!data.hasOwnProperty(key) || isEmptyValue(data[key])) {
      if (Array.isArray(formFields[key])) {
        const isRnage = formOptions.some((x) => {
          return x.field == key && x.range
        })
        formFields[key] = isRnage ? [null, null] : []
      } else {
        const option = getFormOption(formOptions, key, false) || {}
        if (['selectList', 'checkbox', 'cascader', 'treeSelect'].includes(option.type)) {
          formFields[key] = []
        } else {
          formFields[key] = undefined
        }
      }
      continue
    }
    setFormValue(formFields, formOptions, key, data)
  }
}

const setFormValue = (formFields, formOptions, field, data) => {
  const option = getFormOption(formOptions, field, false) || {}
  //图片处理待完

  let newVal = data[field]
  if (['cascader'].includes(option.type)) {
    if (option.orginData && option.orginData.length) {
      newVal = typeof option.orginData[0].id === 'number' ? newVal * 1 || 0 : newVal + ''
      let _cascaderParentTree = common.getTreeAllParent(newVal, option.orginData)
      if (_cascaderParentTree && _cascaderParentTree.length) {
        formFields[field] = _cascaderParentTree.map((x) => {
          return x.id
        })
        return
      }
    }
    formFields[field] = [data[field]]
    return
  }
  const isNumber = option.data && typeof option.data[0]?.key == 'number'
  if (['selectList', 'checkbox', 'treeSelect'].includes(option.type)) {
    let arr = (data[field] + '').split(',')
    if (isNumber) {
      arr = arr.map((x) => {
        return x * 1
      })
    }
    formFields[field] = arr
    return
  }
  if (typeof newVal == 'boolean') {
    newVal = newVal ? 1 : 0;
    if (!isNumber) {
      newVal = newVal + '';
    }
  }
  else if (isNumber || option.type == 'number' || option.type == 'decimal') {
    newVal = newVal * 1
  } else {
    if (typeof newVal == 'number') {
      newVal = newVal + '';
    }
  }
  formFields[field] = newVal
}
// Array.prototype.getFormOption = (field) => {
//   return getFormOption(this, field)
// }

//获取表单配置
const getFormOption = (formOptions, field, showMsg = true) => {
  if (formOptions.value && Array.isArray(formOptions.value)) {
    formOptions = formOptions.value
  }
  let option
  for (var index = 0; index < formOptions.length; index++) {
    var options = formOptions[index]
    option = options.find((x) => {
      return x.field == field
    })
    if (option) return option
  }
  // if (showMsg) {
  //   console.log(`未获取字段配置:${field}`)
  // }
  return
}
// Array.prototype.getFormDicData = (field) => {
//   return getFormDicData(this, field)
// }
//获取表单字典
const getFormDicData = (formOptions, field) => {
  const option = getFormOption(formOptions, field)
  return option.data
}
// Array.prototype.getFormDicItem = (field, key) => {
//   return getFormDicItem(this, field, key)
// }
//获取表单字典选项(根据key获取value显示文本)
const getFormDicItem = (formOptions, field, key) => {
  const data = getFormDicData(formOptions, field)
  if (Array.isArray(key)) {
    return data.filter((x) => {
      return key.includes(x.key)
    })
  }
  return data.find((x) => {
    return key == x.key
  })
}

// Array.prototype.setFormFocus = (field, timeout = 400) => {
//   return setFormFocus(this, field, timeout)
// }
//设置表单焦点
const setFormFocus = (formOptions, field, timeout = 400) => {
  const option = getFormOption(formOptions, field)
  if (!timeout || timeout < 50) {
    timeout = 200
  }
  option.focus = false
  setTimeout(() => {
    option.focus = true
  }, timeout)
}
// Array.prototype.getColumn = (field) => {
//   return getColumn(this, field)
// }
//获取表格配置
const getColumn = (columns, field) => {
  if (columns.value && Array.isArray(columns.value)) {
    columns = columns.value
  }
  const column = columns.find((x) => {
    return x.field == field
  })
  if (!column) {
    console.log('未获取字段配置')
  }
  return column
}
// Array.prototype.getColumnDicData = (field) => {
//   return getColumnDicData(this, field)
// }
//获取表格字典配置
const getColumnDicData = (columns, field) => {
  const column = getColumn(columns, field)

  if (column.bind && column.bind.data) {
    return column.bind.data
  }
  console.log('未获取到字段字典配置')
  return
}
// Array.prototype.getColumnDicItem = (field, key) => {
//   return getColumnDicItem(this, field, key)
// }
//获取表格字典项(根据key获取value显示文本)
const getColumnDicItem = (columns, field, key) => {
  const data = getColumnDicData(columns, field)
  if (Array.isArray(key)) {
    return data.filter((x) => {
      return key.includes(x.key)
    })
  }
  return (
    data.find((x) => {
      return key == x.key
    }) || {}
  )
}

const setItem = (key, obj) => {
  store.getters.data()[key] = obj
}
const getItem = (key) => {
  const obj = store.getters.data()[key]
  return obj
}

const ASYNCAPI = 'asyncApi';
const getAsyncApi = (table) => {
  return (store.getters.data()[ASYNCAPI] || []).includes(table) ? 'Async' : '';
}
const setAsyncApi = (tables) => {
  store.getters.data()[ASYNCAPI] = tables || []
}

const getAccessToken = () => {
  const tk = (store.getters.getUserInfo() || { accessToken: '' }).accessToken
  return tk ? '?access_token=' + tk : ''
}
const isEmptyValue = (value) => {
  if (typeof value == 'string') {
    value = value.trim()
    if (value === '') {
      return true
    }
    return false
  }
  if (value instanceof Array && !value.length) {
    return true
  }
  return value === null || value === undefined
}
//将表单转换为后台接口查询条件
const getSearchParameters = (proxy, formFields, formRules) => {
  if (formFields.value && !formFields.hasOwnProperty('value')) {
    formFields = formFields.value
  }
  if (formRules.value && Array.isArray(formRules.value)) {
    formRules = formRules.value
  }
  const wheres = []
  for (const key in formFields) {
    let value = formFields[key]
    if (isEmptyValue(value)) continue

    if (typeof value == 'number') {
      value = value + ''
    }

    const option = getFormOption(formRules, key) || {}

    let displayType = option.range ? 'range' : option.type

    //联级只保留选中节点的最后一个值
    if (displayType == 'cascader') {
      //查询下面所有的子节点，如：选中的是父节点，应该查询下面所有的节点数据--待完
      if (value && value.length) {
        let nodes = proxy.base.getTreeAllChildren(value[value.length - 1], option.orginData)
        if (!(nodes?.length)) {
          value = [value[value.length - 1]]
        } else {
          value = nodes.map((x) => {
            return x.id
          })
        }
        displayType = 'selectList'
      }
    } else if (displayType == 'treeSelect' && Array.isArray(value)) {
      displayType = 'selectList'
      value = (value || []).join(',')
    }
    //2021.05.02增加区间查询
    if (
      typeof value == 'string' ||
      ['date', 'datetime', 'month', 'range'].indexOf(displayType) == -1
    ) {
      wheres.push({
        name: key,
        value: typeof value == 'string' ? (value + '').trim() : value.join(','),
        displayType: displayType
      })
      continue
    }
    for (let index = 0; index < value.length; index++) {
      if (!isEmptyValue(value[index])) {
        wheres.push({
          name: key,
          value: (value[index] + '').trim(),
          displayType: (() => {
            if (['date', 'datetime', 'month', 'range'].indexOf(displayType) != -1) {
              return index ? 'lessorequal' : 'thanorequal'
            }
            return displayType
          })()
        })
      }
    }
  }
  return wheres
}


const generateUniqueFileName = (originalFileName) => {
  const timestamp = new Date().getTime();
  const random = Math.random().toString(36).substring(2, 8);
  const parts = originalFileName.split('.');
  const fileExtension = parts.length > 1 ? parts.pop().toLowerCase() : '';
  let uniqueFileName = `${timestamp}_${random}.${fileExtension}`
  return uniqueFileName;
};
//重置文件名
const resetFileName = async (files, callbck) => {
  if (!files?.length) return
  for (let index = 0; index < files.length; index++) {
    let originalFile = files[index]
    if (!originalFile.size) {
      continue;
    }
    let uniqueFileName = await callbck?.(originalFile);
    if (uniqueFileName === false) {
      continue;
    }
    if (!uniqueFileName) {
      uniqueFileName = generateUniqueFileName(originalFile.name)
    }
    let extension = '';
    if (!uniqueFileName.includes('.')) {
      extension = '.' + originalFile.name.split('.').pop();
    }
    const newFile = new File([originalFile], uniqueFileName + extension, {
      type: originalFile.type,
      lastModified: originalFile.lastModified
    });
    newFile.input = originalFile.input
    files.splice(index, 1, newFile);
  }
}
const fileType = ['img', 'file', 'excel']

const convertToVolFormArray = (data, tableName) => {

  data = data.filter(x => { return !x.detail }).map(x => {
    const obj = {
      field: x.field, title: x.title, width: x.width,
      // readonly: !!x.isReadDataset,
      // required: x.isNull + '' === '0'
    };
    const readDs = Number(x.isReadDataset);
    if (readDs === 1 || readDs === 2) {
      obj.readonly = true;
    }
    if (x.isNull + '' === '0') {
      obj.required = true;
    }
    if (x.dropNo && x.formType) {
      obj.dataKey = x.dropNo;
      obj.data = []
    }
    obj.type = x.formType
    if (fileType.includes(obj.type)) {
      obj.url = `api/${tableName}/upload`
      obj.multiple = true;
      obj.maxFile = 6;
      obj.maxSize = 100
    }
    //级联默认可以选择任意一级
    if (x.formType == "cascader" && x.checkStrictly === undefined) {
      obj.checkStrictly = true
    }
    return obj
  })
  const result = [];
  let currentGroup = [];
  let wd = 0;
  let len = 0;
  for (let i = 0; i < data.length; i++) {
    let item = data[i];
    wd += item.width || 25;
    if (wd > 100) {
      result.push(currentGroup);
      currentGroup = []
      currentGroup.push(item);
      wd = item.width || 25;
    } else {
      currentGroup.push(item);
    }
    if (currentGroup.length > len) {
      len = currentGroup.length
    }
  }
  if (currentGroup.length) {
    result.push(currentGroup);
    if (currentGroup.length > len) {
      len = currentGroup.length
    }
  }
  len = len * (len == 1 ? 350 : 200);
  if (len > document.body.clientWidth * 0.95) {
    len = document.body.clientWidth * 0.95
  }
  // width.value = len;
  return { formOptions: result, width: len };
}
//表单配置转换为volform表单
const convertDataToFormOptions = (data, tableName) => {
  data = data.map(x => { return { ...x } })
  let fields = {};
  data.forEach(x => {
    if (['selectList', 'checkbox', 'cascader', 'treeSelect'].includes(x.formType) || fileType.includes(x.formType)) {
      fields[x.field] = []
    } else {
      fields[x.field] = null;
    }
  })
  return { fields, ...convertToVolFormArray(data, tableName) }
}


const formatLongDecimal = (value) => {
  if (typeof (value) === 'number' && /\.\d{5,}/.test(value + '')) {
    return value + ''
  }
  return value;
}

const convertRowsValueToString = (rows, ignoreFields) => {
  //const types = ['selectList', 'cascader', 'treeSelect']
  ignoreFields = ignoreFields || []
  return rows.map(item => {
    // 每一行对象遍历key
    const newItem = { ...item };
    for (const key in newItem) {
      if (ignoreFields.includes(key)) {
        continue
      }
      // 判断值为数组则逗号拼接
      if (Array.isArray(newItem[key])) {
        newItem[key] = newItem[key].join(',');
      } else {
        newItem[key] = formatLongDecimal(newItem[key]);
      }
    }
    return newItem;
  });
}

const setFormAddOrUpdateReadonly = (formOptions, action) => {
  formOptions.flat().forEach(x => {
    if (x.readonlyUpdate) {
      x.readonly = action != 'Add'
    } else if (x.readonlyAdd) {
      x.readonly = action == 'Add'
    }
  })
}

const setFormDefaultValue = (formOptions, formFields) => {
  formOptions.flat().forEach(x => {
    if (x.addDefaultValue || x.addDefaultValue + '' === '0') {
      if ((x.type == 'date' || x.type == 'datetime') && x.addDefaultValue == 'today') {
        formFields[x.field] = common.getDate(x.type == 'datetime')
      } else {
        if (x.data?.length) {
          const isString = typeof (x.data[0].key) == 'string';
          //级联、多选、checkbox等待处理
          formFields[x.field] = isString ? (x.addDefaultValue + '') : (x.addDefaultValue * 1)
        } else {
          formFields[x.field] = x.addDefaultValue
        }
      }
    }
  })
}

export default {
  getFormValues,
  resetForm,
  getFormOption,
  getFormDicData,
  getFormDicItem,
  setFormFocus,
  getColumn,
  getColumnDicData,
  getColumnDicItem,
  setItem,
  getItem,
  setAsyncApi,
  getAsyncApi,
  getAccessToken,
  isEmptyValue,
  getSearchParameters,
  resetFileName,
  compressImage,
  convertDataToFormOptions,
  formatLongDecimal,
  convertRowsValueToString,
  setFormAddOrUpdateReadonly,
  setFormDefaultValue
}
