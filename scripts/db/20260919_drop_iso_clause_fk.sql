-- 2026-09-19: 删除 cert_iso_clause 表的多余外键约束
-- 原因：YZH 架构不依赖数据库级外键约束维护引用完整性，由应用层保证。
--       前端传 SelectedNode.Code（GUID）给 StandardCode 时如果传递了不存在的值（空字符串等），
--       会导致 MySQL 报 FK 违反 → HTTP 400 "数据被其他业务引用，无法操作"。
-- 操作：删除两个 FK，保留普通索引（查询性能不受影响）

ALTER TABLE cert_iso_clause DROP FOREIGN KEY IF EXISTS fk_iso_clause_standard;
ALTER TABLE cert_iso_clause DROP FOREIGN KEY IF EXISTS fk_iso_clause_parent;
