-- ============================================================
-- 2026-11-27 · cert_onsite_plan_item 可空列对齐（幂等）
--
-- 起因（抽样自测暴露）：AI 生成入场检查内容时落库报
--   「新增失败：必填字段为空」。
-- 根因：实际 DB 把 CheckMethod / TargetDept / ContactCode /
--   AuditorCode / AuditorName / SampleRequirement / ExpectedFinding /
--   ClauseCodes 建成了 NOT NULL（无默认值），与两处权威口径冲突：
--     ① 实体 CertPlatform.Auditor/Entities/Expert/CertOnsitePlanItem.cs
--        —— 这 8 列均声明为 string?（可空）
--     ② 建表脚本 scripts/db/fix/20261103_onsite_plan_item_and_prompt_V1.sql
--        —— DDL 同样写 VARCHAR(...) NULL
--   现表由更早版本 CodeFirst 建过，CREATE TABLE IF NOT EXISTS 不会改列，
--   故遗留为 NOT NULL。AI 生成路径未给这 8 列赋值 ⇒ 全部命中 NOT NULL 失败；
--   手工保存路径同样会在用户留空时失败。
--
-- 修法：把这 8 列 ALTER 回 NULL，与实体 / 建表脚本一致。
-- 幂等：MODIFY 为幂等语句，可重复执行。
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

ALTER TABLE `cert_onsite_plan_item`
    MODIFY COLUMN `CheckMethod`       VARCHAR(50)  NULL,
    MODIFY COLUMN `TargetDept`        VARCHAR(200) NULL,
    MODIFY COLUMN `ContactCode`       VARCHAR(50)  NULL,
    MODIFY COLUMN `ContactName`       VARCHAR(100) NULL,
    MODIFY COLUMN `AuditorCode`       VARCHAR(64)  NULL,
    MODIFY COLUMN `AuditorName`       VARCHAR(100) NULL,
    MODIFY COLUMN `SampleRequirement` VARCHAR(200) NULL,
    MODIFY COLUMN `ExpectedFinding`   VARCHAR(500) NULL,
    MODIFY COLUMN `ClauseCodes`       VARCHAR(500) NULL;

-- 验证：下列查询应返回 0 行（即 8 列均已可空）
SELECT COLUMN_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME   = 'cert_onsite_plan_item'
  AND COLUMN_NAME IN ('CheckMethod','TargetDept','ContactCode','ContactName',
                      'AuditorCode','AuditorName','SampleRequirement',
                      'ExpectedFinding','ClauseCodes')
  AND IS_NULLABLE = 'NO';
