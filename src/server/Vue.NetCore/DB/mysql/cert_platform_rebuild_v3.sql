-- ============================================================
-- 体系认证平台 数据库重建 V3
-- 日期: 2026-08-14
-- 说明: snake_case 列名 + V2 规范 + 双 OSS 路径
-- ============================================================

SET FOREIGN_KEY_CHECKS = 0;
SET NAMES utf8mb4;

-- ============================================================
-- 域 A：认证体系配置（13 张表）
-- ============================================================

-- A-01 认证机构（收敛原 cert_org_config 字段）
DROP TABLE IF EXISTS cert_certification_body;
CREATE TABLE cert_certification_body (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '认证机构编码(GUID)',
    org_code        varchar(50)  NOT NULL UNIQUE COMMENT '机构简称编码(如CB001)',
    name            varchar(200) NOT NULL COMMENT '机构全称',
    short_name      varchar(100) COMMENT '简称',
    cb_code         varchar(50)  UNIQUE COMMENT 'CNAS认可编号',
    legal_person    varchar(100) COMMENT '法人代表',
    contact_name    varchar(50)  COMMENT '联系人',
    contact_phone   varchar(20)  COMMENT '联系电话',
    contact_email   varchar(200) COMMENT '联系邮箱',
    address         varchar(500) COMMENT '地址',
    logo_url        varchar(500) COMMENT 'Logo URL',
    scope_text      text         COMMENT '认证范围描述',
    theme_config    json         COMMENT '主题配置(JSON)',
    login_config    json         COMMENT '登录配置(JSON)',
    max_users       int          NOT NULL DEFAULT 100 COMMENT '最大用户数',
    max_enterprises int          NOT NULL DEFAULT 1000 COMMENT '最大企业数',
    expire_date     date         COMMENT '到期日期',
    status          varchar(20)  NOT NULL DEFAULT 'active' COMMENT 'active/inactive',
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
    INDEX idx_org_code (org_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='认证机构';

-- A-02 ISO标准
DROP TABLE IF EXISTS cert_iso_standard;
CREATE TABLE cert_iso_standard (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '标准编码(GUID)',
    org_code        varchar(50)  NOT NULL COMMENT '所属认证机构编码',
    standard_code   varchar(50)  NOT NULL COMMENT '标准编号(如ISO9001)',
    standard_name   varchar(200) NOT NULL COMMENT '标准中文名称',
    version_year    year         NOT NULL COMMENT '版本年份',
    category        varchar(50)  DEFAULT 'quality' COMMENT '类别',
    description     text         COMMENT '描述',
    status          varchar(20)  NOT NULL DEFAULT 'active',
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    UNIQUE KEY uk_org_std_ver (org_code, standard_code, version_year),
    INDEX idx_org_code (org_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='ISO标准';

-- A-03 标准条款
DROP TABLE IF EXISTS cert_iso_clause;
CREATE TABLE cert_iso_clause (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '条款编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    standard_code   varchar(36)  NOT NULL COMMENT '所属标准 code',
    parent_code     varchar(36)  COMMENT '父条款 code(树形结构)',
    clause_number   varchar(20)  NOT NULL COMMENT '条款编号(如7.1)',
    title           varchar(200) NOT NULL COMMENT '条款标题',
    description     text         COMMENT '条款原文或摘要',
    sort_order      int          DEFAULT 0,
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    INDEX idx_standard_code (standard_code),
    INDEX idx_parent_code (parent_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='标准条款';

-- A-04 阶段定义
DROP TABLE IF EXISTS cert_phase_definition;
CREATE TABLE cert_phase_definition (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '阶段编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    phase_code      varchar(20)  NOT NULL UNIQUE COMMENT '阶段编号(S1/S2/Surv1/Surv2/Recert)',
    phase_name      varchar(100) NOT NULL COMMENT '中文名称',
    sequence_order  int          NOT NULL COMMENT '顺序(1=S1 2=S2 3=一监 4=二监 5=再认证)',
    description     text         COMMENT '阶段说明',
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间'
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='阶段定义';

-- A-05 标准-阶段配置
DROP TABLE IF EXISTS cert_standard_phase_config;
CREATE TABLE cert_standard_phase_config (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '配置编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    standard_code   varchar(36)  NOT NULL COMMENT '关联标准 code',
    phase_code      varchar(36)  NOT NULL COMMENT '关联阶段定义 code',
    required_clauses json        COMMENT '此阶段需检查的条款 code 列表(JSON)',
    required_files  json         COMMENT '此阶段必需的文件清单(JSON)',
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    UNIQUE KEY uk_std_phase (standard_code, phase_code),
    INDEX idx_org_code (org_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='标准-阶段配置';

-- A-06 文件目录模板（合并原 cert_standard_directory_folder 职责）
DROP TABLE IF EXISTS cert_directory_template;
CREATE TABLE cert_directory_template (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '模板编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    config_code     varchar(36)  NOT NULL COMMENT '关联标准-阶段配置 code',
    parent_code     varchar(36)  COMMENT '父文件夹 code(树形结构)',
    folder_name     varchar(200) NOT NULL COMMENT '文件夹名称',
    sort_order      int          DEFAULT 0,
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    INDEX idx_config_code (config_code),
    INDEX idx_parent_code (parent_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='文件目录模板';

-- A-07 文件要求（合并原 cert_standard_directory_file 模板职责）
DROP TABLE IF EXISTS cert_file_requirement;
CREATE TABLE cert_file_requirement (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '文件要求编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    folder_code     varchar(36)  NOT NULL COMMENT '所属模板文件夹 code',
    file_name_template varchar(200) NOT NULL COMMENT '文件名称模板',
    file_type       varchar(50)  NOT NULL COMMENT '允许的文件类型(pdf/docx/xlsx等)',
    is_required     tinyint(1)   DEFAULT 1 COMMENT '是否必须提供',
    max_size_mb     int          DEFAULT 10 COMMENT '最大文件大小(MB)',
    description     text         COMMENT '文件说明/要求描述',
    sort_order      int          DEFAULT 0,
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    INDEX idx_folder_code (folder_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='文件要求';

-- A-08 数据提取规则
DROP TABLE IF EXISTS cert_extraction_rule;
CREATE TABLE cert_extraction_rule (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '规则编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    file_requirement_code varchar(36) NOT NULL COMMENT '关联文件要求 code',
    skill_code      varchar(36)  NOT NULL COMMENT '使用的 Skill code',
    rule_type       varchar(20)  NOT NULL COMMENT 'title/table/text/form/mixed',
    rule_config     json         NOT NULL COMMENT '规则配置(参数/提取逻辑)',
    description     text         COMMENT '规则说明',
    is_active       tinyint(1)   DEFAULT 1,
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    INDEX idx_file_req_code (file_requirement_code),
    INDEX idx_skill_code (skill_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='数据提取规则';

-- A-09 提取字段定义
DROP TABLE IF EXISTS cert_extraction_field;
CREATE TABLE cert_extraction_field (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '字段编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    rule_code       varchar(36)  NOT NULL COMMENT '所属提取规则 code',
    skill_code      varchar(36)  COMMENT '提取此字段的 Skill code(可覆盖规则级)',
    field_code      varchar(100) NOT NULL COMMENT '字段编码(如 iso9001.ent_base.biz_lic.name)',
    label_tag       varchar(500) NOT NULL COMMENT '字段标签(如[ISO9001_企业基础资料_营业执照_企业名称])',
    field_name      varchar(100) NOT NULL COMMENT '字段显示名称',
    field_type      varchar(20)  DEFAULT 'string' COMMENT 'string/number/date/boolean/enum/list',
    enum_values     json         COMMENT '枚举值列表(field_type=enum时)',
    sort_order      int          DEFAULT 0,
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    UNIQUE KEY uk_label_tag (label_tag),
    INDEX idx_rule_code (rule_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='提取字段定义';

-- A-10 校验规则
DROP TABLE IF EXISTS cert_validation_rule;
CREATE TABLE cert_validation_rule (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '规则编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    standard_code   varchar(36)  NOT NULL COMMENT '关联标准 code',
    phase_code      varchar(36)  NOT NULL COMMENT '关联阶段 code',
    clause_code     varchar(36)  NOT NULL COMMENT '关联条款 code',
    workflow_code   varchar(36)  NOT NULL COMMENT '关联工作流定义 code',
    rule_code       varchar(50)  NOT NULL UNIQUE COMMENT '规则编号(如VR-001)',
    rule_name       varchar(200) NOT NULL COMMENT '规则名称',
    severity_if_violated varchar(20) NOT NULL COMMENT 'major/minor/observation',
    nc_description_template text  COMMENT 'NC描述模板',
    is_active       tinyint(1)   DEFAULT 1,
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    INDEX idx_std_phase (standard_code, phase_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='校验规则';

-- A-11 校验规则溯源
DROP TABLE IF EXISTS cert_validation_rule_source;
CREATE TABLE cert_validation_rule_source (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    rule_code       varchar(36)  NOT NULL COMMENT '校验规则 code',
    file_requirement_code varchar(36) NOT NULL COMMENT '溯源文件要求 code',
    source_path     varchar(500) COMMENT '溯源路径(文件内位置描述)',
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    INDEX idx_rule_code (rule_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='校验规则溯源';

-- A-12 报告模板
DROP TABLE IF EXISTS cert_report_template;
CREATE TABLE cert_report_template (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '模板编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    cb_code         varchar(36)  NOT NULL COMMENT '认证机构 code',
    standard_code   varchar(36)  NOT NULL COMMENT '标准 code',
    phase_code      varchar(36)  NOT NULL COMMENT '阶段 code',
    template_name   varchar(200) NOT NULL COMMENT '模板名称',
    template_file_path varchar(500) COMMENT '空白文档文件路径(MinIO)',
    section_config  json         COMMENT '报告章节配置(JSON)',
    is_default      tinyint(1)   DEFAULT 0,
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    INDEX idx_cb_std_phase (cb_code, standard_code, phase_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='报告模板';

-- A-13 条款提取规则
DROP TABLE IF EXISTS cert_clause_extraction_rule;
CREATE TABLE cert_clause_extraction_rule (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    org_code        varchar(50)  COMMENT '所属认证机构编码',
    clause_code     varchar(36)  NOT NULL COMMENT '条款 code',
    workflow_code   varchar(36)  NOT NULL COMMENT '关联工作流定义 code',
    description     text         COMMENT '规则集说明',
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    delete_id       int          COMMENT '删除人ID',
    deleter         varchar(50)  COMMENT '删除人姓名',
    delete_time     datetime     COMMENT '删除时间',
    INDEX idx_clause_code (clause_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='条款提取规则';

-- ============================================================
-- 域 B：企业档案（9 张表）
-- ============================================================

-- B-01 企业
DROP TABLE IF EXISTS ent_enterprise;
CREATE TABLE ent_enterprise (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '企业编码(GUID)',
    enterprise_no   varchar(20)  NOT NULL UNIQUE COMMENT '企业短编码(如ENT-2026-0001，用于OSS路径)',
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
    INDEX idx_create_id (create_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业';

-- B-02 企业阶段
DROP TABLE IF EXISTS ent_enterprise_phase;
CREATE TABLE ent_enterprise_phase (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '企业阶段编码(GUID)',
    enterprise_code varchar(36)  NOT NULL COMMENT '关联企业 code',
    standard_code   varchar(36)  NOT NULL COMMENT '关联标准 code',
    phase_code      varchar(36)  NOT NULL COMMENT '关联阶段定义 code',
    status          varchar(20)  NOT NULL DEFAULT 'pending' COMMENT 'pending/in_progress/completed/closed',
    started_at      datetime     COMMENT '开始时间',
    completed_at    datetime     COMMENT '完成时间',
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    UNIQUE KEY uk_ent_std_phase (enterprise_code, standard_code, phase_code),
    INDEX idx_enterprise_code (enterprise_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业阶段';

-- B-03 企业文档目录
DROP TABLE IF EXISTS ent_enterprise_document;
CREATE TABLE ent_enterprise_document (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '企业文档编码(GUID)',
    enterprise_code varchar(36)  NOT NULL COMMENT '关联企业 code',
    phase_code      varchar(36)  COMMENT '关联企业阶段 code(scope=phase时必填)',
    scope           varchar(20)  NOT NULL COMMENT 'enterprise_base=企业基础资料共享层 / phase=阶段隔离层',
    template_folder_code varchar(36) COMMENT '对应的模板文件夹 code',
    parent_code     varchar(36)  COMMENT '父文件夹 code(树形结构)',
    folder_name     varchar(200) NOT NULL COMMENT '文件夹名称',
    sort_order      int          DEFAULT 0,
    create_id       int          COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    INDEX idx_enterprise_code (enterprise_code),
    INDEX idx_phase_code (phase_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='企业文档目录';

-- B-04 企业文件
DROP TABLE IF EXISTS ent_enterprise_file;
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

-- B-05 文件版本
DROP TABLE IF EXISTS ent_file_version;
CREATE TABLE ent_file_version (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '版本编码(GUID)',
    file_code       varchar(36)  NOT NULL COMMENT '源文件 code',
    version_number  int          NOT NULL COMMENT '版本号(从1开始递增)',
    file_size       bigint       NOT NULL COMMENT '版本文件大小',
    storage_path    varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    file_hash       varchar(64)  NOT NULL COMMENT 'SHA256哈希',
    change_notes    text         COMMENT '变更说明',
    upload_by       int          NOT NULL COMMENT '上传人ID',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uk_file_version (file_code, version_number),
    INDEX idx_file_code (file_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='文件版本';

-- B-06 资料质量预审结果
DROP TABLE IF EXISTS ent_file_pre_check_result;
CREATE TABLE ent_file_pre_check_result (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    file_code       varchar(36)  NOT NULL COMMENT '被检查的文件 code',
    version_number  int          NOT NULL COMMENT '检查的文件版本',
    check_type      varchar(20)  NOT NULL COMMENT 'readability/clarity/format/completeness',
    check_result    varchar(20)  NOT NULL COMMENT 'pass/warning/block',
    message         text         COMMENT '检查信息',
    detail          json         COMMENT '详细信息(DPI值/倾斜角度/缺页数等)',
    checked_at      datetime     NOT NULL COMMENT '检查时间',
    INDEX idx_file_code (file_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='资料质量预审结果';

-- B-07 文件合规检查
DROP TABLE IF EXISTS ent_file_compliance_check;
CREATE TABLE ent_file_compliance_check (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    file_code       varchar(36)  NOT NULL COMMENT '被检查的文件 code',
    version_number  int          NOT NULL COMMENT '检查的文件版本',
    rule_code       varchar(36)  NOT NULL COMMENT '触发的校验规则 code',
    workflow_execution_code varchar(36) COMMENT '工作流执行记录 code',
    check_status    varchar(20)  NOT NULL COMMENT 'pass/fail/warning/blocked',
    message         text         COMMENT '检查信息',
    detail          json         COMMENT '详细信息(含具体位置/偏离描述)',
    checked_at      datetime     NOT NULL COMMENT '检查时间',
    INDEX idx_file_code (file_code),
    INDEX idx_rule_code (rule_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='文件合规检查';

-- B-08 文档提取结果
DROP TABLE IF EXISTS ent_extraction_result;
CREATE TABLE ent_extraction_result (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    enterprise_code varchar(36)  NOT NULL COMMENT '关联企业 code',
    phase_code      varchar(36)  COMMENT '关联企业阶段 code',
    file_code       varchar(36)  NOT NULL COMMENT '提取的源文件 code',
    version_number  int          NOT NULL COMMENT '提取的文件版本',
    rule_code       varchar(36)  NOT NULL COMMENT '使用的提取规则 code',
    field_code      varchar(36)  NOT NULL COMMENT '对应的提取字段 code',
    label_tag       varchar(500) COMMENT '字段标签冗余(便于查询)',
    extracted_value text         COMMENT '提取的值',
    confidence      decimal(3,2) COMMENT 'AI提取可信度(0.00-1.00)',
    position_info   json         COMMENT '位置信息(页码/行号/列号/单元格)',
    is_manual_edited tinyint(1)  DEFAULT 0 COMMENT '是否被人工修改',
    extracted_at    datetime     NOT NULL COMMENT '提取时间',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_enterprise_code (enterprise_code),
    INDEX idx_file_code (file_code),
    INDEX idx_label_tag (label_tag)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='文档提取结果';

-- B-09 表格提取结果
DROP TABLE IF EXISTS ent_table_extraction_result;
CREATE TABLE ent_table_extraction_result (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    enterprise_code varchar(36)  NOT NULL COMMENT '关联企业 code',
    phase_code      varchar(36)  COMMENT '关联企业阶段 code',
    file_code       varchar(36)  NOT NULL COMMENT '提取的源文件 code',
    version_number  int          NOT NULL COMMENT '提取的文件版本',
    rule_code       varchar(36)  NOT NULL COMMENT '使用的提取规则 code',
    table_index     int          DEFAULT 1 COMMENT '文档中第几个表格',
    extracted_json  json         NOT NULL COMMENT '表格内容(JSON)',
    confidence      decimal(3,2) COMMENT 'AI提取可信度',
    position_info   json         COMMENT '表格在文档中的位置信息',
    extracted_at    datetime     NOT NULL COMMENT '提取时间',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_enterprise_code (enterprise_code),
    INDEX idx_file_code (file_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='表格提取结果';

-- ============================================================
-- 域 C：审核执行（6 张表）
-- ============================================================

-- C-01 审核任务
DROP TABLE IF EXISTS audit_task;
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
    create_id       int          NOT NULL COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_id       int          COMMENT '修改人ID',
    modifier        varchar(50)  COMMENT '修改人姓名',
    modify_date     datetime     COMMENT '修改时间',
    INDEX idx_phase_code (phase_code),
    INDEX idx_auditor_id (auditor_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='审核任务';

-- C-02 检查表条目
DROP TABLE IF EXISTS audit_checklist_item;
CREATE TABLE audit_checklist_item (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    task_code       varchar(36)  NOT NULL COMMENT '所属审核任务 code',
    clause_code     varchar(36)  NOT NULL COMMENT '对应条款 code',
    audit_criteria  text         COMMENT '审核准则(标准条款原文)',
    finding_description text     COMMENT '审核发现描述',
    conformity      varchar(20)  DEFAULT 'pending' COMMENT 'pending/conform/nonconform/observation/na',
    ncs_found       int          DEFAULT 0 COMMENT '发现NC数量',
    checked_by      int          COMMENT '检查人ID',
    checked_at      datetime     COMMENT '检查时间',
    sort_order      int          DEFAULT 0,
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_date     datetime     COMMENT '修改时间',
    INDEX idx_task_code (task_code),
    INDEX idx_clause_code (clause_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='检查表条目';

-- C-03 不符合项(NC)
DROP TABLE IF EXISTS audit_nonconformity;
CREATE TABLE audit_nonconformity (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    task_code       varchar(36)  NOT NULL COMMENT '所属审核任务 code',
    clause_code     varchar(36)  NOT NULL COMMENT '对应条款 code',
    nc_number       varchar(50)  NOT NULL UNIQUE COMMENT 'NC编号',
    severity        varchar(20)  NOT NULL COMMENT 'major/minor/observation',
    description     text         NOT NULL COMMENT 'NC描述(不符合事实)',
    requirement_ref text         COMMENT '违反的标准要求原文',
    evidence_ref    text         COMMENT '客观证据引用',
    status          varchar(20)  NOT NULL DEFAULT 'open' COMMENT 'open/rectifying/rectified/pending_verification/closed',
    source_type     varchar(20)  DEFAULT 'manual' COMMENT 'auto_rule/manual',
    source_check_code varchar(36) COMMENT '触发的合规检查记录 code(auto_rule时必填)',
    rule_code       varchar(36)  COMMENT '触发的校验规则 code(auto_rule时必填)',
    due_date        date         COMMENT '整改截止日期',
    opened_by       int          NOT NULL COMMENT '开具人ID',
    opened_at       datetime     NOT NULL COMMENT '开具时间',
    closed_at       datetime     COMMENT '关闭时间',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_date     datetime     COMMENT '修改时间',
    INDEX idx_task_code (task_code),
    INDEX idx_clause_code (clause_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='不符合项(NC)';

-- C-04 审核发现明细
DROP TABLE IF EXISTS audit_finding;
CREATE TABLE audit_finding (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    checklist_item_code varchar(36) NOT NULL COMMENT '检查表条目 code',
    nc_code         varchar(36)  COMMENT '关联NC code(一条发现可生成0或1个NC)',
    source_file_code varchar(36) COMMENT '来源文件 code',
    source_position varchar(200) COMMENT '来源位置(页码/行号/列号)',
    source_content  text         COMMENT '来源内容摘录',
    finding_type    varchar(20)  NOT NULL COMMENT 'conform/discrepancy/comment',
    description     text         NOT NULL COMMENT '描述',
    confidence      decimal(3,2) COMMENT 'AI提取可信度(0.00-1.00)',
    is_manual       tinyint(1)   DEFAULT 0 COMMENT '是否人工添加',
    created_by      int          NOT NULL COMMENT '记录人ID',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_checklist_item (checklist_item_code),
    INDEX idx_nc_code (nc_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='审核发现明细';

-- C-05 审核证据
DROP TABLE IF EXISTS audit_evidence;
CREATE TABLE audit_evidence (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    task_code       varchar(36)  NOT NULL COMMENT '所属审核任务 code',
    clause_code     varchar(36)  COMMENT '关联条款 code(便于报告溯源)',
    evidence_type   varchar(20)  NOT NULL COMMENT 'photo/audio/screenshot/video/document/other',
    storage_path    varchar(500) NOT NULL COMMENT 'MinIO存储路径',
    file_hash       varchar(64)  NOT NULL COMMENT 'SHA256哈希',
    is_voided       tinyint(1)   DEFAULT 0 COMMENT '是否废弃',
    voided_at       datetime     COMMENT '废弃时间',
    voided_by       int          COMMENT '废弃操作人ID',
    captured_at     datetime     COMMENT '采集时间',
    captured_by     int          NOT NULL COMMENT '采集人ID',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_task_code (task_code),
    INDEX idx_clause_code (clause_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='审核证据';

-- C-06 整改记录
DROP TABLE IF EXISTS audit_rectification;
CREATE TABLE audit_rectification (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    nc_code         varchar(36)  NOT NULL COMMENT '关联NC code',
    correction      text         NOT NULL COMMENT '纠正措施描述',
    corrective_action text       COMMENT '纠正措施(根因分析+防再发生)',
    evidence_files  json         COMMENT '整改证据文件路径列表(JSON)',
    submitted_by    int          NOT NULL COMMENT '提交人ID',
    submitted_at    datetime     NOT NULL COMMENT '提交时间',
    verified_by     int          COMMENT '复核人ID',
    verified_at     datetime     COMMENT '复核时间',
    verify_result   varchar(20)  COMMENT 'approved/rejected',
    verify_notes    text         COMMENT '复核意见',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_nc_code (nc_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='整改记录';

-- ============================================================
-- 域 D：报告生成（4 张表）
-- ============================================================

-- D-01 报告任务
DROP TABLE IF EXISTS rpt_report_task;
CREATE TABLE rpt_report_task (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    phase_code      varchar(36)  NOT NULL COMMENT '关联企业阶段 code',
    based_on_audit_task_code varchar(36) COMMENT '基于的审核任务 code',
    template_code   varchar(36)  NOT NULL COMMENT '使用的报告模板 code',
    task_number     varchar(50)  NOT NULL UNIQUE COMMENT '报告任务编号',
    status          varchar(20)  NOT NULL DEFAULT 'pending' COMMENT 'pending/generating/completed/failed',
    started_at      datetime     COMMENT '开始生成时间',
    completed_at    datetime     COMMENT '完成时间',
    create_id       int          NOT NULL COMMENT '创建人ID',
    creator         varchar(50)  COMMENT '创建人姓名',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_date     datetime     COMMENT '修改时间',
    INDEX idx_phase_code (phase_code),
    INDEX idx_audit_task (based_on_audit_task_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='报告任务';

-- D-02 报告正文
DROP TABLE IF EXISTS rpt_audit_report;
CREATE TABLE rpt_audit_report (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    report_task_code varchar(36) NOT NULL COMMENT '关联报告任务 code',
    report_number   varchar(50)  NOT NULL UNIQUE COMMENT '报告编号',
    file_path       varchar(500) COMMENT '生成的报告文件路径(MinIO)',
    status          varchar(20)  NOT NULL DEFAULT 'draft' COMMENT 'draft/finalized',
    created_by      int          NOT NULL COMMENT '创建人ID',
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modify_date     datetime     COMMENT '修改时间',
    INDEX idx_report_task (report_task_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='报告正文';

-- D-03 报告章节
DROP TABLE IF EXISTS rpt_report_section;
CREATE TABLE rpt_report_section (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    report_code     varchar(36)  NOT NULL COMMENT '关联报告 code',
    clause_code     varchar(36)  COMMENT '关联条款 code',
    workflow_code   varchar(36)  COMMENT '关联工作流 code',
    section_name    varchar(200) NOT NULL COMMENT '章节名称',
    content         text         COMMENT '章节填充内容',
    sort_order      int          DEFAULT 0,
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_report_code (report_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='报告章节';

-- D-04 报告内容溯源
DROP TABLE IF EXISTS rpt_report_section_source;
CREATE TABLE rpt_report_section_source (
    id              bigint       NOT NULL AUTO_INCREMENT PRIMARY KEY,
    code            varchar(36)  NOT NULL UNIQUE COMMENT '编码(GUID)',
    section_code    varchar(36)  NOT NULL COMMENT '关联报告章节 code',
    source_type     varchar(20)  NOT NULL COMMENT 'extraction_result/audit_finding/nc/compliance_check',
    source_code     varchar(36)  NOT NULL COMMENT '来源记录 code',
    source_summary  text         COMMENT '来源摘要',
    sort_order      int          DEFAULT 0,
    create_date     datetime     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_section_code (section_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='报告内容溯源';

-- ============================================================
-- 审核员资质表
-- ============================================================

DROP TABLE IF EXISTS cert_auditor_profile;
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

SET FOREIGN_KEY_CHECKS = 1;

-- ============================================================
-- 完成：33 张表 + 1 张审核员资质表 = 34 张表
-- ============================================================
SELECT CONCAT('V3 rebuild complete: ', 
    (SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name LIKE 'cert_%' OR table_name LIKE 'ent_%' OR table_name LIKE 'audit_%' OR table_name LIKE 'rpt_%')
) AS result;