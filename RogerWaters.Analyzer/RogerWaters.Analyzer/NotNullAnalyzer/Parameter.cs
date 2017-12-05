using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace RogerWaters.Analyzer.NotNullAnalyzer
{
    public class Parameter: List<Expression<Func<object, bool>>>
    {
        private readonly IParameterSymbol _parameter;

        public Parameter(IParameterSymbol parameter)
        {
            _parameter = parameter;
        }


    }
}
