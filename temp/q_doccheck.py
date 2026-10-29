import pymysql, collections
c = pymysql.connect(host='127.0.0.1', port=3307, user='root', password='Yzh123456.', database='yzh_cert_platform', charset='utf8mb4')
cur = c.cursor()
cur.execute("SELECT StandardCode, AttrValue, COUNT(*) FROM cert_clause_attr WHERE AttrCode='DocCheckMode' AND IsDeleted=0 AND IsValid=1 GROUP BY StandardCode, AttrValue")
print('DocCheckMode 分布:')
for r in cur.fetchall():
    print('  ', r)
cur.execute("SELECT StandardCode, COUNT(*) FROM cert_clause_attr WHERE IsDeleted=0 AND IsValid=1 GROUP BY StandardCode")
print('各标准属性行数:')
for r in cur.fetchall():
    print('  ', r)
cur.execute("SELECT COUNT(*) FROM cert_clause_attr WHERE AttrCode='DocCheckMode' AND IsDeleted=0 AND IsValid=1 AND AttrValue<>'none'")
print('DocCheckMode<>none 总数:', cur.fetchone())
cur.execute("SELECT Code, StandardCode, StandardName, Abbrev FROM cert_iso_standard WHERE IsValid=1 AND IsDeleted=0")
print('标准:')
for r in cur.fetchall():
    print('  ', r)
c.close()
