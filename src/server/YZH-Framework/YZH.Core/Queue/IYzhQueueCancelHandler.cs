using System.Threading.Tasks;

namespace YZH.Core.Queue
{
    /// <summary>
    /// 队列取消后的业务清理钩子（可选注册，可注册多个实现）
    /// <para>业务侧实现：取消文件转换/上传队列时，彻底清理本次上传产生的数据（数据库记录 + MinIO 对象）</para>
    /// <para>在 YzhQueueManager.CancelQueueAsync 将队列置为终态后调用，异常不影响取消本身</para>
    /// </summary>
    public interface IYzhQueueCancelHandler
    {
        /// <summary>
        /// 队列取消后的业务清理回调
        /// </summary>
        Task OnQueueCancelledAsync(YzhQueue queue);
    }
}
