/* ══════════════════════════════════════════════════════════════
   后台端原型 V7 · 状态流转冒烟（无浏览器可跑）
   跑法：node smoke-v7.mjs
   ──────────────────────────────────────────────────────────────
   验证「一条条把缺项补上 ⇒ 状态自己变」这条链：
     C1 → 补模板+锚点 → 完成
     C5 → 配掉未配来源 → 消失
     C4 → 配全局填写规则 → 消失
     C3 → 填匹配依据 → 完成 ；人工覆盖成「标准自带」→ 变 C2
     C2 → 标准域补上文件 → 完成
     C6 → 固定文档硬塞锚点 ⇒ 只是提示，⛔ 不影响状态
   ══════════════════════════════════════════════════════════════ */
import fs from 'fs';

const html = fs.readFileSync(new URL('./41-文档填写规则页-原型-V7.html', import.meta.url), 'utf8');
const js = html.match(/<script>([\s\S]*?)<\/script>/)[1];

const mk = () => new Proxy(
  { innerHTML: '', textContent: '', value: '', className: '', style: {},
    classList: { add(){}, remove(){}, contains(){ return false } },
    querySelectorAll: () => [], querySelector: () => null,
    focus(){}, setSelectionRange(){}, closest: () => null },
  { get(t, k) { return (k in t) ? t[k] : undefined; }, set(t, k, v) { t[k] = v; return true; } });

const store = {};
global.document = { getElementById: id => (store[id] = store[id] || mk()),
  querySelectorAll: () => [], addEventListener: () => {} };
global.location = { search: '' };
global.setTimeout = () => 0; global.clearTimeout = () => {};
global.console = console;

const api = new Function(js + `
; return { issues, docState, missingItems, outcomeOf, FILES, state };`)();

const R = [];
const t = (n, c) => R.push((c ? '✓ ' : '✗ ') + n);
const clone = f => JSON.parse(JSON.stringify(f));
const pick  = code => clone(api.FILES.find(f => f.code === code));
const C     = (f, c) => api.issues(f).some(x => x.c === c);

/* 1) C1 → 补模板 + 锚点 → 完成 */
let f = pick('f1');
t('步骤 1  f1 初始：可编辑但没有空白模板 ⇒ C1', C(f, 'C1') && api.docState(f) === 'draft');
f.hasTpl = true; f.anchors = [{ ref: '{{ENT_NAME}}', ai: false, state: 'ok', required: true }];
t('步骤 2  上传空白模板并扫到锚点 ⇒ C1 消失', !C(f, 'C1'));
t('步骤 3  锚点全配、且无 ai 锚点 ⇒ 已完成设置', api.docState(f) === 'done');

/* 2) C5 → 配掉未配来源 */
f = pick('f2');
t('步骤 4  f2：有锚点未配数据源 ⇒ C5', C(f, 'C5'));
f.anchors.forEach(a => { if (a.state === 'nosrc') { a.state = 'ok'; a.src = '全局参数 ENT_NAME'; } });
t('步骤 5  配掉未配来源 ⇒ C5 消失', !C(f, 'C5'));

/* 3) C4 → 配全局填写规则 */
t('步骤 6  f2：有 ai 锚点但没配全局填写规则 ⇒ C4', C(f, 'C4'));
f.globalRule = true;
t('步骤 7  配了全局填写规则 ⇒ C4 消失', !C(f, 'C4'));
t('步骤 8  此时 f2 已完成设置', api.docState(f) === 'done');

/* 4) C3 → 填匹配依据；再人工覆盖成「标准自带」→ 变 C2 */
f = pick('f4');
t('步骤 9  f4：企业提供但没有匹配依据 ⇒ C3', C(f, 'C3'));
f.matchPrompt = '含「营业执照」且含企业名称的 PDF / 图片';
t('步骤 10 填了匹配依据 ⇒ C3 消失，已完成设置', !C(f, 'C3') && api.docState(f) === 'done');
f.subtype = 'standard_provided';
t('步骤 11 人工覆盖为「标准自带」⇒ 转为 C2（标准域里没这份文件）', C(f, 'C2') && !C(f, 'C3'));

/* 5) C2 → 标准域补上文件 */
f = pick('f8');
t('步骤 12 f8：标准自带但标准域无此文件 ⇒ C2', C(f, 'C2'));
f.stdHasFile = true;
t('步骤 13 标准域补上该文件 ⇒ C2 消失，已完成设置', !C(f, 'C2') && api.docState(f) === 'done');

/* 6) ★ 不阻断：矛盾组合照样算得出态 */
f = pick('f7');
f.anchors = [{ ref: '{{X}}', ai: false, state: 'ok' }];
t('步骤 14 固定文档硬塞锚点 ⇒ 只报 C6 提示，⛔ 不影响状态',
  C(f, 'C6') && api.issues(f).find(x => x.c === 'C6').k === 'warn' && api.docState(f) === 'done');

/* 7) 产物跟着类型/可替换性走 */
t('步骤 15 产物随「可替换性」自动变（企业提供 → 标准自带）',
  api.outcomeOf({ type: 'fixed', subtype: 'enterprise_provided' }).label === '企业上传的原件' &&
  api.outcomeOf({ type: 'fixed', subtype: 'standard_provided' }).label === '标准域那一份');

const pass = R.filter(x => x.startsWith('✓')).length;
console.log(R.join('\n'));
console.log('\n结果: ' + pass + '/' + R.length + ' 步通过');
process.exit(pass === R.length ? 0 : 1);
