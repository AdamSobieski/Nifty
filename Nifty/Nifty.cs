using Nifty.Activities;
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
using Nifty.Knowledge.Reasoning.Derivation;
using Nifty.Knowledge.Schema;
using Nifty.Knowledge.Updating;
using Nifty.Logging;
using Nifty.Modelling.Users;
using Nifty.Sessions;
using System.Diagnostics.CodeAnalysis;
using System.Xml;

namespace Nifty.Activities
{
    public interface IActivityGeneratorStore : IHasReadOnlyFormulaCollection, /*IQueryable<IActivityGenerator>,*/ ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
    public interface IActivityGenerator : IHasReadOnlyFormulaCollection
    {
        // public IActivityPreconditions Preconditions { get; }
        // public IActivityEffects       Effects { get; }

        public Task<IActivity> Generate(ISession session, CancellationToken cancellationToken);
    }
    public interface IActivity : IHasReadOnlyFormulaCollection, ISessionInitializable, ISessionDisposable, IDisposable
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
    public interface IAlgorithm : IHasReadOnlyFormulaCollection, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable
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
    public interface IAction
    {
        public IAskQuery Preconditions { get; }
        public IUpdate Effects { get; }
    }

    // to do: explore abstract actions, variables, substituting variables for terms, generating concrete actions, etc.
    public interface IActionGenerator : IHasVariables, ISubstitute<IAction> { }
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
        public event EventHandler Changed;
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
        public IUriTerm Term { get; }
        public T DefaultValue { get; }
    }

    public interface IConfiguration : ISessionInitializable, ISessionOptimizable, ISessionDisposable, INotifyChanged
    {
        public bool About(IUriTerm setting, [NotNullWhen(true)] out IReadOnlyFormulaCollection? about);
        public bool About(IUriTerm setting, [NotNullWhen(true)] out IReadOnlyFormulaCollection? about, string language);
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
    public interface IEventSource : IHasReadOnlyFormulaCollection
    {
        public IDisposable Subscribe(IUriTerm eventType, IEventHandler listener);
        public IDisposable Subscribe(IAskQuery query, IEventHandler listener);
    }
    public interface IEventHandler // : IHasReadOnlyFormulaCollection
    {
        public Task Handle(IEventSource source, ITerm eventInstance, IReadOnlyFormulaCollection aboutEventInstance, ITerm eventData, IReadOnlyFormulaCollection aboutEventData);
    }
}

namespace Nifty.Knowledge
{
    public interface IReadOnlyFormulaCollection : IQueryable<IFormula>, IHasVariables, ISubstitute<IReadOnlyFormulaCollection>, IHasReadOnlySchema, IEventSource, INotifyChanged
    {
        public IQueryable<ITerm> Predicates { get; }

        public bool IsGround { get; }
        public bool IsReadOnly { get; }
        public bool IsInferred { get; }
        public bool IsValid { get; }
        public bool IsGraph { get; }

        public bool Contains(IFormula formula);
        public bool Contains(IFormula formula, [NotNullWhen(true)] out IReadOnlyFormulaCollection? about);

        public IEnumerable<IDerivation> Derivations(IFormula formula);

        public IQueryable<IFormula> Find(IFormula formulas);

        public int Count();
        public int Count(IFormula formula);
        public int Count(ISelectQuery query);
        public int Count(IConstructQuery query);

        public ISimpleUpdate DifferenceFrom(IReadOnlyFormulaCollection other);

        // public IReadOnlyFormulaCollection Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map);

        public bool Query(IAskQuery query);
        public IEnumerable<IReadOnlyDictionary<IVariableTerm, ITerm>> Query(ISelectQuery query);
        public IEnumerable<IReadOnlyFormulaCollection> Query(IConstructQuery query);
        public IReadOnlyFormulaCollection Query(IDescribeQuery query);

        public IReadOnlyFormulaCollection Clone();
        public IReadOnlyFormulaCollection Clone(IReadOnlyFormulaCollection removals, IReadOnlyFormulaCollection additions);
    }
    public interface IFormulaCollection : IReadOnlyFormulaCollection
    {
        public bool Add(IFormula formula);
        public bool Add(IReadOnlyFormulaCollection formulas);

        public bool Remove(IFormula formula);
        public bool Remove(IReadOnlyFormulaCollection formulas);
    }

    public enum TermType
    {
        Any,
        Blank,
        Uri,
        Literal,
        Variable,
        Formula,
        // QuotedFormula or a unary predicate 'quote' with same semantics; see also: RDF-star and SPARQL-star (https://www.w3.org/2021/12/rdf-star.html)
        // FormulaCollection,
    }
    public interface ITerm : IHasVariables, ISubstitute<ITerm>
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

        // public ITerm Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map);

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

        public bool IsValid(IReadOnlySchema schema);
    }

    public interface IHasVariables
    {
        public IReadOnlyList<IVariableTerm> Variables { get; }
    }
    public interface ISubstitute<out T> : IHasVariables
    {
        public T Substitute(IReadOnlyDictionary<IVariableTerm, ITerm> map);
    }

    public interface IHasTerm
    {
        public ITerm Term { get; }
    }
    public interface IHasReadOnlyFormulaCollection : IHasTerm
    {
        public IReadOnlyFormulaCollection About { get; }
    }
    public interface IHasFormulaCollection : IHasReadOnlyFormulaCollection
    {
        public new IFormulaCollection About { get; }
    }

    public interface ITermVisitor
    {
        public object Visit(IAnyTerm term);
        public object Visit(IUriTerm term);
        public object Visit(IBlankTerm term);
        public object Visit(ILiteralTerm term);
        public object Visit(IVariableTerm term);
        //public object Visit(IFormula formula);
        //public object Visit(IReadOnlyFormulaCollection collection);
    }

    public interface IKnowledgebase : IFormulaCollection, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable { }
}

namespace Nifty.Knowledge.Probabilistic
{

}

namespace Nifty.Knowledge.Querying
{
    // see also: https://www.w3.org/TR/sparql11-query/
    // see also: Jena ARQ

    // https://www.w3.org/TR/sparql11-query/#QueryForms
    public enum QueryType
    {
        Select,
        Construct,
        Ask,
        Describe
    }

    public interface IQuery
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
    public interface IReasoner : IHasReadOnlyFormulaCollection
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
}

namespace Nifty.Knowledge.Reasoning.Derivation
{
    public interface IDerivation
    {

    }
}

namespace Nifty.Knowledge.Schema
{
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

    public interface IUpdate
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
        public IDisposable Subscribe(IObserver<TAction, TObservation, TReward> observer);
    }

    public interface IObserver<in TAction, TObservation, TReward>
    {
        public void OnCompleted();
        public void OnError(Exception error);
        public (TObservation observation, TReward reward, bool done) OnNext(TAction action);
    }

    public interface IAsyncEnumerator<out TAction, in TObservation, in TReward> : IAsyncDisposable
    {
        public ValueTask<bool> MoveNextAsync(TObservation observation, TReward reward);
        public TAction Current { get; }
    }

    public interface IAsyncEnumerable<out TAction, in TObservation, in TReward>
    {
        public IAsyncEnumerator<TAction, TObservation, TReward> GetAsyncEnumerator(CancellationToken cancellationToken = default);
    }

    public interface IAsyncObservable<out TAction, TObservation, TReward>
    {
        public IDisposable Subscribe(IAsyncObserver<TAction, TObservation, TReward> observer);
    }

    public interface IAsyncObserver<in TAction, TObservation, TReward>
    {
        public Task OnCompleted();
        public Task OnError(Exception error);
        public Task<(TObservation observation, TReward reward, bool done)> OnNext(TAction value);
    }
}

namespace Nifty.Messaging
{

}

namespace Nifty.Modelling.Domains
{
    public interface IDomainModel : IHasReadOnlyFormulaCollection, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
}

namespace Nifty.Modelling.Users
{
    public interface IUserModel : IHasReadOnlyFormulaCollection, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
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

    public interface ISession : IHasReadOnlyFormulaCollection, IInitializable, IOptimizable, IEventSource, IEventHandler, IDisposable, IAsyncEnumerable<IActivityGenerator>
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

        public static IAnyTerm Any()
        {
            throw new NotImplementedException();
        }
        public static IAnyTerm Any(string language)
        {
            throw new NotImplementedException();
        }
        public static IUriTerm Uri(string uri)
        {
            throw new NotImplementedException();
        }
        public static IBlankTerm Blank()
        {
            throw new NotImplementedException();
        }
        public static IBlankTerm Blank(string id)
        {
            throw new NotImplementedException();
        }
        public static IVariableTerm Variable(string name)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(bool value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(sbyte value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(byte value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(short value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(ushort value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(int value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(uint value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(long value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(ulong value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(float value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(double value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(string value)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(string value, string language)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(string value, string language, IUriTerm datatypeUri)
        {
            throw new NotImplementedException();
        }
        public static ILiteralTerm Literal(string value, IUriTerm datatypeUri)
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
        public static ISchema FormulaCollectionSchema(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
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
    }

    public static partial class Extensions
    {
        //...
    }
}