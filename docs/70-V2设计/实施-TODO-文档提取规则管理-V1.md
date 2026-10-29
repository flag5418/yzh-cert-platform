# 实施 TODO：文档提取规则管理（简化版）

> **版本**：V1 ｜ **创建**：2026-10-28 ｜ **状态**：未开始
> **权威设计**：[28-文档建模与执行引擎设计-V3.md](./28-文档建模与执行引擎设计-V3.md)
> **范围**：只做三件事——①标准 Markdown 入库 ②提示词管理 UI ③规则测试显示 JSON
> **严格串行**，每阶段须用户确认再推下一阶段。

---

## 0. 开工前基线

- [ ] 0.1 `git status` 干净
- [ ] 0.2 `dotnet build CertPlatform.sln` **0 Error**
- [ ] 0.3 `cert_iso_standard` 有 4 条标准（iso9001/14001/45001/13485）

**已有素材：**

| 文件 | 大小 | 行数 | 质量 |
|---|---|---|---|
| `temp/gbt_extract/gbt19001_ocr3.txt` | 62KB | 1383 | ✅ OCR 已校对 |
| `temp/gbt_extract/gbt24001.txt` | 85KB | 1297 | ✅ 原生文本 |
| `temp/gbt_extract/gbt45001.txt` | 122KB | 1899 | ✅ 原生文本 |
| `temp/gbt_extract/gbt42061_ocr3.txt` | 79KB | 1904 | ✅ OCR 已校对 |

---

## Phase A：标准 Markdown 清洗 + 入库

> **目标**：4 份国标原文清洗为 Markdown 文件，存入 `cert_doc_extract_rules`。

- [ ] **A.1** 新建表 `cert_doc_extract_rules`
  - 写 `scripts/db/20261028_doc_extract_rules_V1.sql`
  - 执行 SQL → 验 `SHOW CREATE TABLE cert_doc_extract_rules`

- [ ] **A.2** 清洗 `gbt19001_ocr3.txt` → `temp/standards/iso9001-2016.md`
  - 去除 `===PAGE N===`
  - OCR 残损处标 `[待校对]`
  - 验证：章节 1–10 完整，关键条款号存在

- [ ] **A.3** 清洗 `gbt24001.txt` → `temp/standards/iso14001-2016.md`
  - 同上

- [ ] **A.4** 清洗 `gbt45001.txt` → `temp/standards/iso45001-2020.md`
  - 同上

- [ ] **A.5** 清洗 `gbt42061_ocr3.txt` → `temp/standards/iso13485-2016.md`
  - 同上

- [ ] **A.6** 入库 4 行数据
  - 写 `scripts/db/20261028_doc_extract_rules_seed_V1.sql`
  - StandardCode = cert_iso_standard.Code GUID（查出来填）
  - MarkdownContent = 清洗后的 MD 全文
  - PromptContent 暂为空（Phase C/D 填充）
  - 幂等：`INSERT ... ON DUPLICATE KEY UPDATE`

- [ ] **A.7** 验证
  - `SELECT COUNT(*) FROM cert_doc_extract_rules` = **4**
  - 4 条 MarkdownContent 均非空且长度 > 10000
  - 用户肉眼抽样审阅

**Phase A 验收**：用户确认 4 份 Markdown 质量可接受 → 签准进入 Phase B

---

## Phase B：提示词管理 UI

> **目标**：新建`/business/doc-extract-rule`页面，左树右编辑。

- [ ] **B.1** 菜单注册
  - 在 MENU_00302（规则定义）下新增"文档提取规则"
  - 路由：`/business/doc-extract-rule`

- [ ] **B.2** 左侧标准树
  - 数据源：`cert_iso_standard` 列表
  - 点击节点 → 右侧加载该标准的规则

- [ ] **B.3** 右侧编辑器
  - PromptContent 编辑区（textarea 或 Monaco Editor）
  - MarkdownContent 只读折叠面板
  - [保存] [保存并发布] 按钮

- [ ] **B.4** 后端 CRUD API
  - `DocExtractRuleController : YzhControllerBase`
  - `POST /filter` 列表查询
  - `POST /update` 保存
  - `POST /test` 测试规则

- [ ] **B.5** 实体 + DTO + Service
  - `DocExtractRule.cs` 实体
  - `DocExtractRuleDto.ts` 前端契约
  - `DocExtractRuleService.cs` 业务逻辑

**Phase B 验收**：用户确认 UI 交互顺畅 → 签准进入 Phase C

---

## Phase C：ISO 9001 专属提示词 + 测试

> **目标**：以 ISO 9001 为试点，完成提示词编写与测试闭环。

- [ ] **C.1** 编写 ISO 9001 专属 PromptContent
  - 从 ISO 9001 视角定义每类文档的专属提取字段
  - 参考该国标 Markdown（driver）
  - 写入数据库（更新 cert_doc_extract_rules）

- [ ] **C.2** 实现 /test API
  - 接收上传文档
  - 调现有 AI Vision 转 Markdown
  - 拼接 PromptContent + document_content
  - 调 LLM → 返回 JSON

- [ ] **C.3** 前端测试区
  - 上传文档按钮
  - 运行测试按钮
  - 结果显示 JSON（格式高亮）

- [ ] **C.4** 反复调整 + 测试
  - 上传 3–5 份不同类型文档
  - 检查 JSON 结构 + 字段完整性 + 缺标注

**Phase C 验收**：用户确认 ISO 9001 产出满足要求 → 签准进入 Phase D

---

## Phase D：其他 3 个标准的提示词

- [ ] **D.1** ISO 14001 专属 PromptContent 编写 + 测试
- [ ] **D.2** ISO 45001 专属 PromptContent 编写 + 测试
- [ ] **D.3** ISO 13485 专属 PromptContent 编写 + 测试
- [ ] **D.4** 每标准至少 1 份测试文档
- [ ] **D.5** 发布全部 4 个标准（IsPublished = 1）
- [ ] **D.6** 前端守卫 + 后端编译验证

**Phase D 验收**：4 个标准全部发布 → 本 TODO 完成 → 进入下一模块（认证准备/检查设计）

---

## 不在 TODO 范围（明确排除）

- ❌ cert_doc_extract 结果存储表
- ❌ 条款与文档的匹配逻辑
- ❌ 认证准备阶段消费
- ❌ 认证检查阶段 + 阶段管理
- ❌ 提取引擎的供给+推理分离

---

*严格串行：A → B → C → D。每阶段完成后由用户确认再推下一阶段。*
