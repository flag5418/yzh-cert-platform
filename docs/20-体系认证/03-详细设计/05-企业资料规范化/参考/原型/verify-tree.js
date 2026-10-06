/* ★ v4 专项验证：左树只到文件夹 / 三态 / 顶住 / 依赖失效 */
const fs = require('fs'), vm = require('vm'), path = require('path');
const D = __dirname;
const html = fs.readFileSync(path.join(D, 'index.html'), 'utf8');
const REAL = new Set([...html.matchAll(/id="([^"]+)"/g)].map(m => m[1]));
const store = {};
const mk = () => { const e = { textContent:'', value:'', style:{}, disabled:false, children:[],
  classList:{toggle(){},add(){},remove(){},contains:()=>false}, appendChild(){},remove(){},focus(){},
  setAttribute(){},getAttribute:()=>null,insertBefore(){},contains:()=>false,
  querySelector:()=>null,querySelectorAll:()=>[],closest:()=>null };
  let h=''; Object.defineProperty(e,'innerHTML',{get:()=>h,set:v=>{h=String(v);}}); return e; };
const ctx = { document:{ getElementById:i=>REAL.has(i)?(store[i]||(store[i]=mk())):null,
    createElement:()=>mk(), querySelector:()=>null, querySelectorAll:()=>[],
    addEventListener:(e,f)=>{ if(e==='DOMContentLoaded') ctx.__b=f; }, body:mk() },
  console, Math, Date, JSON, Object, Array, String, Number, isNaN, confirm:()=>true,
  location:{reload(){}}, CSS:{escape:s=>s}, setTimeout:()=>0, __b:null };
vm.createContext(ctx);
vm.runInContext(fs.readFileSync(path.join(D,'data.js'),'utf8'), ctx);
vm.runInContext(fs.readFileSync(path.join(D,'app.js'),'utf8'), ctx);
ctx.__b();
const $t = () => store.tree.innerHTML;
const run = e => vm.runInContext(e, ctx);
const tb = () => store.tbody.innerHTML;
const fp = () => store.filePage.innerHTML;
let bad = 0;
const chk = (c, m, x) => { console.log((c?'✓ ':'✗ ')+m+(x?'  '+x:'')); if(!c) bad++; };

console.log('\n【1】左树只到「文件夹」，不含文件');
const t = $t();
chk(t.length > 0, 'tree 非空');
chk(/0 基础资料/.test(t), '含文件夹节点');
chk(!/风险管理报告|法人身份证|营业执照/.test(t), '★ 不含任何文件名');
run('expandAll(true)');
const t2 = $t();
chk(/4 记录文件/.test(t2), '展开后含更多文件夹');
chk(!/风险管理报告/.test(t2), '★ 展开后仍不含文件名');

console.log('\n【2】右区只有两种页面');
run("pickStage('S_RE');pickStd('ISO9001')");
chk(store.folderCard.style.display === '' && store.filePage.style.display === 'none', '标准层 → 文件夹列表页');
run("pickFolder('F4')");
chk(store.filePage.style.display === 'none', '文件夹页 → 仍是列表页');
run("gotoFile('D_RISK')");
chk(store.folderCard.style.display === 'none' && store.filePage.style.display === '', '★ 点文件 → 文件页面（整页替换）');
run('backToFolder()');
chk(store.folderCard.style.display === '' , '回退 → 文件夹页面');

console.log('\n【3】三态：锁定 / 为空 / 待审');
const st = c => vm.runInContext(`(()=>{const f=findFile('${c}');return docState(f)})()`, ctx);
run("toggleLock('D_RISK',true)"); chk(st('D_RISK')==='locked', '锁定后 = locked');
run("toggleLock('D_RISK',false)"); chk(st('D_RISK')==='review', '解锁后 = review（即使 100%）');
chk(st('D_AUDITREC')==='empty', '未生成 = empty');
chk(st('D_LICENSE_I')==='empty', 'fixed 未确认 = empty');
run("unpick('D_LICENSE_I'); toggleLock('D_LICENSE_I',false); render(); pick('D_LICENSE_I','OF001')");
chk(st('D_LICENSE_I')==='locked', '★ fixed 选中即锁定（无需再点确认）');

console.log('\n【4】顶住：自动 + 人工');
const pin = (doc,field) => vm.runInContext(`(()=>{const c=S.docs['${doc}'].Cells.find(x=>x.Field==='${field}');return [isPinned(c),pinSource(c)]})()`, ctx);
chk(JSON.stringify(pin('D_RISK','ENT_NAME'))==='[true,"auto"]', '全局参数 → 自动顶住');
chk(JSON.stringify(pin('D_RISK','RISK_ROWS'))==='[false,""]', '企业资料 → 不自动顶住');
run("togglePin('D_RISK','{{table:风险清单}}',true)");
chk(JSON.stringify(pin('D_RISK','RISK_ROWS'))==='[true,"manual"]', '★ 人工勾选后 = manual');
run("togglePin('D_RISK','{{table:风险清单}}',false)");
chk(pin('D_RISK','RISK_ROWS')[0]===false, '可解除顶住');
run("bulkPin('D_RISK',true)");
const allPinned = vm.runInContext(`S.docs.D_RISK.Cells.filter(c=>c.Kind!=='pending').every(c=>isPinned(c))`, ctx);
chk(allPinned, '★ 批量顶住：所有已填字段都顶住');

console.log('\n【5】依赖失效：不一刀切');
const inv = vm.runInContext(`(()=>{const r=applyInvalidate('OF030');return {n:r.Cleared.length,skipped:r.SkippedLocked}})()`, ctx);
chk(inv.n > 0, 'profile/ai 来源字段被清空', inv.n + ' 个');
const clearedFields = vm.runInContext(`MOCK.invalidated.Cleared.map(c=>c.Kind)`, ctx);
chk(clearedFields.every(k => ['profile','ai'].includes(k)), '★ 只清 profile/ai，不清 global/compute/manual');
const kept = vm.runInContext(`(()=>{const c=S.docs.D_RISK.Cells.find(x=>x.Field==='ENT_NAME');return c.Kind})()`, ctx);
chk(kept==='global', '★ global 字段保留（仍是 global，未被清）');
const pinnedKept = vm.runInContext(`(()=>{bulkPin('D_RISK',true);applyInvalidate('OF032');const c=S.docs.D_RISK.Cells.find(x=>x.Field==='RISK_POLICY');return c.Kind})()`, ctx);
chk(pinnedKept!=='pending', '★ 人工顶住的字段不因资料更新被清空');

console.log('\n【6】锁定文档完全不受资料更新影响');
run("toggleLock('D_RESP',true)");
const before = vm.runInContext(`JSON.stringify(S.docs.D_RESP)`, ctx);
run("applyInvalidate('OF030')");
const after = vm.runInContext(`JSON.stringify(S.docs.D_RESP)`, ctx);
chk(before===after, '★ 锁定文档的内容零变化');

console.log('\n【7】文件页面两 tab');
run("gotoFile('D_RISK');S.fdTab=0;render()");
chk(/pdf__page/.test(fp()), '预览 tab = PDF');
chk(!/从企业资料筛选/.test(fp()), '★ 预览 tab 无编辑控件');
run('S.fdTab=1;render()');
chk(/从企业资料筛选/.test(fp()), '审核 tab = 字段表（可编辑）');
chk(/勾选 = 认可|勾选=认可/.test(fp()), '★ 审核 tab 说明「勾选=认可」');
chk(!/历史留痕/.test(fp()), '★ 审计已合并进审核（无独立审计 tab）');
chk(/可信度/.test(fp()), '可信度仍显示（仅供参考）');

console.log('\n【8】★ v6 功能菜单在面包屑下方');
run("pickStage('S_RE');pickStd('ISO9001');pickFolder('F0')");
const fb = () => store.funcBar.innerHTML;
chk(fb().length > 0, '功能菜单条存在');
chk(store.crumb.innerHTML.indexOf('0 基础资料') >= 0, '面包屑含文件夹');
chk(/重新生成/.test(fb()) && /锁定/.test(fb()) && /解锁/.test(fb()) && /导出/.test(fb()), '★ 文件夹操作齐全');
run("gotoFile('D_RISK')");
const fb2 = fb();
chk(/返回文件夹/.test(fb2), '文件页有「返回文件夹」');
chk(/锁定/.test(fb2) && /更新文档/.test(fb2), '★ 可编辑：锁定 + 更新文档');
chk(/关联企业文件/.test(fb2), '★ 显示关联企业文件数');
run("gotoFile('D_IDCARD')");
chk(!/更新文档/.test(fb()), '★ 固定文档无「更新文档」');
chk(/锁定/.test(fb()), '★ 固定文档有锁定');

console.log('\n【9】★ 类型独立成列 + 表格不换行');
run("backToFolder()");
const th = require('fs').readFileSync(path.join(D,'index.html'),'utf8');
chk(/<th[^>]*>类型<\/th>/.test(th), '★ 表头有独立「类型」列');
chk(!/名称[\s\S]{0,80}·[\s\S]{0,40}可编辑/.test(store.tbody.innerHTML), '★ 名称列内不再混入类型标签');
const cssTxt = require('fs').readFileSync(path.join(D,'style.css'),'utf8');
chk(/\.tbl td\{white-space:nowrap/.test(cssTxt), '★ CSS 强制单元格不换行');
chk(/table-layout:fixed/.test(cssTxt), '★ table-layout:fixed（列宽可控）');

console.log('\n【10】★ 说明与注释置底');
chk(store.footnote.innerHTML.indexOf('使用说明') >= 0, '★ 页脚有「使用说明」');
chk(store.footnote.innerHTML.indexOf('左树层级') >= 0, '★ 页脚有规则注释');
const tb2 = store.topbar ? store.topbar.innerHTML : '';
run("switchMenu('normalize')");
chk(store.menu.innerHTML.indexOf('说明') < 0, '★ 顶栏已无「说明」按钮');

console.log('\n【11】★ 底部只留问题');
run("pickStage('S_RE');pickStd('ISO9001');pickFolder('F0')");
const bb = store.bottomBar.innerHTML;
chk(/本页存在的问题/.test(bb), '底部有「本页存在的问题」');
chk(!/重新生成|导出/.test(bb), '★ 底部不再放操作按钮');

console.log('\n【12】★ 备注列');
chk(/remark__t|remark__none/.test(store.tbody.innerHTML), '★ 表格有备注列');

console.log('\n【13】★ v7 顶住=勾选框，去掉「编」按钮');
run("gotoFile('D_RISK');S.fdTab=1;render()");
const t7 = store.filePage.innerHTML;
chk(/<th[^>]*width="34px"[^>]*title="勾选=认可/.test(t7) || /勾选=认可/.test(t7), '★ 表头有勾选列（说明「勾选=认可」）');
chk(/id="pinAll"/.test(t7), '★ 表头有全选勾选框');
chk(!/>编</.test(t7), '★ 已去掉「编」按钮');
chk(/onchange="togglePin/.test(t7), '★ 勾选框绑定 togglePin');
chk(!/bulkPin\('\$\{r.Code\}',true\)/.test(t7), '★ 工具条不再有「全部认可顶住」按钮');
vm.runInContext(`togglePin('D_RISK','{{table:风险清单}}',false);render()`, ctx);   /* 先复位 */
const pinState = vm.runInContext(`(()=>{const c=S.docs.D_RISK.Cells.find(x=>x.Field==='RISK_ROWS');return isPinned(c)})()`, ctx);
chk(pinState===false, 'RISK_ROWS（企业资料来源）默认未顶住');
chk(vm.runInContext(`(()=>{const c=S.docs.D_RISK.Cells.find(x=>x.Field==='ENT_NAME');return isPinned(c)})()`, ctx), 'ENT_NAME（全局参数）自动顶住');
vm.runInContext(`togglePin('D_RISK','{{table:风险清单}}',true);render()`, ctx);
chk(/checked/.test(store.filePage.innerHTML), '★ 勾选后渲染为 checked');
chk(vm.runInContext(`isPinned(S.docs.D_RISK.Cells.find(x=>x.Field==='RISK_ROWS'))`, ctx)===true, '✓ 顶住状态生效');

console.log('\n【14】固定文档：选中即替换');
run("gotoFile('D_IDCARD');S.fdTab=1;render()");
chk(/点卡片即选中并替换|已自动选定/.test(store.filePage.innerHTML), '选中即替换语义');
chk(!/确认并更新/.test(store.filePage.innerHTML), '无「确认并更新」');

console.log('\n' + (bad ? `✗ ${bad} 项失败\n` : '★ 全部通过\n'));
process.exit(bad ? 1 : 0);
