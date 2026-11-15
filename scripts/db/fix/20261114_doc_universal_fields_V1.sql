-- 2026-11-14 文档类型提示词补通用属性（Id=9–16）
-- 目的：所有类型提示词先输出 DocType/DocTitle/StandardRelevant/Extractable/ExtractReason，
--       再输出该类型专属字段；不改变原提示词的专属字段与硬规则。
-- 幂等：Template 已含 "DocType": 时不重复插入。

CREATE TABLE IF NOT EXISTS cert_prompt_template_bak_20261114 AS
SELECT * FROM cert_prompt_template WHERE Id BETWEEN 9 AND 16;

UPDATE cert_prompt_template
SET Template = REPLACE(Template,
'  "DocUse":',
'  "DocType": "procedure",
  "DocTitle": "生产和服务提供控制程序",
  "StandardRelevant": true,
  "Extractable": true,
  "ExtractReason": "程序文件，规定过程控制要求",
  "DocUse":')
WHERE Id = 9 AND IsDeleted = 0 AND Template NOT LIKE '%"DocType":%';

UPDATE cert_prompt_template
SET Template = REPLACE(Template,
'  "DocUse":',
'  "DocType": "record",
  "DocTitle": "来料检验记录",
  "StandardRelevant": true,
  "Extractable": true,
  "ExtractReason": "记录表单，提供执行证据",
  "DocUse":')
WHERE Id = 10 AND IsDeleted = 0 AND Template NOT LIKE '%"DocType":%';

UPDATE cert_prompt_template
SET Template = REPLACE(Template,
'  "DocUse":',
'  "DocType": "index",
  "DocTitle": "文件索引",
  "StandardRelevant": true,
  "Extractable": true,
  "ExtractReason": "索引类文件，只提供目录与引用关系",
  "DocUse":')
WHERE Id = 11 AND IsDeleted = 0 AND Template NOT LIKE '%"DocType":%';

UPDATE cert_prompt_template
SET Template = REPLACE(Template,
'  "DocUse":',
'  "DocType": "audit_record",
  "DocTitle": "内审检查表",
  "StandardRelevant": true,
  "Extractable": true,
  "ExtractReason": "内审/评审记录，提供二线审核证据",
  "DocUse":')
WHERE Id = 12 AND IsDeleted = 0 AND Template NOT LIKE '%"DocType":%';

UPDATE cert_prompt_template
SET Template = REPLACE(Template,
'  "DocUse":',
'  "DocType": "document_control",
  "DocTitle": "受控文件清单",
  "StandardRelevant": true,
  "Extractable": true,
  "ExtractReason": "文件控制类文档，提供编制/审批/版本/分发信息",
  "DocUse":')
WHERE Id = 13 AND IsDeleted = 0 AND Template NOT LIKE '%"DocType":%';

UPDATE cert_prompt_template
SET Template = REPLACE(Template,
'  "DocUse":',
'  "DocType": "work_instruction",
  "DocTitle": "来料检查作业指示书",
  "StandardRelevant": true,
  "Extractable": true,
  "ExtractReason": "作业指导书，提供关键步骤与验收要求",
  "DocUse":')
WHERE Id = 14 AND IsDeleted = 0 AND Template NOT LIKE '%"DocType":%';

UPDATE cert_prompt_template
SET Template = REPLACE(Template,
'  "DocUse":',
'  "DocType": "context_list",
  "DocTitle": "外部因素识别监视评价总清单",
  "StandardRelevant": true,
  "Extractable": true,
  "ExtractReason": "组织环境/相关方清单，提供 4.1/4.2 证据",
  "DocUse":')
WHERE Id = 15 AND IsDeleted = 0 AND Template NOT LIKE '%"DocType":%';

UPDATE cert_prompt_template
SET Template = REPLACE(Template,
'  "DocUse":',
'  "DocType": "policy",
  "DocTitle": "销售人员客户拜访管理办法",
  "StandardRelevant": true,
  "Extractable": true,
  "ExtractReason": "制度/规范/管理办法，提供管理规则证据",
  "DocUse":')
WHERE Id = 16 AND IsDeleted = 0 AND Template NOT LIKE '%"DocType":%';

-- 验证：8 条均应含 DocType
SELECT Id, PromptCode,
       CASE WHEN Template LIKE '%"DocType":%' THEN 'OK' ELSE 'MISSING' END AS HasDocType
FROM cert_prompt_template
WHERE Id BETWEEN 9 AND 16 AND IsDeleted = 0
ORDER BY Id;
