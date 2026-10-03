-- ============================================================================
-- 20261002_tag_dict_seed_V1.sql
-- cert_tag_dict —— 平台级默认标签种子（首批 27 个）
--
-- 为什么必须先有种子（33 号 §3.1 / 验收 A1 / A6）：
--   · {{tag_list}} 由 cert_tag_dict 渲染 —— 表为空时提示词会注入
--     「字典暂无标签」，LLM 无从选择，输出必然全部越界；
--   · 后端校验要求 tags[].tagCode **必须 ∈ cert_tag_dict.TagCode**，
--     越界强制回退 OTHER —— 连 OTHER 都没有就无法回退；
--   · 验收 A1 要求「≥8 个标签初值」，A6 要求 OTHER 占比 ≤15%。
--
-- 设计来源（33 号 §3.1「种子来源」三条，逐条对应）：
--   ① 标准目录文件夹树 —— 实测 1质量手册 / 2程序文件 / 3制度文件 / 4记录文件 /
--                          检验作业指导书 / 技术类 / 生产类 / 质量类 / 其它
--   ② 体系域 —— 质量 / 环境 / 职业健康安全
--   ③ ISO 条款业务过程 —— 文件控制 / 内审 / 管理评审 / 培训 / 设备 / 计量 /
--                          不合格品 / 纠正预防 / 采购 / 检验 / 风险 / 变更 / 顾客 …
--
-- 分组 TagGroup 三值（按 §3.1「三层分类」）：
--   形态 / 业务过程 / 体系域   —— 另加 兜底（OTHER 专用）
--
-- ⚠️ StandardCodes = NULL 表示**全标准通用**（33 号 Q1 裁决 C1 的「空 = 通用」语义）。
--    某标准专属标签由工作台「AI 生成」产出，GenSource = 'ai'。
--
-- ⛔ 铁律：TagCode PascalCase；启用 = IsValid(int 0/1)；状态 = Status 'active'；
--    utf8mb4 + COLLATE=utf8mb4_general_ci
--
-- 执行：
--   docker exec -i -e MYSQL_PWD="Yzh123456." yzh-mysql mysql -uroot \
--     --default-character-set=utf8mb4 yzh_cert_platform \
--     < scripts/db/20261002_tag_dict_seed_V1.sql
--
-- 设计依据：33-文档语义规则设计-V1.md §3.1 / §4.3 / 验收 A1
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- ① 形态（8）—— 直接来自标准目录文件夹树
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_tag_dict`
  (`Code`, `TagCode`, `TagName`, `TagGroup`, `ApplicableSide`, `StandardCodes`,
   `MatchFeature`, `SampleDocNames`, `TagPurposeHint`,
   `GenSource`, `IsManualCorrected`, `Sort`, `Status`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  (UUID(), 'ManualQuality',      '质量手册',   '形态', 'both', NULL,
   '质量手册|管理手册|体系手册|QM-', '质量手册.doc;管理手册.doc',
   '【是什么】质量管理体系的纲领性文件【审核关注点】体系范围、职责、过程相互作用与实际一致【来源口径】质量部编制、管理者代表批准【包含信息】体系范围、组织机构、方针目标、过程清单',
   'manual', 0, 10, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'ProcedureDocument',  '程序文件',   '形态', 'both', NULL,
   '程序文件|控制程序|管理办法|QP-', 'QP-01 文件控制程序.doc;QP-05 内部审核控制程序.doc',
   '【是什么】规定某项质量活动的职责、权限与实施步骤的文件【审核关注点】职责分工明确、与实际流程一致、受控发放【来源口径】归口部门编制、管理者代表批准【包含信息】文件编号、版本号、生效日期、编制/审核/批准人',
   'manual', 0, 20, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'WorkInstruction',    '作业指导书', '形态', 'both', NULL,
   '作业指导书|操作规程|检验作业指导书|SOP|QW-', '检验作业指导书.doc;设备操作规程.doc',
   '【是什么】指导具体岗位/设备/工序如何作业的文件【审核关注点】参数与实际设备一致、可操作、现场可得【来源口径】技术/生产部门编制【包含信息】适用范围、工艺参数、操作步骤、安全注意事项',
   'manual', 0, 30, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'SystemRegulation',   '制度文件',   '形态', 'both', NULL,
   '制度|规定|规范|管理办法|管理制度', '考勤管理制度.doc;安全生产管理制度.doc',
   '【是什么】企业内部通用管理规定【审核关注点】与法规/体系要求不冲突、已宣贯执行【来源口径】归口部门拟定、总经理批准【包含信息】适用范围、职责、管理要求、考核标准',
   'manual', 0, 40, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'RecordForm',         '记录表格',   '形态', 'both', NULL,
   '记录|台账|清单|表格|检查表|签到表|一览表|QR-', 'XASL-QR-001 受控文件清单.xls;XASL-QR-014 年度内审计划.doc',
   '【是什么】体系运行留痕的表单/台账【审核关注点】填写及时完整、有签署、可追溯【来源口径】各归口部门按期填写、部门负责人确认【包含信息】记录编号、填写日期、责任人、结论、保存期限',
   'manual', 0, 50, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'CertificateLicense', '证书证照',   '形态', 'both', NULL,
   '营业执照|资质证书|许可证|认证证书|检验报告|检测报告', '营业执照.jpg;ISO9001证书.pdf',
   '【是什么】证明企业法人资格、资质或产品合规的证照【审核关注点】在有效期内、范围覆盖经营范围、年检齐全【来源口径】政府/认证机构颁发【包含信息】证书编号、有效期、批准范围、发证机关',
   'manual', 0, 60, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'ContractAgreement',  '合同协议',   '形态', 'both', NULL,
   '合同|协议|订单|承诺书|授权书', '销售合同.doc;供方质量协议.doc',
   '【是什么】明确双方权利义务的商务/质量约定【审核关注点】质量条款明确、签署齐全、评审记录【来源口径】业务部门签订、法务/质量会签【包含信息】合同编号、甲乙方、质量要求、交付与违约条款',
   'manual', 0, 70, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'ReportDocument',     '报告文档',   '形态', 'both', NULL,
   '报告|评价|分析|自查', '风险管理报告.doc;医疗器械召回计划.doc',
   '【是什么】针对某项活动/问题的分析与结论性文件【审核关注点】数据真实、结论有据、措施闭环【来源口径】归口部门编制、分管领导批准【包含信息】报告期、范围、方法、发现、结论、改进建议',
   'manual', 0, 80, 'active', 1, 0, NOW(), 'seed_tag_dict')
ON DUPLICATE KEY UPDATE
  `TagName` = VALUES(`TagName`), `TagGroup` = VALUES(`TagGroup`),
  `ApplicableSide` = VALUES(`ApplicableSide`), `MatchFeature` = VALUES(`MatchFeature`),
  `SampleDocNames` = VALUES(`SampleDocNames`), `TagPurposeHint` = VALUES(`TagPurposeHint`),
  `Sort` = VALUES(`Sort`), `Status` = 'active', `IsValid` = 1, `IsDeleted` = 0,
  `UpdateTime` = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- ② 业务过程（15）—— 来自 ISO 条款业务过程 + 样例文件实测
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_tag_dict`
  (`Code`, `TagCode`, `TagName`, `TagGroup`, `ApplicableSide`, `StandardCodes`,
   `MatchFeature`, `SampleDocNames`, `TagPurposeHint`,
   `GenSource`, `IsManualCorrected`, `Sort`, `Status`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  (UUID(), 'FileControl',          '文件控制',       '业务过程', 'both', NULL,
   '受控文件|文件发放|文件回收|文件销毁|修订|废止|受控文件清单', 'XASL-QR-001 受控文件清单.xls;XASL-QR-003 文件发放、回收记录表.doc;XASL-QR-005 文件销毁登记表.doc',
   '【是什么】文件从编制到作废全过程的受控记录【审核关注点】现场使用文件均为有效版本、发放回收有据【来源口径】综合管理部归口管理【包含信息】文件编号、版本、发放日期、回收/销毁日期、责任人',
   'manual', 0, 110, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'InternalAudit',         '内部审核',       '业务过程', 'both', NULL,
   '内部审核|内审计划|内审检查表|内审报告|陪审', 'XASL-QR-014 年度内审计划.doc;XASL-QR-017 内部审核检查表.doc;XASL-QR-019 内部质量审核报告.doc;陪审人员.doc',
   '【是什么】体系内部审核的策划、实施与报告记录【审核关注点】审核员独立于被审部门、不符合项闭环【来源口径】质量管理部组织、内审组长负责【包含信息】年度、审核组长、审核范围、审核日期、不符合项、整改验证',
   'manual', 0, 120, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'ManagementReview',      '管理评审',       '业务过程', 'both', NULL,
   '管理评审|评审计划|评审报告|评审输入', 'XASL-QR-011 管理评审计划.doc;XASL-QR-013 管理评审报告.doc',
   '【是什么】最高管理者按期评价体系适宜性、充分性、有效性的记录【审核关注点】输入齐全、决议有落实跟踪【来源口径】总经理主持、质量管理部组织【包含信息】评审时间、参加人、输入项、决议事项、责任部门、完成期限',
   'manual', 0, 130, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'PersonnelTraining',     '人员培训',       '业务过程', 'both', NULL,
   '培训计划|培训记录|上岗证|考核|能力|意识', '年度培训计划.doc;培训记录表.doc',
   '【是什么】人员能力、培训与意识的管理记录【审核关注点】关键岗位持证上岗、培训效果评价【来源口径】人力资源部/综合管理部归口【包含信息】培训对象、课程、时间、讲师、考核结果、上岗资格',
   'manual', 0, 140, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'EquipmentCalibration',  '设备与计量',     '业务过程', 'both', NULL,
   '监视和测量设备|台账|校准|检定|维护保养|期间核查', 'XASL-QR-006 监视和测量设备台账.xlsx',
   '【是什么】监视和测量设备的台帐、校准与维护记录【审核关注点】在检定有效期内、状态标识清晰【来源口径】设备/计量部门归口【包含信息】设备编号、名称、型号、检定日期、有效期、下次检定、责任人',
   'manual', 0, 150, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'NonconformityControl',  '不合格品控制',   '业务过程', 'both', NULL,
   '不合格品|让步接收|返工|报废|处置|评审', 'XASL-QR-020 不合格品通知及评审处置表.doc',
   '【是什么】不合格品识别、标识、隔离与处置的记录【审核关注点】不流入下道工序/交付、处置有授权【来源口径】质量部判定、授权人批准【包含信息】不合格描述、数量、原因、处置方式、批准人、验证结论',
   'manual', 0, 160, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'CorrectiveAction',      '纠正与预防措施', '业务过程', 'both', NULL,
   '纠正措施|预防措施|纠正预防|CAPA|原因分析|整改', 'XASL-QR-029 纠正预防措施表.doc;XASL-QR-030 纠正预防措施清单.doc',
   '【是什么】针对问题/不符合的原因分析与纠正预防措施记录【审核关注点】根因分析到位、措施有效并验证关闭【来源口径】责任部门制定、质量部跟踪验证【包含信息】问题来源、根本原因、措施、责任人、完成期限、验证结论',
   'manual', 0, 170, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'PurchasingControl',     '采购与供方',     '业务过程', 'both', NULL,
   '采购计划|供方评价|合格供方|请验单|进货|原辅料', 'XASL-QR-024 原材料请验单.doc;合格供方名录.doc',
   '【是什么】采购信息与供方评价、来料验收的记录【审核关注点】供方在合格名录内、采购要求含质量条款【来源口径】采购部归口、质量部参与评价【包含信息】供方名称、物料、采购日期、验收结论、供方等级',
   'manual', 0, 180, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'InspectionRecord',      '检验与试验',     '业务过程', 'both', NULL,
   '检验记录|过程检验|成品检验|终检|请验单|放行', 'XASL-QR-037 过程检验记录.xlsx;XASL-QR-027 成品检验记录.xlsx;XASL-QR-028 产品放行审核记录.doc',
   '【是什么】来料、过程、成品检验的实施与放行记录【审核关注点】检验项目齐全、判定有据、放行授权【来源口径】质量部/检验员实施【包含信息】批次、检验项、实测值、判定、检验员、放行日期',
   'manual', 0, 190, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'RiskManagement',        '风险与机遇',     '业务过程', 'both', NULL,
   '风险|机遇|相关方需求|应对措施|风险识别', '风险与机遇识别评价和应对措施表.xls;相关方需求和期望一览表.xls',
   '【是什么】风险与机遇的识别、评价与应对策划【审核关注点】覆盖体系全部过程、措施与实际一致【来源口径】各部门识别、质量管理部汇总【包含信息】风险项、评价准则、等级、应对措施、责任部门、评审结论',
   'manual', 0, 200, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'ChangeControl',         '变更控制',       '业务过程', 'both', NULL,
   '变更申请|变更评审|更改申请|修订申请|版本变更', 'XASL-QR-002 文件修订 废止申请表.doc;XASL-QR-004 文件补（换）申请单.doc',
   '【是什么】文件/过程/设计变更的申请、评审与实施记录【审核关注点】变更经评审、影响已评估、版本受控【来源口径】发起部门申请、相关方会签【包含信息】变更内容、原因、影响评估、会签意见、生效日期',
   'manual', 0, 210, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'CustomerFeedback',      '顾客反馈与满意', '业务过程', 'both', NULL,
   '顾客满意|顾客反馈|投诉|退货|质量信息反馈|满意度', 'XASL-QR-023 质量信息反馈单.doc;顾客满意度调查表.doc',
   '【是什么】顾客反馈、投诉处理与满意度测量的记录【审核关注点】投诉闭环、满意度统计有分析【来源口径】销售/客服归口、质量部协同【包含信息】反馈来源、问题描述、处理措施、回复日期、满意度得分',
   'manual', 0, 220, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'ProductionProcess',     '生产过程控制',   '业务过程', 'both', NULL,
   '生产计划|工艺卡|批记录|首件|过程确认|需确认过程', '需确认过程确认.doc;生产批记录.doc',
   '【是什么】生产运行过程的策划、执行与监控记录【审核关注点】工艺参数受控、特殊过程确认有效【来源口径】生产部归口【包含信息】批次、工序、工艺参数、操作人、确认结论、数量',
   'manual', 0, 230, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'RecallTraceability',    '召回与追溯',     '业务过程', 'both', NULL,
   '召回|追溯|追溯性|模拟召回|UDI', 'XASL-QR-035 召回计划实施情况报告.doc;XASL-QR-032 医疗器械召回计划.doc',
   '【是什么】产品召回预案与追溯（含模拟召回）的演练记录【审核关注点】追溯链完整、时限达标【来源口径】质量部组织、销售/生产配合【包含信息】产品、批次、去向、数量、召回时限、演练结论',
   'manual', 0, 240, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'StorageProtection',     '仓储与防护',     '业务过程', 'both', NULL,
   '仓库|储存|防护|环境温湿度|留样|台账', 'XASL-QR-039 留样台账.doc;XASL-QR-036 温度、湿度记录表.doc',
   '【是什么】库存产品/物料的储存、防护与环境监控记录【审核关注点】环境受控、先进先出、留样齐全【来源口径】仓储部归口【包含信息】库位、物料/批次、温湿度、日期、异常处理',
   'manual', 0, 250, 'active', 1, 0, NOW(), 'seed_tag_dict')
ON DUPLICATE KEY UPDATE
  `TagName` = VALUES(`TagName`), `TagGroup` = VALUES(`TagGroup`),
  `ApplicableSide` = VALUES(`ApplicableSide`), `MatchFeature` = VALUES(`MatchFeature`),
  `SampleDocNames` = VALUES(`SampleDocNames`), `TagPurposeHint` = VALUES(`TagPurposeHint`),
  `Sort` = VALUES(`Sort`), `Status` = 'active', `IsValid` = 1, `IsDeleted` = 0,
  `UpdateTime` = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- ③ 体系域（3）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_tag_dict`
  (`Code`, `TagCode`, `TagName`, `TagGroup`, `ApplicableSide`, `StandardCodes`,
   `MatchFeature`, `SampleDocNames`, `TagPurposeHint`,
   `GenSource`, `IsManualCorrected`, `Sort`, `Status`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  (UUID(), 'QualityManagement',     '质量管理体系',   '体系域', 'both', NULL,
   'ISO9001|质量管理体系|质量方针|质量目标', '质量方针目标展开表.doc',
   '【是什么】质量管理体系（ISO 9001）相关文件【审核关注点】体系范围与实际一致【来源口径】质量部归口【包含信息】体系范围、过程、目标指标',
   'manual', 0, 310, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'EnvironmentManagement', '环境管理体系',   '体系域', 'both', NULL,
   'ISO14001|环境管理体系|环境因素|合规义务', '环境因素识别评价表.doc',
   '【是什么】环境管理体系（ISO 14001）相关文件【审核关注点】环境因素识别充分、合规义务清单完整【来源口径】环保/EHS 归口【包含信息】环境因素、排放数据、法规条款、目标指标',
   'manual', 0, 320, 'active', 1, 0, NOW(), 'seed_tag_dict'),
  (UUID(), 'OHSHSManagement',       '职业健康安全管理体系', '体系域', 'both', NULL,
   'ISO45001|职业健康安全|危险源|劳保', '危险源辨识与风险评价表.doc',
   '【是什么】职业健康安全管理体系（ISO 45001）相关文件【审核关注点】危险源辨识全面、控制措施落实【来源口径】安全/EHS 归口【包含信息】危险源、风险等级、控制措施、应急措施、培训记录',
   'manual', 0, 330, 'active', 1, 0, NOW(), 'seed_tag_dict')
ON DUPLICATE KEY UPDATE
  `TagName` = VALUES(`TagName`), `TagGroup` = VALUES(`TagGroup`),
  `ApplicableSide` = VALUES(`ApplicableSide`), `MatchFeature` = VALUES(`MatchFeature`),
  `SampleDocNames` = VALUES(`SampleDocNames`), `TagPurposeHint` = VALUES(`TagPurposeHint`),
  `Sort` = VALUES(`Sort`), `Status` = 'active', `IsValid` = 1, `IsDeleted` = 0,
  `UpdateTime` = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- ④ 兜底（后端 tagCode 越界强制回退的目标，必须存在）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_tag_dict`
  (`Code`, `TagCode`, `TagName`, `TagGroup`, `ApplicableSide`, `StandardCodes`,
   `MatchFeature`, `SampleDocNames`, `TagPurposeHint`,
   `GenSource`, `IsManualCorrected`, `Sort`, `Status`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  (UUID(), 'OTHER', '其他', '兜底', 'both', NULL,
   NULL, NULL,
   '【是什么】无法归入任何既有标签的文档【审核关注点】是否应新建标签并进待审核【来源口径】不适用【包含信息】不适用',
   'manual', 1, 900, 'active', 1, 0, NOW(), 'seed_tag_dict')
ON DUPLICATE KEY UPDATE
  `TagName` = VALUES(`TagName`), `TagGroup` = VALUES(`TagGroup`),
  `Sort` = VALUES(`Sort`), `Status` = 'active', `IsValid` = 1, `IsDeleted` = 0,
  `UpdateTime` = NOW();

-- ────────────────────────────────────────────────────────────────────────────
-- 幂等：①②③ 段若重复执行，靠 uk_tag_code 冲突会报错 → 统一兜底 UPDATE
-- ────────────────────────────────────────────────────────────────────────────
UPDATE `cert_tag_dict`
   SET `Status` = 'active', `IsValid` = 1, `IsDeleted` = 0, `UpdateTime` = NOW()
 WHERE `TagCode` IN ('ManualQuality','ProcedureDocument','WorkInstruction','SystemRegulation',
                     'RecordForm','CertificateLicense','ContractAgreement','ReportDocument',
                     'FileControl','InternalAudit','ManagementReview','PersonnelTraining',
                     'EquipmentCalibration','NonconformityControl','CorrectiveAction',
                     'PurchasingControl','InspectionRecord','RiskManagement','ChangeControl',
                     'CustomerFeedback','ProductionProcess','RecallTraceability','StorageProtection',
                     'QualityManagement','EnvironmentManagement','OHSHSManagement','OTHER');

-- ────────────────────────────────────────────────────────────────────────────
-- 验证
-- ────────────────────────────────────────────────────────────────────────────
-- 期望：total = 27，has_other = 1，groups 为 形态/业务过程/体系域/兜底 四类
SELECT COUNT(*) AS total,
       SUM(`TagCode` = 'OTHER') AS has_other,
       SUM(`IsDeleted` = 0 AND `IsValid` = 1) AS active_count
  FROM `cert_tag_dict`;

SELECT `TagGroup`, COUNT(*) AS c
  FROM `cert_tag_dict` WHERE `IsDeleted` = 0 AND `IsValid` = 1
 GROUP BY `TagGroup` ORDER BY MIN(`Sort`);

-- 归零检查：OTHER 必须存在（否则后端越界回退会失败）
SELECT COUNT(*) AS other_missing
  FROM (SELECT 'OTHER' AS k) want
 WHERE NOT EXISTS (SELECT 1 FROM `cert_tag_dict` c
                    WHERE c.`TagCode` = want.k AND c.`IsDeleted` = 0 AND c.`IsValid` = 1);
-- 期望 other_missing = 0
