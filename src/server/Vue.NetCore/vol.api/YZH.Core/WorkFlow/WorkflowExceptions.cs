using System;

namespace YZH.Core.Workflow
{
    /// <summary>
    /// 工作流执行异常（拓扑环 / 未知 Skill / 节点失败）。
    /// </summary>
    public class WorkflowExecutionException : Exception
    {
        public WorkflowExecutionException(string message) : base(message) { }
        public WorkflowExecutionException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Skill 编码不存在于注册表。
    /// </summary>
    public class UnknownSkillException : WorkflowExecutionException
    {
        public string SkillCode { get; }
        public UnknownSkillException(string skillCode)
            : base($"未知 Skill 编码: {skillCode}")
        {
            SkillCode = skillCode;
        }
    }
}
