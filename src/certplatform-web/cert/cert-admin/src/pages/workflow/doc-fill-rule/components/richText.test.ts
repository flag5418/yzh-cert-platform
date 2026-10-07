/**
 * 帮助文案富文本分段 —— 纯函数单测
 *
 * 【为什么测它】
 *   这个坑已经踩过两次（`PreviewPane` 脚注、帮助浮层），共同点都是
 *   **把带标记的字符串直接塞进 `{{ }}`**。抽出纯函数后就能把它钉死：
 *   以后谁改了 `HELP` 的写法，这里会先红。
 */
import { describe, expect, it } from 'vitest'
import { parseBold } from './richText'

describe('parseBold — `**` 加粗分段', () => {
  it('无标记 ⇒ 单段纯文本', () => {
    expect(parseBold('锁定表示这份配置已确定')).toEqual([
      { t: '锁定表示这份配置已确定' },
    ])
  })

  it('★ 单个标记在中间 ⇒ 拆成「前 / 加粗 / 后」三段', () => {
    expect(parseBold('仅**可编辑文档**适用')).toEqual([
      { t: '仅' },
      { t: '可编辑文档', b: true },
      { t: '适用' },
    ])
  })

  it('标记在开头 / 结尾 / 独占整串', () => {
    expect(parseBold('**前**后')).toEqual([{ t: '前', b: true }, { t: '后' }])
    expect(parseBold('前**后**')).toEqual([{ t: '前' }, { t: '后', b: true }])
    expect(parseBold('**整串**')).toEqual([{ t: '整串', b: true }])
  })

  it('多个标记 ⇒ 依次拆开，顺序不变', () => {
    expect(parseBold('**字段 / 表格**两组，点一条即打开它的配置**抽屉**。')).toEqual([
      { t: '字段 / 表格', b: true },
      { t: '两组，点一条即打开它的配置' },
      { t: '抽屉', b: true },
      { t: '。' },
    ])
  })

  it('★ 剥离标记后拼回来 === 去掉 `**` 的原文（防止「吃掉字符」）', () => {
    const src = '锁定后**不能再修改配置**，换模板重扫时会**保留**。'
    const joined = parseBold(src)
      .map((s) => s.t)
      .join('')
    expect(joined).toBe(src.replace(/\*\*/g, ''))
  })

  it('空串 ⇒ 空数组（⛔ 不产出空文本段，否则模板会多渲染一个空节点）', () => {
    expect(parseBold('')).toEqual([])
  })

  it('⛔ 不抛异常：单个星号、未闭合、畸形写法一律原样保留', () => {
    // 单个星号不是标记
    expect(parseBold('*斜体* 不支持')).toEqual([{ t: '*斜体* 不支持' }])
    // 未闭合 ⇒ 全部当纯文本
    expect(parseBold('**没闭合')).toEqual([{ t: '**没闭合' }])
    // 畸形（尾部多余 `**`）⇒ 认了前面那对，尾巴原样留着 —— 最多是不加粗，⛔ 不该打挂页面
    expect(parseBold('**a**b**')).toEqual([{ t: 'a', b: true }, { t: 'b**' }])
    // 空标记 `****` ⇒ 不是 `**x**`，原样
    expect(parseBold('****')).toEqual([{ t: '****' }])
  })

  it('防御：null / undefined / 非字符串不抛异常', () => {
    expect(parseBold(undefined as unknown as string)).toEqual([])
    expect(parseBold(null as unknown as string)).toEqual([])
    expect(parseBold(123 as unknown as string)).toEqual([{ t: '123' }])
  })

  it('★ 本页 `HELP` 的每一条都能被正确分段（回归：真实配置不得再漏标记）', () => {
    // 直接照抄 index.vue 里出现过的两条原文，钉住真实用法
    const samples = [
      '把空白模板里的 {{标签}} 与「值从哪来」绑定。仅**可编辑文档**适用。',
      '锁定表示这份配置**已确定** —— 锁定后**不能再修改配置**，换模板重扫时会保留。',
      '在**顶栏**切换；图片 / PDF 无法解析 ⇒ 固定文档，类型置灰锁定。',
    ]
    for (const s of samples) {
      const segs = parseBold(s)
      // 每段都非空
      expect(segs.every((x) => x.t !== '')).toBe(true)
      // 渲染出来绝不能残留星号
      expect(segs.map((x) => x.t).join('')).not.toContain('*')
    }
  })
})
