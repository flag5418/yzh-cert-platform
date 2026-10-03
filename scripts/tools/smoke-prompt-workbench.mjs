/**
 * 提示词工作台 · 接口冒烟测试（2026-10-03）
 *
 * 用途：验证「保存 → 读回 → 切类型 → 切回」这条链路在**真实后端**上是通的。
 *   背景：用户实测报出「选食品标准 → AI 生成 → 切换作用提示词 → 之前的提示词清空了」。
 *   前端内存态丢失已由 `cert-admin/src/pages/workflow/prompt-template/index.test.ts` 覆盖；
 *   本脚本证明**服务端**这一段没问题（排除「后端把内容弄丢了」这一可能）。
 *
 * 跑法（后端 9992 需在运行）：
 *   node scripts/tools/smoke-prompt-workbench.mjs
 *   BASE=http://127.0.0.1:9992 node scripts/tools/smoke-prompt-workbench.mjs
 *
 * ⚠️ 唯一的写操作：把目标标准那条**已有**提示词**原样**再存一次（内容逐字节不变）。
 *    幂等 upsert，不新增、不删除任何行。若该标准没有提示词行，则跳过写操作。
 */
const BASE = process.env.BASE || 'http://127.0.0.1:9992'
const USER = process.env.YZH_USER || 'admin'
const PASS = process.env.YZH_PASS || '123456'

async function call(method, path, { token, body, query } = {}) {
  let url = BASE + path
  if (query) {
    const qs = new URLSearchParams(
      Object.entries(query).filter(([, v]) => v !== undefined && v !== null && v !== '')
    ).toString()
    if (qs) url += '?' + qs
  }
  const res = await fetch(url, {
    method,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: 'Bearer ' + token } : {})
    },
    body: body === undefined ? undefined : JSON.stringify(body)
  })
  const text = await res.text()
  try {
    return JSON.parse(text)
  } catch {
    return { __http: res.status, __raw: text.slice(0, 300) }
  }
}

let pass = 0
let fail = 0
function check(name, ok, detail = '') {
  if (ok) {
    pass++
    console.log(`  ✓ ${name}`)
  } else {
    fail++
    console.log(`  ✗ ${name}${detail ? '  → ' + detail : ''}`)
  }
}

// ---------- 0. 登录（⚠️ 是 /api/User/login，不是 /api/Auth/login） ----------
const login = await call('POST', '/api/User/login', {
  body: { UserName: USER, Password: PASS, Captcha: '', Uuid: '' }
})
check(`登录 ${USER}`, login.success === true, JSON.stringify(login.err))
if (!login.success) {
  console.log('\n登录失败，后续无法进行。')
  process.exit(1)
}
const token = login.data.Token

// ---------- 1. 标准列表 ----------
const std = await call('GET', '/api/PromptTemplate/workbench/standards', { token })
check('取标准下拉', std.success === true && Array.isArray(std.data) && std.data.length > 0)
console.log(
  '    标准：',
  (std.data || []).map((s) => `${s.standardName}(${s.standardCode})`).join(' | ') || '(空)'
)

const target =
  (std.data || []).find((s) => String(s.standardCode).includes('9001')) || (std.data || [])[0]
if (!target) {
  console.log('\n没有可用标准，无法继续。')
  process.exit(1)
}
console.log(`    目标标准：${target.standardName}  standardCode=${target.standardCode}  code=${target.code}`)

// ---------- 2. 已有行 ----------
const gList = await call('GET', '/api/PromptTemplate/workbench/list', {
  token,
  query: { promptType: 'doc_group' }
})
const cList = await call('GET', '/api/PromptTemplate/workbench/list', {
  token,
  query: { promptType: 'doc_content' }
})
check('分类提示词列表可取', gList.success === true)
check('作用提示词列表可取', cList.success === true)

const gRow = (gList.data || []).find((r) => r.standardCode === target.code)
const cRow = (cList.data || []).find((r) => r.standardCode === target.code)
check('该标准已有「分类提示词」行', !!gRow)
check('该标准已有「作用提示词」行', !!cRow)

if (!gRow) {
  console.log('\n该标准没有「分类提示词」行 —— 跳过写操作与读回断言（不是失败）。')
  console.log(`\n结果：${pass} 通过 / ${fail} 失败`)
  process.exit(fail === 0 ? 0 : 1)
}

// ---------- 3. resolve：切走前 ----------
const A1 = await call('GET', '/api/PromptTemplate/workbench/resolve', {
  token,
  query: { promptType: 'doc_group', standardCode: target.code }
})
check('resolve(分类) 命中', A1.success === true && !!A1.data)
check(
  'resolve(分类) 返回的就是本标准那行',
  A1.data?.promptCode === gRow.promptCode,
  `got=${A1.data?.promptCode} want=${gRow.promptCode}`
)
const lenA = (A1.data?.template || '').length
check('resolve(分类) 正文非空', lenA > 0, `len=${lenA}`)
console.log(`    分类正文长度 = ${lenA}`)

// ---------- 4. 幂等重存（模拟「AI 生成后自动落库」） ----------
const saved = await call('POST', '/api/PromptTemplate/workbench/save', {
  token,
  body: {
    PromptCode: A1.data.promptCode,
    PromptName: A1.data.promptName,
    PromptType: 'doc_group',
    StandardCode: target.code,
    SkillTarget: A1.data.skillTarget ?? null,
    Template: A1.data.template,
    Description: A1.data.description ?? null
  }
})
check('save 幂等重存成功（模拟自动落库）', saved.success === true, JSON.stringify(saved.err))

// ---------- 5. resolve：切回后必须一模一样 ----------
const A2 = await call('GET', '/api/PromptTemplate/workbench/resolve', {
  token,
  query: { promptType: 'doc_group', standardCode: target.code }
})
check(
  '★ 保存后读回：正文逐字节一致',
  (A2.data?.template || '') === (A1.data?.template || ''),
  `len ${(A2.data?.template || '').length} vs ${lenA}`
)
check('★ 保存后读回：promptCode 未漂移', A2.data?.promptCode === A1.data?.promptCode)

// ---------- 6. 切类型：内容必须换成另一条 ----------
const B = await call('GET', '/api/PromptTemplate/workbench/resolve', {
  token,
  query: { promptType: 'doc_content', standardCode: target.code }
})
check('resolve(作用) 命中', B.success === true && !!B.data)
check(
  '★ 切类型后拿到的是「作用提示词」而不是「分类提示词」',
  B.data?.promptType === 'doc_content' && B.data?.promptCode !== A1.data?.promptCode,
  `type=${B.data?.promptType} code=${B.data?.promptCode}`
)
console.log(`    作用正文长度 = ${(B.data?.template || '').length}`)

// ---------- 7. 再切回分类：内容仍在（用户报的 bug 的验证点） ----------
const A3 = await call('GET', '/api/PromptTemplate/workbench/resolve', {
  token,
  query: { promptType: 'doc_group', standardCode: target.code }
})
check('★★ 切走再切回，分类正文仍在且一致', (A3.data?.template || '') === (A1.data?.template || ''))
check('★★ 切走再切回，promptCode 仍是原来那条', A3.data?.promptCode === A1.data?.promptCode)

// ---------- 8. 平台级与标准级必须是两条不同的行 ----------
const P = await call('GET', '/api/PromptTemplate/workbench/resolve', {
  token,
  query: { promptType: 'doc_group' }
})
console.log(
  `    平台级分类：success=${P.success} code=${P.data?.promptCode ?? '(无)'} len=${(P.data?.template || '').length}`
)
check('平台级与标准级是两条不同的行（或平台级本就没有）', !P.data || P.data.promptCode !== A1.data?.promptCode)

console.log(`\n结果：${pass} 通过 / ${fail} 失败`)
process.exit(fail === 0 ? 0 : 1)
