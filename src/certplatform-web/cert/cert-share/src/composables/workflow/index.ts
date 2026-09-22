/**
 * 工作流模型层 barrel exports
 *
 * 注：compiler 再导出的 setLogLevel/setLogEnabled 与 topology 重复，
 * barrel 统一从 topology 出口（单一来源，TS2300 消歧）；
 * compiler 内部自己 import 使用不受影响。
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
export { useWorkflowStore } from './useWorkflowStore'
export type { WorkflowState, WorkflowNode as WNode, WorkflowEdge as WEdge } from './useWorkflowStore'
export {
  analyzeWorkflowTopology, nodeStyle, compileToWorkflowConfig, decompileToGraphData,
  extractTopologicalPaths, topologicalOrder,
  type GraphNode, type GraphEdge, type GraphData, type WorkflowConfig as CompilerWorkflowConfig
} from './compiler'
