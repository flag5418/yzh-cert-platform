import re
import os
import glob

def fix_sql_script(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()

    # Define the mapping from snake_case to PascalCase
    mapping = {
        'id': 'Id',
        'code': 'Code',
        'org_code': 'OrgCode',
        'create_id': 'CreateID',
        'create_by': 'CreateID',
        'creator': 'Creator',
        'create_date': 'CreateDate',
        'create_time': 'CreateDate',
        'modify_id': 'ModifyID',
        'update_by': 'ModifyID',
        'modifier': 'Modifier',
        'modify_date': 'ModifyDate',
        'update_time': 'ModifyDate',
        'delete_id': 'DeleteID',
        'delete_by': 'DeleteID',
        'deleter': 'Deleter',
        'delete_time': 'DeleteTime',
        'status': 'Status',
        'enable': 'Enable',
        'sort': 'Sort',
        'remark': 'Remark',
        'name': 'Name',
        'short_name': 'ShortName',
        'cb_code': 'CbCode',
        'contact_name': 'ContactName',
        'contact_phone': 'ContactPhone',
        'standard_code': 'StandardCode',
        'standard_name': 'StandardName',
        'version_year': 'VersionYear',
        'parent_code': 'ParentCode',
        'clause_number': 'ClauseNumber',
        'title': 'Title',
        'description': 'Description',
        'sort_order': 'SortOrder',
        'phase_code': 'PhaseCode',
        'phase_name': 'PhaseName',
        'enterprise_name': 'Name',
        'unified_social_credit_code': 'CreditCode',
        'contact_person': 'ContactName',
        'industry_type': 'IndustryType',
        'employee_count': 'EmployeeCount',
        'application_no': 'ApplicationNo',
        'enterprise_code': 'EnterpriseCode',
        'cert_type': 'CertType',
        'scope_text': 'ScopeText',
        'submit_time': 'SubmitTime',
        'accept_time': 'AcceptTime',
        'complete_time': 'CompleteTime',
        'project_no': 'ProjectNo',
        'application_code': 'ApplicationCode',
        'current_phase': 'CurrentPhase',
        'project_manager_id': 'ProjectManagerId',
        'planned_start_date': 'PlannedStartDate',
        'planned_end_date': 'PlannedEndDate',
        'actual_end_date': 'ActualEndDate',
        'notes': 'Remark',
    }

    new_content = content
    # Replace using regex to match word boundaries and handle backticks
    for snake, pascal in mapping.items():
        # Replace backticked: `cb_code` -> `CbCode`
        new_content = new_content.replace(f'`{snake}`', f'`{pascal}`')
        # Replace non-backticked word: cb_code -> CbCode (only if not part of another word)
        # We use regex for this
        pattern = re.compile(r'\b' + re.escape(snake) + r'\b')
        new_content = pattern.sub(pascal, new_content)

    if new_content != content:
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
        print(f"Fixed {file_path}")
    else:
        print(f"No changes needed for {file_path}")

if __name__ == "__main__":
    sql_dir = '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/DB/mysql/'
    sql_files = glob.glob(os.path.join(sql_dir, '*.sql'))

    for sql_file in sql_files:
        fix_sql_script(sql_file)
