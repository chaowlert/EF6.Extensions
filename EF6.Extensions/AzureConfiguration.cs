using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServer;
using System.Runtime.Remoting.Messaging;

namespace EF6.Extensions
{
    public class AzureConfiguration : DbConfiguration
    {
        public static readonly IDbExecutionStrategy SqlAzureExecutionStrategy = new SqlAzureExecutionStrategy();
        public static readonly IDbExecutionStrategy DefaultExecutionStrategy = new DefaultExecutionStrategy();

        public AzureConfiguration()
        {
            this.SetExecutionStrategy("System.Data.SqlClient", () => SuspendExecutionStrategy ? DefaultExecutionStrategy : SqlAzureExecutionStrategy);
        }

        public static bool SuspendExecutionStrategy
        {
            get
            {
                return (bool?)CallContext.LogicalGetData("SuspendExecutionStrategy") ?? false;
            }
            set
            {
                CallContext.LogicalSetData("SuspendExecutionStrategy", value);
            }
        }
    }
}
