using System.Collections.Generic;
using System.Threading.Tasks;
using YZH.Core.Extensions.AutofacManager;

namespace Cert.Platform.IServices.Admin.Platform
{
    public interface IMessageService : IDependency
    {
        Task<int> GetUnreadCountAsync(int userId);
        Task<List<MessageDto>> GetListAsync(int userId, int page, int pageSize, bool unreadOnly);
        Task MarkReadAsync(long messageId);
        Task MarkAllReadAsync(int userId, string messageType = null);
        Task CreateAsync(int userId, string userName, string title, string content, string messageType, string extraData);
    }

    public class MessageDto
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string MessageType { get; set; }
        public int IsRead { get; set; }
        public string ExtraData { get; set; }
        public System.DateTime CreateDate { get; set; }
        public System.DateTime? ReadDate { get; set; }
    }
}
