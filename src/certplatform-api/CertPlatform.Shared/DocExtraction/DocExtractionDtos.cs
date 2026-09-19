using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CertPlatform.Shared.DocExtraction
{
    /// <summary>
    /// 文档提取规则 DTO 集
    /// <para>对照旧项目 YZH.Entity/Admin/Platform/DocExtraction/DTOs/ExtractionRuleDto.cs 平移（契约保持兼容）</para>
    /// <para>⚠️ 序列化契约（D-5）：新后端默认 PascalCase，而前端按 camelCase 读取
    /// （data.fields / field.nameEn / field.extractedValue …），因此**响应 DTO 全部显式标注
    /// [JsonPropertyName] camelCase**；未标注时前端拿到 undefined → 页面显示为空
    /// （即“开始分析无结果”类问题的根因）。请求 DTO 不标注（MVC 绑定大小写不敏感）。</para>
    /// </summary>

    #region 保存

    /// <summary>保存提取规则请求（单事务：规则 + 字段定义 + 表格定义）</summary>
    public class SaveExtractionRuleRequest
    {
        /// <summary>规则键：实际文件 FileCode（FL-xxx）或模板 Code（FR-xxx）</summary>
        [Required(ErrorMessage = "文件编码不能为空")]
        public string FileCode { get; set; } = "";

        public string OrgCode { get; set; } = "";
        public string StandardCode { get; set; } = "";
        public string PhaseCode { get; set; } = "";
        public string Skill { get; set; } = "";
        public List<FieldDefDto> Fields { get; set; } = new();
        public List<TableDefDto> Tables { get; set; } = new();
        public string Prompt { get; set; } = "";

        /// <summary>本次保存的验证结论（决定 status=configured/failed）</summary>
        public bool IsValid { get; set; }

        /// <summary>同步落库的提取值（写入 YZH-STD-ENT 企业的 ExtractionResult）</summary>
        public ExtractionData? ExtractionData { get; set; }
    }

    #endregion

    #region 字段/表格定义

    public class FieldDefDto
    {
        /// <summary>字段中文名（文档中的实际标签）</summary>
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("nameEn")] public string? NameEn { get; set; }

        /// <summary>字段编码（英文驼峰，工作流引用键）</summary>
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        [JsonPropertyName("dataType")] public string DataType { get; set; } = "string";
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("isRequired")] public bool IsRequired { get; set; }
        [JsonPropertyName("isManual")] public bool IsManual { get; set; }
        [JsonPropertyName("isAiRecommended")] public bool IsAiRecommended { get; set; } = true;
        [JsonPropertyName("extractedValue")] public string? ExtractedValue { get; set; }
    }

    public class TableDefDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("nameEn")] public string? NameEn { get; set; }
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("sheetName")] public string? SheetName { get; set; }
        [JsonPropertyName("columns")] public List<TableColumnDto> Columns { get; set; } = new();
        [JsonPropertyName("isAiRecommended")] public bool IsAiRecommended { get; set; } = true;

        /// <summary>AI 推荐时提取的数据样例（V2 格式，行数组）</summary>
        [JsonPropertyName("extractedData")] public List<Dictionary<string, object>>? ExtractedData { get; set; }
    }

    public class TableColumnDto
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("nameEn")] public string? NameEn { get; set; }
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        [JsonPropertyName("dataType")] public string DataType { get; set; } = "string";
        [JsonPropertyName("isRequired")] public bool IsRequired { get; set; }
    }

    #endregion

    #region AI 分析

    public class AIAnalyzeRequest
    {
        [Required(ErrorMessage = "文件编码不能为空")]
        public string FileCode { get; set; } = "";

        /// <summary>前端可传但后端不信任：技能类型由文件扩展名权威推导</summary>
        public string? Skill { get; set; }
    }

    public class AIAnalyzeResponse
    {
        [JsonPropertyName("fields")] public List<FieldDefDto> Fields { get; set; } = new();
        [JsonPropertyName("tables")] public List<TableDefDto> Tables { get; set; } = new();
        /// <summary>透传转换层/AI 层消息（转换中/转换失败/开关关闭降级/AI分析完成）</summary>
        [JsonPropertyName("message")] public string Message { get; set; } = "";
    }

    public class GeneratePromptRequest
    {
        [Required(ErrorMessage = "文件编码不能为空")]
        public string FileCode { get; set; } = "";
        public List<FieldDefDto> Fields { get; set; } = new();
        public List<TableDefDto> Tables { get; set; } = new();
    }

    public class VerifyPromptRequest
    {
        [Required(ErrorMessage = "文件编码不能为空")]
        public string FileCode { get; set; } = "";

        /// <summary>
        /// 提取提示词。**留空表示使用「固定提示词」** —— 由该文件已配置的字段/表格清单
        /// 自动生成（推荐：能保证字段与表格结构性分离）。
        /// 传入自定义/模板提示词时，支持 {{document_content}} / {{fields_json}} / {{tables_json}} 占位符。
        /// </summary>
        public string Prompt { get; set; } = "";
    }

    public class VerifyPromptResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = "";
        [JsonPropertyName("data")] public ExtractionData? Data { get; set; }
    }

    #endregion

    #region 提取数据

    /// <summary>提取结果（字段值 + 表格行数据）</summary>
    public class ExtractionData
    {
        // 字段/表格提取结果（前端按 camelCase 读 data.fields / data.tables / data.message）
        /// <summary>字段值：field_code → value</summary>
        [JsonPropertyName("fields")] public Dictionary<string, object>? Fields { get; set; }

        /// <summary>表格数据：table_code → 行列表（行 = column_code → value）</summary>
        [JsonPropertyName("tables")] public Dictionary<string, List<Dictionary<string, object>>>? Tables { get; set; }

        /// <summary>提取过程消息（转换中/转换失败/AI提取失败）</summary>
        [JsonPropertyName("message")] public string? Message { get; set; }
    }

    #endregion

    #region 规则详情

    public class RuleDetailResponse
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        [JsonPropertyName("standardFileCode")] public string StandardFileCode { get; set; } = "";
        [JsonPropertyName("orgCode")] public string OrgCode { get; set; } = "";
        [JsonPropertyName("standardCode")] public string StandardCode { get; set; } = "";
        [JsonPropertyName("phaseCode")] public string PhaseCode { get; set; } = "";
        [JsonPropertyName("skill")] public string Skill { get; set; } = "";
        [JsonPropertyName("prompt")] public string? Prompt { get; set; }
        [JsonPropertyName("isValid")] public bool IsValid { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "none";
        [JsonPropertyName("fields")] public List<FieldDefDto> Fields { get; set; } = new();
        [JsonPropertyName("tables")] public List<TableDefDto> Tables { get; set; } = new();
        [JsonPropertyName("createTime")] public DateTime? CreateTime { get; set; }
        [JsonPropertyName("updateTime")] public DateTime? UpdateTime { get; set; }
    }

    #endregion

    #region AI 配置

    /// <summary>AI 配置 DTO（页面级；连接参数权威源 cert_sys_config）</summary>
    public class AIConfigDto
    {
        [JsonPropertyName("provider")] public string Provider { get; set; } = "qwen";
        [JsonPropertyName("apiKey")] public string ApiKey { get; set; } = "";
        [JsonPropertyName("model")] public string Model { get; set; } = "qwen-turbo";
        [JsonPropertyName("temperature")] public float Temperature { get; set; } = 0.7f;
        [JsonPropertyName("maxTokens")] public int MaxTokens { get; set; } = 4096;
    }

    /// <summary>技能信息（按扩展名推导 word/excel/pdf）</summary>
    public class SkillInfo
    {
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("supportedExtensions")] public List<string> SupportedExtensions { get; set; } = new();
    }

    #endregion

    #region 配置期试运行

    /// <summary>测试字段提取请求（工作流 docField 节点配置期验证）</summary>
    public class TestFieldRequest
    {
        [Required(ErrorMessage = "规则编码不能为空")]
        public string RuleCode { get; set; } = "";

        [Required(ErrorMessage = "字段编码不能为空")]
        public string FieldCode { get; set; } = "";

        /// <summary>文档类型：standard=标准文档，enterprise=企业文档</summary>
        public string DocType { get; set; } = "standard";
    }

    /// <summary>测试表格提取请求（工作流 docTable 节点配置期验证）</summary>
    public class TestTableRequest
    {
        [Required(ErrorMessage = "规则编码不能为空")]
        public string RuleCode { get; set; } = "";

        [Required(ErrorMessage = "表格编码不能为空")]
        public string TableCode { get; set; } = "";

        /// <summary>文档类型：standard=标准文档，enterprise=企业文档</summary>
        public string DocType { get; set; } = "standard";
    }

    /// <summary>字段试运行响应</summary>
    public class TestFieldResponse
    {
        [JsonPropertyName("fieldCode")] public string FieldCode { get; set; } = "";
        [JsonPropertyName("value")] public object? Value { get; set; }
        [JsonPropertyName("confidence")] public double Confidence { get; set; } = 1.0;
        [JsonPropertyName("sampleData")] public Dictionary<string, object>? SampleData { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; } = "";
    }

    /// <summary>表格试运行响应</summary>
    public class TestTableResponse
    {
        [JsonPropertyName("tableCode")] public string TableCode { get; set; } = "";
        [JsonPropertyName("rows")] public List<Dictionary<string, object>> Rows { get; set; } = new();
        [JsonPropertyName("confidence")] public double Confidence { get; set; } = 1.0;
        [JsonPropertyName("message")] public string Message { get; set; } = "";
    }

    #endregion
}
