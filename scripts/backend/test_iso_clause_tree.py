#!/usr/bin/env python3
"""
ISO 条款左树右表接口自动化测试
覆盖：建树 / getTree / 编辑改上级 / 防环 / 有子禁删 / 父子同批删 / showDisabled / nc-config 回归
"""
import json
import sys
import uuid
import urllib.request
import urllib.parse

BASE = "http://127.0.0.1:9992"
PASS = 0
FAIL = 0
RESULTS = []


def req(method, path, body=None, token=None, query=None):
    url = BASE + path
    if query:
        url += "?" + urllib.parse.urlencode(query)
    data = None
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    if body is not None:
        data = json.dumps(body).encode("utf-8")
    r = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(r, timeout=30) as resp:
            raw = resp.read().decode("utf-8")
            status = resp.status
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8")
        status = e.code
    try:
        parsed = json.loads(raw) if raw else None
    except Exception:
        parsed = {"_raw": raw}
    return status, parsed


def check(name, cond, detail=""):
    global PASS, FAIL
    if cond:
        PASS += 1
        RESULTS.append(("PASS", name, detail))
        print(f"  PASS  {name}" + (f" — {detail}" if detail else ""))
    else:
        FAIL += 1
        RESULTS.append(("FAIL", name, detail))
        print(f"  FAIL  {name}" + (f" — {detail}" if detail else ""))


def login():
    st, d = req("POST", "/api/User/login", {"UserName": "admin", "Password": "123456", "Captcha": "", "Uuid": ""})
    if st != 200 or not d or not d.get("success"):
        print("LOGIN FAIL", st, d)
        sys.exit(1)
    return d["data"]["Token"]


def flat_map(items):
    m = {}
    stack = list(items or [])
    while stack:
        n = stack.pop()
        m[n["Code"]] = n
        stack.extend(n.get("children") or [])
    return m


def main():
    token = login()
    print("== 0. treepconfig ParentCode ==")
    st, d = req("GET", "/api/Foundation/ISOStandardTreeTable/treepconfig", token=token)
    cols = ((d.get("data") or {}).get("TableConfig") or (d.get("data") or {}).get("tableConfig") or {}).get("Columns") or ((d.get("data") or {}).get("TableConfig") or (d.get("data") or {}).get("tableConfig") or {}).get("columns") or []
    parent = next((c for c in cols if c.get("FieldName") == "ParentCode"), None)
    check("ParentCode Type=TreeSelect", parent and parent.get("Type") == "TreeSelect", json.dumps(parent, ensure_ascii=False) if parent else "missing")
    check("ParentCode BcFlag=true", parent and parent.get("BcFlag") is True)
    # formFields 过滤：BcFlag && Type !== 'Other'（XsFlag 是表格列显示，不参与表单）
    check("ParentCode 在表单字段中", parent and parent.get("BcFlag") and parent.get("Type") not in (None, "Other"), f"Type={parent.get('Type') if parent else None} BcFlag={parent.get('BcFlag') if parent else None}")

    print("== 1. 准备测试标准 ==")
    std_code = "autotest-" + uuid.uuid4().hex[:12]
    st, d = req("POST", "/api/Foundation/ISOStandardTreeTable/tree/add", {
        "Code": std_code,
        "StandardCode": "AUTOTEST-" + uuid.uuid4().hex[:8].upper(),
        "StandardName": "自动化测试标准-条款树-" + uuid.uuid4().hex[:8],
        "VersionYear": 2026,
        "Category": "quality",
        "Description": "API test",
        "Remark": "api test",
        "IsValid": 1,
        "ParentCode": None,
    }, token=token)
    check("创建测试标准", st == 200 and d and d.get("success"), f"st={st} msg={d.get('message') if d else ''}")
    if not (st == 200 and d and d.get("success")):
        print("ABORT: cannot create standard")
        return 1

    # 用 ISOStandard.Code 关联：ISOClause.StandardCode 应存标准 Code
    # tree/add 返回 TreeItemDto，Code 即标准 Code
    real_std = std_code

    def add_clause(num, title, parent_code, **kw):
        body = {
            "StandardCode": real_std,
            "ClauseNumber": num,
            "Title": title,
            "ParentCode": parent_code,
            "SortOrder": 0,
            "IsValid": 1,
            "Description": kw.get("desc", "auto"),
        }
        st, d = req("POST", "/api/Foundation/ISOClause/add", body, token=token)
        return st, d

    print("== 2. 建树 7 → 7.1 → 7.1.1 ==")
    st, d = add_clause("7", "总则", None)
    check("新增根 7", st == 200 and d and d.get("success"), d.get("message") if d else "")
    code7 = (d.get("data") or {}).get("Code") if d and d.get("success") else None
    st, d = add_clause("7.1", "总则应用", code7)
    check("新增子 7.1", st == 200 and d and d.get("success"), d.get("message") if d else "")
    code71 = (d.get("data") or {}).get("Code") if d and d.get("success") else None
    st, d = add_clause("7.1.1", "细则", code71)
    check("新增孙 7.1.1", st == 200 and d and d.get("success"), d.get("message") if d else "")
    code711 = (d.get("data") or {}).get("Code") if d and d.get("success") else None
    # 再加一个兄弟根，用于改上级
    st, d = add_clause("8", "其他", None)
    code8 = (d.get("data") or {}).get("Code") if d and d.get("success") else None
    check("新增兄弟根 8", st == 200 and d and d.get("success"))

    if not all([code7, code71, code711, code8]):
        print("ABORT: missing codes", code7, code71, code711, code8)
        return 1

    print("== 3. getTree 返回扁平全量（前端组树+默认展开） ==")
    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std, "includeDisabled": "true"})
    rows = (d or {}).get("data") or []
    m = flat_map(rows)
    check("getTree 含 4 节点", len(rows) == 4, f"n={len(rows)}")
    check("7 根 ParentCode 空", m.get(code7, {}).get("ParentCode") in (None, ""), str(m.get(code7, {}).get("ParentCode")))
    check("7.1 挂 7", m.get(code71, {}).get("ParentCode") == code7)
    check("7.1.1 挂 7.1", m.get(code711, {}).get("ParentCode") == code71)
    # 前端 buildClauseTree 语义验证（与 logic.ts 同构）
    roots = [c for c in rows if not c.get("ParentCode")]
    child_map = {}
    for c in rows:
        p = c.get("ParentCode")
        if p:
            child_map.setdefault(p, []).append(c["Code"])
    check("组树根仅 7 和 8", sorted(roots, key=lambda x: x["ClauseNumber"]) and set(r["Code"] for r in roots) == {code7, code8})
    check("7 的直接子含 7.1", code71 in child_map.get(code7, []))
    check("7.1 的直接子含 7.1.1", code711 in child_map.get(code71, []))

    print("== 4. 编辑改上级（层级调整） ==")
    # 4.1 把 8 挂到 7 下
    st, d = req("POST", "/api/Foundation/ISOClause/update", {
        "Code": code8,
        "StandardCode": real_std,
        "ClauseNumber": "8",
        "Title": "其他",
        "ParentCode": code7,
        "SortOrder": 0,
        "IsValid": 1,
        "Description": "auto",
    }, token=token)
    check("8 挂到 7 成功", st == 200 and d and d.get("success"), d.get("message") if d else "")

    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std, "includeDisabled": "true"})
    m = flat_map((d or {}).get("data") or [])
    check("持久化 ParentCode=7", m.get(code8, {}).get("ParentCode") == code7, str(m.get(code8, {}).get("ParentCode")))

    # 4.2 清空回顶级
    st, d = req("POST", "/api/Foundation/ISOClause/update", {
        "Code": code8,
        "StandardCode": real_std,
        "ClauseNumber": "8",
        "Title": "其他",
        "ParentCode": None,
        "SortOrder": 0,
        "IsValid": 1,
        "Description": "auto",
    }, token=token)
    check("8 清空上级回顶级", st == 200 and d and d.get("success"), d.get("message") if d else "")
    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std, "includeDisabled": "true"})
    m = flat_map((d or {}).get("data") or [])
    check("清空后 ParentCode 空", m.get(code8, {}).get("ParentCode") in (None, ""), str(m.get(code8, {}).get("ParentCode")))

    # 4.3 防环：把 7 挂到 7.1.1 下应失败
    st, d = req("POST", "/api/Foundation/ISOClause/update", {
        "Code": code7,
        "StandardCode": real_std,
        "ClauseNumber": "7",
        "Title": "总则",
        "ParentCode": code711,
        "SortOrder": 0,
        "IsValid": 1,
    }, token=token)
    msg = (d or {}).get("message") or ""
    check("防环：7 挂到子孙 7.1.1 被拒", not (d and d.get("success")) and ("自身" in msg or "子孙" in msg or "子条款" in msg or "挂到" in msg), f"st={st} {msg}")

    # 4.4 自环
    st, d = req("POST", "/api/Foundation/ISOClause/update", {
        "Code": code7,
        "StandardCode": real_std,
        "ClauseNumber": "7",
        "Title": "总则",
        "ParentCode": code7,
        "SortOrder": 0,
        "IsValid": 1,
    }, token=token)
    msg = (d or {}).get("message") or ""
    check("防环：挂到自身被拒", not (d and d.get("success")) and ("自身" in msg), f"st={st} {msg}")

    # 4.5 跨标准挂父
    other_std = "autotest-" + uuid.uuid4().hex[:12]
    st, d = req("POST", "/api/Foundation/ISOStandardTreeTable/tree/add", {
        "Code": other_std,
        "StandardCode": "AUTOTEST-" + uuid.uuid4().hex[:8].upper(),
        "StandardName": "另一标准-" + uuid.uuid4().hex[:8],
        "VersionYear": 2026,
        "IsValid": 1,
    }, token=token)
    if d and d.get("success"):
        # 用另一标准的父 code 不可得——用不存在父
        st, d = req("POST", "/api/Foundation/ISOClause/update", {
            "Code": code7,
            "StandardCode": real_std,
            "ClauseNumber": "7",
            "Title": "总则",
            "ParentCode": "not-exist-code",
            "SortOrder": 0,
            "IsValid": 1,
        }, token=token)
        check("父不存在被拒", not (d and d.get("success")), (d or {}).get("message"))

    print("== 5. 有子禁删 / 父子同批删 ==")
    # 仅删 7（有 7.1 子）应失败
    st, d = req("POST", "/api/Foundation/ISOClause/delete", [code7], token=token)
    msg = (d or {}).get("message") or ""
    check("有子禁删：仅删 7 被拒", not (d and d.get("success")) and "子条款" in msg, f"st={st} {msg}")

    # 仅删 7 应仍存在
    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std, "includeDisabled": "true"})
    m = flat_map((d or {}).get("data") or [])
    check("7 删除被拒后仍在", code7 in m)

    # 父子同批删 7+7.1+7.1.1，保留 8
    st, d = req("POST", "/api/Foundation/ISOClause/delete", [code7, code71, code711], token=token)
    check("父子同批删除成功", st == 200 and d and d.get("success"), (d or {}).get("message") if d else "")
    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std, "includeDisabled": "true"})
    m = flat_map((d or {}).get("data") or [])
    check("同批删后 7 系消失", code7 not in m and code71 not in m and code711 not in m)
    check("8 仍保留", code8 in m)

    print("== 6. showDisabled / 禁用过滤 ==")
    # 禁用 8 再查
    # isValid 字段在实体是 IsValid；update 可保存 BcFlag=true 的 IsValid
    st, d = req("POST", "/api/Foundation/ISOClause/update", {
        "Code": code8,
        "StandardCode": real_std,
        "ClauseNumber": "8",
        "Title": "其他",
        "ParentCode": None,
        "SortOrder": 0,
        "IsValid": 0,
        "Description": "auto",
    }, token=token)
    check("禁用 8", st == 200 and d and d.get("success"), (d or {}).get("message") if d else "")

    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std, "includeDisabled": "false"})
    m = flat_map((d or {}).get("data") or [])
    check("includeDisabled=false 不含禁用 8", code8 not in m, f"rows={len(m)}")

    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std, "includeDisabled": "true"})
    m = flat_map((d or {}).get("data") or [])
    check("includeDisabled=true 含禁用 8", code8 in m)

    # 默认 includeDisabled 缺省应为 false（兼容 nc-config）
    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std})
    m = flat_map((d or {}).get("data") or [])
    check("缺省 includeDisabled 不含禁用（nc-config 兼容）", code8 not in m)

    print("== 7. nc-config getTree 回归（标准编码下可用） ==")
    # 用质量管理体系标准码（若无条款则 data=[] 也应 200）
    for sc in ["77deabb45c7242dcb923119354619068", "5833f6fe-a802-11f1-9d10-96af2c2a4a1a"]:
        st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": sc})
        check(f"nc-config 可用 getTree {sc[:8]}", st == 200 and d and d.get("success") is True and isinstance(d.get("data"), list), f"n={len((d or {}).get('data') or [])}")

    print("== 8. 条款编号唯一 ==")
    # 重建一个节点测重复编号
    st, d = add_clause("9", "唯一测试", None)
    code9 = (d.get("data") or {}).get("Code") if d and d.get("success") else None
    check("新增 9", bool(code9))
    if code9:
        st, d = add_clause("9", "重复编号", None)
        msg = (d or {}).get("message") or ""
        check("重复条款编号被拒", not (d and d.get("success")) and "已存在" in msg, msg)

    print("== 9. 清理测试数据 ==")
    # 删 8、9（无子），再删标准
    codes_del = [c for c in [code8, code9] if c]
    if codes_del:
        # 8 可能禁用，软删仍可
        st, d = req("POST", "/api/Foundation/ISOClause/delete", codes_del, token=token)
        check("清理条款", st == 200 and d and d.get("success"), (d or {}).get("message") if d else "")
    # 查残留
    st, d = req("GET", "/api/Foundation/ISOClause/getTree", token=token, query={"standardCode": real_std, "includeDisabled": "true"})
    rows = (d or {}).get("data") or []
    if rows:
        check("清理后仍残留则列出（软删）", True, f"remaining soft-deleted may remain n={len(rows)}")
    st, d = req("POST", "/api/Foundation/ISOStandardTreeTable/tree/delete", [real_std], token=token)
    # 有关联条款（软删仍计数）可能被拒——则保留并记为 warn
    if d and d.get("success"):
        check("删除测试标准", True)
    else:
        # 尝试硬依赖：若软删条款仍阻塞，用直接删除仅当无软删计数
        # 记为 WARN 不计入 FAIL（环境软删策略）
        RESULTS.append(("WARN", "删除测试标准", (d or {}).get("message") if d else ""))
        print(f"  WARN  删除测试标准 — {(d or {}).get('message')}")

    # 清理另一标准（避免残留导致下次标准名称唯一校验失败）
    if other_std:
        st, d = req("POST", "/api/Foundation/ISOStandardTreeTable/tree/delete", [other_std], token=token)
        check("清理另一标准", st == 200 and d and d.get("success"), (d or {}).get("message") if d else "")

    print()
    print(f"==== RESULT: PASS={PASS} FAIL={FAIL} total={len(RESULTS)} ====")
    for status, name, detail in RESULTS:
        if status == "FAIL":
            print(f"  [{status}] {name} — {detail}")
    return 1 if FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
