-- =============================================================================
-- 脚本名称：20260918_skill_rename_to_pascal_V1c.sql (续2 - wf_skill 改造 + FK 修复)
-- =============================================================================

-- 0. 先删除外键（wf_field_label_mapping → wf_skill.Code）
ALTER TABLE wf_field_label_mapping DROP FOREIGN KEY fk_flm_skill;

-- 1. 补齐 wf_skill 审计列
ALTER TABLE wf_skill ADD COLUMN CreateTime DATETIME NULL DEFAULT NULL COMMENT '创建时间';
ALTER TABLE wf_skill ADD COLUMN CreateBy VARCHAR(64) NULL COMMENT '创建人Code';
ALTER TABLE wf_skill ADD COLUMN UpdateTime DATETIME NULL DEFAULT NULL COMMENT '更新时间';
ALTER TABLE wf_skill ADD COLUMN UpdateBy VARCHAR(64) NULL COMMENT '更新人Code';
ALTER TABLE wf_skill ADD COLUMN DeleteTime DATETIME NULL DEFAULT NULL COMMENT '删除时间（软删除）';
ALTER TABLE wf_skill ADD COLUMN DeleteBy VARCHAR(64) NULL COMMENT '删除人Code';

-- 2. 数据迁移
UPDATE wf_skill SET
    CreateTime = create_date,
    CreateBy = COALESCE(create_by, creator),
    UpdateTime = modify_date,
    UpdateBy = COALESCE(update_by, modifier),
    DeleteTime = delete_time,
    DeleteBy = COALESCE(deleter, delete_by);

-- 3. 删除 Vol 风格 snake_case 列
ALTER TABLE wf_skill DROP COLUMN creator;
ALTER TABLE wf_skill DROP COLUMN create_date;
ALTER TABLE wf_skill DROP COLUMN modifier;
ALTER TABLE wf_skill DROP COLUMN modify_date;
ALTER TABLE wf_skill DROP COLUMN deleter;
ALTER TABLE wf_skill DROP COLUMN delete_time;
ALTER TABLE wf_skill DROP COLUMN status;
ALTER TABLE wf_skill DROP COLUMN is_valid;
ALTER TABLE wf_skill DROP COLUMN create_by;
ALTER TABLE wf_skill DROP COLUMN update_by;
ALTER TABLE wf_skill DROP COLUMN delete_by;

-- 4. 恢复外键（修改 collation 以兼容）
ALTER TABLE wf_field_label_mapping MODIFY COLUMN SkillCode VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL;
ALTER TABLE wf_field_label_mapping ADD CONSTRAINT fk_flm_skill FOREIGN KEY (SkillCode) REFERENCES wf_skill(Code);

-- 5. 验证
SELECT '=== wf_skill (最终) ===' AS info;
DESCRIBE wf_skill;
SELECT '=== FK 验证 ===' AS info;
SELECT CONSTRAINT_NAME, TABLE_NAME, COLUMN_NAME, REFERENCED_TABLE_NAME, REFERENCED_COLUMN_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE CONSTRAINT_NAME='fk_flm_skill' AND TABLE_SCHEMA='yzh_cert_platform';
