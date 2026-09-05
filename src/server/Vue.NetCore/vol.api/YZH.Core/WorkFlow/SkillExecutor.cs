using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace YZH.Core.Workflow
{
    /// <summary>
    /// Skill 执行器：反射调用静态方法 + 标准输出包装。
    /// - 从 wf_skill_reflection 表读取 classPath + methodName
    /// - 反射获取静态类和方法，自动分析参数（业务参数/依赖参数/框架参数）
    /// - 从 SkillContext.Inputs 绑定业务参数，从 DI 容器获取依赖参数
    /// - 调用静态方法，包装标准输出 { success, error, result }
    /// </summary>
    public class SkillExecutor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SkillExecutor> _logger;

        public SkillExecutor(IServiceProvider serviceProvider, ILogger<SkillExecutor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// 执行 Skill。
        /// </summary>
        public async Task<SkillResult> ExecuteAsync(
            string classPath, string methodName,
            SkillContext context, CancellationToken ct = default)
        {
            var standardOutputs = new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = string.Empty,
                ["result"] = new Dictionary<string, object>()
            };

            try
            {
                // ① 定位类型
                var type = ResolveType(classPath);
                if (type == null)
                {
                    standardOutputs["error"] = $"无法找到类型: {classPath}";
                    return SkillResult.Ok(standardOutputs, null);
                }

                var skillAttr = type.GetCustomAttribute<SkillAttribute>();
                if (skillAttr == null)
                {
                    standardOutputs["error"] = $"类型 {classPath} 缺少 [Skill] 特性";
                    return SkillResult.Ok(standardOutputs, null);
                }

                // ② 定位方法
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    standardOutputs["error"] = $"类型 {classPath} 中找不到静态方法: {methodName}";
                    return SkillResult.Ok(standardOutputs, null);
                }

                // ③ 分析参数 + 绑定
                var args = BindArguments(method.GetParameters(), context, ct);

                // ④ 必填校验
                var missing = ValidateRequired(method.GetParameters(), context);
                if (missing.Count > 0)
                {
                    standardOutputs["error"] = $"{skillAttr.Code} 缺少必填入参: {string.Join(", ", missing)}";
                    return SkillResult.Ok(standardOutputs, null);
                }

                // ⑤ 调用静态方法
                var result = await (Task<SkillResult>)method.Invoke(null, args)!;

                // ⑥ 包装标准输出
                if (result.Success)
                {
                    standardOutputs["success"] = true;
                    standardOutputs["result"] = result.Outputs ?? new Dictionary<string, object>();
                    // 业务输出平铺到顶层
                    if (result.Outputs != null)
                        foreach (var kv in result.Outputs)
                            standardOutputs[kv.Key] = kv.Value;
                }
                else
                {
                    standardOutputs["error"] = result.Error ?? string.Empty;
                }

                var final = SkillResult.Ok(standardOutputs, result.Confidence);
                final.PromptTokens = result.PromptTokens;
                final.CompletionTokens = result.CompletionTokens;
                return final;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Skill 执行异常: {ClassPath}.{MethodName}", classPath, methodName);
                standardOutputs["error"] = $"执行异常: {ex.Message}";
                return SkillResult.Ok(standardOutputs, null);
            }
        }

        /// <summary>
        /// 反射分析 Skill 的元数据（用于验证接口）。
        /// </summary>
        public SkillMetadata? Analyze(string classPath, string methodName = "ExecuteAsync")
        {
            var type = ResolveType(classPath);
            if (type == null) return null;

            var skillAttr = type.GetCustomAttribute<SkillAttribute>();
            if (skillAttr == null) return null;

            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            if (method == null) return null;

            var inputPorts = new List<SkillPortInfo>();
            foreach (var p in method.GetParameters())
            {
                // 跳过 [FromService] 参数
                if (p.GetCustomAttribute<FromServiceAttribute>() != null) continue;
                // 跳过 CancellationToken
                if (p.ParameterType == typeof(CancellationToken) ||
                    p.ParameterType == typeof(CancellationToken?)) continue;

                var paramAttr = p.GetCustomAttribute<SkillParamAttribute>();

                inputPorts.Add(new SkillPortInfo
                {
                    Name = p.Name ?? string.Empty,
                    Type = MapCSharpType(p.ParameterType),
                    Required = !p.HasDefaultValue,
                    DefaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null,
                    Description = paramAttr?.Description ?? string.Empty,
                    BindMode = paramAttr?.BindMode.ToString() ?? "LinkOrConstant",
                    EnumSource = paramAttr?.EnumSource
                });
            }

            return new SkillMetadata
            {
                Code = skillAttr.Code,
                Name = skillAttr.Name,
                ReturnType = skillAttr.ReturnType,
                Description = skillAttr.Description,
                ClassPath = classPath,
                MethodName = methodName,
                InputPorts = inputPorts
            };
        }

        // ── 私有方法 ──

        private static Type? ResolveType(string classPath)
        {
            var type = Type.GetType(classPath);
            if (type != null) return type;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = asm.GetType(classPath, false);
                    if (type != null) return type;
                }
                catch { /* ignore */ }
            }
            return null;
        }

        private static string MapCSharpType(Type type)
        {
            // 去掉 Nullable
            var underlying = Nullable.GetUnderlyingType(type) ?? type;

            if (underlying == typeof(string)) return "string";
            if (underlying == typeof(bool)) return "boolean";
            if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) || underlying == typeof(DateOnly))
                return "date";
            if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(double) ||
                underlying == typeof(decimal) || underlying == typeof(float))
                return "number";
            return "json";
        }

        private object?[] BindArguments(ParameterInfo[] parameters, SkillContext context, CancellationToken ct)
        {
            var args = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];

                if (p.GetCustomAttribute<FromServiceAttribute>() != null)
                {
                    args[i] = _serviceProvider.GetService(p.ParameterType);
                }
                else if (p.ParameterType == typeof(CancellationToken))
                {
                    args[i] = ct;
                }
                else
                {
                    // 业务参数：从 inputs 字典按名取值
                    if (context.Inputs != null && context.Inputs.TryGetValue(p.Name!, out var val))
                    {
                        args[i] = ConvertValue(val, p.ParameterType);
                    }
                    else if (p.HasDefaultValue)
                    {
                        args[i] = p.DefaultValue;
                    }
                    else
                    {
                        args[i] = p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null;
                    }
                }
            }
            return args;
        }

        private static List<string> ValidateRequired(ParameterInfo[] parameters, SkillContext context)
        {
            var missing = new List<string>();
            foreach (var p in parameters)
            {
                if (p.GetCustomAttribute<FromServiceAttribute>() != null) continue;
                if (p.ParameterType == typeof(CancellationToken)) continue;
                if (p.HasDefaultValue) continue; // 有默认值=选填

                // 必填参数检查
                if (context.Inputs == null ||
                    !context.Inputs.TryGetValue(p.Name!, out var v) ||
                    v == null ||
                    string.IsNullOrWhiteSpace(v?.ToString()))
                {
                    missing.Add(p.Name!);
                }
            }
            return missing;
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null) return null;

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlying == typeof(string)) return value.ToString();
            if (underlying == typeof(bool)) return Convert.ToBoolean(value);
            if (underlying == typeof(int)) return Convert.ToInt32(value);
            if (underlying == typeof(long)) return Convert.ToInt64(value);
            if (underlying == typeof(double)) return Convert.ToDouble(value);
            if (underlying == typeof(decimal)) return Convert.ToDecimal(value);
            if (underlying == typeof(DateTime)) return Convert.ToDateTime(value);
            if (underlying == typeof(DateTimeOffset)) return DateTimeOffset.Parse(value.ToString()!);

            // 其他类型（json）原样返回
            return value;
        }
    }

    /// <summary>Skill 元数据（反射分析结果）</summary>
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
        /// <summary>字典编码（BindMode=Enum 时有值），对应 Sys_Dictionary.DicNo</summary>
        public string? EnumSource { get; set; }
    }
}
