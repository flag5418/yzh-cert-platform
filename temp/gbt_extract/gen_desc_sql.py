# -*- coding: utf-8 -*-
"""
把「条款完整正文」写回 cert_iso_clause.Description。

输入：temp/gbt_extract/clause_text.json
输出：scripts/db/20261026_gbt_standard_clause_text_V1.sql

★ 说明：
  - 正文 = 该条款标题之后、下一条款标题之前的全部文字（含 4.1.1 / a) b) 等无标题编号正文段）
  - 父条款若无引导段（如「4 组织环境」直接进 4.1），Description 保持 NULL
  - OCR 来源（19001 / 42061）已做定点错字校正，见 FIXES
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
sys.path.insert(0, HERE)

from gen_seed import STANDARDS, md5  # noqa: E402

OUT = os.path.join(ROOT, "scripts/db/20261026_gbt_standard_clause_text_V1.sql")

# OCR 定点校正（仅收「上下文唯一、无歧义」的错字；宁缺勿滥）
FIXES = [
    ("股务", "服务"),
    ("殷务", "服务"),
    ("穷得", "赢得"),
    ("窮得", "赢得"),
    ("邇讯", "通讯"),
    ("热菇", "热源"),
    ("监祝", "监视"),
    ("揹施", "措施"),
    ("眀示", "明示"),
    ("润讚油", "润滑油"),
    ("許多", "许多"),
    ("饺店", "饭店"),
    ("筑围", "氛围"),
    ("耍求", "要求"),
    ("职贵", "职责"),
    ("手冊", "手册"),
    ("过桯", "过程"),
    ("。是注日期", "。凡是注日期"),
]


CJK = "\u4e00-\u9fff"

# 排版归一：24001/45001 的 PDF 文本层用的是半角标点 + 两端对齐插入的空格，
# 与 19001/42061 的 OCR 结果不一致，统一成国标排印形态（全角标点、无字间空格）。
NORMALIZE = [
    (re.compile(rf"(?<=[{CJK}])\s+(?=[{CJK}])"), ""),          # 汉字之间多余空格
    (re.compile(rf"(?<=[{CJK}]),"), "，"),
    (re.compile(rf",(?=[{CJK}])"), "，"),
    (re.compile(rf"(?<=[{CJK}]);"), "；"),
    (re.compile(rf";(?=[{CJK}])"), "；"),
    (re.compile(rf"(?<=[{CJK}]):"), "："),
    (re.compile(rf":(?=[{CJK}])"), "："),
    (re.compile(rf"(?<=[{CJK}])\?"), "？"),
    (re.compile(rf"(?<=[{CJK}])!"), "！"),
]


def fix(t: str) -> str:
    for a, b in FIXES:
        t = t.replace(a, b)
    for pat, rep in NORMALIZE:
        t = pat.sub(rep, t)
    return t


def esc(s: str) -> str:
    return s.replace("\\", "\\\\").replace("'", "''")


def main():
    data = json.load(open(os.path.join(HERE, "clause_text.json"), encoding="utf-8"))
    L = []
    A = L.append
    A("-- =====================================================================================")
    A("-- 20261026_gbt_standard_clause_text_V1.sql")
    A("-- 回填 cert_iso_clause.Description = 国标条款完整正文")
    A("--")
    A("-- 口径：正文 = 该条款标题之后 → 下一条款标题之前的全部文字，")
    A("--       因此「4.1 总要求」完整包含 4.1.1~4.1.6 等无标题编号正文段（不是提纲）。")
    A("--       父条款无引导段的（如「4 组织环境」直接进 4.1），Description 保持 NULL。")
    A("-- 来源：docs/90-归档/案例资料/ 四份国标 PDF；24001/45001 原生文本层，")
    A("--       19001/42061 为 macOS Vision 3 倍分辨率 OCR，已做定点错字校正。")
    A("-- 幂等：按 Code = MD5('<slug>|<条款编号>') 定位，可重复执行。")
    A("-- =====================================================================================")
    A("SET NAMES utf8mb4 COLLATE utf8mb4_general_ci;")
    A("")

    total = 0
    chars = 0
    for st in STANDARDS:
        slug = st["key"]
        m = data.get(slug, {})
        A(f"-- ---------- {st['name']}：{len(m)} 条有正文 ----------")
        for num, raw in m.items():
            txt = fix(raw).strip()
            if not txt:
                continue
            total += 1
            chars += len(txt)
            A(f"UPDATE cert_iso_clause SET Description = '{esc(txt)}', UpdateTime = NOW()")
            A(f" WHERE Code = '{md5(slug + '|' + num)}';")
        A("")

    A("-- ---------- 验证 SQL ----------")
    A(f"-- ① 有正文条款数 = {total}，正文合计 {chars} 字")
    A("-- SELECT s.StandardCode, COUNT(c.Id) AS 条款数,")
    A("--        SUM(CASE WHEN c.Description IS NULL THEN 1 ELSE 0 END) AS 无正文,")
    A("--        SUM(CHAR_LENGTH(IFNULL(c.Description,''))) AS 正文总字数")
    A("--   FROM cert_iso_standard s JOIN cert_iso_clause c")
    A("--     ON c.StandardCode = s.Code AND c.IsDeleted = 0")
    A("--  WHERE s.IsDeleted = 0 GROUP BY s.StandardCode;")
    A("-- ② 抽查：iso13485 的 4.1 应含 4.1.1~4.1.6 全文")
    A("-- SELECT Description FROM cert_iso_clause WHERE Code = '" + md5("iso13485|4.1") + "';")

    sql = "\n".join(L) + "\n"
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(sql)
    print(f"written {OUT}")
    print(f"  UPDATE {total} 条，正文 {chars} 字，文件 {len(sql)} 字节")


if __name__ == "__main__":
    main()
