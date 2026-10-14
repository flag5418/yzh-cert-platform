namespace YZH.Core.Api.Services;

/// <summary>
/// <see cref="EntityService{T}"/> 写入方式（设计：AI节点参数与审计字段统一 §3.3）。
///
/// <para>动机（用户原话）：「让 baseentity 通过基类实体的 insert/update/delete 方法统一为审计字段进行赋值；
/// service 和 controller 只需要完善必要字段，然后调用基类的 update/delete 等自动完成审计字段的填入。
/// 基类方法可以增加一个参数『是否提交』，默认是提交。」</para>
/// </summary>
public enum WriteMode
{
    /// <summary>
    /// <b>填充审计字段 + 立即执行</b>（默认）。单条写入走它：业务代码补完必要字段直接调基类，
    /// 执行与审计同一句话完成。
    /// </summary>
    Commit = 0,

    /// <summary>
    /// <b>只填充审计字段，⛔ 不执行任何 SQL、不写审计日志</b>（没落库就没有「新增/更新记录」这回事）。
    ///
    /// <para>适用：调用方自己管事务、或自己有更快的写法（如 <c>InsertBatchAsync</c> 真批量），
    /// 只想借基类把 <c>Code</c>/<c>CreateBy</c>/<c>UpdateBy</c> 等按
    /// <c>IUserContext.UserCode</c> 统一填好 —— 这是绕过基类写库却**拿不到审计人**的正解。</para>
    ///
    /// <code>
    /// var fill = await svc.InsertBatch(rows, mode: WriteMode.FillOnly);   // 只填 Code/审计
    /// if (!fill.Success) throw ...;
    /// await _db.InsertBatchAsync(fill.Data);                              // 调用方自己的事务里执行
    /// </code>
    ///
    /// <para>⚠️ 为什么不能在事务里直接调 <c>Commit</c> 模式的 <c>InsertBatch</c>：
    /// 它内部会 <c>BeginTransaction()</c>，嵌在已有事务上会破坏原子性（可能提前 COMMIT）。</para>
    /// </summary>
    FillOnly = 1
}
