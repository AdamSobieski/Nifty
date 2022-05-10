using Nifty.Activities;
using Nifty.Algorithms;
using Nifty.Analytics;
using Nifty.Channels;
using Nifty.Common;
using Nifty.Configuration;
using Nifty.Dialogue;
using Nifty.Events;
using Nifty.Knowledge;
using Nifty.Knowledge.Reasoning;
using Nifty.Knowledge.Semantics;
using Nifty.Knowledge.Semantics.Ontology;
using Nifty.Logging;
using Nifty.Modelling.Users;
using Nifty.Sessions;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Net.Mime;
using System.Xml;

namespace Nifty.Activities
{
    public interface IActivityGeneratorStore : IHasReadOnlyGraph, /*IQueryable<IActivityGenerator>,*/ ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
    public interface IActivityGenerator : IHasReadOnlyGraph
    {
        // public IActivityPreconditions Preconditions { get; }
        // public IActivityEffects       Effects { get; }

        Task<IActivity> Generate(ISession session, CancellationToken cancellationToken);
    }
    public interface IActivity : IHasReadOnlyGraph, ISessionInitializable, ISessionDisposable, IDisposable
    {
        //public IActivityGenerator Generator { get; }

        //public IActivity Parent { get; }
        //public IReadOnlyList<IActivity> Children { get; }

        // public IActivityPreconditions Preconditions { get; }
        // public IActivityEffects       Effects { get; }

        public IActivityExecutionResult Execute(ISession session, IActivityExecutionContext context, CancellationToken cancellationToken);
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
    public interface IAlgorithm : IHasReadOnlyGraph, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable
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

        public long Ticks { get; }
        public TimeSpan Time { get; }
        public long SectionTicks { get; }
        public TimeSpan SectionTime { get; }

        public string Status { get; }
    }
}

namespace Nifty.Channels
{
    public interface IChannel { }

    public interface IChannelCollection { }
}

namespace Nifty.Common
{
    public interface IInitializable
    {
        public IDisposable Initialize();
    }

    public interface IOptimizable
    {
        public IDisposable Optimize(IEnumerable<ITerm> hints);
    }

    public interface INotifyChanged
    {
        public event EventHandler Changed;
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
        public IUriTerm Term { get; }
        public T DefaultValue { get; }
    }

    public interface IConfiguration : ISessionInitializable, ISessionOptimizable, ISessionDisposable, INotifyChanged
    {
        public bool About(IUriTerm setting, [NotNullWhen(true)] out ITerm? term, [NotNullWhen(true)] out IReadOnlyGraph? about);
        public bool About(IUriTerm setting, [NotNullWhen(true)] out ITerm? term, [NotNullWhen(true)] out IReadOnlyGraph? about, string language);
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
    public interface IEventSource : IHasReadOnlyGraph
    {
        public bool Subscribe(ITerm eventCategory, IEventHandler listener);
        public bool Unsubscribe(ITerm eventCategory, IEventHandler listener);
    }
    public interface IEventHandler
    {
        public Task Handle(IEventSource source, ITerm eventCategory, ITerm data, IReadOnlyGraph dataGraph);
    }

    public interface IReport : IHasReadOnlyGraph
    {
        public EventLevel Level { get; }
        public string Description { get; }
    }
}

namespace Nifty.Knowledge
{
    public interface IReadOnlyCompoundCollection : IEventSource, INotifyChanged
    {
        public IEnumerable<ITerm> Predicates { get { return this.Statements.Select(s => s.Predicate).Distinct(); } }

        public IQueryable<ICompound> Statements { get; }

        public int Count { get; }
        public bool IsConcrete { get; }
        public bool IsReadOnly { get; }
        public bool IsInferred { get; }
        public bool IsGraph { get; }

        public bool Contains(ICompound statement)
        {
            return Find(statement).Any();
        }

        public IEnumerable<ICompound> Find(ICompound statement);

        public IReadOnlyCompoundCollection Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map);

        public IEnumerable<IReadOnlyDictionary<IVariableTerm, ITerm>> Query(IReadOnlyCompoundCollection query);
        public IEnumerable<IReadOnlyCompoundCollection> Query2(IReadOnlyCompoundCollection query)
        {
            return Query(query).Select(map => query.Replace(map));
        }
    }
    public interface ICompoundCollection : IReadOnlyCompoundCollection
    {
        public bool Add(ICompound statement);
        public bool Add(IReadOnlyCompoundCollection statements);

        public bool Remove(ICompound statement);
        public bool Remove(IReadOnlyCompoundCollection statements);
    }

    public enum TermType
    {
        Any,
        Blank,
        Uri,
        Literal,
        Variable,
        Compound,
        Triple,
        CompoundCollection,
        Graph
    }
    public interface ITerm
    {
        public TermType TermType { get; }

        public bool IsConcrete { get; }

        public object Visit(ITermVisitor visitor);

        public bool Matches(ITerm other);

        public ITerm Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map);

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
        public IUriTerm? Datatype { get; }
    }
    public interface IVariableTerm : ITerm
    {
        public string Name { get; }
    }

    public interface ICompound : ITerm
    {
        public ITerm Predicate { get; }

        public int Count { get; }
        public ITerm this[int index] { get; }

        public bool Matches(ICompound other);
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
        //public object Visit(ICompound compound);
        //public object Visit(ITriple triple);
        //public object Visit(IReadOnlyCompoundCollection collection);
        //public object Visit(IReadOnlyGraph graph);
    }

    public interface IKnowledgebase : ICompoundCollection, ISessionInitializable, ISessionOptimizable, IEventHandler, ISessionDisposable { }
}

namespace Nifty.Knowledge.Reasoning
{
    public interface IReasoner : IHasReadOnlyGraph
    {
        public IConfiguration Configuration { get; }

        Task<IReasoner> BindRules(IReadOnlyCompoundCollection rules);

        Task<IInferredReadOnlyCompoundCollection> Bind(IReadOnlyCompoundCollection collection);
    }

    public interface IInferredReadOnlyCompoundCollection : IReadOnlyCompoundCollection
    {
        public IReasoner Reasoner { get; }
        public IReadOnlyCompoundCollection Base { get; }

        public IEnumerable<IDerivation> GetDerivations(ICompound statement);
    }

    public interface IDerivation
    {

    }
}

namespace Nifty.Knowledge.Semantics
{
    public interface ITriple : ICompound
    {
        public ITerm Subject { get; }
        public ITerm Object { get; }

        public bool Matches(ITriple other);
    }

    public interface IReadOnlyGraph : IReadOnlyCompoundCollection, IHasReadOnlyOntology, IEventSource, INotifyChanged
    {
        //public IEnumerable<ITerm> Predicates { get { return this.Statements.Select(t => t.Predicate).Distinct(); } }
        public IEnumerable<ITerm> Subjects { get { return this.Statements.Select(t => t.Subject).Distinct(); } }
        public IEnumerable<ITerm> Objects { get { return this.Statements.Select(t => t.Object).Distinct(); } }

        public new IQueryable<ITriple> Statements { get; }

        //public int Count { get; }
        //public bool IsConcrete { get; }
        //public bool IsReadOnly { get; }
        public bool IsValid { get; }

        public bool Contains(ITriple triple)
        {
            return Find(triple).Any();
        }
        public bool Contains(ITriple triple, [NotNullWhen(true)] out ITerm? reified);

        public IEnumerable<ITriple> Find(ITriple triple)
        {
            return this.Statements.Where(t => triple.Predicate.Matches(t.Predicate) && triple.Subject.Matches(t.Subject) && triple.Object.Matches(t.Object));
        }

        public new IReadOnlyGraph Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map);

        public IEnumerable<IReadOnlyDictionary<IVariableTerm, ITerm>> Query(IReadOnlyGraph query);
        public IEnumerable<IReadOnlyGraph> Query2(IReadOnlyGraph query)
        {
            return Query(query).Select(result => query.Replace(result));
        }
    }
    public interface IGraph : ICompoundCollection, IReadOnlyGraph
    {
        public bool Add(ITriple triple);
        public bool Add(ITriple triple, [NotNullWhen(true)] out ITerm? reified);
        public bool Remove(ITriple triple);

        public bool Add(IReadOnlyGraph graph);
        public bool Remove(IReadOnlyGraph graph);

        public new IGraph Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map);
    }

    public interface IHasReadOnlyGraph : IHasTerm
    {
        public IReadOnlyGraph About { get; }
    }
    public interface IHasGraph : IHasReadOnlyGraph
    {
        public new IGraph About { get; }
    }
}

namespace Nifty.Knowledge.Semantics.Reasoning
{
    public interface IGraphReasoner : IReasoner
    {
        Task<IGraphReasoner> BindRules(IReadOnlyGraph rules);

        Task<IInferredReadOnlyGraph> Bind(IReadOnlyGraph graph);
    }

    public interface IInferredReadOnlyGraph : IReadOnlyGraph, IInferredReadOnlyCompoundCollection
    {
        public new IGraphReasoner Reasoner { get; }
        public new IReadOnlyGraph Base { get; }

        public IEnumerable<IDerivation> GetDerivations(ITriple statement);
    }
}

namespace Nifty.Knowledge.Semantics.Ontology
{
    public interface IReadOnlyOntology : IReadOnlyGraph
    {
        public Task<bool> Validate(IReadOnlyGraph graph);
        public Task<bool> Validate(IReadOnlyGraph graph, IObserver<IReport> observer);
    }
    public interface IOntology : IReadOnlyOntology, IGraph { }

    public interface IHasReadOnlyOntology
    {
        public IReadOnlyOntology Ontology { get; }
    }
    public interface IHasOntology : IHasReadOnlyOntology
    {
        public new IOntology Ontology { get; }
    }
}

namespace Nifty.Knowledge.Semantics.Serialization
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
        public void Serialize(IGraph graph);
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

namespace Nifty.Modelling.Domains
{
    public interface IDomainModel : IHasReadOnlyGraph, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
}

namespace Nifty.Modelling.Users
{
    public interface IUserModel : IHasReadOnlyGraph, ISessionInitializable, IEventHandler, ISessionDisposable, INotifyChanged { }
}

namespace Nifty.Sessions
{
    public interface ISessionInitializable
    {
        public IDisposable Initialize(ISession session);
    }
    public interface ISessionOptimizable
    {
        public IDisposable Optimize(ISession session, IEnumerable<ITerm> hints);
    }
    public interface ISessionDisposable
    {
        public void Dispose(ISession session);
    }

    public interface ISession : IHasReadOnlyGraph, IInitializable, IOptimizable, IEventSource, IEventHandler, IDisposable, IAsyncEnumerable<IActivityGenerator>
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

            DialogueSystem.Subscribe(Keys.Events.All, this);

            return value;
        }

        IDisposable IOptimizable.Optimize(IEnumerable<ITerm> hints)
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

            DialogueSystem.Unsubscribe(Keys.Events.All, this);

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
                public static readonly IUriTerm hasEventCategory = Factory.Uri("http://www.event-ontology.org/hasEventCategory");
            }
        }

        public static class Settings
        {
            public static readonly ISetting<bool> ShouldPerformAnalytics = Factory.Setting("http://www.settings.org/analytics/ShouldPerformAnalytics", false);
            public static readonly ISetting<bool> ShouldPerformConfigurationAnalytics = Factory.Setting("http://www.settings.org/analytics/ShouldPerformConfigurationAnalytics", false);
        }

        public static class Events
        {
            public static readonly ITerm All = Factory.Uri("http://www.w3.org/2002/07/owl#Thing");

            public static readonly ITerm InitializedSession = Factory.Uri("http://www.events.org/events/InitializedSession");
            public static readonly ITerm ObtainedGenerator = Factory.Uri("http://www.events.org/events/ObtainedGenerator");
            public static readonly ITerm GeneratingActivity = Factory.Uri("http://www.events.org/events/GeneratingActivity");
            public static readonly ITerm GeneratedActivity = Factory.Uri("http://www.events.org/events/GeneratedActivity");
            public static readonly ITerm ExecutingActivity = Factory.Uri("http://www.events.org/events/ExecutingActivity");
            public static readonly ITerm ExecutedActivity = Factory.Uri("http://www.events.org/events/ExecutedActivity");
            public static readonly ITerm DisposingSession = Factory.Uri("http://www.events.org/events/DisposingSession");
        }

        public static class Data
        {
            public static readonly ITerm Algorithm = Factory.Uri("urn:eventdata:Algorithm");
            public static readonly ITerm Generator = Factory.Uri("urn:eventdata:Generator");
            public static readonly ITerm Activity = Factory.Uri("urn:eventdata:Activity");
            public static readonly ITerm User = Factory.Uri("urn:eventdata:User");
            public static readonly ITerm Result = Factory.Uri("urn:eventdata:Result");
        }
    }

    public static partial class Factory
    {
        internal sealed class AnyTerm : IAnyTerm
        {
            public TermType TermType => TermType.Any;

            public bool IsConcrete => false;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                return true;
            }

            public ITerm Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map)
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

            public bool IsConcrete => true;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                throw new NotImplementedException();
            }

            public ITerm Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map)
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

            public bool IsConcrete => true;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                throw new NotImplementedException();
            }

            public ITerm Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map)
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
                m_datatype = datatype;
            }

            private readonly string m_value;
            private readonly string? m_language;
            private readonly IUriTerm? m_datatype;

            public string Value => m_value;

            public string? Language => m_language;

            public IUriTerm? Datatype => m_datatype;

            public TermType TermType => TermType.Literal;

            public bool IsConcrete => true;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                throw new NotImplementedException();
            }

            public ITerm Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map)
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

            public bool IsConcrete => false;

            public sealed override bool Equals(object? obj)
            {
                return base.Equals(obj);
            }

            public bool Matches(ITerm other)
            {
                throw new NotImplementedException();
            }

            public ITerm Replace(IReadOnlyDictionary<IVariableTerm, ITerm> map)
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

        public static ICompound Compound(ITerm predicate, params ITerm[] arguments)
        {
            throw new NotImplementedException();
        }
        public static ITriple Triple(ITerm predicate, ITerm subject, ITerm @object)
        {
            throw new NotImplementedException();
        }

        public static IGraph Graph(IReadOnlyOntology ontology)
        {
            throw new NotImplementedException();
        }
        public static IGraph Graph(IEnumerable<ITriple> statements, IReadOnlyOntology ontology)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyGraph ReadOnlyGraph(IEnumerable<ITriple> statements)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyGraph ReadOnlyGraph(IEnumerable<ITriple> statements, IReadOnlyOntology ontology)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyGraph ParseReadOnlyGraph(ContentType type, Stream stream)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyGraph ParseReadOnlyGraph(ContentType type, Stream stream, IReadOnlyOntology ontology)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyOntology EmptyOntology
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public static IOntology Ontology()
        {
            throw new NotImplementedException();
        }
        public static IOntology Ontology(IReadOnlyOntology ontology)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyOntology ParseReadOnlyOntology(ContentType type, Stream stream)
        {
            throw new NotImplementedException();
        }
        public static IReadOnlyOntology ParseReadOnlyOntology(ContentType type, Stream stream, IReadOnlyOntology ontology)
        {
            throw new NotImplementedException();
        }

        public static IReadOnlyCompoundCollection ReadOnlyKnowledgeCollection(IEnumerable<ICompound> statements)
        {
            throw new NotImplementedException();
        }
    }

    public static partial class Extensions
    {
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, bool value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, sbyte value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, byte value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, short value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, ushort value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, int value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, uint value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, long value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, ulong value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, float value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, double value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        public static bool Add(this IGraph graph, ITerm predicate, ITerm subject, string value)
        {
            return graph.Add(Factory.Triple(predicate, subject, Factory.Literal(value)));
        }
        //...

        public static IEnumerable<ITerm> GetClasses(this IHasReadOnlyGraph thing)
        {
            return thing.About.Find(Factory.Triple(Keys.Semantics.Rdf.type, thing.Term, Factory.Any())).Select(t => t.Object).Distinct();
        }
        public static bool HasClass(this IHasReadOnlyGraph thing, ITerm type)
        {
            return thing.About.Contains(Factory.Triple(Keys.Semantics.Rdf.type, thing.Term, type));
        }
        public static IEnumerable<ITerm> GetProperties(this IHasReadOnlyGraph thing)
        {
            return thing.About.Find(Factory.Triple(Factory.Any(), thing.Term, Factory.Any())).Select(t => t.Predicate).Distinct();
        }
        public static bool HasProperty(this IHasReadOnlyGraph thing, ITerm predicate)
        {
            return thing.About.Contains(Factory.Triple(predicate, thing.Term, Factory.Any()));
        }
        public static IEnumerable<ITerm> GetEvents(this IEventSource thing)
        {
            return thing.About.Find(Factory.Triple(Keys.Semantics.Eo.hasEventCategory, thing.Term, Factory.Any())).Select(t => t.Object).Distinct();
        }
        public static bool HasEvent(this IEventSource thing, ITerm eventCategory)
        {
            return thing.About.Contains(Factory.Triple(Keys.Semantics.Eo.hasEventCategory, thing.Term, eventCategory));
        }

        public static T Setting<T>(this ISession session, ISetting<T> setting)
        {
            return session.Configuration.TryGetSetting(setting.Term, out IConvertible? value) ? (T)value.ToType(typeof(T), System.Globalization.CultureInfo.CurrentCulture) : setting.DefaultValue;
        }
        public static bool About<T>(this ISession session, ISetting<T> setting, out string? title, out string? description, string? language = null)
        {
            if (session.Configuration.About(setting.Term, out ITerm? term, out IReadOnlyGraph? about))
            {
                if (language == null)
                {
                    title = (about.Find(Factory.Triple(Keys.Semantics.Dc.title, term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
                    description = (about.Find(Factory.Triple(Keys.Semantics.Dc.description, term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
                }
                else
                {
                    title = (about.Find(Factory.Triple(Keys.Semantics.Dc.title, term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
                    description = (about.Find(Factory.Triple(Keys.Semantics.Dc.description, term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
                }
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
                return (activity.About.Find(Factory.Triple(Keys.Semantics.Dc.title, activity.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
            else
            {
                return (activity.About.Find(Factory.Triple(Keys.Semantics.Dc.title, activity.Term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
        }
        public static string? GetDescription(this IActivity activity, string? language = null)
        {
            if (language == null)
            {
                return (activity.About.Find(Factory.Triple(Keys.Semantics.Dc.description, activity.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
            else
            {
                return (activity.About.Find(Factory.Triple(Keys.Semantics.Dc.description, activity.Term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
        }

        public static string? GetTitle(this IAlgorithm algorithm, string? language = null)
        {
            if (language == null)
            {
                return (algorithm.About.Find(Factory.Triple(Keys.Semantics.Dc.title, algorithm.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
            else
            {
                return (algorithm.About.Find(Factory.Triple(Keys.Semantics.Dc.title, algorithm.Term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
        }
        public static string? GetDescription(this IAlgorithm algorithm, string? language = null)
        {
            if (language == null)
            {
                return (algorithm.About.Find(Factory.Triple(Keys.Semantics.Dc.description, algorithm.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
            else
            {
                return (algorithm.About.Find(Factory.Triple(Keys.Semantics.Dc.description, algorithm.Term, Factory.Any(language))).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
            }
        }
        public static string? GetVersion(this IAlgorithm algorithm)
        {
            return (algorithm.About.Find(Factory.Triple(Keys.Semantics.Swo.version, algorithm.Term, Factory.Any())).SingleOrDefault()?.Object as ILiteralTerm)?.Value;
        }
    }
}