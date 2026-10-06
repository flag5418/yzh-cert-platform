/* ==========================================================================
   原型 42 · v2 · 企业资料规范化 · 交互逻辑
   零依赖，双击 index.html 打开

   ★ v2 改动（按用户 2026-10-04 第二轮反馈）
     1. 左树层级 = 企业 → 阶段 → 标准 → 文件夹 → 文件
        （阶段在上：企业的「阶段定义」已绑定标准）
     2. 日志 / 企业资料管理 移到一级菜单，本页不再承载
     3. 选中任意层级 → 右区显示该范围「执行后的结果」
     4. 上下文操作条随选中节点类型变化
     5. 文档预览 = 生成后的 PDF（不是 HTML 模拟）
     6. 锚点三操作 = 查看 / 编辑 / 锁定
   ========================================================================== */

const S = {
  menu: 'normalize',              // normalize | log | resource
  ent: 'E001',
  stage: 'S_RE',
  std: 'ISO9001',
  folder: null,
  file: null,                     // 当前打开的文件 Code
  page: 'list',                    // ★ v4 'list'=文件夹列表 | 'folder'=文件夹页面 | 'file'=文件页面
  fdTab: 0,                        // ★ v4 文件页面 tab：0=预览 1=审核
  open: {},
  sel: [],
  filter: 'all',
  locked: {},
  queue: null,
  cur: null,                      // 当前抽屉对象
  fdTab: 0,
  edits: {},
  picked: JSON.parse(JSON.stringify(MOCK.fixedDocs)),
  docs:  JSON.parse(JSON.stringify(MOCK.editDocs)),
  logs: [
    { Time:'2026-10-04 09:12:30', User:'王专家', Action:'锁定原件', Target:'营业执照', Reason:'已核对，与上传原件一致', Scope:'文件' },
    { Time:'2026-10-04 09:15:02', User:'王专家', Action:'批量规范化', Target:'4 记录文件 / 5 个文档', Reason:'首次一键规范化', Scope:'目录' },
    { Time:'2026-10-04 09:16:44', User:'王专家', Action:'改值', Target:'风险管理报告 · 表2行4列2 · RISK_LEVEL', Reason:'AI 取「中」，核对原件应为「高」', Scope:'单元格' },
    { Time:'2026-10-04 09:18:10', User:'王专家', Action:'锁定锚点', Target:'风险管理报告 · {{RISK_POLICY}}', Reason:'人工润色过，重写时保留', Scope:'单元格' },
    { Time:'2026-10-04 09:20:55', User:'王专家', Action:'更新文档', Target:'风险管理报告', Reason:'完成度 88% → 88%（19 锚点）', Scope:'文件' }
  ],
  toast: null
};

const MENUS = [
  { k:'normalize', t:'企业资料规范化' },
  { k:'log',       t:'规范化日志' },
  { k:'resource',  t:'企业资料管理' }
];

/* ---------------- 工具 ---------------- */
const $ = id => document.getElementById(id);
const esc = s => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;' }[c]));
const pct = v => Math.round(v * 100) + '%';
function say(kind, text) { S.toast = { kind, text, t: Date.now() }; render(); }
function confLevel(c) {
  if (c >= 0.95) return { k:'hi', t:'高', cls:'tag--success' };
  if (c >= 0.70) return { k:'mid', t:'中', cls:'tag--warning' };
  return { k:'lo', t:'低', cls:'tag--danger' };
}
function confDots(c) {
  const l = confLevel(c);
  return `<span class="dot dot--${l.k}"></span><span class="dot dot--${l.k}"></span><span class="dot dot--${c >= 0.7 ? l.k : 'none'}"></span><span class="small">${l.t}</span>`;
}

/* ---------------- 树访问 ---------------- */
function entNode() { return MOCK.tree[S.ent]; }
function stageNode(code) { return (entNode().Stages || []).find(s => s.Code === code); }
function stdNode(code, stageCode) { return (stageNode(stageCode)?.Standards || []).find(s => s.Code === code); }
function folderNode(code, stageCode, stdCode) {
  const s = stdNode(stdCode, stageCode); if (!s) return null;
  for (const f of s.Folders) if (f.Code === code) return f;
  return null;
}
function findFile(code) {
  for (const st of entNode().Stages || []) {
    for (const s of st.Standards) {
      for (const f of s.Folders) {
        for (const fl of f.Files) {
          if (fl.Code !== code) continue;
          /* ⭐ 返回副本（见 enrich 注释）：同一 Code 可能在多个阶段出现 */
          return Object.assign({}, fl, {
            _stage: st.Code, _stageName: st.Name,
            _std: s.Code, _stdName: s.Name,
            _folder: f.Code, _folderName: f.Name
          });
        }
      }
    }
  }
  return null;
}

/* ======================================================================
   ★ v4 · 页面模型：右区【只有两种页面】
     folder → 文件夹页面（面包屑 + 菜单 + 文件和文件夹列表）
     file   → 文件页面（面包屑 + 菜单 + 预览/审核 两个 tab）
   企业/阶段/标准 层级选中时，仍是「文件夹列表」（等价于标准页的文件夹视图）
   ====================================================================== */
function scope() {
  /* —— 文件页面：只显示这一个文件 —— */
  if (S.page === 'file' && S.file) {
    const f = findFile(S.file);
    return { type: 'file', rows: f ? [enrich(f)] : [] };
  }
  /* —— 文件夹页面：该文件夹下的文件 —— */
  if (S.folder) {
    const f = folderNode(S.folder, S.stage, S.std);
    return { type: 'folder', node: f, rows: (f?.Files || []).map(enrich) };
  }
  /* —— 标准层：文件夹列表 —— */
  if (S.std) {
    const sd = stdNode(S.std, S.stage);
    const rows = [];
    (sd?.Folders || []).forEach(fo => rows.push({
      _isFolder: true, Code: fo.Code, Name: fo.Name, Cat: 'folder', Std: S.std, Stage: S.stage
    }));
    return { type: 'std', node: sd, rows };
  }
  /* —— 阶段层：全部文件夹 —— */
  if (S.stage) {
    const st = stageNode(S.stage);
    const rows = [];
    (st?.Standards || []).forEach(sd => sd.Folders.forEach(fo => rows.push({
      _isFolder: true, Code: fo.Code, Name: `${sd.Name} / ${fo.Name}`, Cat: 'folder', Std: sd.Code, Stage: st.Code
    })));
    return { type: 'stage', node: st, rows };
  }
  /* —— 企业层：全部文件夹 —— */
  const rows = [];
  (entNode().Stages || []).forEach(st => st.Standards.forEach(sd => sd.Folders.forEach(fo => rows.push({
    _isFolder: true, Code: fo.Code, Name: `${st.Name} / ${sd.Name} / ${fo.Name}`, Cat: 'folder', Std: sd.Code, Stage: st.Code
  }))));
  return { type: 'ent', node: entNode(), rows };
}

/** 文件夹的完成度统计（用于文件夹列表的三态徽标） */
function folderStats(code) {
  const f = folderNode(code, S.stage, S.std) || findFolderAnywhere(code);
  if (!f) return { total: 0, locked: 0, empty: 0, review: 0 };
  const rows = f.Files.map(enrich);
  const cnt = { total: rows.length, locked: 0, empty: 0, review: 0 };
  rows.forEach(r => cnt[docState(r)]++);
  return cnt;
}
function findFolderAnywhere(code) {
  for (const st of entNode().Stages || [])
    for (const sd of st.Standards)
      for (const fo of sd.Folders)
        if (fo.Code === code) return fo;
  return null;
}

/** ⭐ 返回**副本**并补齐定位字段 —— ⛔ 绝不能就地改 MOCK 里的节点对象：
 *  同一份文件会在多处被引用（不同阶段/标准），就地改会互相污染，
 *  导致 findFile 找到「已被 enrich 过但不属于当前路径」的那份。 */
function enrich(fl) {
  return Object.assign({}, fl, {
    _stage:   fl._stage   || S.stage,
    _std:     fl._std     || S.std,
    _folder:  fl._folder  || S.folder,
    _folderName: fl._folderName || ''
  });
}

/* ======================================================================
   ★ v4 · 三态派生（不是存储字段，是实时算出来的）
   ----------------------------------------------------------------------
     锁定 → IsLocked = 1                       只有人工能进此态
     为空 → 无锚点规则 / 无匹配信息 / 锚点总数=0   系统判定
     待审 → 其余全部（含「填完 100%」）          没锁定就是没认可
   ⚠️ 因此「完成度 100% 但没锁定」仍然是「待审」
   ====================================================================== */
function docState(r) {
  if (S.locked[r.Code]) return DOC_STATE.LOCKED;
  if (r.Cat === 'platform_generated') return DOC_STATE.EMPTY;   /* 平台生成，无需审核 */
  if (r.Cat === 'fixed') {
    const p = S.picked[r.Code];
    if (!p || p.State === 'no_match') return DOC_STATE.EMPTY;  /* 无匹配信息 */
    if (p.State === 'need_confirm') return DOC_STATE.REVIEW;   /* 待人工指定 */
    return DOC_STATE.EMPTY;                                     /* fixed：非锁定一律「为空」（待接收） */
  }
  const e = S.docs[r.Code];
  if (!e || e.Status === 'none') return DOC_STATE.EMPTY;
  if (e.AnchorTotal === 0) return DOC_STATE.EMPTY;               /* 无填写规则 */
  return DOC_STATE.REVIEW;                                        /* 其余全部 */
}
const STATE_META = {
  locked: { label:'锁定', cls:'tag--warning', icon:'🔒', tip:'审核员已认可，不受后续上传/重新生成影响' },
  empty:  { label:'为空', cls:'tag--gray',    icon:'○',  tip:'无填写规则 / 无匹配信息 / 锚点总数为 0' },
  review: { label:'待审', cls:'tag--info',    icon:'◔',  tip:'已填写，但可能有空字段或信息未确认（含 100% 完成）' }
};
function stateTag(r) {
  const st = docState(r), m = STATE_META[st];
  return `<span class="tag ${m.cls}" title="${m.tip}">${m.icon} ${m.label}</span>`;
}

/* ======================================================================
   ★ v4 · 依赖失效（企业重新上传 → 反查字段 → 按来源决定清空/保留）
   ----------------------------------------------------------------------
   ★ 关键：不一刀切！只有 profile/ai 来源才清空；
     global/compute/replace 不依赖企业文件 ⇒ 保留；
     manual 是人填的 ⇒ 保留但标「待复核」。
   ====================================================================== */
const DEP_KILL   = ['profile', 'ai'];      /* ⛔ 依赖企业文件 ⇒ 清空 */
const DEP_KEEP   = ['global', 'compute', 'replace'];  /* ✅ 不依赖 ⇒ 保留 */
const DEP_REVIEW = ['manual'];            /* ⚠️ 人填 ⇒ 保留 + 标待复核 */

/** 某文档的字段依赖清单（从 MOCK.fieldDeps 读） */
function depsOf(docCode) { return MOCK.fieldDeps[docCode] || []; }

/** 某文档依赖了哪些企业文件（文件级） */
function depFiles(docCode) {
  const set = new Set();
  depsOf(docCode).forEach(d => set.add(d.From));
  /* fixed 文档也依赖：选定的原件 */
  const p = S.picked[docCode];
  if (p && p.Picked) set.add(p.Picked);
  return [...set];
}

/** ★ 核心：应用一次「企业文件重新上传」的失效传播（原型可点，用于演示） */
function applyInvalidate(originalFileCode) {
  const inv = MOCK.invalidated;
  inv.File = originalFileCode;
  const raw = MOCK.rawFiles.find(x => x.Code === originalFileCode);
  const newVer = (raw ? raw.Ver : 1) + 1;
  inv.NewVer = newVer; inv.NewSha = (raw ? raw.Sha : '') + '-x';
  inv.At = new Date().toLocaleString('zh-CN', { hour12: false }).replace(/\//g, '-');

  /* 反查：哪些字段依赖了这份文件 —— ⛔ 不能全表扫 JSON，必须走反向索引（cert_doc_fill_dependency） */
  const hits = [];
  Object.keys(MOCK.fieldDeps).forEach(docCode => {
    MOCK.fieldDeps[docCode].forEach(d => { if (d.From === originalFileCode) hits.push({ docCode, ...d }); });
  });
  /* fixed 文档：选定的原件变了 ⇒ 需要重新确认（但不「清空」，只是标记） */
  const fixedHits = Object.keys(S.picked).filter(code => S.picked[code].Picked === originalFileCode);

  const cleared = [];
  const keptPinned = [];
  let skippedLocked = 0;
  hits.forEach(h => {
    /* ★ 规则 ①：宿主文档已锁定 ⇒ 不动（26 号 S-6 锁定优先） */
    if (S.locked[h.docCode]) { skippedLocked++; return; }
    const e = S.docs[h.docCode];
    if (!e || !e.Cells) return;
    /* ★ 规则 ②：只有 profile/ai 来源才清空 */
    const c = e.Cells.find(x => x.Field === h.Field || x.Loc === h.Field || x.Ref === h.Field);
    if (!c) return;
    /* ★ 规则 ③（★ 本轮补充）：已「顶住」的字段一律不清空
       —— 顶住 = 「我认可了这个值，不要动」 */
    if (isPinned(c)) { keptPinned.push({ Doc: h.docCode, Field: c.Field, Loc: c.Loc }); return; }
    if (DEP_KILL.includes(c.Kind)) {
      cleared.push({ Doc: h.docCode, Field: c.Field || h.Field, Loc: c.Loc, From: originalFileCode, Kind: c.Kind });
      c.Val = ''; c.Kind = 'pending'; c.Conf = 0; c.Invalidated = true;
    } else if (DEP_REVIEW.includes(c.Kind)) {
      c.NeedRecheck = true;   /* ⚠️ 人工值保留，但标待复核 */
    } else {
      /* global/compute/replace ⇒ 完全不动 */
    }
  });

  /* 重算受影响文档的完成度 */
  const touched = [...new Set(cleared.map(c => c.Doc))];
  touched.forEach(code => {
    const e = S.docs[code];
    const filled = e.Cells.filter(x => x.Kind !== 'pending').length;
    e.FilledCount = filled;
    e.Completion = e.AnchorTotal ? filled / e.AnchorTotal : 0;
    e.Status = e.Completion >= 1 ? 'generated' : 'partial';
  });

  inv.Cleared = cleared;
  inv.KeptPinned = keptPinned;
  inv.SkippedLocked = skippedLocked;
  inv.FixedHits = fixedHits;
  if (raw) { raw.Ver = newVer; raw.Sha = inv.NewSha; raw.Updated = inv.At.slice(0, 10); }
  addLog('依赖失效', `${MOCK.rawFiles.find(x => x.Code === originalFileCode)?.Name || originalFileCode} → v${newVer}`,
    `清空 ${cleared.length} 个字段；顶住保留 ${keptPinned.length} 个；跳过锁定文档 ${skippedLocked} 个`, '文件');
  return inv;
}

/* ---------------- 完成度 / 可信度 ---------------- */
function doneRatio(r) {
  if (S.locked[r.Code]) return 1;
  if (r.Cat === 'fixed') {
    const p = S.picked[r.Code];
    if (!p) return 0;
    return p.State === 'confirmed' ? 1 : (p.State === 'need_confirm' ? 0.5 : (p.State === 'platform_generated' ? 1 : 0.6));
  }
  const e = S.docs[r.Code]; if (!e) return 0;
  if (e.Status === 'platform_generated') return 1;
  if (e.AnchorTotal === 0) return e.Status === 'generated' ? 1 : 0;
  return e.Completion;
}
function cellsOf(r) { return (S.docs[r.Code] && S.docs[r.Code].Cells) || []; }
function avgConf(r) {
  const cs = cellsOf(r).filter(c => c.Kind !== 'pending').map(c => c.Conf);
  return cs.length ? cs.reduce((a,b)=>a+b,0) / cs.length : 1;
}
function minConf(r) {
  const cs = cellsOf(r).filter(c => c.Kind !== 'pending').map(c => c.Conf);
  return cs.length ? Math.min.apply(null, cs) : 1;
}
function pickConf(code) {
  const p = S.picked[code]; const c = (MOCK.candidates[code] || []).find(x => x.FileCode === p?.Picked);
  return c ? c.Score : 0.5;
}

/* ==================== 左树：★ v4 只到「文件夹」层（不含文件） ==================== */
function renderTree() {
  const e = entNode();
  let h = `<div class="tnode ${!S.stage ? 'is-active' : ''}" onclick="pickEnt()">
      <span class="tnode__caret" onclick="event.stopPropagation();tgl('en')">${S.open.en ? '▼' : '▶'}</span>
      <span class="tnode__icon">🏢</span><span class="tnode__label">${esc(e.Name)}</span>
      <span class="tnode__badge small muted">${e.Stages.length} 阶段</span></div>`;
  if (!S.open.en) { $('tree').innerHTML = h; return; }

  e.Stages.forEach(st => {
    const k = 'st:' + st.Code, on = S.open[k];
    const stCur = S.stage === st.Code && !S.std;
    h += `<div class="tnode ${stCur ? 'is-active' : ''}" style="padding-left:16px" onclick="pickStage('${st.Code}')">
      <span class="tnode__caret" onclick="event.stopPropagation();tgl('${k}')">${on ? '▼' : '▶'}</span>
      <span class="tnode__icon">◈</span><span class="tnode__label">${esc(st.Name)}</span>
      <span class="tnode__badge small muted">${st.Standards.length} 标准</span></div>`;
    if (!on) return;
    st.Standards.forEach(sd => {
      const k2 = 'sd:' + st.Code + ':' + sd.Code, on2 = S.open[k2];
      const sdCur = S.stage === st.Code && S.std === sd.Code && !S.folder;
      h += `<div class="tnode ${sdCur ? 'is-active' : ''}" style="padding-left:34px" onclick="pickStd('${sd.Code}')">
        <span class="tnode__caret" onclick="event.stopPropagation();tgl('${k2}')">${on2 ? '▼' : '▶'}</span>
        <span class="tnode__icon">▤</span><span class="tnode__label">${esc(sd.Name)}</span>
        <span class="tnode__badge small muted">${sd.Folders.length}</span></div>`;
      if (!on2) return;
      /* ★ 到文件夹为止，不再往下带文件（v4 决策：与右区「只两种页面」保持一致） */
      sd.Folders.forEach(fo => {
        const k3 = 'fo:' + fo.Code, on3 = S.open[k3];
        const foCur = S.folder === fo.Code;
        const rows = fo.Files.map(enrich);
        const cnt = { locked: 0, empty: 0, review: 0 };
        rows.forEach(r => cnt[docState(r)]++);
        h += `<div class="tnode ${foCur ? 'is-active' : ''}" style="padding-left:52px" onclick="pickFolder('${fo.Code}')">
          <span class="tnode__caret" onclick="event.stopPropagation();tgl('${k3}')">${on3 ? '▼' : '▶'}</span>
          <span class="tnode__icon">▣</span><span class="tnode__label">${esc(fo.Name)}</span>
          <span class="tnode__badge nowrap">
            ${cnt.locked ? `<span class="tag tag--warning" title="锁定">🔒${cnt.locked}</span>` : ''}
            ${cnt.empty  ? `<span class="tag tag--gray" title="为空">○${cnt.empty}</span>` : ''}
            ${cnt.review ? `<span class="tag tag--info" title="待审">◔${cnt.review}</span>` : ''}
          </span></div>`;
      });
    });
  });
  $('tree').innerHTML = h;
}
function pickEnt()    { S.stage = null; S.std = null; S.folder = null; S.page = 'list'; S.sel = []; S.open.en = true; render(); }
function pickStage(c) { S.stage = c; S.std = null; S.folder = null; S.page = 'list'; S.sel = []; S.open['st:' + c] = true; render(); }
function pickStd(c)   { S.std = c; S.folder = null; S.page = 'list'; S.sel = []; S.open['sd:' + S.stage + ':' + c] = true; render(); }
/** ★ v4：选中文件夹 ⇒ 右区切到「文件夹页面」 */
function pickFolder(c){ S.folder = S.folder === c ? null : c; S.page = S.folder ? 'folder' : 'list'; S.sel = []; S.open['fo:' + c] = true; render(); }
/** ★ v4：从文件夹页面点某个文件 ⇒ 切到「文件页面」（整页替换，不是抽屉） */
function gotoFile(code) { S.file = code; S.page = 'file'; S.sel = []; render(); }
function backToFolder() { S.file = null; S.page = 'folder'; render(); }

function tgl(k) { S.open[k] = !S.open[k]; render(); }
function expandAll(v) {
  const e = entNode();
  S.open.en = v;
  e.Stages.forEach(st => {
    S.open['st:' + st.Code] = v;
    st.Standards.forEach(sd => {
      S.open['sd:' + st.Code + ':' + sd.Code] = v;
      sd.Folders.forEach(fo => { S.open['fo:' + fo.Code] = v; });   /* ★ v4：树不到文件层 */
    });
  });
  render();
}
/* ==================== 抽屉 ==================== */
const CAP = { dep:'Dep', q:'Q', h:'H', m:'M' };
function showDrawer(k) {
  Object.keys(CAP).forEach(x => { if (x !== k) { $('drawer'+CAP[x]).style.display = 'none'; $('mask'+CAP[x]).style.display = 'none'; } });
  $('drawer'+CAP[k]).style.display = 'flex'; $('mask'+CAP[k]).style.display = 'block';
}
function closeDrawer(k) { $('drawer'+CAP[k]).style.display = 'none'; $('mask'+CAP[k]).style.display = 'none'; }

/* ---------- 从企业原始资料里筛候选值 ---------- */
const RAW_CAND = {
  RISK_LEVEL:[['2026年度风险清单.xlsx','高'],['2026年度风险清单.xlsx','中'],['2026年度风险清单.xlsx','低']],
  RISK_POLICY:[['质量管理体系风险管理制度.docx','（略：质量管理体系风险管理制度 第3章 风险评价准则）']],
  RISK_ROWS:[['2026年度风险清单.xlsx','12 行 × 4 列']],
  AUDIT_LEADER:[['2026年度内审计划.docx','李建国'],['组织架构图.png','李建国（质量管理部经理）']],
  AUDIT_ROWS:[['2026年度内审计划.docx','10 行 × 7 列']],
  TRAIN_TOPIC:[['2026年1月培训签到表.xlsx','ISO 9001:2015 换版要点'],['2026年1月培训签到表.xlsx','内审员培训']],
  TRAIN_HOURS:[['2026年1月培训签到表.xlsx','4'],['2026年1月培训签到表.xlsx','6']],
  TRAIN_ROW2:[['2026年1月培训签到表.xlsx','（签到表：2026-01-15 内审员培训 6 人）']],
  TRAIN_ROW3:[['2026年1月培训签到表.xlsx','（签到表：2026-01-16 换版宣贯 23 人）']],
  TRAIN_ROW4:[['2026年1月培训签到表.xlsx','（签到表：待补充）']],
  OBJ_2026:[['质量管理体系风险管理制度.docx','顾客满意度 ≥ 95%'],['2026年度风险清单.xlsx','≥ 95%']],
  OBJ_ROWS:[['质量管理体系风险管理制度.docx','6 行 × 3 列']]
};
function rawCandidates(field) { return (RAW_CAND[field] || []).map(x => ({ f: x[0], v: x[1] })); }

/* ==================== 主渲染 ==================== */
function render() {
  $('menu').innerHTML = MENUS.map(m => `<span class="menu__i ${S.menu === m.k ? 'is-active' : ''}" onclick="switchMenu('${m.k}')">${m.t}</span>`).join('');
  if (S.menu !== 'normalize') { renderSubMenu(); return; }
  $('mainBox').style.display = '';
  renderTree();
  const sc = scope();
  renderCrumb(sc);
  renderFuncBar(sc);         /* ★ 面包屑下方的功能菜单 */
  if (sc.type === 'file') renderFilePage(sc.rows[0]);
  else renderFolderCard(sc);
  renderBottom(sc);          /* ★ 底部：本页存在的问题（操作已在上方） */
  renderFootnote();
  renderQueue();
  if (S.toast && Date.now() - S.toast.t < 2600) {
    const b = document.createElement('div');
    b.className = 'toast toast--' + (S.toast.kind === 'ok' ? 'ok' : S.toast.kind === 'warn' ? 'warn' : 'info');
    b.textContent = S.toast.text; document.body.appendChild(b);
    setTimeout(() => b.remove(), 2600); S.toast = null;
  }
}

/** ★ 演示：模拟「企业重新上传了某份文件」→ 依赖失效传播 */
function demoUpload() {
  const pool = MOCK.rawFiles.filter(x => depFilesOfAny().includes(x.Code));
  if (!pool.length) return say('info', '当前没有可触发依赖的文件');
  const f = pool[Math.floor(Math.random() * pool.length)];
  applyInvalidate(f.Code);
  say('warn', `模拟：${f.Name} 重新上传 → 依赖它的字段已按「顶住/来源」规则处理`);
  render();
}
function depFilesOfAny() {
  const s = new Set();
  Object.keys(MOCK.fieldDeps).forEach(d => MOCK.fieldDeps[d].forEach(x => s.add(x.From)));
  return [...s];
}

/* ---------- 面包屑（★ 可定位到目录） ---------- */
function renderCrumb(sc) {
  const e = entNode(), st = S.stage ? stageNode(S.stage) : null;
  const sd = S.std ? stdNode(S.std, S.stage) : null;
  const fo = S.folder ? (folderNode(S.folder, S.stage, S.std) || findFolderAnywhere(S.folder)) : null;
  const fi = S.file ? findFile(S.file) : null;
  const items = [
    { t: e.Name, cur: !st, go: 'pickEnt()' },
    st ? { t: st.Name, cur: !sd, go: `pickStage('${st.Code}')` } : null,
    sd ? { t: sd.Name, cur: !fo && !fi, go: `pickStd('${sd.Code}')` } : null,
    fo ? { t: fo.Name, cur: !fi, go: 'backToFolder()' } : null
  ].filter(Boolean);
  if (fi) items.push({ t: fi.Name, cur: true, go: '' });
  $('crumb').innerHTML = items.map((x, i) =>
    (i ? '<span class="crumb__sep">›</span>' : '') +
    `<span class="crumb__i ${x.cur ? 'is-cur' : ''}" onclick="${x.go}">${esc(x.t)}</span>`).join('');
}

/* ======================================================================
   ★ v4 · 顶住（pin）的两个来源
   ----------------------------------------------------------------------
   自动 auto   ：manual / global / compute / replace  —— 不依赖企业文件
   人工 manual ：审核员勾选认可 AI/画像推断的结果（★ 本轮新增）
   ⇒ 「顶住」= 这个值不受企业文件变更影响
   ====================================================================== */
function pinAuto(kind) {
  return DEP_KEEP.includes(kind) || DEP_REVIEW.includes(kind);   /* manual / global / compute / replace */
}
function isPinned(cell) {
  if (cell.PinManual !== undefined) return cell.PinManual;      /* 人工勾选优先 */
  if (cell.PinOverride === false) return false;                  /* 人工显式解除 */
  return pinAuto(cell.Kind);
}
function pinSource(cell) {
  if (cell.PinManual) return 'manual';
  if (cell.PinOverride === false) return 'off';
  return pinAuto(cell.Kind) ? 'auto' : '';
}
function togglePin(docCode, ref, v) {
  const e = S.docs[docCode];
  const c = (e.Cells || []).find(x => x.Ref === ref); if (!c) return;
  if (v) { c.PinManual = true; c.PinOverride = undefined; }
  else { c.PinManual = false; c.PinOverride = false; }
  say(v ? 'ok' : 'info', v ? '已顶住：企业文件变更不再影响此值' : '已解除顶住');
  render();
}
/** ★ 批量顶住：认可本页所有推断结果（审核员一键） */
function bulkPin(docCode, v) {
  const e = S.docs[docCode]; if (!e || !e.Cells) return;
  let n = 0;
  e.Cells.forEach(c => {
    if (c.Kind === 'pending') return;                 /* 空值不顶住 */
    if (v) { if (!isPinned(c)) n++; c.PinManual = true; c.PinOverride = undefined; }
    else { if (isPinned(c) && pinSource(c) === 'manual') n++; c.PinManual = false; c.PinOverride = false; }
  });
  addLog(v ? '批量顶住' : '批量解除顶住', e.Template, `${n} 个字段`);
  say(v ? 'ok' : 'info', v ? `已顶住 ${n} 个推断结果，之后不受企业文件变更影响` : `已解除 ${n} 个`);
  render();
}

/* ======================================================================
   ★ v5 · 问题诊断（★ 一个函数同时供「底部问题清单」与「表格备注列」使用）
   ----------------------------------------------------------------------
   原则：★ 只报「需要人动手」的，不做统计、不做告警、不拦截
   ====================================================================== */
function diagnose(r) {
  const out = [];
  const e = S.docs[r.Code], p = S.picked[r.Code];
  const inv = MOCK.invalidated || { Cleared: [], KeptPinned: [] };

  /* ① 依赖失效：字段被清空 */
  const cleared = (inv.Cleared || []).filter(x => x.Doc === r.Code);
  if (cleared.length) out.push({ lv:'danger', n: cleared.length, t: `${cleared.length} 个字段因企业资料更新被清空`, go: `gotoFile('${r.Code}')` });

  /* ② 固定文档未选定原件 */
  if (r.Cat === 'fixed' && p && p.State === 'need_confirm') {
    out.push({ lv:'danger', n: p.CandidateCount, t: `企业上传了 ${p.CandidateCount} 份同类文件，需你选定`, go: `gotoFile('${r.Code}')` });
  }
  /* ③ 固定文档无候选 */
  if (r.Cat === 'fixed' && (!p || p.State === 'no_match')) {
    out.push({ lv:'danger', n: 1, t: '企业资料中找不到可匹配的文件', go: `gotoFile('${r.Code}')` });
  }
  /* ④ 固定文档告警（如证件过期） */
  if (r.Cat === 'fixed' && p && p.State === 'warn') {
    out.push({ lv:'warn', n: 1, t: p.Reason || '原件已接收，但有告警', go: `gotoFile('${r.Code}')` });
  }
  /* ⑤ 可编辑：未生成 */
  if (r.Cat === 'editable' && (!e || e.Status === 'none')) {
    out.push({ lv:'warn', n: 1, t: '尚未规范化（可能模板未上传）', go: `gotoFile('${r.Code}')` });
  }
  /* ⑥ 可编辑：待填字段 */
  if (e && e.Cells) {
    const pend = e.Cells.filter(c => c.Kind === 'pending').length;
    if (pend) out.push({ lv:'warn', n: pend, t: `${pend} 个字段还没填`, go: `gotoFile('${r.Code}')` });
    /* ⑦ 推断结果尚未认可（profile/ai 且未顶住） */
    const unpinned = e.Cells.filter(c => c.Kind !== 'pending' && !isPinned(c) && (c.Kind === 'profile' || c.Kind === 'ai')).length;
    if (unpinned) out.push({ lv:'info', n: unpinned, t: `${unpinned} 个推断结果待你认可（可一键顶住）`, go: `gotoFile('${r.Code}')` });
  }
  /* ⑧ 待办改动未提交 */
  if (countEdits(r.Code)) out.push({ lv:'warn', n: countEdits(r.Code), t: `${countEdits(r.Code)} 处改动未提交`, go: `gotoFile('${r.Code}')` });

  return out;
}

/** 文件夹页：把子文件的问题聚合 */
function diagnoseFolder(code) {
  const f = folderNode(code, S.stage, S.std) || findFolderAnywhere(code);
  if (!f) return [];
  const agg = {};
  f.Files.map(enrich).forEach(r => diagnose(r).forEach(i => {
    const k = i.t.replace(/\d+/g, 'N');
    if (!agg[k]) agg[k] = { ...i, n: 0, docs: [] };
    agg[k].n += i.n; agg[k].docs.push(r.Code);
  }));
  return Object.values(agg).map(x => ({ ...x, t: x.t.replace(/N/, x.docs.length + ' 个文件共 ') }));
}

/* ---------- ★ 底部统一区 ---------- */
function renderBottom(sc) {
  const issues = sc.type === 'file'
    ? diagnose(sc.rows[0] || {})
    : sc.rows[0] && sc.rows[0]._isFolder ? [] : diagnoseFolder(sc.folder);
  const box = $('bottomBar');
  const left = issues.length
    ? `<div class="bottom__title">本页存在的问题（${issues.length}）</div>` +
      issues.slice(0, 4).map(i =>
        `<div class="issue issue--${i.lv}" onclick="${i.go}">
          <span class="issue__n">${i.n}</span>
          <span class="issue__txt">${esc(i.t)}</span>
          <span class="issue__go">去处理 ›</span></div>`).join('') +
      (issues.length > 4 ? `<div class="small muted" style="padding-left:4px">… 还有 ${issues.length - 4} 项</div>` : '')
    : `<div class="bottom__title">本页存在的问题</div><div class="small muted">没有需要处理的问题</div>`;

  box.innerHTML = `<div class="bottom"><div class="bottom__left">${left}</div></div>`;
}

/* ======================================================================
   ★ v6 · 功能菜单条（面包屑正下方）
   ====================================================================== */
function renderFuncBar(sc) {
  const bar = $('funcBar');
  if (sc.type === 'file' && sc.rows[0]) {
    const r = sc.rows[0], lk = S.locked[r.Code], isFixed = r.Cat === 'fixed';
    const e = S.docs[r.Code];
    bar.innerHTML =
      `<div class="funcbar__grp">
        <button class="btn btn--sm" onclick="backToFolder()">‹ 返回文件夹</button>
        <span class="crumb__sep">·</span>
        <span class="tag ${STATE_META[docState(r)].cls}">${STATE_META[docState(r)].icon} ${STATE_META[docState(r)].label}</span>
        ${e && e.AnchorTotal ? `<span class="small muted">${e.FilledCount}/${e.AnchorTotal} 已填 · 可信度 ${pct(avgConf(r))}</span>` : ''}
      </div>
      <span class="grow"></span>
      <div class="funcbar__grp">
        <button class="btn btn--sm" onclick="showDeps('${r.Code}')">关联企业文件（${depFiles(r.Code).length}）</button>
        ${isFixed
          ? `<button class="btn btn--sm ${lk ? '' : 'btn--primary'}" onclick="toggleLock('${r.Code}',${!lk})">${lk ? '🔓 解锁' : '🔒 锁定（认可）'}</button>`
          : `<button class="btn btn--sm" onclick="updateSelf()">🔄 更新文档</button>
             <button class="btn btn--sm ${lk ? '' : 'btn--primary'}" onclick="toggleLock('${r.Code}',${!lk})">${lk ? '🔓 解锁' : '🔒 锁定（认可）'}</button>`}
        <button class="btn btn--sm" onclick="downloadDoc('${r.Code}')">⬇ 下载</button>
      </div>`;
    return;
  }
  /* 文件夹 / 目录层级 */
  const n = S.sel.length;
  const scopeName = (() => {
    const fo = S.folder ? (folderNode(S.folder, S.stage, S.std) || findFolderAnywhere(S.folder)) : null;
    return fo ? fo.Name : (S.std ? (stdNode(S.std, S.stage) || {}).Name : (S.stage ? (stageNode(S.stage) || {}).Name : ''));
  })();
  bar.innerHTML =
    `<div class="funcbar__grp">
      <span class="small muted">已勾选</span><b>${n}</b><span class="small muted">个</span>
      <button class="btn btn--link btn--sm" onclick="S.sel=[];render()">清空</button>
    </div>
    <span class="funcbar__sep"></span>
    <div class="funcbar__grp">
      <button class="btn btn--sm btn--primary" onclick="rewriteSel()">↻ 重新生成</button>
      <button class="btn btn--sm" onclick="lockSel(true)">🔒 锁定</button>
      <button class="btn btn--sm" onclick="lockSel(false)">🔓 解锁</button>
    </div>
    <span class="funcbar__sep"></span>
    <div class="funcbar__grp">
      <button class="btn btn--sm" onclick="exportSel()">📦 导出</button>
    </div>
    <span class="grow"></span>
    <span class="small muted">${n ? '「重新生成」会自动解除勾选项的锁定状态' : '勾选文件后可操作 · 重新生成会自动解锁已锁定项'}</span>`;
}

/* ---------- ★ 页脚说明（使用说明也放这里） ---------- */
function renderFootnote() {
  $('footnote').innerHTML = `
    <div class="footnote__item"><span class="footnote__k">左树层级</span>企业 → 阶段（阶段定义已绑定标准）→ 标准 → 文件夹（后台标准文档目录）</div>
    <div class="footnote__item"><span class="footnote__k">状态三态</span><b>锁定</b>=你已认可（不受资料更新影响）·<b>为空</b>=无规则或无匹配 ·<b>待审</b>=其余（含 100% 完成）。合格只由「锁定」决定。</div>
    <div class="footnote__item"><span class="footnote__k">顶住</span>人工填写/全局参数/计算自动顶住；AI 推断需在「审核」里勾选认可。顶住 = 不受企业文件更新影响。</div>
    <div class="footnote__item"><span class="footnote__k">操作</span><button class="btn btn--link btn--sm" onclick="showHelp()">使用说明与完整注释</button>
      <button class="btn btn--link btn--sm" onclick="showHelp()">查看全部规则</button></div>`;
}

/* ---------- 依赖失效明细 ---------- */
function openDepLog() {
  const inv = MOCK.invalidated;
  const raw = MOCK.rawFiles.find(x => x.Code === inv.File);
  $('depBody').innerHTML = `
    <div class="alert alert--info"><b>${esc(raw?.Name || inv.File)}</b> 已更新到 v${inv.NewVer}（内容指纹变化）· ${esc(inv.At)}</div>
    <div class="gtitle">① 已清空的字段（${inv.Cleared.length}）—— 只清 profile / ai 来源</div>
    <table class="tbl"><thead><tr><th>文档</th><th style="width:170px">字段</th><th style="width:130px">位置</th><th style="width:90px">来源</th><th>操作</th></tr></thead><tbody>
    ${inv.Cleared.map(c => `<tr><td>${esc((S.docs[c.Doc] || {}).Template || c.Doc)}</td>
      <td class="mono small">${esc(c.Field)}</td><td class="small">${esc(c.Loc || '—')}</td>
      <td><span class="tag tag--warning">${esc((SRC_KIND[c.Kind] || {}).label || c.Kind)}</span></td>
      <td><button class="btn btn--sm" onclick="closeDrawer('dep');gotoFile('${c.Doc}')">去重填</button></td></tr>`).join('')
      || '<tr><td colspan="5" class="muted" style="text-align:center;padding:16px">无</td></tr>'}
    </tbody></table>
    <div class="gtitle">② 顶住而保留的字段（${(inv.KeptPinned || []).length}）</div>
    <div class="hint">${(inv.KeptPinned || []).length
      ? '你勾选了「顶住」的字段<b>不会被清空</b> —— 顶住 = 这个值我认可了，不要动。'
      : '本次没有被顶住的字段。'}
      ${(inv.KeptPinned || []).length ? `<div style="margin-top:6px">${inv.KeptPinned.map(k => `<span class="tag tag--warning">${esc(k.Field)}</span>`).join(' ')}</div>` : ''}</div>
    <div class="gtitle">③ 未受影响的文档（${inv.SkippedLocked || 0}）—— 因为已锁定</div>
    <div class="hint">${inv.SkippedLocked ? `已锁定 ${inv.SkippedLocked} 个文档，即使企业文件变化也<b>不改变其内容</b>。` : '本次没有已锁定文档被跳过。'}</div>
    <div class="gtitle">④ 规则说明</div>
    <table class="tbl"><thead><tr><th style="width:140px">字段来源</th><th style="width:100px">是否依赖企业文件</th><th>企业文件变更后</th></tr></thead><tbody>
      <tr><td><span class="tag tag--gray">人工填写</span></td><td>否</td><td><b>保留</b>，且自动顶住</td></tr>
      <tr><td><span class="tag tag--info">全局参数</span></td><td>否</td><td><b>保留</b>，自动顶住</td></tr>
      <tr><td><span class="tag tag--info">计算/统计</span></td><td>否</td><td><b>保留</b>，自动顶住</td></tr>
      <tr><td><span class="tag tag--info">企业属性</span></td><td>否</td><td><b>保留</b>，自动顶住</td></tr>
      <tr><td><span class="tag tag--warning">企业资料</span></td><td><b>是</b></td><td>⛔ <b>清空</b>待重填（<b>已顶住则保留</b>）</td></tr>
      <tr><td><span class="tag tag--warning">AI 推断</span></td><td><b>是</b></td><td>⛔ <b>清空</b>待重填（<b>已顶住则保留</b>）</td></tr>
    </tbody></table>
    <div class="hint" style="margin-top:10px">★ <b>你顶住的字段不会被清空</b> —— 顶住 = 「这个值我认可了，不要动」。</div>`;
  showDrawer('dep');
}

/* ======================================================================
   ★ v6 · 页面 A：文件夹页面（★ 类型独立成列 · 表格不换行）
   ====================================================================== */
function renderFolderCard(sc) {
  $('filePage').style.display = 'none';
  $('folderCard').style.display = '';
  if (!sc.rows.length) { $('tbody').innerHTML = '<tr><td colspan="9" class="muted" style="padding:24px;text-align:center">该范围为空</td></tr>'; return; }

  const isFolderLayer = !!sc.rows[0]._isFolder;
  const rows = filtered(sc.rows);
  let h = '';
  if (isFolderLayer) {
    rows.forEach(r => {
      const c = folderStats(r.Code);
      const on = S.sel.indexOf(r.Code) >= 0;
      const iss = diagnoseFolder(r.Code);
      h += `<tr class="${on ? 'is-sel' : ''}">
        <td><input type="checkbox" ${on ? 'checked' : ''} onclick="event.stopPropagation();toggleSel('${r.Code}')"></td>
        <td>▣</td>
        <td><span class="tag tag--gray">文件夹</span></td>
        <td><span class="tbl__name" onclick="pickFolder('${r.Code}')">${esc(r.Name)}</span></td>
        <td class="nowrap small">${c.locked ? `<span class="tag tag--warning">🔒${c.locked}</span> ` : ''}${c.empty ? `<span class="tag tag--gray">○${c.empty}</span> ` : ''}${c.review ? `<span class="tag tag--info">◔${c.review}</span>` : ''}</td>
        <td class="small muted">${c.total} 个文件</td>
        <td class="small muted">—</td>
        <td>${remarkCell(iss)}</td>
        <td class="nowrap"><button class="btn btn--sm" onclick="event.stopPropagation();pickFolder('${r.Code}')">打开</button></td></tr>`;
    });
  } else {
    rows.forEach(r => {
      const st = docState(r), meta = STATE_META[st];
      const on = S.sel.indexOf(r.Code) >= 0;
      const e = S.docs[r.Code], c = doneRatio(r);
      const iss = diagnose(r);
      /* ★ 类型独立成列 */
      const typeTag = r.Cat === 'fixed' ? '<span class="tag tag--info">固定</span>'
        : r.Cat === 'platform_generated' ? '<span class="tag tag--gray">平台</span>'
        : '<span class="tag tag--warning">可编辑</span>';
      const prog = r.Cat === 'fixed'
        ? (S.picked[r.Code]?.Picked ? '<span class="small">已接收</span>' : '<span class="small muted">—</span>')
        : (e && e.AnchorTotal === 0 ? '<span class="small muted">无锚点</span>'
          : `<span class="pbar"><span class="pbar__fill ${c >= .9 ? '' : c >= .5 ? 'is-mid' : 'is-low'}" style="width:${Math.round(c * 100)}%"></span></span><span class="small">${pct(c)}</span>`);
      const conf = r.Cat === 'fixed' ? '—' : (cellsOf(r).length ? `<span class="small">${pct(avgConf(r))}</span>` : '—');
      const lockBtn = st === 'locked'
        ? `<button class="btn btn--sm" onclick="event.stopPropagation();toggleLock('${r.Code}',false)">解锁</button>`
        : `<button class="btn btn--sm" onclick="event.stopPropagation();toggleLock('${r.Code}',true)">锁定</button>`;
      h += `<tr class="${on ? 'is-sel' : ''}">
        <td><input type="checkbox" ${on ? 'checked' : ''} onclick="event.stopPropagation();toggleSel('${r.Code}')"></td>
        <td>${r.Cat === 'fixed' ? '▧' : r.Cat === 'platform_generated' ? '✦' : '▤'}</td>
        <td>${typeTag}</td>
        <td><span class="tbl__name" onclick="gotoFile('${r.Code}')">${esc(r.Name)}</span></td>
        <td><span class="tag ${meta.cls}" title="${meta.tip}">${meta.icon} ${meta.label}</span></td>
        <td class="nowrap">${prog}</td>
        <td class="small">${conf}</td>
        <td>${remarkCell(iss)}</td>
        <td class="nowrap">
          <button class="btn btn--sm" onclick="event.stopPropagation();gotoFile('${r.Code}')">${r.Cat === 'fixed' ? '选定' : '打开'}</button>
          ${lockBtn}
        </td></tr>`;
    });
  }
  $('tbody').innerHTML = h || '<tr><td colspan="9" class="muted" style="padding:24px;text-align:center">无匹配</td></tr>';
  $('chkAll').checked = rows.length > 0 && S.sel.length === rows.length;
}
/** ★ 备注列：单行不换行，超出用省略号（完整内容在 title 与底部问题区） */
function remarkCell(issues) {
  if (!issues || !issues.length) return '<span class="remark__none">—</span>';
  const txt = issues.map(i => (i.n > 1 ? i.n + ' ' : '') + i.t.replace(/^\d+ /, '')).join('；');
  const lv = issues[0].lv;
  const show = issues.slice(0, 1).map(i => `${i.n > 1 ? i.n + ' ' : ''}${esc(i.t.replace(/^\d+ /, ''))}`).join('');
  const more = issues.length > 1 ? ` <span class="remark__t remark__t--gray">+${issues.length - 1}</span>` : '';
  return `<div class="remark" title="${esc(txt)}">
    <span class="remark__t remark__t--${lv === 'danger' ? 'danger' : lv === 'warn' ? 'warn' : 'info'}">${show}</span>${more}</div>`;
}

function filtered(rows) {
  let r = rows;
  const f = S.filter;
  if (f === 'locked') r = r.filter(x => x._isFolder ? folderStats(x.Code).locked > 0 : docState(x) === 'locked');
  else if (f === 'empty')  r = r.filter(x => x._isFolder ? folderStats(x.Code).empty > 0 : docState(x) === 'empty');
  else if (f === 'review') r = r.filter(x => x._isFolder ? folderStats(x.Code).review > 0 : docState(x) === 'review');
  else if (f === 'need')   r = r.filter(x => !x._isFolder && x.Cat === 'fixed' && S.picked[x.Code]?.State === 'need_confirm');
  else if (f === 'pending')r = r.filter(x => !x._isFolder && S.docs[x.Code] && S.docs[x.Code].Status === 'partial');
  else if (f === 'inval')  r = r.filter(x => !x._isFolder && (MOCK.invalidated?.Cleared || []).some(y => y.Doc === x.Code));
  return r;
}
function toggleSel(c) { const i = S.sel.indexOf(c); if (i >= 0) S.sel.splice(i, 1); else S.sel.push(c); render(); }
function toggleAll(v) { S.sel = v ? filtered(scope().rows).map(r => r.Code) : []; render(); }
function showDeps(code) {
  const dfs = depFiles(code);
  const cells = (S.docs[code] || {}).Cells || [];
  $('depBody').innerHTML = `<div class="alert alert--info">本文件的内容填写<b>涉及到 ${dfs.length} 份企业文件</b>。
    企业重新上传其中任何一份时，依赖它的字段会被清空（除非已顶住或来源与文件无关）。</div>
    <table class="tbl"><thead><tr><th>企业文件</th><th style="width:70px">版本</th><th style="width:110px">上传日期</th><th>本文件用它的哪些字段</th></tr></thead><tbody>
    ${dfs.map(code2 => {
      const raw = MOCK.rawFiles.find(x => x.Code === code2);
      const fs = depsOf(code).filter(d => d.From === code2).map(d => d.Field);
      return `<tr><td>${esc(raw?.Name || code2)}</td><td class="small">v${raw?.Ver || 1}</td><td class="small muted">${esc(raw?.Updated || '—')}</td>
        <td class="small">${fs.length ? fs.map(f => `<span class="tag tag--gray">${esc(f)}</span>`).join(' ') : '<span class="muted">（整份文件作为来源）</span>'}</td></tr>`;
    }).join('')}</tbody></table>
    <div class="gtitle">本文件 ${cells.length} 个字段的顶住状态</div>
    <div class="hint">🔒 自动 = 来源与企业文件无关（人工填写 / 全局参数 / 计算）　🔒 人工 = 你认可了推断结果　○ 未顶住 = 依赖企业文件</div>
    <table class="tbl"><thead><tr><th style="width:150px">字段</th><th style="width:100px">来源</th><th>顶住</th></tr></thead><tbody>
    ${cells.map(c => { const ps = pinSource(c); return `<tr><td class="mono small">${esc(c.Field)}</td>
      <td><span class="tag ${(SRC_KIND[c.Kind] || {}).tag}">${(SRC_KIND[c.Kind] || {}).label || c.Kind}</span></td>
      <td>${ps === 'manual' ? '<span class="tag tag--warning">🔒 人工认可</span>' : ps === 'auto' ? '<span class="tag tag--gray">🔒 自动</span>' : '<span class="muted small">○ 未顶住</span>'}</td></tr>`; }).join('')}
    </tbody></table>`;
  showDrawer('dep');
}

/* ======================================================================
   ★ v4 · 页面 B：文件页面（整页替换）
   两个 tab：预览（看现在填写的信息）· 审核（查来源 / 编辑 / 顶住）
   ====================================================================== */
function renderFilePage(r) {
  if (!r) return;
  $('folderCard').style.display = 'none';
  $('filePage').style.display = '';
  S.fileList = (folderNode(r._folder, S.stage, S.std) || findFolderAnywhere(r._folder) || { Files: [] }).Files.map(enrich);

  const e = S.docs[r.Code], p = S.picked[r.Code], st = docState(r), meta = STATE_META[st];
  const isFixed = r.Cat === 'fixed';

  let h = `<div class="card"><div class="card__body row" style="padding:var(--yzh-space-3)">
      <span class="tag ${meta.cls}">${meta.icon} ${meta.label}</span>
      <b style="font-size:var(--yzh-font-size-lg)">${esc(r.Name)}</b>
      ${isFixed ? '<span class="tag tag--info">固定文档</span>' : (e && e.AnchorTotal ? `<span class="tag tag--gray">${e.FilledCount}/${e.AnchorTotal} 已填 · 可信度 ${pct(avgConf(r))}</span>` : '')}
    </div></div>
    <div class="tabs">
      <div class="tab ${S.fdTab === 0 ? 'is-active' : ''}" onclick="S.fdTab=0;render()">预览</div>
      <div class="tab ${S.fdTab === 1 ? 'is-active' : ''}" onclick="S.fdTab=1;render()">${isFixed ? '选定原件' : '审核'}</div>
    </div>`;

  if (isFixed) {
    h += (S.fdTab === 0 ? fixedPreviewTab(r, p) : fixedIdentifyTab(r, p));
  } else {
    h += (S.fdTab === 0 ? previewTab(r, e) : auditTab(r, e));
  }
  $('filePage').innerHTML = h;
}

/* ---------- Tab0 预览：看现在填写的信息 ---------- */
function previewTab(r, e) {
  if (!e || e.Status === 'none') {
    return `<div class="card"><div class="card__body"><div class="alert alert--info" style="margin:0">
      尚未规范化。执行后会在这里显示生成的文件。</div></div></div>`;
  }
  const mock = MOCK.pdfMock[r.Code];
  const nPages = mock ? mock.pages : (e.PdfPages || 1);
  const pdfBody = mock
    ? `<div class="pdf__title">${esc(mock.title)}</div><div class="pdf__sub">${esc(mock.sub)}</div>
       ${mock.body.map(b => b.includes('　')
         ? `<div class="pdf__kv"><span class="pdf__k">${esc(b.split('　')[0])}</span><span class="pdf__v pdf__hl">${esc(b.split('　').slice(1).join('　'))}</span></div>`
         : `<div class="pdf__p">${esc(b)}</div>`).join('')}`
    : `<div class="pdf__title">${esc(e.Template)}</div><div class="pdf__sub">（预览）</div>
       <div class="pdf__p muted">此文档无预览样例，但已生成 ${e.AnchorTotal} 个锚点的内容。</div>`;
  return `<div class="card"><div class="card__body">
      <div class="row" style="margin-bottom:10px">
        <span class="small muted">源路径 enterprise-documents/…/${esc(e.Template)}.pdf</span><span class="grow"></span>
        <button class="btn btn--sm" onclick="downloadDoc('${r.Code}')">⬇ 下载</button>
        <button class="btn btn--sm" onclick="switchSibling(1)">下一个文件 ›</button>
      </div>
      <div class="pdf"><div class="pdf__bar"><span>第 <b>1</b> / ${nPages} 页</span><span class="grow"></span>
        <button class="btn btn--sm">‹</button><button class="btn btn--sm">›</button></div>
        <div class="pdf__page">${pdfBody}<div class="pdf__note">黄色高亮 = 由平台按规则填入</div></div></div>
    </div></div>`;
}

/* ---------- Tab1 审核：字段表 + 溯源 + 编辑 + 顶住（★ 审计合并在此） ---------- */
function auditTab(r, e) {
  if (!e || !e.Cells || !e.Cells.length) {
    return `<div class="card"><div class="card__body"><div class="alert alert--info" style="margin:0">
      ${!e || e.Status === 'none' ? '尚未规范化，无字段可审核。'
        : (e.AnchorTotal === 0 ? '该模板无可填锚点（纯复制文档），无需审核。' : '暂无字段。')}
      </div></div></div>`;
  }
  const ed = S.edits[r.Code] || {};
  const filled = e.Cells.filter(c => c.Kind !== 'pending').length;
  const pinnedN = e.Cells.filter(c => isPinned(c)).length;
  const clearedN = e.Cells.filter(c => c.Invalidated).length;
  const allPinned = e.Cells.filter(c => c.Kind !== 'pending').length > 0
    && e.Cells.filter(c => c.Kind !== 'pending').every(c => isPinned(c));

  let h = `<div class="card">
    <div class="card__body row" style="padding:var(--yzh-space-3)">
      <span class="tag tag--gray">${filled}/${e.AnchorTotal} 已填</span>
      <span class="tag tag--gray">可信度 ${pct(avgConf(r))}</span>
      <span class="tag ${pinnedN ? 'tag--warning' : 'tag--gray'}">🔒 ${pinnedN} 个已顶住</span>
      ${clearedN ? `<span class="tag tag--danger">${clearedN} 个待重填</span>` : ''}
      ${countEdits(r.Code) ? `<span class="tag tag--info">${countEdits(r.Code)} 处改动未提交</span>` : ''}
      <span class="grow"></span>
      <span class="small muted">★ 勾选 = 认可这个值，之后企业资料更新不清空它（表头可全选）</span>
    </div>
    <table class="tbl">
      <thead><tr>
        <th style="width:34px" title="勾选=认可这个值（顶住），企业资料更新时不清空"><input type="checkbox" id="pinAll" ${allPinned?'checked':''} onclick="bulkPin('${r.Code}',this.checked)"></th>
        <th style="width:128px">位置</th><th style="width:106px">字段</th>
        <th style="width:auto;min-width:200px">值（可直接改）</th>
        <th style="width:92px">来源</th><th style="width:58px">可信度</th>
        <th style="width:170px">从企业资料筛选</th><th style="width:52px">操作</th>
      </tr></thead><tbody>`;
  e.Cells.forEach(c => {
    const ov = ed[c.Ref];
    const val = ov ? ov.val : c.Val;
    const kind = ov ? ov.srcKind : c.Kind;
    const sk = SRC_KIND[kind] || SRC_KIND.manual;
    const ps = pinSource(c);
    const pin = isPinned(c);
    const edited = ov && ov.val !== ov.from;
    const cands = rawCandidates(c.Field);
    const pinTip = ps === 'manual' ? '你已认可（企业资料更新不影响）'
      : ps === 'auto'   ? '来源与企业资料无关，自动顶住'
      : '未顶住：企业资料更新会清空它';
    h += `<tr>
      <td title="${pinTip}"><input type="checkbox" ${pin ? 'checked' : ''} ${c.Kind === 'pending' ? 'disabled' : ''}
            onchange="togglePin('${r.Code}','${esc(c.Ref)}',this.checked)"></td>
      <td class="mono small">${esc(c.Loc)}${c.Invalidated ? '<div><span class="tag tag--danger tiny">已清空</span></div>' : ''}</td>
      <td class="mono small">${esc(c.Field)}</td>
      <td><input class="inp ${edited ? 'is-edited' : ''}" value="${esc(val)}" ${c.Kind === 'pending' ? 'placeholder="待填写…"' : ''}
            oninput="editCell('${r.Code}','${esc(c.Ref)}',this.value,'${kind}')"></td>
      <td><span class="tag ${sk.tag}">${sk.label}</span></td>
      <td class="small">${c.Kind === 'pending' ? '—' : pct(c.Conf)}</td>
      <td>${cands.length ? `<select class="inp" onchange="pickFromRaw('${r.Code}','${esc(c.Ref)}',this)">
          <option value="">— ${cands.length} 份 —</option>
          ${cands.map(x => `<option value="${esc(x.v)}" ${val === x.v ? 'selected' : ''}>${esc(x.f.slice(0,14))}：${esc(x.v.slice(0,14))}</option>`).join('')}
        </select>` : '<span class="small muted">—</span>'}</td>
      <td><span class="ops__b" title="查看来源与证据" onclick="showSource('${r.Code}','${esc(c.Ref)}')">查</span></td></tr>`;
  });

  /* 待办 */
  const todo = e.Cells.filter(c => c.Kind === 'pending');
  if (todo.length) {
    h += `<div class="card"><div class="card__body">
      <b>${todo.length} 个字段还没填</b>
      <span class="small muted">直接在上面补填即可；或到「企业全局参数定义」补值后重新生成。</span>
      <div style="margin-top:6px">${todo.map(c => `<span class="tag tag--gray">${esc(c.Field)}</span>`).join(' ')}</div>
      </div></div>`;
  }
  return h;
}

/* ---------- 固定文档：Tab0 预览（原件） ---------- */
function fixedPreviewTab(r, p) {
  const mock = MOCK.pdfMock[r.Code];
  const c = (MOCK.candidates[r.Code] || []).find(x => x.FileCode === p?.Picked);
  const body = mock
    ? `<div class="pdf__title">${esc(mock.title)}</div><div class="pdf__sub">${esc(mock.sub)}</div>
       ${mock.body.map(b => b.includes('　')
         ? `<div class="pdf__kv"><span class="pdf__k">${esc(b.split('　')[0])}</span><span class="pdf__v pdf__hl">${esc(b.split('　').slice(1).join('　'))}</span></div>`
         : `<div class="pdf__p">${esc(b)}</div>`).join('')}`
    : `<div class="pdf__title">${esc(r.Name)}</div><div class="pdf__sub">（未接收原件）</div>
       <div class="pdf__p muted">选定原件后，这里显示该原件的 PDF。</div>`;
  return `<div class="card"><div class="card__body">
    <div class="row" style="margin-bottom:10px">
      ${p?.Picked ? `<span class="tag tag--success">已接收：${esc(c?.FileName || p.Picked)}</span>`
                  : '<span class="tag tag--danger">未选定原件</span>'}
      <span class="grow"></span>
      <button class="btn btn--sm" onclick="downloadDoc('${r.Code}')">⬇ 下载</button>
    </div>
    <div class="pdf"><div class="pdf__bar"><span>原件预览</span></div>
      <div class="pdf__page">${body}</div></div>
  </div></div>`;
}

/* ---------- 固定文档：Tab1 原件识别（★ 候选 + 人工指定） ---------- */
function fixedIdentifyTab(r, p) {
  const cands = MOCK.candidates[r.Code] || [];
  if (!cands.length) {
    return `<div class="card"><div class="card__body"><div class="alert alert--danger" style="margin:0">
      企业原始资料里没有可匹配的文件。请到顶部菜单「<b>企业资料管理</b>」上传，或标记「企业暂无此件」。</div></div></div>`;
  }
  let h = '';
  if (cands.length > 1) {
    h += `<div class="alert alert--warn">企业上传了 <b>${cands.length} 份</b>同类文件，系统<b>无法判断哪一份是正确的</b>。
      <b>点候选卡片即选中并替换</b>，无需再确认。
      <span class="mono small">CandidateCount=${cands.length} · Top1Score=${cands[0].Score.toFixed(2)}</span></div>`;
  } else {
    h += `<div class="alert alert--info">只有 <b>1 份</b>候选，已自动选定，可点其它候选替换。</div>`;
  }
  if (p?.Picked) {
    const c = cands.find(x => x.FileCode === p.Picked);
    h += `<div class="card"><div class="card__head">✓ 当前原件（${pct(c?.Score || 0)}）<span class="grow"></span>
      <button class="btn btn--sm" onclick="unpick('${r.Code}')">取消选定</button></div>
      <div class="card__body row"><div class="cand__thumb">${esc(c?.FileType || '')}</div>
      <div class="grow"><b>${esc(c?.FileName || '')}</b><div class="small muted">${esc(c?.MatchedBy || '')}</div>
      <div class="cand__evid" style="margin-top:4px">${c?.Evidence || ''}</div></div></div></div>`;
  }
  h += `<div class="card"><div class="card__head">候选文件（${cands.length}）<span class="grow"></span>
    <span class="small muted">按匹配度降序 · <b>点卡片即选中并替换原文件</b></span></div><div class="card__body">`;
  cands.forEach((c, i) => {
    const on = p?.Picked === c.FileCode;
    const lv = c.Score >= 0.9 ? 'tag--success' : c.Score >= 0.6 ? 'tag--warning' : 'tag--danger';
    h += `<div class="cand ${on ? 'is-picked' : ''}" onclick="pick('${r.Code}','${c.FileCode}')">
      <div class="cand__head"><div class="cand__thumb">${esc(c.FileType)}</div>
      <div class="grow"><div class="cand__name">${esc(c.FileName)} ${i === 0 ? '<span class="tag tag--info">Top1</span>' : ''}</div>
        <div class="small muted">命中：${esc(c.MatchField)}${c.MatchedBy ? ' · ' + esc(c.MatchedBy) : ''}</div></div>
      <span class="tag ${lv}">${pct(c.Score)}</span>
      <span class="tag ${on ? 'tag--success' : 'tag--gray'}">${on ? '✓ 当前' : '选它替换'}</span></div>
      <div class="cand__evid">${c.Evidence}</div>
      <div class="row" style="margin-top:4px"><button class="btn btn--sm" onclick="event.stopPropagation();previewRaw('${c.FileCode}')">🔍 预览原件</button>
      <button class="btn btn--sm" onclick="event.stopPropagation();markNone('${r.Code}')">企业暂无此件</button></div>
    </div>`;
  });
  h += `</div></div>`;
  return h;
}

/* ---------- 锚点溯源（★ 审核 tab 里的「查」） ---------- */
function showSource(code, ref) {
  const e = S.docs[code];
  const c = (e.Cells || []).find(x => x.Ref === ref); if (!c) return;
  const sk = SRC_KIND[c.Kind] || SRC_KIND.manual;
  const ps = pinSource(c);
  const dep = (MOCK.fieldDeps[code] || []).find(d => d.Field === c.Field || d.Field.startsWith(c.Field + '@'));
  const raw = dep ? MOCK.rawFiles.find(x => x.Code === dep.From) : null;
  $('depBody').innerHTML = `
    <div class="row" style="margin-bottom:10px"><button class="btn btn--sm" onclick="closeDrawer('dep')">‹ 返回审核</button></div>
    <div class="gtitle">字段来源 · ${esc(c.Field)}</div>
    <div class="card"><div class="card__body">
      <table class="tbl"><tbody>
        <tr><td style="width:110px" class="small muted">位置</td><td class="mono small">${esc(c.Loc)}</td></tr>
        <tr><td class="small muted">当前值</td><td>${esc(c.Val || '（空）')}${c.Invalidated ? ' <span class="tag tag--danger">因企业资料更新被清空</span>' : ''}</td></tr>
        <tr><td class="small muted">来源</td><td><span class="tag ${sk.tag}">${sk.label}</span></td></tr>
        <tr><td class="small muted">可信度</td><td class="small">${c.Kind === 'pending' ? '—' : pct(c.Conf)}　<span class="muted">（仅供参考，合格与否由你决定）</span></td></tr>
        <tr><td class="small muted">顶住</td><td>${ps === 'manual' ? '<span class="tag tag--warning">🔒 你已认可（企业资料更新不影响）</span>'
          : ps === 'auto' ? '<span class="tag tag--gray">🔒 自动顶住（来源与企业资料无关）</span>'
          : '<span class="muted">○ 未顶住 —— 企业资料更新会清空它</span>'}</td></tr>
        ${dep ? `<tr><td class="small muted">依赖文件</td><td class="small">${esc(raw?.Name || dep.From)} <span class="tag tag--gray">v${raw?.Ver || 1}</span>
          <button class="btn btn--link btn--sm" onclick="previewRaw('${dep.From}')">预览</button></td></tr>` : ''}
      </tbody></table>
      ${dep ? `<div class="hr"></div><div class="gtitle">证据原文</div>
        <div class="cand__evid">本次提取到：<em>${esc(c.Val || '（原值）')}</em>　来自《${esc(raw?.Name || '')}》。</div>` : ''}
      <div class="hr"></div>
      <div class="row">
        <button class="btn btn--sm ${isPinned(c) ? '' : 'btn--primary'}" onclick="togglePin('${code}','${esc(ref)}',${!isPinned(c)});closeDrawer('dep')">
          ${isPinned(c) ? '🔓 解除顶住' : '🔒 顶住（认可这个值）'}</button>
        <span class="small muted">顶住后，企业重新上传资料不会清空它</span>
      </div>
    </div></div>`;
  showDrawer('dep');
}

/* ==================== 操作 ==================== */
function switchSibling(dir) {
  if (!S.fileList || !S.fileList.length) return;
  const i = S.fileList.findIndex(x => x.Code === S.file);
  const n = S.fileList[(i + dir + S.fileList.length) % S.fileList.length];
  if (n) gotoFile(n.Code);
}
function toggleLock(code, v) {
  if (v) {
    S.locked[code] = true;
    const e = S.docs[code];
    addLog('锁定', (findFile(code) || {}).Name || code, e ? `已认可，完成度 ${pct(e.Completion)}` : '已认可该文档');
    say('ok', '已锁定：之后企业资料更新、重新生成都不会改变这份文档');
  } else {
    delete S.locked[code];
    addLog('解锁', (findFile(code) || {}).Name || code, '允许重新生成');
    say('info', '已解锁，可重新生成');
  }
  render();
}
function toggleLockSelf() { if (S.file) toggleLock(S.file, !S.locked[S.file]); }
/** ★ 更新文档：把暂存改动落笔 + 重算 + 同步资料库 */
function updateSelf() {
  const code = S.file, r = findFile(code); if (!r) return;
  const e = S.docs[code];
  if (e && e.Cells) {
    const ed = S.edits[code] || {};
    e.Cells.forEach(c => { const o = ed[c.Ref]; if (o) { c.Val = o.val; if (o.srcKind) c.Kind = o.srcKind; c.Invalidated = false; } });
    const filled = e.Cells.filter(c => c.Kind !== 'pending').length;
    e.FilledCount = filled;
    e.Completion = e.AnchorTotal ? filled / e.AnchorTotal : 0;
    e.Status = e.Completion >= 1 ? 'generated' : 'partial';
  }
  S.edits[code] = {};
  addLog('更新文档', r.Name, e ? `完成度 ${pct(e.Completion)}（已填 ${e.FilledCount}/${e.AnchorTotal}）` : '已同步到企业资料库');
  say('ok', `「${r.Name}」已更新并同步到企业资料库`);
  render();
}
function editCell(code, ref, val, kind) {
  const e = S.docs[code];
  const c = (e.Cells || []).find(x => x.Ref === ref); if (!c) return;
  S.edits[code] = S.edits[code] || {};
  S.edits[code][ref] = Object.assign({ pinned: isPinned(c) }, S.edits[code][ref], { from: c.Val, val, srcKind: kind });
}
function pickFromRaw(code, ref, sel) {
  if (!sel.value) return;
  const e = S.docs[code];
  const c = (e.Cells || []).find(x => x.Ref === ref);
  S.edits[code] = S.edits[code] || {};
  S.edits[code][ref] = Object.assign({ pinned: isPinned(c) }, S.edits[code][ref],
    { from: c.Val, val: sel.value, srcKind: 'profile', srcChanged: true, fromFile: sel.value.split('：')[0] });
  say('ok', '已换来源，点「更新文档」生效');
}
function countEdits(code) {
  const ed = S.edits[code] || {}, e = S.docs[code];
  return ((e && e.Cells) || []).filter(c => ed[c.Ref] && ed[c.Ref].val !== ed[c.Ref].from).length;
}
function addLog(action, target, reason) {
  S.logs.unshift({ Time: new Date().toLocaleString('zh-CN', { hour12: false }).replace(/\//g, '-'), User: '王专家', Action: action, Target: target, Reason: reason });
}
function downloadDoc(code) { say('info', '原型：下载 enterprise-documents/… .pdf'); }
function previewRaw(code) { const f = MOCK.rawFiles.find(x => x.Code === code); say('info', `原型：预览企业原始资料 ${f?.Name || code}（只读）`); }
/* ★ v5：固定文档 —— 选中即替换原文件，不需要「更新文档」 */
function pick(code, fileCode) {
  const c = (MOCK.candidates[code] || []).find(x => x.FileCode === fileCode);
  const prev = S.picked[code]?.Picked;
  if (prev === fileCode) return;
  S.picked[code] = { Picked: fileCode, State: 'confirmed' };
  S.locked[code] = true;
  const nm = (findFile(code) || {}).Name || code;
  addLog(prev ? '替换原件' : '选定原件', nm,
    `${c?.FileName}（${c ? pct(c.Score) : ''}）${prev ? ` ← 原 ${MOCK.candidates[code].find(x => x.FileCode === prev)?.FileName || prev}` : ''}`);
  say('ok', prev ? `已替换为「${c?.FileName}」` : `已选定「${c?.FileName}」`);
  render();
}
function unpick(code) {
  S.picked[code] = { Picked: null, State: 'need_confirm', CandidateCount: (MOCK.candidates[code] || []).length };
  delete S.locked[code];
  say('info', '已取消选定'); render();
}
function markNone(code) { say('warn', '原型：标记「企业暂无」→ 写入缺口清单，不锁定'); }

/* ==================== 文件夹菜单动作 ==================== */
function doRunSel() {
  const list = S.sel.filter(c => !S.locked[c]);
  if (!list.length) return say('warn', S.sel.length ? '所选全部已锁定，需先解锁' : '请先勾选');
  const tasks = list.map(c => ({ Code: c, Doc: (findFile(c) || {}).Name, State: 'running', Ms: 0, Msg: '执行中…' }));
  S.queue = { Code: 'Q-20261004-' + (1000 + Math.floor(Math.random() * 8999)), State: 'running', Tasks: tasks };
  say('info', `已入队 ${list.length} 个文件任务`); render(); step(0);
}
function step(i) {
  const q = S.queue; if (!q || q.State !== 'running') return;
  if (i >= q.Tasks.length) {
    q.State = 'done'; addLog('批量规范化', `${q.Tasks.length} 个文档`, '文件夹勾选执行');
    say('ok', `完成 ${q.Tasks.length} 个`); render(); return;
  }
  const t = q.Tasks[i];
  setTimeout(() => {
    const r = findFile(t.Code), e = S.docs[t.Code], p = S.picked[t.Code];
    t.Ms = 400 + Math.round(Math.random() * 2600);
    if (r.Cat === 'fixed') {
      if (!p) { t.State = 'failed'; t.Msg = '企业资料中无可匹配文件'; }
      else if (p.State === 'need_confirm') { t.State = 'success'; t.Msg = `${p.CandidateCount} 个候选，待人工指定`; }
      else { t.State = 'success'; t.Msg = '已按选定原件接收'; }
    } else if (e && e.Status !== 'none') { t.State = 'success'; t.Msg = `${e.FilledCount}/${e.AnchorTotal} 已填`; }
    else { t.State = 'failed'; t.Msg = '模板未上传'; }
    render(); step(i + 1);
  }, 300 + Math.random() * 200);
}
/** ★ 重新生成（勾选）：自动解除锁定，但必须二次确认并列出将解锁的文件 */
function rewriteSel() {
  if (!S.sel.length) return say('warn', '请先勾选要重新生成的文件');
  const willUnlock = S.sel.filter(c => S.locked[c]);
  if (willUnlock.length) {
    const names = willUnlock.map(c => `· ${esc((findFile(c) || {}).Name || c)} 🔒`).join('<br>');
    if (!confirm(`勾选的 ${S.sel.length} 个文件中，${willUnlock.length} 个已锁定，将被解锁并重新生成：\n\n${names}\n\n解锁后，这些文档会按当前规则重新填充。确认？`)) return;
    willUnlock.forEach(c => delete S.locked[c]);
    addLog('批量解锁', `${willUnlock.length} 个文档`, '随重新生成自动解锁');
  }
  doRunSel();
}
function lockSel(v) {
  if (!S.sel.length) return say('warn', '请先勾选');
  S.sel.forEach(c => v ? (S.locked[c] = true) : delete S.locked[c]);
  addLog(v ? '批量锁定' : '批量解锁', `${S.sel.length} 个`, '文件夹勾选操作');
  say(v ? 'ok' : 'info', `已${v ? '锁定' : '解锁'} ${S.sel.length} 个（锁定后不受资料更新影响）`);
  S.sel = []; render();
}
function exportSel() {
  if (!S.sel.length) return say('warn', '请先勾选要导出的文件');
  say('ok', `导出 ${S.sel.length} 个文件 → ZIP（含填写说明.txt）`);
}
function renderQueue() {
  const bar = $('queueInline'), q = S.queue;
  if (!q) { if (bar) bar.innerHTML = ''; return; }
  const done = q.Tasks.filter(t => ['success', 'skipped'].includes(t.State)).length;
  const fail = q.Tasks.filter(t => t.State === 'failed').length;
  const p = Math.round(done / q.Tasks.length * 100);
  if (!bar) return;
  bar.innerHTML = `<div class="queue" style="margin-bottom:12px">
    <span class="queue__txt">${q.State === 'running' ? '⟳ 执行中' : '✓ 已完成'}</span>
    <span class="mono small">${q.Code}</span>
    <span class="small">完成 <b>${done}</b> / ${q.Tasks.length}${fail ? ` · 失败 <b>${fail}</b>` : ''}</span>
    <span class="queue__bar"><span class="queue__fill ${fail ? 'is-fail' : 'is-done'}" style="width:${p}%"></span></span>
    <button class="btn btn--sm" onclick="openQueue()">明细</button></div>`;
}
/** 重试失败项（只重试失败的文件，不动已成功的） */
function retryFailed() {
  const q = S.queue; if (!q) return say('info', '尚无队列');
  const failed = q.Tasks.filter(t => t.State === 'failed');
  if (!failed.length) return say('info', '没有失败项');
  failed.forEach(t => { t.State = 'running'; t.Msg = '重试中…'; });
  q.State = 'running';
  say('info', `正在重试 ${failed.length} 个失败项…`);
  render(); step(q.Tasks.indexOf(failed[0]));
}

function openQueue() {
  const q = S.queue; if (!q) return say('info', '尚无队列');
  $('qCode').textContent = q.Code;
  const map = { success: ['tag--success', '成功'], failed: ['tag--danger', '失败'], running: ['tag--info', '进行中'] };
  $('qBody').innerHTML = `<table class="tbl"><thead><tr><th>文件</th><th style="width:76px">状态</th><th style="width:64px">耗时</th><th>说明</th></tr></thead><tbody>
    ${q.Tasks.map(t => { const m = map[t.State] || map.running;
      return `<tr><td>${esc(t.Doc)}</td><td><span class="tag ${m[0]}">${m[1]}</span></td>
        <td class="mono small">${t.Ms ? t.Ms + 'ms' : '—'}</td><td class="small">${esc(t.Msg)}</td></tr>`; }).join('')}
  </tbody></table>`;
  showDrawer('q');
}

/* ==================== 独立一级菜单 ==================== */
function switchMenu(k) {
  S.menu = k; render();
  if (k === 'normalize') return;
  if (k === 'log') {
    $('mTitle').textContent = '规范化日志';
    $('mBody').innerHTML = `<table class="tbl"><thead><tr><th style="width:150px">时间</th><th style="width:90px">操作</th><th>对象</th><th>说明</th></tr></thead><tbody>
      ${S.logs.map(l => `<tr><td class="mono small">${esc(l.Time)}</td><td><span class="tag tag--info">${esc(l.Action)}</span></td>
        <td class="small">${esc(l.Target)}</td><td class="small muted">${esc(l.Reason)}</td></tr>`).join('')}</tbody></table>`;
  }
  if (k === 'resource') {
    $('mTitle').textContent = '企业资料管理（只读预览）';
    $('mBody').innerHTML = `<div class="alert alert--info">原始资料的上传/替换/版本由本菜单承载；本页只<b>只读消费</b>。</div>
      <table class="tbl"><thead><tr><th>文件名</th><th style="width:70px">版本</th><th style="width:100px">上传日期</th><th style="width:160px">被哪些标准文件引用</th></tr></thead><tbody>
      ${MOCK.rawFiles.map(f => {
        const used = Object.keys(S.picked).filter(k2 => S.picked[k2].Picked === f.Code).map(k2 => (findFile(k2) || {}).Name).filter(Boolean);
        return `<tr><td>${esc(f.Name)}</td><td class="small">v${f.Ver}</td><td class="small muted">${esc(f.Updated)}</td>
          <td class="small">${used.length ? used.map(u => `<span class="tag tag--success">${esc(u)}</span>`).join(' ') : '<span class="muted">未被引用</span>'}</td></tr>`;
      }).join('')}</tbody></table>`;
  }
  showDrawer('m');
}
function renderSubMenu() {
  $('menu').innerHTML = MENUS.map(m => `<span class="menu__i ${S.menu === m.k ? 'is-active' : ''}" onclick="switchMenu('${m.k}')">${m.t}</span>`).join('');
  $('mainBox').style.display = 'none';
}
function showHelp() {
  $('hBody').innerHTML = `
  <div class="alert alert--info"><b>v5 设计</b></div>
  <div class="col">
    <div class="card"><div class="card__head">① 页面只有两处：顶部面包屑 + 底部操作区</div><div class="card__body small">
      没有统计卡、没有顶部工具条、没有提示条。<br>
      <b>底部左侧 = 本页存在的问题</b>（点条目直接跳到要处理的文件）<br>
      <b>底部右侧 = 操作按钮</b>（随页面类型变化）</div></div>
    <div class="card"><div class="card__head">② 表格有「备注」列</div><div class="card__body small">
      每行直接标出「这个文件可能有什么问题」，不用点进去才知道。</div></div>
    <div class="card"><div class="card__head">③ 固定文档：选中即替换</div><div class="card__body small">
      点候选卡片 → 立刻替换原文件并锁定，<b>没有「更新文档」按钮</b>。</div></div>
    <div class="card"><div class="card__head">④ 可编辑文档：预览 / 审核 两 tab</div><div class="card__body small">
      预览 = 看成品；审核 = 字段表（可直接改 / 从企业资料筛 / 顶住）。<br>
      <b>审计已合并进审核</b>（查来源时就看到了）。</div></div>
    <div class="card"><div class="card__head">⑤ 三态 + 顶住</div><div class="card__body small">
      锁定 / 为空 / 待审。<b>合格只由「锁定」决定</b>，与可信度无关。<br>
      顶住 = 不受企业文件更新影响。人工填写自动顶住；AI 推断的需你勾选认可。</div></div>
    <div class="card"><div class="card__head">⑥ 依赖失效不一刀切</div><div class="card__body small">
      企业重新上传 → 只清空 <b>profile/ai</b> 来源；顶住过的保留；锁定文档完全不动。</div></div>
  </div>
  <div class="hint" style="margin-top:10px"><b>建议路径</b>：0 基础资料 → 法人身份证 → 选定原件 tab → 点一个候选 → 回文件夹 → 风险管理报告 → 审核 tab → 改值/顶住 → 锁定 → 顶栏「⚡ 模拟资料更新」看依赖失效。</div>`;
  showDrawer('h');
}

/* ==================== 启动 ==================== */
document.addEventListener('DOMContentLoaded', () => {
  S.open.en = true;
  S.open['st:S_RE'] = true;
  S.open['sd:S_RE:ISO9001'] = true;
  S.open['fo:F0'] = true;
  S.folder = 'F0'; S.page = 'folder';
  render();
});
