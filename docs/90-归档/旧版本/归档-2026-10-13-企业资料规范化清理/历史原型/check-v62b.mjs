import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const PROTO = path.join(HERE, '62-填写规则-简化原型-V2.html');
const src = fs.readFileSync(PROTO, 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1];

const STATIC_IDS = ['cnt','docSel','mBar','mKinds','mParams','mTitle','mWrite','mask','rows'];
const DYN_IDS = ['pParam','pHint','pIns','pHasParams','pParamBox','wType','ctxvars'];

function mkEl(id){
  return { id, _html:'', value:'', textContent:'', checked:false, dataset:{}, style:{},
    classList:{ _s:new Set(), add(c){this._s.add(c)}, remove(c){this._s.delete(c)}, contains(c){return this._s.has(c)} },
    get innerHTML(){return this._html;}, set innerHTML(v){this._html=String(v);},
    querySelectorAll(){return [];}, focus(){} };
}
const store = {};
[...STATIC_IDS, ...DYN_IDS].forEach(id => store[id] = mkEl(id));

const alerts = [];
const ctx = {
  document:{ getElementById:id=>store[id]||null, querySelectorAll:()=>[], addEventListener(){} },
  alert:(m)=>alerts.push(String(m)),
  console, setTimeout, clearTimeout, Math, Date, JSON, String, Number, Boolean,
  Array, Object, RegExp, Set, Map, Error, isNaN, parseInt, parseFloat
};
ctx.globalThis = ctx;
vm.createContext(ctx);

const api = vm.runInContext(
  '(function(){\n' + src + '\nreturn {ANCHORS, KINDS, PARAMS, CTX_VARS, KIND_LABEL, AI_KINDS,' +
  ' srcSummary, writeSummary, esc, render, openM, closeM, pickKind, renderKinds, renderParams,' +
  ' renderWrite, setW, renderParamList, editParam, addParam, delParam, saveM, getEditing:()=>editing};\n})()',
  ctx
);

let pass = 0, fail = 0;
function ok(name, cond, extra){
  if(cond){ pass++; }
  else { fail++; console.log('  FAIL: ' + name + (extra!==undefined?('  → '+JSON.stringify(extra)):'')); }
}
const cnt = (s, re) => (String(s).match(re)||[]).length;

/* ── T1 静态模型 ── */
ok('T1 KINDS 恰好 5 种', api.KINDS.length === 5, api.KINDS.length);
ok('T1b 键序正确',
  api.KINDS.map(k=>k.k).join(',') === 'global,semantic,ai_field,ai_table,manual',
  api.KINDS.map(k=>k.k));
ok('T1c ⛔ 无「企业资料画像」', !api.KINDS.some(k => /画像/.test(k.n) || k.k==='profile'));
ok('T1d AI 三件套齐', api.AI_KINDS.join(',') === 'semantic,ai_field,ai_table', api.AI_KINDS);
ok('T2 PARAMS 13 项', api.PARAMS.length === 13, api.PARAMS.length);
ok('T2b 上下文变量 10 项', api.CTX_VARS.length === 10, api.CTX_VARS.length);
ok('T3 ANCHORS 5 行（实测）', api.ANCHORS.length === 5, api.ANCHORS.length);

/* ── T4 来源属性摘要 ── */
const A = api.ANCHORS;
ok('T4a global→参数名', api.srcSummary(A[0]) === '参数：企业名称', api.srcSummary(A[0]));
ok('T4b manual→提示', api.srcSummary(A[1]).startsWith('提示：'), api.srcSummary(A[1]));
ok('T4c ai_table→提示词', api.srcSummary(A[3]).startsWith('提取企业近一年'), api.srcSummary(A[3]));
ok('T4d 未配置→—', api.srcSummary(A[4]) === '—', api.srcSummary(A[4]));

/* ── T5 填写规则摘要（★ 本版核心：覆盖/填充 + 行列）── */
ok('T5a 标量 覆盖·text', api.writeSummary(A[0]) === '覆盖 · text', api.writeSummary(A[0]));
ok('T5b 标量 带格式与必填',
  api.writeSummary(A[2]) === '覆盖 · date · yyyy年MM月dd日 · 必填', api.writeSummary(A[2]));
ok('T5c 表格 填充·行列·自动扩展',
  api.writeSummary(A[3]) === '填充 · 11行 A-F列 · 自动扩展', api.writeSummary(A[3]));
ok('T5d 未配置→—', api.writeSummary(A[4]) === '—', api.writeSummary(A[4]));

/* ── T6 esc ── */
ok('T6 esc 转义', api.esc('<a "b" & c>') === '&lt;a &quot;b&quot; &amp; c&gt;', api.esc('<a "b" & c>'));
ok('T6b esc null 安全', api.esc(null) === '', api.esc(null));

/* ── T7 列表渲染 ── */
api.render();
ok('T7 渲染 5 行', cnt(store.rows._html, /<tr /g) === 5, cnt(store.rows._html, /<tr /g));
ok('T7b 仅 1 行未配置', cnt(store.rows._html, /未配置/g) === 1, cnt(store.rows._html, /未配置/g));
ok('T7c 计数文案（1/2/3/4 已配，5 未配）',
  store.cnt.textContent === '已配 4 / 共 5', store.cnt.textContent);
ok('T7d 孤儿锚点有标注', store.rows._html.includes('模板里已不存在'));

/* ── T8 三段结构（★ 用户要求：①②不合并）── */
api.openM(1);
ok('T8 定位条含 ENT_NAME', store.mBar._html.includes('ENT_NAME'));
ok('T8b 5 张来源卡片', cnt(store.mKinds._html, /class="kc/g) === 5, cnt(store.mKinds._html, /class="kc/g));
ok('T8c 恰 1 个选中', cnt(store.mKinds._html, /kc on/g) === 1, cnt(store.mKinds._html, /kc on/g));
ok('T8d ②来源属性 = 参数下拉', store.mParams._html.includes('id="pParam"'));
ok('T8e ②不含填写规则字段', !store.mParams._html.includes('值类型'), store.mParams._html.slice(0,80));
ok('T8f ③填写规则独立渲染', store.mWrite._html.includes('name="wmode"'));
ok('T8g ③含覆盖/填充两项', cnt(store.mWrite._html, /name="wmode"/g) === 2, cnt(store.mWrite._html, /name="wmode"/g));
ok('T8h 弹层已开', store.mask.classList.contains('on'));

/* ── T9 日期锚点带出已有属性 ── */
api.closeM();
api.openM(3);
ok('T9 值类型=date', store.mWrite._html.includes('value="date" selected'), store.mWrite._html.slice(0,200));
ok('T9b 格式带出', store.mWrite._html.includes('yyyy年MM月dd日'));
ok('T9c 必填带出', cnt(store.mWrite._html, /checked/g) >= 1);
ok('T9d 标量无行列字段', !store.mWrite._html.includes('起始行'));

/* ── T10 表格锚点 ── */
api.closeM();
api.openM(4);
ok('T10 表格有起始行', store.mWrite._html.includes('起始行'));
ok('T10b 表格有起始列', store.mWrite._html.includes('起始列'));
ok('T10c 表格有结束列', store.mWrite._html.includes('结束列'));
ok('T10d 表格有行数选项', store.mWrite._html.includes('name="wrows"'));
ok('T10e 自动扩展已选', store.mWrite._html.includes('value="auto" checked'));
ok('T10f 起始行带出 11', store.mWrite._html.includes('value="11"'), store.mWrite._html.slice(0,300));
ok('T10g 起始列带出 A', store.mWrite._html.includes('value="A"'));
ok('T10h 结束列带出 F', store.mWrite._html.includes('value="F"'));
ok('T10i 填充已选', store.mWrite._html.includes('value="fill" checked'));
ok('T10j 标量无值类型字段', !store.mWrite._html.includes('值类型'));

/* ── T11 ②来源属性：AI 有无参数 ── */
ok('T11 AI 有参数开关', store.mParams._html.includes('id="pHasParams"'));
/* ⚠️ 参数表写在 #pParamBox 里（桩不做嵌套 innerHTML 合成）⇒ 断言必须打在 pParamBox 上 */
ok('T11b 已带 1 个参数', store.pParamBox._html.includes('value="stage"'), store.pParamBox._html.slice(0,80));
ok('T11c 参数取值带出', store.pParamBox._html.includes('{{阶段名称}}'));
ok('T11d 有上下文变量候选', store.pParamBox._html.includes('id="ctxvars"'));

/* ── T12 参数增删改 ── */
api.addParam();
ok('T12 加参数后 2 行', api.getEditing().params.length === 2, api.getEditing().params.length);
api.editParam(1, 'name', 'product');
api.editParam(1, 'value', '{{认证范围}}');
ok('T12b 改参数生效',
  api.getEditing().params[1].name === 'product' && api.getEditing().params[1].value === '{{认证范围}}',
  api.getEditing().params[1]);
api.delParam(1);
ok('T12c 删参数后 1 行', api.getEditing().params.length === 1, api.getEditing().params.length);

/* ── T13 关掉「需要参数」清空列表 ── */
store.pHasParams.checked = false;
store.pHasParams.onchange({ target: store.pHasParams });
ok('T13 关开关清空参数', api.getEditing().params.length === 0, api.getEditing().params.length);
store.pHasParams.checked = true;
store.pHasParams.onchange({ target: store.pHasParams });
ok('T13b 再打开补 1 空参数', api.getEditing().params.length === 1);

/* ── T14 换来源类型：同类型保留、不同类型清空 ── */
api.pickKind('ai_table');                 // 与当前相同
ok('T14 同类型保留参数', api.getEditing().params.length === 1, api.getEditing().params.length);
api.pickKind('ai_field');                 // 不同类型
ok('T14b 换类型清空参数', api.getEditing().params.length === 0, api.getEditing().params.length);
ok('T14c 换类型清空来源属性', Object.keys(api.getEditing().p).length === 0, api.getEditing().p);
ok('T14d 换类型不动填写规则', api.getEditing().w.startRow === '11', api.getEditing().w.startRow);
ok('T14e 换类型后卡片重选', cnt(store.mKinds._html, /kc on/g) === 1);
ok('T14f 提示词表单切换', store.mParams._html.includes('这个格子要填什么'));

/* ── T15 校验 ── */
api.closeM();
api.openM(5);                       // 未配置锚点
let n0 = alerts.length;
api.saveM();
ok('T15 未选来源被拦', alerts.length === n0+1 && alerts[n0] === '请先选择来源', alerts.slice(n0));

api.pickKind('global');             // 选了全局参数但没选参数
n0 = alerts.length;
api.saveM();
ok('T15b 未选参数被拦', alerts[n0] === '请选择全局参数', alerts.slice(n0));

api.pickKind('ai_field');           // AI 字段没填提示词
n0 = alerts.length;
api.saveM();
ok('T15c 空提示词被拦', alerts[n0] === '请填写提示词', alerts.slice(n0));
ok('T15d 被拦后未落库', api.ANCHORS[4].src === '', api.ANCHORS[4].src);

/* ── T16 保存成功 ── */
api.getEditing().p.instruction = '测试提示词';
api.getEditing().params = [ {name:'stage', value:'{{阶段名称}}'} ];
n0 = alerts.length;
api.saveM();
ok('T16 落库成功', api.ANCHORS[4].src === 'ai_field', api.ANCHORS[4].src);
ok('T16b 参数落库', api.ANCHORS[4].params.length === 1);
ok('T16c 保存后关弹层', !store.mask.classList.contains('on'));
ok('T16d 保存后清编辑态', api.getEditing() === null);
ok('T16e 列表已刷新', store.rows._html.includes('测试提示词'));
ok('T16f 计数变 5（全部配完）', store.cnt.textContent === '已配 5 / 共 5', store.cnt.textContent);

/* ── T17 ③填写规则可改并落库 ── */
api.openM(5);
api.setW('mode', 'fill');
api.setW('valueType', 'number');
api.setW('format', '#,##0.00');
api.setW('required', true);
api.setW('fallback', '—');
api.saveM();
ok('T17 写入方式落库', api.ANCHORS[4].w.mode === 'fill', api.ANCHORS[4].w.mode);
ok('T17b 值类型落库', api.ANCHORS[4].w.valueType === 'number');
ok('T17c 格式落库', api.ANCHORS[4].w.format === '#,##0.00');
ok('T17d 必填落库', api.ANCHORS[4].w.required === true);
ok('T17e 兜底落库', api.ANCHORS[4].w.fallback === '—');
ok('T17f 摘要反映填充',
  api.writeSummary(api.ANCHORS[4]) === '填充 · number · #,##0.00 · 必填',
  api.writeSummary(api.ANCHORS[4]));

/* ── T18 表格行列可改 ── */
api.openM(4);
api.setW('startRow', '20');
api.setW('startCol', 'B');
api.setW('endCol', 'D');
api.setW('rows', 'fixed');
api.saveM();
ok('T18 起始行落库', api.ANCHORS[3].w.startRow === '20');
ok('T18b 列范围落库', api.ANCHORS[3].w.startCol === 'B' && api.ANCHORS[3].w.endCol === 'D');
ok('T18c 行数改固定', api.ANCHORS[3].w.rows === 'fixed');
ok('T18d 摘要反映',
  api.writeSummary(api.ANCHORS[3]) === '填充 · 20行 B-D列 · 固定 1 行',
  api.writeSummary(api.ANCHORS[3]));

console.log(fail ? ('FAILED: ' + fail + ' 失败 / 共 ' + (pass+fail)) : ('ALL PASS (' + pass + ' 条断言)'));
process.exit(fail ? 1 : 0);
