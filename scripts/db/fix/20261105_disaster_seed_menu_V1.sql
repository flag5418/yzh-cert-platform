-- ===================================================================
-- 20261105_disaster_seed_menu_V1.sql — 灾难恢复 · 全量菜单种子
--
-- 背景：iCloud 灾难后空库，Sys_Menu = 空 → 前端侧栏全白。
-- 前菜单重组方案：docs/10-YZH架构/菜单结构 与 20261002_menu_restructure_V1.sql。
-- 当前路由：cert-admin/src/router/index.ts
--
-- 结构（Tag=admin / auditor，≤2 层）：
--   平台管理 MENU_00001 —— 9 项系统页
--   业务管理 MENU_00002
--      ├── 基础资料 MENU_00301（8 项）
--      ├── 规则定义 MENU_00302（5 项）
--      └── 系统管理 MENU_00303（4 项）
--
--   专家系统 MENU_AUD_00 —— 6 + 3 项
--
-- 幂等：整表 DELETE + INSERT（灾难重建场景，非增量升级）。
-- � 跑完必须：./scripts/db/verify/sync_menu_urls.sh
-- ===================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 清理（空表，幂等兜底）
DELETE FROM Sys_Menu;

-- =================================================================
-- 一、顶级：平台管理 / 业务管理
-- =================================================================
INSERT INTO Sys_Menu (Code,ParentCode,MenuName,Icon,OrderNo,Url,Tag,IsValid,IsDeleted,CreateTime,CreateBy) VALUES
('MENU_00001','0','平台管理','Setting',100,NULL,'admin',1,0,NOW(),'seed_disaster'),
('MENU_00002','0','业务管理','DocumentChecked',200,NULL,'admin',1,0,NOW(),'seed_disaster');

-- =================================================================
-- 二、平台管理下：9 项系统页
-- =================================================================
INSERT INTO Sys_Menu (Code,ParentCode,MenuName,Icon,OrderNo,Url,Tag,IsValid,IsDeleted,CreateTime,CreateBy) VALUES
('MENU_00101','MENU_00001','机构-人员管理','OfficeBuilding', 100,'/system/organization', 'admin',1,0,NOW(),'seed_disaster'),
('MENU_00102','MENU_00001','角色-人员管理','UserFilled',     200,'/system/role-user',    'admin',1,0,NOW(),'seed_disaster'),
('MENU_00103','MENU_00001','角色-菜单管理','Menu',           300,'/system/role-menu',    'admin',1,0,NOW(),'seed_disaster'),
('MENU_00104','MENU_00001','角色-接口管理','Connection',     400,'/system/role-api',     'admin',1,0,NOW(),'seed_disaster'),
('MENU_00105','MENU_00001','菜单管理','Folder',              500,'/system/menu',         'admin',1,0,NOW(),'seed_disaster'),
('MENU_00106','MENU_00001','接口管理','Link',                600,'/system/api',          'admin',1,0,NOW(),'seed_disaster'),
('MENU_00107','MENU_00001','数据字典','Collection',          700,'/system/dictionary',   'admin',1,0,NOW(),'seed_disaster'),
('MENU_00108','MENU_00001','日志管理','Document',            800,'/system/log',          'admin',1,0,NOW(),'seed_disaster'),
('MENU_00109','MENU_00001','系统参数配置','Tools',           900,'/system/config',       'admin',1,0,NOW(),'seed_disaster'),
('MENU_00110','MENU_00001','服务器监控','Monitor',          950,'/business/server-monitor','admin',1,0,NOW(),'seed_disaster');

-- =================================================================
-- 三、业务管理下：三级分组
-- =================================================================
INSERT INTO Sys_Menu (Code,ParentCode,MenuName,Icon,OrderNo,Url,Tag,IsValid,IsDeleted,CreateTime,CreateBy) VALUES
('MENU_00301','MENU_00002','基础资料','Files',   100,NULL,'admin',1,0,NOW(),'seed_disaster'),
('MENU_00302','MENU_00002','规则定义','SetUp',   200,NULL,'admin',1,0,NOW(),'seed_disaster'),
('MENU_00303','MENU_00002','系统管理','Setting', 300,NULL,'admin',1,0,NOW(),'seed_disaster');

-- ── 基础资料 8 项 ──
INSERT INTO Sys_Menu (Code,ParentCode,MenuName,Icon,OrderNo,Url,Tag,IsValid,IsDeleted,CreateTime,CreateBy) VALUES
('MENU_00202','MENU_00301','认证机构管理','OfficeBuilding', 100,'/foundation/certification-body','admin',1,0,NOW(),'seed_disaster'),
('MENU_00219','MENU_00301','标准管理','Document',          200,'/cert/standard-manage',        'admin',1,0,NOW(),'seed_disaster'),
('MENU_00203','MENU_00301','阶段管理','Histogram',         300,'/cert/cert-stage',              'admin',1,0,NOW(),'seed_disaster'),
('MENU_00205','MENU_00301','机构-标准关联','Connection',   400,'/cert/link-org-standard',       'admin',1,0,NOW(),'seed_disaster'),
('MENU_00206','MENU_00301','机构-阶段关联','Link',         500,'/cert/link-org-stage',          'admin',1,0,NOW(),'seed_disaster'),
('MENU_00207','MENU_00301','标准资料清单','FolderOpened',  600,'/business/directory-manager',   'admin',1,0,NOW(),'seed_disaster'),
('MENU_00220','MENU_00301','条款属性池','ListBox',        700,'/cert/clause-attr-def',        'admin',1,0,NOW(),'seed_disaster'),
('MENU_00221','MENU_00301','条款配置','Connection',        800,'/cert/clause-attr',            'admin',1,0,NOW(),'seed_disaster');

-- ── 规则定义 5 项 ──
INSERT INTO Sys_Menu (Code,ParentCode,MenuName,Icon,OrderNo,Url,Tag,IsValid,IsDeleted,CreateTime,CreateBy) VALUES
('MENU_00212','MENU_00302','NC 规则设计','Cpu',             100,'/business/nc-config',         'admin',1,0,NOW(),'seed_disaster'),
('MENU_00211','MENU_00302','报告内容设计','DocumentChecked',  200,'/business/report-rule-config',  'admin',1,0,NOW(),'seed_disaster'),
('MENU_00210','MENU_00302','文档语义规则','MagicStick',       300,'/business/prompt-template',    'admin',1,0,NOW(),'seed_disaster'),
('MENU_00208','MENU_00302','文档提取规则','Files',            400,'/business/doc-extract-rule',   'admin',1,0,NOW(),'seed_disaster'),
('MENU_00218','MENU_00302','标准文档填写规则','EditPen',      500,'/business/doc-fill-rule',      'admin',1,0,NOW(),'seed_disaster');

-- ── 系统管理 4 项 ──
INSERT INTO Sys_Menu (Code,ParentCode,MenuName,Icon,OrderNo,Url,Tag,IsValid,IsDeleted,CreateTime,CreateBy) VALUES
('MENU_00217','MENU_00303','企业资料参数','Setting',          100,'/business/fill-param-def','admin',1,0,NOW(),'seed_disaster'),
('MENU_00215','MENU_00303','技能管理','Avatar',               200,'/business/skill-manage',  'admin',1,0,NOW(),'seed_disaster'),
('MENU_00214','MENU_00303','AI 费用分析','DataAnalysis',       300,'/business/ai-usage',      'admin',1,0,NOW(),'seed_disaster'),
('MENU_00216','MENU_00303','队列监控','Cpu',                  400,'/business/queue-monitor', 'admin',1,0,NOW(),'seed_disaster');

-- =================================================================
-- 四、专家系统（Tag=auditor）
-- =================================================================
INSERT INTO Sys_Menu (Code,ParentCode,MenuName,Icon,OrderNo,Url,Tag,IsValid,IsDeleted,CreateTime,CreateBy) VALUES
('MENU_AUD_00','0','专家系统','Avatar',300,'/','auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_01','MENU_AUD_00','系统一览','Odometer',      100,'/overview',              'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_02','MENU_AUD_00','企业管理','OfficeBuilding', 200,'/enterprises',           'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_03','MENU_AUD_00','任务中心','List',           300,'/tasks',                 'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_13','MENU_AUD_00','企业队列监控','Cpu',        310,'/queue',                 'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_04','MENU_AUD_00','认证资料管理','FolderOpened',400,'/resources',            'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_11','MENU_AUD_00','企业原始资料管理','Folder', 450,'/enterprise-original',   'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_12','MENU_AUD_00','企业资料规范化','MagicStick',470,'/enterprise-normalize', 'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_18','MENU_AUD_00','企业资料','Files',         295,'/enterprise-doc',        'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_05','MENU_AUD_00','组织与成员','UserFilled',   500,'/organization',           'auditor',1,0,NOW(),'seed_disaster'),
('MENU_AUD_06','MENU_AUD_00','系统设置','Setting',        600,'/settings',              'auditor',1,0,NOW(),'seed_disaster');

-- =================================================================
-- 五、校验
-- =================================================================
SELECT COUNT(*) AS menu_total FROM Sys_Menu WHERE IsDeleted = 0;
SELECT Tag, COUNT(*) AS n FROM Sys_Menu WHERE IsDeleted = 0 GROUP BY Tag;
