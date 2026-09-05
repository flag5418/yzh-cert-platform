using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YZH.Core.Workflow
{
    /// <summary>
    /// Skill 参数声明（端口：name + type + 必填 + 说明）。
    /// 用途：① 画布生成输入表单模板；② 必填校验；③ 强约束输出契约校验；④ 与 wf_skill 表登记互相校验（登记即用）。
    /// </summary>
    public class SkillParam
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>string / number / date / boolean / json（复杂结构一律 json）</summary>
        public string Type { get; set; } = "json";
        public bool Required { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Skill 基类：声明式元数据 + ExecuteAsync 模板方法。
    /// - 内置 Skill 继承本类实现 ExecuteCoreAsync；
    /// - 自定义 Skill 经反射加载（wf_skill_reflection.class_path → ReflectionSkillLoader，DI 优先实例化），
    ///   声明式元数据即"登记即用"的落点——代码声明与 wf_skill 表登记互相校验；
    /// - ExecuteAsync 统一处理：必填校验 → 执行 → 标准输出包装 → 强约束输出契约校验 → 异常包装，子类只需关注业务逻辑。
    /// 
    /// V1.3 标准输出端口约定：
    /// - 所有功能节点自动包含 success(boolean) / error(string) / result(json) 三个标准输出端口
    /// - 子类只需声明业务自定义输出端口（OutputDecls），标准端口由基类自动包装
    /// - 下游节点统一引用 nX.success 判断是否成功，nX.result 取结果
    /// </summary>
    public abstract class SkillBase : ISkillNode
    {
        public abstract string SkillCode { get; }
        public abstract string SkillName { get; }
        /// <summary>功能分类：data_access / data_process / ai_judge / ai_generate / output（面板分组）</summary>
        public abstract string Category { get; }
        /// <summary>0=逻辑性（纯函数，无副作用）/ 1=功能性（读写外部）</summary>
        public virtual bool SideEffect => true;
        /// <summary>输出契约强度：1=强约束（按 OutputDecls 校验）/ 0=弱约束（ai_node 放行）</summary>
        public virtual bool OutputStrict => true;
        /// <summary>主输出类型：string / number / date / boolean / json</summary>
        public virtual string ReturnType => "json";

        /// <summary>输入声明（表单模板 + 必填校验）；ai_node 等动态输入返回空</summary>
        public virtual IReadOnlyList<SkillParam> InputDecls { get; } = Array.Empty<SkillParam>();
        /// <summary>业务自定义输出声明（不含标准端口）；OutputStrict=true 时强约束校验</summary>
        public virtual IReadOnlyList<SkillParam> OutputDecls { get; } = Array.Empty<SkillParam>();

        /// <summary>
        /// 标准输出端口（所有功能节点自动包含，子类无需声明）。
        /// </summary>
        public static IReadOnlyList<SkillParam> StandardOutputDecls { get; } = new[]
        {
            new SkillParam { Name = "success", Type = "boolean", Required = true, Description = "是否执行成功" },
            new SkillParam { Name = "error", Type = "string", Required = false, Description = "失败时的错误信息" },
            new SkillParam { Name = "result", Type = "json", Required = true, Description = "执行结果（业务数据）" }
        };

        /// <summary>最终输出声明 = 标准端口 + 业务自定义端口</summary>
        public IReadOnlyList<SkillParam> AllOutputDecls =>
            StandardOutputDecls.Concat(OutputDecls).ToList();

        public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            // 标准输出结构
            var standardOutputs = new Dictionary<string, object>
            {
                ["success"] = false,
                ["error"] = string.Empty,
                ["result"] = new Dictionary<string, object>()
            };

            try
            {
                // ① 必填入参校验
                var missing = InputDecls
                    .Where(p => p.Required && (context.Inputs == null
                                               || !context.Inputs.TryGetValue(p.Name, out var v)
                                               || v == null
                                               || string.IsNullOrWhiteSpace(v?.ToString())))
                    .Select(p => p.Name)
                    .ToList();
                if (missing.Count > 0)
                {
                    standardOutputs["error"] = $"{SkillCode} 缺少必填入参: {string.Join(", ", missing)}";
                    return SkillResult.Ok(standardOutputs, null);
                }

                // ② 执行子类业务逻辑
                var result = await ExecuteCoreAsync(context, ct);

                // ③ 包装标准输出
                if (result.Success)
                {
                    standardOutputs["success"] = true;
                    standardOutputs["error"] = string.Empty;
                    standardOutputs["result"] = result.Outputs ?? new Dictionary<string, object>();

                    // 业务自定义输出端口平铺到顶层（供下游兼容引用 nX.field_value 等）
                    if (result.Outputs != null)
                    {
                        foreach (var kv in result.Outputs)
                            standardOutputs[kv.Key] = kv.Value;
                    }

                    // ④ 强约束输出契约校验（校验业务自定义端口）
                    if (OutputStrict)
                    {
                        var missingOut = OutputDecls
                            .Where(p => p.Required && (!result.Outputs.TryGetValue(p.Name, out var v) || v == null))
                            .Select(p => p.Name)
                            .ToList();
                        if (missingOut.Count > 0)
                        {
                            standardOutputs["success"] = false;
                            standardOutputs["error"] = $"{SkillCode} 输出契约校验失败，缺少输出端口: {string.Join(", ", missingOut)}";
                            standardOutputs["result"] = new Dictionary<string, object>();
                        }
                    }
                }
                else
                {
                    standardOutputs["success"] = false;
                    standardOutputs["error"] = result.Error ?? string.Empty;
                    standardOutputs["result"] = new Dictionary<string, object>();
                }

                var finalResult = SkillResult.Ok(standardOutputs, result.Confidence);
                finalResult.PromptTokens = result.PromptTokens;
                finalResult.CompletionTokens = result.CompletionTokens;
                return finalResult;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                standardOutputs["success"] = false;
                standardOutputs["error"] = $"{SkillCode} 执行异常: {ex.Message}";
                standardOutputs["result"] = new Dictionary<string, object>();
                return SkillResult.Ok(standardOutputs, null);
            }
        }

        /// <summary>子类实现真实业务逻辑，返回业务输出（标准端口由基类自动包装）</summary>
        protected abstract Task<SkillResult> ExecuteCoreAsync(SkillContext context, CancellationToken ct);
    }
}
