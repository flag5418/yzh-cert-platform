-- =============================================================================
-- 脚本名称：20260918_skill_rename_to_pascal_V1b.sql (续)
-- 功能说明：完成 wf_skill 表改造（wf_skill_category 已完成）
-- =============================================================================

-- ============================================
-- 补齐 wf_skill 审计列
-- ============================================
ALTER TABLE wf_skill ADD COLUMN CreateTime DATETIME NULL DEFAULT NULL COMMENT '创建时间';
ALTER TABLE wf_skill ADD COLUMN CreateBy VARCHAR(64) NULL COMMENT '创建人Code';
ALTER TABLE wf_skill ADD COLUMN UpdateTime DATETIME NULL DEFAULT NULL COMMENT '更新时间';
ALTER TABLE wf_skill ADD COLUMN UpdateBy VARCHAR(64) NULL COMMENT '更新人Code';
ALTER TABLE wf_skill ADD COLUMN DeleteTime DATETIME NULL DEFAULT NULL COMMENT '删除时间（软删除）';
ALTER TABLE wf_skill ADD COLUMN DeleteBy VARCHAR(64) NULL COMMENT '删除人Code';

-- 数据迁移
UPDATE wf_skill SET
    CreateTime = create_date,
    CreateBy = COALESCE(create_by, creator),
    UpdateTime = modify_date,
    UpdateBy = COALESCE(update_by, modifier),
    DeleteTime = delete_time,
    DeleteBy = COALESCE(deleter, delete_by);

-- 删除 Vol 风格 snake_case 列
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

-- 恢复外键
ALTER TABLE wf_field_label_mapping MODIFY COLUMN SkillCode VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;
ALTER TABLE wf_field_label_mapping ADD CONSTRAINT fk_flm_skill FOREIGN KEY (SkillCode) REFERENCES wf_skill(Code);

-- 验证
SELECT '=== wf_skill ===' AS info;
DESCRIBE wf_skill;
