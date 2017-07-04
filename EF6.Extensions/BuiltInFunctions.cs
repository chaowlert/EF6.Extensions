using EntityFramework.Functions;

namespace EF6.Extensions
{
    public static class BuiltInFunctions
    {
        [Function(FunctionType.BuiltInFunction, "FORMAT")]
        public static string Format(decimal value, string format) => Function.CallNotSupported<string>();

        [Function(FunctionType.BuiltInFunction, "CONTAINS")]
        public static bool Contains(string value, string pattern) => Function.CallNotSupported<bool>();

        [Function(FunctionType.BuiltInFunction, "ISNUMERIC")]
        public static bool IsNumeric(string value) => Function.CallNotSupported<bool>();

        [Function(FunctionType.BuiltInFunction, "JSON_VALUE")]
        public static string JsonValue(string str, string path) => Function.CallNotSupported<string>();

        public static int Compare(byte[] source, byte[] target) => Function.CallNotSupported<int>();
    }
}