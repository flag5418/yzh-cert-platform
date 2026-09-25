#!/usr/bin/env node
/**
 * 前端引用审计：找出「零真实消费方」模块（区分 barrel 再导出 vs 真实使用）
 *
 * 用途：判定死代码 / 能力储备的**第一道证据**。
 *   ⚠️ 判死铁律（V3/V4 教训）：零引用 ≠ 死代码。判死必须双问：
 *      ① 属不属于框架层能力（对照 YZH.WPF.Core 分层）
 *      ② 有没有明确替代物
 *   本脚本只回答「有没有人引用」，**不回答「该不该删」**。
 *
 * ⚠️ 已知局限：消费方从 barrel（index.ts）导入时，引用边指向 barrel 而非源文件，
 *   因此**必须对候选逐条做符号级 grep 复核**（例：utils/apiResponse.ts 的 unwrap
 *   被 api/system/role-api.ts 经 utils barrel 使用，本脚本会误报为零消费方）。
 *
 * 用法: node scripts/frontend/ref-audit.mjs
 */
import { readdirSync, readFileSync, statSync } from 'node:fs'
import { join, relative, resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'

const WEB = resolve(dirname(fileURLToPath(import.meta.url)), '../../src/certplatform-web')
const ROOTS = [
  join(WEB, 'yzh.vue.core/src'),
  join(WEB, 'cert/cert-share/src'),
  join(WEB, 'cert/cert-admin/src'),
  join(WEB, 'cert/cert-auditor/src'),
  join(WEB, 'cert/cert-enterprise/src'),
]

function walk(dir, out = []) {
  let entries
  try { entries = readdirSync(dir) } catch { return out }
  for (const name of entries) {
    const full = join(dir, name)
    let st
    try { st = statSync(full) } catch { continue }
    if (st.isDirectory()) {
      if (name === 'node_modules' || name === 'dist' || name === '.git') continue
      walk(full, out)
    } else if (/\.(ts|vue)$/.test(name) && !name.endsWith('.d.ts')) {
      out.push(full)
    }
  }
  return out
}

const all = ROOTS.flatMap((r) => walk(r))
const contents = new Map()
for (const f of all) contents.set(f, readFileSync(f, 'utf8'))

const rel = (p) => relative(WEB, p).replaceAll('\\', '/')

/** 判定该文件是否为 barrel（纯再导出） */
function isBarrel(text) {
  const lines = text.split('\n').map((l) => l.trim()).filter(Boolean)
  const exportLines = lines.filter((l) => l.startsWith('export'))
  if (exportLines.length === 0) return false
  return exportLines.every((l) => /^export\s*(\*|\{[^}]*\})\s*from/.test(l))
}

/** 从文件中提取它引用的"本地模块 basename"（不含自身） */
function extractRefs(text, selfPath) {
  const set = new Set()
  const re = /from\s+['"]([^'"]+)['"]/g
  let m
  while ((m = re.exec(text))) {
    const spec = m[1]
    if (spec.startsWith('.') || spec.startsWith('@yzh-core') || spec.startsWith('@share') || spec.startsWith('@/')) {
      set.add(spec)
    }
  }
  return set
}

/** 把 import spec 解析为绝对路径候选 */
function resolveSpec(spec, fromFile) {
  const cands = []
  if (spec.startsWith('.')) {
    cands.push(resolve(join(fromFile, '..', spec)))
  } else if (spec.startsWith('@yzh-core')) {
    const rest = spec.slice('@yzh-core'.length).replace(/^\//, '')
    cands.push(join(WEB, 'yzh.vue.core/src', rest))
  } else if (spec.startsWith('@share')) {
    const rest = spec.slice('@share'.length).replace(/^\//, '')
    cands.push(join(WEB, 'cert/cert-share/src', rest))
  } else if (spec.startsWith('@/')) {
    const rest = spec.slice(2)
    cands.push(join(fromFile.slice(0, fromFile.indexOf('/src/') + 5), rest))
  }
  return cands
}

const EXT = ['', '.ts', '.vue', '/index.ts', '/index.vue']

/** 统计每个文件被哪些"非 barrel"文件引用 */
const consumers = new Map()
for (const f of all) consumers.set(f, new Set())

for (const [file, text] of contents) {
  const refs = extractRefs(text, file)
  const selfIsBarrel = isBarrel(text)
  for (const spec of refs) {
    for (const base of resolveSpec(spec, file)) {
      for (const e of EXT) {
        const target = base + e
        if (contents.has(target) && target !== file) {
          const c = consumers.get(target)
          if (c) c.add(selfIsBarrel ? `[barrel]${rel(file)}` : rel(file))
        }
      }
    }
  }
}

/** 候选清单：core 全部文件 + 行数 */
const rows = []
for (const f of all) {
  if (!f.includes('yzh.vue.core/src')) continue
  // pages/ 与 layouts/ 由 router 动态 import() 注册，非死代码，排除
  if (/\/pages\//.test(f) || /\/layouts\//.test(f)) continue
  const text = contents.get(f)
  if (isBarrel(text)) continue
  const cons = consumers.get(f)
  const real = [...cons].filter((c) => !c.startsWith('[barrel]'))
  const barrels = [...cons].filter((c) => c.startsWith('[barrel]'))
  const lines = text.split('\n').length
  rows.push({ file: rel(f), lines, real: real.length, barrel: barrels.length, consumers: real.slice(0, 3) })
}

rows.sort((a, b) => (a.real - b.real) || (b.lines - a.lines))

console.log('=== 零真实消费方（real=0）===')
for (const r of rows) {
  if (r.real === 0) {
    console.log(`${String(r.lines).padStart(5)} 行  barrel=${r.barrel}  ${r.file}`)
  }
}

console.log('\n=== 仅 1 个真实消费方 ===')
for (const r of rows) {
  if (r.real === 1) {
    console.log(`${String(r.lines).padStart(5)} 行  ${r.file}  <- ${r.consumers[0]}`)
  }
}

const zeroLines = rows.filter((r) => r.real === 0).reduce((n, r) => n + r.lines, 0)
console.log(`\n合计：${all.length} 个源文件；零真实消费方 ${rows.filter((r) => r.real === 0).length} 个 / ${zeroLines} 行`)
