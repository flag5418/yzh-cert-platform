-- =============================================================================
-- 2026-11-03 · 移除失效的专家端菜单入口
--
-- 用户口径：「/audit-task、/audit-evidence 应该是无效的菜单了吧」。
-- 事实：认证检查已改为「认证准备 / 认证检查」两菜单 + 详情页内聚证据挂接，
--       两条旧入口（任务队列视角 / 独立证据挂接）已无业务归属。
--
-- 处置：软删除（IsDeleted=1 + IsValid=0），⛔ 不物理删除（保留审计留痕）。
-- 路由已在 cert-auditor/src/router/index.ts 同步移除，页面文件已删除。
--
-- 幂等：按 Code 定位，重复执行结果一致。
-- =============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

UPDATE sys_menu
SET IsDeleted = 1,
    IsValid = 0,
    UpdateTime = NOW()
WHERE Code IN ('MENU_AUD_19', 'MENU_AUD_20');

-- 验证：应为 2 行 IsDeleted=1，且不再出现在启用清单
SELECT Code, MenuName, Url, IsValid, IsDeleted
FROM sys_menu
WHERE Code IN ('MENU_AUD_19', 'MENU_AUD_20');
