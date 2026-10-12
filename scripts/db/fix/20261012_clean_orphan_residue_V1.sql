-- ============================================================================
-- 20261012_clean_orphan_residue_V1.sql
--   目的：清理「孤儿行 + 历史执行残留」，为 ISO 13485 真实数据测试准备干净基线。
--
--   判据来源（本轮实测，全部 IsDeleted=0 精确 COUNT）：
--     · cert_doc_template            全表 0 行（含软删亦 0）⇒ 锚点 100% 孤儿
--     · cert_doc_extraction_rule     0 行 ⇒ 表格/字段定义孤儿
--     · cert_expert_task             0 行 ⇒ 任务日志孤儿
--     · cert_enterprise              0 行 · cert_enterprise_original_file 0 行
--       ⇒ 全部 yzh_queue / wf_execution_* 的业务对象已不存在
--     · cert_standard_directory_config 仅 1 行（b27945be…）
--       ⇒ 22 个上传任务里 20 个指向已删配置
--
--   备份：scripts/db/backup/backup_20261012_full_pre_orphan_clean.sql.gz
--   回滚：见文末 §回滚
-- ============================================================================

-- ── 0. 清理前快照（供比对）────────────────────────────────────────────────
SELECT '清理前' AS phase, '模板锚点' AS obj, COUNT(*) AS rows_cnt FROM cert_doc_template_anchor
UNION ALL SELECT '清理前', '表格定义',        COUNT(*) FROM cert_doc_table_def
UNION ALL SELECT '清理前', '表格字段定义',    COUNT(*) FROM cert_doc_table_field_def
UNION ALL SELECT '清理前', '文档字段定义',    COUNT(*) FROM cert_doc_field_def
UNION ALL SELECT '清理前', '专家任务日志',    COUNT(*) FROM cert_expert_task_log
UNION ALL SELECT '清理前', '上传任务(孤儿)',  COUNT(*) FROM cert_upload_task u
    WHERE u.IsDeleted=0 AND NOT EXISTS (SELECT 1 FROM cert_standard_directory_config c WHERE c.Code=u.ConfigCode)
UNION ALL SELECT '清理前', '队列',            COUNT(*) FROM yzh_queue
UNION ALL SELECT '清理前', '队列任务',        COUNT(*) FROM yzh_queue_task
UNION ALL SELECT '清理前', '队列资源锁',      COUNT(*) FROM yzh_queue_resource_lock
UNION ALL SELECT '清理前', '工作流执行任务',  COUNT(*) FROM wf_execution_task
UNION ALL SELECT '清理前', '工作流执行子项',  COUNT(*) FROM wf_execution_task_item
UNION ALL SELECT '清理前', '工作流节点执行',  COUNT(*) FROM wf_node_execution
UNION ALL SELECT '清理前', '工作流路径执行',  COUNT(*) FROM wf_path_execution;

-- ── 1. 孤儿锚点（模板全表为空）────────────────────────────────────────────
DELETE FROM cert_doc_template_anchor
 WHERE TemplateCode NOT IN (SELECT Code FROM cert_doc_template);

-- ── 2. 孤儿「文档表格/字段定义」（提取规则表为空）──────────────────────────
DELETE FROM cert_doc_table_field_def
 WHERE TableCode NOT IN (SELECT Code FROM cert_doc_table_def);
DELETE FROM cert_doc_table_def
 WHERE RuleCode NOT IN (SELECT Code FROM cert_doc_extraction_rule);
DELETE FROM cert_doc_field_def
 WHERE RuleCode NOT IN (SELECT Code FROM cert_doc_extraction_rule);

-- ── 3. 孤儿专家任务日志（任务表为空）──────────────────────────────────────
DELETE FROM cert_expert_task_log
 WHERE TaskCode NOT IN (SELECT Code FROM cert_expert_task);

-- ── 4. 孤儿上传任务（目录配置已删）────────────────────────────────────────
DELETE FROM cert_upload_task
 WHERE NOT EXISTS (SELECT 1 FROM cert_standard_directory_config c WHERE c.Code=cert_upload_task.ConfigCode);

-- ── 5. 历史队列残留（业务对象已删）────────────────────────────────────────
-- ★ 判据分两步，缺一即漏（首版只按「主表 Status 终态」写，实测漏了 133 + 119 行
--   指向**已不存在队列**的孤儿 ⇒ 第二步按 NOT EXISTS 兜底）。
-- 5.1 主表已删 ⇒ 子表孤儿
DELETE FROM yzh_queue_task
 WHERE NOT EXISTS (SELECT 1 FROM yzh_queue q WHERE q.QueueCode = yzh_queue_task.QueueCode);
DELETE FROM yzh_queue_resource_lock
 WHERE NOT EXISTS (SELECT 1 FROM yzh_queue q WHERE q.QueueCode = yzh_queue_resource_lock.QueueCode);
-- 5.2 主表仍在但已终态（⛔ 不动 running / pending）
DELETE FROM yzh_queue_resource_lock
 WHERE QueueCode IN (SELECT QueueCode FROM yzh_queue WHERE Status IN ('completed','failed','cancelled'));
DELETE FROM yzh_queue_task
 WHERE QueueCode IN (SELECT QueueCode FROM yzh_queue WHERE Status IN ('completed','failed','cancelled'));
DELETE FROM yzh_queue
 WHERE Status IN ('completed','failed','cancelled');

-- ── 6. 工作流历史执行残留（业务对象已删）──────────────────────────────────
DELETE FROM wf_path_execution;
DELETE FROM wf_node_execution;
DELETE FROM wf_execution_task_item;
DELETE FROM wf_execution_task;

-- ── 7. 废弃表 ⛔ 本轮未删（外键挡住，需先裁决）─────────────────────────────
--   `z_deprecated_cert_report_template` 被 `rpt_report_task.fk_rpttask_template` 外键引用
--   ⇒ `DROP TABLE` 报 ERROR 3730。两表均 0 行，不占空间，**保留**。
--   若确要删：先 `ALTER TABLE rpt_report_task DROP FOREIGN KEY fk_rpttask_template;`
--   （rpt_report_task 当前 0 行），再 DROP。⛔ 属结构变更，需单独裁决。

-- ── 8. 清理后核对 ─────────────────────────────────────────────────────────
SELECT '清理后' AS phase, '模板锚点' AS obj, COUNT(*) AS rows_cnt FROM cert_doc_template_anchor
UNION ALL SELECT '清理后', '表格定义',        COUNT(*) FROM cert_doc_table_def
UNION ALL SELECT '清理后', '表格字段定义',    COUNT(*) FROM cert_doc_table_field_def
UNION ALL SELECT '清理后', '文档字段定义',    COUNT(*) FROM cert_doc_field_def
UNION ALL SELECT '清理后', '专家任务日志',    COUNT(*) FROM cert_expert_task_log
UNION ALL SELECT '清理后', '上传任务(全部)',  COUNT(*) FROM cert_upload_task WHERE IsDeleted=0
UNION ALL SELECT '清理后', '队列',            COUNT(*) FROM yzh_queue
UNION ALL SELECT '清理后', '队列任务',        COUNT(*) FROM yzh_queue_task
UNION ALL SELECT '清理后', '队列资源锁',      COUNT(*) FROM yzh_queue_resource_lock
UNION ALL SELECT '清理后', '工作流执行任务',  COUNT(*) FROM wf_execution_task
UNION ALL SELECT '清理后', '工作流执行子项',  COUNT(*) FROM wf_execution_task_item
UNION ALL SELECT '清理后', '工作流节点执行',  COUNT(*) FROM wf_node_execution
UNION ALL SELECT '清理后', '工作流路径执行',  COUNT(*) FROM wf_path_execution;

-- ── 9. 13485 保留基线（这些是真实测试依赖，⛔ 不可删）────────────────────
SELECT '13485 保留' AS phase, '标准' AS obj, COUNT(*) AS rows_cnt FROM cert_iso_standard WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '章节',            COUNT(*) FROM cert_iso_clause WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', 'NC 检查规则',      COUNT(*) FROM cert_validation_rule WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '报告章节',         COUNT(*) FROM cert_report_section WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '目录配置',         COUNT(*) FROM cert_standard_directory_config WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '目录文件',         COUNT(*) FROM cert_standard_directory_file WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '阶段',             COUNT(*) FROM cert_cert_stage WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '机构×标准',        COUNT(*) FROM cert_org_standard WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '机构×阶段',        COUNT(*) FROM cert_org_stage WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '参数定义',         COUNT(*) FROM cert_fill_param_def WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '填写提示词',       COUNT(*) FROM cert_doc_fill_prompt WHERE IsDeleted=0
UNION ALL SELECT '13485 保留', '机构',             COUNT(*) FROM sys_organization WHERE IsDeleted=0;

-- ============================================================================
-- §回滚
--   1) 全库恢复：
--      gunzip -c scripts/db/backup/backup_20261012_full_pre_orphan_clean.sql.gz \
--        | docker exec -i yzh-mysql mysql -uroot -pYzh123456. yzh_cert_platform
--   2) 仅恢复被 DROP 的两张废弃表：从上述备份中提取对应 CREATE TABLE 段。
--   ⚠️ 本次清理**未触碰**任何 13485 依赖表（见 §9）。
-- ============================================================================
