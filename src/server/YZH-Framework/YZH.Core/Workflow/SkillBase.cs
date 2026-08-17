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
    /// - ExecuteAsync 统一处理：必填校验 → 执行 → 强约束输出校验 → 异常包装，子类只需关注业务逻辑。
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
        /// <summary>输出声明（OutputStrict=true 时强约束校验）</summary>
        public virtual IReadOnlyList<SkillParam> OutputDecls { get; } = Array.Empty<SkillParam>();

        public async Task<SkillResult> ExecuteAsync(SkillContext context, CancellationToken ct = default)
        {
            try
            {
                var missing = InputDecls
                    .Where(p => p.Required && (context.Inputs == null
                                               || !context.Inputs.TryGetValue(p.Name, out var v)
                                               || v == null
                                               || string.IsNullOrWhiteSpace(v?.ToString())))
                    .Select(p => p.Name)
                    .ToList();
                if (missing.Count > 0)
                    return SkillResult.Fail($"{SkillCode} 缺少必填入参: {string.Join(", ", missing)}");

                var result = await ExecuteCoreAsync(context, ct);

                if (result.Success && OutputStrict)
                {
                    var missingOut = OutputDecls
                        .Where(p => p.Required && (!result.Outputs.TryGetValue(p.Name, out var v) || v == null))
                        .Select(p => p.Name)
                        .ToList();
                    if (missingOut.Count > 0)
                        return SkillResult.Fail($"{SkillCode} 输出契约校验失败，缺少输出端口: {string.Join(", ", missingOut)}");
                }
                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return SkillResult.Fail($"{SkillCode} 执行异常: {ex.Message}");
            }
        }

        /// <summary>子类实现真实业务逻辑</summary>
        protected abstract Task<SkillResult> ExecuteCoreAsync(SkillContext context, CancellationToken ct);
    }
}
