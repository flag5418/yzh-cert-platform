# yzh.vue.core 包化设计与业务边界评估-V1

> 版本：V1.0 ｜ 日期：2026-09-24 ｜ 状态：**待你裁决**
> 评估对象：`src/certplatform-web/yzh.vue.core/`（下称 **core**）
> 触发问题（用户原话）：
> 「现在针对 yzh.vue.core 做了大面积的修改，我需要你进行评估，将 yzh.vue.core 按 npm 包的方式进行设计，
> 将业务中的机构-人员 等一系列业务逻辑也移动到了 yzh.vue.core 中了，这个可能会对后续的开发有非常大的影响」
> 关联文档：`docs/50-任务/迁移计划/yzh.vue.core系统底座化迁移计划-V1.md`（status: 已完成 P0–P8）
> 方法：源码级实测（find/grep/读源码）+ 只读，未修改任何文件

---

## 〇、结论速览（先看这 5 条）

| # | 结论 | 置信度 |
|---|------|--------|
| 1 | **"机构-人员进 core" 这个判断需要修正一半**：core 里的 `organization`/`user` 操作的是 `Sys_Organization`/`Sys_User`（**系统实体**，后端对应控制器**本来就在框架层** `YZH.Core.Web/Controllers/System/`）。core 里**没有任何认证业务实体**（ISO 标准/认证阶段/机构能力/企业档案 = 0 处）。**边界没有被突破。** | 高（实测） |
| 2 | **但"npm 包方式"目前只是名义上的。** 真实形态是「**源码目录 + vite alias**」：`private: true`、`main: src/index.ts`、无 `types`/`files`/`.d.ts`、无版本策略、宿主 `package.json` 里**根本不声明 core 依赖**。所以"按 npm 包设计"这句话，**该做而没做的部分比做了的多**。 | 高（实测） |
| 3 | **最大的真实风险不是"业务混进 core"，而是"core 无版本边界 → 改动即全局生效、无法按端回滚"。** 单人开发时这是**优势**（改一处三端生效），一旦 auditor/enterprise 接入就变成**同步成本**。 | 中高（推理） |
| 4 | **第二个风险是边界"只靠约定、没有机制"**：守卫 R10 只覆盖 5 个目录，`logic/`/`utils/`/`adapters/`/`types/` 是盲区；且**没有任何规则禁止 core 出现认证业务实体**。今天守住了，靠的是人的自觉。 | 高（实测） |
| 5 | **收益目前只兑现了 1/3**：`cert-admin` 深度使用（31 处引用、登录/布局/11 系统路由全部来自 core）；`cert-auditor` **完全没用 core 的 UI**（自己手写 420 行登录 + 153 行布局 + 7 个页面），只用 api/composables（11 处）；`cert-enterprise` 引用数 = **0**。所谓"一套底座多端复用"，**目前只有一个端在用**。 | 高（实测） |

**一句话**：方向是对的，边界也没破；但**"npm 包"这层壳是空的**，而**守卫这层网有洞**。真正要决策的是：**core 到底做不做成真包**。

---

## 一、现状事实（实测，全部可复现）

### 1.1 core 的规模与构成

`yzh.vue.core/src/` 共 **88 个 `.ts`/`.vue`，14,799 行**：

| 目录 | 文件 | 行数 | 性质判断 |
|---|---|---|---|
| `components/` | 21 | 4,723 | ✅ 原子组件（YzhTable/YzhForm/YzhTree…），R1 守卫保证零领域依赖 |
| `pages/` | 25 | 3,032 | ⚠️ **本次讨论焦点**：登录 + 首页 + 11 个 system 页 |
| `logic/` | 8 | 2,465 | ✅ 原子内核：`SingleTableCore`/`TreeTableCore`/`CheckTreeCore`/`LinkTableCore`/`AssociationTreeCore`/`TreeSide` + `TreeTableLogic`(废弃别名) |
| `utils/` | 7 | 1,347 | ✅ tree 工具 / 大小写转换 / 菜单工具 |
| `api/` | 9 | 1,065 | ✅ `client.ts`(yzhApi) + `auth.ts` + `file-storage.ts` + `api/system/{menu,api,role-api,role-menu,role-user}.ts` |
| `types/` | 6 | 637 | ✅ 契约类型 |
| `layouts/` | 2 | 614 | ✅ `YzhAppLayout.vue`（侧栏+顶栏+个人中心+改密） |
| `composables/` | 8 | 355 | ✅ 含 2 个**模块级单例**（见 §3.4） |
| `adapters/` | 1 | 339 | ✅ `entityAdapters.ts`（EntityConfig→组件契约，纯函数） |
| `router/` | 1 | 128 | ✅ `yzhSystemRoutes` / `createYzhRoutes()` |

### 1.2 core/pages 全量清单 —— 有没有业务？

`find src/pages -type f` = **25 个**：

```
auth/Login.vue(431)            home/Home.vue(178)
system/api/{index.vue,logic.ts}          ← SysApi
system/config/{index.vue,logic.ts}       ← cert_sys_config
system/dictionary/{index.vue,logic.ts}   ← Sys_Dictionary
system/log/{index.vue,logic.ts}          ← SysLog
system/menu/{index.vue,logic.ts,IconPicker.vue}  ← Sys_Menu
system/organization/{index.vue,logic.ts} ← Sys_Organization / Sys_User  ★
system/role/{index.vue,logic.ts}         ← Sys_Role
system/role-api/{index.vue,logic.ts}     ← Sys_RoleApi
system/role-menu/{index.vue,logic.ts}    ← Sys_RoleMenu
system/role-user/{index.vue,logic.ts}    ← Sys_RoleUser
system/user/{index.vue,logic.ts}         ← Sys_User  ★
```

**关于 ★ 机构-人员这两个页（用户重点关注的）**，逐行核过：

- `system/user/logic.ts` **全文 19 行**，唯一覆写 `defaultValues → { IsValid: 1 }`。
- `system/role/logic.ts` 仅 12–14 行，**零业务覆写**。
- `system/organization/logic.ts` 覆写的字段全是通用系统字段：`controllerName='Organization'`、`entity.OrgCode = selectedNode?.Code`、`return 'UserTrueName'`、`ParentCode`、`IsLeaf`、`IsValid`、`CanAddUnderNode`。唯一的"业务味道"是一句文案「请选择末端机构（不含子机构的节点）」。
- **没有**角色编码、机构类型、认证业务枚举/字典值等业务常量。

**结论：这两个页是"重形态的系统页"（organization 756 行原始版本），但不是"业务页"。**

### 1.3 core 里的项目特定痕迹（全部实测）

| 项 | 命中 | 位置 |
|---|---|---|
| 硬编码后端地址（`127.0.0.1`/`localhost:`/`http(s)://`） | **0 处** ✅ | 地址由 `api/client.ts:356 baseURL:''` + `configureYzhApi()` 宿主注入 |
| 认证业务实体名（`ISOStandard`/`CertStage`/`CertificationBody`/`ent_enterprise`/`cert_`） | **0 处代码**，仅 1 处注释 | `pages/system/api/logic.ts:10` 注释里举例 `Foundation/ISOClause` |
| 宿主别名（`from '@/`、`from '@share/'`、`from '@cert-`） | **0 处** ✅ | 反向依赖未发生 |
| 品牌名硬编码（「映智汇」） | **5 处**，均为**可覆盖的 props 默认值** | `pages/auth/Login.vue:98,103,106`、`layouts/YzhAppLayout.vue:203`、`router/index.ts:85`(注释) |

> 品牌名 5 处是**合理设计**：core 给默认皮肤，宿主用 props 覆盖。cert-admin 实测就是这么用的（见 §1.5）。

### 1.4 "npm 包"的真实成熟度 —— **这是本评估的核心发现**

`yzh.vue.core/package.json`：

```json
{ "name": "yzh.vue.core", "version": "1.0.0", "private": true,
  "main": "src/index.ts",            // ← 指向 TS 源码，不是产物
  "exports": { ".": "./src/index.ts", "./components/*": "...", ... },
  "dependencies": { "axios": "^1.6.0" },
  "peerDependencies": { "vue", "vue-router", "element-plus", "@element-plus/icons-vue" },
  "scripts": { "build": "node ../scripts/guards.mjs && vite build", "typecheck": "vue-tsc --noEmit" } }
```

| 检查项 | 实况 | 判定 |
|---|---|---|
| `private` | `true` | ❌ 不可发布 |
| 入口 | `main: src/index.ts`（**源码**） | ❌ 非包入口 |
| `types` / `module` / `files` | **均缺失** | ❌ 无类型契约声明 |
| 构建产物 | `dist/yzh-vue-core.mjs` + `style.css`（200K） | ⚠️ 有产物但**无 `.d.ts`** |
| 宿主实际消费 | **vite alias → `../../yzh.vue.core/src`**（源码直连） | ❌ **不用 dist** |
| 宿主依赖声明 | 仅 `cert-share/package.json:28` 有 `"yzh.vue.core": "file:../../yzh.vue.core"`；**admin/auditor/enterprise 三端均未声明** | ❌ 依赖关系不可见 |
| 版本策略 | `1.0.0` 写死，**无 CHANGELOG / 无 changeset / 无 publish 脚本** | ❌ 无版本演进机制 |

各端 `vite.config.ts` / `tsconfig.json` 均把 `@yzh-core` 指向 core 的 **`src` 源码目录**（admin `vite.config.ts:12`、auditor `:12`、share `:10`；tsconfig paths 同理）。

**判定：这不是 npm 包，是"通过 alias 共享的源码目录"。** 两者工程后果完全不同（见 §3.1）。

### 1.5 宿主实际用了 core 的什么（实测引用数）

| 宿主 | 引用 core 处数 | 用了什么 |
|---|---|---|
| `cert-admin` | **31 处 / 25 文件** | 最深：`/login` → `@yzh-core/pages/auth/Login.vue`；`/` → `@yzh-core/layouts/YzhAppLayout.vue`（props 注入 `{menuTag:'admin', logoText:'YZH', appTitle:'映智汇认证平台'}`）；children 里 `...yzhSystemRoutes`（11 条系统路由）；再加 **17 条本地业务路由**（`@/pages/foundation/*`、`@/pages/workflow/*`） |
| `cert-auditor` | **11 处 / 6 文件** | **只用了 api + composables**。UI 全部自写：`layouts/AuditorLogin.vue`(420 行)、`layouts/AuditorLayout.vue`(153 行)、`pages/{overview,enterprises,organization,register,resources,settings,tasks}/` 共 7 个页面 |
| `cert-enterprise` | **0 处 / 0 文件** | 只有 `src/index.ts`（空壳） |
| `cert-share` | 32 处 / 22 文件 | 业务共享层，依赖 core（方向合法：share → core） |

**边界在 cert-admin 里是干净的**：
```
core 负责：登录页 + 应用壳 + 11 个系统管理页（yzhSystemRoutes）
admin 负责：17 条业务路由（ISO 标准/认证机构/阶段/目录/提取规则/报告/NC/队列…）
```

**但"多端复用"的收益目前 = 1 个端。** 迁移计划 §12 自己写明 auditor 接入是"后续跟进"，尚未做。

> ⚠️ **顺带发现一个命名冲突隐患**：core 有 `system/organization`（Sys_Organization 组织树），auditor 有自己的 `pages/organization/`（专家端机构页）。两者是**不同东西**。auditor 将来接入 core 时极易混淆——建议 auditor 侧改名（如 `org-profile`）或在文档中显式区分。

### 1.6 守卫的覆盖与盲区（实测 `scripts/guards.mjs`）

| 规则 | roots | 覆盖 core/pages & layouts？ | 说明 |
|---|---|---|---|
| **R10** 禁宿主反向依赖（`from '@/`、`from '@share/'`） | `CORE_APP_ROOTS = [pages, layouts, router, composables, api]`（`:59-66`） | ✅ 是 | ⚠️ **不含 `components/`（R1 管）、`logic/`、`utils/`、`adapters/`、`types/`** → 这 5 个目录无"禁反向依赖"规则 |
| **R11** 禁硬编码地址 | `CORE_ALL = yzh.vue.core/src`（`:68-69`） | ✅ 是 | 覆盖全 src |
| **R1** 组件零领域依赖 | `components/**` | — | 只管组件 |
| **"core 禁出现业务实体"** | **不存在** | — | ❌ **无此规则** |

实跑 `node scripts/guards.mjs --report`：`10 条规则 / 564 个文件 / 0 处违规`，R10/R11 均 ✓。

**判定：今天的干净是"人自觉"的结果，不是"机制强制"的结果。** 任何人（包括 AI）往 `core/logic/` 或 `core/utils/` 里塞认证业务逻辑，守卫**不会报错**。

---

## 二、正面影响（先说做对了什么）

1. **与后端对称，架构自洽。** 后端 `YZH.Core.Web/Controllers/System/` 早已托管 User/Role/Org/Menu/Dictionary 控制器；前端把对应页面下沉 core，是**同一动作的前端补完**。判据统一为「每个项目都需要的模块」——这个判据是对的。
2. **单人开发的 DRY 收益真实。** 11 个系统页 + 登录 + 布局 ≈ 4,000 行，若三端各写一份就是 12,000 行。这是**用户明确要花的成本**（"无限制的 AI 代码后续个人很难维护"）。
3. **配置驱动让"系统页"真的可复用。** 因为页面由 EntityConfig 驱动（列/表单/校验都来自 JSON），它才能脱离具体项目——**这是 core 能装页面的前提**，不是巧合。
4. **品牌/皮肤用 props 注入，宿主持有覆盖权。** `cert-admin` 实测注入 `appTitle` 生效；`path 单一来源` 契约（§3 铁律）让宿主可整条覆盖 `/login`。**auditor 手写 420 行登录页，正是这个机制允许的合法用法。**
5. **依赖方向没被破坏。** 0 处 `@/`/`@share/` 反向引用，0 处硬编码地址。守卫 R10/R11 确实在起作用。
6. **样板路径已同步到 AGENTS.md**（P8 完成）→ AI 协作层面的迁移成本**已经处理过了**，不是遗留问题。

---

## 三、风险（按严重性排序）

### 🔴 R-1｜"包"是空的：无版本边界 → 改动即全局生效，无法按端回滚

**触发**：任何对 core 的修改。
**症状**：
- 宿主 `vite alias` 直连 core **源码**，所以改 core 一个文件 → 所有引用它的宿主**立刻**改变行为，**没有版本过渡期**。
- 一旦 auditor/enterprise 也接入，一次 core 改动要**同时**回归三端；想"只让 admin 用新版、auditor 留在旧版"——**做不到**（没有版本号可 pin）。
- 反过来，如果将来要把 core 抽出去给**另一个项目**用（这是"npm 包"的本意），当前形态**无法交付**：`private:true` + 源码入口 + 无 `.d.ts`。

**为什么现在没爆**：只有 1 个宿主在用（cert-admin），"全局生效"≈"单端生效"。
**缓解**：见 §5 建议 ①（明确命名）与 ③（真做包）。

---

### 🔴 R-2｜边界只靠约定，守卫有洞

**触发**：任何人/AI 往 `core/logic/`、`core/utils/`、`core/adapters/`、`core/types/` 里加东西。
**症状**：
- 这些目录**不在 R10 的 roots 里** → 可以合法地 `import { something } from '@/...'` 或 `@share/...`，形成**宿主反向依赖**而不被拦截。
- **没有"core 禁认证业务实体"的规则** → 往 core 里塞 `CertStage` 相关逻辑，守卫静默通过。
- 后果是**慢性**的：边界一天天模糊，等发现时已经缠在一起，拆不动了。

**为什么值得现在管**：这正是用户担心的"对后续开发有非常大的影响"——影响不是今天，是**半年后**。

---

### 🟡 R-3｜模块级单例状态跨宿主共享

**实况**：`composables/useAuthState.ts:20-22` 的 `token/userInfo/roles` 与 `useMenuTree.ts:17-19` 的 `menus/loading/loaded` 都是**模块顶层 ref**（源码注释自认"模块级单例"）。宿主 store 是**薄适配**（`defineStore('auth', () => useAuthState())`），非双份实现 ✅。

**风险**：
- **登出/切换用户必须显式清理**，否则状态残留（菜单/权限串到下一个用户）→ 隐蔽且危险。
- 单元测试**无法隔离**：一个用例改了单例，污染后续用例。
- 未来若出现"同一页面内嵌另一个应用实例"（微前端/多窗口），状态会串。
- `useMenuChanged` 用 `window.dispatchEvent(new CustomEvent('yzh:menu-changed'))` 做全局事件 → 同样是隐式全局。

**判定**：当前**可用**（每端独立构建、单实例运行），但这是"设计选择"还是"顺手写成单例"需要明确下来并写进文档。

---

### 🟡 R-4｜UI 页面进"包"→ 与 UI 库/主题强耦合

**症状**：core 的 11 个系统页 + 登录页 + 布局都依赖 `element-plus` + `@element-plus/icons-vue`（在 `peerDependencies` 里）。宿主若想换 UI 库、升级 Element Plus 大版本、或做深色主题改造，**必须跟着 core 一起动**，无法独立演进。
**现状**：`peerDependencies` 声明是正确做法（避免双份 Element Plus），这点没问题；问题在**版本升级时的联动成本**。

---

### 🟡 R-5｜收益未兑现：只有 1/3 宿主在用，且 auditor 已产生"重复实现"

**症状**：`cert-auditor` 手写了 420 行登录 + 153 行布局 + 7 个页面，**没有复用 core 的登录页/布局**。这意味着：
- 迁移计划承诺的"一套底座多端复用"**尚未闭环验证**——也就是说 **core 的"可复用性"这个核心假设，还没被第二个端检验过**。
- 一旦 auditor 接入，很可能发现 core 的登录页/布局**不够通用**（专家端有注册、邀请码、机构工作区等 admin 没有的流程），届时需要回头改 core → 又是一轮全局改动。

**这是本评估里"最该尽快消除的不确定性"**——因为它决定 core 的边界划得对不对。

---

### 🟢 R-6｜依赖声明缺失（工具链可见性）

**症状**：admin/auditor/enterprise 的 `package.json` **不声明** core 依赖（只有 cert-share 声明了）。虽然运行时靠 alias 能跑，但：
- 依赖图对工具不可见（npm/pnpm workspace 排序、`npm ls`、依赖分析、IDE 跳转都可能不准）。
- 新人/AI 读 `cert-admin/package.json` 会**以为它不依赖 core**。

---

## 四、边界判断：机构-人员到底该不该在 core？

### 判断：**该在。** 但有前置条件。

**理由**：
1. **它们是系统实体，不是业务实体。** `Sys_Organization`/`Sys_User` 在后端**本来就在框架层**（`YZH.Core.Web/Controllers/System/OrganizationController`、`UserController`）。前端页面留在业务层反而**制造了前后端不对称**。
2. **判据是统一的**：「换个完全不同的项目，这个页面还需要吗？」——需要（任何系统都有组织/人员管理）→ 归 core。这个判据与后端一致。
3. **实测证明它们没有夹带业务**：logic 层只有通用字段，0 处认证业务常量。

**前置条件（必须同时满足，否则"该在"就变成"不该在"）**：
| # | 条件 | 当前状态 |
|---|---|---|
| C1 | 页面保持**配置驱动**形态，不硬编码业务字段/枚举 | ✅ 满足 |
| C2 | 不引用任何**认证业务实体**（ISOStandard/CertStage/…） | ✅ 满足（0 处） |
| C3 | 宿主保有**per-path 覆盖权**（业务差异用覆盖解决，不改 core） | ✅ 机制具备 |
| C4 | 有**机制**保证 C1/C2 不被未来破坏 | ❌ **不满足** ← 唯一缺口 |

**所以结论是：C1–C3 都满足，缺的是 C4。** 而 C4 恰好是成本最低、收益最高的一项（见 §5 建议 ①）。

**红线（必须写死）**：core **永不**出现 `ISOStandard`/`CertStage`/`CertificationBody`/`cert_org_*`/`ent_enterprise`/`ValidationRule`/报告模板/N C 规则 等认证业务实体。

---

## 五、建议（按成本/收益排序，符合三过滤器）

### ① 立即做（<30 分钟，F1+F2）—— 把边界从"约定"变成"机制"

**新增守卫 R12：core 禁认证业务实体**

在 `scripts/guards.mjs` 加一条，roots = `yzh.vue.core/src`，forbid 认证业务标识：

```
ISOStandard | ISOClause | CertStage | CertificationBody | PhaseDefinition
ent_enterprise | cert_org_standard | cert_org_stage | ValidationRule
ReportTemplate | ReportSection | WfSkill | DocExtractionRule
```

**同时扩 R10 的 roots** 到 `logic/`、`utils/`、`adapters/`、`types/`（补上"禁反向依赖"的盲区）。

> 成本：~20 分钟。收益：**用户担心的"后续影响"从此有机制兜底**，且基线为 0（实测 0 处违规）→ 立刻可启用，符合"规则启用铁律：基线必须为 0"。

### ② 立即做（<15 分钟，F1）—— 让依赖关系可见

三个宿主的 `package.json` 补声明 `"yzh.vue.core": "file:../../yzh.vue.core"`。
> 成本：3 行。收益：依赖图对工具/人/AI 可见。**注意**：这只是"声明"，实际解析仍走 vite alias，不改变构建行为。

### ③ 需要你裁决（本评估的核心决策）—— **core 到底做不做成真包？**

| 选项 | 含义 | 成本 | 适合场景 |
|---|---|---|---|
| **A. 保持"源码共享"，把话说清楚** | 删掉 `package.json` 里"npm 包"的描述，文档明确写「core = 源码共享目录，非 npm 包」；保留 `private:true`；不引入版本/产物 | ~30 分钟（改描述+文档） | **单人开发、单仓多端** —— 收益（DRY）已拿到，成本（版本/发布/产物）不必付 |
| **B. 真做成包** | 加 `vite-plugin-dts` 产出 `.d.ts`、补 `types`/`files` 字段、引入语义化版本 + CHANGELOG、宿主改为消费 `dist` 或保留 alias 但**宿主 pin 版本** | 半天~1 天，且**每次改 core 多一道"构建+升版"流程** | 将来要**跨仓复用**（给另一个项目/团队用），或需要"按端 pin 版本" |

**我的建议：选 A（保持源码共享，但把定位说准确）。**
理由：
- 单人独立开发 + 单仓多端 → npm 包化要解决的问题（发布、版本、跨仓分发）**你现在都不需要**。
- 你**真正需要**的是"边界不腐化"，那是**守卫**（建议①）解决的，不是包化解决的。
- B 会引入**每次改 core 的额外仪式**（构建产物 + 升版 + 宿主跟版），在 10/15 死线前是净负担。
- 保留 B 的可能性：**代码结构已经为 B 准备好了**（有 `exports` 映射、有 `vite build` lib 模式、有 peerDependencies）——将来真要发布，补 `.d.ts` + `files` + 版本策略即可，**不用重构**。

> ⚠️ 若选 A，**必须同步改文档**（`package.json` 的 description、`03-前端架构.md`、迁移计划），否则"npm 包"这个说法会继续误导后续的 AI 和协作者——**这本身就是一种技术债**。

### ④ 短期（半天，与 auditor 接入合并做）—— 兑现"多端复用"的闭环

按迁移计划 §12 让 `cert-auditor` 接入 core（`YzhAppLayout` + `configureYzhApi` + 按需 `yzhSystemRoutes`）。
**这一步的真正价值不是省代码，而是"检验 core 的边界划得对不对"**：
- 若 auditor 能顺利复用 → 边界假设**被验证**，可以放心继续往 core 放系统页。
- 若发现 core 不够通用（专家端注册/工作区/邀请码流程）→ **趁早发现**，比等到 enterprise 端接入时再发现便宜得多。

> 建议在 10/15 死线**之后**做，除非它不阻塞关键路径。当前它不在关键路径上（专家端已有可用的自写 UI）。

### ⑤ 短期（<30 分钟）—— 单例状态显式化

给 `useAuthState` / `useMenuTree` 加 `reset()`，并在登出、切换用户时调用；在源码注释与 `03-前端架构.md` 里**明确写"模块级单例是设计选择"**，避免后来者误以为是 bug 而改成非单例（那会破坏"宿主 store 薄适配"的结构）。
> 成本：~30 分钟。收益：消除 R-3 的隐蔽状态残留风险。

---

## 六、待你裁决

| # | 决策 | 我的建议 |
|---|---|---|
| **D1** | **core 做不做成真包？**（选项 A 保持源码共享 / B 真做成包） | **选 A**，但必须把文档里的"npm 包"说法改准确 |
| **D2** | 是否加守卫 R12（core 禁认证业务实体）+ 扩 R10 roots？ | **做**（20 分钟，基线 0，立刻可启用） |
| **D3** | 是否现在就让 auditor 接入 core？ | **10/15 之后**（不在关键路径；当前 auditor 自写 UI 可用） |
| **D4** | 单例状态是否加 `reset()`？ | **做**（30 分钟） |

---

## 七、附：本评估的取证命令（可复现）

```bash
# core 规模
cd src/certplatform-web/yzh.vue.core/src
for d in adapters api components composables layouts logic pages router types utils; do
  find $d -type f \( -name '*.ts' -o -name '*.vue' \) -exec cat {} + | wc -l
done

# core 里有无业务实体 / 反向依赖 / 硬编码地址
grep -rnE "ISOStandard|CertStage|CertificationBody|ent_enterprise|cert_org_" yzh.vue.core/src
grep -rnE "from ['\"](@/|@share/|@cert-)" yzh.vue.core/src
grep -rnE "127\.0\.0\.1|localhost:[0-9]+|https?://[a-zA-Z0-9]" yzh.vue.core/src

# 宿主引用面
for h in cert/cert-admin cert/cert-auditor cert/cert-enterprise cert/cert-share; do
  grep -rn "@yzh-core" $h/src | wc -l
done

# 守卫
cd src/certplatform-web && node scripts/guards.mjs --report
```
