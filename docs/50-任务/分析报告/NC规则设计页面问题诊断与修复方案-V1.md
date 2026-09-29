# NC 规则设计页面问题诊断与修复方案

> **诊断对象**：`http://127.0.0.1:9990/business/nc-config`（菜单「NC 规则设计」，路由 `business/nc-config`）
> **诊断方式**：源码静态分析 + 后端接口实测（curl 带 Token）+ 浏览器实测（真实登录、点击、拖拽、截图）
> **关联文档**：`项目全局规则.md` §16.9 / §7.3 / §8.2 / §九；`docs/50-任务/开发计划/工作流设计器通用组件抽象与重构方案-V1.md`
> **日期**：2026-09-22

---

## 〇、结论摘要

两个上报问题**均已复现并定位到根因**，且根因不限于本页面，属于 **YZH 新架构「PascalCase 契约」与「camelCase 旧代码」的迁移残留**。

| # | 现象 | 根因编号 | 根因一句话 | 优先级 |
|---|------|---------|-----------|--------|
| 1 | 左树 NC 检查项不显示 | **P0-1** | `designer.vue` 的 `treeConfig.textField/codeField` 写成 camelCase（`ruleName`/`ruleCode`），而后端 `/api/ValidationRule/filter` 返回 PascalCase（`RuleName`/`RuleCode`），叶子节点取值为 `undefined` → 渲染为空行 | P0 |
| 2 | 保存布局后切换丢失 | **P0-2** | `WorkflowDesigner` 用 `{ ...leaf }` 浅拷贝持有 `currentLeaf`，保存成功后只回写了拷贝对象，树里缓存的原始叶子仍是旧值；同会话切走再切回即回滚 | P0 |
| 2 | 同上 | **P0-3** | 画布未监听 `node:drop`，拖拽节点后 `store.nodes[].x/y` 不更新，`store.dirty` 也不置位 → **保存按钮保持禁用，用户根本无法保存自己排好的布局** | P0 |
| 2 | 同上 | **P0-4** | `autoLayout()` 用 `setProperties(id, {x,y})` 试图移动节点，而 LogicFlow 的 `setProperties` 只写 `properties`，不移动坐标 → 画布不动但 store 已改，保存下来的坐标与所见不一致 | P0 |

**连带故障（同一根因衍生，一并修复）**：

| 位置 | 现象 | 根因 |
|------|------|------|
| 画布标题 | 永远显示「工作流：未命名」 | P0-1 |
| 保存确认框 | 显示「保存工作流到「undefined」？」 | P0-1 |
| 底部规则编码 | 始终为空 | P0-1 |
| 树节点「已配置」标记 | 已配置的规则不显示绿色对勾（`.configured` 失效） | P0-1（`leaf.workflowConfig` 不存在，实际字段是 `RuleJson`） |
| 保存动作 | **把已禁用的规则强制改回启用** | P0-1（`IsActive: r.isActive !== false`，`r.isActive` 恒为 `undefined`） |

---

## 一、诊断环境与证据链

### 1.1 代码定位

| 层 | 文件 | 说明 |
|----|------|------|
| 路由 | `src/certplatform-web/cert/cert-admin/src/router/index.ts` | `business/nc-config` → `pages/workflow/nc-config/designer.vue` |
| 页面（薄包装） | `src/certplatform-web/cert/cert-admin/src/pages/workflow/nc-config/designer.vue` | 78 行，只传 `treeConfig` / `saveConfig` / `executeConfig` |
| 设计器（共享） | `src/certplatform-web/cert/cert-share/src/components/workflow/WorkflowDesigner.vue` | 947 行，树 + 画布 + 保存全在这里 |
| 状态层 | `src/certplatform-web/cert/cert-share/src/composables/workflow/useWorkflowStore.ts` | `moveNode()` 存在但**零调用点** |
| 序列化 | `src/certplatform-web/cert/cert-share/src/composables/workflow/serializer.ts` | `extractLayout()` 从 `store.nodes[].x/y` 取坐标 |
| 后端 | `src/certplatform-api/CertPlatform.Admin/Controllers/Workflow/ValidationRuleController.cs` | `[Route("api/ValidationRule")]`，继承 `YzhControllerBase<ValidationRule>` |
| 实体 | `src/certplatform-api/CertPlatform.Shared/Entities/Cert/ValidationRule.cs` | `RuleName` / `RuleCode` / `RuleJson` / `LayoutJson` / `IsActive` |
| 组织树 | `CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs` | `GetOrganizationTreeAsync()` 返回 **camelCase** 字典（`id/label/cbCode/stdCode/phaseCode`） |

> 注意：**组织树是 camelCase，规则列表是 PascalCase**——这是本项目最容易踩的混合契约点。

### 1.2 实测证据（全部可复现）

| # | 操作 | 实测结果 | 结论 |
|---|------|---------|------|
| E1 | `POST /api/ValidationRule/filter`（OrgCode+StandardCode+PhaseCode=AP） | 返回 `success:true`，`data.Items` 长度 **2**，字段为 `RuleName` / `RuleCode` / `RuleJson` / `LayoutJson`（PascalCase） | 后端数据正常，命名是 PascalCase |
| E2 | 页面展开「河北雄安尚龙认证有限公司 → ISO 13485:2016 → AP - 申请受理」 | 阶段徽标出现 `2`，DOM 中生成 2 个 `.tree-node.level-3`，但 `innerText === ""`（**完全空文本**） | 问题一复现：数据到了，标签取值为 undefined |
| E3 | 点击第 1 个空白叶子 | 画布标题 = 「工作流：**未命名**」；底部规则编码为空 | P0-1 连带 |
| E4 | 点击「保存」 | 确认框文案 = 「保存工作流到「**undefined**」？」 | P0-1 连带 |
| E5 | 点击「布局」 | footer 由「已保存」变「**未保存**」；但节点屏幕坐标 **前后完全一致**（598,272 / 598,281 / 675,449）；DB `LayoutJson` 被写成 `120/360/600` | 问题二根因 C 复现：所见 ≠ 所存 |
| E6 | 鼠标拖拽「结束」节点 | 画布边 polyline 由 `410,80 550,80` 变为终点 `650,130`（**节点确实动了**）；footer 仍是「**已保存**」；「保存」按钮 `disabled: true` | 问题二根因 B 复现：拖动不置脏，**用户无法保存** |
| E7 | 保存后切到第 2 个叶子再切回第 1 个 | 画布仍是**旧坐标**（598,272 / 598,281 / 675,449），DB 里却是新坐标 `120/360/600` | 问题二根因 A 复现：会话内缓存未回写 |
| E8 | **刷新整页**后重新展开、重新选中同一叶子 | 边 polyline = `170,80 310,80` / `410,80 550,80`，即 DB 中的 `120/360/600` | **持久化本身是好的**，问题 100% 出在前端会话态 |

> E7 与 E8 的对照是本次诊断的关键：**刷新能恢复 → 说明落库链路没问题 → 排除后端，锁定前端缓存与同步**。

### 1.3 已还原的测试数据

诊断过程中 E5/E6 改动了 `cert_validation_rule` 中 `NC-…-001`（测试规则1）的 `LayoutJson`，**已还原为诊断前的原值**：

```json
{"nodePositions":{"start_n1":{"x":100,"y":150},"ai_node_n1":{"x":100,"y":149.40625},"end_n1":{"x":177.01385498046875,"y":327.3315887451172}}}
```

---

## 二、问题一：左树「NC 检查项」不显示

### 2.1 现象

左树 `机构 / 标准 / 阶段 / NC检查项` 四级中，第 4 级（NC 检查项）**只渲染出空行**——有图标、有 hover 效果、可点击，但**没有任何文字**，视觉上等同于「不显示」。

### 2.2 根因：P0-1 字段大小写失配

**文件**：`src/certplatform-web/cert/cert-admin/src/pages/workflow/nc-config/designer.vue:19-20`

```ts
textField: 'ruleName',     // ❌ 后端实际返回 RuleName
codeField: 'ruleCode'      // ❌ 后端实际返回 RuleCode
```

**取值链**（`WorkflowDesigner.vue`）：

```ts
// line 346 —— 构造叶子节点
phase.children = items.map((item: any) => ({
  ...item,
  id: item.Code || item.code || item.RuleCode || item.ruleCode,
  _label: item[textField] || item.label          // item['ruleName'] → undefined
}))

// line 70 —— 渲染标签
{{ leaf._label || leaf[treeConfig.textField] || leaf.label }}
//  undefined      undefined                      undefined  → 渲染空字符串
```

`item` 是 `/api/ValidationRule/filter` 原样返回的实体，字段为 `RuleName`（PascalCase）。三处兜底全部落空，标签渲染为空。

### 2.3 依据：违反 `项目全局规则.md` §16.9 铁律七

> **铁律七：DB 列名 / C# 属性名 / JSON 字段名「原样一致」，禁止局部改写**
> 「JSON 是投影，不是新定义」——前端消费实体字段时**不得**自行转换大小写。
> 违规判定：「为对齐风格而在实体上新增映射」「按模块自定风格」均视为违反本条。

**因此修复方向是唯一的：改前端，改后端。** 不得通过给后端加 `[JsonPropertyName("ruleName")]` 或加 camelCase DTO 来"兼容"。

### 2.4 修复

```diff
// designer.vue
-      textField: 'ruleName',
-      codeField: 'ruleCode'
+      // 后端 /api/ValidationRule/filter 返回 PascalCase 实体（项目全局规则 §16.9 铁律七）
+      textField: 'RuleName',
+      codeField: 'RuleCode'
```

同时修正 `saveConfig.buildPayload` 中的 `IsActive`（同一根因，会静默篡改数据）：

```diff
-          IsActive: r.isActive !== false,
+          IsActive: r.IsActive !== false,   // 兜底 r.isActive ?? r.IsActive ?? true
```

> ⚠️ 该行现状为 `r.isActive !== false` → `undefined !== false` → **恒为 `true`**。
> 后果：对任何 `IsActive = 0` 的规则点一次「保存」，规则会被**静默启用**。

---

## 三、问题二：保存布局后切换丢失

### 3.1 现象

用户把节点排好 → 点保存 → 切到别的检查项 → 切回来，**布局回到保存前（甚至被清空）**。

### 3.2 根因 A：P0-2 保存成功后未回写树叶子缓存

**文件**：`WorkflowDesigner.vue:353` 与 `:776-777`

```ts
// line 353 —— 浅拷贝！currentLeaf 与 phase.children[i] 从此是两个对象
currentLeaf.value = { ...leaf }

// line 776-777 —— 保存成功后只写了拷贝对象
currentLeaf.value.workflowConfig = JSON.stringify(config)
currentLeaf.value.layoutJson = JSON.stringify(layout)
```

切走再切回时，`selectLeaf(leaf)` 拿到的是 `phase.children` 里那个**从未被更新过的原始对象**：

```ts
// line 369
renderWorkflow(
  leaf.workflowConfig || leaf.WorkflowConfig || leaf.ruleJson || leaf.RuleJson,  // 仍是 null
  leaf.layoutJson || leaf.LayoutJson || null                                      // 仍是 null
)
// → renderWorkflow(null, null) → clearCanvas() + ensureStartNode() → 用户的工作流消失
```

**E7 已验证**：切回后画布坐标是旧值，而 DB 是新值。

### 3.3 根因 B：P0-3 拖动节点未同步 store（最严重）

`createLogicFlowInstance()`（`WorkflowDesigner.vue:382-452`）注册的事件只有：

```
node:click / node:dbclick / edge:click / blank:click / edge:add / edge:delete
```

**没有任何 `node:drop` / `node:drag` 监听**，全项目 `store.moveNode()` 调用点数为 **0**。

后果链：

1. 用户拖动节点 → LogicFlow 内部坐标变了（画布上看得见）；
2. `store.state.nodes[].x/y` **不变** → `extractLayout()` 取到的是拖动前的旧坐标；
3. `store.dirty` **不置位** → 「保存」按钮 `:disabled="!currentLeaf || !store.state.dirty"` **恒为禁用**（E6 实测 `disabled: true`）。

**即：用户根本无法保存自己排好的布局。** 这是「布局信息没有保存」最直接的成因。

### 3.4 根因 C：P0-4 `autoLayout()` 移动节点无效

**文件**：`WorkflowDesigner.vue:727`

```ts
for (const n of nodes) {
  if (posMap[n.id]) diagram.value.setProperties(n.id, { x: posMap[n.id].x, y: posMap[n.id].y })
}
```

LogicFlow 的 `setProperties(id, props)` 只把键值写进 `node.properties`，**不会移动节点坐标**。
正解是 `graphModel.moveNode2Coordinate(nodeId, x, y, isIgnoreRule)`（LogicFlow 2.2.5 `GraphModel.d.ts:265`）。

**E5 已验证**：点「布局」后 footer 变「未保存」（store 改了），但画布节点屏幕坐标一像素未动，DB 却写入了新坐标 → **保存下来的布局与用户所见完全不同**。

> 补充：该缺陷是**从历史项目原样搬迁**的（`src/old/.../NCConfig/index.vue:1545-1553` 同样是 `setProperties({x,y})`），属于历史遗留，不是本次迁移新引入。

### 3.5 根因 D：P1-1 缺少规则详情接口（契约缺口）

历史项目有专用详情接口，新架构没有：

| 能力 | 历史项目 | 当前新架构 |
|------|---------|-----------|
| 列表 | `GET api/validation-rule/list?orgCode&standardCode&phaseCode` | `POST /api/ValidationRule/filter`（通用） |
| **详情** | `GET api/validation-rule/{ruleCode}` | **无** |
| 保存 | `POST api/validation-rule` | `POST /api/ValidationRule/update` |

后果：列表接口把 `RuleJson`（整份工作流 JSON）+ `LayoutJson` 一起返回，**每行规则都携带完整工作流定义**，列表载荷随规则数量线性膨胀；且前端只能依赖列表数据，缺少"选中即拉最新详情"的能力（历史项目正是靠详情接口规避了缓存陈旧问题）。

### 3.6 修复

| 编号 | 改动 | 文件 |
|------|------|------|
| P0-2 | 保存成功后**回写原叶子对象**（保持引用而非浅拷贝） | `WorkflowDesigner.vue` |
| P0-3 | 监听 `node:drop` → `store.moveNode(id, x, y)` + 置脏；保存前兜底 `syncPositionsFromDiagram()` | `WorkflowDesigner.vue` |
| P0-4 | `setProperties({x,y})` → `graphModel.moveNode2Coordinate(id, x, y)` | `WorkflowDesigner.vue` |
| P1-1 | 后端补 `GET /api/ValidationRule/{code}`；前端 `selectLeaf` 懒加载详情 | `ValidationRuleController.cs` + `WorkflowDesigner.vue` |
| P1-2 | 切叶子/切阶段前做未保存校验（`store.dirty` 时二次确认） | `WorkflowDesigner.vue` |

---

## 四、与历史项目（`src/old`）的差异对照

**历史参照文件**：`src/old/server/Vue.NetCore/vol.web/src/views/cert/admin/business/Standard/NCConfig/index.vue`（1719 行）
**历史后端**：`src/old/server/Vue.NetCore/vol.api/YZH.WebApi/Controllers/Admin/Platform/ValidationRuleController.cs`

### 4.1 数据结构与命名（**问题一的分水岭**）

| 维度 | 历史项目 | 当前新架构 | 判定 |
|------|---------|-----------|------|
| 组织树字段 | camelCase（`id/label/cbCode/stdCode/phaseCode`） | camelCase（**未变**） | ✅ 一致 |
| 规则列表字段 | **camelCase**（`ruleName` / `ruleJson` / `layoutJson`） | **PascalCase**（`RuleName` / `RuleJson` / `LayoutJson`） | ⚠️ **契约变更** |
| 前端读取方式 | `rule.ruleName`（camelCase 一致） | `treeConfig.textField='ruleName'`（**未同步**） | ❌ **失配** |
| 序列化策略 | Vol 框架默认 camelCase | `YZH.Core.Stand` 显式 camelCase（`ApiResponse`）+ 实体 PascalCase | ⚠️ 混合 |

> **这就是问题一的本质**：新架构把实体 JSON 统一成 PascalCase（符合 §16.9），但页面配置层沿用了历史的 camelCase 字面量，两边没有同步。**同一份 `WorkflowDesigner` 组件被 `nc-config`（PascalCase 数据）与 `report-rule-config`（camelCase 数据）共用**，配置错误只在 nc-config 侧暴露。

### 4.2 数据加载链路

| 环节 | 历史项目 | 当前新架构 | 判定 |
|------|---------|-----------|------|
| 列表接口 | `api/validation-rule/list`（专用轻量） | `/api/ValidationRule/filter`（通用分页） | ⚠️ 载荷膨胀 |
| 详情接口 | `api/validation-rule/{ruleCode}` | **无** | ❌ 能力缺失 |
| 选中后行为 | `Object.assign(rule, d)` —— **回写树叶子对象** | 无回写（浅拷贝后丢弃） | ❌ **问题二根因 A** |
| 保存接口 | `POST api/validation-rule`（单接口） | `/update` + `/add` 双接口 | ⚠️ 契约变更（`getUrl` 已适配） |
| 保存后回写 | `currentRule.value.ruleJson = …`（`currentRule` **就是**树叶子对象） | `currentLeaf.value.xxx = …`（**是拷贝**） | ❌ **问题二根因 A** |
| 列表排序 | `OrderBy(x => x.RuleName)` | 无显式排序 | ⚠️ 体验降级 |

> **关键差异**：历史项目两处都保持了"树叶子对象 === 当前对象"的**引用同一性**（`currentRule.value = rule` 而非 `{...rule}`，且详情接口 `Object.assign(rule, d)` 回写）。新架构把 `currentLeaf.value = { ...leaf }` 改成浅拷贝，**破坏了引用同一性**，却没有补上回写动作。

### 4.3 画布坐标同步（**历史遗留缺陷，两代都有**）

| 能力 | 历史项目 | 当前新架构 | 判定 |
|------|---------|-----------|------|
| `node:drop` / `node:drag` 监听 | ❌ 无 | ❌ 无 | ⚠️ **历史遗留，被原样继承** |
| `autoLayout` 移动节点 | `setProperties({x,y})`（无效） | `setProperties({x,y})`（无效） | ⚠️ **历史遗留，被原样继承** |
| `store.moveNode()` 调用点 | 0 | 0 | ⚠️ 同 |
| 保存前从画布回读坐标 | ❌ 无 | ❌ 无 | ⚠️ 同 |

> 历史项目之所以"看起来没这个问题"，是因为它的**列表/详情链路自洽**（camelCase 一致 + 详情接口 + 叶子回写），用户切来切去看到的一直是最新数据；坐标同步缺陷被掩盖了。新架构引入 P0-1/P0-2 后，缺陷被同时放大。

### 4.4 组件架构差异（新架构的改进，需保留）

| 维度 | 历史项目 | 当前新架构 | 判定 |
|------|---------|-----------|------|
| 组件形态 | 1719 行单文件 | `WorkflowDesigner.vue`（947 行共享）+ `designer.vue`（78 行薄包装） | ✅ 架构更优 |
| 复用方 | 仅 NC | NC + 报告内容设计 | ✅ 收益明确 |
| 左/右栏折叠 | ❌ | ✅ | ✅ |
| `ensureSizeThenInit` 轮询 | ❌ | ✅ | ✅ |
| 初始空图 `render([])` | ❌ | ✅ | ✅ |
| 运行按钮 + 结果面板 | ✅ | ✅ | ✅ |

> **结论**：新架构的抽象方向正确，**不要回退到单文件**。缺陷集中在「配置契约」与「状态回写」两处，属可精确修复的局部问题。

---

## 五、与项目全局规则的对齐分析

| 规则条目 | 要求 | 当前状态 | 判定 |
|---------|------|---------|------|
| **§16.9 铁律七** | DB 列名 / C# 属性名 / JSON 字段名原样一致，禁止局部改写、禁止前端自定风格 | 前端 `treeConfig` 使用 `ruleName`/`ruleCode`，且 `IsActive` 未对齐 | ❌ **违反** |
| **§16.9 检查清单 #5** | 「是否新增了转换/映射层」必须为「否」 | `renderWorkflow`/`selectLeaf` 维护 `xxx \|\| Xxx` 双写兜底，实质是**非正式映射层** | ⚠️ 需收口 |
| **§7.3 三层同构** | Controller 路径 = API 文件路径 = Pages 路径 | `Controllers/Workflow/ValidationRuleController.cs` ↔ `api/workflow/nc-config.ts` ↔ `pages/workflow/nc-config/` | ✅ 一致 |
| **§8.2 前端架构** | 通用组件 → `yzh.vue.core`；业务组件 → `share`；页面 → `admin/pages` | `WorkflowDesigner` 位于 `cert-share/src/components/workflow/`，页面位于 `cert-admin/src/pages/workflow/` | ✅ 一致 |
| **§九 禁止事项** | 禁止调用旧架构控制器/服务；禁止修改 `src/old` | 未发生 | ✅ 合规 |
| **§7.5 技术决策偏好** | 约定大于配置；2 人能维护 | `treeConfig` 是"约定"，但**无契约校验**，字段写错静默失败 | ⚠️ 需补校验 |

**规则层面的行动项**：

1. 本次修复**必须**改前端对齐 PascalCase，**禁止**改后端迁就 camelCase（§16.9）。
2. `WorkflowDesigner` 的 `treeConfig` 契约需补**运行时字段存在性校验**，避免同类"静默空白"再次发生。
3. `renderWorkflow` / `selectLeaf` 的 `xxx || Xxx` 双写兜底应标注为**过渡兼容**并加注释，避免被后续开发误认为规范。

---

## 六、完整修复计划

### 6.1 修复清单

| 编号 | 优先级 | 文件 | 位置 | 改动摘要 | 预估 |
|------|--------|------|------|---------|------|
| F1 | **P0** | `cert-admin/src/pages/workflow/nc-config/designer.vue` | L19-20 | `textField: 'RuleName'`、`codeField: 'RuleCode'` | 5 min |
| F2 | **P0** | 同上 | L44 | `IsActive: r.IsActive !== false` | 2 min |
| F3 | **P0** | `cert-share/src/components/workflow/WorkflowDesigner.vue` | L353 / L776-777 | 保持叶子引用 + 保存后 `Object.assign` 回写原对象 | 30 min |
| F4 | **P0** | 同上 | L382-452 | 注册 `node:drop` / `node:drag` → `store.moveNode` + 置脏 | 40 min |
| F5 | **P0** | 同上 | L713-729 | `setProperties({x,y})` → `graphModel.moveNode2Coordinate` | 15 min |
| F6 | **P1** | 同上 | L352-370 / L762-783 | 新增 `syncPositionsFromDiagram()`，在保存前 / 切叶子前调用（兜底） | 30 min |
| F7 | **P1** | 同上 | L64 / L67-69 | `configured` 判定改用 `hasWorkflow(leaf)`（兼容 `RuleJson` / `workflowConfig`） | 10 min |
| F8 | **P1** | 同上 | L324-331 / L352 | 切阶段 / 切叶子前做 `store.dirty` 二次确认，防未保存丢失 | 25 min |
| F9 | **P1** | `CertPlatform.Admin/Controllers/Workflow/ValidationRuleController.cs` | 新增端点 | `GET /api/ValidationRule/{code}` 返回单条详情（含 RuleJson/LayoutJson） | 30 min |
| F10 | **P1** | `WorkflowDesigner.vue` | L332-349 | `loadLeavesForPhase` 增加 `textField` 缺失运行时告警；`selectLeaf` 优先拉详情 | 40 min |
| F11 | **P2** | `WorkflowDesigner.vue` | L297-320 | 左树搜索支持过滤叶子节点 | 20 min |
| F12 | **P2** | `ValidationRuleController.cs` | `FilterCore` | 默认按 `RuleName` 排序（对齐历史行为） | 10 min |
| F13 | **P2** | `designer.vue` | L28-31 | 移除 `Code: r.Code \|\| r.code \|\| r.ruleCode` 中的 `ruleCode` 兜底（会污染 `Code`） | 5 min |

### 6.2 代码补丁

#### F1 / F2 — `designer.vue`

```diff
     :tree-config="{
       loadApi: '/api/ValidationRule/filter',
       loadMethod: 'post',
       loadBodyBuilder: (filter) => ({ /* 不变 */ }),
-      textField: 'ruleName',
-      codeField: 'ruleCode'
+      // ⚠️ 后端 /api/ValidationRule/filter 返回 PascalCase 实体
+      //    依据：项目全局规则 §16.9 铁律七（DB列名 = C#属性名 = JSON字段名）
+      //    注意：组织树 organization-tree 是 camelCase，两者不同源，勿混用
+      textField: 'RuleName',
+      codeField: 'RuleCode'
     }"
```

```diff
         RuleJson: JSON.stringify(ctx.config),
         LayoutJson: JSON.stringify(ctx.layout),
         NcDescriptionTemplate: r.NcDescriptionTemplate || r.ncDescriptionTemplate,
-        IsActive: r.isActive !== false,
+        // 实体为 PascalCase；原写法 r.isActive 恒为 undefined，导致恒真、静默启用已禁用规则
+        IsActive: r.IsActive !== false,
         Remark: r.Remark || r.remark
```

```diff
       buildPayload: (ctx) => {
         const r = ctx.leaf
         return {
-          Code: r.Code || r.code || r.ruleCode,
+          Code: r.Code || r.code,
```

#### F3 — 保持叶子引用 + 保存后回写

```diff
+const currentPhase = ref<any>(null)
+
 async function selectLeaf(leaf: any, phase: any) {
-  currentLeaf.value = { ...leaf }
+  // 保持引用同一性：currentLeaf 必须 === phase.children[i]
+  // 否则保存后的回写会落到拷贝对象上，切走再切回读到旧值（P0-2）
+  currentLeaf.value = leaf
+  currentPhase.value = phase
```

```diff
     if (res?.success !== false) {
       savedTip.value = `${new Date().toLocaleTimeString()} 已保存`
-      currentLeaf.value.workflowConfig = JSON.stringify(config)
-      currentLeaf.value.layoutJson = JSON.stringify(layout)
+      const cfgStr = JSON.stringify(config)
+      const layoutStr = JSON.stringify(layout)
+      // 回写「树里那个对象」（currentLeaf 已是引用），并同时覆盖 PascalCase / camelCase 两套键
+      Object.assign(currentLeaf.value, {
+        RuleJson: cfgStr, LayoutJson: layoutStr,
+        workflowConfig: cfgStr, layoutJson: layoutStr,
+      })
       store.markClean()
```

#### F4 — 拖动同步 + 置脏

```diff
   diagram.value.on('edge:add', (_event: any) => { autoSetBranchHandle(_event.data); onEdgeChange() })
   diagram.value.on('edge:delete', (_event: any) => { onEdgeChange() })
+
+  // ── 节点拖动结束 → 同步回 store（否则 store.x/y 与画布脱节，dirty 不置位） ──
+  // LogicFlow 2.2.5 事件：node:dragstart / node:drag / node:drop，payload = { e, data }
+  const onNodeMove = ({ data }: any) => {
+    if (!data?.id) return
+    store.moveNode(data.id, data.x, data.y)      // 内部 markDirty()
+    const n = store.getNodeById(data.id)
+    if (n) { n.x = data.x; n.y = data.y }
+  }
+  diagram.value.on('node:drop', onNodeMove)
+  diagram.value.on('node:drag', onNodeMove)
+
   document.addEventListener('keydown', handleKeyDown)
```

#### F5 — `autoLayout` 真正移动节点

```diff
   store.markDirty()
   for (const n of nodes) {
-    if (posMap[n.id]) diagram.value.setProperties(n.id, { x: posMap[n.id].x, y: posMap[n.id].y })
+    const p = posMap[n.id]
+    if (!p) continue
+    // setProperties 只写 node.properties，不会移动节点！
+    // 必须用 graphModel.moveNode2Coordinate（LogicFlow 2.2.5 GraphModel.d.ts:265）
+    const gm = diagram.value?.graphModel
+    if (gm?.moveNode2Coordinate) gm.moveNode2Coordinate(n.id, p.x, p.y)
+    else if (gm?.moveNode) gm.moveNode(n.id, p.x - (n.x || 0), p.y - (n.y || 0))
   }
   ElMessage.success('自动布局完成')
```

#### F6 — 保存前兜底回读画布坐标

```ts
/** 以画布为准，把节点坐标回写 store（保存 / 切换前调用，兜底任何未捕获的位移） */
function syncPositionsFromDiagram() {
  const gd = diagram.value?.getGraphData()
  if (!gd?.nodes?.length) return
  for (const gn of gd.nodes) {
    const sn = store.getNodeById(gn.id)
    if (sn && (sn.x !== gn.x || sn.y !== gn.y)) {
      sn.x = gn.x; sn.y = gn.y; store.markDirty()
    }
  }
}
```

调用点：`handleSave()` 中 `extractLayout()` **之前**；`selectLeaf()` 切走**之前**。

#### F9 — 后端补详情接口

```csharp
/// <summary>获取单条规则详情（含工作流定义与布局）</summary>
[HttpGet("{code}")]
public async Task<ActionResult<ApiResponse<ValidationRule>>> GetByCode(string code)
{
    var result = await Entity.GetOne(r => r.Code == code || r.RuleCode == code);
    if (!result.Success || result.Data == null)
        return NotFound(ApiResponse.Fail("规则不存在"));
    OnQueried(new PagedResult<ValidationRule>(new[] { result.Data }, 1, 1, 1));
    return Ok(ApiResponse<ValidationRule>.Ok(result.Data));
}
```

前端 `selectLeaf` 改为：先渲染列表数据（秒开），再异步拉详情覆盖（保证最新）。

### 6.3 实施顺序

```
第 1 批（P0，必须连续做完，否则问题 1 修好但问题 2 仍在）
  F1 → F2 → 验证问题一（左树出现规则名、标题正确、确认框正确）
  F3 → F5 → F4 → F6 → 验证问题二（拖动能保存、布局切换后保留）
第 2 批（P1，体验与防回归）
  F7 → F8 → F9 → F10
第 3 批（P2，收尾）
  F11 → F12 → F13
```

### 6.4 验收清单（逐步可复现）

**问题一验收**

| # | 操作 | 期望 |
|---|------|------|
| A1 | 打开 `/business/nc-config`，展开 尚龙 → ISO 13485 → AP | 第 4 级出现「测试规则1」「天天让他」**文字** |
| A2 | 选中「测试规则1」 | 画布标题 = 「工作流：测试规则1」；底部显示 `NC-…-001` |
| A3 | 点「保存」 | 确认框 = 「保存工作流到「测试规则1」？」 |
| A4 | 把某规则 `IsActive` 置 0，再在页面保存它 | DB 中 `IsActive` **仍为 0**（不被静默启用） |

**问题二验收**

| # | 操作 | 期望 |
|---|------|------|
| B1 | 拖动「结束」节点 | footer 变「**未保存**」，「保存」按钮**可点击** |
| B2 | 点「布局」 | 画布节点**真的移动**到 120/360/600 位置 |
| B3 | 保存 → 切到另一规则 → 切回 | 画布布局 = 保存时所见（**不丢失**） |
| B4 | 保存 → F5 刷新整页 → 重新选中 | 布局与保存时一致（回归 E8 路径） |
| B5 | 拖动节点后点「清空」 | 弹二次确认（未保存保护） |

**回归验收（共享组件影响面）**

| # | 操作 | 期望 |
|---|------|------|
| C1 | 打开 `/business/report-rule-config` | 树第 4 级正常显示章节名（该页数据源是 camelCase，F3/F4/F5/F6 不得破坏它） |
| C2 | 该页保存 → 切换 → 切回 | 布局保留 |
| C3 | `npm run typecheck`（cert-admin） | 0 error |

> ⚠️ **C1 是本次修复最大的回归风险点**：`WorkflowDesigner` 被两个页面共用，而两页的数据源命名不同（nc-config = PascalCase，report-rule-config = camelCase）。F3 的回写必须**同时覆盖两套键名**，F4/F5/F6 不得引入任何大小写假设。

---

## 七、影响面与风险

### 7.1 影响面

| 范围 | 是否受影响 | 说明 |
|------|-----------|------|
| `/business/nc-config` | ✅ 直接 | 本次修复目标 |
| `/business/report-rule-config` | ⚠️ 间接 | 共用 `WorkflowDesigner`，F3-F6 改动需回归 |
| `/business/workflow-rules`（NC 检查规则表格页） | ❌ 不受影响 | 走 `logic.ts`（`SingleTableCore`）+ `useFileTree`，字段映射正确 |
| 后端 `ValidationRule` 实体/表 | ❌ 不动 | §16.9 铁律七禁止改列名/加映射 |

### 7.2 风险与对策

| 风险 | 等级 | 对策 |
|------|------|------|
| F3 保持叶子引用后，`currentLeaf` 与树对象共享，误改会污染树 | 中 | 只在 `handleSave` 成功分支做 `Object.assign`；不改其他字段 |
| F4 监听 `node:drag` 高频触发 → 置脏过于频繁 | 低 | `node:drag` 只同步坐标不置脏，置脏交给 `node:drop`；或加 16ms 节流 |
| F5 改变自动布局后，用户已有布局被覆盖 | 中 | 属预期行为（用户主动点「布局」）；配合 F8 未保存确认 |
| F9 新增端点未授权 | 低 | 继承 `YzhControllerBase` 的 `[YZHAuthorize]`，无需额外配置 |
| 两页命名不一致导致回归 | **高** | 严格按 C1 验收；`renderWorkflow`/`selectLeaf` 的 `xxx \|\| Xxx` 兜底**保留**并加注释 |

### 7.3 后续治理建议（超出本次修复范围）

1. **`treeConfig` 契约校验**：在 `WorkflowDesigner` 中检测 `item[textField] === undefined` 时 `console.warn` 并 `ElMessage.warning` 一次，避免"静默空白"。
2. **统一「组织树 camelCase / 实体 PascalCase」的混合契约**：组织树是服务层手写 `Dictionary<string, object>` 返回，属于 §16.9 之外的灰色地带，建议在 `docs/10-YZH架构/04-数据契约.md` 中显式登记为**已知例外**。
3. **`store.moveNode()` 零调用点**属于典型的"死代码掩盖缺陷"，建议后续加一层：`WorkflowDesigner` 卸载前断言 `store.nodes[].x/y === diagram.getGraphData()` 一致（开发期断言）。
4. **文档同步**：`docs/50-任务/开发计划/工作流设计器通用组件抽象与重构方案-V1.md` 的 §3.3「NC 规则列表」行需更新（原文写「新架构未使用（由 `useYzhTreeTable` 驱动）」，与现状不符）。

---

## 八、实施记录与验收结果（2026-09-22 完成）

### 8.1 实施范围

在铁律七「DB 列名 = C# 属性名 = TS 字段名 = PascalCase，三处逐字一致」约束下落地 F1–F13，并补齐历史项目已有而新架构遗漏的能力。

| 文件 | 改动摘要 |
|------|---------|
| `cert-admin/src/pages/workflow/nc-config/designer.vue` | `textField:'RuleName'` / `codeField:'RuleCode'`；新增 `detailApi:'/api/ValidationRule'`；`IsActive: r.IsActive !== false`；payload 全 PascalCase |
| `cert-admin/src/pages/workflow/report-rule-config/index.vue` | 同步 PascalCase（`SectionName`/`WorkflowConfig`/`LayoutJson`/`IsActive`） |
| `cert-share/src/components/workflow/WorkflowDesigner.vue` | `TreeConfig.detailApi`；`currentLeaf` 保持引用 + 保存后 `Object.assign` 回写；`node:drop`/`node:drag` → `store.moveNode`；`autoLayout` 改 `graphModel.moveNode2Coordinate`；新增 `syncPositionsFromDiagram()`；新增 `hasWorkflow/getWorkflowConfig/getLayoutJson`；`loadLeavesForPhase` 契约校验 + `applySearchFilter` 叶子过滤；切阶段/切叶子未保存二次确认；`handleSave` 前同步坐标 |
| `cert-share/src/components/workflow/ExecutionResultPanel.vue` | `IsSuccess`/`Status`/`DurationMs`/`PathResults`/`PathIndex`/`FailedAtNodeId` 等改 PascalCase（`NcResult` 内层键保留 camelCase，登记为例外 E3） |
| `CertPlatform.Admin/Controllers/Workflow/ValidationRuleController.cs` | 新增 `GET /api/ValidationRule/{code}`（含 `RuleJson`/`LayoutJson` + 条款回填）；`FilterCore` 默认 `RuleName asc` |
| `CertPlatform.Admin/Services/Workflow/Models/TaskExecutionModels.cs` | `TaskPathResult`/`TaskExecutionResponse`/`NodeTestResponse` 去 `[JsonPropertyName]`，改纯 PascalCase |
| `CertPlatform.Admin/Services/Workflow/Models/CustomParam.cs` | `AiNodeDebugInfo`/`AiNodeTestResponse` 同步去 camelCase 映射 |
| `CertPlatform.Admin/Services/Workflow/WfExecutionTaskService.cs` | `TaskPathResult` 构造改 PascalCase |
| `项目全局规则.md` §16.9 | 增补「③′ 前端（TS）」条款 + 「已登记例外清单 E1–E5」+ 判定口径 |

**已登记的例外（本次新增登记，未改动）**：E1 `ApiResponse` 信封、E2 工作流落库 schema（`nodeId`/`nodeType`/…）、E3 运行时载荷字典键（`NcResult` 内层）、E4 `organization-tree` 展示 DTO、E5 `NewEntity`/`Schema` 反射字典。

### 8.2 编译与类型检查

| 检查 | 结果 |
|------|------|
| `dotnet build YZH.Core.Web.csproj` | **0 Error**（519 Warning 均为既有历史告警） |
| `vue-tsc --noEmit`（cert-admin） | 工作流相关文件 **0 Error**；仅 `src/pages/system/log/*` 5 个**既有**错误（`CrudLogic` 相关，与本次改动无关） |
| `vue-tsc --noEmit`（cert-share） | **0 Error**（用探针文件验证过该检查确实生效） |

> ⚠️ 收尾阶段（§8.9）重跑 `cert-admin` 类型检查时错误数升至 17。按文件维度汇总确认，新增的 12 个错误全部落在
> **会话期间被并行编辑**的文件里，与本次改动及死代码清理无因果关系。详见 §8.9「复检说明」。

### 8.3 接口层验收

| 编号 | 项 | 结果 |
|------|----|------|
| — | `POST /api/ValidationRule/filter` 字段名 | 全 PascalCase（`Code`/`OrgCode`/`StandardCode`/`PhaseCode`/`RuleCode`/`RuleName`/`RuleJson`/`LayoutJson`…）✓ |
| — | `FilterCore` 默认排序 | 返回顺序 `天天让他` → `测试规则1`，即 `RuleName` 升序 ✓ |
| **F9** | `GET /api/ValidationRule/{code}` | 200，返回 `RuleJson`(679B) + `LayoutJson`(141B) + `ClauseNumber`/`ClauseTitle` 回填 ✓ |
| — | 不存在编码 | 404 + `规则不存在：xxx` ✓ |
| — | 未带 Token | 401 ✓ |

### 8.4 浏览器端到端验收（admin/123456 → `/business/nc-config`）

> 数据前提：2 条测试规则实际归属 **河北雄安尚龙认证有限公司 / ISO 13485 / AP 阶段**（`OrgCode=5833c904-…`）。
> 若误点「接口驱动测试」分支会得到 0 条，属**正常过滤结果**，不是缺陷。

| 编号 | 验收项 | 结果 |
|------|--------|------|
| A1 | 左树第 4 级显示 NC 检查项文字 | 「天天让他」「测试规则1」正常显示，**不再是空行** ✓ |
| A2 | 排序 / 已配置标记 | `RuleName` 升序；`测试规则1` 绿勾 `configured=true`，`天天让他` 无配置 ✓ |
| F10 | 详情懒加载 | 选中叶子后发出 `GET /api/ValidationRule/4b0ac6ef…` → 200 ✓ |
| B1 | 拖动节点后置脏 | 状态由「已保存」→「未保存」，保存按钮 `disabled: false` ✓ |
| B2 | 保存落库 | DB `LayoutJson.ai_node_n1` 由 `(100, 149.41)` → `(169.31, 309.54)` ✓ |
| **B3** | 切走再切回布局保持 | 切到「天天让他」（1 节点）再切回「测试规则1」，画布仍为 `(169.31, 309.54)`，**未回滚** ✓ |
| B4 | 刷新整页后布局保持 | 硬刷新 → 重新展开 → 选中，画布仍为 `(169.31, 309.54)` ✓ |
| C1 | 左树搜索过滤叶子 | 搜「测试」只剩「测试规则1」；搜「天天」只剩「天天让他」，祖先链自动点亮 ✓ |
| — | 「运行」按钮新契约 | 请求体 `{"TaskType","RuleCode","EnterpriseCode","StandardCode","PhaseCode","ConfigJson"}`；响应体 `{"TaskCode","ItemCode","Status","IsSuccess","NcResult","PathResults"}`；前端正确解析 `Status`/`IsSuccess`/`NcResult.error` 并弹出错误详情 ✓ |

> 「运行」的业务失败信息为 `路径0@ai_node_n1: LLM 调用失败: Prompt 为空`——该测试规则的 AI 节点未配 Prompt，与本次改动无关；契约本身已验证贯通。

### 8.5 实施中新发现并修正的问题

| # | 问题 | 处理 |
|---|------|------|
| 1 | `applySearchFilter` 首版用「兄弟阶段命中结果」推导可见性，导致首个命中阶段之后的兄弟阶段被连带放行（展开后会把无关叶子全放出来） | 改为 `passThrough = ancestorHit \|\| phaseHit`，命中只向上卷，不横向泄漏 ✓ |
| 2 | `handleTestDocExtract` 判定用 `res?.status`（`ApiResponse` 无此字段）→ 恒走 `onError` | 改为 `res?.success` ✓ |
| 3 | `handleExecuteTest` / `handleTestWorkflow` 残留 `currentLeaf.value.code` / `.ruleCode` 等 camelCase 兜底（F13） | 清除，只保留 `Code` / `RuleCode` ✓ |

### 8.6 验收期间改动的测试数据（已还原）

B2/B3/B4 的拖拽+保存真实写入了 `cert_validation_rule`（`NC-…-001` / 测试规则1）的 `LayoutJson`。
验收结束后已通过 `POST /api/ValidationRule/update` 还原为诊断前原值：

```json
{"nodePositions":{"start_n1":{"x":100,"y":150},"ai_node_n1":{"x":100,"y":149.40625},"end_n1":{"x":177.01385498046875,"y":327.3315887451172}}}
```

`RuleJson` 长度前后均为 679 字符，内容未变（节点/边结构未被拖拽影响）。

第二轮（开发期断言验证）再次写入「天天让他」（`Code=1d2d7813b2ff49938e2a3020ecb784e7`）的 `LayoutJson`：

| | `LayoutJson` |
|---|---|
| 验收前 | `{"nodePositions":{"start_n1":{"x":100,"y":150},"end_n1":{"x":288.01385498046875,"y":346.3315887451172}}}` |
| 点击「布局」+ 保存后 | `{"nodePositions":{"start_n1":{"x":120,"y":80},"end_n1":{"x":360,"y":80}}}` |
| 还原后 | 与验收前逐字一致 ✓ |

`RuleJson` 前后逐字相同（`True`），`UpdateTime` 由 `03:01:36` → `03:15:27`（服务端自动维护）。

### 8.7 遗留待办（未在本次处理，建议单独排期）

1. ~~**死代码清理**~~ → **已执行**，见 §8.9。
2. ~~**`getCurrentUser` 端点未迁移**~~ → **已补齐**，见 §9。

### 8.8 第二轮加固（2026-09-22 追加）

第一轮收尾后继续处理「§8.7 遗留待办 2/3/4」，并顺带清掉两处同源的 camelCase 残留。

#### 8.8.1 `auth.ts` 失效字段修正

| 文件 | 问题 | 处理 |
|------|------|------|
| `yzh.vue.core/src/api/auth.ts` | URL 写 `/api/Auth/login`（实际 `/api/User/login`）、读 `data.data.token`（实际 `data.Token`） | 重写：URL + `data.data.Token`，`LoginResult` 接口全 PascalCase，补注释说明与 `cert-share` 版是同一契约的两处实现。**该文件经 grep 确认为孤儿（全项目零 import）** |
| `cert-share/src/api/auth.ts` | `CurrentUser` 接口为 camelCase；`getCurrentUser()` 指向新后端未实现的端点 | `CurrentUser` 改 PascalCase（`UserCode`/`UserName`/`UserTrueName`/`RoleCode`/`RoleName?`/`OrgCode?`）；`getCurrentUser()` 加「调用会 404」警示；`CaptchaData` 加「裸匿名对象、无信封」注释 |
| `cert-admin/src/layouts/AdminLogin.vue` | `refreshCaptcha()` 读 `res.data.img` → **必抛错**（接口返回裸对象 `{img,uuid}`，`yzhApi` 原样透传不拆信封），DEBUG 跳过校验才没暴露 | 改为 `res?.img ?? res?.data?.img ?? ''` |

浏览器实测确认修复生效：登录页验证码 `naturalWidth=72 / naturalHeight=32 / complete=true`，控制台零错误。

#### 8.8.2 画布 / store 一致性开发期断言（§8.7 待办 4）

在 `WorkflowDesigner.vue` 新增 `assertCanvasStoreConsistency(stage)`：

- 比对 `store.state.nodes[].x/y` 与 `diagram.getGraphData().nodes[].x/y`，双向检测（store 有画布无 / 画布有 store 无），容差 `0.5px`；
- 仅在 `import.meta.env.DEV` 生效，生产构建整段被 tree-shake；
- 挂载点三处：`handleSave` 的**同步之前**（此时才检测得到漂移）、`onBeforeUnmount` 的**清理之前**、`autoLayout` 的移动之后；
- 为让 cert-share 独立类型检查也能识别 `import.meta.env`，在 `cert-share/src/shims-vue.d.ts` 补了 `ImportMetaEnv` / `ImportMeta` 声明（此前只有宿主应用的 `vite-env.d.ts`）。

#### 8.8.3 顺带修正：`autoLayout` 兜底分支的增量恒为 0

原实现先改 `store` 坐标、再调 `graphModel.moveNode(id, p.x - n.x, p.y - n.y)`。由于 `n.x/n.y` 已被改成目标值，**增量恒为 0，画布纹丝不动而 store 已改** —— 正是「画布与 store 脱节」缺陷本身（`moveNode2Coordinate` 分支不受影响，属潜伏缺陷）。

已改为**先移画布、后改 store**，且兜底分支从画布当前坐标取增量：

```ts
} else if (typeof gm.moveNode === 'function') {
  const gn = typeof gm.getNodeModelById === 'function' ? gm.getNodeModelById(n.id) : null
  const fromX = Number(gn?.x ?? n.x) || 0
  const fromY = Number(gn?.y ?? n.y) || 0
  gm.moveNode(n.id, p.x - fromX, p.y - fromY)
}
```

#### 8.8.4 第二轮验收结果

| 项 | 结果 |
|----|------|
| `cert-admin` `vue-tsc --noEmit` | 仅剩 `src/pages/system/log/*` 的 5 个**既有**错误（`CrudPageLogic` 未导出，与本次改动无关）✓ |
| `cert-share` `vue-tsc --noEmit` | **0 错误**（并用探针文件验证过该检查确实生效）✓ |
| 自动布局实际移动画布 | 「开始」`(100,150)` → `(120,80)`、「结束」`(288.01,346.33)` → `(360,80)`，与 `posMap` 预期逐值一致 ✓ |
| 布局落库 | `LayoutJson` 由 `…{"x":100,"y":150}…{"x":288.01,"y":346.33}` 变为 `…{"x":120,"y":80}…{"x":360,"y":80}` ✓ |
| 硬刷新后保持 | 整页重载 → 重新展开 → 选中，画布仍为 `(120,80)` / `(360,80)` ✓ |
| 断言无假阳性 | 加载、自动布局、保存、刷新四个路径 `console.error` 均为空 ✓ |
| 测试数据还原 | 「天天让他」`LayoutJson` 已还原为原值，`RuleJson` 逐字未变 ✓ |

### 8.9 死代码清理（用户确认后执行）

`cert-admin/src/pages/workflow/nc-config/` 下的 9 个文件已被 `cert-share/src/components/workflow/` 同名文件取代，
全项目零引用（对 `nc-config/<文件名>` 形式的 import 路径做全量 grep，无任何命中），**已用 `git rm` 删除**：

| 文件 | 与 `cert-share` 版对比 |
|------|----------------------|
| `ExecutionResultPanel.vue` | **DIFFERS** —— 仍是 camelCase 旧版，误导性最强 |
| `NodePropertyForm.vue` / `SkillPanel.vue` / `panels/PortControl.vue` / `panels/PromptRefEditor.vue` / `panels/CustomParamsEditor.vue` | IDENTICAL |
| `composables/compiler.ts` / `composables/useWorkflowStore.ts` | 各差 5 行 |
| `adapters/branchNode.ts` | `cert-share` 无对应物 |

> 9 个文件**全部被 git 跟踪**，`git rm` 后可由 `git restore --staged --worktree <path>` 或 `git checkout HEAD -- <path>` 完整回滚。

**保留（活跃代码，不可删）**：

- `nc-config/index.vue` —— 路由 `business/workflow-rules` 的页面本体
- `nc-config/logic.ts` —— 该页面的逻辑层
- `nc-config/designer.vue` —— NC 规则设计页的薄包装层（承载 `treeConfig` / `saveConfig`）

删除后目录仅剩上述 3 个文件。

**复检说明（重要）**：删除后重跑 `cert-admin` `vue-tsc --noEmit`，错误数由 5 升至 **17**。按文件维度汇总后确认，**17 个错误无一涉及被删文件或本次改动的文件**：
| 文件 | 错误数 | 备注 |
|------|--------|------|
| `src/pages/workflow/report-rule/index.vue` | 5 | 多为 `TS6133 未使用变量`（`Document`/`OfficeBuilding`/`Calendar`/`fileTreeData`/`treeLoading`） |
| `src/pages/system/log/index.vue` + `logic.ts` | 5 | **删除前就存在的既有错误**（`CrudPageLogic` 未导出） |
| `../cert-share/src/components/CertDirectoryTree.vue` | 3 | `TS6133 未使用变量` |
| `../cert-share/src/components/CertBizTree.vue` | 2 | 含 1 个 `TS2339: searchText 不存在`（模板引用未暴露的 ref） |
| `src/pages/workflow/nc-config/index.vue` | 1 | `TS6133: treeLoading 未使用` |
| `src/pages/workflow/directory/index.vue` | 1 | `TS6133: treeLoading 未使用` |

上述文件在本次验收期间（11:13–11:16）由**并行进行的其他编辑**改动过（mtime 落在会话中间），
与本次死代码清理**无因果关系**。清理本身零风险：被删的 9 个文件在删除前已确认全项目零引用。

---

## 九、补齐历史项目遗漏：个人中心（个人设置 / 修改密码）

> 本节属用户「**将历史项目遗漏的代码补全**」要求的延续，与 NC 页面无直接关系，但同属一次补齐动作，故并入本文档。
> 诊断起点是 §8.8.1 修正 `cert-share/src/api/auth.ts` 时发现的「`getCurrentUser` 端点未迁移」。

### 9.1 发现的缺口

历史项目 `src/old/.../Sys_UserController.cs` 有 3 个个人中心端点，新后端**一个都没有**；
而 `cert-admin/src/layouts/AdminLayout.vue` 的「个人设置 / 修改密码」两个弹窗**只是把成功提示演了一遍**：

| 历史端点 | 历史实现 | 新后端（改前） | 前端（改前） |
|---|---|---|---|
| `POST api/User/getCurrentUserInfo` | `Sys_UserService.GetCurrentUserInfo()` | ❌ 不存在（调用 404） | ❌ 未调用 |
| `POST api/User/updateUserInfo` | 更新 `UserTrueName/Gender/Remark/HeadImageUrl` | ❌ 不存在 | ❌ `saveProfile()` 只改本地 Pinia，**不发请求** |
| `POST api/User/modifyPwd` | 校验旧密码后改密 | ❌ 不存在 | ❌ `changePassword()` 只弹 `ElMessage.success('密码修改成功，请重新登录')`，**不发请求** |

**用户可感知的后果**：

1. 登录响应只给 `Token/UserCode/UserName/UserTrueName/RoleCode` 且**未持久化** → **刷新页面后顶栏从「超级管理员」退化成「管理员」**，个人设置各字段全空；
2. 个人设置点「保存」→ 提示成功但**什么都没落库**；
3. 修改密码 → 提示成功并跳登录页，但**密码根本没变**（旧密码仍可登录），属会误导用户的安全缺陷；
4. 表单里还有个「昵称」字段 —— `Sys_User` 表**根本没有这一列**，纯幽灵字段。

### 9.2 后端实现（`YZH.Core.Web/Controllers/System/UserController.cs`）

沿用该文件既有的 Vol 兼容绝对路由写法（`getVierificationCode` 已在用），新增 3 个端点：

| 端点 | 路由 | 说明 |
|---|---|---|
| `GetCurrentUserInfo` | `POST/GET api/User/getCurrentUserInfo` | 返回 `ApiResponse<CurrentUserInfoDto>`，data 全 PascalCase（15 个字段） |
| `ModifyPwd` | `POST api/User/modifyPwd` | 请求体 `{ OldPwd, NewPwd }`（PascalCase） |
| `UpdateUserInfo` | `POST api/User/updateUserInfo` | 请求体全字段可选，只更新显式传入的字段 |

要点：

- **命名铁律**：请求体与响应体全部 PascalCase。历史项目用的 `oldPwd`/`newPwd` 已在新前端同步改名（E 类例外不适用于此 —— 这些是**请求 DTO**，不是历史落库 payload）。
- **白名单更新**：`Entity.Update(entity, clientIp, updateFields)` 走 `SqlSugar .UpdateColumns()`，只写白名单列，避免整实体回写误覆盖并发修改的其他列。
- **防越权**：`UpdateUserInfo` 的 `Code`/`Enable`/`UserPwd`/`OrgCode` 一律取自库中原值，**不接受请求体传入**。
- **无变化短路**：若提交值与库中一致，直接返回成功 —— 否则 MySQL 影响行数为 0，会被 ORM 判成「更新失败：记录不存在或无变化」。
- **有意修正历史缺陷**：历史 `UpdateUserInfo` 的白名单漏了 `Email`/`PhoneNo`，但前端表单却在编辑并提交它们 —— 保存后静默丢失。新实现把这两个字段纳入白名单，已在代码注释中标注差异。

### 9.3 前端实现

| 文件 | 改动 |
|---|---|
| `cert-share/src/api/auth.ts` | `CurrentUser` 接口补全为 15 个 PascalCase 字段；新增 `UpdateUserInfoParams`、`updateUserInfo()`、`modifyPwd()`；`getCurrentUser()` 去掉「调用会 404」警示，改为 `POST` |
| `cert-admin/src/store/auth.ts` | `UserInfo` 由 camelCase 手写模型改为 `CurrentUser & { Token? }`（**直接承接接口响应**，§16.9 ③′）；删掉无对应列的 `userId`/`nickname`；新增 `patchUserInfo()` |
| `cert-admin/src/layouts/AdminLayout.vue` | 新增 `loadCurrentUser()` 并在 `onMounted` 调用；`saveProfile()` 改调 `updateUserInfo`；`changePassword()` 改调 `modifyPwd`；顶栏与头像首字母改读 `UserTrueName ?? UserName`；个人设置表单字段改名 `UserTrueName/Email/PhoneNo`，**幽灵字段「昵称」替换为真实存在的「备注」**；补 `profileSaving`/`passwordSaving` 防重复提交 |
| `cert-admin/src/layouts/AdminLogin.vue` | `setUserInfo` 载荷改 PascalCase |

### 9.4 验收结果（接口 + 浏览器）

**接口层（curl）**

| 项 | 结果 |
|---|---|
| `getCurrentUserInfo`（POST） | HTTP 200，返回 13 个非空字段：`UserCode=USER_000001`、`UserTrueName=超级管理员`、`RoleCode=ROLE_SUPER_ADMIN`、`RoleName=体系管理员`、`Email`/`PhoneNo`/`Address`/`Remark`/`HeadImageUrl`/`CreateTime`/`LastLoginDate` 全对 ✓ |
| `modifyPwd` 空旧密码 / 新密码过短 / 旧密码错误 / 新旧相同 | 4 个分支均 HTTP 400 且文案正确（`旧密码不能为空` / `密码不能少于 6 位` / `旧密码不正确` / `新密码不能与旧密码相同`）✓ |
| `modifyPwd` 成功路径 | 200 `密码修改成功，请重新登录`；改后旧密码登录失败、新密码登录成功 ✓ |
| `updateUserInfo` 无变化提交 | HTTP 200 `修改成功`（短路生效，未被误判为失败）✓ |
| `updateUserInfo` 真实修改 | 只改 `Remark`，`UserTrueName`/`Email`/`PhoneNo` 保持原值（局部更新正确）✓ |

**浏览器端到端（agent-browser，admin/123456）**

| 项 | 结果 |
|---|---|
| 登录后顶栏 | 「超级管理员」✓ |
| **硬刷新后顶栏** | 仍为「超级管理员」（**改前会退化成「管理员」**）✓ |
| 个人设置回填 | 账号 `admin` / 姓名 `超级管理员` / 邮箱 `283591387@qq.com` / 电话 `13888888888` / 备注 `~还没想好...`，**改前全空** ✓ |
| 个人设置保存 | 提示「个人信息已保存」→ API 复查 `Remark` 已落库，其余字段未受影响 ✓ |
| 修改密码（错误旧密码） | 界面提示 **`旧密码不正确`**（**改前会假报成功**）✓ |
| 修改密码（正确路径） | 提示成功并跳转 `/login` ✓ |
| 控制台 | 全流程 `console.error` 为空 ✓ |

### 9.5 顺带发现：SSO「挤号」机制实际未生效

验证改密时发现：**改密后递增 SSO 版本号并不能让旧 Token 失效** —— 用改密前的旧 Token 调接口仍返回 HTTP 200。
进一步实测：**重新登录后，上一次登录的旧 Token 依然有效**。

结论：`TokenVersionService`（Redis `token_ver:{userCode}`）**只写不校验** —— 版本号在登录时写入 JWT 的 `ver` claim，
但请求鉴权链路从未拿它跟 Redis 里的当前版本比对。即 §16.x 文档中描述的「挤号」能力当前**是设计而非现实**。

- 影响面：任何登录过的 Token 在有效期内都不会因再次登录而失效；改密也不会踢下线。
- 归属：框架层既有缺口（`src/yzh-core/`），**与本次新增端点无关**，属独立安全议题。
- 本次处理：在 `ModifyPwd` 的 XML 注释中如实标注该现象（避免后来者误以为已生效）；前端仍主动清 Token 跳登录页，作为补偿。

### 9.6 验收期间改动的测试数据（已还原）

| 对象 | 改动 | 还原 |
|---|---|---|
| `admin` 密码 | `123456` → `temp123456` → `123456` | ✓ 已还原（`123456` 登录成功、`temp123456` 登录失败） |
| `admin.Remark` | `~还没想好...` → `[e2e-probe] 个人中心保存测试` | ✓ 已还原 |
| `admin.LastModifyPwdDate` / `LastLoginDate` | 由服务端自动维护 | 未回滚（审计字段，属正常写入） |

### 9.7 制度层同步

- `项目全局规则.md` §16.9 例外清单新增 **E6「未包信封的裸 JSON 响应」**（`api/User/getVierificationCode` 的 `{ img, uuid }`），并补充判定技巧（`curl` 看顶层有无 `success`/`data`）与「两个验证码端点风格不同」的提醒：
  - `AuthController.GetCaptcha()`（路由 `api/User/captcha`）→ **带** `ApiResponse` 信封；
  - `UserController.GetVierificationCode()`（路由 `api/User/getVierificationCode`）→ **裸对象**。
- 文件版本号对齐：文首 `V3.0`（2026-09-12）与文末 `V2.4` 长期不一致，已统一为 **V3.1 / 2026-09-22**。

---

## 十、收尾：前端最后一处未迁移页面 + 一处并发编辑留下的语法错误

### 10.1 `system/log` 页面迁移到 `SingleTableCore`

排查 `vue-tsc` 错误时发现：全项目已完成 `CrudPageLogic` → `SingleTableCore` 迁移
（`yzh.vue.core/src/logic/CrudPageLogic.ts` 已删除，`logic/index.ts` 只导出 `SingleTableCore` / `TreeTableCore` 等），
**只有 `src/pages/system/log/` 没跟上** —— 仍 `import { CrudPageLogic } from '@yzh-core'`，导致 5 个 TS 错误。

| 文件 | 改动 |
|---|---|
| `system/log/logic.ts` | `extends CrudPageLogic<any>` → `extends SingleTableCore<any>`；删除多余的 `init() { await this.loadConfig() }` 覆写（基类 `init()` 本身就是 `loadConfig()` → `onAfterInit()`） |
| `system/log/index.vue` | 手工 `new LogLogic()` + `logic.dataLoader` 改为 `useSingleTable(LogLogic)`（统一 CP-5 样板）；补绑 `toolbar-actions` / `row-action-buttons` / `@row-action` / `@toolbar-action` |

**只读性无需前端处理**：`SysLogController` 已在后端覆写 `GetToolbar()` / `GetRowButtons()`，关闭 Add / Delete / Edit、仅保留 Export。实测 `/config` 返回 `Toolbar: {Add:false, Delete:false, Export:true, Import:false}`、`RowButtons: {Edit:false, Delete:false, Enable:false}`，因此本页**不需要** `YzhFormDialog`。

**浏览器实测**（`/system/log`）：表头 7 列正确（模块 / 操作 / 目标类型 / 目标ID / 操作人ID / IP地址 / 操作时间）；搜索项 2 个（模块、操作）；工具栏 查询 / 重置 / **导出** / 列设置；空表显示「暂无数据」；`console.error` 为空。

> **导出按钮是本次新增的可见改进** —— 旧 `index.vue` 从未绑定 `toolbar-actions`，后端明明开了 `Export: true` 却渲染不出来。
> `/api/System/Log/export` 实测返回 HTTP 400 `没有可导出的数据`（`sys_log` 表为空），属正确行为。

### 10.2 修复 `CertDirectoryTree.vue` 的语法错误

排查过程中发现 `cert-share/src/components/CertDirectoryTree.vue` 处于**改到一半的破损状态**（mtime 11:28，会话进行中）：

- 第 58 行有一个**多余的 `}`**，`<script setup>` 被提前闭合 → `TS1128: Declaration or statement expected`；
- 第 92 行引用 `fileTreeData.value`，但第 53 行只解构了 `loadStageFiles` → `fileTreeData` 未定义。

该文件正在把自身重构为「委托给新的通用组件 `CertBizTree.vue` + `useFileTree()` 组合式函数」。
`CertBizTree.vue`（11:27）与 `useFileTree.ts`（11:30）均已就绪，只有它没跟上。

**处理**：只做**最小化 un-break**，不动重构者的结构 —— 删掉多余的 `}`，并把 `fileTreeData` 补进解构：

```ts
const { loadStageFiles, fileTreeData } = useFileTree()
```

> 这两处是**无歧义的破损**（语法错误 + 引用未定义变量），修完即与重构意图一致，不构成对他人设计的改动。

### 10.3 ⚠️ 重要观察：会话期间存在**活跃的并行编辑**

本次会话期间，同一工作区有另一路编辑在**持续改写文件**（非本人操作）。证据：

| 时间 | 文件 | 现象 |
|---|---|---|
| 11:13–11:16 | `CertBizTree.vue`(新增)、`CertDirectoryTree.vue`、`report-rule/index.vue`、`nc-config/index.vue`、`directory/index.vue` | `vue-tsc` 错误数 5 → 17 |
| 11:27–11:28 | `CertBizTree.vue`、`CertDirectoryTree.vue`、`useFileTree.ts` | 错误数回落至 1 |
| **11:30:15** | `useFileTree.ts` | 又改了一次，错误数回到 8（新出现 `Cannot find name 'getFiles'`） |

**结论与建议**：

- `vue-tsc` 的错误数在本会话内经历了 `5 → 17 → 1 → 8` 的反复，**每次变化都由并行编辑驱动**，与本次改动无关。
  判定归属的可靠方法：`grep -oE '^[^(]+' <tsc输出> | sort | uniq -c` 按文件汇总 + 对照文件 mtime。
- **本人负责的文件已全部清零**：`system/log/*`（迁移后 0 错误）、`AdminLayout.vue`、`AdminLogin.vue`、`store/auth.ts`、`api/auth.ts`、`shims-vue.d.ts`、`WorkflowDesigner.vue`、`UserController.cs`。
- **剩余错误全部落在并行编辑正在改写的文件里**，本次**未继续处理**（避免与对方冲突、造成丢改动）：
  `useFileTree.ts`(`getFiles` 未定义)、`report-rule/index.vue`(5 个未使用变量)、`directory/index.vue` + `nc-config/index.vue`(各 1 个未使用变量)。
- 建议：若这不是你预期的并行任务，请确认是否有另一个 AI 会话/IDE 插件在同时写这个工作区；否则容易出现互相覆盖。

### 10.4 回归确认

改完上述两处后，浏览器复查 **`/business/nc-config`** 仍正常：左树正常渲染（机构 → 3 个标准，层级与计数正确）、顶栏显示「超级管理员」、`console.error` 为空。本次所有改动未对 NC 页面造成回归。

---

## 11. 端到端迁移缺口扫描（承接「将历史项目遗漏的代码补全」）

### 11.1 背景

用户要求「将历史项目遗漏的代码补全」。为把这件事从「凭感觉补」变成「按清单补」，本轮对历史后端（71 控制器 / 235 路由）与新后端（33 控制器 / 144 路由）做了全量路由对照，并对每条结论做 HTTP 实测。

**产出**：`docs/50-任务/迁移计划/端到端迁移缺口清单-V1.md`（独立文档，含完整对照表与排期建议）

### 11.2 扫描口径的一个坑（值得记录）

历史项目大量使用 **Partial 分部类**：类级 `[Route]` 在 `XxxController.cs`，业务动作在 `Partial/XxxController.cs`；且一个类可能挂**多个类级路由**（如 `Sys_RoleController` 同挂 `api/Role` 与 `api/Sys_Role`）。

扫描器若「只看单文件 + 只取首条路由」，会**系统性低估**缺口。实测同一份代码库：

| 口径 | 缺口数 | 性质 |
|------|--------|------|
| 只看单文件 + 只取首条类级路由 | 52 | 低估 |
| 跨文件合并类级路由 + 只取首条 | 80 | 仍低估 |
| **跨文件合并 + 展开全部类级路由** | **97** | ✅ 采用 |

典型漏检案例：`api/AuditorAuth/Register`（审核员注册）—— 类级路由在 `Auditors/AuthController.cs`，动作在 `Partial/AuthController.cs`，早期口径下该端点**完全不可见**。

### 11.3 扫描结果

| 分类 | 条数 | 处置 |
|------|------|------|
| A 类 虚警（路径改名/能力合并） | 55 | ✅ 无需补，实测等价能力存在 |
| B 类 真缺口（未迁移） | 28 | ⏳ 按优先级补齐 |
| C 类 已废弃（V4 架构裁剪） | 14 | ⛔ 不补，应标注「已废弃」 |

**B 类缺口分组**（均经 HTTP 404 实测确认）：

| 优先级 | 模块 | 端点 |
|--------|------|------|
| **P0** | 审核员端认证 | `AuditorAuth/Register` / `GetCurrentUser` / `GetOrgList` |
| **P0** | 消息中心 | `message/unread-count` / `read-all` / `list` / `read/{id}` |
| P1 | 机构关联同步 | `org-link/SyncOrgStandards` / `SyncOrgStages` / `GetOrgStdIds` / `GetOrgStageIds` |
| P1 | AI 用量余额 | `ai-usage/balance` |
| P2 | 审核员资料管理 | `auditor/register` / `sendsmscode` / `updateprofile` |
| P2 | 企业端整体 | `Enterprise/*`（5）+ `enterprisefile/*`（3） |
| P3 | 代码生成器 / 最大编号 / SSO 挤号 | `builder`（10）/ `certcertificationbody/getmaxid` / `user/replacetoken` |

**根因**：`CertPlatform.Auditor` 与 `CertPlatform.Enterprise` 均为**空项目**（仅 `.csproj`），对应前端 `cert-auditor` 只有 1 个 workspace 骨架、`cert-enterprise` 只有 3 个配置文件。

### 11.4 顺带修复的真 bug：`system-log.ts`

断链检查中筛出的唯一真 bug。`cert-share/src/api/system-log.ts` 的 `getLogPage()` 有**四处契约偏差**：

| # | 问题 | 原值 | 修正 |
|---|------|------|------|
| 1 | 路径缺 `/api` 前缀 | `/SysLog/filter` | `/api/System/Log/filter` |
| 2 | 控制器名错 | `SysLog` | `System/Log` |
| 3 | 直接读信封字段 | `res.Items` | `res.data.Items` |
| 4 | 入参字段不存在 | `params.pageSize` | `params.rows` |

实测：

```
[修复前] POST /SysLog/filter          → HTTP 404
[修复后] POST /api/System/Log/filter  → HTTP 200
         {"success":true,"data":{"Items":[],"TotalCount":0,"PageIndex":1,"PageSize":20,...}}
```

实现改为复用 `generic.ts` 的 `getEntityPage('System/Log', ...)`，返回 `Page<T>`（`{rows,total}`），与 `YzhTable` 数据加载器契约一致。`cert-share` 独立 `vue-tsc` 复检 **0 错误**。

> 该函数当前无调用方（`system/log` 页面已迁移到 `SingleTableCore`），属「被 barrel 导出但无人使用」的活代码。修复而非删除，是为消除 `api/index.ts` 导出面的陷阱。

### 11.5 一个方法论收获：继承基类的端点会误判为「断链」

静态扫描不建模继承，因此 `filter`/`add`/`update`/`delete`/`config` 这类**由基类 `YzhControllerBase` 提供**的标准动作，在派生控制器文件里找不到声明，会被误判为缺失。

本轮初筛出 5 条「断链」，实测后 4 条为假阳性：

| 疑似断链 | 实测 |
|---------|------|
| `/api/ValidationRule/filter` | ✅ HTTP 200（基类提供） |
| `/api/Foundation/ISOStandard/filter` | ✅ HTTP 200（基类提供） |
| `/api/Workflow/WfSkill/filter` | ✅ HTTP 200（基类提供） |
| `/api/Workflow/WfSkillCategory/filter` | ✅ HTTP 200（基类提供） |
| `/SysLog/filter` | ❌ HTTP 404（真 bug，已修） |

**结论**：路由扫描的结果**必须用 HTTP 实测兜底**，否则假阳性会淹没真问题。

### 11.6 待用户决策

1. **B 类补齐排期**：P0 两项（审核员端认证、消息中心）是否立即开工？
2. **企业端定位**：B3/B4 属「新端建设」而非「补遗漏」，需确认产品排期。
3. **并行编辑冲突**（承接 §10.3）：会话期间仍有另一路编辑在改写工作区，剩余 TS 错误全在对方正在改写的文件里，本次未处理。

---

**文档版本**：V1（含 2026-09-22 实施记录 + 第二轮加固 + 个人中心端点补齐 + 收尾迁移 + 端到端缺口扫描）
**创建时间**：2026-09-22
**诊断人**：AI（源码 + 接口 + 浏览器三方实测）
