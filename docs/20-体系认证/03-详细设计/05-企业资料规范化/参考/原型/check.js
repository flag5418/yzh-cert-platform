/* 原型自检脚本 —— node check.js */
const fs = require('fs'), vm = require('vm'), path = require('path');
const D = __dirname;
const ctx = {}; vm.createContext(ctx);
vm.runInContext(fs.readFileSync(path.join(D, 'data.js'), 'utf8') + ';globalThis.__M=MOCK;globalThis.__K=SRC_KIND;', ctx);
const M = ctx.__M, K = ctx.__K;
const app = fs.readFileSync(path.join(D, 'app.js'), 'utf8');
const html = fs.readFileSync(path.join(D, 'index.html'), 'utf8');
let fail = 0;
const ok = (c, m) => { console.log((c ? '✓ ' : '✗ ') + m); if (!c) fail++; };

/* 1. 树规模 */
let ns = 0, nfo = 0, nf = 0;
M.tree.E001.Stages.forEach(st => { ns++; st.Standards.forEach(sd => sd.Folders.forEach(f => { nfo++; nf += f.Files.length; })); });
console.log(`\n【树规模】2 企业 / ${ns} 阶段 / ${nfo} 文件夹 / ${nf} 文件\n`);

/* 2. 收集所有文件 */
const files = [];
Object.values(M.tree).forEach(e => e.Stages.forEach(st => st.Standards.forEach(sd => sd.Folders.forEach(f => f.Files.forEach(fl => files.push(fl))))));
ok(files.every(f => f.Cat === 'platform_generated' || M.fixedDocs[f.Code] || M.editDocs[f.Code]),
  `全部 ${files.length} 个文件都有状态定义`);

/* 3. fixed 文档有 candidates */
const fdocs = files.filter(f => f.Cat === 'fixed').map(f => f.Code);
ok(fdocs.every(c => M.candidates[c] !== undefined), `fixed 文档均有 candidates 定义：${fdocs.join(', ')}`);

/* 4. SRC_KIND 覆盖 */
const kinds = new Set();
Object.values(M.editDocs).forEach(d => (d.Cells || []).forEach(c => kinds.add(c.Kind)));
const badK = [...kinds].filter(k => !K[k]);
ok(badK.length === 0, `SRC_KIND 覆盖全部用到的来源：${[...kinds].join(', ')}`);

/* 5. picked 都能在候选池找到 */
const badP = Object.keys(M.fixedDocs).filter(k => {
  const p = M.fixedDocs[k];
  return p.Picked && !(M.candidates[k] || []).some(c => c.FileCode === p.Picked);
});
ok(badP.length === 0, '所有 Picked 均能在候选池中找到');

/* 6. rawCandidates 覆盖的字段 */
const fields = new Set();
Object.values(M.editDocs).forEach(d => (d.Cells || []).forEach(c => fields.add(c.Field)));
const covered = new Set([...app.matchAll(/^\s{2}([A-Z_]+):\[\[/gm)].map(m => m[1]));
const hasCand = [...fields].filter(f => covered.has(f));
console.log(`  锚点字段 ${fields.size} 个 · 有「从企业资料筛」候选的 ${hasCand.length} 个：${hasCand.join(', ')}`);

/* 7. app.js 语法 */
try { new Function(app); ok(true, 'app.js 语法通过'); } catch (e) { ok(false, 'app.js 语法：' + e.message); }

/* 8. id 对齐 */
const ids = new Set([...html.matchAll(/id="([^"]+)"/g)].map(m => m[1]));
const refs = new Set([...app.matchAll(/\$\('([^']+)'\)/g)].map(m => m[1]));
const dyn = new Set(['depBody','qCode','qBody','menu','tree','crumb','ctxBar','metrics','alertBox','folderView','tblCard','tblTitle','filterSel','chkAll','tbody','selInfo','queueInline','depBody','qCode','qBody','hBody','mTitle','mBody']);
const badId = [...refs].filter(r => !ids.has(r) && !dyn.has(r));
ok(badId.length === 0, `\$() 引用的 ${refs.size} 个 id 全部存在于 HTML` + (badId.length ? '（缺：' + badId.join(', ') + '）' : ''));

/* 9. drawer id 对齐 */
const dr = [...app.matchAll(/\$\('drawer([A-Za-z]+)'\)/g)].map(m => m[1]);
const drBad = [...new Set(dr)].filter(k => !ids.has('drawer' + k));
ok(drBad.length === 0, `抽屉容器 id 齐全（${[...new Set(dr)].join(', ')}）`);

/* 10. onclick 引用的函数都存在 */
const calls = new Set([...html.matchAll(/onclick="([a-zA-Z_][\w]*)\(/g)].map(m => m[1]));
const fns = new Set([...app.matchAll(/^function ([a-zA-Z_][\w]*)/gm)].map(m => m[1]));
const missFn = [...calls].filter(f => !fns.has(f));
ok(missFn.length === 0, `HTML 中 ${calls.size} 个 onclick 函数全部已定义` + (missFn.length ? '（缺：' + missFn.join(', ') + '）' : ''));

/* 11. 功能菜单条引用的函数都存在 */
const fbBlock = app.slice(app.indexOf('function renderFuncBar'), app.indexOf('function renderFootnote'));
const STYLE_WORDS = ['primary', 'warn', 'link', 'sm'];
const fbFns = [...new Set([...fbBlock.matchAll(/onclick="([a-zA-Z]\w*)\(/g)].map(m => m[1]))];
const missFb = fbFns.filter(f => !fns.has(f));
ok(missFb.length === 0, `功能菜单条 ${fbFns.length} 个函数全部已定义` + (missFb.length ? '（缺：' + missFb.join(', ') + '）' : ''));

/* 12. 死代码检测：函数名以字符串形式出现在 HTML onclick 或 app 内即可 */
const dead = [...fns].filter(f => {
  if (new RegExp(`onclick="[^"]*\\b${f}\\b`).test(html)) return false;
  const body = app.slice(app.indexOf('\nfunction ' + f) + 1);
  return !new RegExp(`['"\`]${f}['"\`]|\\b${f}\\s*\\(`).test(body.slice(body.indexOf('\n')));
});
if (dead.length) console.log(`  ℹ 未被直接调用的函数：${dead.join(', ')}`);
else ok(true, '无死代码');

console.log('\n' + (fail === 0 ? '★ 全部通过' : `✗ ${fail} 项失败`) + '\n');
process.exit(fail === 0 ? 0 : 1);