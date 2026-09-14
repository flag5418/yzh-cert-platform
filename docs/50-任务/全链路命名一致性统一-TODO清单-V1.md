# 全链路命名一致性统一 - TODO 清单-V1

> 创建时间：2026-09-13
> 关联方案：`docs/50-任务/全链路命名一致性统一方案-V1.md`
> 状态：⚠️ **Phase 1 / Phase 2 已作废（前提不成立，且执行会造成破坏性改名）；TS 偏移与 EF→SqlSugar 部分仍有效**

---

## ⚠️ 00. 作废声明（2026-09-13，`information_schema` 实测）

**Phase 1 与 Phase 2 全部取消。**

原因：数据库中**根本不存在**待迁移的 snake_case 业务列。实测结果：

| 表 | PascalCase 列数 / 总列数 | 含下划线列 |
|----|:--:|:--:|
| `cert_certification_body` | 29 / 29 | 0 |
| `cert_iso_standard` | 22 / 22 | 0 |
| `audit_task` | 21 / 21 | 0 |
| `ent_enterprise` | 29 / 29 | 0 |
| `Sys_Organization` | 24 / 24 | 0 |
| `Sys_User` | 42 / 44 | 5（旧框架遗留） |

执行下方 Phase 2 的 `ALTER TABLE ... CHANGE COLUMN` 会**破坏现有已正确的表结构**，
违反《项目全局规则》**§16.9 铁律七**（已存在的 PascalCase 列名禁止二次改名）。

**仍建议执行的部分**：后续 Phase（TS 语义偏移修正、EF Core → SqlSugar 迁移、JSON `FieldName` 对齐）
——这些与列名改不改无关，仍是真实的债。

---

## ~~Phase 1：确认方案 + 逐表列名清单（0.5 天）~~ ❌ 作废

- [x] 1.1 确认统一风格为 PascalCase
- [x] 1.2 逐表列出所有需改的 DB 列名 → **实测无一需改**（见 §00）
- [ ] 1.3 逐表确认 C# 实体迁移范围（EF Core → SqlSugar）  ← 仍有效
- [ ] 1.4 逐表确认 TS 接口修正范围（语义偏移）  ← 仍有效
- [ ] 1.5 评审方案，确认无遗漏

---

## ~~Phase 2：DB DDL 迁移脚本（0.5 天）~~ ❌ 作废（禁止执行）

### ~~2.1 cert_iso_standard~~ — 列名已正确，无需重命名
- [x] 2.1.3 验证：实测 22/22 列均为 PascalCase ✅

### ~~2.2 cert_phase_definition~~ — 见 §00

### ~~2.3 cert_certification_body~~ — 列名已正确，无需重命名
- [x] 2.3.3 验证：实测 29/29 列均为 PascalCase ✅（认证机构模块实测报告见 `认证机构管理-开发计划-V1.md` §3.3）

### ~~2.4 audit_task~~ — 列名已正确，无需重命名
- [x] 2.4.3 验证：实测 21/21 列均为 PascalCase ✅

### ~~2.5 cert_enterprise~~ — 列名已正确，无需重命名
- [x] 2.5.3 验证：实测 29/29 列均为 PascalCase ✅

### ~~2.6 cert_auditor_profile~~ — 见 §00

### ~~2.7 视图重建~~ — 无列名变更，无需重建

> 唯一可选的历史债务：`Sys_User` 的 `User_Id` / `Role_Id` / `Dept_Id` / `wechat_openid` / `wechat_unionid`。
> 属旧框架表，需单独评估影响面，**不在本清单范围**。

### 2.8 DDL 脚本归档
- [ ] 2.8.1 合并所有 ALTER 脚本到 `scripts/db/rename_columns_pascalcase.sql`
- [ ] 2.8.2 添加注释和回滚说明

---

## Phase 3：C# 实体迁移（EF Core → SqlSugar）（0.5 天）

### 3.1 PhaseDefinition
- [ ] 3.1.1 `[Table]` → `[SugarTable]`，`EntityBase` → `BaseEntity`
- [ ] 3.1.2 删除所有 `[Column("xxx")]` 映射
- [ ] 3.1.3 确认属性名 = DB 列名（PascalCase）

### 3.2 CertificationBody
- [ ] 3.2.1 `[Table]` → `[SugarTable]`，`EntityBase` → `BaseEntity`
- [ ] 3.2.2 删除所有 `[Column("xxx")]` 映射
- [ ] 3.2.3 确认属性名 = DB 列名（PascalCase）

### 3.3 AuditTask
- [ ] 3.3.1 `[Table]` → `[SugarTable]`，`EntityBase` → `BaseEntity`
- [ ] 3.3.2 删除所有 `[Column("xxx")]` 映射
- [ ] 3.3.3 确认属性名 = DB 列名（PascalCase）

### 3.4 Enterprise
- [ ] 3.4.1 `[Table]` → `[SugarTable]`，`EntityBase` → `BaseEntity`
- [ ] 3.4.2 删除所有 `[Column("xxx")]` 映射
- [ ] 3.4.3 确认属性名 = DB 列名（PascalCase）

### 3.5 AuditorProfile（如适用）
- [ ] 3.5.1 检查并迁移

### 3.6 ISOStandard（清理）
- [ ] 3.6.1 删除不必要的 `[SugarColumn(ColumnName)]` 映射（列名已与属性名一致）

### 3.7 清理 Vol 兼容层
- [ ] 3.7.1 检查 `YzhCompatShims.cs` 中 `EntityBase` 的 `[NotMapped]` 默认值
- [ ] 3.7.2 确认迁移后不再需要 Vol 兼容

---

## Phase 4：TS 接口修正 + JSON 同步（0.5 天）

### 4.1 CertificationBody TS 修正
- [ ] 4.1.1 `OrgName` → `Name`
- [ ] 4.1.2 `OrgShortName` → `ShortName`
- [ ] 4.1.3 `OrgStatus` → `Status`
- [ ] 4.1.4 `CreateDate` → `CreateTime`

### 4.2 AuditTask TS 修正
- [ ] 4.2.1 `TaskNo` → `TaskNumber`
- [ ] 4.2.2 `AuditDate` → `PlannedDate`

### 4.3 Enterprise TS 修正
- [ ] 4.3.1 `EntCode` → `Code`
- [ ] 4.3.2 `EntName` → `Name`
- [ ] 4.3.3 `EntStatus` → `Status`
- [ ] 4.3.4 `OrgId` → `OrgCode`
- [ ] 4.3.5 `CreateDate` → `CreateTime`

### 4.4 ISOClause TS 修正
- [ ] 4.4.1 `CreateDate` → `CreateTime`（如存在）

### 4.5 JSON 配置同步
- [ ] 4.5.1 检查所有 EntityConfig JSON 的 FieldName 与 C# 属性名一致
- [ ] 4.5.2 修正不一致项

### 4.6 前端页面同步
- [ ] 4.6.1 检查所有页面的 `formatter`、`prop` 引用是否与新字段名一致
- [ ] 4.6.2 修正不一致项

---

## Phase 5：构建验证 + 回归测试（0.5 天）

### 5.1 后端构建
- [ ] 5.1.1 `dotnet build` 0 错误
- [ ] 5.1.2 重启后端

### 5.2 API 验证
- [ ] 5.2.1 认证阶段管理 CRUD
- [ ] 5.2.2 ISO 标准管理 CRUD
- [ ] 5.2.3 标准条款管理 CRUD
- [ ] 5.2.4 阶段定义管理 CRUD
- [ ] 5.2.5 认证机构管理 CRUD
- [ ] 5.2.6 系统参数配置 CRUD

### 5.3 前端页面验证
- [ ] 5.3.1 每个页面：列表加载正常
- [ ] 5.3.2 每个页面：搜索功能正常
- [ ] 5.3.3 每个页面：新增功能正常
- [ ] 5.3.4 每个页面：编辑功能正常
- [ ] 5.3.5 每个页面：删除功能正常
- [ ] 5.3.6 每个页面：字典翻译正常

### 5.4 文档更新
- [ ] 5.4.1 更新 `项目全局规则.md` 中的命名规范
- [ ] 5.4.2 更新 `docs/30-项目规则/知识库/` 相关条目
- [ ] 5.4.3 标记本 TODO 为已完成

---

## 完成确认

- [ ] 所有 Phase 1-5 任务完成
- [ ] 全部页面回归测试通过
- [ ] 文档已更新
- [ ] 方案文档状态更新为「已完成」
