using System;
using PuzzleCore.Jam;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>The exception of every discard arm over an engine closed set names the value's type, never the value.</summary>
    public sealed class UnknownEngineCaseExceptionTests
    {
        [Fact]
        public void The_message_names_the_type_of_the_unhandled_value_and_formats_no_number()
        {
            var exception = new UnknownEngineCaseException((LotTextProblem)99);

            Assert.Contains("PuzzleCore.Jam.LotTextProblem", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotMatch("[0-9]", exception.Message);
        }
    }
}
