using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace VOL.Core.SignalR
{
    /// <summary>
    /// 上传进度 SignalR Hub
    /// 按 taskId 分组建组，前端订阅对应 task 的实时进度
    /// </summary>
    public class UploadProgressHub : Hub
    {
        public async Task BroadcastUploadProgress(string taskId, object progress)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"upload_{taskId}");
            await Clients.Group($"upload_{taskId}").SendAsync("ReceiveUploadProgress", progress);
        }

        public async Task UnsubscribeFromUpload(string taskId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"upload_{taskId}");
        }
    }
}
