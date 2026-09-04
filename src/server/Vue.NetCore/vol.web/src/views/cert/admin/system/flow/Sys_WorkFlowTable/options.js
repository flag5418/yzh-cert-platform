// *Author：jxx
// *Contact：283591387@qq.com
// *代码由框架生成,任何更改都可能导致被代码生成器覆盖
export default function(){
    const table = {
        key: 'WorkFlowTable_Id',
        footer: "Foots",
        cnName: '审批流程',
        name: 'Sys_WorkFlowTable',
        newTabEdit: false,
        url: "/Sys_WorkFlowTable/",
        sortName: "CreateDate",
        fixedSearch:false
    };
    const tableName = table.name;
    const tableCNName = table.cnName;
    const newTabEdit = false;
    const key = table.key;
    const editFormFields = {};
    const editFormOptions = [];
    const searchFormFields = {"WorkName":"","TitleTemplate":"","WorkTableName":""};
    const searchFormOptions = [[{"title":"流程名称","field":"WorkName","colSize":25.0,"type":"like"},{"title":"标题","field":"TitleTemplate","colSize":25.0},{"title":"业务名称","field":"WorkTableName","colSize":25.0,"type":"like"}]];
    const columns = [{field:'WorkFlowTable_Id',title:'WorkFlowTable_Id',type:'string',width:110,hidden:true,require:true,align:'left'},
                       {field:'WorkFlow_Id',title:'流程id',type:'string',width:110,hidden:true,align:'left'},
                       {field:'WorkName',title:'流程名称',type:'string',width:100,align:'left'},
                       {field:'TitleTemplate',title:'标题',type:'string',width:110,align:'left'},
                       {field:'WorkTableKey',title:'表主键id',type:'string',width:180,hidden:true,align:'left'},
                       {field:'WorkTable',title:'表名',type:'string',width:90,align:'left'},
                       {field:'WorkTableName',title:'业务名称',type:'string',width:80,align:'left'},
                       {field:'CurrentStepId',title:'节点ID',type:'string',width:80,align:'left'},
                       {field:'StepName',title:'节点名称',type:'string',width:100,align:'left'},
                       {field:'CurrentOrderId',title:'不用',type:'int',width:90,hidden:true,align:'left'},
                       {field:'AuditStatus',title:'审批状态',type:'int',bind:{ key:'audit',data:[]},width:80,align:'left'},
                       {field:'Creator',title:'创建人',type:'string',width:80,align:'left'},
                       {field:'CreateDate',title:'创建时间',type:'datetime',width:120,align:'left'},
                       {field:'CreateID',title:'CreateID',type:'int',width:80,hidden:true,align:'left'},
                       {field:'Enable',title:'Enable',type:'byte',width:110,hidden:true,align:'left'},
                       {field:'Modifier',title:'修改人',type:'string',width:130,hidden:true,align:'left'},
                       {field:'ModifyDate',title:'修改时间',type:'datetime',width:110,hidden:true,align:'left'},
                       {field:'ModifyID',title:'ModifyID',type:'int',width:80,hidden:true,align:'left'},
                       {field:'DbServiceId',title:'DbServiceId',type:'string',width:120,hidden:true,align:'left'}];
    const detail =  {
                    cnName: '审批节点',
                    table: 'Sys_WorkFlowTableStep',
                    columns: [{field:'Sys_WorkFlowTableStep_Id',title:'Sys_WorkFlowTableStep_Id',type:'string',width:110,hidden:true,readonly:true,require:true,align:'left'},
                       {field:'WorkFlowTable_Id',title:'主表id',type:'string',width:110,require:true,align:'left'},
                       {field:'WorkFlow_Id',title:'流程id',type:'string',width:110,align:'left'},
                       {field:'StepId',title:'节点id',type:'string',width:120,align:'left'},
                       {field:'StepName',title:'节名称',type:'string',width:180,align:'left'},
                       {field:'StepType',title:'审批类型',type:'int',width:110,align:'left'},
                       {field:'StepValue',title:'节点类型(1=按用户审批,2=按角色审批)',type:'string',width:110,align:'left'},
                       {field:'OrderId',title:'审批顺序',type:'int',width:110,align:'left'},
                       {field:'Remark',title:'Remark',type:'string',width:220,align:'left'},
                       {field:'CreateDate',title:'创建时间',type:'datetime',width:110,align:'left'},
                       {field:'CreateID',title:'CreateID',type:'int',width:80,hidden:true,align:'left'},
                       {field:'Creator',title:'创建人',type:'string',width:130,align:'left'},
                       {field:'Enable',title:'Enable',type:'byte',width:110,align:'left'},
                       {field:'Modifier',title:'修改人',type:'string',width:130,align:'left'},
                       {field:'ModifyDate',title:'修改时间',type:'datetime',width:110,align:'left'},
                       {field:'ModifyID',title:'ModifyID',type:'int',width:80,hidden:true,align:'left'},
                       {field:'AuditId',title:'审核人id',type:'int',width:80,align:'left'},
                       {field:'Auditor',title:'审核人',type:'string',width:120,align:'left'},
                       {field:'AuditStatus',title:'审核状态',type:'int',width:80,align:'left'},
                       {field:'AuditDate',title:'审核时间',type:'datetime',width:150,align:'left'},
                       {field:'StepAttrType',title:'节点属性(start、node、end))',type:'string',width:110,align:'left'},
                       {field:'ParentId',title:'ParentId',type:'string',width:120,align:'left'},
                       {field:'NextStepId',title:'NextStepId',type:'string',width:120,align:'left'},
                       {field:'Weight',title:'Weight',type:'int',width:80,align:'left'},
                       {field:'AuditMethod',title:'AuditMethod',type:'int',width:120,align:'left'},
                       {field:'FormOptions',title:'FormOptions',type:'string',width:120,align:'left'},
                       {field:'SourceType',title:'SourceType',type:'string',width:120,align:'left'},
                       {field:'AttachFile',title:'附件',type:'string',width:120,align:'left'},
                       {field:'AttachType',title:'附件类型',type:'string',width:120,align:'left'},
                       {field:'StepEditForm',title:'编辑表单',type:'string',width:120,align:'left'},
                       {field:'AttachQty',title:'附件数量',type:'int',width:120,align:'left'}],
                    sortName: 'CreateDate',
                    key: 'Sys_WorkFlowTableStep_Id'
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