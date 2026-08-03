/**
 * 认证机构管理 - ViewGrid 配置
 * 表名：cert_certification_body
 * 基于Vol框架标准view-grid模式
 */

export default function () {
  return {
    table: {
      // 表名（必须与后端实体一致）
      name: 'CertificationBody',
      // 中文名称
      cnName: '认证机构管理',
      // API接口路径前缀
      url: '/CertCertificationBody/',
      // 分页配置
      pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
      // 排序字段
      sortName: 'Id',
    },

    // 编辑表单字段定义
    editFormFields: {
      Code: '',
      Name: '',
      ShortName: '',
      CbCode: '',
      Status: 'active',
      ContactName: '',
      ContactPhone: '',
      Remark: '',
    },

    // 编辑表单选项配置
    editFormOptions: [
      [
        { title: '基本信息', field: 'Code', type: 'hidden' },
        {
          title: '机构全称',
          field: 'Name',
          required: true,
          maxlength: 200,
          colSize: 12,
        },
        {
          title: '简称',
          field: 'ShortName',
          maxlength: 100,
          colSize: 6,
        },
        {
          title: 'CNAS编号',
          field: 'CbCode',
          maxlength: 50,
          colSize: 6,
        },
      ],
      [
        {
          title: '状态',
          field: 'Status',
          dataKey: 'org_status',
          data: [],
          type: 'select',
          colSize: 6,
        },
        {
          title: '联系人',
          field: 'ContactName',
          maxlength: 50,
          colSize: 6,
        },
        {
          title: '联系电话',
          field: 'ContactPhone',
          maxlength: 20,
          colSize: 6,
        },
      ],
      [
        {
          title: '备注',
          field: 'Remark',
          type: 'textarea',
          rows: 3,
          colSize: 12,
        },
      ],
    ],

    // 搜索表单字段
    searchFormFields: {
      keyword: '',
      Status: '',
    },

    // 搜索表单选项
    searchFormOptions: [
      [
        {
          title: '关键词',
          field: 'keyword',
          placeholder: '机构名称/简称/CNAS编号',
          colSize: 8,
        },
        {
          title: '状态',
          field: 'Status',
          dataKey: 'org_status',
          data: [],
          type: 'select',
          colSize: 4,
        },
      ],
    ],

    // 表格列配置
    columns: [
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
    ],

    // 明细表配置（暂无）
    detail: null,
    details: [],

    // 扩展配置
    extend: {
      // 自定义按钮
      buttons: [],
      methods: {},
    },
  };
}
