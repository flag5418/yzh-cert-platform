using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;

namespace CertPlatform.Auditor.Services.Ent.Normalize
{
    /// <summary>
    ///     ★ <see cref="EnterpriseNormalizeExecutor"/> 的<b>单例桥接壳</b>。
    ///
    ///     <para><b>为什么需要它</b>：<c>QueueManager</c> 注册为<b>单例</b>，且构造器注入
    ///     <c>IEnumerable&lt;IYzhTaskExecutor&gt;</c> ⇒ <b>所有执行器实例都必须是单例</b>，
    ///     否则 DI 会在构造 <c>QueueManager</c> 时抛
    ///     「Cannot consume scoped service from singleton」。</para>
    ///
    ///     <para>而执行器依赖 <see cref="DocumentFillOrchestrator"/>（→ <c>IDbOrm</c> /
    ///     <c>IObjectStorage</c>，<b>均为 Scoped</b>）⇒ 不能直接注册成单例。
    ///     解法（与 <c>OfficeConvertTaskExecutor</c> 同一精神）：本壳只持
    ///     <see cref="IServiceProvider"/>（单例安全），每次调用开一个 scope。</para>
    ///
    ///     <para>⛔ <b>不要</b>把 scoped 服务缓存进字段 —— <c>QueueManager</c> 按
    ///     <c>queue_max_concurrent</c>（默认 4）起并发 worker，同一执行器实例会被<b>并发调用</b>，
    ///     缓存字段会导致 scope 串号。</para>
    /// </summary>
    public class EnterpriseNormalizeExecutorAdapter : IYzhTaskExecutor
    {
        private readonly IServiceProvider _serviceProvider;

        public EnterpriseNormalizeExecutorAdapter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>★ 取自执行器本体的常量，⛔ 不在此重复硬编码</summary>
        public string TaskType => EnterpriseNormalizeExecutor.TaskTypeName;

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var inner = scope.ServiceProvider.GetRequiredService<EnterpriseNormalizeExecutor>();
            return await inner.ExecuteAsync(task, cancellationToken);
        }

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var inner = scope.ServiceProvider.GetRequiredService<EnterpriseNormalizeExecutor>();
            return inner.OnTaskStateChangedAsync(task, newStatus, message);
        }
    }
}
