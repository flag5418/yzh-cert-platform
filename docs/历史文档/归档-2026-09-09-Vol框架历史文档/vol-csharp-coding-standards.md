# C# 后端编码规范

> **版本**: V1.0 | **日期**: 2026-08-11 | **状态**: 草案

---

## 一、类设计规范

### 1.1 类注释规范

**所有公共类必须包含完整的XML注释**，包括：
- 类的作用描述
- 使用示例（何时使用、如何使用）
- 注意事项

```csharp
/// <summary>
/// 文档提取规则服务
/// 
/// 【职责】
/// - 管理文档提取规则的CRUD操作
/// - 调用AI引擎进行文档分析和字段提取
/// - 管理提取Prompt模板
/// 
/// 【使用方式】
/// 通过DI注入使用：
/// var service = serviceProvider.GetRequiredService<IDocExtractionRuleService>();
/// var result = await service.AIAnalyzeAsync(request);
/// 
/// 【线程安全】
/// 本类无状态，线程安全。
/// 
/// 【注意事项】
/// - AI调用可能超时，请设置合理的TimeoutSeconds
/// - 大文件提取建议异步处理
/// </summary>
public class DocExtractionRuleService : IDocExtractionRuleService, IDependency
{
    // ...
}
```

### 1.2 类大小限制

| 类型 | 最大行数 | 最大方法数 | 处理方式 |
|------|----------|------------|----------|
| Service | 500行 | 20个 | 超出需拆分 |
| Controller | 300行 | 15个 | 超出需拆分 |
| Repository | 200行 | 10个 | 超出需拆分 |
| Entity | 150行 | - | 考虑拆分表 |

**上帝类检测**：
```csharp
// ❌ 错误：一个Service承担过多职责
public class StandardDirectoryService  // 1816行
{
    // 目录管理、文件转换、编码生成、权限控制...
}

// ✅ 正确：按职责拆分
public interface IStandardDirectoryService : IDependency { }
public interface IFileConversionService : IDependency { }
public interface ICodeGenerationService : IDependency { }
```

---

## 二、方法设计规范

### 2.1 方法注释规范

**公共方法必须包含摘要注释**：
```csharp
/// <summary>
/// AI自动分析文档，推荐字段和表格定义
/// 
/// 【执行流程】
/// 1. 验证文件存在性
/// 2. 提取文档内容（NPOI/PDF.NET）
/// 3. 调用WorkflowEngine + LlmExtractSkill
/// 4. 解析AI返回的结构化数据
/// 
/// 【异常处理】
/// - 文件不存在：返回 AIAnalyzeResponse { Message = "文件不存在" }
/// - AI调用失败：返回 AIAnalyzeResponse { Message = "AI分析失败" }
/// 
/// 【性能提示】
/// 大文件（>50MB）分析可能需要30秒以上
/// </summary>
public async Task<AIAnalyzeResponse> AIAnalyzeAsync(AIAnalyzeRequest request)
```

### 2.2 方法长度限制

| 方法类型 | 最大行数 | 说明 |
|----------|----------|------|
| 业务方法 | 50行 | 超出需抽取子方法 |
| 私有方法 | 30行 | 过于复杂应重新设计 |
| 构造函数 | 10行 | 仅做初始化 |

---

## 三、异常处理规范

### 3.1 全局异常处理

**系统已配置全局异常中间件**，业务代码**禁止**使用 try-catch 捕获系统异常：

```csharp
// ❌ 错误：滥用try-catch
public async Task<AIConfigDto> GetAIConfigAsync()
{
    try
    {
        var config = await repository.DbContext.Set<AIConfig>()...
        return new AIConfigDto { ... };
    }
    catch (Exception ex)
    {
        // 全局异常已处理，不应在此捕获
        return null;
    }
}

// ✅ 正确：让异常自然抛出
public async Task<AIConfigDto> GetAIConfigAsync()
{
    var config = await repository.DbContext.Set<AIConfig>()
        .FirstOrDefaultAsync(x => x.IsEnabled);
    
    if (config == null)
        return new AIConfigDto { Provider = "qwen", Model = "qwen-turbo" };
    
    return new AIConfigDto 
    { 
        Provider = config.Provider,
        ApiKey = config.ApiKey,
        Model = config.Model
    };
}
```

### 3.2 允许使用try-catch的场景

| 场景 | 示例 |
|------|------|
| 外部调用容错 | HTTP请求、文件I/O |
| 资源清理 | FileStream、HttpClient |
| 异步取消 | CancellationToken |

```csharp
// ✅ 允许：外部调用容错
try
{
    await fileExtractor.ExtractAsync(fileInfo);
}
catch (FileFormatException)
{
    // 记录日志，返回友好提示
    return FileExtractionResult.CreateBase("不支持的文件格式");
}

// ✅ 允许：资源清理
using var stream = new FileStream(...);
try
{
    await stream.WriteAsync(data);
}
finally
{
    await stream.DisposeAsync();
}
```

### 3.3 禁止的空catch块

```csharp
// ❌ 严禁：空catch块
catch { }

// ❌ 严禁：吞掉异常
catch (Exception)
{
    // 什么都不做
}

// ✅ 正确：记录日志后重新抛出
catch (Exception ex)
{
    _logger.LogError(ex, "处理文档失败: {FileCode}", fileCode);
    throw;  // 或抛出自定义异常
}
```

---

## 四、依赖注入规范

### 4.1 禁止静态Instance属性

```csharp
// ❌ 错误：静态Instance绕过DI容器
public static IService Instance 
{
    get { return AutofacContainerModule.GetService<IService>(); }
}

// ✅ 正确：通过构造函数注入
public class MyService : IService
{
    private readonly IDependency _dep;
    
    public MyService(IDependency dep)  // [ActivatorUtilitiesConstructor]
    {
        _dep = dep;
    }
}
```

### 4.2 构造函数规范

```csharp
// ✅ 标准模式
public partial class DocExtractionRuleService : ServiceBase<...>, IDependency
{
    [ActivatorUtilitiesConstructor]
    public DocExtractionRuleService(ICertDocExtractionRuleRepository repository)
        : base(repository)
    {
    }
}
```

---

## 五、DTO与Entity分离

### 5.1 禁止直接暴露Entity

```csharp
// ❌ 错误：Controller直接返回Entity
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id)
{
    var entity = await _service.GetAsync(id);
    return Ok(entity);  // 暴露内部模型
}

// ✅ 正确：使用DTO
[HttpGet("{id}")]
public async Task<IActionResult> Get(int id)
{
    var dto = await _service.GetDtoAsync(id);
    return Ok(dto);
}
```

### 5.2 DTO设计规范

```csharp
public class AIAnalyzeRequest
{
    /// <summary>文件编码</summary>
    [Required]
    public string FileCode { get; set; }
    
    /// <summary>技能类型：word/excel/pdf</summary>
    [Required][RegularExpression("^(word|excel|pdf)$")]
    public string Skill { get; set; }
}
```

---

## 六、命名规范

### 6.1 方法命名

| 类型 | 后缀 | 示例 |
|------|------|------|
| 异步方法 | Async | `GetListAsync()`, `SaveAsync()` |
| 布尔返回 | Is/Can/Has | `IsEnabled`, `CanDelete` |
| 查询方法 | Get/Find/Query | `GetById()`, `FindByCode()` |

### 6.2 常量定义

```csharp
// ✅ 正确：提取为常量
private const string PromptCodeAnalyze = "analyze_{skill}_v1";
private const int MaxDocumentSizeMB = 50;
private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);
```

---

## 七、日志规范

### 7.1 使用ILogger

```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
    }
    
    public async Task DoWork()
    {
        _logger.LogInformation("开始处理任务");
        _logger.LogError(ex, "处理失败: {Detail}", detail);
    }
}
```

### 7.2 禁止Console.WriteLine

```csharp
// ❌ 错误
Console.WriteLine("[DocExtractionRule] 📝 使用数据库提示词");

// ✅ 正确
_logger.LogInformation("[DocExtractionRule] 使用数据库提示词: {PromptCode}", promptCode);
```

---

## 八、代码审查检查清单

- [ ] 类是否有完整的XML注释？
- [ ] 方法是否有摘要注释？
- [ ] 是否使用了静态Instance属性？（禁止）
- [ ] 是否有不必要的try-catch？（全局异常已处理）
- [ ] 是否有空catch块？（禁止）
- [ ] 是否直接暴露Entity？（应使用DTO）
- [ ] 类是否超过500行？（需拆分）
- [ ] 方法是否超过50行？（需抽取子方法）
- [ ] 是否使用ILogger而非Console.WriteLine？
