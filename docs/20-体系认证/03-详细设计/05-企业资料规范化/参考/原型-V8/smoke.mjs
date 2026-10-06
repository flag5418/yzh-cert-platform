import fs from 'fs';
const html = fs.readFileSync('index.html','utf8');
let js = html.match(/<script>([\s\S]*)<\/script>/)[1];
js = js.replace(/if \(location\.search[\s\S]*$/, '');
const mk = () => new Proxy({ innerHTML:'', textContent:'', className:'', style:{},
  classList:{ add(){}, remove(){}, contains(){return false} } },
  { get(t,k){ return (k in t)?t[k]:undefined; }, set(t,k,v){ t[k]=v; return true; } });
const store = {};
global.document = { getElementById: id => (store[id]=store[id]||mk()), querySelectorAll:()=>[] };
global.location={search:''}; global.setTimeout=()=>0; global.clearTimeout=()=>{};

const api = new Function(js + `
; return { render, scopeFolders, scopeFiles, openFile, backFolder, goFolder, goEnt, goStage,
           goStd, expandAll, cycleType, toggleSel, toggleAll, showRule, showField, showQueue,
           togglePin, pickDoc, editCell, toggleHelp, stateOf, issues, missing };`)();

const steps = [];
const run = (n,f) => { try { f(); steps.push('✓ '+n); } catch(e){ steps.push('✗ '+n+' :: '+e.message); } };

run('企业层渲染', ()=>{ api.goEnt(); api.render(); });
run('阶段层渲染', ()=>{ api.goStage('S_RE'); api.render(); });
run('标准层渲染', ()=>{ api.goStd('ISO9001'); api.render(); });
run('展开全部', ()=>api.expandAll(true));
run('进入文件夹 F0', ()=>api.goFolder('F0'));
run('打开 营业执照', ()=>api.openFile('D_LICENSE'));
run('打开 标准自带文档', ()=>api.openFile('D_SYSFILE'));
run('打开 风险管理报告', ()=>api.openFile('D_RISK'));
run('切到审核 tab', ()=>{ api.openFile('D_RISK'); api.showField('D_RISK','RISK_POLICY'); });
run('规则回链', ()=>api.showRule('match'));
run('人工覆盖类型', ()=>api.cycleType('D_SYSFILE'));
run('覆盖后状态重算', ()=>{ if(!api.stateOf('D_SYSFILE')) throw new Error('无状态'); });
run('选定原件', ()=>api.pickDoc('D_IDCARD','R010'));
run('勾选文件', ()=>api.toggleSel('D_RISK'));
run('全选', ()=>{ api.goFolder('F4'); api.toggleAll(true); });
run('改值', ()=>api.editCell('D_RISK','LEGAL_PERSON','李四'));
run('顶住切换', ()=>api.togglePin('D_RISK','LEGAL_PERSON',true));
run('队列抽屉', ()=>api.showQueue());
run('帮助浮层', ()=>{ api.toggleHelp(); api.toggleHelp(); });
run('返回上级', ()=>{ api.backFolder(); api.goStd('ISO9001'); api.goStage('S_RE'); api.goEnt(); });
run('再渲染', ()=>api.render());
run('状态三态齐全', ()=>{ const s=new Set(api.scopeFiles().map(api.stateOf)); ['none','draft','done'].forEach(x=>{ if(!s.has(x)) throw new Error('缺 '+x); }); });
run('每个文件都有问题清单', ()=>{ api.scopeFiles().forEach(c=>{ if(!Array.isArray(api.issues(c))) throw new Error(c); }); });

console.log(steps.join('\n'));
const bad = steps.filter(s=>s.startsWith('✗'));
console.log('\n' + (steps.length-bad.length) + '/' + steps.length + ' 步通过');
process.exit(bad.length?1:0);
