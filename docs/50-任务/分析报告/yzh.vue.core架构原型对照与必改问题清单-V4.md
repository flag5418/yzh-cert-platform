# yzh.vue.core 架构原型对照与必改问题清单 V4

> 版本：V4.0 ｜ 日期：2026-09-24 ｜ 状态：**待你审阅**
> 触发（你的原话）：
> 「让你查看 `YZH架构/YZH.WPF.Core` 就明白为什么我这样去设计了……**当前的问题，是我们的前端架构文档没有跟上我们的代码**，其实最初设计 yzh.vue.core 的核心，就是从当前的描述中产生的，只是我分析代码，发现该代码并不是如我的预想这样设计，所以才花费了很多精力进行完善。**有些函数或方法没有被当前项目使用，并不是需要将这些方法或函数彻底清理的核心依据，是要评估这些方法在未来的前端逻辑中是否可以使用**，根据这些进行进一步的评估，**给出必须要完善和解决的问题建议清单**」
>
> **本版的关键转向**：V2/V3 我用"**当前是否被引用**"作为死活判据 —— **这个判据是错的**。V4 改用你确立的判据：**"是否属于 `YZH.{技术栈}.Core` 这一层的能力"**。判据一换，结论大面积反转。

---

## 〇、先认错：我用错了判据

| | V2/V3 的判据（错误） | V4 的判据（正确） |
|---|---|---|
| 提问 | "当前项目有没有调用它？" | "**它是不是 `.Core` 这一层该有的能力？**" |
| 零引用 → | 🗑 删除 | **保留为能力储备**（框架层的本分就是"备而不用"） |
| 后果 | 会把框架层的能力储备当垃圾清掉，**破坏架构的完整性** | 保留能力，只清"重复实现"与"过时遗留" |

**你原话就是判据**：*"有些函数或方法没有被当前项目使用，并不是需要将这些方法或函数彻底清理的核心依据，是要评估这些方法在未来的前端逻辑中是否可以使用。"*

---

## 一、`YZH.WPF.Core` 揭示的设计原型

### 1.1 这不是一个项目，是一整个架构家族

`YZH架构/` 下的实测清单：

| 项目 | 技术栈 | 内部分层 |
|---|---|---|
| **`YZH.WPF.Core`** | WPF | `BaseView` / `Control` / `Converter` / `Extensions` / `Helper` / `Models` / `View` / `ViewModel` / `Static` |
| **`YZH.Avalonia.Lib`** | Avalonia | `BaseView` / `Control` / `Converter` / `Extensions` / `Helper` / `View` / `ViewModel` / `Assest` / **`DOCS`** |
| `YZH.Blazor.Core` | Blazor | `Components` / `Extensions` / `Helpers` / `Models` |
| `YZH.Windows.Core` | WinForms | `Enums` / `Extensions` / `Helper` / `Lib` / `Model` / `View` |
| `YZH.Web.Core` | ASP.NET | `Controllers` / `Helpers` / `Middleware` / `Configuration` |
| `YZH.Api.Core` | Web API | `Aops` / `Extensions` / `CorsMiddleware` |
| `YZH.Stand` | 跨技术共享 | `Enums` / `Extension` / `Helper` / `Libs` / `Mappings` / `Models` / `Validator` |
| `YZH.DataBase` | 数据访问 | `DbOrm` / `Enum` / `Helper` / `Interface` / `Models` / `NoSql` |
| **`yzh.vue.core`** | **Vue** | `components` / `logic` / `composables` / `utils` / `adapters` / `types` / `api` / `router` / `pages` / `layouts` |

> **结论**：`yzh.vue.core` **就是这个家族在 Vue 技术栈上的成员**。命名规律是 `YZH.{技术栈}.Core`。
> 所以"为什么 core 里要有机构-人员"这个问题的答案很简单：**因为 `YZH.WPF.Core` 里就有**。

### 1.2 `YZH.WPF.Core` 的实测分层（规模）

| 层 | 文件 | 行数 | 职责 |
|---|---|---|---|
| `BaseView/` | 10 | 1,851 | **基础窗体**：`FrmBaseEdit`(编辑) / `FrmBaseMain`(主列表) / `FrmInput`(输入) / `FrmSelect`(选择) / `FrmTree`(树) |
| `Control/` | 11 | 640 | **原子控件**：`ButtonEdit` / `DefineColumn` / `FrmBaseWin` / `FrmMessage` / `FrmWait` / `TreeMenuControl` |
| `Converter/` | 6 | 127 | **值转换器**：Bool→Visibility / InverseBool / Null→Visibility / Numeric→Bool / SafeSelection / Svg→Image |
| `Extensions/` | 5 | 661 | **扩展方法**：`BaseEntity` / `Button` / `DataGrid` / `HttpRequestData` / `SelectHelper` |
| `Helper/` | 8 | 1,307 | **工具类**：`AccessHelper` / `HttpHelper` / `LiteDB_NoSql` / `PasswordBoxBinding` / `PdfToImgHelper` / `ThemeManager` / `WorkHelper`(Word) / `XamlHelper` |
| `Models/` | 1 | 199 | **契约**：`DefinitionNode` |
| `View/` | 22 | 2,337 | **界面**：`FrmLogin` / `FrmChangePass` / `FrmLoginByKey` / `FrmMenuRole` / `FrmSelectTest` / **`work_flow/`(6 窗体)** |
| `ViewModel/` | 17 | 2,824 | **逻辑**：`Base{Edit,Main,Win}Model` + `Menu/` + **`User/`** + **`WorkFlow/`** |
| `Static/` | 1 | 7 | 常量：`HttpRequestMethod` |

### 1.3 ★ 决定性证据：机构-人员 / 工作流**本来就在 Core 里**

```
ViewModel/User/DepartAndUserModel.cs     ← 部门 + 人员   ★
ViewModel/User/MenuRoleModel.cs          ← 菜单 + 角色
ViewModel/User/RoleAndUserModel.cs       ← 角色 + 人员   ★
ViewModel/Menu/TreeMenuControlModel.cs
ViewModel/WorkFlow/  (4 个文件)          ← 工作流逻辑
View/work_flow/      (6 个窗体)          ← 工作流界面
```

**"部门-人员"「角色-人员」正是你原话说的"机构-人员、角色-人员"** —— 它们在 WPF 时代就住在 `.Core` 里。
→ ✅ **你的设计一以贯之。`yzh.vue.core/pages/system/{organization,role-user,role-menu}` 是对 WPF 原型的正确移植。**

### 1.4 `YZH.Stand` —— 跨技术共享层（前端缺这一整块）

| 目录 | 实测内容 |
|---|---|
| `Enums/` | `FillMode` / `HttpMethod` / `OrmOperatorEnum` / `RsaKeyType` / `ThumbnailCutMode` / `WatermarkPosition` |
| `Extension/` | 16 个：`ConfigItem` / `Enum` / `Exception` / `Generic` / `IConvertible` / `IDictionary` / `IEnumerable` / `Int` / `Long` / `Object` / `Random` / `Short` / `String` / `Byte` / `ValueTypeConvert` |
| `Helper/` | `AliFace`(人脸) / `Baidu` / `DataRow` / `Entity` / `Excel` / `Files` / `Mapper` / `Pdf` / `SSH` / `Security` / `Socket` / `Wechat` / `Word` / `http` |
| `Libs/` | Aspose.Words / PDFRender4NET / Spire.Pdf / Spire.XLS |
| `Models/` | `ApiResult` / `BaseEntity` / `ConfigItem` / **`DefineColumn`** / `Entities` / **`GridConfig`** / `HttpHeaders` / `HttpRequestData` / `ITree` / `JwtSettings` / **`PagerOpertion`** / `SocketModel` |
| `Validator/` | `ComplexPassword` / `IsEmail` / `IsIPAddress` / `IsPhone` / `MaxValue` / `MinValue` |

**★ `DefineColumn.cs:48` 是 `Yxk` 的原始定义**：

```csharp
/// <summary>
///     允许空标志
/// </summary>
public bool YXK { get; set; }
```

→ **`YXK` = 允许空**（拼音首字母），注释白纸黑字"允许空标志"。**你完全正确，我此前读反了。** 这条现已可封档。

**★ 契约对应表（后端 `YZH.Stand` ↔ 前端 core）**：

| `YZH.Stand/Models` | 前端 core |
|---|---|
| `GridConfig.cs`（ConfigName/TableName/FloorFlag/Columns/OrmCode） | `EntityConfigDto`（EntityConfig JSON） |
| `DefineColumn.cs`（含 `YXK`/`BCFlag`） | `ColumnConfigDto` |
| `PagerOpertion.cs` | `PageParams` / `PagedData` |
| `ApiResult.cs` | `ApiResponse` |
| `ITree.cs` | `TreeNode` |
| `BaseEntity.cs` | `BaseEntity` |
| `Enums/HttpMethod.cs` | — （前端用字符串） |

---

## 二、评判标准修正后，"死代码"清单**大面积反转**

### 2.1 三分类判据（新的）

| 类别 | 判据 | 处置 |
|---|---|---|
| **A. 能力储备** | WPF Core / `YZH.Stand` 里有对应的层或能力 | ✅ **保留**（框架层的本分） |
| **B. 重复实现** | 同一能力在 core 内有 ≥2 份实现 | 🗑 删旧留新 |
| **C. 过时遗留** | 已被替代 + 自带 `@deprecated` + 违反守卫 | 🗑 删除 |

### 2.2 逐项重新裁定（对比 V3 的结论）

| 项 | 行数 | V3 判定 | **V4 重新裁定** | 依据 |
|---|---|---|---|---|
| `utils/treeUtils.ts`（23 个树查询函数：buildTree/findNode/search/filterTree/validate/hasCycle…） | 584 | 🗑 删 | ✅ **保留（A）** | WPF 有 `Extensions/Extension.DataGrid`、`Helper/XamlHelper` 的树/控件操作；树工具是框架能力 |
| `utils/treeOps.ts`（7 个不可变树变换） | 483 | 🗑 删 | ✅ **保留（A）** | 同上；且 `validate` 对应 `YZH.Stand/Validator` |
| `components/layout/YzhTreeTableSelector.vue` | 417 | 🗑 删 | ✅ **保留（A）** | WPF 有 `BaseView/FrmSelect.xaml`（**选择窗体**）—— 完全对应的框架能力 |
| `components/ui/{YzhCard,YzhEmptyState,YzhStatusBadge}.vue` | 219 | 🗑 删 | ✅ **保留（A）** | WPF `Control/` 就是原子控件层；空状态/徽章是标准控件 |
| `logic/LinkTableCore.ts` + `useLinkTable` | 147 | 🗑 删 | ✅ **保留（A）** | WPF 有 `Extensions/Extension.SelectHelper`（选择器扩展） |
| `composables/useConfirm.ts` | 37 | 🗑 删 | ✅ **保留（A）** | WPF 有 `Control/FrmMessage.xaml`（**消息框**）—— 一一对应 |
| `composables/useAuth.ts` | 39 | 🗑 删 | 🗑 **删除（B）** | **与 `useAuthState.ts` 重复**（第二套 auth 实现）→ 会造出两个 token 真相源 |
| `composables/useTable.ts` | 49 | 🗑 删 | ⚠️ **待判（B?）** | 需确认是否被 `useCores.ts` 的 `useSingleTable/useTreeTable` 完全取代；若是 → 删 |
| `types/ApiResponse.ts` | 9 | 🗑 删 | 🗑 **删除（C）** | 已被 `types/contracts.ts` 的 `ApiResponse` 取代（`YZH.Stand/Models/ApiResult` 的对应物是 contracts 那份） |
| **`utils/http.ts`** | **102** | **（V3 漏检）** | 🗑 **删除（C）** | 头部自述 `@deprecated 将在 v2.0 移除`、`保留原因：历史业务页面仍引用此模块` —— **但实测 0 处引用**；且 `import axios` **违反 R4** |

**修订后的死代码量：约 200 行（原来我说的 1,984 行）→ 降到 ~10 倍以下。**

> **一句话**：**V3 说"通用层 13.4% 是死代码"是错的。真实情况是：绝大多数是框架能力储备，只有 3 项（`useAuth` / `types/ApiResponse` / `utils/http`）该删，另 1 项（`useTable`）待确认。**

### 2.3 但"重复实现"是真问题（保留能力 ≠ 容忍重复）

| 重复 | 实测 | 处置建议 |
|---|---|---|
| **树构建 3 处** | `utils/treeUtils.ts:47` `buildTree<T>()`（通用版）｜`api/system/menu.ts:64`（菜单专用）｜`pages/system/api/logic.ts:155`（接口页专用）｜`YzhTreeTableCheckSelector.vue:364` `rebuildTree()` | **保留通用版，把后 3 处逐步收敛到它**（不急于一次做完） |
| **契约重复定义** | `Page`/`PageParams` 在 `types/Page.ts` 与 `components/table/types.ts:100-113` 各一份，barrel 静默遮蔽其一 | 删一处 |
| **`api/system` 三兄弟同名函数** | `role-api` / `role-menu` / `role-user` 的 `checkAdd`/`getCheckTree`… 三份几乎相同 | 参数化（可延后） |

---

## 三、★ 必须完善和解决的问题清单（本报告核心交付）

> 按"**必须解决**"排序。每条含：问题 / 证据 / 为什么必须解决 / 建议做法 / 成本。
> **判据**：凡"不解决就会让架构腐化或让 AI 误判"的，进 P0。

### 🔴 P0-1｜前端架构文档缺失（**这是你自己指出的核心问题**）

| 项 | 内容 |
|---|---|
| **问题** | `yzh.vue.core` 的设计原则、分层判据、能力边界**全部只存在于你脑中**。`docs/` 全库搜"五原子"= **0 命中**；`docs/10-YZH架构/03-前端架构.md` 没有 core 的定位与准入判据 |
| **证据** | `grep -r "五原子" docs/` → 0；core 的 `package.json` description 是唯一的"文档"，且它自称"npm 包"（名实不符） |
| **为什么必须解决** | ① **AI 每轮都要重新猜**"什么该进 core"，猜错就返工（你已经历过）；② 你说"代码并不是如我的预想这样设计"—— 根源就是**没有可对照的文档**，只能靠人肉 diff；③ `YZH.Avalonia.Lib` 有 `DOCS` 目录，**前端这一块是空的** |
| **建议做法** | 新建 `docs/10-YZH架构/03-前端架构.md` 的 **《yzh.vue.core 定位与准入》** 一节，必须写进 5 件事：<br>① **家族定位**：`yzh.vue.core` = `YZH.{技术栈}.Core` 家族的 Vue 成员（附 WPF/Avalonia/Blazor 对照表）<br>② **分层映射**：WPF `BaseView/Control/Converter/Extensions/Helper/Models/View/ViewModel` ↔ vue `logic/components/adapters/utils/types/pages/composables`<br>③ **准入判据（两条）**：<br>　　a. 后端 `YZH.Core.Web`（框架层）有的 → core 建镜像；`certplatform-api`（业务层）有的 → 不进 core<br>　　b. **WPF/Avalonia 的 `.Core` 里有的层或能力 → vue core 应有对应物**<br>④ **能力储备原则（★ 关键，防止后人误删）**：**"当前未被引用"不是删除依据**；删除依据是"① 与 core 内其他实现重复 或 ② 已自带 `@deprecated` 且被替代"<br>⑤ **红线**：core 永不出现认证业务实体（`ISOStandard`/`CertStage`/`ent_enterprise`/…） |
| **成本** | ~1.5h |

### 🔴 P0-2｜能力缺口：`utils/` 工具箱与 `validators` 几乎为空

| 项 | 内容 |
|---|---|
| **问题** | WPF Core 的 `Helper/`（8 类）+ `YZH.Stand/Extension/`（16 类）+ `Validator/`（6 个），在 vue core 里**对应物几乎为零** |
| **证据** | vue core `utils/` 只有 7 个文件：`apiResponse` / `case` / `http` / `index` / `menu` / `treeOps` / `treeUtils`；`find -iname "*valid*"` = **0**；`find -ipath "*enum*"` = **0**；无 loading/wait 组件（WPF 有 `Control/FrmWait`） |
| **对照缺口** | 缺 **验证器**（WPF 侧在 `YZH.Stand/Validator`：邮箱/IP/手机/密码强度/最大最小）｜缺 **枚举层**｜缺 **字符串/日期/数字扩展**｜缺 **文件/Excel/PDF 工具**｜缺 **加解密**｜缺 **Loading 组件**（现直接用 element-plus） |
| **为什么必须解决** | 你说"要评估这些方法在**未来的前端逻辑**中是否可以使用" —— **反向也成立**：WPF 侧**已经在用**的框架能力（如验证器、导出、文件处理），前端**迟早要用**（建档/上传/提取/报告生成这条链上一定需要文件与 Excel/PDF 处理）。现在不建，未来会在业务层各写一份 |
| **建议做法** | 按需增补，**不追求一次补齐**。优先级：<br>① `utils/validator.ts`（邮箱/手机/IP/密码强度）—— 对应 `YZH.Stand/Validator`，**立即可用**<br>② `components/ui/YzhLoading.vue` —— 对应 `Control/FrmWait`<br>③ `utils/file.ts`（下载/导出）—— 与 `api/file-storage.ts` 配套<br>④ `utils/date.ts` / `utils/string.ts` —— 对应 `Extension.String/Int/Long`<br>⑤ Excel/PDF 导出 —— 对应 `Helper/Excel`、`Helper/Pdf`（**可在需要时再做**） |
| **成本** | ①~④ 各 30min，⑤ 视需要 |

### 🔴 P0-3｜"能力储备"没有标注 → 后人（含 AI）会误删

| 项 | 内容 |
|---|---|
| **问题** | `treeUtils` / `treeOps` / `YzhTreeTableSelector` / `components/ui/*` / `useConfirm` / `LinkTableCore` 这些**能力储备**，与**真正的死代码**在代码里长得一模一样（都是"零引用"）→ **无标注就会被误删**（我 V3 就误判了） |
| **证据** | V3 报告把 1,984 行判为"死代码应删除"；实际上其中 ~1,780 行是能力储备 |
| **为什么必须解决** | ① 这是**唯一能防止"能力储备被当垃圾清掉"的机制**；② 直接对应你说的"AI 代码后续个人很难维护" |
| **建议做法** | 给能力储备文件加**统一标记头**（与 `@deprecated` 对称）：<br>```ts<br>/**<br> * @reserve 框架能力储备（当前项目未引用，属 YZH.Core 能力清单）<br> * 对应：YZH.WPF.Core/BaseView/FrmSelect.xaml<br> * ⚠️ 删除前须确认"是否属于 .Core 能力层"——"零引用"不是删除依据<br> */<br>```<br>并加**守卫 R13**：`@reserve` 文件若被删除，或非 `@reserve`/`@deprecated` 的零引用导出出现 → 提示复核（而非直接失败） |
| **成本** | 标记 ~40min + 守卫 ~30min |

### 🟡 P1-1｜契约归属错位

| 项 | 内容 |
|---|---|
| **问题** | `YzhTableColumn` / `YzhAction` / `YzhFormField` 等**跨层契约**物理住在 `components/table/types.ts` / `components/form/YzhForm.vue` |
| **证据** | `logic/TreeTableCore.ts:24,25`、`logic/SingleTableCore.ts:33,40`、`adapters/entityAdapters.ts:12,17`、`composables/useTable.ts:2` 全部反向 `import type ... from '../components/...'`；**宿主 3 处 `@yzh-core/components/table/types`** |
| **为什么必须解决** | ① 契约层（`types/`）与组件层耦合，违反"契约应是最底层"；② 宿主被迫伸手进组件内部目录；③ 与后端 `YZH.Stand/Models/`（**契约独立成层**）的先例不一致 |
| **建议做法** | 搬到 `types/table.ts` + `types/form.ts`；`components/`、`logic/`、`adapters/`、`composables/` 全部改指 `types/`；删 `types/Page.ts`（与迁入的重复） |
| **成本** | ~1h（改动面大，需 `vue-tsc` 护航） |

### 🟡 P1-2｜守卫盲区：`utils/` 与 `logic/` 不在任何规则内

| 项 | 内容 |
|---|---|
| **问题** | R4（禁 axios）roots = `PAGE_ROOTS + API_ROOTS + components`；R10（禁反向依赖）roots = `pages/layouts/router/composables/api`。→ **`utils/`、`logic/`、`adapters/`、`types/` 是盲区** |
| **证据** | **`utils/http.ts:5` `import axios` 违反 R4，但 `guards.mjs` 报 0 违规** ← 盲区已被真实利用 |
| **为什么必须解决** | 盲区 = 规则失效；且这个盲区**已经放过了真实违规** |
| **建议做法** | ① R4 roots 补 `utils/`、`logic/`、`adapters/`、`types/`；② R10 roots 同上；③ 新增 R12（core 禁认证业务实体）—— 基线 0，符合"规则启用铁律" |
| **成本** | ~20min |

### 🟡 P1-3｜core 零测试

| 项 | 内容 |
|---|---|
| **问题** | `find yzh.vue.core -name '*.spec.ts'` = **0**；而它是三端共享底座 |
| **为什么必须解决** | ① 改 core = 三端盲改；② **能力储备没有测试 = 没人敢用**（这也是它们至今零引用的原因之一） |
| **建议做法** | 先建 3 个最小 spec：`logic/SingleTableCore`（dataLoader 装配）、`useAuthState`（set/clear）、`router/createYzhRoutes`（路由表结构）。**给能力储备补 spec 反而比补注释更有用**——测试即用法文档 |
| **成本** | ~2h（3 个 spec） |

### 🟡 P1-4｜工作流归属：在 `cert-share` 而非 core

| 项 | 内容 |
|---|---|
| **问题** | 工作流在 `cert/cert-share/src/{components,composables,api}/workflow/`（8 个 vue + 若干 ts） |
| **证据** | **WPF 先例：`YZH.WPF.Core/View/work_flow/`（6 窗体）+ `ViewModel/WorkFlow/`（4 文件）→ 工作流在 `.Core` 里** |
| **为什么必须解决** | ① 按你确立的判据，工作流属 `.Core` 能力层；② `cert-share` 是"业务共享层"，放这里会导致**未来新项目无法复用工作流**（违背"可在任意项目使用"） |
| **建议做法** | ⚠️ **这是一个需要你决策的归属问题，不是纯技术问题**。选项：<br>**A. 迁到 core**（符合 WPF 先例，但改动面大：`cert-share` 有 8 个 vue 依赖 logicflow 366KB）<br>**B. 保持现状，但在文档中说明"工作流为业务共享层能力，暂不入 core"**（成本 0）<br>→ **建议 B + 文档记录**，理由：工作流与具体业务（NC/报告规则）耦合较深，且 10/15 前不宜大动；**但必须先写清楚，否则未来会被反复质疑** |
| **成本** | A ~1d｜B ~15min |

### 🟡 P1-5｜`package.json` 名实不符 + 依赖声明缺失

| 项 | 内容 |
|---|---|
| **问题** | description 自称"**系统底座 npm 包**"，实为"源码目录 + vite alias"；`cert-admin`/`cert-auditor`/`cert-enterprise` 的 `package.json` **均未声明** `yzh.vue.core` 依赖（仅 `cert-share` 有 `file:`） |
| **为什么必须解决** | ① 名实不符会误导后续 AI 与协作者（**这本身就是技术债**）；② 幻影依赖让依赖图对工具不可见 |
| **建议做法** | ① description 改为"**YZH 前端框架层（`YZH.{技术栈}.Core` 家族 Vue 成员；源码共享，vite alias 消费）**"；② 三端补 `"yzh.vue.core": "file:../../yzh.vue.core"` |
| **成本** | ~15min |

### 🟢 P2（卫生项，不阻塞）

| ID | 问题 | 证据 | 建议 |
|---|---|---|---|
| P2-1 | `src/assets/README.md` 描述**不存在的目录** `assets/entityconfig/`，且写的加载路径 `/api/entityconfig/{name}` 与实现（`/api/{controllerName}/config`）不符 | `find assets -type f` → 只有 README.md | 改文档（或建目录） |
| P2-2 | core 的 `node_modules` 是**断链**（→ `src/server/…`，真实路径 `src/old/server/…`） | `ls -la node_modules` | 修复或删除软链 |
| P2-3 | core 的 `vite.config.ts` 有**失效 alias** `'@share': '../share/src'` | `vite.config.ts:10` | 删除（core 本不该引用 @share） |
| P2-4 | R1 debt 2 文件长期挂账（`YzhTree.vue`、`YzhTreeTableLayout.vue`） | `guards.mjs:127-131` | 排期修 |
| P2-5 | 导出面 ≈ 消费面 ×3（`index.ts` 用 `export *` 通配 7 个模块） | 宿主只消费 34 符号 / 12 子路径 | ⚠️ **但按新判据，"导出面大"不是缺点** —— 框架层本就该暴露能力。**不建议收窄** |

> ⚠️ **P2-5 是 V3 的另一个误判**：V3 把"导出面 ≈ 消费面 ×3"当成 B4 不达标。按 `.Core` 定位，**框架层暴露完整能力是正常的**（`YZH.Stand` 的 `Helper/` 也一样，很多项目只用其中几个）。**收回这条。**

---

## 四、WPF Core → vue core 的能力映射对照表（补文档的素材）

| WPF Core | vue core 对应 | 状态 |
|---|---|---|
| `BaseView/FrmBaseEdit` | `components/form/YzhFormDialog` + `logic/SingleTableCore` | ✅ |
| `BaseView/FrmBaseMain` | `components/table/YzhTable` + `logic/SingleTableCore` | ✅ |
| `BaseView/FrmInput` | `components/form/YzhForm` | ✅ |
| `BaseView/FrmSelect` | `components/layout/YzhTreeTableSelector` | ⚠️ 有（能力储备） |
| `BaseView/FrmTree` | `components/layout/YzhTree` + `logic/TreeTableCore` | ✅ |
| `Control/ButtonEdit` | — | ❓ 缺 |
| `Control/DefineColumn` | `adapters/entityAdapters` | ✅ |
| `Control/FrmBaseWin` | `components/layout/YzhDialog` | ✅ |
| `Control/FrmMessage` | `composables/useConfirm` | ⚠️ 有（能力储备） |
| `Control/FrmWait` | — | ❌ **缺（建议补 `YzhLoading.vue`）** |
| `Control/TreeMenuControl` | `components/layout/YzhTree` | ✅ |
| `Converter/`（6 个） | `utils/case.ts` + `adapters/` | ⚠️ 部分 |
| `Extensions/Extension.BaseEntity` | `adapters/entityAdapters` | ✅ |
| `Extensions/Extension.Button` | `adapters/entityAdapters.toToolbarActions` | ✅ |
| `Extensions/Extension.DataGrid` | `adapters/entityAdapters.toTableColumns` | ✅ |
| `Extensions/Extension.HttpRequestData` | — | ❓ 缺（表名信息注入） |
| `Extensions/Extension.SelectHelper` | `logic/AssociationTreeCore` + `LinkTableCore` | ✅ |
| `Helper/AccessHelper` | — | ❓ 缺（权限判断工具） |
| `Helper/HttpHelper` | `api/client.ts` | ✅ |
| `Helper/LiteDB_NoSql` | — | ❌ 缺（前端本地存储） |
| `Helper/PdfToImgHelper` | — | ❌ 缺 |
| `Helper/ThemeManager` | 品牌 props（部分） | ⚠️ 部分 |
| `Helper/WorkHelper`(Word) | — | ❌ 缺 |
| `Models/DefinitionNode` | `types/tree.ts` | ✅ |
| `View/FrmLogin` | `pages/auth/Login.vue` | ✅ |
| `View/FrmChangePass` | `YzhAppLayout` 内嵌（非独立页） | ⚠️ 差异 |
| `View/FrmMenuRole` | `pages/system/role-menu` | ✅ |
| `View/work_flow/*`(6) | `cert-share/components/workflow/*` | ⚠️ **在 share 不在 core** |
| `ViewModel/Base*Model` | `logic/*Core` | ✅ |
| `ViewModel/User/DepartAndUserModel` | `pages/system/organization` | ✅ |
| `ViewModel/User/MenuRoleModel` | `pages/system/role-menu` | ✅ |
| `ViewModel/User/RoleAndUserModel` | `pages/system/role-user` | ✅ |
| `ViewModel/WorkFlow/*`(4) | `cert-share/composables/workflow/*` | ⚠️ 在 share |
| `YZH.Stand/Validator/*`(6) | — | ❌ **缺（建议补）** |
| `YZH.Stand/Extension/*`(16) | `utils/case.ts`（仅 1 类） | ❌ **缺（建议补 string/date/number）** |
| `YZH.Stand/Enums/*`(6) | — | ❌ 缺 |
| `YZH.Stand/Models/*` | `types/*` | ✅ |

**统计**：✅ 对应 16 项 ｜ ⚠️ 差异/在别处 9 项 ｜ ❌ **缺失 9 项**

---

## 五、建议的推进顺序

| 序 | 事项 | 成本 | 理由 |
|---|---|---|---|
| 1 | **P0-1 写文档**（含"能力储备不是死代码"原则） | 1.5h | **没有它，其余全部会被反复推翻**；且直接解决你说的"文档没跟上代码" |
| 2 | **P0-3 给能力储备加 `@reserve` 标记** | 1h | 防止后续（含 AI）误删；与文档互为保险 |
| 3 | **P1-2 补守卫盲区 + R12** | 20min | 基线 0，立刻可用；盲区已放过真实违规 |
| 4 | **P0-2 补验证器 + Loading**（缺口里最急的两项） | 1h | 关键路径（建档/上传/提取/报告）一定会用到 |
| 5 | **P1-5 命名纠正 + 补依赖声明** | 15min | 消除误导 |
| 6 | **P1-1 契约归位** | 1h | 一次性消灭 4 条反向 import |
| 7 | **P1-3 core 三个最小 spec** | 2h | 让能力储备"有用法、敢使用" |
| 8 | **P1-4 工作流归属：先写文档定调** | 15min | 决策问题，成本 0 的 B 方案 |
| 9 | **P2 卫生项** | 30min | 不阻塞 |

**总成本约 8 小时**，其中前 5 项（约 4h）是"**必须完善**"，后 4 项是"**建议解决**"。

---

## 六、附：本版取证命令

```bash
# 1. 架构家族
ls "/Volumes/Expand/wangqingquan/Documents/work/work/YZH架构/"

# 2. WPF Core 分层与规模
cd "/Volumes/Expand/wangqingquan/Documents/work/work/YZH架构/YZH.WPF.Core"
for d in BaseView Control Converter Extensions Helper Models View ViewModel Static; do
  printf "%-12s %s 文件 %s 行\n" "$d" \
    "$(find $d -type f \( -name '*.cs' -o -name '*.xaml' \) | wc -l)" \
    "$(find $d -type f \( -name '*.cs' -o -name '*.xaml' \) -exec cat {} + | wc -l)"
done

# 3. ★ 机构-人员/工作流在 Core 里的证据
ls ViewModel/User/          # → DepartAndUserModel.cs / MenuRoleModel.cs / RoleAndUserModel.cs
ls ViewModel/WorkFlow/ View/work_flow/

# 4. ★ Yxk 的原始定义
grep -n -B2 "public bool YXK" "/Volumes/Expand/wangqingquan/Documents/work/work/YZH架构/YZH.Stand/Models/DefineColumn.cs"
# → ///     允许空标志

# 5. 前端能力缺口
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/certplatform-web/yzh.vue.core/src
ls utils/                    # → 仅 7 个文件
find . -iname "*valid*"      # → 空
find . -ipath "*enum*"       # → 空

# 6. ★ 守卫盲区证据（utils/http.ts 违反 R4 但未报）
grep -n "from 'axios'" utils/http.ts          # → :5
head -3 utils/http.ts                          # → @deprecated 将在 v2.0 移除
grep -rn "utils/http\|from './http'" . | wc -l # → 0（注释说"历史业务页面仍引用"，实测零引用）
cd ../../ && node scripts/guards.mjs           # → 0 违规（证明盲区）
```

---

> **最后一句**：看过 `YZH.WPF.Core` 之后，结论要改口 —— **你的架构设计是一以贯之的，`yzh.vue.core` 只是 `YZH.{技术栈}.Core` 家族在 Vue 上的成员**。真正的问题不是"代码写错了"，而是：
> ① **文档没跟上**（导致代码与预想产生偏差，只能靠人肉 diff 发现）；
> ② **能力储备没有标记**（导致"零引用"被误读为"死代码"—— 我 V3 就误判了 1,780 行）；
> ③ **部分能力层还是空的**（验证器 / 扩展 / Loading / 本地存储 / 文件处理 —— WPF 侧都有）；
> ④ **少数真正的重复与过时**（`useAuth` / `types/ApiResponse` / `utils/http` —— 约 200 行）。
>
> **先补文档与标记，再补能力，最后清重复。** 顺序反了就会重演"清掉能力储备"的错误。
