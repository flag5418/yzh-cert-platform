import os
import re

paths = [
    '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/admin/src/views/cert/',
    '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/views/cert/'
]

def process_file(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # 1. Rename Notes to Remark
    # Match "Notes:" in objects
    content = content.replace('Notes:', 'Remark:')
    # Match "field: 'Notes'"
    content = content.replace("field: 'Notes'", "field: 'Remark'")
    
    # 2. Fix sortName: 'id' to 'Id'
    content = content.replace("sortName: 'id'", "sortName: 'Id'")
    
    # 3. Ensure other audit fields are PascalCase if they appear (just in case)
    replacements = {
        "field: 'create_time'": "field: 'CreateDate'",
        "field: 'create_by'": "field: 'CreateID'",
        "field: 'update_time'": "field: 'ModifyDate'",
        "field: 'update_by'": "field: 'ModifyID'",
    }
    for old, new in replacements.items():
        content = content.replace(old, new)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)

for base_path in paths:
    if not os.path.exists(base_path): continue
    for root, dirs, files in os.walk(base_path):
        for file in files:
            if file == 'options.js':
                process_file(os.path.join(root, file))

print("Frontend options.js files updated successfully.")
