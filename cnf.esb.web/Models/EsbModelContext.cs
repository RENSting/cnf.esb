using Microsoft.EntityFrameworkCore;

namespace cnf.esb.web.Models
{
    public class EsbModelContext : DbContext
    {
        public EsbModelContext(DbContextOptions<EsbModelContext> options)
            : base(options)
        {
            ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <value></value>
        public DbSet<EsbConsumer> Consumers { get; set; }

        public DbSet<EsbService> Services { get; set; }

        public DbSet<EsbInstance> Instances { get; set; }

        public DbSet<InstanceMapping> InstanceMappings { get; set; }
        
        public DbSet<EsbLog> Logs{get;set;}
    }
}