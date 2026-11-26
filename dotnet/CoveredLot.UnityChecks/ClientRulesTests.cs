using System.Collections.Generic;
using System.Linq;
using CoveredLot.Shared;
using CoveredLot.UnityChecks.Support;
using Xunit;

namespace CoveredLot.UnityChecks
{
    /// <summary>
    /// The Client's own rules (<see cref="ClientRules"/>, design section 10.4) on Unity's build of the Client: it names no
    /// member of the rules engine's types, makes no number text, builds no text from values and constructs no colour.
    /// CoveredLot.Core.Tests shows each rule on planted samples (ClientRulesTests there); the rules' known gaps are in the
    /// remarks of <see cref="ClientRules"/>.
    /// </summary>
    public sealed class ClientRulesTests
    {
        private const string Client = "CoveredLot.Client";

        [Fact]
        public void Unity_built_Client_keeps_its_own_rules()
        {
            using AssemblyScanner client = UnityOutputs.Scan(Client);

            IReadOnlyList<Violation> violations = ClientRules.Violations(client);

            Assert.True(violations.Count == 0, "Unity's build of " + Client + " breaks its own rules:\n" + string.Join("\n", violations.Select(violation => "  " + violation)));
        }
    }
}
