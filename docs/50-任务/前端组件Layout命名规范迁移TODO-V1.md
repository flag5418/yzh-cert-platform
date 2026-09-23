# 前端组件 Layout 命名规范迁移 TODO V1

> **status**: living  
> **分支**: `refactor/frontend-atomic-core`  
> **创建**: 2026-09-23  
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

---

## P0 基线

- [ ] P0.1 本 TODO 文档入库
- [ ] P0.2 `node scripts/guards.mjs` = 0 违规
- [ ] P0.3 `cert-admin` `npm run build` 通过
- [ ] P0.4 提交并推送「规范迁移前业务基线快照」
- [ ] P0.5 记录备份 commit SHA

## P1 代码改名 `YzhTreeTable` → `YzhTreeTableLayout`

- [ ] P1.1 `git mv` `layout/YzhTreeTable.vue` → `YzhTreeTableLayout.vue`
- [ ] P1.2 文件头注释 / 组件名标识
- [ ] P1.3 `layout/index.ts` 导出
- [ ] P1.4 `src/index.ts` 导出
- [ ] P1.5 `scripts/guards.mjs` R1 debt 路径
- [ ] P1.6 `pages/system/role/index.vue`
- [ ] P1.7 `pages/system/dictionary/index.vue`
- [ ] P1.8 `pages/system/organization/index.vue`
- [ ] P1.9 `pages/foundation/iso-standard/index.vue`
- [ ] P1.10 `pages/workflow/skill-manage/index.vue`
- [ ] P1.11 确认 Selector/CheckSelector 未被误改
- [ ] P1.12 确认 `.yzh-tree-table` CSS 未误伤
- [ ] P1.13 `guards.mjs` = 0 违规
- [ ] P1.14 `cert-admin` `npm run build` 通过

## P2 文档与规范

- [ ] P2.1 命名规范写入 `docs/10-YZH架构/06-代码结构规范.md`（或原子组件规范）
- [ ] P2.2 架构权威文档：`01` / `03` / `07` / `README` / `样板页面指南` / `TreeTable基类架构设计规范` / `前端原子组件与逻辑内核分层架构设计规范` / `EntityValidationHandler设计文档` / `99-文档来源索引`
- [ ] P2.3 `单表控制器约定规范`：`YzhSingleTable` 示例 → `YzhPageLayout`
- [ ] P2.4 任务/迁移文档：`50-任务` + `50-迁移计划` + `00-工程体系/README` + `20-.../certplatform-web-开发指南`
- [ ] P2.5 文档全库检索旧布局名 = 0（Selector / 后端 Service 除外）
- [ ] P2.6 本 TODO 勾选完整

## P3 验收

- [ ] P3.1 代码检索：无旧 `import { YzhTreeTable }`、无 `<YzhTreeTable>`（Selector 除外）
- [ ] P3.2 文件检索：无 `layout/YzhTreeTable.vue`；有 `YzhTreeTableLayout.vue`
- [ ] P3.3 文档检索：无幽灵 `YzhSingleTable`、无旧布局名
- [ ] P3.4 `guards` 0 + `npm run build` 绿
- [ ] P3.5 人工点开五页：角色 / 字典 / 机构 / ISO / 技能 左树右表正常
- [ ] P3.6 提交规范迁移 commit 并 push
- [ ] P3.7 本清单 status → done + 日期

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
| 备份 Commit SHA | （执行后填写） |
| 规范 Commit SHA | （执行后填写） |
| 完成日期 | |
