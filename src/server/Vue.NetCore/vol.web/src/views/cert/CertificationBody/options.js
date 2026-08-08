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

    // ===== V3.0：以下内容由数据库 yzh_field_config 驱动 =====
    columns: [],
    editFormOptions: [],
    searchFormOptions: [],
    detail: { columns: [] },
    details: [],
  };
}
