#!/usr/bin/env python3
"""快速测试 ISO 9001 提取提示词（直接调 DashScope API，绕过后端）"""
import json
import sys
import os

# 读取 docx 内容
try:
    from docx import Document
except ImportError:
    os.system(f"{sys.executable} -m pip install python-docx -q")
    from docx import Document

DOCX_PATH = "docs/90-归档/案例资料/CS河北雄安尚龙医疗科技有限公司13485体系材料/3制度文件（可根据企业实际修改）/检验作业指导书/《注塑工艺卡-真空拔罐器》.docx"

doc = Document(DOCX_PATH)
text_parts = []
for p in doc.paragraphs:
    if p.text.strip():
        text_parts.append(p.text.strip())

for table in doc.tables:
    text_parts.append("--- 表格 ---")
    for row in table.rows:
        row_text = " | ".join([cell.text.strip() for cell in row.cells if cell.text.strip()])
        if row_text:
            text_parts.append(row_text)

document_text = "\n".join(text_parts)
print("=" * 60)
print(f"文档: {DOCX_PATH}")
print(f"文本长度: {len(document_text)} 字符")
print("=" * 60)
print("原文预览（前 800 字）：")
print("-" * 60)
print(document_text[:800])
print("-" * 60)

# 读取提示词（从数据库导出）
import subprocess
result = subprocess.run(
    ["docker", "exec", "yzh-mysql", "mysql", "-uroot", "-pYzh123456.",
     "--default-character-set=utf8mb4", "yzh_cert_platform",
     "-e", "SELECT PromptContent FROM cert_doc_extract_rules WHERE Code='ISO9001-2016'"],
    capture_output=True, text=True
)
# 简单解析（跳过表头行）
lines = result.stdout.strip().split('\n')
prompt_lines = []
capture = False
for line in lines:
    if capture:
        prompt_lines.append(line)
    if 'PromptContent' in line and '---' not in line:
        capture = True
        continue

# mysql 输出可能分行拼接，需要重新组合
import re
raw = result.stdout
# 找表头后的内容
match = re.search(r'PromptContent\s*-+\s*(.*)', raw, re.DOTALL)
if match:
    prompt_content = match.group(1).strip()
else:
    # 备用：直接取最后列
    prompt_content = '\n'.join(prompt_lines).strip() if prompt_lines else ""

print(f"\n提示词长度: {len(prompt_content)} 字符")
print("=" * 60)

# 调用 LLM
import urllib.request

# API 配置（从数据库读取）
API_KEY = "sk-ws-H.EEDLRRX.r9Zg.MEQCIGLTcu6ml6sIRzQ52GRsHlD5Fn35z8FzLFBPtd8C-FH3AiAMuIPgrmwkhuYYfxRnz0rgi3j4RhB7X6acz97x0WXCsQ"
BASE_URL = "https://dashscope.aliyuncs.com/compatible-mode/v1"
MODEL = "qwen-plus"

# 组合最终 prompt
final_prompt = prompt_content + "\n\n---\n" + document_text + "\n---"

print(f"\n发送请求到 {MODEL}...")
print(f"Prompt 长度: {len(prompt_content)} + 文档长度: {len(document_text)} = 总长: {len(final_prompt)} 字符")

payload = json.dumps({
    "model": MODEL,
    "messages": [
        {"role": "user", "content": final_prompt}
    ],
    "temperature": 0.2,
    "max_tokens": 32768,
    "response_format": {"type": "json_object"}
}, ensure_ascii=False).encode("utf-8")

req = urllib.request.Request(
    f"{BASE_URL}/chat/completions",
    data=payload,
    headers={
        "Authorization": f"Bearer {API_KEY}",
        "Content-Type": "application/json"
    }
)

try:
    with urllib.request.urlopen(req, timeout=180) as resp:
        result_data = json.loads(resp.read().decode("utf-8"))
        llm_output = result_data["choices"][0]["message"]["content"]
        usage = result_data.get("usage", {})

        print(f"\n✅ LLM 调用成功！")
        print(f"   Prompt tokens: {usage.get('prompt_tokens', 'N/A')}")
        print(f"   Completion tokens: {usage.get('completion_tokens', 'N/A')}")
        print(f"   Total tokens: {usage.get('total_tokens', 'N/A')}")

        # 解析 JSON
        output_clean = llm_output.strip()
        if output_clean.startswith("```"):
            lines = output_clean.split("\n")
            output_clean = "\n".join(lines[1:-1] if lines[-1].strip() == "```" else lines[1:])

        try:
            result_json = json.loads(output_clean)
            print("\n" + "=" * 60)
            print("📋 提取结果（JSON）：")
            print("=" * 60)
            print(json.dumps(result_json, ensure_ascii=False, indent=2))
        except json.JSONDecodeError:
            print("\n⚠️ 输出不是标准 JSON：")
            print(llm_output[:2000])

except Exception as e:
    print(f"\n❌ LLM 调用失败: {e}")
