-- ============================================================
-- YZH 架构 - 接口权限表结构
-- 创建日期: 2026-09-10
-- 说明: 用于接口权限自动同步和权限校验
-- ============================================================

-- 1. 接口表
CREATE TABLE IF NOT EXISTS sys_api (
    id          BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    code        VARCHAR(64) NOT NULL COMMENT '接口编码（SHA256 Hash）',
    method      VARCHAR(10) NOT NULL COMMENT 'HTTP方法: GET/POST/PUT/DELETE',
    path        VARCHAR(200) NOT NULL COMMENT '接口路径',
    tree_path   VARCHAR(500) COMMENT '树形路径（如 System|User|filter）',
    name        VARCHAR(200) NOT NULL COMMENT '接口名称',
    author      VARCHAR(50) COMMENT '负责人',
    enable      TINYINT(1) DEFAULT 1 COMMENT '是否启用',
    create_date DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    update_date DATETIME ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    UNIQUE INDEX uk_api_code (code),
    INDEX idx_api_path (path),
    INDEX idx_api_tree (tree_path)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='接口表';

-- 2. 角色-接口关联表
CREATE TABLE IF NOT EXISTS sys_role_api (
    id          BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    role_code   VARCHAR(50) NOT NULL COMMENT '角色编码',
    api_code    VARCHAR(64) NOT NULL COMMENT '接口编码',
    create_date DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    UNIQUE INDEX uk_role_api (role_code, api_code),
    INDEX idx_api_code (api_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='角色-接口关联表';

-- 3. 用户权限缓存表（角色-接口关联的展开表）
CREATE TABLE IF NOT EXISTS sys_user_permission (
    id          BIGINT PRIMARY KEY AUTO_INCREMENT COMMENT '主键ID',
    user_code   VARCHAR(36) NOT NULL COMMENT '用户编码',
    api_code    VARCHAR(64) NOT NULL COMMENT '接口编码',
    create_date DATETIME DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    update_date DATETIME ON UPDATE CURRENT_TIMESTAMP COMMENT '更新时间',
    UNIQUE INDEX uk_user_api (user_code, api_code),
    INDEX idx_user_code (user_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='用户权限缓存表';

-- 4. 初始化数据（超级管理员拥有所有接口权限）
-- 注意：此脚本应在接口同步完成后执行
-- INSERT INTO sys_role_api (role_code, api_code)
-- SELECT 'superadmin', code FROM sys_api;
