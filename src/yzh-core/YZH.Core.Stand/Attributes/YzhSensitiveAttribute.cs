namespace YZH.Core.Stand.Annotations;

/// <summary>
///     敏感字段标记：**序列化输出时永不写出，但反序列化输入仍可正常绑定**。
///
///     用途：密码、令牌、密钥等「只进不出」的字段。
///
///     ⚠️ 与 <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute" /> 的区别：
///     - <c>[JsonIgnore]</c>：输入输出**双向**禁掉 → 密码无法从请求体绑定（新增/改密会失效）
///     - <c>[JsonIgnore(Condition = WhenWritingDefault)]</c>：**只在值为默认值时**不写出
///       → 对「库里是非空密文」的密码字段**完全无效**（这是本项目曾踩过的坑）
///     - <c>[YzhSensitive]</c>：**只禁输出**，输入不受影响 → 密码可提交、绝不可能被回吐
///
///     生效前提：已在 <c>Program.cs</c> / <c>YzhWebBuilder.UseYzhCore</c> 的
///     <c>AddJsonOptions</c> 中调用 <c>ApplySensitiveFieldMasking()</c>。
///
///     注意：<see cref="Helpers.EntitySchemaHelper" /> 会把本特性标记的字段
///     排除出 Schema / NewEntity（与既有 [JsonIgnore] 行为保持一致）。
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class YzhSensitiveAttribute : Attribute
{
}
