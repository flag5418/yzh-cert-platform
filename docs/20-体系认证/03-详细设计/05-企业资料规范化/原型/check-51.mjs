/* ══════════════════════════════════════════════════════════════
   标准文档标准化 · 原型 V1 · 逻辑自检（无浏览器可跑）
   跑法：node check-51.mjs
   ══════════════════════════════════════════════════════════════ */
import fs from 'fs';

const html = fs.readFileSync(new URL('./51-标准文档标准化-原型-V1.html', import.meta.url), 'utf8');
const m = html.match(/<script>([\s\S]*?)<\/script>/);
if (!m) { console.error('未找到 <script>'); process.exit(1); }
const js = m[1];

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
; return { selfTest, statusOf, missingOf, hasAi, aiAnchors, DOCS, SOURCES, OPS, S, autoValue, HELP, renderHelp, toggleHelp, closeHelp };`)();

/* 先跑原型内置的 selfTest，再补几条结构断言 */
const R = api.selfTest();
const t = (n, c) => R.push((c ? '✓ ' : '✗ ') + n);
const pick = c => api.DOCS.find(d => d.code === c);

t('★ 结构：右侧只有 2 个 Tab（锚点规则 / 全局规则）',
  api.S.tab === 'anchor' || api.S.tab === 'global');
t('★ 打开文档默认落「全局规则」（分组/作用由原始文件语义分析带出，第一次就有）',
  api.S.tab === 'global');
t('★ 结构：没有九宫格功能菜单（无 menuForm / fnav 状态）',
  api.S.menuForm === undefined && api.S.nav === undefined);
t('★ 结构：预览三视图（原始 / 空白模板 / 填充后）',
  api.S.view === 'origin' || api.S.view === 'template' || api.S.view === 'filled');
t('★ 固定文档被锁（parseable=false 时 type 恒为 fixed）',
  api.DOCS.filter(d => !d.parseable).every(d => d.type === 'fixed'));

/* ★ 第二十一轮：注释收敛 —— 帮助收进 Tab 行右侧「!」，页面里 ⛔ 不印说明 */
t('★ 注释收敛：顶部说明条已删除（无 note / noteBody / noteToggle）',
  !/class="note"|noteBody|noteToggle/.test(html));
t('★ 注释收敛：帮助入口在 Tab 行（HELP 常量 + btnHelp 按钮）',
  !!api.HELP && !!api.HELP.anchor && !!api.HELP.global && /id="btnHelp"/.test(html));
t('★ 注释收敛：页面内无解释性 tip / hint / ※',
  !/class="tip"|class="hint"|※/.test(html));
t('★ 注释收敛：两个 Tab 各有帮助条目（锚点 5 条 / 全局 2 条）',
  api.HELP.anchor.length === 5 && api.HELP.global.length === 2);
t('★ 无重复 id：btnUpTpl 只出现 1 次',
  (html.match(/id="btnUpTpl"/g) || []).length === 1);

/* 状态：任意组合都不抛异常（⛔ 不阻断） */
let noThrow = true;
try {
  api.DOCS.forEach(d => { api.statusOf(d); api.missingOf(d); api.hasAi(d); api.aiAnchors(d); });
  api.statusOf({ touched: true, type: 'editable', anchors: [{ ref: '{{x}}' }], aiRules: [], tags: [], purpose: '' });
  api.statusOf({ touched: false });
} catch (e) { noThrow = false; console.error(e.message); }
t('任意组合都不抛异常（⛔ 不阻断）', noThrow);

const pass = R.filter(x => x.startsWith('✓')).length;
console.log(R.join('\n'));
console.log('\n结果: ' + pass + '/' + R.length + ' 通过');
process.exit(pass === R.length ? 0 : 1);
