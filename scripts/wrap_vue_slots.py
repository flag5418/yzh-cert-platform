import os
import re

paths = [
    '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/admin/src/views/cert/',
    '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/vol.web/src/views/cert/'
]

# Additional mappings for specific fields found in vue files
mapping = {
    'application_no': 'ApplicationNo',
    'application_code': 'ApplicationCode',
    'phase_name': 'PhaseName',
    'standard_code': 'StandardCode',
    'standard_name': 'StandardName',
}

def wrap_grid_header(content):
    # Match <template #gridHeader>...</template>
    pattern = r'(<template\s+#[Gg]ridHeader>)([\s\S]*?)(</template>)'
    
    def replacer(match):
        inner = match.group(2).strip()
        if inner.startswith('<div>') and inner.endswith('</div>'):
            return match.group(0)
        return f'{match.group(1)}\n      <div>\n        {inner}\n      </div>\n    {match.group(3)}'
    
    return re.sub(pattern, replacer, content)

def process_file(file_path):
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    # 1. Field mappings
    for old, new in mapping.items():
        content = re.sub(rf'\.({old})\b', f'.{new}', content)
        content = re.sub(rf"(['\"]){old}\1", f"\\1{new}\\1", content)
        content = re.sub(rf"\b({old}):\s*", f"{new}: ", content)
    
    # 2. Wrap gridHeader in div to avoid parentNode null error
    if '.vue' in file_path:
        content = wrap_grid_header(content)

    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(content)

for base_path in paths:
    if not os.path.exists(base_path): continue
    for root, dirs, files in os.walk(base_path):
        for file in files:
            if file.endswith('.vue') or file == 'options.js':
                process_file(os.path.join(root, file))

print("Vue files slots wrapped and fields updated.")
