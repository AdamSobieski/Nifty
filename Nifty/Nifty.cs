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
using Nifty.Knowledge.Querying.Expressions;
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
    public struct Literal : IEquatable<Literal>
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

        public bool Equals(Literal other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            if(obj is Literal other)
            {
                return Equals(other);
            }
            return false;
        }
        public override int GetHashCode()
        {
            int hash = value.GetHashCode();
            if(language != null)
            {
                hash ^= language.GetHashCode() * 31;
            }
            if(datatype != null)
            {
                hash ^= datatype.GetHashCode() * 31;
            }
            return hash;
        }
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

    public static class String
    {
        public static int Add(string text)
        {
            throw new NotImplementedException();
        }
        public static void Dereference(int key)
        {
            throw new NotImplementedException();
        }
        public static string Lookup(int key)
        {
            throw new NotImplementedException();
        }
    }
}

namespace Nifty.Dialogs
{
    public interface IDialogSystem : IBot, IMessagingComponent
    {
        // to do: explore more granular interfaces between dialog systems and items, exercises, and activities
        public void EnterScope(IHasMetadata scope);
        public void ExitScope(IHasMetadata scope);
    }

    // see also: https://docs.microsoft.com/en-us/azure/bot-service/bot-activity-handler-concept?view=azure-bot-service-4.0&tabs=csharp
    // see also: https://docs.microsoft.com/en-us/azure/bot-service/bot-builder-concept-state?view=azure-bot-service-4.0
    // see also: https://github.com/microsoft/BotBuilder-Samples/blob/main/samples/csharp_dotnetcore/19.custom-dialogs/Bots/DialogBot.cs
    // see also: https://github.com/microsoft/BotBuilder-Samples/blob/main/samples/csharp_dotnetcore/45.state-management/Bots/StateManagementBot.cs

    public class DialogBot<T> : ActivityHandler
        where T : Dialog
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
    // see also: http://semantic-web-journal.org/content/componentsjs-semantic-dependency-injection-0

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
        public Stream Retrieve(Knowledge.Uri uri);
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
    public interface IFormulaCollection : Querying.IQueryable, IHasIdentifier, IEnumerable<Formula>
    {
        public bool IsReadOnly { get; }
        public bool IsGraph { get; }
        public bool IsGround { get; }

        public IBasicUpdate DifferenceFrom(IFormulaCollection other);

        public bool Add(Formula formula);
        public bool Add(IEnumerable<Formula> formulas);

        public bool Remove(Formula formula);
        public bool Remove(IEnumerable<Formula> formulas);

        public IFormulaCollection Clone(bool? isReadOnly = null);
        public IFormulaCollection Clone(IEnumerable<Formula> removals, IEnumerable<Formula> additions, bool? isReadOnly = null);
    }

    public interface IFormulaDataset : Querying.IQueryable, IEnumerable<IFormulaCollection>
    {
        public bool IsReadOnly { get; }
        public bool IsGraph { get; }

        public IEnumerable<Knowledge.Uri> Keys { get; }

        public IFormulaCollection this[Knowledge.Uri id] { get; }
        public IFormulaCollection Default { get; }

        public bool Add(Formula formula);
        public bool Add(IEnumerable<Formula> formulas);

        public bool Remove(Formula formula);
        public bool Remove(IEnumerable<Formula> formulas);


        public bool Add(Formula formula, Knowledge.Uri collection);
        public bool Add(IEnumerable<Formula> formulas, Knowledge.Uri collection);

        public bool Remove(Formula formula, Knowledge.Uri collection);
        public bool Remove(IEnumerable<Formula> formulas, Knowledge.Uri collection);

        public IFormulaDataset Clone(bool? isReadOnly = null);
    }

    public enum TermType
    {
        Variable,
        Box,
        Blank,
        Uri,
        Formula
    }

    public abstract partial class Term : IEquatable<Term>
    {
        public abstract TermType TermType { get; }

        public abstract void Visit(ITermVisitor visitor);
        public abstract Term Transform(ITermTransformer transformer);

        public abstract bool Matches(Term other);
        public abstract bool Equals(Term? other);
        public sealed override bool Equals([NotNullWhen(true)] object? obj)
        {
            if (obj is Term otherTerm)
            {
                return Equals(otherTerm);
            }
            return false;
        }
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
        //public string? ToString(XmlNamespaceManager xmlns, bool quoting);
    }
    public sealed class Variable : Term
    {
        internal Variable(string name)
        {
            m_name = Common.String.Add(name);
        }
        ~Variable()
        {
            Common.String.Dereference(m_name);
        }

        private readonly int m_name;
        public string Name => Common.String.Lookup(m_name);

        public override TermType TermType => TermType.Variable;

        public override void Visit(ITermVisitor visitor)
        {
            visitor.Visit(this);
        }
        public override Term Transform(ITermTransformer transformer)
        {
            return transformer.Visit(this);
        }

        public override bool Matches(Term other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals([NotNullWhen(true)] Term? other)
        {
            if(other is Variable otherVariable)
            {
                return m_name == otherVariable.m_name;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return m_name;
        }
    }
    public abstract class Constant : Term { }
    public sealed class Blank : Constant
    {
        internal Blank(string id)
        {
            m_id = Common.String.Add(id);
        }
        ~Blank()
        {
            Common.String.Dereference(m_id);
        }

        private readonly int m_id;

        public string Value => Common.String.Lookup(m_id);

        public override TermType TermType => TermType.Blank;

        public override void Visit(ITermVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override Term Transform(ITermTransformer transformer)
        {
            return transformer.Visit(this);
        }

        public override bool Matches(Term other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals([NotNullWhen(true)] Term? other)
        {
            if(other is Blank otherBlank)
            {
                return m_id == otherBlank.m_id;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return m_id;
        }
    }
    public sealed class Uri : Constant
    {
        internal Uri(string uri)
        {
            m_uri = Common.String.Add(uri);
        }
        ~Uri()
        {
            Common.String.Dereference(m_uri);
        }

        private readonly int m_uri;

        public string Value => Common.String.Lookup(m_uri);

        public override TermType TermType => TermType.Uri;

        public override void Visit(ITermVisitor visitor)
        {
            visitor.Visit(this);
        }
        public override Term Transform(ITermTransformer transformer)
        {
            return transformer.Visit(this);
        }

        public override bool Matches(Term other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals([NotNullWhen(true)] Term? other)
        {
            if(other is Uri otherUri)
            {
                return m_uri == otherUri.m_uri;
            }
            return false;
        }
        public override int GetHashCode()
        {
            return m_uri;
        }
    }
    // should this be an abstract base type for a dozen or so other, internal, classes... one per builtin type?
    public sealed class Box : Constant
    {
        internal Box(object value)
        {
            m_value = value;
        }

        private readonly object m_value;

        public object Value => m_value;

        public override TermType TermType => TermType.Box;

        public override void Visit(ITermVisitor visitor)
        {
            visitor.Visit(this);
        }
        public override Term Transform(ITermTransformer transformer)
        {
            return transformer.Visit(this);
        }

        public override bool Matches(Term other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals([NotNullWhen(true)] Term? other)
        {
            if(other is Box otherBox)
            {
                return m_value.Equals(otherBox.m_value);
            }
            return false;
        }
        public override int GetHashCode()
        {
            return m_value.GetHashCode();
        }
    }

    public sealed partial class Formula : Term
    {
        internal Formula(Term predicate, Term[] args)
        {
            m_predicate = predicate;
            m_args = args;
        }
        private readonly Term m_predicate;
        private readonly Term[] m_args;

        public Term Predicate => m_predicate;

        public Term this[int index]
        {
            get
            {
                return m_args[index];
            }
        }

        public int Count => m_args.Length;

        public override TermType TermType => TermType.Formula;

        public override void Visit(ITermVisitor visitor)
        {
            visitor.Visit(this);
        }
        public override Term Transform(ITermTransformer transformer)
        {
            return transformer.Visit(this);
        }

        public override bool Matches(Term other)
        {
            throw new NotImplementedException();
        }
        public override bool Equals([NotNullWhen(true)] Term? other)
        {
            if(other is Formula otherFormula)
            {
                if (!m_predicate.Equals(otherFormula.m_predicate)) return false;
                int length = m_args.Length;
                if (length != otherFormula.m_args.Length) return false;
                for(int i = 0; i < length; ++i)
                {
                    if (!m_args[i].Equals(otherFormula.m_args[i])) return false;
                }
                return true;
            }
            return false;
        }
        public override int GetHashCode()
        {
            int hash = m_predicate.GetHashCode();
            int length = m_args.Length;
            for (int i = 0; i < length; ++i)
            {
                hash ^= m_args[i].GetHashCode() * hash;
            }
            return hash;
        }
    }


    public interface IHasIdentifier
    {
        public Knowledge.Uri Id { get; }
    }
    public interface IHasMetadata : IHasIdentifier
    {
        public IFormulaCollection About { get; }
    }


    public interface ITermVisitor
    {
        public void Visit(Variable term);
        public void Visit(Box term);
        public void Visit(Blank term);
        public void Visit(Uri term);
        public void Visit(Formula formula);
    }
    public interface ITermTransformer
    {
        public Term Visit(Variable term);
        public Term Visit(Box term);
        public Term Visit(Blank term);
        public Term Visit(Uri term);
        public Term Visit(Formula formula);
    }


    public interface IKnowledgebase : IFormulaDataset, IMessagingComponent { }



    public abstract partial class Term
    {
        public static Knowledge.Uri Uri(string uri)
        {
            return new Knowledge.Uri(uri);
        }
        public static Blank Blank()
        {
            throw new NotImplementedException();
        }
        public static Blank Blank(string id)
        {
            return new Blank(id);
        }
        public static Variable Variable()
        {
            throw new NotImplementedException();
        }
        public static Variable Variable(string name)
        {
            return new Variable(name);
        }



        public static Box Box(bool value)
        {
            return new Box(value);
        }
        public static Box Box(sbyte value)
        {
            return new Box(value);
        }
        public static Box Box(byte value)
        {
            return new Box(value);
        }
        public static Box Box(short value)
        {
            return new Box(value);
        }
        public static Box Box(ushort value)
        {
            return new Box(value);
        }
        public static Box Box(int value)
        {
            return new Box(value);
        }
        public static Box Box(uint value)
        {
            return new Box(value);
        }
        public static Box Box(long value)
        {
            return new Box(value);
        }
        public static Box Box(ulong value)
        {
            return new Box(value);
        }
        public static Box Box(float value)
        {
            return new Box(value);
        }
        public static Box Box(double value)
        {
            return new Box(value);
        }
        public static Box Box(string value)
        {
            return new Box(value);
        }
        public static Box Box(object value)
        {
            throw new NotImplementedException();
        }



        public static Box Literal(bool value)
        {
            // return Box(new Literal(value.ToString(), null, Keys.Semantics.Xsd.boolean.Uri));
            throw new NotImplementedException();
        }
        public static Box Literal(sbyte value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(byte value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(short value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(ushort value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(int value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(uint value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(long value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(ulong value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(float value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(double value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(string value)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(string value, string language)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(string value, string language, Knowledge.Uri datatypeUri)
        {
            throw new NotImplementedException();
        }
        public static Box Literal(string value, Knowledge.Uri datatypeUri)
        {
            throw new NotImplementedException();
        }



        public static Knowledge.Formula Formula(Knowledge.Term predicate, params Knowledge.Term[] arguments)
        {
            return new Knowledge.Formula(predicate, arguments);
        }
        public static Knowledge.Formula Triple(Knowledge.Term predicate, Knowledge.Term subject, Knowledge.Term @object)
        {
            return new Knowledge.Formula(predicate, new Knowledge.Term[] { subject, @object });
        }
        public static Knowledge.Formula TripleSPO(Knowledge.Term subject, Knowledge.Term predicate, Knowledge.Term @object)
        {
            return new Knowledge.Formula(predicate, new Knowledge.Term[] { subject, @object });
        }
    }

    // there might be other, possibly better, ways, to generate builtin formulas,
    // e.g., allowing developers to provide formula collections which describe the terms to be combined into formulas
    // in these cases, these methods would be generators which bind to the most specific predicates depending on the types of the terms, e.g., integers or complex numbers
    public sealed partial class Formula
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

        public static Knowledge.Formula Add(Knowledge.Term x, Knowledge.Term y)
        {
            return new Formula(Keys.Builtins.add, new Knowledge.Term[] { x, y });
        }
        public static Knowledge.Formula And(Knowledge.Term x, Knowledge.Term y)
        {
            return new Formula(Keys.Builtins.and, new Knowledge.Term[] { x, y });
        }
        public static Knowledge.Formula AndAlso(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula Divide(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula Equals(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula ExclusiveOr(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula GreaterThan(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula GreaterThanOrEqual(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula LessThan(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula LessThanOrEqual(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula Multiply(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula Negate(Knowledge.Term x)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula Not(Knowledge.Term x)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula NotEquals(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula Or(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula OrElse(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }
        public static Knowledge.Formula Subtract(Knowledge.Term x, Knowledge.Term y)
        {
            throw new NotImplementedException();
        }

        // ...

        //public static LambdaFormula Lambda(Knowledge.Term body, params Variable[]? parameters)
        //{
        //    throw new NotImplementedException();
        //}
    }
}

namespace Nifty.Knowledge.Building
{
    public interface IFormulaCollectionBuilder : IFormulaCollection
    {
        public bool IsBuilt { get; }

        // public new IFormulaCollection About { get; set; }

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
    // version 0.2.7.0
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
        Undefined,
        Select,
        Construct,
        Ask,
        Describe
    }

    public interface IQuery
    {
        public QueryType QueryType { get; }

        public Expression Expression { get; }
    }
    public interface IOrderedQuery : IQuery
    {

    }


    public interface ISelectQuery : IQuery
    {
        public IReadOnlyList<Variable> Variables { get; }
    }
    public interface IConstructQuery : IQuery
    {
        public IFormulaCollection Template { get; }
    }
    public interface IAskQuery : IQuery
    {

    }
    public interface IDescribeQuery : IQuery
    {
        public IReadOnlyList<Term> Terms { get; }
    }


    public interface IQueryable
    {
        public bool Query(IAskQuery query);
        public IEnumerable<IReadOnlyDictionary<Variable, Term>> Query(ISelectQuery query);
        public IEnumerable<IFormulaCollection> Query(IConstructQuery query);
        public IFormulaCollection Query(IDescribeQuery query);

        // public IDisposable Query(IAskQuery query, IObserver<bool> observer);
        public IDisposable Query(ISelectQuery query, IObserver<IReadOnlyDictionary<Variable, Term>> observer);
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


    public static class Query
    {
        public static IQuery Parse(ContentType type, string query)
        {
            throw new NotImplementedException();
        }



        public static IAskQuery Ask(this IQuery query)
        {
            throw new NotImplementedException();
        }
        public static ISelectQuery Select(this IQuery query, params Variable[] variables)
        {
            throw new NotImplementedException();
        }
        public static IConstructQuery Construct(this IQuery query, IFormulaCollection template)
        {
            throw new NotImplementedException();
        }
        public static IDescribeQuery Describe(this IQuery query, params Term[] terms)
        {
            throw new NotImplementedException();
        }



        public static IQuery Where(this IQuery query, Expression pattern)
        {
            throw new NotImplementedException();
        }
        public static IQuery GroupBy(this IQuery query, Variable variable)
        {
            throw new NotImplementedException();
        }
        public static IQuery GroupBy(this IQuery query, Variable variable, Formula having)
        {
            throw new NotImplementedException();
        }
        public static IOrderedQuery OrderBy(this IQuery query, Variable variable)
        {
            throw new NotImplementedException();
        }
        public static IOrderedQuery OrderByDescending(this IQuery query, Variable variable)
        {
            throw new NotImplementedException();
        }
        public static IOrderedQuery ThenBy(this IOrderedQuery query, Variable variable)
        {
            throw new NotImplementedException();
        }
        public static IOrderedQuery ThenByDescending(this IOrderedQuery query, Variable variable)
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
    }
}

namespace Nifty.Knowledge.Querying.Expressions
{
    public enum ExpressionType
    {
        Empty,
        Table,
        BasicPattern,
        FormulaCollection,
        Filter,
        Assign,
        Extend,
        Join,
        LeftJoin,
        Diff,
        Minus,
        Union,
        Conditional,

        GroupBy,
        OrderBy,
        Project,
        Reduce,
        Distinct,
        Slice
    }

    public enum SortDirection
    {
        Descending,
        Ascending
    }

    public sealed class EmptyExpression : Expression
    {
        internal EmptyExpression() { }

        public override ExpressionType ExpressionType => ExpressionType.Empty;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Empty) return false;
            return true;
        }
    }
    public sealed class TableExpression : Expression
    {
        internal TableExpression(IEnumerable<IReadOnlyDictionary<Variable, Term?>> rows)
        {
            IEnumerable<Variable>? m_tmp = null;

            foreach (var row in rows)
            {
                if (m_tmp == null)
                {
                    m_tmp = row.Keys;
                }
                else
                {
                    if (!m_tmp.SequenceEqual(row.Keys)) throw new ArgumentException("Each row should have identical variables.", nameof(rows));
                }
            }

            m_variables = m_tmp != null ? new List<Variable>(m_tmp).AsReadOnly() : new List<Variable>(0).AsReadOnly();
            m_rows = rows;
        }
        private readonly IReadOnlyList<Variable> m_variables;
        private readonly IEnumerable<IReadOnlyDictionary<Variable, Term?>> m_rows;

        public override ExpressionType ExpressionType => ExpressionType.Table;

        public IReadOnlyList<Variable> Variables => m_variables;
        public IEnumerable<IReadOnlyDictionary<Variable, Term?>> Rows => m_rows;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Table) return false;
            if (!(other is TableExpression otherTable)) return false;
            return m_rows.SequenceEqual(otherTable.m_rows);
        }
    }
    public sealed class BasicFormulaCollectionPatternExpression : Expression
    {
        internal BasicFormulaCollectionPatternExpression(IFormulaCollection formulaCollection)
        {
            if (!formulaCollection.IsReadOnly) throw new ArgumentException("A formula collection used in a query must be read-only.", nameof(formulaCollection));
            m_formulaCollection = formulaCollection;
        }
        private readonly IFormulaCollection m_formulaCollection;

        public override ExpressionType ExpressionType => ExpressionType.BasicPattern;

        public new IFormulaCollection FormulaCollection => m_formulaCollection;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.BasicPattern) return false;
            if (!(other is BasicFormulaCollectionPatternExpression otherBasicPattern)) return false;
            return m_formulaCollection.SequenceEqual(otherBasicPattern.m_formulaCollection);
        }
    }
    public sealed class FormulaCollectionExpression : Expression
    {
        internal FormulaCollectionExpression(Expression expression, Term term)
        {
            m_expression = expression;
            m_term = term;
        }
        private readonly Expression m_expression;
        private readonly Term m_term;

        public override ExpressionType ExpressionType => ExpressionType.FormulaCollection;

        public Expression Expression => m_expression;
        public Term Term => m_term;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.FormulaCollection) return false;
            if (!(other is FormulaCollectionExpression otherFormulaCollection)) return false;
            return m_expression.Equals(otherFormulaCollection.m_expression) && m_term.Equals(otherFormulaCollection.m_term);
        }
    }
    public sealed class FilterExpression : Expression
    {
        internal FilterExpression(Expression expression, IFormulaCollection filter)
        {
            m_expression = expression;
            m_filter = filter;
        }
        private readonly Expression m_expression;
        private readonly IFormulaCollection m_filter;

        public override ExpressionType ExpressionType => ExpressionType.Filter;

        public Expression Expression => m_expression;
        public new IFormulaCollection Filter => m_filter;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Filter) return false;
            if (!(other is FilterExpression otherFilter)) return false;
            return m_expression.Equals(otherFilter.m_expression) && m_filter.SequenceEqual(otherFilter.m_filter);
        }
    }
    public sealed class AssignExpression : Expression
    {
        internal AssignExpression(Expression expression, IReadOnlyDictionary<Variable, Term> assignments)
        {
            m_expression = expression;
            m_assignments = assignments;
        }
        private readonly Expression m_expression;
        private readonly IReadOnlyDictionary<Variable, Term> m_assignments;

        public override ExpressionType ExpressionType => ExpressionType.Assign;

        public Expression Expression => m_expression;

        public IReadOnlyDictionary<Variable, Term> Assignments => m_assignments;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Assign) return false;
            if (!(other is AssignExpression otherAssign)) return false;
            return m_expression.Equals(otherAssign.m_expression) && m_assignments.SequenceEqual(otherAssign.m_assignments);
        }
    }
    public sealed class ExtendExpression : Expression
    {
        internal ExtendExpression(Expression expression, IReadOnlyDictionary<Variable, Term> assignments)
        {
            m_expression = expression;
            m_assignments = assignments;
        }
        private readonly Expression m_expression;
        private readonly IReadOnlyDictionary<Variable, Term> m_assignments;

        public override ExpressionType ExpressionType => ExpressionType.Extend;

        public Expression Expression => m_expression;

        public IReadOnlyDictionary<Variable, Term> Assignments => m_assignments;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Extend) return false;
            if (!(other is ExtendExpression otherExtend)) return false;
            return m_expression.Equals(otherExtend.m_expression) && m_assignments.SequenceEqual(otherExtend.m_assignments);
        }
    }
    public sealed class JoinExpression : Expression
    {
        internal JoinExpression(Expression left, Expression right)
        {
            m_left = left;
            m_right = right;
        }
        private readonly Expression m_left;
        private readonly Expression m_right;

        public override ExpressionType ExpressionType => ExpressionType.Join;

        public Expression Left => m_left;
        public Expression Right => m_right;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Join) return false;
            if (!(other is JoinExpression otherJoin)) return false;
            return m_left.Equals(otherJoin.m_left) && m_right.Equals(otherJoin.m_right);
        }
    }
    public sealed class LeftJoinExpression : Expression
    {
        internal LeftJoinExpression(Expression left, Expression right, IFormulaCollection filter)
        {
            m_left = left;
            m_right = right;
            m_filter = filter;
        }
        private readonly Expression m_left;
        private readonly Expression m_right;
        private readonly IFormulaCollection m_filter;

        public override ExpressionType ExpressionType => ExpressionType.LeftJoin;

        public Expression Left => m_left;
        public Expression Right => m_right;
        public new IFormulaCollection Filter => m_filter;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.LeftJoin) return false;
            if (!(other is LeftJoinExpression otherLeftJoin)) return false;
            return m_left.Equals(otherLeftJoin.m_left) && m_right.Equals(otherLeftJoin.m_right) && m_filter.Equals(otherLeftJoin.m_filter);
        }
    }
    public sealed class DiffExpression : Expression
    {
        internal DiffExpression(Expression left, Expression right)
        {
            m_left = left;
            m_right = right;
        }
        private readonly Expression m_left;
        private readonly Expression m_right;

        public override ExpressionType ExpressionType => ExpressionType.Diff;

        public Expression Left => m_left;
        public Expression Right => m_right;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Diff) return false;
            if (!(other is DiffExpression otherDiff)) return false;
            return m_left.Equals(otherDiff.m_left) && m_right.Equals(otherDiff.m_right);
        }
    }
    public sealed class MinusExpression : Expression
    {
        internal MinusExpression(Expression left, Expression right)
        {
            m_left = left;
            m_right = right;
        }
        private readonly Expression m_left;
        private readonly Expression m_right;

        public override ExpressionType ExpressionType => ExpressionType.Minus;

        public Expression Left => m_left;
        public Expression Right => m_right;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Minus) return false;
            if (!(other is MinusExpression otherMinus)) return false;
            return m_left.Equals(otherMinus.m_left) && m_right.Equals(otherMinus.m_right);
        }
    }
    public sealed class UnionExpression : Expression
    {
        internal UnionExpression(Expression left, Expression right)
        {
            m_left = left;
            m_right = right;
        }
        private readonly Expression m_left;
        private readonly Expression m_right;

        public override ExpressionType ExpressionType => ExpressionType.Union;

        public Expression Left => m_left;
        public Expression Right => m_right;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Union) return false;
            if (!(other is UnionExpression otherUnion)) return false;
            return m_left.Equals(otherUnion.m_left) && m_right.Equals(otherUnion.m_right);
        }
    }
    public sealed class ConditionalExpression : Expression
    {
        internal ConditionalExpression(Expression left, Expression right)
        {
            m_left = left;
            m_right = right;
        }
        private readonly Expression m_left;
        private readonly Expression m_right;

        public override ExpressionType ExpressionType => ExpressionType.Conditional;

        public Expression Left => m_left;
        public Expression Right => m_right;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Conditional) return false;
            if (!(other is ConditionalExpression otherConditional)) return false;
            return m_left.Equals(otherConditional.m_left) && m_right.Equals(otherConditional.m_right);
        }
    }

    internal sealed class GroupByExpression : Expression
    {
        internal GroupByExpression(Expression expression, IReadOnlyDictionary<Variable, Term> groupVariables, IReadOnlyDictionary<Variable, Formula> aggregators)
        {
            m_expression = expression;
            m_groupVariables = groupVariables;
            m_aggregators = aggregators;
        }
        private readonly Expression m_expression;
        private readonly IReadOnlyDictionary<Variable, Term> m_groupVariables;
        private readonly IReadOnlyDictionary<Variable, Formula> m_aggregators;

        public override ExpressionType ExpressionType => ExpressionType.GroupBy;

        public Expression Expression => m_expression;

        public IReadOnlyDictionary<Variable, Term> Variables => m_groupVariables;

        public IReadOnlyDictionary<Variable, Formula> Aggregators => m_aggregators;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.GroupBy) return false;
            if (!(other is GroupByExpression otherGroupBy)) return false;
            return m_expression.Equals(otherGroupBy.m_expression) && m_groupVariables.SequenceEqual(otherGroupBy.m_groupVariables) && m_aggregators.SequenceEqual(otherGroupBy.m_aggregators);
        }
    }
    internal sealed class OrderByExpression : Expression
    {
        internal OrderByExpression(Expression expression, IReadOnlyDictionary<Variable, SortDirection> sorts)
        {
            m_expression = expression;
            m_sorts = sorts;
        }
        private readonly Expression m_expression;
        private readonly IReadOnlyDictionary<Variable, SortDirection> m_sorts;

        public override ExpressionType ExpressionType => ExpressionType.OrderBy;

        public Expression Expression => m_expression;

        public IReadOnlyDictionary<Variable, SortDirection> Sorts => m_sorts;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.OrderBy) return false;
            if (!(other is OrderByExpression otherOrderBy)) return false;
            return m_expression.Equals(otherOrderBy.m_expression) && m_sorts.SequenceEqual(otherOrderBy.m_sorts);
        }
    }
    internal sealed class ProjectExpression : Expression
    {
        internal ProjectExpression(Expression expression, IReadOnlyList<Variable> variables)
        {
            m_expression = expression;
            m_variables = variables;
        }
        private readonly Expression m_expression;
        private readonly IReadOnlyList<Variable> m_variables;

        public override ExpressionType ExpressionType => ExpressionType.Project;

        public Expression Expression => m_expression;

        public IReadOnlyList<Variable> Variables => m_variables;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Project) return false;
            if (!(other is ProjectExpression otherProject)) return false;
            return m_expression.Equals(otherProject.m_expression) && m_variables.SequenceEqual(otherProject.m_variables);
        }
    }
    internal sealed class ReduceExpression : Expression
    {
        internal ReduceExpression(Expression expression)
        {
            m_expression = expression;
        }
        private readonly Expression m_expression;

        public override ExpressionType ExpressionType => ExpressionType.Reduce;

        public Expression Expression => m_expression;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Reduce) return false;
            if (!(other is ReduceExpression otherReduce)) return false;
            return m_expression.Equals(otherReduce.m_expression);
        }
    }
    internal sealed class DistinctExpression : Expression
    {
        internal DistinctExpression(Expression expression)
        {
            m_expression = expression;
        }
        private readonly Expression m_expression;

        public override ExpressionType ExpressionType => ExpressionType.Distinct;

        public Expression Expression => m_expression;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Distinct) return false;
            if (!(other is DistinctExpression otherDistinct)) return false;
            return m_expression.Equals(otherDistinct.m_expression);
        }
    }
    internal sealed class SliceExpression : Expression
    {
        internal SliceExpression(Expression expression, uint start, uint? length)
        {
            m_expression = expression;
            m_start = start;
            m_length = length;
        }
        private readonly Expression m_expression;
        private readonly uint m_start;
        private readonly uint? m_length;

        public override ExpressionType ExpressionType => ExpressionType.Slice;

        public Expression Expression => m_expression;
        public uint Start => m_start;
        public uint? Length => m_length;

        public override bool Equals(Expression? other)
        {
            if (other == null) return false;
            if (other.ExpressionType != ExpressionType.Slice) return false;
            if (!(other is SliceExpression otherSlice)) return false;
            return m_expression.Equals(otherSlice.m_expression) && m_start == otherSlice.m_start && m_length == otherSlice.m_length;
        }
    }

    public abstract class Expression : IEquatable<Expression>
    {
        private static readonly EmptyExpression s_empty = new EmptyExpression();

        public static EmptyExpression Empty()
        {
            return s_empty;
        }
        public static TableExpression Values(IEnumerable<IReadOnlyDictionary<Variable, Term?>> values)
        {
            return new TableExpression(values);
        }
        public static BasicFormulaCollectionPatternExpression Pattern(IFormulaCollection formulaCollection)
        {
            return new BasicFormulaCollectionPatternExpression(formulaCollection);
        }
        public static FormulaCollectionExpression FormulaCollection(Expression expression, Term term)
        {
            return new FormulaCollectionExpression(expression, term);
        }
        public static FilterExpression Filter(Expression expression, IFormulaCollection filter)
        {
            return new FilterExpression(expression, filter);
        }
        public static AssignExpression Assign(Expression expression, Variable variable, Formula formula)
        {
            var assignments = new Dictionary<Variable, Term> { { variable, formula } };
            return new AssignExpression(expression, assignments);
        }
        public static AssignExpression Assign(Expression expression, IReadOnlyDictionary<Variable, Term> assignments)
        {
            return new AssignExpression(expression, assignments);
        }
        public static ExtendExpression Extend(Expression expression, Variable variable, Formula formula)
        {
            var assignments = new Dictionary<Variable, Term> { { variable, formula } };
            return new ExtendExpression(expression, assignments);
        }
        public static ExtendExpression Extend(Expression expression, IReadOnlyDictionary<Variable, Term> assignments)
        {
            return new ExtendExpression(expression, assignments);
        }
        public static JoinExpression Join(Expression left, Expression right)
        {
            return new JoinExpression(left, right);
        }
        public static LeftJoinExpression LeftJoin(Expression left, Expression right)
        {
            return new LeftJoinExpression(left, right, Factory.EmptyFormulaCollection);
        }
        public static LeftJoinExpression LeftJoin(Expression left, Expression right, IFormulaCollection filter)
        {
            return new LeftJoinExpression(left, right, filter);
        }
        public static DiffExpression Diff(Expression left, Expression right)
        {
            return new DiffExpression(left, right);
        }
        public static MinusExpression Minus(Expression left, Expression right)
        {
            return new MinusExpression(left, right);
        }
        public static UnionExpression Union(Expression left, Expression right)
        {
            return new UnionExpression(left, right);
        }
        public static ConditionalExpression Conditional(Expression left, Expression right)
        {
            return new ConditionalExpression(left, right);
        }


        internal static GroupByExpression GroupBy(Expression expression, IReadOnlyDictionary<Variable, Term> groupVariables, IReadOnlyDictionary<Variable, Formula> aggregators)
        {
            return new GroupByExpression(expression, groupVariables, aggregators);
        }
        internal static OrderByExpression OrderBy(Expression expression, IReadOnlyDictionary<Variable, SortDirection> sorts)
        {
            return new OrderByExpression(expression, sorts);
        }
        internal static ProjectExpression Project(Expression expression, IReadOnlyList<Variable> variables)
        {
            return new ProjectExpression(expression, variables);
        }
        internal static ReduceExpression Reduce(Expression expression)
        {
            return new ReduceExpression(expression);
        }
        internal static DistinctExpression Distinct(Expression expression)
        {
            return new DistinctExpression(expression);
        }
        internal static SliceExpression Slice(Expression expression, uint start, uint? length)
        {
            return new SliceExpression(expression, start, length);
        }


        public abstract ExpressionType ExpressionType { get; }

        // void Visit(IQueryExpressionVisitor visitor);
        // IQueryExpression Transform(IQueryExpressionTransformer transformer);

        public abstract bool Equals(Expression? other);
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public sealed override bool Equals(object? obj)
        {
            if (obj != null && obj is Expression other)
                return Equals(other);
            else
                return false;
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

        public IEnumerable<IDerivation> Derivations(Formula formula);
        public IEnumerable<IDerivation> Derivations(IEnumerable<Formula> formulas);
    }

    public interface IDerivation
    {

    }

    public interface IFormulaEvaluator
    {
        public bool Evaluate(Formula formula, [NotNullWhen(true)] out Term? evaluation);
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
            m_uri = new System.Uri(uri);
        }
        private readonly System.Uri m_uri;
        public System.Uri Uri => m_uri;
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
        public IEnumerable<Variable> GetVariables();
        public IFormulaCollection GetConstraints();
        public bool CanReplace(IReadOnlyDictionary<Variable, Knowledge.Term> map, IFormulaEvaluator evaluator);
        public bool Replace(IReadOnlyDictionary<Variable, Knowledge.Term> map, IFormulaEvaluator evaluator, [NotNullWhen(true)] out IAction? result);
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
            public static readonly Knowledge.Uri add = Term.Uri("urn:builtin:add");
            public static readonly Knowledge.Uri and = Term.Uri("urn:builtin:and");
            // ...

            public static readonly Knowledge.Uri lambda = Term.Uri("urn:builtin:lambda");

            public static class Types
            {

            }
        }

        public static class Composition
        {
            public static readonly Knowledge.Uri hasComposition = Term.Uri("urn:builtin:hasComposition");

            public static readonly Knowledge.Uri exists = Term.Uri("urn:builtin:exists");
            public static readonly Knowledge.Uri notExists = Term.Uri("urn:builtin:notExists");
            public static readonly Knowledge.Uri filter = Term.Uri("urn:builtin:filter");
            public static readonly Knowledge.Uri optional = Term.Uri("urn:builtin:optional");
            public static readonly Knowledge.Uri minus = Term.Uri("urn:builtin:minus");
            public static readonly Knowledge.Uri union = Term.Uri("urn:builtin:union");
            public static readonly Knowledge.Uri bind = Term.Uri("urn:builtin:bind");
            public static readonly Knowledge.Uri values = Term.Uri("urn:builtin:values");

            public static class Types
            {
                public static readonly Knowledge.Uri Expression = Term.Uri("urn:builtin:Expression");

                public static readonly Knowledge.Uri ExistsExpression = Term.Uri("urn:builtin:ExistsExpression");
                public static readonly Knowledge.Uri NotExistsExpression = Term.Uri("urn:builtin:NotExistsExpression");
                public static readonly Knowledge.Uri FilterExpression = Term.Uri("urn:builtin:FilterExpression");
                public static readonly Knowledge.Uri OptionalExpression = Term.Uri("urn:builtin:OptionalExpression");
                public static readonly Knowledge.Uri MinusExpression = Term.Uri("urn:builtin:MinusExpression");
                public static readonly Knowledge.Uri UnionExpression = Term.Uri("urn:builtin:UnionExpression");
                public static readonly Knowledge.Uri BindExpression = Term.Uri("urn:builtin:BindExpression");
                public static readonly Knowledge.Uri ValuesExpression = Term.Uri("urn:builtin:ValuesExpression");
            }
        }

        public static class Constraints
        {
            public static readonly Knowledge.Uri hasConstraint = Term.Uri("urn:builtin:hasConstraint");
        }

        public static class Querying
        {
            public static readonly Knowledge.Uri where = Term.Uri("urn:builtin:where");
            public static readonly Knowledge.Uri groupBy = Term.Uri("urn:builtin:groupBy");
            public static readonly Knowledge.Uri orderBy = Term.Uri("urn:builtin:orderBy");
            public static readonly Knowledge.Uri distinct = Term.Uri("urn:builtin:distinct");
            public static readonly Knowledge.Uri reduced = Term.Uri("urn:builtin:reduced");
            public static readonly Knowledge.Uri offset = Term.Uri("urn:builtin:offset");
            public static readonly Knowledge.Uri limit = Term.Uri("urn:builtin:limit");

            public static readonly Knowledge.Uri ask = Term.Uri("urn:builtin:ask");
            public static readonly Knowledge.Uri select = Term.Uri("urn:builtin:select");
            public static readonly Knowledge.Uri construct = Term.Uri("urn:builtin:construct");
            public static readonly Knowledge.Uri describe = Term.Uri("urn:builtin:describe");

            public static class Types
            {
                public static readonly Knowledge.Uri Query = Term.Uri("urn:builtin:Query");

                public static readonly Knowledge.Uri WhereQuery = Term.Uri("urn:builtin:WhereQuery");
                public static readonly Knowledge.Uri GroupByQuery = Term.Uri("urn:builtin:GroupByQuery");
                public static readonly Knowledge.Uri OrderByQuery = Term.Uri("urn:builtin:OrderByQuery");
                public static readonly Knowledge.Uri DistinctQuery = Term.Uri("urn:builtin:DistinctQuery");
                public static readonly Knowledge.Uri ReducedQuery = Term.Uri("urn:builtin:ReducedQuery");
                public static readonly Knowledge.Uri OffsetQuery = Term.Uri("urn:builtin:OffsetQuery");
                public static readonly Knowledge.Uri LimitQuery = Term.Uri("urn:builtin:LimitQuery");

                public static readonly Knowledge.Uri AskQuery = Term.Uri("urn:builtin:AskQuery");
                public static readonly Knowledge.Uri SelectQuery = Term.Uri("urn:builtin:SelectQuery");
                public static readonly Knowledge.Uri ConstructQuery = Term.Uri("urn:builtin:ConstructQuery");
                public static readonly Knowledge.Uri DescribeQuery = Term.Uri("urn:builtin:DescribeQuery");
            }
        }

        public static readonly Knowledge.Uri type = Term.Uri("urn:builtin:type");
        public static readonly Knowledge.Uri quote = Term.Uri("urn:builtin:quote");
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

        public static IFormulaCollection FormulaCollection(IEnumerable<Knowledge.Formula> formulas, ISchema schema, bool isReadOnly = true)
        {
            var builder = Factory.FormulaCollectionBuilder(schema);
            foreach (var formula in formulas)
            {
                builder.Add(formula);
            }
            return builder.Build(isReadOnly: isReadOnly);
        }
        public static IFormulaCollection KnowledgeGraph(IEnumerable<Knowledge.Formula> formulas, ISchema schema, bool isReadOnly = true)
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