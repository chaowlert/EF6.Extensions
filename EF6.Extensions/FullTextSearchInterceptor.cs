using System;
using System.Data;
using System.Data.Common;
using System.Data.Entity.Infrastructure.Interception;
using System.Text.RegularExpressions;
using Ewbi;

namespace EF6.Extensions
{
    public enum FullTextParseOption
    {
        NoParse,
        Parse,
        ParseAsPrefix,
    }
    public class FullTextSearchInterceptor : IDbCommandInterceptor
    {
        private readonly string _prefix;
        private readonly FullTextParseOption _option;

        public FullTextSearchInterceptor(string prefix = "fulltext:", FullTextParseOption option = FullTextParseOption.ParseAsPrefix)
        {
            _prefix = prefix;
            _option = option;
        }

        public void NonQueryExecuting(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
        }
        public void NonQueryExecuted(DbCommand command, DbCommandInterceptionContext<int> interceptionContext)
        {
        }
        public void ReaderExecuting(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
            RewriteFullTextQuery(command);
        }
        public void ReaderExecuted(DbCommand command, DbCommandInterceptionContext<DbDataReader> interceptionContext)
        {
        }
        public void ScalarExecuting(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
            RewriteFullTextQuery(command);
        }
        public void ScalarExecuted(DbCommand command, DbCommandInterceptionContext<object> interceptionContext)
        {
        }
        public void RewriteFullTextQuery(DbCommand cmd)
        {
            for (int i = 0; i < cmd.Parameters.Count; i++)
            {
                var parameter = cmd.Parameters[i];
                if (parameter.DbType != DbType.String)
                    continue;
                var value = parameter.Value as string;
                if (value == null || !value.StartsWith(_prefix, StringComparison.Ordinal))
                    continue;

                var oldText = cmd.CommandText;
                cmd.CommandText = Regex.Replace(oldText,
                    $@"\(?CONTAINS\((\[\w+\]\.\[\w+\]),\s*@{parameter.ParameterName}(?:,\s*(@\w+|\d+)?)\)\)?\s*=\s*1",
                    match =>
                    {
                        var lang = match.Groups[2].Success ? ", LANGUAGE " + match.Groups[2].Value : "";
                        return $"CONTAINS({match.Groups[1].Value}, @{parameter.ParameterName}{lang})";
                    });
                if (oldText != cmd.CommandText)
                {
                    parameter.Size = 4000;
                    var len = _prefix.Length;
                    var exp = value.Substring(len, value.Length - len);
                    if (_option != FullTextParseOption.NoParse)
                    {
                        if (_option == FullTextParseOption.ParseAsPrefix)
                            exp = exp + '*';
                        exp = FullTextSearch.Parse(exp);
                    }
                    parameter.Value = exp;
                }
            }
        }
    }
}
