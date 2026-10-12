-- ============================================================================
-- 2026-10-12 · 跨端共享层收敛 阶段 1.A 配套：与铁律冲突 / 损坏引用的数据清理
--
-- 依据（用户 2026-10-12 原话）：
--   「如果发现执行的时候，这些数据和我们的逻辑有异常，可以彻底删除，
--     最后执行完成后，给我一个清单，我会重新补充数据」
--
-- 处置范围（只清「明确损坏」与「明确测试残留」，共 13 行）：
--   ① sys_organization 内部孤儿：企业节点的 OrgCode 指向已不存在的
--      cert_enterprise.Code ⇒ 2 行
--   ② 测试认证机构「xxx测试认证机构」(JG001) 及其全套重复配置 ⇒ 11 行
--
-- ⛔ 本脚本**不**触碰（见交付清单「待确认」段）：
--   · 40 行已软删机构垃圾（IsDeleted=1，功能上已不可见，删除仅美化）
--   · 7 个测试用户（指向上述软删机构）
--   · 40 行已软删核心数据（11 标准 / 20 章节 / 9 参数 —— 有意软删，不恢复）
--   · 4 个测试企业节点（「G4测试企业甲」持有 105 个原始文件，不可删）
--   · cert_doc_extraction_rule c8c0e278-…（实体注释明载为机构级；被 95 行结果引用）
--
-- 幂等：可重复执行（DELETE 带精确键；二次执行影响 0 行）
-- ============================================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ────────────────────────────────────────────────────────────────────────────
-- ① 孤儿企业节点（OrgCode 指向不存在的 cert_enterprise.Code）
--    5a60ade32ece4c269423924da2d897de 「4334」   → bfa7bd00cc4b4b9c96c8a708313574ab（无）
--    19f1d0c56b3a4eb3842ae86d14098a8d 「434334」 → 41e2241202724ba990f26cdfc8460849（无）
-- ────────────────────────────────────────────────────────────────────────────
DELETE FROM sys_organization
 WHERE Code IN ('5a60ade32ece4c269423924da2d897de',
                '19f1d0c56b3a4eb3842ae86d14098a8d');

-- ────────────────────────────────────────────────────────────────────────────
-- ② 测试认证机构「xxx测试认证机构」(Code=1579641b…, OrgCode=JG001) 及其全套配置
--    实测引用面（全表扫描）：cert_certification_body 1 / cert_org_standard 1 /
--      cert_org_stage 3 / cert_standard_directory_config 3 /
--      cert_standard_directory_file 1（经 ConfigCode） / cert_report_section 1
--    其配置与「河北雄安尚龙认证有限公司」对同一标准重复 ⇒ 纯测试残留
-- ────────────────────────────────────────────────────────────────────────────
-- ②.1 目录子项（经 ConfigCode → 3 个 ConfigCode）
DELETE FROM cert_standard_directory_file
 WHERE ConfigCode IN ('34cbb75fb61145cb92fc91426260d17f',
                      'cf6f1034add745b491bc9a8ce21d516c',
                      'b7ade90e87bd4679bb58e7145d183569');
-- ②.2 目录配置主表
DELETE FROM cert_standard_directory_config
 WHERE OrgCode = '1579641b3d61498bba78dcbc959bd4ba';
-- ②.3 机构×标准 / 机构×阶段
DELETE FROM cert_org_standard WHERE OrgCode = '1579641b3d61498bba78dcbc959bd4ba';
DELETE FROM cert_org_stage    WHERE OrgCode = '1579641b3d61498bba78dcbc959bd4ba';
-- ②.4 报告章节规则（该机构专属 1 行）
DELETE FROM cert_report_section WHERE OrgCode = '1579641b3d61498bba78dcbc959bd4ba';
-- ②.5 认证机构主记录（与 sys_organization 1:1 同步表）
DELETE FROM cert_certification_body WHERE Code = '1579641b3d61498bba78dcbc959bd4ba'
   OR OrgCode = '1579641b3d61498bba78dcbc959bd4ba';
-- ②.6 机构行本体
DELETE FROM sys_organization WHERE Code = '1579641b3d61498bba78dcbc959bd4ba';

-- ────────────────────────────────────────────────────────────────────────────
-- ③ 体检（执行后自证 —— 取消注释单独跑，必须全部返回 0）
-- ────────────────────────────────────────────────────────────────────────────
-- SELECT '孤儿企业节点' t, COUNT(*) c FROM sys_organization o
--   WHERE o.OrgType='Enterprise' AND o.IsDeleted=0
--     AND NOT EXISTS (SELECT 1 FROM cert_enterprise e WHERE e.Code = o.OrgCode)
-- UNION ALL SELECT '测试机构残留', COUNT(*) FROM sys_organization WHERE Code='1579641b3d61498bba78dcbc959bd4ba'
-- UNION ALL SELECT '测试机构配置残留', (
--     (SELECT COUNT(*) FROM cert_org_standard WHERE OrgCode='1579641b3d61498bba78dcbc959bd4ba') +
--     (SELECT COUNT(*) FROM cert_org_stage    WHERE OrgCode='1579641b3d61498bba78dcbc959bd4ba') +
--     (SELECT COUNT(*) FROM cert_standard_directory_config WHERE OrgCode='1579641b3d61498bba78dcbc959bd4ba') +
--     (SELECT COUNT(*) FROM cert_report_section WHERE OrgCode='1579641b3d61498bba78dcbc959bd4ba') +
--     (SELECT COUNT(*) FROM cert_certification_body WHERE Code='1579641b3d61498bba78dcbc959bd4ba')
-- );

-- ============================================================================
-- 反向（回滚）—— ⛔ 数据已物理删除，仅能从备份恢复
--   mysqldump -uroot -p yzh_cert_platform sys_organization cert_certification_body \
--     cert_org_standard cert_org_stage cert_standard_directory_config \
--     cert_standard_directory_file cert_report_section > backup_20261012.sql
-- ============================================================================
