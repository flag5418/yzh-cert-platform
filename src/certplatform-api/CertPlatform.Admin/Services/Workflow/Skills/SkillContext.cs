using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// Skill 执行上下文与结果
    /// <para>移植自：旧 YZH.Core.Workflow.SkillContext.cs（结构零改动）</para>
    /// </summary>
    public class SkillContext
    {
        public IDictionary<string, object> Inputs { get; set; } = new Dictionary<string, object>();
        public string WorkflowInstanceId { get; set; } = string.Empty;
        public string NodeId { get; set; } = string.Empty;
        public ILogger? Logger { get; set; }
    }

    /// <summary>
    /// Skill 执行结果（标准输出 + 元数据）
    /// </summary>
    public class SkillResult
    {
        public bool Success { get; set; }
        public IDictionary<string, object> Outputs { get; set; } = new Dictionary<string, object>();
        public double? Confidence { get; set; }
        public string? Error { get; set; }
        public long DurationMs { get; set; }
        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }

        public static SkillResult Ok(IDictionary<string, object>? outputs = null, double? confidence = null)
            => new() { Success = true, Outputs = outputs ?? new Dictionary<string, object>(), Confidence = confidence };

        public static SkillResult Fail(string error)
            => new() { Success = false, Error = error };
    }

    /// <summary>Skill 元数据（反射分析结果，用于验证/画布端口推断）</summary>
    public class SkillMetadata
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ReturnType { get; set; } = "json";
        public string Description { get; set; } = string.Empty;
        public string ClassPath { get; set; } = string.Empty;
        public string MethodName { get; set; } = "ExecuteAsync";
        public List<SkillPortInfo> InputPorts { get; set; } = new();
    }

    /// <summary>Skill 输入端口信息</summary>
    public class SkillPortInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "json";
        public bool Required { get; set; }
        public string? DefaultValue { get; set; }
        public string Description { get; set; } = string.Empty;
        /// <summary>绑定模式：Link / LinkOrConstant / Enum</summary>
        public string BindMode { get; set; } = "LinkOrConstant";
        /// <summary>字典编码（BindMode=Enum 时有值）</summary>
        public string? EnumSource { get; set; }
    }
}
