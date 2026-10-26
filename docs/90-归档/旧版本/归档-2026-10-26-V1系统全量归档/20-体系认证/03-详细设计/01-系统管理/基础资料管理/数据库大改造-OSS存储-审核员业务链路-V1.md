# 数据库大改造 + OSS 存储重设计 + 审核员业务链路 V1

> **版本**：V1.0 | **日期**：2026-08-14 | **状态**：待审核  
> **定位**：项目前期大刀阔斧改造的完整方案，涵盖 OSS 存储、审核员业务链路、现有表规范化  
> **前置条件**：清空当前 OSS 和标准目录相关所有表，重新开始测试

---

## 一、OSS 存储方案最终定稿

### 1.1 中文路径问题结论

**现状**：MinIO 中已大量使用中文路径（如 `CS河北雄安尚龙医疗科技有限公司13485体系材料`），运行正常，无编码问题。

**决策**：**维持中文路径**，不做 Base64/Hash 编码。

**理由**：
1. MinIO（基于 Go 语言）原生支持 UTF-8 路径，中文路径在实际运行中无兼容问题
2. 如果对文件夹和文件名做 Base64/Hash 编码，管理后台浏览文件将完全不可读，运维极其困难
3. 标准目录的文件夹名本身就是中文业务术语（如 `1质量手册`、`2程序文件`），编码后失去语义
4. 顶层目录和结构化层级（`standard-directory`、`enterprise-documents`、`CB001`、`ISO134852016`、`STAGE01`）全部使用英文/编码，只有**实际文件名和文件夹名**保持中文原样

### 1.2 OSS 存储最终结构

```
cert-platform/                                          ← Bucket
│
├── standard-directory/                                 ← ① 标准目录（模板/参考文件）
│   └── {OrgCode}/                                      ← 认证机构编码（如 CB001）
│       └── {StandardCode}/                             ← 标准编码（如 ISO134852016）
│           └── {PhaseCode}/                            ← 阶段编码（如 STAGE01）
│               └── {FolderPath}/                       ← 文件夹路径（中文，如 1质量手册）
│                   └── {FileName}                      ← 文件名（中文原样）
│
└── enterprise-documents/                               ← ② 企业资料（企业上传文件）
    └── {EnterpriseCode}/                               ← 企业编码（如 ENT-2026-0001）
        └── {OrgCode}/                                  ← 认证机构编码
            └── {StandardCode}/                         ← 标准编码
                └── {PhaseCode}/                         ← 阶段编码
                    └── {FolderPath}/                   ← 文件夹路径（中文）
                        ├── {FileName}                   ← 原始文件（中文原样）
                        └── .converted/{FileName}       ← 转换后文件
```

**具体示例**：
```
标准目录：
/standard-directory/CB001/ISO134852016/STAGE01/1质量手册/XASL-QM 质量手册模板.docx

企业资料：
/enterprise-documents/ENT-2026-0001/CB001/ISO134852016/STAGE01/1质量手册/XASL-QM 质量手册.docx
```

### 1.3 路径一致性

```
标准目录：  /standard-directory/  ──┐
                                    ├── {OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
企业资料：  /enterprise-documents/  ┘  + {EnterpriseCode}/
```

从 `{OrgCode}` 开始，两者结构完全一致。企业资料仅多了一层 `{EnterpriseCode}`。

### 1.4 放弃任务维度

**决策**：OSS 路径中**不包含** `{TaskCode}`。

**理由**：
1. 一个企业、一个标准、一个阶段下的资源是固定的，不应因任务不同而路径不同
2. 任务的核心作用是**后期付费体系**（按任务收费），与文件存储无关
3. 同一企业同一标准同一阶段下的文件，可以被多次审核任务复用
4. 任务信息通过数据库 `TaskId` 字段关联，不需要体现在 OSS 路径中

### 1.5 不包含审核员维度

**决策**：OSS 路径中**不包含** `{AuditorCode}`。

**理由**：
1. 企业已经有唯一 `EnterpriseCode`，天然实现企业级隔离
2. 审核员创建企业的关系在数据库中维护（`ent_enterprise.created_by`），不需要 OSS 路径冗余
3. 同一审核员的多个企业，通过不同的 `EnterpriseCode` 自然区分

---

## 二、审核员完整业务链路

### 2.1 业务流程

```
┌─────────────────────────────────────────────────────────────────────┐
│                         审核员完整业务链路                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ① 认证机构初始化（平台管理员）                                        │
│     cert_certification_body ← 平台预置                                │
│     ↓                                                                │
│  ② 审核员注册                                                          │
│     注册方式：手机验证码注册（或 email+密码+手机号）                      │
│     选择认证机构 → Sys_User (UserType=22, OrgCode=CB001)               │
│     ↓                                                                │
│  ③ 审核员创建企业（1:N）                                               │
│     企业绑定在审核员下                                                 │
│     同名企业在不同审核员下 = 两个独立企业                                │
│     ent_enterprise (created_by = 审核员ID, org_code = CB001)          │
│     ↓                                                                │
│  ④ 新建审核任务                                                        │
│     选择企业 + 选择标准 + 选择阶段                                      │
│     ent_enterprise_phase (企业Code + 标准Code + 阶段Code)              │
│     audit_task (phase_code + auditor_id + task_number)               │
│     ↓                                                                │
│  ⑤ 上传企业资料                                                        │
│     基于标准目录模板 → 实例化企业目录                                    │
│     ent_enterprise_document (企业目录实例)                              │
│     ent_enterprise_file (企业实际上传文件)                              │
│     OSS 路径：/enterprise-documents/{EntCode}/{OrgCode}/...           │
│     ↓                                                                │
│  ⑥ 文件转换 + 内容提取                                                  │
│     .doc → .docx 转换                                                  │
│     ent_extraction_result (字段级提取)                                  │
│     ent_table_extraction_result (表格级提取)                            │
│     ↓                                                                │
│  ⑦ 自动校验 → 自动 NC                                                  │
│     ent_file_compliance_check (合规检查结果)                            │
│     audit_nonconformity (自动+手动 NC)                                 │
│     ↓                                                                │
│  ⑧ 审核员复核                                                          │
│     audit_checklist_item (逐条款检查)                                   │
│     audit_finding (审核发现)                                           │
│     ↓                                                                │
│  ⑨ 生成报告                                                            │
│     rpt_report_task (报告任务)                                         │
│     rpt_audit_report (报告正文)                                        │
│     rpt_report_section (章节内容)                                      │
│     rpt_report_section_source (内容溯源)                                │
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 审核员注册流程细化

```
审核员注册：
  1. 填写手机号 → 发送验证码 → 验证
  2. 填写登录名 + 密码
  3. 选择认证机构（从 cert_certification_body 列表选择）
  4. 提交注册
  
  → 写入 Sys_User 表：
    - User_Id = 自增
    - UserName = 登录名
    - UserPwd = 加密密码
    - PhoneNo = 手机号
    - Email = 邮箱（可选）
    - UserType = 22（普通审核员）
    - OrgCode = 选择的认证机构 Code
    - Enable = 1
  
  → 可选：写入 cert_auditor_profile 表（审核员资质信息）
    - 资格证书编号、审核范围、专业领域等
```

### 2.3 任务与付费体系的关系

```
企业 → 阶段 → 任务（可多次发起）
                    │
                    ├── 任务1：初审 NC + 报告（付费）
                    ├── 任务2：一监 NC + 报告（付费）
                    └── 任务3：报告补充生成（付费）
```

- 企业阶段下的**文件资源是固定的**（同一企业同一阶段同一标准）
- 每次发起任务（NC/报告生成）是独立的计费单元
- 任务记录在 `audit_task` 表中，不影响 OSS 存储路径

---

## 三、现有 32 张表规范化改造方案

### 3.1 命名规范问题汇总

**V2 规范要求**：
- 表名：`{前缀}_{模块名}`，全小写下划线
- 列名：snake_case（如 `create_id`、`org_code`）
- 关联：统一用 `code`（GUID），不用 `id`（自增主键）
- 基类字段：`id`、`code`、`create_id`、`creator`、`create_date`、`modify_id`、`modifier`、`modify_date`、`delete_id`、`deleter`、`delete_time`

**当前 32 张表分类**：

| 类别 | 表名 | 问题 | 处理 |
|------|------|------|------|
| **✅ 符合规范** | `cert_ai_config`、`cert_ai_usage_log`、`cert_doc_extraction_rule`、`cert_doc_field_def`、`cert_doc_table_def`、`cert_doc_table_field_def`、`cert_sys_config` | snake_case 列名，符合基类规范 | 保留 |
| **⚠️ PascalCase 列名** | `cert_certification_body`、`cert_iso_standard`、`cert_iso_clause`、`cert_phase_definition`、`cert_cert_stage`、`cert_org_config`、`cert_org_standard`、`cert_org_stage`、`cert_directory_template`、`cert_file_requirement`、`cert_report_template`、`cert_standard_phase_config`、`cert_clause_extraction_rule`、`cert_extraction_field`、`cert_extraction_rule`、`cert_validation_rule`、`cert_validation_rule_source` | 列名用 PascalCase（如 `CreateID`、`OrgCode`），不符合 snake_case 规范 | **重建表，列名改 snake_case** |
| **⚠️ PascalCase + 冗余字段** | `cert_standard_directory_config`、`cert_standard_directory_folder`、`cert_standard_directory_file` | 既有 PascalCase 列名，又有 `Status_field`、`Enable_field`、`Sort`、`IsValid`、`TaskId`、`UploadStatus`、`StoragePath`、`FullPath` 等业务字段混入 | **重建表** |
| **❌ 不符合前缀规范** | `cert_enterprise` | 应为 `ent_` 前缀 | **废弃，迁移到 `ent_enterprise`** |
| **❌ 不符合前缀规范** | `cert_registration` | 应为 `sys_` 或 `cert_` + snake_case | **废弃，审核员注册直接写 Sys_User** |
| **❌ 不符合前缀规范** | `cert_application` | V2 无此表设计 | **废弃** |
| **❌ 不符合前缀规范** | `cert_upload_task` | V2 用 `yzh_queue_task` 替代 | **废弃** |
| **❌ 不符合前缀规范** | `cert_message` | 应为 `sys_message` | **保留，后续改名** |

### 3.2 冗余字段问题

以下表存在 `Status_field`、`Enable_field`、`Sort` 等冗余字段（Vol 框架遗留）：

| 表 | 冗余字段 | 说明 |
|----|----------|------|
| `cert_standard_directory_config` | `Status_field`、`Enable_field`、`Sort`、`Remark` | 与 `Status`、`Enable` 重复 |
| `cert_standard_directory_folder` | 同上 + `TaskId`、`IsValid` | `TaskId` 不应在目录模板表中 |
| `cert_standard_directory_file` | 同上 + `TaskId`、`IsValid`、`UploadStatus`、`StoragePath`、`FullPath`、`converted_storage_path`、`convert_status`、`convert_message`、`convert_date` | 文件模板表不应有上传状态和存储路径 |

### 3.3 标准目录相关表重构方案

**当前 3 张表**（模板+实际混用）→ **重构为 V2 设计的独立表**：

| 当前表 | 职责 | 重构后 | 新职责 |
|--------|------|--------|--------|
| `cert_standard_directory_config` | 标准-阶段目录配置 | `cert_directory_template` (A-06) | 仅存标准目录模板的文件夹树结构 |
| `cert_standard_directory_folder` | 标准目录文件夹 | 合并到 `cert_directory_template` | 模板文件夹定义 |
| `cert_standard_directory_file` | 标准目录文件（模板+实际） | 拆分：`cert_file_requirement` (A-07) + `ent_enterprise_file` (B-04) | A-07 存文件要求，B-04 存企业实际上传文件 |

### 3.4 审核员相关新表设计

#### 新表：cert_auditor_profile（审核员资质档案）

> 审核员注册后，补充审核员资质信息。Sys_User 只存基本登录信息。

```sql
CREATE TABLE cert_auditor_profile (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '审核员编码(GUID)',
    user_id         bigint       NOT NULL COMMENT '关联 Sys_User.User_Id',
    org_code        varchar(50)  NOT NULL COMMENT '所属认证机构编码',
    auditor_no      varchar(50)  NOT NULL UNIQUE COMMENT '审核员资格证号',
    auditor_name    varchar(100) NOT NULL COMMENT '审核员姓名',
    phone           varchar(20)  NOT NULL COMMENT '手机号',
    email           varchar(200) COMMENT '邮箱',
    qualification   json         COMMENT '审核资质(标准类型+级别)',
    expertise_areas json        COMMENT '专业领域(行业分类)',
    status          varchar(20)  NOT NULL DEFAULT 'active' COMMENT 'active/inactive/suspended',
    
    -- 基类审计字段
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    
    UNIQUE KEY uk_user_id (user_id),
    INDEX idx_org_code (org_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='审核员资质档案';
```

#### V2 表结构修正：ent_enterprise（企业）

> 在 V2 基础上增加审核员关联和企业编码字段。

```sql
CREATE TABLE ent_enterprise (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '企业编码(GUID)',
    enterprise_no   varchar(20)  NOT NULL UNIQUE COMMENT '企业短编码(如 ENT-2026-0001，用于OSS路径)',
    org_code        varchar(50)  NOT NULL COMMENT '所属认证机构编码',
    name            varchar(200) NOT NULL COMMENT '企业全称',
    short_name      varchar(100) COMMENT '简称',
    credit_code     varchar(50)  UNIQUE COMMENT '统一社会信用代码',
    legal_person    varchar(50)  COMMENT '法人代表',
    province        varchar(50)  COMMENT '省份',
    city            varchar(50)  COMMENT '城市',
    address         varchar(500) COMMENT '企业地址',
    industry_type   varchar(100) COMMENT '行业类型',
    employee_count  int          COMMENT '员工人数',
    cert_scope      text         COMMENT '认证范围描述',
    contact_name    varchar(50)  COMMENT '对接人姓名',
    contact_phone   varchar(20)  COMMENT '对接人电话',
    contact_email   varchar(200) COMMENT '对接人邮箱',
    status          varchar(20)  NOT NULL DEFAULT 'active' COMMENT 'active/archived',
    archive_date    date         COMMENT '归档日期',
    
    -- 基类审计字段
    create_id       int          NOT NULL COMMENT '创建审核员ID(关联Sys_User.User_Id)',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    
    INDEX idx_org_code (org_code),
    INDEX idx_create_id (create_id),
    INDEX idx_credit_code (credit_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业';
```

**关键变更**：
1. 新增 `enterprise_no` 短编码字段，用于 OSS 路径（比 GUID 更可读）
2. `create_id` 直接关联 `Sys_User.User_Id`，明确"审核员创建了哪些企业"
3. 补齐 `province`、`city`、`industry_type`、`employee_count`（从 `cert_enterprise` 迁移）
4. 列名统一 snake_case

#### V2 表结构修正：ent_enterprise_phase（企业阶段）

```sql
CREATE TABLE ent_enterprise_phase (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '企业阶段编码(GUID)',
    enterprise_code varchar(36)  NOT NULL COMMENT '关联企业 code',
    standard_code   varchar(36)  NOT NULL COMMENT '关联标准 code',
    phase_code      varchar(36)  NOT NULL COMMENT '关联阶段定义 code',
    status          varchar(20)  NOT NULL DEFAULT 'pending' COMMENT 'pending/in_progress/completed/closed',
    started_at      datetime     COMMENT '开始时间',
    completed_at    datetime     COMMENT '完成时间',
    
    -- 基类审计字段
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    
    UNIQUE KEY uk_ent_std_phase (enterprise_code, standard_code, phase_code),
    INDEX idx_enterprise_code (enterprise_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业阶段';
```

#### V2 表结构修正：ent_enterprise_document（企业文档目录）

```sql
CREATE TABLE ent_enterprise_document (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '企业文档编码(GUID)',
    enterprise_code varchar(36)  NOT NULL COMMENT '关联企业 code',
    phase_code      varchar(36)  COMMENT '关联企业阶段 code(scope=phase时必填)',
    scope           varchar(20)  NOT NULL COMMENT 'enterprise_base=企业基础资料共享层 / phase=阶段隔离层',
    template_folder_code varchar(36) COMMENT '对应的模板文件夹 code',
    parent_code     varchar(36)  COMMENT '父文件夹 code(树形结构)',
    folder_name     varchar(200) NOT NULL COMMENT '文件夹名称',
    sort_order      int          NOT NULL DEFAULT 0,
    
    -- 基类审计字段
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    
    INDEX idx_enterprise_code (enterprise_code),
    INDEX idx_phase_code (phase_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业文档目录';
```

#### V2 表结构修正：ent_enterprise_file（企业文件）

```sql
CREATE TABLE ent_enterprise_file (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '文件编码(GUID)',
    enterprise_code varchar(36)  NOT NULL COMMENT '关联企业 code',
    folder_code     varchar(36)  NOT NULL COMMENT '关联企业文档目录 code',
    file_name       varchar(500) NOT NULL COMMENT '文件名(中文原样)',
    file_type       varchar(50)  NOT NULL COMMENT '文件类型(pdf/docx/xlsx等)',
    file_size       bigint       NOT NULL COMMENT '文件大小(bytes)',
    storage_path    varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    converted_storage_path varchar(500) COMMENT '转换后文件路径(.docx/.xlsx)',
    convert_status  varchar(20)  COMMENT 'null/pending/converting/converted/failed',
    convert_message varchar(1024) COMMENT '转换失败原因',
    convert_date    datetime     COMMENT '转换完成时间',
    file_hash       varchar(64)  COMMENT 'SHA256哈希(增量审核依据)',
    current_version int          NOT NULL DEFAULT 1 COMMENT '当前版本号',
    upload_status   varchar(20)  NOT NULL DEFAULT 'active' COMMENT 'pending/uploading/active/failed',
    
    -- 基类审计字段
    create_id       int          NOT NULL COMMENT '上传人ID(审核员)',
    creator         varchar(50)  COMMENT '上传人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    
    INDEX idx_enterprise_code (enterprise_code),
    INDEX idx_folder_code (folder_code),
    INDEX idx_create_id (create_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业文件';
```

#### V2 表结构修正：audit_task（审核任务）

```sql
CREATE TABLE audit_task (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '任务编码(GUID)',
    phase_code      varchar(36)  NOT NULL COMMENT '关联企业阶段 code',
    task_number     varchar(50)  NOT NULL UNIQUE COMMENT '任务编号',
    auditor_id      bigint       NOT NULL COMMENT '审核员ID(关联Sys_User.User_Id)',
    status          varchar(20)  NOT NULL DEFAULT 'pending' COMMENT 'pending/in_progress/completed/closed',
    planned_date    date         COMMENT '计划审核日期',
    actual_start_date date       COMMENT '实际开始日期',
    actual_complete_date date     COMMENT '实际完成日期',
    audit_scope     text         COMMENT '审核范围描述',
    
    -- 基类审计字段
    create_id       int          NOT NULL COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    
    INDEX idx_phase_code (phase_code),
    INDEX idx_auditor_id (auditor_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='审核任务';
```

---

## 四、现有表分类处理决策

### 4.1 保留并改造的表（14 张）

这些表结构基本合理，但需要将 PascalCase 列名改为 snake_case：

| # | 表名 | 改造内容 |
|---|------|----------|
| 1 | `cert_certification_body` | 列名 snake_case 化，收敛 `cert_org_config` 的字段 |
| 2 | `cert_iso_standard` | 列名 snake_case 化 |
| 3 | `cert_iso_clause` | 列名 snake_case 化 |
| 4 | `cert_phase_definition` | 列名 snake_case 化 |
| 5 | `cert_standard_phase_config` | 列名 snake_case 化 |
| 6 | `cert_directory_template` | 列名 snake_case 化，合并原 `cert_standard_directory_folder` 的模板职责 |
| 7 | `cert_file_requirement` | 列名 snake_case 化，合并原 `cert_standard_directory_file` 的模板职责 |
| 8 | `cert_extraction_rule` | 列名 snake_case 化 |
| 9 | `cert_extraction_field` | 列名 snake_case 化 |
| 10 | `cert_validation_rule` | 列名 snake_case 化 |
| 11 | `cert_validation_rule_source` | 列名 snake_case 化 |
| 12 | `cert_report_template` | 列名 snake_case 化 |
| 13 | `cert_clause_extraction_rule` | 列名 snake_case 化 |
| 14 | `cert_message` | 改名 `sys_message`，列名 snake_case 化 |

### 4.2 保留不动的表（7 张）

这些表已经是 snake_case，符合规范：

| # | 表名 | 说明 |
|---|------|------|
| 1 | `cert_ai_config` | AI 配置 |
| 2 | `cert_ai_usage_log` | AI 调用日志 |
| 3 | `cert_doc_extraction_rule` | 文档提取规则 |
| 4 | `cert_doc_field_def` | 文档字段定义 |
| 5 | `cert_doc_table_def` | 文档表定义 |
| 6 | `cert_doc_table_field_def` | 文档表字段定义 |
| 7 | `cert_sys_config` | 系统配置 |

### 4.3 废弃的表（8 张）

| # | 表名 | 废弃原因 | 替代方案 |
|---|------|----------|----------|
| 1 | `cert_enterprise` | 前缀错误 + 双表并存 | 迁移到 `ent_enterprise` |
| 2 | `cert_registration` | 审核员注册直接写 Sys_User | 无需独立注册表 |
| 3 | `cert_application` | V2 无此表设计 | 用 `ent_enterprise_phase` 替代 |
| 4 | `cert_upload_task` | 用队列中心替代 | `yzh_queue_task` |
| 5 | `cert_standard_directory_config` | 职责合并 | → `cert_directory_template` |
| 6 | `cert_standard_directory_folder` | 职责合并 | → `cert_directory_template` |
| 7 | `cert_standard_directory_file` | 拆分 | 模板→`cert_file_requirement`，实际→`ent_enterprise_file` |
| 8 | `cert_org_config` | 收敛到 `cert_certification_body` | 字段合并 |

### 4.4 保留但改名的表（3 张）

| # | 当前表名 | 改为 | 原因 |
|---|---------|------|------|
| 1 | `cert_cert_stage` | `cert_stage_definition` | 与 `cert_phase_definition` 区分（Stage = 审核阶段，Phase = 认证阶段） |
| 2 | `cert_org_standard` | `cert_cb_standard` | `org_` 易与多租户混淆，`cb_` 更明确指向 CertificationBody |
| 3 | `cert_org_stage` | `cert_cb_stage` | 同上 |

### 4.5 新建的表

| # | 表名 | 说明 |
|---|------|------|
| 1 | `cert_auditor_profile` | 审核员资质档案 |
| 2 | `ent_enterprise` | 企业（V2 设计，已建但需重建补字段） |
| 3 | `ent_enterprise_phase` | 企业阶段（已建但需重建） |
| 4 | `ent_enterprise_document` | 企业文档目录（已建但需重建） |
| 5 | `ent_enterprise_file` | 企业文件（已建但需重建） |
| 6 | `ent_file_version` | 文件版本 |
| 7 | `ent_file_pre_check_result` | 资料质量预审结果 |
| 8 | `ent_file_compliance_check` | 文件合规检查 |
| 9 | `ent_extraction_result` | 文档提取结果（已建但需重建补字段） |
| 10 | `ent_table_extraction_result` | 表格提取结果（已建但需重建补字段） |
| 11 | `audit_task` | 审核任务（已建但需重建） |
| 12 | `audit_checklist_item` | 检查表条目 |
| 13 | `audit_nonconformity` | 不符合项 |
| 14 | `audit_finding` | 审核发现 |
| 15 | `audit_evidence` | 审核证据 |
| 16 | `audit_rectification` | 整改记录 |
| 17 | `rpt_report_task` | 报告任务 |
| 18 | `rpt_audit_report` | 报告正文 |
| 19 | `rpt_report_section` | 报告章节 |
| 20 | `rpt_report_section_source` | 报告内容溯源 |

---

## 五、MinIO 存储路径生成代码修改

### 5.1 CodeGeneratorService.cs 改造

```csharp
/// <summary>
/// 生成标准目录存储路径
/// 格式：/standard-directory/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
/// </summary>
public string GenerateStandardDirectoryPath(string orgCode, string standardCode, 
    string phaseCode, string folderPath, string fileName)
{
    var cleanOrg = CleanCode(orgCode);
    var cleanStandard = CleanCode(standardCode);
    var cleanPhase = CleanCode(phaseCode);
    var cleanFolderPath = folderPath?.Trim('/') ?? "";
    
    if (string.IsNullOrEmpty(cleanFolderPath))
        return $"/standard-directory/{cleanOrg}/{cleanStandard}/{cleanPhase}/{fileName}";
    
    return $"/standard-directory/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/{fileName}";
}

/// <summary>
/// 生成企业资料存储路径
/// 格式：/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/{FileName}
/// </summary>
public string GenerateEnterpriseDocumentPath(string enterpriseNo, string orgCode, 
    string standardCode, string phaseCode, string folderPath, string fileName)
{
    var cleanEnt = CleanCode(enterpriseNo);
    var cleanOrg = CleanCode(orgCode);
    var cleanStandard = CleanCode(standardCode);
    var cleanPhase = CleanCode(phaseCode);
    var cleanFolderPath = folderPath?.Trim('/') ?? "";
    
    if (string.IsNullOrEmpty(cleanFolderPath))
        return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/{fileName}";
    
    return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/{fileName}";
}

/// <summary>
/// 生成转换后文件存储路径
/// 格式：/enterprise-documents/{EnterpriseNo}/{OrgCode}/{StandardCode}/{PhaseCode}/{FolderPath}/.converted/{FileName}
/// </summary>
public string GenerateConvertedStoragePath(string enterpriseNo, string orgCode, 
    string standardCode, string phaseCode, string folderPath, string fileName)
{
    var cleanEnt = CleanCode(enterpriseNo);
    var cleanOrg = CleanCode(orgCode);
    var cleanStandard = CleanCode(standardCode);
    var cleanPhase = CleanCode(phaseCode);
    var cleanFolderPath = folderPath?.Trim('/') ?? "";
    
    if (string.IsNullOrEmpty(cleanFolderPath))
        return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/.converted/{fileName}";
    
    return $"/enterprise-documents/{cleanEnt}/{cleanOrg}/{cleanStandard}/{cleanPhase}/{cleanFolderPath}/.converted/{fileName}";
}
```

---

## 六、清空与重建计划

### 6.1 清空范围

**MinIO 清空**：
```bash
# 清空 cert-platform bucket 中的所有对象
docker exec yzh-minio sh -c "rm -rf /data/cert-platform/*"
```

**数据库清空**（标准目录相关表）：
```sql
-- 清空标准目录相关数据
TRUNCATE TABLE cert_standard_directory_file;
TRUNCATE TABLE cert_standard_directory_folder;
TRUNCATE TABLE cert_standard_directory_config;
TRUNCATE TABLE cert_upload_task;

-- 清空企业相关旧数据
TRUNCATE TABLE cert_enterprise;
TRUNCATE TABLE cert_application;
TRUNCATE TABLE cert_registration;

-- 清空 V2 空表（重建用）
TRUNCATE TABLE ent_enterprise;
TRUNCATE TABLE ent_enterprise_phase;
TRUNCATE TABLE ent_enterprise_document;
TRUNCATE TABLE ent_enterprise_file;
TRUNCATE TABLE ent_extraction_result;
TRUNCATE TABLE ent_table_extraction_result;
TRUNCATE TABLE audit_task;
```

### 6.2 重建顺序

```
Step 1: 清空 MinIO + 数据库
Step 2: 重建标准目录相关表（DROP + CREATE，snake_case 列名）
Step 3: 重建企业相关表（ent_* 系列）
Step 4: 重建审核任务表（audit_*）
Step 5: 重建报告表（rpt_*）
Step 6: 新建审核员资质表（cert_auditor_profile）
Step 7: 更新 C# 实体类（snake_case → PascalCase + [Column] 特性）
Step 8: 更新 CodeGeneratorService.cs 路径生成逻辑
Step 9: 更新 StandardDirectoryService.cs 上传逻辑
Step 10: 测试验证
```

### 6.3 改造后的 OSS 验证流程

```
1. 平台管理员初始化认证机构 → cert_certification_body
2. 审核员注册 → Sys_User + cert_auditor_profile
3. 审核员创建企业 → ent_enterprise (enterprise_no = ENT-2026-0001)
4. 新建审核任务 → ent_enterprise_phase + audit_task
5. 上传文件 → ent_enterprise_file
   OSS 路径 = /enterprise-documents/ENT-2026-0001/CB001/ISO134852016/STAGE01/1质量手册/质量手册.docx
6. 文件转换 → .converted/ 子目录
7. 内容提取 → ent_extraction_result
8. 自动校验 → ent_file_compliance_check → audit_nonconformity
9. 生成报告 → rpt_report_task → rpt_audit_report
```

---

## 七、完整业务链路与 OSS 路径对应关系

| 业务步骤 | 数据库表 | OSS 路径 | 说明 |
|---------|---------|---------|------|
| 认证机构初始化 | `cert_certification_body` | — | 无文件 |
| 审核员注册 | `Sys_User` + `cert_auditor_profile` | — | 无文件 |
| 创建企业 | `ent_enterprise` | — | 无文件 |
| 新建审核任务 | `audit_task` | — | 无文件（任务不产生 OSS 路径） |
| 上传企业资料 | `ent_enterprise_file` | `/enterprise-documents/{EntNo}/{OrgCode}/{StdCode}/{PhaseCode}/{Folder}/{File}` | 企业资料区 |
| 文件转换 | `ent_enterprise_file.converted_storage_path` | `.../{Folder}/.converted/{File}` | 转换后文件 |
| 标准目录模板文件 | `cert_file_requirement` | `/standard-directory/{OrgCode}/{StdCode}/{PhaseCode}/{Folder}/{File}` | 标准目录区 |
| 审核证据（现场采集） | `audit_evidence` | `/enterprise-documents/{EntNo}/evidence/{TaskCode}/{File}` | 证据文件 |
| 报告输出 | `rpt_audit_report` | `/enterprise-doc