#!/usr/bin/env python3
"""
上传源文件到 MinIO 正确路径
1. 从数据库读取所有需要修复的文件记录（StoragePath 已修复为正确格式）
2. 将 StoragePath 映射到 docs/历史文档/案例/ 下的源文件
3. 上传到 MinIO

StoragePath 格式: /CB001/ISO134852016/STAGE01/{folderPath}/{fileName}
源文件路径:       docs/历史文档/案例/{folderPath}/{fileName}
"""

import os
import sys
import pymysql
from minio import Minio
from minio.error import S3Error

# ===== 配置 =====
MYSQL_HOST = "127.0.0.1"
MYSQL_PORT = 3307
MYSQL_USER = "root"
MYSQL_PASS = "Yzh123456."
MYSQL_DB   = "yzh_cert_platform"

MINIO_ENDPOINT = "127.0.0.1:9000"
MINIO_ACCESS   = "admin"
MINIO_SECRET   = "Yzh123456."
MINIO_BUCKET   = "cert-platform"

# 项目根目录
PROJECT_ROOT = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台"
SOURCE_BASE  = os.path.join(PROJECT_ROOT, "docs/历史文档/案例")

def main():
    # 连接 MySQL
    conn = pymysql.connect(
        host=MYSQL_HOST, port=MYSQL_PORT,
        user=MYSQL_USER, password=MYSQL_PASS,
        database=MYSQL_DB, charset="utf8mb4"
    )
    cursor = conn.cursor(pymysql.cursors.DictCursor)

    # 查询所有需要上传的文件（StoragePath 以 /CB001/ISO134852016/ 开头，排除已有的5个和 .DS_Store）
    cursor.execute("""
        SELECT FileCode, FileName, StoragePath, FolderCode
        FROM cert_standard_directory_file
        WHERE StoragePath LIKE '/CB001/ISO134852016/STAGE01/CS%'
          AND FileName != '.DS_Store'
          AND FolderCode LIKE 'FD-SDC-ISO134852016|STAGE01|L0%'
        ORDER BY FolderCode
    """)
    files = cursor.fetchall()
    print(f"📋 共 {len(files)} 个文件需要上传")

    # 连接 MinIO
    client = Minio(
        MINIO_ENDPOINT,
        access_key=MINIO_ACCESS,
        secret_key=MINIO_SECRET,
        secure=False
    )

    # 确保 bucket 存在
    if not client.bucket_exists(MINIO_BUCKET):
        client.make_bucket(MINIO_BUCKET)
        print(f"📦 创建 bucket: {MINIO_BUCKET}")

    uploaded = 0
    skipped = 0
    failed = 0

    for f in files:
        storage_path = f["StoragePath"]
        file_name = f["FileName"]

        # 从 StoragePath 提取 folderPath
        # /CB001/ISO134852016/STAGE01/{folderPath}/{fileName}
        parts = storage_path.strip("/").split("/", 3)  # ["CB001", "ISO134852016", "STAGE01", "{folderPath}/{fileName}"]
        if len(parts) < 4:
            print(f"  ⚠️ 跳过 {file_name}: StoragePath 格式异常")
            skipped += 1
            continue

        folder_and_file = parts[3]  # {folderPath}/{fileName}
        # 源文件路径 = SOURCE_BASE / {folder_and_file}
        source_path = os.path.join(SOURCE_BASE, folder_and_file)

        if not os.path.exists(source_path):
            print(f"  ❌ 源文件不存在: {source_path}")
            failed += 1
            continue

        # MinIO object name = StoragePath 去掉前导 /
        object_name = storage_path.lstrip("/")

        # 检查 MinIO 中是否已存在
        try:
            client.stat_object(MINIO_BUCKET, object_name)
            print(f"  ⏭️ 已存在，跳过: {object_name}")
            skipped += 1
            continue
        except S3Error:
            pass  # 文件不存在，继续上传

        # 上传文件
        try:
            content_type = "application/octet-stream"
            ext = os.path.splitext(file_name)[1].lower()
            if ext == ".pdf":
                content_type = "application/pdf"
            elif ext in (".doc",):
                content_type = "application/msword"
            elif ext in (".docx",):
                content_type = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            elif ext in (".xls",):
                content_type = "application/vnd.ms-excel"
            elif ext in (".xlsx",):
                content_type = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            elif ext in (".ppt",):
                content_type = "application/vnd.ms-powerpoint"
            elif ext in (".pptx",):
                content_type = "application/vnd.openxmlformats-officedocument.presentationml.presentation"

            file_size = os.path.getsize(source_path)
            with open(source_path, "rb") as f_data:
                client.put_object(
                    MINIO_BUCKET, object_name,
                    f_data, file_size,
                    content_type=content_type
                )
            print(f"  ✅ 上传成功: {file_name} → {object_name} ({file_size} bytes)")
            uploaded += 1
        except Exception as e:
            print(f"  ❌ 上传失败: {file_name} - {e}")
            failed += 1

    print(f"\n📊 统计: 上传={uploaded}, 跳过={skipped}, 失败={failed}")

    # 上传 .converted 目录下的转换文件（如果有）
    # 检查源文件中是否有 .doc/.xls 需要转换为 .docx/.xlsx
    # 这里先跳过转换，只上传原始文件

    cursor.close()
    conn.close()
    print("✅ 完成")

if __name__ == "__main__":
    main()
