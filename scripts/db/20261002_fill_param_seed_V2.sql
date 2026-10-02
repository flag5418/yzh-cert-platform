-- ============================================================================
-- 20261002_fill_param_seed_V2.sql
-- 体系认证全局参数 —— 种子修正：内置参数改「通用」+ 补作用域示例
--
-- 为什么需要 V2（V1 的问题）：
--   V1 把 17 条内置参数**全部**绑死在「9001标准 × 复审」上，导致：
--     ① 企业端不选标准/阶段时 `merge-list` 返回 **0 项** —— 页面打开是空的，
--        看起来像「功能没做完」，而真实原因是「参数被限定到了某个标准/阶段」；
--     ② 「机构 × 标准 × 阶段」这个设计点**无法被看见**（全是同一条）。
--
-- V2 的语义（对齐 23 号 §四「标准 × 阶段裁剪」+ ParamValueResolver.Specificity）：
--   基础层  —— 17 条内置参数 → `StandardCode=''` + `StageCode=''`（**通用**，任何标准/阶段都出）
--   覆写层  —— 某标准可覆写同名参数（Specificity: 指定标准 +2、指定阶段 +1，取最高）
--   扩展层  —— 标准专属 / 阶段专属的**新增**参数（只在该标准/阶段出现）
--
-- ⛔ 身份段（OrgCode/StandardCode/StageCode）NOT NULL DEFAULT ''，
--    「通用」必须写**空串**而不是 NULL（C# 侧判据是 `== ""`，NULL 判不中 → 静默查不到）
-- ============================================================================

-- ⛔ 必须指定 COLLATE：本库列是 utf8mb4_general_ci，而 MySQL 8 默认连接排序规则是
--    utf8mb4_0900_ai_ci —— 用 @变量 与列比较时会报
--    「Illegal mix of collations ... for operation '='」（不是语法错，是排序规则冲突）
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 机构 = 河北雄安尚龙认证有限公司
SET @cb   := '906e8b2a962c4062b21144af4cc4abc0';
-- 标准 = 9001标准 / 食品标准
SET @std9001 := '846dec4b-c534-4983-94e6-8cf04982b7d9';
SET @stdFood := '475da4fe-8f50-4bf7-bf2b-b39869d5ddf7';
-- 阶段 = 复审
SET @stgReview := '29c1bcc3a18942e1865b2497a0262504';

-- ────────────────────────────────────────────────────────────────────────────
-- ① 把 V1 种下的 17 条内置参数**放归通用**
--    这些参数（企业全称 / 信用代码 / 法定代表人 / 地址…）本来就是「任何标准、任何阶段
--    都需要」的基础信息，绑在「9001 × 复审」上是种子的表述错误，不是设计意图。
-- ────────────────────────────────────────────────────────────────────────────
UPDATE `cert_fill_param_def`
   SET `StandardCode` = '',
       `StageCode`    = '',
       `UpdateBy`     = 'seed_fill_param_v2',
       `UpdateTime`   = NOW()
 WHERE `OrgCode`      = @cb
   AND `StandardCode` = @std9001
   AND `StageCode`    = @stgReview
   AND `IsDeleted`    = 0;

-- ────────────────────────────────────────────────────────────────────────────
-- ② 作用域示例（让「机构 × 标准 × 阶段」在界面上**看得见**）
--    ②-1 标准专属新增：食品标准多一个「食品安全管理员」
--    ②-2 阶段专属新增：复审阶段多一个「上次审核遗留问题」
--    ②-3 标准覆写通用：食品标准下的「质量目标」用食品版（覆写通用那条）
-- ────────────────────────────────────────────────────────────────────────────
INSERT INTO `cert_fill_param_def`
  (`Code`, `OrgCode`, `StandardCode`, `StageCode`,
   `ParamCode`, `ParamName`, `GroupName`, `ValueType`, `EnumOptions`,
   `SourceKind`, `SourceExpr`, `MaintainMode`, `DefaultValue`, `Placeholder`,
   `IsRequired`, `IsBuiltin`, `SortOrder`, `Description`,
   `CreateBy`, `CreateTime`, `IsDeleted`, `IsValid`)
VALUES
  -- ②-1 标准专属（食品标准）
  (UUID(), @cb, @stdFood, '',
   'food_safety_manager', '食品安全管理员', '标准专属', 'text', NULL,
   'global', NULL, 'manual', NULL, '姓名 / 岗位 / 培训证书编号',
   0, 0, 210, '★ 仅「食品标准」出现 —— 演示「标准专属参数」：切到 9001 标准时此项消失',
   'seed_fill_param_v2', NOW(), 0, 1),

  -- ②-2 阶段专属（复审）
  (UUID(), @cb, '', @stgReview,
   'last_audit_findings', '上次审核遗留问题', '阶段专属', 'text', NULL,
   'global', NULL, 'manual', NULL, '如：上次审核开具的 2 项一般不符合，已整改关闭',
   0, 0, 220, '★ 仅「复审」阶段出现 —— 演示「阶段专属参数」：切到初审/预审时此项消失',
   'seed_fill_param_v2', NOW(), 0, 1),

  -- ②-3 覆写通用（食品标准覆写「质量目标」）
  (UUID(), @cb, @stdFood, '',
   'quality_objective', '质量目标（食品）', '体系信息', 'text', NULL,
   'ai', NULL, 'both', '产品出厂检验合格率 100%，客户投诉率≤1%，重大食品安全事故 0 起',
   '企业自述，可 AI 提炼后人工确认',
   0, 0, 170, '★ 覆写通用「质量目标」—— 演示 Specificity：选食品标准时取本条，其它标准取通用那条',
   'seed_fill_param_v2', NOW(), 0, 1)
ON DUPLICATE KEY UPDATE
  `ParamName`   = VALUES(`ParamName`),
  `GroupName`   = VALUES(`GroupName`),
  `ValueType`   = VALUES(`ValueType`),
  `SourceKind`  = VALUES(`SourceKind`),
  `SourceExpr`  = VALUES(`SourceExpr`),
  `MaintainMode`= VALUES(`MaintainMode`),
  `DefaultValue`= VALUES(`DefaultValue`),
  `Placeholder` = VALUES(`Placeholder`),
  `IsRequired`  = VALUES(`IsRequired`),
  `SortOrder`   = VALUES(`SortOrder`),
  `Description` = VALUES(`Description`),
  `IsDeleted`   = 0,
  `IsValid`     = 1;

-- ────────────────────────────────────────────────────────────────────────────
-- 验证（跑完必须与注释一致）
-- ────────────────────────────────────────────────────────────────────────────
-- ① 通用参数 17 条
-- SELECT COUNT(*) AS universal_cnt FROM cert_fill_param_def
--  WHERE OrgCode=@cb AND StandardCode='' AND StageCode='' AND IsDeleted=0 AND IsValid=1;
-- 期望：17
--
-- ② 作用域示例 3 条
-- SELECT StandardCode, StageCode, ParamCode, ParamName, SortOrder
--   FROM cert_fill_param_def
--  WHERE OrgCode=@cb AND (StandardCode<>'' OR StageCode<>'') AND IsDeleted=0
--  ORDER BY SortOrder;
-- 期望：食品标准 food_safety_manager / '' last_audit_findings(复审) / 食品标准 quality_objective
