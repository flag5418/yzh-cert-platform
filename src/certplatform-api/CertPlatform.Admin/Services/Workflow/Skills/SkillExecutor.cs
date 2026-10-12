using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CertPlatform.Shared.Workflow.Models;

namespace CertPlatform.Admin.Services.Workflow.Skills
{
    /// <summary>
    /// Skill 执行器：反射调用静态方法 + 标准输出包装
    /// <para>移植自：旧 YZH.Core.Workflow.SkillExecutor.cs（295 行，逻辑零改动，仅 namespace 调整）</para>
    /// <para>职责：</para>
    /// <para>1. 从 wf_skill_reflection 表的 classPath + methodName 反射定位静态方法</para>
    /// <para>2. 自动分析参数（业务参数 / [FromService] 依赖参数 / CancellationToken）</para>
    /// <para>3. 从 SkillContext.Inputs 绑定业务参数，从 DI 容器获取依赖参数</para>
    /// <para>4. 调用静态方法，包装<b>统一信封</b> { success, errorCode, error, result, outputs, … }</para>
    ///
    /// <para><b>★★★ 2026-10-12 用户裁定后的三处语义修正</b>（原实现是三个真缺陷）：</para>
    /// <list type="number">
    /// <item><b>入参校验从死代码变活</b>：原 <c>ValidateRequired</c> 第 238 行
    ///       <c>if (p.HasDefaultValue) continue;</c> 把全部参数判成「选填」（所有 skill 的参数
    ///       都写了 <c>= null</c>）⇒ <c>missing</c> 恒空 ⇒「必填」概念从未生效。
    ///       现判据 = <see cref="IsRequired"/>（<c>[SkillParam(AllowEmpty)]</c> 三态）。</item>
    /// <item><b>失败不再被吞</b>：原实现<b>恒返回</b> <c>SkillResult.Ok</c>，把 <c>success:false</c>
    ///       塞进输出字典 ⇒ 下游 <c>NodeExecutor.ExecuteSkillAsync</c> 的
    ///       <c>if (!skillResult.Success) throw</c> <b>永不触发</b> ⇒ 业务失败不中断路径。
    ///       现在失败<b>如实返回 <c>SkillResult.Fail</c></b> 并带 <see cref="SkillResult.ErrorCode"/>。</item>
    /// <item><b><c>result</c> 从整包字典变主输出</b>：原 <c>standardOutputs["result"] = result.Outputs</c>
    ///       （整包）⇒ 连线下游拿到字典 ⇒ 实测 <c>get_field → is_empty</c> 恒判「非空」、
    ///       <c>assemble → is_empty</c> 空串也判「非空」。现按
    ///       <see cref="SkillAttribute.ResultPort"/> 提取<b>那一个最该被连线的值</b>。</item>
    /// </list>
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
        /// 执行 Skill
        /// </summary>
        public async Task<SkillResult> ExecuteAsync(
            string classPath, string methodName,
            SkillContext context, CancellationToken ct = default)
        {
            try
            {
                // ① 定位类型
                var type = ResolveType(classPath);
                if (type == null)
                    return SkillResult.Fail($"无法找到类型: {classPath}", WorkflowErrorCodes.ConfigError);

                var skillAttr = type.GetCustomAttribute<SkillAttribute>();
                if (skillAttr == null)
                    return SkillResult.Fail($"类型 {classPath} 缺少 [Skill] 特性", WorkflowErrorCodes.ConfigError);

                // ② 定位方法
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                    return SkillResult.Fail(
                        $"类型 {classPath} 中找不到静态方法: {methodName}", WorkflowErrorCodes.ConfigError);

                var parameters = method.GetParameters();

                // ③ 参数绑定
                var args = BindArguments(parameters, context, ct);

                // ④ ★ 入参完备性校验（判据 = [SkillParam(AllowEmpty)]，见 IsRequired）
                var missing = CollectMissingInputs(parameters, context);
                if (missing.Count > 0)
                {
                    // PRECONDITION_EMPTY ⇒ NodeExecutor 会抛 WorkflowDataMissingException
                    // ⇒ 全局中断当前检查项 + 落补录缺口 + 标「不可判定」（链路已存在，直接复用）
                    return SkillResult.Fail(
                        $"{skillAttr.Code} 参数为空且该参数不允许为空: {string.Join(", ", missing)}",
                        WorkflowErrorCodes.PreconditionEmpty);
                }

                // ⑤ 调用静态方法
                var result = await (Task<SkillResult>)method.Invoke(null, args)!;

                // ⑥ 统一信封包装
                return Wrap(skillAttr, result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Skill 执行异常: {ClassPath}.{MethodName}", classPath, methodName);
                return SkillResult.Fail($"执行异常: {ex.Message}", WorkflowErrorCodes.SystemError);
            }
        }

        /// <summary>
        /// 把 skill 自报的 <see cref="SkillResult"/> 包成<b>统一信封</b>。
        ///
        /// <para><b>形状</b>（三层同形：节点 / skill / 路径）：</para>
        /// <code>
        /// {
        ///   "success":  true,          // 程序层：这个节点跑完了没有
        ///   "errorCode": null,         // 失败分类码（成功时 null）
        ///   "error":    "",            // 人读原因（成功时空）
        ///   "result":   "实际的值",     // ★ 主输出（单值 / 数组），⛔ 不是整包字典
        ///   "outputs":  { 端口名: 值 }, // 全量端口值（含 result），供多输出节点使用
        ///   …业务键平铺（向后兼容既有消费方）
        /// }
        /// </code>
        ///
        /// <para><b>⚠️ 写入顺序有讲究</b>：先平铺业务键，再写信封键 ——
        /// 否则某个 skill 若恰好输出名为 <c>success</c> / <c>result</c> 的业务键，
        /// 会把信封覆盖掉（修复前正是这个顺序，靠巧合没出事）。</para>
        /// </summary>
        private SkillResult Wrap(SkillAttribute skillAttr, SkillResult result)
        {
            // ── 失败：如实返回，并透传分类码 ──
            if (!result.Success)
            {
                var code = string.IsNullOrWhiteSpace(result.ErrorCode)
                    // 兜底：skill 主动 Fail 绝大多数是「入参 / 前置不满足业务规则」⇒ 规则作者可修。
                    // 真正的系统异常走 catch(Exception) ⇒ SYSTEM_ERROR，不会落到这里。
                    ? WorkflowErrorCodes.ConfigError
                    : result.ErrorCode;

                _logger.LogWarning("[SKILL_FAIL] {Skill} code={Code} error={Error}",
                    skillAttr.Code, code, result.Error);

                return new SkillResult
                {
                    Success = false,
                    Error = result.Error,
                    ErrorCode = code,
                    Outputs = result.Outputs ?? new Dictionary<string, object>(),
                    Confidence = result.Confidence,
                    DurationMs = result.DurationMs,
                    PromptTokens = result.PromptTokens,
                    CompletionTokens = result.CompletionTokens
                };
            }

            var business = result.Outputs ?? new Dictionary<string, object>();

            var outputs = new Dictionary<string, object>();

            // ① 业务键平铺到顶层 —— 兼容既有消费方（fieldValue / assembled_text / rows / state …）
            foreach (var kv in business)
                outputs[kv.Key] = kv.Value;

            // ② ★ result = 主输出（唯一口径，见 ResolveMainOutput）
            var main = ResolveMainOutput(business, skillAttr.ResultPort, skillAttr.Code);
            outputs["result"] = main!;

            // ③ 全量端口值 —— 多输出节点（nc_validate.items / nc_conclusion_calc.stats）
            //    从这里取，不必担心与信封键撞名
            outputs["outputs"] = business;

            // ④ 信封键最后写（保证不被业务键覆盖）
            outputs["success"] = true;
            outputs["errorCode"] = null!;
            outputs["error"] = string.Empty;

            var final = SkillResult.Ok(outputs, result.Confidence);
            final.DurationMs = result.DurationMs;
            final.PromptTokens = result.PromptTokens;
            final.CompletionTokens = result.CompletionTokens;
            return final;
        }

        /// <summary>
        /// 从业务输出里挑出「主输出」—— 即连线下游靠 <c>result</c> 能拿到的那<b>一个</b>值。
        ///
        /// <para><b>推断顺序</b>：</para>
        /// <list type="number">
        /// <item><c>[Skill(ResultPort=…)]</c> 指定的键（<b>唯一权威</b>，逐个 skill 显式声明）</item>
        /// <item>业务输出自带的 <c>result</c> 键（<c>compare</c> / <c>is_empty</c> / <c>is_std_file_missing</c> 用这个）</item>
        /// <item>只有一个键 ⇒ 取该键（旧行为保留）</item>
        /// <item>都不满足 ⇒ 整包字典 + <b>Warning 日志</b>（兜底，⛔ 不应长期存在 ——
        ///       出现说明有 skill 漏声明 <c>ResultPort</c>，下游 <c>is_empty</c> 会误判「非空」）</item>
        /// </list>
        /// </summary>
        private object? ResolveMainOutput(
            IDictionary<string, object> business, string? resultPort, string skillCode)
        {
            if (business == null || business.Count == 0)
                return null;

            if (!string.IsNullOrEmpty(resultPort) && business.TryGetValue(resultPort!, out var byPort))
                return byPort;

            if (business.TryGetValue("result", out var byResult))
                return byResult;

            if (business.Count == 1)
                return business.Values.First();

            _logger.LogWarning(
                "[SKILL_RESULT_PORT] Skill '{Skill}' 未声明 [Skill(ResultPort=…)] 且输出无 result 键 " +
                "⇒ result 兜底为整包字典（{Keys}）。下游 is_empty 会把它判成「非空」，请补声明。",
                skillCode, string.Join(", ", business.Keys));

            return business;
        }

        /// <summary>
        /// 反射分析 Skill 的元数据（用于验证接口 / 画布端口推断）
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
                // ★ 2026-10-12 修：原为 `Required = !p.HasDefaultValue`，
                //   与执行期判据同源才有意义 ⇒ 统一走 IsRequired/AllowsEmpty。
                var allowEmpty = AllowsEmpty(p);

                inputPorts.Add(new SkillPortInfo
                {
                    Name = p.Name ?? string.Empty,
                    Type = MapCSharpType(p.ParameterType),
                    Required = !allowEmpty,
                    AllowEmpty = allowEmpty,
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
                ResultPort = skillAttr.ResultPort,
                InputPorts = inputPorts
            };
        }

        // ── 私有方法 ──

        /// <summary>
        /// ★ <b>该参数是否允许为空</b> —— 全项目<b>唯一判据</b>（执行期与元数据端共用，口径不分叉）。
        ///
        /// <para>三态（<see cref="SkillParamEmptyPolicy"/>）：</para>
        /// <list type="bullet">
        /// <item><c>[SkillParam(AllowEmpty = SkillParamEmptyPolicy.Allow)]</c> ⇒ 允许空（显式声明优先）</item>
        /// <item><c>[SkillParam(AllowEmpty = SkillParamEmptyPolicy.Deny)]</c>  ⇒ 不允许空</item>
        /// <item><c>Unspecified</c>（未声明）⇒ 沿用旧判据 <c>HasDefaultValue</c>
        ///       （有 C# 默认值 = 选填 = 允许空）—— <b>零行为变化</b></item>
        /// </list>
        ///
        /// <para>⚠️ 用 <c>switch</c> 穷举而非 <c>??</c>：属性类型是<b>非可空枚举</b>
        /// （<c>bool?</c> 不能作特性参数，见 <see cref="SkillParamAttribute.AllowEmpty"/> 注释），
        /// 新增枚举值时会在此处编译期暴露，不会被静默吞掉。</para>
        /// </summary>
        private static bool AllowsEmpty(ParameterInfo p)
        {
            var attr = p.GetCustomAttribute<SkillParamAttribute>();
            return attr?.AllowEmpty switch
            {
                SkillParamEmptyPolicy.Allow => true,
                SkillParamEmptyPolicy.Deny => false,
                _ => p.HasDefaultValue   // Unspecified ⇒ 沿用旧行为
            };
        }

        /// <summary>参数值是否算「空」（null / 空白字符串 / 空集合）</summary>
        private static bool IsEmptyValue(object? v)
        {
            if (v == null) return true;
            if (v is string s) return string.IsNullOrWhiteSpace(s);
            if (v is ICollection c) return c.Count == 0;
            return false;
        }

        /// <summary>
        /// ★ 收集「声明了不允许为空、但实际为空」的参数名。
        /// <para>⚠️ 修复前此方法的判据是 <c>if (p.HasDefaultValue) continue;</c>，
        /// 而所有 skill 的参数都写了 <c>= null</c> ⇒ <c>missing</c> 恒空 ⇒ 死代码。</para>
        /// </summary>
        private static List<string> CollectMissingInputs(ParameterInfo[] parameters, SkillContext context)
        {
            var missing = new List<string>();
            foreach (var p in parameters)
            {
                if (p.GetCustomAttribute<FromServiceAttribute>() != null) continue;
                if (p.ParameterType == typeof(CancellationToken) ||
                    p.ParameterType == typeof(CancellationToken?)) continue;

                if (AllowsEmpty(p)) continue;   // 允许空 ⇒ 不校验

                if (context.Inputs == null ||
                    !context.Inputs.TryGetValue(p.Name!, out var v) ||
                    IsEmptyValue(v))
                {
                    missing.Add(p.Name!);
                }
            }
            return missing;
        }

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
}
