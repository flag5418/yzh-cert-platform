# 文档语义规则（提示词工作台）—— 下一步 TODO（AI 手交清单）

> 最后更新：**2026-10-03 10:25**
> 当前状态：后端 **0 Error**｜前端守卫 **通过**（20 规则 / 1011 文件）｜`vue-tsc` **0 错误**
> ⚠️ **仍未在浏览器实测**（本机可跑，但沙箱内不允许启动 dev server）
> ★ 本轮新增：**提示词可读性重构 V3**（3 条种子已重写入库）+ **UI 统一到框架令牌**
> → 规范全文见 **`05-提示词编写规范-V1.md`**

---

## 一、当前实现（真实状态，勿重复造）

### 1.1 菜单与路由（⛔ 都不许改名）

| 项 | 值 |
|---|---|
| 菜单 | `MENU_00210` **文档语义规则**（OrderNo 250） |
| 路由 | `/business/prompt-template` → `pages/workflow/prompt-template/index.vue` |
| 后端路由 | `/api/PromptTemplate`（⛔ 改名 = `ApiCode` 变化 = 角色-接口关联**静默断裂**） |

### 1.2 前端页面结构（2026-10-02 二次重构后）

```
WorkbenchBar        作用域 › 类型切换 │ AI生成 恢复 保存 专注
├─ StandardTree     左：标准树（根「全部标准」= 平台级；节点下标 [分类] [作用] 徽章）
└─ wb__main         右：上下分栏（比例持久化到 localStorage）
   ├─ PromptEditor      上：提示词正文（宽 = 全部剩余，纵向长文）
   ├─ 拖拽把手          比例 0.15~0.85
   └─ PromptTestPanel   下：上传 + 结构化结果（5 页签）
      └─ SemanticResult     「结果」页签：把语义 JSON 渲染成结论
```

组件目录：`pages/workflow/prompt-template/components/`
（`WorkbenchBar.vue` / `StandardTree.vue` / `PromptEditor.vue` / `PromptTestPanel.vue` / `SemanticResult.vue`）

**为什么是上下分栏而不是三栏**：实测三栏时中栏仅 474px ≈ 34 字/行，而提示词正文
1500–2500 字 —— 长文被压成窄条；测试是偶发动作却永久占 410px。

### 1.3 后端端点（8 个，全在 `PromptTemplateController`）

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `workbench/resolve` | 三层回退定位（标准级 → 平台级） |
| GET | `workbench/list` | 列某类型 + 标准下的全部提示词 |
| POST | `workbench/generate` | AI 生成 / **优化**草稿（`currentTemplate` 非空 = 优化） |
| POST | `workbench/test` | 上传试跑（multipart，200MB；双路径见 1.4） |
| GET | `workbench/standards` | 标准下拉 |
| POST | `workbench/save` | 幂等 upsert（**不 +1 版本**） |
| POST | `workbench/delete` | 逻辑禁用 `IsValid = 0` |
| POST | `workbench/activate` | 设为生效（同类型其他自动失效） |

### 1.4 ★ Markdown 复用（不是「文件即弃」）

```
首次/换文件：Files → 转 Markdown → 落 Redis（PromptMarkdownCache，TTL 8h）→ 回传 cacheKey
反复调提示词：只传 CacheKey → 读 Redis 直接跑 LLM（零转换、零上传）
缓存过期   ：报「测试缓存已过期或不存在」→ 重新上传
Redis 挂掉 ：Get 返回 null / Set 静默丢弃（**不抛异常**，工作台降级为「每次重新转换」）
```

- 实现：`CertPlatform.Admin/Services/Workflow/PromptMarkdownCache.cs`
- key 前缀 `prompt_md:`，配置 `appsettings.json → Redis:ConnectionString`（默认 `127.0.0.1:6380`）
- ⚠️ **只有本工作台的 Markdown 走 Redis**；框架层 `INoSql`/认证/字典仍是内存缓存，一行未改

### 1.5 ★ 统一 AI 配置（Q3=a）

模型参数**一律读 `cert_sys_config` 六键**（`ai_api_key` / `ai_base_url` / `ai_model_name` /
`ai_max_tokens` / `ai_temperature`）。

- ⛔ 提示词行上的 `ModelName`/`MaxTokens`/`Temperature` **本工作台不再读取、不再下发、界面不展示**
- ⚠️ 但 `PromptTemplateService.SaveAsync` 更新分支**会原样保留**这三个字段
  （`PromptWorkbenchService.cs` 中 `entity.ModelName = existing.ModelName` 等三行）
  —— 因为 NC 链路 `BuildNcPromptSkill` 仍在读它们。**别删这三行。**
- ⛔ 提示词**不做版本管理**：`SaveAsync` 不再 `Version + 1`，保留原值
  （`DocExtractionRuleService.AI` 与 `BuildNcPromptSkill` 仍按 `OrderByDescending(Version)` 取行）

### 1.6 语义上下文占位符（提示词按标准差异化的入口）

后端 `BuildSemanticContextAsync` 注入，前端 `logic.ts` 的 `STD_PLACEHOLDERS` 同步暴露：

| 占位符 | 内容 |
|---|---|
| `{{standard_name}}` | 标准名 + 版本年 |
| `{{std_doc_catalog}}` | 该标准下的标准文档清单摘要（最多 120 行） |
| `{{folder_tree}}` | 标准目录文件夹树（3 层全路径，缩进 = 层级） |
| `{{tag_list}}` | 该标准启用的标签清单（LLM **只能从中选**） |
| `{{code_prefix_map}}` | 编号前缀 → 标签映射（L0 规则） |
| `{{output_schema}}` | 输出 JSON Schema 文本 |
| `{{tag_constraint}}` | 「只能从字典选」硬约束文本 |
| `{{file_list}}` | 批次文件清单（分组专用） |
| `{{document_content}}` | 单份全文（作用专用） |

> ⚠️ 渲染顺序：语义上下文**必须在注入原始正文之前**渲染
> （`PromptWorkbenchService.cs` 第 ~419 行），否则 Markdown 里偶发的 `{{...}}` 会被误替换。

### 1.7 种子提示词（当前库内 3 条 —— 2026-10-03 重写后实测值）

| PromptCode | 类型 | 标准 | 模板长度 |
|---|---|---|---|
| `prompt_generator` | 元提示词 | 平台级（NULL） | 2219 |
| `doc_group_iso9001` | 分组（分类） | ISO 9001（`846dec4b-…b7d9`） | 2270 |
| `doc_content_iso9001` | 作用 | ISO 9001（`846dec4b-…b7d9`） | 2724 |

⚠️ **只有 9001 配了提示词**：食品标准（`475da4fe-…ddf7`）下 **0 行**，
且**平台级 `doc_group` / `doc_content` 也是 0 行**（平台级只有 `prompt_generator`）。
⇒ 选「食品标准」时编辑器本来就是空的，这是数据现状而非缺陷。**见 §三 P1·B 裁决 C/D**。

### 1.8 保存闸门（P5 —— 2026-10-03 由「硬拦」改为「软提示」）

`index.vue` 的 `saveBlockedReason` —— 四道拦截项（**判定逻辑不变**）：

```
正文为空              → 拦
分析未通过            → 拦
未测试                → 拦
已修改但未重新测试    → 拦
```

**但处置方式变了**（用户裁决）：

| | 旧（硬拦） | 新（软提示） |
|---|---|---|
| 保存按钮 | `:disabled="!!saveBlockedReason"` | **不禁用**，始终可点 |
| 点保存 | 无反应（按钮灰） | 弹 `ElMessageBox.confirm`「未通过测试闸门 —— {原因}。仍要保存吗？」 |
| 问题 | 「有改动 → 想保存 → 被拦 → 卡死」；且与「AI 生成后自动保存」**直接冲突**（刚生成必然「未测试」⇒ 自动保存永远失败） | 保留安全意识，不阻塞操作流 |

**绕过闸门的合法路径**（`doSave({bypassGate:true})`）：
① AI 生成后自动落库；② 切换守卫里用户选「保存并切换」。
理由：**保存 ≠ 值得信任**，生效与否仍由测试结果说话。

---

## 二、2026-10-03 本轮修复（已完成）

| # | 位置 | 问题 | 处理 |
|---|---|---|---|
| ① | `PromptWorkbenchService.cs` L102 | `PhFolderTreePh = "folder_tree"` 与 L88 的 `PhFolderTree` **同值同义且从未被引用** —— 两个同名常量并存时改一个不会有编译错误，另一处仍用旧值 ⇒ 占位符静默不替换 | 删除死常量，留唯一一份 + 注释说明 |
| ② | `PromptWorkbenchService.cs` L442/452 | `"{{file_list}}"` / `"{{document_content}}"` 写成**字面量**，导致 `PhFileList`/`PhDocContent` 两个常量沦为死代码，且两处可能悄悄不一致 | 改用 `"{{" + PhFileList + "}}"` 拼接，占位符名单一来源 |
| ③ | `SemanticResult.vue` 表格区 | `(d.tables \|\| []).slice(0, 6)` **静默截断**：模型返回 20 个表格时界面只显示 6 个且无任何提示 ⇒ 用户以为「只识别出 6 个」，结论被界面误导 | 加「… 另有 N 个表格未展开」提示（warning 色） |
| ④ | `logic.ts` `makePromptCode` | 用 `Date.now().toString(36)` 生成 `PromptCode`；同毫秒连续两次调用产出**完全相同**的码，而 Code 是唯一业务键（幂等 upsert）⇒「新建」静默变「覆盖另一条」且提示保存成功 | 改 6 位随机 base36（≈21.8 亿组合） |

**验证**：后端 `0 Error`｜守卫 `✓ 通过`｜`vue-tsc` `0 错误`

---

## 二·B、2026-10-03 第二轮（用户四点补充 → 已完成）

### 2B.1 第 3 点：提示词可读性 / 可维护性重构

**用户原话**：「我阅读了提示词，感觉不太容易理解，真正让实施人员维护感觉比较困难，
是否有更合理的写法，还有个问题，是否考虑不同 iso 标准的差异」

**诊断出 V2 正文的 6 个可维护性问题**：
装饰符号过载（`★ ★★ 〇 ⚠️ ⛔` 混用）/ 规则跨节重复（「一文档多标签」在 §四 与 §六.1 各写一遍）/
规则散落（`skip` 判定散在三处）/ **占位符无语境（最大来源）** / 标准差异隐含 / 两类提示词职责重叠

**修法 = 编写规范 V1（R1~R6）**：固定 8 节骨架 · 规则编号唯一 · 符号只用 `⚠️`/`⛔` ·
占位符首次出现处写人话说明 · 新增「二、本标准上下文」节作为差异化唯一位置 · 正文 1200~2500 字

**已落地**：`scripts/db/20261003_prompt_readable_V3.sql`（幂等，已执行）

| 提示词 | 改写前 | 改写后 | 关键变化 |
|---|---|---|---|
| `doc_group_iso9001` | 1999 字 | **2270 字** | 8 节骨架 / R1~R5 编号 / 去 ★ 〇 / 新增「本标准上下文」节 |
| `doc_content_iso9001` | 1492 字 | **2724 字** | 同上 + **修复 `{{output_schema}}` 本体回吐缺陷** |
| `prompt_generator` | 1268 字 | **2219 字** | 教模型「8 节骨架 + 编号规则 + 符号限制」 |

**校验结果**（已跑）：
- 占位符白名单 **全部通过** —— `doc_group` 8 个 / `doc_content` 8 个 / `prompt_generator` 9 个，**越界 0**
- 8 个占位符检查（含 `folder_tree`）对两类业务提示词**全为 1**
- 行级参数三列（`ModelName`/`MaxTokens`/`Temperature`）三条**全为 NULL**（统一 AI 配置保持）

### 2B.2 ★ 两处事实更正（此前记录有误）

| 此前记录 | 实测事实 | 依据 |
|---|---|---|
| ❌「`cert_standard_doc_contract` 0 条 ⇒ `{{std_doc_catalog}}` 注入空清单」 | ✅ **数据是齐的**。`BuildStdDocCatalogTextAsync` 读的是 `cert_standard_directory_config`（10 条）+ `cert_standard_directory_file`（**668 行**，按 `FileName` 去重后 **168 份**） | `PromptWorkbenchService.cs` L850-898 |
| ❌「只有 9001 一个标准」 | ✅ `cert_iso_standard` 实测 **2 条**：`9001标准`(iso9001-2015) / `食品标准`(iso4001-2016)。但**只有 9001 有提示词行** ⇒ 选「食品标准」时 `resolve` 回退到平台级、平台级无行 ⇒ 编辑器显示空（**正确行为，非 bug**） | 直查 DB |

**⇒ 当前真正的缺口**：不是「占位符没数据」，而是**只有 1 个标准配了提示词**。
要验证「同一文件在不同标准下结论不同」，需要给第二个标准也建提示词行 + 目录配置。

### 2B.3 第 4 点：UI 统一到框架令牌与标准尺寸

**用户原话**：「该页面的布局是否还有优化的空间，我感觉整体的布局，字体，按钮，不够大气，
可以参考其他页面的设计，尽量统一字体，按钮，布局的尺寸，未来好统一进行优化」

| # | 问题 | 修法 |
|---|---|---|
| ① | 用 `hide-toolbar` + 自造 `WorkbenchBar`（44px 高 / 14px 内边距 / 自造下边框） | 改走 `YzhPageLayout` 的 `#toolbar` 插槽 —— 内边距/边框/高度由框架统一提供（**12px 20px**），与全站一致 |
| ② | 全部按钮 `size="small"`（28px），全站其他页面用默认（32px） | 去掉 `size="small"` —— 这是「不够大气」的直接原因 |
| ③ | 字号硬编码 `11/12/13/14px`（11px 不在令牌刻度上） | 全部改 `var(--yzh-font-size-xs/sm/md)`（12/13/14px） |
| ④ | 间距/圆角/颜色硬编码（`#c6e2ff` `#e4e7ed` `#409eff` `6px`…） | 全部改 `var(--yzh-space-*)` / `var(--yzh-radius-*)` / `var(--yzh-color-*)` |
| ⑤ | `title="文档语义规则"` 传给 `YzhPageLayout`，但该组件**没有 `title` prop**（只有 `pageTitle`，且模板未渲染） | 删除该死属性（落到根元素当 HTML 属性，无效果） |

**涉及 5 个组件**：`index.vue` / `WorkbenchBar.vue` / `StandardTree.vue` / `PromptEditor.vue` / `PromptTestPanel.vue` / `SemanticResult.vue`

### 2B.4 附赠：占位符 tooltip（直接解决「看不懂」）

`logic.ts` 新增 `PLACEHOLDER_DOCS` + `describePlaceholder()`：
9 个占位符各配「人话说明 + 数据源表名」；`PromptEditor.vue` 的 chips 挂 `el-tooltip`。
鼠标悬停即知 `{{tag_list}}` 运行时变成什么，**不必猜、不必问开发**。

**验证**：后端 `0 Error`｜守卫 `✓ 通过（20 规则 / 1011 文件）`｜`vue-tsc` `0 错误`

---

## 二·C、2026-10-03 第三轮（用户实测报 bug → 已完成）

### 2C.1 用户原话（逐字）

> 「我测试了逻辑上还有问题，我选择食品行业，点击 ai 自动生成，但我切换了作用提示词结果之前的提示词清空了，
>  我们如果有改动，立刻页面应该提示自动保存，或者 ai 自动生成的时候，进行自动保存，
>  我觉得你应该启动前端测试，测试页面的操作逻辑，并修复页面逻辑的相关问题」

### 2C.2 ★★★ 根因链（**不是「忘了保存」，是两个正确决定叠加出的错误结果**）

```
watch([scope, activeType]) 无条件调 loadCurrent() → 覆写 templateText
        ↑
generatePromptDraft 刻意不落库（源码注释：「★ 不落库，返回正文由用户确认后保存」）
        ⇒ AI 结果只存在 templateText 内存里
        ⇒ 点完「AI 生成」再切一个页签 = 一次几十秒 + 一次 token 换来的正文**静默消失**
```

`loadCurrent()` 按「作用域+类型」重载是对的；生成不落库（让用户先看再决定）也是对的。
**错在两者之间没有缓冲区。**

### 2C.3 修复（三层防御 + 2 处配套）

| # | 层 | 实现 | 为什么必须这样 |
|---|---|---|---|
| ① | **草稿暂存** | localStorage `yzh.prompt-workbench.drafts`，key = `${type}\|${scope \|\| '__platform__'}`，**700ms 防抖** | 存本机而非服务端：草稿是**编辑态**，不该污染 DB；刷新/误关标签页仍能恢复 |
| ② | **切换守卫** | `ElMessageBox.confirm` 三选一：**保存并切换 / 暂存草稿并切换 / 留在本页**（点 X·Esc = 留在本页） | 破坏性动作默认不执行 |
| ③ | **生成后自动落库** | `doSave({bypassGate:true, silent:true})` | 用户明确要求「ai 自动生成的时候，进行自动保存」 |
| ④ | P5 闸门 **硬拦 → 软提示** | 见 §1.8 | 原实现把用户卡死，且与③**直接冲突** |
| ⑤ | `StandardTree.resync()` | 拒绝切换时把 `el-tree` 高亮拨回 | `el-tree` 点击**立即**移动自己的高亮，而「要不要真切」由父决定 ⇒ 不拨回 = **界面在说谎** |

**★ 最容易踩的一个点 —— `syncing` 标志**：`loadCurrent()` 期间置 `true`，草稿 watch 里 `if (syncing) return`。
不加这个开关，切作用域时刚载入的**库内值**会被当成用户编辑写进草稿 ⇒ 草稿越滚越乱、下次载入拿旧草稿盖掉库内新值。
`finally` 里必须 `await nextTick(); syncing = false`。

### 2C.4 本轮同时扫出并修掉的 3 个真实缺陷

| # | 缺陷 | 后果 | 修法 |
|---|---|---|---|
| **F3** | AI 按钮文案用 `dirty` 判断，而 `onGenerate` 用 `!isBlankTemplate(正文)` 判断 | **正文已保存但非空**时按钮写「AI 生成」、实际执行「优化」⇒ 文案与行为不符 | 新增 `hasContent` computed 专供文案 |
| **F2** | `onSelectScope`/`onSelectType` 选「保存并切换」后**不检查 `doSave` 返回值** | 保存失败弹红字、界面却照样切走 ⇒ 用户以为内容丢了 | 失败则**不切**（作用域分支还要 `resync()`） |
| **F1** | AI 优化是**破坏性替换**（结果自动落库 + `dropDraft` 清草稿），手改内容静默消失 | 点一下「AI 优化」，自己改的内容没了且无人告知 | 在唯一的输入弹窗 message 里说清「原改动不会单独留存」 |

### 2C.5 ★★ 前端测试体系已建立（复利资产，已写进 `AGENTS.md` 强制约定第 12 条）

**跑法**：`cd src/certplatform-web/cert/cert-admin && ./node_modules/.bin/vitest run`
⚠️ vitest **只装在 `cert/cert-admin/node_modules`**；`vue-tsc` 反过来在 `certplatform-web/node_modules/.bin`（**两边不通用**）

**8 个用例**（`cert-admin/src/pages/workflow/prompt-template/index.test.ts`）：

| 用例 | 断言要点 |
|---|---|
| T1 | AI 生成后 `savePrompt` 被调 1 次且 `template` = AI 返回内容 |
| T2 | 切类型时 `confirm` 被调 1 次；选「留在本页」后正文不变、未落库 |
| T3 | 切走再切回，编辑器内容 = 草稿（不是库内旧值） |
| T4 | 保存状态在 已保存 / 未保存 之间正确切换 |
| T5 | 切换被拒时 `resolveActivePrompt` 只被调 1 次（未重新载入） |
| T6 | 保存失败时**不切换** |
| T7 | 已保存但非空 ⇒ 按钮显示「AI 优化」；正文空 ⇒ 「AI 生成」 |
| T8 | 有未保存改动时 `prompt` 的 message 含「未保存的改动」 |

**★ 两条 stub 铁律（踩过才知道）**：
1. 带**作用域插槽**的组件（`el-tree`）stub 必须 `v-for` 渲染 `<slot :data="n" />`。
   ⛔ 只写 `<slot />` ⇒ `#default="{ data }"` 拿 `undefined` ⇒ 渲染期抛
   `Cannot read properties of undefined (reading 'isPlatform')` ⇒ **全部用例倒在渲染**，
   与断言内容毫无关系，**极易误判为「修复没生效」**。
2. 组件用到的指令（`v-loading`）须在 `mount` 的 `global.directives` 补 stub。

**★ 守卫 R15 陷阱**：测试里写 `expect(ElMessageBox.confirm)` 会被 `/ElMessageBox\.confirm/`
当成产品代码的「无 catch 调用」⇒ 违规。改用 spy 引用断言。

**⛔ 本机无 Chromium**（`agent-browser` 未装、playwright 缓存只有 ffmpeg）⇒
页面**逻辑**验证走组件测试；「页面**能否编译**」用
`curl --noproxy '*' http://127.0.0.1:9990/src/<路径>.vue`（**500 = Vite 编译失败 = 白屏**）。

### 2C.6 真实后端链路验证（17/17 通过）

**脚本已落库**：`scripts/tools/smoke-prompt-workbench.mjs`（跑法见 §四 命令速查）
对 9001 那条提示词做**幂等重存**（内容逐字节不变，不新增/不删除行）后读回：

```
✓ 保存后读回：正文逐字节一致（2270 字）        ✓ 保存后读回：promptCode 未漂移
✓ 切类型后拿到的是「作用提示词」而非「分类提示词」（2724 字，不同行）
✓★ 切走再切回，分类正文仍在且一致              ✓★ promptCode 仍是原来那条
```

**登录端点**（写脚本时容易找错）：`POST /api/User/login`，
body `{UserName, Password, Captcha:'', Uuid:''}`（DEBUG 构建跳过验证码）；鉴权头 `Authorization: Bearer <Token>`。
⚠️ 是 `/api/User/login`，**不是** `/api/Auth/login`。⛔ curl 必须带 `--noproxy '*'`。

### 2C.7 验证结果（全绿）

后端未改（本轮纯前端 + 文档）｜`vue-tsc` **0 错误**｜前端守卫 `✓ 通过（20 条规则 / 1016 个文件）`｜
vitest **15/15 通过**（prompt-template 8 + doc-extraction-rule 7）｜Vite 转译 5 个模块全 200

---

## 三、待办

### P0 — 浏览器实测（必须人工，沙箱内做不了）

1. **重启后端**：`./scripts/backend/restart-backend.sh`
2. **前端在自己终端启动**（⛔ 不要从沙箱内启动，`broker.sock` ENOENT）：
   `scripts/frontend/start.sh auditor restart` 或按你的习惯
3. 打开 `http://localhost:9990/business/prompt-template`，逐项验证：
   - [ ] 标准树：根「全部标准」+ 各标准节点，节点下 `[分类]` `[作用]` 徽章是否随库内数据正确显示
   - [ ] 切换作用域/类型 → 正文正确载入（平台级回退时提示「显示平台级默认，保存将新建本标准版本」）
   - [ ] **AI 生成**（正文为空时）→ 弹框填补充要求 → 返回草稿
   - [ ] **AI 优化**（正文非空时）→ 确认是「优化」而非全量重写
   - [ ] **上传分析** → 转换日志页签能看到完整 Markdown → 结果页签渲染结论
   - [ ] **重测**（改提示词后不重传文件）→ 走 Redis 复用，`缓存命中` 标签出现
   - [ ] **保存闸门（已改软提示）**：未测试时点保存 → 应弹「未通过测试闸门 —— 未测试。仍要保存吗？」
         而**不是**按钮变灰；点「取消」应不保存
   - [ ] **Redis 降级**：停掉 Redis 容器后上传 → 应能正常转换分析（只是不能复用）
   - [ ] ★ **新：读一遍重写后的提示词**，确认「实施人员视角」是否真的好读了
     （点 9001 标准 → 分类提示词 / 作用提示词，逐节读；不满意的地方直接说，我再调）
   - [ ] ★ **新：占位符 tooltip** —— 鼠标悬停编辑器顶部 chips，应弹出「它运行时变成什么」
   - [ ] ★ **新：尺寸统一** —— 本页工具栏按钮应与「技能管理」等页面**同高同内边距**
   - [ ] ★ **新：跑一次「分析」** → 打开「提示词」页签，确认**没有残留的字面量 `{{...}}`**
     （若看到 `{{standard_name}}` 原样出现，说明模板里写了后端不认识的占位符名）

   **★★ 本轮（第三轮）新增 —— 复现用户报的 bug 与三条防丢机制：**
   - [ ] ★★★ **复现原 bug**：选「食品标准」→ 点「AI 生成」→ 切「作用提示词」→ **再切回「分类提示词」**，
         正文必须还在（且因已自动落库，切回时**不再弹守卫**）
   - [ ] ★★ **未保存状态提示**：手动改几个字 → 工具栏立刻出现橙色「未保存 · 已自动暂存」；
         点「恢复」→ 变绿「已保存」
   - [ ] ★★ **切换守卫**：有未保存改动时切类型/作用域 → 弹三选一；
         点 X 或 Esc → **留在本页**，且左树高亮要**拨回原节点**（不能出现「高亮跳了、内容没换」）
   - [ ] ★★ **AI 优化提示**：有未保存改动时点「AI 优化」→ 弹窗里应写明「原改动不会单独留存」
   - [ ] ★ **按钮文案**：正文非空（哪怕已保存）时按钮应显示「AI 优化」，不是「AI 生成」
   - [ ] ★ **刷新兜底**：改几个字不保存 → 直接刷新页面 → 正文应从草稿恢复，且状态显示「未保存 · 已恢复草稿」

### P1·B — 本轮新产生的待裁决项

**裁决 C：是否为第二个标准（`食品标准` / iso4001-2016）也建提示词行？**

现状（2026-10-03 实测）：`wf_prompt_template` **全表仅 3 行** —— 9001 的分类/作用 + 平台级 generator。
食品标准下 **0 行**，且平台级 `doc_group` / `doc_content` 也 **0 行** ⇒ 选「食品标准」时编辑器本来就是空的
（与用户描述一致，不是缺陷）。该标准**已有 10 条目录配置**（其中 4 条有数据），
所以「标准文档清单」这一层是有料的 —— 即**标准差异化 4 层已具备数据条件**。

- 选项 1：暂不建 —— 先把 9001 的提示词调到满意，再复制出第二套
- 选项 2（建议）：现在就用「AI 生成」在「食品标准」作用域下生成一版，
  正好**实测「按标准差异化」是否真的生效**（对比两条提示词的正文差异）

**裁决 D：新增标准的提示词，是「复制 9001 再改」还是「AI 从零生成」？**

按新规范 R5，标准差异集中在「二、本标准上下文」节 —— 理论上**其余 7 节可以逐字复用**。
若成立，可考虑做一个「从模板复制」按钮（比 AI 生成更可控、更快、零 token 成本）。

- 选项 1：只用 AI 生成
- 选项 2（建议）：加「复制到本作用域」按钮 —— 复制 9001 那版，人工只改差异节

### P1 — 需你裁决的两项

**裁决 A ✅ 已落地（2026-10-03）：保存闸门已由「硬拦」改为「软提示」**（详见 §1.8）。

用户裁决依据（逐字）：「针对我们当前页面，测试是需要完整数据的，既然是测试，我们的频率不会很高，
我们会反复用多个不同的文件验证提示词的准确性，同一个文件可能会反复调整提示词」
⇒ 硬拦会打断「改词 → 重测」的调试闭环，且与「AI 生成后自动保存」直接冲突。

**裁决 B：`ConvertLogItem.Markdown` 是否限制长度？**

当前**全量回传**完整 Markdown 正文（`PromptWorkbenchService.cs` L432 `Markdown = c.Markdown`）。

- 实测量级（更正 2026-10-02 的估算）：`MaxTestFiles = 20`，按单份 3 万字 Markdown 估算
  ≈ **1.8 MB**；极端 20 份 × 10 万字 ≈ **6 MB**。**不是此前说的 40MB。**
- 本机/内网开发完全可接受；若将来开放外网或单份文件极大，再加单文件上限（如 100KB 截断）
- **建议：暂不改**。「转换日志」页签要看完整 Markdown 才能诊断转换质量（表格是否转对），
  截断会削弱这个核心调试能力

### P2 — 系统参数与安全

4. **`ai_max_tokens` 当前 4096**：分类提示词输出 JSON 可能超限被截断
   （`TryRepairTruncatedJson` 的存在即证据）。建议提到 **8192**。
   `qwen-turbo` 真实规格：最大输入 98,304 / 最大输出 16,384 / 上下文 131,072
   —— 4096 是**我们自设的上限**，不是模型能力。
5. **`ai_api_key` 明文存 DB**，且曾在会话上下文中被打印。建议轮换 key + 后端日志脱敏。
6. **`qwen-long` 不支持结构化输出** ⇒ 不能用于 `doc_content`（试跑 `ForceJson=true` 会失败）。

### P3 — 配套（非本页阻塞）

7. **`cert_standard_doc_contract`（标准文档契约）为空（0 条）** ——
   `{{std_doc_catalog}}` 占位符注入的是空清单，提示词不知道「标准侧要什么文档」。
   **这是当前影响提示词效果的最大缺口。**
   > ✅ 更正：`cert_tag_dict` **不是空的**，实查有 **27 条**（业务过程 15 / 形态 8 /
   > 体系域 3 / 兜底 1），且 `MatchFeature`（L0 编号前缀规则）已填
   > ⇒ `{{tag_list}}` 与 `{{code_prefix_map}}` 的原料**是齐的**。
8. **6 个 `doc_*` Skill 注册到 `wf_skill`**：先核 `wf_skill_reflection` 的 10 条 active。
9. **并行会话 28/29/31/32 号文档合并**（已查出 6 处缺陷，待裁决）。

### P4 — 记忆纠正

10. `MEMORY.md` 的「YzhForm 不读 dictCode」已于 10-02 修正。
    本文件（`04-下一步TODO.md`）的 10-02 版本已整体过时（描述的还是三栏布局 + 文件即弃），
    **已由本版本替换**。

---

## 四、命令速查

```bash
# 后端编译（★ 必须串行，只编 Web 项目）
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台
/Volumes/Expand/wangqingquan/.dotnet/dotnet build src/yzh-core/YZH.Core.Web/YZH.Core.Web.csproj -v q

# 重启后端
./scripts/backend/restart-backend.sh

# 前端守卫 + 类型检查
cd src/certplatform-web && node scripts/guards.mjs
cd src/certplatform-web/cert/cert-admin && ../../node_modules/.bin/vue-tsc --noEmit
#   ⚠️ vue-tsc 在 certplatform-web/node_modules/.bin（不在 cert-admin 里）

# ★ 前端单元测试（2026-10-03 建立，改本页必跑）
cd src/certplatform-web/cert/cert-admin && ./node_modules/.bin/vitest run
#   ⚠️ vitest 只装在 cert/cert-admin/node_modules；只跑单文件：
#   ./node_modules/.bin/vitest run src/pages/workflow/prompt-template/index.test.ts

# ★ 页面「能否编译」自检（无需浏览器；500 = Vite 编译失败 = 白屏）
curl -s --noproxy '*' -o /dev/null -w "%{http_code}\n" \
  "http://127.0.0.1:9990/src/pages/workflow/prompt-template/index.vue"

# ★ 提示词工作台接口冒烟（后端 9992 需在运行；幂等，不污染数据）
node scripts/tools/smoke-prompt-workbench.mjs

# 查提示词表
docker exec yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 -N -B yzh_cert_platform -e "
SELECT PromptCode, PromptType, StandardCode, IsActive, CHAR_LENGTH(Template) AS Len
FROM wf_prompt_template WHERE IsDeleted=0 ORDER BY PromptType;"

# 查 Redis 里的 Markdown 缓存
docker exec yzh-redis redis-cli -p 6379 --scan --pattern 'prompt_md:*'

# 查标签字典（P3 的缺口）
docker exec yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 -N -B yzh_cert_platform -e "
SELECT COUNT(*) AS TagCount FROM cert_tag_dict WHERE IsDeleted=0;"
```

---

## 五、已知风险（诚实版）

1. **未浏览器实测**：守卫 + 类型检查全绿只证明「能编译、不违架构」，不证明交互正确。
   上传 multipart、AI 生成弹框、保存闸门、Redis 复用路径都需人工走一遍。
2. **`{{std_doc_catalog}}` 原料为空**（`cert_standard_doc_contract` = 0 条，见 P3-7）——
   占位符机制已通，但注入的是空清单，提示词不知道标准侧期望哪些文档。
   （`{{tag_list}}` / `{{code_prefix_map}}` 的原料 `cert_tag_dict` 有 27 条，**是齐的**。）
   **这是当前最影响「提示词好不好用」的因素，优先于任何界面优化。**
3. **Redis 降级是静默的**：`PromptMarkdownCache` 连接失败只记 warning 日志，
   界面表现为「重测按钮点了没反应 / 提示缓存过期」。排查时先看后端日志有没有
   `[PromptMdCache] 连接失败`。
4. **`PromptTemplateService.SaveAsync` 的三行参数保留**是**刻意**的（见 1.5），
   重构时容易被当成冗余代码删掉 —— 删了会让 NC 链路的行级模型参数变 NULL。
5. **`workbench/test` 的 `[RequestSizeLimit(200_000_000)]`** 是 200MB，
   与 `MaxTestFiles = 20` 配合。若单文件极大仍可能撑爆内存（整个文件读进 `MemoryStream`）。
