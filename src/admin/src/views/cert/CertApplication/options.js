/**
 * 认证申请管理 - ViewGrid 配置
 * 表名：cert_application
 * 核心业务页面：展示企业提交的认证申请及审核进度
 */

export default function () {
  return {
    table: {
      name: 'CertApplication',
      cnName: '认证申请管理',
      url: '/CertApplication/',
      pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
      sortName: 'CreateDate',
      sort: 'desc',
    },

    editFormFields: {
      Code: '',
      ApplicationNo: '',
      CbCode: '',
      StandardCode: '',
      EnterpriseCode: '',
      CertType: 'QMS',
      ScopeText: '',
      Status: 'draft',
      Remark: '',
    },

    editFormOptions: [
      [
        { title: 'Code', field: 'Code', type: 'hidden' },
        {
          title: '申请编号',
          field: 'ApplicationNo',
          readonly: true,
          colSize: 6,
        },
        {
          title: '认证类型',
          field: 'CertType',
          required: true,
          dataKey: 'cert_type',
          data: [],
          type: 'select',
          colSize: 6,
        },
      ],
      [
        {
          title: '认证机构',
          field: 'CbCode',
          required: true,
          type: 'select',
          dataKey: 'cb_list',
          data: [],
          colSize: 12,
        },
        {
          title: '认证标准',
          field: 'StandardCode',
          required: true,
          type: 'select',
          dataKey: 'standard_list',
          data: [],
          colSize: 12,
        },
        {
          title: '申请企业',
          field: 'EnterpriseCode',
          required: true,
          type: 'select',
          dataKey: 'enterprise_list',
          data: [],
          colSize: 12,
        },
      ],
      [
        {
          title: '认证范围',
          field: 'ScopeText',
          required: true,
          type: 'textarea',
          rows: 4,
          placeholder: '详细描述需要认证的产品、过程或服务范围',
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
      keyword: '',
      Status: '',
      CbCode: '',
      EnterpriseCode: '',
      dateRange: [],
    },

    searchFormOptions: [
      [
        {
          title: '关键词',
          field: 'keyword',
          placeholder: '申请编号/企业名称',
          colSize: 4,
        },
        {
          title: '状态',
          field: 'Status',
          dataKey: 'application_status',
          data: [],
          type: 'select',
          colSize: 3,
        },
        {
          title: '机构',
          field: 'CbCode',
          dataKey: 'cb_list',
          data: [],
          type: 'select',
          colSize: 3,
        },
        {
          title: '申请日期',
          field: 'dateRange',
          type: 'date',
          range: true,
          colSize: 4,
        },
      ],
    ],

    columns: [
      { field: 'Id', title: 'ID', width: 70, hidden: true, align: 'center' },
      {
        field: 'ApplicationNo',
        title: '申请编号',
        width: 180,
        align: 'center',
        sortable: true,
        link: true,
      },
      {
        field: 'EnterpriseName',
        title: '申请企业',
        width: 220,
        sortable: true,
        showOverflowTooltip: true,
      },
      {
        field: 'StandardName',
        title: '认证标准',
        width: 160,
        align: 'center',
        render: (h, { row }) => {
          return h(
            'el-tag',
            { type: '', size: 'small' },
            row.StandardName || 'ISO 13485:2016'
          );
        },
      },
      {
        field: 'CertType',
        title: '类型',
        width: 80,
        align: 'center',
        bind: { key: 'cert_type', value: 'CertType' },
      },
      {
        field: 'Status',
        title: '状态',
        width: 120,
        align: 'center',
        render: (h, { row }) => {
          const statusConfig = {
            draft: { text: '草稿', type: 'info' },
            submitted: { text: '已提交', type: '' },
            accepted: { text: '受理中', type: 'warning' },
            doc_reviewing: { text: '文件评审中', type: 'warning' },
            auditing: { text: '审核中', type: 'danger' },
            completed_pass: { text: '已通过', type: 'success' },
            completed_fail: { text: '未通过', type: 'danger' },
            rejected: { text: '已拒绝', type: 'danger' },
            cancelled: { text: '已取消', type: 'info' },
          };
          const config = statusConfig[row.Status] || { text: row.Status, type: 'info' };
          return h('el-tag', { type: config.type, size: 'small' }, config.text);
        },
      },
      {
        field: 'SubmitTime',
        title: '提交时间',
        width: 160,
        align: 'center',
        sortable: true,
      },
      {
        field: 'CompleteTime',
        title: '完成时间',
        width: 160,
        align: 'center',
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
