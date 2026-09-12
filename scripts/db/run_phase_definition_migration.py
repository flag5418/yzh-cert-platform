"""
认证阶段定义 — 数据库迁移脚本执行器
执行：cert_phase_definition_setup.sql
"""
import mysql.connector
import sys
import os

DB_CONFIG = {
    'host': '127.0.0.1',
    'port': 3307,
    'user': 'root',
    'password': 'Yzh123456.',
    'database': 'yzh_cert_platform'
}

def execute_sql_file(file_path: str):
    """执行单个 SQL 文件"""
    try:
        conn = mysql.connector.connect(**DB_CONFIG)
        cursor = conn.cursor()
        
        with open(file_path, 'r', encoding='utf-8') as f:
            sql = f.read()
        
        print(f"[INFO] 执行 SQL 文件: {file_path}")
        
        # 按 ; 分割，跳过注释行
        commands = sql.split(';')
        executed = 0
        errors = []
        
        for command in commands:
            cmd = command.strip()
            if not cmd:
                continue
            # 跳过纯注释
            lines = [l for l in cmd.split('\n') if not l.strip().startswith('--')]
            clean_cmd = '\n'.join(lines).strip()
            if not clean_cmd:
                continue
            try:
                cursor.execute(clean_cmd)
                while cursor.nextset():
                    pass
                executed += 1
            except mysql.connector.Error as err:
                # IGNORE 语句失败不影响整体
                if 'duplicate' in str(err).lower() or 'already exists' in str(err).lower():
                    print(f"  [SKIP] 已存在: {err}")
                    continue
                errors.append((clean_cmd[:80], str(err)))
        
        conn.commit()
        cursor.close()
        conn.close()
        
        print(f"[OK] 执行完成: {executed} 条语句，{len(errors)} 条错误")
        if errors:
            for cmd, err in errors[:5]:
                print(f"  [WARN] {cmd}... → {err}")
        return len(errors) == 0
        
    except mysql.connector.Error as err:
        print(f"[ERROR] 连接失败: {err}")
        return False

def verify_inserted_data():
    """验证插入的数据"""
    try:
        conn = mysql.connector.connect(**DB_CONFIG)
        cursor = conn.cursor()
        cursor.execute("SELECT phase_code, phase_name, sequence_order, is_valid FROM cert_phase_definition ORDER BY sequence_order")
        rows = cursor.fetchall()
        cursor.close()
        conn.close()
        print(f"[VERIFY] cert_phase_definition 数据: {len(rows)} 条")
        for r in rows:
            print(f"  {r[0]} | {r[1]} | order={r[2]} | valid={r[3]}")
        return len(rows) >= 5
    except Exception as e:
        print(f"[VERIFY] 验证失败: {e}")
        return False

if __name__ == "__main__":
    script_dir = os.path.dirname(os.path.abspath(__file__))
    sql_file = os.path.join(script_dir, 'cert_phase_definition_setup.sql')
    
    if not os.path.exists(sql_file):
        print(f"[ERROR] SQL 文件不存在: {sql_file}")
        sys.exit(1)
    
    print("=" * 60)
    print("  认证阶段定义 — 数据库迁移")
    print("=" * 60)
    
    ok = execute_sql_file(sql_file)
    if ok:
        verify_inserted_data()
        print("\n[SUCCESS] 数据库迁移完成")
    else:
        print("\n[WARNING] 部分语句执行失败，请检查日志")
    
    sys.exit(0 if ok else 1)
