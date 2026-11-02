/**
 * 全局字典池（`Sys_Dictionary.DicNo` → 选项）
 *
 * 为什么要有它：
 * - `YzhForm` 早已按 `dictCode` 拉字典，但 `YzhTable` 的 `dictCode` 分支一直是**空壳**
 *   （只把原始值渲染出来）⇒ 列表里 0/1 原样上屏，与编辑弹窗显示不一致。
 * - 同一个字典会被表格列 + 表单字段同时消费，**必须共用一份缓存**，
 *   否则一次打开页面会重复打 N 次 `/api/System/Dictionary/items/by-no/{dicNo}`。
 *
 * 契约（与 20-前后端契约权威表一致）：
 * - 接口：`GET /api/System/Dictionary/items/by-no/{dicNo}`，信封 `{success,data,err}`；
 * - 载荷 **PascalCase**：`{ Value, Label, Code?, Color? }`（DB 列 = C# 属性 = TS 字段）；
 * - `Value` 是 **字符串**（`Sys_DictionaryList.DicValue` 是 varchar），
 *   而业务列常是 int（如 `cert_audit_member.Gender`）⇒ 比对一律走 `String(value)` 松散匹配，
 *   表单侧再用 `coerceDictValue` 按字段 schema 类型对齐，避免 el-select 严格比较不命中。
 */

import { reactive } from 'vue'
import { yzhApi } from '../api/client'

/** 字典项（载荷字段名保持后端 PascalCase） */
export interface YzhDictItem {
  Value: string
  Label: string
  Code?: string
  Color?: string
}

/** DicNo → 选项（reactive：加载完成后表格/表单自动重渲染） */
const dictCache = reactive<Record<string, YzhDictItem[]>>({})

/** 进行中的请求（并发去重：同一字典同时只发一次） */
const inflight = new Map<string, Promise<YzhDictItem[]>>()

/** 加载失败的字典（避免无限重试；`force` 可强制重试） */
const failed = reactive<Record<string, string>>({})

/** 只读快照（未加载时返回 undefined，渲染侧据此回落原值） */
export function getDictItems(dicNo: string | undefined | null): YzhDictItem[] | undefined {
  if (!dicNo) return undefined
  return dictCache[dicNo]
}

/** 按 DicValue 取显示文本；未命中/未加载时回落原值（⛔ 不要抛错，列表不能因字典失败而空白） */
export function dictLabelOf(dicNo: string | undefined | null, value: any): any {
  const items = getDictItems(dicNo)
  if (!items || value === null || value === undefined) return value
  const key = String(value)
  const hit = items.find((it) => String(it.Value) === key)
  return hit ? hit.Label : value
}

/**
 * 拉取字典项（带缓存 + 并发去重）。
 * 失败时**不抛错到渲染路径**：记录到 `failed` 并返回空数组，调用方自行决定是否提示。
 */
export async function loadDictItems(dicNo: string, force = false): Promise<YzhDictItem[]> {
  if (!dicNo) return []
  if (!force) {
    const cached = dictCache[dicNo]
    if (cached) return cached
    const p = inflight.get(dicNo)
    if (p) return p
  }
  const task = (async () => {
    try {
      const res = await yzhApi.get<{
        success?: boolean
        data?: YzhDictItem[]
        err?: string
      }>(`/api/System/Dictionary/items/by-no/${encodeURIComponent(dicNo)}`)
      if (!res?.success || !Array.isArray(res.data)) {
        throw new Error(res?.err || `字典【${dicNo}】加载失败`)
      }
      dictCache[dicNo] = res.data.map((o) => ({
        Value: o.Value,
        Label: o.Label,
        Code: o.Code,
        Color: o.Color,
      }))
      delete failed[dicNo]
      return dictCache[dicNo]
    } catch (e: any) {
      // ⚠️ 失败时**不写** dictCache：写空数组会让「网络抖一下」永久失去翻译能力
      //    （渲染侧只按 dictCodes 变化触发一次加载，不会反复重试打接口）。
      failed[dicNo] = e?.message || String(e)
      return []
    } finally {
      inflight.delete(dicNo)
    }
  })()
  inflight.set(dicNo, task)
  return task
}

/** 失败原因（无失败返回 undefined） */
export function getDictError(dicNo: string): string | undefined {
  return failed[dicNo]
}

/**
 * 把字典 `Value`（字符串）对齐到实体列类型。
 *
 * 为什么必须做：`cert_audit_member.Gender` 是 **int**，而字典 `DicValue` 是 `'0'/'1'`。
 * `el-select` 用 `===` 比较 `modelValue` 与 `el-option.value` ⇒ `0 !== '0'`，
 * 弹窗里会显示原始值 0/1 而不是「男/女」，且下拉看起来「没有选中项」。
 *
 * @param value      字典原始值（字符串）
 * @param schemaType 后端 `EntityFieldSchema.Type`：string | number | boolean | datetime
 */
export function coerceDictValue(
  value: string,
  schemaType?: string
): string | number | boolean {
  if (schemaType === 'number') {
    const n = Number(value)
    return Number.isNaN(n) ? value : n
  }
  if (schemaType === 'boolean') {
    return value === '1' || value.toLowerCase() === 'true'
  }
  return value
}

/** 清空缓存（登出 / 切换工作区时调用，避免字典被上一个租户污染） */
export function clearDictCache() {
  Object.keys(dictCache).forEach((k) => delete dictCache[k])
  Object.keys(failed).forEach((k) => delete failed[k])
  inflight.clear()
}
