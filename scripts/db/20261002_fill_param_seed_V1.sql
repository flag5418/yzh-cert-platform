-- ============================================================================
-- 20261002_fill_param_seed_V1.sql
-- 体系认证全局参数 —— 字典注册 + 内置参数示例数据
--
-- ① 3 个数据字典（供 EntityConfig 的 DictCode 下拉使用）
-- ② 内置参数示例（★ 直接对应 25 号实测报告 §8.5「企业概况缺 4 类字段」的结论）
--
-- ⛔ 字典明细表的关联键是 Sys_DictionaryList.DicCode = Sys_Dictionary.Code（不是 DicNo）
-- ============================================================================

SET NAMES utf8mb4;

-- ────────────────────────────────────────────────────────────────────────────
-- ① 数据字典
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `Sys_Dictionary` (`Code`, `DicName`, `DicNo`, `ParentCode`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`, `Remark`)
VALUES
  ('FILL_VALUE_TYPE',    '全局参数值类型',   'fill_value_type',    '26b0f1d2ae6c11f1953796fd503fd974', 300, 1, 0, NOW(), 'seed_fill_param', '体系认证全局参数 ValueType 下拉'),
  ('FILL_SOURCE_KIND',   '全局参数取值来源', 'fill_source_kind',   '26b0f1d2ae6c11f1953796fd503fd974', 310, 1, 0, NOW(), 'seed_fill_param', '体系认证全局参数 SourceKind 下拉'),
  ('FILL_MAINTAIN_MODE', '全局参数维护方式', 'fill_maintain_mode', '26b0f1d2ae6c11f1953796fd503fd974', 320, 1, 0, NOW(), 'seed_fill_param', '体系认证全局参数 MaintainMode 下拉')
ON DUPLICATE KEY UPDATE
  `DicName` = VALUES(`DicName`), `ParentCode` = VALUES(`ParentCode`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;

INSERT INTO `Sys_DictionaryList` (`Code`, `DicCode`, `DicValue`, `DicName`, `OrderNo`, `IsValid`, `IsDeleted`, `CreateTime`, `CreateBy`)
VALUES
  -- 值类型
  (CONCAT('FVT_', 'text'),   'FILL_VALUE_TYPE', 'text',   '文本',   10, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FVT_', 'number'), 'FILL_VALUE_TYPE', 'number', '数字',   20, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FVT_', 'date'),   'FILL_VALUE_TYPE', 'date',   '日期',   30, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FVT_', 'enum'),   'FILL_VALUE_TYPE', 'enum',   '枚举',   40, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FVT_', 'bool'),   'FILL_VALUE_TYPE', 'bool',   '是否',   50, 1, 0, NOW(), 'seed_fill_param'),
  -- 取值来源（★ 对应 22 册 §二 的 7 类来源，一期实测只用到前 3 类 + ai）
  (CONCAT('FSK_', 'global'),       'FILL_SOURCE_KIND', 'global',       '全局参数',    10, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FSK_', 'replace'),      'FILL_SOURCE_KIND', 'replace',      '替换',        20, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FSK_', 'headerFooter'), 'FILL_SOURCE_KIND', 'headerFooter', '页眉页脚',    30, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FSK_', 'ai'),           'FILL_SOURCE_KIND', 'ai',           'AI 生成',     40, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FSK_', 'manual'),       'FILL_SOURCE_KIND', 'manual',       '人工填写',    50, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FSK_', 'compute'),      'FILL_SOURCE_KIND', 'compute',      '计算派生',    60, 1, 0, NOW(), 'seed_fill_param'),
  -- 维护方式
  (CONCAT('FMM_', 'auto'),   'FILL_MAINTAIN_MODE', 'auto',   '自动映射（企业只读）',   10, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FMM_', 'manual'), 'FILL_MAINTAIN_MODE', 'manual', '企业手工填写',           20, 1, 0, NOW(), 'seed_fill_param'),
  (CONCAT('FMM_', 'both'),   'FILL_MAINTAIN_MODE', 'both',   '自动带出可覆盖',         30, 1, 0, NOW(), 'seed_fill_param')
ON DUPLICATE KEY UPDATE
  `DicValue` = VALUES(`DicValue`), `DicName` = VALUES(`DicName`),
  `OrderNo` = VALUES(`OrderNo`), `IsValid` = 1, `IsDeleted` = 0;


-- ────────────────────────────────────────────────────────────────────────────
-- ② 内置参数示例
--    机构 = 河北雄安尚龙认证有限公司 / 标准 = 9001标准 / 阶段 = 复审
--    ★ 第 1 组「基础信息」全部 MaintainMode=auto，SourceExpr 指向 cert_enterprise 列
--    ★ 第 2 组「体系信息」全部 MaintainMode=manual —— 正是 25 号实测发现的
--      「企业概况需要 7 类信息，cert_enterprise 只能供 3 类」的缺口补充
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_fill_param_def`
  (`Code`, `OrgCode`, `StandardCode`, `StageCode`,
   `ParamCode`, `ParamName`, `GroupName`, `ValueType`, `EnumOptions`,
   `SourceKind`, `SourceExpr`, `MaintainMode`, `DefaultValue`, `Placeholder`,
   `IsRequired`, `IsBuiltin`, `SortOrder`, `Description`,
   `CreateBy`, `CreateTime`, `IsDeleted`, `IsValid`)
VALUES
  -- ── 组 1：基础信息（自动映射，企业端只读）──
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'company_name', '企业全称', '基础信息', 'text', NULL,
   'global', 'enterprise.Name', 'auto', NULL, '与营业执照一致的企业全称',
   1, 1, 10, '直接取「企业管理」中的企业全称，改一处全文档同步',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'company_short_name', '企业简称', '基础信息', 'text', NULL,
   'global', 'enterprise.ShortName', 'auto', NULL, NULL,
   0, 1, 20, '用于页眉页脚与文件编号前缀的展示',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'credit_code', '统一社会信用代码', '基础信息', 'text', NULL,
   'global', 'enterprise.CreditCode', 'auto', NULL, '18 位统一社会信用代码',
   1, 1, 30, '固定 18 位，可校验位校验',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'legal_person', '法定代表人', '基础信息', 'text', NULL,
   'global', 'enterprise.LegalPerson', 'auto', NULL, NULL,
   1, 1, 40, '用于质量手册「颁布令」签署人',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'company_address', '企业地址', '基础信息', 'text', NULL,
   'global', 'enterprise.Address', 'auto', NULL, NULL,
   1, 1, 50, '质量手册「认证范围」中的场所地址',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'industry_type', '行业类型', '基础信息', 'text', NULL,
   'global', 'enterprise.IndustryType', 'auto', NULL, NULL,
   0, 1, 60, '影响 AI 生成时的行业语境',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'cert_scope', '认证范围', '基础信息', 'text', NULL,
   'global', 'enterprise.CertScope', 'both', NULL, '如：手动轮椅车的生产和销售',
   1, 1, 70, '★ 质量手册 1.3 节「适用范围」直接取此值',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'employee_count', '员工人数', '基础信息', 'number', NULL,
   'global', 'enterprise.EmployeeCount', 'auto', NULL, NULL,
   0, 1, 80, '影响内审/管评频次的判定',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'contact_name', '体系对接人', '基础信息', 'text', NULL,
   'global', 'enterprise.ContactName', 'auto', NULL, NULL,
   0, 1, 90, NULL,
   'seed_fill_param', NOW(), 0, 1),

  -- ── 组 2：体系信息（企业手工填 —— 25 号实测发现的 cert_enterprise 缺口）──
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'doc_prefix', '文件编号前缀', '体系信息', 'text', NULL,
   'global', NULL, 'manual', NULL, '如 XASL（企业简称拼音首字母）',
   1, 1, 110, '★ 实测 167 份文档中 444 处文件编号锚点全靠它；改一处，全套编号同步',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'established_date', '成立日期', '体系信息', 'date', NULL,
   'global', NULL, 'manual', NULL, '如 2007-04-29',
   1, 0, 120, '★ cert_enterprise 无此列 —— 质量手册 0.4 企业概况必需',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'qualifications', '持有资质证书', '体系信息', 'text', NULL,
   'global', NULL, 'manual', NULL, '如：医疗器械生产备案凭证、医疗器械经营备案凭证',
   0, 0, 130, '★ cert_enterprise 无此列 —— 质量手册 0.4 企业概况必需',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'main_products', '主要产品', '体系信息', 'text', NULL,
   'global', NULL, 'manual', NULL, '如：可折叠轮椅、医用智能轮椅',
   0, 0, 140, '★ cert_enterprise 无此列 —— 质量手册 0.4 企业概况必需',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'honors', '企业荣誉 / 入库情况', '体系信息', 'text', NULL,
   'global', NULL, 'manual', NULL, '如：2026 年区/县级高新技术培育库入库企业',
   0, 0, 150, '★ cert_enterprise 无此列 —— 质量手册 0.4 企业概况必需',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'quality_policy', '质量方针', '体系信息', 'text', NULL,
   'ai', NULL, 'both', '满足顾客需求是我们长期的质量方针；持续质量改进是公司发展的根本动力。',
   '企业自述，可 AI 提炼后人工确认',
   0, 0, 160, '对应《质量手册》附录二；AI 任务一律「根据素材提炼」，⛔ 禁凭空生成',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'quality_objective', '质量目标', '体系信息', 'text', NULL,
   'ai', NULL, 'both', '客户投诉率≤2%，顾客满意度≥95分，产品一次检验合格率≥98%',
   '企业自述，可 AI 提炼后人工确认',
   0, 0, 170, '对应《质量手册》附录二',
   'seed_fill_param', NOW(), 0, 1),
  (UUID(), '906e8b2a962c4062b21144af4cc4abc0', '846dec4b-c534-4983-94e6-8cf04982b7d9', '29c1bcc3a18942e1865b2497a0262504',
   'company_profile', '企业概况', '体系信息', 'text', NULL,
   'ai', NULL, 'both', NULL, '150~250 字，由企业基本信息提炼',
   0, 0, 180, '★ 对应《质量手册》0.4 节 —— 实测 1066 行手册里唯一一段需要「创作」的文本',
   'seed_fill_param', NOW(), 0, 1)
ON DUPLICATE KEY UPDATE
  `ParamName` = VALUES(`ParamName`), `GroupName` = VALUES(`GroupName`),
  `ValueType` = VALUES(`ValueType`), `SourceKind` = VALUES(`SourceKind`),
  `SourceExpr` = VALUES(`SourceExpr`), `MaintainMode` = VALUES(`MaintainMode`),
  `DefaultValue` = VALUES(`DefaultValue`), `Placeholder` = VALUES(`Placeholder`),
  `IsRequired` = VALUES(`IsRequired`), `IsBuiltin` = VALUES(`IsBuiltin`),
  `SortOrder` = VALUES(`SortOrder`), `Description` = VALUES(`Description`),
  `IsDeleted` = 0, `IsValid` = 1;

-- ── 验证 ──
-- SELECT ParamCode, ParamName, GroupName, ValueType, SourceKind, SourceExpr, MaintainMode, SortOrder
--   FROM cert_fill_param_def
--  WHERE OrgCode='906e8b2a962c4062b21144af4cc4abc0' AND IsDeleted=0
--  ORDER BY SortOrder;
