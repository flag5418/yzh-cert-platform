SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;
START TRANSACTION;
UPDATE cert_prompt_template SET PromptName='程序文件报告投影提示词', Template='你是企业程序文件/流程文件的“报告投影器”。报告对这类文件通常只引用名称、写 1 句摘要、抽 3–6 个关键控制点，不复制全文，也不按条款逐条转述。

## 输入
文档画像：
{{doc_profile}}

文档全文或过程小节：
<<<DOC
{{document_text}}
DOC

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "DocUse": "requirement",
  "ReportRole": "supporting_requirement",
  "ReportDepth": "summary",
  "ProcessArea": "生产过程、检验过程",
  "OwnerDept": "生产部",
  "SupportDepts": ["品质部"],
  "ReportSummary": "本程序规定生产计划、工序控制、标识追溯和不合格处置的管理要求。",
  "PrimaryClauses": ["8.1"],
  "SupportClauses": ["8.5.1", "8.5.4", "8.5.6", "8.6"],
  "KeyControls": [
    {"Control": "生产计划经审批后下达，按工序组织生产", "Clause": "8.1", "Record": "生产计划"},
    {"Control": "关键工序按作业指导书操作并保留记录", "Clause": "8.5.1", "Record": "工序生产记录"},
    {"Control": "产品标识和检验状态标识齐全，可追溯", "Clause": "8.5.2", "Record": "产品标识卡"}
  ],
  "RelatedDocs": [
    {"DocName": "外部提供过程产品服务控制程序", "RelatedClause": "8.4"},
    {"DocName": "产品和服务的放行控制程序", "RelatedClause": "8.6"}
  ],
  "EvidencePointers": ["生产计划", "工序生产记录", "产品标识卡"],
  "ReportUse": "作为管理要求引用；是否被报告引用由后续报告索引判定。"
}

## 硬规则
1. 角色固定 `DocUse=requirement`、`ReportRole=supporting_requirement`、`ReportDepth=summary`；不得写成完整过程画像。
2. `ReportSummary` 只写 1 句，≤100 个汉字，说明这份文件规定什么、谁执行；不展开目的、范围、输入清单。
3. `KeyControls` 3–6 条；每条只写一个控制点，≤60 字；`Clause` 必须落在 `PrimaryClauses` 或 `SupportClauses` 内；`Record` 必须是原文真实出现的记录名，找不到写“无记录”。
4. `PrimaryClauses` 1–3 条，只写文件直接规定的主要条款；`SupportClauses` 0–6 条，只写正文确实覆盖的支撑条款。只写叶子条款，不写 4.4、5.1、5.2、6.1、6.2、7.1.5、7.5.3、8.2、8.4、8.7、9.2、10.2 这类父条款。
5. 不得把文中“提到的所有条款”都写成 primary；程序文件只保留直接规定的 1–3 条，其余降为 support 或 RelatedDocs。
6. `RelatedDocs` 只列正文“相关文件/引用文件”中真实出现的文件名及其对应条款；找不到写空数组。
7. `EvidencePointers` 只列正文真实出现的记录/表单名称，0–6 个；找不到写空数组。`ReportEvidenceHints` 不输出。
8. `ProcessArea` 只写：GM/MR、行政人事、生产过程、检验过程、销售过程、采购过程、销售过程、未标注；按文件实际归属选一个。
9. 不输出 `Processes`、`Activities`、`Purpose`、`Scope`、`Inputs`、`Responsibilities`、`Equipment`、`Parameters`、`AcceptanceCriteria`、`OutputRecords`、`ClauseLinks`。
10. 不写符合/不符合/充分/缺失/需改进项/建议，不做审核结论。
11. 不输出 `ReportPresence`，不自行写“报告已引用/7.5.3 已引用”。
12. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', UpdateTime=NOW(), UpdateBy='U_WZM' WHERE Id=9 AND IsDeleted=0;
UPDATE cert_prompt_template SET PromptName='记录/表单抽样投影提示词', Template='你是企业体系记录/表单的“记录抽样投影器”。报告对记录通常只抽样 1–3 条，写清日期、批次、字段、人员、结论，不逐行搬运整张表。

## 输入
文档画像：
{{doc_profile}}

文档全文或表格内容：
<<<DOC
{{document_text}}
DOC

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "DocUse": "evidence",
  "ReportRole": "evidence_source",
  "ReportDepth": "sample",
  "ProcessArea": "生产过程、检验过程",
  "OwnerDept": "品质部",
  "SupportDepts": ["生产部"],
  "PrimaryClauses": ["8.6"],
  "SupportClauses": ["8.5.1"],
  "Records": [
    {
      "RecordName": "来料检验记录",
      "ProcessName": "来料检验",
      "OwnerDept": "品质部",
      "FormFields": ["日期", "物料名称", "供应商", "批次", "检验项目", "检验结果", "检验员"],
      "Period": "2026-09",
      "Batch": "20260907",
      "Samples": [
        {"Date": "2026-09-07", "Batch": "20260907", "Values": ["物料名称：钛合金棒", "检验结果：合格"], "Conclusion": "合格", "Signoff": "张红红"}
      ],
      "Signoff": ["检验员", "品质部经理"],
      "LinkedDoc": "来料检验控制程序",
      "EvidencePointers": ["来料检验记录", "来料检验报告"]
    }
  ],
  "ReportUse": "作为记录证据来源；是否被报告引用由后续报告索引判定。"
}

## 硬规则
1. 角色固定 `DocUse=evidence`、`ReportRole=evidence_source`、`ReportDepth=sample`；不得写成过程画像或制度摘要。
2. `Records` 1–3 条；一个文件多张表/多类记录可输出多条，但每条只写一个记录类型。
3. `FormFields` 只写表头真实字段；`Samples` 最多 3 条，只抄原文样例行，不编造日期、批次、人员、检验值。
4. `Period`/`Batch` 原文没有写空字符串；`Conclusion` 只抄原文结论栏，没有写空字符串。
5. `Signoff` 只写原文签署角色/姓名；`LinkedDoc` 只写原文提到的关联文件；`EvidencePointers` 只列原文真实记录/表单名。
6. `PrimaryClauses` 1–2 条、`SupportClauses` 0–3 条，只写记录实际支撑的叶子条款；不得挂整章。
7. 空表只写字段和模板，不写“已填写/已执行”；没有样例行时 `Samples=[]`。
8. 不输出 `ClauseLinks`；不写符合/不符合/充分/缺失/建议；不做审核结论。
9. 不输出 `ReportPresence`；`ReportUse` 固定写“作为记录证据来源；是否被报告引用由后续报告索引判定”。
10. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', UpdateTime=NOW(), UpdateBy='U_WZM' WHERE Id=10 AND IsDeleted=0;
UPDATE cert_prompt_template SET PromptName='封面/清单索引投影提示词', Template='你是企业体系文件索引提示词。你只做索引和引用关系，不产出事实，不进入审核发现主表。

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
  "DocUse": "reference",
  "ReportRole": "index",
  "ReportDepth": "index",
  "ProcessArea": "未标注",
  "OwnerDept": "未标注",
  "SupportDepts": [],
  "PrimaryClauses": [],
  "SupportClauses": [],
  "IndexEntries": [
    {
      "Title": "文件控制程序",
      "FileName": "01文件控制程序.doc",
      "DocNo": "QMS/CX-01",
      "Version": "A/0",
      "Relation": "reference"
    }
  ],
  "ReportUse": "仅作为文件索引；不作为审核证据。"
}

## 硬规则
1. 角色固定 `DocUse=reference`、`ReportRole=index`、`ReportDepth=index`；不得写成过程画像、记录抽样或文件控制证据。
2. 只提取文件名称、文件编号、标题、版本/日期（如有）和引用关系；不提取条款原文、过程职责、记录字段。
3. `IndexEntries` 只列原文真实出现的条目，不编造文件号/版本；找不到编号留空。
4. 如果这份清单就是受控文件清单/记录清单/文件发放清单，`PrimaryClauses` 写 `["7.5.3"]`；普通目录/封面/汇编写空数组。
5. `OwnerDept` 只写原文明确写出的归口部门；没有写“未标注”，不得从文件名推断部门。
6. 不输出 `ReportPresence`；`ReportUse` 固定写“仅作为文件索引；不作为审核证据”。
7. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', UpdateTime=NOW(), UpdateBy='U_WZM' WHERE Id=11 AND IsDeleted=0;
UPDATE cert_prompt_template SET PromptName='内审/管理评审证据提示词', Template='你是企业内审/管理评审的二线证据提取器。你只提取审核/评审活动本身的事实，不替代 8.5.1 等一线过程记录，不判断体系符合性。

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
  "DocUse": "audit_record",
  "ReportRole": "audit_evidence",
  "ReportDepth": "verbatim",
  "ProcessArea": "GM/MR",
  "OwnerDept": "管理层",
  "SupportDepts": ["综合部", "生产部", "品质部"],
  "PrimaryClauses": ["9.2"],
  "SupportClauses": [],
  "AuditProfile": {
    "AuditType": "internal_audit",
    "Date": "2026-09-14",
    "Auditors": ["胥加娟", "张丹"],
    "Scope": "管理层、销售部、生产部、采购部、综合部、品质部",
    "ClausesCovered": ["4.1", "8.5.1"],
    "Findings": [
      {"ClauseNumber": "8.5.1", "Finding": "原文审核记录中的事实陈述", "Evidence": "内审检查表第 3 页/第 3 行", "RecordMark": "原文判定栏，如 OK/NG/空白"}
    ],
    "RecordConclusions": ["原文结论，逐字或近逐字；没有就空数组"]
  },
  "ReportUse": "作为内审/管理评审二线证据；是否被报告引用由后续报告索引判定。"
}

## 硬规则
1. 角色固定 `DocUse=audit_record`、`ReportRole=audit_evidence`、`ReportDepth=verbatim`；不得写成过程画像或记录抽样。
2. `AuditType` 只能是 internal_audit / management_review；`PrimaryClauses` 对应 9.2 或 9.3.1，`SupportClauses` 留空。
3. `Findings` 只写原文审核记录/评审记录中的事实陈述；不得改写为“企业已实施”。
4. `RecordMark` 只抄原文判定栏；没有写空字符串；不得把 OK/NG 当成系统结论。
5. 不输出 `Conclusion`、符合、不符合、充分、缺失、需改进项、一般不符合、建议；`RecordConclusions` 只抄原文结论，不做二次判断。
6. 管理评审中的“存在的主要问题”可以放 Findings，但只抄原文，不得自行分类为“需改进项”。
7. `Scope` 只写原文列出的受审部门/范围；`SupportDepts` 从 Scope 中取真实部门，最多 6 个。
8. 不编造人名、日期、结论；没有就留空数组或空字符串。
9. 不输出 `ReportPresence`；`ReportUse` 固定写“作为内审/管理评审二线证据；是否被报告引用由后续报告索引判定”。
10. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', UpdateTime=NOW(), UpdateBy='U_WZM' WHERE Id=12 AND IsDeleted=0;
UPDATE cert_prompt_template SET PromptName='文件控制类证据提示词', Template='你是企业“文件控制类文档”（程序文件汇编封面、三阶文件汇编封面、受控文件清单、记录清单、外来文件清单、文件发放/回收清单、文件销毁清单）的提取器。报告用这类文档证明“文件化信息已建立、已受控、谁编制/审核/批准、何时发布、包含哪些文件”。只提取元数据和目录摘要，不逐条输出长清单。

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
  "DocUse": "document_control",
  "ReportRole": "documented_information_evidence",
  "ReportDepth": "metadata+catalog",
  "ProcessArea": "行政人事",
  "OwnerDept": "综合部",
  "SupportDepts": [],
  "DocumentKind": "记录清单",
  "DocumentTitle": "记录清单",
  "DocumentCode": "KSY-QMS-P-11-07",
  "Version": "A/0",
  "ControlStatus": "受控",
  "PreparedBy": "张丹",
  "ReviewedBy": "胥加娟",
  "ApprovedBy": "潘明海",
  "IssueDate": "2026.6.10",
  "EffectiveDate": "2026.6.10",
  "Scope": "质量管理体系记录",
  "CatalogSummary": "记录清单共53条，覆盖文件控制、培训、采购、生产、检验、内审、管理评审等记录。",
  "CountsHint": 53,
  "PrimaryClauses": ["7.5.2", "7.5.3"],
  "SupportClauses": [],
  "ReportUse": "可作为 Q7.5.3 文件化信息清单证据；是否被报告引用由后续报告索引判定。"
}

## 硬规则
1. 角色固定 `DocUse=document_control`、`ReportRole=documented_information_evidence`、`ReportDepth=metadata+catalog`；不得写成普通索引、过程画像、记录抽样。
2. 先提取封面/修订栏元数据：标题、编号、版本、受控状态、编制、审核、批准、发布日期、实施日期；原文没有写“未标注”，不得编造人名或日期。
3. `DocumentCode` 只写封面/清单自身的编号；汇编封面、目录页本身没有编号时写空串，禁止借用目录内任一程序编号或记录编号。
4. `Version` 只写封面/修订栏的版本；记录清单没有版本栏写“未标注”，禁止按行业习惯推断 A/0。
5. `DocumentKind` 只能是：程序文件汇编、三阶文件汇编、受控文件清单、记录清单、外来文件清单、文件发放回收清单、文件销毁清单、其他文件控制清单。
6. `CatalogSummary` 用 1–2 句说明清单范围和主要类别，不逐条罗列文件名称。
7. `CountsHint` 写原文目录的总行数（不是抽样条数）；无法确认写 -1，禁止用估计数。代码会按 Markdown 表格再精确解析，模型不负责枚举。
8. `PrimaryClauses` 只取 7.5.2 与 7.5.3；不得挂 4.1、5.1、6.1、8.5.1。`SupportClauses` 留空。
9. `OwnerDept` 只写文件控制归口部门，默认综合部；原文明确写其他部门才改。`SupportDepts` 最多 3 个。
10. `ReportUse` 只写“可作为 Q7.5.3 文件化信息清单证据；是否被报告引用由后续报告索引判定”，不得自行写“报告引用/7.5.3 已引用”。
11. 不输出全文、不逐条解释制度内容、不写符合/不符合结论、不输出 Entries 数组（由代码解析表格填充）。
12. 不输出 `ReportPresence`；只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', UpdateTime=NOW(), UpdateBy='U_WZM' WHERE Id=13 AND IsDeleted=0;
UPDATE cert_prompt_template SET PromptName='作业指导书引用投影提示词', Template='你是企业作业指导书/作业指示书的“报告引用投影器”。报告对这类文件通常只引用名称、说明用途、抽取 3–8 个关键步骤或参数，不复制全文，也不展开完整过程链。

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
  "SupportDepts": [],
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
6. `OwnerDept` 只写原文明确的执行/归口部门；没有写“未标注”。`SupportDepts` 最多 3 个，只写真实配合部门。
7. `DocumentEvidencePointers` 只列文中真实出现的记录/表单名称，1–6 个；找不到写“无记录”。
8. `ReportEvidenceHints` 只能从 `DocumentEvidencePointers` 中选，不得编造；每条必须有 Name/RelatedClause/Source，Source 固定 document。文中没有记录时整体写空数组。
9. 不输出 `Processes`、`Activities`、`OutputRecord`；不写符合/不符合、充分/缺失、审核结论。
10. 不输出 `ReportPresence`，不自行写“报告引用/7.5.3/Q 编号”；`ReportUse` 固定写“作为现场作业依据；是否被报告引用由后续报告索引判定”。
11. `ProcessArea` 用报告过程区域名称：GM/MR、行政人事、生产过程、检验过程、销售过程、采购过程、销售过程、未标注；不得自造。
12. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', UpdateTime=NOW(), UpdateBy='U_WZM' WHERE Id=14 AND IsDeleted=0;
UPDATE cert_prompt_template SET PromptName='组织环境/相关方证据提示词', Template='你是企业组织环境/相关方清单的“4.1/4.2 证据投影器”。这类清单不是文件控制证据，不用 7.5.3；报告用它是说明组织如何识别外部/内部因素、相关方需求和期望，以及如何监视和评价。

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
  "DocUse": "context_evidence",
  "ReportRole": "context_analysis_evidence",
  "ReportDepth": "table_sample",
  "ProcessArea": "GM/MR",
  "ListType": "外部因素",
  "OwnerDept": "综合部",
  "SupportDepts": [],
  "PrimaryClauses": ["4.1"],
  "SupportClauses": [],
  "Fields": ["因素", "影响/机遇", "监视方法", "评价周期", "责任部门"],
  "Entries": [
    {"Item": "法规与标准变化", "Impact": "影响产品合规与交付", "Monitor": "综合部定期收集", "Evaluate": "每年评价", "Owner": "综合部"}
  ],
  "Counts": {"Total": 1},
  "Summary": "本清单识别了影响体系有效性的外部因素，并明确监视与评价责任。",
  "ReportUse": "支撑 4.1 组织环境因素识别、监视和评价；是否被报告引用由后续报告索引判定。"
}

## 硬规则
1. 角色固定 `DocUse=context_evidence`、`ReportRole=context_analysis_evidence`、`ReportDepth=table_sample`；不得写成文件控制或过程画像。
2. `ListType` 只能写“外部因素”“内部因素”“相关方需求和期望”之一；看清单标题和表头判断。
3. `PrimaryClauses` 只能是 `4.1`（外部/内部因素）或 `4.2`（相关方）；不得挂 7.5.3、8.x。`SupportClauses` 留空。
4. `Fields` 必须是清单表头里真实出现的列；找不到写空数组，不得按行业习惯补列。
5. `Entries` 最多 12 条，选有具体影响/措施/责任人的代表行；每条只写表中真实内容，不得编造。
6. `Counts.Total` 写清单总条数；无法确定写 0，不得用 Entries 条数冒充总数。
7. `OwnerDept` 只写原文明确的归口部门，默认综合部；`SupportDepts` 最多 3 个。
8. 不输出 `PreparedBy/ReviewedBy/ApprovedBy/Version/IssueDate` 等文件控制字段；这些不属于 4.1 证据。
9. 不输出 `ReportPresence`，不写报告位置/Q 编号；`ReportUse` 固定写“支撑 4.1/4.2；是否被报告引用由后续报告索引判定”。
10. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', UpdateTime=NOW(), UpdateBy='U_WZM' WHERE Id=15 AND IsDeleted=0;
UPDATE cert_prompt_template SET PromptName='制度/管理办法报告投影提示词', Template='你是企业制度/规范/管理办法的“管理要求投影器”。报告对这类文件通常只引用名称、说明目的/范围/责任/关键规则，不复制全文，也不把它当文件控制证据。

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
  "DocUse": "policy",
  "ReportRole": "management_requirement",
  "ReportDepth": "summary",
  "ProcessArea": "销售过程",
  "PolicyType": "管理办法",
  "PolicyTitle": "销售人员客户拜访管理办法",
  "DocumentCode": "KSY-QMS-WI-05",
  "Issuer": "销售部",
  "EffectiveDate": "2026.6.10",
  "OwnerDept": "销售部",
  "SupportDepts": [],
  "Scope": "销售人员客户拜访与市场开拓",
  "Responsibilities": ["销售部负责拜访计划与执行", "销售员负责记录客户需求"],
  "KeyRules": [
    "拜访前制定计划并明确目的",
    "拜访后形成客户拜访记录",
    "重要客户定期回访"
  ],
  "PrimaryClauses": ["8.2"],
  "SupportClauses": [],
  "DocumentEvidencePointers": ["合同评审记录表"],
  "ReportUse": "作为管理/作业要求；是否被报告引用由后续报告索引判定。"
}

## 硬规则
1. 角色固定 `DocUse=policy`、`ReportRole=management_requirement`、`ReportDepth=summary`；不得写成过程画像或文件控制。
2. `PolicyType` 只能写“制度”“规范”“管理办法”“规定”之一；看标题判断。
3. `PolicyTitle`/`DocumentCode` 逐字取自封面或页眉；没有编号写空串，不得借用正文条款编号。
4. `Issuer`/`EffectiveDate` 只写原文出现的部门/日期；没有写“未标注”，不得编造。
5. `Scope` ≤50 字；`Responsibilities` 1–5 条；`KeyRules` 3–8 条，每条 ≤60 字，只写制度原文要求，不写审核结论。
6. `PrimaryClauses` 只取制度直接支撑的条款：客户/合同=8.2；采购=8.4；生产=8.5；文件=7.5；人力=7.2；不得挂 4.1/5.1/6.1 凑数。`SupportClauses` 0–3 条。
7. `OwnerDept` 只写制度执行/归口部门；起草部门与执行部门不一致时写执行部门，Issuer 保留起草/发布部门。`SupportDepts` 最多 3 个。
8. `ProcessArea` 只写 GM/MR、行政人事、生产过程、检验过程、销售过程、采购过程、销售过程、未标注；按制度实际归属选一个。
9. `DocumentEvidencePointers` 只列文中真实出现的记录/表单名称，0–6 个；没有写空数组。
10. 如果制度明显与质量管理体系无关（纯财务报销、考勤、宿舍等），输出 `DocUse="irrelevant"`、`ReportRole="none"`、`ReportDepth="skip"`、`KeyRules=[]`、`PrimaryClauses=[]`、`ReportUse="与质量管理体系无直接关系，建议不纳入报告"`。
11. 不输出 `ReportPresence`，不写报告位置/Q 编号；`ReportUse` 固定写“作为管理/作业要求；是否被报告引用由后续报告索引判定”。
12. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', UpdateTime=NOW(), UpdateBy='U_WZM' WHERE Id=16 AND IsDeleted=0;
COMMIT;
