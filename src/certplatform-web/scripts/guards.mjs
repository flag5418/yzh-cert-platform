#!/usr/bin/env node
/**
 * YZH 前端架构守卫（零依赖）
 *
 * 用途：在重构期间固化架构铁律，防止"改完又回潮"。
 * 用法：
 *   node scripts/guards.mjs            # 有违规 → exit 1
 *   node scripts/guards.mjs --report   # 仅报告，不失败
 *
 * 设计：默认失败 + "债务白名单"。白名单里的条目是**已知待修项**，
 * 随重构推进逐条删除，删完即代表该规则全站达标。
 */

import { readdirSync, readFileSync, statSync } from 'node:fs'
import { dirname, join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const WEB = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const REPORT_ONLY = process.argv.includes('--report')

const CORE = join(WEB, 'yzh.vue.core/src/components')
const PAGES = join(WEB, 'cert/cert-admin/src/pages')
const API = join(WEB, 'cert/cert-admin/src/api')

/** 递归收集文件 */
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

const rel = (p) => relative(WEB, p).replaceAll('\\', '/')

/** 是否为行注释（含块注释行内的 * 行） */
function isCommentLine(line) {
  const t = line.trim()
  return t.startsWith('*') || t.startsWith('//') || t.startsWith('/*')
}

/**
 * 规则定义
 * - root: 扫描根目录
 * - exts: 扩展名
 * - forbid: RegExp[]，命中即违规
 * - debt: 已知待修文件（rel 路径包含其一即豁免），删完即达标
 */
const RULES = [
  {
    id: 'R1',
    desc: '原子组件必须零领域依赖（禁 @share / @/api / Logic / store / router）',
    root: CORE,
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
      'yzh.vue.core/src/components/layout/YzhTreeTable.vue',
      'yzh.vue.core/src/components/page/YzhCrudPage.vue',
    ],
  },
  {
    id: 'R2',
    desc: 'TreeNode 一律 PascalCase（禁 node.code / selectedNode.value.name 等小写读取）',
    root: PAGES,
    exts: ['.ts', '.vue'],
    forbid: [
      /\bnode\.(code|name|extra|children|isLeaf|parentCode)\b/,
      /\bn\.(code|name|extra|children|isLeaf|parentCode)\b/,
      /\b(selectedNode|parentNode|treeParentNode|treeEditingNode|stdEditingNode|orgEditingNode|categoryEditingNode)\.value\.(code|name|extra|children|isLeaf|parentCode)\b/,
      /\b(selectedNode|parentNode|treeParentNode|treeEditingNode)\.value\?\.(code|name|extra|children|isLeaf|parentCode)\b/,
    ],
    skipComments: true,
    debt: [
      // 关联型页面（role-user/menu/api）待 PG-C 迁移
      'system/_shared/useRoleTreeBadges.ts',
    ],
  },
  {
    id: 'R3',
    desc: '统一 ApiResponse 契约（禁旧 res.code === 200）',
    root: PAGES,
    exts: ['.ts', '.vue'],
    forbid: [/\.code\s*===\s*200/, /\.code\s*!==\s*200/],
    skipComments: true,
    debt: [
      // menu 页面待 PG-D1 迁移
      'system/menu/logic.ts',
      // 手写页面待 PG-E1（后端迁基类）后重写
      'workflow/directory/',
    ],
  },
  {
    id: 'R3b',
    desc: '统一 ApiResponse 契约（api 模块层，禁旧 res.code === 200）',
    root: API,
    exts: ['.ts'],
    forbid: [/\.code\s*===\s*200/, /\.code\s*!==\s*200/],
    skipComments: true,
    debt: ['api/system/menu.ts'],
  },
]

let totalViolations = 0

for (const rule of RULES) {
  const files = walk(rule.root, rule.exts)
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
  if (violations.length) {
    totalViolations += violations.length
    console.log(`\n✗ ${rule.id} ${rule.desc}`)
    for (const v of violations) console.log(`   ${v.file}:${v.line}  ${v.text}`)
  } else {
    console.log(`✓ ${rule.id} ${rule.desc}`)
  }
}

// 债务盘点（报告，不失败）：页面直连 @/api
const directApi = walk(PAGES, ['.ts', '.vue']).filter((f) =>
  /from\s+['"]@\/api/.test(readFileSync(f, 'utf8')),
)
if (directApi.length) {
  console.log(`\nℹ 债务盘点：${directApi.length} 个页面文件仍直连 @/api（PG-D/PG-C 迁移后应清零）：`)
  for (const f of directApi) console.log(`   ${rel(f)}`)
}

console.log(
  `\n${totalViolations === 0 ? '守卫通过' : `守卫失败：${totalViolations} 处违规`}${
    REPORT_ONLY ? '（report 模式，不阻断）' : ''
  }`,
)

if (totalViolations > 0 && !REPORT_ONLY) process.exit(1)
