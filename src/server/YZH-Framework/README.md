# YZH-Framework

**定位**：映智汇（YZH / YingZhiHui）跨项目复用的 .NET 全栈框架增强层  
**版本**：1.0.0-alpha.1  
**状态**：🚧 Phase 1 基础设施建设中（80% 完成）  
**许可证**：待确定（建议 MIT 或 Apache 2.0）

---

## 📖 目录

- [🎯 核心定位](#-核心定位)
- [🏗️ 项目结构](#-项目结构)
- [⚡ 快速开始](#-快速开始)
- [🔧 开发环境要求](#-开发环境要求)
- [📦 架构设计原则](#-架构设计原则)
- [🎨 核心组件说明](#-核心组件说明)
- [🧪 测试指南](#-测试指南)
- [📚 文档索引](#-文档索引)
- [🛣️ 路线图](#️-路线图)
- [❓ FAQ](#-faq)

---

## 🎯 核心定位

### YZH 是什么？

YZH Framework 是在 **Vol 框架**之上的**增量增强层**，不是替代品。

```
┌─────────────────────────────────────┐
│         业务代码（具体项目）          │
│    CertificationBodyService         │
└──────────────┬──────────────────────┘
               │ 继承
┌──────────────▼──────────────────────┐
│     YZH Framework（增量增强层）✨     │
│                                     │
│  ✅ 特性驱动设计（Attribute）        │
│  ✅ 统一审计字段自动填充             │
│  ✅ 编码规则引擎                     │
│  ✅ 声明式校验                       │
│  ✅ 删除策略管理                     │
│  ✅ 多租户数据隔离                   │
│  ✅ 接口幂等性（防重复提交）         │
└──────────────┬──────────────────────┘
               │ 依赖（不修改）
┌──────────────▼──────────────────────┐
│      Vol Framework（基础能力层）      │
│                                     │
│  ✅ CRUD + 钩子 + 事务               │
│  ✅ RBAC 权限系统                    │
│  ✅ 字典缓存                         │
│  ✅ JWT 认证                         │
│  ✅ 日志基础设施                     │
└─────────────────────────────────────┘
```

### 核心价值

| 维度 | Vol 负责 | YZH 增强 |
|------|---------|---------|
| **数据存取** | CRUD、缓存、事务 | 审计字段自动填充、多租户过滤 |
| **权限控制** | RBAC、菜单权限 | 数据权限、操作级权限 |
| **日志记录** | 请求日志、异常日志 | 业务审计日志、字段变更追踪 |
| **代码生成** | 无 | 编码规则引擎（幂等、并发安全） |
| **数据校验** | DataAnnotations | 声明式业务校验（唯一性、复杂条件） |
| **删除策略** | 物理删除 | 逻辑删除（默认）、级联删除 |

### 设计哲学

1. **声明式优于命令式** → 用 Attribute 声明意图，而非硬编码
2. **约定优于配置** → 提供合理默认值，80% 场景零配置
3. **全局容错优于局部捕获** → 禁止 try-catch 泛滥，统一异常处理
4. **组合优于继承** → 通过特性组合能力，避免深层继承链
5. **渐进式完善** → 先建立正确方针和接口，实现可迭代

---

## 🏗️ 项目结构

```
src/server/YZH-Framework/
│
├── YZH.sln                          # 解决方案文件
├── README.md                        # 本文件（开发者指南）
├── .editorconfig                    # 代码风格统一配置
├── Directory.Build.props            # 统一版本号和 NuGet 包引用
│
├── YZH.Core/                        # 🔬 核心类库（已实现 80%）
│   ├── YZH.Core.csproj              # 项目文件
│   ├── YZHModule.cs                 # Autofac 模块注册入口
│   │
│   ├── Entities/                    # 实体基类
│   │   └── YZHBaseEntity.cs         # ✅ 统一审计字段（12 字段 + 辅助方法）
│   │
│   ├── Attributes/                  # 特性定义（接口 + 占位实现）
│   │   └── (Phase 2 实现)           # TODO:P2
│   │
│   ├── Audit/                       # 审计模块
│   │   └── YZHAuditedAttribute.cs   # ✅ 审计标注特性（完整参数定义）
│   │
│   ├── CodeRule/                    # 编码规则模块
│   │   └── ICodeRule.cs             # ✅ 编码规则接口 + 配置类 + 特性
│   │
│   ├── DeleteStrategy/              # 删除策略模块
│   │   └── IDeleteStrategy.cs       # ✅ 删除策略接口 + 枚举 + 特性
│   │
│   ├── Validation/                  # 校验模块
│   │   └── YZHValidationAttribute.cs # ✅ 校验特性基类 + 内置特性声明
│   │
│   ├── Filters/                     # 过滤器扩展（Phase 2）
│   │   └── (TODO:P2)
│   │
│   ├── Services/                    # 服务基类（Phase 2）
│   │   └── (TODO:P2)
│   │
│   └── Controllers/                 # 控制器基类（Phase 3）
│       └── (TODO:P3)
│
├── YZH.Core.Tests/                  # 🧪 单元测试项目（已建立骨架）
│   ├── YZH.Core.Tests.csproj        # 测试项目文件
│   └── Entities/
│       └── YZHBaseEntityTests.cs    # ✅ 基础字段默认值测试
│
└── YZH.CertPlatform/                # 🏢 认证平台业务实体（Phase 2 迁移目标）
    ├── YZH.CertPlatform.csproj      # 项目文件
    └── Entities/
        └── _placeholder.md         # 待迁移实体占位
```

---

## ⚡ 快速开始

### 前置条件

- ✅ Visual Studio 2022 (v17.8+) 或 JetBrains Rider 2023.3+
- ✅ .NET 8.0 SDK
- ✅ Vol 框架源码（`Vue.NetCore/vol.api/`）
- ✅ MySQL 8.0 / SQL Server（任选）
- ✅ Git 版本控制

### 步骤 1：克隆并编译

```bash
# 进入 YZH-Framework 目录
cd src/server/YZH-Framework

# 还原 NuGet 包
dotnet restore

# 编译解决方案
dotnet build --configuration Debug

# 运行测试
dotnet test --verbosity normal
```

### 步骤 2：集成到 Vol 项目

在 Vol 项目的 `Startup.cs` 或 `Program.cs` 中添加一行：

```csharp
// 文件位置：vol.api/VOL.WebApi/Startup.cs（或 Program.cs）

public void ConfigureServices(IServiceCollection services)
{
    // ... Vol 原有配置 ...
    
    // 集成 YZH Framework（仅此一行！）
    builder.RegisterModule(new YZH.Core.YZHModule());
    
    // ... 其他配置 ...
}
```

### 步骤 3：使用 YZHBaseEntity

```csharp
// 在 YZH.CertPlatform 项目中定义业务实体
using YZH.Core.Entities;

namespace YZH.CertPlatform.Entities
{
    // 应用编码规则
    [YZHCodeRule(Prefix = "CB", DateFormat = "yyyyMM", SerialLength = 4)]
    // 启用审计追踪
    [YZHAudited(TrackChanges = true, Category = AuditCategory.Certification)]
    // 使用默认逻辑删除（无需显式声明）
    public class CertificationBody : YZHBaseEntity
    {
        // 业务字段...
        public string Name { get; set; }
        
        [YZHUnique("统一社会信用代码已存在")]
        public string CreditCode { get; set; }
        
        [YZHRequired("机构简称不能为空")]
        [YZHLength(50)]
        public string ShortName { get; set; }
    }
}
```

---

## 🔧 开发环境要求

### 必需工具

| 工具 | 版本 | 用途 |
|------|------|------|
| .NET SDK | 8.0+ | 编译运行 |
| Visual Studio 2022 | v17.8+ | IDE 开发（推荐） |
| JetBrains Rider | 2023.3+ | 替代 IDE（可选） |
| MySQL Workbench | 8.0+ | 数据库管理 |
| Git | 2.x+ | 版本控制 |

### 可选工具

| 工具 | 用途 |
|------|------|
| ReSharper | 代码分析和重构 |
| dotCover | 代码覆盖率分析 |
| Postman | API 测试 |

### IDE 配置建议

1. **安装 .editorconfig 插件** - 自动应用代码风格
2. **启用可空引用类型警告** - 提前发现空引用问题
3. **配置代码格式化快捷键** - Ctrl+E, D（格式化文档）
4. **启用文件头模板** - 自动添加版权信息

---

## 📦 架构设计原则

### ⛔ 不可违反的铁律

| # | 铁律 | 正确做法 | 错误做法 |
|---|------|---------|---------|
| **1** | **禁止修改 Vol 源码** | 在 YZH.Core 中继承和扩展 | 直接修改 VOL.Core/ 下的任何文件 |
| **2** | **禁止 try-catch 泛滥** | 抛出 `YZHBusinessException` | 每个 Service 方法都包 try-catch |
| **3** | **禁止手动设置审计字段** | 由 `YZHServiceBase` 自动填充 | 手动设置 CreateID/CreateDate 等 |
| **4** | **禁止硬编码业务编码** | 使用 `[YZHCodeRule]` 配置 | 直接拼接字符串 "CB" + 序号 |
| **5** | **禁止创建中间件** | 使用 `IAsyncActionFilter` | 自定义 Middleware 类 |

### ✅ 推荐的开发模式

```
模式 A: 直接继承（推荐用于 80% 场景）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
场景：Vol 功能已满足需求，只需添加 YZH 特有能力

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

// 从 Vol.ServiceBase 拷贝核心代码到 YZH
public class YZHEnhancedServiceBase<TEntity, TRepository>
{
    // Vol 原有 CRUD 逻辑（拷贝）
    // + YZH 改进的钩子机制（虚方法替代 Func）
    // 注释必须标注 "Derived from Vol.ServiceBase"
}

模式 C: 完全自研（仅用于 YZH 独有功能）
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
场景：Vol 完全没有此能力，YZH 创新功能

[AttributeUsage(AttributeTargets.Class)]
public class YZHCodeRuleAttribute : Attribute  // 完全自研
{
    public string Pattern { get; set; }
}
```

---

## 🎨 核心组件说明

### 1️⃣ YZHBaseEntity（实体基类）

**文件位置**：`YZH.Core/Entities/YZHBaseEntity.cs`  
**状态**：✅ Phase 1 已完成  
**继承关系**：`YZHBaseEntity → Vol.BaseEntity`

#### 字段列表（12 个统一字段）

| 分类 | 字段名 | 类型 | 说明 | 自动填充时机 |
|------|--------|------|------|-------------|
| **业务编码** | Code | `string` | 业务标识（非主键） | 新增时由 ICodeRule 生成 |
| **多租户** | OrgCode | `string` | 组织编码（数据隔离） | 新增时从 UserContext 获取 |
| **创建信息** | CreateID | `int?` | 创建人 ID | 新增时自动填充 |
| | Creator | `string` | 创建人姓名 | 新增时自动填充 |
| | CreateDate | `DateTime?` | 创建时间 | 新增时自动填充 |
| **修改信息** | ModifyID | `int?` | 修改人 ID | 更新时自动填充 |
| | Modifier | `string` | 修改人姓名 | 更新时自动填充 |
| | ModifyDate | `DateTime?` | 修改时间 | 更新时自动填充 |
| **删除信息** | DeleteID | `int?` | 删除人 ID | 逻辑删除时填充 |
| | Deleter | `string` | 删除人姓名 | 逻辑删除时填充 |
| | DeleteTime | `DateTime?` | 删除时间 | 逻辑删除时填充 |
| **状态辅助** | Enable | `bool` | 启用状态（默认 true） | 新增时默认 true |
| | Sort | `int` | 排序号（默认 0） | 手动设置 |
| | Remark | `string` | 备注 | 手动设置 |

#### 辅助方法

```csharp
var entity = new CertificationBody();

// 填充创建信息（由框架调用）
entity.FillCreateInfo(userId: 1, userName: "管理员", orgCode: "CB001");

// 填充修改信息
entity.FillModifyInfo(userId: 1, userName: "管理员");

// 标记为逻辑删除
entity.MarkAsDeleted(userId: 1, userName: "管理员");
// 结果：Enable=false, DeleteID=1, Deleter="管理员", DeleteTime=现在

// 判断状态
bool isDeleted = entity.IsDeleted;   // false（Enable=true 时）
bool isDisabled = entity.IsDisabled; // false
```

---

### 2️⃣ YZHAuditedAttribute（审计标注特性）

**文件位置**：`YZH.Core/Audit/YZHAuditedAttribute.cs`  
**状态**：✅ 接口定义完成，TODO:P2 实现逻辑

#### 参数说明

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| TrackChanges | bool | false | 是否记录字段新旧值对比 |
| Category | AuditCategory | General | 审计分类（Certification/Audit/System...） |
| Scope | AuditScope | Crud | 追踪级别（Crud/Audit/All） |
| TableName | string | null | 自定义审计表名（null 则使用约定命名） |
| SensitiveFields | string | null | 需脱敏的字段列表（逗号分隔） |
| ExcludeFields | string | null | 排除审计的字段列表（逗号分隔） |

#### 使用示例

```csharp
[YZHAudited(
    TrackChanges: true,
    Category: AuditCategory.Certification,
    Scope: AuditScope.Audit,
    SensitiveFields: "MobilePhone,IDCard",
    ExcludeFields: "Remark")]
public class CertificationBody : YZHBaseEntity { }
```

---

### 3️⃣ ICodeRule（编码规则引擎）

**文件位置**：`YZH.Core/CodeRule/ICodeRule.cs`  
**状态**：✅ 接口定义完成，TODO:P2 实现算法

#### CodeRuleConfig 配置项

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| Prefix | string | 必填 | 编码前缀（如 "CB"） |
| DateFormat | string | "yyyyMM" | 日期格式（null/yyyy/yyyyMM/yyyyMMdd） |
| SerialLength | int | 4 | 序列号位数（支持 0000-9999） |
| ResetRule | SerialResetRule | Monthly | 重置规则（None/Daily/Monthly/Yearly） |
| StartSerial | int | 1 | 序列号起始值 |
| Separator | string | "" | 分隔符（如 "-" → CB-202607-0001） |
| IncludeCheckDigit | bool | false | 是否包含校验位（Mod 11） |

#### 使用示例

```csharp
[YZHCodeRule(
    Prefix: "AP",
    DateFormat: "yyyyMMdd",
    SerialLength: 6,
    ResetRule: SerialResetRule.Daily)]
public class CertificationApplication : YZHBaseEntity { }

// 自动生成示例：
// AP20260731000001
// AP20260731000002
// AP20260801000001（每日重置）
```

---

### 4️⃣ IDeleteStrategy（删除策略）

**文件位置**：`YZH.Core/DeleteStrategy/IDeleteStrategy.cs`  
**状态**：✅ 接口定义完成，TODO:P3 实现逻辑

#### DeleteMode 枚举

| 模式 | 值 | 行为 | 适用场景 |
|------|-----|------|---------|
| Logical | 0 | 设置 Enable=false + 填充删除信息 | **90% 的业务表（默认）** |
| Physical | 1 | 直接 DELETE 记录 | 临时文件、日志缓存 |
| Cascade | 2 | 删除主表 + 级联删除从表 | 主从表结构 |

#### 使用示例

```csharp
// 默认逻辑删除（推荐，无需声明）
public class CertificationBody : YZHBaseEntity { }

// 显式物理删除
[YZHDeleteStrategy(Mode = DeleteMode.Physical)]
public class TempFile : YZHBaseEntity { }

// 级联删除
[YZHDeleteStrategy(
    Mode = DeleteMode.Cascade,
    CascadeEntities = typeof(Detail[]))]
public class Order : YZHBaseEntity { }
```

---

### 5️⃣ YZHValidationAttribute（校验体系）

**文件位置**：`YZH.Core/Validation/YZHValidationAttribute.cs`  
**状态**：✅ 抽象基类 + 内置特性声明完成，TODO:P2 实现逻辑

#### 内置校验特性

| 特性 | 用途 | 示例 |
|------|------|------|
| YZHRequired | 必填（支持条件必填） | `[YZHRequired]`, `[YZHRequired(Condition="Status==1")]` |
| YZHUnique | 唯一性（支持联合唯一） | `[YZHUnique]`, `[YZHUnique(WithFields=new[]{"OrgCode"})]` |
| YZHLength | 长度限制 | `[YZHLength(50)]`, `[YZHLength(10, 50)]` |
| YZHRegex | 正则匹配 | `[YZHRegex(Pattern=@"^1\d{10}$")]`, `[YZHRegex(PredefinedPattern="MobilePhone")]` |
| YZHRange | 数值/日期范围 | `[YZHRange(0, 100)]`, `[YzHRange(DateTime.Now, DateTime.Now.AddYears(1))]` |

#### 使用示例

```csharp
public class CertificationBody : YZHBaseEntity
{
    [YZHRequired("机构名称不能为空")]
    public string Name { get; set; }
    
    [YZHUnique("统一社会信用代码已存在")]
    public string CreditCode { get; set; }
    
    [YZHLength(50, "机构简称不能超过50个字符")]
    public string ShortName { get; set; }
    
    [YZHRegex(PredefinedPattern="MobilePhone", "手机号格式不正确")]
    public string ContactPhone { get; set; }
}
```

---

## 🧪 测试指南

### 当前测试覆盖

**已完成**：
- ✅ YZHBaseEntity 默认值测试（Enable=true, Sort=0）
- ✅ 基础编译通过验证

**待补充**（Phase 2）：
- ⏳ 辅助方法测试（FillCreateInfo, MarkAsDeleted, IsDeleted）
- ⏳ 校验特性单元测试
- ⏳ 编码规则算法测试
- ⏳ Vol 兼容性集成测试

### 运行测试

```bash
# 运行所有测试
cd src/server/YZH-Framework
dotnet test --verbosity normal

# 运行特定测试类
dotnet test --filter "FullyQualifiedName~YZHBaseEntityTests"

# 运行测试并生成覆盖率报告
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### 测试命名规范

```
测试类命名：{被测类}Tests
测试方法命名：{场景}_{预期行为}

示例：
public class YZHBaseEntityTests
{
    [Fact]
    public void Default_Enable_ShouldBe_True() { ... }
    
    [Fact]
    public void MarkAsDeleted_Should_Set_Enable_False_And_Fill_DeleteInfo() { ... }
    
    [Theory]
    [InlineData(true, null, false)]   // Enable=true, DeleteTime=null → Not deleted
    [InlineData(false, DateTime.Now, true)] // Enable=false, DeleteTime有值 → Deleted
    public void IsDeleted_Should_Return_Correct_Value(bool enable, DateTime? deleteTime, bool expected) { ... }
}
```

---

## 📚 文档索引

### 架构文档（宪法级）

| 文档 | 位置 | 说明 |
|------|------|------|
| **YZH-建设原则-V1.md** | `docs/00-工程体系/` | 最高纲领，所有决策的依据 |
| **协作模型与分工协议-V1.md** | `docs/00-工程体系/` | 团队协作规范 |
| **实施建设方针-V1.md** | `docs/00-工程体系/` | 开发流程和质量标准 |

### 设计文档（设计级）

| 文档 | 位置 | 说明 |
|------|------|------|
| **YZH-改造路线.md** | `docs/50-规划与优先级/` | 三阶段路线图和任务分解 |
| **YZH-Framework架构设计-V1.6.md** | `docs/20-架构决策/` | 完整技术架构（历史归档版） |
| **评审报告-V1.md** | `docs/20-架构决策/` | Vol 分析和改进建议 |

### 知识库（参考级）

| 文档 | 位置 | 说明 |
|------|------|------|
| **README.md** | `docs/60-AI工程设计/YZH-知识库/` | 知识库导航索引 |
| **01-Vol能力清单.md** | 同上 | Vol 32 个钩子、20 条路由全索引 |
| **02-YZH增量清单.md** | 同上 | YZH 11 个组件定义和状态 |
| **03-边界与约束.md** | 同上 | 不可修改清单、硬边界、废弃方案 |
| **04-代码模板/** | 同上 | 可复用的代码片段 |
| **05-踩坑记录/** | 同上 | 改造过程中的教训 |

---

## 🛣️ 路线图

### Phase 1：基础设施建设 ✅（80% 完成）

**目标**：搭建骨架，建立规范  
**时间预算**：4 人天  
**当前状态**：✅ T1.1-T1.8 全部完成

**已完成任务**：
- ✅ T1.1 建立知识库框架
- ✅ T1.2 填充 Vol 能力清单
- ✅ T1.3 定义 YZHBaseEntity（完整版 12 字段）
- ✅ T1.4 注册 YZHModule 到 Vol 容器
- ✅ T1.5 建立测试项目
- ✅ T1.6 统一接口参数定义
- ✅ T1.7 工程化配置（.editorconfig + Directory.Build.props）
- ✅ T1.8 文档一致性修复

---

### Phase 2：核心能力 🔜（待启动）

**目标**：选认证申请模块验证 YZH 能力  
**时间预算**：10 人天  
**关键里程碑**：
- 🎯 M2.1（第 5 天）：YZHServiceBase 可用于 CRUD
- 🎯 M2.2（第 8 天）：校验规则生效
- 🎯 M2.3（第 10 天）：编码规则自动生成
- 🎯 M2.4（第 12 天）：审计日志写入数据库

**待完成任务**：
- ⏳ T2.1 实现 YZHServiceBase（继承 Vol.ServiceBase）
- ⏳ T2.2 实现 YZHValidationRules
- ⏳ T2.3 实现 YZHCodeRule
- ⏳ T2.4 实现 YZHAudited
- ⏳ T2.5 废弃 YZHDecoratorMiddleware
- ⏳ T2.6 回写知识库

---

### Phase 3：扩展完善 📋（规划中）

**目标**：高级能力和持续优化  
**时间预算**：持续迭代

**计划任务**：
- 📋 T3.1 实现 YZHDeleteStrategy
- 📋 T3.2 多租户隔离方案
- 📋 T3.3 接口幂等性（Redis 防重复提交）
- 📋 T3.4 实现 YZHControllerBase
- 📋 T3.5 实现 YZHGlobalExceptionFilter
- 📋 T3.6 知识库持续维护

---

## ❓ FAQ

### Q1: YZH 和 Vol 的关系？

**A**: YZH 不是 Vol 的替代品，而是增量增强层。Vol 负责"怎么存取数据"，YZH 负责"业务规则是什么"。两者通过继承无缝融合。

### Q2: 为什么不能修改 Vol 源码？

**A**: 
1. **升级兼容性** - Vol 升级后不会冲突
2. **跨项目复用** - YZH 可独立抽离为 NuGet 包
3. **团队协作** - 变更可控，边界清晰
4. **法律合规** - 不违反开源协议

### Q3: 如何开始使用 YZH？

**A**: 三步即可：
1. 引用 YZH.Core 项目
2. 在 Startup 中注册 `new YZHModule()`
3. 让你的实体继承 `YZHBaseEntity`

### Q4: 性能有影响吗？

**A**: 影响极小。YZH 主要通过以下方式工作：
- **编译期**：Attribute 读取（零运行时开销）
- **运行时**：Filter 管道（微秒级延迟）
- **数据库**：审计日志异步批量写入

### Q5: 可以只使用部分功能吗？

**A**: 当然可以。YZH 采用"按需组合"设计：
- 只需要审计？→ 只加 `[YZHAudited]`
- 只需要编码规则？→ 只加 `[YZHCodeRule]`
- 需要全部功能？→ 组合多个特性

---

## 🤝 贡献指南

### 代码提交规范

```
feat: 新功能（如：feat: 实现 YZHCodeRule 基础算法）
fix: 修复 Bug（如：fix: 修复 YZHBaseEntity 默认值问题）
docs: 文档更新（如：docs: 补充 API 使用示例）
test: 测试相关（如：test: 增加 FillCreateInfo 单元测试）
refactor: 重构（如：重构: 统一异常层次体系）
chore: 构建/工具（如：chore: 更新 Directory.Build.props）
```

### 分支策略

```
main ← 生产稳定版
  ↑
develop ← 开发主分支
  ↑
feature/xxx ← 功能分支（从 develop 拉出）
hotfix/xxx ← 紧急修复（从 main 拉出）
```

---

## 📄 许可证

待确定（建议 MIT 或 Apache 2.0）

---

## 👥 团队

- **架构设计**：AI Assistant + 映智汇团队
- **Phase 1 开发**：AI Assistant（自动化代码生成）
- **审核确认**：映智汇技术负责人

---

**最后更新**：2026-07-31  
**文档版本**：V1.0（对应 YZH Framework v1.0.0-alpha.1）  
**下一步**：启动 Phase 2 - 核心能力实现
