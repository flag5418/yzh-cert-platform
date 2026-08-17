using System;
using System.Linq;
using System.Reflection;

namespace YZH.Core.Workflow
{
    /// <summary>
    /// 反射 Skill 加载器：按 wf_skill_reflection.class_path 实例化自定义 Skill（登记即用）。
    /// - 优先走 DI 容器解析（构造依赖如 VOLContext/ILlmClient），失败降级无参构造；
    /// - SkillBase 声明式元数据让反射加载的 Skill 无需额外代码配置，代码声明与 wf_skill 表登记互相校验。
    /// </summary>
    public interface IReflectionSkillLoader
    {
        ISkillNode? Create(string typeName);
    }

    public class ReflectionSkillLoader : IReflectionSkillLoader
    {
        private readonly IServiceProvider _serviceProvider;
        public ReflectionSkillLoader(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ISkillNode? Create(string typeName)
        {
            var type = Type.GetType(typeName)
                       ?? AppDomain.CurrentDomain.GetAssemblies()
                           .Select(a => SafeGetType(a, typeName))
                           .FirstOrDefault(t => t != null);
            if (type == null)
                return null;
            if (!typeof(ISkillNode).IsAssignableFrom(type))
                return null;

            var fromDi = _serviceProvider.GetService(type);
            if (fromDi is ISkillNode diSkill)
                return diSkill;

            return Activator.CreateInstance(type) as ISkillNode;
        }

        private static Type? SafeGetType(Assembly assembly, string typeName)
        {
            try { return assembly.GetType(typeName, false); }
            catch { return null; }
        }
    }
}
