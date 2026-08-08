# YZH-Framework V2.0 TODO 清单

**版本**: V2.0  
**日期**: 2026-08-08  
**状态**: 待实施

---

## 优先级说明

| 优先级 | 说明 | 时间预算 |
|--------|------|----------|
| **P0** | 核心功能，必须实现 | Week 1-2 |
| **P1** | 重要功能，建议实现 | Week 3 |
| **P2** | 增强功能，可选实现 | Week 4 |

---

## Phase 2.1：核心拦截器实现（P0）

### T2.1.1 实现 YZHAuditedInterceptor（审计拦截器）
- [ ] 创建文件：`YZH.Core/Interceptors/YZHAuditedInterceptor.cs`
- [ ] 实现自动填充 CreateID/Creator/CreateDate
- [ ] 实现自动填充 ModifyID/Modifier/ModifyDate
- [ ] 实现自动填充 DeleteID/Deleter/DeleteTime
- [ ] 编写单元测试：`YZH.Core.Tests/Interceptors/YZHAuditedInterceptorTests.cs`
- [ ] 测试用例：
  - [ ] 测试新增时自动填充创建信息
  - [ ] 测试更新时自动填充修改信息
  - [ ] 测试删除时自动填充删除信息
  - [ ] 测试未登录时不填充审计字段

**预计工期**: 2 天

---

### T2.1.2 实现 YZHCodeRuleInterceptor（编码规则拦截器）
- [ ] 创建文件：`YZH.Core/Interceptors/YZHCodeRuleInterceptor.cs`
- [ ] 实现读取 [YZHCodeRule] 特性
- [ ] 实现调用 ICodeRuleService 生成编码
- [ ] 编写单元测试：`YZH.Core.Tests/Interceptors/YZHCodeRuleInterceptorTests.cs`
- [ ] 测试用例：
  - [ ] 测试自动生成编码（CB2026080001）
  - [ ] 测试已有 Code 时不重新生成
  - [ ] 测试无 [YZHCodeRule] 特性时不生成编码

**预计工期**: 2 天

---

### T2.1.3 实现 YZHPermissionInterceptor（权限拦截器）
- [ ] 创建文件：`YZH.Core/Interceptors/YZHPermissionInterceptor.cs`
- [ ] 实现读取 [YZHPermission] 特性
- [ ] 实现校验用户权限
- [ ] 实现权限不足时抛出异常
- [ ] 编写单元测试：`YZH.Core.Tests/Interceptors/YZHPermissionInterceptorTests.cs`
- [ ] 测试用例：
  - [ ] 测试有权限时正常执行
  - [ ] 测试无权限时抛出异常
  - [ ] 测试未登录时抛出异常

**预计工期**: 2 天

---

### T2.1.4 注册拦截器到 Autofac 容器
- [ ] 修改文件：`YZH.Core/YZHModule.cs`
- [ ] 注册 YZHAuditedInterceptor
- [ ] 注册 YZHCodeRuleInterceptor
- [ ] 注册 YZHPermissionInterceptor
- [ ] 为 YZHServiceBase 添加拦截器
- [ ] 编写集成测试：`YZH.Core.Tests/Integration/InterceptorIntegrationTests.cs`
- [ ] 测试用例：
  - [ ] 测试拦截器链正确执行
  - [ ] 测试拦截器异常处理

**预计工期**: 1 天

---

### T2.1.5 完善拦截器文档
- [ ] 更新 README.md
- [ ] 添加拦截器使用说明
- [ ] 添加代码示例
- [ ] 位置：`YZH-Framework/README.md`

**预计工期**: 0.5 天

---

## Phase 2.2：视图支持（P0）

### T2.2.1 创建 BaseView 基类
- [ ] 创建文件：`YZH.Core/Entities/BaseView.cs`
- [ ] 定义通用字段（Id, Code, CreateDate, Creator, Status）
- [ ] 定义前端常用字段（CheckFlag, DeleteFlag）
- [ ] 添加 XML 注释

**预计工期**: 0.5 天

---

### T2.2.2 实现实体到视图的映射扩展方法
- [ ] 创建文件：`YZH.Core/Extensions/BaseEntityExtensions.cs`
- [ ] 实现 ToView<TView>() 单个实体映射
- [ ] 实现 ToViewList<TView>() 批量映射
- [ ] 实现 FromView<TEntity>() 视图转实体
- [ ] 编写单元测试：`YZH.Core.Tests/Extensions/BaseEntityExtensionsTests.cs`
- [ ] 测试用例：
  - [ ] 测试单个实体映射
  - [ ] 测试批量映射
  - [ ] 测试视图转实体

**预计工期**: 1 天

---

### T2.2.3 创建视图示例（CertificationBodyView）
- [ ] 创建文件：`YZH.CertPlatform/Entities/Cert/CertificationBodyView.cs`
- [ ] 定义视图字段
- [ ] 编写映射测试
- [ ] 位置：`YZH.CertPlatform/Entities/Cert/`

**预计工期**: 0.5 天

---

### T2.2.4 更新前端组件支持视图
- [ ] 修改文件：`vol.web/src/yzh/components/YzhCrudTable.vue`
- [ ] 增加 ViewMode 配置项
- [ ] 支持视图类型渲染
- [ ] 测试视图模式下的表格渲染

**预计工期**: 2 天

---

### T2.2.5 编写视图使用示例
- [ ] 创建示例项目：`YZH.Examples/ViewExample/`
- [ ] 演示视图与实体的使用
- [ ] 更新 README.md

**预计工期**: 1 天

---

## Phase 2.3：配置管理页面（P1）

### T2.3.1 创建配置管理控制器
- [ ] 创建文件：`VOL.WebApi/Controllers/CertPlatform/YzhPageConfigController.cs`
- [ ] 实现 GetPageConfig 接口
- [ ] 实现 GetAllPageConfigs 接口
- [ ] 实现 ImportConfig 接口（XML/JSON）
- [ ] 实现 ExportConfig 接口
- [ ] 位置：`VOL.WebApi/Controllers/CertPlatform/`

**预计工期**: 2 天

---

### T2.3.2 创建配置管理前端页面
- [ ] 创建页面：`vol.web/src/views/yzh/config/PageConfig.vue`
- [ ] 实现页面配置列表
- [ ] 实现页面配置编辑
- [ ] 创建页面：`vol.web/src/views/yzh/config/FieldConfig.vue`
- [ ] 实现字段配置列表
- [ ] 实现字段配置编辑
- [ ] 配置路由：`vol.web/src/router/viewGird.js`

**预计工期**: 3 天

---

### T2.3.3 实现 XML 导入导出功能
- [ ] 修改文件：`YZH.CertPlatform/Services/YzhPageConfigService.cs`
- [ ] 实现 ExportToXml 方法
- [ ] 实现 ImportFromXml 方法
- [ ] 实现 ExportToJson 方法
- [ ] 实现 ImportFromJson 方法
- [ ] 编写单元测试

**预计工期**: 2 天

---

### T2.3.4 编写配置管理测试
- [ ] 创建测试文件：`YZH.Core.Tests/Services/YzhPageConfigServiceTests.cs`
- [ ] 测试配置加载
- [ ] 测试 XML 导入导出
- [ ] 测试 JSON 导入导出
- [ ] 测试缓存机制

**预计工期**: 1 天

---

## Phase 2.4：扩展方法完善（P1）

### T2.4.1 实现 HttpRequest 扩展方法
- [ ] 创建文件：`YZH.Api.Core/Extensions/HttpRequestExtensions.cs`
- [ ] 实现 GetRequestEntity<T>()
- [ ] 实现 GetRequestEntities<T>()
- [ ] 实现 Insert<T>()
- [ ] 实现 Update<T>()
- [ ] 实现 Delete<T>()
- [ ] 编写单元测试

**预计工期**: 1 天

---

### T2.4.2 实现数据库操作扩展方法
- [ ] 创建文件：`YZH.Core/Extensions/DbHelperExtensions.cs`
- [ ] 实现 Insert<T>()
- [ ] 实现 Update<T>()
- [ ] 实现 Delete<T>()
- [ ] 实现 Query<T>()
- [ ] 编写单元测试

**预计工期**: 1 天

---

### T2.4.3 实现分页查询扩展方法
- [ ] 创建文件：`YZH.Core/Extensions/PaginationExtensions.cs`
- [ ] 实现 PageQuery<T>()
- [ ] 实现 PageQueryAsync<T>()
- [ ] 编写单元测试

**预计工期**: 1 天

---

## Phase 2.5：特性体系完善（P2）

### T2.5.1 完善 [YZHAudited] 特性
- [ ] 修改文件：`YZH.Core/Attributes/YZHAuditedAttribute.cs`
- [ ] 添加 TrackChanges 参数
- [ ] 添加 Category 枚举
- [ ] 添加 Scope 枚举
- [ ] 添加 SensitiveFields 参数
- [ ] 添加 ExcludeFields 参数

**预计工期**: 0.5 天

---

### T2.5.2 完善 [YZHCodeRule] 特性
- [ ] 修改文件：`YZH.Core/Attributes/YZHCodeRuleAttribute.cs`
- [ ] 添加 Prefix 参数
- [ ] 添加 DateFormat 参数
- [ ] 添加 SerialLength 参数
- [ ] 添加 ResetRule 枚举
- [ ] 添加 Separator 参数

**预计工期**: 0.5 天

---

### T2.5.3 新增 [YZHPermission] 特性
- [ ] 创建文件：`YZH.Core/Attributes/YZHPermissionAttribute.cs`
- [ ] 添加 PermissionCode 参数
- [ ] 添加 ErrorMessage 参数
- [ ] 编写使用示例

**预计工期**: 0.5 天

---

### T2.5.4 新增 [YZHValidation] 特性
- [ ] 创建文件：`YZH.Core/Attributes/YZHValidationAttribute.cs`
- [ ] 添加验证规则基类
- [ ] 添加 Required 验证
- [ ] 添加 Unique 验证
- [ ] 添加 Length 验证
- [ ] 添加 Regex 验证
- [ ] 编写单元测试

**预计工期**: 2 天

---

## Phase 2.6：文档与示例（P2）

### T2.6.1 更新 README.md
- [ ] 修改文件：`YZH-Framework/README.md`
- [ ] 添加拦截器使用说明
- [ ] 添加视图使用示例
- [ ] 添加配置管理说明
- [ ] 添加扩展方法说明

**预计工期**: 1 天

---

### T2.6.2 创建使用示例项目
- [ ] 创建项目：`YZH.Examples/CertPlatformExample/`
- [ ] 示例：认证机构管理（实体 + 视图）
- [ ] 示例：ISO 标准管理（实体 + 视图）
- [ ] 示例：配置管理页面使用

**预计工期**: 2 天

---

### T2.6.3 编写开发指南
- [ ] 创建文档：`docs/60-AI工程设计/YZH-知识库/06-开发指南-V2.md`
- [ ] 如何创建新实体
- [ ] 如何创建新视图
- [ ] 如何使用拦截器
- [ ] 如何配置管理页面
- [ ] 如何使用扩展方法

**预计工期**: 2 天

---

## Phase 2.7：测试与质量保障（P1）

### T2.7.1 完善单元测试
- [ ] 测试拦截器功能
- [ ] 测试视图映射
- [ ] 测试扩展方法
- [ ] 测试配置管理
- [ ] 目标覆盖率：> 80%

**预计工期**: 3 天

---

### T2.7.2 集成测试
- [ ] 测试拦截器链
- [ ] 测试视图与实体协同
- [ ] 测试配置动态加载
- [ ] 测试前端组件渲染

**预计工期**: 2 天

---

### T2.7.3 性能测试
- [ ] 测试大量数据下的视图渲染性能
- [ ] 测试配置加载性能
- [ ] 测试拦截器性能开销
- [ ] 优化瓶颈点

**预计工期**: 2 天

---

## 总计统计

| Phase | 任务数 | 预计工期（天） |
|-------|--------|---------------|
| Phase 2.1：核心拦截器实现 | 5 | 7.5 |
| Phase 2.2：视图支持 | 5 | 5 |
| Phase 2.3：配置管理页面 | 4 | 8 |
| Phase 2.4：扩展方法完善 | 3 | 3 |
| Phase 2.5：特性体系完善 | 4 | 4.5 |
| Phase 2.6：文档与示例 | 3 | 5 |
| Phase 2.7：测试与质量保障 | 3 | 7 |
| **总计** | **27** | **40** |

---

## 实施建议

### 建议 1：分阶段交付
- Week 1：完成 Phase 2.1（拦截器）
- Week 2：完成 Phase 2.2（视图）+ Phase 2.4（扩展方法）
- Week 3：完成 Phase 2.3（配置管理）
- Week 4：完成 Phase 2.5-2.7（完善与测试）

### 建议 2：优先实现核心功能
1. 先完成拦截器（T2.1.1-T2.1.4）
2. 再完成视图支持（T2.2.1-T2.2.4）
3. 最后完成配置管理（T2.3.1-T2.3.4）

### 建议 3：边开发边测试
- 每个任务完成后立即编写测试
- 确保测试覆盖率 > 80%
- 及时修复发现的问题

---

**文档版本**: V2.0  
**最后更新**: 2026-08-08  
**下次更新**: 每个 Phase 完成后更新进度
