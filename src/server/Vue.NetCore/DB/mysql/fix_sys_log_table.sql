-- ============================================================
-- 修复 Sys_Log 表结构
-- 问题：Sys_Log 表被 YZH 建表脚本覆盖为自定义审计日志结构
-- 导致 Vol 框架 Logger 批量写入时报错：Unknown column 'LogType' in 'field list'
-- 
-- 解决方案：重建为 Vol 框架期望的原始 Sys_Log 结构
-- 执行时间：2026-08-06
-- ============================================================

-- 1. 删除被错误覆盖的表（表为空，无数据丢失风险）
DROP TABLE IF EXISTS `Sys_Log`;

-- 2. 重建为 Vol 框架标准结构（与 mysql表结构与表数据.sql 一致）
CREATE TABLE `Sys_Log` (
  `Id` int(11) NOT NULL AUTO_INCREMENT,
  `BeginDate` datetime(0) NULL DEFAULT NULL,
  `BrowserType` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `ElapsedTime` int(11) NULL DEFAULT NULL,
  `EndDate` datetime(0) NULL DEFAULT NULL,
  `ExceptionInfo` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `LogType` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `RequestParameter` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `ResponseParameter` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `Role_Id` int(11) NULL DEFAULT NULL,
  `ServiceIP` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `Success` int(11) NULL DEFAULT NULL,
  `Url` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `UserIP` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL,
  `UserName` text CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
  `User_Id` int(11) NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci ROW_FORMAT = Dynamic;
