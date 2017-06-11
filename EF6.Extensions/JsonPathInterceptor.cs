using System;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Text.RegularExpressions;

namespace EF6.Extensions
{
    public class JsonPathInterceptor : IDbCommandInterceptor
    {
        public void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
        }
        public void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
        }
        public void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            RewriteJsonPath(command);
        }
        public void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
        }
        public void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            RewriteJsonPath(command);
        }
        public void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
        }
        private static void RewriteJsonPath(DbCommand cmd)
        {
            for (int i = cmd.Parameters.Count - 1; i >= 0; i--)
            {
                var parameter = cmd.Parameters[i];
                if (parameter.DbType != DbType.String)
                    continue;
                var value = parameter.Value as string;
                if (value == null ||
                    (!value.StartsWith("$.") && !value.Contains("'")))
                    continue;

                cmd.CommandText = cmd.CommandText.Replace($"@{parameter.ParameterName})", $"'{value}')");
            }
        }
    }
}