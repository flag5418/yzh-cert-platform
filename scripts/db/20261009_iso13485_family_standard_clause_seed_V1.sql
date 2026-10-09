-- =====================================================================================
-- 20261009_iso13485_family_standard_clause_seed_V1.sql
-- ISO 13485 医疗器械体系：族 + 标准 + 全量条款树（★ 数据很少改动，一次成稿）
--   目标表：cert_standard_family / cert_iso_standard / cert_iso_clause
--   案例来源：docs/90-归档/案例资料/CS河北雄安尚龙医疗科技有限公司13485体系材料/
--             1质量手册/XASL-QM 质量手册.doc（提取文本 qm.txt，998 行）
--
--   ★ 用户裁决（2026-10-09，六项全部确认）：
--     ① 族 FamilyNo=iso13485、标准 StandardCode=iso13485 / VersionYear=2016 /
--        StandardName=ISO 13485 医疗器械质量管理体系、Category=medical
--     ② 条款树 = ISO 官方编号主干 + 质量手册全量独有章节
--     ③ 落库 = 本 SQL 种子脚本（固定 GUID + MD5 确定性 Code，INSERT IGNORE 幂等）
--     ④ iso9001 下 2 条垃圾条款本次不删（另写 scripts/db/fix/ 处理）
--     ⑤ 编号冲突口径 = 「ISO 官方为准」：4.2 五条（含 4.2.3 医疗器械文档）、
--        7.3 十条、6.4.1/6.4.2、5.6.1–5.6.3、7.5.9.1 补建；8.3 无子条款；
--        手册 0.x/1.3/1.4/4.3/5.7/6.5/7.7/8.6/9/10 等独有章节照收；
--        手册原编号错位处写入 Description（7.3 映射、4.2.4/4.2.5 编号、5.6.x 无标题）
--     ⑥ 7.5.2 补 ISO 标题建节点：Title=产品清洁（GB/T 42061-2022 作「产品的清洁」），
--        Description 引手册 qm.txt 行770 原文
--
--   ★ 建树规则（与既有裁决一致）：
--     - 只收「短标题式章节」；无标题编号正文段不建节点（4.1.1–4.1.5、4.2.1.1–4.2.1.4、
--       5.4.1.1/5.4.1.2、5.4.2.1/5.4.2.2、6.1.x、6.3.x、7.1.x、7.4.2.x、7.5.1.x、
--       7.5.6.x、7.5.8.1–7.5.8.3、7.5.8.4.1–.3、7.6.x、8.1.x、8.4.x、1.4.x）
--     - 支持性文件/程序清单子项（4.3.x、5.7.x、6.5.x、7.7.x、8.6.x）作叶子收
--     - 手册独有编号子项：5.5.2.1–5.5.2.5（部门职责）、7.5.8.4（状态标识）保留
--     - 不适用条款（7.5.3/7.5.5/7.5.7/7.5.9.2，手册 1.3）建节点，说明写 Description
--     - 「附录七」=目录 0.7 同一内容，不重复建节点；附录一/二/三 挂 9 下
--
--   ★ 技术要点：
--     - Code = MD5(CONCAT('iso13485|', ClauseNumber))（32 hex，适配 varchar(36)，确定性幂等）
--     - ParentCode = MD5(CONCAT('iso13485|', 父ClauseNumber))，根节点 NULL（VALUES 不写死，
--       由子查询的 ParentNo 推导 —— 避免父子两处各写一遍 MD5）
--     - cert_iso_clause 仅 uk_code(Code) 唯一；(StandardCode,ClauseNumber) 唯一性在应用层
--       （UniqueField），本脚本按 Code 幂等已足够
--     - 只增不改：INSERT IGNORE，重复执行不报错、不覆盖已有行
--
--   执行：docker exec -i yzh-mysql mysql -uroot -pYzh123456. \
--            --default-character-set=utf8mb4 yzh_cert_platform \
--            < scripts/db/20261009_iso13485_family_standard_clause_seed_V1.sql
-- =====================================================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- ---------- ① 标准族（三层：体系 medical → 族 iso13485 → 版本 2016） ----------
INSERT IGNORE INTO cert_standard_family
  (Code, FamilyNo, FamilyName, Category, Description, Sort, IsValid, IsDeleted, CreateTime)
VALUES
  ('ad35df13-eb00-4741-9600-413122d0be22', 'iso13485', 'ISO 13485 医疗器械质量管理体系族', 'medical',
   '案例：河北雄安尚龙医疗科技有限公司（手动轮椅车）；对应 GB/T 42061-2022 idt ISO 13485:2016',
   10, 1, 0, NOW());

-- ---------- ② 标准（版本行；关联键=Code GUID，FamilyCode 指向族 Code） ----------
INSERT IGNORE INTO cert_iso_standard
  (Code, StandardCode, StandardName, VersionYear, Category, FamilyCode, Description,
   Sort, IsValid, IsDeleted, ParentCode, IsLeaf, CreateTime)
VALUES
  ('dfca5483-f83c-4999-b773-3624de5f7cf7', 'iso13485', 'ISO 13485 医疗器械质量管理体系', 2016, 'medical',
   'ad35df13-eb00-4741-9600-413122d0be22',
   'GB/T 42061-2022 idt ISO 13485:2016；案例企业：河北雄安尚龙医疗科技有限公司；条款树 = ISO 官方编号主干 + 质量手册独有章节',
   10, 1, 0, NULL, 1, NOW());

-- ---------- ③ 条款树（141 行 = 19 根 + 122 子；SortOrder 按手册正文 DFS ×10） ----------
INSERT IGNORE INTO cert_iso_clause
  (Code, CreateTime, IsDeleted, IsValid, StandardCode, ParentCode,
   ClauseNumber, Title, Description, SortOrder)
SELECT MD5(CONCAT('iso13485|', ClauseNumber)), NOW(), 0, 1,
       'dfca5483-f83c-4999-b773-3624de5f7cf7',
       CASE WHEN ParentNo IS NULL THEN NULL ELSE MD5(CONCAT('iso13485|', ParentNo)) END,
       ClauseNumber, Title, Description, SortOrder
FROM (
  -- ── 手册前置章节 0.x（目录标题；正文变体写 Description） ──
  SELECT '0.1' AS ClauseNumber, NULL AS ParentNo, '质量手册颁布令' AS Title, '手册目录标题；正文标题为「0.1 颁布令」' AS Description, 10 AS SortOrder UNION ALL
  SELECT '0.2', NULL, '管理者代表任命书', '手册独有章节', 20 UNION ALL
  SELECT '0.3', NULL, '质量手册管理', '手册独有章节', 30 UNION ALL
  SELECT '0.4', NULL, '企业概况', '手册目录标题；正文标题为「公司概况」', 40 UNION ALL
  SELECT '0.5', NULL, '企业组织结构图', '手册独有章节', 50 UNION ALL
  SELECT '0.6', NULL, '质量体系结构图', '手册目录标题；正文标题为「质量管理体系结构图」', 60 UNION ALL
  SELECT '0.7', NULL, '质量管理体系职能分配表', '手册目录 0.7；正文以「附录七 质量管理体系职能分配表」呈现（同一内容，不另建附录七节点）', 70 UNION ALL
  SELECT '0.8', NULL, '质量方针', '手册独有章节', 80 UNION ALL
  SELECT '0.9', NULL, '质量目标', '手册独有章节', 90 UNION ALL
  -- ── 1 范围 ──
  SELECT '1', NULL, '范围', 'ISO 第 1 章 / 手册正文「1.范围」', 100 UNION ALL
  SELECT '1.1', '1', '总则', NULL, 110 UNION ALL
  SELECT '1.2', '1', '应用', NULL, 120 UNION ALL
  SELECT '1.3', '1', '删减和不适用条款', '手册独有章节。不适用条款：7.5.3 安装活动（无安装）、7.5.5 无菌专用要求、7.5.7 灭菌过程确认专用要求、7.5.9.2 植入性专项要求（手动轮椅车均未涉及）', 130 UNION ALL
  SELECT '1.4', '1', '外包过程识别', '手册独有章节（1.4.1/1.4.2 为正文段，不建节点）。本公司外包过程：无', 140 UNION ALL
  -- ── 2 引用文件 / 3 术语和定义 ──
  SELECT '2', NULL, '引用文件', '手册标题（ISO 第 2 章 Normative references）', 150 UNION ALL
  SELECT '3', NULL, '术语和定义', 'ISO 第 3 章 / 手册正文「3.术语和定义」', 160 UNION ALL
  -- ── 4 质量管理体系 ──
  SELECT '4', NULL, '质量管理体系', 'ISO 第 4 章 / 手册正文「4.质量管理体系」', 170 UNION ALL
  SELECT '4.1', '4', '总要求', 'ISO 4.1.1–4.1.6 与手册 4.1.1–4.1.5 均为无标题编号正文段，不建节点', 180 UNION ALL
  SELECT '4.2', '4', '文件总要求', '手册正文标题「文件总要求」（ISO Documentation requirements）', 190 UNION ALL
  SELECT '4.2.1', '4.2', '总则', '手册 4.2.1.1–4.2.1.4（文件四层次/规模/产品主文档/动态体系）为正文段不建节点；手册 4.2.1.3 产品主文档要求对应 ISO 4.2.3', 200 UNION ALL
  SELECT '4.2.2', '4.2', '质量手册', '手册同号（4.2.2 质量手册）', 210 UNION ALL
  SELECT '4.2.3', '4.2', '医疗器械文档', '手册正文无此节点（手册编号 4.2.3 为文件控制）；GB/T 42061-2022 标题「医疗器械文档」，按 ISO 官方补建', 220 UNION ALL
  SELECT '4.2.4', '4.2', '文件控制', '手册正文编号为 4.2.3（文件控制）；手册中「见4.2.3」即指本条', 230 UNION ALL
  SELECT '4.2.5', '4.2', '记录控制', '手册正文编号为 4.2.4（记录控制）；部分交叉引用按 ISO 写 4.2.5', 240 UNION ALL
  SELECT '4.3', '4', '支持性文件', '手册独有章节', 250 UNION ALL
  SELECT '4.3.1', '4.3', 'XASL-QP-001《文件控制程序》', NULL, 260 UNION ALL
  SELECT '4.3.2', '4.3', 'XASL-QP-002《记录控制程序》', NULL, 270 UNION ALL
  -- ── 5 管理职责 ──
  SELECT '5', NULL, '管理职责', 'ISO 第 5 章 / 手册正文「5.管理职责」', 280 UNION ALL
  SELECT '5.1', '5', '管理承诺', NULL, 290 UNION ALL
  SELECT '5.2', '5', '以顾客为关注焦点', NULL, 300 UNION ALL
  SELECT '5.3', '5', '质量方针', NULL, 310 UNION ALL
  SELECT '5.4', '5', '策划', NULL, 320 UNION ALL
  SELECT '5.4.1', '5.4', '质量目标', '手册 5.4.1.1/5.4.1.2 为正文段不建节点', 330 UNION ALL
  SELECT '5.4.2', '5.4', '质量管理体系策划', '手册 5.4.2.1/5.4.2.2 为正文段不建节点', 340 UNION ALL
  SELECT '5.5', '5', '职责、权限和沟通', NULL, 350 UNION ALL
  SELECT '5.5.1', '5.5', '职责和权限', NULL, 360 UNION ALL
  SELECT '5.5.2', '5.5', '管理者代表', NULL, 370 UNION ALL
  SELECT '5.5.2.1', '5.5.2', '总经理', '手册独有（ISO 5.5.2 无子条款）：总经理在质量管理体系中的职责', 380 UNION ALL
  SELECT '5.5.2.2', '5.5.2', '生产部', '手册独有（ISO 5.5.2 无子条款）：生产部在质量管理体系中的职责', 390 UNION ALL
  SELECT '5.5.2.3', '5.5.2', '质量部', '手册独有（ISO 5.5.2 无子条款）：质量部在质量管理体系中的职责', 400 UNION ALL
  SELECT '5.5.2.4', '5.5.2', '综合部', '手册独有（ISO 5.5.2 无子条款）：综合部在质量管理体系中的职责', 410 UNION ALL
  SELECT '5.5.2.5', '5.5.2', '供销部', '手册独有（ISO 5.5.2 无子条款）：供销部在质量管理体系中的职责', 420 UNION ALL
  SELECT '5.5.3', '5.5', '内部沟通', NULL, 430 UNION ALL
  SELECT '5.6', '5', '管理评审', 'ISO 5.6.1–5.6.3 按官方补建；手册正文 5.6.1/5.6.2 为内容段（评审组织、每年至少一次）无独立标题', 440 UNION ALL
  SELECT '5.6.1', '5.6', '总则', 'ISO 官方子条款；手册无对应标题', 450 UNION ALL
  SELECT '5.6.2', '5.6', '评审输入', 'ISO 官方子条款；手册无对应标题', 460 UNION ALL
  SELECT '5.6.3', '5.6', '评审输出', 'ISO 官方子条款；手册无对应标题', 470 UNION ALL
  SELECT '5.7', '5', '支持性文件', '手册独有章节', 480 UNION ALL
  SELECT '5.7.1', '5.7', 'XASL-QP-003《职责权限和沟通控制程序》', NULL, 490 UNION ALL
  SELECT '5.7.2', '5.7', 'XASL-QP-004《管理评审控制程序》', NULL, 500 UNION ALL
  -- ── 6 资源管理 ──
  SELECT '6', NULL, '资源管理', 'ISO 第 6 章 / 手册正文「6.资源管理」', 510 UNION ALL
  SELECT '6.1', '6', '资源提供', '手册 6.1.1–6.1.3 为正文段不建节点', 520 UNION ALL
  SELECT '6.2', '6', '人力资源', NULL, 530 UNION ALL
  SELECT '6.2.1', '6.2', '总则', NULL, 540 UNION ALL
  SELECT '6.2.2', '6.2', '能力、意识和培训', '手册 6.2.2.1–6.2.2.4 为正文段不建节点', 550 UNION ALL
  SELECT '6.3', '6', '基础设施', '手册 6.3.1–6.3.3 为正文段不建节点', 560 UNION ALL
  SELECT '6.4', '6', '工作环境和污染控制', '手册正文为单段（引《工作环境管理制度》），无子条款标题；6.4.1/6.4.2 按 ISO/GB 官方补建', 570 UNION ALL
  SELECT '6.4.1', '6.4', '工作环境', 'ISO 官方子条款；手册无对应标题', 580 UNION ALL
  SELECT '6.4.2', '6.4', '污染控制', 'ISO 官方子条款；手册无对应标题', 590 UNION ALL
  SELECT '6.5', '6', '支持性文件', '手册独有章节', 600 UNION ALL
  SELECT '6.5.1', '6.5', 'XASL-QP-005《培训控制程序》', NULL, 610 UNION ALL
  SELECT '6.5.2', '6.5', 'XASL-QP-006《基础设施及设备控制程序》', NULL, 620 UNION ALL
  SELECT '6.5.3', '6.5', 'XASL-QD-001《工作环境管理制度》', NULL, 630 UNION ALL
  -- ── 7 产品实现 ──
  SELECT '7', NULL, '产品实现', 'ISO 第 7 章 / 手册正文「7.产品实现」', 640 UNION ALL
  SELECT '7.1', '7', '实现过程的策划', '手册标题（ISO Planning of product realization）', 650 UNION ALL
  SELECT '7.2', '7', '与顾客有关的过程', NULL, 660 UNION ALL
  SELECT '7.2.1', '7.2', '与产品要求有关过程的确定', NULL, 670 UNION ALL
  SELECT '7.2.2', '7.2', '与产品有关要求的评审', NULL, 680 UNION ALL
  SELECT '7.2.3', '7.2', '沟通', NULL, 690 UNION ALL
  SELECT '7.3', '7', '设计和开发', '手册编号与 ISO 错位：手册 7.3.1–7.3.7 → ISO 7.3.2/7.3.3/7.3.4/7.3.5/7.3.6/7.3.7/7.3.9；ISO 7.3.1 总则、7.3.8 转换、7.3.10 文档手册未单列', 700 UNION ALL
  SELECT '7.3.1', '7.3', '总则', 'ISO 官方子条款；手册无「总则」标题（手册 7.3.1=设计和开发策划，对应 ISO 7.3.2）', 710 UNION ALL
  SELECT '7.3.2', '7.3', '设计和开发策划', '对应手册 7.3.1 设计和开发策划（编号错位）', 720 UNION ALL
  SELECT '7.3.3', '7.3', '设计和开发输入', '对应手册 7.3.2 设计开发输入（编号错位）', 730 UNION ALL
  SELECT '7.3.4', '7.3', '设计和开发输出', '对应手册 7.3.3 设计和开发输出（编号错位）', 740 UNION ALL
  SELECT '7.3.5', '7.3', '设计和开发评审', '对应手册 7.3.4 设计和开发评审（编号错位）', 750 UNION ALL
  SELECT '7.3.6', '7.3', '设计和开发验证', '对应手册 7.3.5 设计和开发验证（编号错位）', 760 UNION ALL
  SELECT '7.3.7', '7.3', '设计和开发确认', '对应手册 7.3.6 设计和开发确认（编号错位）', 770 UNION ALL
  SELECT '7.3.8', '7.3', '设计和开发转换', 'GB/T 42061-2022 标题「设计和开发转换」；手册无独立节点（7.3.1 策划段提及转换活动）', 780 UNION ALL
  SELECT '7.3.9', '7.3', '设计和开发更改的控制', '对应手册 7.3.7 设计和开发更改的控制（编号错位）', 790 UNION ALL
  SELECT '7.3.10', '7.3', '设计和开发文档', 'GB/T 42061-2022 标题「设计和开发文档」；手册无独立节点', 800 UNION ALL
  SELECT '7.4', '7', '采购', NULL, 810 UNION ALL
  SELECT '7.4.1', '7.4', '采购过程', '手册 7.4.1.1–7.4.1.4 为正文段不建节点', 820 UNION ALL
  SELECT '7.4.2', '7.4', '采购信息', '手册 7.4.2.1–7.4.2.3 为正文段不建节点', 830 UNION ALL
  SELECT '7.4.3', '7.4', '采购产品的验证', NULL, 840 UNION ALL
  SELECT '7.5', '7', '生产和服务提供', NULL, 850 UNION ALL
  SELECT '7.5.1', '7.5', '生产和服务提供的控制', '手册 7.5.1.1/7.5.1.2 为正文段不建节点', 860 UNION ALL
  SELECT '7.5.2', '7.5', '产品清洁', '手册行770 为无标题正文段：「7.5.2.公司制定了《工作环境管理制度》、《产品防护控制程序》规定了产品清洁和污染控制的途径和方法。」；按裁决取 ISO 标题「产品清洁」（GB/T 42061-2022 作「产品的清洁」）', 870 UNION ALL
  SELECT '7.5.3', '7.5', '安装活动', '本公司暂不适用（手册 1.3：产品不存在安装活动）', 880 UNION ALL
  SELECT '7.5.4', '7.5', '服务活动', NULL, 890 UNION ALL
  SELECT '7.5.5', '7.5', '无菌医疗器械的专用要求', '本公司暂不适用（手册 1.3：未涉及需灭菌医疗器械）', 900 UNION ALL
  SELECT '7.5.6', '7.5', '生产和服务提供过程的确认', '手册 7.5.6.1–7.5.6.3 为正文段不建节点', 910 UNION ALL
  SELECT '7.5.7', '7.5', '灭菌过程和无菌屏障系统确认的专用要求', '本公司暂不适用（手册 1.3：未涉及需灭菌医疗器械）', 920 UNION ALL
  SELECT '7.5.8', '7.5', '标识', '手册 7.5.8.1–7.5.8.3、7.5.8.4.1–7.5.8.4.3 为正文段不建节点；状态标识单列 7.5.8.4', 930 UNION ALL
  SELECT '7.5.8.4', '7.5.8', '状态标识', '手册独有（ISO 7.5.8 无子条款）', 940 UNION ALL
  SELECT '7.5.9', '7.5', '可追溯性', '手册正文单段（无 7.5.9.1 标题）；7.5.9.1 按 ISO 官方补建', 950 UNION ALL
  SELECT '7.5.9.1', '7.5.9', '总则', 'ISO 官方子条款；手册无对应标题', 960 UNION ALL
  SELECT '7.5.9.2', '7.5.9', '植入性医疗器械的专项要求', '本公司暂不适用（手册 1.3：未涉及植入性医疗器械）', 970 UNION ALL
  SELECT '7.5.10', '7.5', '顾客财产', NULL, 980 UNION ALL
  SELECT '7.5.11', '7.5', '产品防护', NULL, 990 UNION ALL
  SELECT '7.6', '7', '监视和测量设备的控制', '手册 7.6.1–7.6.4 为正文段不建节点', 1000 UNION ALL
  SELECT '7.7', '7', '支持文件', '手册独有章节（程序文件清单）', 1010 UNION ALL
  SELECT '7.7.1', '7.7', 'XASL-QP-021《生产和服务提供控制程序》', NULL, 1020 UNION ALL
  SELECT '7.7.2', '7.7', 'XASL-QP-015《与顾客有关的过程控制程序》', NULL, 1030 UNION ALL
  SELECT '7.7.3', '7.7', 'XASL-QP-011《设计开发控制程序》', NULL, 1040 UNION ALL
  SELECT '7.7.4', '7.7', 'XASL-QP-022《供方评估和复评程序》', NULL, 1050 UNION ALL
  SELECT '7.7.5', '7.7', 'XASL-QP-012《采购控制程序》', NULL, 1060 UNION ALL
  SELECT '7.7.6', '7.7', 'XASL-QP-018《产品标识和可追溯性控制程序》', NULL, 1070 UNION ALL
  SELECT '7.7.7', '7.7', 'XASL-QP-019《顾客财产的控制程序》', NULL, 1080 UNION ALL
  SELECT '7.7.8', '7.7', 'XASL-QP-014《产品防护控制程序》', NULL, 1090 UNION ALL
  SELECT '7.7.9', '7.7', 'XASL-QP-017《验证和确认控制程序》', NULL, 1100 UNION ALL
  SELECT '7.7.10', '7.7', 'XASL-QP-020《监视和测量装置控制程序》', NULL, 1110 UNION ALL
  SELECT '7.7.11', '7.7', 'XASL-QP-009《风险管理控制程序》', NULL, 1120 UNION ALL
  -- ── 8 测量、分析和改进 ──
  SELECT '8', NULL, '测量、分析和改进', '手册目录作「测量、分析改进」，取正文标题「测量、分析和改进」', 1130 UNION ALL
  SELECT '8.1', '8', '总则', '手册 8.1.1/8.1.2 为正文段不建节点', 1140 UNION ALL
  SELECT '8.2', '8', '监视和测量', NULL, 1150 UNION ALL
  SELECT '8.2.1', '8.2', '反馈', NULL, 1160 UNION ALL
  SELECT '8.2.2', '8.2', '投诉处置', '手册标题（ISO Complaint handling）', 1170 UNION ALL
  SELECT '8.2.3', '8.2', '向监管机构报告', NULL, 1180 UNION ALL
  SELECT '8.2.4', '8.2', '内审', '手册标题（ISO Internal audit）', 1190 UNION ALL
  SELECT '8.2.5', '8.2', '过程的监视和测量', NULL, 1200 UNION ALL
  SELECT '8.2.6', '8.2', '产品的测量和监控', '手册标题（ISO Monitoring and measurement of product）', 1210 UNION ALL
  SELECT '8.3', '8', '不合格品控制', 'ISO 8.3 无子条款（用户裁决⑤）；手册正文 8.3.1–8.3.4（不合格品控制程序、交付前/交付后响应措施、返工）为编号正文段，不建子节点', 1220 UNION ALL
  SELECT '8.4', '8', '数据分析', '手册 8.4.1–8.4.5 为正文段不建节点', 1230 UNION ALL
  SELECT '8.5', '8', '改进', NULL, 1240 UNION ALL
  SELECT '8.5.1', '8.5', '总则', NULL, 1250 UNION ALL
  SELECT '8.5.2', '8.5', '纠正措施', NULL, 1260 UNION ALL
  SELECT '8.5.3', '8.5', '预防措施', NULL, 1270 UNION ALL
  SELECT '8.6', '8', '支持性文件', '手册独有章节（程序文件清单）', 1280 UNION ALL
  SELECT '8.6.1', '8.6', 'XASL-QP-023《顾客满意度测量控制程序》', NULL, 1290 UNION ALL
  SELECT '8.6.2', '8.6', 'XASL-QP-007《内部审核程序》', NULL, 1300 UNION ALL
  SELECT '8.6.3', '8.6', 'XASL-QP-024《过程和产品监视测量程序》', NULL, 1310 UNION ALL
  SELECT '8.6.4', '8.6', 'XASL-QP-013《不合格品控制程序》', NULL, 1320 UNION ALL
  SELECT '8.6.5', '8.6', 'XASL-QP-025《纠正预防措施控制程序》', NULL, 1330 UNION ALL
  SELECT '8.6.6', '8.6', 'XASL-QP-026《数据分析控制程序》', NULL, 1340 UNION ALL
  SELECT '8.6.7', '8.6', 'XASL-QP-016《质量信息报警反馈控制程序》', NULL, 1350 UNION ALL
  SELECT '8.6.8', '8.6', 'XASL-QP-028《顾客抱怨处理程序》', NULL, 1360 UNION ALL
  -- ── 9 附录 / 10 修订历史（手册独有章节） ──
  SELECT '9', NULL, '附录', '手册独有章节', 1370 UNION ALL
  SELECT '附录一', '9', '质量管理体系过程识别图', '手册附录一（ClauseNumber 取手册写法）', 1380 UNION ALL
  SELECT '附录二', '9', '质量方针和质量目标', '手册附录二', 1390 UNION ALL
  SELECT '附录三', '9', '程序文件清单', '手册附录三', 1400 UNION ALL
  SELECT '10', NULL, '修订历史', '手册独有章节；正文「10.修订历史」与附录三同段，版本/修改日期/更改记录表格不建节点', 1410
) t;

-- ---------- 验证 SQL（执行后手工跑，期望值已注明） ----------
-- ① 条款总数 = 141、根节点 = 19（0.1–0.9 + 1–10）
-- SELECT COUNT(*) FROM cert_iso_clause WHERE StandardCode='dfca5483-f83c-4999-b773-3624de5f7cf7';                       -- 141
-- SELECT COUNT(*) FROM cert_iso_clause WHERE StandardCode='dfca5483-f83c-4999-b773-3624de5f7cf7' AND ParentCode IS NULL; -- 19
-- ② 孤儿检查（应 0 行）
-- SELECT c.ClauseNumber, c.Title FROM cert_iso_clause c
--   LEFT JOIN cert_iso_clause p ON c.ParentCode = p.Code
--  WHERE c.StandardCode='dfca5483-f83c-4999-b773-3624de5f7cf7' AND c.ParentCode IS NOT NULL AND p.Code IS NULL;
-- ③ 关键层级子节点数：4.2→5、7.3→10、7.5→11、5.5.2→5、8.6→8、9→3
-- SELECT p.ClauseNumber, p.Title, COUNT(c.Id) children FROM cert_iso_clause p
--   JOIN cert_iso_clause c ON c.ParentCode = p.Code
--  WHERE p.StandardCode='dfca5483-f83c-4999-b773-3624de5f7cf7'
--  GROUP BY p.ClauseNumber, p.Title ORDER BY p.SortOrder;
-- ④ 族 / 标准
-- SELECT FamilyNo, FamilyName, Category, Sort FROM cert_standard_family WHERE FamilyNo='iso13485';
-- SELECT StandardCode, StandardName, VersionYear, Category, FamilyCode FROM cert_iso_standard WHERE StandardCode='iso13485';
-- ⑤ 幂等复跑：再次执行本脚本，三表行数不变（INSERT IGNORE on Code）
