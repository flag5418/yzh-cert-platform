/**
 * 认证机构管理 - YZH V3.0 配置驱动模式
 *
 * V3.0 改造：columns / editFormOptions / searchFormOptions 全部由数据库驱动
 * 本文件仅保留最小化元数据（table 配置 + 字段默认值）
 *
 * 数据库配置来源：yzh_page_config (page_key='CertificationBody') + yzh_field_config
 */

export default function () {
  const table = {
    name: 'CertificationBody',
    cnName: '认证机构管理',
    url: '/CertCertificationBody/',
    sortName: 'Id',
    key: 'Id',
    footer: 'Foots',
    pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
  };

  // ========== 编辑表单字段默认值（新增时的初始值） ==========
  const editFormFields = {
    Code: '',
    Name: '',
    ShortName: '',
    CbCode: '',
    Status: 'active',
    ContactName: '',
    ContactPhone: '',
    Remark: '',
  };

  // ========== 搜索字段默认值 ==========
  const searchFormFields = {
    Name: '',
    Status: '',
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
      { field: 'Name', title: '机构名称', width: 250, sortable: true, showOverflow: true },
      { field: 'ShortName', title: '简称', width: 120 },
      { field: 'CbCode', title: '机构编号', width: 120, sortable: true },
      { field: 'LegalPerson', title: '法人', width: 100 },
      { field: 'ContactName', title: '联系人', width: 100 },
      { field: 'ContactPhone', title: '联系电话', width: 130 },
      { field: 'ContactEmail', title: '邮箱', width: 180, showOverflow: true },
      { field: 'Status', title: '状态', width: 80, align: 'center', bind: { key: 'org_status' } },
      { field: 'Creator', title: '创建人', width: 100 },
      { field: 'CreateDate', title: '创建时间', width: 160, sortable: true },
    ],
    editFormOptions: [
      [
        { field: 'Name', title: '机构名称', type: 'input', required: true, colSize: 2 },
        { field: 'ShortName', title: '简称', type: 'input', colSize: 1 },
        { field: 'CbCode', title: '机构编号', type: 'input', required: true, colSize: 1 },
      ],
      [
        { field: 'LegalPerson', title: '法人', type: 'input', colSize: 1 },
        { field: 'ContactName', title: '联系人', type: 'input', colSize: 1 },
        { field: 'ContactPhone', title: '联系电话', type: 'input', colSize: 1 },
        { field: 'ContactEmail', title: '邮箱', type: 'input', colSize: 1 },
      ],
      [
        { field: 'Address', title: '地址', type: 'textarea', rows: 2, colSize: 2 },
        { field: 'Status', title: '状态', type: 'select', dataKey: 'org_status', colSize: 1 },
        { field: 'Remark', title: '备注', type: 'textarea', rows: 2, colSize: 1 },
      ],
    ],
    searchFormOptions: [
      [
        { field: 'Name', title: '关键词', type: 'input', placeholder: '机构名称/简称' },
        { field: 'Status', title: '状态', type: 'select', dataKey: 'org_status' },
      ],
    ],
    detail: { columns: [] },
    details: [],
  };
}
