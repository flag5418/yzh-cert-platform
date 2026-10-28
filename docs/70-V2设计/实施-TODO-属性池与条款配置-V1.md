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

- [ ] 3.1 实体 `CertPlatform.Shared/Entities/Cert/ClauseAttrDef.cs`（`[SugarTable("cert_clause_attr_def")]`）
- [ ] 3.2 实体 `CertPlatform.Shared/Entities/Cert/ClauseAttr.cs`
- [ ] 3.3 EntityConfig `Admin/Assets/EntityConfigs/Cert/ClauseAttrDef.json`（⚠️ 文件名 = 类名；`Yxk` 可空列必须 `true`）
- [ ] 3.4 EntityConfig `Admin/Assets/EntityConfigs/Cert/ClauseAttr.json`
- [ ] 3.5 控制器 `Admin/Controllers/Cert/ClauseAttrDefController.cs`（`: YzhControllerBase<ClauseAttrDef>`）
- [ ] 3.6 控制器 `Admin/Controllers/Cert/ClauseAttrController.cs`（含左树端点 `/tree/root`：标准 → 条款）
- [ ] 3.7 登记 `Program.cs` 的 `BusinessEntityConfigPaths`（若 `Assets/**` 通配已覆盖则记录实测结论）
- [ ] 3.8 `dotnet build CertPlatform.sln` **0 Error** + 重启后端
- [ ] 3.9 `curl` 冒烟：`GET config` / `POST add` / `POST filter` / `POST toggle-valid` 全通
- [ ] 3.10 ★ 反向用例：`ValueSource` 空提交必须被拒（`NOT NULL` 语义）、`AttrCode` 不在池中必须被拒

---

## 4. 前端页面（P3）

### 4.1 属性池页（`/cert/clause-attr-def`）

- [ ] 4.1.1 `cert-admin/src/pages/cert/clause-attr-def/index.vue`（单表 CRUD，`YzhTablePage` 范式）
- [ ] 4.1.2 `logic.ts`：`controllerName = 'Cert/ClauseAttrDef'`
- [ ] 4.1.3 `ValueType` 用受控下拉（`text/dict/bool/json/prompt`），`DictCode` 仅在 `dict` 时必填（前端联动）
- [ ] 4.1.4 `prompt` 类型用多行文本域（⛔ 单行 input），并提示「值即提示词，内部须输出 JSON」

### 4.2 条款属性配置页（`/cert/clause-attr`）

- [ ] 4.2.1 `index.vue`：`YzhTreeTableLayout` 左树（标准 → 条款）+ 右表（该条款属性）
- [ ] 4.2.2 `logic.ts` 继承 `TreeTableLogic`，覆写 4 处（`loadConfig`/`loadTreeRoot`/`shouldApplyTreeFilter`/`relatedValue`）
- [ ] 4.2.3 右表列：属性名 / `ValueSource`（固定·参考）/ 值 / 启用
- [ ] 4.2.4 新增行时 `AttrCode` 从属性池下拉选（只列 `IsValid=1`），`ValueSource` 默认 `reference`
- [ ] 4.2.5 `prompt` 类型属性用 `el-drawer` 大文本编辑（⛔ 表格内塞 textarea）
- [ ] 4.2.6 ★ 右面板滚动容器自给（`YzhTreeTableLayout` 右面板 `overflow:hidden` 坑）
- [ ] 4.2.7 路由注册（`router/index.ts`，path 必须与 `Sys_Menu.Url` 逐字一致）

### 4.3 菜单

- [ ] 4.3.1 SQL 插入 `MENU_00220` 条款属性池（`MENU_00302` 下，OrderNo 400）
- [ ] 4.3.2 SQL 插入 `MENU_00221` 条款属性配置（`MENU_00302` 下，OrderNo 410）
- [ ] 4.3.3 `bash scripts/db/verify/sync_menu_urls.sh` 重新导出快照（⚠️ 该文件须随代码提交）

### 4.4 前端收口

- [ ] 4.4.1 `node scripts/guards.mjs` **0 违规**（重点 `S04` 显式 type / `S07` 禁裸 dialog / `R18` 禁裸 hex）
- [ ] 4.4.2 `npm run build`（= guards + `vue-tsc` + vite build）通过
- [ ] 4.4.3 浏览器实测：能新增/编辑/删除，`prompt` 长文本能存能回显

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
