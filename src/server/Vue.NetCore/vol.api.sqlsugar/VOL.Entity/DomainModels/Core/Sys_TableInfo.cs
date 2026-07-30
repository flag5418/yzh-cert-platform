using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity;
using VOL.Entity.SystemModels;


namespace VOL.Entity.DomainModels
{
    [Table("Sys_TableInfo")]
    [EntityAttribute(DetailTable = new Type[] { typeof(Sys_TableColumn) })]
    public class Sys_TableInfo : BaseEntity
    {
        [Key]
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
        [Column(TypeName = "int")]
        public int Table_Id { get; set; }
        [Editable(true)]
        [Column(TypeName = "int")]
        public int? ParentId { get; set; }

        [Editable(true)]
        public string TableName { get; set; }
        [Editable(true)]
        public string TableTrueName { get; set; }
        [Editable(true)]
        public string ColumnCNName { get; set; }


        public string Namespace { get; set; }

        [Editable(true)]
        public string FolderName { get; set; }

        [Editable(true)]
        public string DataTableType { get; set; }

        [Editable(true)]
        public string EditorType { get; set; }
        [Editable(true)]
        [Column(TypeName = "int")]
        public int? OrderNo { get; set; }
        [Editable(true)]
        public string UploadField { get; set; }
        [Editable(true)]
        public int? UploadMaxCount { get; set; }
        [Editable(true)]
        public string RichText { get; set; }
        [Editable(true)]
        public string ExpressField { get; set; }
        [Editable(true)]
        public string SortName { get; set; }
        [Editable(true)]
        public string DetailCnName { get; set; }


        [Editable(true)]
        public string DetailName { get; set; }

        [Editable(true)]
        [Column(TypeName = "int")]
        public int? Enable { get; set; }



        [Editable(true)]
        public string CnName { get; set; }

  

        /// <summary>
        /// 显示所有查询条件
        /// </summary>
        [Editable(true)]
        public int? FixedSearch { get; set; }




        [Editable(true)]
        public string MainKeyField { get; set; }

        /// <summary>
        /// Text1
        /// </summary>
        [Editable(true)]
        public string Text1 { get; set; }

        /// <summary>
        /// Text2
        /// </summary>
        [Editable(true)]
        public string Text2 { get; set; }

        /// <summary>
        /// 快捷查询字段
        /// </summary>
        [Editable(true)]
        public string QuickQueryFields { get; set; }

        public int? AsyncApi { get; set; }

        /// <summary>
        /// 列表显示明细表
        /// </summary>
        [Editable(true)]
        public int? ShowDetail { get; set; }

        [ForeignKey("Table_Id")]
        [Navigate(NavigateType.OneToMany, nameof(Table_Id), nameof(Table_Id))]
        public List<Sys_TableColumn> TableColumns { get; set; }

    }
}
