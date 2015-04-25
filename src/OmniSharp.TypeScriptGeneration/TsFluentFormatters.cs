using System;
using System.Collections.Generic;
using System.IO;
using OmniSharp.Models;
using OmniSharp.Stdio.Protocol;
using TypeLite.TsModels;

namespace OmniSharp.TypeScriptGeneration
{
    public static class TsFluentFormatters
    {
        public static string FormatPropertyType(TsProperty type, string memberTypeName)
        {
            if (type.MemberInfo.DeclaringType.GetProperty(type.MemberInfo.Name).PropertyType == typeof(IDictionary<string, string>))
            {
                return "{ [key: string]: string }";
            }

            if (type.MemberInfo.DeclaringType.GetProperty(type.MemberInfo.Name).PropertyType == typeof(Guid))
            {
                return "string";
            }

            if (type.MemberInfo.DeclaringType.GetProperty(type.MemberInfo.Name).PropertyType == typeof(Stream))
            {
                return "any";
            }

            return memberTypeName;
        }

        public static string FormatPropertyName(TsProperty property)
        {
            // These are mapped as arguments from the client side.
            if (property.Name == nameof(RequestPacket.ArgumentsStream))
            {
                return "Arguments";
            }

            // Request type arguments are optional
            // TODO: Leverage [Required] to know what is needed and not?
            if (property.MemberInfo.DeclaringType.Name != nameof(Request) &&
                !property.MemberInfo.DeclaringType.Name.Contains(nameof(Packet)) &&
                property.MemberInfo.DeclaringType.Name.Contains(nameof(Request)))
            {
                return $"{property.Name}?";
            }

            return property.Name;
        }
    }
}
