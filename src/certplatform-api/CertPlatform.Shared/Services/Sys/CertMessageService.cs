using CertPlatform.Shared.Entities.Sys;
using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Shared.Services.Sys;

/// <summary>
/// 站内消息（cert_message）服务。
///
/// <para>★ 职责：落库（CreateForLoginAsync）+ 读取（GetUnreadCount/GetList/MarkRead/MarkAllRead）。
/// 实时通道由框架能力（YZH.Core.Stand 的 IYzhMessagePusher）承担，本类不碰推送。</para>
///
/// <para>★ 收件人定位（2026-10-11 统一）：<b>主路径按用户 Code</b>（<see cref="CreateForUserAsync"/>）——
/// 与 <c>yzh_queue.CreateBy</c> / 推送分组键 / JWT <c>code</c> claim <b>同键</b>，不需要任何映射。
/// 另保留按<b>登录名</b>的入口（<see cref="CreateForLoginAsync"/>）专供「管理端按登录名强制下线」这一种场景
/// （人输入的只能是登录名）。找不到用户 = 跳过。</para>
///
/// <para>⚠️ 历史坑：本类曾经<strong>只能</strong>按登录名查，而队列那条链路后来按契约改存 Code ⇒
/// 两种键混用会「查不到用户 → 消息静默不落库」。新增调用一律优先用
/// <see cref="CreateForUserAsync"/>，⛔ 不要传 Code 进 <see cref="CreateForLoginAsync"/>。</para>
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
    ///     ★ <b>按收件人用户 Code 落一条站内消息</b>（推荐入口）。
    ///
    ///     <para>与 <c>yzh_queue.CreateBy</c>、推送分组键、JWT <c>code</c> claim 同键 ——
    ///     调用方无需做任何 Code/登录名 映射。</para>
    /// </summary>
    /// <param name="userCode">收件人业务编码（<c>Sys_User.Code</c>）</param>
    /// <param name="title">标题</param>
    /// <param name="content">正文</param>
    /// <param name="messageType">消息类型（≤20 字符；队列类用 "queue"）</param>
    /// <param name="extraData">附加 JSON</param>
    /// <returns>true=已落库；false=用户不存在/Code 为空（静默跳过）</returns>
    public async Task<bool> CreateForUserAsync(
        string? userCode, string title, string content, string messageType, string extraData)
    {
        if (string.IsNullOrWhiteSpace(userCode)) return false;

        var users = await _db.Client.Ado.SqlQueryAsync<UserBrief>(
            "SELECT Code, UserName FROM Sys_User WHERE Code = @code AND IsDeleted = 0 LIMIT 1",
            new { code = userCode.Trim() });

        return await InsertAsync(users.FirstOrDefault(), title, content, messageType, extraData);
    }

    /// <summary>
    ///     按收件人<b>登录名</b>落一条站内消息。
    ///
    ///     <para>⚠️ 仅供「管理端按登录名强制下线」这一种场景 —— 人在界面上输入的只能是登录名。
    ///     ⛔ 其它调用一律用 <see cref="CreateForUserAsync"/>（传 Code）：把 Code 传进来会查不到用户，
    ///     消息<b>静默不落库</b>。</para>
    /// </summary>
    /// <param name="loginName">收件人登录名（<c>Sys_User.UserName</c>，唯一）</param>
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
            new { name = loginName.Trim() });

        return await InsertAsync(users.FirstOrDefault(), title, content, messageType, extraData);
    }

    /// <summary>
    ///     登录名 → 用户 Code（查不到返回 null）。
    ///
    ///     <para>用途：管理端「强制下线」按登录名定位人，而落库与<b>推送分组都按 Code</b>
    ///     ⇒ 调一次转换，⛔ 不要把登录名直接传给推送（那会进一个没人在的组且不报错）。</para>
    /// </summary>
    public async Task<string?> ResolveUserCodeByLoginNameAsync(string? loginName)
    {
        if (string.IsNullOrWhiteSpace(loginName)) return null;

        var users = await _db.Client.Ado.SqlQueryAsync<UserBrief>(
            "SELECT Code, UserName FROM Sys_User WHERE UserName = @name AND IsDeleted = 0 LIMIT 1",
            new { name = loginName.Trim() });

        var code = users.FirstOrDefault()?.Code;
        return string.IsNullOrWhiteSpace(code) ? null : code;
    }

    /// <summary>落库公共段（两个入口共用：收件人已解析为 Code + UserName）</summary>
    private async Task<bool> InsertAsync(
        UserBrief? user, string title, string content, string messageType, string extraData)
    {
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
            // ★ 2026-10-14：UtcNow → Now —— MessageBell 前端 new Date() 按本地解析（JSON 无 Z），
            //   写 UTC 会让「刚刚/N分钟前」与消息时间整体错 8 小时。
            CreateTime = DateTime.Now,
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
