using System;
using System.Reflection;

namespace CertPlatform.Admin.Tests.Fill
{
    /// <summary>
    ///     <b>最小动态假实现</b>（.NET 内置 <see cref="DispatchProxy"/>，零第三方包）。
    ///
    ///     <para><b>为什么不用 Moq / NSubstitute</b>：本仓测试环境<b>无网络</b>（实测
    ///     <c>api.nuget.org</c> 不可达、本地 NuGet 缓存亦无这两个包）⇒ 新增包 = 直接 restore 失败。
    ///     <see cref="DispatchProxy"/> 是 BCL 自带能力，不引入任何依赖。</para>
    ///
    ///     <para><b>用法</b>（只预置被测代码真正调用的成员；其余一律抛
    ///     <see cref="NotSupportedException"/>，⛔ <b>不静默返回 <c>default</c></b>
    ///     —— 静默 default 会让「被测代码调了没预置的方法」伪装成通过）：</para>
    ///     <code>
    ///     var core = Fake.Of&lt;ICoreResourceQuery&gt;((m, a) =&gt; m.Name switch
    ///     {
    ///         "GetFillParamDefsAsync" =&gt; Task.FromResult(defs),
    ///         _ =&gt; throw new NotSupportedException(m.Name),
    ///     });
    ///     </code>
    ///
    ///     <para>⚠️ 代理类<b>不能是 <c>sealed</c></b>：<see cref="DispatchProxy.Create{T,TProxy}"/>
    ///     会生成它的子类。</para>
    /// </summary>
    public class FakeProxy<T> : DispatchProxy where T : class
    {
        /// <summary>调用处理器：(被调方法, 实参数组) → 返回值（返回类型必须与方法声明一致）。</summary>
        internal Func<MethodInfo, object?[]?, object?> Handler { get; set; } =
            (m, _) => throw new NotSupportedException($"未预置假实现：{m.DeclaringType?.Name}.{m.Name}");

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => Handler(targetMethod!, args);
    }

    /// <summary>造假实现的工厂（见 <see cref="FakeProxy{T}"/> 说明）。</summary>
    public static class Fake
    {
        /// <summary>按「方法 + 实参」分派。</summary>
        public static T Of<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
        {
            var proxy = DispatchProxy.Create<T, FakeProxy<T>>();
            ((FakeProxy<T>)(object)proxy).Handler = handler;
            return proxy;
        }

        /// <summary>按「方法名 + 实参」分派（未命中即抛）。</summary>
        public static T Of<T>(Func<string, object?[]?, object?> byName) where T : class
            => Of<T>((m, a) => byName(m.Name, a));
    }
}
