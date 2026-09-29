# yzh.vue.core 结构合理性与科学性评估 + 专家端接入完整方案

> 版本：V2.0 ｜ 日期：2026-09-24 ｜ 状态：**待你审阅**
> 评估对象：`src/certplatform-web/yzh.vue.core/`（下称 **core**）
> 上游文档：V1《yzh.vue.core 包化设计与业务边界评估》—— 那份回答的是"**边界有没有破**"
> **本版回答你真正问的两件事**：
> ① 「当前只是从结构上做了大量改变 —— **分析合理性、科学性**」
> ② 「我需要**完整计划**后再决定专家系统如何接入」
>
> **方法**：源码级实测（find / grep / Python 静态扫描 / `vue-tsc` / `guards.mjs --report`）+ 只读，**未修改任何文件**。
> 所有结论均附 `文件:行号` 证据，可复现（命令见 §9）。

---

## 〇、先给结论（8 条，读完可停）

| # | 结论 | 性质 |
|---|------|------|
| 1 | **结构性改动方向是对的，而且做对了最难的部分**：core 内部**没有出现循环依赖**，`components/` 真正做到零向外依赖，`pages/`、`layouts/` 的依赖方向完全正确。这是"分层架构"里最容易翻车的地方，你没翻。 | ✅ 合理性 |
| 2 | **但"合理"和"科学"是两回事。合理 = 服务于目标（已达成）；科学 = 有一致判据 + 机制兜底（**只达成一半**）。** | ⚠️ 关键区分 |
| 3 | **最大的结构性问题不是"业务混进来了"，而是"通用层里堆了 13.4% 的死代码"** —— 1,984 行 / 14,799 行，其中 `utils/treeOps.ts`+`treeUtils.ts` 共 **1,067 行零引用**。**抽象建好了，却没人用；大家各写各的**（树构建实测重复 3 处）。 | 🔴 P0 |
| 4 | **"五原子"模型是自洽的，但它只活在 `package.json` 的一句 description 里，`docs/` 全库 0 命中。** 一个没有文档、没有守卫、没有测试的模型，等于**只存在于作者脑中的模型**。 | 🔴 P0 |
| 5 | **契约归属错位**：`YzhTableColumn`/`YzhAction`/`YzhFormField`/`Page`/`PageParams` 这些**跨层契约物理上住在 `components/table/types.ts`**，导致 `logic/`、`adapters/`、`composables/` 都要反向 `import type ... from '../components/...'`，宿主也要 `import ... from '@yzh-core/components/table/types'`（实测 3 处）。**契约应住 `types/`。** | 🟡 P1 |
| 6 | **同名契约重复定义**：`Page`/`PageParams` 在 `types/Page.ts` 和 `components/table/types.ts` 各有一份，barrel 同时导出 → 显式具名导出遮蔽 `export *`，**后者被静默吞掉**（`vue-tsc` 零错误正说明是"静默"而非"报错"）。 | 🟡 P1 |
| 7 | **core 零测试**（0 个 spec/test），而它是被三端共享的底座。**改动 core = 三端同时盲改**。这是"科学性"里最贵的一条欠账。 | 🟡 P1 |
| 8 | **专家端接入：技术上完全可行，且收益明确**（可删 ~583 行重复 UI），**但会暴露 core 的 2 个通用性缺口**（登录页无"注册入口"插槽、布局无"租户/工作区"位）。**建议先补缺口再接，避免接入后回头改 core（那就是全局改动）**。 | ✅ 可行 |

**一句话**：**你的结构是"合理的"（方向对、依赖干净），但还**不是"科学的"（判据没写下来、机制没兜住、通用层有 13.4% 是死的）**。下一步不该是"再做一次结构改动"，而是**把已有的正确结构"固化"下来**：写判据、清死代码、补契约归属、加最小机制。**

---

## 一、评估坐标系：用什么判据说"合理/科学"

**先立判据，再打分**——否则"合理性评估"就是主观意见。以下 7 条是软件分层架构的通用判据，每条都给**检验方法**（可复现）。

### A. 合理性（服务于目标）—— 2 条

| # | 判据 | 检验方法 |
|---|------|----------|
| **A1** | **收益真实存在**：下沉到 core 的东西，确实被 ≥2 个宿主复用（否则下沉只是搬家） | 统计符号的宿主引用数 |
| **A2** | **成本可接受**：不引入超出单人维护能力的流程负担 | 看是否强制"构建+升版"等仪式 |

### B. 科学性（原则一致 + 机制兜底）—— 5 条

| # | 判据 | 检验方法 |
|---|------|----------|
| **B1** | **判据一致**：同一个规则决定每个模块的归属，且规则被写下来 | 能否用一句话说出"什么进 core" |
| **B2** | **依赖单向无环**：层间依赖指向同一方向，无环 | 提取 import 图（本报告 §2.2） |
| **B3** | **单一职责**：每层只有一个变更理由 | 逐层看"什么情况下要改这层" |
| **B4** | **接口最小**：只暴露被消费的部分 | 导出面 vs 消费面 对照 |
| **B5** | **名实相符 + 可验证**：名字不撒谎；规则由机制（守卫/测试）保证，不靠记忆 | 跑守卫、看有无测试 |

---

## 二、现状全貌（全部实测）

### 2.1 规模与分层

`yzh.vue.core/src/` 共 **90 个 `.ts`/`.vue`，14,799 行**：

| 目录 | 文件 | 行数 | 定位（五原子） |
|---|---|---|---|
| `components/` | 21 | 4,723 | 原子组件 |
| `pages/` | 25 | 3,032 | 原子界面 |
| `logic/` | 8 | 2,465 | 原子方法（内核） |
| `utils/` | 7 | 1,347 | 原子方法（工具） |
| `api/` | 9 | 1,065 | 原子 API |
| `types/` | 6 | 637 | 契约 |
| `layouts/` | 2 | 614 | 原子界面（壳） |
| `composables/` | 8 | 355 | 原子方法（组合式） |
| `adapters/` | 1 | 339 | 适配层（EntityConfig→契约） |
| `router/` | 1 | 128 | 原子路由 |

**"五原子" = 原子组件 / 原子方法 / 原子 API / 原子路由 / 原子界面**（出处：`package.json` 的 `description`）。
→ ⚠️ **`docs/` 全库搜索"五原子"：0 命中。** 模型没有文档。

### 2.2 真实依赖图（这是"科学性 B2"的核心证据）

**实测的层间 import 边**（`from '../X'` 全量提取）：

```
                    ┌─────────────────────────────────────┐
   pages/  ─────────┤→ api/  → composables/  → utils/      │
   layouts/ ────────┤                                      │
                    └─────────────────────────────────────┘
                            ↑ 正确方向（上层→下层）

   logic/      → adapters/  (运行期)  ┐
               → components/(仅 type) ├─ 契约耦合，非运行期
               → utils/    (运行期)  ┘
   adapters/   → components/(仅 type)
   composables/→ components/(仅 type)
   utils/menu  → api/system/(仅 type)

   components/ → 只有自己（table/ layout/ 子目录）  ✅ 零向外依赖
   types/      → 只有自己                          ✅ 叶子
```

**判定（B2 通过，但要注明一处概念倒挂）**：
- ✅ **无环**。所有运行期依赖都指向同一方向。
- ✅ `components/` 零向外依赖（R1 守卫在起作用，虽有 2 个 debt 文件）。
- ⚠️ **唯一的"概念倒挂"是契约位置**：`logic/`、`adapters/`、`composables/` 都 `import type ... from '../components/...'` —— 这不是运行期循环（type 会被擦除），**但它说明"契约住在组件层"，而契约本该住在 `types/`**。见 §6 P1-1。

**具体证据**：
```
logic/TreeTableCore.ts:24   import type { YzhFormField } from '../components/form'
logic/TreeTableCore.ts:25   import type { YzhAction }    from '../components/table/types'
logic/SingleTableCore.ts:33 import type { YzhFormField } from '../components/form'
adapters/entityAdapters.ts:12,17  import type ... from '../components/...'
composables/useTable.ts:2   import type { Page, PageParams } from '../components/table/types'
```

### 2.3 宿主消费面 —— 34 个符号 / 12 个子路径（B4 的核心证据）

Python 静态扫描 113 个宿主文件，得到**权威消费面**：

| 消费的模块入口 | 导入次数 |
|---|---|
| `@yzh-core`（**根 barrel**） | 66 |
| `@yzh-core/api/client` | 21 |
| `@yzh-core/types` | 10 |
| `@yzh-core/composables/useAuthState` | 4 |
| **`@yzh-core/components/table/types`** | **3** ← 宿主被迫伸手进组件层拿契约 |
| `@yzh-core/api/auth` | 2 |
| `@yzh-core/composables/useMenuTree` | 2 |
| `@yzh-core/utils/menu` | 2 |
| `@yzh-core/composables/useMenuChanged` | 1 |
| `@yzh-core/api/system/menu` | 1 |
| `@yzh-core/components/table` | 1 |
| `@yzh-core/router` | 1 |

**宿主实际消费的全部符号（34 个）**：

```
yzhApi(21)  ApiResponse(9)  YzhTable(8)  PageParams(6)  YzhFormDialog(6)
SingleTableCore(6)  useSingleTable(5)  Page(4)  PagedData(4)  YzhPageLayout(4)
YzhForm(4)  YzhFormField(4)  TreeNode(4)  configureYzhApi(2)  useMenuTree(2)
useAuthState(2)  UserInfo(2)  YzhTableColumn(2)  YzhTreeTableLayout(2)
TreeTableLogic(2)  YzhAction(2)  tokenStore(1)  FilterRequest(1)  PagedResult(1)
login(1)  getCaptcha(1)  onMenuChanged(1)  filterMenuTreeByTag(1)
formatMenuIcon(1)  SysMenu(1)  SearchField(1)  yzhSystemRoutes(1)
FilterItem(1)  YzhTreeTable(1)
```

而 core 的 `index.ts` 用 **`export *` 通配导出 7 个模块**（adapters / composables / logic / types×4 / treeUtils）+ 逐个具名导出组件。

→ **导出面 ≈ 消费面的 3 倍。** 这是 B4（接口最小）不达标。

### 2.4 与后端的对称性（这是"合理性 A1"的加分项）

| 前端 core | 后端框架层 | 对称？ |
|---|---|---|
| `pages/system/{user,role,organization,menu,dictionary,api,log,config,…}` | `YZH.Core.Web/Controllers/System/{User,Role,Organization,Menu,Dictionary,…}Controller` | ✅ 完全对称 |
| `api/system/{menu,role-user,role-menu,role-api}` | 同上 | ✅ |
| `logic/{SingleTableCore,TreeTableCore,CheckTreeCore}` | `YzhControllerBase<V>` / `TreeTableControllerBase<T,V>` | ✅ 前后端基类对应 |

→ **"系统实体放 core"有后端先例支撑，不是拍脑袋。** 这条判据（"换个项目还需要吗"）在前后端是一致的。

---

## 三、逐层体检

| 层 | 单一职责（B3） | 依赖方向（B2） | 实测问题 | 判定 |
|---|---|---|---|---|
| `components/` | 原子 UI，零领域 | ✅ 零向外 | R1 debt 2 文件（`YzhTree.vue`、`YzhTreeTableLayout.vue`）；`components/ui/` 3 文件零引用 | 🟡 基本健康 |
| `logic/` | 页面逻辑内核 | ✅ 仅 type 耦合组件契约 | `LinkTableCore.ts`(147 行) 零引用；`TreeTableLogic` 是别名但**宿主仍在用**（2 处） | 🟡 |
| `composables/` | 组合式封装 | ✅ | `useAuth.ts`(39) / `useTable.ts`(49) / `useConfirm.ts`(37) **全零引用** | 🔴 |
| `utils/` | 纯工具 | ✅ | **`treeOps.ts`(483) + `treeUtils.ts`(584) = 1,067 行零引用** | 🔴 |
| `adapters/` | EntityConfig→契约 纯函数 | ⚠️ 反向 import 组件契约 | 仅此 1 文件，职责清晰 | 🟢 |
| `types/` | 契约 | ✅ 叶子 | `Page`/`PageParams` 与组件层**重复定义**；`ApiResponse.ts` 零引用 | 🟡 |
| `api/` | 原子 API | ✅ | `api/system/{role-user,role-menu,role-api}` 三文件**同名函数冲突**（`checkAdd`/`getCheckTree`…）→ 无法进 barrel，只能子路径导入 | 🟡 |
| `router/` | 原子路由 | ✅ | `createYzhRoutes()` 设计优秀（path 单一来源 + per-path 覆盖） | 🟢 |
| `pages/` | 原子界面 | ✅ | 25 页全部 `Sys_*` 系统实体，0 处认证业务实体 | 🟢 |
| `layouts/` | 原子壳 | ✅ | 仅 `YzhAppLayout.vue` 1 个（613 行） | 🟢 |

### 3.1 死代码精确清单（B4 核心证据）

Python 脚本对 30 个可疑符号做"core 内引用 / 宿主引用"双向计数：

| 符号 | 文件 | 行数 | core内 | 宿主 | 判定 |
|---|---|---|---|---|---|
| `treeOps` 命名空间 + 7 函数 | `utils/treeOps.ts` | 483 | 0 | 0 | ❌ 死 |
| `treeUtils` 命名空间 + 23 函数 | `utils/treeUtils.ts` | 584 | 0 | 0 | ❌ 死 |
| `YzhTreeTableSelector` | `components/layout/…` | 417 | 0 | 0 | ❌ 死 |
| `YzhCard` + `YzhEmptyState` + `YzhStatusBadge` | `components/ui/*` | 219 | 0 | 0 | ❌ 死 |
| `LinkTableCore` + `useLinkTable` | `logic/`+`composables/` | 147+ | 0 | 0 | ❌ 死 |
| `useTable` | `composables/useTable.ts` | 49 | 0 | 0 | ❌ 死 |
| `useAuth`（第二套 auth 实现！） | `composables/useAuth.ts` | 39 | 0 | 0 | ❌ 死 |
| `useConfirm` | `composables/useConfirm.ts` | 37 | 0 | 0 | ❌ 死 |
| `LegacyApiResponse` | `types/ApiResponse.ts` | 9 | 0 | 0 | ❌ 死 |
| | | **≈1,984** | | | **占 core 13.4%** |

**对照：真正在用的内部能力**（宿主不直接用，但 core 自己的页面用 → 属"内部实现"，不该公开导出）：
`TreeTableCore`(core内5) / `CheckTreeCore`(3) / `AssociationTreeCore`(3) / `TreeSide`(1) / `useTreeTable`(4) / `useCheckTree`(3)

### 3.2 树构建：抽象建好了，却重复实现 3 次

| # | 实现 | 位置 | 状态 |
|---|---|---|---|
| 1 | `buildTree<T>()` **通用版** | `utils/treeUtils.ts:47` | ❌ **零引用（死）** |
| 2 | `buildTree(RawSysMenu[])` 菜单专用 | `api/system/menu.ts:64` | ✅ 在用 |
| 3 | `private buildTree(ApiItem[])` 接口页专用 | `pages/system/api/logic.ts:155` | ✅ 在用 |
| 4 | `rebuildTree()` 勾选树专用 | `components/layout/YzhTreeTableCheckSelector.vue:364` | ✅ 在用 |

→ **教科书式的"抽象与使用脱节"**：通用抽象存在但无人使用，每个消费者各写一份。

---

## 四、合理性评估（A1 / A2）

### A1 收益真实存在 —— ✅ **部分达成**

| 下沉内容 | 宿主复用数 | 判定 |
|---|---|---|
| 11 个系统页 + 登录 + 壳 | **仅 `cert-admin` 1 个端**（31 处/25 文件） | ⚠️ 收益只兑现 1/3 |
| `api/client`(yzhApi) | 3 端（21 处） | ✅ |
| `useAuthState` / `useMenuTree` | 2 端 | ✅ |
| `utils/menu`(filterMenuTreeByTag) | 2 端 | ✅ |

**结论**：**"系统页/登录/壳"这三样的复用目前只有 admin 一端在用**；`cert-auditor` 复用了 API 与 composables，但**UI 全部自写**；`cert-enterprise` 引用数 = 0。
→ **合理性成立，但"多端复用"这个核心假设尚未被第二个端检验。** 这正是专家端接入的价值所在（§8）。

### A2 成本可接受 —— ✅ **达成**

- core 是 npm workspace 成员（根 `package.json` 的 `workspaces: ["cert/*","yzh.vue.core"]`），宿主通过 **vite alias 直连源码**，**改 core 无需构建/升版**。
- `build:core` 会产出 `dist/`，但**宿主不消费 dist** → 该步骤目前是**仪式性**的（见 §7 建议 3）。
- **代价 = 0 额外流程**。这符合单人开发的现实。✅

**→ 合理性总分：方向正确、代价为零、收益部分兑现（待专家端验证）。判"合理"。**

---

## 五、科学性评估（B1–B5 逐条打分）

| 判据 | 得分 | 依据 |
|---|---|---|
| **B1 判据一致** | 🟡 **半** | 判据客观存在（"每个项目都需要的系统模块 → core"，且与后端对称），**但从未写进任何文档**；`docs/` 搜"五原子"= 0 命中。判据活在作者脑中，新人/AI 无法继承。 |
| **B2 依赖单向无环** | ✅ **达标** | 实测无环，`components/` 零向外依赖，`pages/layouts` 方向正确。**这是做得最好的一条。** |
| **B3 单一职责** | 🟡 **半** | 10 层职责基本清晰；但 `components/table/types.ts` 同时承担"组件契约 + 跨层通用契约"两个角色 → 契约归属错位（§6 P1-1）。 |
| **B4 接口最小** | ❌ **不达标** | 导出面 ≈ 消费面 ×3；`export *` 通配导出 7 个模块；**13.4% 导出物零引用**。 |
| **B5 名实相符 + 可验证** | ❌ **不达标** | ① `package.json` 自称"npm 包"，实为"源码目录 + alias"（V1 已述）；② **core 零测试**；③ 守卫 R10 roots 漏 `logic/utils/adapters/types`；④ 无"core 禁业务实体"规则。 |

**科学性总分：1 达标 / 2 半 / 2 不达标。**

> **关键洞察**：你做成的是"**依赖干净**"（B2）——这是**结构**层面的成就，而且是最难的一项。
> 你还没做的是"**判据成文 + 机制兜底 + 接口收口 + 可验证**"（B1/B4/B5）——这是**工程化**层面。
> **结构干净 ≠ 工程科学。前者靠设计者的一次正确决策，后者靠机制让正确能被持续复制。**

---

## 六、问题清单（分级 + 证据）

### 🔴 P0（影响"科学性"根基，成本低，建议本周处理）

| ID | 问题 | 证据 | 成本 |
|---|---|---|---|
| **P0-1** | **1,984 行死代码（13.4%）留在通用层** —— 抽象不删，后来者（含 AI）会以为它在用，继续往上加，越滚越大 | §3.1 表 | 删除 ~30min（需先确认 `TreeTableLogic` 别名不动） |
| **P0-2** | **"五原子"模型零文档** —— 模型不写下来就无法传承，AI 每轮都要重新猜"什么进 core" | `docs/` 搜"五原子" = 0 | 写 1 页 ~40min |
| **P0-3** | **core 零测试** —— 三端共享的底座，改动即三端盲改 | `find yzh.vue.core -name '*.spec.ts'` = 0 | 先建 1 个 smoke（~2h） |
| **P0-4** | **`useAuth.ts` 是第二套 auth 实现**（与 `useAuthState` 并存）—— 未来若有人误用 `useAuth()`，token 会出现**两个真相源** | `composables/useAuth.ts` vs `useAuthState.ts` | 删除 ~5min |

### 🟡 P1（结构正确性问题）

| ID | 问题 | 证据 | 成本 |
|---|---|---|---|
| **P1-1** | **契约归属错位**：`YzhTableColumn`/`YzhAction`/`YzhFormField`/`Page`/`PageParams` 住在 `components/table/types.ts`，导致 4 个层反向 import + 宿主伸手进组件层 | `logic/*:24,25,33`、`adapters:12,17`、`composables/useTable.ts:2`、宿主 3 处 | 搬迁 ~1h（改动面大，需 typecheck 护航） |
| **P1-2** | **同名契约重复定义**：`Page`/`PageParams` 两处定义，barrel 静默遮蔽其中一份 | `types/Page.ts` vs `components/table/types.ts:100-113` | 删一处 ~5min |
| **P1-3** | **守卫盲区**：R10 roots 漏 `logic/utils/adapters/types`；无"core 禁认证业务实体"规则 → 边界靠自觉 | `guards.mjs:60-66` | 加规则 ~20min（基线 0） |
| **P1-4** | **api/system 三个兄弟文件同名函数冲突**（`checkAdd`/`getCheckTree`…）→ 无法进 barrel，只能子路径导入 | `api/system/index.ts` 注释自述 | 参数化 ~1h（可延后） |
| **P1-5** | **`@yzh-core/pages/*`、`@yzh-core/layouts` 未在 `index.ts` 导出**，宿主只能走深路径（这是**设计意图**，但未文档化，易被误当遗漏） | `index.ts` vs `router/index.ts` 的 `import('../pages/...')` | 文档注明 ~10min |

### 🟢 P2（卫生问题，不阻塞）

| ID | 问题 | 证据 |
|---|---|---|
| P2-1 | core 的 `node_modules` 是**断链**（指向 `src/server/…`，真实路径是 `src/old/server/…`）—— 靠 workspace 根提升才没炸 | `ls -la node_modules` |
| P2-2 | core 的 `vite.config.ts` 有**失效 alias** `'@share': '../share/src'`（真实路径 `../cert/cert-share/src`，且 core 本不该引用 @share） | `vite.config.ts:10` |
| P2-3 | `cert-admin` **未声明** `yzh.vue.core` 依赖（仅经 `@certplatform/share` 传递）→ 幻影依赖 | `cert-admin/package.json` |
| P2-4 | R1 debt 2 文件（`YzhTree.vue`、`YzhTreeTableLayout.vue`）长期挂账 | `guards.mjs:127-131` |

---

## 七、完整建议（体系化，不是零散修补）

> **总原则**：**你已经把"结构"做对了。不要再做结构改动，而要把"正确的结构"固化下来。**
> 建议按"**固化四件事**"组织，全部符合你的三过滤器（F1 低成本消风险 / F2 复利 / F3 关键路径）。

### 固化 ①：写下判据（B1）—— 让"什么进 core"可继承
- 在 `docs/10-YZH架构/03-前端架构.md` 新增一节 **《yzh.vue.core 五原子模型与准入判据》**，内容：
  - 五原子定义（组件/方法/API/路由/界面）
  - **准入三问**：① 换个完全不同的项目还需要吗？② 后端是否已有对称的框架层实现？③ 是否配置驱动（不硬编码业务字段）？
  - **红线**：core 永不出现 `ISOStandard`/`CertStage`/`CertificationBody`/`cert_org_*`/`ent_enterprise`/`ValidationRule`/报告模板/NC 规则
  - **覆盖优先**：业务差异用"per-path 覆盖 / props 注入"解决，**不改 core**
- 成本 ~40min。**收益：AI 和未来的你都不再需要猜。**

### 固化 ②：清死代码（B4）—— 让"导出面 = 消费面"
- **删除**（已实测零引用）：`utils/treeOps.ts`、`utils/treeUtils.ts`、`components/layout/YzhTreeTableSelector.vue`、`components/ui/*`(3)、`logic/LinkTableCore.ts`、`composables/{useAuth,useTable,useConfirm}.ts`、`types/ApiResponse.ts`，及对应的 barrel 导出行。
- **注意保留**：`TreeTableLogic` 别名**不能删**（`skill-manage`、`iso-standard` 两个宿主仍在用）。
- **同时删除** `api/system/index.ts` 里"同名函数不可进 barrel"的规避性注释对应物（若 P1-4 暂不做，则保留注释）。
- 成本 ~30min。**验证：`guards.mjs` + `vue-tsc` 双绿 + 三端 `typecheck`。**
- ⚠️ **删除前必须逐个 grep 确认**（`export *` barrel 会让死代码"看起来在用"—— 本次已用符号名逐个核过）。

### 固化 ③：契约归位（B3）
- 把 `YzhTableColumn`/`YzhAction`/`YzhActionType`/`YzhRowAction*`/`YzhNodeActions`/`YzhToolbarActions`/`YzhTableDataLoader`/`Page`/`PageParams`/`SearchField`/`DefaultSort` 从 `components/table/types.ts` **搬到 `types/table.ts`**；`YzhFormField`/`YzhFieldType` 搬到 `types/form.ts`。
- `components/`、`logic/`、`adapters/`、`composables/` 全部改为 `from '../types/...'`；宿主改为 `from '@yzh-core/types'`。
- **删掉 `types/Page.ts`**（与迁入的 `Page`/`PageParams` 重复，P1-2 一并解决）。
- 成本 ~1h。**这是本方案唯一"有改动面"的一项** —— 但它一次性消灭 4 条反向 import + 1 处重复定义 + 宿主伸手进组件层。
- 若想更稳：**先只做 `Page`/`PageParams`（P1-2，5min），把大搬迁留到专家端接入之后再评估。**

### 固化 ④：加最小机制（B5）
| 措施 | 内容 | 成本 |
|---|---|---|
| **守卫 R12** | core 禁认证业务实体（roots = `yzh.vue.core/src`，forbid 认证实体名列表） | 20min（基线 0，立即可启用） |
| **扩 R10 roots** | 补 `logic/`、`utils/`、`adapters/`、`types/` | 含在上条 |
| **core smoke 测试** | 1 个 spec：`SingleTableCore`/`TreeTableCore` 的 `dataLoader` 装配 + `useAuthState` 的 set/clear + `router.createYzhRoutes()` 的路由表结构 | ~2h |
| **命名纠正** | 把 `package.json` description 的"npm 包"改为"**源码共享底座（vite alias 消费，非发布包）**"；`03-前端架构.md` 同步 | 30min |
| **补依赖声明** | 三端 `package.json` 补 `"yzh.vue.core": "file:../../yzh.vue.core"` | 3 行 |

### 不建议做（明确列出，避免你被"看起来该做"的事分散注意力）
- ❌ **不要真做 npm 包**（你已定调；且与 A2 冲突——会引入构建+升版仪式）
- ❌ **不要为"结构好看"重构 `logic/` 内核**（`SingleTableCore` 870 行 / `TreeTableCore` 969 行**是被验证在用的**，重写风险 >> 收益）
- ❌ **不要急着修 P1-4（api/system 同名函数）**（不在关键路径，且要动 3 个页面）
- ❌ **不要修 P2-1/P2-2**（断链软链和失效 alias 目前**不产生实际故障**，属卫生问题）

---

## 八、专家端接入完整方案

### 8.1 目标与验收标准

| 项 | 内容 |
|---|---|
| **业务目标** | `cert-auditor`（专家端）复用 core 的登录/壳/系统能力，只写"专家端独有"的业务页 |
| **真正价值** | **不是省代码，而是"检验 core 的边界划得对不对"** —— core 的通用性假设至今只有 admin 一个样本 |
| **验收标准（可量化）** | ① `cert-auditor/src` 删除 `layouts/AuditorLogin.vue`(420) + `layouts/AuditorLayout.vue`(163)，改为 **≤15 行**路由配置；② 登录/登出/菜单分流/菜单变更刷新 **端到端可用**；③ `guards.mjs` + `vue-tsc` 双绿；④ **记录 core 暴露的通用性缺口**（本次接入的副产品，比省代码更值钱） |

### 8.2 现状：哪些能复用、哪些是重复

**实测 `cert-auditor/src` = 15 个文件**：

| 文件 | 行数 | 与 core 的关系 |
|---|---|---|
| `layouts/AuditorLogin.vue` | 420 | 🔴 **重复 core `Login.vue`(431)** —— 同样的图标集(User/Lock/PictureRounded/Check/Cpu)、同样调 `@yzh-core/api/auth` 的 `login`/`getCaptcha` |
| `layouts/AuditorLayout.vue` | 163 | 🔴 **重复 core `YzhAppLayout.vue`(613)** —— `el-menu`+`el-sub-menu`+`el-menu-item` 结构逐行相同（含同一句"结构固定 2 层"注释）；已用 core 的 `useMenuTree`/`onMenuChanged`/`filterMenuTreeByTag` |
| `api/auditor-auth.ts` | 72 | ✅ **保留**（`/api/AuditorAuth/GetOrgList`、`/Register` —— 专家端独有） |
| `store/auth.ts` | 14 | ✅ 保留（已是 `useAuthState()` 的薄适配） |
| `router/index.ts` | 92 | 🔄 改为 `createYzhRoutes()` |
| `pages/register/index.vue` | 458 | ✅ 保留（注册页，专家端独有） |
| `pages/{overview,enterprises,organization,resources,settings}/index.vue` | 21–22 | ✅ 保留（业务页，**当前是桩**） |
| `pages/tasks/index.vue` | 52 | ✅ 保留 |

**关键事实（决定方案可行性）**：
- ✅ **登录端点相同**：`AuditorLogin.vue:78` 用的就是 core 的 `login`（`POST /api/User/login`）→ **core 的 `Login.vue` 可直接复用**。
- ✅ **菜单分流机制已就绪**：core 的 `createYzhRoutes({ menuTag: 'auditor' })` 正好对应 `AuditorLayout` 里硬编码的 `filterMenuTreeByTag(menus, 'auditor')`。
- ⚠️ **但有两个通用性缺口**（见 8.4）—— 这就是接入要"先补缺口"的原因。

### 8.3 接入设计（目标形态）

```ts
// cert-auditor/src/router/index.ts —— 目标：从 92 行 → ~25 行
import { createYzhRoutes } from '@yzh-core/router'

export const routes = createYzhRoutes({
  menuTag: 'auditor',
  branding: {
    logoText: 'YZH',
    appTitle: '映智汇认证专家平台',
    login: {
      appTitle: '映智汇认证专家平台',
      appSubtitle: 'CERTIFICATION EXPERT PLATFORM',
      footerText: '© 2026 映智汇 (YZH) 版权所有',
    },
  },
  businessRoutes: [
    { path: 'overview',     component: () => import('@/pages/overview/index.vue') },
    { path: 'tasks',        component: () => import('@/pages/tasks/index.vue') },
    { path: 'enterprises',  component: () => import('@/pages/enterprises/index.vue') },
    { path: 'organization', component: () => import('@/pages/organization/index.vue') },
    { path: 'resources',    component: () => import('@/pages/resources/index.vue') },
    { path: 'settings',     component: () => import('@/pages/settings/index.vue') },
  ],
})

// 注册页：专家端独有 → 作为"额外顶层路由"追加（不进 shell）
routes.push({ path: '/register', component: () => import('@/pages/register/index.vue') })
```

**删除**：`layouts/AuditorLogin.vue`、`layouts/AuditorLayout.vue`、`store/auth.ts`（若 `YzhAppLayout` 已内建）、`router` 中手写的 shell 结构。

### 8.4 接入前必须补的 2 个 core 通用性缺口

> **这是本次接入最重要的产出**：接入过程会把 core 的"隐藏假设"暴露出来。**先补缺口再接**，否则接完还得回头改 core（= 全局改动）。

| 缺口 | 现象 | 建议补法 | 成本 |
|---|---|---|---|
| **G-1 登录页无"扩展链接"位** | 专家端登录页需要"**注册**"入口（admin 不需要） | 给 core `Login.vue` 加 **`#extra` 插槽**（放在登录按钮下方），宿主传 `<router-link to="/register">注册</router-link>` | 15min |
| **G-2 壳无"租户/工作区"位** | 专家端是"一次注册 = 一个机构工作区"，顶栏可能需要显示**当前机构名**（admin 是平台方，无此概念） | 给 `YzhAppLayout` 加 **`#header-extra` 插槽** + 一个可选 `tenantName` prop | 30min |

**若你不想动 core**：`createYzhRoutes({ login: false })` 走 **per-path 覆盖**（保留 `AuditorLogin.vue`），但那样 420 行重复保留 → **违背接入初衷**。
→ **建议：补 G-1/G-2（合计 45min），这两个插槽对 admin 无害、对未来 enterprise 端同样有用（复利）。**

### 8.5 分阶段执行（建议顺序）

| 阶段 | 内容 | 前置 | 验收 |
|---|---|---|---|
| **S1** | 补 G-1/G-2 插槽（改 core，2 处） | — | admin 端登录/壳回归通过（插槽可选，不传不影响） |
| **S2** | 接入 core 登录 + 壳，删 2 个 layout 文件 | S1 | 专家端可登录、菜单按 `auditor` tag 分流、菜单变更后侧栏刷新 |
| **S3** | 路由改 `createYzhRoutes`，注册页作顶层路由 | S2 | 6 条业务路由可达；`/register` 可达 |
| **S4** | 回归 + 记录缺口 | S3 | `guards.mjs` + `vue-tsc` 双绿；**产出《core 通用性缺口记录》** |

**建议时点**：你已定"现在就做"→ 可执行；但**S1 改 core 会让 admin 也重新构建**，建议**在 admin 当前无未提交改动时做**，便于出问题时快速定位。

### 8.6 风险与回滚

| 风险 | 概率 | 影响 | 对策 |
|---|---|---|---|
| core `Login.vue` 与专家端登录流程不完全兼容（如注册后自动登录） | 中 | 中 | S1 先补插槽；若仍有差异，走 `login:false` 保留 `AuditorLogin.vue`（回滚点） |
| `YzhAppLayout` 缺专家端需要的顶栏元素 | 中 | 低 | G-2 插槽解决 |
| 删除 layout 后遗漏某个已实现细节（如菜单折叠状态持久化） | 中 | 低 | S2 前先 diff 两个 layout 的能力清单 |
| 菜单 tag 过滤后专家端菜单为空（后端未配 auditor 菜单） | **高** | **高** | ⚠️ **S2 前先确认 `sys_menu` 里有 `Tag='auditor'` 的菜单记录**，否则登录后侧栏空白（**注意：重建库后业务数据为 0，需你手动录入**） |

> ⚠️ **最后一条是真实阻塞点**：库重建后 `sys_menu` 只有系统种子数据。若没有 auditor 的菜单行，接入后侧栏会是空的——**这会被误判为"接入失败"**。建议 S2 前用只读查询确认。

---

## 九、附录：取证命令（可复现）

```bash
cd src/certplatform-web

# 1. core 规模
cd yzh.vue.core/src && for d in adapters api components composables layouts logic pages router types utils; do
  printf "%-12s %s\n" "$d" "$(find $d -type f \( -name '*.ts' -o -name '*.vue' \) -exec cat {} + | wc -l)"
done; cd ../../..

# 2. core 类型检查（应零错误）
cd src/certplatform-web/yzh.vue.core && npx vue-tsc --noEmit; cd ../../..

# 3. 守卫全量报告
cd src/certplatform-web && node scripts/guards.mjs --report

# 4. core 里有无认证业务实体 / 反向依赖 / 硬编码地址（应全为 0）
cd src/certplatform-web/yzh.vue.core/src
grep -rnE "ISOStandard|CertStage|CertificationBody|ent_enterprise|cert_org_|ValidationRule|ReportTemplate" . | grep -v '\.md'
grep -rnE "from ['\"](@/|@share/|@cert-)" . | grep -v '\.md'
grep -rnE "127\.0\.0\.1|localhost:[0-9]+|https?://[a-zA-Z0-9]" . | grep -v '\.md'

# 5. 死代码复核（逐个符号名，勿用 export * 判断）
for s in treeOps treeUtils YzhTreeTableSelector YzhCard useAuth useTable useConfirm LegacyApiResponse LinkTableCore; do
  echo "── $s: $(grep -rn "\b$s\b" ../cert ../yzh.vue.core/src 2>/dev/null | grep -v node_modules | grep -v /dist/ | wc -l) 处"
done

# 6. 树构建重复实现
grep -rnE "function (buildTree|rebuildTree)" yzh.vue.core/src

# 7. core 测试
find yzh.vue.core -name '*.spec.ts' -o -name '*.test.ts' | grep -v node_modules   # → 空

# 8. node_modules 断链验证
ls -la yzh.vue.core/node_modules   # → 指向 src/server/... （不存在）
```

---

## 十、与你已定决策的衔接

| 你的决策 | 本方案如何衔接 |
|---|---|
| **"不做成 npm 包"** | ✅ 建议 ④ 的"命名纠正"落实为文档措辞修正；**不做任何打包/版本动作** |
| **"暂时什么都不做，我需要完整建议"** | ✅ 本文即"完整建议"（§7 四件固化 + §6 分级清单）；**你审阅后再决定动哪一项** |
| **"专家系统现在接入"** | ✅ §8 给出完整方案 + S1–S4 阶段 + 阻塞点预警（菜单 tag） |
| **"需要完整计划后再决定专家系统如何接入"** | ✅ §8.3 目标形态 + §8.4 两个缺口 + §8.5 顺序 + §8.6 回滚点，可据此决策 |
| **"不要临时造数据"** | ✅ §8.6 明确：菜单数据需**你手动录入**，方案不产出业务种子 |

---

> **最后一句**：你这次"大面积结构改动"的**实际成果比你自己可能以为的更好** —— 依赖图干净、边界没破、与后端对称。问题不在"改错了"，而在"**改对了却没把对的东西固化下来**"。§7 的四件固化全部是低成本、可回滚、产生复利的动作，且**不需要再动结构**。
