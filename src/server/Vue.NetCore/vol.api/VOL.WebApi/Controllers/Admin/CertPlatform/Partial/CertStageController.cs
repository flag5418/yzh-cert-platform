/*
 * 接口编写处...
 * 如果接口需要做 Action 的权限验证，请在 Action 上使用属性
 * 如: [ApiActionPermission("CertStage", Enums.ActionPermissionOptions.Delete)]
 *
 * 认证阶段管理 Controller
 * 全局基础资料，使用 Vol 框架标准 CRUD
 */
namespace VOL.WebApi.Controllers.CertPlatform
{
    public partial class CertStageController
    {
        // CertStage 的 CRUD 操作由框架自动生成
        // 此处可添加自定义业务方法
    }
}
