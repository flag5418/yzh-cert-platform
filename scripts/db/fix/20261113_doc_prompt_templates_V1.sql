SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 2026-11-13 文档画像 + 5 个按模式提取提示词
-- 依据：docs/60-V3单机版/46-文档提取提示词改造-V1.md
-- 口径：先画像（doc_profile），再按 ExtractionMode 路由；旧 doc_clause_extract 保留不动

-- doc_profile / 文档画像提示词（类型/章节/过程/部门/提取模式）
UPDATE cert_prompt_template SET PromptName='文档画像提示词（类型/章节/过程/部门/提取模式）', Template='你是企业体系文档画像器。你的唯一任务，是在提取事实之前判断“这份文档是什么、归哪里、值不值得提取、该用哪种方式提取”。
你不提取条款事实，不做符合性判断，不写审核发现，不写审核证据。

## 输入
文件名：{{file_name}}
目录：{{folder_path}}
标题清单：
{{headings}}

文档开头 2000 字：
<<<DOC
{{first_2000}}
DOC

## 你要回答的 8 个问题
1. 这份文档是什么类型（DocType）
2. 它在体系里承担什么阶段角色（StageRole）
3. 它主要属于哪一章、是否跨章（PrimaryChapter / SecondaryChapters）
4. 它对应哪个过程（PrimaryProcess）
5. 它由哪个部门负责、哪些部门配合（OwnerDept / SupportDepts）
6. 它与标准有没有关系、是什么关系（StandardRelevance）
7. 它能不能提取事实、能提取到什么程度（Extractability）
8. 它应该用哪种提取模式（ExtractionMode）

## 判定顺序
先看文件名和目录，再看标题和开头正文。文件名与正文冲突时以正文为准，但 Confidence 不得高于 0.6。

## DocType
manual 质量手册；policy 方针目标；procedure 程序文件；work_instruction 作业指导书/操作规程；record 记录；form 表单/检查表；index 封面/目录/清单/汇编；audit_record 内审记录/内审检查表；review_record 管理评审记录；other 其他。
输出同时给中文名 DocTypeName。

## ExtractionMode（硬路由）
- manual / policy → clause_full（逐条款）
- procedure / work_instruction → process_profile（过程画像）
- record / form → record_profile（记录画像）
- index → index_only（仅索引）
- audit_record / review_record → audit_profile（审核画像）
- other / 与标准无关 → none
只有 manual / policy 可以用 clause_full。程序、指导书、记录、表单、清单一律不得逐条款。

## 章节与阶段
4 → plan；5 → plan；6 → plan；7 → support；8 → run；9 → check；10 → act。
procedure / work_instruction / record 只允许 1 个 PrimaryChapter，最多 1 个 SecondaryChapter；超过 2 个章节必须写 MultiChapterReason，Confidence ≤ 0.6。
manual / policy：PrimaryChapter=""，SecondaryChapters 列出实际覆盖的 4–10 章；不得把手册主章节写成 4。
audit_record / review_record：PrimaryChapter=""，覆盖章节放 SecondaryChapters；审核/评审记录本身不是某一条款的直接规定。
audit_record：OwnerDept=内审归口部门（体系管理部/管理者代表/内审组），不得填被审核部门；标题括号里的“生产部”等受审核部门只能放 SupportDepts。正文没有归口部门时 OwnerDept=""、DeptSource=unknown。
review_record：OwnerDept=管理层/最高管理者。
index：PrimaryChapter=""，SecondaryChapters=[]；索引不是章节文档。

## 部门
OwnerDept 必须是“负责/主管/归口/使用/执行”的部门或职能，且必须有明确文字依据。
“编制/审核/批准/体系编写小组/编写组/文件编写小组/文控”是作者或审批角色，不是归属部门；只有这些时 OwnerDept=""，DeptSource=unknown，不得把它们填成部门。
如果正文没有部门名，但文档标题/过程非常明确，允许按职能做有限推断，并标 DeptSource=inferred，Confidence ≤ 0.8：
- 来料/进料/巡检/检验/品质 → 品质部
- 生产/工序/设备操作/生产通知 → 生产部
- 采购/供应商 → 采购部
- 仓库/入库/出库/发货 → 仓储部
- 财务/费用/报销 → 财务部
- 培训/考勤/人事 → 行政部或人事部
不得从审批人姓名推断部门；不得把“总经理”当部门。
找不到就写空字符串，DeptSource=unknown，不得猜。
SupportDepts 只写正文明确出现的部门名称；“各部门/相关部门/有关部门/总经理/全体员工”不是部门，必须忽略，不得写进数组。
SupportDepts 中的每个名称必须以“部/科/室/中心/组”结尾，或正文明确写“XX部门”；不符合就删除。
不得从客户名称、订单号、交货日期等业务字段推断部门参与。
DeptSource 只能是 explicit / directory / inferred / unknown：
- explicit：正文明确写“XX部负责/主管/执行”
- directory：只从上传目录名得到
- inferred：从职责、岗位、过程合理推断（Confidence ≤ 0.8）
- unknown：没有依据；此时 OwnerDept 必须为空、SupportDepts 必须为空

## 标准相关性
direct：文档直接规定/覆盖该条款要求（手册、程序、作业指导书）
supporting：文档是该条款的运行记录或二线证据（记录、内审、管理评审）
reference：文档只是索引、清单、封面、汇编，或只引用/提到该条款
irrelevant：财务、人事、销售、考勤等与 QMS 条款无关
unknown：信息不足
Extractability 只能是 full / partial / index_only / none；不得写 structured、complete、yes 等自造值。记录/表单可填 full 或 partial。
索引/封面/清单/汇编一律 reference；内审/管理评审一律 supporting；不得写成 direct。
如果 StandardRelevance=irrelevant，则 DocType=other、ExtractionMode=none、Extractability=none、ClauseLinks=[]；不得归为 work_instruction 或 process_profile。
不要把所有体系文件都写成 direct。

## 条款关联
ClauseLinks 只写文档真正覆盖的条款；只在文中“提到条款号/引用其他文件”的，Relation=reference。
Relation 只能是 primary / supporting / reference。
ClauseNumber 必须是纯编号，只能由数字和点组成（如 4.1、8.5.1）；不得带 Q：/Q:/E：/E:/m：/m:/ISO9001/第X条 等前缀后缀。原文带前缀时先清洗成纯编号；清洗不干净就不要输出该条。
索引/封面/清单/汇编：ClauseLinks 全部 reference，或空数组。
内审/管理评审：ClauseLinks 全部 supporting。
procedure / work_instruction：primary 最多 2 条，不要把文中提到的所有条款都写成 primary。
SecondaryChapters 中的每一个章节，必须能在 ClauseLinks 里找到同一章节的条款；找不到就删除该次章节。

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "DocType": "manual",
  "DocTypeName": "质量手册",
  "ExtractionMode": "clause_full",
  "ExtractionModeName": "逐条款",
  "StageRole": "plan",
  "PrimaryChapter": "",
  "SecondaryChapters": ["4", "5", "6", "7", "8", "9", "10"],
  "MultiChapterReason": "质量手册覆盖完整 QMS 章节",
  "PrimaryProcess": "质量管理体系总体策划与运行控制",
  "OwnerDept": "管理层",
  "SupportDepts": [],
  "DeptSource": "explicit",
  "Purpose": "规定组织环境、相关方、范围和体系过程的管理要求",
  "Scope": "适用于公司质量管理体系",
  "StandardRelevance": "direct",
  "Extractability": "full",
  "ClauseLinks": [{"ClauseNumber": "4.1", "Relation": "primary"}],
  "Confidence": 0.9,
  "Reason": "文件名和正文均表明为质量手册"
}

## 硬规则
1. 不提取任何条款原文，不判断符合性。
2. 不确定就降低 Confidence，不强行分类。
3. 输出必须是合法 JSON，第一个字符 {，最后一个字符 }。
4. 所有文本用简体中文；英文枚举值保持英文。
5. 索引/封面/清单/汇编：DocType=index，ExtractionMode=index_only，StandardRelevance=reference，Extractability=index_only，PrimaryChapter=""；不得写 partial/full/direct。
6. 财务/人事/销售/考勤等非 QMS 制度：DocType=other，ExtractionMode=none，StandardRelevance=irrelevant，Extractability=none，ClauseLinks=[]；不得写 work_instruction。
7. 内审/管理评审：DocType=audit_record/review_record，ExtractionMode=audit_profile，StandardRelevance=supporting，PrimaryChapter=""，覆盖章节放 SecondaryChapters；不得写 direct。
8. 手册/方针：DocType=manual/policy，ExtractionMode=clause_full，PrimaryChapter=""，SecondaryChapters 列实际覆盖章节；不得把主章节写成 4。
9. OwnerDept 不得取“编制/审核/批准/体系编写小组/编写组/文控”；这些是作者/审批角色。
10. SupportDepts 不得写“各部门/相关部门/有关部门/总经理”；不得从业务字段推断部门。
11. DeptSource=unknown 时 OwnerDept 必须为空、SupportDepts 必须为空。
12. 程序/指导书/记录只允许一个 PrimaryChapter；跨 2 章以上必须 MultiChapterReason 且 Confidence≤0.6。
13. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。

## 输出前一致性自检（必须逐条执行；任何一条不满足，先修正字段再输出）
A. 先判 QMS 相关性：财务、人事、销售、考勤、行政后勤等非 QMS 制度 ⇒ DocType=other，ExtractionMode=none，StandardRelevance=irrelevant，Extractability=none，ClauseLinks=[]。
B. 如果是 index/封面/清单/汇编 ⇒ DocType=index，ExtractionMode=index_only，StandardRelevance=reference，Extractability=index_only，PrimaryChapter=""，SecondaryChapters=[]，ClauseLinks 只能 reference 或空。
C. 如果是 audit_record/review_record ⇒ ExtractionMode=audit_profile，StandardRelevance=supporting，PrimaryChapter=""，ClauseLinks 全部 supporting，不得 direct；audit_record 的 OwnerDept 不得是标题括号里的受审核部门（受审核部门放 SupportDepts），review_record 的 OwnerDept 为管理层/最高管理者。
D. 如果是 manual/policy ⇒ ExtractionMode=clause_full，PrimaryChapter=""，SecondaryChapters 列实际覆盖章节。
E. 如果是 procedure/work_instruction ⇒ ExtractionMode=process_profile；SecondaryChapters 长度不得超过 1，且必须与 ClauseLinks 中某个条款的章节一致，否则置空。
F. 如果是 record/form ⇒ ExtractionMode=record_profile；SecondaryChapters 长度不得超过 1。
G. DeptSource=unknown ⇒ OwnerDept="" 且 SupportDepts=[]。
H. SupportDepts 中不得出现“各部门/相关部门/有关部门/总经理/全体员工”等非部门词；每个名称必须以“部/科/室/中心/组”结尾，否则删除。
I. ClauseLinks 中 relation 只能是 primary/supporting/reference；索引/审核/评审不得出现 primary；ClauseNumber 必须清洗为纯编号（只含数字和点），带 Q：/Q:/E：/m：/ISO9001 前缀的必须去掉。
J. SecondaryChapters 中的每个章节必须能被 ClauseLinks 中同章节条款支撑，否则删除；最后再检查一次 DocType 与 ExtractionMode 是否匹配，不匹配时以 ExtractionMode 的硬路由为准，并同步修正 DocType。', Placeholders='{{file_name}},{{folder_path}},{{headings}},{{first_2000}}', Remark='46号文提示词1：每份文档上传后先画像，决定是否逐条款与用哪种提取模式', Sort=10, IsValid=1, Status='active', UpdateTime=NOW(), UpdateBy='migration' WHERE PromptCode='doc_profile' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0;
INSERT INTO cert_prompt_template (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy) SELECT 'doc_profile', '文档画像提示词（类型/章节/过程/部门/提取模式）', '', '', '你是企业体系文档画像器。你的唯一任务，是在提取事实之前判断“这份文档是什么、归哪里、值不值得提取、该用哪种方式提取”。
你不提取条款事实，不做符合性判断，不写审核发现，不写审核证据。

## 输入
文件名：{{file_name}}
目录：{{folder_path}}
标题清单：
{{headings}}

文档开头 2000 字：
<<<DOC
{{first_2000}}
DOC

## 你要回答的 8 个问题
1. 这份文档是什么类型（DocType）
2. 它在体系里承担什么阶段角色（StageRole）
3. 它主要属于哪一章、是否跨章（PrimaryChapter / SecondaryChapters）
4. 它对应哪个过程（PrimaryProcess）
5. 它由哪个部门负责、哪些部门配合（OwnerDept / SupportDepts）
6. 它与标准有没有关系、是什么关系（StandardRelevance）
7. 它能不能提取事实、能提取到什么程度（Extractability）
8. 它应该用哪种提取模式（ExtractionMode）

## 判定顺序
先看文件名和目录，再看标题和开头正文。文件名与正文冲突时以正文为准，但 Confidence 不得高于 0.6。

## DocType
manual 质量手册；policy 方针目标；procedure 程序文件；work_instruction 作业指导书/操作规程；record 记录；form 表单/检查表；index 封面/目录/清单/汇编；audit_record 内审记录/内审检查表；review_record 管理评审记录；other 其他。
输出同时给中文名 DocTypeName。

## ExtractionMode（硬路由）
- manual / policy → clause_full（逐条款）
- procedure / work_instruction → process_profile（过程画像）
- record / form → record_profile（记录画像）
- index → index_only（仅索引）
- audit_record / review_record → audit_profile（审核画像）
- other / 与标准无关 → none
只有 manual / policy 可以用 clause_full。程序、指导书、记录、表单、清单一律不得逐条款。

## 章节与阶段
4 → plan；5 → plan；6 → plan；7 → support；8 → run；9 → check；10 → act。
procedure / work_instruction / record 只允许 1 个 PrimaryChapter，最多 1 个 SecondaryChapter；超过 2 个章节必须写 MultiChapterReason，Confidence ≤ 0.6。
manual / policy：PrimaryChapter=""，SecondaryChapters 列出实际覆盖的 4–10 章；不得把手册主章节写成 4。
audit_record / review_record：PrimaryChapter=""，覆盖章节放 SecondaryChapters；审核/评审记录本身不是某一条款的直接规定。
audit_record：OwnerDept=内审归口部门（体系管理部/管理者代表/内审组），不得填被审核部门；标题括号里的“生产部”等受审核部门只能放 SupportDepts。正文没有归口部门时 OwnerDept=""、DeptSource=unknown。
review_record：OwnerDept=管理层/最高管理者。
index：PrimaryChapter=""，SecondaryChapters=[]；索引不是章节文档。

## 部门
OwnerDept 必须是“负责/主管/归口/使用/执行”的部门或职能，且必须有明确文字依据。
“编制/审核/批准/体系编写小组/编写组/文件编写小组/文控”是作者或审批角色，不是归属部门；只有这些时 OwnerDept=""，DeptSource=unknown，不得把它们填成部门。
如果正文没有部门名，但文档标题/过程非常明确，允许按职能做有限推断，并标 DeptSource=inferred，Confidence ≤ 0.8：
- 来料/进料/巡检/检验/品质 → 品质部
- 生产/工序/设备操作/生产通知 → 生产部
- 采购/供应商 → 采购部
- 仓库/入库/出库/发货 → 仓储部
- 财务/费用/报销 → 财务部
- 培训/考勤/人事 → 行政部或人事部
不得从审批人姓名推断部门；不得把“总经理”当部门。
找不到就写空字符串，DeptSource=unknown，不得猜。
SupportDepts 只写正文明确出现的部门名称；“各部门/相关部门/有关部门/总经理/全体员工”不是部门，必须忽略，不得写进数组。
SupportDepts 中的每个名称必须以“部/科/室/中心/组”结尾，或正文明确写“XX部门”；不符合就删除。
不得从客户名称、订单号、交货日期等业务字段推断部门参与。
DeptSource 只能是 explicit / directory / inferred / unknown：
- explicit：正文明确写“XX部负责/主管/执行”
- directory：只从上传目录名得到
- inferred：从职责、岗位、过程合理推断（Confidence ≤ 0.8）
- unknown：没有依据；此时 OwnerDept 必须为空、SupportDepts 必须为空

## 标准相关性
direct：文档直接规定/覆盖该条款要求（手册、程序、作业指导书）
supporting：文档是该条款的运行记录或二线证据（记录、内审、管理评审）
reference：文档只是索引、清单、封面、汇编，或只引用/提到该条款
irrelevant：财务、人事、销售、考勤等与 QMS 条款无关
unknown：信息不足
Extractability 只能是 full / partial / index_only / none；不得写 structured、complete、yes 等自造值。记录/表单可填 full 或 partial。
索引/封面/清单/汇编一律 reference；内审/管理评审一律 supporting；不得写成 direct。
如果 StandardRelevance=irrelevant，则 DocType=other、ExtractionMode=none、Extractability=none、ClauseLinks=[]；不得归为 work_instruction 或 process_profile。
不要把所有体系文件都写成 direct。

## 条款关联
ClauseLinks 只写文档真正覆盖的条款；只在文中“提到条款号/引用其他文件”的，Relation=reference。
Relation 只能是 primary / supporting / reference。
ClauseNumber 必须是纯编号，只能由数字和点组成（如 4.1、8.5.1）；不得带 Q：/Q:/E：/E:/m：/m:/ISO9001/第X条 等前缀后缀。原文带前缀时先清洗成纯编号；清洗不干净就不要输出该条。
索引/封面/清单/汇编：ClauseLinks 全部 reference，或空数组。
内审/管理评审：ClauseLinks 全部 supporting。
procedure / work_instruction：primary 最多 2 条，不要把文中提到的所有条款都写成 primary。
SecondaryChapters 中的每一个章节，必须能在 ClauseLinks 里找到同一章节的条款；找不到就删除该次章节。

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "DocType": "manual",
  "DocTypeName": "质量手册",
  "ExtractionMode": "clause_full",
  "ExtractionModeName": "逐条款",
  "StageRole": "plan",
  "PrimaryChapter": "",
  "SecondaryChapters": ["4", "5", "6", "7", "8", "9", "10"],
  "MultiChapterReason": "质量手册覆盖完整 QMS 章节",
  "PrimaryProcess": "质量管理体系总体策划与运行控制",
  "OwnerDept": "管理层",
  "SupportDepts": [],
  "DeptSource": "explicit",
  "Purpose": "规定组织环境、相关方、范围和体系过程的管理要求",
  "Scope": "适用于公司质量管理体系",
  "StandardRelevance": "direct",
  "Extractability": "full",
  "ClauseLinks": [{"ClauseNumber": "4.1", "Relation": "primary"}],
  "Confidence": 0.9,
  "Reason": "文件名和正文均表明为质量手册"
}

## 硬规则
1. 不提取任何条款原文，不判断符合性。
2. 不确定就降低 Confidence，不强行分类。
3. 输出必须是合法 JSON，第一个字符 {，最后一个字符 }。
4. 所有文本用简体中文；英文枚举值保持英文。
5. 索引/封面/清单/汇编：DocType=index，ExtractionMode=index_only，StandardRelevance=reference，Extractability=index_only，PrimaryChapter=""；不得写 partial/full/direct。
6. 财务/人事/销售/考勤等非 QMS 制度：DocType=other，ExtractionMode=none，StandardRelevance=irrelevant，Extractability=none，ClauseLinks=[]；不得写 work_instruction。
7. 内审/管理评审：DocType=audit_record/review_record，ExtractionMode=audit_profile，StandardRelevance=supporting，PrimaryChapter=""，覆盖章节放 SecondaryChapters；不得写 direct。
8. 手册/方针：DocType=manual/policy，ExtractionMode=clause_full，PrimaryChapter=""，SecondaryChapters 列实际覆盖章节；不得把主章节写成 4。
9. OwnerDept 不得取“编制/审核/批准/体系编写小组/编写组/文控”；这些是作者/审批角色。
10. SupportDepts 不得写“各部门/相关部门/有关部门/总经理”；不得从业务字段推断部门。
11. DeptSource=unknown 时 OwnerDept 必须为空、SupportDepts 必须为空。
12. 程序/指导书/记录只允许一个 PrimaryChapter；跨 2 章以上必须 MultiChapterReason 且 Confidence≤0.6。
13. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。

## 输出前一致性自检（必须逐条执行；任何一条不满足，先修正字段再输出）
A. 先判 QMS 相关性：财务、人事、销售、考勤、行政后勤等非 QMS 制度 ⇒ DocType=other，ExtractionMode=none，StandardRelevance=irrelevant，Extractability=none，ClauseLinks=[]。
B. 如果是 index/封面/清单/汇编 ⇒ DocType=index，ExtractionMode=index_only，StandardRelevance=reference，Extractability=index_only，PrimaryChapter=""，SecondaryChapters=[]，ClauseLinks 只能 reference 或空。
C. 如果是 audit_record/review_record ⇒ ExtractionMode=audit_profile，StandardRelevance=supporting，PrimaryChapter=""，ClauseLinks 全部 supporting，不得 direct；audit_record 的 OwnerDept 不得是标题括号里的受审核部门（受审核部门放 SupportDepts），review_record 的 OwnerDept 为管理层/最高管理者。
D. 如果是 manual/policy ⇒ ExtractionMode=clause_full，PrimaryChapter=""，SecondaryChapters 列实际覆盖章节。
E. 如果是 procedure/work_instruction ⇒ ExtractionMode=process_profile；SecondaryChapters 长度不得超过 1，且必须与 ClauseLinks 中某个条款的章节一致，否则置空。
F. 如果是 record/form ⇒ ExtractionMode=record_profile；SecondaryChapters 长度不得超过 1。
G. DeptSource=unknown ⇒ OwnerDept="" 且 SupportDepts=[]。
H. SupportDepts 中不得出现“各部门/相关部门/有关部门/总经理/全体员工”等非部门词；每个名称必须以“部/科/室/中心/组”结尾，否则删除。
I. ClauseLinks 中 relation 只能是 primary/supporting/reference；索引/审核/评审不得出现 primary；ClauseNumber 必须清洗为纯编号（只含数字和点），带 Q：/Q:/E：/m：/ISO9001 前缀的必须去掉。
J. SecondaryChapters 中的每个章节必须能被 ClauseLinks 中同章节条款支撑，否则删除；最后再检查一次 DocType 与 ExtractionMode 是否匹配，不匹配时以 ExtractionMode 的硬路由为准，并同步修正 DocType。', '{{file_name}},{{folder_path}},{{headings}},{{first_2000}}', '46号文提示词1：每份文档上传后先画像，决定是否逐条款与用哪种提取模式', 10, 1, 'active', 0, 'PT_DOC_PROFILE_V1', NOW(), 'migration' WHERE NOT EXISTS (SELECT 1 FROM cert_prompt_template WHERE PromptCode='doc_profile' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0);

-- doc_extract_clause_full / 手册/方针逐条款提取提示词
UPDATE cert_prompt_template SET PromptName='手册/方针逐条款提取提示词', Template='你是管理体系手册/方针的条款事实提取器。你只回答“这份文档在这一条款下实际写了什么”，不判断符合性，不写审核证据。
你面对的是一份手册或方针，不是运行记录，因此你只能证明“规定了什么”，不能写成“已经实施”。

## 输入
文档画像：
{{doc_profile}}

标准条款：{{clause_number}} {{clause_title}}
标准要求点：
{{requirement_points}}

本条款完整小节原文（从条款标题到下一个同级/上级标题）：
<<<SECTION
{{section_text}}
SECTION

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "ClauseNumber": "4.1",
  "Title": "理解组织及其环境",
  "SubClauses": [
    {
      "SubClause": "4.1.5",
      "ParentSubClause": "4.1",
      "Text": "原文摘录",
      "Anchor": "原文里逐字存在的连续片段，10~40 字",
      "FactKind": "requirement"
    },
    {
      "SubClause": "4.1.5-a",
      "ParentSubClause": "4.1.5",
      "Text": "原文摘录",
      "Anchor": "原文里逐字存在的连续片段，10~40 字",
      "FactKind": "requirement"
    }
  ],
  "Coverage": {
    "Declared": 5,
    "Covered": 5,
    "Missing": [
      {"SubClause": "4.1.5", "MissingReason": "本节没有写该分点"}
    ]
  },
  "ExtraFacts": [
    {
      "Text": "企业自加的 QMS 范围、地址、不适用说明等原文",
      "Anchor": "原文里逐字存在的连续片段，10~40 字",
      "FactKind": "extra"
    }
  ]
}

## 硬规则（违反任何一条，本次输出作废）
1. 先枚举本条款下所有子条款和 a) b) c) d) 分点，再逐项填；不得只填前几个。
2. 每个子条款/分点必须有独立条目；不得把 4.1.4、4.1.5 合并进 4.1.1–4.1.3，也不得把 a–d 合并成一段。
3. 找不到的，不得省略，写进 Coverage.Missing，并给出 MissingReason。
4. Text 必须是原文摘录，不得改写、概括、翻译；Anchor 必须是原文连续片段。
5. FactKind 只能是 requirement / responsibility / result / record / reference / extra：
   - 规定、要求、原则 → requirement
   - 职责、权限、角色 → responsibility
   - 实际结果、实际清单、实际结论 → result
   - 记录、表单、台账 → record
   - 只引用其他文件/条款 → reference
   - 标准外的企业自加内容 → extra
6. 手册只证明“规定了什么”，不得写成“已实施”“已执行”“已完成”。
7. 绝对不许判断符合性：不写符合、不符合、充分、缺失、满足要求、需要改进。
8. ClauseNumber 必须逐字等于输入条款；SubClause 必须写完整路径（如 4.1.5、4.1.5-a，禁止只写 a/b）；ParentSubClause 必须写最近父条（顶级子条写条款号，分点写父条编号）。
9. Coverage.Declared 必须等于你枚举出的标准子条款/分点总数；Covered 必须等于 SubClauses 中唯一 SubClause 条数；两者不相等时，Missing 必须补齐差额。
10. 子条款带 a)–d) 时，父条目必须保留，即使它只有引出句；父条目只摘录引出句，不得把 a)–d) 正文并入父条目；a)–d) 各独立条目。父条目 Text 不超过 80 字。
11. 同一段原文只能出现在一个条目；父条目与分点不得重复摘录。若父条目没有独立引出句，只写标题句，正文留给分点。
12. 父条目 Text 禁止出现 a)/a）/b)/b）/c)/c）/d)/d）、- a、- b 等分点标记；出现即视为重复，必须截断到第一个分点标记之前。
13. SubClause 只能是数字编号或完整路径（4.1.5、4.1.5-a …）；不得写 QMS、生产地址、不适用说明等标题。这类企业自加内容放 ExtraFacts，FactKind=extra。
14. ExtraFacts 不计入 Coverage.Declared/Covered，也不算 Missing；Coverage 只统计标准条款下的子条款和 a–d 分点。
15. 分点必须挂父条：4.1.3 的 a/b 写 4.1.3-a、4.1.3-b；4.1.5 的 a–d 写 4.1.5-a … 4.1.5-d；不得把两个父条的分点拍平成 a–f。
16. 父条下已有字母分点（如 4.1.3 的 a/b），即使标准 requirement_points 未列，也必须写成 4.1.3-a、4.1.3-b 放 SubClauses，不得放 ExtraFacts；只有无父条的非编号标题（QMS/地址/不适用说明）和超出父条列举的 k) 才放 ExtraFacts。
17. 原文中每个编号子条款都必须保留；即使只有一句话（如 4.3.3），也不得省略。
18. 5.1.1 的标准分点只到 j；k) 及之后放 ExtraFacts，FactKind=extra，不计入 Coverage。
19. Coverage.Declared 必须按 SubClauses 逐条重算：Declared = SubClauses 条数 + Missing 条数；Covered = SubClauses 条数；不得把父条和分点重复计数。

## 其它要求
- Text 每条 20~300 字，按原文顺序；同一段不要重复摘录。
- 如果整节只有标题没有正文，SubClauses 为空，Coverage.Missing 列出全部枚举项，并说明“只有标题，无正文”。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', Placeholders='{{doc_profile}},{{clause_number}},{{clause_title}},{{requirement_points}},{{section_text}}', Remark='46号文提示词2：仅 manual/policy 使用；子条款全覆盖 + Coverage', Sort=20, IsValid=1, Status='active', UpdateTime=NOW(), UpdateBy='migration' WHERE PromptCode='doc_extract_clause_full' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0;
INSERT INTO cert_prompt_template (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy) SELECT 'doc_extract_clause_full', '手册/方针逐条款提取提示词', '', '', '你是管理体系手册/方针的条款事实提取器。你只回答“这份文档在这一条款下实际写了什么”，不判断符合性，不写审核证据。
你面对的是一份手册或方针，不是运行记录，因此你只能证明“规定了什么”，不能写成“已经实施”。

## 输入
文档画像：
{{doc_profile}}

标准条款：{{clause_number}} {{clause_title}}
标准要求点：
{{requirement_points}}

本条款完整小节原文（从条款标题到下一个同级/上级标题）：
<<<SECTION
{{section_text}}
SECTION

## 输出
只输出一个 JSON 对象，不要 markdown 代码块，不要解释：
{
  "ClauseNumber": "4.1",
  "Title": "理解组织及其环境",
  "SubClauses": [
    {
      "SubClause": "4.1.5",
      "ParentSubClause": "4.1",
      "Text": "原文摘录",
      "Anchor": "原文里逐字存在的连续片段，10~40 字",
      "FactKind": "requirement"
    },
    {
      "SubClause": "4.1.5-a",
      "ParentSubClause": "4.1.5",
      "Text": "原文摘录",
      "Anchor": "原文里逐字存在的连续片段，10~40 字",
      "FactKind": "requirement"
    }
  ],
  "Coverage": {
    "Declared": 5,
    "Covered": 5,
    "Missing": [
      {"SubClause": "4.1.5", "MissingReason": "本节没有写该分点"}
    ]
  },
  "ExtraFacts": [
    {
      "Text": "企业自加的 QMS 范围、地址、不适用说明等原文",
      "Anchor": "原文里逐字存在的连续片段，10~40 字",
      "FactKind": "extra"
    }
  ]
}

## 硬规则（违反任何一条，本次输出作废）
1. 先枚举本条款下所有子条款和 a) b) c) d) 分点，再逐项填；不得只填前几个。
2. 每个子条款/分点必须有独立条目；不得把 4.1.4、4.1.5 合并进 4.1.1–4.1.3，也不得把 a–d 合并成一段。
3. 找不到的，不得省略，写进 Coverage.Missing，并给出 MissingReason。
4. Text 必须是原文摘录，不得改写、概括、翻译；Anchor 必须是原文连续片段。
5. FactKind 只能是 requirement / responsibility / result / record / reference / extra：
   - 规定、要求、原则 → requirement
   - 职责、权限、角色 → responsibility
   - 实际结果、实际清单、实际结论 → result
   - 记录、表单、台账 → record
   - 只引用其他文件/条款 → reference
   - 标准外的企业自加内容 → extra
6. 手册只证明“规定了什么”，不得写成“已实施”“已执行”“已完成”。
7. 绝对不许判断符合性：不写符合、不符合、充分、缺失、满足要求、需要改进。
8. ClauseNumber 必须逐字等于输入条款；SubClause 必须写完整路径（如 4.1.5、4.1.5-a，禁止只写 a/b）；ParentSubClause 必须写最近父条（顶级子条写条款号，分点写父条编号）。
9. Coverage.Declared 必须等于你枚举出的标准子条款/分点总数；Covered 必须等于 SubClauses 中唯一 SubClause 条数；两者不相等时，Missing 必须补齐差额。
10. 子条款带 a)–d) 时，父条目必须保留，即使它只有引出句；父条目只摘录引出句，不得把 a)–d) 正文并入父条目；a)–d) 各独立条目。父条目 Text 不超过 80 字。
11. 同一段原文只能出现在一个条目；父条目与分点不得重复摘录。若父条目没有独立引出句，只写标题句，正文留给分点。
12. 父条目 Text 禁止出现 a)/a）/b)/b）/c)/c）/d)/d）、- a、- b 等分点标记；出现即视为重复，必须截断到第一个分点标记之前。
13. SubClause 只能是数字编号或完整路径（4.1.5、4.1.5-a …）；不得写 QMS、生产地址、不适用说明等标题。这类企业自加内容放 ExtraFacts，FactKind=extra。
14. ExtraFacts 不计入 Coverage.Declared/Covered，也不算 Missing；Coverage 只统计标准条款下的子条款和 a–d 分点。
15. 分点必须挂父条：4.1.3 的 a/b 写 4.1.3-a、4.1.3-b；4.1.5 的 a–d 写 4.1.5-a … 4.1.5-d；不得把两个父条的分点拍平成 a–f。
16. 父条下已有字母分点（如 4.1.3 的 a/b），即使标准 requirement_points 未列，也必须写成 4.1.3-a、4.1.3-b 放 SubClauses，不得放 ExtraFacts；只有无父条的非编号标题（QMS/地址/不适用说明）和超出父条列举的 k) 才放 ExtraFacts。
17. 原文中每个编号子条款都必须保留；即使只有一句话（如 4.3.3），也不得省略。
18. 5.1.1 的标准分点只到 j；k) 及之后放 ExtraFacts，FactKind=extra，不计入 Coverage。
19. Coverage.Declared 必须按 SubClauses 逐条重算：Declared = SubClauses 条数 + Missing 条数；Covered = SubClauses 条数；不得把父条和分点重复计数。

## 其它要求
- Text 每条 20~300 字，按原文顺序；同一段不要重复摘录。
- 如果整节只有标题没有正文，SubClauses 为空，Coverage.Missing 列出全部枚举项，并说明“只有标题，无正文”。
- 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', '{{doc_profile}},{{clause_number}},{{clause_title}},{{requirement_points}},{{section_text}}', '46号文提示词2：仅 manual/policy 使用；子条款全覆盖 + Coverage', 20, 1, 'active', 0, 'PT_DOC_EXTRACT_CLAUSE_FULL_V1', NOW(), 'migration' WHERE NOT EXISTS (SELECT 1 FROM cert_prompt_template WHERE PromptCode='doc_extract_clause_full' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0);

-- doc_extract_process_profile / 程序/指导书过程画像提示词
UPDATE cert_prompt_template SET PromptName='程序/指导书过程画像提示词', Template='你是企业体系程序文件/作业指导书的过程画像器。你不逐条款转述，也不判断符合性；你只把这份文件还原成“谁在什么过程里做什么、用什么、产出什么记录”。

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
  "Processes": [
    {
      "ProcessName": "文件控制",
      "OwnerDept": "品质部",
      "SupportDepts": ["生产部", "采购部"],
      "Purpose": "规定体系文件的编制、审批、发放、更改和作废",
      "Scope": "适用于公司所有体系文件",
      "Inputs": ["文件编制申请", "外来文件"],
      "Activities": [
        {"Activity": "文件编制", "Responsible": "编制部门", "OutputRecord": "文件审批单"}
      ],
      "Responsibilities": [
        {"Role": "文控", "Duty": "负责文件发放、回收和归档"}
      ],
      "Controls": ["文件必须经审批后发放"],
      "Equipment": [],
      "Parameters": [],
      "AcceptanceCriteria": [],
      "OutputRecords": ["文件审批单", "文件发放回收记录"],
      "Interfaces": ["管理评审", "内部审核"],
      "ClauseLinks": [{"ClauseNumber": "7.5.2", "Relation": "primary"}]
    }
  ]
}

## 硬规则
1. 不要求输出条款全文，不要求逐条款；只输出过程画像。
2. 每个 Activity 尽量给出 Responsible 和 OutputRecord；没有就留空，不得编造。
3. 程序文件如果覆盖多个子过程，输出多个 Processes；不得把多个过程硬压成一个。
4. 作业指导书必须输出：工序/岗位、操作步骤、设备、参数、接收准则、输出记录。
5. ClauseLinks 只标 primary / supporting / reference，不把文中提到的所有条款都写成 primary。
6. OwnerDept 只能来自文件明确写出的职责/审批/发放范围；找不到写空字符串并降低 Confidence。
7. 不写“符合/不符合/充分/缺失”，不写审核结论。
8. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', Placeholders='{{doc_profile}},{{document_text}}', Remark='46号文提示词3：procedure/work_instruction 使用；不逐条款', Sort=30, IsValid=1, Status='active', UpdateTime=NOW(), UpdateBy='migration' WHERE PromptCode='doc_extract_process_profile' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0;
INSERT INTO cert_prompt_template (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy) SELECT 'doc_extract_process_profile', '程序/指导书过程画像提示词', '', '', '你是企业体系程序文件/作业指导书的过程画像器。你不逐条款转述，也不判断符合性；你只把这份文件还原成“谁在什么过程里做什么、用什么、产出什么记录”。

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
  "Processes": [
    {
      "ProcessName": "文件控制",
      "OwnerDept": "品质部",
      "SupportDepts": ["生产部", "采购部"],
      "Purpose": "规定体系文件的编制、审批、发放、更改和作废",
      "Scope": "适用于公司所有体系文件",
      "Inputs": ["文件编制申请", "外来文件"],
      "Activities": [
        {"Activity": "文件编制", "Responsible": "编制部门", "OutputRecord": "文件审批单"}
      ],
      "Responsibilities": [
        {"Role": "文控", "Duty": "负责文件发放、回收和归档"}
      ],
      "Controls": ["文件必须经审批后发放"],
      "Equipment": [],
      "Parameters": [],
      "AcceptanceCriteria": [],
      "OutputRecords": ["文件审批单", "文件发放回收记录"],
      "Interfaces": ["管理评审", "内部审核"],
      "ClauseLinks": [{"ClauseNumber": "7.5.2", "Relation": "primary"}]
    }
  ]
}

## 硬规则
1. 不要求输出条款全文，不要求逐条款；只输出过程画像。
2. 每个 Activity 尽量给出 Responsible 和 OutputRecord；没有就留空，不得编造。
3. 程序文件如果覆盖多个子过程，输出多个 Processes；不得把多个过程硬压成一个。
4. 作业指导书必须输出：工序/岗位、操作步骤、设备、参数、接收准则、输出记录。
5. ClauseLinks 只标 primary / supporting / reference，不把文中提到的所有条款都写成 primary。
6. OwnerDept 只能来自文件明确写出的职责/审批/发放范围；找不到写空字符串并降低 Confidence。
7. 不写“符合/不符合/充分/缺失”，不写审核结论。
8. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', '{{doc_profile}},{{document_text}}', '46号文提示词3：procedure/work_instruction 使用；不逐条款', 30, 1, 'active', 0, 'PT_DOC_EXTRACT_PROCESS_PROFILE_V1', NOW(), 'migration' WHERE NOT EXISTS (SELECT 1 FROM cert_prompt_template WHERE PromptCode='doc_extract_process_profile' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0);

-- doc_extract_record_profile / 记录/表单记录画像提示词
UPDATE cert_prompt_template SET PromptName='记录/表单记录画像提示词', Template='你是企业体系记录/表单的记录画像器。你不逐条款转述，也不判断符合性；你只还原这份记录“是什么过程的记录、哪个部门填、有哪些字段、覆盖什么期间/批次、谁签署、关联哪份文件”。

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
  "Records": [
    {
      "RecordName": "生产日报表",
      "ProcessName": "生产和服务提供",
      "OwnerDept": "生产部",
      "FormFields": ["日期", "产品名称", "计划数量", "实际数量", "操作人"],
      "Period": "2026-10",
      "Batch": "",
      "Sample": "",
      "Signoff": ["操作人", "班组长"],
      "LinkedDoc": "生产和服务提供控制程序",
      "ClauseLinks": [{"ClauseNumber": "8.5.1", "Relation": "supporting"}]
    }
  ]
}

## 硬规则
1. 只读表头、字段、样例行；不得编造记录值、日期、批次、人员。
2. 记录核心是过程、部门、字段、期间、批次/样本、签署、关联文件。
3. 一个文件多张表/多类记录，输出多个 Records。
4. 记录不输出条款原文，不做条款转述。
5. 空表只写字段和记录模板，不写“已填写”“已执行”。
6. 不写符合/不符合/充分/缺失，不写审核结论。
7. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', Placeholders='{{doc_profile}},{{document_text}}', Remark='46号文提示词4：record/form 使用；只还原记录结构', Sort=40, IsValid=1, Status='active', UpdateTime=NOW(), UpdateBy='migration' WHERE PromptCode='doc_extract_record_profile' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0;
INSERT INTO cert_prompt_template (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy) SELECT 'doc_extract_record_profile', '记录/表单记录画像提示词', '', '', '你是企业体系记录/表单的记录画像器。你不逐条款转述，也不判断符合性；你只还原这份记录“是什么过程的记录、哪个部门填、有哪些字段、覆盖什么期间/批次、谁签署、关联哪份文件”。

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
  "Records": [
    {
      "RecordName": "生产日报表",
      "ProcessName": "生产和服务提供",
      "OwnerDept": "生产部",
      "FormFields": ["日期", "产品名称", "计划数量", "实际数量", "操作人"],
      "Period": "2026-10",
      "Batch": "",
      "Sample": "",
      "Signoff": ["操作人", "班组长"],
      "LinkedDoc": "生产和服务提供控制程序",
      "ClauseLinks": [{"ClauseNumber": "8.5.1", "Relation": "supporting"}]
    }
  ]
}

## 硬规则
1. 只读表头、字段、样例行；不得编造记录值、日期、批次、人员。
2. 记录核心是过程、部门、字段、期间、批次/样本、签署、关联文件。
3. 一个文件多张表/多类记录，输出多个 Records。
4. 记录不输出条款原文，不做条款转述。
5. 空表只写字段和记录模板，不写“已填写”“已执行”。
6. 不写符合/不符合/充分/缺失，不写审核结论。
7. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', '{{doc_profile}},{{document_text}}', '46号文提示词4：record/form 使用；只还原记录结构', 40, 1, 'active', 0, 'PT_DOC_EXTRACT_RECORD_PROFILE_V1', NOW(), 'migration' WHERE NOT EXISTS (SELECT 1 FROM cert_prompt_template WHERE PromptCode='doc_extract_record_profile' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0);

-- doc_extract_index / 封面/清单/汇编索引提示词
UPDATE cert_prompt_template SET PromptName='封面/清单/汇编索引提示词', Template='你是企业体系文件索引提取器。你只做索引和引用关系，不产出事实，不进入审核发现主表。

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
  "IndexEntries": [
    {
      "Title": "文件控制程序",
      "FileName": "01文件控制程序.doc",
      "DocNo": "QMS/CX-01",
      "Relation": "reference"
    }
  ]
}

## 硬规则
1. 只提取文件名称、文件编号、标题、版本/日期（如有）和引用关系。
2. 不提取条款原文，不写过程职责，不写记录字段，不判断符合性。
3. 清单/封面/汇编本身不是事实证据，只能作为文件索引。
4. 找不到文件编号就留空，不得编造。
5. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', Placeholders='{{doc_profile}},{{document_text}}', Remark='46号文提示词5：index 使用；只做索引，不产出事实', Sort=50, IsValid=1, Status='active', UpdateTime=NOW(), UpdateBy='migration' WHERE PromptCode='doc_extract_index' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0;
INSERT INTO cert_prompt_template (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy) SELECT 'doc_extract_index', '封面/清单/汇编索引提示词', '', '', '你是企业体系文件索引提取器。你只做索引和引用关系，不产出事实，不进入审核发现主表。

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
  "IndexEntries": [
    {
      "Title": "文件控制程序",
      "FileName": "01文件控制程序.doc",
      "DocNo": "QMS/CX-01",
      "Relation": "reference"
    }
  ]
}

## 硬规则
1. 只提取文件名称、文件编号、标题、版本/日期（如有）和引用关系。
2. 不提取条款原文，不写过程职责，不写记录字段，不判断符合性。
3. 清单/封面/汇编本身不是事实证据，只能作为文件索引。
4. 找不到文件编号就留空，不得编造。
5. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', '{{doc_profile}},{{document_text}}', '46号文提示词5：index 使用；只做索引，不产出事实', 50, 1, 'active', 0, 'PT_DOC_EXTRACT_INDEX_V1', NOW(), 'migration' WHERE NOT EXISTS (SELECT 1 FROM cert_prompt_template WHERE PromptCode='doc_extract_index' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0);

-- doc_extract_audit / 内审/管理评审二线证据提示词
UPDATE cert_prompt_template SET PromptName='内审/管理评审二线证据提示词', Template='你是企业内审/管理评审的二线证据画像器。你只提取审核/评审活动本身的事实，不替代 8.5.1 等一线过程记录，不判断体系符合性。

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
  "AuditProfile": {
    "AuditType": "internal_audit",
    "Date": "2026-09-20",
    "Auditors": ["张三", "李四"],
    "Scope": "管理层、生产部、品质部",
    "ClausesCovered": ["4.1", "8.5.1"],
    "Findings": [
      {"ClauseNumber": "8.5.1", "Finding": "原文审核记录/评审记录中的事实陈述", "Evidence": "内审检查表第 3 页/第 3 行", "RecordMark": "原文判定栏，如 OK/NG/空白"}
    ],
    "RecordConclusions": ["原文结论，逐字或近逐字；没有就空数组"]
  }
}

## 硬规则
1. AuditType 只能是 internal_audit / management_review。
2. Findings 只写原文审核记录/评审记录中的事实陈述；不得改写为“企业已实施”。
3. RecordMark 只抄原文判定栏；没有就写空字符串；不得把 OK/NG 当成系统结论。
4. 不输出 Conclusion / 符合 / 不符合 / 充分 / 缺失 / 需改进项 / 一般不符合 / 建议 等审核结论；RecordConclusions 只抄原文结论，不做二次判断。
5. 管理评审中的“存在的主要问题”可以放 Findings，但只抄原文，不得自行分类为“需改进项”。
6. 内审/管理评审只作二线证据；不替代 8.5.1 等一线过程记录。
7. 不编造人名、日期、结论；没有就留空数组或空字符串。
8. ClausesCovered 只写纯编号，不带 Q：/Q:/E：/m： 前缀。
9. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', Placeholders='{{doc_profile}},{{document_text}}', Remark='46号文提示词6：audit_record/review_record 使用；只作二线证据', Sort=60, IsValid=1, Status='active', UpdateTime=NOW(), UpdateBy='migration' WHERE PromptCode='doc_extract_audit' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0;
INSERT INTO cert_prompt_template (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy) SELECT 'doc_extract_audit', '内审/管理评审二线证据提示词', '', '', '你是企业内审/管理评审的二线证据画像器。你只提取审核/评审活动本身的事实，不替代 8.5.1 等一线过程记录，不判断体系符合性。

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
  "AuditProfile": {
    "AuditType": "internal_audit",
    "Date": "2026-09-20",
    "Auditors": ["张三", "李四"],
    "Scope": "管理层、生产部、品质部",
    "ClausesCovered": ["4.1", "8.5.1"],
    "Findings": [
      {"ClauseNumber": "8.5.1", "Finding": "原文审核记录/评审记录中的事实陈述", "Evidence": "内审检查表第 3 页/第 3 行", "RecordMark": "原文判定栏，如 OK/NG/空白"}
    ],
    "RecordConclusions": ["原文结论，逐字或近逐字；没有就空数组"]
  }
}

## 硬规则
1. AuditType 只能是 internal_audit / management_review。
2. Findings 只写原文审核记录/评审记录中的事实陈述；不得改写为“企业已实施”。
3. RecordMark 只抄原文判定栏；没有就写空字符串；不得把 OK/NG 当成系统结论。
4. 不输出 Conclusion / 符合 / 不符合 / 充分 / 缺失 / 需改进项 / 一般不符合 / 建议 等审核结论；RecordConclusions 只抄原文结论，不做二次判断。
5. 管理评审中的“存在的主要问题”可以放 Findings，但只抄原文，不得自行分类为“需改进项”。
6. 内审/管理评审只作二线证据；不替代 8.5.1 等一线过程记录。
7. 不编造人名、日期、结论；没有就留空数组或空字符串。
8. ClausesCovered 只写纯编号，不带 Q：/Q:/E：/m： 前缀。
9. 只输出 JSON：第一个字符必须是 {，最后一个字符必须是 }。', '{{doc_profile}},{{document_text}}', '46号文提示词6：audit_record/review_record 使用；只作二线证据', 60, 1, 'active', 0, 'PT_DOC_EXTRACT_AUDIT_V1', NOW(), 'migration' WHERE NOT EXISTS (SELECT 1 FROM cert_prompt_template WHERE PromptCode='doc_extract_audit' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0);

-- 验证：应为 6 条且 IsValid=1
SELECT PromptCode, PromptName, LENGTH(Template) AS TemplateLength, IsValid, Status
FROM cert_prompt_template
WHERE PromptCode IN ('doc_profile','doc_extract_clause_full','doc_extract_process_profile','doc_extract_record_profile','doc_extract_index','doc_extract_audit')
  AND IsDeleted=0
ORDER BY Sort;
