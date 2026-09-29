# 专家任务设计（NC 检查 + 体系认证报告）

> 状态：**设计已定稿，待编码**
> 平台英文名：**CertExpert**（2026-09-29 裁决）
> 领域定位：**工具型辅助系统** —— 输出按标准计算出的固定格式结果，**不按客户定制报表格式**
> 本目录是「专家端任务子系统」的唯一权威源。改任何一处先改这里，再改代码。

---

## 一、为什么有这份文档

专家端截至 2026-09-29 的实现状态：

| 能力 | 配置层 | 执行层 | 结论 |
|---|---|---|---|
| NC 检查规则 | 完整（`cert_validation_rule` + 实体 + 控制器 + 管理端页面） | **无**（仅 `WorkflowTestController` 测试入口） | 规则能配，跑不起来 |
| 报告章节定义 | 完整（`cert_report_template` + `rpt_report_section` + 管理端页面） | **无** | 同上 |
| 专家端任务 | 菜单已通 | `pages/tasks/index.vue` = **52 行假数据空壳** | 0 实现 |
| 数据源 | 完整（`cert_extraction_result` / `cert_table_extraction_result` 已落地） | — | 具备被消费的条件 |

**本文档要回答的唯一问题**：把「专家选企业+阶段 → 补录缺失数据 → 跑队列 → 专家认可 → 导出结果」这条链路，从接口到页面到 DDL 完整设计出来，并明确哪些现有资产复用、哪些改造、哪些废弃。

---

## 二、文档清单与阅读顺序

| 序 | 文档 | 内容 | 读者 |
|---|---|---|---|
| 00 | **本文件** | 索引 · 决策台账 · 术语 | 全部 |
| 01 | [01-总体设计-V1.md](01-总体设计-V1.md) | 业务全景 · 核心概念 · 状态机 · 端到端链路 · 边界 | 全部（**先读这个**） |
| 02 | [02-详细设计-任务与队列-V1.md](02-详细设计-任务与队列-V1.md) | 任务生命周期 · 标准子任务 · 完备性检查 · 队列编排 · 业务锁 | 后端 |
| 03 | [03-详细设计-NC检查-V1.md](03-详细设计-NC检查-V1.md) | 检查项执行 · 结论判定 · 严重度 · NC 记录生成 | 后端 |
| 04 | [04-详细设计-报告生成-V1.md](04-详细设计-报告生成-V1.md) | 章节执行 · 章节结果 · 正文编辑 · 多标准分节 | 后端 |
| 05 | [05-详细设计-补录与日志-V1.md](05-详细设计-补录与日志-V1.md) | 缺口检测算法 · 人工补录写回 · 两类日志 | 后端 |
| 06 | [06-数据库设计-V1.md](06-数据库设计-V1.md) | 全部 DDL · 改造 SQL · 废弃标记 · 验证 SQL | 后端/DBA |
| 07 | [07-接口设计-V1.md](07-接口设计-V1.md) | 端点清单 · 请求响应契约 · 错误码 | 前后端 |
| 08 | [08-页面设计-V1.md](08-页面设计-V1.md) | 5 个专家端页面 + 管理端联动 + 守卫合规 | 前端 |
| 09 | [09-实施计划-V1.md](09-实施计划-V1.md) | S0-S6 分期 · 验收标准 · 风险登记 | 项目管理 |
| 10 | [10-待裁决问题-V1.md](10-待裁决问题-V1.md) | 开放问题（不阻塞编码） | 全部 |

**阅读路径建议**：
- 产品/验收 → 01 → 08 → 09
- 后端 → 01 → 02 → 05 → 06 → 07
- 前端 → 01 → 07 → 08

---

## 三、决策台账（2026-09-29 用户裁决，16 项）

> ⛔ 本节是本设计的**约束来源**。任何与本节冲突的实现均视为缺陷。

| # | 议题 | 裁决 | 影响面 |
|---|---|---|---|
| D01 | 任务表结构 | **单一任务表** `tsk_task`，用 `TaskType` 区分 NC/报告，用 `ScopeType` 区分局部/全局 | 不建 `tsk_nc_task`/`tsk_report_task` |
| D02 | 多标准结构 | **1 个任务 → 自动按标准拆 N 个标准子任务 → N 个队列**。子任务下可挂多个标准的任务 | `tsk_task_standard` |
| D03 | 局部任务约束 | 局部勾选**每次只能提交一个标准**下的项；跨标准勾选必须拦截 | 前端校验 + 后端二次校验 |
| D04 | 完备性不完整的处理 | **软阻塞**：补录完成后再执行；专家**可跳过全部补录** | 不是硬拦截 |
| D05 | 跳过后的行为 | 缺数据的规则**自动不执行**，标记「该规则缺乏必要数据」，**不显示结果** | `tsk_task_item.AutoStatus=skipped` |
| D06 | 补录数据落点 | **不新建补录表**。直接人工编辑 `cert_extraction_result` / `cert_table_extraction_result` | 补录 = 编辑提取结果 |
| D07 | 补录留痕 | 提取结果两张表增加**来源类型**（系统自动计算/人工录入）+ **独立日志表**，一个字段/表格可反复人工修改，每次留痕 | `cert_extraction_change_log` |
| D08 | 追溯粒度 | 文件/字段级追溯**不是本期重点**，归后台工作流设计（规则侧）实现 | 专家端不做字段级下钻 |
| D09 | 导出格式 | **Excel（xlsx）**，一个标准一个 Sheet，固定格式 | 引入 NPOI |
| D10 | 队列 | **新建专家任务专用队列**，不混用 `yzh_queue`（该表有跨租户泄露隐患 H12） | `tsk_task_queue` / `tsk_task_queue_item` |
| D11 | 修改与认可的关系 | **修改即认可**。改完保存即完成认可，日志 Action=MODIFY | 状态直接落 `modified` |
| D12 | 认可强制性 | **所有结果必须经专家手动认可**，必须有认可记录。无认可 = 任务不可批准 | `ReviewStatus` 必落终态 |
| D13 | 符合项落库 | **符合项也留记录**（`audit_checklist_item` 已存在但无实体，本期启用） | 能证明"检了什么" |
| D14 | 报告最终产物 | **不生成 docx、不落 `rpt_audit_report`**。执行完成 = 按标准+章节的列表，专家可接受/修改，**只有导出才是 Excel** | 报告与 NC 检查项数据形态对称 |
| D15 | 业务锁粒度 | **不分任务类型**：`(企业, 阶段)` 上存在任一非终态任务即锁定 | 天然保证"先 NC 后报告" |
| D16 | 阻塞遗留修复 | **两处都修**（本期必修）：R2 阶段口径 FK、R3 铁律九 `IsActive`→`IsValid` | 见 §五 |

### 补充技术裁决

| # | 议题 | 裁决 |
|---|---|---|
| D17 | Excel 库 | **NPOI**（Apache-2.0，无授权风险，支持 xlsx 流式写入） |
| D18 | 术语 | 对外统一「**专家**」/「**专家平台**」；英文名 **CertExpert**。代码标识符 `CertPlatform.*` / `cert-auditor` **本期不改名**（改名影响面过大，另开任务） |
| D19 | 报告章节正文 | 存**纯文本**，前端 **HTML 渲染**展示（转义后换行转 `<br>`），不用富文本编辑器 |
| D20 | 任务追加 | **不支持追加**。任务进入 `running` 后不可新增任务项；要加就建新任务 |
| D21 | 权限 | **不区分制单人/审核人**。注册人即专家系统管理员，默认拥有全部权限 |
| D22 | 规则库范围 | 本期**只做框架与逻辑**。用手工造的少量规则数据跑通全链路；真实规则由实施人员 + 专家在后台配置 |

---

## 四、核心模型一句话

```
任务(tsk_task)  1 ── N  标准子任务(tsk_task_standard)  1 ── 1  队列(tsk_task_queue)
                                                                     │
                                                                     1 ── N  队列项(tsk_task_queue_item)
                                                                                    │
任务(tsk_task)  1 ── N  ★任务项(tsk_task_item)★  ← 统一表：NC检查项 与 报告章节 都在这里
                              │            │
                              1:N          1:N
                     tsk_task_item_log  tsk_task_data_gap
                    （认可/修改日志）   （补录清单 → 写回 cert_extraction_result）
```

**★ 最重要的设计判断**：NC 检查项和报告章节在数据形态上**完全对称** —— 都是「自动算出一个结果 → 专家认可或改 → 留日志 → 可导出」。因此用**一张 `tsk_task_item` 统一表**承载，两者只在 3 个标量列上有差异（`Conformity` / `Severity` / `ContentText`），其余完全共用。这让认可、修改、日志、导出、列表混排全部只写一遍。

---

## 五、存量资产处置（复用 / 改造 / 废弃）

### 5.1 直接复用（不动）

| 资产 | 路径 / 表名 | 复用方式 |
|---|---|---|
| 工作流执行引擎 | `CertPlatform.Admin/Services/Workflow/`（`WorkflowInterpreter` / `NodeExecutor` / `AiNodeExecutor` / `WorkflowConfigParser` / `CertSkillRegistry`） | **执行引擎地基**，本期只补正式 `run` 端点（`WorkflowTestController.cs:68` 硬编码 `TaskType="TEST"` 需放开） |
| `wf_execution_task` / `wf_execution_task_item` | 表已存在、实体已存在 | 执行过程留痕载体，`TaskType` 已预留 `NC_CHECK` / `REPORT_GENERATE` |
| NC 规则 | `cert_validation_rule` + `ValidationRule.cs` + `ValidationRuleController` | 规则来源 |
| 报告章节定义 | `rpt_report_section` + `ReportSection.cs` | 章节来源 |
| 提取结果 | `cert_extraction_result` / `cert_table_extraction_result` | **唯一数据源**，补录直接改这两张表 |
| 不符合项 | `audit_nonconformity` | **启用**（已有 `SourceType`/`Severity`/`RuleCode`/`Description` 等完整字段） |
| 企业-阶段-标准三元组 | `cert_enterprise_stage` | 任务范围解析的输入 |
| 阶段主数据 | `cert_cert_stage` | **权威阶段表**（见 R2 修复） |

### 5.2 必改（本期，阻塞项）

| # | 对象 | 改什么 | 为什么 |
|---|---|---|---|
| R2 | `cert_validation_rule.PhaseCode` FK | `cert_phase_definition`（**0 行的孤儿表**）→ `cert_cert_stage` | 阶段口径双轨。`cert_enterprise_stage.StageCode` 关联的是 `cert_cert_stage.Code`，规则表指向另一张表会导致规则**配不出来、也查不出来** |
| R2 | `cert_report_template.PhaseCode` FK | 同上 | 同上 |
| R2 | `cert_org_stage.StageCode` 口径核对 | 确认关联 `cert_cert_stage.StageCode`（业务码）与 `cert_enterprise_stage` 的 `Code` 混用问题 | 注释与代码已脱节 |
| R3 | `cert_validation_rule.IsActive` | → `IsValid`（int，0/1） | 铁律九：`Enable` 零容忍，启用/禁用唯一字段 = `IsValid` |
| R3 | `rpt_report_section.IsActive` | → `IsValid`（当前该表**根本没有** `IsValid` 列，`enable` 已被 DROP） | 同上；且前端 `types/cert.ts:437` 已声明 `IsValid` 但后端实体没实现 → 契约不一致 |
| R3 | `ValidationRule.json:4` | `"EnableField": "IsActive"` → `"IsValid"` | 名字保留、值必须纠正（AGENTS.md 铁律九） |
| 修复 | `cert_table_extraction_result` | 补 `IsManualEdited` + `ValueSource` | 字段级表有、表格级表缺 → **不对称**，补录留痕会漏 |
| 修复 | `audit_checklist_item.TaskCode` FK | `audit_task` → `tsk_task` | 任务表统一后指向新表 |
| 修复 | `audit_nonconformity.TaskCode` FK | `audit_task` → `tsk_task` | 同上 |
| 修复 | `audit_nonconformity.SourceCheckCode` FK | 指向已删除的 `ent_file_compliance_check`（悬挂 FK） | 改指 `tsk_task_item.Code` |

### 5.3 废弃（加注释标记，不删）

| 表 | 理由 |
|---|---|
| `audit_task` | 任务表统一到 `tsk_task`（D01） |
| `rpt_report_task` | 同上 |
| `rpt_audit_report` | D14：报告产物只导出 Excel，不落报告实例 |
| `rpt_report_section_source` | D08：字段级追溯归后台工作流设计 |
| `cert_validation_rule_source` | 同上 |
| `cert_phase_definition` | R2 修复后无 FK 指向它 → 彻底孤儿 |
| `yzh_queue` / `yzh_queue_task` | D10：专家任务用专用队列；此表继续服务文档转换/提取 |

> 全部标 `-- DEPRECATED 2026-09-29` 注释，**不执行 DROP**（保留历史数据可追溯性）。

### 5.4 新建依赖

| 依赖 | 版本 | 用途 | 许可 |
|---|---|---|---|
| `NPOI` | 2.7.x | xlsx 导出 | Apache-2.0 |

> 项目当前**无任何 Excel 依赖**（`CertPlatform.Shared.csproj` 只有 `Newtonsoft.Json` + `Minio`）。

---

## 六、与既有文档的关系

| 既有文档 | 关系 |
|---|---|
| `docs/20-体系认证/05-业务知识库/ISO体系认证NC与报告标准约束-V1.md` | **业务依据**（NC 四要素、报告七要素、NC 生命周期）。本设计是它的系统实现 |
| `docs/40-实施/企业资料管理/09-内容提取与队列管理-执行方案-V1.md` | **上游**。该方案 D20 明确「本次不动 NC/报告，只提供 `GetActiveAsync` 读取契约」—— 本设计就是那个消费方 |
| `docs/40-实施/企业资料管理/10-执行计划-V1.md` | **上游**。其中 `EnterpriseExtractionResultStore`（`ArchiveAsync`/`RehydrateVersionAsync`/`GetActiveAsync`）是本期补录写回的**正确落点**，不要绕过 |
| `docs/50-任务/开发计划/体系认证专家系统建设计划-V4.md` | **战略**。§4.7 规划的 `POST api/AuditTask/runNc` / `POST api/ReportTask/run` 在本设计中改为统一任务模型，接口清单见 07 |
| `docs/10-YZH架构/23-前后端信封统一改造计划-V1.md` | **规范**。所有新端点的返回信封必须遵守 |
| `docs/50-任务/迁移计划/NC规则设计迁移方案-V1.md` | **历史参考**。NC 规则字段的历史设计出处 |
| `src/old/**` | ⛔ **禁止参考、禁止修改** |

---

## 七、关键术语

| 中文 | 英文 / 标识 | 说明 |
|---|---|---|
| 专家平台 | CertExpert | 本系统。代码标识符暂仍为 `CertPlatform` / `cert-auditor`（D18） |
| 企业 | Enterprise | 受审核组织。表 `cert_enterprise`，租户键 `OrgCode` = 专家工作区 |
| 阶段 | Stage | `cert_cert_stage`。`Code`（GUID 业务键）与 `StageCode`（业务码如 `S1`）**双码**，本设计统一用 `Code` |
| 标准 | Standard | `cert_iso_standard` |
| 任务 | Task | 专家对某企业某阶段发起的一次 NC 检查或报告生成。表 `tsk_task` |
| 全局任务 | `ScopeType=FULL` | 该阶段该标准下**全部**检查项/章节 |
| 局部任务 | `ScopeType=PARTIAL` | 专家**勾选的部分**项。一次只能一个标准（D03） |
| 标准子任务 | SubTask | 任务按标准拆分后的单元，1 子任务 = 1 队列（D02） |
| 任务项 | TaskItem | ★ 统一表 `tsk_task_item`。`ItemType=nc_check` 或 `report_section` |
| 完备性检查 | DataCheck | 校验规则所需字段/表格是否都已提取（D04） |
| 数据缺口 | DataGap | 完备性检查的产物 = **补录清单**。表 `tsk_task_data_gap` |
| 跳过 | Skip | 专家放弃补录，缺数据的规则不执行、不显示结果（D05） |
| 认可 | Acknowledge | 专家确认自动结果正确。`ReviewStatus=acknowledged`（D12 强制） |
| 修改 | Modify | 专家改了结果。**修改即认可**（D11），`ReviewStatus=modified` |
| 业务锁 | BizLock | `(企业, 阶段)` 有非终态任务时，禁建任务 + 禁文件操作（D15） |
| 数据源 | Source | `cert_extraction_result`（字段级）/ `cert_table_extraction_result`（表格级） |

---

## 八、编码前置（AGENTS.md 强制）

动手前必读：

1. `AGENTS.md` 三条铁律：PascalCase 三处一致 / 后端继承基类 / `Enable` 零容忍
2. `docs/10-YZH架构/样板页面指南-V1.md` —— 本期 5 个新页面全部照抄 `yzh.vue.core/src/pages/system/user/` 骨架，**零手写 CRUD**
3. 改完必跑：
   ```bash
   cd src/certplatform-web && node scripts/guards.mjs   # 0 违规（含 R12 路由↔菜单）
   ```
   再跑对应端 `npm run build`（含 `vue-tsc`）。
4. **动了路由或菜单 → 额外跑** `./scripts/db/verify/sync_menu_urls.sh` 刷新菜单快照

---

## 九、变更记录

| 日期 | 版本 | 变更 |
|---|---|---|
| 2026-09-29 | V1 | 首版。16+6 项用户裁决全部落章，10 篇文档成型 |
