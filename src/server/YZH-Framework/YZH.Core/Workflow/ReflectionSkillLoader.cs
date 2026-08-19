using System;
using System.Linq;
using System.Reflection;

namespace YZH.Core.Workflow
{
    /// <summary>
    /// 反射 Skill 加载器（V2 静态方法版）。
    /// 不再创建实例，仅负责定位静态类和方法，委托给 SkillExecutor 执行。
    /// 保留此接口供 SkillRegistry 按需调用。
    /// </summary>
    public interface IReflectionSkillLoader
    {
        /// <summary>分析 classPath + methodName，返回 Skill 元数据</summary>
        SkillMetadata? Analyze(string classPath, string methodName = "ExecuteAsync");
    }

    public class ReflectionSkillLoader : IReflectionSkillLoader
    {
        private readonly SkillExecutor _executor;
        public ReflectionSkillLoader(SkillExecutor executor)
        {
            _executor = executor;
        }

        public SkillMetadata? Analyze(string classPath, string methodName = "ExecuteAsync")
        {
            return _executor.Analyze(classPath, methodName);
        }
    }
}
