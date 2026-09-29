-- 06 册 G-3：cert_enterprise_file_op_log / cert_enterprise_file_version 补齐 ISoftDelete 接口列
-- 症状：实体声明 ISoftDelete（DeleteBy/DeleteTime），表缺列 → InsertAsync 报 Unknown column 'DeleteBy'，
--       版本行与操作日志**静默丢失**（Replace 成功但表 0 行）。
ALTER TABLE `cert_enterprise_file_op_log`
  ADD COLUMN `DeleteBy` varchar(64) DEFAULT NULL COMMENT '删除人Code（ISoftDelete）',
  ADD COLUMN `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间（ISoftDelete）';

ALTER TABLE `cert_enterprise_file_version`
  ADD COLUMN `DeleteBy` varchar(64) DEFAULT NULL COMMENT '删除人Code（ISoftDelete）',
  ADD COLUMN `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间（ISoftDelete）';

-- 验证：两表均应含 DeleteBy/DeleteTime
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA='yzh_cert_platform' AND TABLE_NAME IN ('cert_enterprise_file_op_log','cert_enterprise_file_version')
  AND CONVERT(COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_bin IN ('DeleteBy','DeleteTime');
