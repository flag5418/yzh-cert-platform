-- =====================================================================================
-- 20261026_gbt_standard_clause_tree_seed_V1.sql
-- 国标体系认证标准 + 分层条款树（Q/E/H + 13485 四标准）
--   目标表：cert_standard_family / cert_iso_standard / cert_iso_clause
--   来源：docs/90-归档/案例资料/ 下 4 份国标 PDF（19001 / 24001 / 45001 / 42061）
--
--   ★ 用户裁决（2026-10-26）：
--     ① 条款结构 = 国标官方层级（4→4.1→4.2→4.2.1…），对齐真实审核检查表的条款引用口径
--        （案例：Documents except for the above parts(4)-邝敏威.docx 使用 4.1 / Q8.5.1 /
--         E6.1.2 / H 6.1.2.1 / E&H8.2 这类「标准前缀 + 官方编号」写法）
--     ② 条款内容（Description）无业务意义 ⇒ 全部置 NULL，不写入原文
--     ③ 重建 iso13485 条款树：清除原「质量手册派生」条款（0.x 手册章节、部门职责、
--        XASL-QP-xxx 程序清单等企业专属节点），改为 GB/T 42061-2022 官方结构
--     ④ 只收第 1~10 章规范性条款；资料性附录（附录A/B/NA）不建节点；
--        第 3 章术语和定义不展开（术语分组不可审核）
--
--   ★ 技术要点：
--     - Code = MD5('<标准slug>|<条款编号>')，32 hex，确定性幂等
--     - ParentCode = MD5('<标准slug>|<父条款编号>')，根节点 NULL
--     - Sort 按 DFS 顺序 ×10
--     - cert_iso_clause 无 (StandardCode,ClauseNumber) 唯一索引，唯一性由应用层保证
--
--   执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. \
--            --default-character-set=utf8mb4 yzh_cert_platform \
--            < scripts/db/20261026_gbt_standard_clause_tree_seed_V1.sql
-- =====================================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- ① 标准族（补 iso14000 / iso45000；iso9000、iso13485 已存在） ----------
INSERT IGNORE INTO cert_standard_family
  (Code, FamilyNo, FamilyName, Category, Description, Sort, IsValid, IsDeleted, CreateTime)
VALUES
  ('f1a4d2c8-3b7e-4c15-9a62-8d0e5b1f7c30', 'iso14000', 'ISO 14000 环境管理体系族', 'environment', '按国标条款树建立', 20, 1, 0, NOW()),
  ('f4b8e6d1-7c92-4e38-b5a1-6f2d9c4e8a17', 'iso45000', 'ISO 45000 职业健康安全管理体系族', 'safety', '按国标条款树建立', 30, 1, 0, NOW());

-- ---------- ② 标准（iso13485 为既有行，只更新描述；其余三条新建） ----------
INSERT IGNORE INTO cert_iso_standard
  (Code, StandardCode, StandardName, VersionYear, Category, FamilyCode, Description,
   Sort, IsValid, IsDeleted, ParentCode, IsLeaf, CreateTime)
VALUES
  ('c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'iso9001', 'GB/T 19001-2016 质量管理体系 要求', 2016, 'quality', '64df1dbd-c2ef-11f1-be9a-3e60ac2dc9d1',
   '等同采用 ISO 9001:2015（IDT）。条款树取自 GB/T 19001-2016 正文标题（含目次漏印的 7.1.5.1/7.1.5.2）。', 10, 1, 0, NULL, 1, NOW()),
  ('d2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'iso14001', 'GB/T 24001-2016 环境管理体系 要求及使用指南', 2016, 'environment', 'f1a4d2c8-3b7e-4c15-9a62-8d0e5b1f7c30',
   '等同采用 ISO 14001:2015（IDT）。条款树取自 GB/T 24001-2016 正文标题（第 4~10 章，不含资料性附录）。', 10, 1, 0, NULL, 1, NOW()),
  ('e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'iso45001', 'GB/T 45001-2020 职业健康安全管理体系 要求及使用指南', 2020, 'safety', 'f4b8e6d1-7c92-4e38-b5a1-6f2d9c4e8a17',
   '等同采用 ISO 45001:2018（IDT）。条款树取自 GB/T 45001-2020 正文标题，含 6.1.2.1~6.1.2.3、8.1.4.1~8.1.4.3。', 10, 1, 0, NULL, 1, NOW()),
  ('dfca5483-f83c-4999-b773-3624de5f7cf7', 'iso13485', 'ISO 13485 医疗器械质量管理体系', 2016, 'medical', 'ad35df13-eb00-4741-9600-413122d0be22',
   'GB/T 42061-2022 等同采用 ISO 13485:2016（IDT）。2026-10-26 按国标官方层级重建条款树（原手册派生条款已清除）。', 10, 1, 0, NULL, 1, NOW());

-- iso13485 标准名/描述对齐国标来源（Code 不变，族关联不变）
UPDATE cert_iso_standard SET Description = 'GB/T 42061-2022 等同采用 ISO 13485:2016（IDT）。2026-10-26 按国标官方层级重建条款树（原手册派生条款已清除）。', UpdateTime = NOW()
 WHERE Code = 'dfca5483-f83c-4999-b773-3624de5f7cf7';

-- ---------- ③ 存量引用改挂（必须先于条款清理；FK fk_valrule_clause 指向 cert_iso_clause.Code） ----------
-- 说明：cert_validation_rule.ClauseCode / cert_expert_nc_item.ClauseCode 为 NOT NULL 且带外键，
--      旧节点 0.1（质量手册颁布令）/ 0.5（企业组织结构图）在新树中不存在，按语义就近改挂：
--      0.1 质量手册颁布令  → 4.2.2 质量手册
--      0.5 企业组织结构图  → 5.5.1 职责和权限
UPDATE cert_validation_rule SET ClauseCode = '9b67dfd6c25879d49f6a781669003837', UpdateTime = NOW()
 WHERE Code = 'ac70e048dc3a46af94842f1703110424';   -- 测试规则001（原 0.1）
UPDATE cert_validation_rule SET ClauseCode = '9ac7a32f1ebd7433ca7b325f032ee8de', UpdateTime = NOW()
 WHERE Code = '9b9739d3fbe14600be2b99f66c71e165';   -- 4444（原 0.5）
UPDATE cert_expert_nc_item
   SET ClauseCode = '9b67dfd6c25879d49f6a781669003837', ClauseNumber = '4.2.2', ClauseTitle = '质量手册'
 WHERE Code = 'eb2821280f1046fe8819bf5fd5feb945';   -- 原 0.1

-- ---------- ④ 条款树：GB/T 19001-2016 质量管理体系 要求（78 条） ----------
-- Code = MD5('<slug>|<条款编号>') 确定性幂等；ON DUPLICATE KEY 保证复跑时标题/层级/排序被纠正，
-- Description 恒置 NULL（用户裁决：条款内容无业务意义）
INSERT INTO cert_iso_clause
  (Code, CreateTime, IsDeleted, IsValid, StandardCode, ParentCode,
   ClauseNumber, Title, Description, Sort)
VALUES
  ('de781187cfcf3efa523701da42b21e41', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '1', '范围', NULL, 10),
  ('c68842a9868e250691cb54858c7ec91d', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '2', '规范性引用文件', NULL, 20),
  ('dac6f1f8885eda9cab398b4dc9ae2b1e', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '3', '术语和定义', NULL, 30),
  ('09c42d84614250213eecf332650400e1', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '4', '组织环境', NULL, 40),
  ('27a42d04937fb76d4b5080b10c2f8838', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '09c42d84614250213eecf332650400e1', '4.1', '理解组织及其环境', NULL, 50),
  ('9186fa5e7f3049b8b770b9391bc9b527', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '09c42d84614250213eecf332650400e1', '4.2', '理解相关方的需求和期望', NULL, 60),
  ('054fc0a8a287a684cb77330c06980474', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '09c42d84614250213eecf332650400e1', '4.3', '确定质量管理体系的范围', NULL, 70),
  ('018e28c4466f08d9be63fa18e70fa52b', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '09c42d84614250213eecf332650400e1', '4.4', '质量管理体系及其过程', NULL, 80),
  ('3aac078111d876e5e77fc9eda841406e', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '5', '领导作用', NULL, 90),
  ('57c67c1b0e65ae5b8a2786338c01ce28', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '3aac078111d876e5e77fc9eda841406e', '5.1', '领导作用和承诺', NULL, 100),
  ('f331dc605ad475bdbfdc55564ff5b0b4', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '57c67c1b0e65ae5b8a2786338c01ce28', '5.1.1', '总则', NULL, 110),
  ('cc5fbc0e2563ef748fbbd071974bc7bc', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '57c67c1b0e65ae5b8a2786338c01ce28', '5.1.2', '以顾客为关注焦点', NULL, 120),
  ('16c6bfbe6eaffc1435f24831de86bd45', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '3aac078111d876e5e77fc9eda841406e', '5.2', '方针', NULL, 130),
  ('a5ff6401e418e0229407cf101329038e', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '16c6bfbe6eaffc1435f24831de86bd45', '5.2.1', '制定质量方针', NULL, 140),
  ('5c6e2db6cd483ea544fe75684d240e17', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '16c6bfbe6eaffc1435f24831de86bd45', '5.2.2', '沟通质量方针', NULL, 150),
  ('754f0138e6313822cbc3af1bf47225b5', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '3aac078111d876e5e77fc9eda841406e', '5.3', '组织的岗位、职责和权限', NULL, 160),
  ('359b52ba50732bdde976c2cc829aecbd', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '6', '策划', NULL, 170),
  ('a188398722711beab9e0e22626d18460', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '359b52ba50732bdde976c2cc829aecbd', '6.1', '应对风险和机遇的措施', NULL, 180),
  ('d7a2f72fb4e9fff616b9be8d1d49966d', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '359b52ba50732bdde976c2cc829aecbd', '6.2', '质量目标及其实现的策划', NULL, 190),
  ('4f5cfd278e12c6e2f36112c51b9a0ecf', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '359b52ba50732bdde976c2cc829aecbd', '6.3', '变更的策划', NULL, 200),
  ('65881d535148e08b4705cea08ae5c022', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '7', '支持', NULL, 210),
  ('c73a801e391e1538e260c27ad3ce8712', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '65881d535148e08b4705cea08ae5c022', '7.1', '资源', NULL, 220),
  ('eb51dc7ad5184d588be9e28f8fc3568d', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'c73a801e391e1538e260c27ad3ce8712', '7.1.1', '总则', NULL, 230),
  ('0a2a5b6695517ad29c6ad3f4fcce561a', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'c73a801e391e1538e260c27ad3ce8712', '7.1.2', '人员', NULL, 240),
  ('d0bd60095002d38ac50b4d7cfd5708c5', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'c73a801e391e1538e260c27ad3ce8712', '7.1.3', '基础设施', NULL, 250),
  ('c0e5312fe92b9c472c484c6901041953', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'c73a801e391e1538e260c27ad3ce8712', '7.1.4', '过程运行环境', NULL, 260),
  ('d698c4ece19d60dbd165fb6dc53d3ec2', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'c73a801e391e1538e260c27ad3ce8712', '7.1.5', '监视和测量资源', NULL, 270),
  ('d152286e1d432aa18ee88f4c597b67f6', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'd698c4ece19d60dbd165fb6dc53d3ec2', '7.1.5.1', '总则', NULL, 280),
  ('dfeddcca6f3632f91b67b2bbd9165a94', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'd698c4ece19d60dbd165fb6dc53d3ec2', '7.1.5.2', '测量溯源', NULL, 290),
  ('ee731ddb2bb05ecc2c396eef38bb4906', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'c73a801e391e1538e260c27ad3ce8712', '7.1.6', '组织的知识', NULL, 300),
  ('917abfd48bf7719d4e3c2b3ef37c7d9c', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '65881d535148e08b4705cea08ae5c022', '7.2', '能力', NULL, 310),
  ('ad4d520c32206137ea1eb98de756f6e3', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '65881d535148e08b4705cea08ae5c022', '7.3', '意识', NULL, 320),
  ('0c2c9f56400160fa0187924694747bda', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '65881d535148e08b4705cea08ae5c022', '7.4', '沟通', NULL, 330),
  ('417546bd9e08f3b0111ce11f2fe2543c', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '65881d535148e08b4705cea08ae5c022', '7.5', '成文信息', NULL, 340),
  ('f4077349075697812736a668d766e30a', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '417546bd9e08f3b0111ce11f2fe2543c', '7.5.1', '总则', NULL, 350),
  ('0386aea13031705006a95f5416554320', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '417546bd9e08f3b0111ce11f2fe2543c', '7.5.2', '创建和更新', NULL, 360),
  ('1e75441643ffe1813ae8abf0e9a63d82', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '417546bd9e08f3b0111ce11f2fe2543c', '7.5.3', '成文信息的控制', NULL, 370),
  ('877af64f41e467a9ba25d4b63a3f7ea6', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '8', '运行', NULL, 380),
  ('78071aaf65017da134aeb7d68e141ffd', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '877af64f41e467a9ba25d4b63a3f7ea6', '8.1', '运行的策划和控制', NULL, 390),
  ('300426eb6648cf4d7b6771eba4111993', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '877af64f41e467a9ba25d4b63a3f7ea6', '8.2', '产品和服务的要求', NULL, 400),
  ('d072cbafe47d34359a48c09b87b93fce', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '300426eb6648cf4d7b6771eba4111993', '8.2.1', '顾客沟通', NULL, 410),
  ('12eb7d74248a6b5df3566d93f442f931', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '300426eb6648cf4d7b6771eba4111993', '8.2.2', '产品和服务要求的确定', NULL, 420),
  ('3d5bd5bd1c4e29a3b06dca498f49d2d2', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '300426eb6648cf4d7b6771eba4111993', '8.2.3', '产品和服务要求的评审', NULL, 430),
  ('0b146bb760f49d0dbe231f5a04cff8ac', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '300426eb6648cf4d7b6771eba4111993', '8.2.4', '产品和服务要求的更改', NULL, 440),
  ('6d9eddf74ed4c780a28d12cfde6f94a5', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '877af64f41e467a9ba25d4b63a3f7ea6', '8.3', '产品和服务的设计和开发', NULL, 450),
  ('c743810bbb6624565776882565093cf8', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '6d9eddf74ed4c780a28d12cfde6f94a5', '8.3.1', '总则', NULL, 460),
  ('47354d974f0c1cac1a71a2310c172b7c', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '6d9eddf74ed4c780a28d12cfde6f94a5', '8.3.2', '设计和开发策划', NULL, 470),
  ('ad49094fb150946b94155db889f2c67e', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '6d9eddf74ed4c780a28d12cfde6f94a5', '8.3.3', '设计和开发输入', NULL, 480),
  ('0f46fb5d3e7ef3268461013fd8b98ea5', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '6d9eddf74ed4c780a28d12cfde6f94a5', '8.3.4', '设计和开发控制', NULL, 490),
  ('3accf3714ee5feaff1019d6ce92bf371', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '6d9eddf74ed4c780a28d12cfde6f94a5', '8.3.5', '设计和开发输出', NULL, 500),
  ('aa666ae254ece15d7e8881be3f9ded36', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '6d9eddf74ed4c780a28d12cfde6f94a5', '8.3.6', '设计和开发更改', NULL, 510),
  ('074556572246bebab55756eb0fd38d48', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '877af64f41e467a9ba25d4b63a3f7ea6', '8.4', '外部提供的过程、产品和服务的控制', NULL, 520),
  ('56e6469fa70bd57835da0ad37c03b256', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '074556572246bebab55756eb0fd38d48', '8.4.1', '总则', NULL, 530),
  ('fb3eb917de4a1a6148732e97790acb4f', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '074556572246bebab55756eb0fd38d48', '8.4.2', '控制类型和程度', NULL, 540),
  ('1d6ad0edf35ed5c8305cf8227319b2d7', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '074556572246bebab55756eb0fd38d48', '8.4.3', '提供给外部供方的信息', NULL, 550),
  ('89436155c5e1324ade78b7d997aea7fd', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '877af64f41e467a9ba25d4b63a3f7ea6', '8.5', '生产和服务提供', NULL, 560),
  ('391369badbfffd5d1ebd4ff0665812ef', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '89436155c5e1324ade78b7d997aea7fd', '8.5.1', '生产和服务提供的控制', NULL, 570),
  ('8e777ede0fb5daae18988394ce43fbbf', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '89436155c5e1324ade78b7d997aea7fd', '8.5.2', '标识和可追溯性', NULL, 580),
  ('4e28d4324f99af6a70257ca8b04694ba', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '89436155c5e1324ade78b7d997aea7fd', '8.5.3', '顾客或外部供方的财产', NULL, 590),
  ('b0a8aedb86841ac6a6923719dc753b54', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '89436155c5e1324ade78b7d997aea7fd', '8.5.4', '防护', NULL, 600),
  ('9673399b400bd2ae22b42085cc3a7e86', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '89436155c5e1324ade78b7d997aea7fd', '8.5.5', '交付后活动', NULL, 610),
  ('72834cf0b3620657424e6c88a3ab195a', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '89436155c5e1324ade78b7d997aea7fd', '8.5.6', '更改控制', NULL, 620),
  ('5ccabe8a5267957d7449bc91db050fd6', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '877af64f41e467a9ba25d4b63a3f7ea6', '8.6', '产品和服务的放行', NULL, 630),
  ('a8d1d383fd6ae3d3ded3357deec0210f', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '877af64f41e467a9ba25d4b63a3f7ea6', '8.7', '不合格输出的控制', NULL, 640),
  ('2843b8d68df7fdd01a970c54629337b3', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '9', '绩效评价', NULL, 650),
  ('357beb125ddcde99474ce4b01e18b743', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '2843b8d68df7fdd01a970c54629337b3', '9.1', '监视、测量、分析和评价', NULL, 660),
  ('7e2b39f658905f636464595b37ad5f12', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '357beb125ddcde99474ce4b01e18b743', '9.1.1', '总则', NULL, 670),
  ('b5c7c925e2fc5ca88c06e228346b988e', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '357beb125ddcde99474ce4b01e18b743', '9.1.2', '顾客满意', NULL, 680),
  ('c39a1f5a5d07fb6c1328f8518710f895', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '357beb125ddcde99474ce4b01e18b743', '9.1.3', '分析与评价', NULL, 690),
  ('63f92be67d82d69feb02604d882a249c', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '2843b8d68df7fdd01a970c54629337b3', '9.2', '内部审核', NULL, 700),
  ('a78cb1a495d77ce1f6927fb7491eaa75', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', '2843b8d68df7fdd01a970c54629337b3', '9.3', '管理评审', NULL, 710),
  ('2608c522c56ca10545b34495b0ed43df', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'a78cb1a495d77ce1f6927fb7491eaa75', '9.3.1', '总则', NULL, 720),
  ('aee7b8397deb17c3932c2499efca8cfb', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'a78cb1a495d77ce1f6927fb7491eaa75', '9.3.2', '管理评审输入', NULL, 730),
  ('db70cf6b88d7f4f2022cb23e22625956', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'a78cb1a495d77ce1f6927fb7491eaa75', '9.3.3', '管理评审输出', NULL, 740),
  ('d22363649d8e1739709a8e1e5dd640da', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', NULL, '10', '改进', NULL, 750),
  ('d54f1b0132e2bda9bd2e0450a35c82b1', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'd22363649d8e1739709a8e1e5dd640da', '10.1', '总则', NULL, 760),
  ('43d90137867cdb298fe88a0ba02bc266', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'd22363649d8e1739709a8e1e5dd640da', '10.2', '不合格和纠正措施', NULL, 770),
  ('3b2655bb73fc76c04e7af9ae5e5dba4a', NOW(), 0, 1, 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62', 'd22363649d8e1739709a8e1e5dd640da', '10.3', '持续改进', NULL, 780)
ON DUPLICATE KEY UPDATE Title = VALUES(Title), ParentCode = VALUES(ParentCode),
                        Sort = VALUES(Sort), Description = NULL, IsDeleted = 0, IsValid = 1;
DELETE FROM cert_iso_clause WHERE StandardCode = 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62'
   AND ClauseNumber NOT IN ('1', '2', '3', '4', '4.1', '4.2', '4.3', '4.4', '5', '5.1', '5.1.1', '5.1.2', '5.2', '5.2.1', '5.2.2', '5.3', '6', '6.1', '6.2', '6.3', '7', '7.1', '7.1.1', '7.1.2', '7.1.3', '7.1.4', '7.1.5', '7.1.5.1', '7.1.5.2', '7.1.6', '7.2', '7.3', '7.4', '7.5', '7.5.1', '7.5.2', '7.5.3', '8', '8.1', '8.2', '8.2.1', '8.2.2', '8.2.3', '8.2.4', '8.3', '8.3.1', '8.3.2', '8.3.3', '8.3.4', '8.3.5', '8.3.6', '8.4', '8.4.1', '8.4.2', '8.4.3', '8.5', '8.5.1', '8.5.2', '8.5.3', '8.5.4', '8.5.5', '8.5.6', '8.6', '8.7', '9', '9.1', '9.1.1', '9.1.2', '9.1.3', '9.2', '9.3', '9.3.1', '9.3.2', '9.3.3', '10', '10.1', '10.2', '10.3');

-- ---------- ④ 条款树：GB/T 24001-2016 环境管理体系 要求及使用指南（48 条） ----------
-- Code = MD5('<slug>|<条款编号>') 确定性幂等；ON DUPLICATE KEY 保证复跑时标题/层级/排序被纠正，
-- Description 恒置 NULL（用户裁决：条款内容无业务意义）
INSERT INTO cert_iso_clause
  (Code, CreateTime, IsDeleted, IsValid, StandardCode, ParentCode,
   ClauseNumber, Title, Description, Sort)
VALUES
  ('ce7b36c725834e7e754ba7c6ee1b2ad5', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '1', '范围', NULL, 10),
  ('000ec473101d1d7835da2ae4905cc924', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '2', '规范性引用文件', NULL, 20),
  ('758745ff903ae0c13490ad06627ef8c9', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '3', '术语和定义', NULL, 30),
  ('d81bad7cdf773e735ecf613557f8571c', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '4', '组织所处的环境', NULL, 40),
  ('68436e116597f2cc469144cfae3f675f', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'd81bad7cdf773e735ecf613557f8571c', '4.1', '理解组织及其所处的环境', NULL, 50),
  ('1df7633d43576485567e9cfebfa36fe0', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'd81bad7cdf773e735ecf613557f8571c', '4.2', '理解相关方的需求和期望', NULL, 60),
  ('065370a96335ab2a5dc7641a909ad3cd', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'd81bad7cdf773e735ecf613557f8571c', '4.3', '确定环境管理体系的范围', NULL, 70),
  ('e91eb0a635dddda56a338a4f9f59619c', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'd81bad7cdf773e735ecf613557f8571c', '4.4', '环境管理体系', NULL, 80),
  ('9a852086b4fb91d56d4c49f680e728d3', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '5', '领导作用', NULL, 90),
  ('b91b886e2ac153e2316eb8db15e2f8d8', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '9a852086b4fb91d56d4c49f680e728d3', '5.1', '领导作用与承诺', NULL, 100),
  ('9808328c7661e32c805f8f129b786835', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '9a852086b4fb91d56d4c49f680e728d3', '5.2', '环境方针', NULL, 110),
  ('8049ffb61287595c01aeadcf8e6361fb', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '9a852086b4fb91d56d4c49f680e728d3', '5.3', '组织的角色、职责和权限', NULL, 120),
  ('a6f3701349346593ac1a3ec3aebd7e1a', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '6', '策划', NULL, 130),
  ('869ce6b2b916120bb0bc7073518acbba', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'a6f3701349346593ac1a3ec3aebd7e1a', '6.1', '应对风险和机遇的措施', NULL, 140),
  ('7e63ec6414467765dcca88f0627bbcbf', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '869ce6b2b916120bb0bc7073518acbba', '6.1.1', '总则', NULL, 150),
  ('c30526cd659b7bde2b2c78165022c919', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '869ce6b2b916120bb0bc7073518acbba', '6.1.2', '环境因素', NULL, 160),
  ('a9189568792a0b411fafad6f4abb8d0e', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '869ce6b2b916120bb0bc7073518acbba', '6.1.3', '合规义务', NULL, 170),
  ('0b95e8109b6d524f53917f8c2bd51ee3', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '869ce6b2b916120bb0bc7073518acbba', '6.1.4', '措施的策划', NULL, 180),
  ('cf00a090c5783a375cba3d2b81d9ebda', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'a6f3701349346593ac1a3ec3aebd7e1a', '6.2', '环境目标及其实现的策划', NULL, 190),
  ('8dfe234e05df27c24b792c840b0a3028', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'cf00a090c5783a375cba3d2b81d9ebda', '6.2.1', '环境目标', NULL, 200),
  ('0434127b88a0ead45c24489e17837351', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'cf00a090c5783a375cba3d2b81d9ebda', '6.2.2', '实现环境目标的措施的策划', NULL, 210),
  ('e78f79742c49f41a378a8cd8b6a19c08', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '7', '支持', NULL, 220),
  ('07902a7b8c64bdcc4906544ff62ddd8b', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'e78f79742c49f41a378a8cd8b6a19c08', '7.1', '资源', NULL, 230),
  ('acc3d017ff4aab34be59524e8a7424d4', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'e78f79742c49f41a378a8cd8b6a19c08', '7.2', '能力', NULL, 240),
  ('c3ef4ab295c7441b65eb017f8b391a9a', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'e78f79742c49f41a378a8cd8b6a19c08', '7.3', '意识', NULL, 250),
  ('f0b3192231c3f32dfa8a11b761c13bc7', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'e78f79742c49f41a378a8cd8b6a19c08', '7.4', '信息交流', NULL, 260),
  ('100e144703d56b3ad35d354738103e48', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'f0b3192231c3f32dfa8a11b761c13bc7', '7.4.1', '总则', NULL, 270),
  ('a506f584d3ad9dc77adccf8eb68c2d44', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'f0b3192231c3f32dfa8a11b761c13bc7', '7.4.2', '内部信息交流', NULL, 280),
  ('acb2a538f8c747fdc752be0478edac2a', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'f0b3192231c3f32dfa8a11b761c13bc7', '7.4.3', '外部信息交流', NULL, 290),
  ('f294bca2eee49edd47276e3565bfd08e', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'e78f79742c49f41a378a8cd8b6a19c08', '7.5', '文件化信息', NULL, 300),
  ('9f9b24ad99781a6532294e44a09d8f26', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'f294bca2eee49edd47276e3565bfd08e', '7.5.1', '总则', NULL, 310),
  ('540d1ef8edbfc24d4c95c494456bdc8a', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'f294bca2eee49edd47276e3565bfd08e', '7.5.2', '创建和更新', NULL, 320),
  ('41c86ec85fdeb56bc9dcb1cb2721e45a', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'f294bca2eee49edd47276e3565bfd08e', '7.5.3', '文件化信息的控制', NULL, 330),
  ('cc22cf6e44bd0ee801ba58168fca5c1d', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '8', '运行', NULL, 340),
  ('91a8113c5fb06dedfaebf766177158bd', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'cc22cf6e44bd0ee801ba58168fca5c1d', '8.1', '运行策划和控制', NULL, 350),
  ('4d82a5d48f13060baaaafe54a44cbc75', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'cc22cf6e44bd0ee801ba58168fca5c1d', '8.2', '应急准备和响应', NULL, 360),
  ('7b2fd4f761c50eda938371dce222c41d', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '9', '绩效评价', NULL, 370),
  ('0993ca82d7ea856d6231325907d0fbcf', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '7b2fd4f761c50eda938371dce222c41d', '9.1', '监视、测量、分析和评价', NULL, 380),
  ('9f0f6f2885b7e324e1c1ccd2a2f5b1e7', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '0993ca82d7ea856d6231325907d0fbcf', '9.1.1', '总则', NULL, 390),
  ('742acea093a61a249114b9575a2aa72d', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '0993ca82d7ea856d6231325907d0fbcf', '9.1.2', '合规性评价', NULL, 400),
  ('1053f496cf6ce7af5821d139ff7ff9b2', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '7b2fd4f761c50eda938371dce222c41d', '9.2', '内部审核', NULL, 410),
  ('212f4a708acf3eccde2e95c07c40f47d', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '1053f496cf6ce7af5821d139ff7ff9b2', '9.2.1', '总则', NULL, 420),
  ('5f17b52409c603726fa8da136f21c87d', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '1053f496cf6ce7af5821d139ff7ff9b2', '9.2.2', '内部审核方案', NULL, 430),
  ('854f438adf1628638852fbe0729378c6', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', '7b2fd4f761c50eda938371dce222c41d', '9.3', '管理评审', NULL, 440),
  ('e01f90bd02343717b7ee03ee08ab289c', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', NULL, '10', '改进', NULL, 450),
  ('5473628dd7ff9ca593a18ba1dddcfb62', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'e01f90bd02343717b7ee03ee08ab289c', '10.1', '总则', NULL, 460),
  ('15068a4c9a8cb004fd2bbb0173dea2ab', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'e01f90bd02343717b7ee03ee08ab289c', '10.2', '不符合和纠正措施', NULL, 470),
  ('d750bd9ad3598670490dc34da4a9cf0c', NOW(), 0, 1, 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46', 'e01f90bd02343717b7ee03ee08ab289c', '10.3', '持续改进', NULL, 480)
ON DUPLICATE KEY UPDATE Title = VALUES(Title), ParentCode = VALUES(ParentCode),
                        Sort = VALUES(Sort), Description = NULL, IsDeleted = 0, IsValid = 1;
DELETE FROM cert_iso_clause WHERE StandardCode = 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46'
   AND ClauseNumber NOT IN ('1', '2', '3', '4', '4.1', '4.2', '4.3', '4.4', '5', '5.1', '5.2', '5.3', '6', '6.1', '6.1.1', '6.1.2', '6.1.3', '6.1.4', '6.2', '6.2.1', '6.2.2', '7', '7.1', '7.2', '7.3', '7.4', '7.4.1', '7.4.2', '7.4.3', '7.5', '7.5.1', '7.5.2', '7.5.3', '8', '8.1', '8.2', '9', '9.1', '9.1.1', '9.1.2', '9.2', '9.2.1', '9.2.2', '9.3', '10', '10.1', '10.2', '10.3');

-- ---------- ④ 条款树：GB/T 45001-2020 职业健康安全管理体系 要求及使用指南（59 条） ----------
-- Code = MD5('<slug>|<条款编号>') 确定性幂等；ON DUPLICATE KEY 保证复跑时标题/层级/排序被纠正，
-- Description 恒置 NULL（用户裁决：条款内容无业务意义）
INSERT INTO cert_iso_clause
  (Code, CreateTime, IsDeleted, IsValid, StandardCode, ParentCode,
   ClauseNumber, Title, Description, Sort)
VALUES
  ('b43ae402948831ea2f82a6c4c8595430', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '1', '范围', NULL, 10),
  ('4df602398d9311d3e6f738491bdc47c9', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '2', '规范性引用文件', NULL, 20),
  ('2290aaf56ad421b538ccbbca1ec57509', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '3', '术语和定义', NULL, 30),
  ('5005c197b3e5d44fbf960039af3e6d2f', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '4', '组织所处的环境', NULL, 40),
  ('a09b54322c4d409b92360dbcec6322a8', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '5005c197b3e5d44fbf960039af3e6d2f', '4.1', '理解组织及其所处的环境', NULL, 50),
  ('f50d41fc3b0214c3c42357a21e3d188b', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '5005c197b3e5d44fbf960039af3e6d2f', '4.2', '理解工作人员和其他相关方的需求和期望', NULL, 60),
  ('d5bbea8792241d9638732bfc722f3a3c', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '5005c197b3e5d44fbf960039af3e6d2f', '4.3', '确定职业健康安全管理体系的范围', NULL, 70),
  ('f8787ea854d99a1235331af123a95b0b', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '5005c197b3e5d44fbf960039af3e6d2f', '4.4', '职业健康安全管理体系', NULL, 80),
  ('8045dfd42d9c2c9c82102c2c9df9ce6d', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '5', '领导作用和工作人员参与', NULL, 90),
  ('f7a08c8273dcb6fd8d6794e7153cc151', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '8045dfd42d9c2c9c82102c2c9df9ce6d', '5.1', '领导作用和承诺', NULL, 100),
  ('e21065314b94cdedd777d1ad7546b300', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '8045dfd42d9c2c9c82102c2c9df9ce6d', '5.2', '职业健康安全方针', NULL, 110),
  ('5a440da43a8d4bdda1055dd7c80f27ba', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '8045dfd42d9c2c9c82102c2c9df9ce6d', '5.3', '组织的角色、职责和权限', NULL, 120),
  ('7d9fef26262e8b57ab74cf8d21bf3a2e', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '8045dfd42d9c2c9c82102c2c9df9ce6d', '5.4', '工作人员的协商和参与', NULL, 130),
  ('988475ad278685ce5e5630b000d67311', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '6', '策划', NULL, 140),
  ('e837e3a1b21c62567c0a13d463a66fc0', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '988475ad278685ce5e5630b000d67311', '6.1', '应对风险和机遇的措施', NULL, 150),
  ('92aaf9242e072a95672cb7056a88a961', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'e837e3a1b21c62567c0a13d463a66fc0', '6.1.1', '总则', NULL, 160),
  ('0dead9136521a13ae5dffca88343fd85', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'e837e3a1b21c62567c0a13d463a66fc0', '6.1.2', '危险源辨识及风险和机遇的评价', NULL, 170),
  ('e559de3f1856e29a2dcd7ec2a51b1a44', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '0dead9136521a13ae5dffca88343fd85', '6.1.2.1', '危险源辨识', NULL, 180),
  ('470d571e67a6ab69cb0068692f2a6724', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '0dead9136521a13ae5dffca88343fd85', '6.1.2.2', '职业健康安全风险和职业健康安全管理体系的其他风险的评价', NULL, 190),
  ('cb5d53feb72632ae4647216714348a95', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '0dead9136521a13ae5dffca88343fd85', '6.1.2.3', '职业健康安全机遇和职业健康安全管理体系的其他机遇的评价', NULL, 200),
  ('cca84499d400374388823a6b5281c7e6', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'e837e3a1b21c62567c0a13d463a66fc0', '6.1.3', '法律法规要求和其他要求的确定', NULL, 210),
  ('2cff92b916ae1dc6b058cb4da663953c', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'e837e3a1b21c62567c0a13d463a66fc0', '6.1.4', '措施的策划', NULL, 220),
  ('a8c4f2755b958552b8998919006b0b09', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '988475ad278685ce5e5630b000d67311', '6.2', '职业健康安全目标及其实现的策划', NULL, 230),
  ('c8098a520b1856c85870ea8acd138183', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'a8c4f2755b958552b8998919006b0b09', '6.2.1', '职业健康安全目标', NULL, 240),
  ('ea11a661459e09211bb9d2e485540d41', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'a8c4f2755b958552b8998919006b0b09', '6.2.2', '实现职业健康安全目标的策划', NULL, 250),
  ('de5c764eaa0253329a4a1f4103ec1585', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '7', '支持', NULL, 260),
  ('8e488ed688a7b70c4dadaefedc0baa68', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'de5c764eaa0253329a4a1f4103ec1585', '7.1', '资源', NULL, 270),
  ('08001c3f6b7c1234182d618cbe6287b9', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'de5c764eaa0253329a4a1f4103ec1585', '7.2', '能力', NULL, 280),
  ('15249c1dba247effe683a5be8e5cdc98', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'de5c764eaa0253329a4a1f4103ec1585', '7.3', '意识', NULL, 290),
  ('39191d31406be568bec087c452430775', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'de5c764eaa0253329a4a1f4103ec1585', '7.4', '沟通', NULL, 300),
  ('93fef0cc3b3ddb5fc01b86166bd6f06e', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '39191d31406be568bec087c452430775', '7.4.1', '总则', NULL, 310),
  ('0c44addddcf3c65a698448db6d08dad2', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '39191d31406be568bec087c452430775', '7.4.2', '内部沟通', NULL, 320),
  ('7ee2a9242fcaeba456b6aadef0123d4e', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '39191d31406be568bec087c452430775', '7.4.3', '外部沟通', NULL, 330),
  ('0ef342eba9ab38a7cff747ddcab3a558', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'de5c764eaa0253329a4a1f4103ec1585', '7.5', '文件化信息', NULL, 340),
  ('acd77d25c0e0620da6fb8a28dd525d0e', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '0ef342eba9ab38a7cff747ddcab3a558', '7.5.1', '总则', NULL, 350),
  ('22ccb0de53605fafe9d29c7789aeebb6', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '0ef342eba9ab38a7cff747ddcab3a558', '7.5.2', '创建和更新', NULL, 360),
  ('c5e88f31fddcc99ca3d8a01a07e7507d', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '0ef342eba9ab38a7cff747ddcab3a558', '7.5.3', '文件化信息的控制', NULL, 370),
  ('8345041540a8376de6ec06f8d3a054f8', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '8', '运行', NULL, 380),
  ('f316823a4af39028a23eeab9f312265c', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '8345041540a8376de6ec06f8d3a054f8', '8.1', '运行策划和控制', NULL, 390),
  ('92178451fed5352fd958c90a977e6275', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'f316823a4af39028a23eeab9f312265c', '8.1.1', '总则', NULL, 400),
  ('983596358e28540555c58f64343a41fc', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'f316823a4af39028a23eeab9f312265c', '8.1.2', '消除危险源和降低职业健康安全风险', NULL, 410),
  ('b5db5cd1904f2d42aa3152c380a2c60d', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'f316823a4af39028a23eeab9f312265c', '8.1.3', '变更管理', NULL, 420),
  ('1b4daa9ac7f0cda7b1fc8ca256a31f67', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'f316823a4af39028a23eeab9f312265c', '8.1.4', '采购', NULL, 430),
  ('52e350b41a618fb9129265784d042193', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '1b4daa9ac7f0cda7b1fc8ca256a31f67', '8.1.4.1', '总则', NULL, 440),
  ('d36e94c86390d877ef37530c81686b7e', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '1b4daa9ac7f0cda7b1fc8ca256a31f67', '8.1.4.2', '承包方', NULL, 450),
  ('e1dba51b5f6bc11298603b83a0d92e29', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '1b4daa9ac7f0cda7b1fc8ca256a31f67', '8.1.4.3', '外包', NULL, 460),
  ('1209b2a9f5733e11140c3868af9ca58a', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '8345041540a8376de6ec06f8d3a054f8', '8.2', '应急准备和响应', NULL, 470),
  ('572639a601709e7f9b7268b627a27a5c', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '9', '绩效评价', NULL, 480),
  ('457b044d536f29b4fc7df9498137544c', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '572639a601709e7f9b7268b627a27a5c', '9.1', '监视、测量、分析和评价绩效', NULL, 490),
  ('cc94ff89fe8cd450b4346e2168ade7db', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '457b044d536f29b4fc7df9498137544c', '9.1.1', '总则', NULL, 500),
  ('b78ca7ceb7ccefa17c9241a8d335cf3d', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '457b044d536f29b4fc7df9498137544c', '9.1.2', '合规性评价', NULL, 510),
  ('3751bbf428f216ca2605b12b48650718', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '572639a601709e7f9b7268b627a27a5c', '9.2', '内部审核', NULL, 520),
  ('c03b6d09d2b13cc61ca2d4767da18fe5', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '3751bbf428f216ca2605b12b48650718', '9.2.1', '总则', NULL, 530),
  ('9a50620b4fd488a7d619e33f344fcac5', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '3751bbf428f216ca2605b12b48650718', '9.2.2', '内部审核方案', NULL, 540),
  ('30c359929a73a9dccfbe3508b3e68c87', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', '572639a601709e7f9b7268b627a27a5c', '9.3', '管理评审', NULL, 550),
  ('ba0d47a61186f375124cb0f2736419f5', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', NULL, '10', '改进', NULL, 560),
  ('e93e65617b464bba9d9d269f1158ad47', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'ba0d47a61186f375124cb0f2736419f5', '10.1', '总则', NULL, 570),
  ('a8051959e4f4cec26aec3653d5e34cbf', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'ba0d47a61186f375124cb0f2736419f5', '10.2', '事件、不符合和纠正措施', NULL, 580),
  ('8251835d5d4a1af03ebe75522e92c25c', NOW(), 0, 1, 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02', 'ba0d47a61186f375124cb0f2736419f5', '10.3', '持续改进', NULL, 590)
ON DUPLICATE KEY UPDATE Title = VALUES(Title), ParentCode = VALUES(ParentCode),
                        Sort = VALUES(Sort), Description = NULL, IsDeleted = 0, IsValid = 1;
DELETE FROM cert_iso_clause WHERE StandardCode = 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02'
   AND ClauseNumber NOT IN ('1', '2', '3', '4', '4.1', '4.2', '4.3', '4.4', '5', '5.1', '5.2', '5.3', '5.4', '6', '6.1', '6.1.1', '6.1.2', '6.1.2.1', '6.1.2.2', '6.1.2.3', '6.1.3', '6.1.4', '6.2', '6.2.1', '6.2.2', '7', '7.1', '7.2', '7.3', '7.4', '7.4.1', '7.4.2', '7.4.3', '7.5', '7.5.1', '7.5.2', '7.5.3', '8', '8.1', '8.1.1', '8.1.2', '8.1.3', '8.1.4', '8.1.4.1', '8.1.4.2', '8.1.4.3', '8.2', '9', '9.1', '9.1.1', '9.1.2', '9.2', '9.2.1', '9.2.2', '9.3', '10', '10.1', '10.2', '10.3');

-- ---------- ④ 条款树：ISO 13485 医疗器械质量管理体系（88 条） ----------
-- Code = MD5('<slug>|<条款编号>') 确定性幂等；ON DUPLICATE KEY 保证复跑时标题/层级/排序被纠正，
-- Description 恒置 NULL（用户裁决：条款内容无业务意义）
INSERT INTO cert_iso_clause
  (Code, CreateTime, IsDeleted, IsValid, StandardCode, ParentCode,
   ClauseNumber, Title, Description, Sort)
VALUES
  ('680cf5ab6a973ff43967b7764d6a9a37', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', NULL, '1', '范围', NULL, 10),
  ('d20871b40b7c4c9e3ba2fe856a007aaf', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', NULL, '2', '规范性引用文件', NULL, 20),
  ('38329cf53e42b2ab174c50a083d9a02a', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', NULL, '3', '术语和定义', NULL, 30),
  ('472f1fc51d8380ba50716498b06c86bc', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', NULL, '4', '质量管理体系', NULL, 40),
  ('032cd25af0665c138868d939e8a7f2ad', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '472f1fc51d8380ba50716498b06c86bc', '4.1', '总要求', NULL, 50),
  ('d60c7cc65ea9139c6b1457aaf1b285cd', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '472f1fc51d8380ba50716498b06c86bc', '4.2', '文件要求', NULL, 60),
  ('ddb3c1dc786f2939157e6dd6b66c323a', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'd60c7cc65ea9139c6b1457aaf1b285cd', '4.2.1', '总则', NULL, 70),
  ('9b67dfd6c25879d49f6a781669003837', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'd60c7cc65ea9139c6b1457aaf1b285cd', '4.2.2', '质量手册', NULL, 80),
  ('c68d43cd39d3f4b81fca45255e58e5b7', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'd60c7cc65ea9139c6b1457aaf1b285cd', '4.2.3', '医疗器械文档', NULL, 90),
  ('3978fdbda649209ef9931420bde4cf79', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'd60c7cc65ea9139c6b1457aaf1b285cd', '4.2.4', '文件控制', NULL, 100),
  ('3dfdf2e5506fe53e190e5e5447960490', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'd60c7cc65ea9139c6b1457aaf1b285cd', '4.2.5', '记录控制', NULL, 110),
  ('44582734a0cedbb03cad7a2aa1090642', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', NULL, '5', '管理职责', NULL, 120),
  ('9ee3134d366de6e6606ef1a5dcc88d74', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '44582734a0cedbb03cad7a2aa1090642', '5.1', '管理承诺', NULL, 130),
  ('f42ab586ffd78687ec103f5c47ba0f38', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '44582734a0cedbb03cad7a2aa1090642', '5.2', '以顾客为关注焦点', NULL, 140),
  ('bf0605f8dc1ef48feabef29225b053e4', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '44582734a0cedbb03cad7a2aa1090642', '5.3', '质量方针', NULL, 150),
  ('7acc23c5cb93dbbf7958ae322d3b7ebe', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '44582734a0cedbb03cad7a2aa1090642', '5.4', '策划', NULL, 160),
  ('909052f8d605139d98cf1aede7919c87', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '7acc23c5cb93dbbf7958ae322d3b7ebe', '5.4.1', '质量目标', NULL, 170),
  ('b3cdc55def29dd2ee7a485e0a65c7f6b', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '7acc23c5cb93dbbf7958ae322d3b7ebe', '5.4.2', '质量管理体系策划', NULL, 180),
  ('469ee7a0d98b01469550a9e083266188', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '44582734a0cedbb03cad7a2aa1090642', '5.5', '职责、权限与沟通', NULL, 190),
  ('9ac7a32f1ebd7433ca7b325f032ee8de', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '469ee7a0d98b01469550a9e083266188', '5.5.1', '职责和权限', NULL, 200),
  ('7de585ef44e9fa355704982f2d638fa9', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '469ee7a0d98b01469550a9e083266188', '5.5.2', '管理者代表', NULL, 210),
  ('a0c28df416a1a7957f67c9102e52dea6', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '469ee7a0d98b01469550a9e083266188', '5.5.3', '内部沟通', NULL, 220),
  ('8991cf34e004c1d44c2d7d95f90083d2', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '44582734a0cedbb03cad7a2aa1090642', '5.6', '管理评审', NULL, 230),
  ('845ca7e27378311a15b16cf3a7f03b1f', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '8991cf34e004c1d44c2d7d95f90083d2', '5.6.1', '总则', NULL, 240),
  ('7898aaa7cffc2eb8981079fec9cb1ed3', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '8991cf34e004c1d44c2d7d95f90083d2', '5.6.2', '评审输入', NULL, 250),
  ('8f781f765c6e118861b962549a557aa0', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '8991cf34e004c1d44c2d7d95f90083d2', '5.6.3', '评审输出', NULL, 260),
  ('9d38d9c2dd3586462b21ab1156eeed9c', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', NULL, '6', '资源管理', NULL, 270),
  ('316223515dbdc424389be4e7d34bbdf7', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '9d38d9c2dd3586462b21ab1156eeed9c', '6.1', '资源提供', NULL, 280),
  ('42c3b55d16883005debe7cbf78a92b1c', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '9d38d9c2dd3586462b21ab1156eeed9c', '6.2', '人力资源', NULL, 290),
  ('93804f161aadcffc5a6b7b56ac2b156d', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '9d38d9c2dd3586462b21ab1156eeed9c', '6.3', '基础设施', NULL, 300),
  ('4b928b6181abbd95fc2f32f741da6504', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '9d38d9c2dd3586462b21ab1156eeed9c', '6.4', '工作环境和污染控制', NULL, 310),
  ('934172dd9cb25930e2ee254c30c6bde2', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '4b928b6181abbd95fc2f32f741da6504', '6.4.1', '工作环境', NULL, 320),
  ('ccc013a6538561c5e3fb722fe258c232', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '4b928b6181abbd95fc2f32f741da6504', '6.4.2', '污染控制', NULL, 330),
  ('5c9c95f1a54027245f7ccfd45fb3c2d9', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', NULL, '7', '产品实现', NULL, 340),
  ('74e82dad9b1877ab73c91848edc3063c', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '5c9c95f1a54027245f7ccfd45fb3c2d9', '7.1', '产品实现的策划', NULL, 350),
  ('fa6c4a85b65cd0c58ba99ef872438b92', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '5c9c95f1a54027245f7ccfd45fb3c2d9', '7.2', '与顾客有关的过程', NULL, 360),
  ('1565d06ddbba5ec7162beecafd0ac7e4', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'fa6c4a85b65cd0c58ba99ef872438b92', '7.2.1', '产品要求的确定', NULL, 370),
  ('90d3000379d5f469deac3aca2a2ab819', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'fa6c4a85b65cd0c58ba99ef872438b92', '7.2.2', '产品要求的评审', NULL, 380),
  ('1512b2237924f5233b5fd033e4878397', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'fa6c4a85b65cd0c58ba99ef872438b92', '7.2.3', '沟通', NULL, 390),
  ('2da56525fe6ebe3fdadb321331f24b36', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '5c9c95f1a54027245f7ccfd45fb3c2d9', '7.3', '设计和开发', NULL, 400),
  ('a4001313f7297bddf3f5e90a935d7545', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.1', '总则', NULL, 410),
  ('b7b1070752e7bf793ab21a2c12741332', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.2', '设计和开发策划', NULL, 420),
  ('ffed3da3e245bc7c36b6c8603ba014f9', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.3', '设计和开发输入', NULL, 430),
  ('0bf1ba323a8dad8018b7a8457fc9c1ea', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.4', '设计和开发输出', NULL, 440),
  ('c933b3dce2632ab6d45c04ccced8fcf3', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.5', '设计和开发评审', NULL, 450),
  ('340646ba38291373b78520478945f06d', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.6', '设计和开发验证', NULL, 460),
  ('ccb8d045e5fd87957e008b9d33f2f181', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.7', '设计和开发确认', NULL, 470),
  ('8a0f03bb1a26165b876dbba508fcded9', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.8', '设计和开发转换', NULL, 480),
  ('edf90de28fb4ee1cd635c860494ffe1e', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.9', '设计和开发更改的控制', NULL, 490),
  ('fc3942a172ab8da10b31498d77876699', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '2da56525fe6ebe3fdadb321331f24b36', '7.3.10', '设计和开发文档', NULL, 500),
  ('f68d5480ac4c9f8e3e8b0086a4a0d1f2', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '5c9c95f1a54027245f7ccfd45fb3c2d9', '7.4', '采购', NULL, 510),
  ('0ff92484abe9d91917531767b7a20fee', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'f68d5480ac4c9f8e3e8b0086a4a0d1f2', '7.4.1', '采购过程', NULL, 520),
  ('d66035fc93bf5528c4ddb1ab517dec07', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'f68d5480ac4c9f8e3e8b0086a4a0d1f2', '7.4.2', '采购信息', NULL, 530),
  ('4f194b4f42c2d915cbae851c43a12fe3', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', 'f68d5480ac4c9f8e3e8b0086a4a0d1f2', '7.4.3', '采购产品的验证', NULL, 540),
  ('55a2d641549f81c6d71dea2e293bdc82', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '5c9c95f1a54027245f7ccfd45fb3c2d9', '7.5', '生产和服务提供', NULL, 550),
  ('c687680abf665d836986ffdedff65a5d', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.1', '生产和服务提供的控制', NULL, 560),
  ('e1d0936e2a964e6e9d961349a64c1527', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.2', '产品的清洁', NULL, 570),
  ('48d7bff8ea962bf975e61a84e1210652', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.3', '安装活动', NULL, 580),
  ('24b82c01836749c11f0a453be75a777e', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.4', '服务活动', NULL, 590),
  ('2e43c86387b747182ee7d05975ff0752', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.5', '无菌医疗器械的专用要求', NULL, 600),
  ('15efbe0da845b405f32f038f4c123185', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.6', '生产和服务提供过程的确认', NULL, 610),
  ('20f0969ff320315cd28508bfd0ad920e', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.7', '灭菌过程和无菌屏障系统确认的专用要求', NULL, 620),
  ('fb01f1935fa4b19e4342d44805f8db13', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.8', '标识', NULL, 630),
  ('938c1d0425d7ecf192f0a7549e4b3f77', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.9', '可追溯性', NULL, 640),
  ('1b7ec24295a09ff9c6e47353df66225c', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '938c1d0425d7ecf192f0a7549e4b3f77', '7.5.9.1', '总则', NULL, 650),
  ('e1278baa32a182a975f61b32ce6a943a', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '938c1d0425d7ecf192f0a7549e4b3f77', '7.5.9.2', '植入性医疗器械的专用要求', NULL, 660),
  ('b107391f85b6a42cff771028b33084b8', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.10', '顾客财产', NULL, 670),
  ('847d73954dec5417864853f00f3a5c10', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a2d641549f81c6d71dea2e293bdc82', '7.5.11', '产品防护', NULL, 680),
  ('83374a107e330f63c2c27fe96bb95865', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '5c9c95f1a54027245f7ccfd45fb3c2d9', '7.6', '监视和测量设备的控制', NULL, 690),
  ('1a9f5ce570b65b9e88ffedf433ff24f3', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', NULL, '8', '测量、分析和改进', NULL, 700),
  ('c360234d3803e409e7b96d759b241046', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '1a9f5ce570b65b9e88ffedf433ff24f3', '8.1', '总则', NULL, 710),
  ('36c3698d057d729278926a223885f988', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '1a9f5ce570b65b9e88ffedf433ff24f3', '8.2', '监视和测量', NULL, 720),
  ('55d44eebf39b3a72911720355ae19ebf', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '36c3698d057d729278926a223885f988', '8.2.1', '反馈', NULL, 730),
  ('f09dd9614a260c2afa021730fb51d235', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '36c3698d057d729278926a223885f988', '8.2.2', '投诉处置', NULL, 740),
  ('930302c01c72d768f1d0146792dca21d', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '36c3698d057d729278926a223885f988', '8.2.3', '向监管机构报告', NULL, 750),
  ('cf2cdfac2c73edd40b63b1aa68612ab7', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '36c3698d057d729278926a223885f988', '8.2.4', '内部审核', NULL, 760),
  ('6d3549a9da65b274a3289ea8264c48b4', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '36c3698d057d729278926a223885f988', '8.2.5', '过程的监视和测量', NULL, 770),
  ('428cba0b30a46510bf2829b673432af7', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '36c3698d057d729278926a223885f988', '8.2.6', '产品的监视和测量', NULL, 780),
  ('55a25c6788953540bf5a6d134dd37570', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '1a9f5ce570b65b9e88ffedf433ff24f3', '8.3', '不合格品控制', NULL, 790),
  ('7fae9a9cde761b6ec1993757809a3631', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a25c6788953540bf5a6d134dd37570', '8.3.1', '总则', NULL, 800),
  ('121407cc6d053ab04f0e539adff3ec1a', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a25c6788953540bf5a6d134dd37570', '8.3.2', '交付前发现不合格品的响应措施', NULL, 810),
  ('0b1832c9cbb9d5cf9ad720591fa3c025', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a25c6788953540bf5a6d134dd37570', '8.3.3', '交付后发现不合格品的响应措施', NULL, 820),
  ('dff53b1fa0c49e3d88fd6ceefcbe56dc', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '55a25c6788953540bf5a6d134dd37570', '8.3.4', '返工', NULL, 830),
  ('66adf7c6180ec5c39476dd40fd917960', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '1a9f5ce570b65b9e88ffedf433ff24f3', '8.4', '数据分析', NULL, 840),
  ('17aea7d58aafdcc303be4db4f4d082f7', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '1a9f5ce570b65b9e88ffedf433ff24f3', '8.5', '改进', NULL, 850),
  ('2b5b7eca77cf4a2b7baf5c7f411c99ed', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '17aea7d58aafdcc303be4db4f4d082f7', '8.5.1', '总则', NULL, 860),
  ('3687560b7262c2fc7d99534697bf9ad7', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '17aea7d58aafdcc303be4db4f4d082f7', '8.5.2', '纠正措施', NULL, 870),
  ('c6bcc071be3a7178f1ece897ee54befb', NOW(), 0, 1, 'dfca5483-f83c-4999-b773-3624de5f7cf7', '17aea7d58aafdcc303be4db4f4d082f7', '8.5.3', '预防措施', NULL, 880)
ON DUPLICATE KEY UPDATE Title = VALUES(Title), ParentCode = VALUES(ParentCode),
                        Sort = VALUES(Sort), Description = NULL, IsDeleted = 0, IsValid = 1;
DELETE FROM cert_iso_clause WHERE StandardCode = 'dfca5483-f83c-4999-b773-3624de5f7cf7'
   AND ClauseNumber NOT IN ('1', '2', '3', '4', '4.1', '4.2', '4.2.1', '4.2.2', '4.2.3', '4.2.4', '4.2.5', '5', '5.1', '5.2', '5.3', '5.4', '5.4.1', '5.4.2', '5.5', '5.5.1', '5.5.2', '5.5.3', '5.6', '5.6.1', '5.6.2', '5.6.3', '6', '6.1', '6.2', '6.3', '6.4', '6.4.1', '6.4.2', '7', '7.1', '7.2', '7.2.1', '7.2.2', '7.2.3', '7.3', '7.3.1', '7.3.2', '7.3.3', '7.3.4', '7.3.5', '7.3.6', '7.3.7', '7.3.8', '7.3.9', '7.3.10', '7.4', '7.4.1', '7.4.2', '7.4.3', '7.5', '7.5.1', '7.5.2', '7.5.3', '7.5.4', '7.5.5', '7.5.6', '7.5.7', '7.5.8', '7.5.9', '7.5.9.1', '7.5.9.2', '7.5.10', '7.5.11', '7.6', '8', '8.1', '8.2', '8.2.1', '8.2.2', '8.2.3', '8.2.4', '8.2.5', '8.2.6', '8.3', '8.3.1', '8.3.2', '8.3.3', '8.3.4', '8.4', '8.5', '8.5.1', '8.5.2', '8.5.3');

-- ---------- 验证 SQL（执行后手工跑，期望值已注明） ----------
-- ① 四标准条款总数 = 273
-- SELECT s.StandardCode, s.StandardName, s.VersionYear, COUNT(c.Id) AS Clauses
--   FROM cert_iso_standard s LEFT JOIN cert_iso_clause c
--     ON c.StandardCode = s.Code AND c.IsDeleted = 0
--  WHERE s.IsDeleted = 0 GROUP BY s.StandardCode, s.StandardName, s.VersionYear;
-- ② 孤儿检查（应 0 行）：ParentCode 指向不存在的条款
-- SELECT c.ClauseNumber, c.Title FROM cert_iso_clause c
--   LEFT JOIN cert_iso_clause p ON c.ParentCode = p.Code
--  WHERE c.IsDeleted = 0 AND c.ParentCode IS NOT NULL AND p.Code IS NULL;
-- ③ 条款内容应为空（应 0 行）
-- SELECT COUNT(*) FROM cert_iso_clause WHERE IsDeleted = 0 AND Description IS NOT NULL;
-- ④ 幂等复跑：家族/标准 INSERT IGNORE 不重复；条款因先 DELETE 后 INSERT 结果一致
