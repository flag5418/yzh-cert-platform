-- ============================================================
-- G-1c：标准目录 file 行 StandardCode/StageCode 冗余列存量回填
-- 日期：2026-09-28
-- 背景：StandardDirectoryService 历史创建链（批量 confirm / UploadFileLegacy / 复活分支）
--       只写 ConfigCode 不写 StandardCode/StageCode → 模板 file 行两列为空串。
--       企业槽位懒建复制（tf.StandardCode ?? template.StandardCode）对空串不生效
--       （空串 ≠ null），路径构造与槽位匹配均依赖这两列 → 必须回填。
--       代码侧已同步修复：三处创建/更新点补写 StandardCode/StageCode。
-- 执行：docker exec -i yzh-mysql mysql -uroot -p'***' yzh_cert_platform < 本文件
-- ============================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 回填：ConfigCode 关联 config 取权威 StandardCode/StageCode
UPDATE cert_standard_directory_file f
JOIN cert_standard_directory_config c ON f.ConfigCode = c.Code
SET f.StandardCode = c.StandardCode,
    f.StageCode    = c.StageCode
WHERE IFNULL(f.StandardCode, '') = ''
   OR IFNULL(f.StageCode, '') = '';

-- 验证：应返回 0 行（所有 file 行都有槽位冗余列）
SELECT f.Code, f.ConfigCode, f.FileName
FROM cert_standard_directory_file f
WHERE IFNULL(f.StandardCode, '') = ''
   OR IFNULL(f.StageCode, '') = '';
