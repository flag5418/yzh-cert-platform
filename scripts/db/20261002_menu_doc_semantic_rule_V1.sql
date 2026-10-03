-- ============================================================================
-- 20261002_menu_doc_semantic_rule_V1.sql
-- 文档语义规则 —— 菜单落位（34 号 §8.3 方案甲）
--
-- 改动（⛔ 只动 Sys_Menu 一行，URL / 页面目录 / 后端 Controller 全部不变）：
--   MenuName  : Prompt 模板  → 文档语义规则
--   ParentCode: MENU_00303 系统管理 → MENU_00302 规则定义
--   OrderNo   : 200 → 250
--
-- 为什么 OrderNo = 250（34 号 §8.4）：
--   落在「报告内容设计 200」与「文档提取规则 300」之间，
--   使侧栏顺序成为 语义(识别) → 提取(取值) → 生成(落笔) 的业务顺序。
--
-- 为什么 URL 与 Controller 都不改（34 号 §8.3）：
--   ① 改 URL → 触发守卫 R12 全量校验，收益低；
--   ② 改 Controller 名 → ApiCode 变化 → 角色-接口关联**静默断裂**（AGENTS.md 编码强制约定 ②）
--   ⇒ 两方案下 PromptTemplateController 均不改名，这是硬约束。
--
-- 改菜单后必跑：
--   ./scripts/db/verify/sync_menu_urls.sh      （刷新 R12 快照）
--   cd src/certplatform-web && node scripts/guards.mjs
--
-- 设计依据：34-核心模块定位与端到端流程-V1.md §8（命名与菜单落位建议）
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ① 改名 + 挪父级 + 调序（幂等：重复执行结果一致）
UPDATE `Sys_Menu`
   SET `MenuName`   = '文档语义规则',
       `ParentCode` = 'MENU_00302',
       `OrderNo`    = 250,
       `Description` = CONCAT('文档语义规则（原「Prompt 模板」）：定义文档标签字典与标准文档的分类/作用语义，',
                              '是 04 号五路召回 R2/R3/R4 的词表与语义原料供给侧'),
       `UpdateTime` = NOW(),
       `UpdateBy`   = 'menu_doc_semantic_rule'
 WHERE `Code` = 'MENU_00210' AND `IsDeleted` = 0;

-- ────────────────────────────────────────────────────────────────────────────
-- 验证
-- ────────────────────────────────────────────────────────────────────────────

-- 1. 本菜单已落位（应返回 1 行：文档语义规则 / MENU_00302 / 250）
SELECT `Code`, `MenuName`, `ParentCode`, `OrderNo`, `Url`, `Tag`
FROM `Sys_Menu`
WHERE `Code` = 'MENU_00210' AND `IsDeleted` = 0;

-- 2. 规则定义组完整顺序（应返回 100 NC 规则设计 → 200 报告内容设计 → 250 文档语义规则 → 300 文档提取规则）
SELECT `Code`, `MenuName`, `OrderNo`, `Url`
FROM `Sys_Menu`
WHERE `ParentCode` = 'MENU_00302' AND `IsDeleted` = 0 AND `IsValid` = 1
ORDER BY `OrderNo`;

-- 3. 系统管理组不再含本菜单（应不含「文档语义规则」）
SELECT `Code`, `MenuName`, `OrderNo`
FROM `Sys_Menu`
WHERE `ParentCode` = 'MENU_00303' AND `IsDeleted` = 0 AND `IsValid` = 1
ORDER BY `OrderNo`;

-- 4. 路由契约未变（Url 必须仍是 /business/prompt-template —— R12 双向判据）
SELECT `Code`, `Url` FROM `Sys_Menu`
WHERE `Code` = 'MENU_00210' AND `IsDeleted` = 0
  AND `Url` <> '/business/prompt-template';
-- ↑ 应返回 0 行；返回非 0 说明路由契约被误改，须回滚
