using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace YZH.Core.Api.Tests;

/// <summary>
///     关键路径端到端冒烟测试（真实 HTTP，直打运行中的后端）
///
///     <para>前置条件</para>
///     <para>1. 后端已在 :9992 运行 —— <c>bash scripts/backend/restart-backend.sh</c></para>
///     <para>2. 无需处理代理：本类 HttpClient 已 <c>UseProxy = false</c>（否则 HTTP_PROXY 会劫持 localhost 请求）</para>
///
///     <para>运行</para>
///     <para><c>dotnet test src/yzh-core/YZH.Core.Api.Tests/YZH.Core.Api.Tests.csproj</c></para>
///     <para>可用环境变量 <c>YZH_BASE_URL</c> 覆盖后端地址</para>
///
///     <para>覆盖范围</para>
///     <list type="bullet">
///         <item>认证：登录成功 / 密码错 401 / 缺参 → 200+信封 code=400 / 未带 Token 401（全局认证兜底）</item>
///         <item>验证码：<c>getVierificationCode</c> 返回裸 JSON（例外 E6）且 img 是 PNG base64</item>
///         <item>契约：菜单树扁平 PascalCase（<c>MenuName</c> 而非 <c>Name</c>）、filter 分页信封、<c>UserPwd</c> 脱敏</item>
///         <item>CRUD 往返：<c>cert_sys_config</c> 的 add → filter → update → filter → delete</item>
///     </list>
///
///     <para>⚠️ 尚未覆盖（依赖未建成的模块）</para>
///     <para>建档 → 上传 → 提取（<c>CertPlatform.Enterprise</c> / <c>cert-enterprise</c> 仍为空）、报告生成。</para>
///
///     <para>⚠️ 数据副作用</para>
///     <para><see cref="Config_Crud_RoundTrip_Should_Succeed"/> 每次运行会在 <c>cert_sys_config</c> 留下 1 行
///     <b>软删除</b>探针数据（<c>ConfigKey</c> 以 <c>E2E_SMOKE_</c> 开头）。定期清理：</para>
///     <code>
///     DELETE FROM cert_sys_config WHERE ConfigKey LIKE 'E2E_SMOKE_%';
///     </code>
/// </summary>
public class CriticalPathSmokeTests
{
    private const string ProbeKeyPrefix = "E2E_SMOKE_";

    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>登录 Token（懒加载，整个测试类复用一次登录）</summary>
    private static readonly Lazy<Task<string>> Token =
        new(LoginAsync, LazyThreadSafetyMode.ExecutionAndPublication);

    // ========================================================
    // 基础设施
    // ========================================================

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            // ⚠️ 必须关掉代理：否则 HTTP_PROXY 会劫持 localhost 请求，测试表现为莫名超时
            UseProxy = false,
            AllowAutoRedirect = false
        };

        var baseUrl = Environment.GetEnvironmentVariable("YZH_BASE_URL") ?? "http://127.0.0.1:9992";
        return new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    private static StringContent JsonBody(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static async Task<HttpResponseMessage> PostAuthorizedAsync(string path, object? payload = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonBody(payload ?? new { })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await Token.Value);
        return await Http.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetAuthorizedAsync(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await Token.Value);
        return await Http.SendAsync(request);
    }

    /// <summary>后端可达性前置检查 —— 不可达时给出可操作的错误，而不是让断言以「401」形式误报</summary>
    private static async Task EnsureBackendReachableAsync()
    {
        try
        {
            using var response = await Http.GetAsync("api/User/ping");
            if (response.StatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException(
                    $"后端 {Http.BaseAddress} 健康检查返回 {(int)response.StatusCode}，期望 200。");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"后端未在 {Http.BaseAddress} 运行。请先执行：bash scripts/backend/restart-backend.sh", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new InvalidOperationException($"后端 {Http.BaseAddress} 请求超时。", ex);
        }
    }

    private static async Task<string> LoginAsync()
    {
        await EnsureBackendReachableAsync();

        using var response = await Http.PostAsync("api/User/login",
            JsonBody(new { UserName = "admin", Password = "123456" }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, $"登录失败：HTTP {(int)response.StatusCode} —— {body}");

        using var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean(), $"登录返回 success=false：{body}");

        var token = doc.RootElement.GetProperty("data").GetProperty("Token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token), $"登录响应缺少 data.Token：{body}");
        return token!;
    }

    // ========================================================
    // 一、认证
    // ========================================================

    [Fact]
    public async Task Ping_Should_Return_200()
    {
        using var response = await Http.GetAsync("api/User/ping");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("pong", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_With_Valid_Credentials_Should_Return_Token()
    {
        await EnsureBackendReachableAsync();

        using var response = await Http.PostAsync("api/User/login",
            JsonBody(new { UserName = "admin", Password = "123456" }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // 信封是 camelCase（例外 E1），但 data 载荷是 PascalCase
        Assert.True(root.GetProperty("success").GetBoolean(), body);
        var data = root.GetProperty("data");
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("Token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(data.GetProperty("UserCode").GetString()));
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Should_Return_401()
    {
        await EnsureBackendReachableAsync();

        using var response = await Http.PostAsync("api/User/login",
            JsonBody(new { UserName = "admin", Password = "__definitely_wrong_password__" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    ///     缺参登录 → **业务失败**：HTTP 200 + 信封 <c>success:false</c> / <c>code:400</c> / <c>err</c> 非空。
    ///
    ///     <para>★ 2026-09-25 契约变更（前后端信封统一改造）：业务失败不再用 HTTP 状态码表达，
    ///     一律 HTTP 200，业务码落在 payload 的 <c>code</c>。原断言 <c>BadRequest</c> 已过期。</para>
    ///     <para>真·传输层/鉴权错误仍是 4xx（见 <see cref="Login_With_Wrong_Password_Should_Return_401"/>
    ///     与 <see cref="Protected_Endpoint_Without_Token_Should_Return_401"/>）。</para>
    /// </summary>
    [Fact]
    public async Task Login_Without_Credentials_Should_Return_Envelope_BizFail()
    {
        await EnsureBackendReachableAsync();

        using var response = await Http.PostAsync("api/User/login",
            JsonBody(new { UserName = "", Password = "" }));

        // 业务失败恒 HTTP 200：HTTP 状态只表达传输层语义
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(400, root.GetProperty("code").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("err").GetString()));
    }

    [Fact]
    public async Task Protected_Endpoint_Without_Token_Should_Return_401()
    {
        await EnsureBackendReachableAsync();

        // 刻意不带 Authorization 头 —— 验证全局认证兜底（PermissionFilter）生效
        using var response = await Http.PostAsync("api/System/User/filter",
            JsonBody(new { Page = 1, PageSize = 1 }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ========================================================
    // 二、验证码（2026-09-24 端点收敛后的唯一生产端点）
    // ========================================================

    [Fact]
    public async Task Captcha_Should_Return_Raw_Png_Json_Without_Envelope()
    {
        using var response = await Http.GetAsync("api/User/getVierificationCode");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        // 例外 E6：裸 JSON，**没有** ApiResponse 信封（前端 yzhApi 原样透传）
        Assert.False(root.TryGetProperty("success", out _),
            "验证码端点应返回裸 JSON（例外 E6），不应出现 success 信封字段");

        Assert.True(root.TryGetProperty("img", out var img), "缺少 img 字段");
        Assert.True(root.TryGetProperty("uuid", out var uuid), "缺少 uuid 字段");

        var imgValue = img.GetString();
        Assert.False(string.IsNullOrWhiteSpace(imgValue), "img 为空");
        Assert.False(string.IsNullOrWhiteSpace(uuid.GetString()), "uuid 为空");

        // 前端渲染为 data:image/png;base64,${img} —— 必须是 PNG，不能是 SVG data-uri
        Assert.StartsWith("iVBORw0KGgo", imgValue, StringComparison.Ordinal);
    }

    // ========================================================
    // 三、数据契约（PascalCase 铁律 + 分页信封 + 脱敏）
    // ========================================================

    [Fact]
    public async Task Menu_Tree_Should_Return_Flat_PascalCase_List()
    {
        using var response = await GetAuthorizedAsync("api/System/MenuManagement/tree");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean(), body);

        var data = root.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() > 0, "菜单树为空（超管应返回全量）");

        var first = data[0];
        // ⚠️ 字段名是 MenuName 不是 Name —— 读 row.name 会静默渲染成空白（铁律七陷阱）
        Assert.True(first.TryGetProperty("MenuName", out _), "菜单节点缺少 PascalCase 的 MenuName");
        Assert.False(first.TryGetProperty("name", out _), "菜单节点不应出现小写 name");
        // 扁平结构：前端自行 buildTree，节点不应自带 Children
        Assert.False(first.TryGetProperty("Children", out _), "MenuManagement/tree 应返回扁平列表（前端 buildTree）");
    }

    [Fact]
    public async Task User_Filter_Should_Return_PascalCase_PagedResult_And_Mask_Password()
    {
        using var response = await PostAuthorizedAsync("api/System/User/filter", new { Page = 1, PageSize = 5 });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean(), body);

        // PagedResult 无 [JsonPropertyName]，走 PropertyNamingPolicy = null → PascalCase
        var data = root.GetProperty("data");
        Assert.True(data.TryGetProperty("Items", out var items), "分页契约字段应为 PascalCase 的 Items");
        Assert.True(data.TryGetProperty("TotalCount", out var totalCount), "分页契约字段应为 PascalCase 的 TotalCount");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(totalCount.GetInt32() > 0, "用户总数为 0（种子数据异常）");

        // [YzhSensitive] 脱敏：UserPwd 永不写出（曾实测 18 处密码密文外泄）
        Assert.DoesNotContain("UserPwd", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Organization_Tree_Root_Should_Return_PascalCase_Nodes()
    {
        using var response = await PostAuthorizedAsync("api/System/Organization/tree/root", new { });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.True(root.GetProperty("success").GetBoolean(), body);

        var data = root.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.True(data.GetArrayLength() > 0, "组织树根节点为空");

        // 组织树 DTO 是已登记例外 E4：PascalCase 的 Code/Name/IsLeaf/Level
        var first = data[0];
        Assert.True(first.TryGetProperty("Code", out _));
        Assert.True(first.TryGetProperty("Name", out _));
        Assert.True(first.TryGetProperty("IsLeaf", out _));
    }

    // ========================================================
    // 四、CRUD 往返（框架基类契约）
    // ========================================================

    [Fact]
    public async Task Config_Crud_RoundTrip_Should_Succeed()
    {
        // uk_config_key 是全局唯一（不含 IsDeleted）→ 必须每次用新 key，否则第二次运行必然唯一约束冲突
        var key = $"{ProbeKeyPrefix}{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        string? code = null;

        try
        {
            // ── add ──
            using (var response = await PostAuthorizedAsync("api/System/Config/add", new
                   {
                       ConfigKey = key,
                       // ⚠️ cert_sys_config.Category：DB 是 NOT NULL 且无默认值，但 EntityConfig 标了 Yxk=true。
                       //    ★ Yxk = 「允许为空」（不是必填）→ 基类校验条件 `BcFlag && !Yxk` 被跳过 →
                       //      省略该字段会一路写到 MySQL，由 DB 层抛 cannot be null，再被归一化成
                       //      「新增失败：必填字段为空」（**文案误导**：来源是 DB 约束，不是 EntityConfig）。
                       //    经 UI 提交时字段是空串（空串 ≠ NULL）故不暴露，只有 API 直调省略才踩到。
                       Category = "E2E",
                       ConfigType = "String",
                       ConfigValue = "v1",
                       IsReadonly = 0, // ⚠️ 实体里是 int，不是 bool（EntityConfig 写的是 Switch）
                       IsValid = 1
                   }))
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.True(response.IsSuccessStatusCode, $"新增失败：HTTP {(int)response.StatusCode} —— {body}");

                using var doc = JsonDocument.Parse(body);
                Assert.True(doc.RootElement.GetProperty("success").GetBoolean(), $"新增返回 success=false：{body}");
                code = doc.RootElement.GetProperty("data").GetProperty("Code").GetString();
            }

            Assert.False(string.IsNullOrWhiteSpace(code), "新增响应缺少 data.Code");

            // ── filter：新增后立即可查（同时验证 IsValid 硬过滤未误伤新记录） ──
            Assert.Equal("v1", await ReadConfigValueAsync(key));

            // ── update ──
            using (var response = await PostAuthorizedAsync("api/System/Config/update", new
                   {
                       Code = code,
                       ConfigKey = key,
                       Category = "E2E",
                       ConfigType = "String",
                       ConfigValue = "v2",
                       IsReadonly = 0,
                       IsValid = 1
                   }))
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.True(response.IsSuccessStatusCode, $"修改失败：HTTP {(int)response.StatusCode} —— {body}");
            }

            // ── filter 复核更新生效 ──
            Assert.Equal("v2", await ReadConfigValueAsync(key));

            // ── delete（软删除） ──
            using (var response = await PostAuthorizedAsync("api/System/Config/delete", new[] { code! }))
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.True(response.IsSuccessStatusCode, $"删除失败：HTTP {(int)response.StatusCode} —— {body}");
                using var doc = JsonDocument.Parse(body);
                Assert.True(doc.RootElement.GetProperty("success").GetBoolean(), body);
            }

            // ── 删除后不可查 ──
            Assert.Null(await ReadConfigValueAsync(key));
        }
        finally
        {
            // 安全网：断言中途失败也要尽量回收探针数据（幂等，重复删除返回「已删除 0 条记录」）
            if (!string.IsNullOrWhiteSpace(code))
            {
                try
                {
                    using var _ = await PostAuthorizedAsync("api/System/Config/delete", new[] { code! });
                }
                catch
                {
                    // 清理失败不得掩盖真实断言失败
                }
            }
        }
    }

    /// <summary>按 ConfigKey 精确查询，返回 ConfigValue；查不到返回 null</summary>
    private static async Task<string?> ReadConfigValueAsync(string configKey)
    {
        using var response = await PostAuthorizedAsync("api/System/Config/filter", new
        {
            Page = 1,
            PageSize = 5,
            Filters = new[] { new { Field = "ConfigKey", Operator = "eq", Value = configKey } }
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(body);
        var items = doc.RootElement.GetProperty("data").GetProperty("Items");
        if (items.GetArrayLength() == 0) return null;

        Assert.Equal(1, items.GetArrayLength()); // 唯一约束保证最多一条
        return items[0].GetProperty("ConfigValue").GetString();
    }
}
