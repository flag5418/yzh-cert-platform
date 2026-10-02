# 提示词工作台 —— 下一步 TODO（AI 手交清单）

> 最后更新：2026-10-02 14:40
> 本轮已完成：后端服务 + 前端页面 + 守卫/类型检查全绿；**尚未在浏览器实测**

## 一、本轮已完成（可复用，勿重复）

### 后端
- `PromptWorkbenchService.cs`：三层回退定位 + AI 生成草稿（`prompt_generator` 元提示词驱动）+ 上传试跑（转 Markdown → 调 LLM → 即弃）
- `PromptTemplateController.cs`：新增 7 个端点
  - `GET /api/PromptTemplate/workbench/resolve` — 回退定位
  - `GET /api/PromptTemplate/workbench/list` — 列出某类型+标准下的全部提示词
  - `POST /api/PromptTemplate/workbench/generate` — AI 生成草稿（强类型 DTO，camelCase）
  - `POST /api/PromptTemplate/workbench/test` — 上传文件试跑（multipart，200MB）
  - `GET /api/PromptTemplate/workbench/standards` — 标准下拉
  - `POST /api/PromptTemplate/workbench/save` — 幂等 upsert（版本+1 并置生效）
  - `POST /api/PromptTemplate/workbench/delete` — 逻辑禁用
- `PromptTemplate.json` EntityConfig：补 `StandardCode`/`ModelName`/`MaxTokens`/`Temperature` + 模型下拉 Options
- SQL 脚本：`scripts/db/20261002_prompt_workbench_V1.sql`（已执行）—— 补 4 列 + 索引 + 3 条提示词种子 + `prompt_type`/`skill_target` 字典
- 编译：**0 Error**

### 前端
- API 层：`cert/cert-share/src/api/workflow/prompt-workbench.ts`（完整类型 + 显式 PascalCase 请求体映射）
- 页面：`cert/cert-admin/src/pages/workflow/prompt-template/index.vue`（三栏：列表 / 编辑器 / 试跑）
- 组件：`PromptTestPanel.vue`（上传 → 试跑 → 结果展示：转换日志 / 解析 JSON / 原始输出 / 实际提示词）
- 守卫：**通过**；`vue-tsc --noEmit`：**0 错误**

### 提示词种子（已入库）
| PromptCode | Type | Model | Tokens | Temp |
|---|---|---|---|---|
| prompt_generator | 元提示词 | qwen-plus | 8192 | 0.30 |
| doc_group_iso9001 | 分类 | qwen-flash | 8192 | 0.10 |
| doc_content_iso9001 | 作用 | qwen-plus | 8192 | 0.20 |

---

## 二、下一步（按优先级）

### P0 — 实测与调通
1. **重启后端** → 浏览器打开 `http://localhost:9990/business/prompt-template`
   - 后端启动脚本：`scripts/backend/restart-backend.sh`
   - 前端启动脚本：⛔ **不要从沙箱内启动**；让用户在自己终端跑 `scripts/frontend/start.sh`
2. **实测 AI 生成**（分类提示词 + 作用提示词）：
   - 选标准「ISO 9001」→ 类型「分类提示词」→ 点「AI 生成草稿」→ 确认质量
   - 同样测「作用提示词」
   - 如有明显偏差，在对话框里补充「额外要求」后重新生成
3. **实测上传试跑**：
   - 分类提示词：上传 2~3 份不同文件（Word/PDF）→ 看 JSON 返回的类别是否正确
   - 作用提示词：上传 1 份文件 → 看 `purpose`/`clauses`/`keyPoints` 是否合理
   - 注意：文件即弃，不会留痕
4. **实测保存/生效切换**：保存一条新提示词 → 刷新 → 确认列表出现 → 点「设为生效」→ 确认同类型其他提示词变为未生效

### P1 — 种子质量优化（用户裁决）
5. **调整 `prompt_generator` 元提示词**：
   - 若生成的分类/作用提示词质量不佳，编辑 `prompt_generator` 的 Template，加约束或示例
   - 改完后重新 AI 生成，再保存到 `doc_group_xxx` / `doc_content_xxx`
6. **补充其他标准**：目前只有 ISO 9001；若有其他标准（如 ISO 14001/45001），按同样模式建提示词

### P2 — 系统参数与安全（用户裁决）
7. **是否提高 `ai_max_tokens`**：当前 `cert_sys_config.ai_max_tokens = 4096`
   - 建议：提到 **8192**（分类提示词结果通常 2~4K）或 **16384**（作用提示词 JSON 较长时）
   - 修改位置：`cert_sys_config` 表 `ai_max_tokens` 值，或通过系统配置页修改
8. **API Key 安全提醒**：`ai_api_key` 明文存 DB，且曾被打印进会话上下文。建议：
   - 轮换 key（在阿里云/灵积控制台重置）
   - 后端日志脱敏（检查 `LlmInvokeService` / `PromptWorkbenchService` 是否打印了 key）

### P3 — 专家任务流水线（历史遗留）
9. **注册 6 个 `doc_*` Skill 到 `wf_skill`**（与文档提取规则配套）：
   - 先核 `wf_skill_reflection` 的 10 条 active 记录
   - 注册：analyze_word / analyze_excel / analyze_pdf / nc_judge / nc_conclude / doc_group / doc_content
10. **合并并行会话 28/29/31/32 号文档**：用户此前查出 6 处缺陷，需裁决是否合并修正

### P4 — 记忆纠正（技术债）
11. **MEMORY.md 过时结论**：当前 `MEMORY.md` 写「⚠️ 下拉选项必须前端注入（YzhForm 不读 dictCode）」—— 这是**错误的**。2026-09-26 框架改动后 `YzhForm` 已完整读取 `dictCode` → `GET /api/System/Dictionary/items/by-no/{dicNo}`。请在下一次同步时修正。

---

## 三、关键命令速查

```bash
# 重启后端（必须）
cd /Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台
./scripts/backend/restart-backend.sh

# 前端类型检查
cd src/certplatform-web/cert/cert-admin
npx vue-tsc --noEmit

# 前端守卫
cd src/certplatform-web
node scripts/guards.mjs

# 后端编译
cd src/yzh-core/YZH.Core.Web
/Volumes/Expand/wangqingquan/.dotnet/dotnet build YZH.Core.Web.csproj -v q

# 查提示词表
docker exec yzh-mysql mysql -uroot -pYzh123456. --default-character-set=utf8mb4 -N -B yzh_cert_platform -e "
SELECT PromptCode, PromptType, StandardCode, ModelName, MaxTokens, Temperature, IsActive
FROM wf_prompt_template WHERE IsDeleted=0;"
```

---

## 四、已知限制 / 风险

1. **提示词工作台页面尚未在浏览器实测**——虽然守卫和类型检查全绿，但交互流程（上传 multipart、AI 生成对话框、保存后刷新列表）需人工验证。
2. **文件即弃的「弃」依赖 `DocumentConvertClient` 的 `finally` 清理**——若后端进程崩溃或 OOM，临时目录可能残留；已确认该组件内部有 `finally { Directory.Delete(sessionDir, true) }`。
3. **`qwen-long` 不支持结构化输出**——不能用于 `doc_content`（试跑时 `ForceJson=true`），但下拉里列了它；如用户选它会导致 JSON 解析失败。当前种子已避开它（用 qwen-flash/qwen-plus）。
4. **分类提示词输出可能超限**——当文件清单超过 ~15 份、每份开头片段 300 字符时，提示词正文本身可能接近 4096 token；若同时 `MaxTokens=4096`，输出会被截断。建议把 `ai_max_tokens` 提到 8192。
