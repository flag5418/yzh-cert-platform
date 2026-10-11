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
 *   B-R2  禁 `new ApiResponse(` / `new ApiResponse<T>(` / `new ApiResponse {` —— 构造已封闭，只能走静态工厂（工厂保证三条不变量）
 *   B-R3  禁业务语境 `return BadRequest(` / `return NotFound(` —— 业务失败一律 HTTP 200
 *         （401 `Unauthorized(` 属基础设施信号，保留，不在此规则内）
 *   B-R4  信封不变量运行时守卫存在性 —— ApiResponseContractFilter 已注册进全局过滤器
 *         （实际校验在运行时：success:false ⇒ err 非空 / success:true ⇒ err 空 / 失败 ⇒ message 空）
 *   B-R5  禁**丢弃** `DbOrm` 批量写入的返回值 —— `await _db.InsertBatchAsync(x);` 这种写法
 *         ⚠️ 病因：`SqlSugarDbOrm.InsertAsync / InsertBatchAsync / UpdateAsync` 三个方法的实现体
 *            都是 `try { … } catch (Exception ex) { return Result.Fail(…) }`
 *            ⇒ **失败时不抛异常、只返回失败结果**。丢弃返回值 = **静默失败**：
 *            事务照样提交，DB 里什么都没写，而调用方以为成功。
 *         ★ 真实事故（2026-10-07）：`DocumentFillOrchestrator` 的取值账本整批写入被丢弃返回值，
 *            叠加「构造行时漏赋 `Code`（DB `NOT NULL`）」⇒ `cert_doc_fill_value` **恒 0 行**，
 *            而结果对象报「取值账本 N 行」（N 取自内存 `List.Count`）—— 页面看到的是**假成功**。
 *         ★ 正确写法：`var r = await _db.InsertBatchAsync(x); if (!r.Success) throw new …(r.Error);`
 *         📏 基线：全仓 `_db/_dbOrm.InsertBatchAsync` 调用点共 3 处，规则启用时已全部收口 = 0。
 *         ⚠️ 已知局限：按行匹配。若写成「赋值换行 + 下一行 `await …`」会误报
 *            —— 本仓风格为单行赋值，误报时改成单行即可。
 *   B-R6  菜单种子 SQL 的 `ON DUPLICATE KEY UPDATE` 禁覆盖「人工可改列」`MenuName` / `IsValid`
 *         （扫描 `scripts/db/*.sql`，⛔ 不在 ROOTS 里 —— 那批只收 .cs）
 *         ★ 真实事故（2026-10-11 用户报障）：「我改了菜单名 / 禁用了菜单，跑一次 SQL 又被还原」
 *            —— 7 个 `*_menu_*.sql` 的覆盖子句里全都写着
 *            `MenuName = VALUES(MenuName)` 与 `IsValid = 1`，**每次重跑都冲掉人工改动**。
 *         ★ 判据：`MenuName`（纯展示）/ `IsValid`（纯启禁）与**路由正确性无关** ⇒ 归人工；
 *            而 `Url` / `ParentCode` 必须由脚本保证（守卫 R12 要查「路由 ↔ 菜单」一致）
 *            ⇒ ⛔ 不纳入本规则，否则种子脚本无法修正菜单接线。
 *         ⚠️ `Description` 属**边界列**（也是人工可改，但常需随职责变更修订）：
 *            本轮不拦，但修订式 `UPDATE` 应自带「旧值匹配」条件保证幂等
 *            （样板见 `20261011_menu_aud_14_V1.sql` §②）。
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
    desc: '禁 new ApiResponse( / new ApiResponse<T>( / new ApiResponse {（构造已封闭，只能用静态工厂）',
    forbid: [/new ApiResponse\s*[<({]/],
  },
  {
    id: 'B-R3',
    desc: '禁业务语境 return BadRequest( / return NotFound(（业务失败一律 HTTP 200）',
    forbid: [/return (BadRequest|NotFound)\(/],
  },
  {
    id: 'B-R5',
    desc: '禁丢弃 DbOrm 批量写入返回值（`await _db.InsertBatchAsync(x);` ⇒ 失败被静默吞掉）',
    // 行首锚定 ⇒ `var r = await _db.InsertBatchAsync(…)` 不匹配（合规写法）
    forbid: [/^\s*await\s+(?:_db|_dbOrm)\.InsertBatchAsync\(/],
  },
]

/** B-R6 扫描根：菜单种子 SQL（⛔ 不在 ROOTS 里 —— 那批只收 `.cs`） */
const MENU_SQL_DIR = join(ROOT, 'scripts/db')

/**
 * SQL 去注释（**保持行号**，否则违规行号会漂）：
 * 整行 `--` 注释 + `/* … *\/` 块注释。
 * ⚠️ 只处理**整行** `--`（行首可选空白）：行内 `--` 在 SQL 里也可能是字符串内容，
 *    误删会让规则漏报而不是误报 —— 这是刻意选的保守方向。
 */
function stripSqlComments(text) {
  return text
    .replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, ' '))
    .replace(/^[ \t]*--.*$/gm, (m) => ' '.repeat(m.length))
}

function lineOf(text, index) {
  return text.slice(0, index).split('\n').length
}

/**
 * B-R6：菜单种子 SQL 禁覆盖「人工可改列」。
 *
 * ★ 病因（2026-10-11 用户报障，实测 7 个文件全中）：
 *    `scripts/db/*_menu_*.sql` 的 `ON DUPLICATE KEY UPDATE` 里写着
 *    `` `MenuName` = VALUES(`MenuName`) `` 与 `` `IsValid` = 1 ``。
 *    这两列是**人工可改列**（库里直接改名 / 手动停用），而覆盖子句**每次重跑都执行**
 *    ⇒ 症状：「我改了菜单、被执行 sql 后又被还原了」，用户反复踩了很多次。
 *
 * ★ 判据（为什么只拦这两列）：
 *    - `MenuName`（纯展示）、`IsValid`（纯启禁）与**路由正确性无关** ⇒ 归人工；
 *    - `Url` / `ParentCode` 必须由脚本保证 —— 守卫 `R12` 要查「路由 ↔ 菜单」一致，
 *      拦掉它们会让种子脚本无法修正菜单接线（⛔ 不纳入）。
 *
 * ⚠️ 只认 `INSERT INTO Sys_Menu` 的覆盖子句；其它表（`Sys_Dictionary` / `wf_skill` …）
 *    不在本规则范围内。
 */
function checkMenuSql() {
  const violations = []
  let names = []
  try {
    names = readdirSync(MENU_SQL_DIR).filter((f) => f.endsWith('.sql'))
  } catch {
    return violations // 目录不存在 ⇒ 不阻断（本规则不是信封铁律的一部分）
  }

  /** 人工可改列 → 违规文案 */
  const PROTECTED = {
    MenuName: '重跑会把人工改的**菜单名**冲回脚本值',
    IsValid: '重跑会把人工**停用**的菜单强制启用',
  }

  for (const name of names) {
    const text = stripSqlComments(readFileSync(join(MENU_SQL_DIR, name), 'utf8'))
    // 非贪婪到第一个 `;` ⇒ 一条 INSERT 一段（本仓 SQL 的字符串字面量里没有 `;`）
    for (const m of text.matchAll(/INSERT\s+INTO\s+`?(\w+)`?([\s\S]*?);/gi)) {
      if (m[1].toLowerCase() !== 'sys_menu') continue
      const body = m[2]
      const dup = /ON\s+DUPLICATE\s+KEY\s+UPDATE/i.exec(body)
      if (!dup) continue
      const clause = body.slice(dup.index)
      for (const [col, why] of Object.entries(PROTECTED)) {
        if (new RegExp('`?' + col + '`?\\s*=', 'i').test(clause)) {
          violations.push({
            file: relative(ROOT, join(MENU_SQL_DIR, name)),
            line: lineOf(text, m.index + dup.index),
            text: `Sys_Menu 的 ON DUPLICATE KEY UPDATE 覆盖了 \`${col}\` —— ${why}`,
          })
        }
      }
    }
  }
  return violations
}

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

{
  const rule = {
    id: 'B-R6',
    desc: '菜单种子 SQL 不覆盖人工可改列（Sys_Menu 的 MenuName / IsValid）',
  }
  const violations = checkMenuSql()
  if (violations.length) failures.push({ rule, violations })
  else if (REPORT_ONLY) console.log(`✓ ${rule.id} ${rule.desc}`)
}

/** 自定义检查条数（B-R4 / B-R6）—— 与 RULES 一起计入摘要 */
const CUSTOM_CHECKS = 2

if (REPORT_ONLY) {
  console.log(`\n报告模式：${RULES.length + CUSTOM_CHECKS} 条规则 / ${scannedFiles} 个文件 / ${failures.length} 条违规规则（不阻断）`)
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

console.log(`✓ 后端信封守卫通过（${RULES.length + CUSTOM_CHECKS} 条规则 / ${scannedFiles} 个文件）`)
