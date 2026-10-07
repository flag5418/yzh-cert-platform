/**
 * 全局规则 Tab · chips 展示文本（2026-10-06 修 `[object Object]`）
 *
 * 【根因】AI 落库的是**对象数组**，且两类元素的键名**不同**：
 *   · 业务标签 `{tagCode, tagName, confidence, reason}`
 *     （`PromptWorkbenchService.OutputSchemaJson` 的 `tags[]`）
 *   · 关键信息项 `{itemName, itemDesc, valueType, isKey}`（同上 `infoItems[]`）
 *     ⚠️ 首版只认 `tagName/name` ⇒ 信息项照样渲染成 `[object Object]`。
 *   而人工 `addTag`/`addItem` 存的是纯字符串 —— 两种形态必须**同一函数**兜住。
 *
 * ⛔ 字段名不改（用户裁决：只改显示），大小写两套都认
 *   （后端 PascalCase / AI 提示词 camelCase）。
 */

/** 按序取第一个命中的可读键（命中顺序 = 业务优先级） */
const LABEL_KEYS = [
  'tagName',
  'TagName',
  'itemName',
  'ItemName',
  'name',
  'Name',
  'label',
  'Label',
  'title',
  'Title',
  'tagCode',
  'TagCode',
  'code',
  'Code',
]

function firstString(v: unknown): string {
  if (typeof v === 'string' && v.trim()) return v
  return ''
}

/** chips 上该显示什么（字符串原样、对象取名、实在没名则摊开 JSON） */
export function labelOf(t: unknown): string {
  if (typeof t === 'string') return t
  if (t && typeof t === 'object') {
    const o = t as Record<string, unknown>
    for (const k of LABEL_KEYS) {
      const s = firstString(o[k])
      if (s) return s
    }
    // 兜底①：对象里第一个非空字符串值（如 `{Required, Hint}` 这种无名形态）
    for (const v of Object.values(o)) {
      const s = firstString(v)
      if (s) return s
    }
    // 兜底②：一条可读字符串都没有 ⇒ 摊成紧凑 JSON，⛔ 绝不落到 `[object Object]`
    try {
      return JSON.stringify(t)
    } catch {
      return ''
    }
  }
  if (typeof t === 'number' || typeof t === 'boolean') return String(t)
  return String(t ?? '')
}
