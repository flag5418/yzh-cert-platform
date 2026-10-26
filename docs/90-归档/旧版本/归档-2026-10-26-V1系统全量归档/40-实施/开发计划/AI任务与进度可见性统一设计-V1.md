# AI 任务与进度可见性统一设计

> **版本**：V1 | **日期**：2026-10-16 | **状态**：**已裁决（8 条）· 已实施（待人工验收）**
>
> **触发**：用户 2026-10-16 提出 —— 「我们很多地方都有 AI 分析这类的按钮，只是在按钮上有个分析的状态，
> 但并不是当前页面或全局的，这样可能会造成用户的误解、不确定，为什么打开了一个页面，为什么没有任何信息」
>
> **关联**：`64`（专家端企业资料规范化实施规格）· `67`（文档约束与字段收敛）· `37`（标准文档填写规则）
> **性质**：**跨页面交互一致性**设计，⛔ 不是单页 bug 修复
>
> ⚠️ **本轮范围修正（用户 2026-10-16 追加）**：「我们有**后台管理端和专家端 2 个系统**，
> 本次的改动应该要覆盖 **2 套前端系统**」 ⇒ 见 **§6.1 事实修正**（两端**不是**同一个布局）。

---

## 一、问题陈述

### 1.1 用户原话拆解

| 用户描述 | 我的解读 |
|---|---|
| 「很多地方都有 AI 分析这类按钮」 | 至少 3 个页面有同步 AI 按钮，形态各异 |
| 「只是在按钮上有个分析的状态」 | 状态只挂在**按钮的 `loading`** 上 |
| 「并不是当前页面或全局的」 | ⛔ 缺**页面级**与**全局级**两层 |
| 「为什么打开了一个页面，为什么没有任何信息」 | 重进页面时，**看不出**「没分析过 / 分析过 / 失败过 / 正在分析」 |

### 1.2 一句话结论

> **问题成立。** 根因不是「忘了加 UI」，而是 **AI 分析走的是「同步 HTTP 直调」，它天生没有「可被观察的状态」** ——
> 没有任务标识、没有可查询的进度、没有进度推送 ⇒ 从技术上就**做不出**页面级 / 全局级提示。
> 而项目里**另一条路径（走队列的上传 / 转换）三层可见性全都有**，于是形成**同一个事实、两种可见性**的不一致。

---

## 二、现状盘点（2026-10-16 实测）

| # | 页面 | 触发方式 | 状态表达 | 并发计数 | 页面级 | 全局级 |
|---|---|---|---|---|---|---|
| 1 | `doc-fill-rule`（文档填写规则） | **打开自动** + 手动按钮 | 按钮 `loading` + `v-loading` 遮罩 + 底部进度条 + 三种 `ElMessage` | ✅ `analyzeRuns` 计数 | ⚠️ 仅遮罩，**无状态文案** | ❌ |
| 2 | `doc-extraction-rule`（文档抽取规则） | 手动按钮 | 按钮 `loading` + `ElMessage` | ❌ **单 `boolean`** | ❌ | ❌ |
| 3 | `prompt-template`（提示词模板测试） | 手动按钮 | 按钮 `loading` + `ElMessage` | ❌ **单 `boolean`** | ❌ | ❌ |
| 4 | `directory`（标准目录管理） | 上传后**入队** | **`queue-banner` 横幅 + 进度条** | ✅ 队列天然 | ✅ | ⚠️ 仅 push 到消息铃铛 |
| 5 | `cert-auditor/enterprise-original` | 上传后**入队** | **`queue-banner` + 页头状态行 + 「处理进度」按钮** | ✅ 队列天然 | ✅ | ⚠️ 同上 |

### 2.1 盘点结论

- **走队列的 2 个页面（#4 #5）**：页面级横幅 + 进度条 + 进度推送 —— **做得对，是范式**
- **同步直调的 3 个页面（#1 #2 #3）**：只有按钮转圈 —— **问题区**
- ⚠️ **倒退缺陷**：#1 已有并发计数 `analyzeRuns`（防重复发起），但 #2 #3 用**单 `boolean`** ——
  两次请求先后 `finally` 会把先结束的那次置 `false`，另一还在跑时按钮却恢复可点 ⇒ **同一文件被重复发起、重复烧 LLM**。

### 2.2 `AnalyzeStatus` 被消费了吗？（#1 的关键实测）

`AnalyzeStatus`（`pending` / `completed` / `partial` / `failed` / `manual`）**只被用于两件事**：

1. `logic.ts:886` —— 判断「缺项」时 push 一句 `'AI 语义分析'`（混在**缺项清单**里）
2. `index.vue:451` —— 决定要不要**自动跑**

⇒ ★ **页面上没有任何地方把 5 个取值渲染成一个明确的「状态」**。
用户看到的只是底部进度条里一行「缺：AI 语义分析」——**这就是「打开页面没有任何信息」的直接原因**。

---

## 三、根因（三层）

### 3.1 技术根因：同步直调 vs 队列

| 维度 | 队列驱动（#4 #5） | 同步直调（#1 #2 #3） |
|---|---|---|
| 任务标识 | ✅ `QueueCode` | ❌ 无 |
| 状态存储 | ✅ 落库（队列表） | ❌ 只活在一次请求里 |
| 进度推送 | ✅ `QueueManager.RefreshQueueProgressAsync` | ❌ 无 |
| 可取消 | ✅ | ❌ |
| 切走 / 刷新后 | ✅ 仍在，可恢复 | ❌ 丢失，无痕迹 |

★ **唯一进度推送点** = `YZH.Core.DataBase/Services/QueueManager.cs:679 RefreshQueueProgressAsync`
⇒ **不走队列 = 没有推送 = 没有全局可见性**。这是硬约束，不是 UI 层能绕过的。

### 3.2 不一致根因

同一个「AI / 后台正在干活」的事实，**两条技术路径给出两种可见性**：

- 走队列 ⇒ 页面横幅 + 进度条 + push 到消息铃铛
- 同步直调 ⇒ 只有按钮转圈

用户**无法理解为什么**有的页面有进度、有的没有 —— 而这正是「用户视角铁律」禁止的：
⛔ **不能把内部实现差异（同步 / 异步）暴露成用户的困惑**。

### 3.3 用户困惑的四个具体场景（#1 为例）

| 场景 | 现状 | 用户感受 |
|---|---|---|
| **打开页面** | `AnalyzeStatus` 非 `pending` 就**什么都不做、什么都不说** | 「为什么打开了一个页面，没有任何信息」 |
| **打开页面（已完成但没结论）** | 只在「缺项」里列一条 | 「到底分析没分析？」 |
| **分析中切走再回来** | `autoAnalyzed` 是**内存 `Set`**，重进即丢 ⇒ 要么重复烧 LLM，要么不跑且无提示 | 「刚才那个转圈去哪了？」 |
| **切到别的页面** | 完全看不到「有 AI 任务在跑」 | 「我的分析还在跑吗？」 |

---

## 四、已有基础设施（★ 结论：底座全都有，不用新建）

| 能力 | 现成实现 | 位置 |
|---|---|---|
| **全局插槽位** | `YzhAppLayout` 的 `#header-right`（已放 `MessageBell`） | `cert-admin/src/layouts/CertAdminLayout.vue` |
| **两端共享组件层** | `cert-share/src/components/`（`MessageBell` 即此层，299 行） | `cert-share` |
| **push 通道** | `yzhPush` + `YzhPushMessage.QueueProgress` + SignalR Hub | `yzh.vue.core/api/push.ts` · `YZH.Core.Web/Hubs/` |
| **消息中心** | `MessageBell`（含 `queue` 类型标签「队列通知」）+ `/system/message` | `cert-share/components/MessageBell/` |
| **队列 + 资源锁** | `QueueManager`（**单例，两端共用**） | `YZH.Core.DataBase/Services/QueueManager.cs` |
| **队列查询端点** | `POST /api/Admin/System/QueueMonitor/status` · `/list`（可按 `Status` 过滤） | `Admin/Controllers/System/QueueMonitorController.cs` |
| **页面级横幅范式** | `queue-banner`（含自动轮询 + 进度条 + 详情） | `directory/index.vue` · `enterprise-original/index.vue` |
| **「AI 分析走队列」现成范例** | `EnterpriseOriginalAnalyzeExecutor`（`IYzhTaskExecutor`，`TaskType = enterprise_original_analyze`） | `Auditor/Services/Ent/` |

⇒ ★★ **本轮不需要发明任何新底座**。缺的只是：**把同步 AI 分析这条路径「接上」已有底座**，以及**把状态文案补出来**。

---

## 五、★ 用户裁决（2026-10-16，共 8 条）

### 5.1 用户原话

> 「1、我的建议是做**全屏**，但**只是整体页面的右边区域**，并不影响用户切换菜单，**只针对当前页面**才显示，
> 如果切换了其他页，如果该页面没有等待这不显示，
> 2、我们整体页面布局，**没有打开页面的标签**，就造成了一个问题，我在另一个页面比较增加了文档，回到规则页面，
> 页面没有刷新（当前这个不是问题），麻烦的时候，我还需要从菜单中重新找之前的页面，
> 我们的**打开的标签不能重复，能批量关闭**，
> 3、**挂在 `YzhAppLayout`**，
> 4、是否接受「只接长耗时操作」的判据 —— 这个**根据情况来添加**，不会每个按钮都加入，这样很奇怪，
> 5、**队列任务本轮不进这个横幅**，
> 6、「打开页面空白」那半（页面级状态文案）这个**暂时不考虑**，
> 7、`doc-extraction-rule`/`prompt-template` 的单 `boolean` 缺陷**本轮一并一起修**，
> 8、`manual`（人工录入）下跑 AI 是否覆盖人工结论 —— **不覆盖**」

### 5.2 裁决对照（相对本设计 V1 原稿）

| # | 事项 | 原稿建议 | **用户裁决** |
|---|---|---|---|
| ① | 形态 | 非阻塞**顶部横幅** | ★ **内容区遮罩**（"全屏，但只是右边区域"）：盖住 `el-main`，**不盖**侧边栏 / 顶栏 / 标签栏 ⇒ 期间仍可切菜单 |
| ② | 作用域 | 全局 | ★ **按页面过滤**：只在**发起它的那个页面**显示；切走不显示、切回再现 |
| ③ | 挂载 | `YzhAppLayout` | ✅ 同（但见 §6.1 的两端事实修正） |
| ④ | 接入范围 | 只接长耗时 | ✅ **逐处判断**，⛔ 不是每个按钮都加 |
| ⑤ | 队列任务 | 不进 | ✅ **不进**（`queue-banner` + `queue_progress` 已覆盖） |
| ⑥ | 页面级状态文案 | 下一批 | ✅ **本轮不做** |
| ⑦ | 单 `boolean` 缺陷 | 一并修 | ✅ **一并修** |
| ⑧ | `manual` 下跑 AI | 不覆盖 | ✅ **不覆盖** |

### 5.3 新增需求：多标签页（原稿没有，用户裁决 ② 带出）

用户报的真实痛点：**「我在另一个页面增加了文档，回到规则页面……麻烦的时候，我还需要从菜单中重新找之前的页面」**
⇒ 加**标签页栏**，硬约束两条：**① 同一页面不重复开标签 ② 支持批量关闭**。

> ⚠️ 用户同时明确「**页面没有刷新（当前这个不是问题）**」——
> ⇒ 标签**只管导航**，⛔ **不改** `keep-alive` 缓存策略（保持"缓存所有访问过的页面"现状）。

### 5.4 为什么这个方案好（我的评估）

1. **成本可控**：**纯前端**，⛔ 不碰后端、⛔ 不改端点语义（同步仍是同步）
2. **覆盖面广**：一次改造，所有异步等待都受益 —— 不止 AI 分析，还有上传 / 转换 / 扫描 / 打包导出
3. **不锁界面**：遮罩**只盖内容区**（用户裁决 ①），15~25 秒的 AI 分析期间仍可切菜单、开标签
4. **不引入新风险**：不涉及"同步 → 异步"语义变更，不动 LLM 并发 / 重试 / 取消
5. ★ **用户的核心洞察成立**：「我们能控制某个按钮的后台执行状态」—— 每个按钮的 `loading` 已经存在，
   缺的只是**把它上抛到一个全局的地方**

---

## 六、设计（已实施）

### 6.1 ★★ 落位（含一处**事实修正**）

| 层 | 落点 | 内容 |
|---|---|---|
| **机制** | `yzh.vue.core/src/composables/useGlobalPending.ts`（新建） | 模块级单例 + `begin` / `end` / `update` / `clearScope` |
| **便捷包裹** | `yzh.vue.core/src/composables/useAsyncAction.ts`（新建） | **并发计数** + 自动接全局等待 + 超时兜底 |
| **标签机制** | `yzh.vue.core/src/composables/useTabs.ts`（新建） | 模块级单例：`open`（**幂等，不重复**）/ `close` / `closeOthers` / `closeRight` / `closeAll` / `retitle` |
| **UI · 遮罩** | `yzh.vue.core/src/layouts/components/YzhPendingOverlay.vue`（新建） | 内容区遮罩（转圈 + 文案 + 可选的**单项**进度条） |
| **UI · 标签栏** | `yzh.vue.core/src/layouts/components/YzhTabsBar.vue`（新建） | `el-tabs type="card"` + 「批量关闭」下拉 |
| **挂载 ①** | `yzh.vue.core/src/layouts/YzhAppLayout.vue` | 管理端（`cert-admin` 经 `CertAdminLayout` 用它） |
| **挂载 ②** | `cert/cert-auditor/src/layouts/AuditorLayout.vue` | 专家端（**手写布局**） |

#### ★★★ 事实修正：两端**不是**同一个布局

本设计原稿（§6.1）写的「`YzhAppLayout` 是两端唯一顶层布局 ⇒ 一次挂载、两端生效」**是错的**。
实测（2026-10-16）：

| 端 | 布局文件 | 是否复用 `YzhAppLayout` |
|---|---|---|
| 管理端 `cert-admin` | `cert-admin/src/layouts/CertAdminLayout.vue`（薄壳） | ✅ 是（`<YzhAppLayout v-bind="$props">`） |
| 专家端 `cert-auditor` | `cert-auditor/src/layouts/AuditorLayout.vue`（**手写，约 275 行**） | ❌ **否** —— 只是"样式逐条对齐"，实现是独立的 |
| `cert-enterprise` | — | 空壳（仅 `src/index.ts`），无布局 |

⇒ **必须挂两处**。两端共用的是 **`yzh.vue.core` 里的组件与 composable**（`@yzh-core` 别名两端都已配），
⛔ 不是布局本身。`useTabs` / `useGlobalPending` 是**模块级单例**，而两端是**两个独立 bundle**
⇒ 各持一份实例，**互不串扰**（这点是安全的）。

### 6.2 核心 API

```ts
// ① 机制层：模块级单例（⛔ 不用 pinia —— yzh.vue.core 不依赖任何 store 实现）
export interface YzhPendingItem { key: string; label: string; scope: string; progress?: number; startedAt: number }
export function useGlobalPending() {
  return { pendings, count, begin, end, update, clearScope, clearAll }
}
/** 按页面过滤（★ 传 getter 而非路径值 —— 路径会变，传值会失去响应式） */
export function usePendingByScope(getScope: () => string) { ... }

// ② 便捷层（★ 推荐用法）
const { busy, run } = useAsyncAction()
// 模板：<el-button type="primary" :loading="busy" @click="run(() => doAnalyze(), '正在分析：XX.docx')">
```

`useAsyncAction` 契约（★ 两条关键）：

- `busy` = **并发计数 `runs > 0`**（⛔ 不用单 `boolean` —— 沿用 `doc-fill-rule` 的 `analyzeRuns` 教训）
- `run(fn, label)` = `begin(label, { scope: route.path })` → `await fn()` → `finally { end(key) }`
  （★ **`finally` 保证不漏调**；另加 **120s 超时兜底**，防请求卡死时遮罩不消失）

#### ⚠️ 两个必须记住的用法坑

| 坑 | 说明 |
|---|---|
| **必须解构** | `busy` 是 `ComputedRef`。写成 `const a = useAsyncAction()` 再在模板用 `a.busy` —— **模板不会自动解包**（自动解包只对顶层 ref 生效）⇒ 传进 `:loading` 的是 ref 对象，**恒为真**。必须 `const { busy, run } = useAsyncAction()` |
| **`inject` 只能在 setup 期调** | `useAsyncAction` 内部要 `inject(routeLocationKey)` 取路由对象。**不能在 `run()` 里 `inject`**（那是事件回调期）⇒ 实现是"setup 取对象、run 读 `.path`"。该对象是 `shallowReactive(currentRoute)` ⇒ 值恒为当前路由 |
| **无 router 上下文要容错** | 单测 `mount(页面)` 只给 stubs、不给 router（见 `prompt-template/index.test.ts`）⇒ 用 `inject(routeLocationKey, null)` 带默认值**静默降级**为 `''`（遮罩永不命中 = 不显示），⛔ **不能裸用 `useRoute()`**（会刷 injection 警告） |

### 6.3 UI 形态

| 项 | 值 |
|---|---|
| **位置** | `el-main` 内的 `position:absolute; inset:0`（★ 覆盖整个内容区，⛔ 不盖侧边栏 / 顶栏 / 标签栏） |
| **面板** | 居中卡片：转圈图标 + 文案 + 「另有 N 项任务进行中」（>1 项时）+ `el-progress`（**仅恰好 1 项且有 progress** 时） |
| **底衬** | `.yzh-pending::before` 承载 `background: var(--yzh-color-bg-page); opacity: .86`（⛔ 不直接影响面板不透明度，避免文字发虚） |
| **过滤** | `scope === route.path` ⇒ 在 A 页发起、切到 B 页**不显示**；切回 A 页**再现** |
| **结束** | `v-if` 移除 + `Transition` 淡出，⛔ 不残留「已完成」痕迹 |

★ **为什么滚动必须下移**：`el-main` 若仍 `overflow: auto`，`inset:0` 的遮罩会**随内容滚走**。
⇒ 把 `padding` / `overflow-y` 下移到内层 `.yzh-layout__scroll`（专家端为 `.auditor-layout__scroll`），
`el-main` 变成**不滚动的定位父级**。内层 padding 用原值（`--yzh-space-6` = 24px / `--yzh-space-5` = 20px）
⇒ 视觉与改造前**逐像素一致**。

### 6.4 ★★ 关键设计决策

#### ① 遮罩只盖内容区（用户裁决 ①）

用户说"做全屏，但只是整体页面的右边区域" ⇒ **覆盖 `el-main`，不盖侧边栏 / 顶栏 / 标签栏**：

- 同步 AI 分析要 **15~25 秒**，全屏遮罩会**锁死界面**
- 只盖内容区 ⇒ 期间**仍可切菜单、开标签**（这正是"不影响用户切换菜单"）

#### ② 并发多项：计数

同一时刻可能有多个等待（"分析 A" + "上传 B"）⇒ 显示主文案 + 「另有 N 项任务进行中」。

#### ③ ★★ 哪些操作该接？（判据，⛔ 不能全接）

**判据 = 「预计 > 3 秒 且 用户可能想切走」**，且**逐处判断**（用户裁决 ④：「不会每个按钮都加入，这样很奇怪」）

| 该接 ✅ | 不该接 ❌ |
|---|---|
| 调 **LLM** 的（AI 分析 / AI 生成 / AI 验证 / 提示词测试） | **列表加载**（`useFileTree` / `MessageBell` / `DocPreview`） |
| 等**外部服务**的（LibreOffice 试填转 PDF / 打包 ZIP / 文件扫描） | **详情读取**（秒内返回） |
| **长计算**（规范化"干跑"预览：多表 JOIN） | **保存配置**（秒内返回） |
| — | **队列任务**（裁决 ⑤） |

★ **为什么必须筛**：全仓实测有 **30+ 处** `xxx = ref(false)` 异步状态；若**全接**，遮罩会**一直闪**，
反而变成噪音 —— 与"减少困惑"的目标相反。

#### ④ 队列任务：本轮 ⛔ **不进**（用户裁决 ⑤）

- 队列已有自己的 `queue-banner`（页面级）+ `queue_progress` 推送 + 消息铃铛
- ⇒ 本轮 ⛔ 不动队列，避免与现有 `queue-banner` **语义重叠**（两个横幅同时出现会更乱）

---

## 七、★ 覆盖边界（必须诚实说明，⛔ 不假装解决）

| 场景 | 全局等待提示能解决吗 |
|---|---|
| 点了按钮，**切到别的页面**，不知道还在不在跑 | ⚠️ **部分** —— 遮罩是全局**存在**的，但按裁决 ② **只在发起页显示**；切回即见（这是刻意的：不打扰别的页） |
| 点了按钮，**切走再回来** | ✅ 解决（页面状态由 `keep-alive` 保留） |
| 打开页面，**什么都没点**，为什么空白 | ❌ **不解决** —— 没有等待在进行，遮罩不会出现（用户裁决 ⑥：本轮不做） |
| **F5 刷新后** | ❌ 不解决 —— 前端状态全丢，同步请求也已断 |

> ★ **本方案的边界一句话**：
> 它解决「**我正在跑，你不知道**」；⛔ **不解决**「**我没在跑，你也不知道为什么空白**」。
>
> 后半句才是用户最初那句「打开了一个页面，为什么没有任何信息」的**直接原因** ——
> 但它的解法是**页面级状态文案**（§八），与「全局等待提示」是**两件不同的事**（裁决 ⑥：暂时不考虑）。

---

## 八、（备选）页面级状态文案 —— 边界外补充，**本轮不做**（用户裁决 ⑥）

`AnalyzeStatus` 的 5 个取值**当前全项目只被消费 2 处**（算"缺项" + 决定要不要自动跑），
**页面上没有任何地方把它渲染成状态** —— 这是"打开页面空白"的直接原因。

**成本极小**（值已有，只是没渲染），但**性质不同**：一个是"全局等待"，一个是"页面状态"。

| 内部值 | 文案（用户视角） | 动作 |
|---|---|---|
| `pending` | 这份文档还没做过 AI 分析 —— 分析后可自动带出分组与文档作用，约 20 秒 | 开始 AI 分析 |
| `completed` | AI 分析已完成 · 已带出 N 项 | 重新分析 |
| `partial` | AI 分析已完成，但只带出部分结论 · 缺：<哪几项> | 重新分析 |
| `failed` | AI 分析失败：<原因> | 重试 |
| `manual` | 这份文档的分组与作用是人工录入的，未跑 AI | 跑 AI 分析（⚠️ **不覆盖**人工结论，裁决 ⑧） |

---

## 九、裁决结果（8 条，✅ 全部已裁）

| # | 事项 | **裁决** |
|---|---|---|
| ① | 形态：非阻塞横幅 vs 全屏遮罩 | ✅ **内容区遮罩**（全屏但只盖右边区域，不阻塞切菜单） |
| ② | 作用域：全局 vs 按页面 | ✅ **按页面**（`scope === route.path`，切走不显示、切回再现） |
| ③ | 挂载位置 | ✅ **`YzhAppLayout`** —— ⚠️ 实测专家端**不复用**该布局 ⇒ 另挂 `AuditorLayout`（见 §6.1） |
| ④ | 只接长耗时操作 | ✅ **逐处判断**，⛔ 不是每个按钮都加 |
| ⑤ | 队列任务本轮不进 | ✅ **不进** |
| ⑥ | 页面级状态文案 | ✅ **暂时不考虑**（降级为 §八 备选） |
| ⑦ | `doc-extraction-rule` / `prompt-template` 单 `boolean` 缺陷 | ✅ **本轮一并修** |
| ⑧ | `manual` 下跑 AI 是否覆盖人工结论 | ✅ **不覆盖** |

**另附需求**：多标签页（不重复、可批量关闭）—— 见 §5.3、§六。

---

## 十、验收判据

| # | 判据 | 方法 | 状态 |
|---|---|---|---|
| 1 | 任意页面点「AI 分析」，**切走再切回**仍能看到等待遮罩 | 人工目视 | ⏳ 待跑 |
| 2 | 操作结束，遮罩**自动消失**，⛔ 不残留 | 人工目视 | ⏳ 待跑 |
| 3 | 切到**别的**页面时，该页**不显示**遮罩（裁决 ②） | 人工目视 | ⏳ 待跑 |
| 4 | **列表加载 / 保存配置 / 队列任务不触发**遮罩 | 人工目视（回归：不该吵的地方不吵） | ⏳ 待跑 |
| 5 | 连续点两次「分析」，**只发起一次** | 单测（并发计数） | ✅ 已覆盖 |
| 6 | 标签**不重复**：同一 path 点两次只有一个标签 | 单测 | ✅ 已覆盖 |
| 7 | **批量关闭**：关闭其他 / 关闭右侧 / 关闭全部 | 单测 + 人工目视 | ✅ 单测已覆盖 · ⏳ 目视待跑 |
| 8 | 内容区**滚动仍正常**（遮罩改造未破坏滚动） | 人工目视 | ⏳ 待跑 |
| 9 | 基线不破 | 守卫 0 违规 · `vue-tsc` 5 包 0 · `vitest` 全绿 | ✅ 见 §十二 |

> 第 5~7 条由 `cert/cert-admin/src/layouts/globalPendingAndTabs.test.ts`（21 例）覆盖；
> 第 1~4、8 条属**浏览器目视**范畴，单测无法替代，留待人工验收。

---

## 十一、风险

| # | 风险 | 处置 |
|---|---|---|
| R1 | 遮罩「一直闪」变噪音 | 靠 §6.4③ 的判据筛（只接长耗时操作） |
| R2 | 同步请求若中途异常，`end` 漏调 ⇒ 遮罩**卡住不消失** | `useAsyncAction` 用 `try/finally` 强制 `end`；另加**超时兜底**（120s 自动清）；组件卸载时 `onScopeDispose` 再兜一层 |
| R3 | 遮罩盖住内容区 ⇒ 用户以为页面卡死 | 面板文案是**动词 + 对象**（如「正在分析文档：XX.docx」），并提示「另有 N 项」；且**侧边栏 / 标签栏仍可点** |
| R4 | 两端布局**手写两份** ⇒ 改一处漏一处 | 两端只挂载、不实现 —— 组件与 composable 都在 `yzh.vue.core`（唯一实现）；本节 §6.1 已把"两处挂载"写成事实，⛔ 不再假设"一次生效" |
| R5 | `keep-alive` 无 `include` ⇒ 缓存所有访问过的页面 | **保持现状**（用户明确「页面没有刷新不是问题」）；标签⛔ 不控制缓存 |
| R6 | 标签标题与面包屑**两处维护** | 同一份 `useMenuTree` 菜单树 + 同一个"按 `url === path` 递归查找"算法（见组件内注释） |

---

## 十二、实施记录（2026-10-16）

### 12.1 新建（`yzh.vue.core`）

| 文件 | 作用 |
|---|---|
| `src/composables/useGlobalPending.ts` | 全局等待机制（模块级单例，含 `clearScope`） |
| `src/composables/useAsyncAction.ts` | 并发计数 + 全局等待 + 超时兜底（含无 router 容错） |
| `src/composables/useTabs.ts` | 多标签页机制（幂等 open + 批量关闭） |
| `src/layouts/components/YzhPendingOverlay.vue` | 内容区等待遮罩 |
| `src/layouts/components/YzhTabsBar.vue` | 标签页栏 + 批量关闭下拉 |

> ⚠️ 两个 UI 组件放在 `src/layouts/components/`（与 `YzhMenuNode.vue` 同处），
> ⛔ **不是** `src/components/layout/` —— 守卫 **R1** 要求 `components/` 下的原子组件**零领域依赖**，
> 而这两个组件依赖 `vue-router`（读 `route.path` / 做 `router.push`）。
> ⇒ 它们是**布局内部件**，也不从公共出口导出。

### 12.2 改造

| 文件 | 改动 |
|---|---|
| `yzh.vue.core/src/layouts/YzhAppLayout.vue` | `el-header` 后插 `<YzhTabsBar />`；`el-main` 内加滚动容器 + `<YzhPendingOverlay />` |
| `cert-auditor/src/layouts/AuditorLayout.vue` | 同上（★ 专家端**手写布局**，必须单独改） |
| `yzh.vue.core/src/composables/index.ts` · `src/index.ts` · `src/components/layout/index.ts` | 桶文件导出调整 |

### 12.3 接入的长耗时操作（★ 两端都覆盖）

| 端 | 页面 | 操作 | 接法 |
|---|---|---|---|
| 管理端 | `doc-fill-rule` | AI 语义分析（**自动 + 手动共用同一 action**） | `useAsyncAction()` |
| 管理端 | `doc-fill-rule` | 试填 + 转 PDF（B7） | `useAsyncAction()` |
| 管理端 | `doc-extraction-rule` | AI 分析 / 生成提示词 / 验证 | `useAsyncAction()` ×3 |
| 管理端 | `doc-extraction-rule` | 保存规则（防并发，⛔ 不接遮罩） | `useAsyncAction({ global: false })` |
| 管理端 | `prompt-template` | AI 生成 / 提示词测试 | `useAsyncAction()` ×2 |
| 管理端 | `prompt-template` | 保存（防并发，⛔ 不接遮罩） | `useAsyncAction({ global: false })` |
| **专家端** | `enterprise-normalize/create` | **规范化"干跑"预览**（多表 JOIN，只读） | `useAsyncAction()` |
| **专家端** | `enterprise-review` | **按范围打包导出 ZIP** | `useAsyncAction()` |
| **专家端** | `enterprise-review`（账本抽屉） | 改写 / 钉住取值（**写库**，防并发） | `useAsyncAction({ global: false })` |
| **专家端** | `tasks/detail` | 仅生成执行队列（**写库**，防并发） | `useAsyncAction({ global: false })` |

**⛔ 明确不接**（按裁决 ⑤ / §6.4③）：

- 队列类：规范化**入队**（`executeRun`）、重试失败转换（`onRetryFailed`）、`enterprise_original_analyze`、
  报告生成、NC 检测 —— 进度归各自的 `queue-banner` / 队列页
- 秒级类：各页的列表加载 / 详情读取 / `contractLoading` / `uploading`（有自身进度）

### 12.4 基线实测（2026-10-16 本轮结束时）

| 闸门 | 结果 |
|---|---|
| `guards.mjs` | ✅ **34 条规则 / 1372 文件 / 0 违规** |
| `vue-tsc`（5 包：core / admin / auditor / share / enterprise） | ✅ **0 错误** |
| `vitest`（`@certplatform/admin`） | ✅ **23 文件 / 423 用例 全绿**（含本次新增 `src/layouts/globalPendingAndTabs.test.ts` 21 例） |

> ⚠️ **测量期间的并发干扰**：本轮测量时另有一个会话正在改
> `cert-share/src/composables/workflow/`（`compiler.ts` / `serializer.ts` / `topology.ts`）
> 与 `scripts/guards.mjs`（规则 32 → 34）。期间曾读到 11 个 `cert-share` 类型错误与
> 一次守卫脚本崩溃（文件写入中）—— **均非本次改动引入**，随后由对方修好。
> ⇒ 按项目铁律，**未触碰**这些文件。
> ✅ 上述干扰在**最终复测**时已消失（`vue-tsc` 5 包 0 错误）。

