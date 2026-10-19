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
  // ── 专家端 · 企业资料规范化向导（2026-10-11）──
  // 理由：`/enterprise-normalize/create` 是「企业资料规范化 `/enterprise-normalize`」的
  //   **子页面**（开任务的向导，版式对齐 `/tasks/create`），不是独立功能入口。
  //   入口在列表页工具栏「创建规范化任务」，以及行按钮「重新执行」（带 `?from=`）。
  //   菜单侧只需 `MENU_AUD_12`（/enterprise-normalize）一条；
  //   审核页 `/enterprise-review` 是**独立功能**，有自己的菜单 `MENU_AUD_14`，⛔ 不在此登记。
  'auditor:/enterprise-normalize/create',
  // ── 专家端 · **刻意停用**的三个菜单（2026-10-11 用户已裁决，✅ 不再是「待裁决」）──
  // ★ 用户原话：「菜单是故意的，因为有些菜单现在没有实现，我故意禁用了」。
  // 事实：`Sys_Menu` 里 MENU_AUD_01(/overview) / MENU_AUD_05(/organization) /
  //   MENU_AUD_06(/settings) 的 `IsValid = 0`（**人工停用**，对应页面尚未实现）；
  //   `sync_menu_urls.sh` 只导 `IsValid = 1` ⇒ 这三条不在 `menu-urls.tsv` 里 ⇒
  //   在守卫看来是「没有菜单入口的孤儿路由」。**这是期望状态，不是缺陷。**
  // ⚠️ 与 `20260923_auditor_menu_seed_V1.sql` 的关系：该脚本里这三行原本是
  //   `IsValid = 1`，且 `ON DUPLICATE KEY UPDATE` 带 `IsValid = VALUES(IsValid)`
  //   ⇒ **每次重跑都会把人工停用冲回启用**（用户报障：「改了菜单、跑一次 SQL 又被还原」）。
  //   已修：脚本 VALUES 改为 `IsValid = 0` 且覆盖子句**不再动** `IsValid`，
  //   回潮由后端守卫 `B-R6` 拦截。⇒ 本段登记**长期有效**，⛔ 不需要「改回 1」。
  // ⛔ 页面文件与路由**保留**（不是死代码）：功能实现后只要在库里把 `IsValid` 置 1
  //   （或改脚本 VALUES 后重跑）即可启用，无需重新接线。
  // ⚠️ 遗留待办（属产品决定，⛔ 不在本守卫范围）：`cert-auditor/src/router/index.ts`
  //   的 `redirect: '/overview'` 指向的正是被停用页 ⇒ 登录后落地页没有菜单高亮。
  'auditor:/overview',
  'auditor:/organization',
  'auditor:/settings',
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
 * R-A 禁止单参 `UpdateAsync(entity)`（全列写回 = 陷阱 ㉑ / P1-14）。
 * 判据：`UpdateAsync(X)` 且括号内无逗号；多参 `UpdateAsync(entity, fields…)` 放行。
 * 口径：`certplatform-api/**\/Services/**` + `certplatform-api/**\/Controllers/**`。
 */
function runRA() {
  const violations = []
  const servicesRoot = walkAll([API_SRC], ['.cs']).filter((f) => f.includes('/Services/') || f.includes('/Controllers/'))
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

/**
 * R21 —— 上传契约单源（2026-10-14 用户铁律：全平台上传后缀只认
 * `cert/cert-share/src/constants/upload-file-policy.json` 生成的两端清单，「加后缀只改一处」）。
 *
 * 三段：
 *  ① 生成物同步：`node scripts/generate/upload-policy.mjs --check`（与后端 B-R7 同源同判，
 *     两端 `<generated>` 段与 JSON 漂移 ⇒ 失败）；
 *  ② 禁字面量 `accept="…"`（含 `.` 后缀串）—— 合法写法是 `:accept="EXPR"` 绑定契约生成值；
 *  ③ 有 file 选择面的文件（`type="file"` / `<el-upload` / `YzhFolderUpload` / `webkitdirectory`）
 *     必须 import `upload-file-policy`（用 buildAcceptAttribute / partitionUploadable /
 *     checkUploadable）—— 裸上传入口零契约 = 违规。
 *
 * 📏 基线：规则启用时（2026-10-14）逐文件实测 = 0 违规（豁免仅 2 个：通用组件 / 桶文件）。
 */
const UPLOAD_CONTRACT_EXEMPT = [
  // 通用组件：accept 经 prop 注入，运行时闸门由消费方页面负责（本规则只管「谁用谁接契约」）
  'cert/cert-share/src/components/YzhFolderUpload.vue',
  // 桶文件（仅 re-export，无上传面）
  'cert/cert-share/src/components/index.ts',
]

function runR21() {
  const violations = []
  const WEB_ROOT = resolve(WEB, '..', '..')

  // ① 生成物与 upload-file-policy.json 同步
  const gen = join(WEB_ROOT, 'scripts/generate/upload-policy.mjs')
  if (!existsSync(gen)) {
    violations.push({
      file: '../scripts/generate/upload-policy.mjs',
      line: 0,
      text: 'R21① 上传契约生成脚本缺失 —— 单源链路断了（无法保证两端清单一致）',
    })
  } else {
    const res = spawnSync(process.execPath, [gen, '--check'], { encoding: 'utf8' })
    if (res.error || res.status !== 0) {
      const why = res.error
        ? res.error.message
        : (res.stdout || res.stderr || '').trim().split('\n').slice(-1)[0] || `exit ${res.status}`
      violations.push({
        file: '../scripts/generate/upload-policy.mjs',
        line: 0,
        text: `R21① 生成物与 upload-file-policy.json 漂移（跑 node scripts/generate/upload-policy.mjs 修复）：${why}`,
      })
    }
  }

  // ② + ③ 页面 / 分享层 / 核心组件的上传面
  const roots = [...PAGE_ROOTS, join(WEB, 'cert/cert-share/src'), join(WEB, 'yzh.vue.core/src/components')]
  const surfaceRe = /type\s*=\s*["']file["']|<el-upload\b|YzhFolderUpload|webkitdirectory/
  const literalAcceptRe = /(?<![:\w-])accept\s*=\s*["'][^"']*\.[^"']*["']/
  for (const file of walkAll(roots, ['.vue', '.ts'])) {
    const r = rel(file)
    if (UPLOAD_CONTRACT_EXEMPT.includes(r)) continue
    const text = readFileSync(file, 'utf8')
    const lines = text.split('\n')

    // ② 字面量 accept（硬编码后缀串）
    lines.forEach((line, i) => {
      if (isCommentLine(line)) return
      if (literalAcceptRe.test(line)) {
        violations.push({
          file: r,
          line: i + 1,
          text: `R21② 字面量 accept 后缀串 —— 改用 :accept="buildAcceptAttribute(...)"（契约生成）：${line.trim().slice(0, 80)}`,
        })
      }
    })

    // ③ 有上传面必须接契约
    if (surfaceRe.test(text) && !/upload-file-policy/.test(text)) {
      const at = text.search(surfaceRe)
      violations.push({
        file: r,
        line: lineOf(text, at),
        text: 'R21③ 有 file 选择面但未接上传契约（import @share/constants/upload-file-policy）',
      })
    }
  }
  return { violations, skipped: false }
}

/* ==========================================================================
 * ★ R22 —— 工作流跨边界命名 G2：前端禁以小写 B 键读/写 node.config
 *
 *   依据：docs/40-实施/开发计划/工作流跨边界命名统一方案-V1.md §2.2（边界 B）
 *   口径：node.config 内层键一律 PascalCase（config.PromptTemplate / config.RuleCode …）。
 *         边界 F（LayoutJson nodePositions）与 HTTP 请求体（边界 D）**不在此例**。
 *   ⚠️ `value` 只查「带引号」形态（panelSchema/panelFieldValues 键），
 *      因为 `this.config.value` 是 Vue ref 取值的正常写法（代码形态会误报）。
 * ========================================================================== */
const WORKFLOW_CFG_KEYS =
  'docType|ruleCode|fieldCode|tableCode|standardFileCode|promptTemplate|outputType|outputSchema|iterateMode|params|model|systemPrompt'
const WORKFLOW_CFG_ROOTS = [
  join(WEB, 'cert/cert-share/src/composables/workflow'),
  join(WEB, 'cert/cert-share/src/components/workflow'),
  join(WEB, 'cert/cert-admin/src/pages/workflow'),
  join(WEB, 'cert/cert-auditor/src/pages/workflow'),
]

function runR22() {
  const violations = []
  const codeRe = new RegExp(`\\bconfig\\s*\\.\\s*(${WORKFLOW_CFG_KEYS})\\b`)
  const strRe = new RegExp(`['"\`]config\\.(${WORKFLOW_CFG_KEYS}|value)['"\`]`)
  for (const file of walkAll(WORKFLOW_CFG_ROOTS, ['.ts', '.vue'])) {
    const r = rel(file)
    if (r.includes('.test.')) continue
    const lines = readFileSync(file, 'utf8').split('\n')
    lines.forEach((line, i) => {
      if (isCommentLine(line)) return
      const m = codeRe.exec(line) || strRe.exec(line)
      if (!m) return
      violations.push({
        file: r,
        line: i + 1,
        text: `G2 node.config 键必须 PascalCase（命中 config.${m[1]}）—— 见 工作流跨边界命名统一方案-V1.md §2.2`,
      })
    })
  }
  return { violations, skipped: false }
}

/* ★ R23 —— 工作流跨边界命名 G3：禁归一化层 portNormalize 复活
 *   端口字段已于 2026-10-16 P3 统一为 PascalCase（后端 metadata / rule_json / PortDef 同名），
 *   归一化层属「两套命名并存」的补丁，一旦复活即意味着命名再次分裂。 */
function runR23() {
  const violations = []
  const f = join(WEB, 'cert/cert-share/src/composables/workflow/portNormalize.ts')
  if (existsSync(f)) {
    violations.push({
      file: rel(f),
      line: 0,
      text: 'G3 portNormalize 归一化层复活 —— 端口字段已统一 PascalCase，禁止再加归一化层',
    })
  }
  const roots = [
    join(WEB, 'cert/cert-share/src'),
    join(WEB, 'cert/cert-admin/src'),
    join(WEB, 'cert/cert-auditor/src'),
    join(WEB, 'yzh.vue.core/src'),
  ]
  const re = /\b(normalizePorts|portNormalize)\b/
  for (const file of walkAll(roots, ['.ts', '.vue'])) {
    const r = rel(file)
    if (r.includes('.test.')) continue
    const lines = readFileSync(file, 'utf8').split('\n')
    lines.forEach((line, i) => {
      if (isCommentLine(line)) return
      if (re.test(line)) {
        violations.push({
          file: r,
          line: i + 1,
          text: `G3 引用了已删除的归一化层（portNormalize/normalizePorts）—— 端口字段统一 PascalCase，无需归一化`,
        })
      }
    })
  }
  return { violations, skipped: false }
}

/* ==========================================================================
 * ★ R24 —— 页面 logic.ts 必须继承内核基类（2026-10-17 用户裁定 D4）
 *
 *   为什么：`logic.ts` 是页面唯一的业务逻辑落点。不继承内核基类时，
 *   它既没有标准生命周期（init / loading / reportError），也没有统一的数据通道
 *   （dataLoader / apiPost），表现为「每个页面重新发明一套 ref + try/catch」。
 *
 *   两类违规：
 *     ① 裸 class —— `export class XxxLogic {`（无 extends）
 *     ② 模块级单例 —— 无 class，但有顶层 `export const x = ref(...)`
 *        （比 ① 更严重：状态挂在模块作用域，离开页面再进来**不重置**，多标签页共用一份）
 *
 *   ⛔ 不算违规：无 class 且无顶层 ref 的**纯函数模块**（例：`prompt-template/logic.ts`
 *      是静态配置 + 纯函数，页面是三栏工作台而非列表页，本就不该套 CRUD 基类）。
 *   ⚠️ `TreeTableLogic` 是 `@deprecated` 改名过渡别名（真身 `TreeTableCore`），
 *      仍算合规，但 TT-1 收尾时应统一改名（当前 5 个文件在用）。
 * ========================================================================== */
const LOGIC_CORE_BASES = [
  'SingleTableCore',
  'TreeTableCore',
  'CheckTreeCore',
  'CheckTreeCrudCore',
  'LinkTableCore',
  'AssociationTreeCore',
  // @deprecated 过渡别名：仍指向 TreeTableCore，故算合规
  'TreeTableLogic',
]

function runR24() {
  const violations = []
  for (const file of walkAll(PAGE_ROOTS, ['.ts'])) {
    if (basename(file) !== 'logic.ts') continue
    const r = rel(file)
    const text = readFileSync(file, 'utf8')
    const lineOf = (idx) => text.slice(0, idx).split('\n').length

    const classes = [
      ...text.matchAll(
        /^[ \t]*(?:export[ \t]+)?(?:default[ \t]+)?(?:abstract[ \t]+)?class[ \t]+(\w+)[ \t]*(?:extends[ \t]+([\w<>,.\s]+?))?[ \t]*\{/gm,
      ),
    ]

    if (classes.length === 0) {
      // 无 class：只有「顶层 ref/reactive/computed」才算模块级单例，纯函数模块放行
      const moduleState = [
        ...text.matchAll(/^export[ \t]+const[ \t]+\w+[ \t]*=[ \t]*(?:ref|reactive|computed)[<(]/gm),
      ]
      if (moduleState.length > 0) {
        violations.push({
          file: r,
          line: lineOf(moduleState[0].index),
          text: `模块级单例 Logic（${moduleState.length} 个顶层 ref/reactive/computed）—— 状态跨路由不重置，应改为 class extends 内核基类`,
        })
      }
      continue
    }

    for (const m of classes) {
      const name = m[1]
      const base = (m[2] || '').split('<')[0].trim()
      if (!base) {
        violations.push({
          file: r,
          line: lineOf(m.index),
          text: `${name} 未继承内核基类（必须 extends ${LOGIC_CORE_BASES.slice(0, 6).join(' | ')}）`,
        })
      } else if (!LOGIC_CORE_BASES.includes(base)) {
        violations.push({
          file: r,
          line: lineOf(m.index),
          text: `${name} extends ${base} —— 不是内核基类`,
        })
      }
    }
  }
  return { violations, skipped: false }
}

/* ==========================================================================
 * ★ R25 —— 暗色层完整性（2026-10-17 · D5 配套）
 *
 *   问题：`tokens.css` 的 `html.dark` 是**手写覆盖层**，`:root` 新增一个
 *   `--yzh-color-*` 令牌而忘了在 `html.dark` 补值 ⇒ 该令牌在暗色下**仍取亮色值**，
 *   且**任何地方都不报错**（页面只是「颜色不对」，不是「功能坏了」）。
 *
 *   判据（三条同时成立才算违规）：
 *     ① 令牌名以 `--yzh-color-` 开头（阴影/圆角/间距/字号属主题中性，不参与）；
 *     ② 在 `:root` 有定义、在 `html.dark` **没有**定义；
 *     ③ **确实被业务代码引用**（引用数 > 0）—— 定义了却没人用，不构成暗色缺陷。
 *
 *   ⚠️ 选择器不写死 `html.dark`：任何含 `.dark` 或 `[data-theme="dark"]` 的块都算覆盖层，
 *      避免将来换主题方案（data 属性）时规则误报。
 * ========================================================================== */
const DARK_TOKENS_FILE = join(WEB, 'yzh.vue.core/src/assets/css/tokens.css')

/**
 * 待补的暗色覆盖（**只准减不准增**）：补齐后把对应行删掉。
 * 2026-10-17 实测 = 8 个「已引用但暗色未覆盖」的颜色令牌。
 */
const DARK_DEBT = [
  '--yzh-color-primary-light-7',
  '--yzh-color-primary-light-8',
  '--yzh-color-success-light-7',
  '--yzh-color-success-light-8',
  '--yzh-color-warning-light-7',
  '--yzh-color-warning-light-8',
  '--yzh-color-danger-light-8',
  '--yzh-color-text-inverse',
]

/** 取某选择器块内的全部自定义属性名（按花括号配对，不依赖缩进） */
function blockTokenNames(text, selectorRe) {
  const m = selectorRe.exec(text)
  if (!m) return []
  const start = text.indexOf('{', m.index)
  if (start < 0) return []
  let depth = 0
  let end = text.length
  for (let i = start; i < text.length; i++) {
    if (text[i] === '{') depth++
    else if (text[i] === '}') {
      depth--
      if (depth === 0) {
        end = i
        break
      }
    }
  }
  return [...text.slice(start + 1, end).matchAll(/(--[\w-]+)\s*:/g)].map((x) => x[1])
}

function runR25() {
  const violations = []
  if (!existsSync(DARK_TOKENS_FILE)) return { violations, skipped: false }
  const text = readFileSync(DARK_TOKENS_FILE, 'utf8')

  const rootTokens = blockTokenNames(text, /^:root\s*\{/m)
  const darkTokens = new Set(
    blockTokenNames(text, /^[^\n{}]*?(?:\.dark|\[data-theme=['"]dark['"]\])[^\n{}]*\{/m),
  )
  if (!rootTokens.length) return { violations, skipped: false }

  // 引用计数（业务代码，排除令牌定义文件自身）
  const usage = new Map()
  for (const file of walkAll(STYLE_ROOTS, ['.vue', '.ts'])) {
    if (rel(file) === rel(DARK_TOKENS_FILE)) continue
    const body = readFileSync(file, 'utf8')
    for (const m of body.matchAll(/var\(\s*(--yzh-color-[\w-]+)/g)) {
      usage.set(m[1], (usage.get(m[1]) || 0) + 1)
    }
  }

  for (const name of rootTokens) {
    if (!name.startsWith('--yzh-color-')) continue
    if (darkTokens.has(name)) continue
    if (DARK_DEBT.includes(name)) continue
    const refs = usage.get(name) || 0
    if (refs === 0) continue
    violations.push({
      file: rel(DARK_TOKENS_FILE),
      line: 0,
      text: `${name} 被引用 ${refs} 处，但暗色层（html.dark）未覆盖 ⇒ 暗色下仍取亮色值`,
    })
  }
  return { violations, skipped: false }
}

/**
 * R26 免认证纪律（D2 / D6 已定 2026-10-17）：
 * - 全仓禁 `YZHAnonymous` 字面量（含 using / 特性 / 注释引用；⛔ 零容忍）。
 * - `certplatform-api/**` 中出现 `[AllowAnonymous]` ⇒ **只允许 D6 白名单内的方法**。
 * - `yzh-core`（框架层）不在本守卫范围（框架层按 AGENTS §11 单独准入）。
 *
 * ★ 2026-10-17 首次启用：`YZHAnonymousAttribute` 当前 **0 处业务使用**；
 *   `[AllowAnonymous]` 在 certplatform-api 内共 6 处（Auth × 2 + AuditorAuth × 2 + User × 1 + WorkflowTest × 1），
 *   其中 `WorkflowTest:222`（health）按 D2 需转 JWT ⇒ 白名单不含它；其余 5 处入白名单。
 */
function runR26() {
  const violations = []
  // ── ① YZHAnonymous 字面量禁令（含 yzh-core，零豁免除定义文件本身）──
  const yzhAnonFiles = walkAll([API_SRC, YZH_CORE_SRC], ['.cs'])
  for (const file of yzhAnonFiles) {
    if (/YZHAnonymousAttribute\.cs$/.test(file)) continue // 定义文件正在被删除，豁免
    const text = readFileSync(file, 'utf8')
    const lines = text.split('\n')
    for (let i = 0; i < lines.length; i++) {
      if (/\bYZHAnonymous\b/.test(lines[i])) {
        violations.push({ file: rel(file), line: i + 1, text: lines[i].trim().slice(0, 120) })
      }
    }
  }
  // ── ② certplatform-api 内 [AllowAnonymous] 白名单（D6 已定）──
  //     白名单按「文件:方法名」粒度；未命中即违规。
  //     yzh-core 全豁免（框架层按 AGENTS §11 单独管控，不在此守卫内）。
  const ALLOWED = new Set([
    '../certplatform-api/CertPlatform.Admin/Controllers/System/AuthController.cs:Login',
    '../certplatform-api/CertPlatform.Admin/Controllers/System/UserController.cs:getVierificationCode',
    '../certplatform-api/CertPlatform.Auditor/Controllers/AuditorAuthController.cs:Register',
    '../certplatform-api/CertPlatform.Auditor/Controllers/AuditorAuthController.cs:GetOrgList',
  ])
  const apiFiles = walkAll([API_SRC], ['.cs'])
  for (const file of apiFiles) {
    const text = readFileSync(file, 'utf8')
    const lines = text.split('\n')
    for (let i = 0; i < lines.length; i++) {
      if (!/AllowAnonymous/.test(lines[i])) continue
      // 向后最多 6 行查找方法声明（形如 `public async Task<T> Method(`）
      let methodName = null
      for (let j = i + 1; j <= Math.min(i + 6, lines.length - 1); j++) {
        const m = lines[j].match(/public\s+(?:async\s+)?(?:Task<[^>]+>|void|string|int|bool|long|IActionResult|IEnumerable<[^>]+>)\s+(\w+)\s*\(/)
        if (m) { methodName = m[1]; break }
      }
      if (!methodName) continue
      const key = rel(file) + ':' + methodName
      if (!ALLOWED.has(key)) {
        violations.push({ file: rel(file), line: i + 1, text: `${lines[i].trim().slice(0, 80)} → 方法 ${methodName}` })
      }
    }
  }
  return { violations, skipped: false }
}

/* ==========================================================================
 * ★ A-R1 ~ A-R10 —— 跨端共享与核心资源检索铁律（2026-10-12 用户裁定 / 10-13 归属模型 V2）
 *
 *   规则全文：docs/30-项目规则/跨端共享与核心资源检索规则-V1.md
 *   实施计划：docs/40-实施/迁移计划/跨端共享层收敛-实施计划-V2.md
 *
 *   违规面四类：
 *     ① 依赖方向 —— A-R1（Admin→Auditor）/ A-R2（Auditor→Admin）/ A-R3（Shared→端）
 *     ② 死 using —— A-R5
 *     ③ 宿主语义 —— A-R7
 *     ④ 归属判据分裂 —— A-R6（核心资源禁端内直查）/ A-R10（已废除的机构常量字面量全仓禁）
 *
 *   ⚠️ 2026-10-13 归属模型 V2：原 A-R8（OrgCode 空值判据）/ A-R9（yzh-std-env 字面量唯一）
 *      / A-R11（企业列禁机构常量）三条**已退役** —— 它们监管的常量（CoreOrgCodes.StdEnv）
 *      本身已被删除，判据统一收敛到 Shared/Domain/Scope/OrgScope.cs。A-R10 吸收其职责。
 *
 *   ⚠️ 为什么这些规则重要：项目此前**依赖方向零守卫** —— 全仓 24 条守卫里
 *      只有本组才覆盖「Admin / Shared / Auditor 三方结构」。
 * ========================================================================== */

/** 读文本（不存在 ⇒ 空串）—— 供 csproj 判定用 */
function readText(p) {
  return existsSync(p) ? readFileSync(p, 'utf8') : ''
}

/** 收集目录下所有类型名（class / record / interface / enum / struct）—— 供死 using 判定 */
function collectTypeNames(dir) {
  const names = new Set()
  if (!existsSync(dir)) return names
  for (const file of listCsFiles(dir)) {
    const text = readFileSync(file, 'utf8')
    for (const m of text.matchAll(
      /\b(?:public|internal)\s+(?:sealed\s+|abstract\s+|partial\s+|static\s+)*(?:class|record|interface|enum|struct)\s+(\w+)/g,
    )) {
      names.add(m[1])
    }
  }
  return names
}

/** 命名空间 → 类型集（按 `CertPlatform.{端}.{路径}` 目录约定反推） */
function typesOfNamespace(ns) {
  const m = ns.match(/^CertPlatform\.(Admin|Auditor)\.(.+)$/)
  if (!m) return null
  const dir = join(API_ROOT, `CertPlatform.${m[1]}`, m[2].replaceAll('.', '/'))
  return existsSync(dir) ? collectTypeNames(dir) : null
}

/** A-R1：`Admin` ⛔ 引 `Auditor` */
function runAR1() {
  const violations = []
  const adminDir = join(API_ROOT, 'CertPlatform.Admin')
  const proj = join(adminDir, 'CertPlatform.Admin.csproj')
  if (/CertPlatform\.Auditor\.csproj/.test(readText(proj))) {
    violations.push({
      file: rel(proj),
      line: 0,
      text: 'csproj 引用了 CertPlatform.Auditor —— ⛔ 反向依赖（两端互不引用，只能通过 Shared）',
    })
  }
  for (const file of listCsFiles(adminDir)) {
    readFileSync(file, 'utf8')
      .split('\n')
      .forEach((line, i) => {
        if (isCommentLine(line)) return
        if (/^\s*using\s+CertPlatform\.Auditor\b/.test(line)) {
          violations.push({ file: rel(file), line: i + 1, text: line.trim().slice(0, 120) })
        }
      })
  }
  return { violations, skipped: false }
}

/** A-R2：`Auditor` ⛔ 引 `Admin`（debt = 阶段 4 待解耦清单，逐条摘除后转正） */
function runAR2() {
  const violations = []
  const audDir = join(API_ROOT, 'CertPlatform.Auditor')
  const proj = join(audDir, 'CertPlatform.Auditor.csproj')
  if (/CertPlatform\.Admin\.csproj/.test(readText(proj))) {
    violations.push({
      file: rel(proj),
      line: 0,
      text: 'csproj 引用了 CertPlatform.Admin —— ⛔ 反向依赖（阶段 4 摘除后本规则转正）',
    })
  }
  for (const file of listCsFiles(audDir)) {
    readFileSync(file, 'utf8')
      .split('\n')
      .forEach((line, i) => {
        if (isCommentLine(line)) return
        if (/^\s*using\s+CertPlatform\.Admin\b/.test(line) || /\bCertPlatform\.Admin\./.test(line)) {
          violations.push({ file: rel(file), line: i + 1, text: line.trim().slice(0, 120) })
        }
      })
  }
  return { violations, skipped: false }
}

/** A-R3：`Shared` ⛔ 引任何一端（会成环 ⇒ 两端重新变成「一个系统」） */
function runAR3() {
  const violations = []
  const sharedDir = join(API_ROOT, 'CertPlatform.Shared')
  const proj = join(sharedDir, 'CertPlatform.Shared.csproj')
  const projText = readText(proj)
  for (const bad of [
    'CertPlatform.Admin.csproj',
    'CertPlatform.Auditor.csproj',
    'CertPlatform.Enterprise.csproj',
  ]) {
    if (projText.includes(bad)) {
      violations.push({ file: rel(proj), line: 0, text: `Shared 引用了 ${bad} —— ⛔ 会成环` })
    }
  }
  for (const file of listCsFiles(sharedDir)) {
    readFileSync(file, 'utf8')
      .split('\n')
      .forEach((line, i) => {
        if (isCommentLine(line)) return
        if (/^\s*using\s+CertPlatform\.(Admin|Auditor|Enterprise)\b/.test(line)) {
          violations.push({ file: rel(file), line: i + 1, text: line.trim().slice(0, 120) })
        }
      })
  }
  return { violations, skipped: false }
}

/**
 * A-R5：禁死 using。
 *
 * 判据：`using CertPlatform.{Admin,Auditor}.<X>;` 而 `<X>` 命名空间下的**类型零引用**。
 * 成因：端 csproj 有 `<Using Include="CertPlatform.Shared.Entities.*" />` 全局 using
 * ⇒ 文件里写 `CertStage` 命中的是 **Shared**，文件顶那句 Admin using 是历史残留。
 * （2026-10-12 已清 14 条；本规则防回归。）
 */
function runAR5() {
  const violations = []
  for (const root of ['CertPlatform.Admin', 'CertPlatform.Auditor', 'CertPlatform.Shared']) {
    for (const file of listCsFiles(join(API_ROOT, root))) {
      const lines = readFileSync(file, 'utf8').split('\n')
      const codeOnly = lines.filter((l) => !isCommentLine(l)).join('\n')
      lines.forEach((line, i) => {
        const m = line.match(/^\s*using\s+(CertPlatform\.(?:Admin|Auditor)\.[\w.]+)\s*;/)
        if (!m) return
        const types = typesOfNamespace(m[1])
        if (!types || types.size === 0) return // 目录不存在 ⇒ 不判（避免误报）
        let hit = false
        for (const t of types) {
          if (new RegExp(`\\b${t}\\b`).test(codeOnly)) {
            hit = true
            break
          }
        }
        if (!hit) {
          violations.push({
            file: rel(file),
            line: i + 1,
            text: `死 using：${m[1]} 下 ${types.size} 个类型零引用`,
          })
        }
      })
    }
  }
  return { violations, skipped: false }
}

/**
 * A-R6：★ 端内禁「核心资源实体」裸直查。
 *
 * 判据：`CertPlatform.{Admin,Auditor,Enterprise}` 下出现
 *   `GetListAsync<T>` / `GetOneAsync<T>` / `CountAsync<T>` / `Queryable<T>` / `SqlQueryAsync<T>`
 * 且 `T` ∈ 核心资源实体集 ⇒ 违规。取数只允许走 `Shared/Domain/Scope/CoreResourceQuery.cs`。
 *
 * ★ 铁律 L5（一个业务概念一个检索入口）：**同一个 WHERE 出现在第二处 ⇒ 就是违规。**
 *   端内只允许「投影」：选字段 / 加派生列 / 排序分页 / 组装 DTO。
 *
 * ★ 存量豁免 = `debtBudget`（**按文件预算，只准减不准增**，2026-10-13 起）：
 *   原「文件级 debt」有盲区 —— 文件在白名单内 ⇒ 该文件**新增**任何核心资源直查也静默通过。
 *   现改为 `文件 → 允许的最大命中数`：超出即失败；减掉一处请同步下调预算；全清后删行。
 * ⛔ 不加实体到本清单 = 该实体处于守卫盲区。
 */
const CORE_RESOURCE_ENTITIES = [
  // ── 原 9 类（2026-10-12 首批纳管）──
  'ISOStandard',
  'ISOClause',
  'CertStage',
  'CertOrgStandard',
  'CertOrgStage',
  'FillParamDef',
  'DocFillPrompt',
  'DocExtractionRule',
  'StandardDirectoryFile',
  // ── 核心域全口径扩充 5 类（2026-10-13 用户裁决「不留盲区」，原守卫盲区）──
  'StandardDirectoryFolder',
  'StandardDirectoryConfig',
  'Enterprise',
  'DocTemplate',
  'CertificationBody',
  // ── 挂账 3 类（实体未下沉 ⇒ 阶段 2/3 收口；`Shared` ⛔ 不能引 `Admin`/`Auditor`，A-R3）──
  'CertEnterpriseStage',
  'FileRequirement',
  'ConclusionRule',
  // ── 2026-10-13 追加：ValidationRule（实体**已在** Shared/Entities/Cert ⇒ 非挂账，4 处直查已收口）──
  'ValidationRule',
]

function runAR6() {
  const violations = []
  const pattern = new RegExp(
    `\\b(?:GetListAsync|GetOneAsync|CountAsync|Queryable|SqlQueryAsync|GetPageListAsync)\\s*<\\s*(?:${CORE_RESOURCE_ENTITIES.join('|')})\\s*>`,
  )
  for (const root of ['CertPlatform.Admin', 'CertPlatform.Auditor', 'CertPlatform.Enterprise']) {
    const dir = join(API_ROOT, root)
    if (!existsSync(dir)) continue
    for (const file of listCsFiles(dir)) {
      readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, i) => {
          if (isCommentLine(line)) return
          if (pattern.test(line)) {
            violations.push({ file: rel(file), line: i + 1, text: line.trim().slice(0, 120) })
          }
        })
    }
  }
  return { violations, skipped: false }
}

/** A-R7：`Shared/Domain/**` 禁宿主语义（`IUserContext` / `HttpContext`）—— 随 L4 纪律落地 */
function runAR7() {
  const violations = []
  const dir = join(API_ROOT, 'CertPlatform.Shared/Domain')
  if (!existsSync(dir)) return { violations, skipped: false }
  for (const file of listCsFiles(dir)) {
    readFileSync(file, 'utf8')
      .split('\n')
      .forEach((line, i) => {
        if (isCommentLine(line)) return
        if (/\bIUserContext\b|\bHttpContext\b/.test(line)) {
          violations.push({ file: rel(file), line: i + 1, text: line.trim().slice(0, 120) })
        }
      })
  }
  return { violations, skipped: false }
}

/**
 * A-R10：★ 归属模型 V2 —— 已废除的机构常量字面量全仓禁（2026-10-13 用户裁定）。
 *
 * 背景：旧模型用两个 magic value 标记「标准域」——
 *   · `YZH-STD-ENT`（旧 `YzhVirtualEnterprise.Code`）—— 2026-10-12 已删；
 *   · `yzh-std-env`（`CoreOrgCodes.StdEnv`）—— **2026-10-13 归属模型 V2 废除**。
 *
 * ★ **归属模型 V2（三态 + 机构冗余）**：
 *   · 机构行     = `OrgCode` 有值（机构 Code）；
 *   · 企业行     = `EnterpriseCode` 有值（可带 `OrgCode` = 机构 Code，冗余合法）；
 *   · 平台标准级 = **两列都空**（⛔ 不再用任何常量标记）。
 *   ⇒ 两个常量**都不再有存在理由**：后端 `CoreOrgCodes` 类已删除、
 *     前端 `cert/cert-share/src/constants/coreOrgCodes.ts` 已删除。
 *
 * 判据：全仓源码（`.cs` / `.ts` / `.vue` / `.js`，跳过注释行）⛔ 禁止出现：
 *   ① `YZH-STD-ENT`（2026-10-12 已废弃）；
 *   ② `yzh-std-env`（2026-10-13 归属模型 V2 废除）。
 *
 * ⚠️ 扫描范围 = 前后端源码；⛔ **不扫**：
 *   · `docs/`（历史记录需保留原值，改了反而读不懂）
 *   · `scripts/db/`（迁移 SQL 必须引用旧值才能清理）
 *   · `dist/`（构建产物，随源码重新生成）
 *   · **注释行**（写明「旧值已废弃」是有价值的留痕，规则要拦的是**代码里还在用**）
 *
 * ★ 本规则**吸收了已退役的三条**：A-R8（`OrgCode` 空值判据）/ A-R9（`yzh-std-env` 字面量唯一）
 *   / A-R11（企业列禁机构常量）—— 它们监管的常量本身已不存在，故一并退役。
 */
function runAR10() {
  const violations = []
  const SKIP = new Set(['obj', 'bin', 'node_modules', '.git', 'Migrations', 'dist', '__pycache__'])
  const EXTS = ['.cs', '.ts', '.vue', '.js']
  const roots = [
    join(API_ROOT, 'CertPlatform.Shared'),
    join(API_ROOT, 'CertPlatform.Admin'),
    join(API_ROOT, 'CertPlatform.Auditor'),
    join(API_ROOT, 'CertPlatform.Enterprise'),
    join(API_ROOT, 'CertPlatform.Admin.Tests'),
    join(WEB, 'cert'),
    join(WEB, 'yzh.vue.core/src'),
  ]
  const rec = (d) => {
    if (!existsSync(d)) return
    for (const e of readdirSync(d, { withFileTypes: true })) {
      if (SKIP.has(e.name)) continue
      const p = join(d, e.name)
      if (e.isDirectory()) {
        rec(p)
        continue
      }
      if (!EXTS.some((x) => e.name.endsWith(x))) continue
      const r = rel(p)
      readFileSync(p, 'utf8')
        .split('\n')
        .forEach((line, i) => {
          if (isCommentLine(line)) return
          if (line.includes('YZH-STD-ENT')) {
            violations.push({
              file: r,
              line: i + 1,
              text: `已废弃的旧标记值 YZH-STD-ENT（归属模型 V2：平台标准级 = 两列都空）：${line.trim().slice(0, 80)}`,
            })
          } else if (/["']yzh-std-env["']/.test(line)) {
            violations.push({
              file: r,
              line: i + 1,
              text: `已废除的机构常量 yzh-std-env（归属模型 V2：平台标准级 = 两列都空）：${line.trim().slice(0, 80)}`,
            })
          }
        })
    }
  }
  roots.forEach(rec)
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
//   Admin / Auditor 两端的**生产代码**引用次数（跳过定义文件自身、跳过 bin/obj/node_modules、
//   ⛔ 不计测试工程 —— 见 PROJECT_DIRS.Tests 处 2026-10-14 裁定说明）。
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
  // ⚠️ 测试工程**不参与** R17 的端归属计数（2026-10-14 用户裁定）。
  //    理由：R17 判的是「**生产代码**里谁真的读这张表」。测试工程引用某类型
  //    只说明「有人给它写了测试」，不构成「那一端在生产里用它」。
  //    ★ 触发案例：CertPlatform.Admin.Tests 引用 Auditor 以测 IFillParamService，
  //      测试文件里出现 FillParamValue（Auditor 独占实体）7 次 ⇒ 旧口径误判
  //      「Admin=7 / Auditor=24 ⇒ 双端共用 ⇒ 分层倒退」。实测 Admin 生产代码 = 0 次。
  //    ★ 副作用实测：摘除该桶后守卫 0 违规，且**不新增任何其他违规**
  //      （说明该桶当时只制造了这一个假阳性）。
  //    ⛔ 保留本常量供未来规则使用，勿删。
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

/* ── R17 登记例外：Shared/Entities 中「当前只有一端接线、但已裁定双端共用」的实体 ──
 *
 * 默认判据把「Shared 里只有一端引用」判为应下沉（24 号 §一）。但有一类实体属于
 * 「设计上双端共用，只是另一端尚未接线」—— 此时按默认判据下沉，会逼出两份同义实现。
 *
 * 登记三条门槛（须同时满足，缺一不可）：
 *   ① 双端使用场景明确（不是「以后可能会用」）
 *   ② 下沉会造成重复实现（两套同义代码 / 两个同义列）
 *   ③ 用户已明确裁定 —— reason 必须写明裁定日期与原话要点
 *
 * ⛔ 本表不是「压告警」的白名单：未登记的实体一律照规则报错；
 *    已登记的实体若日后另一端长期不接线，须回访并撤登记。
 *
 * ★ 2026-10-06 用户裁定原话：「我的建议还是放置到 share 中，毕竟后台的填充和
 *   专家端的填充，都需要进行复用」—— 下列三个实体据此留在 CertPlatform.Shared/Entities/。
 */
const R17_SHARED_ALLOW = new Map([
  [
    'DocFillPrompt',
    {
      since: '2026-10-06',
      reason:
        '填写提示词（SystemPrompt/UserTemplate）：后台填写规则页配置、专家端规范化填充读取，双端共用同一份提示词',
    },
  ],
  [
    'DocTableFieldDef',
    {
      since: '2026-10-06',
      reason:
        '表格字段定义：后台填写规则页定义列口径，专家端取值/落笔共用同一字段口径，下沉会造出两套字段定义',
    },
  ],
  [
    'StandardDocContract',
    {
      since: '2026-10-06',
      reason:
        '标准文档契约：Admin 侧语义分析写入（StandardDocContractController），Auditor 侧规范化读取（P1 编排器），双端共用同一契约行',
    },
  ],  [
    'CertOrgStandard',
    {
      since: '2026-10-10',
      reason:
        '机构-标准关联：后台配置两端复用同一份中间表，下沉 Shared 后两端均需重新接线；当前 Auditor 仅通过 using alias 引用，暂未影响业务',
    },
  ],
  [
    'ISOClause',
    {
      since: '2026-10-12',
      reason:
        'ISO 条款：检索唯一入口 ICoreResourceQuery.GetClausesAsync/GetClauseByCodeAsync 定义在 Shared，返回类型必须留 Shared/Entities；Auditor 端 2026-10-12 批 3 接线后改为经入口取数，不再直接引用实体类',
    },
  ],
  [
    'OrgStandardStageView',
    {
      since: '2026-10-13',
      reason:
        '「机构×标准×阶段」关联链视图（纯只读 V，DDL = scripts/db/views/v_org_standard_stage.sql）：检索唯一入口 ICoreResourceQuery.GetOrgStandardStageChainAsync 定义在 Shared，返回类型必须留 Shared/Entities（下沉到 Admin 会让 Shared 反向引用 Admin ⇒ 成环）；当前仅 Admin 端 StandardDirectoryService（T1 组织树）消费，Auditor 端接线后同样经该入口取数',
    },
  ],
  [
    'DocExtractionRule',
    {
      since: '2026-10-13',
      reason:
        '提取规则：检索唯一入口 ICoreResourceQuery.GetDocExtractionRuleByCodeAsync / GetDocExtractionRulesByStandardFileCodesAsync 定义在 Shared，返回类型必须留 Shared/Entities（下沉到 Admin ⇒ Shared 反向引用 Admin，A-R3 硬违规）。双端真实在用：Admin 写/管（DocExtractionRuleService 等 34 处），Auditor 经入口读 4 站（GapDetector:469 · GapFillService:204/300 · EnterpriseExtractTaskExecutor:128 · EnterpriseFileService:2307，均 var 接收 ⇒ 文本计数为 0，非真单端）。设计文档《核心资源检索统一-设计-V1》§1.2 实体分布表标注 Shared/Entities/Doc ✅（2026-10-13）',
    },
  ],
])

function runR17() {
  const violations = []

  for (const [owner, dir] of Object.entries(ENTITY_DIRS)) {
    for (const file of listCsFiles(dir)) {
      const cls = basename(file, '.cs')
      const text = readFileSync(file, 'utf8')
      const st = text.indexOf('SugarTable')
      if (st < 0) continue // 纯常量类 / DTO / enum 不参与归属判定

      // ⚠️ 只数 Admin **生产代码**：⛔ 不含 PROJECT_DIRS.Tests（见该常量处 2026-10-14 裁定说明）
      const nAdmin = countRefs(cls, [PROJECT_DIRS.Admin])
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
        // ★ 登记例外：已裁定双端共用、只是另一端尚未接线 ⇒ 跳过（见 R17_SHARED_ALLOW 门槛）
        if (R17_SHARED_ALLOW.has(cls)) continue
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
 *      R20 = 组件写法条款（按钮/空态/确认框/弹窗/标签/图标/布局/!important/主题可适配性）。
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
 *   - S05 豁免 YzhEmptyState.vue（el-empty 的封装实现体本身——S05 管的是页面层）
 *   - S06 豁免 confirm.ts / useConfirm.ts（confirmOrFalse 的实现体）
 *   - S07 豁免 YzhDialog.vue / YzhDrawer.vue / YzhFormDialog.vue（它们自身必须包 el-dialog / el-drawer）
 * ========================================================================== */

/** 法条基线文件（相对 WEB） */
const CLAUSE_BASELINE = join(WEB, 'scripts/style-clause-baseline.json')

/** 条款豁免：该文件是这条写法的实现体本身 */
const CLAUSE_EXEMPT = {
  S04: ['cert/cert-share/src/components/MessageBell/MessageBell.vue'],
  S05: ['yzh.vue.core/src/components/ui/YzhEmptyState.vue', 'cert/cert-share/src/components/MessageBell/MessageBell.vue'],
  S08: ['cert/cert-share/src/components/MessageBell/MessageBell.vue'],
  S06: ['yzh.vue.core/src/utils/confirm.ts', 'yzh.vue.core/src/composables/useConfirm.ts', 'yzh.vue.core/src/api/push.ts'],
  S07: [
    'yzh.vue.core/src/components/layout/YzhDialog.vue',
    'yzh.vue.core/src/components/layout/YzhDrawer.vue',
    'yzh.vue.core/src/components/form/YzhFormDialog.vue',
  ],
  // S13 豁免 = 颜色是「品牌资产 / 数据的一部分」，改令牌会丢语义：
  //   - 登录 / 注册页：品牌渐变（AGENTS.md ⑤ 明文豁免「登录页品牌渐变」）
  //   - LogicFlow 画布 / ECharts：颜色由画布消费，不参与 CSS 主题（与 R18 的 STYLE_ALLOW 同源）
  S13: [
    'yzh.vue.core/src/pages/auth/Login.vue',
    'cert/cert-auditor/src/layouts/AuditorLogin.vue',
    'cert/cert-auditor/src/pages/register/index.vue',
    'cert/cert-share/src/components/workflow/WorkflowDesigner.vue',
    'cert/cert-share/src/composables/workflow/specialNodes.ts',
    'cert/cert-share/src/composables/workflow/compiler.ts',
    'cert/cert-admin/src/pages/workflow/ai-usage/index.vue',
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
      // MessageBox / 确认工具的选项对象（{ type: 'warning' } 是弹窗样式，不是按钮语义色）
      if (/confirmButtonText|ElMessageBox|confirmOrFalse|confirmChoice|useConfirm/.test(before)) continue
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
    // 法条范围是「状态 / 启用 / 结果」徽标；⛔ 不含**用户可维护的标签集合**
    // （`closable` 的可删 chip —— 那是标签编辑器控件，不是状态展示）。
    // 判据：带 `closable` 且**没有**任何 type 绑定（写死 type="info" 也算状态色）的排除。
    const tags = text.match(/<el-tag(?![\w-])[\s\S]*?>/g) || []
    return tags.filter((t) => {
      if (!/\bclosable\b/.test(t)) return true
      return /(?:^|[\s"'])(?::|v-bind:)?type\s*=/.test(t)
    }).length
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
  // S13 主题可适配性（明暗双主题）：background / background-color / color 里出现
  // 「不随主题变化的颜色」= 裸 hex 或 黑白 rgba。
  //
  // 为什么单独一条（2026-10-17 用户裁定 D5）：
  //   ① R18 只查 `<style>` 块内的裸 hex + 字号/间距 px —— 漏 rgba，也漏 `<template>` 内联 style
  //      与 TS 字面量（`background: '#fafbfc'` 这类写在对象里，R18 完全看不见）；
  //   ② 暗色主题（`html.dark`）下，`rgba(255,255,255,.05)` 叠在白底上尚可，叠在深底上仍是白蒙层；
  //   ③ 判据只认「颜色类声明」，`box-shadow` / `border-color` 不计 —— 阴影与描边属主题中性，
  //      强转令牌反而丢语义（与 R18「width/height 不计」同源考虑）。
  //   ⚠️ 令牌兜底 `var(--x, #fff)` 已由 stripStyleNoise 剥掉：兜底值不参与主题切换，是合法写法。
  S13(text) {
    const clean = stripStyleNoise(text)
    let n = 0
    const declRe = /(?:^|["';\s{])(background(?:-color)?|color)\s*:\s*([^;}]*)/g
    for (const m of clean.matchAll(declRe)) {
      const v = m[2]
      if (/#[0-9a-fA-F]{3,8}\b/.test(v) || /rgba?\(\s*(?:0|255)\s*,/.test(v)) n++
    }
    return n
  },
}

/**
 * ★ 测试文件豁免（2026-10-04 修守卫自身缺陷）。
 *
 * <para>S 法条（S03–S13）针对的是**生产 UI 代码**的组件写法。测试文件里的
 * <c>ElMessageBox.confirm</c> 是给真实实现**打桩的 mock**，它反过来去 spy 被测代码 ——
 * 扫它等于把测试替身当成生产代码的「二次确认直调」，属误判。</para>
 *
 * <para>实测触发：`prompt-template/index.test.ts:55,57` 两个 mock 被判 S06 违规（基线 0）
 * ⇒ 逼着加基线豁免，而豁免是<b>永久债务</b>（测试文件永远不合格）。
 * 正解是<b>测试文件不进法条扫描</b>。</para>
 */
function isTestFile(file) {
  const f = String(file)
  return /\.(test|spec)\.[cm]?[jt]sx?$/.test(f)
    || /[/\\]__tests__[/\\]/.test(f)
    || /\.test-d\.[jt]sx?$/.test(f)
}

function runR20() {
  const files = walkAll(STYLE_ROOTS, STYLE_EXTS).filter((f) => !isTestFile(f))
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
      // ★ 真实偏离（非误报）：SysMenu DTO 为 camelCase API 契约（见 api/system/menu.ts normalizeMenu），
      //   与 TreeNode PascalCase 属不同体系；YzhMenuNode.vue 已通过重命名变量规避，此处显式登记。
      'yzh.vue.core/src/pages/system/menu/MenuParentPicker.vue',
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
    //
    // ★ 2026-10-17 作用域修正（用户裁定 D3「需要检查」→ 检查后扩域）：
    //   原 roots 只有 PAGE_ROOTS（pages + layouts），**不含 cert-share** ⇒ 共享层表格成盲区：
    //   同一目录的 <el-dialog> 被 R20(S07) 扫到，<el-table> 却无人管。
    //   实测扩域后新增命中 7 处 / 3 文件（ExecutionHistoryDrawer 3 · QueueFileMonitor 3 ·
    //   ExecutionResultPanel 1）+ 核心组件 1 处（YzhTreeTableCheckSelector），已全部登记。
    //   ⚠️ 与 S07 的「地盘」对齐：R20 用 STYLE_ROOTS（四包整个 src）。
    roots: [...PAGE_ROOTS, join(WEB, 'cert/cert-share/src'), join(WEB, 'yzh.vue.core/src/components')],
    exts: ['.vue'],
    forbid: [/<el-table\b/],
    skipComments: true,
    // ★ 实现体豁免（永久）：这两个文件**本身就是表格组件**，包 el-table 是它们的职责。
    //   `YzhTreeTableCheckSelector` 之所以不套 `YzhTable`：它整套语义挂在 el-table 实例上
    //   （`toggleRowSelection` / `clearSelection` + `tree-props.checkStrictly`），
    //   套一层只会把「selection 与 checkedKeys 必须恒等」这条隐式契约隔开一层，
    //   收益为零、风险为正（文件顶部 36–44 行记录过级联错位事故）。
    exempt: [
      'yzh.vue.core/src/components/table/YzhTable.vue',
      'yzh.vue.core/src/components/layout/YzhTreeTableCheckSelector.vue',
    ],
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
      // cert-auditor 新代码区基线为 0：queue/index.vue 主从双表 + 自定义渲染，重构为 YzhTable 属独立任务
      'cert/cert-auditor/src/pages/queue/index.vue',
      // MessageBell 为新文件，R18/R20 待修（独立任务）
      'cert/cert-share/src/components/MessageBell/MessageBell.vue',
      // ★ 2026-10-17 扩域新增的三处共享层债务 —— 已于第 2 批全部收口（3 文件 / 7 处 → 0），
      //   故对应条目已删除；`YzhTreeTableCheckSelector` 转入上面的 exempt。
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
    desc: '后端实体归属一致性（单端实体不得留在 Shared/Entities；已下沉的不得两端共用；例外见 R17_SHARED_ALLOW）',
    run: runR17,
    // ✅ 2026-10-03 分层落地时 debt 清零：Admin 独占 22 个 + Auditor 独占 15 个已全部下沉，
    //    Shared/Entities 现在只剩双端共用实体（24 号清单 §三）。
    //    新增实体时按 24 号 §一 选对目录 —— 放错会被本规则直接拦下。
    // ★ 2026-10-06：「当前只有一端接线、但已裁定双端共用」的实体走 R17_SHARED_ALLOW
    //    显式登记例外（三门槛 + 裁定原话），⛔ 不是改判据、⛔ 不是压告警。
    debt: [],
  },
  {
    id: 'R18',
    type: 'custom',
    desc: '硬编码样式只准减不准增（裸 hex / font-size·padding·margin 裸 px；基线 scripts/style-baseline.json）',
    run: runR18,
    debt: ['cert/cert-share/src/components/MessageBell/MessageBell.vue'],  // 新文件，R18 待修（独立任务）
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
  {
    id: 'R21',
    type: 'custom',
    desc: '上传契约单源（生成物同步 + 禁字面量 accept + file 选择面必接 upload-file-policy）',
    run: runR21,
    debt: [],
  },
  {
    id: 'R22',
    type: 'custom',
    desc: '工作流跨边界命名 G2：node.config 内层键必须 PascalCase（禁 config.docType 等小写 B 键）',
    run: runR22,
    debt: [],
  },
  {
    id: 'R23',
    type: 'custom',
    desc: '工作流跨边界命名 G3：禁归一化层 portNormalize/normalizePorts 复活（端口统一 PascalCase）',
    run: runR23,
    debt: [],
  },
  {
    id: 'R24',
    type: 'custom',
    desc: '页面 logic.ts 必须继承内核基类（禁裸 class / 模块级单例；纯函数模块除外）',
    run: runR24,
    // ★ 2026-10-17 首次启用（用户裁定 D4「增加守卫」）：实测存量 4 文件。
    //   ⚠️ 用 debtBudget（按文件处数）而非 debt（按文件整体豁免）—— 后者有盲区：
    //      文件进了白名单，再往里新增第二个裸 class 也静默通过（A-R6 踩过同一个坑）。
    //   每页收口 ⇒ 下调 / 删行；删完 ⇒ 规则转正，新页面写裸 class 直接失败。
    debtBudget: {
      // ★ 2026-10-19：`resources` 已拆分 + extends SingleTableCore ⇒ 债务归零、删行
      //   （logic.ts 1405 → 741 行；上传状态机 → useUpload.ts，文件操作 → useFileActions.ts）
      'cert/cert-auditor/src/pages/enterprise-review/logic.ts': 1,
      // ★ 2026-10-19：`enterprise-fill-params` 已 extends SingleTableCore ⇒ 债务归零、删行
      // ★ 2026-10-19：`enterprise-original` 已整类化（原 28 个顶层 ref → 69 字段 / 38 方法）
      //   ⇒ 债务归零、删行。⚠️ 该文件仍**导出** 16 个纯函数（statusOf / buildTree …）
      //   供 index.vue 与 OriginalFolderTree.vue 共用 —— 那是模块级**函数**不是状态，R24 放行。
    },
  },
  {
    id: 'R25',
    type: 'custom',
    desc: '暗色层完整性（:root 的 --yzh-color-* 被引用时，html.dark 必须有覆盖）',
    run: runR25,
    debt: [],
  },
  {
    id: 'R26',
    type: 'custom',
    desc: '免认证纪律：全仓禁 YZHAnonymous 字面量；certplatform-api 内 [AllowAnonymous] 仅限 D6 白名单（见 runR26）',
    run: runR26,
    // ★ 2026-10-17 首次启用（D2/D6 已定）：
    //   - YZHAnonymousAttribute.cs 定义文件豁免（本轮删除后基线归零）
    //   - certplatform-api [AllowAnonymous] 白名单 = 4 处（见 runR26 ALLOWED）
    //   - yzh-core [AllowAnonymous] 不在本守卫范围（框架层 AGENTS §11 单独管控）
    debt: [],
  },
  // ===== 审计 §8（标准目录链路）：全部为后端 .cs 扫描，debt 随修复逐条删除 =====
  {
    id: 'R-A',
    type: 'custom',
    desc: '服务层禁止单参 UpdateAsync(entity)（全列写回 = 陷阱 ㉑ / P1-14）',
    run: runRA,
    // 2026-10-17 第 3 批：6 文件全部列级化，债务清零；R-A 同时覆盖 Controllers 后仍 0 违规。
    debt: [],
  },
  {
    id: 'R-B',
    type: 'custom',
    desc: '控制器禁止直接继承裸 ControllerBase（白名单 Auth / 健康检查；应继承 YzhControllerBase<V> 或 WebControllerBase）',
    run: runRB,
    // 迁移到 YzhControllerBase<V> / WebControllerBase（路由已显式 [Route]、ApiCode 不变）后删除对应行
    // 2026-10-17 第 2 批：9 个已迁；R-B 债务清零。
    debt: [],
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
  // ===== ★ 跨端共享与核心资源检索铁律（docs/30-项目规则/跨端共享与核心资源检索规则-V1.md）=====
  {
    id: 'A-R1',
    type: 'custom',
    desc: 'Admin ⛔ 引 Auditor（三方结构：两端互不引用，只能通过 Shared 共享）',
    run: runAR1,
    debt: [],
  },
  {
    id: 'A-R2',
    type: 'custom',
    desc: 'Auditor ⛔ 引 Admin（反向依赖，已全部摘除 ⇒ 规则转正，零容忍）',
    run: runAR2,
    // ★ 2026-10-12 已解耦：Auditor 侧 8 个文件 + csproj 全部改为消费 CertPlatform.Shared 契约
    //   （IExtractionRuleQuery / IPromptWorkbench / IWorkflowRunner / TableRegionBuilder /
    //     Shared.Fill.Ai / Shared.Workflow.Models / Shared.Services.Ent …），
    //   实现侧在 Admin 经 DI 注册为 Shared 接口。实测 `using CertPlatform.Admin` = 0 处
    //   ⇒ debt 清空，本规则自本次起为硬性零容忍（任何回潮即失败）。
    debt: [],
  },
  {
    id: 'A-R3',
    type: 'custom',
    desc: 'Shared ⛔ 引任何一端（Admin/Auditor/Enterprise —— 会成环，两端重新变成「一个系统」）',
    run: runAR3,
    debt: [],
  },
  {
    id: 'A-R5',
    type: 'custom',
    desc: '禁死 using（using 的命名空间下零类型被引用；成因 = csproj 全局 using 已覆盖）',
    run: runAR5,
    debt: [],
  },
  {
    id: 'A-R6',
    type: 'custom',
    desc: '端内禁核心资源实体裸直查（只允许 Shared/Domain/Scope/CoreResourceQuery —— 铁律 L5）',
    run: runAR6,
    // ★ 2026-10-12 首次启用：实测存量 23 文件 / 131 处（按文件路径子串匹配）。
    //   接线一处 ⇒ 下调该文件预算；一个文件全清 ⇒ 从本清单删一行；删完 ⇒ 规则转正（零容忍）。
    //   2026-10-12 接线第一轮（13 处 ISOStandard）后：22 文件 / 118 处。
    //   2026-10-12 接线第二轮（31 处 StandardDirectoryFile 清晰站：Code 单查/集合、ConfigCode、FolderCode）后：
    //   剩 20 文件 / 87 处；已全清移出：ExpertResultController.cs · PromptWorkbenchService.cs · StandardDocContractController.cs。
    //   2026-10-12 接线第三轮（31 处：includeDisabled 站 12 + CertStage/ISOClause 站 13 + 提取/填写规则站 6）后：
    //   剩 16 文件 / 56 处；已全清移出：QueueMonitorController.cs · ExpertTaskService.cs · GapFillService.cs · IsStdFileMissingSkill.cs。
    //   2026-10-13 扩容轮：实体清单 9→18 类（核心域全口径 + ValidationRule）+ 豁免改**按文件预算**
    //   （原文件级 debt 的盲区：文件在白名单内 ⇒ 新增直查也静默通过）。当前 26 文件 / 138 处。
    //   ⚠️ 判据：`Shared/Domain/Scope/CoreResourceQuery.cs` 是**唯一**核心资源检索入口；
    //      端内只允许「投影」（选字段 / 派生列 / 排序分页 / 组装 DTO），⛔ 不许自己写 WHERE。
    debtBudget: {
      // ---- ★ 挂账（实体未下沉 ⇒ 阶段 2/3 收口；本文件内含其直查）----
      // CertEnterpriseStage：EnterpriseFileService(3) · EnterpriseOriginalService(2) ·
      //   EnterpriseNormalizeController(1) · FillParamValueController(1) · ExpertTaskService(1) ·
      //   EnterpriseStageController(1) · EnterpriseOriginalAnalyzeExecutor(1) = 10 处
      // EFS 5 = 挂账 CertEnterpriseStage 3 + BY 例外 1（:1829 反向谓词）+ 裸查例外 1（:1514 历史原始行）
      'CertPlatform.Auditor/Services/Ent/EnterpriseFileService.cs': 5,
      'CertPlatform.Auditor/Controllers/EnterpriseNormalizeController.cs': 4,
      'CertPlatform.Auditor/Services/Ent/EnterpriseOriginalService.cs': 2,
       'CertPlatform.Auditor/Controllers/FillParamValueController.cs': 3,
      'CertPlatform.Auditor/Services/Expert/ExpertTaskService.cs': 1,
      'CertPlatform.Auditor/Controllers/EnterpriseStageController.cs': 1,
      'CertPlatform.Auditor/Services/Ent/EnterpriseOriginalAnalyzeExecutor.cs': 1,
      // FileRequirement：DocExtractionRuleService(1) + .AI(1) = 2 处
      'CertPlatform.Admin/Services/DocExtraction/DocExtractionRuleService.cs': 1,
      'CertPlatform.Admin/Services/DocExtraction/DocExtractionRuleService.AI.cs': 1,
      // ConclusionRule：NcConclusionCalcSkill(1) = 1 处
      'CertPlatform.Admin/Services/Workflow/Skills/NcConclusionCalcSkill.cs': 1,
      // ConclusionRule（挂账）：NcConclusionV2Skill 的 LoadMatrix 站 1 处 —— 实体仍在
      //   Admin/Entities ⇒ Shared 取不到（A-R3），与上面同批同款，随实体下沉一并清零
      'CertPlatform.Admin/Services/Workflow/Skills/NcConclusionV2Skill.cs': 1,
      // ---- ★ 死类冻结（2026-10-06 已从 DI 摘除，仅存对照 ⇒ ⛔ 不改造、⛔ 不得增长）----
      'CertPlatform.Admin/Services/Ent/Executors/EnterpriseDocNormalizationExecutor.cs': 6,
      // ---- 其余 = 1.B.6d-4 可收敛 + 6d-3 一次性例外（2026-10-13 6d-3 已收口 16 处：
      //      SDS 13 · DTC 1 · EFS 1 · UQC 1（全清删行）；收敛后逐行下调 / 删除）----
      // SDS 剩 8 = BY 例外 4（:676/:916/:970/:2417）+ 裸查例外 4（folder FullPath / 卡死扫描 / 重试候选 / 回填粗筛）
      'CertPlatform.Admin/Services/StandardDirectory/StandardDirectoryService.cs': 8,
      // DTC 剩 6 = 6d-4 模板/提示 5 + stdFileCodes 裸查例外 1（:616 无默认过滤语义，含已删/禁用行）
       'CertPlatform.Admin/Controllers/Workflow/DocTemplateController.cs': 4,
      'CertPlatform.Admin/Controllers/Workflow/DocFillPromptController.cs': 5,
      'CertPlatform.Admin/Controllers/Cert/FillParamDefController.cs': 3,
      'CertPlatform.Admin/Controllers/Workflow/StandardDocContractController.cs': 1,
       'CertPlatform.Auditor/Controllers/DocumentFillController.cs': 1,
       'CertPlatform.Auditor/Controllers/EnterpriseController.cs': 2,
    },
  },
  {
    id: 'A-R7',
    type: 'custom',
    desc: 'Shared/Domain/** 禁宿主语义（IUserContext / HttpContext —— 共享层不得读登录态）',
    run: runAR7,
    debt: [],
  },
  {
    id: 'A-R10',
    type: 'custom',
    desc: "归属模型 V2：全仓禁已废除的机构常量字面量（YZH-STD-ENT / yzh-std-env —— 平台标准级 = 两列都空）",
    run: runAR10,
    // ★ 2026-10-12 新增（YZH-STD-ENT）→ 2026-10-13 归属模型 V2 扩展为「两个常量全禁」。
    //    A-R8（OrgCode 空值判据）/ A-R9（yzh-std-env 字面量唯一）/ A-R11（企业列禁机构常量）
    //    三条旧守卫**同时退役** —— 它们监管的常量本身已不存在（守卫体系由 4 条收敛为 1 条）。
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
    // ★ exempt = 「该文件是这条规则的**实现体本身**」，永久豁免（例：YzhTable 自身必须包 el-table）。
    //   与 debt 的区别：debt 是「欠着的债，将来要还」；exempt 是「法外之地不存在，但这里就是法本身」。
    if (rule.exempt?.some((d) => r.includes(d))) continue
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
  let kept = violations
  if (rule.debtBudget) {
    // ★ 按文件预算（只准减不准增）：文件命中数 ≤ 预算 ⇒ 豁免；> 预算 ⇒ 新增直查，失败。
    const counts = new Map()
    for (const v of violations) counts.set(v.file, (counts.get(v.file) || 0) + 1)
    kept = violations.filter((v) => {
      const key = Object.keys(rule.debtBudget).find((d) => v.file.includes(d))
      return !(key && counts.get(v.file) <= rule.debtBudget[key])
    })
  } else if (rule.debt?.length) {
    kept = violations.filter((v) => !rule.debt.some((d) => v.file.includes(d)))
  }
  if (violations.length > kept.length)
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
