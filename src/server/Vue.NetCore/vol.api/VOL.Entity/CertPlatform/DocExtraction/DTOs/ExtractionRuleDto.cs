using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VOL.Entity.CertPlatform.DocExtraction.DTOs
{
    /// <summary>
    /// 保存提取规则请求
    /// 规则绑定到标准文件（cert_file_requirement.code），而非企业文件
    /// </summary>
    public class SaveExtractionRuleRequest
    {
        /// <summary>
        /// 标准文件编码（关联 cert_file_requirement.code）
        /// </summary>
        [Required(ErrorMessage = "标准文件编码不能为空")]
        public string StandardFileCode { get; set; }

        /// <summary>
        /// 机构编码（冗余，方便过滤）
        /// </summary>
        public string OrgCode { get; set; }

        /// <summary>
        /// 标准编码（冗余，关联 cert_iso_standard.code）
        /// </summary>
        public string StandardCode { get; set; }

        /// <summary>
        /// 阶段编码（冗余，方便过滤）
        /// </summary>
        public string PhaseCode { get; set; }

        /// <summary>
        /// 技能类型
        /// </summary>
        [Required(ErrorMessage = "技能类型不能为空")]
        public string Skill { get; set; }

        /// <summary>
        /// 字段定义列表
        /// </summary>
        public List<FieldDefDto> Fields { get; set; } = new List<FieldDefDto>();

        /// <summary>
        /// 表格定义列表
        /// </summary>
        public List<TableDefDto> Tables { get; set; } = new List<TableDefDto>();

        /// <summary>
        /// Prompt内容
        /// </summary>
        public string Prompt { get; set; }

        /// <summary>
        /// 是否验证通过
        /// </summary>
        public bool IsValid { get; set; }
    }

    /// <summary>
    /// 字段定义DTO（支持 V2 格式：中英文双名 + 提取值预览）
    /// </summary>
    public class FieldDefDto
    {
        /// <summary>中文字段名（展示用）</summary>
        public string Name { get; set; }

        /// <summary>英文字段名（snake_case，数据库存储用）</summary>
        public string NameEn { get; set; }

        /// <summary>字段编码（兼容旧版，优先使用 NameEn）</summary>
        public string Code { get; set; }

        /// <summary>数据类型</summary>
        public string DataType { get; set; } = "string";

        /// <summary>字段描述</summary>
        public string Description { get; set; }

        /// <summary>是否必须</summary>
        public bool IsRequired { get; set; }

        /// <summary>是否手动维护</summary>
        public bool IsManual { get; set; }

        /// <summary>是否 AI 推荐字段（true=可自动提取，false=手动添加/审核员必填字段，生成Prompt时过滤掉）</summary>
        public bool IsAiRecommended { get; set; } = true;

        /// <summary>AI 提取的值（前端预览用）</summary>
        public string ExtractedValue { get; set; }
    }

    /// <summary>
    /// 表格定义DTO（支持 V2 格式：中英文双名 + 提取数据预览）
    /// </summary>
    public class TableDefDto
    {
        /// <summary>中文表名（展示用）</summary>
        public string Name { get; set; }

        /// <summary>英文表名（snake_case，数据库存储用）</summary>
        public string NameEn { get; set; }

        /// <summary>表格编码（兼容旧版，优先使用 NameEn）</summary>
        public string Code { get; set; }

        /// <summary>表格描述</summary>
        public string Description { get; set; }

        /// <summary>工作表名称（Excel 特有）</summary>
        public string SheetName { get; set; }

        /// <summary>列定义</summary>
        public List<TableColumnDto> Columns { get; set; } = new List<TableColumnDto>();

        /// <summary>AI 提取的数据样例（前端预览用，最多5行）</summary>
        public List<Dictionary<string, object>> ExtractedData { get; set; } = new List<Dictionary<string, object>>();

        /// <summary>是否 AI 推荐表格（true=可自动提取，false=手动添加，生成Prompt时过滤掉）</summary>
        public bool IsAiRecommended { get; set; } = true;
    }

    /// <summary>
    /// 表格列定义DTO（支持 V2 格式：中英文双名）
    /// </summary>
    public class TableColumnDto
    {
        /// <summary>中文列名（展示用）</summary>
        public string Name { get; set; }

        /// <summary>英文列名（snake_case，数据库存储用）</summary>
        public string NameEn { get; set; }

        /// <summary>列编码（兼容旧版，优先使用 NameEn）</summary>
        public string Code { get; set; }

        /// <summary>数据类型</summary>
        public string DataType { get; set; } = "string";

        /// <summary>是否必须（NC/报告生成等下游业务要求必填字段不能为空）</summary>
        public bool IsRequired { get; set; }
    }

    /// <summary>
    /// AI分析请求
    /// </summary>
    public class AIAnalyzeRequest
    {
        [Required(ErrorMessage = "标准文件编码不能为空")]
        public string StandardFileCode { get; set; }

        [Required(ErrorMessage = "技能类型不能为空")]
        public string Skill { get; set; }
    }

    /// <summary>
    /// AI分析响应
    /// </summary>
    public class AIAnalyzeResponse
    {
        /// <summary>
        /// 提取的字段
        /// </summary>
        public List<FieldDefDto> Fields { get; set; }

        /// <summary>
        /// 提取的表格
        /// </summary>
        public List<TableDefDto> Tables { get; set; }

        /// <summary>
        /// 分析消息
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// 生成Prompt请求
    /// </summary>
    public class GeneratePromptRequest
    {
        [Required(ErrorMessage = "标准文件编码不能为空")]
        public string StandardFileCode { get; set; }

        public List<FieldDefDto> Fields { get; set; } = new List<FieldDefDto>();
        public List<TableDefDto> Tables { get; set; } = new List<TableDefDto>();
    }

    /// <summary>
    /// 验证Prompt请求
    /// </summary>
    public class VerifyPromptRequest
    {
        [Required(ErrorMessage = "标准文件编码不能为空")]
        public string StandardFileCode { get; set; }

        [Required(ErrorMessage = "Prompt不能为空")]
        public string Prompt { get; set; }
    }

    /// <summary>
    /// 验证Prompt响应
    /// </summary>
    public class VerifyPromptResponse
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// 提取的数据
        /// </summary>
        public ExtractionData Data { get; set; }
    }

    /// <summary>
    /// 提取的数据
    /// </summary>
    public class ExtractionData
    {
        /// <summary>
        /// 字段值
        /// </summary>
        public Dictionary<string, object> Fields { get; set; }

        /// <summary>
        /// 表格数据
        /// </summary>
        public Dictionary<string, List<Dictionary<string, object>>> Tables { get; set; }

        /// <summary>
        /// 提取过程消息（如“转换中/转换失败/不支持的文件类型”）
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// 规则详情响应
    /// </summary>
    public class RuleDetailResponse
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string StandardFileCode { get; set; }
        public string OrgCode { get; set; }
        public string StandardCode { get; set; }
        public string PhaseCode { get; set; }
        public string Skill { get; set; }
        public string Prompt { get; set; }
        public bool IsValid { get; set; }
        public string Status { get; set; }
        public List<FieldDefDto> Fields { get; set; }
        public List<TableDefDto> Tables { get; set; }
        public DateTime? CreateDate { get; set; }
        public DateTime? ModifyDate { get; set; }
    }

    /// <summary>
    /// AI配置DTO
    /// </summary>
    public class AIConfigDto
    {
        public string Provider { get; set; }
        public string ApiKey { get; set; }
        public string Model { get; set; }
        public float Temperature { get; set; }
        public int MaxTokens { get; set; }
    }

    /// <summary>
    /// 技能信息
    /// </summary>
    public class SkillInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> SupportedExtensions { get; set; }
    }
}
