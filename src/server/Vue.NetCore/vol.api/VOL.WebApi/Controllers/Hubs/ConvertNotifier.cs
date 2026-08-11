using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using VOL.Builder.IServices.CertPlatform;
using VOL.WebApi.Controllers.Hubs;

namespace VOL.WebApi.Hubs
{
    /// <summary>
    /// 转换进度通知实现（桥接 ConvertQueueManager 与 HomePageMessageHub）
    /// </summary>
    public class ConvertNotifier : IConvertNotifier
    {
        private readonly IHubContext<HomePageMessageHub> _hubContext;

        public ConvertNotifier(IHubContext<HomePageMessageHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendToConvertGroup(string taskId, object message)
        {
            if (!string.IsNullOrEmpty(taskId))
            {
                await _hubContext.Clients
                    .Group($"convert_{taskId}")
                    .SendAsync("ReceiveHomePageMessage", message);
            }
        }

        public async Task SendToUser(string userName, object message)
        {
            if (!string.IsNullOrEmpty(userName))
            {
                // HomePageMessageHub 用 ConcurrentDictionary 按 userName 存连接
                // 但 IHubContext 无法直接访问，改用组方式
                await _hubContext.Clients
                    .Group($"user_{userName}")
                    .SendAsync("ReceiveHomePageMessage", message);
            }
        }
    }
}
