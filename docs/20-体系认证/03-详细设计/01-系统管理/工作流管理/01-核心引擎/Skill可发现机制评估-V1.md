# Skill 可发现机制评估 V1

> **日期**：2026-09-27
> **版本**：V1 | **状态**：待评审
> **性质**：**机制评估**（非功能设计）—— 回答"基建 + Skill 可发现模式这套机制是否合理"
> **前置**：`规则Skill与证据链-功能设计-V1.md`（能力层要补什么）、`工作流引擎-总体架构设计-V3.md`（基建层已有什么）
> **方法**：给出可判断的评估判据 → 逐条用代码取证 → 分层给结论 → 缺口分级
> **★ 交付状态（2026-10-10）**：§6 的 **P0-1 / P0-2 已落地**（端点 `GET /api/Admin/Workflow/WfSkill/metadata` + 前端 `loadSkills` 端口回填），D1→D2 通路已通，详见 **§10 实施记录**。§2 表 ②③、§3 D2 的"死链"结论为 2026-09-27 取证快照，已被 §10 覆盖。

---

## 0. 结论摘要（先看这 5 行）

| # | 判断 | 依据强度 |
|---|---|---|
| 1 | **分层方向是对的**：基建（DAG/队列/执行/状态机）与能力（Skill）分离，是正确且成熟的架构选择 | V3 架构 + 引擎实现完整 |
| 2 | **"可发现"只在 C# 代码里成立**，在数据库里不成立、在设计器里不成立 | 四套契约来源，两套是死的 |
| 3 | **自描述能力已建好但没有出口**：`SkillExecutor.Analyze()` 能产出完整端口元数据，`ISkillRegistry.LoadAsync` **零调用方** | grep 全仓仅接口声明 |
| 4 | **推断：普通 skill 节点在属性面板零可配置项**（仅名称/类型/测试），端口与参数面板只覆盖 8 个硬编码特殊节点 | 三处代码串联，需实测复核 |
| 5 | **评估机制本身的核心度量**：新增一个 Skill 需改 **5 个编辑点**，其中 2 个纯属人工冗余 —— 这就是"可发现"未贯通的量化证据 | 见 §5.1 |

**一句话**：这套机制的**骨架合理**，但"可发现"目前是**设计意图而非已交付能力**；补齐的代价不高（一个查询端点 + 一次设计期校验），因为它缺的不是架构，是**一条把反射结果送到前端的通路**。

---

## 1. 评估框架：什么叫"可发现"

"Skill 是可发现模式"这句话要能被评估，必须先拆成四条可判定的命题：

| 判据 | 命题 | 谁发现 | 发现成什么样 |
|---|---|---|---|
| **D1 自描述** | Skill 能否不读源码就说清"我要什么、我给什么、我是什么语义" | 研发/运维 | 端口、类型、必填、说明、绑定模式 |
| **D2 可渲染** | 设计器能否据此**自动**画出端口与配置表单，专家无需手写 | 专家 | 端口控件 + 参数面板 + 默认值 |
| **D3 可校验** | 配置期与执行期能否依据契约**拒绝错配**（缺参、类型不符、连错线） | 引擎 | 保存时报错 / 执行前拦截 |
| **D4 可演进** | 增删改 Skill 时契约能否同步、版本能否追溯、已发布规则是否失效 | 研发+专家 | 版本列 + 漂移检测 |

> 只有 D1 成立是"研发可发现"；D1+D2 成立才是"专家可发现"。**当前项目需要的是后者。**

---

## 2. 现状：Skill 契约有 6 个来源，2 套是死的

| # | 契约来源 | 谁生产 | 谁消费 | 状态 |
|---|---|---|---|---|
| ① | C# `[Skill]` / `[SkillParam]` + 方法签名 | 研发写代码 | `SkillExecutor.Analyze` 反射；`ValidateRequired` 运行期必填校验 | ✅ **唯一真正生效** |
| ② | `SkillExecutor.Analyze()` → `SkillMetadata`（含完整 `InputPorts`） | 反射自动 | **`ISkillRegistry.LoadAsync` 全仓零调用方** | ❌ **有产无出（死能力）** |
| ③ | `wf_skill` 主表（Code/Name/Description/CategoryCode/SkillType/PromptTemplate） | 种子脚本 + 前端单表 CRUD | 设计器 `loadSkills()` —— **只取名称与分类** | ⚠️ **仅展示用** |
| ④ | `wf_skill_input`（表单模板，注释"非硬校验"）/ `wf_skill_output`（注释"output_strict=1 时强校验"） | 人工/种子 | `SkillDetailDto.Inputs/Outputs` —— **该 DTO 全仓零使用方** | ❌ **死表** |
| ⑤ | `wf_skill_reflection`（class_path/method_name） | 种子脚本 | `CertSkillRegistry` 执行绑定 | ✅ 生效 |
| ⑥ | 前端 `specialNodes.ts` 硬编码（`inputPorts` + `panelSchema`） | 研发 | 设计器属性面板 | ⚠️ **生效，但只覆盖 8 个特殊节点** |

**关键观察**：`wf_skill_output` 注释里承诺的"强校验"在新架构**没有实现**——`output_strict` 的校验逻辑只存在于已冻结的旧代码（`src/old/.../SkillBase.cs:110`），新架构 `SkillExecutor` 中 grep 无此实现。

### 2.1 三条死链（代码级）

| 链路 | 断点 | 证据 |
|---|---|---|
| 反射元数据 → 前端 | `ISkillRegistry.LoadAsync` 仅接口声明，无实现调用、无 Controller 暴露 | `ISkillRegistry.cs:14` |
| DB 端口契约 → 任何消费者 | `WfSkillInput`/`WfSkillOutput` 仅实体 + DTO 声明 | `WfSkillInput.cs`、`WfSkillOutput.cs`、`SkillDetailDto.cs` |
| 输出契约 → 运行期校验 | `Skill.OutputStrict` 属性存在但引擎不读 | `Skill.cs:61` vs `SkillExecutor.cs` |

---

## 3. 逐条判据打分

### D1 自描述 —— ✅ 达成（但出口被掐断）

`SkillAttribute` 提供 `Code/Name/ReturnType/Description`，`SkillParamAttribute` 提供 `Description/BindMode/EnumSource`，反射还能推导 `Required = !p.HasDefaultValue` 与 `DefaultValue`。

> **这套元数据对 D1 来说是充分的**。问题不在"描述得不够"，在"描述没人取"。

### D2 可渲染 —— ❌ 未达成

推断链条（三处串联）：

1. `WorkflowDesigner.loadSkills()` → `POST /api/Workflow/WfSkill/filter` → 返回 `wf_skill` 行。**该表无端口列**，故 `skills[]` 中无 `inputPorts`。
2. `useWorkflowStore.addNode()`：`inputPorts = item.inputPorts?.length ? item.inputPorts : (meta?.inputPorts || [])`
   - `item.inputPorts` = undefined（见 1）
   - `meta = getSpecialNode(classCode)`，`specialNodes` 的 classCode 全集 = `start / end / branch / ai_node / constant / loop / docField / docTable` —— **不含 `compare`/`get_field`/`get_table`/`assemble`/`llm_extract`**
   - → **普通 skill 节点 `inputPorts = []`**
3. `NodePropertyForm` 两段渲染条件：
   - `v-if="visibleInputPorts.length > 0"` → 不渲染"输入端口"
   - `visiblePanelSchema = specialMeta?.panelSchema || []` → `v-if="visiblePanelSchema.length > 0"` → 不渲染"参数配置"

> **推断结论**：反射型 Skill 拖入画布后，属性面板仅剩「节点名称 / 节点类型 / 描述 / 测试」。
> **复核方法**：打开 `nc-config` 设计器 → 拖入「值比较」→ 观察右侧面板是否出现输入端口与参数区。若实测有端口，则说明存在我未读到的回填逻辑，请回填此结论并定位该逻辑（全仓 grep 未见 `Analyze`/`LoadAsync` 的前端调用）。

### D3 可校验 —— ⚠️ 部分达成

| 场景 | 现状 | 判定 |
|---|---|---|
| 执行期缺必填参 | `SkillExecutor.ValidateRequired` 按方法签名拦截，写入 `standardOutputs["error"]` | ✅ 有效 |
| 设计期端口类型/连线校验 | 无（`compiler.ts` 不校验端口类型） | ❌ |
| 保存期契约校验 | 无（`WfSkillController` 为裸 `YzhControllerBase`，仅 Code 唯一性） | ❌ |
| 输出端口校验 | 无（`wf_skill_output` 未接入） | ❌ |

### D4 可演进 —— ❌ 未达成

`wf_skill` 无版本列；改 C# 方法签名后，已保存规则的 `inputs` **静默漂移**（缺参只在执行时以 `error` 形式出现，而非设计期报错）；`wf_skill_reflection` 5 分钟缓存可自动刷新，但契约变化无检测。

---

## 4. 分层合理性判断（回答"这套机制是否合理"）

### 4.1 基建层：✅ 合理，且已基本成型

| 基建该保证的 | 现状 |
|---|---|
| DAG 解析、依赖推导 | ✅ `WorkflowConfigParser` / 隐式依赖图 |
| 任务派发、队列、重试、超时 | ✅ `wf_execution_task` + queue 页 + `wf_node_execution` |
| 节点类型 → 执行器路由 | ✅ `NodeExecutor` switch（缺 `logic` 一处） |
| 执行历史、回放、缓存 | ✅ `wf_node_execution` / `sharedOutputs` / IMemoryCache |
| 状态机与持久化 | ✅ V3 三层任务模型 |

> **基建不需要为 Skill 的语义负责** —— 这个边界划得对，不要动它。

### 4.2 能力层：⚠️ 方向对，契约太薄且未贯通

Skill 契约当前只回答"**怎么调**"（类名、方法名、参数名），没回答"**是什么/给什么/变了会怎样**"：

| Skill 应自描述但未自描述的 | 影响 |
|---|---|
| 输出端口与类型 | 设计器无法画输出、无法做连线类型校验 |
| 语义/用途（给专家看） | 专家只能看 `wf_skill.Description` 一句话 |
| 确定性 / 是否调 AI / 是否有副作用 | 引擎无法据此决定重试、超时、缓存策略 |
| 成本（token/耗时） | 无法按 Skill 做配额与费用归集 |
| 置信度是否可信（AI 类） | 与 `规则Skill与证据链` 的三级处置无法对接 |
| 版本 | 改契约后老规则静默漂移 |

### 4.3 关于"可发现模式"这个抽象本身

"可发现"要成立，须明确**四种不同的发现**，当前只做到第一种：

| 发现类型 | 主体 → 客体 | 现状 |
|---|---|---|
| 引擎发现 Skill | 执行期按 `skillCode` 找到实现 | ✅ `wf_skill_reflection` + DI 回退 |
| **专家发现 Skill** | 配置期看到能力清单与契约 | ⚠️ **只见名称，不见契约** |
| Skill 发现数据 | 运行期定位字段/表格/段落 | ⚠️ 能取值，**不能取证据**（见证据链文档 §4.3） |
| 规则发现自身结论 | 裁决期产出违规/通过/未知 | ❌ **缺 `verdict` 出口**（P0） |

> 结论：**"可发现模式"是合理的抽象，但当前实现只覆盖了"引擎→Skill"这一跳。** 专家侧的发现依赖一条未打通的通路。

---

## 5. 量化度量：新增一个 Skill 要改几处？

| # | 编辑点 | 是否可自动推导 | 现状 |
|---|---|---|---|
| 1 | 新建 `Skills/XxxSkill.cs`（`[Skill]` + `[SkillParam]` + `static ExecuteAsync`） | — | 必须手工 |
| 2 | `wf_skill` 插行（Name/Category/Description） | ✅ 可由 `[Skill]` 推导 | **手工** |
| 3 | `wf_skill_reflection` 插行（class_path/method_name） | ✅ 可由反射扫描推导 | **手工** |
| 4 | 前端 `specialNodes.ts` 加 `inputPorts` + `panelSchema`（否则面板为空） | ✅ 可由 `Analyze()` 推导 | **手工（冗余最痛）** |
| 5 | 文档 / Skill 清单登记 | ⚠️ 半自动 | 手工 |

**= 5 个编辑点，其中 2、3、4 是同一份信息的三次重复录入。**

> 这是评估"可发现"是否成立的**最直观指标**：目标应是 **1 个编辑点（只写 C# 类），2/3/4 全部自动同步**。
> 实现手段：启动时反射扫描 → upsert `wf_skill` + `wf_skill_reflection` → 提供 `GET /api/Workflow/WfSkill/{code}/contract` 返回 `SkillMetadata` → 前端拖入节点时按 `skillCode` 拉取端口。

---

## 6. 缺口清单（按优先级）

| 优先级 | 缺口 | 动作 | 解锁 |
|---|---|---|---|
| **P0-1** | 反射契约无出口 | 新增 `GET /api/Admin/Workflow/WfSkill/metadata`（内部调 `ISkillRegistry.LoadAsync` + `[Skill]` 反射索引）；前端 `loadSkills` 同步拉取并建双键索引（Code + SkillCode） | D1→D2 通路 ✅ 2026-10-10 已交付 |
| **P0-2** | 拖入节点端口为空 | `addNode` 时若 `item.inputPorts` 空则从索引回填；`renderWorkflow` 后对已保存节点调用 `applySkillPorts` 回灌，避免老规则永久缺参数 | 专家能配参数 ✅ 2026-10-10 已交付 |
| **P0-3** | 违规裁决出口缺失 | `verdict` Skill + `AggregateNcResult` 三态分离（见证据链文档 §5） | NC 检测成立 |
| **P1-1** | 设计期无契约校验 | 保存时校验必填端口、端口类型与连线类型匹配 | D3 |
| **P1-2** | 新增 Skill 5 个编辑点 | 启动时反射扫描自动 upsert ②③ | 单一编辑点 |
| **P1-3** | 死表 `wf_skill_input/output` | 二选一：**接入**（作为契约的 DB 覆写层，供专家改中文标签）或**删除**（避免误导） | 消除双真相源 |
| **P2-1** | Skill 无版本/演进标记 | 加 `Version` + `DeprecatedAt`；契约变更时标记受影响规则 | D4 |
| **P2-2** | Skill 缺运行时属性 | 加 `Determinism / CallsAi / SideEffect / CostHint` 字段，引擎据此选重试与超时策略 | 基建对能力的反向感知 |

> **P1-3 是一个必须做的决策**：`wf_skill_input/output` 现在是"看着像契约、实际无人读"的状态，最容易误导后续维护者。**要么接通，要么删掉**，不能留。

---

## 7. 建议的目标形态（最小闭环）

一个 Skill 的契约应该走完这一圈：

```
 ① 研发写 C# 类 [Skill]+[SkillParam]
        ↓ （启动时反射扫描，自动）
 ② upsert wf_skill / wf_skill_reflection
        ↓ （GET /contract，实时）
 ③ 设计器拖入 → 自动画端口 + 参数表单
        ↓ （保存时校验）
 ④ 必填/类型/连线不匹配 → 拒绝保存并指出具体端口
        ↓ （执行期）
 ⑤ ValidateRequired + 输出契约校验 → 失败进 wf_node_execution
        ↓ （裁决期）
 ⑥ verdict → 三态 + 证据包 → compliance_check / NC / 报告
        ↓ （演进期）
 ⑦ 版本变更 → 标记受影响规则 → 专家确认后失效或升级
```

**当前状态：① ②（手工）⑤（部分）已通；③ ④ ⑥ ⑦ 断。**

---

## 8. 与《规则Skill与证据链-功能设计 V1》的分工

| 文档 | 管什么 | 不管什么 |
|---|---|---|
| **本文档** | 机制是否合理、契约通路怎么打通、Skill 元数据要补哪些**属性** | 不定义具体 Skill 干什么 |
| **规则Skill与证据链** | 要新增哪些 Skill（`ai_normalize`/`ai_judge`/`verdict`…）、证据对象长什么样、节点三要素 | 不管契约怎么发现与校验 |
| **工作流引擎总体架构 V3** | 基建：任务模型、队列、状态机 | 不管 Skill 语义 |

**衔接点**：本文档 §6 的 P0-1/P0-2 打通后，`规则Skill与证据链` 新增的 11 个 Skill 才能**自动**出现在设计器里 —— 否则每个新 Skill 都要再改一次 `specialNodes.ts`（5 个编辑点问题会在新增 11 个 Skill 时放大为 55 次手工录入）。

---

## 9. 职责边界

| 层 | 归属 | 内容 |
|---|---|---|
| 契约元数据的**生产** | 研发 | `[Skill]`/`[SkillParam]` 特性完备度 |
| 契约通路的**打通** | 研发 | `/contract` 端点、前端回填、反射扫描 upsert |
| 设计期**校验规则** | 研发 | 必填/类型/连线匹配的算法 |
| 契约中的**中文标签、描述、枚举值** | 专家/维护 | 可在 DB 层覆写（若 P1-3 选择"接入"） |
| Skill 的**业务语义与阈值** | 专家/维护 | 见 `规则Skill与证据链` §9 |
| 基建的队列/重试/状态机 | 研发 | 不因 Skill 语义而改动 |

---

## 10. 实施记录（P0-1 / P0-2 已于 2026-10-10 落地）

### 问题复盘
2026-09-27 评估时抓到的两处死链：
- **P0-1** `ISkillRegistry.LoadAsync` 零调用方 → 反射元数据出不去；
- **P0-2** `loadSkills` 只取 `wf_skill` 行，前端 `inputPorts=[]` → 拖入的 Skill 节点属性面板无端口。

### 实现方案（方案 A · 反射元数据）
**后端**（`CertPlatform.Admin/Controllers/Workflow/WfSkillController.cs`）
- 新增 `GET /api/Admin/Workflow/WfSkill/metadata`。
- 解析顺序：
  1. `[Skill]` 特性静态索引（`BuildSkillIndex`，进程内 `Lazy<IReadOnlyDictionary>`）→ `_skillExecutor.Analyze`。
  2. 回退：`ISkillRegistry.LoadAsync`（DB `wf_skill_reflection`）。
- 返回 DTO：`{ Code, SkillCode, Name, Description, InputPorts: [{Name, Label, Type, Required, DefaultValue, Description, BindMode, EnumSource}] }`。
- 双键索引（`Code` + `SkillCode`）兼容历史画布可能存的 Code 值。

**前端**（`cert-share/.../WorkflowDesigner.vue`）
- `loadSkillMetadata()`：调 metadata 端点，建双键索引 `skillPortIndex`。
- `loadSkills()`：`skillCode = s.SkillCode || s.Code`（修复 `SK_*` 类技能反射查不到的隐性 bug）；把 `inputPorts` 挂到 item 上。
- `applySkillPorts(nodes)`：`renderWorkflow` 后对已保存节点回灌 `inputPorts`，避免存量规则永久缺参数。

### 验证结果
- 后端：编译 0 错误；`GET /metadata` 返回 18 个 Skill、17 个含端口（`is_std_file_missing`=4 端口，`assemble`/`compare` 等全部命中）。
- 前端：`vue-tsc --noEmit` 通过；`curl /@fs/.../WorkflowDesigner.vue` 返回 200（Vite 能编译）。
- 守卫：`node scripts/guards.mjs` 仍报 32 处违规，但均为 **未在本任务中修改的文件**（queue/index.vue、MenuParentPicker.vue、CertOrgStage.cs 等）；我的改动 WfSkillController.cs / WorkflowDesigner.vue / 迁移 SQL 均未出现在违规列表里。
- 测试：`vitest run` 全仓 347 用例通过。

### 后续可考虑
- P1-3（`wf_skill_input/output` 二选一）待裁决：若专家需要修改中文标签/必填，可「接入」作为契约覆写层；否则「删除」避免误导。
- P1-1（保存期端口类型校验）按 §7 目标形态需补。
- P1-2（反射 upsert 启动自动同步）减少编辑点，但需额外处理废弃 Skill 清理。

---

## 附录 A：代码位置索引（本文所有结论的出处）

| 结论 | 位置 |
|---|---|
| `Analyze()` 产出完整端口元数据 | `CertPlatform.Admin/Services/Workflow/Skills/SkillExecutor.cs:116-160` |
| `LoadAsync` 零调用方 | `CertPlatform.Admin/Services/Workflow/ISkillRegistry.cs:14` |
| 运行期必填校验（唯一生效的契约校验） | `SkillExecutor.cs:75-78`、`:231-249` |
| 设计器技能列表只取 `wf_skill` 行 | `cert-share/src/components/workflow/WorkflowDesigner.vue:308-321` |
| 端口 fallback 逻辑 | `cert-share/src/composables/workflow/useWorkflowStore.ts:100-101` |
| 特殊节点 classCode 全集（不含反射型 Skill） | `cert-share/src/composables/workflow/specialNodes.ts:61-320` |
| 属性面板两段渲染条件 | `cert-share/src/components/workflow/NodePropertyForm.vue:15`、`:98` |
| `WfSkillController` 为裸单表 CRUD | `CertPlatform.Admin/Controllers/Workflow/WfSkillController.cs:23` |
| `SkillDetailDto` 零使用方 | `CertPlatform.Shared/Entities/Wf/SkillDetailDto.cs:25-26` |
| `output_strict` 校验只存在于旧代码 | `src/old/server/Vue.NetCore/vol.api/YZH.Core/WorkFlow/SkillBase.cs:110` |
| `wf_skill_input/output` 表注释与结构 | `scripts/db/all_tables_ddl.sql:2828-2892` |
| Skill 种子（5 个） | `scripts/db/20260916_wf_skill_reflection_seed.sql` |

## 附录 B：待实测复核项

| # | 待复核 | 复核方法 | 若结论不同则影响 |
|---|---|---|---|
| B1 | 反射型 Skill（如「值比较」）拖入设计器后，属性面板是否真的无输入端口/参数区 | 打开 `workflow/nc-config` 设计器实拖 | 若有端口 → 找到未被 grep 到的回填逻辑，D2 改判"部分达成" |
| B2 | 已保存规则 JSON 中是否带有历史遗留的 `inputPorts` | 查看任一已存规则的 `rule_json.nodes[]` | 若带 → 存量规则可配，仅新拖节点不可配 |
