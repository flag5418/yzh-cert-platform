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

                // ④ ★ 入参完备性校验（判据 = [SkillParam(AllowEmpty)]，见 AllowsEmpty）
                //    ★★ 2026-10-15 修：**先分「锅的归属」再报错**（见 CollectMissingInputs）。
                //    修复前一律报 PRECONDITION_EMPTY ⇒ 一个 `ConstantOnly` 参数（如
                //    `stamp_judge_level.judge_code`）没填，系统会给**企业**开一个它
                //    **永远填不了**的补录缺口，并把「作者没配」伪装成「数据缺失」。
                var (missingData, missingConfig) = CollectMissingInputs(parameters, context);

                // ④-a 规则作者的锅 ⇒ CONFIG_ERROR
                //   NodeExecutor 抛 WorkflowNodeFailureException ⇒ 终止当前**路径**，
                //   ⛔ **不落补录缺口**（补录解决不了配置错），提示语指向「改配置」。
                if (missingConfig.Count > 0)
                {
                    return SkillResult.Fail(
                        $"{skillAttr.Code} 配置不完整：参数 {string.Join(", ", missingConfig)} 未填写" +
                        "（该参数只能由规则作者在画布上配置，企业无法补录）",
                        WorkflowErrorCodes.ConfigError);
                }

                // ④-b 上游没给值 ⇒ PRECONDITION_EMPTY
                //   NodeExecutor 抛 WorkflowDataMissingException ⇒ 全局中断当前检查项 +
                //   落补录缺口 + 标「不可判定」（链路已存在，直接复用）。
                if (missingData.Count > 0)
                {
                    return SkillResult.Fail(
                        $"{skillAttr.Code} 上游未提供必要数据：{string.Join(", ", missingData)}",
                        WorkflowErrorCodes.PreconditionEmpty);
                }

                // ⑤ 调用静态方法
                var result = await (Task<SkillResult>)method.Invoke(null, args)!;

                // ⑥ 统一信封包装
                var wrapped = Wrap(skillAttr, result);

                // ⑦ ★ 运行级登记（[Skill(RegistersTo)]）—— 把主输出追加进上下文里的池。
                //    ⛔ 放这里而不是 NodeExecutor：只有本类拿得到 [Skill] 特性 ⇒ 零硬编码。
                RegisterToPool(skillAttr, context, wrapped);

                return wrapped;
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
                // ★ 跳过 [FromContext] 参数 —— 值由引擎从运行上下文注入，
                //   ⛔ 规则作者看不到也填不了 ⇒ 必须不出现在画布端口上（否则又是一个要手填 GUID 的空框）。
                if (p.GetCustomAttribute<FromContextAttribute>() != null) continue;
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
        /// ★ 收集「声明了不允许为空、但实际为空」的参数，并按<b>锅的归属</b>分成两桶。
        ///
        /// <para><b>返回值</b>：<c>MissingData</c> = <b>上游没给值</b>（报
        /// <c>PRECONDITION_EMPTY</c>，处置人 = <b>企业</b>）；
        /// <c>MissingConfig</c> = <b>规则作者没配</b>（报 <c>CONFIG_ERROR</c>，处置人 = <b>规则作者</b>）。</para>
        ///
        /// <para><b>★★ 为什么必须分桶（2026-10-15 修）</b>：<c>PRECONDITION_EMPTY</c> 的引擎动作是
        /// <b>全局中断当前检查项 + 落补录缺口 + 标「不可判定」</b>（见 <c>WorkflowErrorCodes</c>）。
        /// 若一个 <c>ConstantOnly</c> 参数为空也报它，系统就会给企业开一个
        /// <b>它永远填不了</b>的补录缺口 —— 把「作者没配」伪装成「数据缺失」，用户按提示去补录
        /// 却发现无从下手。这正是 <see cref="FromContextAttribute"/> 注释里点名的反模式。</para>
        ///
        /// <para><b>判据（唯一口径）= 这个值「有没有可能来自上游」</b>：</para>
        /// <list type="number">
        /// <item><c>ConstantOnly</c> / <c>Enum</c> ⇒ 值<b>只能</b>来自规则作者的手填/选择
        ///       ⇒ 空 = <b>配置错</b>。</item>
        /// <item><c>Link</c> / <c>LinkOrConstant</c> ⇒ 若<b>确实没有来源</b>
        ///       （<see cref="SkillContext.ConnectedPorts"/> 可用且不含该端口 —— 既没连线、
        ///       也没非空常量）⇒ 谈不上「上游没给值」⇒ <b>配置错</b>。</item>
        /// <item>其余（端口确有来源，只是取到了空）⇒ <b>上游没给值</b> ⇒ 数据缺口。</item>
        /// </list>
        ///
        /// <para>⚠️ <c>ConnectedPorts</c> 为空集合（如直接单测调用、上下文未装配）时
        /// <b>保守回落到数据缺口</b> —— 与 branch 裁剪的保守策略同源：<b>判据缺失时不误杀</b>
        /// （否则会把「合法的上游为空」错报成配置错，反而更难排查）。</para>
        ///
        /// <para>⚠️ 修复前此方法的判据是 <c>if (p.HasDefaultValue) continue;</c>，
        /// 而所有 skill 的参数都写了 <c>= null</c> ⇒ <c>missing</c> 恒空 ⇒ 死代码。</para>
        /// </summary>
        private static (List<string> MissingData, List<string> MissingConfig) CollectMissingInputs(
            ParameterInfo[] parameters, SkillContext context)
        {
            var missingData = new List<string>();
            var missingConfig = new List<string>();
            var connected = context.ConnectedPorts;

            foreach (var p in parameters)
            {
                if (p.GetCustomAttribute<FromServiceAttribute>() != null) continue;
                // ★ 跳过 [FromContext] —— 值由引擎装配，缺失只说明「本次运行上下文没带这个键」，
                //   ⛔ 不是规则作者能补的 ⇒ 报 PRECONDITION_EMPTY 会把人引到「去补录」的错误方向。
                if (p.GetCustomAttribute<FromContextAttribute>() != null) continue;
                if (p.ParameterType == typeof(CancellationToken) ||
                    p.ParameterType == typeof(CancellationToken?)) continue;

                if (AllowsEmpty(p)) continue;   // 允许空 ⇒ 不校验

                if (context.Inputs != null &&
                    context.Inputs.TryGetValue(p.Name!, out var v) &&
                    !IsEmptyValue(v))
                {
                    continue;                   // 有值 ⇒ 无需校验
                }

                if (IsConfigSupplied(p, connected)) missingConfig.Add(p.Name!);
                else missingData.Add(p.Name!);
            }

            return (missingData, missingConfig);
        }

        /// <summary>
        /// 该参数的空值是否<b>只可能</b>是「规则作者没配」（⇒ <c>CONFIG_ERROR</c>）。
        ///
        /// <para>判据 = <b>这个值有没有可能来自上游数据</b>：只能手填/选字典的
        /// （<c>ConstantOnly</c> / <c>Enum</c>）⇒ 是；连来源都没有的（端口不在
        /// <paramref name="connectedPorts"/> 里）⇒ 是。</para>
        ///
        /// <para>⚠️ <paramref name="connectedPorts"/> 为空（上下文未装配 / 直接单测调用）时
        /// <b>保守返回 <c>false</c></b> ⇒ 归入数据缺口，⛔ 不误杀。</para>
        /// </summary>
        private static bool IsConfigSupplied(ParameterInfo p, ISet<string>? connectedPorts)
        {
            var bindMode = p.GetCustomAttribute<SkillParamAttribute>()?.BindMode
                           ?? SkillParamBindMode.LinkOrConstant;

            // ① 只能由规则作者提供 ⇒ 与上游无关，空就是没配
            if (bindMode == SkillParamBindMode.ConstantOnly || bindMode == SkillParamBindMode.Enum)
                return true;

            // ② 端口确实没有来源（没连线、也没有非空常量）⇒ 谈不上「上游没给值」
            //    ⚠️ connectedPorts 为空集合时保守返回 false（不误杀）
            if (connectedPorts != null && connectedPorts.Count > 0 &&
                !connectedPorts.Contains(p.Name!))
                return true;

            return false;
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
                else if (p.GetCustomAttribute<FromContextAttribute>() != null)
                {
                    // ★ 运行上下文注入（2026-10-15）：键名 = 特性显式指定，缺省 = 参数名。
                    //   ⛔ 刻意**不回落 Inputs** —— 该参数在画布上不存在，
                    //   回落只会让「键名写错」伪装成「有人手动传了值」，更难查。
                    var ctxAttr = p.GetCustomAttribute<FromContextAttribute>()!;
                    var ctxKey = string.IsNullOrEmpty(ctxAttr.Key) ? p.Name! : ctxAttr.Key!;

                    if (context.ContextParams != null &&
                        context.ContextParams.TryGetValue(ctxKey, out var ctxVal))
                    {
                        args[i] = ConvertValue(ctxVal, p.ParameterType);
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

        /// <summary>
        /// ★ <b>运行级登记</b>（2026-10-15 新增）—— 把本节点的<b>主输出</b>追加进
        /// <c>context.ContextParams[skillAttr.RegistersTo]</c> 指向的池。
        ///
        /// <para><b>★ 为什么放在本类而不是 <c>NodeExecutor</c></b>：只有这里拿得到
        /// <see cref="SkillAttribute"/>（<c>NodeExecutor</c> 手上只有一个 skillCode 字符串）
        /// ⇒ 放这里可<b>零硬编码</b>地支持任意 skill 声明登记（新增 skill 时引擎不用改）。</para>
        ///
        /// <para><b>⛔ 池不存在时静默跳过，不报错</b>：单节点测试（<c>test/node</c>）等场景
        /// 没有 item 级池。这与「配了不生效且不报错」不同 —— 池是<b>引擎装配的</b>，
        /// 不存在只说明「本次不是完整 item 执行」，⛔ 不是配置缺陷。</para>
        ///
        /// <para><b>幂等性由调用链保证</b>：节点被跨路径复用时（<c>WorkflowInterpreter.ExecutePathAsync</c>）
        /// <b>不会重新执行</b> ⇒ 本方法不会被二次调用 ⇒ 池里不会有重复项。</para>
        /// </summary>
        private static void RegisterToPool(SkillAttribute skillAttr, SkillContext context, SkillResult wrapped)
        {
            var poolName = skillAttr.RegistersTo;
            if (string.IsNullOrWhiteSpace(poolName)) return;
            if (!wrapped.Success) return;                       // ⛔ 失败不登记（池里只放有效产物）
            if (context.ContextParams == null) return;
            if (!context.ContextParams.TryGetValue(poolName!, out var poolObj)) return;
            if (poolObj is not List<object> pool) return;

            // 主输出 = Wrap 已写入信封的 "result"（唯一口径，与 ResolveMainOutput 一致）
            object? main = wrapped.Outputs.TryGetValue("result", out var r) ? r : null;
            if (main == null) return;

            pool.Add(main);
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
