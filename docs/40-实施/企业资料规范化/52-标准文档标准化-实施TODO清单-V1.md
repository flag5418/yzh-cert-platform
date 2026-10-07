# 52 · 标准文档标准化 · 实施 TODO 清单 V1

> 日期：2026-10-05
> 依据：**已批准的原型** `原型/51-标准文档标准化-原型-V1.html`（自检 `check-51.mjs` **35/35**）
> 用户口径（逐字）：
> ① 「现在**原始文件**，显示**全局规则**，（第一次未设置，我们其实已经可以通过语义分析得到分组和文档作用信息，这些根据原始文件生成得出的）」
> ② 「**只做逻辑功能**，我们功能稳定后，会来验证 ai 节点提示词的可用性」
> ③ 「先做功能设计，**确定逻辑的存储和逻辑的控制**，测试往后放」
> ④ 「**我现在需要的功能开发**」

---

## 〇、一句话说清本次改动的形状

**不是新建页面，是「改造已有页面 + 补齐后端 6 个缺口」。**

```
已有（⛔ 别重建）                              本次要改
──────────────────────────────────────────   ──────────────────────────────────
pages/workflow/doc-fill-rule/                index.vue  → 干掉九宫格，改 2 Tab
  index.vue            34 KB                  logic.ts   → 默认 Tab / 三态 / readyOf
  logic.ts             30 KB                  18 个组件  → 保留 5 / 改 9 / 重写 2
  components/          18 个文件
  *.test.ts             6 个文件
cert-share/src/api/workflow/
  doc-fill-rule.ts     22 个导出             → 补 2 组 API + 1 个字段
router/index.ts:27                            ✅ 已登记，⛔ 不动
菜单 MENU_00218 → /business/doc-fill-rule      ✅ 已登记，⛔ 不动
后端 4 控制器 + 22 个端点                      → 补 6 处
```

---

## 一、盘点：现有资产 vs 缺口

### 1.1 后端已有（⛔ 不要重建）

| 控制器 | 路由前缀 | 已有端点 |
|---|---|---|
| `DocTemplateController` | `api/Admin/Workflow/DocTemplate` | `candidates` · `register` · **`upload-template`** · **`directory-tree`** · **`set-prompt`** · `tree` |
| `DocTemplateAnchorController` | `…/DocTemplateAnchor` | **`scan`** · `validate` · `publish` · **`save-batch`** · `clear` · `keys` + 基类 CRUD |
| `DocFillPromptController` | `…/DocFillPrompt` | `resolve` · `versions` · `codes` + 基类 CRUD |
| `StandardDocContractController` | `…/StandardDocContract` | `detail` · `save` · **`analyze`** |

★ **`StandardDocContract/Analyze` 已经实现了 3 件事里的 2 件**：

- ✅ **C 字段提取** —— 复用 `DocExtractionRuleService.AIAnalyzeAsync`，**已通**
- ✅ **A 锚点扫描** —— 无空白模板时如实返回 `blocked`（**不报错**），**已通**
- ⛔ **B 文档语义分析**（分类 / 作用 / 标签）—— `StandardDocContractController.cs:358` **硬编码 `Status = "not_wired"`**

### 1.2 前端已有

| 文件 | 大小 | 处置 |
|---|---|---|
| `pages/workflow/doc-fill-rule/index.vue` | 34 KB | **重写**（九宫格 → 2 Tab） |
| `.../logic.ts`（`DocFillRuleLogic extends TreeTableLogic<any>`） | 30 KB | **改** |
| `.../components/AnchorRuleTab.vue` | 16 KB | 改 |
| `.../components/AnchorSidePanel.vue` | 26 KB | **重写**（数据源 5 类 + 操作自动推导 + 锁定） |
| `.../components/ContractTab.vue` | 17 KB | 并入「全局规则」Tab |
| `.../components/PromptPanel.vue` | 34 KB | 改（条件显示） |
| `.../components/PreviewPane.vue` | 10 KB | 改（三视图） |
| `.../components/ValidateTab.vue` | 12 KB | 保留 |
| `.../components/RegisterTemplateDialog.vue` | 7 KB | 保留 |
| `.../components/MaterialArea.vue` | 5 KB | 保留 |
| `.../components/sourceSpec.ts` | 11 KB | **改**（对齐 5 类） |
| `.../components/anchorStats.ts` | 6 KB | 保留 |
| `.../components/promptCode.ts` | 2 KB | 保留 |
| 6 个 `*.test.ts` | — | 改 |

### 1.3 ★★ 6 个缺口（本次要补的全部）

| # | 缺口 | 类型 | 实测证据 |
|:--:|---|---|---|
| **G1** | **`FixedDocSubtype` 列在库、实体无属性** | 存储 | `cert_standard_doc_contract` DDL **有**该列（`varchar(20) NOT NULL DEFAULT 'enterprise_provided'`）；全仓 `src/` grep **0 命中** ⇒ ORM 看不见 | ✅ **已修**（A3） |
| **G2** | **`cert_doc_template_anchor` 无「锁定」列** | 存储 | 表 **37 列**（实测）里**没有** `Locked` / `IsConfirmed` ⇒ 第 20 轮需求无处落 | ✅ **已修**（A1/A2） |
| **G3** | **`Semantic` 段未接** | 控制 | 原 `StandardDocContractController.cs:358` 硬编码 `not_wired` | ✅ **已修**（B2/B3/B4） |
| **G4** | **`EnterpriseDocNormalizationExecutor` 未注册 DI** | 控制 | 原 `CertPlatformAdminServiceExtensions.cs:82` 只注册了 `OfficeConvertTaskExecutor` | ✅ **已修**（B1） |
| **G5** | **「试填 / 预览」端点不存在** | 控制 | 全仓无 `FillPreview` / `TryFill` 端点（grep 只命中内部 Filler 类） | ⏳ 待做（B7/B8，**已裁定新建 `DocFillPreviewController`**） |
| **G6** | **文档填写未进专家任务队列** | 控制 | `ItemType` 仅 `nc_check` / `report_section` | ⏳ 待做（B9，**已裁定延后**） |

---

## 二、TODO 清单

### A 组 · 存储层（DDL + 实体）

| # | 做什么 | 落在哪 | 验收标准 |
|:--:|---|---|---|
| **A1** | 加列 `IsLocked tinyint(1) NOT NULL DEFAULT 0`（**单列**；⛔ 不加 `LockedBy`/`LockedTime`） | 新脚本 `scripts/db/20261005_doc_template_anchor_locked_V1.sql` | `SHOW COLUMNS` 有该列；存量 5 行全为 0 |
| **A2** | `DocTemplateAnchor` 加 **`IsLocked` 一个属性**（⛔ **不加** `LockedBy` / `LockedTime` —— Q1 已裁定「只加一个标志位」，`BaseEntity` 的 `UpdateBy`/`UpdateTime` 已记录人与时间） | `Entities/Doc/DocTemplateAnchor.cs` | 编译 0 error |
| **A3** | `StandardDocContract` 加 `FixedDocSubtype` 属性（`string`，默认 `enterprise_provided`），放在 `DocCategory` 之后 | `Entities/Doc/StandardDocContract.cs` | 编译 0 error；`Grep FixedDocSubtype src/` 有命中 |
| **A4** | **只读复核**：`SourceSpec`(json) + `WriteMode` + `AnchorKind` 是否已够承载「数据源 5 类 / 操作 2 类」 | 只读，⛔ 不改表 | 结论回填本文档 §五 |

> ⚠️ **A3 是「加了就必须有人写」的列** —— 见 §五 风险 R1。

### B 组 · 控制层（后端）

| # | 做什么 | 落在哪 | 验收标准 |
|:--:|---|---|---|
| **B1** | 注册 `EnterpriseDocNormalizationExecutor` 到 DI | `CertPlatformAdminServiceExtensions.cs`（照 `OfficeConvertTaskExecutor` 双行写法） | 启动无异常；`ent_doc_normalize` 任务能分发 |
| **B2** | 接 **`Semantic` 段**：复用 `PromptWorkbenchService.AnalyzeForQueueAsync`（`public`，`:567`） | `StandardDocContractController.Analyze`（替换 `:358`） | 分析后 `cert_standard_doc_contract` 有行、`AnalyzeStatus=completed` |
| **B3** | 语义分析**落库**：`TagsJson` / `DocPurpose` / `InfoItemsJson` + 元数据（`ModelName` / `PromptCode` / `PromptTokens` / `CompletionTokens` / `DurationMs`） | 同上 + `StandardDocContract` | 一文件一契约（`uk_standard_file_code`）幂等 |
| **B4** | `IsManualCorrected = true` 的行，**批量重跑不得覆盖** | 同上 | 单测覆盖 |
| **B5** | 锚点**锁定**端点：`POST lock` / `POST unlock`（或走 `save-batch` 带 `IsLocked`） | `DocTemplateAnchorController` | 加锁成功、可解锁 |
| **B6** | **重扫 merge**：`scan` 时把 `IsLocked = 1` 的旧配置 merge 回新扫描结果 | `DocTemplateAnchorController.scan` | 单测：加锁 → 换模板重扫 → 配置还在 |
| **B7** | 新增「**试填 / 预览**」端点 —— **新建 `DocFillPreviewController`**（`api/Admin/Workflow/DocFillPreview`），职责 = ① 试填 ② **生成试填后的 PDF** | 新建 `Controllers/Workflow/DocFillPreviewController.cs` | 返回填充后文档 + 预览 PDF 产物路径 |
| **B8** | 试填取值链：复用 `Shared/Fill` 取值 + `Shared/Office` 的 Filler（⛔ 不新造） | 同上 | 测试基线不破 |
| **B9** | `ItemType` 新增文档填写（如 `doc_fill`） | `CertPlatform.Auditor/Services/Expert/ExpertTaskConst.cs:101-102` | 专家任务能挂文档填写项 |

> ⚠️ **B7 / B8 是本次最大的一块**，且**前置依赖 B1~B6** —— 锚点规则没配好就没法试填。

### C 组 · 前端页面（V6 → 51 原型）

| # | 做什么 | 落在哪 | 对应 |
|:--:|---|---|---|
| **C1** | **干掉一级功能九宫格**，右侧改 **2 Tab**（锚点规则 / 全局规则） | `index.vue` | ⛔ 用户第 19 轮 |
| **C2** | ★ **默认落「全局规则」Tab** | `logic.ts` + `index.vue` | ★ 用户第 22 轮口径 |
| **C3** | **类型自动判定**：`parseable=false`（图片 / PDF）⇒ 固定文档 + 锁定 | `logic.ts` | 原型 §三 |
| **C4** | **锚点按字段 / 表格分组** → 点开 = 设置页 | `AnchorRuleTab.vue` | 原型 §四 |
| **C5** | 设置页**数据区域**：数据源 5 类 + 参数 + 是否关联企业文档 + AI 提示词 | `AnchorSidePanel.vue` | 原型 §四 |
| **C6** | 设置页**操作区域**：**操作方式自动推导**（字段⇒单元格更新 / 表格⇒表格更新），⛔ 不让人选 | 同上 | ★ 第 20 轮第 4 条 |
| **C7** | **锚点「锁定」** UI（列表行 + 设置页顶部） | `AnchorRuleTab.vue` + `AnchorSidePanel.vue` | ★ 第 20 轮第 2 条 |
| **C8** | **锚点未配齐 ⇒ 禁用「自动填充 / 预览」** + 一条警示 | `AnchorRuleTab.vue` | ★ 第 20 轮第 3 条 |
| **C9** | 「上传空白模板 / 下载原始文档」**同一行** | `PreviewPane.vue` | ★ 第 20 轮第 1 条 |
| **C10** | 中栏**三视图**（原始文档 / 空白模板 / 填充后预览） | `PreviewPane.vue` | 原型 §一 |
| **C11** | **全局填写规则条件显示**：无 ai 节点整块不显示；有则 +「自动分析」+ 每段单独保存 | `PromptPanel.vue` | 原型 §二 |
| **C12** | **状态三态徽标**（已设置 / 正在设置 / 未设置），⛔ 不弹提醒、⛔ 不拦保存 | `logic.ts` `statusOf()` | 原型 §五 |
| **C13** | **帮助浮层「!」**：Tab 行右侧圆形按钮，按 Tab 给条目；再点 / 点浮层外 / 切 Tab 三处收起 | `index.vue` | ★ 第 21 轮 |
| **C14** | **注释收敛**：清掉旧页面的 `tip` / `hint` / `note` / 长段说明 | 全页 | ★ 第 21 轮 |
| **C15** | 清理 V6 遗留：九宫格相关 props / 死代码 / 无引用入口 | 各组件 | 无死代码 |

> ★ **第 28 轮进度**：**C1 · C2 · C3 · C4 · C7 · C8 · C9 · C12 · C13 · C14 · C15 ✅ 已完成**；
> **C5 · C6 · C10 · C11 ⏳ 待做**（其中 **C11** 需先定「ai 节点 vs 提示词版本」口径）；
> **C3 的「自动填充 / 预览按钮」部分**依赖 B7/B8（已降级 TODO，⛔ 本轮不造）。
> 逐条证据 → **§十三**。

### D 组 · API 层

| # | 做什么 | 落在哪 |
|:--:|---|---|
| **D1** | 补「试填 / 预览」API | `cert-share/src/api/workflow/doc-fill-rule.ts` |
| **D2** | 补「锚点锁定」API | 同上 |
| **D3** | 对齐 `SOURCE_KINDS` 与原型 5 类（`global` / `ai_gen` / `ai_field` / `ai_table` / `ai_prompt`） | `components/sourceSpec.ts` |
| **D4** | `DocContractDetail` 加 `FixedDocSubtype` 字段 | `api/workflow/doc-fill-rule.ts:333` |

> ★ **第 28 轮进度**：**D2 · D4 ✅ 已完成**；**D1 ⏸**（依赖 B7/B8）；**D3 ⏳**（原型是 mock，
> 真值 7 类 vs 原型 5 类，**需先裁定**，⛔ 不对齐就等于按 mock 改真值）。

### E 组 · 测试与验收

| # | 做什么 | 验收标准 |
|:--:|---|---|
| **E1** | 后端 `dotnet test` 基线不破 | 现全绿 → 仍全绿 |
| **E2** | 后端新增单测覆盖 B2 / B4 / B6 / B8 | 每条至少 1 个用例 |
| **E3** | 前端 `logic.test.ts` 重写覆盖 C2 / C3 / C12 | `vitest` 绿 |
| **E4** | 前端 `vue-tsc` 0 error | `npm run build` 绿（`CODEBUDDY_SAFE_DELETE_ENABLED=0`） |
| **E5** | 菜单 URL 同步（**仅当改了 URL**） | 跑 `scripts/db/verify/sync_menu_urls.sh` |
| **E6** | 原型自检保持绿 | `node check-51.mjs` = **35/35** |

---

## 三、顺序与依赖（关键路径）

```
A1 ──→ A2 ──┐
            ├──→ B5 ──→ B6 ──┐
A3 ─────────┘                │
                             ├──→ C7 ──→ C8 ──→ C10 ──┐
B1（独立，随时可做）          │                        │
B2 ──→ B3 ──→ B4 ──→ C11 ────┤                        ├──→ D1 ──→ B7 ──→ B8 ──→ E1
                             │                        │
C1 ──→ C2 ──→ C3 ──→ C4 ──→ C5 ──→ C6 ──→ C9 ────────┘
C12 ──→ C13 ──→ C14   ← 收尾，必须最后做（改的是同一个 index.vue）
B9（独立，可延后）
```

### 建议开工顺序（4 批）

| 批次 | 内容 | 为什么是这个顺序 | 状态 |
|:--:|---|---|:--:|
| **第 1 批** | **A1 · A2 · A3 · B1** | 全是「一行级」改动，一次消掉 **3 个静默缺口**（G1 / G2 / G4），⛔ **不动任何业务逻辑** ⇒ 风险最低、见效最快 | ✅ |
| **第 2 批** | **B2 · B3 · B4** | 语义分析落库 = 「原始文件 → 全局规则」的燃料，**正是你说的第一件事** | ✅ |
| **第 2.5 批** | **B5 · B6** | 锚点锁定（C7 / C8 的前置）+ 重扫去向回报；**不写模糊匹配**（见 §八） | ✅ |
| **第 3 批** | **C1 ~ C15** | 界面重写 —— 后端形状已定（B1~B6 全绿），**第 28 轮已开工并完成 11 条** | ▶ |
| 独立可做 | **D 组（API 4 条）** | 纯 TS 声明，**不阻塞** C 组；`D2`（锁定 API）已可落笔 | ⏳ |
| 延后 | **B7 / B8 / B9** | 见 §四 | ⏸ |

> ★ **第 28 轮收尾（2026-10-05）**：第 3 批已完成 **C1 · C2 · C3 · C4 · C7 · C8 · C9 · C12 · C13 · C14 · C15**
> 共 11 条（+ 顺带 **D2 · D4**）⇒ 开工记录见 **§十三**。
> ⛔ **本轮未动后端**；**B7 / B8 / B9 保持「延后」不变**（用户已裁决降级 TODO）。

---

## 四、本次**不做**（明确边界）

| 不做 | 为什么 |
|---|---|
| ⛔ 补真实样本（PDF / 空白模板） | 用户口径：「测试往后放」 |
| ⛔ 配 AI 提示词内容 | 用户口径：「只有通过企业资料上传后才能真正的测试」 |
| ⛔ 补第二个标准 | 用户口径：「我现在需要的功能开发」 |
| ⛔ 专家端规则录入 UI | 项目既定范围 |
| ⛔ 文档填写进队列（B9） | 可延后，不阻塞主链 |

---

## 五、风险与静默陷阱

| # | 风险 | 应对 |
|:--:|---|---|
| **R1** | **A3 加了 `FixedDocSubtype` 却没人写它** ⇒ 又是一次「列在库、值恒默认」 | B3 落库时必须一并写该列，否则「可替换性」永远读不到真实值 |
| **R2** | **B1 注册 DI 可能形成循环** | `OfficeConvertTaskExecutor` 的注释已警告：`QueueManager` 构造注入 `IEnumerable<IYzhTaskExecutor>` ⇒ 照它的双行写法（`AddSingleton<X>()` + `AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<X>())`） |
| **R3** | **B2 单文件同步调 2 次 LLM（≈15~25s）** | 前端超时须 ≥ **120s**；批量走队列 |
| **R4** | **C13 / C14 改的是同一个 `index.vue`** | 先改结构再收敛注释，⛔ 不要并行改 |
| **R5** | **`Edit` 工具可能「报成功但未落盘」** | 每次编辑后 `git diff` / `Grep` 复核 |
| **R6** | **前端 `controllerName` 必须含 `Admin/`** | 漏段 = 静默 404 |
| **R7** | **`cert_standard_doc_contract` 一文件一契约**（`uk_standard_file_code`） | B3 落库走「**含已删查重 + 复活**」，⛔ 不直接 INSERT |
| **R8** | **`json` 列不收空串** | 空数组发 `'[]'`；空串 ⇒ 整条 INSERT 失败 |
| **R9** | **`Analyze` 的 A / C 两段已通** | ⛔ 不要顺手重写它们 —— 只补 B 段 |

---

## 六、现在做什么 / 什么延后 / 为什么

- **已完成**：第 1 批（A1 / A2 / A3 / B1）→ 第 2 批（B2 / B3 / B4）→ 第 2.5 批（B5 / B6）
- **下一步（二选一，见 §九 待裁决）**：
  - **A**：**D 组（API 4 条）** —— 纯 TS 声明，把后端已经定型的形状先落到前端 API 层，为 C 组铺路；
  - **B**：**第 3 批 C 组（前端 15 条）** —— 界面重写（用户已明确「先后端再前端」，后端主链已通）。
- **延后**：试填链路（B7 / B8）、队列接入（B9）、**一切测试与数据准备**
- **为什么**：你的口径是「先确定**逻辑的存储**和**逻辑的控制**」——
  **存储** = A 组 + D4；**控制** = B 组前半 + C 组的判定函数（C2 / C3 / C6 / C8 / C12）。
  试填与测试**都依赖这两件事**，只能往后排。

---

## 九、待裁决（2026-10-05 第 2.5 批收尾时浮现）

| # | 问题 | 现状 | 建议 |
|:--:|:--:|---|---|
| **Q3** | 换版后「**唯一键变了**的锁定锚点」（典型：Excel 改了工作表名），配置要不要**自动搬运**？ | ⛔ **不搬**，只如实回报 `Locked.LostRefs` | **维持不搬**。搬运靠「引用名相同」= 猜；猜错会把 A 字段的来源写到 B 字段上且写库显示成功。若你希望搬，需要一条**可判定的规则**（例如「同一模板内引用名唯一才搬」）——**请裁决** |
| **Q4** | `cert_standard_doc_contract` 在语义分析**失败**时要不要建行留痕？ | ⛔ **不建**（只写日志 + 响应里回报） | **维持不建**。契约是**业务对象**，为记录失败而建空壳会让 `detail` 返回 `Exists=true`，前端显示一份「存在的空契约」——比丢一条日志更糟 |
| **Q5** | 锁定后要不要**禁止**再改该锚点的配置？ | ✅ **已裁决（2026-10-05 第 26 轮）**：**禁止** | **用户逐字**：「**锁定的问题，就是不能再修改配置**」⇒ 见 §十一。⛔ 此前「维持不禁」的建议（理由「锁定只是标记、不是权限位」）**已作废** —— 那条理由把 49-V4 的 P2'（「程序不阻断业务组合」）错误地套到了「锁定之后还能不能改」上，两者是不同问题 |

---

## 十、API 实测记录（2026-10-05，后端重启后逐条跑通）

> 依据用户口径：「**测试放缓，但不意味着我们不能通过 postman 等工具，自己提供数据进行接口测试**」
> 方式：`curl` + 库内复核。测试数据**已清理**（库已回到测试前状态）。

### 10.1 ★★★ 一个必须纠正的认知：语义提示词**不是空的**

**此前结论**（本清单 §八 第 2 批注释里也写过）：「提示词内容全空 ⇒ `AnalyzeForQueueAsync` 必然失败」。
**实测推翻**。真相是**两张不同的表**：

| 表 | 用途 | 现状 |
|---|---|---|
| **`wf_prompt_template`** | **语义分析**提示词（`doc_group` / `doc_content` / `doc_essential`） | ✅ **有内容**：`doc_group_iso9001`（4,683 字）· `doc_content_iso9001`（5,161 字），`IsActive=1`，`StandardCode=846dec4b-…`（= 全部 669 份文件所属标准） |
| `cert_doc_fill_prompt` | **文档填写**提示词（新表） | ⛔ 2 行，`SystemPrompt` / `UserTemplate` 全 NULL |

⇒ 「提示词全空」说的是**填写提示词**，被错误地套用到了**语义提示词**上。
**教训**：说「X 是空的」之前必须先确认**是哪张表** —— 两张表名字都带「prompt」，但一个是工作流提示词模板，
一个是本模块新建的填写提示词表。

### 10.2 逐条结果

| # | 端点 | 入参 | 结果 |
|:--:|---|---|---|
| 1 | `POST /api/User/login` | `admin` / `123456` | ✅ 拿到 Token（548 字符） |
| 2 | `GET …/DocTemplateAnchor/keys` | `templateCode=2078f2d4…` | ✅ `Total=4 / LiveCount=2 / DeletedCount=2 / LockedCount=0`，每项带 `IsLocked` |
| 3 | `POST …/DocTemplateAnchor/lock` | `codes:[e5725c24…], locked:true` | ✅ `Affected=1, LockedTotal=1`；⚠️ **如实告警**「1 个是孤儿（最近一次扫描已消失）」 |
| 4 | `POST …/lock` | `locked:false` | ✅ `Affected=1, LockedTotal=0` |
| 5 | `POST …/lock` | 重复解锁 | ✅ `Affected=0, Unchanged=1`（**幂等**） |
| 6 | `POST …/lock` | `codes:["NOT_EXIST"]` | ✅ `success=false` + 「未找到要操作的锚点（可能已删除，或不属于该模板）」 |
| 7 | `POST …/lock` | 既无 `codes` 也无 `all` | ✅ `success=false` + 「请指定要操作的锚点（codes），或传 all=true…」 |
| 8 | `POST …/StandardDocContract/analyze` | `fileCode=17a4c6df…`（**无** Markdown） | ✅ `Semantic.Status=blocked`（⛔ 不是 failed）；`Field.Status=empty`；`Scan.Status=blocked` |
| 9 | `POST …/analyze` | `fileCode=7e82dc36…`（**有** Markdown） | ✅ **`Semantic.Status=completed` + `Saved=true`** —— 真调 LLM：`qwen-flash`，`16402+834` tok，`7846ms` |
| 10 | 库内复核（第 9 条） | — | ✅ 契约行落库，`DocPurpose` 四段式完整、`TagsJson` 两标签（`RecordForm` 0.95 / `InternalAudit` 0.92）、`InfoItemsJson` 3 项、`TagsSource=ai`、`DocPurposeSource=ai`、元数据齐全 |
| 11 | `GET …/StandardDocContract/detail` | 同上 | ✅ 新增返回 `FixedDocSubtype` |
| 12 | `POST …/StandardDocContract/save` | `fixedDocSubtype:"BOGUS"` | ✅ `success=false` + 「只能是 standard_provided / enterprise_provided」 |
| 13 | `POST …/save` | `docCategory:"fixed"` + `fixedDocSubtype:"standard_provided"` | ✅ `FixedDocSubtype` **真落库**（不再恒默认值，缺口 R1 关闭）；`IsManualCorrected=true` |
| 14 | 库内复核（第 13 条） | — | ✅ 契约行与 `cert_standard_directory_file.DocCategory` **双写一致**（都是 `fixed`） |
| 15 | **B4 复测**：人工修正后再 `analyze` | — | ✅ `Semantic.Status=skipped` + `Saved=false` + 「已被人工修正，自动分析不覆盖」；**`UpdateTime` 未变**（确实没写库）；AI 结论**仍原样回带**供人工参考 |

### 10.3 ★ 本轮顺带关闭的缺口（原清单里没单列的）

| 缺口 | 说明 |
|---|---|
| **R1 实锤** | 原以为「B3 落库时一并写 `FixedDocSubtype`」就够了。实测发现**连入口都没有** —— `ContractSaveRequest` 没有该字段、`Detail` 也不返回它 ⇒ 即使实体声明了属性，**没有任何请求能写它**，会永远停在 DB 默认值。**已补 DTO + Detail + 白名单校验**（第 11~13 条） |
| **架构边界复核**（用户第 1 条口径） | 本轮全部改动落在 `CertPlatform.Admin` / `CertPlatform.Shared` 内，**⛔ 未触碰 `YZH.Core.Web`**（`git status` 复核） |

---

## 七、已裁决（2026-10-05 用户逐字答复）

| # | 问题 | 裁决 |
|:--:|---|---|
| **Q1** | 锚点锁定要不要记「谁锁的、什么时候锁的」？ | **不记**。用户原话：「**锚点是实施人员操作的，他认可了这个设置规则 ok 了，就加上锚点了**」⇒ **只是一个确认标志位**；`BaseEntity` 的 `UpdateBy` / `UpdateTime` 已记录人和时间 ⇒ **A1 只加一列 `IsLocked`** |
| **Q2** | 试填端点放现有 `DocTemplateController` 还是新建？ | **新建**。用户原话：「**试填，可以翻到新建的 controller 中，因为试填还有生成试填后的 pdf 职责不一样**」⇒ 新建 **`DocFillPreviewController`**（`api/Admin/Workflow/DocFillPreview`），职责 = ① 试填 ② 生成试填后的 PDF |

---

## 八、开工记录

### 第 1 批（A1 · A2 · A3 · B1）—— ✅ 已完成

| # | 改动 | 文件 |
|:--:|---|---|
| **A1** | 新增 `IsLocked tinyint(1) NOT NULL DEFAULT 0`（**单列**，按 Q1 裁决） | `scripts/db/20261005_doc_template_anchor_locked_V1.sql`（幂等） |
| **A2** | 实体加 `IsLocked` 属性 | `Entities/Doc/DocTemplateAnchor.cs` |
| **A2b** | EntityConfig 加 `IsLocked` 列（`Switch`，`Sxh=18`，后续列顺延） | `Assets/EntityConfigs/Doc/DocTemplateAnchor.json` |
| **A3** | 实体加 `FixedDocSubtype` 属性（缺口 G1） | `Entities/Doc/StandardDocContract.cs` |
| **B1** | 新建**单例桥接壳** + 执行器加 `TaskTypeName` 常量 + DI 注册 | `Services/Ent/Executors/EnterpriseDocNormalizationExecutorAdapter.cs`（新建）· `EnterpriseDocNormalizationExecutor.cs` · `CertPlatformAdminServiceExtensions.cs` |

#### ★★ B1 的真实难点（比预估大，已解决）

原以为「一行注册」，实际卡在**生命周期冲突**：

```
QueueManager        = AddSingleton<QueueManager>()          ← 单例
   └ 构造注入 IEnumerable<IYzhTaskExecutor>                  ⇒ 所有执行器必须是单例
        └ EnterpriseDocNormalizationExecutor 依赖 IDbOrm / IObjectStorage
                                            ↑ 两者都是 AddScoped ⇒ ⛔ 冲突
```

**这就是它此前「只有类定义、没有注册」的真正根因** —— 一注册就会抛
「Cannot consume scoped service from singleton」。

**解法**（照 `OfficeConvertTaskExecutor` 的同一精神：**单例壳 + 每次 `CreateScope`**）：

```csharp
services.AddScoped<EnterpriseDocNormalizationExecutor>();                 // 本体保持 Scoped
services.AddSingleton<EnterpriseDocNormalizationExecutorAdapter>();       // 壳是单例
services.AddSingleton<IYzhTaskExecutor>(sp => sp.GetRequiredService<EnterpriseDocNormalizationExecutorAdapter>());
```

⛔ **不能**用「把 scoped 服务缓存进字段」的偷懒写法：`QueueManager` 按
`queue_max_concurrent`（默认 4）起并发 worker，同一实例会被**并发调用** ⇒ scope 串号。

#### ★ 顺带消除的隐患

`TaskType` 原来在两处各写一遍字面量 ⇒ 抽出 `EnterpriseDocNormalizationExecutor.TaskTypeName`
常量，壳与本体**共用**（两处不一致 = 任务静默分发不到、不报错）。

---

### 第 2 批（B2 · B3 · B4）—— ✅ 已完成

| # | 改动 | 文件 |
|:--:|---|---|
| **B2** | `Analyze` 的 `Semantic` 段接线：调 `PromptWorkbenchService.AnalyzeForQueueAsync` 两跳（`doc_group` + `doc_content`） | `Controllers/Workflow/StandardDocContractController.cs` |
| **B3** | 语义结论落 `cert_standard_doc_contract`：`TagsJson` / `DocPurpose` / `InfoItemsJson` / `TagsSource` / `DocPurposeSource` / `TagsReason` / 两个置信度 / 元数据（`ModelName` / `PromptCode` / `PromptVersion` / `PromptTokens` / `CompletionTokens` / `DurationMs`）/ `AnalyzeStatus` / `AnalyzeTime` | 同上 |
| **B4** | `IsManualCorrected = true` 的行 **⛔ 不覆盖**（仍把 AI 结论原样回给前端供人工参考） | 同上 |
| **B2a** | ★ **新建 `CertPlatform.Shared/Fill/SemanticHints.cs`**：把 `ParseGroupItems` / `ExtractGroupHints` / `ExtractContentHints` 从 Auditor **下沉**到 Shared | `CertPlatform.Shared/Fill/SemanticHints.cs`（新建） |
| **B2b** | `EnterpriseOriginalAnalyzeExecutor` 改为复用 `SemanticHints.*`，删掉三份私有实现（只留 `ReadString` 转发） | `CertPlatform.Auditor/Services/Ent/EnterpriseOriginalAnalyzeExecutor.cs` |
| **B2c** | `AnalyzeStatus` 新增取值 `partial`（两跳只成一跳），实体注释同步 | `Entities/Doc/StandardDocContract.cs` |
| **B2d** | 前端 API 注释修正（原文写「B 语义分析本轮**未接**」⇒ 已过时） | `cert-share/src/api/workflow/doc-fill-rule.ts` |

#### ★★ B2 的三个关键裁定（写进代码注释，避免后来者「顺手改回去」）

**① 为什么下沉到 `Shared` 而不是复制一份**
`Admin` ⛔ **不引用** `Auditor`（引用方向是 `Auditor → Admin`）⇒ 三个提取方法**无法**被 Admin 复用。
若在 Admin 再抄一遍 = **「复制即漂移」**：33 号的输出校验口径一旦分叉，
契约表与画像表的画像字段会长出不同形状。
⚠️ 因此 `SemanticHints` **不得**依赖 `AnalyzeForQueueResult`（那个类型在 `Admin` 里，
`Shared` 引用它会形成**项目循环**）⇒ 需要的那一个字段（`ValidationMessages`）改为**显式传参** `IReadOnlyList<string>?`。

**② ⛔ 本方法**不写** `DocCategory`**
该列的**权威位置是 `cert_standard_directory_file.DocCategory`**（`detail` 也一律读文件行）。
只写契约行 ⇒ `detail` 读不到 ⇒ **「写了但不生效」的静默分叉**。
故 AI 的分类结论只作为 `SuggestedCategory` **建议**返回，由人工在 Tab3 确认后经 `save`
同批写两处（P3 建议 ≠ 事实）。

**③ 两跳独立判定（P2' 只如实推导）**
`doc_group` 挂了不该把 `doc_content` 的成果也丢掉。任一项失败只影响它自己的 `Status` 段：
`completed`（两跳都成）/ `partial`（只成一跳）/ `blocked`（Markdown 未就绪）/
`skipped`（人工已修正）/ `failed`（无产出）。

> ⚠️ **本清单 V1 初稿此处曾断言「提示词内容全空 ⇒ 必然返回 partial/failed」—— 该断言已被实测推翻。**
> 语义提示词在 **`wf_prompt_template`** 且**有内容**（9001 的 `doc_group` / `doc_content` 均已填好），
> 空的是**另一张表** `cert_doc_fill_prompt`（填写提示词）。实测 `Semantic.Status=completed` + 真 LLM 输出。
> 详见 **§十 10.1**。

#### ★ 基线

`dotnet build` **0 Error**；`dotnet test CertPlatform.Admin.Tests` **435/435** ✅

---

### 第 2.5 批（B5 · B6）—— ✅ 已完成

| # | 改动 | 文件 |
|:--:|---|---|
| **B5** | 新增 **`POST lock`** 端点：按 `codes` 清单或 `all=true` 批量锁定 / 解锁；**不阻断**（未配来源只进 `Warnings`） | `Controllers/Workflow/DocTemplateAnchorController.cs` |
| **B5a** | `keys` 端点补 `IsLocked` + `LockedCount`（供前端预检与徽标） | 同上 |
| **B6** | `scan` 落库**前**拍「锁定锚点快照」、落库**后**如实回报 `Locked.{Total,Carried,Lost,LostRefs}` | 同上 |
| **B6a** | ★ **新建 `CertPlatform.Shared/Office/AnchorScanMerge.cs`**：锁定锚点去向判定（**纯函数**，可单测） | `CertPlatform.Shared/Office/AnchorScanMerge.cs`（新建） |
| **B6b** | `PersistScanAsync` 返回值增加 `persisted`（本次**实际落库**的锚点键集合） | `DocTemplateAnchorController.cs` |
| **E2** | 新增 **17 条**单测覆盖 B6（去向判定 / 唯一键 6 段 / 边界） | `CertPlatform.Admin.Tests/Office/AnchorScanMergeTests.cs`（新建） |

#### ★★ B6 的真实结论：**「重扫 merge」不需要新写 merge 代码**（实测）

原以为 B6 要写一段「把旧配置搬到新锚点」的逻辑。**实测后前提不成立**：

```
换版（SourceSha256 变）
  └ DocTemplateController.InvalidateAnchorsAsync 把旧锚点【全部软删】
        └ 重扫 → PersistScanAsync 按 uk_tpl_anchor 命中【已软删行】
              └ 就地复活 + 只更新「扫描列」 ⇒ 配置（SourceSpec/WriteMode/Required…）【自动保留】
```

⇒ TODO 的验收标准「**加锁 → 换模板重扫 → 配置还在**」**在既有代码下已经成立**，
B6 真正缺的只有一件事：**「配置丢了」这件事目前是静默的** ——
新模板里已消失的锁定锚点会一声不响地退出，实施人员直到发布前才发现规则不见了。

故 B6 实际交付 = **如实回报**，⛔ **不做模糊匹配**：
靠「引用名相同」把配置从旧位置搬到新位置是**猜**，猜错会把 A 字段的取值来源写到 B 字段上，
而且写库会显示成功（静默错配）。让实施人员看到「已丢失」清单后自己重配，比程序猜错后再排查便宜得多。

#### ★ 两个必须钉住的实现细节（已写进测试）

1. **快照必须含已软删行**：换版刚把旧锚点软删掉，只查存活行 ⇒ 快照是**空的** ⇒
   `Carried=0 / Lost=0` ⇒ 界面显示「一切正常」，而锁定规则其实已丢 —— 典型静默失败。
2. **判定用的是「落库集合」而非「原始扫描结果」**：扫描识别出但被合规校验拒掉的项
   **并没有进库**，拿它们当「已保留」就是骗人 ⇒ `PersistScanAsync` 额外返回 `persisted`。

#### ★ B5 的一处刻意设计：`IsLocked` **不**进 `BatchUpdatableColumns`

锁定与配置是**两个正交动作**。若 `IsLocked` 在批量保存的列清单里，前端「保存锚点」时
漏传 `isLocked` 会被写成 `false` ⇒ 实施人员辛苦确认过的锁定被**静默解除**（且日志显示成功）。
⇒ **配置走 `save-batch`（⛔ 不碰 `IsLocked`）、锁定走 `lock`（⛔ 不碰配置）**。

#### ★ 基线

`dotnet build` **0 Error**；`dotnet test CertPlatform.Admin.Tests` **452/452** ✅（435 基线 + 17 新增）

---

## 十一、第 3 批（结构错误修复 + 锁定强制）—— ✅ 已完成（2026-10-05 第 26 轮）

> **用户逐字**：「**数据错位了不是关键，可以重新造数据，但是数据库结构错误必须修复，
> 提示词错误的问题，不在本页面解决，是在提示词的那个模块解决，
> 锁定的问题，就是不能再修改配置**，还是那句话，**后端全部解决彻底后，再帮我总结**」

### 11.1 ★ 库结构审计结论（全量比对，不只是本次模块）

**比对方法**：`information_schema` 逐列导出 + 与实体属性逐一对照 + 全库规范列分布统计。

| 检查项 | 结论 |
|---|---|
| **实体 ↔ DDL 列是否对齐** | ✅ `StandardDocContract` 46 列 / `DocTemplateAnchor` 37 列，**逐列一致，无缺列**（G1 的「库里有、实体无」已彻底消除） |
| **字符集 / 排序规则** | ✅ 全库 `utf8mb4_general_ci`，**无分叉**（106 张表 + 9 视图） |
| **外键** | ⚠️ `cert_standard_doc_contract` / `cert_doc_template_anchor` **无 FK**，但全库仅 20 张表建了 FK ⇒ **是项目风格（逻辑引用），不是本次错误** |
| **唯一键** | ✅ `uk_standard_file_code(StandardFileCode)` + `uk_tpl_anchor(7 列)` 均在；唯一键**不含 `IsDeleted`** 是既定范式（配合「含已删查重 + 复活」） |
| **`*By` 列宽** | ⚠️ 主流是 `varchar(50)`（84 张表），本次新表用 `varchar(64)`。**但框架 `BaseEntity` 标注就是 `StringLength(64)`** ⇒ **本次是对的**，50 才是历史遗留。⚠️ 副作用：`cert_standard_directory_file.CreateBy` 是 `varchar(50)` 而实体标注 64 ⇒ 理论上有静默截断风险（实际 Code 是 36 位 GUID，不触发） |
| **`IsValid` 类型** | ⚠️ 全库 7 种形态（`int` 60 张 / `tinyint(1)` 30 张 …）。本次新表用 `int` = **主流口径** ✅ |
| **`CreateTime` 可空性** | ⚠️ 全库 83 张可空 / 28 张非空。框架 `BaseEntity.CreateTime` 是 `DateTime`（非空）⇒ **本次非空是对的** |

⇒ **本次模块只引入 3 处「结构定义错误」，全部是「列注释与代码/设计定稿不一致」**（下面 11.2）。
其余差异都是**历史遗留**（涉及 100+ 张表），⛔ 不在本次范围 —— 单独立项，不夹带。

### 11.2 修的 3 处结构错误（`scripts/db/fix_doc_fill_comment_V1.sql`）

> 只改 `COMMENT`，**列类型 / 可空 / 默认值一律保持原样**（⛔ 不动数据、不动语义）。`MODIFY COLUMN` 天然幂等。

| # | 表.列 | 错在哪 | 改成 |
|:--:|---|---|---|
| **S1** | `cert_standard_doc_contract.FixedDocSubtype` | 注释写 `platform_generated`（= 平台自动生成，是「**谁生产**」的意思） | `standard_provided` / `enterprise_provided`（= 「**能不能被企业替换**」）。依据 **49-V3 §2.2 已裁定 D-AA1** + 代码白名单 + 全部原型（V7/V8/51）**都**用 `standard_provided`。⚠️ **默认值 `enterprise_provided` 保留不动**（49-V3 明确「DB 不动，页面让用户显式确认」） |
| **S2** | `cert_standard_doc_contract.AnalyzeStatus` | 注释缺 `partial` | 补上，并写明「两跳提示词只成功一跳，结论部分可用」 |
| **S3** | `cert_doc_template_anchor.IsLocked` | 只描述了副作用（换版重扫保留配置），没表达主语义 | 改为「**配置就此冻结，⛔ 不可再修改**」+ 点明 `save-batch`/`UpdateCore` 拒绝、`Clear` 跳过 |

> ★ **为什么「注释不一致」算结构错误**：注释是后来者的**唯一实现依据**。
> 按 `platform_generated` 去实现，会写出一个**永远匹配不上任何存量值**的分支 ——
> 编译通过、测试通过、运行期静默不生效。这与「列不存在」是同一类伤害，只是更隐蔽。

### 11.3 顺手修掉的 2 处代码缺陷（同文件，审计时发现）

| # | 位置 | 缺陷 | 修法 |
|:--:|---|---|---|
| **C1** | `DocTemplateAnchorController` 第 208 行起 | **XML 注释块未闭合** —— `SaveBatch` 的 `<summary>` 没写 `</summary>`，把下面 `Scan` 的注释整段「吞并」⇒ 两个端点的文档注释**都是错的**（`SaveBatch` 无描述、`Scan` 描述重复） | 把 `SaveBatch` 的注释摘出来归位，`Scan` 恢复独立 |
| **C2** | `SaveBatch` 插入 / 更新分支 | 用**整行**覆盖，而 `BatchUpdatableColumns` 含 `IsValid` ⇒ 前端漏传 `isValid` 时反序列化成 `0`，**把锚点静默置为无效**（此后校验与计数都看不见它，日志却显示「保存成功」） | 新增 `NormalizeInterfaceColumns()`，落库前强制「存活 + 有效」；插入/更新/`UpdateCore` 三处统一调用 |

### 11.4 ★ 锁定 = 不能再修改配置（用户裁定落地）

| 端点 | 行为 | 为什么这样 |
|---|---|---|
| **`save-batch`** | 提交项命中**已锁定**锚点且**配置列有实质变化** ⇒ **整批拒绝**，消息点名「哪条锚点 / 哪些列」 | 本页「保存」是**整批提交**（含锁定行）。若「锁定行一律拒绝」，实施人员一个字没改也会被挡 ⇒ **假闸门** |
| **`UpdateCore`** | 同上，共用**同一个** `ConfigDiff` 判定 | ⛔ 不各写一份 —— 两处清单漂移就是「这条路拦得住、那条拦不住」的静默漏拦 |
| **`Clear`**（清空） | **跳过**锁定行，返回体加 `SkippedLocked` 并如实提示 | 清空 = 把配置整批删掉，与「锁定后不可改配置」直接冲突。⛔ 不静默少删 |
| **`Lock`**（锁定动作本身） | **不设前置**：未配来源 / 孤儿也允许锁定，只进 `Warnings` | 「锁定动作不设闸」与「锁定后不可改」是**两件事**，不冲突 |

**★ `ConfigDiff` 的设计要点**（`DocTemplateAnchorController` 私有静态）：

1. **用差异比对，不是「锁定行一律拒绝」** —— 见上表理由。
2. **与落库共用同一份列清单**：反射遍历 `BatchUpdatableColumns` 并跳过 `NonConfigColumns`
   （`IsValid`/`IsDeleted`/`DeleteBy`/`DeleteTime`/`UpdateTime`/`IsOrphan`/`SourceSummary`）。
   ⛔ **不另抄一份「配置列」清单** —— 两处清单各自漂移是漏拦的经典成因。
3. 在**开事务之前**做只读预检 ⇒ 失败时不留任何痕迹，也不会「半批生效」。

**⛔ 旧口径已作废**：此前 `lock` 的注释写「它**不是权限位、不阻断任何操作**」，依据是 49-V4 的 **P2'**
（「程序不阻断，只如实推导」）。**P2' 管的是「能不能这么配」这类业务组合**（如 C1：没模板也允许标可编辑），
⛔ **不管「锁定之后还能不能再改」** —— 锁定是实施人员的**显式冻结动作**。两者是不同问题，别再用 P2' 给「锁定可绕过」背书。

### 11.5 接口复测：**19/19 全绿**（`scripts/db` 外的临时脚本，库已复原）

| # | 场景 | 结果 |
|:--:|---|---|
| 1 | 登录 | ✅ Token 548 字符 |
| 2 | `keys` 基线 | ✅ `LockedCount=0`，每项带 `IsLocked` |
| 3 | 未锁定 `save-batch` | ✅ 正常落库 |
| 4 | `lock` 锁定 | ✅ `Affected=1, LockedTotal=1` |
| 5 | ★ **锁定 + 内容不变** | ✅ **放行**（证明不是假闸门） |
| 6 | ★★ **锁定 + 改配置**（改 `ValueType` + `DefaultText`） | ✅ **拒绝**，报错原文：「以下锚点已锁定，不能修改其配置：**{{文件控制程序}}（ValueType / DefaultText）**。如需修改请先解锁。」**两个字段都被点名** |
| 7 | 库内复核（第 6 条后） | ✅ 值**未被改动**（预检没落任何痕迹） |
| 8 | 锁定后 `clear` | ✅ `Deleted=0, SkippedLocked=1`；锁定锚点仍在 |
| 9 | `lock` 解锁 → `save-batch` 改配置 | ✅ 成功，且**改动真落库** |
| 10 | 重复解锁（幂等） | ✅ `Affected=0, Unchanged=1` |
| 11 | 复原测试数据 | ✅ |

**踩到并记下的 3 个坑**（写进注释，免得下次再踩）：
① `mysql -B` 会**转义反斜杠** ⇒ 拼 JSON 必须加 `-r`（`--raw`），否则 `json.loads` 报 `Expecting ',' delimiter`；
② SQL 侧产不出 JSON **布尔字面量** —— `IF(col,'true','false')` 出来的是**字符串** `"false"`，
   C# `bool` 拒绝 ⇒ 必须在脚本侧转回真 bool；
③ 响应体恒 **PascalCase**、`keys` 是 **GET**、`success=false` 时消息在 **`err`**（`message` 是空串）。

### 11.6 ⛔ 提示词问题：**不在本页解决，转交提示词模块**

> 用户逐字：「**提示词错误的问题，不在本页面解决，是在提示词的那个模块解决**」
> ⇒ 本页代码**一行不动**，只把「是什么问题、在哪张表」记清楚交出去。

| 表 | 列 | 用途 | 现状 | 待办归属 |
|---|---|---|---|---|
| **`wf_prompt_template`** | `Template` | **语义分析**（`doc_group` / `doc_content` / `doc_essential`） | ✅ **有内容**（`doc_group_iso9001` 4,683 字 / `doc_content_iso9001` 5,161 字，`IsActive=1`） | 提示词模块。⚠️ **只有 9001 配了** ⇒ 其余标准调 `AnalyzeForQueueAsync` 会失败，本页已按 `blocked`（而非 `failed`）如实回报 |
| `cert_doc_fill_prompt` | `SystemPrompt` / `UserTemplate` | **文档填写**提示词（本模块新表） | ⛔ 2 行全 NULL | **提示词模块** |
| `cert_doc_extraction_rule` | `Prompt` | **字段提取**提示词 | ⛔ 1 行且为空 | **提示词模块** |

⇒ **本页只负责「调用与如实回报」，⛔ 不负责「把提示词填上」**。三张表名字都带「prompt」，
但归属不同模块 —— 交接时必须写清是**哪一张**（本清单 §10.1 已踩过一次「看错表」的坑）。

### 11.7 基线（第 3 批完成后）

`dotnet build src/yzh-core/YZH.Core.Web/YZH.Core.Web.csproj` = **0 Error** ✅
`dotnet test CertPlatform.Admin.Tests` = **452/452** ✅
后端 curl 复测 = **19/19** ✅
⛔ 全部改动落在 `CertPlatform.Admin` / `CertPlatform.Shared` 内，**未触碰 `YZH.Core.Web`** ✅

---

## 十二、试填链路的存储口径（2026-10-05 用户补充）

> **用户逐字**：「我们**不是直接生产 pdf**，还是**先按 office 进行填写**，然后**调用后台方法形成 pdf**，
> pdf 的目的**是为了显示**，为了**节约后台的空间**，**建议 pdf 还是存储在 minIO 中**」

### 12.1 ✅ 现状核实：与口径**完全一致**，无需改造

| 口径 | 现状证据 |
|---|---|
| **PDF 存 MinIO** | `OfficeConvertService.ConvertToPdfAsync` ⇒ `_storage.UploadAsync(core.TargetPath.TrimStart('/'), targetStream, …)`（`_storage` = `IObjectStorage` = MinIO） |
| **不占后台磁盘** | 全程 `MemoryStream`，**无任何本地持久化**（无 `Path.GetTempPath` 写文件） |
| **路径走唯一权威** | `core.TargetPath` 由 `PathBuilder.Product(src, PdfSegment, '.pdf')` 派生 ⇒ `/standard-directory/{Org}/{Std}/{Stage}/{Folder}/pdf/{原名}.pdf` |
| **库前缀白名单** | `DocumentLibraryPath.Prefixes` 含 `standard-directory`（不在白名单 ⇒ **静默全失败且 200**） |
| **DB 实测** | 669 行中 **185 行**有 `PreviewPdfPath`，值正是上述形态的 MinIO key |

⇒ **「PDF 不落磁盘」这条已经是既有事实**，B7/B8 不需要为此做任何事。

### 12.2 B7/B8 的链路（按用户口径确定）

```
① NPOI 填空白模板  →  产出 Office 字节（.docx / .xlsx）
② 复用 IFileConvertCore.ConvertToPdfAsync(fileName, sourcePath, content)   ← ⛔ 不新写转换
③ 自己 _storage.UploadAsync(core.TargetPath, content, "application/pdf")   ← 落 MinIO
④ 前端拿 PDF 路径显示
```

**★ 复用入口的选择（关键）**：

| 入口 | 能否复用 | 原因 |
|---|:--:|---|
| `OfficeConvertService` | ⛔ **不能** | 它绑定 `StandardDirectoryFile`，会写 `PreviewPdfPath` / `ConvertStatus` 列 —— 试填**不是标准目录文件**，没有这些列可写 |
| **`IFileConvertCore.ConvertToPdfAsync`** | ✅ **能** | 它是**纯内核**（接口注释逐字：「**不做转换**，**不上传产物**」），返回 `TargetPath` + `Content`，上传由调用方决定 |

⇒ 试填链 = **`IFileConvertCore`（转换）+ 自己上传**，⛔ **不要**去调 `OfficeConvertService`。

### 12.3 ⚠️ 待裁决：试填产物**不能复用 `pdf/` 段**（会覆盖正式预览 PDF）

`PathBuilder.Product` 的 `pdf/` 段**已被「源文件预览 PDF」占用**：

```
源文件：  …/4记录文件/风险管理报告.doc
正式预览：…/4记录文件/pdf/风险管理报告.doc.pdf      ← 已占用（185 行在用）
```

试填的产物来自**空白模板**（不是源文件），若也走 `Product(源路径, PdfSegment, …)` ⇒ **同 key 互相覆盖** ⛔

**三个选项（请裁决）**：

| 选项 | 试填产物落点 | 优点 | 代价 |
|---|---|---|---|
| **A（推荐）** | 新增保留段 `_preview/`：<br>`…/4记录文件/_preview/风险管理报告.docx.pdf` | ① 与 `pdf/` **不撞** ② 与 `_template/`（人工资产）**不混** ③ **每模板固定 key ⇒ 重复试填覆盖**，不堆积（省空间） ④ 删模板时同层级一起清理 | 改 `PathBuilder`（+ 保留段 + `IsProductPath`）+ **前端 `useFileTree.ts` 必须同步**（漏 ⇒ 幽灵文件夹） |
| **B** | 落 `_template/` 下：`…/_template/pdf/…` | 改动最小（`_template` 段已存在） | 把产物塞进「人工标注资产」目录，语义混乱；且 `_template` 不在 `IsProductPath` 里 ⇒ 删除/清理逻辑要特殊处理 |
| **C** | 每次试填新 key（带时间戳）+ 定期清理 | 可回溯历次试填 | **会堆积**（与「节约空间」相悖）；需要清理任务（新开销） |

> ★ **为什么推荐 A 且「固定 key 覆盖」**：试填是「**看看填出来长什么样**」，不是正式产物 ⇒
> **不需要历史版本**。固定 key 让空间占用**恒定**（每模板最多 1 个 PDF），最贴合「节约后台空间」。
> ⚠️ 副作用：并发试填同一模板会互相覆盖 —— 试填是单人操作，可接受。

> ★ **★ 另需确认：试填 PDF 的路径要不要写 DB 列？**
> **建议不写**（没有对应列，且试填是临时态）。但那样就**必须有确定性路径**（可重算）——
> 这又反过来支持「固定 key」方案 A。若将来要记「最近一次试填时间」，应新开列，⛔ 不挤占 `PreviewPdfPath`。

> ⚠️ **实施顺序约束**：选 A 则 `PathBuilder.ReservedSegments` 与前端 `useFileTree.ts` 的保留段
> **必须逐字一致**（既有铁律）；且 `_preview` 要决定是否进 `IsProductPath`
> （进 ⇒ 删文件时视为产物；不进 ⇒ 与 `_template` 同类）。**这两处不定，B7/B8 不动手。**

### 12.4 ✅ 已裁决（2026-10-05 第 28 轮，用户逐字）

> **用户逐字**：「**1\a，2\可以存储**，因为用户可以随时对一个已经完成配置的信息，进行自动填写，
> 并进行预览，但这已经不是最核心的功能了，**可以作为 todo**，未来主流程完成后，进行完善的
> 一个瑕疵，**最重要的是要开始前端的优化了**」

| # | 裁决点 | 结论 |
|:--:|---|---|
| ① | 试填产物落点 | **选 A** —— 新增保留段 **`_preview/`**：`…/{Folder}/_preview/{模板名}.docx.pdf`，**固定 key 覆盖**（省空间，试填不需历史版本） |
| ② | 试填 PDF 路径要不要落库 | **可以存储**（推翻 §12.3 末尾的「建议不写」）—— 用户明确「2、可以存储」 |
| ③ | 优先级 | **⏸ 整体降级为 TODO** —— 非最核心功能，**主流程完成后**再回来补这个「瑕疵」 |
| ④ | 当前重心 | **▶ 开始前端优化**（C 组 15 条） |

> ★ **①②③ 是「将来做 B7/B8 时的既定前提」，不是「现在要做的动作」**：
> 本轮**不动任何后端**，`PathBuilder` 的 `_preview` 段与新 DB 列都**等到 B7/B8 开工时**再加。
> 现在只把结论钉在这里，避免下次重新讨论。
>
> ⚠️ 落库方式待 B7/B8 开工时再定（新增列 vs 复用某列）——
> 但**⛔ 不得挤占 `PreviewPdfPath`**（那是「源文件预览」的位置，185 行在用）。
>
> ★ **因此 §三「顺序与依赖」里的 B7/B8/B9 保持「延后」不变**，C 组提前到最前。

### 12.5 ★ 前端优化（C 组）开工范围（第 28 轮）

| 本轮做 | 本轮不做（下一轮） |
|---|---|
| **C1** 去九宫格 → 2 Tab（锚点规则 / 全局规则） | **C5** 设置页「数据区域」（5 类数据源 + 参数 + 关联企业文档 + AI 提示词） |
| **C2** 默认落「全局规则」 | **C6** 设置页「操作区域」（操作方式自动推导 + 落空策略 + 位置只读） |
| **C4** 锚点按字段 / 表格分组 | **C10** 中栏三视图（原始文档 / 空白模板 / 填充后预览） |
| **C7** 锚点「锁定」UI（列表行 + 设置页顶部） | **C11** 全局填写规则的「ai 节点」条件块（现模型是提示词版本，非 ai 节点 ⇒ 需先定口径） |
| **C8** 锚点未配齐 ⇒ 警示条 + 状态徽标 | **C3 的自动填充/预览按钮** —— 依赖 B7/B8（已降级 TODO） |
| **C9** 上传空白模板 / 下载原始文档同一行 | **D3** 对齐 `SOURCE_KINDS` 与原型 5 类（原型是 mock，真值是 7 类，需先裁定） |
| **C12** 状态三态文案对齐 | **D1** 试填 API（依赖 B7/B8，⏸） |
| **D2** `lockAnchor()` API + `LockAnchorResult`｜**D4** `FixedDocSubtype` 字段（顺带） | |
| **C13** 帮助浮层「!」｜**C14** 注释收敛｜**C15** 清死代码 | |

> ★ **C8 的落地口径**：本轮**只做「未配齐 ⇒ 警示 + 徽标」**，⛔ **不造「自动填充 / 预览」按钮** ——
> 那两个按钮**就是 B7/B8 本身**（已裁决降级 TODO）。造一对点了没反应的按钮，比不造更糟。

---

## 十三、第 4 批（C 组前端优化）—— ✅ 已完成（2026-10-05 第 28 轮）

> **用户逐字**：「……但这已经不是最核心的功能了，**可以作为 todo**，未来主流程完成后，进行完善的
> 一个瑕疵，**最重要的是要开始前端的优化了**」

### 13.1 改动清单

| # | 改动 | 落点 |
|:--:|---|---|
| **C1** | 右栏**九宫格 → 2 Tab**（`anchor` 锚点规则 / `global` 全局规则）；删 `menuItems` 与 `.fnav` 样式 | `index.vue` |
| **C2** | `activeTab` **默认落 `'global'`**（全局规则是高频入口） | `index.vue` |
| **C3** | **类型自动判定**：`UNPARSEABLE_TYPES` **否定清单** + `effectiveDocCategory` + 顶栏按钮组 `:disabled` + tooltip 说明 | `logic.ts` · `index.vue` |
| **C4** | 锚点清单按 **字段 / 表格** 分组（`fieldViews` / `tableView`），组头带条数 + 分组空态 | `AnchorRuleTab.vue` |
| **C7** | 锚点**锁定 UI**：行内锁/解锁按钮（`lockAnchor`）、锁定后禁用「必填」开关、「配置规则」文案变「查看」 | `AnchorRuleTab.vue` |
| **C8** | **未配齐警示**：`anchorReadiness` 四条件 + `.warnbar` + 两个 `YzhStatusBadge`（已配齐 / N 个已锁定） | `logic.ts` · `AnchorRuleTab.vue` |
| **C9** | 「上传空白模板」与「下载原始文档」**同一行** —— 给共享 `DocPreview` 加 `#actions` 插槽 | `PreviewPane.vue` · `DocPreview.vue` |
| **C12** | 状态三态文案对齐（`draft` 未设置 / `setting` 正在设置 / `done` **已设置**） | `index.vue` |
| **C13** | 帮助**浮层「!」**（每 Tab 恰 4 条），三处自动收起 + `document` 点击判内 | `index.vue` |
| **C14** | 注释收敛（去掉过时的「本轮未接」类断言） | 多文件 |
| **C15** | 清死代码：删 `menuItems` / 旧 `helpItems` / `.fnav` / `.steps` / `.typegrid`(类型部分) / `typeConfirmed` prop / `analyzeStatus` | 多文件 |
| **D2** | 新增 `lockAnchor()` + `LockAnchorResult`（对接 `POST /DocTemplateAnchor/lock`） | `cert-share/src/api/workflow/doc-fill-rule.ts` |
| **D4** | `DocContractDetail` / `DocContractSavePayload` 补 `FixedDocSubtype`（`standard_provided` / `enterprise_provided`） | 同上 · `ContractTab.vue` |

### 13.2 ★★ 本轮发现的两个**真实缺陷**（都不是新引入的，是改写时才暴露）

#### ① 锚点清单只在父页加载 ⇒ 单独挂载子组件时**静默空白**

改写前 `AnchorRuleTab` 自己持有 `rows/loading/stats`，靠 `watch(templateCode)` 拉数据；
父页 `handleNodeClick` 也会拉一次。改写后清单**上移 `logic`**（默认落「全局规则」Tab 时
`AnchorRuleTab` **未挂载**，父页无法通过函数 ref 掏它的内部状态 —— 这正是 `PromptPanel`
注释里记过的同一个坑：`v-if` 互斥 ⇒ ref 恒为 `null`）。

上移后暴露的事实：**「谁先拉」没有契约**。父页请求失败、或将来有人单独挂载它 ⇒ 页面
**静默空白**（不抛异常、不打日志）。
⇒ 修法：`AnchorRuleTab` 加 `onMounted(() => { if (!logic.anchorRows?.length) loadAll() })` ——
**空才拉**，有数据时不重复请求（避免每次切 Tab 都打接口）。

#### ② 完成度**分母写死** ⇒ 引入 C3 后必然算错

原实现 `const total = isFixedDoc ? 4 : 3`。引入「图片 / PDF **不需要**类型确认」后，
分母必须**跟着类型判定走**，写死就会得出「3/4 完成」这种自相矛盾的读数。
⇒ 修法：抽 `requiredItems(ex)`（与 `missingItems` **同源**），`completion` 只读它的长度。

> ⚠️ **同源里藏着一个易错点**：「锚点扫描」与「锚点来源」是**同一槽位的两种缺法**
> （`missingItems` 里是 `if / else if` ⇒ 最多报一个）⇒ 分母**只加 1 项**，⛔ 不能加成 2 项。
> 已用单测钉住：`{ done: 2, total: 3, miss: ['锚点来源'] }`。

### 13.3 ★ 三个值得记住的设计判断

1. **判可解析性用「否定清单」而非「肯定清单」** —— 资料清单里实测存在 `txt`（1 份），
   以及未来可能出现的未知扩展名。肯定清单会把它们一并判成「不可解析」⇒ 把一份本来
   能配规则的文档**锁死成固定文档**，且用户**无从解锁**。
2. **C8 只做警示、不造按钮** —— 「自动填充 / 预览」按钮**就是 B7/B8 本身**（已裁决降级 TODO）。
   造一对点了没反应的按钮，比不造更糟。
3. **`DocPreview` 用插槽而非 prop 承接「上传模板」** —— 该动作是 `doc-fill-rule` 页独有，
   塞 prop 会把领域概念带进共享渲染器；插槽可选，未提供时渲染结果与改动前**逐字一致**。

### 13.4 ★ 顺带修掉的**预先存在**守卫违规（R-A）

`EnterpriseDocNormalizationExecutor.cs` 的 `await _db.UpdateAsync(instance);`
（**单参重载 = 全列写回**，陷阱 ㉑）—— 本次只改了 4 列，却会把整行其余列按内存快照覆盖回去。
⇒ 改为**点名列**的 4 参数重载。守卫 `R-A` 由此转绿。

### 13.5 ★ 基线

| 项 | 结果 |
|---|---|
| 前端架构守卫 | ✓ **24 条规则 / 1133 个文件** |
| `vitest` | **6 files / 163 tests** ✅ |
| `vue-tsc` + `vite build` | **0 error** ✅（首轮报 1 处 `analyzeStatus` 未使用 ⇒ 已按 C15 删除） |
| 后端 | ⛔ **本轮未动**（`dotnet` 基线保持 `435 → 452` 不变） |

> ★ **新增单测**：`logic.test.ts` 加两个 `describe`（C3 类型判定 4 例 / C8 配齐 + C11 ai 节点 7 例）；
> `PreviewPane.test.ts` 加一个 `describe`（**C9 上传按钮进 `#actions` 插槽** 4 例 ——
> 桩必须真渲染插槽，否则插槽内容永远测不到，C9 等于没测）；
> `AnchorRuleTab.test.ts` 由「手搓 mock」改为**真实 `DocFillRuleLogic` + `vi.spyOn` 两个出口** ——
> 手搓 mock 等于**在测试里重写一遍生产逻辑**，它永远不会发现生产逻辑写错。

---

## 十四、第 5 批（代码评审处置）—— ✅ 已完成

> **来源**：用户「代码评审结论 — 标准文档填写规则页」（走查 10 项全 ✅ + 6 条问题清单）。
> **用户指令**：「合并前建议修 **#1**（帮助文案去掉 `**`）、**#2**（PromptPanel 复用 `logic.anchorRows` 或去重请求）；
> **#3、#5 需要你确认方向**（补存储对象 / 更新菜单快照文档）。」

### 14.1 逐条处置

| # | 级别 | 结论 | 落点 |
|---|---|---|---|
| **#1** | 中 | ✅ **治本**：抽可测纯函数（坑已踩两次，「注释提醒」无效） | 新建 `components/richText.ts` + `richText.test.ts`（9 例）；`index.vue` 模板改 `parseBold(h.v)` |
| **#2** | 中 | ✅ **根因不是「多发一次请求」，是「两份数据」** ⇒ 只去重不够 | `logic.ensureAnchors()`（幂等）+ `anchorLoadedFor` 标记；`PromptPanel` 改读 `logic.anchorRows`；`AnchorRuleTab` 兜底判据改「本模板没加载过」 |
| **#3** | 中 | ✅ **根因与评审推断完全相反**（见 14.2） | `scripts/backend/run-backend.sh` 加 `NO_PROXY` |
| **#4** | 低 | ✅ 已补 `ContractTab.vue`（5 处）+ `PromptPanel.vue`（3 处） | `el-select` ⛔ 不能用 `for/id`（`id` 落内部隐藏 input）⇒ 统一 `aria-label` |
| **#5** | 低 | ✅ **过期点比评审说的更多（3 处）** | `AGENTS.md` 菜单速览 + `26 号 §五` 校正块 |
| **#6** | 备忘 | ✅ **原本就有注释**，仅补强「不违规 + 防误判」 | `StandardDocContractController.cs:24-25` |

### 14.2 ★★★ #3 的真正根因：**代理毒死 MinIO**（不是对象缺失）

**评审推断**：MinIO 对象缺失（DB 有行、存储无对象）⇒ 建议「补存储对象」。

**实测（新增对账脚本，列 1012 个 MinIO 对象 × 对 5 个 DB 路径列）**：
`StoragePath` 185/185 · `PreviewPdfPath` 184/184 · `MarkdownPath` 179/179 ·
`EditableStoragePath` 167/167 · 空白模板 2/2 ⇒ **对象 100% 存在，零缺失。**

> ⚠️ **脚本第一版会得出「100% 缺失」的假结论**：DB 存 `/standard-directory/…`（**带前导斜杠**），
> MinIO key **不带**。不归一化就会复现评审的误判形态。

**日志实证的根因**：
```
System.Net.Http.HttpRequestException: Connection refused (127.0.0.1:62083)
   at Minio.MinioClient.BucketExistsAsync → StatObjectAsync
   at YZH.Core.DataBase.Infra.MinioObjectStorage.DownloadAsync
```
.NET 在 Unix 上 `HttpClient.DefaultProxy` **读 `HTTP_PROXY`/`HTTPS_PROXY`** ⇒ MinIO SDK
把**本机 9000** 的请求送去代理；后端继承的是**更早会话的代理端口**（每会话都变，那个端口早没了）。

**⇒ 症状与病因完全错位**：对外报「文件不存在 / 源文件读取失败（对象不存在或存储不可用）」，
**看起来是数据问题，实际是网络层**。影响面远大于预览/下载 —— **所有走 MinIO 的功能同时全坏**
（Office 转换产物读写、Markdown 提取、语义分析读 `MarkdownPath`）。

**修复（★ 三层，与启动路径无关；均只「追加/绕过」，⛔ 绝不 unset —— LLM 调用要代理出网）**：

| 层 | 位置 | 作用 |
|---|---|---|
| **① 根治** | `YZH.Core.Web/Extensions/StorageServiceExtensions.cs` | MinioClient `.WithHttpClient(new HttpClient(new SocketsHttpHandler { UseProxy = false }) { Timeout = 10min })` ⇒ **不依赖任何环境变量**（IDE / 裸 `dotnet run` / 服务 / 容器一律生效）。Minio 7.0.0 的 `MinioClientExtensions.WithHttpClient` 可链在 `.WithSSL(false)` 后、`.Build()` 前。⛔ 超时不用 `InfiniteTimeSpan`（异常时会永久挂住） |
| **② 兜底** | `Program.cs` **第一条语句** | 把 `127.0.0.1,localhost,::1,0.0.0.0` **合并进**（⛔ 不覆盖）`NO_PROXY`/`no_proxy`。★ 必须在**任何 `HttpClient` 创建之前** —— `HttpClient.DefaultProxy` 惰性初始化、**首次访问即定稿** |
| **③ 进程级** | `scripts/backend/run-backend.sh` | `export NO_PROXY="127.0.0.1,localhost,::1,0.0.0.0${NO_PROXY:+,$NO_PROXY}"` |

> ★ **MinIO 是内网对象存储，走代理没有任何意义** ⇒ `UseProxy = false` 是**正确语义**，不是绕过。
> ✅ 原「只覆盖 `run-backend.sh`、裸 `dotnet run` 会复发」的缺口**已消除**。

**验收（★ 必须按「原始故障场景」验，脚本启动只能证明第 ③ 层）**：
手工启动 + `HTTP_PROXY` 指向**当初那个已死的 62083** + `NO_PROXY` 为空 ⇒ 跑接口矩阵：

| # | 端点 | 覆盖的链路 | 结果 |
|---|---|---|---|
| ① | `DocExtractionRule/preview-by-path` | 空白模板读 + 实时转 PDF + 上传 | 200 · 107,088 B · **真 PDF v1.7 / 2 页** |
| ② | `StandardDirectory/download` | 源文件读 | 200 · 53 B |
| ③ | `DocExtractionRule/file-preview` | 读已缓存产物 | 200 · 15,103 B · PDF |
| ④ | `DocExtractionRule/file-markdown` | ★ **语义分析的上游**（读 Markdown） | `success:true` · 679 B **真 Markdown** |
| ⑤ | `preview-by-path` + **`useCache=false`** | ★ 强制重新转换并**上传**（**写链路**） | 200 · 107,088 B · **真 PDF** |

日志 `Connection refused` = **0**、`源文件读取失败` = **0**；`dotnet build CertPlatform.sln` **0 Error**。

### 14.3 ★ 本轮基线

| 闸 | 结果 |
|---|---|
| 前端架构守卫 | ✓ 24 条规则 / 1133 文件 |
| `vitest`（`doc-fill-rule` 目录） | **7 files / 172 tests** ✅（163 → +9 `richText`） |
| `vue-tsc` + `vite build` | **0 error** ✅（`✓ built in 10.40s`） |
| 后端编译 | **0 Error**（340 warnings 均既有） |
| MinIO 对账 | 5 个路径列**零缺失**；1012 对象中 295 个孤儿（正常） |

### 14.4 ★ 沉淀

- **新增技能** `yzh-diagnose-file-failure`（含对账脚本）—— 「文件打不开」类故障的判据表 + 三层定位顺序。
- **纠正一条旧结论**：`el-input-number` / `el-switch` **支持** `aria-label`
  （此前记的「无 `useAriaProps`」是错的）。
- **口径**：**报错文本 ≠ 根因层**。「文件不存在」先看**日志** → 再看**存储** → 最后才怀疑**数据**。

---

## 十五、第 6 批（第二轮 4 项改造）—— ✅ 已完成（2026-10-07）

用户逐字口径（4 项）：

| # | 口径 | 落点 |
|---|---|---|
| ① | 全局规则只留「**文本框 + 自动生成（后端 LLM）+ 保存**」；未挂接时点自动生成**自动新建+挂接**；版本表 / 挂接对话框 / 解绑 / 编辑抽屉**全删**；仅锚点含 `kind='ai'` 节点才显示 | `PromptPanel.vue` 整体重写 + `index.vue` 卡3 `v-if="logic.hasAiNode"` |
| ② | 「全局规则」Tab 改**三块分组卡片**：分组 / 文档作用 / 全局填写规则 | `ContractTab.vue` 拆卡1（分组）+ 卡2（文档作用）；卡3 = `PromptPanel`；发布校验为卡4 |
| ③ | 新增第三个「**预览**」Tab：闸门 → 自动填值可改 → 带 `Overrides` 出 PDF（前后端一起改） | 新建 `PreviewTab.vue` + `index.vue` `RightTab += 'preview'`；后端 `PreviewAnchorValue.Value` + `runDocFillPreview(overrides?)` |
| ④ | 错误改 **Tab 角标 + 点击弹层明细**，⛔ 不让提示占太多 Tab 空间 | 三颗徽标外包 `el-popover`（`anchorIssues` / 挂接态+`promptInvalidRefs` / 闸门）；删 `AnchorRuleTab` C8 顶部警示条 |

### 15.1 后端配套（本轮仅 3 处小改）

| 位置 | 改动 | 为什么 |
|---|---|---|
| `NormalizeModels.cs` `PreviewAnchorValue` | 加 `Value`（全两处构造点：override 分支 `ov.Value`、工厂分支 `pv.Value.ToDisplayText()`） | `Display` 被截到 120 字符，编辑框初值 / 回传覆盖必须用**未截断**的 `Value`，否则长值被静默截断写进产物 |
| `DocFillPromptController.cs` `versions` select | 补 `UserTemplate / SystemPrompt / OutputSchema / Remark` | 「保存 = 整行 update」的行数据源 —— 缺列会把 `PromptName/Sort` 等写成 CLR 默认值（`updateFields` 是全部 `BcFlag` 列） |
| 同上 `versions` 可见范围 | `p.OrgCode == org` → `p.OrgCode == '' \|\| p.OrgCode == org` | 与 `resolve` 的候选范围**逐字对齐**，否则「resolve 选中的行在 versions 查不到 ⇒ 无从保存」 |

（`POST DocFillPrompt/generate` + `runDocFillPreview` 带 `Overrides` 属本轮前置，已在前一次提交完成；`dotnet build CertPlatform.Auditor` 0 Error。）

### 15.2 关键设计判断

- **生成 ≠ 保存**：`generate` 只把正文填进文本框；已挂接时必须点「保存」才落库（改坏了还能重来）。未挂接时按用户裁决①就地 `addDocFillPrompt` + `setDocTemplatePrompt`（新建+挂接两步跨控制器非事务，第二步失败如实抛出）。
- **`localCode` 防双击**：新建挂接 → 父页 `reloadTree` 刷回 `promptCode` 之间有窗口，`hasPrompt` 用 `promptCode || localCode` 拼接，避免连点建出两个提示词。
- **非法引用只上报不展示**：`PromptPanel` emit `invalidRefs` → `index.vue` 存 `promptInvalidRefs` 进角标弹层；换文件清零。组件内 ⛔ 不留错误大块。
- **预览闸门三处同源**：锚点页按钮 / 预览 Tab 徽标 / `PreviewTab` 内部门槛全部读 `logic.anchorReadiness.ready`（含 `previewBlockReason` 由父页传入，⛔ 不复述判据）。
- **弹层必须 teleport**：`el-popover` 默认进 body ⇒ 组件 `scoped` 样式够不着，走 `popper-class` + 全局样式块（仍全用令牌，R18 可扫）；顺带避免弹层撑变形 Tab 行。
- ⚠️ **S04 注释踩坑**：注释里写字面量 `el-button` 标签会被 `guards.mjs` 的正则当成真标签计违规 —— 注释措辞要避开标签原文。

### 15.3 ★ 本轮基线

| 闸 | 结果 |
|---|---|
| 前端架构守卫 | ✓ 24 条规则 / 1189 文件 |
| `vitest`（`cert-admin` 全量） | **13 files / 273 tests** ✅ |
| `vue-tsc --noEmit` | **0 error** ✅ |
| `npm run build` | ✓ built（含 `vue-tsc`） |
| 后端 `dotnet build CertPlatform.Auditor` | **0 Error**（326 warnings 均既有） |
