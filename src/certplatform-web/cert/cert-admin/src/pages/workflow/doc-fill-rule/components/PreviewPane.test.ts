/**
 * 中栏预览面板 —— 组件测试
 *
 * 【为什么测它】
 *   `PreviewPane` 是本页唯一「有状态策略」的组件：**看哪一份文件**由它决定，
 *   而它的失败模式全是**静默**的 ——
 *   · 自动切换失效 ⇒ 用户明明上传了模板，却一直看到含示例数据的原始件；
 *   · 换版不重挂 ⇒ 路径没变（`_template/x.docx` 同名覆盖），`DocPreview` 内部
 *     按路径做 key ⇒ 复用旧实例 ⇒ **看到上一版的 PDF**；
 *   · key 里漏掉 `fileName` ⇒ 契约异步回填「干净文件名」后不重挂 ⇒ **标题对、内容错**。
 *   这三种都不会抛异常，只会让人误判「我的模板没生效」。故用组件测试钉住。
 */
import { describe, it, expect } from 'vitest'
import { mount } from '@vue/test-utils'
import PreviewPane from './PreviewPane.vue'

/** 只保留「可断言」的渲染：真实 `DocPreview` 会去拉 PDF，测试里必须换掉 */
const stubs = {
  DocPreview: {
    name: 'DocPreview',
    props: ['file', 'downloadLabel'],
    // ★ 第 28 轮 C9：`PreviewPane` 通过 `#actions` 插槽把「上传空白模板」按钮送进来。
    //   桩**必须渲染这个插槽**，否则该按钮在测试里根本不会被解析 ——
    //   于是「插槽内容对不对」这件事就永远测不到（C9 等于没测）。
    template: '<div class="dp-stub"><slot name="actions" /></div>',
  },
  YzhEmptyState: {
    name: 'YzhEmptyState',
    props: ['icon', 'title'],
    template: '<div class="empty-stub" />',
  },
  'el-radio-group': {
    name: 'el-radio-group',
    props: ['modelValue'],
    emits: ['update:modelValue'],
    template: '<div class="rg"><slot /></div>',
  },
  'el-radio-button': { template: '<label class="rb"><slot /></label>', props: ['value'] },
  'el-icon': { template: '<i><slot /></i>' },
  'el-button': {
    name: 'el-button',
    props: ['type', 'size', 'icon', 'loading'],
    emits: ['click'],
    template: '<button class="eb" @click="$emit(\'click\')"><slot /></button>',
  },
}

const BASE = {
  fileCode: 'FILE-1',
  fileName: '质量手册.doc',
  originalPath: 'std/质量手册.doc',
  templatePath: '',
  templateFileName: '',
  hasTemplate: false,
}

function mountPane(over: Record<string, any> = {}) {
  return mount(PreviewPane, {
    props: { ...BASE, ...over },
    global: { stubs },
  })
}

/** 取预览渲染器（断言它实际收到了哪一份文件） */
function dp(wrapper: ReturnType<typeof mountPane>) {
  return wrapper.findComponent({ name: 'DocPreview' })
}

/**
 * 模拟用户点「查看：原始文档 / 空白模板」。
 *
 * ⚠️ stub 出来的 `el-radio-button` 不会驱动 `v-model`（真实切换逻辑在 Element Plus
 *    的 radio-group 内部），所以直接对 group 抛 `update:modelValue` —— 这正是
 *    `v-model` 编译出来的那条通路。
 */
async function switchSource(wrapper: ReturnType<typeof mountPane>, value: string) {
  wrapper.findComponent({ name: 'el-radio-group' }).vm.$emit('update:modelValue', value)
  await wrapper.vm.$nextTick()
}

describe('PreviewPane — 预览源自动切换', () => {
  it('未上传模板 ⇒ 不显示切换条，渲染原始文档，下载按钮是「下载原始件」', () => {
    const w = mountPane()
    expect(w.find('.source-bar').exists()).toBe(false)
    expect(dp(w).props('file')).toMatchObject({
      fileCode: 'FILE-1',
      storagePath: 'std/质量手册.doc',
    })
    expect(dp(w).props('downloadLabel')).toBe('下载原始件')
  })

  it('已上传模板 ⇒ 显示切换条，且**默认就是空白模板**', () => {
    const w = mountPane({
      hasTemplate: true,
      templatePath: '_template/x.docx',
      templateFileName: '质量手册.docx',
    })
    expect(w.find('.source-bar').exists()).toBe(true)
    expect(dp(w).props('file')).toMatchObject({
      storagePath: '_template/x.docx',
      fileName: '质量手册.docx',
    })
    // 模板没有 fileCode —— 传了会让 DocPreview 走错链路
    expect(dp(w).props('file').fileCode).toBeUndefined()
    expect(dp(w).props('downloadLabel')).toBe('下载模板')
  })

  it('★ 有模板路径但 hasTemplate=false ⇒ 切换条仍显示（有得看就让你看），但默认仍是原始件', () => {
    const w = mountPane({
      hasTemplate: false,
      templatePath: '_template/x.docx',
      templateFileName: '质量手册.docx',
    })
    expect(w.find('.source-bar').exists()).toBe(true)
    expect(dp(w).props('file').fileCode).toBe('FILE-1')
  })

  it('无 fileCode 且无模板 ⇒ 空态，⛔ 不渲染预览器', () => {
    const w = mountPane({ fileCode: '', originalPath: '' })
    expect(dp(w).exists()).toBe(false)
    expect(w.find('.empty-stub').exists()).toBe(true)
  })
})

describe('PreviewPane — 换版 / 换文件必须重挂', () => {
  it('★ showTemplate() 后切到模板（上传成功由父页显式调用）', async () => {
    const w = mountPane({
      hasTemplate: true,
      templatePath: '_template/x.docx',
      templateFileName: '质量手册.docx',
    })
    // 先手动切回原始件，模拟用户点了「原始文档」
    await switchSource(w, 'original')
    expect(dp(w).props('file').fileCode).toBe('FILE-1')
    const beforeUid = dp(w).vm.$.uid

    ;(w.vm as any).showTemplate()
    await w.vm.$nextTick()

    expect(dp(w).props('file').storagePath).toBe('_template/x.docx')
    // 路径没变也要重挂（换版 = 同路径覆盖）
    expect(dp(w).vm.$.uid).not.toBe(beforeUid)
  })

  it('★ refresh() 强制重挂（路径不变、字节变了）', async () => {
    const w = mountPane()
    const beforeUid = dp(w).vm.$.uid
    ;(w.vm as any).refresh()
    await w.vm.$nextTick()
    expect(dp(w).vm.$.uid).not.toBe(beforeUid)
  })

  it('★ key 里含 fileName：文件名后到（契约异步回填）也会重挂，避免「标题对、内容错」', async () => {
    const w = mountPane({ fileName: '质量手册.doc  ⬜未上传模板' })
    const beforeUid = dp(w).vm.$.uid
    await w.setProps({ fileName: '质量手册.doc' })
    expect(dp(w).vm.$.uid).not.toBe(beforeUid)
    expect(dp(w).props('file').fileName).toBe('质量手册.doc')
  })

  it('★ 换文件 ⇒ 回到该文件的默认源（不串台：上一个文件手动切过的源不带过来）', async () => {
    const w = mountPane({
      hasTemplate: true,
      templatePath: '_template/x.docx',
      templateFileName: '质量手册.docx',
    })
    // 用户手动切到「原始文档」
    await switchSource(w, 'original')
    expect(dp(w).props('file').fileCode).toBe('FILE-1')

    // 换一个同样「已上传模板」的文件 —— hasTemplate 不变 ⇒ 不能靠 hasTemplate 的 watch
    await w.setProps({
      fileCode: 'FILE-2',
      fileName: '程序文件.doc',
      templatePath: '_template/y.docx',
      templateFileName: '程序文件.docx',
    })
    expect(dp(w).props('file').storagePath).toBe('_template/y.docx')
    expect(dp(w).props('file').fileCode).toBeUndefined()
  })
})

describe('PreviewPane — 脚注文案', () => {
  it('有模板可切且当前看原始件 ⇒ 提示「可切换到空白模板」', () => {
    const w = mountPane({ templatePath: '_template/x.docx' })
    expect(w.find('.source-bar__hint').text()).toContain('可切换到「空白模板」')
  })

  it('当前看模板 ⇒ 明说「扫描出的锚点位置就在这份文件里」', () => {
    const w = mountPane({
      hasTemplate: true,
      templatePath: '_template/x.docx',
      templateFileName: 'x.docx',
    })
    expect(w.find('.source-bar__hint').text()).toContain('锚点位置')
  })

  it('⚠️ 已知：`sourceHint` 的第三分支（「上传后会自动切换」）当前**不可达**', () => {
    // 切换条只在 templateAvailable 时渲染，而那时前两个分支必命中 ⇒ 第三分支永远走不到。
    // 钉住这个事实：一旦有人把切换条改成常显，这条断言会失败并提醒他重新考虑文案。
    const w = mountPane({ templatePath: '' })
    expect(w.find('.source-bar').exists()).toBe(false)
    expect(w.text()).not.toContain('自动切换')
  })
})

describe('PreviewPane — ★ C9 上传模板按钮进 `#actions` 插槽', () => {
  /** 取插槽里那个按钮 */
  function uploadBtn(w: ReturnType<typeof mountPane>) {
    return dp(w).find('.eb')
  }

  it('允许上传 + 未上传过 ⇒ 文案「上传空白模板」，点击向父页抛 `upload-template`', async () => {
    const w = mountPane({ canUploadTemplate: true, hasTemplate: false })
    expect(uploadBtn(w).exists()).toBe(true)
    expect(uploadBtn(w).text()).toBe('上传空白模板')

    await uploadBtn(w).trigger('click')
    expect(w.emitted('upload-template')).toHaveLength(1)
  })

  it('允许上传 + 已上传过 ⇒ 文案变「重新上传模板」（同一个按钮，不新增入口）', () => {
    const w = mountPane({
      canUploadTemplate: true,
      hasTemplate: true,
      templatePath: '_template/x.docx',
      templateFileName: '质量手册.docx',
    })
    expect(uploadBtn(w).text()).toBe('重新上传模板')
  })

  it('★ 固定文档（`canUploadTemplate=false`）⇒ 插槽在但按钮不在（固定文档没有空白模板可传）', () => {
    const w = mountPane({ canUploadTemplate: false })
    // 插槽本身仍被提供（父页无条件给），但内容被 `v-if` 拦掉
    expect(dp(w).exists()).toBe(true)
    expect(uploadBtn(w).exists()).toBe(false)
  })

  it('★ 未选中文件 ⇒ 连预览器都不渲染，插槽自然无从谈起', () => {
    const w = mountPane({ fileCode: '', originalPath: '', canUploadTemplate: true })
    expect(dp(w).exists()).toBe(false)
    expect(w.find('.eb').exists()).toBe(false)
  })
})
