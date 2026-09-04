/**
 * 审核任务管理 - ViewGrid 配置
 * 表名：cert_audit_task
 * 
 * 布局规范：
 * - 统一 2 列布局（colSize: 6）
 * - 隐藏字段用 type: 'hidden'
 * - 整行字段用 colSize: 12
 */

export default function () {
  const table = {
    name: "AuditTask",
    cnName: "审核任务管理",
    url: "/AuditTask/",
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
    Code: "",             // 隐藏：业务编码
    TaskNumber: "",      // 任务编号
    ApplicationCode: "", // 所属申请
    PhaseCode: "",       // 审核阶段
    AuditorId: "",     // 审核员
    Status: "pending",    // 状态
    PlannedDate: "",  // 计划日期
    Remark: "",            // 备注
  };

  // 表单配置 - 统一 2 列布局
  const editFormOptions = [
    // 第一行：隐藏字段 + 任务编号 + 所属申请
    [
      { field: "Code", type: "hidden" },
      {
        title: "任务编号",
        field: "TaskNumber",
        maxlength: 50,
        colSize: 6,
      },
      {
        title: "所属申请",
        field: "ApplicationCode",
        required: true,
        type: "select",
        dataKey: "application_list",
        data: [],
        colSize: 6,
      },
    ],
    // 第二行：审核阶段 + 审核员
    [
      {
        title: "审核阶段",
        field: "PhaseCode",
        required: true,
        type: "select",
        dataKey: "PhaseCode",
        data: [],
        colSize: 6,
      },
      {
        title: "审核员",
        field: "AuditorId",
        type: "select",
        dataKey: "auditor_list",
        data: [],
        colSize: 6,
      },
    ],
    // 第三行：状态 + 计划日期
    [
      {
        title: "状态",
        field: "Status",
        dataKey: "task_status",
        data: [],
        type: "select",
        colSize: 6,
      },
      {
        title: "计划日期",
        field: "PlannedDate",
        type: "date",
        colSize: 6,
      },
    ],
    // 第四行：备注（整行）
    [
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
    TaskNumber: "",
    Status: "",
  };

  // 搜索配置
  const searchFormOptions = [
    [
      {
        title: "关键词",
        field: "TaskNumber",
        placeholder: "任务编号/申请编号",
        colSize: 8,
      },
      {
        title: "状态",
        field: "Status",
        dataKey: "task_status",
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
      field: "TaskNumber",
      title: "任务编号",
      width: 150,
      align: "center",
      sortable: true,
    },
    {
      field: "ApplicationCode",
      title: "所属申请",
      width: 150,
      bind: { key: "application_list", value: "ApplicationCode" },
    },
    {
      field: "PhaseCode",
      title: "审核阶段",
      width: 120,
      align: "center",
      bind: { key: "PhaseCode", value: "PhaseCode" },
    },
    {
      field: "AuditorId",
      title: "审核员",
      width: 120,
      bind: { key: "auditor_list", value: "AuditorId" },
    },
    {
      field: "Status",
      title: "状态",
      width: 100,
      align: "center",
      bind: { key: "task_status", value: "Status" },
    },
    {
      field: "PlannedDate",
      title: "计划日期",
      width: 120,
      align: "center",
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
