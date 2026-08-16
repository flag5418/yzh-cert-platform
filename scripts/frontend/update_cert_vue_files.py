import os
import re

paths = [
    '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/admin/src/views/cert/',
    '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/views/cert/'
]

mapping = {
    'status': 'Status',
    'cb_code': 'CbCode',
    'short_name': 'ShortName',
    'task_number': 'TaskNumber',
    'phase_type': 'PhaseCode', # Based on options.js
    'auditor_id': 'AuditorId',
    'planned_date': 'PlannedDate',
    'actual_start_date': 'ActualStartDate',
    'actual_complete_date': 'ActualCompleteDate',
    'audit_scope': 'AuditScope',
    'notes': 'Remark',
    'remark': 'Remark',
    'create_time': 'CreateDate',
    'create_by': 'CreateID',
    'update_time': 'ModifyDate',
    'update_by': 'ModifyID',
    'delete_time': 'DeleteTime',
    'delete_by': 'DeleteID',
    'archive_date': 'ArchiveDate',
    'standard_code': 'StandardCode',
    'standard_name': 'StandardName',
    'version_year': 'VersionYear',
    'credit_code': 'CreditCode',
    'legal_person': 'LegalPerson',
    'contact_name': 'ContactName',
    'contact_phone': 'ContactPhone',
    'contact_email': 'ContactEmail',
    'task_code': 'Code', # Based on AuditTask.vue logic
}

def process_file(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # Replace .field access
    for old, new in mapping.items():
        # Match .status, .cb_code etc
        content = re.sub(rf'\.({old})\b', f'.{new}', content)
        # Match field: 'status' or name: 'status'
        content = re.sub(rf"(['\"]){old}\1", f"\\1{new}\\1", content)
    
    # Special fix for sortName = 'id' to 'Id'
    content = content.replace("sortName = 'id'", "sortName = 'Id'")
    content = content.replace("sortName: 'id'", "sortName: 'Id'")

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)

for base_path in paths:
    if not os.path.exists(base_path): continue
    for root, dirs, files in os.walk(base_path):
        for file in files:
            if file.endswith('.vue') or file == 'options.js':
                process_file(os.path.join(root, file))

print("Vue and JS files in cert directory updated to PascalCase.")
