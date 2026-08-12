/**
 * certcore API 响应解包工具
 * 统一处理 Vol 后端响应四态（Status/status/Data/data），消灭全站散落的判断
 */

/** 是否成功：Status===true 或 status===0 或 status===true */
export function isOk(res) {
  if (!res) return false
  return res.Status === true || res.status === 0 || res.status === true
}

/** 取数据：Data / data / 原对象 */
export function getData(res) {
  if (!res) return null
  return res.Data !== undefined && res.Data !== null ? res.Data
    : (res.data !== undefined && res.data !== null ? res.data : res)
}

/** 取消息：Message / message / '' */
export function getMessage(res) {
  if (!res) return ''
  return res.Message || res.message || ''
}

/** 统一解包：{ ok, data, message } */
export function unwrap(res) {
  return {
    ok: isOk(res),
    data: getData(res),
    message: getMessage(res)
  }
}

/** PascalCase → camelCase 字段复制（仅复制存在的字段） */
export function pickCamel(source, mapping) {
  if (!source) return source
  const out = { ...source }
  for (const [from, to] of Object.entries(mapping || {})) {
    if (source[from] !== undefined) out[to] = source[from]
  }
  return out
}
