# 文档提取（office → Markdown → 字段/表格）全链路测试与修复记录 V1

> **日期**：2026-09-17 | **范围**：`/business/doc-extraction-rule`（文档提取规则） + `/business/directory-manager`（标准文件管理）
> **触发**：用户反馈「目录管理页没有 AI 分析按钮」「文档提取规则页『开始分析』结果是空的」，并提出新规则：**office 先转 Markdown，再统一从 Markdown 抽取字段与表格**
> **结论**：定位 3 个真实缺陷（2 已修 + 1 布局），全链路在 doc / docx / xls 三类文档上实测通过

---

## 一、根因与修复

### 1.1 【已修·致命】响应 DTO 序列化 PascalCase，前端按 camelCase 读 → 页面恒为空

**现象**：`开始分析` 后弹「分析完成」，但「字段定义（0）」「表格定义（0）」，界面空白。

**根因**：新后端默认 PascalCase 序列化，而前端读取 camelCase。

实测原始响应（修复前）：

```json
{ "code": 200, "data": { "Fields": [ { "Name": "总经理", "NameEn": "zong_jing_li",
  "ExtractedValue": "尚月永", "IsRequired": true } ], "Tables": [...], "Message": "AI分析完成" },
  "message": "AI分析完成" }
```

前端（`doc-extraction-rule/index.vue`）：

```ts
const data = res?.data
fields.value = data?.fields || []   // ← data.fields 为 undefined → 恒为 []
tables.value = data?.tables || []
```

即 **AI 已正确分析出 5 字段 / 3 表格，但全部被前端丢弃**（`data.message` 也取不到，所以只走了「否则提示分析完成」分支，掩盖了问题）。

**修复**：`CertPlatform.Shared/DocExtraction/DocExtractionDtos.cs` 中**所有响应 DTO 补 `[JsonPropertyName]` camelCase**：

| DTO | 用途 |
|---|---|
| `FieldDefDto` / `TableDefDto` / `TableColumnDto` | 字段与表格定义（analyze、规则回读、save 回显） |
| `AIAnalyzeResponse` | analyze 响应（fields / tables / message） |
| `VerifyPromptResponse` / `ExtractionData` | verify 响应（success / message / data.fields / data.tables） |
| `RuleDetailResponse` | 规则回读（status / prompt / fields / tables / isValid …） |
| `AIConfigDto` / `SkillInfo` | AI 配置、技能列表 |
| `TestFieldResponse` / `TestTableResponse` | 工作流 docField / docTable 节点配置期试运行 |

> 请求 DTO 未标注：MVC 模型绑定大小写不敏感，改动无必要。
> ⚠️ 不能改成「全局 camelCase」：框架通用端点（`filter` 等）前端是按 `Items`/`TotalCount` PascalCase 读的，全局改动会打断这些页面。业务响应 DTO 逐类标注是风险最小的做法（与 D-5 契约、`AiNodeExecutor`、`PromptTemplateDto` 一致）。

**验证**：`data` 的键变为 `["fields","tables","message"]`，页面「字段定义（9）」正常渲染（中文名 + snake_case 英文名 + 类型 + 提取值）。

### 1.2 【已修】目录管理页「看不到 AI 分析按钮」——操作列被挤出可视区

**澄清**：按钮一直存在且与历史项目一致（历史 `DirectoryManager/index.vue` L237 同为文件行的 `重命名/替换/下载/AI 分析/删除`，文件夹行只有 `重命名/删除`）。**问题是看不见**：

实测（836px 窗口，左导航 240 + 左侧树 280）：

```
.file-list-container  clientWidth = 272   scrollWidth = 619
.action-cell.x = 885  →  超出容器右边界约 300px
固定列宽合计 40+100+120+180+300 = 740px  >  272px
```

即：列宽合计远超面板宽度 → `名称` 被压到 105px（中文竖排换行），`修改时间/操作` 列跑到可视区之外；macOS 横向滚动条默认隐藏，用户「看不到操作列 = 没有 AI 分析按钮」。

**修复**（`pages/workflow/directory/index.vue`）：

1. **操作列吸附右侧**：`.action-cell { position: sticky; right: 0 }` + 表头 `th:last-child` 同步 sticky，行 hover/选中态同步底色，用 `box-shadow` 画分隔线（sticky 与 `border-collapse` 共用会丢 border）——**对齐历史项目 el-table `fixed="right"` 语义**，横向滚动时按钮始终可达。
2. `.file-table { min-width: 760px }`：低于此宽度表体横向滚动，不再把列压扁。
3. `.file-list-container { overflow: auto }`（显式横向 + 纵向）。
4. `.name-text` 单行省略（`ellipsis` + `max-width: 340px`），替代原「flex 单元格 + 自由换行」。
5. 左侧树宽 `280px` → `clamp(180px, 20vw, 280px)`，窄屏让出空间。

**验证**：`AI 分析` 按钮 `withinVisible = true`；按钮集合恢复为 `重命名 / 替换 / 下载 / AI 分析 / 删除`。

> 仍需注意：文件夹行没有 AI 分析（与历史一致）；仅含文件夹的阶段看不到该按钮。

### 1.2.1 【已修·致命】目录页 AI 分析弹窗「验证成功但没有任何字段/表格」——verify 输出映射只认 V1 格式

**现象**：在 `/business/directory-manager` 文件行点「AI 分析」→ 从下拉选模板（如「Word文档结构分析」）→ 点「开始分析」→ 提示成功，但字段/表格一片空白（用户反馈「当前这个文件肯定是存在表格和字段信息的」）。

**根因**：AI 输出映射函数与提示词格式不匹配，**两个映射器支持范围不一致**：

| 映射器 | 使用场景 | 支持的 AI 输出格式 |
|---|---|---|
| `MapAiFieldsToDtos` / `MapAiTablesToDtos` | analyze（自动分析） | **V1 + V2**（`usesV2` 判定、双键回退） |
| `MapOutputsToExtractionData` | **verify（弹窗/校验）** | **仅 V1**（`field_code`/`field_value`、`table_code`/`rows`） |

而 `wf_prompt_template` 里的分析模板（`analyze_word`、`extract_all` …）输出的是 **V2 格式**：

```json
{ "fields": [ { "field_name_cn": "文件编号", "field_name_en": "wen_jian_bian_hao", "extracted_value": "XASL-QM" } ],
  "tables": [ { "table_name_cn": "审批记录表", "table_name_en": "shen_pi_ji_lu_biao",
                "columns": [ … ], "extracted_data": [ { "姓名": "尚月永", "角色": "编制" } ] } ] }
```

→ `MapOutputsToExtractionData` 找不到 `field_code`/`table_code` → 映射结果 `{fields:{}, tables:{}}` → 弹窗「验证成功」+ 空白。
（用「自动分析」页签 → `generate-prompt` 生成的是 **V1 格式**提示词，所以那条路径一直是好的，掩盖了此问题。）

**修复**（`DocExtractionRuleService.AI.cs`）：

1. `MapOutputsToExtractionData` 支持 V2：
   - 字段键回退链：`field_code` → `field_name_en` → `field_name` → `field_name_cn`；值回退链：`field_value` → `value` → `extracted_value`
   - 表格键回退链：`table_code` → `table_name_en` → `table_name` → `table_name_cn`；行数组：`rows` → `extracted_data`
2. **V2 语义对齐 analyze**：丢弃「未提取到实际值」的字段（新增 `HasJsonValue()`：null / undefined / 空白串 / 空数组 → 视作无值），表体无行数的表格同样丢弃，避免弹窗里出现一排只有标签没有值的空条目。
3. **不再静默返回空结果**：映射后 fields + tables 均为空时，返回 `success=false` + 明确提示（「AI 未提取到任何字段或表格：请确认提示词为…格式…」），而不是「验证成功 + 空白」。

**验证**（真实 qwen 调用，选「Word文档结构分析」模板作为提示词）：

| 文档 | 修复前 | 修复后 |
|---|---|---|
| `XASL-QM 质量手册.doc` | 0 字段 / 0 表格 | **5 字段 / 4 表格**：`wen_jian_bian_hao=XASL-QM`、`ban_ben_hao=A/0`、`sheng_xiao_ri_qi=2026-02-05`；表键 `shen_pi_ji_lu_biao` / `zhi_neng_fen_pei_biao` / `bu_shi_yong_tiao_kuan_biao`，行如 `{姓名: 尚月永, 日期: 2026-02-05, 角色: 编制}` |
| `XASL-TR-001 年度验证计划.doc` | 0 字段 | **3 字段**：`bian_zhi_ri_qi=2024-01-15`、`shen_he_ri_qi=2024-01-16`、`pi_zhun_ri_qi=2024-01-17` |
| 目录页 UI（同一文件 + 模板） | 「验证成功」+ 空白 | 「验证成功」+ 提取字段列表正常渲染 |

> 备注：`.txt`（如测试用 `hello.txt`）走 markdown 转换会被 anydoc 归为「不支持的文件类型」（`ClassifyError` 将 Unsupported 归类为该文案），AI 分析会直接报此错——属预期（分析对象应为 office/pdf 类文档）。

### 1.2.2 【已修】前端 prop 三态类型不匹配：`Expected Boolean, got Null`

**现象**（控制台告警）：

```
select.vue:407 Invalid prop: type check failed for prop "isValid". Expected Boolean, got Null
  at <PromptVerifyTab> …
```

**根因**：页面 `isValid = ref<boolean | null>(null)` 是**三态**（null=尚未验证 / true=通过 / false=失败），组件模板也依赖三态（`v-if="isValid !== null"`），但 `PromptVerifyTab` 把 prop 声明成了 `isValid: boolean`：`<script setup>` 类型式声明下非可选 prop 默认 `required: true`，而 Vue 只在 `value == null && !required` 时跳过校验 → 传 `null` 必然告警。
页面之前用 `:is-valid="isValid as any"` 绕过 TS 报错，但这只骗过了类型检查，运行时告警依然存在。

**修复**：把 prop 声明为三态并在页面去掉 `as any` 掩盖：

```ts
// PromptVerifyTab.vue
const props = defineProps<{ …; isValid: boolean | null; … }>()
```
```diff
- <PromptVerifyTab :is-valid="isValid as any" … />
+ <PromptVerifyTab :is-valid="isValid" … />
```

> Vue 3.5 的运行时类型推断支持 `null`（`[Boolean, null]`，`getType(null) === 'null'`），`null` 不再触发校验失败。

**验证**：组件实际渲染（「Prompt 生成与验证」页签、`isValid === null`、验证结果区不展示），控制台**零告警**；`vue-tsc` 对该文件 0 错误。

### 1.2.3 【已修】POST 端点 `[FromQuery]` 参数被放在 body → 恒定 400（「取消队列」从未生效）

**现象**：目录页控制台批量 `400 Bad Request`。

**根因**：后端两个 POST 端点的参数来自**查询串**且非空必填，而前端把参数放进了 body。`[ApiController]` 的模型校验对必填 query 缺失会直接返回 400 validation error（控制器内部根本不执行，所以后端日志里看不到业务报错）。

实测两种传参方式：

| 调用方式 | 结果 |
|---|---|
| `POST convert/progress` + body `{TaskId}` | **400** `{"errors":{"taskId":["The taskId field is required."]}}` |
| `POST convert/progress?taskId=…` | 200 `{taskId,total,completed,failed,processing,pending,cancelled,isFinished}` |
| `POST convert/cancel` + body `{QueueCode}` | **400**（同上） |
| `POST convert/cancel?queueCode=…` | 200 `{code:400,msg:"队列不存在"}`（业务失败，HTTP 仍 200） |

→ 影响：目录页「取消队列」按钮和转换进度查询**一直打不通**（用户侧表现为按钮无效 + 控制台报错）。

**修复**（`cert-share/composables/useDirectoryApi.ts`）：

```ts
export async function getConvertProgress(taskId: string) {
  const res = await yzhApi.post(… '/convert/progress', undefined, { params: { taskId } })
  return res.data!
}
// cancelConvert 同改为 query 传参，并改为返回 BizResult
export async function cancelConvert(queueCode: string): Promise<BizResult> { … }
```

同时修正页面 `cancelActiveQueue` 的假成功：该端点 **HTTP 恒 200、成败在 body.code**，原实现不看 code 就直接提示「已取消队列」；现改为判断 `code === 200`，失败时提示后端 `msg`（如「队列不存在」）。

**同类问题排查**：扫描全部 POST + `[FromQuery]` 端点（`copy` / `activate` / `delete` / `toggle-active` / `template/delete` / `section/delete` / `deleteFolder` / `deleteTemplateFile` / `renameTemplateFile` 等），前端均已在 URL 上拼查询串，**仅上述两个不一致**。

**验证**：直接调用前端真实封装（动态 import 模块）——`getConvertProgress('dummy')` 返回进度对象（200）、`cancelConvert('dummy')` 返回 `{code:400, msg:'队列不存在'}`；页面清空控制台后观察 2 个 5s 轮询周期，**零报错**。

### 1.3 【数据侧】`IsValid = 0` 的文件无法被 analyze 命中

`GetFileInfoAsync` → `GetOneAsync<StandardDirectoryFile>(x => x.FileCode == ...)`，而 `StandardDirectoryFile` 声明了 `IsValid` → ORM 自动追加 `IsValid = 1`。

- `cert_standard_directory_file` 共 1001 行：**717 行 IsValid=1 / 284 行 IsValid=0**（同一文件在不同阶段重复登记，仅部分有效）。
- 选中 `IsValid = 0` 的文件时 analyze 返回：`{ fields: [], tables: [], message: "未找到可分析的文件：请确认该标准已上传模板文件（文件要求），或该文件已上传到标准目录" }`。

**这是数据/语义问题，不是代码缺陷**（树只列出 IsValid=1 的文件，正常路径不会命中）。已记录，若要支持历史/失效文件分析，应显式放宽该查询或补 `includeDisabled` 语义。

---

## 二、新规则链路确认（office → Markdown → 字段/表格）

代码路径（`DocExtractionRuleService.AI.cs`）：

```
GetFileInfoAsync(fileCode)                // FR-xxx 模板优先 → FL-xxx 实际文件兜底
  → Markdown 产物命中则读 MinIO（markdown_status=completed）
  → 否则取 MinIO 源文件 → DocumentConvertClient.ConvertToMarkdownAsync()   // anydoc，容器 yzh-anydoc
  → BuildAnalysisPromptAsync(skill)       // wf_prompt_template: analyze_{word|excel|pdf}
  → LlmInvokeService.CompleteAsync(ForceJson) // OpenAI 兼容直连（qwen-turbo）
  → MapAiFieldsToDtos / MapAiTablesToDtos
```

- 转换容器 `yzh-anydoc`（anydoc v0.2.3，node:22-slim），**14 种格式 → GFM Markdown**，毫秒级（实测 `XASL-QM 质量手册.doc` 21,354 字节 Markdown / **460ms**）。
- 中间产物链保留 LibreOffice（`doc→docx`、`xls→xlsx`、`→PDF` 预览），PDF 用于 `file-preview`；**字段/表格提取统一只吃 Markdown**（即本次确认的新规则）。
- 提示词来源：`wf_prompt_template.analyze_word / analyze_excel / analyze_pdf`（7 条存量模板，5 条启用），已能正常读取（见 `PromptTemplateController` 补齐记录）。

---

## 三、全链路实测结果（真实 HTTP + 真实 anydoc + 真实 qwen）

### 3.1 analyze（按文档类型）

| 文档 | 类型 | 耗时 | 字段 | 表格 | 样例 |
|---|---|---|---|---|---|
| `XASL-QM 质量手册.doc`（STAGE01） | doc | 12.3s | **5** | **3** | 总经理/`zong_jing_li`=尚月永；表：质量管理体系职能分配表(6列/3行)、修订历史(3列/1行) |
| `《注塑工艺卡-真空拔罐器》.docx` | docx | 15.6s | **15** | **2** | 企业名称=`河北雄安尚龙医疗科技有限公司`；表：工序表(4列/3行)、注塑成型工艺参数表(7列/4行) |
| `XASL-OR-004 员工培训档案.xls` | xls | 4.4s | 0 | **1** | 员工培训记录(7列/3行) |

> xls 字段数为 0 **符合设计**：`analyze_excel` 提示词明确规定「Excel 内容全部来自表格，fields 默认空数组，表头/单元格不得作为独立字段」，故只输出 tables。

### 3.2 链路后半段（`XASL-QM 质量手册.doc`）

| 步骤 | 结果 |
|---|---|
| analyze | 200，6 字段 / 2 表格，9.2s |
| generate-prompt | 200，生成 2,048 字符 Prompt（含字段清单表 + 输出 JSON 规范），21ms |
| verify | 200，`success: true`，`message: 验证成功`，**提取出 6 字段 / 2 表格真实值**（`zong_jing_li=尚月永`、`qi_ye_ming_cheng=河北雄安尚龙医疗科技有限公司`），33.7s（上下文 37,394 字符） |
| 规则回读 `GET /DocExtractionRule/{fileCode}` | 200，camelCase 键 `[id, code, standardFileCode, orgCode, standardCode, phaseCode, skill, prompt, isValid, status, fields, tables, createDate, modifyDate]`，`status=configured`，5 字段 / 4 表格 |
| 落库 | `cert_doc_extraction_rule.sample_data` = 7,249 字节，含 `ban_ben_hao: A/0`、`zong_jing_li: 尚月永`、`sheng_xiao_ri_qi: 2026.02.05` 等；`DocIsValid=1`、`status=configured` |
| AI 用量 | `cert_ai_usage_log` 记录 analyze/verify 每次调用：provider=qianwen、model=qwen-turbo、prompt/completion tokens、duration_ms、success |

### 3.3 编译与类型检查

| 项 | 结果 |
|---|---|
| `dotnet build YZH.Core.sln` | **0 error** |
| `vue-tsc --noEmit`（`directory/`、`ConfigTab.vue`） | 0 新增错误 |
| 数据副作用 | 未新增规则行；测试中 `verify` 更新了既有规则的 `sample_data/UpdateTime`（该规则本就存在，`doc_content` 缓存复用）；调试用 Prompt 模板临时记录已物理删除、`is_active` 已复原 |

---

## 四、遗留与建议

| # | 事项 | 说明 |
|---|---|---|
| 0 | **V2 表格行键为中文列名** | V2 的 `extracted_data` 行以「中文列名」为键（来自模型输出）。弹窗按原键展示无影响；若工作流 `docTable` 节点要按**列编码（英文）**取单元格，后续需在映射时用 `columns[].column_name_en` 做一次键归一 |
| 1 | **284 个 `IsValid = 0` 文件** | 同文件多阶段重复登记，仅部分有效；树不显示它们（正常），但若产品需要「分析历史/失效文件」，需放宽 `GetFileInfoAsync` 的 ORM 过滤或明确忽略 |
| 2 | **PDF 无测试样本** | `cert_standard_directory_file` 无 `.pdf` 文件，`analyze_pdf` 提示词未实测；anydoc 对**扫描版 PDF（无文本层）返回 Unsupported**，需走 MinerU OCR 分支（未内置 OCR） |
| 3 | **analyze 不缓存 Markdown** | verify 会把转换结果写入 `rule.doc_content` 复用；analyze 每次重新转换（anydoc 毫秒级，影响小），建议 analyze 也回写缓存以减少重复转换 |
| 4 | **LLM 结果非确定性** | `temperature = 0.7`，同一文件多次 analyze 字段数会波动（同文件实测 3 / 5 / 6 / 9 字段）。若需稳定评测，建议把 analyze 场景降到 0.1~0.3 |
| 5 | **AI 调用耗时较长** | analyze 4~15s、verify 34s（上下文 3.7 万字符），前端需保留 loading 与超时提示（现有实现已有 loading，未设超时） |
| 6 | **`IsValid` 标准列语义** | `cert_standard_directory_file` 新增的框架标准列 `IsValid` 与旧 `status/enable` 语义并存，易踩「ORM 静默过滤」坑（本次即为典型案例），建议在实体上注释说明或在迁移脚本中统一回填 |
