using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using CoveredLot.Core.Pictures;
using CoveredLot.Core.Tests.Support;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The game's style sheet and Core's class names agree (design sections 6 and 9): CoveredLot.uss has a rule for every
    /// class StyleNames names, each swatch's included, and names no cl- class that StyleNames does not, so no element the
    /// Client builds goes unstyled and the sheet holds no style that nothing uses. Comments are left out, so a class a
    /// comment names counts for nothing.
    /// </summary>
    public sealed class StyleSheetTests
    {
        private const string StyleSheet = "Assets/CoveredLot/UI/CoveredLot.uss";

        /// <summary>A class in a selector: a dot, cl- and the rest of the name.</summary>
        private static readonly Regex ClassInSelector = new(@"\.(cl-[a-z]+(?:-[a-z]+)*)", RegexOptions.CultureInvariant);

        private static readonly Regex Comment = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        [Fact]
        public void The_style_sheet_styles_every_class_StyleNames_names_and_no_other_cl_class()
        {
            IReadOnlyList<string> named = ClassesStyleNamesNames();
            ISet<string> styled = ClassesTheStyleSheetStyles();

            IReadOnlyList<string> unstyled = named.Where(name => !styled.Contains(name)).ToList();
            IReadOnlyList<string> unnamed = styled.Where(name => !named.Contains(name, StringComparer.Ordinal)).OrderBy(name => name, StringComparer.Ordinal).ToList();

            Assert.Equal(51, named.Count);
            Assert.True(unstyled.Count == 0, StyleSheet + " has no rule for " + string.Join(", ", unstyled));
            Assert.True(unnamed.Count == 0, StyleSheet + " styles classes StyleNames does not name: " + string.Join(", ", unnamed));
        }

        /// <summary>The values of StyleNames' public string constants and the class of each swatch.</summary>
        private static IReadOnlyList<string> ClassesStyleNamesNames()
        {
            var names = new List<string>();
            foreach (FieldInfo field in typeof(StyleNames).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.GetRawConstantValue() is string name)
                {
                    names.Add(name);
                }
            }

            foreach (Swatch swatch in Enum.GetValues<Swatch>())
            {
                names.Add(StyleNames.Of(swatch));
            }

            return names;
        }

        /// <summary>Every cl- class the style sheet's selectors name, outside its comments.</summary>
        private static ISet<string> ClassesTheStyleSheetStyles()
        {
            string sheet = Comment.Replace(File.ReadAllText(RepositoryLayout.FullPath(StyleSheet), Encoding.UTF8), string.Empty);
            var classes = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in ClassInSelector.Matches(sheet))
            {
                classes.Add(match.Groups[1].Value);
            }

            return classes;
        }
    }
}
