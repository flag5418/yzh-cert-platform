-- YZH V3.0 完整初始化
DROP TABLE IF EXISTS yzh_field_config;
DROP TABLE IF EXISTS yzh_page_config;

CREATE TABLE yzh_page_config (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    page_key VARCHAR(50) NOT NULL, page_title VARCHAR(100) NOT NULL,
    entity_name VARCHAR(100) NOT NULL, table_name VARCHAR(100) NOT NULL,
    controller_name VARCHAR(100) NOT NULL, key_field VARCHAR(50) DEFAULT 'Id',
    key_field_type VARCHAR(10) DEFAULT 'number', sort_field VARCHAR(50),
    sort_order VARCHAR(5) DEFAULT 'desc', dialog_width INT DEFAULT 960,
    dialog_max_height VARCHAR(20) DEFAULT '85vh', dialog_label_width INT DEFAULT 120,
    row_height VARCHAR(10) DEFAULT 'default', stripe TINYINT DEFAULT 1,
    show_row_number TINYINT DEFAULT 1, search_mode VARCHAR(10) DEFAULT 'fixed',
    visible_buttons TEXT, show_action_column TINYINT DEFAULT 1,
    checkbox_selection TINYINT DEFAULT 1, incremental_update TINYINT DEFAULT 1,
    org_code VARCHAR(50) DEFAULT '', is_active TINYINT DEFAULT 1,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    remark VARCHAR(500) DEFAULT '',
    UNIQUE KEY uk_page_org (page_key, org_code), INDEX idx_page_key (page_key)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE yzh_field_config (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    page_key VARCHAR(50) NOT NULL, field_name VARCHAR(50) NOT NULL,
    field_alias VARCHAR(100) DEFAULT '', xs_flag TINYINT DEFAULT 1,
    column_sxh INT DEFAULT 0, column_title VARCHAR(100) DEFAULT '',
    column_width INT DEFAULT 120, column_fixed VARCHAR(10) DEFAULT NULL,
    sortable TINYINT DEFAULT 1, column_formatter VARCHAR(50) DEFAULT '',
    show_overflow TINYINT DEFAULT 1, align VARCHAR(10) DEFAULT 'left',
    bc_flag TINYINT DEFAULT 1, form_title VARCHAR(100) DEFAULT '',
    control_type VARCHAR(20) DEFAULT 'input',
    grid_row INT DEFAULT 0, grid_col INT DEFAULT 0,
    grid_row_span INT DEFAULT 1, grid_col_span INT DEFAULT 1,
    required TINYINT DEFAULT 0, maxlength INT DEFAULT 0,
    placeholder VARCHAR(200) DEFAULT '', default_value VARCHAR(500) DEFAULT '',
    readonly TINYINT DEFAULT 0, disabled TINYINT DEFAULT 0,
    `precision` INT DEFAULT NULL, min_val DECIMAL(18,6) DEFAULT NULL,
    max_val DECIMAL(18,6) DEFAULT NULL, textarea_rows INT DEFAULT 3,
    data_key VARCHAR(50) DEFAULT NULL, remote_url VARCHAR(255) DEFAULT NULL,
    group_index INT DEFAULT 0,
    search_flag TINYINT DEFAULT 0, search_title VARCHAR(100) DEFAULT '',
    search_placeholder VARCHAR(100) DEFAULT '',
    search_control_type VARCHAR(20) DEFAULT NULL, search_width INT DEFAULT 180,
    org_code VARCHAR(50) DEFAULT '',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    remark VARCHAR(500) DEFAULT '',
    UNIQUE KEY uk_page_field (page_key, field_name, org_code),
    INDEX idx_page_key (page_key), INDEX idx_field_name (field_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO yzh_page_config (page_key,page_title,entity_name,table_name,controller_name,key_field,dialog_width,dialog_label_width,visible_buttons,search_mode,stripe,show_row_number,checkbox_selection,is_active,remark) VALUES
('CertificationBody','认证机构管理','CertCertificationBody','cert_certification_body','CertCertificationBody','Id',800,110,'["add","refresh","batchDelete","columnSetting"]','fixed',1,1,1,1,'YZH V3.0 第一个试点窗体');

INSERT INTO yzh_field_config (page_key,field_name,field_alias,xs_flag,column_sxh,column_title,column_width,column_fixed,sortable,column_formatter,show_overflow,align,bc_flag,form_title,control_type,required,maxlength,placeholder,default_value,readonly,disabled,grid_row,grid_col,grid_row_span,grid_col_span,`precision`,min_val,max_val,textarea_rows,data_key,remote_url,group_index,search_flag,search_title,search_placeholder,search_control_type,search_width) VALUES
('CertificationBody','Id','Id',0,0,'ID',70,NULL,1,'',1,'center',0,'','hidden',0,0,'','',0,0,0,0,1,1,NULL,NULL,NULL,3,NULL,NULL,9,0,'','',NULL,180),
('CertificationBody','CbCode','CbCode',1,1,'CNAS编号',130,NULL,1,'',1,'center',1,'CNAS编号','input',0,50,'请输入CNAS认可编号','',0,0,1,0,1,1,NULL,NULL,NULL,3,NULL,NULL,0,0,'','',NULL,180),
('CertificationBody','Name','Name',1,2,'机构全称',250,NULL,1,'',1,'left',1,'机构全称','input',1,200,'请输入机构全称','',0,0,0,0,1,1,NULL,NULL,NULL,3,NULL,NULL,0,1,'关键词','机构名称/简称/CNAS编号','input',240),
('CertificationBody','ShortName','ShortName',1,3,'简称',120,NULL,0,'',1,'center',1,'简称','input',0,100,'请输入简称','',0,0,0,1,1,1,NULL,NULL,NULL,3,NULL,NULL,0,0,'','',NULL,180),
('CertificationBody','Status','Status',1,4,'状态',100,NULL,0,'',1,'center',1,'状态','select',0,0,'','',0,0,1,1,1,1,NULL,NULL,NULL,3,'org_status',NULL,0,1,'状态','','select',160),
('CertificationBody','ContactName','ContactName',1,5,'联系人',100,NULL,0,'',1,'center',1,'联系人','input',0,50,'请输入联系人','',0,0,2,0,1,1,NULL,NULL,NULL,3,NULL,NULL,0,0,'','',NULL,180),
('CertificationBody','ContactPhone','ContactPhone',1,6,'联系电话',140,NULL,0,'',1,'center',1,'联系电话','input',0,20,'请输入联系电话','',0,0,2,1,1,1,NULL,NULL,NULL,3,NULL,NULL,0,0,'','',NULL,180),
('CertificationBody','CreateDate','CreateDate',1,7,'创建时间',170,NULL,1,'',1,'center',0,'','hidden',0,0,'','',0,0,999,0,1,1,NULL,NULL,NULL,3,NULL,NULL,9,0,'','',NULL,180),
('CertificationBody','Remark','Remark',0,8,'备注',200,NULL,0,'',1,'left',1,'备注','textarea',0,500,'请输入备注信息','',0,0,3,0,1,2,NULL,NULL,NULL,3,NULL,NULL,0,0,'','',NULL,180);
