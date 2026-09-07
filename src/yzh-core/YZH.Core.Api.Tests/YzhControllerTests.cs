using Microsoft.AspNetCore.Mvc;
using YZH.Core.Api.Controllers;
using YZH.Core.Api.Models.Users;
using YZH.Core.Api.Services;
using YZH.Core.Stand.Models;
using YZH.Entity.DomainModels;

namespace YZH.Core.Api.Tests;

/// <summary>
/// YZH-Core 架构自动化测试套件
/// 测试单表解耦的 Controller 接口
/// </summary>
public class YzhControllerTests
{
    private readonly Dictionary<string, object> _state = new();
    private string? _token;

    #region 测试辅助方法

    /// <summary>执行登录并获取 Token</summary>
    private async Task<string> LoginAsync()
    {
        var loginData = new
        {
            userName = "admin",
            password = "123456",
            rememberMe = false
        };

        var response = await ExecutePostAsync("api/Auth/login", loginData);
        var result = ParseJsonResponse<LoginResult>(response);
        
        if (result.Success && !string.IsNullOrEmpty(result.Data?.AccessToken))
        {
            _token = result.Data.AccessToken;
            return _token;
        }
        
        throw new Exception($"登录失败：{result.Error ?? "未知错误"}");
    }

    /// <summary>执行 GET 请求</summary>
    private async Task<string> ExecuteGetAsync(string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"http://localhost:9992/{endpoint}");
        AddAuthHeader(request);
        return await SendRequestAsync(request);
    }

    /// <summary>执行 POST 请求</summary>
    private async Task<string> ExecutePostAsync(string endpoint, object? data = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"http://localhost:9992/{endpoint}")
        {
            Content = data != null 
                ? new StringContent(System.Text.Json.JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json")
                : null
        };
        AddAuthHeader(request);
        return await SendRequestAsync(request);
    }

    /// <summary>执行 PUT 请求</summary>
    private async Task<string> ExecutePutAsync(string endpoint, object data)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"http://localhost:9992/{endpoint}")
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(data), System.Text.Encoding.UTF8, "application/json")
        };
        AddAuthHeader(request);
        return await SendRequestAsync(request);
    }

    /// <summary>执行 DELETE 请求</summary>
    private async Task<string> ExecuteDeleteAsync(string endpoint)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, $"http://localhost:9992/{endpoint}");
        AddAuthHeader(request);
        return await SendRequestAsync(request);
    }

    /// <summary>添加认证头</summary>
    private void AddAuthHeader(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_token))
        {
            request.Headers.Add("Authorization", $"Bearer {_token}");
        }
    }

    /// <summary>发送请求并返回响应体</summary>
    private async Task<string> SendRequestAsync(HttpRequestMessage request)
    {
        using var client = new HttpClient();
        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"HTTP {response.StatusCode}: {content}");
        }
        
        return content;
    }

    /// <summary>解析 JSON 响应</summary>
    private T ParseJsonResponse<T>(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(json) 
            ?? throw new Exception("响应解析失败");
    }

    #endregion

    #region SysUserController 测试

    [Fact]
    public async Task SysUser_GetPage_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();
        var requestData = new { page = 1, pageSize = 20, searchKey = "" };

        // Act
        var response = await ExecutePostAsync("api/SysUser/page", requestData);
        var result = ParseJsonResponse<ApiResponse<PagedResult<Sys_User>>>(response);

        // Assert
        Assert.True(result.Success, $"分页查询失败：{result.Error}");
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Items);
    }

    [Fact]
    public async Task SysUser_Add_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();
        var newUser = new
        {
            userName = $"testuser_{DateTime.Now:yyyyMMddHHmmss}",
            userTrueName = "测试用户",
            roleId = 30,
            enable = 1,
            phoneNo = "13800138000",
            userPwd = "Test1234!"
        };

        // Act
        var response = await ExecutePostAsync("api/SysUser/add", newUser);
        var result = ParseJsonResponse<ApiResponse<Sys_User>>(response);

        // Assert
        Assert.True(result.Success, $"新增失败：{result.Error}");
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.Id);
        
        // 保存 ID 用于后续清理
        _state[$"UserId_{newUser.userName}"] = result.Data.Id;
    }

    [Fact]
    public async Task SysUser_Update_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();
        var userId = _state.TryGetValue("UserId_testuser", out var id) ? id.ToString() : "dummy-id";
        var updateData = new
        {
            id = userId,
            userName = "testuser",
            userTrueName = "更新后的姓名",
            roleId = 30,
            enable = 1
        };

        // Act
        var response = await ExecutePostAsync("api/SysUser/update", updateData);
        var result = ParseJsonResponse<ApiResponse<Sys_User>>(response);

        // Assert
        Assert.True(result.Success, $"更新失败：{result.Error}");
    }

    [Fact]
    public async Task SysUser_Delete_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();
        var userId = _state.TryGetValue("UserId_testuser", out var id) ? id.ToString() : "dummy-id";

        // Act
        var response = await ExecutePostAsync("api/SysUser/delete", new[] { userId });
        var result = ParseJsonResponse<ApiResponse>(response);

        // Assert
        Assert.True(result.Success, $"删除失败：{result.Error}");
    }

    [Fact]
    public async Task SysUser_GetConfig_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();

        // Act
        var response = await ExecuteGetAsync("api/SysUser/config");
        var result = ParseJsonResponse<ApiResponse<EntityConfig>>(response);

        // Assert
        Assert.True(result.Success, $"获取配置失败：{result.Error}");
        Assert.NotNull(result.Data);
    }

    #endregion

    #region SysRoleController 测试

    [Fact]
    public async Task SysRole_GetPage_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();
        var requestData = new { page = 1, pageSize = 20, searchKey = "" };

        // Act
        var response = await ExecutePostAsync("api/SysRole/page", requestData);
        var result = ParseJsonResponse<ApiResponse<PagedResult<Sys_Role>>>(response);

        // Assert
        Assert.True(result.Success, $"分页查询失败：{result.Error}");
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task SysRole_Add_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();
        var newRole = new
        {
            code = $"ROLE_{DateTime.Now:yyyyMMddHHmmss}",
            roleName = "测试角色",
            parentId = 0,
            enable = 1
        };

        // Act
        var response = await ExecutePostAsync("api/SysRole/add", newRole);
        var result = ParseJsonResponse<ApiResponse<Sys_Role>>(response);

        // Assert
        Assert.True(result.Success, $"新增失败：{result.Error}");
        Assert.NotNull(result.Data);
    }

    #endregion

    #region SysMenuController 测试

    [Fact]
    public async Task SysMenu_GetPage_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();
        var requestData = new { page = 1, pageSize = 20, searchKey = "" };

        // Act
        var response = await ExecutePostAsync("api/SysMenu/page", requestData);
        var result = ParseJsonResponse<ApiResponse<PagedResult<Sys_Menu>>>(response);

        // Assert
        Assert.True(result.Success, $"分页查询失败：{result.Error}");
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task SysMenu_GetTree_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();

        // Act
        var response = await ExecuteGetAsync("api/SysMenu/tree");
        var result = ParseJsonResponse<ApiResponse<List<Sys_Menu>>>(response);

        // Assert
        Assert.True(result.Success, $"获取菜单树失败：{result.Error}");
        Assert.NotNull(result.Data);
    }

    #endregion

    #region SysDictionaryController 测试

    [Fact]
    public async Task SysDictionary_GetPage_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();
        var requestData = new { page = 1, pageSize = 20, searchKey = "" };

        // Act
        var response = await ExecutePostAsync("api/SysDictionary/page", requestData);
        var result = ParseJsonResponse<ApiResponse<PagedResult<Sys_Dictionary>>>(response);

        // Assert
        Assert.True(result.Success, $"分页查询失败：{result.Error}");
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task SysDictionary_GetItems_ReturnsSuccess()
    {
        // Arrange
        await LoginAsync();

        // Act
        var response = await ExecuteGetAsync("api/SysDictionary/items/cert_status");
        var result = ParseJsonResponse<ApiResponse<List<DictItem>>>(response);

        // Assert
        Assert.True(result.Success, $"获取字典项失败：{result.Error}");
    }

    #endregion

    #region 辅助模型

    private class LoginResult
    {
        public string? AccessToken { get; set; }
    }

    private class ApiResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
    }

    private class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public T? Data { get; set; }
        public int Code { get; set; }
        public long Timestamp { get; set; }
    }

    private class ApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Error { get; set; }
        public int Code { get; set; }
        public long Timestamp { get; set; }
    }

    private class PagedResult<T>
    {
        public List<T>? Items { get; set; }
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    private class EntityConfig
    {
        public string? TableName { get; set; }
        public string? Title { get; set; }
        public List<DefineColumn>? Columns { get; set; }
    }

    private class DefineColumn
    {
        public string? FieldName { get; set; }
        public string? DesName { get; set; }
        public bool BCFlag { get; set; }
        public bool YXK { get; set; }
    }

    private class DictItem
    {
        public string? DictCode { get; set; }
        public string? DictLabel { get; set; }
        public string? DictValue { get; set; }
        public int SortNo { get; set; }
    }

    #endregion
}
