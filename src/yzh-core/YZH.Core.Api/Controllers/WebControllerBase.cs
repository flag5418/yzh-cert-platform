using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Attributes;

namespace YZH.Core.Api.Controllers;

/// <summary>
///     YZH Web 端所有 Controller 的基类（默认强认证）
///     
///     特性：
///     - 类级别标记 [YZHAuthorize]，继承此类的所有 Controller 默认要求 JWT 认证
///     - 子类无需再标记任何认证特性，天然受保护
///     - 需要临时测试的接口，在方法上标记 [YZHAnonymous] 即可
///     
///     继承链：
///     WebControllerBase → ControllerBase(.NET) + [YZHAuthorize]
///     YzhControllerBase&lt;V&gt; → ControllerBase(.NET) + EntityConfig + 业务方法
///     TreeTableControllerBase → YzhControllerBase
///     
///     使用建议：
///     - 业务 Web 控制器（无 EntityConfig 需求）→ 继承 WebControllerBase
///     - 标准 CRUD 控制器 → 继承 YzhControllerBase（如需要强认证，可在子类重写）
/// </summary>
[ApiController]
[Route("api/[controller]")]
[YZHAuthorize]
public abstract class WebControllerBase : ControllerBase
{
}
