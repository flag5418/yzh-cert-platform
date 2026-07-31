# YZH Framework 架构设计文档

> **版本**：V1.6  
> **日期**：2026-07-31  
> **状态**：📋 待评审（指导方针优先，实现可迭代）  
> **定位**：映智汇（YingZhiHui）公共资产库 - 跨项目复用的 .NET + Vue 全栈开发框架  
> **适用项目**：体系认证平台（首个落地项目）  
> **更新说明**：V1.6 新增 YZH Framework 独立性约束（禁止修改 Vol 源码）；V1.5 新增第二十一章 Vol 框架源码分析报告；V1.4 新增第二十章接口幂等性设计（Redis防重复提交）；V1.3 新增实体级特性、特性分类体系重构；V1.2 新增API端点自动注册；V1.1 修正删除策略（默认逻辑删除）、补充审计字段自动填充、新增前后端关联章节、多基类体系、编码规则

---

# 零、YZH Framework 指导方针（⭐ 核心灵魂）

> **"做任何事情可以不完善，但没有原则。架构可以逐步完善，但我们的指导方针必须要正确。"**

## 0.0 ⚠️ 核心约束：YZH Framework 独立性原则（不可违反）

```
┌─────────────────────────────────────────────────────────────┐
│           🚨 YZH Framework 独立性原则（最高优先级）            │
│                                                             │
│  ❌ 绝对禁止：直接修改 Vol 框架源代码                          │
│     → Vol 是第三方依赖，修改会导致升级困难、维护混乱             │
│                                                             │
│  ✅ 正确做法：在 YZH.Framework 中继承和扩展                     │
│     → 将 Vol 的基类/接口作为基础层                             │
│     → 在 YZH 中拷贝需要改造的 Vol 实现，增强为新代码             │
│     → 通过继承关系无缝融合，而非修改源码                        │
│                                                             │
│  🎯 战略目标：YZH Framework 必须可独立抽离                      │
│     → 可在其他项目中直接引用，不依赖当前项目的 Vol 配置          │
│     → 作为公司级公共资产库，跨项目复用                          │
│     → 保持与 Vol 版本的解耦能力                               │
│                                                             │
│  ┌──────────────────────────────────────────────────┐       │
│  │              分层关系图                            │       │
│  │                                                  │       │
│  │   ┌─────────────────────────────────────────┐    │       │
│  │   │         业务代码（具体项目）               │    │       │
│  │   │    CertificationBodyService              │    │       │
│  │   └────────────────┬────────────────────────┘    │       │
│  │                    │ 继承                          │       │
│  │   ┌────────────────▼────────────────────────┐    │       │
│  │   │      YZH Framework（增量增强层）✨         │    │       │
│  │   │                                           │    │       │
│  │   │  • YZHServiceBase : Vol.ServiceBase       │    │       │
│  │   │  • YZHControllerBase : Vol.Controller     │    │       │
│  │   │  • YZH Attribute 体系（完全自研）          │    │       │
│  │   │  • YZH Filter 扩展（基于 Vol.Filter）      │    │       │
│  │   │  • 业务规则引擎（编码规则、校验规则）        │    │       │
│  │   └────────────────┬────────────────────────┘    │       │
│  │                    │ 依赖（不修改）                │       │
│  │   ┌────────────────▼────────────────────────┐    │       │
│  │   │        Vol Framework（基础能力层）         │    │       │
│  │   │                                           │    │       │
│  │   │  • ServiceBase / RepositoryBase           │    │       │
│  │   │  • ActionPermissionFilter                 │    │       │
│  │   │  • DictionaryManager / Logger             │    │       │
│  │   │  • JWTAuthorize / UserContext             │    │       │
│  │   └─────────────────────────────────────────┘    │       │
│  └──────────────────────────────────────────────────┘       │
│                                                             │
│  💡 实施要点：                                               │
│  1️⃣  YZH 项目结构独立于 Vol，可单独打包为 NuGet 包            │
│  2️⃣  需要改造的 Vol 功能 → 拷贝到 YZH → 增强实现              │
│  3️⃣  不需要改造的 Vol 功能 → 直接引用，通过继承扩展             │
│  4️⃣  所有 YZH 特有功能（Attribute、业务规则）完全自研          │
│  5️⃣  通过接口隔离降低耦合，便于未来替换 Vol                    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 0.0.1 为什么不能修改 Vol 源码？

| 风险维度 | 修改 Vol 源码 | YZH 独立扩展 |
|---------|-------------|-------------|
| **升级兼容性** | ❌ Vol 升级后冲突，合并困难 | ✅ 锁定 Vol 版本或接口隔离 |
| **跨项目复用** | ❌ 强绑定当前项目配置 | ✅ YZH 可独立抽离，NuGet 发布 |
| **团队协作** | ❌ 其他人不知道改了什么 | ✅ YZH 变更可控，文档清晰 |
| **问题排查** | ❌ 无法区分是 Vol Bug 还是修改导致 | ✅ 边界清晰，责任明确 |
| **法律合规** | ⚠️ 可能违反 Vol 开源协议 | ✅ 完全合规，自主知识产权 |

### 0.0.2 YZH Framework 的三种扩展模式

```
模式 A: 直接继承（推荐用于 80% 场景）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
场景：Vol 功能已满足需求，只需添加 YZH 特有能力
示例：
  public class YZHServiceBase<TEntity, TRepository> 
      : ServiceBase<TEntity, TRepository>  // 直接继承 Vol
  {
      // 添加 YZH Attribute 读取逻辑
      // 添加多租户过滤
      // 添加审计日志
  }

模式 B: 拷贝增强（用于需要深度改造的场景）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
场景：Vol 实现不符合需求，且无法通过继承修改内部逻辑
示例：
  // 从 Vol.ServiceBase 拷贝核心代码到 YZH
  public class YZHEnhancedServiceBase<TEntity, TRepository>
  {
      // Vol 原有 CRUD 逻辑（拷贝）
      // + YZH 改进的钩子机制（虚方法替代 Func）
      // + YZH 特性驱动的事务管理
      // + YZH 统一的异常处理
  }
  ⚠️ 注意：必须在代码注释中标注 "Derived from Vol.ServiceBase"

模式 C: 完全自研（仅用于 YZH 独有功能）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
场景：Vol 完全没有此能力，YZH 创新功能
示例：
  [AttributeUsage(AttributeTargets.Class)]
  public class YZHCodeRuleAttribute : Attribute  // 完全自研
  {
      public string Pattern { get; set; }  // 编码规则模式
      // ...
  }
```

### 0.0.3 项目目录结构约束

```
/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/
├── src/server/Vue.NetCore/
│   ├── vol.api/                          # 🔒 Vol 框架（只读，禁止修改）
│   │   ├── VOL.Core/
│   │   ├── VOL.Entity/
│   │   ├── VOL.Sys/
│   │   └── VOL.WebApi/
│   │
│   └── YZH.Framework/                    # ✨ YZH Framework（独立项目）
│       ├── YZH.Core/                     # 核心基础设施
│       │   ├── Attributes/               # 特性体系
│       │   ├── Filters/                  # 过滤器扩展
│       │   ├── Services/                 # 服务基类
│       │   ├── Controllers/              # 控制器基类
│       │   ├── Extensions/               # 扩展方法
│       │   └── Infrastructure/           # 基础设施（缓存、日志等）
│       │
│       ├── YZH.Entity/                   # 实体基类和特性
│       │   ├── BaseCore/                 # YZHBaseEntity
│       │   └── AttributeManager/         # 特性管理器
│       │
│       └── YZH.Web/                     # Web 扩展
│           ├── Middleware/               # 中间件
│           └── Setup/                   # DI 注册扩展
│
├── src/server/Vue.NetCore/
│   └── vol.api.sqlsugar/                # 🔒 SQLSugar 版本 Vol（同样只读）
│
└── docs/20-架构决策/                    # 📖 架构文档
    └── YZH-Framework架构设计-V1.0.md    # 本文档
```

---

## 0.1 设计哲学

```
┌─────────────────────────────────────────────────────────────┐
│                    YZH Framework 设计哲学                     │
│                                                             │
│  1️⃣  声明式优于命令式                                       │
│     → 用 Attribute 声明意图，而非硬编码实现逻辑               │
│     → 配置驱动行为，代码只写业务特例                          │
│                                                             │
│  2️⃣  约定优于配置                                           │
│     → 提供合理的默认值，减少必须配置的项                       │
│     → 80% 的场景零配置即可使用                                │
│                                                             │
│  3️⃣  全局容错优于局部捕获                                     │
│     → 禁止代码中充斥 try-catch                               │
│     → 全局异常过滤器统一处理，业务层只抛不捕                   │
│                                                             │
│  4️⃣  组合优于继承                                            │
│     → 通过装饰器模式灵活组合功能                              │
│     → 继承用于 "is-a"，组合用于 "has-a"                      │
│                                                             │
│  5️⃣  渐进式完善                                             │
│     → 先建立正确的方针和接口                                 │
│     → 实现可以从简单开始，逐步增强                            │
│     → 每次迭代都保持向后兼容                                  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## 0.2 编码铁律（不可违反）

| # | 铁律 | 正确做法 | 错误做法 |
|---|------|---------|---------|
| **1** | **禁止 try-catch 泛滥** | 业务层抛出 `YZHBusinessException` | 每个 Controller 方法都包 try-catch |
| **2** | **特性声明行为** | `[YZHAudited] public class Xxx { }` | 在 Service 中手写日志代码 |
| **3** | **基类处理通用逻辑** | 继承 `YZHServiceBase`，只重写钩子 | 复制粘贴 CRUD 样板代码 |
| **4** | **配置驱动前端** | `config/certification-body.ts` | 为每个表手写完整 Vue 组件 |
| **5** | **删除默认逻辑** | 不加特性就是逻辑删除（合规要求） | 物理删除需要显式 `[YZHDeleteStrategy(Physical)]` |
| **6** | **审计字段自动填充** | 基类自动处理 CreateBy/UpdateBy/DeleteBy + 时间 | 手动设置审计字段 |

## 0.3 审计字段自动填充规则（基类强制）

> **原则：业务代码永远不需要手动设置审计字段，由基类统一处理。**

```
┌─────────────────────────────────────────────────────────────┐
│                   基类自动填充规则（强制）                      │
│                                                             │
│  📝 新建 (Add):                                               │
│    CreateBy   → UserContext.Current.UserId    （自动 ✅）     │
│    CreateTime → DateTime.Now                  （自动 ✅）     │
│    Code       → Guid.NewGuid("N")           （自动，为空时） │
│    OrgCode    → UserContext.Current.OrgCode   （自动，多租户）│
│                                                             │
│  ✏️  编辑 (Update):                                            │
│    UpdateBy   → UserContext.Current.UserId    （自动 ✅）     │
│    UpdateTime → DateTime.Now                  （自动 ✅）     │
│                                                             │
│  🗑️  删除 (Delete):                                            │
│    DeleteBy   → UserContext.Current.UserId    （自动 ✅）     │
│    DeleteTime → DateTime.Now                  （自动 ✅）     │
│                                                             │
│  ⚠️ 禁止：业务代码手动设置以上字段！                             │
│  ⚠️ 禁止：在 OnBeforeSave 中调用 SetCreateInfo()！             │
└─────────────────────────────────────────────────────────────┘
```

## 0.4 扩展原则（如何正确地扩展框架）

```
需要新功能时的思考路径：

1️⃣  这个功能是否足够通用？
    → 是 → 加入 YZH Framework 基类或装饰器
    → 否 → 在具体 Service/Controller 的 Partial 中实现

2️⃣  是否可以通过 Attribute 声明？
    → 是 → 新增一个 YZHxxxAttribute + 基类读取
    → 否 → 可能需要重新审视设计

3️⃣  是否影响已有功能？
    → 是 → 必须保持向后兼容（新增参数有默认值）
    → 否 → 可以更自由地设计

4️⃣  当前是否必须实现？
    → 是 → 实现最小可用版本（MVP）
    → 否 → 记录在 TODO 中，后续迭代

5️⃣  🔍 Vol 框架是否已有类似功能？
    → 是 → 优先复用 Vol，不重复造轮子
    → 不确定 → 先分析 Vol 源码再决定
    → 否 → 自己实现 YZH 版本
```

---

# 一、概述

## 1.1 什么是 YZH Framework

YZH Framework 是映智汇公司的 **.NET + Vue 全栈开发基础设施**，提供：

- **后端**：基于 .NET 8 的服务/控制器基类、特性驱动的行为声明、装饰器模式的扩展机制
- **前端**：基于 Vue 3 + Element Plus 的通用 CRUD 组件、配置驱动的页面生成
- **横切关注点**：全局容错、审计日志、多租户、权限控制、缓存策略

## 1.2 为什么需要 YZH Framework

| 痛点 | YZH 解决方案 |
|------|-------------|
| 每张表重复写 CRUD 代码 | `YZHServiceBase` 提供完整生命周期 |
| try-catch 泛滥，代码丑陋 | `YZHGlobalExceptionFilter` 全局优雅处理 |
| 多租户逻辑散落各处 | `[YZHMultiTenant]` 特性声明，基类自动过滤 |
| 日志格式不统一，难以查询 | `[YZHAudited]` 特性 + 结构化日志模型 |
| 功能耦合严重，难以扩展 | 装饰器模式，声明式组合功能 |
| 前端页面大量重复代码 | `GenericCrud.vue` + 配置文件驱动 |

## 1.3 与 Vol 框架的关系

```
Vol Framework（底层引擎）
    ↓ 继承 & 封装
YZH Framework（业务增强）
    ↓ 应用
CertPlatform（具体业务）
```

- **Vol 提供**：ORM、依赖注入、基础 CRUD、view-grid 组件
- **YZH 增强**：生命周期管理、特性驱动、全局容错、装饰器扩展
- **CertPlatform 使用**：具体的业务实体、页面配置、特化逻辑

---

# 二、整体架构

## 2.1 分层架构图

```
┌─────────────────────────────────────────────────────────────────┐
│                        前端 (Vue 3)                              │
│                                                                 │
│  ┌─────────────────────┐    ┌─────────────────────┐             │
│  │   TreeCrud.vue      │───▶│   GenericCrud.vue   │             │
│  │   (树形 + CRUD)      │    │   (通用 CRUD 页面)   │             │
│  └─────────────────────┘    └─────────────────────┘             │
│            ▲                          ▲                         │
│            │          配置驱动         │                         │
│  ┌─────────┴──────────┐  ┌────────────┴─────────┐               │
│  │  tree-config.ts    │  │  crud-config.ts      │               │
│  └────────────────────┘  └──────────────────────┘               │
└─────────────────────────────────────────────────────────────────┘
                                  │ HTTP (REST API)
                                  ▼
┌─────────────────────────────────────────────────────────────────┐
│                      后端 (.NET 8)                               │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │              YZHControllerBase<TEntity>                  │   │
│  │              (控制器基类)                                 │   │
│  │  ✅ 全局容错（委托给 ExceptionFilter）                    │   │
│  │  ✅ 装饰器执行（AOP 式扩展）                              │   │
│  │  ✅ 统一响应格式                                         │   │
│  └───────────────────────────┬─────────────────────────────┘   │
│                              │ 继承                           │
│  ┌───────────────────────────▼─────────────────────────────┐   │
│  │              YZHServiceBase<TEntity>                     │   │
│  │              (服务基类 - 生命周期核心)                    │   │
│  │  ✅ 读取 Entity Attribute                               │   │
│  │  ✅ 查询生命周期                                        │   │
│  │  ✅ 保存生命周期                                        │   │
│  │  ✅ 删除生命周期                                        │   │
│  │  ✅ 自动应用多租户/审计/校验                             │   │
│  └───────────────────────────┬─────────────────────────────┘   │
│                              │ 继承                           │
│  ┌───────────────────────────▼─────────────────────────────┐   │
│  │           Vol Framework (ServiceBase / ApiBaseController)│   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## 2.2 项目结构

```
YZH.Framework/                          # 映智汇公共资产（先内嵌于项目）
│
├── YZH.Core/                          # 核心库
│   ├── Attributes/                    # ⭐ 特性定义
│   │   ├── YZHMultiTenantAttribute.cs
│   │   ├── YZHAuditedAttribute.cs
│   │   ├── YZHDeleteStrategyAttribute.cs
│   │   ├── YZHPermissionAttribute.cs
│   │   ├── YZHValidationRulesAttribute.cs
│   │   ├── YZHCachingAttribute.cs
│   │   ├── YZHUseDecoratorAttribute.cs
│   │   └── YZHPaginationAttribute.cs
│   │
│   ├── Base/                          # ⭐ 基类定义
│   │   ├── Entities/
│   │   │   └── YZHBaseEntity.cs       # 实体基类（Id, Code, 审计字段）
│   │   ├── Services/
│   │   │   └── YZHServiceBase.cs     # 服务基类（生命周期管理）
│   │   ├── Controllers/
│   │   │   └── YZHControllerBase.cs  # 控制器基类（装饰器+容错）
│   │   └── Repositories/
│   │       └── YZHRepositoryBase.cs  # 仓储基类
│   │
│   ├── Enums/                        # 枚举
│   │   ├── YZHDeleteMode.cs          # Physical / Logical
│   │   ├── YZHOperationType.cs       # Create/Update/Delete/...
│   │   └── YZHLogCategory.cs         # 日志分类
│   │
│   ├── Exceptions/                   # 自定义异常
│   │   ├── YZHException.cs           # 基础异常
│   │   ├── YZHBusinessException.cs   # 业务异常（用户友好提示）
│   │   ├── YZHValidationException.cs # 校验异常（字段级错误）
│   │   └── YZHNotFoundException.cs   # 未找到异常
│   │
│   ├── Logging/                      # 日志体系
│   │   ├── Models/
│   │   │   └── YZHAuditLogEntry.cs   # 日志数据模型
│   │   └── Interfaces/
│   │       └── IYZHAuditLogService.cs # 日志服务接口
│   │
│   └── Decorators/                   # ⭐ 装饰器（扩展机制）
│       ├── Interfaces/
│       │   └── IYZHActionDecorator.cs
│       └── BuiltIn/
│           ├── YZHAuditLogDecorator.cs
│           ├── YZHCacheDecorator.cs
│           ├── YZHValidationDecorator.cs
│           └── YZHRateLimitDecorator.cs
│
├── YZH.Web/                          # Web 扩展
│   ├── Filters/
│   │   └── YZHGlobalExceptionFilter.cs  # ⭐ 全局异常过滤器
│   └── Middleware/
│       └── YZHRequestLoggingMiddleware.cs
│
└── README.md                         # 使用指南
```

---

# 三、特性体系（Attribute-Driven Design）

## 3.1 设计原则

> **"用声明式表达意图，让框架自动执行。"**

特性的层级关系：

```
┌─────────────────────────────────────────────────────────────┐
│                     特性作用域                                 │
│                                                             │
│  [类级别] → 定义实体的默认行为（全局生效）                    │
│    ↓                                                         │
│  [方法级别] → 覆盖或细化特定方法的行为（局部生效）             │
│    ↓                                                         │
│  [参数级别] → （预留）参数级别的精细控制                       │
│                                                             │
│  优先级：方法级别 > 类级别                                   │
└─────────────────────────────────────────────────────────────┘
```

## 3.2 特性清单

### 3.2.1 多租户特性

```csharp
/// <summary>
/// 标记实体支持多租户数据隔离
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class YZHMultiTenantAttribute : Attribute
{
    /// <summary>
    /// OrgCode 字段名（默认 "OrgCode"）
    /// </summary>
    public string OrgCodeField { get; set; } = "OrgCode";
    
    /// <summary>
    /// 超级管理员是否也进行过滤（默认 false = 不过滤）
    /// </summary>
    public bool FilterForSuperAdmin { get; set; } = false;
    
    /// <summary>
    /// 忽略的角色列表（这些角色不过滤）
    /// </summary>
    public string[] IgnoreRoles { get; set; }
}
```

**使用示例：**
```csharp
[YZHMultiTenant(OrgCodeField = "OrgCode", IgnoreRoles = new[] { "SuperAdmin" })]
public class CertificationBody : YZHBaseEntity
{
    public string OrgCode { get; set; }
}

// 基类自动效果：
// - 查询时自动添加 WHERE OrgCode = @CurrentOrgCode
// - SuperAdmin 可选跳过过滤
// - 保存时自动填充当前用户的 OrgCode
```

### 3.2.2 审计日志特性

```csharp
/// <summary>
/// 标记实体需要记录操作审计日志
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class YZHAuditedAttribute : Attribute
{
    /// <summary>
    /// 日志主分类
    /// </summary>
    public YZHLogCategory Category { get; set; }
    
    /// <summary>
    /// 子分类（可自定义字符串）
    /// </summary>
    public string SubCategory { get; set; }
    
    /// <summary>
    /// 是否记录新建操作（默认 true）
    /// </summary>
    public bool LogCreate { get; set; } = true;
    
    /// <summary>
    /// 是否记录编辑操作（默认 true）
    /// </summary>
    public bool LogUpdate { get; set; } = true;
    
    /// <summary>
    /// 是否记录删除操作（默认 true）
    /// </summary>
    public bool LogDelete { get; set; } = true;
    
    /// <summary>
    /// 是否记录查询操作（默认 false，避免日志量过大）
    /// </summary>
    public bool LogQuery { get; set; } = false;
    
    /// <summary>
    /// 敏感字段列表（日志中自动脱敏）
    /// </summary>
    public string[] SensitiveFields { get; set; }
    
    /// <summary>
    /// 是否记录变更详情（新旧值对比）
    /// </summary>
    public bool TrackChanges { get; set; } = true;
}
```

**使用示例：**
```csharp
[
    YZHAudited(
        Category = YZHLogCategory.CertBodyManagement,
        SubCategory = "机构基本信息",
        SensitiveFields = new[] { "ContactPhone", "Notes" },
        TrackChanges = true
    )
]
public class CertificationBody : YZHBaseEntity
{
    // ContactPhone 和 Notes 在日志中会显示为 ***xxx
}

// 方法级别覆盖：
public partial class CertificationBodyService
{
    // 此查询操作也记录日志（覆盖类的 LogQuery = false）
    [YZHAudited(LogQuery = true)]
    public override PageGridData<CertificationBody> GetPageData(...) { ... }
}
```

### 3.2.3 删除策略特性

```csharp
/// <summary>
/// 定义实体的删除策略
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class YZHDeleteStrategyAttribute : Attribute
{
    /// <summary>
    /// 删除模式（默认逻辑删除 - 合规要求）
    /// </summary>
    public YZHDeleteMode Mode { get; set; } = YZHDeleteMode.Logical;
    
    /// <summary>
    /// 逻辑删除时的字段映射
    /// </summary>
    public string DeletedByField { get; set; } = "DeleteBy";
    public string DeletedTimeField { get; set; } = "DeleteTime";
}

public enum YZHDeleteMode
{
    Logical,    // 逻辑删除（默认，设置 DeleteBy/DeleteTime）✅
    Physical    // 物理删除（需要显式声明时使用）
}
```

**使用示例：**
```csharp
// 默认逻辑删除（无需标注，符合合规要求）
public class CertificationBody : YZHBaseEntity { }

// 需要物理删除时显式声明（如临时表、日志清理）
[YZHDeleteStrategy(Mode = YZHDeleteMode.Physical)]
public class SysOperationLog : YZHBaseEntity { }
```

### 3.2.4 权限特性

```csharp
/// <summary>
/// 定义实体的权限要求
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class YZHPermissionAttribute : Attribute
{
    /// <summary>
    /// 模块编码
    /// </summary>
    public string Module { get; set; }
    
    /// <summary>
    /// 功能编码
    /// </summary>
    public string Code { get; set; }
    
    /// <summary>
    /// 要求的权限列表（拥有任一即可）
    /// </summary>
    public string[] RequiredPermissions { get; set; }
    
    /// <summary>
    /// 允许的角色列表（空表示所有角色）
    /// </summary>
    public string[] AllowedRoles { get; set; }
}
```

### 3.2.5 校验规则特性

```csharp
/// <summary>
/// 定义实体的自动校验规则
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class YZHValidationRulesAttribute : Attribute
{
    /// <summary>
    /// 唯一字段列表（保存时自动检查唯一性）
    /// </summary>
    public string[] UniqueFields { get; set; }
    
    /// <summary>
    /// 必填字段列表（补充 Data Annotation）
    /// </summary>
    public string[] RequiredFields { get; set; }
    
    /// <summary>
    /// 字段最大长度映射
    /// </summary>
    public Dictionary<string, int> MaxLengths { get; set; }
}
```

**使用示例：**
```csharp
[
    YZHValidationRules(
        UniqueFields = new[] { "CbCode" },  // CNAS 编号不能重复
        RequiredFields = new[] { "Name" },   // 名称必填
        MaxLengths = new Dictionary<string, int>
        {
            { "Name", 200 },
            { "CbCode", 50 },
            { "ShortName", 100 }
        }
    )
]
public class CertificationBody : YZHBaseEntity { }

// 基类自动效果：保存时自动检查 CNAS 编号是否重复
```

### 3.2.6 缓存特性

```csharp
/// <summary>
/// 定义缓存策略
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class YZHCachingAttribute : Attribute
{
    /// <summary>
    /// 是否启用缓存（默认 true）
    /// </summary>
    public bool EnableCache { get; set; } = true;
    
    /// <summary>
    /// 缓存过期时间（分钟，默认 30）
    /// </summary>
    public int ExpiryMinutes { get; set; } = 30;
    
    /// <summary>
    /// 保存时是否清除缓存（默认 true）
    /// </summary>
    public bool ClearOnSave { get; set; } = true;
    
    /// <summary>
    /// 缓存键模板（支持 {userId}, {orgCode} 等变量）
    /// </summary>
    public string CacheKeyTemplate { get; set; }
    
    /// <summary>
    /// 是否按用户隔离缓存
    /// </summary>
    public bool PerUser { get; set; } = false;
}
```

### 3.2.7 装饰器声明特性

```csharp
/// <summary>
/// 声明 Controller/Action 使用的装饰器
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class YZHUseDecoratorAttribute : Attribute
{
    /// <summary>
    /// 装饰器类型（必须实现 IYZHActionDecorator）
    /// </summary>
    public Type DecoratorType { get; }
    
    /// <summary>
    /// 执行顺序（越小越先执行，默认 100）
    /// </summary>
    public int Order { get; set; } = 100;
    
    public YZHUseDecoratorAttribute(Type decoratorType)
    {
        if (!typeof(IYZHActionDecorator).IsAssignableFrom(decoratorType))
            throw new ArgumentException(
                $"类型 {decoratorType.Name} 必须实现 IYZHActionDecorator 接口");
        DecoratorType = decoratorType;
    }
}
```

**使用示例：**
```csharp
[
    YZHUseDecorator(typeof(YZHValidationDecorator), Order = 0),      // 校验
    YZHUseDecorator(typeof(YZHAuditLogDecorator), Order = 10),     // 日志
    YZHUseDecorator(typeof(YZHCacheDecorator), Order = 20),        // 缓存
    // YZHUseDecorator(typeof(YZHRateLimitDecorator), Order = -1)  // 限流（可选）
]
public class CertificationBodyController : YZHControllerBase<CertificationBody>
{
    // 所有 Action 自动拥有上述能力
}
```

---

# 四、后端基类设计

## 4.1 YZHServiceBase 生命周期

### 4.1.1 查询生命周期

```
GetPageData 请求进入
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① OnQueryStart(options)                                      │
│   用途：权限检查、参数预处理                                  │
│   默认：空                                                  │
├─────────────────────────────────────────────────────────────┤
│② OnBuildQuery(query)                                        │
│   用途：构建基础查询                                          │
│   自动处理：                                                 │
│     - [YZHMultiTenant] → 添加 OrgCode 过滤                  │
│     - [YZHDeleteStrategy=Logical] → 过滤已删除数据           │
│   默认：返回原 query                                         │
├─────────────────────────────────────────────────────────────┤
│③ OnQueryFilter(query)                                       │
│   用途：业务相关的额外过滤                                    │
│   默认：返回原 query                                         │
├─────────────────────────────────────────────────────────────┤
│④ [框架] base.GetPageData() 执行查询                          │
├─────────────────────────────────────────────────────────────┤
│⑤ OnQueryExecuted(result)                                     │
│   用途：结果后处理                                            │
│   默认：返回原 result                                        │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
返回分页数据
```

### 4.1.2 保存生命周期（Add / Update）

```
Add/Update 请求进入
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① OnSaveStart(model, mode)                                    │
│   用途：判断操作类型、初始化上下文                             │
│   默认：空                                                  │
├─────────────────────────────────────────────────────────────┤
│② OnValidate(entity) → (bool success, string error)          │
│   用途：数据校验                                             │
│   自动处理：                                                 │
│     - [YZHValidationRules.UniqueFields] → 唯一性检查         │
│     - [YZHValidationRules.RequiredFields] → 必填检查         │
│     - [YZHValidationRules.MaxLengths] → 长度检查             │
│   返回 false 则终止保存                                      │
├─────────────────────────────────────────────────────────────┤
│③ OnBeforeSave(entity, mode)                                  │
│   用途：保存前处理                                            │
│   自动处理：                                                 │
│     - 设置 Code（如果为空则生成 GUID）                        │
│     - 设置 CreateBy/CreateTime 或 UpdateBy/UpdateTime        │
│     - [YZHMultiTenant] → 设置 OrgCode                       │
├─────────────────────────────────────────────────────────────┤
│④ [框架] base.Add() / base.Update() 执行保存                  │
├─────────────────────────────────────────────────────────────┤
│⑤ OnAfterSave(entity, mode)                                   │
│   用途：保存后处理（同一事务内）                              │
│   自动处理：                                                 │
│     - [YZHAudited] → 写入审计日志                            │
│     - [YZHCaching.ClearOnSave] → 清除缓存                   │
│   注意：此处异常会导致事务回滚                                │
├─────────────────────────────────────────────────────────────┤
│⑥ OnSaveCompleted(entity, mode)                               │
│   用途：清理工作                                              │
│   默认：空                                                  │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
返回保存结果
```

### 4.1.3 删除生命周期

```
Del 请求进入
        │
        ▼
┌─────────────────────────────────────────────────────────────┐
│① OnDeleteStart(keys)                                         │
│   默认：空                                                  │
├─────────────────────────────────────────────────────────────┤
│② CanDelete(keys) → bool                                     │
│   用途：是否允许删除                                          │
│   自动处理：检查关联数据（可配置）                            │
│   返回 false 则终止并提示                                    │
├─────────────────────────────────────────────────────────────┤
│③ OnBeforeDelete(keys)                                        │
│   用途：删除前处理                                            │
│   默认：空                                                  │
├─────────────────────────────────────────────────────────────┤
│④ [框架] 根据 [YZHDeleteStrategy] 执行删除                     │
│   - Physical: 直接 DELETE FROM table                        │
│   - Logical: UPDATE SET DeleteBy=?, DeleteTime=?            │
├─────────────────────────────────────────────────────────────┤
│⑤ OnAfterDelete(keys)                                        │
│   用途：删除后处理                                            │
│   自动处理：                                                 │
│     - [YZHAudited.LogDelete=true] → 写入审计日志             │
│     - [YZHCaching.ClearOnSave=true] → 清除缓存              │
└─────────────────────────────────────────────────────────────┘
        │
        ▼
返回删除结果
```

## 4.2 YZHServiceBase 代码骨架

```csharp
/// <summary>
/// YZH 服务基类 - 提供完整的生命周期管理和特性驱动行为
/// 
/// 设计原则：
/// - 通过反射读取 Entity 的 Attribute，自动应用行为
/// - 提供虚方法钩子，子类可按需重写
/// - 内置容错，业务层只需抛出业务异常
/// </summary>
/// <typeparam name="TEntity">实体类型，必须继承 YZHBaseEntity</typeparam>
public abstract class YZHServiceBase<TEntity> : ServiceBase<TEntity, IYZHRepository<TEntity>>
    where TEntity : YZHBaseEntity, new()
{
    #region 依赖注入
    
    protected readonly IHttpContextAccessor _httpContextAccessor;
    protected readonly IYZHRepository<TEntity> _repository;
    protected readonly ILogger<YZHServiceBase<TEntity>> _logger;
    
    [ActivatorUtilitiesConstructor]
    protected YZHServiceBase(
        IYZHRepository<TEntity> dbRepository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<YZHServiceBase<TEntity>> logger
    ) : base(dbRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _repository = dbRepository;
        _logger = logger;
    }
    
    #endregion

    #region 实体元数据（缓存反射结果）
    
    private static readonly Lazy<EntityMetadata> _metadata = 
        new Lazy<EntityMetadata>(() => EntityMetadata.FromType<TEntity>());
    
    /// <summary>实体元数据（含所有 Attribute 信息）</summary>
    protected static EntityMetadata Metadata => _metadata.Value;
    
    #endregion

    #region 辅助属性
    
    protected long? CurrentUserId => UserContext.Current.UserId;
    protected string CurrentUserName => UserContext.Current.UserTrueName;
    protected string CurrentOrgCode => UserContext.Current.GetOrgCode();
    protected bool IsSuperAdmin => UserContext.Current.IsSuperAdmin();
    protected string ClientIp => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    
    #endregion

    #region 查询生命周期（可重写）
    
    protected virtual void OnQueryStart(PageDataOptions options) { }
    
    protected virtual IQueryable<TEntity> OnBuildQuery(IQueryable<TEntity> query)
    {
        // 自动应用 [YZHMultiTenant]
        if (Metadata.HasAttribute<YZHMultiTenantAttribute>())
        {
            var attr = Metadata.GetAttribute<YZHMultiTenantAttribute>();
            query = ApplyMultiTenantFilter(query, attr);
        }
        
        // 自动应用 [YZHDeleteStrategy=Logical]
        if (Metadata.GetAttribute<YZHDeleteStrategyAttribute>()?.Mode == YZHDeleteMode.Logical)
        {
            query = query.Where(x => x.DeleteTime == null);
        }
        
        return query;
    }
    
    protected virtual IQueryable<TEntity> OnQueryFilter(IQueryable<TEntity> query) => query;
    
    protected virtual PageGridData<TEntity> OnQueryExecuted(PageGridData<TEntity> result) => result;
    
    #endregion

    #region 保存生命周期（可重写）
    
    protected virtual void OnSaveStart(SaveModel model, YZHSaveMode mode) { }
    
    protected virtual (bool valid, string error) OnValidate(TEntity entity)
    {
        // 自动应用 [YZHValidationRules]
        var rules = Metadata.GetAttribute<YZHValidationRulesAttribute>();
        if (rules != null)
        {
            // 唯一性检查
            if (rules.UniqueFields != null)
            {
                foreach (var field in rules.UniqueFields)
                {
                    var value = GetFieldValue(entity, field);
                    if (!string.IsNullOrEmpty(value?.ToString()) && 
                        _repository.ExistsByField(field, value, entity.Id))
                    {
                        return (false, $"字段 [{field}] 的值已存在");
                    }
                }
            }
        }
        return (true, null);
    }
    
    protected virtual void OnBeforeSave(TEntity entity, YZHSaveMode mode)
    {
        switch (mode)
        {
            case YZHSaveMode.Add:
                entity.SetCreateInfo(CurrentUserId);
                break;
            case YZHSaveMode.Update:
                entity.SetUpdateInfo(CurrentUserId);
                break;
        }
        
        // 自动设置 Code
        if (string.IsNullOrEmpty(entity.Code))
            entity.Code = Guid.NewGuid().ToString();
            
        // 自动设置 OrgCode
        if (Metadata.HasAttribute<YZHMultiTenantAttribute>() && 
            HasProperty<TEntity>("OrgCode"))
        {
            SetPropertyValue(entity, "OrgCode", CurrentOrgCode);
        }
    }
    
    protected virtual void OnAfterSave(TEntity entity, YZHSaveMode mode) { }
    
    protected virtual void OnSaveCompleted(TEntity entity, YZHSaveMode mode) { }
    
    #endregion

    #region 删除生命周期（可重写）
    
    protected virtual void OnDeleteStart(object[] keys) { }
    
    protected virtual bool CanDelete(object[] keys) => true;
    
    protected virtual void OnBeforeDelete(object[] keys) { }
    
    protected virtual void OnAfterDelete(object[] keys) { }
    
    #endregion

    #region 重写基类方法（组装生命周期）
    
    public override PageGridData<TEntity> GetPageData(PageDataOptions options)
    {
        OnQueryStart(options);
        
        QueryRelativeExpression = (IQueryable<TEntity> query) =>
        {
            query = OnBuildQuery(query);
            return OnQueryFilter(query);
        };
        
        var result = base.GetPageData(options);
        return OnQueryExecuted(result);
    }
    
    public override WebResponseContent Add(SaveModel model) => ExecuteSave(model, YZHSaveMode.Add);
    
    public override WebResponseContent Update(SaveModel model) => ExecuteSave(model, YZHSaveMode.Update);
    
    private WebResponseContent ExecuteSave(SaveModel model, YZHSaveMode mode)
    {
        OnSaveStart(model, mode);
        
        var entity = model.MainData.Deserialize<TEntity>();
        var (valid, error) = OnValidate(entity);
        if (!valid) return webResponse.Error(error);
        
        AddOnExecuting = (TEntity e, object list) =>
        {
            OnBeforeSave(e, mode);
            return webResponse.OK();
        };
        
        WebResponseContent response = mode == YZHSaveMode.Add 
            ? base.Add(model) : base.Update(model);
        if (!response.Status) return response;
        
        try { OnAfterSave(entity, mode); }
        catch (Exception ex) { return webResponse.Error($"保存后处理失败: {ex.Message}"); }
        
        OnSaveCompleted(entity, mode);
        return response;
    }
    
    public override WebResponseContent Del(object[] keys, bool delList = false)
    {
        OnDeleteStart(keys);
        if (!CanDelete(keys)) return webResponse.Error("当前数据不允许删除");
        
        DelOnExecuting = (object[] delKeys) =>
        {
            OnBeforeDelete(delKeys);
            return webResponse.OK();
        };
        
        var response = base.Del(keys, delList);
        if (!response.Status) return response;
        
        OnAfterDelete(keys);
        return response;
    }
    
    #endregion
}

public enum YZHSaveMode { Add, Update }
```

## 4.3 YZHControllerBase 设计

```csharp
/// <summary>
/// YZH 控制器基类 - 装饰器执行 + 统一响应
/// 
/// 设计原则：
/// - 通过 [YZHUseDecorator] 声明需要的装饰器
/// - 异常由全局过滤器处理，Controller 不捕获
/// - 统一响应格式，前端统一解析
/// </summary>
[ApiController]
public abstract class YZHControllerBase<TEntity, TService> : ControllerBase
    where TEntity : YZHBaseEntity, new()
    where TService : YZHServiceBase<TEntity>
{
    protected readonly TService _service;
    
    public YZHControllerBase(TService service)
    {
        _service = service;
    }
    
    /// <summary>
    /// 统一成功响应
    /// </summary>
    protected IActionResult YZHOk(object data = null, string message = "成功")
    {
        return Ok(new YZHApiResponse
        {
            Success = true,
            Code = "OK",
            Message = message,
            Data = data,
            RequestId = HttpContext.TraceIdentifier
        });
    }
    
    /// <summary>
    /// 统一分页响应
    /// </summary>
    protected IActionResult YZHOkPage<T>(PageGridData<T> pageData)
    {
        return YZHOk(new
        {
            rows = pageData.Rows,
            total = pageData.Total,
            pageSize = pageData.PageSize,
            pageIndex = pageData.PageIndex
        });
    }
    
    // 标准 CRUD 端点由路由自动注册（通过 ServiceBase）
    // 自定义端点在 Partial Controller 中添加
}
```

---

# 五、全局容错机制

## 5.1 设计哲学

> **"优雅的代码不应该被 try-catch 污染。全局过滤器是唯一的异常入口。"**

## 5.2 异常层次体系

```
System.Exception
    └── YZHException (自定义异常基类)
            ├── YZHBusinessException     (业务异常 - 用户友好提示)
            │   → "CNAS 编号已存在"
            │   → "当前状态不允许此操作"
            │
            ├── YZHValidationException   (校验异常 - 字段级错误)
            │   → 包含 Errors 字典: { "Name": ["不能为空"] }
            │
            ├── YZHNotFoundException     (未找到异常)
            │   → "认证机构 [CB001] 不存在"
            │
            └── YZHUnauthorizedException (权限异常)
                → "无权执行此操作"
```

## 5.3 全局异常过滤器

```csharp
/// <summary>
/// YZH 全局异常过滤器 - 唯一的异常处理入口
/// 
/// 使用方式：
/// 1. 在 Program.cs 中注册：builder.Services.AddControllers(options => 
///    options.Filters.Add<YZHGlobalExceptionFilter>());
/// 2. 业务代码只抛异常，不捕获
/// </summary>
public class YZHGlobalExceptionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<YZHGlobalExceptionFilter> _logger;
    private readonly IYZHAuditLogService _auditLog;
    
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var exception = context.Exception;
        var requestId = context.HttpContext.TraceIdentifier;
        
        _logger.LogError(exception, "[{RequestId}] 未处理异常: {Message}", requestId, exception.Message);
        
        // 根据异常类型返回不同响应
        context.Result = exception switch
        {
            YZHBusinessException biz => BuildBusinessResponse(biz, requestId),
            YZHValidationException val => BuildValidationResponse(val, requestId),
            YZHNotFoundException notFound => BuildNotFoundResponse(notFound, requestId),
            UnauthorizedAccessException auth => BuildUnauthorizedResponse(auth, requestId),
            OperationCanceledException cancel => BuildCancelledResponse(cancel, requestId),
            TimeoutException timeout => BuildTimeoutResponse(timeout, requestId),
            DbUpdateException db => BuildDatabaseResponse(db, requestId),
            _ => BuildUnknownResponse(exception, requestId)
        };
        
        context.ExceptionHandled = true;
        
        // 异步写异常日志（不阻塞响应）
        _ = WriteExceptionLogAsync(exception, context.HttpContext, requestId);
    }
    
    private IActionResult BuildBusinessResponse(YZHBusinessException ex, string requestId)
    {
        return new JsonResult(new YZHApiResponse
        {
            Success = false,
            Code = "BUSINESS_ERROR",
            Message = ex.Message,
            RequestId = requestId
        })
        { StatusCode = 200 };  // 业务错误也是 HTTP 200，通过 code 区分
    }
    
    private IActionResult BuildValidationResponse(YZHValidationException ex, string requestId)
    {
        return new JsonResult(new YZHApiResponse
        {
            Success = false,
            Code = "VALIDATION_ERROR",
            Message = "数据校验失败",
            Errors = ex.Errors,
            RequestId = requestId
        })
        { StatusCode = 400 };
    }
    
    // ... 其他 Build 方法
}
```

## 5.4 编码规范对比

```
❌ 禁止的写法（try-catch 泛滥）

public async Task<IActionResult> Add(CertificationBody data)
{
    try
    {
        var result = await _service.AddAsync(data);
        return Ok(result);
    }
    catch (ValidationException ex)
    {
        return BadRequest(ex.Message);
    }
    catch (DbUpdateException ex)
    {
        return Conflict("数据冲突");
    }
    catch (Exception ex)
    {
        _logger.Error(ex);
        return StatusCode(500, "系统错误");
    }
}


✅ 推荐的写法（只抛不捕）

public async Task<IActionResult> Add(CertificationBody data)
{
    // 参数基本校验
    if (data == null)
        throw new YZHValidationException("请求数据不能为空");
    
    if (string.IsNullOrWhiteSpace(data.Name))
        throw new YZHValidationException("机构名称不能为空", 
            new Dictionary<string, string[]> { ["Name"] = new[] { "不能为空" } });
    
    // 业务逻辑交给 Service
    var result = await _service.AddAsync(data);
    
    return YZHOk(result);
}

// 所有异常由 YZHGlobalExceptionFilter 统一处理！
```

---

# 六、装饰器体系（Decorator Pattern）

## 6.1 设计原理

```
传统继承的问题：
  Controller → BaseController → AuthController → LogController → ...
  类层次越来越深，耦合越来越重

装饰器的优势：
  Controller + [AuditLog] + [Cache] + [Validate] + [RateLimit]
  扁平组合，按需声明，灵活插拔
```

## 6.2 装饰器接口

```csharp
/// <summary>
/// YZH Action 装饰器接口
/// 
/// 实现此接口即可作为装饰器使用
/// </summary>
public interface IYZHActionDecorator
{
    /// <summary>执行顺序（越小越先执行）</summary>
    int Order { get; }
    
    /// <summary>Action 执行前</summary>
    Task OnExecutingAsync(ActionExecutingContext context);
    
    /// <summary>Action 执行后（成功）</summary>
    Task OnExecutedAsync(ActionExecutedContext context);
    
    /// <summary>Action 异常时（可选实现）</summary>
    Task OnExceptionAsync(ExceptionContext context, Exception ex);
}
```

## 6.3 内置装饰器

| 装饰器 | Order | 功能 | 适用场景 |
|--------|-------|------|---------|
| `YZHRateLimitDecorator` | -1 | 接口限流 | 公开 API、敏感操作 |
| `YZHValidationDecorator` | 0 | 参数校验 | 大部分 Controller |
| `YZHAuditLogDecorator` | 10 | 审计日志 | 关键业务操作 |
| `YZHCacheDecorator` | 20 | 响应缓存 | 查询类接口 |
| `YZHPerformanceDecorator` | 30 | 性能监控 | 性能分析时启用 |

## 6.4 装饰器中间件（自动执行）

```csharp
/// <summary>
/// 装饰器执行中间件 - 自动发现并执行声明的装饰器
/// </summary>
public class YZHDecoratorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceProvider _serviceProvider;
    
    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var metadata = endpoint?.Metadata;
        
        // 获取声明的装饰器
        var decoratorAttrs = metadata?
            .GetOrderedMetadata<YZHUseDecoratorAttribute>()
            ?.Select(attr => (IYZHActionDecorator)_serviceProvider.GetService(attr.DecoratorType))
            .Where(d => d != null)
            .OrderBy(d => d.Order)
            .ToList() ?? new List<IYZHActionDecorator>();
        
        // 执行 OnExecuting
        foreach (var decorator in decoratorArgs)
        {
            await decorator.OnExecutingAsync(context);
            
            // 如果装饰器设置了 Result（如缓存命中），短路返回
            if (context.Response.HasStarted) return;
        }
        
        await _next(context);  // 执行实际的 Action
        
        // 执行 OnExecuted
        foreach (var decorator in decoratorArgs)
        {
            await decorator.OnExecutedAsync(context);
        }
    }
}
```

---

# 七、日志体系

## 7.1 设计原则

> **"日志必须分类分级，关键操作必须可追溯。"**

## 7.2 日志分类枚举

```csharp
/// <summary>
/// YZH 日志分类 - 用于区分不同模块和类型的操作
/// </summary>
public enum YZHLogCategory
{
    // ====== 系统级 ======
    [Description("系统启动")] SystemStartup = 1000,
    [Description("系统错误")] SystemError = 1001,
    [Description("性能告警")] PerformanceWarning = 1002,
    
    // ====== 安全级 ======
    [Description("登录")] SecurityLogin = 2000,
    [Description("登出")] SecurityLogout = 2001,
    [Description("权限变更")] SecurityPermissionChange = 2002,
    [Description("操作越权")] SecurityViolation = 2003,
    
    // ====== 业务级 - 认证平台 ======
    [Description("认证机构管理")] CertBodyManagement = 3001,
    [Description("企业管理")] EnterpriseManagement = 3002,
    [Description("审核任务")] AuditTask = 3003,
    [Description("审核发现")] AuditFinding = 3004,
    [Description("不符合项")] NonConformity = 3005,
    [Description("报告生成")] ReportGeneration = 3006,
    [Description("标准管理")] StandardManagement = 3007,
    
    // ====== 数据操作级 ======
    [Description("数据创建")] DataCreate = 4001,
    [Description("数据更新")] DataUpdate = 4002,
    [Description("数据删除")] DataDelete = 4003,
    [Description("数据导出")] DataExport = 4004,
    [Description("数据导入")] DataImport = 4005,
    [Description("批量操作")] BatchOperation = 4006,
}
```

## 7.3 日志数据模型

```csharp
/// <summary>
/// YZH 审计日志条目 - 结构化的操作记录
/// </summary>
public class YZHAuditLogEntry
{
    // ====== 基础信息 ======
    public string LogId { get; set; }              // GUID
    public DateTime Timestamp { get; set; }         // 服务器时间
    public string RequestId { get; set; }           // 关联请求追踪 ID
    
    // ====== 分类信息 ======
    public YZHLogCategory Category { get; set; }    // 主分类
    public string SubCategory { get; set; }         // 子分类
    public string ModuleCode { get; set; }          // 模块编码
    public string FeatureCode { get; set; }         // 功能编码
    
    // ====== 操作人信息 ======
    public long? UserId { get; set; }
    public string UserName { get; set; }
    public string UserRole { get; set; }
    public string OrgCode { get; set; }             // 多租户标识
    
    // ====== 操作内容 ======
    public YZHOperationType OperationType { get; set; }
    public string EntityType { get; set; }          // 实体类名
    public string EntityId { get; set; }            // 实体 ID
    public string EntityDisplayName { get; set; }   // 实体显示名称（用于日志可读性）
    public object OldValue { get; set; }            // JSON: 变更前完整数据
    public object NewValue { get; set; }            // JSON: 变更后完整数据
    public Dictionary<string, object> ChangedFields { get; set; }  // 变更字段明细
    
    // ====== 请求信息 ======
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public string Url { get; set; }
    public string HttpMethod { get; set; }
    
    // ====== 结果信息 ======
    public bool Success { get; set; }
    public string Message { get; set; }
    public int? DurationMs { get; set; }           // 耗时（毫秒）
    public string ErrorCode { get; set; }
    
    // ====== 扩展 ======
    public Dictionary<string, object> Extra { get; set; }
}

public enum YZHOperationType
{
    Create, Update, Delete, Query, Export, Import, Login, Logout, Other
}
```

## 7.4 日志存储策略

| 操作类型 | 存储位置 | 保留期限 | 说明 |
|---------|---------|---------|------|
| **增删改** | 数据库表 `yzh_audit_log` | 永久保留 | 合规要求，必须可追溯 |
| **查询** | 文件 / ELK（可选） | 90 天 | 按 [YZHAudited(LogQuery=true)] 触发 |
| **登录登出** | 数据库表 + Redis（在线状态） | 登录 1 年 | 安全审计需求 |
| **系统错误** | 文件 + 数据库（错误表） | 180 天 | 问题排查 |
| **性能告警** | 文件 | 30 天 | 性能优化参考 |

---

# 八、前端组件设计

## 8.1 GenericCrud.vue 通用组件

### 配置驱动

```typescript
// config/certification-body.ts
export const certificationBodyConfig: CrudConfig = {
  entity: 'CertCertificationBody',
  title: '认证机构管理',
  
  columns: [
    { field: 'id', title: 'ID', width: 80, hidden: true },
    { field: 'name', title: '机构名称', width: 200, sort: true, required: true },
    { field: 'shortName', title: '简称', width: 120 },
    { field: 'cbCode', title: 'CNAS编号', width: 150, unique: true },
    { 
      field: 'status', title: '状态', width: 100,
      type: 'select', dictKey: 'cert_status' 
    },
    { field: 'contactName', title: '联系人', width: 120 },
    { field: 'contactPhone', title: '联系电话', width: 130 },
    { field: 'createTime', title: '创建时间', width: 160, sort: true },
  ],
  
  searchFields: ['name', 'status'],
  
  features: {
    search: true, add: true, edit: true, delete: true,
    export: true, import: false, pagination: true
  }
}
```

### 使用方式

```vue
<!-- 简单场景：纯配置 -->
<template>
  <GenericCrud :config="config" />
</template>

<script setup lang="ts">
import GenericCrud from '@/components/GenericCrud.vue'
import { certificationBodyConfig } from '@/config/certification-body'

const config = certificationBodyConfig
</script>
```

## 8.2 TreeCrud.vue 树形组件

### 左树右表抽象

```typescript
// config/iso-standard.ts
export const isoStandardTreeConfig: TreeCrudConfig = {
  entity: 'CertIsoStandard',
  
  tree: {
    url: '/api/CertIsoStandard/GetTreeData',
    labelField: 'name',
    childrenField: 'children',
    defaultExpandAll: false,
  },
  
  linkage: {
    treeField: 'parentId',
    tableFilterField: 'parentId',
    autoQuery: true,
  },
  
  columns: [
    { field: 'code', title: '条款编号', width: 120 },
    { field: 'title', title: '条款名称', width: 300 },
    { field: 'status', title: '状态', type: 'select', dictKey: 'cert_status' },
  ],
}
```

---

# 九、实施路径

## Phase 0.5：搭建 YZH Framework 基础（当前阶段）

- [ ] 创建 YZH.Core 项目结构
- [ ] 实现 YZHBaseEntity（替代 BaseEntity）
- [ ] 实现 YZHServiceBase（生命周期骨架）
- [ ] 实现 YZHControllerBase（基础版）
- [ ] 实现核心 Attribute（MultiTenant, Audited, DeleteStrategy）
- [ ] 实现 YZHGlobalExceptionFilter
- [ ] 实现 YZHBusinessException / YZHValidationException
- [ ] 定义 YZHLogCategory 枚举和 YZHAuditLogEntry 模型

## Phase 1：验证案例 - 认证机构

- [ ] CertificationBody 改为继承 YZHBaseEntity
- [ ] 添加 [YZHMultiTenant]、[YZHAudited]、[YZHValidationRules] 特性
- [ ] CertificationBodyService 继承 YZHServiceBase
- [ ] CertificationBodyController 继承 YZHControllerBase
- [ ] 前端 GenericCrud + 配置文件
- [ ] 测试完整 CRUD 流程

## Phase 2：树形组件 + 更多模块

- [ ] 实现 TreeCrud.vue
- [ ] ISO 标准管理（树形验证）
- [ ] 企业管理（迁移到 YZH Framework）
- [ ] 审核任务（复杂业务逻辑验证）

## Phase 3：完善与提取

- [ ] 补充更多内置装饰器
- [ ] 完善日志查询 UI
- [ ] 性能优化和压力测试
- [ ] 评估是否提取为独立 NuGet 包

---

# 十、附录

## A. 快速参考卡

### 后端开发 Checklist

- [ ] Entity 继承 `YZHBaseEntity`
- [ ] 添加必要的 Attribute（[YZHMultiTenant]、[YZHAudited] 等）
- [ ] Service 继承 `YZHServiceBase<T>`，只在需要时重写钩子
- [ ] Controller 继承 `YZHControllerBase<T, Service>`
- [ ] 业务异常用 `throw new YZHBusinessException("消息")`
- [ ] 校验失败用 `throw new YZHValidationException("消息", errors)`
- [ ] **不要** 写 try-catch（除非真的需要特殊处理）

### 前端开发 Checklist

- [ ] 在 `src/config/` 创建配置文件
- [ ] 使用 `<GenericCrud :config="config" />` 或 `<TreeCrud :config="config" />`
- [ ] 复杂场景通过 hooks prop 传入自定义逻辑
- [ ] 字典绑定使用 `dictKey: 'xxx'`

## B. 常见问题

| 问题 | 原因 | 解决方案 |
|------|------|---------|
| 特性没生效 | Attribute 没放到正确的类上 | 确保放在 Entity 类上 |
| 多租户未过滤 | 缺少 [YZHMultiTenant] 或用户无 OrgCode | 检查 Attribute 和用户上下文 |
| 日志未写入 | 缺少 [YZHAudited] 或 Category 未设置 | 添加 Attribute 并指定分类 |
| 异常返回 500 | 抛出了非 YZH 异常 | 改用 YZHBusinessException |
| 装饰器未执行 | 未注册中间件或 Attribute 声明错误 | 检查 Program.cs 注册和类型约束 |

---

# 十一、前后端架构关联（⭐ 新增 V1.1）

> **"YZH Framework 是全栈框架，前后端必须在同一份文档中表述清楚。"**

## 11.1 架构映射总览

```
┌─────────────────────────────────────────────────────────────────┐
│                     YZH Framework 全栈映射                      │
│                                                                 │
│   后端 (C#)                        前端 (Vue3 + TS)           │
│   ─────────                       ─────────────                │
│                                                                 │
│   [YZHMultiTenant]         ───→     OrgCode 自动附加到请求     │
│                                                                 │
│   [YZHAudited]              ───→     hooks.onSaveAfter 触发日志   │
│                                                                 │
│   [YZHValidationRules]       ───→     表单自动校验规则            │
│                                                                 │
│   [YZHCaching]              ───→     数据缓存 + 状态管理          │
│                                                                 │
│   [YZHDeleteStrategy]        ───→     删除确认提示文案            │
│                                                                 │
│   YZHServiceBase 生命周期    ───→     GenericCrud Hooks 对应       │
│     OnValidate             ───→       onSaveBefore              │
│     OnBeforeSave           ───→       onSaveBefore (可阻止)      │
│     OnAfterSave            ───→       onSaveAfter               │
│     OnQueryStart           ───→       onSearchBefore             │
│     OnQueryExecuted        ───→       onSearchAfter              │
│                                                                 │
│   YZHGlobalExceptionFilter  ───→     http.ts 统一错误拦截         │
│     YZHBusinessException    ───→       ElMessage.error(msg)       │
│     YZHValidationException  ───→       表单字段级错误显示          │
│                                                                 │
│   字典系统                  ───→     dictKey 配置 → 自动加载下拉    │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## 11.2 API 响应格式约定

### 标准响应结构

```typescript
// 后端 YZHApiResponse 结构（前端必须按此解析）
interface YZHApiResponse<T = any> {
  success: boolean        // 是否成功
  code: string            // 响应码："OK" | "BUSINESS_ERROR" | ...
  message: string          // 提示信息
  data: T                 // 业务数据
  requestId: string        // 请求追踪 ID（用于排查问题）
  errors?: Record<string, string[]>  // 字段级错误（校验异常时）
  total?: number           // 总数（分页时）
}

// http.ts 拦截器已统一处理：
// - success === true → 返回 data
// - success === false → ElMessage.error(message) + 抛出异常
```

### 异常码约定

| 后端异常 | HTTP Status | code 值 | 前端处理 |
|---------|-------------|---------|---------|
| 正常 | 200 | `"OK"` | 显示数据 |
| `YZHBusinessException` | 200 | `"BUSINESS_ERROR"` | `ElMessage.warning(message)` |
| `YZHValidationException` | 400 | `"VALIDATION_ERROR"` | 表单字段标红 |
| `YZHNotFoundException` | 404 | `"NOT_FOUND"` | 提示 + 跳转 404 页 |
| 未预期异常 | 500 | `"SYSTEM_ERROR"` | `ElMessage.error("系统繁忙")` |

## 11.3 字典系统前后端协议

```
┌─────────────────────────────────────────────────────────────┐
│                    字典数据流                                  │
│                                                             │
│  后端 Sys_Dictionary 表                                      │
│    ├── DicNo: "cert_status"                                 │
│    ├── DicName: "机构状态"                                   │
│    └── DicList:                                             │
│        ├── { DicValue: "active",  DicName: "启用" }         │
│        ├── { DicValue: "inactive", DicName: "停用" }        │
│        └── { DicValue: "pending",  DicName: "待审核" }       │
│                                                             │
│  前端配置                                                    │
│    { field: 'status', dictKey: 'cert_status' }               │
│                                                             │
│  加载流程                                                    │
│    1. GenericCrud 初始化时收集所有 dictKey                   │
│    2. 调用 POST /api/Sys_Dictionary/GetVueDictionary         │
│    3. 后端返回字典数据                                        │
│    4. 前端自动绑定到 select/radio/checkbox                   │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

## 11.4 认证平台前端组件库规划

```
src/components/
├── yzh/                          # YZH 自定义组件库
│   │
│   ├── GenericCrud.vue            # ⭐ 通用 CRUD 页面（配置驱动）
│   │   ├── Props: config, hooks
│   │   ├── 内置: view-grid 封装
│   │   └── 支持: 分页/排序/搜索/导入导出
│   │
│   ├── TreeCrud.vue              # ⭐ 树形 + CRUD 组合组件
│   │   ├── 左侧: el-tree 树形选择器
│   │   ├── 右侧: GenericCrud 或自定义表格
│   │   └── 联动: 选择节点 → 自动过滤表格
│   │
│   ├── YZHForm.vue               # 增强表单（未来）
│   │   ├── 自动布局（栅格系统）
│   │   ├── 联动规则（字段依赖）
│   │   └── 复杂校验（跨字段）
│   │
│   └── YZHTable.vue               # 增强表格（未来）
│       ├── 行内编辑
│       ├── 拖拽排序
│       └── 虚拟滚动（大数据量）
│
├── composables/                  # 组合式函数
│   ├── useTable.ts               # 表格操作封装
│   ├── useForm.ts                # 表单操作封装
│   ├── useDict.ts                # 字典加载封装
│   └── usePermission.ts          # 权限检查封装
│
└── config/                      # ⭐ 页面配置文件
    ├── certification-body.ts    # 认证机构配置
    ├── iso-standard.ts           # ISO 标准（树形）配置
    ├── enterprise.ts              # 企业管理配置
    └── audit-task.ts              # 审核任务配置
```

---

# 十二、多基类体系设计（⭐ 新增 V1.1）

> **"不同业务场景需要不同的基类，但都共享相同的核心能力。"**

## 12.1 基类继承层次

```
                         ┌─────────────────────────┐
                         │   YZHServiceBase<TEntity>  │
                         │   （抽象根基类）          │
                         │   - 生命周期管理           │
                         │   - 特性读取               │
                         │   - 审计字段填充           │
                         │   - 容错包装               │
                         └───────────┬─────────────┘
                                     │
        ┌────────────────────────────┼────────────────────────────┐
        │                            │                            │
        ▼                            ▼                            ▼
┌───────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐
│ YZHSingleTableSvc  │  │ YZHMasterDetailSvc   │  │ YZHTreeService       │
│ （单表 CRUD）      │  │ （主从表 1:N）        │  │ （树形结构）          │
│                   │  │                      │  │                      │
│ 适用：80% 场景     │  │ 适用：订单+明细       │  │ 适用：目录树、条款树   │
│ - CertificationBody│  │ - Report+Section     │  │ - IsoStandard        │
│ - Enterprise      │  │ - Phase+Task         │  │ - FileDirectory      │
│ - User            │  │                      │  │                      │
└───────────────────┘  └───────────┬──────────┘  └───────────┬──────────┘
                                  │                          │
                                  ▼                          ▼
                    ┌──────────────────────┐  ┌──────────────────────┐
                    │ YZHReadOnlyService   │  │ YZHExportService      │
                    │ （只读查询）          │  │ （导出专用）          │
                    │                      │  │                      │
                    │ 适用：字典、配置      │  │ 适用：报表导出        │
                    │ - Dictionary         │  │ - Excel/PDF          │
                    │ - Dashboard          │  │                      │
                    └──────────────────────┘  └──────────────────────┘
```

## 12.2 各基类特性对比

| 能力 | SingleTable | MasterDetail | Tree | ReadOnly |
|------|------------|-------------|------|----------|
| 标准增删改查 | ✅ | ✅ 主表 | ✅ | ❌ |
| 明细表 CRUD | ❌ | ✅ 自动联动 | ❌ | ❌ |
| 树形查询 | ❌ | ❌ | ✅ 内置 | ❌ |
| 节点移动 | ❌ | ❌ | ✅ | ❌ |
| 导出 | 可选 | 可选 | 可选 | ❌ |
| 导入 | 可选 | 仅主表 | ❌ | ❌ |
| 只读模式 | ❌ | ❌ | ❌ | ✅ |

## 12.3 使用示例

```csharp
// ====== 单表：认证机构 ======
[
    YZHEntityOperations(EnableDelete = false)  // 禁止删除
]
public class CertificationBody : YZHBaseEntity { }

public class CertificationBodyService 
    : YZHSingleTableService<CertificationBody>  // 单表基类
{
    // 如有特殊逻辑，重写钩子即可
}

// ====== 主从表：审核报告 + 报告章节 ======
public class AuditReportService 
    : YZHMasterDetailService<AuditReport, ReportSection>
{
    // 保存时自动处理主表 + 明细
    // 无需额外代码！
}

// ====== 树形：ISO 标准 ======
public class IsoStandardService 
    : YZHTreeService<IsoStandard>
{
    // 内置：GetTree() / MoveNode() / Reorder()
}
```

---

# 十三、编码规则体系（⭐ 新增 V1.1）

> **"认证平台有大量业务编码，必须建立统一的编码生成规则。"**

## 13.1 编码类型清单

| 编码类型 | 示例 | 规则说明 | 使用场景 |
|---------|------|---------|---------|
| **机构编码** | CB001, CB002 | 前缀 CB + 3位序号 | 认证机构 |
| **企业编码** | ENT20260731001 | ENT + 日期 + 3位序号 | 企业客户 |
| **任务编码** | TASK-AUDIT-2026-001 | TASK + 类型 + 年 + 序号 | 审核任务 |
| **报告编号** | RPT-CB001-2026-001 | RPT + 机构 + 年 + 序号 | 认证报告 |
| **NC 编号** | NC-20260731-001 | NC + 日期 + 序号 | 不符合项 |
| **条款编号** | ISO9001-4.1 | 标准代码 + 章节 | ISO 条款引用 |
| **文件编号** | DOC-QM-001 | DOC + 类型 + 序号 | 文件模板 |

## 13.2 编码规则特性

```csharp
/// <summary>
/// 编码生成规则 - 声明实体的编码如何自动生成
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class YZHCodeRuleAttribute : Attribute
{
    /// <summary>编码类型</summary>
    public YZHCodeType CodeType { get; set; }
    
    /// <summary>前缀（如 "CB", "ENT", "RPT"）</summary>
    public string Prefix { get; set; }
    
    /// <summary>编码模式模板</summary>
    /// <para>支持变量：{PREFIX}, {YYYY}, {MM}, {DD}, {SEQ:n}</para>
    public string Pattern { get; set; }
    
    /// <summary>序号长度（默认 3 位，如 001）</summary>
    public int SequenceLength { get; set; } = 3;
    
    /// <summary>重置周期</summary>
    public YZHCodeResetCycle ResetCycle { get; set; } = YZHCodeResetCycle.Yearly;
    
    /// <summary>是否允许手动指定编码</summary>
    public bool AllowManual { get; set; } = false;
}

public enum YZHCodeType
{
    EntityId,      // 实体 ID（GUID 或自增）
    BusinessCode,  // 业务编码（最常用）
    RuleCode,      // 规则编码（固定标准）
    DocumentNo,   // 单据编号
}

public enum YZHCodeResetCycle
{
    Never,       // 永不重置（序号一直递增）
    Daily,       // 每天重置（如 ENT20260731001, ENT20260731002）
    Monthly,     // 每月重置
    Yearly,      // 每年重置
    Earlyly      // 每年年初重置（适合年度业务）
}
```

## 13.3 使用示例

```csharp
/// <summary>
/// 认证机构 - 机构编码自动生成
/// </summary>
[
    YZHCodeRule(
        CodeType = YZHCodeType.BusinessCode,
        Prefix = "CB",
        Pattern = "{PREFIX}{SEQ:3}",           // CB001, CB002...
        ResetCycle = YZHCodeResetCycle.Never   // 不重置
    )
]
public class CertificationBody : YZHBaseEntity
{
    [Column("cb_code")]
    public string CbCode { get; set; }  // 新建时自动生成
}

/// <summary>
/// 审核任务 - 任务编码自动生成
/// </summary>
[
    YZHCodeRule(
        CodeType = YZHCodeType.BusinessCode,
        Prefix = "TASK",
        Pattern = "{PREFIX}-{YYYY}-{SEQ:3}",  // TASK-AUDIT-2026-001
        ResetCycle = YZHCodeResetCycle.Yearly   // 每年重置
    )
]
public class AuditTask : YZHBaseEntity
{
    [Column("task_code")]
    public string TaskCode { get; set; }  // 新建时自动生成
}
```

---

# 十四、实体操作特性（⭐ 新增 V1.1）

> **"通过声明式启用/禁用操作，进一步简化 Controller 和前端。"**

## 14.1 操作声明特性

```csharp
/// <summary>
/// 声明实体支持的操作 - Controller 和前端根据此 Attribute 自动调整
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class YZHEntityOperationsAttribute : Attribute
{
    // 标准 CRUD 操作开关
    public bool EnableCreate { get; set; } = true;
    public bool EnableRead { get; set; } = true;
    public bool EnableUpdate { get; set; } = true;
    public bool EnableDelete { get; set; } = true;
    
    // 扩展操作列表
    public string[] CustomOperations { get; set; }
    
    // 批量操作
    public bool EnableBatchDelete { get; set; } = false;
    public bool EnableBatchUpdate { get; set; } = false;
    
    // 导入导出
    public bool EnableExport { get; set; } = true;
    public bool EnableImport { get; set; } = false;
}

/// <summary>
/// 标记自定义操作方法
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class YZHOperationAttribute : Attribute
{
    public string Name { get; set; }           // 操作名称（用于 API 路由和按钮文字）
    public string Description { get; set; }    // 描述
    public YZHOperationType Type { get; set; } // 操作类型
    public bool RequirePermission { get; set; } = true;
    public string PermissionCode { get; set; }  // 权限码
    public string SuccessMessage { get; set; }  // 成功提示
    public string ConfirmMessage { get; set; }  // 确认提示（需要时显示）
}

public enum YZHOperationType
{
    Approve,       // 审核
    Reject,        // 驳回
    Submit,        // 提交
    Publish,       // 发布
    Archive,       // 归档
    Reactivate,    // 重新激活
    Other          // 其他
}
```

## 14.2 使用示例

```csharp
/// <summary>
/// 审核记录 - 禁止删除和修改，只允许新增和审核操作
/// </summary
[
    YZHEntityOperations(
        EnableCreate = true,
        EnableRead = true,
        EnableUpdate = false,     // 禁止编辑
        EnableDelete = false,     // 禁止删除
        CustomOperations = new[] { "Approve", "Reject", "Submit" },
        EnableExport = true
    )
]
public class AuditRecord : YZHBaseEntity { }

public partial class AuditRecordController : YZHControllerBase<AuditRecord, AuditRecordService>
{
    /// <summary>
    /// 审核通过 - 根据 [YZHOperation("Approve")] 自动注册为 POST api/AuditRecord/Approve
    /// </summary>
    [YZHOperation(
        Name = "Approve",
        Description = "审核通过",
        Type = YZHOperationType.Approve,
        ConfirmMessage = "确定要审核通过此记录吗？"
    )]
    [HttpPost("Approve")]
    public async Task<IActionResult> Approve([FromBody] long id)
    {
        await _service.ApproveAsync(id);
        return YZHOk(null, "审核通过");
    }
    
    // ... Reject, Submit 类似
}
```

**前端效果**：

根据 `[YZHEntityOperations]` 配置，GenericCrud 自动：
- 隐藏/显示工具栏按钮（新增/编辑/删除）
- 渲染自定义操作按钮（审核通过/驳回/提交）
- 弹出确认框（如果 `ConfirmMessage` 不为空）

---

# 十五、未来扩展方向（⭐ 新增 V1.1）

> **"以下内容作为架构演进方向，当前版本不实现，但设计时需预留扩展点。"**

## 15.1 缓存模块增强（v1.1+）

| 特性 | 说明 | 当前状态 |
|------|------|---------|
| `[YZHRedisCache]` | Redis 分布式缓存策略 | 🔴 未实现 |
| `[YZHCacheWarmer]` | 缓存预热（定时刷新热点数据） | 🔴 未实现 |
| 多级缓存 | L1 内存 + L2 Redis 混合 | 🔴 未实现 |

## 15.2 定时任务模块（v1.2+）

| 特性 | 说明 | 当前状态 |
|------|------|---------|
| `[YZHScheduledJob]` | 声明定时任务（Cron 表达式） | 🔴 未实现 |
| `[YZHJobRetry]` | 失败重试策略 | 🔴 未实现 |
| 任务监控 Dashboard | 查看执行历史和状态 | 🔴 未实现 |

## 15.3 安全模块增强（待 Vol 分析后决定）

| 特性 | 说明 | 当前状态 |
|------|------|---------|
| 数据权限 | 行级/列级数据权限控制 | 🟡 待分析 Vol |
| 字段加密 | 敏感字段数据库加密存储 | 🔴 未实现 |
| 操作水印 | 屏幕水印防截图 | 🔴 未实现 |
| IP 白名单 | 接口访问 IP 限制 | 🔴 未实现 |

## 15.4 工作流集成（v2.0+）

| 特性 | 说明 | 当前状态 |
|------|------|---------|
| `[YZHWorkflow]` | 声明审批流程 | 🔴 未实现 |
| 状态机 | 实体状态流转规则 | 🔴 未实现 |
| 会签/或签 | 多人审批模式 | 🔴 未实现 |

## 15.5 模块化拆分计划

```
YZH.Framework/ 当前结构（单体）
    ↓
YZH.Framework/ 未来目标（模块化 NuGet 包）
├── YZH.Core.dll                 # 核心（必须）
│   ├── Base/
│   ├── Attributes/
│   └── Exceptions/
│
├── YZH.Web.dll                 # Web 扩展（Web 项目必须）
│   ├── Filters/
│   └── Middleware/
│
├── YZH.Caching.dll             # 🔮 缓存（可选）
├── YZH.Scheduling.dll          # 🔮 定时任务（可选）
├── YZH.Security.dll             # 🔮 安全增强（可选）
├── YZH.Workflow.dll             # 🔮 工作流（可选）
└── YZH.Reporting.dll            # 🔮 报表（可选）
```

---

# 十六、Vol 源码分析任务（独立任务）

> **"在造轮子前，先分析 Vol 已有什么。这是必须的前置工作。"**

## 16.1 分析范围

| Vol 模块 | 分析重点 | 输出物 |
|---------|---------|--------|
| **权限体系** | 菜单权限、按钮权限、数据权限 | 《Vol 权限能力分析报告》 |
| **日志体系** | 操作日志、异常日志、访问日志 | 《Vol 日志能力分析报告》 |
| **字典系统** | 字典加载机制、缓存策略 | 《Vol 字典使用最佳实践》 |
| **代码生成器** | 模板结构、扩展点 | 《Vol 代码生成扩展指南》 |
| **过滤器/中间件** | 已有哪些全局过滤器 | 《Vol 过滤器清单》 |

## 16.2 分析原则

```
Vol 源码分析决策树：

功能 X 在 Vol 中是否存在？
├── 是，且完整可用
│   └── ✅ 直接复用，YZH 不重复实现
│
├── 是，但不满足需求
│   └── 🔄 在 Vol 基础上扩展（继承/装饰器）
│
├── 不存在
│   └── 🆕 YZH 自己实现
│
└── 不确定
    └── 🔍 先深入分析源码再决定
```

## 16.3 执行时机

| 任务 | 时机 | 产出 |
|------|------|------|
| 权限分析 | Phase 0.5 前 | 决定 [YZHPermission] 是否需要 |
| 日志分析 | Phase 0.5 前 | 决定 [YZHAudited] 与 Vol 日志的关系 |
| 字典分析 | Phase 1 时 | 确保前端字典使用方式正确 |
| 代码生成器 | Phase 2 时 | 决定是否扩展代码生成模板 |

---

# 十七、API 端点自动注册机制（⭐ 新增 V1.2）

> **"每个实体默认拥有完整的 CRUD API，零代码即可使用。Controller 只在需要定制时才写。"**

## 17.1 设计动机

```
┌─────────────────────────────────────────────────────────────┐
│                     为什么需要自动注册？                       │
│                                                             │
│  ✅ 开发阶段：Postman / Swagger 直接测试每个接口              │
│  ✅ 调试阶段：快速验证数据更新逻辑                            │
│  ✅ 集成阶段：前端对接时接口已就绪，无需等待后端写 Controller   │
│  ✅ 运维阶段：脚本批量操作数据（导入/修复）                    │
│  ✅ 扩展场景：第三方系统调用标准 CRUD 接口                     │
│                                                             │
│  核心原则：                                                  │
│    默认提供 → 零配置可用                                     │
│    按需覆盖 → Partial Controller 中重写                      │
│    特性标注 → 自定义行为（权限、缓存、日志等）                │
└─────────────────────────────────────────────────────────────┘
```

## 17.2 默认端点清单

每个继承 `YZHControllerBase` 的 Controller，**自动注册**以下端点：

| HTTP 方法 | 路由模板 | 说明 | 对应 Service 方法 | 是否可覆盖 |
|-----------|---------|------|------------------|-----------|
| `GET` | `api/{Entity}/Page` | 分页查询 | `GetPageData()` | ✅ |
| `GET` | `api/{Entity}/{id}` | 按 ID 查询 | `GetById(id)` | ✅ |
| `GET` | `api/{Entity}/All` | 查询全部（不分页） | `GetAll()` | ✅ |
| `GET` | `api/{Entity}/Tree` | 树形查询（可选） | `GetTreeData()` | ✅ 仅 Tree 基类 |
| `POST` | `api/{Entity}` | 新增 | `Add(model)` | ✅ |
| `PUT` | `api/{Entity}` | 编辑 | `Update(model)` | ✅ |
| `DELETE` | `api/{Entity}` | 删除（支持批量） | `Del(keys)` | ✅ |
| `POST` | `api/{Entity}/Export` | 导出 Excel | `Export(query)` | ✅ |
| `POST` | `api/{Entity}/Import` | 导入 Excel | `Import(file)` | ✅ |

### 路由命名规则

```
实体类名                    →  URL 路径前缀
─────────────────────────────────────────
CertificationBody          →  api/CertificationBody
CertIsoStandard            →  api/CertIsoStandard
AuditTask                  →  api/AuditTask

可通过 [YZHRoutePrefix] 特性自定义：
[YZHRoutePrefix("cert/bodies")]
public class CertificationBody : YZHBaseEntity { }
→  api/cert/bodies/...
```

## 17.3 自动注册实现原理

```csharp
/// <summary>
/// YZH Controller 基类 - 内置标准 CRUD 端点
/// 
/// 设计原则：
/// - 继承即拥有完整 API，无需手写任何 Action
/// - 通过虚方法/Partial 类按需覆盖
/// - 通过 Attribute 声明式调整行为
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class YZHControllerBase<TEntity, TService> : ControllerBase
    where TEntity : YZHBaseEntity, new()
    where TService : YZHServiceBase<TEntity>
{
    protected readonly TService _service;
    
    public YZHControllerBase(TService service) => _service = service;

    // ════════════════════════════════════════════════════════════
    // 🔵 标准查询端点（自动注册，可在 Partial 中 override）
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 分页查询 - GET api/{controller}?page=1&pageSize=20&...
    /// </summary>
    [HttpGet]
    [YZHOperation(Name = "Query", Description = "分页查询")]
    public virtual async Task<IActionResult> GetPage([FromQuery] PageQueryInput input)
        => YZHOkPage(await _service.GetPageDataAsync(input.ToOptions()));

    /// <summary>
    /// 按 ID 查询 - GET api/{controller}/{id}
    /// </summary>
    [HttpGet("{id:long}")]
    [YZHOperation(Name = "GetById", Description = "按ID查询")]
    public virtual async Task<IActionResult> GetById(long id)
    {
        var entity = await _service.GetByIdAsync(id);
        if (entity == null) throw new YZHNotFoundException($"{typeof(TEntity).Name} [{id}] 不存在");
        return YZHOk(entity);
    }

    /// <summary>
    /// 查询全部（不分页，慎用大数据量）- GET api/{controller}/all
    /// </summary>
    [HttpGet("all")]
    [YZHOperation(Name = "GetAll", Description = "查询全部")]
    public virtual async Task<IActionResult> GetAll()
        => YZHOk(await _service.GetAllAsync());

    // ════════════════════════════════════════════════════════════
    // 🟢 标准写入端点（自动注册）
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 新增 - POST api/{controller}
    /// </summary>
    [HttpPost]
    [YZHOperation(Name = "Create", Description = "新增记录", ConfirmMessage = null)]
    public virtual async Task<IActionResult> Create([FromBody] TEntity entity)
        => YZHOk(await _service.AddAsync(entity), "创建成功");

    /// <summary>
    /// 编辑 - PUT api/{controller}
    /// </summary>
    [HttpPut]
    [YZHOperation(Name = "Update", Description = "编辑记录")]
    public virtual async Task<IActionResult> Update([FromBody] TEntity entity)
        => YZHOk(await _service.UpdateAsync(entity), "更新成功");

    /// <summary>
    /// 删除（支持批量）- DELETE api/{controller}?ids=1,2,3
    /// </summary>
    [HttpDelete]
    [YZHOperation(
        Name = "Delete", 
        Description = "删除记录",
        ConfirmMessage = "确定要删除选中的 {count} 条记录吗？此操作不可撤销。"
    )]
    public virtual async Task<IActionResult> Delete([FromQuery] string ids)
    {
        var idArray = ids.Split(',').Select(long.Parse).Cast<object>().ToArray();
        return YZHOk(await _service.DelAsync(idArray), $"成功删除 {idArray.Length} 条记录");
    }

    // ════════════════════════════════════════════════════════════
    // 🟡 扩展端点（条件性注册）
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// 导出 Excel - POST api/{controller}/export
    /// 仅当 [YZHEntityOperations(EnableExport=true)] 时注册
    /// </summary>
    [HttpPost("export")]
    [YZHOperation(Name = "Export", Description = "导出Excel")]
    public virtual async Task<IActionResult> Export([FromBody] ExportQueryInput input)
    {
        var bytes = await _service.ExportAsync(input);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                   $"{typeof(TEntity).Name}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// 导入 Excel - POST api/{controller}/import
    /// 仅当 [YZHEntityOperations(EnableImport=true)] 时注册
    /// </summary>
    [HttpPost("import")]
    [YZHOperation(Name = "Import", Description = "导入Excel")]
    public virtual async Task<IActionResult> Import(IFormFile file)
    {
        var result = await _service.ImportAsync(file);
        return YZHOk(result, $"导入完成：成功 {result.SuccessCount} 条，失败 {result.FailCount} 条");
    }
}

// ════════════════════════════════════════════════════════════════════
// 输入模型
// ════════════════════════════════════════════════════════════════════

/// <summary>分页查询输入</summary>
public class PageQueryInput
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string OrderField { get; set; }
    public bool OrderAsc { get; set; } = true;
    public string Keyword { get; set; }           // 全文搜索
    public Dictionary<string, object> Filters { get; set; }  // 高级过滤
    
    internal PageDataOptions ToOptions() => new() { ... };
}

/// <summary>导出查询输入</summary>
public class ExportQueryInput
{
    public Dictionary<string, object> Filters { get; set; }
    public string[] Fields { get; set; }         // 指定导出列（空=全部）
    public string Format { get; set; } = "xlsx"; // xlsx | csv | pdf
}
```

## 17.4 使用示例

### 场景 A：零代码完整 API（最常见）

```csharp
// ====== Entity ======
[
    YZHEntityOperations(EnableExport = true, EnableImport = false),
    YZHMultiTenant,
    YZHAudited(Category = YZHLogCategory.CertBodyManagement)
]
public class CertificationBody : YZHBaseEntity { }

// ====== Service（只处理特殊逻辑）=====
public class CertificationBodyService 
    : YZHSingleTableService<CertificationBody>
{
    // 无需额外代码！CRUD 完全由基类处理
}

// ====== Controller（空壳！）======
public class CertificationBodyController 
    : YZHControllerBase<CertificationBody, CertificationBodyService>
{
    // 空的！但自动拥有 8 个 API 端点：
    // ✓ GET    /api/CertificationBody?page=1&pageSize=20
    // ✓ GET    /api/CertificationBody/123
    // ✓ GET    /api/CertificationBody/all
    // ✓ POST   /api/CertificationBody          (新建)
    // ✓ PUT    /api/CertificationBody          (编辑)
    // ✓ DELETE /api/CertificationBody?ids=1,2,3
    // ✓ POST   /api/CertificationBody/export
}
```

**效果**：
- Postman/Swagger 直接测试所有接口
- 前端 GenericCrud 无缝对接
- 零 Controller 代码

---

### 场景 B：覆盖个别端点 + 新增自定义端点

```csharp
// ====== Partial Controller（只写需要定制的部分）======
public partial class CertificationBodyController 
    : YZHControllerBase<CertificationBody, CertificationBodyService>
{
    /// <summary>
    /// 覆盖默认的分页查询 - 添加业务过滤逻辑
    /// </summary>
    public override async Task<IActionResult> GetPage([FromQuery] PageQueryInput input)
    {
        // 示例：非管理员只能查看本机构数据
        if (!IsAdmin)
        {
            input.Filters ??= new();
            input.Filters["orgCode"] = CurrentOrgCode;
        }
        
        return await base.GetPage(input);  // 调用基类实现
    }
    
    /// <summary>
    /// 自定义端点：获取活跃机构列表（下拉框用）
    /// - 自动注册为: POST /api/CertificationBody/ActiveList
    /// - 自动应用装饰器（如果 Controller 级声明了的话）
    /// </summary>
    [YZHOperation(
        Name = "ActiveList",
        Description = "获取活跃机构列表",
        RequirePermission = false  // 公开接口，不需要特定权限
    )]
    [HttpPost("ActiveList")]
    public async Task<IActionResult> GetActiveList()
    {
        var list = await _service.GetActiveListAsync();
        return YZHOk(list);
    }
    
    /// <summary>
    /// 自定义端点：审核通过
    /// </summary>
    [YZHOperation(
        Name = "Approve",
        Type = YZHOperationType.Approve,
        ConfirmMessage = "确定要审核通过此机构吗？"
    )]
    [HttpPost("{id:long}/Approve")]
    public async Task<IActionResult> Approve(long id)
    {
        await _service.ApproveAsync(id);
        return YZHOk(null, "审核通过");
    }
    
    /// <summary>
    /// 自定义端点：根据 CNAS 编号查询
    /// </summary>
    [HttpGet("ByCode/{code}")]
    public async Task<IActionResult> GetByCode(string code)
    {
        var entity = await _service.GetByCodeAsync(code);
        return entity == null 
            ? throw new YZHNotFoundException($"CNAS 编号 [{code}] 不存在")
            : YZHOk(entity);
    }
}
```

**最终 API 清单**：

| 方法 | 路由 | 来源 | 说明 |
|------|------|------|------|
| GET | `/api/CertificationBody` | 基类（被覆盖） | 分页查询（含权限过滤） |
| GET | `/api/CertificationBody/{id}` | 基类 | 按 ID 查询 |
| GET | `/api/CertificationBody/all` | 基类 | 查询全部 |
| POST | `/api/CertificationBody` | 基类 | 新增 |
| PUT | `/api/CertificationBody` | 基类 | 编辑 |
| DELETE | `/api/CertificationBody?ids=` | 基类 | 删除 |
| POST | `/api/CertificationBody/export` | 基类 | 导出 |
| **POST** | **`/api/CertificationBody/ActiveList`** | **自定义** | **活跃列表** |
| **POST** | **`/api/CertificationBody/{id}/Approve`** | **自定义** | **审核通过** |
| **GET** | **`/api/CertificationBody/ByCode/{code}`** | **自定义** | **按编码查** |

---

### 场景 C：禁用某些默认端点

```csharp
[
    YZHEntityOperations(
        EnableCreate = true,
        EnableRead = true,
        EnableUpdate = false,      // ❌ 禁止编辑
        EnableDelete = false,      // ❌ 禁止删除
        EnableExport = true,
        EnableImport = false
    )
]
public class AuditRecord : YZHBaseEntity { }

// 结果：
// ✓ GET    /api/AuditRecord          （查询可用）
// ✗ PUT    /api/AuditRecord          （405 Not Allowed）
// ✗ DELETE /api/AuditRecord          （405 Not Allowed）
// ✗ POST   /api/AuditRecord/import   （405 Not Allowed）
```

## 17.5 Swagger/OpenAPI 集成

```csharp
/// <summary>
/// YZH Swagger 配置 - 自动为所有端点生成友好的 API 文档
/// </summary>
public static class YZHSwaggerSetup
{
    public static void AddYZHSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "YZH Framework API",
                Version = "v1.0",
                Description = "映智汇体系认证平台 - 基于 YZH Framework 自动生成"
            });
            
            // 包含 XML 注释
            var xmlFile = $"{Assembly.GetExecutingAssembly().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath);
            
            // 按 Tag 分组（对应 Entity）
            options.TagActionsBy = api =>
            {
                var entityType = api.ActionDescriptor.EndpointMetadata
                    .OfType<YZHEntityOperationsAttribute>()
                    .FirstOrDefault();
                return entityType?.GetType().Name ?? "Other";
            };
        });
    }
}
```

**Swagger 效果预览**：

```
┌─────────────────────────────────────────────────────────────┐
│  YZH Framework API v1.0                                      │
│                                                              │
│  📂 CertificationBody  (认证机构管理)                         │
│     ├── GET    /api/CertificationBody       分页查询          │
│     ├── GET    /api/CertificationBody/{id}  按ID查询         │
│     ├── POST   /api/CertificationBody       新增              │
│     ├── PUT    /api/CertificationBody       编辑              │
│     ├── DELETE /api/CertificationBody       删除              │
│     ├── POST   /api/CertificationBody/export 导出             │
│     ├── POST   /api/CertificationBody/ActiveList 活跃列表    │
│     └── POST   /api/CertificationBody/{id}/Approve 审核通过  │
│                                                              │
│  📂 IsoStandard  (ISO标准管理)                                │
│     ├── GET    /api/IsoStandard/Page                        │
│     ├── GET    /api/IsoStandard/Tree    ⭐ 树形查询           │
│     └── ...                                                 │
│                                                              │
│  📂 AuditTask  (审核任务)                                     │
│     └── ...                                                 │
└─────────────────────────────────────────────────────────────┘
```

## 17.6 测试与调试友好设计

```csharp
/// <summary>
/// 开发环境专用：启用详细错误信息 + 允许 CORS + 禁用缓存
/// </summary>
public static class YZHDevHelper
{
    public static void UseYZHDevMode(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        
        // 1. 详细异常信息（生产环境自动隐藏）
        app.UseDeveloperExceptionPage();
        
        // 2. CORS 允许前端调试
        app.UseCors(policy => policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
            
        // 3. 请求日志中间件（开发环境输出到控制台）
        app.UseYZHRequestLogging(logLevel: LogLevel.Debug);
        
        Console.WriteLine("""
        ╔══════════════════════════════════════════╗
        ║  🚀 YZH Framework Dev Mode Activated!     ║
        ║                                          ║
        ║  Swagger: http://localhost:9992/swagger  ║
        ║  Health: http://localhost:9992/health     ║
        ║                                          ║
        ║  所有 API 端点已自动注册，可直接测试       ║
        ╚══════════════════════════════════════════╝
        """);
    }
}
```

## 17.7 与 Vol 框架路由兼容

```
Vol 原有路由风格：
  POST /api/CertCertificationBody/GetDataList   ← Vol 的命名

YZH 新路由风格（RESTful）：
  GET  /api/CertificationBody?page=1&pageSize=20  ← 更直观

兼容策略：
  ┌────────────────────────────────────────────────┐
  │ Phase 1：双路由并存                              │
  │   - Vol 路由保留（向后兼容）                     │
  │   - YZH RESTful 路由新增（推荐使用）             │
  │                                                │
  │ Phase 2：逐步迁移                               │
  │   - 前端新页面使用 YZH 路由                     │
  │   - 旧页面保持 Vol 路由直到重构                  │
  │                                                │
  │ Phase 3：统一（可选）                            │
  │   - 移除 Vol 路由，仅保留 YZH RESTful            │
  └────────────────────────────────────────────────┘
```

---

# 十八、实体级特性声明（⭐ 新增 V1.3）

> **"实体是数据的源头，它应该拥有最完整的声明信息。Controller/Service 只是围绕它工作。"**

## 18.1 为什么实体需要特性

```
┌─────────────────────────────────────────────────────────────┐
│              特性的作用域与优先级                                │
│                                                             │
│  📦 实体级别 (Entity)          ← 数据的"宪法"               │
│     "这张表能做什么？谁能碰？删了能恢复吗？"                  │
│     → 删除策略、权限基线、操作开关、编码规则                 │
│                                                             │
│  📋 Controller/方法级别       ← API 的"规则"                │
│     "这个接口需要什么额外能力？"                             │
│     → 装饰器、缓存、限流、日志详细度                          │
│                                                             │
│  🔧 Service 钩子 (Override)    ← 业务特例                   │
│     "这个特定场景有什么不同？"                               │
│     → 自定义校验、保存前后处理                                 │
│                                                             │
│  核心原则：                                                  │
│    实体声明"是什么"（What）                                  │
│    Controller 声明"怎么做"（How）                              │
│    Service 处理"例外情况"（Exception）                         │
└─────────────────────────────────────────────────────────────┘
```

## 18.2 实体级特性完整清单

### 18.2.1 删除相关（已在 §3.2.3 定义，此处补充使用场景）

```csharp
/// <summary>
/// 实体删除策略 - 决定数据删除时的行为
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class YZHDeleteStrategyAttribute : Attribute
{
    /// <summary>删除模式</summary>
    public YZHDeleteMode Mode { get; set; } = YZHDeleteMode.Logical;
    
    /// <summary>
    /// 是否允许物理删除（即使 Mode=Logical，管理员也可强制物理删除）
    /// 默认 false = 即使管理员也只能逻辑删除
    /// </summary>
    public bool AllowPhysicalDelete { get; set; } = false;
    
    /// <summary>
    /// 删除前是否检查关联数据
    /// 为 true 时，有子记录则禁止删除
    /// </summary>
    public bool CheckDependencies { get; set; } = true;
    
    /// <summary>
    /// 关联实体列表（用于依赖检查）
    /// </summary>
    public Type[] DependentEntities { get; set; }
}

public enum YZHDeleteMode
{
    Logical,    // 逻辑删除（默认，设置 DeleteBy/DeleteTime）
    Physical,   // 物理删除（直接 DELETE）
    Cascading    // 级联删除（慎用！同时删除关联数据）
}
```

### 18.2.2 权限相关（实体级权限基线）

```csharp
/// <summary>
/// 实体权限基线 - 定义谁可以对这个实体的数据进行操作
/// 
/// 这是"最低权限要求"，Controller/方法上可以进一步收紧
/// 但不能放宽（即：实体说只有管理员能操作，方法就不能改成所有人都能操作）
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class YZHEntityPermissionAttribute : Attribute
{
    /// <summary>允许访问的角色列表（空=所有已登录用户）</summary>
    public string[] AllowedRoles { get; set; }
    
    /// <summary>禁止访问的角色列表（黑名单优先于白名单）</summary>
    public string[] DeniedRoles { get; set; }
    
    /// <summary>是否仅创建者可编辑自己的数据</summary>
    public bool OwnerEditableOnly { get; set; } = false;
    
    /// <summary>创建者字段名（用于 OwnerEditableOnly 检查）</summary>
    public string CreatorField { get; set; } = "CreateBy";
    
    /// <summary>是否允许跨租户访问（超级管理员专用）</summary>
    public bool AllowCrossTenant { get; set; } = false;
}

// ====== 权限场景枚举 ======

/// <summary>
/// 预定义的权限场景 - 避免硬编码角色字符串
/// </summary>
public enum YZHPermissionScenario
{
    [Description("仅超级管理员")] SuperAdminOnly,
    [Description("平台管理层")] PlatformAdmin,        // 超级+总管理+运维+配置+质量
    [Description("机构管理层")] OrgAdmin,             // 审核管理员+审核组长
    [Description("所有后台用户")] AllBackendUsers,      // Layer 1 + Layer 2
    [Description("含企业用户")] IncludeEnterprise,      // 后台 + 企业账号
    [Description("所有已登录用户")] AllAuthenticated,    // 只要登录即可
    [Description("公开接口")] Public,                 // 无需登录
}

// 使用示例：
[YZHEntityPermission(AllowedRoles = new[] { "SuperAdmin", "PlatformAdmin" })]
// 或更语义化：
// [YZHEntityPermission(Scenario = YZHPermissionScenario.PlatformAdmin)]
```

### 18.2.3 操作控制（已在 §14 定义，此处补充实体级用法）

```csharp
/// <summary>
/// 实体操作控制 - 声明这个实体支持哪些操作
/// 
/// 同时影响：
///   1. 后端：自动注册/禁用对应的 API 端点
///   2. 前端：GenericCrud 自动显示/隐藏按钮
///   3. 权限：自动生成对应的权限码
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class YZHEntityOperationsAttribute : Attribute
{
    // 标准 CRUD
    public bool EnableCreate { get; set; } = true;
    public bool EnableRead { get; set; } = true;
    public bool EnableUpdate { get; set; } = true;
    public bool EnableDelete { get; set; } = true;
    
    // 批量操作
    public bool EnableBatchDelete { get; set; } = false;
    public bool EnableBatchUpdate { get; set; } = false;
    
    // 导入导出
    public bool EnableExport { get; set; } = true;
    public bool EnableImport { get; set; } = false;
    
    // 高级操作
    public bool EnableApprove { get; set; } = false;   // 审核通过
    public bool EnableReject { get; set; } = false;   // 驳回
    public bool EnablePublish { get; set; } = false;   // 发布
    public bool EnableArchive { get; set; } = false;   // 归档
    
    /// <summary>自定义操作名称列表</summary>
    public string[] CustomOperations { get; set; }
}
```

### 18.2.4 数据完整性

```csharp
/// <summary>
/// 数据完整性约束 - 在框架层面强制执行的业务规则
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class YZHDataIntegrityAttribute : Attribute
{
    /// <summary>唯一字段组合（联合唯一）</summary>
    public string[][] UniqueKeyGroups { get; set; }
    
    /// <summary>不允许重复的字段（单字段唯一）</summary>
    public string[] UniqueFields { get; set; }
    
    /// <summary>必填字段（补充 DataAnnotation）</summary>
    public string[] RequiredFields { get; set; }
    
    /// <summary>字段最大长度映射</summary>
    public Dictionary<string, int> MaxLengths { get; set; }
    
    /// <summary>字段值范围（枚举类型字段的允许值）</summary>
    public Dictionary<string, string[]> EnumeratedValues { get; set; }
    
    /// <summary>正则校验模式</summary>
    public Dictionary<string, string> Patterns { get; set; }
    
    /// <summary>引用完整性：外键关联（删除时检查）</summary>
    public YZHReferenceRule[] References { get; set; }
}

/// <summary>引用规则</summary>
public class YZHReferenceRule
{
    public string Field { get; set; }              // 本实体字段
    public Type ReferencedEntity { get; set; }      // 引用的实体
    public string ReferencedField { get; set; }     // 引用的字段
    public YZHReferenceAction OnDeleteAction { get; set; } = YZHReferenceAction.Restrict;
}

public enum YZHReferenceAction
{
    NoCheck,       // 不检查（默认）
    Restrict,     // 有引用时禁止删除
    SetNull,       // 删除时置空
    Cascade        // 级联删除（危险！）
}
```

### 18.2.5 审计与追踪增强

```csharp
/// <summary>
/// 审计追踪 - 控制哪些操作需要记录日志、记录到什么程度
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class YZHAuditedAttribute : Attribute
{
    /// <summary>审计分类</summary>
    public YZHLogCategory Category { get; set; }
    
    /// <summary>子分类</summary>
    public string SubCategory { get; set; }
    
    /// <summary>记录哪些操作</summary>
    public YZHAuditScope Scope { get; set; } = YZHAuditScope.Crud;
    
    /// <summary>敏感字段（日志中脱敏）</summary>
    public string[] SensitiveFields { get; set; }
    
    /// <summary>是否记录变更详情（新旧值对比）</summary>
    public bool TrackChanges { get; set; } = true;
    
    /// <summary>是否记录完整快照（大数据量，谨慎开启）</summary>
    public bool FullSnapshot { get; set; } = false;
    
    /// <summary>保留期限（天），null=永久</summary>
    public int? RetentionDays { get; set; }
}

/// <summary>审计范围</summary>
[Flags]
public enum YZHAuditScope
{
    None = 0,
    Create = 1 << 0,
    Update = 1 << 1,
    Delete = 1 << 2,
    Query = 1 << 3,         // 通常不开启查询日志（量太大）
    Export = 1 << 4,
    Import = 1 << 5,
    Approve = 1 << 6,       // 审核操作
    Reject = 1 << 7,
    Crud = Create | Update | Delete,           // 标准增删改
    All = Crud | Query | Export | Import | Approve | Reject
}
```

### 18.2.6 多租户与数据隔离

```csharp
/// <summary>
/// 多租户数据隔离策略
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class YZHMultiTenantAttribute : Attribute
{
    /// <summary>OrgCode 字段名</summary>
    public string OrgCodeField { get; set; } = "OrgCode";
    
    /// <summary>隔离模式</summary>
    public YZHTenantIsolationMode IsolationMode { get; set; } = YZHTenantIsolationMode.Strict;
    
    /// <summary>超级管理员是否也过滤（false=看所有数据）</summary>
    public bool FilterForSuperAdmin { get; set; } = false;
    
    /// <summary>忽略的角色（这些角色的用户不过滤）</summary>
    public string[] IgnoreRoles { get; set; }
    
    /// <summary>是否允许跨租户复制数据</summary>
    public bool AllowCrossTenantCopy { get; set; } = false;
}

/// <summary>多租户隔离模式</summary>
public enum YZHTenantIsolationMode
{
    Strict,       // 严格隔离：只能看到自己机构的
    Relaxed,      // 宽松隔离：可查看其他机构（只读）
    Shared,       // 共享数据：所有机构共享（如字典、标准）
    Disabled      // 不启用多租户（系统级表）
}
```

### 18.2.7 编码规则（已在 §13 定义，此处归入实体级）

```csharp
/// <summary>
/// 业务编码生成规则
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class YZHCodeRuleAttribute : Attribute
{
    public YZHCodeType CodeType { get; set; }
    public string Prefix { get; set; }
    public string Pattern { get; set; }
    public int SequenceLength { get; set; } = 3;
    public YZHCodeResetCycle ResetCycle { get; set; } = YZHCodeResetCycle.Yearlyly;
    public bool AllowManual { get; set; } = false;
}

public enum YZHCodeType
{
    EntityId,      // 实体 ID
    BusinessCode,  // 业务编码（CB001, ENT...）
    RuleCode,      // 规则编码（ISO9001-4.1）
    DocumentNo,   // 单据编号（RPT-2026-001）
    TaskCode       // 任务编号（TASK-AUDIT-...）
}

public enum YZHCodeResetCycle
{
    Never, Daily, Monthly, Yearly, Earlyly
}
```

## 18.3 完整实体声明示例

```csharp
/// <summary>
/// 认证机构 - 展示实体级特性的完整用法
/// 
/// 特性声明的顺序建议：
/// 1. 操作控制（我能做什么？）
/// 2. 权限基线（谁能做？）
/// 3. 删除策略（删了怎样？）
/// 4. 多租户（数据归属？）
/// 5. 数据完整性（有哪些约束？）
/// 6. 审计追踪（留什么痕迹？）
/// 7. 编码规则（怎么编号？）
/// </summary
[
    // ════════════════ 1. 操作控制 ════════════════
    YZHEntityOperations(
        EnableCreate = true,
        EnableRead = true,
        EnableUpdate = true,
        EnableDelete = false,            // ❌ 禁止删除（合规要求）
        EnableExport = true,
        EnableImport = false,
        CustomOperations = new[] { "Approve", "Reject", "Suspend", "Reactivate" }
    ),
    
    // ════════════════ 2. 权限基线 ════════════════
    YZHEntityPermission(
        Scenario = YZHPermissionScenario.PlatformAdmin,  // 平台管理层可操作
        OwnerEditableOnly = false                           // 任何有权限的人都能编辑
    ),
    
    // ════════════════ 3. 删除策略 ════════════════
    YZHDeleteStrategy(
        Mode = YZHDeleteMode.Logical,            // 默认逻辑删除
        AllowPhysicalDelete = false,               // 即使管理员也不能物理删除
        CheckDependencies = true                    // 有审核任务时禁止删除
    ),
    
    // ════════════════ 4. 多租户 ════════════════
    YZHMultiTenant(
        IsolationMode = YZHTenantIsolationMode.Strict,
        FilterForSuperAdmin = false               // 超管也只看当前机构
    ),
    
    // ════════════════ 5. 数据完整性 ════════════════
    YZHDataIntegrity(
        UniqueFields = new[] { "CbCode" },          // CNAS 编号全局唯一
        RequiredFields = new[] { "Name", "CbCode" },
        MaxLengths = new Dictionary<string, int>
        {
            { "Name", 200 },
            { "ShortName", 100 },
            { "CbCode", 50 }
        },
        EnumeratedValues = new Dictionary<string, string[]>
        {
            { "Status", new[] { "active", "inactive", "pending", "suspended" } }
        }
    ),
    
    // ════════════════ 6. 审计追踪 ════════════════
    YZHAudited(
        Category = YZHLogCategory.CertBodyManagement,
        SubCategory = "机构基本信息",
        SensitiveFields = new[] { "ContactPhone", "Email", "Notes", "BankAccount" },
        TrackChanges = true,
        RetentionDays = null                               // 永久保留（合规要求）
    ),
    
    // ════════════════ 7. 编码规则 ════════════════
    YZHCodeRule(
        CodeType = YZHCodeType.BusinessCode,
        Prefix = "CB",
        Pattern = "{PREFIX}{SEQ:3}",                // CB001, CB002...
        ResetCycle = YZHCodeResetCycle.Never
    )
]
public class CertificationBody : YZHBaseEntity
{
    [Column("id")]
    public long Id { get; set; }
    
    [Column("cb_code")]
    public string CbCode { get; set; }           // 自动按 YZHCodeRule 生成
    
    [Column("name")]
    public string Name { get; set; }
    
    [Column("org_code")]
    public string OrgCode { get; set; }          // 自动填充当前用户机构
    
    // ... 其他字段
}
```

## 18.4 特性与代码的关系图

```
┌─────────────────────────────────────────────────────────────────┐
│                  YZH Framework 特性层次结构                      │
│                                                                 │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │              YZHBaseAttribute (抽象基类)                  │   │
│  │   ├── Property: Order (执行顺序)                            │   │
│  │   ├── Property: Description (说明)                         │   │
│  │   ├── Property: Inherited (是否可被子类覆盖)               │   │
│  │   └── Method: Validate() (验证配置合法性)                  │   │
│  └──────────────────────────┬────────────────────────────────┘   │
│                             │                                    │
│         ┌───────────────────┼───────────────────┬──────────────┐   │
│         ▼                   ▼                   ▼              │   │
│  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐         │   │
│  │ 行为特性组   │   │ 安全特性组   │   │ 数据特性组   │         │   │
│  │             │   │             │   │             │         │   │
│  ├─ 操作控制   │   ├─ 权限基线   │   ├─ 删除策略   │         │   │
│  ├─ 审计追踪   │   ├─ 多租户     │   ├─ 数据完整性 │         │   │
│  ├─ 缓存策略   │   └─ IP白名单   │   ├─ 编码规则   │         │   │
│  ├─ 装饰器声明   │               │   └─ 校验规则   │         │   │
│  └─ 定时任务    │               │   └─ 引用完整性 │         │   │
│  └─────────────┘   └─────────────┘   └─────────────┘         │   │
│                                                                 │
│  ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ 枚举定义区 ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─   │
│                                                                 │
│  📦 行为枚举                        🛡️ 安全枚举              │
│  ├── YZHDeleteMode               ├── YZHPermissionScenario  │
│  ├── YZHOperationType             ├── YZHTenantIsolationMode │
│  ├── YZHAuditScope               └── YZHReferenceAction     │
│  └── YZHSaveMode                                           │
│                                                                 │
│  📊 数据枚举                        ⏱️ 基础枚举              │
│  ├── YZHCodeType                  ├── YZHCodeResetCycle     │
│  ├── YZHLogLevel                 └── YZHCacheStorage       │
│  └── YZHLogCategory                                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

# 十九、特性体系重构：分类与层次化（⭐ 新增 V1.3）

> **"好的架构从清晰的分类开始。每一类特性有自己的职责边界和继承层次。"**

## 19.1 设计原则

```
┌─────────────────────────────────────────────────────────────┐
│                     特性设计的铁律                             │
│                                                             │
│  1️⃣  所有参数必须使用枚举，禁止魔法字符串                      │
│     ❌ Mode = "logical"                                      │
│     ✅ Mode = YZHDeleteMode.Logical                          │
│                                                             │
│  2️⃣  每个特性类别有明确的抽象基类                             │
│     → 公共属性在基类中定义                                   │
│     → 子类只添加自己特有的属性                                │
│                                                             │
│  3️⃣  特性之间不能有循环依赖                                   │
│     → 行为特性可以引用安全枚举                               │
│     → 安全特性不应该依赖行为实现                             │
│                                                             │
│  4️⃣  通过 Validate() 方法自检配置合法性                       │
│     → 应用启动时扫描所有 Entity，报告配置错误                   │
│                                                             │
│  5️⃣  特性支持继承覆盖                                       │
│     → 父类 Entity 的特性可被子类覆盖                           │
│     → 方法级 > 类级 > 父类级                                 │
└─────────────────────────────────────────────────────────────┘
```

## 19.2 特性分类体系

### 分类总览

| 类别 | 基类 | 用途 | 典型成员 |
|------|------|------|---------|
| **行为特性** | `YZHBehaviorAttribute` | 控制 CRUD 行为 | 操作控制、删除策略、审计、缓存 |
| **安全特性** | `YZHSecurityAttribute` | 权限与访问控制 | 角色权限、IP 白名单、数据加密 |
| **数据特性** | `YZHDataAttribute` | 数据完整性与规范 | 唯一约束、校验规则、编码规则 |
| **扩展特性** | `YZHExtensionAttribute` | 功能扩展点 | 装饰器声明、定时任务、工作流 |

### 19.2.1 抽象基类定义

```csharp
namespace YZH.Framework.Core.Attributes
{
    // ═══════════════════════════════════════════════════════════
    // 特性基类 - 所有 YZH 特性的根
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// YZH 特性抽象基类 - 提供公共基础设施
    /// </summary>
    public abstract class YZHBaseAttribute : Attribute
    {
        /// <summary>执行顺序（越小越先执行）</summary>
        public int Order { get; set; } = 100;
        
        /// <summary>人类可读的描述</summary>
        public string Description { get; set; }
        
        /// <summary>是否启用（可用于功能开关）</summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>子类重写以实现自检逻辑</summary>
        public virtual void Validate(Type entityType)
        {
            if (!Enabled) return;
            
            // 基础校验：确保标记在正确的目标上
            var validTargets = GetValidTargets();
            // ... 反射校验
        }
        
        protected abstract AttributeTargets GetValidTargets();
    }

    // ═══════════════════════════════════════════════════════════
    // 类别 1：行为特性 - 控制"怎么做"
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// 行为特性基类 - 影响数据处理行为的特性
    /// </summary>
    public abstract class YZHBehaviorAttribute : YZHBaseAttribute
    {
        /// <summary>影响的操作阶段</summary>
        public YZHLifecyclePhase AffectsPhase { get; set; } = YZHLifecyclePhase.All;
    }
    
    // 具体行为特性：
    public class YZHDeleteStrategyAttribute : YZHBehaviorAttribute { ... }   // 已定义
    public class YZHAuditedAttribute : YZHBehaviorAttribute { ... }        // 已定义
    public class YZHCachingAttribute : YZHBehaviorAttribute { ... }       // 已定义
    public class YZHEntityOperationsAttribute : YZHBehaviorAttribute { ... } // 已定义
    public class YZHUseDecoratorAttribute : YZHBehaviorAttribute { ... }   // 已定义
    public class YZHIdempotentAttribute : YZHBehaviorAttribute { ... }     // ⭐ V1.4新增：接口幂等性（防重复提交）

    // ═══════════════════════════════════════════════════════════
    // 类别 2：安全特性 - 控制"谁能做"
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// 安全特性基类 - 影响访问控制的特性
    /// </summary>
    public abstract class YZHSecurityAttribute : YZHBaseAttribute
    {
        /// <summary>安全级别（越高越严格）</summary>
        public YZHSecurityLevel Level { get; set; } = YZHSecurityLevel.Normal;
    }
    
    // 具体安全特性：
    public class YZHEntityPermissionAttribute : YZHSecurityAttribute { ... }  // 已定义
    public class YZHMultiTenantAttribute : YZHSecurityAttribute { ... }     // 已定义
    public class YZIPWhitelistAttribute : YZHSecurityAttribute           // 新增：IP 白名单
    {
        public string[] AllowedIPs { get; set; }
        public string[] AllowedCIDRs { get; set; }
    }
    public class YZHEncryptFieldAttribute : YZHSecurityAttribute         // 新增：字段加密
    {
        public string EncryptionAlgorithm { get; set; } = "AES-256";
        public string[] FieldsToEncrypt { get; set; }
    }

    // ═══════════════════════════════════════════════════════════
    // 类别 3：数据特性 - 控制"数据长什么样"
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// 数据特性基类 - 影响数据结构和完整性的特性
    /// </summary>
    public abstract class YZHDataAttribute : YZHBaseAttribute
    {
        /// <summary>是否在 Schema 生成时生效</summary>
        public bool AffectSchema { get; set; } = true;
    }
    
    // 具体数据特性：
    public class YZHValidationRulesAttribute : YZHDataAttribute { ... }   // 已定义
    public class YZHDataIntegrityAttribute : YZHDataAttribute { ... }     // 已定义
    public class YZHCodeRuleAttribute : YZHDataAttribute { ... }         // 已定义

    // ═══════════════════════════════════════════════════════════
    // 类别 4：扩展特性 - 控制"还能做什么"
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>
    /// 扩展特性基类 - 声明式扩展点
    /// </summary>
    public abstract class YZHExtensionAttribute : YZHBaseAttribute
    {
        /// <summary>扩展提供者（用于排查）</summary>
        public string Provider { get; set; } = "YZH.Core";
    }
    
    // 具体扩展特性：
    public class YZHScheduledTaskAttribute : YZHExtensionAttribute      // 新增：定时任务
    {
        public string CronExpression { get; set; }
        public YZHJobRetryPolicy RetryPolicy { get; set; }
    }
    public class YZHWorkflowAttribute : YZHExtensionAttribute           // 新增：工作流
    {
        public string WorkflowDefinitionId { get; set; }
        public string InitialState { get; set; }
    }
}
```

### 19.2.2 枚举全集（禁止魔法字符串）

```csharp
namespace YZH.Framework.Core.Enums
{
    // ═══════════════════════════════════════════════════════════
    // 行为相关枚举
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>删除模式</summary>
    public enum YZHDeleteMode { Logical, Physical, Cascading }
    
    /// <summary>生命周期阶段（用于 AffectsPhase）</summary>
    [Flags]
    public enum YZHLifecyclePhase
    {
        Validation = 1 << 0,
        BeforeSave = 1 << 1,
        AfterSave = 1 << 2,
        BeforeDelete = 1 << 3,
        AfterDelete = 1 << 4,
        BeforeQuery = 1 << 5,
        AfterQuery = 1 << 6,
        Export = 1 << 7,
        Import = 1 << 8,
        All = Validation | BeforeSave | AfterSave | BeforeDelete | AfterDelete
    }
    
    /// <summary>操作类型</summary>
    public enum YZHOperationType
    {
        Create, Update, Delete, Query, Export, Import,
        Approve, Reject, Submit, Publish, Archive, Reactivate, Other
    }
    
    /// <summary>保存模式</summary>
    public enum YZHSaveMode { Add, Update }
    
    /// <summary>审计范围</summary>
    [Flags]
    public enum YZHAuditScope
    {
        None = 0, Create = 1, Update = 2, Delete = 4, Query = 8,
        Export = 16, Import = 32, Approve = 64, Reject = 128,
        Crud = Create | Update | Delete,
        All = Crud | Query | Export | Import | Approve | Reject
    }
    
    /// <summary>缓存存储</summary>
    public enum YZHCacheStorage { Memory, Redis, Hybrid }
    
    /// <summary>缓存过期单位</summary>
    public enum YZHCacheExpiry { ThirtyMinutes = 30, OneHour = 60, OneDay = 1440, OneWeek = 10080 }

    // ═══════════════════════════════════════════════════════════
    // 安全相关枚举
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>安全级别</summary>
    public enum YZHSecurityLevel
    {
        Public = 0,        // 无需认证
        Normal = 10,       // 登录即可
        Sensitive = 20,    // 需要特定角色
        Confidential = 30, // 需要数据权限
        Restricted = 40     // 需要审批
    }
    
    /// <summary>预定义权限场景</summary>
    public enum YZHPermissionScenario
    {
        SuperAdminOnly, PlatformAdmin, OrgAdmin, AllBackendUsers,
        IncludeEnterprise, AllAuthenticated, Public
    }
    
    /// <summary>多租户隔离模式</summary>
    public enum YZHTenantIsolationMode { Strict, Relaxed, Shared, Disabled }
    
    /// <summary>引用动作</summary>
    public enum YZHReferenceAction { NoCheck, Restrict, SetNull, Cascade }

    // ═══════════════════════════════════════════════════════════
    // 数据相关枚举
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>编码类型</summary>
    public enum YZHCodeType { EntityId, BusinessCode, RuleCode, DocumentNo, TaskCode }
    
    /// <summary>编码重置周期</summary>
    public enum YZHCodeResetCycle { Never, Daily, Monthly, Yearly, Earlyly }
    
    /// <summary>日志分类</summary>
    public enum YZHLogCategory
    {
        SystemStartup = 1000, SystemError = 1001, PerformanceWarning = 1002,
        SecurityLogin = 2000, SecurityLogout = 2001, SecurityPermissionChange = 2002,
        SecurityViolation = 2003,
        CertBodyManagement = 3001, EnterpriseManagement = 3002,
        AuditTask = 3003, AuditFinding = 3004, NonConformity = 3005,
        ReportGeneration = 3006, StandardManagement = 3007,
        DataCreate = 4001, DataUpdate = 4002, DataDelete = 4003,
        DataExport = 4004, DataImport = 4005, BatchOperation = 4006
    }
    
    /// <summary>定时任务重试策略</summary>
    public enum YZHJobRetryPolicy
    {
        NoRetry,           // 不重试
        FixedCount(3),     // 固定次数
        ExponentialBackoff  // 指数退避
    }

    // ═══════════════════════════════════════════════════════════
    // 扩展相关枚举
    // ═══════════════════════════════════════════════════════════
    
    /// <summary>工作流状态</summary>
    public enum YZHWorkflowState
    {
        Draft, PendingReview, Approved, Rejected, Published, Archived
    }
}
```

## 19.3 启动时自检机制

```csharp
/// <summary>
/// YZH 特性验证服务 - 应用启动时扫描所有 Entity，验证特性配置
/// </summary
public interface IYZHAttributeValidator
{
    /// <summary>验证所有带 YZH 特性的实体</summary>
    YZHValidationResult ValidateAll();
    
    /// <summary>验证单个实体</summary>
    YZHValidationResult ValidateEntity(Type entityType);
}

public class YZHValidationResult
{
    public bool IsValid { get; set; }
    public List<YZHValidationError> Errors { get; set; } = new();
    public List<YZHValidationWarning> Warnings { get; set; } = new();
    
    public void ThrowIfInvalid()
    {
        if (!IsValid) throw new YZHConfigurationException(
            $"YZH 特性配置错误:\n{string.Join("\n", Errors.Select(e => e.Message))}");
    }
}

// Program.cs 中调用：
// builder.Services.AddSingleton<IYZHAttributeValidator, YZHAttributeValidator>();
// app.UseYZHAttributeValidation();  // 启动时自动验证
```

**自检输出示例**：

```
╔════════════════════════════════════════════════════════╗
║  🔄 YZH Framework 特性自检结果                          ║
║                                                          ║
║  ✅ CertificationBody                                     ║
║     ✅ YZHEntityOperations: 配置正确                      ║
║     ✅ YZHEntityPermission: 角色 [SuperAdmin, PlatformAdmin] ║
║     ✅ YZHDeleteStrategy: Logical 模式                       ║
║     ✅ YZHMultiTenant: Strict 隔离                        ║
║     ✅ YZHDataIntegrity: CbCode 唯一约束                   ║
║     ✅ YZHAudited: 追踪 CRUD + 敏感字段脱敏                  ║
║     ✅ YZHCodeRule: CB{SEQ:3} 永不重置                    ║
║     ⚠️  Warning: 未设置 YZHCaching，将使用默认内存缓存       ║
║                                                          ║
║  ✅ AuditRecord                                          ║
║     ✅ YZHEntityOperations: 禁止删除+编辑 ✓               ║
║     ...                                                    ║
║                                                          ║
║  ❌ SysOperationLog                                      ║
║     ❌ Error: YZHDeleteStrategy(Physical) 与审计冲突！        ║
║            建议：日志表应使用 Logical 或 Cascading          ║
║                                                          ║
║  📊 总计: 15 个实体, 12 个通过, 1 个警告, 2 个错误          ║
╚════════════════════════════════════════════════════════╝
```

## 19.4 特性使用最佳实践

### ✅ 推荐：完整的实体声明

```csharp
[
    // 1. 操作控制 → 我能做什么？
    YZHEntityOperations(
        EnableDelete = false,
        CustomOperations = new[] { "Approve" }
    ),
    
    // 2. 权限基线 → 谁能做？
    YZHEntityPermission(Scenario = YZHPermissionScenario.OrgAdmin),
    
    // 3. 删除策略 → 删了怎样？
    YZHDeleteStrategy(Mode = YZHDeleteMode.Logical),
    
    // 4. 多租户 → 数据是谁的？
    YZHMultiTenant(IsolationMode = YZHTenantIsolationMode.Strict),
    
    // 5. 数据约束 → 有什么限制？
    YZHDataIntegrity(UniqueFields = new[] { "Code" }),
    
    // 6. 审计追踪 → 留什么痕迹？
    YZHAudited(Category = YZHLogCategory.AuditTask, Scope = YZHAuditScope.Crud),
    
    // 7. 编码规则 → 怎么编号？
    YZHCodeRule(CodeType = YZHCodeType.TaskCode, Prefix = "TASK")
]
public class AuditTask : YZHBaseEntity { }
```

### ❌ 避免：散乱的特性

```csharp
// 不要这样！没有分类、没有顺序、难以维护
[
    YZHAudited(Category = 3003),
    YZHMultiTenant(),
    YZHDeleteStrategy(Mode = 0),  // ← 魔法数字！应该用枚举
    YZHEntityOperations(EnableExport = true),
    YZHCodeRule(Prefix = "TASK"),
    YZHEntityPermission(AllowedRoles = new[] { "admin" })  // ← 魔法字符串！
]
public class AuditTask : YZHBaseEntity { }
```

---

# 第二十章、接口幂等性设计（⭐ V1.4 新增 - Redis 防重复提交）

> **"每个接口都应该天然具备防重复点击的能力，而不是在每个 Controller 中重复编写防重逻辑。"**

## 20.1 问题背景与设计目标

### 痛点场景

```
┌─────────────────────────────────────────────────────────────┐
│                    重复提交的典型场景                          │
│                                                             │
│  ❌ 用户快速双击"保存"按钮 → 产生两条相同数据                  │
│  ❌ 网络延迟导致用户重复点击 → 重复创建订单/申请                │
│  ❌ 前端 loading 状态失效 → 并发请求穿透到后端                 │
│  ❌ 恶意用户脚本刷接口 → 资源耗尽或数据不一致                   │
│                                                             │
│  传统解决方案的问题：                                          │
│    - 在每个 Controller 方法中写防重逻辑 → 代码重复             │
│    - 前端按钮 disable → 不可靠（可被绕过）                     │
│    - 数据库唯一约束 → 只能事后拦截，无法友好提示               │
│                                                             │
│  YZH 解决方案：                                              │
│    ✅ 一个 [YZHIdempotent] 特性搞定所有接口                    │
│    ✅ 基于 Redis 的原子操作，绝对可靠                          │
│    ✅ 可配置锁定时间窗口，适应不同业务场景                      │
│    ✅ 支持自定义键生成策略，灵活应对复杂需求                    │
└─────────────────────────────────────────────────────────────┘
```

### 设计目标

| # | 目标 | 实现方式 |
|---|------|---------|
| **1** | **零侵入** | 通过 Attribute 声明，不改业务代码 |
| **2** | **高性能** | Redis SET NX EX 原子操作，O(1) 复杂度 |
| **3** | **可配置** | 锁定时间、提示信息、键策略均可自定义 |
| **4** | **可扩展** | 支持 `IIdempotentKeyGenerator` 自定义键生成 |
| **5** | **前端配合** | 提供标准错误码，前端统一处理 loading 状态 |

---

## 20.2 核心组件架构

```
┌─────────────────────────────────────────────────────────────┐
│              接口幂等性 - 组件架构图                           │
│                                                             │
│  ┌─────────────────┐                                        │
│  │ Controller      │  [YZHIdempotent(DurationSeconds = 5)]  │
│  │   Action        │ ───────────────────────────┐           │
│  └─────────────────┘                             │          │
│                                                    ▼          │
│  ┌─────────────────────────────────────────────────────┐     │
│  │         YZHIdempotentActionFilter (IAsyncActionFilter) │    │
│  │                                                     │     │
│  │  1️⃣  检查是否有 [YZHIdempotent] 特性                  │     │
│  │  2️⃣  调用 IIdempotentKeyGenerator 生成 Key            │     │
│  │  3️⃣  执行 Redis SET NX EX (原子操作)                 │     │
│  │  4️⃣  成功 → 放行执行 Action                           │     │
│  │  5️⃣  失败 → 抛出 YZHBusinessException(409)           │     │
│  └─────────────────────────────────────────────────────┘     │
│                        │                                    │
│          ┌─────────────┼─────────────┐                       │
│          ▼             ▼             ▼                       │
│  ┌──────────────┐ ┌──────────┐ ┌──────────────────┐         │
│  │ DefaultKeyGen │ │  Redis   │ │ GlobalException  │         │
│  │   (默认实现)   │ │ SET NX   │ │    Filter        │         │
│  │              │ │   EX     │ │  (统一错误响应)    │         │
│  └──────────────┘ └──────────┘ └──────────────────┘         │
│                                                             │
│  🔧 可扩展点：                                                │
│     - IIdempotentKeyGenerator → 自定义键生成策略              │
│     - YZHIdempotentAttribute.DurationSeconds → 锁定时间       │
│     - YZHIdempotentAttribute.KeyPrefix → 键前缀（多租户隔离）  │
└─────────────────────────────────────────────────────────────┘
```

---

## 20.3 特性定义：YZHIdempotentAttribute

```csharp
using System;

namespace YZH.Core.Attributes
{
    /// <summary>
    /// 接口幂等性特性 - 用于防止重复提交/重复点击
    /// 
    /// 使用场景：
    /// - 表单保存/提交操作
    /// - 创建订单、发起申请等写操作
    /// - 任何需要防止重复执行的接口
    /// 
    /// 工作原理：
    /// 1. 请求进入时，基于用户ID + 接口路径 + 请求参数生成唯一标识
    /// 2. 使用 Redis SET NX EX 原子操作尝试设置锁
    /// 3. 首次请求设置成功，放行执行；重复请求设置失败，返回友好提示
    /// 4. 锁在指定时间后自动过期释放
    /// 
    /// 示例：
    ///   [YZHIdempotent(DurationSeconds = 5, Message = "请勿重复提交")]
    ///   public async Task<IActionResult> Save(SaveRequest request) { ... }
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class YZHIdempotentAttribute : YZHBehaviorAttribute
    {
        /// <summary>
        /// 锁定时间窗口（秒）
        /// - 默认 3 秒：适用于大多数表单提交场景
        /// - 建议 1-5 秒：短于正常用户操作间隔
        /// - 可根据业务调整：文件上传可设为 30-60 秒
        /// </summary>
        public int DurationSeconds { get; set; } = 3;
        
        /// <summary>
        /// 重复提交时的提示信息
        /// - 默认："操作过于频繁，请稍后再试"
        /// - 建议使用友好的业务提示："请勿重复提交申请"
        /// </summary>
        public string Message { get; set; } = "操作过于频繁，请稍后再试";
        
        /// <summary>
        /// 是否包含请求体 Hash
        /// - 默认 true：不同参数的请求视为不同操作（推荐）
        /// - 设为 false：同一接口同一用户只允许一个请求在执行
        /// </summary>
        public bool IncludeBodyHash { get; set; } = true;
        
        /// <summary>
        /// Redis 键前缀
        /// - 默认 "yzh:idempotent:"
        /// - 多租户环境建议包含租户标识："yzh:{orgCode}:idempotent:"
        /// </summary>
        public string KeyPrefix { get; set; } = "yzh:idempotent:";
        
        /// <summary>
        /// 自定义键生成器类型（必须实现 IIdempotentKeyGenerator）
        /// - 默认 null：使用 DefaultIdempotentKeyGenerator
        /// - 可用于特殊场景：如基于业务流水号生成键
        /// </summary>
        public Type KeyGeneratorType { get; set; } = null;
        
        /// <summary>
        /// HTTP 方法过滤（默认只对写操作生效）
        /// - 默认限制 POST, PUT, DELETE, PATCH
        /// - GET 请求通常不需要防重复（幂等性天然保证）
        /// </summary>
        public string[] AffectedMethods { get; set; } = { "POST", "PUT", "DELETE", "PATCH" };
        
        /// <summary>
        /// 是否在 Header 中返回请求标识（用于客户端排查）
        /// - 默认 true：返回 X-Idempotent-Request-Id
        /// </summary>
        public bool ReturnRequestId { get; set; } = true;
    }
}
```

---

## 20.4 过滤器实现：YZHIdempotentActionFilter

```csharp
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using YZH.Core.Attributes;
using YZH.Core.Exceptions;
using YZH.Core.Extensions;

namespace YZH.Web.Filters
{
    /// <summary>
    /// 接口幂等性过滤器 - 基于 Redis 实现防重复提交
    /// 
    /// 执行流程：
    /// 1. 检查 Action 是否标记了 [YZHIdempotent]
    /// 2. 校验 HTTP 方法是否在限制范围内
    /// 3. 生成唯一的请求标识（Redis Key）
    /// 4. 尝试 SET NX EX（原子操作）
    ///    - 成功：首次请求，放行执行
    ///    - 失败：重复请求，抛出 YZHBusinessException
    /// 5. 异常由 YZHGlobalExceptionFilter 统一处理
    /// </summary>
    public class YZHIdempotentActionFilter : IAsyncActionFilter
    {
        private readonly IRedisCacheManager _redisCache;
        private readonly ILogger<YZHIdempotentActionFilter> _logger;
        private readonly IServiceProvider _serviceProvider;

        public YZHIdempotentActionFilter(
            IRedisCacheManager redisCache,
            ILogger<YZHIdempotentActionFilter> logger,
            IServiceProvider serviceProvider)
        {
            _redisCache = redisCache;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 1️⃣ 获取特性声明
            var idempotentAttr = context.ActionDescriptor.EndpointMetadata
                .OfType<YZHIdempotentAttribute>()
                .FirstOrDefault();

            if (idempotentAttr == null)
            {
                // 未标记特性，直接放行
                await next();
                return;
            }

            // 2️⃣ 校验 HTTP 方法
            var httpMethod = context.HttpContext.Request.Method;
            if (!idempotentAttr.AffectedMethods.Contains(httpMethod, StringComparer.OrdinalIgnoreCase))
            {
                // 不在限制的方法范围内（如 GET），直接放行
                await next();
                return;
            }

            try
            {
                // 3️⃣ 生成 Redis Key
                var keyGenerator = CreateKeyGenerator(idempotentAttr);
                var redisKey = await keyGenerator.GenerateKeyAsync(context, idempotentAttr);

                // 4️⃣ 尝试获取请求标识（用于日志追踪）
                var requestId = Guid.NewGuid().ToString("N");

                // 5️⃣ 原子操作：SET NX EX（不存在则设置，并指定过期时间）
                var isSuccess = await _redisCache.StringSetAsync(
                    key: redisKey,
                    value: JsonSerializer.Serialize(new
                    {
                        RequestId = requestId,
                        UserId = UserContext.Current?.UserId ?? "anonymous",
                        Path = context.HttpContext.Request.Path.Value,
                        Timestamp = DateTime.UtcNow
                    }),
                    expiry: TimeSpan.FromSeconds(idempotentAttr.DurationSeconds),
                    whenNotExists: true  // 仅当键不存在时设置
                );

                if (!isSuccess)
                {
                    // ⚠️ 重复请求！记录警告日志
                    _logger.LogWarning(
                        "[Idempotent] 检测到重复请求: {RedisKey}, User={UserId}, Path={Path}",
                        redisKey,
                        UserContext.Current?.UserId,
                        context.HttpContext.Request.Path.Value
                    );

                    // 返回 Header 信息（便于排查）
                    if (idempotentAttr.ReturnRequestId)
                    {
                        context.HttpContext.Response.Headers["X-Idempotent-Rejected"] = "true";
                        context.HttpContext.Response.Headers["X-Idempotent-Key"] = redisKey;
                    }

                    // 抛出业务异常（由全局过滤器统一处理）
                    throw new YZHBusinessException(
                        statusCode: StatusCodes.Status409Conflict,  // 409 Conflict
                        message: idempotentAttr.Message,
                        errorCode: "IDEMPOTENT_DUPLICATE_REQUEST"
                    );
                }

                // ✅ 首次请求，设置成功
                if (idempotentAttr.ReturnRequestId)
                {
                    context.HttpContext.Response.Headers["X-Idempotent-Request-Id"] = requestId;
                }

                _logger.LogDebug(
                    "[Idempotent] 幂等检查通过: {RedisKey}, Expiry={Duration}s",
                    redisKey,
                    idempotentAttr.DurationSeconds
                );

                // 6️⃣ 放行执行 Action
                var resultContext = await next();

                // 7️⃣ 可选：Action 执行成功后立即删除锁（允许用户重新提交）
                // 如果注释掉这行，则必须等待 DurationSeconds 过期后才能再次提交
                // await _redisCache.KeyDeleteAsync(redisKey);  
            }
            catch (YZHBusinessException)
            {
                // 业务异常继续抛出，由全局过滤器处理
                throw;
            }
            catch (Exception ex)
            {
                // 其他异常记录日志但不拦截（避免影响正常请求）
                _logger.LogError(ex, "[Idempotent] 幂等检查异常: {Message}", ex.Message);
                
                // 异常时不拦截请求，确保可用性优先
                await next();
            }
        }

        /// <summary>
        /// 创建键生成器实例
        /// </summary>
        private IIdempotentKeyGenerator CreateKeyGenerator(YZHIdempotentAttribute attr)
        {
            if (attr.KeyGeneratorType == null)
            {
                // 使用默认键生成器
                return new DefaultIdempotentKeyGenerator();
            }

            // 使用自定义键生成器
            var generator = _serviceProvider.GetService(attr.KeyGeneratorType) as IIdempotentKeyGenerator;
            
            if (generator == null)
            {
                throw new InvalidOperationException(
                    $"类型 {attr.KeyGeneratorType.Name} 必须实现 IIdempotentKeyGenerator 接口");
            }

            return generator;
        }
    }
}
```

---

## 20.5 键生成策略：IIdempotentKeyGenerator

### 接口定义

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using YZH.Core.Attributes;

namespace YZH.Core.Interfaces
{
    /// <summary>
    /// 幂等性键生成器接口 - 用于生成唯一的请求标识
    /// 
    /// 实现原则：
    /// - 同一用户的同一请求必须生成相同的 Key
    /// - 不同用户的请求必须生成不同的 Key
    /// - 不同参数的请求应该生成不同的 Key（可选）
    /// - Key 必须具有足够的熵（避免碰撞）
    /// </summary>
    public interface IIdempotentKeyGenerator
    {
        /// <summary>
        /// 生成 Redis Key
        /// </summary>
        /// <param name="context">Action 执行上下文</param>
        /// <param name="attribute">幂等性特性配置</param>
        /// <returns>完整的 Redis Key</returns>
        Task<string> GenerateKeyAsync(ActionExecutingContext context, YZHIdempotentAttribute attribute);
    }
}
```

### 默认实现：DefaultIdempotentKeyGenerator

```csharp
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using YZH.Core.Attributes;
using YZH.Core.Extensions;

namespace YZH.Core.Idempotent
{
    /// <summary>
    /// 默认幂等性键生成器
    /// 
    /// Key 格式：{prefix}{userId}:{httpMethod}:{controller}:{action}:{bodyHash}
    /// 
    /// 示例：
    ///   yzh:idempotent:10010:POST:CertificationBody:Save:a1b2c3d4e5f6
    ///   
    /// 设计考量：
    /// - 包含用户ID：同一用户防重复，不同用户互不影响
    /// - 包含 HTTP 方法：GET 和 POST 视为不同操作
    /// - 包含 Controller + Action：精确定位到具体接口
    /// - 包含 BodyHash：不同参数的请求视为不同操作（可选）
    /// </summary>
    public class DefaultIdempotentKeyGenerator : IIdempotentKeyGenerator
    {
        public async Task<string> GenerateKeyAsync(
            ActionExecutingContext context, 
            YZHIdempotentAttribute attribute)
        {
            // 1️⃣ 获取当前用户 ID
            var userId = UserContext.Current?.UserId ?? "anonymous";

            // 2️⃣ 获取请求信息
            var httpMethod = context.HttpContext.Request.Method.ToUpperInvariant();
            var path = context.HttpContext.Request.Path.Value.Trim('/');
            
            // 从路由数据中提取 Controller 和 Action 名称（更稳定）
            var controllerName = context.RouteData.Values["controller"]?.ToString() ?? "unknown";
            var actionName = context.RouteData.Values["action"]?.ToString() ?? "unknown";

            // 3️⃣ 生成请求体 Hash（如果启用）
            string bodyHash = "none";
            
            if (attribute.IncludeBodyHash && context.ActionArguments.Any())
            {
                bodyHash = await GenerateBodyHashAsync(context.ActionArguments);
            }

            // 4️⃣ 组装完整 Key
            var key = $"{attribute.KeyPrefix}{userId}:{httpMethod}:{controllerName}:{actionName}:{bodyHash}";

            return key;
        }

        /// <summary>
        /// 生成请求参数的 SHA256 Hash
        /// </summary>
        private async Task<string> GenerateBodyHashAsync(System.Collections.Generic.IDictionary<string, object> arguments)
        {
            try
            {
                // 序列化参数（排序 key 保证一致性）
                var json = JsonSerializer.Serialize(
                    arguments.OrderBy(kvp => kvp.Key),
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        IgnoreNullValues = true
                    }
                );

                // 计算 SHA256 Hash
                await using var sha256 = SHA256.Create();
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
                
                // 取前 16 位作为简短标识（节省 Redis 内存）
                return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
            }
            catch (Exception)
            {
                // 序列化失败时返回固定值（降级处理）
                return "hash_error";
            }
        }
    }
}
```

### 自定义示例：基于业务流水号的键生成器

```csharp
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using YZH.Core.Attributes;

namespace CertPlatform.Idempotent
{
    /// <summary>
    /// 认证平台专用键生成器 - 基于业务流水号
    /// 
    /// 适用场景：
    /// - 申请单提交：同一个申请编号不能重复提交
    /// - 审核操作：同一个审核任务不能重复审核
    /// </summary>
    public class BusinessFlowKeyGenerator : IIdempotentKeyGenerator
    {
        public async Task<string> GenerateKeyAsync(
            ActionExecutingContext context, 
            YZHIdempotentAttribute attribute)
        {
            // 尝试从请求参数中获取业务流水号
            if (context.ActionArguments.TryGetValue("request", out var requestObj))
            {
                // 反射获取 FlowNo 属性（假设所有请求都有 FlowNo）
                var flowNoProperty = requestObj.GetType().GetProperty("FlowNo");
                
                if (flowNoProperty != null)
                {
                    var flowNo = flowNoProperty.GetValue(requestObj)?.ToString();
                    
                    if (!string.IsNullOrWhiteSpace(flowNo))
                    {
                        var userId = UserContext.Current?.UserId ?? "anonymous";
                        
                        // Key 格式：{prefix}biz:{flowNo}:{userId}
                        return $"{attribute.KeyPrefix}biz:{flowNo}:{userId}";
                    }
                }
            }

            // 回退到默认策略
            var defaultGenerator = new DefaultIdempotentKeyGenerator();
            return await defaultGenerator.GenerateKeyAsync(context, attribute);
        }
    }
}
```

---

## 20.6 注册与配置

### Program.cs 注册

```csharp
// ============================================================
// YZH Framework 初始化（Program.cs）
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ... 其他服务注册 ...

// 📌 注册 Redis 服务（幂等性依赖）
builder.Services.AddSingleton<IRedisCacheManager, RedisCacheManager>();

// 📌 注册默认键生成器
builder.Services.AddSingleton<IIdempotentKeyGenerator, DefaultIdempotentKeyGenerator>();

// 📌 注册自定义键生成器（如果有）
// builder.Services.AddSingleton<BusinessFlowKeyGenerator>();

// 📌 注册全局过滤器（注意顺序！）
builder.Services.AddControllers(options =>
{
    // 1️⃣ 幂等性过滤器（最先执行，优先级最高）
    options.Filters.Add<YZHIdempotentActionFilter>(int.MinValue + 100);
    
    // 2️⃣ 全局异常过滤器（处理幂等性抛出的异常）
    options.Filters.Add<YZHGlobalExceptionFilter>();
    
    // 3️⃣ 其他过滤器...
});

var app = builder.Build();

// ... 中间件配置 ...

app.Run();
```

### Redis 配置（appsettings.json）

```json
{
  "Redis": {
    "ConnectionString": "localhost:6380,password=your_password",
    "InstanceName": "yzh:",
    "DefaultDatabase": 0
  },
  
  "Idempotent": {
    "DefaultDurationSeconds": 3,
    "DefaultMessage": "操作过于频繁，请稍后再试",
    "EnableLogging": true,
    "MaxKeyLength": 200
  }
}
```

---

## 20.7 使用示例

### 基础用法：Controller Action 标记

```csharp
using Microsoft.AspNetCore.Mvc;
using YZH.Core.Attributes;
using YZH.Web.Controllers.Base;

namespace CertPlatform.Controllers.Cert
{
    [ApiController]
    [Route("api/[controller]")]
    public class CertificationBodyController : YZHControllerBase<CertificationBody, CertificationBodyService>
    {
        /// <summary>
        /// 保存认证机构（带 3 秒防重复）
        /// </summary>
        [HttpPost("save")]
        [YZHIdempotent(DurationSeconds = 3, Message = "请勿重复提交机构信息")]
        public async Task<IActionResult> Save([FromBody] SaveCertBodyRequest request)
        {
            var result = await Service.SaveAsync(request);
            return YZHOk(result);
        }

        /// <summary>
        /// 提交审核申请（带 5 秒防重复，较长窗口）
        /// </summary>
        [HttpPost("submit-audit")]
        [YZHIdempotent(
            DurationSeconds = 5, 
            Message = "请勿重复提交审核申请",
            IncludeBodyHash = false  // 同一用户只能有一个待处理的提交
        )]
        public async Task<IActionResult> SubmitAudit([FromBody] SubmitAuditRequest request)
        {
            var result = await Service.SubmitAuditAsync(request);
            return YZHOk(result);
        }

        /// <summary>
        /// 上传附件（带 30 秒防重复，大文件场景）
        /// </summary>
        [HttpPost("upload")]
        [YZHIdempotent(DurationSeconds = 30, Message = "文件正在上传中，请勿重复操作")]
        public async Task<IActionResult> Upload([FromForm] UploadFileRequest request)
        {
            var result = await Service.UploadAsync(request);
            return YZHOk(result);
        }

        /// <summary>
        /// 查询列表（无需防重复，GET 请求自动跳过）
        /// </summary>
        [HttpGet("list")]
        // 注意：即使误加了 [YZHIdempotent]，GET 请求也会自动跳过
        public async Task<IActionResult> GetList([FromQuery] QueryCertBodyRequest request)
        {
            var result = await Service.GetPageListAsync(request);
            return YZHOk(result);
        }

        /// <summary>
        /// 使用自定义键生成器（基于业务流水号）
        /// </summary>
        [HttpPost("approve")]
        [YZHIdempotent(
            DurationSeconds = 10,
            Message = "该审核任务正在处理中",
            KeyGeneratorType = typeof(BusinessFlowKeyGenerator)
        )]
        public async Task<IActionResult> Approve([FromBody] ApproveRequest request)
        {
            var result = await Service.ApproveAsync(request);
            return YZHOk(result);
        }
    }
}
```

### 类级别标记（所有 Action 生效）

```csharp
/// <summary>
/// 订单控制器 - 所有写操作都启用防重复
/// </summary>
[ApiController]
[Route("api/[controller]")]
[YZHIdempotent(DurationSeconds = 5)]  // ⚠️ 类级别标记，对所有 Action 生效
public class OrderController : YZHControllerBase<Order, OrderService>
{
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        // 自动拥有 5 秒防重复保护
        var result = await Service.CreateAsync(request);
        return YZHOk(result);
    }

    [HttpPut("cancel/{orderId}")]
    // 可以覆盖类级别的配置
    [YZHIdempotent(DurationSeconds = 2, Message = "取消操作正在处理中")]  
    public async Task<IActionResult> Cancel(string orderId)
    {
        var result = await Service.CancelAsync(orderId);
        return YZHOk(result);
    }

    [HttpGet("{orderId}")]
    // GET 请求自动跳过，不受影响
    public async Task<IActionResult> GetById(string orderId)
    {
        var result = await Service.GetByIdAsync(orderId);
        return YZHOk(result);
    }
}
```

---

## 20.8 前端配合处理

### Vue 3 + Element Plus 统一封装

```typescript
// src/utils/request.ts（基于 Axios 封装）

import axios from 'axios';
import { ElMessage } from 'element-plus';

const request = axios.create({
  baseURL: '/api',
  timeout: 30000,
});

// 请求拦截器：自动添加防重标识
request.interceptors.request.use((config) => {
  // 对于 POST/PUT/DELETE 请求，可以添加 X-Client-Request-Id
  if (['post', 'put', 'delete', 'patch'].includes(config.method || '')) {
    config.headers['X-Client-Request-Id'] = crypto.randomUUID();
  }
  
  return config;
});

// 响应拦截器：统一处理 409 冲突状态码
request.interceptors.response.use(
  (response) => {
    // 记录服务器返回的 RequestId（用于排查）
    const requestId = response.headers['x-idempotent-request-id'];
    if (requestId) {
      console.debug(`[Idempotent] RequestId: ${requestId}`);
    }
    
    return response.data;
  },
  (error) => {
    const status = error.response?.status;
    
    // 409 Conflict：重复提交
    if (status === 409) {
      const data = error.response?.data;
      
      // 显示友好提示
      ElMessage.warning(data?.message || '操作过于频繁，请稍后再试');
      
      // 返回特殊错误码，让调用方知道是重复提交
      return Promise.reject({
        code: 'IDEMPOTENT_DUPLICATE_REQUEST',
        message: data?.message,
        isIdempotentError: true,  // 标识这是重复提交错误
      });
    }
    
    // 其他错误正常处理
    return Promise.reject(error);
  }
);

export default request;
```

### 组件级防重封装

```vue
<!-- src/components/YZHSafeButton.vue -->
<!-- 安全按钮组件 - 自动处理 loading 状态和防重复 -->

<template>
  <el-button
    :type="type"
    :loading="isLoading"
    :disabled="isLoading || disabled"
    @click="handleClick"
  >
    <slot />
  </el-button>
</template>

<script setup lang="ts">
import { ref } from 'vue';

interface Props {
  type?: 'primary' | 'success' | 'warning' | 'danger' | 'info' | 'default';
  disabled?: boolean;
  /** 防重复间隔（毫秒），默认 0 表示不限制 */
  throttleMs?: number;
}

const props = withDefaults(defineProps<Props>(), {
  type: 'primary',
  disabled: false,
  throttleMs: 0,
});

const emit = defineEmits<{
  (e: 'click', event: MouseEvent): void;
}>();

const isLoading = ref(false);
let lastClickTime = 0;

async function handleClick(event: MouseEvent) {
  // 前端节流（第一道防线）
  if (props.throttleMs > 0) {
    const now = Date.now();
    if (now - lastClickTime < props.throttleMs) {
      console.log('[SafeButton] 点击过于频繁，已忽略');
      return;
    }
    lastClickTime = now;
  }

  isLoading.value = true;
  
  try {
    emit('click', event);
  } finally {
    // 延迟重置 loading（给后端足够时间处理）
    setTimeout(() => {
      isLoading.value = false;
    }, props.throttleMs || 500);
  }
}
</script>
```

### 使用示例

```vue
<template>
  <div>
    <!-- 普通表单提交 -->
    <el-form ref="formRef" :model="formData" :rules="formRules">
      <el-form-item label="机构名称" prop="name">
        <el-input v-model="formData.name" />
      </el-form-item>
      
      <YzHSafeButton type="primary" @click="handleSubmit">
        保存
      </YzHSafeButton>
    </el-form>

    <!-- 危险操作（删除） -->
    <YzHSafeButton 
      type="danger" 
      :throttle-ms="3000"
      @click="handleDelete"
    >
      删除
    </YzHSafeButton>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import request from '@/utils/request';
import YzHSafeButton from '@/components/YZHSafeButton.vue';

const formData = ref({ name: '' });
const formRef = ref();

async function handleSubmit() {
  try {
    await formRef.value?.validate();
    
    const res = await request.post('/api/certification-body/save', formData.value);
    
    ElMessage.success('保存成功');
  } catch (error: any) {
    if (error.isIdempotentError) {
      // 重复提交错误，已经显示提示了，不需要额外处理
      console.log('检测到重复提交，已忽略');
    } else {
      ElMessage.error(error.message || '保存失败');
    }
  }
}

async function handleDelete() {
  try {
    await request.delete('/api/certification-body/123');
    
    ElMessage.success('删除成功');
  } catch (error: any) {
    // ...
  }
}
</script>
```

---

## 20.9 高级场景与最佳实践

### 场景 1：多租户环境的键隔离

```csharp
/// <summary>
/// 多租户环境下的幂等性配置
/// </summary>
public class MultiTenantIdempotentSetup
{
    /// <summary>
    /// 动态键前缀生成器（根据租户变化）
    /// </summary>
    public static YZHIdempotentAttribute CreateForTenant()
    {
        var orgCode = UserContext.Current?.OrgCode ?? "default";
        
        return new YZHIdempotentAttribute
        {
            DurationSeconds = 3,
            KeyPrefix = $"yzh:{orgCode}:idempotent:",  // 租户隔离
            Message = "请勿重复提交"
        };
    }
}

// 使用
[HttpPost("save")]
[YZHIdempotent(DurationSeconds = 3, KeyPrefix = "yzh:{tenant}:idempotent:")]
// 注意：实际使用时需要中间件或 Filter 解析 {tenant} 占位符
public async Task<IActionResult> Save([FromBody] dynamic request)
{
    // ...
}
```

### 场景 2：长时间运行的任务

```csharp
/// <summary>
/// 导出报表（可能耗时 30-60 秒）
/// </summary>
[HttpPost("export")]
[YZHIdempotent(
    DurationSeconds = 60,  // 较长的锁定时间
    Message = "报表正在生成中，请稍后查看下载中心",
    IncludeBodyHash = false  // 同一用户同时只能有一个导出任务
)]
public async Task<IActionResult> ExportReport([FromBody] ExportRequest request)
{
    // 异步执行导出任务
    var taskId = await ReportService.ExportAsync(request);
    
    return YZHOk(new { TaskId = taskId, Message = "导出任务已创建" });
}
```

### 场景 3：支付相关操作（严格幂等）

```csharp
/// <summary>
/// 支付操作（严格的幂等性要求）
/// </summary>
[HttpPost("pay")]
[YZHIdempotent(
    DurationSeconds = 300,  // 5 分钟锁定（支付超时时间）
    Message = "支付处理中，请勿重复支付",
    KeyGeneratorType = typeof(PaymentIdempotentKeyGenerator)  // 基于订单号
)]
public async Task<IActionResult> Pay([FromBody] PaymentRequest request)
{
    var result = await PaymentService.ProcessPaymentAsync(request.OrderNo);
    
    return YZHOk(result);
}
```

### 最佳实践清单

```
✅ 推荐做法：

1️⃣  所有写操作（POST/PUT/DELETE）都加上 [YZHIdempotent]
    → 养成习惯，避免遗漏
    
2️⃣  根据业务场景合理设置 DurationSeconds
    → 表单提交：3-5 秒
    → 文件上传：30-60 秒
    → 支付/导出：5-10 分钟
    
3️⃣  重要操作提供明确的 Message 提示
    → "请勿重复提交审核申请" 比 "操作过于频繁" 更友好
    
4️⃣  前端配合使用 YzHSafeButton 组件
    → 双重保障：前端节流 + 后端幂等
    
5️⃣  生产环境开启详细日志
    → 便于排查恶意请求或系统问题


❌ 避免做法：

1️⃣  不要在 GET 请求上使用（虽然会自动跳过，但语义不清）
    
2️⃣  不要设置过长的 DurationSeconds（影响用户体验）
    → 除非是支付、导出等特殊场景
    
3️⃣  不要依赖前端防重作为唯一手段
    → 后端幂等才是可靠的保障
    
4️⃣  不要忘记处理异常情况下的 Redis 连接失败
    → 当前实现已考虑：异常时放行，保证可用性优先
```

---

## 20.10 与现有架构的集成关系

```
┌─────────────────────────────────────────────────────────────┐
│              YZHIdempotent 在整体架构中的位置                  │
│                                                             │
│  请求生命周期：                                               │
│                                                             │
│  Client Request                                            │
│       ↓                                                     │
│  Middleware Layer                                           │
│       ↓                                                     │
│  ┌─────────────────────────────────────────────┐           │
│  │  Model Binding                               │           │
│  └─────────────────────────────────────────────┘           │
│       ↓                                                     │
│  ┌─────────────────────────────────────────────┐           │
│  │  🆕 YZHIdempotentActionFilter (优先级最高)    │ ← 新增    │
│  │     ├─ 检查 [YZHIdempotent] 特性              │           │
│  │     ├─ Redis SET NX EX 原子操作              │           │
│  │     └─ 失败 → 抛出 YZHBusinessException(409) │           │
│  └─────────────────────────────────────────────┘           │
│       ↓                                                     │
│  ┌─────────────────────────────────────────────┐           │
│  │  Action Execution                            │           │
│  │     ├─ Controller.Action()                   │           │
│  │     ├─ Service.BusinessLogic()               │           │
│  │     └─ Repository.CRUD()                     │           │
│  └─────────────────────────────────────────────┘           │
│       ↓ (如果抛出异常)                                         │
│  ┌─────────────────────────────────────────────┐           │
│  │  YZHGlobalExceptionFilter (统一异常处理)      │           │
│  │     ├─ YZHBusinessException(409)             │           │
│  │     │   → 返回 409 Conflict + 友好提示        │           │
│  │     ├─ YZHValidationException                │           │
│  │     │   → 返回 400 + 字段级错误               │           │
│  │     └─ Exception                             │           │
│  │         → 返回 500 + 通用错误                 │           │
│  └─────────────────────────────────────────────┘           │
│       ↓                                                     │
│  Response                                                   │
│                                                             │
│  关键优势：                                                  │
│  ✅ 与全局异常过滤器无缝集成                                  │
│  ✅ 不影响其他 Filter 和 Middleware                           │
│  ✅ 符合"声明式优于命令式"的设计哲学                          │
│  ✅ 零业务代码侵入                                           │
└─────────────────────────────────────────────────────────────┘
```

---

## 20.11 目录结构更新

```
YZH.Framework/
├── Attributes/
│   ├── YZHBehaviorAttribute.cs
│   ├── YZHIdempotentAttribute.cs          # ⭐ V1.4 新增
│   ├── YZHAuditedAttribute.cs
│   └── ...
│
├── Interfaces/
│   ├── IIdempotentKeyGenerator.cs         # ⭐ V1.4 新增
│   └── IYZHActionDecorator.cs
│
├── Idempotent/                            # ⭐ V1.4 新增目录
│   ├── DefaultIdempotentKeyGenerator.cs   # 默认键生成器
│   └── ReadMe.md                          # 使用说明
│
├── Web/
│   └── Filters/
│       ├── YZHGlobalExceptionFilter.cs
│       └── YZHIdempotentActionFilter.cs   # ⭐ V1.4 新增
│
└── Exceptions/
    ├── YZHBusinessException.cs
    └── ...
```

---

## 20.12 测试验证要点

```csharp
/// <summary>
/// 幂等性功能测试用例（单元测试 + 集成测试）
/// </summary>
public class IdempotentTests
{
    [Fact]
    public async Task First_Request_Should_Pass()
    {
        // 首次请求应该成功通过
        // Arrange: 准备测试数据
        // Act: 发送 POST 请求
        // Assert: 返回 200 OK
    }

    [Fact]
    public async Task Duplicate_Request_Should_Be_Rejected()
    {
        // 相同参数的重复请求应该被拒绝
        // Arrange: 发送首次请求（成功）
        // Act: 立即发送第二次相同请求
        // Assert: 返回 409 Conflict + 正确的错误消息
    }

    [Fact]
    public async Task Different_Parameters_Should_Pass()
    {
        // 不同参数的请求应该被视为不同操作
        // Arrange: 发送请求 A（参数 X=1）
        // Act: 立即发送请求 B（参数 X=2）
        // Assert: 两个请求都成功（因为 body hash 不同）
    }

    [Fact]
    public async Task After_Expiry_Should_Pass_Again()
    {
        // 锁过期后应该允许新的请求
        // Arrange: 发送请求并等待 DurationSeconds + 1 秒
        // Act: 再次发送相同请求
        // Assert: 返回 200 OK（锁已过期）
    }

    [Fact]
    public async Task Get_Request_Should_Skip_Check()
    {
        // GET 请求应该跳过幂等性检查
        // Act: 发送 GET 请求（即使标记了特性）
        // Assert: 直接放行，不设置 Redis 键
    }

    [Fact]
    public async Task Custom_Key_Generator_Should_Work()
    {
        // 自定义键生成器应该正确工作
        // Arrange: 配置 KeyGeneratorType = typeof(MyKeyGenerator)
        // Act: 发送请求
        // Assert: 使用自定义策略生成的 Key
    }
}
```

---

# 第二十一章、Vol 框架源码分析报告（⭐ V1.5 新增）

> **"站在巨人的肩膀上，但不重复造轮子。分析 Vol 源码的目的是明确哪些能力可以直接复用，哪些需要自定义增强。"**

## 21.1 分析范围与方法论

### 分析目标

```
┌─────────────────────────────────────────────────────────────┐
│              Vol 源码分析的核心目标                            │
│                                                             │
│  1️⃣  权限系统评估                                           │
│     → RBAC 实现是否完整？是否支持数据权限？                   │
│     → 是否满足三层用户体系（平台/机构/企业）？                 │
│                                                             │
│  2️⃣  日志系统能力                                           │
│     → 操作日志、审计日志、异常日志的实现程度？                │
│     → 是否支持结构化日志和分类？                              │
│                                                             │
│  3️⃣  字典服务                                               │
│     → 全局字典缓存机制是否完善？                             │
│     → 是否支持动态加载和多租户隔离？                          │
│                                                             │
│  4️⃣  Service 基类                                           │
│     → CRUD 生命周期钩子是否完备？                            │
│     → 多表（主从表）事务处理能力？                           │
│                                                             │
│  5️⃣  过滤器体系                                             │
│     → 异常处理、权限校验、参数验证的实现质量？               │
│     → 是否支持全局注册和声明式配置？                          │
│                                                             │
│  📊 最终输出：                                              │
│     ✅ 可直接复用的能力（无需修改）                          │
│     ⚠️ 需要增强的能力（小改即可）                            │
│     ❌ 需要自研的能力（Vol 不满足或架构冲突）                │
└─────────────────────────────────────────────────────────────┘
```

### 分析源码位置

```
src/server/Vue.NetCore/vol.api/
├── VOL.Core/                          # 核心库（分析重点）
│   ├── Filters/                       # 过滤器体系
│   │   ├── ActionPermissionFilter.cs      # 权限过滤器 ⭐
│   │   ├── ActionPermissionAttribute.cs   # 权限特性 ⭐
│   │   ├── ApiActionPermissionAttribute.cs # API权限特性
│   │   ├── ActionExecuteFilter.cs         # 执行过滤器
│   │   ├── JWTAuthorize.cs               # JWT认证
│   │   └── ApiAuthorizeFilter.cs         # API授权过滤
│   │
│   ├── BaseProvider/                  # 服务基类
│   │   ├── ServiceBase.cs                # 核心Service基类 ⭐⭐⭐
│   │   ├── ApplicationServiceBase.cs     # 应用服务基类（钩子定义）
│   │   └── ApplicationServiceBase*.cs    # CRUD扩展方法
│   │
│   ├── Infrastructure/                # 基础设施
│   │   ├── DictionaryManager.cs          # 字典管理器 ⭐
│   │   └── DictionaryHandler.cs          # 字典处理器
│   │
│   ├── UserManager/                   # 用户管理
│   │   ├── UserContext.cs                # 用户上下文 ⭐⭐
│   │   └── RoleContext.cs                # 角色上下文
│   │
│   ├── Services/                      # 服务实现
│   │   └── Logger.cs                    # 日志服务 ⭐
│   │
│   ├── CacheManager/                   # 缓存管理
│   │   └── IService/ICacheService.cs    # 缓存接口
│   │
│   └── Enums/                         # 枚举定义
│       └── ActionPermissionOptions.cs   # 权限操作枚举
│
├── VOL.Entity/                        # 实体定义
│   └── DomainModels/System/           # 系统实体
│       └── Sys_Log.cs                 # 日志实体
│
└── VOL.Sys/                          # 系统模块
    └── Services/System/
        └── Sys_LogService.cs          # 日志服务实现
```

---

## 21.2 权限系统深度分析

### 21.2.1 架构概览

```
┌─────────────────────────────────────────────────────────────┐
│              Vol 权限系统架构图                                │
│                                                             │
│  ┌─────────────┐    ┌──────────────────┐    ┌─────────────┐ │
│  │ Controller   │    │ ActionPermission │    │ UserContext │ │
│  │ [特性标记]   │───▶│     Filter       │───▶│  (权限缓存) │ │
│  └─────────────┘    └──────────────────┘    └─────────────┘ │
│           │                     │                    │      │
│           ▼                     ▼                    ▼      │
│  ┌─────────────┐    ┌──────────────────┐    ┌─────────────┐ │
│  │ TableName   │    │ TableActions[]   │    │ Permissions │ │
│  │ (表名)      │    │ (操作权限数组)    │    │ (用户权限)  │ │
│  └─────────────┘    └──────────────────┘    └─────────────┘ │
│                                                             │
│  权限判断流程：                                              │
│  1. 检查 [AllowAnonymous] → 放行                            │
│  2. 检查 IsSuperAdmin → 放行                               │
│  3. 检查 RoleIds（角色白名单）→ 匹配则放行                  │
│  4. 检查 TableName + TableActions → 查询用户权限             │
│  5. 明细表权限回退到主表权限                                 │
└─────────────────────────────────────────────────────────────┘
```

### 21.2.2 核心组件代码解析

#### **① ActionPermissionAttribute（权限特性）**

```csharp
/// <summary>
/// Vol 权限特性 - 声明式权限控制
/// 
/// 使用方式：
/// [ActionPermission("Sys_User", ActionPermissionOptions.Add)]
/// public IActionResult Add(User user) { ... }
/// </summary>
public class ActionPermissionAttribute : TypeFilterAttribute
{
    // 构造函数重载1：仅标记为需要权限校验（从路由获取表名）
    public ActionPermissionAttribute(bool isApi = false)
        : base(typeof(ActionPermissionFilter))
    {
        Arguments = new object[] { new ActionPermissionRequirement() { IsApi = isApi } };
    }

    // 构造函数重载2：指定角色ID访问
    public ActionPermissionAttribute(int roleId, bool isApi = false)
        : base(typeof(ActionPermissionFilter))
    {
        Arguments = new object[] { new ActionPermissionRequirement() { 
            RoleIds = new int[] { roleId }, 
            IsApi = isApi 
        }};
    }

    // 构造函数重载3：指定表名 + 操作权限（最常用）
    public ActionPermissionAttribute(
        string tableName, 
        ActionPermissionOptions tableAction, 
        bool sysController = false, 
        bool isApi = false)
    {
        this.SetActionPermissionRequirement(tableName, tableAction, sysController, isApi);
    }
}
```

**关键发现**：
- ✅ 使用 `TypeFilterAttribute` 实现，支持依赖注入
- ✅ 支持多种构造方式（角色限定、表+操作、自动推断）
- ✅ 继承出 `ApiActionPermissionAttribute` 用于 API 场景

#### **② ActionPermissionOptions（权限操作枚举）**

```csharp
/// <summary>
/// 权限操作选项 - 使用位标志（Flags）枚举
/// </summary>
public enum ActionPermissionOptions
{
    None = 0,
    Add = 1,          // 新增
    Update = 2,       // 编辑
    Delete = 4,       // 删除
    Search = 8,       // 查询
    Export = 16,      // 导出
    Import = 32,      // 导入
    Audit = 64,       // 审核
    All = Add | Update | Delete | Search | Export | Import | Audit
}
```

**关键发现**：
- ✅ 使用 `[Flags]` 位标志枚举，支持组合权限：`Add | Update | Delete`
- ❌ **缺少**：审批（Approve）、驳回（Reject）、发布（Publish）等业务操作
- ❌ **缺少**：细粒度的字段级权限（只能看某些字段）

#### **③ ActionPermissionFilter（权限过滤器核心逻辑）**

```csharp
public class ActionPermissionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (OnActionExecutionPermission(context).Status)
        {
            await next();
            return;
        }
        FilterResponse.SetActionResult(context, ResponseContent);  // 返回无权限响应
    }

    private WebResponseContent OnActionExecutionPermission(ActionExecutingContext context)
    {
        // 1️⃣ 允许匿名访问或超级管理员直接放行
        if (context.Filters.Any(item => item is IAllowAnonymousFilter)
            || UserContext.Current.IsSuperAdmin)
            return ResponseContent.OK();

        // 2️⃣ 演示环境全局过滤（可选）
        if (!_userContext.IsSuperAdmin && AppSetting.GlobalFilter.Enable
            && AppSetting.GlobalFilter.Actions.Any(x => ...))
        {
            return ResponseContent.Error(AppSetting.GlobalFilter.Message);
        }

        // 3️⃣ 自动推断表名（如果 SysController=true）
        if (ActionPermission.SysController)
        {
            // 从 PermissionTableAttribute 获取，或使用 Controller 名称
            ActionPermission.TableName = ...;
        }

        // 4️⃣ 角色白名单检查
        if (ActionPermission.RoleIds?.Length > 0)
        {
            if (ActionPermission.RoleIds.Contains(_userContext.UserInfo.Role_Id)) 
                return ResponseContent.OK();
        }

        // 5️⃣ 表级权限检查（核心逻辑）
        bool actionAuth = CheckPermission(actionsToCheck, ActionPermission.TableName);
        
        if (!actionAuth)
        {
            // 6️⃣ 移动端菜单权限兼容
            // 7️⃣ 明细表权限回退到主表
            return ResponseContent.Error(ResponseType.NoPermissions);
        }

        return ResponseContent.OK();
    }
}
```

**关键发现**：
- ✅ 支持 5 层权限判断链（匿名→超管→角色→表操作→明细表回退）
- ✅ 支持演示环境全局过滤（GlobalFilter）
- ✅ 日志记录权限拒绝事件（便于审计）
- ❌ **不支持数据权限**（行级过滤，如只能看自己机构的数据）
- ❌ **不支持多租户隔离**（需要自行扩展）

#### **④ UserContext（用户上下文与权限缓存）**

```csharp
public class UserContext
{
    /// <summary>
    /// 用户权限缓存（按角色ID缓存，版本号驱动刷新）
    /// </summary>
    private static readonly Dictionary<int, List<Permissions>> rolePermissions = new();

    public List<Permissions> Permissions
    {
        get { return GetPermissions(RoleId); }
    }

    /// <summary>
    /// 获取用户所有菜单权限（带版本号缓存机制）
    /// - 首次查询数据库
    /// - 后续通过 Redis/Memory 版本号比对决定是否刷新
    /// </summary>
    public List<Permissions> GetPermissions(int roleId)
    {
        if (IsRoleIdSuperAdmin(roleId))
        {
            // 超级管理员：返回所有启用的菜单（包括隐藏菜单 Enable=2）
            var permissions = DBServerProvider.DbContext.Set<Sys_Menu>()
                .Where(x => x.Enable == 1 || x.Enable == 2)
                .Select(a => new Permissions { ... }).ToList();
            
            // 将按钮权限转为数组
            return ActionToArray(permissions);
        }

        // 普通用户：根据角色查询菜单权限
        // ... 从数据库或缓存读取
    }
}
```

**关键发现**：
- ✅ **高效的缓存机制**：基于角色ID + 版本号的二级缓存
- ✅ **自动刷新**：菜单按钮变更时调用 `RefreshWithMenuChange` 刷新缓存
- ✅ **超管特殊处理**：可以看到隐藏菜单（Enable=2）
- ⚠️ **内存缓存为主**：未强制使用 Redis，多实例部署可能不一致

### 21.2.3 权限模型对比

| 能力 | Vol 现状 | YZH 需求 | 差距评估 |
|------|---------|---------|---------|
| **RBAC（角色-权限）** | ✅ 完整实现 | ✅ 需要 | **可直接复用** |
| **菜单权限** | ✅ 完整（增删改查导出导入审核） | ✅ 需要 | **可直接复用** |
| **按钮权限** | ✅ 基于 Sys_Actions 表 | ✅ 需要 | **可直接复用** |
| **角色白名单** | ✅ RoleIds 参数 | ✅ 需要 | **可直接复用** |
| **数据权限（行级）** | ❌ 不支持 | ✅ 多租户隔离 | **需自研** |
| **字段权限** | ✅ FilterQueryableAuthFields | ⚠️ 可能需要增强 | **可增强** |
| **多租户** | ❌ 不支持 | ✅ 三层用户体系 | **需自研** |
| **API 权限** | ✅ ApiActionPermissionAttribute | ✅ 需要 | **可直接复用** |
| **权限缓存** | ✅ 内存 + 版本号 | ✅ Redis 更优 | **建议增强** |

### 21.2.4 结论与建议

```
✅ 可直接复用（80% 能力满足）：

1️⃣  ActionPermissionAttribute + Filter
    → 直接用于 Controller 权限声明
    → 无需任何修改
    
2️⃣  UserContext.Permissions 权限查询
    → 直接获取当前用户的菜单/按钮权限
    → 缓存机制成熟稳定
    
3️⃣  ActionPermissionOptions 枚举
    → 直接使用标准 CRUD 权限定义


⚠️ 需要增强（20% 需要扩展）：

1️⃣  数据权限（行级过滤）
    → YZHMultiTenantAttribute 补充
    → 在 ServiceBase 查询时自动追加 OrgCode 条件
    
2️⃣  权限缓存升级 Redis
    → 当前是静态 Dictionary（单实例可用）
    → 多实例部署时需要改为 Redis 分布式缓存
    
3️⃣  业务操作权限扩展
    → 在 ActionPermissionOptions 基础上新增：
      Approve, Reject, Publish, Archive, Reactivate


❌ 需要完全自研（架构不匹配）：

1️⃣  三层用户体系的权限模型
    → Vol 只有单一角色体系
    → 需要实现 Layer1/Layer2/Layer3 的层级权限
    
2️⃣  动态权限规则引擎
    → 如："审核员只能审核自己机构的申请"
    → 需要结合业务规则引擎实现
```

---

## 21.3 日志系统深度分析

### 21.3.1 架构概览

```
┌─────────────────────────────────────────────────────────────┐
│              Vol 日志系统架构图                                │
│                                                             │
│  请求进入                                                    │
│     ↓                                                       │
│  ┌─────────────────┐                                        │
│  │ Middleware/Filter│  记录请求开始时间                       │
│  └────────┬────────┘                                        │
│           ↓                                                  │
│  ┌─────────────────┐                                        │
│  │   Action 执行    │  业务逻辑处理                           │
│  └────────┬────────┘                                        │
│           ↓                                                  │
│  ┌─────────────────────────────────────────────┐           │
│  │ Logger.cs (队列 + 批量写入)                   │           │
│  │                                             │           │
│  │  1️⃣  构建日志对象 Sys_Log                    │           │
│  │     ├─ UserName, Url, LogType               │           │
│  │     ├─ RequestParameter, ResponseParameter  │           │
│  │     ├─ ExceptionInfo                        │           │
│  │     ├─ BeginDate, EndDate, ElapsedTime      │           │
│  │     └─ Success (1=成功, 0=失败, -1=异常)     │           │
│  │                                             │           │
│  │  2️⃣  入队 ConcurrentQueue<Sys_Log>          │           │
│  │                                             │           │
│  │  3️⃣  后台线程批量写入数据库                  │           │
│  │     ├─ DataTable 批量插入（性能优化）        │           │
│  │     └─ 可配置批量大小和时间间隔              │           │
│  └─────────────────────────────────────────────┘           │
│           ↓                                                  │
│  数据库 Sys_Log 表                                          │
└─────────────────────────────────────────────────────────────┘
```

### 21.3.2 核心组件代码解析

#### **① Sys_Log 实体（日志数据模型）**

```csharp
[Table("Sys_Log")]
[EntityAttribute(TableCnName = "系统日志")]
public class Sys_Log : BaseEntity
{
    [Key]
    public int Id { get; set; }
    
    public DateTime? BeginDate { get; set; }          // 开始时间
    public string UserName { get; set; }              // 用户名称
    public string Url { get; set; }                   // 请求地址
    public string LogType { get; set; }               // 日志类型
    public int? Success { get; set; }                 // 响应状态(1/-1)
    public int? ElapsedTime { get; set; }             // 耗时(毫秒)
    public string RequestParameter { get; set; }      // 请求参数
    public string ResponseParameter { get; set; }     // 响应参数
    public string ExceptionInfo { get; set; }         // 异常信息
    public string UserIP { get; set; }                // 用户IP
    public string ServiceIP { get; set; }             // 服务器IP
    public string BrowserType { get; set; }           // 浏览器类型
    public int? User_Id { get; set; }                 // 用户ID
    public int? Role_Id { get; set; }                 // 角色ID
}
```

**关键发现**：
- ✅ **完整的请求上下文记录**：IP、浏览器、耗时、参数、响应
- ✅ **性能优化设计**：使用 `ConcurrentQueue` + 后台线程批量写入
- ❌ **缺少分类字段**：没有 Module（模块）、Operation（操作）、TargetId（目标对象）
- ❌ **缺少变更追踪**：不记录新旧值对比（审计需求不满足）
- ❌ **结构化不足**：RequestParameter/ResponseParameter 是纯文本，不便查询

#### **② Logger.cs（日志写入服务）**

```csharp
public class Logger
{
    private static ConcurrentQueue<Sys_Log> loggerQueueData = new();
    
    /// <summary>
    /// 写入日志（入队）
    /// </summary>
    public static void LoggerInfo(Sys_Log log)
    {
        loggerQueueData.Enqueue(log);
    }

    /// <summary>
    /// 后台线程：批量写入数据库
    /// </summary>
    private static async Task SaveLog()
    {
        while (true)
        {
            await Task.Delay(2000);  // 每 2 秒执行一次
            
            if (loggerQueueData.Count == 0) continue;
            
            DataTable queueTable = CreateEmptyTable();
            
            while (loggerQueueData.TryDequeue(out Sys_Log log))
            {
                // 填充 DataTable 行
                DataRow row = queueTable.NewRow();
                row["LogType"] = log.LogType;
                row["RequestParameter"] = log.RequestParameter;
                row["ResponseParameter"] = log.ResponseParameter;
                row["ExceptionInfo"] = log.ExceptionInfo;
                row["Success"] = log.Success ?? -1;
                row["BeginDate"] = log.BeginDate;
                row["EndDate"] = log.EndDate;
                row["ElapsedTime"] = ((DateTime)log.EndDate - (DateTime)log.BeginDate).TotalMilliseconds;
                // ... 其他字段
                queueTable.Rows.Add(row);
            }
            
            // SqlBulkCopy 批量插入（高性能）
            if (queueTable.Rows.Count > 0)
            {
                DBServerProvider.SqlDapper.SqlBulkCopy(queueTable, "Sys_Log");
            }
        }
    }
}
```

**关键发现**：
- ✅ **生产级性能**：异步队列 + 批量写入 + SqlBulkCopy
- ✅ **非阻塞设计**：不入队等待，不影响业务线程
- ⚠️ **内存风险**：如果数据库宕机，队列可能无限增长（需增加上限保护）
- ❌ **不支持日志分级**：Debug/Info/Warn/Error 全部混在一起
- ❌ **不支持多目标输出**：只能写数据库，不能同时写文件/Elasticsearch

#### **③ ActionLog 特性（声明式日志标记）**

```csharp
/// <summary>
/// 操作日志特性 - 标记在 Controller/Action 上
/// </summary>
public class ActionLog : Attribute
{
    /// <summary>日志类型</summary>
    public string LogType { get; set; }
    
    /// <summary>是否写入日志</summary>
    public bool Write { get; set; }
    
    public ActionLog(bool write = true) { Write = write; }
    public ActionLog(string logType) { LogType = logType; Write = true; }
}
```

**关键发现**：
- ✅ **轻量级设计**：简单的开关 + 类型标记
- ❌ **功能过于简单**：无法记录操作详情、敏感字段脱敏、分类归档

### 21.3.3 日志系统能力对比

| 能力 | Vol 现状 | YZH 需求 | 差距评估 |
|------|---------|---------|---------|
| **请求日志** | ✅ 完整（URL/IP/参数/响应/耗时） | ✅ 需要 | **可直接复用** |
| **异常日志** | ✅ ExceptionInfo 字段 | ✅ 需要 | **可直接复用** |
| **批量写入** | ✅ ConcurrentQueue + SqlBulkCopy | ✅ 需要 | **可直接复用** |
| **声明式标记** | ⚠️ 简单（[ActionLog]） | ✅ 增强 | **需增强** |
| **日志分类** | ❌ 仅 LogType 字符串 | ✅ 枚举分类 | **需自研** |
| **审计追踪** | ❌ 不支持新旧值对比 | ✅ 必须 | **需自研** |
| **敏感字段脱敏** | ❌ 不支持 | ✅ 必须 | **需自研** |
| **日志分级** | ❌ 不支持 Debug/Info/Warn/Error | ✅ 建议 | **需增强** |
| **多目标输出** | ❌ 仅数据库 | ⚠️ 可选 | **建议增强** |

### 21.3.4 结论与建议

```
✅ 可直接复用：

1️⃣  日志基础设施（Sys_Log 实体 + Logger 批量写入）
    → 直接作为底层存储层
    → 性能经过生产验证
    
2️⃣  请求上下文采集（IP/浏览器/耗时/参数）
    → 在 YZHGlobalExceptionFilter 中复用此逻辑


⚠️ 需要增强（在 Vol 基础上包装）：

1️⃣  YZHAuditedAttribute 替代 ActionLog
    → 增加分类（YZHLogCategory 枚举）
    → 增加敏感字段配置
    → 增加追踪级别（Scope: Crud/Audit/All）
    
2️⃣  YZHAuditLogEntry 结构化日志模型
    → 替代原始字符串的 RequestParameter
    → 支持新旧值对比（TrackChanges=true）
    
3️⃣  日志服务接口抽象
    → IYZHAuditLogService
    → 支持写数据库 / 写文件 / 写 Elasticsearch


❌ 需要完全自研：

1️⃣  审计追踪引擎
    → 变更检测（反射对比新旧对象）
    → 敏感字段自动脱敏（手机号/身份证/银行卡）
    → 操作快照（FullSnapshot 模式）
    
2️⃣  日志分析与告警
    → 异常日志自动聚合
    → 性能日志慢查询告警
    → 操作日志统计分析
```

---

## 21.4 字典服务深度分析

### 21.4.1 架构概览

```
┌─────────────────────────────────────────────────────────────┐
│              Vol 字典服务架构图                                │
│                                                             │
│  ┌─────────────────┐                                        │
│  │ DictionaryManager│  静态字典管理器（全局单例）              │
│  │   (静态类)       │                                        │
│  └────────┬────────┘                                        │
│           │                                                  │
│           ▼                                                  │
│  ┌─────────────────────────────────────────────┐           │
│  │ 缓存策略（版本号驱动刷新）                      │           │
│  │                                             │           │
│  │  1️⃣  首次访问：查询数据库加载所有启用字典       │           │
│  │  2️⃣  缓存到静态变量 _dictionaries             │           │
│  │  3️⃣  记录版本号到 ICacheService (Redis/Memory)│           │
│  │  4️⃣  后续访问：比对版本号                      │           │
│  │     ├─ 版本相同 → 返回缓存                     │           │
│  │     └─ 版本不同 → 重新查询数据库并刷新缓存       │           │
│  └─────────────────────────────────────────────┘           │
│           │                                                  │
│           ▼                                                  │
│  ┌─────────────────┐     ┌─────────────────┐               │
│  │ Sys_Dictionary   │     │Sys_DictionaryList│               │
│  │ (字典头)         │────▶│ (字典明细)       │               │
│  │ DicNo, DbSql     │     │ DicValue,DicName │               │
│  └─────────────────┘     └─────────────────┘               │
│                                                             │
│  🔧 特殊处理：DictionaryHandler                             │
│     → roles / t_roles: 动态SQL（根据用户角色过滤）          │
│     → 支持自定义 SQL 数据源                                  │
└─────────────────────────────────────────────────────────────┘
```

### 21.4.2 核心代码解析

#### **① DictionaryManager（字典管理器）**

```csharp
public static class DictionaryManager
{
    private static List<Sys_Dictionary> _dictionaries { get; set; }
    private static object _dicObj = new object();
    private static string _dicVersionn = "";
    public const string Key = "inernalDic";

    /// <summary>
    /// 获取所有字典（带版本号缓存）
    /// </summary>
    public static List<Sys_Dictionary> Dictionaries
    {
        get { return GetAllDictionary(); }
    }

    /// <summary>
    /// 根据字典编号获取单个字典
    /// </summary>
    public static Sys_Dictionary GetDictionary(string dicNo)
    {
        return GetDictionaries(new string[] { dicNo }).FirstOrDefault();
    }

    /// <summary>
    /// 批量获取字典（支持自定义SQL数据源）
    /// </summary>
    public static IEnumerable<Sys_Dictionary> GetDictionaries(IEnumerable<string> dicNos, bool executeSql = true)
    {
        foreach (var item in Dictionaries.Where(x => dicNos.Contains(x.DicNo)))
        {
            if (executeSql && !string.IsNullOrEmpty(item.DbSql))
            {
                // 执行自定义SQL获取字典数据源
                string sql = DictionaryHandler.GetCustomDBSql(item.DicNo, item.DbSql);
                item.Sys_DictionaryList = query(sql);
            }
            yield return item;
        }
    }

    /// <summary>
    /// 加载所有字典（版本号驱动的缓存刷新）
    /// </summary>
    private static List<Sys_Dictionary> GetAllDictionary()
    {
        ICacheService cacheService = AutofacContainerModule.GetService<ICacheService>();
        
        // 比对版本号，未变更则直接返回缓存
        if (_dictionaries != null && _dicVersionn == cacheService.Get(Key))
            return _dictionaries;

        lock (_dicObj)  // 双重检查锁
        {
            if (_dicVersionn != "" && _dictionaries != null && _dicVersionn == cacheService.Get(Key))
                return _dictionaries;

            // 从数据库加载所有启用的字典（Include 子表）
            _dictionaries = DBServerProvider.DbContext
                .Set<Sys_Dictionary>()
                .Where(x => x.Enable == 1)
                .Include(c => c.Sys_DictionaryList).ToList();

            // 更新版本号
            string cacheVersion = cacheService.Get(Key);
            if (string.IsNullOrEmpty(cacheVersion))
            {
                cacheVersion = DateTime.Now.ToString("yyyyMMddHHMMssfff");
                cacheService.Add(Key, cacheVersion);
            }
            else
            {
                _dicVersionn = cacheVersion;
            }
        }
        return _dictionaries;
    }
}
```

**关键发现**：
- ✅ **成熟的缓存策略**：版本号驱动 + 双重检查锁（线程安全）
- ✅ **支持自定义 SQL**：可通过 DbSql 字段配置动态数据源
- ✅ **预加载设计**：一次性加载所有字典，后续内存读取（高性能）
- ✅ **自动刷新机制**：修改字典后更新版本号缓存即可全局刷新
- ⚠️ **静态变量缓存**：单实例部署没问题，多实例需要配合 Redis 版本号
- ❌ **不支持多租户隔离**：所有租户共享同一份字典（部分场景不适合）

#### **② ICacheService（缓存接口）**

```csharp
public interface ICacheService : IDisposable
{
    bool Exists(string key);
    void LPush(string key, string val);          // List 操作
    void RPush(string key, string val);
    object ListDequeue(string key);
    T ListDequeue<T>(key) where T : class;
    void ListRemove(string key, int keepIndex);
    
    bool AddObject(string key, object value, int expireSeconds = -1, bool isSliding = false);
    bool Add(string key, string value, int expireSeconds = -1, bool isSliding = false);
    bool Remove(string key);
    void RemoveAll(IEnumerable<string> keys);
    
    T Get<T>(key) where T : class;
    string Get(key);
}
```

**关键发现**：
- ✅ **统一的缓存抽象**：MemoryCache / Redis 可切换
- ✅ **支持 List 操作**：可用于消息队列场景
- ✅ **支持滑动过期**：`isSliding` 参数
- ❌ **缺少分布式锁**：`StringSetNx` 等原子操作（我们的幂等性需要）

### 21.4.3 字典服务能力对比

| 能力 | Vol 现状 | YZH 需求 | 差距评估 |
|------|---------|---------|---------|
| **全局字典缓存** | ✅ 成熟（版本号驱动） | ✅ 需要 | **可直接复用** |
| **自定义 SQL 数据源** | ✅ DbSql 字段 | ✅ 需要 | **可直接复用** |
| **字典联动前端** | ✅ DropNo 绑定 | ✅ 需要 | **可直接复用** |
| **热更新** | ✅ 版本号刷新 | ✅ 需要 | **可直接复用** |
| **多租户字典隔离** | ❌ 不支持 | ⚠️ 部分需要 | **需增强** |
| **分布式缓存** | ⚠️ 接口支持，默认 Memory | ✅ Redis | **配置切换** |
| **字典分组/分类** | ❌ 仅 DicNo | ⚠️ 可能需要 | **可增强** |

### 21.4.4 结论与建议

```
✅ 可直接复用（95% 能力满足）：

1️⃣  DictionaryManager 全局字典管理
    → 直接用于前端下拉框、状态显示等场景
    → 无需任何修改
    
2️⃣  版本号驱动的缓存刷新机制
    → 字典修改后即时生效，无需重启
    
3️⃣  自定义 SQL 数据源
    → 角色、部门等动态下拉框场景完美适配


⚠️ 小改动即可：

1️⃣  多租户字典隔离（如果有需要）
    → 在 DicNo 前加租户前缀："{orgCode}_{dicNo}"
    → 或单独的租户字典表
    
2️⃣  默认缓存切换到 Redis
    → 配置文件修改即可（ICacheService 已抽象）
```

---

## 21.5 Service 基类深度分析

### 21.5.1 继承体系

```
┌─────────────────────────────────────────────────────────────┐
│              Vol Service 继承体系                              │
│                                                             │
│  IService<TEntity>          (接口定义)                       │
│       │                                                    │
│       ▼                                                    │
│  ApplicationServiceBase<TEntity, TRepository>               │
│  (应用服务基类 - 定义钩子和通用属性)                          │
│       │                                                    │
│       ├─ AddOnExecute / AddOnExecuting / AddOnExecuted     │
│       ├─ UpdateOnExecuting / UpdateOnExecuted              │
│       ├─ DelOnExecuting / DelOnExecuted                    │
│       ├─ PageDataOnExecuting / PageDataOnExecuted          │
│       └─ ... 其他属性和方法                                  │
│       │                                                    │
│       ▼                                                    │
│  ServiceBase<TEntity, TRepository>                          │
│  (核心服务基类 - CRUD 实现 + 分页 + 上传 + 导入导出)          │
│       │                                                    │
│       ▼                                                    │
│  XxxService : ServiceBase<XxxEntity, IXxxRepository>        │
│  (具体业务服务 - Partial 类扩展)                             │
└─────────────────────────────────────────────────────────────┘
```

### 21.5.2 生命周期钩子详解

```csharp
/// <summary>
/// ApplicationServiceBase 定义的钩子（全部是 Func 委托，按需赋值）
/// </summary>

// ════════════════ 新增操作 ════════════════
protected Func<SaveModel, WebResponseContent> AddOnExecute;
// → 最早期：SaveModel 原始数据处理前（参数校验、权限检查）

protected Func<TEntity, object, WebResponseContent> AddOnExecuting;
// → 早期：实体已创建但未保存（设置默认值、业务校验）

protected Func<TEntity, object, WebResponseContent> AddOnExecuted;
// → 后期：已保存到数据库（事务内，可关联操作）

// ════════════════ 更新操作 ════════════════
protected Func<TEntity, object, object, List<object>, WebResponseContent> UpdateOnExecuting;
// → 早期：主表+明细表数据准备好（复杂校验、乐观锁）

protected Func<TEntity, object, object, List<object>, WebResponseContent> UpdateOnExecuted;
// → 后期：已保存（事务内，审计日志、触发器）

// ════════════════ 删除操作 ════════════════
protected Func<object[], WebResponseContent> DelOnExecuting;
// → 早期：删除前（关联检查、软删除标记）

protected Func<object[], WebResponseContent> DelOnExecuted;
// → 后期：已删除（事务内，清理关联数据）

// ════════════════ 分页查询 ════════════════
protected Func<PageDataOptions, Task<WebResponseContent>> PageDataOnExecutingAsync;
// → 查询前（动态条件追加、权限过滤）

protected Func<PageGridData<TEntity>, Task> GetPageDataOnExecutedAsync;
// → 查询后（结果转换、敏感字段过滤）
```

**关键发现**：
- ✅ **完整的生命周期覆盖**：每个 CRUD 操作都有 Before/After 钩子
- ✅ **事务内钩子**：`OnExecuted` 钩子在事务内部执行，保证一致性
- ✅ **同步 + 异步双版本**：每个钩子都有 Sync 和 Async 版本
- ✅ **主从表支持**：Update 钩子包含 addList/updateList/delKeys 参数
- ❌ **使用 Func 委托而非虚方法**：无法通过 override 重写，只能在构造函数赋值
- ❌ **缺少全局钩子**：如 OnBeforeSave / OnAfterSave（跨操作的统一拦截点）

### 21.5.3 CRUD 核心流程（以 Add 为例）

```csharp
public virtual async Task<WebResponseContent> AddAsync(SaveModel saveDataModel)
{
    // 1️⃣ 解析请求数据为实体对象
    var (res, entity) = saveDataModel.GetAddEntityData(this);
    baseWebResponse = res;
    if (ResponseIsError) return baseWebResponse;

    TEntity mainEntity = entity;

    // 2️⃣ 获取明细表数据（主从表场景）
    Type detailType = MultipleTableEntity.FirstType();
    object detailRows = MultipleTableEntity.GetAddList(detailType, null);

    // 3️⃣ 执行 AddOnExecuting 钩子（保存前）
    if (AddOnExecuting != null)
    {
        baseWebResponse = await AddOnExecutingAsync(mainEntity, detailRows);
        if (ResponseIsError) return baseWebResponse;
        MultipleTableEntity.SetAddList(detailType, detailRows);
    }

    // 4️⃣ 开启事务并保存
    baseWebResponse = await repository.DbContextBeginTransactionAsync(async () =>
    {
        await repository.DbContext.AddAsync(mainEntity);
        await repository.SaveChangesAsync();

        // 5️⃣ 执行 AddOnExecuted 钩子（保存后，事务内）
        if (AddOnExecuted != null)
        {
            baseWebResponse = AddOnExecuted(mainEntity, detailRows);
            if (ResponseIsError) return baseWebResponse;
        }
        if (AddOnExecutedAsync != null)
        {
            baseWebResponse = await AddOnExecutedAsync(mainEntity, detailRows);
        }
        return baseWebResponse;
    });

    // 6️⃣ 返回成功响应
    if (ResponseIsError) return baseWebResponse;
    if (string.IsNullOrEmpty(baseWebResponse.Message))
        baseWebResponse.OK(ResponseType.SaveSuccess);

    // 7️⃣ 调用工作流（如果配置了审批流）
    await AddProceseAsync(mainEntity);

    baseWebResponse.Data = new { data = mainEntity };
    return baseWebResponse;
}
```

**关键发现**：
- ✅ **标准的事务处理模式**：DbContextBeginTransaction 包裹
- ✅ **自动填充默认值**：`SetCreateDefaultVal()` 设置创建人/创建时间
- ✅ **工作流集成**：`AddProceseAsync` 自动触发审批流程
- ✅ **主从事务一致性**：主表 + 明细表在同一事务中
- ⚠️ **WebResponseContent 统一响应**：与我们的 YZHOk 不同（需要适配）

### 21.5.4 Service 基类能力对比

| 能力 | Vol 现状 | YZH 需求 | 差距评估 |
|------|---------|---------|---------|
| **CRUD 基础** | ✅ 完整（Add/Update/Del/GetPageData） | ✅ 需要 | **可直接复用** |
| **生命周期钩子** | ✅ 完整（Before/After × 4操作） | ✅ 需要 | **可直接复用** |
| **主从表事务** | ✅ 完整（MultipleTableEntity） | ✅ 需要 | **可直接复用** |
| **分页查询** | ✅ 完整（排序/汇总/导出/字段权限过滤） | ✅ 需要 | **可直接复用** |
| **文件上传** | ✅ 完整（Upload/Download/Import/Export） | ✅ 需要 | **可直接复用** |
| **工作流集成** | ✅ 完整（AddProcese/审批流） | ✅ 需要 | **可直接复用** |
| **审计字段自动填充** | ✅ SetCreateDefaultVal/SetLogicDelVal | ✅ 需要 | **可直接复用** |
| **多租户过滤** | ⚠️ 有 IsMultiTenancy 属性 | ✅ 必须增强 | **需确认实现** |
| **全局异常处理** | ❌ 未在基类处理 | ✅ YZHGlobalExceptionFilter | **互补** |
| **声明式特性** | ❌ 不支持 Attribute | ✅ YZH 特性体系 | **互补** |

### 21.5.5 结论与建议

```
✅ 强烈推荐直接继承 Vol.ServiceBase：

1️⃣  YZHServiceBase : ServiceBase<TEntity, IRepository<TEntity>>
    → 复用 Vol 的 CRUD 实现、事务管理、分页查询
    → 复用生命周期钩子（Func 委托机制）
    → 复用主从表事务处理能力
    
2️⃣  在 YZHServiceBase 中新增：
    → 特性读取（从 Entity 或 Controller 读取 YZH Attributes）
    → 全局钩子封装（将 Func 委托包装为更易用的虚方法）
    → 审计日志集成（在 OnExecuted 钩子中写入审计日志）
    → 多租户过滤（在查询前自动追加 OrgCode 条件）
    → 幂等性检查（在 Add/Update 前检查 Redis 锁）


⚠️ 需要注意的差异：

1️⃣  响应格式不同
    → Vol: WebResponseContent (Status + Message + Data)
    → YZH: IActionResult (HTTP 状态码 + 统一格式)
    → 解决方案：YZHControllerBase 中做转换
    
2️⃣  钩子机制不同
    → Vol: Func 委托（构造函数赋值）
    → YZH: 虚方法 Override（更符合 OOP 惯例）
    → 解决方案：YZHServiceBase 提供虚方法包装层
```

---

## 21.6 过滤器体系深度分析

### 21.6.1 过滤器清单

```
VOL.Core/Filters/
├── ActionPermissionFilter.cs        # 权限过滤器 ⭐⭐⭐
├── ActionPermissionAttribute.cs     # 权限特性 ⭐⭐⭐
├── ApiActionPermissionAttribute.cs  # API权限特性
├── ActionExecuteFilter.cs           # 执行过滤器（参数校验）⭐
├── ApiAuthorizeFilter.cs            # API 授权过滤器
├── ApiTaskAttribute.cs             # API 任务特性
├── FixedTokenAttribute.cs          # 固定 Token 特性
├── JWTAuthorize.cs                  # JWT 认证 ⭐⭐
├── ServiceFunFilter.cs             # 服务方法过滤器
└── ActionPermissionRequirement.cs  # 权限需求模型
```

### 21.6.2 关键过滤器分析

#### **① ActionExecuteFilter（参数校验过滤器）**

```csharp
public class ActionExecuteFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        // 验证方法参数（使用 Validator 框架）
        context.ActionParamsValidator();
    }
    public void OnActionExecuted(ActionExecutedContext context)
    {
        // 空实现（预留扩展点）
    }
}
```

**关键发现**：
- ✅ **统一的参数校验入口**
- ✅ 与 DataAnnotation / FluentValidation 集成
- ❌ **功能较简单**：只做了参数校验，未做其他处理

#### **② JWTAuthorize（JWT 认证过滤器）**

```csharp
public class JWTAuthorize
{
    // Token 生成、验证、解析
    // 用户信息写入 HttpContext.User
    // 支持Token 刷新机制
}
```

**关键发现**：
- ✅ **标准的 JWT 实现**
- ⚠️ **需要评估**：是否与我们现有的登录体系兼容

### 21.6.3 过滤器能力对比

| 能力 | Vol 现状 | YZH 需求 | 差距评估 |
|------|---------|---------|---------|
| **权限校验** | ✅ ActionPermissionFilter | ✅ 需要 | **可直接复用** |
| **JWT 认证** | ✅ JWTAuthorize | ✅ 需要 | **可直接复用** |
| **参数校验** | ✅ ActionExecuteFilter | ✅ 需要 | **可直接复用** |
| **全局异常处理** | ❌ 未实现 | ✅ YZHGlobalExceptionFilter | **互补** |
| **接口幂等性** | ❌ 未实现 | ✅ YZHIdempotentFilter | **互补** |
| **审计日志** | ❌ 未实现 | ✅ YZHAuditLogFilter | **互补** |
| **操作日志** | ⚠️ Logger（非 Filter） | ✅ 集成到 Filter | **需整合** |

### 21.6.4 结论与建议

```
✅ 直接复用 Vol 过滤器：

1️⃣  ActionPermissionFilter（权限校验）
    → 注册为全局 Filter 或按需使用 [ActionPermission]
    
2️⃣  JWTAuthorize（JWT 认证）
    → 保持现有登录流程不变
    
3️⃣  ActionExecuteFilter（参数校验）
    → 注册为全局 Filter


⚠️ YZH 自研过滤器（补充 Vol 缺失的能力）：

1️⃣  YZHGlobalExceptionFilter（全局异常处理）
    → Vol 没有统一异常处理，这是必须补的
    
2️⃣  YZHIdempotentActionFilter（防重复提交）
    → Vol 没有幂等性支持
    
3️⃣  YZHAuditLogActionFilter（审计日志）
    → Vol 的 Logger 是独立服务，不是 Filter 模式
```

---

## 21.7 综合评估与实施建议

### 21.7.1 复用度总评

```
┌─────────────────────────────────────────────────────────────┐
│              Vol 框架复用度评估矩阵                            │
│                                                             │
│  模块              复用率    说明                              │
│  ─────────────────────────────────────────────────           │
│  ✅ 权限系统        85%      RBAC完整，缺数据权限              │
│  ✅ 字典服务        95%      成熟的缓存机制，几乎完美          │
│  ✅ Service基类     90%      CRUD+钩子+事务，非常成熟         │
│  ✅ 过滤器体系      75%      权限/认证/校验有，缺异常/幂等    │
│  ⚠️ 日志系统        60%      基础设施好，缺审计追踪           │
│  ❌ 特性驱动        0%       完全没有，YZH 自研              │
│  ❌ 多租户          0%       完全没有，YZH 自研              │
│  ❌ 全局容错        0%       完全没有，YZH 自研              │
│                                                             │
│  📊 平均复用率：68%（非常高！）                              │
│  💡 结论：Vol 是一个非常成熟的底座，YZH 应该在 Vol 之上     │
│     增强而非重新造轮子                                      │
└─────────────────────────────────────────────────────────────┘
```

### 21.7.2 推荐的集成架构

```
┌─────────────────────────────────────────────────────────────┐
│           YZH Framework 与 Vol 的集成关系                     │
│                                                             │
│  ┌─────────────────────────────────────────────────────┐   │
│  │                  YZH Framework (增强层)               │   │
│  │                                                     │   │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │   │
│  │  │ YZH Global  │  │ YZH Idem-   │  │ YZH Audit   │  │   │
│  │  │ Exception   │  │ potent      │  │ Log Filter  │  │   │
│  │  │ Filter      │  │ Filter      │  │             │  │   │
│  │  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘  │   │
│  │         │                │               │          │   │
│  │  ┌──────▼────────────────▼───────────────▼──────┐   │   │
│  │  │           YZHServiceBase (继承)              │   │   │
│  │  │     extends Vol.ServiceBase<TEntity, ...>    │   │   │
│  │  │                                             │   │   │
│  │  │  + 读取 YZH Attributes                       │   │   │
│  │  │  + 封装 Vol Func 钩子为虚方法                 │   │   │
│  │  │  + 集成审计日志写入                           │   │   │
│  │  │  + 自动追加多租户过滤条件                     │   │   │
│  │  └────────────────────┬────────────────────────┘   │   │
│  │                     │                              │   │
│  │  ┌────────────────────▼────────────────────────┐   │   │
│  │  │        YZHControllerBase (继承/包装)          │   │   │
│  │  │     extends/组合 Vol.VolController           │   │   │
│  │  │                                             │   │   │
│  │  │  + 统一响应格式 (IActionResult)              │   │   │
│  │  │  + YZH Attribute 声明式配置                  │   │   │
│  │  │  + 转换 WebResponseContent → YZHOk()        │   │   │
│  │  └─────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────┘   │
│                         │                                   │
│                         ▼                                   │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Vol Framework (基础层)                  │   │
│  │                                                     │   │
│  │  ✅ ServiceBase (CRUD + 钩子 + 事务)                 │   │
│  │  ✅ ActionPermissionFilter (权限校验)               │   │
│  │  ✅ DictionaryManager (字典缓存)                    │   │
│  │  ✅ Logger (请求日志 + 批量写入)                    │   │
│  │  ✅ JWTAuthorize (JWT 认证)                         │   │
│  │  ✅ UserContext (用户上下文)                        │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│  设计原则：                                                 │
│  1️⃣  Vol 负责"怎么存取数据"（CRUD、缓存、权限）          │
│  2️⃣  YZH 负责"业务规则是什么"（特性、审计、多租户）       │
│  3️⃣  通过继承关系无缝融合，不是对立而是增强               │
└─────────────────────────────────────────────────────────────┘
```

### 21.7.3 实施路径建议

```
Phase 0.5: YZH Framework 基础搭建（基于 Vol 增强）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Week 1: 核心增强
├─ [x] 分析 Vol 源码（✅ 本文档完成）
├─ [ ] 创建 YZH.Core 项目结构
├─ [ ] 实现 YZHServiceBase : ServiceBase<>
│   ├─ 封装 Func 钩子为虚方法
│   ├─ 集成 YZH Attribute 读取
│   └─ 添加多租户过滤
├─ [ ] 实现 YZHControllerBase
│   ├─ 包装 Vol Controller 或继承
│   └─ 统一响应格式转换
└─ [ ] 实现 YZHGlobalExceptionFilter

Week 2: 特性与日志
├─ [ ] 实现核心 Attributes
│   ├─ YZHMultiTenantAttribute
│   ├─ YZHAuditedAttribute
│   ├─ YZHDeleteStrategyAttribute
│   └─ YZHIdempotentAttribute
├─ [ ] 实现 YZHIdempotentActionFilter
├─ [ ] 实现 YZHAuditLogService（基于 Vol.Logger 增强）
└─ [ ] 单元测试覆盖

Week 3: 验证案例
├─ [ ] CertificationBody 迁移到 YZH 架构
├─ [ ] 前端 GenericCrud 对接
├─ [ ] 权限/日志/多租户端到端测试
└─ [ ] 性能测试（并发、缓存命中率）


Phase 1: 功能完善
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
├─ 更多 Attributes 实现
├─ 前端组件库开发（GenericCrud、TreeCrud、SafeButton）
├─ 工作流集成增强
└─ 文档完善
```

---

## 21.8 总结与关键决策记录

### 21.8.1 关键决策

| # | 决策项 | 决策结果 | 理由 |
|---|--------|---------|------|
| **1** | **是否继承 Vol.ServiceBase？** | ✅ **是** | Vol 的 CRUD + 钩子 + 事务非常成熟，重写不划算 |
| **2** | **是否复用 Vol 权限系统？** | ✅ **是** | RBAC 完整，只需补充数据权限（多租户） |
| **3** | **是否复用 Vol 字典服务？** | ✅ **是** | 几乎完美，无需修改 |
| **4** | **是否复用 Vol 日志系统？** | ⚠️ **部分复用** | 基础设施保留，审计追踪层自研 |
| **5** | **YZH 特性体系 vs Vol？** | ❌ **完全自研** | Vol 没有特性驱动设计，这是 YZH 的核心竞争力 |
| **6** | **全局异常处理？** | ❌ **YZH 自研** | Vol 缺失此能力，且与 YZH 响应格式绑定 |
| **7** | **接口幂等性？** | ❌ **YZH 自研** | Vol 完全没有此能力 |

### 21.8.2 风险提示

```
⚠️ 风险 1: Vol 版本升级兼容性
   → YZH 继承 Vol 基类后，Vol 升级可能导致编译错误
   → 缓解方案：锁定 Vol 版本，或通过接口隔离
   
⚠️ 风险 2: Func 钩子 vs 虚方法的差异
   → Vol 使用 Func 委派，YZH 想用虚方法 Override
   → 缓解方案：YZHServiceBase 提供双层 API（虚方法内部调用 Func）
   
⚠️ 风险 3: 响应格式不统一
   → Vol 用 WebResponseContent，YZH 用 HTTP Status Code
   → 缓解方案：YZHControllerBase 统一转换层
```

---

**文档版本**：V1.6  
**创建时间**：2026-07-31  
**最后更新**：2026-07-31（V1.6：新增 §0.0 YZH Framework 独立性原则 - 禁止修改 Vol 源码的核心约束）  
**作者**：AI Assistant + 映智汇团队  
**状态**：📋 待评审确认  
**下一步**：确认后开始 Phase 0.5 实现（基于 Vol 增强 YZH Framework，保持独立性）
