-- 2026-11-12 分析结果落库列修正：实体按可空，DB 却是 NOT NULL / 长度不足
-- 症状：分析跑完返回 success，但落库报 Column 'IsCurrent' cannot be null / InputTokenCount cannot be null
-- 影响：每次重跑只改了内存结果，旧分析行未降级、新分析行写不进 → 抽屉里一直看到旧结论
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 1) 历史行必须用 NULL 表示（唯一键依赖 MySQL「多个 NULL 可共存」）
ALTER TABLE cert_check_analysis
    MODIFY COLUMN IsCurrent TINYINT NULL COMMENT '1=当前生效；NULL=历史（⛔ 不是 0）';

-- 2) 分析日志的 token / 耗时允许为空（模型未返回 usage 时不写）
ALTER TABLE cert_analysis
    MODIFY COLUMN InputTokenCount INT NULL,
    MODIFY COLUMN OutputTokenCount INT NULL,
    MODIFY COLUMN DurationMs INT NULL;

-- 3) 产出摘要会超过 255 字符，按文本存
ALTER TABLE cert_analysis
    MODIFY COLUMN ResultSummaryJson MEDIUMTEXT NULL;

-- 验证：三列应为 YES
SELECT table_name, column_name, is_nullable, column_type
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND (
      (table_name = 'cert_check_analysis' AND column_name = 'IsCurrent')
      OR (table_name = 'cert_analysis' AND column_name IN ('InputTokenCount', 'OutputTokenCount', 'DurationMs', 'ResultSummaryJson'))
  )
ORDER BY table_name, column_name;
