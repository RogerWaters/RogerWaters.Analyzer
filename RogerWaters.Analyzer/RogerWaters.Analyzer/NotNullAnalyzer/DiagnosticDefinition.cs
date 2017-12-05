using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace RogerWaters.Analyzer.NotNullAnalyzer
{
    internal static class DiagnosticDefinition
    {
        private static readonly Dictionary<string, DiagnosticDescriptor> _descriptors = new Dictionary<string, DiagnosticDescriptor>();

        public static IEnumerable<DiagnosticDescriptor> Descriptors => _descriptors.Values;

        public static DiagnosticDescriptor Get(string index) => _descriptors[index];

        static DiagnosticDefinition()
        {
            var collection = ((ICollection<KeyValuePair<string,DiagnosticDescriptor>>)_descriptors);

            collection.Add(Build("RA0000", "Show Require conditions", DiagnosticSeverity.Info));
            collection.Add(Build("RA0001", "Conditions are unsatisfiable", DiagnosticSeverity.Warning, "Some conditions cannot be resolved"));
            collection.Add(Build("RA0002", "Condition is unknown", DiagnosticSeverity.Warning));
            collection.Add(Build("RA0003", "Unexpecte Exception", DiagnosticSeverity.Error));
        }
        private static KeyValuePair<string, DiagnosticDescriptor> Build(string id, string title, DiagnosticSeverity severity, string desctription = null)
        {
            return new KeyValuePair<string, DiagnosticDescriptor>
            (
                id,
                new DiagnosticDescriptor
                (
                    id,
                    title,
                    "{0}",
                    "CodeAnalysis",
                    severity,
                    true,
                    desctription
                )
            );
        }
    }
}
