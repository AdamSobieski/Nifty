using Nifty.Activities;
using Nifty.Algorithms;
using Nifty.Analytics;
using Nifty.Collections;
using Nifty.Common;
using Nifty.Configuration;
using Nifty.Dialogue;
using Nifty.Knowledge;
using Nifty.Knowledge.Building;
using Nifty.Knowledge.Querying;
using Nifty.Knowledge.Reasoning;
using Nifty.Knowledge.Schema;
using Nifty.Knowledge.Updating;
using Nifty.Logging;
using Nifty.Messaging;
using Nifty.Messaging.Events;
using Nifty.Modelling.Users;
using Nifty.Sessions;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace Nifty.Activities
{
    public interface IActivityGeneratorStore : IHasReadOnlyMetadata, ISessionInitializable, ISessionDisposable { }
    public interface IActivityGenerator : IHasReadOnlyMetadata
    {
        // public IAskQuery Preconditions { get; }
        // public IUpdate   Effects { get; }

        public Task<IActivity> Generate(ISession session, CancellationToken cancellationToken);
    }
    public interface IActivity : IHasReadOnlyMetadata, ISessionInitializable, ISessionDisposable, IDisposable
    {
        //public IActivityGenerator Generator { get; }

        //public IActivity Parent { get; }
        //public IReadOnlyList<IActivity> Children { get; }

        // public IAskQuery Preconditions { get; }
        // public IUpdate   Effects { get; }

        public Task<IActivityExecutionResult> Execute(ISession session, IActivityExecutionContext context, CancellationToken cancellationToken);
    }
    public interface IActivityExecutionContext : IInitializable, IDisposable
    {
        public bool GetArgument<T>(string name, [NotNullWhen(true)] out T? value);
        public void SetArgument<T>(string name, T value);

        public bool GetVariable<T>(string name, [NotNullWhen(true)] out T? value);
        public void SetVariable<T>(string name, T value);
    }
    public interface IActivityExecutionResult { }

    public interface IActivityScheduler : ISessionInitializable, ISessionDisposable
    {
        public Task<IActivityExecutionResult> Schedule(ISession session, IActivity activity, IActivityExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.Run(() => activity.Execute(session, context, cancellationToken));
        }
    }
}

namespace Nifty.Algorithms
{
    public interface IAlgorithm : IHasReadOnlyMetadata, ISessionInitializable, ISessionOptimizable, IMessageSource, IMessageHandler, IEventSource, IEventHandler, ISessionDisposable
    {
        public IAsyncEnumerator<IActivityGenerator> GetAsyncEnumerator(ISession session, CancellationToken cancellationToken);
    }
    public interface IPaginatedAlgorithm : IAlgorithm
    {
        public IAsyncEnumerator<IReadOnlyList<IActivityGenerator>> GetAsyncEnumerator(ISession session, int count, CancellationToken cancellationToken);
    }
}

namespace Nifty.Analytics
{
    public interface IAnalytics : ISessionInitializable, IEventHandler, ISessionDisposable { }

    public interface IProgressMonitor
    {
        public void Start(string? message = null);
        public void StartSection(string? message = null);
        public void FinishSection(string? message = null);
        public void Finish(string? message = null);

        public void Tick();

        public ulong Ticks { get; }
        public TimeSpan Time { get; }
        public ulong SectionTicks { get; }
        public TimeSpan SectionTime { get; }

        public string Status { get; }
    }
}

namespace Nifty.Automata
{
    // see also: http://learnlib.github.io/automatalib/maven-site/latest/apidocs/net/automatalib/automata/Automaton.html

    // to do: consider approaches, e.g., fluent, to defining and building automata
}

namespace Nifty.Collections
{
    public interface IOrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IList<KeyValuePair<TKey, TValue>> { }
}

namespace Nifty.Collections.Graphs
{

}

namespace Nifty.Common
{
    public struct Literal
    {
        public Literal(string value, string? language, string? datatype)
        {
            this.value = value;
            this.language = language;
            this.datatype = datatype;
        }

        // the struct's data could be one string using RDF literal notation or other delimiters
        private readonly string value;
        private readonly string? language;
        private readonly string? datatype;

        public string Value { get { return value; } }
        public string? Language { get { return language; } }
        public string? Datatype { get { return datatype; } } // or is this IUri Datatype ?
    }

    public interface IInitializable
    {
        public IDisposable Initialize();
    }

    public interface IOptimizable
    {
        public IDisposable Optimize(IEnumerable<string> hints);
    }

    public interface INotifyChanged
    {
        public event EventHandler? Changed;
    }

    public interface IResumable<T>
    {
        public T Suspend();
        public void Resume(T state);
    }

    public interface ITransaction : IDisposable
    {
        public void Commit();
        public void Rollback();
    }

    public static class Disposable
    {
        class CombinedDisposable : IDisposable
        {
            public CombinedDisposable(IDisposable[] array)
            {
                m_disposed = false;
                m_array = array;
            }
            bool m_disposed;
            readonly IDisposable[] m_array;

            public void Dispose()
            {
                if (!m_disposed)
                {
                    m_disposed = true;

                    List<Exception> errors = new List<Exception>();

                    for (int index = 0; index < m_array.Length; index++)
                    {
                        try
                        {
                            m_array[index].Dispose();
                        }
                        catch (Exception ex)
                        {
                            errors.Add(ex);
                        }
                    }

                    if (errors.Count > 0)
                    {
                        throw new AggregateException(errors);
                    }
                }
            }
        }
        static readonly CombinedDisposable s_empty = new CombinedDisposable(Array.Empty<IDisposable>());

        public static IDisposable Empty
        {
            get
            {
                return s_empty;
            }
        }
        public static IDisposable All(params IDisposable[] scopes)
        {
            return new CombinedDisposable(scopes);
        }
    }
}

namespace Nifty.Configuration
{
    public interface ISetting<out T>
    {
        public IUri Term { get; }
        public T DefaultValue { get; }
    }

    public interface IConfiguration : ISessionInitializable, ISessionOptimizable, ISessionDisposable, INotifyChanged
    {
        public bool About(IUri setting, [NotNullWhen(true)] out IReadOnlyFormulaCollection? description);
        public bool About(IUri setting, [NotNullWhen(true)] out IReadOnlyFormulaCollection? description, string language);
        public bool TryGetSetting(IUri setting, [NotNullWhen(true)] out IConvertible? value);
        public bool TryGetSetting(IUri setting, [NotNullWhen(true)] out IConvertible? value, string language);
    }
}

namespace Nifty.Dialogue
{
    // see also: https://github.com/microsoft/botframework-sdk

    public interface IDialogueSystem : ISessionInitializable, ISessionOptimizable, IEventHandler, IEventSource, ISessionDisposable
    {
        public IDisposable SetCurrentActivity(IActivity activity);
    }
}

namespace Nifty.Extensibility
{
    // the Nifty architecture will support developing add-ons, plugins, and extensions

    // see also: https://github.com/weikio/PluginFramework
    // see also: https://github.com/merken/Prise

    // see also: https://stackoverflow.com/questions/835182/choosing-between-mef-and-maf-system-addin
    // see also: System.Composition (https://www.nuget.org/packages/System.Composition/)
}

namespace Nifty.Knowledge
{
    public interface IReadOnlyFormulaCollection : IHasReadOnlyMetadata, IHasReadOnlySchema, ISubstitute<IReadOnlyFormulaCollection> //, IEventSource, INotifyChanged
    {
        public bool IsReadOnly { get; }
        public bool IsGround { get; }
        public bool IsInferred { get; }
        public bool IsValid { get; }
        public bool IsGraph { get; }
        public bool IsEnumerable { get; }
        //public bool IsEmpty { get; }

        public bool Contains(IFormula formula);

        public IEnumerable<IDerivation> Derivations(IFormula formula);

        public IEnumerable<IFormula> Find(IFormula formula);
        public IDisposable Find(IFormula formula, IObserver<IFormula> observer);

        public int Count();
        public int Count(IFormula formula);
        public int Count(ISelectQuery query);
        public int Count(IConstructQuery query);

        public IUpdate DifferenceFrom(IReadOnlyFormulaCollection other);

        public bool Query(IAskQuery query);
        public IEnumerable<IReadOnlyDictionary<IVariable, ITerm>> Query(ISelectQuery query);
        public IEnumerable<IReadOnlyFormulaCollection> Query(IConstructQuery query);
        public IReadOnlyFormulaCollection Query(IDescribeQuery query);

        // public IDisposable Query(IAskQuery query, IObserver<bool> observer);
        public IDisposable Query(ISelectQuery query, IObserver<IReadOnlyDictionary<IVariable, ITerm>> observer);
        public IDisposable Query(IConstructQuery query, IObserver<IReadOnlyFormulaCollection> observer);
        //public IDisposable Query(IDescribeQuery query, IObserver<IReadOnlyFormulaCollection> observer);

        public IReadOnlyFormulaCollection Clone();
        public IReadOnlyFormulaCollection Clone(IReadOnlyFormulaCollection removals, IReadOnlyFormulaCollection additions);
    }
    public interface IFormulaCollection : IReadOnlyFormulaCollection
    {
        public bool Add(IFormula formula);
        public bool Add(IReadOnlyFormulaCollection formulas);

        public bool Remove(IFormula formula);
        public bool Remove(IReadOnlyFormulaCollection formulas);

        // to do: support advanced querying where observers can receive query results and subsequent notifications as query results change due to formulas being removed from and added to formula collections

        // public IDisposable Query(IAskQuery query, IObserver<Change<bool>> observer);
        // public IDisposable Query(ISelectQuery query, IObserver<Change<IReadOnlyDictionary<IVariableTerm, ITerm>>> observer);
        // public IDisposable Query(IConstructQuery query, IObserver<Change<IReadOnlyFormulaCollection>> observer);
        // public IDisposable Query(IDescribeQuery query, IObserver<Change<IReadOnlyFormulaCollection>> observer);

        // see also: "incremental tabling"

        // could also use components from Nifty.Knowledge.Updating
    }

    public interface IReadOnlyFormulaList : IReadOnlyFormulaCollection, IReadOnlyList<IFormula> { }


    public enum TermType
    {
        Any,
        Blank,
        Uri,
        Box,
        Variable,
        Formula,
    }
    public interface ITerm : ISubstitute<ITerm>
    {
        public TermType TermType { get; }

        public bool IsGround { get; }

        public object Visit(ITermVisitor visitor);

        public bool Matches(ITerm other);

        public string? ToString(XmlNamespaceManager xmlns, bool quoting);
    }

    public interface IAny : ITerm { }
    public interface IUri : ITerm
    {
        public string Uri { get; }

        //public string Namespace { get; }
        //public string LocalName { get; }
    }
    public interface IBlank : ITerm
    {
        public string Label { get; }
    }
    public interface IVariable : ITerm
    {
        public string Name { get; }
    }

    public interface IBox : ITerm
    {
        public object Value { get; }
        public BoxType BoxType { get; } // resembling System.TypeCode, or is this Type Type { get; }
    }
    public enum BoxType
    {
        Empty = 0,
        Object = 1,
        DBNull = 2,
        Boolean = 3,
        Char = 4,
        SByte = 5,
        Byte = 6,
        Int16 = 7,
        UInt16 = 8,
        Int32 = 9,
        UInt32 = 10,
        Int64 = 11,
        UInt64 = 12,
        Single = 13,
        Double = 14,
        Decimal = 0xF,
        DateTime = 0x10,
        String = 18,
        Literal = 19, // Nifty.Commons.Literal
        FormulaCollection = 20
    }

    public interface IFormula : ITerm
    {
        public ITerm Predicate { get; }

        public int Count { get; }
        public ITerm this[int index] { get; }
    }

    public interface ILambdaFormula : IFormula { }

    public interface IHasVariables
    {
        public IReadOnlyList<IVariable> GetVariables();
    }
    public interface ISubstitute<T> : IHasVariables
    {
        public bool CanSubstitute(IReadOnlyDictionary<IVariable, ITerm> map);
        public bool Substitute(IReadOnlyDictionary<IVariable, ITerm> map, [NotNullWhen(true)] out T? result);
    }

    public interface IHasReadOnlyIdentifier
    {
        public ITerm Id { get; }
    }

    public interface IHasReadOnlyMetadata : IHasReadOnlyIdentifier
    {
        public IReadOnlyFormulaCollection About { get; }
    }
    public interface IHasMetadata : IHasReadOnlyMetadata
    {
        public new IFormulaCollection About { get; }
    }

    public interface ITermVisitor
    {
        public object Visit(IAny term);
        public object Visit(IUri term);
        public object Visit(IBlank term);
        public object Visit(IVariable term);
        public object Visit(IBox term);
        public object Visit(IFormula formula);
    }

    public interface IKnowledgebase : IFormulaCollection, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable { }
}

namespace Nifty.Knowledge.Building
{
    // for efficiency, implementations could implement both formula collection interfaces and formula collection builder interfaces, resembling something like:

    //internal class FormulaCollectionImplementation : IFormulaCollection, IFormulaCollectionBuilder, IEnumerable<IFormula>
    //{
    //    public FormulaCollectionImplementation()
    //    {
    //        m_isBuilt = false;
    //        m_isReadOnly = false;
    //        m_isValid = null;
    //        m_isGround = null;

    //        m_id = Factory.Box(this); // a rationale for the *Builder interfaces is that, with *Builder, developers can easily have the formula collection's id be Factory.Box(this) instead of Factory.Blank()
    //                                  // Factory.Box(this) resembles Expression.Constant(this)
    //        m_formulas = new(0);

    //        m_metadata = null;
    //        m_schema = null;
    //    }

    //    ITerm m_id;
    //    bool m_isBuilt;
    //    bool m_isReadOnly;
    //    bool? m_isValid;
    //    bool? m_isGround;

    //    HashSet<IFormula> m_formulas;
    //    IReadOnlyFormulaCollection? m_metadata;
    //    IReadOnlySchema? m_schema;

    //    public ITerm Id
    //    {
    //        get
    //        {
    //            return m_id;
    //        }
    //    }

    //    bool IFormulaCollectionBuilder.IsBuilt => m_isBuilt;

    //    public bool IsReadOnly => m_isReadOnly;
    //    public bool IsGround
    //    {
    //        get
    //        {
    //            bool? r;

    //            if (m_isReadOnly)
    //            {
    //                if (m_isGround == null)
    //                {
    //                    m_isGround = m_formulas.All(f => f.IsGround);
    //                }
    //                r = m_isGround ?? throw new Exception();
    //            }
    //            else
    //            {
    //                r = m_formulas.All(f => f.IsGround);
    //            }

    //            return (bool)r;
    //        }
    //    }
    //    public bool IsInferred => false;
    //    public bool IsValid
    //    {
    //        get
    //        {
    //            bool? r;

    //            if (m_isReadOnly)
    //            {
    //                if (m_isValid == null)
    //                {
    //                    m_isValid = m_schema?.Validate(this).Result;
    //                }
    //                r = m_isValid ?? throw new Exception();
    //            }
    //            else
    //            {
    //                r = m_schema?.Validate(this).Result ?? throw new Exception();
    //            }
    //            return (bool)r;
    //        }
    //    }
    //    public bool IsEmpty => m_formulas.Count == 0;
    //    public bool IsGraph => false;
    //    public bool IsEnumerable => true;

    //    IFormulaCollectionBuilder IFormulaCollectionBuilder.About
    //    {
    //        get
    //        {
    //            if (m_isBuilt) throw new InvalidOperationException();
    //            if (m_metadata == null) m_metadata = new FormulaCollectionImplementation();
    //            return m_metadata as IFormulaCollectionBuilder ?? throw new Exception();
    //        }
    //    }

    //    ISchemaBuilder IFormulaCollectionBuilder.Schema
    //    {
    //        get
    //        {
    //            if (m_isBuilt) throw new InvalidOperationException();
    //            if (m_schema == null) m_schema = new SchemaImplementation();
    //            return m_schema as ISchemaBuilder ?? throw new Exception();
    //        }
    //    }

    //    bool IFormulaCollectionBuilder.Add(IFormula formula)
    //    {
    //        if (m_isBuilt) throw new InvalidOperationException();
    //        return m_formulas.Add(formula);
    //    }
    //    bool IFormulaCollection.Add(IFormula formula)
    //    {
    //        if (m_isReadOnly) throw new InvalidOperationException();
    //        return m_formulas.Add(formula);
    //    }

    //    void IFormulaCollectionBuilder.SetMetadata(IReadOnlyFormulaCollection metadata)
    //    {
    //        if (m_isBuilt) throw new InvalidOperationException();
    //        if (m_metadata != null) throw new InvalidOperationException();
    //        m_metadata = metadata;
    //    }
    //    void IFormulaCollectionBuilder.SetSchema(IReadOnlySchema schema)
    //    {
    //        if (m_isBuilt) throw new InvalidOperationException();
    //        if (m_schema != null) throw new InvalidOperationException();
    //        m_schema = schema;
    //    }

    //    IReadOnlyFormulaCollection IFormulaCollectionBuilder.Build(bool isReadOnly)
    //    {
    //        if (m_isBuilt) throw new InvalidOperationException();

    //        if (m_metadata is IFormulaCollectionBuilder mb && !mb.IsBuilt)
    //        {
    //            m_metadata = mb.Build(isReadOnly);
    //        }
    //        else if (m_metadata == null)
    //        {
    //            m_metadata = Factory.EmptyFormulaCollection;
    //        }

    //        if (m_schema is ISchemaBuilder sb && !sb.IsBuilt)
    //        {
    //            m_schema = sb.Build(isReadOnly);
    //        }
    //        else if (m_schema == null)
    //        {
    //            m_schema = Factory.EmptySchema;
    //        }

    //        m_isReadOnly = isReadOnly;
    //        m_isBuilt = true;

    //        return this;
    //    }

    //    public IEnumerator<IFormula> GetEnumerator()
    //    {
    //        return m_formulas.GetEnumerator();
    //    }
    //    IEnumerator IEnumerable.GetEnumerator()
    //    {
    //        return GetEnumerator();
    //    }
    //}

    public interface IFormulaCollectionBuilder
    {
        public ITerm Id { get; }

        public bool IsBuilt { get; }

        public IFormulaCollectionBuilder About { get; }
        public ISchemaBuilder Schema { get; }

        public bool Add(IFormula formula);

        public void SetMetadata(IReadOnlyFormulaCollection metadata);
        public void SetSchema(IReadOnlySchema schema);

        public IReadOnlyFormulaCollection Build(bool isReadOnly = true); // perhaps other parameters
    }

    public interface ISchemaBuilder : IFormulaCollectionBuilder
    {
        public new IReadOnlySchema Build(bool isReadOnly = true);
    }

    internal interface IQueryBuilder : IFormulaCollectionBuilder
    {
        public new IQuery Build(bool isReadOnly = true);
    }
}

namespace Nifty.Knowledge.Querying
{
    public enum QueryType
    {
        None,
        Select,
        Construct,
        Ask,
        Describe
    }

    public interface IQuery : IReadOnlyFormulaCollection
    {
        public QueryType QueryType { get; }
    }

    public interface ISelectQuery : IQuery
    {

    }

    public interface IConstructQuery : IQuery
    {

    }

    public interface IAskQuery : IQuery
    {

    }

    public interface IDescribeQuery : IQuery
    {

    }


    public static partial class Query
    {
        // "Fluent N-ary SPARQL"
        //        
        // the expressiveness for querying formula collections with Nifty should be comparable with or exceed that of SPARQL for triple collections
        //
        // to do: https://www.w3.org/TR/sparql11-query/#subqueries
        //
        // example syntax:
        //
        // IReadOnlyFormulaCollection formulas = ...;
        //
        // IAskQuery askQuery = Factory.Query().Where(...).Ask();
        // bool result = formulas.Query(askQuery);
        //
        // ISelectQuery selectQuery = Factory.Query().Where(...).Select(...);
        // foreach(var result in formulas.Query(selectQuery))
        // {
        //     ...
        // }


        // these conclude a query into one of the four query types
        public static IAskQuery Ask(this IQuery query)
        {
            throw new NotImplementedException();
        }
        public static ISelectQuery Select(this IQuery query, params IVariable[] variables)
        {
            throw new NotImplementedException();
        }
        public static IConstructQuery Construct(this IQuery query, IReadOnlyFormulaCollection template)
        {
            throw new NotImplementedException();
        }
        public static IDescribeQuery Describe(this IQuery query, params ITerm[] terms)
        {
            throw new NotImplementedException();
        }


        // these methods build queries before they are concluded into one of four query types
        public static IQuery Where(this IQuery query, IReadOnlyFormulaCollection pattern)
        {
            // something like:
            //
            // if (query.GetComposition(out ITerm? qc) && pattern.GetComposition(out ITerm? pc))
            // {
            //     var builder = Factory.QueryBuilder(query.Schema, query.About.Schema);
            //
            //     builder.About.Add(Factory.Formula(Keys.type, builder.Id, Keys.Querying.Types.WhereQuery));
            //     builder.About.Add(Factory.Formula(Keys.Composition.hasComposition, builder.Id, Factory.Formula(Keys.Querying.where, qc, pc)));
            //
            //     var result = builder.Build();
            //     if (!result.About.IsValid) throw new Exception();
            //
            //     return result;
            // }
            // throw new Exception();

            throw new NotImplementedException();
        }
        public static IQuery GroupBy(this IQuery query, IVariable variable)
        {
            throw new NotImplementedException();
        }
        public static IQuery GroupBy(this IQuery query, IVariable variable, IFormula having)
        {
            throw new NotImplementedException();
        }
        public static IQuery OrderBy(this IQuery query, IVariable variable)
        {
            throw new NotImplementedException();
        }
        public static IQuery OrderByDescending(this IQuery query, IVariable variable)
        {
            throw new NotImplementedException();
        }
        public static IQuery ThenBy(this IQuery query, IVariable variable)
        {
            throw new NotImplementedException();
        }
        public static IQuery ThenByDescending(this IQuery query, IVariable variable)
        {
            throw new NotImplementedException();
        }
        public static IQuery Distinct(this IQuery query)
        {
            throw new NotImplementedException();
        }
        public static IQuery Reduced(this IQuery query)
        {
            throw new NotImplementedException();
        }
        public static IQuery Offset(this IQuery query, int offset)
        {
            throw new NotImplementedException();
        }
        public static IQuery Limit(this IQuery query, int limit)
        {
            throw new NotImplementedException();
        }


        public static IReadOnlyFormulaCollection Merge(this IReadOnlyFormulaCollection formulas, IReadOnlyFormulaCollection other)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection Concat(this IReadOnlyFormulaCollection formulas, IReadOnlyFormulaCollection other)
        {
            throw new NotImplementedException();
        }


        // these are operations pertaining to formula patterns utilized by the Where operator
        public static IReadOnlyFormulaCollection Union(this IReadOnlyFormulaCollection formulas, IReadOnlyFormulaCollection other)
        {
            // this one should be moved as it could be utilized outside of querying as a basic OR operator
            // something like:
            //
            //if (formulas.GetComposition(out ITerm? fc) && other.GetComposition(out ITerm? oc))
            //{
            //    var builder = Factory.FormulaCollectionBuilder(); // ? there should be builtin schema to use here
            //
            //    builder.About.Add(Factory.Formula(Keys.type, builder.Id, Keys.Composition.Types.UnionExpression));
            //    builder.About.Add(Factory.Formula(Keys.Composition.hasComposition, builder.Id, Factory.Formula(Keys.Composition.union, fc, oc)));
            //
            //    var result = builder.Build();
            //    if (!result.About.IsValid) throw new Exception();
            //
            //    return result;
            //}
            //throw new Exception();

            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection Optional(this IReadOnlyFormulaCollection formulas, IReadOnlyFormulaCollection other)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection Exists(this IReadOnlyFormulaCollection formulas, IReadOnlyFormulaCollection other)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection NotExists(this IReadOnlyFormulaCollection formulas, IReadOnlyFormulaCollection other)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection Minus(this IReadOnlyFormulaCollection formulas, IReadOnlyFormulaCollection other)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyFormulaCollection Filter(this IReadOnlyFormulaCollection formulas, IFormula expression)
        {
            // this one should be moved as it could be utilized outside of querying as a basic filtering/constraints operator
            // something like:
            //
            //if (formulas.GetComposition(out ITerm? fc))
            //{
            //    var builder = Factory.FormulaCollectionBuilder(); // ? there should be builtin schema to use here, perhaps resembling those utilized in bootstrapping Factory.Query()
            //
            //    var qe = Factory.Formula(Keys.quote, expression);
            //
            //    builder.About.Add(Factory.Formula(Keys.type, builder.Id, Keys.Composition.Types.FilterExpression));
            //    builder.About.Add(Factory.Formula(Keys.Composition.hasComposition, builder.Id, Factory.Formula(Keys.Composition.filter, fc, qe)));
            //    builder.About.Add(Factory.Formula(Keys.Constraints.hasConstraint, builder.Id, qe));
            //
            //    var result = builder.Build();
            //    if (!result.About.IsValid) throw new Exception();
            //
            //    return result;
            //}
            //throw new Exception();

            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection Bind(this IReadOnlyFormulaCollection formulas, IVariable variable, IFormula expression)
        {
            throw new NotImplementedException();
        }


        // support for inline data
        public static IReadOnlyFormulaCollection Values(this IReadOnlyFormulaCollection formulas, IEnumerable<IReadOnlyDictionary<IVariable, ITerm>> values)
        {
            throw new NotImplementedException();
        }


        // returns a set of formulas which describes another set of formulas, e.g., using reification
        public static (ITerm Identifier, IReadOnlyFormulaCollection About) About(this IReadOnlyFormulaCollection formulas)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection About(this IReadOnlyFormulaCollection formulas, ITerm identifier)
        {
            throw new NotImplementedException();
        }


        internal static IFormula ElementAt(this IReadOnlyFormulaCollection formulas, int index)
        {
            if (formulas.IsEnumerable)
            {
                if (formulas is IReadOnlyList<IFormula> list)
                {
                    return list[index];
                }
                else if (formulas is IEnumerable<IFormula> enumerable)
                {
                    return Enumerable.ElementAt(enumerable, index);
                }
            }
            throw new ArgumentException("Argument is neither indexed nor enumerable.", nameof(formulas));
        }

        internal static bool GetComposition(this IReadOnlyFormulaCollection formulas, [NotNullWhen(true)] out ITerm? composition)
        {
            if(formulas.About.IsEnumerable && formulas.About is IEnumerable<IFormula> fe)
            {
                var hasComposition = fe.Where(f => f.Predicate == Keys.Composition.hasComposition);
                using (var enumerator = hasComposition.GetEnumerator())
                {
                    if (enumerator.MoveNext())
                    {
                        composition = enumerator.Current[1];
                        return true;
                    }
                }
            }

            composition = formulas.Id;
            return true;
        }
        internal static bool GetConstraints(this IReadOnlyFormulaCollection formulas, [NotNullWhen(true)] out IEnumerable<IFormula>? constraints)
        {
            // search for the predicate 'hasConstraint' in metadata and then unquote the quoted formula
            throw new NotImplementedException();
        }
    }
}

namespace Nifty.Knowledge.Reasoning
{
    public interface IReasoner : IHasReadOnlyMetadata
    {
        public IConfiguration Configuration { get; }

        public Task<IReasoner> BindRules(IReadOnlyFormulaCollection rules);

        public Task<IInferredReadOnlyFormulaCollection> Bind(IReadOnlyFormulaCollection collection);
    }

    public interface IInferredReadOnlyFormulaCollection : IReadOnlyFormulaCollection
    {
        public IReasoner Reasoner { get; }
        public IReadOnlyFormulaCollection Base { get; }
    }

    public interface IDerivation
    {

    }

    // formula evaluation
    // the default evaluator will evaluate the builtin predicates, resembling expression trees and lambda.
    // in addition to reasoners, these are topics for extensiblity, extensible default formula schema and extensible evaluatable vocabulary
    public interface IFormulaEvaluator
    {
        public bool Evaluate(IFormula formula, [NotNullWhen(true)] out ITerm? evaluation);
    }
}

namespace Nifty.Knowledge.Schema
{
    // Schemas should be sufficiently expressive so as to validate those formalas representing queries.
    // That is, n-ary formulas are constructed as developers make use of fluent interfaces to construct queries and these formulas should be able to be validated by schema.

    public interface IReadOnlySchema : IReadOnlyFormulaCollection
    {
        public Task<bool> Validate(IReadOnlyFormulaCollection formulas);
    }
    public interface ISchema : IReadOnlySchema, IFormulaCollection { }

    public interface IHasReadOnlySchema
    {
        public IReadOnlySchema Schema { get; }
    }
    public interface IHasSchema : IHasReadOnlySchema
    {
        public new ISchema Schema { get; }
    }
}

namespace Nifty.Knowledge.Serialization
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
    public sealed class UriAttribute : Attribute
    {
        public UriAttribute(string uri)
        {
            m_uri = new Uri(uri);
        }
        private readonly Uri m_uri;
        public Uri Uri => m_uri;
    }

    public interface ISerializable
    {
        public void Serialize(IFormulaCollection formulas);
    }
}

namespace Nifty.Knowledge.Updating
{
    // see also: https://en.wikipedia.org/wiki/Delta_encoding

    public enum UpdateType
    {
        /* Empty? */
        Simple,
        QueryBased,
        Composite,
        Conditional
        /* Other? */
    }

    public interface IUpdate // : IReadOnlyFormulaCollection, IHasComposition ?
    {
        public UpdateType UpdateType { get; }

        public IReadOnlyFormulaCollection Apply(IReadOnlyFormulaCollection formulas);
        public void Update(IFormulaCollection formulas);

        public ICompositeUpdate Then(IUpdate action);
    }

    public interface ISimpleUpdate : IUpdate
    {
        public IReadOnlyFormulaCollection Removals { get; }
        public IReadOnlyFormulaCollection Additions { get; }
    }

    public interface IQueryBasedUpdate : IUpdate
    {
        // for each query result, substitute those variables as they occur in removals and additions and remove and add the resultant contents from a formula collection

        public ISelectQuery Query { get; }
        public IReadOnlyFormulaCollection Removals { get; }
        public IReadOnlyFormulaCollection Additions { get; }
    }

    public interface ICompositeUpdate : IUpdate
    {
        public IReadOnlyList<IUpdate> Children { get; }
    }

    public interface IConditionalUpdate
    {
        public IAskQuery Query { get; }

        public IUpdate If { get; }
        public IUpdate Else { get; }
    }
}

namespace Nifty.Logging
{
    // see also: https://developer.mozilla.org/en-US/docs/Web/API/console
    public interface ILog : ISessionInitializable, IMessageHandler, IEventHandler, ISessionDisposable
    {
        public void WriteLine(string format, params object?[]? args);
    }
}

namespace Nifty.MachineLearning
{

}

namespace Nifty.MachineLearning.Probabilistic
{
    // see also: https://dotnet.github.io/infer/userguide/Recommender%20System.html
}

namespace Nifty.MachineLearning.ReinforcementLearning
{
    // see also: Afsar, M. Mehdi, Trafford Crump, and Behrouz Far. "Reinforcement learning based recommender systems: A survey." arXiv preprint arXiv:2101.06286 (2021). (https://arxiv.org/abs/2101.06286)

    // see also: https://www.gymlibrary.ml/content/api/
    // see also: https://www.gymlibrary.ml/_images/AE_loop.png

    public interface IAgent<out TAction, in TObservation, in TReward> : IDisposable
    {
        public bool MoveNext(TObservation observation, TReward reward);
        public TAction Current { get; }
    }

    public interface IEnvironment<in TAction, TObservation, TReward>
    {
        public void OnCompleted();
        public void OnError(Exception error);
        public (TObservation observation, TReward reward, bool done) OnNext(TAction action);
    }
}

namespace Nifty.Messaging
{
    public interface IMessageSource
    {
        public IDisposable Subscribe(IAskQuery query, IMessageHandler listener);
    }

    public interface IMessageHandler
    {
        public Task Handle(IMessageSource source, IReadOnlyFormulaCollection message);
    }
}

namespace Nifty.Messaging.Events
{
    public interface IEventSource
    {
        public IDisposable Subscribe(IAskQuery query, IEventHandler listener);
    }
    public interface IEventHandler
    {
        public Task Handle(IEventSource source, IReadOnlyFormulaCollection @event, IReadOnlyFormulaCollection data);
    }
}

namespace Nifty.Modelling.Domains
{
    public interface IDomainModel : IHasReadOnlyMetadata, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
}

namespace Nifty.Modelling.Users
{
    public interface IUserModel : IHasReadOnlyMetadata, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
}

namespace Nifty.NaturalLanguage.Evaluation
{

}

namespace Nifty.NaturalLanguage.Generation
{

}

namespace Nifty.NaturalLanguage.Processing
{
    public interface IOnlineNaturalLanguageParser : IObserver<IOrderedDictionary<string, float>>, IObservable<IOrderedDictionary<IUpdate, float>> { }
}

namespace Nifty.Planning.Actions
{
    // see also: Grover, Sachin, Tathagata Chakraborti, and Subbarao Kambhampati. "What can automated planning do for intelligent tutoring systems?" ICAPS SPARK (2018).

    public interface IAction : IHasReadOnlyMetadata
    {
        public IAskQuery Preconditions { get; }
        public IUpdate Effects { get; }
    }

    public interface IActionGenerator : ISubstitute<IAction> { }
}

namespace Nifty.Planning.Constraints
{
    // traversing automata to process sequences, e.g., of actions
    // should OnNext() return a next, stateful interface instance or should it more resemble IObserver<> and return void, perhaps encapsulating automata traversal?

    public interface IContext<in TAlphabet>
    {
        public void OnCompleted();
        public void OnError(Exception error);
        public IContext<TAlphabet> OnNext(TAlphabet value);
    }

    public interface IRecognitionContext<in TAlphabet> : IContext<TAlphabet>
    {
        public bool Continue { get; } // continued input sequences can be recognized at future points
        public bool Recognized { get; } // the input sequence is recognized at this point

        public new IRecognitionContext<TAlphabet> OnNext(TAlphabet value);
    }
}

namespace Nifty.Sessions
{
    public interface ISessionInitializable
    {
        public IDisposable Initialize(ISession session);
    }
    public interface ISessionOptimizable
    {
        public IDisposable Optimize(ISession session, IEnumerable<string> hints);
    }
    public interface ISessionDisposable
    {
        public void Dispose(ISession session);
    }

    public interface ISession : IHasReadOnlyMetadata, IInitializable, IOptimizable, IEventSource, IEventHandler, IDisposable, IAsyncEnumerable<IActivityGenerator>
    {
        public IConfiguration Configuration { get; }
        public IKnowledgebase Knowledgebase { get; }
        public IUserModel User { get; }
        public IActivityGeneratorStore Store { get; }
        public IAlgorithm Algorithm { get; }
        public IActivityScheduler Scheduler { get; }
        public IAnalytics Analytics { get; }
        public ILog Log { get; }

        public IDialogueSystem DialogueSystem { get; }

        IDisposable IInitializable.Initialize()
        {
            var value = Disposable.All(
                Log.Initialize(this),
                Configuration.Initialize(this),
                Analytics.Initialize(this),
                Knowledgebase.Initialize(this),
                User.Initialize(this),
                Store.Initialize(this),
                Algorithm.Initialize(this),
                Scheduler.Initialize(this),
                DialogueSystem.Initialize(this)
            );

            // DialogueSystem.Subscribe(Keys.Events.All, this);

            return value;
        }

        IDisposable IOptimizable.Optimize(IEnumerable<string> hints)
        {
            return Disposable.All(
                Configuration.Optimize(this, hints),
                Knowledgebase.Optimize(this, hints),
                DialogueSystem.Optimize(this, hints),
                Algorithm.Optimize(this, hints)
            );
        }

        public Task SaveStateInBackground(CancellationToken cancellationToken);

        void IDisposable.Dispose()
        {
            GC.SuppressFinalize(this);

            // DialogueSystem.Unsubscribe(Keys.Events.All, this);

            Algorithm.Dispose(this);
            User.Dispose(this);
            Store.Dispose(this);
            Scheduler.Dispose(this);
            DialogueSystem.Dispose(this);
            Knowledgebase.Dispose(this);
            Configuration.Dispose(this);
            Analytics.Dispose(this);
            Log.Dispose(this);

            GC.ReRegisterForFinalize(this);
        }

        IAsyncEnumerator<IActivityGenerator> IAsyncEnumerable<IActivityGenerator>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return Algorithm.GetAsyncEnumerator(this, cancellationToken);
        }
    }
}

namespace Nifty
{
    public static partial class Keys
    {
        //public static class Semantics
        //{
        //    public static class Xsd
        //    {
        //        // https://docs.microsoft.com/en-us/dotnet/standard/data/xml/mapping-xml-data-types-to-clr-types

        //        public static readonly IUri @string = Factory.Uri("http://www.w3.org/2001/XMLSchema#string");

        //        public static readonly IUri @duration = Factory.Uri("http://www.w3.org/2001/XMLSchema#duration");
        //        public static readonly IUri @dateTime = Factory.Uri("http://www.w3.org/2001/XMLSchema#dateTime");
        //        public static readonly IUri @time = Factory.Uri("http://www.w3.org/2001/XMLSchema#time");
        //        public static readonly IUri @date = Factory.Uri("http://www.w3.org/2001/XMLSchema#date");
        //        //...
        //        public static readonly IUri @anyURI = Factory.Uri("http://www.w3.org/2001/XMLSchema#anyURI");
        //        public static readonly IUri @QName = Factory.Uri("http://www.w3.org/2001/XMLSchema#QName");

        //        public static readonly IUri @boolean = Factory.Uri("http://www.w3.org/2001/XMLSchema#boolean");

        //        public static readonly IUri @byte = Factory.Uri("http://www.w3.org/2001/XMLSchema#byte");
        //        public static readonly IUri @unsignedByte = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedByte");
        //        public static readonly IUri @short = Factory.Uri("http://www.w3.org/2001/XMLSchema#short");
        //        public static readonly IUri @unsignedShort = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedShort");
        //        public static readonly IUri @int = Factory.Uri("http://www.w3.org/2001/XMLSchema#int");
        //        public static readonly IUri @unsignedInt = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedInt");
        //        public static readonly IUri @long = Factory.Uri("http://www.w3.org/2001/XMLSchema#long");
        //        public static readonly IUri @unsignedLong = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedLong");

        //        public static readonly IUri @decimal = Factory.Uri("http://www.w3.org/2001/XMLSchema#decimal");

        //        public static readonly IUri @float = Factory.Uri("http://www.w3.org/2001/XMLSchema#float");
        //        public static readonly IUri @double = Factory.Uri("http://www.w3.org/2001/XMLSchema#double");
        //    }
        //    public static class Dc
        //    {
        //        public static readonly IUri title = Factory.Uri("http://purl.org/dc/terms/title");
        //        public static readonly IUri description = Factory.Uri("http://purl.org/dc/terms/description");
        //    }
        //    public static class Swo
        //    {
        //        public static readonly IUri version = Factory.Uri("http://www.ebi.ac.uk/swo/SWO_0004000");
        //    }
        //    public static class Rdf
        //    {
        //        public static readonly IUri type = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
        //        public static readonly IUri subject = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#subject");
        //        public static readonly IUri predicate = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate");
        //        public static readonly IUri @object = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#object");

        //        public static readonly IUri Statement = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement");
        //    }
        //    public static class Foaf
        //    {
        //        public static readonly IUri name = Factory.Uri("http://xmlns.com/foaf/0.1/name");
        //    }
        //    public static class Lom
        //    {

        //    }
        //    public static class Eo
        //    {
        //        public static readonly IUri raisesEventType = Factory.Uri("http://www.event-ontology.org/raisesEventType");
        //        public static readonly IUri Event = Factory.Uri("http://www.event-ontology.org/Event");
        //    }
        //}

        //public static class Settings
        //{
        //    public static readonly ISetting<bool> ShouldPerformAnalytics = Factory.Setting("http://www.settings.org/analytics/ShouldPerformAnalytics", false);
        //    public static readonly ISetting<bool> ShouldPerformConfigurationAnalytics = Factory.Setting("http://www.settings.org/analytics/ShouldPerformConfigurationAnalytics", false);
        //}

        //public static class Events
        //{
        //    //public static readonly IUriTerm All = Factory.Uri("http://www.w3.org/2002/07/owl#Thing");

        //    public static readonly IUri InitializedSession = Factory.Uri("http://www.events.org/events/InitializedSession");
        //    public static readonly IUri ObtainedGenerator = Factory.Uri("http://www.events.org/events/ObtainedGenerator");
        //    public static readonly IUri GeneratingActivity = Factory.Uri("http://www.events.org/events/GeneratingActivity");
        //    public static readonly IUri GeneratedActivity = Factory.Uri("http://www.events.org/events/GeneratedActivity");
        //    public static readonly IUri ExecutingActivity = Factory.Uri("http://www.events.org/events/ExecutingActivity");
        //    public static readonly IUri ExecutedActivity = Factory.Uri("http://www.events.org/events/ExecutedActivity");
        //    public static readonly IUri DisposingSession = Factory.Uri("http://www.events.org/events/DisposingSession");

        //    public static class Data
        //    {
        //        public static readonly IUri Algorithm = Factory.Uri("urn:eventdata:Algorithm");
        //        public static readonly IUri Generator = Factory.Uri("urn:eventdata:Generator");
        //        public static readonly IUri Activity = Factory.Uri("urn:eventdata:Activity");
        //        public static readonly IUri User = Factory.Uri("urn:eventdata:User");
        //        public static readonly IUri Result = Factory.Uri("urn:eventdata:Result");
        //    }
        //}

        public static class Composition
        {
            public static readonly IUri hasComposition = Factory.Uri("urn:builtin:hasComposition");

            public static readonly IUri exists = Factory.Uri("urn:builtin:exists");
            public static readonly IUri notExists = Factory.Uri("urn:builtin:notExists");
            public static readonly IUri filter = Factory.Uri("urn:builtin:filter");
            public static readonly IUri optional = Factory.Uri("urn:builtin:optional");
            public static readonly IUri minus = Factory.Uri("urn:builtin:minus");
            public static readonly IUri union = Factory.Uri("urn:builtin:union");

            public static class Types
            {
                public static readonly IUri ExistsExpression = Factory.Uri("urn:builtin:ExistsExpression");
                public static readonly IUri NotExistsExpression = Factory.Uri("urn:builtin:NotExistsExpression");
                public static readonly IUri FilterExpression = Factory.Uri("urn:builtin:FilterExpression");
                public static readonly IUri OptionalExpression = Factory.Uri("urn:builtin:OptionalExpression");
                public static readonly IUri MinusExpression = Factory.Uri("urn:builtin:MinusExpression");
                public static readonly IUri UnionExpression = Factory.Uri("urn:builtin:UnionExpression");
            }
        }

        public static class Constraints
        {
            public static readonly IUri hasConstraint = Factory.Uri("urn:builtin:hasConstraint");
        }

        public static class Querying
        {
            public static readonly IUri where = Factory.Uri("urn:builtin:where");
            public static readonly IUri groupBy = Factory.Uri("urn:builtin:groupBy");
            public static readonly IUri orderBy = Factory.Uri("urn:builtin:orderBy");
            public static readonly IUri distinct = Factory.Uri("urn:builtin:distinct");
            public static readonly IUri reduced = Factory.Uri("urn:builtin:reduced");
            public static readonly IUri offset = Factory.Uri("urn:builtin:offset");
            public static readonly IUri limit = Factory.Uri("urn:builtin:limit");

            public static class Types
            {
                public static readonly IUri WhereQuery = Factory.Uri("urn:builtin:WhereQuery");
                public static readonly IUri GroupByQuery = Factory.Uri("urn:builtin:GroupByQuery");
                public static readonly IUri OrderByQuery = Factory.Uri("urn:builtin:OrderByQuery");
                public static readonly IUri DistinctQuery = Factory.Uri("urn:builtin:DistinctQuery");
                public static readonly IUri ReducedQuery = Factory.Uri("urn:builtin:ReducedQuery");
                public static readonly IUri OffsetQuery = Factory.Uri("urn:builtin:OffsetQuery");
                public static readonly IUri LimitQuery = Factory.Uri("urn:builtin:LimitQuery");
            }
        }

        public static readonly IUri type = Factory.Uri("urn:builtin:type");
        public static readonly IUri quote = Factory.Uri("urn:builtin:quote");
    }

    public static partial class Factory
    {
        internal sealed class SettingImpl<T> : ISetting<T>
        {
            public SettingImpl(IUri term, T defaultValue)
            {
                m_term = term;
                m_defaultValue = defaultValue;
            }

            private readonly IUri m_term;
            private readonly T m_defaultValue;

            public IUri Term => m_term;

            public T DefaultValue => m_defaultValue;
        }
        public static ISetting<T> Setting<T>(string uri, T defaultValue)
        {
            return new SettingImpl<T>(Uri(uri), defaultValue);
        }

        public static IAny Any()
        {
            throw new NotImplementedException();
        }
        public static IAny Any(string language)
        {
            throw new NotImplementedException();
        }
        public static IUri Uri(string uri)
        {
            throw new NotImplementedException();
        }
        public static IBlank Blank()
        {
            throw new NotImplementedException();
        }
        public static IBlank Blank(string id)
        {
            throw new NotImplementedException();
        }
        public static IVariable Variable(string name)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(bool value)
        {
            // return Box(new Literal(value.ToString(), null, Keys.Semantics.Xsd.boolean.Uri));
            throw new NotImplementedException();
        }
        public static IBox Literal(sbyte value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(byte value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(short value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(ushort value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(int value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(uint value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(long value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(ulong value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(float value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(double value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(string value)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(string value, string language)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(string value, string language, IUri datatypeUri)
        {
            throw new NotImplementedException();
        }
        public static IBox Literal(string value, IUri datatypeUri)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(object value)
        {
            throw new NotImplementedException();
        }

        public static IFormula Formula(ITerm predicate, params ITerm[] arguments)
        {
            throw new NotImplementedException();
        }

        public static IFormula TriplePSO(ITerm predicate, ITerm subject, ITerm @object)
        {
            throw new NotImplementedException();
        }
        public static IFormula TripleSPO(ITerm subject, ITerm predicate, ITerm @object)
        {
            throw new NotImplementedException();
        }


        public static IReadOnlyFormulaCollection EmptyFormulaCollection
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        public static IReadOnlySchema EmptySchema
        {
            get
            {
                throw new NotImplementedException();
            }
        }


        // the downside of these factory methods is that cannot easily use the formula collection id, Box(this), in the formulas, so have to use Blank() instead
        // considering expression trees and Constant(value)...
        // however, if the factory methods are desired, can encapsulate use of the builders inside these factory methods

        //public static IReadOnlySchema ReadOnlyFormulaCollectionSchemaWithSelfSchema(IEnumerable<IFormula> formulas)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlySchema ReadOnlyFormulaCollectionSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlySchema ReadOnlyKnowledgeGraphSchemaWithSelfSchema(IEnumerable<IFormula> formulas)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlySchema ReadOnlyKnowledgeGraphSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static ISchema FormulaCollectionSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static ISchema KnowledgeGraphSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlyFormulaCollection ReadOnlyFormulaCollection(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IFormulaCollection FormulaCollection(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlyFormulaCollection ReadOnlyKnowledgeGraph(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IFormulaCollection KnowledgeGraph(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}

        //public static IReadOnlySchema ReadOnlyFormulaCollectionSchemaWithSelfSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlySchema ReadOnlyFormulaCollectionSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlySchema ReadOnlyKnowledgeGraphSchemaWithSelfSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlySchema ReadOnlyKnowledgeGraphSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static ISchema FormulaCollectionSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static ISchema KnowledgeGraphSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlyFormulaCollection ReadOnlyFormulaCollection(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IFormulaCollection FormulaCollection(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IReadOnlyFormulaCollection ReadOnlyKnowledgeGraph(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IFormulaCollection KnowledgeGraph(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}


        // considering a builder model


        public static IFormulaCollectionBuilder FormulaCollectionBuilder()
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollectionBuilder KnowledgeGraphBuilder()
        {
            throw new NotImplementedException();
        }
        public static ISchemaBuilder SchemaBuilder()
        {
            throw new NotImplementedException();
        }
        internal static IQueryBuilder QueryBuilder()
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollectionBuilder FormulaCollectionBuilder(IReadOnlySchema schema, IReadOnlySchema metadataSchema)
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollectionBuilder KnowledgeGraphBuilder(IReadOnlySchema schema, IReadOnlySchema metadataSchema)
        {
            throw new NotImplementedException();
        }
        public static ISchemaBuilder SchemaBuilder(IReadOnlySchema schema, IReadOnlySchema metadataSchema)
        {
            throw new NotImplementedException();
        }
        internal static IQueryBuilder QueryBuilder(IReadOnlySchema schema, IReadOnlySchema metadataSchema)
        {
            throw new NotImplementedException();
        }


        public static IQuery Query()
        {
            throw new NotImplementedException();
        }


        internal static IQuery Query(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        internal static IAskQuery AskQuery(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        internal static IConstructQuery ConstructQuery(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema /* , ... */)
        {
            throw new NotImplementedException();
        }
        internal static ISelectQuery SelectQuery(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema /* , ... */)
        {
            throw new NotImplementedException();
        }
        internal static IDescribeQuery DescribeQuery(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema /* , ... */)
        {
            throw new NotImplementedException();
        }
    }

    // there might be other, possibly better, ways, e.g., allowing developers to provide formula collections which describe the terms to be combined into formulas
    // in this case, these would be generators which bind to the most specific predicates depending on the types of the terms, e.g., integers or complex numbers.
    public static partial class Formula
    {
        // these could be extension methods
        //public static bool IsPredicate(this ITerm term, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static int HasArity(this ITerm term, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IEnumerable<ITerm> ClassesOfArgument(this ITerm term, int index, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}

        public static IFormula Add(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula And(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula AndAlso(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula Divide(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula Equals(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula ExclusiveOr(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula GreaterThan(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula GreaterThanOrEqual(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula LessThan(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula LessThanOrEqual(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula Multiply(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula Negate(ITerm x)
        {
            throw new NotImplementedException();
        }
        public static IFormula Not(ITerm x)
        {
            throw new NotImplementedException();
        }
        public static IFormula NotEquals(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula Or(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula OrElse(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }
        public static IFormula Subtract(ITerm x, ITerm y)
        {
            throw new NotImplementedException();
        }

        // ...

        // would using lambdas be benefitted by extending IFormula, e.g., ILambdaFormula : IFormula ?
        public static ILambdaFormula Lambda(ITerm body, params IVariable[]? parameters)
        {
            throw new NotImplementedException();
        }
    }

    // "Fluent N-ary SPARQL" moved to Nifty.Knowledge.Querying
}