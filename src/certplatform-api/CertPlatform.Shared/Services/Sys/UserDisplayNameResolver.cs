using YZH.Core.DataBase.Interfaces;

namespace CertPlatform.Shared.Services.Sys;

/// <summary>
/// <b>审计人字段（<c>CreateBy</c> / <c>UpdateBy</c> / <c>DeleteBy</c>）→ 显示姓名</b> 的解析器。
///
/// <para>★ 为什么必须有它：审计人字段按契约存的是 <b>用户 Code</b>（<c>Sys_User.Code</c>），
/// 而页面要显示的是人 —— 直接 <c>prop="CreateBy"</c> 渲染就会把
/// <c>df6e03e93623402bb6cf0e57d1c921d8</c> 当人名显示（用户报障原话：「应该显示姓名」）。
/// 解析规则统一在这里，⛔ 不要再各页面手抄一份。</para>
///
/// <para>★ <b>双键命中</b>：既认 <c>Code</c>（新契约），也认 <c>UserName</c>（历史数据里存过登录名，
/// 见 <c>scripts/db/verify/verify_createby.sql</c>）。两个键<b>都</b>写进返回字典 —— 调用方拿到的
/// 键可能是任意一种，不用自己判断。</para>
///
/// <para>⚠️ <c>CertPlatform.Shared</c> 不引用 <c>YZH.Core.Api</c>（只引 Stand + DataBase）
/// ⇒ 查 <c>Sys_User</c> 走<b>列投影 SQL</b>（同 <see cref="CertMessageService"/>）。</para>
///
/// <para>⚠️ 姓名取值 = <c>UserTrueName</c> 优先、为空退回 <c>UserName</c>（账号名），
/// 与 <c>QueueMonitorController</c> / <c>EnterpriseNormalizeController</c> 的既有口径一致。</para>
/// </summary>
public class UserDisplayNameResolver
{
    private readonly IDbOrm _db;

    public UserDisplayNameResolver(IDbOrm db)
    {
        _db = db;
    }

    /// <summary>
    ///     批量把审计人键（Code 或登录名）解析成显示姓名。
    ///
    ///     <para>返回字典同时含 <c>Code</c> 与 <c>UserName</c> 两个键；查不到的键<b>不出现在字典里</b>
    ///     （调用方用 <c>TryGetValue</c>，⛔ 不要因为查不到就填空串把原有值覆盖掉 —— 那会让
    ///     连「谁做的」都看不出来）。</para>
    /// </summary>
    /// <param name="keys">审计人字段原值集合（可含 null / 空串 / 重复）</param>
    public async Task<Dictionary<string, string>> ResolveManyAsync(IEnumerable<string?> keys)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        var list = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (list.Count == 0) return map;

        // ★ 一次查询搞定，不用逐条查（IN 列表）：Code / UserName 都只含字母数字与连字符/下划线，
        //   **不可能含逗号** ⇒ 用 FIND_IN_SET 把整个批量压成一个参数，避免拼 SQL 字符串。
        //   IsValid 刻意不过滤：停用/禁用的人历史上做过的事仍要显示姓名；只排除软删除。
        var r = await _db.SqlQueryAsync<Brief>(
            @"SELECT Code, UserName, UserTrueName FROM sys_user
               WHERE IsDeleted = 0
                 AND (FIND_IN_SET(Code, @keys) > 0 OR FIND_IN_SET(UserName, @keys) > 0)",
            new { keys = string.Join(',', list) });

        foreach (var u in r.Data ?? [])
        {
            var name = string.IsNullOrWhiteSpace(u.UserTrueName) ? u.UserName : u.UserTrueName!;
            if (string.IsNullOrWhiteSpace(name)) continue;

            if (!string.IsNullOrWhiteSpace(u.Code)) map[u.Code!.Trim()] = name;
            if (!string.IsNullOrWhiteSpace(u.UserName)) map[u.UserName!.Trim()] = name;
        }

        return map;
    }

    /// <summary>单个键解析（查不到返回 null —— ⛔ 不返回空串，避免把原值抹掉）</summary>
    public async Task<string?> ResolveOneAsync(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var map = await ResolveManyAsync([key]);
        return map.TryGetValue(key.Trim(), out var name) ? name : null;
    }

    /// <summary><c>sys_user</c> 列投影（Shared 层不引 <c>YZH.Core.Api</c>）</summary>
    public sealed class Brief
    {
        public string? Code { get; set; }
        public string? UserName { get; set; }
        public string? UserTrueName { get; set; }
    }
}
