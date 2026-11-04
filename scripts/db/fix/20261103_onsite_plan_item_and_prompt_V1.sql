-- ============================================================
-- 2026-11-03 · 入场检查内容（cert_onsite_plan_item）+ 生成提示词
-- 幂等脚本：可重复执行
-- ============================================================
SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

-- 一、入场检查内容表
-- 设计来源：docs/70-V2设计/48-认证准备与认证检查-业务设计-V3.md §9
-- 用途：认证准备阶段为每个检查项定义「查什么 / 怎么查」（可多条），
--       认证检查阶段据此生成入场计划。
CREATE TABLE IF NOT EXISTS cert_onsite_plan_item (
    Id BIGINT NOT NULL AUTO_INCREMENT,
    Code VARCHAR(50) NOT NULL,
    PlanCode VARCHAR(50) NOT NULL,
    CheckItemCode VARCHAR(50) NOT NULL,
    SeqNo INT NOT NULL DEFAULT 0,
    CheckWhat VARCHAR(500) NOT NULL,
    CheckMethod VARCHAR(50) NULL,
    TargetDept VARCHAR(200) NULL,
    ContactCode VARCHAR(50) NULL,
    ContactName VARCHAR(100) NULL,
    AuditorCode VARCHAR(64) NULL,
    AuditorName VARCHAR(100) NULL,
    SampleRequirement VARCHAR(200) NULL,
    ExpectedFinding VARCHAR(500) NULL,
    ClauseCodes VARCHAR(500) NULL,
    -- manual / ai
    Source VARCHAR(20) NOT NULL DEFAULT 'manual',
    -- planned / done / skipped
    Status VARCHAR(20) NOT NULL DEFAULT 'planned',
    Sort INT NOT NULL DEFAULT 0,
    Remark VARCHAR(500) NULL,
    CreateBy VARCHAR(64) NULL,
    CreateTime DATETIME NULL,
    UpdateBy VARCHAR(64) NULL,
    UpdateTime DATETIME NULL,
    DeleteBy VARCHAR(64) NULL,
    DeleteTime DATETIME NULL,
    IsDeleted TINYINT NOT NULL DEFAULT 0,
    IsValid INT NOT NULL DEFAULT 1,
    PRIMARY KEY (Id),
    UNIQUE KEY uk_onsite_plan_code (Code),
    KEY idx_onsite_plan_check_item (CheckItemCode),
    KEY idx_onsite_plan_plan (PlanCode)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

-- 二、入场检查内容生成提示词（PromptCode = onsite_plan）
INSERT INTO wf_prompt_template
    (Code, PromptCode, PromptName, PromptType, Template, Description,
     Version, IsActive, IsValid, IsDeleted, CreateTime)
SELECT
    REPLACE(UUID(), '-', ''),
    'onsite_plan',
    '入场检查内容生成',
    'onsite_plan',
    '你是资深 ISO 管理体系审核员。请针对下列检查项，生成「入场检查内容」清单（审核员到场后具体查什么、怎么查）。

【检查项条款信息】
{{ClauseInfo}}

【受检部门】
{{dept_names}}

输出严格 JSON（不要输出任何解释文字、不要用 Markdown 代码块）：
{
  "items": [
    {
      "check_what": "具体检查内容（一句话，可操作、可验证）",
      "check_method": "文件查阅|现场观察|人员访谈|抽样验证|记录核对（可组合，如：文件查阅+人员访谈）",
      "target_dept": "应检查的部门或岗位（没有则留空）",
      "sample_requirement": "抽样要求（如：抽 3 份近 6 个月记录；没有则留空）",
      "expected_finding": "期望看到的合规证据（没有则留空）",
      "clause_numbers": "对应条款号，多个用英文逗号分隔"
    }
  ]
}

要求：
1. 生成 3–8 条，每条只针对一个检查点，不要重复条款原文；
2. 检查方式必须落到具体手段（文件 / 现场 / 访谈 / 抽样）；
3. 只能基于给定条款信息推断，没有依据不要编造；
4. clause_numbers 必须来自检查项条款信息。',
    '认证准备阶段为每个检查项生成入场检查内容（查什么 / 怎么查），供审核员编辑与入场使用。',
    1, 1, 1, 0, NOW()
WHERE NOT EXISTS (
    SELECT 1 FROM wf_prompt_template WHERE PromptCode = 'onsite_plan'
);

-- 三、占位符纠偏（幂等）：代码 BuildPrompt 的键是 {{dept_names}}，
--   早期插入过的 {{DeptNames}} 不会被替换 ⇒ 正文残留占位符。
UPDATE wf_prompt_template
SET Template = REPLACE(Template, '{{DeptNames}}', '{{dept_names}}'),
    UpdateTime = NOW()
WHERE PromptCode = 'onsite_plan'
  AND Template LIKE '%{{DeptNames}}%';
