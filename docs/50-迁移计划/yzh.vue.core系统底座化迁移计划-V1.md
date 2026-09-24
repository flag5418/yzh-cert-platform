# yzh.vue.core 系统底座化迁移计划-V1

> status: 已完成（P0–P8 · 2026-09-24 收口）
> 日期: 2026-09-24
> 范围: 前端 certplatform-web（四端）+ 后端对称项（yzh-core / certplatform-api）
> 前置必读: `docs/10-YZH架构/样板页面指南-V1.md`、`01-架构总纲.md`、`06-代码结构规范.md`、`docs/30-项目规则/前后端代码结构统一规则-V1.md`
> 执行方式: 逐阶段推进，每阶段过门禁后进入下一阶段；每模块独立 commit 可回滚
> 关键结果: system 11 页 + 登录/布局/首页 + api/system + yzhSystemRoutes(11) 全在 `yzh.vue.core`；P7 后端 Config/SysLog 下沉；P8 样板路径与文档同步完成

---

## 0. 审批摘要

| 项 | 结论 |
|----|------|
| 核心命题 | 后端早已把「每个项目都需要的系统模块」下沉框架层（`YZH.Core.Web/Controllers/System/`）；前端补完同一动作——系统页面、登录、布局、首页、系统 API、原子路由全部沉入 `yzh.vue.core` |
| yzh.vue.core 定位 | **YZH 架构的 npm 包（纯包、系统底座）**：五原子 = 原子组件 + 原子方法 + 原子 API + 原子路由 + 原子界面；不启动、不感知后端地址、不引用项目 |
| 宿主定位 | **独立可运行的 Vue 启动框架**：main.ts / createRouter / env / vite proxy 全权；引用 core 五原子组装应用，特色页可手写覆盖 |
| 迁移范围 | system 11 页面（3716 行，全迁）+ 应用壳（登录/布局/默认首页）+ auth/menu 依赖吸收 + 后端 Config/SysLog 对称下沉 |
| 执行顺序 | P0 文档守卫基线 → P1 依赖吸收 → P2 壳(Wave S) → P3 达标页(Wave A) → P4 整改页(Wave B) → P5 重形态页(Wave C) → P6 清理 → P7 后端对称 → P8 收口门禁 |
| 独立运行 | core **不建**运行壳（已拍板）；「独立」指包边界独立，宿主负责启动 |
| 决策点 | D-1~D-6（§11）按推荐值执行，执行至对应阶段前可改 |

---

## 1. 背景与现状断层

### 1.1 前后端不对称（迁移动因）

| 维度 | 后端 ✅ 已是底座 | 前端 ❌ 未对齐 |
|------|------------------|----------------|
| 系统模块归属 | User/Role/Org/Menu/Dictionary/RoleMenu/RoleApi 控制器已在框架层 `src/yzh-core/YZH.Core.Web/Controllers/System/`（判据=「每个项目都需要」） | 11 个 system 页面散落 `cert-admin/src/pages/system/`（3716 行） |
| 壳层 | SSO/Token/验证码/改密等接口在框架层 | 登录 admin 395 行 + auditor 420 行**重复手写**；布局 577 + 153 行重复 |
| auth API | 单一契约 | **两处实现**：`yzh.vue.core/api/auth.ts`（裸 fetch 无验证码）+ `cert-share/api/auth.ts`（yzhApi 全功能），core 注释自认「两处实现」 |
| 首页 | — | 不存在：`/` 直接 redirect `/system/organization` |
| 后端地址 | — | ⛔ core 硬编码 `client.ts:337` 与 `cert-admin/api/system/api.ts:58` 均兜底 `http://127.0.0.1:9992` |
| 定位表述 | 「纯 RBAC 底座」 | 文档定义为「业务无关组件库」；全仓无系统页面下沉计划 |

### 1.2 system 页面现状分类（迁移输入，实测）

| 类别 | 模块 | 行数 | 动作 |
|------|------|------|------|
| ✅ 达标 4 | user(67=48+19,唯一样板) / role(16 行零覆写) / config / log | 414 | 直接搬迁 |
| ⛔ 待整改 5 | menu、api（无内核+裸 el-table+`res.code===200`）；role-api/role-menu/role-user（继承自建 `_shared/BaseRoleTreeLogic` 而非 yzh 内核） | 1527 | 就地整改达标 → 搬迁 |
| ⚠️ 重形态 2 | organization(756) / dictionary(651) | 1407 | 重构为标准 useTreeTable 形态 → 搬迁 |

---

## 2. 目标架构

### 2.1 五原子模型（本计划确立的定位表述）

**yzh.vue.core = YZH 架构的 npm 包**，包含且仅包含：

| 原子 | 内容 | 现状 |
|------|------|------|
| 原子组件 | YzhTable/YzhForm/YzhTree/布局壳等 15 个（守卫 R1 零领域依赖，**维持原状**） | ✅ 已有 |
| 原子方法 | SingleTableCore/TreeTableCore/CheckTreeCore 内核 + useSingleTable 等 composables + tree 工具 | ✅ 已有 |
| 原子 API | yzhApi 传输层 + auth（合并后唯一实现）+ file-storage + **api/system/***(新增) | ⚠️ 部分 |
| 原子路由 | `yzhLoginRoute` / `yzhHomeRoute` / `yzhSystemRoutes` / `createYzhRoutes()` (新增) | ❌ 无 |
| 原子界面 | 登录页 / 默认首页 / 11 系统页 / YzhAppLayout (新增) | ❌ 无 |

**宿主工程 = 独立可运行的 Vue 启动框架**：main.ts、index.html、vite.config（含 proxy）、createRouter 组装、env、品牌注入、业务页面、特色页手写。

### 2.2 目标结构

```
业务工程（cert-admin / cert-auditor / 未来新项目）＝ 宿主（独立运行）
  main.ts · vite proxy · .env · createRouter 组装 · 品牌 props · 业务/特色页面
  │  npm 包方式引用（alias @yzh-core / file: 两种现网已验证）
  ▼
yzh.vue.core ＝ 纯包（不启动 · 不感知后端地址 · 不引用项目）
  ├── components/        原子组件（R1 不变）
  ├── logic/             原子方法（逻辑内核）
  ├── composables/       + useAuthState / useMenuTree / useMenuChanged（模块单例，零 pinia）
  ├── api/               yzhApi + configureYzhApi() + auth（合并唯一实现）+ file-storage
  ├── api/system/        ★ 系统域 API（menu / api-sync / role-api / role-menu / role-user）
  ├── pages/auth/        ★ 登录页（默认皮肤）
  ├── pages/home/        ★ 默认占位首页
  ├── pages/system/      ★ 11 个系统管理页
  ├── layouts/           ★ YzhAppLayout（侧栏+顶栏+个人中心+改密）
  ├── router/            ★ 原子路由导出 + createYzhRoutes()
  └── utils/             + filterMenuTreeByTag / formatMenuIcon（自 cert-share 迁入）
  ▼
后端对称层：src/yzh-core/YZH.Core.Web/Controllers/System/（已存在；补 Config/SysLog 下沉）
```

### 2.3 界限分割表（权威定义）

| 关注点 | yzh.vue.core（纯包） | 项目（宿主工程） |
|--------|---------------------|------------------|
| 后端地址 | ❌ 不感知——零硬编码（守卫 R11），初始 `baseURL=''` | ✅ `configureYzhApi({baseURL})` 注入；dev 走宿主 vite proxy，prod 走同源部署，跨域才用宿主 `.env` |
| 接口路径契约 | ✅ 知道 `/api/{Controller}/{action}` 固定约定（YZH 架构协议=包的「语法」） | 决定协议挂在哪个地址 |
| 项目代码 | ❌ 零引用——禁 `@/`、`@share/`（守卫 R10） | — |
| 五原子 | ✅ 提供 | ✅ 引用、组装、按 path 覆盖 |
| 启动权 | ❌ 无 main.ts/index.html/dev server | ✅ 全权 |
| 状态 | ✅ 模块单例 composable，零 pinia | pinia 薄适配（可选，沿用 store/menu.ts 模式） |
| 品牌/特色 | 默认件 + props/slots | 注入品牌；登录/注册/首页可整页手写（auditor 模式） |

**「不感知后端」精确澄清（防过度解读，须随文档同步）**：

> = 不知道后端**在哪**（地址/端口/部署/环境），而非不知道怎么调后端。
> `/api/System/User/config` 等路径约定是 YZH 架构协议，core 必须知道才能驱动系统页面；
> **地址与部署形态**才是项目控制权。即：**core 知道「说什么话」，项目决定「对谁说」。**

### 2.4 归属判定法则（前端版黄金法则）

> 第一问（同后端）：「换个完全不同的项目，这段代码还需要吗？」→ 否：宿主
> 第二问：「还需要的部分，逐字相同吗？」→ 逐字相同：core；结构同皮肤异：core 默认件 + props/slots；内容即业务：宿主覆盖路由
> 歧义时：归 core 做**默认可替换件**，宿主保有路由最终决定权。

### 2.5 登录 / 布局 / 首页 / 注册专项判定

auditor 现状实证（活标本）：`/login` 手写 420 行、`/register` 手写、`/`→手写布局+overview 首页、无 system 页——已是「独立启动框架+特色页手写」，缺的正是「可引用的原子能力」。

| 组件 | 判定 | 归属 | 定制机制 |
|------|------|------|----------|
| 登录页 | 流程逐字同、皮肤异 | core `pages/auth/Login.vue` | props：`appLogo/appTitle/appSubtitle/features[]/footer`；宿主可整页覆盖 `/login` |
| auth API（login/验证码/个人资料/改密/ping） | 逐字同（后端本在框架层） | core `api/auth.ts` 唯一实现 | 合并两份重复，废除裸 fetch 版 |
| 应用布局骨架（侧栏/顶栏/视口） | 逐字同 | core `layouts/YzhAppLayout.vue` | props：`menuTag/logoText`；slots：`brand/header-right/user-dropdown` |
| 个人中心 + 改密弹窗 | 系统功能（后端接口在框架层） | 随布局进 core | — |
| 菜单树/登录状态 | 逐字同 | core composables 模块单例 | 宿主 pinia 薄适配 |
| 默认首页 | 骨架通用、内容业务 | core 占位件（欢迎+菜单快捷入口，**无业务图表**） | 宿主可覆盖 `/`（业务仪表盘归宿主） |
| 注册页 | 业务流程，core 无需有 | **宿主手写**（auditor 范例） | — |
| createRouter / 守卫结构 / 业务路由 | 组合关系 | **宿主** | core 只导出片段与组装函数 |

---

## 3. 三条契约铁律（模型成立的前提）

| # | 铁律 | 落地方式 |
|---|------|----------|
| 1 | **每条 path 单一来源** | 宿主对每个 path 二选一：spread core 原子路由 **或** 手写自己的组件；永不双注册（规避 vue-router 同 path 歧义） |
| 2 | **core 零地址、零宿主依赖** | 删全部硬编码地址；`configureYzhApi()` 由宿主注入；守卫 R10（禁 `@/`/`@share/`）+ R11（禁 IP/host 硬编码） |
| 3 | **细粒度命名导出** | `yzhLoginRoute`、`yzhHomeRoute`、`yzhSystemRoutes`、`createYzhRoutes({businessRoutes, menuTag, branding, redirect})`——宿主可整组用、单条用、全不用 |

### 3.1 后台地址新契约

| 规则 | 内容 |
|------|------|
| 现状违例 | `yzh.vue.core/src/api/client.ts:337` 与 `cert-admin/src/api/system/api.ts:58` 硬编码 `\|\| 'http://127.0.0.1:9992'` |
| 整改 | 两处硬编码删除；`yzhApi` 初始 `baseURL=''`（相对路径 `/api/*`） |
| 宿主注入 | core 导出 `configureYzhApi({baseURL?, getToken?, onUnauthorized?, onError?})`，宿主 main.ts 启动时调用，值来自宿主 env/配置 |
| 缺省语义 | `baseURL=''` → 相对路径 → **dev 由宿主 vite proxy 承接（admin/auditor 均已有 `/api→9992`）、prod 由同源/nginx 承接**——默认路径零风险 |
| 防回潮 | 守卫 R11：`yzh.vue.core/src/**` 禁 `127.0.0.1`、`localhost:<port>`、`http(s)://<host>` |

### 3.2 宿主接入契约（目标形态示例）

```ts
// cert-admin：最大化引用（不造车）
const routes = createYzhRoutes({
  menuTag: 'admin',
  branding: { appTitle: '映智汇认证管理平台' },
  businessRoutes: [/* foundation/workflow 手写业务路由 */],
})
// main.ts: configureYzhApi({ baseURL: import.meta.env.VITE_API_BASE ?? '' })

// cert-auditor：特色页手写 + 系统能力引用（混合示范）
const routes = [
  { path: '/login',    component: () => import('@/layouts/AuditorLogin.vue') },   // 手写
  { path: '/register', component: () => import('@/pages/register') },             // 手写
  { path: '/', component: YzhAppLayout, redirect: '/overview',                    // core 原子布局
    children: [
      { path: 'overview', component: () => import('@/pages/overview') },          // 手写首页
      ...yzhSystemRoutes,                                                          // core 原子路由（按需）
    ]},
]
```

宿主五接缝：① 路由组装 ② pinia 薄适配 ③ 菜单数据（DB `Sys_Menu`，Url=路由 path，机制不变）④ 品牌 props/slots ⑤ `notifyMenuChanged()/onMenuChanged()` 菜单变更事件。

---

## 4. 迁移范围

### 4.1 纳入

| 类 | 项 |
|----|----|
| system 页面 | organization、user、role、role-user、role-menu、role-api、menu、api、dictionary、log、config（**11 个全迁**，config 已拍板：系统参数是每个系统都需要的底层支撑，阿里云等差异属数据配置非代码） |
| 应用壳 | 登录页、YzhAppLayout（含个人中心/改密）、默认占位首页 |
| 依赖吸收 | auth API 合并、menu API + useMenuTree + 菜单 utils 迁入、useAuthState/useMenuChanged、`_shared/` 消化 |
| system API | cert-admin `api/system/{api,role-api,role-menu,role-user}.ts` + cert-share `api/system/menu.ts` → `core/api/system/` |
| 配套组件 | `MenuFormDialog.vue`、`IconPicker.vue`（随 menu 页） |
| 后端对称 | ConfigController + SysLogController + EntityConfig JSON 下沉 `YZH.Core.Web`；MenuManagementController 升级 TreeTable（D-1） |

### 4.2 不做

- ❌ core 独立运行壳（index.html/main.ts/dev server）——已拍板
- ❌ 改 `components/**` 的 R1 零领域约束
- ❌ 动 `src/old/**`；改 `Sys_Menu.Url`/路由 path（对外 URL 恒定）
- ❌ foundation/workflow 业务页面迁移
- ❌ 本批改造 cert-auditor/cert-enterprise（auditor 列**后续跟进示范宿主**，见 §12）
- ❌ 注册页进 core（业务流程，宿主手写）

---

## 5. 分阶段执行计划

> 总序：P0 → P1 → P2(Wave S) → P3(Wave A) → P4(Wave B) → P5(Wave C) → P6 → P7 → P8
> 模块策略（已拍板）：**逐模块「就地整改达标 → 搬迁 → 验证」**，债务不进框架层。
> Commit 约定：每模块两笔——`refactor(system/{x}): 就地整改达标` + `feat(yzh-core): 迁入 system/{x}`，独立可回滚。

### P0 文档与守卫基线

| # | 动作 | 文件 |
|---|------|------|
| 0.1 | 本计划落盘 | `docs/50-迁移计划/yzh.vue.core系统底座化迁移计划-V1.md` |
| 0.2 | 守卫扩展：`PAGE_ROOTS += yzh.vue.core/src/{pages,layouts}`、`API_ROOTS += yzh.vue.core/src/api`；新增 R10、R11 | `scripts/guards.mjs` |
| 0.3 | 登记「系统底座包」定位与五原子模型 | `docs/10-YZH架构/03-前端架构.md`、`06-代码结构规范.md` |
| 0.4 | 三层同构 System 域映射修订：`YZH.Core.Web/Controllers/System ↔ yzh.vue.core/src/api/system ↔ yzh.vue.core/src/pages/system`（业务域映射不变） | `docs/30-项目规则/前后端代码结构统一规则-V1.md`、`docs/10-YZH架构/06` |
| 0.5 | AGENTS.md + 知识库副本预告标注（样板路径、system 归属将变） | `AGENTS.md`、`docs/30-项目规则/知识库/AGENTS.md` |

**门禁**：`node scripts/guards.mjs` 0 违规。

### P1 依赖吸收与承接结构（搬迁前置）

| # | 动作 |
|---|------|
| 1.1 | **auth API 合并**：以 cert-share 的 yzhApi 全功能版（login/验证码/getCurrentUser/updateUserInfo/modifyPwd/ping）为准并入 `core/api/auth.ts`；废除 core 裸 fetch 版；清除「两处实现」注释债务 |
| 1.2 | **菜单依赖吸收**：`cert-share/api/system/menu.ts`(146) + `useMenuTree.ts`(49) + `filterMenuTreeByTag/formatMenuIcon` → 迁 core；share/宿主 import 源改指向 core（方向合法 share→core） |
| 1.3 | **composables**：`useAuthState()`（模块单例 token/userInfo/clear，对接 tokenStore）+ `useMenuChanged()`（CustomEvent：`notifyMenuChanged/onMenuChanged`） |
| 1.4 | **承接目录与导出**：`core/src/{pages/{auth,home,system},layouts,router,api/system}`；`router/` 导出 `yzhLoginRoute/yzhHomeRoute/yzhSystemRoutes/createYzhRoutes()`；`package.json` exports 增 `./router`、`./pages/*`、`./layouts`、`./api/*`；vue-router 入 peerDependencies；vite lib external 补 vue-router |
| 1.5 | **宿主 pinia 薄适配**：`store/auth.ts`、`store/menu.ts` 改为 re-export core composables（对外接口不变，调用方零改动） |
| 1.6 | **后台地址解耦**：删 `client.ts:337` 硬编码（初始 `baseURL=''`）+ core 增 `configureYzhApi()` + 两宿主 main.ts 注入；`api/system/api.ts:58` 硬编码随 Wave B 搬迁时删 |

**门禁**：guards → `yzh.vue.core` typecheck/build → 三端 build。

### P2 Wave S：应用壳（登录 → 布局 → 首页）

| 步骤 | 动作 |
|------|------|
| S1 登录 | cert-admin 内就地把 AdminLogin 品牌硬编码改 props + 登录逻辑接 core auth/useAuthState → 迁 `core/pages/auth/Login.vue` → 宿主 `/login` 指向 core → 删 `AdminLogin.vue` |
| S2 布局 | AdminLayout 改 `menuTag/logoText` props + brand/header-right/user-dropdown slots；个人中心+改密弹窗随迁；菜单/用户状态接 core composables → 迁 `core/layouts/YzhAppLayout.vue` → 宿主 `/` 组件指向 core → 删 `AdminLayout.vue` |
| S3 首页 | 新建 `core/pages/home/Home.vue`（欢迎 + 读菜单树快捷入口 + 版本信息，用 YzhPageLayout/YzhCard）；`createYzhRoutes` 缺省 `/`→此页 |
| S4 验证 | 登录→侧栏→菜单跳转→个人中心→改密→退出 全链路回归；guards/build |

### P3 Wave A：4 达标页直接搬迁（顺序 log → config → role → user）

每页四步作业卡：

| 步骤 | 动作 |
|------|------|
| ① 搬迁 | `mv cert-admin/src/pages/system/{x} → core/src/pages/system/{x}`，import 归位 |
| ② 路由 | `yzhSystemRoutes` 注册该页；cert-admin children 删除对应条目 |
| ③ 清理 | 删旧目录（独立 api 文件迁 `core/api/system/`） |
| ④ 验证 | guards 0 违规 → 页面实测（列表/搜索/增删改）→ core typecheck → cert-admin build |

`user` 最后搬；搬迁同时在样板指南加路径切换预告（正式切换在 P8）。

### P4 Wave B：5 待整改页（整改达标 → 搬迁；debt 白名单随整改摘除）

顺序：role-user → role-menu → role-api（共用模式）→ menu → api。

| 页 | 整改动作（cert-admin 就地） | 关键决策/风险 | 搬迁附加 |
|----|------------------------------|---------------|----------|
| role-user / role-menu / role-api | 弃 `_shared/BaseRoleTreeLogic`(271 行)；三页重写为 `extends CheckTreeCore` + `useCheckTree`（core 内核 check 端点约定与 Role/RoleMenu/RoleApi 三控制器**完全对口**）；role-user 的 `yzhApi.post('/api/Role/tree/*')` 直调收编进 api 模块 | 预计零后端改动 | 删 `_shared/`；摘 R2 debt（useRoleTreeBadges）；3 个 api 文件迁 core |
| menu | 重写为 `TreeTableCore`+`useTreeTable`+`YzhTreeTableLayout`：消灭裸 el-table、8 个手写 handle*、`res.code===200`×5、camelCase 字段；`MenuFormDialog`+`IconPicker` 收进页面目录改 YzhFormDialog 实现；`refreshMenus()` 直调 → `notifyMenuChanged()` | **R-1（唯一前后端联动点，D-1）**：推荐 `MenuManagementController` 升级 `TreeTableControllerBase`+TreeConfig；备选=内核覆写适配现有 `/tree` 端点（配置驱动打折） | 摘 R3/R3b/R6 debt；cert-share menu CRUD import 源改 core |
| api（接口管理） | 重写为 `YzhTreeTable`（分组树+接口表）+ toolbar 同步/Swagger 注册为 custom action 走 dispatch；消灭裸 el-table | 列配置（D-2）：推荐后端补 `/config`；工期紧可手写 columns 并登记例外；`sys_api.Enable` 维持已登记例外 | 摘 R6 debt；`api/system/api.ts` 迁 core（顺带删第 58 行硬编码） |

### P5 Wave C：2 个历史重形态页

| 页 | 整改 | 搬迁 |
|----|------|------|
| organization(756) | index.vue 429 行手写分发 → 标准 `useTreeTable`+`YzhTreeTableLayout`（抄 role/index.vue 骨架）；logic 人员/机构双弹窗保留为合法 virtual 覆写 | 四步作业卡 |
| dictionary(651) | index.vue 314 行 → 标准形态；logic 树/字典项弹窗保留覆写；`fetchItems` 手拼 filter → `buildFilters` | 四步作业卡 |

### P6 清理

- cert-admin：`pages/system/`、`api/system/`、`layouts/`、`components/{MenuFormDialog,IconPicker}` **全部清零**（ls 验证）
- cert-share：`api/system/`、`api/auth.ts`、`useMenuTree`、菜单 utils **全部迁走**，只剩业务 API（cert/workflow）
- guards debt：R2/R3/R3b/R6 中 system 相关条目确认删除；债务盘点 A（直连 @/api 页面）system 项清零

### P7 后端对称（可与 P8 并行，D-5）

1. `ConfigController`(84 行) + `SysLogController`(55 行) + 对应 EntityConfig JSON → `src/yzh-core/YZH.Core.Web/Controllers/System/`（[Route]/namespace 修正）
2. MenuManagementController 升级 TreeTable + TreeConfig（若 D-1 选推荐，已随 P4 执行则此步跳过）
3. `scripts/backend` 脚本启停 + system 全端点冒烟；编译 0 错误

### P8 收口与全量门禁

| # | 动作 |
|---|------|
| 8.1 | **样板指南路径切换**：唯一样板 → `yzh.vue.core/src/pages/system/user`；`cp -r` 源路径更新；§六「不要参考」清单移除已整改的 menu/api/role-*/organization/dictionary |
| 8.2 | 同步：`项目全局规则.md` §6.1 目录树、`12-框架能力清单-V1.md` 前端页列、`07-开发流程.md`、`docs/10-YZH架构/README.md` 漂移修正 |
| 8.3 | 三层同构检查清单（统一规则 §六 C-A/C-F 系列）按新 System 映射逐项跑 |
| 8.4 | **全量门禁**：guards 0 违规 + 0 system debt → core build+typecheck → cert-admin/auditor/enterprise build → 后端编译 0 错 → 11 系统页 + 登录 + 布局手工回归 |

---

## 6. 执行顺序总览

```
P0 文档+守卫基线
P1 依赖吸收（auth 合并 / 菜单吸收 / composables / 承接结构 / pinia 薄适配 / 后台地址解耦）
P2 Wave S：登录 → 布局 → 默认首页
P3 Wave A：log → config → role → user
P4 Wave B：role-user → role-menu → role-api → menu → api
P5 Wave C：organization → dictionary
P6 cert-share / cert-admin 残留清零
P7 后端对称（Config/SysLog 下沉；Menu TreeConfig）
P8 文档收口 + 全量门禁
（后续跟进）cert-auditor 示范宿主接入
```

---

## 7. 守卫与验证策略

| 守卫 | 状态 | 内容 |
|------|------|------|
| R1~R7 | 维持 | R1 仍只圈 `components/**`（零领域依赖不变） |
| PAGE_ROOTS/API_ROOTS | P0 扩展 | 增 `yzh.vue.core/src/{pages,layouts}`、`yzh.vue.core/src/api` |
| **R10（新增）** | P0 启用 | `yzh.vue.core/src/{pages,layouts,router,composables,api}/**` 禁 `from '@/`、`from '@share/`（防宿主反向依赖） |
| **R11（新增）** | P0 启用 | `yzh.vue.core/src/**` 禁 `127.0.0.1`、`localhost:<port>`、`http(s)://<非空host>`（防地址回潮） |
| debt 摘除 | 随整改 | R2:`system/_shared/useRoleTreeBadges.ts`；R3:`system/menu/logic.ts`；R3b:`api/system/menu.ts`；R6:`system/api/`、`system/menu/` |

每阶段/每模块固定验证链：

```
node scripts/guards.mjs（0 违规）
→ cd yzh.vue.core && npm run typecheck && npm run build
→ cd cert/cert-admin && npm run build（含 vue-tsc）
→ 对应页面手工回归
```

---

## 8. 风险登记

| # | 风险 | 等级 | 缓解 |
|---|------|------|------|
| R-1 | menu 后端仅自定义 `/tree /tree/all`，非 TreeTable 约定端点 | 高 | D-1 拍板：升级 `TreeTableControllerBase`（推荐）vs 内核覆写（备选） |
| R-2 | 样板路径变更后 AI 仍照旧路径抄（AGENTS/指南不同步=返工） | 高 | P0 预告标注 + P8 同批收口，两处必须同改 |
| R-3 | core 新层误引入宿主依赖/硬编码地址 | 中 | R10 + R11 + PAGE_ROOTS 覆盖 |
| R-4 | 壳层迁移破坏登录/菜单全链路 | 中 | Wave S 独立成波，全链路回归通过才动 system 页面 |
| R-5 | menu 页对宿主 store `refreshMenus()` 耦合 | 中 | P1.3 事件解耦，P4 消化 |
| R-6 | api 页无 EntityConfig（配置驱动打折） | 中 | D-2：后端补 `/config` 或登记手写 columns 例外 |
| R-7 | 状态双源（core 单例 vs 宿主 pinia 不同步） | 中 | 沿用已验证薄适配模式（store/menu.ts 现状即此） |
| R-8 | 宿主忘调 `configureYzhApi` | 低 | 缺省相对路径仍被 proxy/同源承接，dev/prod 默认正确；文档契约 + 首请求告警（可选） |
| R-9 | auditor/enterprise 未同步接入 | 低 | 本批不做；列后续跟进 |

---

## 9. 明确不做 / 不回潮清单

- core 永不出现：main.ts / index.html / dev server / 硬编码后端地址 / `@/` / `@share/` / pinia store / 业务页面（foundation/workflow）
- 宿主永保留：启动权、createRouter、品牌、业务路由、per-path 覆盖权

---

## 10. 文档同步清单

| 文档 | 改动 | 阶段 |
|------|------|------|
| `docs/50-迁移计划/yzh.vue.core系统底座化迁移计划-V1.md` | ★ 本文落盘 | P0 |
| `docs/10-YZH架构/03-前端架构.md` | 定位升级：组件库→系统底座包（五原子）；界限表 | P0 |
| `docs/10-YZH架构/06-代码结构规范.md` | System 域三层同构映射特例 | P0 |
| `docs/30-项目规则/前后端代码结构统一规则-V1.md` | 域映射表 System 行 | P0 |
| `AGENTS.md` + 知识库副本 | 预告标注 → P8 正式切换样板路径 | P0 / P8 |
| `docs/10-YZH架构/样板页面指南-V1.md` | 样板路径切换 + §六清单收敛 | P8 |
| `项目全局规则.md` §6.1 | 目录树（core 增五原子目录） | P8 |
| `docs/10-YZH架构/README.md`、`07-开发流程.md`、`12-框架能力清单-V1.md` | 路径/看板漂移修正 | P8 |
| `scripts/guards.mjs` | roots 扩展 + R10/R11 + debt 摘除 | P0 起持续 |

---

## 11. 决策点（按推荐值执行，执行至对应阶段前可改）

| # | 决策 | 推荐值 | 拍板时点 | 状态 |
|---|------|--------|----------|------|
| D-1 | menu 整改后端方案 | 升级 `TreeTableControllerBase` + TreeConfig | P4 前 | 按推荐执行 |
| D-2 | api 页列配置 | 后端补 `/config`（工期紧：手写 columns+登记例外） | P4 前 | 按推荐执行 |
| D-3 | 默认首页形态 | 欢迎 + 菜单快捷入口（无业务图表） | P2 前 | 按推荐执行 |
| D-4 | Wave S 与 Wave A 先后 | 壳先（P2→P3），可对调 | P2 前 | 按推荐执行 |
| D-5 | P7 后端 Config/SysLog 下沉 | 做 | P7 前 | 按推荐执行 |
| D-6 | auditor 本批改造 | 不做，列后续跟进示范宿主 | — | 按推荐执行 |

## 12. 后续跟进（不阻塞本计划）

**cert-auditor 示范宿主接入**：手写 login/register/overview 保留 + 引用 core `YzhAppLayout`（menuTag='auditor'）与按需 `yzhSystemRoutes` + `configureYzhApi`；删除 `AuditorLogin.vue`(420)+`AuditorLayout.vue`(153) 重复——完成「一套底座、多端复用」首次闭环验证。
