using System.Threading.Tasks;

namespace YZH.Core.Queue
{
    /// <summary>
    /// 队列终态通知抽象
    /// <para>业务侧实现：消息落库（cert_message）+ SignalR 实时推送等</para>
    /// </summary>
    public interface IYzhQueueNotifier
    {
        /// <summary>队列进入终态（completed/failed/cancelled）时调用</summary>
        Task NotifyAsync(YzhQueue queue);
    }
}
