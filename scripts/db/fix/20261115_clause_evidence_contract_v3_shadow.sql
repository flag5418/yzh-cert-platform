-- 20261115_clause_evidence_contract_v3_shadow.sql
-- 用途：P2 影子变更脚本（未执行）。把 cert_clause_attr_def.EvidenceContract 的提示从旧 schema 改为 v3。
-- 执行前：先备份；本脚本不覆盖生产数据，确认 P4 通过后再执行。
-- 备份表：cert_clause_attr_def_bak_20261115_v3

-- 0. 备份
CREATE TABLE IF NOT EXISTS cert_clause_attr_def_bak_20261115_v3 AS
SELECT * FROM cert_clause_attr_def WHERE Code = 'EvidenceContract';

-- 1. 改 PromptHint（varchar 255，必须压在一行内）
UPDATE cert_clause_attr_def
SET PromptHint = 'JSON对象v3：ContractVersion/ChapterCode/PDCAPhase/EvidenceItems[ItemId/DocRole/EvidenceRole/Name/NameAliases/Required/Condition/ExpectedFields/Frequency/Coverage/ReportUse]/Dependencies/CrossChecks/AuditorRules',
    Remark = 'v3：证据单元数组+依赖+交叉检查+审核员规则；空白模板=template_only，不写企业事实/符合性',
    UpdateTime = NOW()
WHERE Code = 'EvidenceContract'
  AND IsDeleted = 0;

-- 2. 校验：只应有 1 行
SELECT Id, Code, PromptHint, Remark
FROM cert_clause_attr_def
WHERE Code = 'EvidenceContract' AND IsDeleted = 0;
