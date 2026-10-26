import re, sys
fn = sys.argv[1]; p1 = int(sys.argv[2]); p2 = int(sys.argv[3])
s = open(fn, encoding="utf-8").read()
pages = re.split(r"===PAGE (\d+)===", s)
pat = re.compile(r"^\s*((?:[4-9]|10)(?:\.\d{1,2}){0,3})\s+(\S[^。；，、]{0,44})$")
for i in range(1, len(pages), 2):
    pno = int(pages[i])
    if not (p1 <= pno <= p2): continue
    for l in pages[i+1].split("\n"):
        m = pat.match(l)
        if m:
            print(f"p{pno}\t{m.group(1)}\t{m.group(2).strip()}")
