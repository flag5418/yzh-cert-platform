-- ============================================================
-- 视图：v_iso_standard
-- 用途：ISO 标准列表（T+V 的 V 端）—— 含机构简称 CbName 与字典翻译列 CategoryName / StatusName
-- 引用表：cert_iso_standard / cert_certification_body / sys_dictionary / sys_dictionarylist
-- 算法：MERGE（无 GROUP BY / UNION / DISTINCT / 聚合 ⇒ 谓词可下推到基表）
-- 日期：2026-10-13 补录（★ 库中早已存在、scripts/db/views/ 缺档 ⇒ 见规范 §一 偏差 D-1）
--
-- ★ 有效性规范（26-数据库视图设计规范-V1 §三）
--   · 固化 IsDeleted = 0 —— 主表 + 3 张关联表全部（软删行 = 逻辑不存在）
--   · ⛔ 不固化 IsValid —— 以列暴露（s.IsValid AS IsValid），由消费侧判
--
-- ★ 消费现状（2026-10-13 实测）：**本视图当前无任何代码消费方**
--   V 实体 CertPlatform.Shared/Entities/Cert/ISOStandardView.cs 仅自身声明、零引用
--   （其类注释「当前未创建」已过期 —— 视图确实存在，见本文件）
--   ⇒ 补录 DDL 属「补档 + 按规范加固」，无线上行为变更
--
-- ★ 补录时的加固说明（相对原库定义的行为差异，仅一处）：
--   原定义 `LEFT JOIN cert_certification_body cb ON s.CbCode = cb.Code` **未**过滤 cb.IsDeleted。
--   本 DDL 加 `AND cb.IsDeleted = 0` ⇒ 机构被软删时 CbName 由「仍显示名称」变 NULL。
--   判定：符合规范 §三「软删行 = 逻辑不存在」，且本视图零消费方 ⇒ 无回归风险。
--   （⛔ 若将来需要「显示已删机构名」，应改用 `cb.Name` 直取 + 另加 IsValid 列，而不是回退本过滤）
--
-- ★ 排序规则：原库定义含 COLLATE utf8mb4_general_ci 提示；20260923_unify_collation_V1
--   统一后相关列均已是 utf8mb4_general_ci ⇒ 提示已成冗余，本 DDL 去除（语义等价）
--
-- 回滚：DROP VIEW IF EXISTS `v_iso_standard`;
-- ============================================================

CREATE OR REPLACE VIEW `v_iso_standard` AS
SELECT
    s.Id,
    s.Code,
    s.OrgCode,
    s.CbCode,
    cb.ShortName AS CbName,
    s.StandardCode,
    s.StandardName,
    s.VersionYear,
    s.Category,
    cat.DicName AS CategoryName,
    s.Description,
    s.Status,
    CASE s.Status
        WHEN 'active'   THEN '启用'
        WHEN 'inactive' THEN '停用'
        ELSE s.Status
    END AS StatusName,
    s.CreateBy,
    s.CreateTime,
    s.UpdateBy,
    s.UpdateTime,
    s.DeleteBy,
    s.DeleteTime,
    s.IsValid,
    s.Sort,
    s.Remark,
    s.ParentCode,
    s.IsLeaf
FROM cert_iso_standard s
LEFT JOIN cert_certification_body cb
       ON s.CbCode = cb.Code
      AND cb.IsDeleted = 0
LEFT JOIN sys_dictionary d
       ON d.DicNo = 'iso_category'
      AND d.IsDeleted = 0
LEFT JOIN sys_dictionarylist cat
       ON cat.DicCode = d.Code
      AND cat.DicValue = s.Category
      AND cat.IsDeleted = 0
WHERE s.IsDeleted = 0;
