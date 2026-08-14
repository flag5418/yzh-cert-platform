-- ========================================================-- 文档提取规则分析提示词模板
-- 用途：AI 自动分析 Word/Excel 文档结构，推荐提取字段和表格
-- 执行时机：Step 2 - 在 NPOI 提取验证通过后
-- ========================================================

-- 先清理旧版本（保持幂等性）
DELETE FROM wf_prompt_template WHERE prompt_code LIKE 'analyze_%_v1';

-- ========================================================-- 1. Word 文档分析提示词
-- 适用：.docx 格式的体系认证文档（质量手册、程序文件、记录等）
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
    'analyze_word_v1',
    'Word文档结构分析_v1',
    'document_analysis',
    'word',
    '你是专业的体系认证文档分析专家。请深度分析以下 Word 文档的内容结构，识别所有可提取的关键信息字段和表格。

## 分析任务

1. **识别字段**：从文档中提取以下类型的字段
   - 文档标识：文件编号、版本号、生效日期、编制人、审核人、批准人
   - 组织信息：企业名称、部门名称、岗位名称
   - 时间信息：日期、时间周期、有效期
   - 状态信息：文件状态、审批状态、执行状态
   - 数值信息：数量、比例、百分比、金额
   - 描述信息：标题、摘要、备注、说明

2. **识别表格**：文档中可能包含的表格类型
   - 记录表格：签到表、检查表、评审记录表
   - 清单表格：文件清单、设备清单、人员清单
   - 统计表格：数据统计、趋势分析、汇总报表
   - 流程表格：流程步骤、职责分工、时间节点

## 输出格式（严格 JSON）

```json
{
  "fields": [
    {
      "field_code": "file_code",
      "field_name": "文件编号",
      "field_type": "string",
      "is_required": true,
      "description": "文档的唯一标识编号，如 XASL-QM-001"
    }
  ],
  "tables": [
    {
      "table_code": "approval_records",
      "table_name": "审批记录表",
      "description": "记录文件编制、审核、批准的流程信息",
      "columns": [
        {"column_code": "role", "column_name": "角色", "column_type": "string"},
        {"column_code": "name", "column_name": "姓名", "column_type": "string"},
        {"column_code": "date", "column_name": "日期", "column_type": "date"}
      ]
    }
  ]
}
```

## 字段类型规范
- string: 文本、名称、描述
- number: 整数、小数、计数
- date: 日期、时间
- boolean: 是/否、有/无
- money: 金额、价格
- percent: 百分比、比例

## 分析原则
1. 只提取文档中实际存在的信息，不要臆测
2. 优先识别体系认证相关字段（文件控制、质量记录、审核证据）
3. 表格列定义要完整，包含表头和数据样例反映的列
4. 字段命名使用英文驼峰（field_code），展示用中文（field_name）
5. 必须字段标记 is_required: true（如文件编号、版本等关键标识）

## 文档内容

{{document_content}}

请只输出 JSON，不要任何解释文字。',
    '用于分析 Word 文档（质量手册、程序文件、记录表单等）的结构，自动识别可提取的字段和表格',
    1,
    1,
    1,
    NOW(),
    'system'
);

-- ========================================================-- 2. Excel 表格分析提示词
-- 适用：.xlsx/.xls 格式的记录表格、台账、清单
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
    'analyze_excel_v1',
    'Excel表格结构分析_v1',
    'document_analysis',
    'excel',
    '你是专业的数据表格分析专家。请深度分析以下 Excel 表格的结构，识别表头字段和数据特征。

## 分析任务

1. **识别工作表**：分析每个工作表的业务含义
   - 主数据表：核心记录数据
   - 配置表：下拉选项、参数配置
   - 汇总表：统计汇总、数据分析
   - 模板表：填写模板、示例数据

2. **识别字段**：从表头中提取字段定义
   - 基础字段：序号、日期、编号、名称
   - 业务字段：根据表格业务类型识别
     * 人员类：姓名、部门、岗位、工号
     * 设备类：设备编号、设备名称、规格型号
     * 产品类：产品编号、产品名称、批次号
     * 质量类：检验项、检验结果、不合格描述
   - 状态字段：状态、结论、签字、备注

3. **识别表格关系**：多个工作表之间的关联

## 输出格式（严格 JSON）

```json
{
  "fields": [
    {
      "field_code": "check_date",
      "field_name": "检查日期",
      "field_type": "date",
      "is_required": true,
      "description": "记录检查或填表的日期"
    }
  ],
  "tables": [
    {
      "table_code": "equipment_list",
      "table_name": "设备台账",
      "description": "记录企业所有生产设备的基本信息",
      "sheet_name": "设备清单",
      "columns": [
        {"column_code": "seq_no", "column_name": "序号", "column_type": "number"},
        {"column_code": "equipment_code", "column_name": "设备编号", "column_type": "string"},
        {"column_code": "equipment_name", "column_name": "设备名称", "column_type": "string"},
        {"column_code": "model", "column_name": "规格型号", "column_type": "string"},
        {"column_code": "location", "column_name": "存放位置", "column_type": "string"},
        {"column_code": "status", "column_name": "状态", "column_type": "string"}
      ]
    }
  ]
}
```

## 字段类型规范
- string: 文本、编码、名称、描述
- number: 整数、小数、计数、金额
- date: 日期、时间
- boolean: 是/否、合格/不合格
- money: 金额、价格（带货币单位）
- percent: 百分比、比例

## 分析原则
1. 第一行通常是表头，用于定义字段
2. 第二行开始是数据，用于推断字段类型
3. 识别数据格式：日期格式、数字格式、文本长度
4. 注意合并单元格（通常是分类标题）
5. 多个工作表时，分别分析每个表的结构
6. 字段命名使用英文驼峰，展示用中文
7. 必须字段：根据业务常识判断（如日期、编号等）

## 表格内容

{{document_content}}

请只输出 JSON，不要任何解释文字。',
    '用于分析 Excel 表格（记录台账、检验记录、设备清单等）的结构，自动识别表头字段和数据特征',
    1,
    1,
    1,
    NOW(),
    'system'
);

-- ========================================================-- 3. PDF 文档分析提示词（预留）
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
    'analyze_pdf_v1',
    'PDF文档结构分析_v1',
    'document_analysis',
    'pdf',
    '你是专业的文档分析专家。请分析以下 PDF 文档的内容结构，识别可提取的关键信息字段。

## 分析任务

1. **识别字段**：
   - 文档元数据：标题、作者、创建日期、页数
   - 内容字段：根据文档类型识别关键信息
   - 标识信息：编号、版本、日期、签章

2. **识别表格**：
   - 文本表格：通过布局识别的表格结构
   - 表单字段：可填写的表单区域

## 输出格式（严格 JSON）

```json
{
  "fields": [
    {
      "field_code": "document_title",
      "field_name": "文档标题",
      "field_type": "string",
      "is_required": true,
      "description": "PDF 文档的主标题"
    }
  ],
  "tables": []
}
```

## 文档内容

{{document_content}}

请只输出 JSON，不要任何解释文字。',
    '用于分析 PDF 文档的结构，自动识别可提取的字段',
    1,
    1,
    1,
    NOW(),
    'system'
);

-- 验证插入结果
SELECT prompt_code, prompt_name, prompt_type, skill_target, version, is_active, enable 
FROM wf_prompt_template 
WHERE prompt_code LIKE 'analyze_%_v1'
ORDER BY prompt_code;
