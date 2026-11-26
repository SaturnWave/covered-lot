using System.Collections.Generic;
using System.Linq;

namespace CoveredLot.Shared
{
    /// <summary>
    /// The randomness rule of design section 10.4, for every CoveredLot assembly: no type reference to System.Random or
    /// UnityEngine.Random. It reads the assembly's type reference table, which lists every type the assembly uses from
    /// another assembly, as both random types are; HygieneTests shows it on the planted System.Random call
    /// (dotnet/Shared/Samples/ForbiddenSamples.cs).
    /// </summary>
    internal static class CodeHygiene
    {
        public static IReadOnlyList<string> RandomTypes { get; } = new[] { "System.Random", "UnityEngine.Random" };

        /// <summary>The random types that the assembly's type reference table names, in the order of <see cref="RandomTypes"/>.</summary>
        public static IReadOnlyList<string> RandomTypeReferences(AssemblyScanner scanner) => RandomTypes.Where(scanner.References).ToList();
    }
}
