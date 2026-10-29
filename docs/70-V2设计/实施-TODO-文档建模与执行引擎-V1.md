# 实施 TODO：文档建模与执行引擎（Phase A → D）

> ⚠️ **2026-10-28 口径变更，动工前必读**：权威设计已从 28 号 V3 改为 [28 号 V4](./28-文档建模与执行引擎设计-V4.md)。
> - V4 的范围是**规则侧**（文档要求 + 三套提示词资产），本 TODO 的 Phase A（标准 Markdown 入库）仍有效，可先做；
> - Phase B–D 里涉及「企业 200 份文件批量提取 / 产出企业 JSON」的部分属**企业侧执行**，不在本轮，须等企业侧设计；
> - `cert_doc_extract_knowledge` 不再作为企业侧结果表；V4 明确不新建 `cert_doc_extract*`，企业侧落点另定。
>
> **版本**：V1 ｜ **创建**：2026-10-28 ｜ **状态**：未开始（范围待按 V4 重排）
> **权威设计**：[28-文档建模与执行引擎设计-V3.md](./28-文档建模与执行引擎设计-V3.md)
> **用法**：**完成一项就把 `[ ]` 改成 `[x]`**，并在行尾补 `→ 证据`。每 Phase 完成后须用户确认再推下一 Phase。

---

## 0. 开工前基线

- [ ] 0.1 `git status` 干净（除本轮预期改动）
- [ ] 0.2 `dotnet build CertPlatform.sln` **0 Error**（⚠️ 仓库根）
- [ ] 0.3 `cert_iso_clause` 273 条 + `cert_clause_attr_def` 8 条 + `cert_clause_attr` 存在
- [ ] 0.4 MySQL 中确认 `cert_doc_extract_knowledge` 不存在（未开始）

**已知素材：**

| 文件 | 大小 | 行数 | 说明 |
|---|---|---|---|
| `temp/gbt_extract/gbt19001_ocr3.txt` | 62KB | 1383 | ✅ OCR 已校对文本 |
| `temp/gbt_extract/gbt24001.txt` | 85KB | 1297 | ✅ 原生文本 |
| `temp/gbt_extract/gbt45001.txt` | 122KB | 1899 | ✅ 原生文本 |
| `temp/gbt_extract/gbt42061_ocr3.txt` | 79KB | 1904 | ✅ OCR 已校对文本 |

**数据库基线（2026-10-28）：**

| 项 | 值 |
|---|---|
| `cert_iso_standard` 标准数 | 5 行（iso9001/14001/45001/13485 等） |
| `cert_iso_clause` 条款数 | 273 行（含 208 条正文） |
| `cert_clause_attr_def` 属性种子 | 8 行 |
| `cert_doc_extract_knowledge` | **不存在**（本 TODO 新建） |

---

## Phase A：生成 4 份标准 Markdown 文件 → 入库

> **目标**：将 4 份国标原文清洗为标准化 Markdown 文件，存入 `cert_doc_extract_knowledge`。

- [ ] **A.1** 新建表 `cert_doc_extract_knowledge`
  - 写 `scripts/db/20261028_doc_extract_knowledge_V1.sql`（建表 DDL，含唯一索引）
  - 执行 SQL → 验 `SHOW CREATE TABLE cert_doc_extract_knowledge` 字段齐全
  - 幂等：全部 `IF NOT EXISTS`

- [ ] **A.2** 清洗 `gbt19001_ocr3.txt` → ISO 9001 Markdown
  - 去除 `===PAGE N===` 分页符
  - OCR 残损可疑处标记 `[待校对]`
  - 头部加 YAML front matter（标准号、名称、发布日期）
  - 保存为 `temp/standards/iso9001-2016.md`
  - 验证：章节覆盖 1–10 章，关键条款号（4.1/5.1/6.1/7.1/8.1/8.5.1 等）存在

- [ ] **A.3** 清洗 `gbt24001.txt` → ISO 14001 Markdown
  - 同上清洗规则
  - 保存为 `temp/standards/iso14001-2016.md`
  - 验证：章节覆盖 1–10 章

- [ ] **A.4** 清洗 `gbt45001.txt` → ISO 45001 Markdown
  - 同上清洗规则
  - 保存为 `temp/standards/iso45001-2020.md`
  - 验证：章节覆盖 1–10 章

- [ ] **A.5** 清洗 `gbt42061_ocr3.txt` → ISO 13485 Markdown
  - 同上清洗规则
  - 保存为 `temp/standards/iso13485-2016.md`（对应 GB/T 42061-2022）
  - 验证：章节覆盖完整

- [ ] **A.6** 写入库 SQL `scripts/db/20261028_doc_extract_knowledge_seed_V1.sql`
  - 4 行数据（Scope=standard，StandardCode = cert_iso_standard.Code GUID）
  - MarkdownContent 为清洗后 Markdown 全文（⚠️ 注意 SQL 转义）
  - PromptContent 暂为空（Phase C/D 填充）
  - 幂等：`INSERT ... ON DUPLICATE KEY UPDATE`

- [ ] **A.7** 执行入库 + 验证
  - 验 `SELECT COUNT(*) FROM cert_doc_extract_knowledge WHERE Scope='standard'` = **4**
  - 验 4 条 MarkdownContent 均非空且长度 > 10000
  - 用户肉眼审阅（取出任一条 `LEFT(MarkdownContent, 500)` 抽样）

**Phase A 验收**：用户确认 4 份 Markdown 质量可接受 → 签准进入 Phase B

---

## Phase B：全局总框架（平台总论）

> **目标**：建立 StandardCode=NULL 的全局总框架，作为兜底和通用骨架。

- [ ] **B.1** 编写全局总框架 MarkdownContent
  - 管理体系认证通用框架（审核逻辑、成文信息要求）
  - 文档分类体系定义（生产/运营/管理/内监/资质/设备/设计/监测 8 类）
  - 通用属性提取规则（文档名称/编号/过程归属/文档类型/文档分类/核心摘要/Markdown）
  - 缺失标注规范（"文档中未提及" / "文档不涉及" / null 三态）
  - 输出 JSON Schema 定义

- [ ] **B.2** 编写全局兜底 PromptContent
  - 通用属性的提取指令（所有标准共享的骨架）
  - 通用 JSON 输出格式定义
  - 约束：当某标准未配置专属提示词时，用此全局版本兜底

- [ ] **B.3** 入库全局行
  - Scope = 'global'，StandardCode = NULL
  - 验证唯一约束：Scope='global' 只有一行

**Phase B 验收**：用户审核总框架内容 → 签准进入 Phase C

---

## Phase C：ISO 9001 专属提示词 + 测试迭代

> **目标**：以 ISO 9001 为试点，完成"提示词编写 → 上传测试文档 → LLM → 看结果 → 调整"闭环。

- [ ] **C.1** 新建管理端页面"文档提取提示词管理"
  - 路由：`/business/doc-extract-knowledge`
  - 菜单：在"规则定义"（MENU_00302）下新增
  - 布局：左侧标准树（cert_iso_standard 列表 + "全局总框架"节点）+ 右侧编辑器
  - 右侧：Markdown 只读预览 + 提示词编辑器（textarea）+ [上传测试文档] [运行测试] [保存] [发布]

- [ ] **C.2** 新建后端 API
  - `DocExtractKnowledgeController : YzhControllerBase<DocExtractKnowledgeDto>`
  - GET `/config` + POST `/filter` + POST `/update`（由基类提供）
  - POST `/test`：接收上传文档 → 调用 AI Vision 转 Markdown → 拼接提示词的 `{{document_content}}` → 调用 LLM → 返回 JSON

- [ ] **C.3** DocExtractKnowledge 实体 + Service
  - 实体：`DocExtractKnowledge.cs`（映射 cert_doc_extract_knowledge）
  - Service：封装 PromptContent 读取 + MarkdownContent 读取 + 提示词拼接

- [ ] **C.4** 编写 ISO 9001 专属 PromptContent
  - 从 ISO 9001 视角定义每类文档的专属提取字段
  - 参考该国标对"成文信息"的具体要求（8.1/8.5 等）
  - 写入数据库（更新 cert_doc_extract_knowledge WHERE StandardCode = iso9001 GUID）

- [ ] **C.5** 测试迭代
  - 上传 3–5 份不同类型的企业文档（真实案例或模拟）
  - 运行提取 → 检查 JSON 结构 + 字段完整性 + 缺标注
  - 反复调整 PromptContent，直到用户满意

**Phase C 验收**：用户确认 ISO 9001 产出满足要求 → 签准进入 Phase D

---

## Phase D：完善全局 + 批量建立其他 3 个标准提示词

> **目标**：基于 ISO 9001 的经验，完成全部 4 个标准的专属提示词。

- [ ] **D.1** 基于 Phase C 经验，完善全局总框架
  - 审视通用属性部分是否需要增删
  - 兜底提示词是否需要调整

- [ ] **D.2** 编写 ISO 14001 专属 PromptContent
  - 环境管理视角的专属字段（环境因素识别/评价准则/控制措施/法规对应/排污许可…）
  - 参照 ISO 14001 Markdown 文件中的章节（6.1.2/8.1/8.2 等）

- [ ] **D.3** 编写 ISO 45001 专属 PromptContent
  - 职业健康安全视角（危险源辨识/LEC/控制方式/安全生产许可…）

- [ ] **D.4** 编写 ISO 13485 专属 PromptContent
  - 医疗器械质量管理视角（设计开发/风险管理/法规要求/产品特性…）

- [ ] **D.5** 4 个标准各跑测试 + 发布
  - 每标准至少 1 份测试文档验证
  - 用户确认后 `IsPublished = 1`

- [ ] **D.6** 跑前端守卫 + 后端编译
  - `node scripts/guards.mjs` → 0 违规
  - `dotnet build` → 0 Error

**Phase D 验收**：4 个标准全部发布，文档提取功能可用 → 进入下一模块（执行引擎）

---

## 关键决策记录

| 决策 | 结论 | 理由 |
|---|---|---|
| Markdown + 提示词是否分表？ | 合一张表（用户裁决） | 简化关联；标准未入库时无需多表 JOIN |
| StandardCode 为空 = 全局？ | 是（用户裁决） | NULL 是天然语义，不需额外枚举字段表示"全局" |
| Markdown 内容存 DB 还是文件？ | 存 DB（MarkdownContent MEDIUMTEXT） | 提示词+Markdown 同表，事务一致；MEDIUMTEXT 上限 16MB 足够 |
| 提示词测试页面的方案？ | 新建（参考 iso-standard 左树右编辑） | 现有 prompt-template 交互模型不同，不适合 |

---

*本 TODO 严格串行：A → B → C → D，每阶段须用户确认。断点续做时从第一个 `[ ]` 往下读。*
