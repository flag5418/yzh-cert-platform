-- ============================================================================
--  修复：cert_standard_directory_file.SourceProfileCode 列宽不足（36 → 200）
--  日期：2026-10-09
--  依据：`54` §4.3（11 列）/ `55` §4.5 —— 本次为**实测缺陷修复**，非设计变更
-- ============================================================================
--
--  【症状】（真机实测，2026-10-09 由新增端点 batch 的失败明细当场暴露）
--    企业资料规范化执行时，编排器回写实例行报：
--      Data too long for column 'SourceProfileCode' at row 1
--    ⇒ 整个实例行 UPDATE 失败 ⇒ 事务回滚 ⇒ 该文件填充判 failed。
--    实测队列 Q-20261008-3caf2d90e：Total=1 / Failed=1 / RetryCount=3（重试三次都栽在同一处）。
--
--  【根因】
--    列注释自己写明了语义：「★生成依据：产出本次文件所用的画像 → cert_enterprise_doc_profile.Code
--    （**可多值时逗号分隔，最多 3 个**）」
--      ⇒ 3 × 36（单个 Code 长度）+ 2（两个逗号）= **110 字符**
--    而实际列宽只有 varchar(36) —— **只够放 1 个 Code**。
--    单画像场景侥幸通过；一旦一个标准文件命中 ≥2 个画像（多标准场景），必然超长。
--
--  【影响面】
--    · 仅「一个标准文件对应多个画像」的多标准场景；单画像场景不受影响
--      ⇒ 这正是该缺陷能潜伏至今的原因（此前跑的队列都是单画像）。
--    · 后果不是「少写一个字段」，而是**整次填充失败**（事务回滚，产物也出不来）。
--
--  【修法】
--    列宽 36 → 200（覆盖 3 个 Code 的 110，留余量给将来放宽到 5 个）。
--    ★ 同步必改（两处缺一即出问题）：
--        src/certplatform-api/CertPlatform.Shared/Entities/Dir/StandardDirectoryFile.cs
--        [SugarColumn(Length = 36)] → [SugarColumn(Length = 200)]
--      ⚠️ 只改 DB ⇒ 实体标注宽于 DB 时非严格模式**静默截断**（Code 被砍半 ⇒ 溯源指错行）；
--         只改实体 ⇒ 严格模式直接报错。
--
--  【回滚】
--    ALTER TABLE `cert_standard_directory_file`
--      MODIFY COLUMN `SourceProfileCode` varchar(36) NOT NULL DEFAULT ''
--      COMMENT '★生成依据：产出本次文件所用的画像 → cert_enterprise_doc_profile.Code（可多值时逗号分隔，最多 3 个）';
--    ⚠️ 回滚前必须先确认没有 >36 字符的数据，否则会被截断：
--      SELECT COUNT(*) FROM cert_standard_directory_file WHERE CHAR_LENGTH(SourceProfileCode) > 36;
-- ============================================================================

ALTER TABLE `cert_standard_directory_file`
  MODIFY COLUMN `SourceProfileCode` varchar(200) NOT NULL DEFAULT ''
  COMMENT '★生成依据：产出本次文件所用的画像 → cert_enterprise_doc_profile.Code（可多值时逗号分隔，最多 3 个 ⇒ 上限 110 字符）'
  AFTER `LockedTime`;

-- ── 验证（应输出 varchar(200) / 200） ──
SELECT COLUMN_NAME, COLUMN_TYPE, CHARACTER_MAXIMUM_LENGTH
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = 'cert_standard_directory_file'
   AND COLUMN_NAME = 'SourceProfileCode';
