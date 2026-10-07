/**
 * status - 节点/行「启用 / 禁用」状态徽章的唯一判据（全站统一口径）
 *
 * 消费方：`YzhTree`（左树零绑定徽章）、`YzhTreeTableCheckSelector`（勾选树表 Name 列徽章）、
 * 直用 `el-tree` 的页面节点模板。三处共用本函数，禁止各自再写一份判据。
 *
 * 口径（与内核 `isRowEnabled` 同源）：
 * 1. 字段优先读 `Extra.<statusField>`，Extra 缺失/未注入时回退到节点顶层字段；
 *    双 Key 认读：PascalCase（`IsValid`）优先、camelCase（`isValid`）兜底（同 readNodeField 约定）。
 * 2. 启用判据：int 1 / bool true / 字符串 '1'、'true'（大小写不敏感），其余一律判停用。
 * 3. 字段缺失（undefined/null）→ 不标（无启停语义的树不受影响）。
 * 4. 文案为空串 → 该状态不标注（如长树只标停用节点）。
 *
 * 铁律九：启停唯一字段 = `IsValid`（IIsValid），组件默认 statusField 即 `'IsValid'`。
 */

export interface StatusBadge {
  text: string
  type: 'success' | 'info'
}

/** 双 Key 读取：PascalCase 优先、camelCase 兜底 */
function readField(source: Record<string, any> | null | undefined, field: string): any {
  if (!source) return undefined
  const camel = field.charAt(0).toLowerCase() + field.slice(1)
  return source[field] ?? source[camel]
}

/**
 * 解析启用/禁用徽章。
 *
 * @param data 节点或行数据（含 Extra 的树节点 / 含顶层字段的实体行）
 * @param statusField 状态字段名；空串/undefined = 关闭徽章
 * @param enabledText 启用文案（默认「启用」，空串 = 启用不标）
 * @param disabledText 停用文案（默认「禁用」，空串 = 停用不标）
 * @param extraField 扩展字段容器名（默认 `Extra`，与 YzhTree 的 extraField prop 对齐）
 * @returns 徽章描述；null = 该节点不渲染徽章
 */
export function resolveStatusBadge(
  data: Record<string, any> | null | undefined,
  statusField: string | undefined,
  enabledText = '启用',
  disabledText = '禁用',
  extraField = 'Extra',
): StatusBadge | null {
  if (!statusField || !data) return null
  const extra = readField(data, extraField)
  const raw = readField(extra, statusField) ?? readField(data, statusField)
  if (raw === undefined || raw === null) return null
  const enabled =
    raw === 1 || raw === true || raw === '1' || String(raw).toLowerCase() === 'true'
  const text = enabled ? enabledText : disabledText
  if (!text) return null
  return { text, type: enabled ? 'success' : 'info' }
}
