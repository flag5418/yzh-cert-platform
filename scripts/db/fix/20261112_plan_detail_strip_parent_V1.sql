-- 2026-11-12 · 清理计划明细里的「无正文父级标题」
-- 背景：4 / 4.4 只有标题没有条款正文，真实审核报告只列 4.1 / 4.4.1 / 4.4.2。
-- 幂等：按检查项 Code 定点更新，可重复执行。
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

UPDATE cert_audit_plan_detail
SET ClauseNumbers = '4.1',
    ClauseRefsJson = '[{"StandardNo":"iso9001","ClauseNumber":"4.1"}]'
WHERE Code = 'c7d4f69b92a0462c940a0dba501df07a' AND IsDeleted = 0;

UPDATE cert_audit_plan_detail
SET ClauseNumbers = '4.2',
    ClauseRefsJson = '[{"StandardNo":"iso9001","ClauseNumber":"4.2"}]'
WHERE Code = '8bb2c77e34094b66aaccb1f8ea0bc75b' AND IsDeleted = 0;

UPDATE cert_audit_plan_detail
SET ClauseNumbers = '4.3',
    ClauseRefsJson = '[{"StandardNo":"iso9001","ClauseNumber":"4.3"}]'
WHERE Code = 'fd7cb0dd13f84af097474656978545c3' AND IsDeleted = 0;

UPDATE cert_audit_plan_detail
SET ClauseNumbers = '4.4.1,4.4.2',
    ClauseRefsJson = '[{"StandardNo":"iso9001","ClauseNumber":"4.4.1"},{"StandardNo":"iso9001","ClauseNumber":"4.4.2"}]'
WHERE Code = 'e88752560c5043459d035878ab04621e' AND IsDeleted = 0;

UPDATE cert_audit_plan_detail
SET ClauseNumbers = '5.1.1,5.1.2',
    ClauseRefsJson = '[{"StandardNo":"iso9001","ClauseNumber":"5.1.1"},{"StandardNo":"iso9001","ClauseNumber":"5.1.2"}]'
WHERE Code = '2e8e54ae82ec4cd0a8b786e7c6e9dedb' AND IsDeleted = 0;

UPDATE cert_audit_plan_detail
SET ClauseNumbers = '5.2.1,5.2.2',
    ClauseRefsJson = '[{"StandardNo":"iso9001","ClauseNumber":"5.2.1"},{"StandardNo":"iso9001","ClauseNumber":"5.2.2"}]'
WHERE Code = '066c08730eff42b1961593039610f466' AND IsDeleted = 0;

UPDATE cert_audit_plan_detail
SET ClauseNumbers = '5.3',
    ClauseRefsJson = '[{"StandardNo":"iso9001","ClauseNumber":"5.3"}]'
WHERE Code = 'ebf1b72eaa5e45d2a69aeefce8c000f7' AND IsDeleted = 0;
