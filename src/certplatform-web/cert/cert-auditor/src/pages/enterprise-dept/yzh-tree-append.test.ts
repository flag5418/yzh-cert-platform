/**
 * YzhTree.appendNode —— 「追加子节点后立刻可见」回归测试
 *
 * 2026-10-30 用户实测：企业机构管理页（/enterprise-dept）底部「新增部门」保存成功后，
 * 左侧树看不到新部门。根因在 el-tree 的两条内部机制：
 *   ① 懒加载把 `props.isLeaf` 的返回值缓存成 `Node.isLeafByUser`，之后改 `data.IsLeaf`
 *      不再生效（`updateLeafState` 在该分支直接 return）⇒ 原先是叶子的父节点继续按
 *      叶子渲染，**连展开箭头都没有**；
 *   ② `append` 只往父节点 `childNodes` 里塞，父节点未展开 ⇒ 新节点收着看不见。
 *
 * 本文件断言的是「渲染结果」：追加后新节点文字必须出现在树里。
 */

import { describe, it, expect } from 'vitest'
import { nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import YzhTree from '@yzh-core/components/layout/YzhTree.vue'

function makeRoot() {
  return {
    Code: 'E1',
    Name: '示例企业',
    ParentCode: null,
    NodeType: 'enterprise',
    IsLeaf: true,
    Extra: {},
    Children: [],
  }
}

function makeChild() {
  return {
    Code: 'D1',
    Name: '质量部',
    ParentCode: 'E1',
    NodeType: 'dept',
    IsLeaf: true,
    Extra: {},
    Children: [],
  }
}

describe('YzhTree.appendNode', () => {
  it('父节点原先是叶子时，追加后自动变可展开并显示新节点', async () => {
    const wrapper = mount(YzhTree, {
      props: { data: [makeRoot()], statusField: '' },
    })
    await nextTick()

    expect(wrapper.text()).toContain('示例企业')
    expect(wrapper.text()).not.toContain('质量部')

    ;(wrapper.vm as any).appendNode('E1', makeChild())
    await nextTick()
    await nextTick()

    expect(wrapper.text()).toContain('质量部')
  })

  it('根级追加（parentCode=null）同样可见，且只写一份到 data', async () => {
    const data: any[] = []
    const wrapper = mount(YzhTree, { props: { data, statusField: '' } })
    await nextTick()

    ;(wrapper.vm as any).appendNode(null, makeChild())
    await nextTick()

    expect(wrapper.text()).toContain('质量部')
    expect(data.map((n: any) => n.Code)).toEqual(['D1'])
  })
})
