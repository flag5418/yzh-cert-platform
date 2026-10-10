using CertPlatform.Shared.Entities.Sys;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Shared.Services.Sys;

/// <summary>
/// 站内消息（cert_message）服务。
///
/// <para>★ 职责：落库（CreateForLoginAsync）+ 读取（GetUnreadCount/GetList/MarkRead/MarkAllRead）。
/// 实时通道由框架能力（YZH.Core.Stand 的 IYzhMessagePusher）承担，本类不碰推送。</para>
///
/// <para>★ 收件人定位：按**登录名**查 Sys_User 换业务编码 Code 落 UserCode 列 ——
/// yzh_queue.CreateBy 存的就是创建者登录名（QueueManager: CreateBy = req.UserName），
/// 队列通知链路只能拿到它。找不到用户 = 跳过。</para>
///
/// <para>⚠️ Shared 不引用 YZH.Core.Api（只引 Stand + DataBase），查 Sys_User 用列投影 SQL。</para>
/// </summary>
public class CertMessageService
{
    private readonly IDbOrm _db;

    public CertMessageService(IDbOrm db)
    {
        _db = db;
    }

    /// <summary>
    /// 按收件人登录名落一条站内消息。
    /// </summary>
    /// <param name="loginName">收件人登录名（Sys_User.UserName，唯一）</param>
    /// <param name="title">标题</param>
    /// <param name="content">正文</param>
    /// <param name="messageType">消息类型（≤20 字符；队列类用 "queue"）</param>
    /// <param name="extraData">附加 JSON</param>
    /// <returns>true=已落库；false=收件人不存在/登录名为空（静默跳过）</returns>
    public async Task<bool> CreateForLoginAsync(
        string? loginName, string title, string content, string messageType, string extraData)
    {
        if (string.IsNullOrWhiteSpace(loginName)) return false;

        var users = await _db.Client.Ado.SqlQueryAsync<UserBrief>(
            "SELECT Code, UserName FROM Sys_User WHERE UserName = @name AND IsDeleted = 0 LIMIT 1",
            new { name = loginName });
        var user = users.FirstOrDefault();
        if (user == null || string.IsNullOrEmpty(user.Code)) return false;

        await _db.InsertAsync(new CertMessage
        {
            Code = Guid.NewGuid().ToString("N"),
            UserCode = user.Code,
            UserName = user.UserName,
            Title = title,
            Content = content,
            MessageType = messageType,
            IsRead = 0,
            ExtraData = extraData ?? string.Empty,
            CreateBy = user.Code,
            CreateTime = DateTime.UtcNow,
            IsValid = 1,
        });
        return true;
    }

    /// <summary>查询某用户的未读消息数（按 UserCode）</summary>
    public async Task<int> GetUnreadCountByUserCodeAsync(string userCode)
    {
        var r = await _db.SqlScalarAsync<int>(
            "SELECT COUNT(*) FROM cert_message WHERE UserCode = @code AND IsRead = 0 AND IsDeleted = 0",
            new { code = userCode });
        return r.Data;
    }

    /// <summary>
    /// 分页查询某用户的消息列表（按 UserCode）。
    /// </summary>
    /// <param name="userCode">业务编码（Sys_User.Code）</param>
    /// <param name="page">页码（1-based）</param>
    /// <param name="pageSize">每页条数</param>
    /// <param name="unreadOnly">true=仅未读；false=全部</param>
    public async Task<List<MessageItem>> GetListByUserCodeAsync(
        string userCode, int page, int pageSize, bool unreadOnly)
    {
        var sql = @"
            SELECT Id, Code, Title, Content, MessageType, IsRead, ExtraData, CreateTime, ReadDate
            FROM cert_message
            WHERE UserCode = @code AND IsDeleted = 0
            " + (unreadOnly ? "AND IsRead = 0 " : "") + @"
            ORDER BY CreateTime DESC
            LIMIT @limit OFFSET @offset";
        var r = await _db.SqlQueryAsync<MessageItem>(sql, new
        {
            code = userCode,
            limit = pageSize,
            offset = (page - 1) * pageSize,
        });
        return r.Data ?? [];
    }

    /// <summary>将单条消息标记为已读（按 Code 业务键，非自增 Id）</summary>
    public async Task<bool> MarkReadAsync(string code)
    {
        var r = await _db.SqlExecuteAsync(@"
            UPDATE cert_message
            SET IsRead = 1, ReadDate = NOW()
            WHERE Code = @code AND IsRead = 0 AND IsDeleted = 0
            LIMIT 1", new { code });
        return r.Data > 0;
    }

    /// <summary>
    /// 批量标记某用户的消息为已读。
    /// </summary>
    /// <param name="userCode">业务编码</param>
    /// <param name="messageType">按类型过滤（null=全部类型）</param>
    public async Task<int> MarkAllReadAsync(string userCode, string? messageType)
    {
        var sql = "UPDATE cert_message SET IsRead = 1, ReadDate = NOW() WHERE UserCode = @code AND IsRead = 0 AND IsDeleted = 0";
        object param = new { code = userCode };
        if (!string.IsNullOrWhiteSpace(messageType))
        {
            sql += " AND MessageType = @type";
            param = new { code = userCode, type = messageType };
        }
        var r = await _db.SqlExecuteAsync(sql, param);
        return r.Data;
    }

    /// <summary>按登录名查 Sys_User 的列投影</summary>
    public sealed class UserBrief
    {
        public string Code { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }

    /// <summary>消息列表项（GET /api/Admin/Message/list 返回）</summary>
    public sealed class MessageItem
    {
        public long Id { get; set; }
        public string Code { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string MessageType { get; set; } = "system";
        public int IsRead { get; set; }
        public string? ExtraData { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime? ReadDate { get; set; }
    }
}
