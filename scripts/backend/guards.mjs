#!/usr/bin/env node
/**
 * YZH 后端信封守卫（零依赖）
 *
 * 用途：固化「接口返回」铁律（docs/10-YZH架构/22-接口返回规范-V1.md），
 *      防止 P0–P2 的收口成果回潮。
 *
 * 用法：
 *   node scripts/backend/guards.mjs            # 通过 → 1 行摘要；失败 → 明细 + exit 1
 *   node scripts/backend/guards.mjs --report   # 全量盘点，永不失败
 *
 * 规则（前后端信封统一改造计划 §P3）：
 *   B-R1  禁**裸** `Ok(new {` —— 匿名对象响应无 success/err，前端读不到判据
 *         （已包 `ApiResponse<>.Ok(new {...})` 的内层 22 处属合法：`.Ok(` 前有 `.`
 *          故被负向后顾排除；P2 已清零，P3 起基线 0）
 *   B-R2  禁 `new ApiResponse` —— 构造已封闭，只能走静态工厂（工厂保证三条不变量）
 *   B-R3  禁业务语境 `return BadRequest(` / `return NotFound(` —— 业务失败一律 HTTP 200
 *         （401 `Unauthorized(` 属基础设施信号，保留，不在此规则内）
 *   B-R4  信封不变量运行时守卫存在性 —— ApiResponseContractFilter 已注册进全局过滤器
 *         （实际校验在运行时：success:false ⇒ err 非空 / success:true ⇒ err 空 / 失败 ⇒ message 空）
 *
 * ⛔ 严禁挂到 dotnet watch / dev 启动脚本 —— 只允许挂到 build / pre-commit / 手动门禁。
 *
 * 📏 规则启用前提（铁律）：**基线必须为 0**（本脚本创建时逐条实测）。
 *
 * ⚠️ 扫描范围：src/certplatform-api/**、src/yzh-core/**（⛔ 不含 src/old/ —— 历史项目冻结）。
 */

import { readdirSync, readFileSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const REPORT_ONLY = process.argv.includes('--report')

/** 扫描根（⛔ src/old/ 永不扫描） */
const ROOTS = [join(ROOT, 'src/certplatform-api'), join(ROOT, 'src/yzh-core')]
const SKIP_DIRS = new Set(['bin', 'obj', 'node_modules', '.git'])

/** 行号保持不变地抹掉注释（否则注释里的示例会误报，如 ApiResponse.cs 第 13 行） */
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

/**
 * 规则定义
 * - id / desc: 标识与说明
 * - forbid: RegExp[]，在**去注释**文本上逐行匹配，命中即违规
 */
const RULES = [
  {
    id: 'B-R1',
    desc: '禁裸 Ok(new {（匿名对象响应无 success/err）',
    forbid: [/(?<![.\w])Ok\(new \{/],
  },
  {
    id: 'B-R2',
    desc: '禁 new ApiResponse（构造已封闭，只能用静态工厂）',
    forbid: [/new ApiResponse/],
  },
  {
    id: 'B-R3',
    desc: '禁业务语境 return BadRequest( / return NotFound(（业务失败一律 HTTP 200）',
    forbid: [/return (BadRequest|NotFound)\(/],
  },
]

/** B-R4：静态存在性检查（运行时校验由该过滤器执行） */
function checkB4() {
  const filterPath = join(ROOT, 'src/yzh-core/YZH.Core.Api/Filters/ApiResponseContractFilter.cs')
  const programPath = join(ROOT, 'src/yzh-core/YZH.Core.Web/Program.cs')
  const violations = []
  let filterOk = false
  let registerOk = false
  try {
    filterOk = readFileSync(filterPath, 'utf8').includes('class ApiResponseContractFilter')
  } catch {
    filterOk = false
  }
  try {
    registerOk = readFileSync(programPath, 'utf8').includes('ApiResponseContractFilter')
  } catch {
    registerOk = false
  }
  if (!filterOk) {
    violations.push({
      file: relative(ROOT, filterPath),
      line: 0,
      text: 'B-R4：ApiResponseContractFilter.cs 缺失或未定义 class —— 信封不变量无运行时守卫',
    })
  }
  if (!registerOk) {
    violations.push({
      file: relative(ROOT, programPath),
      line: 0,
      text: 'B-R4：Program.cs 未注册 ApiResponseContractFilter —— 信封不变量守卫未生效（全局过滤器）',
    })
  }
  return violations
}

const files = ROOTS.flatMap((r) => walk(r))
const failures = []
let scannedFiles = files.length

for (const rule of RULES) {
  const violations = []
  for (const file of files) {
    const code = stripComments(readFileSync(file, 'utf8'))
    const lines = code.split('\n')
    lines.forEach((line, i) => {
      for (const re of rule.forbid) {
        if (re.test(line)) {
          violations.push({
            file: relative(ROOT, file),
            line: i + 1,
            text: line.trim().slice(0, 120),
          })
          break
        }
      }
    })
  }
  if (violations.length) failures.push({ rule, violations })
  else if (REPORT_ONLY) console.log(`✓ ${rule.id} ${rule.desc}`)
}

{
  const rule = { id: 'B-R4', desc: '信封不变量运行时守卫（ApiResponseContractFilter）已存在并注册' }
  const violations = checkB4()
  if (violations.length) failures.push({ rule, violations })
  else if (REPORT_ONLY) console.log(`✓ ${rule.id} ${rule.desc}`)
}

if (REPORT_ONLY) {
  console.log(`\n报告模式：${RULES.length + 1} 条规则 / ${scannedFiles} 个文件 / ${failures.length} 条违规规则（不阻断）`)
  process.exit(0)
}

if (failures.length) {
  console.error('✗ 后端信封守卫未通过：\n')
  for (const { rule, violations } of failures) {
    console.error(`  [${rule.id}] ${rule.desc} —— ${violations.length} 处`)
    for (const v of violations.slice(0, 30)) {
      console.error(`      ${v.file}:${v.line}  ${v.text}`)
    }
    if (violations.length > 30) console.error(`      …… 其余 ${violations.length - 30} 处省略`)
    console.error('')
  }
  process.exit(1)
}

console.log(`✓ 后端信封守卫通过（${RULES.length + 1} 条规则 / ${scannedFiles} 个文件）`)
