import io

P = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/.workbuddy-ai/memory/MEMORY.md"
s = io.open(P, encoding="utf-8").read()
orig = len(s.encode("utf-8"))

R = [
("""- **★ C 组剩 4 条**：**C5** 数据区域（**已有来源链编辑器** ⇒ 补齐非新建）｜**★ C6 = 唯一「设计走样」**（`AnchorSidePanel.vue:525` 手选 `WriteMode`，与 `autoOp` 矛盾 ⇒ **唯一立即做项**）｜**C10** 三视图（依赖 B7/B8）｜**C11** 需先定口径""",
 """- **★ C 组剩 4 条**：**C5** 数据区域（补齐非新建）｜**★ C6 = 唯一「设计走样」**（`AnchorSidePanel.vue:525` 手选 `WriteMode`，与 `autoOp` 矛盾 ⇒ **唯一立即做项**）｜**C10** 三视图｜**C11** 需先定口径"""),

("""- **★ B7/B8/B9（降级 TODO；`59` 裁定 ⛔ 不提前）**：试填预览 = **`S2` 的 `FillOneAsync` 只读入口**（⛔ 不另造填充链 —— 否则两套入口 = 新重复）｜段 = `_preview/` + 固定 key""",
 """- **★ B7/B8/B9（降级 TODO）**：试填预览 = **`S2` 的 `FillOneAsync` 只读入口**（⛔ 不另造填充链）｜段 = `_preview/`"""),

("""- **★ `59` 复核（12 断言 11 确证）**：`SourceResolver` **只支持 `global`/`manual`**（`ai` 归 Skill 层）｜`FixedDocSubtype` **已闭合**｜**★ P3 扩四类 ⛔ 不在 `SourceResolver` 做**（随 `S1` 下沉 `Shared/Services/Fill/`）""",
 """- **★ `59` 复核**：`SourceResolver` **只支持 `global`/`manual`**（`ai` 归 Skill 层）｜`FixedDocSubtype` **已闭合**｜**★ P3 扩四类 ⛔ 不在 `SourceResolver` 做**（随 `S1` 下沉）"""),

("""- **★ `58` §八 待裁 3 条（均不阻塞 S1）**：**C1** `PromptWorkbenchService` 拆点（建议**按「有没有 `HttpContext`」拆**）｜**C2** `DocFillPrompt` 实体是否上移 `Shared`（建议**是**）｜**C3** `Shared/{IServices,Repositories,WorkflowEngine,Data}` 四个预留目录（建议**本次不用**）""",
 """- **★ `58` §八 待裁 3 条（不阻塞 S1）**：**C1** `PromptWorkbenchService` 拆点（按「有没有 `HttpContext`」）｜**C2** `DocFillPrompt` 上移 `Shared`（建议**是**）｜**C3** 四个预留目录（建议**本次不用**）"""),

("""｜**Q3** 锁定锚点换版搬运（建议**不搬**）｜**Q4** 语义失败建契约行留痕（建议**不建**）｜**A3/Q-N3** 纵向切片优先（建议**是**）""",
 """｜**Q3** 锁定锚点换版搬运（**不搬**）｜**Q4** 语义失败建契约行留痕（**不建**）｜**A3/Q-N3** 纵向切片优先（**是**）"""),

("""- **★ 遗留**：PDF 预览仍是模拟｜「阶段」层让树膨胀｜队列/日志/企业资料管理三菜单仍是占位｜Task #71/#72｜E2 单测｜E5 菜单 URL 同步｜E6 原型自检｜`55`/`54` 的 8 条裁定落纸（`56` §三）""",
 """- **★ 遗留**：PDF 预览仍是模拟｜「阶段」层让树膨胀｜三菜单仍是占位｜Task #71/#72｜E2 单测｜E5 菜单 URL 同步｜E6 原型自检｜`55`/`54` 的 8 条裁定落纸"""),

("""- **★★★ 作用域裁定（10-04 用户逐字）**：**后台只配「规则」**（⛔ **无 `EnterpriseCode`**）；**企业由专家端任务注入** —— 运行期作用域唯一载体 = **`cert_expert_task`**（`OrgCode`+`EnterpriseCode`+`StageCode`+`StandardCodes[]`+`TaskType`）""",
 """- **★★★ 作用域裁定（10-04 用户逐字）**：**后台只配「规则」**（⛔ 无 `EnterpriseCode`）；**企业由专家端任务注入** —— 运行期作用域唯一载体 = **`cert_expert_task`**（`OrgCode`+`EnterpriseCode`+`StageCode`+`StandardCodes[]`+`TaskType`）"""),

("""｜**锚点 = 4 组属性**（形态 / 写入 / 值类型+格式 / **取值来源链**）｜⚠️ **「AI 节点」指两件事**：`{{ai:xxx}}`（⛔ 零 LLM）≠ `SourceKind='ai'`""",
 """｜**锚点 = 4 组属性**（形态 / 写入 / 值类型+格式 / **取值来源链**）｜⚠️ **「AI 节点」指两件事**：`{{ai:xxx}}`（⛔ 零 LLM）≠ `SourceKind='ai'`"""),
]

n = 0
miss = []
for old, new in R:
    if old in s:
        if old != new:
            s = s.replace(old, new, 1); n += 1
    else:
        miss.append(old[:50])

io.open(P, "w", encoding="utf-8").write(s)
now = len(s.encode("utf-8"))
print("hit:", n, "/", len(R), "miss:", miss)
print("size:", orig, "->", now, "saved", orig - now)
