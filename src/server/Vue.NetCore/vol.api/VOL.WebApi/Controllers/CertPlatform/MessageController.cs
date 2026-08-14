using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.Extensions;
using VOL.Core.Infrastructure;
using VOL.Core.ManageUser;

namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/message")]
    [Authorize]
    public class MessageController : ControllerBase
    {
        private readonly IMessageService _messageService;

        public MessageController(IMessageService messageService)
        {
            _messageService = messageService;
        }

        [HttpPost("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = UserContext.Current.UserId;
            var count = await _messageService.GetUnreadCountAsync(userId);
            return new JsonResult(new { status = true, data = count });
        }

[HttpPost("list")]
public async Task<IActionResult> GetList([FromBody] dynamic param)
{
    int page = (int)(param?.page ?? 1);
    int pageSize = (int)(param?.pageSize ?? 20);
    bool unreadOnly = (bool)(param?.unreadOnly ?? false);

    var userId = UserContext.Current.UserId;
    var list = await _messageService.GetListAsync(userId, page, pageSize, unreadOnly);
    return new JsonResult(new { status = true, data = list });
}

        [HttpPost("read/{id}")]
        public async Task<IActionResult> MarkRead(long id)
        {
            await _messageService.MarkReadAsync(id);
            return new JsonResult(new { status = true, message = "已标记已读" });
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead([FromBody] dynamic param)
        {
            var userId = UserContext.Current.UserId;
            string messageType = param?.type;
            await _messageService.MarkAllReadAsync(userId, messageType);
            return new JsonResult(new { status = true, message = "全部已读" });
        }
    }
}
