using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CoveredLot.Core.Pictures;
using PuzzleCore;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The palette and the style names (design section 8.1): the colour ids 1 to 6 give the six swatches in order, with the
    /// letters R, B, Y, G, P and O; every other colour id, the default one (0) too, throws ArgumentOutOfRangeException from
    /// the palette itself; each swatch has a class of its own, and every style name is a USS class name that no other one
    /// shares.
    /// </summary>
    public sealed class PaletteTests
    {
        private const byte PaletteColours = 6;

        [Fact]
        public void The_colour_ids_1_to_6_give_the_six_swatches_in_order_with_the_letters_R_B_Y_G_P_O()
        {
            var swatches = new List<Swatch>();
            var letters = new List<string>();
            for (byte id = 1; id <= PaletteColours; id++)
            {
                swatches.Add(Palette.SwatchOf(new ColourId(id)));
                letters.Add(Palette.LetterOf(new ColourId(id)));
            }

            Assert.Equal(new[] { Swatch.Red, Swatch.Blue, Swatch.Yellow, Swatch.Green, Swatch.Pink, Swatch.Orange }, swatches);
            Assert.Equal(Enum.GetValues<Swatch>(), swatches);
            Assert.Equal(new[] { "R", "B", "Y", "G", "P", "O" }, letters);
        }

        [Fact]
        public void Each_colour_id_from_7_to_15_throws_for_its_swatch_and_for_its_letter()
        {
            var tried = new List<int>();
            for (byte id = PaletteColours + 1; id <= ColourId.MaxValue; id++)
            {
                var colour = new ColourId(id);
                Assert.Throws<ArgumentOutOfRangeException>(() => Palette.SwatchOf(colour));
                Assert.Throws<ArgumentOutOfRangeException>(() => Palette.LetterOf(colour));
                tried.Add(id);
            }

            Assert.Equal(Enumerable.Range(7, 9), tried);
        }

        /// <summary>
        /// default(ColourId) skips the engine's constructor and has the value 0, which names no colour. The palette refuses it
        /// itself, naming the colour, where an unguarded letter would fail inside string.Substring instead.
        /// </summary>
        [Fact]
        public void The_default_colour_id_is_refused_by_the_palette_itself_for_its_swatch_and_for_its_letter()
        {
            ColourId zero = default;

            Assert.Equal("colour", Assert.Throws<ArgumentOutOfRangeException>(() => Palette.SwatchOf(zero)).ParamName);
            Assert.Equal("colour", Assert.Throws<ArgumentOutOfRangeException>(() => Palette.LetterOf(zero)).ParamName);
        }

        [Fact]
        public void Each_swatch_has_a_class_of_its_own_and_every_style_name_is_a_USS_class_name_no_other_one_shares()
        {
            Swatch[] swatches = Enum.GetValues<Swatch>();
            IReadOnlyList<string> swatchClasses = swatches.Select(StyleNames.Of).ToList();
            IReadOnlyList<string> constants = StyleNameConstants();

            IReadOnlyList<string> names = constants.Concat(swatchClasses).ToList();

            Assert.Equal(6, swatchClasses.Count);
            Assert.NotEmpty(constants);
            Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
            Assert.All(names, name => Assert.Matches("^cl-[a-z]+(-[a-z]+)*$", name));
            Assert.Throws<ArgumentOutOfRangeException>(() => StyleNames.Of((Swatch)swatches.Length));
        }

        /// <summary>The values of StyleNames' public string constants, read through reflection.</summary>
        private static IReadOnlyList<string> StyleNameConstants()
        {
            var names = new List<string>();
            foreach (FieldInfo field in typeof(StyleNames).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral && field.GetRawConstantValue() is string name)
                {
                    names.Add(name);
                }
            }

            return names;
        }
    }
}
