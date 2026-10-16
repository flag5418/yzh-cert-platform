-- ========================================================
-- 落库 analyze_word 分析提示词（v3）—— 修复「UPDATE 空转」缺陷
-- 背景：
--   DocExtractionRuleService.AI.cs BuildAnalysisPromptAsync 按
--   PromptCode = 'analyze_{skill}' 优先读 wf_prompt_template；
--   本库此前无 analyze_* 行，而 scripts/db/update_analyze_word_prompt_V1.sql
--   只有 UPDATE（匹配 0 行静默空转）⇒ 分析一直走内嵌默认提示词（V1 契约，
--   无 extracted_value）⇒ 前端「提取值」列永远为空。
-- 本脚本：幂等 INSERT（PromptCode 唯一键 ON DUPLICATE KEY UPDATE）。
-- 执行：
--   docker exec -i yzh-mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" \
--     --default-character-set=utf8mb4 yzh_cert_platform \
--     < scripts/db/20261015_analyze_word_prompt_seed_V1.sql
-- ========================================================

SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;

INSERT INTO wf_prompt_template
    (Code, PromptCode, PromptName, PromptType, SkillTarget, StandardCode,
     Template, Description, Version, IsActive, IsValid, IsDeleted,
     Status, Sort, CreateTime, UpdateTime, CreateBy, Remark)
VALUES
    (REPLACE(UUID(), '-', ''),
     'analyze_word',
     'Word 文档分析（字段/表格推荐）',
     'analyze',
     'word',
     NULL,
'你是专业的体系认证文档分析专家。请深度分析以下 Word 文档的内容结构，识别所有可提取的关键信息字段和表格。

## 分析任务

1. **识别字段**：识别文档**普通段落**中**实际存在**的信息字段。以下字段类型仅供参考，**只输出文档中真实出现、且能提取到实际内容的字段**：
   - **优先穷举「标签」冒号「值」信息对**：扫描普通段落中所有以中文冒号「：」或英文冒号「:」分隔的键值对（如 文件编号：XASL-QM、Version: A），冒号前的标签作为 field_name_cn，冒号后的文本填入 extracted_value；值为空的键值对跳过；同一标签在文档中出现多次时只保留一个字段，extracted_value 取首次出现的值
   - 文档标识：文件编号、版本号、生效日期、编制人、审核人、批准人
   - 组织信息：企业名称、部门名称、岗位名称
   - 时间信息：日期、时间周期、有效期
   - 状态信息：文件状态、审批状态、执行状态
   - 数值信息：数量、比例、百分比、金额
   - 描述信息：标题、摘要、备注、说明
   - 文档中没有对应内容的字段类型，一律不要输出，不要为了凑齐列表而输出空字段
   - **禁止把表格单元格内容（如“质量方针”“质量目标”）当作字段输出**，详见“表格内容处理规则”（正文为空时的例外见该节第 5 条）
   - **字段名称必须与文档中的实际标签一致**：文档中写“总经理”就输出字段“总经理”，禁止把“总经理”改名为“编制人”、把“管理者代表”改名为“审核人/批准人”等角色替换；文档中不存在的字段（如编制人、审核人、批准人）一律不得输出

2. **识别表格**：识别文档中**实际存在**的表格。以下类型仅供参考，**只输出文档中真实出现、且能提取到真实数据的表格**：
   - 记录表格：签到表、检查表、评审记录表
   - 清单表格：文件清单、设备清单、人员清单
   - 统计表格：数据统计、趋势分析、汇总报表
   - 流程表格：流程步骤、职责分工、时间节点
   - **表格必须来自文档真实存在的表格结构**（下方「【表格 n】」区块或 Markdown 表格行列）；**禁止根据文件名、目录名、标题或常识臆造表格**（如仅因文件名含“清单”就输出清单表）
   - 文档中没有表格时，tables 输出空数组 []

## 表格内容处理规则（必须严格遵守）

1. 文档内容中「【表格 n】」区块属于**表格内容**，每行即表格的一行数据
2. 表格中的任何单元格内容（如“质量方针”“质量目标”等）**一律禁止输出到 fields**，只能作为表格数据存在
3. 表格内容只能通过 tables 提取：每个表格只需输出**表格名称 + 列定义（columns）** + 少量 extracted_data 样例，禁止把表格内容拆成独立字段
4. fields 只允许来自普通段落中的真实字段；无法判断来源的信息宁可少输出，也不要臆造
5. **例外（仅当「【正文】」为空时生效）**：表单/工艺卡类文档的内容全部落在一个合并单元格大表里，此时允许从表格中识别「标签：值」形式的字段（标签作 field_name_cn，冒号后的实际内容作 extracted_value，不得臆造值）；纯数据型行列表格（如步骤表、清单表）仍必须整体输出到 tables，禁止拆成字段

## 输出格式（严格 JSON）

```json
{
  "fields": [
    {
      "field_name_cn": "文件编号",
      "field_name_en": "wen_jian_bian_hao",
      "field_type": "string",
      "is_required": true,
      "description": "文档的唯一标识编号，如 XASL-QM-001",
      "extracted_value": "从文档中提取的实际值，如 XASL-QP-024"
    }
  ],
  "tables": [
    {
      "table_name_cn": "审批记录表",
      "table_name_en": "shen_pi_ji_lu_biao",
      "description": "记录文件编制、审核、批准的流程信息",
      "columns": [
        {"column_name_cn": "角色", "column_name_en": "jiao_se", "column_type": "string", "column_is_required": true},
        {"column_name_cn": "姓名", "column_name_en": "xing_ming", "column_type": "string", "column_is_required": true},
        {"column_name_cn": "日期", "column_name_en": "ri_qi", "column_type": "date", "column_is_required": false}
      ],
      "extracted_data": [
        {"角色": "编制", "姓名": "张三", "日期": "2024-01-15"},
        {"角色": "审核", "姓名": "李四", "日期": "2024-01-16"}
      ]
    }
  ]
}
```

## 字段命名规则

### 中文字段名（field_name_cn）
- 使用简洁的中文名称；**标签内的多余空白须去除**（如“设  备”输出为“设备”），**冒号不得进入字段名**（如“页码：第1页共1页”应拆为 field_name_cn=“页码”、extracted_value=“第1页共1页”）
- 示例："文件编号"、"企业名称"、"生效日期"

### 英文字段名（field_name_en）
- 将中文翻译成英文后转换为 snake_case（小写+下划线）
- 转换规则：
  * "文件编号" → "wen_jian_bian_hao"
  * "企业名称" → "qi_ye_ming_cheng"
  * "生效日期" → "sheng_xiao_ri_qi"
  * "审批记录表" → "shen_pi_ji_lu_biao"

## 字段类型规范
- string: 文本、名称、描述
- number: 整数、小数、计数
- date: 日期、时间（格式：YYYY-MM-DD）
- boolean: 是/否、有/无（true/false）
- money: 金额、价格
- percent: 百分比、比例

## 分析原则
1. 只提取文档中实际存在的信息，不要臆测，禁止输出文档中不存在的字段或表格
2. fields 中每个字段必须能在文档中找到实际内容（extracted_value 非空），且**必须来自普通段落（正文为空时按表格内容处理规则第 5 条），禁止来自表格单元格**；**字段名称必须与文档中的实际标签一致，禁止角色替换（如把“总经理”改写为“编制人”、把“管理者代表”改写为“审核人/批准人”）**；文档中不存在的字段一律不要输出
3. tables 中每个表格必须能在文档中找到真实数据（extracted_data 至少一行，行内各列取文档中的真实值）；找不到真实数据的表格不要输出；文档不存在真实表格结构时 tables 必须输出 []
4. 优先识别体系认证相关字段（文件控制、质量记录、审核证据）
5. 表格列定义要完整，包含表头和数据样例反映的列
6. 确实作为关键标识且文档中真实存在的字段标记 is_required: true（如文件编号）
7. 表格列同样标记 column_is_required（关键列如编号/名称必填，非关键列可空）
8. 同一文档内字段的 column_name_en/field_name_en 必须唯一，不得重复
9. extracted_value 和 extracted_data 用于前端预览，展示 AI 实际提取的内容

## 输出体积约束（必须严格遵守）
1. **fields 最多输出 20 个，tables 最多输出 5 张**，只保留最重要的字段/表格
2. **每个表格的 extracted_data 最多输出 3 行样例数据**，用于前端预览即可，不要输出全部数据行
3. 整个 JSON 输出必须控制在 4000 tokens 以内，**优先保证 JSON 完整合法**；宁可精简字段数量，也绝不能输出被截断的 JSON
4. 不要重复输出文档中的大段原文， description 保持一句话描述

## 文档内容

{{document_content}}

请只输出 JSON，不要任何解释文字。',
     'Word文档分析v3（2026-10-15落库）：穷举中英文冒号键值对字段、带 extracted_value 预览、正文为空时允许从表单大表格取「标签：值」字段',
     3, 1, 1, 0,
     'active', 0, NOW(), NOW(), 'fix_20261015', '落库 analyze_word，消除内嵌默认提示词回退')
ON DUPLICATE KEY UPDATE
    Template     = VALUES(Template),
    Description  = VALUES(Description),
    Version      = VALUES(Version),
    IsActive     = 1,
    IsValid      = 1,
    IsDeleted    = 0,
    Status       = 'active',
    UpdateTime   = NOW(),
    UpdateBy     = 'fix_20261015';

-- ---------- 验证（应返回 1 行，version=3，IsActive=1，template_len > 5000） ----------
SELECT Id, PromptCode, PromptName, Version, IsActive, CHAR_LENGTH(Template) AS template_len
FROM wf_prompt_template
WHERE PromptCode = 'analyze_word';
