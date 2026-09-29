# 16 — 模板标注与 Skill 流程化方案（V1 探索稿 · 已归档）

> **📦 归档声明（2026-09-29）**：本稿为**探索过程记录**，内容**已被 [`16-模板标注与Skill流程化方案-V2.md`](./16-模板标注与Skill流程化方案-V2.md) 取代**。V2 对 6 处骨架做了实质修订（流程粒度 / 模板层统一 / 锚点分层 / 阶段 A·B 并发分段 / 画布降级 / 规范体系），并补齐了 4 张表 DDL、模板设计规范、隐患台账。
> **保留价值**：§1 问题定义、§2 复用清单、§11.2 参考项目反面证据、§11.3 外部调研结论**仍然有效**，V2 已引用。
> ⚠️ **不得作为编码依据**。请以 V2 为准。

---

> **版本**：V1 | **日期**：2026-09-29 | **状态**：📦 **已归档 — 过程记录**
> **性质**：本册 00–15 之外新增分册，登记「Word/Excel 模板落位与赋值」的**原创设计方向**。
> **⚠️ 强制声明**：本文件**不含实施规格**，**不得作为编码依据**。文末 §9「待讨论议题」全部关闭、§10「决策登记」拍板后，方可另出 `-V2` 实施分册。
> **作者立场**：本模块无成熟范式可抄，poi-tl / docxtemplater / JasperReports 只在「模板打标 + 通用引擎」这一层同构，**坐标自解析、标记样式自验收、取值/落笔分层**三处为原创，尚未经工程验证。
> **上位文档**：`05-标准文档契约模型-V1.md`、`14-需求深化讨论收敛与增补设计-V1.md`、`docs/30-项目规则/Skill清单-V1.md`、`AGENTS.md`

---

## 目录

1. [背景与问题定义](#1-背景与问题定义)
2. [已有资产盘点（复用清单）](#2-已有资产盘点复用清单)
3. [三条既定架构确认（不重新论证）](#3-三条既定架构确认不重新论证)
4. [分层裁决：取值层 vs 落笔层](#4-分层裁决取值层-vs-落笔层)
5. [整体方案草图](#5-整体方案草图)
6. [原创部分逐项分析](#6-原创部分逐项分析)
7. [重大隐患登记](#7-重大隐患登记)
8. [待讨论议题（分批）](#8-待讨论议题分批)
9. [决策登记](#9-决策登记)
10. [与既有分册的修订清单](#10-与既有分册的修订清单)
11. [证据索引](#11-证据索引)

---

## 1. 背景与问题定义

### 1.1 问题的真实形态

用户原始诉求：「如何通过 web 页面设计，每个 word 或 excel，设置单元格，页眉，页脚，或单元格的公式」。

盘点后确认，这不是一个"页面设计问题"，而是**三个缺口叠加**：

| 缺口 | 册内设计现状 | 代码现状 | 证据 |
|---|---|---|---|
| **① 模板结构解析** | 无设计 | `DocumentConvertClient` 只出 Markdown + PDF，**不产出坐标/合并区/页眉页脚分区** | `CertPlatform.Shared/DocExtraction/DocumentConvertClient.cs` |
| **② 落位元数据** | `14` 号 D18 一句话，3 个 type（`cell`/`table`/`placeholder`），**无页眉页脚/无公式/无合并区/无重复区** | `FillTarget`/`CellPrompt` 列**均不存在** | `14-…:184-190`；`Entities/Doc/DocFieldDef.cs`（12 字段，无这三列） |
| **③ 执行层** | D18 规划 `StandardDocInstanceService` | **全仓无 NPOI/EPPlus/OpenXml 包**；`IExcelService` 唯一实现 `NullExcelService` 三个方法全 `NotImplementedException` | `src/yzh-core/YZH.Core.Stand/Interfaces/IExcelService.cs:53-60` |

### 1.2 前置问题的回答：需不需要在线编辑器

联网调研结论（2026-09-29）：

| 候选 | 授权 | 读 xlsx/docx | 公式 | 判定 |
|---|---|---|---|---|
| **Univer OSS** `@univerjs/*` 1.0.2 / MIT | MIT | ❌ **Pro 付费功能** | ✅ 500+ 函数 | 底层 import/export 在私有仓库 `dream-num/univer-rs` |
| Univer Pro | 商业 | ✅ | ✅ | 全能但需询价 |
| **ONLYOFFICE Docs Community** | AGPLv3 / 免费 | ✅ | ✅ | **「up to 20 users recommended」+ AGPL 传染** |
| `@vue-office/excel` 1.7.14 / MIT | MIT | ✅ 预览 | ❌ | 丢合并区/公式，只能"看"不能"配" |
| Handsontable | 商业 | ❌ | 400 函数（**付费特性**） | 不兼容 Office 格式 |
| luckysheet 2.1.13 | MIT | ✅ | ✅ | **GitHub 仓库 404，已废弃** |
| x-data-spreadsheet 1.1.5 | MIT | ❌ | ❌ | Vue2 时代产物，npm 停更 |

**结论：开源界不存在同时满足「能读模板 + 能配落位 + 可商用 + 不拖编辑器」四条的组件。** 本方案走**自建配置面**路线，不引入 Office 编辑器。

---

## 2. 已有资产盘点（复用清单）

> 原则：**能复用的一律复用**。本方案的真实成本不在"新建"，在"接线"。

| 能力 | 现状 | 位置 | 动作 |
|---|---|---|---|
| DAG 编排 | ✅ `WorkflowConfig{Nodes,Edges,WorkflowType}` | `Services/Workflow/Models/WorkflowConfig.cs` | **复用** |
| 四层执行模型 | ✅ `wf_execution_task` → `_item` → `wf_path_execution` → `wf_node_execution` | `Entities/Wf/WfPathExecution.cs:11-25`（注释即为权威说明） | **复用，零新表** |
| 任务类型枚举 | ✅ `TEST` / `NC_CHECK` / `REPORT_GENERATE` | `Models/TaskExecutionModels.cs:12` | **加第 4 个枚举值** |
| 节点执行器 | ✅ `NodeExecutor`（`start`/`end`/`branch`/`skill`）+ `AiNodeExecutor` | `Services/Workflow/NodeExecutor.cs:85-100` | **复用，加 nodeType 分支** |
| Skill 反射注册 | ✅ `[Skill]` + `ExecuteAsync` + `wf_skill_reflection` | `Skills/SkillExecutor.cs` | **复用** |
| `[FromService]` DI 注入 | ✅ `_serviceProvider.GetService(p.ParameterType)` | `SkillExecutor.cs:203-205` | **复用（关键，见 §3.3）** |
| 队列 + 每任务 Scope | ✅ `CreateScope()` 模式 | `OfficeConvertTaskExecutor.cs:46`、`EnterpriseExtractTaskExecutor.cs:59` | **照抄** |
| 单节点试跑 | ✅ `POST /test/node`（落库可回溯） | `Controllers/Workflow/WorkflowTestController.cs:91` | **复用** |
| 节点审批 | ✅ `POST /approve`（含 `Confidence` 评分 + `ManualResult` 人工改值） | 同上 `:362` + `Models/TaskExecutionModels.cs:170-185` | **复用** |
| 数据源取值 | ⚠️ `GetFieldSkill`/`GetTableSkill` **代码存在、已从 DB 移除注册** | `Skills/GetFieldSkill.cs`；`Skill清单 §11` 明确"保留代码" | **恢复注册** |
| 纯函数 Skill | ✅ 2 个（`compare`/`assemble`） | `Skill清单 §6.1` | **扩若干** |
| 全局参数 | ⚠️ `cert_global_param_def/instance` 设计有、**零实现** | `14` 号 D28 | **P0 建表** |
| 前端画布 | ✅ `nc-config` 页面 + `@logicflow/core` 已装 + `cert-share/src/components/workflow` | — | **复用组件** |
| 上传底座 | ✅ 分片 init/confirm/cancel | `StandardDirectoryController` | **复用** |
| 转换链 | ✅ `anydoc`→Markdown / `LibreOffice`→PDF（docker 容器已在） | `DocumentConvertClient.cs` | **复用（兼作公式重算兜底）** |
| 报告模板存储 | ⚠️ `cert_report_template` 有 `TemplateFilePath` + `SectionConfig`，**零生成代码** | `Entities/Cert/ReportTemplate.cs` | 加 `ScanCode` 列后复用 |
| Office 库 | 🔴 **零** | — | **引入 NPOI（Apache-2.0）** |
| 模板扫描 | 🔴 零 | — | **新建** |
| 文档写入 | 🔴 零 | — | **新建** |

**净新建清单（预估）**：

| 类型 | 内容 |
|---|---|
| NuGet | `NPOI` 1 个包 |
| 接口 | `IWorkbookContext` + `IExcelDoc` + `IWordDoc` |
| 实现 | `NpoiExcelContext` / `NpoiWordContext`（Scoped 注册 1 行） |
| Service | `TemplateScanService`（扫描）+ `TemplateGenerateTaskExecutor`（队列） |
| Skill | 8 新增 + 2 恢复注册 |
| 表 | 3 张（`cert_doc_template_scan` / `cert_doc_template_part` / `cert_doc_fill_plan`） |
| 引擎改动 | **0 行**（`TaskType` 加 1 个枚举值） |
| 前端 | 1 个结构树页 + 复用 logicflow 画布 |

---

## 3. 三条既定架构确认（不重新论证）

> 本节结论**已有代码/文档证据**，不列入 §8 待讨论范围。

### 3.1 D3 两条链分离 → 填充阶段完全不打开企业文档

`03-业务流程与状态机` **D3**（README `:98` 已拍板）：画像链（无规则、一次性）与定向链（有规则、按契约）分离，互不混用。

**落成可执行结构**：

```
┌─ 取值阶段（每份文档跑一次，AI 为主）──────────────────┐
│  企业文档 → anydoc → Markdown → LLM → 字段/表格       │
│     ↓ 落库                                            │
│  cert_extraction_result          (LabelTag=FieldCode)  │
│  cert_table_extraction_result                          │
│  cert_ent_doc_profile            (画像，仅用于匹配)     │
└───────────────────────────────────────────────────────┘
                    ▼ 断点：企业文档不再被读
┌─ 填充阶段（每份标准文档跑一次，纯本地）────────────────┐
│  cert_extraction_result  +  FieldOverrideJson          │
│  cert_global_param_instance                            │
│           ↓ 合并视图（人工覆盖优先）                    │
│      值 → 坐标 → NPOI → docx/xlsx                      │
└───────────────────────────────────────────────────────┘
```

**三个推论**：

| 推论 | 价值 | 待议 |
|---|---|---|
| `MarkdownPath` 填充阶段不读 | 可设保留期清理，MinIO 成本降一个量级 | §8-B3 |
| **模板坐标与字段值完全解耦** | 换模板 = 只重跑扫描；值有错 = 只重跑提取。**互不牵连** | 已成立 |
| 重算不碰原始文件 | 已归档标准文档实例不受企业文件变更影响（`cert_doc_match_decision` 只追加，守住 D9） | 已成立 |

> 对齐键：`README :82` 已锁定 `LabelTag = FieldCode`，是本方案字段侧唯一权威。

### 3.2 四层执行模型 → 零新表承载「一个单元格一个流程」

`WfPathExecution.cs:11-25` 的注释即权威：

```
wf_execution_task          一次触发（TEST / NC_CHECK / REPORT_GENERATE）
  └─ wf_execution_task_item   一个检查项
       └─ wf_path_execution      一条路径
            └─ wf_node_execution     一个节点
```

**一份 50 字段文档的落位分布**：

| 层 | 表 | 数量 | 语义 |
|---|---|---|---|
| 一次触发 | `wf_execution_task` | **1** | 生成这一份文档（`TaskType=TEMPLATE_GENERATE`） |
| **一个落位项** | `wf_execution_task_item` | **50** | 一个单元格 / 一个表格 |
| 一条取值路径 | `wf_path_execution` | 每项 1–3 条 | 多来源竞争（营业执照 vs 章程 vs 手动） |
| 一个节点 | `wf_node_execution` | 每路径 3–5 个 | 取数 → 计算 → 格式化 → 写入 |

**每层已有独立执行记录、独立失败重试、独立审批。** 多路径天然承接 `14` 号 D25 `AggregateRule` 的三种 mode（`priority` / `concat` / `max_confidence`）。

> ⚠️ **队列粒度 = 一个 Task = 一份文档**，对应"逐个文档生成"。Item 层不做队列。

### 3.3 `[FromService]` + Scoped → 有状态文档 Skill 可行（已验证）

**本条曾一度被判定为"不可行"，经代码验证推翻。**

证据链：

```csharp
// SkillExecutor.cs:203-205  —— 不是全局单例，是按请求解析
if (p.GetCustomAttribute<FromServiceAttribute>() != null)
{
    args[i] = _serviceProvider.GetService(p.ParameterType);
}
```

```csharp
// CertPlatformAdminServiceExtensions.cs:42,51  —— 两者都是 Scoped
services.AddScoped<SkillExecutor>();
services.AddScoped<WfExecutionTaskService>();
```

```csharp
// OfficeConvertTaskExecutor.cs:46  —— 队列任务每任务建 Scope（既有模式）
using var scope = _serviceProvider.CreateScope();
var convertService = scope.ServiceProvider.GetRequiredService<OfficeConvertService>();
```

**推导**：`IWorkbookContext` 注册为 `Scoped` → 每个队列任务一个实例 → 静态 Skill 通过 `[FromService] IWorkbookContext doc = null!` 拿到**本次执行那一份**文档句柄。

**结论：Skill 契约（`public static class` + `ExecuteAsync`）一行不改，执行引擎一行不改。**

---

## 4. 分层裁决：取值层 vs 落笔层

> 这是本方案最核心的一条原创裁决，也是唯一一处**否决了用户原始设想**的地方。

### 4.1 问题的概念拆分

「填充」是两件独立的事：

| | 内容 | 谁负责 | 理由 |
|---|---|---|---|
| **① 取值** | 这个位置该填**什么值** | 🤖 **AI 合适** | 非结构化文档只有 LLM 能读 |
| **② 落笔** | 值写进 **B5 / 第 3 页页脚**，且**保留样式、合并区、分页、页眉页脚、打印设置** | 🔧 **只能程序** | 格式保真问题，不是理解问题 |

### 4.2 裁决

> **AI 负责「填什么」，NPOI 负责「填到哪、填成什么样」。两者是流水线上下游，不是替代关系。**

**否决"AI 直接产出文件"的三条硬约束**：

| # | 约束 | 说明 |
|---|---|---|
| 1 | **样式保真** | 合并区/边框/字号/数字格式/列宽/打印区域 — LLM 做不到，也不该做 |
| 2 | **页眉页脚域** | `PAGE`/`NUMPAGES` 必须是 OOXML `fldChar` 三段结构，构造是纯工程活 |
| 3 | **可复现 + 可审计** | 审核体系要求"同一资料两次生成结果一致"；AI 直接生成文件做不到 |

**否决"让 AI 描述整个文档"的根因**：模板的全部价值就是保真。交给 AI 描述 = **放弃模板**。

### 4.3 但「一次性理解」这个方向是对的，且优于现状

| 优势 | 说明 |
|---|---|
| **跨字段一致性** | AI 能做「合计 = 分项之和」「分户面积之和 = 建筑面积」这类**跨字段校验**，逐字段抽取做不到 |
| 调用次数 | 50 字段 **1 次**调用 vs 50 次调用 |
| 一次修复 | 某字段错 = 重跑 1 次，不是 50 次 |

> 对齐 `08-技能与提示词设计:140` 精排「每契约 1 次批式」的思路 —— **同样的"批"思想应下沉到提取链**。

### 4.4 修正形态：AI 产出「填充计划表」，不产出文件

```
LLM 一次调用
  输入：Markdown + 全部 FieldCode 定义 + 表格定义 + 合并视图
  输出：{ fields:{ENT_NAME:{value,confidence},…},
         tables:{DEVICE_ROWS:[…]},
         cross_check:[{rule:"合计=分项和",pass:true}] }
        ↓ 落 cert_doc_fill_plan（纯数据，可 diff）
DocFillExecutor 照表写 → NPOI
Verifier 照表验 → 标记样式残留 + cross_check
```

**收益**：可审计（可 diff / 可回溯 / 可人工改，`POST /approve` 的 `ManualResult` 已预留）· 可复现 · 成本可控（LLM 只在取值阶段调用）。

---

## 5. 整体方案草图

```
① 人工做模板（Word/Excel）
   ├─ 写 {{TOKEN}}  + 修饰符
   └─ 应用专用字符样式「YZH_Mark」        ← 验收依据，非颜色（见 §7.3）
        ↓ 上传（复用上传底座）

② 扫描  TemplateScanService（NPOI）
   → cert_doc_template_scan（版本快照）
   → cert_doc_template_part（坐标/合并区/样式/标记，全自动）
   → 体检：未定义 token / 已定义未用 / 疑似拼错 / 跨表公式告警

③ 建流程（页面只看不改）
   ├─ 结构树 + 只读预览（@vue-office/excel 仅作核对）
   └─ baseline 自动生成（~90%，零人工）
      例外覆写才上 logicflow 画布（~10%）

④ 取值  LLM 批式一次 → cert_doc_fill_plan
        ↓ 合并视图（人工覆盖优先）

⑤ 落笔  DocFillExecutor（Scoped IWorkbookContext）
   Phase1 写值 → Phase2 重复区扩行+引用重算 → Phase3 doc_save

⑥ 验收  POST test/node（单 Item 试跑，落库可回溯）
        POST approve（人工确认 + Confidence + ManualResult）
        自验收：YZH_Mark 样式残留 = 硬失败
```

---

## 6. 原创部分逐项分析

> 以下 5 项**无成熟范式可抄**，是原创设计，是多轮讨论的重点。

### 6.1 标注规范（待定稿）

**双通道**：

| 通道 | 标记 | 用途 | 状态 |
|---|---|---|---|
| 文本通道 | `{{TOKEN|修饰:值}}` | 可 grep、可校验，**承载语义** | 语法待定稿 |
| 样式通道 | 字符样式 `YZH_Mark` | **承载自验收**（写入后必须改掉，改不掉 = 未命中） | 命名待定稿 |

**Token 语法草案**：

| 类型 | 语法 | 示例 | 用途 |
|---|---|---|---|
| 字段 | `{{FieldCode}}` | `{{ENT_NAME}}` | 标量取值（90%） |
| 字段+格式 | `{{F\|fmt:0.00}}` | `{{BUILD_AREA\|fmt:0.00}}` | 替代硬编码 `.ToString("0.00")` |
| 字段+默认 | `{{F\|def:—}}` | `{{FLOOR\|def:—}}` | 空值兜底 |
| 字段+源 | `{{F\|src:global}}` | `{{ENT_NAME\|src:global}}` | 走 D28 全局参数 |
| 计算 | `{{=表达式}}` | `{{=SUM(QTY)}}` | 平台侧求值（**不用 Excel 原生公式**） |
| 重复区 | `{{#CODE}}…{{/CODE}}` | — | Excel 行块 / Word 书签块 |
| Word 域 | `{{PAGE}}` | `{{PAGE}} {{NUMPAGES}}` | 页眉页脚（**必须用域不能写死**） |

**待议**：语法字符集（`[]` 与正则字符类冲突倾向 `{{}}`）、修饰符是否需要 `def` 与 `src` 两个、转义规则、`{{#}}`/`{{/}}` 在 Excel 内的表达方式（**Word 有书签，Excel 没有**）。

### 6.2 三张新表（待评审）

| 表 | 职责 | 生命周期 |
|---|---|---|
| `cert_doc_template_scan` | 一次扫描 = 一个不可变快照 | 换模板则新版本，`IsLatest` 翻转 |
| `cert_doc_template_part` | 扫描产出的部件与落位（坐标/合并/样式/标记） | 随 Scan 版本 |
| `cert_doc_fill_plan` | AI/规则产出的填充计划（值 + 位置 + 校验） | 每次执行一份 |

**扫描与绑定解耦**的关键设计：按 `(PartType, SheetName, SectionIndex, HeaderKind, AnchorRef)` 做 diff，重扫时**继承已绑定的 `FieldCode`，消失的标黄告警** → 换模板不必重录。

**为什么 `FillTarget` 不落 `cert_doc_field_def`**（对 `14` 号 D18 的修正建议）：

| 硬伤 | 说明 |
|---|---|
| 类型必爆炸 | 现 3 type；加页眉页脚 → 5；加公式 → 6；加合并区/重复区 → 8。JSON 无 schema 校验，终成失控 |
| 标签格无处安放 | 「企业名称：」这个**无 FieldCode 的标签格**是覆盖率校验/样式保留/画布渲染的必需信息 |
| 换模板即失效 | 坐标是**模板的生存期事实**，不是字段的附属属性 |

### 6.3 写入时序（Phase1/Phase2）

⚠️ **核心风险点**：NPOI `ShiftRows` 插行会移动**已写好的**单元格值。

| Phase | 内容 | 顺序理由 |
|---|---|---|
| **1** | 静态 / 字段 / 计算 / 公式，按**原始** `AnchorRef` 写 | 先写后插，避免值被移动 |
| **2** | 重复区扩行（`ShiftRows` + 复制样式 + 复制合并区）+ **引用重算** | 插完才知道新行数 |
| **3** | `doc_save` → `VerifyMarks()` | 落盘即销毁 Scoped 句柄 |

**并行策略**：不同 Sheet 的 Item 可并行；同一 Sheet 按 `AnchorRef` 排序串行；重复区 Item 必须最后。

**跨表引用问题**：NPOI **不更新跨表引用**（`Sheet3!D10` 不会变 `Sheet3!D28`）。三条对策见 §7.2。

### 6.4 Skill 链

| 层 | SkillCode | 状态 | 作用 |
|---|---|---|---|
| **数据源** | `get_field` | ✅ 恢复注册 | `field_code` → `{value, confidence}` |
| | `get_table` | ✅ 恢复注册 | `table_code` → 行集 |
| | `get_global_param` | 🆕 | D28 全局参数取值 |
| **加工**（纯函数） | `sum` / `count` | 🆕 | `{{=SUM(QTY)}}` |
| | `format` | 🆕 | `\|fmt:` 修饰符 |
| | `if` / `concat` / `lookup` | 🆕 | 枚举映射 · 拼接 · 码表翻译 |
| **写入** | `doc_cell_write` | 🆕 | 写值 + 保 `CellStyle` + 改掉 `YZH_Mark` |
| | `doc_table_write` | 🆕 | 填行 + 样式复制 |
| | `doc_replace` | 🆕 | Word 占位符替换（保 run 格式） |
| | `doc_domain` | 🆕 | 写 `PAGE`/`NUMPAGES`（`fldChar` 三段） |
| **收口** | `doc_save` | 🆕 | 落盘 + `VerifyMarks()` 自验收 |

**`doc_*` 系列签名草案**（照 `Skill清单 §4.1`）：

```csharp
[Skill(Code="doc_cell_write", Name="写入单元格", ReturnType="json",
       Description="按扫描坐标写值，保留原样式并改掉人工标记样式")]
public static class DocCellWriteSkill
{
    public static async Task<SkillResult> ExecuteAsync(
        [SkillParam(Description="工作表名")] string sheet_name,
        [SkillParam(Description="A1 坐标，如 B5")] string anchor_ref,
        [SkillParam(Description="待写值")] string value = null!,
        [SkillParam(Description="数字格式，空=保持模板原格式")] string? fmt = null,
        [SkillParam(Description="是否改掉标记样式（自验收用）",
                    BindMode=SkillParamBindMode.Enum, EnumSource="bool_flag")] bool clear_mark = true,
        [FromService] IWorkbookContext doc = null!,   // Scoped，本次任务专属
        CancellationToken ct = default)
    {
        var cell = doc.Sheet(sheet_name).Cell(anchor_ref);
        cell.WritePreserveStyle(value, fmt);
        if (clear_mark) cell.ClearMarkStyle();
        return SkillResult.Ok(new() { ["written"]=true, ["anchor"]=$"{sheet_name}!{anchor_ref}" });
    }
}
```

> ⚠️ 上为**接口草案**，非最终规格。`IWritePreserveStyle` / `IClearMarkStyle` 的方法签名待 §8-B2 讨论后定稿。

### 6.5 baseline 自动生成 vs 例外覆写

**这是决定方案成败的配置面划分**（见 §7.1）：

| 部分 | 占比 | 怎么配 | 人工成本 |
|---|---|---|---|
| **baseline** | ~90% 字段 | 扫描器按 token 与修饰符**自动生成完整节点链**，画布**只读** | **0** |
| **例外覆写** | ~10% 字段 | 多源竞争 / 跨字段业务规则 / 单位换算，才上 logicflow 手工连 | 5 项 × 3 节点 = 15 节点 ✅ |

**关键点：token 修饰符就是 baseline。** 扫描器见 `|fmt:0.00` → 自动插 `format`；见 `{{=SUM(QTY)}}` → 自动插 `sum`。**大部分加工逻辑用修饰符表达，不需要画布。**

---

## 7. 重大隐患登记

### 7.1 【致命】画布配置成本爆炸

| | |
|---|---|
| **症状** | 50 字段 × 3 节点 = **150 节点 + 149 边**的 logicflow 画布，无人能用 |
| **量化** | 25 分钟/模板 × 20 份模板 × 每年 5 次换版 = **42 小时/年，且每次换版重配**（坐标全变） |
| **对照** | **比它要替代的东西更贵** —— 房产测绘 8 个地市 5662 行虽烂，写一次能用 |
| **对策** | §6.5：画布从「主配置面」降级为「例外覆写面」，baseline 靠修饰符自动生成 |
| **待议** | baseline 自动生成的可信度如何保证（§8-B4） |

### 7.2 【高】跨表公式的维护责任落到平台

| | |
|---|---|
| **症状** | 模板作者写 `Sheet3!D10 = Sheet2!C5*Sheet1!B3`；Sheet2 重复区插 20 行后，**NPOI 不会**更新为 `=Sheet2!C25*...`（Excel 自己会，NPOI 不会） |
| **为何必然发生** | 汇总表引明细表是最自然的做法，模板作者一定会写 |
| **对策 1** | `RepeatCap` + 每 Sheet **默认只允许 1 个重复区** |
| **对策 2** | 体检阶段**主动检出跨表公式并告警**，建议改 `{{=SUM(...)}}` 平台侧算 |
| **对策 3（安全网）** | 生成后强制调一次 **LibreOffice 重算**（`yzh-libreoffice` 容器已在，`DocumentConvertClient.ConvertToPdfAsync` 现成） |
| **待议** | 对策 3 的成本（每次生成一次容器调用）是否可接受（§8-B5） |

### 7.3 【中高】标记自验收会误报

| | |
|---|---|
| **原方案** | 用**底色**标记，写入后清色 → 残留 = 未命中 |
| **误报场景** | 模板作者用黄色做装饰；Excel **斑马纹**交替底色；Word「底色」是**字符底纹** `w:highlight`（仅 16 种固定色），与单元格填充语义不同，检测需写两套；复制粘贴继承标记色 |
| **结论** | **用颜色做标记 = 会误报**，而"零误报"是本方案的核心卖点 |
| **对策** | 改用**专用字符样式 `YZH_Mark`** —— 系统检测**样式名**而非颜色值；写入成功后把样式**改为正文样式**（而非"清空"），语义更明确 |
| **待议** | Excel 命名样式 / Word 字符样式的跨版本兼容（§8-B2） |

### 7.4 【中】`{{TOKEN}}` 是第 4 处命名，守卫管不到

`AGENTS.md` ③ 铁律管 **DB 列名 / C# 属性名 / TS 字段名** 三处；`{{ENT_NAME}}` 里的 `FieldCode` 是**第 4 处**，`guards.mjs` R7 查不到。

**对策**：新增守卫 —— 扫描结果中所有 token 必须命中 `cert_doc_field_def.FieldCode`，否则红牌阻断生成。

> 房产测绘项目已有活证据：`[build_name]` 出现 16 次，`[buildname]` 出现 1 次（`Daos/Report/SurveyReportDao.cs:436`），**同一字段两种拼法，永久静默失败，无任何报错**。

### 7.5 【项目级】范围蔓延

当前同时动了：模板扫描 + Skill 体系 + 工作流引擎 + NPOI 引入 + 队列 + 画布。而 **05 册 P0 的 9 张新表一张未建**（`10-实施路线与验收:52-60` 全部 `□` 未勾选）。

**对策**：严格切期 —— 先只做「扫描 + 体检 + baseline 写入 + 标记样式自验收」，画布与例外覆写放最后。

---

## 8. 待讨论议题（分批）

> **本节全部关闭前，不得开始编码。** 分 4 批，每批可独立讨论。

### 批次 A — 标注规范（地基，必须先定）

| # | 议题 | 选项 | 影响 |
|---|---|---|---|
| A1 | Token 语法字符集 | ① `[xxx]` 方括号 ② `{{xxx}}` | ②：`[]` 与正则字符类冲突；`14` 号 D16 已定 `{{FieldCode}}` |
| A2 | 修饰符集合 | ① 只 `fmt` ② `fmt`+`def` ③ `fmt`+`def`+`src` | ③ 的 `src:global` 承接 D28 |
| A3 | 标记样式命名 | ① `YZH_Mark` ② 其他 | 需确认 Excel 命名样式 / Word 字符样式跨版本行为 |
| A4 | Excel 重复区表达 | ① `{{#CODE}}`/`{{/CODE}}` ② 单行样例行自动识别 | **Excel 无书签概念**，②可能更自然 |
| A5 | 未定义 token 行为 | ① 静默跳过 ② 黄牌 ③ **红牌阻断** | ③：对照 §7.4 的 `[buildname]` |

### 批次 B — 执行与验收

| # | 议题 | 选项 |
|---|---|---|
| B1 | `IWorkbookContext` 契约 | 接口方法集、Excel/Word 是否分接口、生命周期（`IAsyncDisposable` 防句柄泄漏） |
| B2 | 标记样式的写入后处理 | 改为正文样式 vs 清空；样式 API 兼容性 |
| B3 | Markdown 保留期 | ① 永久 ② 画像完成后 N 天清理（填充阶段不读） |
| B4 | baseline 自动生成的可信度 | 生成后是否强制人工过目；还是靠自验收兜底 |
| B5 | LibreOffice 重算兜底 | 每次生成一次容器调用的成本是否可接受 |

### 批次 C — 架构与规则修订

| # | 议题 | 建议 |
|---|---|---|
| C1 | `Skill清单 §1.3` 处置 | **改写而非删除**。删除「不做报告生成/状态管理/数据源查询」；补「Skill 状态约定」（有状态文档操作必须走 `[FromService] IWorkbookContext`（Scoped），禁 static 缓存 —— 这是**防呆约定不是禁令**） |
| C2 | `Skill清单 §1.2` 废弃清单 | `get_field`/`get_table` **撤销废弃**（原裁决"行为与功能性节点不一致"已被证明不完整 —— 它们是 `doc_*` 的上游数据源）；`document_extract` **保留废弃**（那个裁决是对的，它有独立 Service/Controller/规则表/页面） |
| C3 | `TaskType` 新枚举值命名 | `TEMPLATE_GENERATE` |
| C4 | 编排落 `cert_validation_rule.RuleJson` 还是新建 `cert_doc_fill_plan` | 复用（`WorkflowType` 加 `template`）；但 fill_plan 承担 AI↔执行器契约，职责不同 |
| C5 | 报告模板表 | 复用 `cert_report_template` 加 `ScanCode`，还是独立 |

### 批次 D — 取值链

| # | 议题 | 选项 | 建议 |
|---|---|---|---|
| D1 | 提取链粒度 | ① 逐字段抽取 ② **一份文档 1 次 LLM 出全部字段+表格+跨字段校验** | ②（token 省 90%+，且能做跨字段校验） |
| D2 | `cert_doc_fill_plan` 是否建表 | ① 不建，直写 `cert_extraction_result` ② **建表作为 AI↔执行器契约** | ②（可审计 + 可人工改 + 执行器不依赖 AI） |
| D3 | 落笔层选型 | ① NPOI ② OpenXML SDK | ①（`XWPFHeaderFooter` 支持更全） |
| D4 | 跨字段校验失败的处理 | ① 阻断 ② 黄牌继续 ③ 写进 `cross_check` 交人工裁决 | 待议 |

---

## 9. 决策登记

### 9.1 本轮已定（技术事实，有代码/文档证据）

| ID | 决策 | 证据 |
|---|---|---|
| **D29** | 填充阶段**不打开企业文档**，唯一数据源 = 3 张表 + 合并视图 | D3 + `README :82` |
| **D30** | 编排复用四层执行模型，**零新表**；`TaskType` 加 `TEMPLATE_GENERATE` | `WfPathExecution.cs:11-25`；`TaskExecutionModels.cs:12` |
| **D31** | 有状态文档 Skill 走 `[FromService] IWorkbookContext`（Scoped），**Skill 契约与引擎零改动** | `SkillExecutor.cs:203-205` + `AddScoped` × 2 + 队列 `CreateScope` |
| **D32** | **取值层 AI 批式 / 落笔层 NPOI 分层**；AI 产出 `fill_plan` 不产出文件 | §4 三条硬约束 |
| **D33** | 不引入 Office 编辑器；`@vue-office/*` 仅作只读核对 | §1.2 调研 |

### 9.2 待拍板（源自 §8）

| ID | 决策点 | 批次 | 建议 |
|---|---|---|---|
| P30 | Token 语法字符集 | A | `{{}}` |
| P31 | 标记自验收介质 | A | **专用字符样式，非颜色** |
| P32 | 重复区上限 | B | 每 Sheet 默认 1 个 + `RepeatCap` |
| P33 | `Skill清单 §1.3` 处置 | C | 改写 + 补状态约定 |
| P34 | `get_field`/`get_table` 撤销废弃 | C | 是 |
| P35 | 提取链批式化 | D | 是 |
| P36 | `cert_doc_fill_plan` 建表 | D | 是 |

---

## 10. 与既有分册的修订清单

| 分册 | 修订内容 | 性质 | 状态 |
|---|---|---|---|
| `14` D18 | `cert_doc_field_def.FillTarget` JSON 列 → **改为 3 张模板结构表**；理由见 §6.2 | **勘误** | 待 C4/D2 拍板 |
| `14` D25 | `AggregateRule` 三 mode → 映射为 `wf_path_execution` 的多路径 | 承接 | 已成立 |
| `14` D28 | 全局参数 → 提为 P0（`get_global_param` Skill 的上游） | 提前 | 待拍板 |
| `05` §2.3 | `CellPrompt/TablePrompt` 之外需补 `MarkStyleJson`（自验收依据） | 增补 | 待 B2 |
| `02` §6 | 契约表加 `FixedDocSubtype`（D25）/ `ComputeHandler`（例外扩展点） | 增补 | 待议 |
| `Skill清单` §1.2/§1.3 | 见 §8-C1/C2 | **改写** | 待拍板 |
| `01` C10 | 契约能力标注「❌ 缺失」→ 本方案不解决契约表本身，仍缺 | 登记 | — |

**未修订项说明**：`cert_standard_doc_contract`（契约表）**本方案不涉及**，仍是 `02 §3.1` 的设计稿 + 零实现状态。本方案产出的是「模板落位与执行」，消费契约表的 `RuleCode` / `FillStrategy`，不替代它。

---

## 11. 证据索引

### 11.1 本项目代码

| 结论 | 位置 |
|---|---|
| 四层执行模型 | `src/certplatform-api/CertPlatform.Shared/Entities/Wf/WfPathExecution.cs:11-25` |
| `TaskType` 三值 | `.../Services/Workflow/Models/TaskExecutionModels.cs:12` |
| `[FromService]` 按请求解析 | `.../Services/Workflow/Skills/SkillExecutor.cs:203-205` |
| `AddScoped` × 2 | `.../CertPlatformAdminServiceExtensions.cs:42,51` |
| 队列每任务建 Scope | `.../Services/StandardDirectory/OfficeConvertTaskExecutor.cs:46` |
| `NodeExecutor` nodeType 分支 | `.../Services/Workflow/NodeExecutor.cs:85-100` |
| 验收三件套 | `.../Controllers/Workflow/WorkflowTestController.cs:62,91,362` |
| `ManualResult` 预留 | `.../Models/TaskExecutionModels.cs:170-185` |
| `get_field` 代码保留 | `.../Services/Workflow/Skills/GetFieldSkill.cs`；`Skill清单 §11` |
| 无 Office 库 | 全仓 `*.csproj` 无 NPOI/EPPlus/OpenXml |
| `IExcelService` 空实现 | `src/yzh-core/YZH.Core.Stand/Interfaces/IExcelService.cs:53-60` |
| 预览走 PDF 降级 | `cert-admin/src/pages/workflow/doc-extraction-rule/components/DocPreview.vue:325-329` |
| `DocFieldDef` 无三列 | `CertPlatform.Shared/Entities/Doc/DocFieldDef.cs` |
| `cert_report_template` 零生成 | `CertPlatform.Shared/Entities/Cert/ReportTemplate.cs` |

### 11.2 参考项目（房产测绘系统，反面证据）

| 结论 | 位置 |
|---|---|
| `[token]` + 硬编码坐标机制 | `YZH架构/YZH.Stand/Helper/Excel/SheetHelper.cs:87-107` |
| 调用样例 | `.../Daos/Report/TianShuiReportDao.cs:34-38` |
| **拼写错误活证据** | `.../Daos/Report/SurveyReportDao.cs:436`（`[buildname]` vs 16 处 `[build_name]`） |
| **三套不兼容 token 语法** | Excel `[xxx]` / Word 裸文本 / Word 整句（`.../Daos/Report/YinChuanReport.cs:141-175`） |
| **C# 表达式当 token** | `.../Daos/Report/YinChuanReport.cs:155,174-175` |
| 8 地市 8 类 5662 行 | `.../Daos/Report/*.cs`（`wc -l` 实测） |
| 重复区手写 `i++` | `.../Daos/Report/TianShuiReportDao.cs:41-64` |

### 11.3 外部调研（2026-09-29）

| 来源 | 关键结论 |
|---|---|
| `github.com/dream-num/univer` README | Sheets/Docs 的 **import/export 属 Pro**；OSS 有公式引擎（500+ 函数）与 Docs 页眉页脚控制器 |
| `github.com/ONLYOFFICE/DocumentServer` | Community = AGPLv3 / 免费 / **up to 20 users recommended** |
| registry.npmjs.org | `@univerjs/sheets-formula` 1.0.2（MIT）、`@vue-office/excel` 1.7.14（MIT）、`luckysheet` 2.1.13（仓库已 404） |

---

## 12. 下一轮讨论建议

**建议先只做批次 A（标注规范）**，因为：

1. A1–A5 是**地基**，一旦定了，三张表和 8 个 Skill 的 schema 全部可推导
2. A4（Excel 重复区表达）是**唯一无对标物**的议题 —— Word 有书签，Excel 没有，必须原创决策
3. A5 直接对应 §7.4 的历史教训，代价可量化

**批次 A 关闭后再进批次 B**（执行与验收），B 关闭后 C（规则修订）与 D（取值链）可并行。

> **在此之前不动代码。** 05 册 P0 的 9 张新表尚未开工，本方案与之并行会放大 §7.5 的范围风险。

---

> **维护约定**：本文件是**讨论快照**。每轮讨论后更新 §9 决策登记与 §8 议题状态；§8 全部关闭后出 `-V2` 实施分册，本文件降级为过程记录（文首加「已并入」标记）。
