using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace cnf.esb.web.Models
{
    [Table("EsbInstance")]
    public class EsbInstance
    {
        public int ID { get; set; }
        [ForeignKey(nameof(Client))]
        public int ClientID { get; set; }
        [ForeignKey(nameof(Service))]
        public int ServiceID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ActiveStatus { get; set; }
        public DateTime CreatedOn { get; set; }
        public EsbConsumer Client { get; set; }
        public EsbService Service { get; set; }
        public InstanceMapping InstanceMapping { get; set; }
    }

    [Table("InstanceMapping")]
    public class InstanceMapping
    {
        [Key]
        public int InstanceID { get; set; }
        public string ParameterMappings { get; set; }
        public EsbInstance Instance { get; set; }
    }
}