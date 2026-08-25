using VOL.Core.Utilities;

namespace YZH.Core.Validation;

/// <summary>
/// 校验结果。只承载校验状态和错误信息，不涉及数据持久化。
/// </summary>
public class EntityValidationResult
{
    public bool IsValid { get; set; } = true;
    public string? ErrorMessage { get; set; }

    public EntityValidationResult OK()
    {
        IsValid = true;
        ErrorMessage = null;
        return this;
    }

    public EntityValidationResult Error(string message)
    {
        IsValid = false;
        ErrorMessage = message;
        return this;
    }

    /// <summary>
    /// 转换为 Vol 的 WebResponseContent，可直接返回给 Controller
    /// </summary>
    public WebResponseContent ToWebResponse()
    {
        if (IsValid)
            return new WebResponseContent().OK();
        return new WebResponseContent().Error(ErrorMessage);
    }
}
