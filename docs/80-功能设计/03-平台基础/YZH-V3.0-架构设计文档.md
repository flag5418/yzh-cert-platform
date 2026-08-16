# YZH Framework V3.0 架构设计文档

> **版本**: v1.2（Phase 1 核心功能已实现）
> **日期**: 2026-08-07（更新）
> **状态**: 开发中
> **作者**: AI + 人工协作设计  

---

## 一、设计目标与原则

### 1.1 核心目标

构建一套**配置驱动、原子化组件、渐进式演进、精确可控**的前端 CRUD 框架，用于替代 Vol 框架的 ViewGrid，服务于认证平台及后续企业级项目。

### 1.2 设计原则（已确认）

| 原则 | 说明 |
|------|------|
| **渐进式演进** | 单表 CRUD → 主从表(左树右表) → 其他基类窗体，不追求一步到位 |
| **配置数据库化** | 所有 UI 配置存数据库，改配置即改界面（不改前端代码） |
| **原子化组件** | 功能拆分到最小粒度，最大化复用（表格/表单/选择器/弹窗各自独立） |
| **插槽扩展** | 预留丰富的插槽点，应对特殊业务场景 |
| **弹窗优化** | 限制最大高度 + 内部滚动，适应字段多的场景 |
| **统一放置** | 所有架构代码在 `src/yzh/` 目录下 |
| **命名规范化** | 数据库表以 `yzh_` 前缀，字段采用 snake_case 风格 |
| **精确可控** | 按钮/列/字段均有命名标识，支持 ref 精准操控和 watch 监听 |

### 1.3 与现有代码的关系

- **V2.0** (`src/yzh/` 当前代码): 已实现的基础版本，作为 V3.0 的起点
- **V3.0**: 在 V2.0 基础上重构，引入 Grid 布局、BCFlag、数据库配置、组件拆分
- **Vol 框架**: 作为参考借鉴（钩子体系、字典机制、主从表），不直接依赖

---

## 二、整体架构图

### 2.1 目录结构（目标状态）

```
src/yzh/
├── index.ts                          # 统一出口
│
├── types/                            # 类型定义
│   ├── index.ts
│   ├── YZHEntitySchema.ts            # 实体元信息 (已有)
│   ├── YZHLifecycles.ts              # 生命周期钩子 (已有)
│   ├── YZHPageProps.ts               # 组件 Props (已有)
│   └── YZHV3Config.ts                # [新增] 数据库配置类型定义
│
├── core/                             # 核心逻辑层（纯 TS，无 Vue 依赖）
│   ├── YZHBaseApiClient.ts           # API 客户端 (已有)
│   ├── YZHEditGuard.ts               # 编辑守卫 (已有)
│   ├── YZHPageLifecycle.ts           # 生命周期运行时 (已有)
│   ├── YZHRowDiff.ts                 # 行差异计算 (已有)
│   ├── YZHConfigLoader.ts            # [新增] 数据库配置加载器
│   └── YZHDictionary.ts              # [新增] 字典管理器
│
├── composables/                      # Vue 组合式函数
│   ├── useYZHEditMode.ts             # 编辑模式 (已有)
│   └── useYZHIncrementSync.ts        # 增量同步 (已有)
│
├── components/                       # 原子组件（每个文件单一职责）
│   ├── YzhCrudTable.vue              # 单表 CRUD 主组件 (重构)
│   ├── YzhSearchBar.vue              # [新增] 搜索栏（从 CrudTable 拆出）
│   ├── YzhToolbar.vue                # [新增] 工具栏按钮组（从 CrudTable 拆出）
│   ├── YzhDataTable.vue              # [新增] 数据表格（从 CrudTable 拆出）
│   ├── YzhPagination.vue             # [新增] 分页器（从 CrudTable 拆出）
│   ├── YzhEditDialog.vue             # [新增] 编辑弹窗（从 CrudTable 拆出）
│   ├── YzhFormGrid.vue               # [新增] CSS Grid 表单布局引擎
│   ├── YzhFormField.vue              # [新增] 单个表单字段渲染器
│   ├── YzhColumnSettings.vue         # [新增] 列设置面板
│   │
│   ├── YzhSelectPicker.vue           # [新增] 通用表格选择器弹窗
│   ├── YzhTreePicker.vue             # [新增] 通用树形选择器弹窗
│   └── YzhInputPicker.vue            # [新增] 通用文本输入弹窗
│
└── presets/                          # 预设配置
    └── defaultButtons.ts             # 默认工具栏按钮 (已有)
```

### 2.2 组件层次与数据流

```
┌─────────────────────────────────────────────────────────────────┐
│                     业务页面 (CertificationBody.vue)              │
│                                                                  │
│  <YzhCrudTable                                                    │
│    :schema="schema"                                              │
│    :config-key="'CertificationBody'"   ← 从数据库加载配置        │
│    :lifecycles="lifecycles"                                      │
│    :current-phase="0"                                            │
│  >                                                               │
│    <template #toolbarLeft>...</template>                           │
│    <template #gridHeader>...</template>                            │
│    <template #modelHeader>...</template>                           │
│    <template #modelFooter>...</template>                           │
│  </YzhCrudTable>                                                 │
└──────────────────────────────┬──────────────────────────────────┘
                               ↓ 消费配置
┌─────────────────────────────────────────────────────────────────┐
│                    YzhCrudTable (组合者)                         │
│                                                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐             │
│  │YzhSearchBar │  │ YzhToolbar  │  │YzhDataTable │             │
│  │ (搜索栏)     │  │ (工具栏)     │  │ (数据表格)   │             │
│  └─────────────┘  └─────────────┘  └──────┬──────┘             │
│                                          │                      │
│  ┌─────────────┐  ┌─────────────┐        │  ┌─────────────┐    │
│  │YzhPagination│  │YzhColumnSet-│        │  │YzhEditDialog│    │
│  │ (分页)       │  │ tings       │        │  │ (编辑弹窗)   │    │
│  └─────────────┘  └─────────────┘        └──┬──┴────────────┘    │
│                                               │                   │
│                                    ┌──────────┴──────────┐      │
│                                    │   YzhFormGrid       │      │
│                                    │   (CSS Grid 表单)    │      │
│                                    │  ┌────────────────┐  │      │
│                                    │  │YzhFormField×N │  │      │
│                                    │  └────────────────┘  │      │
│                                    └─────────────────────┘      │
└─────────────────────────────────────────────────────────────────┘
                               ↑ API 调用
┌─────────────────────────────────────────────────────────────────┐
│                    YZHBaseApiClient                              │
│         POST /api/{controllerName}/GetPageData                  │
│         POST /api/{controllerName}/Add                          │
│         POST /api/{controllerName}/Update                       │
│         POST /api/{controllerName}/Del                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## 三、数据库配置表设计

### 3.0 命名规范

所有 YZH 框架核心表统一使用 `yzh_` 前缀，字段采用 **snake_case** 风格：

| 前缀 | 用途 | 示例 |
|------|------|------|
| `yzh_` | 框架核心配置表 | `yzh_page_config`, `yzh_field_config` |
| `cert_` | 业务数据表 | `cert_certification_body`, `cert_application` |
| `sys_` | 系统表（Vol 已有） | `sys_user`, `sys_dictionary` |

### 3.1 表一：`yzh_page_config` —— 页面级配置

**职责**: 描述一个实体页面的整体行为配置（弹窗尺寸、搜索模式、权限等），与字段配置是**一对多**关系。

```sql
CREATE TABLE yzh_page_config (
    -- ====== 主键 ======
    id BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键',
    
    -- ====== 页面标识 ======
    page_key VARCHAR(50) NOT NULL COMMENT '页面唯一标识(英文), 如: CertificationBody, AuditTask',
    page_title VARCHAR(100) NOT NULL COMMENT '页面中文名, 如: 认证机构管理',
    
    -- ====== 实体映射 ======
    entity_name VARCHAR(100) NOT NULL COMMENT '实体类名(后端), 如: CertCertificationBody',
    table_name VARCHAR(100) NOT NULL COMMENT '数据库表名, 如: cert_certification_body',
    controller_name VARCHAR(100) NOT NULL COMMENT '后端Controller名(不含后缀), 如: CertCertificationBody',
    key_field VARCHAR(50) NOT NULL DEFAULT 'Id' COMMENT '主键字段名',
    key_field_type VARCHAR(10) NOT NULL DEFAULT 'number' COMMENT '主键类型: number/guid/string',
    sort_field VARCHAR(50) DEFAULT NULL COMMENT '默认排序字段',
    sort_order VARCHAR(5) DEFAULT 'desc' COMMENT '排序方向: asc/desc',
    
    -- ====== 弹窗配置 ======
    dialog_width INT DEFAULT 960 COMMENT '编辑弹窗宽度(px)',
    dialog_max_height VARCHAR(20) DEFAULT '85vh' COMMENT '弹窗最大高度(css值)',
    dialog_label_width INT DEFAULT 120 COMMENT '弹窗表单标签宽度(px)',
    
    -- ====== 表格配置 ======
    row_height VARCHAR(10) DEFAULT 'default' COMMENT '行高: default/large/small',
    stripe TINYINT DEFAULT 1 COMMENT '斑马纹: 0=关 1=开',
    show_row_number TINYINT DEFAULT 1 COMMENT '显示序号列: 0=否 1=是',
    
    -- ====== 搜索区配置 ======
    search_mode VARCHAR(10) DEFAULT 'fixed' COMMENT '搜索模式: fixed/togglable/hidden',
    
    -- ====== 工具栏按钮可见性 (JSON数组) ======
    visible_buttons TEXT DEFAULT '["add","refresh","export","import","batchDelete"]' 
        COMMENT '显示的按钮列表, 可选值: add/refresh/export/import/batchDelete/columnSetting/custom1~customN',
    
    -- ====== 功能开关 ======
    show_action_column TINYINT DEFAULT 1 COMMENT '显示操作列: 0=否 1=是',
    checkbox_selection TINYINT DEFAULT 1 COMMENT '多选框: 0=单选 1=多选',
    incremental_update TINYINT DEFAULT 1 COMMENT '增量刷新: 0=全量刷新 1=增量刷新',
    
    -- ====== 多租户 ======
    org_code VARCHAR(50) DEFAULT '' COMMENT '机构隔离码(空=全局配置)',
    
    -- ====== 状态 ======
    is_active TINYINT DEFAULT 1 COMMENT '是否启用: 0=禁用 1=启用',
    
    -- ====== 元信息 ======
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    remark VARCHAR(500) DEFAULT '' COMMENT '备注',
    
    -- ====== 约束 ======
    UNIQUE KEY uk_page_org (page_key, org_code),
    INDEX idx_page_key (page_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='YZH V3.0 页面级UI配置 - 一个实体一套配置';
```

### 3.2 表二：`yzh_field_config` —— 字段级配置

**职责**: 描述每个字段在表格列、弹窗表单、搜索区的详细配置。通过 `page_key` 关联到 `yzh_page_config`。

```sql
CREATE TABLE yzh_field_config (
    -- ====== 主键 ======
    id BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键',
    
    -- ====== 关联 ======
    page_key VARCHAR(50) NOT NULL COMMENT '关联 yzh_page_config.page_key',
    
    -- ====== 字段标识 ======
    field_name VARCHAR(50) NOT NULL COMMENT '数据库字段名(与实体属性一致), 如: Name, CbCode, Status',
    field_alias VARCHAR(100) DEFAULT '' COMMENT '字段别名/组件命名标识(见4.8节), 默认同field_name',
    
    -- ====== A. 表格列配置 ======
    xs_flag TINYINT DEFAULT 1 COMMENT '表格显示标志: 0=隐藏 1=显示',
    column_sxh INT DEFAULT 0 COMMENT '表格列显示序号(越小越靠左)',
    column_title VARCHAR(100) DEFAULT '' COMMENT '列表头标题(中文名)',
    column_width INT DEFAULT 120 COMMENT '表格列宽(px)',
    column_fixed VARCHAR(10) DEFAULT NULL COMMENT '列固定位置: left/right/null',
    sortable TINYINT DEFAULT 1 COMMENT '可排序: 0=否 1=是',
    column_formatter VARCHAR(50) DEFAULT '' COMMENT '列格式化器名称(自定义渲染)',
    show_overflow TINYINT DEFAULT 1 COMMENT '文本溢出省略号: 0=否 1=是',
    align VARCHAR(10) DEFAULT 'left' COMMENT '对齐: left/center/right',
    
    -- ====== B. 弹窗表单/Grid布局 ======
    bc_flag TINYINT DEFAULT 1 COMMENT '保存标志: 0=不保存到DB(视图关联字段) 1=保存到DB',
    form_title VARCHAR(100) DEFAULT '' COMMENT '表单标签(中文名), 为空则取column_title',
    control_type VARCHAR(20) DEFAULT 'input' COMMENT 
        '控件类型: input/textarea/select/number/decimal/date/switch/cascader/treeSelect/file/img/slot/hidden',
    grid_row INT DEFAULT 0 COMMENT 'Grid所在行(从0开始)',
    grid_col INT DEFAULT 0 COMMENT 'Grid所在列(从0开始)',
    grid_row_span INT DEFAULT 1 COMMENT '跨行数',
    grid_col_span INT DEFAULT 1 COMMENT '跨列数',
    required TINYINT DEFAULT 0 COMMENT '必填: 0=否 1=是',
    maxlength INT DEFAULT 0 COMMENT '最大长度(0=不限)',
    placeholder VARCHAR(200) DEFAULT '' COMMENT '占位文本',
    default_value VARCHAR(500) DEFAULT '' COMMENT '默认值',
    readonly TINYINT DEFAULT 0 COMMENT '只读: 0=否 1=是',
    disabled TINYINT DEFAULT 0 COMMENT '禁用: 0=否 1=是',
    precision INT DEFAULT NULL COMMENT '小数精度(number/decimal类型)',
    min_val DECIMAL(18,6) DEFAULT NULL COMMENT '最小值',
    max_val DECIMAL(18,6) DEFAULT NULL COMMENT '最大值',
    textarea_rows INT DEFAULT 3 COMMENT '文本域行数(textarea类型)',
    
    -- ====== 字典/数据源 ======
    data_key VARCHAR(50) DEFAULT NULL COMMENT '字典编号(select/treeSelect/cascader使用), 如: org_status',
    remote_url VARCHAR(255) DEFAULT NULL COMMENT '远程数据源URL(remote模式时使用)',
    
    -- ====== 业务控制 ======
    group_index INT DEFAULT 0 COMMENT 
        '工作流阶段分组: 0=所有阶段 1=申请阶段 2=审核阶段 9=系统字段(始终不可编辑)',
    
    -- ====== C. 搜索区配置 ======
    search_flag TINYINT DEFAULT 0 COMMENT '作为搜索条件: 0=否 1=是',
    search_title VARCHAR(100) DEFAULT '' COMMENT '搜索标签(为空则取form_title)',
    search_placeholder VARCHAR(100) DEFAULT '' COMMENT '搜索占位文本',
    search_control_type VARCHAR(20) DEFAULT NULL COMMENT '搜索控件类型(为空则取control_type)',
    search_width INT DEFAULT 180 COMMENT '搜索控件宽度(px)',
    
    -- ====== 多租户 ======
    org_code VARCHAR(50) DEFAULT '' COMMENT '机构隔离码(空=继承page_config)',
    
    -- ====== 元信息 ======
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    remark VARCHAR(500) DEFAULT '' COMMENT '备注',
    
    -- ====== 约束 ======
    UNIQUE KEY uk_page_field (page_key, field_name, org_code),
    INDEX idx_page_key (page_key),
    INDEX idx_field_name (field_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='YZH V3.0 字段级UI配置 - 控制表格列/弹窗表单/搜索条件的显示与行为';

-- 外键关联(逻辑外键,不设物理FK以灵活应对多租户场景)
-- ALTER TABLE yzh_field_config ADD CONSTRAINT fk_field_page FOREIGN KEY (page_key) REFERENCES yzh_page_config(page_key);
```

### 3.3 ER 关系图

```
┌─────────────────────┐         ┌─────────────────────────────┐
│   yzh_page_config    │ 1     N │    yzh_field_config          │
│                     │─────────│                             │
│  PK: id             │         │  PK: id                     │
│  UK: page_key+org   │         │  FK: page_key ──────────────┤
│                     │         │  UK: page_key+field+org      │
│  page_key           │         │                             │
│  page_title         │         │  field_name                 │
│  entity_name        │         │  field_alias (组件命名)       │
│  table_name         │         │                             │
│  controller_name    │         │  [A] 表格列配置:              │
│  key_field          │         │    xs_flag / column_*        │
│  dialog_width       │         │                             │
│  visible_buttons [] │         │  [B] 弹窗表单配置:            │
│  ...                │         │    bc_flag / control_type     │
│                     │         │    grid_row/col/span          │
│                     │         │    required / group_index      │
│                     │         │                             │
│                     │         │  [C] 搜索区配置:              │
│                     │         │    search_flag / search_*     │
│                     │         │                             │
│                     │         │  [D] 数据源:                  │
│                     │         │    data_key (字典编号)         │
│                     │         │    remote_url (远程URL)       │
└─────────────────────┘         └─────────────────────────────┘
```

### 3.4 配置示例数据

```sql
-- ============================================================
--  页面级配置: 认证机构管理
-- ============================================================
INSERT INTO yzh_page_config (
    page_key, page_title, entity_name, table_name, controller_name,
    key_field, dialog_width, visible_buttons
) VALUES (
    'CertificationBody', 
    '认证机构管理', 
    'CertCertificationBody', 
    'cert_certification_body', 
    'CertCertificationBody',
    'Id',
    960,
    '["add","refresh","batchDelete","columnSetting"]'
);

-- ============================================================
--  字段级配置: 表格列
-- ============================================================
INSERT INTO yzh_field_config (page_key, field_name, xs_flag, column_sxh, column_title, column_width, sortable, align) VALUES
('CertificationBody', 'Id',              0,  0, 'ID',               70,  1, 'center'),
('CertificationBody', 'CbCode',          1,  1, 'CNAS编号',        120,  1, 'center'),
('CertificationBody', 'Name',            1,  2, '机构全称',        250,  1, 'left'),
('CertificationBody', 'ShortName',       1,  3, '简称',            120,  0, 'center'),
('CertificationBody', 'Status',          1,  4, '状态',            100,  0, 'center'),
('CertificationBody', 'ContactName',     1,  5, '联系人',          100,  0, 'center'),
('CertificationBody', 'ContactPhone',    1,  6, '联系电话',        130,  0, 'center'),
('CertificationBody', 'CreateDate',      1,  7, '创建时间',        160,  1, 'center'),
('CertificationBody', 'Remark',          1,  8, '备注',            200,  0, 'left');

-- ============================================================
--  字段级配置: 弹窗表单 (Grid布局)
-- ============================================================
INSERT INTO yzh_field_config (
    page_key, field_name, bc_flag, form_title, control_type, 
    required, maxlength, grid_row, grid_col, grid_row_span, grid_col_span, 
    data_key, group_index
) VALUES
('CertificationBody', 'Code',          0, '',            'hidden',     0, 0,    0, 0, 1, 1, NULL,     0),
('CertificationBody', 'Name',          1, '机构全称',    'input',      1, 200,  0, 0, 1, 1, NULL,     0),
('CertificationBody', 'ShortName',     1, '简称',        'input',      0, 100,  0, 1, 1, 1, NULL,     0),
('CertificationBody', 'CbCode',        1, 'CNAS编号',    'input',      0, 50,   1, 0, 1, 1, NULL,     0),
('CertificationBody', 'Status',        1, '状态',        'select',     0, 0,    1, 1, 1, 1, 'org_status', 0),
('CertificationBody', 'ContactName',   1, '联系人',      'input',      0, 50,   2, 0, 1, 1, NULL,     0),
('CertificationBody', 'ContactPhone',  1, '联系电话',    'input',      0, 20,   2, 1, 1, 1, NULL,     0),
('CertificationBody', 'Remark',        1, '备注',        'textarea',   0, 1000, 3, 0, 1, 2, NULL,     0);

-- ============================================================
--  字段级配置: 搜索区
-- ============================================================
INSERT INTO yzh_field_config (
    page_key, field_name, search_flag, search_title, 
    search_placeholder, search_control_type, search_width, data_key
) VALUES
('CertificationBody', 'Name',   1, '关键词', '机构名称/简称/CNAS编号', 'input',  200, NULL),
('CertificationBody', 'Status', 1, '状态',   '',                       'select', 180, 'org_status');
```

### 3.5 配置字段速查表

#### 页面级 (`yzh_page_config`) 字段

| 字段 | 含义 | 典型值 | 影响范围 |
|------|------|--------|---------|
| `page_key` | 页面唯一标识 | `'CertificationBody'` | 全局引用 |
| `visible_buttons` | 工具栏按钮列表 | `["add","refresh",...]` | YzhToolbar 渲染 |
| `dialog_width/max_height` | 弹窗尺寸 | `960` / `'85vh'` | YzhEditDialog |
| `search_mode` | 搜索模式 | `'fixed'` / `'togglable'` | YzhSearchBar |

#### 字段级 (`yzh_field_config`) 分组速查

| 分组 | 核心字段 | 含义 | 影响组件 |
|------|---------|------|---------|
| **A.表格列** | `xs_flag` | 列显隐 | YzhDataTable |
| | `column_sxh` | 列排序 | YzhDataTable |
| | `column_width/fixed` | 列宽/固定 | YzhDataTable |
| **B.弹窗表单** | `bc_flag` | 是否保存到 DB | 保存时过滤 |
| | `control_type` | 控件类型 | YzhFormField |
| | `grid_row/col/span` | Grid 定位 | YzhFormGrid |
| | `group_index` | 阶段分组 | YzhFormField disabled |
| | `required` | 必填 | 校验规则 |
| **C.搜索区** | `search_flag` | 可搜索 | YzhSearchBar |
| **D.数据源** | `data_key` | 字典编号 | 自动加载选项 |
| | `remote_url` | 远程 URL | 远程搜索 |

#### `control_type` 完整枚举

| 值 | 渲染为 | 适用场景 |
|----|--------|---------|
| `input` | `<el-input>` | 文本、名称、编号 |
| `textarea` | `<el-input textarea>` | 备注、描述 |
| `select` | `<el-select>` + 字典 | 状态、类型、枚举 |
| `number` | `<el-input-number>` | 整数数量 |
| `decimal` | `<el-input-number>` + precision | 金额、精度小数 |
| `date` | `<el-date-picker>` | 日期 |
| `switch` | `<el-switch>` | 开关、布尔 |
| `cascader` | `<el-cascader>` | 级联选择（省市区） |
| `treeSelect` | `<el-tree-select>` | 树形选择 |
| `file` | `<el-upload>` | 文件上传 |
| `img` | `<el-upload picture-card>` | 图片上传 |
| `slot` | `<slot :name="field">` | 自定义插槽（特殊字段） |
| `hidden` | 不渲染 | 内部计算字段 |

#### `visible_buttons` 完整枚举

| 按钮标识 | 显示文本 | 图标 | 说明 |
|---------|---------|------|------|
| `add` | 新增 | Plus | 打开新增弹窗 |
| `refresh` | 刷新 | RefreshRight | 重新加载数据 |
| `export` | 导出 | Download | 导出当前查询结果 |
| `import` | 导入 | Upload | 上传文件导入数据 |
| `batchDelete` | 删除 | Delete | 批量删除选中行 |
| `columnSetting` | 列设置 | Setting | 列筛选/排序面板 |
| `custom1` ~ `customN` | 自定义 | — | 业务自定义按钮 |

---

## 四、组件详细设计

### 4.1 YzhCrudTable —— 单表 CRUD 主组件

**职责**: 组装所有子组件，协调数据流，暴露对外接口。

**Props**:

```typescript
interface IYzhCrudTableProps {
  // ====== 必填 ======
  schema: IYZHEntitySchema           // 实体元信息
  
  // ====== 配置来源（二选一） ======
  configKey?: string                 // [推荐] 从数据库加载的页面 key
  options?: () => any                // [兼容] 本地 options 函数（V2.0 方式）
  
  // ====== 生命周期 ======
  lifecycles?: Partial<IYZHPageLifecycle>
  
  // ====== 功能开关 ======
  incrementalUpdate?: boolean        // 增量刷新（默认 true）
  searchMode?: 'fixed' | 'togglable' | 'hidden'
  showActionColumn?: boolean         // 操作列（默认 true）
  dialogMaxHeight?: string | number  // 弹窗最大高度（默认 '85vh'）
  
  // ====== 工作流阶段 ======
  currentPhase?: number              // 当前所处阶段（控制 GroupIndex 字段显隐）
  
  // ====== 外部过滤（左树右表场景） ======
  externalFilter?: IYZHExternalFilter[]
}
```

**Slots（插槽）**:

| 插槽名 | 位置 | 参数 | 用途 |
|--------|------|------|------|
| `toolbarLeft` | 工具栏左侧 | `{ selectedRow, selectedRows, editMode }` | 自定义业务按钮 |
| `toolbarRight` | 工具栏右侧 | 同上 | 列设置等 |
| `gridHeader` | 表格上方 | 无 | 提示信息、统计卡片 |
| `gridFooter` | 表格下方 | 无 | 汇总行、底部操作 |
| `modelHeader` | 弹窗标题下方 | `{ editForm, action }` | 弹窗内顶部提示 |
| `modelBody` | 弹窗表单上方 | `{ editForm }` | 嵌入自定义组件 |
| `modelFooter` | 弹窗表单下方 | `{ editForm }` | 附加操作按钮 |
| `modelExtra` | 弹窗底部按钮左侧 | 无 | 自定义按钮 |

**Expose（对外方法）**:

```typescript
interface IYzhCrudTableExpose {
  table: Ref<InstanceType<typeof ElTable>>
  selectedRow: any                    // 当前行
  selectedRows: any[]                 // 多选行
  refresh(): void                     // 刷新列表
  search(): void                      // 执行查询
  getData(): any[]                    // 获取当前数据
  getApi(): YZHBaseApiClient         // 获取 API 客户端实例
}
```

### 4.2 YzhEditDialog —— 编辑弹窗

**职责**: 独立的弹窗容器，内部包含 YzhFormGrid，处理打开/关闭/滚动逻辑。

**关键设计**:
- 最大高度 `85vh`（或通过 prop 自定义），超出部分 `overflow-y: auto`
- 内容区域独立滚动（header/footer 固定）
- 支持 `destroy-on-close` 或缓存模式

```
┌──────────────────────────────────────┐
│  ×  新增认证机构管理            [□] [×]│  ← 固定 header
├──────────────────────────────────────┤
│  <slot #modelHeader />              │  ← 可选插槽
│  ┌────────────────────────────────┐  │
│  │                                │  │
│  │     YzhFormGrid (可滚动区域)    │  │  ← overflow-y: auto
│  │     - Row 0: 机构全称 | 简称    │  │
│  │     - Row 1: CNAS编号 | 状态    │  │
│  │     - Row 2: 联系人 | 联系电话  │  │
│  │     - Row 3: 备注(跨2列)       │  │
│  │     - Row 4: ...               │  │
│  │     - Row N: ...               │  │  ← 超出高度后滚动
│  │                                │  │
│  └────────────────────────────────┘  │
│  <slot #modelBody />                │  ← 可选插槽
├──────────────────────────────────────┤
│  <slot #modelFooter />              │  ← 可选插槽
│              [取消]  [保存]          │  ← 固定 footer
└──────────────────────────────────────┘
```

### 4.3 YzhFormGrid —— CSS Grid 表单布局引擎

**职责**: 根据 `grid_row/grid_col/grid_row_span/grid_col_span` 配置，精确排列表单项。

**核心原理**:
```css
.yzh-form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.yzh-form-grid__item {
  /* 根据 grid_row / grid_col 动态设置 */
  grid-row: {{ item.gridRow + 1 }} / span {{ item.gridRowSpan }};
  grid-column: {{ item.gridCol + 1 }} / span {{ item.gridColSpan }};
}
```

**布局效果示例**:
```
colSpan=1  colSpan=1
┌──────────┬──────────┐
│ 机构全称  │   简称    │  Row 0
├──────────┼──────────┤
│ CNAS编号  │   状态 ▼  │  Row 1
├──────────┴──────────┤
│     备注（colSpan=2）│  Row 2
└─────────────────────┘
```

### 4.4 YzhFormField —— 单个表单字段渲染器

**职责**: 根据 `control_type` 渲染对应的 Element Plus 控件，统一处理 v-model、disabled、字典加载。

**渲染映射**:
```typescript
const controlMap = {
  input:     () => <el-input ... />,
  textarea:  () => <el-input type="textarea" ... />,
  select:    () => <el-select><el-option v-for="opt in dictData" .../></el-select>,
  number:    () => <el-input-number ... />,
  decimal:   () => <el-input-number :precision="precision" ... />,
  date:      () => <el-date-picker ... />,
  switch:    () => <el-switch ... />,
  cascader:  () => <el-cascader :options="cascaderData" ... />,
  treeSelect:() => <el-tree-select :data="treeData" ... />,
  file:      => <el-upload ... />,
  img:       => <el-upload list-type="picture-card" ... />,
}
```

**GroupIndex 阶段控制**:
```typescript
// 字段禁用逻辑
const isDisabled = computed(() => {
  if (!props.currentPhase) return false
  if (field.groupIndex === 0) return false  // 全阶段可见
  if (field.groupIndex === 9) return true   // 系统字段始终不可编辑
  return field.groupIndex !== props.currentPhase
})
```

### 4.5 YzhSelectPicker —— 通用表格选择器弹窗

**对标**: BlazorServer 的 `FrmSelect<T>`

**用途**: 当选项数据量大（>50条）或需要多选/搜索时，替代 el-select 下拉框。

**接口设计**:
```typescript
interface IYzhSelectPickerProps {
  visible: boolean                    // 受控显示
  title?: string                      // 弹窗标题
  width?: number \| string            // 弹窗宽度（默认 800px）
  multiple?: boolean                  // 是否多选（默认 false）
  columns: IColumnConfig[]            // 表格列配置
  dataSource: any[]                   // 数据源
  rowKey: string                      // 行唯一标识字段
}

interface IYzhSelectPickerEmits {
  (e: 'confirm', selected: any): void       // 单选确认
  (e: 'confirmMulti', selected: any[]): void // 多选确认
  (e: 'cancel'): void                       // 取消
}
```

**使用场景**:
- 申请单 → 选择认证机构（从几十家机构中选）
- 审核任务 → 分配审核员（从审核员列表中选）
- 明细表 → 选择物料/产品

### 4.6 YzhTreePicker —— 通用树形选择器弹窗

**对标**: BlazorServer 的 `FrmTree`

**接口设计**:
```typescript
interface IYzhTreePickerProps {
  visible: boolean
  title?: string
  width?: number \| string             // 默认 600px
  multiple?: boolean                  // 多选模式
  checkable?: boolean                 // 显示 checkbox
  dataSource: TreeData[]              // 树形数据
  fieldNames?: {                    // 字段映射
    label?: string                    // 默认 'label'
    value?: string                    // 默认 'value'
    children?: string                 // 默认 'children'
  }
  searchable?: boolean               // 搜索过滤
}
```

### 4.7 其他子组件简述

| 组件 | 职责 | 从哪里拆出 |
|------|------|-----------|
| **YzhSearchBar** | 搜索条件区域（inline form + 查询/重置按钮） | YzhCrudTable template 第 13-60 行 |
| **YzhToolbar** | 工具栏按钮组（新增/刷新/导入/导出/删除 + 右侧 slot） | YzhCrudTable template 第 62-90 行 |
| **YzhDataTable** | el-table 封装（列渲染、排序、选择、行事件） | YzhCrudTable template 第 92-150 行 |
| **YzhPagination** | el-pagination 封装 | YzhCrudTable template 第 152-165 行 |
| **YzhColumnSettings** | 列筛选/排序 Popover 面板 | YzhCrudTable template 第 168-200 行 |

### 4.8 精确可控设计 —— 按钮/列/字段的 ref 与 watch

#### 设计思想

所有动态生成的 UI 元素（工具栏按钮、表格列、表单字段）都必须有**明确的命名标识**，使得业务代码可以：

1. 通过 `ref` **精准操控**任意组件实例（调用方法、修改属性）
2. 通过 `watch` **精准监听**任意字段值变化（触发联动逻辑）
3. 通过 `expose` **精准暴露**内部状态供外部读取

#### 4.8.1 工具栏按钮精确控制

每个按钮有固定的 `buttonKey` 标识，通过 `YzhToolbar` 的 expose 可获取：

```typescript
// YzhToolbar 内部维护的按钮注册表
const buttonInstances = reactive(new Map<string, IButtonInstance>())

// 按钮标识 → 实例映射
interface IButtonInstance {
  key: string                    // 'add' / 'refresh' / 'custom1' / ...
  visible: Ref<boolean>          // 控制显隐
  disabled: Ref<boolean>         // 控制禁用
  loading: Ref<boolean>         // 控制加载态
  onClick: () => void            // 触发点击
}
```

**业务页面使用方式**：

```vue
<template>
  <YzhCrudTable 
    ref="crudRef"
    :schema="schema" 
    :config-key="'CertificationBody'"
    :visible-buttons="['add','refresh','batchDelete','audit']"  <!-- 自定义显示哪些按钮 -->
  >
    <template #toolbarLeft>
      <!-- 也可以用插槽自定义，但推荐用下面的方式 -->
    </template>
  </YzhCrudTable>
</template>

<script setup>
const crudRef = ref()

onMounted(() => {
  const toolbar = crudRef.value.getToolbar()
  
  // 精准控制某个按钮
  toolbar.getButton('add').disabled = false        // 启用新增按钮
  toolbar.getButton('batchDelete').visible = false   // 隐藏批量删除
  
  // 自定义按钮: audit (审批)
  toolbar.registerButton({
    key: 'audit',
    label: '审批',
    icon: 'Select',
    type: 'warning',
    onClick: () => handleAudit()
  })
  
  // 监听按钮事件
  toolbar.onButtonClick('add', () => {
    console.log('新增按钮被点击')
  })
})
</script>
```

**内置按钮完整列表**：

| buttonKey | 默认文本 | 默认图标 | 说明 |
|----------|---------|---------|------|
| `add` | 新增 | Plus | 打开新增弹窗 |
| `refresh` | 刷新 | RefreshRight | 重新加载数据 |
| `export` | 导出 | Download | 导出当前查询结果 |
| `import` | 导入 | Upload | 上传文件导入数据 |
| `batchDelete` | 删除 | Delete | 批量删除选中行 |
| `columnSetting` | 列设置 | Setting | 列筛选/排序面板 |
| `custom1` ~ `customN` | 自定义 | — | 业务自定义 |

#### 4.8.2 表格列精确控制

每列通过 `field_alias`（默认同 `field_name`）作为唯一标识：

```typescript
// YzhDataTable 内部
const columnRefs = reactive(new Map<string, IColumnInstance>())

interface IColumnInstance {
  fieldAlias: string              // 列标识
  visible: Ref<boolean>           // 显隐控制
  width: Ref<number>             // 动态改宽度
  formatter?: Function           // 自定义格式化器
  headerSlot?: string             # 使用自定义表头插槽
}

// Expose 方法
function getColumn(alias: string): IColumnInstance | undefined { ... }
function setColumnVisible(alias: string, visible: boolean): void { ... }
```

**使用示例**：
```javascript
onMounted(() => {
  const table = crudRef.value.getTable()
  
  // 隐藏某列
  table.setColumnVisible('CreateDate', false)
  
  // 动态修改列宽
  const col = table.getColumn('Name')
  if (col) col.width.value = 300
  
  // 自定义列渲染
  table.getColumn('Status').formatter = (row) => {
    return h('el-tag', { type: row.Status === 'active' ? 'success' : 'info' }, row._statusText)
  }
})
```

#### 4.8.3 表单字段精确控制

每个表单字段通过 `field_alias`（默认同 `field_name`）作为唯一标识：

```typescript
// YzhFormGrid / YzhFormField 内部
const fieldRefs = reactive(new Map<string, IFieldInstance>())

interface IFieldInstance {
  fieldAlias: string               // 字段标识
  value: Ref<any>                  // 响应式值引用（可读写）
  disabled: Ref<boolean>          // 禁用状态
  readonly: Ref<boolean>          // 只读状态
  visible: Ref<boolean>           // 显隐
  validate: () => Promise<boolean>// 触发校验
  focus: () => void                // 聚焦
  reset: () => void                // 重置为默认值
  componentRef: Ref<any>           // 底层组件 ref（el-input/el-select 等）
}
```

**使用示例 —— ref 精准操控**：
```javascript
onMounted(() => {
  const dialog = crudRef.value.getEditDialog()
  
  // 获取字段实例
  const statusField = dialog.getField('Status')
  const nameField = dialog.getField('Name')
  
  // 精准操控：禁用状态字段
  if (statusField) statusField.disabled.value = true
  
  // 精准操控：设置只读
  if (nameField) nameField.readonly.value = true
  
  // 精准操控：聚焦到某个字段
  dialog.getField('CbCode')?.focus()
  
  // 精准操控：程序化设值（会触发 v-model 更新）
  dialog.getField('Remark')?.value.value = '自动填充的备注'
})

// 精准操控：动态切换必填规则
function onPhaseChange(phase) {
  const dialog = crudRef.value.getEditDialog()
  const opinionField = dialog.getField('AuditOpinion')
  if (opinionField) {
    // 审核阶段时，审核意见变为必填
    opinionField.required = phase === 2
  }
}
```

**使用示例 —— watch 字段联动**：
```vue
<script setup>
const crudRef = ref()

onMounted(() => {
  const dialog = crudRef.value.getEditDialog()
  
  // ====== 场景1: 选择机构后自动填充编号 ======
  dialog.watchField('ent_code', async (newValue) => {
    if (newValue) {
      const ent = await api.getEnterprise(newValue)
      dialog.getField('CreditCode')?.value.value = ent.CreditCode
      dialog.getField('LegalPerson')?.value.value = ent.LegalPerson
    }
  })
  
  // ====== 场景2: 选择标准后联动更新费用 ======
  dialog.watchField('StandardCode', async (newValue) => {
    if (newValue) {
      const std = await api.getStandard(newValue)
      dialog.getField('AuditFee')?.value.value = std.BaseFee
    }
  })
  
  // ====== 场景3: 状态变更后联动其他字段 ======
  dialog.watchField('Status', (newValue) => {
    if (newValue === 'cancelled') {
      // 取消时清空审核信息
      dialog.getField('AuditOpinion')?.value.value = ''
      dialog.getField('AuditDate')?.value.value = null
      dialog.getField('AuditOpinion')?.disabled.value = true
    }
  })
  
  // ====== 场景4: 数字字段实时计算 ======
  dialog.watchField('Quantity', (newQty) => {
    const price = dialog.getField('UnitPrice')?.value ?? 0
    dialog.getField('Amount')?.value.value = newQty * price
  })
  dialog.watchField('UnitPrice', (newPrice) => {
    const qty = dialog.getField('Quantity')?.value ?? 0
    dialog.getField('Amount')?.value.value = qty * newPrice
  })
})
</script>
```

#### 4.8.4 命名规范总结

| 元素类型 | 命名字段 | 命名来源 | 默认值 | 获取方式 |
|---------|---------|---------|--------|---------|
| **工具栏按钮** | `buttonKey` | `visible_buttons` 数组中的值 | `'add'`, `'refresh'`, `'custom1'...` | `getToolbar().getButton(key)` |
| **表格列** | `field_alias` | `yzh_field_config.field_alias` | 同 `field_name` | `getTable().getColumn(alias)` |
| **表单字段** | `field_alias` | `yzh_field_config.field_alias` | 同 `field_name` | `getEditDialog().getField(alias)` |

> **重要**: `field_alias` 的核心价值在于——当数据库字段名为 `CbCode` 但你想在代码中用更语义化的名称（如 `certBodyCode`）时，可以通过配置 `field_alias='certBodyCode'` 来实现解耦。

---

## 五、配置加载流程（✅ 已实现，2026-08-07 更新）

> **重要**: 以下为**实际已实现的架构**。原设计文档中的类式 `YZHConfigLoader` 已简化为函数式模块 + Vuex Store 模式。

### 5.1 实际数据流架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        用户登录成功                               │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│  Index.vue onMounted → store.dispatch('yzhConfig/init')         │
│    ├─ 1. RESTORE_FROM_CACHE: 从 localStorage 恢复旧配置          │
│    └─ 2. refresh: GET /api/yzh-page-config/all (JWT鉴权)        │
│           └─ 后端 YzhPageConfigService.GetAllConfigsFullAsync() │
│              ├─ 查 yzh_page_config (所有活跃页面)                │
│              ├─ 查 yzh_field_config (所有字段配置)               │
│              └─ 版本号 = MAX(updated_at)                         │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│  yzhConfig.js Store (Vuex)                                       │
│    state.configs = {                                             │
│      'CertificationBody': { pageMeta: {...}, fieldConfigs: [...] },│
│      'ISOStandard': { pageMeta: {...}, fieldConfigs: [...] }     │
│    }                                                             │
│    + localStorage 持久化                                         │
└──────────────────────────┬──────────────────────────────────────┘
                           │
           ┬               │               ┬
           ▼               ▼               ▼
┌────────────────┐ ┌──────────────┐ ┌────────────────┐
│ Certification  │ │ ISOStandard   │ │ 其他页面...     │
│ Body.vue       │ │ .vue         │ │                │
│ pageKey=       │ │ pageKey=     │ │                │
│ 'Certifica...' │ │ 'ISOStandard'│ │                │
└───────┬────────┘ └──────┬───────┘ └───────┬────────┘
        │                 │                 │
        ▼                 ▼                 ▼
┌─────────────────────────────────────────────────────────────────┐
│  YZHConfigLoader.loadPageConfig(pageKey)                        │
│    1. Vuex Store.getter → 命中? → 返回 (同步, 0ms)              │
│    2. 未命中 → 内存缓存 Map? → 返回                              │
│    3. 都没有 → HTTP API 请求 (降级)                              │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│  YzhCrudTable / YzhTreeTable                                     │
│    dbFieldConfigs → buildColumnsFromDbConfig() → columns         │
│                    buildEditFormFromDbConfig() → editFormOptions │
│                    buildSearchFormFromDbConfig() → searchOptions │
└─────────────────────────────────────────────────────────────────┘
```

### 5.2 核心文件职责

| 文件 | 类型 | 核心职责 |
|------|------|---------|
| `yzh/store/yzhConfig.js` | Vuex Module | 全量缓存、localStorage 持久化、版本号管理 |
| `yzh/core/YZHConfigLoader.ts` | TS 模块 | Store 优先加载、API 降级、数据格式转换 |
| `yzh/components/YzhCrudTable.vue` | Vue 组件 | 消费配置构建 columns/editForm/searchForm |

### 5.3 后端 API 接口

| 方法 | 路径 | 鉴权 | 说明 |
|------|------|------|------|
| `GET` | `/api/yzh-page-config/{pageKey}` | JWT | 获取单个页面配置（降级用） |
| `GET` | `/api/yzh-page-config/all` | ✅ JWT | 全量获取所有页面配置（启动时调用） |

### 5.4 安全措施

1. **JWT 鉴权**: `[JWTAuthorize]` 特性保护所有接口
2. **数据脱敏**: 仅返回 UI 渲染所需字段，不含业务数据
3. **版本号机制**: 基于 `yzh_field_config.updated_at` 生成，用于缓存失效判断

### 5.5 刷新配置按钮

位置：`Index.vue` 顶部工具栏（`<div class="h-link">` 内）
- 风格：与其他工具栏元素一致（`<a>` + `<i>` 图标）
- 功能：触发 `store.dispatch('yzhConfig/refresh')`
- 显示：当前版本号 `v{version}`
- 加载状态：图标变为 `el-icon-loading`

### 5.6 ⚠️ EF Core Column 映射注意事项

> **踩坑记录**: 数据库表使用 snake_case 列名（如 `checkbox_selection`），但 C# 实体属性是 PascalCase（如 `CheckboxSelection`）。EF Core 默认不会自动转换，必须显式添加 `[Column("snake_case")]` 特性。

**影响实体**:
- `YzhPageConfig.cs` — 23 个属性需加 `[Column]`
- `YzhFieldConfig.cs` — 30+ 个属性需加 `[Column]`

**错误现象**: `Unknown column 'y.CheckboxSelection' in 'field list'` → HTTP 400

---

## 六、生命周期钩子（完整定义）

### 6.1 钩子总览（融合 Vol + 你的架构）

```
页面加载阶段:
  onInit(vm)              → 组件创建前，可修改配置
  onInited()              → 组件创建后，DOM 已就绪

查询阶段:
  ★ onLoadBefore(param)  → 发送请求前，可改参数/返回 false 阻止  [★=新增]
  onLoadAfter(rows, raw) → 数据返回后，可二次加工

新增阶段:
  onAddBefore(formData)   → 打开弹窗前
  onAddSaveBefore(main)   → 保存前校验
  onAddSaveAfter(main, res)→ 保存后处理

编辑阶段:
  onUpdateBefore(row, formData) → 打开编辑弹窗前
  onUpdateSaveBefore(main)     → 保存前校验
  onUpdateSaveAfter(main, res)  → 保存后处理

删除阶段:
  onDeleteBefore(rows, ids) → 删除前确认
  onDeleteAfter(ids)          → 删除后刷新

弹窗阶段:
  ★ modelOpenBefore(row, action) → 弹窗打开前，返回 false 阻止  [★=新增]
  modelOpenAfter(row, action)  → 弹窗打开后，设默认值/加载数据

行交互:
  onRowClick(row)           → 点击行
  onRowDbClick(row)          → 双击行
  onRowSelect(row, rows)     → 选择变化
  onEditModeChange(editing)  → 编辑模式切换

导入导出:
  onImportBefore(formData)   → 导入前
  onImportAfter(rows)        → 导入后
  onExportBefore(param)       → 导出前
  onExportAfter(blob)         → 导出后
```

> 注：带 ★ 为相比 V2.0 新增的钩子

---

## 七、渐进式演进路线图

### Phase 1: 单表 CRUD（✅ 已完成核心功能，2026-08-07）

**目标**: 替换 V2.0 的 YzhCrudTable，支持数据库配置驱动的单表增删改查

**交付物**:
- [x] `yzh_page_config` + `yzh_field_config` 建表 SQL（双表设计）— `yzh_v3_config_tables.sql`
- [x] 后端配置 API (`YzhPageConfigController`) — 含 `GET /all` 全量加载接口
- [x] `YZHConfigLoader.ts` 配置加载器（Store 优先 → API 降级）
- [x] `yzhConfig.js` Vuex Store（全量加载 + localStorage 持久化 + 版本号机制）
- [x] 重构 `YzhCrudTable.vue` 支持 `pageKey` prop 配置驱动
- [x] 重构 `YzhTreeTable.vue` 透传 `pageKey` prop
- [x] CertificationBody 页面迁移到数据库配置驱动
- [x] ISOStandard 页面迁移到数据库配置驱动（左树右表模式）
- [x] Index.vue 顶部工具栏集成「刷新配置」按钮
- [x] JWT 鉴权保护配置接口
- [ ] 新增 `YzhEditDialog.vue`（含滚动）— **待开发**
- [ ] 新增 `YzhFormGrid.vue`（CSS Grid 布局引擎）— **待开发**
- [ ] 新增 `YzhFormField.vue` — **待开发**
- [ ] 拆分 `YzhSearchBar` / `YzhToolbar` / `YzhDataTable` / `YzhPagination` — **待开发**
- [ ] 更新 `IYZHLifecycles.ts` 新增 `onLoadBefore` / `modelOpenBefore` — **待开发**

**关键文件清单（已实现）**:

| 层级 | 文件路径 | 职责 |
|------|---------|------|
| **后端实体** | `vol.api/VOL.Entity/CertPlatform/Sys/YzhPageConfig.cs` | 页面配置实体（含 `[Column]` snake_case 映射） |
| **后端实体** | `vol.api/VOL.Entity/CertPlatform/Sys/YzhFieldConfig.cs` | 字段配置实体（含 `[Column]` snake_case 映射） |
| **后端服务** | `src/server/YZH-Framework/.../Services/YzhPageConfigService.cs` | 配置查询业务逻辑 |
| **后端接口** | `vol.api/VOL.WebApi/Controllers/CertPlatform/YzhPageConfigController.cs` | REST API（含 `[JWTAuthorize]`） |
| **前端 Store** | `vol.web/src/yzh/store/yzhConfig.js` | Vuex 全量缓存 + localStorage 持久化 |
| **前端加载器** | `vol.web/src/yzh/core/YZHConfigLoader.ts` | Store 优先 → API 降级加载策略 |
| **CRUD 组件** | `vol.web/src/yzh/components/YzhCrudTable.vue` | 支持 `pageKey` prop 的配置驱动表格 |
| **树表组件** | `vol.web/src/yzh/components/YzhTreeTable.vue` | 左树右表，透传 pageKey |
| **主界面** | `vol.web/src/views/Index.vue` | 初始化 Store + 刷新配置按钮 |
| **SQL 脚本** | `DB/mysql/yzh_v3_config_tables.sql` | 建表 + CertificationBody/ISOStandard 配置数据 |

**验收标准**:
- [x] CertificationBody 页面完全由数据库配置驱动
- [x] ISOStandard 页面完全由数据库配置驱动
- [x] 登录后自动全量加载配置到 Vuex + localStorage
- [x] 顶部工具栏「刷新配置」按钮可用
- [x] 配置接口受 JWT 保护
- [ ] 弹窗支持内部滚动（字段多时不溢出）— **待实现**
- [ ] GroupIndex 阶段控制生效 — **待实现**
- [ ] BCFlag 保存过滤生效 — **待实现**
- [ ] CSS Grid 表单布局 — **待实现**

### Phase 2: 左树右表（🔄 进行中，基础骨架已完成）

**目标**: 实现 `YzhTreeTable` 组件，对标 BlazorServer 的 `FrmBaseMain<T>`

**当前状态（2026-08-07）**:
- [x] `YzhTreeTable.vue` 基础骨架（左树 + 右表 CRUD）
- [x] ISOStandard 页面使用 YzhTreeTable（认证机构树 → ISO标准表）
- [x] 树节点点击联动表格数据过滤
- [ ] `YzhTreePanel.vue` 独立树形面板组件 — **待拆分**
- [ ] 树节点搜索/筛选功能 — **待开发**
- [ ] 树节点右键菜单 — **待开发**
- [ ] 树节点新增/编辑/删除操作 — **待开发**

**预览**:
```
┌─────────────────────────────────────────────────────┐
│  <YzhTreeTable                                     │
│    :tree-config="treeConfig"                       │
│    :table-config="tableConfig"                      │
│    :tree-options="{ searchable: true }"              │
│  >                                                  │
│    <template #treeExtra>...</template>               │
│    <template #tableToolbar>...</template>             │
│  </YzhTreeTable>                                    │
└─────────────────────────────────────────────────────┘
```

**交付物**（待 Phase 1 完成后展开）:
- [ ] `YzhTreeTable.vue` 左树右表骨架
- [ ] `YzhTreePanel.vue` 树形面板（含搜索/新增/右键菜单）
- [ ] 树-表联动逻辑（选中节点 → 过滤表格）

### Phase 3: 选择器组件库（并行或 Phase 2 后）

**交付物**:
- [ ] `YzhSelectPicker.vue` 表格选择器
- [ ] `YzhTreePicker.vue` 树形选择器
- [ ] `YzhInputPicker.vue` 文本输入器

### Phase 4: 高级特性（远期）

- [ ] 主从表支持（detail 配置）
- [ ] 工作流审核面板
- [ ] 远程字典 / 级联选择
- [ ] 可视化表单设计器页面
- [ ] 配置变更实时推送（WebSocket）

---

## 八、与 V2.0 的兼容性策略

### 8.1 双模式共存

V3.0 不强制废弃 V2.0 的 `options.js` 方式，两者并存：

```typescript
// 方式 A: 数据库配置驱动（V3.0 推荐）
<YzhCrudTable :schema="schema" :config-key="'CertificationBody'" />

// 方式 B: 本地 options 函数（V2.0 兼容）
<YzhCrudTable :schema="schema" :options="viewOptions" />
```

内部判断逻辑：
```typescript
const uiConfig = props.configKey
  ? await configLoader.loadConfig(props.configKey)  // 从 DB 加载
  : transformOptions(props.options())                    // 从函数转换
```

### 8.2 迁移路径

```
现有页面 (V2.0 options.js)  →  中间态 (同时支持两种方式)  →  最终态 (纯 DB 配置)
     │                          │                            │
     ▼                          ▼                            ▼
 options.js              options.js + DB         纯 DB 配置
 + crud-table             crud-table              crud-table
 (当前方式)               (双模式共存)           (目标方式)
```

---

## 九、待决策事项

以下事项需要审核确认后才能进入编码：

| # | 问题 | 建议 | 状态 |
|---|------|------|------|
| D1 | 配置表是否需要多语言支持（title 字段国际化）？ | 暂不需要，后续可加 `i18n_key` 字段 | 待确认 |
| D2 | 缓存策略：前端内存缓存 vs Pinia store？ | 前端内存缓存 + 可手动刷新 | 待确认 |
| D3 | 配置修改后：刷新页面生效 vs WebSocket 实时推送？ | 先做刷新生效，后期加 WS | 待确认 |
| D4 | `yzh/` 目录是否需要迁移到独立 npm 包？ | 暂时放在项目内，成熟后提取 | 待确认 |
| D5 | 弹窗内字段过多时是否需要 Tab 分组？ | 先做滚动，Tab 作为后续优化 | 待确认 |
| D6 | 后端配置管理界面是否纳入本次范围？ | 否，先手动 SQL 操作，后期做管理页面 | 待确认 |

---

## 十、附录

### A. 参考架构

| 来源 | 借鉴点 |
|------|--------|
| BlazorServer XML 配置 | Grid 布局、BCFlag、GroupIndex、FrmSelect/FrmTree |
| Vol ViewGrid | 15+ 钩子、.jsx 扩展、字典机制、主从表、弹窗自适应尺寸 |
| Ant Design Pro | 路由配置、权限指令 |

### B. 现有文件清单（V2.0 已有，V3.0 将改造）

| 文件 | 状态 | V3.0 处理方式 |
|------|------|-------------|
| `types/YZHEntitySchema.ts` | 保留 | 可能新增 `tableName?` 等字段 |
| `types/YZHLifecycles.ts` | 扩展 | 新增 `onLoadBefore` / `modelOpenBefore` |
| `types/YZHPageProps.ts` | 扩展 | 新增 `configKey` / `currentPhase` / `dialogMaxHeight` |
| `core/YZHBaseApiClient.ts` | 保留 | 无需改动 |
| `core/YZHEditGuard.ts` | 保留 | 无需改动 |
| `core/YZHPageLifecycle.ts` | 扩展 | 新增钩子的 runGuard 逻辑 |
| `core/YZHRowDiff.ts` | 保留 | 无需改动 |
| `composables/useYZHEditMode.ts` | 保留 | 无需改动 |
| `composables/useYZHIncrementSync.ts` | 保留 | 无需改动 |
| `presets/defaultButtons.ts` | 保留 | 无需改动 |
| `components/YzhCrudTable.vue` | **重写** | 改为组合模式，消费子组件 |
| `index.ts` | 更新 | 导出新组件 |

---

> **文档结束** — 请审核以上内容，确认或修改后即可进入编码阶段。
