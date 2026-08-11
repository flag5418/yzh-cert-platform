using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VOL.Entity.SystemModels;

namespace VOL.Entity.CertPlatform.Sys
{
    /// <summary>
    /// 站内消息实体
    /// </summary>
    [Entity(TableCnName = "站内消息", TableName = "cert_message", DBServer = "VOLContext")]
    [Table("cert_message")]
    public class CertMessage : BaseEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public long Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("user_name")]
        public string UserName { get; set; }

        [Column("title")]
        public string Title { get; set; }

        [Column("content")]
        public string Content { get; set; }

        [Column("message_type")]
        public string MessageType { get; set; } = "system";

        [Column("is_read")]
        public int IsRead { get; set; }

        [Column("extra_data")]
        public string ExtraData { get; set; }

        [Column("create_date")]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [Column("read_date")]
        public DateTime? ReadDate { get; set; }
    }
}
