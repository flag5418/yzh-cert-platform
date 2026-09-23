<!--
  报告内容设计页面 — 薄包装层，所有工作流逻辑委托给 WorkflowDesigner

  ⚠️ 命名铁律（项目全局规则 §16.9 / YZH 架构铁律）
    数据库列名 = C# 属性名 = TS 字段名 = PascalCase，三处逐字一致。
    - 章节实体 ReportSection → PascalCase（SectionName / WorkflowConfig / LayoutJson / SortOrder / IsActive）
    - 组织树 organization-tree 是服务层手写 DTO（camelCase），属于已登记的例外。
    - 查询串键名 orgCode/standardCode/phaseCode 对应后端 C# 形参名
      （ReportDefinitionController.GetSectionsByContext），与 C# 侧逐字一致，保持 camelCase。
-->
<template>
  <WorkflowDesigner
    title="报告内容设计"
    :workflow-type="'report'"
    :tree-config="{
      loadApi: '/api/ReportDefinition/section/by-context',
      loadMethod: 'get',
      textField: 'SectionName',
      codeField: 'Code'
    }"
    :save-config="{
      api: '/api/ReportDefinition/section/save',
      buildPayload: (ctx) => ({
        Id: ctx.leaf.Id,
        Code: ctx.leaf.Code,
        OrgCode: ctx.leaf.OrgCode || ctx.filter.OrgCode,
        ReportCode: ctx.leaf.ReportCode,
        SectionName: ctx.leaf.SectionName,
        SectionNameEn: ctx.leaf.SectionNameEn,
        SortOrder: ctx.leaf.SortOrder,
        IsActive: ctx.leaf.IsActive ?? 1,
        WorkflowConfig: JSON.stringify(ctx.config),
        LayoutJson: JSON.stringify(ctx.layout),
        Remark: ctx.leaf.Remark
      })
    }"
    :execute-config="{ enabled: true, runApi: '/api/Workflow/test/run' }"
    @save-success="onSaveSuccess"
    @execute-success="onExecuteSuccess"
  />
</template>

<script setup lang="ts">
import WorkflowDesigner from '@share/components/workflow/WorkflowDesigner.vue'
import { ElMessage } from 'element-plus'

function onSaveSuccess(result: any) {
  if (result?.success === false) ElMessage.error(result?.message || '保存失败')
}
function onExecuteSuccess(result: any) { void result }
</script>

<style scoped lang="less">
.studio-layout { display: flex; flex-direction: column; height: 100%; }
</style>
