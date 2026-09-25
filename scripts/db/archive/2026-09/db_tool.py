#!/usr/bin/env python3
"""
数据库连接测试和 SQL 执行工具
使用 pymysql（轻量级，无需 Xcode 依赖）
"""
import sys
import os

DB_CONFIG = {
    'host': '127.0.0.1',
    'port': 3307,
    'user': 'root',
    'password': 'Yzh123456.',
    'database': 'yzh_cert_platform',
    'charset': 'utf8mb4'
}

def get_connection():
    """获取数据库连接"""
    import pymysql
    return pymysql.connect(**DB_CONFIG)

def execute_sql_file(file_path: str):
    """执行 SQL 文件"""
    conn = get_connection()
    try:
        if True:  # Always use pymysql
            cursor = conn.cursor()
        else:
            cursor = conn.cursor(dictionary=True)
        
        with open(file_path, 'r', encoding='utf-8') as f:
            sql = f.read()
        
        print(f"[INFO] 执行 SQL 文件: {file_path}")
        
        # 按 ; 分割
        commands = sql.split(';')
        executed = 0
        errors = []
        
        for command in commands:
            cmd = command.strip()
            if not cmd:
                continue
            # 跳过注释行
            lines = [l for l in cmd.split('\n') if not l.strip().startswith('--')]
            clean_cmd = '\n'.join(lines).strip()
            if not clean_cmd:
                continue
            try:
                cursor.execute(clean_cmd)
                if True:  # Always use pymysql
                    while cursor.nextset():
                        pass
                executed += 1
            except Exception as err:
                # IGNORE 语句失败不影响整体
                err_str = str(err).lower()
                if 'duplicate' in err_str or 'already exists' in err_str:
                    print(f"  [SKIP] 已存在: {err}")
                    continue
                errors.append((clean_cmd[:80], str(err)))
        
        conn.commit()
        print(f"[OK] 执行完成: {executed} 条语句，{len(errors)} 条错误")
        if errors:
            for cmd, err in errors[:5]:
                print(f"  [WARN] {cmd}... → {err}")
        return len(errors) == 0
        
    finally:
        conn.close()

def verify_table():
    """验证表结构和数据"""
    conn = get_connection()
    try:
        cursor = conn.cursor()
        
        # 检查表是否存在
        cursor.execute("SHOW TABLES LIKE 'cert_phase_definition'")
        table_exists = cursor.fetchone()
        
        if not table_exists:
            print("[ERROR] 表 cert_phase_definition 不存在")
            return False
        
        print("[OK] 表 cert_phase_definition 存在")
        
        # 检查列结构
        cursor.execute("DESCRIBE cert_phase_definition")
        columns = cursor.fetchall()
        print(f"[OK] 表结构: {len(columns)} 列")
        for col in columns:
            print(f"  - {col[0]} ({col[1]})")
        
        # 检查数据
        cursor.execute("SELECT phase_code, phase_name, sequence_order, is_valid FROM cert_phase_definition ORDER BY sequence_order")
        rows = cursor.fetchall()
        print(f"[OK] 数据: {len(rows)} 条")
        for r in rows:
            print(f"  {r[0]} | {r[1]} | order={r[2]} | valid={r[3]}")
        
        # 检查视图
        cursor.execute("SHOW TABLES LIKE 'v_cert_phase_definition'")
        view_exists = cursor.fetchone()
        if view_exists:
            print("[OK] 视图 v_cert_phase_definition 存在")
            cursor.execute("SELECT * FROM v_cert_phase_definition LIMIT 5")
            views = cursor.fetchall()
            print(f"[OK] 视图数据: {len(views)} 条")
        else:
            print("[WARN] 视图 v_cert_phase_definition 不存在")
        
        return len(rows) >= 5
        
    finally:
        conn.close()

if __name__ == "__main__":
    print("=" * 60)
    print("  数据库连接测试")
    print("=" * 60)
    
    print("[INFO] 使用驱动: pymysql")
    
    try:
        conn = get_connection()
        print("[OK] 数据库连接成功")
        conn.close()
    except Exception as e:
        print(f"[ERROR] 数据库连接失败: {e}")
        sys.exit(1)
    
    # 执行 SQL
    script_dir = os.path.dirname(os.path.abspath(__file__))
    sql_file = os.path.join(script_dir, 'cert_phase_definition_setup.sql')
    
    if os.path.exists(sql_file):
        ok = execute_sql_file(sql_file)
        if ok:
            verify_table()
            print("\n[SUCCESS] 数据库迁移完成")
        else:
            print("\n[WARNING] 部分语句执行失败，请检查日志")
    else:
        print(f"[ERROR] SQL 文件不存在: {sql_file}")
    
    sys.exit(0)
