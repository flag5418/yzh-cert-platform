// *Author：jxx
// *Contact：283591387@qq.com
// *代码由框架生成,任何更改都可能导致被代码生成器覆盖
export default function(){
    const table = {
        key: 'WorkStepFlow_Id',
        footer: "Foots",
        cnName: '审批流程节点配置',
        name: 'Sys_WorkFlowStep',
        newTabEdit: false,
        url: "/Sys_WorkFlowStep/",
        sortName: "CreateDate",
        fixedSearch:false
    };
    const tableName = table.name;
    const tableCNName = table.cnName;
    const newTabEdit = false;
    const key = table.key;
    const editFormFields = {};
    const editFormOptions = [];
    const searchFormFields = {};
    const searchFormOptions = [];
    const columns = [{field:'WorkStepFlow_Id',title:'WorkStepFlow_Id',type:'string',width:110,hidden:true,require:true,align:'left'},
                       {field:'WorkFlow_Id',title:'流程主表id',type:'string',width:110,align:'left'},
                       {field:'StepId',title:'流程节点Id',type:'string',width:120,align:'left'},
                       {field:'StepName',title:'节点名称',type:'string',width:110,align:'left'},
                       {field:'StepType',title:'节点类型(1=按用户审批,2=按角色审批)',type:'int',width:110,align:'left'},
                       {field:'StepValue',title:'审批用户id或角色id',type:'string',width:110,align:'left'},
                       {field:'Remark',title:'备注',type:'string',width:220,align:'left'},
                       {field:'OrderId',title:'审批顺序',type:'int',width:80,align:'left'},
                       {field:'CreateDate',title:'创建时间',type:'datetime',width:110,align:'left'},
                       {field:'CreateID',title:'CreateID',type:'int',width:80,hidden:true,align:'left'},
                       {field:'Creator',title:'创建人',type:'string',width:130,align:'left'},
                       {field:'Enable',title:'Enable',type:'byte',width:110,align:'left'},
                       {field:'Modifier',title:'修改人',type:'string',width:130,align:'left'},
                       {field:'ModifyDate',title:'修改时间',type:'datetime',width:110,align:'left'},
                       {field:'ModifyID',title:'ModifyID',type:'int',width:80,hidden:true,align:'left'},
                       {field:'NextStepIds',title:'下一个审批节点',type:'string',width:220,hidden:true,align:'left'},
                       {field:'ParentId',title:'父级节点',type:'string',width:120,hidden:true,align:'left'},
                       {field:'AuditRefuse',title:'审核未通过(返回上一节点,流程重新开始,流程结束)',type:'int',width:80,hidden:true,align:'left'},
                       {field:'AuditBack',title:'驳回(返回上一节点,流程重新开始,流程结束)',type:'int',width:80,hidden:true,align:'left'},
                       {field:'AuditMethod',title:'审批方式(启用会签)',type:'int',width:80,hidden:true,align:'left'},
                       {field:'SendMail',title:'审核后发送邮件通知',type:'int',width:80,hidden:true,align:'left'},
                       {field:'Filters',title:'审核条件',type:'string',width:220,hidden:true,align:'left'},
                       {field:'StepAttrType',title:'节点属性(start、node、end))',type:'string',width:110,hidden:true,align:'left'},
                       {field:'Weight',title:'权重(相同条件权重大的优先匹配)',type:'int',width:80,align:'left'},
                       {field:'StepEditForm',title:'节点编辑表彰',type:'string',width:120,align:'left'},
                       {field:'AllowUpload',title:'上传附件',type:'int',width:120,align:'left'},
                       {field:'AttachType',title:'附件类型',type:'string',width:120,align:'left'},
                       {field:'AttachQty',title:'附件数量',type:'int',width:120,align:'left'}];
    const detail ={columns:[]};
    const details = [];

    return {
        table,
        key,
        tableName,
        tableCNName,
        newTabEdit,
        editFormFields,
        editFormOptions,
        searchFormFields,
        searchFormOptions,
        columns,
        detail,
        details
    };
}