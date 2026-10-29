-- ============================================================================
-- 20261028_doc_extract_rules_seed_V1.sql
-- cert_doc_extract_rules 种子数据 —— 4 份国标 Markdown 入库
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 yzh_cert_platform < 本文件
--
-- ⚠️ 幂等：INSERT IGNORE（按 uk_standard 唯一键）
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
