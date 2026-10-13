-- ============================================================
-- 视图：v_org_standard_stage
-- 用途：「机构 → 标准 → 阶段」关联链 + 目录配置 Code（含有效性列）
--       替代 StandardDirectoryService.GetOrganizationTreeAsync 的 6 表 C# 内存组树
-- 引用表：cert_certification_body / cert_org_standard / cert_iso_standard
--         / cert_org_stage / cert_cert_stage / cert_standard_directory_config
-- 算法：MERGE（无 GROUP BY / UNION / DISTINCT / 聚合 ⇒ 谓词可下推到基表）
-- 日期：2026-10-13（阶段 1.B 试点 · V1 新建）
--
-- ★ 有效性规范（2026-10-13 用户裁定：视图不固化业务有效性）
--   · 固化 IsDeleted = 0 —— 软删行 = 逻辑不存在
--   · ⛔ 不固化 IsValid —— 业务启用状态一律以列暴露，由唯一检索入口
--     CoreResourceQuery 统一判（判据只有一处）：
--       OrgIsValid / StandardIsValid / OrgStandardIsValid /
--       StageIsValid / OrgStageIsValid / ConfigIsValid
--   · ⚠️ 六列全为 tinyint(1)；未命中的 LEFT JOIN 侧为 NULL ⇒ 消费方判 `== 1`（⛔ 不判 `!= 0`）
--
-- ★ 结构语义（与 C# 内存组树逐条对齐 —— 实测 2026-10-13）
--   ① 以机构为左表（LEFT JOIN）⇒「无关联标准的机构」仍出现（标准列全 NULL），
--      ⛔ 不用 INNER JOIN 丢掉它；消费方据 StandardCode 是否为 NULL 决定是否建标准节点。
--   ② og.StandardCode 为 NULL = 该阶段适用于本机构全部标准
--      （★ 逐字对齐 C#：只认 NULL，⛔ 不把空串 '' 当通配 —— C# 侧亦不认）
--   ③ ConfigCode 两级兜底：机构行（OrgCode = 本机构）优先，未命中再取平台通用行（OrgCode = ''）
--      ⚠️ 两侧都要求 EnterpriseCode = ''（机构/平台模板行）—— 企业专属目录不参与机构树
--      （★ C# 侧 GroupBy 键不含 EnterpriseCode，理论上会被企业行撞键；
--        当前实测企业行 0 条 ⇒ 行为一致，此处为确定性收口）
--   ④ 行数放大风险：同 (OrgCode,StandardCode,StageCode) 若有多条 _config ⇒ 视图行数放大；
--      当前 uk_org_ent_std_stage 下实测 2 行 key 不同 ⇒ 不放大
--
-- 回滚：DROP VIEW IF EXISTS `v_org_standard_stage`;
-- ============================================================

CREATE OR REPLACE VIEW `v_org_standard_stage` AS
SELECT
    cb.Code        AS OrgCode,
    cb.Name        AS OrgName,
    cb.Sort        AS OrgSort,
    cb.IsValid     AS OrgIsValid,

    s.Code         AS StandardCode,
    s.StandardCode AS StandardNo,
    s.StandardName AS StandardName,
    s.VersionYear  AS VersionYear,
    s.Sort         AS StandardSort,
    s.IsValid      AS StandardIsValid,
    os.IsValid     AS OrgStandardIsValid,

    st.Code        AS StageCode,
    st.StageCode   AS StageNo,
    st.StageName   AS StageName,
    st.Sort        AS StageSort,
    st.IsValid     AS StageIsValid,
    og.IsValid     AS OrgStageIsValid,

    COALESCE(cfg_org.Code, cfg_any.Code) AS ConfigCode,
    CASE WHEN cfg_org.Code IS NOT NULL THEN cfg_org.IsValid ELSE cfg_any.IsValid END AS ConfigIsValid
FROM `cert_certification_body` cb
LEFT JOIN `cert_org_standard` os
       ON os.OrgCode = cb.Code
      AND os.IsDeleted = 0
LEFT JOIN `cert_iso_standard` s
       ON s.Code = os.StandardCode
      AND s.IsDeleted = 0
LEFT JOIN `cert_org_stage` og
       ON og.OrgCode = cb.Code
      AND og.IsDeleted = 0
      AND (og.StandardCode IS NULL OR og.StandardCode = os.StandardCode)
LEFT JOIN `cert_cert_stage` st
       ON st.Code = og.StageCode
      AND st.IsDeleted = 0
LEFT JOIN `cert_standard_directory_config` cfg_org
       ON cfg_org.OrgCode = cb.Code
      AND cfg_org.EnterpriseCode = ''
      AND cfg_org.IsDeleted = 0
      AND cfg_org.StandardCode = s.Code
      AND cfg_org.StageCode = st.Code
LEFT JOIN `cert_standard_directory_config` cfg_any
       ON cfg_any.OrgCode = ''
      AND cfg_any.EnterpriseCode = ''
      AND cfg_any.IsDeleted = 0
      AND cfg_any.StandardCode = s.Code
      AND cfg_any.StageCode = st.Code
WHERE cb.IsDeleted = 0;
