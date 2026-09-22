-- 阶段二：测试入口统一 —— wf_execution_task 增 TestScope 列
--
-- 背景：三种测试入口（test/run 整流 / test/node 单节点 / test/ai-node AI 节点）
--       统一走 WfExecutionTaskService 落库后，需要一列区分来源，
--       否则「任意节点测试」产生的任务在 wf_execution_task 里无法与整流测试区分。
--
-- 命名规范（项目全局规则 §16.9 铁律七）：DB 列名 = C# 属性名 = PascalCase，逐字一致。
--   实体：CertPlatform.Shared/Entities/Wf/WfExecutionTask.cs → TestScope
--
-- 取值：FULL（整流完整测试）| NODE（任意单节点）| AI_NODE（AI 节点）
-- 默认 FULL：存量任务都是整流测试，语义正确。
--
-- 执行：docker exec -i yzh-mysql mysql -uroot -p'Yzh123456.' < 本文件
-- 幂等：MySQL 8.0 不支持 ADD COLUMN IF NOT EXISTS，重复执行会报 1060，
--       可忽略；或先执行下方查询确认列不存在。

USE yzh_cert_platform;

-- 前置检查（返回 0 行才可执行 ALTER）
SELECT COUNT(*) AS ExistingTestScopeColumn
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'wf_execution_task'
  AND COLUMN_NAME = 'TestScope';

ALTER TABLE wf_execution_task
    ADD COLUMN TestScope varchar(20) NOT NULL DEFAULT 'FULL'
        COMMENT '测试范围：FULL（整流）| NODE（单节点）| AI_NODE（AI 节点），仅 TaskType=TEST 时有效'
        AFTER TaskStatus;

-- 支持「按测试范围 + 状态」检索测试历史（阶段四前端测试历史抽屉依赖）
ALTER TABLE wf_execution_task
    ADD KEY idx_test_scope (TaskType, TestScope, TaskStatus);

-- 校验
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLUMN_COMMENT
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = 'yzh_cert_platform'
  AND TABLE_NAME = 'wf_execution_task'
  AND COLUMN_NAME = 'TestScope';
