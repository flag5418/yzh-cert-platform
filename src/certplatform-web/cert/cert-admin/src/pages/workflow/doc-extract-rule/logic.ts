/**
 * 文档提取规则 —— 提示词管理页面（左列表 + 右编辑工作区）
 *
 * 菜单：MENU_00210（规则定义 → 文档语义规则之后）
 * 路由：/business/doc-extract-rule
 * 后端：/api/Admin/Workflow/DocExtractRule
 *
 * 布局：
 *   左栏 = 标准列表（YzhTable 单选，4 行固定）
 *   右栏 = Prompt 编辑器 + Markdown 预览 + 测试面板
 */

/**
 * 占位符说明（对应提示词中 {{xxx}} 变量）
 *
 * ★ 2026-10-29：改为从后端 Placeholders 注册表动态加载（PromptTemplateService.RegisterPlaceholder）。
 *   此处保留静态说明作为兜底（后端未注册时前端仍能渲染按钮）。
 */
export const PLACEHOLDER_DOCS: Record<string, { label: string; desc: string }> = {
  document_content: {
    label: '文档内容',
    desc: '企业上传的原始文档转 Markdown 后的全文',
  },
  standard_name: {
    label: '标准名称',
    desc: '当前选中的标准名称（如"GB/T 19001-2016 质量管理体系 要求"）',
  },
  clause_list: {
    label: '条款列表',
    desc: '该标准对应的全部条款编号列表',
  },
  extract_fields: {
    label: '提取字段',
    desc: '当前规则配置的字段和表格清单（由系统自动生成）',
  },
}
