/**
 * 文档提取规则管理 - 页面配置
 * 用于 Vol 框架代码生成器
 */

let $vue = null;

export default {
  // 页面标题
  title: "文档提取规则管理",

  // 表格配置
  table: {
    // 是否显示行号
    index: true,
    // 是否显示选择框
    ck: false,
    // 表格字段
    columns: [
      { field: "fileName", title: "文档名称", width: 200, sort: true },
      { field: "standardName", title: "所属标准", width: 150, sort: true },
      { field: "stageName", title: "所属阶段", width: 120, sort: true },
      { field: "skill", title: "技能类型", width: 100, bind: { key: "doc_skill", data: [] } },
      { field: "fieldCount", title: "字段数", width: 80 },
      { field: "tableCount", title: "表格数", width: 80 },
      { field: "isValid", title: "验证状态", width: 100, bind: { key: "rule_status", data: [] } },
      { field: "createDate", title: "创建时间", width: 150 },
      { field: "updateDate", title: "更新时间", width: 150 }
    ],
    // 操作列按钮
    action: {
      width: 200,
      buttons: [
        {
          name: "编辑规则",
          icon: "el-icon-edit",
          type: "primary",
          onClick: (row) => {
            $vue.$router.push({
              path: "/CertPlatform/DocExtractionRule/edit",
              query: { fileCode: row.fileCode }
            });
          }
        },
        {
          name: "删除",
          icon: "el-icon-delete",
          type: "danger",
          onClick: (row) => {
            $vue.$confirm("确认删除该规则?", "提示", {
              confirmButtonText: "确定",
              cancelButtonText: "取消",
              type: "warning"
            }).then(() => {
              $vue.http.post(`api/DocExtractionRule/${row.fileCode}/delete`).then(() => {
                $vue.$message.success("删除成功");
                $vue.refresh();
              });
            });
          }
        }
      ]
    }
  },

  // 查询条件
  searchForm: {
    fields: [
      { field: "standardId", title: "所属标准", type: "select", data: [] },
      { field: "stageId", title: "所属阶段", type: "select", data: [] },
      { field: "isValid", title: "验证状态", type: "select", data: [] },
      { field: "fileName", title: "文档名称", type: "text" }
    ]
  },

  // 弹窗编辑表单
  editForm: {
    fields: []
  },

  // 页面方法
  methods: {
    // 页面初始化
    onInit() {
      $vue = this;

      // 设置查询表单数据
      this.searchForm.fields[0].data = this.getStandards();
      this.searchForm.fields[1].data = this.getStages();
      this.searchForm.fields[2].data = this.getRuleStatus();
    },

    // 获取标准列表
    getStandards() {
      return [];
    },

    // 获取阶段列表
    getStages() {
      return [];
    },

    // 获取规则状态
    getRuleStatus() {
      return [
        { key: "0", value: "未验证" },
        { key: "1", value: "验证通过" },
        { key: "2", value: "验证失败" }
      ];
    }
  }
};
