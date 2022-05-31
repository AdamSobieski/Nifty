﻿using Nifty.Activities;
using Nifty.Algorithms;
using Nifty.Analytics;
using Nifty.Channels;
using Nifty.Collections;
using Nifty.Common;
using Nifty.Configuration;
using Nifty.Dialogue;
using Nifty.Events;
using Nifty.Knowledge;
using Nifty.Knowledge.Querying;
using Nifty.Knowledge.Reasoning;
using Nifty.Knowledge.Schema;
using Nifty.Knowledge.Updating;
using Nifty.Logging;
using Nifty.Modelling.Users;
using Nifty.Sessions;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace Nifty.Activities
{
    public interface IActivityGeneratorStore : IHasReadOnlyMetadata, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
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
    public interface IAlgorithm : IHasReadOnlyMetadata, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable
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

namespace Nifty.AutomatedPlanning.Actions
{
    // see also: Grover, Sachin, Tathagata Chakraborti, and Subbarao Kambhampati. "What can automated planning do for intelligent tutoring systems?" ICAPS SPARK (2018).

    public interface IAction : IHasReadOnlyMetadata
    {
        public IAskQuery Preconditions { get; }
        public IUpdate Effects { get; }
    }

    public interface IActionGenerator : ISubstitute<IAction> { }
}

namespace Nifty.AutomatedPlanning.Constraints
{
    // here will go constraints and preferences for uses upon objects and upon sequences of objects, e.g., upon sequences of actions
    // this namespace will utilize Nifty.Collections.Automata
    // this namespace will be general-purpose
    // this namespace may be either Nifty.AutomatedPlanning.Constraints or Nifty.Constraints

    // here is a sketch of an System.IObserver<>-like interface which can process a sequence of inputs while potentially transitioning upon a recognizing automata
    public interface IObserver<in TAlphabet>
    {
        public bool Continue { get; } // continued input sequences can be valid at future points
        public bool Recognized { get; } // the input sequence is valid at this point

        public void OnCompleted();
        public void OnError(Exception error);
        public IObserver<TAlphabet> OnNext(TAlphabet value);
    }

    // to do: consider approaches, e.g., fluent, to defining and building automata
}

namespace Nifty.Channels
{
    public interface IChannel { }

    public interface IChannelCollection { }
}

namespace Nifty.Collections
{
    public interface IOrderedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IList<KeyValuePair<TKey, TValue>> { }
}

namespace Nifty.Collections.Automata
{
    // see also: http://learnlib.github.io/automatalib/maven-site/latest/apidocs/net/automatalib/automata/Automaton.html
}

namespace Nifty.Collections.Graphs
{
    // initial graph model based on Infer.NET

    public interface IReadOnlyEdge<TNode>
    {
        public TNode Source { get; }
        public TNode Target { get; }
    }

    public interface IEdge<TNode> : IReadOnlyEdge<TNode>
    {
        public new TNode Source { get; set; }
        public new TNode Target { get; set; }
    }

    public interface IHasOutEdges<TEdge>
    {
        public ICollection<TEdge> OutEdges { get; }
    }

    public interface IHasInEdges<TEdge>
    {
        public ICollection<TEdge> InEdges { get; }
    }

    public interface IHasTargets<TNode>
    {
        public ICollection<TNode> Targets { get; }
    }

    public interface IHasSources<TNode>
    {
        public ICollection<TNode> Sources { get; }
    }

    public interface IHasSourcesAndTargets<TNode> : IHasSources<TNode>, IHasTargets<TNode> { }

    public interface IHasInAndOutEdges<TEdge> : IHasInEdges<TEdge>, IHasOutEdges<TEdge> { }

    public interface IReadOnlyGraph<TNode>
    {
        public IEnumerable<TNode> Nodes { get; }

        public int EdgeCount();

        public int NeighborCount(TNode node);

        public IEnumerable<TNode> NeighborsOf(TNode node);

        public bool ContainsEdge(TNode source, TNode target);
    }

    public interface IGraph<TNode> : IReadOnlyGraph<TNode>
    {
        public TNode AddNode();
        public bool RemoveNodeAndEdges(TNode node);
        public void Clear();
        public void AddEdge(TNode source, TNode target);
        public bool RemoveEdge(TNode source, TNode target);
        public void ClearEdges();
        public void ClearEdgesOf(TNode node);
    }

    public interface IReadOnlyDirectedGraph<TNode> : IReadOnlyGraph<TNode>
    {
        public int TargetCount(TNode source);
        public int SourceCount(TNode target);
        public IEnumerable<TNode> TargetsOf(TNode source);
        public IEnumerable<TNode> SourcesOf(TNode target);
    }

    public interface IDirectedGraph<TNode> : IReadOnlyDirectedGraph<TNode>, IGraph<TNode>
    {
        public void ClearEdgesOutOf(TNode source);
        public void ClearEdgesInto(TNode target);
    }

    public interface IReadOnlyGraph<TNode, TEdge> : IReadOnlyGraph<TNode>
    {
        public IEnumerable<TEdge> Edges { get; }

        public TEdge GetEdge(TNode source, TNode target);

        public bool TryGetEdge(TNode source, TNode target, out TEdge edge);

        public IEnumerable<TEdge> EdgesOf(TNode node);
    }

    public interface IReadOnlyMultigraph<TNode, TEdge> : IReadOnlyGraph<TNode, TEdge>
    {
        public int EdgeCount(TNode source, TNode target);

        public IEnumerable<TEdge> EdgesLinking(TNode source, TNode target);

        public bool AnyEdge(TNode source, TNode target, out TEdge edge);
    }

    public interface IGraph<TNode, TEdge> : IReadOnlyGraph<TNode, TEdge>, IGraph<TNode>
    {
        public new TEdge AddEdge(TNode source, TNode target);
        public bool RemoveEdge(TEdge edge);
    }

    public interface IReadOnlyDirectedGraph<TNode, TEdge> : IReadOnlyGraph<TNode, TEdge>, IReadOnlyDirectedGraph<TNode>
    {
        public TNode SourceOf(TEdge edge);
        public TNode TargetOf(TEdge edge);
        public IEnumerable<TEdge> EdgesOutOf(TNode source);
        public IEnumerable<TEdge> EdgesInto(TNode target);
    }

    public interface IDirectedGraph<TNode, TEdge> : IReadOnlyDirectedGraph<TNode, TEdge>, IGraph<TNode, TEdge>, IDirectedGraph<TNode> { }

    public interface IReadOnlyLabeledGraph<TNode, TLabel> : IReadOnlyGraph<TNode>
    {
        public new ILabeledCollection<TNode, TLabel> Nodes { get; }
    }

    public interface ILabeledEdgeGraph<TNode, TLabel> : IReadOnlyGraph<TNode>
    {
        public void AddEdge(TNode fromNode, TNode toNode, TLabel label);
        public void RemoveEdge(TNode fromNode, TNode toNode, TLabel label);
        public void ClearEdges(TLabel label);
    }

    public interface ILabeledCollection<TItem, TLabel> : ICollection<TItem>
    {
        ICollection<TLabel> Labels { get; }
        ICollection<TItem> WithLabel(TLabel label);
    }

    public class IndexedProperty<TKey, TValue>
    {
        public readonly Converter<TKey, TValue> Get;

        public readonly Action<TKey, TValue> Set;

        public readonly Action Clear;

        public TValue this[TKey key]
        {
            get { return Get(key); }
            set { Set(key, value); }
        }

        public IndexedProperty(Converter<TKey, TValue> getter, Action<TKey, TValue> setter, Action clearer)
        {
            Get = getter;
            Set = setter;
            Clear = clearer;
        }

        public IndexedProperty(IDictionary<TKey, TValue> dictionary, TValue defaultValue)
        {
            Get = delegate (TKey key)
            {
                TValue value;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                bool containsKey = dictionary.TryGetValue(key, out value);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8603 // Possible null reference return.
                if (!containsKey) return defaultValue;
                else return value;
#pragma warning restore CS8603 // Possible null reference return.
            };
            Set = delegate (TKey key, TValue value) { dictionary[key] = value; };
            Clear = dictionary.Clear;
        }
    }

    public interface ICanCreateNodeData<TNode>
    {
        public IndexedProperty<TNode, T> CreateNodeData<T>(T defaultValue);
    }

    public interface ICanCreateEdgeData<TEdge>
    {
        public IndexedProperty<TEdge, T> CreateEdgeData<T>(T defaultValue);
    }

    public sealed class EdgeNotFoundException : Exception
    {
        public EdgeNotFoundException()
        {
        }

        public EdgeNotFoundException(object source, object target)
            : base("No edge from " + source + " to " + target)
        {
        }
    }

    public sealed class AmbiguousEdgeException : Exception
    {
        public AmbiguousEdgeException()
        {
        }

        public AmbiguousEdgeException(object source, object target)
            : base("Ambiguous edge from " + source + " to " + target)
        {
        }
    }
}

namespace Nifty.Common
{
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

namespace Nifty.Concurrency
{

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
        public bool About(IUri setting, [NotNullWhen(true)] out IReadOnlyFormulaCollection? about);
        public bool About(IUri setting, [NotNullWhen(true)] out IReadOnlyFormulaCollection? about, string language);
        public bool TryGetSetting(IUri setting, [NotNullWhen(true)] out IConvertible? value);
        public bool TryGetSetting(IUri setting, [NotNullWhen(true)] out IConvertible? value, string language);
    }
}

namespace Nifty.Dialogue
{
    public interface IDialogueSystem : ISessionInitializable, ISessionOptimizable, IEventHandler, IEventSource, ISessionDisposable
    {
        public IDisposable SetCurrentActivity(IActivity activity);
    }
}

namespace Nifty.Events
{
    public interface IEventSource // : IHasReadOnlyMetadata
    {
        // this could be an extension method
        // public IDisposable Subscribe(IUriTerm eventType, IEventHandler listener);
        public IDisposable Subscribe(IAskQuery query, IEventHandler listener);
    }
    public interface IEventHandler // : IHasReadOnlyMetadata
    {
        public Task Handle(IEventSource source, IReadOnlyFormulaCollection @event, IReadOnlyFormulaCollection data);
    }
}

namespace Nifty.Knowledge
{
    public interface IReadOnlyFormulaCollection : ISubstitute<IReadOnlyFormulaCollection> //, IEventSource, INotifyChanged
    {
        public bool IsReadOnly { get; }
        public bool IsGround { get; }
        public bool IsInferred { get; }
        public bool IsValid { get; }
        public bool IsGraph { get; }
        public bool IsEnumerable { get; }

        public bool HasSchema { get; } // IHasReadOnlySchema
        public bool HasIdentifier { get; } // IHasReadOnlyIdentifier
        public bool HasMetadata { get; } // IHasReadOnlyMetadata
        public bool HasComposition { get; } // IHasReadOnlyComposition
        public bool HasConstraints { get; } // IHasReadOnlyConstraints
        //...

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
        Variable,
        Formula,
        Box
        // FormulaCollection
    }
    public interface ITerm : ISubstitute<ITerm>
    {
        public TermType TermType { get; }

        public bool IsGround { get; }

        // these could be extension methods
        //public bool IsPredicate(IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public int HasArity(IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public IEnumerable<ITerm> ClassesOfArgument(int index, IReadOnlySchema schema)
        //{
        //    throw new NotImplementedException();
        //}

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
    //public interface ILiteral : ITerm
    //{
    //    public string Value { get; }
    //    public string? Language { get; }
    //    public string? Datatype { get; }
    //}
    public interface IVariable : ITerm
    {
        public string Name { get; }
    }

    // considering a model which includes the capability of boxing and unboxing system builtins and other datatypes
    // implementations might include optimized internal datatypes: BoxedString, BoxedLiteral, BoxedInt32, BoxedFloat, BoxedDouble, etc...
    public interface IBox : ITerm
    {
        public object Value { get; }
        public BoxType BoxType { get; } // resembling System.TypeCode
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
        Literal = 19
    }
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

    public interface IFormula : ITerm
    {
        public ITerm Predicate { get; }

        public int Count { get; }
        public ITerm this[int index] { get; }
    }

    public interface IHasVariables
    {
        public IReadOnlyList<IVariable> GetVariables();
    }
    public interface ISubstitute<out T> : IHasVariables
    {
        public T Substitute(IReadOnlyDictionary<IVariable, ITerm> map);
    }

    public interface IHasReadOnlyIdentifier
    {
        public ITerm Identifier { get; }
    }

    public interface IHasReadOnlyMetadata : IHasReadOnlyIdentifier
    {
        public IReadOnlyFormulaCollection About { get; }
    }
    public interface IHasMetadata : IHasReadOnlyMetadata
    {
        public new IFormulaCollection About { get; }
    }

    public interface IHasReadOnlyComposition
    {
        // the formula describing the composition of this formula collection, e.g., {a} UNION {b}, builtin:union(a, b), can have metadata and a validating schema
        public IReadOnlyFormulaCollection Composition { get; }
    }
    public interface IHasComposition : IHasReadOnlyComposition
    {
        public new IFormulaCollection Composition { get; }
    }

    public interface IHasReadOnlyConstraints
    {
        // constraints can be added to sets of formulas, typically those sets with variables, e.g., using Filter()
        // and/or is this part of the metadata from IHasReadOnlyMetadata
        // should all formula collections have this or should there be a property 'HasConstraints'?
        // might these be formulas or metadata formulas, e.g., builtin:holds(identifier, constraint_1)
        // public IReadOnlyFormulaCollection Constraints { get; }

        public IReadOnlyFormulaCollection Constraints { get; }
    }
    public interface IHasConstraints : IHasReadOnlyConstraints
    {
        public new IFormulaCollection Constraints { get; }
    }

    public interface ITermVisitor
    {
        public object Visit(IAny term);
        public object Visit(IUri term);
        public object Visit(IBlank term);
        public object Visit(IVariable term);
        public object Visit(IFormula formula);
        public object Visit(IBox term);
    }

    public interface IKnowledgebase : IFormulaCollection, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable { }
}

namespace Nifty.Knowledge.Probabilistic
{

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

    // A query is a formula collection; it can accumulate formulas as it is constructed (instead of only accumulating metadata)
    // and it has an auxiliary formula collection with one formula, its composition, which resembles IQueryable::Expression.
    public interface IQuery : IReadOnlyFormulaCollection, IHasReadOnlyComposition
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
}

namespace Nifty.Knowledge.Schema
{
    // Schemas should be sufficiently expressive so as to validate those formalas representing queries.
    // That is, n-ary formulas are constructed as developers make use of fluent interfaces to construct queries and these formulas should be able to be validated by schema.

    public interface IReadOnlySchema : IReadOnlyFormulaCollection
    {
        //public Task<bool> Validate(IFormula formula);
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

    // to do: consider kinds of actions upon formula collections and knowledge graphs, e.g., simple (deltas / diffs), query-based updates / rules, composite, etc.
    //        updates could have properties such as being reversible, having an undo method (see also: transactions)
    //        how might this model of knowledgebase updates pertain to planning actions, action sequences, and plans?

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
    public interface ILog : ISessionInitializable, IEventHandler, ISessionDisposable
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
    // utilizing mutable ordered dictionaries, observers can provide feedback to observables with respect to the numerical weights on hypotheses
    // these dictionaries are ordered, sorted, so that consumers can inspect and enumerate the key-value pairs in order of decreasing weights on the hypotheses
    // downstream observers could, then, utilize reasoners to prune "deltas" which result in paradoxes by setting the numerical weights of the relevant "deltas" to 0 and/or by removing them from dictionary instances
    // as envisioned, feedback propagates across components, enabling adaptation and learning
    // dictionary implementations might implement INotifyCollectionChanged (see also: https://gist.github.com/kzu/cfe3cb6e4fe3efea6d24) and/or receive callbacks in their constructors
    // these scenarios might be benefitted by a new interface type, perhaps one extending IDictionary<T, float>
    // see also: https://en.wikipedia.org/wiki/Online_algorithm

    //public interface IOnlineNaturalLanguageParser : IObserver<IDictionary<string, float>>, IObservable<IDictionary<IUpdate, float>> { }
    public interface IOnlineNaturalLanguageParser : IObserver<IOrderedDictionary<string, float>>, IObservable<IOrderedDictionary<IUpdate, float>> { }
    // public interface IOnlineNaturalLanguageParser : System.Reactive.Subjects.ISubject<IOrderedDictionary<string, float>, IOrderedDictionary<IFormulaCollectionUpdate, float>> { }
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

        public IChannelCollection UserInterface { get; }

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

namespace Nifty.Transactions
{
    public interface ITransaction : IDisposable
    {
        public void Commit();
        public void Rollback();
    }
}

namespace Nifty
{
    public static partial class Keys
    {
        public static class Semantics
        {
            public static class Xsd
            {
                // https://docs.microsoft.com/en-us/dotnet/standard/data/xml/mapping-xml-data-types-to-clr-types

                public static readonly IUri @string = Factory.Uri("http://www.w3.org/2001/XMLSchema#string");

                public static readonly IUri @duration = Factory.Uri("http://www.w3.org/2001/XMLSchema#duration");
                public static readonly IUri @dateTime = Factory.Uri("http://www.w3.org/2001/XMLSchema#dateTime");
                public static readonly IUri @time = Factory.Uri("http://www.w3.org/2001/XMLSchema#time");
                public static readonly IUri @date = Factory.Uri("http://www.w3.org/2001/XMLSchema#date");
                //...
                public static readonly IUri @anyURI = Factory.Uri("http://www.w3.org/2001/XMLSchema#anyURI");
                public static readonly IUri @QName = Factory.Uri("http://www.w3.org/2001/XMLSchema#QName");

                public static readonly IUri @boolean = Factory.Uri("http://www.w3.org/2001/XMLSchema#boolean");

                public static readonly IUri @byte = Factory.Uri("http://www.w3.org/2001/XMLSchema#byte");
                public static readonly IUri @unsignedByte = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedByte");
                public static readonly IUri @short = Factory.Uri("http://www.w3.org/2001/XMLSchema#short");
                public static readonly IUri @unsignedShort = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedShort");
                public static readonly IUri @int = Factory.Uri("http://www.w3.org/2001/XMLSchema#int");
                public static readonly IUri @unsignedInt = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedInt");
                public static readonly IUri @long = Factory.Uri("http://www.w3.org/2001/XMLSchema#long");
                public static readonly IUri @unsignedLong = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedLong");

                public static readonly IUri @decimal = Factory.Uri("http://www.w3.org/2001/XMLSchema#decimal");

                public static readonly IUri @float = Factory.Uri("http://www.w3.org/2001/XMLSchema#float");
                public static readonly IUri @double = Factory.Uri("http://www.w3.org/2001/XMLSchema#double");
            }
            public static class Dc
            {
                public static readonly IUri title = Factory.Uri("http://purl.org/dc/terms/title");
                public static readonly IUri description = Factory.Uri("http://purl.org/dc/terms/description");
            }
            public static class Swo
            {
                public static readonly IUri version = Factory.Uri("http://www.ebi.ac.uk/swo/SWO_0004000");
            }
            public static class Rdf
            {
                public static readonly IUri type = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
                public static readonly IUri subject = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#subject");
                public static readonly IUri predicate = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate");
                public static readonly IUri @object = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#object");

                public static readonly IUri Statement = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement");
            }
            public static class Foaf
            {
                public static readonly IUri name = Factory.Uri("http://xmlns.com/foaf/0.1/name");
            }
            public static class Lom
            {

            }
            public static class Eo
            {
                public static readonly IUri raisesEventType = Factory.Uri("http://www.event-ontology.org/raisesEventType");
                public static readonly IUri Event = Factory.Uri("http://www.event-ontology.org/Event");
            }
        }

        public static class Settings
        {
            public static readonly ISetting<bool> ShouldPerformAnalytics = Factory.Setting("http://www.settings.org/analytics/ShouldPerformAnalytics", false);
            public static readonly ISetting<bool> ShouldPerformConfigurationAnalytics = Factory.Setting("http://www.settings.org/analytics/ShouldPerformConfigurationAnalytics", false);
        }

        public static class Events
        {
            //public static readonly IUriTerm All = Factory.Uri("http://www.w3.org/2002/07/owl#Thing");

            public static readonly IUri InitializedSession = Factory.Uri("http://www.events.org/events/InitializedSession");
            public static readonly IUri ObtainedGenerator = Factory.Uri("http://www.events.org/events/ObtainedGenerator");
            public static readonly IUri GeneratingActivity = Factory.Uri("http://www.events.org/events/GeneratingActivity");
            public static readonly IUri GeneratedActivity = Factory.Uri("http://www.events.org/events/GeneratedActivity");
            public static readonly IUri ExecutingActivity = Factory.Uri("http://www.events.org/events/ExecutingActivity");
            public static readonly IUri ExecutedActivity = Factory.Uri("http://www.events.org/events/ExecutedActivity");
            public static readonly IUri DisposingSession = Factory.Uri("http://www.events.org/events/DisposingSession");

            public static class Data
            {
                public static readonly IUri Algorithm = Factory.Uri("urn:eventdata:Algorithm");
                public static readonly IUri Generator = Factory.Uri("urn:eventdata:Generator");
                public static readonly IUri Activity = Factory.Uri("urn:eventdata:Activity");
                public static readonly IUri User = Factory.Uri("urn:eventdata:User");
                public static readonly IUri Result = Factory.Uri("urn:eventdata:Result");
            }
        }
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

        public static IReadOnlySchema EmptySchema
        {
            get
            {
                throw new NotImplementedException();
            }
        }


        public static IReadOnlySchema ReadOnlyFormulaCollectionSchemaWithSelfSchema(IEnumerable<IFormula> formulas)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlySchema ReadOnlyFormulaCollectionSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlySchema ReadOnlyKnowledgeGraphSchemaWithSelfSchema(IEnumerable<IFormula> formulas)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlySchema ReadOnlyKnowledgeGraphSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static ISchema FormulaCollectionSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static ISchema KnowledgeGraphSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection ReadOnlyFormulaCollection(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollection FormulaCollection(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection ReadOnlyKnowledgeGraph(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollection KnowledgeGraph(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlySchema ReadOnlyFormulaCollectionSchemaWithSelfSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlySchema ReadOnlyFormulaCollectionSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlySchema ReadOnlyKnowledgeGraphSchemaWithSelfSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlySchema ReadOnlyKnowledgeGraphSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static ISchema FormulaCollectionSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static ISchema KnowledgeGraphSchema(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection ReadOnlyFormulaCollection(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollection FormulaCollection(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyFormulaCollection ReadOnlyKnowledgeGraph(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollection KnowledgeGraph(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }


        public static IQuery Query()
        {
            throw new NotImplementedException();
        }


        internal static IQuery Query(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema, IReadOnlyFormulaCollection composition)
        {
            throw new NotImplementedException();
        }
        internal static IAskQuery AskQuery(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema, IReadOnlyFormulaCollection composition)
        {
            throw new NotImplementedException();
        }
        internal static IConstructQuery ConstructQuery(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema, IReadOnlyFormulaCollection composition /* , ... */)
        {
            throw new NotImplementedException();
        }
        internal static ISelectQuery SelectQuery(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema, IReadOnlyFormulaCollection composition /* , ... */)
        {
            throw new NotImplementedException();
        }
        internal static IDescribeQuery DescribeQuery(IEnumerable<IFormula> formulas, ITerm identifier, IReadOnlyFormulaCollection meta, IReadOnlySchema schema, IReadOnlyFormulaCollection composition /* , ... */)
        {
            throw new NotImplementedException();
        }
    }

    // there might be other, possibly better, ways, e.g., allowing developers to provide formula collections which describe the terms to be combined into formulas
    // in this case, these would be generators which bind to the most specific predicates depending on the types of the terms, e.g., integers or complex numbers.
    public static partial class Formula
    {
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

        // would using lambda be benefitted by extending IFormula, e.g., ILambdaFormula : IFormula ?
        public static IFormula Lambda(ITerm body, params IVariable[]? parameters)
        {
            throw new NotImplementedException();
        }
    }

    public static partial class Extensions
    {
        // the expressiveness for querying formula collections with Nifty should be comparable with or exceed that of SPARQL for triple collections

        // "Fluent N-ary SPARQL"
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

        internal static bool TryGetSchema(this IReadOnlyFormulaCollection formulas, [NotNullWhen(true)] out IReadOnlySchema? schema)
        {
            if (formulas.HasSchema && formulas is IHasReadOnlySchema hasSchema)
            {
                schema = hasSchema.Schema;
                return true;
            }
            else
            {
                schema = null;
                return false;
            }
        }
        internal static bool TryGetIdentifier(this IReadOnlyFormulaCollection formulas, [NotNullWhen(true)] out ITerm? identifier)
        {
            if (formulas.HasIdentifier && formulas is IHasReadOnlyIdentifier hasIdentifier)
            {
                identifier = hasIdentifier.Identifier;
                return true;
            }
            else
            {
                identifier = null;
                return false;
            }
        }
        internal static bool TryGetMetadata(this IReadOnlyFormulaCollection formulas, [NotNullWhen(true)] out ITerm? identifier, [NotNullWhen(true)] out IReadOnlyFormulaCollection? metadata)
        {
            if (formulas.HasMetadata && formulas is IHasReadOnlyMetadata hasMetadata)
            {
                identifier = hasMetadata.Identifier;
                metadata = hasMetadata.About;
                return true;
            }
            else
            {
                identifier = null;
                metadata = null;
                return false;
            }
        }
        internal static bool TryGetComposition(this IReadOnlyFormulaCollection formulas, [NotNullWhen(true)] out IReadOnlyFormulaCollection? composition)
        {
            if (formulas.HasComposition && formulas is IHasReadOnlyComposition hasComposition)
            {
                composition = hasComposition.Composition;
                return true;
            }
            else
            {
                composition = null;
                return false;
            }
        }
        internal static bool TryGetConstraints(this IReadOnlyFormulaCollection formulas, [NotNullWhen(true)] out IReadOnlyFormulaCollection? constraints)
        {
            if (formulas.HasConstraints && formulas is IHasReadOnlyConstraints hasConstraints)
            {
                constraints = hasConstraints.Constraints;
                return true;
            }
            else
            {
                constraints = null;
                return false;
            }
        }
    }
}