<!--
  报告内容设计页面 — 薄包装层，所有工作流逻辑委托给 WorkflowDesigner
-->
<template>
  <WorkflowDesigner
    title="报告内容设计"
    :workflow-type="'report'"
    :tree-config="{
      loadApi: '/api/ReportDefinition/section/by-context',
      loadMethod: 'get',
      textField: 'sectionName',
      codeField: 'code'
    }"
    :save-config="{
      api: '/api/ReportDefinition/section/save',
      buildPayload: (ctx) => ({
        id: ctx.leaf.id,
        code: ctx.leaf.code || ctx.leaf.Code,
        orgCode: ctx.filter.orgCode,
        reportCode: ctx.leaf.reportCode || ctx.leaf.ReportCode,
        sectionName: ctx.leaf.sectionName || ctx.leaf.SectionName,
        sectionNameEn: ctx.leaf.sectionNameEn || ctx.leaf.SectionNameEn,
        sortOrder: ctx.leaf.sortOrder || ctx.leaf.SortOrder,
        isActive: ctx.leaf.isActive !== false,
        workflowConfig: JSON.stringify(ctx.config),
        layoutJson: JSON.stringify(ctx.layout),
        remark: ctx.leaf.remark || ctx.leaf.Remark
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
