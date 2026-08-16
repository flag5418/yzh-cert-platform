#!/usr/bin/env python3
"""清空 MinIO cert-platform bucket 和数据库文件/文件夹表"""
import pymysql
from minio import Minio
from minio.deleteobjects import DeleteObject

# ===== MinIO =====
client = Minio('127.0.0.1:9000', access_key='admin', secret_key='Yzh123456.', secure=False)
bucket = 'cert-platform'

objects = list(client.list_objects(bucket, recursive=True))
print(f'MinIO: 找到 {len(objects)} 个对象')

delete_list = [DeleteObject(obj.object_name) for obj in objects]
if delete_list:
    errors = list(client.remove_objects(bucket, delete_list))
    if errors:
        for e in errors:
            print(f'  删除失败: {e.object_name}: {e}')
    else:
        print(f'  ✅ 已删除 {len(delete_list)} 个对象')
else:
    print('  无对象需删除')

remaining = list(client.list_objects(bucket, recursive=True))
print(f'  剩余对象: {len(remaining)}')

# ===== MySQL =====
conn = pymysql.connect(
    host='127.0.0.1', port=3307,
    user='root', password='Yzh123456.',
    database='yzh_cert_platform', charset='utf8mb4'
)
cursor = conn.cursor()

# 文件表
cursor.execute('SELECT COUNT(*) FROM cert_standard_directory_file')
file_count = cursor.fetchone()[0]
cursor.execute('TRUNCATE TABLE cert_standard_directory_file')
print(f'DB: cert_standard_directory_file 已清空 ({file_count} 条)')

# 文件夹表
cursor.execute('SELECT COUNT(*) FROM cert_standard_directory_folder')
folder_count = cursor.fetchone()[0]
cursor.execute('TRUNCATE TABLE cert_standard_directory_folder')
print(f'DB: cert_standard_directory_folder 已清空 ({folder_count} 条)')

# 上传任务表
cursor.execute('SELECT COUNT(*) FROM cert_upload_task')
task_count = cursor.fetchone()[0]
cursor.execute('TRUNCATE TABLE cert_upload_task')
print(f'DB: cert_upload_task 已清空 ({task_count} 条)')

conn.commit()
cursor.close()
conn.close()
print('\n✅ 全部清空完成')
