/**
 * serializer 跨边界键大小写回归测试（2026-10-16 · P2.3）
 *
 * 锁定「工作流跨边界命名统一」O2 的 **边界 A**（rule_json 外层键 PascalCase）：
 *  - `serialize()` 落库输出**只准出现 PascalCase 外层键**（后端契约 + 存量 NULL ⇒ 零迁移窗口）；
 *  - `deserialize()` **容忍旧数据 camelCase**（存量虽全 NULL，仍要能读历史快照/测试夹具）；
 *  - **边界 F**（`extractLayout` 的 `nodePositions`）**必须保持 camelCase**，不随 O2 翻。
 *
 * ⛔ 不可放宽本测试来迁就实现：外层键一旦回到 camelCase，
 *    后端 `WorkflowNodeConfig.NodeId` 等属性将全部反序列化为 null（且**不报错**，静默空图）。
 */
import { describe, expect, it } from 'vitest'
import { serialize, deserialize, extractLayout } from '@share/composables/workflow/serializer'

/** rule_json 顶层键白名单（PascalCase，顺序无关） */
const CONFIG_KEYS = ['Version', 'WorkflowType', 'Nodes', 'Edges', 'OutputConfig', 'Glossary']
/** 节点键白名单（PascalCase；Config/Inputs/... 为可选） */
const NODE_KEYS = [
  'NodeId',
  'NodeType',
  'Title',
  'Description',
  'SkillCode',
  'Config',
  'Inputs',
  'InputTypes',
  'Outputs',
  'InputPorts',
  'OutputPorts'
]
/** 边键白名单（PascalCase） */
const EDGE_KEYS = ['Source', 'Target', 'SourceHandle', 'TargetHandle']

/** 收集对象自身键，任何以小写字母开头的键都视为 camelCase 泄漏 */
function camelLeaks(obj: Record<string, any>): string[] {
  return Object.keys(obj).filter(k => /^[a-z]/.test(k))
}

const NODES = [
  { id: 'start_n1', classCode: 'start', title: '开始' },
  { id: 'ai_node_n1', classCode: 'ai_node', title: 'AI 节点', skillCode: 'ai_extract', config: { PromptTemplate: 'p', OutputType: 'string' } },
  { id: 'end_n1', classCode: 'end', title: '结束' }
]
const EDGES = [
  { source: 'start_n1', target: 'ai_node_n1' },
  { source: 'ai_node_n1', target: 'end_n1', sourceHandle: 'out' }
]

describe('serialize — 边界 A 落库键必须 PascalCase', () => {
  const config = serialize(NODES, EDGES, { version: 1, workflowType: 'validation' })

  it('顶层键 = PascalCase 白名单，且无 camelCase 泄漏', () => {
    expect(Object.keys(config).sort()).toEqual([...CONFIG_KEYS].sort())
    expect(camelLeaks(config)).toEqual([])
  })

  it('顶层键含大小写重复（Version vs version）时判为违规', () => {
    const dup = { ...config, version: 2 } as Record<string, any>
    expect(camelLeaks(dup)).toEqual(['version'])
  })

  it('节点键全部落在 PascalCase 白名单内', () => {
    for (const n of config.Nodes || []) {
      for (const k of Object.keys(n)) expect(NODE_KEYS).toContain(k)
      expect(camelLeaks(n as Record<string, any>)).toEqual([])
    }
  })

  it('节点主键写 PascalCase（NodeId/NodeType/Title），不是 id/nodeId/classCode', () => {
    const ai = (config.Nodes || []).find(n => n.NodeId === 'ai_node_n1')!
    expect(ai).toBeTruthy()
    expect(ai.NodeType).toBe('ai_node')
    expect(ai.Title).toBe('AI 节点')
    expect(ai.SkillCode).toBe('ai_extract')
    expect('nodeId' in ai || 'id' in ai || 'classCode' in ai).toBe(false)
  })

  it('节点 config 内容**原样透传**（内部键由 P5 负责，serializer 不改写）', () => {
    const ai = (config.Nodes || []).find(n => n.NodeId === 'ai_node_n1')!
    expect(ai.Config).toEqual({ PromptTemplate: 'p', OutputType: 'string' })
  })

  it('边键为 Source/Target(+Handle)，无 source/target', () => {
    for (const e of config.Edges || []) {
      for (const k of Object.keys(e)) expect(EDGE_KEYS).toContain(k)
      expect(camelLeaks(e as Record<string, any>)).toEqual([])
    }
    const second = (config.Edges || [])[1]
    expect(second.Source).toBe('ai_node_n1')
    expect(second.Target).toBe('end_n1')
    expect(second.SourceHandle).toBe('out')
  })

  it('空 description/skillCode/config 不写空键（保持 @JsonPropertyCase 简洁输出）', () => {
    const start = (config.Nodes || []).find(n => n.NodeId === 'start_n1')!
    expect('Description' in start).toBe(false)
    expect('SkillCode' in start).toBe(false)
    expect('Config' in start).toBe(false)
  })
})

describe('deserialize — 读侧容忍 camelCase 旧数据', () => {
  /** 旧版（P4 前）camelCase 落库格式 */
  const legacy = {
    version: 1,
    workflowType: 'validation',
    nodes: [
      { nodeId: 'start_n1', nodeType: 'start', title: '开始' },
      { nodeId: 'ai_node_n1', nodeType: 'ai_node', title: 'AI 节点', skillCode: 'ai_extract', config: { promptTemplate: 'p' } },
      { nodeId: 'end_n1', nodeType: 'end', title: '结束' }
    ],
    edges: [
      { source: 'start_n1', target: 'ai_node_n1' },
      { source: 'ai_node_n1', target: 'end_n1', sourceHandle: 'out' }
    ],
    outputConfig: {},
    glossary: ''
  }

  it('旧 camelCase 顶层键仍可读（Nodes/Edges 不丢）', () => {
    const r = deserialize(legacy as any)
    expect(r.nodes.length).toBe(3)
    expect(r.edges.length).toBe(2)
    expect(r.nodes.find(n => n.id === 'ai_node_n1')!.title).toBe('AI 节点')
  })

  it('旧 camelCase 边键仍可读（source/target/sourceHandle）', () => {
    const r = deserialize(legacy as any)
    const e = r.edges.find(x => x.source === 'ai_node_n1')!
    expect(e.target).toBe('end_n1')
    expect(e.sourceHandle).toBe('out')
  })

  it('新 PascalCase 落库格式可读，且与旧格式解析结果一致', () => {
    const fromNew = deserialize(serialize(NODES, EDGES, { version: 1, workflowType: 'validation' }))
    const fromOld = deserialize(legacy as any)
    expect(fromNew.nodes.map(n => n.id)).toEqual(fromOld.nodes.map(n => n.id))
    expect(fromNew.edges.map(e => `${e.source}->${e.target}`)).toEqual(fromOld.edges.map(e => `${e.source}->${e.target}`))
  })
})

describe('extractLayout — 边界 F（LayoutJson）不翻 PascalCase', () => {
  it('输出仅有 camelCase 的 nodePositions', () => {
    const layout = extractLayout([{ id: 'start_n1', x: 10, y: 20 }])
    expect(Object.keys(layout)).toEqual(['nodePositions'])
    expect(layout.nodePositions!.start_n1).toEqual({ x: 10, y: 20 })
    expect('NodePositions' in layout).toBe(false)
  })
})
