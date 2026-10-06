using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using YZH.Core.Stand.Interfaces;
using YZH.Core.Stand.Models.Queue;

namespace CertPlatform.Admin.Services.Ent.Executors
{
    /// <summary>
    /// ★ <see cref="EnterpriseDocNormalizationExecutor"/> 的<b>单例桥接壳</b>。
    ///
    /// <para><b>为什么需要它</b>：<c>QueueManager</c> 注册为 <b>单例</b>，且构造器注入
    /// <c>IEnumerable&lt;IYzhTaskExecutor&gt;</c> ⇒ <b>所有执行器实例都必须是单例</b>，
    /// 否则 DI 会在构造 <c>QueueManager</c> 时抛
    /// 「Cannot consume scoped service from singleton」。</para>
    ///
    /// <para>而 <see cref="EnterpriseDocNormalizationExecutor"/> 依赖 <c>IDbOrm</c> 与
    /// <c>IObjectStorage</c>，<b>两者都是 Scoped</b> ⇒ 它本身<b>不能</b>直接注册成单例。
    /// 这正是它此前「只有类定义、没有 DI 注册」的根因 —— <c>ent_doc_normalize</c> 任务
    /// <b>永远分发不到，且不报错</b>。</para>
    ///
    /// <para><b>解法</b>（与 <c>OfficeConvertTaskExecutor</c> 同一精神：单例壳 + 每次 CreateScope）：
    /// 本壳只持 <see cref="IServiceProvider"/>（单例安全），每次调用开一个 scope，
    /// 在 scope 内解析 Scoped 的执行器本体。</para>
    ///
    /// <para>⛔ <b>不要</b>改成「把 scoped 服务缓存进字段」—— <c>QueueManager</c> 按
    /// <c>queue_max_concurrent</c>（默认 4）起并发 worker，同一个执行器实例会被<b>并发调用</b>，
    /// 缓存字段会导致 scope 串号。</para>
    /// </summary>
    public class EnterpriseDocNormalizationExecutorAdapter : IYzhTaskExecutor
    {
        private readonly IServiceProvider _serviceProvider;

        public EnterpriseDocNormalizationExecutorAdapter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>★ 取自执行器本体的常量，⛔ 不在此重复硬编码</summary>
        public string TaskType => EnterpriseDocNormalizationExecutor.TaskTypeName;

        public async Task<TaskExecutionResult> ExecuteAsync(YzhQueueTask task, CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var inner = scope.ServiceProvider.GetRequiredService<EnterpriseDocNormalizationExecutor>();
            return await inner.ExecuteAsync(task, cancellationToken);
        }

        public Task OnTaskStateChangedAsync(YzhQueueTask task, string newStatus, string message)
        {
            using var scope = _serviceProvider.CreateScope();
            var inner = scope.ServiceProvider.GetRequiredService<EnterpriseDocNormalizationExecutor>();
            return inner.OnTaskStateChangedAsync(task, newStatus, message);
        }
    }
}
