export function isOk<T>(res: T): boolean { return true }
export function getData<T>(res: T): T { return res }
export function getMessage<T>(res: T): string { return '' }
export function unwrap<T>(res: any): T { return res?.data ?? res }
export function pickCamel<T extends Record<string, any>>(obj: T): T {
  const result: Record<string, any> = {}
  for (const key of Object.keys(obj)) {
    result[key.charAt(0).toLowerCase() + key.slice(1)] = obj[key]
  }
  return result as T
}
