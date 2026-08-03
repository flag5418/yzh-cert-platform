/**
 * ISO 标准管理 - ViewGrid 配置
 * 表名：cert_iso_standard
 */

export default function () {
  return {
    table: {
      name: 'ISOStandard',
      cnName: 'ISO 标准管理',
      url: '/ISOStandard/',
      pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
      sortName: 'Id',
    },

    editFormFields: {
      Code: '',
      CbCode: '',
      StandardCode: '',
      StandardName: '',
      VersionYear: 2016,
      Status: 'pending',
      Remark: '',
    },

    editFormOptions: [
      [
        { title: 'Code', field: 'Code', type: 'hidden' },
        {
          title: '所属机构',
          field: 'CbCode',
          required: true,
          type: 'select',
          dataKey: 'cb_list',
          data: [],
          colSize: 12,
        },
        {
          title: '标准编号',
          field: 'StandardCode',
          required: true,
          maxlength: 50,
          placeholder: '如：ISO 13485:2016',
          colSize: 8,
        },
        {
          title: '版本年份',
          field: 'VersionYear',
          type: 'number',
          min: 1990,
          max: 2030,
          colSize: 4,
        },
      ],
      [
        {
          title: '标准名称',
          field: 'StandardName',
          required: true,
          maxlength: 200,
          colSize: 12,
        },
        {
          title: '实施状态',
          field: 'Status',
          dataKey: 'standard_status',
          data: [],
          type: 'select',
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

    searchFormFields: {
      StandardName: '',
      Status: '',
      CbCode: '',
    },

    searchFormOptions: [
      [
        {
          title: '关键词',
          field: 'StandardName',
          placeholder: '标准编号/名称',
          colSize: 8,
        },
        {
          title: '状态',
          field: 'Status',
          dataKey: 'standard_status',
          data: [],
          type: 'select',
          colSize: 4,
        },
      ],
    ],

    columns: [
      { field: 'Id', title: 'ID', width: 70, hidden: true, align: 'center' },
      {
        field: 'StandardCode',
        title: '标准编号',
        width: 150,
        align: 'center',
        sortable: true,
        link: true,
      },
      {
        field: 'StandardName',
        title: '标准名称',
        width: 300,
        sortable: true,
        showOverflowTooltip: true,
      },
      {
        field: 'VersionYear',
        title: '版本年份',
        width: 100,
        align: 'center',
        sortable: true,
      },
      {
        field: 'Status',
        title: '状态',
        width: 100,
        align: 'center',
        bind: { key: 'standard_status', value: 'Status' },
      },
      {
        field: 'Remark',
        title: '备注',
        width: 200,
        showOverflowTooltip: true,
      },
      {
        field: 'CreateDate',
        title: '创建时间',
        width: 160,
        align: 'center',
        sortable: true,
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
