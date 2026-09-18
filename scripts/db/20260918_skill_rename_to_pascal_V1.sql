-- =============================================================================
-- 脚本名称：20260918_skill_rename_to_pascal_V1.sql
-- 功能说明：将 wf_skill_category / wf_skill 的审计字段统一为 PascalCase
--           （符合 YZH 架构命名规范，DB/C#/TS 全部 PascalCase 统一）
--
-- 执行方式：由 AI 自动执行，禁止手动执行
-- 日期：2026-09-18
-- =============================================================================

-- ============================================
-- 一、wf_skill_category 改造
-- ============================================

-- 1.1 添加 ParentCode 列（TreeTable 左树右表必需）
ALTER TABLE wf_skill_category ADD COLUMN ParentCode VARCHAR(64) NULL COMMENT '父节点编码（树结构）' AFTER Code;

-- 1.2 补齐 PascalCase 审计列（YZH 架构标准命名）
ALTER TABLE wf_skill_category ADD COLUMN CreateTime DATETIME NULL DEFAULT NULL COMMENT '创建时间';
ALTER TABLE wf_skill_category ADD COLUMN CreateBy VARCHAR(64) NULL COMMENT '创建人Code';
ALTER TABLE wf_skill_category ADD COLUMN UpdateTime DATETIME NULL DEFAULT NULL COMMENT '更新时间';
ALTER TABLE wf_skill_category ADD COLUMN UpdateBy VARCHAR(64) NULL COMMENT '更新人Code';
ALTER TABLE wf_skill_category ADD COLUMN DeleteTime DATETIME NULL DEFAULT NULL COMMENT '删除时间（软删除）';
ALTER TABLE wf_skill_category ADD COLUMN DeleteBy VARCHAR(64) NULL COMMENT '删除人Code';

-- 1.3 数据迁移：从 snake_case 复制到 PascalCase
UPDATE wf_skill_category SET
    CreateTime = create_date,
    CreateBy = COALESCE(create_by, creator),
    UpdateTime = modify_date,
    UpdateBy = COALESCE(update_by, modifier),
    DeleteTime = delete_time,
    DeleteBy = COALESCE(deleter, delete_by);

-- 1.4 删除 Vol 风格的 snake_case 审计列（YZH 不再使用）
ALTER TABLE wf_skill_category DROP COLUMN creator;
ALTER TABLE wf_skill_category DROP COLUMN create_date;
ALTER TABLE wf_skill_category DROP COLUMN modifier;
ALTER TABLE wf_skill_category DROP COLUMN modify_date;
ALTER TABLE wf_skill_category DROP COLUMN deleter;
ALTER TABLE wf_skill_category DROP COLUMN delete_time;
ALTER TABLE wf_skill_category DROP COLUMN status;
ALTER TABLE wf_skill_category DROP COLUMN enable;
ALTER TABLE wf_skill_category DROP COLUMN is_valid;
ALTER TABLE wf_skill_category DROP COLUMN create_by;
ALTER TABLE wf_skill_category DROP COLUMN update_by;
ALTER TABLE wf_skill_category DROP COLUMN delete_by;

-- ============================================
-- 二、wf_skill 表改造
-- ============================================

-- 2.0 先删除外键（wf_field_label_mapping.SkillCode → wf_skill.Code 存在 collation 不兼容）
ALTER TABLE wf_field_label_mapping DROP FOREIGN KEY fk_flm_skill;

-- 2.1 补齐 PascalCase 审计列
ALTER TABLE wf_skill ADD COLUMN CreateTime DATETIME NULL DEFAULT NULL COMMENT '创建时间';
ALTER TABLE wf_skill ADD COLUMN CreateBy VARCHAR(64) NULL COMMENT '创建人Code';
ALTER TABLE wf_skill ADD COLUMN UpdateTime DATETIME NULL DEFAULT NULL COMMENT '更新时间';
ALTER TABLE wf_skill ADD COLUMN UpdateBy VARCHAR(64) NULL COMMENT '更新人Code';
ALTER TABLE wf_skill ADD COLUMN DeleteTime DATETIME NULL DEFAULT NULL COMMENT '删除时间（软删除）';
ALTER TABLE wf_skill ADD COLUMN DeleteBy VARCHAR(64) NULL COMMENT '删除人Code';

-- 2.2 数据迁移
UPDATE wf_skill SET
    CreateTime = create_date,
    CreateBy = COALESCE(create_by, creator),
    UpdateTime = modify_date,
    UpdateBy = COALESCE(update_by, modifier),
    DeleteTime = delete_time,
    DeleteBy = COALESCE(deleter, delete_by);

-- 2.3 删除 Vol 风格的 snake_case 审计列
ALTER TABLE wf_skill DROP COLUMN creator;
ALTER TABLE wf_skill DROP COLUMN create_date;
ALTER TABLE wf_skill DROP COLUMN modifier;
ALTER TABLE wf_skill DROP COLUMN modify_date;
ALTER TABLE wf_skill DROP COLUMN deleter;
ALTER TABLE wf_skill DROP COLUMN delete_time;
ALTER TABLE wf_skill DROP COLUMN status;
ALTER TABLE wf_skill DROP COLUMN enable;
ALTER TABLE wf_skill DROP COLUMN is_valid;
ALTER TABLE wf_skill DROP COLUMN create_by;
ALTER TABLE wf_skill DROP COLUMN update_by;
ALTER TABLE wf_skill DROP COLUMN delete_by;

-- ============================================
-- 三、恢复外键 + 验证
-- ============================================

-- 3.1 恢复外键（修改 wf_field_label_mapping.SkillCode 的 collation 以匹配 wf_skill.Code）
ALTER TABLE wf_field_label_mapping MODIFY COLUMN SkillCode VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL;
ALTER TABLE wf_field_label_mapping ADD CONSTRAINT fk_flm_skill FOREIGN KEY (SkillCode) REFERENCES wf_skill(Code);

-- 3.2 验证
SELECT '=== wf_skill_category ===' AS info;
DESCRIBE wf_skill_category;
SELECT '=== wf_skill ===' AS info;
DESCRIBE wf_skill;
