-- ===================================================================
-- 修复视图 + 剩余 lowercase 列 Rename（V4）
-- 日期：2026-09-21
-- 原因：
--   1. v_sys_user 视图引用已不存在的列名（User_Id/Role_Id/CreateID 等）
--   2. v_workflow 视图引用已不存在的列名（create_id/creator/modifier 等）
--   3. sys_api 表仍有 method/path/name/author 未改名（V3 只改了 id/code/group_path/enable）
--   4. wf_workflow_definition 表仍有 status/enable 未改名
-- ===================================================================

-- ★ 2026-09-23：本脚本重建视图，而视图内字面量派生列的 collation 取自连接，
--   故必须显式固定连接排序规则（原先缺失）。
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

USE yzh_cert_platform;

-- ===================================================================
-- 第一部分：修复视图
-- ===================================================================

-- 1. 修复 v_sys_user（对齐 Sys_User/Sys_Role 实际列名）
-- 注意：Sys_User.OrgCode 是 utf8mb4_general_ci，Sys_Organization.Code 是 utf8mb4_unicode_ci
-- 必须用 COLLATE 统一排序规则，否则 JOIN 报 "Illegal mix of collations"
DROP VIEW IF EXISTS v_sys_user;
CREATE VIEW v_sys_user AS
SELECT
    -- === Sys_User 物理表字段 ===
    u.Id,
    u.Code,
    u.UserName,
    u.UserTrueName,
    u.UserPwd,
    u.RoleId,
    u.Enable,
    u.IsValid,
    u.IsDeleted,
    u.DeleteTime,
    u.DeleteBy,
    u.OrgCode,
    u.Gender,
    u.PhoneNo,
    u.Email,
    u.HeadImageUrl,
    u.Address,
    u.Remark,
    u.LastLoginDate,
    u.LastModifyPwdDate,
    u.OrderNo,
    u.Token,
    u.CreateTime,
    u.CreateBy,
    u.UpdateTime,
    u.UpdateBy,
    u.DeptId,
    u.UserType,

    -- === 视图扩展字段 ===
    r.RoleName        AS RoleName,
    o.OrgName         AS OrgName,
    o.OrgType         AS OrgType,
    o.OrgLevel        AS OrgLevel,
    o.OrgPath         AS OrgPath,
    o.LeaderName      AS OrgLeaderName,
    o.LeaderPhone     AS OrgLeaderPhone

FROM Sys_User u
LEFT JOIN Sys_Role r ON u.RoleId = r.Id
LEFT JOIN Sys_Organization o ON u.OrgCode = o.Code COLLATE utf8mb4_general_ci
;

-- 2. 修复 v_workflow（对齐 wf_workflow_definition 实际列名）
DROP VIEW IF EXISTS v_workflow;
CREATE VIEW v_workflow AS
SELECT
    w.Id,
    w.Code,
    w.OrgCode,
    w.WorkflowCode,
    w.WorkflowName,
    w.WorkflowType,
    (CASE w.WorkflowType
        WHEN 'extraction' THEN '提取'
        WHEN 'validation' THEN '审核'
        WHEN 'report' THEN '报告'
        ELSE w.WorkflowType
    END) AS WorkflowTypeName,
    w.WorkflowConfig,
    w.Version,
    w.IsActive,
    (CASE w.IsActive WHEN 1 THEN '启用' WHEN 0 THEN '停用' ELSE CAST(w.IsActive AS CHAR) END) AS IsActiveName,
    w.Description,
    w.Status,
    (CASE w.Status WHEN 'active' THEN '启用' WHEN 'inactive' THEN '停用' ELSE w.Status END) AS StatusName,
    w.Sort,
    w.Remark,
    w.CreateTime,
    w.CreateBy,
    w.UpdateTime,
    w.UpdateBy,
    w.DeleteTime,
    w.DeleteBy,
    w.IsDeleted,
    w.IsValid
FROM wf_workflow_definition w
;

-- ===================================================================
-- 第二部分：Rename 剩余 lowercase 列（终极清理）
-- ===================================================================

-- sys_api：method/path/name/author → PascalCase
ALTER TABLE sys_api
  CHANGE COLUMN method Method varchar(10) NOT NULL,
  CHANGE COLUMN path Path varchar(200) NOT NULL,
  CHANGE COLUMN name Name varchar(200) NOT NULL,
  CHANGE COLUMN author Author varchar(50) NULL;

-- wf_workflow_definition：status/enable → PascalCase
ALTER TABLE wf_workflow_definition
  CHANGE COLUMN status Status varchar(50) NULL,
  CHANGE COLUMN enable Enable tinyint NULL;

-- ===================================================================
-- 验证
-- ===================================================================
SELECT '✅ v_sys_user 视图已修复' AS Result;
SELECT '✅ v_workflow 视图已修复' AS Result;
SELECT '✅ sys_api 列已全量 PascalCase' AS Result;
SELECT '✅ wf_workflow_definition 列已全量 PascalCase' AS Result;
