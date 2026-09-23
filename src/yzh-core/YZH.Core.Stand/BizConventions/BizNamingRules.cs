using System.Reflection;
using YZH.Core.Stand.Models.Entity;

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
    ///
    ///     ⚠️ 本清单为**手工兜底**，已于 2026-09-23 按框架层实际类型校准：
    ///     - 修正：<c>Sys_UserRole</c> → <c>Sys_RoleUser</c>（框架实体名是 Sys_RoleUser，原条目不存在）
    ///     - 补充：<c>Sys_Department</c>（YZH.Core.Api/Models/Organization）、<c>ITreeEntity</c>（TreeTableControllerBase 泛型约束）
    ///     - 移除幽灵条目：<c>Sys_Log</c> / <c>Sys_AuditLog</c> / <c>TreeNodeViewBase</c>（框架层均不存在）
    ///
    ///     优先使用 <see cref="DeriveFromFramework"/> 从程序集动态推导，本静态清单作为补充。
    /// </summary>
    public static readonly IReadOnlySet<string> BannedEntityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // 用户域
        "Sys_User",
        "Sys_Role",
        "Sys_RoleUser",
        "Sys_Menu",
        "Sys_RoleMenu",
        // 组织机构域
        "Sys_Organization",
        "Sys_Department",
        // 字典域
        "Sys_Dictionary",
        "Sys_DictionaryList",
        // 基类与树接口
        "BaseEntity",
        "TreeNodeViewBase",
        "ITreeNode",
        "ITreeEntity"
    };

    /// <summary>
    ///     框架层实体所在的命名空间前缀（用于动态推导黑名单）
    /// </summary>
    private static readonly string[] FrameworkEntityNamespacePrefixes =
    {
        "YZH.Core.Api.Models",
        "YZH.Core.Stand.Models"
    };

    /// <summary>
    ///     从框架程序集**动态推导**黑名单（防手工清单漂移）
    ///
    ///     ⚠️ 只收**实体**（继承 <see cref="BaseEntity"/> 或带 <c>[SugarTable]</c>），不收 DTO/信封/配置类。
    ///     原因：真正的风险是「业务实体与框架实体映射同一张表」，而 DTO 同名只是命名困扰，
    ///     不应阻断启动（例：框架 <c>ExportRequest</c> 与业务 <c>ExportRequest</c> 均为 DTO，可共存）。
    ///
    ///     框架层新增实体时，业务层自动被禁止复用同名类，无需再改本文件。
    /// </summary>
    public static IReadOnlySet<string> DeriveFromFramework(params Assembly[] frameworkAssemblies)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asm in frameworkAssemblies)
        {
            foreach (var t in SafeGetTypes(asm))
            {
                if (t == null || !t.IsPublic) continue;
                var ns = t.Namespace ?? string.Empty;
                if (!FrameworkEntityNamespacePrefixes.Any(p => ns.StartsWith(p, StringComparison.Ordinal)))
                    continue;
                if (!IsEntityType(t)) continue;

                names.Add(t.Name);
            }
        }
        return names;
    }

    /// <summary>
    ///     判定是否为实体类型：继承 <see cref="BaseEntity"/> 或带 <c>[SugarTable]</c>
    /// </summary>
    private static bool IsEntityType(Type t)
    {
        if (typeof(BaseEntity).IsAssignableFrom(t)) return true;
        return t.GetCustomAttributes(inherit: true)
                .Any(a => a.GetType().Name == "SugarTable");
    }

    /// <summary>
    ///     安全取类型（容忍部分类型加载失败）
    /// </summary>
    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null).Cast<Type>(); }
    }

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
    /// <param name="extraBanned">额外黑名单（通常来自 <see cref="DeriveFromFramework"/>，可空）</param>
    /// <exception cref="InvalidOperationException">类名/命名空间违反规范时抛出</exception>
    public static void ValidateEntityType(Type entityType, IReadOnlySet<string>? extraBanned = null)
    {
        if (entityType == null) throw new ArgumentNullException(nameof(entityType));

        // 校验 1：类名不在黑名单（静态清单 + 框架动态推导清单）
        if (BannedEntityNames.Contains(entityType.Name) ||
            (extraBanned != null && extraBanned.Contains(entityType.Name)))
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
    ///     已告警过的命名空间（累积，末尾汇总输出，避免启动日志被同类警告刷屏）
    /// </summary>
    private static readonly HashSet<string> WarnedNamespaces = new(StringComparer.Ordinal);

    /// <summary>
    ///     验证业务命名空间（**仅累积，不直接输出**；由 <see cref="FlushNamespaceWarnings"/> 统一汇总）
    ///
    ///     说明：推荐前缀 <c>CertPlatform.{Admin|Auditor|Enterprise}.Domain.*</c> 属**目标态**，
    ///     当前绝大多数业务命名空间尚未迁移，逐条输出会淹没启动日志中的真实错误。
    ///     故做两件事：① 末尾**一行汇总** ② 标记为 <c>[YZH.INFO]</c> 而非 <c>WARN</c>。
    ///
    ///     ⚠️ 为何不用 WARN：本项基线为 0/N（全部未迁移），且每次启动都必然出现。
    ///     按项目「基线必须为 0 才能启用告警」的铁律，它属**技术债盘点**而非告警；
    ///     若标 WARN，开发者 grep 日志排查真实错误时会恒被命中 → 造成误判。
    /// </summary>
    public static void ValidateNamespace(string? ns)
    {
        if (string.IsNullOrEmpty(ns)) return;

        var isRecommended = RecommendedNamespacePrefixes.Any(p => ns!.StartsWith(p, StringComparison.Ordinal));
        if (!isRecommended) WarnedNamespaces.Add(ns!);
    }

    /// <summary>
    ///     输出命名空间告警汇总（每个入口方法末尾调用一次）
    /// </summary>
    private static void FlushNamespaceWarnings()
    {
        if (WarnedNamespaces.Count == 0) return;

        Console.WriteLine(
            $"[YZH.INFO] {WarnedNamespaces.Count} 个业务命名空间尚未迁移到推荐格式 " +
            $"（目标态：CertPlatform.{{Admin|Auditor|Enterprise}}.Domain.{{Dir|Cert|...}}.{{Entity}}）。" +
            $"属既有技术债、非错误、不阻断启动；详见 12-框架能力清单-V1.md §4.2");
        WarnedNamespaces.Clear();
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
            foreach (var t in SafeGetTypes(assembly))
            {
                if (t == null) continue;
                if (!t.IsClass || t.IsAbstract) continue;
                if (t.Namespace?.StartsWith("CertPlatform.", StringComparison.Ordinal) != true) continue;

                ValidateEntityType(t);
                count++;
            }
        }
        FlushNamespaceWarnings();
        return count;
    }

    /// <summary>
    ///     ★ 推荐入口：启动期校验业务实体，**同时使用静态黑名单 + 框架程序集动态推导黑名单**
    ///
    ///     相比 <see cref="ValidateAssembly"/>，本方法额外从 <paramref name="frameworkAssemblies"/>
    ///     推导框架实体名，因此框架层新增实体时无需再维护静态清单（防漂移）。
    /// </summary>
    /// <param name="businessAssemblies">业务程序集（CertPlatform.*）</param>
    /// <param name="frameworkAssemblies">框架程序集（YZH.Core.*），用于动态推导黑名单</param>
    /// <returns>已校验的实体类型数</returns>
    /// <exception cref="InvalidOperationException">发现命名违规时抛出（导致启动失败）</exception>
    public static int ValidateWithFrameworkGuard(
        IEnumerable<Assembly> businessAssemblies,
        params Assembly[] frameworkAssemblies)
    {
        var derived = DeriveFromFramework(frameworkAssemblies);

        int count = 0;
        foreach (var assembly in businessAssemblies)
        {
            foreach (var t in SafeGetTypes(assembly))
            {
                if (t == null) continue;
                if (!t.IsClass || t.IsAbstract) continue;
                if (t.Namespace?.StartsWith("CertPlatform.", StringComparison.Ordinal) != true) continue;

                ValidateEntityType(t, derived);
                count++;
            }
        }
        FlushNamespaceWarnings();
        return count;
    }
}
