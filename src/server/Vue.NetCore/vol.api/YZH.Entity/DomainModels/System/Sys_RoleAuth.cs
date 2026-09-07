using YZH.Entity.SystemModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YZH.Entity.DomainModels
{
    [Table("Sys_RoleAuth")]
    public class Sys_RoleAuth: BaseEntity
    {
        /// <summary>
       ///
       /// </summary>
       [Key]
       [Display(Name ="")]
       [Required(AllowEmptyStrings=false)]
       public int Auth_Id { get; set; }

       /// <summary>
       ///
       /// </summary>
       [Display(Name ="")]
       [Column(TypeName="int")]
       public int? Role_Id { get; set; }

       /// <summary>
       ///角色编码（Code关联）
       /// </summary>
       [Display(Name ="角色编码")]
       [MaxLength(50)]
       [Column(TypeName="varchar(50)")]
       public string? RoleCode { get; set; }

       /// <summary>
       ///
       /// </summary>
       [Display(Name ="")]
       [Column(TypeName="int")]
       public int? User_Id { get; set; }

       /// <summary>
       ///用户编码（Code关联）
       /// </summary>
       [Display(Name ="用户编码")]
       [MaxLength(50)]
       [Column(TypeName="varchar(50)")]
       public string? UserCode { get; set; }

       /// <summary>
       ///
       /// </summary>
       [Display(Name ="")]
       [Column(TypeName="int")]
       [Required(AllowEmptyStrings=false)]
       public int Menu_Id { get; set; }

       /// <summary>
       ///菜单编码（Code关联）
       /// </summary>
       [Display(Name ="菜单编码")]
       [MaxLength(50)]
       [Column(TypeName="varchar(50)")]
       public string? MenuCode { get; set; }

       /// <summary>
       ///用户权限
       /// </summary>
       [Display(Name ="用户权限")]
       [MaxLength(1000)]
       [Column(TypeName="nvarchar(1000)")]
       [Required(AllowEmptyStrings=false)]
       public string AuthValue { get; set; }

       /// <summary>
       ///
       /// </summary>
       [Display(Name ="")]
       [MaxLength(100)]
       [Column(TypeName="nvarchar(1000)")]
       public string Creator { get; set; }

       /// <summary>
       ///
       /// </summary>
       [Display(Name ="")]
       [Column(TypeName="datetime")]
       public DateTime? CreateDate { get; set; }

       /// <summary>
       ///
       /// </summary>
       [Display(Name ="")]
       [MaxLength(100)]
       [Column(TypeName="nvarchar(1000)")]
       public string Modifier { get; set; }

       /// <summary>
       ///
       /// </summary>
       [Display(Name ="")]
       [Column(TypeName="datetime")]
       public DateTime? ModifyDate { get; set; }

       
    }
}
