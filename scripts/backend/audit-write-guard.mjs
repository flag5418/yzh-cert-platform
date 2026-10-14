#!/usr/bin/env node
/**
 * 后端「审计字段写入收敛」守卫 B-R8（设计 §3.5 防复发守卫 · 选项 C）
 *
 * 用途：扫全仓**绕过基类**的直接写库调用，输出清单 + 基线 —— **只准减、不准增**。
 *
 * 为什么必须有这道闸：
 *   `EntityService<T>.Insert/Update/InsertBatch` 会按 `IUserContext.UserCode`
 *   自动填 `CreateBy / UpdateBy / DeleteBy`（AGENTS ⑥ 的同键方）。
 *   而 `_db.Client.Insertable / Updateable / Deleteable` 与 `_db.InsertAsync / UpdateAsync /
 *   InsertBatchAsync` 是**裸写**：值不会被填 ⇒ 落库后这三列要么是 NULL（读侧显示「创建人为空」），
 *   要么是代码里手写的字面量（`USER_000001` / 登录名 / 角色名）—— 读侧一律按 Code 解释，
 *   命不中**不报错**，只表现为「姓名永远解析不出来」。
 *
 * 判据（设计 §3.5 原文）：
 *   扫描「实体含审计列」场景下对 `_db.Client.Insertable/Updateable/Deleteable`
 *   与 `_db.InsertAsync/UpdateAsync` 的直接调用，输出清单 + 基线（只准减不准增）。
 *
 * ⚠️ 按行匹配 + **逐文件计数**（不是全局一个数）：
 *   - 逐文件 ⇒ 某文件从 7 降到 3 会被记录为改进，不会被另一处的新增抵消；
 *   - 计数而非行号 ⇒ 中间插入/删除代码不会让基线漂移（行号会漂）。
 *
 * 用法：
 *   node scripts/backend/audit-write-guard.mjs                    # 通过 → 1 行摘要；失败 → 明细 + exit 1
 *   node scripts/backend/audit-write-guard.mjs --report           # 全量盘点，永不失败
 *   node scripts/backend/audit-write-guard.mjs --update-baseline  # 重新测定基线（⚠️ 人工确认后才跑）
 *
 * 📏 基线文件：scripts/backend/audit-write-baseline.json
 *   首轮基线（2026-10-14）= 设计 §3.4 收敛清单落地当天的实测值。
 *   ⚠️ 与 `guards.mjs` 的「基线必须为 0」铁律不同：本规则**允许非 0 首轮基线**（§3.5 明文），
 *      因为存量直写点有 200+ 处，只能靠「只准减不准增」逐批收敛。
 *
 * ⛔ 严禁挂到 dotnet watch / dev 启动脚本 —— 只允许挂到 build / pre-commit / 手动门禁。
 *
 * ⚠️ 扫描范围：src/certplatform-api/**（⛔ 不含 src/old/ —— 历史项目冻结）。
 *   yzh-core 不扫：框架自身（`SqlSugarDbOrm` / `EntityService`）就是写入实现，天然要裸调。
 */

import { readdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const SCAN_ROOT = join(ROOT, 'src/certplatform-api')
const BASELINE_FILE = join(ROOT, 'scripts/backend/audit-write-baseline.json')

const REPORT_ONLY = process.argv.includes('--report')
const UPDATE_BASELINE = process.argv.includes('--update-baseline')

const SKIP_DIRS = new Set(['bin', 'obj', 'node_modules', '.git'])

/**
 * 直写判据（去注释后逐行匹配）：
 *   ① `_db.Client.Insertable / Updateable / Deleteable`（SqlSugar 链式裸写）
 *   ② `任何接收者.InsertAsync / UpdateAsync / InsertBatchAsync`（DbOrm 裸写）
 *      ⚠️ 接收者不限定为 `_db`：本仓还有 `db` / `db2` / `profile` 等别名，限定接收者会漏报。
 */
const PATTERNS = [
  /\.Client\.(Insertable|Updateable|Deleteable)\s*[<(]/,
  /\.(InsertAsync|UpdateAsync|InsertBatchAsync)\s*\(/,
]

/** 行号保持不变地抹掉注释（否则注释里的示例会误报） */
function stripComments(text) {
  return text
    .replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, ' '))
    .replace(/(^|[^:])\/\/.*$/gm, '$1')
}

function walk(dir, out = []) {
  let entries
  try {
    entries = readdirSync(dir, { withFileTypes: true })
  } catch {
    return out
  }
  for (const e of entries) {
    if (SKIP_DIRS.has(e.name)) continue
    const p = join(dir, e.name)
    if (e.isDirectory()) walk(p, out)
    else if (e.name.endsWith('.cs')) out.push(p)
  }
  return out
}

function scan() {
  const counts = new Map()   // relPath -> hit count（逐行）
  const hits = []            // { rel, line, text }

  for (const abs of walk(SCAN_ROOT)) {
    const rel = relative(ROOT, abs)
    const raw = readFileSync(abs, 'utf8')
    const text = stripComments(raw)
    const lines = text.split('\n')

    let n = 0
    lines.forEach((line, i) => {
      if (PATTERNS.some((re) => re.test(line))) {
        n++
        hits.push({ rel, line: i + 1, text: raw.split('\n')[i]?.trim() ?? '' })
      }
    })
    if (n > 0) counts.set(rel, n)
  }

  return { counts, hits }
}

function loadBaseline() {
  try {
    return JSON.parse(readFileSync(BASELINE_FILE, 'utf8'))
  } catch {
    return null
  }
}

const { counts, hits } = scan()
const total = [...counts.values()].reduce((a, b) => a + b, 0)

if (UPDATE_BASELINE) {
  const sorted = Object.fromEntries([...counts.entries()].sort((a, b) => a[0].localeCompare(b[0])))
  writeFileSync(BASELINE_FILE, JSON.stringify(sorted, null, 2) + '\n', 'utf8')
  console.log(`✓ 基线已重写：${BASELINE_FILE}（${counts.size} 个文件 / ${total} 处直写）`)
  process.exit(0)
}

const baseline = loadBaseline()

if (REPORT_ONLY) {
  const base = baseline ?? {}
  console.log(`📊 盘点：${counts.size} 个文件 / ${total} 处直写（基线 ${Object.keys(base).length} 个文件）`)
  for (const [rel, n] of [...counts.entries()].sort((a, b) => b[1] - a[1])) {
    const b = base[rel] ?? 0
    const tag = n > b ? '↑新增' : n < b ? '↓改进' : '='
    console.log(`  ${tag} ${rel}：${n}（基线 ${b}）`)
  }
  const improved = Object.entries(base).filter(([rel, n]) => (counts.get(rel) ?? 0) < n)
  console.log(`改进文件：${improved.length} 个`)
  process.exit(0)
}

if (!baseline) {
  console.error(`✗ 缺少基线文件：${relative(ROOT, BASELINE_FILE)}`)
  console.error('  首次运行请先测基线（人工确认清单后）：')
  console.error('  node scripts/backend/audit-write-guard.mjs --update-baseline')
  process.exit(1)
}

// 只准减、不准增：逐文件比对
const violations = []
for (const [rel, n] of counts) {
  const base = baseline[rel] ?? 0
  if (n > base) violations.push({ rel, base, now: n })
}
// 基线里有、本轮没有 ⇒ 改进（记入报告，不算违规）
const improved = Object.entries(baseline).filter(([rel, n]) => (counts.get(rel) ?? 0) < n)

if (REPORT_ONLY) {
  console.log(`📊 盘点：${counts.size} 个文件 / ${total} 处直写（基线 ${Object.keys(baseline).length} 个文件）`)
  for (const [rel, n] of [...counts.entries()].sort((a, b) => b[1] - a[1])) {
    const base = baseline[rel] ?? 0
    const tag = n > base ? '↑新增' : n < base ? '↓改进' : '='
    console.log(`  ${tag} ${rel}：${n}（基线 ${base}）`)
  }
  console.log(`改进文件：${improved.length} 个；违规文件：${violations.length} 个`)
  process.exit(0)
}

if (violations.length === 0) {
  console.log(
    `✓ 审计写入守卫通过（${counts.size} 个文件 / ${total} 处直写 ≤ 基线，改进 ${improved.length} 个文件）`
  )
  process.exit(0)
}

console.error(`✗ 审计写入守卫：${violations.length} 个文件直写调用**超过基线**（只准减不准增）\n`)
for (const v of violations) {
  console.error(`  ${v.rel}：${v.now} 处（基线 ${v.base}，新增 +${v.now - v.base}）`)
  for (const h of hits.filter((x) => x.rel === v.rel)) {
    console.error(`    :${h.line}  ${h.text.slice(0, 140)}`)
  }
  console.error('')
}
console.error('  改法：改走 `EntityService<T>.Insert/Update/InsertBatch`（由基类按 IUserContext.UserCode 填审计字段）；')
console.error('        已在自己事务里 / 需要真批量 ⇒ `mode: WriteMode.FillOnly` 只借基类填字段（设计 §3.3）。')
console.error('        确认是**无审计列**的表而应豁免 ⇒ 跑 --update-baseline 前先在设计文档登记。')
process.exit(1)
