# -*- coding: utf-8 -*-
"""
从国标 PDF 提取文本中切分「每条条款的完整正文」，输出 JSON。

输入：temp/gbt_extract/*.txt（===PAGE n=== 分隔）
输出：temp/gbt_extract/clause_text.json  → { slug: { "4.1": "正文…", ... } }

切分口径（与「完整内容」要求一致）：
  - 条款节点 = 有正式标题的编号（即 gen_seed.py 里的清单）
  - 某节点的正文 = 从它的标题行之后，到「下一个节点标题」之前的全部文字
  - 因此 42061 的 4.1 总要求 会完整包含 4.1.1~4.1.6 的无标题正文段（正是用户截图要的内容）
  - 无标题编号正文段（4.1.1 / 6.1.1 / 9.2.1 …）不单独建节点，其文字归属上级节点
"""
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from gen_seed import GBT19001, GBT24001, GBT45001, GBT42061  # noqa: E402

# slug -> (txt 文件, 正文起页, 正文止页, 条款清单)
SOURCES = {
    "iso9001": ("gbt19001_ocr3.txt", 10, 23, GBT19001),
    "iso14001": ("gbt24001.txt", 9, 21, GBT24001),
    "iso45001": ("gbt45001.txt", 11, 28, GBT45001),
    "iso13485": ("gbt42061_ocr3.txt", 9, 28, GBT42061),
}

# 页眉 = 「GB/T xxxxx—yyyy/ISO xxxxx:yyyy」形态；⚠️ 不能用 `^GB/T \d` 宽匹配 ——
# 会把正文里的规范性引用文件行（如「GB/T 19000—2016 界定的术语和定义适用于本文件。」）当页眉删掉。
HEADER_RE = re.compile(r"^GB/T\s*\d+[\-—–]\d+\s*/\s*ISO")
COVER_RE = re.compile(r"^(ICS\s|中华人民共和国|目\s*次$|前\s*言$)")
PAGENO_RE = re.compile(r"^[0-9]{1,3}$")
ROMAN_RE = re.compile(r"^[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩIVX\s]{1,6}$")
TERM_PUNCT = "。！？：；"
LIST_MARK = re.compile(r"^([a-z]|[a-z]）|[a-z]\)|注\s*[：:]|———|\[来源)")


def load_pages(path, p_from, p_to):
    s = open(path, encoding="utf-8").read()
    parts = re.split(r"===PAGE (\d+)===", s)
    lines = []
    for i in range(1, len(parts), 2):
        pno = int(parts[i])
        if not (p_from <= pno <= p_to):
            continue
        for raw in parts[i + 1].split("\n"):
            t = raw.strip()
            if not t:
                continue
            if HEADER_RE.match(t) or COVER_RE.match(t):
                continue
            if PAGENO_RE.match(t) or ROMAN_RE.match(t):
                continue
            lines.append(t)
    return lines


def _looks_like_heading(rest):
    """标题行的特征：短、无句末标点（用于 OCR 把标题识别错字时的兜底）"""
    return bool(rest) and len(rest) <= 20 and rest[-1] not in TERM_PUNCT


NOISE = " \u3000\t/、.,，:：|·"


def find_headings(lines, clauses):
    """按顺序定位每个条款标题所在行；返回 [(idx, number, body_start), ...]

    四种命中形态（OCR 版国标常见）：
      a) '4.1 总要求'                 —— 编号 + 标题同行
      b) '4.2.3' + 下一行 '医疗器械文档'  —— 编号与标题分行
      c) '7.5.10/' + '顾客财产'        —— 编号后带 OCR 噪声符号
      d) '7.1.2人员' / '5.5.1 职贵和权限' —— 无空格 或 标题错字
    """
    pos = []
    cursor = 0
    for num, title in clauses:
        head = title[:2]
        found = -1
        skip = 1
        for i in range(cursor, len(lines)):
            ln = lines[i]
            if not ln.startswith(num):
                continue
            rest = ln[len(num):]
            if rest[:1].isdigit():          # 7.5.90 之类，不是本条
                continue
            rest = rest.strip(NOISE)
            if rest.startswith(head):
                # 标题前两字命中即认定（长标题如 45001 的 6.1.2.2 无法用长度判断）
                found = i
                break
            if not rest:
                nxt = lines[i + 1].strip() if i + 1 < len(lines) else ""
                if nxt.startswith(head) or _looks_like_heading(nxt):
                    found = i
                    skip = 2
                    break
            elif _looks_like_heading(rest) and len(rest) <= 12:
                found = i
                break
        if found < 0:
            print(f"  !! 未定位标题 {num} {title}")
            continue
        pos.append((found, num, found + skip))
        cursor = found + 1
    return pos


def build_paragraphs(lines):
    """按「句末标点断段 + 列表标记断段」合并物理行"""
    paras, buf = [], ""
    for ln in lines:
        if buf and (buf[-1] in TERM_PUNCT or buf.endswith("。")):
            paras.append(buf)
            buf = ""
        if LIST_MARK.match(ln) and buf:
            paras.append(buf)
            buf = ""
        buf = (buf + ln) if buf else ln
    if buf:
        paras.append(buf)
    return paras


def extract(slug, fname, p_from, p_to, clauses):
    lines = load_pages(os.path.join(HERE, fname), p_from, p_to)
    pos = find_headings(lines, clauses)
    out = {}
    for k, (idx, num, body_start) in enumerate(pos):
        end = pos[k + 1][0] if k + 1 < len(pos) else len(lines)
        body = lines[body_start:end]
        # 去掉尾部残余页码 / 下一章页眉
        body = [b for b in body if not PAGENO_RE.match(b)]
        if not body:
            continue
        paras = build_paragraphs(body)
        txt = "\n".join(paras).strip()
        if txt:
            out[num] = txt
    return out


def main():
    result = {}
    for slug, (fname, p1, p2, clauses) in SOURCES.items():
        print(f"### {slug}  ({fname} p{p1}-p{p2})")
        d = extract(slug, fname, p1, p2, clauses)
        result[slug] = d
        missing = [n for n, _ in clauses if n not in d]
        print(f"  有正文 {len(d)}/{len(clauses)} 条；无正文: {missing}")

    out_path = os.path.join(HERE, "clause_text.json")
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=1)
    total = sum(len(v) for v in result.values())
    chars = sum(len(t) for v in result.values() for t in v.values())
    print(f"\nwritten {out_path}  条款 {total} 条，正文合计 {chars} 字")


if __name__ == "__main__":
    main()
