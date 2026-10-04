/* ==========================================================================
   原型 42 · 企业资料规范化 · 交互逻辑
   零依赖，直接双击 index.html 打开
   ========================================================================== */

/* ---------------- 运行时状态（可修改，用于演示） ---------------- */
const S = {
  ent: 'E001',
  std: 'ISO9001',
  stage: 'S_RE',
  folder: null,          // null = 该阶段全部目录
  open: {},              // 树展开态
  sel: [],               // 勾选的文档 Code
  tab: 'fixed',          // fixed | edit | all
  filter: 'all',
  locked: {},            // Code -> true
  queue: null,           // 当前批次
  editing: null,         // 正在编辑的文档 Code
  edTab: 0,
  edits: {},             // Code -> { cellRef: {val, srcKind, pinned, edited} }
  picked: JSON.parse(JSON.stringify(MOCK.fixedDocs)),   // 固定文档匹配结果（可改）
  docs:  JSON.parse(JSON.stringify(MOCK.editDocs)),     // 可编辑文档状态（可改）
  logs:  MOCK.actions.slice(),
  toast: null
};

/* ---------------- 工具 ---------------- */
const $ = id => document.getElementById(id);
const esc = s => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;' }[c]));
const pct = v => Math.round(v * 100) + '%';
function say(kind, text) {
  S.toast = { kind, text, t: Date.now() };
  render();
}

/** 可信度 → 三档（阈值 0.95 / 0.70，见 41-01 §三·3） */
function confLevel(c) {
  if (c >= 0.95) return { k: 'hi', t: '高', cls: 'tag--success' };
  if (c >= 0.70) return { k: 'mid', t: '中', cls: 'tag--warning' };
  return { k: 'lo', t: '低', cls: 'tag--danger' };
}
function confDots(c) {
  const l = confLevel(c);
  return `<span class="dot dot--${l.k}"></span><span class="dot dot--${l.k}"></span><span class="dot dot--${c >= 0.7 ? l.k : 'none'}"></span> <span class="small">${l.t} ${Math.round(c * 100)}%</span>`;
}

/* ---------------- 左树 ---------------- */
function treeOf(ent) { return MOCK.tree[ent] || []; }

function renderTree() {
  let h = '';
  treeOf(S.ent).forEach(std => {
    const sok = S.open['std:' + std.Code];
    const hasKids = std.children && std.children.length;
    h += `<div class="tnode ${S.std === std.Code ? 'is-active' : ''}" onclick="pickStd('${std.Code}')">
      <span class="tnode__caret" onclick="event.stopPropagation();tgl('std:${std.Code}')">${hasKids ? (sok ? '▼' : '▶') : ''}</span>
      <span class="tnode__icon">▤</span><span class="tnode__label">${esc(std.Name)}</span>
      <span class="tnode__badge muted small">${hasKids ? std.children.length : 0}</span></div>`;
    if (!sok || !hasKids) return;
    std.children.forEach(st => {
      const sk = S.open['st:' + st.Code];
      h += `<div class="tnode ${S.stage === st.Code ? 'is-active' : ''}" style="padding-left:20px" onclick="pickStage('${st.Code}')">
        <span class="tnode__caret" onclick="event.stopPropagation();tgl('st:${st.Code}')">${st.children.length ? (sk ? '▼' : '▶') : ''}</span>
        <span class="tnode__icon">◈</span><span class="tnode__label">${esc(st.Name)}</span></div>`;
      if (!sk) return;
      st.children.forEach(f => {
        const fk = S.open['fo:' + f.Code];
        const rows = docsOfStage(S.stage).filter(d => d.folder === f.Code);
        const done = rows.filter(d => doneRatio(d) === 1).length;
        h += `<div class="tnode ${S.folder === f.Code ? 'is-active' : ''}" style="padding-left:38px" onclick="pickFolder('${f.Code}')">
          <span class="tnode__caret" onclick="event.stopPropagation();tgl('fo:${f.Code}')">${rows.length ? (fk ? '▼' : '▶') : ''}</span>
          <span class="tnode__icon">▣</span><span class="tnode__label">${esc(f.Name)}</span>
          <span class="tnode__badge">${done === rows.length && rows.length
            ? `<span class="tag tag--success">${done}/${rows.length}</span>`
            : `<span class="small muted">${done}/${rows.length}</span>`}</span></div>`;
        if (!fk) return;
        rows.forEach(d => {
          const r = doneRatio(d);
          h += `<div class="tnode ${S.editing === d.code ? 'is-active' : ''}" style="padding-left:56px" onclick="openDoc('${d.code}')">
            <span class="tnode__caret"></span>
            <span class="tnode__icon">${d.cat === 'fixed' ? '▧' : '▤'}</span>
            <span class="tnode__label">${esc(d.name)}</span>
            <span class="tnode__badge">${S.locked[d.code] ? '<span class="tag tag--warning">🔒</span>' : badgeMini(d, r)}</span></div>`;
        });
      });
    });
  });
  $('tree').innerHTML = h || '<div class="hint" style="margin:12px">该企业暂无资源</div>';
}

function badgeMini(d, r) {
  if (d.cat === 'fixed') {
    const p = S.picked[d.code];
    if (!p) return '<span class="tag tag--gray">—</span>';
    if (p.State === 'confirmed') return '<span class="tag tag--success">已确认</span>';
    if (p.State === 'warn') return '<span class="tag tag--warning">告警</span>';
    if (p.State === 'need_confirm') return `<span class="tag tag--danger">待选 ${p.CandidateCount}</span>`;
    return '<span class="tag tag--info">已生成</span>';
  }
  if (r === 1) return '<span class="tag tag--success">100%</span>';
  if (r > 0) return `<span class="tag tag--warning">${pct(r)}</span>`;
  return '<span class="small muted">未生成</span>';
}

function tgl(k) { S.open[k] = !S.open[k]; render(); }
function expandAll(v) {
  treeOf(S.ent).forEach(s => {
    S.open['std:' + s.Code] = v;
    (s.children || []).forEach(st => {
      S.open['st:' + st.Code] = v;
      (st.children || []).forEach(f => { S.open['fo:' + f.Code] = v; });
    });
  });
  render();
}
function pickStd(c) { S.std = c; S.stage = (treeOf(S.ent).find(x => x.Code === c) || { children: [] }).children[0]?.Code || null; S.folder = null; S.sel = []; S.tab = 'fixed'; S.open['std:' + c] = true; render(); }
function pickStage(c) { S.stage = c; S.folder = null; S.sel = []; S.tab = 'fixed'; S.open['st:' + c] = true; render(); }
function pickFolder(c) { S.folder = S.folder === c ? null : c; S.sel = []; S.open['fo:' + c] = true; render(); }

/* ---------------- 取当前范围文档 ---------------- */
function docsOfStage(stage) {
  const std = treeOf(S.ent).find(x => x.Code === S.std) || { children: [] };
  const st = (std.children || []).find(x => x.Code === stage);
  if (!st) return [];
  let rows = [];
  (st.children || []).forEach(f => (f.children || []).forEach(fl => {
    rows.push({ code: fl.Code, name: fl.Name, cat: fl.DocCategory, folder: f.Code, folderName: f.Name });
  }));
  return rows;
}
function currentDocs() {
  let rows = docsOfStage(S.stage);
  if (S.folder) rows = rows.filter(r => r.folder === S.folder);
  return rows;
}
function doneRatio(d) {
  if (S.locked[d.code]) return 1;
  if (d.cat === 'fixed') { const p = S.picked[d.code]; return p && p.State === 'confirmed' ? 1 : (p ? 0.5 : 0); }
  const e = S.docs[d.code];
  if (!e) return 0;
  if (e.Status === 'platform_generated') return 1;
  return e.AnchorTotal === 0 ? (e.Status === 'generated' ? 1 : 0) : e.Completion;
}

/* ---------------- 顶栏 / 页签 / 表格 ---------------- */
function render() {
  const ent = MOCK.enterprises.find(e => e.Code === S.ent);
  const std = treeOf(S.ent).find(x => x.Code === S.std);
  const st = (std?.children || []).find(x => x.Code === S.stage);
  $('crumb').textContent = `${ent?.Name || ''} · ${std?.Name || ''} · ${st?.Name || ''}${S.folder ? ' · ' + (st.children.find(f => f.Code === S.folder)?.Name || '') : ''}`;
  $('scopeTitle').innerHTML = `<b style="font-size:var(--yzh-font-size-lg)">${esc(st?.Name || '—')}</b> <span class="muted small">资源范围</span>`;

  const all = currentDocs();
  const fixed = all.filter(r => r.cat === 'fixed');
  const edit = all.filter(r => r.cat !== 'fixed');
  const needFix = fixed.filter(r => S.picked[r.code]?.State === 'need_confirm').length;

  $('tabs').innerHTML = [
    t('fixed', `固定文档 <span class="small muted">${fixed.length}</span>`, ''),
    t('edit', `可编辑文档 <span class="small muted">${edit.length}</span>`, ''),
    t('all', `全部 <span class="small muted">${all.length}</span>`, '')
  ].join('');

  renderQueue();
  renderAlerts(all, needFix);
  renderTable(all);
  renderRaw(all);
  renderLog();
  renderTree();
  if (S.toast && Date.now() - S.toast.t < 2600) {
    const box = document.createElement('div');
    box.className = 'toast toast--' + (S.toast.kind === 'ok' ? 'ok' : S.toast.kind === 'warn' ? 'warn' : 'info');
    box.textContent = S.toast.text;
    document.body.appendChild(box);
    setTimeout(() => box.remove(), 2600);
    S.toast = null;
  }
}
function t(k, label, cls) {
  return `<div class="tab ${S.tab === k ? 'is-active' : ''}" onclick="setTab('${k}')">${label}${cls}</div>`;
}
function setTab(k) { S.tab = k; render(); }

function renderAlerts(all, needFix) {
  let h = '';
  if (needFix > 0) {
    h += `<div class="alert alert--warn">
      <b>⚠ ${needFix} 个固定文档存在多个待匹配项，自动识别无法确定</b><br>
      典型场景：标准目录要求「法人身份证」，企业上传了多份身份证文件。
      系统已按规则召回候选，但无法判定哪一张是法定代表人的 —— <b>需要人工从候选中指定</b>。
      <button class="btn btn--sm btn--warn" style="margin-left:8px" onclick="setTab('fixed')">去处理</button>
    </div>`;
  }
  const lowc = all.filter(r => r.cat !== 'fixed' && minConf(r) < 0.7 && doneRatio(r) > 0);
  if (lowc.length) {
    h += `<div class="alert alert--info">
      <b>ℹ ${lowc.length} 个文档存在低可信度取值</b>（&lt; 70%）：${lowc.map(r => esc(r.name)).join('、')}
      —— 建议逐格核对后再「更新文档」。
    </div>`;
  }
  $('alertBox').innerHTML = h;
}

function filtered(all) {
  let rows = S.tab === 'fixed' ? all.filter(r => r.cat === 'fixed')
          : S.tab === 'edit'  ? all.filter(r => r.cat !== 'fixed') : all;
  const f = S.filter;
  if (f === 'need')    rows = rows.filter(r => S.picked[r.code]?.State === 'need_confirm');
  if (f === 'pending') rows = rows.filter(r => r.cat !== 'fixed' && S.docs[r.code] && S.docs[r.code].Status === 'partial');
  if (f === 'lowconf') rows = rows.filter(r => r.cat !== 'fixed' && minConf(r) < 0.7);
  if (f === 'notgen')  rows = rows.filter(r => doneRatio(r) === 0);
  if (f === 'locked')  rows = rows.filter(r => S.locked[r.code]);
  return rows;
}
function minConf(r) {
  const e = S.docs[r.code];
  if (!e || !e.Cells || !e.Cells.length) return 1;
  const cs = e.Cells.filter(c => c.Kind !== 'pending').map(c => c.Conf);
  return cs.length ? Math.min.apply(null, cs) : 1;
}
function avgConf(r) {
  const e = S.docs[r.code];
  if (!e || !e.Cells || !e.Cells.length) return 1;
  const cs = e.Cells.filter(c => c.Kind !== 'pending').map(c => c.Conf);
  return cs.length ? cs.reduce((a, b) => a + b, 0) / cs.length : 1;
}

function renderTable(all) {
  const rows = filtered(all);
  let filled = 0, need = 0;
  rows.forEach(r => {
    const c = doneRatio(r);
    if (c === 1) filled++; if (r.cat !== 'fixed' && c > 0 && c < 1) need++;
  });
  $('listSum').textContent = `${rows.length} 个文档 · 已就位 ${filled} · 部分完成 ${need}`;
  $('selInfo').textContent = S.sel.length ? `已勾选 ${S.sel.length} 个` : '未勾选';

  let h = '';
  rows.forEach(r => {
    const c = doneRatio(r);
    const lk = S.locked[r.code];
    const src = S.picked[r.code];
    const ed = S.docs[r.code];

    /* 类型列 */
    const typeTag = r.cat === 'fixed' ? '<span class="tag tag--info">固定</span>'
                  : r.cat === 'platform_generated' ? '<span class="tag tag--gray">平台生成</span>'
                  : '<span class="tag tag--warning">可编辑</span>';

    /* 完成度列 */
    let prog;
    if (r.cat === 'fixed') {
      prog = src && src.State === 'confirmed'
        ? '<span class="tag tag--success">原件已接收</span>'
        : src && src.State === 'need_confirm'
          ? '<span class="tag tag--danger">待人工指定</span>'
          : src && src.State === 'platform_generated' ? '<span class="tag tag--gray">自动派生</span>'
          : src && src.State === 'warn' ? '<span class="tag tag--warning">已就位·有告警</span>'
          : '<span class="small muted">—</span>';
    } else {
      const bar = c === 0 ? 'is-none' : c >= 0.9 ? '' : c >= 0.5 ? 'is-mid' : 'is-low';
      prog = ed && ed.AnchorTotal === 0 && ed.Status === 'generated'
        ? '<span class="small muted">无锚点·纯复制</span>'
        : `<span class="pbar"><span class="pbar__fill ${bar}" style="width:${Math.round(c * 100)}%"></span></span><span class="small">${pct(c)}</span>`;
    }

    /* 可信度列 */
    const conf = r.cat === 'fixed'
      ? (src && src.Picked ? confDots(pickConf(r.code)) : '<span class="small muted">—</span>')
      : (ed && ed.Cells && ed.Cells.length ? confDots(avgConf(r)) : '<span class="small muted">—</span>');

    /* 状态列 */
    let stt;
    if (lk) stt = '<span class="tag tag--warning">🔒 已固定</span>';
    else if (r.cat === 'fixed') {
      stt = !src ? '<span class="small muted">未就位</span>'
        : src.State === 'confirmed' ? '<span class="tag tag--success">已就位</span>'
        : src.State === 'need_confirm' ? `<span class="tag tag--danger">${src.CandidateCount} 个候选</span>`
        : src.State === 'warn' ? '<span class="tag tag--warning">已就位·告警</span>'
        : '<span class="tag tag--info">已生成</span>';
    } else if (ed && ed.Status === 'generated') stt = '<span class="tag tag--success">已生成</span>';
    else if (ed && ed.Status === 'partial') stt = '<span class="tag tag--warning">部分完成</span>';
    else if (ed && ed.Status === 'platform_generated') stt = '<span class="tag tag--info">平台生成</span>';
    else stt = '<span class="small muted">未生成</span>';

    /* 操作列 */
    let ops = '';
    if (r.cat === 'fixed') {
      ops = `<button class="btn btn--sm" onclick="event.stopPropagation();openDoc('${r.code}')">${src && src.State === 'need_confirm' ? '选定原件' : '查阅匹配'}</button>
             <button class="btn btn--sm" onclick="event.stopPropagation();previewDoc('${r.code}')">预览</button>`;
    } else {
      const has = ed && ed.Status !== 'none';
      ops = `<button class="btn btn--sm" onclick="event.stopPropagation();openDoc('${r.code}')">${has ? '查看/修改' : '规范化'}</button>
             <button class="btn btn--sm" onclick="event.stopPropagation();previewDoc('${r.code}')">预览</button>`;
    }
    if (!lk) ops += ` <button class="btn btn--sm" onclick="event.stopPropagation();lockOne('${r.code}',true)">锁定</button>`;
    else     ops += ` <button class="btn btn--sm" onclick="event.stopPropagation();lockOne('${r.code}',false)">解锁</button>`;

    h += `<tr class="${S.sel.indexOf(r.code) >= 0 ? 'is-sel' : ''}">
      <td><input type="checkbox" ${S.sel.indexOf(r.code) >= 0 ? 'checked' : ''} onclick="event.stopPropagation();toggleSel('${r.code}')"></td>
      <td><span class="tbl__name" onclick="openDoc('${r.code}')">${esc(r.name)}</span>
          <div class="tbl__sub">${esc(r.folderName)}${ed && ed.TemplateName ? ' · ' + esc(ed.TemplateName) : ''}</div></td>
      <td>${typeTag}</td><td class="nowrap">${prog}</td><td class="nowrap">${conf}</td><td>${stt}</td><td class="nowrap">${ops}</td></tr>`;
  });
  $('tbody').innerHTML = h || '<tr><td colspan="7" class="muted" style="padding:20px;text-align:center">该筛选条件下无文档</td></tr>';
  $('chkAll').checked = rows.length > 0 && S.sel.length === rows.length;
}
function pickConf(code) {
  const cands = MOCK.candidates[code] || [];
  const p = S.picked[code];
  const c = cands.find(x => x.FileCode === p?.Picked);
  return c ? c.Score : 0.5;
}
function toggleSel(code) { const i = S.sel.indexOf(code); if (i >= 0) S.sel.splice(i, 1); else S.sel.push(code); render(); }
function toggleAll(v) { S.sel = v ? filtered(currentDocs()).map(r => r.code) : []; render(); }

/* ---------------- 队列 ---------------- */
function renderQueue() {
  const q = S.queue;
  if (!q) { $('queueBox').innerHTML = ''; return; }
  const done = q.Tasks.filter(t => t.State === 'success' || t.State === 'skipped').length;
  const fail = q.Tasks.filter(t => t.State === 'failed').length;
  const run = q.Tasks.filter(t => t.State === 'running').length;
  const total = q.Tasks.length;
  const p = Math.round((done / total) * 100);
  const cls = fail ? 'is-fail' : (done === total ? 'is-done' : '');
  $('queueBox').innerHTML = `<div class="queue">
    <span class="queue__txt">${q.State === 'running' ? '⟳ 执行中' : q.State === 'done' ? '✓ 已完成' : '✕ 已取消'}</span>
    <span class="mono">${q.Code}</span>
    <span class="small">进行中 <b>${run}</b> · 完成 <b>${done}</b> · 失败 <b>${fail}</b></span>
    <span class="queue__bar"><span class="queue__fill ${cls}" style="width:${p}%"></span></span>
    <span class="small mono">${p}%</span>
    <button class="btn btn--sm" onclick="openQueue()">明细</button>
    ${q.State === 'running' ? '<button class="btn btn--sm" onclick="cancelQueue()">取消</button>' : ''}
  </div>`;
}
function openQueue() { S.edTab = 0; showDrawer('q'); renderQueueDetail(); }
function renderQueueDetail() {
  const q = S.queue; if (!q) return;
  $('qCode').textContent = q.Code;
  const map = { success: ['tag--success', '成功'], failed: ['tag--danger', '失败'], running: ['tag--info', '进行中'], skipped: ['tag--gray', '跳过'] };
  $('qBody').innerHTML = `
    <div class="alert alert--info">
      <b>原子任务 = 单个文件</b>（对齐 26 号 §3.6 粒度模型）：一个范围 = 一个批次，一个文件 = 一个任务。<br>
      ⇒ <b>部分失败可单独重试</b>，重试不重跑已成功的文件；进度是<b>文件数</b>而不是步骤数。
    </div>
    <table class="tbl">
      <thead><tr><th>文档</th><th style="width:80px">类型</th><th style="width:80px">状态</th><th style="width:70px">耗时</th><th>说明</th><th style="width:90px">操作</th></tr></thead>
      <tbody>${q.Tasks.map((t, i) => {
        const m = map[t.State] || map.skipped;
        const act = t.State === 'failed' ? `<button class="btn btn--sm" onclick="retryOne(${i})">重试</button>`
                  : t.State === 'success' ? `<button class="btn btn--sm" onclick="locateDoc('${t.Doc}')">查看</button>` : '';
        return `<tr><td>${esc(t.Doc)}</td><td class="small">${t.Type}</td>
          <td><span class="tag ${m[0]}">${m[1]}</span></td>
          <td class="mono small">${t.Ms ? t.Ms + 'ms' : '—'}</td>
          <td class="small">${esc(t.Msg)}</td><td>${act}</td></tr>`;
      }).join('')}</tbody></table>`;
}
function locateDoc(name) {
  const row = currentDocs().find(r => r.name === name);
  if (row) { closeDrawer('q'); openDoc(row.code); } else say('warn', '该文档不在当前范围内');
}
function retryOne(i) {
  S.queue.Tasks[i].State = 'running'; S.queue.State = 'running';
  setTimeout(() => { S.queue.Tasks[i].State = 'success'; S.queue.Tasks[i].Msg = '重试成功'; say('ok', '重试成功'); }, 900);
}
function retryFailed() {
  const n = S.queue.Tasks.filter(t => t.State === 'failed').length;
  if (!n) return say('info', '没有失败项');
  S.queue.Tasks.forEach(t => { if (t.State === 'failed') { t.State = 'running'; } });
  S.queue.State = 'running'; say('info', `正在重试 ${n} 个失败项…`);
  setTimeout(() => {
    S.queue.Tasks.forEach(t => { if (t.State === 'running') { t.State = 'success'; t.Msg = '模板已配置，重试成功'; } });
    S.queue.State = 'done'; say('ok', `${n} 个失败项重试成功`);
  }, 1400);
}
function cancelQueue() { S.queue.State = 'cancelled'; S.queue.Tasks.forEach(t => { if (t.State === 'running') t.State = 'skipped'; }); say('warn', '队列已取消，已完成的产物保留'); }
/* ================= 抽屉通用 ================= */
function showDrawer(k) {
  ['fixed','edit','q','h'].forEach(x => {
    if (x !== k) { $('drawer'+cap(x)).style.display = 'none'; $('mask'+cap(x)).style.display = 'none'; }
  });
  $('drawer'+cap(k)).style.display = 'flex'; $('mask'+cap(k)).style.display = 'block';
}
function cap(s) { return s === 'q' ? 'Q' : s === 'h' ? 'H' : s === 'fixed' ? 'Fixed' : 'Edit'; }
function closeDrawer(k) {
  $('drawer'+cap(k)).style.display = 'none'; $('mask'+cap(k)).style.display = 'none';
  if (k === 'edit' || k === 'fixed') { S.editing = null; render(); }
}
function closeAll() { ['fixed','edit','q','h'].forEach(closeDrawer); }

/* ================= 抽屉 1：固定文档 · 匹配确认 ================= */
function openDoc(code) {
  const row = currentDocs().find(r => r.code === code);
  if (!row) return say('warn', '文档不在当前范围');
  if (row.cat === 'fixed') { S.editing = code; openFixed(code); }
  else { S.editing = code; S.edTab = 0; openEdit(code); }
}

function openFixed(code) {
  const row = currentDocs().find(r => r.code === code);
  const p = S.picked[code] || { State: 'none' };
  const cands = MOCK.candidates[code] || [];
  $('fxTitle').textContent = row.name;

  /* 头部：为什么要人工选 */
  let h = '';
  if (cands.length > 1) {
    h += `<div class="alert alert--warn">
      <b>⚠ 检测到 ${cands.length} 个待匹配项</b><br>
      系统已按「文件名 + 文档内关键字段 + 分类标签」召回候选，但<b>无法自动确定哪一个是正确的</b>。<br>
      <span class="mono" style="font-size:11px">后端返回字段：MatchState='ambiguous' · CandidateCount=${cands.length} · Top1Score=${(cands[0].Score).toFixed(2)}</span><br>
      <b>请人工指定</b> —— 指定后本平台不再自动改动（会「固定」）。
    </div>`;
  } else if (!cands.length) {
    h += `<div class="alert alert--danger"><b>企业原始资料里没有可匹配的文件</b><br>请到「企业原始资料管理」上传，或在本页标记「企业暂无」。</div>`;
  } else {
    h += `<div class="alert alert--info"><b>唯一候选，已自动匹配</b> —— 请核对后确认。</div>`;
  }

  /* 当前已选 */
  if (p.Picked) {
    const c = cands.find(x => x.FileCode === p.Picked);
    if (c) {
      h += `<div class="card"><div class="card__head">✓ 当前已选（匹配度 <b style="color:var(--yzh-color-success)">${pct(c.Score)}</b>）</div>
        <div class="card__body row">
          <div class="cand__thumb">${esc(c.FileType)}</div>
          <div class="grow"><div><b>${esc(c.FileName)}</b></div>
            <div class="small muted">命中字段：${esc(c.MatchField)} · ${esc(c.Size)} · 来自企业原始资料</div>
            <div class="cand__evid" style="margin-top:6px">${c.Evidence}</div></div>
          <button class="btn btn--sm" onclick="unpick('${code}')">取消选用</button>
        </div></div>`;
    }
  }

  /* 候选列表 */
  h += `<div class="card"><div class="card__head">候选文件（${cands.length}）
    <span class="spacer"></span><span class="small muted">按匹配度降序 · 点击卡片即选中</span></div>
    <div class="card__body" id="candBox">`;
  if (!cands.length) h += '<div class="muted">无候选</div>';
  cands.forEach((c, i) => {
    const on = p.Picked === c.FileCode;
    const lv = c.Score >= 0.9 ? 'tag--success' : c.Score >= 0.6 ? 'tag--warning' : 'tag--danger';
    h += `<div class="cand ${on ? 'is-picked' : ''}" onclick="pick('${code}','${c.FileCode}')">
      <div class="cand__head">
        <div class="cand__thumb">${esc(c.FileType)}</div>
        <div class="grow">
          <div class="cand__name">${esc(c.FileName)} ${i === 0 ? '<span class="tag tag--info">Top1</span>' : ''}</div>
          <div class="small muted">${esc(c.Size)} · 命中：${esc(c.MatchField)}${c.MatchedBy ? ' · ' + esc(c.MatchedBy) : ''}</div>
        </div>
        <span class="cand__score"><span class="tag ${lv}">${pct(c.Score)}</span></span>
        <span class="tag ${on ? 'tag--success' : 'tag--gray'}">${on ? '✓ 已选' : '选用'}</span>
      </div>
      <div class="cand__evid">${c.Evidence}</div>
      <table class="tbl" style="margin-top:6px"><tbody>${c.Fields.map(f => `<tr><td style="width:120px" class="small muted">${esc(f[0])}</td><td class="small">${esc(f[1])}</td></tr>`).join('')}</tbody></table>
      <div class="row" style="margin-top:6px">
        <button class="btn btn--sm" onclick="event.stopPropagation();previewRaw('${c.FileCode}')">🔍 预览原件</button>
        <button class="btn btn--sm" onclick="event.stopPropagation();markEnterpriseNone('${code}')">企业暂无此件</button>
      </div>
    </div>`;
  });
  h += `</div></div>`;

  /* 生成后的文档预览（★ 队列执行完成后可预览） */
  if (p.Picked) {
    h += `<div class="card"><div class="card__head">📄 生成后的文档预览
      <span class="spacer"></span>
      <span class="small muted">即企业资料库里看到的版本</span></div>
      <div class="card__body">
      <div class="preview">${fixedPreview(row, cands.find(x => x.FileCode === p.Picked))}</div>
      <div class="row" style="margin-top:10px">
        <button class="btn btn--sm" onclick="downloadDoc('${code}')">⬇ 下载</button>
        <button class="btn btn--sm" onclick="say('info','原型：走「四段式上传」重新上传')">⬆ 重新上传（替换原件）</button>
        <span class="grow"></span>
        <span class="small muted">重新上传后本平台会重新识别，仍可再次人工指定</span>
      </div></div></div>`;
  }

  $('fxBody').innerHTML = h;
  $('fxFoot').innerHTML = p.Picked ? `将「${esc(row.name)}」<b>固定</b>为选定原件，后续重新规范化不会覆盖` : '尚未选定原件';
  $('fxSave').disabled = !p.Picked;
  showDrawer('fixed');
}
function fixedPreview(row, c) {
  if (!c) return '—';
  const f = Object.fromEntries(c.Fields);
  return `<div style="text-align:center;margin-bottom:16px">
      <div style="font-size:16px;font-weight:700">${esc(row.name)}</div>
      <div class="small muted">（企业原件接收后归档视图 · 只读）</div></div>
    <div>企业名称：<span class="ph">${esc(f['企业名称'] || c.FileName)}</span></div>
    <div>证件/证照编号：<span class="ph">${esc(f['信用代码'] || f['证号'] || f['公民身份号码'] || '—')}</span></div>
    <div>法定代表人 / 姓名：<span class="ph">${esc(f['法定代表人'] || f['姓名'] || '—')}</span></div>
    <div>来源：<span class="small">企业原始资料 · ${esc(c.FileName)}</span> <span class="tag tag--success">匹配度 ${pct(c.Score)}</span></div>
    <div class="hr"></div><div class="small muted">此文档为 <b>fixed（固定格式）</b>：平台不生成内容，只做「识别 → 人工指定 → 接收 → 固定」。</div>`;
}
function pick(code, fileCode) {
  const p = S.picked[code] || (S.picked[code] = { State: 'none' });
  p.Picked = fileCode; p.Ambiguous = false; p.State = 'confirmed'; p.Reason = '';
  logIt('选定原件', currentDocs().find(r => r.code === code)?.name || code, '人工从候选中指定');
  say('ok', '已选定，点「确认并更新文档」生效');
  openFixed(code); render();
}
function unpick(code) {
  const p = S.picked[code];
  const cands = MOCK.candidates[code] || [];
  p.Picked = null;
  p.State = cands.length > 1 ? 'need_confirm' : 'none';
  p.CandidateCount = cands.length;
  say('info', '已取消选用'); openFixed(code); render();
}
function markEnterpriseNone(code) { say('warn', '原型：标记「企业暂无」会写入 cert_standard_directory_file 的缺口清单（不锁定，可后续补传）'); }
function saveFixed() {
  const code = S.editing;
  const row = currentDocs().find(r => r.code === code);
  const cands = MOCK.candidates[code] || [];
  const c = cands.find(x => x.FileCode === S.picked[code]?.Picked);
  logIt('固定原件', row?.name || code, `选定 ${c?.FileName}（匹配度 ${c ? pct(c.Score) : '—'}）`);
  S.locked[code] = true;
  closeDrawer('fixed');
  say('ok', `「${row?.name}」已固定并归档到企业资料库`);
}
function previewRaw(fileCode) {
  const r = MOCK.rawFiles.find(x => x.Code === fileCode);
  say('info', `原型：打开企业原始资料预览 — ${r ? r.Name : fileCode}（只读，不进本页树）`);
}
function previewDoc(code) {
  const row = currentDocs().find(r => r.code === code);
  if (row.cat === 'fixed') { S.editing = code; openFixed(code); }
  else { S.editing = code; S.edTab = 0; openEdit(code); }
}

/* ================= 抽屉 2：可编辑文档 · 单元格干预 ================= */
function openEdit(code) {
  const row = currentDocs().find(r => r.code === code);
  const e = S.docs[code];
  $('edTitle').textContent = row.name;
  if (!e || e.Status === 'none') {
    $('edBody').innerHTML = `<div class="alert alert--warn"><b>尚未生成</b><br>请先执行「一键规范化」。若提示「模板未上传」，需到后台「标准文档填写规则」上传空白模板并扫描锚点。</div>`;
    $('edFoot').textContent = '';
    $('edTodoCnt').textContent = '0';
    showDrawer('edit'); return;
  }
  S.edits[code] = S.edits[code] || {};
  S.edTab = 0;
  switchEdTab(0);
  showDrawer('edit');
}
function switchEdTab(i, el) {
  S.edTab = i;
  document.querySelectorAll('.drawer__tabs .tab').forEach((x, k) => x.classList.toggle('is-active', k === i));
  const code = S.editing, e = S.docs[code];
  if (!e) return;

  if (i === 0) {
    $('edBody').innerHTML = `
      <div class="row" style="margin-bottom:12px">
        <span class="tag tag--success">完成度 ${pct(e.Completion)}</span>
        <span class="tag tag--info">已填 ${e.FilledCount} / ${e.AnchorTotal}</span>
        <span class="tag tag--gray">加权可信度 ${pct(avgConf({code}))}</span>
        <span class="grow"></span>
        <span class="small muted">模板：${esc(e.TemplateName)}</span>
      </div>
      <div class="preview">${docPreview(e)}</div>
      <div class="row" style="margin-top:10px">
        <button class="btn btn--sm" onclick="downloadDoc('${code}')">⬇ 下载</button>
        <button class="btn btn--sm" onclick="say('info','原型：重新上传走四段式，替换企业资料库版本')">⬆ 重新上传</button>
        <span class="grow"></span>
        <span class="small muted">修改后须点右下「更新文档」才会真正写入并同步到企业资料库</span>
      </div>`;
    $('edFoot').textContent = `完成度 ${pct(e.Completion)}`;
  }

  if (i === 1) {
    const todo = e.AnchorTotal - e.FilledCount;
    $('edTodoCnt').textContent = todo;
    let h = `<div class="alert alert--info">
      <b>逐格干预</b>：直接改值，或从「企业原始资料」中重新筛选最匹配的内容。<br>
      <span class="small">已改 <b id="edN">${countEdits(code)}</b> 格 · 钉住 <b>${countPins(code)}</b> 格 —— 钉住的格在「全部重写」时保留。</span></div>
      <table class="tbl">
      <thead><tr>
        <th style="width:120px">位置</th><th style="width:110px">字段</th>
        <th style="width:230px">值（可直接改）</th>
        <th style="width:170px">来源</th><th style="width:110px">可信度</th>
        <th style="width:180px">从企业资料重新筛选</th><th style="width:60px">钉住</th>
      </tr></thead><tbody>`;
    (e.Cells || []).forEach((c, i) => {
      const k = c.Ref;
      const ov = S.edits[code][k];
      const val = ov ? ov.val : c.Val;
      const kind = ov ? ov.srcKind : c.Kind;
      const sk = SRC_KIND[kind] || SRC_KIND.manual;
      const lv = confLevel(c.Conf);
      const pinned = ov?.pinned || c.Pinned;
      const cands = rawCandidates(k);
      h += `<tr>
        <td class="mono small">${esc(c.Loc)}</td>
        <td class="mono small">${esc(c.Field)}</td>
        <td><input class="inp ${ov ? 'is-edited' : ''}" value="${esc(val)}" oninput="editCell('${code}','${k}',this.value,'${kind}')"></td>
        <td><span class="tag ${sk.tag}">${sk.label}</span>${ov ? ' <span class="tag tag--warning">已改</span>' : ''}</td>
        <td class="small nowrap"><span class="dot dot--${lv.k}"></span><span class="dot dot--${lv.k}"></span><span class="dot dot--${c.Conf >= .7 ? lv.k : 'none'}"></span> ${Math.round(c.Conf * 100)}%</td>
        <td>${cands.length ? `<select class="inp" onchange="pickFromRaw('${code}','${k}',this)">
            <option value="">— 从 ${cands.length} 个原始文件中筛 —</option>
            ${cands.map(x => `<option value="${esc(x.v)}" ${val === x.v ? 'selected' : ''}>${esc(x.f)}：${esc(x.v.slice(0, 26))}</option>`).join('')}
          </select>` : '<span class="small muted">无候选（确定性来源）</span>'}</td>
        <td><input type="checkbox" ${pinned ? 'checked' : ''} onchange="pinCell('${code}','${k}',this.checked)"></td>
      </tr>`;
    });
    if (!(e.Cells || []).length) h += `<tr><td colspan="7" class="muted" style="text-align:center;padding:20px">该模板未识别到任何可填锚点（纯复制文档）</td></tr>`;
    h += `</tbody></table>`;

    /* 待办清单 */
    if (todo > 0) {
      h += `<div class="card" style="margin-top:14px"><div class="card__head" style="background:var(--yzh-color-warning-light);color:var(--yzh-color-warning)">
        待办 ${todo} 项 —— 这些锚点没能自动取值</div><div class="card__body">
        <div class="hint">常见原因：<b>无此参数</b>（后台没定义该参数）· <b>未填写</b>（企业参数为空）· <b>锚点未独占段落</b>（Word 域写入要求）· <b>模板未上传</b>。<br>
        处理方式：① 在上方表格直接补填 ② 到「企业全局参数定义」补值后重新规范化 ③ 到后台「标准文档填写规则」补锚点规则。</div>
        <table class="tbl" style="margin-top:8px"><thead><tr><th style="width:200px">锚点</th><th style="width:150px">位置</th><th>原因</th></tr></thead><tbody>
          <tr><td class="mono small">{{table:培训记录}}</td><td class="small">数据区 行4-9</td><td class="small">企业资料中未找到匹配的培训记录（0 命中）</td></tr>
          <tr><td class="mono small">{{TRAIN_HOURS}}</td><td class="small">Sheet1!C3</td><td class="small">未填写 —— 企业参数「培训学时」为空</td></tr>
        </tbody></table></div></div>`;
    }
    $('edBody').innerHTML = h;
    $('edFoot').textContent = `已改 ${countEdits(code)} 格`;
  }

  if (i === 2) {
    const list = logsOf(code);
    $('edBody').innerHTML = `
      <div class="alert alert--warn"><b>改动清单</b>：点右下「更新文档」后，以下改动才会真正写入文档并同步到企业资料库。关闭则丢弃。</div>
      <table class="tbl"><thead><tr><th style="width:150px">位置</th><th style="width:110px">字段</th><th>原值 → 新值</th><th style="width:90px">操作</th></tr></thead><tbody>
      ${Object.keys(S.edits[code]).map(k => {
        const c = (e.Cells || []).find(x => x.Ref === k) || { Loc: k, Field: '—' };
        const o = S.edits[code][k];
        const from = o.from, to = o.val;
        const same = from === to;
        return `<tr><td class="mono small">${esc(c.Loc)}</td><td class="mono small">${esc(c.Field)}</td>
          <td class="small">${same ? `<span class="muted">${esc(to)}（值未变）</span>` : `<span class="muted">${esc(from)}</span> → <b style="color:var(--yzh-color-primary)">${esc(to)}</b>`}
            ${o.pinned ? ' <span class="tag tag--warning">钉住</span>' : ''}${o.srcChanged ? ' <span class="tag tag--info">来源已换</span>' : ''}</td>
          <td><button class="btn btn--sm" onclick="revertCell('${code}','${k}')">撤销</button></td></tr>`;
      }).join('') || '<tr><td colspan="4" class="muted" style="text-align:center;padding:20px">暂无改动</td></tr>'}
      </tbody></table>`;
    $('edFoot').textContent = `已改 ${countEdits(code)} 格`;
  }
}
function docPreview(e) {
  let h = `<div style="text-align:center;margin-bottom:14px"><b style="font-size:15px">${esc(e.TemplateName.replace(/\.(docx|xlsx)$/, ''))}</b>
    <div class="small muted">G4测试石油天然气股份有限公司</div></div>`;
  (e.Cells || []).forEach(c => {
    const s = SRC_KIND[c.Kind] || SRC_KIND.manual;
    const ov = S.edits[S.editing] && S.edits[S.editing][c.Ref];
    const val = ov ? ov.val : c.Val;
    h += `<div>${esc(c.Loc)}：<mark>${esc(val)}</mark>
      <span class="small muted"> ← ${s.label} ${Math.round(c.Conf * 100)}%</span>${ov ? ' <span class="tag tag--warning">已改</span>' : ''}</div>`;
  });
  if (!(e.Cells || []).length) h += '<div class="muted">该文档无可填锚点，内容与模板完全一致。</div>';
  h += `<div class="hr"></div><div class="small muted">黄色高亮 = 由平台填入的内容；灰色 = 模板原文。</div>`;
  return h;
}
function rawCandidates(ref) {
  if (/ENT_NAME|CREDIT_CODE|LEGAL|ORG_NAME|DOC_NO|PLAN_YEAR|AUDIT_TYPE|ISSUE_DATE/.test(ref)) return [];
  if (/AUDIT_LEADER/.test(ref)) return [
    { f: '2026年度内审计划.docx', v: '李建国' }, { f: '组织架构图.png', v: '李建国（质量管理部经理）' }];
  if (/RISK_LEVEL/.test(ref)) return [
    { f: '2026年度风险清单.xlsx', v: '高' }, { f: '2026年度风险清单.xlsx', v: '中' }, { f: '质量管理体系风险管理制度.docx', v: '低' }];
  if (/TRAIN_TOPIC/.test(ref)) return [
    { f: '2026年1月培训签到表.xlsx', v: 'ISO 9001:2015 换版要点' }, { f: '2026年1月培训签到表.xlsx', v: '内审员培训' }];
  if (/TRAIN_HOURS/.test(ref)) return [{ f: '2026年1月培训签到表.xlsx', v: '4' }, { f: '2026年1月培训签到表.xlsx', v: '6' }];
  if (/RISK_POLICY/.test(ref)) return [{ f: '质量管理体系风险管理制度.docx', v: '（略：质量管理体系风险管理制度 第3章 风险评价准则）' }];
  if (/RISK_ROWS/.test(ref)) return [{ f: '2026年度风险清单.xlsx', v: '12 行 × 4 列' }];
  if (/AUDIT_ROWS/.test(ref)) return [{ f: '2026年度内审计划.docx', v: '10 行 × 7 列' }];
  if (/TRAIN_ROW/.test(ref)) return [{ f: '2026年1月培训签到表.xlsx', v: '（从签到表生成 6 行）' }];
  return [];
}
function editCell(code, ref, val, kind) {
  const e = S.docs[code];
  const c = (e.Cells || []).find(x => x.Ref === ref);
  if (!c) return;
  S.edits[code][ref] = Object.assign({ pinned: c.Pinned || false }, S.edits[code][ref], { from: c.Val, val, srcKind: kind });
  render();
}
function pickFromRaw(code, ref, sel) {
  if (!sel.value) return;
  const e = S.docs[code];
  const c = (e.Cells || []).find(x => x.Ref === ref);
  const f = sel.value;
  S.edits[code][ref] = Object.assign({ pinned: c.Pinned || false }, S.edits[code][ref], {
    from: c.Val, val: f, srcKind: 'profile', srcChanged: true, fromFile: f.split('：')[0]
  });
  logIt('换来源', `${e.TemplateName} · ${c.Loc}`, `从 ${f.split('：')[0]} 重新筛选`);
  say('ok', '已换来源，点「更新文档」生效');
  render();
}
function pinCell(code, ref, v) {
  const e = S.docs[code];
  const c = (e.Cells || []).find(x => x.Ref === ref);
  c.Pinned = v;
  if (!S.edits[code][ref]) S.edits[code][ref] = { from: c.Val, val: c.Val, srcKind: c.Kind, pinned: v };
  else S.edits[code][ref].pinned = v;
  logIt(v ? '钉住' : '取消钉住', `${e.TemplateName} · ${c.Loc}`, '重写时' + (v ? '保留' : '不保留'));
  render();
}
function revertCell(code, ref) {
  delete S.edits[code][ref];
  const e = S.docs[code];
  const c = (e.Cells || []).find(x => x.Ref === ref);
  if (c) c.Pinned = false;
  render();
}
function countEdits(code) { return Object.keys(S.edits[code] || {}).filter(k => S.edits[code][k].val !== S.edits[code][k].from).length; }
function countPins(code) { const e = S.docs[code]; return ((e && e.Cells) || []).filter(c => c.Pinned).length; }
function saveEdit() {
  const code = S.editing, e = S.docs[code];
  const row = currentDocs().find(r => r.code === code);
  const n = countEdits(code);
  (e.Cells || []).forEach(c => {
    const o = S.edits[code][c.Ref];
    if (!o) return;
    c.Val = o.val; if (o.srcKind) c.Kind = o.srcKind;
  });
  const pending = (e.AnchorTotal || 0) - (e.Cells || []).filter(c => c.Kind !== 'pending').length;
  e.FilledCount = (e.Cells || []).filter(c => c.Kind !== 'pending').length;
  e.Completion = e.AnchorTotal ? e.FilledCount / e.AnchorTotal : 0;
  e.Status = e.Completion >= 1 ? 'generated' : 'partial';
  logIt('更新文档', row?.name || code, `完成度 ${pct(e.Completion)}（已填 ${e.FilledCount}/${e.AnchorTotal}）`);
  S.edits[code] = {};
  closeDrawer('edit');
  say('ok', `「${row?.name}」已更新并同步到企业资料库${pending ? `（仍有 ${pending} 项待填）` : ''}`);
}

/* ================= 操作 ================= */
function runNormalize() {
  const rows = filtered(currentDocs()).filter(r => !S.locked[r.code]);
  if (!rows.length) return say('warn', '当前范围内没有可规范化的文档');
  const noTpl = rows.filter(r => r.cat !== 'fixed' && (!S.docs[r.code] || S.docs[r.code].Status === 'none')).length;
  const q = { Code: 'Q-20261004-' + (1000 + Math.floor(Math.random() * 8999)), State: 'running',
    Tasks: rows.map(r => ({ Doc: r.name, Type: r.cat === 'fixed' ? '固定' : (r.cat === 'platform_generated' ? '生成' : '可编辑'), State: 'running', Ms: 0, Msg: '执行中…', code: r.code })) };
  S.queue = q; render();
  say('info', `已入队 ${rows.length} 个单文件任务${noTpl ? `（其中 ${noTpl} 个可能提示「模板未上传」）` : ''}`);
  let i = 0;
  const step = () => {
    if (!S.queue || S.queue.State !== 'running') return;
    if (i >= q.Tasks.length) {
      q.Tasks.forEach(t => { if (t.State === 'running') { t.State = 'success'; t.Msg = '完成'; } });
      q.State = 'done';
      render();
      const fail = q.Tasks.filter(t => t.State === 'failed').length;
      say(fail ? 'warn' : 'ok', fail ? `队列完成，${fail} 个失败` : '队列完成，全部成功');
      return;
    }
    const t = q.Tasks[i++];
    setTimeout(() => {
      if (!S.queue || S.queue.State !== 'running') return;
      const d = S.docs[t.code];
      if (t.Type === '固定') {
        const p = S.picked[t.code];
        t.Ms = 600 + i * 180;
        if (!p) { t.State = 'failed'; t.Msg = '企业原始资料中无可匹配文件'; }
        else if (p.State === 'need_confirm') { t.State = 'success'; t.Msg = `${p.CandidateCount} 个候选，待人工指定`; }
        else { t.State = 'success'; t.Msg = p.State === 'platform_generated' ? '平台自动生成' : (p.State === 'warn' ? '已就位，有效期告警' : '已按选定原件接收'); }
      } else if (t.Type === '生成') {
        t.Ms = 300; t.State = 'success'; t.Msg = '平台自动派生';
      } else if (d && d.Status !== 'none') {
        t.Ms = 700 + Math.round(Math.random() * 3000);
        t.State = 'success'; t.Msg = `${d.FilledCount}/${d.AnchorTotal} 已填，完成度 ${pct(d.Completion)}`;
      } else {
        t.Ms = 500; t.State = 'failed'; t.Msg = '模板未上传（后台「标准文档填写规则」尚未配置此模板）';
      }
      render();
      step();
    }, 380 + Math.random() * 260);
  };
  step();
}
function rewriteSelected() {
  if (!S.sel.length) return say('warn', '请先勾选要重新生成的文档');
  const locked = S.sel.filter(c => S.locked[c]);
  const targets = S.sel.filter(c => !S.locked[c]);
  if (!targets.length) return say('warn', `勾选的 ${S.sel.length} 个文档全部已锁定 🔒，不会重新生成`);
  say('info', `重新生成 ${targets.length} 个（跳过已锁定 ${locked.length} 个${$('edKeepManual').checked ? ' · 保留人工填写值' : ''}）`);
  S.sel = [];
  setTimeout(runNormalize, 500);
}
function lockOne(code, v) {
  if (v) { S.locked[code] = true; logIt('锁定', currentDocs().find(r => r.code === code)?.name || code, '人工确认，接收后不再自动覆盖'); }
  else { delete S.locked[code]; logIt('解锁', currentDocs().find(r => r.code === code)?.name || code, '允许重新生成'); }
  say(v ? 'ok' : 'info', v ? '已锁定，重新生成时跳过' : '已解锁');
}
function lockSelected(v) {
  if (!S.sel.length) return say('warn', '请先勾选文档');
  if (v && $('edKeepManual') && !confirm('锁定后，这些文档将不再被自动规范化覆盖（重新上传企业资料也不影响）。\n锁定会记录操作人与时间，可在「修改记录」查看。\n\n确认锁定 ' + S.sel.length + ' 个文档？')) return;
  S.sel.forEach(c => v ? (S.locked[c] = true) : delete S.locked[c]);
  logIt(v ? '批量锁定' : '批量解锁', `${S.sel.length} 个文档`, '勾选批量操作');
  say(v ? 'ok' : 'info', `已${v ? '锁定' : '解锁'} ${S.sel.length} 个`);
  S.sel = [];
}
function packageZip() {
  const rows = filtered(currentDocs()).filter(r => doneRatio(r) > 0);
  if (!rows.length) return say('warn', '没有已生成的文档可打包');
  const fix = rows.filter(r => r.cat === 'fixed').length;
  say('ok', `打包 ${rows.length} 个文档（固定件 ${fix} + 生成件 ${rows.length - fix}）→ G4测试企业甲_ISO9001_复审_20261004.zip（含「填写说明.txt」）`);
}
function exportValues() {
  const rows = filtered(currentDocs()).filter(r => r.cat !== 'fixed' && doneRatio(r) > 0);
  if (!rows.length) return say('warn', '没有已生成的文档');
  say('ok', `导出填写明细 ${rows.length} 个文档 × 逐锚点（位置/字段/值/来源/可信度/是否人工改）→ .xlsx`);
}
function downloadDoc(code) { say('info', '原型：下载生成后的文档（MinIO enterprise-documents/…）'); }

function logIt(type, target, reason) {
  const row = currentDocs().find(r => r.code === S.editing);
  S.logs.unshift({ Time: new Date().toLocaleString('zh-CN', { hour12: false }).replace(/\//g, '-'), User: '王专家', Type: type, Target: target, Reason: reason });
}
function logsOf(code) {
  const row = currentDocs().find(r => r.code === code);
  return S.logs.filter(l => l.Target.indexOf(row?.name || '###') >= 0);
}
function renderRaw(all) {
  $('rawCnt').textContent = MOCK.rawFiles.length;
  const used = {};
  all.forEach(r => { const p = S.picked[r.code]; if (p?.Picked) used[p.Picked] = (used[p.Picked] || []).concat(r.name); });
  $('rawTbody').innerHTML = MOCK.rawFiles.map(f => `<tr>
    <td>${esc(f.Name)}</td><td><span class="tag tag--gray">${esc(f.Cat)}</span></td>
    <td class="small">${esc(f.Stage)}</td><td class="small muted">${esc(f.Updated)}</td>
    <td class="small">${used[f.Code] ? used[f.Code].map(u => `<span class="tag tag--success">${esc(u)}</span>`).join(' ') : '<span class="muted">未引用</span>'}</td>
  </tr>`).join('');
}
function renderLog() {
  $('logTbody').innerHTML = S.logs.map(l => `<tr>
    <td class="mono small">${esc(l.Time)}</td><td class="small">${esc(l.User)}</td>
    <td><span class="tag ${l.Type.indexOf('锁定') >= 0 || l.Type === '锁定' ? 'tag--warning' : l.Type.indexOf('更新') >= 0 ? 'tag--success' : 'tag--info'}">${esc(l.Type)}</span></td>
    <td class="small">${esc(l.Target)}</td><td class="small muted">${esc(l.Reason)}</td></tr>`).join('');
}
function showHelp() {
  $('hBody').innerHTML = `
  <div class="alert alert--info"><b>本原型演示的 4 件事</b></div>
  <div class="col">
    <div class="card"><div class="card__head">① 左树 = 企业 + 资源（只有两层语义）</div><div class="card__body small">
      企业 → 资源（标准 → 阶段 → 目录 → 文档）。<b>企业原始资料不在树里</b>，
      它只出现在底部「企业原始资料池」（只读）和右侧候选选择里 —— 因为它已经上传完了，本页只消费不管理。</div></div>
    <div class="card"><div class="card__head">② 固定文档：多个待匹配项 → 人工指定</div><div class="card__body small">
      典型场景：标准目录要求「法人身份证」，企业上传了 <b>4 份身份证</b>。
      点「法人身份证」行 → 抽屉显示 4 个候选（按匹配度降序 + 命中字段 + 证据原文 + 抽取字段表）→ 人工点选 → 「确认并更新文档」→ 自动<b>固定</b>。</div></div>
    <div class="card"><div class="card__head">③ 可编辑文档：逐单元格干预</div><div class="card__body small">
      三个页签：<b>预览</b>（高亮显示平台填了什么）· <b>单元格明细</b>（每格可改值 / 从原始资料重新筛 / 钉住）· <b>改动清单</b>（改了什么、能不能撤销）。<br>
      ⚠ 所有改动<b>暂存</b>，点右下「<b>更新文档</b>」才真正写入 docx 并同步到企业资料库。</div></div>
    <div class="card"><div class="card__head">④ 一键规范化 = 一批单文件队列任务</div><div class="card__body small">
      一个范围 = 一个批次，一个文件 = 一个任务（原子）。队列条显示进度，失败可单独重试，已锁定自动跳过，可取消。</div></div>
  </div>
  <div class="alert alert--warn"><b>原型边界</b>：数据为模拟；所有后端交互用 toast 提示代替；「更新文档」只改内存态，不落库。</div>`;
  showDrawer('h');
}
function resetAll() {
  if (!confirm('重置全部演示数据？')) return;
  location.reload();
}
