/* ==========================================================================
   原型 42 · Mock 数据
   数据全部来自 25 号纸面实验实测口径 + 05 册设计，不臆造业务字段
   ========================================================================== */

/** 字典：取值来源（22 号 D1 七类，一期只用 4 类） */
const SRC_KIND = {
  global:   { label: '全局参数',       tag: 'info' },
  replace:  { label: '企业属性',       tag: 'info' },
  compute:  { label: '计算/统计',      tag: 'info' },
  profile:  { label: '企业资料',       tag: 'warning' },
  ai:       { label: 'AI 建议',        tag: 'warning' },
  manual:   { label: '人工录入',       tag: 'success' },
  pending:  { label: '待填写',         tag: 'gray' }
};

/** 25 号实测：确定性锚点占比 98.6%（编号 35.0% + 日期 37.1% + 人名 24.8% + 公司名 1.7%） */
const MOCK = {
  /* ---------- 企业 ---------- */
  enterprises: [
    { Code: 'E001', Name: 'G4测试企业甲', StageCount: 2, StdCount: 3 },
    { Code: 'E002', Name: '测试企业b',    StageCount: 1, StdCount: 1 }
  ],

  /* ---------- 左树（企业 → 资源 → 标准 → 阶段 → 目录 → 文档） ---------- */
  tree: {
    E001: [
      {
        Code: 'ISO9001', Name: 'ISO 9001:2015', Type: 'standard',
        children: [
          { Code: 'S_RE', Name: '复审', Type: 'stage', Std: 'ISO9001', children: [
            { Code: 'F0', Name: '0 基础资料', Type: 'folder', children: [
              { Code: 'D_LICENSE',  Name: '营业执照',       Type: 'file', DocCategory: 'fixed',    Std: 'ISO9001', Stage: 'S_RE', Folder: 'F0' },
              { Code: 'D_IDCARD',   Name: '法人身份证',     Type: 'file', DocCategory: 'fixed',    Std: 'ISO9001', Stage: 'S_RE', Folder: 'F0' },
              { Code: 'D_PRODLIC',  Name: '生产许可证',     Type: 'file', DocCategory: 'fixed',    Std: 'ISO9001', Stage: 'S_RE', Folder: 'F0' },
              { Code: 'D_SYSFILE',  Name: '体系文件清单',   Type: 'file', DocCategory: 'template', Std: 'ISO9001', Stage: 'S_RE', Folder: 'F0' }
            ]},
            { Code: 'F4', Name: '4 记录文件', Type: 'folder', children: [
              { Code: 'D_RISK',     Name: '风险管理报告',   Type: 'file', DocCategory: 'editable', Std: 'ISO9001', Stage: 'S_RE', Folder: 'F4' },
              { Code: 'D_AUDITPLAN',Name: '年度内审计划',   Type: 'file', DocCategory: 'editable', Std: 'ISO9001', Stage: 'S_RE', Folder: 'F4' },
              { Code: 'D_TRAIN',    Name: '培训记录表',     Type: 'file', DocCategory: 'editable', Std: 'ISO9001', Stage: 'S_RE', Folder: 'F4' },
              { Code: 'D_AUDITREC', Name: '内审检查表',     Type: 'file', DocCategory: 'editable', Std: 'ISO9001', Stage: 'S_RE', Folder: 'F4' },
              { Code: 'D_QUALOBJ',  Name: '质量目标',       Type: 'file', DocCategory: 'editable', Std: 'ISO9001', Stage: 'S_RE', Folder: 'F4' }
            ]},
            { Code: 'F5', Name: '5 管理职责', Type: 'folder', children: [
              { Code: 'D_REVIEW',   Name: '管理评审报告',   Type: 'file', DocCategory: 'editable', Std: 'ISO9001', Stage: 'S_RE', Folder: 'F5' },
              { Code: 'D_ORGCHART', Name: '组织架构图',     Type: 'file', DocCategory: 'platform_generated', Std: 'ISO9001', Stage: 'S_RE', Folder: 'F5' }
            ]}
          ]},
          { Code: 'S_INIT', Name: '初审', Type: 'stage', Std: 'ISO9001', children: [
            { Code: 'F0i', Name: '0 基础资料', Type: 'folder', children: [
              { Code: 'D_LICENSE_I', Name: '营业执照', Type: 'file', DocCategory: 'fixed', Std: 'ISO9001', Stage: 'S_INIT', Folder: 'F0i' }
            ]}
          ]}
        ]
      },
      {
        Code: 'ISO14001', Name: 'ISO 14001:2015', Type: 'standard',
        children: [
          { Code: 'S_R2', Name: '复审', Type: 'stage', Std: 'ISO14001', children: [
            { Code: 'F4b', Name: '4 记录文件', Type: 'folder', children: [
              { Code: 'D_ENVREC', Name: '环境因素识别表', Type: 'file', DocCategory: 'editable', Std: 'ISO14001', Stage: 'S_R2', Folder: 'F4b' }
            ]}
          ]}
        ]
      },
      { Code: 'ISO19001', Name: 'GB/T 19001-2016', Type: 'standard', children: [] }
    ],
    E002: [
      { Code: 'ISO9001', Name: 'ISO 9001:2015', Type: 'standard',
        children: [
          { Code: 'S_RB', Name: '复审', Type: 'stage', Std: 'ISO9001', children: [
            { Code: 'F4c', Name: '4 记录文件', Type: 'folder', children: [
              { Code: 'D_RISK_B', Name: '风险管理报告', Type: 'file', DocCategory: 'editable', Std: 'ISO9001', Stage: 'S_RB', Folder: 'F4c' }
            ]}
          ]}
        ]
      }
    ]
  },

  /* ---------- 固定文档：候选池（模拟企业上传的原始资料） ---------- */
  /* ⭐ 用户的核心场景：标准要求法人身份证，企业上传了 3~4 份身份证，
        后台无法精准判定哪一张是法人的 ⇒ 必须回传候选让人工选 */
  candidates: {
    D_LICENSE: [
      { FileCode: 'OF001', FileName: '营业执照-副本.pdf', FileType: 'PDF',   Size: '412 KB',
        Score: 0.99, MatchField: '统一社会信用代码', Evidence: '注册号 <em>91610xxxxxxMA7K3</em> · 法定代表人 <em>张明远</em>',
        Fields: [['企业名称','G4 测试石油天然气股份有限公司'],['信用代码','91610xxxxxxMA7K3'],['法定代表人','张明远'],['成立日期','2003-06-18']] },
      { FileCode: 'OF002', FileName: '营业执照正本扫描.jpg', FileType: 'JPG', Size: '1.2 MB',
        Score: 0.71, MatchField: '企业名称', Evidence: '名称含 <em>测试石油天然气</em>，无信用代码字段',
        Fields: [['企业名称','G4 测试石油天然气股份有限公司'],['类型','有限责任公司']] }
    ],
    D_IDCARD: [
      { FileCode: 'OF010', FileName: '张明远_身份证正面.jpg', FileType: 'JPG', Size: '268 KB',
        Score: 0.94, MatchField: '姓名 + 公民身份号码',
        Evidence: '姓名 <em>张明远</em> · 公民身份号码 <em>6101**********2317</em> · 与法定代表人姓名一致 ✓',
        Fields: [['姓名','张明远'],['性别','男'],['民族','汉'],['出生','1978-04-12'],['住址','陕西省西安市雁塔区…'],['公民身份号码','6101**********2317']],
        MatchedBy: '与营业执照「法定代表人 张明远」姓名一致' },
      { FileCode: 'OF011', FileName: '张明远_身份证反面.jpg', FileType: 'JPG', Size: '241 KB',
        Score: 0.62, MatchField: '姓名',
        Evidence: '国徽面 · 签发机关 <em>西安市公安局</em> · 有效期限；<b>无姓名字段</b>',
        Fields: [['签发机关','西安市公安局雁塔分局'],['有效期限','2018.03.05-2038.03.05']],
        MatchedBy: '同名前缀推断（反面通常与正面成对）' },
      { FileCode: 'OF012', FileName: '李建国_身份证正面.jpg', FileType: 'JPG', Size: '255 KB',
        Score: 0.38, MatchField: '（无匹配）',
        Evidence: '姓名 <em>李建国</em> · 与法定代表人 <em>张明远</em> 不一致',
        Fields: [['姓名','李建国'],['性别','男'],['出生','1975-09-30'],['公民身份号码','6101**********0815']],
        MatchedBy: '同为身份证类文件，仅按类别召回' },
      { FileCode: 'OF013', FileName: '王秀英_身份证正面.jpg', FileType: 'JPG', Size: '249 KB',
        Score: 0.34, MatchField: '（无匹配）',
        Evidence: '姓名 <em>王秀英</em> · 与法定代表人 <em>张明远</em> 不一致',
        Fields: [['姓名','王秀英'],['性别','女'],['出生','1982-11-08'],['公民身份号码','6101**********1620']],
        MatchedBy: '同为身份证类文件，仅按类别召回' }
    ],
    D_PRODLIC: [
      { FileCode: 'OF020', FileName: '石油天然气开采许可证.pdf', FileType: 'PDF', Size: '680 KB',
        Score: 0.58, MatchField: '许可证类别',
        Evidence: '证号 <em>61010xxxxxxxx</em> · 类别 <em>石油天然气开采</em>；<b>有效期至 2027-04 已过期</b>',
        Fields: [['证号','61010xxxxxxxx'],['类别','石油天然气开采'],['有效期至','2027-04-30']],
        MatchedBy: '类别匹配，但有效期告警' }
    ]
  },

  /* ---------- 固定文档：自动匹配结果 + 完成率 ---------- */
  fixedDocs: {
    D_LICENSE:  { Picked: 'OF001', Ambiguous: false, State: 'confirmed', HasTemplate: true },
    D_IDCARD:   { Picked: null,   Ambiguous: true,  State: 'need_confirm',
                  Reason: '企业上传 4 份身份证，AI 无法判定哪一张是法定代表人的', CandidateCount: 4 },
    D_PRODLIC:  { Picked: 'OF020', Ambiguous: false, State: 'warn',
                  Reason: '许可证有效期至 2027-04-30，评审时需确认是否在有效期内', HasTemplate: false },
    D_SYSFILE:  { Picked: null,   Ambiguous: false, State: 'platform_generated',
                  Reason: '按目录自动派生，无需企业上传' },
    D_LICENSE_I:{ Picked: 'OF001', Ambiguous: false, State: 'confirmed', HasTemplate: true }
  },

  /* ---------- 可编辑文档：锚点明细（★ 逐单元格干预的对象） ---------- */
  /* Completion 口径见 41-01 §二：已写入 / 有效锚点总数；Total=0 ⇒ 0 */
  editDocs: {
    D_RISK: {
      Status: 'generated', Completion: 0.88, TemplateName: '风险管理报告.docx', AnchorTotal: 19, FilledCount: 17,
      Cells: [
        { Ref: '{{ENT_NAME}}',        Loc: '正文·第3段',   Field: 'ENT_NAME',     Val: 'G4测试石油天然气股份有限公司', Kind: 'global',  Conf: 1.00 },
        { Ref: '{{CREDIT_CODE}}',     Loc: '正文·第3段',   Field: 'CREDIT_CODE',  Val: '91610xxxxxxMA7K3',            Kind: 'global',  Conf: 1.00 },
        { Ref: '{{LEGAL_PERSON}}',    Loc: '正文·第4段',   Field: 'LEGAL_PERSON', Val: '张明远',                        Kind: 'global',  Conf: 1.00 },
        { Ref: '{{DOC_NO}}',          Loc: '页眉[默认]',   Field: 'DOC_NO',       Val: 'YZH-FX-2026-001',               Kind: 'compute', Conf: 1.00 },
        { Ref: '{{ORG_NAME}}',        Loc: '页眉[默认]',   Field: 'ORG_NAME',     Val: 'G4 认证中心',                    Kind: 'replace', Conf: 1.00 },
        { Ref: '{{ISSUE_DATE}}',      Loc: '正文·落款',    Field: 'ISSUE_DATE',   Val: '2026-03-20',                    Kind: 'compute', Conf: 1.00 },
        { Ref: '{{table:风险清单}}',  Loc: '表1 行1-8',   Field: 'RISK_ROWS',    Val: '8 行 × 4 列',                   Kind: 'profile', Conf: 0.95 },
        { Ref: '表2 行3 列2',        Loc: '表2 行3 列2',  Field: 'RISK_LEVEL',   Val: '高',                            Kind: 'profile', Conf: 0.78 },
        { Ref: '表2 行4 列2',        Loc: '表2 行4 列2',  Field: 'RISK_LEVEL',   Val: '中',                            Kind: 'profile', Conf: 0.74 },
        { Ref: '{{RISK_POLICY}}',    Loc: '正文·第9段',   Field: 'RISK_POLICY',  Val: '（略：企业上年度风险管理制度第 3 章）', Kind: 'profile', Conf: 0.71 }
      ]
    },
    D_AUDITPLAN: {
      Status: 'generated', Completion: 1.00, TemplateName: '年度内审计划.docx', AnchorTotal: 12, FilledCount: 12,
      Cells: [
        { Ref: '{{ENT_NAME}}',      Loc: '封面',        Field: 'ENT_NAME',    Val: 'G4测试石油天然气股份有限公司', Kind: 'global',  Conf: 1.00 },
        { Ref: '{{PLAN_YEAR}}',     Loc: '正文·表头',   Field: 'PLAN_YEAR',   Val: '2026',                          Kind: 'compute', Conf: 1.00 },
        { Ref: '{{AUDIT_TYPE}}',    Loc: '正文·表头',   Field: 'AUDIT_TYPE',  Val: '内部审核（第一阶段）',            Kind: 'global',  Conf: 1.00 },
        { Ref: '{{AUDIT_LEADER}}',  Loc: '表1 行2 列4', Field: 'AUDIT_LEADER',Val: '李建国',                        Kind: 'profile', Conf: 0.88 },
        { Ref: '{{table:审核安排}}',Loc: '表1 行2-9',   Field: 'AUDIT_ROWS',  Val: '8 行 × 7 列',                   Kind: 'profile', Conf: 0.96 },
        { Ref: '{{CRITERIA}}',      Loc: '正文·第4段',  Field: 'CRITERIA',    Val: 'GB/T 19001-2016 第 9.2 条',       Kind: 'global',  Conf: 1.00 }
      ]
    },
    D_TRAIN: {
      Status: 'partial', Completion: 0.41, TemplateName: '培训记录表.xlsx', AnchorTotal: 17, FilledCount: 7,
      Cells: [
        { Ref: 'Sheet1!B2',       Loc: 'Sheet1!B2',  Field: 'TRAIN_DATE',  Val: '2026-01-15',              Kind: 'global',  Conf: 1.00 },
        { Ref: 'Sheet1!B3',       Loc: 'Sheet1!B3',  Field: 'TRAIN_TOPIC', Val: 'ISO 9001:2015 换版要点',   Kind: 'profile', Conf: 0.82 },
        { Ref: 'Sheet1!C3',       Loc: 'Sheet1!C3',  Field: 'TRAIN_HOURS', Val: '4',                       Kind: 'profile', Conf: 0.79 },
        { Ref: 'Sheet1!A4:F4',    Loc: '数据区 行2', Field: 'TRAIN_ROW2',  Val: '6 列',                     Kind: 'profile', Conf: 0.55 },
        { Ref: 'Sheet1!A5:F5',    Loc: '数据区 行3', Field: 'TRAIN_ROW3',  Val: '6 列',                     Kind: 'profile', Conf: 0.55 },
        { Ref: '{{table:培训记录}}', Loc: '数据区 行4-9', Field: 'TRAIN_ROW4',  Val: '待填',                    Kind: 'pending', Conf: 0.00 }
      ]
    },
    D_AUDITREC: { Status: 'none',  Completion: 0.00, TemplateName: '内审检查表.docx', AnchorTotal: 34, FilledCount: 0, Cells: [] },
    D_QUALOBJ:  { Status: 'none',  Completion: 0.00, TemplateName: '质量目标.docx',   AnchorTotal: 0,  FilledCount: 0, Cells: [] },
    D_REVIEW:   { Status: 'none',  Completion: 0.00, TemplateName: '管理评审报告.docx', AnchorTotal: 16, FilledCount: 0, Cells: [] },
    D_ORGCHART: { Status: 'platform_generated', Completion: 1.00, TemplateName: '（平台生成）', AnchorTotal: 0, FilledCount: 0, Cells: [] },
    D_ENVREC:   { Status: 'none',  Completion: 0.00, TemplateName: '环境因素识别表.xlsx', AnchorTotal: 22, FilledCount: 0, Cells: [] },
    D_RISK_B:   { Status: 'none',  Completion: 0.00, TemplateName: '风险管理报告.docx', AnchorTotal: 19, FilledCount: 0, Cells: [] }
  },

  /* ---------- 企业原始资料池（候选来源，用于「从待选内容中重新筛选」） ---------- */
  /* ⭐ 只读，输入侧；⛔ 不在本页编辑 */
  rawFiles: [
    { Code: 'OF010', Name: '张明远_身份证正面.jpg',  Cat: '身份证明', Stage: '复审', Updated: '2026-10-03' },
    { Code: 'OF011', Name: '张明远_身份证反面.jpg',  Cat: '身份证明', Stage: '复审', Updated: '2026-10-03' },
    { Code: 'OF012', Name: '李建国_身份证正面.jpg',  Cat: '身份证明', Stage: '复审', Updated: '2026-10-03' },
    { Code: 'OF013', Name: '王秀英_身份证正面.jpg',  Cat: '身份证明', Stage: '复审', Updated: '2026-10-03' },
    { Code: 'OF001', Name: '营业执照-副本.pdf',       Cat: '资质证照', Stage: '复审', Updated: '2026-10-03' },
    { Code: 'OF020', Name: '石油天然气开采许可证.pdf',Cat: '资质证照', Stage: '复审', Updated: '2026-10-03' },
    { Code: 'OF030', Name: '2026年度风险清单.xlsx',  Cat: '记录表格', Stage: '复审', Updated: '2026-10-02' },
    { Code: 'OF031', Name: '2026年度内审计划.docx',  Cat: '体系文件', Stage: '复审', Updated: '2026-10-02' },
    { Code: 'OF032', Name: '质量管理体系风险管理制度.docx', Cat: '制度文件', Stage: '复审', Updated: '2026-09-28' },
    { Code: 'OF033', Name: '2026年1月培训签到表.xlsx',Cat: '记录表格', Stage: '复审', Updated: '2026-09-20' },
    { Code: 'OF034', Name: '组织架构图.png',         Cat: '技术文件', Stage: '复审', Updated: '2026-09-15' }
  ],

  /* ---------- 修改记录（审计，A-2/A-7） ---------- */
  actions: [
    { Time: '2026-10-04 09:12:30', User: '王专家', Type: '锁定', Target: '营业执照', Reason: '已核对原件，与营业执照一致', Scope: '文件' },
    { Time: '2026-10-04 09:15:02', User: '王专家', Type: '批量规范化', Target: '4 记录文件 / 5 个文档', Reason: '首次一键规范化', Scope: '目录' },
    { Time: '2026-10-04 09:16:44', User: '王专家', Type: '改值', Target: '风险管理报告 · 表2行4列2 · RISK_LEVEL', Reason: 'AI 取的是中，核对原件应为高', Scope: '单元格' },
    { Time: '2026-10-04 09:18:10', User: '王专家', Type: '钉住', Target: '风险管理报告 · {{RISK_POLICY}}', Reason: '人工润色过，重写时保留', Scope: '单元格' },
    { Time: '2026-10-04 09:20:55', User: '王专家', Type: '更新文档', Target: '风险管理报告', Reason: '完成率 88% → 100%', Scope: '文件' }
  ],

  /* ---------- 队列任务（与 yzh_queue_task 对齐） ---------- */
  queue: {
    Code: 'Q-20261004-0007',
    Tasks: [
      { Doc: '营业执照',        Type: '固定', State: 'success', Ms: 820,  Msg: '已按 OF001 就位' },
      { Doc: '法人身份证',      Type: '固定', State: 'success', Ms: 1240, Msg: '4 个候选，待人工确认' },
      { Doc: '生产许可证',      Type: '固定', State: 'success', Ms: 660,  Msg: '已就位，有效期告警' },
      { Doc: '体系文件清单',    Type: '固定', State: 'success', Ms: 410,  Msg: '平台自动生成' },
      { Doc: '风险管理报告',    Type: '可编辑', State: 'success', Ms: 3480, Msg: '17/19 已填，完成度 88%' },
      { Doc: '年度内审计划',    Type: '可编辑', State: 'success', Ms: 2910, Msg: '12/12 已填，完成度 100%' },
      { Doc: '培训记录表',      Type: '可编辑', State: 'success', Ms: 4120, Msg: '7/17 已填，10 项待填' },
      { Doc: '内审检查表',      Type: '可编辑', State: 'failed',  Ms: 520,  Msg: '模板未上传（后台「标准文档填写规则」尚未配置此模板）' },
      { Doc: '质量目标',        Type: '可编辑', State: 'success', Ms: 380,  Msg: '模板无锚点，纯复制，无需填写' },
      { Doc: '管理评审报告',    Type: '可编辑', State: 'skipped', Ms: 0,    Msg: '已锁定 🔒，跳过覆盖' }
    ]
  }
};