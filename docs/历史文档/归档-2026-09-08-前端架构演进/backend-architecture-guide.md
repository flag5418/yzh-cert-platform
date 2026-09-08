# 后端架构与代码存放规范

> **版本**：V1.0 | **日期**：2026-08-25 | **状态**：成熟态
>
> 本文档定义后端代码的架构分层、模块划分、代码存放规则，确保后续开发不再乱放代码。

---

## 一、架构总览

### 1.1 分层架构

```
VOL.WebApi (Controller)
    ↓
VOL.CERT (业务模块 - 体系认证专属)
    ↓
YZH.Core (架构层 - 可复用)
    ↓
VOL.Core (基础设施 - Vol 框架)
    ↓
VOL.Entity (实体定义)
```

### 1.2 模块职责

| 模块 | 性质 | 职责 | 可复用性 |
|------|------|------|----------|
| `VOL.WebApi` | API 层 | HTTP 接口、路由、认证 | ❌ 项目专属 |
| `VOL.CERT` | 业务层 | 体系认证业务逻辑 | ❌ 项目专属 |
| `YZH.Core` | 架构层 | 工作流、队列、AI、文件解析 | ✅ 跨项目复用 |
| `VOL.Core` | 基础设施 | EF Core、Dapper、Autofac、缓存 | ✅ Vol 框架 |
| `VOL.Entity` | 实体层 | 数据库实体、DTO | ✅ Vol 框架 |
| `VOL.Builder` | 框架级 Service | Sys_TableInfoService 等 | ✅ Vol 框架 |
| `VOL.Sys` | 系统模块 | 用户、角色、菜单 | ✅ Vol 框架 |
| `VOL.MES` | MES 模块 | 制造执行系统 | ❌ 独立模块 |

---

## 二、VOL.CERT 业务模块结构

### 2.1 目录结构

```
VOL.CERT/
├── IRepositories/
│   └── CertPlatform/
│       ├── Audit/              审核相关 Repository 接口
│       ├── Cert/               认证相关 Repository 接口
│       ├── DocExtraction/      文档提取 Repository 接口
│       ├── Ent/                企业相关 Repository 接口
│       ├── Rpt/                报告相关 Repository 接口
│       └── Wf/                 工作流相关 Repository 接口
├── Repositories/
│   └── CertPlatform/
│       └── (同上结构)          Repository 实现
├── IServices/
│   └── CertPlatform/
│       ├── Audit/              审核相关 Service 接口
│       ├── Cert/               认证相关 Service 接口
│       ├── DocExtraction/      文档提取 Service 接口
│       ├── Ent/                企业相关 Service 接口
│       ├── Infra/              基础设施 Service 接口
│       └── Wf/                 工作流相关 Service 接口
├── Services/
│   └── CertPlatform/
│       ├── Audit/              审核相关 Service 实现
│       ├── Cert/               认证相关 Service 实现
│       ├── DocExtraction/      文档提取 Service 实现
│       ├── Ent/                企业相关 Service 实现
│       ├── Infra/              基础设施 Service 实现
│       ├── Partial/            部分重写/扩展的 Service
│       ├── WorkflowEngine/     工作流引擎实现
│       ├── Helpers/            辅助工具
│       ├── Converters/         转换器
│       └── Wf/                 工作流 Service 实现
├── Extensions/
│   └── UniqueValidationExtensions.cs  扩展方法
└── VOL.CERT.csproj
```

### 2.2 代码存放规则

| 代码类型 | 存放位置 | 命名规范 |
|----------|----------|----------|
| Repository 接口 | `VOL.CERT/IRepositories/CertPlatform/{子模块}/` | `I{Entity}Repository.cs` |
| Repository 实现 | `VOL.CERT/Repositories/CertPlatform/{子模块}/` | `{Entity}Repository.cs` |
| Service 接口 | `VOL.CERT/IServices/CertPlatform/{子模块}/` | `I{Entity}Service.cs` |
| Service 实现 | `VOL.CERT/Services/CertPlatform/{子模块}/` | `{Entity}Service.cs` |
| 扩展方法 | `VOL.CERT/Extensions/` | `{Feature}Extensions.cs` |
| 辅助工具 | `VOL.CERT/Services/CertPlatform/Helpers/` | `{Feature}Helper.cs` |

### 2.3 子模块划分

| 子模块 | 说明 | 包含的实体/功能 |
|--------|------|----------------|
| `Audit` | 审核相关 | AuditTask, AuditorProfile |
| `Cert` | 认证相关 | ISOStandard, ISOClause, CertStage, CertificationBody |
| `DocExtraction` | 文档提取 | CertDocExtractionRule, StandardDirectory |
| `Ent` | 企业相关 | Enterprise, EnterpriseFile, ExtractionResult |
| `Rpt` | 报告相关 | ReportTemplate, ReportSection |
| `Wf` | 工作流相关 | Skill, PromptTemplate, WfSkillCategory |
| `Infra` | 基础设施 | SysConfig, Message, AIUsageLog, OfficeConvert |

---

## 三、YZH.Core 架构层结构

### 3.1 目录结构

```
YZH.Core/
├── AI/                     LLM 客户端、Prompt 解析
│   ├── Clients/            ILlmClient, QwenApiProvider, OllamaProvider
│   ├── Prompt/             PromptInterpreter
│   └── Plan/               AiPlan, AiStep
├── Workflow/               工作流引擎核心
│   ├── IWorkflowEngine.cs  工作流引擎接口
│   ├── WorkflowEngine.cs   工作流引擎实现
│   ├── ISkillNode.cs       Skill 节点接口
│   ├── SkillBase.cs        Skill 基类
│   ├── SkillExecutor.cs    反射调用器
│   └── Models/             工作流配置模型
├── Queue/                  通用队列引擎
│   ├── YzhQueueManager.cs  队列管理器
│   ├── YzhQueue.cs         队列主表
│   ├── YzhQueueTask.cs     队列任务
│   └── YzhQueueResourceLock.cs  资源锁
├── Extractor/              文件解析能力
│   ├── IFileExtractor.cs   文件提取器接口
│   ├── FileExtractorService.cs  统一文件提取服务
│   └── (Word/Excel/Pdf/Text/)  具体提取器实现
├── Skills/                 内置通用 Skill
│   ├── AssembleSkill.cs    文本拼接
│   ├── CompareSkill.cs     值比较
│   ├── LlmExtractSkill.cs  LLM 提取
│   └── DocumentExtractSkill.cs  文档解析
├── Audit/                  审计属性
├── Validation/             验证属性
├── CodeRule/               编码规则接口
├── DeleteStrategy/         删除策略接口
└── YZHModule.cs            Autofac 模块注册
```

### 3.2 代码存放规则

| 代码类型 | 存放位置 | 命名规范 |
|----------|----------|----------|
| 工作流引擎 | `YZH.Core/Workflow/` | `{Feature}.cs` |
| 队列引擎 | `YZH.Core/Queue/` | `YzhQueue*.cs` |
| AI 客户端 | `YZH.Core/AI/Clients/` | `{Provider}Provider.cs` |
| 文件解析 | `YZH.Core/Extractor/` | `{Format}Extractor.cs` |
| 通用 Skill | `YZH.Core/Skills/` | `{Feature}Skill.cs` |
| 架构属性 | `YZH.Core/{Module}/` | `YZH{Feature}.cs` |

---

## 四、VOL.WebApi 层结构

### 4.1 目录结构

```
VOL.WebApi/
├── Controllers/
│   ├── CertPlatform/        认证平台 Controller
│   │   ├── Partial/         部分重写的 Controller
│   │   └── *.cs             Controller 实现
│   ├── Sys/                 系统管理 Controller
│   ├── MES/                 MES 模块 Controller
│   ├── Builder/             代码生成器 Controller
│   ├── Hubs/                SignalR Hub
│   └── OSS/                 对象存储 Controller
├── Program.cs               应用入口、DI 注册
├── Startup.cs               服务配置
└── appsettings.json         配置文件
```

### 4.2 代码存放规则

| 代码类型 | 存放位置 | 命名规范 |
|----------|----------|----------|
| Controller | `VOL.WebApi/Controllers/CertPlatform/` | `{Feature}Controller.cs` |
| SignalR Hub | `VOL.WebApi/Controllers/Hubs/` | `{Feature}Hub.cs` |
| DI 注册 | `Program.cs` | 按功能分组注册 |

---

## 五、命名空间规范

### 5.1 命名空间对照表

| 模块 | 命名空间 | 说明 |
|------|----------|------|
| VOL.CERT IRepositories | `VOL.CERT.IRepositories.CertPlatform` | Repository 接口 |
| VOL.CERT Repositories | `VOL.CERT.Repositories.CertPlatform` | Repository 实现 |
| VOL.CERT IServices | `VOL.CERT.IServices.CertPlatform` | Service 接口 |
| VOL.CERT Services | `VOL.CERT.Services.CertPlatform` | Service 实现 |
| YZH.Core Workflow | `YZH.Core.Workflow` | 工作流引擎 |
| YZH.Core Queue | `YZH.Core.Queue` | 队列引擎 |
| YZH.Core AI | `YZH.Core.AI` | AI 客户端 |
| YZH.Core Extractor | `YZH.Core.Extractor` | 文件解析 |

### 5.2 using 语句规范

```csharp
// ✅ 正确：使用新命名空间
using VOL.CERT.IRepositories.CertPlatform;
using VOL.CERT.IServices.CertPlatform;
using VOL.CERT.Services.CertPlatform;

// ❌ 错误：使用旧命名空间
using VOL.Builder.IRepositories.CertPlatform;
using VOL.Builder.IServices.CertPlatform;
using VOL.Builder.Services.CertPlatform;
```

---

## 六、代码编写规范

### 6.1 Repository 编写规范

```csharp
// 1. 接口定义（IRepositories 目录）
namespace VOL.CERT.IRepositories.CertPlatform.Cert
{
    public partial interface IISOStandardRepository : IRepository<ISOStandard>
    {
    }
}

// 2. 实现类（Repositories 目录）
namespace VOL.CERT.Repositories.CertPlatform.Cert
{
    public partial class ISOStandardRepository : RepositoryBase<ISOStandard>, 
        IISOStandardRepository, IDependency
    {
        [ActivatorUtilitiesConstructor]
        public ISOStandardRepository(VOLContext dbContext)
            : base(dbContext)
        {
        }

        public static IISOStandardRepository Instance
        {
            get { return AutofacContainerModule.GetService<IISOStandardRepository>(); }
        }
    }
}
```

### 6.2 Service 编写规范

```csharp
// 1. 接口定义（IServices 目录）
namespace VOL.CERT.IServices.CertPlatform.Cert
{
    public partial interface IISOStandardService : IService<ISOStandard>
    {
    }
}

// 2. 实现类（Services 目录）
namespace VOL.CERT.Services.CertPlatform.Cert
{
    public partial class ISOStandardService : ServiceBase<ISOStandard, IISOStandardService>, 
        IISOStandardService, IDependency
    {
        public ISOStandardService(IISOStandardRepository repository)
            : base(repository)
        {
        }
    }
}
```

### 6.3 Controller 编写规范

```csharp
namespace VOL.WebApi.Controllers.CertPlatform
{
    [Route("api/[controller]")]
    [ApiController]
    [JWTAuthorize]
    public class ISOStandardController : ApiBaseController<ISOStandard>
    {
        private readonly IISOStandardService _service;

        public ISOStandardController(IISOStandardService service)
            : base(service)
        {
            _service = service;
        }
    }
}
```

---

## 七、依赖关系规范

### 7.1 允许的依赖关系

```
VOL.WebApi → VOL.CERT (业务)
VOL.WebApi → YZH.Core (架构)
VOL.WebApi → VOL.Core (基础设施)
VOL.WebApi → VOL.Entity (实体)

VOL.CERT → YZH.Core (架构)
VOL.CERT → VOL.Core (基础设施)
VOL.CERT → VOL.Entity (实体)

YZH.Core → VOL.Core (基础设施)
YZH.Core → VOL.Entity (实体，仅基类)
```

### 7.2 禁止的依赖关系

```
❌ YZH.Core → VOL.CERT (架构不能依赖业务)
❌ VOL.CERT → VOL.WebApi (业务不能依赖 API 层)
❌ VOL.Core → YZH.Core (基础设施不能依赖架构)
```

---

## 八、新功能开发检查清单

### 8.1 开发前

- [ ] 确认功能属于哪个业务子模块（Audit/Cert/DocExtraction/Ent/Rpt/Wf/Infra）
- [ ] 确认是否需要新的 Repository/Service
- [ ] 确认是否需要扩展 YZH.Core 架构能力

### 8.2 开发中

- [ ] Repository 接口放在 `VOL.CERT/IRepositories/CertPlatform/{子模块}/`
- [ ] Repository 实现放在 `VOL.CERT/Repositories/CertPlatform/{子模块}/`
- [ ] Service 接口放在 `VOL.CERT/IServices/CertPlatform/{子模块}/`
- [ ] Service 实现放在 `VOL.CERT/Services/CertPlatform/{子模块}/`
- [ ] Controller 放在 `VOL.WebApi/Controllers/CertPlatform/`
- [ ] 使用正确的命名空间

### 8.3 开发后

- [ ] 编译通过（0 errors）
- [ ] 更新相关 README 文档
- [ ] 如有新模块，更新本文档

---

## 九、常见错误对照表

| 错误 | 原因 | 解决方案 |
|------|------|----------|
| `CS0234: 命名空间"VOL.Builder"中不存在类型` | 使用了旧命名空间 | 改为 `VOL.CERT.*` |
| `CS0246: 未能找到类型或命名空间名` | 缺少 using 引用 | 添加正确的 using 语句 |
| `CS0234: 命名空间"VOL"中不存在类型或命名空间名"Entity"` | 缺少 VOL.Entity 引用 | 在 csproj 中添加引用 |
| 循环依赖编译错误 | 依赖方向错误 | 检查 §7 依赖关系规范 |

---

**文档版本**：V1.0  
**创建时间**：2026-08-25  
**最后更新**：2026-08-25  
**更新内容**：初始版本，定义后端架构分层、模块划分、代码存放规则
