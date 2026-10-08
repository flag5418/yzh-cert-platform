-- =====================================================================================
-- 20261008_cert_stage_standard_seed_V1.sql
-- 认证阶段主数据按 ISO/IEC 17021-1 标准补齐（★ 数据很少改动，一次成稿）
--   目标表：cert_cert_stage（线上唯一阶段主表；cert_phase_definition 0 行且无 FK 指向，不启用）
--   分类：stage_category 字典 process=流程阶段 / audit=审核阶段 / post=证后阶段
--   原则：只增不改 —— 既有 3 行 jd01 初审 / jd02 预审 / 03 复审（被 cert_org_stage、
--         cert_enterprise_stage、cert_validation_rule、cert_report_section 引用）保持原样；
--         新增行 Status='active'、IsValid=1；uk_stage_code 保证幂等（重复执行不报错）
--   产出物口径来源：docs/20-体系认证/05-业务知识库/认证阶段业务全景与NC报告产出矩阵-V1.md §二
-- =====================================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- process 流程阶段 ----------
INSERT IGNORE INTO cert_cert_stage
  (Code, StageCode, StageName, Category, SortOrder, Description, Status, IsValid, IsDeleted, CreateTime)
VALUES
  ('1bc8e72ed8144a90a4daf85518c1c821','AP','申请受理','process',10,
   '受理企业认证申请（ISO/IEC 17021-1 §8.1）；产出：申请受理记录，不出 NC','active',1,0,NOW()),
  ('134b0d7584ff42d2b195d4e3360c5ec3','CR','合同评审','process',20,
   '合同评审与签约（§8.2）；产出：合同评审记录，不出 NC','active',1,0,NOW()),
  ('1aa7f2670db942ce8b6f1b767cf51dfe','SP','审核方案策划','process',30,
   '审核方案、审核组、人天与范围策划；产出：审核方案/计划，不出 NC','active',1,0,NOW()),
  ('798509c21c054956be697a0bafa172ed','CD','认证决定','process',40,
   '认证决定（§9.5.3）；产出：认证决定记录（推荐/不予推荐），不出 NC','active',1,0,NOW());

-- ---------- audit 审核阶段（出 NC / 报告的审核活动） ----------
INSERT IGNORE INTO cert_cert_stage
  (Code, StageCode, StageName, Category, SortOrder, Description, Status, IsValid, IsDeleted, CreateTime)
VALUES
  ('fc7e10becf4c4b32ae454f1eeccf2eef','S1','第一阶段审核','audit',10,
   '文件与策划审核（§9.3.1.2）；仅出「发现/关注事项」，不构成正式 NC','active',1,0,NOW()),
  ('9777866c836b4a719040c402781bbd9e','S2','第二阶段审核','audit',20,
   '现场审核（§9.3.1.3）；出 NC + 初审审核报告','active',1,0,NOW()),
  ('b30c6bd720904bd3b3a1cc4d37cb5865','SV','监督审核','audit',30,
   '监督审核（§9.6.2）；出 NC + 监督报告；强制覆盖上次 NC 验证/内审/管理评审/投诉','active',1,0,NOW()),
  ('654c6c4014b64593887bf7c9bce8d6a6','RC','再认证审核','audit',40,
   '再认证审核（§9.6.3，S1+S2 合并）；出 NC + 再认证报告','active',1,0,NOW()),
  ('fe71825abb824710869a68841067f2d5','SA','特殊审核','audit',50,
   '触发式现场审核（变更/投诉/事故，§9.3.3）；出 NC + 特殊审核报告','active',1,0,NOW()),
  ('a19db69493a34da8b6dcfec49f258000','TT','转机构审核','audit',60,
   '接收转入企业的审核（按剩余有效期折算）；出转机构审核报告 + 新证书','active',1,0,NOW());

-- ---------- post 证后阶段（证书生命周期处置，不出 NC） ----------
INSERT IGNORE INTO cert_cert_stage
  (Code, StageCode, StageName, Category, SortOrder, Description, Status, IsValid, IsDeleted, CreateTime)
VALUES
  ('0c81891e039649f88f3d7fac57d809aa','CE','颁发证书','post',10,
   '证书签发，3 年有效期（§8.5）；产出：证书，不出 NC、不出审核报告','active',1,0,NOW()),
  ('548611a8ab164ada81eb3ee7eeb318fa','SUS','暂停','post',20,
   '证书暂停（§8.7）；产出：状态变更记录，不出 NC','active',1,0,NOW()),
  ('d0a5b6219ce74361ab56187ccbbaabaa','REV','撤销','post',30,
   '证书撤销（§8.8）；产出：状态变更记录，不出 NC','active',1,0,NOW()),
  ('faf2c962bb2440ebb787428f3b30970c','CAN','注销','post',40,
   '证书注销/到期未续；产出：状态变更记录，不出 NC','active',1,0,NOW());

-- ---------- 验证 SQL ----------
-- ① 分类计数（期望：process=4+3 既有、audit=6、post=4；既有 3 行均 process）
-- SELECT Category, COUNT(*) FROM cert_cert_stage WHERE IsDeleted=0 GROUP BY Category;
-- ② 明细（按分类+排序）
-- SELECT Category, SortOrder, StageCode, StageName, Status, IsValid FROM cert_cert_stage
--  WHERE IsDeleted=0 ORDER BY Category, SortOrder, StageCode;
-- ③ 幂等复查：重跑本脚本后总数应不变（17 行）
-- SELECT COUNT(*) FROM cert_cert_stage WHERE IsDeleted=0;
