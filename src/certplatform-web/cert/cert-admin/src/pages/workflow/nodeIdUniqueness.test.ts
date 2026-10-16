/**
 * 节点 ID 唯一性回归测试（2026-10-15）
 *
 * 缺陷现场：AI 节点测试 POST /api/Admin/Workflow/test/ai-node 返回 400
 * `{"err":"AI 节点执行失败: An item with the same key has already been added. Key: ai_node_n1"}`。
 *
 * 根因链：
 *  1. `serializer.deserialize` 在 map 里**逐节点**调用 `idGenerator.resetFromNodes([{id}])`，
 *     而该方法先 clear 全表 ⇒ 循环结束后计数器只剩「最后一个合法节点」那一档；
 *  2. `WorkflowDesigner.renderWorkflow` 又把 `store.state.idGenerator` 覆盖成这个坏生成器
 *     ⇒ 新增 AI 节点时 `next('ai_node')` 从 0 起算，撞出已存在的 `ai_node_n1`；
 *  3. store 里两个节点同 id ⇒ 画布与 store 分叉（LogicFlow 自造 UUID）⇒
 *     落库 rule_json 带重复 nodeId ⇒ 后端 `ToDictionary(n => n.NodeId)` 直接抛。
 *
 * 本测试锁定三层防线：deserialize 计数器播种 / store.addNode 唯一性兜底 /
 * loadFromData 去重 + topology 重复 ID 校验。
 */
import { describe, expect, it } from 'vitest'
import { deserialize } from '@share/composables/workflow/serializer'
import { useWorkflowStore } from '@share/composables/workflow/useWorkflowStore'
import { buildDependencyGraph, validateTopology } from '@share/composables/workflow/topology'
import { NodeIdGenerator } from '@share/composables/workflow/nodeIdGenerator'

/** 事故规则的真实骨架：最后一个节点是 end（而非 ai_node） */
const RULE_JSON_SKELETON = {
  version: 1,
  workflowType: 'validation',
  nodes: [
    { nodeId: 'start_n1', nodeType: 'start', title: '开始' },
    { nodeId: 'constant_n1', nodeType: 'constant', title: '常量' },
    { nodeId: 'ai_node_n1', nodeType: 'ai_node', title: 'AI 节点' },
    { nodeId: 'end_n1', nodeType: 'end', title: '结束' }
  ],
  edges: [
    { source: 'start_n1', target: 'ai_node_n1' },
    { source: 'constant_n1', target: 'ai_node_n1', targetHandle: 'input' },
    { source: 'ai_node_n1', target: 'end_n1' }
  ],
  outputConfig: {},
  glossary: ''
}

describe('serializer.deserialize — idGenerator 计数器播种', () => {
  it('★ 缺陷本体：加载后 next(ai_node) 必须是 ai_node_n2，不得撞已存在的 ai_node_n1', () => {
    const { idGenerator } = deserialize(RULE_JSON_SKELETON)
    // 修复前：逐节点 reset 导致计数器只剩 {end:1} ⇒ next('ai_node') === 'ai_node_n1'
    expect(idGenerator.next('ai_node')).toBe('ai_node_n2')
  })

  it('所有类别的计数器都被保留（不因最后一个节点是 end 而丢失）', () => {
    const { idGenerator } = deserialize(RULE_JSON_SKELETON)
    expect(idGenerator.getCurrentMax('start')).toBe(1)
    expect(idGenerator.getCurrentMax('constant')).toBe(1)
    expect(idGenerator.getCurrentMax('ai_node')).toBe(1)
    expect(idGenerator.getCurrentMax('end')).toBe(1)
  })

  it('迁移旧 ID（UUID/时间戳）时不得与已有合法 ID 相撞，且边引用同步重映射', () => {
    const config = {
      ...RULE_JSON_SKELETON,
      nodes: [
        ...RULE_JSON_SKELETON.nodes.slice(0, 3),
        { nodeId: 'a1fd68b3-a2c2-442b-978a-0a5462acccf0', nodeType: 'ai_node', title: 'AI 节点 2' },
        RULE_JSON_SKELETON.nodes[3]
      ],
      edges: [
        ...RULE_JSON_SKELETON.edges.slice(0, 3),
        { source: 'docTable_n1', target: 'a1fd68b3-a2c2-442b-978a-0a5462acccf0' }
      ]
    }
    const { nodes, edges, migrated, oldToNew } = deserialize(config)
    expect(migrated).toBe(true)
    const migratedId = oldToNew['a1fd68b3-a2c2-442b-978a-0a5462acccf0']
    expect(migratedId).toBe('ai_node_n2')
    // 迁移结果不得与任何原有 ID 重复
    const ids = nodes.map(n => n.id)
    expect(new Set(ids).size).toBe(ids.length)
    // 悬挂边（docTable_n1 不在 nodes 里）被过滤，重映射后的边引用存在的节点
    expect(edges.every(e => ids.includes(e.source) && ids.includes(e.target))).toBe(true)
  })

  it('合法 ID 一个不丢（回归：修复不得改变节点数量/ID）', () => {
    const { nodes } = deserialize(RULE_JSON_SKELETON)
    expect(nodes.map(n => n.id)).toEqual(['start_n1', 'constant_n1', 'ai_node_n1', 'end_n1'])
  })
})

describe('useWorkflowStore.addNode — ID 唯一性兜底', () => {
  it('计数器失真（next 返回已占用 ID）时自动递增到未占用的 ID', () => {
    const store = useWorkflowStore()
    store.loadFromData(
      [
        mkNode('ai_node_n1', 'ai_node'),
        mkNode('end_n1', 'end')
      ] as any,
      []
    )
    // 模拟事故现场：生成器被覆盖成「只剩 end 计数」的坏实例
    const broken = new NodeIdGenerator()
    broken.resetFromNodes([{ id: 'end_n1' }])
    // @ts-ignore - state.idGenerator 允许替换（WorkflowDesigner.renderWorkflow 正是这么做的）
    store.state.idGenerator = broken

    const node = store.addNode({ classCode: 'ai_node' }, 100, 100)
    expect(node).not.toBeNull()
    expect(node!.id).toBe('ai_node_n2')
    // store 内不得出现重复 ID
    const ids = store.state.nodes.map(n => n.id)
    expect(new Set(ids).size).toBe(ids.length)
  })

  it('计数器正常时不改变原有行为（依次取 n2、n3）', () => {
    const store = useWorkflowStore()
    store.loadFromData([mkNode('ai_node_n1', 'ai_node')] as any, [])
    expect(store.addNode({ classCode: 'ai_node' }, 0, 0)!.id).toBe('ai_node_n2')
    expect(store.addNode({ classCode: 'ai_node' }, 0, 0)!.id).toBe('ai_node_n3')
  })
})

describe('useWorkflowStore.loadFromData — 重复 ID 防御', () => {
  it('坏数据里的重复 nodeId 被重编号，先出现者保留原 ID', () => {
    const store = useWorkflowStore()
    const dup = [
      mkNode('ai_node_n1', 'ai_node', 'AI 节点'),
      mkNode('ai_node_n1', 'ai_node', 'AI 节点 2'),
      mkNode('end_n1', 'end')
    ]
    store.loadFromData(dup as any, [])
    const ids = store.state.nodes.map(n => n.id)
    expect(new Set(ids).size).toBe(3)
    expect(ids[0]).toBe('ai_node_n1')
    expect(ids[1]).toBe('ai_node_n2')
    expect(store.state.nodes[1].title).toBe('AI 节点 2')
  })
})

describe('topology — 重复 nodeId 校验（保存/执行前拦截）', () => {
  it('buildDependencyGraph 留痕，validateTopology 报 DUPLICATE_NODE_ID 且 valid=false', () => {
    const config = {
      ...RULE_JSON_SKELETON,
      nodes: [...RULE_JSON_SKELETON.nodes, { nodeId: 'ai_node_n1', nodeType: 'ai_node', title: 'AI 节点 2' }]
    }
    const graph = buildDependencyGraph(config)
    expect(graph.duplicateNodeIds).toEqual(['ai_node_n1'])
    const result = validateTopology(graph)
    const dupError = result.errors.find(e => e.code === 'DUPLICATE_NODE_ID')
    expect(dupError).toBeTruthy()
    expect(dupError!.node).toBe('ai_node_n1')
    expect(result.valid).toBe(false)
  })

  it('无重复时不得误报', () => {
    const graph = buildDependencyGraph(RULE_JSON_SKELETON)
    expect(graph.duplicateNodeIds).toEqual([])
    const result = validateTopology(graph)
    expect(result.errors.some(e => e.code === 'DUPLICATE_NODE_ID')).toBe(false)
  })
})

function mkNode(id: string, classCode: string, title = id): any {
  return {
    id, classCode, nodeType: classCode, title, skillCode: '',
    x: 0, y: 0, config: {}, inputs: {}, inputTypes: {}, outputs: {},
    inputPorts: [], outputPorts: []
  }
}
