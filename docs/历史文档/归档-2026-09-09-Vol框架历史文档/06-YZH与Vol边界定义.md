# YZH Framework 与 Vol 框架边界定义

> **版本**: v1.0
> **日期**: 2026-08-08
> **状态**: 已确认（基于 CertificationBody + ISOStandard 实际验证）

---

## 一、架构定位

### 1.1 核心原则

**YZH Framework 是独立的前后端开发框架**，Vol 框架仅提供"基础设施"层。

```
┌─────────────────────────────────────────────────────────────┐
│                     业务应用层（认证平台）                    │
│                                                              │
│   ┌──────────┐  ┌──────────┐  ┌──────────┐              │
│   │CertBody  │  │ISOStd    │  │CertStage │  ...         │
│   │(已完成)  │  │(已完成)  │  │(开发中)  │              │
│   └────┬─────┘  └────┬─────┘  └────┬─────┘              │
│        │             │             │                      │
│   ┌────▼─────────────▼─────────────▼──────────────┐       │
│   │            YZH Framework（自研）               │       │
│   │                                            │       │
│   │  ┌────────────┐  ┌────────────┐  ┌──────────┐  │       │
│   │  │YzhCrudTable│  │YzhTreeTable│  │YzhTree   │  │       │
│   │  │(单表CRUD)  │  │(左树右表)  │  │Checkbox  │  │       │
│   │  └────────────┘  └────────────┘  │Table     │  │       │
│   │                                  └──────────┘  │       │
│   │  ┌────────────┐  ┌────────────┐                │       │
│   │  │YZHBaseApi  │  │YZHConfig  │                │       │
│   │  │Client      │  │Loader     │                │       │
│   │  └────────────┘  └────────────┘                │       │
│   └────────────────────────────────────────────────┘       │
│                        │                                │
│   ┌─────────────────▼─────────────────────────────────┐  │
│   │              Vol 框架（仅基础设施）           │  │
│   │                                            │  │
│   │  ✅ JWT 鉴权    ✅ 登录认证    ✅ 菜单权限      │  │
│   │  ✅ UserContext  ✅ 全局异常处理  ✅ 日志框架    │  │
│   │  ❌ CRUD 操作    ❌ Service逻辑  ❌ 组件渲染     │  │
│   └────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### 1.2 为什么不完全脱离 Vol？

| 理由 | 说明 |
|------|------|
| **登录体系完整** | Vol 的 JWT + Sys_User + 角色权限已成熟，无需重造 |
| **菜单权限已就绪** | 前端直接用 Vol 的菜单数据渲染，不同角色看到不同菜单 |
| **后端基础能力** | 拦截器、过滤器、日志、UserContext 开箱即用 |
| **渐进式迁移** | 新页面用 YZH，旧页面可继续用 Vol ViewGrid |

---

## 二、前端边界定义

### 2.1 ✅ 使用 Vol 的部分（无需修改）

| 功能 | Vol 提供 | YZH 使用方式 | 状态 |
|------|----------|-------------|------|
| **登录页面** | `/login` 路由 | 直接跳转 | ✅ 可用 |
| **Token 管理** | `http.js` 的 getToken/setToken | 直接调用 | ✅ 可用 |
| **菜单数据** | `store/index` 的 menuData | 直接读取 | ✅ 可用 |
| **用户信息** | `store/index` 的 userInfo | 直接读取 | ✅ 可用 |
| **字典接口** | `/api/Sys_Dictionary/GetVueDictionary` | YZHConfigLoader 调用 | ⚠️ 需封装 |
| **HTTP 封装** | `http.js` post/get 方法 | YZHBaseApiClient 调用 | ⚠️ 注意 404 弹窗问题 |

### 2.2 ❌ 不使用 Vol 的部分（YZH 自研）

| 功能 | YZH 替代方案 | 原因 |
|------|---------------|------|
| **CRUD 表格** | `YzhCrudTable.vue` | Vol ViewGrid 过度耦合，不灵活 |
| **左树右表** | `YzhTreeTable.vue` | Vol 无此组件模式 |
| **关联选择** | `YzhTreeCheckboxTable.vue` | 业务特有需求 |
| **编辑弹窗** | YzhCrudTable 内置 el-dialog | 自定义宽高/滚动 |
| **搜索栏** | YzhCrudTable 内置 el-form | 动态字段配置 |
| **分页器** | YzhCrudTable 内置 el-pagination | 样式统一 |

### 2.3 ⚠️ 需要注意的 Vol 行为

| Vol 行为 | 影响 | 应对策略 |
|----------|------|----------|
| **http.js 404 全局弹窗** | yzh-page-config 404 会触发 "未找到请求地址" 提示 | 方案A: 数据库插入配置记录；方案B: 修改 http.js（不推荐） |
| **http.js 401 自动跳转登录** | token 过期时自动跳转 | 正常行为，无需处理 |
| **Vol 返回格式** | `{ status, message, data }` 或 `{ rows, total }` | YZHBaseApiClient 已适配 |
| **Content-Type** | 后端返回 JSON | axios 默认处理 |

---

## 三、后端边界定义

### 3.1 ✅ 使用 Vol 的部分（基础设施）

| 功能 | Vol 提供 | YZH 使用方式 | 状态 |
|------|----------|-------------|------|
| **JWT 鉴权** | `[JWTAuthorize]` 特性 | Controller 加特性即可 | ✅ 可用 |
| **用户上下文** | `UserContext.Current` | Service 中获取当前用户 | ✅ 可用 |
| **全局异常** | `GlobalExceptionFilter` | 自动捕获未处理异常 | ✅ 可用 |
| **请求日志** | `RequestLog` | 自动记录请求日志 | ✅ 可用 |
| **响应包装** | `WebResponseContent` | Controller 返回此类型 | ✅ 可用 |
| **DB Context** | `VOLContext` | EF Core 数据库连接 | ✅ 可用 |
| **依赖注入** | Autofac | Service 通过构造函数注入 | ✅ 可用 |

### 3.2 ❌ YZH 自研的部分（业务逻辑）

| 功能 | YZH 实现 | 所在位置 |
|------|---------|---------|
| **实体基类** | `YZHBaseEntity` | `VOL.Entity.CertPlatform.YZHBaseEntity.cs` |
| **Service 基类** | `YZHServiceBase`（规划中） | `YZH-Framework/` |
| **Controller 基类** | 继承 Vol 的 `ApiBaseController` | 复用其路由/JWT 能力 |
| **分页查询** | `ServiceBase.GetPageData()` | Vol 内部方法，需理解参数格式 |
| **删除操作** | 需自定义 Remove/Del 接口 | Vol 默认 Del 可能不满足需求 |
| **业务校验** | 特性驱动 `[YZHValidationRules]` | 规划中 |

### 3.3 ⚠️ Vol 内部机制（必须了解的）

| Vol 机制 | 工作原理 | YZH 如何使用 |
|----------|----------|------------|
| **GetPageData 参数** | 接收 `PageDataOptions`（page, rows, sort, order, filter） | 前端按此格式传参 |
| **Del 参数** | 接收 `object[]` 类型的主键数组 | 前端传 `{ ids: [1,2,3] }` |
| **Add 参数** | 接收 `SaveModel`（TableName + MainData + DetailData） | 前端按此格式组装 |
| **权限控制** | `[PermissionTable]` + `[ApiActionPermission]` | Controller 加特性 |
| **字段校验** | `[Editable(true)]` 标记可编辑列 | Entity 属性加特性 |

---

## 四、已验证可用的 Vol 接口清单

### 4.1 认证机构管理 (CertificationBody) — ✅ 完全正常

| 操作 | API 路径 | Vol 方法 | 状态 |
|------|---------|----------|------|
| 分页查询 | `POST /api/CertCertificationBody/GetPageData` | `ServiceBase.GetPageData()` | ✅ |
| 新增 | `POST /api/CertCertificationBody/Add` | `ServiceBase.Add()` | ✅ |
| 编辑 | `POST /api/CertCertificationBody/Update` | `ServiceBase.Update()` | ✅ |
| 删除 | `POST /api/CertCertificationBody/Del` | `ServiceBase.Del()` | ✅ |
| 导出 | `POST /api/CertCertificationBody/Export` | `ServiceBase.ExportBytes()` | ✅ |

**关键发现**：
- CertificationBody 用的是 **`Del`** 接口（不是 `Remove`）
- 主键类型是 **`string`**（Code 字段），不是 `long`
- 前端 schema.keyField = `'Code'`

### 4.2 ISO 标准 (ISOStandard) — ✅ 完全正常

| 操作 | API 路径 | Vol 方法 | 状态 |
|------|---------|----------|------|
| 分页查询 | `POST /api/ISOStandard/GetPageData` | `ServiceBase.GetPageData()` | ✅ |
| 新增 | `POST /api/ISOStandard/Add` | `ServiceBase.Add()` | ✅ |
| 编辑 | `POST /api/ISOStandard/Update` | `ServiceBase.Update()` | ✅ |
| 删除 | `POST /api/ISOStandard/Del` | `ServiceBase.Del()` | ✅ |

**关键发现**：
- ISOStandard 同样用 **`Del`** 接口
- 主键类型是 **`long`**（Id 字段）
- 使用 `YzhTreeTable` 左树右表模式

---

## 五、标准页面开发流程（基于 CertificationBody 经验）

### 5.1 新建标准页面的步骤

```
Step 1: 创建数据库表
   ↓
Step 2: 创建 C# 实体（继承 YZHBaseEntity）
   ↓
Step 3: 创建 Service（继承 Vol 的 ServiceBase）
   ↓
Step 4: 创建 Controller（主类 + Partial 类）
   ↓
Step 5: 创建前端 options.js（列定义 + 编辑表单 + 搜索条件）
   ↓
Step 6: 创建前端 Vue 页面（选择组件模式）
   ↓
Step 7: 配置路由和菜单
   ↓
Step 8: 测试验证
```

### 5.2 组件模式选择指南

| 场景 | 推荐组件 | 示例 |
|------|----------|------|
| **单表 CRUD（无关联）** | `YzhCrudTable` | CertificationBody, CertStage |
| **主从浏览（左树右表）** | `YzhTreeTable` | ISOStandard |
| **多选关联（勾选保存）** | `YzhTreeCheckboxTable` | OrgStandard, OrgStage |
| **复杂表单** | `YzhFormGrid`（规划中） | - |

### 5.3 Schema 定义规范

```typescript
// 必须定义的字段
const schema = Object.freeze({
  keyField: 'Id',           // 主键字段名（对应数据库主键）
  keyType: 'number',        // 主键类型: 'number' | 'string' | 'guid'
  controllerName: 'Xxx',   // 后端 Controller 路由名（不含 Controller 后缀）
  tableName: 'xxx_table',  // 数据库表名
  defaultSortField: 'CreateDate', // 默认排序字段
  defaultSortOrder: 'desc',     // 默认排序方向
  statusTagColors: { Status: 'dict_key' }, // 状态列字典 Tag 色
})
```

### 5.4 删除功能注意事项

**重要发现**：Vol 的删除接口是 **`Del`**（不是 `Remove`）！

```csharp
// 前端调用
POST /api/{controller}/Del
Body: { ids: [1, 2, 3] }  // object[] 类型

// 后端 Controller（如果需要自定义）
[HttpPost("Del")]
public object Del([FromBody] object[] ids)
{
    return Service.Del(ids);
}
```

---

## 六、待解决问题（需要进一步讨论）

### 6.1 架构层面

- [ ] YZHServiceBase 是否需要完全自研？
- [ ] 字典加载是否应该完全走 yzh-page-config 配置驱动？
- [ ] YzhTreeCheckboxTable 是否应该重构为更简单的模式？

### 6.2 具体问题

- [ ] CertStage 删除功能需改用 Del 接口
- [ ] OrgStandard/OrgStage 数据加载时序问题
- [ ] 字典翻译方案需要确定：配置驱动 vs 循环填充？

### 6.3 文档完善

- [ ] 基于 CertificationBody 和 ISOStandard 补充踩坑记录
- [ ] 整理每个组件的使用示例和限制
- [ ] 记录 Vol 内部机制的实测行为

---

## 七、决策记录

| # | 决策 | 日期 | 原因 |
|---|------|------|------|
| ADR-001 | YZH 不脱离 Vol，但明确边界 | 2026-08-08 | 降低风险，复用成熟设施 |
| ADR-002 | 前端 CRUD 完全自研 | 2026-08-08 | Vol ViewGrid 不灵活 |
| ADR-003 | 后端 Service 继承 Vol ServiceBase | 2026-08-08 | 获取分页/增删改查能力 |
| ADR-004 | 删除接口使用 Del 而非 Remove | 2026-08-08 | Vol 标准命名 |
