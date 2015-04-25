using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using OmniSharp.Models;

namespace OmniSharp.TypeScriptGeneration
{
    public static class OmnisharpControllerExtractor
    {
        public static string GetInterface()
        {
            var methods = "        " + string.Join("\n        ", GetInterfaceMethods()) + "\n";

            return $"declare module {nameof(OmniSharp)} {{\n    interface Api {{\n{methods}    }}\n}}";
        }

        private static IEnumerable<string> GetInterfaceMethods()
        {
            foreach (var method in GetControllerMethods())
            {
                var returnType = method.ReturnType;
                if (method.ReturnsArray)
                    returnType += "[]";
                if (method.RequestType != null)
                {
                        yield return $"(action: \"{method.Action}\", request: {method.RequestType.FullName}): Promise<{returnType}>;";
                }
                else
                {
                    yield return $"(action: \"{method.Action}\"): Promise<{returnType}>";
                }
            }
        }

        class MethodResult
        {
            public string Action { get; set; }
            public Type RequestType { get; set; }
            public string ReturnType { get; set; }
            public bool ReturnsArray { get; set; }
        }

        private static IEnumerable<MethodResult> GetControllerMethods()
        {
            var controller = typeof(OmnisharpController);
            foreach (var method in controller.GetTypeInfo().DeclaredMethods.Where(z => z.IsPublic))
            {
                var attribute = method.GetCustomAttribute<HttpPostAttribute>();
                if (attribute != null)
                {
                    var parameters = method.GetParameters();
                    var param = parameters.Length == 1 ? parameters[0].ParameterType : null;

                    var returnType = method.ReturnType;
                    var returnsArray = false;
                    if (returnType.Name.StartsWith(nameof(Task), StringComparison.Ordinal))
                    {
                        returnType = returnType.GetGenericArguments().First();
                    }
                    if (returnType.Name.StartsWith(nameof(IEnumerable), StringComparison.Ordinal))
                    {
                        returnsArray = true;
                        returnType = returnType.GetGenericArguments().First();
                    }

                    string returnString = "any";
                    if (returnType != null && returnType.FullName.StartsWith(InferNamespace(typeof(Request)), StringComparison.Ordinal))
                    {
                        returnString = returnType.FullName;
                    }

                    if (returnType == typeof(Boolean))
                    {
                        returnString = nameof(Boolean).ToLowerInvariant();
                    }

                    yield return new MethodResult()
                    {
                        RequestType = param,
                        ReturnType = returnString,
                        ReturnsArray = returnsArray,
                        Action = attribute.Template
                    };
                }
            }
        }

        internal static string InferNamespace(Type type)
        {
            var pieces = type.FullName.Split('.');
            return string.Join(".", pieces.Take(pieces.Length - 1)) + ".";
        }
    }
}
