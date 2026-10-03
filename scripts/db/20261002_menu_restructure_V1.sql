-- ============================================================================
-- 20261002_menu_restructure_V1.sql
-- 后台管理（Tag=admin）体系认证菜单重组 —— 从「业务管理 17 项平铺」改为「3 组分类」
--
-- 背景（用户 2026-10-02 裁决）：
--   「后台的体系认证的菜单都挤在一起，感觉比较乱」「菜单名称比较乱，不够精简、不够科学」
--
-- 定稿结构（★ 用户逐项裁决，2 级侧栏）：
--   平台管理（MENU_00001，原「系统管理」，改名让出「系统管理」名）
--     └ 9 项系统级页面（本次不动）
--   业务管理（MENU_00002）
--     ├ 基础资料（MENU_00301）
--     │   认证机构管理 / 标准管理 / 阶段管理 / 机构-标准关联 / 机构-阶段关联
--     │   标准资料清单 / NC 检查项 / 报告章节
--     ├ 规则定义（MENU_00302，原「流程定义」改名）
--     │   NC 规则设计 / 报告内容设计 / 文档提取规则
--     └ 系统管理（MENU_00303）
--         标准核心字段 / Prompt 模板 / 技能管理 / AI 费用分析 / 队列监控
--
-- 命名法（消除「定义 / 设计」歧义）：
--   不带「设计」= 左树右表**清单**（可增删改查）  → 归「基础资料」
--   带「设计」  = 工作流**设计器**（NC / 报告是两种完全不同的配置） → 归「规则定义」
--
-- ⛔ 铁律与约束：
--   1. 本脚本**只改 Sys_Menu**，不碰 Sys_RoleMenu / 路由 / 页面。
--      超管由 MenuPermissionService:55-56 拿全量；非超管由 :88-100「祖先补全」自动挂上父目录。
--   2. **全部 Url 保持不变** —— 叶子菜单只改 ParentCode / OrderNo / MenuName，
--      故前端守卫 R12（路由 ↔ 菜单双向差集）天然通过。
--   3. 目录型菜单 Url = NULL（分类节点）—— R12 readMenuSnapshot() 会跳过空 Url 与 '/'。
--   4. ⛔ 专家端 `MENU_AUD_*`（10 项）本次**零改动**（用户：「专家端的暂时不处理」）。
--   5. 幂等：可重复执行。目录菜单用「不存在才 INSERT」，叶子用「按 Code 定位 UPDATE」。
--   6. 执行后必跑：./scripts/db/verify/sync_menu_urls.sh（刷新菜单路由契约快照）
-- ============================================================================

SET NAMES utf8mb4;

-- ============================================================================
-- 一、顶级：MENU_00001「系统管理」→「平台管理」
--   ★ 目的：把「系统管理」这个名字让给业务侧的运行配置分组（用户指定其为「系统管理」）。
--   ★ 同时补 Tag='admin'：原 Tag 为 NULL，filterMenuTreeByTag 靠 `!m.tag` 兜底，
--     显式补齐后语义更清晰（Tag 不参与权限过滤，只用于前端侧边栏分流）。
-- ============================================================================
UPDATE `Sys_Menu`
   SET `MenuName` = '平台管理',
       `Tag`      = 'admin',
       `UpdateTime` = NOW(),
       `UpdateBy`  = 'menu-restructure'
 WHERE `Code` = 'MENU_00001'
   AND `IsDeleted` = 0;

-- ============================================================================
-- 二、新增 3 个目录型分组菜单（Url = NULL = 分类节点）
--   OrderNo：基础资料 100 / 规则定义 200 / 系统管理 300
--   Tag：admin（与父节点业务管理一致）
-- ============================================================================

-- 基础资料
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`,
   `CreateTime`, `CreateBy`, `Tag`, `IsDeleted`, `IsValid`)
SELECT 'MENU_00301', 'MENU_00002', '基础资料', NULL, 'Files',
       '认证体系的主数据：机构 / 标准 / 阶段 / 三者关联 / 资料清单 / 规则清单',
       100, NULL, NOW(), 'menu-restructure', 'admin', 0, 1
  FROM DUAL
 WHERE NOT EXISTS (SELECT 1 FROM `Sys_Menu` WHERE `Code` = 'MENU_00301');

-- 规则定义（NC 与报告是两套完全不同的配置，故保持两个独立入口，不合并）
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`,
   `CreateTime`, `CreateBy`, `Tag`, `IsDeleted`, `IsValid`)
SELECT 'MENU_00302', 'MENU_00002', '规则定义', NULL, 'SetUp',
       '工作流与抽取规则的设计器：NC 规则 / 报告内容 / 文档提取',
       200, NULL, NOW(), 'menu-restructure', 'admin', 0, 1
  FROM DUAL
 WHERE NOT EXISTS (SELECT 1 FROM `Sys_Menu` WHERE `Code` = 'MENU_00302');

-- 系统管理（业务侧的运行与 AI 配置）
INSERT INTO `Sys_Menu`
  (`Code`, `ParentCode`, `MenuName`, `Auth`, `Icon`, `Description`, `OrderNo`, `Url`,
   `CreateTime`, `CreateBy`, `Tag`, `IsDeleted`, `IsValid`)
SELECT 'MENU_00303', 'MENU_00002', '系统管理', NULL, 'Setting',
       '体系认证运行参数与 AI 能力配置、费用与队列监控',
       300, NULL, NOW(), 'menu-restructure', 'admin', 0, 1
  FROM DUAL
 WHERE NOT EXISTS (SELECT 1 FROM `Sys_Menu` WHERE `Code` = 'MENU_00303');

-- ============================================================================
-- 三、叶子菜单：改挂 ParentCode + 重排 OrderNo（7 项同时改名）
--
-- ⚠️ Url 一律不动（保持路由契约）；⚠️ Tag 保持 'admin'。
-- ============================================================================

-- ── 基础资料（8 项）──────────────────────────────────────────────────────────

-- 100 认证机构管理
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00301', `OrderNo` = 100,
       `MenuName` = '认证机构管理',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00202' AND `IsDeleted` = 0;

-- 200 ISO 标准管理 → 标准管理
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00301', `OrderNo` = 200,
       `MenuName` = '标准管理',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00201' AND `IsDeleted` = 0;

-- 300 认证阶段定义 → 阶段管理
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00301', `OrderNo` = 300,
       `MenuName` = '阶段管理',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00203' AND `IsDeleted` = 0;

-- 400 机构-标准关联
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00301', `OrderNo` = 400,
       `MenuName` = '机构-标准关联',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00205' AND `IsDeleted` = 0;

-- 500 机构-阶段关联
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00301', `OrderNo` = 500,
       `MenuName` = '机构-阶段关联',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00206' AND `IsDeleted` = 0;

-- 600 标准文件管理 → 标准资料清单
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00301', `OrderNo` = 600,
       `MenuName` = '标准资料清单',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00207' AND `IsDeleted` = 0;

-- 700 NC 检查规则 → NC 检查项（清单归基础资料）
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00301', `OrderNo` = 700,
       `MenuName` = 'NC 检查项',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00213' AND `IsDeleted` = 0;

-- 800 报告章节定义 → 报告章节（清单归基础资料）
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00301', `OrderNo` = 800,
       `MenuName` = '报告章节',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00209' AND `IsDeleted` = 0;

-- ── 规则定义（3 项）──────────────────────────────────────────────────────────

-- 100 NC 规则设计（工作流设计器，与「NC 检查项」是同一对象的两半）
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00302', `OrderNo` = 100,
       `MenuName` = 'NC 规则设计',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00212' AND `IsDeleted` = 0;

-- 200 报告内容设计（工作流设计器）
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00302', `OrderNo` = 200,
       `MenuName` = '报告内容设计',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00214' AND `IsDeleted` = 0;

-- 300 文档提取规则（用户：「标准文档内容提取也是规则」）
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00302', `OrderNo` = 300,
       `MenuName` = '文档提取规则',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00208' AND `IsDeleted` = 0;

-- ── 系统管理（5 项）──────────────────────────────────────────────────────────

-- 100 体系认证全局参数定义 → 标准核心字段（用户：名称太长）
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00303', `OrderNo` = 100,
       `MenuName` = '标准核心字段',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00217' AND `IsDeleted` = 0;

-- 200 Prompt 模板
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00303', `OrderNo` = 200,
       `MenuName` = 'Prompt 模板',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00210' AND `IsDeleted` = 0;

-- 300 技能管理
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00303', `OrderNo` = 300,
       `MenuName` = '技能管理',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00211' AND `IsDeleted` = 0;

-- 400 AI 费用监控 → AI 费用分析
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00303', `OrderNo` = 400,
       `MenuName` = 'AI 费用分析',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00215' AND `IsDeleted` = 0;

-- 500 队列监控
UPDATE `Sys_Menu`
   SET `ParentCode` = 'MENU_00303', `OrderNo` = 500,
       `MenuName` = '队列监控',
       `UpdateTime` = NOW(), `UpdateBy` = 'menu-restructure'
 WHERE `Code` = 'MENU_00216' AND `IsDeleted` = 0;

-- ============================================================================
-- 四、遗留说明（本次不做，仅登记）
--   • 「空白文档填写规则」菜单：**页面尚未开发**，用户明确「暂时不管」，
--     故本次不建目录项。开发完成后在 MENU_00302 下追加一条（OrderNo=400）即可。
--   • 专家端 MENU_AUD_*（10 项）：用户明确「暂时不处理」，零改动。
-- ============================================================================

-- ────────────────────────────────────────────────────────────────────────────
-- 五、验收查询（执行后手工跑一次，结果应为 3 组 / 8+3+5 项）
-- ────────────────────────────────────────────────────────────────────────────
-- SELECT m.MenuName AS 组, COUNT(*) AS 菜单数
--   FROM Sys_Menu c JOIN Sys_Menu m ON c.ParentCode = m.Code
--  WHERE m.Code IN ('MENU_00301','MENU_00302','MENU_00303') AND c.IsDeleted = 0
--  GROUP BY m.Code, m.OrderNo, m.MenuName ORDER BY m.OrderNo;