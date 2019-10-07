using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Orleans.Analyzers;
using Xunit;

namespace Analyzers.Tests
{
    public class SerializableMemberDiagnosticAnalyzerTest : DiagnosticAnalyzerTestBase<SerializableMemberDiagnosticAnalyzer>
    {
        protected override Task<(Diagnostic[], string)> GetDiagnosticsAsync(string source, params string[] extraUsings)
            => base.GetDiagnosticsAsync(source, extraUsings.Concat(new[] { "Hagar" }).ToArray());

        [Fact]
        public async Task AlwaysInterleave_Analyzer_NoWarningsIfAttributeIsNotUsed() => await this.AssertNoDiagnostics(@"
class C
{
    Task M() => Task.CompletedTask;
}
");

        [Fact]
        public async Task AlwaysInterleave_Analyzer_NoWarningsIfAttributeIsUsedOnInterface() => await this.AssertNoDiagnostics(@"
public interface I : IGrain
{
    [AlwaysInterleave]
    Task<string> M();
}
");

        [Fact]
        public async Task AlwaysInterleave_Analyzer_WarningIfAttributeisUsedOnGrainClass()
        {
            var (diagnostics, source) = await this.GetDiagnosticsAsync(@"
public interface I : IGrain
{
    Task<int> Method();
}

public class C : I
{
    [AlwaysInterleave]
    public Task<int> Method() => Task.FromResult(0);
}
");

            var diagnostic = diagnostics.Single();

            Assert.Equal(SerializableMemberDiagnosticAnalyzer.DiagnosticId, diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Equal(SerializableMemberDiagnosticAnalyzer.MessageFormat, diagnostic.GetMessage());

            var span = diagnostic.Location.SourceSpan;
            Assert.Equal("AlwaysInterleave", source.Substring(span.Start, span.End - span.Start));
        }
    }
}