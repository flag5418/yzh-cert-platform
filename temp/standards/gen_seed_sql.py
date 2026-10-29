#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
读取清洗后的 Markdown 文件，生成 INSERT SQL
使用 LOAD_FILE 方式避免转义问题（文件需在 MySQL secure_file_priv 允许目录）
或使用 Python pymysql 直接连接执行 —— 这里直接连接执行
"""

import os
import sys
import pymysql

base_dir = os.path.dirname(os.path.abspath(__file__))
root_dir = os.path.dirname(base_dir)  # 项目根目录

# ── 4 条种子数据 ──
SEEDS = [
    {
        'code': 'ISO9001-2016',
        'standard_code': 'c9d3e1f5-2a68-4b7c-8e04-5a1b7f3d9c62',
        'standard_name': 'GB/T 19001-2016 质量管理体系 要求',
        'md_file': 'iso9001-2016.md',
        'sort': 1,
        'remark': 'GB/T 19001-2016 / ISO 9001:2015，2026-10-28 清洗入库',
    },
    {
        'code': 'ISO14001-2016',
        'standard_code': 'd2f7a9c4-6e13-4a58-b9d7-3c8e1f5a2b46',
        'standard_name': 'GB/T 24001-2016 环境管理体系 要求及使用指南',
        'md_file': 'iso14001-2016.md',
        'sort': 2,
        'remark': 'GB/T 24001-2016 / ISO 14001:2015，2026-10-28 清洗入库',
    },
    {
        'code': 'ISO45001-2020',
        'standard_code': 'e5a1c8b3-9d47-4f26-a3e8-7b1c4d9f6a02',
        'standard_name': 'GB/T 45001-2020 职业健康安全管理体系 要求及使用指南',
        'md_file': 'iso45001-2020.md',
        'sort': 3,
        'remark': 'GB/T 45001-2020 / ISO 45001:2018，2026-10-28 清洗入库',
    },
    {
        'code': 'ISO13485-2016',
        'standard_code': 'dfca5483-f83c-4999-b773-3624de5f7cf7',
        'standard_name': 'ISO 13485 医疗器械质量管理体系',
        'md_file': 'iso13485-2016.md',
        'sort': 4,
        'remark': 'GB/T 42061-2022 / ISO 13485:2016，2026-10-28 清洗入库',
    },
]

def main():
    # 连接 MySQL（通过 docker 容器网络或直接 localhost:3307）
    conn = pymysql.connect(
        host='127.0.0.1',
        port=3307,
        user='root',
        password='Yzh123456.',
        database='yzh_cert_platform',
        charset='utf8mb4',
    )
    cur = conn.cursor()
    cur.execute("SET NAMES utf8mb4 COLLATE utf8mb4_general_ci")

    inserted = 0
    skipped = 0

    for seed in SEEDS:
        md_path = os.path.join(base_dir, seed['md_file'])
        with open(md_path, 'r', encoding='utf-8') as f:
            markdown = f.read()

        # 检查是否已存在
        cur.execute(
            "SELECT COUNT(*) FROM cert_doc_extract_rules WHERE Code = %s",
            (seed['code'],)
        )
        exists = cur.fetchone()[0]

        if exists:
            # UPDATE
            cur.execute(
                """UPDATE cert_doc_extract_rules
                   SET StandardCode=%s, StandardName=%s, MarkdownContent=%s,
                       Sort=%s, Remark=%s, UpdateTime=NOW()
                   WHERE Code=%s""",
                (seed['standard_code'], seed['standard_name'], markdown,
                 seed['sort'], seed['remark'], seed['code'])
            )
            print(f"🔄 {seed['code']} — 已存在，更新 Markdown ({len(markdown)} 字符)")
            skipped += 1
        else:
            # INSERT
            cur.execute(
                """INSERT INTO cert_doc_extract_rules
                   (Code, StandardCode, StandardName, MarkdownContent,
                    Version, IsPublished, Sort, IsValid, Remark, CreateTime)
                   VALUES (%s, %s, %s, %s, 1, 0, %s, 1, %s, NOW())""",
                (seed['code'], seed['standard_code'], seed['standard_name'],
                 markdown, seed['sort'], seed['remark'])
            )
            print(f"✅ {seed['code']} — 插入成功 ({len(markdown)} 字符)")
            inserted += 1

    conn.commit()

    # 验证
    cur.execute("SELECT COUNT(*) AS total, SUM(CASE WHEN MarkdownContent IS NOT NULL THEN 1 ELSE 0 END) AS has_md FROM cert_doc_extract_rules")
    row = cur.fetchone()
    print(f"\n验证：cert_doc_extract_rules 共 {row[0]} 行，其中 {row[1]} 行有 MarkdownContent")
    print(f"本次：插入 {inserted}，更新 {skipped}")

    cur.close()
    conn.close()

if __name__ == '__main__':
    main()
