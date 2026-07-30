import buttons from '@/api/buttons.js'
import store from '@/store/index.js'

const checkData = (permission) => {
  if (!permission || (Array.isArray(permission) && !permission?.length)) {
    return false;
  }
  return true;
}

const getButtons = (path, extra, table, tableName) => {
  if (tableName) {
    tableName = '/' + ((tableName + '').split('/').pop());
  }
  let orgTable;
  //打开的不是当前页面优先读取当前表的菜单权限
  if (tableName && path != tableName) {
    orgTable = tableName
  }
  if(!path){
    path=''
  }
  //extra自定额外按钮
  //table获取指定表的权限
  if (table && table[0] != '/') {
    table = '/' + table
  }
  //优先查询当前是否有权限
  let permission = store.getters.getPermission(orgTable || table || path)
  if (!checkData(permission)) {
    //当前表没权限，查询当前路由的权限
    if (orgTable) {
      permission = store.getters.getPermission(path)
    }
    if (!checkData(permission)) {
      //查询表名与路由不一致的权限
      permission = store.getters.getPermission(path.substring(1))
      if (!permission) {
        //查询指定表权限
        permission = store.getters.getPermission(tableName)
        if (!checkData(permission)) {
          permission = { permission: ['Search'] }
        }
      }
    }
  }
  let permissions = permission.permission || ['Search']
  let gridButtons = buttons
    .filter((item) => {
      return !item.value || permissions.indexOf(item.value) != -1
    })
    .map((x) => {
      return Object.assign({}, x)
    })

  if (extra && extra instanceof Array) {
    gridButtons.push(...extra)
  }
  return gridButtons
}
const getAuthButtons = (table) => {
  //return getButtons(null, null, table)
   const buttons= store.getters.getPermission().find(x=>{return x.tableName==table});
   if (!buttons||!buttons.permission) {
      return []
   }
    return buttons.permission.map(x=>{return {value:x.value||x}});
}
const hasAuthButton = (table, buttonName) => {
  return getAuthButtons(table).some((x) => {
    return x.value == buttonName
  })
}

export default { getButtons, getAuthButtons, hasAuthButton }
