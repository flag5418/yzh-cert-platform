---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_325408fc8ca711f189c1525400f8a581
    ReservedCode1: noas7748I7SFDhLCssQG8nlSB94smkApeG+2bt7kpdjByVO3xlU5476BoQYDYpjaQh8z9N5GWVSr7ks7jtmabPbVtuQ6ZfNvUunQvGfEKXjlDi5qOFKz7XzPCaYr0tAFC84TWNbndGOD4H3Dq2POkVeZCTCN6c1wsg0IIbh0LdQi2LZ2acMkPm5sBFQ=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_325408fc8ca711f189c1525400f8a581
    ReservedCode2: noas7748I7SFDhLCssQG8nlSB94smkApeG+2bt7kpdjByVO3xlU5476BoQYDYpjaQh8z9N5GWVSr7ks7jtmabPbVtuQ6ZfNvUunQvGfEKXjlDi5qOFKz7XzPCaYr0tAFC84TWNbndGOD4H3Dq2POkVeZCTCN6c1wsg0IIbh0LdQi2LZ2acMkPm5sBFQ=
---

# YZH-Framework 建设原则

**版本**：V1
**日期**：2026-07-31
**状态**：正式发布
**来源**：提取自 YZH-Framework架构设计-V1.0（原 4541 行文档，V1.4）

## 1. 概述

YZH-Framework 是在 Vol 框架之上的增强层，定位为"声明式特性驱动框架"。"YZH"即"映智汇"，核心理念是**在不修改 Vol 源码的前提下**，通过 Autofac 模块挂载、ServiceBase 继承、ActionFilter 扩展三种途径，将多租户隔离、审计追踪、编码规则、删除策略等横切关注点从业务代码中剥离，实现**零业务代码侵入**的架构目标。

YZH 不是 Vol 的替代品，而是 Vol 的增量补强。Vol 负责"怎么存取数据"（CRUD、缓存、权限、字典），YZH 负责"业务规则是什么"（特性声明、审计、多租户、容错）。两者通过继承关系和 Filter 管道无缝融合。

本文件是 YZH-Framework 建设的最高纲领，所有编码决策和架构设计必须对齐本文中的原则。

## 2. 五大设计哲学

### 2.1 声明式优于命令式

**原则定义**：通过特性（Attribute）声明"做什么"，而非在业务代码中写"怎么做"。框架驱动引擎负责读取特性并自动执行。

**在 YZH 中的体现**：
- `[YZHMultiTenant]` 标记实体 → 框架自动注入租户隔离条件，无需在每处查询手写 `WHERE OrgCode = @orgCode`
- `[YZHAudited]` 标记实体 → 框架自动记录增删改的审计日志，无需在每个 Service 方法中手动写日志
- `[YZHDeleteStrategy(Mode = SoftDelete)]` → 框架在删除操作时自动执行逻辑删除

**实例**：
```csharp
// 命令式（传统做法）：业务代码 + 基础设施代码混杂
public async Task SaveAsync(CertBody entity)
{
    entity.OrgCode = UserContext.Current.OrgCode;  // 租户赋值
    entity.CreateDate = DateTime.Now;               // 审计字段
    await _repository.AddAsync(entity);
    await _auditLogService.LogAsync("Create", entity);  // 审计日志
}

// 声明式（YZH 做法）：只需声明特性，框架自动处理
[YZHMultiTenant("OrgCode")]
[YZHAudited(TrackChanges = true)]
public class CertBody : YZHBaseEntity
{
    public string Name { get; set; }
}
// Service 中只需关注业务逻辑，横切关注点由框架接管
```

### 2.2 约定优于配置

**原则定义**：建立统一的命名规范和默认行为，减少显式配置。开发者只需关注例外情况。

**在 YZH 中的体现**：
- `Y` 前缀的审计字段（`CreateID / Creator / CreateDate / ModifyID / Modifier / ModifyDate`）由框架自动填充，约定即生效
- `YZHBaseEntity` 的 `Code` 字段默认为业务编码，`Enable` 默认为 `true`，`Sort` 默认为 `0`
- 审计日志表名约定：`{EntityName}_Log`，无需单独配置
- 控制器路由约定：从 Controller 名称自动推导表名和权限，无需手动指定 `TableName`

**实例**：
```csharp
// 约定（默认行为，零配置）
public class CertBodyService : ServiceBase<CertBody, ICertBodyRepository>
{
    // 框架自动：填充审计字段、启用逻辑删除、记录操作日志
}

// 配置（仅例外情况才需要）
[YZHDeleteStrategy(Mode = DeleteMode.HardDelete)]  // 覆盖默认软删除
public class TempFile : YZHBaseEntity { }
```

### 2.3 全局容错

**原则定义**：框架层面统一捕获和处理异常，上层业务代码**禁止使用 try-catch** 做业务判断。所有异常归入统一的异常层次体系，由全局异常过滤器统一处理。

**在 YZH 中的体现**：
- `YZHGlobalExceptionFilter` 作为全局过滤器，拦截所有未处理异常
- 异常层次体系：`YZHBusinessException`（业务异常，400）、`YZHValidationException`（校验异常，400）、`YZHAuthenticationException`（认证异常，401 / 403）、未分类异常（500）
- 全局异常过滤器与 Vol 的 `ActionPermissionFilter` 风格一致，均使用 `IAsyncActionFilter` 而非中间件

**实例**：
```csharp
// ✅ 正确：业务层只抛出异常
public async Task SaveAsync(CertBody entity)
{
    if (string.IsNullOrWhiteSpace(entity.Name))
        throw new YZHBusinessException("机构名称不能为空");  // 框架统一处理为 400

    await _repository.AddAsync(entity);
}

// ❌ 错误：禁止在业务代码中 try-catch 做流程控制
public async Task SaveAsync(CertBody entity)
{
    try
    {
        await _repository.AddAsync(entity);
    }
    catch (DbUpdateException ex)
    {
        return Error("保存失败");  // 反模式：异常应在框架层统一转换
    }
}
```

**禁止事项**：
- 禁止在 Service / Controller 中使用 `try-catch` 包裹业务逻辑
- 禁止在 `catch` 块中做业务分支判断
- 唯一例外：调用外部不可控服务（短信、支付回调）时可以使用 `try-catch`，但必须记录详细日志

### 2.4 组合优于继承

**原则定义**：优先通过特性组合和接口实现来扩展能力，而非深层继承链。每个特性专注单一职责。

**在 YZH 中的体现**：
- YZH 特性体系采用"接口 + 特性 + Filter"的组合模式，而非让实体继承各种基类
- `YZHBaseEntity` 仅提供最基础的审计字段，其他能力通过特性按需组合：
  ```
  [YZHMultiTenant] + [YZHAudited] + [YZHDeleteStrategy] + [YZHCodeRule]
  ```
- 一个实体可以自由组合需要的特性，不强制全部继承

**实例**：
```csharp
// 组合方式：按需叠加特性
[YZHMultiTenant("OrgCode")]       // 多租户
[YZHAudited(TrackChanges = true)] // 审计
[YZHDeleteStrategy(Mode = SoftDelete)]  // 软删除
public class CertBody : YZHBaseEntity { }

// 简单的配置实体：不需要审计和租户
public class SysConfig : YZHBaseEntity { }  // 仅基础审计字段
```

### 2.5 渐进式完善

**原则定义**：框架按 Phase 分批交付，每阶段产出可独立验证。早期用最小可行实现（MVP），后续基于真实场景反馈逐步增强。

**在 YZH 中的体现**：
- Phase 1（基础设施建设）：YZHBaseEntity + 审计字段自动填充 + Vol 能力清单
- Phase 2（特性体系）：YZHAudited + YZHCodeRule + YZHDeleteStrategy + YZHMultiTenant
- Phase 3（高级能力）：接口幂等性 YZHIdempotent + 动态权限规则引擎

**实例**：
- Phase 1 的 `YZHBaseEntity` 是 Phase 2 所有特性的基础
- 编码规则 `YZHCodeRule` 在 Phase 2 仅实现固定前缀模式，Phase 3 再支持动态占位符（日期、流水号）

## 3. 架构边界原则

### 3.1 YZH 与 Vol 的硬边界

| 规则 | 说明 |
|------|------|
| **YZH 做增量不做替代** | YZH 不重写 Vol 已有能力（CRUD、权限、字典、日志基础设施），只在 Vol 之上做增强 |
| **YZH 不侵入 Vol 源码** | 禁止直接修改 `VOL.Core/`、`vol.api/` 中的任何文件，YZH 的所有代码在独立项目 `YZH.Core` 中 |
| **YZH 通过 Autofac 模块挂载** | 在 `vol.api` 的 `Startup` 或 `Program.cs` 中注册 `YZHModule`，不修改 Vol 本身的注册逻辑 |
| **中间件只能用 IAsyncActionFilter** | 禁止在 YZH 中创建新的中间件管道，与 Vol 的 `ActionPermissionFilter` 风格一致 |
| **Filter 注册顺序** | YZH 的 Filter 注册在 Vol 权限 Filter 之前（`int.MinValue + 100` 优先级），确保幂等性检查等先执行 |

### 3.2 继承边界

| 继承关系 | 说明 |
|------|------|
| `YZHServiceBase<T> : Vol.ServiceBase<T>` | YZH 服务基类继承 Vol 基类，复用 CRUD + 钩子 + 事务 |
| `YZHControllerBase : Vol.ApiBaseController` | YZH 控制器基类继承/包装 Vol 控制器基类 |
| `YZHBaseEntity : Vol.BaseEntity` | YZH 实体继承 Vol 空基类，追加审计字段 |

### 3.3 不可修改的 Vol 源码

| 文件 | 路径 | 原因 |
|------|------|------|
| `ServiceBase` | `VOL.Core/BaseProvider/ApplicationServiceBase.cs` | Vol 核心基础设施，YZH 通过钩子委托扩展 |
| `ApiBaseController` | `VOL.Core/Controllers/Basic/ApiBaseController.cs` | 路由注册依赖此基类 |
| `ActionPermissionFilter` | `VOL.Core/Filters/ActionPermissionFilter.cs` | 权限体系核心，只能扩展不能替换 |
| `ExceptionHandlerMiddleWare` | `VOL.Core/Middleware/ExceptionHandlerMiddleWare.cs` | 全局异常捕获，YZH 不重复实现 |
| `Logger` | `VOL.Core/Services/Logger.cs` | 日志基础设施（队列 + 批量写入） |

## 4. 编码规范

以下规范提取自原设计文档，属于 YZH 范围必须遵守的约定。

### 4.1 实体定义规范

- 实体统一继承 `YZHBaseEntity`，提供 **15 个统一字段**（Phase 1 已完整实现）：
  - **业务编码**：`Code`（业务标识）、`OrgCode`（多租户组织编码）
  - **创建信息**：`CreateID`（int?）、`Creator`、`CreateDate`
  - **修改信息**：`ModifyID`（int?）、`Modifier`、`ModifyDate`
  - **删除信息**：`DeleteID`（int?）、`Deleter`、`DeleteTime`
  - **状态辅助**：`Enable`（bool, 默认 true）、`Sort`（int, 默认 0）、`Remark`
- `CreateID` / `ModifyID` / `DeleteID` 统一使用 **int? 类型**（对应 Sys_User.Id），禁止使用 string
- `Code` 字段作为业务编码，与数据库主键 `Id` 分离（不依赖自增 ID 做业务标识）
- 审计字段命名以 `Create / Modify / Delete` 为前缀，统一风格，避免出现 `CreatedBy / UpdatedAt` 等混用
- 逻辑删除使用 `Enable` + `DeleteTime` 组合判断：
  - `Enable = false && DeleteTime != null` → 已逻辑删除（`IsDeleted` 属性）
  - `Enable = false && DeleteTime == null` → 仅禁用未删除（`IsDisabled` 属性）
  - 不定义独立的 `IsDeleted` 字段，通过计算属性实现

### 4.2 特性命名规范

- YZH 自研特性统一使用 `YZH` 前缀（`YZHAuditedAttribute`、`YZHMultiTenantAttribute`），与 Vol 自带特性区分
- 接口使用 `IYZH` 前缀（`IYZHActionFilter`、`IIdempotentKeyGenerator`）
- 特性一律使用 `Attribute` 后缀，即使使用时省略

### 4.3 响应格式规范

- YZH Controller 返回 `IActionResult`，通过 `YZHOk()` / `YZHError()` 扩展方法统一格式
- 业务异常统一抛出 `YZHBusinessException`（HTTP 400），校验异常抛出 `YZHValidationException`（HTTP 400），框架层统一转换为 JSON 响应
- 不使用 Vol 的 `WebResponseContent` 作为 Controller 返回值，在 `YZHControllerBase` 中完成转换

### 4.4 日志规范

- 业务日志使用 `IYZHAuditLogService`，不直接调用 Vol 的 `Logger.LoggerInfo()`
- 审计日志记录新旧值对比（`TrackChanges = true` 时），敏感字段自动脱敏
- 禁止在日志中明文记录手机号、身份证号、银行卡号等敏感信息

## 5. 禁止事项

以下行为在原设计文档中明确列为反模式或禁止行为。

| # | 禁止事项 | 说明 |
|---|---------|------|
| 1 | **禁止修改 Vol 源码** | 包括 `VOL.Core/`、`vol.api/`、`VOL.Entity/` 中的任何 `.cs` 文件。保持 Vol 升级兼容性的唯一途径 |
| 2 | **禁止在业务代码中使用 try-catch** | 业务异常应抛出 `YZHBusinessException`，由全局异常过滤器统一处理。唯一例外：调用外部不可控服务 |
| 3 | **禁止在 YZH 中创建中间件** | 使用 `IAsyncActionFilter` 实现横切关注点，与 Vol 风格一致 |
| 4 | **禁止在 Service 中硬编码租户过滤** | 使用 `[YZHMultiTenant]` 特性，框架自动注入过滤条件 |
| 5 | **禁止在实体中重复定义审计字段** | 所有实体继承 `YZHBaseEntity`，不再单独定义 `CreateDate` 等字段 |
| 6 | **禁止在 Controller 中直接返回 Vol 的 WebResponseContent** | 使用 `YZHOk()` / `YZHError()` 统一响应格式 |
| 7 | **禁止直接实例化 YZH Service** | 必须通过 DI 容器（Autofac）注入，确保钩子委托和特性驱动正确初始化 |
| 8 | **禁止使用硬编码的业务编码前缀** | 编码规则应通过 `[YZHCodeRule]` 配置，便于统一管理和修改 |
*（内容由AI生成，仅供参考）*
