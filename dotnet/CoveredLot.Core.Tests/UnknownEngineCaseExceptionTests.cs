using System;
using PuzzleCore.Jam;
using Xunit;

namespace CoveredLot.Core.Tests
{
    /// <summary>
    /// The exception of every discard arm over an engine closed set (design section 8.1): a sealed
    /// InvalidOperationException whose message names the value's type, never the value.
    /// </summary>
    public sealed class UnknownEngineCaseExceptionTests
    {
        [Fact]
        public void The_exception_is_a_sealed_invalid_operation_exception()
        {
            Type type = typeof(UnknownEngineCaseException);

            Assert.True(type.IsSealed, type.Name + " must be sealed.");
            Assert.Equal(typeof(InvalidOperationException), type.BaseType);
        }

        [Fact]
        public void The_message_names_the_type_of_the_unhandled_value_and_formats_no_number()
        {
            var exception = new UnknownEngineCaseException((LotTextProblem)99);

            Assert.Contains("PuzzleCore.Jam.LotTextProblem", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotMatch("[0-9]", exception.Message);
        }
    }
}
