/**
 * group.ts — 标准管理页不再需要独立的组树函数，
 * 所有树构建逻辑已移至 logic.ts 的 buildCategoryFamilyTree。
 * 保留此文件仅为兼容 import 路径，避免其他文件引用断裂。
 */
export { formatStdLabel, type CategoryItem } from '../iso-standard/group'
