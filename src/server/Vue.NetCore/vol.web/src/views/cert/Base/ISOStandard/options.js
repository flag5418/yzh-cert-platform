/**
 * ISO 标准注册（全局基础资料）
 *
 * 改造说明（2026-08-07）：
 * - 从机构从表改造为全局独立基础资料
 * - 移除 CbCode 字段，不再属于某个认证机构
 * - 机构和标准的关系通过 cert_org_standard 关联表管理
 */
export default function () {
  const table = {
    name: "ISOStandard",
    cnName: "ISO 标准注册",
    url: "/ISOStandard/",
    sortName: "CreateDate",
    key: "Id",
    footer: "Foots",
    pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
  };

  const editFormFields = {
    StandardCode: "",
    StandardName: "",
    VersionYear: new Date().getFullYear(),
    Category: "quality",
    Status: "draft",
    Description: "",
    Remark: "",
  };

  const searchFormFields = {
    keyword: "",
    Category: "",
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

    // ===== 列定义（V3.0：后续可迁移到 yzh_field_config 表） =====
    columns: [
      { field: 'Id', title: 'ID', width: 70, align: 'center', hidden: true },
      { field: 'StandardCode', title: '标准编号', width: 160, sortable: true },
      { field: 'StandardName', title: '标准名称', width: 280, sortable: true, showOverflow: true },
      { field: 'VersionYear', title: '版本', width: 80, align: 'center' },
      { field: 'Category', title: '分类', width: 100, align: 'center',
        dataKey: 'iso_category' },
      { field: 'Status', title: '状态', width: 100, align: 'center',
        dataKey: 'standard_status' },
    ],
    editFormOptions: [
      [
        { field: 'StandardCode', title: '标准编号', type: 'input', required: true, placeholder: '如 ISO 9001:2015' },
        { field: 'StandardName', title: '标准名称', type: 'input', required: true, colSize: 2 },
        { field: 'VersionYear', title: '版本年份', type: 'number', required: false },
        { field: 'Category', title: '分类', type: 'select', dataKey: 'iso_category' },
        { field: 'Status', title: '状态', type: 'select', dataKey: 'standard_status' },
        { field: 'Description', title: '描述', type: 'textarea', rows: 3 },
        { field: 'Remark', title: '备注', type: 'textarea', rows: 2 },
      ],
    ],
    searchFormOptions: [
      [
        { field: 'keyword', title: '关键词', type: 'input', placeholder: '搜索标准编号/名称' },
        { field: 'Category', title: '分类', type: 'select', dataKey: 'iso_category' },
        { field: 'Status', title: '状态', type: 'select', dataKey: 'standard_status' },
      ],
    ],
    detail: { columns: [] },
    details: [],
  };
}
