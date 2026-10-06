/* ══════════════════════════════════════════════════════════════
   后台端原型 V7 · 逻辑自检（无浏览器可跑）
   跑法：node check-v7.mjs
   ──────────────────────────────────────────────────────────────
   做法：从 HTML 里抠出 <script>，用 document 桩跑起来，
        再把内部函数导出，对「C1~C6 / 三态 / 产物三分支 / 可覆盖」逐条断言。
   ══════════════════════════════════════════════════════════════ */
import fs from 'fs';

const html = fs.readFileSync(new URL('./41-文档填写规则页-原型-V7.html', import.meta.url), 'utf8');
const m = html.match(/<script>([\s\S]*?)<\/script>/);
if (!m) { console.error('未找到 <script>'); process.exit(1); }
const js = m[1];

/* document / window 桩 —— 只为让脚本能跑，不校验 DOM */
const mk = () => new Proxy(
  { innerHTML: '', textContent: '', value: '', className: '', style: {},
    classList: { add(){}, remove(){}, contains(){ return false } },
    querySelectorAll: () => [], querySelector: () => null,
    focus(){}, setSelectionRange(){}, closest: () => null },
  { get(t, k) { return (k in t) ? t[k] : undefined; }, set(t, k, v) { t[k] = v; return true; } });

const store = {};
global.document = {
  getElementById: id => (store[id] = store[id] || mk()),
  querySelectorAll: () => [],
  addEventListener: () => {},
};
global.location = { search: '' };
global.setTimeout = () => 0;
global.clearTimeout = () => {};
global.console = console;

const fn = new Function(js + `
; return { issues, missingItems, advisoryItems, docState, canPublish, outcomeOf,
           suggestTypeByExt, requiredCount, isComplete, FILES, RULE, state, ANCHORS_TPL };`);
const api = fn();

const R = [];
const t = (name, cond) => R.push((cond ? '✓ ' : '✗ ') + name);
const by  = code => api.FILES.find(f => f.code === code);
const has = (code, c) => api.issues(by(code)).some(x => x.c === c);

/* ── 数据基线 ── */
t('FILES 共 8 份（含 2 份「标准自带」样本）', api.FILES.length === 8);
t('RULE 覆盖 10 个后台配置项（回链）', Object.keys(api.RULE).length === 10);

/* ── C1~C6 每条都有可见样本 ── */
t('C1 有样本：可编辑但无空白模板', has('f1', 'C1'));
t('C2 有样本：标准自带但标准域无此文件', has('f8', 'C2'));
t('C3 有样本：企业提供但无匹配依据', has('f4', 'C3'));
t('C4 有样本：有 ai 锚点但无全局填写规则', has('f2', 'C4'));
t('C5 有样本：锚点未配数据源', has('f2', 'C5'));

/* ── C6 是「提示」不是「必需」 ── */
const fixedWithAnchor = { ...by('f7'), anchors: [{ ref: '{{X}}', ai: false, state: 'ok' }] };
const c6 = api.issues(fixedWithAnchor).filter(x => x.c === 'C6');
t('C6 固定文档有锚点 ⇒ warn（不影响状态）', c6.length === 1 && c6[0].k === 'warn');

/* ── 三态是「算出来的」 ── */
t('「标准自带 + 标准域有文件」⇒ 已完成设置', api.docState(by('f7')) === 'done');
t('「标准自带 + 标准域无文件」⇒ 正在设置（C2）', api.docState(by('f8')) === 'draft');
t('未建档 ⇒ 未设置规则', api.docState(by('f3')) === 'none');
t('已完成 ⇔ 必需项为空', api.missingItems(by('f7')).length === 0 && api.isComplete(by('f7')));

/* ── 产物三分支 ── */
t('可编辑 ⇒ 产物 = 空白模板填完的结果', api.outcomeOf(by('f2')).label === '空白模板填完的结果');
t('固定 + 企业提供 ⇒ 产物 = 企业上传的原件', api.outcomeOf(by('f4')).label === '企业上传的原件');
t('固定 + 标准自带 ⇒ 产物 = 标准域那一份', api.outcomeOf(by('f7')).label === '标准域那一份');
t('固定 + 可替换性未定 ⇒ 产物待定', api.outcomeOf({ type: 'fixed', subtype: null }).label.startsWith('待定'));
t('类型未定 ⇒ 产物待定', api.outcomeOf({ type: null }).label.startsWith('待定'));

/* ── ★ P2'：程序不阻断 —— 任意组合都算得出态，⛔ 不抛异常 ── */
let noThrow = true;
try {
  api.FILES.forEach(f => { api.issues(f); api.docState(f); api.outcomeOf(f); });
  api.issues({ type: 'editable', anchors: [{ ai: true, state: 'nosrc' }] });
} catch (e) { noThrow = false; console.error(e.message); }
t('任意组合都不抛异常（⛔ 不阻断）', noThrow);

/* ── ★ P3：建议 ≠ 事实（类型按扩展名推导，可被覆盖） ── */
t('类型建议按扩展名推导', api.suggestTypeByExt('pdf') === 'fixed' && api.suggestTypeByExt('docx') === 'editable');

/* ── 发布 = 状态已完成（同一套判据，⛔ 不另立门槛） ── */
api.state.file = by('f7'); t('已完成 ⇒ 可发布', api.canPublish() === true);
api.state.file = by('f1'); t('未完成 ⇒ 不可发布', api.canPublish() === false);

const pass = R.filter(x => x.startsWith('✓')).length;
console.log(R.join('\n'));
console.log('\n结果: ' + pass + '/' + R.length + ' 通过');
process.exit(pass === R.length ? 0 : 1);
