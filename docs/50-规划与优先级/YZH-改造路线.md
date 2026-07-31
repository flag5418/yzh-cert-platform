---
AIGC:
    Label: "1"
    ContentProducer: 001191440300708461136T1XGW3
    ProduceID: 9a16bac6e27d25132787d930f50d9879_83f533d78c9e11f19986525400287e28
    ReservedCode1: GNLfi/+56l1DsNUQrjKt+H/jGABNuYZ+7kf1OCT9u9VOZtLpHfZIsMr1IpTcj0TS3szNR61EE9X8Y+h61sAEda2KoYg+hMvKzImvnKn0ydsxAwNaC+Y1wFcv/+43rt6G3Jw677G9CqiDw6S8EyoqP7lKitM6SvjNgO2hIb6eM7Bq4vEOFOPM2ui7vs=
    ContentPropagator: 001191440300708461136T1XGW3
    PropagateID: 9a16bac6e27d25132787d930f50d9879_83f533d78c9e11f19986525400287e28
    ReservedCode2: GNLfi/+56l1DsNUQrjKt+H/jGABNuYZ+7kf1OCT9u9VOZtLpHfZIsMr1IpTcj0TS3szNR61EE9X8Y+h61sAEda2KoYg+hMvKzImvnKn0ydsxAwNaC+Y1wFcv/+43rt6G3Jw677G9CqiDw6S8EyoqP7lKitM6SvjNgO2hIb6eM7Bq4vEOFOPM2ui7vs=
---

# YZH-Framework 改造路线

**版本**：V1.1  
**日期**：2026-07-31  
**最后更新**：2026-07-31（V1.1：同步 Phase 1 完成状态，细化时间估算）  
**状态**：正式发布  
**策略**：先搭建改造基础设施 → 选一个业务模块验证 → 扩展完善

## 整体节奏

| 阶段 | 目标 | 时间预算 | 产出物 | 状态 |
|------|------|----------|--------|------|
| Phase 1 | 搭建骨架 | 4 人天 | 知识库、YZHBaseEntity（完整版）、模块注册、测试项目、工程化配置 | ✅ **已完成 80%** |
| Phase 2 | 业务验证 | 10 人天 | ValidationRules + CodeRule + Audited + ServiceBase，选认证申请模块验证 | 🔜 待启动 |
| Phase 3 | 扩展完善 | 持续 | DeleteStrategy、多租户、接口幂等性、持续回写知识库 | 📋 规划中 |

---

## Phase 1：基础改造

| ID | 任务 | 优先级 | 状态 | 工时 | 完成时间 | 说明 |
|---|---|---|---|---|---|---|
| T1.1 | 建立 YZH 知识库框架 | P0 | ✅ DONE | 0.5d | 2026-07-31 | 目录结构 + README + Vol 能力清单 |
| T1.2 | 填充 Vol 能力清单 | P0 | ✅ DONE | 1d | 2026-07-31 | 结构化能力索引（32 个钩子 + 20 条路由） |
| T1.3 | 定义 YZHBaseEntity | P0 | ✅ DONE | 1d | 2026-07-31 | 继承 Vol 空 BaseEntity，扩展 Code + 审计字段（12 字段）+ 辅助方法 + OrgCode/DeleteBy/DeleteTime |
| T1.4 | 注册 YZH 模块到 Vol 容器 | P0 | ✅ DONE | 0.5d | 2026-07-31 | Autofac 模块注册骨架，不破坏 Vol 现有行为 |
| T1.5 | 建立 YZH 测试项目 | P0 | ✅ DONE | 1d | 2026-07-31 | xUnit + 基础字段默认值测试 + 辅助方法测试 |

### Phase 1 额外完成项（原计划外）

| ID | 任务 | 优先级 | 状态 | 工时 | 说明 |
|---|---|---|---|---|---|
| T1.6 | 统一接口参数定义 | P0 | ✅ DONE | 0.5d | 对齐架构文档，修复 YZHAuditedAttribute / IDeleteStrategy / ICodeRule |
| T1.7 | 工程化配置 | P1 | ✅ DONE | 0.5d | .editorconfig + Directory.Build.props + 增强 README |
| T1.8 | 文档一致性修复 | P1 | ✅ DONE | 1d | 更新知识库、建设原则、代码模板对齐实现 |

**依赖关系**：T1.1 → T1.2 → T1.3 → T1.4 → T1.5 → T1.6 → T1.7 → T1.8

### Phase 1 Definition of Done（验收标准）

- [x] YZHBaseEntity 编译通过且包含所有审计字段（12 字段）
- [x] YZHModule 可成功加载到 Vol 容器（空实现但结构正确）
- [x] 测试项目建立且有基础用例通过
- [x] 知识库 5 个文件全部就绪
- [x] 文档拆分完成并通过 review
- [x] 接口参数与架构文档完全一致
- [x] 工程化配置文件就位

---

## Phase 2：核心能力

| ID | 任务 | 优先级 | 状态 | 工时 | 说明 | Plan B |
|---|---|---|---|---|---|---|
| T2.1 | 实现 YZHServiceBase | P1 | 🔜 TODO | 2d | 继承 Vol.ServiceBase，封装 Func 钩子为虚方法，集成 Attribute 读取 | 如果继承冲突，改为组合模式包装 |
| T2.2 | 实现 YZHValidationRules | P1 | 🔜 TODO | 2d | 声明式校验，在认证申请模块验证 | 如果过于复杂，先用 DataAnnotations + FluentValidation |
| T2.3 | 实现 YZHCodeRule | P1 | 🔜 TODO | 1.5d | 编码规则引擎，幂等+并发安全（Redis 分布式锁） | 先实现单机版（lock + 内存缓存） |
| T2.4 | 实现 YZHAudited | P1 | 🔜 TODO | 2d | 实体级审计标注特性，新旧值对比，敏感字段脱敏 | 先只记录操作日志，不做 diff |
| T2.5 | 废弃 YZHDecoratorMiddleware | P1 | 🔜 TODO | 0.5d | 改用 IAsyncActionFilter 对齐 Vol 风格 | - |
| T2.6 | 回写知识库 | P1 | 🔜 TODO | 1d | 验证中发现的坑记录到 05-踩坑记录 | - |

**验证模块**：认证申请（CertificationApplication）—— CRUD 完整、涉及校验和编码、有审计需求

**关键里程碑**：
- 🎯 M2.1（第 5 天）：YZHServiceBase 可用于 CertificationApplication 的 CRUD
- 🎯 M2.2（第 8 天）：校验规则在新增申请时生效
- 🎯 M2.3（第 10 天）：编码规则自动生成申请编号
- 🎯 M2.4（第 12 天）：审计日志记录到数据库

---

## Phase 3：扩展完善

| ID | 任务 | 优先级 | 状态 | 工时 | 说明 |
|---|---|---|---|---|---|
| T3.1 | 实现 YZHDeleteStrategy | P2 | 📋 PLAN | 2d | 软删除策略（Logical/Physical/Cascade），级联删除支持 |
| T3.2 | 多租户隔离方案 | P3 | 📋 PLAN | 3d | OrgCode 过滤 + 数据权限 + 租户管理员 |
| T3.3 | 实现接口幂等性（Redis 防重复提交） | P2 | 📋 PLAN | 1.5d | YZHIdempotentAttribute + ActionFilter + Redis SET NX EX |
| T3.4 | 实现 YZHControllerBase | P2 | 📋 PLAN | 2d | 统一响应格式转换，YZHOk()/YZHError() 扩展方法 |
| T3.5 | 实现 YZHGlobalExceptionFilter | P2 | 📋 PLAN | 1.5d | 异常层次体系，统一错误响应格式 |
| T3.6 | 知识库持续维护 | P3 | 📋 TODO | 持续 | 随业务模块推进补充踩坑记录和最佳实践 |

---

## 任务状态图例

| 标记 | 含义 | 颜色 |
|---|------|------|
| ✅ DONE | 已完成并通过验证 | 绿色 |
| 🔜 TODO | 下一阶段待执行 | 蓝色 |
| 📋 PLAN | 方向确定，细节待定 | 橙色 |
| ⚠️ BLOCKED | 被阻塞，等待前置条件 | 红色 |
| DEPRECATED | 已废弃，不再执行 | 灰色 |

---

## 时间线总览（甘特图风格）

```
Week 1 (Phase 1)          Week 2-3 (Phase 2)         Week 4+ (Phase 3)
━━━━━━━━━━━━━━━━━━━       ━━━━━━━━━━━━━━━━━━━━━       ━━━━━━━━━━━━━━━━━
T1.1 T1.2 T1.3 T1.4 T1.5   T2.1 T2.2 T2.3 T2.4        T3.1 T3.3 T3.4
 │    │    │    │    │      │    │    │    │           │    │    │
 └────┴────┴────┴────┘      └────┴────┴────┘           └────┴────┘
   T1.6 T1.7 T1.8              T2.5 T2.6                T3.2 T3.5 T3.6
    │    │    │                 │    │                  │    │    │
    └────┴────┘                 └────┘                  └────┴────┘
  ✅ 已完成                      🔜 进行中               📋 规划中
```

---

## 风险与应对

| 风险 | 概率 | 影响 | 应对方案 | 负责人 |
|------|------|------|---------|--------|
| Vol 版本升级导致编译错误 | 中 | 高 | 锁定 EF Core 8.0 + Vol 当前版本；YZH 代码物理隔离 | 架构师 |
| Func 钩子 vs 虚方法 API 差异 | 中 | 中 | YZHServiceBase 提供双层 API | 开发者 |
| 响应格式不统一（WebResponseContent vs IActionResult） | 低 | 低 | YZHControllerBase 统一转换层 | 开发者 |
| 多租户性能影响 | 低 | 高 | 先实现简单版 OrgCode WHERE 过滤，后续优化为查询重写 | 架构师 |
| 编码规则并发冲突 | 中 | 中 | Redis 分布式锁兜底；Phase 2 先用单机 lock | 开发者 |

---

*（内容由 AI 生成，仅供参考。最后更新：2026-07-31 by AI Assistant）*
