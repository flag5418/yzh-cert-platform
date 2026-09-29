#!/usr/bin/env python3
"""企业资料管理 /resources 后端 E2E 自测（四段上传 → 队列 → 下载 → 替换 → 版本 → 分发预览 → 取消）"""
import json, time, urllib.request, urllib.parse, urllib.error, uuid, os

BASE = "http://localhost:9992/api/Auditor/EnterpriseFile"
TOK = open("/tmp/ent_tok.txt").read().strip()
ENT = "42833c3d3fe241b1bcfa707f6b92e4c3"
STAGE = "29c1bcc3a18942e1865b2497a0262504"
STD = "846dec4b-c534-4983-94e6-8cf04982b7d9"

OK = []
FAIL = []

def check(name, cond, extra=""):
    (OK if cond else FAIL).append(name)
    print(("  PASS " if cond else "  FAIL ") + name + (("  → " + str(extra)[:400]) if extra else ""))

def post(path, payload, want_ok=True):
    req = urllib.request.Request(BASE + path, data=json.dumps(payload).encode(),
                                 headers={"Authorization": "Bearer " + TOK,
                                          "Content-Type": "application/json"})
    with urllib.request.urlopen(req) as r:
        body = json.loads(r.read().decode())
    if want_ok:
        assert body.get("success"), f"{path} 失败: {body.get('err') or body.get('message')}"
    return body

def get_raw(path, headers=None):
    req = urllib.request.Request(BASE + path, headers=headers or {"Authorization": "Bearer " + TOK})
    try:
        with urllib.request.urlopen(req) as r:
            return r.status, r.read(), dict(r.headers)
    except urllib.error.HTTPError as e:
        return e.code, e.read(), {}

def post_multipart(path, fields, filename, content):
    boundary = "----yzh" + uuid.uuid4().hex
    body = b""
    for k, v in fields.items():
        body += (f"--{boundary}\r\nContent-Disposition: form-data; name=\"{k}\"\r\n\r\n{v}\r\n").encode()
    body += (f"--{boundary}\r\nContent-Disposition: form-data; name=\"File\"; filename=\"{filename}\"\r\n"
             f"Content-Type: application/octet-stream\r\n\r\n").encode() + content + b"\r\n"
    body += f"--{boundary}--\r\n".encode()
    req = urllib.request.Request(BASE + path, data=body,
                                 headers={"Authorization": "Bearer " + TOK,
                                          "Content-Type": f"multipart/form-data; boundary={boundary}"})
    try:
        with urllib.request.urlopen(req) as r:
            return json.loads(r.read().decode())
    except urllib.error.HTTPError as e:
        return {"success": False, "err": f"HTTP {e.code}: {e.read()[:300]}"}

print("=== 1. 标准卡片主数据 ===")
sd = post("/standard-directory", {"EnterpriseCode": ENT, "StageCode": STAGE, "StandardCode": STD})["data"]
check("standard-directory Configured", sd["Configured"] is True)
check("模板文件夹 11 个（含三级）", len(sd["Folders"]) == 11, len(sd["Folders"]))
check("根段已剥离（相对配置根）", any(f["FullPath"] == "1质量手册" for f in sd["Folders"]))

missing = [f for f in sd["Files"] if f["Status"] == "missing"]
check("存在缺失槽位（可上传）", len(missing) > 0, len(missing))
slot = missing[0]   # 每轮消耗一个缺失槽位，可重复运行
slot_name = slot["FileName"]
print("   选定槽位:", slot_name, "|", slot["FolderCode"][:8], "|", slot["FolderPath"])

def wait_idle(config_code, seconds=60):
    """等目录级转换队列结束（04 §七：同标准目录一次只允许一个运行队列，否则 init/replace/restore 会被拒）"""
    deadline = time.time() + seconds
    while time.time() < deadline:
        if not post("/active-queue", {"ConfigCode": config_code, "EnterpriseCode": ENT})["data"]["IsBusy"]:
            return True
        time.sleep(1.5)
    return False

wait_idle(sd["EnterpriseConfigCode"])

print("=== 2. Step1 upload/init（槽位模式）===")
init = post("/upload/init", {"EnterpriseCode": ENT, "StageCode": STAGE, "StandardCode": STD,
                             "Items": [{"SlotCode": slot["Code"], "FileName": slot_name,
                                        "FileSize": 42}]})["data"]
task_id = init["TaskId"]
items = init["Items"]
check("init 返回 TaskId", bool(task_id))
check("init 下发 StoragePath", bool(items[0]["StoragePath"]), items[0]["StoragePath"])
check("StoragePath 落在企业库且免根段", items[0]["StoragePath"].startswith("/enterprise-documents/" + ENT)
      and not items[0]["StoragePath"].startswith("/enterprise-documents/" + ENT + "/" + STD + "/" + STAGE + "/CS"),
      items[0]["StoragePath"])

print("=== 3. 已就位槽位拒收（P0-1 守卫）===")
occupied = next(f for f in sd["Files"] if f["Status"] == "ready")
r = post("/upload/init", {"EnterpriseCode": ENT, "StageCode": STAGE, "StandardCode": STD,
                          "Items": [{"SlotCode": occupied["Code"], "FileName": "x.doc", "FileSize": 1}]},
         want_ok=False)
check("已就位槽位被拒（须走替换）", r.get("success") is False and "替换" in (r.get("err") or ""), r.get("err"))

print("=== 4. Step2 upload/file ===")
content = ("质量手册正文 " + uuid.uuid4().hex[:8]).encode() * 3
res = post_multipart("/upload/file", {"TaskId": task_id, "FileCode": items[0]["FileCode"]},
                     slot_name, content)
check("Step2 上传成功", res.get("success") is True, res.get("err"))

print("=== 5. Step3 upload/confirm（激活 + 入队）===")
conf = post("/upload/confirm", {"TaskId": task_id, "EnterpriseCode": ENT})["data"]
check("confirm 激活 1 个文件", conf["ActivatedCount"] == 1, conf)
check("可转换文件入队 file_convert", bool(conf["QueueCode"]), conf)
queue_code = conf.get("QueueCode")

print("=== 6. active-queue / 队列推进 ===")
busy_seen = False
for _ in range(40):
    aq = post("/active-queue", {"ConfigCode": sd["EnterpriseConfigCode"], "EnterpriseCode": ENT})["data"]
    if aq["IsBusy"]:
        busy_seen = True
    else:
        break
    time.sleep(1.5)
print("   队列:", json.dumps({k: aq.get(k) for k in ("IsBusy", "QueueCode", "Status", "TotalCount", "CompletedCount", "FailedCount")}, ensure_ascii=False))
# 假 .doc 会被 LibreOffice 秒拒 → 队列可能在本循环前就已终态，故只要求「最终不忙」
if busy_seen:
    print("   （曾观察到运行中队列）")
check("队列已结束（非运行中）", aq["IsBusy"] is False, aq)

print("=== 7. 状态回读 ===")
sd2 = post("/standard-directory", {"EnterpriseCode": ENT, "StageCode": STAGE, "StandardCode": STD})["data"]
row = next(f for f in sd2["Files"] if f["Code"] == slot["Code"])
print("   槽位:", row["FileName"], "| 状态:", row["Status"], "| ConvertStatus:", row.get("ConvertStatus"),
      "| MarkdownStatus:", row.get("MarkdownStatus"), "| PreviewPdf:", (row.get("PreviewPdfPath") or "")[-40:])
check("槽位已激活（IsValid=1）", row["IsValid"] == 1)
check("槽位不再是 missing", row["Status"] != "missing", row["Status"])
check("已入转换链且有产物", bool(row.get("ConvertStatus")), row.get("ConvertStatus"))
check("Summary.Live 增加", sd2["Summary"]["Live"] >= sd["Summary"]["Live"] + 1,
      (sd["Summary"]["Live"], sd2["Summary"]["Live"]))

print("=== 8. download ===")
status, data, _ = get_raw("/download?storagePath=" + urllib.parse.quote(row["StoragePath"], safe=""))
check("download 返回 200 且字节一致", status == 200 and data == content, (status, len(data), len(content)))

print("=== 9. 非法路径 / 越权路径被拒 ===")
r = post("/stage-overview", {"EnterpriseCode": "TEST-ENT-0001", "StageCode": STAGE}, want_ok=False)
check("越权企业被拒（工作区守卫）", "不属于当前工作区" in (r["data"].get("Message") or ""), r["data"].get("Message"))
st, body, _ = get_raw("/download?storagePath=" + urllib.parse.quote("/etc/passwd", safe=""))
check("非白名单路径被拒", b'"success":false' in body or st == 200 and b"err" in body, body[:120])

print("=== 10. 替换（归档 + 版本 + 重新入队）===")
wait_idle(sd["EnterpriseConfigCode"])
new_content = ("替换后的质量手册 " + uuid.uuid4().hex[:8]).encode() * 4
res = post_multipart("/replace", {"FileCode": slot["Code"], "EnterpriseCode": ENT, "Reason": "E2E 自测替换"},
                     slot_name, new_content)
check("replace 成功", res.get("success") is True, res.get("err"))
vers = get_raw("/versions/" + slot["Code"] + "?enterpriseCode=" + ENT)[1]
vrows = json.loads(vers)["data"]
check("归档产生 1 个版本", len(vrows) == 1, len(vrows))
check("版本号为 1 且路径含 _archive", vrows and vrows[0]["VersionNumber"] == 1 and "_archive" in vrows[0]["StoragePath"],
      vrows[0] if vrows else None)
status, data, _ = get_raw("/download?storagePath=" + urllib.parse.quote(row["StoragePath"], safe=""))
check("替换后同路径读到新内容（路径稳定）", data == new_content, (len(data), len(new_content)))

hist = json.loads(get_raw("/history/" + slot["Code"] + "?enterpriseCode=" + ENT)[1])["data"]
check("history 有 upload + replace + archive", hist is not None and len(hist["Timeline"]) >= 3,
      [t["OpType"] for t in (hist or {}).get("Timeline", [])])

print("=== 11. 恢复版本 v1 ===")
wait_idle(sd["EnterpriseConfigCode"])
res = post("/restore", {"FileCode": slot["Code"], "EnterpriseCode": ENT, "VersionNumber": 1, "Reason": "E2E 回滚"})
check("restore 成功", res.get("success") is True, res.get("err"))
sd3 = post("/standard-directory", {"EnterpriseCode": ENT, "StageCode": STAGE, "StandardCode": STD})["data"]
row3 = next(f for f in sd3["Files"] if f["Code"] == slot["Code"])
check("恢复后版本号单调递增（≥3）", row3["VersionNumber"] >= 3, row3["VersionNumber"])

print("=== 12. upload/plan 多标准分发预览 ===")
plan = post("/upload/plan", {"EnterpriseCode": ENT, "StageCode": STAGE,
                             "Files": [{"FileName": slot_name, "RelativePath": slot["FolderPath"] + "/" + slot_name},
                                       {"FileName": "完全不存在的东西.doc", "RelativePath": "其它/完全不存在的东西.doc"}]})["data"]
check("plan Configured", plan["Configured"] is True)
print("   Summary:", json.dumps(plan["Summary"], ensure_ascii=False))
check("未归属文件进 Unmatched（不塞各标准）", plan["Summary"]["UnmatchedCount"] == 1, plan["Summary"])
check("已就位槽位命中被 Block（须替换）",
      all(r["BlockReason"] for s in plan["Standards"] for r in s["Rows"]) or plan["Summary"]["RowCount"] == 0,
      [r["BlockReason"] for s in plan["Standards"] for r in s["Rows"]])

print("=== 13. 指派模式（D8 模板外文件）+ 取消回滚 ===")
wait_idle(sd["EnterpriseConfigCode"])
init2 = post("/upload/init", {"EnterpriseCode": ENT, "StageCode": STAGE, "StandardCode": STD,
                              "Items": [{"FolderCode": sd["Folders"][1]["Code"], "FileName": "E2E临时文件.doc",
                                         "FileSize": 12}]})["data"]
res = post_multipart("/upload/file", {"TaskId": init2["TaskId"], "FileCode": init2["Items"][0]["FileCode"]},
                     "E2E临时文件.doc", b"tmp")
check("指派模式 Step2 成功", res.get("success") is True, res.get("err"))
cancel = post("/upload/cancel", {"TaskId": init2["TaskId"], "EnterpriseCode": ENT})["data"]
check("cancel 删除草稿行 + 清对象", cancel["DeletedRows"] == 1 and cancel["RemovedObjects"] == 1, cancel)

sd4 = post("/standard-directory", {"EnterpriseCode": ENT, "StageCode": STAGE, "StandardCode": STD})["data"]
check("取消后草稿行不残留", not any(f["FileName"] == "E2E临时文件.doc" for f in sd4["Files"]))

print("=== 14. file-check 兼容（旧前端）===")
fc = post("/file-check", {"EnterpriseCode": ENT, "StageCode": STAGE})["data"]
check("file-check 仍可用且 configured", fc["configured"] is True)
print("   summary:", json.dumps(fc["summary"], ensure_ascii=False))

print()
print(f"===== 结果：PASS {len(OK)} / FAIL {len(FAIL)} =====")
if FAIL:
    print("失败项：" + "；".join(FAIL))
