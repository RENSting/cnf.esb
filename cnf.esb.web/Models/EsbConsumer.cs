using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace cnf.esb.web.Models
{
    [Table("EsbConsumer")]
    public class EsbConsumer
    {
        public int ID { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; }

        [Required]
        public string HostIP { get; set; }

        public string Token { get; set; }

        [DataType(DataType.Date)]
        public DateTime CreatedOn { get; set; }

        public int ActiveStatus { get; set; }

        public ICollection<EsbInstance> Instances { get; set; }
    }
}