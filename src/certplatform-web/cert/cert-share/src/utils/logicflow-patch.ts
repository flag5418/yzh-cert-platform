/**
 * LogicFlow v2 + Preact CSSStyleDeclaration 兼容性补丁
 *
 * 根因：LogicFlow v2 使用 Preact 渲染 SVG。Preact 的 setProperty(props.js) 在处理
 * SVG 元素的 style 属性时，会用 for...in 遍历 style 对象的所有可枚举属性。
 * 如果 style 值是 CSSStyleDeclaration 实例（array-like），Preact 会拿到数字索引
 * "0","1",... 然后调用 setStyle(style, "0", value)，最终执行 style["0"] = value。
 * SVG 的 CSSStyleDeclaration 不支持 indexed setter → TypeError。
 *
 * 实测（Chrome 130 / Freebuff WebView）：即便通过 defineProperty 在实例/原型上
 * 安装了 "0".."99" 的 accessor 拦截器，原生 Object.assign(style, {0: x}) 仍在
 * 引擎内部触发 "Failed to set an indexed property [0]" —— 引擎层拦截不可行。
 *
 * 修复策略（v2，实测有效）：
 * 1. 包装全局 Object.assign：target 为 CSSStyleDeclaration 时过滤源对象中的
 *    纯数字键（这是 Preact diff 复制 style 对象的入口）
 * 2. 将 CSSStyleDeclaration 原型上所有数字索引属性设为不可枚举（阻止 for...in 遍历）
 * 3. 安装通用 indexed setter（防御性，万一仍有代码尝试 style[i] = x）
 * 4. 包装 setProperty 使其对无效属性静默失败
 */

let patched = false

export function installLogicFlowPatch(): void {
  if (patched || typeof window === 'undefined') return
  patched = true

  try {
    // ── 策略 1: 包装 Object.assign，过滤 CSSStyleDeclaration 目标的数字键 ──
    const origAssign = Object.assign
    Object.assign = function (target: any, ...sources: any[]) {
      if (target instanceof CSSStyleDeclaration) {
        sources = sources.map((src) => {
          if (src && typeof src === 'object') {
            const cleaned: Record<string, any> = {}
            for (const k in src) {
              if (!/^\d+$/.test(k)) cleaned[k] = src[k]
            }
            return cleaned
          }
          return src
        })
      }
      return origAssign.apply(Object, [target, ...sources])
    } as typeof Object.assign

    const proto = CSSStyleDeclaration.prototype as any
    if (proto && !proto.__logicflow_patched) {
      Object.defineProperty(proto, '__logicflow_patched', { value: true, writable: false })

      // ── 策略 2: 将 CSSStyleDeclaration 实例上可能存在的数字索引属性设为不可枚举 ──
      // 这阻止 Preact 的 for...in 循环拿到 "0","1",... 等数字键
      const defineIndexProtector = (obj: any) => {
        for (let i = 0; i < 100; i++) {
          const key = String(i)
          try {
            Reflect.defineProperty(obj, key, {
              get() { return '' },
              set(_v: string) { /* 静默忽略 Preact 的 style[i] = value */ },
              enumerable: false,
              configurable: true
            })
          } catch { break }
        }
      }

      // 对原型安装（所有实例继承）
      defineIndexProtector(proto)

      // ── 策略 3: 包装 setProperty，对无效属性静默失败 ──
      const origSetProperty = proto.setProperty
      if (origSetProperty) {
        proto.setProperty = function (name: string, value: string, priority?: string) {
          try {
            if (priority) {
              origSetProperty.call(this, name, value, priority)
            } else {
              origSetProperty.call(this, name, value)
            }
          } catch {
            // 忽略无效的 CSS 属性（如 Preact 传入的数字键被误解析为属性名）
          }
        }
      }
    }
  } catch {
    // 非关键补丁，静默失败
  }
}
