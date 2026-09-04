/**
 * 认证阶段定义（全局基础资料）
 *
 * 基于 ISO/IEC 17021-1:2015 规定的认证流程阶段
 * 9 个标准阶段：申请受理 → 合同评审 → 审核方案策划 →
 *   第一阶段审核 → 第二阶段审核 → 认证决定 → 颁发证书 → 监督审核 → 再认证
 */
export default function () {
  const table = {
    name: "CertStage",
    cnName: "认证阶段定义",
    url: "/CertStage/",
    sortName: "SortOrder",
    key: "Id",
    footer: "Foots",
    pagination: { pageSize: 50, pageSizes: [20, 50, 100] },
  };

  const editFormFields = {
    Code: "",
    StageCode: "",
    StageName: "",
    SortOrder: 0,
    Category: "process",
    Status: "active",
    Remark: "",
  };

  const searchFormFields = {
    keyword: "",
    Category: "",
    Status: "",
  };

  return {
    table,
    key: table.key,
    tableName: table.name,
    tableCNName: table.cnName,
    newTabEdit: false,
    editFormFields,
    searchFormFields,

    // ===== 列定义（V3.0：后端视图已包含字典翻译，直接显示中文字段） =====
    columns: [
      { field: 'Id', title: 'ID', width: 70, align: 'center', hidden: true },
      { field: 'StageCode', title: '阶段编码', width: 120, sortable: true },
      { field: 'StageName', title: '阶段名称', width: 220, sortable: true, showOverflow: true },
      { field: 'SortOrder', title: '排序', width: 80, align: 'center', sortable: true },
      { field: 'CategoryName', title: '分类', width: 120, align: 'center' },        // ✅ 视图字段，中文
      { field: 'StatusName', title: '状态', width: 100, align: 'center' },          // ✅ 视图字段，中文
      { field: 'Remark', title: '备注', width: 200, showOverflow: true },
    ],
    editFormOptions: [
      [
        { field: 'StageCode', title: '阶段编码', type: 'input', required: true, placeholder: '如 STAGE-01' },
        { field: 'StageName', title: '阶段名称', type: 'input', required: true, colSize: 2 },
        { field: 'SortOrder', title: '排序号', type: 'number', required: false },
        { field: 'Category', title: '分类', type: 'select', dataKey: 'stage_category' },
        { field: 'Status', title: '状态', type: 'select', dataKey: 'stage_status' },
        { field: 'Remark', title: '备注', type: 'textarea', rows: 3 },
      ],
    ],
    searchFormOptions: [
      [
        { field: 'keyword', title: '关键词', type: 'input', placeholder: '搜索阶段名称/编码' },
        { field: 'Category', title: '分类', type: 'select', dataKey: 'stage_category' },
        { field: 'Status', title: '状态', type: 'select', dataKey: 'stage_status' },
      ],
    ],
    detail: { columns: [] },
    details: [],
  };
}
