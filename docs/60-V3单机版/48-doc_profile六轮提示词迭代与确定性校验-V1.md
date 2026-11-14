# doc_profile 六轮提示词迭代与确定性校验 V1

> 目标：只优化“文档画像”提示词，不改最终证据提示词，不接运行时主链。
> 结论：`doc_profile` 第 6 版已达到可用基线；12 份真实企业文档经确定性清洗后 **0 残留问题**。
> 落库位置：`cert_prompt_template.doc_profile`，平台级 `OrgCode=''`、`ScopeKey=''`，`IsValid=1`、`Status=active`。
> SQL：`scripts/db/fix/20261113_doc_prompt_templates_V1.sql`（45,342 bytes，幂等）。
> 当前 DB 模板长度：10,603 字符。

---

## 一、六轮迭代做了什么

| 轮次 | 暴露的问题 | 加入的规则 | 结果 |
|---|---|---|---|
| 第 1 轮 | 手册主章节写成 4；索引判 `direct/partial`；财务制度判 `irrelevant` 却仍 `work_instruction/process_profile`；把“体系编写小组”当部门；管理评审记录判 `direct`；内审把 9 当主章节 | 多章规则、索引硬路由、无关制度硬路由、作者不等于部门、SupportDepts 不得泛称 | 大方向修正，仍有残留 |
| 第 2 轮 | 手册 `PrimaryChapter` 已为空；索引已 `index_only`；管理评审已 `supporting`；财务仍残留 `work_instruction`；组织环境程序仍有“总经理/各部门” | 增加“输出前一致性自检 A–J”和“按过程有限推断部门” | 财务、设备规程、来料检查、生产通知、设备台账的部门开始可推断 |
| 第 3 轮 | 内审 `ClauseNumber` 带 `Q：`/`Q:`/`Q` 前缀；程序文件多塞次章节；`SupportDepts` 仍有“总经理/各部门” | 增加“ClauseNumber 纯编号清洗”；次章节必须有同章节条款支撑；SupportDepts 词法校验 | 编号清洗成功；财务已 `other/none/irrelevant/none` |
| 第 4 轮 | 模型自造 `Extractability=structured`；组织环境程序仍漏“总经理/各部门”；次章节仍可能无条款支撑 | `Extractability` 白名单；审核记录归属规则；自检 H/I/J 加强 | `structured` 消失；内审归属部门修正为体系管理部 |
| 第 5 轮 | 内审把标题括号里的“生产部”当 `OwnerDept`；组织环境程序仍有“总经理” | 明确：`audit_record` 的 `OwnerDept` = 内审归口部门，受审核部门放 `SupportDepts` | 内审 `OwnerDept` 恢复为体系管理部 |
| 第 6 轮 | 提示词已基本稳定；仅剩 2 类长尾：`总经理/各部门`、无条款支撑的次章节 | 不加新提示词规则，改用确定性清洗验证 | 12 份全部通过清洗；**0 残留问题** |

---

## 二、第 6 版提示词的关键约束

### 1. 先判 QMS 相关性，再判文档类型

```text
财务、人事、销售、考勤、行政后勤等非 QMS 制度
→ DocType=other，ExtractionMode=none，StandardRelevance=irrelevant，Extractability=none，ClauseLinks=[]
```

这条是硬路由，不能因为文件名带“制度/规范”就判成 `work_instruction`。

### 2. 按类型硬路由提取模式

```text
manual / policy                 → clause_full
procedure / work_instruction    → process_profile
record / form                   → record_profile
index / 封面 / 清单 / 汇编       → index_only
audit_record / review_record    → audit_profile
other                           → none
```

只有 `manual/policy` 逐条款；其余都不逐条款。

### 3. 章节边界

```text
procedure / work_instruction / record：PrimaryChapter 只允许 1 个，SecondaryChapters 最多 1 个
manual / policy：PrimaryChapter=""，SecondaryChapters 列实际覆盖的 4–10 章
audit_record / review_record：PrimaryChapter=""，覆盖章节放 SecondaryChapters
index：PrimaryChapter=""，SecondaryChapters=[]
SecondaryChapters 中每个章节必须能在 ClauseLinks 找到同章节条款，否则删除
```

### 4. 部门边界

```text
OwnerDept 必须是“负责/主管/归口/使用/执行”的部门或职能
“编制/审核/批准/体系编写小组/编写组/文件编写小组/文控”是作者/审批角色，不是归属部门
“总经理/各部门/相关部门/有关部门/全体员工”不是部门，不得进 SupportDepts
SupportDepts 只写正文明确出现的部门；每个名称以“部/科/室/中心/组”结尾，或属于“管理层/最高管理者/管理者代表/内审组”
audit_record 的 OwnerDept = 内审归口部门（体系管理部/管理者代表/内审组），不得填标题括号里的受审核部门
```

允许按过程做有限推断，但必须标 `DeptSource=inferred`、`Confidence ≤ 0.8`：

```text
来料/进料/巡检/检验/品质 → 品质部
生产/工序/设备操作/生产通知 → 生产部
采购/供应商 → 采购部
仓库/入库/出库/发货 → 仓储部
财务/费用/报销 → 财务部
培训/考勤/人事 → 行政部或人事部
```

### 5. 条款编号必须纯数字

```text
ClauseNumber 只能是数字和点（4.1、8.5.1）
原文的 Q：5.3、Q:6.1、Q7.1.3、E：8.1 等前缀必须先清洗
清洗不干净就不输出该条
```

### 6. 输出前自检 A–J

提示词末尾已加入 A–J 自检：QMS 相关性、索引、审核/评审、手册、程序/指导书、记录、部门来源、SupportDepts 词法、ClauseLinks 关系与编号、次章节支撑、DocType 与 ExtractionMode 匹配。

---

## 三、第 6 轮 12 份样本结果（清洗后）

| 文档 | DocType | ExtractionMode | PrimaryChapter | SecondaryChapters | OwnerDept | SupportDepts | DeptSource | StandardRelevance | Extractability | Confidence |
|---|---|---|---|---|---|---|---|---|---|---|
| 01管理手册QMS.doc | manual | clause_full | "" | 4–10 | 管理层 | [] | explicit | direct | full | 0.95 |
| 01组织环境相关方需求期望控制程序.doc | procedure | process_profile | 4 | [] | 综合部 | [] | explicit | direct | full | 0.95 |
| 17生产和服务提供控制程序.doc | procedure | process_profile | 8 | [7] | 生产部 | [品质部] | explicit | direct | full | 0.95 |
| 09通用设备安全操作规程.doc | work_instruction | process_profile | 8 | [7] | 生产部 | [] | inferred | direct | full | 0.80 |
| 10来料检查作业指示书.xlsx | work_instruction | process_profile | 8 | [] | 品质部 | [] | inferred | direct | full | 0.80 |
| 04生产通知单.xls | record | record_profile | 8 | [] | 生产部 | [] | inferred | supporting | full | 0.80 |
| 01设备台账.xlsx | record | record_profile | 7 | [] | 生产部 | [] | inferred | supporting | full | 0.80 |
| 05-1内审检查表（生产部）.docx | audit_record | audit_profile | "" | [5,6,7,8] | 体系管理部 | [生产部] | directory | supporting | full | 0.95 |
| 管理评审记录.doc | review_record | audit_profile | "" | 4–10 | 管理层 | [综合部,生产部,质量部,仓储部,销售部,采购部,财务部] | explicit | supporting | full | 0.95 |
| 00程序文件汇编封面.doc | index | index_only | "" | [] | "" | [] | unknown | reference | index_only | 0.95 |
| 07记录清单.docx | index | index_only | "" | [] | "" | [] | unknown | reference | index_only | 0.95 |
| 02公司财务管理制度.doc | other | none | "" | [] | "" | [] | unknown | irrelevant | none | 0.95 |

清洗前仍有 2 类长尾：

- `01组织环境相关方需求期望控制程序.doc`：`SupportDepts=["总经理","各部门"]`，`SecondaryChapters=["5"]` 但 `ClauseLinks` 只有 4.1/4.2。
- `05-1内审检查表（生产部）.docx` 在第 5 轮曾把受审核部门“生产部”当 `OwnerDept`，第 6 轮提示词已修正为“体系管理部”。

清洗后 `TOTAL_AFTER_ISSUES=0`。

---

## 四、为什么还需要代码侧确定性校验

模型能理解规则，但不能保证每次执行。第 6 轮仍出现“总经理/各部门”和“次章节 5 无条款支撑”，说明**仅靠提示词自检不够**。建议在接入运行时前加 `DocProfileGuard`，顺序固定：

```text
LLM 输出 JSON
→ 1. 枚举白名单（DocType / ExtractionMode / Extractability / Relation）
→ 2. 脏编号清洗（Q：/Q:/E：/m：/ISO9001 前缀）
→ 3. 次章节裁剪（只保留 ClauseLinks 同章节）
→ 4. 部门清洗（泛称/角色删除，只保留合法部门词）
→ 5. 硬路由（irrelevant / index / audit / review / manual）
→ 6. DeptSource=unknown 时清空部门
→ 7. 落库
```

校验规则已先在 `/tmp/doc_profile_guard.py` 中验证通过；后续接入 C# 时按同一顺序实现，不要只信任模型输出。

---

## 五、下一步，不需要你手动执行

本轮画像测试已自动完成，当前不需要你操作。请**不要**在企业资料页点“重新分析”：

- 现在点“重新分析”仍会强制重跑旧的 `doc_clause_extract`，不是新的 `doc_profile`。
- 新 6 条提示词虽然已落库，但 `DocExtractPromptService` / `ClauseExtractExecutor` 还没接新提示词。
- 现在重跑只会得到旧结构，不会得到这份画像结果。

你可以只看结果：

```text
/tmp/doc_profile_round6_guarded.json
```

查看当前 DB 提示词：

```bash
PW=$(docker exec yzh-mysql printenv MYSQL_ROOT_PASSWORD)
docker exec -i yzh-mysql mysql -uroot -p"$PW" --default-character-set=utf8mb4 yzh_cert_platform -N -B \
  -e "SELECT Template FROM cert_prompt_template WHERE PromptCode='doc_profile' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0 LIMIT 1;"
```

---

## 六、建议的下一步顺序

1. **先测 5 条按模式提取提示词**，不要先改主链：
   - `doc_extract_clause_full`：用 `/tmp/manual_4_1.md`、`/tmp/manual_4_3.md`、`/tmp/manual_5_1.md`、`/tmp/manual_6_1.md`
   - `doc_extract_process_profile`：用 `01组织环境相关方需求期望控制程序.doc`、`17生产和服务提供控制程序.doc`
   - `doc_extract_record_profile`：用 `04生产通知单.xls`、`01设备台账.xlsx`
   - `doc_extract_index`：用 `00程序文件汇编封面.doc`、`07记录清单.docx`
   - `doc_extract_audit`：用 `05-1内审检查表（生产部）.docx`、`管理评审记录.doc`
2. 5 条提取提示词稳定后，再接 `DocExtractPromptService` 的路由。
3. 路由稳定后，再把 `DocProfileJson` 扩展字段写回 `cert_enterprise_doc`。
4. 最后才改证据提示词，避免“画像没稳、提取没稳、证据也跟着错”。

---

## 七、当前结论

- `doc_profile` 第 6 版：**可作为基线**。
- 12 份真实文档：**清洗后 0 残留问题**。
- 仍需代码侧 `DocProfileGuard`：**必须做，不是可选**。
- 5 条提取提示词：**尚未实跑**，是下一步。
- 运行时主链：**尚未接入**，当前“重新分析”仍是旧提示词。
