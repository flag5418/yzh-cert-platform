# 文档填写 Skill 开发计划

> **版本**：V1.8 | **日期**：2026-10-03 | **状态**：**✅ 已批准 —— 实施中（`S-1` 已完成并已跑批 / `S0` 已完成 / `S1`+`S2` 未开工）**
> ⚠️ **V1.8 更正**：本节曾误写「`S1`+`S2` 已完成」—— 实查**证伪**：全仓 **无 `DocFillRule` 控制器**、
> `AnchorRef｜FillPrompt｜TemplateAnchor｜SectionIndex｜HeaderKind` **0 命中** ⇒ `S1`/`S2` **均未开工**。
> **★★ 配套详细设计 → `39-文档填写Skill详细设计-V1.md`**（**8 个 Skill 的签名/参数/内部步骤/边界/测试用例**）。
> 本文管「**做哪些、怎么排期、有哪些裁决**」，`39` 号 管「**每个 Skill 具体长什么样**」。
> **★ `39` 号 另有两节本文未覆盖**：**§十六 `{{}}` 的格式化设计**（`{{key:yyyy年MM月dd日}}` 怎么落笔，**Excel 已完整 / Word 是缺口** ⇒ **`Q-26`**）｜
> **§十七 AI 建议表 `cert_doc_ai_suggestion`**（**一个单元格多候选 + 人工确定数据源**，**企业资料上传期写** ⇒ **`Q-27`**）。
> **⚠️ 两文冲突时以 `39` 号 为准**（它修正了本文 3 处不一致 + 2 处实质缺陷，见 `39` 号 §一）。
> **V1.1 修订**：新增 **§十三**（用户 11 条补充核对 + **NPOI 2.7.2 全面排查** + 3 处认知纠正 + **Q-9~Q-14**），
> 并在实施顺序前插入 **`S-1`（`.doc/.xls` 归一）**。§〇~§十二 **未推翻**。
> **V1.2 修订**：新增 **§十四**（**两类 Skill 权威分类** + **填充 Skill 的建立方式** + 骨架/注册/调用链 + **Q-15~Q-17**），
> 并把 §6.2 的填充 Skill **由 3 个收敛为 2 个**（`fill_scalar`+`fill_block` → `fill_cell`）。
> **V1.3 修订**：新增 **§十五**（**3 个 AI 内容类 Skill = 一期必做核心** + 与「全文填写规则」的接线 + **Q-18~Q-20**），
> **作废 Q-13**（原建议「ai 字段/表格放二期」**是错的**），并把 `37` 号 从「⏸ 全部暂缓」改为「🟡 **部分解冻**」。
> **V1.4 修订**：新增 **§15.10**（**企业文档「两级过滤 + 统一合并」管线** = 用户第 5 轮补充；**落点全部现成、⛔ 不新建表**），
> **纠正 §15.9 里 `Q-19` 的一处错误引用**（`08` 号 **没有**召回/精排管线），细化 `Q-19` 并新增 **`Q-21`**，
> 并据实测（**画像表 0 行 / markdown 3 行中 2 行 failed**）在 §15.8 补 **`S2.5`**。
> **V1.5 修订**：**不自洽修正版** —— 用户指出「还没看到具体的设计」后，产出配套 **`39` 号**，
> 并**反向修正本文 3 处内部不一致**（**I-1** 命名两套 → §5.2 加收敛注｜**I-2** §14.2 一期/二期列过期｜**I-3** §14.2 参数列过期）
> 与 **2 处实质缺陷**（**M-1/M-2**：§14.6 的 `OfficeFillRequest`/`FillValue` 参数在 JSON 链上还原不了 ⇒ 改为 `FillSession`），
> 同时修正 §14.7 的 `ClassPath`（补 `.Fill` 命名空间段）。
> **V1.6 修订**：**交叉引用补齐**（用户第 4 轮「`{{}}` 格式化 + AI 建议独立表」已全部落进 `39` 号）——
> 文档头补 **`39` 号 §十六 / §十七** 指引；⚠️ 本文 **`Q-1`~`Q-21`** 与 `39` 号 **`Q-22`~`Q-27`** 需**合并审批**。
> **V1.7 修订**：**`S-1` 实施回填**（用户批准后开工）——
> ① 新增 **§13.1a**（归一链**真实可执行规模**：**169 份**而非 570，因 413 份 `.doc` 无源文件 ⇒ **修正验收口径**）；
> ② 新增 **§13.1b**（`S-1` 服务端实施记录 9 项，含 `editable` 产物段 / 列白名单 / 一处既有隐患修正）；
> ③ **作废 §15.10 的「Markdown 失败大概率是 LibreOffice 环境问题」判断** —— 实测容器正常，
> 失败原因是**上传了 81 字节的损坏文件**（详见 §15.10(4) 的核查表）。
> **V1.8 修订**：**开工障碍清理**（用户「我认同你的默认规则，落地先解决这几个阻碍问题」）——
> ① **`S0` 四项收尾全部落地**（`DB/mysql/phase12_doc_fill_s0.sql`，**已执行 2 次验证幂等**）：
>   **(a)** `cert_standard_doc_contract` 补 `FixedDocSubtype varchar(20) NOT NULL DEFAULT 'enterprise_provided'`；
>   **(b)** 菜单 `MENU_00218`（`标准文档填写规则` / `/business/doc-fill-rule` / `OrderNo=350`）入库并授权
>   `ROLE_ADMIN` + `ROLE_SUPER_ADMIN`；**(c)** `PathBuilder` 新增 `_template` 段（`TemplateSegment` +
>   `ReservedSegments` + `TemplateFile()` + `IsTemplatePath()`，**⛔ 刻意不进 `IsProductPath`** —— 它与
>   `editable/` 是**并列关系**而非包含关系）；**(d)** 修正 `OfficeFillModels.cs:94` 一条**与事实相反**的过期注释
>   （原文称「上传侧已拒绝 `.doc/.xls`」，实测白名单**含** `doc`/`xls`，且库内 `.doc` 570 + `.xls` 44 占 92%）。
> ② **单测**：`DocumentLibraryPathTests` **+10 用例（75 passed）**｜**全量 343 passed / 0 failed**（331 → 343）。
> ③ **★ 障碍 2（扫描器输入不存在）已消除**：`POST /api/Admin/Workflow/StandardDirectory/backfill-conversions`
>   ⇒ `scanned 185 / enqueued 172`，最终 **`completed 167` / `failed 1`**（`.doc` **156/156 ✅**，`.xls` 11/12）。
>   **`EditableStoragePath`：0 份 → 167 份** ⇒ **发现型扫描器（§15.8 `S5`）的输入已就绪**。
>   ⚠️ 唯一失败 `受控文件清单-程序文件.xls`（企业资料库）＝ soffice **退出码 0 但无产物**，**被正确检出并标
>   `failed`（非静默）** ⇒ 属**内容问题**而非代码缺陷。
> ④ **★ 障碍 1（现有扫描器是「自验收型」非「发现型」）仍未开工** —— 这是本计划的**下一步**（详见 §0.4）。
> **定位**：**`37` 号（标准文档填写规则综合页面）的前置** —— 先把「填一个字段 / 填一张表」的 Skill 做出来并验证，
> 再回头做综合编排页面。
> **来源**：用户 2026-10-03 口述 + `27` 号（规则分类）+ `22` 号（填写单元三元组）+ `Office/` 层 1（**已实现**）+ `docs/30-项目规则/Skill清单-V1.md`
> **编号说明**：本册编号被两个会话交叉写过（`26` 号 §十 已登记冲突），本文取目录最大号 **`38`**。
> **⛔ 本文是设计 + 开发计划，不是实现记录。**

---

## 〇、先对齐三件事

### 0.1 你为什么叫停 `37` 号 —— **我同意，你的判断是对的**

**你的原话**：
> 「我们应该**先设计 skill**，再来讨论标准文档填写规则的开发，因为**我们没有准备好周边的必要结构和能力**，
>  现在讨论综合页面的实现，**是有问题的**」

**为什么这个判断是对的**（不是客气）：

`37` 号设计的是**编排层** —— 左树 + 预览 + 4 页签 + 来源链编辑器。但编排层的**每个格子都要落到一个「能力」上**：

| `37` 号的 UI | 它落到的能力 | 现状 |
|---|---|---|
| Tab1「取值来源」列 | 「值从哪来」 | ❌ **能力不存在**（没有可调用的取值单元） |
| Tab1「写入方式」列 | 「怎么写进去」 | ⚠️ **`Office/` 层 1 已有**，但没有对外接口 |
| Tab2「全文填写规则」 | 「整篇怎么组织」 | ❌ 未设计 |
| Tab1′「指纹规则」 | 「怎么认这份证件」 | ⚠️ 数据结构有，能力没有 |

⇒ **编排层是「壳」，取值/写入能力是「核」。先做壳会导致：页面做完了，点「试跑」没有东西可跑。**

### 0.2 本计划与既有文档的关系

```
27 号（6 类填写规则分类）  ─┐
22 号（D1×D2×D3 三元组）   ─┼──→  38 号（本文：Skill 开发计划）  ──→  37 号（综合编排页面）
Office/ 层 1（已实现）     ─┘
```

| 文档 | 定位 | 本计划如何用 |
|---|---|---|
| `27` 号 | 「有哪些填法」（6 类，带优先级） | **直接映射为取值 Skill 清单**（§6.1） |
| `22` 号 | 「一个填写单元 = 三元组」 | `D1` → 取值 Skill；`D2`+`D3` → 填充 Skill |
| `Office/` 层 1 | NPOI 落笔（15 源文件，148/148 全绿） | **包装为填充 Skill，⛔ 不重写** |
| `37` 号 | 综合编排页面 | **本计划的下一站**，暂缓 |

### 0.3 一句话结论

> **把「取值」和「写入」拆成两级 Skill：取值 Skill 是纯函数（Word/Excel 无关），
>  写入 Skill 包装已有的 `Office/` 层 1（Word/Excel 差异在这里收敛）。
>  `37` 号的「来源链」= 取值 Skill 的有序列表。**

### 0.4 ★ 开工障碍清单（V1.8 更新：**3 条已消 2 条**）

本计划开工前实测出的 **3 大障碍**，当前状态：

| # | 障碍（实查原文） | 状态 | 处置 / 证据 |
|---|---|---|---|
| **1** | **现有扫描器是「自验收型」非「发现型」** —— `WordDocumentScanner`/`ExcelSheetScanner` 只返回 token 字符串、**无坐标**、`HashSet` 去重、`internal static`；而 `37` 号 §`7.2` 要的是**带坐标的发现型扫描**（`AnchorRef`/`SheetName`/`SectionIndex`/`HeaderKind`）⇒ **`37` 号 `Q-6`「在现有基础上扩展、⛔ 不新写一套解析」的前提不成立，必须新写**。**书签全仓 0 实现**。 | 🔴 **未开工** | **本计划的下一步**（对应 §15.8 `S5` + `37` 号 §7.2）。**输入已就绪**（见障碍 2）。 |
| **2** | **扫描器输入不存在** —— `EditableStoragePath` **全库 0 份**；`.doc` 570 + `.xls` 44 = **614 行（92%）NPOI 读不了**。 | ✅ **已消除** | 回填跑批 **`completed 167`** ⇒ `EditableStoragePath` **0 → 167 份**（`.doc` 156/156）。 |
| **3** | **后端 / 前端 / 菜单三项全零** —— grep `doc-fill｜DocFillRule｜FillPrompt｜TemplateAnchor` **0 命中**。 | ✅ **菜单已消**（后端/前端待 `S1`） | `MENU_00218` 已入库 + 授权；`S0` 表列已补齐。 |

**⇒ 结论**：**障碍 2、3 已清除，可以开始写 `S1`（控制器骨架）+ `S2`（发现型扫描器）**；
**障碍 1 是唯一实质开发任务**，且**它的输入（167 份 `editable/` 产物）已在磁盘上**。

---

## 一、现状核实（实连库 + 实读代码）

### 1.1 ★★★ Skill 体系：**权威文档与库严重不一致 —— 必须先修，否则会照错文档写代码**

**权威文档** `docs/30-项目规则/Skill清单-V1.md` §6.1 说「**已建立的 Skill（2 个）**」。
**实连库** `wf_skill` 有 **11 行**，`wf_skill_reflection` 有 **10 行**：

| SkillCode | 中文名 | 分类 | 反射登记 | 清单里怎么写的 |
|---|---|---|---|---|
| `assemble` | 数据组装 | `data_process` | ✅ | ✅ 有 |
| `compare` | 值比较 | **`ai_judge`** | ✅ | ⚠️ 清单说 `data_process`（**不符**） |
| `build_nc_prompt` | NC判定Prompt装配 | `ai_judge` | ✅ | ❌ 清单未提 |
| `get_field` | 获取字段值 | `data_access` | ✅ | ⛔ 清单 §1.2 说「**已移除**」（**不符，仍在且 active**） |
| `get_table` | 获取表格数据 | `data_access` | ✅ | ⛔ 同上（**不符**） |
| `is_field_empty` | 字段是否为空 | `ai_judge` | ✅ | ❌ 清单未提 |
| `is_std_file_missing` | 标准目录文件是否缺失 | `ai_judge` | ✅ | ❌ 清单未提 |
| `is_table_empty` | 表格是否为空 | `ai_judge` | ✅ | ❌ 清单未提 |
| `llm_extract` | LLM提取 | `ai_generate` | ❌ **未登记** | ⛔ 清单 §1.2 说「**臆想的，已废弃**」（**不符，`wf_skill` 里有且 active，但无反射配置**） |
| `nc_conclusion_calc` | NC认证结论判定 | `ai_judge` | ✅ | ❌ 清单未提 |
| `nc_validate` | NC输出校验 | `ai_judge` | ✅ | ❌ 清单未提 |

**另有 3 处结构不符**（清单 §8.1 的列名 vs 实库）：

| 清单写的列 | 实库实际列 |
|---|---|
| `skill_name` | **`Name`** |
| `category` | **`CategoryCode`** |
| `enable` | **`IsActive`** + `IsValid`（两个） |

**⇒ 结论（必须先做）**：**在写任何新 Skill 之前，先修 `Skill清单-V1.md`**（改成「库为准」的口径），
否则新 Skill 会照着过期的约定写。

> ⚠️ 这正是记忆里登记过的「Skill 体系当前**文档 ↔ 实现不一致**」（`22` 号 Q-2）—— 本轮**实证确认**。

### 1.2 ★★★ 最大的好消息：`Office/` 层 1 已是**完整对称双实现**

`src/certplatform-api/CertPlatform.Shared/Office/`（**15 源文件 + 4 测试，148/148 全绿**）：

```
OfficeFillModels.cs          ← ★ 统一模型（下面详解）
NpoiExcelService.cs

Word/                        Excel/
  WordFillWriter.cs            ExcelFillWriter.cs
  WordDocumentScanner.cs       ExcelSheetScanner.cs
  WordTableRegionFiller.cs     ExcelRegionFiller.cs
  WordParagraphFiller.cs       ExcelCellWriter.cs
  WordCellWriter.cs            ExcelRowInserter.cs
  WordRunText.cs
  WordTableRowInserter.cs
  WordFieldWriter.cs
```

**★ 用户问题 B（Word/Excel 差异）的答案已经写在代码里了**：

`OfficeFillModels.cs` 的 `OfficeFillRegion` 就是**差异的收敛点**：

| 差异 | Word 侧 | Excel 侧 | 统一字段 |
|---|---|---|---|
| 表格怎么定位 | `{{table:Tag}}` 内容标签 | `SheetName` + `StartRow` + `StartCol` | `Kind` ∈ `{WordTable, ExcelRange}` |
| 表格数据 | `Rows`（行 × 列，每格 `FillValue`） | **同一个 `Rows`** | ✅ 已统一 |
| 行数不足 | 克隆 `CT_Row`（深拷贝） | 复制行样式 | `insufficient: 'clone'` |
| 行数多余 | **置空，⛔ 不删** | **置空，⛔ 不删** | `surplus: 'blank'` |
| 列多余 | 丢弃 | 丢弃 | `surplusCol: 'drop'` |

**层 1 铁律（代码注释逐字）**：**「AI 只产值，NPOI 只落笔」** —— 层 1 **⛔ 不做任何语义判断**。

⇒ **本计划的填充 Skill 只是「给层 1 喂数据」的薄封装，不是重写。**

### 1.3 ★★★ 表格填写的**真实缺口**：列级锚点**无处落点**

`15-全册评估报告-V1.md:603` 已明确登记（**逐字**）：

> 「`16` §3.1 的表格锚点是**区域**（`Sheet1!A11:F11`）整体写入，`fill_item` 是「每个锚点一行」。
>  所以「**改表格里的某一个单元格**」目前**无处落点**。需在 `fill_item` 支持子行/子列定位
>  （或表格值以结构化形式存 + **行内列锚点**）。」

**现状核实**：

| 项 | 现状 |
|---|---|
| `OfficeFillRegion.Rows` | ✅ 二维 `List<List<FillValue?>>` —— **能承载** |
| `cert_doc_template_anchor.ColumnsJson` | ✅ 存在：「表格列顺序 `["TRAIN_DATE","TRAIN_TOPIC","HOURS"]`」 |
| **「模板里第 3 列 = 哪个 FieldCode」的落点** | ❌ **没有** —— `ColumnsJson` 只有**顺序**，没有**列锚点** |

⇒ **这就是「表格如何填写」必须补的第一件事**（§3）。

---

## 二、架构：两级 Skill

### 2.1 为什么分两级

| | **取值 Skill**（`src_*`） | **填充 Skill**（`fill_*`） |
|---|---|---|
| 回答的问题 | 「**这个格子该填什么**」 | 「**怎么把它写进 Word/Excel**」 |
| 性质 | **纯函数**（给定输入必得同一输出） | **副作用**（读写文件字节） |
| 碰文件吗 | ❌ **不碰** | ✅ 碰（NPOI） |
| **Word/Excel 差异** | ⚠️ **只有一处**：输出必须带**值类型**（§4.3） | ✅ **全部差异在此收敛** |
| 可编排 | ✅ 是（进 `37` 号来源链） | ❌ 否（引擎在填充期调用） |
| 已有实现 | ❌ 无 | ⚠️ **`Office/` 层 1 已有**，缺封装 |

**⇒ 这个分法的直接好处**：
- 取值侧**完全不用考虑 Word/Excel**（占工作量的 70%）⇒ **大幅简化**
- Word/Excel 差异**只在 3 个填充 Skill 里**出现，且**已被层 1 吸收** ⇒ 不用重新发明

### 2.2 取值 Skill（纯函数）

**契约**：输入参数（由反射分析）→ 输出 `{ value, value_kind, number_format?, source?, confidence? }`

```csharp
[Skill(Code = "src_global_param", Name = "全局参数取值",
       ReturnType = "json",
       Description = "按标准+阶段解析生效的全局参数值（含来源链去重）")]
public static class SrcGlobalParamSkill
{
    public static Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "参数编码", BindMode = SkillParamBindMode.LinkOrConstant)]
        string? param_code = null,

        [SkillParam(Description = "标准Code（空=平台级）")]
        string? standard_code = null,

        [SkillParam(Description = "阶段Code（空=全阶段）")]
        string? stage_code = null,

        [FromService] IGlobalParamResolver? resolver = null!,
        CancellationToken ct = default)
    { ... }
}
```

**★ 三条铁律**：

1. **输出必须带 `value_kind`** —— ⛔ 不允许只给字符串（否则 Excel 会把数字写成文本，**不报错但结果错**）
2. **找不到值时返回 `success=false` + 明确 `error`** —— ⛔ 不返回空串（空串与「没找到」无法区分）
3. **不做格式判断** —— `number_format` 由参数给或留空（留空 = 保留单元格原格式）

### 2.3 填充 Skill（副作用，Word/Excel 差异在此收敛）

**只有 3 个**（按 `D3 承载形态` 分组）：

| SkillCode | 对应 D3 | 内部走 |
|---|---|---|
| `fill_scalar` | `scalar` | `WordParagraphFiller` / `WordCellWriter` / `ExcelCellWriter` |
| `fill_block` | `block` | `WordParagraphFiller`（多段落） |
| `fill_table` | `table` + `table_total` | `WordTableRegionFiller` / `ExcelRegionFiller` |

**★ 关键**：`fill_table` **不接受用户指定 `region_kind`** —— 由引擎按文件实际类型（`FileKind`）自动判定。
理由：让用户选「这是 Word 表格还是 Excel 区域」是**必然配错的接口**（模板是 docx 还是 xlsx 是客观事实）。

### 2.4 ⛔ 不做的事（照 `Skill清单-V1.md` §1.3 与 `08` 号 D4 的口径）

| ⛔ 不做 | 理由 |
|---|---|
| 把「画像 / 召回 / 精排」做成 Skill | `08` 号 D4：「**确定性数据管线 ≠ 人机交互能力**」—— 它们是事件驱动管线，归队列 |
| 在取值 Skill 里读文件 | 破坏「纯函数」⇒ 不可重放、不可缓存、不可单测 |
| 在填充 Skill 里猜值 / 补值 / 转类型 | 破坏层 1 铁律「AI 只产值，NPOI 只落笔」 |
| 为 Word/Excel 各写一套取值 Skill | 取值不碰文件，**没有任何理由分叉** |

---

## 三、★ 补充 A：**表格如何填写**（完整定义）

### 3.1 三件事必须分开解决

| # | 问题 | 现状 | 本计划 |
|---|---|---|---|
| ① | **定位**：往哪张表、从哪开始写 | ⚠️ 有（`OfficeFillRegion`） | 复用 |
| ② | **列映射**：模板第 N 列 ↔ 哪个 FieldCode | ❌ **没有**（`15` 号 §603 登记的缺口） | **★ 本计划补** |
| ③ | **行数对齐**：数据 8 行、模板 3 行 | ✅ 有（克隆/置空/丢弃） | 复用 |

### 3.2 ★ 列映射的落点：**行内列锚点 `{{col:FieldCode}}`**

**约定**（模板制作规范，写进《模板标注手册》）：

```
Word 模板里的表格：
┌────────────┬──────────────┬────────┐
│ 培训日期     │ 培训主题       │ 学时    │   ← 表头行（固定文本，不填）
├────────────┼──────────────┼────────┤
│ {{col:TRAIN_DATE}} │ {{col:TRAIN_TOPIC}} │ {{col:TRAIN_HOURS}} │   ← ★ 样板行（列锚点）
└────────────┴──────────────┴────────┘
     ↑ 表格上方某处写 {{table:items}} 用于定位

Excel 模板里的区域（Sheet1!A11:C11 为样板行）：
A11: {{col:TRAIN_DATE}}   B11: {{col:TRAIN_TOPIC}}   C11: {{col:TRAIN_HOURS}}
```

**为什么用「样板行里写列锚点」而不是「配置里写列顺序」**：

| 方案 | 优点 | 缺点 |
|---|---|---|
| **A. 列锚点在模板里**（本计划） | ① 列映射**跟着模板走**，改模板即改映射，不会配置与模板脱节 ② 扫描器**零 LLM** 直接扫出来 ③ 与标量锚点**同一套语法**，学习成本 0 | 需要人工在样板行写 `{{col:}}`（已有《模板标注手册》承担） |
| B. 配置里写 `ColumnsJson` | 不用改模板 | ⚠️ **配置与模板会静默脱节** —— 有人在模板中间插了一列，配置不会变，结果**整体错位且不报错** |

⇒ **取 A**。`ColumnsJson` 保留，但语义从「列顺序的**来源**」改为「**扫描结果的快照**」（只读，供界面展示与校验）。

**⇒ 新增 `cert_doc_template_anchor` 的用法（⛔ 不加列）**：
列锚点也存成 `AnchorType='table'` 的行，`AnchorRef = '{{col:TRAIN_HOURS}}'`，`FieldCode = 'TRAIN_HOURS'`，
`Sort` = 列序号，并新增 `ParentAnchorCode` 指向表格锚点（`{{table:items}}` 那行）。
⇒ **需要 1 个新列**：`ParentAnchorCode varchar(36)`（见 §7）。

### 3.3 Word 表格填写算法（逐步）

```
① 定位表格
   扫全文找 {{table:items}} ⇒ (tableIndex, rowIndex, cellIndex)
   ⛔ 不用「第 N 张表格」定位 —— 作者插一张表格就静默错位（26 号 §3.4.6 已记）

② 确定样板行 = {{table:items}} 所在行（若该行无数据，用下一行）

③ 确定列映射
   遍历样板行各 cell，正则提 {{col:X}} ⇒ colIndex → FieldCode
   · 某 cell 无 {{col:}} ⇒ 该列不由数据填（固定文本，保留）
   · 某 cell 有多个 {{col:}} ⇒ 拼接（语义同 assemble）

④ 行数对齐
   dataRows.Count > 1  ⇒ 克隆样板行 Count-1 次，插在样板行之后
        ★ 必须 table.AddRow(new XWPFTableRow(clone, table), pos)
        ⛔ 直接改 CT_Tbl ⇒ trList=3 但 Rows.Count=2，不报错（坑 R2）
   dataRows.Count == 0 ⇒ 样板行置空（写空串），⛔ 不删行

⑤ 逐行逐列写入
   cell.Paragraphs[0].Runs[0].SetText(...)
   ⛔ 不用 cell.SetText()（同会话读回空串，坑 R3）
   ⚠️ {{col:X}} 跨 run 时走字符索引映射法（W9），⛔ 不合并 run

⑥ 合计行（{{table_total:SUM_HOURS}}）
   平台算完写入（⛔ 不让 Word 算）
```

### 3.4 Excel 表格填写算法（逐步）

```
① 定位区域
   SheetName + StartRow + StartCol（来自 AnchorRef = 'Sheet1!A11:F11'）
   ⛔ null/空 SheetName = 第一个工作表

② 确定列映射
   样板行（StartRow）各 cell 文本提 {{col:X}}；或表头行（StartRow-1）文本 vs ColumnsJson 顺序

③ 行数对齐
   dataRows.Count > 模板行数 ⇒ sheet.ShiftRows(...) 插入
        ⚠️★ 区外内容会被一起下移（坑 R8）
        ⇒ 模板约定：数据区下方⛔ 不能有别的内容
   dataRows.Count < 模板行数 ⇒ 多余行置空（⛔ 不删行）

④ 逐格写入
   按 FillValue.Kind 走重载：
     Text   → SetCellValue(string)
     Number → SetCellValue(double) + NumberFormat
     Date   → SetCellValue(DateTime) + ★必须显式 DataFormat（否则显示成序列号）
     Bool   → SetCellValue(bool)
   ⚠️★ 传错重载不报错但结果错：数字写成文本后「1,000」左对齐、不能求和、打开看不出异常

⑤ 合计行
   平台算（⛔ 不用 Excel 公式 —— 避免「打开时重算」与「平台算」两套口径不一致）
```

### 3.5 表格填写的**数据契约**（Skill 之间的传递格式）

```json
{
  "table_tag": "items",
  "columns": [
    { "field_code": "TRAIN_DATE",  "kind": "date",   "format": "yyyy-mm-dd" },
    { "field_code": "TRAIN_TOPIC", "kind": "text" },
    { "field_code": "TRAIN_HOURS", "kind": "number", "format": "0.0" }
  ],
  "rows": [
    { "TRAIN_DATE": "2026-03-01", "TRAIN_TOPIC": "质量意识", "TRAIN_HOURS": 4 },
    { "TRAIN_DATE": "2026-04-15", "TRAIN_TOPIC": "内审技巧", "TRAIN_HOURS": 8 }
  ],
  "totals": { "TRAIN_HOURS": 12 }
}
```

**★ 关键**：`rows` 是**对象数组**（带 `field_code` 键），⛔ **不是**裸二维数组。
理由：对象数组**能容忍列顺序变化**（模板插一列不影响取值）；裸二维数组一旦错位就是**静默写错值**。

**⇒ 二维数组的转换由 `fill_table` 内部完成**（用 `columns` 的顺序），⛔ 不让取值 Skill 拼。

### 3.6 表格填写的已知坑（照实报出）

| # | 坑 | 后果 |
|---|---|---|
| T1 | `XWPFTable.Rows` 是**缓存列表** | 直接改 `CT_Tbl` ⇒ `trList` 变了但 `Rows.Count` 不变，**不报错** |
| T2 | `XWPFTableCell.SetText()` 后同会话读回**空串** | 扫描器/校验误判 |
| T3 | Excel `ShiftRows` 下移**区外内容** | 模板数据区下方有内容 ⇒ 被推走 |
| T4 | Excel 值**类型重载传错** | 数字变文本，**不报错但结果错**，打开看不出 |
| T5 | Word 表格**序号定位** | 作者插表 ⇒ 静默错位（⛔ 必须用内容标签） |
| T6 | `{{col:}}` **跨 run** | 扫描找得到、写入找不到（W9） |
| T7 | 合并单元格 | Word `gridSpan`/`vMerge` 与 Excel `mergedRegions` 语义不同；**归模板控制，⛔ 不重设** |

---

## 四、★ 补充 B：**Word / Excel 差异矩阵**

### 4.1 差异全表（含收敛点）

| 维度 | Word (`.docx`) | Excel (`.xlsx`) | **收敛点** | 状态 |
|---|---|---|---|---|
| 标量定位 | `{{Token}}`（跨 run）/ 书签 | 单元格坐标 / 命名区域 | `AnchorKind` | ✅ 已有 |
| 表格定位 | `{{table:Tag}}` 内容标签 | `SheetName` + `StartRow/StartCol` | `OfficeFillRegion.Kind` | ✅ 已有 |
| **表格列映射** | 样板行 cell 的 `{{col:X}}` | 样板行 cell 的 `{{col:X}}` | **同一约定** | ★ **本计划补** |
| 行数不足 | 克隆 `CT_Row`（深拷贝） | 复制行样式 / `ShiftRows` | `insufficient: 'clone'` | ✅ 已有 |
| 行数多余 | 置空，⛔ 不删 | 置空，⛔ 不删 | `surplus: 'blank'` | ✅ 已有 |
| 列多余 | 丢弃 | 丢弃 | `surplusCol: 'drop'` | ✅ 已有 |
| **值类型** | 文本为主 + 域 | **string / double / DateTime / bool 四种重载** | **`FillValueKind`** | ⚠️ **已有，但 Skill 层必须遵守** |
| 数字格式 | 不适用（样式在模板） | `NumberFormat` | `FillValue.NumberFormat` | ✅ 已有 |
| 页眉 | **只替换，⛔ 不新建** | 页眉是 sheet 概念 | `FillHeader` | ✅ 已有 |
| 页脚 | **不做** | 不做 | — | ✅ 已定 |
| 域 | `{{PAGE}}` 交给 Word 算 | 不适用 | `FillValueKind.Field` | ✅ 已有 |
| 合并单元格 | `gridSpan` / `vMerge` | `mergedRegions` | **模板控制，⛔ 不重设** | ✅ 已定 |
| 样式 | **run 级**（⛔ 不合并 run） | cell style | **模板控制** | ✅ 已定 |
| 文本归一化 | **W9 字符索引映射法** | 无此问题 | `WordRunText` | ✅ 已有 |

### 4.2 ★★ 差异**泄漏到 Skill 层**的只有两处（这是本节的核心结论）

#### 泄漏点 ①：**取值 Skill 必须输出带类型的值**

```csharp
// ✅ 正确 —— 带 value_kind
return SkillResult.Ok(new {
    value = 4.0, value_kind = "number", number_format = "0.0", source = "培训记录表·学时"
});

// ⛔ 错误 —— 只给字符串
return SkillResult.Ok(new { value = "4.0" });
```

**为什么必须这样**（`OfficeFillModels.cs` 逐字注释）：

> 「NPOI 的 `ICell` 有一组重载（`SetCellValue(string)` / `SetCellValue(double)` / `SetCellValue(DateTime)`），
>  传错重载**不报错但结果错** —— 数字写成文本后 Excel 里「1,000」会变成左对齐的字符串，
>  既不能求和也不能排序，而且**打开文件看不出异常**。
>  ⇒ 类型由**层 2（值字典）**显式给出，写入器**只按类型落笔，不做任何推断**。」

⇒ **取值 Skill 是「层 2」的一部分，所以它必须给类型。这是「所有 Skill 都要考虑 Word/Excel 差异」的第一处落地。**

#### 泄漏点 ②：**`fill_table` 的定位参数形状不同**

```csharp
// Word：给 TableTag
// Excel：给 SheetName + StartRow + StartCol
// ⇒ 由 fill_table 内部按 FileKind 分派，⛔ 不暴露给用户配
```

### 4.3 ⛔ 不要试图统一的 5 处（统一了会更糟）

| 不统一 | 理由 |
|---|---|
| 表格定位方式 | Word 是**流式**（无坐标），Excel 是**坐标式**。强行统一成坐标 ⇒ Word 侧要维护脆弱的「第 N 张表」 |
| 行插入机制 | Word 克隆行对象，Excel `ShiftRows` —— 底层模型不同，统一接口即可，不必统一实现 |
| 文本跨 run | **Word 独有**问题，Excel 没有。给它做抽象是**凭空造复杂度** |
| 数字格式 | Excel 需要 `NumberFormat`，Word 不需要。**参数可选**即可，不必抽象 |
| 域 | Word 独有（`PAGE`/`NUMPAGES`） |

---

## 五、★ 补充 C：**综合填写规则 ↔ Skill 的关联契约**

### 5.1 一句话答案

> **`22` 号 `SourceSpec.sources[]` 的每一项，从「枚举 kind」升级为「`SkillCode` 引用 + 参数字典」。**

### 5.2 契约变更（**这是本计划对 `22` 号的唯一实质修改**）

**现状**（`22` 号 §三，`kind` 是枚举）：

```json
{
  "combine": "firstHit",
  "sources": [
    { "kind": "global",  "paramCode": "ENT_NAME", "onMissing": "next" },
    { "kind": "profile", "fieldCode": "ENT_NAME", "onMissing": "next" },
    { "kind": "manual",  "onMissing": "todo" }
  ]
}
```

**升级后**（`skill` 是 `SkillCode`，参数是字典）：

```json
{
  "combine": "firstHit",
  "separator": "",
  "expr": "",
  "sources": [
    { "skill": "src_global_param", "params": { "param_code": "ENT_NAME" }, "onMissing": "next" },
    { "skill": "src_ai_field",     "params": { "anchor_code": "ENT_NAME", "instruction": "企业全称" }, "onMissing": "next" },
    { "skill": "src_manual",       "params": { "hint": "请填写企业名称" }, "onMissing": "todo" }
  ]
}
```

> **⚠️ 命名已收敛（`39` 号 §2.2）**：本段原用 `src_from_profile` / `src_related_doc` / `src_self` / `src_sibling` / `src_compute`，
> 与 §14.2/§十五 的命名**是两套**（= `39` 号 登记的 **I-1**）⇒ 统一裁定：
> `src_from_profile` / `src_related_doc` → **`src_ai_field`**｜`src_self` / `src_sibling` → **`src_global_param`**（同表不同参数）｜
> `src_compute` → **一期不做**（`Q-24`）｜`src_manual` **保留**。**迁移映射全表见 `39` 号 §2.2。**

**兼容性**：`kind` 枚举值 → `skill` 的一一映射表（**迁移脚本一次性转换，`kind` 列保留只读**）：

| 旧 `kind` | 新 `skill` |
|---|---|
| `global` | `src_global_param` |
| `profile` | `src_from_profile` |
| `self` | `src_self` |
| `sibling` | `src_sibling` |
| `compute` | `src_compute` |
| `ai` | `src_ai_generate` |
| `manual` | `src_manual` |
| —（`27` 号新增） | `src_related_doc` |
| —（`27` 号新增） | `src_dict_pick` |

### 5.3 参数 UI **由反射驱动**（⛔ 不写死）

```
前端拿到 anchor.SourceSpec
  → 每个 source 的 skill 查 wf_skill_reflection
  → 调 POST /api/skill/analyze 拿参数元数据（name/type/required/default/bindMode/enumSource/description）
  → 按 BindMode 渲染：
      LinkOrConstant → 输入框（可切「连线 / 常量」）
      Enum           → 下拉（从 EnumSource 字典加载）
      Link           → 只读 + 连线提示
```

**★ 收益**：**新增一种取值方式 = 注册一个 Skill，⛔ 不改页面代码。**

**⇒ 这就是 `37` 号「来源链编辑器」的实现方式**：它不是「自定义 UI」，而是 **`WorkflowDesigner` 的参数绑定 UI 的复用**。

### 5.4 ⛔ 为什么**不用画布**（`WorkflowDesigner`）而用「有序链」

| | 画布（数据流图） | 来源链（有序列表） |
|---|---|---|
| 表达 | 并行、多入多出 | **有序回退**（第 1 个命中就用，否则试第 2 个） |
| 本场景 | ❌ 表达不了「有序」 | ✅ 天然表达 |
| 复用 | — | ✅ **只复用参数绑定渲染**，不复用画布 |

⇒ `37` 号的编辑器 = **有序列表 + 每项展开为 Skill 参数表单**。画布的「节点/连线」概念**不要引入**。

---

## 六、Skill 清单

### 6.1 取值 Skill（`src_*`，纯函数）—— 9 个，**一期做 4 个**

| # | SkillCode | 对应 `27` 号 | 对应 `22` 号 D1 | 核心端口 | 一期 |
|---|---|---|---|---|---|
| 1 | `src_global_param` | ① 全局参数自动替换（**最高**） | `global` | `param_code`, `standard_code`, `stage_code` | ✅ |
| 2 | `src_from_profile` | ② 关联文档提取（企业侧） | `profile` | `field_code`, `enterprise_code`, `stage_code` | ✅ |
| 3 | `src_compute` | ③ 规则推断（**中**） | `compute` | `rule_code`(Enum), `inputs`(json) | ✅ |
| 4 | `src_manual` | ⑥ 人工录入（**兜底**） | `manual` | `hint`, `default_value` | ✅ |
| 5 | `src_related_doc` | ② 关联文档提取（标准文档侧） | — | `upstream_doc_code`, `field_code` | ⏸ 二期 |
| 6 | `src_dict_pick` | ④ 字典枚举选择 | — | `dict_no`, `cascade_path` | ⏸ 二期 |
| 7 | `src_ai_generate` | ⑤ AI 辅助生成 | `ai` | `prompt_code`, `context`(json) | ⏸ 二期 |
| 8 | `src_self` | — | `self` | `anchor_code` | ⏸ 二期 |
| 9 | `src_sibling` | — | `sibling` | `anchor_code` | ⏸ 二期 |

**一期只做 4 个的理由**（对齐 `37` 号 Q-7）：`25` 号 167 份实测显示一期实际只用 `global`/`compute`/`manual` 三类，
加 `src_from_profile` 是因为**固定文档抽值回流全局参数**这条链（`14` 号 §D16）必须打通。

### 6.2 填充 Skill（`fill_*`，操作类）—— **2 个**（★ V1.1 由 3 个收敛为 2 个）

| # | SkillCode | 输入 | 产出 | 内部走 |
|---|---|---|---|---|
| 1 | `fill_cell` | 值字典 + `fill_header` | `OfficeFillRequest.Values` 片段 | `WordParagraphFiller` / `WordCellWriter` / `ExcelCellWriter` / `WordFieldWriter` |
| 2 | `fill_table` | 表格数据（`columns`/`rows`/`totals`） | `OfficeFillRequest.Regions` 片段 | `WordTableRegionFiller` / `ExcelRegionFiller` |

**★ V1.1 为什么把原 `fill_scalar` + `fill_block` 合并为 `fill_cell`**：
层 1 的 `WordParagraphFiller` / `ExcelCellWriter` **完全不区分值的长短** —— 两者接收的都是**同一个 `FillValue`**，走**同一条路径**。
「短文本 / 长文本块」是**值的内容差异，不是写入方式的差异** ⇒ 拆成两个 Skill **没有任何技术依据**，只会让编排层多一个分支。
（用户 2026-10-03 第 4 条亦明确：「**严格来讲没有替换问题**」——长短文本的标识都是 `{{}}`。）

**⛔ 填充 Skill 不进来源链** —— 它们由**编排器**在**填充期**调用，不在**配置期**编排（见 §十四）。

**⚠️ V1.1 重要澄清**：`fill_*` **不做 IO、不落盘** —— 它们是**声明式装配器**。
理由与完整设计见 **§十四.5**（「每个 Skill 各写一次文件」会破坏层 1 的执行顺序不变量）。

### 6.3 与现有 11 个 Skill 的关系

| 现有 | 关系 |
|---|---|
| `get_field` / `get_table` | ⚠️ **与新取值 Skill 职责重叠**（它们做「查库取值」）。`Skill清单-V1.md` §1.2 说它们**已移除但库里还在** ⇒ **裁决 Q-1** |
| `llm_extract` | ⚠️ 与 `src_ai_generate` 部分重叠；且**无反射配置**（不可用）⇒ **裁决 Q-2** |
| `compare` / `assemble` | ✅ **正交，可复用**（`assemble` 正好服务 `SourceSpec.combine='concat'`） |
| `is_field_empty` / `is_table_empty` | ✅ 正交，可用于 `onMissing` 判定 |
| NC 类 5 个 | ✅ 正交 |

---

## 七、数据模型改动

### 7.1 新增 1 列（锚点表）—— ⚠️ V1.2 修正：**锚点表本身也还没建**

**实测**：`cert_doc_template_anchor` **在库里不存在**（`information_schema` 查 `cert_doc%` 只有 4 张：
`cert_doc_extraction_rule` / `cert_doc_field_def` / `cert_doc_table_def` / `cert_doc_table_field_def`）。
它是 `37` 号 §4.2 **表 2 的设计**（标注 ❌ 未建）。

⇒ **`ParentAnchorCode` 不是 `ALTER`，而是并入 `37` 号 表 2 的 `CREATE TABLE`**（省一次 `ALTER`）：

```sql
-- 并入 37 号 §4.2 表 2 的 DDL（⛔ 不要写 ALTER —— 表还没建）
`ParentAnchorCode` varchar(36) DEFAULT NULL
  COMMENT '★父锚点 → 本表 Code。表格列锚点指向表格锚点({{table:Tag}})；非表格锚点为 NULL',
...
KEY `idx_parent` (`ParentAnchorCode`)
```

**为什么需要**：列锚点（`{{col:X}}`）必须挂在表格锚点（`{{table:items}}`）之下，否则无法区分「同一个 FieldCode 出现在两张表里」。

⚠️ **连带影响**：`37` 号 §4.2 的表 1/2/3（`cert_doc_template` / `cert_doc_template_anchor` / `cert_doc_fill_prompt`）
**三张全部未建** ⇒ **`S-1`/`S1` 的建表清单要一并包含它们**（见 §十五.8）。

### 7.2 新增 1 张表（Skill 调用留痕）

```sql
CREATE TABLE `cert_doc_fill_log` (
  `Id`            bigint       NOT NULL AUTO_INCREMENT,
  `Code`          varchar(36)  NOT NULL                COMMENT '业务键GUID',
  `TemplateCode`  varchar(36)  NOT NULL                COMMENT '模板 → cert_doc_template.Code',
  `AnchorCode`    varchar(36)  DEFAULT NULL            COMMENT '锚点 → cert_doc_template_anchor.Code',
  `SkillCode`     varchar(100) NOT NULL                COMMENT '调用的 Skill',
  `ParamsJson`    json         DEFAULT NULL            COMMENT '入参快照',
  `OutputJson`    json         DEFAULT NULL            COMMENT '出参快照（含 value_kind）',
  `Success`       tinyint(1)   NOT NULL DEFAULT 0,
  `Error`         varchar(1000) DEFAULT NULL,
  `DurationMs`    int          DEFAULT NULL,
  `CreateTime`    datetime     NOT NULL,
  `CreateBy`      varchar(64)  DEFAULT NULL,
  `IsDeleted`     tinyint(1)   NOT NULL DEFAULT 0,
  `IsValid`       int          NOT NULL DEFAULT 1,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `uk_code` (`Code`),
  KEY `idx_tpl_anchor` (`TemplateCode`, `AnchorCode`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci
  COMMENT='填写 Skill 调用留痕（调试与审计）';
```

**为什么需要**：取值是**多源回退链**，出问题时必须能回答「**最终这个值是从第几个 source 来的、为什么**」。
⇒ 这是「一键看证据摘要」在取值侧的数据基础。

### 7.3 ⛔ 不改的（避免重复造）

| 表 | 原因 |
|---|---|
| `Office/` 层 1 全部模型 | 已完整，本计划只包装 |
| `cert_fill_param_def` / `_value` | 已有（20 行），`src_global_param` 直接读 |
| `wf_skill` / `wf_skill_reflection` | 复用，只新增行 |

---

## 八、接口设计

| # | 端点 | 方法 | 作用 |
|---|---|---|---|
| 1 | `/api/Workflow/DocFillSkill/list-sources` | GET | 列出可编排的取值 Skill（含反射参数元数据） |
| 2 | `/api/Workflow/DocFillSkill/resolve` | POST | **单锚点试跑**：入参 `{templateCode, anchorCode}` → 返回值 + 命中链 |
| 3 | `/api/Workflow/DocFillSkill/fill-preview` | POST | **整模板试跑**：返回 `OfficeFillReport`（不落盘） |
| 4 | `/api/Workflow/DocFillSkill/fill` | POST | 正式填充（落盘 + 归档） |
| 5 | `/api/Workflow/DocFillSkill/scan-table-columns` | POST | 扫描表格列锚点 → 回填 `ParentAnchorCode` |

> 端点 2/3/5 是**本计划最有价值的交付**：它们让「表格怎么填」**当场可验证**，
> 而不用等 `37` 号页面做完。

---

## 九、实施顺序

| 阶段 | 内容 | 可独立验证？ | 依赖 |
|---|---|---|---|
| **S0** | ★ **修 `Skill清单-V1.md`**（改成「库为准」+ 补 9 个已存在 Skill + 修正列名） | ✅ 纯文档 | — |
| **S1** | `cert_doc_fill_log` 建表 + `ParentAnchorCode` 加列 | ✅ 建表 | — |
| **S2** | **取值 Skill 骨架**：`src_global_param` + `src_manual`（最简单两个，先跑通链路） | ✅ 单元测试 | S1 |
| **S3** | **`fill_scalar` + `fill_block`**（包装层 1，Word + Excel 各 1 例） | ✅ 拿真模板写值 | S2 |
| **S4** | ★ **表格列锚点扫描**（`{{col:X}}` → `ParentAnchorCode`） | ✅ 扫出列清单 | S1 |
| **S5** | ★ **`fill_table`**（Word 表格 + Excel 区域，含行数对齐） | ✅ 8 行数据填进 3 行模板 | S3 + S4 |
| **S6** | `src_from_profile` + `src_compute` | ✅ 端到端填完一份 | S5 |
| **S7** | 试跑端点（§8 的 2/3/5） | ✅ 界面上能点 | S6 |

**⚠️ 建议 S3 之前先做 spike**：拿 **1 份真 docx + 1 份真 xlsx**，各做一个「**表格列锚点 + 8 行数据填进 3 行模板**」的最小验证。
**理由**：S5 是本计划风险最高的一步（跨 run + 行克隆 + 类型重载三个坑叠加），**先验证再投入**。

---

## 十、待裁决

| ID | 决策点 | 选项 | **建议** |
|----|--------|------|---------|
| **Q-1** | 现有 `get_field` / `get_table` 怎么办（库里有、清单说已移除） | A 保留，与新 `src_*` 并存<br>B 从 `wf_skill` + `wf_skill_reflection` **移除**（照清单口径）<br>C 改名迁移为 `src_from_profile` / `src_related_doc` | **B** —— 它们做的是「查库取值」，与 `src_*` 职责重叠；且清单已判定「行为与功能性节点不一致」。保留会形成两套取数路径 |
| **Q-2** | `llm_extract` 怎么办（`wf_skill` 有、无反射配置 ⇒ 不可用） | A 补反射配置启用<br>B **清理掉**（照清单 §1.2「臆想的，已废弃」） | **B** —— 无反射配置说明从未跑通；AI 取值由 `src_ai_generate` 承担 |
| **Q-3** | 取值 Skill 的输出契约 | A 只给 `value`（字符串）<br>B **`value` + `value_kind` + `number_format`** | **B** —— ⛔ 只给字符串会让 Excel 数字变文本，**不报错但结果错**（§4.2） |
| **Q-4** | 表格列映射的落点 | A 配置里写 `ColumnsJson`<br>B **模板样板行写 `{{col:FieldCode}}`** | **B** —— 配置与模板会静默脱节（§3.2） |
| **Q-5** | `SourceSpec` 是否从枚举升级为 `SkillCode` | A 保持枚举（7 类写死）<br>B **升级为 SkillCode + 参数字典** | **B** —— 这是「综合规则 ↔ Skill 关联」的唯一实现方式（§5）；且新增取值方式不用改代码 |
| **Q-6** | 填充 Skill 是否注册进 `wf_skill` | A 注册<br>B 不注册（独立 Service） | **A** —— 获得统一执行器 + 反射元数据 + 统一错误包装；但**不进来源链**（§6.2） |
| **Q-7** | 一期取值 Skill 范围 | A 全 9 个<br>B **4 个**（`global`/`profile`/`compute`/`manual`） | **B** —— `25` 号 167 份实测（§6.1） |
| **Q-8** | 是否**先修 `Skill清单-V1.md`** 再写代码 | A 先修<br>B 边写边修 | **A** —— 现在照文档写会写错列名（`skill_name` 实际是 `Name`） |

> ⚠️ **以上 8 条全部有建议值**。若你只回「都按建议」，本计划即可进入实施。
> ⚠️ **V1.1 追加 6 条（Q-9~Q-14）→ 见 §十三.5**，其中 **Q-13 已被用户推翻（见 §十五.1，作废）**。
> ⚠️ **V1.2 追加 3 条（Q-15~Q-17）→ 见 §十四.11**（填充 Skill 的形态，`Q-15` 是关键）。
> ⚠️ **V1.3 追加 3 条（Q-18~Q-20）→ 见 §十五.9**（AI 三件套范围 / 文档召回 / `37` 号解冻）。

---

## 十一、风险与已知坑

### 11.1 高危

| # | 风险 | 后果 | 规避 |
|---|---|---|---|
| R1 | **照过期的 `Skill清单-V1.md` 写代码** | 列名/分类写错，**运行时才炸** | **S0 先修文档**（Q-8） |
| R2 | **表格列锚点与模板脱节** | 整体错位，**不报错** | 列锚点写模板里（Q-4），并加**校验**：`ColumnsJson` 快照 vs 实际扫描结果不一致 ⇒ 黄牌 |
| R3 | **跨 run + 行克隆 + 类型重载三坑叠加** | 表格填错，**不报错** | S3 前 spike（§9） |
| R4 | Excel `ShiftRows` 下移区外内容 | 模板被推走 | 模板约定 + 校验「数据区下方为空」 |
| R5 | 取值 Skill 只给字符串 | Excel 数字变文本，**打开看不出** | Q-3 强制 `value_kind` |

### 11.2 中危

| # | 风险 | 说明 |
|---|---|---|
| R6 | `OfficeFillRegion` **没有列定义** | 本计划**刻意不加**（保持层 1「不做语义判断」铁律）⇒ 列映射由 `fill_table` 内部用 `columns` 完成 |
| R7 | 取值 Skill 的**缓存** | 同一模板多个锚点会重复取同一个全局参数 ⇒ 建议在引擎层做**单次填充内的 Memo**，⛔ 不做跨请求缓存 |
| R8 | `src_ai_generate` 的**成本** | 一个模板 42 个锚点若都走 AI ⇒ 42 次调用。**建议**：`ai` 只用于 `block` 类型，且必须人工确认后才落库 |

### 11.3 待核实（实施前先核）

| # | 项 | 说明 |
|---|---|---|
| V1 | `wf_skill_reflection.ParamBinding` 的实际用途 | 10 行里 9 行是 `{}` 或 `NULL`，只有 `compare` 是 `NULL`。需确认它是否已在用 |
| V2 | `wf_skill` 分类字典 `skill_category` 的项 | 实查未见 `data_access`/`ai_generate` 之外的完整清单，需核 |
| V3 | `llm_extract` 为何无反射配置 | 确认是「从未跑通」还是「走另一条路」 |

---

## 十二、证据索引

| 结论 | 证据 |
|------|------|
| `Office/` 层 1 = 15 源文件 + 4 测试 | `src/certplatform-api/CertPlatform.Shared/Office/**` 实读 |
| `OfficeFillRegion` 是 Word/Excel 差异收敛点 | `Office/OfficeFillModels.cs` `OfficeRegionKind`（`ExcelRange`/`WordTable`） |
| 「AI 只产值，NPOI 只落笔」铁律 | 同上，`FillValue` 类注释逐字 |
| Excel 类型重载坑 | 同上，`FillValueKind` 类注释逐字 |
| 行不足克隆 / 行多余不删 / 列多余丢弃 | 同上，`OfficeFillRegion` 类注释逐字 |
| **列级锚点无处落点** | `15-全册评估报告-V1.md:603` 逐字 |
| `wf_skill` 11 行 / `wf_skill_reflection` 10 行 | `information_schema` + `SELECT *` |
| `Skill清单-V1.md` 说「2 个」 | `docs/30-项目规则/Skill清单-V1.md` §6.1 |
| 清单列名与实库不符 | 清单 §8.1 `skill_name`/`category`/`enable` vs 实库 `Name`/`CategoryCode`/`IsActive` |
| `cert_doc_template_anchor.ColumnsJson` 存在 | `37` 号 §4.2 表 2 DDL |
| `27` 号 6 类规则与优先级 | `27-文档填写规则分类与开源方案适配分析-V1.md` §一 |
| `22` 号 `SourceSpec` 有序回退链 | `22` 号 §三 + §八 |
| `08` 号 D4（管线不注册 Skill） | `08-技能与提示词设计-V1.md` §1 |
| Skill 编写规范（静态类/反射/特性/BindMode） | `docs/30-项目规则/Skill清单-V1.md` §二~§五 |

---

## 十三、★★★ 用户 11 条补充的核对结论（2026-10-03 追加，含 NPOI 全面排查）

> **本节是 §〇~§十二 的补充，不推翻任何结论。** 新增 **Q-9 ~ Q-14** 六条裁决。
> 所有「实测」均指 2026-10-03 实读代码 / 实查 NPOI 2.7.2 包 / 实连库。

### 13.1 ⛔⛔ 本轮最重要的发现：**92% 的标准模板当前根本填不了**

实查 `cert_standard_directory_file`（`IsValid=1`，668 行）扩展名分布：

| 扩展名 | 份数 | 占比 | ★ 有 `StoragePath` | 填充引擎（NPOI）能处理？ |
|---|---|---|---|---|
| **`.doc`** | **570** | **85.3%** | **157** | ❌ **不能**（见 13.2） |
| **`.xls`** | **44** | 6.6% | **12** | ❌ 不能（当前未接 HSSF） |
| `.docx` | 33 | 4.9% | 10 | ✅ |
| `.xlsx` | 20 | 3.0% | 5 | ✅ |
| `.txt` | 1 | 0.1% | 1 | — |
| **合计** | **668** | 100% | **185** | — |

⇒ **`.docx` + `.xlsx` 合计 53 份 = 7.9%**。**其余 92% 的文件，填充链路一行也填不了。**

⇒ **用户第 2 条（下载时自动 doc→docx / xls→xlsx）不是「优化项」，是「前置阻塞项」。**
不解决归一，「上传→填充」这条路对 92% 的模板直接不可达。

#### 13.1a ★ 归一链的**真实可执行规模**（2026-10-03 实施时补测，**修正验收口径**）

上表只按**扩展名**统计。「能归一」还要求**有源文件**（`StoragePath` 非空）—— 否则无字节可转：

| 分类 | `.doc` | `.xls` | 说明 |
|---|---|---|---|
| **模板行**（`EnterpriseCode = YZH-STD-ENT`） | **143 / 143 全有源文件** ✅ | — | ★ **填写引擎的输入，100% 可归一** |
| **企业行**（真实 `EnterpriseCode`） | 14 / 427 有源文件 | 12 / 44 有源文件 | 413 份 `.doc` 是**企业槽位定义行**（尚未实际上传文件）⇒ 无字节可转 |
| **需归一合计** | **157** | **12** | **= 169 份** |

**⇒ ⛔ 原验收标准「570 份 `.doc` 能产出 `.docx`」不可达且口径错误** —— 570 里有 413 份根本没有源文件。
**⇒ ✅ 正确口径**：**「有源文件的旧格式文件（169 份 = 模板 143 + 企业 26）全部产出 `.docx`」**；
其中**模板 143 份 100% 覆盖** ⇒ **关键路径（模板 → 填写引擎）无缺口**。

**⇒ 为什么这条区分重要**：`.docx/.xlsx` 53 份中只有 15 份有源文件 ——
所以「92% 的文件填不了」按**文件数**成立，但按**「已上传且需归一」**算，
缺口是 `169 / (169 + 15) = 92%` **恰好仍成立**（巧合，但结论一致）。

#### 13.1b ★ `S-1` 实施记录（2026-10-03，服务端已完成）

| # | 动作 | 状态 |
|---|---|---|
| ① | 建表：`cert_doc_template` / `_template_anchor` / `_fill_prompt` / `_fill_log` / `_ai_suggestion` | ✅ `DB/mysql/phase11_doc_fill_tables.sql` 已执行，5 表 + 4 列入库 |
| ② | `cert_standard_directory_file` 加 4 列（`EditableStoragePath`/`EditableStatus`/`EditableMessage`/`EditableDate`） | ✅ 镜像既有 `Markdown*` 四列 |
| ③ | `PathBuilder` 新增 **`editable` 产物段**（与 `pdf/`、`markdown/` 对称） | ✅ 含 `ReservedSegments` 登记 + `IsProductPath` 判定 |
| ④ | 归一链：`OfficeConvertService.ConvertToEditableAsync` + `EditableChainColumns` 列白名单 | ✅ 复用 `IFileConvertCore.ConvertToFormatAsync`（⛔ 未重写转换能力） |
| ⑤ | 入队：`BuildConvertPayloads` / `BuildBackfillTasks` 加第三条链（**仅旧二进制格式**） | ✅ 判据收口 `EditableTargetFormat`（唯一一处） |
| ⑥ | 替换文件时作废归一产物（`InvalidateProductsAsync`） | ✅ 防「填写引擎读到替换前内容且状态 completed」的静默错误 |
| ⑦ | 单元测试 | ✅ **331 passed / 0 failed**（301 → 331，新增 30） |
| ⑧ | 容器端到端实测（真实 `.doc`/`.xls`） | ✅ 见 §13.6(4) 的实测表 |
| ⑨ | 重启后端 + 跑一次真实批量回填（169 份） | ⏸ **待执行**（运维动作，需后端重启） |

**三条链并列后的并发安全**：`EditableChainColumns` **刻意不含 `IsValid`** ——
该列属于预览链（两条链都写会出现「提取成功置 1、而 PDF 还在转」的假就绪，既有注释已记录）。
另**修正了 `BackfillConversionsAsync` 的一处既有隐患**：置 `pending` 原用 `UpdateAsync(f)` **全列写回**，
而入队后队列可能**立即执行**并把 `EditableStatus` 改成 `converting` ⇒ 会被覆盖回旧值且零报错；
现改为**列级写回**（只写本次投递的链的列 + `UpdateTime`）。

#### ⚠️ 顺带纠正一处**与事实相反的代码注释**

`CertPlatform.Shared/Office/OfficeFillModels.cs:94` 写着：
> `⛔ 不接受 .doc / .xls —— 上传侧已拒绝（21 号 Q-3）`

**事实是反的**：
- `StandardDirectoryService.ValidateUploadFileType` 的 `allowedExts` **明确包含** `"doc"` 与 `"xls"`；
- `GetContentType` 为 `.doc`/`.xls` **都配了 MIME**；
- 库里**就有 570 份 `.doc` + 44 份 `.xls`**。

⇒ **上传侧从来没有拒绝过 `.doc`/`.xls`。** 该注释是**唯一**让人误以为「归一问题不存在」的地方。
（本次不改代码 —— 属 `38` 号批准后的 S0 动作；见 Q-14。）

### 13.2 ★ NPOI 2.7.2 能力全面排查（逐条实测，用户第 7 条要求）

> 排查方法：`~/.nuget/packages/npoi/2.7.2/lib/net6.0/` 下 `NPOI.Core.xml`（3131 处 `NPOI.HSSF`）、
> `NPOI.OOXML.xml`（723 处 `NPOI.XWPF`）逐符号检索 + `strings` 验 DLL。

| # | 能力 | NPOI 2.7.2 | 证据 / 说明 |
|---|---|---|---|
| 1 | `.docx` 读 / 写 | ✅ | `NPOI.XWPF.UserModel`（在产） |
| 2 | **`.doc` 读 / 写** | ❌ **完全不支持** | **`NPOI.HWPF` 命名空间在 2.7.2 里不存在**（XML 文档 0 处、DLL 字符串 0 处、全包 0 文件引用）⇒ `.doc` **连读都读不了** |
| 3 | `.xlsx` 读 / 写 | ✅ | `NPOI.XSSF.UserModel`（在产） |
| 4 | `.xls` 读 / 写 | ✅ 有（HSSF，3131 处） | **当前项目完全没用** ⇒ 需新写一层 |
| 5 | Word 段落 replace | ⚠️ **有，但跨 run 不支持** | `XWPFParagraph.ReplaceText(old,new)` 官方注释逐字：**`Replace text inside each run (cross run is not supported yet)`** |
| 6 | **Excel replace** | ❌ **没有** | 全包仅 `HSSF.Model.InternalSheet.ReplaceValueRecord`（HSSF 内部，非 XSSF 公开 API） |
| 7 | Word 页眉 / 页脚 | ✅ | `XWPFDocument.HeaderList` / `FooterList` / `CreateHeader` / `CreateFooter`（**页眉已用；页脚按规格不做**） |
| 8 | **Excel 打印页眉 / 页脚** | ✅ **可写，但是纯字符串** | `ISheet.Header` / `ISheet.Footer`（`IHeaderFooter.Left/Center/Right`）。**无段落/run/富文本**，只有 `&` 格式码（`&L`/`&C`/`&R`/`&"字体"`/`&12`/`&P`）⇒ **是「打印设置」不是「文档内容」**（**用户第 8 条的猜测正确**） |
| 9 | Word 域 | ✅ | `CreateField` / `CT_SimpleField`（`WordFieldWriter` 在用） |
| 10 | Word 表格行克隆 | ✅ | 深拷贝 `CT_Row`（`WordTableRowInserter` 在用） |
| 11 | Excel 行插入 | ⚠️ `ShiftRows` **会把区外内容一起下移** | 模板数据区下方**不能有别的内容** |
| 12 | Excel 数字格式 | ✅ | `IDataFormat` / `CloneStyleFrom`（`ExcelCellWriter` 在用）；⚠️ 单簿样式上限 64,000 ⇒ 必须缓存 |
| 13 | Excel 合并单元格 | ✅ `MergedRegions`，但**只有左上角可写** | 锚点落在非左上角 ⇒ **写入被静默忽略** |
| 14 | Word 文本框 `w:txbxContent` | ⚠️ **需自行遍历，当前未覆盖** | `WordDocumentScanner` 类注释已登记为已知边界 |
| 15 | Word 批注 / 脚注 / 尾注 / 艺术字 | ⚠️ 同上，**未覆盖** | 同上 |
| 16 | Word 编号 / 项目符号 | ✅ 读得到，但**写入不重排** | 编号由模板控制 |

**★ 排查得出的三条硬结论**：
1. **`.doc` 是死路** —— 不是「NPOI 支持得不好」，是**根本没有这个命名空间**。⇒ 归一必须走 LibreOffice。
2. **「替换」在 NPOI 里没有可用实现** —— Word 的 `ReplaceText` 恰好不支持我们最需要的场景（跨 run）；Excel 干脆没有。
   ⇒ **现有 `Office/` 层自己实现的「拼接整段 → 正则定位 → 反查 run 区间 → 从后往前改」是唯一可行路线**，不是重复造轮子。
3. **Excel 页眉页脚能做，但性质与 Word 不同** —— 它是**打印设置字符串**，不是文档流内容。

### 13.3 ★ 三处必须纠正的认知（照错的写会把设计写歪）

| # | 用户原话 | 事实 | 后果 |
|---|---|---|---|
| **C-1** | 「哪一行（**尽量用 a b c d，因为 excel 的行是字母**），哪一列(**0 1 2** 和 excel 的标识一致)」 | **说反了**：Excel 的 **列是字母**（A/B/C…），**行是数字**（1/2/3…）。`A1` = 第 A 列第 1 行。NPOI 内部**行列都是 0-based 整数**（`StartRow=0, StartCol=0` ⇒ `A1`） | 若按原话实现，配置界面**行列表头互换** ⇒ 值填到**错误的格子**，且**不报错** |
| **C-2** | 「我不了解是否能操作 word 和 excel 的 replace 功能」 | **Word 有但残缺**（跨 run 不支持，而跨 run 是常态）；**Excel 完全没有** | 若依赖 NPOI 内置 replace ⇒ **替换静默失效**（模板里 token 被 Word 拆成 3 个 run 时） |
| **C-3** | 「下载的时候，自动将 doc 转 docx」 | **NPOI 做不到**（无 HWPF）⇒ 只能走 **LibreOffice 容器**（`IFileConvertCore.ConvertToFormatAsync` **已实现**） | 若按「用 NPOI 转」设计 ⇒ 方案从第一步就不成立 |

### 13.4 ★ 用户 11 条逐条比对

| 条 | 主题 | 结论 | 说明 |
|---|---|---|---|
| 1 | 核心 = 替换/填写；类型 = 字段/表格；文档 = word/excel | ✅ **完全吻合** | 正好是 **2×2 矩阵**，`Office/` 层已覆盖 4 格 |
| 2 | 下载时 doc→docx / xls→xlsx | ⚠️ **方向对，但必须换实现 + 改时机** | 见 13.1（92% 阻塞）+ 13.3 C-3（不能用 NPOI）⇒ **裁决 Q-9 / Q-10** |
| 3 | 表格数据 = **json 结构** | ⚠️ **真实差距** | 现有 `OfficeFillRegion.Rows` 是 `List<List<FillValue?>>`（**位置型二维数组**），**不是键值 json** ⇒ 缺**列映射层**（= §3.2 的 `{{col:FieldCode}}`） |
| 4 | 替换：先得整段文字 → 更新 → 针对 run/cell 替换 | ✅ **完全正确，且已实现** | 正是 `WordParagraphFiller` 的**字符索引映射法**。**且我们多做了跨 run**（token 被拆到多个 run 是常态，不是例外） |
| 5a | Word：通过标签找格子，读所有格子内容，发现 `{{}}` 赋值后替换 | ✅ **完全正确，且已实现** | `WordTableRegionFiller.FindTag` + `WordCellWriter` |
| 5b | Excel：能否得到带 `{{}}` 格子的坐标 | ✅ **能，已实现** | `ExcelFillWriter.FillSheet` 全簿遍历 + `CellRef` 转 A1 ⇒ **不需要人工在属性里填 row/col**（仅**区域填充的起始坐标**需要） |
| 5c | 「保留替换功能即可，替换可以代替填写」 | ✅ **同意** | 现有实现里「单元格填写」与「替换」是**同一条路径**（`WordCellWriter` 内部就是替换首个 run 的文本） |
| 6 | Word 用标签；Excel 是范围 + sheet | ✅ **逐字吻合已实现** | `OfficeRegionKind.WordTable`（`{{table:Tag}}`）/ `ExcelRange`（`SheetName`+`StartRow`+`StartCol`） |
| 7 | 必须考虑所有可能性 + 全面排查 NPOI + 全部测试 | ✅ **本轮已排查**（见 13.2） | ⚠️ 但**测试尚未覆盖**：Excel 页眉、`.doc/.xls` 拒绝路径、合并单元格、文本框、跨 run 的 Excel 场景 |
| 8 | 页眉：Word 只替换；Excel 是打印设置，能否 NPOI 替换需研究 | ✅ **判断正确 + 已研究出结论** | Excel 页眉 = `ISheet.Header`，**纯字符串可替换**（`&` 格式码需保护），**每 sheet 各一份** ⇒ **裁决 Q-11** |
| 9 | 先选**数据来源 skill**，再选**操作方法**（属性按文档类型/信息类型自动生成） | ✅ **与本文两级 Skill 架构逐字吻合** | `src_*`（数据来源）+ `fill_*`（操作方法）；`fill_*` 按 `FileKind` + `value_kind` 分派属性 |
| 10 | 全文 AI：**合并成 1 个请求**，不是 10 个填写项发 10 次 | ⚠️ **新要求，当前设计是逐个调用** | 需新增「全文 AI 生成」编排步骤 ⇒ **裁决 Q-12** |
| 11 | Skill 分类：内容类（全局/字典/语义分析/ai字段/ai表格）+ 操作类（单元格填写/表格填写） | ✅ **与 `src_*` / `fill_*` 吻合** | 映射见下表；⚠️ 但「ai 字段 / ai 表格」在本文原属**二期** ⇒ **裁决 Q-13** |

**第 11 条的分类映射**：

| 用户分类 | 对应本文 Skill | 本文原定阶段 |
|---|---|---|
| 全局参数 | `src_global_param` | **一期** |
| 字典 | `src_dict_pick` | 二期 |
| 语义分析（改写） | `src_ai_generate` | 二期 |
| **ai 字段**（跨文档取数，只能写规则） | `src_related_doc` | **二期** |
| **ai 表格**（跨文档成表） | `src_related_doc`（table 形态） | **二期** |
| 单元格填写 | `fill_scalar` | **一期** |
| 表格填写 | `fill_table` | **一期** |

### 13.5 新增裁决 Q-9 ~ Q-14（全部有建议值）

| ID | 决策点 | 选项 | **建议** |
|---|---|---|---|
| **Q-9** | `.doc`/`.xls` 归一**何时做** | A **入库/上传时归一一次**（产物落库，可复现）<br>B 下载时实时转（每次）<br>C 填充前按需转 | **A** —— ① LibreOffice 输出**非确定性**，下载时转会导致「用户看到的」与「系统填的」是两份文件，出问题无法复现；② 570 份可批量归一一次（已有 `backfill-conversions` 端点范式）；③ **下载时拿到的就是归一后的文件**，用户的原始诉求自动满足 |
| **Q-10** | 归一产物**存哪 / 是否回写为「该槽位的可编辑版本」** | A 新增独立列 `EditableStoragePath`（与 `PreviewPdfPath`/`MarkdownPath` 并列）<br>B 复用已停用的 `ConvertedStoragePath` | **A** —— `ConvertedStoragePath` 是**遗留字段**（`StandardDirectoryFile.cs:134` 已标注「停止写入新值」），复用它会让「遗留排空」与「新链」混在一起；新列语义清晰且可独立回填 |
| **Q-11** | Excel 打印页眉 / 页脚**做不做** | A **做**（字符串替换 + `&` 格式码保护，每 sheet 独立）<br>B 不做（与 Word 页脚同口径） | **A** —— 成本极低（就是字符串替换），且 Excel 模板的打印页眉里**常带企业名/标准号**，是真实填写需求；⚠️ 但**必须先实现 `&` 格式码转义保护**，否则 `&"宋体"` 会被当普通文本 |
| **Q-12** | 全文 AI 的**合并粒度** | A **一份文档一次请求**（所有 AI 项合并）<br>B 一份文档 + 一张表一次<br>C 保持逐个调用 | **A + 结构化输出 + 缺项回退** —— 用户的诉求（省 token、上下文互参）成立；⚠️ 但必须配套「**逐项校验 + 缺项单独回退**」，否则一次请求里漏掉一个字段 ⇒ **无法定位、静默留空** |
| **Q-13** | `src_related_doc`（ai 字段/ai 表格）**是否进一期** | A 进一期<br>B 保持二期 | ~~**B（暂定）**~~ ⇒ **⛔ 本建议已被用户推翻，作废** —— 3 个 AI Skill **必须进一期**（见 **§十五**，替代裁决为 **Q-18**） |
| **Q-14** | 是否**同时修** `OfficeFillModels.cs:94` 的错误注释 | A **随 S0 一起修**（1 行注释）<br>B 不动 | **A** —— 该注释**与事实相反**（上传侧从未拒绝 `.doc/.xls`，库里 570 份），是**唯一**会让人误判「归一问题不存在」的地方，留着必然误导下一个人 |

### 13.6 对 §九 实施顺序的影响

**原 `S0~S7` 不变**，但**前面插一个 `S-1`**（因为 13.1 是**前置阻塞**）：

| 阶段 | 内容 | 可独立验证 |
|---|---|---|
| **S-1（新增，★ 前置）** | **`.doc/.xls` 归一链**：加 `EditableStoragePath` 列 + 复用 `IFileConvertCore.ConvertToFormatAsync` + 批量回填端点 | ✅ **有源文件的旧格式文件全部产出 `.docx`**（**169 份** = 模板 143 + 企业 26；⚠️ 原写「570 份」口径错误，见 §13.1a） |
| S0 | 修 `Skill清单-V1.md` + 修 `OfficeFillModels.cs:94` 注释（Q-14） | ✅ 纯文档 |
| S1 | `cert_doc_fill_log` 建表 + `ParentAnchorCode` 加列 | ✅ 建表 |
| S2 | 取值 Skill 骨架（`src_global_param` + `src_manual`） | ✅ 单元测试 |
| S3 | `fill_scalar` + `fill_block` | ✅ 拿真模板写值 |
| **S4** | ★ 表格列锚点扫描（`{{col:X}}` → `ParentAnchorCode`） | ✅ 扫出列清单 |
| **S5** | ★ `fill_table`（Word 表格 + Excel 区域，含行数对齐） | ✅ 8 行填进 3 行模板 |
| S6 | `src_from_profile` + `src_compute` | ✅ 端到端 |
| **S6.5（新增）** | Excel 打印页眉页脚（若 Q-11 选 A） | ✅ 页眉里 `{{}}` 被替换且 `&` 码完好 |
| S7 | 试跑端点 | ✅ 界面上能点 |

**★ 建议的 spike 文件（已找到，无需自造）**：
`docs/90-归档/案例资料/CS河北雄安尚龙医疗科技有限公司13485体系材料/` 下有**真实**的表格型文件：
- `4记录文件/生产类/XASL-PR-027 生产过程自检记录.xlsx`（**Excel 表格**）
- `4记录文件/其它/XASL-OR-014 产品销售台账.docx`（**Word 表格**）
- `4记录文件/其它/XASL-OR-010 合格供方评定记录.docx`
- `1质量手册/目标分解考核统计报表.docx`

---

## 十四、★★★ 两类 Skill 的权威分类 + 填充 Skill 的建立方式（2026-10-03 追加）

> **本节回应**：「没有回答 skill 2 类分类问题，没有明确进行数据填充的 skill 如何建立，实施起来有问题」。
> **结论**：§六 的分类**确实不够用**，且填充 Skill 的建立方式**原稿没写** ⇒ 本节补齐，并**收敛 §6.2**。

### 14.1 先承认：§六 的分类不够用

| 问题 | 说明 |
|---|---|
| **数量对不上** | §六 给了 `9 + 3 = 12` 个；你的分类是 **`5 + 2 = 7`** 个 |
| **`fill_scalar`/`fill_block` 是人为拆分** | 层 1 的两个写入器**不区分值的长短**（都收同一个 `FillValue`、走同一条路径）⇒ 拆分**无技术依据** ⇒ **已合并为 `fill_cell`**（§6.2 已改） |
| **没写「怎么建」** | §六 只列了 SkillCode 与端口名，**没有签名、没有注册方式、没有调用链** ⇒ 无法照做 |

### 14.2 ★ 权威分类表（7 个 Skill = 5 内容类 + 2 操作类）

> **⚠️⚠️ 本节表已过期，以 `39` 号 §二 为准**（`39` 号 是本文的**详细设计**）。三处修正：
> ① **数量**：`39` 号 修订为 **8 个 = 6 内容类 + 2 操作类**（**补 `src_manual`**：不产值、只声明待办，故按「产值来源」分是 5 个，按「参与来源链」分是 6 个）；
> ② **一期列**：下表 `src_semantic`/`src_ai_field`/`src_ai_table` 标「二期」**已被 §十五 推翻** ⇒ 实为 **一期必做**；
> ③ **参数列**：下表 `upstream_doc_code`/`field_code`/`context` **已被 §15.10 两级过滤管线取代** ⇒ 实际参数见 `39` 号 §六~§八。

**内容类（产出「值」）**

| 你的命名 | SkillCode | 产出 | 值形态 | 主要参数 | 一期 |
|---|---|---|---|---|---|
| 全局参数 | `src_global_param` | 标量值 | `scalar` | `param_code`, `standard_code`, `stage_code` | ✅ |
| 字典 | `src_dict` | 标量值 | `scalar` | `dict_no`, `cascade_path` | 二期 |
| 语义分析 | `src_semantic` | 改写后的文本 | `scalar` | `prompt_code`, `source_text`, `context` | 二期 |
| ai 字段 | `src_ai_field` | 跨文档抽出的值 | `scalar` | `upstream_doc_code`, `field_code` | 二期（**Q-13**） |
| ai 表格 | `src_ai_table` | **表格数据** | **`table`** | `upstream_doc_code`, `table_spec` | 二期（**Q-13**） |

**操作类（产出「写入指令」）**

| 你的命名 | SkillCode | 输入 | 产出 | 内部走 |
|---|---|---|---|---|
| 单元格填写 | `fill_cell` | 值字典 | `OfficeFillRequest.Values` | `WordParagraphFiller`/`WordCellWriter`/`ExcelCellWriter`/`WordFieldWriter` |
| 表格填写 | `fill_table` | 表格数据 | `OfficeFillRequest.Regions` | `WordTableRegionFiller`/`ExcelRegionFiller` |

> **★ 与 §6.1 的关系**：§6.1 的 9 个是**实现清单**（把「值从哪来」拆细），本节 5 个是**你的业务分类**。
> 映射：`src_from_profile`/`src_related_doc`/`src_compute`/`src_manual`/`src_self`/`src_sibling` 都是 **`src_ai_field` 或 `src_dict` 的实现变体**，
> 一期保留 §6.1 的命名（更精确），**对用户呈现的仍是 5 类**。

### 14.3 ★★ 分类的落点：`wf_skill.SideEffect` + `CategoryCode`（★ 现成列，不用新造）

**实测 `wf_skill` 表结构**，发现**已经有 `SideEffect tinyint(1) NOT NULL DEFAULT 0`（注释「是否有副作用」）**。

**而现有 11 个 Skill 全部是 `SideEffect = 0`** ⇒ **这一列至今没被用过** —— 它恰好就是为「操作类」准备的。

现有 11 个 Skill 的分类分布（实测）：

| `CategoryCode` | Skill | 个数 |
|---|---|---|
| `ai_judge` | `build_nc_prompt` / `compare` / `is_field_empty` / `is_std_file_missing` / `is_table_empty` / `nc_conclusion_calc` / `nc_validate` | 7 |
| `ai_generate` | `llm_extract` | 1 |
| `data_access` | `get_field` / `get_table` | 2 |
| `data_process` | `assemble` | 1 |

⇒ **两类分类的落点**：

| 维度 | 内容类 `src_*` | 操作类 `fill_*` |
|---|---|---|
| **`SideEffect`** | **`0`** | **`1`** ← ★ 语义契约：它导致文件变化 |
| **`CategoryCode`** | `doc_src_scalar`（标量）/ `doc_src_table`（表格） | `doc_fill` |
| **产出** | 值 | 写入指令 |
| **是否碰文件** | ⛔ 不碰 | ⛔ 不落盘（装配） |
| **调用时机** | 配置期（被来源链引用） | **填充期**（被编排器调用） |
| **参数来源** | **UI 绑定**（反射驱动表单） | **编排器注入**（见 14.9） |

### 14.4 ★★★ 两类如何接线（回答你第 9 条「操作方法自动生成」）

**核心规则**：**内容类的「值形态」决定它能接哪个操作类**。

```
  内容类 src_*                        操作类 fill_*
┌──────────────────┐
│ src_global_param │─┐
│ src_dict         │─┼──→ 值字典 {AnchorCode: FillValue} ──→ ┌───────────┐
│ src_semantic     │─┤                                        │ fill_cell │─┐
│ src_ai_field     │─┘                                        └───────────┘ │
└──────────────────┘                                                         │
                                                                             ├─→ OfficeFillRequest
┌──────────────────┐                                                         │        ↓
│ src_ai_table     │────→ 表格数据 {columns, rows, totals} ─→ ┌────────────┐ │   层 1（一次落盘）
└──────────────────┘                                          │ fill_table │─┘        ↓
                                                              └────────────┘        文件
```

⇒ **UI 上的落地**（这正是你说的「操作方式自动生成」）：
1. 用户先选**数据来源 Skill**（内容类）；
2. 系统读该 Skill 的 `CategoryCode`：
   - `doc_src_scalar` ⇒ **操作方法只能选 `fill_cell`**
   - `doc_src_table` ⇒ **操作方法只能选 `fill_table`**
3. 选完操作方法后，**操作属性按「文档类型 + 值类型」自动生成**：
   - Excel ⇒ 追加 `start_row`/`start_col`/`sheet_name`（**区域填充才需要**）
   - Word ⇒ 追加 `table_tag`（表格）/ 无（单元格）
   - `value_kind` ⇒ 追加 `number_format`

⇒ **⛔ 不需要用户手工判断「该用哪个操作方法」**，也**不需要用户填 row/col 来定位单元格**（单元格的坐标是扫描出来的，见 §13.4 第 5b 条）。

### 14.5 ★★★ 填充 Skill 如何建立（本节核心）

#### （1）⛔ 先排除一个错误做法：**每个填充 Skill 各写一次文件**

看起来最直观的实现是：`fill_cell` 打开模板→写值→保存；`fill_table` 打开模板→写表→保存。**这是错的**，三个理由：

| # | 后果 | 原因 |
|---|---|---|
| **①** | **数据互相抹掉** | `WordFillWriter.Fill` 内部顺序是「**区域 → 正文 → 表格 → 页眉 → 自验收 → 落盘**」。若 `fill_table` 先跑，它那次 `Fill` 的 `Values` 为空 ⇒ **所有 `{{company_name}}` 被置空**（`KeepUnresolvedAsIs` 默认 `false`）；随后 `fill_cell` 再跑，`{{table:items}}` 标签**已经不在文档里**了 ⇒ 表格数据**丢失** |
| **②** | **N 次 IO** | 一份文档被打开/保存 N 次（大模板单次 3~5 秒） |
| **③** | **顺序不变量被破坏** | `WordFillWriter` 类注释逐字：「⚠️ 区域填充必须**先于**锚点替换 —— 否则 `{{table:xxx}}` 会被当成普通锚点处理掉」 |

#### （2）★ 正确做法：**填充 Skill = 声明式装配器（不落盘）**

```
OfficeFillRequest(空)
   → fill_table(请求, 表格数据)  ⇒  请求 + Regions
   → fill_cell (请求, 值字典)    ⇒  请求 + Values
   → ★ 编排器：按扩展名分派 → WordFillWriter.Fill(请求) / ExcelFillWriter.Fill(请求)  ← 只落盘一次
```

**职责切分（写死）**：

| 角色 | 职责 | 是否碰文件 |
|---|---|---|
| **内容类 Skill** | 产出值 | ⛔ |
| **操作类 Skill** | 把值**装配**成 `OfficeFillRequest` 的片段 | ⛔ |
| **编排器** `DocumentFillOrchestrator` | 取模板字节 → 依次调操作类 Skill → **合并成一个请求** → 调层 1 **一次** → 上传产物 → 写留痕 | ✅ **唯一碰文件的地方** |
| **层 1** `WordFillWriter`/`ExcelFillWriter` | 确定性写入 + 自验收 | ✅（由编排器调） |

**⇒ 为什么这样切是对的**：
- ✅ **顺序不变量由层 1 保证**（它本来就在一次 `Fill` 里按固定顺序处理）
- ✅ **一份文档只打开/保存一次**
- ✅ **操作类 Skill 变成纯函数** ⇒ 可**独立单测**（不用真文件，断言产出的请求即可）
- ✅ **⛔ 不用改造已完成的层 1**（15 源文件 / 148 测试全绿是**资产**，改造风险 > 收益）

**⚠️ 代价（照实说）**：`fill_*` 的「副作用」是**声明性的**（`SideEffect=1` 表达「它导致文件变化」这个**语义契约**），
而不是「它自己写文件」这个**实现事实**。⇒ 若你希望 `fill_*` **真正持有文件句柄**，就必须把层 1 改成
「打开 / 写 / 保存」三段式（**裁决 Q-15**）。

### 14.6 ★ 完整骨架（可直接照抄）

> **⚠️ 本节骨架已被 `39` 号 §四~§十一 取代（2 处实质修正）**，**以 `39` 号 为准**：
> - **M-1**：本节的 `OfficeFillRequest? request = null` 参数**走不通** —— 声明式来源链的参数是 **JSON 存库**的，
>   取回后是 `JsonElement`/`Dictionary<string,object>`，而 `SkillExecutor.ConvertValue` **只转基础类型** ⇒ **永远还原不成强类型**。
>   ⇒ `39` 号 改为 **`FillSession? session = null`**（编排器在 `context.Inputs` 里**直接放对象实例**，`ConvertValue` 原样透传）。
> - **M-2**：`IDictionary<string, FillValue>` 同理 ⇒ `39` 号 改为收 **`IDictionary<string, object>`**，内部 `FillValueFactory.Coerce` 归一。
> - **另**：`TableColumn` 缺 **`Title`（中文列名）** ⇒ AI 看不懂 `field_code="reg_capital"` 要填什么（`39` 号 `Q-25`）。
>
> **本节骨架保留作设计溯源**，实现请照 `39` 号 写。

**公共契约**（新增，放 `CertPlatform.Shared/Office/`）：

```csharp
/// <summary>表格填充的数据契约（★ 键值 json，不是位置型二维数组）</summary>
public sealed class TablePayload
{
    public string? TableTag { get; set; }                                   // Word：{{table:Tag}}
    public List<TableColumn> Columns { get; set; } = new();                 // ★ 列映射（键）
    public List<Dictionary<string, object?>> Rows { get; set; } = new();     // ★ 每行是键值字典
    public Dictionary<string, object?> Totals { get; set; } = new();         // 合计行
}

public sealed class TableColumn
{
    public string FieldCode { get; set; } = string.Empty;   // 对应模板里的 {{col:FieldCode}}
    public string Kind { get; set; } = "text";              // text / number / date / bool
    public string? Format { get; set; }                     // Excel 数字格式
}
```

**操作类 Skill 1 —— `fill_cell`**：

```csharp
namespace CertPlatform.Admin.Services.Workflow.Skills;

/// <summary>
/// 单元格填写（操作类）—— 把「值字典」装配进 <see cref="OfficeFillRequest"/> 的 Values 片段。
/// <para>⛔ <b>不落盘</b>：真正的写入由编排器调 <see cref="WordFillWriter"/>/<see cref="ExcelFillWriter"/>
/// <b>一次</b>完成（见 `38` 号 §14.5）。</para>
/// </summary>
[Skill(
    Code = "fill_cell",
    Name = "单元格填写",
    ReturnType = "json",
    Description = "把值字典装配成 Word/Excel 单元格填充指令（含页眉）。不落盘。")]
public static class FillCellSkill
{
    public static Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "值字典：{AnchorCode: FillValue}")]
        IDictionary<string, FillValue> values,
        [SkillParam(Description = "上游累积的填充请求（空=新建）", BindMode = SkillParamBindMode.LinkOrConstant)]
        OfficeFillRequest? request = null,
        [SkillParam(Description = "是否处理 Word 页眉")] bool fill_header = true,
        [SkillParam(Description = "未命中锚点是否保留原文（仅模板调试）")] bool keep_unresolved = false,
        CancellationToken ct = default)
    {
        if (values == null || values.Count == 0)
            return Task.FromResult(SkillResult.Fail("values 不能为空"));

        request ??= new OfficeFillRequest();

        // ★ 重复锚点必须报错，⛔ 不静默覆盖 —— 两个来源填同一个锚点 = 配置错误
        foreach (var kv in values)
        {
            if (request.Values.ContainsKey(kv.Key))
                return Task.FromResult(SkillResult.Fail(
                    $"锚点 {kv.Key} 被重复赋值（已有值来自 {request.Values[kv.Key].Source}）"));

            request.Values[kv.Key] = kv.Value;
        }

        request.FillHeader = fill_header;
        request.KeepUnresolvedAsIs = keep_unresolved;

        return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
        {
            ["request"] = request,
            ["anchor_count"] = request.Values.Count
        }));
    }
}
```

**操作类 Skill 2 —— `fill_table`**：

```csharp
/// <summary>
/// 表格填写（操作类）—— 把「表格数据」装配进 <see cref="OfficeFillRequest"/> 的 Regions 片段。
/// <para>★ 本 Skill 就是「<b>列映射层</b>」的落点：把键值 json（<see cref="TablePayload"/>）
/// 投影成层 1 要的位置型二维数组（<c>List&lt;List&lt;FillValue?&gt;&gt;</c>）。</para>
/// <para>⛔ <b>不落盘</b>（同 <see cref="FillCellSkill"/>）。</para>
/// </summary>
[Skill(
    Code = "fill_table",
    Name = "表格填写",
    ReturnType = "json",
    Description = "把表格数据装配成 Word 表格 / Excel 区域的填充指令。不落盘。")]
public static class FillTableSkill
{
    public static Task<SkillResult> ExecuteAsync(
        [SkillParam(Description = "表格数据：{table_tag, columns, rows, totals}")]
        TablePayload table,
        [SkillParam(Description = "上游累积的填充请求（空=新建）", BindMode = SkillParamBindMode.LinkOrConstant)]
        OfficeFillRequest? request = null,
        [SkillParam(Description = "Excel 起始行（0-based；<0=按模板样板行自动探测）")] int start_row = -1,
        [SkillParam(Description = "Excel 起始列（0-based；<0=自动探测）")] int start_col = -1,
        [SkillParam(Description = "Excel 工作表名（空=第 1 个工作表）")] string? sheet_name = null,
        CancellationToken ct = default)
    {
        if (table == null || table.Columns.Count == 0)
            return Task.FromResult(SkillResult.Fail("table.columns 不能为空"));

        request ??= new OfficeFillRequest();

        // ★ 列映射：Columns 是键，Rows 是键值字典 ⇒ 投影成位置型矩阵
        var matrix = new List<List<FillValue?>>();
        foreach (var row in table.Rows)
        {
            var line = new List<FillValue?>(table.Columns.Count);
            foreach (var col in table.Columns)
            {
                row.TryGetValue(col.FieldCode, out var raw);
                line.Add(TableValueMapper.ToFillValue(col, raw));   // ⛔ 缺列 ⇒ null ⇒ 写空
            }
            matrix.Add(line);
        }

        request.Regions.Add(new OfficeFillRegion
        {
            Kind      = sheet_name != null || start_row >= 0
                        ? OfficeRegionKind.ExcelRange      // Excel
                        : OfficeRegionKind.WordTable,      // Word
            SheetName = sheet_name,
            StartRow  = start_row < 0 ? 0 : start_row,
            StartCol  = start_col < 0 ? 0 : start_col,
            TableTag  = table.TableTag,
            Rows      = matrix
        });

        return Task.FromResult(SkillResult.Ok(new Dictionary<string, object>
        {
            ["request"] = request,
            ["region_count"] = request.Regions.Count,
            ["row_count"] = matrix.Count
        }));
    }
}
```

> ⚠️ **`Kind` 的判定不要靠猜** —— 上面用「有没有 `sheet_name`/`start_row`」判 Excel 是**示意**；
> 实现时应由编排器**显式传入 `FileKind`**（`.docx`→Word / `.xlsx`→Excel），否则 `sheet_name` 恰好为空时会误判。

### 14.7 ★ 注册（`wf_skill` + `wf_skill_reflection` 各 2 行）

```sql
-- 操作类（★ SideEffect = 1；ClassPath 前缀是 CertPlatform.Admin.Services.Workflow.Skills，⛔ 不是 YZH.Core.*）
INSERT INTO wf_skill (Code, SkillCode, Name, SkillType, CategoryCode, SideEffect, ReturnType, Description, IsActive, SortOrder)
VALUES
 ('SK_FILL_CELL',  'fill_cell',  '单元格填写', 'method', 'doc_fill', 1, 'json', '把值字典装配成 Word/Excel 单元格填充指令（含页眉）。不落盘。', 1, 100),
 ('SK_FILL_TABLE', 'fill_table', '表格填写',   'method', 'doc_fill', 1, 'json', '把表格数据装配成 Word 表格 / Excel 区域的填充指令。不落盘。', 1, 101);

INSERT INTO wf_skill_reflection (Code, SkillCode, ClassPath, MethodName)
VALUES
 ('SKR_FILL_CELL',  'fill_cell',
  'CertPlatform.Admin.Services.Workflow.Skills.FillCellSkill',  'ExecuteAsync'),
 ('SKR_FILL_TABLE', 'fill_table',
  'CertPlatform.Admin.Services.Workflow.Skills.FillTableSkill', 'ExecuteAsync');
```

⚠️ **`ClassPath` 必须与真实命名空间逐字一致** —— `SkillExecutor.ResolveType` 靠 `Type.GetType` 反射，
写错 ⇒ 运行时报「无法找到类型」，且**只在真正执行时才发现**。

> **⚠️ 上面的 `ClassPath` 已被 `39` 号 §2.3 取代**：`39` 号 加了 **`.Fill`** 子命名空间段
> （`...Workflow.Skills.Fill.FillCellSkill`），并把注册扩展到 **8 个 Skill 的完整 SQL**（含 `Icon`/`Color`/`OutputStrict`/`Version` 等实测列）。
> **以 `39` 号 §2.3 为准。**

⚠️ **`CategoryCode` 用的是新值 `doc_fill`**；`wf_skill_category_dict` 表**实测为空**（0 行）⇒
新增分类**不需要**先去字典表登记（但前端若要显示中文名，需在页面侧维护映射）。

### 14.8 ★ 调用链全景（编排器视角）

```csharp
public sealed class DocumentFillOrchestrator
{
    public async Task<OfficeFillResult> FillAsync(
        string templateFileCode, IReadOnlyList<FillStep> steps, CancellationToken ct)
    {
        // ① 取模板字节（MinIO）+ 判 FileKind（按扩展名，⛔ 不猜）
        // ② 内容类：对每个填写项解析 SourceSpec → 调 src_* Skill → 汇总成「值字典 / 表格数据」
        //    （★ 全文 AI 项在此合并成 1 次请求 —— 见 §13.5 Q-12）
        // ③ 操作类：依次调 fill_cell / fill_table，累积同一个 OfficeFillRequest
        // ④ ★ 层 1 落盘（一次）：按 FileKind 分派
        //      WordFillWriter.Fill(request)  /  ExcelFillWriter.Fill(request)
        // ⑤ 上传产物到 MinIO（新路径，⛔ 不覆盖模板）
        // ⑥ 写 cert_doc_fill_log（§7.2）+ 回写结果表
        // ⑦ 返回 OfficeFillResult（含 Report：Hits/Pendings/LeftoverTokens/Regions）
    }
}
```

**★ 编排器必须校验的三条不变量**（否则会静默出错）：
1. `request.Template` 非空且是 `.docx`/`.xlsx`（`.doc/.xls` ⇒ **先归一**，见 §13.6 的 `S-1`）
2. `report.Verified == true`（`LeftoverTokens` 与 `LeftoverMarkRuns` 都为空）⇒ 否则**产物不合格**，不能回写为「已生成」
3. `report.Pendings` 非空时**必须展示**（「一键看证据摘要」的核心体验），⛔ 不得静默留空

### 14.9 ★ 参数从哪来（内容类 vs 操作类的**关键差异**）

| | 内容类 `src_*` | 操作类 `fill_*` |
|---|---|---|
| **参数由谁给** | **用户在配置期绑定** | **编排器在填充期注入** |
| **绑定方式** | `[SkillParam(BindMode)]` ⇒ 前端**反射生成表单**（`SkillExecutor.Analyze` 产出 `InputPorts`） | `context.Inputs` **直接放强类型对象** |
| **UI 是否显示** | ✅ 显示 | ❌ **不显示**（用户配的是「填什么」，不是「怎么装配」） |

⚠️ **一个必须知道的框架细节**：`SkillExecutor.ConvertValue` **只转换基础类型**
（`string`/`bool`/`int`/`long`/`double`/`decimal`/`DateTime`），**其他类型原样返回**。
⇒ 所以 `IDictionary<string, FillValue>` / `OfficeFillRequest` / `TablePayload` 这类参数，
**`context.Inputs["参数名"]` 里要直接放对象实例**（⛔ 不是 JSON 字符串，否则会走 `ToString()`）。

### 14.10 ★ 测试怎么建（对齐你第 7 条「全部进行测试才能处理」）

| 层 | 测什么 | 怎么测 | 现有基础 |
|---|---|---|---|
| **操作类 Skill（单元）** | 装配正确性 | **不碰真文件**：给 `TablePayload` → 断言产出的 `OfficeFillRegion.Rows` 矩阵形状与值 | 新增 |
| **操作类 Skill（集成）** | 装配 + 层 1 落盘 | 用 §13.6 的**真实样例**（`XASL-PR-027….xlsx` / `XASL-OR-014….docx`）跑「8 行填进 3 行模板」 | `RealDocumentSmokeTests` 已有范式 |
| **内容类 Skill（单元）** | 取值正确性 | 造假数据（DB 用 `IDbOrm` mock 或测试库） | `GetFieldSkill` 已有范式 |
| **端到端** | 一份真模板从 `src_*` 到产物 | 断言 `Report.Verified == true` + 抽样比对单元格 | 新增 |

**⚠️ 现有测试覆盖缺口**（本轮实查）：`Office/` 层 4 个测试文件共 **42 个测试方法**（`Fact`/`Theory`），
但**未覆盖**：① Excel 打印页眉页脚 ② `.doc/.xls` 拒绝路径 ③ 合并单元格非左上角 ④ Word 文本框 ⑤ Excel 跨 run 场景。

### 14.11 新增裁决 Q-15 ~ Q-17

| ID | 决策点 | 选项 | **建议** |
|---|---|---|---|
| **Q-15** | `fill_*` 的形态 | A **声明式装配器**（不落盘；层 1 不动）<br>B **会话式写入器**（持文件句柄；需把层 1 改成「打开/写/保存」三段） | **A** —— 层 1 的 15 源文件 / **148 测试全绿是资产**；改成三段式的回归风险 > 收益。代价是 `fill_*` 的「副作用」是**语义声明**而非实现事实 |
| **Q-16** | 两类的落点 | A **`wf_skill.SideEffect`**（内容=0 / 操作=1）+ 新 `CategoryCode`：`doc_src_scalar`/`doc_src_table`/`doc_fill`<br>B 只用新 `CategoryCode`，不动 `SideEffect` | **A** —— `SideEffect` 列**已存在且 11 个 Skill 全为 0**（从未被用过），语义完全吻合「操作类」，**不用新造概念** |
| **Q-17** | 操作类 Skill 的参数是否走 UI 绑定 | A **不走**（编排器注入强类型对象），但**仍注册**进 `wf_skill`<br>B 走 UI 绑定（用户在页面上配 `start_row` 等） | **A** —— 用户配的是「**填什么**」（内容类），不是「**怎么装配**」（操作类）；但 `start_row`/`sheet_name` 这类**区域定位**参数**例外**，需在「表格填写规则」页暴露（属 `37` 号 Tab2） |

---

## 十五、★★★ 3 个 AI 内容类 Skill（**一期必做 = 核心功能**，2026-10-03 追加）

> **用户指令（逐字）**：
> 「我设计了 **3 个 ai 内容 skill 是必须要实现的，这是最核心的功能**。
>  第一个 **ai 语义生成**，针对一些标准的规则，我们可以按企业的实际情况进行改写；
>  另两类才是 **ai 自动填表的核心**，因为我们需要填写的内容，就是**不确定企业的信息是什么样的**，
>  但我们**知道需要填写的是单元格还是表格**，需要**通过提示词，让 ai 从企业文档中动态分析得到**，
>  这是最重要的功能，**不能砍掉**」

### 15.1 ⚠️ 先认错：Q-13 的「建议二期」是错的

我在 §13.5 的 **Q-13** 建议把 `src_related_doc`（ai 字段 / ai 表格）放**二期**，理由是「依赖企业资料入库 + 画像 + 跨文档召回」。
**这个判断错了** —— 它不是「锦上添花」，而是**产品定义本身**：

| 我在 Q-13 的假设 | 事实 |
|---|---|
| 「ai 字段/表格可以延后，先用全局参数 + 人工兜底」 | ❌ **不行** —— 企业信息**本来就是不确定的**，全局参数只能覆盖「企业名/地址」这类**已知固定项**；模板里**绝大多数格子**（培训记录、检验数据、人员资质…）**只能靠 AI 读企业文档动态得出** |
| 「依赖太重，一期做不完」 | ⚠️ 依赖是真的，但**不做就等于填充功能没有内容来源** |

⇒ **Q-13 建议作废**。3 个 AI Skill **进一期**，且**排在关键路径上**。

### 15.2 ★★ 3 个 Skill 的职责边界（★ 关键区分）

| Skill | 你的描述 | 输入 | **是否需企业文档** | 输出 |
|---|---|---|---|---|
| **`src_semantic`** | ai 语义生成：按企业实际情况**改写标准规则** | 模板里的**标准原文** + 企业画像/参数 | **❌ 不需要** | 改写后的文本 |
| **`src_ai_field`** | ai 字段：从企业文档**动态分析**出**单元格**的值 | 提示词 + **企业文档** + 锚点说明 | **✅ 需要** | 单元格值（`FillValue`） |
| **`src_ai_table`** | ai 表格：从企业文档**动态分析**出**表格** | 提示词 + **企业文档** + 表格规格 | **✅ 需要** | 表格数据（`TablePayload`） |

**★ 这个区分是本节的要害**：

- **`src_semantic` 是「改写已知文本」** —— 输入输出都是**文档内的文本**，企业画像只做**风格/事实校正**；
- **`src_ai_field` / `src_ai_table` 是「从企业文档里找/推断未知信息」** —— 这才是**自动填表的核心**：
  **我们知道「要填的是单元格还是表格」（形态确定），但不知道「企业到底有什么信息」（内容不确定）**
  ⇒ **只能靠提示词 + 企业文档，让 LLM 动态判断**。

⇒ 这也解释了**为什么不能用固定规则提取**：`get_field` 那类「按 `field_code` 查已提取结果表」的前提是
**提取期已经知道要提哪些字段**；而填充期面对的锚点是**模板决定的**，两者不可能预先对齐。

### 15.3 ★★★ 与「全文填写规则」的关系：**一份文档 = 一次请求**（= 你第 10 条）

**好消息：`37` 号 已经把载体设计好了** —— `cert_doc_fill_prompt`（表 3，**未建**）：

| 列 | 作用 |
|---|---|
| `SystemPrompt` | 角色设定 |
| **`UserTemplate` longtext** | ★ 用户提示词模板，**含 `{{__FILL__.xxx}}` 占位符** |
| **`OutputSchema` json** | ★ **期望输出结构（字段清单 + 表格清单），用于校验 AI 返回** |
| `Temperature decimal(3,2) DEFAULT 0.00` | ★ **低温保证可复现** |
| `Model` / `MaxTokens` / `Version` / `IsDefault` | 模型与版本管理 |

**`cert_doc_template.FillPromptCode`** ⇒ 指向该 Prompt（**一份文档一个 Prompt**，空 = 不走全文规则）。

⇒ **这正好实现你第 10 条「合并成 1 个完整的 ai 请求」** —— 不是 10 个填写项发 10 次，而是**一份文档一次**。

**★ 一次请求的输出契约**（`OutputSchema` 校验的对象）：

```json
{
  "semantic": { "ANCHOR_CODE": "按企业实际改写后的文本" },
  "fields":   { "ANCHOR_CODE": "从企业文档分析出的单元格值" },
  "tables":   { "TABLE_TAG": { "columns": ["A","B"], "rows": [{ "A": 1, "B": "x" }] } }
}
```

⇒ **3 个 Skill 在批量路径下的职责 = 按段切片**（`semantic` / `fields` / `tables`）。

**`UserTemplate` 需要的 3 类占位符**（建议命名）：

| 占位符 | 内容 | 谁需要 |
|---|---|---|
| `{{__FILL__.anchors}}` | 待填锚点清单（`AnchorRef` + 每项的**填写说明/提示词** + 形态 scalar/table） | 三者 |
| `{{__FILL__.enterprise_docs}}` | **企业文档（markdown）** | `src_ai_field` / `src_ai_table` |
| `{{__FILL__.template_text}}` | 模板里的**标准原文** | `src_semantic` |

⚠️ **一个必须正视的前置依赖（token 体积）**：企业文档可能有**几十份**，
全量塞进一次请求**必然超限** ⇒ **必须先做「召回 + 裁剪」**。
⇒ 这正是 `08` 号 的「召回/精排管线」（记忆：**它是管线，⛔ 不注册 `wf_skill`**）。
⇒ **一期必须有一个最小可用版本**（哪怕先按「标准/阶段 + 标签」粗筛，不做向量检索）。

### 15.4 ★★ 批量 vs 单项：两条路径**必须共用同一套装配器**

| 路径 | 谁发起 | 调用次数 | 用途 |
|---|---|---|---|
| **批量**（生产） | `AiFillBatchService`（编排器的一部分，⛔ **不是 Skill**） | **1 次**（缺项回退时才补调） | 正式填充 |
| **单项**（调试） | `37` 号 Tab1「试跑」按钮 → `SkillExecutor` | 1 项 1 次 | 配规则时验证提示词 |

**⚠️ 最大的风险：两套 prompt 组装逻辑 ⇒ 必然漂移**（「页面上试跑是对的，正式跑却不对」）。
⇒ **必须抽公共件**：

```
AiFillPromptBuilder   ← 组装 UserTemplate（填 3 类占位符）+ OutputSchema 校验
IAiFillInvoker        ← 包装 LlmInvokeService（已有）+ ForceJson + 重试 + 留痕
   ↑                                  ↑
AiFillBatchService              src_semantic / src_ai_field / src_ai_table
（批量：1 次请求 → 切片）         （单项：1 项 1 次请求）
```

**⇒ 3 个 Skill 的双重身份**：
1. **批量路径下**：`AiFillBatchService` 调 1 次 LLM 后，把结果**按段分派**给 3 个 Skill 做**校验 + 转 `FillValue`/`TablePayload`**；
2. **单项路径下**：每个 Skill **独立组装 + 独立调用**（调试用）。

⚠️ **缺项回退策略（必做）**：批量返回后**逐项校验** `OutputSchema`；
**缺的项单独再调一次**（单项模式）。⛔ **不得静默留空** —— 缺项必须进 `OfficeFillReport.Pendings`。

### 15.5 ★ 可复用的现成组件（⛔ 不重造）

| 需要的能力 | 现成的 | 位置 |
|---|---|---|
| LLM 调用（OpenAI 兼容 / 强制 JSON / token 统计） | **`LlmInvokeService.CompleteAsync`** | `CertPlatform.Shared/DocExtraction/LlmInvokeService.cs` |
| 配置六键（`ai_base_url`/`ai_api_key`/`ai_model_name`/…） | `cert_sys_config` | 已有 |
| 文档字段/表格定义 | `cert_doc_field_def`（1 行）/ `cert_doc_table_def`（6 行）/ `cert_doc_table_field_def`（23 行） | ✅ **已建**（骨架在，数据少） |
| 提取规则 + 提示词 | `cert_doc_extraction_rule`（**1 行，含 `Prompt` text 列 + `Skill` 列**） | ✅ 已建 |

⚠️ **`LlmInvokeService` 的一个已知约束**：它把消息体拼成**纯字符串**（`LlmInvokeService.cs:102-105`）
⇒ **不支持视觉模型**（图片/扫描件）。
⇒ 本节的 3 个 Skill **只用纯文本**，**不受影响**；但若企业文档里有扫描件，**必须先在提取期转成 markdown**（走 OCR 或人工）。

### 15.6 ⚠️ 数据落点：**3 张表全部未建**（实测）

| 表 | 实测 | 用途 | 处置 |
|---|---|---|---|
| `cert_doc_template` | ❌ **未建** | 模板主表（`FillPromptCode` 挂这里） | **S-1 建** |
| `cert_doc_template_anchor` | ❌ **未建** | 锚点清单（`SourceSpec` 挂这里） | **S-1 建** |
| **`cert_doc_fill_prompt`** | ❌ **未建** | **★ 3 个 AI Skill 的提示词载体** | **S-1 建** |
| `cert_doc_field_def` / `_table_def` / `_table_field_def` | ✅ 已建（1 / 6 / 23 行） | 字段与表格定义 | 复用 |
| `cert_doc_extraction_rule` | ✅ 已建（1 行，有 `Prompt` + `Skill` 列） | 提取规则 | 复用 |

### 15.7 ⚠️ 连锁影响：`37` 号 必须**部分解冻**

3 个 AI Skill 的提示词**必须能录入**（否则「通过提示词让 AI 分析」无处配置）。
⇒ **`37` 号 的 Tab2「全文填写规则」不能暂缓**，至少要：
- ✅ **提示词编辑器**（`SystemPrompt` + `UserTemplate` + `OutputSchema`）—— **一期必做**
- ✅ **锚点清单 + 每项填写说明** —— **一期必做**（`src_ai_field` 需要「这个格子要填什么」的描述）
- ⏸ 可延后：左树徽标 / 发布流程 / 指纹规则编辑器（组 F）

⇒ **`37` 号 从「⏸ 全部暂缓」改为「🟡 部分解冻」**：恢复 Tab1（锚点）+ Tab2（提示词），其余暂缓。

### 15.8 ★ 实施顺序（V1.3 重排：AI 三件套进关键路径；**⚠️ V1.4 增补 `S2.5`，见 §15.10(6)**）

| 阶段 | 内容 | 可独立验证 |
|---|---|---|
| **S-1** | ★ `.doc/.xls` 归一链 + **建 5 张表**（`cert_doc_template` / `_anchor`（含 `ParentAnchorCode`+`NumberFormat`）/ `_fill_prompt` / **`_fill_log`** / **`_ai_suggestion`**）+ `cert_standard_directory_file` 加 4 列 | ✅ **169 份**（见 §13.1a）产出 `.docx`；**5 表已建**（✅ 2026-10-03 完成） |
| **S0** | 修 `Skill清单-V1.md` + 修 `OfficeFillModels.cs:94` 注释 | ✅ 纯文档 |
| **S1** | `cert_doc_fill_log` 建表 + `wf_skill`/`wf_skill_reflection` 注册骨架 | ✅ 建表 |
| **S2** | 取值 Skill 骨架：`src_global_param` + `src_manual`（最简两个，先跑通链路） | ✅ 单元测试 |
| **S3** | ★ **AI 三件套**：`AiFillPromptBuilder` + `IAiFillInvoker` + `src_semantic` / `src_ai_field` / `src_ai_table` | ✅ **拿 1 份真企业文档 + 1 段提示词，产出 1 个值** |
| **S3.5** | `AiFillBatchService`（**一份文档一次请求** + 切片 + 缺项回退） | ✅ **10 个 AI 项只发 1 次请求** |
| **S4** | `fill_cell` + `fill_table`（装配器，见 §十四） | ✅ 拿真模板写值 |
| **S5** | 表格列锚点扫描（`{{col:X}}` → `ParentAnchorCode`） | ✅ 扫出列清单 |
| **S6** | 端到端：`src_*` → `fill_*` → 层 1 → 产物 | ✅ 8 行填进 3 行模板 |
| **S7** | 试跑端点 + `37` 号 Tab1/Tab2 部分解冻 | ✅ 界面上能点 |

**⚠️ 建议 S3 之前先做 spike**（同 §九）：拿**真实样例**验证「表格列锚点 + 8 行数据填进 3 行模板」。
**★ S3 本身也应先 spike**：用 `LlmInvokeService` + 1 份真企业 markdown，验证「提示词 → 结构化 JSON → 值」这条链。

### 15.9 新增裁决 Q-18 ~ Q-20（⚠️ `Q-19` 已由 **§15.10(7)** 细化，并新增 **`Q-21`**）

| ID | 决策点 | 选项 | **建议** |
|---|---|---|---|
| **Q-18** | 3 个 AI Skill 的**一期范围** | A **全部进一期**（`src_semantic` + `src_ai_field` + `src_ai_table`）<br>B 只进 `src_ai_field` + `src_ai_table`，`src_semantic` 延后 | **A** —— 你的指令是「3 个必须实现」。⚠️ 但若 10/15 前必须砍一刀，**砍 `src_semantic`**（它是「改写」，另两个是「填表核心」） |
| **Q-19** | 企业文档的**召回/裁剪**怎么做 | A **一期做最小版**（**两级过滤**，见 §15.10；⛔ 不做向量检索）<br>B 一期全量塞入（**会超 token 限**）<br>C 一期不做召回，只支持「单文档 + 提示词」 | **A** —— B 必然失败；C 会让「跨文档分析」这个核心能力打折。~~⚠️ 需确认：`08` 号的召回/精排是否已有可复用实现~~ → **⚠️ 此引用有误，已纠正**：`08` 号 是「能力单元与提示词设计」，其 `§3.1` 的 **L2「精排」指提示词分层**，**不是文档召回管线**；全册检索 `召回/精排/rerank/向量` **在 `08`/`26`/`33`/`36` 号 均 0 命中** ⇒ **本管线是全新设计，无现成实现可复用**（详见 §15.10） |
| **Q-20** | `37` 号 解冻范围 | A **部分解冻**（Tab1 锚点 + Tab2 提示词，其余仍暂缓）<br>B 保持全部暂缓，提示词先**用 SQL 直录**（一期验证） | **A** —— 提示词编辑器是「配规则」的必需界面；B 只能撑过 spike，撑不到试运行 |

### 15.10 ★★★ 企业文档的「两级过滤 + 统一合并」管线（V1.4，回答第 5 轮）

> **用户原话（逐字）**：
> 「我补充下我们针对 **ai 分析**的理解，我们的 ai 分析都有一个特点：
>  **首先通过文档的分类，对所有的文档进行了过滤，只保留有限的 5-10 个文件**；
>  **然后通过文件的语义进行一遍语义分析，这样又缩小了范围，最后得到符合要求的内容，可能就 2-5 个左右**；
>  **这些文档的 markdown 是固定的**，我们是进行文档 ai 分析的时候，**统一做了文档的筛选，并可以换成 markdown 的信息**；
>  这样在**有限的信息**，将**单个 ai 提示词，进行统一合并为一个大的提示词**，进行语义分析，**效率可以接受的**；
>  **得到信息后，再调用操作 skill 进行填充**；
>  并且我们的**填充 skill 是最后统一实现的**，因为**我们不能针对一个文档反复的打开重复操作**」

#### （1）逐段落位：**你描述的每一段，数据落点都已经存在**（★ 本轮最重要的结论）

我把你的描述拆成 7 段，逐段找落点。**结论：⛔ 不需要新建任何表**。

| # | 你的描述 | 数据落点（**实测存在**） | 状态 |
|---|---|---|---|
| ① | **按文档分类过滤** → 只留 **5~10 份** | **`cert_enterprise_doc_profile.DocCategory`** + `.StandardCode` / `.StageCode` / `.TagsJson` / `.SuggestedStandardCodes`，配 `.IsLatest=1` 取最新画像 | ✅ **列齐**；⚠️ **表 0 行**（见 (4)） |
| ② | （同上）**先把「不该分析」的剔掉** | **`cert_enterprise_original_file.AnalyzePolicy`**（`analyze` / `skip` / `ignore`）+ `.IsDeleted=0` | ✅ **列齐**（记忆已定「忽略是**用途级**不是文件级」） |
| ③ | **按文件语义再分析一遍** → 缩到 **2~5 份** | 输入 = ① 的画像轻量字段（`Summary` / `Keywords` / `InfoItemsJson` / `DocPurpose`）；**输出 = 一份入选 `Code` 清单**（无新表，进 `cert_doc_fill_log` 留痕） | 🟡 **需新增 1 次轻量 LLM 调用**（复用 `IAiFillInvoker`，见 §15.4） |
| ④ | **markdown 是固定的**（可换成 markdown 信息） | **`cert_enterprise_original_file.MarkdownPath`** + `.MarkdownStatus='completed'`；画像侧冗余快照 **`cert_enterprise_doc_profile.SourceMarkdownPath`** | ✅ **列齐**；⚠️ **实测 3 行中 2 行 `failed`**（见 (4)） |
| ⑤ | **单个 ai 提示词合并为一个大提示词** | **`cert_doc_fill_prompt.UserTemplate`** 的 **`{{__FILL__.enterprise_docs}}`** 占位符（= **§15.3**，**已有设计**） | 🟡 **表未建**（`S-1`） |
| ⑥ | **得到信息后，再调用操作 skill 进行填充** | **§14.8 调用链**：② 内容类 `src_*` → ③ 操作类 `fill_cell`/`fill_table` | 🟡 待建（`S4`） |
| ⑦ | **填充 skill 最后统一实现**（⛔ 不能反复打开同一文档） | **§14.5「填充 Skill = 声明式装配器，⛔ 不落盘」+ §14.8「层 1 只落盘一次」** | 🟡 待建（`S4`） |

**★ 两处「你说的和我写的其实是同一件事」**：

- **⑤ 你说的「合并成一个大提示词」** = §15.3 的「**一份文档 = 一次请求**」；
- **⑦ 你说的「不能反复打开同一文档」** = §14.5 的「**各 Skill 各写一次会互相抹掉数据**」+ §14.8 的「**层 1 落盘一次**」。
  ⇒ **你的约束不是新增需求，而是我那个设计的动机本身。** 两条独立推理落到同一结论，可信度较高。

#### （2）为什么要「两级」，而不是一级（★ token 经济性）

| 做法 | 送入 LLM 的体积 | 结果 |
|---|---|---|
| **一级：全部文档直接塞** | 假设 60 份 × 平均 8k 字符 ≈ **48 万字符 ≈ 24 万 token** | ⛔ **必然超限**（超主流模型上下文） |
| **一级：只用「分类」筛完就塞** | 5~10 份 × 8k ≈ **4~8 万字符 ≈ 2~4 万 token** | 🟡 勉强可行，但**分类不准时把无关文档一起塞进去了**（噪声拉低准确率） |
| **★ 两级：分类 → 语义 → 只塞 2~5 份** | **2~5 份 × 8k ≈ 1.6~4 万字符 ≈ 0.8~2 万 token** | ✅ **你判断的「效率可以接受」是成立的** |

**★ 两级的分工（★ 关键）**：

- **① 分类过滤 = 便宜且确定** —— 纯 SQL（`DocCategory` + 标准/阶段 + 标签），**0 次 LLM 调用**，把 60 → 5~10；
- **③ 语义过滤 = 贵但精确** —— **1 次 LLM 调用**（且**只读画像摘要**，不读正文），把 5~10 → 2~5；
- **④ 取 markdown = 零 LLM** —— 文件已在 `MarkdownPath` **落过盘**，**直接读**（这就是你说的「markdown 是固定的」的实现含义：**转一次、存起来、反复用，⛔ 不重转**）。

⇒ **只有「③ 语义过滤」这一步是新增的 LLM 调用，且它读的是短文本（画像），成本极低。**
⇒ **整条管线相对「一次填充请求」只多花 1 次轻量调用** —— 这是可接受的。

#### （3）代码形状（接口草案，⛔ 不注册 `wf_skill`）

**★ 它不是 Skill，是编排器的一个部件**（同 §15.4 的 `AiFillBatchService`）：

```csharp
/// <summary>企业文档检索器：两级过滤 + markdown 取用。⛔ 不注册 wf_skill（无独立业务语义，仅服务填充编排）。</summary>
public interface IEnterpriseDocRetriever
{
    /// <summary>① 分类过滤（纯 SQL，0 次 LLM）：按 DocCategory/标准/阶段/标签 粗筛。</summary>
    Task<IReadOnlyList<DocProfileBrief>> FilterByCategoryAsync(DocFilterContext ctx, CancellationToken ct);

    /// <summary>③ 语义过滤（1 次轻量 LLM，读画像摘要）：把 5~10 份缩到 2~5 份。</summary>
    Task<IReadOnlyList<DocProfileBrief>> RerankBySemanticAsync(
        IReadOnlyList<DocProfileBrief> candidates, string requirement, CancellationToken ct);

    /// <summary>④ 取 markdown 正文（零 LLM，直接读 MarkdownPath；带条数/字符上限保护）。</summary>
    Task<IReadOnlyList<EnterpriseDocSnippet>> LoadMarkdownAsync(
        IReadOnlyList<DocProfileBrief> picked, int maxDocs, int maxCharsPerDoc, CancellationToken ct);
}
```

**`DocProfileBrief`（① 的产出，全是画像现成列）**：`Code` / `FileName` / `DocCategory` / `Summary` / `Keywords` / `TagsJson` / `InfoItemsJson` / `SourceMarkdownPath` / `Confidence`。

**★ 三个必须内建的保护**（否则会「静默填错」）：

| 保护 | 为什么 |
|---|---|
| **`maxDocs` / `maxCharsPerDoc` 硬上限** | ② 若语义过滤失效（返回全部），必须**在取 markdown 前截断**，⛔ 不能靠「LLM 会听话」 |
| **`MarkdownStatus != 'completed'` 的必须跳过并记 `Pending`** | 实测 **3 行里 2 行是 `failed`** ⇒ 直接读会拿到空文件，**表现为「AI 说没找到信息」而不是报错** |
| **入选清单写入 `cert_doc_fill_log`** | 出了问题要能回答「这次填充到底看了哪几份文档」——**可审计**（同 §14.8 第 ⑥ 步） |

#### （4）⚠️ 必须正视的两个实测缺口（这是本节的坏消息）

我实测了库里现有数据（`IsValid=1`）：

| 项 | 实测 | 含义 |
|---|---|---|
| `cert_enterprise_original_file` | **3 行** | 上传链**只跑过 3 次** |
| **`cert_enterprise_doc_profile`** | **0 行** | ⛔⛔ **画像表是空的** ⇒ **① 分类过滤没有数据可过滤** |
| `cert_enterprise_original_file.MarkdownStatus` | **failed 2 / completed 1** | ⛔ **2/3 的 markdown 转换失败** |
| `AnalyzePolicy` | analyze 3 | 策略列已生效 |

**⇒ 两级过滤管线的「前置」不是它自己，而是记忆里那条「上传 6 段」链**：
`⓪策略 → 落原始 → 归一 → 转 md → 语义分析 → 落画像 → 进候选`

**⇒ 这条链没跑通（画像 0 行、md 2/3 失败），两级过滤就是空中楼阁。**
**⇒ 处置：`S-1` 的验收标准必须加一条**「**≥1 份企业文档走完 6 段，`doc_profile` 有 1 行且 `DocCategory` 非空**」。
**⇒ ✅ 2026-10-03 实施时已查清 `MarkdownStatus` 2/3 失败的根因 —— ⛔ 不是环境问题**（原判断「大概率是 **LibreOffice 环境**」**作废**）：

| 核查项 | 实测结果 |
|---|---|
| 容器状态 | `yzh-libreoffice` **Up 3 days**；`soffice --version` = **LibreOffice 25.8.1.1 580** ✅ |
| `.doc → .docx` 实测 | OLE2 头 `d0cf11e0`（35,328 B）→ ZIP 头 `504b0304`（9,140 B）；`zipfile.testzip()` **OK**、13 条目、含 `word/document.xml` ✅ |
| `.xls → .xlsx` 实测 | BIFF8 头 `d0cf11e0` → ZIP 头 `504b0304`（5,825 B）✅ |
| 中文文件名 | `质量手册测试.doc` 正常转换，**无空格/中文拆参问题** ✅ |
| 失败**真实**原因 | `MarkdownMessage` = `anydoc: malformed document: not an OLE2 compound file: Invalid CFB file (81 bytes …)` ⇒ **上传的是 81 字节的损坏文件** |
| 全库口径 | 走完双链的 **179 份中 Markdown 失败仅 4 份（2.2%）**，原因同类（文件损坏） |

⇒ **结论：`S-1` 归一链不存在「环境阻塞」，容器侧可直接实施。**「两链合并排查环境」的建议**不再需要**。

#### （5）与 §15.3 的拼图关系

```
两级过滤管线（本节）              §15.3 一份文档一次请求
┌──────────────────────┐        ┌─────────────────────────┐
│ ① 分类过滤  (SQL)     │        │  UserTemplate            │
│   60 → 5~10          │        │   ├ {{__FILL__.anchors}}  │
│         ↓            │        │   ├ {{__FILL__.enterprise_docs}} ← ④ 的产物
│ ③ 语义过滤  (1×LLM)   │───────→│   └ {{__FILL__.template_text}}
│   5~10 → 2~5         │        │  OutputSchema 校验        │
│         ↓            │        │  → {semantic, fields,    │
│ ④ 取 markdown (读盘)  │        │      tables}             │
│   2~5 份正文          │        └─────────────────────────┘
└──────────────────────┘                    ↓
                                  §14.8 ②→③→④（层 1 落盘一次）
```

**★ 关键**：`{{__FILL__.enterprise_docs}}` 的内容 **= 本节 ④ 的产物**。
⇒ **§15.3 之前写「⚠️ 一个必须正视的前置依赖（token 体积）」时只说「必须先做召回 + 裁剪」，本节就是那个「召回 + 裁剪」的具体形态。**

#### （6）对 §15.8 实施顺序的增补：插入 `S2.5`

| 阶段 | 内容 | 可独立验证 |
|---|---|---|
| **S-1** | （增补验收）`.doc/.xls` 归一链 + 建 5 表 + **⚠️ 跑通「上传 6 段」至少 1 份（`doc_profile` 有行、`DocCategory` 非空、`MarkdownStatus='completed'`）** | ✅ **169 份**产出 `.docx`（§13.1a）；5 表建好；**画像有 1 行** |
| **S2** | 取值 Skill 骨架：`src_global_param` + `src_manual` | ✅ 单元测试 |
| **★ S2.5** | **`IEnterpriseDocRetriever` 两级过滤**（① SQL + ③ 1 次轻量 LLM + ④ 读 markdown） | ✅ **给定 1 条需求，能从 N 份文档里选出 ≤5 份并打印清单** |
| **S3** | AI 三件套（`src_semantic` / `src_ai_field` / `src_ai_table`）—— **★ 依赖 `S2.5`** | ✅ 拿 1 份真企业文档 + 1 段提示词，产出 1 个值 |
| **S3.5** | `AiFillBatchService`（一份文档一次请求 + 切片 + 缺项回退）—— **★ 消费 `S2.5` 的产物** | ✅ 10 个 AI 项只发 1 次请求 |

**⇒ 顺序理由**：`S3` 要「从企业文档动态分析」，**它拿到的文档必须已经过 `S2.5` 过滤**，否则就是把 60 份塞进提示词（= `Q-19` 的 B 选项，必然失败）。

#### （7）裁决细化与新增

| ID | 决策点 | 选项 | **建议** |
|---|---|---|---|
| **Q-19**（细化） | 「一期最小版」**具体是什么** | A **就是本节的「两级」**（① 分类 SQL → ③ 语义 1 次 LLM → ④ 读 markdown）<br>B 只做 ①（分类粗筛），③ 语义过滤延后<br>C ①② 都不做，全量塞（**必然超限**） | **A** —— 你第 5 轮已经把「两级」说清楚了，**且 ③ 只多 1 次轻量调用**；B 会让「语义缩到 2~5 份」这个**准确率来源**丢失 |
| **★ Q-21**（新增） | 第 ③ 级语义过滤**读什么** | A 读**画像摘要**（`Summary`+`Keywords`+`InfoItemsJson`，短，便宜）<br>B 读 **markdown 正文**（准，但 5~10 份 × 8k = token 高）<br>C **两级都用**：③ 用画像，④ 才读正文 | **C** —— **这正是你原话的顺序**：「先按语义缩小范围」→「这些文档的 markdown 是固定的……换成 markdown 的信息」。A 单用会在画像质量差时选错；B 单用等于没做过滤 |

---

## 附：本计划与前后文档的关系

```
27 号（6 类规则）───┐
22 号（三元组）  ───┼──→ 38 号（本文）──→ 37 号（综合页面）
Office/ 层 1（已有）─┘
                      ↓
               S7 试跑端点 ⇒ 37 号的 Tab2「试跑」按钮直接复用
```

> **一句话**：`27` 号说「有哪些填法」，`22` 号说「一个填写单元是什么」，
> **本文说「这些填法怎么变成可调用、可验证、Word/Excel 通吃的 Skill」**，`37` 号说「怎么把它们编排成页面」。
