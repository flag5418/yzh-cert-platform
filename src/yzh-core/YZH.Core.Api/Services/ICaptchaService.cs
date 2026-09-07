namespace YZH.Core.Api.Services;

/// <summary>
///     验证码服务接口
/// </summary>
public interface ICaptchaService
{
    /// <summary>
    ///     生成验证码
    ///     <param name="code">输出的验证码文本</param>
    ///     <param name="uuid">输出的唯一标识</param>
    ///     <returns>Base64 格式的图片</returns>
    /// </summary>
    string Generate(out string code, out string uuid);

    /// <summary>
    ///     验证验证码（验证后自动清除）
    /// </summary>
    /// <param name="uuid">唯一标识</param>
    /// <param name="code">用户输入的验证码</param>
    /// <returns>是否验证通过</returns>
    bool Verify(string uuid, string code);
}
