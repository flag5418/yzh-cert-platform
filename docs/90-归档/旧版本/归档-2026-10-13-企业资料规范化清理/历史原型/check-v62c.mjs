/*
 * V3 回归断言（无浏览器 —— 沙箱没有 chromium）
 * 跑法：node check-v62c.mjs
 *
 * V3 相对 V2 的四条改动，全部是用户 2026-10-09 的裁决：
 *   ① AI 节点 = 固定 3 项属性：参数(无/有) → 是否依赖企业资料(不依赖/依赖) → 提示词
 *   ② 覆盖/填充 改口径：覆盖=整格换掉；填充=只换格子里的 {{}}，其余文字保留
 *   ③ 一个锚点有且只有一个来源（写死单选）
 *   ④ 全局参数 = 三个来源的并集（企业基本信息 + 后台定义的全局参数 + 标准对应的参数）
 */
import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const PROTO = path.join(HERE, '62-填写规则-简化原型-V3.html');
const src = fs.readFileSync(PROTO, 'utf8').match(/<script>([\s\S]*?)<\/script>/)[1];

const STATIC_IDS = ['cnt','mBar','mKinds','mParams','mTitle','mWrite','mask','rows'];
const DYN_IDS = ['pParam','pHint','pIns','wType'];

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
  '(function(){\n' + src + '\nreturn {ANCHORS, KINDS, PARAM_GROUPS, ALL_PARAMS, KIND_LABEL, AI_KINDS,' +
  ' paramLabel, srcSummary, writeSummary, esc, render, openM, closeM, pickKind, renderKinds, renderParams,' +
  ' renderParamPick, aiNote, toggleAiParam, setP, renderWrite, setW, validate, saveM, getEditing:()=>editing};\n})()',
  ctx
);

let pass = 0, fail = 0;
function ok(name, cond, extra){
  if(cond){ pass++; }
  else { fail++; console.log('  FAIL: ' + name + (extra!==undefined?('  → '+JSON.stringify(extra)):'')); }
}
const cnt = (s, re) => (String(s).match(re)||[]).length;

/* ══════ T1 来源类型：5 种，⛔ 无「企业资料画像」 ══════ */
ok('T1 KINDS 恰好 5 种', api.KINDS.length === 5, api.KINDS.length);
ok('T1b 键序正确',
  api.KINDS.map(k=>k.k).join(',') === 'global,semantic,ai_field,ai_table,manual',
  api.KINDS.map(k=>k.k));
ok('T1c ⛔ 无「企业资料画像」', !api.KINDS.some(k => /画像/.test(k.n) || k.k==='profile'));
ok('T1d AI 三件套齐', api.AI_KINDS.join(',') === 'semantic,ai_field,ai_table', api.AI_KINDS);

/* ══════ T2 全局参数 = 三个来源的并集（裁决 #4）══════ */
const G = api.PARAM_GROUPS;
ok('T2 恰好 3 组', G.length === 3, G.length);
ok('T2b 组名与顺序 = 企业基本信息 / 后台定义的全局参数 / 标准对应的参数',
  G.map(g=>g.g).join('|') === '企业基本信息|后台定义的全局参数|标准对应的参数',
  G.map(g=>g.g));
ok('T2c 第1组出处 = 企业档案 cert_enterprise', G[0].from === 'cert_enterprise', G[0].from);
ok('T2d 第1组 11 项，且全是 ent_ 前缀（⛔ 不恢复那 9 条被软删的镜像参数行）',
  G[0].items.length === 11 && G[0].items.every(p=>p[0].startsWith('ent_')),
  G[0].items.map(p=>p[0]));
ok('T2e 第2组 7 项（实测可用）', G[1].items.length === 7, G[1].items.length);
ok('T2f 第2组 7 项全部库里已存在', G[1].items.every(p=>p[2]===1));
ok('T2g 第3组 5 项', G[2].items.length === 5, G[2].items.length);
ok('T2h 第3组只有 1 项库里已存在（last_audit_findings），其余 4 项需补',
  G[2].items.filter(p=>p[2]===1).length === 1
  && G[2].items.find(p=>p[2]===1)[0] === 'last_audit_findings',
  G[2].items.filter(p=>p[2]===1).map(p=>p[0]));
ok('T2i ALL_PARAMS = 23 项', api.ALL_PARAMS.length === 23, api.ALL_PARAMS.length);

/* ══════ T3 paramLabel ══════ */
ok('T3 企业档案参数名', api.paramLabel('ent_name') === '企业名称', api.paramLabel('ent_name'));
ok('T3b 后台参数名', api.paramLabel('doc_prefix') === '文件编号前缀', api.paramLabel('doc_prefix'));
ok('T3c 空 → 未选', api.paramLabel('') === '未选', api.paramLabel(''));
ok('T3d ★ 查不到 ⛔ 不回退空串（显式带出「库里没有」）',
  api.paramLabel('company_name') === 'company_name（库里没有）', api.paramLabel('company_name'));

/* ══════ T4 锚点 = 实测 5 行 ══════ */
const A = api.ANCHORS;
ok('T4 ANCHORS 5 行（实测 cert_doc_template_anchor）', A.length === 5, A.length);
ok('T4b 第1行 = 已发布模板的 {{文件控制程序}}',
  A[0].ref === '{{文件控制程序}}' && A[0].doc.includes('3bfc3563'), A[0].doc);
ok('T4c 第1行实测配的是 company_name（已软删）', A[0].p.param === 'company_name', A[0].p.param);
ok('T4d 第5行 = 表格锚点 Sheet1!A11:F11', A[4].ref === 'Sheet1!A11:F11' && A[4].type === 'table');
ok('T4e 已废弃 3 行（ENT_NAME / AUDIT_DATE / 表格）',
  A.filter(a=>a.deprecated).length === 3, A.filter(a=>a.deprecated).map(a=>a.ref));

/* ══════ T5 来源属性摘要（★ AI 节点 = 3 属性）══════ */
ok('T5 global → 参数名', api.srcSummary(A[0]) === '参数：company_name（库里没有）', api.srcSummary(A[0]));
ok('T5b 未配置 → —', api.srcSummary(A[1]) === '—', api.srcSummary(A[1]));
ok('T5c ★ AI 节点摘要 = 参数 · 是否依赖企业资料 · 提示词',
  api.srcSummary(A[4]).startsWith('参数：有（1） · 依赖企业资料 · 提示词：提取企业近一年的培训记录'),
  api.srcSummary(A[4]));

/* ══════ T6 填写规则摘要 ══════ */
ok('T6 标量 覆盖·text', api.writeSummary(A[0]) === '覆盖 · text', api.writeSummary(A[0]));
ok('T6b 表格 填充·行列·自动扩展',
  api.writeSummary(A[4]) === '填充 · 11行 A-F列 · 自动扩展', api.writeSummary(A[4]));
ok('T6c 未配置 → —', api.writeSummary(A[1]) === '—', api.writeSummary(A[1]));

/* ══════ T7 esc ══════ */
ok('T7 esc 转义', api.esc('<a "b" & c>') === '&lt;a &quot;b&quot; &amp; c&gt;', api.esc('<a "b" & c>'));
ok('T7b esc null 安全', api.esc(null) === '', api.esc(null));

/* ══════ T8 列表渲染 ══════ */
api.render();
ok('T8 渲染 5 行', cnt(store.rows._html, /<tr /g) === 5, cnt(store.rows._html, /<tr /g));
ok('T8b 3 行未配置', cnt(store.rows._html, /未配置/g) === 3, cnt(store.rows._html, /未配置/g));
ok('T8c 计数 = 已配 2 / 共 5', store.cnt.textContent === '已配 2 / 共 5', store.cnt.textContent);
ok('T8d 已废弃标注 3 处', cnt(store.rows._html, /模板里已废弃/g) === 3, cnt(store.rows._html, /模板里已废弃/g));
ok('T8e 孤儿标注 1 处', cnt(store.rows._html, /模板里已不存在/g) === 1);
ok('T8f ★ 第1行带出「参数已软删」告警', store.rows._html.includes('该参数行已被软删'));

/* ══════ T9 三段结构（裁决：①②⛔不合并）══════ */
api.openM(1);
ok('T9 定位条含锚点名', store.mBar._html.includes('文件控制程序'));
ok('T9b 5 张来源卡片', cnt(store.mKinds._html, /class="kc/g) === 5, cnt(store.mKinds._html, /class="kc/g));
ok('T9c ★ 恰 1 个选中（一个锚点一个来源，写死单选）',
  cnt(store.mKinds._html, /kc on/g) === 1, cnt(store.mKinds._html, /kc on/g));
ok('T9d ②来源属性 = 参数下拉', store.mParams._html.includes('id="pParam"'));
ok('T9e ②不含填写规则字段（①②未合并）', !store.mParams._html.includes('值类型'));
ok('T9f ③填写规则独立渲染', store.mWrite._html.includes('name="wmode"'));
ok('T9g 弹层已开', store.mask.classList.contains('on'));

/* ══════ T10 global 来源：参数下拉分 3 组 ══════ */
ok('T10 ★ 下拉 3 个 optgroup', cnt(store.mParams._html, /<optgroup/g) === 3, cnt(store.mParams._html, /<optgroup/g));
ok('T10b 组名带出', store.mParams._html.includes('企业基本信息')
  && store.mParams._html.includes('后台定义的全局参数')
  && store.mParams._html.includes('标准对应的参数'));
ok('T10c 未存在的参数标注「库里没有，需补」',
  cnt(store.mParams._html, /（库里没有，需补）/g) === 4, cnt(store.mParams._html, /（库里没有，需补）/g));

/* ══════ T11 ★ AI 节点 = 固定 3 项属性，顺序不可变 ══════ */
api.closeM();
api.openM(5);                       // ai_table
const P = store.mParams._html;
ok('T11 ★「参数」只有 2 个选项（无 / 有）', cnt(P, /name="aiP"/g) === 2, cnt(P, /name="aiP"/g));
ok('T11b ★「是否依赖企业资料」只有 2 个选项（不依赖 / 依赖）',
  cnt(P, /name="aiD"/g) === 2, cnt(P, /name="aiD"/g));
ok('T11c ★ 属性顺序 = 参数 → 是否依赖企业资料 → 提示词',
  P.indexOf('>参数</label>') > -1
  && P.indexOf('>参数</label>') < P.indexOf('>是否依赖企业资料</label>')
  && P.indexOf('>是否依赖企业资料</label>') < P.indexOf('>提示词</label>'),
  [P.indexOf('>参数</label>'), P.indexOf('>是否依赖企业资料</label>'), P.indexOf('>提示词</label>')]);
ok('T11d 参数=有 已选中', P.includes('name="aiP" value="1" checked'), P.match(/name="aiP"[^>]*/g));
ok('T11e 依赖企业资料=是 已选中', P.includes('name="aiD" value="1" checked'), P.match(/name="aiD"[^>]*/g));
ok('T11f 提示词框带出原文', P.includes('提取企业近一年的培训记录'));
ok('T11g ★ 参数=有 ⇒ 出现全局参数勾选面板', P.includes('class="ppick"'));
ok('T11h 勾选面板也是 3 组', cnt(P, /class="pgrp"/g) === 3, cnt(P, /class="pgrp"/g));
ok('T11i 已勾的 stage_name 带出 checked',
  P.includes("toggleAiParam('stage_name',this.checked)") && P.includes('checked\n            onchange="toggleAiParam(\'stage_name\''),
  P.match(/toggleAiParam\('stage_name'[^\n]*/g));
ok('T11j 提示词说明随「依赖」变', store.mParams._html.includes('模型拿这句话 + <b>企业资料</b>去分析'));

/* ══════ T12 参数勾选只改数据、⛔ 不重渲 ══════ */
ok('T12 初始 1 个参数', api.getEditing().p.params.length === 1, api.getEditing().p.params);
api.toggleAiParam('ent_name', true);
ok('T12b 勾上 → 2 个', api.getEditing().p.params.join(',') === 'stage_name,ent_name', api.getEditing().p.params);
api.toggleAiParam('stage_name', false);
ok('T12c 取消 → 1 个', api.getEditing().p.params.join(',') === 'ent_name', api.getEditing().p.params);
api.toggleAiParam('ent_name', false);
ok('T12d 再取消 → 0 个', api.getEditing().p.params.length === 0, api.getEditing().p.params);
api.toggleAiParam('ent_name', true);
ok('T12e 重复勾不会重复入列', api.getEditing().p.params.length === 1, api.getEditing().p.params);

/* ══════ T13 参数=无 ⇒ 勾选面板消失（②随①变）══════ */
api.setP('useParams', false);
ok('T13 关掉参数 ⇒ 面板消失', !store.mParams._html.includes('class="ppick"'));
ok('T13b 关掉参数 ⛔ 不清空已勾（切回来还在）', api.getEditing().p.params.length === 1);
api.setP('useParams', true);
ok('T13c 再打开 ⇒ 面板回来', store.mParams._html.includes('class="ppick"'));
ok('T13d 勾选状态保留', store.mParams._html.includes("toggleAiParam('ent_name',this.checked)")
  && /checked[\s\S]{0,40}toggleAiParam\('ent_name'/.test(store.mParams._html));

/* ══════ T14 ★ 覆盖 / 填充 改口径（裁决 #2）══════ */
const W = store.mWrite._html;
ok('T14 ★ 写入方式恰 2 项', cnt(W, /name="wmode"/g) === 2, cnt(W, /name="wmode"/g));
ok('T14b 覆盖 = 整个格子换成取值', W.includes('整个格子换成取值'), W.slice(0,200));
ok('T14c ★ 填充 = 只把格子里的 {{}} 换成取值，其余文字保留',
  W.includes('只把格子里的 {{}} 换成取值，其余文字保留'));
ok('T14d ★ 带出「一句话带 {{}}」的示例', W.includes('{{QUALITY_POLICY}}') && W.includes('全体员工须遵照执行'));
ok('T14e 示例里 覆盖 的结果是整句被冲掉', W.includes('以质量求生存') && W.includes('整句话被冲掉'));
ok('T14f 示例里 填充 的结果保留其余文字',
  W.includes('本公司的质量方针为：以质量求生存，全体员工须遵照执行。'));
ok('T14g 说明「需要测试，两个都留着」', W.includes('需要测试'));
ok('T14h 表格锚点带行列', W.includes('起始行') && W.includes('起始列') && W.includes('结束列'));
ok('T14i 表格带行数选项', W.includes('name="wrows"'));
ok('T14j 表格 自动扩展已选', W.includes('value="auto" checked'));

/* ══════ T15 换来源类型：同类型保留、不同类型清空 ══════ */
api.pickKind('ai_table');           // 与当前相同
ok('T15 同类型保留属性', api.getEditing().p.params.length === 1, api.getEditing().p.params);
api.pickKind('ai_field');           // 不同类型
ok('T15b ★ 换类型清空来源属性', api.getEditing().p.instruction === ''
  && api.getEditing().p.params.length === 0
  && api.getEditing().p.useParams === false
  && api.getEditing().p.needDocs === false,
  api.getEditing().p);
ok('T15c 换类型不动填写规则', api.getEditing().w.startRow === '11', api.getEditing().w.startRow);
ok('T15d 换类型后卡片重选', cnt(store.mKinds._html, /kc on/g) === 1);
ok('T15e 换类型后 ② 重渲成 AI 字段', store.mParams._html.includes('name="aiP"'));

/* ══════ T16 校验 ══════ */
api.closeM();
api.openM(3);                       // 未配置
let n0 = alerts.length;
api.saveM();
ok('T16 未选来源被拦', alerts[n0] === '请先选择来源', alerts.slice(n0));

api.pickKind('global');
n0 = alerts.length;
api.saveM();
ok('T16b 未选全局参数被拦', alerts[n0] === '请选择全局参数', alerts.slice(n0));

api.pickKind('ai_field');
n0 = alerts.length;
api.saveM();
ok('T16c AI 字段空提示词被拦', alerts[n0] === '请填写提示词', alerts.slice(n0));

api.pickKind('semantic');           // 参数=无 且 不依赖 且 无提示词 ⇒ 拦
n0 = alerts.length;
api.saveM();
ok('T16d ★ 参数=无 + 不依赖企业资料 + 无提示词 ⇒ 被拦（没有任何输入）',
  alerts[n0] === '参数=无 且 不依赖企业资料 ⇒ 提示词不能为空，否则没有任何输入', alerts.slice(n0));

api.getEditing().p.instruction = '按企业简称生成程序文件名称';
api.getEditing().p.useParams = true;
api.getEditing().p.params = ['ent_short_name'];
n0 = alerts.length;
api.saveM();
ok('T16e 参数=有 + 提示词 ⇒ 放行', alerts.length === n0, alerts.slice(n0));
ok('T16f 被拦过但最终落库', api.ANCHORS[2].src === 'semantic', api.ANCHORS[2].src);
ok('T16g 参数落库', api.ANCHORS[2].p.params.join(',') === 'ent_short_name', api.ANCHORS[2].p.params);
ok('T16h 保存后关弹层', !store.mask.classList.contains('on'));
ok('T16i 保存后清编辑态', api.getEditing() === null);
ok('T16j 计数变 3', store.cnt.textContent === '已配 3 / 共 5', store.cnt.textContent);

/* ══════ T17 ★ 不依赖企业资料 的形态（用户举的 xxx规则 / xxx内容）══════ */
api.openM(3);
ok('T17 参数=有 已选中', store.mParams._html.includes('name="aiP" value="1" checked'));
ok('T17b 不依赖企业资料 已选中', store.mParams._html.includes('name="aiD" value="0" checked'));
ok('T17c ★ 不依赖 ⇒ 提示词说明为「不读企业资料」',
  store.mParams._html.includes('模型<b>不读企业资料</b>，只用「全局参数 + 这句话」生成'));
ok('T17d 依赖选项的说明提到 xxx 规则 / xxx 内容',
  store.mParams._html.includes('如 xxx 规则 / xxx 内容'));

/* ══════ T18 ③填写规则可改并落库 ══════ */
api.setW('mode', 'fill');
api.setW('valueType', 'number');
api.setW('format', '#,##0.00');
api.setW('required', true);
api.setW('fallback', '—');
api.saveM();
ok('T18 写入方式落库', api.ANCHORS[2].w.mode === 'fill', api.ANCHORS[2].w.mode);
ok('T18b 值类型落库', api.ANCHORS[2].w.valueType === 'number');
ok('T18c 格式落库', api.ANCHORS[2].w.format === '#,##0.00');
ok('T18d 必填落库', api.ANCHORS[2].w.required === true);
ok('T18e 摘要反映填充',
  api.writeSummary(api.ANCHORS[2]) === '填充 · number · #,##0.00 · 必填',
  api.writeSummary(api.ANCHORS[2]));

/* ══════ T19 validate 纯函数可直接单测（⛔ 不依赖 DOM）══════ */
ok('T19 无来源', api.validate({ src:'', p:{} }) === '请先选择来源');
ok('T19b global 缺参数', api.validate({ src:'global', p:{} }) === '请选择全局参数');
ok('T19c global 有参数 ⇒ 过', api.validate({ src:'global', p:{ param:'ent_name' } }) === '');
ok('T19d ai_table 无提示词',
  api.validate({ src:'ai_table', p:{ useParams:false, params:[], needDocs:true } }) === '请填写提示词');
ok('T19e ai_table 靠参数无提示词 ⇒ 仍拦（表格必须说清提取什么）',
  api.validate({ src:'ai_table', p:{ useParams:true, params:['ent_name'], needDocs:true } }) === '请填写提示词');
ok('T19f semantic 参数=有 + 无提示词 ⇒ 过',
  api.validate({ src:'semantic', p:{ useParams:true, params:['ent_name'], needDocs:false } }) === '');
ok('T19g semantic 参数=无 + 不依赖 + 无提示词 ⇒ 拦',
  api.validate({ src:'semantic', p:{ useParams:false, params:[], needDocs:false } }) !== '');
ok('T19h manual 只要有来源就过', api.validate({ src:'manual', p:{} }) === '');

/* ══════ T20 表格锚点行列可改 ══════ */
api.closeM();
api.openM(5);
api.setW('startRow', '20');
api.setW('startCol', 'B');
api.setW('endCol', 'D');
api.setW('rows', 'fixed');
api.saveM();
ok('T20 起始行落库', api.ANCHORS[4].w.startRow === '20');
ok('T20b 列范围落库', api.ANCHORS[4].w.startCol === 'B' && api.ANCHORS[4].w.endCol === 'D');
ok('T20c 行数改固定', api.ANCHORS[4].w.rows === 'fixed');
ok('T20d 摘要反映',
  api.writeSummary(api.ANCHORS[4]) === '填充 · 20行 B-D列 · 固定 1 行',
  api.writeSummary(api.ANCHORS[4]));

console.log(fail ? ('FAILED: ' + fail + ' 失败 / 共 ' + (pass+fail)) : ('ALL PASS (' + pass + ' 条断言)'));
process.exit(fail ? 1 : 0);
