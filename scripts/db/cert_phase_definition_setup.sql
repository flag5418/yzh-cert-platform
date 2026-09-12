-- ============================================================
--  认证阶段定义 — 数据库准备
--  文件：cert_phase_definition_setup.sql
--  日期：2026-09-12
--  说明：
--    1. 删除旧表 cert_phase_definition（如有），重建为新结构
--    2. 创建视图 v_cert_phase_definition（含状态翻译）
--    3. 插入初始五阶段数据
--    4. 清理旧表 cert_cert_stage（如存在）
--  注意：本脚本为幂等设计，可重复执行
-- ============================================================

-- 0. 清理旧视图（如有）
DROP VIEW IF EXISTS v_cert_cert_stage;
DROP VIEW IF EXISTS v_cert_phase_definition;

-- 1. 清理旧表 cert_cert_stage（如有）
-- 注意：如 cert_cert_stage 有历史数据，请先手动迁移后再执行 DROP
DROP TABLE IF EXISTS cert_cert_stage;

-- 2. 重建 cert_phase_definition 表（snake_case + 审计字段）
-- 先 DROP 再 CREATE，确保表结构符合新规范
DROP TABLE IF EXISTS cert_phase_definition;

CREATE TABLE cert_phase_definition (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY COMMENT '自增主键',
    code            varchar(36)  NOT NULL UNIQUE COMMENT '业务编码(Guid)，内部关联键',
    phase_code      varchar(20)  NOT NULL UNIQUE COMMENT '阶段编码(S1/S2/Surv1/Surv2/Recert)，业务标识',
    phase_name      varchar(100) NOT NULL COMMENT '中文名称',
    sequence_order  int          NOT NULL DEFAULT 0 COMMENT '顺序(1=S1, 2=S2, 3=一监, 4=二监, 5=再认证)',
    description     text         COMMENT '阶段说明',
    is_valid        int          NOT NULL DEFAULT 1 COMMENT '有效标志(1=启用, 0=停用)',
    create_time     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    create_by       varchar(50)  DEFAULT NULL COMMENT '创建人编码',
    update_time     datetime     DEFAULT NULL COMMENT '更新时间',
    update_by       varchar(50)  DEFAULT NULL COMMENT '更新人编码',
    delete_time     datetime     DEFAULT NULL COMMENT '删除时间',
    delete_by       varchar(50)  DEFAULT NULL COMMENT '删除人编码',
    is_deleted      tinyint      NOT NULL DEFAULT 0 COMMENT '软删除标志',
    UNIQUE KEY uk_code (code),
    UNIQUE KEY uk_phase_code (phase_code),
    KEY idx_is_valid (is_valid),
    KEY idx_is_deleted (is_deleted)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='认证阶段定义（通用五阶段，ISO 17021）';

-- 3. 插入初始数据（幂等）
INSERT IGNORE INTO cert_phase_definition (code, phase_code, phase_name, sequence_order, description) VALUES
(REPLACE(UUID(), '-', ''), 'S1',   '一阶段审核',   1, '初次认证一阶段审核（文件评审）'),
(REPLACE(UUID(), '-', ''), 'S2',   '二阶段审核',   2, '初次认证二阶段审核（现场审核）'),
(REPLACE(UUID(), '-', ''), 'Surv1','监督审核一',   3, '监督审核第一次'),
(REPLACE(UUID(), '-', ''), 'Surv2','监督审核二',   4, '监督审核第二次'),
(REPLACE(UUID(), '-', ''), 'Recert','再认证',      5, '证书到期再认证');

-- 4. 创建视图（含状态翻译）
CREATE OR REPLACE VIEW v_cert_phase_definition AS
SELECT
    p.id,
    p.code,
    p.phase_code,
    p.phase_name,
    p.sequence_order,
    p.description,
    p.is_valid,
    p.create_time,
    p.create_by,
    p.update_time,
    p.update_by,
    p.delete_time,
    p.delete_by,
    p.is_deleted,
    CASE p.is_valid WHEN 1 THEN '启用' ELSE '停用' END AS status_name
FROM cert_phase_definition p
WHERE p.is_deleted = 0;

-- 6. 验证
SELECT '认证阶段定义数据：' AS info;
SELECT phase_code, phase_name, sequence_order, is_valid, status_name
FROM v_cert_phase_definition
ORDER BY sequence_order;
