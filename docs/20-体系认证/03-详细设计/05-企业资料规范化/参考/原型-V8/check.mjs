import fs from 'fs';
const html = fs.readFileSync('index.html','utf8');
const m = html.match(/<script>([\s\S]*)<\/script>/);
if(!m){ console.error('no script'); process.exit(1); }
let js = m[1];
// 去掉启动渲染 + selftest 自动触发，改为显式调用
js = js.replace(/^render\(\);\s*$/m, '');
js = js.replace(/if \(location\.search[\s\S]*$/, '');

const mk = () => new Proxy({ innerHTML:'', textContent:'', className:'', style:{},
  classList:{ add(){}, remove(){}, contains(){return false} } },
  { get(t,k){ if(k in t) return t[k]; return undefined; }, set(t,k,v){ t[k]=v; return true; } });

const store = {};
global.document = { getElementById: id => (store[id] = store[id] || mk()), querySelectorAll: ()=>[] };
global.location = { search:'' };
global.setTimeout = (f)=>0; global.clearTimeout = ()=>{};
global.console = console;

const fn = new Function(js + '\n; return { selfTest, missing, stateOf, effType, issues, RULE, DEP_KILL, DEP_KEEP, DEP_REVIEW, S, DOCS };');
const api = fn();
const R = api.selfTest();
const pass = R.filter(x=>x.startsWith('✓')).length;
console.log('\n结果: ' + pass + '/' + R.length + ' 通过');
process.exit(pass===R.length?0:1);
