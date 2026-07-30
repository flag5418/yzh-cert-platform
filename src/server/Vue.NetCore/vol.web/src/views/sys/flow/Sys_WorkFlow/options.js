// *Author：jxx
// *Contact：283591387@qq.com
// *代码由框架生成,任何更改都可能导致被代码生成器覆盖
export default function(){
    const table = {
        key: 'WorkFlow_Id',
        footer: "Foots",
        cnName: '审批流程配置',
        name: 'Sys_WorkFlow',
        newTabEdit: false,
        url: "/Sys_WorkFlow/",
        sortName: "CreateDate",
        fixedSearch:false
    };
    const tableName = table.name;
    const tableCNName = table.cnName;
    const newTabEdit = false;
    const key = table.key;
    const editFormFields = {"WorkName":"","WorkTable":"","Remark":""};
    const editFormOptions = [[{"title":"流程名称","required":true,"field":"WorkName","colSize":33.333},
                               {"title":"业务表","required":true,"field":"WorkTable","colSize":33.333},
                               {"title":"备注","field":"Remark","colSize":33.333}]];
    const searchFormFields = {"WorkName":"","TitleTemplate":"","WorkTable":"","WorkTableName":"","CreateDate":""};
    const searchFormOptions = [[{"title":"流程名称","field":"WorkName","colSize":20.0},{"title":"业务表","field":"WorkTable","colSize":20.0},{"title":"模板标题","field":"TitleTemplate","colSize":20.0},{"title":"功能菜单","field":"WorkTableName","colSize":20.0},{"title":"创建时间","field":"CreateDate","colSize":20.0,"type":"datetime"}]];
    const columns = [{field:'WorkFlow_Id',title:'WorkFlow_Id',type:'string',width:110,hidden:true,require:true,align:'left'},
                       {field:'WorkName',title:'流程名称',type:'string',link:true,width:100,require:true,align:'left'},
                       {field:'TitleTemplate',title:'模板标题',type:'string',width:150,align:'left'},
                       {field:'WorkTable',title:'业务表',type:'string',width:100,require:true,align:'left'},
                       {field:'WorkTableName',title:'功能菜单',type:'string',width:100,align:'left'},
                       {field:'Weight',title:'权重',type:'int',width:70,align:'left'},
                       {field:'Enable',title:'是否启用',type:'byte',bind:{ key:'enable',data:[]},width:90,hidden:true,align:'left'},
                       {field:'AuditingEdit',title:'审核中数据是否可以编辑',type:'int',bind:{ key:'enable',data:[]},width:80,align:'left'},
                       {field:'NodeConfig',title:'节点信息',type:'string',width:110,hidden:true,align:'left'},
                       {field:'LineConfig',title:'连接配置',type:'string',width:110,hidden:true,align:'left'},
                       {field:'Remark',title:'备注',type:'string',width:130,hidden:true,align:'left'},
                       {field:'CreateID',title:'CreateID',type:'int',width:80,hidden:true,align:'left'},
                       {field:'Creator',title:'创建人',type:'string',width:80,align:'left'},
                       {field:'CreateDate',title:'创建时间',type:'datetime',width:140,align:'left'},
                       {field:'ModifyID',title:'ModifyID',type:'int',width:80,hidden:true,align:'left'},
                       {field:'Modifier',title:'修改人',type:'string',width:80,hidden:true,align:'left'},
                       {field:'ModifyDate',title:'修改时间',type:'datetime',width:140,align:'left'},
                       {field:'DbServiceId',title:'DbServiceId',type:'string',width:120,hidden:true,align:'left'}];
    const detail =  {
                    cnName: '审批步骤',
                    table: 'Sys_WorkFlowStep',
                    columns: [{field:'WorkStepFlow_Id',title:'WorkStepFlow_Id',type:'string',width:110,hidden:true,readonly:true,require:true,align:'left'},
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
                       {field:'AttachQty',title:'附件数量',type:'int',width:120,align:'left'}],
                    sortName: 'CreateDate',
                    key: 'WorkStepFlow_Id'
                                            };
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