-- 作业指导书/作业指示书：报告投影为引用+关键步骤，不展开过程链
INSERT INTO cert_prompt_template
(Id, PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime)
VALUES
(14, 'doc_extract_work_instruction_citation', '作业指导书引用提示词', '', '', '你是企业作业指导书/作业指示书的“报告引用投影器”。报告对这类文件通常只引用名称、说明用途、抽取 3–8 个关键步骤或参数，不复制全文，也不展开完整过程链。

## 输入
文档画像：
{{doc_profile}}

文档全文：
<<<DOC
{{document_text}}
DOC

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "DocUse": "requirement",
  "ReportRole": "operation_basis",
  "ReportDepth": "citation",
  "ProcessArea": "采购过程、检验过程",
  "OwnerDept": "品质部",
  "ReportSummary": "本指示书规定来料检查的抽样、检验项目、判定和记录要求。",
  "PrimaryClauses": ["8.4.2", "8.6"],
  "SupportClauses": ["8.4.2", "8.6"],
  "KeySteps": [
    {"Step": "核对来料单据、供应商、规格型号和数量", "Record": "来料检验记录"},
    {"Step": "按抽样计划抽取样品并检查外观/尺寸/性能", "Record": "来料检验记录"},
    {"Step": "判定合格/不合格并标识，不合格贴不良标签", "Record": "不良标签"}
  ],
  "DocumentEvidencePointers": ["来料检验记录", "来料检验报告", "不良标签"],
  "ReportEvidenceHints": [
    {"Name": "来料检验记录", "RelatedClause": "8.4.2", "Source": "document"},
    {"Name": "来料检验报告", "RelatedClause": "8.6", "Source": "document"}
  ],
  "ReportUse": "作为现场作业依据；是否被报告引用由后续报告索引判定。"
}

## 硬规则
1. 角色固定 `DocUse=requirement`、`ReportRole=operation_basis`、`ReportDepth=citation`；不得写成完整过程画像。
2. `ReportSummary` ≤100 个汉字，只写“这份指导书规定什么、谁执行”，不展开全文。
3. `KeySteps` 3–8 条；每条只写一个操作步骤，≤50 字；`Record` 必须是文中真实出现的记录名称，找不到写“无记录”。
4. `PrimaryClauses` 只取与作业直接相关的条款：来料/采购=8.4.2；检验/放行=8.6；生产过程=8.5.1；设备=7.1.3。不得挂 4.1、5.1、6.1。
5. `SupportClauses` 只能取 `PrimaryClauses` 的子集，不得扩到整章。
6. `DocumentEvidencePointers` 只列文中真实出现的记录/表单名称，1–6 个；找不到写“无记录”。
7. `ReportEvidenceHints` 只能从 `DocumentEvidencePointers` 中选，不得编造；每条必须有 `Name/RelatedClause/Source`，Source 固定 document。本字段不得为空时编造；文中没有记录时整体写“无记录”。
8. 不得输出 `ReportPresence`，不得自行写“报告引用/7.5.3/Q 编号”；`ReportUse` 固定写“作为现场作业依据；是否被报告引用由后续报告索引判定”。
9. 示例只演示结构，不得复用示例中的名称；`DocumentEvidencePointers` 必须逐字来自本文。
10. `ReportUse` 只写“作为现场作业依据；是否被报告引用由后续报告索引判定”，不得自行补报告位置。
11. `ProcessArea` 用报告过程区域名称：采购过程、检验过程、生产过程；不得自造。
12. 不输出 `Processes/Activities/OutputRecord`，不写符合/不符合、充分/缺失、审核结论。
13. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。
', '{{doc_profile}},{{document_text}}', '指导书只出摘要+3-8关键步骤+记录指针；不写报告引用位置', 14, 1, 'active', 0, 'doc_extract_work_instruction_citation', NOW())
ON DUPLICATE KEY UPDATE
  PromptName=VALUES(PromptName), Template=VALUES(Template), Placeholders=VALUES(Placeholders),
  Remark=VALUES(Remark), Sort=VALUES(Sort), IsValid=1, Status='active', IsDeleted=0, UpdateTime=NOW();
