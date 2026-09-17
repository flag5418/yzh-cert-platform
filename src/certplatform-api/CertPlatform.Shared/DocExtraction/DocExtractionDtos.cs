using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace YZH.Entity.Admin.Platform.Doc
{
    /// <summary>
    /// 文档提取规则 DTO 集
    /// <para>对照旧项目 YZH.Entity/Admin/Platform/DocExtraction/DTOs/ExtractionRuleDto.cs 平移（契约保持兼容）</para>
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
        public string Name { get; set; } = "";
        public string? NameEn { get; set; }

        /// <summary>字段编码（英文驼峰，工作流引用键）</summary>
        public string Code { get; set; } = "";
        public string DataType { get; set; } = "string";
        public string? Description { get; set; }
        public bool IsRequired { get; set; }
        public bool IsManual { get; set; }
        public bool IsAiRecommended { get; set; } = true;
        public string? ExtractedValue { get; set; }
    }

    public class TableDefDto
    {
        public string Name { get; set; } = "";
        public string? NameEn { get; set; }
        public string Code { get; set; } = "";
        public string? Description { get; set; }
        public string? SheetName { get; set; }
        public List<TableColumnDto> Columns { get; set; } = new();
        public bool IsAiRecommended { get; set; } = true;

        /// <summary>AI 推荐时提取的数据样例（V2 格式，行数组）</summary>
        public List<Dictionary<string, object>>? ExtractedData { get; set; }
    }

    public class TableColumnDto
    {
        public string Name { get; set; } = "";
        public string? NameEn { get; set; }
        public string Code { get; set; } = "";
        public string DataType { get; set; } = "string";
        public bool IsRequired { get; set; }
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
        public List<FieldDefDto> Fields { get; set; } = new();
        public List<TableDefDto> Tables { get; set; } = new();
        /// <summary>透传转换层/AI 层消息（转换中/转换失败/开关关闭降级/AI分析完成）</summary>
        public string Message { get; set; } = "";
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

        [Required(ErrorMessage = "Prompt 不能为空")]
        public string Prompt { get; set; } = "";
    }

    public class VerifyPromptResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public ExtractionData? Data { get; set; }
    }

    #endregion

    #region 提取数据

    /// <summary>提取结果（字段值 + 表格行数据）</summary>
    public class ExtractionData
    {
        /// <summary>字段值：field_code → value</summary>
        public Dictionary<string, object>? Fields { get; set; }

        /// <summary>表格数据：table_code → 行列表（行 = column_code → value）</summary>
        public Dictionary<string, List<Dictionary<string, object>>>? Tables { get; set; }

        /// <summary>提取过程消息（转换中/转换失败/AI提取失败）</summary>
        public string? Message { get; set; }
    }

    #endregion

    #region 规则详情

    public class RuleDetailResponse
    {
        public long Id { get; set; }
        public string Code { get; set; } = "";
        public string StandardFileCode { get; set; } = "";
        public string OrgCode { get; set; } = "";
        public string StandardCode { get; set; } = "";
        public string PhaseCode { get; set; } = "";
        public string Skill { get; set; } = "";
        public string? Prompt { get; set; }
        public bool IsValid { get; set; }
        public string Status { get; set; } = "none";
        public List<FieldDefDto> Fields { get; set; } = new();
        public List<TableDefDto> Tables { get; set; } = new();
        public DateTime? CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }

    #endregion

    #region AI 配置

    /// <summary>AI 配置 DTO（页面级；连接参数权威源 cert_sys_config）</summary>
    public class AIConfigDto
    {
        public string Provider { get; set; } = "qwen";
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "qwen-turbo";
        public float Temperature { get; set; } = 0.7f;
        public int MaxTokens { get; set; } = 4096;
    }

    /// <summary>技能信息（按扩展名推导 word/excel/pdf）</summary>
    public class SkillInfo
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> SupportedExtensions { get; set; } = new();
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
        public string FieldCode { get; set; } = "";
        public object? Value { get; set; }
        public double Confidence { get; set; } = 1.0;
        public Dictionary<string, object>? SampleData { get; set; }
        public string Message { get; set; } = "";
    }

    /// <summary>表格试运行响应</summary>
    public class TestTableResponse
    {
        public string TableCode { get; set; } = "";
        public List<Dictionary<string, object>> Rows { get; set; } = new();
        public double Confidence { get; set; } = 1.0;
        public string Message { get; set; } = "";
    }

    #endregion
}
