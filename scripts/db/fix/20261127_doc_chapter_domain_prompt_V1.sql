SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 2026-11-27 文档大章节归属「语义档」提示词（doc_chapter_domain）
-- 依据：docs/80-v4/27-提示词单一化与文档大章节归属-执行计划-V1.md §五 批3-4
-- 口径：正文与 DocExtractPromptService.FallbackChapterDomainTemplate 逐字一致；
--       仅在「确定性档(ClauseNumbers/Toc) + 关键词档(FromText)」均判不出（源=unknown）时调用；
--       ⛔ 判不出必须落 CD_UNKNOWN，不许硬猜。
-- 幂等：WHERE NOT EXISTS 命中已有平台级行即跳过。

INSERT INTO cert_prompt_template
  (PromptCode, PromptName, OrgCode, ScopeKey, Template, Placeholders, Remark, Sort, IsValid, Status, IsDeleted, Code, CreateTime, CreateBy)
SELECT 'doc_chapter_domain', '文档大章节归属判定提示词', '', '', '你是 ISO 管理体系文档的「大章节归属」判定器。只判断这份文档主要属于哪几个大章节，不做符合性判断、不提取条款事实。

## 可选章域（只能从这里选，可多选，最多 3 个）
CD_ENV 组织环境（第4章）：理解组织及其环境、相关方需求与期望、管理体系范围
CD_LEAD 领导作用（第5章）：领导承诺、方针、岗位职责与权限、沟通协商
CD_DECIDE 策划（第6章）：风险和机遇、目标及其实现策划、变更策划
CD_SUPPORT 支持（第7章）：资源、能力与意识、培训、基础设施、监视和测量资源、组织知识、成文信息（文件控制/记录控制）
CD_RUN 运行（第8章）：运行策划与控制、设计开发、采购与外部提供、生产与服务提供、检验与放行、不合格输出、标识追溯防护、交付
CD_ASSESS 绩效评价（第9章）：监视测量分析评价、内部审核、管理评审、顾客满意、合规性评价
CD_IMPROVE 改进（第10章）：不符合与纠正措施、持续改进

## 判定规则（记录/表单类重点）
- 记录/表单常无条款号、无正文，只能靠「表头字段 + 抽样行 + 部门 + 文件名」的语义判断归属。
- 「培训计划 / 培训签到 / 岗位能力评价 / 检验人员任命 / 质量意识」→ CD_SUPPORT（7.2 能力、7.5 成文信息）。
- 「进料检验 / 出厂检验 / 制程巡检 / 首件 / 质检日报 / 生产日报 / 发货单 / 装箱单 / 生产通知单」→ CD_RUN（8.5 生产与服务提供、8.6 放行）。
- 「文件借阅 / 文件更改 / 文件销毁 / 外来文件清单」→ CD_SUPPORT（7.5.3 成文信息控制）。
- 与 ISO 管理体系无关的行政制度（考勤、劳动纪律、请销假、宿舍、报销等）⇒ Domains=["CD_UNKNOWN"]。
- 证据不足、判断不了 ⇒ Domains=["CD_UNKNOWN"]，⛔ 不许硬猜、⛔ 不许默认归 CD_RUN。

## 输入
文档类型：{{doc_type}}
语义骨架（简介 / 目的 / 范围 / 目录标题 / 表头抽样行）：
<<<DOC
{{abstract_text}}
DOC

## 输出（只输出一个 JSON 对象；第一个字符 {，最后一个字符 }；不要代码块、不要解释）
{"Domains":["CD_XXX"],"Confidence":0.0,"Reason":"一句话依据"}
Confidence 0~1：语义明确对应某章 ⇒ 0.8 以上；相关但不充分 ⇒ 0.4~0.7；只能靠猜测 ⇒ 0.4 以下并改判 CD_UNKNOWN。',
  '{{doc_type}},{{abstract_text}}',
  '27号文批3-4：仅确定性档+关键词档均判不出（源=unknown）时调用；判不出落 CD_UNKNOWN',
  5, 1, 'active', 0, 'PT_DOC_CHAPTER_DOMAIN_V1', NOW(), 'migration'
WHERE NOT EXISTS (
  SELECT 1 FROM cert_prompt_template
  WHERE PromptCode='doc_chapter_domain' AND OrgCode='' AND ScopeKey='' AND IsDeleted=0
);

-- 验证
SELECT PromptCode, PromptName, IsValid, LENGTH(Template) AS TemplateLen, Placeholders
FROM cert_prompt_template
WHERE PromptCode='doc_chapter_domain' AND IsDeleted=0;
