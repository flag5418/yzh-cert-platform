# 认证平台业务实体

此目录将承载从 vol.api 迁移过来的认证平台业务实体。

## 待迁移实体（30+）

- Enterprise（企业）
- CertificationBody（认证机构）
- AuditTask（审核任务）
- AuditPlan（审核计划）
- NCR（不符合项报告）
- CertificationApplication（认证申请）
- ...

## 迁移原则

1. 继承 YZHBaseEntity 替代直接继承 BaseEntity
2. 保持原有属性和关系不变
3. 原有 Service 层不迁移（继续使用 Vol 的 ApplicationServiceBase）

## 状态：[TODO:P2] 待 Phase 2 执行
