-- 修复：ent_extraction_result / ent_table_extraction_result 缺少实体引用的列
USE yzh_cert_platform;

-- 缺少的列（实体有属性，但 DB 不存在对应列）
ALTER TABLE ent_extraction_result
    ADD COLUMN StandardCode varchar(36) NULL AFTER FileCode,
    ADD COLUMN StandardFileCode varchar(200) NULL AFTER StandardCode,
    ADD COLUMN PhaseCode varchar(36) NULL AFTER StandardFileCode;

ALTER TABLE ent_table_extraction_result
    ADD COLUMN StandardCode varchar(36) NULL AFTER FileCode,
    ADD COLUMN StandardFileCode varchar(200) NULL AFTER StandardCode,
    ADD COLUMN PhaseCode varchar(36) NULL AFTER StandardFileCode;
