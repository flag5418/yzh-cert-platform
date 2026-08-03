import re

file_path = '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/DB/mysql/cert_platform_tables_v2.1.sql'

def to_pascal_case(snake_str):
    if not snake_str: return ""
    if snake_str.lower() == 'id': return 'Id'
    if snake_str.lower() == 'code': return 'Code'
    mapping = {
        'create_time': 'CreateDate',
        'create_by': 'CreateID',
        'update_time': 'ModifyDate',
        'update_by': 'ModifyID',
        'delete_time': 'DeleteTime',
        'delete_by': 'DeleteID',
        'notes': 'Remark',
        'remark': 'Remark',
        'status': 'Status',
        'enable': 'Enable',
        'sort': 'Sort'
    }
    if snake_str.lower() in mapping:
        return mapping[snake_str.lower()]
        
    components = snake_str.split('_')
    # Use capitalize() instead of title() to avoid lowercasing existing caps if any, 
    # though snake_case shouldn't have them. 
    # Actually x[0].upper() + x[1:] is safest.
    return "".join(x[0].upper() + x[1:] for x in components if x)

with open(file_path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

base_fields = ['Id', 'Code', 'OrgCode', 'CreateID', 'Creator', 'CreateDate', 'ModifyID', 'Modifier', 'ModifyDate', 'DeleteID', 'Deleter', 'DeleteTime', 'Status', 'Enable', 'Sort', 'Remark']

new_lines = []
current_table_fields = set()
in_business_fields = False
in_base_fields = False

for line in lines:
    if 'CREATE TABLE' in line:
        current_table_fields = set()
        new_lines.append(line)
        continue
        
    if '-- 基类字段' in line:
        in_base_fields = True
        new_lines.append(line)
        continue
        
    if '-- 业务字段' in line:
        in_base_fields = False
        in_business_fields = True
        new_lines.append(line)
        continue

    if in_base_fields:
        match = re.search(r'^\s+`(\w+)`', line)
        if match:
            current_table_fields.add(match.group(1))
        new_lines.append(line)
        continue

    if in_business_fields:
        if 'PRIMARY KEY' in line or ');' in line:
            in_business_fields = False
            new_lines.append(line)
            continue
            
        match = re.search(r'^\s+`(\w+)`', line)
        if match:
            old_name = match.group(1)
            new_name = to_pascal_case(old_name)
            
            if new_name in base_fields or new_name in current_table_fields:
                continue
            
            line = line.replace(f'`{old_name}`', f'`{new_name}`')
            current_table_fields.add(new_name)
            
    new_lines.append(line)

with open(file_path, 'w', encoding='utf-8') as f:
    f.writelines(new_lines)

print("SQL script cleaned and converted successfully (fixed casing).")
