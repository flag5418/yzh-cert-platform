# Phase 1：逐表列名变更清单

> 创建时间：2026-09-13
> 说明：以下列出所有需要从 snake_case/mixed 重命名为 PascalCase 的 DB 列名

---

## 1. cert_iso_standard（ISO 标准）

需重命名 8 列：

| 原列名 | 新列名 | 类型 | 说明 |
|--------|--------|------|------|
| `cb_code` | `CbCode` | varchar(50) | 机构编号 |
| `standard_code` | `StandardCode` | varchar(50) | 标准编码 |
| `standard_name` | `StandardName` | varchar(200) | 标准名称 |
| `version_year` | `VersionYear` | int | 版本年份 |
| `category` | `Category` | varchar(50) | 分类 |
| `description` | `Description` | text | 说明 |
| `parent_code` | `ParentCode` | varchar(100) | 父级编码 |
| `is_leaf` | `IsLeaf` | tinyint(1) | 是否叶子节点 |
| `status` | `Status` | varchar(50) | 状态 |

无需重命名（已是 PascalCase）：`Id`, `Code`, `OrgCode`, `CreateBy`, `CreateTime`, `UpdateBy`, `UpdateTime`, `DeleteBy`, `DeleteTime`, `IsDeleted`, `IsValid`, `Sort`, `Remark`

**共 9 列需改。**

---

## 2. cert_phase_definition（阶段定义）

需重命名 6 列：

| 原列名 | 新列名 | 类型 | 说明 |
|--------|--------|------|------|
| `id` | `Id` | bigint | 主键 |
| `code` | `Code` | varchar(36) | 业务编码 |
| `phase_code` | `PhaseCode` | varchar(20) | 阶段编码 |
| `phase_name` | `PhaseName` | varchar(100) | 阶段名称 |
| `sequence_order` | `SequenceOrder` | int | 顺序 |
| `description` | `Description` | text | 说明 |

无需重命名（已是 PascalCase）：`IsValid`, `CreateTime`, `CreateBy`, `UpdateTime`, `UpdateBy`, `DeleteTime`, `DeleteBy`, `IsDeleted`

**共 6 列需改。**

---

## 3. cert_certification_body（认证机构）

需重命名 17 列：

| 原列名 | 新列名 | 类型 | 说明 |
|--------|--------|------|------|
| `name` | `Name` | varchar(200) | 机构名称 |
| `short_name` | `ShortName` | varchar(100) | 简称 |
| `cb_code` | `CbCode` | varchar(50) | CNAS 编号 |
| `legal_person` | `LegalPerson` | varchar(100) | 法人 |
| `contact_name` | `ContactName` | varchar(50) | 联系人 |
| `contact_phone` | `ContactPhone` | varchar(20) | 联系电话 |
| `contact_email` | `ContactEmail` | varchar(200) | 邮箱 |
| `address` | `Address` | varchar(500) | 地址 |
| `logo_url` | `LogoUrl` | varchar(500) | Logo |
| `scope_text` | `ScopeText` | text | 业务范围 |
| `theme_config` | `ThemeConfig` | text | 主题配置 |
| `login_config` | `LoginConfig` | text | 登录配置 |
| `max_users` | `MaxUsers` | int | 最大用户数 |
| `max_enterprises` | `MaxEnterprises` | int | 最大企业数 |
| `expire_date` | `ExpireDate` | datetime | 到期日期 |
| `status` | `Status` | varchar(50) | 状态 |

无需重命名（已是 PascalCase）：`Id`, `Code`, `OrgCode`, `CreateBy`, `CreateTime`, `UpdateBy`, `UpdateTime`, `DeleteBy`, `DeleteTime`, `IsDeleted`, `IsValid`, `Sort`, `Remark`

**共 16 列需改。**

---

## 4. audit_task（审核任务）

需重命名 12 列：

| 原列名 | 新列名 | 类型 | 说明 |
|--------|--------|------|------|
| `creator` | `Creator` | varchar(50) | 创建人 |
| `create_date` | `CreateDate` | datetime | 创建时间 |
| `modifier` | `Modifier` | varchar(50) | 修改人 |
| `modify_date` | `ModifyDate` | datetime | 修改时间 |
| `deleter` | `Deleter` | varchar(50) | 删除人 |
| `delete_time` | `DeleteTime` | datetime | 删除时间 |
| `status` | `Status` | varchar(50) | 状态 |
| `enable` | `Enable` | tinyint | 启用 |
| `create_by` | `CreateBy` | varchar(50) | 创建人 |
| `update_by` | `UpdateBy` | varchar(50) | 更新人 |
| `delete_by` | `DeleteBy` | varchar(50) | 删除人 |

无需重命名（已是 PascalCase）：`Id`, `Code`, `OrgCode`, `Sort`, `Remark`, `PhaseCode`, `TaskNumber`, `AuditorCode`, `PlannedDate`, `ActualStartDate`, `ActualCompleteDate`, `AuditScope`, `IsDeleted`

**共 11 列需改。**

---

## 5. cert_enterprise（企业）

需重命名 3 列：

| 原列名 | 新列名 | 类型 | 说明 |
|--------|--------|------|------|
| `province` | `Province` | varchar(50) | 省份 |
| `city` | `City` | varchar(50) | 城市 |
| `status` | `Status` | tinyint | 状态 |

无需重命名（已是 PascalCase）：`Id`, `Code`, `Name`, `ShortName`, `CreditCode`, `LegalPerson`, `ContactName`, `ContactPhone`, `ContactEmail`, `Address`, `IndustryType`, `EmployeeCount`, `OrgCode`, `Remark`, `CreateBy`, `UpdateBy`, `CreateTime`, `UpdateTime`, `DeleteBy`, `DeleteTime`, `IsDeleted`

**共 3 列需改。**

---

## 6. cert_auditor_profile（审核员档案）

需重命名 8 列：

| 原列名 | 新列名 | 类型 | 说明 |
|--------|--------|------|------|
| `id` | `Id` | bigint | 主键 |
| `code` | `Code` | varchar(36) | 业务编码 |
| `user_code` | `UserCode` | varchar(50) | 用户编码 |
| `auditor_no` | `AuditorNo` | varchar(50) | 审核员编号 |
| `auditor_name` | `AuditorName` | varchar(100) | 审核员姓名 |
| `phone` | `Phone` | varchar(20) | 电话 |
| `email` | `Email` | varchar(200) | 邮箱 |
| `qualification` | `Qualification` | json | 资质 |
| `expertise_areas` | `ExpertiseAreas` | json | 专业领域 |
| `status` | `Status` | varchar(20) | 状态 |

无需重命名（已是 PascalCase）：`OrgCode`, `CreateBy`, `CreateTime`, `UpdateBy`, `UpdateTime`, `DeleteBy`, `DeleteTime`, `IsDeleted`, `IsValid`, `Remark`

**共 10 列需改。**

---

## 汇总

| 表名 | 需改列数 | 已是 PascalCase 列数 |
|------|---------|---------------------|
| cert_iso_standard | 9 | 13 |
| cert_phase_definition | 6 | 8 |
| cert_certification_body | 16 | 13 |
| audit_task | 11 | 13 |
| cert_enterprise | 3 | 19 |
| cert_auditor_profile | 10 | 10 |
| **合计** | **55** | **76** |
