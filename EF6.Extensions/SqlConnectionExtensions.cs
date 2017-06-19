using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;

namespace EF6.Extensions
{
    public static class SqlConnectionExtensions
    {
        public static Task<int> ExecuteNonQueryAsync(this SqlConnection conn, string command)
        {
            return ExecuteNonQueryAsync(conn, command, CancellationToken.None);
        }

        public static async Task<int> ExecuteNonQueryAsync(this SqlConnection conn, string command, CancellationToken cancellationToken)
        {
            using (var comm = new SqlCommand(command, conn))
            {
                return await comm.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        public static int ExecuteNonQuery(this SqlConnection conn, string command)
        {
            using (var comm = new SqlCommand(command, conn))
            {
                return comm.ExecuteNonQuery();
            }
        }
    }
}
