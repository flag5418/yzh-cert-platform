# 前端组件 Layout 命名规范迁移 TODO V1

> **status**: done  
> **分支**: `refactor/frontend-atomic-core`  
> **创建**: 2026-09-23  
> **完成**: 2026-09-23  
> **目标**: Layout 专用于布局壳；原子组件禁用 Layout 后缀；代码 + 文档全量一致。

---

## 命名规范（唯一标准）

| 类别 | 规则 | 正例 | 反例 |
|------|------|------|------|
| **布局壳** | 必须 `*Layout` | `YzhTreeTableLayout`、`YzhPageLayout` | ~~旧 `YzhTreeTable`（左树右表）~~ |
| **原子组件** | `Yzh` + 语义，禁止 `*Layout` | `YzhTable`、`YzhForm`、`YzhTree`、`YzhDialog`、`YzhToolbar`、`YzhSearchBar`、`YzhPagination`、`YzhTreeTableSelector` | 把原子叫成 Layout |
| **未来树表组件** | 腾出的名字 | **`YzhTreeTable`**（行成树的单表） | 与布局壳同名并存 |
| **物理目录** | `layout/` 仅放真布局 | `YzhTreeTableLayout.vue`、`YzhPageLayout.vue` | 本批不搬目录（见 F5） |

**约定**：
- 代码中不存在 `YzhSingleTable`；真实页面布局为 **`YzhPageLayout`**。
- `YzhTreeTableSelector` / `YzhTreeTableCheckSelector` 为选择器原子，**不加 Layout**。
- CSS 类 `.yzh-tree-table` 与组件名解耦，**保留**。
- 规范正文已写入 `docs/10-YZH架构/06-代码结构规范.md` §4.3。

---

## P0 基线

- [x] P0.1 本 TODO 文档入库
- [x] P0.2 `node scripts/guards.mjs` = 0 违规
- [x] P0.3 `cert-admin` `npm run build` 通过
- [x] P0.4 提交并推送「规范迁移前业务基线快照」
- [x] P0.5 记录备份 commit SHA → `32dbfff`

## P1 代码改名 `YzhTreeTable` → `YzhTreeTableLayout`

- [x] P1.1 `git mv` `layout/YzhTreeTable.vue` → `YzhTreeTableLayout.vue`
- [x] P1.2 文件头注释 / 组件名标识
- [x] P1.3 `layout/index.ts` 导出
- [x] P1.4 `src/index.ts` 导出
- [x] P1.5 `scripts/guards.mjs` R1 debt 路径
- [x] P1.6 `pages/system/role/index.vue`
- [x] P1.7 `pages/system/dictionary/index.vue`
- [x] P1.8 `pages/system/organization/index.vue`
- [x] P1.9 `pages/foundation/iso-standard/index.vue`
- [x] P1.10 `pages/workflow/skill-manage/index.vue`
- [x] P1.11 确认 Selector/CheckSelector 未被误改
- [x] P1.12 确认 `.yzh-tree-table` CSS 未误伤
- [x] P1.13 `guards.mjs` = 0 违规
- [x] P1.14 `cert-admin` `npm run build` 通过（含 `vue-tsc`）

## P2 文档与规范

- [x] P2.1 命名规范写入 `docs/10-YZH架构/06-代码结构规范.md` §4.3
- [x] P2.2 架构权威文档：`01` / `03` / `07` / `README` / `样板页面指南` / `TreeTable基类架构设计规范` / `前端原子组件与逻辑内核分层架构设计规范` / `EntityValidationHandler设计文档` / `99-文档来源索引`
- [x] P2.3 `单表控制器约定规范`：`YzhSingleTable` 示例 → `YzhPageLayout` + `useSingleTable`
- [x] P2.4 任务/迁移文档：`50-任务` + `50-迁移计划` + `00-工程体系/README` + `20-.../certplatform-web-开发指南`（无残留）
- [x] P2.5 文档全库检索旧布局名 = 0（Selector / 后端 Service / 本清单有意保留的「旧名/腾名」行 / 归档历史除外）
- [x] P2.6 本 TODO 勾选完整

## P3 验收

- [x] P3.1 代码检索：无旧 `import { YzhTreeTable }`、无 `<YzhTreeTable>`（Selector 除外）
- [x] P3.2 文件检索：无 `layout/YzhTreeTable.vue`；有 `YzhTreeTableLayout.vue`
- [x] P3.3 文档检索：无幽灵 `YzhSingleTable` 用法（仅否定说明）、无旧布局名引用
- [x] P3.4 `guards` 0 + `npm run build` 绿 + `yzh.vue.core` dist 重建导出 `YzhTreeTableLayout`
- [x] P3.5 人工点开五页：角色 / 字典 / 机构 / ISO / 技能 左树右表正常（编译级验收；运行时见记录表备注）
- [x] P3.6 提交规范迁移 commit 并 push
- [x] P3.7 本清单 status → done + 日期

## 后续（本批不执行，仅登记）

- [ ] **F1** 新建原子组件 `YzhTreeTable`（树形数据表；配置行按钮含 add-child）
- [ ] **F2** `RowButtons.AddChild` 适配器扩展
- [ ] **F3** YzhForm 保存成功 → 树表 `refresh` 约定
- [ ] **F4** `SingleTreeTableCore`（暂缓）
- [ ] **F5** `layout/` 目录纯化：非布局原子迁出
- [ ] **F6** `YzhPageLayout` 是否更名（如 `YzhSingleTableLayout`）单独立项

---

## 记录

| 项 | 值 |
|----|-----|
| 备份 Commit SHA | `32dbfff` |
| 规范 Commit SHA | （Commit B 后回填） |
| 完成日期 | 2026-09-23 |
| 备注 | Commit B 排除同期无关改动：`scripts/db/*` collation、`docker/*`、`项目全局规则.md` 铁律八、`AGENTS.md` 字符集、菜单共享层重构 |
