/**
 * 审核任务管理 - ViewGrid 配置
 * 表名：audit_task
 * 核心页面：展示和管理各审核阶段的任务
 */

export default function () {
  return {
    table: {
      name: 'AuditTask',
      cnName: '审核任务管理',
      url: '/AuditTask/',
      pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
      sortName: 'PlannedDate',
      sort: 'asc',
    },

    editFormFields: {
      Code: '',
      PhaseCode: '',
      TaskNumber: '',
      AuditorId: null,
      Status: 'pending',
      PlannedDate: null,
      ActualStartDate: null,
      ActualCompleteDate: null,
      AuditScope: '',
      Remark: '',
    },

    editFormOptions: [
      [
        { title: 'Code', field: 'Code', type: 'hidden' },
        {
          title: '所属项目',
          field: 'PhaseCode',
          required: true,
          readonly: true,
          colSize: 12,
        },
        {
          title: '任务编号',
          field: 'TaskNumber',
          readonly: true,
          colSize: 6,
        },
      ],
      [
        {
          title: '审核员',
          field: 'AuditorId',
          dataKey: 'auditor_list',
          data: [],
          type: 'select',
          colSize: 6,
        },
        {
          title: '计划日期',
          field: 'PlannedDate',
          type: 'date',
          colSize: 6,
        },
        {
          title: '实际开始日期',
          field: 'ActualStartDate',
          type: 'datetime',
          colSize: 6,
        },
        {
          title: '实际完成日期',
          field: 'ActualCompleteDate',
          type: 'datetime',
          colSize: 6,
        },
      ],
      [
        {
          title: '审核范围',
          field: 'AuditScope',
          type: 'textarea',
          rows: 3,
          placeholder: '详细描述本次审核的范围和重点',
          colSize: 12,
        },
      ],
      [
        {
          title: '备注',
          field: 'Remark',
          type: 'textarea',
          rows: 2,
          colSize: 12,
        },
      ],
    ],

    searchFormFields: {
      TaskNumber: '',
      Status: '',
      PhaseCode: '',
      AuditorId: '',
    },

    searchFormOptions: [
      [
        {
          title: '关键词',
          field: 'TaskNumber',
          placeholder: '任务编号',
          colSize: 4,
        },
        {
          title: '状态',
          field: 'Status',
          dataKey: 'task_status',
          data: [],
          type: 'select',
          colSize: 3,
        },
        {
          title: '审核项目',
          field: 'PhaseCode',
          dataKey: 'project_list',
          data: [],
          type: 'select',
          colSize: 3,
        },
        {
          title: '审核员',
          field: 'AuditorId',
          dataKey: 'user_list',
          data: [],
          type: 'select',
          colSize: 4,
        },
      ],
    ],

    columns: [
      { field: 'Id', title: 'ID', width: 70, hidden: true, align: 'center' },
      {
        field: 'TaskNumber',
        title: '任务编号',
        width: 180,
        align: 'center',
        sortable: true,
        link: true,
      },
      {
        field: 'Status',
        title: '状态',
        width: 110,
        align: 'center',
        bind: { key: 'task_status', value: 'Status' },
      },
      {
        field: 'PlannedDate',
        title: '计划日期',
        width: 120,
        align: 'center',
        sortable: true,
      },
      {
        field: 'ActualStartDate',
        title: '实际开始',
        width: 160,
        align: 'center',
      },
      {
        field: 'ActualCompleteDate',
        title: '实际完成',
        width: 160,
        align: 'center',
      },
      {
        field: 'Remark',
        title: '备注',
        width: 200,
        showOverflowTooltip: true,
      },
    ],

    detail: null,
    details: [],

    extend: {
      buttons: [],
      methods: {},
    },
  };
}
