# yzh.vue.core 架构分离评估与演进建议 V3

> 版本：V3.0 ｜ 日期：2026-09-24 ｜ 状态：**待你审阅**
> 触发（你的原话）：
> 「我需要阐明 yzh.vue.core **为什么要做架构和项目分离**……不仅有原子组件、原子函数，还拥有**所有项目底层支撑的机构-人员、角色-人员等核心能力**，启动项目由项目工程来启动，项目可以将 yzh.vue.core **作为一个 npm 使用**，可以调用它的方法、原子组件、甚至是路由，这样是**将前端架构和项目彻底分离**，未来 yzh.vue.core 可以作为**前端核心能力，在任意项目中使用**……至于死代码、冗余代码、vol 残留等历史问题，的确需要**彻底解决**。根据我的描述，进行重新分析和建议」
>
> **与 V2 的关系**：V2 评估的是"**结构质量**"（依赖是否干净、死代码多少）；**V3 评估的是"架构意图是否达成"**（分离做到了没有）。两者互补，**V3 的坐标系以你的战略目标为准**。

---

## 〇、先纠正我上一版的坐标系错误

| 项 | V2 的（错误）假设 | 你澄清后的（正确）理解 |
|---|---|---|
| "机构-人员进 core" | 需要论证"该不该在" | **这是有意的设计**——core 拥有**项目底层支撑能力**，不是越界 |
| "npm 包" | 一个待裁决的选项（A/B） | **是既定方向**（"项目可以将 core 作为一个 npm 使用"），只是**现在不急于发布** |
| 评估目标 | 结构是否"合理/科学" | **"前端架构与项目彻底分离"这个目标达成了没有** |
| "彻底分离" | 未定义 | 你给了定义：**core 不启动应用、宿主可调用其方法/组件/路由、可在任意项目复用** |

→ **V2 里"收益只兑现 1/3（只有 admin 在用）"这个判断，在"分离"这个目标下要重新解读**：它不是"下沉错了"，而是"**分离做完了，但还没被第二个项目验收**"。这是**进度问题，不是方向问题**。

---

## 一、"分离"到底有哪几个维度 —— 逐维度实测

"彻底分离"不是一句话，它至少有 **4 个可独立检验的维度**。我逐条实测：

| # | 维度 | 含义 | 实测状态 | 证据 |
|---|---|---|---|---|
| **①** | **运行时分离** | core 不启动应用；启动由宿主工程负责 | ✅ **已达成** | core 无 `main.ts` / `App.vue` / `index.html`；宿主 `cert-admin/src/main.ts` + `index.html` 负责启动 |
| **②** | **依赖方向分离** | core 不反向依赖宿主 | ✅ **已达成** | 守卫 R10；实测 0 处 `from '@/` / `from '@share/'` |
| **③** | **环境分离** | core 不硬编码后端地址与品牌 | ✅ **已达成** | 守卫 R11；`baseURL` 由 `configureYzhApi({baseURL})` 注入（缺省 `''` 相对路径）；品牌 5 处全为**可覆盖的 props 默认值** |
| **④** | **契约分离** | core 不绑定**特定后端 API 契约** | ⚠️ **未达成（唯一 gap）** | core 内置 **30+ 个端点**，全文 `/api/` 出现 **91 次** |

### 1.1 维度 ① 运行时分离 —— 实测通过

```
yzh.vue.core/          →  无 main.ts / App.vue / index.html   ✅ 不启动
cert/cert-admin/       →  index.html + src/main.ts            ✅ 启动方
cert/cert-auditor/     →  src/main.ts                         ✅ 启动方
```

**这正是你说的"启动项目由项目工程来启动"，已经做到了。** core 只导出能力（组件 / 内核 / API / 路由 / 壳），不碰 `createApp`。

### 1.2 维度 ②③ 依赖与环境分离 —— 实测通过

- **地址注入点**（`api/client.ts:35-39`）：
  ```ts
  export interface YzhApiClientOptions {
    baseURL: string                 // 宿主注入；缺省 '' → 相对 /api/*
    getToken?: () => string | null  // 宿主注入（默认 tokenStore）
    onUnauthorized?: () => void     // 宿主注入（401 处理）
    onError?: (err: Error) => void  // 宿主注入
  }
  ```
  → **4 个注入点**，宿主 `main.ts` 启动时调 `configureYzhApi()`。这是干净的"控制反转"。
- **品牌注入点**：`Login.vue` / `YzhAppLayout.vue` 全部品牌内容走 props（`appLogo`/`appTitle`/`appSubtitle`/`features`/`footerText`/`menuTag`），`createYzhRoutes({ branding })` 统一透传。**宿主持有覆盖权。**

### 1.3 维度 ④ 契约分离 —— **唯一未达成的维度**

core 的 `index.ts` 第 2 行写着：

> `// 与项目完全无关的通用底层能力`

**但实测它内置了 30+ 个特定后端端点**：

| 文件 | 硬编码端点 | 数量 |
|---|---|---|
| `api/auth.ts` | `/api/User/login`、`/getVierificationCode`、`/getCurrentUserInfo`、`/updateUserInfo`、`/modifyPwd`、`/ping` | 6 |
| `api/system/menu.ts` | `/api/System/MenuManagement/{tree,tree/all,add,update,delete}` | 5 |
| `api/system/role-api.ts` | `/api/RoleApi/{tree/root,tree/children,checkTree,check/add,check/remove,check/all}` | 6 |
| `api/system/role-menu.ts` | `/api/RoleMenu/{…同上…}` | 6 |
| `api/system/role-user.ts` | `/api/Role/{tree/root,tree/children,checkTree,check/add,check/remove,check/all}` | 6 |
| `api/system/api.ts` | `/api/ApiSync/{list,sync,scan}` | 3 |
| `api/file-storage.ts` | `/api/file-storage` | 1 |

**更深的耦合在 CRUD 内核**——`controllerName` 拼出的是**一套约定**：

```ts
// logic/SingleTableCore.ts
abstract controllerName: string                                    // :65
await this.apiGet('/config')      // → /api/{controllerName}/config   :283
await this.apiPost('/filter')     // → /api/{controllerName}/filter   :305
// logic/TreeTableCore.ts
'/tree/root' | '/tree/children' | '/tree/add' | '/tree/update'
'/tree/delete' | '/tree/toggle-valid' | `/tree/action/{method}`      // :388–:793
```

`controllerName` 的取值：`'System/User'`、`'Organization'`、`'Role'`、`'Dictionary'`、`'System/MenuManagement'`、`'System/ApiSync'`、`'System/Log'`、`'System/Config'`。

> **结论**：core 的 CRUD **不写死具体实体**（由 `controllerName` 参数化），但**写死了后端约定**：
> ① 路径形状 `/api/{controller}/{config|filter|add|update|delete|tree/*}`
> ② 响应信封 `ApiResponse{success,message,data,code,timestamp}`
> ③ `config` 端点返回 **EntityConfig**（列/表单/校验都由它驱动）
> ④ 树/勾选端点命名（`tree/root`、`checkTree`、`check/add`…）

**→ core 不是"与项目无关"，而是"与一套后端约定耦合"。** 这是"彻底分离"里唯一没做完的一环。

---

## 二、关键确认：「机构-人员 / 角色-人员进 core」是**对的**

你说 core 应该拥有"所有项目底层支撑的机构-人员、角色-人员等核心能力"。**实测证据支持你，而且是 1:1 的硬证据。**

### 2.1 后端框架层早已托管这些控制器

`src/yzh-core/YZH.Core.Web/Controllers/System/`（**框架层，不是业务层**）实测有 **11 个控制器**：

```
ApiSyncController  ConfigController    DictionaryController  MenuController
MenuManagementController  OrganizationController  RoleApiController
RoleController     RoleMenuController  SysLogController      UserController
```

### 2.2 前端 core 的 11 个系统页 = 后端 11 个控制器的**前端镜像**

| core `pages/system/` | `controllerName` | 后端框架层控制器 | 对称 |
|---|---|---|---|
| `api/` | `System/ApiSync` | `ApiSyncController` | ✅ |
| `config/` | `System/Config` | `ConfigController` | ✅ |
| `dictionary/` | `Dictionary` | `DictionaryController` | ✅ |
| `log/` | `System/Log` | `SysLogController` | ✅ |
| `menu/` | `System/MenuManagement` | `MenuManagementController`（+ `MenuController`） | ✅ |
| **`organization/`** | `Organization` | **`OrganizationController`** | ✅ |
| `role-api/` | `RoleApi` | `RoleApiController` | ✅ |
| `role-menu/` | `RoleMenu` | `RoleMenuController` | ✅ |
| **`role-user/`** | `Role`（check 端点） | `RoleController` | ✅ |
| `role/` | `Role` | `RoleController` | ✅ |
| **`user/`** | `System/User` | **`UserController`** | ✅ |

> **所以：机构（`OrganizationController`）与人员（`UserController`）在后端**本来就住在框架层 `YZH.Core.Web`**。
> 前端把它们放进 core，不是"业务混入框架"，而是**框架层完整性的必然要求**——否则就会出现"后端框架层有 Organization，前端却在业务层"的**不对称**。
>
> ✅ **你的设计判断正确，且有 1:1 证据。V2 报告里"该不该在 core"的论证可以彻底封档。**

### 2.3 由此得出 core 的真实身份

> **core = 前端框架层，与后端 `YZH.Core.Web` 对称。**

这个定位一旦明确，"什么该进 core"就有了**客观判据**（不再需要逐案讨论）：

> **判据：后端 `YZH.Core.Web`（框架层）里有的东西，前端 core 就该有对应的镜像；后端在 `certplatform-api`（业务层）里有的，前端就不该进 core。**

用这条判据回测：
- `pages/system/*`（11 页）↔ `YZH.Core.Web/Controllers/System/*`（11 个）→ **该在 core** ✅
- `pages/auth/Login.vue` ↔ `UserController.login` → **该在 core** ✅
- `layouts/YzhAppLayout.vue` → 框架壳 → **该在 core** ✅
- 认证业务实体（`ISOStandard`/`CertStage`/`ent_enterprise`）→ 在 `certplatform-api` → **不该进 core** ✅（实测 0 处）

**这条判据应该写进文档**（见 §5 建议 1），它是你能给 AI 和未来自己的最有用的一句话。

---

## 三、core 作为"可复用前端核心"的成熟度模型

你说"未来 core 可以作为前端核心能力，在任意项目中使用"。这句话对应一个**成熟度阶梯**，我按实测给你定位：

| 级别 | 特征 | 消费方式 | 跨项目可行？ |
|---|---|---|---|
| **L0 源码复制** | 无 package.json | 复制粘贴 | ❌（会分叉） |
| **L1 源码共享** | 有 package.json，靠 alias | `vite alias → src` | ❌（需同仓） |
| **L2 约定化包** | + `exports` 映射 + `vite lib` 构建 + `peerDependencies` | 仍靠 alias | ⚠️（同仓/同工作区可行） |
| **L3 可发布包** | + `.d.ts` 类型 + 语义化版本 + CHANGELOG | `npm install` + pin 版本 | ✅ |
| **L4 独立产品** | + **后端契约可替换** + API 文档 + 准入判据 | 任意项目 / 任意后端 | ✅✅ |

### 3.1 当前位置：**L2**（实测）

| L3/L4 要求 | 现状 | 差距 |
|---|---|---|
| `.d.ts` 类型声明 | ❌ 无 `types` 字段、无 `.d.ts` 产出；`main: src/index.ts`（源码入口） | 需 `vite-plugin-dts` |
| 语义化版本 | ⚠️ `version: 1.0.0` 写死，无 CHANGELOG/changeset | 需版本策略 |
| 宿主声明依赖 | ⚠️ 只有 `cert-share` 声明 `file:`；**admin/auditor/enterprise 未声明** | 补 3 行 |
| 后端契约可替换 | ❌ 30+ 端点内置 | 需契约抽象（**大工程**） |
| API 文档 | ❌ 无（`docs/` 搜"五原子"= 0 命中） | 需 1 份 |
| 准入判据 | ❌ 未成文 | 需 1 页 |

### 3.2 **"L2 → L4" 不是一条直线，中间有一个战略岔路口**

```
L2（当前）──┬──→ L3 → L4   路径 A：做成可发布的通用包（需要契约抽象）
            │
            └──→ L2+        路径 B：定位为「YZH 前端架构」，把约定文档化
```

**两条路的差别不在技术，在"core 是什么"这个定位**：

| | **路径 A：通用前端核心** | **路径 B：YZH 前端架构** |
|---|---|---|
| core 的定位 | 与后端无关的通用 UI/架构库 | 一套后端约定的前端实现 |
| 要做什么 | 把 30+ 端点抽成接口/适配器（`configureYzhApi({ endpoints })` 或 provider） | 把"YZH 后端约定"**文档化 + 版本化** |
| 成本 | **高**（要动 CRUD 内核 + 所有 api 模块 + 三端回归） | **低**（写文档 + 加 `CONTRACT.md`） |
| 收益 | 任意后端都能用 | 任意**遵循该约定**的项目能用 |
| 对当前项目 | **零收益**，且拖慢 10/15 | 零成本，且立刻让"任意项目"这句话**可执行** |
| 风险 | 过度抽象（YAGNI）；单人维护成本上升 | 无 |

**我的建议：走路径 B，但为路径 A 留口子（不现在做）。**

理由：
1. **你真正的目标是"分离"，不是"通用"。** "分离"= 前端架构与项目工程解耦 —— **这个已经做到了（维度①②③）**。"通用"= 跨后端 —— 那是**另一个更大的目标**，且当前没有任何需求驱动它。
2. **路径 A 在当前会破坏"科学性"**：把端点抽成接口，会新增一层抽象，而这一层**只有 1 个实现**（本项目的后端）。单实现的抽象是负债，不是资产。
3. **路径 B 是路径 A 的必经前置**：你**必须先写清楚"约定是什么"**，才可能把它抽象成接口。所以路径 B 无论如何都要做，且成本极低。
4. **路径 A 的口子已经在**：`configureYzhApi` 已经是注入点。将来要加 `endpoints` 注入，是**扩展而非重构**。

---

## 四、历史问题：彻底清理方案（你明确要求解决）

你说"死代码、冗余代码、vol 残留等历史问题，的确需要彻底解决"。我分三类给你**精确清单 + 处置方式**。

### 4.1 死代码 —— 1,984 行（13.4%），全部实测零引用

| 文件 | 行数 | 处置 |
|---|---|---|
| `utils/treeOps.ts` | 483 | 🗑 删除 |
| `utils/treeUtils.ts` | 584 | 🗑 删除 |
| `components/layout/YzhTreeTableSelector.vue` | 417 | 🗑 删除 |
| `components/ui/{YzhCard,YzhEmptyState,YzhStatusBadge}.vue` | 219 | 🗑 删除 |
| `logic/LinkTableCore.ts` + `composables/useLinkTable` | 147 | 🗑 删除 |
| `composables/useTable.ts` | 49 | 🗑 删除 |
| `composables/useAuth.ts` | 39 | 🗑 删除（**第二套 auth 实现**，误用会造成两个 token 真相源） |
| `composables/useConfirm.ts` | 37 | 🗑 删除 |
| `types/ApiResponse.ts` | 9 | 🗑 删除 |
| | **≈1,984** | 同步删除 `index.ts` / `logic/index.ts` / `utils/index.ts` / `composables/index.ts` 的导出行 |

⚠️ **唯一不可删**：`logic/TreeTableLogic.ts`（别名）—— `cert-admin/src/pages/{workflow/skill-manage,foundation/iso-standard}/logic.ts` 两个宿主仍在 `extends TreeTableLogic<any>`。

> **验证**：删后 `guards.mjs` + `vue-tsc` 双绿 + 三端 `typecheck`。
> **方法提醒**：`export *` barrel 会让死代码"看起来在用"，**必须按符号名逐个 grep**（本次已逐个核过）。

### 4.2 冗余代码

| 冗余 | 位置 | 处置 |
|---|---|---|
| `Page`/`PageParams` **重复定义** | `types/Page.ts` 与 `components/table/types.ts:100-113` | 删一处（barrel 中后者被静默遮蔽） |
| **树构建重复实现 3 处** | `treeUtils.ts:47`(通用版，死) / `api/system/menu.ts:64` / `pages/system/api/logic.ts:155` / `YzhTreeTableCheckSelector.vue:364` | 删通用版后，**评估是否合并剩下 3 处**（不急） |
| **契约归属错位** | `YzhTableColumn`/`YzhAction`/`YzhFormField` 住在 `components/`，导致 4 层反向 import + 宿主伸手进组件层 | 搬到 `types/table.ts`、`types/form.ts` |
| `api/system/{role-api,role-menu,role-user}` **同名函数冲突** | `checkAdd`/`getCheckTree`… 三份几乎相同 | 参数化（可延后） |

### 4.3 vol 残留 —— **实测只有 3 行注释，比你预期的干净得多**

| 位置 | 内容 | 性质 |
|---|---|---|
| `components/ui/YzhEmptyState.vue:27` | `对齐 vidlang EmptyState` | 注释 |
| `components/ui/YzhStatusBadge.vue:12` | `对齐 vidlang Badge + 4 语义子类` | 注释 |
| `components/table/types.ts:2` | `替代 vol 的 view-grid` | 注释 |

→ **core 里没有任何 vol 的代码依赖**（0 处 `VolBox`/`VolForm`/`VolProvider`/`view-grid` 引用）。
→ **"vol 残留"的重灾区在别处**：`cert-admin` 的 **97 处内联 `<el-table>`**（R6 守卫已冻结存量）+ `src/old/` 整个目录。

> **纠正认知**：core 的 vol 残留 = 3 行注释（建议保留，它们是**设计来源的可追溯记录**，删了反而丢失"为什么长这样"的信息）。**真正需要"彻底解决"的是 `cert-admin` 的 97 处 `<el-table>`**，那才是 vol 时代遗留的实现方式。

### 4.4 文档与实现不符（新发现，P1）

| 问题 | 证据 |
|---|---|
| `src/assets/README.md` 描述了**不存在的目录** `assets/entityconfig/` | `find assets -type f` → 只有 `README.md` |
| README 说配置走 `HTTP API (/api/entityconfig/{name})`，**与实现不符** | 实现是 `/api/{controllerName}/config`（`SingleTableCore.ts:283`） |

→ 这个 README 描述的是**早期设计意图**，实现已演进但文档没跟。**要么改文档，要么建目录**——但**不能留着一个撒谎的 README**（AI 会照它写代码）。

---

## 五、建议（按你的目标排序）

### 建议 1（最高优先级，~1h）—— 把"core = 前端框架层"这个定位**写下来**

在 `docs/10-YZH架构/03-前端架构.md` 新增一节，包含 4 件事：

1. **定位**：`yzh.vue.core = 前端框架层，与后端 YZH.Core.Web 对称`
2. **★ 准入判据（一句话，可机械执行）**：
   > 后端 `YZH.Core.Web`（框架层）有的 → 前端 core 建镜像；后端在 `certplatform-api`（业务层）有的 → **不进 core**。
3. **红线**：core 永不出现 `ISOStandard`/`CertStage`/`CertificationBody`/`cert_org_*`/`ent_enterprise`/`ValidationRule`/报告模板/NC 规则
4. **分离的 4 个维度 + 当前状态**（本文 §1 的表）→ 让"彻底分离"变成**可检验的指标**，而不是口号

> **收益**：AI 和未来的你不再需要"逐案讨论该不该进 core"。**这是把"分离"从愿景变成机制的第一步。**

### 建议 2（~1h）—— 写 `CONTRACT.md`：把"后端约定"**文档化**

既然定位是"YZH 前端架构"（路径 B），那**约定就是契约的一部分，必须写下来**：

```markdown
# yzh.vue.core 后端契约（CONTRACT.md）

core 依赖以下后端约定。任何使用 core 的项目，其后端须满足：

## 1. 响应信封
{ success: boolean, message: string, data: T, code: number, timestamp: number }
例外 E6: /api/User/getVierificationCode → 裸 { img, uuid }
例外 E7: StandardDirectory* → { code, data, msg }（无 success）

## 2. CRUD 端点约定
GET  /api/{controllerName}/config     → EntityConfig（列/表单/校验/搜索/启禁）
POST /api/{controllerName}/filter     → PagedData<T>
POST /api/{controllerName}/add | update | delete

## 3. 树端点约定
POST /api/{controllerName}/tree/root | tree/children | tree/add | tree/update
POST /api/{controllerName}/tree/delete | tree/toggle-valid
POST /api/{controllerName}/tree/action/{methodName}

## 4. 勾选授权端点约定
POST /api/{controllerName}/checkTree | check/add | check/remove | check/all

## 5. 系统端点
/api/User/{login,getVierificationCode,getCurrentUserInfo,updateUserInfo,modifyPwd,ping}
/api/System/MenuManagement/{tree,tree/all,add,update,delete}
/api/ApiSync/{list,sync,scan}
/api/file-storage
```

> **收益**：把"任意项目中使用"这句愿景**变成可执行的准入条件**。同时它也是**路径 A（契约可替换）的必经前置**。

### 建议 3（~1.5h）—— 彻底清历史债（§4 全部）

按 §4.1 → §4.2 → §4.4 顺序执行；§4.3 的 3 行注释**建议保留**。

### 建议 4（~20min）—— 补机制，让"分离"不再腐化

| 措施 | 内容 |
|---|---|
| **守卫 R12** | core 禁认证业务实体（roots = `yzh.vue.core/src`）→ **把 §建议1 的红线变成编译期拦截** |
| **扩 R10 roots** | 补 `logic/`、`utils/`、`adapters/`、`types/`（当前是"禁反向依赖"的盲区） |
| **补依赖声明** | 三端 `package.json` 补 `"yzh.vue.core": "file:../../yzh.vue.core"`（3 行，让依赖可见） |
| **命名纠正** | `package.json` description 从"npm 包"改为"**前端框架层（源码共享，vite alias 消费）**" |

### 建议 5（专家端接入，与"分离"直接相关）

**专家端接入 core = "分离"的第一次真实验收**。当前 core 的复用只有 admin 一个样本；接入后：
- 若顺利 → **"分离"假设被验证**，core 可以放心继续承接框架层能力
- 若发现不够通用 → **趁早发现**（比等到 enterprise 端接入便宜得多）

**接入前需补 2 个通用性缺口**（详见 V2 §8.4）：
- **G-1** core `Login.vue` 加 `#extra` 插槽（专家端要"注册"入口）
- **G-2** `YzhAppLayout` 加 `#header-extra` 插槽 + `tenantName` prop（专家端要显示当前机构名）

⚠️ **阻塞预警**：库重建后 `sys_menu` 无 `Tag='auditor'` 行 → 接入后侧栏空白会被误判为"接入失败"，**接入前须只读确认**。

---

## 六、三阶段演进路线

| 阶段 | 时点 | 目标 | 内容 |
|---|---|---|---|
| **阶段 1** | 现在（~4h 总计） | **让"分离"从愿景变成机制** | 建议 1（写定位+判据）+ 建议 2（CONTRACT.md）+ 建议 3（清死代码/冗余）+ 建议 4（守卫 R12 + 扩 R10） |
| **阶段 2** | 10/15 前 | **验收"分离"** | 建议 5（专家端接入，补 G-1/G-2）→ 产出《core 通用性缺口记录》 |
| **阶段 3** | 10/15 后（按需） | **决定是否走路径 A** | 若确有"跨后端复用"需求 → 在 `configureYzhApi` 上加 `endpoints` 注入（**扩展，非重构**）；否则**停在 L2+ 即可** |

> **关键**：**阶段 3 是可选的**。你的核心目标是"分离"，阶段 1+2 完成即达成。**不要为"未来可能用得上"提前付 L3/L4 的成本。**

---

## 七、一句话回答你的问题

> **「yzh.vue.core 为什么要做架构和项目分离？」**
>
> 因为它是**前端框架层**——与后端 `YZH.Core.Web` 对称。框架层与业务项目分离，才能让"机构-人员、角色-人员、菜单、字典、日志、接口管理"这些**每个项目都要的底层支撑**只写一次、被所有端复用，而不是在每个项目里各写一份。
>
> **当前分离度**：运行时 ✅ / 依赖方向 ✅ / 环境 ✅ / **契约 ⚠️（唯一 gap）**。
> **所以"分离"已经做到 3/4。剩下那 1/4 不是"没做"，而是"没写清楚"** —— 把 YZH 后端约定写成 `CONTRACT.md`，这一步就补上了，**成本 1 小时**。
>
> 而"机构-人员进 core"这个决策，**有 1:1 的对称证据支持（11 页 ↔ 11 控制器），是对的**。

---

## 八、附录：本版新增取证命令

```bash
cd src/certplatform-web/yzh.vue.core/src

# 1. core 无启动责任
find .. -maxdepth 2 \( -name main.ts -o -name App.vue -o -name index.html \) | grep -v node_modules   # → 空

# 2. core 硬编码后端端点（30+）
grep -rnoE "'/api/[A-Za-z/{}._-]+'" . | sort

# 3. core 里 /api 出现总次数
grep -rn "/api/" . | grep -v '\.md' | wc -l      # → 91

# 4. CRUD 约定（controllerName 拼 URL）
grep -nE "apiGet\('|apiPost\('" logic/SingleTableCore.ts logic/TreeTableCore.ts

# 5. 后端框架层 System 控制器（11 个）—— 对称性证据
ls ../../../yzh-core/YZH.Core.Web/Controllers/System/

# 6. vol 残留（应只有 3 处注释）
grep -rniE "volbox|volform|volprovider|view-grid|vidlang" . | grep -v '\.md'

# 7. 项目专有痕迹（应全是 props 默认值或注释）
grep -rnE "映智汇|ISO|CertStage" . | grep -v '\.md'

# 8. assets/README.md 描述的目录是否存在
find assets -type f          # → 只有 README.md（entityconfig/ 不存在）
```

---

> **最后一句**：你这次的澄清让我确认了一件事 —— **你的架构方向不是"试出来的"，而是有明确意图的**（框架层与项目分离、启动由宿主负责、能力可被调用、未来可复用）。**实测证明这个意图已经落地了 3/4**。真正需要做的不是"再做结构改动"，而是**把已有的分离成果"写成文档 + 加进守卫 + 清掉历史债"**，让它可以被继承、被检验、不被腐化。
