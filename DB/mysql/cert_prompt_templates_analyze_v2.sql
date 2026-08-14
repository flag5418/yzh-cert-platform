-- ========================================================
-- 文档提取规则分析提示词模板 V2
-- 更新：字段同时输出中文名和 snake_case 英文名
-- 用途：AI 自动分析 Word/Excel 文档结构，推荐提取字段和表格
-- ========================================================

-- 先清理旧版本（保持幂等性）
DELETE FROM wf_prompt_template WHERE prompt_code LIKE 'analyze_%_v2';

-- ========================================================
-- 1. Word 文档分析提示词 V2
-- ========================================================
INSERT INTO wf_prompt_template (
    code,
    prompt_code,
    prompt_name,
    prompt_type,
    skill_target,
    template,
    description,
    version,
    is_active,
    enable,
    create_date,
    creator
) VALUES (
    UUID(),
    'analyze_word_v2',
    'Word文档结构分析_v2',
    'document_analysis',
    'word',
    '你是专业的体系认证文档分析专家。请深度分析以下 Word 文档的内容结构，识别所有可提取的关键信息字段和表格。

## 分析任务

1. **识别字段**：识别文档**普通段落**中**实际存在**的信息字段。以下字段类型仅供参考，**只输出文档中真实出现、且能提取到实际内容的字段**：
   - 文档标识：文件编号、版本号、生效日期、编制人、审核人、批准人
   - 组织信息：企业名称、部门名称、岗位名称
   - 时间信息：日期、时间周期、有效期
   - 状态信息：文件状态、审批状态、执行状态
   - 数值信息：数量、比例、百分比、金额
   - 描述信息：标题、摘要、备注、说明
   - 文档中没有对应内容的字段类型，一律不要输出，不要为了凑齐列表而输出空字段
   - **禁止把表格单元格内容（如“质量方针”“质量目标”）当作字段输出**，详见“表格内容处理规则”
   - **字段名称必须与文档中的实际标签一致**：文档中写“总经理”就输出字段“总经理”，禁止把“总经理”改名为“编制人”、把“管理者代表”改名为“审核人/批准人”等角色替换；文档中不存在的字段（如编制人、审核人、批准人）一律不得输出

2. **识别表格**：识别文档中**实际存在**的表格。以下类型仅供参考，**只输出文档中真实出现、且能提取到真实数据的表格**：
   - 记录表格：签到表、检查表、评审记录表
   - 清单表格：文件清单、设备清单、人员清单
   - 统计表格：数据统计、趋势分析、汇总报表
   - 流程表格：流程步骤、职责分工、时间节点
   - 文档中没有表格时，tables 输出空数组 []

## 表格内容处理规则（必须严格遵守）

1. 文档内容中标记为 `(table)` 的 Section 属于**表格内容**，单元格以制表符分隔，每行即表格的一行数据
2. 表格中的任何单元格内容（如“质量方针”“质量目标”等）**一律禁止输出到 fields**，只能作为表格数据存在
3. 表格内容只能通过 tables 提取：每个表格只需输出**表格名称 + 列定义（columns）**，列即该表格的字段，禁止把表格内容拆成独立字段
4. fields 只允许来自普通段落中的真实字段；无法判断来源的信息宁可少输出，也不要臆造

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
- 使用简洁的中文名称
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
2. fields 中每个字段必须能在文档中找到实际内容（extracted_value 非空），且**必须来自普通段落，禁止来自表格单元格**；**字段名称必须与文档中的实际标签一致，禁止角色替换（如把“总经理”改写为“编制人”、把“管理者代表”改写为“审核人/批准人”）**；文档中不存在的字段一律不要输出
3. tables 中每个表格必须能在文档中找到真实数据（extracted_data 至少一行，行内各列取文档中的真实值）；找不到真实数据的表格不要输出
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
4. 不要重复输出文档中的大段原文，description 保持一句话描述

## 文档内容

{{document_content}}

请只输出 JSON，不要任何解释文字。',
    'Word文档分析：输出中英文双名字段，snake_case英文格式，包含提取值预览',
    2,
    1,
    1,
    NOW(),
    'system'
);

-- ========================================================
-- 2. Excel 表格分析提示词 V2
-- ========================================================
INSERT INTO wf_prompt_template (
    code,
    prompt_code,
    prompt_name,
    prompt_type,
    skill_target,
    template,
    description,
    version,
    is_active,
    enable,
    create_date,
    creator
) VALUES (
    UUID(),
    'analyze_excel_v2',
    'Excel表格结构分析_v2',
    'document_analysis',
    'excel',
    '你是专业的数据表格分析专家。请深度分析以下 Excel 表格的结构，识别表头字段和数据特征。

## 分析任务

1. **识别工作表**：分析每个工作表的业务含义
   - 主数据表：核心记录数据
   - 配置表：下拉选项、参数配置
   - 汇总表：统计汇总、数据分析
   - 模板表：填写模板、示例数据

2. **识别字段**：Excel 内容全部来自表格，**默认 fields 输出空数组 []**；仅当存在表格之外的独立说明信息（如标题、单位、填表说明）时才作为字段输出：
   - 基础字段：序号、日期、编号、名称
   - 业务字段：根据表格业务类型识别
     * 人员类：姓名、部门、岗位、工号
     * 设备类：设备编号、设备名称、规格型号
     * 产品类：产品编号、产品名称、批次号
     * 质量类：检验项、检验结果、不合格描述
   - 状态字段：状态、结论、签字、备注
   - **表头列与单元格内容一律不作为独立字段输出**，详见“表格内容处理规则”

## 表格内容处理规则（必须严格遵守）

1. Excel 的每个工作表/数据区都是表格，一律通过 tables 提取
2. 每个表格只需输出**表格名称 + 列定义（columns）**，列即该表格的字段；禁止把表头列或单元格内容输出到 fields
3. extracted_data 仅用于前端预览样例，禁止把表格数据行内容拆成独立字段

## 输出格式（严格 JSON）

```json
{
  "fields": [
    {
      "field_name_cn": "检查日期",
      "field_name_en": "jian_cha_ri_qi",
      "field_type": "date",
      "is_required": true,
      "description": "记录检查或填表的日期",
      "extracted_value": "2024-03-15"
    }
  ],
  "tables": [
    {
      "table_name_cn": "设备台账",
      "table_name_en": "she_bei_tai_zhang",
      "description": "记录企业所有生产设备的基本信息",
      "sheet_name": "设备清单",
      "columns": [
        {"column_name_cn": "序号", "column_name_en": "xu_hao", "column_type": "number", "column_is_required": true},
        {"column_name_cn": "设备编号", "column_name_en": "she_bei_bian_hao", "column_type": "string", "column_is_required": true},
        {"column_name_cn": "设备名称", "column_name_en": "she_bei_ming_cheng", "column_type": "string", "column_is_required": true},
        {"column_name_cn": "规格型号", "column_name_en": "gui_ge_xing_hao", "column_type": "string", "column_is_required": false},
        {"column_name_cn": "存放位置", "column_name_en": "cun_fang_wei_zhi", "column_type": "string", "column_is_required": false},
        {"column_name_cn": "状态", "column_name_en": "zhuang_tai", "column_type": "string", "column_is_required": false}
      ],
      "extracted_data": [
        {"序号": "1", "设备编号": "SB-001", "设备名称": "注塑机", "规格型号": "XZ-100", "存放位置": "A车间", "状态": "正常"},
        {"序号": "2", "设备编号": "SB-002", "设备名称": "冲压机", "规格型号": "CY-200", "存放位置": "B车间", "状态": "维修中"}
      ]
    }
  ]
}
```

## 字段命名规则

### 中文字段名（field_name_cn）
- 使用表头中的中文名称
- 示例："序号"、"设备编号"、"检验结果"

### 英文字段名（field_name_en）
- 将中文翻译成英文后转换为 snake_case（小写+下划线）
- 转换规则：
  * "设备编号" → "she_bei_bian_hao"
  * "设备名称" → "she_bei_ming_cheng"
  * "规格型号" → "gui_ge_xing_hao"
  * "存放位置" → "cun_fang_wei_zhi"

## 字段类型规范
- string: 文本、编码、名称、描述
- number: 整数、小数、计数、金额
- date: 日期、时间（格式：YYYY-MM-DD）
- boolean: 是/否、合格/不合格（true/false）
- money: 金额、价格（带货币单位）
- percent: 百分比、比例

## 分析原则
1. 第一行通常是表头，用于定义字段
2. 只提取表头中真实存在的列，禁止输出表头中不存在的字段
3. 第二行开始是数据，用于推断字段类型
4. 识别数据格式：日期格式、数字格式、文本长度
5. 注意合并单元格（通常是分类标题）
6. 多个工作表时，分别分析每个表的结构
7. fields 默认输出空数组 []，且字段必须来自表格之外的独立信息，禁止把表头列或单元格内容当作字段
8. 表格列同样标记 column_is_required（关键列如编号/名称必填，非关键列可空）
9. 同一表格内列的 column_name_en 必须唯一，不得重复
10. extracted_data 用于前端预览，展示 AI 实际提取的数据样例（最多3行），每行取文档中的真实数据

## 输出体积约束（必须严格遵守）
1. **fields 最多输出 20 个，tables 最多输出 5 张**，只保留最重要的字段/表格
2. **每个表格的 extracted_data 最多输出 3 行样例数据**，用于前端预览即可，不要输出全部数据行
3. 整个 JSON 输出必须控制在 4000 tokens 以内，**优先保证 JSON 完整合法**；宁可精简字段数量，也绝不能输出被截断的 JSON
4. 不要重复输出表格中的全部数据行，description 保持一句话描述

## 表格内容

{{document_content}}

请只输出 JSON，不要任何解释文字。',
    'Excel表格分析：输出中英文双名字段，snake_case英文格式，包含提取数据预览',
    2,
    1,
    1,
    NOW(),
    'system'
);

-- 验证插入结果
SELECT prompt_code, prompt_name, prompt_type, skill_target, version, is_active, enable 
FROM wf_prompt_template 
WHERE prompt_code LIKE 'analyze_%_v2'
ORDER BY prompt_code;
