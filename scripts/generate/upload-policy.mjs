#!/usr/bin/env node
/**
 * ============================================================
 * 上传文件类型契约 · 清单段生成器
 *
 * 唯一事实源：src/certplatform-api/CertPlatform.Shared/Storage/upload-file-policy.json
 * 生成目标（两端 <generated> 标记段，⛔ 标记段禁手改）：
 *   ① src/certplatform-api/CertPlatform.Shared/Storage/UploadFilePolicy.cs
 *      id=categories（Document/Image/Pdf/Archive 四数组）
 *      id=blacklist（Never/Script/SystemNoiseNames）
 *   ② src/certplatform-web/cert/cert-share/src/constants/upload-file-policy.ts
 *      id=ext（EXT_* 四常量）
 *      id=blacklist（UPLOAD_NEVER/SCRIPT/NOISE 三常量）
 *
 * 用法：
 *   node scripts/generate/upload-policy.mjs           # 写入生成段
 *   node scripts/generate/upload-policy.mjs --check   # 只校验（守卫用；有漂移退出码 1）
 *
 * 改后缀流程：改 JSON → 跑本脚本 → 两端一起更新，守卫 --check 保证零漂移。
 * 维护人：上传后缀契约整改 V1（2026-10-14）
 * ============================================================
 */
import { readFileSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const HERE = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(HERE, '../..')

const JSON_PATH = resolve(ROOT, 'src/certplatform-api/CertPlatform.Shared/Storage/upload-file-policy.json')
const CS_PATH = resolve(ROOT, 'src/certplatform-api/CertPlatform.Shared/Storage/UploadFilePolicy.cs')
const TS_PATH = resolve(ROOT, 'src/certplatform-web/cert/cert-share/src/constants/upload-file-policy.ts')

const CHECK = process.argv.includes('--check')

// ── 读 JSON 事实源 ──
const policy = JSON.parse(readFileSync(JSON_PATH, 'utf8'))
for (const key of ['document', 'image', 'pdf', 'archive', 'never', 'script', 'systemNoiseNames']) {
  if (!Array.isArray(policy[key]) || policy[key].length === 0)
    throw new Error(`upload-file-policy.json 缺少非空数组字段：${key}`)
}

// ── 渲染工具 ──
const csQuote = (s) => `"${s}"`
const tsQuote = (s) => `'${s}'`

/** C# 数组：整行 <= 96 字符则单行，否则每行 2 项（沿用原文件风格） */
function csArray(items, indent) {
  const oneLine = `new[] { ${items.map(csQuote).join(', ')} }`
  if (indent.length + oneLine.length <= 96) return oneLine
  const lines = []
  for (let i = 0; i < items.length; i += 2)
    lines.push(`${indent}    ${items.slice(i, i + 2).map(csQuote).join(', ')},`)
  return `new[]\n${indent}{\n${lines.join('\n')}\n${indent}}`
}

/** TS 数组：全部单行放不下（>90）则每行 4 项 */
function tsArray(items, quote) {
  const oneLine = `[${items.map(quote).join(', ')}]`
  if (oneLine.length <= 90) return oneLine
  const lines = []
  for (let i = 0; i < items.length; i += 4)
    lines.push(`  ${items.slice(i, i + 4).map(quote).join(', ')},`)
  return `[\n${lines.join('\n')}\n]`
}

// ── C# 两段 ──
function renderCsCategories(p) {
  const I = '        '
  return `${I}/// <summary>办公文档（Word/Excel/PPT/文本/RTF/OpenDocument）</summary>
${I}public static readonly IReadOnlyList<string> DocumentExtensions = ${csArray(p.document, I)};

${I}/// <summary>图片（证件扫描件 / 现场照片）</summary>
${I}public static readonly IReadOnlyList<string> ImageExtensions = ${csArray(p.image, I)};

${I}/// <summary>PDF（文本层直解优先，失败才 AI）</summary>
${I}public static readonly IReadOnlyList<string> PdfExtensions = ${csArray(p.pdf, I)};

${I}/// <summary>
${I}/// 压缩包 —— <b>默认不开放</b>。
${I}/// <para>⛔ 不给开口子的理由：压缩包无法做内容抽取 / 语义分析 / 预览，
${I}/// 后续所有处理链都不支持它；放进来只会占 MinIO 与数据库。
${I}/// 将来若要「打包上传并自动解压」，正确做法是<b>新增解压任务</b>，不是放宽白名单。</para>
${I}/// </summary>
${I}public static readonly IReadOnlyList<string> ArchiveExtensions = ${csArray(p.archive, I)};`
}

function renderCsBlacklist(p) {
  const I = '        '
  return `${I}/// <summary>永久黑名单：可执行文件 / 动态库 / 脚本执行器</summary>
${I}public static readonly IReadOnlyList<string> NeverExtensions = ${csArray(p.never, I)};

${I}/// <summary>
${I}/// 永久黑名单：HTML / SVG / XML —— 可内嵌脚本，放行等于给存储开 XSS 面。
${I}/// <para>⚠️ 本项目 Markdown 会被前端直接渲染（提示词工作台），SVG 内嵌
${I}/// <c>&lt;script&gt;</c> 时会执行。</para>
${I}/// </summary>
${I}public static readonly IReadOnlyList<string> ScriptExtensions = ${csArray(p.script, I)};

${I}/// <summary>操作系统 / 编辑器垃圾文件名（小写）</summary>
${I}private static readonly HashSet<string> SystemNoiseNames = new(StringComparer.OrdinalIgnoreCase)
${I}{
${I}    ${p.systemNoiseNames.map(csQuote).join(', ')}
${I}};`
}

// ── TS 两段 ──
function renderTsExt(p) {
  return `/** 办公文档类 */
export const EXT_DOCUMENT = ${tsArray(p.document, tsQuote)} as const

/** 图片类（证件 / 现场照片主要走这一类） */
export const EXT_IMAGE = ${tsArray(p.image, tsQuote)} as const

/** PDF 类（单独成类：判定顺序是「先文本层直解 → 失败才 AI」，与其它格式不同） */
export const EXT_PDF = ${tsArray(p.pdf, tsQuote)} as const

/** 压缩包类（默认**不开放**；理由见下方 DEFAULT_UPLOAD_CATEGORIES 注释） */
export const EXT_ARCHIVE = ${tsArray(p.archive, tsQuote)} as const`
}

function renderTsBlacklist(p) {
  return `/** 可执行文件 / 系统二进制 —— 明确黑名单（即使将来白名单写错，这些也永远拒） */
export const UPLOAD_NEVER_EXTENSIONS: readonly string[] = ${tsArray(p.never, tsQuote)}

/** HTML/SVG —— ⛔ 拒绝：可内嵌脚本，放行等于给存储开了 XSS 面 */
export const UPLOAD_SCRIPT_EXTENSIONS: readonly string[] = ${tsArray(p.script, tsQuote)}

/**
 * 操作系统 / 编辑器垃圾文件名（**小写**）。
 * ⛔ 不要用黑名单穷举：\`.DS_Store\` 在每个目录都可能生成，还有 \`.Spotlight-V100\`、
 * \`.Trashes\`、\`.fseventsd\` 等 ⇒ 永远列不全。改用**前缀规则**兜底（见 \`isSystemNoise\`）。
 */
export const UPLOAD_SYSTEM_NOISE_NAMES: readonly string[] = ${tsArray(p.systemNoiseNames, tsQuote)}`
}

// ── 标记段替换 ──
function replaceRegion(text, id, content, fileLabel) {
  const re = new RegExp(`(// <generated id="${id}">\\r?\\n)[\\s\\S]*?(\\r?\\n[ \\t]*// </generated>)`)
  if (!re.test(text))
    throw new Error(`${fileLabel} 缺少标记段 // <generated id="${id}">`)
  return text.replace(re, (_m, head, tail) => head + content + tail)
}

// ── 主流程 ──
let failures = 0
function apply(label, path, regions) {
  const before = readFileSync(path, 'utf8')
  let after = before
  for (const [id, content] of regions) after = replaceRegion(after, id, content, label)
  if (after === before) {
    console.log(`✓ ${label} 已是最新`)
    return
  }
  if (CHECK) {
    console.error(`✗ ${label} 与 upload-file-policy.json 不一致 —— 请跑 node scripts/generate/upload-policy.mjs`)
    failures++
  } else {
    writeFileSync(path, after)
    console.log(`✓ 已生成 ${label}`)
  }
}

apply('UploadFilePolicy.cs', CS_PATH, [
  ['categories', renderCsCategories(policy)],
  ['blacklist', renderCsBlacklist(policy)],
])
apply('upload-file-policy.ts', TS_PATH, [
  ['ext', renderTsExt(policy)],
  ['blacklist', renderTsBlacklist(policy)],
])

if (CHECK && failures > 0) process.exit(1)
console.log(CHECK ? '✓ 上传契约生成段与 JSON 一致' : '完成。')
