/**
 * ISO 标准管理 - ViewGrid 配置
 * 表名：cert_iso_standard
 * 
 * 布局规范：
 * - 统一 2 列布局（colSize: 6）
 * - 隐藏字段用 type: 'hidden'
 * - 整行字段用 colSize: 12
 */

export default function () {
  const table = {
    name: "ISOStandard",
    cnName: "ISO 标准管理",
    url: "/ISOStandard/",
    sortName: "id",
    key: "Id",
    footer: "Foots",
    pagination: { pageSize: 20, pageSizes: [10, 20, 50, 100] },
  };

  const tableName = table.name;
  const tableCNName = table.cnName;
  const newTabEdit = false;
  const key = table.key;

  // 表单字段
  const editFormFields = {
    Code: "",           // 隐藏：业务编码
    CbCode: "",        // 所属机构编码
    StandardCode: "",  // 标准编号
    StandardName: "",  // 标准名称
    VersionYear: 2016, // 版本年份
    Status: "pending",  // 状态
    Remark: "",          // 备注
  };

  // 表单配置 - 统一 2 列布局
  const editFormOptions = [
    // 第一行：隐藏字段 + 所属机构（整行）
    [
      { field: "Code", type: "hidden" },
      {
        title: "所属机构",
        field: "CbCode",
        required: true,
        type: "select",
        dataKey: "cb_list",
        data: [],
        colSize: 12,
      },
    ],
    // 第二行：标准编号 + 版本年份
    [
      {
        title: "标准编号",
        field: "StandardCode",
        required: true,
        maxlength: 50,
        placeholder: "如：ISO 13485:2016",
        colSize: 8,
      },
      {
        title: "版本年份",
        field: "VersionYear",
        type: "number",
        min: 1990,
        max: 2100,
        colSize: 4,
      },
    ],
    // 第三行：标准名称（整行）
    [
      {
        title: "标准名称",
        field: "StandardName",
        required: true,
        maxlength: 200,
        colSize: 12,
      },
    ],
    // 第四行：状态 + 备注
    [
      {
        title: "状态",
        field: "Status",
        dataKey: "standard_status",
        data: [],
        type: "select",
        colSize: 6,
      },
      {
        title: "备注",
        field: "Notes",
        type: "textarea",
        rows: 3,
        colSize: 12,
      },
    ],
  ];

  // 搜索字段
  const searchFormFields = {
    StandardName: "",
    Status: "",
  };

  // 搜索配置
  const searchFormOptions = [
    [
      {
        title: "关键词",
        field: "StandardName",
        placeholder: "标准编号/名称",
        colSize: 8,
      },
      {
        title: "状态",
        field: "Status",
        dataKey: "standard_status",
        data: [],
        type: "select",
        colSize: 4,
      },
    ],
  ];

  // 列配置（操作列由 YZHBaseCrud 自动添加）
  const columns = [
    {
      field: "Id",
      title: "ID",
      width: 70,
      hidden: true,
      align: "center",
    },
    {
      field: "StandardCode",
      title: "标准编号",
      width: 150,
      align: "center",
      sortable: true,
    },
    {
      field: "StandardName",
      title: "标准名称",
      width: 300,
      sortable: true,
    },
    {
      field: "VersionYear",
      title: "版本",
      width: 80,
      align: "center",
    },
    {
      field: "Status",
      title: "状态",
      width: 100,
      align: "center",
      bind: { key: "standard_status", value: "Status" },
    },
    {
      field: "CreateDate",
      title: "创建时间",
      width: 160,
      align: "center",
      sortable: true,
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
