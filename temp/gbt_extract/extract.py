import sys, os, re
from pypdf import PdfReader

base = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/docs/90-归档/案例资料"
files = {
    "gbt19001": "【1】GBT 19001-2016《质量管理体系 要求》.pdf",
    "gbt24001": "【1】GBT 24001-2016 《环境管理体系 要求及使用指南》.pdf",
    "gbt45001": "【1】GB T 45001-2020 《职业健康安全管理体系 要求及使用指南》.pdf",
    "gbt42061": "GBT 42061-2022 医疗器械 质量管理体系 用于法规的要求.pdf",
}
out = "/Volumes/Expand/wangqingquan/Documents/work/study/体系认证平台/temp/gbt_extract"
for key, fn in files.items():
    p = os.path.join(base, fn)
    r = PdfReader(p)
    txt = []
    for i, pg in enumerate(r.pages):
        t = pg.extract_text() or ""
        txt.append(f"\n===PAGE {i+1}===\n" + t)
    s = "".join(txt)
    open(os.path.join(out, key + ".txt"), "w", encoding="utf-8").write(s)
    print(key, "pages=", len(r.pages), "chars=", len(s))
