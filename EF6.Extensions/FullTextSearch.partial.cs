// ReSharper disable once CheckNamespace
namespace Ewbi
{
    partial class FullTextSearch
    {
        public static string Parse(string condition)
        {
            return Parse(condition, FullTextSearchOptions.Default);
        }
        public static string Parse(string condition, FullTextSearchOptions options)
        {
            var parser = new ConditionParser(condition, options);
            return parser.RootExpression.ToString();
        }

    }
}