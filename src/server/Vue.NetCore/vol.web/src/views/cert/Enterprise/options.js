/**
 * 企业管理 - ViewGrid 配置
 * 表名：ent_enterprise
 */

export default function () {
  return {
    table: {
      name: 'Enterprise',
      cnName: '企业管理',
      url: '/Enterprise/',
      pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
      sortName: 'Id',
    },

    editFormFields: {
      Code: '',
      Name: '',
      ShortName: '',
      CreditCode: '',
      LegalPerson: '',
      Address: '',
      CertScope: '',
      ContactName: '',
      ContactPhone: '',
      ContactEmail: '',
      ArchiveDate: null,
    },

    editFormOptions: [
      [
        { title: 'Code', field: 'Code', type: 'hidden' },
        {
          title: '企业全称',
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
          title: '统一社会信用代码',
          field: 'CreditCode',
          maxlength: 50,
          colSize: 6,
        },
      ],
      [
        {
          title: '法人代表',
          field: 'LegalPerson',
          maxlength: 50,
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
        {
          title: '电子邮箱',
          field: 'ContactEmail',
          maxlength: 200,
          colSize: 6,
        },
      ],
      [
        {
          title: '企业地址',
          field: 'Address',
          type: 'textarea',
          rows: 2,
          colSize: 12,
        },
      ],
      [
        {
          title: '经营范围/认证范围',
          field: 'CertScope',
          type: 'textarea',
          rows: 3,
          colSize: 12,
        },
      ],
    ],

    searchFormFields: {
      Name: '',
      CreditCode: '',
    },

    searchFormOptions: [
      [
        {
          title: '关键词',
          field: 'Name',
          placeholder: '企业名称/简称/信用代码',
          colSize: 8,
        },
        {
          title: '信用代码',
          field: 'CreditCode',
          colSize: 4,
        },
      ],
    ],

    columns: [
      { field: 'Id', title: 'ID', width: 70, hidden: true, align: 'center' },
      {
        field: 'Name',
        title: '企业全称',
        width: 250,
        sortable: true,
        link: true,
      },
      {
        field: 'ShortName',
        title: '简称',
        width: 120,
        align: 'center',
      },
      {
        field: 'CreditCode',
        title: '信用代码',
        width: 180,
        align: 'center',
      },
      {
        field: 'LegalPerson',
        title: '法人',
        width: 100,
        align: 'center',
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
    ],

    detail: null,
    details: [],

    extend: {
      buttons: [],
      methods: {},
    },
  };
}
