# 16 — 模板标注与 Skill 流程化方案（V2 收敛稿）

> **版本**：V2 | **日期**：2026-09-29 | **状态**：🟡 **架构骨架已定，细节待决 — 非实施规格**
> **前身**：[`16-模板标注与Skill流程化方案-V1.md`](./16-模板标注与Skill流程化方案-V1.md)（讨论稿，V1 的探索过程，**保留归档**）
> **⚠️ 强制声明**：**§13 待决议题全部关闭前不得编码。** 本稿登记的是**已收敛的架构骨架**（有明确依据）与**待决的细节**（无对标物），两者严格分离，不得混用。
> **作者立场**：本模块无成熟范式可抄。poi-tl / docxtemplater / JasperReports 仅在「模板打标 + 通用引擎」层同构；**坐标自解析、书签分��锚点、标记样式自验收、取值/落笔分层、阶段 A/B 并发分段** 五处为原创。
> **上位文档**：`05-标准文档契约模型-V1.md`、`14-需求深化讨论收敛与增补设计-V1.md`、`docs/30-项目规则/Skill清单-V1.md`、`AGENTS.md`

---

## 目录

1. [V1 → V2 收敛摘要](#1-v1--v2-收敛摘要)
2. [架构总则：三条铁律](#2-架构总则三条铁律)
3. [锚点模型](#3-锚点模型)
4. [模板设计规范（W1–W8 / E1–E7）](#4-模板设计规范w1w8--e1e7)
5. [流程粒度与编排](#5-流程粒度与编排)
6. [执行模型：阶段 A / 屏障 / 阶段 B](#6-执行模型阶段-a--屏障--阶段-b)
7. [预取缓存与合并视图](#7-预取缓存与合并视图)
8. [Skill 清单与签名草案](#8-skill-清单与签名草案)
9. [数据模型](#9-数据模型)
10. [验收体系](#10-验收体系)
11. [模板换版：重扫与差异继承](#11-模板换版重扫与差异继承)
12. [排期切分](#12-排期切分)
13. [待决议题登记](#13-待决议题登记)
14. [决策登记](#14-决策登记)
15. [隐患台账（V2 更新）](#15-隐患台账v2-更新)
16. [证据索引](#16-证据索引)

---

## 1. V1 → V2 收敛摘要

V1 记录了 6 轮讨论的过程，V2 是收敛结果。**骨架发生 6 处实质变更**：

| # | V1 原设计 | V2 修订 | 变更动因 |
|---|---|---|---|
| 1 | 编排 = 1 份文档 1 个 `WorkflowConfig`，50 个 Item 塞进去 | **1 个字段 = 1 个流程**（50 行 `cert_validation_rule`），`PlanCode` 归属同一份文档 | V1 的单一大 JSON 不可 diff、不可局部重配；V2 一行一个字段 |
| 2 | 输出层 = Word + Excel 双渲染器，平级 | **模板层统一到 Word**；输出层 `DocxRenderer`(P0) + `XlsxRenderer`(P1) | 「一种模板语言，两种输出」比「两种模板语言」更省；Excel 规范难统一 |
| 3 | 锚点 = A1 坐标 / token 文本 | **标量 = token 文本；区块 = 书签（Word）/ 区域 + `total_token`（Excel）** | token 表达不了范围；书签名使换模板时配置零改动 |
| 4 | Phase 2 需「公式引用重算」 | **删除** | 不依赖 Office 单元格引用（用户原则 1） |
| 5 | 需 LibreOffice 重算兜底 | **删除** | 同上 |
| 6 | 画布 = 主配置面（~90% 走 baseline） | **画布 = 例外覆写面**；50 个小画布通过列表页按需加载 | V1 的「150 节点画布」在 1 字段 1 画布下不存在 |

**V1 中已定且 V2 保留**：分层裁决（取值 AI / 落笔 NPOI）、四层执行模型零新表、`[FromService]`+Scoped 验证、填充不碰企业文档。

---

## 2. 架构总则：三条铁律

> 这三条是全部设计的公理。**任何新需求先过这三条**，不符则改需求而非改铁律。

### 2.1 铁律一：格式归模板，值归平台

| 归谁 | 内容 |
|---|---|
| **模板** | 版式、样式、字体、边框、合并区、列宽、`numFmt`、分页、打印设置、页眉页脚结构 |
| **平台** | **只写值**。按 `FieldCode` 的 `DataType` 决定写 `decimal` / `DateTime` / `string` |

**配套规则（否则出���类型坑）**：

- `number` / `date` → 写**强类型值**，让模板既有 `numFmt` 生效
- 写 `string` 会让 Excel 把 `1234.5678` 变左对齐文本 —— 这不是"依赖 Office 函数"，是类型正确性，**必须管**
- 平台**永不**修改 `CellStyle` / run 格式（唯一例外：写入成功后把 `YZH_Mark` 样式**改为正文样式**，见 §10.3）

**推论**：

| 消除项 | 理由 |
|---|---|
| 跨表公式维护责任 | 平台不产生 Excel 公式 |
| 重复区插行后的引用重算 | 平台不产生单元格引用 |
| LibreOffice 重算兜底 | 不需要 |
| 样式保真风险 | 平台不碰样式 |

### 2.2 铁律二：一种模板语言，两种输出渲染器

```
┌─ 模板层（唯一 · 唯一语言）──────────────────────┐
│  Token 文本（标量）· 书签/区域（区块）· 页眉页脚域 │
└────────────────────┬────────────────────────────┘
                     ↓  同一份 cert_doc_fill_plan
          ┌──────────┴──────────┐
          ▼                     ▼
   DocxRenderer            XlsxRenderer
   （P0 先做）             （P1 后加）
```

**「只操作一种格式」的准确含义是「只有一种模板语言」，不是「只有一种文件格式」。** 取值逻辑、Skill 链、`fill_plan`、验收全部共用，只有**最后落笔那一步**分叉。

| 文件类型 | 处置 |
|---|---|
| `.docx` | **P0 唯一目标格式** |
| `.xlsx` | **P1 目标格式** |
| `.doc` / `.xls` | ⛔ **不研究**，用转换 Skill 归一（复用 `OfficeConvertService.cs:446-499` 遗留 `doc2docx` 逻辑 + `yzh-libreoffice` 容器） |

> ⚠️ **不可省略 Excel 的理由**（`14` 号 D16/D25 明确）：`extract` 策略适用「培训记录/设备台账/内审计划等**结构化字段密集型文档**」；`RepeatPolicy=row`、`max_confidence` 多源聚合均为 Excel 场景设计。审核员现场基本都用 Excel 填台账。**P1 是必需项，不是可选项。**

### 2.3 铁律三：唯一写入者不变量

> **每个锚点在一条填充计划中，最多一个 `doc_*` 节点（唯一写入者）。**

**推导出的执行性质**：

| 性质 | 结论 |
|---|---|
| 写冲突 | **不可能**（不变量保证） |
| 阶段 B 是否需按依赖排序 | ❌ **不需要，任意顺序** |
| 是否需屏障后才能求和 | ❌ **不需要**，值在阶段 A 已算完（`sum` 是 O(1) 纯函数，23 行数据在 DB 侧已就绪） |
| 是否需循环编排 | ❌ **不需要**，每步都是简单函数 |

**这条不变量同时给了模板规范一条硬要求**：同一 Token **不得在同一模板出现两次**（重复区块内的样例行除外）。扫描器可自动检出。

---

## 3. 锚点模型

### 3.1 双层锚点

| 锚点类型 | 机制 | 用途 | 模板作者操作 | 唯一性 |
|---|---|---|---|---|
| **标量锚点** | **Token 文本** `{{TOKEN\|修饰:值}}` | 单个值 | 打字 + 套 `YZH_Mark` 样式 | 铁律三要求唯一 |
| **区块锚点**（Word） | **书签** Bookmark A→B | 重复区（表格行块）/ 整张表格 | 选中 → 插入 → 书签，命名 `ROW_<表名>` | ✅ Word 原生唯一 |
| **区块锚点**（Excel） | **区域** `anchor:range` + `total_token` | 数据矩形区 | 圈定区域 | 扫描器校验连续性（E1） |
| **域锚点** | `{{PAGE}}` / `{{NUMPAGES}}` | 页眉页脚页码 | 打字（渲染为域） | 每类页眉页脚各一处 |

### 3.2 为什么必须分层

| | Token 文本 | 书签 / 区域 |
|---|---|---|
| 标量定位 | ✅ run 级 / cell 级定位已足够精确 | 冗余 |
| **区块范围** | ❌ **表达不了**（"从 A 到 B 含表格"） | ✅ Word 原生；Excel 需显式声明 |
| 唯一性 | ⚠️ 可重复出现 | ✅ 天然唯一 |

**Token 表达不了范围是硬伤**：Word 里判断"某 token 属于哪个重复区"要靠表格结构推断，脆。用书签 A→B 包住整块，扫描器直读边界，确定。

### 3.3 换模板的收益（选书签的决定性理由）

| 锚点存什么 | 换模板时 N 个流程配置 |
|---|---|
| 坐标（段落序号 / A1） | 🔴 **全废** |
| Token 文本 | 🟡 字段名不变则不废 |
| **书签名 / 区域标识** | 🟢 **可复用**（命名规范不变即可），且扫描器可校验存在性 |

### 3.4 Token 语法草案（**未定稿，见 §13-A1/A2**）

| 类型 | 语法 | 示例 | 产出 |
|---|---|---|---|
| 字段 | `{{FieldCode}}` | `{{ENT_NAME}}` | `FillType=field` |
| 字段+格式 | `{{F\|fmt:0.00}}` | `{{BUILD_AREA\|fmt:0.00}}` | 写入强类型 + 数字格式 |
| 字段+默认 | `{{F\|def:—}}` | `{{FLOOR\|def:—}}` | 空值兜底 |
| 字段+源 | `{{F\|src:global}}` | `{{ENT_NAME\|src:global}}` | 走 D28 全局参数 |
| 计算 | `{{=表达式}}` | `{{=SUM(QTY)}}` | 平台侧求值，**非 Excel 公式** |
| 域 | `{{PAGE}}` | `{{PAGE}} / {{NUMPAGES}}` | 写 `fldChar` 三段 |

> **取消** V1 的 `{{#CODE}}` / `{{/CODE}}` 重复区语法 → 改用书签/区域（§3.1）。

---

## 4. 模板设计规范（W1–W8 / E1–E7）

> **本节是 V2 的重要产出。** 你的前提（标准模板稳定 + 专门维护团队）成立时，**规范比代码更能控风险**。
> **规范强制项**须进模板包 + 维护团队培训材料；**可自动检出项**（标 ✅）由扫描器做规范校验，违反红牌阻断。

### 4.1 Word 模板规范

| # | 规范 | 可检出 | 理由 |
|---|---|---|---|
| **W1** | 标量值一律 `{{Token}}` 文本，**不用书签** | — | 打字即可；书签留给区块 |
| **W2** | 重复区用**书签 A→B** 包住，命名 `ROW_<表名>` | ✅ 检出无书签的重复表格 | token 表达不了范围 |
| **W3** | 书签名全局唯一、PascalCase、≤ 32 字符、仅字母数字下划线 | ✅ | Word 书签名有字符/长度限制 |
| **W4** | 所有可替换内容套 `YZH_Mark` 字符样式 | ✅ 检出无标记的 token | 自验收唯一依据 |
| **W5** | 重复区**至少留 1 行样例行** | ✅ | 扫描器识别 + 样式克隆源 |
| **W6** | 除 `PAGE`/`NUMPAGES` 外**不用 Word 域**做动态值 | — | 域不参与替换 |
| **W7** | 页眉页脚**三类（首页 `first` / 奇偶 `even` / 默认 `default`）都要检查** | ✅ 检出只配了一类 | 最容易漏配 |
| **W8** | 跨页长表格：表头行设 `w:tblHeader` 重复 | — | 否则第 2 页无表头 |

### 4.2 Excel 模板规范

| # | 规范 | 可检出 | 理由 |
|---|---|---|---|
| **E1** | 数据区必须**连续矩形** `anchor:range`，不得有空洞 | ✅ | 空洞破坏偏移计算 |
| **E2** | **区外元素（合计/汇总）紧贴数据区下沿**，用 `{{SUM_ROWS}}` 类 token 声明，**禁硬坐标** | ✅ 检出区外 token | 见 §4.3 |
| **E3** | 样例行 = 第 1 个含 `{{...}}` 的行 | ✅ | 扫描器识别 |
| **E4** | 数值单元格**先在模板设好 `numFmt`** | — | 格式归模板（铁律一） |
| **E5** | **不用 Excel 原生公式**（除明确要给用户二次编辑的） | ✅ 检出 `=` 开头单元格 | 平台不消费，且 NPOI 不更新引用 |
| **E6** | 合并单元格**只允许表头和标签区**，数据区禁合并 | ✅ 检出数据区合并 | 合并区破坏偏移 |
| **E7** | 隐藏 sheet / 隐藏行不参与数据区 | ✅ | 易漏 |

### 4.3 E2 的具体机制（Excel 唯一真实成本的解法）

**问题**：Excel 锚点是坐标，**插行即移动**。

```
模板：A10:F60 数据区（60 行）  +  A61 合计行
数据：23 条
执行：写 23 行 → 插 22 行 → 数据区变 A10:A33
      🔴 A61 没动，合计跑到数据区外第 28 行
```

**解法**：`total_token` 声明的是「**哪个 token 是合计**」，不是「合计在第几行」。

```
模板：
  A10:F10   表头（固定）
  A11:F11   样例数据行（含 {{...}}）
  A12       合计行，含 {{SUM_ROWS}}          ← 只声明"我是合计"

Skill 参数：doc_table_write(anchor="A11", range="A11:F11", total_token="SUM_ROWS")

执行：
  1. 按相对偏移写 N 行数据（从 A11 起，不需知道最终行数）
  2. 按模板样式复制到 A12..A(N+11)
  3. 合计锚点重定位到 A(N+12)（= 数据区末行 + 1）→ 写值
  4. 清理模板原第 12~60 行残留
```

**插多少行都不需要回改任何流程配置。漂移问题彻底消失。**

---

## 5. 流程粒度与编排

### 5.1 粒度

| 粒度 | 定义 | 数量（50 标量 + 2 表格的文档） |
|---|---|---|
| 填充计划 | 一份文档 | 1 |
| **流程** | **一个字段 / 一张表格** | **52** |
| 节点 | 取数 → 计算 → 格式化 → 写入 | 每流程 3–5 |
| 画布 | 一个流程 | 每流程 3–5 节点，**完全可用** |

**表格 = 1 个流程，不拆成 N×M 个。** 理由：插行 / 样式 / 合并区是**原子操作**，拆开写会破坏表格结构。`DEVICE_ROWS` 23 行 × 6 列 = 138 个值，由 1 个 `doc_table_write` 一次完成。

### 5.2 存储

```
cert_doc_fill_plan            一次填充计划 = 一份文档        🆕
  Code / StandardFileCode / ScanCode / EnterpriseCode / StandardCode / StageCode
  PlanState: drafting → running → verifying → archived | failed
  ExpectedFieldCount / FilledFieldCount / FailedFieldCount

cert_validation_rule          一行 = 一个字段的流程           ♻️ 复用 + 加 PlanCode 列
  Code / PlanCode / RuleName / WorkflowType='template'
  RuleJson = { Nodes:[start, get_field, format, doc_write, end], Edges:[…] }
  ↑ 52 行小 JSON，各自可 diff / 局部重配 / 单字段回滚

队列                          1 个 plan = 1 个 task（TEMPLATE_GENERATE）
                              └ 52 个 wf_execution_task_item，并发
```

**V1 误判澄清**：V1 曾把 50 个 Item 塞进一个 `RuleJson`。错在**把行式表当文档仓库用** —— 改一个字段要 diff 整个 JSON。V2 修正为一行一流程 + `PlanCode` 聚合。

### 5.3 UI 形态

**50 个画布不做成 50 个 tab。** 列表页按需加载：

```
字段/表格列表（52 行）
┌──────────────────────────────────────────────────────────┐
│ #  名称        类型      锚点        流程状态   操作       │
│ 1  企业名称     赋值      书签 ENT_NAME  ● 已通过  [测试][改]│
│ 2  设备明细     表格填充  ROW_设备      ○ 待配置  [测试][改]│
│ 3  合计        计算      SUM_ROWS     ○ 待配置  [测试][改]│
│ …                                                           │
└──────────────────────────────────────────────────────────┘
                              ↓ 点「改」才加载那一个画布
```

---

## 6. 执行模型：阶段 A / 屏障 / 阶段 B

### 6.1 为什么必须分段

⚠️ **`XWPFDocument` / `XSSFWorkbook` 不是线程安全的。** 52 路并发同时写同一文档会损坏：
- `XSSFWorkbook` 内部 `SharedStringsTable` 并发 add → 结构损坏
- 行/列索引是共享可变状态
- `ShiftRows` 改结构时，其他线程正按旧坐标写 → 写到错格

**解法：并发段与写入段分离。并发收益完整保留在阶段 A。**

### 6.2 三段式

```
阶段 A（并发 N 路 · 只读缓存 · 零文件副作用）
  每条字段流程：get_field(缓存优先) → sum / count / avg / format / if / lookup
  → 结果落 cert_doc_fill_plan（按 FieldCode 落行：value / confidence / source / override）

────── 屏障：等 N 路全回 + 校验命中数（§7.2）──────

阶段 B（单线程 · Scoped IWorkbookContext · 唯一写入者）
  ① 遍历 fill_plan → doc_write 逐行写入        ← 任意顺序（铁律三）
  ② 重复区扩行
     Word：克隆表格行 XML（样式/合并区/行高）→ XWPFTable.InsertNewTr
     Excel：ShiftRows + 复制样式；合计锚点按 §4.3 相对重定位
  ③ doc_save → 落盘
  ④ VerifyMarks() → 标记样式残留清单
  ⑤ 释放 Scoped 文档句柄（防 MinIO 句柄泄漏）
```

**阶段 B 里没有编排、没有依赖、没有循环。**

### 6.3 `IWorkbookContext` 生命周期

| 项 | 要求 |
|---|---|
| 注册 | `AddScoped<IWorkbookContext, NpoiDocContext>()` |
| 创建 | plan task 的 `CreateScope()` 内，从模板路径打开 docx/xlsx |
| 持有 | 阶段 B 全程单线程 |
| 释放 | `doc_save` 之后，`IAsyncDisposable` 释放文件流 |
| ⚠️ 硬约束 | `SkillExecutor` / `WfExecutionTaskService` 必须**从同一个 scope 内解析**（两者均 `AddScoped`，见 §16） |

---

## 7. 预取缓存与合并视图

### 7.1 `FillDataCacheBuilder`

阶段 A 开始前**一次性预取**，把 52 次 DB 往返压成 1 次。

```
预取 SQL：cert_extraction_result
  WHERE EnterpriseCode=? AND StandardCode=? AND StageCode=?
    AND StandardFileCode=? AND LabelTag IN (...N 个 FieldCode...)
  ＋ cert_global_param_instance（同作用域）
  ＋ FieldOverrideJson（人工覆盖）
        ↓
  Dictionary<FieldCode, MergedValue{value, confidence, source, override, conflict}>
        ↓
  挂到 Scoped 的 IFillDataCache，N 路并发只读
```

### 7.2 两条必须写死的规则

| 规则 | 后果 |
|---|---|
| **预取时必须合并人工覆盖值** | 否则审核员改过的值被 AI 原值覆盖 → **违反 D9 / `14` 号 §7「原值不灭」** |
| **预取后校验命中数** | `命中数 / 预期数 < 阈值` → **任务直接失败，不进阶段 B**。key 写错会导致 52 个字段全空，比单字段失败严重得多 |

### 7.3 `get_field` 双路径

```
IFillDataCache 命中（含人工覆盖）  → 直接返回        ← 快路径
未命中                            → 回退查 DB        ← 兜底
```

---

## 8. Skill 清单与签名草案

> ⚠️ 签名为**草案**，`IWritePreserveStyle` / `IClearMarkStyle` / `CloneRow` 等内部 API 命名待 §13-B1 讨论后定稿。

### 8.1 清单

| 层 | SkillCode | 状态 | 作用 |
|---|---|---|---|
| **数据源** | `get_field` | ✅ **恢复注册** | `field_code` → `{value, confidence}`（代码已存在，`Skill清单 §11`） |
| | `get_table` | ✅ **恢复注册** | `table_code` → 行集（代码已存在） |
| | `get_global_param` | 🆕 | D28 全局参数取值 |
| **加工**（纯函数 O(1)） | `sum` / `count` / `avg` | 🆕 | 表格数据聚合 |
| | `format` | 🆕 | `\|fmt:` 修饰符 |
| | `if` / `concat` / `lookup` | 🆕 | 条件取值 · 拼接 · 码表翻译 |
| **写入**（Scoped 句柄） | `doc_cell_write` | 🆕 | 写值 + 保留样式 + 改掉 `YZH_Mark` |
| | `doc_table_write` | 🆕 | 插行 + 样式克隆 + 填 N×M + 合计重定位 |
| | `doc_replace` | 🆕 | Word 片段替换（保留前后文字与 run 格式） |
| | `doc_domain` | 🆕 | 写 `PAGE`/`NUMPAGES`（`fldChar` 三段） |
| **收口** | `doc_save` | 🆕 | 落盘 + `VerifyMarks()` 自验收 |
| **归一** | `doc_convert` | 🆕 | `.doc`/`.xls` → `.docx`/`.xlsx`（复用 `OfficeConvertService.cs:446-499`） |

### 8.2 签名草案

```csharp
[Skill(Code="doc_cell_write", Name="写入单元格", ReturnType="json",
       Description="按扫描锚点写值，保留原样式并改掉人工标记样式")]
public static class DocCellWriteSkill
{
    public static async Task<SkillResult> ExecuteAsync(
        // ── 定位（来自扫描，人工零输入）──
        [SkillParam(Description="锚点：Word=书签名，Excel=Sheet!A1")] string anchor = null!,
        [SkillParam(Description="Token 名（用于回查 part 记录）")] string token = null!,

        // ── 数据（阶段 A 已备好）──
        [SkillParam(Description="待写值")] string value = null!,
        [SkillParam(Description="值类型：auto/string/number/date")]
        string value_type = "auto",
        [SkillParam(Description="数字格式，空=保持模板原 numFmt")] string? fmt = null,
        [SkillParam(Description="空值兜底文案")] string? default_text = null,

        [SkillParam(Description="是否改掉标记样式（自验收用）",
                    BindMode=SkillParamBindMode.Enum, EnumSource="bool_flag")]
        bool clear_mark = true,
        [FromService] IWorkbookContext doc = null!,   // Scoped，本次任务专属
        CancellationToken ct = default)
    {
        var target = doc.Resolve(anchor);
        target.WritePreserveStyle(value, value_type, fmt, default_text);
        if (clear_mark) target.ApplyNormalStyle();
        return SkillResult.Ok(new() { ["written"] = true, ["anchor"] = anchor });
    }
}
```

```csharp
[Skill(Code="doc_table_write", Name="表格填充", ReturnType="json",
       Description="按样例行克隆扩行并填充行集；合计锚点按数据区下沿相对重定位")]
public static class DocTableWriteSkill
{
    public static async Task<SkillResult> ExecuteAsync(
        // ── 定位（来自扫描）──
        [SkillParam(Description="书签/区域名")] string anchor = null!,
        [SkillParam(Description="样例区间：Word=书签对，Excel=Sheet!A11:F11")]
        string sample_ref = null!,

        // ── 数据（阶段 A 已备好）──
        [SkillParam(Description="行集 JSON：[{列名:值,…},…]", BindMode=Link)]
        string rows_json = null!,
        [SkillParam(Description="列顺序 JSON：[列名,…]")] string columns_json = null!,

        // ── 区外元素（相对声明，非硬坐标）──
        [SkillParam(Description="合计/汇总 token 名，空=无")] string? total_token = null,
        [SkillParam(Description="合计计算式，如 SUM(QTY)",
                    BindMode=SkillParamBindMode.LinkOrConstant)] string? total_expr = null,

        [SkillParam(Description="最大行数上限，防模板撑爆")] int max_rows = 500,
        [FromService] IWorkbookContext doc = null!,
        CancellationToken ct = default)
    { /* 见 §4.3 */ }
}
```

**参数来源三分**：`anchor`/`sample_ref` 来自扫描（人工零输入）；`rows_json` 来自阶段 A 缓存；`total_*` 是相对声明。三者独立，任一变动不影响其余。

### 8.3 「替换 vs 赋值」自动判定（**不进配置项**）

| 模板内容 | 判定 | 动作 |
|---|---|---|
| 整格/整段文本 **== token**（可带修饰符） | **赋值** | 整体写值，保留 `CellStyle` / run 格式 |
| 文本**含其他内容** | **替换** | 只替换 token 片段，保留前后文字与格式 |

**让用户在页面选会经常选错**（`名称：{{ENT_NAME}}，编号：{{ENT_CODE}}` 选"赋值"会抹掉前后文）。分类留在代码里。房产测绘 `SheetHelper.cs:93-97` 已隐含此逻辑（走 `Replace` 分支），此处显式化。

---

## 9. 数据模型

### 9.1 `cert_doc_template_scan`（🆕 扫描快照）

```sql
CREATE TABLE `cert_doc_template_scan` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) NOT NULL COMMENT '业务键GUID',
  `CreateTime` datetime NOT NULL COMMENT '创建时间',
  `CreateBy` varchar(64) DEFAULT NULL COMMENT '创建人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(64) DEFAULT NULL COMMENT '更新人Code',
  `StandardFileCode` varchar(36) NOT NULL COMMENT '模板宿主 → cert_standard_directory_file.Code（YZH-STD-ENT 侧）',
  `TemplateVersion` int NOT NULL DEFAULT '1' COMMENT '模板版本（换模板递增）',
  `IsLatest` bit(1) NOT NULL DEFAULT b'1' COMMENT '是否最新版本',
  `FileKind` varchar(10) NOT NULL COMMENT 'docx / xlsx',
  `SourcePath` varchar(512) NOT NULL COMMENT '本次扫描所用模板存储路径',
  `SourceSha256` varchar(64) DEFAULT NULL COMMENT '模板指纹：同 sha 不重扫',
  `PartCount` int NOT NULL DEFAULT '0' COMMENT '识别到的部件数',
  `BookmarkCount` int NOT NULL DEFAULT '0' COMMENT '识别到的书签数',
  `MarkCount` int NOT NULL DEFAULT '0' COMMENT '识别到的 YZH_Mark 标记数',
  `ScanStatus` varchar(20) NOT NULL DEFAULT 'pending' COMMENT 'pending/processing/completed/failed',
  `ScanMessage` varchar(1024) DEFAULT NULL COMMENT '失败原因',
  `ViolationJson` json DEFAULT NULL COMMENT '【W1-W8/E1-E7 规范校验结果】[{"code":"E2","level":"error","message":"…","anchor":"…"}]',
  `SummaryJson` json DEFAULT NULL COMMENT '概览：{"sections":3,"headers":["default","first"],"bookmarks":8}',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除',
  `DeleteBy` varchar(64) DEFAULT NULL COMMENT '删除人Code',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '1有效0无效',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_file_version` (`StandardFileCode`,`TemplateVersion`),
  KEY `idx_file_latest` (`StandardFileCode`,`IsLatest`),
  KEY `idx_sha` (`SourceSha256`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='模板结构扫描快照';
```

### 9.2 `cert_doc_template_part`（🆕 部件与锚点）

```sql
CREATE TABLE `cert_doc_template_part` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) NOT NULL COMMENT '业务键GUID',
  `CreateTime` datetime NOT NULL COMMENT '创建时间',
  `CreateBy` varchar(64) DEFAULT NULL COMMENT '创建人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(64) DEFAULT NULL COMMENT '更新人Code',
  `ScanCode` varchar(36) NOT NULL COMMENT '所属扫描快照 → cert_doc_template_scan.Code',
  `StandardFileCode` varchar(36) NOT NULL COMMENT '冗余：模板宿主',
  `TemplateVersion` int NOT NULL DEFAULT '1' COMMENT '冗余：模板版本',
  `IsLatest` bit(1) NOT NULL DEFAULT b'1' COMMENT '是否当前有效',

  `PartType` varchar(20) NOT NULL COMMENT 'scalar=标量 / table=表格 / table_total=合计 / repeat=重复区 / domain=页眉页脚域',
  `AnchorKind` varchar(20) NOT NULL DEFAULT 'token' COMMENT 'token=Token文本 / bookmark=书签 / range=Excel区域',
  `AnchorRef` varchar(200) NOT NULL COMMENT 'token={{ENT_NAME}} / bookmark=ROW_设备 / range=Sheet1!A11:F11',
  `BookmarkEnd` varchar(100) DEFAULT NULL COMMENT '区块结束书签（Word 重复区 A→B）',
  `SheetName` varchar(100) DEFAULT NULL COMMENT 'Excel 工作表名',
  `SectionIndex` int DEFAULT NULL COMMENT 'Word 分节序号',
  `HeaderKind` varchar(20) DEFAULT NULL COMMENT 'Word 页眉页脚分类：default/first/even',

  `FieldCode` varchar(100) DEFAULT NULL COMMENT '绑定的语义字段（= Token 名）',
  `TokenModifiersJson` json DEFAULT NULL COMMENT '修饰符：{"fmt":"0.00","def":"—","src":"global"}',
  `ValueType` varchar(20) NOT NULL DEFAULT 'auto' COMMENT 'auto/string/number/date（铁律一）',
  `DefaultText` varchar(200) DEFAULT NULL COMMENT '空值兜底文案',
  `ColumnsJson` json DEFAULT NULL COMMENT '表格列顺序：["DEVICE_NO","DEVICE_NAME","QTY"]',

  `MergeJson` json DEFAULT NULL COMMENT '{"rowSpan":1,"colSpan":3,"anchor":"A3"}',
  `StyleJson` json DEFAULT NULL COMMENT '写入需保留的样式快照（只读，不改）',
  `MarkStyleName` varchar(50) DEFAULT NULL COMMENT '人工标记样式名（YZH_Mark），自验收依据',

  `Required` bit(1) NOT NULL DEFAULT b'0' COMMENT '必填',
  `Sort` int NOT NULL DEFAULT '0' COMMENT '排序号',
  `Remark` varchar(500) DEFAULT NULL COMMENT '备注',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除',
  `DeleteBy` varchar(64) DEFAULT NULL COMMENT '删除人Code',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '1有效0无效',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_scan_anchor` (`ScanCode`,`PartType`,`AnchorKind`,`SheetName`,`SectionIndex`,`HeaderKind`,`AnchorRef`),
  KEY `idx_file_latest` (`StandardFileCode`,`IsLatest`),
  KEY `idx_field` (`FieldCode`),
  KEY `idx_bookmark` (`BookmarkEnd`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='模板部件与锚点（Token/书签/区域）';
```

> **铁律三自动校验**：`idx_bookmark` + 唯一键可检出"同一 `FieldCode` 绑多个锚点"与"同一锚点被多个 FlowRule 引用"两类冲突。

### 9.3 `cert_doc_fill_plan`（🆕 AI ↔ 执行器契约）

```sql
CREATE TABLE `cert_doc_fill_plan` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) NOT NULL COMMENT '业务键GUID',
  `CreateTime` datetime NOT NULL COMMENT '创建时间',
  `CreateBy` varchar(64) DEFAULT NULL COMMENT '创建人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(64) DEFAULT NULL COMMENT '更新人Code',
  `StandardFileCode` varchar(36) NOT NULL COMMENT '模板宿主 → cert_standard_directory_file.Code',
  `ScanCode` varchar(36) NOT NULL COMMENT '扫描快照 → cert_doc_template_scan.Code',
  `EnterpriseCode` varchar(36) NOT NULL COMMENT '企业Code（冗余）',
  `StandardCode` varchar(36) NOT NULL DEFAULT '' COMMENT '标准Code（冗余）',
  `StageCode` varchar(36) NOT NULL DEFAULT '' COMMENT '阶段Code（冗余）',
  `PlanState` varchar(20) NOT NULL DEFAULT 'drafting' COMMENT 'drafting/running/verifying/archived/failed',
  `ExpectedFieldCount` int NOT NULL DEFAULT '0' COMMENT '预期字段数',
  `FilledFieldCount` int NOT NULL DEFAULT '0' COMMENT '已填字段数',
  `FailedFieldCount` int NOT NULL DEFAULT '0' COMMENT '失败字段数',
  `UnfilledFieldCount` int NOT NULL DEFAULT '0' COMMENT '完成度 = 1 - (未填+失败)/预期',
  `FetchRatio` decimal(3,2) DEFAULT NULL COMMENT '预取命中率（低于阈值直接失败，见 §7.2）',
  `MarkResidueJson` json DEFAULT NULL COMMENT '【自验收】标记样式残留清单（残留=未命中）',
  `CrossCheckJson` json DEFAULT NULL COMMENT '跨字段校验结果：[{"rule":"合计=分项和","pass":true}]',
  `OutputPath` varchar(512) DEFAULT NULL COMMENT '产物存储路径',
  `ErrorMessage` varchar(1024) DEFAULT NULL COMMENT '失败原因',
  `StartedAt` datetime DEFAULT NULL COMMENT '开始时间',
  `CompletedAt` datetime DEFAULT NULL COMMENT '完成时间',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除',
  `DeleteBy` varchar(64) DEFAULT NULL COMMENT '删除人Code',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '1有效0无效',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_target` (`StandardFileCode`,`EnterpriseCode`,`PlanState`),
  KEY `idx_scan` (`ScanCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='模板填充计划（一份文档一次生成）';
```

### 9.4 `cert_doc_fill_item`（🆕 每个锚点一行 = AI 产出值）

```sql
CREATE TABLE `cert_doc_fill_item` (
  `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',
  `Code` varchar(36) NOT NULL COMMENT '业务键GUID',
  `CreateTime` datetime NOT NULL COMMENT '创建时间',
  `CreateBy` varchar(64) DEFAULT NULL COMMENT '创建人Code',
  `UpdateTime` datetime DEFAULT NULL COMMENT '更新时间',
  `UpdateBy` varchar(64) DEFAULT NULL COMMENT '更新人Code',
  `PlanCode` varchar(36) NOT NULL COMMENT '所属填充计划 → cert_doc_fill_plan.Code',
  `PartCode` varchar(36) NOT NULL COMMENT '锚点 → cert_doc_template_part.Code',
  `FlowRuleCode` varchar(36) NOT NULL COMMENT '执行的流程 → cert_validation_rule.Code',
  `FieldCode` varchar(100) DEFAULT NULL COMMENT '冗余：语义字段',
  `AnchorRef` varchar(200) NOT NULL COMMENT '冗余：锚点（写入时用）',
  `ValueText` text COMMENT '值（字符串形态）',
  `ValueNumber` decimal(20,6) DEFAULT NULL COMMENT '值（数值形态，供聚合）',
  `ValueDate` datetime DEFAULT NULL COMMENT '值（日期形态）',
  `Confidence` decimal(3,2) DEFAULT NULL COMMENT '值可信度（D8 口径，NULL=人工）',
  `SourceType` varchar(20) NOT NULL DEFAULT 'extraction' COMMENT 'extraction/globalParam/llm_batch/manual/computed',
  `IsOverridden` bit(1) NOT NULL DEFAULT b'0' COMMENT '是否人工覆盖（原值不灭，另存于 extraction_result）',
  `ItemState` varchar(20) NOT NULL DEFAULT 'planned' COMMENT 'planned/filled/failed/verified',
  `ErrorMessage` varchar(500) DEFAULT NULL COMMENT '失败原因',
  `DurationMs` int DEFAULT NULL COMMENT '执行耗时',
  `IsDeleted` tinyint(1) NOT NULL DEFAULT '0' COMMENT '软删除',
  `DeleteBy` varchar(64) DEFAULT NULL COMMENT '删除人Code',
  `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
  `IsValid` int NOT NULL DEFAULT '1' COMMENT '1有效0无效',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  UNIQUE KEY `uk_plan_part` (`PlanCode`,`PartCode`),
  KEY `idx_plan_state` (`PlanCode`,`ItemState`),
  KEY `idx_field` (`FieldCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci COMMENT='填充计划明细（每个锚点一行，AI 产出值）';
```

> **设计要点**：`ValueText` / `ValueNumber` / `ValueDate` 三形态并存 → 铁律一「写强类型值」可直接支持，`ValueNumber` 供 `sum`/`avg` O(1) 聚合。`IsOverridden` 显式记录人工覆盖事实，守住 D9。

### 9.5 既有表扩展

| 表 | 变更 | 说明 |
|---|---|---|
| `cert_validation_rule` | `ADD COLUMN PlanCode varchar(36) DEFAULT NULL`<br>`ADD COLUMN WorkflowType varchar(20) NOT NULL DEFAULT 'validation'`（取值加 `template`）<br>`ADD KEY idx_plan (PlanCode)` | 一行 = 一个字段的流程 |
| `cert_report_template` | `ADD COLUMN ScanCode varchar(36) DEFAULT NULL`<br>`ADD COLUMN FileKind varchar(10) DEFAULT NULL` | 复用已有 `TemplateFilePath` + `SectionConfig` |
| `cert_standard_directory_file` | `ADD COLUMN ScanCode varchar(36) DEFAULT NULL` | 模板行反向锚点 |
| `cert_standard_doc_contract` | `ADD COLUMN ComputeHandler varchar(100) DEFAULT NULL`（例外扩展点） | 纯 DSL 不够时的出口 |

### 9.6 命名规范核对

| 铁律 | 满足情况 |
|---|---|
| ④ 双关键字准则 A | 4 张新表均以 `Code` 为业务键，`Id` 不进 WHERE / 关联 / 分流 |
| ③ 铁律九 `Enable` 零容忍 | 4 张表全部用 `IsValid`，无 `Enable` / `EnableField` |
| ⑧ 三处一致 PascalCase | 全部列名 PascalCase，与 C# 属性名逐字一致 |
| 铁律八 字符集 | 全部显式 `COLLATE=utf8mb4_general_ci` |

---

## 10. 验收体系

### 10.1 三层验收

| 层 | 机制 | 复用 |
|---|---|---|
| **单字段试跑** | `POST /test/node`（落库可回溯 `wf_execution_task` / `wf_node_execution`） | ✅ 已有 `WorkflowTestController.cs:91` |
| **整模板试跑** | `POST /test/run` + `TaskType=TEMPLATE_GENERATE` | ✅ 已有 `:62` |
| **人工确认** | `POST /approve`（含 `Confidence` 评分 + `ManualResult` 改值） | ✅ 已有 `:362`；`TaskExecutionModels.cs:170-185` |

### 10.2 自验收：标记样式残留（零误报的关键）

**机制**：

```
生成前：扫描器记录「哪些锚点被套了 YZH_Mark」        → 期望清零清单
生成后：再扫一遍
  ├─ 样式已改 + 值已写   → ✅ 通过
  ├─ 样式仍在            → ❌ 该锚点未命中（Token 拼错 / 流程没跑到 / 扩展行错位）
  └─ 样式改了但值为空    → ⚠️ 命中了但取数为空 → 报「缺数据源」
```

**为什么用专用样式而非颜色**（V1 建议已被否决）：

| 冲突场景 | 颜色方案 | 样式名方案 |
|---|---|---|
| 模板作者用黄色做装饰 | ❌ 误报 | ✅ 无冲突 |
| Excel 斑马纹交替底色 | ❌ 误报 | ✅ 无冲突 |
| Word 字符底纹 `w:highlight`（仅 16 色） | ❌ 需写两套检测 | ✅ 统一按样式名 |
| 复制粘贴继承标记 | ⚠️ 该清的没清 | ⚠️ 同样问题（写入后统一改样式可解） |

**判定依据是**「样式名 = `YZH_Mark`」，不是颜色值 → 装饰色/斑马纹都不冲突。

### 10.3 残留处理

| `MarkResidueJson` 内容 | 动作 |
|---|---|
| 空 | ✅ 通过 |
| 非空 | 🔴 **硬失败**，`PlanState=failed`，不给归档（对齐 `14` 号 D24「NC 入口前置校验 CompletionRatio=1.0」） |

---

## 11. 模板换版：重扫与差异继承

**这是「模板很少改变 + 维护团队」前提成立的关键配套。** 换模板不重录，全靠这一条。

```
上传新模板 → 生成新 TemplateVersion → 扫描
        ↓
按 (PartType, AnchorKind, SheetName, SectionIndex, HeaderKind, AnchorRef) 做 diff
        ↓
┌─ 锚点未变  → 继承 FieldCode / TokenModifiers / Columns / 绑定流程      ✅ 零改动
├─ 锚点消失  → 标红「锚点丢失」，对应流程置 disabled，配置保留待人工确认
├─ 锚点新增  → 标黄「未绑定」，提示需新建流程
└─ 类型变化  → 标黄「类型变更」（如 scalar → table），需人工确认绑定
        ↓
差异报告页（列表 + 颜色）→ 人工确认 → 切 IsLatest
```

**换模板成本**：命名规范不变 → **绝大多数锚点零改动**；书签名/字段名变更 → 批量修改（因一行一流程，diff 清晰）。

---

## 12. 排期切分

> ⚠️ **前置提醒**：05 册 P0 的 9 张新表一张未建（`10-实施路线:52-60` 全部 `□`）。本模块与之并行会放大 §15-5 范围风险。**建议本册 §13-A 批关闭后，先落 05 册 P0 建表。**

| 期 | 范围 | 出口门 |
|---|---|---|
| **P0** | NPOI 引入 + `TemplateScanService` + 2 张扫描表 + **规范校验（W/E 可检出项）** | 拿 1 份真实 docx 模板跑通扫描 + 规范报告 |
| **P1** | `DocxRenderer`（`doc_*` 5 Skill）+ `IWorkbookContext` + `fill_plan`/`fill_item` 2 表 + 阶段 A/B 执行器 | 单字段流程端到端跑通 + 标记样式自验收通过 |
| **P2** | 列表页 + logicflow 画布（例外覆写面）+ 验收三件套接线 | 50 字段文档一次生成成功，`UnfilledFieldCount=0` |
| **P3** | 队列化（`TemplateGenerateTaskExecutor`）+ 预取缓存 + 换版 diff 继承 | 10 份文档并发生成，成本可测 |
| **P4** | `XlsxRenderer`（`doc_table_write` Excel 分支）+ E1–E7 校验 | Excel 台账模板端到端跑通 |
| **P5** | `derive` 差距分析策略接入（`14` 号 D25） | — |

**先做 P0/P1（Word 最小闭环）再扩 P4（Excel）**，理由：P0/P1 验证的是**引擎**（扫描/流程/Skill/验收），Excel 只是多一个渲染器；引擎不通用则加渲染器无意义。

---

## 13. 待决议题登记

> **本节全部关闭前不得编码。** 分 4 批，A 批是地基。

### 批次 A — 标注与锚点（地基，必须先定）

| # | 议题 | 选项 | 影响 |
|---|---|---|---|
| **A1** | Token 语法字符集 | ① `[xxx]` ② `{{xxx}}` | ②：`[]` 与正则字符类冲突；`14` 号 D16 已定 `{{FieldCode}}` |
| **A2** | 修饰符集合 | ① 只 `fmt` ② `fmt`+`def` ③ `fmt`+`def`+`src` | ③ 的 `src:global` 承接 D28 |
| **A3** | 标记样式命名 | ① `YZH_Mark` ② 其他 | 需确认 Excel 命名样式 / Word 字符样式跨版本行为 |
| **A4** | Excel 重复区表达 | ① `{{#CODE}}` ② **区域 `anchor:range` + 样例行** | ②（V2 已改，**待确认**） |
| **A5** | 未定义 Token 行为 | ① 静默跳过 ② 黄牌 ③ **红牌阻断** | ③：对照 §15-4 的 `[buildname]` 历史教训 |

### 批次 B — 执行与验收

| # | 议题 | 选项 |
|---|---|---|
| **B1** | `IWorkbookContext` / `IExcelDoc` / `IWordDoc` 内部 API 签名 | 现有草案 |
| **B2** | 标记样式写入后处理 | 改为正文样式（V2 采纳）vs 清空；跨版本兼容 |
| **B3** | Markdown 保留期 | ① 永久 ② 画像完成后 N 天清理（填充阶段不读，§3.1 已确认） |
| **B4** | baseline 流程的生成后是否强制人工过目 | 靠自验收兜底 vs 强制确认 |
| **B5** | 未定义/多源冲突的裁决 UI | 复用 `14` 号 D26 候选面板 or 简化 |

### 批次 C — 架构与规则修订

| # | 议题 | 建议 |
|---|---|---|
| **C1** | `Skill清单 §1.3` 处置 | **改写而非删除**：删「不做报告生成/状态管理/数据源查询」；补「**Skill 状态约定**」——有状态文档操作必须走 `[FromService] IWorkbookContext`（Scoped），禁 static 缓存（**防呆约定，不是禁令**） |
| **C2** | `Skill清单 §1.2` 废弃清单 | `get_field`/`get_table` **撤销废弃**（它们是 `doc_*` 的上游数据源，原裁决"行为与功能性节点不一致"已被证明不完整）；`document_extract` **保留废弃**（该裁决正确 —— 它有独立 Service/Controller/规则表/页面） |
| **C3** | `TaskType` 新枚举值命名 | `TEMPLATE_GENERATE` |
| **C4** | `WorkflowType` 加值 | `template` |
| **C5** | `doc_convert` 归一 Skill 的输入输出契约 | `.doc`/`.xls` → `.docx`/`.xlsx` |

### 批次 D — 取值链

| # | 议题 | 选项 | 建议 |
|---|---|---|---|
| **D1** | 提取链粒度 | ① 逐字段抽取 ② **一份文档 1 次 LLM 出全部字段+表格+跨字段校验** | ②（token 省 90%+，且能做跨字段校验进 `CrossCheckJson`） |
| **D2** | `cert_doc_fill_item` 是否建表 | ① 不建，直写 `cert_extraction_result` ② **建表作为 AI↔执行器契约** | ②（可审计 + 可人工改 + 执行器不依赖 AI） |
| **D3** | 落笔层选型 | ① NPOI ② OpenXML SDK | ①（`XWPFHeaderFooter` 三类支持更全） |
| **D4** | `CrossCheckJson` 失败处理 | ① 阻断 ② 黄牌继续 ③ 交人工裁决 | 待议（对齐 D22 的 NC 前置校验思路） |

---

## 14. 决策登记

### 14.1 已定（技术事实，有代码/文档证据）

| ID | 决策 | 证据 |
|---|---|---|
| **D29** | 填充阶段**不打开企业文档**，唯一数据源 = 提取结果 + 全局参数 + 合并视图 | D3 + `README :82` |
| **D30** | 编排复用四层执行模型，**零新表**；加 `TEMPLATE_GENERATE` | `WfPathExecution.cs:11-25`；`TaskExecutionModels.cs:12` |
| **D31** | 有状态文档 Skill 走 `[FromService] IWorkbookContext`（Scoped），**Skill 契约与引擎零改动** | `SkillExecutor.cs:203-205` + `AddScoped`×2 + 队列 `CreateScope` |
| **D32** | **取值层 AI 批式 / 落笔层 NPOI 分层**；AI 产出 `fill_item` 不产出文件 | §2.1 三条硬约束 |
| **D33** | 不引入 Office 编辑器；`@vue-office/*` 仅作只读核对 | 外部调研（V1 §1.2） |
| **D34** | **格式归模板，值归平台**；平台按 `DataType` 写强类型值 | 用户原则 1 |
| **D35** | **一种模板语言（Word）+ 两种输出渲染器**（docx P0 / xlsx P1）；`.doc`/`.xls` 用 `doc_convert` 归一 | 用户原则 2 + `14` 号 D16/D25 的 Excel 必需性 |
| **D36** | **锚点分层**：标量 = Token 文本；区块 = 书签（Word）/ 区域+`total_token`（Excel）；域独立 | Token 表达不了范围；书签名使换模板配置零改动 |
| **D37** | **唯一写入者不变量** → 阶段 B 无序化、无屏障、无循环 | 阶段 A 已算完全部值（`sum` 为 O(1)） |
| **D38** | **模板设计规范 W1–W8 / E1–E7 为强制项**，可检出项由扫描器校验，违反红牌 | 用户前提「模板稳定 + 维护团队」 |

### 14.2 待拍板（源自 §13）

| ID | 决策点 | 批次 | 建议 |
|---|---|---|---|
| P30 | Token 语法字符集 | A | `{{}}` |
| P31 | 标记自验收介质 | A | **专用字符样式，非颜色** |
| P32 | Excel 重复区表达 | A | 区域 + 样例行 |
| P33 | 重复区行数上限 | B | `max_rows` 默认 500 |
| P34 | `Skill清单 §1.3` 处置 | C | 改写 + 补状态约定 |
| P35 | `get_field`/`get_table` 撤销废弃 | C | 是 |
| P36 | 提取链批式化 | D | 是 |
| P37 | `cert_doc_fill_item` 建表 | D | 是 |
| P38 | 落笔层选型 | D | NPOI |

---

## 15. 隐患台账（V2 更新）

| # | 隐患 | V1 等级 | **V2 状态** | 依据 |
|---|---|---|---|---|
| 1 | **画布配置成本爆炸**（150 节点） | 🔴 致命 | 🟢 **已消除** | 1 字段 1 画布 → 每画布 3–5 节点；画布降级为例外覆写面 |
| 2 | **跨表公式维护责任** | 🔴 高 | 🟢 **已消除** | 铁律一：平台不产生 Excel 公式 |
| 3 | 重复区插行后坐标漂移 | 🔴 高 | 🟢 **已消除** | §4.3 `total_token` 相对推导；Word 书签锚定内容不受影响 |
| 4 | 标记自验收误报（颜色方案） | 🟡 中高 | 🟢 **已消除** | 改用专用样式名判定（§10.2） |
| 5 | `{{TOKEN}}` 是第 4 处命名，守卫管不到 | 🟡 中 | 🟡 **缓解中** | 需新增 `guards.mjs` 规则（未实施）；扫描期红牌兜底 |
| 6 | 并发写同一文档（`XSSFWorkbook` 非线程安全） | 🔴 致命 | 🟢 **已消除** | 阶段 A/B 分段（§6.2） |
| 7 | 求和需屏障 | 🟡 中 | 🟢 **已消除** | `sum` 为 O(1) 纯函数，阶段 A 完成 |
| 8 | 预取漏合并人工覆盖 | 🔴 高 | 🟡 **设计已定，未实施** | §7.2 规则 1；需 `FieldOverrideJson` 合并 |
| 9 | 预取 key 错 → 全字段空 | 🔴 高 | 🟡 **设计已定，未实施** | §7.2 规则 2 命中数校验 |
| 10 | 范围蔓延（同时动 6 个子系统） | 🟡 项目级 | 🟡 **仍在** | §12 已切期；但 05 册 P0 未开工，**建议先落 05 P0** |
| 11 | 换模板成本 | 🟡 中 | 🟡 **设计已定，未实施** | §11 diff 继承；依赖 §3.3 书签名稳定 |
| 12 | Word 重复区克隆行 XML 的合并区处理 | 🟡 中 | ⚪ **未评估** | `XWPFTable` 行克隆需手动处理 `gridSpan`/`vMerge`/`tcW`，**P1 前必须先做技术验证** |

---

## 16. 证据索引

### 16.1 本项目代码

| 结论 | 位置 |
|---|---|
| 四层执行模型 | `CertPlatform.Shared/Entities/Wf/WfPathExecution.cs:11-25` |
| `TaskType` 三值 | `CertPlatform.Admin/Services/Workflow/Models/TaskExecutionModels.cs:12` |
| `[FromService]` 按请求解析 | `.../Services/Workflow/Skills/SkillExecutor.cs:203-205` |
| `AddScoped` × 2 | `.../CertPlatformAdminServiceExtensions.cs:42,51` |
| 队列每任务建 Scope | `.../Services/StandardDirectory/OfficeConvertTaskExecutor.cs:46` |
| `NodeExecutor` nodeType 分支 | `.../Services/Workflow/NodeExecutor.cs:85-100` |
| 验收三件套 | `.../Controllers/Workflow/WorkflowTestController.cs:62,91,362` |
| `ManualResult` 预留 | `.../Models/TaskExecutionModels.cs:170-185` |
| `get_field`/`get_table` 代码保留 | `.../Services/Workflow/Skills/GetFieldSkill.cs`、`GetTableSkill.cs`；`Skill清单 §11` |
| `doc2docx` 遗留逻辑 | `.../Services/StandardDirectory/OfficeConvertService.cs:446-499` |
| 无 Office 库 | 全仓 `*.csproj` 无 NPOI/EPPlus/OpenXml |
| `IExcelService` 空实现 | `src/yzh-core/YZH.Core.Stand/Interfaces/IExcelService.cs:53-60` |
| `DocFieldDef` 无 `FillTarget`/`CellPrompt` | `CertPlatform.Shared/Entities/Doc/DocFieldDef.cs` |
| `cert_report_template` 零生成 | `CertPlatform.Shared/Entities/Cert/ReportTemplate.cs` |
| 前端画布 | `cert-admin/src/pages/workflow/nc-config` + `@logicflow/core` |

### 16.2 参考项目（房产测绘系统 · 反面证据）

| 结论 | 位置 |
|---|---|
| `[token]` + 硬编码坐标机制 | `YZH架构/YZH.Stand/Helper/Excel/SheetHelper.cs:87-107` |
| **替换/赋值已隐含二分（走 `Replace` 分支）** | 同上 `:93-97`（V2 §8.3 显式化的依据） |
| **拼写错误活证据** | `.../Daos/Report/SurveyReportDao.cs:436`（`[buildname]` vs 16 处 `[build_name]`） |
| **三套不兼容 Token 语法** | Excel `[xxx]` / Word 裸文本 / Word 整句（`.../Daos/Report/YinChuanReport.cs:141-175`） |
| 8 地市 8 类 5662 行 | `.../Daos/Report/*.cs`（`wc -l` 实测） |
| 重复区手写 `i++` | `.../Daos/Report/TianShuiReportDao.cs:41-64` |
| Word 表格操作可行 | `.../Daos/Report/ShareReportDao.cs:94-150`（`CreateTable`/`MergeCells`/`CreateCellParagraph`） |

### 16.3 外部调研（2026-09-29）

| 来源 | 关键结论 |
|---|---|
| `github.com/dream-num/univer` README | Sheets/Docs 的 **import/export 属 Pro**；OSS 有公式引擎（500+ 函数）与 Docs 页眉页脚控制器 |
| `github.com/ONLYOFFICE/DocumentServer` | Community = AGPLv3 / 免费 / **up to 20 users recommended** |
| registry.npmjs.org | `@univerjs/sheets-formula` 1.0.2（MIT）、`@vue-office/excel` 1.7.14（MIT）、`luckysheet` 2.1.13（仓库已 404） |

---

## 17. 下一轮建议

**先只做批次 A（标注与锚点）**，因为：

1. A1–A5 是**地基**，定了之后 4 张表 + 12 个 Skill 的 schema 全部可推导
2. **隐患 12（Word 行克隆的合并区处理）是唯一无任何评估的技术点** —— 必须在 P1 之前做**技术验证**（写一个 30 行的 NPOI PoC，克隆带 `gridSpan`/`vMerge` 的表格行，验证样式与合并是否完整），否则 P1 排期不可信
3. A5 直接对应 §15-5 的历史教训，代价可量化

**批次 A 关闭后进批次 B**；B 关闭后 C（规则修订）与 D（取值链）可并行。

> **在此之前不动代码。** 另：05 册 P0 的 9 张新表尚未开工，建议**先落 05 册 P0 建表**，本册 P1 与之串行。

---

> **维护约定**：本文件是 V2 收敛稿。每轮讨论后更新 §14 决策登记与 §13 议题状态；§13 全部关闭后出 `-V3` 实施分册，本文件与 V1 一同降级为过程记录。
