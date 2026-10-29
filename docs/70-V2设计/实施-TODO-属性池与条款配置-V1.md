# 实施 TODO：属性池 + 条款属性配置（含前端 + AI 预生成）

> ⚠️ **2026-10-28 口径变更，先读再动工**（以 [25 号 V3.0](./25-总体设计-V2.md) 为准）：
> 1. 属性池**加两列**：`AttrLayer`（通用 / 条款族专属 / 标准专属）+ `AttrSide`（企业端 / 执行端 / 共有）→ 表要 `ALTER`，前端要能按层、按端筛选。
> 2. 通用属性由 **8 条扩为 12 条**：补 `RequirementItems` / `EvidenceKinds` / `CheckModes` / `Process` / `VerificationLevel` / `EvidenceChain` → 种子数据要重灌。
> 3. `Artifacts`（要求的企业材料）由 `prompt` 改回 **`json` 结构化清单**；`EvidenceChain` 保留 `prompt` 并降为**专属层**属性。
> 4. `Process` 降为**专属层**属性（不进通用基础）。
> 5. 本文第 2 节已完成的「8 条种子 / 不分层」验收记录**只作历史**，需按新口径复验。
> 6. 新增两项未开工：**条款判断提示词**（`clause_judge`）、**标准侧文档要求**表；见 28 号 V4。
>
> **版本**：V1 ｜ **创建**：2026-10-28 ｜ **状态**：执行中（口径待补齐）
> **权威设计**：[24-条款标准化-属性池与条款配置-V2.md](./24-条款标准化-属性池与条款配置-V2.md)（字段级）· [25-总体设计-V2.md](./25-总体设计-V2.md)（层与边界）
> **用法**：**完成一项就把 `[ ]` 改成 `[x]`**，并在行尾补 `→ 证据`（命令输出/文件路径/实测结果）。断点续做时从第一个 `[ ]` 往下读。

---

## 0. 开工前基线（⛔ 每次接手先跑这三条）

- [x] `git status` 干净（除本轮预期改动）；当前分支 `v2/clause-centric`
- [ ] `dotnet build CertPlatform.sln` **0 Error**（⚠️ 仓库根）
- [ ] `docker exec yzh-mysql ... -e "SELECT COUNT(*) FROM cert_iso_clause"` = **273**；`cert_clause_attr_def` 存在性已确认

**已知基线（2026-10-28 实测）**

| 项 | 值 |
|---|---|
| 条款总数 / 4 标准 | 273（iso13485 88 / iso9001 78 / iso14001 48 / iso45001 59） |
| 条款 `Description` 空值 | **0**（全部有正文，可直接喂 LLM） |
| `cert_clause_attr_def` / `cert_clause_attr` | **均不存在**（业务库 0 张新表） |
| 后端 | `localhost:9992`；前端 vite `localhost:9990`（代理 `/api`） |
| 菜单父节点 | `MENU_00301` 基础资料 / `MENU_00302` 规则定义 / `MENU_00303` 系统管理 |

---

## 1. 归档过期文档（P0）

- [x] 1.1 建归档目录 `docs/90-归档/旧版本/归档-2026-10-28-V2条款属性设计演进/`
- [x] 1.2 把 **23 号 §二（208 条叶子语法实测）** 迁进 24 号（作为 §十二 附录）—— 否则归档后 25 号 §十四 的指针会断
- [x] 1.3 移入归档：`00` + `01`–`19` + `21` + `23`（共 22 个文件）→ 已 git mv，剩 20/22/24/25/26/README/TODO/依据
- [x] 1.4 写 `归档记录-V1.md`（谁取代谁 / 存活内容迁到哪 / 不再引用声明）
- [x] 1.5 更新 `docs/70-V2设计/README.md` 索引（去掉已归档条目，改指归档目录）
- [x] 1.6 修 25 号 §十四「语法实测以 23 号为准」→ 改指 24 号附录
- [x] 1.7 `git commit` 归档（单独一笔，便于回溯）→ `42c1e2f3`

> **保留不归档**：`20`（评估报告，P0/P1 未裁）· `22`（表清单，D-K1…D-K6 未裁）· `24`/`25`/`26`/`README`

---

## 2. 建表 + 种子数据（P1）

- [x] 2.1 写 `scripts/db/20261028_clause_attr_V1.sql`
  - `cert_clause_attr_def`（L0 属性池）+ `cert_clause_attr`（L1 条款配置），字段严格照 24 号 §九
  - 8 条种子：`ClauseIntent`/`Obligation`/`Applicability`/`ApplicabilityCondition`/`Process`/`Artifacts`/`EvidenceChain`/`Severity`
  - `FilterCode` 生成列（`Obligation`/`Severity`/`Applicability`）
- [x] 2.2 执行 SQL → 验 `SELECT COUNT(*) FROM cert_clause_attr_def` = **8** ✅ 实测 8 条
- [x] 2.3 字典：`ArtifactType`（材料类型受控码，`ArtifactHit.Kind` 落它）✅ 10 项
- [x] 2.4 字典：`ProcessDomain`（受控过程词表：生产/销售/设计/采购/检验/计量/仓储/售后/人力/管理）✅ 10 项
- [x] 2.5 验唯一键 `uk_attr(StandardCode,ClauseCode,AttrCode)` 与生成列 `idx_filter` 可用 ✅ `Obligation=shall`→`FilterCode=shall`；`Process`→NULL

---

## 3. 后端实体 / 配置 / 控制器（P2）

- [x] 3.1 实体 `CertPlatform.Shared/Entities/Cert/ClauseAttrDef.cs`（`[SugarTable("cert_clause_attr_def")]`）
- [x] 3.2 实体 `CertPlatform.Shared/Entities/Cert/ClauseAttr.cs`
- [x] 3.3 EntityConfig `Admin/Assets/EntityConfigs/Cert/ClauseAttrDef.json`（⚠️ 文件名 = 类名；`Yxk` 可空列必须 `true`）
- [x] 3.4 EntityConfig `Admin/Assets/EntityConfigs/Cert/ClauseAttr.json`
- [x] 3.5 控制器 `Admin/Controllers/Cert/ClauseAttrDefController.cs`（`: YzhControllerBase<ClauseAttrDef>`）
- [x] 3.6 控制器 `Admin/Controllers/Cert/ClauseAttrController.cs`（含左树端点 `/tree/root`：标准 → 条款）
- [x] 3.7 登记 `Program.cs` 的 `BusinessEntityConfigPaths` → **不需要**：实测 `CertPlatform.Admin/Assets/**` 通配已把 JSON 复制到宿主 `YZH.Core.Web/bin/.../Assets/EntityConfigs/Cert/`，`ContentRoot/Assets/EntityConfigs` 即核心目录 ⇒ 无需注册
- [x] 3.8 `dotnet build CertPlatform.sln` **0 Error** + 重启后端 ✅ 505 警告 / **0 错误**，3s 就绪
- [x] 3.9 `curl` 冒烟 ✅ `/config` 7 列 · `/options` 8 条 · `/tree/root` 4 标准 208 叶子 · `/filter` TotalCount=1 · `/tree/counts` 正确
- [x] 3.10 ★ 反向用例 ✅ 5 类全拒：重复属性 / 属性不在池 / 标准与条款不匹配 / ValueType=int / dict 无字典；删被引用属性被拒

**⚠️ 已知框架缺陷（本轮不修，记在这里）**：`YzhControllerBase.ValidateEntity` 的必填文案取 `col.DesName`，
但实测输出的是 **FieldName**（"ValueSource 不能为空" 而不是 "值来源 不能为空"），而 `/config` 返回的 `DesName` 是对的
⇒ `Config` 与 `/config` 用的不是同一份列定义。影响：所有模块的必填提示都会漏英文列名。
**业务后果可控**：前端表单按 `Yxk` 已有必填规则，用户正常操作看不到这句；后端业务校验（重复/不匹配/枚举）文案是对的。

---

## 4. 前端页面（P3）

### 4.1 属性池页（`/cert/clause-attr-def`）

- [x] 4.1.1 `cert-admin/src/pages/cert/clause-attr-def/index.vue`（单表 CRUD，`SingleTableCore` + `YzhTable` + `YzhFormDialog`）
- [x] 4.1.2 `logic.ts`：`controllerName = 'Admin/Cert/ClauseAttrDef'`（★ 原文漏了 `Admin/` 段，三层同构路由必须带）
- [x] 4.1.3 `ValueType` 用受控下拉（`text/dict/bool/json/prompt`，由 JSON 的 `DictCode: clause_attr_value_type` 驱动），`DictCode` 仅在 `dict`/`json` 时必填
  → 证据：实测切到「字典」后「字典编码」`is-required=true`；切回「文本」变 false
- [x] 4.1.4 `prompt` 类型的提示文案：弹窗顶部按值类型动态显示一句话说明（含「值即提示词，内部须输出 JSON」三条铁律）
  → ★ 属性池本身**不存**提示词正文（正文是每条款各一份，住 L1）⇒ 多行编辑落在 4.2.5 的抽屉

> ★★ **实现坑（必记）**：`formFields` 里**不能**读 `this.formData.X` 做联动。
> `YzhFormDialog` 的 `v-model` 回写是 `logic.formData = $event`，而逻辑实例**不是** `reactive` 的
> ⇒ 这一步把 `SingleTableCore.formData`（reactive 对象）整个替换成**普通对象**，
> 之后所有 `this.formData.X` 读取都不再建立响应依赖 ⇒ 依赖它的 getter 永远不重算。
> 解法：用 `fieldProps.onChange` 把值镜像进一个独立 `ref`，getter 只读 ref。

### 4.2 条款属性配置页（`/cert/clause-attr`）

> ★ **2026-10-29 形态改版（右表 → 条款工作台）**：下面 4.2.1–4.2.6 记录的是旧「左树 + 右表」实现，
> 已由新版取代（编号保留以留痕）。改版理由：稀疏行表格里「没配」与「配了但值为空」长得一样，
> 且每配一条都要「新增 → 下拉选属性 → 填值」重复一遍，条款一多不可用。

- [x] 4.2.1 ~~右表~~ → **条款工作台**：`YzhTreeTableLayout` 左树 + 右面板「条款详情 + 属性清单」
- [x] 4.2.2 `logic.ts` 继承 `TreeTableCore`；覆写 `loadConfig`（后端无 `/treepconfig`，改走 `/config` + 手工拼 `TreeConfig`，`RelateField=ClauseCode`）/ `loadTreeRoot`（`/tree/root` 嵌套递归映射）/ `onNodeClick`（只刷工作台，不查本实体分页）/ `afterTreeLoaded`（载属性池选项）
  → ⚠️ `dtoToNode` **不递归**（只服务懒加载），多层嵌套树必须自己写 `dtoToNodeDeep`
- [x] 4.2.3 **左树 = 完整层次**（标准 → 章 → 节 → 条款，最多 4 层）；章 / 节点击只显示正文与「该节点不能配置属性」引导，⛔ 不写库
- [x] 4.2.4 **属性清单 = 属性池全集**：勾选即配置、直接填值、一次保存（新增 / 更新 / 取消勾选→删除 三态一次提交）
- [x] 4.2.5 **提示词就是值**：`prompt` 类型（`Artifacts` / `EvidenceChain`）在属性行内 `textarea` 直接编辑，
  ⛔ 已删除独立「编辑提示词」按钮与抽屉；顶部「通用提示词维护」入口一并移除
- [x] 4.2.6 值控件按 `ValueType` 分派：`text` / `prompt` → textarea；`dict` → 单选；`json`（有字典）→ 多选写回 JSON 数组；
  `json`（无字典）→ JSON 文本；`bool` → 开关。`ValueSource` 固定值 / 参考值两档在行内可选
- [x] 4.2.7 路由注册（`router/index.ts` 两条，path 与 `Sys_Menu.Url` 逐字一致）
- [x] 4.2.8 契约补充（`@share/api/cert/clause-attr`）：`ClauseAttrRow` / `DictItemDto` + `listClauseAttrs`（`ShowDisabled:true`）/ `createClauseAttr` / `updateClauseAttr` / `deleteClauseAttrs` / `getDictItems`
  → ⚠️ `/update` 必须**整行回传**（三键 `StandardCode`/`ClauseCode`/`AttrCode` 不可缺，否则后端判「三键不可修改」）
- [x] 4.2.9 实测（2026-10-29）：4 层路径 `[iso9001] › 7 支持 › 7.1 资源 › 7.1.5 监视和测量资源` 正常；
  改值 → 「已保存：更新 1」→ 落库；取消勾选 → S06 二次确认弹窗文案正确；AI 预生成 / AI 重生成此条款 弹窗正常；控制台 0 错误

### 4.3 菜单

- [x] 4.3.1 SQL `scripts/db/20261028_clause_attr_menu_V1.sql` → `MENU_00220` 条款属性池（`MENU_00302` 下，OrderNo 400，`/cert/clause-attr-def`）
- [x] 4.3.2 同脚本 → `MENU_00221` 条款属性配置（OrderNo 410，`/cert/clause-attr`）✅ 已执行（幂等，`ON DUPLICATE KEY UPDATE`）
- [x] 4.3.3 `bash scripts/db/verify/sync_menu_urls.sh` ✅ 47 条菜单，两条新 URL 已入快照

### 4.4 前端收口

- [x] 4.4.1 `node scripts/guards.mjs` **0 违规** ✅ `✓ 前端架构守卫通过（37 条规则 / 1505 个文件）`
  → ⚠️ 踩过一次 `S08`（禁裸 `<el-tag>`）⇒ 状态徽标统一用 `YzhStatusBadge`
- [x] 4.4.2 `vue-tsc` **0 错** + `vite build` **2712 modules transformed 成功**
  → ⚠️ `npm run build` 整体会在最后 `emptyDir(dist)` 被**沙箱 safe-delete 守卫**拦下（87 文件 > 阈值 50，与代码无关）；
     绕法：`npx vite build --outDir dist-verify --emptyOutDir false`
- [x] 4.4.3 浏览器实测（Playwright + 本机 Chrome）：
  - 属性池：8 行渲染、7 列正确、新增弹窗打开、值类型联动必填生效
  - 条款配置：左树 4 标准 / 208 叶子、点条款右表联动、空态文案区分「没选条款」与「选了没配」
  - 新增 2 条（`ClauseIntent` text / `Artifacts` prompt）→ 提示「保存成功」，`Sort` 自动 10/20
  - 抽屉改提示词 → 「提示词已保存」，`AttrValue` 落库为改后正文
  - 控制台错误 **0**
  - 测试数据已清理（`cert_clause_attr` 归零）

---

## 5. AI 预生成条款属性（P4）

- [x] 5.1 新增提示词类型 `clause_attr`（`wf_prompt_template`，`PromptType='clause_attr'`，平台级 1 条）
  - 证据：`scripts/db/20261028_clause_attr_prompt_V1.sql` → 实测 `Code=5fb6ece2d28911f1a0746e326917f828`、`Version=1`、`IsValid=1`、`Template` 2852 字
- [x] 5.2 提示词正文：输入「条款号 + 标题 + 正文」，输出 8 属性的 JSON（严格 schema，见下）
  - 证据：正文含「各字段判据 1–7」+ `Artifacts`/`EvidenceChain` 两段独立要求（10 个 `Kind` 码 / 4 个 `Kind` 码逐条列出）
- [x] 5.3 `Admin/Services/Cert/ClauseAttrGenerateService.cs`
  - 分批（`MaxBatchSize=50`，入参 `MaxCount` 再夹一次）→ `LlmInvokeService.CompleteAsync(ForceJson=true)`
  - `ValueSource`：只有 `Artifacts`/`EvidenceChain` = `reference`，其余 6 条 `fixed`（`ReferenceAttrs` 唯一出处）
  - ⛔ 直插显式 `Code = Guid.NewGuid().ToString("N")` + **接返回值判 `Success`**（守卫 `B-R5`）
  - ★ **2026-10-28 修 ①**：覆盖已有走**列级** `UpdateAsync(existing, nameof(AttrValue), nameof(UpdateBy), nameof(UpdateTime))`
    （原单参全列写回，守卫 `R-A` 报违规；实测覆盖后 `ValueSource`/`IsValid` 未被冲掉）
  - ★ **2026-10-28 修 ②（关键）**：`leaves.Take(limit)` 原来**恒取前 N 条** ⇒ 点 5 次「开始生成」都跑同一批
    （40 条全跳过），永远推不到第 51 条 —— 分批等于没有。现 `Overwrite=false` 时先摘掉「属性已配齐」的条款；
    全齐时返回 **success** 信封 + 人话提示（⛔ 不返回 `Fail`，否则前端弹红字报错）
- [x] 5.4 端点 `POST /api/Admin/Cert/ClauseAttr/generate`（入参 `StandardCode` + `ClauseCodes[]` + `Overwrite` + `MaxCount`）
  - 证据：`200 / success:true / Written=8 / PromptCode+PromptVersion+Model 均回填`；`MaxCount=1&Overwrite=true` 复跑同条款也通过
- [x] 5.5 前端：条款属性配置页加「AI 预生成」按钮 + 分批进度条 + 逐条结果列表
  - `clause-attr.ts`：`generateClauseAttrs()` + 3 个 interface
  - `logic.ts`：`genVisible/genRunning/genStandardCode/genCount/genOverwrite/genResult/genItems/genElapsed/genColumns`
  - `index.vue`：`YzhDialog`（`:show-close="!genRunning"`，跑动中禁关）+ 结果表走 **`YzhTable` 静态 `:data` 通道**
    （守卫 `R6` 禁页面内联 `<el-table>`；`genItems` 用独立 ref 整体替换，⛔ 不写 `?.Items ?? []` —— 每次渲染新数组会反复重载）
  - 「进度」= **已跑 N 秒**计时器（服务端同步串行，拿不到逐条进度 ⇒ 至少证明还活着）
  - 证据：`vue-tsc` 0 错 / `guards.mjs` 37 条规则 0 违规 / Playwright 实测弹窗→跑 5 条→结果表 5 行（截图已留）
- [x] 5.6 先跑 **iso9001 1 个条款** 单点验证输出结构对不对（⛔ 别直接全量）
  - 证据：条款 `1 范围` → 8 个属性全落库；`ApplicabilityCondition` 空串（通用条款，符合判据 3）
- [x] 5.7 抽 3 条人工评审（`Obligation`/`Process`/`Artifacts` 是否靠谱），改提示词再跑
  - 证据：抽 6 条（iso9001 `1/2/3/4.1/4.2/4.3`）评审 → **§5.11**；用户裁定「先改提示词再跑」+「前置条款生成阶段跳过」
  - 已改：提示词升 **v2**（`20261028_clause_attr_prompt_V2.sql`）+ 服务加 `IsPreambleClause()`；复验 `iso14001` 5 条 + `7.1.5.2`/`8.5.1` 2 条
- [ ] 5.8 全量跑 273 条 → 统计每个属性的填充率
  - 🟡 **用户裁定「暂不跑，先看评审结果」** ⇒ 待确认后再跑；当前已生成 7 条（iso9001 5 条 + iso14001 5 条 − 重叠）
- [ ] 5.9 落库校验：`SELECT AttrCode, COUNT(*) FROM cert_clause_attr GROUP BY AttrCode`
  - 🟡 现有数据已可跑通该 SQL（8 属性 × 已生成条款数，各 8 行齐平）


### 5.11 评审发现（2026-10-28，抽 6 条）→ 已按裁决修完（v2）

| # | 现象 | 判据 | 处置 |
|---|---|---|---|
| 1 | `Obligation` 6 条**全是 `shall`**，含「范围 / 规范性引用文件 / 术语和定义」这类**没有义务动词**的前置条款 | 提示词判据 1 只写了「有措辞时怎么取」，**没写「一个义务动词都没有时取什么」** ⇒ 模型默认 `shall` | ✅ **v2 已补**：无义务动词 ⇒ `note`，并显式禁止「按标准惯例默认 shall」 |
| 2 | 前置条款（`1`/`2`/`3`）**也生成了 8 个属性**，将来会变成检查项 | 叶子判定只看「有没有子节点」，不看「有没有要求」 | ✅ **用户裁定「生成阶段直接跳过」** ⇒ `IsPreambleClause()`（条款号 ∈ {1,2,3} ∪ 正文无义务动词；正文为空不判）；已清理历史 48 行 |
| 3 | `Process` 6 条**全是 `["management"]`** | 这 6 条确实都属管理类 | ✅ **误报**：用 `7.1.5.2` → `["metrology"]`、`8.5.1` → `["production"]` 复验，判据有效（v2 另加了 4 个正例） |
| 4 | `Artifacts`/`EvidenceChain` 产出形态**不统一**（有的给 JSON 契约、有的写散文） | 未规定**开头必须给输出契约** | ✅ **v2 已补**：第一句逐字给契约句式；实测 5+2 条 7/7 全部合规 |
| 5 | `Applicability` 6 条全 `general`、`ApplicabilityCondition` 全空 | 这 6 条确实都是通用条款 | ✅ 复验 `7.1.5.2` = `conditional` + 「当要求测量溯源时…」 |
| 6 | `Severity`：`4.1/4.2/4.3`=major、`1/2/3`=minor | 与判据 5 一致 | ✅ 成立 |

**v2 落地物**：`scripts/db/20261028_clause_attr_prompt_V2.sql`
- `PromptCode='clause_attr_v2'` / `Version=2` / 3730 字；旧 v1 置 `IsActive=0`
- ⚠️ 停用只停 `Version < 2`：⛔ 不能写「停所有 active」—— 重跑脚本会把 v2 自己停掉，而 INSERT 因唯一键冲突不做事 ⇒ 提示词凭空消失
- 实测：`PromptVersion=2` 生效；`iso14001` 跑 5 条 → 自动跳过 `1/2/3` 并在消息里点名



### 5.10 生成 JSON schema（提示词输出契约）

```json
{
  "ClauseNumber": "7.1.5.2",
  "ClauseIntent": "一句话说明这条款到底在管什么",
  "Obligation": "shall|should|may|note",
  "Applicability": "general|conditional",
  "ApplicabilityCondition": "有条件时的触发条件原文，否则空串",
  "Process": ["计量"],
  "Artifacts": "一段提取提示词（见 24 号 §3.5 范式）",
  "EvidenceChain": "一段取证提示词（见 24 号 §3.5 范式）",
  "Severity": "critical|major|minor",
  "Confidence": 0.0
}
```

> `Confidence` **不落库**（24 号已删该字段），只用于前端排序「先看低置信度的」。

---

## 6. 收口验收（P5）

- [x] 6.1 `dotnet build CertPlatform.sln` **0 Error**
  - 证据：`0 Error(s)`（214 Warning 为存量可空告警，⛔ 不作基线）
- [x] 6.2 后端单测全绿（基线 785/785 + `Api.Tests` 18/18）
  - 证据：`CertPlatform.Admin.Tests` **768/768** + `YZH.Core.Api.Tests` **39/39**，`Failed: 0`
- [x] 6.3 `node scripts/guards.mjs` 0 违规；`vitest` 全绿；`vue-tsc` 0
  - 证据：`✓ 前端架构守卫通过（37 条规则 / 1505 个文件）`；`vue-tsc -p cert/cert-admin/tsconfig.json` 0 错
- [ ] 6.4 `scripts/tools/audit-entity-config.py` 0 违规
- [ ] 6.5 端到端：登录 → 属性池页看 8 条 → 条款属性配置页选条款 → AI 预生成 → 清单出数 → 手改 → 保存
  - 🟡 已过：登录 → 条款属性配置页 → 点「AI 预生成」→ 跑 5 条 → 结果表 5 行（`/tmp/clause-attr-result.png`）
  - ✅ 已过（2026-10-29 改版后）：选「4.1 总要求」→ 清单 8 条全配 → 改「适用条件」→「已保存：更新 1」→ 落库；
    随后清回空值再保存，测试痕迹已清零（`cert_clause_attr` 96 行、`AttrValue` 无测试值）
  - ⬜ 未过：属性清单「新增」路径的 UI 点击（接口层已验证 `/add` 成功且 `Sort` 自动 10，测试行已物理清理）
- [ ] 6.6 更新 `MEMORY.md` §1.6 + 今日日志
- [ ] 6.7 `git commit`

---

## 7. 多标准隔离（P5，2026-10-28 用户指正）

> 用户原话：「针对一个标准一个提示词的设计还是没有体现在文档中，因为不确定最终企业选择几个标准进行认证，企业文档的结果，也应该只针对一个标准，如果有 3 标准认证，就有 3 个结果。」
> 落点：**24 号 §零「标准是第一维」**（唯一权威）+ 25 号 P9/结论 26。

- [x] 7.1 文档立「标准是第一维」：L0 共享 / L1 与提示词按标准独立 / 企业结果每标准一份 / 五条红线
  - 证据：24 号 §零（0.1–0.7）· 25 号 §二 P9 · §十二 结论 26 · §4.3 L2 唯一键说明
- [x] 7.2 代码：提示词解析改传 **标准 GUID**（原传 `null` ⇒ 永远只取平台级，标准级提示词等于不存在）
  - `ClauseAttrGenerateService`：`ResolveActiveAsync(PromptType, standard.Code)`
  - ⚠️ 口径：`wf_prompt_template.StandardCode` 存 **GUID**，与 `cert_iso_standard.StandardCode`（短码）不同（24 号 §0.6）
- [x] 7.3 回退可见：新增 `PromptScope`（`standard`/`platform`）+ 前端结果弹窗徽标（平台兜底 = 黄色警告）
  - 理由：⛔ 不许静默降级 —— 否则 4 个标准跑出同一种味道，还以为是模型问题
- [ ] 7.4 **4 个标准各写一份标准级提示词**（`clause_attr_iso9001_v1` / `_iso14001_` / `_iso45001_` / `_iso13485_`）
  - 依据 24 号 §0.4 四条理由：术语按标准分册（成文信息全在 9001 / 记录 44 条全在 13485）· `必须` 只在 45001+24001 · 13485 有四级条款 · 换版只换一本
  - 🟡 **待裁决**：是现在写 4 份，还是先全量跑平台兜底看整体效果再拆
- [ ] 7.5 企业侧表带 `StandardCode`（GUID 口径）—— 企业侧开工时的硬约束，⛔ 禁止 `StandardCodes` JSON 列
- [ ] 7.6 `cert_clause_attr.StandardCode` 短码 → GUID（D-N12，技术债；现状已显式换算，能跑）

---

## 8. 明确不做（本轮范围外）

| 不做 | 原因 |
|---|---|
| L2 企业值 `cert_enterprise_clause_attr` | 企业端暂缓（24 号） |
| L3 证据 / 预NC | 同上 |
| 26 号联网 Skill 的 2 张表 | 旁路基建，不占主链工期（D-P2 待裁） |
| 属性池「完备度红黄绿」视图 | 24 号已删（不设必填集） |
| `RequirementItems` a/b/c 拆分 | 24 号已删（实测无细项证据） |

---

## 9. 断点记录（每次收工更新这一节）

| 日期 | 停在哪 | 下一步 | 备注 |
|---|---|---|---|
| 2026-10-28 | — | 从 1.1 开始 | 本轮新建 TODO |
