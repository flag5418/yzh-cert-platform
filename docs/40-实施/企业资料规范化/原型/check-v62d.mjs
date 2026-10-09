/*
 * V4 回归断言（无浏览器 —— 沙箱没有 chromium）
 * 跑法：node check-v62d.mjs
 *
 * V4 相对 V3 的改动，全部来自用户 2026-10-09 第三轮指令：
 *   「1、参考 /enterprise-fill-params 的填写信息；2、清理垃圾数据」
 *   ⇒ 把「全局参数」从「我编的三组」改成**严格对齐引擎**：
 *       replace（只读：enterprise.* / org.* / system.*）+ global（可覆盖：cert_fill_param_def）
 *       —— 判据出处：ReplaceResolver 类注释
 */
import fs from 'node:fs';
import vm from 'node:vm';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const PROTO = path.join(HERE, '62-填写规则-简化原型-V4.html');
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
  ' findParam, paramLabel, paramCapLabel, srcSummary, writeSummary, esc, render, openM, closeM, pickKind,' +
  ' renderKinds, renderParams, renderParamPick, aiNote, toggleAiParam, setP, renderWrite, setW, validate, saveM,' +
  ' getEditing:()=>editing};\n})()',
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

/* ══════ T2 ★ 全局参数四组 —— 严格对齐引擎 IFillResolver ══════ */
const G = api.PARAM_GROUPS;
ok('T2 ★ 恰好 4 组', G.length === 4, G.length);
ok('T2b 组名与顺序 = 企业基本信息 / 机构·系统信息 / 后台定义的全局参数 / 标准对应的参数',
  G.map(g=>g.g).join('|') === '企业基本信息|机构 / 系统信息|后台定义的全局参数|标准对应的参数',
  G.map(g=>g.g));
ok('T2c ★ 第1、2 组 = replace 能力（只读）',
  G[0].cap === 'replace' && G[0].ro === true && G[1].cap === 'replace' && G[1].ro === true,
  [G[0].cap, G[0].ro, G[1].cap, G[1].ro]);
ok('T2d ★ 第3、4 组 = global 能力（可覆盖）',
  G[2].cap === 'global' && G[2].ro === false && G[3].cap === 'global' && G[3].ro === false,
  [G[2].cap, G[2].ro, G[3].cap, G[3].ro]);
ok('T2e ★ 第1组 16 项，全是 enterprise. 前缀',
  G[0].items.length === 16 && G[0].items.every(p=>p[0].startsWith('enterprise.')),
  G[0].items.map(p=>p[0]));
ok('T2f ★ 第1组字段与 ReplaceResolver.EnterpriseAttrLabels 一致',
  G[0].items.map(p=>p[0]).join(',') ===
  ['enterprise.Code','enterprise.Name','enterprise.ShortName','enterprise.CreditCode',
   'enterprise.LegalPerson','enterprise.Province','enterprise.City','enterprise.Address',
   'enterprise.IndustryType','enterprise.EmployeeCount','enterprise.CertScope',
   'enterprise.ContactName','enterprise.ContactPhone','enterprise.ContactEmail',
   'enterprise.EnterpriseNo','enterprise.ArchiveDate'].join(','),
  G[0].items.map(p=>p[0]));
ok('T2g ★ 第2组 12 项 = org.* 6 + system.* 6',
  G[1].items.length === 12
  && G[1].items.filter(p=>p[0].startsWith('org.')).length === 6
  && G[1].items.filter(p=>p[0].startsWith('system.')).length === 6,
  G[1].items.map(p=>p[0]));
ok('T2h ★ system.* 六项与 ReplaceResolver.FromSystem 一致',
  G[1].items.filter(p=>p[0].startsWith('system.')).map(p=>p[0]).join(',') ===
  'system.date,system.datetime,system.date_cn,system.year,system.month,system.day',
  G[1].items.filter(p=>p[0].startsWith('system.')).map(p=>p[0]));
ok('T2i ★ 第3组 8 条 = 清理后的真实参数（⛔ 无 3333）',
  G[2].items.length === 8 && !G[2].items.some(p=>p[0]==='3333'),
  G[2].items.map(p=>p[0]));
ok('T2j ★ 第3组与 /enterprise-fill-params 实测清单一致',
  G[2].items.map(p=>p[0]).join(',') ===
  'doc_prefix,qualifications,main_products,honors,quality_policy,quality_objective,company_profile,last_audit_findings',
  G[2].items.map(p=>p[0]));
ok('T2k ★ 第4组实测 0 条（StandardCode 非空的行一条都没有）',
  G[3].items.length === 0, G[3].items.length);
ok('T2l ALL_PARAMS = 36 项', api.ALL_PARAMS.length === 36, api.ALL_PARAMS.length);

/* ══════ T3 findParam / paramLabel / paramCapLabel ══════ */
ok('T3 企业属性可查', api.findParam('enterprise.Name')?.name === '企业全称', api.findParam('enterprise.Name'));
ok('T3b 机构属性可查', api.findParam('org.Name')?.name === '机构名称');
ok('T3c 系统变量可查', api.findParam('system.date')?.name === '制表日期 yyyy-MM-dd');
ok('T3d 后台参数可查', api.findParam('doc_prefix')?.name === '文件编号前缀');
ok('T3e 未知参数返回 null', api.findParam('company_name') === null);
ok('T3f paramLabel 空 → 未选', api.paramLabel('') === '未选');
ok('T3g paramLabel 企业属性', api.paramLabel('enterprise.Name') === '企业全称');
ok('T3h ★ paramLabel 查不到 ⛔ 不回退空串',
  api.paramLabel('company_name') === 'company_name（库里没有）', api.paramLabel('company_name'));
ok('T3i ★ paramCapLabel 只读', api.paramCapLabel('enterprise.Name') === '只读 · replace',
  api.paramCapLabel('enterprise.Name'));
ok('T3j ★ paramCapLabel 可覆盖', api.paramCapLabel('doc_prefix') === '可覆盖 · global',
  api.paramCapLabel('doc_prefix'));

/* ══════ T4 锚点 = 实测 5 行 ══════ */
const A = api.ANCHORS;
ok('T4 ANCHORS 5 行（实测 cert_doc_template_anchor）', A.length === 5, A.length);
ok('T4b 第1行 = 已发布模板的 {{文件控制程序}}',
  A[0].ref === '{{文件控制程序}}' && A[0].doc.includes('3bfc3563'), A[0].doc);
ok('T4c 第1行实测配的是 company_name（已软删）', A[0].p.param === 'company_name', A[0].p.param);
ok('T4d 第5行 = 表格锚点 Sheet1!A11:F11', A[4].ref === 'Sheet1!A11:F11' && A[4].type === 'table');
ok('T4e 已废弃 3 行', A.filter(a=>a.deprecated).length === 3, A.filter(a=>a.deprecated).map(a=>a.ref));

/* ══════ T5 来源属性摘要 ══════ */
ok('T5 ★ global 摘要带出「只读/可覆盖」',
  api.srcSummary(A[0]) === '参数：company_name（库里没有）', api.srcSummary(A[0]));
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

/* ══════ T9 三段结构 ══════ */
api.openM(1);
ok('T9 定位条含锚点名', store.mBar._html.includes('文件控制程序'));
ok('T9b 5 张来源卡片', cnt(store.mKinds._html, /class="kc/g) === 5, cnt(store.mKinds._html, /class="kc/g));
ok('T9c ★ 恰 1 个选中（一个锚点一个来源）',
  cnt(store.mKinds._html, /kc on/g) === 1, cnt(store.mKinds._html, /kc on/g));
ok('T9d ②来源属性 = 参数下拉', store.mParams._html.includes('id="pParam"'));
ok('T9e ②不含填写规则字段（①②未合并）', !store.mParams._html.includes('值类型'));
ok('T9f ③填写规则独立渲染', store.mWrite._html.includes('name="wmode"'));
ok('T9g 弹层已开', store.mask.classList.contains('on'));

/* ══════ T10 global 来源：下拉 4 组 + 只读/可覆盖 ══════ */
ok('T10 ★ 下拉 4 个 optgroup', cnt(store.mParams._html, /<optgroup/g) === 4, cnt(store.mParams._html, /<optgroup/g));
ok('T10b 组名带出', store.mParams._html.includes('企业基本信息')
  && store.mParams._html.includes('机构 / 系统信息')
  && store.mParams._html.includes('后台定义的全局参数')
  && store.mParams._html.includes('标准对应的参数'));
ok('T10c ★ optgroup 标出只读 / 可覆盖',
  cnt(store.mParams._html, /只读/g) >= 2 && cnt(store.mParams._html, /可覆盖/g) >= 2,
  [cnt(store.mParams._html, /只读/g), cnt(store.mParams._html, /可覆盖/g)]);
ok('T10d ★ 第4组空组给出说明', store.mParams._html.includes('这一组实测 0 条'));
ok('T10e 未选时说明列四组', store.mParams._html.includes('四组来源'));

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
ok('T11d 参数=有 已选中', P.includes('name="aiP" value="1" checked'));
ok('T11e 依赖企业资料=是 已选中', P.includes('name="aiD" value="1" checked'));
ok('T11f 提示词框带出原文', P.includes('提取企业近一年的培训记录'));
ok('T11g ★ 参数=有 ⇒ 出现全局参数勾选面板', P.includes('class="ppick"'));
ok('T11h ★ 勾选面板也是 4 组', cnt(P, /class="pgrp"/g) === 4, cnt(P, /class="pgrp"/g));
ok('T11i ★ 每组带只读/可覆盖徽标',
  cnt(P, /class="cap ro"/g) === 2 && cnt(P, /class="cap rw"/g) === 2,
  [cnt(P, /class="cap ro"/g), cnt(P, /class="cap rw"/g)]);
ok('T11j 空组给出说明', P.includes('这一组实测 0 条'));
ok('T11k 已勾的 enterprise.Name 带出 checked', P.includes("toggleAiParam('enterprise.Name'"));
ok('T11l 提示词说明随「依赖」变', P.includes('模型拿这句话 + <b>企业资料</b>去分析'));

/* ══════ T12 参数勾选只改数据、⛔ 不重渲 ══════ */
ok('T12 初始 1 个参数', api.getEditing().p.params.length === 1, api.getEditing().p.params);
api.toggleAiParam('doc_prefix', true);
ok('T12b 勾上 → 2 个',
  api.getEditing().p.params.join(',') === 'enterprise.Name,doc_prefix', api.getEditing().p.params);
api.toggleAiParam('doc_prefix', false);
ok('T12c 取消 → 1 个', api.getEditing().p.params.join(',') === 'enterprise.Name');
api.toggleAiParam('enterprise.Name', false);
ok('T12d 再取消 → 0 个', api.getEditing().p.params.length === 0);
api.toggleAiParam('enterprise.Name', true);
ok('T12e 重复勾不会重复入列', api.getEditing().p.params.length === 1);

/* ══════ T13 参数=无 ⇒ 勾选面板消失 ══════ */
api.setP('useParams', false);
ok('T13 关掉参数 ⇒ 面板消失', !store.mParams._html.includes('class="ppick"'));
ok('T13b 关掉参数 ⛔ 不清空已勾', api.getEditing().p.params.length === 1);
api.setP('useParams', true);
ok('T13c 再打开 ⇒ 面板回来', store.mParams._html.includes('class="ppick"'));
ok('T13d 勾选状态保留', store.mParams._html.includes("toggleAiParam('enterprise.Name',this.checked)"));

/* ══════ T14 覆盖 / 填充 ══════ */
const W = store.mWrite._html;
ok('T14 ★ 写入方式恰 2 项', cnt(W, /name="wmode"/g) === 2, cnt(W, /name="wmode"/g));
ok('T14b 覆盖 = 整个格子换成取值', W.includes('整个格子换成取值'));
ok('T14c ★ 填充 = 只把格子里的 {{}} 换成取值，其余文字保留',
  W.includes('只把格子里的 {{}} 换成取值，其余文字保留'));
ok('T14d ★ 带出「一句话带 {{}}」的示例', W.includes('{{QUALITY_POLICY}}') && W.includes('全体员工须遵照执行'));
ok('T14e 示例里 覆盖 的结果是整句被冲掉', W.includes('整句话被冲掉'));
ok('T14f 示例里 填充 的结果保留其余文字',
  W.includes('本公司的质量方针为：以质量求生存，全体员工须遵照执行。'));
ok('T14g 说明「需要测试，两个都留着」', W.includes('需要测试'));
ok('T14h 表格锚点带行列', W.includes('起始行') && W.includes('起始列') && W.includes('结束列'));
ok('T14i 表格带行数选项', W.includes('name="wrows"'));
ok('T14j 表格 自动扩展已选', W.includes('value="auto" checked'));

/* ══════ T15 换来源类型 ══════ */
api.pickKind('ai_table');           // 与当前相同
ok('T15 同类型保留属性', api.getEditing().p.params.length === 1);
api.pickKind('ai_field');           // 不同类型
ok('T15b ★ 换类型清空来源属性',
  api.getEditing().p.instruction === '' && api.getEditing().p.params.length === 0
  && api.getEditing().p.useParams === false && api.getEditing().p.needDocs === false,
  api.getEditing().p);
ok('T15c 换类型不动填写规则', api.getEditing().w.startRow === '11');
ok('T15d 换类型后卡片重选', cnt(store.mKinds._html, /kc on/g) === 1);

/* ══════ T16 校验 ══════ */
api.closeM();
api.openM(3);
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

api.pickKind('semantic');
n0 = alerts.length;
api.saveM();
ok('T16d ★ 参数=无 + 不依赖企业资料 + 无提示词 ⇒ 被拦',
  alerts[n0] === '参数=无 且 不依赖企业资料 ⇒ 提示词不能为空，否则没有任何输入', alerts.slice(n0));

api.getEditing().p.instruction = '按企业简称生成程序文件名称';
api.getEditing().p.useParams = true;
api.getEditing().p.params = ['enterprise.ShortName'];
n0 = alerts.length;
api.saveM();
ok('T16e 参数=有 + 提示词 ⇒ 放行', alerts.length === n0, alerts.slice(n0));
ok('T16f 落库成功', api.ANCHORS[2].src === 'semantic', api.ANCHORS[2].src);
ok('T16g 参数落库', api.ANCHORS[2].p.params.join(',') === 'enterprise.ShortName');
ok('T16h 保存后关弹层', !store.mask.classList.contains('on'));
ok('T16i 保存后清编辑态', api.getEditing() === null);
ok('T16j 计数变 3', store.cnt.textContent === '已配 3 / 共 5', store.cnt.textContent);

/* ══════ T17 不依赖企业资料 的形态 ══════ */
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
ok('T18 写入方式落库', api.ANCHORS[2].w.mode === 'fill');
ok('T18b 值类型落库', api.ANCHORS[2].w.valueType === 'number');
ok('T18c 格式落库', api.ANCHORS[2].w.format === '#,##0.00');
ok('T18d 必填落库', api.ANCHORS[2].w.required === true);
ok('T18e 摘要反映填充',
  api.writeSummary(api.ANCHORS[2]) === '填充 · number · #,##0.00 · 必填',
  api.writeSummary(api.ANCHORS[2]));

/* ══════ T19 validate 纯函数可直接单测 ══════ */
ok('T19 无来源', api.validate({ src:'', p:{} }) === '请先选择来源');
ok('T19b global 缺参数', api.validate({ src:'global', p:{} }) === '请选择全局参数');
ok('T19c global 有参数 ⇒ 过', api.validate({ src:'global', p:{ param:'enterprise.Name' } }) === '');
ok('T19d ai_table 无提示词',
  api.validate({ src:'ai_table', p:{ useParams:false, params:[], needDocs:true } }) === '请填写提示词');
ok('T19e ai_table 靠参数无提示词 ⇒ 仍拦',
  api.validate({ src:'ai_table', p:{ useParams:true, params:['enterprise.Name'], needDocs:true } }) === '请填写提示词');
ok('T19f semantic 参数=有 + 无提示词 ⇒ 过',
  api.validate({ src:'semantic', p:{ useParams:true, params:['enterprise.Name'], needDocs:false } }) === '');
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

/* ══════ T21 ★ global 选中后带出「只读 / 可覆盖」说明 ══════ */
api.closeM();
api.openM(1);
api.pickKind('global');
api.getEditing().p.param = 'enterprise.Name';
api.renderParams();
ok('T21 ★ 选只读参数 ⇒ 说明写明「企业在参数页改了也不生效」',
  store.mParams._html.includes('企业在参数页改了也不生效'), store.mParams._html.slice(0,300));
api.getEditing().p.param = 'doc_prefix';
api.renderParams();
ok('T21b ★ 选可覆盖参数 ⇒ 说明指向「企业资料参数」页',
  store.mParams._html.includes('企业资料参数'), store.mParams._html.slice(0,300));
ok('T21c 摘要带出只读标记', api.srcSummary(api.getEditing()) === '参数：文件编号前缀 · 可覆盖 · global',
  api.srcSummary(api.getEditing()));

/* ══════ T22 ★ 静态说明卡（读整份 HTML，⛔ 不只是 <script>）══════ */
const FULL = fs.readFileSync(PROTO, 'utf8');
ok('T22 标题为 V4', FULL.includes('<title>标准文档填写规则 · 简化原型 V4</title>'));
ok('T22b ★ 引擎 4 项能力表齐全',
  FULL.includes('AiGenerateResolver') && FULL.includes('HeaderFooterResolver')
  && FULL.includes('ReplaceResolver') && FULL.includes('GlobalParamResolver'));
ok('T22c ★ 待你拍板 2 条', FULL.includes('要你拍板的 2 条'));
ok('T22d ★ DocInfo 六项已说明',
  ['No','Title','Version','StandardNo','StageName','Page'].every(k=>FULL.includes('<code>'+k+'</code>')));
ok('T22e ★ 全局参数四组说明卡存在', FULL.includes('「全局参数」下拉里到底是哪些'));
ok('T22f ⛔ 无死字段 combine/firstHit/concat/onMissing',
  !/combine|firstHit|concat|onMissing/.test(FULL));
ok('T22g ⛔ 无「企业资料画像」', !FULL.includes('企业资料画像'));
ok('T22h 三段 step 块 = 3', cnt(FULL, /class="step"/g) === 3, cnt(FULL, /class="step"/g));
ok('T22i ★ 只读 / 可覆盖 徽标 CSS 已定义',
  FULL.includes('.cap.ro') && FULL.includes('.cap.rw'));
ok('T22j ⛔ 不再出现「三个来源」旧口径', !FULL.includes('三个来源'));

console.log(fail ? ('FAILED: ' + fail + ' 失败 / 共 ' + (pass+fail)) : ('ALL PASS (' + pass + ' 条断言)'));
process.exit(fail ? 1 : 0);
