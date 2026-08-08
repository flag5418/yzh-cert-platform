-- ============================================================
--  YZH V3.0 ISO 标准管理 — 数据库配置（全字段覆盖版）
--
--  架构设计原则：
--  1. 覆盖实体所有字段（含基类 YZHBaseEntity 的审计字段）
--  2. control_type 控制弹窗录入方式：
--     input/select/textarea/number → 显示为对应控件，参与保存
--     hidden → 不显示但随表单提交保存（如 Id, CbCode）
--     readonly → 显示但只读不保存（后端自动填充的审计字段）
--     none → 不显示也不保存（纯展示/计算字段）
--  3. xs_flag 控制表格列是否显示
--  4. bc_flag 控制是否持久化到数据库
--  5. search_flag 控制搜索区是否出现
--  6. 前端 options.js 只保留最小化元数据
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
    'ISOStandard',
    'ISO 标准管理',
    'ISOStandard',
    'cert_iso_standard',
    'ISOStandard',
    'Id',
    'number',
    'CreateDate',
    'desc',
    800,
    '80vh',
    120,
    'default',
    1,
    1,
    'fixed',
    '["add","refresh","batchDelete","columnSetting"]',
    1,
    1,
    1,
    1,
    'YZH V3.0 左树右表模式 — 认证机构 → ISO标准（全字段配置驱动）'
) ON DUPLICATE KEY UPDATE
    page_title = VALUES(page_title),
    remark = VALUES(remark),
    updated_at = CURRENT_TIMESTAMP;

-- ========== 2. 字段级配置（全字段覆盖）==========
-- 
-- ISOStandard 实体字段：Id, Code, OrgCode, CbCode, StandardCode, StandardName, VersionYear, Status, Remark, Enable
-- YZHBaseEntity 基类字段：CreateID, Creator, CreateDate, ModifyID, Modifier, ModifyDate, DeleteID, Deleter, DeleteTime, Sort
--
-- 字段分组说明：
--   Group 0: 系统隐藏字段（Id, Code, OrgCode, Enable）
--   Group 1: 业务录入字段（CbCode→hidden自动填充, StandardCode, StandardName, VersionYear, Status, Remark）
--   Group 9: 审计只读字段（CreateDate 等，后端填充）

-- ---- Group 0: 系统隐藏字段（hidden = 不在弹窗显示但参与保存）----
INSERT INTO yzh_field_config (page_key, field_name, xs_flag, column_sxh, column_title, column_width, sortable, align, bc_flag, form_title, control_type, required, maxlength, placeholder, default_value, grid_row, grid_col, grid_col_span, group_index, search_flag) VALUES
('ISOStandard', 'Id',         0, 0,  '',      0,   1, 'center', 1, '',      'hidden',  0, 0,  '',    '',          0, 0, 1, 0, 0),
('ISOStandard', 'Code',       0, 0,  '',      0,   1, 'center', 1, '',      'hidden',  0, 0,  '',    '',          0, 0, 1, 0, 0),
('ISOStandard', 'OrgCode',    0, 0,  '',      0,   1, 'center', 1, '',      'hidden',  0, 0,  '',    '',          0, 0, 1, 0, 0),
('ISOStandard', 'Enable',     0, 0,  '',      0,   1, 'center', 1, '',      'hidden',  0, 0,  '',    'true',      0, 0, 1, 0, 0)
ON DUPLICATE KEY UPDATE
    xs_flag = VALUES(xs_flag), control_type = VALUES(control_type),
    bc_flag = VALUES(bc_flag), group_index = VALUES(group_index),
    updated_at = CURRENT_TIMESTAMP;

-- ---- Group 1: 业务录入字段 ----
INSERT INTO yzh_field_config (page_key, field_name, xs_flag, column_sxh, column_title, column_width, sortable, align, bc_flag, form_title, control_type, required, maxlength, placeholder, default_value, grid_row, grid_col, grid_col_span, min_val, max_val, data_key, group_index, search_flag, search_title, search_placeholder, search_control_type, search_width) VALUES
-- CbCode: 左树右表模式 → 弹窗 hidden（由左侧机构树自动填充），表格不显示(xs_flag=0)
('ISOStandard', 'CbCode',       0, 0,  '所属机构', 130, 1, 'center', 1, '所属机构', 'hidden',  1, 36, '',        '',            0, 0, 1, NULL, NULL, NULL,                  0, 0, '',  '',  NULL, 180),

-- StandardCode: 表格显示 + 弹窗录入
('ISOStandard', 'StandardCode', 1, 1,  '标准编号', 160, 1, 'center', 1, '标准编号', 'input',   1, 50, '如：ISO 13485:2016', '',            1, 0, 1, NULL, NULL, NULL,                  0, 1, '关键词', '标准编号 / 名称', 'input', 240),

-- StandardName: 表格显示 + 弹窗录入（整行）
('ISOStandard', 'StandardName', 1, 2,  '标准名称', 280, 1, 'left',   1, '标准名称', 'input',   1, 200, '如：医疗器械质量管理体系', '', 1, 0, 2, NULL, NULL, NULL,                  0, 0, '',  '',  NULL, 180),

-- VersionYear: 表格显示 + 弹窗数字输入
('ISOStandard', 'VersionYear',  1, 3,  '版本',     80,  0, 'center', 1, '版本年份', 'number',  0, 0,  '',        '2026',        1, 1, 1, 1990, 2100, NULL,                  0, 0, '',  '',  NULL, 180),

-- Status: 表格显示 + 弹窗下拉选择
('ISOStandard', 'Status',       1, 4,  '状态',     100, 0, 'center', 1, '状态',     'select',  0, 0,  '',        'draft',       1, 1, 1, NULL, NULL, 'standard_status', 0, 1, '状态', '', 'select', 160),

-- Remark: 表格可选显示 + 弹窗多行文本（整行）
('ISOStandard', 'Remark',       0, 5,  '备注',     200, 0, 'left',   1, '备注',     'textarea',0, 500,'请输入备注信息', '',            2, 0, 2, NULL, NULL, NULL,                  0, 0, '',  '',  NULL, 180)
ON DUPLICATE KEY UPDATE
    xs_flag = VALUES(xs_flag), column_sxh = VALUES(column_sxh),
    column_title = VALUES(column_title), control_type = VALUES(control_type),
    required = VALUES(required), placeholder = VALUES(placeholder),
    default_value = VALUES(default_value), data_key = VALUES(data_key),
    min_val = VALUES(min_val), max_val = VALUES(max_val),
    grid_row = VALUES(grid_row), grid_col = VALUES(grid_col), grid_col_span = VALUES(grid_col_span),
    group_index = VALUES(group_index),
    search_flag = VALUES(search_flag), search_title = VALUES(search_title),
    updated_at = CURRENT_TIMESTAMP;

-- ---- Group 9: 审计字段（readonly = 只读展示，不参与编辑保存，后端自动填充）----
INSERT INTO yzh_field_config (page_key, field_name, xs_flag, column_sxh, column_title, column_width, sortable, align, bc_flag, form_title, control_type, required, maxlength, placeholder, grid_row, grid_col, grid_col_span, group_index, search_flag) VALUES
('ISOStandard', 'CreateID',   0, 10, '创建人ID', 0,   1, 'center', 0, '',    'none', 0, 0, '', 999, 0, 1, 9, 0),
('ISOStandard', 'Creator',    0, 11, '创建人',   100, 0, 'center', 0, '创建人','readonly',0, 50, '', 999, 0, 1, 9, 0),
('ISOStandard', 'CreateDate', 1, 12, '创建时间', 160, 1, 'center', 0, '创建时间','readonly',0, 0, '', 999, 0, 1, 9, 0),
('ISOStandard', 'ModifyID',   0, 13, '修改人ID', 0,   1, 'center', 0, '',    'none', 0, 0, '', 999, 0, 1, 9, 0),
('ISOStandard', 'Modifier',   0, 14, '修改人',   100, 0, 'center', 0, '修改人','readonly',0, 50, '', 999, 0, 1, 9, 0),
('ISOStandard', 'ModifyDate', 0, 15, '修改时间', 160, 1, 'center', 0, '修改时间','readonly',0, 0, '', 999, 0, 1, 9, 0),
('ISOStandard', 'DeleteID',   0, 16, '删除人ID', 0,   1, 'center', 0, '',    'none', 0, 0, '', 999, 0, 1, 9, 0),
('ISOStandard', 'Deleter',    0, 17, '删除人',   100, 0, 'center', 0, '删除人','readonly',0, 50, '', 999, 0, 1, 9, 0),
('ISOStandard', 'DeleteTime', 0, 18, '删除时间', 160, 1, 'center', 0, '删除时间','readonly',0, 0, '', 999, 0, 1, 9, 0),
('ISOStandard', 'Sort',       0, 19, '排序号',   80,  1, 'center', 1, '排序号','hidden', 0, 0, '', '0', 0, 0, 1, 9, 0)
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
WHERE fc.page_key = 'ISOStandard'
ORDER BY fc.group_index, fc.column_sxh;
