import re

file_path = '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/DB/mysql/cert_platform_tables_v2.1.sql'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Standard base fields block
base_fields = """    `OrgCode` varchar(50) DEFAULT NULL COMMENT '组织编码',
    `CreateID` int DEFAULT NULL COMMENT '创建人ID',
    `Creator` nvarchar(50) DEFAULT NULL COMMENT '创建人姓名',
    `CreateDate` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `ModifyID` int DEFAULT NULL COMMENT '修改人ID',
    `Modifier` nvarchar(50) DEFAULT NULL COMMENT '修改人姓名',
    `ModifyDate` datetime DEFAULT NULL COMMENT '修改时间',
    `DeleteID` int DEFAULT NULL COMMENT '删除人ID',
    `Deleter` nvarchar(50) DEFAULT NULL COMMENT '删除人姓名',
    `DeleteTime` datetime DEFAULT NULL COMMENT '删除时间',
    `Status` varchar(50) DEFAULT 'active' COMMENT '业务状态',
    `Enable` tinyint DEFAULT 1 COMMENT '启用状态',
    `Sort` int DEFAULT 0 COMMENT '排序号',
    `Remark` nvarchar(500) DEFAULT NULL COMMENT '备注',"""

# Correct regex to match the table definition and base fields
# Match: CREATE TABLE `...` ( followed by any content until -- 业务字段
def clean_table(match):
    table_name = match.group(1)
    return f"CREATE TABLE `{table_name}` (\n    -- 基类字段\n    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT '主键ID',\n    `Code` varchar(36) NOT NULL COMMENT '全局唯一编码（GUID）',\n{base_fields}\n\n    -- 业务字段"

# This pattern matches from CREATE TABLE up to -- 业务字段
pattern = re.compile(r'CREATE TABLE `(\w+)` \(\s+-- 基类字段\s+`Id` bigint NOT NULL AUTO_INCREMENT COMMENT \'主键ID\',\s+`Code` varchar\(36\) NOT NULL COMMENT \'全局唯一编码（GUID）\',.*?\n\s+-- 业务字段', re.DOTALL)

# But wait, my previous run already messed up some lines. I should fix the "table_name\n    `OrgCode`" part too.
# Match: cert_clause_extraction_rule\n    `OrgCode`
messed_up_pattern = re.compile(r'(\w+)\n\s+`OrgCode` varchar\(50\) DEFAULT NULL COMMENT \'组织编码\',', re.MULTILINE)
content = messed_up_pattern.sub(r'CREATE TABLE `\1` (\n    -- 基类字段\n    `Id` bigint NOT NULL AUTO_INCREMENT COMMENT \'主键ID\',\n    `Code` varchar(36) NOT NULL COMMENT \'全局唯一编码（GUID）\',\n    `OrgCode` varchar(50) DEFAULT NULL COMMENT \'组织编码\',', content)

# Now apply the clean_table pattern again to be sure
cleaned_content = pattern.sub(clean_table, content)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(cleaned_content)

print("SQL script cleaned successfully (v2).")
