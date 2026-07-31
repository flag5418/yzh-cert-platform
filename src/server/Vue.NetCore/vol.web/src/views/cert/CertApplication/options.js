/**
 * 认证申请管理 - ViewGrid 配置
 * 表名：cert_application
 */

export default function () {
  const table = {
    name: 'CertApplication',
    cnName: '认证申请管理',
    url: '/api/CertApplication/',
    sortName: 'id',
    key: 'Id',
    footer: 'Foots',
    pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
  };

  const tableName = table.name;
  const tableCNName = table.cnName;
  const newTabEdit = false;
  const key = table.key;

  const editFormFields = {
    code: '',
    application_no: '',
    ent_code: '',
    cb_code: '',
    standard_code: '',
    status: 'draft',
    apply_date: '',
    notes: '',
  };

  const editFormOptions = [
    [
      { title: 'code', field: 'code', type: 'hidden' },
      {
        title: '申请编号',
        field: 'application_no',
        maxlength: 50,
        colSize: 6,
      },
      {
        title: '企业',
        field: 'ent_code',
        required: true,
        type: 'select',
        dataKey: 'ent_list',
        data: [],
        colSize: 6,
      },
    ],
    [
      {
        title: '认证机构',
        field: 'cb_code',
        required: true,
        type: 'select',
        dataKey: 'cb_list',
        data: [],
        colSize: 6,
      },
      {
        title: '认证标准',
        field: 'standard_code',
        required: true,
        type: 'select',
        dataKey: 'standard_list',
        data: [],
        colSize: 6,
      },
    ],
    [
      {
        title: '状态',
        field: 'status',
        dataKey: 'application_status',
        data: [],
        type: 'select',
        colSize: 6,
      },
      {
        title: '申请日期',
        field: 'apply_date',
        type: 'date',
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

  const searchFormFields = {
    keyword: '',
    status: '',
  };

  const searchFormOptions = [
    [
      {
        title: '关键词',
        field: 'keyword',
        placeholder: '申请编号/企业名称',
        colSize: 8,
      },
      {
        title: '状态',
        field: 'status',
        dataKey: 'application_status',
        data: [],
        type: 'select',
        colSize: 4,
      },
    ],
  ];

  const columns = [
    {
      field: 'id',
      title: 'ID',
      width: 70,
      hidden: true,
      align: 'center',
    },
    {
      field: 'application_no',
      title: '申请编号',
      width: 150,
      align: 'center',
      sortable: true,
    },
    {
      field: 'ent_code',
      title: '企业',
      width: 200,
      bind: { key: 'ent_list', value: 'ent_code' },
    },
    {
      field: 'cb_code',
      title: '认证机构',
      width: 150,
      bind: { key: 'cb_list', value: 'cb_code' },
    },
    {
      field: 'standard_code',
      title: '认证标准',
      width: 150,
      bind: { key: 'standard_list', value: 'standard_code' },
    },
    {
      field: 'status',
      title: '状态',
      width: 100,
      align: 'center',
      bind: { key: 'application_status', value: 'status' },
    },
    {
      field: 'apply_date',
      title: '申请日期',
      width: 120,
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
  ];

  const detail = { columns: [] };
  const details = [];

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
