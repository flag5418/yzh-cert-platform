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
      url: '/api/AuditTask/',
      pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
      sortName: 'planned_date',
      sort: 'asc',
    },

    editFormFields: {
      code: '',
      phase_code: '',
      task_number: '',
      auditor_id: null,
      status: 'pending_assignment',
      planned_date: null,
      actual_start_date: null,
      actual_complete_date: null,
      audit_scope: '',
      notes: '',
    },

    editFormOptions: [
      [
        { title: 'code', field: 'code', type: 'hidden' },
        {
          title: '所属项目',
          field: 'phase_code',
          required: true,
          readonly: true,
          colSize: 12,
        },
        {
          title: '任务编号',
          field: 'task_number',
          readonly: true,
          colSize: 6,
        },
        {
          title: '审核阶段',
          field: 'phase_type',
          dataKey: 'audit_phase',
          data: [],
          type: 'select',
          readonly: true,
          colSize: 6,
        },
      ],
      [
        {
          title: '审核员',
          field: 'auditor_id',
          dataKey: 'auditor_list',
          data: [],
          type: 'select',
          colSize: 6,
        },
        {
          title: '计划日期',
          field: 'planned_date',
          type: 'date',
          colSize: 6,
        },
        {
          title: '实际开始日期',
          field: 'actual_start_date',
          type: 'datetime',
          colSize: 6,
        },
        {
          title: '实际完成日期',
          field: 'actual_complete_date',
          type: 'datetime',
          colSize: 6,
        },
      ],
      [
        {
          title: '审核范围',
          field: 'audit_scope',
          type: 'textarea',
          rows: 3,
          placeholder: '详细描述本次审核的范围和重点',
          colSize: 12,
        },
      ],
      [
        {
          title: '备注',
          field: 'notes',
          type: 'textarea',
          rows: 2,
          colSize: 12,
        },
      ],
    ],

    searchFormFields: {
      keyword: '',
      status: '',
      phase_code: '',
      auditor_id: '',
    },

    searchFormOptions: [
      [
        {
          title: '关键词',
          field: 'keyword',
          placeholder: '任务编号',
          colSize: 4,
        },
        {
          title: '状态',
          field: 'status',
          dataKey: 'task_status',
          data: [],
          type: 'select',
          colSize: 3,
        },
        {
          title: '审核项目',
          field: 'phase_code',
          dataKey: 'project_list',
          data: [],
          type: 'select',
          colSize: 3,
        },
        {
          title: '审核员',
          field: 'auditor_id',
          dataKey: 'user_list',
          data: [],
          type: 'select',
          colSize: 4,
        },
      ],
    ],

    columns: [
      { field: 'id', title: 'ID', width: 70, hidden: true, align: 'center' },
      {
        field: 'task_number',
        title: '任务编号',
        width: 180,
        align: 'center',
        sortable: true,
        link: true,
      },
      {
        field: 'phase_name',
        title: '审核阶段',
        width: 140,
        align: 'center',
        render: (h, { row }) => {
          const phaseConfig = {
            application_review: { text: '申请受理', icon: 'el-icon-document-checked' },
            document_review: { text: '文件评审', icon: 'el-icon-document' },
            stage1_audit: { text: '一阶段审核', icon: 'el-icon-search' },
            stage2_audit: { text: '二阶段审核', icon: 'el-icon-search' },
            certification_decision: { text: '认证决定', icon: 'el-icon-finished' },
          };
          const config = phaseConfig[row.phase_type] || { text: row.phase_type };
          return h(
            'el-tag',
            { props: { type: '', size: 'small' } },
            config.text
          );
        },
      },
      {
        field: 'auditor_name',
        title: '审核员',
        width: 100,
        align: 'center',
      },
      {
        field: 'status',
        title: '状态',
        width: 110,
        align: 'center',
        render: (h, { row }) => {
          const statusConfig = {
            pending_assignment: { text: '待分配', type: 'info' },
            pending_start: { text: '待开始', type: 'warning' },
            in_progress: { text: '进行中', type: 'danger' },
            completed: { text: '已完成', type: 'success' },
            paused: { text: '已暂停', type: 'warning' },
            cancelled: { text: '已取消', type: 'info' },
          };
          const config = statusConfig[row.status] || { text: row.status, type: 'info' };
          return h('el-tag', { props: { type: config.type, size: 'small' } }, config.text);
        },
      },
      {
        field: 'planned_date',
        title: '计划日期',
        width: 120,
        align: 'center',
        sortable: true,
        formatter: true,
      },
      {
        field: 'actual_start_date',
        title: '实际开始',
        width: 160,
        align: 'center',
        formatter: true,
      },
      {
        field: 'actual_complete_date',
        title: '实际完成',
        width: 160,
        align: 'center',
        formatter: true,
      },
      {
        field: 'notes',
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
