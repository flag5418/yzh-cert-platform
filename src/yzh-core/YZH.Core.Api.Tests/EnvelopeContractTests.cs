using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using YZH.Core.Api.Filters;
using YZH.Core.Api.Exceptions;
using YZH.Core.Stand.Models.Result;

namespace YZH.Core.Api.Tests;

/// <summary>
///     信封不变量与异常映射测试（前后端信封统一改造计划 §八 验收矩阵 P-07 / P-08 / P-12）
///
///     <list type="bullet">
///         <item><b>P-12</b>：<c>success:false</c> 却 <c>err==""</c> → <see cref="ApiResponseContractFilter"/> 违规即抛</item>
///         <item><b>P-08</b>：未处理系统异常 → 500 + <c>err</c> 脱敏「操作失败，请联系管理员」（生产环境）</item>
///         <item><b>P-07</b>：业务异常（<c>YZHNotFoundException</c>）→ HTTP 200 + <c>err</c> 原文</item>
///         <item><b>非信封响应</b>（匿名对象）→ 过滤器跳过，不参与判定</item>
///     </list>
///
///     <para>说明：P-08/P-12 无法从外部接口诱导（信封构造已封闭、业务异常全部走 200 信封），
///     正因如此它们才是「防回潮绊线」——这里用 <c>RuntimeHelpers.GetUninitializedObject</c>
///     绕过工厂造出坏信封，直接验证守卫会拦截。</para>
/// </summary>
public class EnvelopeContractTests
{
    private static ActionContext NewActionContext()
    {
        var httpContext = new DefaultHttpContext();
        return new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
    }

    private static ResultExecutingContext NewResultContext(object result) =>
        new(NewActionContext(), new List<IFilterMetadata>(), new ObjectResult(result), controller: null!);

    // ========================================================
    // P-12：信封不变量运行时守卫
    // ========================================================

    [Fact]
    public void 契约过滤器_失败信封但err为空_应当抛出()
    {
        // 绕过封闭构造：直接造一个未经工厂的坏信封（success:false / err:""）
        var broken = (ApiResponse)RuntimeHelpers.GetUninitializedObject(typeof(ApiResponse));
        var filter = new ApiResponseContractFilter(NullLogger<ApiResponseContractFilter>.Instance);

        var ex = Assert.Throws<InvalidOperationException>(
            () => filter.OnResultExecuting(NewResultContext(broken)));

        Assert.Contains("B-R4", ex.Message);
        Assert.Contains("①", ex.Message);
    }

    [Fact]
    public void 契约过滤器_成功信封err非空_应当抛出()
    {
        var broken = (ApiResponse)RuntimeHelpers.GetUninitializedObject(typeof(ApiResponse));
        // 用反射写入 init-only 属性（模拟错误的序列化回填）
        typeof(ApiResponse).GetProperty(nameof(ApiResponse.Success))!.SetValue(broken, true);
        typeof(ApiResponse).GetProperty(nameof(ApiResponse.Err))!.SetValue(broken, "不该有的错误文本");
        var filter = new ApiResponseContractFilter(NullLogger<ApiResponseContractFilter>.Instance);

        var ex = Assert.Throws<InvalidOperationException>(
            () => filter.OnResultExecuting(NewResultContext(broken)));

        Assert.Contains("②", ex.Message);
    }

    [Fact]
    public void 契约过滤器_合法信封_不抛()
    {
        var filter = new ApiResponseContractFilter(NullLogger<ApiResponseContractFilter>.Instance);

        filter.OnResultExecuting(NewResultContext(ApiResponse.Ok("操作成功")));
        filter.OnResultExecuting(NewResultContext(ApiResponse.Fail("机构不存在")));
        filter.OnResultExecuting(NewResultContext(ApiResponse<object>.Ok(new { Code = "C1" })));
        filter.OnResultExecuting(NewResultContext(ApiResponse<object>.Fail("该节点下有子节点")));
    }

    [Fact]
    public void 契约过滤器_匿名对象响应_跳过不判()
    {
        // P-13 的运行时侧：匿名对象不是信封，过滤器不参与判定（由 B-R1 静态守卫拦）
        var filter = new ApiResponseContractFilter(NullLogger<ApiResponseContractFilter>.Instance);
        filter.OnResultExecuting(NewResultContext(new { Name = "裸匿名对象" }));
    }

    // ========================================================
    // P-08 / P-07：GlobalExceptionFilter 的异常映射
    // ========================================================

    [Theory]
    [InlineData("Production", "操作失败，请联系管理员")]
    [InlineData("Staging", "操作失败，请联系管理员")]
    [InlineData("Development", "boom-secret-detail")]
    public void 异常过滤器_未处理系统异常_应当500并按环境脱敏(string envName, string expectedErr)
    {
        var filter = new GlobalExceptionFilter(
            NullLogger<GlobalExceptionFilter>.Instance,
            new StubHostEnvironment { EnvironmentName = envName });

        var ctx = new ExceptionContext(NewActionContext(), new List<IFilterMetadata>())
        {
            Exception = new InvalidOperationException("boom-secret-detail")
        };
        filter.OnException(ctx);

        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        var envelope = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.False(envelope.Success);
        Assert.Equal(expectedErr, envelope.Err);
        Assert.Equal(string.Empty, envelope.Message);
        Assert.Equal(500, envelope.Code);
        Assert.True(ctx.ExceptionHandled);
    }

    [Fact]
    public void 异常过滤器_业务异常_应当200并返回原文()
    {
        var filter = new GlobalExceptionFilter(
            NullLogger<GlobalExceptionFilter>.Instance,
            new StubHostEnvironment { EnvironmentName = "Production" });

        var ctx = new ExceptionContext(NewActionContext(), new List<IFilterMetadata>())
        {
            Exception = new YZHNotFoundException("机构不存在")
        };
        filter.OnException(ctx);

        var result = Assert.IsType<ObjectResult>(ctx.Result);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        var envelope = Assert.IsType<ApiResponse<object>>(result.Value);
        Assert.False(envelope.Success);
        Assert.Equal("机构不存在", envelope.Err);
        Assert.Equal(string.Empty, envelope.Message);
        Assert.Equal(400, envelope.Code);
    }

    /// <summary>IHostEnvironment 打桩（避免为测试引入 Mock 库）</summary>
    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "YZH.Core.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
