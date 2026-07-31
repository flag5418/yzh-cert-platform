/**
 * 认证机构管理 - ViewGrid 配置
 * 表名：cert_certification_body
 * 基于Vol框架标准view-grid模式
 */

export default function () {
  // ========== 1. 表格基本配置 ==========
  const table = {
    name: 'CertificationBody',
    cnName: '认证机构管理',
    url: '/api/CertCertificationBody/',
    sortName: 'id',
    key: 'Id',
    footer: 'Foots',
    pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
  };

  const tableName = table.name;
  const tableCNName = table.cnName;
  const newTabEdit = false;
  const key = table.key;

  // ========== 2. 编辑表单字段 ==========
  const editFormFields = {
    code: '',
    name: '',
    short_name: '',
    cb_code: '',
    status: 'active',
    contact_name: '',
    contact_phone: '',
    notes: '',
  };

  // ========== 3. 编辑表单选项 ==========
  const editFormOptions = [
    [
      { title: '基本信息', field: 'code', type: 'hidden' },
      {
        title: '机构全称',
        field: 'name',
        required: true,
        maxlength: 200,
        colSize: 12,
      },
      {
        title: '简称',
        field: 'short_name',
        maxlength: 100,
        colSize: 6,
      },
      {
        title: 'CNAS编号',
        field: 'cb_code',
        maxlength: 50,
        colSize: 6,
      },
    ],
    [
      {
        title: '状态',
        field: 'status',
        dataKey: 'org_status',
        data: [],
        type: 'select',
        colSize: 6,
      },
      {
        title: '联系人',
        field: 'contact_name',
        maxlength: 50,
        colSize: 6,
      },
      {
        title: '联系电话',
        field: 'contact_phone',
        maxlength: 20,
        colSize: 6,
      },
    ],
    [
      {
        title: '备注',
        field: 'notes',
        type: 'textarea',
        rows: 3,
        colSize: 12,
      },
    ],
  ];

  // ========== 4. 搜索表单字段 ==========
  const searchFormFields = {
    keyword: '',
    status: '',
  };

  // ========== 5. 搜索表单选项 ==========
  const searchFormOptions = [
    [
      {
        title: '关键词',
        field: 'keyword',
        placeholder: '机构名称/简称/CNAS编号',
        colSize: 8,
      },
      {
        title: '状态',
        field: 'status',
        dataKey: 'org_status',
        data: [],
        type: 'select',
        colSize: 4,
      },
    ],
  ];

  // ========== 6. 表格列配置 ==========
  const columns = [
    {
      field: 'id',
      title: 'ID',
      width: 70,
      hidden: true,
      align: 'center',
    },
    {
      field: 'cb_code',
      title: 'CNAS编号',
      width: 120,
      align: 'center',
      sortable: true,
    },
    {
      field: 'name',
      title: '机构全称',
      width: 250,
      link: true,
      sortable: true,
    },
    {
      field: 'short_name',
      title: '简称',
      width: 120,
      align: 'center',
    },
    {
      field: 'status',
      title: '状态',
      width: 100,
      align: 'center',
      bind: { key: 'org_status', value: 'status' },
    },
    {
      field: 'contact_name',
      title: '联系人',
      width: 100,
      align: 'center',
    },
    {
      field: 'contact_phone',
      title: '联系电话',
      width: 130,
      align: 'center',
    },
    {
      field: 'create_time',
      title: '创建时间',
      width: 160,
      align: 'center',
      sortable: true,
      formatter: true,
    },
    {
      field: 'notes',
      title: '备注',
      width: 200,
      showOverflowTooltip: true,
    },
  ];

  // ========== 7. 明细表配置 ==========
  const detail = { columns: [] };
  const details = [];

  // ========== 8. 返回 Vol 框架必需的所有字段 ==========
  return {
    table,
    key,
    tableName,
    tableCNName,
    newTabEdit,
    editFormFields,
    editFormOptions,
    searchFormFields,
    searchFormOptions,
    columns,
    detail,
    details,
  };
}
