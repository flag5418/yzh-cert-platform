using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using YZH.Core.Stand.Annotations;

namespace YZH.Core.Stand.Extensions;

/// <summary>
///     全局敏感字段脱敏：让 <see cref="YzhSensitiveAttribute" /> 真正生效。
///
///     实现方式：给 <see cref="JsonSerializerOptions" /> 装一个
///     <see cref="DefaultJsonTypeInfoResolver" /> 修饰器，凡是带
///     <see cref="YzhSensitiveAttribute" /> 的属性，一律把
///     <c>ShouldSerialize</c> 置为恒 false。
///
///     ✅ 只影响**写出**（序列化）→ 密码绝不会出现在任何响应体里
///     ✅ 不影响**读入**（反序列化）→ 登录 / 新增 / 改密仍可正常绑定密码
///     ✅ 不改动任何字段名 → 不触碰「DB列名 = C#属性名 = TS字段名」铁律
///     ✅ 幂等：重复调用只会替换 resolver，不会叠加多个修饰器
/// </summary>
public static class JsonSensitiveFieldExtensions
{
    /// <summary>
    ///     安装敏感字段脱敏修饰器。必须在 <c>AddJsonOptions</c> / <c>Configure&lt;JsonOptions&gt;</c>
    ///     回调内、序列化选项首次被使用之前调用。
    /// </summary>
    public static JsonSerializerOptions ApplySensitiveFieldMasking(this JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(static typeInfo =>
        {
            // 只处理对象（含实体、DTO）；集合/字典等无需处理
            if (typeInfo.Kind != JsonTypeInfoKind.Object) return;

            foreach (var prop in typeInfo.Properties)
            {
                if (prop.AttributeProvider is null) continue;
                if (!prop.AttributeProvider.IsDefined(typeof(YzhSensitiveAttribute), inherit: true)) continue;

                // 永不写出；反序列化绑定不受影响
                prop.ShouldSerialize = static (_, _) => false;
            }
        });

        options.TypeInfoResolver = resolver;
        return options;
    }
}
