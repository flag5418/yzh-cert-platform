#!/usr/bin/env python3
"""
重跑「已上传 + 正文已就绪 + 提取状态仍是 none」的历史槽位（2026-09-30）。

背景
----
这 173 个文件在 2026-09-27 完成转换，**早于** 9/29「无规则 → skipped」分支落地，
因此 ExtractStatus 一直停在 `none`，页面显示「未提取」——而全库只有 1 条提取规则，
它们绝大多数其实属于「未配置」。

做法
----
不硬改状态（那等于伪造执行器结论），而是**真的入队 doc_extract**：
执行器会自己判定 → 无规则落 `skipped`、有规则真跑 LLM 落 `completed` / `failed`。
队列/任务/资源锁三张表按 `TriggerExtractAsync` 的同款写法插入，保证框架能正常调度。

安全
----
- 只处理 `IsValid=1 AND IsDeleted=0 AND StoragePath 非空 AND MarkdownStatus='completed'`
- 已有活跃资源锁（正在跑）的文件跳过
- 已有 pending/processing 任务的文件跳过
- `SourceId = {fileCode}@{yyyyMMddHHmmss}` 保证 uk_source 唯一
- 默认 **dry-run**，加 `--apply` 才真正写入

用法
----
  python3 scripts/db/fix/rerun-extract-none-20260930.py            # 预览
  python3 scripts/db/fix/rerun-extract-none-20260930.py --apply    # 执行
"""
import subprocess
import sys
import uuid
from datetime import datetime

DB = "yzh_cert_platform"
# ⚠️ 必须带 `-i`：批量写入是把整批 SQL 从 stdin 灌给容器内 mysql，
#    少了 `-i` 时 docker exec 不转发 stdin ⇒ mysql 收到空输入、exit 0、无 stderr
#    ⇒ 脚本会误报「已入队 N 个」而 DB 里一行都没有（实测踩过）。
MYSQL = ["docker", "exec", "-i", "yzh-mysql", "mysql", "-uroot", "-pYzh123456.",
         "--default-character-set=utf8mb4", "-N", "-B", DB, "-e"]
LOCK_TABLE = "cert_standard_directory_file"

APPLY = "--apply" in sys.argv


def sql(query: str) -> str:
    r = subprocess.run(MYSQL + [query], capture_output=True, text=True)
    out = r.stdout.strip()
    err = "\n".join(l for l in r.stderr.splitlines() if "Warning" not in l)
    if err.strip():
        raise RuntimeError(f"SQL 失败: {err}\n---\n{query[:500]}")
    return out


def esc(s: str) -> str:
    return (s or "").replace("\\", "\\\\").replace("'", "''")


def main() -> int:
    # 1. 候选：**企业域**已上传 + 提取状态 none + 有效行
    #
    # ★★ 2026-09-30 实测更正：`cert_standard_directory_file` 同时承载两种行 ——
    #   · `EnterpriseCode='YZH-STD-ENT'` = **标准目录模板行**（机构的标准文件库，
    #     存储路径 `/standard-directory/...`）—— 共 168 行，**不该做企业提取**
    #     （执行器有护栏：`禁止对标准模板域（虚拟企业 Code）执行企业提取`）
    #   · `EnterpriseCode=<真实企业 Code>` = **企业资料槽位行**（路径 `/enterprise-documents/...`）
    #   ⇒ 必须显式排除模板域，否则会把 168 个模板文件当企业资料重跑（全部白跑并失败）。
    rows = sql(f"""
        SELECT Code, IFNULL(EnterpriseCode,''), IFNULL(StageCode,''),
               IFNULL(FileName,''), IFNULL(ConfigCode,'')
        FROM cert_standard_directory_file
        WHERE StoragePath IS NOT NULL AND StoragePath <> ''
          AND EnterpriseCode IS NOT NULL AND EnterpriseCode <> 'YZH-STD-ENT'
          AND (ExtractStatus IS NULL OR ExtractStatus = 'none')
          AND IsValid = 1 AND IsDeleted = 0
    """)
    cands = [l.split("\t") for l in rows.splitlines() if l.strip()]
    if not cands:
        print("没有需要重跑的槽位。")
        return 0

    codes = [c[0] for c in cands]

    # 2. 排除已有活跃资源锁的（正在跑）
    locked = set()
    for i in range(0, len(codes), 500):
        chunk = codes[i:i + 500]
        inlist = ",".join("'" + esc(c) + "'" for c in chunk)
        r = sql(f"""
            SELECT ResourceCode FROM yzh_queue_resource_lock
            WHERE ResourceTable='{LOCK_TABLE}' AND Status='locked'
              AND ResourceCode IN ({inlist})
        """)
        locked.update(l for l in r.splitlines() if l.strip())

    # 3. 排除已有 pending/processing 任务的
    busy = set()
    for i in range(0, len(codes), 500):
        chunk = codes[i:i + 500]
        inlist = ",".join("'" + esc(c) + "'" for c in chunk)
        r = sql(f"""
            SELECT DISTINCT t.LockedBy FROM yzh_queue_task t
            WHERE t.TaskType='doc_extract' AND t.Status IN ('pending','processing')
        """)
        # LockedBy 不是文件码；改用 Payload 匹配（精确到 code 字段）
        r = sql(f"""
            SELECT DISTINCT SUBSTRING_INDEX(SUBSTRING_INDEX(t.Payload,'"code":"',-1),'"',1)
            FROM yzh_queue_task t
            WHERE t.TaskType='doc_extract' AND t.Status IN ('pending','processing')
        """)
        busy.update(l for l in r.splitlines() if l.strip() and l.strip() in set(chunk))

    todo = [c for c in cands if c[0] not in locked and c[0] not in busy]

    print(f"候选 {len(cands)} 个；跳过（有活跃锁 {len(locked)} / 队列在跑 {len(busy)}）；待重跑 {len(todo)} 个")
    if not todo:
        return 0

    if not APPLY:
        print("\n--- dry-run 前 5 个 ---")
        for c in todo[:5]:
            print(f"  {c[0]}  {c[3]}  ent={c[1]}")
        print("\n加 --apply 才真正写入。")
        return 0

    # 4. 写入：queue + task + resource_lock + 槽位置「已加入提取队列」
    stamp = datetime.now().strftime("%Y%m%d%H%M%S")
    day = datetime.now().strftime("%Y%m%d")
    stmts = []
    for code, ent, stage, fname, cfg in todo:
        qcode = f"Q-{day}-{uuid.uuid4().hex[:9]}"
        payload = ('{"code":"%s","enterpriseCode":"%s","stageCode":"%s","fileName":"%s"}'
                   % (esc(code), esc(ent), esc(stage), esc(fname)))
        src_id = f"{code}@{stamp}"
        stmts.append(
            "INSERT INTO yzh_queue (Code, QueueCode, QueueType, QueueName, ScopeKey, "
            "SourceType, SourceId, Status, TotalCount, PendingCount, CreateTime, IsDeleted, IsValid) VALUES ("
            f"'{uuid.uuid4().hex}', '{qcode}', 'doc_extract', "
            f"'企业资料提取（历史重跑） - {esc(fname)}', '{esc(ent)}', "
            f"'enterprise_manual', '{esc(src_id)}', 'pending', 1, 1, NOW(), 0, 1);"
        )
        stmts.append(
            "INSERT INTO yzh_queue_task (Code, QueueCode, TaskType, Payload, Status, "
            "RetryCount, MaxRetryCount, CreateTime, IsDeleted) VALUES ("
            f"'{uuid.uuid4().hex}', '{qcode}', 'doc_extract', '{payload}', 'pending', 0, 3, NOW(), 0);"
        )
        stmts.append(
            "INSERT INTO yzh_queue_resource_lock (Code, QueueCode, ResourceTable, ResourceCode, "
            "ResourceName, Status, ActiveKey, CreateTime, IsDeleted, IsValid) VALUES ("
            f"'{uuid.uuid4().hex}', '{qcode}', '{LOCK_TABLE}', '{esc(code)}', "
            f"'{esc(fname)}', 'locked', '{LOCK_TABLE}|{esc(code)}', NOW(), 0, 1);"
        )
        stmts.append(
            "UPDATE cert_standard_directory_file SET ExtractMessage='已加入提取队列', UpdateTime=NOW() "
            f"WHERE Code='{esc(code)}';"
        )

    batch = "SET NAMES utf8mb4;\n" + "\n".join(stmts)
    # ⚠️ 批量写入必须去掉末尾的 `-e`（那是给单条查询用的），让 mysql 从 stdin 读整批 SQL；
    #    否则报 `option '-e' requires an argument`。
    r = subprocess.run(MYSQL[:-1], input=batch, capture_output=True, text=True)
    err = "\n".join(l for l in r.stderr.splitlines() if "Warning" not in l)
    if err.strip():
        print("写入失败：", err[:2000])
        return 1

    # ★ 写入校验：docker exec 的静默失败曾导致「报成功但一行没写」，必须回查确认
    written = sql(f"SELECT COUNT(*) FROM yzh_queue WHERE QueueName LIKE '%历史重跑%'")
    n = int((written or "0").splitlines()[0] or 0)
    if n < len(todo):
        print(f"⚠️ 校验失败：期望写入 {len(todo)} 个队列，实际查到 {n} 个。请检查 stdin 是否被 docker exec 转发。")
        return 1

    print(f"已入队 {len(todo)} 个提取任务（{len(todo)} 个队列，回查确认 {n} 个）。队列会自行调度执行。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
