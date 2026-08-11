using System.Threading.Tasks;
using VOL.Core.Extensions.AutofacManager;

namespace VOL.Builder.IServices.CertPlatform
{
    /// <summary>
    /// 转换进度通知接口（解耦 ConvertQueueManager 与 SignalR Hub）
    /// 实现在 VOL.WebApi，通过 DI 注入
    /// </summary>
    public interface IConvertNotifier : IDependency
    {
        /// <summary>
        /// 推送到转换任务组
        /// </summary>
        Task SendToConvertGroup(string taskId, object message);

        /// <summary>
        /// 推送给指定用户
        /// </summary>
        Task SendToUser(string userName, object message);
    }
}
