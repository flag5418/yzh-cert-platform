-- ============================================================================
-- 20261008_standard_family_V1.sql
-- 目的：标准三层体系落库 —— 新建「标准族」表 + cert_iso_standard 加族外键 + 回填
-- 裁决：体系=字典(已有) / 族=新表(本脚本) / 版本=现有 cert_iso_standard
--      族表 = 全局表，不带 OrgCode（照 cert_phase_definition 惯例）
--      ⛔ 绝不动 ParentCode/IsLeaf —— TreeTableControllerBase.GetRootNodes
--         只返回 ParentCode=Root 的行，改了会让现有 /cert/iso-standard 白屏
-- ============================================================================
-- 执行：docker exec -i -e MYSQL_PWD='Yzh123456.' yzh-mysql mysql -uroot \
--        --default-character-set=utf8mb4 yzh_cert_platform < 本文件
-- 幂等：可重复执行（表存在则跳过建表；列存在则跳过加列）
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ----------------------------------------------------------------------------
-- 1. 建族表 cert_standard_family
--    ⚠️ 唯一性不建 DB 索引（除 Code）—— 与项目惯例一致：
--       cert_iso_standard 的 UniqueField 亦只做应用层校验（OnBeforeAdd/Update），
--       因为 DB 唯一索引会把 IsDeleted=1 的行也算进去 → 软删后重建同名族必撞
--    唯一性规则（应用层校验）：FamilyNo 全局唯一 / (Category, FamilyName) 组合唯一
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cert_standard_family (
    Id           bigint       NOT NULL AUTO_INCREMENT COMMENT '自增主键',
    Code         varchar(36)  NOT NULL                COMMENT '业务编码（GUID，框架 AddCore 自动填充）',
    FamilyNo     varchar(50)  NOT NULL                COMMENT '族人读编号（如 iso9000）。⚠️ 禁叫 FamilyCode —— 避免与版本表存 GUID 的 FamilyCode 撞语义',
    FamilyName   varchar(200) NOT NULL                COMMENT '族中文名（如 ISO 9000 质量管理体系族）',
    Category     varchar(50)  NOT NULL DEFAULT 'quality' COMMENT '所属体系（存 iso_category 字典 DicValue）',
    Description  text         NULL                    COMMENT '族说明',
    Sort         int          NOT NULL DEFAULT 0      COMMENT '排序号',
    IsValid      int          NOT NULL DEFAULT 1      COMMENT '启用标志（1=启用 0=停用，铁律九：禁用 Enable）',
    CreateTime   datetime     NULL                    COMMENT '创建时间',
    CreateBy     varchar(50)  NULL                    COMMENT '创建人',
    UpdateTime   datetime     NULL                    COMMENT '更新时间',
    UpdateBy     varchar(50)  NULL                    COMMENT '更新人',
    DeleteTime   datetime     NULL                    COMMENT '删除时间（软删）',
    DeleteBy     varchar(50)  NULL                    COMMENT '删除人（软删）',
    IsDeleted    tinyint      NOT NULL DEFAULT 0      COMMENT '软删标志',
    PRIMARY KEY (Id),
    UNIQUE KEY uk_code (Code),
    KEY idx_category (Category),
    KEY idx_family_no (FamilyNo)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='ISO标准族（体系→族→版本 三层中的族层）';

-- ----------------------------------------------------------------------------
-- 2. 版本表加族外键列 + 索引
--    ⛔ 不动 ParentCode/IsLeaf（红线：TreeConfig.ParentCodeField=ParentCode + MaxLevel=1）
--    幂等：列已存在则跳过
-- ----------------------------------------------------------------------------
SET @has_col := (SELECT COUNT(*)
                   FROM information_schema.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME   = 'cert_iso_standard'
                    AND COLUMN_NAME  = 'FamilyCode');
SET @ddl := IF(@has_col = 0,
    'ALTER TABLE cert_iso_standard
        ADD COLUMN FamilyCode varchar(36) NULL COMMENT ''所属标准族（→ cert_standard_family.Code）。存 GUID，非人读编号'' AFTER Category,
        ADD INDEX idx_family_code (FamilyCode)',
    'SELECT ''FamilyCode 列已存在，跳过'' AS msg');
PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;

-- ----------------------------------------------------------------------------
-- 3. 回填：建首个族 iso9000 并挂 iso9001
--    ⚠️ 只回填 iso9001 —— 活数据仅 2 条（iso9001/2015、iso4001/2016），
--       iso4001 数据存疑（名称「食品标准」但 Category=quality），留 FamilyCode=NULL
--       作为「待归族」样本，便于验证新页面左树的未归族分支
--    幂等：族已存在则复用
-- ----------------------------------------------------------------------------
SET @fam := (SELECT Code FROM cert_standard_family
              WHERE FamilyNo = 'iso9000' AND IsDeleted = 0 LIMIT 1);
SET @fam := IF(@fam IS NULL, UUID(), @fam);

INSERT INTO cert_standard_family
       (Code, FamilyNo, FamilyName, Category, Description, Sort, IsValid, CreateTime, CreateBy, IsDeleted)
SELECT @fam, 'iso9000', 'ISO 9000 质量管理体系族', 'quality',
       'ISO 9000 系列质量管理体系基础与术语标准所属族', 10, 1, NOW(), '超级管理员', 0
  FROM DUAL
 WHERE NOT EXISTS (SELECT 1 FROM cert_standard_family WHERE FamilyNo = 'iso9000');

UPDATE cert_iso_standard
   SET FamilyCode = (SELECT Code FROM cert_standard_family
                      WHERE FamilyNo = 'iso9000' AND IsDeleted = 0 LIMIT 1)
 WHERE StandardCode = 'iso9001'
   AND IsDeleted = 0
   AND (FamilyCode IS NULL OR FamilyCode = '');

-- ----------------------------------------------------------------------------
-- 4. 验证
-- ----------------------------------------------------------------------------
SELECT '== 验证1：族表 ==' AS t;
SELECT Id, Code, FamilyNo, FamilyName, Category, Sort, IsValid, IsDeleted
  FROM cert_standard_family ORDER BY Sort;

SELECT '== 验证2：版本挂族 ==' AS t;
SELECT s.Id, s.StandardCode, s.StandardName, s.VersionYear, s.Category,
       s.FamilyCode, f.FamilyNo, f.FamilyName
  FROM cert_iso_standard s
  LEFT JOIN cert_standard_family f ON f.Code = s.FamilyCode
 WHERE s.IsDeleted = 0
 ORDER BY s.Id;

SELECT '== 验证3：FamilyCode 列已加 ==' AS t;
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_COMMENT
  FROM information_schema.COLUMNS
 WHERE TABLE_SCHEMA = DATABASE()
   AND TABLE_NAME = 'cert_iso_standard' AND COLUMN_NAME = 'FamilyCode';

SELECT '== 验证4：ParentCode/IsLeaf 未被改动（应仍为 NULL/1）==' AS t;
SELECT DISTINCT ParentCode, IsLeaf FROM cert_iso_standard;

-- ============================================================================
-- 回滚：
--   UPDATE cert_iso_standard SET FamilyCode=NULL;
--   ALTER TABLE cert_iso_standard DROP INDEX idx_family_code, DROP COLUMN FamilyCode;
--   DROP TABLE cert_standard_family;
-- ============================================================================
