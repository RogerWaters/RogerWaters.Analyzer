using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.Z3;

namespace RogerWaters.Analyzer.Some
{
    internal sealed class Parameter
    {
        private readonly IParameterSymbol _parameter;
        private readonly Context _ctx;
        private readonly Dictionary<string,Expr> _expressions = new Dictionary<string, Expr>();

        public Parameter(IParameterSymbol parameter,Context ctx)
        {
            _parameter = parameter;
            _ctx = ctx;
        }

        public bool HasNull => Has("Null");
        public Expr Null
        {
            get
            {
                return GetOrAdd("Null", name => _ctx.MkBoolConst(name));
            }
        }

        public bool HasValue => Has("Value");
        public Expr Value
        {
            get
            {
                return GetOrAdd("Value", name => _ctx.MkConst(name, GetValueSort(_parameter.Type)));
            }
        }

        public Expr GetOrAdd(string id, Func<string,Expr> constructor)
        {
            var key = "#"+ id;
            if (!_expressions.ContainsKey(key))
            {
                _expressions[key] = constructor(Name+key);
            }
            return _expressions[key];
        }

        public IEnumerable<Expr> AllExpressions => _expressions.Values;

        public IEnumerable<KeyValuePair<string, Expr>> AllNamedExpressions => _expressions
                            .Select(kvp => new KeyValuePair<string, Expr>(Name+kvp.Key,kvp.Value));

        public bool Has(string id)
        {
            return _expressions.ContainsKey("#" + id);
        }

        public string Name => _parameter.Name;

        public bool CanBeNull => _parameter.Type.IsValueType == false;
        public bool IsArray => _parameter.Type is IArrayTypeSymbol;

        private Sort GetValueSort(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol at)
            {
                var sort = GetValueSort(at.ElementType);
                switch (at.Rank)
                {
                    case 1:
                        return _ctx.MkArraySort(_ctx.IntSort, sort);
                    default:
                        throw new ArgumentOutOfRangeException(nameof(at.Rank),at.Rank,"Higher array rank is not supported");
                }
            }

            return GetSpecialTypeSort(type);
        }

        private Sort GetSpecialTypeSort(ITypeSymbol type)
        {
            switch (type.SpecialType)
            {
                case SpecialType.None:
                case SpecialType.System_Void:
                case SpecialType.System_MulticastDelegate:
                case SpecialType.System_Delegate:
                case SpecialType.System_ValueType:
                case SpecialType.System_Enum:
                case SpecialType.System_Object:
                case SpecialType.System_Array:
                case SpecialType.System_Collections_IEnumerable:
                case SpecialType.System_Collections_Generic_IEnumerable_T:
                case SpecialType.System_Collections_Generic_IList_T:
                case SpecialType.System_Collections_Generic_ICollection_T:
                case SpecialType.System_Collections_IEnumerator:
                case SpecialType.System_Collections_Generic_IEnumerator_T:
                case SpecialType.System_Collections_Generic_IReadOnlyList_T:
                case SpecialType.System_Collections_Generic_IReadOnlyCollection_T:
                case SpecialType.System_Nullable_T:
                case SpecialType.System_DateTime:
                case SpecialType.System_Runtime_CompilerServices_IsVolatile:
                case SpecialType.System_IDisposable:
                case SpecialType.System_TypedReference:
                case SpecialType.System_ArgIterator:
                case SpecialType.System_RuntimeArgumentHandle:
                case SpecialType.System_RuntimeFieldHandle:
                case SpecialType.System_RuntimeMethodHandle:
                case SpecialType.System_RuntimeTypeHandle:
                case SpecialType.System_IAsyncResult:
                case SpecialType.System_AsyncCallback:
                    throw new NotSupportedException($"Type {type.Name} not supported");
                case SpecialType.System_Boolean:
                    return _ctx.BoolSort;
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_IntPtr:
                case SpecialType.System_UIntPtr:
                    return _ctx.IntSort;
                case SpecialType.System_Decimal:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                    return _ctx.RealSort;
                case SpecialType.System_String:
                    return _ctx.StringSort;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
