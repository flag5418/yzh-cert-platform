-- 阶段三：路径级持久化 —— 新建 wf_path_execution 表
--
-- 背景：四层执行模型补全第三层。
--   wf_execution_task          一次触发
--     └─ wf_execution_task_item   一个检查项
--          └─ wf_path_execution      一条路径   ← 本表（新增）
--               └─ wf_node_execution     一个节点
--
-- 存在意义：wf_node_execution 唯一键是 (TaskCode, ItemCode, NodeId)，
--   同一节点被多条路径共享时只能落一行，去重后「本路径是复用还是真跑」的信息被抹平
--   （DB 里 IsReused 恒为 0）。本表以 (TaskCode, ItemCode, PathIndex) 为唯一键，
--   把每条路径的节点序列、失败点、最终输出、耗时原样保留。
--
-- 命名规范（项目全局规则 §16.9 铁律七）：DB 列名 = C# 属性名 = PascalCase，逐字一致。
--   实体：CertPlatform.Shared/Entities/Wf/WfPathExecution.cs
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -p'Yzh123456.' < 本文件
-- 幂等：CREATE TABLE IF NOT EXISTS，可重复执行。

USE yzh_cert_platform;

CREATE TABLE IF NOT EXISTS `wf_path_execution` (
  `Id`             bigint        NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code`           varchar(36)   NOT NULL COMMENT '全局唯一编码（GUID）',
  `TaskCode`       varchar(36)   NOT NULL COMMENT 'wf_execution_task.Code',
  `ItemCode`       varchar(36)   NOT NULL COMMENT 'wf_execution_task_item.Code',
  `PathIndex`      int           NOT NULL COMMENT '路径索引（从0开始）',
  `Status`         varchar(20)   NOT NULL COMMENT 'pending|executing|completed|failed',
  `NodeIds`        json          NULL     COMMENT '路径节点ID列表（按执行顺序）',
  `ReusedCount`    int           NOT NULL DEFAULT 0 COMMENT '本路径复用的节点数（未真跑，取自跨路径结果池）',
  `FailedAtNodeId` varchar(64)   NULL     COMMENT '失败节点ID',
  `ErrorMessage`   varchar(2000) NULL     COMMENT '失败原因',
  `OutputJson`     json          NULL     COMMENT '路径最终输出（end 节点输出）',
  `DurationMs`     int           NULL     COMMENT '路径耗时(ms)',
  `StartedAt`      datetime      NULL     COMMENT '路径开始时间',
  `CompletedAt`    datetime      NULL     COMMENT '路径完成时间',
  `CreateBy`       varchar(50)   NULL     COMMENT '创建人Code',
  `CreateTime`     datetime      NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `UpdateBy`       varchar(50)   NULL     COMMENT '更新人Code',
  `UpdateTime`     datetime      NULL     COMMENT '更新时间',
  `IsDeleted`      tinyint(1)    NOT NULL DEFAULT 0 COMMENT '软删除标记',
  `DeleteBy`       varchar(50)   NULL     COMMENT '删除人Code',
  `DeleteTime`     datetime      NULL     COMMENT '删除时间',
  `IsValid`        int           NOT NULL DEFAULT 1 COMMENT '有效性',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_task_item_path` (`TaskCode`, `ItemCode`, `PathIndex`),
  KEY `idx_task_code` (`TaskCode`),
  KEY `idx_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='工作流路径执行记录';

-- ⚠️ COLLATE 必须显式写 utf8mb4_general_ci：
--   本表要与 wf_execution_task / wf_execution_task_item 按 TaskCode / ItemCode 关联，
--   而这三张执行模型表都是 utf8mb4_general_ci。若不写 COLLATE，MySQL 8 会按
--   库/服务器默认给出 utf8mb4_0900_ai_ci，跨表 JOIN 直接报：
--     ERROR 1267 (HY000): Illegal mix of collations (utf8mb4_0900_ai_ci,IMPLICIT)
--                          and (utf8mb4_general_ci,IMPLICIT) for operation '='
--   注：库内排序规则历史不一致（wf_skill* 为 utf8mb4_unicode_ci，
--   wf_prompt_template 为 utf8mb4_0900_ai_ci），本表从属于执行模型族，跟随 general_ci。

-- ────────────────────────────────────────────────────────────────
-- 旧表处置（计划 §4.2 / G9）
-- ────────────────────────────────────────────────────────────────
-- [DEPRECATED] wf_workflow_execution_log
--   已被 wf_execution_task / _item / wf_path_execution / wf_node_execution
--   四层模型取代，禁止新代码读写。
--   弃用理由：字段名 creator/CreateDate/status/enable 为 Vol 历史小写风格，
--             违反铁律七；业务模型已由四层模型覆盖。
--   现状：0 行，无写入方。
--   物理删除需用户确认，本脚本不动它。
--   如需归档观察，可手工执行：
--     RENAME TABLE wf_workflow_execution_log TO zz_archived_wf_workflow_execution_log_20260922;
-- 同样弃用：wf_workflow_definition（Vol 定义表，0 行，已被 cert_validation_rule.workflow_config 取代）

-- ────────────────────────────────────────────────────────────────
-- 校验
-- ────────────────────────────────────────────────────────────────
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_KEY, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'wf_path_execution'
ORDER BY ORDINAL_POSITION;

SELECT INDEX_NAME, GROUP_CONCAT(COLUMN_NAME ORDER BY SEQ_IN_INDEX) AS ColumnsInIndex, NON_UNIQUE
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'wf_path_execution'
GROUP BY INDEX_NAME, NON_UNIQUE;
