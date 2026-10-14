using System;

namespace YZH.Core.Api.UploadPolicy;

/// <summary>
/// ★ 上传文件后缀策略 · 框架扩展点（2026-10-14，按「框架层改造准入」五条落地）。
///
/// <para><b>为什么存在</b>：框架层（FileStorageController / YzhControllerBase.ImportCore）是通用上传入口，
/// 但 YZH.Core.Api <b>不能引用</b> CertPlatform.Shared（依赖方向会倒挂：Shared 是业务共享层）。
/// 故框架只定义本静态钩子（默认放行 = 不破坏既有契约），由启动工程在 Program.cs 注入业务判定。</para>
///
/// <para><b>业务注册（唯一实现）</b>：`YZH.Core.Web/Program.cs` 注入
/// <c>CertPlatform.Shared.Storage.UploadFilePolicy.Check</c> 包装 ——
/// 唯一事实源 = <c>upload-file-policy.json</c>（改后缀只改 JSON + 跑生成脚本）。</para>
///
/// <para><b>语义</b>：返回 null = 放行；返回非空字符串 = 拒绝原因（可直接展示给用户）。
/// 判定委托内部异常时 <b>fail-open 放行</b>（策略自身的 bug 不得阻断全站上传能力，
/// 与「默认放行」契约一致；策略正确性由两端守卫 + 测试保证）。</para>
/// </summary>
public static class UploadPolicyGate
{
    /// <summary>
    /// 判定委托：入参 = 客户端提交的文件名（含扩展名）；返回 null 放行，非 null = 拒绝原因。
    /// 由启动工程注册（见 Program.cs）；未注册 = 框架默认放行。
    /// </summary>
    public static Func<string, string?>? Validator { get; set; }

    /// <summary>判定单个文件名是否被策略拒绝。返回拒绝原因（null = 放行）。</summary>
    public static string? Validate(string? fileName)
    {
        var validator = Validator;
        if (validator == null) return null;
        try
        {
            return validator(fileName ?? "");
        }
        catch
        {
            // fail-open：策略异常不阻断上传（默认放行契约）；策略正确性由守卫与测试兜底
            return null;
        }
    }
}
