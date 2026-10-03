#!/usr/bin/env node
/**
 * YZH 前端架构守卫（零依赖）
 *
 * 用途：固化架构铁律，防止"改完又回潮"。
 *
 * 用法：
 *   node scripts/guards.mjs            # 通过 → 1 行摘要；失败 → 明细 + exit 1
 *   node scripts/guards.mjs --report   # 全量盘点（含债务清单），永不失败
 *
 * ⛔ 严禁挂到 dev 脚本（vite）——每次热更新都会刷屏，干扰调试。
 *    只允许挂在三处：build / pre-commit / 手动 `npm run guard`。
 *
 * 🔇 零噪音原则（2026-09-23 定稿）：
 *    1. 通过 → 只输出 1 行，不产生任何 WARN / 明细
 *    2. 失败 → 只输出失败规则及其定位，不输出通过项
 *    3. 债务盘点（已知待修项）→ 仅 --report 可见，绝不出现在日常构建中
 *    目的：让守卫的输出「要么为空，要么全是真问题」，避免调试时误判。
 *
 * 📏 规则启用前提（铁律）：**基线必须为 0**。
 *    历史存量走 debt 白名单豁免，随修复逐条删除；删完即代表全站达标。
 *    基线不为 0 的规则若强行启用，报错会被存量淹没 → 规则立即失效。
 *
 * ⚠️ 扫描范围：cert-admin / cert-auditor / cert-enterprise / cert-share / yzh.vue.core(新层)。
 *    新增端时必须在 PAGE_ROOTS / API_ROOTS 登记，否则该端处于守卫盲区。
 *    yzh.vue.core 的 pages/ layouts/ 属页面层、api/ 属 API 层，纳入同一套规则；
 *    components/ 仍由 R1 单独圈定（零领域依赖）。
 */

import { existsSync, readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs'
import { basename, dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const WEB = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const REPORT_ONLY = process.argv.includes('--report')

/** 原子组件根（零领域依赖） */
const CORE = join(WEB, 'yzh.vue.core/src/components')

/** 页面扫描根 —— 新增端必须在此登记 */
const PAGE_ROOTS = [
  join(WEB, 'cert/cert-admin/src/pages'),
  join(WEB, 'cert/cert-auditor/src/pages'),
  join(WEB, 'cert/cert-enterprise/src/pages'),
  // 系统底座包新层（原子界面）：pages/ 与 layouts/ 按页面层规则约束
  join(WEB, 'yzh.vue.core/src/pages'),
  join(WEB, 'yzh.vue.core/src/layouts'),
]

/** API 模块扫描根 —— 新增端必须在此登记 */
const API_ROOTS = [
  join(WEB, 'cert/cert-admin/src/api'),
  join(WEB, 'cert/cert-auditor/src/api'),
  join(WEB, 'cert/cert-share/src/api'),
  // 系统底座包原子 API（含 api/system/）
  join(WEB, 'yzh.vue.core/src/api'),
]

/** yzh.vue.core 新层（原子路由/界面/API）——禁宿主反向依赖（R10） */
const CORE_APP_ROOTS = [
  join(WEB, 'yzh.vue.core/src/pages'),
  join(WEB, 'yzh.vue.core/src/layouts'),
  join(WEB, 'yzh.vue.core/src/router'),
  join(WEB, 'yzh.vue.core/src/composables'),
  join(WEB, 'yzh.vue.core/src/api'),
]

/** yzh.vue.core 全源码 —— 禁硬编码后端地址（R11） */
const CORE_ALL = join(WEB, 'yzh.vue.core/src')

/** 后端业务源码根（R-A/R-B/R-C/R-D/R-E 用；⛔ 不含 src/old 历史项目） */
const API_SRC = resolve(WEB, '../certplatform-api')

/** 框架层源码根（R-D/R-E 覆盖；R-A 的「服务层」口径只管业务服务） */
const YZH_CORE_SRC = resolve(WEB, '../yzh-core')

/** 递归收集文件（目录不存在时返回空数组） */
function walk(dir, exts, out = []) {
  let entries
  try {
    entries = readdirSync(dir)
  } catch {
    return out
  }
  for (const name of entries) {
    const full = join(dir, name)
    const st = statSync(full)
    if (st.isDirectory()) {
      if (name === 'node_modules' || name === '.git') continue
      walk(full, exts, out)
    } else if (exts.some((e) => name.endsWith(e))) {
      out.push(full)
    }
  }
  return out
}

/** 多根扫描 */
function walkAll(dirs, exts) {
  return dirs.flatMap((d) => walk(d, exts))
}

const rel = (p) => relative(WEB, p).replaceAll('\\', '/')

/** 是否为行注释（含块注释行内的 * 行） */
function isCommentLine(line) {
  const t = line.trim()
  return t.startsWith('*') || t.startsWith('//') || t.startsWith('/*')
}

/* ==========================================================================
 * ★ R12 数据源 —— 路由 ↔ 菜单一致性（交叉校验，非"文件 + 正则"型）
 *
 * 事实源：
 *   ① 菜单快照 `scripts/db/verify/menu-urls.tsv`
 *      （由 `scripts/db/verify/sync_menu_urls.sh` 从 `Sys_Menu` 只读导出）
 *   ② 各端前端路由表（含 yzh.vue.core 的原子路由，因宿主用 `...yzhSystemRoutes` 展开）
 *
 * 为什么用快照而不是直连数据库：守卫必须能在**无数据库**环境（CI / 干净检出）运行。
 * 代价：菜单变更后须重跑 sync 脚本，否则守卫基于过期数据。
 * ========================================================================== */

/** 菜单快照路径（WEB = src/certplatform-web，故回退两级到仓库根） */
const MENU_SNAPSHOT = resolve(WEB, '../../scripts/db/verify/menu-urls.tsv')

/** 各端路由源文件（抽取 `path:` 用） */
const ROUTE_SOURCES = {
  admin: [
    join(WEB, 'cert/cert-admin/src/router/index.ts'),
    join(WEB, 'yzh.vue.core/src/router/index.ts'),
  ],
  auditor: [join(WEB, 'cert/cert-auditor/src/router/index.ts')],
}

/** 非菜单路由（登录 / 注册 / 应用壳首页）—— 结构性豁免，永远不需要菜单入口 */
const ROUTE_EXEMPT = new Set(['/login', '/register', '/'])

/**
 * 允许「无菜单入口」的路由（显式登记，**默认必须为空**）。
 * 用途：详情页 / 嵌入页等确实不应出现在侧边栏的路由。
 * 登记格式：'admin:/enterprise/detail' —— 必须同时在注释里写明理由。
 */
const ORPHAN_ALLOW = new Set([
  // ── 专家端 · 任务系统（2026-09-30）──
  // 理由：这两条是「任务中心 `/tasks`」的**子页面**，不是独立功能入口。
  //   入口在 `/tasks` 列表页的按钮上（工具栏「创建新任务」/ 行按钮「详情」），
  //   以及结果页「看结果」的跳转。放进侧边栏反而会让菜单变成「同一功能的 3 个入口」。
  //   菜单侧只需 `MENU_AUD_03`（/tasks）一条。
  'auditor:/tasks/create',
  'auditor:/tasks/:code',
])

/** 从路由源文件抽取「绝对 path」集合（相对子路由按 shell 前缀 '/' 归一） */
function extractRoutePaths(files) {
  const out = new Set()
  for (const f of files) {
    let src
    try {
      src = readFileSync(f, 'utf8')
    } catch {
      continue
    }
    for (const m of src.matchAll(/path:\s*'([^']*)'/g)) {
      const p = m[1]
      if (!p || p === '/') continue
      out.add(p.startsWith('/') ? p : '/' + p)
    }
  }
  return out
}

/** 读取菜单快照 → { tag: Set<url> }；文件缺失返回 null */
function readMenuSnapshot() {
  let src
  try {
    src = readFileSync(MENU_SNAPSHOT, 'utf8')
  } catch {
    return null
  }
  const byTag = {}
  for (const line of src.split('\n')) {
    if (!line || line.startsWith('#')) continue
    const [tag, , url] = line.split('\t')
    if (!url || url === '/') continue // '/' 是分类节点（侧边栏分组容器，不落地页面）
    ;(byTag[tag] ??= new Set()).add(url)
  }
  return byTag
}

/** 执行 R12 校验，返回 { violations, skipped } */
function checkRouteMenuConsistency() {
  const snapshot = readMenuSnapshot()
  if (!snapshot) return { violations: [], skipped: true }

  const violations = []
  for (const [tag, files] of Object.entries(ROUTE_SOURCES)) {
    const menuUrls = snapshot[tag] ?? new Set()
    const routePaths = extractRoutePaths(files)

    // ① 菜单有、路由无 → 阻断：点击菜单白屏 / 404
    for (const url of [...menuUrls].sort()) {
      if (!routePaths.has(url)) {
        violations.push({
          file: 'menu-urls.tsv',
          line: 0,
          text: `[${tag}] 菜单 Url 无对应路由 → 点击必白屏：${url}`,
        })
      }
    }
    // ② 路由有、菜单无 → 阻断：孤儿路由（只能手输 URL 到达）
    for (const p of [...routePaths].sort()) {
      if (ROUTE_EXEMPT.has(p)) continue
      if (ORPHAN_ALLOW.has(`${tag}:${p}`)) continue
      if (!menuUrls.has(p)) {
        violations.push({
          file: `${tag} 路由表`,
          line: 0,
          text: `[${tag}] 路由无菜单入口（孤儿路由）：${p}`
            + ` —— 补菜单，或从路由表删除，或登记进 guards.mjs 的 ORPHAN_ALLOW`,
        })
      }
    }
  }
  return { violations, skipped: false }
}

/** ── 信封统一 P3 / F3 自定义规则（R14–R16：需跨行上下文，走 type:'custom'） ── */

const CUSTOM_ROOTS = [
  ...PAGE_ROOTS,
  ...API_ROOTS,
  // 业务共享层（组件/composables）—— WorkflowDesigner 等在此，必须纳入信封防回潮面
  join(WEB, 'cert/cert-share/src'),
  CORE_ALL,
]
const CUSTOM_EXTS = ['.ts', '.vue']

/** 取文本行号（1-based） */
function lineOf(text, index) {
  return text.slice(0, index).split('\n').length
}

/**
 * R14 空 catch 吞错 —— 只抓「try 块内含 await」的（真 I/O 失败被吞）。
 * 纯同步的防御性空 catch（如 setSelectionRange 兼容、画布清理）不在此列。
 */
function runR14() {
  const violations = []
  for (const file of walkAll(CUSTOM_ROOTS, CUSTOM_EXTS)) {
    const text = readFileSync(file, 'utf8')
    const tryBraces = [...text.matchAll(/\btry\s*\{/g)].map((m) => m.index + m[0].length - 1)
    const re = /catch\s*(?:\([^)]*\))?\s*\{\s*\}/g
    let m
    while ((m = re.exec(text)) !== null) {
      const catchIdx = m.index
      const braceIdx = [...tryBraces].reverse().find((b) => b < catchIdx)
      if (braceIdx === undefined) continue
      let depth = 0
      let end = -1
      for (let i = braceIdx; i < text.length; i++) {
        const c = text[i]
        if (c === '{') depth++
        else if (c === '}') {
          depth--
          if (depth === 0) { end = i; break }
        }
      }
      if (end < 0 || end > catchIdx) continue // catch 不属于这个 try
      if (!/\bawait\b/.test(text.slice(braceIdx, end))) continue // 非 I/O，豁免
      violations.push({
        file: rel(file),
        line: lineOf(text, catchIdx),
        text: text.slice(catchIdx, catchIdx + 40).split('\n')[0],
      })
    }
  }
  return { violations, skipped: false }
}

/**
 * R15 ElMessageBox.confirm 无 catch（文件内一个 catch 都没有 → 确认后失败无人提示）。
 * 近似口径：文件级「有 confirm 且无 catch」，基线 0。
 */
function runR15() {
  const violations = []
  for (const file of walkAll(CUSTOM_ROOTS, CUSTOM_EXTS)) {
    const text = readFileSync(file, 'utf8')
    if (!/ElMessageBox\.confirm/.test(text)) continue
    if (/\bcatch\b/.test(text)) continue
    violations.push({
      file: rel(file),
      line: lineOf(text, text.indexOf('ElMessageBox.confirm')),
      text: '文件内 ElMessageBox.confirm 无任何 catch —— 确认后的失败会被静默吞掉',
    })
  }
  return { violations, skipped: false }
}

/**
 * R16 ElMessage.success 出现在 expectOk 之前（先弹成功、后校验信封 → 失败也已报喜）。
 * 近似口径：同文件顺序，基线 0。
 */
function runR16() {
  const violations = []
  for (const file of walkAll(CUSTOM_ROOTS, CUSTOM_EXTS)) {
    const text = readFileSync(file, 'utf8')
    const si = text.indexOf('ElMessage.success')
    if (si < 0) continue
    const ei = text.search(/expectOk|unwrapOk/)
    if (ei < 0) continue
    if (si < ei) {
      violations.push({
        file: rel(file),
        line: lineOf(text, si),
        text: 'ElMessage.success 出现在 expectOk/unwrapOk 之前 —— 信封校验前就报成功',
      })
    }
  }
  return { violations, skipped: false }
}

/* ==========================================================================
 * ★ R-A ~ R-E —— 标准目录链路审计（docs/50-任务/分析报告/标准目录链路-逻辑缺陷审计-V1.md §8）
 * 均为后端 .cs 扫描；历史存量走 debt 豁免，随修复逐条删除。
 * ========================================================================== */

/**
 * R-A 服务层禁止单参 `UpdateAsync(entity)`（全列写回 = 陷阱 ㉑ / P1-14）。
 * 判据：`UpdateAsync(X)` 且括号内无逗号；多参 `UpdateAsync(entity, fields…)` 放行。
 * 口径：仅 `certplatform-api/**\/Services/**`（框架 QueueManager 等框架内部状态机不在此列）。
 */
function runRA() {
  const violations = []
  const servicesRoot = walkAll([API_SRC], ['.cs']).filter((f) => f.includes('/Services/'))
  for (const file of servicesRoot) {
    const lines = readFileSync(file, 'utf8').split('\n')
    lines.forEach((line, i) => {
      if (isCommentLine(line)) return
      if (/\.\s*UpdateAsync\s*\([^,)]*\)/.test(line)) {
        violations.push({ file: rel(file), line: i + 1, text: line.trim().slice(0, 120) })
      }
    })
  }
  return { violations, skipped: false }
}

/**
 * R-B 控制器禁止直接继承裸 `ControllerBase`（P0-4）。
 * 白名单：Auth / 健康检查（无实体、无接口授权语义）。
 * 正确姿势：有实体 → `YzhControllerBase<V>`；无实体 → `WebControllerBase`（自带 [YZHAuthorize]）。
 */
function runRB() {
  const violations = []
  const ctrlFiles = walkAll([API_SRC, YZH_CORE_SRC], ['.cs']).filter((f) => f.includes('/Controllers/'))
  for (const file of ctrlFiles) {
    const base = file.split('/').pop()
    if (/Auth|Health/i.test(base)) continue
    const text = readFileSync(file, 'utf8')
    const m = text.match(/class\s+\w+Controller\s*:\s*ControllerBase\b/)
    if (m) {
      violations.push({
        file: rel(file),
        line: lineOf(text, m.index),
        text: m[0],
      })
    }
  }
  return { violations, skipped: false }
}

/**
 * R-C 树/图递归遍历必须带 `visited` 或深度上限（P0-6，环状 ParentCode → StackOverflow 崩进程）。
 * 判据：方法体含 `ParentCode ==` 且**自调用 ≥ 2 次**（声明 + 递归），却无 visited / depth / MAX_TREE。
 */
function runRC() {
  const violations = []
  for (const file of walkAll([API_SRC], ['.cs'])) {
    const text = readFileSync(file, 'utf8')
    // 方法签名行（访问修饰符开头 + `{` 收尾）→ 切块
    const sigRe = /^[ \t]*(?:public|private|protected|internal)[^\n=;{}]*?\s(\w+)\s*\([^;{]*\)\s*(?:where[^\n{]*)?\{/gm
    const sigs = []
    let m
    while ((m = sigRe.exec(text)) !== null) sigs.push({ name: m[1], index: m.index })
    if (sigs.length < 2) continue
    for (let i = 0; i < sigs.length; i++) {
      const start = sigs[i].index
      const end = i + 1 < sigs.length ? sigs[i + 1].index : text.length
      const chunk = text.slice(start, end)
      if (!/ParentCode\s*==/.test(chunk)) continue
      // 只认「无前缀的自调用」——`base.Foo(` / `x.Foo(` 不算递归（否则 base.DeleteCore 会误报）
      const calls = chunk.match(new RegExp(`(?<!\\.)\\b${sigs[i].name}\\s*\\(`, 'g'))?.length ?? 0
      if (calls < 2) continue
      if (/visited|MAX_TREE|\bdepth\b/i.test(chunk)) continue
      violations.push({ file: rel(file), line: lineOf(text, start), text: `递归方法 ${sigs[i].name} 缺 visited / 深度上限` })
    }
  }
  return { violations, skipped: false }
}

/**
 * R-D 禁止 `return (true, <非空 error>, …)` 形态（P1-10：ok=true 却带 error，调用方按 ok 分支 → 消息被静默丢弃）。
 * 判据：三元及以上元组、第 2 个元素是字符串字面量或含 error/msg/message 的标识符。
 */
function runRD() {
  const violations = []
  const re = /return\s*\(\s*true\s*,\s*(\$?"[^"]*"|'[^']*'|\w*(?:[Ee]rror|[Mm]sg|[Mm]essage)\w*)\s*,/
  for (const file of walkAll([API_SRC, YZH_CORE_SRC], ['.cs'])) {
    const lines = readFileSync(file, 'utf8').split('\n')
    lines.forEach((line, i) => {
      if (isCommentLine(line)) return
      const m = line.match(re)
      if (m) violations.push({ file: rel(file), line: i + 1, text: line.trim().slice(0, 120) })
    })
  }
  return { violations, skipped: false }
}

/**
 * R-E 禁止在 `catch` 块内写文件到导出/产物目录（P1-18：下载失败写占位文件 → ZIP「成功」却内容是假的）。
 * 判据：`catch` 开始的 1200 字符窗口内出现 `File.WriteAllText*`。
 */
function runRE() {
  const violations = []
  const re = /catch\s*(?:\([^)]*\))?\s*\{[^}]{0,1200}?File\.WriteAll/g
  for (const file of walkAll([API_SRC, YZH_CORE_SRC], ['.cs'])) {
    const text = readFileSync(file, 'utf8')
    let m
    while ((m = re.exec(text)) !== null) {
      violations.push({ file: rel(file), line: lineOf(text, m.index), text: 'catch 块内写入文件（疑似导出占位文件）' })
    }
  }
  return { violations, skipped: false }
}

// ── R17：后端实体归属一致性（2026-10-03 用户指出「专家系统实体一个都没有」后新增）──
//
//   背景：曾把 77 个实体全部塞在 CertPlatform.Shared/Entities/，无法回答
//   「哪些表属于后台、哪些属于专家端」；且铁律九守卫 R7 只扫 Shared 一个目录。
//   修订后的规则（docs/10-YZH架构/24-后端实体归属清单-V1.md §一）：
//     · 单端独占 → CertPlatform.{Admin,Auditor}.Entities/
//     · 双端共用 → CertPlatform.Shared/Entities/
//
//   判据是「谁真的读这张表」而非「表名像什么」：统计每个带 [SugarTable] 的实体在
//   Admin / Auditor 两端的引用次数（跳过定义文件自身、跳过 bin/obj/node_modules）。
const API_ROOT = resolve(WEB, '../certplatform-api')
/** 实体**定义**目录（归属判定的依据） */
const ENTITY_DIRS = {
  Shared: join(API_ROOT, 'CertPlatform.Shared/Entities'),
  Admin: join(API_ROOT, 'CertPlatform.Admin/Entities'),
  Auditor: join(API_ROOT, 'CertPlatform.Auditor/Entities'),
}
/** 引用**扫描**目录 —— ⚠️ 是整个项目而非 Entities/ 子目录：
 *  真正的引用在 Controllers/ 与 Services/ 下，只扫 Entities/ 会得出「零引用」的假结论。 */
const PROJECT_DIRS = {
  Shared: join(API_ROOT, 'CertPlatform.Shared'),
  Admin: join(API_ROOT, 'CertPlatform.Admin'),
  Auditor: join(API_ROOT, 'CertPlatform.Auditor'),
  Tests: join(API_ROOT, 'CertPlatform.Admin.Tests'),
}
const SKIP_DIRS = new Set(['obj', 'bin', 'node_modules', '.git', 'Migrations'])

function listCsFiles(dir) {
  if (!existsSync(dir)) return []
  const out = []
  const rec = (d) => {
    for (const e of readdirSync(d, { withFileTypes: true })) {
      if (SKIP_DIRS.has(e.name)) continue
      const p = join(d, e.name)
      if (e.isDirectory()) rec(p)
      else if (e.name.endsWith('.cs')) out.push(p)
    }
  }
  rec(dir)
  return out
}

function countRefs(className, roots) {
  let n = 0
  const re = new RegExp('\\b' + className + '\\b', 'g')
  for (const r of roots) {
    for (const f of listCsFiles(r)) {
      if (basename(f) === className + '.cs') continue // 不算自身定义
      // ⚠️ 必须排除注释：文档注释里常写「该实体在 Xxx 项目里」「若日后迁进 Admin 需移到…」，
      //    那只是**说明归属**，不是代码引用。数进去会把「注释里提到」误判成「双端共用」。
      //    实测：SrcGlobalParamSkill.cs 的类注释里 3 处提到 FillParamValue，被误报成分层倒退。
      const code = readFileSync(f, 'utf8')
        .split('\n')
        .filter((line) => !isCommentLine(line))
        .join('\n')
      const m = code.match(re)
      if (m) n += m.length
    }
  }
  return n
}

function runR17() {
  const violations = []

  for (const [owner, dir] of Object.entries(ENTITY_DIRS)) {
    for (const file of listCsFiles(dir)) {
      const cls = basename(file, '.cs')
      const text = readFileSync(file, 'utf8')
      const st = text.indexOf('SugarTable')
      if (st < 0) continue // 纯常量类 / DTO / enum 不参与归属判定

      const nAdmin = countRefs(cls, [PROJECT_DIRS.Admin, PROJECT_DIRS.Tests])
      const nAuditor = countRefs(cls, [PROJECT_DIRS.Auditor])
      const both = nAdmin > 0 && nAuditor > 0

      if (owner !== 'Shared' && both) {
        // ★ 硬错误：已下沉到单端目录却被两端共用 = 分层倒退（0 容忍）
        violations.push({
          file: rel(file),
          line: lineOf(text, st),
          text: `实体在 ${owner} 端目录，却被两端同时引用（Admin=${nAdmin} / Auditor=${nAuditor}）—— 双端共用必须放 Shared/Entities（分层倒退）`,
        })
      } else if (owner === 'Shared' && !both && nAdmin + nAuditor > 0) {
        // 提示级：Shared 里只有一端用 ⇒ 应下沉（走 debt 逐条清理）
        const target = nAdmin > 0 ? 'Admin' : 'Auditor'
        violations.push({
          file: rel(file),
          line: lineOf(text, st),
          text: `实体在 Shared/Entities 但只有 ${target} 端引用（Admin=${nAdmin} / Auditor=${nAuditor}）—— 应下沉到 CertPlatform.${target}.Entities/（24 号 §一）`,
        })
      }
      // 两端都 0 ⇒ 死实体，另立清单（24 号 §四），本规则不管
    }
  }
  return { violations, skipped: false }
}

/* ==========================================================================
 * ★ R18 —— 硬编码样式守卫（docs/10-YZH架构/25-样式规范与硬编码治理-V1.md §五 P0）
 *
 * 三层判据（一个计数器，两类定位）：
 *   ① 模板内联色：`style="…#hex…"` / `color="#hex"` 属性
 *   ② <script> 传色：JS 字面量 `'#hex'`（绕过 CSS 层）
 *   ③ <style> 裸 hex：不在 `var(--x, #hex)` 兜底里的 hex
 *
 * 「只准减不准增」= 基线文件 `scripts/style-baseline.json`（file → 违规数）：
 *   - 文件不在基线 且 违规数 > 0  ⇒ 违规（新文件必须干净）
 *   - 文件在基线 且 违规数 > 基线 ⇒ 违规（回潮）
 *   - 违规数 < 基线               ⇒ 放行（进度），--report 提示可收基线
 *
 * 收基线：`node scripts/guards.mjs --update-style-baseline`
 * ⚠️ 收基线只应在**批量消毒完成后**执行一次，不得用来掩盖新增违规。
 * ========================================================================== */

/** 样式基线文件（相对 WEB） */
const STYLE_BASELINE = join(WEB, 'scripts/style-baseline.json')

/** 扫描根：所有会写样式的地方（新增端必须登记） */
const STYLE_ROOTS = [
  join(WEB, 'cert/cert-admin/src'),
  join(WEB, 'cert/cert-auditor/src'),
  join(WEB, 'cert/cert-share/src'),
  join(WEB, 'yzh.vue.core/src'),
]
const STYLE_EXTS = ['.vue', '.ts']

/** 允许保留 hex 的文件（图表/画布类：颜色是数据的一部分，改令牌反而丢语义；25 号 §四 豁免） */
const STYLE_ALLOW = [
  'cert-share/src/components/workflow/WorkflowDesigner.vue', // LogicFlow 画布节点/连线/面板
  'cert-share/src/composables/workflow/compiler.ts', // LogicFlow 连线 stroke（canvas 内消费）
  'cert-share/src/composables/workflow/specialNodes.ts', // LogicFlow 节点主题色（canvas 内消费）
  'cert/cert-admin/src/pages/workflow/ai-usage/index.vue', // ECharts itemStyle/areaStyle（canvas 内消费）
]

/** 去掉 var(--x, #hex) 兜底与注释，剩下的 hex 才是「裸硬编码」 */
function stripStyleNoise(text) {
  return text
    .replace(/\/\*[\s\S]*?\*\//g, '') // 块注释
    .replace(/(^|[^:'"`\\])\/\/.*$/gm, '$1') // 行注释（避开 https://）
    .replace(/var\(\s*--[\w-]+\s*,\s*[^)]*\)/g, 'var(--tok)') // 令牌兜底
}

/** 统计一个文件里的裸硬编码（返回 {n, samples}）
 *  = 裸 hex + 字号/间距裸 px（font-size / padding / margin 必须走 --yzh-font-size-* / --yzh-space-*）
 *  ⚠️ width/height/border-radius 不计入（结构尺寸，令牌尚未覆盖全部场景） */
function countStyleHits(text) {
  const clean = stripStyleNoise(text)
  const samples = []
  const push = (s) => {
    if (s && samples.length < 3 && !samples.includes(s)) samples.push(s)
  }
  const hexRe = /#[0-9a-fA-F]{3,8}\b/g
  let n = (clean.match(hexRe) || []).length
  for (const m of clean.matchAll(hexRe)) push(m[0])

  const pxRe = /(?:^|[^-\w])(font-size|padding|padding-(?:top|right|bottom|left)|margin|margin-(?:top|right|bottom|left))\s*:\s*[^;{}]*?\b(\d+(?:\.\d+)?)px/g
  for (const m of clean.matchAll(pxRe)) {
    n++
    push(`${m[1]}:${m[2]}px`)
  }
  return { n, samples }
}

function runR18() {
  const violations = []
  const baseline = existsSync(STYLE_BASELINE)
    ? JSON.parse(readFileSync(STYLE_BASELINE, 'utf8'))
    : {}

  if (process.argv.includes('--update-style-baseline')) {
    // 全量重算并写回（仅在批量消毒后手动执行）
    const next = {}
    for (const file of walkAll(STYLE_ROOTS, STYLE_EXTS)) {
      const r = rel(file)
      if (STYLE_ALLOW.some((a) => r.endsWith(a))) continue
      const { n } = countStyleHits(readFileSync(file, 'utf8'))
      if (n > 0) next[r] = n
    }
    writeFileSync(STYLE_BASELINE, JSON.stringify(next, null, 2) + '\n', 'utf8')
    console.log(`✓ 样式基线已更新：${Object.keys(next).length} 个文件 / ${Object.values(next).reduce((a, b) => a + b, 0)} 处`)
    process.exit(0)
  }

  for (const file of walkAll(STYLE_ROOTS, STYLE_EXTS)) {
    const r = rel(file)
    if (STYLE_ALLOW.some((a) => r.endsWith(a))) continue
    const { n, samples } = countStyleHits(readFileSync(file, 'utf8'))
    if (n === 0) continue
    const base = baseline[r] ?? 0
    if (n > base) {
      violations.push({
        file: r,
        line: 0,
        text:
          `裸硬编码样式 ${n} 处（基线 ${base}）：${samples.join(' ')} … —— ` +
          `颜色改 var(--yzh-*, fallback)、字号/间距改 var(--yzh-font-size-*|space-*)（25 号 §四 映射表）`,
      })
    }
  }
  return { violations, skipped: false }
}

/* ==========================================================================
 * ★ R19 —— 结构性 CSS 门禁（stylelint，基线 0）
 *
 * 配置：`src/certplatform-web/.stylelintrc.js`（规则须保持现状 0 违规，新增即红）
 * 分工：颜色/字号/间距硬编码归 R18；重复选择器/降序特异性/未知单位/未知函数/弃用关键字归本条。
 *
 * 依赖：node_modules/.bin/stylelint（devDep，未安装则跳过并告警，不阻断提交）
 * ========================================================================== */

function runR19() {
  const bin = join(WEB, 'node_modules/.bin/stylelint')
  if (!existsSync(bin)) return { violations: [], skipped: true }

  const res = spawnSync(
    bin,
    ['--formatter=json', '{cert,yzh.vue.core}/src/**/*.{vue,css}'],
    { cwd: WEB, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 },
  )
  // stylelint 的 JSON 输出走 stderr；stdout 只在彻底崩掉时才有内容
  const raw = (res.stderr || '').trim() || (res.stdout || '').trim()
  if (!raw) return { violations: [], skipped: false }

  let out
  try {
    out = JSON.parse(raw)
  } catch {
    return {
      violations: [{ file: 'stylelint', line: 0, text: `stylelint 输出无法解析：${raw.slice(0, 200)}` }],
      skipped: false,
    }
  }

  const violations = []
  for (const f of out) {
    for (const w of f.warnings || []) {
      violations.push({
        file: rel(f.source),
        line: w.line,
        text: `${w.text} —— 规则 ${w.rule}`,
      })
    }
    for (const e of f.parseErrors || []) {
      violations.push({ file: rel(f.source), line: e.line, text: `解析失败：${e.text}` })
    }
  }
  return { violations, skipped: false }
}

/* ==========================================================================
 * ★ R20 —— 样式法条守卫（25 号 §四 S03–S11，基线只准减不准增）
 *
 * 分工：R18 = S01/S02（颜色·字号·间距裸硬编码）；R19 = 结构性 CSS；
 *      R20 = 组件写法条款（按钮/空态/确认框/弹窗/标签/图标/布局/!important）。
 *
 * 基线文件 `scripts/style-clause-baseline.json`：
 *   { "<条款>": { "<rel 路径>": 处数 } }
 *   - 文件不在基线且处数 > 0  ⇒ 违规（新文件必须干净）
 *   - 文件在基线且处数 > 基线  ⇒ 违规（回潮）
 *   - 处数 < 基线              ⇒ 放行（进度），--report 提示可收基线
 *
 * 收基线：`node scripts/guards.mjs --update-clause-baseline`
 * ⚠️ 只应在**一条条款批量收口完成后**执行，不得用来掩盖新增违规。
 *
 * 豁免是「实现体」不是「法外之地」：
 *   - S06 豁免 confirm.ts / useConfirm.ts（confirmOrFalse 的实现体）
 *   - S07 豁免 YzhDialog.vue / YzhFormDialog.vue（它们自身必须包 el-dialog）
 * ========================================================================== */

/** 法条基线文件（相对 WEB） */
const CLAUSE_BASELINE = join(WEB, 'scripts/style-clause-baseline.json')

/** 条款豁免：该文件是这条写法的实现体本身 */
const CLAUSE_EXEMPT = {
  S06: ['yzh.vue.core/src/utils/confirm.ts', 'yzh.vue.core/src/composables/useConfirm.ts'],
  S07: [
    'yzh.vue.core/src/components/layout/YzhDialog.vue',
    'yzh.vue.core/src/components/form/YzhFormDialog.vue',
  ],
}

/** 各条款的机器计数（返回处数；无命中返回 0） */
const CLAUSE_CHECKS = {
  // S03a 模板侧：按钮禁用 info/success/warning（状态色不属按钮）
  // S03b TS 侧：动作对象 { key, text, type } 同样禁用；状态映射 Record< 与 MessageBox 选项是合法语境，必须排除
  S03(text) {
    let n = 0
    for (const tag of text.match(/<el-button(?![\w-])[^>]*>/g) || []) {
      if (/[\s"']type="(info|success|warning)"/.test(tag)) n++
    }
    for (const m of text.matchAll(/\btype:\s*['"](info|success|warning)['"]/g)) {
      const before = text.slice(Math.max(0, m.index - 320), m.index)
      if (/confirmButtonText|ElMessageBox/.test(before)) continue // MessageBox 选项
      // 状态映射表声明 `const X: Record<K, { text; type }> = {` —— 必须带 `= {`，
      // 否则函数签名 `node: Record<string, any>` 也会误伤（曾漏计 3 处）
      if (/Record<[^>]*>\s*=\s*\{/.test(before)) continue
      if (/\{\s*text:\s*string\s*;/.test(before)) continue // 类型声明
      if (/key\s*:|\.push\(|YzhAction/.test(before)) n++ // 动作对象
    }
    return n
  },
  // S04 el-button 必须显式 type（:type 绑定算已声明）
  S04(text) {
    const tags = text.match(/<el-button(?![\w-])[^>]*>/g) || []
    return tags.filter((t) => !/(?:^|[\s"'])[:@]?type\s*=/.test(t)).length
  },
  // S05 空态：el-empty + 模板里的 .empty-hint
  S05(text) {
    return (
      (text.match(/<el-empty(?![\w-])/g) || []).length +
      (text.match(/\bclass="[^"]*\bempty-hint\b/g) || []).length
    )
  },
  // S06 二次确认直调
  S06(text) {
    return (text.match(/ElMessageBox\.confirm\(/g) || []).length
  },
  // S07 裸弹窗（el-dialog + el-drawer）
  S07(text) {
    return (
      (text.match(/<el-dialog(?![\w-])/g) || []).length +
      (text.match(/<el-drawer(?![\w-])/g) || []).length
    )
  },
  // S08 状态标签
  S08(text) {
    return (text.match(/<el-tag(?![\w-])/g) || []).length
  },
  // S09 .bi 字体图标
  S09(text) {
    return (text.match(/\bbi bi-/g) || []).length
  },
  // S10 左树右表自造容器（.link-page；.studio-layout 是纵向壳，不属本条款）
  S10(text) {
    return (text.match(/\blink-page\b/g) || []).length
  },
  // S11 !important 出 :deep() 外（只看 <style> 块）
  S11(text) {
    let n = 0
    for (const sm of text.matchAll(/<style[^>]*>([\s\S]*?)<\/style>/g)) {
      // 先去块注释：法条条文本身会引用 !important 字样（注释里提条款不等于违规）
      const body = sm[1].replace(/\/\*[\s\S]*?\*\//g, (m) => ' '.repeat(m.length))
      let i = body.indexOf('!important')
      while (i !== -1) {
        const brace = body.lastIndexOf('{', i)
        const sel = brace < 0 ? '' : body.slice(body.lastIndexOf('}', brace - 1) + 1, brace)
        if (!sel.includes(':deep(')) n++
        i = body.indexOf('!important', i + 1)
      }
    }
    return n
  },
}

function runR20() {
  const files = walkAll(STYLE_ROOTS, STYLE_EXTS)
  const exemptOf = (id) => CLAUSE_EXEMPT[id] || []

  /** 重算全量：{ S03: { path: n }, … }（只保留 n > 0 的文件） */
  const recalc = () => {
    const next = {}
    for (const file of files) {
      const r = rel(file)
      const text = readFileSync(file, 'utf8')
      for (const [id, fn] of Object.entries(CLAUSE_CHECKS)) {
        if (exemptOf(id).some((a) => r.endsWith(a))) continue
        const n = fn(text)
        if (n > 0) (next[id] ||= {})[r] = n
      }
    }
    return next
  }

  if (process.argv.includes('--update-clause-baseline')) {
    const next = recalc()
    writeFileSync(CLAUSE_BASELINE, JSON.stringify(next, null, 2) + '\n', 'utf8')
    const clauses = Object.keys(next).length
    const hits = Object.values(next).reduce((s, m) => s + Object.values(m).reduce((a, b) => a + b, 0), 0)
    console.log(`✓ 法条基线已更新：${clauses} 条款 / ${hits} 处`)
    process.exit(0)
  }

  const baseline = existsSync(CLAUSE_BASELINE) ? JSON.parse(readFileSync(CLAUSE_BASELINE, 'utf8')) : {}
  const violations = []
  const progress = []

  for (const file of files) {
    const r = rel(file)
    const text = readFileSync(file, 'utf8')
    for (const [id, fn] of Object.entries(CLAUSE_CHECKS)) {
      if (exemptOf(id).some((a) => r.endsWith(a))) continue
      const n = fn(text)
      const base = baseline[id]?.[r] ?? 0
      if (n > base) {
        violations.push({
          file: r,
          line: 0,
          text: `[${id}] ${n} 处（基线 ${base}）—— 25 号 §四 法条违规，只准减不准增`,
        })
      } else if (n < base) {
        // 降到 0 也要报：该文件应从基线里移除
        progress.push({ id, r, n, base })
      }
    }
  }

  if (progress.length > 0) {
    console.log(`\nℹ 法条进度：${progress.length} 个文件已下降，可收基线（--update-clause-baseline）`)
    for (const p of progress.slice(0, 10)) console.log(`   [${p.id}] ${p.r} ${p.base} → ${p.n}`)
  }

  return { violations, skipped: false }
}

/**
 * 规则定义
 * - id / desc: 标识与说明
 * - roots: 扫描根目录数组
 * - exts: 扩展名
 * - forbid: RegExp[]，命中即违规
 * - debt: 已知待修文件（rel 路径包含其一即豁免），删完即达标
 */
const RULES = [
  {
    id: 'R1',
    desc: '原子组件必须零领域依赖（禁 @share / @/api / Logic / store / router）',
    roots: [CORE],
    exts: ['.ts', '.vue'],
    forbid: [
      /from\s+['"]@share\//,
      /from\s+['"]@\/api/,
      /from\s+['"][^'"]*\/logic(\/|['"])/,
      /useStore\s*\(/,
      /from\s+['"]pinia['"]/,
      /from\s+['"]vue-router['"]/,
    ],
    debt: [
      // 待 C-C1 / C-D1 / C-H1 处理
      'yzh.vue.core/src/components/layout/YzhTree.vue',
      'yzh.vue.core/src/components/layout/YzhTreeTableLayout.vue',
    ],
  },
  {
    id: 'R2',
    desc: 'TreeNode 一律 PascalCase（禁 node.code / selectedNode.value.name 等小写读取）',
    roots: PAGE_ROOTS,
    exts: ['.ts', '.vue'],
    forbid: [
      /\bnode\.(code|name|extra|children|isLeaf|parentCode)\b/,
      /\bn\.(code|name|extra|children|isLeaf|parentCode)\b/,
      /\b(selectedNode|parentNode|treeParentNode|treeEditingNode|stdEditingNode|orgEditingNode|categoryEditingNode)\.value\.(code|name|extra|children|isLeaf|parentCode)\b/,
      /\b(selectedNode|parentNode|treeParentNode|treeEditingNode)\.value\?\.(code|name|extra|children|isLeaf|parentCode)\b/,
    ],
    skipComments: true,
    debt: [
      // ★ 真实偏离（非误报）：自建 Record<string,any> 小写 children 树，应改用核心 TreeNode + treeUtils
      'foundation/iso-standard/logic.ts',
    ],
  },
  {
    id: 'R3',
    desc: '统一 ApiResponse 契约（页面层禁旧 res.code === 200）',
    roots: PAGE_ROOTS,
    exts: ['.ts', '.vue'],
    forbid: [/\.code\s*===\s*200/, /\.code\s*!==\s*200/],
    skipComments: true,
    // ★ 2026-09-25 P2：原 debt（workflow/directory/ 6 处手写 page code 判定）已全部改读 success
    debt: [],
  },
  {
    id: 'R3b',
    desc: '统一 ApiResponse 契约（api 模块层禁旧 res.code === 200）',
    roots: API_ROOTS,
    exts: ['.ts'],
    forbid: [/\.code\s*===\s*200/, /\.code\s*!==\s*200/],
    skipComments: true,
    debt: [],
  },
  {
    id: 'R4',
    desc: '统一 HTTP 客户端（禁 axios，必须用 yzhApi）',
    // ⚠️ 用 CORE_ALL（core 全源码）而非 CORE（仅 components/）：
    //    2026-09-24 实测 —— 旧的 CORE 范围**漏掉** `core/src/utils/http.ts`，
    //    那里用 axios 自建了第二套 HTTP 客户端，与 `api/client.ts` 的 yzhApi 并存。
    //    （该文件已删除；扫描面同时扩大，防止同类回归。）
    roots: [...PAGE_ROOTS, ...API_ROOTS, CORE_ALL],
    exts: ['.ts', '.vue'],
    forbid: [/from\s+['"]axios['"]/, /require\(\s*['"]axios['"]\s*\)/],
    skipComments: true,
    debt: [],
  },
  {
    id: 'R5',
    desc: '统一 HTTP 客户端（禁 .vue 内直接 fetch，必须走 yzhApi / api 模块）',
    roots: PAGE_ROOTS,
    exts: ['.vue'],
    forbid: [/\bfetch\s*\(/],
    skipComments: true,
    debt: [],
  },
  {
    id: 'R6',
    desc: '统一表格组件（页面禁内联 <el-table，必须用 YzhTable）',
    // 启用时基线：cert-auditor / cert-enterprise = 0（新代码区）；
    // cert-admin = 97 处 / 11 文件（冻结存量，见下方 debt，随测试驱动修复逐个摘除）。
    roots: PAGE_ROOTS,
    exts: ['.vue'],
    forbid: [/<el-table\b/],
    skipComments: true,
    debt: [
      // 以下为 cert-admin 冻结存量（97 处）。修复某个页面后，把对应行从本清单删除。
      'foundation/cert-org-stage/',
      'foundation/cert-org-standard/',
      'workflow/ai-usage/',
      'workflow/directory/components/ConfigTab.vue',
      'workflow/doc-extraction-rule/components/AIAnalysisTab.vue',
      'workflow/doc-extraction-rule/components/PromptVerifyTab.vue',
      'workflow/queue/',
      'workflow/report-rule/',
    ],
  },
  {
    id: 'R7',
    // ⚠️ 2026-10-03 覆盖面修正：原 roots 只有 Shared/Entities，导致单端实体新落点
    //    （Auditor/Entities、Admin/Entities）完全不被扫。铁律九对**所有**实体目录生效。
    // ⚠️ desc 里的「sys_api 同步 Enable 除外」已作废（2026-09-24 用户裁决：所有表统一 IsValid）。
    desc: '业务实体禁声明 Enable 列（铁律九：启用/禁用唯一字段 = IsValid）',
    roots: [
      resolve(WEB, '../certplatform-api/CertPlatform.Shared/Entities'),
      resolve(WEB, '../certplatform-api/CertPlatform.Auditor/Entities'),
      resolve(WEB, '../certplatform-api/CertPlatform.Admin/Entities'),
      resolve(WEB, '../certplatform-api/CertPlatform.Enterprise/Entities'),
    ],
    exts: ['.cs'],
    forbid: [
      /public\s+bool\s+Enable\b/,
      /ColumnName\s*=\s*"enable"/i,
      /public\s+bool\s+EnableField\b/,
    ],
    skipComments: true,
    debt: [],
  },
  {
    id: 'R10',
    desc: 'yzh.vue.core 新层禁宿主反向依赖（禁 @/ 与 @share/）',
    roots: CORE_APP_ROOTS,
    exts: ['.ts', '.vue'],
    forbid: [/from\s+['"]@\//, /from\s+['"]@share\//],
    skipComments: true,
    debt: [],
  },
  {
    id: 'R11',
    desc: 'yzh.vue.core 禁硬编码后端地址（地址由宿主 configureYzhApi 注入）',
    roots: [CORE_ALL],
    exts: ['.ts', '.vue'],
    forbid: [/127\.0\.0\.1/, /localhost:\d+/, /https?:\/\/[a-zA-Z0-9]/],
    skipComments: true,
    debt: [],
  },
  {
    // ★ 交叉校验型：不做「文件 + 正则」扫描，改做「多事实源集合比对」。
    //   主循环按 rule.type === 'cross' 跳过，由 checkRouteMenuConsistency() 单独执行。
    id: 'R12',
    type: 'cross',
    desc: '路由 ↔ 菜单一致性（菜单 Url 必须可达；路由必须可从菜单到达）',
    run: checkRouteMenuConsistency,
    debt: [],
  },
  {
    id: 'R13',
    desc: '信封判定唯一（禁裸 if (!res.success)，必须走 expectOk/unwrapOk）',
    roots: CUSTOM_ROOTS,
    exts: CUSTOM_EXTS,
    forbid: [/if\s*\(\s*!\s*\w+\.success\s*\)/],
    skipComments: true,
    debt: [],
  },
  {
    id: 'R14',
    type: 'custom',
    desc: '空 catch 禁吞 I/O 错误（try 内含 await 的 catch {} 一律出声）',
    run: runR14,
    debt: [],
  },
  {
    id: 'R15',
    type: 'custom',
    desc: 'ElMessageBox.confirm 必须有 catch（确认后的失败不能静默）',
    run: runR15,
    debt: [],
  },
  {
    id: 'R16',
    type: 'custom',
    desc: 'ElMessage.success 不得出现在 expectOk 之前（信封校验后才准报成功）',
    run: runR16,
    debt: [],
  },
  {
    id: 'R17',
    type: 'custom',
    desc: '后端实体归属一致性（单端实体不得留在 Shared/Entities；已下沉的不得两端共用）',
    run: runR17,
    // ✅ 2026-10-03 分层落地时 debt 清零：Admin 独占 22 个 + Auditor 独占 15 个已全部下沉，
    //    Shared/Entities 现在只剩双端共用实体（24 号清单 §三）。
    //    新增实体时按 24 号 §一 选对目录 —— 放错会被本规则直接拦下。
    debt: [],
  },
  {
    id: 'R18',
    type: 'custom',
    desc: '硬编码样式只准减不准增（裸 hex / font-size·padding·margin 裸 px；基线 scripts/style-baseline.json）',
    run: runR18,
    debt: [],
  },
  {
    id: 'R19',
    type: 'custom',
    desc: '结构性 CSS 门禁（stylelint：重复选择器/降序特异性/未知单位/弃用关键字，基线 0）',
    run: runR19,
    debt: [],
    skipMsg: 'node_modules/.bin/stylelint 未安装（npm install 后生效）',
  },
  {
    id: 'R20',
    type: 'custom',
    desc:
      '样式法条 S03–S11（按钮语义色/显式 type/空态/确认框/弹窗/状态标签/.bi 图标/左树右表/!important，' +
      '基线 scripts/style-clause-baseline.json 只准减不准增）',
    run: runR20,
    debt: [],
  },
  // ===== 审计 §8（标准目录链路）：全部为后端 .cs 扫描，debt 随修复逐条删除 =====
  {
    id: 'R-A',
    type: 'custom',
    desc: '服务层禁止单参 UpdateAsync(entity)（全列写回 = 陷阱 ㉑ / P1-14）',
    run: runRA,
    // §7 第 6 步「14 处列级写入」未做 → 命中文件整文件豁免，修一处也需整文件改完才摘
    debt: [
      'Services/DocExtraction/DocExtractionRuleService.AI.cs',
      'Services/DocExtraction/DocExtractionRuleService.cs',
      'Services/StandardDirectory/DirectoryTemplateService.cs',
      'Services/StandardDirectory/StandardDirectoryService.cs',
      'Services/StandardDirectory/UploadQueueCancelHandler.cs',
      'Services/Workflow/PromptTemplateService.cs',
    ],
  },
  {
    id: 'R-B',
    type: 'custom',
    desc: '控制器禁止直接继承裸 ControllerBase（白名单 Auth / 健康检查；应继承 YzhControllerBase<V> 或 WebControllerBase）',
    run: runRB,
    // 迁移到 YzhControllerBase<V> / WebControllerBase（路由已显式 [Route]、ApiCode 不变）后删除对应行
    debt: [
      'Controllers/Foundation/CertOrgStandardController.cs',
      'Controllers/Foundation/DirectoryTemplateController.cs',
      'Controllers/Foundation/CertOrgStageController.cs',
      'Controllers/System/QueueMonitorController.cs',
      'Controllers/Workflow/DocExtractionRuleController.cs',
      'Controllers/Workflow/AIUsageController.cs',
      'Controllers/Workflow/WorkflowTestController.cs',
      'Controllers/Workflow/ReportDefinitionController.cs',
      'YZH.Core.Web/Controllers/System/ApiSyncController.cs',
      'YZH.Core.Web/Controllers/System/MenuController.cs',
    ],
  },
  {
    id: 'R-C',
    type: 'custom',
    desc: '树递归遍历必须带 visited / 深度上限（P0-6：环状 ParentCode → StackOverflow 崩进程）',
    run: runRC,
    debt: [],
  },
  {
    id: 'R-D',
    type: 'custom',
    desc: '禁止 return (true, 非空 error, …) 形态（P1-10：ok=true 带 error，消息被静默丢弃）',
    run: runRD,
    debt: [],
  },
  {
    id: 'R-E',
    type: 'custom',
    desc: '禁止 catch 块内写导出占位文件（P1-18：下载失败写占位 → ZIP「成功」却内容是假的）',
    run: runRE,
    debt: [],
  },
]

const failures = []
const crossSkipped = []
let scannedFiles = 0

for (const rule of RULES) {
  if (rule.type === 'cross' || rule.type === 'custom') continue // ★ 自定义规则不做文件扫描，见下方单独执行
  const files = walkAll(rule.roots, rule.exts)
  scannedFiles += files.length
  const violations = []
  for (const file of files) {
    const r = rel(file)
    if (rule.debt?.some((d) => r.includes(d))) continue
    const lines = readFileSync(file, 'utf8').split('\n')
    lines.forEach((line, i) => {
      if (rule.skipComments && isCommentLine(line)) return
      for (const re of rule.forbid) {
        if (re.test(line)) {
          violations.push({ file: r, line: i + 1, text: line.trim().slice(0, 120) })
          break
        }
      }
    })
  }
  if (violations.length) failures.push({ rule, violations })
  else if (REPORT_ONLY) console.log(`✓ ${rule.id} ${rule.desc}`)
}

/** ★ 自定义规则（R12 交叉比对 / R14–R16 + 审计 §8 后端规则）：自带 run()，无「文件 + 行号」概念 */
const customDebtHits = []
for (const rule of RULES.filter((r) => r.type === 'cross' || r.type === 'custom')) {
  const { violations, skipped } = rule.run()
  if (skipped) {
    crossSkipped.push(rule)
    continue
  }
  const kept = rule.debt?.length
    ? violations.filter((v) => !rule.debt.some((d) => v.file.includes(d)))
    : violations
  if (rule.debt?.length && violations.length > kept.length)
    customDebtHits.push({ rule, hits: violations.length - kept.length })
  if (kept.length) failures.push({ rule, violations: kept })
  else if (REPORT_ONLY) console.log(`✓ ${rule.id} ${rule.desc}`)
}

/** 违规定位文本：line=0 表示「集合级」违规，只显示来源名 */
const locate = (v) => (v.line ? `${v.file}:${v.line}` : v.file)

const totalViolations = failures.reduce((n, f) => n + f.violations.length, 0)

/** 债务盘点（仅 --report 可见，绝不进入日常构建输出） */
function printDebtLedger() {
  const directApi = walkAll(PAGE_ROOTS, ['.ts', '.vue']).filter((f) =>
    /from\s+['"]@\/api/.test(readFileSync(f, 'utf8')),
  )
  if (directApi.length) {
    console.log(`\nℹ 债务盘点 A：${directApi.length} 个页面文件仍直连 @/api（PG-D/PG-C 迁移后应清零）：`)
    for (const f of directApi) console.log(`   ${rel(f)}`)
  }

  const r6 = RULES.find((x) => x.id === 'R6')
  let legacyFiles = 0
  let legacyHits = 0
  for (const file of walkAll(r6.roots, r6.exts)) {
    const r = rel(file)
    if (!r6.debt?.some((d) => r.includes(d))) continue
    const n = readFileSync(file, 'utf8')
      .split('\n')
      .filter((l) => !isCommentLine(l) && /<el-table\b/.test(l)).length
    if (n > 0) {
      legacyFiles++
      legacyHits += n
    }
  }
  if (legacyHits) {
    console.log(
      `\nℹ 债务盘点 B：${legacyFiles} 个页面文件仍有内联 <el-table（共 ${legacyHits} 处，已豁免）。`,
    )
    console.log(`   修复某个页面后，请从 guards.mjs 的 R6.debt 中删除对应行。`)
  }

  for (const { rule, hits } of customDebtHits) {
    console.log(`\nℹ 债务盘点 C：${rule.id} 命中 ${hits} 处（debt 已豁免）—— 修复后从 debt 删除对应文件。`)
  }
}

if (REPORT_ONLY) {
  for (const { rule, violations } of failures) {
    console.log(`\n✗ ${rule.id} ${rule.desc}`)
    for (const v of violations) console.log(`   ${locate(v)}  ${v.text}`)
  }
  printDebtLedger()
  for (const rule of crossSkipped) {
    console.log(`\n⚠ ${rule.id} 未执行：${rule.skipMsg ?? `缺少菜单快照 ${rel(MENU_SNAPSHOT)}`}`)
    if (!rule.skipMsg) console.log('   生成：./scripts/db/verify/sync_menu_urls.sh')
  }
  console.log(
    `\n报告模式：${RULES.length} 条规则 / ${scannedFiles} 个文件 / ${totalViolations} 处违规（不阻断）`,
  )
  process.exit(0)
}

if (totalViolations > 0) {
  console.error(`\n✗ 前端架构守卫未通过：${totalViolations} 处违规\n`)
  for (const { rule, violations } of failures) {
    console.error(`  ${rule.id} ${rule.desc}`)
    for (const v of violations) console.error(`    ${locate(v)}  ${v.text}`)
    console.error('')
  }
  console.error('  修复后重试；确需跳过：git commit --no-verify')
  console.error('  查看全量债务：npm run guard:report\n')
  process.exit(1)
}

for (const rule of crossSkipped) {
  console.warn(
    rule.skipMsg
      ? `⚠ ${rule.id} 未执行：${rule.skipMsg}`
      : `⚠ ${rule.id} 未执行：缺少菜单快照 ${rel(MENU_SNAPSHOT)}` +
        `（生成：./scripts/db/verify/sync_menu_urls.sh）`,
  )
}

console.log(`✓ 前端架构守卫通过（${RULES.length} 条规则 / ${scannedFiles} 个文件）`)
