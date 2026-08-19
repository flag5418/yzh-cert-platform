-- ============================================================
-- Skill 体系 V1.3 改进：旧列清理
-- 日期：2026-08-18
-- 说明：Skill 类型统一为 method，api 型废弃
--       input_schema/output_schema/endpoint_config 三个旧 JSON 列不再使用
--       wf_skill_api 表保留不删（避免破坏性变更）
-- ============================================================

-- 1. wf_skill 表：清理旧 JSON 列
-- 1a. 将旧列数据置 NULL（可选，释放空间）
UPDATE wf_skill SET input_schema = NULL WHERE input_schema IS NOT NULL;
UPDATE wf_skill SET output_schema = NULL WHERE output_schema IS NOT NULL;
UPDATE wf_skill SET endpoint_config = NULL WHERE endpoint_config IS NOT NULL;

-- 1b. 删除旧列
ALTER TABLE wf_skill DROP COLUMN IF EXISTS input_schema;
ALTER TABLE wf_skill DROP COLUMN IF EXISTS output_schema;
ALTER TABLE wf_skill DROP COLUMN IF EXISTS endpoint_config;

-- 2. wf_skill 表：skill_type 固定为 method
-- 2a. 将所有 api 型记录改为 method
UPDATE wf_skill SET skill_type = 'method' WHERE skill_type = 'api';

-- 2b. 修改列定义：固定 DEFAULT 'method'
ALTER TABLE wf_skill MODIFY COLUMN skill_type VARCHAR(20) NOT NULL DEFAULT 'method';

-- 3. wf_skill_api 表：保留不删，但标注不再使用
-- （不做任何操作，表结构保留）

-- 4. 验证
SELECT 
    COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_SCHEMA = DATABASE() 
    AND TABLE_NAME = 'wf_skill'
    AND COLUMN_NAME IN ('skill_type', 'input_schema', 'output_schema', 'endpoint_config');

-- 预期结果：
-- skill_type | varchar(20) | NO | method
-- input_schema / output_schema / endpoint_config → 不存在（已删除）
