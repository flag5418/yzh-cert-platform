using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VOL.Builder.IServices.CertPlatform;
using VOL.Core.EFDbContext;
using VOL.Entity.CertPlatform.Sys;

namespace VOL.Builder.Services.CertPlatform
{
    /// <summary>
    /// 站内消息服务
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly VOLContext _db;

        public MessageService(VOLContext db)
        {
            _db = db;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _db.Set<CertMessage>()
                .CountAsync(m => m.UserId == userId && m.IsRead == 0);
        }

        public async Task<List<MessageDto>> GetListAsync(int userId, int page, int pageSize, bool unreadOnly)
        {
            var query = _db.Set<CertMessage>()
                .AsNoTracking()
                .Where(m => m.UserId == userId);

            if (unreadOnly)
                query = query.Where(m => m.IsRead == 0);

            var messages = await query
                .OrderByDescending(m => m.CreateDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    Title = m.Title,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    IsRead = m.IsRead,
                    ExtraData = m.ExtraData,
                    CreateDate = m.CreateDate,
                    ReadDate = m.ReadDate
                })
                .ToListAsync();

            return messages;
        }

        public async Task MarkReadAsync(long messageId)
        {
            var msg = await _db.Set<CertMessage>().FirstOrDefaultAsync(m => m.Id == messageId);
            if (msg != null && msg.IsRead == 0)
            {
                msg.IsRead = 1;
                msg.ReadDate = DateTime.Now;
                await _db.SaveChangesAsync();
            }
        }

        public async Task MarkAllReadAsync(int userId, string messageType = null)
        {
            // 使用 ExecuteUpdate 批量更新，避免逐条跟踪的性能问题和潜在错误
            // 这是 EF Core 7+ 的高效批量更新方式
            var query = _db.Set<CertMessage>()
                .Where(m => m.UserId == userId && m.IsRead == 0);

            // 可选按类型过滤：进入系统汇总弹窗展示队列通知后，只标记 queue 类型已读
            if (!string.IsNullOrEmpty(messageType))
                query = query.Where(m => m.MessageType == messageType);

            var updated = await query.ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.IsRead, 1)
                .SetProperty(m => m.ReadDate, DateTime.Now));
            
            // 如果 ExecuteUpdate 不可用（EF Core 版本低于 7），回退到传统方式
            // if (updated == 0) { /* fallback logic */ }
        }

        public async Task CreateAsync(int userId, string userName, string title, string content, string messageType, string extraData)
        {
            var msg = new CertMessage
            {
                UserId = userId,
                UserName = userName,
                Title = title,
                Content = content,
                MessageType = messageType ?? "system",
                IsRead = 0,
                ExtraData = extraData,
                CreateDate = DateTime.Now
            };

            _db.Set<CertMessage>().Add(msg);
            await _db.SaveChangesAsync();
        }
    }
}
