using Nifty.Activities;
using Nifty.Algorithms;
using Nifty.Analytics;
using Nifty.Channels;
using Nifty.Common;
using Nifty.Configuration;
using Nifty.Dialogue;
using Nifty.Events;
using Nifty.Knowledge;
using Nifty.Knowledge.Graphs;
using Nifty.Knowledge.Reasoning.Derivation;
using Nifty.Knowledge.Schema;
using Nifty.Knowledge.Streaming;
using Nifty.Logging;
using Nifty.Modelling.Users;
using Nifty.Sessions;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Xml;

namespace Nifty.Activities
{
    public interface IActivityGeneratorStore : IHasReadOnlyKnowledgeGraph, /*IQueryable<IActivityGenerator>,*/ ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
    public interface IActivityGenerator : IHasReadOnlyKnowledgeGraph
    {
        // public IActivityPreconditions Preconditions { get; }
        // public IActivityEffects       Effects { get; }

        Task<IActivity> Generate(ISession session, CancellationToken cancellationToken);
    }
    public interface IActivity : IHasReadOnlyKnowledgeGraph, ISessionInitializable, ISessionDisposable, IDisposable
    {
        //public IActivityGenerator Generator { get; }

        //public IActivity Parent { get; }
        //public IReadOnlyList<IActivity> Children { get; }

        // public IActivityPreconditions Preconditions { get; }
        // public IActivityEffects       Effects { get; }

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
    public interface IAlgorithm : IHasReadOnlyKnowledgeGraph, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable
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

namespace Nifty.Channels
{
    public interface IChannel { }

    public interface IChannelCollection { }
}

namespace Nifty.Collections
{
    public interface ILabeledCollection<TItem, TLabel> : ICollection<TItem>
    {
        ICollection<TLabel> Labels { get; }
        ICollection<TItem> WithLabel(TLabel label);
    }
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
        public event EventHandler Changed;
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
        public IUriTerm Term { get; }
        public T DefaultValue { get; }
    }

    public interface IConfiguration : ISessionInitializable, ISessionOptimizable, ISessionDisposable, INotifyChanged
    {
        public bool About(IUriTerm setting, [NotNullWhen(true)] out IReadOnlyKnowledgeGraph? about);
        public bool About(IUriTerm setting, [NotNullWhen(true)] out IReadOnlyKnowledgeGraph? about, string language);
        public bool TryGetSetting(IUriTerm setting, [NotNullWhen(true)] out IConvertible? value);
        public bool TryGetSetting(IUriTerm setting, [NotNullWhen(true)] out IConvertible? value, string language);
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
    // event listeners could subscribe to those messages or events described by a query
    // and, resembling IObservable<>, event listeners could hold onto an IDisposable for unsubscribing
    public interface IEventSource : IHasReadOnlyKnowledgeGraph
    {
        public IDisposable Subscribe(IUriTerm eventType, IEventHandler listener)
        {
            var x = Factory.Variable("x");
            var triple = Factory.TriplePSO(Keys.Semantics.Rdf.type, x, eventType);
            var query = Factory.ReadOnlyKnowledgeGraph(new ITriple[] { triple });
            return Subscribe(x, query, listener);
        }
        public IDisposable Subscribe(IVariableTerm eventVariable, IReadOnlyKnowledgeGraph query, IEventHandler listener);
    }
    public interface IEventHandler // : IHasReadOnlyKnowledgeGraph
    {
        public Task Handle(IEventSource source, ITerm eventInstance, IReadOnlyKnowledgeGraph aboutEventInstance, ITerm eventData, IReadOnlyKnowledgeGraph aboutEventData);
    }
}

namespace Nifty.Knowledge
{
    public interface IReadOnlyFormulaCollection : IHasReadOnlyFormulaCollectionSchema, IEventSource, INotifyChanged
    {
        public IQueryable<ITerm> Predicates { get { return this.Contents.Select(f => f.Predicate).Distinct(); } }

        public IQueryable<IFormula> Contents { get; }

        public bool IsGround { get; }
        public bool IsReadOnly { get; }
        public bool IsInferred { get; }
        public bool IsValid { get; }
        public bool IsGraph { get; }

        public bool Contains(IFormula formula)
        {
            return Find(formula).Any();
        }

        public IEnumerable<IDerivation> Derivations(IFormula formula);

        public IQueryable<IFormula> Find(IFormula formulas);

        public int Count();
        public int Count(IFormula formula);
        public int Count(IReadOnlyFormulaCollection query);

        public IFormulaCollectionDifference DifferenceFrom(IReadOnlyFormulaCollection other);
        public IReadOnlyFormulaCollection Apply(IFormulaCollectionDifference change);

        public IReadOnlyFormulaCollection Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map);

        public IEnumerable<IReadOnlyDictionary<IVariableTerm, ITerm>> Query(IReadOnlyFormulaCollection query);
        public IEnumerable<IReadOnlyFormulaCollection> Query2(IReadOnlyFormulaCollection query)
        {
            return Query(query).Select(map => query.Substitute(map));
        }
    }
    public interface IFormulaCollection : IReadOnlyFormulaCollection
    {
        public bool Add(IFormula formula);
        public bool Add(IReadOnlyFormulaCollection formulas);

        public bool Remove(IFormula formula);
        public bool Remove(IReadOnlyFormulaCollection formulas);

        public bool Update(IFormulaCollectionDifference change);
    }

    public enum TermType
    {
        Any,
        Blank,
        Uri,
        Literal,
        Variable,
        Formula,
        Triple,
        FormulaCollection,
        KnowledgeGraph
    }
    public interface ITerm
    {
        public TermType TermType { get; }

        public bool IsGround { get; }

        //public bool IsPredicate(IReadOnlyFormulaCollectionSchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public int HasArity(IReadOnlyFormulaCollectionSchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public IEnumerable<ITerm> ClassesOfArgument(int index, IReadOnlyFormulaCollectionSchema schema)
        //{
        //    throw new NotImplementedException();
        //}

        public object Visit(ITermVisitor visitor);

        public bool Matches(ITerm other);

        public ITerm Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map);

        public string? ToString(XmlNamespaceManager xmlns, bool quoting);
    }

    public interface IAnyTerm : ITerm { }
    public interface IUriTerm : ITerm
    {
        public string Uri { get; }

        //public string Namespace { get; }
        //public string LocalName { get; }
    }
    public interface IBlankTerm : ITerm
    {
        public string Label { get; }
    }
    public interface ILiteralTerm : ITerm
    {
        public string Value { get; }
        public string? Language { get; }
        public string? Datatype { get; }
    }
    public interface IVariableTerm : ITerm
    {
        public string Name { get; }
    }

    public interface IFormula : ITerm
    {
        public ITerm Predicate { get; }

        public int Count { get; }
        public ITerm this[int index] { get; }

        public bool Matches(IFormula other);

        public bool IsValid(IReadOnlyFormulaCollectionSchema schema)
        {
            return schema.Validate(this).Result;
        }
    }

    public interface IHasTerm
    {
        public ITerm Term { get; }
    }

    public interface ITermVisitor
    {
        public object Visit(IAnyTerm term);
        public object Visit(IUriTerm term);
        public object Visit(IBlankTerm term);
        public object Visit(ILiteralTerm term);
        public object Visit(IVariableTerm term);
        //public object Visit(IFormula formula);
        //public object Visit(ITriple triple);
        //public object Visit(IReadOnlyFormulaCollection collection);
        //public object Visit(IReadOnlyKnowledgeGraph graph);
    }

    public interface IKnowledgebase : IFormulaCollection, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable { }
}

namespace Nifty.Knowledge.Graphs
{
    public interface ITriple : IFormula
    {
        public ITerm Subject { get; }
        public ITerm Object { get; }

        public bool Matches(ITriple other);

        public bool IsValid(IReadOnlyKnowledgeGraphSchema schema)
        {
            return schema.Validate(this).Result;
        }
    }

    public interface IReadOnlyKnowledgeGraph : /*IReadOnlyMultigraph<ITerm, ITriple>,*/ IReadOnlyFormulaCollection, IHasReadOnlyKnowledgeGraphSchema, IEventSource, INotifyChanged
    {
        public IQueryable<ITerm> Subjects { get { return this.Contents.Select(t => t.Subject).Distinct(); } }
        public IQueryable<ITerm> Objects { get { return this.Contents.Select(t => t.Object).Distinct(); } }

        public new IQueryable<ITriple> Contents { get; }

        public bool Contains(ITriple triple)
        {
            return Find(triple).Any();
        }
        public bool Contains(ITriple triple, [NotNullWhen(true)] out ITerm? reified);

        public IEnumerable<IDerivation> Derivations(ITriple triple);

        public IQueryable<ITriple> Find(ITriple triple);

        public int Count(ITriple triple);
        public int Count(IReadOnlyKnowledgeGraph query);

        public IKnowledgeGraphDifference DifferenceFrom(IReadOnlyKnowledgeGraph other);
        public IReadOnlyKnowledgeGraph Apply(IKnowledgeGraphDifference change);

        public new IReadOnlyKnowledgeGraph Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map);

        public IEnumerable<IReadOnlyDictionary<IVariableTerm, ITerm>> Query(IReadOnlyKnowledgeGraph query);
        public IEnumerable<IReadOnlyKnowledgeGraph> Query2(IReadOnlyKnowledgeGraph query)
        {
            return Query(query).Select(result => query.Substitute(result));
        }
    }
    public interface IKnowledgeGraph : IFormulaCollection, IReadOnlyKnowledgeGraph
    {
        public bool Add(ITriple triple);
        public bool Add(ITriple triple, [NotNullWhen(true)] out ITerm? reified);
        public bool Remove(ITriple triple);

        public bool Add(IReadOnlyKnowledgeGraph graph);
        public bool Remove(IReadOnlyKnowledgeGraph graph);

        public bool Update(IKnowledgeGraphDifference change);

        public new IKnowledgeGraph Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map);
    }

    public interface IHasReadOnlyKnowledgeGraph : IHasTerm
    {
        public IReadOnlyKnowledgeGraph About { get; }
    }
    public interface IHasKnowledgeGraph : IHasReadOnlyKnowledgeGraph
    {
        public new IKnowledgeGraph About { get; }
    }
}

namespace Nifty.Knowledge.Graphs.Serialization
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
        public void Serialize(IKnowledgeGraph graph);
    }
}

namespace Nifty.Knowledge.Probabilistic
{

}

namespace Nifty.Knowledge.Reasoning
{
    public interface IReasoner : IHasReadOnlyKnowledgeGraph
    {
        public IConfiguration Configuration { get; }

        Task<IReasoner> BindRules(IReadOnlyFormulaCollection rules);

        Task<IInferredReadOnlyFormulaCollection> Bind(IReadOnlyFormulaCollection collection);
    }

    public interface IInferredReadOnlyFormulaCollection : IReadOnlyFormulaCollection
    {
        public IReasoner Reasoner { get; }
        public IReadOnlyFormulaCollection Base { get; }
    }

    public interface IKnowledgeGraphReasoner : IReasoner
    {
        Task<IKnowledgeGraphReasoner> BindRules(IReadOnlyKnowledgeGraph rules);

        Task<IInferredReadOnlyKnowledgeGraph> Bind(IReadOnlyKnowledgeGraph graph);
    }

    public interface IInferredReadOnlyKnowledgeGraph : IReadOnlyKnowledgeGraph, IInferredReadOnlyFormulaCollection
    {
        public new IKnowledgeGraphReasoner Reasoner { get; }
        public new IReadOnlyKnowledgeGraph Base { get; }
    }
}

namespace Nifty.Knowledge.Reasoning.Derivation
{
    public interface IDerivation
    {

    }
}

namespace Nifty.Knowledge.Schema
{
    public interface IReadOnlyFormulaCollectionSchema : IReadOnlyFormulaCollection
    {
        public Task<bool> Validate(IFormula formula);
        public Task<bool> Validate(IReadOnlyFormulaCollection formulas);
    }
    public interface IFormulaCollectionSchema : IReadOnlyFormulaCollectionSchema, IFormulaCollection { }

    public interface IReadOnlyKnowledgeGraphSchema : IReadOnlyKnowledgeGraph, IReadOnlyFormulaCollectionSchema
    {
        public Task<bool> Validate(ITriple triple);
        public Task<bool> Validate(IReadOnlyKnowledgeGraph graph);
    }
    public interface IKnowledgeGraphSchema : IReadOnlyKnowledgeGraphSchema, IKnowledgeGraph, IFormulaCollectionSchema { }

    public interface IHasReadOnlyFormulaCollectionSchema
    {
        public IReadOnlyFormulaCollectionSchema Schema { get; }
    }
    public interface IHasFormulaCollectionSchema : IHasReadOnlyFormulaCollectionSchema
    {
        public new IFormulaCollectionSchema Schema { get; }
    }

    public interface IHasReadOnlyKnowledgeGraphSchema : IHasReadOnlyFormulaCollectionSchema
    {
        public new IReadOnlyKnowledgeGraphSchema Schema { get; }
    }
    public interface IHasKnowledgeGraphSchema : IHasReadOnlyKnowledgeGraphSchema, IHasFormulaCollectionSchema
    {
        public new IKnowledgeGraphSchema Schema { get; }
    }
}

namespace Nifty.Knowledge.Streaming
{
    // https://www.w3.org/community/rsp/
    // https://www.w3.org/community/rsp/wiki/RDF_Stream_Models
    // https://github.com/streamreasoning/rsp4j
    // https://github.com/dotnet/reactive
    // https://en.wikipedia.org/wiki/Delta_encoding

    public interface IFormulaCollectionDifference
    {
        public IEnumerable<IFormula> Removals { get; }
        public IEnumerable<IFormula> Additions { get; }
    }

    public interface IKnowledgeGraphDifference : IFormulaCollectionDifference
    {
        public new IEnumerable<ITriple> Removals { get; }
        public new IEnumerable<ITriple> Additions { get; }
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

    // to do: explore expanding general-purpose pull/push interfaces, IEnumerable/IObservable, and asynchronous varieties, IAsyncEnumerable/IAsyncObservable, with reinforcement learning functionalities
    // see also: https://www.gymlibrary.ml/content/api/
    // see also: https://www.gymlibrary.ml/_images/AE_loop.png

    public interface IEnumerator<out TAction, in TObservation, in TReward> : IDisposable
    {
        public bool MoveNext(TObservation observation, TReward reward);
        public TAction Current { get; }
    }

    public interface IEnumerable<out TAction, in TObservation, in TReward>
    {
        public IEnumerator<TAction, TObservation, TReward> GetEnumerator();
    }

    public interface IObservable<out TAction, TObservation, TReward>
    {
        IDisposable Subscribe(IObserver<TAction, TObservation, TReward> observer);
    }

    public interface IObserver<in TAction, TObservation, TReward>
    {
        void OnCompleted();
        void OnError(Exception error);
        (TObservation observation, TReward reward, bool done) OnNext(TAction action);
    }

    public interface IAsyncEnumerator<out TAction, in TObservation, in TReward> : IAsyncDisposable
    {
        public ValueTask<bool> MoveNextAsync(TObservation observation, TReward reward);
        TAction Current { get; }
    }

    public interface IAsyncEnumerable<out TAction, in TObservation, in TReward>
    {
        IAsyncEnumerator<TAction, TObservation, TReward> GetAsyncEnumerator(CancellationToken cancellationToken = default);
    }

    public interface IAsyncObservable<out TAction, TObservation, TReward>
    {
        IDisposable Subscribe(IAsyncObserver<TAction, TObservation, TReward> observer);
    }

    public interface IAsyncObserver<in TAction, TObservation, TReward>
    {
        Task OnCompleted();
        Task OnError(Exception error);
        Task<(TObservation observation, TReward reward, bool done)> OnNext(TAction value);
    }
}

namespace Nifty.Messaging
{

}

namespace Nifty.Modelling.Domains
{
    public interface IDomainModel : IHasReadOnlyKnowledgeGraph, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
}

namespace Nifty.Modelling.Users
{
    public interface IUserModel : IHasReadOnlyKnowledgeGraph, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
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

    public interface ISession : IHasReadOnlyKnowledgeGraph, IInitializable, IOptimizable, IEventSource, IEventHandler, IDisposable, IAsyncEnumerable<IActivityGenerator>
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
        void Commit();
        void Rollback();
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

                public static readonly IUriTerm @string = Factory.Uri("http://www.w3.org/2001/XMLSchema#string");

                public static readonly IUriTerm @duration = Factory.Uri("http://www.w3.org/2001/XMLSchema#duration");
                public static readonly IUriTerm @dateTime = Factory.Uri("http://www.w3.org/2001/XMLSchema#dateTime");
                public static readonly IUriTerm @time = Factory.Uri("http://www.w3.org/2001/XMLSchema#time");
                public static readonly IUriTerm @date = Factory.Uri("http://www.w3.org/2001/XMLSchema#date");
                //...
                public static readonly IUriTerm @anyURI = Factory.Uri("http://www.w3.org/2001/XMLSchema#anyURI");
                public static readonly IUriTerm @QName = Factory.Uri("http://www.w3.org/2001/XMLSchema#QName");

                public static readonly IUriTerm @boolean = Factory.Uri("http://www.w3.org/2001/XMLSchema#boolean");

                public static readonly IUriTerm @byte = Factory.Uri("http://www.w3.org/2001/XMLSchema#byte");
                public static readonly IUriTerm @unsignedByte = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedByte");
                public static readonly IUriTerm @short = Factory.Uri("http://www.w3.org/2001/XMLSchema#short");
                public static readonly IUriTerm @unsignedShort = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedShort");
                public static readonly IUriTerm @int = Factory.Uri("http://www.w3.org/2001/XMLSchema#int");
                public static readonly IUriTerm @unsignedInt = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedInt");
                public static readonly IUriTerm @long = Factory.Uri("http://www.w3.org/2001/XMLSchema#long");
                public static readonly IUriTerm @unsignedLong = Factory.Uri("http://www.w3.org/2001/XMLSchema#unsignedLong");

                public static readonly IUriTerm @decimal = Factory.Uri("http://www.w3.org/2001/XMLSchema#decimal");

                public static readonly IUriTerm @float = Factory.Uri("http://www.w3.org/2001/XMLSchema#float");
                public static readonly IUriTerm @double = Factory.Uri("http://www.w3.org/2001/XMLSchema#double");
            }
            public static class Dc
            {
                public static readonly IUriTerm title = Factory.Uri("http://purl.org/dc/terms/title");
                public static readonly IUriTerm description = Factory.Uri("http://purl.org/dc/terms/description");
            }
            public static class Swo
            {
                public static readonly IUriTerm version = Factory.Uri("http://www.ebi.ac.uk/swo/SWO_0004000");
            }
            public static class Rdf
            {
                public static readonly IUriTerm type = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
                public static readonly IUriTerm subject = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#subject");
                public static readonly IUriTerm predicate = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate");
                public static readonly IUriTerm @object = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#object");

                public static readonly IUriTerm Statement = Factory.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement");
            }
            public static class Foaf
            {
                public static readonly IUriTerm name = Factory.Uri("http://xmlns.com/foaf/0.1/name");
            }
            public static class Lom
            {

            }
            public static class Eo
            {
                public static readonly IUriTerm raisesEventType = Factory.Uri("http://www.event-ontology.org/raisesEventType");
                public static readonly IUriTerm Event = Factory.Uri("http://www.event-ontology.org/Event");
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

            public static readonly IUriTerm InitializedSession = Factory.Uri("http://www.events.org/events/InitializedSession");
            public static readonly IUriTerm ObtainedGenerator = Factory.Uri("http://www.events.org/events/ObtainedGenerator");
            public static readonly IUriTerm GeneratingActivity = Factory.Uri("http://www.events.org/events/GeneratingActivity");
            public static readonly IUriTerm GeneratedActivity = Factory.Uri("http://www.events.org/events/GeneratedActivity");
            public static readonly IUriTerm ExecutingActivity = Factory.Uri("http://www.events.org/events/ExecutingActivity");
            public static readonly IUriTerm ExecutedActivity = Factory.Uri("http://www.events.org/events/ExecutedActivity");
            public static readonly IUriTerm DisposingSession = Factory.Uri("http://www.events.org/events/DisposingSession");

            public static class Data
            {
                public static readonly IUriTerm Algorithm = Factory.Uri("urn:eventdata:Algorithm");
                public static readonly IUriTerm Generator = Factory.Uri("urn:eventdata:Generator");
                public static readonly IUriTerm Activity = Factory.Uri("urn:eventdata:Activity");
                public static readonly IUriTerm User = Factory.Uri("urn:eventdata:User");
                public static readonly IUriTerm Result = Factory.Uri("urn:eventdata:Result");
            }
        }
    }

    public static partial class Factory
    {
        internal sealed class AnyTerm : IAnyTerm
        {
            public TermType TermType => TermType.Any;

            public bool IsGround => false;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                return true;
            }

            public ITerm Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map)
            {
                throw new NotImplementedException();
            }

            public string? ToString(XmlNamespaceManager xmlns, bool quoting)
            {
                throw new NotImplementedException();
            }

            public object Visit(ITermVisitor visitor)
            {
                throw new NotImplementedException();
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        internal sealed class BlankTerm : IBlankTerm
        {
            public BlankTerm(string label)
            {
                m_label = label;
            }
            private readonly string m_label;

            public string Label => m_label;

            public TermType TermType => TermType.Blank;

            public bool IsGround => true;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                throw new NotImplementedException();
            }

            public ITerm Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map)
            {
                throw new NotImplementedException();
            }

            public string? ToString(XmlNamespaceManager xmlns, bool quoting)
            {
                throw new NotImplementedException();
            }

            public object Visit(ITermVisitor visitor)
            {
                return visitor.Visit(this);
            }

            public sealed override int GetHashCode()
            {
                return m_label.GetHashCode();
            }
        }
        internal sealed class UriTerm : IUriTerm
        {
            public UriTerm(string uri)
            {
                m_uri = uri;
            }

            private readonly string m_uri;

            public string Uri => m_uri;

            public TermType TermType => TermType.Uri;

            public bool IsGround => true;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                throw new NotImplementedException();
            }

            public ITerm Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map)
            {
                throw new NotImplementedException();
            }

            public string? ToString(XmlNamespaceManager xmlns, bool quoting)
            {
                throw new NotImplementedException();
            }

            public object Visit(ITermVisitor visitor)
            {
                return visitor.Visit(this);
            }

            public sealed override int GetHashCode()
            {
                return m_uri.GetHashCode();
            }
        }
        internal sealed class LiteralTerm : ILiteralTerm
        {
            public LiteralTerm(string value, string? language, IUriTerm? datatype)
            {
                m_value = value;
                m_language = language;
                m_datatype = datatype?.Uri;
            }

            private readonly string m_value;
            private readonly string? m_language;
            private readonly string? m_datatype;

            public string Value => m_value;

            public string? Language => m_language;

            public string? Datatype => m_datatype;

            public TermType TermType => TermType.Literal;

            public bool IsGround => true;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                throw new NotImplementedException();
            }

            public ITerm Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map)
            {
                throw new NotImplementedException();
            }

            public string? ToString(XmlNamespaceManager xmlns, bool quoting)
            {
                throw new NotImplementedException();
            }

            public object Visit(ITermVisitor visitor)
            {
                return visitor.Visit(this);
            }

            public sealed override int GetHashCode()
            {
                return m_value.GetHashCode()
                    + (m_language != null ? m_language.GetHashCode() : 0)
                    + (m_datatype != null ? m_datatype.GetHashCode() : 0);
            }
        }
        internal sealed class VariableTerm : IVariableTerm
        {
            public VariableTerm(string name)
            {
                m_name = name;
            }

            private readonly string m_name;

            public string Name => m_name;

            public TermType TermType => TermType.Variable;

            public bool IsGround => false;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                throw new NotImplementedException();
            }

            public ITerm Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map)
            {
                throw new NotImplementedException();
            }

            public string? ToString(XmlNamespaceManager xmlns, bool quoting)
            {
                throw new NotImplementedException();
            }

            public object Visit(ITermVisitor visitor)
            {
                throw new NotImplementedException();
            }

            public sealed override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        internal sealed class SettingImpl<T> : ISetting<T>
        {
            public SettingImpl(IUriTerm term, T defaultValue)
            {
                m_term = term;
                m_defaultValue = defaultValue;
            }

            private readonly IUriTerm m_term;
            private readonly T m_defaultValue;

            public IUriTerm Term => m_term;

            public T DefaultValue => m_defaultValue;
        }

        public static ISetting<T> Setting<T>(string uri, T defaultValue)
        {
            return new SettingImpl<T>(Uri(uri), defaultValue);
        }

        private static readonly IAnyTerm s_any = new AnyTerm();
        private static int counter = 0;

        public static IAnyTerm Any()
        {
            return s_any;
        }
        public static IAnyTerm Any(string language)
        {
            throw new NotImplementedException();
        }
        public static IUriTerm Uri(string uri)
        {
            return new UriTerm(uri);
        }
        public static IBlankTerm Blank()
        {
            return new BlankTerm("urn:blank:" + counter++);
        }
        public static IBlankTerm Blank(string id)
        {
            return new BlankTerm(id);
        }
        public static IVariableTerm Variable(string name)
        {
            return new VariableTerm(name);
        }
        public static ILiteralTerm Literal(bool value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@boolean);
        }
        public static ILiteralTerm Literal(sbyte value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@byte);
        }
        public static ILiteralTerm Literal(byte value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@unsignedByte);
        }
        public static ILiteralTerm Literal(short value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@short);
        }
        public static ILiteralTerm Literal(ushort value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@unsignedShort);
        }
        public static ILiteralTerm Literal(int value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@int);
        }
        public static ILiteralTerm Literal(uint value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@unsignedInt);
        }
        public static ILiteralTerm Literal(long value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@long);
        }
        public static ILiteralTerm Literal(ulong value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@unsignedLong);
        }
        public static ILiteralTerm Literal(float value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@float);
        }
        public static ILiteralTerm Literal(double value)
        {
            return new LiteralTerm(value.ToString(), null, Keys.Semantics.Xsd.@double);
        }
        public static ILiteralTerm Literal(string value)
        {
            return new LiteralTerm(value, null, Keys.Semantics.Xsd.@string);
        }
        public static ILiteralTerm Literal(string value, string language)
        {
            return new LiteralTerm(value, language, Keys.Semantics.Xsd.@string);
        }
        public static ILiteralTerm Literal(string value, string language, IUriTerm datatypeUri)
        {
            return new LiteralTerm(value, language, datatypeUri);
        }
        public static ILiteralTerm Literal(string value, IUriTerm datatypeUri)
        {
            return new LiteralTerm(value, null, datatypeUri);
        }

        public static IFormula Formula(ITerm predicate, params ITerm[] arguments)
        {
            throw new NotImplementedException();
        }

        public static ITriple TriplePSO(ITerm predicate, ITerm subject, ITerm @object)
        {
            throw new NotImplementedException();
        }
        public static ITriple TripleSPO(ITerm subject, ITerm predicate, ITerm @object)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyKnowledgeGraphSchema EmptyKnowledgeGraphSchema
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        public static IReadOnlyFormulaCollectionSchema EmptyFormulaCollectionSchema
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public static IKnowledgeGraph KnowledgeGraph(IReadOnlyKnowledgeGraphSchema schema)
        {
            throw new NotImplementedException();
        }
        public static IKnowledgeGraph KnowledgeGraph(IEnumerable<ITriple> statements, IReadOnlyKnowledgeGraphSchema schema)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyKnowledgeGraph ReadOnlyKnowledgeGraph(IEnumerable<ITriple> statements)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyKnowledgeGraph ReadOnlyKnowledgeGraph(IEnumerable<ITriple> statements, IReadOnlyKnowledgeGraphSchema schema)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyKnowledgeGraph ParseReadOnlyKnowledgeGraph(ContentType type, Stream stream)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyKnowledgeGraph ParseReadOnlyKnowledgeGraph(ContentType type, Stream stream, IReadOnlyKnowledgeGraphSchema schema)
        {
            throw new NotImplementedException();
        }

        public static IKnowledgeGraphSchema KnowledgeGraphSchema()
        {
            throw new NotImplementedException();
        }
        public static IKnowledgeGraphSchema KnowledgeGraphSchema(IReadOnlyKnowledgeGraphSchema schema)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyKnowledgeGraphSchema ParseReadOnlyKnowledgeGraphSchema(ContentType type, Stream stream)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyKnowledgeGraphSchema ParseReadOnlyKnowledgeGraphSchema(ContentType type, Stream stream, IReadOnlyKnowledgeGraphSchema schema)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyFormulaCollection ReadOnlyFormulaCollection(IEnumerable<IFormula> statements, IReadOnlyFormulaCollectionSchema schema)
        {
            throw new NotImplementedException();
        }
    }

    public static partial class Extensions
    {
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, bool value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, sbyte value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, byte value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, short value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, ushort value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, int value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, uint value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, long value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, ulong value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, float value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, double value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IKnowledgeGraph graph, ITerm predicate, ITerm subject, string value)
        {
            return graph.Add(Factory.TriplePSO(predicate, subject, Factory.Literal(value)));
        }
        //...

        public static IEnumerable<ITerm> GetClasses(this IHasReadOnlyKnowledgeGraph thing)
        {
            return thing.About.Find(Factory.TriplePSO(Keys.Semantics.Rdf.type, thing.Term, Factory.Any())).Select(t => t.Object).Distinct();
        }
        public static bool HasClass(this IHasReadOnlyKnowledgeGraph thing, ITerm type)
        {
            return thing.About.Contains(Factory.TriplePSO(Keys.Semantics.Rdf.type, thing.Term, type));
        }
        public static IEnumerable<ITerm> GetProperties(this IHasReadOnlyKnowledgeGraph thing)
        {
            return thing.About.Find(Factory.TriplePSO(Factory.Any(), thing.Term, Factory.Any())).Select(t => t.Predicate).Distinct();
        }
        public static bool HasProperty(this IHasReadOnlyKnowledgeGraph thing, ITerm predicate)
        {
            return thing.About.Contains(Factory.TriplePSO(predicate, thing.Term, Factory.Any()));
        }
        public static IEnumerable<ITerm> GetEvents(this IEventSource thing)
        {
            return thing.About.Find(Factory.TriplePSO(Keys.Semantics.Eo.raisesEventType, thing.Term, Factory.Any())).Select(t => t.Object).Distinct();
        }
        public static bool HasEvent(this IEventSource thing, ITerm eventCategory)
        {
            return thing.About.Contains(Factory.TriplePSO(Keys.Semantics.Eo.raisesEventType, thing.Term, eventCategory));
        }

        public static T Setting<T>(this ISession session, ISetting<T> setting)
        {
            return session.Configuration.TryGetSetting(setting.Term, out IConvertible? value) ? (T)value.ToType(typeof(T), System.Globalization.CultureInfo.CurrentCulture) : setting.DefaultValue;
        }
        public static bool About<T>(this ISession session, ISetting<T> setting, out string? title, out string? description, string? language = null)
        {
            var term = setting.Term;
            bool b;
            IReadOnlyKnowledgeGraph? about;

            if (language == null)
            {
                b = session.Configuration.About(term, out about);
            }
            else
            {
                b = session.Configuration.About(term, out about, language);
            }

            if (b)
            {
                title = (about?.Find(Factory.TriplePSO(Keys.Semantics.Dc.title, term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
                description = (about?.Find(Factory.TriplePSO(Keys.Semantics.Dc.description, term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
                return true;
            }
            else
            {
                title = null;
                description = null;
                return false;
            }
        }

        public static string? GetTitle(this IActivity activity, string? language = null)
        {
            if (language == null)
            {
                return (activity.About.Find(Factory.TriplePSO(Keys.Semantics.Dc.title, activity.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
            else
            {
                return (activity.About.Find(Factory.TriplePSO(Keys.Semantics.Dc.title, activity.Term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
        }
        public static string? GetDescription(this IActivity activity, string? language = null)
        {
            if (language == null)
            {
                return (activity.About.Find(Factory.TriplePSO(Keys.Semantics.Dc.description, activity.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
            else
            {
                return (activity.About.Find(Factory.TriplePSO(Keys.Semantics.Dc.description, activity.Term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
        }

        public static string? GetTitle(this IAlgorithm algorithm, string? language = null)
        {
            if (language == null)
            {
                return (algorithm.About.Find(Factory.TriplePSO(Keys.Semantics.Dc.title, algorithm.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
            else
            {
                return (algorithm.About.Find(Factory.TriplePSO(Keys.Semantics.Dc.title, algorithm.Term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
        }
        public static string? GetDescription(this IAlgorithm algorithm, string? language = null)
        {
            if (language == null)
            {
                return (algorithm.About.Find(Factory.TriplePSO(Keys.Semantics.Dc.description, algorithm.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
            else
            {
                return (algorithm.About.Find(Factory.TriplePSO(Keys.Semantics.Dc.description, algorithm.Term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
        }
        public static string? GetVersion(this IAlgorithm algorithm)
        {
            return (algorithm.About.Find(Factory.TriplePSO(Keys.Semantics.Swo.version, algorithm.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
        }
    }
}