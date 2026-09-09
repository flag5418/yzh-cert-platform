# YZH-Core 架构演进与问题追踪文档

> **版本**：V1.1 | **日期**：2026-09-07 | **状态**：执行中
>
> **文档用途**：开发过程中查阅，问题追踪，进度管理
>
> **项目结构**：
> ```
> src/server/Vue.NetCore/   → 历史代码（Vol 框架），仅参考，不再完善
> src/yzh-core/             → 新架构框架，已完成 70%
> src/certplatform-api/     → 业务代码（待开发）
> src/certplatform-web/     → 前端代码（待开发）
> ```

---

## 一、当前状态总览

### 1.1 完成度评分

| 模块 | 完成度 | 状态 | 备注 |
|------|--------|------|------|
| **Result<T> 泛型** | 100% | ✅ 完成 | 已替代元组返回值 |
| **IDbOrm 接口** | 100% | ✅ 完成 | 统一数据库操作接口 |
| **DapperDbOrm** | 100% | ✅ 完成 | Dapper 实现，支持多方言 |
| **EntityService** | 100% | ✅ 完成 | 原子能力层 |
| **YzhControllerBase** | 100% | ✅ 完成 | 控制器基类 |
| **系统设置模块** | 100% | ✅ 完成 | SysUser/Role/Menu/Dict |
| **全局异常处理** | 100% | ✅ 完成 | 中间件 + 过滤器 |
| **SQL 注入防护** | 100% | ✅ 完成 | SqlSecurityHelper |
| **审计日志框架** | 100% | ✅ 完成 | 文件日志 |
| **业务异常类型** | 100% | ✅ 完成 | 7 种异常类 |
| **权限拦截器** | 60% | ⚠️ 部分完成 | 代码已写，未完全集成 |
| **输入验证** | 60% | ⚠️ 部分完成 | 代码已写，未完全集成 |
| **JWT 认证** | 70% | ⚠️ 部分完成 | 基础功能完成，缺刷新机制 |
| **TreeControllerBase** | 0% | ❌ 未完成 | 待实现 |
| **TreeUtils** | 0% | ❌ 未完成 | 待实现 |
| **YzhTree 组件** | 0% | ❌ 未完成 | 待实现 |
| **单元测试** | 0% | ❌ 未完成 | 待编写 |
| **前端基类** | 0% | ❌ 未完成 | CrudPageLogic/TreeTableLogic |

---

## 二、问题清单与待完善项

### 2.1 P0 级问题（阻塞开发）

#### 问题 1：EF Core 残留代码
- **位置**：`YZH.Core.DataBase/BaseRepository.cs`
- **状态**：已创建 `DapperDbOrm.cs`，但 `BaseRepository.cs` 未删除
- **影响**：双路并存，维护成本高
- **解决方案**：
  ```
  1. 检查所有引用 BaseRepository 的地方
  2. 确认无引用后删除 BaseRepository.cs
  3. 更新 YzhWebBuilder.cs 中的服务注册
  ```
- **负责人**：开发中
- **截止时间**：Day 1

#### 问题 2：权限拦截器未完全集成
- **位置**：`YZH.Core.Api/Filters/PermissionFilter.cs`
- **状态**：代码已写，但未注册到 Program.cs
- **影响**：RequirePermission 特性不生效
- **解决方案**：
  ```csharp
  // Program.cs 中注册
  builder.Services.AddControllers(options =>
  {
      options.Filters.Add<PermissionFilter>();
  });
  ```
- **负责人**：开发中
- **截止时间**：Day 1

#### 问题 3：输入验证未完全集成
- **位置**：`YZH.Core.Api/Filters/ValidationFilter.cs`
- **状态**：代码已写，但未注册到 Program.cs
- **影响**：参数验证不生效
- **解决方案**：
  ```csharp
  // Program.cs 中注册
  builder.Services.AddControllers(options =>
  {
      options.Filters.Add<ValidationFilter>();
  });
  ```
- **负责人**：开发中
- **截止时间**：Day 1

#### 问题 4：代码未编译验证
- **状态**：沙箱环境限制，无法本地编译
- **影响**：可能存在编译错误未发现
- **解决方案**：
  ```
  1. 在本地环境执行：cd src/yzh-core && dotnet build
  2. 修复所有编译错误
  3. 启动服务验证接口
  ```
- **负责人**：用户
- **截止时间**：Day 1

---

### 2.2 P1 级问题（影响功能）

#### 问题 5：JWT Token 刷新机制缺失
- **位置**：`YZH.Core.Api/Services/`
- **状态**：未实现
- **影响**：Token 过期后需重新登录
- **解决方案**：
  ```csharp
  // 新增 TokenRefreshService
  public class TokenRefreshService
  {
      public async Task<(string newToken, DateTime expiresIn)> RefreshTokenAsync(string refreshToken)
      {
          // 1. 验证 refresh token
          // 2. 生成新 access token
          // 3. 返回新 token 和过期时间
      }
  }
  
  // 新增刷新接口
  [HttpPost("refresh")]
  public IActionResult RefreshToken([FromBody] RefreshTokenRequest request)
  {
      // ...
  }
  ```
- **负责人**：开发中
- **截止时间**：Day 3

#### 问题 6：审计日志未入库
- **位置**：`YZH.Core.Api/Services/YzhAuditLogger.cs`
- **状态**：仅写入文件，未入库
- **影响**：无法查询历史操作记录
- **解决方案**：
  ```
  1. 创建 AuditLog 表
  2. 修改 YzhAuditLogger 支持双写（文件 + 数据库）
  3. 新增 AuditLogController 提供查询接口
  ```
- **负责人**：开发中
- **截止时间**：Day 5

#### 问题 7：TreeControllerBase 未实现
- **位置**：`YZH.Core.Api/Controllers/`
- **状态**：未创建
- **影响**：树形结构接口无法零代码实现
- **解决方案**：
  ```csharp
  // 新增 TreeControllerBase.cs
  public abstract class TreeControllerBase<T, V> : YzhControllerBase<T>
      where T : BaseEntity, ITreeNode
      where V : class, new()
  {
      [HttpGet("tree")]
      public virtual async Task<ApiResponse<List<T>>> GetTree()
      {
          // 实现
      }
      
      [HttpPost("getChildren")]
      public virtual async Task<ApiResponse<List<T>>> GetChildren([FromBody] GetChildrenRequest request)
      {
          // 实现
      }
  }
  ```
- **负责人**：开发中
- **截止时间**：Day 3

#### 问题 8：TreeUtils 未实现
- **位置**：`src/certplatform-web/share/src/utils/`
- **状态**：未创建
- **影响**：前端树操作无工具函数
- **解决方案**：
  ```typescript
  // 新增 treeUtils.ts
  export const TreeUtils = {
    buildTree<T>(items: T[], config: TreeConfig): TreeNode[],
    getDescendants(node: TreeNode): TreeNode[],
    findNode(rootNodes: TreeNode[], code: string): TreeNode | null,
    // ... 其他方法
  }
  ```
- **负责人**：开发中
- **截止时间**：Day 4

#### 问题 9：YzhTree 组件未实现
- **位置**：`src/certplatform-web/yzh.vue.core/src/components/layout/`
- **状态**：未创建
- **影响**：前端树组件缺失
- **解决方案**：
  ```vue
  <!-- 新增 YzhTree.vue -->
  <template>
    <el-tree
      ref="treeRef"
      :data="data"
      :props="treeProps"
      :load="loadNode"
      :lazy="lazy"
    >
      <!-- ... -->
    </el-tree>
  </template>
  ```
- **负责人**：开发中
- **截止时间**：Day 4

---

### 2.3 P2 级问题（优化改进）

#### 问题 10：单元测试缺失
- **位置**：`YZH.Core.Api.Tests/`
- **状态**：未创建
- **影响**：无法保证代码质量
- **解决方案**：
  ```
  1. 创建测试项目
  2. 编写 EntityService 单元测试
  3. 编写 TreeUtils 单元测试
  4. 编写 Controller 集成测试
  ```
- **负责人**：开发中
- **截止时间**：Day 7

#### 问题 11：前端基类缺失
- **位置**：`src/certplatform-web/share/src/logic/`
- **状态**：未创建
- **影响**：页面开发重复代码多
- **解决方案**：
  ```typescript
  // 新增 CrudPageLogic.ts
  export abstract class CrudPageLogic<T> {
    abstract controllerName: string;
    // CRUD 方法
  }
  
  // 新增 TreeTableLogic.ts
  export abstract class TreeTableLogic<T, V> {
    abstract treeConfig: TreeConfig;
    // 树 + 表格方法
  }
  ```
- **负责人**：开发中
- **截止时间**：Day 6

#### 问题 12：前后端协议未标准化
- **状态**：响应格式、错误码未统一
- **影响**：前端对接成本高
- **解决方案**：
  ```
  1. 定义统一的响应格式
  2. 定义统一的错误码规范
  3. 编写 API 文档（Swagger + 补充说明）
  ```
- **负责人**：开发中
- **截止时间**：Day 8

---

## 三、架构决策记录（ADR）

### ADR-001：使用 Dapper 替代 EF Core 作为主要 ORM
- **状态**：已接受
- **日期**：2026-09-07
- **背景**：EF Core 在复杂查询场景性能不足，双路并存导致维护成本高
- **决策**：统一使用 Dapper 作为数据库访问层，保留 EF Core 仅用于简单 CRUD（过渡期）
- **后果**：
  - 正面：性能提升，代码简洁，SQL 完全可控
  - 负面：失去变更追踪等特性，需手动管理事务
- **替代方案**：SqlSugar（功能更强但学习成本高）

### ADR-002：Result<T> 泛型包装替代元组返回值
- **状态**：已接受
- **日期**：2026-09-07
- **背景**：元组返回值 `(T?, string?)` 语义不明确，难以链式调用
- **决策**：使用 `Result<T>` 泛型包装，提供 `Success`/`Error`/`Data` 属性
- **后果**：
  - 正面：类型安全，支持链式调用，易于扩展
  - 负面：增加了包装层，轻微性能开销
- **替代方案**：自定义异常 + null 返回

### ADR-003：配置驱动 UI（GridConfig/TreeConfig）
- **状态**：已接受
- **日期**：2026-09-07
- **背景**：大量单表 CRUD 页面重复代码多
- **决策**：使用 JSON 配置文件驱动前端表格和表单的显示逻辑
- **后果**：
  - 正面：减少重复代码，前后端约定清晰
  - 负面：配置复杂度高，调试困难
- **替代方案**：代码生成器

---

## 四、开发检查清单

### 4.1 Day 1 检查清单
- [ ] 本地编译验证（`dotnet build`）
- [ ] 修复所有编译错误
- [ ] 启动服务，测试 SysUser 接口
- [ ] 删除 `BaseRepository.cs`（确认无引用）
- [ ] 注册 `PermissionFilter` 到 Program.cs
- [ ] 注册 `ValidationFilter` 到 Program.cs
- [ ] 测试权限拦截器是否生效
- [ ] 测试输入验证是否生效

### 4.2 Day 2-3 检查清单
- [ ] 实现 `TreeControllerBase<T>`
- [ ] 实现 `TreeUtils`（前端）
- [ ] 实现 `YzhTree` 组件
- [ ] 实现 JWT Token 刷新机制
- [ ] 测试树形接口是否正常

### 4.3 Day 4-5 检查清单
- [ ] 实现 `CrudPageLogic<T>`（前端）
- [ ] 实现 `TreeTableLogic<T,V>`（前端）
- [ ] 审计日志入库（创建 AuditLog 表）
- [ ] 测试审计日志是否正常记录

### 4.4 Day 6-7 检查清单
- [ ] 编写 EntityService 单元测试
- [ ] 编写 TreeUtils 单元测试
- [ ] 编写 Controller 集成测试
- [ ] 标准化前后端协议
- [ ] 完善 API 文档

---

## 五、待开发模块清单

### 5.1 后端模块（YZH-Core）

| 模块 | 文件路径 | 状态 | 优先级 |
|------|---------|------|--------|
| TreeControllerBase | `YZH.Core.Api/Controllers/TreeControllerBase.cs` | ❌ 未开始 | P1 |
| TokenRefreshService | `YZH.Core.Api/Services/TokenRefreshService.cs` | ❌ 未开始 | P1 |
| AuditLog 实体 | `YZH.Core.Api/Models/AuditLog.cs` | ❌ 未开始 | P1 |
| AuditLogController | `YZH.Core.Api/Controllers/AuditLogController.cs` | ❌ 未开始 | P2 |

### 5.2 前端模块（certplatform-web）

| 模块 | 文件路径 | 状态 | 优先级 |
|------|---------|------|--------|
| TreeUtils | `share/src/utils/treeUtils.ts` | ❌ 未开始 | P1 |
| YzhTree 组件 | `yzh.vue.core/src/components/layout/YzhTree.vue` | ❌ 未开始 | P1 |
| CrudPageLogic | `share/src/logic/CrudPageLogic.ts` | ❌ 未开始 | P2 |
| TreeTableLogic | `share/src/logic/TreeTableLogic.ts` | ❌ 未开始 | P2 |
| YzhTable 组件 | `yzh.vue.core/src/components/data/YzhTable.vue` | ❌ 未开始 | P2 |
| YzhForm 组件 | `yzh.vue.core/src/components/form/YzhForm.vue` | ❌ 未开始 | P2 |

---

## 六、风险与应对

| 风险 | 等级 | 应对措施 |
|------|------|---------|
| 编译错误未发现 | 高 | Day 1 必须本地编译验证 |
| 权限拦截器影响性能 | 中 | 缓存权限数据，异步校验 |
| 审计日志入库影响写入性能 | 中 | 异步写入，批量插入 |
| 前后端协议不匹配 | 高 | Day 6 前完成协议标准化 |
| 树形结构性能问题 | 中 | 懒加载 + 虚拟滚动 |

---

## 七、下一步行动

```
今天（Day 1）：
□ 本地编译验证，修复所有错误
□ 删除 BaseRepository.cs
□ 注册权限/验证过滤器
□ 测试基础接口是否正常

明天（Day 2）：
□ 实现 TreeControllerBase
□ 实现 JWT Token 刷新
□ 测试树形接口

后天（Day 3）：
□ 实现 TreeUtils
□ 实现 YzhTree 组件
□ 测试前端树组件

第 4-5 天：
□ 实现前端基类
□ 审计日志入库
□ 测试完整流程

第 6-7 天：
□ 编写单元测试
□ 标准化前后端协议
□ 完善 API 文档
```

---

## 八、参考资源

- **架构演进方案**：`docs/20-架构决策/YZH-Core架构演进与基础安全完善方案-V1.md`
- **树形结构设计**：`docs/00-工程体系/树形结构设计-V1.md`
- **架构对比分析**：`docs/20-架构决策/Ape.Volo与Vol及YZH-Core架构对比分析-V1.md`
- **前端架构设计**：`docs/00-工程体系/前端架构设计-V1.md`
- **迁移指南**：`docs/60-AI工程设计/YZH-知识库/10-架构迁移指南-V1.md`

---

*（内容由AI生成，仅供参考，请根据实际开发情况更新）*
