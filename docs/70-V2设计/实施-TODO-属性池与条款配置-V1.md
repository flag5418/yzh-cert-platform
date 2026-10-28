# 实施 TODO：属性池 + 条款属性配置（含前端 + AI 预生成）

> **版本**：V1 ｜ **创建**：2026-10-28 ｜ **状态**：执行中
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

- [x] 4.2.1 `index.vue`：`YzhTreeTableLayout` 左树（标准 → **叶子条款**）+ 右表（该条款属性）
- [x] 4.2.2 `logic.ts` 继承 `TreeTableCore`（`TreeTableLogic` 已是废弃别名），覆写 `loadConfig`/`loadTreeRoot`/`shouldApplyTreeFilter`/`relatedValue`
  → ★ 额外覆写 `afterTreeLoaded`（补选项）/ `onPrepareAdd`（注入三键之二）/ `buildFilters`（追加 StandardCode）/ `columns` / `formFields` / `rowActions` / `openAddDialog` / `openEditDialog`
  → ⚠️ `dtoToNode` **不递归**（只服务懒加载），两层嵌套树必须自己写 `dtoToNodeDeep`，否则 208 条条款全丢
- [x] 4.2.3 右表列：属性（中文名 + 裸 Code 副行）/ 值来源 / 值 / 排序 / 状态
- [x] 4.2.4 新增行 `AttrCode` 从属性池下拉选（`GET ClauseAttrDef/options`，只列 `IsValid=1`），`ValueSource` 默认 `reference`
- [x] 4.2.5 `prompt` 类型属性用 `YzhDrawer` 大文本编辑（行内「编辑提示词」按钮**只在该行属性是 prompt 时出现**）
  → 实测：2 行数据中只有 prompt 那行有按钮；抽屉保存后 `AttrValue` 落库正确
- [x] 4.2.6 ★ 右面板滚动容器自给（外层 `height:100% + overflow:hidden`，内层 `flex:1;min-height:0;overflow:hidden`，并加 `.xxx__table > * { flex: none }`）
- [x] 4.2.7 路由注册（`router/index.ts` 两条，path 与 `Sys_Menu.Url` 逐字一致）

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

- [ ] 5.1 新增提示词类型 `clause_attr`（`wf_prompt_template`，`PromptType='clause_attr'`，平台级 1 条）
- [ ] 5.2 提示词正文：输入「条款号 + 标题 + 正文」，输出 8 属性的 JSON（严格 schema，见下）
- [ ] 5.3 `Admin/Services/Cert/ClauseAttrGenerateService.cs`
  - 分批（默认 5 条/次）→ `LlmInvokeService.CompleteAsync(ForceJson=true)`
  - 落 `cert_clause_attr`（`ValueSource` 默认 `reference`；`fixed` 只在能确定时给）
  - ⛔ 直插必须显式 `Code = Guid.NewGuid().ToString("N")` + **接返回值判 `Success`**（守卫 `B-R5`）
- [ ] 5.4 端点 `POST /api/Admin/Cert/ClauseAttr/generate`（入参 `StandardCode` + `ClauseCodes[]` + `Overwrite`）
- [ ] 5.5 前端：条款属性配置页加「AI 预生成」按钮 + 分批进度条 + 逐条结果列表
- [ ] 5.6 先跑 **iso9001 1 个条款** 单点验证输出结构对不对（⛔ 别直接全量）
- [ ] 5.7 抽 3 条人工评审（`Obligation`/`Process`/`Artifacts` 是否靠谱），改提示词再跑
- [ ] 5.8 全量跑 273 条 → 统计每个属性的填充率
- [ ] 5.9 落库校验：`SELECT AttrCode, COUNT(*) FROM cert_clause_attr GROUP BY AttrCode`

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

- [ ] 6.1 `dotnet build CertPlatform.sln` **0 Error**
- [ ] 6.2 后端单测全绿（基线 785/785 + `Api.Tests` 18/18）
- [ ] 6.3 `node scripts/guards.mjs` 0 违规；`vitest` 全绿；`vue-tsc` 0
- [ ] 6.4 `scripts/tools/audit-entity-config.py` 0 违规
- [ ] 6.5 端到端：登录 → 属性池页看 8 条 → 条款属性配置页选条款 → AI 预生成 → 右表出数 → 手改 → 保存
- [ ] 6.6 更新 `MEMORY.md` §1.6 + 今日日志
- [ ] 6.7 `git commit`

---

## 7. 明确不做（本轮范围外）

| 不做 | 原因 |
|---|---|
| L2 企业值 `cert_enterprise_clause_attr` | 企业端暂缓（24 号） |
| L3 证据 / 预NC | 同上 |
| 26 号联网 Skill 的 2 张表 | 旁路基建，不占主链工期（D-P2 待裁） |
| 属性池「完备度红黄绿」视图 | 24 号已删（不设必填集） |
| `RequirementItems` a/b/c 拆分 | 24 号已删（实测无细项证据） |

---

## 8. 断点记录（每次收工更新这一节）

| 日期 | 停在哪 | 下一步 | 备注 |
|---|---|---|---|
| 2026-10-28 | — | 从 1.1 开始 | 本轮新建 TODO |
