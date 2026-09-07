# YZH Core - 通用后端核心框架

完全和业务无关的后端基础设施。

## 项目结构

```
YZH.Core/
├── YZH.Core.Stand/      ← 基础模型层：BaseEntity, EntityConfig, ApiResponse, 工具类
├── YZH.Core.DataBase/   ← 数据访问层：EF Core封装, IRepository, BaseRepository
├── YZH.Core.Api/        ← Web核心层：BaseController, EntityConfigController, Filters
└── YZH.Core.Web/        ← Web启动层：YzhWebBuilder, GlobalExceptionMiddleware
```

## 依赖关系

```
YZH.Core.Web
    └──► YZH.Core.Api
             └──► YZH.Core.DataBase
                      └──► YZH.Core.Stand
```

## 核心能力

1. **配置驱动UI（EntityConfig）**
   - 表格/表单布局由 JSON/XML 配置决定
   - 前端通过 `/api/entityconfig/{tableName}` 获取配置
   - 修改配置文件即可调整界面，无需后端代码变更

2. **通用 CRUD（BaseController）**
   - 继承 BaseController<T> 自动获得完整 REST API
   - 分页、排序、过滤、搜索开箱即用

3. **统一仓储（IRepository）**
   - 接口驱动，可替换为任意 ORM 实现
   - 事务、批量操作、延迟加载统一封装

4. **全局异常处理**
   - GlobalExceptionMiddleware：管道最外层捕获
   - GlobalExceptionFilter：Controller层兜底

## 使用方式

业务项目只需：
1. 引用 YZH.Core.Web
2. 继承 BaseDbContext 注册实体
3. 继承 BaseController<T> 创建 API
4. 编写 EntityConfig JSON 配置文件

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.UseYzhCore(options => {
    options.JwtSecret = "your-secret";
});
var app = builder.Build();
app.UseYzhPipeline();
app.Run();
```
