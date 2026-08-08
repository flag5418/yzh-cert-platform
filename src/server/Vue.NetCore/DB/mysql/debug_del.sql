USE yzh_cert_platform;

-- 检查 CertStage 表的所有记录
SELECT Id, StageCode, StageName, Enable, DeleteTime FROM cert_cert_stage ORDER BY Id;
