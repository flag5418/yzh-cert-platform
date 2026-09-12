-- ============================================================
-- 数据字典模块 · 数据库结构完善
-- 目标：使 Sys_Dictionary / Sys_DictionaryList 可套用 TreeTableControllerBase（左树右表），
--       按项目「双键设计」分离 Code（随机稳定标识）与 DicNo（可选业务属性），
--       并清除全部 Id 型关联字段。
-- 日期：2026-09-12 | 版本：V4
-- 依据：docs/50-任务/数据字典管理-开发计划-V1.md §三 / §四
--       项目全局规则.md §11.5（脚本必须支持重复执行）· §15.2（AI 自动执行）
--
-- 【两条架构铁律】
--   ① 关联一律走 Code：定位记录的核心字段是 Code，禁止 ParentId / Dic_ID / CreateID / ModifyID
--      等一切以 Id 建立关联的字段。项目 ~60 条业务外键已全部指向 Code，本脚本使字典两表对齐。
--   ② Code 与业务编码分离：Code 是随机生成、全局唯一、永不重复的稳定标识（也是唯一关联键）；
--      字典编码 DicNo 只是普通属性，可为空、仅用于显示，不参与任何关联。
--
-- 幂等性：本脚本可重复执行（列/索引增删均先探测存在性；DELETE/UPDATE 天然幂等）
-- 预计结果：主表 57 → 50 行；明细 161 → 128 行
-- ============================================================


-- ============================================================
-- 第零步：幂等辅助过程（脚本末尾统一清理）
-- ============================================================
-- 说明：库中已存在 safe_add_column / p_safe_drop_column 等过程，但它们
--       ① 未纳入仓库脚本（不可复现）② 不支持 AFTER 定位与 UNIQUE 索引，
--       故本脚本自带一套自包含过程，使用 __yzh_dict_ 前缀避免命名冲突。
--       另：MySQL 的 PREPARE 只接受字符串字面量或用户变量，不接受例程参数，
--       因此 __yzh_dict_exec_if_col 中须先 SET 到用户变量再 PREPARE。
-- ============================================================

DROP PROCEDURE IF EXISTS __yzh_dict_add_col;
DROP PROCEDURE IF EXISTS __yzh_dict_drop_col;
DROP PROCEDURE IF EXISTS __yzh_dict_add_uniq;
DROP PROCEDURE IF EXISTS __yzh_dict_add_idx;
DROP PROCEDURE IF EXISTS __yzh_dict_exec_if_col;
-- 清理历史遗留（2026-09-12 首次尝试时泄漏）
DROP PROCEDURE IF EXISTS __yzh_add_col;
DROP PROCEDURE IF EXISTS __yzh_drop_col;
DROP PROCEDURE IF EXISTS __yzh_add_idx;
DROP PROCEDURE IF EXISTS __yzh_exec_if_col;

DELIMITER $$
CREATE PROCEDURE __yzh_dict_add_col(IN p_table VARCHAR(64), IN p_col VARCHAR(64), IN p_ddl TEXT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.COLUMNS
                 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND COLUMN_NAME = p_col) THEN
    SET @ddl = CONCAT('ALTER TABLE `', p_table, '` ADD COLUMN ', p_ddl);
    PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

CREATE PROCEDURE __yzh_dict_drop_col(IN p_table VARCHAR(64), IN p_col VARCHAR(64))
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND COLUMN_NAME = p_col) THEN
    SET @ddl = CONCAT('ALTER TABLE `', p_table, '` DROP COLUMN `', p_col, '`');
    PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

CREATE PROCEDURE __yzh_dict_add_uniq(IN p_table VARCHAR(64), IN p_idx VARCHAR(64), IN p_cols TEXT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND INDEX_NAME = p_idx) THEN
    SET @ddl = CONCAT('ALTER TABLE `', p_table, '` ADD UNIQUE INDEX `', p_idx, '` (', p_cols, ')');
    PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

CREATE PROCEDURE __yzh_dict_add_idx(IN p_table VARCHAR(64), IN p_idx VARCHAR(64), IN p_cols TEXT)
BEGIN
  IF NOT EXISTS (SELECT 1 FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND INDEX_NAME = p_idx) THEN
    SET @ddl = CONCAT('ALTER TABLE `', p_table, '` ADD INDEX `', p_idx, '` (', p_cols, ')');
    PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$

CREATE PROCEDURE __yzh_dict_exec_if_col(IN p_table VARCHAR(64), IN p_col VARCHAR(64), IN p_sql TEXT)
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.COLUMNS
             WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = p_table AND COLUMN_NAME = p_col) THEN
    SET @sql2 = p_sql;
    PREPARE stmt FROM @sql2; EXECUTE stmt; DEALLOCATE PREPARE stmt;
  END IF;
END$$
DELIMITER ;


-- ============================================================
-- 第一步：清理重复的「认证平台字典」分支
-- ============================================================
-- 背景：存在两个同名根节点 cert_dict（认证平台字典）
--         Dic_ID=107（2026-07-31 旧） / Dic_ID=137（2026-09-04 新）
--       导致 7 组 DicNo 重复（含根节点本身）：
--         cert_dict 107,137 · cert_status 110,140 · audit_conclusion 111,141
--         nc_severity 112,142 · application_status 114,144 · task_status 115,145 · org_status 116,146
-- 策略：保留较新的 137 分支；107 独有的 7 个子字典改挂 137；其余删除。
-- 依赖 ParentId / Dic_ID 两列，故用 __yzh_dict_exec_if_col 包裹以支持重复执行。
-- ============================================================

-- 1.1 将 107 分支下 7 个「独有」子字典改挂到 137 分支
--     129 standard_status / 131 stage_category / 132 stage_status / 133 iso_category
--     134 doc_skill / 135 rule_status / 136 compare_operator
CALL __yzh_dict_exec_if_col('Sys_Dictionary', 'ParentId',
  'UPDATE Sys_Dictionary SET ParentId = 137 WHERE Dic_ID IN (129, 131, 132, 133, 134, 135, 136)');

-- 1.2 删除 107 分支下 6 组「重复」子字典的明细（内容在 137 分支已各有一份，共 33 行）
CALL __yzh_dict_exec_if_col('Sys_DictionaryList', 'Dic_ID',
  'DELETE FROM Sys_DictionaryList WHERE Dic_ID IN (110, 111, 112, 114, 115, 116)');

-- 1.3 删除 6 组重复子字典
DELETE FROM Sys_Dictionary WHERE Dic_ID IN (110, 111, 112, 114, 115, 116);

-- 1.4 删除 107 根节点（其明细数为 0，无需清理明细）
DELETE FROM Sys_Dictionary WHERE Dic_ID = 107;


-- ============================================================
-- 第二步：Sys_Dictionary（主表 / 左树）—— 新增框架必需的 6 列
-- ============================================================
-- Code       —— 记录定位键 + 关联键。框架全部端点按 Code 操作：
--                 tree/delete 按 Code 解析记录；tree/action/* 只注入 Code；
--                 tree/toggle-valid 按 Code 反查；EntityService.Update 的 WHERE 条件就是 Code。
--                 ⚠️ UpdateAsync 会 IgnoreColumns("Code")，即 Code 插入后不可修改 → 必须在插入前定值。
--                 ⚠️ Code 为随机唯一值（对齐项目既有 Code 值域：32 位无连字符 GUID）。
--                    框架 TreeTableControllerBase.cs:146-147 已自动填充，控制器无需写生成逻辑。
-- ParentCode —— 树父子关系（唯一关联方式，取代原 ParentId）。GetViewList() 硬编码属性名 ParentCode。
-- IsValid    —— 启用标识。tree/toggle-valid 反射取 typeof(T).GetProperty("IsValid")。
-- IsDeleted / DeleteTime / DeleteBy —— 软删除三件套。SoftDelete() 会同时更新这三列。
-- ============================================================

CALL __yzh_dict_add_col('Sys_Dictionary', 'Code',
  '`Code` varchar(50) NULL COMMENT ''稳定标识（随机唯一，关联键）'' AFTER `Dic_ID`');
CALL __yzh_dict_add_col('Sys_Dictionary', 'ParentCode',
  '`ParentCode` varchar(64) NULL COMMENT ''父节点 Code（根节点为 NULL）'' AFTER `ParentId`');
CALL __yzh_dict_add_col('Sys_Dictionary', 'IsValid',
  '`IsValid` tinyint NOT NULL DEFAULT 1 COMMENT ''有效标志（1=有效，0=无效）'' AFTER `Enable`');
CALL __yzh_dict_add_col('Sys_Dictionary', 'IsDeleted',
  '`IsDeleted` tinyint NOT NULL DEFAULT 0 COMMENT ''删除标志（1=已删除）'' AFTER `IsValid`');
CALL __yzh_dict_add_col('Sys_Dictionary', 'DeleteTime',
  '`DeleteTime` datetime NULL COMMENT ''删除时间'' AFTER `IsDeleted`');
CALL __yzh_dict_add_col('Sys_Dictionary', 'DeleteBy',
  '`DeleteBy` varchar(50) NULL COMMENT ''删除人'' AFTER `DeleteTime`');

-- 2.1 回填 Code = 随机唯一值（REPLACE(UUID(),'-','') 逐行求值，天然不重复）
UPDATE Sys_Dictionary SET Code = REPLACE(UUID(), '-', '') WHERE Code IS NULL OR Code = '';

-- 2.2 回填 ParentCode = 父节点的 Code；根节点（ParentId=0）保持 NULL
CALL __yzh_dict_exec_if_col('Sys_Dictionary', 'ParentId',
  'UPDATE Sys_Dictionary child INNER JOIN Sys_Dictionary parent ON child.ParentId = parent.Dic_ID
   SET child.ParentCode = parent.Code WHERE child.ParentId > 0');

-- 2.3 回填 IsValid：沿用历史 Enable 语义（本表 Enable 全为 1）
UPDATE Sys_Dictionary SET IsValid = Enable;

-- 2.4 Code 升为 NOT NULL（关联键不允许为空）
ALTER TABLE Sys_Dictionary MODIFY COLUMN `Code` varchar(50) NOT NULL COMMENT '稳定标识（随机唯一，关联键）';

-- 2.5 DicNo 降为可空 —— 字典编码只是普通属性，不是必填项
ALTER TABLE Sys_Dictionary MODIFY COLUMN `DicNo` varchar(100) NULL COMMENT '字典编码（可选，仅用于显示）';

-- 2.6 索引
CALL __yzh_dict_add_uniq('Sys_Dictionary', 'uk_sys_dictionary_code',   '`Code`');
CALL __yzh_dict_add_uniq('Sys_Dictionary', 'uk_sys_dictionary_dicno',  '`DicNo`');
CALL __yzh_dict_add_idx ('Sys_Dictionary', 'idx_sys_dictionary_pcode', '`ParentCode`');


-- ============================================================
-- 第三步：Sys_Dictionary（主表）—— 删除全部 Id 型关联字段
-- ============================================================
-- ParentId  ：id 型树关联，已被 ParentCode 取代；且 NOT NULL 无默认值，
--             在 STRICT_TRANS_TABLES 下会阻塞新增，必须删除。
-- CreateID  ：id 型审计字段，与 Creator（存人名/账号）语义重复；57 行中 10 行为 NULL。
-- ModifyID  ：同上，57 行中 34 行为 NULL。
-- 安全性：两表无外键约束；无其他表引用 Sys_Dictionary.Dic_ID（已全库核查）。
-- ============================================================

CALL __yzh_dict_drop_col('Sys_Dictionary', 'ParentId');
CALL __yzh_dict_drop_col('Sys_Dictionary', 'CreateID');
CALL __yzh_dict_drop_col('Sys_Dictionary', 'ModifyID');


-- ============================================================
-- 第四步：Sys_DictionaryList（明细表 / 右表）—— 新增框架必需的 6 列
-- ============================================================
-- Code    —— 记录定位键，删除/修改端点按 Code 解析记录，必须存在且唯一
-- DicCode —— 关联列（TreeConfig.RelateField = "DicCode"），存父字典的 Code
--            与 Sys_User.OrgCode → Sys_Organization.Code 完全同构
-- IsValid / IsDeleted / DeleteTime / DeleteBy —— 同上
--
-- 注意：明细表**不设业务编码字段**。字典项没有「项编码」，关联只依赖 Code，
--       这与「没有具体项的编码，也能进行关联」的要求一致。
-- ============================================================

CALL __yzh_dict_add_col('Sys_DictionaryList', 'Code',
  '`Code` varchar(50) NULL COMMENT ''稳定标识（随机唯一，定位键）'' AFTER `DicList_ID`');
CALL __yzh_dict_add_col('Sys_DictionaryList', 'DicCode',
  '`DicCode` varchar(100) NULL COMMENT ''所属字典 Code（= Sys_Dictionary.Code）'' AFTER `Dic_ID`');
CALL __yzh_dict_add_col('Sys_DictionaryList', 'IsValid',
  '`IsValid` tinyint NOT NULL DEFAULT 1 COMMENT ''有效标志（1=有效，0=无效）'' AFTER `Enable`');
CALL __yzh_dict_add_col('Sys_DictionaryList', 'IsDeleted',
  '`IsDeleted` tinyint NOT NULL DEFAULT 0 COMMENT ''删除标志（1=已删除）'' AFTER `IsValid`');
CALL __yzh_dict_add_col('Sys_DictionaryList', 'DeleteTime',
  '`DeleteTime` datetime NULL COMMENT ''删除时间'' AFTER `IsDeleted`');
CALL __yzh_dict_add_col('Sys_DictionaryList', 'DeleteBy',
  '`DeleteBy` varchar(50) NULL COMMENT ''删除人'' AFTER `DeleteTime`');

-- 4.1 回填 Code = 随机唯一值
UPDATE Sys_DictionaryList SET Code = REPLACE(UUID(), '-', '') WHERE Code IS NULL OR Code = '';

-- 4.2 回填 DicCode = 父字典的 Code
CALL __yzh_dict_exec_if_col('Sys_DictionaryList', 'Dic_ID',
  'UPDATE Sys_DictionaryList l INNER JOIN Sys_Dictionary d ON l.Dic_ID = d.Dic_ID
   SET l.DicCode = d.Code');

-- 4.3 回填 IsValid：沿用历史 Enable（NULL 视为启用）
--     注意：历史 Enable=0 的明细（如 Dic_ID=1 下的「女」）将不再出现在字典下拉中，
--     这与旧 Vol 系统行为一致。如需全部保留，请改为 SET IsValid = 1。
UPDATE Sys_DictionaryList SET IsValid = COALESCE(Enable, 1);

-- 4.4 Code / DicCode 升为 NOT NULL（定位键与关联键均不允许为空）
ALTER TABLE Sys_DictionaryList
  MODIFY COLUMN `Code` varchar(50) NOT NULL COMMENT '稳定标识（随机唯一，定位键）',
  MODIFY COLUMN `DicCode` varchar(100) NOT NULL COMMENT '所属字典 Code（= Sys_Dictionary.Code）';

-- 4.5 索引
CALL __yzh_dict_add_uniq('Sys_DictionaryList', 'uk_sys_dictionarylist_code',    '`Code`');
CALL __yzh_dict_add_idx ('Sys_DictionaryList', 'idx_sys_dictionarylist_dicode', '`DicCode`');


-- ============================================================
-- 第五步：Sys_DictionaryList（明细表）—— 删除全部 Id 型关联字段
-- ============================================================
-- Dic_ID   ：id 型关联，已被 DicCode 取代
-- CreateID ：id 型审计字段
-- ModifyID ：id 型审计字段
-- 安全性：两表无外键约束；无其他表引用 Sys_DictionaryList.DicList_ID（已全库核查）。
-- ============================================================

CALL __yzh_dict_drop_col('Sys_DictionaryList', 'Dic_ID');
CALL __yzh_dict_drop_col('Sys_DictionaryList', 'CreateID');
CALL __yzh_dict_drop_col('Sys_DictionaryList', 'ModifyID');


-- ============================================================
-- 第六步：校验（只读，可重复执行）
-- ============================================================

SELECT '--- 6.1 Code 唯一性与回填完整性（应全为 0） ---' AS section;
SELECT
  (SELECT COUNT(*) FROM Sys_Dictionary     WHERE Code IS NULL OR Code = '')              AS main_missing_code,
  (SELECT COUNT(*) FROM Sys_DictionaryList WHERE Code IS NULL OR Code = '')              AS list_missing_code,
  (SELECT COUNT(*) FROM (SELECT Code FROM Sys_Dictionary     GROUP BY Code HAVING COUNT(*)>1) t) AS main_dup_code,
  (SELECT COUNT(*) FROM (SELECT Code FROM Sys_DictionaryList GROUP BY Code HAVING COUNT(*)>1) t) AS list_dup_code;

SELECT '--- 6.2 DicNo 已可空（应返回 YES） ---' AS section;
SELECT IS_NULLABLE AS dicno_nullable FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'Sys_Dictionary' AND COLUMN_NAME = 'DicNo';

SELECT '--- 6.3 根节点（应 4 行） ---' AS section;
SELECT Dic_ID, DicNo, DicName, Code FROM Sys_Dictionary WHERE ParentCode IS NULL ORDER BY Dic_ID;

SELECT '--- 6.4 树完整性：断链父节点（应 0） ---' AS section;
SELECT COUNT(*) AS broken_parent FROM Sys_Dictionary child
WHERE child.ParentCode IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM Sys_Dictionary p WHERE p.Code = child.ParentCode);

SELECT '--- 6.5 关联完整性：孤儿明细（应 0） ---' AS section;
SELECT COUNT(*) AS orphan_items FROM Sys_DictionaryList l
WHERE NOT EXISTS (SELECT 1 FROM Sys_Dictionary d WHERE d.Code = l.DicCode);

SELECT '--- 6.6 Id 型关联字段已清除（应 0 行，主键除外） ---' AS section;
SELECT TABLE_NAME, COLUMN_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('Sys_Dictionary', 'Sys_DictionaryList')
  AND (COLUMN_NAME LIKE '%Id' OR COLUMN_NAME LIKE '%ID')
  AND COLUMN_NAME NOT IN ('Dic_ID', 'DicList_ID');

SELECT '--- 6.7 规模核对（主表 50 / 明细 128） ---' AS section;
SELECT
  (SELECT COUNT(*) FROM Sys_Dictionary)     AS main_total,
  (SELECT COUNT(*) FROM Sys_DictionaryList) AS list_total;

SELECT '--- 6.8 树结构预览（DICT 分支根应 17 个子字典） ---' AS section;
SELECT d.ParentCode, COUNT(*) AS children
FROM Sys_Dictionary d WHERE d.ParentCode IS NOT NULL
GROUP BY d.ParentCode ORDER BY d.ParentCode;

SELECT '--- 6.9 最终表结构 ---' AS section;
SELECT TABLE_NAME, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('Sys_Dictionary', 'Sys_DictionaryList')
ORDER BY TABLE_NAME, ORDINAL_POSITION;


-- ============================================================
-- 第七步：清理幂等辅助过程
-- ============================================================

DROP PROCEDURE IF EXISTS __yzh_dict_add_col;
DROP PROCEDURE IF EXISTS __yzh_dict_drop_col;
DROP PROCEDURE IF EXISTS __yzh_dict_add_uniq;
DROP PROCEDURE IF EXISTS __yzh_dict_add_idx;
DROP PROCEDURE IF EXISTS __yzh_dict_exec_if_col;
-- 兼容清理（若历史遗留再次出现）
DROP PROCEDURE IF EXISTS __yzh_add_col;
DROP PROCEDURE IF EXISTS __yzh_drop_col;
DROP PROCEDURE IF EXISTS __yzh_add_idx;
DROP PROCEDURE IF EXISTS __yzh_exec_if_col;


-- ============================================================
-- 附：本轮未执行、留待后续清理的项
-- ============================================================
-- ① 两表 Enable 列与新的 IsValid 语义重复。本次已用 IsValid = Enable 一次性对齐，
--    但此后 Enable 不再被新架构写入，会逐渐过期。建议旧 Vol 代码退役后删除：
--      ALTER TABLE Sys_Dictionary     DROP COLUMN Enable;
--      ALTER TABLE Sys_DictionaryList DROP COLUMN Enable;
--
-- ② 框架既有表同样残留 Id 型审计字段，与新铁律不一致，建议另立任务统一清理：
--      Sys_Organization : CreateID / ModifyID / DeleteID
--      Sys_User         : CreateID / ModifyID / OrgId / Dept_Id / Role_Id / ParentUserId
--      Sys_Role         : ParentId
--    （Sys_Role.ParentId 被 RoleController.cs:81-114 引用，清理需同步改代码。）
--
-- ③ Sys_User.Code 目前被 UserController.cs:99 当作 UserName 使用
--    （ExistsByCodeAsync(entity.UserName)），导致账号唯一性校验实际失效。
--    属既有缺陷，与本任务无关，建议单独修复。
