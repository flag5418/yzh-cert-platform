#!/usr/bin/env node
/**
 * check-style-tokens.mjs — core 样式令牌体检（★ 只读，报告工具）
 * ------------------------------------------------------------
 * 为什么需要它（G20 教训，2026-09-24）：
 *   core 曾有 6 个令牌被组件引用却**从未定义**
 *   （--yzh-color-bg-card / --yzh-color-text-regular /
 *     --yzh-color-{success,warning,danger,info}-light-9）
 *   → 组件永远走 `var(--x, #硬编码)` 的 fallback → **宿主改主题不生效**，
 *     而且**不报错、不警告**，只能靠人肉发现。
 *
 * 本工具检查三件事：
 *   ① 引用但未定义 的 --yzh-* 令牌（★ 严重：主题穿透失效）
 *   ② 硬编码颜色（hex / rgb / rgba）出现位置（债务，需逐步清零）
 *   ③ 令牌定义是否与宿主契约一致（core tokens.css vs 宿主 main.css）
 *
 * 用法：node scripts/frontend/check-style-tokens.mjs
 * 退出码：恒为 0（报告工具，不作门禁）；如需门禁见 --strict
 */
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join, relative, extname, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

// ⚠️ 不能用 new URL(...).pathname —— 仓库路径含中文（体系认证平台），
//    pathname 会做百分号编码导致 readdir 全部失败。必须用 fileURLToPath。
const ROOT = join(dirname(fileURLToPath(import.meta.url)), '../..')
const CORE = join(ROOT, 'src/certplatform-web/yzh.vue.core/src')
const HOSTS = [
  join(ROOT, 'src/certplatform-web/cert/cert-admin/src'),
  join(ROOT, 'src/certplatform-web/cert/cert-auditor/src')
]
const STRICT = process.argv.includes('--strict')

const TOKEN_USE = /var\(\s*(--yzh-[a-z0-9-]+)\s*(?:,([^)]*))?\)/g
const TOKEN_DEF = /^\s*(--yzh-[a-z0-9-]+)\s*:/gm
const COLOR_LITERAL = /#[0-9a-fA-F]{3,8}\b|\brgba?\([^)]*\)/g
const SKIP_DIR = new Set(['node_modules', 'dist', '.vite-cache'])

function walk(dir, out = []) {
  let entries
  try { entries = readdirSync(dir) } catch { return out }
  for (const name of entries) {
    if (SKIP_DIR.has(name)) continue
    const p = join(dir, name)
    const st = statSync(p)
    if (st.isDirectory()) walk(p, out)
    else if (['.vue', '.ts', '.css', '.scss'].includes(extname(p))) out.push(p)
  }
  return out
}

const coreFiles = walk(CORE)

// ---------- ① 令牌定义集合（core tokens.css） ----------
const tokensCss = join(CORE, 'assets/css/tokens.css')
const defined = new Set()
try {
  for (const m of readFileSync(tokensCss, 'utf8').matchAll(TOKEN_DEF)) defined.add(m[1])
} catch {
  console.error(`✗ 未找到 core 令牌默认层：${relative(ROOT, tokensCss)}`)
  process.exit(1)
}

// ---------- ② 引用扫描 ----------
const used = new Map() // token -> [file:line]
const hardcoded = []   // { file, line, text, literal }
for (const f of coreFiles) {
  const txt = readFileSync(f, 'utf8')
  const rel = relative(ROOT, f)
  for (const m of txt.matchAll(TOKEN_USE)) {
    const line = txt.slice(0, m.index).split('\n').length
    if (!used.has(m[1])) used.set(m[1], [])
    used.get(m[1]).push(`${rel}:${line}${m[2] ? `  fallback=${m[2].trim()}` : ''}`)
  }
  if (!f.endsWith('.css')) {
    const lines = txt.split('\n')
    lines.forEach((l, i) => {
      if (/^\s*(\/\/|\*|\/\*)/.test(l)) return
      for (const c of l.matchAll(COLOR_LITERAL)) {
        hardcoded.push({ file: rel, line: i + 1, text: l.trim().slice(0, 90), literal: c[0] })
      }
    })
  }
}

// ---------- ③ 宿主覆盖层定义集合 ----------
const hostDefined = new Map()
for (const h of HOSTS) {
  for (const f of walk(h)) {
    if (!f.endsWith('.css')) continue
    const txt = readFileSync(f, 'utf8')
    for (const m of txt.matchAll(TOKEN_DEF)) hostDefined.set(m[1], relative(ROOT, f))
  }
}

// ---------- 报告 ----------
const missing = [...used.keys()].filter((t) => !defined.has(t))
const unusedTokens = [...defined].filter((t) => !used.has(t))

console.log('='.repeat(66))
console.log('core 样式令牌体检')
console.log('='.repeat(66))
console.log(`core 扫描文件      : ${coreFiles.length}`)
console.log(`tokens.css 定义令牌: ${defined.size}`)
console.log(`组件引用令牌       : ${used.size}`)

console.log('\n── ① 引用但未定义（★ 严重：主题穿透失效，且静默无报错） ──')
if (missing.length === 0) console.log('  ✓ 无')
else for (const t of missing) console.log(`  ✗ ${t}\n      ${used.get(t).join('\n      ')}`)

console.log('\n── ② 硬编码颜色（债务，逐步清零） ──')
const byFile = new Map()
for (const h of hardcoded) byFile.set(h.file, (byFile.get(h.file) || 0) + 1)
if (byFile.size === 0) console.log('  ✓ 无')
else {
  console.log(`  合计 ${hardcoded.length} 处 / ${byFile.size} 个文件`)
  for (const [f, n] of [...byFile.entries()].sort((a, b) => b[1] - a[1]))
    console.log(`    ${String(n).padStart(3)}  ${f}`)
}

console.log('\n── ③ 宿主覆盖层 vs core 默认层 ──')
console.log(`  宿主共覆盖令牌: ${hostDefined.size}`)
const hostOnly = [...hostDefined.keys()].filter((t) => !defined.has(t))
if (hostOnly.length) {
  console.log(`  ⚠ 宿主定义了 core 默认层没有的令牌（${hostOnly.length} 个）→ 说明 core 默认层不完整：`)
  for (const t of hostOnly.slice(0, 20)) console.log(`      ${t}  (${hostDefined.get(t)})`)
}
const shadowed = [...hostDefined.keys()].filter((t) => defined.has(t))
console.log(`  core 默认值被宿主覆盖: ${shadowed.length} 个（这是正常穿透）`)

if (unusedTokens.length) {
  console.log(`\n── ④ core 定义了但组件未用（${unusedTokens.length} 个，供宿主使用，正常） ──`)
  console.log('  ' + unusedTokens.join('  '))
}

const bad = missing.length > 0
console.log('\n' + '='.repeat(66))
console.log(bad ? '结论：✗ 存在未定义令牌，主题穿透有洞' : '结论：✓ 无未定义令牌')
console.log('='.repeat(66))
process.exit(STRICT && bad ? 1 : 0)
