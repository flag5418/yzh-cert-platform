/* ==========================================================================
   原型 42 · v2 · Mock 数据
   ★ 左树层级 = 企业 → 阶段 → 标准 → 文件夹 → 文件
     阶段在上，因为企业的「阶段定义」已经绑定了标准（三元组 企业×阶段×标准）
     标准下的文件夹 = 后台「标准文档目录结构」
   ========================================================================== */

/** 字典：取值来源（22 号 D1 七类，一期只用 5 类） */
const SRC_KIND = {
  global:  { label: '全局参数',  tag: 'info' },
  replace: { label: '企业属性',  tag: 'info' },
  compute: { label: '计算/统计', tag: 'info' },
  profile: { label: '企业资料',  tag: 'warning' },
  manual:  { label: '人工录入',  tag: 'success' },
  pending: { label: '待填写',    tag: 'gray' }
};

/* ==========================================================================
   ★ v4 · 三态派生规则（不是存储字段，是从 IsLocked + 锚点数 + 依赖失效 实时算出来）
   --------------------------------------------------------------------------
   锁定：IsLocked = 1                                    ⇒ 只有人工能进此态
   为空：无锚点规则 / 无匹配信息 / 锚点总数 = 0            ⇒ 系统判定
   待审：其余全部（含「填完 100%」）                     ⇒ 没锁定就是没认可
   ========================================================================== */
const DOC_STATE = {
  LOCKED: 'locked',   // 锁定
  EMPTY:  'empty',    // 为空
  REVIEW: 'review'    // 待审
};

const MOCK = {
  /* ================= 企业 ================= */
  enterprises: [
    { Code: 'E001', Name: 'G4测试企业甲', StageCnt: 2, StdCnt: 2 },
    { Code: 'E002', Name: '测试企业b',    StageCnt: 1, StdCnt: 1 }
  ],

  /* ================= 左树（企业 → 阶段 → 标准 → 文件夹 → 文件） ================= */
  tree: {
    E001: {
      Name: 'G4测试企业甲',
      Stages: [
        /* ---------- 初审 ---------- */
        { Code: 'S_INIT', Name: '初审', Standards: [
          { Code: 'ISO9001', Name: 'ISO 9001:2015', No: 'GB/T 19001-2016', Folders: [
            { Code: 'F0i', Name: '0 基础资料', Files: [
              { Code: 'D_LICENSE_I', Name: '营业执照',     Cat: 'fixed' },
              { Code: 'D_ORGCHART', Name: '组织架构图',   Cat: 'platform_generated' }
            ]}
          ]}
        ]},
        /* ---------- 复审 ---------- */
        { Code: 'S_RE', Name: '复审', Standards: [
          { Code: 'ISO9001', Name: 'ISO 9001:2015', No: 'GB/T 19001-2016', Folders: [
            { Code: 'F0', Name: '0 基础资料', Files: [
              { Code: 'D_LICENSE',  Name: '营业执照',         Cat: 'fixed' },
              { Code: 'D_IDCARD',   Name: '法人身份证',       Cat: 'fixed' },
              { Code: 'D_PRODLIC',  Name: '生产许可证',       Cat: 'fixed' },
              { Code: 'D_SYSFILE',  Name: '体系文件清单',     Cat: 'fixed' }
            ]},
            { Code: 'F1', Name: '1 文件控制', Files: [
              { Code: 'D_CTRLLIST', Name: '受控文件清单',     Cat: 'editable' },
              { Code: 'D_REVLIST',  Name: '文件修订记录',     Cat: 'editable' }
            ]},
            { Code: 'F4', Name: '4 记录文件', Files: [
              { Code: 'D_RISK',      Name: '风险管理报告',     Cat: 'editable' },
              { Code: 'D_AUDITPLAN', Name: '年度内审计划',     Cat: 'editable' },
              { Code: 'D_AUDITREC',  Name: '内审检查表',       Cat: 'editable' },
              { Code: 'D_TRAIN',     Name: '培训记录表',       Cat: 'editable' },
              { Code: 'D_QUALOBJ',   Name: '质量目标',         Cat: 'editable' }
            ]},
            { Code: 'F5', Name: '5 管理职责', Files: [
              { Code: 'D_REVIEW',    Name: '管理评审报告',     Cat: 'editable' },
              { Code: 'D_RESP',      Name: '质量方针',         Cat: 'editable' }
            ]}
          ]},
          { Code: 'ISO14001', Name: 'ISO 14001:2015', No: 'GB/T 24001-2016', Folders: [
            { Code: 'F4b', Name: '4 记录文件', Files: [
              { Code: 'D_ENVREC',    Name: '环境因素识别表',   Cat: 'editable' },
              { Code: 'D_ENVPLAN',   Name: '环境管理方案',     Cat: 'editable' }
            ]}
          ]}
        ]}
      ]
    },
    E002: {
      Name: '测试企业b',
      Stages: [
        { Code: 'S_RB', Name: '复审', Standards: [
          { Code: 'ISO9001', Name: 'ISO 9001:2015', No: 'GB/T 19001-2016', Folders: [
            { Code: 'F4c', Name: '4 记录文件', Files: [
              { Code: 'D_RISK_B', Name: '风险管理报告', Cat: 'editable' }
            ]}
          ]}
        ]}
      ]
    }
  },

  /* ================= 固定文档：候选池 ================= */
  /* ⭐ 核心场景：标准目录要求「法人身份证」，企业上传了多份身份证，
        后台无法精准判定哪张是法人的 ⇒ 必须回传候选让人工指定 */
  candidates: {
    D_LICENSE: [
      { FileCode: 'OF001', FileName: '营业执照-副本.pdf', FileType: 'PDF', Size: '412 KB', Pages: 2,
        Score: 0.99, MatchField: '统一社会信用代码',
        Evidence: '注册号 <em>91610xxxxxxMA7K3</em> · 法定代表人 <em>张明远</em> · 经营范围含「石油天然气」',
        Fields: [['企业名称','G4 测试石油天然气股份有限公司'],['统一社会信用代码','91610xxxxxxMA7K3'],
                 ['法定代表人','张明远'],['注册资本','伍亿元整'],['成立日期','2003-06-18'],['住所','陕西省西安市雁塔区科技路 xx 号']] },
      { FileCode: 'OF002', FileName: '营业执照正本扫描.jpg', FileType: 'JPG', Size: '1.2 MB', Pages: 1,
        Score: 0.71, MatchField: '企业名称（仅名称一致）',
        Evidence: '名称含 <em>测试石油天然气</em>，<b>图像质量低、无信用代码可校验</b>',
        Fields: [['企业名称','G4 测试石油天然气股份有限公司'],['类型','有限责任公司']] }
    ],
    D_IDCARD: [
      { FileCode: 'OF010', FileName: '张明远_身份证正面.jpg', FileType: 'JPG', Size: '268 KB', Pages: 1,
        Score: 0.94, MatchField: '姓名 + 公民身份号码',
        Evidence: '姓名 <em>张明远</em> · 公民身份号码 <em>6101**********2317</em> · 与营业执照「法定代表人 张明远」<b>姓名一致 ✓</b>',
        Fields: [['姓名','张明远'],['性别','男'],['民族','汉'],['出生','1978-04-12'],
                 ['住址','陕西省西安市雁塔区科技路 xx 号'],['公民身份号码','6101**********2317']],
        MatchedBy: '与营业执照「法定代表人」姓名一致 + 身份证号校验位通过' },
      { FileCode: 'OF011', FileName: '张明远_身份证反面.jpg', FileType: 'JPG', Size: '241 KB', Pages: 1,
        Score: 0.62, MatchField: '仅文件名同名前缀',
        Evidence: '国徽面 · 签发机关 <em>西安市公安局</em> · 有效期限 2018-03-05 ~ 2038-03-05；<b>本面无姓名，无法独立确认</b>',
        Fields: [['签发机关','西安市公安局雁塔分局'],['有效期限','2018.03.05-2038.03.05']],
        MatchedBy: '同名前缀推断（推测为 OF010 的反面）' },
      { FileCode: 'OF012', FileName: '李建国_身份证正面.jpg', FileType: 'JPG', Size: '255 KB', Pages: 1,
        Score: 0.38, MatchField: '（无匹配）',
        Evidence: '姓名 <em>李建国</em> · 与法定代表人 <em>张明远</em> <b>不一致</b> · 可能为内审员/质量负责人',
        Fields: [['姓名','李建国'],['性别','男'],['出生','1975-09-30'],['公民身份号码','6101**********0815']],
        MatchedBy: '仅按「身份证明」类别召回' },
      { FileCode: 'OF013', Name: '', FileName: '王秀英_身份证正面.jpg', FileType: 'JPG', Size: '249 KB', Pages: 1,
        Score: 0.34, MatchField: '（无匹配）',
        Evidence: '姓名 <em>王秀英</em> · 与法定代表人 <em>张明远</em> <b>不一致</b>',
        Fields: [['姓名','王秀英'],['性别','女'],['出生','1982-11-08'],['公民身份号码','6101**********1620']],
        MatchedBy: '仅按「身份证明」类别召回' }
    ],
    D_PRODLIC: [
      { FileCode: 'OF020', FileName: '石油天然气开采许可证.pdf', FileType: 'PDF', Size: '680 KB', Pages: 4,
        Score: 0.58, MatchField: '许可证类别',
        Evidence: '证号 <em>61010xxxxxxxx</em> · 类别 <em>石油天然气开采</em> · <b>有效期至 2027-04-30，评审时需确认是否仍在有效期</b>',
        Fields: [['证号','61010xxxxxxxx'],['类别','石油天然气开采'],['发证机关','陕西省能源局'],['有效期至','2027-04-30']],
        MatchedBy: '类别匹配，但有效期告警' }
    ],
    D_SYSFILE: [],
    D_LICENSE_I: [
      { FileCode:'OF001', FileName:'营业执照-副本.pdf', FileType:'PDF', Size:'412 KB', Pages:2,
        Score:0.99, MatchField:'统一社会信用代码',
        Evidence:'注册号 <em>91610xxxxxxMA7K3</em> · 法定代表人 <em>张明远</em> · 经营范围含「石油天然气」',
        Fields:[['企业名称','G4 测试石油天然气股份有限公司'],['统一社会信用代码','91610xxxxxxMA7K3'],
                ['法定代表人','张明远'],['注册资本','伍亿元整'],['成立日期','2003-06-18'],['住所','陕西省西安市雁塔区科技路 xx 号']],
        MatchedBy:'与复审阶段同一原件（同一企业证件跨阶段复用）' }
    ]
  },

  /* ================= 固定文档：匹配状态 ================= */
  fixedDocs: {
    D_LICENSE:    { Picked: 'OF001', State: 'confirmed', Reason: '' },
    D_IDCARD:     { Picked: null, State: 'need_confirm', CandidateCount: 4,
                    Reason: '企业上传 4 份身份证，无法自动判定哪一张是法定代表人的' },
    D_PRODLIC:    { Picked: 'OF020', State: 'warn', Reason: '许可证有效期至 2027-04-30，评审时需确认' },
    D_SYSFILE:    { Picked: null, State: 'platform_generated', Reason: '按标准目录自动派生，无需企业上传' },
    D_LICENSE_I:  { Picked: 'OF001', State: 'confirmed', Reason: '' }
  },

  /* ================= 可编辑文档：锚点明细 ================= */
  editDocs: {
    D_RISK: { Status: 'generated', Completion: 0.88, AnchorTotal: 19, FilledCount: 17, PdfPages: 6,
      Template: '风险管理报告.docx',
      Cells: [
        { Ref:'{{ENT_NAME}}',       Loc:'正文·第3段',  Field:'ENT_NAME',     Val:'G4测试石油天然气股份有限公司', Kind:'global',  Conf:1.00 },
        { Ref:'{{CREDIT_CODE}}',    Loc:'正文·第3段',  Field:'CREDIT_CODE',  Val:'91610xxxxxxMA7K3',            Kind:'global',  Conf:1.00 },
        { Ref:'{{LEGAL_PERSON}}',   Loc:'正文·第4段',  Field:'LEGAL_PERSON', Val:'张明远',                        Kind:'global',  Conf:1.00 },
        { Ref:'{{DOC_NO}}',         Loc:'页眉[默认]',  Field:'DOC_NO',       Val:'YZH-FX-2026-001',               Kind:'compute', Conf:1.00 },
        { Ref:'{{ORG_NAME}}',       Loc:'页眉[默认]',  Field:'ORG_NAME',     Val:'G4 认证中心',                    Kind:'replace', Conf:1.00 },
        { Ref:'{{ISSUE_DATE}}',     Loc:'正文·落款',   Field:'ISSUE_DATE',   Val:'2026-03-20',                    Kind:'compute', Conf:1.00 },
        { Ref:'{{RISK_SCOPE}}',     Loc:'正文·第2段',  Field:'RISK_SCOPE',   Val:'本公司及所属 12 个生产单位的质量管理体系活动', Kind:'global', Conf:1.00 },
        { Ref:'{{table:风险清单}}', Loc:'表1 行1-8',  Field:'RISK_ROWS',    Val:'8 行 × 4 列',                   Kind:'profile', Conf:0.95 },
        { Ref:'表2 行3 列2',        Loc:'表2 行3 列2', Field:'RISK_LEVEL',   Val:'高',                            Kind:'profile', Conf:0.78 },
        { Ref:'表2 行4 列2',        Loc:'表2 行4 列2', Field:'RISK_LEVEL',   Val:'中',                            Kind:'profile', Conf:0.62 },
        { Ref:'表2 行5 列2',        Loc:'表2 行5 列2', Field:'RISK_LEVEL',   Val:'中',                            Kind:'profile', Conf:0.61 },
        { Ref:'{{RISK_POLICY}}',    Loc:'正文·第9段',  Field:'RISK_POLICY',  Val:'（略：质量管理体系风险管理制度 第3章 风险评价准则）', Kind:'profile', Conf:0.71 }
      ]},
    D_AUDITPLAN: { Status:'generated', Completion:1.00, AnchorTotal:12, FilledCount:12, PdfPages:4,
      Template:'年度内审计划.docx',
      Cells:[
        { Ref:'{{ENT_NAME}}',       Loc:'封面',        Field:'ENT_NAME',    Val:'G4测试石油天然气股份有限公司', Kind:'global',  Conf:1.00 },
        { Ref:'{{PLAN_YEAR}}',      Loc:'正文·表头',   Field:'PLAN_YEAR',   Val:'2026',                          Kind:'compute', Conf:1.00 },
        { Ref:'{{AUDIT_TYPE}}',     Loc:'正文·表头',   Field:'AUDIT_TYPE',  Val:'内部审核（第一阶段）',            Kind:'global',  Conf:1.00 },
        { Ref:'{{AUDIT_CLAIM}}',    Loc:'正文·第2段',  Field:'AUDIT_CLAIM',  Val:'GB/T 19001-2016 8.2.2 / IAF MD 5.2', Kind:'global', Conf:1.00 },
        { Ref:'{{table:审核安排}}', Loc:'表1 行2-9',   Field:'AUDIT_ROWS',   Val:'8 行 × 7 列',                   Kind:'profile', Conf:0.96 },
        { Ref:'表1 行2 列4',        Loc:'表1 行2 列4', Field:'AUDIT_LEADER', Val:'李建国',                        Kind:'profile', Conf:0.88 }
      ]},
    D_TRAIN: { Status:'partial', Completion:0.41, AnchorTotal:17, FilledCount:7, PdfPages:2,
      Template:'培训记录表.xlsx',
      Cells:[
        { Ref:'Sheet1!B2',        Loc:'Sheet1!B2',      Field:'TRAIN_DATE',  Val:'2026-01-15',            Kind:'global',  Conf:1.00 },
        { Ref:'Sheet1!B3',        Loc:'Sheet1!B3',      Field:'TRAIN_TOPIC', Val:'ISO 9001:2015 换版要点', Kind:'profile', Conf:0.82 },
        { Ref:'Sheet1!C3',        Loc:'Sheet1!C3',      Field:'TRAIN_HOURS', Val:'4',                     Kind:'profile', Conf:0.79 },
        { Ref:'Sheet1!A4:F4',     Loc:'数据区 行2',     Field:'TRAIN_ROW2',  Val:'6 列',                  Kind:'profile', Conf:0.55 },
        { Ref:'Sheet1!A5:F5',     Loc:'数据区 行3',     Field:'TRAIN_ROW3',  Val:'6 列',                  Kind:'profile', Conf:0.55 },
        { Ref:'{{table:培训记录}}',Loc:'数据区 行4-9',   Field:'TRAIN_ROW4',  Val:'待填',                  Kind:'pending', Conf:0.00 }
      ]},
    D_AUDITREC:  { Status:'none', Completion:0.00, AnchorTotal:34, FilledCount:0, PdfPages:0, Template:'内审检查表.docx', Cells:[] },
    D_QUALOBJ:   { Status:'generated', Completion:1.00, AnchorTotal:0, FilledCount:0, PdfPages:2, Template:'质量目标.docx', Cells:[] },
    D_REVIEW:    { Status:'none', Completion:0.00, AnchorTotal:16, FilledCount:0, PdfPages:0, Template:'管理评审报告.docx', Cells:[] },
    D_RESP:      { Status:'partial', Completion:0.63, AnchorTotal:8, FilledCount:5, PdfPages:2, Template:'质量方针.docx',
      Cells:[
        { Ref:'{{POLICY_TEXT}}', Loc:'正文·第2段', Field:'POLICY_TEXT', Val:'本组织质量方针：满足顾客要求、持续改进、依法合规。', Kind:'global', Conf:1.00 },
        { Ref:'{{table:质量目标}}',Loc:'表1 行1-6',  Field:'OBJ_ROWS',    Val:'6 行 × 3 列',       Kind:'profile', Conf:0.92 },
        { Ref:'表1 行2 列3',        Loc:'表1 行2 列3',  Field:'OBJ_2026',    Val:'顾客满意度 ≥ 95%', Kind:'profile', Conf:0.69 }
      ]},
    D_CTRLLIST:  { Status:'none', Completion:0.00, AnchorTotal:12, FilledCount:0, PdfPages:0, Template:'受控文件清单.xlsx', Cells:[] },
    D_REVLIST:   { Status:'none', Completion:0.00, AnchorTotal:10, FilledCount:0, PdfPages:0, Template:'文件修订记录.docx', Cells:[] },
    D_ENVREC:    { Status:'none', Completion:0.00, AnchorTotal:22, FilledCount:0, PdfPages:0, Template:'环境因素识别表.xlsx', Cells:[] },
    D_ENVPLAN:   { Status:'none', Completion:0.00, AnchorTotal:18, FilledCount:0, PdfPages:0, Template:'环境管理方案.docx', Cells:[] },
    D_RISK_B:    { Status:'none', Completion:0.00, AnchorTotal:19, FilledCount:0, PdfPages:0, Template:'风险管理报告.docx', Cells:[] },
    D_ORGCHART:  { Status:'platform_generated', Completion:1.00, AnchorTotal:0, FilledCount:0, PdfPages:1, Template:'（平台生成）', Cells:[] }
  },

  /* ================= 企业原始资料（输入侧，只读；仅在候选选择器里出现） ================= */
  rawFiles: [
    { Code:'OF010', Name:'张明远_身份证正面.jpg',       Cat:'身份证明', Stage:'复审', Updated:'2026-10-03', Ver:1, Sha:'a1b2' },
    { Code:'OF011', Name:'张明远_身份证反面.jpg',       Cat:'身份证明', Stage:'复审', Updated:'2026-10-03', Ver:1, Sha:'a1b3' },
    { Code:'OF012', Name:'李建国_身份证正面.jpg',       Cat:'身份证明', Stage:'复审', Updated:'2026-10-03', Ver:1, Sha:'a1b4' },
    { Code:'OF013', Name:'王秀英_身份证正面.jpg',       Cat:'身份证明', Stage:'复审', Updated:'2026-10-03', Ver:1, Sha:'a1b5' },
    { Code:'OF001', Name:'营业执照-副本.pdf',            Cat:'资质证照', Stage:'复审', Updated:'2026-10-03', Ver:1, Sha:'b1c1' },
    { Code:'OF020', Name:'石油天然气开采许可证.pdf',     Cat:'资质证照', Stage:'复审', Updated:'2026-10-03', Ver:1, Sha:'b1c2' },
    { Code:'OF030', Name:'2026年度风险清单.xlsx',       Cat:'记录表格', Stage:'复审', Updated:'2026-10-02', Ver:1, Sha:'c1d1' },
    { Code:'OF031', Name:'2026年度内审计划.docx',       Cat:'体系文件', Stage:'复审', Updated:'2026-10-02', Ver:1, Sha:'c1d2' },
    { Code:'OF032', Name:'质量管理体系风险管理制度.docx', Cat:'制度文件', Stage:'复审', Updated:'2026-09-28', Ver:1, Sha:'c1d3' },
    { Code:'OF033', Name:'2026年1月培训签到表.xlsx',    Cat:'记录表格', Stage:'复审', Updated:'2026-09-20', Ver:1, Sha:'c1d4' },
    { Code:'OF034', Name:'组织架构图.png',              Cat:'技术文件', Stage:'复审', Updated:'2026-09-15', Ver:1, Sha:'c1d5' }
  ],

  /* ======================================================================
     ★ v4 · 依赖追踪（cert_doc_fill_dependency 的 mock）
     ----------------------------------------------------------------------
     两层：
       文件级 —— 本标准文件的内容填写「涉及到哪些企业文件」
       字段级 —— cert_doc_fill_value.DependFileCode（本字段来自哪份企业文件）
     用途：企业重新上传后，能反查「哪些字段被影响」
     ====================================================================== */
  /* 字段级依赖：FieldRef → 依赖的企业原始资料 Code（⛔ 仅 profile/ai 来源才有） */
  fieldDeps: {
    D_RISK: [
      { Field:'RISK_ROWS',   From:'OF030' },   /* 风险清单.xlsx */
      { Field:'RISK_LEVEL@表2行3列2', From:'OF030' },
      { Field:'RISK_LEVEL@表2行4列2', From:'OF030' },
      { Field:'RISK_LEVEL@表2行5列2', From:'OF030' },
      { Field:'RISK_POLICY', From:'OF032' }    /* 风险管理制度.docx */
    ],
    D_AUDITPLAN: [
      { Field:'AUDIT_ROWS',   From:'OF031' },
      { Field:'AUDIT_LEADER', From:'OF031' }
    ],
    D_TRAIN: [
      { Field:'TRAIN_TOPIC', From:'OF033' },
      { Field:'TRAIN_HOURS', From:'OF033' },
      { Field:'TRAIN_ROW2',  From:'OF033' },
      { Field:'TRAIN_ROW3',  From:'OF033' },
      { Field:'TRAIN_ROW4',  From:'OF033' }
    ],
    D_RESP: [
      { Field:'OBJ_ROWS', From:'OF032' },
      { Field:'OBJ_2026', From:'OF030' }
    ]
  },

  /* ★ 演示态：模拟「企业重新上传了 OF030」之后的状态
     —— DependInvalid=1 的字段被清空（值为空、来源标记失效、待重填）
     —— 原型用它展示「字段赋值被清空」的效果 */
  invalidated: {
    File: 'OF030',        /* 哪份企业文件被重新上传了 */
    NewVer: 2,
    NewSha: 'c1d1-x',    /* Sha256 变了 ⇒ VersionNumber+1 ⇒ 依赖失效 */
    At: '2026-10-04 10:22:10',
    /* 受影响的字段：只清 profile/ai 来源，global/compute/replace 保留 */
    Cleared: [
      { Doc:'D_RISK', Field:'RISK_ROWS',            From:'OF030' },
      { Doc:'D_RISK', Field:'RISK_LEVEL@表2行3列2', From:'OF030' },
      { Doc:'D_RISK', Field:'RISK_LEVEL@表2行4列2', From:'OF030' },
      { Doc:'D_RISK', Field:'RISK_LEVEL@表2行5列2', From:'OF030' },
      { Doc:'D_RESP', Field:'OBJ_2026',             From:'OF030' }
    ]
  },

  /* ================= PDF 预览内容（按锚点渲染，模拟 NormalizedPdfPath 的真实内容） ================= */
  pdfMock: {
    D_LICENSE: { pages:2, title:'营业执照（副本）', sub:'统一社会信用代码 91610xxxxxxMA7K3', body:[
      '名称　　G4 测试石油天然气股份有限公司','类型　　有限责任公司（国有控股）',
      '住所　　陕西省西安市雁塔区科技路 xx 号','法定代表人　　张明远',
      '注册资本　伍亿元整','成立日期　2003 年 06 月 18 日','经营范围　石油天然气勘探开发与生产；成品油销售。' ] },
    D_PRODLIC: { pages:4, title:'石油天然气开采许可证', sub:'证号 61010xxxxxxxx', body:[
      '许可证名称：石油天然气开采许可证','证　　号：61010xxxxxxxx','发证机关：陕西省能源局',
      '开采矿种：石油、天然气','开采方式：钻井','企业名称：G4 测试石油天然气股份有限公司',
      '★ 有效期限：2017-05-01 至 2027-04-30','⚠ 评审提醒：距到期不足 7 个月' ] },
    D_RISK: { pages:6, title:'风险管理报告', sub:'文件编号 YZH-FX-2026-001', body:[
      '一、编制说明','本报告依据 GB/T 19001-2016 第 6.1 条编制，覆盖本公司及所属 12 个生产单位的质量管理体系活动。',
      '二、评价依据','质量管理体系风险管理制度 第3章 风险评价准则。',
      '三、风险清单（节选）','表1 共识别 8 项风险，其中高风险 1 项、中风险 2 项。',
      '四、结论与措施','已制定相应控制措施并纳入年度目标跟踪。',
      '（落款）编制：质量管理部　　审核：张明远　　签发日期：2026-03-20' ] },
    D_AUDITPLAN: { pages:4, title:'2026 年度内部审核计划', sub:'文件编号 YZH-AUD-2026-001', body:[
      '一、审核目的','验证体系持续满足 GB/T 19001-2016 及 IAF MD 5.2 要求。',
      '二、审核范围','覆盖总部及 12 个生产单位的质量体系活动。',
      '三、审核组','组长：李建国；组员：王秀英、张明远（技术专家）',
      '四、审核安排（节选）','表1 共 8 行 7 列，自 2026-04-06 起分四批实施。' ] }
  },

  /* ================= 队列任务（与 yzh_queue_task 对齐） ================= */
  queueTasks: [
    { Doc:'D_LICENSE',   Type:'固定',   State:'success', Ms:820  },
    { Doc:'D_IDCARD',    Type:'固定',   State:'success', Ms:1240 },
    { Doc:'D_PRODLIC',   Type:'固定',   State:'success', Ms:660  },
    { Doc:'D_SYSFILE',   Type:'固定',   State:'success', Ms:410  },
    { Doc:'D_CTRLLIST',  Type:'可编辑', State:'failed',  Ms:520  },
    { Doc:'D_REVLIST',   Type:'可编辑', State:'failed',  Ms:480  },
    { Doc:'D_RISK',      Type:'可编辑', State:'success', Ms:3480 },
    { Doc:'D_AUDITPLAN', Type:'可编辑', State:'success', Ms:2910 },
    { Doc:'D_AUDITREC',  Type:'可编辑', State:'failed',  Ms:500  },
    { Doc:'D_TRAIN',     Type:'可编辑', State:'success', Ms:4120 },
    { Doc:'D_QUALOBJ',   Type:'可编辑', State:'success', Ms:380  },
    { Doc:'D_REVIEW',    Type:'可编辑', State:'skipped', Ms:0    },
    { Doc:'D_RESP',      Type:'可编辑', State:'success', Ms:2050 },
    { Doc:'D_ORGCHART',  Type:'生成',   State:'success', Ms:300  }
  ]
};

/* 队列失败原因（人话，26 号 S-8） */
MOCK.failReason = '模板未上传（后台「标准文档填写规则」尚未配置此模板）';