using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using PuzzleCore;
using Xunit.Sdk;

namespace CoveredLot.Core.Tests.Support
{
    /// <summary>
    /// Every string the pictures hold, found by walking their public properties: each string, each item of a sequence, the
    /// value of an Option that holds one, and the properties of each of Core's own types met on the way, at any depth. An
    /// engine value (a LotSize, a PieceId, a Facing) holds no string the game shows, so the walk does not go into one. A
    /// string member that a later piece adds to a picture is walked without a change here.
    /// </summary>
    internal static class PictureStrings
    {
        private const string CoreNamespace = "CoveredLot.Core";

        /// <summary>Every string of the pictures, in the order the walk meets them.</summary>
        public static IReadOnlyList<string> Of(params object[] pictures)
        {
            var strings = new List<string>();
            foreach (object picture in pictures)
            {
                Walk(picture, strings);
            }

            return strings;
        }

        private static void Walk(object value, List<string> strings)
        {
            if (value is string text)
            {
                strings.Add(text);
                return;
            }

            if (value is IEnumerable items)
            {
                foreach (object? item in items)
                {
                    if (item is not null)
                    {
                        Walk(item, strings);
                    }
                }

                return;
            }

            Type type = value.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Option<>))
            {
                if (ValueOf(value) is { } held)
                {
                    Walk(held, strings);
                }

                return;
            }

            if (!IsCoreType(type))
            {
                return;
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetValue(value) is { } member)
                {
                    Walk(member, strings);
                }
            }
        }

        /// <summary>The value an Option holds, through its TryGet; null when it holds none.</summary>
        private static object? ValueOf(object option)
        {
            MethodInfo tryGet = option.GetType().GetMethod(nameof(Option<int>.TryGet))
                ?? throw new XunitException(option.GetType().Name + " has no TryGet.");
            object?[] arguments = { null };
            return tryGet.Invoke(option, arguments) is true ? arguments[0] : null;
        }

        private static bool IsCoreType(Type type) =>
            type.Namespace is { } name && (name == CoreNamespace || name.StartsWith(CoreNamespace + ".", StringComparison.Ordinal));
    }
}
