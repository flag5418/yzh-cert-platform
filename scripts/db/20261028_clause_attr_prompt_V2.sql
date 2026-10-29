-- ============================================================================
-- 20261028_clause_attr_prompt_V2.sql — 「条款属性预生成」提示词 **V2**（★ 幂等）
-- ============================================================================
-- 为什么出 V2（2026-10-28 抽 6 条评审的结论，见
--   `docs/70-V2设计/实施-TODO-属性池与条款配置-V1.md` §5.11）：
--
--   ① `Obligation` **6 条全是 `shall`**，连「范围 / 规范性引用文件 / 术语和定义」这类
--      **一个义务动词都没有**的前置条款也是 `shall`。
--      根因：V1 判据 1 只写了「有措辞时怎么取」，**没写「一个都没有时取什么」** ⇒ 模型默认填 `shall`。
--      V2 补：无义务动词 ⇒ `note`，并显式禁止「按标准惯例默认 shall」。
--
--   ② `Artifacts` / `EvidenceChain` 产出形态不统一：有的给「JSON 数组 + 字段清单」，
--      有的写成「请严格依据以下要求…」散文 ⇒ 下游按契约解析会分叉。
--      V2 补：这两段提示词的**第一句必须是输出契约**（逐字给出模板），⛔ 禁空泛开头。
--
-- ★ 幂等口径：`PromptCode` 上有唯一键 `uk_prompt_code` ⇒ `ON DUPLICATE KEY UPDATE Id = Id`
--   保证重跑不重复插入、也**不覆盖**人工调优过的正文。
-- ⚠️ 停用旧版只停 `Version < 2`（⛔ 不能写 `IsActive=1` 全停 —— 那样重跑一次脚本
--    会把 V2 自己停掉，而 INSERT 因唯一键冲突什么都不做 ⇒ 提示词凭空消失）。
--
-- 定位口径：`IPromptWorkbench.ResolveActiveAsync("clause_attr", null)`
--   ⇒ 平台级（StandardCode 为空）+ IsActive=1；同名多条时取第一条 ⇒ **必须只有一条 active**。
-- ============================================================================

-- ① 先停用旧版（只停 v1 及更早）
UPDATE `wf_prompt_template`
   SET `IsActive` = 0, `UpdateTime` = NOW()
 WHERE `PromptType` = 'clause_attr'
   AND `Version` < 2
   AND `IsActive` = 1;

-- ② 插入 V2（已存在则不动）
INSERT INTO `wf_prompt_template`
  (`Code`, `OrgCode`, `Status`, `Sort`, `Remark`, `PromptCode`, `PromptName`, `PromptType`,
   `SkillTarget`, `StandardCode`, `Template`, `ModelName`, `MaxTokens`, `Temperature`,
   `Description`, `Version`, `IsActive`, `CreateBy`, `CreateTime`, `IsDeleted`, `IsValid`)
VALUES
  (REPLACE(UUID(), '-', ''), NULL, 'active', 100,
   'V2 条款属性预生成（8 属性）',
   'clause_attr_v2', '条款属性预生成', 'clause_attr',
   NULL, NULL,
   '你是 ISO 管理体系认证的资深审核技术专家。你的任务：读一条标准条款，产出这条条款的「属性配置」JSON。

## 输入

- 标准：{{standard_name}}
- 条款号：{{clause_number}}
- 条款标题：{{clause_title}}
- 条款正文：
{{clause_content}}

## 输出

只输出一个 JSON 对象，不要 markdown 代码块，不要任何解释文字。字段如下（全部必填，无内容用空串或空数组）：

{
  "ClauseIntent": "一句话说明这条款到底在管什么（不超过 40 字）",
  "Obligation": "shall | should | may | note",
  "Applicability": "general | conditional",
  "ApplicabilityCondition": "有条件时填触发条件的原文要点，通用条款填空串",
  "Process": ["production"],
  "Artifacts": "……企业侧资料提示词……",
  "EvidenceChain": "……审核员侧取证提示词……",
  "Severity": "critical | major | minor"
}

## 各字段判据

1. **Obligation（约束强度）** —— 看条款正文的措辞：
   - 「应 / 必须 / shall」⇒ `shall`
   - 「宜 / 建议 / should」⇒ `should`
   - 「可 / 可以 / may」⇒ `may`
   - 纯说明性「注 / 注：」⇒ `note`
   - ★ **条款正文里一个义务动词都没有** ⇒ `note`。
     典型是前置条款：`1 范围`（只有「本文件规定了…适用于…」）、
     `2 规范性引用文件`（只有「下列文件…构成本文件必不可少的条款」）、
     `3 术语和定义`（只有「下列术语和定义适用于本文件」）。
     ⛔ **不许**因为「标准里通常都写 shall」就默认填 `shall` —— 没有动词就是 `note`。
   ⚠️ 判据顺序：**先找义务动词**，找到才按上面四档判；一个都没有才落到 `note`。
   ⚠️ 一条条款里出现多种措辞时，取**主句**（第一句带义务动词的话）的措辞。

2. **Applicability（适用性）** —— 这条要求是不是所有企业都得做：
   - 所有该标准的企业都必须做 ⇒ `general`
   - 只在特定条件下才适用（如「当产品有设计职责时」「当组织采用外包过程时」「适用时」「如有时」）⇒ `conditional`

3. **ApplicabilityCondition** —— 仅当 `Applicability = conditional` 时填：把触发条件写成一句可判断的话（如「组织承担产品设计职责」）。`general` 时填空串。

4. **Process（涉及过程）** —— 从下面 10 个受控码里选 1~3 个，按相关度排序，⛔ 不许自造：
   `production`(生产) `sales`(销售) `design`(设计) `purchase`(采购) `inspection`(检验)
   `metrology`(计量) `warehouse`(仓储) `after_sales`(售后) `hr`(人力) `management`(管理)
   ⚠️ 按**条款管的是哪个业务过程**选，⛔ 不要所有条款都填 `management`：
   例如「8.5.1 生产和服务提供的控制」⇒ `production`；「7.1.5 监视和测量资源」⇒ `metrology`；
   「8.4 外部提供的过程、产品和服务的控制」⇒ `purchase`；「7.2 能力」⇒ `hr`。

5. **Severity（条款级严重度）** —— 违反这条通常算几级不符合：
   - `critical`：涉及法规强制要求、产品安全有效、证书真实性（如医疗器械的上市许可、灭菌、可追溯）
   - `major`：涉及体系运行的关键过程控制（如设计控制、采购控制、不合格品控制、内审与管理评审）
   - `minor`：涉及文件措辞、记录格式、职责描述的完整性

6. **Artifacts（企业侧资料提示词）** —— 见下方「Artifacts 要求」。

7. **EvidenceChain（审核员侧取证提示词）** —— 见下方「EvidenceChain 要求」。

## Artifacts 要求（企业侧 · 可公开给企业）

产出一段**提示词正文**（不是结论）。将来这条提示词会连同企业提交的资料一起喂给模型，让模型回答「企业交的材料里，满足这条要求的成文信息有哪些」。因此这段提示词必须：

① 明确要求模型**内部输出结构化 JSON 数组**，元素结构固定为：
   `{"Kind":"材料类型码","FileName":"文件名","DeviceNo":"设备/器具编号，无则省略","ValidUntil":"有效期至，无则省略","Complete":true或false}`
   其中 `Kind` 只能取这 10 个码之一：`ledger`(台账) `certificate`(证书) `procedure`(程序文件)
   `record`(记录) `list`(清单) `report`(报告) `plan`(计划) `license`(资质证照) `mark`(标识) `other`(其他)
② 明确要求**只报「读到了什么」**，⛔ 不许判断符合/不符合，⛔ 不许给整改建议；
③ 明确要求**找不到就交空数组**，⛔ 不许推测、不许编造文件名；
④ 用条款原文里的关键词描述要找的东西（例如本条讲「监视和测量资源」，就写清要找「计量器具台账 / 校准证书 / 检定记录」），但**只说找什么，不说怎么抽样**。
⑤ ★ **这段提示词的第一句必须是输出契约**，逐字照下面这个句式写（只替换【】里的内容）：
   「只输出 JSON 数组，不要任何解释文字。数组每个元素包含：Kind（【本条适用的材料类型码】）、FileName、DeviceNo（无则省略）、ValidUntil（无则省略）、Complete。」
   之后再用 1~3 句写「要找什么」，必须带上条款原文关键词。
   ⛔ **不许**用「请严格依据以下要求」「请从企业提交资料中识别…」这类空泛开头 ——
   下游要按这个契约解析，开头不一致就解析不了。

⚠️ 这段提示词会被企业看到（它是企业准备资料的清单），所以 ⛔ 绝不能写取证手法、抽样数量、访谈问题。

## EvidenceChain 要求（审核员侧 · 取证剧本，⛔ 不提前给企业）

产出一段**提示词正文**（不是结论）。将来这条提示词给审核员用，让模型生成「到这家企业该怎么取证」的步骤。因此这段提示词必须：

① 明确要求模型**内部输出结构化 JSON 数组**，元素结构固定为：
   `{"Kind":"site|record|interview|doc","Action":"做什么","Target":"去哪个部门/看哪份文件/问谁","Sample":"抽多少（如 3 个样本/近 12 个月），无则省略","Finding":"实际发现，取证时填写，此处留空"}`
   `Kind` 含义：`site`=现场观察 / `record`=查记录 / `interview`=访谈 / `doc`=查文件
② 明确要求**只描述「去哪里、看什么、抽多少、问什么」**，⛔ 不许预先写结论，⛔ 不许写「符合/不符合」；
③ 明确要求**找不到证据就留空**，⛔ 不许推测；
④ 至少覆盖 2 种 `Kind`（纯查文件不足以证明体系在运行）。
⑤ ★ **这段提示词的第一句必须是输出契约**，逐字照下面这个句式写：
   「只输出 JSON 数组，不要任何解释文字。数组每个元素包含：Kind（site|record|interview|doc）、Action、Target、Sample（无则省略）、Finding（留空）。」
   之后再用 1~3 句写「去哪、看什么、问谁」。
   ⛔ 同上，不许空泛开头。

## 硬约束

- ⛔ 不许把「企业应提供什么」和「审核员怎么取证」写进同一段 —— 这是两份要求，合并会导致企业照着剧本准备、审核失效。
- ⛔ 不许输出与条款无关的通用套话（如「建立并保持形成文件的程序」这种把条款抄一遍的话）。
- 所有文本用简体中文；字段名保持英文原样。
- 只输出 JSON，第一个字符必须是 `{`，最后一个字符必须是 `}`。',
   NULL, 8192, 0.20,
   'V2 条款属性预生成：输入条款号/标题/正文，输出 8 属性 JSON。V2 相对 V1 修两处：① 无义务动词 ⇒ Obligation=note（V1 全填 shall）② Artifacts/EvidenceChain 第一句必须是输出契约（V1 形态不统一）。',
   2, 1, 'clause-attr-20261028', NOW(), 0, 1)
ON DUPLICATE KEY UPDATE `Id` = `Id`;

-- 验证（必须恰好 1 行 IsActive=1）：
--   SELECT PromptCode, Version, IsActive, LENGTH(Template) FROM wf_prompt_template
--    WHERE PromptType='clause_attr' ORDER BY Version;
