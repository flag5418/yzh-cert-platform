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

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

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
const ORPHAN_ALLOW = new Set([])

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
    debt: [
      // 手写页面待 PG-E1（后端迁基类）后重写
      'workflow/directory/',
    ],
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
    desc: '业务实体禁声明 Enable 列（启用/禁用唯一字段 = IsValid；sys_api 同步 Enable 除外）',
    roots: [resolve(WEB, '../certplatform-api/CertPlatform.Shared/Entities')],
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
]

const failures = []
const crossSkipped = []
let scannedFiles = 0

for (const rule of RULES) {
  if (rule.type === 'cross') continue // ★ 交叉规则不做文件扫描，见下方单独执行
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

/** ★ 交叉规则（R12）：多事实源集合比对，无「文件 + 行号」概念 */
for (const rule of RULES.filter((r) => r.type === 'cross')) {
  const { violations, skipped } = rule.run()
  if (skipped) {
    crossSkipped.push(rule)
    continue
  }
  if (violations.length) failures.push({ rule, violations })
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
}

if (REPORT_ONLY) {
  for (const { rule, violations } of failures) {
    console.log(`\n✗ ${rule.id} ${rule.desc}`)
    for (const v of violations) console.log(`   ${locate(v)}  ${v.text}`)
  }
  printDebtLedger()
  for (const rule of crossSkipped) {
    console.log(`\n⚠ ${rule.id} 未执行：缺少菜单快照 ${rel(MENU_SNAPSHOT)}`)
    console.log('   生成：./scripts/db/verify/sync_menu_urls.sh')
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
    `⚠ ${rule.id} 未执行：缺少菜单快照 ${rel(MENU_SNAPSHOT)}` +
      `（生成：./scripts/db/verify/sync_menu_urls.sh）`,
  )
}

console.log(`✓ 前端架构守卫通过（${RULES.length} 条规则 / ${scannedFiles} 个文件）`)
