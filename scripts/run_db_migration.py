import mysql.connector
import sys
import os

def execute_sql_file(file_path, config):
    try:
        conn = mysql.connector.connect(
            host=config['host'],
            port=config['port'],
            user=config['user'],
            password=config['password'],
            database=config['database']
        )
        cursor = conn.cursor()
        
        with open(file_path, 'r', encoding='utf-8') as f:
            sql = f.read()
        
        print(f"Executing {file_path}...")
        
        # Split by semicolon and execute one by one
        # Simple splitting by ; works for these scripts as they don't have complex procedures
        commands = sql.split(';')
        
        for command in commands:
            cmd = command.strip()
            if not cmd:
                continue
            
            # Skip lines starting with -- or /*
            clean_cmd = []
            for line in cmd.split('\n'):
                if line.strip().startswith('--') or line.strip().startswith('/*'):
                    continue
                clean_cmd.append(line)
            
            final_cmd = "\n".join(clean_cmd).strip()
            if final_cmd:
                try:
                    cursor.execute(final_cmd)
                    # Consume any results to avoid "Unread result found"
                    while cursor.nextset():
                        pass
                except mysql.connector.Error as err:
                    print(f"Error executing statement: {err}")
                    print(f"Statement: {final_cmd[:100]}...")
        
        conn.commit()
        cursor.close()
        conn.close()
        print(f"Successfully finished {file_path}")
    except mysql.connector.Error as err:
        print(f"Connection error in {file_path}: {err}")

if __name__ == "__main__":
    db_config = {
        'host': '127.0.0.1',
        'port': 3307,
        'user': 'root',
        'password': 'Yzh123456.',
        'database': 'yzh_cert_platform'
    }
    
    base_dir = '/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/src/server/Vue.NetCore/DB/mysql/'
    # We should also execute data dictionary first if needed
    files = [
        'cert_platform_tables_v2.1.sql',
        'cert_phase2_data_dictionary.sql',
        'cert_phase2_test_data.sql'
    ]
    
    for f in files:
        execute_sql_file(os.path.join(base_dir, f), db_config)
