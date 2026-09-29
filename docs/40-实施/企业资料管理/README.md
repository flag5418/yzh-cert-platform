# 企业资料管理 — 实施文档集（待审批）

> **模块**：企业资料管理（专家端 `/resources`，菜单 `MENU_AUD_04`）
> **编制**：2026-09-28 | **状态**：**待审批** —— 审批通过后方可编码
> **上位约束**：`AGENTS.md`（样板页面 / 后端基类 / PascalCase 三处一致 / 守卫）+ `项目全局规则.md`
> **代码基线**：`/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台`（即 9990/9992 正在运行的工作区）

---

## 一、模块定位（与"企业资料规范化"划清边界）

| 维度 | 企业资料**规范化**（本模块不做） | 企业资料**管理**（本模块范围） |
|---|---|---|
| 输入 | 企业散乱资料（命名混乱、无目录结构） | 企业**已按标准目录整理好**的资料 |
| 核心动作 | 指纹识别 → 画像 → 召回 → LLM 精排 → **人工裁决** → AI 填充 | **按标准目录对号入座地上传、替换、删除** |
| 是否涉及空标准文件 | 是（空模板 → 按材料填写） | **否**（本模块不填内容） |
| 产出 | 标准文档实例（带可信度 / 溯源） | 就位的企业文件（可预览 / 下载 / 转换） |
| 下游消费 | 审核页面字段图谱 | **NC 检查 + 报告生成** |
| 设计文档 | `docs/20-体系认证/03-详细设计/05-企业资料规范化/`（14 篇，本模块**不引用其算法**） | 本目录 |
| 本次是否做 | **否**（明确排除：匹配引擎 / 画像链 / 契约五要素 / D24–D28 增补设计 / 9 张新表） | **是** |

**一句话**：本模块只保证"企业把标准目录格式的资料正确落到该企业 × 该标准 × 该阶段的目录里"，并把状态（就位 / 转换 / 异常）暴露给下游 NC 与报告。

---

## 二、需求原文（用户 2026-09-28 提交，作为验收基准）

1. **页面设计：左树右表**
   - 左树由**企业 + 阶段**构成；**必须选择阶段，才能进行资料上传**。
   - 右侧按"企业阶段关联"得到该阶段下有哪些标准；**每个标准可独立上传文件夹或文件**。
2. **功能设计**：参考后台管理 `/business/directory-manager`；**表结构全部复用**，只是 `EnterpriseCode` 换成当前企业 Code。
3. **多标准语义（本模块技术重点）**
   - 企业是按阶段上传的，但默认要**按该阶段关联的每个标准分别上传**。
   - 一个文件夹里可能有 3 个标准的文件 ⇒ 该资料需要按标准**各上传一份**（3 份）。
   - 右侧顶部有**针对所有标准的上传**：一次上传后，必须**找到"机构-标准-阶段"的标准目录格式**，把**属于该标准的文件**放进该标准目录；**禁止**把非本标准的文件也塞进所有标准。

---

## 三、文档索引

| # | 文档 | 内容 |
|---|------|------|
| 00 | `README.md`（本文件） | 定位、边界、需求、索引、**待审批决策 D1–D10**、审批流程 |
| 01 | `01-Demo与交互设计-V1.md` | 页面线框、左树/右区/标准卡片、三种上传交互、组件清单、状态与文案、样式规范、与现状差异 |
| 02 | `02-数据库实现-V1.md` | 4 张表逐列（实测）、索引、**键口径两处不一致**、企业行写入规则、查询口径、验证 SQL |
| 03 | `03-MinIO存储规则-V1.md` | 桶与配置、双库前缀、四类路径规则、保留段、`IObjectStorage` 能力、产物/归档细节、现存脏数据 |
| 04 | `04-上传与文件替换-V1.md` | 四段上传全流程、状态机、双队列转换、**替换与版本归档**、删除/回滚、**多标准分发算法** |
| 05 | `05-接口与代码结构-V1.md` | 现有端点（含缺陷）、目标端点清单、三层同构文件清单、权限与菜单、复用改造边界 |
| 06 | `06-开发计划-V1.md` | P0–P5 WBS、开工前置种子、每期验收门、人天估算 |
| 07 | `07-现状缺陷与修正清单-V1.md` | B1–B8 逐条：证据 / 影响 / 修正方案 / 归属阶段 |
| 08 | `08-本批落地与验收待办-V1.md` | 本批交付小结、**验收步骤**、**隐患清单 H1–H7（待验收后统一处理）**、数据现状快照、复现命令 |
| 09 | `09-内容提取与队列管理-执行方案-V1.md` | **内容提取（Markdown → 字段/表格）+ 专家端队列管理**方案：需求 R1–R7、现状实测、WP1–WP9、DDL 草案、H8–H15、D11–D20 |
| 10 | `10-执行计划-V1.md` | **内容提取执行计划 V3（待审批）**：一页速览 → 业务模型（`AutoExtract` 自动提取 / 3 态状态机 / `IsValid` 与 `IsDeleted` 正交）→ 4 个差距 → **追溯链锚点图 + 5 张流程图** → 两域契约与状态机 → S0–S6 分期 → DDL（**仅规则表加 `OrgCode`**）→ 6 条决策（按默认执行） |
| 09 | `09-内容提取与队列管理-执行方案-V1.md` | **第二批（待审批）**：内容提取（Markdown→字段/表格）+ 专家端队列管理。需求 R1–R7、现状实测（提取链已存在且跑通过）、WP1–WP9 功能分解、DDL 草案、接口清单、验收 V1–V9、隐患 H8–H15、**决策 D11–D20**、人天 8.5–9.5 |

---

## 四、现状结论摘要（2026-09-28 实测，证据在 02/03/07 分册）

| 项 | 实测 |
|---|---|
| 表结构 | `cert_standard_directory_config/folder/file` 已含 `EnterpriseCode` 列；`cert_enterprise_file_version` 已建（`scripts/db/20260927_enterprise_file_extend_V1.sql` 已执行） |
| 模板数据 | `config` 5 行（`EnterpriseCode` **全为 NULL**，非 `'YZH-STD-ENT'`）；其中**仅 1 套有内容**：11 文件夹 + **167 文件定义** |
| 有内容的模板归属 | OrgCode=河北雄安尚龙认证有限公司；StandardCode=9001标准；StageCode=**复审** |
| 企业数据 | `cert_enterprise` **0 行**、`cert_enterprise_stage` **0 行**、企业文件行 **0 行** → 无法验收，需先种子 |
| 后端 | `CertPlatform.Auditor`：`EnterpriseFileController`（76 行 / 10 端点）+ `EnterpriseFileService`（362 行）—— 骨架可用但**多处为桩**（见 07） |
| 前端 | `cert-auditor/src/pages/resources/`（index 274 + logic 288）—— 原型，与你给的布局不一致 |
| 存储底座 | `PathBuilder`（318 行，唯一路径构造器）/ `IObjectStorage` + `MinioObjectStorage` 已就绪；`PathBuilder.EnterpriseFile` **零调用待启用** |
| 转换底座 | `file_convert` 双队列（`office2pdf` + `anydoc2md`）+ `OfficeConvertTaskExecutor` **可直接复用**（产物路径由源路径派生，与库前缀无关） |
| 菜单 | `MENU_AUD_04 资料库 /resources`，与路由一致（R12 通过） |

---

## 五、待审批决策（D1–D10，默认值即执行值；不改默认则按默认执行）

| ID | 决策点 | 默认建议 |
|---|---|---|
| D1 | 页面落点 | **改造现有 `/resources`**，菜单名改「企业资料管理」；不新增菜单 |
| D2 | 机构归属解析 | 工作区 → `Sys_Organization` 向上找 `OrgType=CertBody` 祖先 Code；解析不到 → 取该标准+阶段唯一模板并**告警** |
| D3 | 阶段键换算口径 | **后端统一换算**（前端只传 `cert_cert_stage.StageCode` 业务码） |
| D4 | 未归属文件（不匹配任何标准模板） | 单列"未归属区"，可**人工指派**或忽略；**不自动丢弃、不自动塞入各标准** |
| D5 | 同标准内同名多文件命中同一目标 | 冲突区人工选「全部上传 / 只取一份」 |
| D6 | 上传实现 | **泛化 Admin 四段上传**（加 `library` 参数），企业侧不自建一套 |
| D7 | 企业资料存储前缀 | `/enterprise-documents/{EnterpriseCode}/{StandardCode}/{StageCode}/{FolderPath}/{FileName}` |
| D8 | 模板外文件（该标准模板未定义的资料） | **默认不允许自动上传**；仅人工指派时落库（`TemplateFileCode` 留空） |
| D9 | 上传主体 | 首期**专家/审核员代传**（`cert-enterprise` 仍为空壳，本模块不动） |
| D10 | 既有 `trigger-extract` / `extraction-result` 端点 | **移除或标注"下游未实现"**（当前是硬编码假成功），本模块不接 AI |

---

## 六、审批与执行流程

1. **审批人**：用户（项目负责人）。
2. **审批对象**：本目录 8 份文档 + §五 决策表。
3. **审批通过后动作**：
   - 依 `06-开发计划-V1.md` 逐期（P0→P5）执行，每期结束跑出口门（`dotnet build` 0 错误 / `guards.mjs` 0 违规含 R12 / 菜单快照刷新 / `typecheck+build`）；
   - 每期完成后**回写实际人天与偏差**到 06 分册，不新开文档；
   - 决策变更同步改本文件 §五 与 01/04 分册对应处。
4. **未审批前**：不写业务代码，不改表结构，不动 `Sys_Menu`。

---

## 八、执行记录（2026-09-29，后端 + 前端已落地）

> 用户裁决：D1–D10 按默认值实施，**允许合理偏差但须回写本表**。以下为实际实现口径与偏差。

### 8.1 已实现（附验证证据）

| 层 | 产出 | 验证 |
|---|---|---|
| 匹配器 | `Auditor/Services/Ent/DispatchMatcher.cs`（M0–M3 + 归一化 + BuildPlan，纯函数） | `upload/plan` 实测：命中行 / 未归属 / 冲突 / 跨标准 / BlockReason 均正确 |
| 服务 | `Auditor/Services/Ent/EnterpriseFileService.cs` 整体重写（stage-tree / stage-overview / standard-directory / 四段式 / plan / download / queue / 替换恢复版本历史提取） | `scripts/db/verify/enterprise-file-e2e.py` **33/33 PASS** |
| 控制器 | `EnterpriseFileController` 重写（新端点 + 请求 DTO，PascalCase） | 同上 |
| 前端 API | `cert-share/src/api/ent/enterprise-file.ts` 全量重写（新契约 + `yzhApi.getBlob` 取字节，禁裸链接） | `typecheck` + `build:auditor` 通过 |
| 前端页面 | `resources/logic.ts` 重写（含 `treeNodes` 单树模型）+ `resources/components/StandardFolderTree.vue`（**递归**文件夹树）+ `index.vue` 重写（后台管理风格双卡片 + 4 个弹窗/抽屉） | 浏览器实测：左树「企业 → 阶段」单树（搜索命中保留父节点）→ 汇总 2/167/6/0/161 → 11 文件夹三级嵌套 → 槽位状态/操作列正确 → 控制台零告警 |
| 出口门 | `dotnet build` 0 错；`guards.mjs` 20 规则 882 文件 0 违规；`build:share`/`build:auditor` 成功 | — |

### 8.2 偏差与理由（须逐条回写）

| # | 偏差 | 理由 | 对应原文 |
|---|---|---|---|
| E1 | **StoragePath 不在 Step1 落库**，只在 Step2 写；Step1 只把计划路径回给前端 | 01 §5.1 规定「`StoragePath` 空 = 缺失」。Step1 就写库会让**未传字节的槽位**被对账/`IsLive` 误判为「已就位」；改为同一算法在 Step2 按 DB 记录重算（更防伪造） | 04 §一 Step1-5 |
| E2 | 每可转换文件 **1 个** TaskItem（`ConvertType=""`，执行器自动双产物） | 失败语义隔离由执行器内部保证，省一半任务行与锁；`ConvertStatus`/`MarkdownStatus` 仍互不干扰 | 04 §3.1「2 个任务」 |
| E3 | **移除不删对象**（仅 `IsValid=0`，对象原地保留） | 恢复 UX + 06 册 02 号审计链（证据保全）。⛔ 不置 `IsDeleted`：本行同时是「应上传槽位」定义行，软删会被全局过滤器抹掉 ⇒ 需求凭空消失 | 04 §六「删对象 + 软删」 |
| E4 | `trigger-extract` / `extraction-result` **保留真链**（真入队 `doc_extract`） | D10 给的选项是「移除或标注未实现」；实测已是真链（G-2e），移除会砍掉可用能力。页面「提取」入口仅对 `ConvertStatus=completed` 显示 | D10 |
| E5 | 路由名保留 `replace` / `delete` / `restore`（同时提供 `files/{fileCode}/replace|delete` 新契约） | 避免与在飞前端脱节；两套路由指向同一服务方法，非双实现 | 05 §二 #12–13 |
| E6 | 路径口径统一为**相对配置根**：模板 folder 行含根段（`CS…13485体系材料/1质量手册`），对外/落库一律剥掉根段 ⇒ `1质量手册` | 03 §3.2 与 05 §8.1 的示例均为「相对配置根」；不剥根段会让企业库多一层冗余目录，且与文档契约不符。`IncludeEnterpriseDirectoryAsync` 同步补写槽位 `FullPath`（历史行为空，Step2/恢复靠它反推文件夹） | 03 §3.2 / 05 §8.1 |
| E7 | **目录级队列互斥扩展到 `replace`/`restore`**，并在 `EnqueueConvertQueueAsync` 内加兜底校验 | 实测缺陷：同秒的 replace 与 restore 各建一个 `ScopeKey` 相同的队列（原实现只在 `upload/init` 查互斥）⇒ 两个队列并发回写同一行。现在命中即拒并给出队列号 | 04 §七 |
| E8 | `standard-directory` 只回 `IsValid=1` **或有** `StoragePath` 的行 | 排除上传流程自管的草稿行（`IsValid=0` 且无对象）—— 否则草稿行会以「已移除」出现在清单里 | 05 §8.1 |
| E9 | 状态机判定顺序：缺失 → 上传中 → 已移除 → 转换态 | 草稿行「已传字节但未 confirm」不得被误判为「已移除」 | 01 §5.1 |

### 8.2.1 第二轮修正（2026-09-29 缺陷评估后，见 §8.5）

| # | 偏差 | 理由 | 对应原文 |
|---|---|---|---|
| E10 | **M3 不再瞎猜**：改「完整目录前缀 + 扩展名族 + 名称 bigram 相关度（≥2 且唯一胜出）」，猜不准一律落未归属 | 旧实现只取目录**首段**再返回 `slots[0]`：实测 `4记录文件/生产类/不合格品处置记录.doc` 被塞进 `4记录文件/其它/XASL-OR-001 培训签到及有效性评价表.doc`（跨目录 + 跨语义双重错配），且前端**默认已勾选** ⇒ 点确认即写入错槽位。语义从「分类兜底」改为「高置信兜底」 | 04 §4.2 / 01 §3.3 |
| E11 | **编码前缀正则末段改为可选**（`XASL-XX-001 名称` 与 `XASL-XX 名称` 都能剥） | 实测模板 167 条里 19 条是 `XASL-QM 名称` 形式（含旗舰槽位 `XASL-QM 质量手册`），旧正则要求末段必有数字 ⇒ 这 19 条主词永远剥不掉编码前缀 ⇒ M1/M2 全不命中 ⇒ 一律掉进 M3 | 04 §4.2 |
| E12 | M3 行**默认不勾选**（`Selected: !BlockReason && !NeedsConfirm`） | E10 收紧了匹配质量，「需人工确认」必须真的是一个动作，而不是默认值 | 01 §3.3 |
| E13 | 计划行 ↔ 本地 File 的索引键从 `File.name` 改为 **`RelativePath \|\| FileName`**；入参按该键去重 | ① 同名不同目录（`1质量手册/x.doc` 与 `4记录文件/x.doc`）会塌缩到同一个 `File` 对象，同一份字节被写进多个槽位 —— 正是需求 3 的主场景；② 重复添加同一文件会造出「同文件跨标准命中 9001标准 + 9001标准」这类假告警 | 04 §四 / 需求 3 |
| E14 | 计划行新增 **`MaxSizeMB` 拦截**（`upload/plan` 出 BlockReason，`upload/init` + `upload/file` 双重兜底） | 实体与模板都有 `MaxFileSizeMB`（默认 10MB）但**从未被任何一处校验**，模板声明的上限形同虚设 | 02 §三 |
| E15 | 新增状态 **`markdownFailed`**（缺 Markdown 产物），前端标黄 + 禁用「提取」 | 旧 `StatusOf` 只判 `ConvertStatus`：实测 6 个已就位行里 4 个 `.doc` 的 Markdown 产物恒失败（PDF 已生成）却仍显示绿色「已就绪」；「提取」按钮只看 `ConvertStatus==='completed'` 可点，后端却因缺 `MarkdownPath` 直接拒收 ⇒ 死按钮 | 01 §5.1 |
| E16 | `upload/init` 改为**先全量校验、后统一写入** | 旧实现「校验一项写一项」，第 N 项失败时前 N-1 项已把槽位行改成 `TaskId/pending`，而 `UploadTask` 行在循环后才插 ⇒ 半成品行既不能 confirm 也不能 cancel | 04 §一 Step1 |
| E17 | 前端补 **未归属人工指派**（D4 的另一半 / 原 N2-H6）：批量弹窗未归属区可逐个选「标准 + 文件夹」，走已有指派模式；单标准弹窗加「直接放入所选文件夹」 | 后端 `upload/init` 指派模式本就可用，只差前端。**实测必要性**：营业执照、体系证书等 PDF 因模板未定义 pdf 槽位而恒 `no_match`，此前**一条都传不上去** | 04 §四 / D4 / D8 |
| E18 | 树节点 Key 改为 **`S:{企业Code}:{阶段Code}`**（原 `S:{阶段Code}`） | 同一工作区多个企业会关联同一阶段（实测 `G4测试企业甲` 与 `测试企业b` 都关联了 `复审`）⇒ `node-key` 撞车，`setCurrentKey` 会把高亮打到别的企业节点上 | 01 §3.1 |
| E19 | `file-check` 端点字段统一 **PascalCase** | 它是控制器里唯一的 camelCase 出口（`configured`/`standardCode`/`requiredCount`…），违反 AGENTS.md ③ | AGENTS.md ③ / 20 契约表 |
| E20 | `EnsureDirectoryAsync` 增「**机构漂移修正**」分支：归一后的 `OrgCode` 查不到、但存在同企业同标准同阶段挂在别机构下的旧行 ⇒ 改其 `OrgCode` 而非新建空目录 | `uk_enterprise_std_stage` 含 `OrgCode`，`CbCode` 一改 / 企业改挂机构 / 工作区切换就会让带 167 行槽位 + 归档 + 存储对象的旧目录变孤儿，且零报错（H3 的代码侧收口） | 02 §3.3 / H3 |
| E21 | **目录初始化失败改为「原因码 + 等待态不打扰」**：`check/add` 回执新增 `Reason`（`template_missing` / `org_unbound` / `invalid_request`）；关联页对 `template_missing` **不再弹提示**，`org_unbound` 给准确指引，其余才报错 | 用户指出「企业资料目录的初始化是静默后台自动执行的」，原提示与此**自相矛盾**。事实：`EnsureDirectoryAsync` 在 `stage-overview`(178) / `standard-directory`(263) / `upload/plan`(505) / `upload/init`(642) **每次读都跑**，关联时的初始化只是**预热**；`template_missing` 是纯数据缺失的**等待态**，补齐模板后自动恢复，**不需要重新关联**。原来那句 `warning` 会让用户以为刚建好的关联失败了 | 05 §三 / H2 / L3 |
| E22 | `/resources` 的「不可用标准」提示**去掉 Toast**（`loadDirectories` 不再 `ElMessage.warning`），只留常驻提示条；逐标准清单改短原因，处置说明（关联有效 / 补齐后自动生成）统一收到提示条 hint | `loadDirectories` 在每次切阶段与 5s 轮询都会跑 ⇒ 飘字提示等于反复骚扰；且与常驻条内容重复、逐标准清单塞长句会被读成多条重复告警 | 01 §3.3 |
| E23 | 关联页对**真故障**用 `ElMessage.error`、对 `org_unbound` 用 `warning` | 原来所有失败一律 `warning`，把「缺模板」和「代码缺陷」混为一谈 | 05 §三 |


### 8.3 未完成项（下一批）

| # | 项 | 说明 |
|---|---|---|
| ~~N1~~ | ~~右侧卡片布局~~ **已按用户 2026-09-29 裁决改口径** | 01 §3.2 原「每标准一张卡片并存」**废弃**：改为**后台管理风格**——整页灰底 + 两张白卡片（左「企业与阶段」单树、右「阶段资料」：阶段头 + 汇总条 + 标准 Tab），左侧**只有一棵树**（企业为父、阶段为子，不再叠加「企业树 + 阶段单选组」两套控件）。见 §8.4 |
| ~~N2~~ | ~~未归属区「人工指派」~~ **已实现（E17）** | 后端指派模式本就可用，本轮补前端：批量弹窗未归属区逐个选「标准 + 文件夹」+ 单标准弹窗「直接放入所选文件夹」。实测 `营业执照.pdf` 已能落到 `4记录文件/其它/营业执照.pdf`（`StandardFileCode=NULL`、`IsRequired=0`） |
| N3 | `[RequirePermission]` 未标注（05 §七） | 需先迁移 `sys_api.Enable → IsValid`，再跑 ApiSync + 角色重授权；现在标注会静默 403 |
| N4 | 多标准验收种子未建 | 食品标准 `475da4fe × 复审 29c1bcc3` 无模板 config ⇒ 该行恒「机构未配置标准目录」，多标准分发无法现场验收 |
| N5 | 菜单改名「资料库 → 企业资料管理」未执行 | 需动 `Sys_Menu` 并重跑 `sync_menu_urls.sh` |

### 8.5 第二轮遗留（缺陷评估后仍未做）

| # | 级别 | 项 | 说明 |
|---|---|---|---|
| L1 | P1 | **存储路径双口径未迁移** | MinIO 现存两套：`…/1质量手册/XASL-QM 质量手册.doc`（E6 新口径）与 `…/499ceab9883c4101a50bbfacd42f2c12/质量手册封面.docx`（旧口径把 folderCode 当路径段）。`Step2`/`RestoreFileAsync` 按新算法重算 ⇒ 旧行做恢复/替换归档会写新路径、旧对象成孤儿，`_archive` 链跨两套路径。**需一次性迁移脚本**（`scripts/db/fix/`） |
| L2 | P1 | `.doc` 的 Markdown 产物恒失败 | 6 个已就位行里 4 个如此（PDF 成功、Markdown failed）。E15 只是**如实暴露**了它，没有修转换器。需排查 `anydoc2md` 对旧版 `.doc` 的支持，或在上传端提示「建议 docx」 |
| L3 | ~~H2 空模板文案给了错误处方~~ **已改（E21/E22）** | 初审空态原写「维护后**重新关联该企业**」—— 错的。实际只需给模板补内容，下次读接口的 ensure 就会补槽位；现已统一为「关联有效；补齐模板后刷新本页即自动生成」 |
| L4 | P1 | H5 轮询串行 | `startPolling` 内 N 个标准 = N 次串行 `standard-directory` 往返；`queueBusy` 还只取 `stds[0]`（第 2 个标准在转时按钮不禁用），且只在轮询中刷新、初次进入恒 false |
| L5 | P2 | A2 未闭环 | `UploadQueueCancelHandler`（Admin）只处理 `SourceType=="upload_task"`，本模块 replace/restore 用 `"file_replace"` ⇒ 取消队列不撤销这些任务。07 §三 A2 记了但无归属批次 |
| L6 | P2 | 历史脏数据 | `TEST-ENT-29c1bcc3-cfg` 仍是 B8 违规的拼接 Code（B8 修复只对新行生效） |
| L7 | P2 | 单文件入口缺目录上下文 | 批量弹窗用「选择文件」时 `RelativePath` 只有文件名 ⇒ M0/M3 因无目录段必然不命中，只能靠人工指派。属设计边界（分类需目录），但文案未提示 |
| L8 | P2 | 刷新丢选中态 / 左树默认全展开 | H7-3、H7-4，未做 URL/sessionStorage 记忆 |
| L9 | P2 | E2 的「失败语义隔离」论证不成立 | 旧偏差说执行器内部保证双产物失败隔离，实测 PDF 成功 + Markdown 失败时**任务与队列状态仍为 `failed`**（6 个队列全 failed）。E15 改用独立状态暴露，不再依赖该论证 |


### 8.4 布局裁决（用户 2026-09-29 现场评审）

| 项 | 裁决 | 落地 |
|---|---|---|
| 视觉风格 | 对齐后台管理（`/business/directory-manager` 一系）：页底 `--el-bg-color-page` 灰底，内容一律白卡片（`1px solid var(--el-border-color-lighter)` + `4px` 圆角，同 `YzhCard` 令牌） | `index.vue` `.panel` 基类；汇总条用 `--el-fill-color-lighter` 淡底 + 竖分隔线 |
| 左侧结构 | **一棵树**：企业（父）→ 阶段（子）。专家未来管多个企业 ⇒ 左侧只放树本身，搜索框在树上，不用下拉/单选组承载选中态 | `logic.treeNodes`（`Key = E:企业Code / S:阶段Code`）+ `el-tree` 单例；企业节点带「N 阶段」徽标，阶段节点带「N 标准」标签（未关联置灰 + 点击给配置出口） |
| 选中态 | 唯一权威 = `logic.selectedKey`（`watch` 同步 `setCurrentKey`），单阶段企业自动深入到阶段节点 | `logic.selectTreeNode` / `selectEnterpriseByCode` |
| 上传硬约束 | 不变：未选阶段 → 右侧空态、所有上传入口不可用（需求 1） | `index.vue` 右卡片 `v-if="logic.selectedStage.value"` |

### 8.5 第二批范围（2026-09-29 用户提出，**待审批**）

> 用户原文：上传资料时应像后台管理「文档内容提取规则」一样把 Markdown 转成字段/表格并存储，供 NC 检查与报告；**替换文件要清理该文件对应的字段与表格数据**；页面需有「内容提取」按钮（按阶段 / 按标准 / 单文档）；表格要显示哪些文档已提取成功；提取同样要队列管理；专家端也要队列管理（只看当前机构、失败可重试）。
>
> 执行方案见 **09 分册**。现状实测：提取链底座**已存在且跑通过**（12 个 `doc_extract` 队列 completed、执行器已注册、转换完成自动追加提取任务），真缺口是五项——**规则覆盖（全库仅 1 条规则）、替换清理、状态可见、批量入口、企业隔离**。**未审批前不动代码。**

---

## 七、编码强制约定（执行时不得偏离）

- 后端：业务代码写 `src/certplatform-api/`；控制器按职能选基类（本模块建议 `YzhControllerBase<T>` / `WebControllerBase`），**禁止裸 `ControllerBase`**；`src/old/**` 禁改。
- 前端：使用自研 `yzhApi` + `YzhTable`/`YzhForm`（或页面内 `el-tree`/`el-table` 手写，须与 `directory-manager` 同款）；**禁止** `view-grid`/`VolBox`/`VolForm`/`VolProvider`/`axios`；`.vue` 内禁止直接 `fetch(`。
- 命名：**DB 列名 = C# 属性名 = TS 字段名 = PascalCase 逐字一致**；`Enable` 零容忍（唯一启用字段 `IsValid`）；启停用 `IsValid`、软删用 `IsDeleted`。
- 双关键字准则 A：定位/删除/更新/关联**只用 `Code`**；`Id` 不进 WHERE。
- DB：`utf8mb4` + **显式** `COLLATE=utf8mb4_general_ci`；脚本放 `scripts/db/`（不得散落）。
