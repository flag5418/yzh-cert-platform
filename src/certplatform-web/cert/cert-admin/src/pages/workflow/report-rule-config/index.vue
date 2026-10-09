<!--
  ★ 报告内容设计页面 — 薄包装层，所有工作流逻辑委托给 WorkflowDesigner

  ⚠️ ★ 2026-09-29 去主表化（D34）：本包装层的 payload 同步改造
     - 删除 ReportCode（★报告主表已废弃，章节自带归属三元组）
     - 新增 StandardCode / PhaseCode（★从 ctx.filter 取，随左树联动注入）
     - IsActive → ★ IsValid（铁律九：唯一启用字段是 IsValid）

  ⚠️ 命名铁律（项目全局规则 §16.9 / YZH 架构铁律）
    数据库列名 = C# 属性名 = TS 字段名 = PascalCase，三处逐字一致。
    - 章节实体 ReportSection → PascalCase（SectionName / WorkflowConfig / LayoutJson / SortOrder / IsValid）
    - 查询串键名 orgCode/standardCode/phaseCode 对应后端 C# 形参名
      （ReportDefinitionController.GetSectionsByContext），与 C# 侧逐字一致，保持 camelCase。
-->
<template>
  <WorkflowDesigner
    title="报告内容设计"
    :workflow-type="'report'"
    :tree-config="{
      loadApi: '/api/Admin/Workflow/ReportDefinition/section/by-context',
      loadMethod: 'get',
      textField: 'SectionName',
      codeField: 'Code',
      filterLeaf: (item) => item.JudgeMode !== 'manual'
    }"
    :save-config="{
      api: '/api/Admin/Workflow/ReportDefinition/section/save',
      buildPayload: (ctx) => ({
        Id: ctx.leaf.Id,
        Code: ctx.leaf.Code,
        // ★ 归属三元组：优先取左树 filter（去主表化后章节自带归属）
        OrgCode: ctx.filter.OrgCode || ctx.leaf.OrgCode,
        StandardCode: ctx.filter.StandardCode || ctx.leaf.StandardCode,
        PhaseCode: ctx.filter.PhaseCode || ctx.leaf.PhaseCode,
        SectionName: ctx.leaf.SectionName,
        SectionNameEn: ctx.leaf.SectionNameEn,
        SortOrder: ctx.leaf.SortOrder,
        // ★ IsActive → IsValid（铁律九）
        IsValid: ctx.leaf.IsValid ?? 1,
        // ★ 判定方式：auto=AI 自动 / manual=人工 / semi=半自动（与 NC 规则对齐）
        JudgeMode: ctx.leaf.JudgeMode ?? 'auto',
        WorkflowConfig: JSON.stringify(ctx.config),
        LayoutJson: JSON.stringify(ctx.layout),
        Remark: ctx.leaf.Remark
      })
    }"
    :execute-config="{ enabled: true, runApi: '/api/Admin/Workflow/test/run' }"
    @save-success="onSaveSuccess"
    @execute-success="onExecuteSuccess"
  />
</template>

<script setup lang="ts">
import WorkflowDesigner from '@share/components/workflow/WorkflowDesigner.vue'
import { ElMessage } from 'element-plus'

function onSaveSuccess(result: any) {
  if (result?.success === false) ElMessage.error(result?.err || '保存失败')
}
function onExecuteSuccess(result: any) { void result }
</script>

<style scoped lang="less">
.studio-layout { display: flex; flex-direction: column; height: 100%; }
</style>
