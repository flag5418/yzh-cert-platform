-- ============================================================
-- Sys_Role 添加 ParentCode 字段（支持树形结构）
-- 日期：2026-09-10
-- ============================================================

-- 添加 ParentCode 列
ALTER TABLE Sys_Role ADD COLUMN ParentCode varchar(64) NULL COMMENT '父节点编码（树结构，根节点为 NULL）' AFTER Code;

-- 为现有数据填充 ParentCode（根据 ParentId 关联）
UPDATE Sys_Role child
  INNER JOIN Sys_Role parent ON child.ParentId = parent.Role_Id
  SET child.ParentCode = parent.Code
  WHERE child.ParentId > 0;

-- 添加索引
CREATE INDEX idx_role_parent_code ON Sys_Role(ParentCode);
