/* 交互冒烟测试 —— node smoke.js （最小 DOM 桩，验证全链路无异常） */
const fs = require('fs'), vm = require('vm'), path = require('path');
const D = __dirname;

/* ★ 严格 DOM 桩：只返回 index.html 里真实存在的 id。
 *   之前的宽松桩（不存在就造一个）掩盖了「render() 漏调 renderTree」这类 bug。
 *   现在 getElementById 找不到就返回 null，让 JS 原生抛错 —— 与浏览器一致。 */
const html = fs.readFileSync(path.join(D, 'index.html'), 'utf8');
const REAL_IDS = new Set([...html.matchAll(/id="([^"]+)"/g)].map(m => m[1]));
/* 运行期动态创建的 id（DOMContentLoaded 里 insertBefore 注入） */
const DYNAMIC_IDS = new Set(['queueInline']);

/* 支持 querySelector('#id')：在「本元素 innerHTML 里出现过的 id」中查找。
   记录每个元素的 innerHTML 里声明过的 id，供 querySelector 命中。 */
const innerIds = new WeakMap();
const markIds = htmlStr => {
  const ids = [...String(htmlStr).matchAll(/id="([^"]+)"/g)].map(m => m[1]);
  return ids;
};
const el = () => {
  const e = {
    innerHTML: '', textContent: '', value: '', style: {}, disabled: false, children: [],
    classList: { toggle() {}, add() {}, remove() {}, contains: () => false },
    appendChild() {}, remove() {}, focus() {}, setAttribute() {}, getAttribute: () => null,
    insertBefore() {}, contains: () => false, parentNode: null,
    querySelector(sel) {
      const m = /#([\w-]+)/.exec(sel || '');
      if (!m) return null;
      return innerIds.get(e).has(m[1]) ? (store[m[1]] || (store[m[1]] = el())) : null;
    },
    querySelectorAll: () => []
  };
  innerIds.set(e, new Set());
  let _html = '';
  Object.defineProperty(e, 'innerHTML', {
    get: () => _html,
    set(v) { _html = String(v); innerIds.set(e, new Set(markIds(v))); }
  });
  return e;
};
const store = {};
const missing = new Set();
const document = {
  getElementById: id => {
    if (REAL_IDS.has(id) || DYNAMIC_IDS.has(id)) return (store[id] || (store[id] = el()));
    missing.add(id);
    return null;                                  /* ← 严格：与浏览器一致 */
  },
  createElement: () => { const e = el(); Object.defineProperty(e, 'id', { value: '', writable: true }); return e; },
  querySelector: () => null, querySelectorAll: () => [],
  addEventListener: (e, f) => { if (e === 'DOMContentLoaded') ctx.__boot = f; },
  body: el()
};
const timers = [];
const ctx = {
  document, console, Math, Date, JSON, Object, Array, String, Number, isNaN, parseInt, parseFloat,
  setTimeout: (f, t) => { timers.push(f); return timers.length; },
  clearTimeout() {}, confirm: () => true, alert: () => {},
  location: { reload() {} }, CSS: { escape: s => s }, __boot: null
};
vm.createContext(ctx);
vm.runInContext(fs.readFileSync(path.join(D, 'data.js'), 'utf8'), ctx);
vm.runInContext(fs.readFileSync(path.join(D, 'app.js'), 'utf8'), ctx);

/* 在上下文内断言用的取数桥 */
const probe = expr => vm.runInContext('JSON.stringify(' + expr + ')', ctx);

const steps = [
  ['启动 DOMContentLoaded', () => { if (!ctx.__boot) throw new Error('未注册'); ctx.__boot(); }],
  ['初始渲染（文件夹页面）', () => vm.runInContext('render()', ctx)],
  ['★ 左树只到文件夹（不含文件）', () => {
    const t = vm.runInContext('document.getElementById("tree").innerHTML', ctx);
    if (/风险管理报告/.test(t)) throw new Error('左树不该出现文件名');
    if (!/0 基础资料/.test(t)) throw new Error('左树应含文件夹');
  }],
  ['选中阶段', () => vm.runInContext("pickStage('S_RE')", ctx)],
  ['选中标准', () => vm.runInContext("pickStd('ISO9001')", ctx)],
  ['选中文件夹', () => vm.runInContext("pickFolder('F0')", ctx)],
  ['★ 文件夹页面显示文件', () => vm.runInContext('render()', ctx)],
  ['★ 进文件页面', () => vm.runInContext("gotoFile('D_IDCARD')", ctx)],
  ['固定文档 · 预览 tab', () => vm.runInContext('S.fdTab=0;render()', ctx)],
  ['固定文档 · 原件识别 tab', () => vm.runInContext('S.fdTab=1;render()', ctx)],
  ['★ 选定候选（自动锁定）', () => vm.runInContext("pick('D_IDCARD','OF010')", ctx)],
  ['取消选定', () => vm.runInContext("unpick('D_IDCARD')", ctx)],
  ['★ 标记企业暂无', () => vm.runInContext("markNone('D_PRODLIC')", ctx)],
  ['可编辑 · 进文件页', () => vm.runInContext("gotoFile('D_RISK')", ctx)],
  ['可编辑 · 预览 tab', () => vm.runInContext('S.fdTab=0;render()', ctx)],
  ['可编辑 · 审核 tab', () => vm.runInContext('S.fdTab=1;render()', ctx)],
  ['★ 改值（暂存）', () => vm.runInContext("editCell('D_RISK','表2 行4 列2','高','manual');render()", ctx)],
  ['★ 单个顶住', () => vm.runInContext("togglePin('D_RISK','{{RISK_POLICY}}',true)", ctx)],
  ['★ 批量顶住（认可推断）', () => vm.runInContext("bulkPin('D_RISK',true)", ctx)],
  ['批量解除顶住', () => vm.runInContext("bulkPin('D_RISK',false)", ctx)],
  ['★ 从企业资料筛值', () => vm.runInContext("pickFromRaw('D_RISK','表2 行3 列2',{value:'2026年度风险清单.xlsx：高'});render()", ctx)],
  ['★ 查看来源', () => vm.runInContext("showSource('D_RISK','{{RISK_POLICY}}')", ctx)],
  ['★ 更新文档（提交）', () => vm.runInContext("updateSelf()", ctx)],
  ['★ 锁定文档', () => vm.runInContext("toggleLock('D_RISK',true)", ctx)],
  ['解锁', () => vm.runInContext("toggleLock('D_RISK',false)", ctx)],
  ['★ 切换同文件夹上/下一个文件', () => vm.runInContext('switchSibling(1)', ctx)],
  ['★ 查看文件依赖', () => vm.runInContext("showDeps('D_RISK')", ctx)],
  ['★ 企业资料更新 → 依赖失效传播', () => vm.runInContext("applyInvalidate('OF030');render()", ctx)],
  ['★ 已锁定文档不受影响', () => vm.runInContext("toggleLock('D_RISK',true);applyInvalidate('OF031');render()", ctx)],
  ['★ 查看依赖失效明细', () => vm.runInContext('openDepLog()', ctx)],
  ['回文件夹', () => vm.runInContext('backToFolder()', ctx)],
  ['勾选 + 重新生成', () => vm.runInContext("S.sel=['D_AUDITPLAN'];rewriteSel()", ctx)],
  ['跑完队列', () => { let g = 0; while (timers.length && g++ < 300) timers.shift()(); }],
  ['队列明细', () => vm.runInContext('openQueue()', ctx)],
  ['批量锁定勾选', () => vm.runInContext("S.sel=['D_TRAIN'];lockSel(true)", ctx)],
  ['批量解锁', () => vm.runInContext("S.sel=['D_TRAIN'];lockSel(false)", ctx)],
  ['导出勾选', () => vm.runInContext("S.sel=['D_RISK'];exportSel()", ctx)],
  ['筛选：锁定', () => vm.runInContext("S.filter='locked';render()", ctx)],
  ['筛选：待审', () => vm.runInContext("S.filter='review';render()", ctx)],
  ['筛选：依赖失效', () => vm.runInContext("S.filter='inval';render()", ctx)],
  ['筛选：全部', () => vm.runInContext("S.filter='all';render()", ctx)],
  ['切日志菜单', () => vm.runInContext("switchMenu('log')", ctx)],
  ['切资料菜单', () => vm.runInContext("switchMenu('resource')", ctx)],
  ['回主页面', () => vm.runInContext("switchMenu('normalize')", ctx)],
  ['全展开/折叠', () => vm.runInContext('expandAll(true);expandAll(false)', ctx)],
  ['★ 模拟资料更新（顶栏入口）', () => vm.runInContext('demoUpload()', ctx)],
  ['说明', () => vm.runInContext('showHelp()', ctx)]
];

let bad = 0;
steps.forEach(([n, f]) => { try { f(); } catch (e) { bad++; console.log('\u2717 ' + n + ' \u2192 ' + e.message); } });
console.log(bad ? `\n\u2717 ${bad}/${steps.length} 步失败` : `\n\u2605 全部 ${steps.length} 步交互无异常`);
/* ---- 运行期 id 合法性核对（严格桩记录了所有「HTML 中不存在却被 getElementById 访问」的 id）---- */
const staticallyOk = new Set([...fs.readFileSync(path.join(D, 'index.html'), 'utf8').matchAll(/id="([^"]+)"/g)].map(m => m[1]));
const illegal = [...missing].filter(id => !staticallyOk.has(id));

if (illegal.length) console.log('✗ 运行期访问了 HTML 中不存在的 id：' + illegal.join(', '));
else console.log('✓ 运行期 getElementById 的 ' + missing.size + ' 次调用全部命中真实节点（innerHTML 注入的元素走 querySelector）');
console.log('');
process.exit(bad || illegal.length ? 1 : 0);
