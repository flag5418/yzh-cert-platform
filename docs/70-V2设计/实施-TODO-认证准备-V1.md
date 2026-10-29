# 实施 TODO — 认证准备（文档提取 + 疑点自动生成）V1

> **依据**：`docs/70-V2设计/35-文档提取卡与条款检索-V1.md`（V2 设计稿）
> **用法**：完成一项勾一项；⛔ 未勾的不要跳过去做后面（后面依赖前面的产物）
> **当前进度**：**阶段 1 已完成**（iso13485 67/67 条款 × 9 属性，100% 覆盖）；下一步 = 阶段 2 L1 提取

---

## 阶段 0 · 前置（已完成）

- [x] 0.1 设计定稿：35 号 V2（4 张表 / `DocCheckMode` 三态 / L1+L2 两个提示词 / 触发流程）
- [x] 0.2 实库核对：iso13485 = 88 条款；`cert_clause_attr` 仅 `4.1`/`4.2.1` 有属性
- [x] 0.3 技术验证：MySQL 8.0.46 `JSON_TABLE` 可用；`FIND_IN_SET` 逗号精确匹配可用
- [x] 0.4 可复用件确认：`LlmInvokeService` / `IPromptWorkbench` / `ICoreResourceQuery`

---

## 阶段 1 · 属性池与条款属性（当前）

### 1.1 属性池加 `DocCheckMode`

- [x] 字典 `clause_doc_check_mode`（`required` / `optional` / `none`）
- [x] 属性行 `DocCheckMode`（`ValueType=dict`，`Sort=85`，写好 `PromptHint`）
- [x] 脚本：`scripts/db/20261029_clause_doc_check_mode_V1.sql`（幂等）—— 实库执行通过，属性池 9 条

### 1.2 完善属性池页 `/cert/clause-attr-def`

- [x] 列表显示「取值要求」`PromptHint`（`XsFlag` 改 `true`，宽度 360；长文本走 tooltip）
- [x] 确认新增/编辑弹窗里 `DocCheckMode` 的字典联动正常（`dict` ⇒ `DictCode` 必填，`TYPES_NEED_DICT` 已覆盖）
- [x] 顺手清掉属性池里的临时测试行 `TempAttr`（软删）

### 1.3 检查条款属性页 `/cert/clause-attr`

- [x] 确认「AI 预生成」参数：标准选择 / 覆盖已配 / 分批 —— 可用
- [x] 确认提示词抽屉（`PromptDrawer`）能编辑 `clause_attr` 提示词
- [x] **★ 修掉两个致命断链**（否则跑不动 / 跑了也白跑）：
  - 提示词唯一来源改回 `cert_prompt_template.clause_attr_gen`：`ClauseAttrGenerateService` 原读
    `wf_prompt_template.clause_attr_v1/v2`，而抽屉编辑的是前者 ⇒「改了正文没变化」。
    现统一走 `ClauseAttrPromptService`（`GetTemplateAsync` + `RenderWithSection`）。
  - 属性清单改由 `{{attr_pool}}` 实时渲染：v1/v2 正文把 8 条属性**写死在 JSON 骨架**里
    ⇒ 新增 `DocCheckMode` 后模型不产该字段、落库静默缺失。V3 正文（1750 字）只写判据，字段清单唯一来源 = 属性池。
  - 两条 `clause_attr` 旧提示词 `IsActive=0` 且 `ResolveActiveAsync` 命中 0 条直接返回 null ⇒ 预生成必报
    「未找到生效的提示词」。现已在 `wf_prompt_template` 标注**已弃用**（脚本 `20261029_clause_attr_prompt_V3.sql`）。
- [ ] 覆盖率展示（88 条款 × 9 属性 = 792 格）—— 暂用 SQL 核对，页面角标后置

### 1.4 跑完 iso13485 的 88 条款 × 9 属性

- [x] 分批跑（20 条款/批，脚本 `temp/run_13485_all.py`；4 批 8m47s，失败 0）
- [x] 前置条款 1/2/3 跳过并写 `Message`（⛔ 不静默）—— 每批 message 都带「跳过 3 条前置条款（1、2、3）」
- [x] 覆盖率：88 条款 − 18 非叶 = **70 叶**；70 − 3 前置 = **67 条应配，实配 67 条（100%）**；
      属性行 67 × 9 = **603 格**，空值 44 格全部是 `ApplicabilityCondition`（`Applicability=general` 时应为空，正常）

### 1.5 抽查属性质量（5 条）

- [x] `Artifacts` / `EvidenceChain`：**67/67 首句都是输出契约**（「只输出 JSON 数组…」）；
      `4.1` / `4.2.1` 原用旧提示词生成（散文开头），已按 V3 重跑纠正
- [x] `DocCheckMode`：`required×50 / optional×17 / none×0`。
      ★ 结论修正：`none=0` **不是缺陷** —— ISO 13485 是重记录标准，连「管理承诺」都要看管理评审记录，
      没有「纯现场、完全不看资料」的条款。原 TODO 里「`none` 不应为 0」是我拍脑袋的假设，作废。
      但 75% `required` 偏高，L2 阶段过滤收益有限 ⇒ 见下方 D-1
- [x] `Process`：`1 个×26 / 2 个×12 / 3 个×28 / 4 个×1`。★ 结论修正：原「1~3 个」硬上限**是我发明的**，
      `7.3.4 设计和开发输出` 同时约束设计/采购/生产/检验 四个过程是**业务正确**的，截断反而丢部门
      ⇒ 已把 `Process.PromptHint` 改为软约束（「通常 1~3，确有必要可到 4，⛔ 不许全选」）
- [x] `Severity`：`major×51 / critical×16 / minor×0` —— `critical` 落在 4.1/4.2.3/5.1/5.2/6.4.2/7.3.7/7.5.2/7.5.5/
      7.5.6/7.5.7/7.5.8/7.5.9.1/7.5.9.2/8.2.3/8.2.6/8.3.3，全是法规强制 / 无菌 / 可追溯相关，判得住
- [x] `Obligation`：`shall×67`。★ 结论修正：抽 12 条正文含「宜/可」的条款逐条核对，
      **主句全是「应」**（「宜/可」只出现在子项里）⇒ 100% shall 是 ISO 13485 的客观事实，不是模型偷懒

### 1.6 待裁决（下一轮开工前定）

- **D-1 `DocCheckMode` 的 `required` 比例**：现 50/67（75%）。若希望 L2 更省 token，
  可在 `PromptHint` 里加一条「只有**记录类**证据才给 required；含现场观察/访谈能判的一律 optional」。
  ⚠️ 改动会要求全部重跑（≈9 分钟 / 600 次调用）。**默认建议：先不动**，L2 跑一批看实际收益再定。
- **D-2 前端守卫新增债务**（本轮之前做「认证计划」时引入，非本轮改动，但会挡 `npm run build:*`）：
  `cert-auditor/src/pages/audit-plan/index.vue` R6 16 处内联 `<el-table>` / S04×3 / S07×2 / S08×11、
  路由 `/audit-plan` 无菜单入口（R12）、`CertQueryController` 直接继承 `ControllerBase`（R-B）。
  ⇒ 要么改页面用 `YzhTable` + 补菜单 + 换基类，要么登记进基线。**需用户定**。

---

## 阶段 2 · 文档提取（L1）

### 2.1 建表

- [ ] `cert_document_extraction`（`FileCode+StandardCode` 唯一；`ClauseNumbers` 逗号；`ContentJson`；`SourceSha256`；`PromptVersion`）
- [ ] DDL 脚本 + 实库执行；⚠️ `UpdateTime` 必须 `NULL DEFAULT NULL`

### 2.2 提示词 `doc_extract`

- [ ] 写 V1（输出 `{docMeta, clauses:[{clauseNumber, support, facts:[{text,anchor}], why}]}`）
- [ ] 四条硬规则写进提示词（只输出有实质内容的条款 / 必须带锚点 / 不判符合性 / 编号逐字取自清单）
- [ ] 落 `wf_prompt_template`（`PromptCode=doc_extract`，`ScopeKey=iso13485`）

### 2.3 提取执行器 + 接口

- [ ] `DocExtractRunService`：读文档 Markdown + 提示词 + 条款清单（只给 `DocCheckMode≠none`）→ LLM → 落表
- [ ] 缓存：`SourceSha256` 未变则跳过
- [ ] `POST /api/Admin/Workflow/DocExtractRule/extract`（fileCode + standardCode）
- [ ] `POST .../extract-batch`（按企业 + 标准批量跑）

### 2.4 样本验证

- [ ] 选 12 份文档（手册 1 / 程序 4 / 记录 4 / 台账 1 / 证书 2）
- [ ] 跑完并落库

### 2.5 质量验收（3 个数字）

- [ ] 平均每份输出 **3–8 个条款**
- [ ] 事实带锚点比例 **> 80%**
- [ ] 抽查 3 份，`clauseNumber` 全部来自条款清单（无自造编号）

---

## 阶段 3 · 认证准备（L2）

### 3.1 建表

- [ ] `cert_readiness_run`（批次：`TriggerKind` / `ScopeClauses` / 进度）
- [ ] `cert_clause_gap`（四态 / `HitsJson` / `MissingKinds` / `ConflictsJson` / `GapReason`）
- [ ] `cert_clause_point`（疑点：`PointKind` / `Mode` / `Severity`+来源 / `Action` / `EstMinutes`）

### 3.2 提示词 `readiness`

- [ ] 写 V1（8 个输入变量 + 输出契约 + 7 条判定规则）
- [ ] 重点校验 R3（切片空 ⇒ `unknown`，⛔ 不得 `missing`）与 R6（`none` ⇒ 只出 `observe/interview`）

### 3.3 切片拼装 + 判定服务

- [ ] `ClauseSliceService`：`FIND_IN_SET` 取该条款全部切片 → 拼成 `{{doc_slices}}`
- [ ] 去重 + 上限（每条款事实 ≤ 60 条 / 文档 ≤ 15 份，超出标注截断）
- [ ] `ReadinessService.Run(clauseCode)` → 落 gap + point

### 3.4 批次触发

- [ ] 上传完成后自动建 run（`TriggerKind=upload`，`ScopeClauses` = 新文档的条款并集）
- [ ] 手动全量入口（`TriggerKind=manual`）
- [ ] 失败重试（只重跑 `failed` 条款）

### 3.5 端到端跑通

- [ ] iso13485：8 条款（4.1 / 4.2.1 / 4.2.4 / 4.2.5 / 5.6 / 8.2.4 / 6.4.1 / 7.5.1）
- [ ] 每个条款产出 gap + point
- [ ] `unknown` 与 `missing` 可区分（抽查看文案与理由）

---

## 阶段 4 · 页面

- [ ] 4.1 条款缺口页（条款树 + 四态 + 命中文档 + 锚点，可下钻原文件）
- [ ] 4.2 疑点清单页（条款 × 部门矩阵；`Mode` 远程/现场筛选；`Status` 改状态留痕）

---

## 阶段 5 · 全量

- [ ] 5.1 237 份文档全量 L1（增量跑，Sha256 命中跳过）
- [ ] 5.2 88 条款全量 L2 → 缺口 + 疑点
- [ ] 5.3 缺口覆盖率报告（`found/partial/missing/unknown` 四态分布）

---

## 后置（本 TODO 不含）

- 入场计划（34 号 §四：`cert_onsite_plan` + `_slot`，裁剪 + 按部门重排 + 半天粒度排班）
- 证据链 `cert_clause_evidence`
- 检查报告

---

## 风险提示（做的时候盯住）

| # | 风险 | 盯法 |
|---|---|---|
| 1 | `unknown` 被写成 `missing` | 每阶段出口都查一次文案与颜色 |
| 2 | 条款属性不齐就往下走 | 1.4 覆盖率 < 90% 不进入阶段 2 |
| 3 | 提示词改版后旧 JSON 解析失败 | `PromptVersion` 不一致时标记"需重跑"，⛔ 不静默 |
| 4 | 一次跑全量超时 | 一律分批（20 条款/批） |
