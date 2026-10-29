#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
清洗国标文本 → 标准化 Markdown
规则：
  1. 去除 ===PAGE N=== 分页符
  2. 合并连续空行为单空行
  3. 去除行首尾多余空格
  4. OCR 常见错误校对映射
  5. 加 YAML front matter
  6. 保存为 UTF-8
"""

import re
import os

# ── OCR 常见错误校对表（19001/42061） ──
OCR_CORRECTIONS = {
    '碗定': '确定',
    '输人': '输入',
    '这过': '这个过程',
    '因索': '因素',
    '顾容': '顾客',
    '文施': '文件',
    '符介': '符合',
    '为式': '方式',
    '应川': '应用',
    '质象': '质量',
    '管删': '管理',
    '设评': '设计',
    '成文信': '成文信息',
    '成文信总': '成文信息',
    '总指': '总的',
    '贯到': '贯穿',
    '并成': '并非',
    '相大': '相关',
    '第于': '基于',
    '别是': '特别是',
    '的的': '的',
    '以的': '以下的',
    '针划': '策划',
    '效测': '效果',
    '业已': '已经',
    '并人': '并不',
    '人仅': '不仅',
    '人此': '此外',
    '为生': '为生',
    '为应': '应为',
    '个理': '管理',
    '个文': '文件',
    '个业': '企业',
    '成川': '而成',
}

def clean_text(text, frontmatter):
    """清洗并生成 Markdown"""
    # 1. 去分页符
    text = re.sub(r'===PAGE\s*\d+===', '', text)
    # 2. 去行首尾空格
    lines = [l.strip() for l in text.split('\n')]
    # 3. 合并连续空行
    result = []
    prev_empty = False
    for line in lines:
        if not line:
            if not prev_empty:
                result.append('')
            prev_empty = True
        else:
            prev_empty = False
            result.append(line)
    text = '\n'.join(result)
    # 4. OCR 校对
    for wrong, right in OCR_CORRECTIONS.items():
        text = text.replace(wrong, right)
    # 5. 去首尾空行
    text = text.strip()
    # 6. 加 front matter
    return frontmatter + '\n\n' + text

# ── 配置：4 个标准 ──
STANDARDS = [
    {
        'src': '../gbt_extract/gbt19001_ocr3.txt',
        'dst': 'iso9001-2016.md',
        'frontmatter': (
            '---\n'
            'standard_code: GB/T 19001-2016\n'
            'standard_name: 质量管理体系 要求\n'
            'idt_iso: ISO 9001:2015\n'
            'version_year: 2016\n'
            'impl_date: 2017-05-01\n'
            'source: temp/gbt_extract/gbt19001_ocr3.txt\n'
            'cleaned: 2026-10-28\n'
            '---'
        ),
    },
    {
        'src': '../gbt_extract/gbt24001.txt',
        'dst': 'iso14001-2016.md',
        'frontmatter': (
            '---\n'
            'standard_code: GB/T 24001-2016\n'
            'standard_name: 环境管理体系 要求及使用指南\n'
            'idt_iso: ISO 14001:2015\n'
            'version_year: 2016\n'
            'impl_date: 2017-05-01\n'
            'source: temp/gbt_extract/gbt24001.txt\n'
            'cleaned: 2026-10-28\n'
            '---'
        ),
    },
    {
        'src': '../gbt_extract/gbt45001.txt',
        'dst': 'iso45001-2020.md',
        'frontmatter': (
            '---\n'
            'standard_code: GB/T 45001-2020\n'
            'standard_name: 职业健康安全管理体系 要求及使用指南\n'
            'idt_iso: ISO 45001:2018\n'
            'version_year: 2020\n'
            'impl_date: 2020-03-06\n'
            'source: temp/gbt_extract/gbt45001.txt\n'
            'cleaned: 2026-10-28\n'
            '---'
        ),
    },
    {
        'src': '../gbt_extract/gbt42061_ocr3.txt',
        'dst': 'iso13485-2016.md',
        'frontmatter': (
            '---\n'
            'standard_code: GB/T 42061-2022\n'
            'standard_name: 医疗器械 质量管理体系 用于法规的要求\n'
            'idt_iso: ISO 13485:2016\n'
            'version_year: 2022\n'
            'impl_date: 2022-10-01\n'
            'source: temp/gbt_extract/gbt42061_ocr3.txt\n'
            'cleaned: 2026-10-28\n'
            '---'
        ),
    },
]

base_dir = os.path.dirname(os.path.abspath(__file__))

for std in STANDARDS:
    src_path = os.path.join(base_dir, std['src'])
    dst_path = os.path.join(base_dir, std['dst'])
    # 读源文件
    with open(src_path, 'r', encoding='utf-8') as f:
        raw = f.read()
    # 清洗
    markdown = clean_text(raw, std['frontmatter'])
    # 写出
    with open(dst_path, 'w', encoding='utf-8') as f:
        f.write(markdown)
    # 统计
    lines = markdown.count('\n') + 1
    chars = len(markdown)
    print(f"✅ {std['dst']} — {lines} 行 / {chars} 字符")

print("\n全部清洗完成！")
