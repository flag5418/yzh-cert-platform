/**
 * 全局等待提示 + 多标签页 —— **机制层**单测
 *
 * 覆盖设计文档 `docs/40-实施/开发计划/AI任务与进度可见性统一设计-V1.md` §十 的判据 5/6/7。
 *
 * ⚠️ **为什么本文件放在 `cert-admin` 而不是 `yzh.vue.core`**：
 *    被测代码确实在 core（`yzh.vue.core/src/composables/`），但 **core 包没有测试设施**
 *    （`package.json` 无 `test` 脚本、无 `vitest.config.ts`、无自己的 `node_modules`）；
 *    而 `cert-admin` 是**唯一**装了 vitest + happy-dom 的工作区包，且已配 `@yzh-core` 别名。
 *    本文件只 import core 的**纯 composable**，⛔ 不依赖任何页面 / 组件。
 */

import { describe, it, expect, beforeEach, vi } from 'vitest'
import { defineComponent, h, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { useAsyncAction } from '@yzh-core/composables/useAsyncAction'
import { useGlobalPending, usePendingByScope } from '@yzh-core/composables/useGlobalPending'
import { useTabs, YZH_TAB_HOME_PATH } from '@yzh-core/composables/useTabs'

/**
 * 在**真实组件 setup** 里执行 `fn` —— `useAsyncAction` 内部要 `inject` 路由对象，
 * 而 `inject` 只能在 setup 同步执行期调用（在外面调会刷 `inject() can only be used inside setup()`）。
 *
 * ★ 顺带验证了「**没装 router 时静默降级**」：本 helper 挂载的组件没有 router，
 *   `inject(routeLocationKey, null)` 应返回 `null` 而**不刷警告**（见下方 `noWarn` 用例）。
 */
function withSetup<T>(fn: () => T): T {
  let result!: T
  const Comp = defineComponent({
    setup() {
      result = fn()
      return () => h('div')
    },
  })
  mount(Comp)
  return result
}

/** 可控 promise —— 用来制造「两个请求同时在跑」的时序 */
function deferred<T = void>() {
  let resolve!: (v: T) => void
  let reject!: (e: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

describe('useGlobalPending · 机制层', () => {
  const { pendings, count, begin, end, update, clearScope, clearAll } = useGlobalPending()

  beforeEach(() => clearAll())

  it('begin 登记 / end 撤销，count 跟随变化', () => {
    expect(count.value).toBe(0)
    const k = begin('正在分析：A.docx', { scope: '/a' })
    expect(count.value).toBe(1)
    expect(pendings.value[0].label).toBe('正在分析：A.docx')
    expect(pendings.value[0].scope).toBe('/a')
    end(k)
    expect(count.value).toBe(0)
  })

  it('end 幂等 —— key 不存在时无副作用（超时兜底与 finally 会各调一次）', () => {
    const k = begin('x')
    end(k)
    expect(() => end(k)).not.toThrow()
    expect(count.value).toBe(0)
  })

  it('update 只改 label / progress，⛔ 不动 key 与 scope', () => {
    const k = begin('旧文案', { scope: '/a' })
    update(k, { label: '新文案', progress: 40 })
    expect(pendings.value[0]).toMatchObject({ key: k, scope: '/a', label: '新文案', progress: 40 })
  })

  it('clearScope 只清指定页面（页面卸载兜底）', () => {
    begin('A 页任务', { scope: '/a' })
    begin('B 页任务', { scope: '/b' })
    clearScope('/a')
    expect(pendings.value.map((p) => p.scope)).toEqual(['/b'])
  })
})

describe('usePendingByScope · 遮罩按页面过滤（裁决 ②）', () => {
  const { begin, clearAll } = useGlobalPending()
  beforeEach(() => clearAll())

  it('只返回当前页面的等待 —— 切到别的页面即「看不到」', () => {
    begin('A 页任务', { scope: '/a' })
    begin('B 页任务', { scope: '/b' })

    // ⚠️ 必须用 `ref` 模拟 route（`useRoute()` 返回的就是响应式对象）——
    //    用普通对象 `{ path }` 的话 computed 收集不到依赖，会永远停在首次结果
    const current = ref('/a')
    const items = usePendingByScope(() => current.value)
    expect(items.value.map((p) => p.label)).toEqual(['A 页任务'])

    // ★ 切页 ⇒ 该页没有等待 ⇒ 不显示
    current.value = '/b'
    expect(items.value.map((p) => p.label)).toEqual(['B 页任务'])

    // ★ 切到一个「没有等待」的页面 ⇒ 空
    current.value = '/c'
    expect(items.value).toEqual([])
  })

  it('⛔ 传「值」而非 getter ⇒ 永远停在初始值（这是本 API 要求 getter 的理由）', () => {
    begin('A 页任务', { scope: '/a' })
    const current = ref('/a')
    const frozen = current.value // ★ 这就是「传值」的错误写法
    const byValue = usePendingByScope(() => frozen)
    current.value = '/c'
    expect(byValue.value).toHaveLength(1) // ⛔ 还停在 /a 的结果（对比上一个用例）
  })
})

describe('useAsyncAction · 并发计数（判据 5）', () => {
  const { count, clearAll } = useGlobalPending()
  beforeEach(() => clearAll())

  it('★ 两个请求交叠时，先结束的那个**不能**把 busy 置回 false', async () => {
    const { busy, run } = withSetup(() => useAsyncAction())
    const d1 = deferred()
    const d2 = deferred()

    const p1 = run(() => d1.promise, '任务 A')
    const p2 = run(() => d2.promise, '任务 B')
    expect(busy.value).toBe(true)
    expect(count.value).toBe(2)

    // 先结束 A —— 单 boolean 实现在这一行就会变成 false（按钮提前可点 ⇒ 重复发起）
    d1.resolve()
    await p1
    expect(busy.value).toBe(true)
    expect(count.value).toBe(1)

    d2.resolve()
    await p2
    expect(busy.value).toBe(false)
    expect(count.value).toBe(0)
  })

  it('fn 抛错也必须撤销等待（try/finally 不漏调）', async () => {
    const { busy, run } = withSetup(() => useAsyncAction())
    await expect(run(async () => { throw new Error('boom') }, '会失败的任务')).rejects.toThrow('boom')
    expect(busy.value).toBe(false)
    expect(count.value).toBe(0)
  })

  it('`{ global: false }` 只做并发计数，⛔ 不登记全局等待', async () => {
    const { busy, run } = withSetup(() => useAsyncAction({ global: false }))
    const d = deferred()
    const p = run(() => d.promise, '不该出现')
    expect(busy.value).toBe(true)
    expect(count.value).toBe(0) // ★ 关键
    d.resolve()
    await p
    expect(busy.value).toBe(false)
  })

  it('超时兜底：请求卡死时等待也会被清掉（R2 风险处置）', async () => {
    vi.useFakeTimers()
    try {
      const { busy, run } = withSetup(() => useAsyncAction({ timeout: 1000 }))
      const never = new Promise<void>(() => {})
      void run(() => never, '卡死的任务')
      expect(busy.value).toBe(true)
      expect(count.value).toBe(1)

      vi.advanceTimersByTime(1000)
      // 遮罩不再显示（等待被撤销）—— 但 busy 仍为 true（请求确实还在跑）
      expect(count.value).toBe(0)
      expect(busy.value).toBe(true)
    } finally {
      vi.useRealTimers()
    }
  })
})

describe('useAsyncAction · 无 router 上下文要静默降级（单测/纯逻辑类场景）', () => {
  const { pendings, clearAll } = useGlobalPending()
  beforeEach(() => clearAll())

  it('★ 拿不到路由对象时**不刷 injection 警告**，scope 退化为 ""', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    try {
      // `withSetup` 挂载的组件**没有装 router** —— 正是 prompt-template 单测的处境
      const { run } = withSetup(() => useAsyncAction())
      const d = deferred()
      const p = run(() => d.promise, '无路由上下文')

      // ⚠️ 必须在**运行期间**查（run 的 finally 会把它撤销掉）
      expect(pendings.value).toHaveLength(1)
      expect(pendings.value[0].scope).toBe('')

      d.resolve()
      await p
      expect(warn.mock.calls.map((c) => String(c[0])).join('\n')).not.toContain('inject')
    } finally {
      warn.mockRestore()
    }
  })
})

describe('useTabs · 多标签页（判据 6/7）', () => {
  const { tabs, activePath, open, close, closeOthers, closeRight, closeAll, retitle } = useTabs()

  /**
   * ⚠️ 本套用例**不 open('/')**（首页标签不可关闭，`closeAll` 清不掉 ⇒ 会污染后续用例）。
   *    唯一 open('/') 的用例排在**最后**，见本 describe 末尾。
   */
  beforeEach(() => {
    closeAll()
  })

  it('★ open 幂等 —— 同一 path 点两次只有一个标签（用户裁决「打开的标签不能重复」）', () => {
    open('/workflow/a', '规则 A')
    open('/workflow/a', '规则 A')
    open('/workflow/a', '另一个标题也不该新增')
    expect(tabs.value).toHaveLength(1)
    expect(tabs.value[0].title).toBe('规则 A') // ⛔ 不覆盖已有标题（标题由 retitle 负责）
  })

  it('open 空 path 直接忽略（防脏数据进标签栏）', () => {
    open('', '空')
    expect(tabs.value).toHaveLength(0)
  })

  it('open 同时把 activePath 指过去', () => {
    open('/a', 'A')
    open('/b', 'B')
    expect(activePath.value).toBe('/b')
  })

  it('close 当前标签 ⇒ 落到右邻；没有右邻 ⇒ 落到左邻', () => {
    open('/a', 'A')
    open('/b', 'B')
    open('/c', 'C')

    open('/b', 'B') // active = /b
    expect(close('/b')).toBe('/c') // 右邻
    expect(tabs.value.map((t) => t.path)).toEqual(['/a', '/c'])

    // ⚠️ `close` **不改** `activePath`（跳转由调用方 `router.push` 完成，见 `YzhTabsBar`）
    //    ⇒ 这里必须再 `open` 一次把 active 指回被关的那个，才是在测「关当前标签」
    open('/c', 'C')
    expect(close('/c')).toBe('/a') // 已无右邻 ⇒ 左邻
    expect(tabs.value.map((t) => t.path)).toEqual(['/a'])
  })

  it('close 非当前标签 ⇒ 返回 null（不需要跳转）', () => {
    open('/a', 'A')
    open('/b', 'B')
    open('/b', 'B')
    expect(close('/a')).toBeNull()
    expect(activePath.value).toBe('/b')
  })

  it('closeOthers 保留指定标签', () => {
    open('/a', 'A')
    open('/b', 'B')
    open('/c', 'C')
    closeOthers('/b')
    expect(tabs.value.map((t) => t.path)).toEqual(['/b'])
  })

  it('closeRight 保留指定标签及其左侧', () => {
    open('/a', 'A')
    open('/b', 'B')
    open('/c', 'C')
    open('/d', 'D')
    closeRight('/b')
    expect(tabs.value.map((t) => t.path)).toEqual(['/a', '/b'])
  })

  it('closeAll 清空全部（无固定页时）并返回落脚点', () => {
    open('/a', 'A')
    open('/b', 'B')
    expect(closeAll()).toBe(YZH_TAB_HOME_PATH)
    expect(tabs.value).toHaveLength(0)
  })

  it('retitle 回填菜单名（菜单异步加载 ⇒ 首次 open 只有 path 占位）', () => {
    open('/a', '/a')
    retitle('/a', '文档填写规则')
    expect(tabs.value.find((t) => t.path === '/a')?.title).toBe('文档填写规则')
    // 空标题不覆盖
    retitle('/a', '')
    expect(tabs.value.find((t) => t.path === '/a')?.title).toBe('文档填写规则')
  })

  // ⚠️ 必须放在**最后**：它会留下一个不可关闭的首页标签，`closeAll()` 清不掉
  it('★ 首页标签不可关闭 —— 否则「关闭全部」后没有落脚点', () => {
    open(YZH_TAB_HOME_PATH, '首页')
    open('/a', 'A')
    expect(tabs.value[0].closable).toBe(false)
    expect(close(YZH_TAB_HOME_PATH)).toBeNull() // 关不掉
    closeAll()
    expect(tabs.value.map((t) => t.path)).toEqual([YZH_TAB_HOME_PATH])
  })
})
