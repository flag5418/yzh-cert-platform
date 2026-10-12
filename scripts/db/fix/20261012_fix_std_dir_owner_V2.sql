-- ════════════════════════════════════════════════════════════════════════════════
--  2026-10-12  修正 `cert_standard_directory_file.OrgCode` 的错误回填（V2）
--
--  ★ 背景：同日的 `20261012_unify_owner_field_V1.sql` 把 23 行回填成了
--    `yzh-std-env`（误判为「后台标准域模板行」）。当日复核后**判定该回填错误**，
--    本脚本按三重证据修正为**认证机构 Code**。
--
--  ★ 三重证据（互相独立，结论一致）：
--    ① `StoragePath` 第 2 段就是归属主体 —— 见 `PathBuilder.cs:140`：
--       `/standard-directory/{OrgCode}/{StandardCode}/{StageCode}/{FolderPath}/{FileName}`
--       实测：`/standard-directory/906e8b2a962c4062b21144af4cc4abc0/dfca5483-.../9777866c-.../...`
--    ② 父行 `cert_standard_directory_config`（唯一 1 行，Code=b27945be5e1b44f28bcf8fc19852d92d）
--       的 `OrgCode` 就是 `906e8b2a962c4062b21144af4cc4abc0` —— 父子必须同归属
--    ③ `cert_certification_body.Code` = `906e8b2a962c4062b21144af4cc4abc0`
--       （Name='河北雄安尚龙认证有限公司', CbCode='CB001'）
--
--  ★ 为什么 23 行不是「后台标准域」：
--    · 文件是 `XASL-QD-xxx 制度文件`（XASL = 雄安尚龙），是**该机构自己的标准目录文件**
--    · `cert_standard_directory_*` 表族的归属主体**就是机构**（决策⑳修订：按机构隔离），
--      ⛔ 不是后台 —— 后台标准级数据落在 `cert_iso_clause` 等表上（`OrgCode='yzh-std-env'` 156 行）
--
--  ★ 幂等：按「旧值 = yzh-std-env 且 EnterpriseCode 为空」精确定位，重复执行无副作用。
--  ★ 回滚：见 §3（快照表 `_bak_20261012_std_dir_file` 由 V1 建立）。
-- ════════════════════════════════════════════════════════════════════════════════

-- ── §1 修正前快照 ────────────────────────────────────────────────────────────────
DROP TABLE IF EXISTS `_bak_20261012_std_dir_file_v2`;
CREATE TABLE `_bak_20261012_std_dir_file_v2` AS
SELECT Code, OrgCode, EnterpriseCode, StandardCode, StageCode, FileName, StoragePath
FROM `cert_standard_directory_file`;

-- ── §2 修正归属 ──────────────────────────────────────────────────────────────────
-- 只动「V1 误填」的那批：OrgCode='yzh-std-env' 且 EnterpriseCode=''（23 行）
UPDATE `cert_standard_directory_file`
   SET OrgCode = '906e8b2a962c4062b21144af4cc4abc0'
 WHERE OrgCode = 'yzh-std-env'
   AND (EnterpriseCode IS NULL OR EnterpriseCode = '');

-- ── §3 核对 ─────────────────────────────────────────────────────────────────────
-- 期望：机构归属 = 23；yzh-std-env 残留 = 0
SELECT '3.1 归属分布' AS step;
SELECT OrgCode, EnterpriseCode, COUNT(*) AS cnt
  FROM `cert_standard_directory_file`
 GROUP BY OrgCode, EnterpriseCode;

SELECT '3.2 yzh-std-env 残留（期望 0）' AS step;
SELECT COUNT(*) AS residual_yzh_std_env
  FROM `cert_standard_directory_file`
 WHERE OrgCode = 'yzh-std-env';

SELECT '3.3 与父 config 归属一致性（期望 0 行不一致）' AS step;
SELECT f.Code, f.OrgCode AS file_org, c.OrgCode AS config_org
  FROM `cert_standard_directory_file` f
  JOIN `cert_standard_directory_config` c ON c.Code = f.ConfigCode
 WHERE f.OrgCode <> c.OrgCode;

-- ── §4 回滚（如需，手工执行） ─────────────────────────────────────────────────────
-- UPDATE `cert_standard_directory_file` f
--   JOIN `_bak_20261012_std_dir_file_v2` b ON b.Code = f.Code
--    SET f.OrgCode = b.OrgCode, f.EnterpriseCode = b.EnterpriseCode;
