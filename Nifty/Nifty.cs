using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nifty.Common;
using Nifty.Dialogs;
using Nifty.Extensibility;
using Nifty.Extensibility.Activities;
using Nifty.Extensibility.Algorithms;
using Nifty.Hosting;
using Nifty.Knowledge;
using Nifty.Knowledge.Building;
using Nifty.Knowledge.Querying;
using Nifty.Knowledge.Reasoning;
using Nifty.Knowledge.Schema;
using Nifty.Knowledge.Updating;
using Nifty.Messaging;
using Nifty.Messaging.Events;
using Nifty.Modelling.Domains;
using Nifty.Modelling.Pedagogical;
using Nifty.Modelling.Users;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.Loader;

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

    public static class Disposable
    {
        class CombinedDisposable : IDisposable
        {
            public CombinedDisposable(IEnumerable<IDisposable> disposables)
            {
                m_disposed = false;
                m_disposables = disposables;
            }
            bool m_disposed;
            readonly IEnumerable<IDisposable> m_disposables;

            public void Dispose()
            {
                if (!m_disposed)
                {
                    m_disposed = true;

                    List<Exception> errors = new List<Exception>();

                    foreach (var disposable in m_disposables)
                    {
                        try
                        {
                            disposable.Dispose();
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
        static readonly CombinedDisposable s_empty = new CombinedDisposable(Enumerable.Empty<IDisposable>());

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
        public static IDisposable All(IEnumerable<IDisposable> scopes)
        {
            return new CombinedDisposable(scopes);
        }
    }
}

namespace Nifty.Dialogs
{
    public interface IDialogSystem : IBot, IServiceProviderInitializable, IMessageHandler, IMessageSource, IEventHandler, IEventSource, IServiceProviderDisposable
    {
        // to do: explore more granular interfaces between dialog systems and items, exercises, and activities
        public void EnterScope(IHasMetadata scope);
        public void ExitScope(IHasMetadata scope);
    }

    // see also: https://docs.microsoft.com/en-us/azure/bot-service/bot-activity-handler-concept?view=azure-bot-service-4.0&tabs=csharp
    // see also: https://docs.microsoft.com/en-us/azure/bot-service/bot-builder-concept-state?view=azure-bot-service-4.0
    // see also: https://github.com/microsoft/BotBuilder-Samples/blob/main/samples/csharp_dotnetcore/19.custom-dialogs/Bots/DialogBot.cs
    // see also: https://github.com/microsoft/BotBuilder-Samples/blob/main/samples/csharp_dotnetcore/45.state-management/Bots/StateManagementBot.cs

    public class DialogBot<T> : ActivityHandler where T : Dialog
    {
        protected readonly BotState m_conversationState;
        protected readonly Dialog m_dialog;
        protected readonly BotState m_userState;

        public DialogBot(ConversationState conversationState, UserState userState, T dialog)
        {
            m_conversationState = conversationState;
            m_userState = userState;
            m_dialog = dialog;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save any state changes that might have occurred during the turn.
            await m_conversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await m_userState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            // Run the Dialog with the new message Activity.
            await m_dialog.RunAsync(turnContext, m_conversationState.CreateProperty<DialogState>(nameof(DialogState)), cancellationToken);
        }
    }
}

namespace Nifty.Extensibility
{
    public interface IHostBuildingComponent
    {
        public IHostBuilder Build(IHostBuilder builder);
    }

    // considering use of Nifty metadata for describing add-ons, plug-ins, and extensions
    // a "component connecting algorithm" should be able to utilize components' metadata to automatically interconnect components, connecting message sources and message handlers
    public interface IMessagingComponent : IHasMetadata, IServiceProviderInitializable, IMessageSource, IMessageHandler, IEventSource, IEventHandler, IServiceProviderDisposable { }

    public class ComponentMetadata { }
}

namespace Nifty.Extensibility.Activities
{
    // as .NET can dynamically load and unload assemblies, educational items, exercises, and activities can be implemented as components in digitally-signed .NET assemblies
    // this implies that algorithms for interconnecting components based on their metadata should be devised for, beyond system initialization and shutdown scenarios, loading and unloading components at runtime

    public interface IItem : IMessagingComponent
    {
        // use cases:
        // 1. mathematics exercises
        // 2. interactive stories (story-based items, digital gamebooks, interactive films, serious games, etc.)
        //    a. will explore creating and providing to IItem abstracted rendering or streaming contexts so that items can generate text, imagery, video, and 3D graphics over video-calling channels, e.g., Skype, Zoom, WebRTC, et al.
        //       i. see also: https://github.com/3DStreamingToolkit/3DStreamingToolkit , https://3dstreamingtoolkit.github.io/docs-3dstk/
        //       ii. see also: https://docs.unity3d.com/Packages/com.unity.webrtc@2.4/manual/index.html
        // 3. software training exercises
        // 4. other
        //
        // considering using: Silk.NET which includes OpenGL, OpenCL, OpenAL, OpenXR, GLFW, SDL, Vulkan, Assimp, and DirectX (https://github.com/dotnet/Silk.NET)
        //
        // ideally, server-side applications, after initialization, can provide graphics-related interfaces, pointers, and data to dynamically-loaded IItem's
        // so that the IItem's can render content, text, imagery, video, and 3D graphics, in a manner independent of the video-calling channel, e.g., Skype, Zoom, WebRTC
        // otherwise, it would only be WebRTC (which the Bot Framework doesn't yet support? see also: https://github.com/microsoft/botframework-sdk/issues/6516)
        //
        // with graphics and video-calling channels, possibilities include:
        // 1. rendering exercise-related content, e.g., interactive 3D mathematics visualizations and diagrams
        // 2. routing/relaying existing video stream resources through video-call channels
        //    a. then presenting interactions or menus in the WebRTC content or in accompanying Web content
        // 3. rendering interactive educational 3D content (see also: https://www.youtube.com/watch?v=wJyUtbn0O5Y , https://www.youtube.com/watch?v=39HTpUG1MwQ) where users could gesture to pan, rotate, and zoom cameras, select objects, etc.
        // 4. rendering educational game content (see also: https://en.wikipedia.org/wiki/Cloud_gaming)
        // 5. multimodal dialog systems (see also: https://www.youtube.com/watch?v=FyKYBei9D08)
    }

    public interface IItemStore : Nifty.Knowledge.Querying.IQueryable, IServiceProviderInitializable, IServiceProviderDisposable
    {
        // the stream is a .NET assembly which contains an IItem
        public Stream Retrieve(IUri uri);
        public Stream Retrieve(AssemblyName uri);
    }
}

namespace Nifty.Extensibility.Algorithms
{
    public interface IAlgorithm : IMessagingComponent
    {
        public IAsyncEnumerator<IItem> GetAsyncEnumerator(IServiceProvider services, CancellationToken cancellationToken);
    }
}

namespace Nifty.Hosting
{
    public interface IServiceProviderInitializable
    {
        public IDisposable Initialize(IServiceProvider services);
    }
    public interface IServiceProviderDisposable
    {
        public void Dispose(IServiceProvider services);
    }

    public interface ISession : IHasMetadata, IInitializable, IMessageSource, IMessageHandler, IEventSource, IEventHandler, IDisposable, IAsyncEnumerable<IItem>
    {
        protected CompositionHost? CompositionHost { get; set; }

        [ImportMany]
        protected IEnumerable<Lazy<IMessagingComponent, ComponentMetadata>> MessagingComponents { get; set; }

        protected IEnumerable<IMessagingComponent> GetMessagingComponents(IAskQuery query)
        {
            return MessagingComponents.Select(n => n.Value).Where(n => n.About.Query(query));
        }



        public IHost Host { get; }
        public IServiceProvider Services { get; }
        public IConfiguration Configuration { get; }
        public ILogger Log { get; }



        public IDialogSystem DialogueSystem { get; }
        public IKnowledgebase Knowledgebase { get; }
        public IUserModel User { get; }
        public IDomainModel Domain { get; }
        public IPedagogicalModel Pedagogical { get; }
        public IItemStore Store { get; }
        public IAlgorithm Algorithm { get; }



        IDisposable IInitializable.Initialize()
        {
            Compose();

            var disposables = new List<IDisposable>(new IDisposable[] {
                Knowledgebase.Initialize(this.Services),
                Domain.Initialize(this.Services),
                User.Initialize(this.Services),
                Pedagogical.Initialize(this.Services),
                Store.Initialize(this.Services),
                Algorithm.Initialize(this.Services),
                DialogueSystem.Initialize(this.Services)
            });

            foreach (var component in MessagingComponents)
            {
                disposables.Add(component.Value.Initialize(this.Services));
            }

            return Disposable.All(disposables);
        }

        private void Compose()
        {
            string location = Assembly.GetEntryAssembly()?.Location ?? throw new Exception();
            string path = Path.GetDirectoryName(location) ?? throw new Exception();
            path = Path.Combine(path, "Plugins");

            var dlls = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories).Select(AssemblyLoadContext.Default.LoadFromAssemblyPath);

            var configuration = new ContainerConfiguration().WithAssemblies(dlls);

            CompositionHost = configuration.CreateContainer();

            MessagingComponents = CompositionHost.GetExports<Lazy<IMessagingComponent, ComponentMetadata>>();
        }

        public Task SaveStateInBackground(CancellationToken cancellationToken);

        void IDisposable.Dispose()
        {
            GC.SuppressFinalize(this);

            Algorithm.Dispose(this.Services);
            Pedagogical.Dispose(this.Services);
            User.Dispose(this.Services);
            Domain.Dispose(this.Services);
            Store.Dispose(this.Services);
            DialogueSystem.Dispose(this.Services);
            Knowledgebase.Dispose(this.Services);

            foreach (var component in MessagingComponents)
            {
                component.Value.Dispose(this.Services);
            }

            CompositionHost?.Dispose();
            CompositionHost = null;

            GC.ReRegisterForFinalize(this);
        }

        IAsyncEnumerator<IItem> IAsyncEnumerable<IItem>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return Algorithm.GetAsyncEnumerator(this.Services, cancellationToken);
        }
    }
}

namespace Nifty.Knowledge
{
    public interface IFormulaCollection : IQueryExpression, Querying.IQueryable, IHasMetadata, IHasSchema, IEnumerable<IFormula>
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.BasicPattern;

        public bool IsReadOnly { get; }
        public bool IsGraph { get; }
        public bool IsValid { get; }
        public bool IsGround { get; }

        public IBasicUpdate DifferenceFrom(IFormulaCollection other);

        public bool Add(IFormula formula);
        public bool Add(IFormulaCollection formulas);

        public bool Remove(IFormula formula);
        public bool Remove(IFormulaCollection formulas);

        public new IFormulaCollection Clone(bool? isReadOnly = null);
        public IFormulaCollection Clone(IFormulaCollection removals, IFormulaCollection additions, bool? isReadOnly = null);
    }

    // to do: queryable collections, or stores, which can include multiple named formula collections, etc.
    //public interface IFormulaStore : Querying.IQueryable, IHasMetadata, IHasSchema, IEnumerable<IFormulaCollection>
    //{
    //    public IFormulaCollection this[IConstant id] { get; }
    //}

    public enum TermType
    {
        Variable,
        Box,
        Blank,
        Uri,
        Formula
    }
    public interface ITerm
    {
        public TermType TermType { get; }
        public void Visit(ITermVisitor visitor);
        public ITerm Transform(ITermTransformer transformer);

        public bool Matches(ITerm other);
        //public string? ToString(XmlNamespaceManager xmlns, bool quoting);
    }

    public interface IVariable : ITerm
    {
        public string Name { get; }
    }

    public interface IConstant : ITerm
    {
        public object Value { get; }
    }
    public interface IBlank : IConstant
    {
        public new string Value { get; }
    }
    public interface IUri : IConstant
    {
        // public new Uri Value { get; } ?
        public new string Value { get; }
    }
    public interface IBox : IConstant { }

    public interface IFormula : ITerm
    {
        public ITerm Predicate { get; }

        // or is this an extension method?
        // public bool IsGround { get; }

        public int Count { get; }
        public ITerm this[int index] { get; }
    }
    public interface ILambdaFormula : IFormula { }


    public interface IHasIdentifier
    {
        public IConstant Id { get; }
    }
    public interface IHasMetadata : IHasIdentifier
    {
        public IFormulaCollection About { get; }
    }


    public interface ITermVisitor
    {
        public void Visit(IVariable term);
        public void Visit(IBox term);
        public void Visit(IBlank term);
        public void Visit(IUri term);
        public void Visit(IFormula formula);
    }
    public interface ITermTransformer
    {
        public ITerm Visit(IVariable term);
        public ITerm Visit(IBox term);
        public ITerm Visit(IBlank term);
        public ITerm Visit(IUri term);
        public ITerm Visit(IFormula formula);
    }


    public interface IKnowledgebase : IFormulaCollection, IMessagingComponent { }
}

namespace Nifty.Knowledge.Building
{
    public interface IFormulaCollectionBuilder : IFormulaCollection
    {
        public bool IsBuilt { get; }

        public new IFormulaCollection About { get; set; }

        // public new ISchema Schema { get; set; }

        public IFormulaCollection Build(bool isReadOnly = true); // perhaps other parameters, e.g., bool isSelfSchema = false
    }

    public interface ISchemaBuilder : IFormulaCollectionBuilder
    {
        public new ISchema Build(bool isReadOnly = true);
    }
}

namespace Nifty.Knowledge.Querying
{
    // "Fluent N-ary SPARQL"
    // version 0.2.5
    // 
    // the expressiveness for querying n-ary formula collections with Nifty should be comparable with or exceed that of SPARQL for triple collections
    // see also: https://www.w3.org/2001/sw/DataAccess/rq23/examples.html , https://www.w3.org/2001/sw/DataAccess/rq23/rq24-algebra.html
    //
    // to do: https://www.w3.org/TR/sparql11-query/#subqueries
    //
    // example syntax:
    //
    // IFormulaCollection formulas = ...;
    //
    // IAskQuery askQuery = Factory.Query().Where(...).Ask();
    // bool result = formulas.Query(askQuery);
    //
    // ISelectQuery selectQuery = Factory.Query().Where(...).Select(...);
    // foreach(var result in formulas.Query(selectQuery))
    // {
    //     ...
    // }

    public enum QueryType
    {
        None,
        Select,
        Construct,
        Ask,
        Describe
    }

    public interface IQuery //: IQueryExpression
    {
        public QueryType QueryType { get; }

        public IQueryExpression Expression { get; }
    }
    public interface IOrderedQuery : IQuery { }


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


    public interface IQueryable
    {
        public bool Query(IAskQuery query);
        public IEnumerable<IReadOnlyDictionary<IVariable, ITerm>> Query(ISelectQuery query);
        public IEnumerable<IFormulaCollection> Query(IConstructQuery query);
        public IFormulaCollection Query(IDescribeQuery query);

        // public IDisposable Query(IAskQuery query, IObserver<bool> observer);
        public IDisposable Query(ISelectQuery query, IObserver<IReadOnlyDictionary<IVariable, ITerm>> observer);
        public IDisposable Query(IConstructQuery query, IObserver<IFormulaCollection> observer);
        //public IDisposable Query(IDescribeQuery query, IObserver<IFormulaCollection> observer);
    }
    public interface IAdvancedQueryable : IQueryable
    {
        // to do: support advanced querying where observers can receive query results and subsequent notifications as query results change due to formulas being removed from and added to formula collections

        // public IDisposable Query(IAskQuery query, IObserver<Change<bool>> observer);
        // public IDisposable Query(ISelectQuery query, IObserver<Change<IReadOnlyDictionary<IVariableTerm, ITerm>>> observer);
        // public IDisposable Query(IConstructQuery query, IObserver<Change<IFormulaCollection>> observer);
        // public IDisposable Query(IDescribeQuery query, IObserver<Change<IFormulaCollection>> observer);

        // see also: "incremental tabling"

        // could also use components from Nifty.Knowledge.Updating
    }


    public enum ExpressionType
    {
        Null,
        Table,
        BasicPattern,
        Filter,
        Assign,
        Extend,
        Concat,
        Join,
        LeftJoin,
        Diff,
        Minus,
        Union,
        Conditional,

        Group,
        Order,
        Project,
        Reduce,
        Distinct,
    }

    public interface IQueryExpression : IEquatable<IQueryExpression> // , IHasMetadata ? , IHasSchema ?
    {
        public ExpressionType ExpressionType { get; }

        // void Visit(IQueryExpressionVisitor visitor);
        // IQueryExpression Transform(IQueryExpressionTransformer transformer);

        public IQueryExpression Clone(bool? isReadOnly = null);
    }

    public interface INullExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Null;
    }
    public interface ITableExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Table;

        public IReadOnlyList<IVariable> Variables { get; }
        public IEnumerable<IReadOnlyDictionary<IVariable, ITerm?>> Rows { get; }
    }
    public interface IFilterExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Filter;

        public IQueryExpression Expression { get; }
        public IFormulaCollection Filter { get; }
    }
    public interface IAssignExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Assign;

        public IQueryExpression Expression { get; }

        public IReadOnlyDictionary<IVariable, ITerm> Assignments { get; }
    }
    public interface IExtendExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Extend;

        public IQueryExpression Expression { get; }

        public IReadOnlyDictionary<IVariable, ITerm> Assignments { get; }
    }
    public interface IConcatExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Concat;

        public IQueryExpression Left { get; }
        public IQueryExpression Right { get; }
    }
    public interface IJoinExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Join;

        public IQueryExpression Left { get; }
        public IQueryExpression Right { get; }
    }
    public interface ILeftJoinExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.LeftJoin;

        public IQueryExpression Left { get; }
        public IQueryExpression Right { get; }
        public IFormulaCollection Filter { get; }
    }
    public interface IDiffExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Diff;

        public IQueryExpression Left { get; }
        public IQueryExpression Right { get; }
    }
    public interface IMinusExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Minus;

        public IQueryExpression Left { get; }
        public IQueryExpression Right { get; }
    }
    public interface IUnionExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Union;

        public IQueryExpression Left { get; }
        public IQueryExpression Right { get; }
    }
    public interface IConditionalExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Conditional;

        public IQueryExpression Left { get; }
        public IQueryExpression Right { get; }
    }

    public interface IGroupExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Group;

        public IQueryExpression Expression { get; }

        public IReadOnlyDictionary<IVariable, ITerm> Variables { get; }

        //...
    }
    public interface IOrderExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Order;

        // to do...
    }
    public interface IProjectExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Project;

        public IQueryExpression Expression { get; }

        public IReadOnlyList<IVariable> Variables { get; }
    }
    public interface IReduceExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Reduce;

        public IQueryExpression Expression { get; }
    }
    public interface IDistinctExpression : IQueryExpression
    {
        ExpressionType IQueryExpression.ExpressionType => ExpressionType.Distinct;

        public IQueryExpression Expression { get; }
    }

    public static class Query
    {
        public static IQuery Parse(ContentType type, string query)
        {
            throw new NotImplementedException();
        }



        // these conclude a query into one of the four query types
        public static IAskQuery Ask(this IQuery query)
        {
            throw new NotImplementedException();
        }
        public static ISelectQuery Select(this IQuery query, params IVariable[] variables)
        {
            throw new NotImplementedException();
        }
        public static IConstructQuery Construct(this IQuery query, IFormulaCollection template)
        {
            throw new NotImplementedException();
        }
        public static IDescribeQuery Describe(this IQuery query, params ITerm[] terms)
        {
            throw new NotImplementedException();
        }



        public static IQuery Where(this IQuery query, IQueryExpression pattern)
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
        public static IOrderedQuery OrderBy(this IQuery query, IVariable variable)
        {
            throw new NotImplementedException();
        }
        public static IOrderedQuery OrderByDescending(this IQuery query, IVariable variable)
        {
            throw new NotImplementedException();
        }
        public static IOrderedQuery ThenBy(this IOrderedQuery query, IVariable variable)
        {
            throw new NotImplementedException();
        }
        public static IOrderedQuery ThenByDescending(this IOrderedQuery query, IVariable variable)
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



        public static INullExpression Null()
        {
            throw new NotImplementedException();
        }
        public static ITableExpression Values(IEnumerable<IReadOnlyDictionary<IVariable, ITerm?>> values)
        {
            throw new NotImplementedException();
        }
        public static IFilterExpression Filter(IQueryExpression formulas, IFormula filter)
        {
            throw new NotImplementedException();
        }
        public static IFilterExpression Filter(IQueryExpression formulas, IFormulaCollection filter)
        {
            throw new NotImplementedException();
        }
        public static IAssignExpression Assign(IQueryExpression formulas, IVariable variable, IFormula expression)
        {
            throw new NotImplementedException();
        }
        public static IExtendExpression Assign(IQueryExpression formulas, IReadOnlyDictionary<IVariable, ITerm> values)
        {
            throw new NotImplementedException();
        }
        public static IAssignExpression Extend(IQueryExpression formulas, IVariable variable, IFormula expression)
        {
            throw new NotImplementedException();
        }
        public static IExtendExpression Extend(IQueryExpression formulas, IReadOnlyDictionary<IVariable, ITerm> values)
        {
            throw new NotImplementedException();
        }
        public static IConcatExpression Concat(IQueryExpression formulas, IQueryExpression other)
        {
            throw new NotImplementedException();
        }
        public static IJoinExpression Join(IQueryExpression formulas, IQueryExpression other)
        {
            throw new NotImplementedException();
        }
        public static ILeftJoinExpression LeftJoin(IQueryExpression formulas, IQueryExpression other)
        {
            throw new NotImplementedException();
        }
        public static ILeftJoinExpression LeftJoin(IQueryExpression formulas, IQueryExpression other, IFormula filter)
        {
            throw new NotImplementedException();
        }
        public static ILeftJoinExpression LeftJoin(IQueryExpression formulas, IQueryExpression other, IFormulaCollection filter)
        {
            throw new NotImplementedException();
        }
        public static IDiffExpression Diff(IQueryExpression formulas, IQueryExpression other)
        {
            throw new NotImplementedException();
        }
        public static IMinusExpression Minus(IQueryExpression formulas, IQueryExpression other)
        {
            throw new NotImplementedException();
        }
        public static IUnionExpression Union(IQueryExpression formulas, IQueryExpression other)
        {
            throw new NotImplementedException();
        }
        public static IConditionalExpression Conditional(IQueryExpression formulas, IQueryExpression other)
        {
            throw new NotImplementedException();
        }
    }
}

namespace Nifty.Knowledge.Querying.Planning
{
    public interface IQueryPlanner
    {

    }
}

namespace Nifty.Knowledge.Reasoning
{
    public interface IReasoner : IHasMetadata
    {
        public IConfiguration Configuration { get; }

        public Task<IReasoner> BindRules(IFormulaCollection rules);

        public Task<IInferredFormulaCollection> Bind(IFormulaCollection collection);
    }

    public interface IInferredFormulaCollection : IFormulaCollection
    {
        public IReasoner Reasoner { get; }
        public IFormulaCollection Base { get; }

        public IEnumerable<IDerivation> Derivations(IFormula formula);
        public IEnumerable<IDerivation> Derivations(IFormulaCollection formulas);
    }

    public interface IDerivation
    {

    }

    public interface IFormulaEvaluator
    {
        public bool Evaluate(IFormula formula, [NotNullWhen(true)] out ITerm? evaluation);
    }
}

namespace Nifty.Knowledge.Schema
{
    public interface ISchema : IFormulaCollection
    {
        public Task<bool> Validate(IFormulaCollection formulas);
    }

    public interface IHasSchema
    {
        public ISchema Schema { get; }
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
        Basic,
        QueryBased,
        Composite,
        Conditional
        /* Other? */
    }

    public interface IUpdate // : IFormulaCollection
    {
        public UpdateType UpdateType { get; }

        public IFormulaCollection Apply(IFormulaCollection formulas);
        public void Update(IFormulaCollection formulas);

        public ICompositeUpdate Then(IUpdate action);
    }

    public interface IBasicUpdate : IUpdate
    {
        public IFormulaCollection Removals { get; }
        public IFormulaCollection Additions { get; }
    }

    public interface IQueryBasedUpdate : IUpdate
    {
        // for each query result, substitute those variables as they occur in removals and additions and remove and add the resultant contents from a formula collection

        public ISelectQuery Query { get; }
        public IFormulaCollection Removals { get; }
        public IFormulaCollection Additions { get; }
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

    public interface IEnvironment<in TAction, TObservation, TReward> : IDisposable
    {
        public (TObservation Observation, TReward Reward, bool Done, IDictionary<string, object> Info) Reset(int seed = 0, bool return_info = false, IDictionary<string, object>? options = null);
        public (TObservation Observation, TReward Reward, bool Done, IDictionary<string, object> Info) Step(TAction action);
    }
}

namespace Nifty.Messaging
{
    public interface IMessageSource : IHasMetadata
    {
        public IDisposable Subscribe(IAskQuery query, IMessageHandler listener);
    }

    public interface IMessageHandler
    {
        public Task Handle(IMessageSource source, IHasMetadata message);
    }
}

namespace Nifty.Messaging.Events
{
    public interface IEventSource : IHasMetadata
    {
        public IDisposable Subscribe(IAskQuery query, IEventHandler listener);
    }
    public interface IEventHandler
    {
        public Task Handle(IEventSource source, IHasMetadata @event, IFormulaCollection data);
    }
}

namespace Nifty.Modelling.Domains
{
    // https://aisconsortium.com/wp-content/uploads/Design-Recommendations-for-ITS_Volume-1-Learner-Modeling.pdf p.39

    /// <summary>
    /// The domain model contains the set of skills, knowledge, and strategies of the topic being tutored.
    /// It normally contains the ideal expert knowledge and may also contain the bugs, mal-rules, and misconceptions that students periodically exhibit.
    /// It is a representation of all the possible student states in the domain.
    /// While these states are typically tied to content, general psychological states (e.g., boredom, persistence) may also be included, since such states are relevant for a full understanding of possible pedagogy within the domain.
    /// </summary>
    public interface IDomainModel : IHasMetadata, IServiceProviderInitializable, IMessageSource, IMessageHandler, IEventSource, IEventHandler, IServiceProviderDisposable
    {
        // see also: https://docs.microsoft.com/en-us/dotnet/api/microsoft.bot.builder.istorage?view=botbuilder-dotnet-stable
        // see also: https://docs.microsoft.com/en-us/azure/bot-service/bot-builder-custom-storage?view=azure-bot-service-4.0
        public IStorage Storage { get; }
    }
}

namespace Nifty.Modelling.Pedagogical
{
    // https://aisconsortium.com/wp-content/uploads/Design-Recommendations-for-ITS_Volume-1-Learner-Modeling.pdf p.39

    /// <summary>
    /// The pedagogical model takes the domain and student models as input and selects tutoring strategies, steps, and actions on what the tutor should do next in the exchange with the student to move the student state to more optimal states in the domain.
    /// In mixed-initiative systems, the students may also initiate actions, ask questions, or request help, but the ITS always needs to be ready to decide “what to do next” at any point and this is determined by a tutoring model that captures the researchers’ pedagogical theories.
    /// Sometimes what to do next implies waiting for the student to respond.
    /// </summary>
    public interface IPedagogicalModel : IHasMetadata, IServiceProviderInitializable, IMessageSource, IMessageHandler, IEventSource, IEventHandler, IServiceProviderDisposable
    {
        public IStorage Storage { get; }
    }
}

namespace Nifty.Modelling.Users
{
    // https://aisconsortium.com/wp-content/uploads/Design-Recommendations-for-ITS_Volume-1-Learner-Modeling.pdf p.39

    /// <summary>
    /// The student model consists of the cognitive, affective, motivational, and other psychological states that are inferred from performance data during the course of learning.
    /// Typically, these states are summary information about the student that will subsequently be used for pedagogical decision making.
    /// The student model is often viewed as a subset of the domain model, which changes over the course of tutoring.
    /// For example, “knowledge tracing” tracks the student’s progress from problem to problem and builds a profile of strengths and weaknesses relative to the domain model.
    /// Since ITS domain models may track general psychological states, student models may also represent these general states of the student.
    /// </summary>
    public interface IUserModel : IHasMetadata, IServiceProviderInitializable, IMessageSource, IMessageHandler, IEventSource, IEventHandler, IServiceProviderDisposable
    {
        public IStorage Storage { get; }
    }
}

namespace Nifty.Planning.Actions
{
    // see also: Grover, Sachin, Tathagata Chakraborti, and Subbarao Kambhampati. "What can automated planning do for intelligent tutoring systems?" ICAPS SPARK (2018).

    public interface IAction : IHasMetadata
    {
        public IAskQuery Preconditions { get; }
        public IUpdate Effects { get; }
    }

    public interface IActionGenerator
    {
        public IEnumerable<IVariable> GetVariables();
        public IFormulaCollection GetConstraints();
        public bool CanReplace(IReadOnlyDictionary<IVariable, ITerm> map, IFormulaEvaluator evaluator);
        public bool Replace(IReadOnlyDictionary<IVariable, ITerm> map, IFormulaEvaluator evaluator, [NotNullWhen(true)] out IAction? result);
    }
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

namespace Nifty.Telemetry
{
    // see also: https://opentelemetry.io/ , https://opentelemetry.io/docs/instrumentation/net/getting-started/
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

        //        public static readonly IUri @string = Term.Uri("http://www.w3.org/2001/XMLSchema#string");

        //        public static readonly IUri @duration = Term.Uri("http://www.w3.org/2001/XMLSchema#duration");
        //        public static readonly IUri @dateTime = Term.Uri("http://www.w3.org/2001/XMLSchema#dateTime");
        //        public static readonly IUri @time = Term.Uri("http://www.w3.org/2001/XMLSchema#time");
        //        public static readonly IUri @date = Term.Uri("http://www.w3.org/2001/XMLSchema#date");
        //        //...
        //        public static readonly IUri @anyURI = Term.Uri("http://www.w3.org/2001/XMLSchema#anyURI");
        //        public static readonly IUri @QName = Term.Uri("http://www.w3.org/2001/XMLSchema#QName");

        //        public static readonly IUri @boolean = Term.Uri("http://www.w3.org/2001/XMLSchema#boolean");

        //        public static readonly IUri @byte = Term.Uri("http://www.w3.org/2001/XMLSchema#byte");
        //        public static readonly IUri @unsignedByte = Term.Uri("http://www.w3.org/2001/XMLSchema#unsignedByte");
        //        public static readonly IUri @short = Term.Uri("http://www.w3.org/2001/XMLSchema#short");
        //        public static readonly IUri @unsignedShort = Term.Uri("http://www.w3.org/2001/XMLSchema#unsignedShort");
        //        public static readonly IUri @int = Term.Uri("http://www.w3.org/2001/XMLSchema#int");
        //        public static readonly IUri @unsignedInt = Term.Uri("http://www.w3.org/2001/XMLSchema#unsignedInt");
        //        public static readonly IUri @long = Term.Uri("http://www.w3.org/2001/XMLSchema#long");
        //        public static readonly IUri @unsignedLong = Term.Uri("http://www.w3.org/2001/XMLSchema#unsignedLong");

        //        public static readonly IUri @decimal = Term.Uri("http://www.w3.org/2001/XMLSchema#decimal");

        //        public static readonly IUri @float = Term.Uri("http://www.w3.org/2001/XMLSchema#float");
        //        public static readonly IUri @double = Term.Uri("http://www.w3.org/2001/XMLSchema#double");
        //    }
        //    public static class Dc
        //    {
        //        public static readonly IUri title = Term.Uri("http://purl.org/dc/terms/title");
        //        public static readonly IUri description = Term.Uri("http://purl.org/dc/terms/description");
        //    }
        //    public static class Swo
        //    {
        //        public static readonly IUri version = Term.Uri("http://www.ebi.ac.uk/swo/SWO_0004000");
        //    }
        //    public static class Rdf
        //    {
        //        public static readonly IUri type = Term.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#type");
        //        public static readonly IUri subject = Term.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#subject");
        //        public static readonly IUri predicate = Term.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#predicate");
        //        public static readonly IUri @object = Term.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#object");

        //        public static readonly IUri Statement = Term.Uri("http://www.w3.org/1999/02/22-rdf-syntax-ns#Statement");
        //    }
        //    public static class Foaf
        //    {
        //        public static readonly IUri name = Term.Uri("http://xmlns.com/foaf/0.1/name");
        //    }
        //    public static class Lom
        //    {

        //    }
        //    public static class Eo
        //    {
        //        public static readonly IUri raisesEventType = Term.Uri("http://www.event-ontology.org/raisesEventType");
        //        public static readonly IUri Event = Term.Uri("http://www.event-ontology.org/Event");
        //    }
        //}

        //public static class Settings
        //{
        //    public static readonly ISetting<bool> ShouldPerformAnalytics = Factory.Setting("http://www.settings.org/analytics/ShouldPerformAnalytics", false);
        //    public static readonly ISetting<bool> ShouldPerformConfigurationAnalytics = Factory.Setting("http://www.settings.org/analytics/ShouldPerformConfigurationAnalytics", false);
        //}

        //public static class Events
        //{
        //    //public static readonly IUriTerm All = Term.Uri("http://www.w3.org/2002/07/owl#Thing");

        //    public static readonly IUri InitializedSession = Term.Uri("http://www.events.org/events/InitializedSession");
        //    public static readonly IUri ObtainedGenerator = Term.Uri("http://www.events.org/events/ObtainedGenerator");
        //    public static readonly IUri GeneratingActivity = Term.Uri("http://www.events.org/events/GeneratingActivity");
        //    public static readonly IUri GeneratedActivity = Term.Uri("http://www.events.org/events/GeneratedActivity");
        //    public static readonly IUri ExecutingActivity = Term.Uri("http://www.events.org/events/ExecutingActivity");
        //    public static readonly IUri ExecutedActivity = Term.Uri("http://www.events.org/events/ExecutedActivity");
        //    public static readonly IUri DisposingSession = Term.Uri("http://www.events.org/events/DisposingSession");

        //    public static class Data
        //    {
        //        public static readonly IUri Algorithm = Term.Uri("urn:eventdata:Algorithm");
        //        public static readonly IUri Generator = Term.Uri("urn:eventdata:Generator");
        //        public static readonly IUri Activity = Term.Uri("urn:eventdata:Activity");
        //        public static readonly IUri User = Term.Uri("urn:eventdata:User");
        //        public static readonly IUri Result = Term.Uri("urn:eventdata:Result");
        //    }
        //}

        public static class Builtins
        {
            public static readonly IUri add = Term.Uri("urn:builtin:add");
            public static readonly IUri and = Term.Uri("urn:builtin:and");
            // ...

            public static class Types
            {

            }
        }

        public static class Composition
        {
            public static readonly IUri hasComposition = Term.Uri("urn:builtin:hasComposition");

            public static readonly IUri exists = Term.Uri("urn:builtin:exists");
            public static readonly IUri notExists = Term.Uri("urn:builtin:notExists");
            public static readonly IUri filter = Term.Uri("urn:builtin:filter");
            public static readonly IUri optional = Term.Uri("urn:builtin:optional");
            public static readonly IUri minus = Term.Uri("urn:builtin:minus");
            public static readonly IUri union = Term.Uri("urn:builtin:union");
            public static readonly IUri bind = Term.Uri("urn:builtin:bind");
            public static readonly IUri values = Term.Uri("urn:builtin:values");

            public static class Types
            {
                public static readonly IUri Expression = Term.Uri("urn:builtin:Expression");

                public static readonly IUri ExistsExpression = Term.Uri("urn:builtin:ExistsExpression");
                public static readonly IUri NotExistsExpression = Term.Uri("urn:builtin:NotExistsExpression");
                public static readonly IUri FilterExpression = Term.Uri("urn:builtin:FilterExpression");
                public static readonly IUri OptionalExpression = Term.Uri("urn:builtin:OptionalExpression");
                public static readonly IUri MinusExpression = Term.Uri("urn:builtin:MinusExpression");
                public static readonly IUri UnionExpression = Term.Uri("urn:builtin:UnionExpression");
                public static readonly IUri BindExpression = Term.Uri("urn:builtin:BindExpression");
                public static readonly IUri ValuesExpression = Term.Uri("urn:builtin:ValuesExpression");
            }
        }

        public static class Constraints
        {
            public static readonly IUri hasConstraint = Term.Uri("urn:builtin:hasConstraint");
        }

        public static class Querying
        {
            public static readonly IUri where = Term.Uri("urn:builtin:where");
            public static readonly IUri groupBy = Term.Uri("urn:builtin:groupBy");
            public static readonly IUri orderBy = Term.Uri("urn:builtin:orderBy");
            public static readonly IUri distinct = Term.Uri("urn:builtin:distinct");
            public static readonly IUri reduced = Term.Uri("urn:builtin:reduced");
            public static readonly IUri offset = Term.Uri("urn:builtin:offset");
            public static readonly IUri limit = Term.Uri("urn:builtin:limit");

            public static readonly IUri ask = Term.Uri("urn:builtin:ask");
            public static readonly IUri select = Term.Uri("urn:builtin:select");
            public static readonly IUri construct = Term.Uri("urn:builtin:construct");
            public static readonly IUri describe = Term.Uri("urn:builtin:describe");

            public static class Types
            {
                public static readonly IUri Query = Term.Uri("urn:builtin:Query");

                public static readonly IUri WhereQuery = Term.Uri("urn:builtin:WhereQuery");
                public static readonly IUri GroupByQuery = Term.Uri("urn:builtin:GroupByQuery");
                public static readonly IUri OrderByQuery = Term.Uri("urn:builtin:OrderByQuery");
                public static readonly IUri DistinctQuery = Term.Uri("urn:builtin:DistinctQuery");
                public static readonly IUri ReducedQuery = Term.Uri("urn:builtin:ReducedQuery");
                public static readonly IUri OffsetQuery = Term.Uri("urn:builtin:OffsetQuery");
                public static readonly IUri LimitQuery = Term.Uri("urn:builtin:LimitQuery");

                public static readonly IUri AskQuery = Term.Uri("urn:builtin:AskQuery");
                public static readonly IUri SelectQuery = Term.Uri("urn:builtin:SelectQuery");
                public static readonly IUri ConstructQuery = Term.Uri("urn:builtin:ConstructQuery");
                public static readonly IUri DescribeQuery = Term.Uri("urn:builtin:DescribeQuery");
            }
        }

        public static readonly IUri type = Term.Uri("urn:builtin:type");
        public static readonly IUri quote = Term.Uri("urn:builtin:quote");
    }

    public static partial class Term
    {
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
        public static IVariable Variable()
        {
            throw new NotImplementedException();
        }
        public static IVariable Variable(string name)
        {
            throw new NotImplementedException();
        }



        public static IBox Box(bool value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(sbyte value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(byte value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(short value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(ushort value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(int value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(uint value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(long value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(ulong value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(float value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(double value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(string value)
        {
            throw new NotImplementedException();
        }
        public static IBox Box(object value)
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



        public static IFormula Formula(ITerm predicate, params ITerm[] arguments)
        {
            throw new NotImplementedException();
        }
        public static IFormula Triple(ITerm predicate, ITerm subject, ITerm @object)
        {
            throw new NotImplementedException();
        }
        public static IFormula TripleSPO(ITerm subject, ITerm predicate, ITerm @object)
        {
            throw new NotImplementedException();
        }
    }

    // there might be other, possibly better, ways, to generate builtin formulas,
    // e.g., allowing developers to provide formula collections which describe the terms to be combined into formulas
    // in these cases, these methods would be generators which bind to the most specific predicates depending on the types of the terms, e.g., integers or complex numbers
    public static partial class Formula
    {
        // these could be extension methods
        //public static bool IsPredicate(this ITerm term, ISchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static int HasArity(this ITerm term, ISchema schema)
        //{
        //    throw new NotImplementedException();
        //}
        //public static IEnumerable<ITerm> ClassesOfArgument(this ITerm term, int index, ISchema schema)
        //{
        //    throw new NotImplementedException();
        //}

        public static IFormula Add(ITerm x, ITerm y)
        {
            return Term.Formula(Keys.Builtins.add, x, y);
        }
        public static IFormula And(ITerm x, ITerm y)
        {
            return Term.Formula(Keys.Builtins.and, x, y);
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

        public static ILambdaFormula Lambda(ITerm body, params IVariable[]? parameters)
        {
            throw new NotImplementedException();
        }
    }

    public static partial class Factory
    {
        public static IFormulaCollection EmptyFormulaCollection
        {
            get
            {
                throw new NotImplementedException();
            }
        }
        public static ISchema EmptySchema
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        // the downside of these factory methods is that cannot easily use the formula collection id, Box(this), in the formulas, so have to use Blank() instead
        // considering expression trees and Constant(value)...
        // however, if the factory methods are desired, can encapsulate use of the builders inside these factory methods

        public static IFormulaCollection FormulaCollection(IEnumerable<IFormula> formulas, ISchema schema, bool isReadOnly = true)
        {
            var builder = Factory.FormulaCollectionBuilder(schema);
            foreach (var formula in formulas)
            {
                builder.Add(formula);
            }
            return builder.Build(isReadOnly: isReadOnly);
        }
        public static IFormulaCollection KnowledgeGraph(IEnumerable<IFormula> formulas, ISchema schema, bool isReadOnly = true)
        {
            var builder = Factory.KnowledgeGraphBuilder(schema);
            foreach (var formula in formulas)
            {
                builder.Add(formula);
            }
            return builder.Build(isReadOnly: isReadOnly);
        }


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

        public static IFormulaCollectionBuilder FormulaCollectionBuilder(ISchema schema)
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollectionBuilder KnowledgeGraphBuilder(ISchema schema)
        {
            throw new NotImplementedException();
        }
        public static ISchemaBuilder SchemaBuilder(ISchema schema)
        {
            throw new NotImplementedException();
        }


        public static IQuery Query()
        {
            throw new NotImplementedException();
        }
    }
}