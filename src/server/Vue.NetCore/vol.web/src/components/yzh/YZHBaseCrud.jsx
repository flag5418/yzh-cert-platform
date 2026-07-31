/**
 * YZHBaseCrud.jsx - YZH 基础 CRUD 窗体扩展
 * 
 * 此文件为兼容 Vol 框架的扩展点，实际业务逻辑请在 Vue 文件中编写
 * 
 * @author CertPlatform
 * @date 2026-07-31
 */

export default {
  onInit() {
    // ViewGrid 初始化时的扩展逻辑
  },
  onInited() {
    // 初始化完成后的扩展逻辑
  },
  searchBefore(param) {
    // 查询前处理
    return true;
  },
  addBefore(formData) {
    // 新增前处理
    return true;
  },
  addAfter(result, formData) {
    // 新增后处理
    return true;
  },
  updateBefore(formData) {
    // 编辑前处理
    return true;
  },
  updateAfter(result, formData) {
    // 编辑后处理
    return true;
  },
  delBefore(delKeys, rows) {
    // 删除前处理
    return true;
  },
  delAfter(result, rows) {
    // 删除后处理
    return true;
  },
};
