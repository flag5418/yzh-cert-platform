#!/usr/bin/env bash
# 目录域 4+1 读端点 A/B 对账抓取（2026-10-13 核心资源检索统一 S3）
# 用法: test-directory-read-snapshot.sh <out.json>
#   重启前跑一次(旧代码基线) → 重启后跑一次(新代码) → 比对脚本判集合差集/顺序差。
set -uo pipefail
cd "$(dirname "$0")/../.." || exit 1

OUT="${1:-/tmp/dir-ab.json}"
BASE="http://127.0.0.1:9992/api/Admin/Workflow/StandardDirectory"
CFG="b27945be5e1b44f28bcf8fc19852d92d"
FOLDER="233bc384-0000-4000-8000-000000000001"  # 覆盖：运行时用首个有效文件夹

TOKEN=$(curl -s --noproxy '*' -X POST http://127.0.0.1:9992/api/User/login \
  -H 'Content-Type: application/json' \
  -d '{"UserName":"admin","Password":"123456"}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["data"]["Token"])')
[ -z "$TOKEN" ] && { echo "FAIL: login"; exit 1; }
AUTH="Authorization: Bearer $TOKEN"

# 解出一个真实文件夹 Code（新旧代码都从 folders-flat 拿，保证同一输入）
FOLDER=$(curl -s --noproxy '*' -H "$AUTH" "$BASE/configs/$CFG/folders-flat" \
  | python3 -c 'import sys,json;d=json.load(sys.stdin);rows=d.get("data") or [];print(rows[0]["Code"] if rows else "")')
[ -z "$FOLDER" ] && { echo "FAIL: no folder"; exit 1; }

fetch() { curl -s --noproxy '*' -H "$AUTH" "$1"; }

python3 - "$OUT" <<PY
import json, subprocess, sys

def fetch(url):
    r = subprocess.run(["curl","-s","--noproxy","*","-H","Authorization: Bearer $TOKEN",url],
                       capture_output=True, text=True)
    return json.loads(r.stdout)

base = "$BASE"; cfg = "$CFG"; folder = "$FOLDER"
eps = {
  "folder-tree": f"{base}/configs/{cfg}/folders",
  "folders-flat": f"{base}/configs/{cfg}/folders-flat",
  "files": f"{base}/folders/{folder}/files",
  "root-files": f"{base}/directories/{cfg}/root-files",
  "stage-files": f"{base}/stage-files/{cfg}",
}
out = {}
for name, url in eps.items():
    body = fetch(url)
    data = body.get("data")
    if name == "stage-files":
        # 把树拍平成有序列表 [(Code,Name),...] + 文件 [(FileCode,FileName),...]，保序对比
        folds, files = [], []
        def walk(nodes):
            for n in nodes or []:
                folds.append([n.get("Code",""), n.get("Name","")])
                walk(n.get("Children"))
                for f in n.get("Files") or []:
                    files.append([f.get("FileCode",""), f.get("FileName","")])
        walk((data or {}).get("Folders"))
        out[name] = {"folders": folds, "files": files,
                     "stats": (data or {}).get("Statistics"),
                     "rootFiles": [[f.get("FileCode",""), f.get("FileName","")]
                                   for f in next((n.get("Files") for n in (data or {}).get("Folders") or []
                                                  if n.get("Name") == "根目录"), [])]}
    else:
        rows = data or []
        key = "Code" if name in ("folder-tree","folders-flat") else "FileCode"
        if name in ("folder-tree",):
            # 树 → 拍平保序
            flat = []
            def walk(nodes, depth=0):
                for n in nodes or []:
                    flat.append([n.get("Code",""), n.get("FolderName") or n.get("Name",""), depth])
                    walk(n.get("Children"), depth+1)
            walk(rows)
            out[name] = {"rows": flat, "count": len(flat)}
        else:
            out[name] = {"rows": [[r.get("Code") or r.get("FileCode",""),
                                   r.get("FileName") or r.get("FolderName","")] for r in rows],
                         "count": len(rows),
                         "keys": sorted(rows[0].keys()) if rows else []}
json.dump(out, open("$OUT","w"), ensure_ascii=False, indent=1)
print("saved", "$OUT")
for k,v in out.items():
    print(f"  {k}: count={v.get('count', len(v.get('folders',[])))}")
PY
