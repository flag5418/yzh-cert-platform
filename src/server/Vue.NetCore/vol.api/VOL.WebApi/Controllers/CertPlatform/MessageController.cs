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

        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = UserContext.Current.UserId;
            var count = await _messageService.GetUnreadCountAsync(userId);
            return new JsonResult(new { status = true, data = count });
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool unreadOnly = false)
        {
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
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = UserContext.Current.UserId;
            await _messageService.MarkAllReadAsync(userId);
            return new JsonResult(new { status = true, message = "全部已读" });
        }
    }
}
