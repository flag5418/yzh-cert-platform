/**
 * ISO 标准管理 - YZH V3.0 配置驱动模式
 *
 * V3.0 改造：columns / editFormOptions / searchFormOptions 全部由数据库驱动
 * 本文件仅保留最小化元数据（table 配置 + 字段默认值）
 *
 * 数据库配置来源：yzh_page_config (page_key='ISOStandard') + yzh_field_config
 */

export default function () {
  const table = {
    name: "ISOStandard",
    cnName: "ISO 标准管理",
    url: "/ISOStandard/",
    sortName: "CreateDate",
    key: "Id",
    footer: "Foots",
    pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
  };

  // ========== 编辑表单字段默认值（新增时的初始值） ==========
  const editFormFields = {
    CbCode: "",          // 由左树自动填充
    StandardCode: "",
    StandardName: "",
    VersionYear: new Date().getFullYear(),
    Status: "draft",
    Remark: "",
  };

  // ========== 搜索字段默认值 ==========
  const searchFormFields = {
    keyword: "",
    Status: "",
  };

  return {
    table,
    key: table.key,
    tableName: table.name,
    tableCNName: table.cnName,
    newTabEdit: false,
    editFormFields,
    searchFormFields,

    columns: [
      { field: 'Id', title: 'ID', width: 70, align: 'center', hidden: true },
      { field: 'StandardCode', title: '标准编号', width: 150, sortable: true },
      { field: 'StandardName', title: '标准名称', width: 250, sortable: true, showOverflow: true },
      { field: 'VersionYear', title: '版本', width: 80, align: 'center' },
      { field: 'CategoryName', title: '分类', width: 100, align: 'center' },
      { field: 'StatusName', title: '状态', width: 80, align: 'center' },
      { field: 'CbCode', title: '机构编号', width: 120 },
    ],
    editFormOptions: [
      [
        { field: 'CbCode', title: '机构编号', type: 'input', required: true, colSize: 1 },
        { field: 'StandardCode', title: '标准编号', type: 'input', required: true, placeholder: '如 ISO 9001:2015', colSize: 1 },
      ],
      [
        { field: 'StandardName', title: '标准名称', type: 'input', required: true, colSize: 2 },
      ],
      [
        { field: 'VersionYear', title: '版本年份', type: 'number', colSize: 1 },
        { field: 'Category', title: '分类', type: 'select', dataKey: 'iso_category', colSize: 1 },
        { field: 'Status', title: '状态', type: 'select', dataKey: 'standard_status', colSize: 1 },
        { field: 'Remark', title: '备注', type: 'textarea', rows: 2, colSize: 1 },
      ],
    ],
    searchFormOptions: [
      [
        { field: 'keyword', title: '关键词', type: 'input', placeholder: '标准编号/名称' },
        { field: 'Status', title: '状态', type: 'select', dataKey: 'standard_status' },
      ],
    ],
    detail: { columns: [] },
    details: [],
  };
}
