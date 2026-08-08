-- ============================================================
--  YZH V3.0 认证机构管理 — 数据库配置（全字段覆盖版）
--
--  实体：CertificationBody : YZHBaseEntity
--  表名：cert_certification_body
--  业务字段：Name, ShortName, CbCode, ContactName, ContactPhone
--  基类字段（YZHBaseEntity）：Id, Code, OrgCode, Status, Remark, Enable,
--          CreateID, Creator, CreateDate, ModifyID, Modifier, ModifyDate,
--          DeleteID, Deleter, DeleteTime, Sort
-- ============================================================

-- ========== 1. 页面级配置 ==========
INSERT INTO yzh_page_config (
    page_key, page_title, entity_name, table_name, controller_name,
    key_field, key_field_type, sort_field, sort_order,
    dialog_width, dialog_max_height, dialog_label_width,
    row_height, stripe, show_row_number, search_mode,
    visible_buttons, show_action_column, checkbox_selection,
    incremental_update, is_active, remark
) VALUES (
    'CertificationBody',
    '认证机构管理',
    'CertCertificationBody',
    'cert_certification_body',
    'CertCertificationBody',
    'Id',
    'number',
    'CreateDate',
    'desc',
    800,
    '80vh',
    110,
    'default',
    1,
    1,
    'fixed',
    '["add","refresh","batchDelete","columnSetting"]',
    1,
    1,
    1,
    1,
    'YZH V3.0 单表 CRUD 试点窗体 — 认证机构管理（全字段配置驱动）'
) ON DUPLICATE KEY UPDATE
    remark = VALUES(remark),
    updated_at = CURRENT_TIMESTAMP;

-- ========== 2. 字段级配置（全字段覆盖）==========

-- ---- Group 0: 系统隐藏字段 ----
INSERT INTO yzh_field_config (page_key, field_name, xs_flag, column_sxh, column_title, column_width, sortable, align, bc_flag, form_title, control_type, required, maxlength, placeholder, default_value, grid_row, grid_col, grid_col_span, group_index, search_flag) VALUES
('CertificationBody', 'Id',         0, 0,  '',      0,   1, 'center', 1, '',      'hidden',  0, 0,  '',    '',        0, 0, 1, 0, 0),
('CertificationBody', 'Code',       0, 0,  '',      0,   1, 'center', 1, '',      'hidden',  0, 0,  '',    '',        0, 0, 1, 0, 0),
('CertificationBody', 'OrgCode',    0, 0,  '',      0,   1, 'center', 1, '',      'hidden',  0, 0,  '',    '',        0, 0, 1, 0, 0),
('CertificationBody', 'Enable',     0, 0,  '',      0,   1, 'center', 1, '',      'hidden',  0, 0,  '',    'true',    0, 0, 1, 0, 0)
ON DUPLICATE KEY UPDATE
    xs_flag = VALUES(xs_flag), control_type = VALUES(control_type),
    bc_flag = VALUES(bc_flag), group_index = VALUES(group_index),
    updated_at = CURRENT_TIMESTAMP;

-- ---- Group 1: 业务录入字段 ----
INSERT INTO yzh_field_config (page_key, field_name, xs_flag, column_sxh, column_title, column_width, sortable, align, bc_flag, form_title, control_type, required, maxlength, placeholder, default_value, grid_row, grid_col, grid_col_span, data_key, group_index, search_flag, search_title, search_placeholder, search_control_type, search_width) VALUES
-- Name: 机构全称（必填）
('CertificationBody', 'Name',         1, 2,  '机构全称', 250, 1, 'left',   1, '机构全称', 'input',   1, 200, '请输入机构全称', '', 1, 0, 2, NULL, 0, 1, '关键词', '机构名称/简称/CNAS编号', 'input', 240),

-- ShortName: 简称
('CertificationBody', 'ShortName',    1, 3,  '简称',     120, 0, 'center', 1, '简称',     'input',   0, 100, '请输入简称',       '', 1, 1, 1, NULL, 0, 0, '',  '', NULL, 180),

-- CbCode: CNAS编号
('CertificationBody', 'CbCode',       1, 1,  'CNAS编号', 130, 1, 'center', 1, 'CNAS编号', 'input',   0, 50,  '请输入CNAS认可编号','', 2, 0, 1, NULL, 0, 0, '',  '', NULL, 180),

-- Status: 状态（字典选择）
('CertificationBody', 'Status',       1, 4,  '状态',     100, 0, 'center', 1, '状态',     'select',  0, 0,  '',  'active',  2, 1, 1, 'org_status', 0, 1, '状态', '', 'select', 160),

-- ContactName: 联系人
('CertificationBody', 'ContactName',  1, 5,  '联系人',   100, 0, 'center', 1, '联系人',   'input',   0, 50,  '请输入联系人',    '', 3, 0, 1, NULL, 0, 0, '',  '', NULL, 180),

-- ContactPhone: 联系电话
('CertificationBody', 'ContactPhone', 1, 6,  '联系电话', 140, 0, 'center', 1, '联系电话', 'input',   0, 20,  '请输入联系电话',  '', 3, 1, 1, NULL, 0, 0, '',  '', NULL, 180),

-- Remark: 备注（整行）
('CertificationBody', 'Remark',       0, 7,  '备注',     200, 0, 'left',   1, '备注',     'textarea',0, 500, '请输入备注信息',  '', 4, 0, 2, NULL, 0, 0, '',  '', NULL, 180)
ON DUPLICATE KEY UPDATE
    xs_flag = VALUES(xs_flag), column_sxh = VALUES(column_sxh),
    column_title = VALUES(column_title), control_type = VALUES(control_type),
    required = VALUES(required), placeholder = VALUES(placeholder),
    default_value = VALUES(default_value), data_key = VALUES(data_key),
    grid_row = VALUES(grid_row), grid_col = VALUES(grid_col), grid_col_span = VALUES(grid_col_span),
    group_index = VALUES(group_index),
    search_flag = VALUES(search_flag), search_title = VALUES(search_title),
    updated_at = CURRENT_TIMESTAMP;

-- ---- Group 9: 审计字段（readonly/none）----
INSERT INTO yzh_field_config (page_key, field_name, xs_flag, column_sxh, column_title, column_width, sortable, align, bc_flag, form_title, control_type, required, maxlength, placeholder, grid_row, grid_col, grid_col_span, group_index, search_flag) VALUES
('CertificationBody', 'CreateID',   0, 10, '创建人ID', 0,   1, 'center', 0, '',    'none',     0, 0, '', 999, 0, 1, 9, 0),
('CertificationBody', 'Creator',    0, 11, '创建人',   100, 0, 'center', 0, '创建人','readonly', 0, 50, '', 999, 0, 1, 9, 0),
('CertificationBody', 'CreateDate', 1, 12, '创建时间', 160, 1, 'center', 0, '创建时间','readonly',0, 0, '', 999, 0, 1, 9, 0),
('CertificationBody', 'ModifyID',   0, 13, '修改人ID', 0,   1, 'center', 0, '',    'none',     0, 0, '', 999, 0, 1, 9, 0),
('CertificationBody', 'Modifier',   0, 14, '修改人',   100, 0, 'center', 0, '修改人','readonly', 0, 50, '', 999, 0, 1, 9, 0),
('CertificationBody', 'ModifyDate', 0, 15, '修改时间', 160, 1, 'center', 0, '修改时间','readonly',0, 0, '', 999, 0, 1, 9, 0),
('CertificationBody', 'DeleteID',   0, 16, '删除人ID', 0,   1, 'center', 0, '',    'none',     0, 0, '', 999, 0, 1, 9, 0),
('CertificationBody', 'Deleter',    0, 17, '删除人',   100, 0, 'center', 0, '删除人','readonly', 0, 50, '', 999, 0, 1, 9, 0),
('CertificationBody', 'DeleteTime', 0, 18, '删除时间', 160, 1, 'center', 0, '删除时间','readonly',0, 0, '', 999, 0, 1, 9, 0),
('CertificationBody', 'Sort',       0, 19, '排序号',   80,  1, 'center', 1, '排序号','hidden',   0, 0, '', '0', 0, 0, 1, 9, 0)
ON DUPLICATE KEY UPDATE
    xs_flag = VALUES(xs_flag), column_sxh = VALUES(column_sxh),
    column_title = VALUES(column_title), control_type = VALUES(control_type),
    bc_flag = VALUES(bc_flag), group_index = VALUES(group_index),
    updated_at = CURRENT_TIMESTAMP;

-- ========== 验证查询 ==========
SELECT
    fc.field_name AS 字段名,
    fc.control_type AS 录入类型,
    CASE fc.xs_flag WHEN 1 THEN '✓' ELSE '—' END AS 表格,
    CASE fc.bc_flag WHEN 1 THEN '✓' ELSE '—' END AS 保存,
    CASE fc.search_flag WHEN 1 THEN '✓' ELSE '—' END AS 搜索,
    fc.column_title AS 列标题,
    fc.form_title AS 表单标题,
    fc.group_index AS 分组,
    fc.data_key AS 字典
FROM yzh_field_config fc
WHERE fc.page_key = 'CertificationBody'
ORDER BY fc.group_index, fc.column_sxh;
