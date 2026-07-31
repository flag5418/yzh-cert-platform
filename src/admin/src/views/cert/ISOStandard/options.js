/**
 * ISO 标准管理 - ViewGrid 配置
 * 表名：cert_iso_standard
 */

export default function () {
  return {
    table: {
      name: 'ISOStandard',
      cnName: 'ISO 标准管理',
      url: '/api/ISOStandard/',
      pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
      sortName: 'id',
    },

    editFormFields: {
      code: '',
      cb_code: '',
      standard_code: '',
      standard_name: '',
      version_year: 2016,
      status: 'pending',
      notes: '',
    },

    editFormOptions: [
      [
        { title: 'code', field: 'code', type: 'hidden' },
        {
          title: '所属机构',
          field: 'cb_code',
          required: true,
          type: 'select',
          dataKey: 'cb_list',
          data: [],
          colSize: 12,
        },
        {
          title: '标准编号',
          field: 'standard_code',
          required: true,
          maxlength: 50,
          placeholder: '如：ISO 13485:2016',
          colSize: 8,
        },
        {
          title: '版本年份',
          field: 'version_year',
          type: 'number',
          min: 1990,
          max: 2030,
          colSize: 4,
        },
      ],
      [
        {
          title: '标准名称',
          field: 'standard_name',
          required: true,
          maxlength: 200,
          colSize: 12,
        },
        {
          title: '实施状态',
          field: 'status',
          dataKey: 'standard_status',
          data: [],
          type: 'select',
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
    ],

    searchFormFields: {
      keyword: '',
      cb_code: '',
      standard_type: '',
    },

    searchFormOptions: [
      [
        {
          title: '关键词',
          field: 'keyword',
          placeholder: '标准编号/名称',
          colSize: 6,
        },
        {
          title: '所属机构',
          field: 'cb_code',
          dataKey: 'cb_list',
          data: [],
          type: 'select',
          colSize: 4,
        },
        {
          title: '标准类型',
          field: 'standard_type',
          dataKey: 'standard_type',
          data: [],
          type: 'select',
          colSize: 4,
        },
      ],
    ],

    columns: [
      { field: 'id', title: 'ID', width: 70, hidden: true, align: 'center' },
      {
        field: 'standard_code',
        title: '标准编号',
        width: 150,
        align: 'center',
        sortable: true,
        link: true,
      },
      {
        field: 'standard_name',
        title: '标准名称',
        width: 300,
        sortable: true,
        showOverflowTooltip: true,
      },
      {
        field: 'version_year',
        title: '版本年份',
        width: 100,
        align: 'center',
        sortable: true,
      },
      {
        field: 'status',
        title: '状态',
        width: 100,
        align: 'center',
        bind: { key: 'standard_status', value: 'status' },
        render: (h, { row }) => {
          const colorMap = {
            implemented: 'success',
            pending: 'warning',
            deprecated: 'danger',
          };
          return h(
            'el-tag',
            { props: { type: colorMap[row.status] || 'info', size: 'small' } },
            row.status === 'implemented'
              ? '已实施'
              : row.status === 'pending'
              ? '待实施'
              : '已废弃'
          );
        },
      },
      {
        field: 'notes',
        title: '备注',
        width: 200,
        showOverflowTooltip: true,
      },
      {
        field: 'create_time',
        title: '创建时间',
        width: 160,
        align: 'center',
        sortable: true,
        formatter: true,
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
