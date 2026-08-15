-- ============================================================
-- 上传任务追踪表（cert_upload_task）
-- 用于批量上传任务的状态管理，支持回滚和过期清理
-- 
-- 创建时间: 2026-08-15
-- 来源: Entity/CertPlatform/Dir/UploadTask.cs
-- 依赖: 无（独立表）
-- ============================================================

CREATE TABLE IF NOT EXISTS `cert_upload_task` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `TaskId` VARCHAR(64) NOT NULL COMMENT '任务唯一ID（UUID）',
  `DirectoryCode` VARCHAR(128) NOT NULL COMMENT '目标目录编码',
  `TotalFiles` INT NOT NULL DEFAULT 0 COMMENT '总文件数',
  `TotalSize` BIGINT NOT NULL DEFAULT 0 COMMENT '总文件大小（字节）',
  `SuccessCount` INT NOT NULL DEFAULT 0 COMMENT '已成功上传数',
  `Status` VARCHAR(20) NOT NULL DEFAULT 'initialized' COMMENT '任务状态: initialized/uploading/completed/cancelled/expired',
  `Creator` VARCHAR(64) DEFAULT NULL COMMENT '创建人',
  `CreateDate` DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
  `ModifyDate` DATETIME DEFAULT NULL COMMENT '修改时间',
  `ExpireTime` DATETIME DEFAULT NULL COMMENT '过期时间（用于自动清理）',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UK_TaskId` (`TaskId`),
  KEY `IX_DirectoryCode` (`DirectoryCode`),
  KEY `IX_Status` (`Status`),
  KEY `IX_ExpireTime` (`ExpireTime`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='上传任务追踪表';

-- 索引说明：
-- UK_TaskId: 任务ID唯一索引，用于快速查找任务
-- IX_DirectoryCode: 目录编码索引，用于按目录查询任务列表
-- IX_Status: 状态索引，用于查询特定状态的任务（如 initialized）
-- IX_ExpireTime: 过期时间索引，用于定时清理过期任务

-- 使用场景：
-- 1. 批量文件上传前创建任务记录（Status=initialized）
-- 2. 每个文件上传成功后更新 SuccessCount++
-- 3. 所有文件上传完成后更新 Status=completed
-- 4. 用户取消或超时后 Status=cancelled/expired
-- 5. 定时任务清理 ExpireTime < NOW() 的记录
