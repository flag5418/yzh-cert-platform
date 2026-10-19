export function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B'
  const k = 1024
  const sizes = ['B', 'KB', 'MB', 'GB', 'TB']
  const i = Math.floor(Math.log(bytes) / Math.log(k))
  return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i]
}

/**
 * 解析后端日期字符串 → Date（引擎无关）。
 *
 * 后端统一输出 `yyyy-MM-dd HH:mm:ss`（**空格**分隔，见契约表 §3.7），
 * 而 ECMAScript **只保证** ISO-8601（`T` 分隔）被各引擎接受，空格分隔属实现扩展。
 * 实测三大引擎（V8 / SpiderMonkey / JavaScriptCore）均接受空格，
 * 但那是扩展行为、不是规范保证 —— 这里把首个空格规范成 `T`（无时区的 ISO 本地时间）
 * 后再解析，把「靠引擎好心」变成「靠显式契约」。
 *
 * ★ 与后端契约表 §3.7 同源；后端日期格式**唯一**为 `yyyy-MM-dd HH:mm:ss`。
 */
export function toLocalDate(input: string | Date | null | undefined): Date {
  if (input instanceof Date) return input
  const s = String(input ?? '').trim()
  // `yyyy-MM-dd HH:mm:ss(.fff)?` → `yyyy-MM-ddTHH:mm:ss(.fff)?`（仅首个空格，不动时区后缀）
  const normalized = /^\d{4}-\d{2}-\d{2} /.test(s) ? s.replace(' ', 'T') : s
  return new Date(normalized)
}

function pad2(n: number): string {
  return n < 10 ? `0${n}` : String(n)
}

/**
 * 日期 → `yyyy-MM-dd`。
 * ⛔ 不用 `toLocaleDateString()`：输出随系统区域漂移（中文系统给 `2026/10/19`），
 *   与后端/选值控件（`value-format="YYYY-MM-DD"`）不一致。手工拼装才稳定。
 */
export function formatDate(date: string | Date): string {
  if (!date) return ''
  const d = toLocalDate(date)
  if (Number.isNaN(d.getTime())) return String(date)
  return `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())}`
}

/**
 * 日期时间 → `yyyy-MM-dd HH:mm:ss`（★ 与后端契约表 §3.7 逐字一致）。
 * 同 `formatDate`：不用 `toLocaleString()`，避免区域漂移。
 */
export function formatDateTime(date: string | Date): string {
  if (!date) return ''
  const d = toLocalDate(date)
  if (Number.isNaN(d.getTime())) return String(date)
  return (
    `${d.getFullYear()}-${pad2(d.getMonth() + 1)}-${pad2(d.getDate())} ` +
    `${pad2(d.getHours())}:${pad2(d.getMinutes())}:${pad2(d.getSeconds())}`
  )
}
