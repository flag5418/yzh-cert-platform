/**
 * 审核任务管理 - ViewGrid 配置
 * 表名：cert_audit_task
 */

export default function () {
  const table = {
    name: 'AuditTask',
    cnName: '审核任务管理',
    url: '/api/AuditTask/',
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
    task_number: '',
    application_code: '',
    phase_type: '',
    auditor_code: '',
    status: 'pending',
    plan_start_date: '',
    plan_end_date: '',
    notes: '',
  };

  const editFormOptions = [
    [
      { title: 'code', field: 'code', type: 'hidden' },
      {
        title: '任务编号',
        field: 'task_number',
        maxlength: 50,
        colSize: 6,
      },
      {
        title: '所属申请',
        field: 'application_code',
        required: true,
        type: 'select',
        dataKey: 'application_list',
        data: [],
        colSize: 6,
      },
    ],
    [
      {
        title: '审核阶段',
        field: 'phase_type',
        required: true,
        type: 'select',
        dataKey: 'phase_type',
        data: [],
        colSize: 6,
      },
      {
        title: '审核员',
        field: 'auditor_code',
        type: 'select',
        dataKey: 'auditor_list',
        data: [],
        colSize: 6,
      },
    ],
    [
      {
        title: '状态',
        field: 'status',
        dataKey: 'task_status',
        data: [],
        type: 'select',
        colSize: 6,
      },
      {
        title: '计划开始',
        field: 'plan_start_date',
        type: 'date',
        colSize: 3,
      },
      {
        title: '计划结束',
        field: 'plan_end_date',
        type: 'date',
        colSize: 3,
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
        placeholder: '任务编号/申请编号',
        colSize: 8,
      },
      {
        title: '状态',
        field: 'status',
        dataKey: 'task_status',
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
      field: 'task_number',
      title: '任务编号',
      width: 150,
      align: 'center',
      sortable: true,
    },
    {
      field: 'application_code',
      title: '所属申请',
      width: 150,
      bind: { key: 'application_list', value: 'application_code' },
    },
    {
      field: 'phase_type',
      title: '审核阶段',
      width: 120,
      align: 'center',
      bind: { key: 'phase_type', value: 'phase_type' },
    },
    {
      field: 'auditor_code',
      title: '审核员',
      width: 120,
      bind: { key: 'auditor_list', value: 'auditor_code' },
    },
    {
      field: 'status',
      title: '状态',
      width: 100,
      align: 'center',
      bind: { key: 'task_status', value: 'status' },
    },
    {
      field: 'plan_start_date',
      title: '计划开始',
      width: 120,
      align: 'center',
    },
    {
      field: 'plan_end_date',
      title: '计划结束',
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
