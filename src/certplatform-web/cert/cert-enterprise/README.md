# cert-enterprise（企业端）— ⛔ 未开工

> **状态：仅占位。** 当前只有 `package.json` / `tsconfig.json` / `src/index.ts`（一个占位常量），
> **缺 `index.html` 与 `vite.config.ts`** → `vite dev` / `vite build` 结构性不可能成功。

## 为什么保留这个目录

- 三端（admin / auditor / enterprise）**同构**是本项目的结构契约
  （见 `docs/10-YZH架构/21-项目结构设计规范-V1.md` §五）。
- 保留占位，使「新增一个角色端」的 checklist 有现成参照物，而不是从零想起。

## 当前它不在任何构建链上

| 项 | 状态 |
|---|---|
| `build:all` | = `build:core && build:share && build:admin && build:auditor` —— **不含 enterprise** |
| 根 `dev:enterprise` | **已移除**（2026-09-24）—— 必然失败的命令等于噪音，开工时再加回 |
| `workspaces: ["cert/*"]` | 通配内 → npm 会装它的依赖，但**不会被构建** |

## 开工时要做（照 `21-项目结构设计规范-V1.md` §六 checklist）

1. `index.html` —— 照抄 `cert-auditor/index.html`
2. `vite.config.ts` —— 照抄 `cert-auditor/vite.config.ts`，改 `port` 与 `cacheDir`
3. `src/{App.vue,main.ts,vite-env.d.ts}` —— 照抄 auditor（**逐字相同是预期**，不是重复代码问题）
4. `src/router/index.ts` —— 用 `createYzhRoutes({ menuTag: 'enterprise', ... })`
5. `src/store/auth.ts` —— 照抄 auditor
6. 根 `package.json` 加回 `dev:enterprise`，并把 `build:enterprise` 纳入 `build:all`
7. ⚠️ **登记守卫 roots**：`scripts/guards.mjs` 的 `PAGE_ROOTS` / `API_ROOTS` / `CORE_APP_ROOTS`
   —— **不登记 = 新端处于守卫盲区**（静默不检查，`walk()` 对不存在目录返回空数组）
8. ⚠️ **补 `Sys_Menu` 的 enterprise 菜单**（`Tag = 'enterprise'`）
   —— 否则守卫 **R12** 会报「路由无菜单入口（孤儿路由）」
9. 跑 `./scripts/db/verify/sync_menu_urls.sh` 刷新菜单快照，再 `node scripts/guards.mjs`
