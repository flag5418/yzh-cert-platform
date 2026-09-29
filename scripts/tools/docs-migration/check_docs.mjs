#!/usr/bin/env node
/**
 * check_docs.mjs — 文档体系防回归校验（2026-09-28 输出产物清理配套）
 *
 * 用法：node scripts/tools/docs-migration/check_docs.mjs [--no-dead-links]
 *
 * 校验项：
 *   S1  docs/ 根只允许「两位数字开头目录 + README.md + 隐藏文件」（禁平铺、禁自建顶层）
 *   S2  docs/ 顶层目录白名单：00/10/20/30/40/50/90
 *   S3  50-任务 四分区齐全且各带 README.md（50-任务/README.md 必须存在）
 *   S4  输出产物/ 不得出现 .md 文件，活跃文档不得引用 输出产物/*.md
 *   S5  任何文档不得以 markdown 链接指向 关键信息速查.md（gitignore 文件，不可入库引用）
 *   S6  全量死链：docs 内 + AGENTS.md + 项目全局规则.md 的本地相对链接存在性
 *        （90-归档/ 冻结不维护，只查 S1–S5，不查死链）
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const DOCS = path.join(REPO, 'docs');
const TOP_ALLOWED = /^\d{2}-/;
const TOP_DIRS = new Set(['00-工程体系', '10-YZH架构', '20-体系认证', '30-项目规则', '40-实施', '50-任务', '90-归档']);
const PARTITIONS = ['分析报告', '开发计划', '迁移计划', '迁移工作台'];
const NO_DEAD_LINK_DIRS = ['90-归档'];
const strictDeadLinks = !process.argv.includes('--no-dead-links');

const errors = [];
const warnings = [];
const err = (rule, msg) => errors.push(`[${rule}] ${msg}`);
const warn = (rule, msg) => warnings.push(`[${rule}] ${msg}`);

function walk(dir, cb, relBase = '') {
  for (const name of fs.readdirSync(dir)) {
    const abs = path.join(dir, name);
    const rel = relBase ? `${relBase}/${name}` : name;
    const st = fs.statSync(abs);
    if (st.isDirectory()) walk(abs, cb, rel);
    else cb(abs, rel);
  }
}

// ---------- S1 + S2：docs 根平铺与顶层白名单 ----------
{
  for (const name of fs.readdirSync(DOCS)) {
    if (name === '.DS_Store') { warn('S1', 'docs/ 存在 .DS_Store（建议删除并忽略）'); continue; }
    if (name.startsWith('.')) continue; // 其他隐藏文件不参与校验
    const abs = path.join(DOCS, name);
    if (fs.statSync(abs).isDirectory()) {
      if (!TOP_DIRS.has(name) || !TOP_ALLOWED.test(name)) {
        err('S2', `docs/ 顶层目录不在白名单：${name}`);
      }
    } else {
      if (name !== 'README.md') err('S1', `docs/ 根禁止平铺文件：${name}（应归入对应层目录）`);
    }
  }
}

// ---------- S3：50-任务 四分区 + README ----------
{
  const base = path.join(DOCS, '50-任务');
  if (!fs.existsSync(path.join(base, 'README.md'))) err('S3', '缺少 docs/50-任务/README.md');
  for (const p of PARTITIONS) {
    if (!fs.existsSync(path.join(base, p))) err('S3', `50-任务 缺少分区目录：${p}/`);
    else if (!fs.existsSync(path.join(base, p, 'README.md'))) err('S3', `50-任务/${p}/ 缺少 README.md`);
    else {
      // 分区内平铺 md 必须在 README 登记
      const readme = fs.readFileSync(path.join(base, p, 'README.md'), 'utf-8');
      for (const f of fs.readdirSync(path.join(base, p))) {
        if (f.endsWith('.md') && f !== 'README.md' && !readme.includes(f)) {
          err('S3', `50-任务/${p}/${f} 未在该分区 README.md 登记`);
        }
      }
    }
  }
}

// ---------- S4：输出产物 ----------
{
  const outDir = path.join(REPO, '输出产物');
  if (fs.existsSync(outDir)) {
    const found = [];
    walk(outDir, (abs, rel) => { if (rel.endsWith('.md')) found.push(rel); });
    for (const f of found) err('S4', `输出产物/ 出现文档文件：${f}（文档必须放 docs/）`);
  }
  walk(DOCS, (abs, rel) => {
    if (!rel.endsWith('.md')) return;
    if (rel.startsWith('90-归档/') || rel.includes('分类清单-2026-09-28')) return; // 历史记录保留原文
    const text = fs.readFileSync(abs, 'utf-8');
    const m = text.match(/输出产物\/[^\s)\]】`"'>]*\.md/g);
    if (m) err('S4', `${rel} 引用了输出产物路径：${m[0]}`);
  });
  for (const f of ['AGENTS.md', '项目全局规则.md']) {
    const p = path.join(REPO, f);
    if (!fs.existsSync(p)) continue;
    const m = fs.readFileSync(p, 'utf-8').match(/输出产物\/[^\s)\]】`"'>]*\.md/g);
    if (m) err('S4', `${f} 引用了输出产物路径：${m[0]}`);
  }
}

// ---------- S5：禁止链接 gitignore 密钥文件 ----------
{
  const targets = [];
  walk(DOCS, (abs, rel) => { if (rel.endsWith('.md')) targets.push(abs); });
  for (const f of ['AGENTS.md', '项目全局规则.md', 'README.md']) {
    const p = path.join(REPO, f);
    if (fs.existsSync(p)) targets.push(p);
  }
  for (const abs of targets) {
    const rel = path.relative(REPO, abs);
    if (rel.startsWith('90-归档/')) continue;
    const text = fs.readFileSync(abs, 'utf-8');
    const m = text.match(/\]\([^)]*关键信息速查[^)]*\)/g);
    if (m) err('S5', `${rel} 以链接指向 gitignore 密钥文件：${m[0]}（只允许纯文本存在性登记）`);
  }
}

// ---------- S6：死链 ----------
function stripCode(text) {
  return text
    .replace(/```[\s\S]*?```/g, '')          // 围栏代码块
    .replace(/`[^`\n]*`/g, '');               // 行内代码
}
function checkLinks(abs, rel) {
  if (NO_DEAD_LINK_DIRS.some(d => rel.startsWith(d + '/'))) return;
  const text = stripCode(fs.readFileSync(abs, 'utf-8'));
  const re = /\]\(([^)\s]+)(?:\s+"[^"]*")?\)/g;
  let m;
  while ((m = re.exec(text)) !== null) {
    let target = m[1].trim();
    if (/^(https?:|mailto:|tel:|data:|#)/.test(target)) continue;
    if (target.startsWith('<')) target = target.slice(1, -1);
    target = decodeURIComponent(target.split('#')[0].split('?')[0]);
    if (!target || target.includes('{{') || target.includes('${')) continue;
    let absTarget;
    if (target.startsWith('/')) absTarget = path.join(REPO, target.slice(1));
    else absTarget = path.resolve(path.dirname(abs), target);
    if (!fs.existsSync(absTarget)) {
      warn('S6', `死链 ${rel} → ${m[1]}`);
    }
  }
}
{
  const targets = [];
  walk(DOCS, (abs, rel) => { if (rel.endsWith('.md')) targets.push([abs, rel]); });
  for (const f of ['AGENTS.md', '项目全局规则.md', 'README.md']) {
    const p = path.join(REPO, f);
    if (fs.existsSync(p)) targets.push([p, f]);
  }
  for (const [abs, rel] of targets) checkLinks(abs, rel);
}

// ---------- 汇总 ----------
for (const w of warnings) console.log('WARN  ' + w);
for (const e of errors) console.log('ERROR ' + e);
console.log(`\ncheck_docs: ${errors.length} error(s), ${warnings.length} warning(s)`);
if (errors.length) process.exit(1);
if (strictDeadLinks && warnings.some(w => w.startsWith('[S6]'))) {
  console.log('（死链以 WARN 呈现；加 --no-dead-links 可忽略）');
}
