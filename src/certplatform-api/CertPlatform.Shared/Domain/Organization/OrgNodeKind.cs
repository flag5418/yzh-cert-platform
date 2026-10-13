namespace CertPlatform.Shared.Domain.Organization;

/// <summary>
/// ★ <b>「机构 → 标准 → 阶段」树的节点类型</b>（<see cref="OrgStageStandardNode"/> 的 <c>Kind</c>）。
///
/// <para><b>为什么用枚举而不是字符串</b>：旧实现用 <c>"organization"</c> / <c>"standard"</c> / <c>"phase"</c>
/// 这类<b>字面量</b>在多个端点上各自拼写（T1 用 <c>organization</c>、T4 用 <c>IsEnterpriseNode</c> 布尔），
/// 拼错一个字母 ⇒ <b>静默不匹配、无编译期保护</b>。枚举把「节点种类」交给类型系统。</para>
///
/// <para>⚠️ 本枚举是<b>内部中间表示</b>用（见 <see cref="OrgStageStandardNode"/>），
/// ⛔ <b>不是对外 DTO 字段</b> —— 各端点仍投影回自己的原响应形状（保证前端零改动）。</para>
/// </summary>
public enum OrgNodeKind
{
    /// <summary>认证机构（<c>cert_certification_body</c>）。</summary>
    Org = 0,

    /// <summary>标准（<c>cert_iso_standard</c>）。</summary>
    Standard = 1,

    /// <summary>认证阶段（<c>cert_cert_stage</c>）。</summary>
    Stage = 2,

    /// <summary>企业（<c>cert_enterprise</c>）—— 企业侧左树的一级节点。</summary>
    Enterprise = 3,

    /// <summary>目录文件夹（<c>cert_standard_directory_folder</c>）。</summary>
    Folder = 4,

    /// <summary>目录文件（<c>cert_standard_directory_file</c>）。</summary>
    File = 5,
}
