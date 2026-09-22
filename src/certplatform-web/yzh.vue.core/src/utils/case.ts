/**
 * 大小写转换工具（AD/内核共用纯函数）
 */

/** PascalCase → camelCase */
export function toCamelCase(name: string): string {
  if (!name) return name
  if (name[0] >= 'a' && name[0] <= 'z') return name
  return name[0].toLowerCase() + name.slice(1)
}

/** camelCase → PascalCase */
export function toPascalCase(name: string): string {
  if (!name) return name
  if (name[0] >= 'A' && name[0] <= 'Z') return name
  return name[0].toUpperCase() + name.slice(1)
}

/** formData（camelCase key）→ 提交给后端（PascalCase key） */
export function pascalCaseFormData(
  formData: Record<string, any>,
): Record<string, any> {
  const result: Record<string, any> = {}
  for (const [k, v] of Object.entries(formData)) {
    result[toPascalCase(k)] = v
  }
  return result
}

/** 后端返回行（PascalCase）→ formData（camelCase） */
export function rowToFormData(row: Record<string, any>): Record<string, any> {
  const result: Record<string, any> = {}
  for (const [k, v] of Object.entries(row)) {
    result[toCamelCase(k)] = v
  }
  return result
}
