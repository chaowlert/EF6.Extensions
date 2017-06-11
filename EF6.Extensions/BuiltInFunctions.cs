﻿using EntityFramework.Functions;

namespace EF6.Extensions
{
    public static class BuiltInFunctions
    {
        [Function(FunctionType.BuiltInFunction, "FORMAT")]
        public static string Format(decimal value, string format) => Function.CallNotSupported<string>();

        [Function(FunctionType.BuiltInFunction, "ISNUMERIC")]
        public static bool IsNumeric(string value) => Function.CallNotSupported<bool>();

        [Function(FunctionType.BuiltInFunction, "JSON_VALUE")]
        public static string JsonValue(string str, string path) => Function.CallNotSupported<string>();
    }
}