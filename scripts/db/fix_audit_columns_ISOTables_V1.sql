-- ====================================================
-- 修复 ISO 相关表的审计字段（BaseEntity 标准）
-- 日期: 2026-09-13
-- 目标: cert_iso_standard, cert_iso_clause 缺少 IsDeleted
-- ====================================================

-- 1. cert_iso_standard: 添加 IsDeleted（实体 ISOStandard 有 new bool IsDeleted 属性）
ALTER TABLE cert_iso_standard
  ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 AFTER DeleteTime;

-- 2. cert_iso_clause: 添加 IsDeleted（实体 ISOClause 有 new bool IsDeleted 属性）
ALTER TABLE cert_iso_clause
  ADD COLUMN IsDeleted tinyint(1) NOT NULL DEFAULT 0 AFTER DeleteTime;
