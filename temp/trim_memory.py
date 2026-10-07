import io

P = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/.workbuddy-ai/memory/MEMORY.md"
s = io.open(P, encoding="utf-8").read()
orig = len(s.encode("utf-8"))

R = []

# --- 删除整条 ---
R.append(("""- **★★ 中栏预览两条链**：有 `fileCode` → `.../DocExtractionRule/file-preview`｜只有 `storagePath` → `.../preview-by-path`｜缓存 = `PathBuilder.Product(src,pdf,'.pdf')`（**无 DB 列**）⇒ **换版必须显式删缓存**
""", ""))

R.append(("""- 后台填写规则页 `cert/cert-admin/src/pages/workflow/doc-fill-rule/`（`index.vue` + `logic.ts`（`DocFillRuleLogic extends TreeTableLogic<any>`）+ `components/` 18 文件）｜API `cert-share/src/api/workflow/doc-fill-rule.ts`｜路由 `router/index.ts:27`｜菜单 `MENU_00218` → `/business/doc-fill-rule`
""", ""))

R.append(("""- **★ 高优**：**造真数据走一遍主链**（含 D-1~D-4 前置 = `60` M1 / **G0 数据门**）
""", ""))

R.append(("""- ⚠️ **模块所有已交付功能都没被真数据走过** ⇒ 判定正确性**无法被证伪**
""", ""))

# --- 行内压缩 ---
R.append(("（漏 ⇒ 幽灵文件夹）", "（漏 ⇒ 幽灵文件夹）"))

R.append(("前端 `useFileTree.ts` 与后端 `PathBuilder.ReservedSegments` **必须逐字一致**",
          "前后端 `useFileTree.ts`/`ReservedSegments` **必须逐字一致**"))

R.append(("｜`json` 列空数组发 `'[]'`，空串 ⇒ **整条 INSERT 失败**｜**忽略策略** `AnalyzePolicy` = **「用途级」非「文件级」**",
          "｜`json` 空数组发 `'[]'`，空串 ⇒ **整条 INSERT 失败**｜`AnalyzePolicy` = **「用途级」非「文件级」**"))

R.append(("｜**MinIO 新库必须**进 `DocumentLibraryPath.Prefixes`（否则**静默全失败且 200**）｜⚠️ DB 的 `StoragePath` **带前导 `/`**，`Normalize()` **trim 掉** ⇒ 口径须统一",
          "｜**MinIO 新库必须**进 `DocumentLibraryPath.Prefixes`（否则**静默全失败且 200**）｜⚠️ DB `StoragePath` **带前导 `/`**，`Normalize()` **trim 掉**"))

R.append(("｜㊶ **含已删查重走 `Client.Queryable<T>()`**；复活**同 `Code` 走 UPDATE、⛔ 不 INSERT**",
          "｜㊶ **含已删查重走 `Client.Queryable<T>()`**；复活**同 `Code` 走 UPDATE**"))

R.append(("③ 响应恒 **PascalCase** / `keys` 是 **GET** / `success=false` 时消息在 **`err`**",
          "③ 响应恒 **PascalCase** / `keys` 是 **GET** / `success=false` 看 **`err`**"))

R.append(("**用户说「需要认真评估」时，第一步是验证他的前提是否成立** —— ① grep 列名看有无执行代码 ② DB 实测有无数据 ③ 再看文档",
          "**用户说「需要认真评估」时，第一步是验证他的前提是否成立** —— ① grep 看有无执行代码 ② DB 实测有无数据 ③ 再看文档"))

R.append(("｜**Vue 插槽是惰性的**（桩组件不透出插槽 ⇒ 断言全空转）｜`vi.mock` 必须 `async (importOriginal)` + `...actual`｜**手搓 mock = 在测试里重写一遍生产逻辑** ⇒ 用真实类 + `vi.spyOn`",
          "｜**Vue 插槽是惰性的**（桩组件不透出 ⇒ 断言空转）｜`vi.mock` 必须 `async (importOriginal)` + `...actual`｜**手搓 mock = 重写一遍生产逻辑** ⇒ 用真实类 + `vi.spyOn`"))

R.append(("｜**R12** 读快照（菜单入库必跑）｜**R13** ⛔ 禁裸 `if (!res.success)`｜",
          "｜**R12** 读快照｜**R13** ⛔ 禁裸 `if (!res.success)`｜"))

R.append(("｜**ApiCode** = `SHA256(\"{方法}|{路由末段}|{动作}\")` → 改控制器/动作名 ⇒ 授权**静默断裂**｜**★ 前端 `controllerName` 须含 `Admin/`** ⇒ **漏段 = 静默 404**",
          "｜**ApiCode** = `SHA256(\"{方法}|{路由末段}|{动作}\")` → 改控制器/动作名 ⇒ 授权**静默断裂**｜**★ 前端 `controllerName` 须含 `Admin/`** ⇒ 漏段 = **静默 404**"))

R.append(("｜样板 = `iso-standard/logic.ts` + `system/role/index.vue` 的 `YzhTreeTableLayout`｜",
          "｜样板 = `iso-standard/logic.ts` + `system/role/index.vue`｜"))

R.append(("｜**★★ `:deep()` 压不过共享组件 scoped 样式** ⇒ 胜负由 **CSS 注入顺序**决定 ⇒ **页面侧多写一层类名**",
          "｜**★★ `:deep()` 压不过共享组件 scoped 样式** ⇒ 胜负由 **CSS 注入顺序**定 ⇒ **页面侧多写一层类名**"))

R.append(("｜**★ 左树右表内核**：`TreeTableLogic` 覆写点 + 6 钩子，样板 `report-rule/`",
          "｜**★ 左树右表内核**：`TreeTableLogic` + 6 钩子，样板 `report-rule/`"))

R.append(("｜**★ 缺口常是「双层」的** ⇒ 补实体属性**不够**，须同时补 **DTO + Detail + 白名单校验**",
          "｜**★ 缺口常是「双层」的** ⇒ 补实体属性**不够**，须同补 **DTO + Detail + 白名单校验**"))

R.append(("（否则前端漏传 = **静默解锁**）⇒ **配置走 `save-batch`、锁定走 `lock`**",
          "（否则漏传 = **静默解锁**）⇒ **配置走 `save-batch`、锁定走 `lock`**"))

R.append(("｜**两坑**：快照**必须含已软删行**、判定用**落库集合**",
          "｜**两坑**：快照**含已软删行**、判定用**落库集合**"))

R.append(("｜**`AnchorType` 与 `WriteMode` 是死字段** ⇒ **「替换整句」能力根本不存在**｜",
          "｜**`AnchorType` 与 `WriteMode` 是死字段** ⇒ **「替换整句」不存在**｜"))

R.append(("｜**★ 判据**：依赖 `IDbOrm`/`IObjectStorage`（**均 Scoped**）⇒ 走 ②｜★ **`TaskType` 抽 `const` 共用**（不一致 = 任务静默分发不到）",
          "｜**★ 判据**：依赖 `IDbOrm`/`IObjectStorage`（**均 Scoped**）⇒ 走 ②｜★ **`TaskType` 抽 `const` 共用**（不一致 = 分发不到）"))

R.append(("｜**删模板级联软删锚点；复活 ⛔ 不回滚锚点**｜**`IDbOrm.InsertAsync` ⛔ 不生成 `Code`**｜**⛔ 4 控制器全裸奔 ⇒ 发布前必补权限**",
          "｜**删模板级联软删锚点；复活 ⛔ 不回滚锚点**｜**`IDbOrm.InsertAsync` ⛔ 不生成 `Code`**｜**⛔ 4 控制器裸奔 ⇒ 发布前补权限**"))

R.append(("｜**★ 注释不一致 = 结构错误** ⇒ **运行期静默不生效**",
          "｜**★ 注释不一致 = 结构错误** ⇒ 运行期**静默不生效**"))

R.append(("｜**已上线 7**（`Fill/Ai/` 的 3 个 ⇒ `ClassPath` **多一段 `.Ai`**）｜★ **`wf_skill.Code` ≠ `SkillCode`**（用 `Code` 查 **返 0 行且不报错**）",
          "｜**已上线 7**（`Fill/Ai/` 的 3 个 ⇒ `ClassPath` **多一段 `.Ai`**）｜★ **`wf_skill.Code` ≠ `SkillCode`**（用 `Code` 查 **返 0 行不报错**）"))

R.append(("｜**★ `SOURCE_KINDS` 真值 7 类 vs 原型 5 类** ⇒ **D3 待裁**",
          "｜**★ `SOURCE_KINDS` 真值 7 类 vs 原型 5 类** ⇒ D3 待裁"))

R.append(("｜**★★★ 写入侧已算行列号但没回写 vs 扫描侧不算** ⇒ 修法 = 扫描侧照抄（⛔ **不复用函数**）",
          "｜**★★★ 写入侧已算行列号但没回写 vs 扫描侧不算** ⇒ 扫描侧照抄（⛔ **不复用函数**）"))

R.append(("｜**`EnterpriseDocProfile` 实体在 `Auditor/Entities/Doc`",
          "｜**`EnterpriseDocProfile` 实体在 `Auditor/Entities/Doc`"))

n_ok = 0
miss = []
for old, new in R:
    if old in s:
        if old != new:
            s = s.replace(old, new, 1)
            n_ok += 1
    else:
        miss.append(old[:60])

io.open(P, "w", encoding="utf-8").write(s)
now = len(s.encode("utf-8"))
print("hit:", n_ok, "/", len(R))
print("miss:", miss)
print("size:", orig, "->", now, "saved", orig - now)
