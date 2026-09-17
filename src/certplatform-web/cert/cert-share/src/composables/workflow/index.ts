/**
 * 工作流模型层 barrel exports
 */
export { NodeIdGenerator, isValidNodeId, extractClassCode } from './nodeIdGenerator'
export { deserialize, serialize, extractLayout } from './serializer'
export type { WorkflowNode, WorkflowEdge, WorkflowConfig, LayoutJson, DeserializeResult } from './serializer'
export {
  SPECIAL_NODES, SPECIAL_NODE_CODES, getSpecialNode, getSpecialNodeStyle,
  type SpecialNodeMeta, type PortDef, type PanelField
} from './specialNodes'
export {
  setLogLevel, setLogEnabled, buildDependencyGraph, topologicalSort,
  extractAllPaths, detectCycles, validateTopology, findUnreachableNodes,
  analyzeTopology, formatPath, summarizePaths,
  SPECIAL_NODE_TYPES, BRANCH_ANCHORS,
  type DependencyGraph, type PathResult, type TopologyValidation, type TopologyAnalysis
} from './topology'
