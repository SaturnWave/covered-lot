using System;

namespace CoveredLot.Core
{
    /// <summary>
    /// Thrown by the discard arm of every switch over an engine closed set (an enum or a variant hierarchy of
    /// PuzzleCore or PuzzleCore.Jam) when the value is a case this code does not handle. The engine grows these
    /// sets only at the end and the pin tests list every case, so a new case fails a test at the next engine
    /// sync instead of reaching a phone. The message names the value's type and never the value itself, so it
    /// formats no numbers.
    /// </summary>
    public sealed class UnknownEngineCaseException : InvalidOperationException
    {
        /// <param name="unexpected">The value that reached the discard arm.</param>
        public UnknownEngineCaseException(object unexpected)
            : base("The engine gave a case this code does not handle, of type " + unexpected.GetType().ToString() + ".")
        {
        }
    }
}
