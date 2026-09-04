/**
 * 标准条款管理 - ViewGrid 配置
 * 表名：cert_iso_clause
 */

export default function () {
  return {
    table: {
      name: 'ISOClause',
      cnName: '标准条款管理',
      url: '/ISOClause/',
      pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
      sortName: 'SortOrder',
      sort: 'asc',
    },

    editFormFields: {
      Code: '',
      StandardCode: '',
      ParentCode: '',
      ClauseNumber: '',
      Title: '',
      Description: '',
      SortOrder: 0,
    },

    editFormOptions: [
      [
        { title: 'Code', field: 'Code', type: 'hidden' },
        {
          title: '所属标准',
          field: 'StandardCode',
          required: true,
          type: 'select',
          dataKey: 'standard_list',
          data: [],
          colSize: 12,
        },
      ],
      [
        {
          title: '条款编号',
          field: 'ClauseNumber',
          required: true,
          maxlength: 20,
          placeholder: '如：7.1.1',
          colSize: 6,
        },
        {
          title: '父级条款',
          field: 'ParentCode',
          type: 'select',
          dataKey: 'clause_list',
          data: [],
          colSize: 6,
        },
      ],
      [
        {
          title: '条款标题',
          field: 'Title',
          required: true,
          maxlength: 200,
          colSize: 12,
        },
      ],
      [
        {
          title: '条款描述/要求',
          field: 'Description',
          type: 'textarea',
          rows: 5,
          colSize: 12,
        },
      ],
      [
        {
          title: '排序权重',
          field: 'SortOrder',
          type: 'number',
          colSize: 6,
        },
      ],
    ],

    searchFormFields: {
      Title: '',
      StandardCode: '',
      ClauseNumber: '',
    },

    searchFormOptions: [
      [
        {
          title: '所属标准',
          field: 'StandardCode',
          dataKey: 'standard_list',
          data: [],
          type: 'select',
          colSize: 4,
        },
        {
          title: '编号',
          field: 'ClauseNumber',
          colSize: 3,
        },
        {
          title: '标题关键词',
          field: 'Title',
          colSize: 5,
        },
      ],
    ],

    columns: [
      { field: 'Id', title: 'ID', width: 70, hidden: true, align: 'center' },
      {
        field: 'ClauseNumber',
        title: '条款编号',
        width: 120,
        align: 'center',
        sortable: true,
      },
      {
        field: 'Title',
        title: '条款标题',
        width: 250,
        sortable: true,
        showOverflowTooltip: true,
      },
      {
        field: 'StandardCode',
        title: '所属标准',
        width: 150,
        bind: { key: 'standard_list', value: 'StandardCode' },
      },
      {
        field: 'SortOrder',
        title: '排序',
        width: 80,
        align: 'center',
        sortable: true,
      },
      {
        field: 'CreateDate',
        title: '创建时间',
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
