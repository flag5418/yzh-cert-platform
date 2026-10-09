import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const PROTO = path.join(HERE, '62-填写规则-简化原型-V1.html');
const src = fs.readFileSync(PROTO, 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1];

const STATIC_IDS = ['docSel','fDefault','fFormat','fRequired','fValueType','mKinds','mLoc','mParams','mSave','mTitle','mask','rows'];
const DYN_IDS = ['pParam','pIns','pHint'];

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
  '(function(){\n' + src + '\nreturn {ANCHORS, KINDS, PARAMS, KIND_LABEL, summary, esc, render,' +
  ' openM, closeM, pickKind, renderKinds, renderParams, saveM, getEditing:()=>editing};\n})()',
  ctx
);

let pass = 0, fail = 0;
function ok(name, cond, extra){
  if(cond){ pass++; }
  else { fail++; console.log('  FAIL: ' + name + (extra!==undefined?('  → '+JSON.stringify(extra)):'')); }
}
const cnt = (s, re) => (String(s).match(re)||[]).length;

/* ── T1~T3 静态模型 ── */
ok('T1 KINDS 恰好 5 种', api.KINDS.length === 5, api.KINDS.length);
ok('T1b 5 种键正确',
  api.KINDS.map(k=>k.k).join(',') === 'global,semantic,ai_field,ai_table,manual',
  api.KINDS.map(k=>k.k));
ok('T1c ⛔ 无「企业资料画像」', !api.KINDS.some(k => /画像/.test(k.n) || k.k==='profile'));
ok('T2 PARAMS 13 项', api.PARAMS.length === 13, api.PARAMS.length);
ok('T2b PARAMS 含 company_name', api.PARAMS.some(p=>p[0]==='company_name'));
ok('T3 ANCHORS 5 行（实测）', api.ANCHORS.length === 5, api.ANCHORS.length);

/* ── T4 summary ── */
const A = api.ANCHORS;
ok('T4a global→参数名', api.summary(A[0]) === '参数：企业名称', api.summary(A[0]));
ok('T4b manual→提示', api.summary(A[1]).startsWith('提示：'), api.summary(A[1]));
ok('T4c global 日期参数', api.summary(A[2]) === '参数：审核日期', api.summary(A[2]));
ok('T4d ai_table→提示词', api.summary(A[3]).startsWith('提取企业近一年'), api.summary(A[3]));
ok('T4e 未配置→—', api.summary(A[4]) === '—', api.summary(A[4]));

/* ── T5 esc ── */
ok('T5 esc 转义', api.esc('<a "b" & c>') === '&lt;a &quot;b&quot; &amp; c&gt;', api.esc('<a "b" & c>'));
ok('T5b esc null 安全', api.esc(null) === '', api.esc(null));

/* ── T6 render ── */
api.render();
ok('T6 渲染 5 行', cnt(store.rows._html, /<tr /g) === 5, cnt(store.rows._html, /<tr /g));
ok('T6b 仅 1 行未配置', cnt(store.rows._html, /未配置/g) === 1, cnt(store.rows._html, /未配置/g));
ok('T6c 全局参数出现 2 次', cnt(store.rows._html, />全局参数</g) === 2, cnt(store.rows._html, />全局参数</g));
ok('T6d 含 ENT_NAME', store.rows._html.includes('{{ENT_NAME}}'));
ok('T6e 孤儿锚点有标注', store.rows._html.includes('模板里已不存在'));

/* ── T7 openM(1) ── */
api.openM(1);
ok('T7 标题', store.mTitle.textContent === '锚点规则 · {{ENT_NAME}}', store.mTitle.textContent);
ok('T7b 定位含 ENT_NAME', store.mLoc._html.includes('ENT_NAME'));
ok('T7c 5 个来源卡片', cnt(store.mKinds._html, /class="kc/g) === 5, cnt(store.mKinds._html, /class="kc/g));
ok('T7d 恰 1 个选中', cnt(store.mKinds._html, /kc on/g) === 1, cnt(store.mKinds._html, /kc on/g));
ok('T7e global 参数下拉出现', store.mParams._html.includes('id="pParam"'));
ok('T7f 弹层已开', store.mask.classList.contains('on'));

/* ── T8 同类型不重置 / 换类型重置 ── */
const before = api.getEditing().p.param;
api.pickKind('global');
ok('T8 同类型保留参数', api.getEditing().p.param === before, api.getEditing().p);
api.pickKind('ai_field');
ok('T8b 换类型清空参数', Object.keys(api.getEditing().p).length === 0, api.getEditing().p);
ok('T8c 换类型后表单切换', store.mParams._html.includes('这个格子要填什么'));
ok('T8d 换类型后卡片重选', cnt(store.mKinds._html, /kc on/g) === 1);

/* ── T9 校验：AI 字段必须填提示词 ── */
const nBefore = alerts.length;
api.saveM();
ok('T9 空提示词被拦', alerts.length === nBefore + 1, alerts.slice(-1));
ok('T9b 被拦后未落库', api.ANCHORS[0].src === 'global', api.ANCHORS[0].src);

/* ── T10 保存成功 ── */
api.getEditing().p.instruction = '测试提示词';
api.saveM();
ok('T10 落库成功', api.ANCHORS[0].src === 'ai_field', api.ANCHORS[0].src);
ok('T10b 保存后关弹层', !store.mask.classList.contains('on'));
ok('T10c 保存后清编辑态', api.getEditing() === null);
ok('T10d 列表已刷新', store.rows._html.includes('测试提示词'));

/* ── T11 global 缺参数被拦 ── */
api.openM(1);
api.pickKind('global');
api.saveM();
ok('T11 未选参数被拦', alerts[alerts.length-1] === '请选择全局参数', alerts.slice(-1));
ok('T11b 被拦后 src 未变', api.ANCHORS[0].src === 'ai_field', api.ANCHORS[0].src);
api.closeM();

/* ── T12 未配置锚点 ── */
api.openM(5);
ok('T12 未配置提示', store.mParams._html.includes('还没有选来源类型'));
api.closeM();
ok('T12b closeM 清编辑态', api.getEditing() === null);

/* ── T13 表格锚点 ── */
api.openM(4);
ok('T13 表格定位文案', store.mLoc._html.includes('表格区域'), store.mLoc._html.slice(0,120));
ok('T13b 表格提示词表单', store.mParams._html.includes('这张表要提取什么'));
api.closeM();

/* ── T14 日期锚点带出已有属性 ── */
api.openM(3);
ok('T14 值类型=date', store.fValueType.value === 'date', store.fValueType.value);
ok('T14b 格式带出', store.fFormat.value === 'yyyy年MM月dd日', store.fFormat.value);
ok('T14c 必填带出', store.fRequired.checked === true, store.fRequired.checked);

/* ── T15 写入属性保存 ── */
store.fValueType.value = 'number';
store.fFormat.value = '#,##0.00';
store.fRequired.checked = false;
store.fDefault.value = '—';
api.saveM();
ok('T15 值类型落库', api.ANCHORS[2].valueType === 'number', api.ANCHORS[2].valueType);
ok('T15b 格式落库', api.ANCHORS[2].fmt === '#,##0.00', api.ANCHORS[2].fmt);
ok('T15c 必填落库', api.ANCHORS[2].req === false, api.ANCHORS[2].req);
ok('T15d 兜底落库', api.ANCHORS[2].def === '—', api.ANCHORS[2].def);

/* ── T16 manual 表单 ── */
api.openM(2);
ok('T16 manual 提示语表单', store.mParams._html.includes('id="pHint"'));
ok('T16b manual 已带出提示', store.mParams._html.includes('请填写贵公司对应的程序文件名称'));
api.closeM();

/* ── T17 语义生成表单 ── */
api.openM(1);
api.pickKind('semantic');
ok('T17 semantic 表单', store.mParams._html.includes('改写要求'));
ok('T17b semantic 不要求必填', !store.mParams._html.includes('class="req"'));
api.closeM();

console.log(fail ? ('FAILED: ' + fail + ' 失败 / 共 ' + (pass+fail)) : ('ALL PASS (' + pass + ' 条断言)'));
process.exit(fail ? 1 : 0);
