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
    url: '/CertCertificationBody/',
    sortName: 'Id',
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
    Code: '',
    Name: '',
    ShortName: '',
    CbCode: '',
    Status: 'active',
    ContactName: '',
    ContactPhone: '',
    Remark: '',
  };

  // ========== 3. 编辑表单选项 ==========
  // 用户明确要求「只有 2 列」 → 24 栅格每行严格 2 x colSize:12 = 24
  // 行 1：机构全称(12) + 简称(12)
  // 行 2：CNAS编号(12) + 状态(12)
  // 行 3：联系人(12) + 联系电话(12)
  // 行 4：备注(24) 整行
  // 所有字段显式 type：避免 Vol 对缺 type 字段走错误分支（输入框/下拉 v-model setter 无法写回）
  // 隐藏字段 Code 不占 colSize（Vol v-show="!item.hidden"，也不进栅格统计）
  const editFormOptions = [
    [
      { field: 'Code', type: 'input', hidden: true },
      {
        title: '机构全称',
        field: 'Name',
        type: 'input',
        required: true,
        maxlength: 200,
        colSize: 12,
      },
      {
        title: '简称',
        field: 'ShortName',
        type: 'input',
        maxlength: 100,
        colSize: 12,
      },
    ],
    [
      {
        title: 'CNAS编号',
        field: 'CbCode',
        type: 'input',
        maxlength: 50,
        colSize: 12,
      },
      {
        title: '状态',
        field: 'Status',
        type: 'select',
        dataKey: 'org_status',
        data: [],
        colSize: 12,
      },
    ],
    [
      {
        title: '联系人',
        field: 'ContactName',
        type: 'input',
        maxlength: 50,
        colSize: 12,
      },
      {
        title: '联系电话',
        field: 'ContactPhone',
        type: 'input',
        maxlength: 20,
        colSize: 12,
      },
    ],
    [
      {
        title: '备注',
        field: 'Remark',
        type: 'textarea',
        rows: 5,
        colSize: 24,
        maxlength: 1000,
      },
    ],
  ];

  // ========== 4. 搜索表单字段 ==========
  const searchFormFields = {
    Name: '',
    Status: '',
  };

  // ========== 5. 搜索表单选项 ==========
  // 注意：缺 type 的字段 Vol 不一定默认 input，为确保 v-model 能写入（与 editFormOptions 同构）
  const searchFormOptions = [
    [
      {
        title: '关键词',
        field: 'Name',
        type: 'input',
        placeholder: '机构名称/简称/CNAS编号',
        colSize: 8,
      },
      {
        title: '状态',
        field: 'Status',
        type: 'select',
        dataKey: 'org_status',
        data: [],
        colSize: 4,
      },
    ],
  ];

  // ========== 6. 表格列配置 ==========
  const columns = [
    {
      field: 'Id',
      title: 'ID',
      width: 70,
      hidden: true,
      align: 'center',
    },
    {
      field: 'CbCode',
      title: 'CNAS编号',
      width: 120,
      align: 'center',
      sortable: true,
    },
    {
      field: 'Name',
      title: '机构全称',
      width: 250,
      link: true,
      sortable: true,
    },
    {
      field: 'ShortName',
      title: '简称',
      width: 120,
      align: 'center',
    },
    {
      field: 'Status',
      title: '状态',
      width: 100,
      align: 'center',
      bind: { key: 'org_status', value: 'Status' },
    },
    {
      field: 'ContactName',
      title: '联系人',
      width: 100,
      align: 'center',
    },
    {
      field: 'ContactPhone',
      title: '联系电话',
      width: 130,
      align: 'center',
    },
    {
      field: 'CreateDate',
      title: '创建时间',
      width: 160,
      align: 'center',
      sortable: true,
    },
    {
      field: 'Remark',
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
