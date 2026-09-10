namespace YZH.Core.Stand.BizConventions;

/// <summary>
///     业务实体命名约束（架构层强制规范）
///
///     目的：避免 YZH.Core 内置基础实体与 certplatform-api 业务实体命名冲突
///
///     背景：
///     - YZH.Core 内部已包含 Sys_User / Sys_Organization / Sys_Role / Sys_Menu / Sys_Dictionary
///       （位于 YZH.Core.Api/Models/Users/、YZH.Core.Api/Models/Organization/）
///     - certplatform-api 业务层（如 CertPlatform.Shared/Entities/）如果继续用 Sys_* 命名，
///       必须 extern alias 隔离，极易出错
///     - 通过本约束，业务层在启动期即可发现命名违规，提前修复
///
///     规范（V4 强制）：
///     1. 业务实体类名不得与 YZH.Core 内置实体同名（见 BannedEntityNames）
///     2. 业务实体命名空间建议为 CertPlatform.{Admin|Auditor|Enterprise}.Domain.{Dir|Cert|...}.{Entity}
///        （不强制，但强烈建议，便于代码组织和类型识别）
///     3. 业务实体属性名 = 数据库列名 = PascalCase JSON 字段名（YZH 架构铁律）
///
///     使用方式（在 YzhWebBuilder 启动时调用）：
///     BizNamingRules.ValidateEntityType(typeof(YourBizEntity));
///     BizNamingRules.ValidateNamespace(typeof(YourBizEntity).Namespace);
/// </summary>
public static class BizNamingRules
{
    /// <summary>
    ///     YZH.Core 内置实体类名黑名单（业务层禁止复用）
    ///     同步来源：YZH.Core.Api/Models/Users/Sys_User.cs、Organization/Sys_Organization.cs 等
    /// </summary>
    public static readonly IReadOnlySet<string> BannedEntityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // 用户域
        "Sys_User",
        "Sys_Role",
        "Sys_UserRole",
        "Sys_Menu",
        "Sys_RoleMenu",
        // 组织机构域
        "Sys_Organization",
        // 字典域
        "Sys_Dictionary",
        "Sys_DictionaryList",
        // 系统日志/审计
        "Sys_Log",
        "Sys_AuditLog",
        // 业务复用可能被占用的类名
        "BaseEntity",
        "TreeNodeViewBase",
        "ITreeNode"
    };

    /// <summary>
    ///     推荐的业务命名空间前缀
    ///     不强制（保留兼容旧项目），但建议 CertPlatform.{端}.Domain.{域}.{Entity}
    /// </summary>
    public static readonly string[] RecommendedNamespacePrefixes =
    {
        "CertPlatform.Admin.Domain.",
        "CertPlatform.Auditor.Domain.",
        "CertPlatform.Enterprise.Domain."
    };

    /// <summary>
    ///     验证业务实体类型（启动期调用，发现违规立即抛异常）
    /// </summary>
    /// <param name="entityType">业务实体类型</param>
    /// <exception cref="InvalidOperationException">类名/命名空间违反规范时抛出</exception>
    public static void ValidateEntityType(Type entityType)
    {
        if (entityType == null) throw new ArgumentNullException(nameof(entityType));

        // 校验 1：类名不在黑名单
        if (BannedEntityNames.Contains(entityType.Name))
        {
            throw new InvalidOperationException(
                $"[YZH] 业务实体 {entityType.FullName} 的类名与 YZH.Core 内置实体冲突。" +
                $"禁止类名：{entityType.Name}。" +
                $"建议重命名为：{SuggestAlternativeName(entityType.Name)} 或 Cert_{entityType.Name}");
        }

        // 校验 2：命名空间前缀建议（仅警告，不抛异常）
        ValidateNamespace(entityType.Namespace);
    }

    /// <summary>
    ///     验证业务命名空间（仅警告，不抛异常）
    /// </summary>
    public static void ValidateNamespace(string? ns)
    {
        if (string.IsNullOrEmpty(ns)) return;

        var isRecommended = RecommendedNamespacePrefixes.Any(p => ns!.StartsWith(p, StringComparison.Ordinal));
        if (!isRecommended)
        {
            // 不抛异常，仅 warning（保留兼容旧项目）
            Console.WriteLine(
                $"[YZH.WARN] 业务命名空间 [{ns}] 不符合推荐格式。" +
                $"建议：CertPlatform.{{Admin|Auditor|Enterprise}}.Domain.{{Dir|Cert|...}}.{{Entity}}");
        }
    }

    /// <summary>
    ///     建议替代命名（把 Sys_User 改成 Cert_User 等）
    /// </summary>
    private static string SuggestAlternativeName(string bannedName)
    {
        if (bannedName.StartsWith("Sys_", StringComparison.OrdinalIgnoreCase))
            return "Cert_" + bannedName.Substring(4);
        if (bannedName.StartsWith("Base", StringComparison.OrdinalIgnoreCase))
            return "Biz" + bannedName;
        return "Biz" + bannedName;
    }

    /// <summary>
    ///     批量校验程序集内所有业务实体（YZH.WebBuilder 启动时调用）
    ///     ```
    ///     // YzhWebBuilder.UseYzhCore 之后
    ///     var bizTypes = AppDomain.CurrentDomain.GetAssemblies()
    ///         .SelectMany(a => a.GetTypes())
    ///         .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.StartsWith("CertPlatform.") == true);
    ///     foreach (var t in bizTypes) BizNamingRules.ValidateEntityType(t);
    ///     ```
    /// </summary>
    public static int ValidateAssembly(params System.Reflection.Assembly[] assemblies)
    {
        int count = 0;
        foreach (var assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).Cast<Type>().ToArray(); }

            foreach (var t in types)
            {
                if (t == null) continue;
                if (!t.IsClass || t.IsAbstract) continue;
                if (t.Namespace?.StartsWith("CertPlatform.", StringComparison.Ordinal) != true) continue;

                ValidateEntityType(t);
                count++;
            }
        }
        return count;
    }
}
