-- ============================================================
-- 视图：v_sys_user
-- 用途：系统用户列表/详情（T+V 的 V 端）—— 含机构冗余列与角色名
-- 引用表：sys_user / sys_organization / sys_roleuser / sys_role（后两张在标量子查询内）
-- 算法：MERGE（标量子查询随行求值，无 GROUP BY / UNION / DISTINCT / 聚合）
-- 日期：2026-10-13 补录（★ 库中早已存在、scripts/db/views/ 缺档 ⇒ 见规范 §一 偏差 D-1）
--
-- ★ 有效性规范（26-数据库视图设计规范-V1 §三）
--   · 固化 IsDeleted = 0 —— 主表 sys_user + 维表 sys_organization
--   · ⛔ 不固化 IsValid —— 以列暴露（u.IsValid AS IsValid），由消费侧判
--
-- ★ ⚠️ 已知偏差 D-4（登记在案，本 DDL ⛔ 不擅自「修好」）
--   RoleName / OrgName 两列虽在本视图内定义，但实体
--   YZH.Core.Api/Models/Users/Sys_User.cs 用 [SugarColumn(IsIgnore = true)] 声明，
--   而 IsIgnore 会把该列从 SELECT 一并剔除（2026-10-08 在 CertStageView 已踩同一坑）
--   ⇒ 接口响应里根本没有这两个字段（2026-10-13 实测 /api/System/User/filter、/api/User/getCurrentUserInfo）。
--   影响：个人中心 OrgName 恒空（RoleName 被 UserController.GetCurrentUserInfo 的手工回填兜住）。
--   ⛔ 不能简单把 IsIgnore 改成 IsOnlyIgnoreInsert/Update —— 登录链路
--   （YZH.Core.Web/Controllers/AuthController.cs:67 `_db.Client.Queryable<Sys_User>()`）
--   直连**物理表** Sys_User（无 .AS("v_sys_user")）⇒ 解禁后 SELECT 会带上 RoleName/OrgName
--   → Unknown column → 登录直接失败。必须先把直连物理表的查询一并改走视图，再解禁。
--   详见 26-数据库视图设计规范-V1 §一·偏差登记 D-4。
--
-- ★ 补录说明（相对原库定义的差异，仅 2 处，均为「收窄可见行」类）：
--   ① 主表加 `WHERE u.IsDeleted = 0`（原定义无）—— 软删用户 = 逻辑不存在。
--      /filter /list 路径本就追加 IsDeleted = 0（GetPageAsync 无条件加，口径不变）；
--      GetListAsync / CountAsync / ExistsAsync 走 IsDeclaredOnType 判据，
--      而 Sys_User **直接声明** IsDeleted ⇒ 也已过滤。本改动只补住「将来出现裸 SQL 消费方」的缺口。
--   ② sys_organization 关联加 `AND o.IsDeleted = 0` —— 机构被软删时 OrgName 由「仍显示名称」变 NULL。
--   ⚠️ RoleName 标量子查询**保持原样不加过滤**：Sys_RoleUser 表**没有 IsDeleted 列**
--      （2026-10-13 实测），该关联不受软删口径约束；且 RoleName 当前不参与投影（见 D-4），
--      改与不改无线上差异 ⇒ 留待 D-4 修复时与「直连物理表改造」一并决定。
--
-- ★ 排序规则：原库定义含 COLLATE utf8mb4_general_ci 提示；20260923_unify_collation_V1
--   统一后相关列均已是 utf8mb4_general_ci ⇒ 提示已成冗余，本 DDL 去除（语义等价）
--
-- 回滚：DROP VIEW IF EXISTS `v_sys_user`;
-- ============================================================

CREATE OR REPLACE VIEW `v_sys_user` AS
SELECT
    u.Id,
    u.Code,
    u.UserName,
    u.UserTrueName,
    u.UserPwd,
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
    (SELECT GROUP_CONCAT(r.RoleName SEPARATOR ',')
       FROM sys_roleuser ru
       JOIN sys_role r ON ru.RoleCode = r.Code
      WHERE ru.UserCode = u.Code) AS RoleName,
    o.OrgName,
    o.OrgType,
    o.OrgLevel,
    o.OrgPath,
    o.LeaderName  AS OrgLeaderName,
    o.LeaderPhone AS OrgLeaderPhone
FROM sys_user u
LEFT JOIN sys_organization o
       ON u.OrgCode = o.Code
      AND o.IsDeleted = 0
WHERE u.IsDeleted = 0;
