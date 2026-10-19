using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Routing;
using YZH.Core.Api.Attributes;

namespace YZH.Core.Api.Services;

/// <summary>
///     接口名称 / 描述解析器（反射）
///
///     <para>接口名称唯一来源：方法上的 <see cref="ApiDescriptionAttribute"/>.Description
///     → 框架基类端点中文名（config/filter/…）→ 方法名。</para>
///     <para>接口描述（业务分组）：<see cref="ApiDescriptionAttribute"/>.Group（无则空串）。</para>
///
///     <para>★ 单一来源：<c>ApiScanner</c>（接口同步落库 sys_api）与 <c>YzhAuditingFilter</c>
///     （操作日志落库 sys_log）共用本类，避免两处各写一份中文名映射。</para>
/// </summary>
public static class ApiDescriptionResolver
{
    /// <summary>解析接口名称：[ApiDescription] → 框架端点中文名 → 方法名</summary>
    public static string ResolveName(MethodInfo method)
    {
        var attr = method.GetCustomAttribute<ApiDescriptionAttribute>(true);
        if (attr != null && !string.IsNullOrWhiteSpace(attr.Description))
            return attr.Description;

        var template = ResolveRouteTemplate(method);
        return BaseEndpointNames.TryGetValue(template, out var name) ? name : method.Name;
    }

    /// <summary>
    ///     解析.Http 动作的 `[HttpGet("xxx")]` 模板：重写方法上未重复声明时须回退到基类（`GetBaseDefinition`）。
    ///     与 <c>ApiScanner.GetHttpMethodAttribute</c> 同源，保证「接口同步」与「操作日志」名称一致。
    /// </summary>
    public static string ResolveRouteTemplate(MethodInfo method)
    {
        var attr = method.GetCustomAttributes(false).OfType<HttpMethodAttribute>().FirstOrDefault();
        if (attr == null)
        {
            var baseDefinition = method.GetBaseDefinition();
            if (baseDefinition != method)
                attr = baseDefinition.GetCustomAttributes(false).OfType<HttpMethodAttribute>().FirstOrDefault();
        }

        var template = attr?.Template;
        return string.IsNullOrWhiteSpace(template) ? method.Name : template.Trim();
    }

    /// <summary>解析接口描述（业务分组）；无 [ApiDescription] → 空串</summary>
    public static string ResolveGroup(MethodInfo method)
        => method.GetCustomAttribute<ApiDescriptionAttribute>(true)?.Group ?? "";

    /// <summary>框架基类端点（无 [ApiDescription]）的中文名，避免权限树 / 操作日志里出现空白或纯英文名称</summary>
    public static readonly IReadOnlyDictionary<string, string> BaseEndpointNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["config"] = "获取接口配置",
            ["filter"] = "分页查询",
            ["add"] = "新增",
            ["update"] = "修改",
            ["delete"] = "批量删除",
            ["export"] = "导出",
            ["import"] = "导入",
            ["import/template"] = "下载导入模板",
            ["action/{methodName}"] = "执行行操作",
            ["toggle-valid"] = "启用/禁用",
            ["tree/root"] = "树根节点",
            ["tree/children"] = "树子节点",
            ["tree/add"] = "新增树节点",
            ["tree/update"] = "修改树节点",
            ["tree/delete"] = "删除树节点",
            ["tree/action/{methodName}"] = "执行树节点操作",
            ["tree/toggle-valid"] = "树节点启用/禁用",
            ["treepconfig"] = "树表配置",
            ["checkTree"] = "关联树数据",
            ["check/add"] = "新增关联",
            ["check/remove"] = "移除关联",
            ["check/all"] = "全部关联",
        };
}
