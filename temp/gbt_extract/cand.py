import re, sys
fn = sys.argv[1]
lines = open(fn, encoding="utf-8").read().split("\n")
# strip page markers
out=[]
for i,l in enumerate(lines):
    if l.startswith("===PAGE"): continue
    out.append(l)
pat = re.compile(r"^\s*(\d{1,2}(?:\.\d{1,2}){0,4})\s+(\S[^。；，]{0,48})$")
seen=set()
for l in out:
    m = pat.match(l)
    if m:
        num, title = m.group(1), m.group(2).strip()
        if num in seen: continue
        seen.add(num)
        print(f"{num}\t{title}")
