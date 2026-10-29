# -*- coding: utf-8 -*-
import pymysql
conn = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.',
                       database='yzh_cert_platform', charset='utf8mb4')
cur = conn.cursor()
cur.execute("""SELECT Id,PromptCode,PromptName,PromptType,StandardCode,Version,IsActive,IsValid,IsDeleted,
  CHAR_LENGTH(Template), LEFT(Template,300) FROM wf_prompt_template ORDER BY Id""")
for r in cur.fetchall():
    print(r[0],'|',r[1],'|',r[2],'|type=',r[3],'|std=',r[4],'|v=',r[5],'|act=',r[6],'|valid=',r[7],'|del=',r[8],'|len=',r[9])
    print('   HEAD:', (r[10] or '').replace('\n',' ⏎ ')[:300])
print()
cur.execute("SELECT Id,PromptCode,PromptName,ScopeKey,CHAR_LENGTH(Template),IsValid,IsDeleted FROM cert_prompt_template ORDER BY Id")
for r in cur.fetchall(): print(r)
conn.close()
