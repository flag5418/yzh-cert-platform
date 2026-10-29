# -*- coding: utf-8 -*-
"""应用「认证计划」DDL：cert_iso_standard.Abbrev + cert_audit_plan + cert_audit_plan_detail + 菜单"""
import pymysql

conn = pymysql.connect(
    host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
    database='yzh_cert_platform', charset='utf8mb4', autocommit=True,
)
cur = conn.cursor()


def run(sql, label):
    print('>>>', label)
    cur.execute(sql)


# 1) Abbrev 列
cur.execute("""SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
  WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='cert_iso_standard' AND COLUMN_NAME='Abbrev'""")
if cur.fetchone()[0] == 0:
    run("ALTER TABLE `cert_iso_standard` ADD COLUMN `Abbrev` VARCHAR(20) NULL DEFAULT NULL "
        "COMMENT '标准简称（如 iso9001→e），用于条款序号前缀显示' AFTER `VersionYear`", 'ADD Abbrev')
else:
    print('>>> Abbrev 已存在，跳过')

for slug, ab in [('iso9001', 'e'), ('iso14001', 'n'), ('iso45001', 's'),
                 ('iso13485', 'm'), ('iso22000', 'f'), ('iso27001', 'i')]:
    cur.execute("UPDATE `cert_iso_standard` SET `Abbrev`=%s "
                "WHERE `StandardCode`=%s AND (`Abbrev` IS NULL OR `Abbrev`='')", (ab, slug))

# 2) 计划主表
run("""CREATE TABLE IF NOT EXISTS `cert_audit_plan` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT,
  `Code` VARCHAR(50) NOT NULL,
  `Name` VARCHAR(200) DEFAULT NULL COMMENT '计划名称',
  `EnterpriseCode` VARCHAR(50) NOT NULL COMMENT '企业编码（cert_enterprise.Code）',
  `StageCode` VARCHAR(50) NOT NULL COMMENT '阶段编码（cert_cert_stage.Code，GUID）',
  `Standards` VARCHAR(500) DEFAULT NULL COMMENT '认证标准（多个逗号隔开，ISOStandard.Code GUID）',
  `Sort` INT NOT NULL DEFAULT 0,
  `Remark` TEXT DEFAULT NULL,
  `IsValid` INT NOT NULL DEFAULT 1,
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
  `CreateTime` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `CreateBy` VARCHAR(64) DEFAULT NULL,
  `UpdateTime` DATETIME NULL DEFAULT NULL COMMENT '更新时间（新建为空，更新时填）',
  `UpdateBy` VARCHAR(64) DEFAULT NULL,
  `DeleteTime` DATETIME DEFAULT NULL,
  `DeleteBy` VARCHAR(64) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_enterprise_code` (`EnterpriseCode`),
  KEY `idx_stage_code` (`StageCode`),
  KEY `idx_isvalid_deleted` (`IsValid`,`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='认证计划主表（专家端）'""", 'CREATE cert_audit_plan')

# 3) 计划明细
run("""CREATE TABLE IF NOT EXISTS `cert_audit_plan_detail` (
  `Id` BIGINT NOT NULL AUTO_INCREMENT,
  `Code` VARCHAR(50) NOT NULL,
  `PlanCode` VARCHAR(50) NOT NULL COMMENT '所属计划（cert_audit_plan.Code）',
  `Clauses` VARCHAR(1000) DEFAULT NULL COMMENT '条款（逗号隔开，ISOClause.Code GUID）',
  `Departments` VARCHAR(500) DEFAULT NULL COMMENT '部门（逗号隔开，CertEnterpriseDept.Code GUID）',
  `DeptNames` VARCHAR(500) DEFAULT NULL COMMENT '部门名称（逗号隔开，冗余）',
  `ClauseNumbers` VARCHAR(1000) DEFAULT NULL COMMENT '条款序号（逗号隔开，冗余）',
  `Sort` INT NOT NULL DEFAULT 0,
  `Remark` TEXT DEFAULT NULL,
  `IsValid` INT NOT NULL DEFAULT 1,
  `IsDeleted` TINYINT(1) NOT NULL DEFAULT 0,
  `CreateTime` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `CreateBy` VARCHAR(64) DEFAULT NULL,
  `UpdateTime` DATETIME NULL DEFAULT NULL COMMENT '更新时间（新建为空，更新时填）',
  `UpdateBy` VARCHAR(64) DEFAULT NULL,
  `DeleteTime` DATETIME DEFAULT NULL,
  `DeleteBy` VARCHAR(64) DEFAULT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_plan_code` (`PlanCode`),
  KEY `idx_isvalid_deleted` (`IsValid`,`IsDeleted`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='认证计划明细（条款×部门）'""", 'CREATE cert_audit_plan_detail')

# 4) 菜单
run("""INSERT INTO `Sys_Menu`
  (`Code`,`ParentCode`,`MenuName`,`Icon`,`OrderNo`,`Url`,`Tag`,`Description`,`IsValid`,`IsDeleted`,`CreateTime`,`CreateBy`)
VALUES ('MENU_AUD_17','MENU_AUD_00','认证计划','Calendar',290,'/audit-plan','auditor',
  '按条款为核心的体系认证入场计划（企业/阶段/多标准 → 部门+条款组合 → 计划主表+明细）',1,0,NOW(),'seed_auditor')
ON DUPLICATE KEY UPDATE `ParentCode`=VALUES(`ParentCode`),`Icon`=VALUES(`Icon`),`OrderNo`=VALUES(`OrderNo`),
  `Url`=VALUES(`Url`),`Tag`=VALUES(`Tag`),`Description`=VALUES(`Description`),`IsDeleted`=0""", 'INSERT menu')

# 5) 授权
run("""INSERT INTO `Sys_RoleMenu` (`Id`,`RoleCode`,`MenuCode`,`OrderNo`,`CreateTime`,`CreateBy`)
SELECT REPLACE(UUID(),'-',''), 'ROLE_AUDIT_CLIENT_ADMIN', m.`Code`, m.`OrderNo`, NOW(), 'seed_auditor'
FROM `Sys_Menu` m
WHERE m.`Code`='MENU_AUD_17' AND m.`IsDeleted`=0
  AND NOT EXISTS (SELECT 1 FROM `Sys_RoleMenu` rm WHERE rm.`RoleCode`='ROLE_AUDIT_CLIENT_ADMIN' AND rm.`MenuCode`=m.`Code`)""",
    'GRANT role')

# 验证
print('\n=== 验证 ===')
cur.execute("""SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA=DATABASE()
  AND TABLE_NAME IN ('cert_audit_plan','cert_audit_plan_detail')""")
print('tables:', [r[0] for r in cur.fetchall()])
cur.execute("SELECT StandardCode, Abbrev FROM cert_iso_standard WHERE Abbrev IS NOT NULL LIMIT 20")
print('abbrev:', cur.fetchall())
cur.execute("SELECT Code, MenuName, Url FROM Sys_Menu WHERE Code='MENU_AUD_17'")
print('menu:', cur.fetchall())
conn.close()
print('DONE')
