-- ============================================================
-- 2026-10-06 · 企业原始资料「文件级队列」批次 1 配套数据（幂等，可重复执行）
--
-- ① 1-7 队列引擎限流参数（QueueManager 单例**构造时**读 cert_sys_config ⇒ 改完必须重启后端）
--      queue_max_concurrent = 3   （2026-10-06 决策②：并发上限默认 3，原默认 4）
--      queue_timeout_seconds = 480（须 < 租约 queue_lease_minutes 默认 600s，否则租约先过期
--                                   会被另一个 worker 重复认领同一任务）
--    ⇒ 按方案 1-7 修订版走配置、⛔ 不改 QueueHostedService（避免动 yzh-core 框架层）
--
-- ② 1-10 提示词补绑：cert_iso_standard 475da4fe-8f50-4bf7-bf2b-b39869d5ddf7
--      （iso4001-2016「食品标准」）此前**没有任何** doc_group / doc_content 提示词，
--      ResolveActiveAsync 三层回退（标准级 → 平台级）两级都落空 ⇒ 关联该标准的
--      企业阶段（29c1bcc3 / c42582d5 等）AI 分组与作用**全部静默跳过**（问题②根因之一）。
--      模板正文与 iso9001 完全同源 —— 标准上下文由 {{standard_name}} / {{std_doc_catalog}} /
--      {{folder_tree}} 占位符按标准注入，⛔ 不需要为新标准重写正文。
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- ① 队列引擎参数 ----------
INSERT INTO cert_sys_config
    (ConfigKey, Code, ConfigValue, ConfigType, Category, DisplayName, Description,
     Sort, IsReadonly, IsDeleted, IsValid, Status, CreateTime)
VALUES
    ('queue_max_concurrent', 'queue_max_concurrent', '3', 'int', 'queue',
     '队列引擎并发 Worker 数',
     'YZH 队列引擎同时执行的任务数（2026-10-06 文件级队列重构决策②：默认 3）',
     0, 0, 0, 1, 'active', NOW()),
    ('queue_timeout_seconds', 'queue_timeout_seconds', '480', 'int', 'queue',
     '队列单任务执行超时(秒)',
     '超时强制终止并按可重试失败收尾；须小于租约 queue_lease_minutes（默认 600 秒）',
     1, 0, 0, 1, 'active', NOW())
ON DUPLICATE KEY UPDATE
    ConfigValue   = VALUES(ConfigValue),
    Description   = VALUES(Description),
    IsDeleted     = 0,
    IsValid       = 1,
    Status        = 'active';

-- ---------- ② iso4001-2016 分组提示词（克隆自 iso9001 绑定行） ----------
INSERT INTO wf_prompt_template
    (Code, PromptCode, PromptName, PromptType, SkillTarget, StandardCode,
     Template, Description, Version, IsActive, IsValid, IsDeleted, CreateTime, Remark)
SELECT
    REPLACE(UUID(), '-', ''),
    'doc_group_iso4001',
    'ISO 4001 分组提示词（标题/分类）',
    t.PromptType, t.SkillTarget,
    '475da4fe-8f50-4bf7-bf2b-b39869d5ddf7',
    t.Template, t.Description, 1, 1, 1, 0, NOW(),
    '克隆自 doc_group_iso9001（2026-10-06 批次 1-10：iso4001-2016 补绑标准提示词）'
FROM wf_prompt_template t
WHERE t.PromptCode = 'doc_group_iso9001'
ON DUPLICATE KEY UPDATE
    IsValid = 1, IsActive = 1, IsDeleted = 0;

-- ---------- ② iso4001-2016 作用提示词（克隆自 iso9001 绑定行） ----------
INSERT INTO wf_prompt_template
    (Code, PromptCode, PromptName, PromptType, SkillTarget, StandardCode,
     Template, Description, Version, IsActive, IsValid, IsDeleted, CreateTime, Remark)
SELECT
    REPLACE(UUID(), '-', ''),
    'doc_content_iso4001',
    'ISO 4001 作用提示词（文档作用）',
    t.PromptType, t.SkillTarget,
    '475da4fe-8f50-4bf7-bf2b-b39869d5ddf7',
    t.Template, t.Description, 1, 1, 1, 0, NOW(),
    '克隆自 doc_content_iso9001（2026-10-06 批次 1-10：iso4001-2016 补绑标准提示词）'
FROM wf_prompt_template t
WHERE t.PromptCode = 'doc_content_iso9001'
ON DUPLICATE KEY UPDATE
    IsValid = 1, IsActive = 1, IsDeleted = 0;

-- ============================================================
-- 验证 SQL（执行后应各返回预期行）
-- ============================================================
-- 验证 1：队列参数 2 行（值 3 / 480）
-- SELECT ConfigKey, ConfigValue, Category, IsValid, IsDeleted FROM cert_sys_config WHERE ConfigKey LIKE 'queue_%';
-- 验证 2：iso4001 标准应有 doc_group + doc_content 各 1 行（PromptCode *_iso4001）
-- SELECT PromptCode, PromptName, PromptType, StandardCode, IsActive, IsValid
--   FROM wf_prompt_template
--  WHERE StandardCode = '475da4fe-8f50-4bf7-bf2b-b39869d5ddf7' ORDER BY PromptType;
