﻿using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nifty.Activities;
using Nifty.Algorithms;
using Nifty.Analytics;
using Nifty.Collections;
using Nifty.Common;
using Nifty.Dialogs;
using Nifty.Extensibility;
using Nifty.Knowledge;
using Nifty.Knowledge.Building;
using Nifty.Knowledge.Querying;
using Nifty.Knowledge.Reasoning;
using Nifty.Knowledge.Schema;
using Nifty.Knowledge.Updating;
using Nifty.Messaging;
using Nifty.Messaging.Events;
using Nifty.Modelling.Users;
using Nifty.Sessions;
using System.Composition;
using System.Composition.Hosting;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;

namespace Nifty.Activities
{
    // to do: consider using https://github.com/UiPath/CoreWF, https://netflix.github.io/conductor/, https://workflowengine.io/, https://elsa-workflows.github.io/elsa-core/, https://docs.microsoft.com/en-us/azure/logic-apps/, et al (https://github.com/meirwah/awesome-workflow-engines).

    public interface IActivityGeneratorStore : IHasReadOnlyMetadata, ISessionInitializable, ISessionDisposable
    {
        // will this utilize Nifty.Knowledge.Querying or will it have its own querying mechanism?
        // could do something like:
        // public IEnumerable<IReadOnlyDictionary<IVariable, ITerm>> Query(ISelectQuery query);
        // public IActivityGenerator Retrieve(IUri resource);
    }
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
    public interface IAlgorithm : IComponent, IMessageSource, IMessageHandler, IEventSource, IEventHandler
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
    public interface IAnalytics : ISessionInitializable, IMessageHandler, IEventHandler, ISessionDisposable { }

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
    public interface IDialogSystem : IBot, ISessionInitializable, IMessageHandler, IMessageSource, IEventHandler, IEventSource, ISessionDisposable
    {
        public void EnterActivity(Nifty.Activities.IActivity activity);
        public void ExitActivity(Nifty.Activities.IActivity activity);
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
    // considering use of Nifty metadata for describing add-ons, plug-ins, and extensions...
    public interface IComponent : IHasReadOnlyMetadata, ISessionInitializable, ISessionDisposable /*, IMessageSource, IMessageHandler */ { }

    public class ComponentMetadata { }
}

namespace Nifty.Knowledge
{
    public interface IReadOnlyFormulaCollection : Querying.IQueryable, IHasVariables, IHasReadOnlyMetadata, IHasReadOnlySchema, ISubstitute<IReadOnlyFormulaCollection>
    {
        public bool IsReadOnly { get; }
        //public bool IsGround { get; }
        public bool IsInferred { get; }
        public bool IsValid { get; }
        public bool IsGraph { get; }
        public bool IsEnumerable { get; }

        public bool Contains(IFormula formula);

        public IEnumerable<IDerivation> Derivations(IFormula formula);

        public IUpdate DifferenceFrom(IReadOnlyFormulaCollection other);

        public IEnumerable<IFormula> Find(IFormula formula);
        public IDisposable Find(IFormula formula, IObserver<IFormula> observer);

        public IReadOnlyFormulaCollection Clone();
        public IReadOnlyFormulaCollection Clone(IReadOnlyFormulaCollection removals, IReadOnlyFormulaCollection additions);
    }
    public interface IFormulaCollection : IObservableQueryable, IReadOnlyFormulaCollection
    {
        public bool Add(IFormula formula);
        public bool Add(IReadOnlyFormulaCollection formulas);

        public bool Remove(IFormula formula);
        public bool Remove(IReadOnlyFormulaCollection formulas);
    }

    public interface IReadOnlyFormulaList : IReadOnlyFormulaCollection, IReadOnlyList<IFormula> { }



    public enum TermType
    {
        Any,
        Variable,
        Box,
        Blank,
        Uri,
        Formula
    }
    public interface ITerm
    {
        public TermType TermType { get; }
        public object Visit(ITermVisitor visitor);
        public bool Matches(ITerm other);
        //public string? ToString(XmlNamespaceManager xmlns, bool quoting);
    }

    public interface IAny : ITerm { }

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

        public int Count { get; }
        public ITerm this[int index] { get; }
    }
    public interface ILambdaFormula : IFormula { }



    public interface IHasVariables
    {
        public bool IsGround { get; }
        public IEnumerable<IVariable> Variables { get; }
    }
    public interface ISubstitute<T> : IHasVariables
    {
        public bool CanSubstitute(IReadOnlyDictionary<IVariable, ITerm> map);
        public bool Substitute(IReadOnlyDictionary<IVariable, ITerm> map, [NotNullWhen(true)] out T? result);
    }

    public interface IHasReadOnlyIdentifier
    {
        public IConstant Id { get; }
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
        public object Visit(IVariable term);
        public object Visit(IConstant term);
        public object Visit(IBlank term);
        public object Visit(IUri term);
        public object Visit(IFormula formula);
    }



    public interface IKnowledgebase : IFormulaCollection, ISessionInitializable, IEventHandler, ISessionDisposable { }
}

namespace Nifty.Knowledge.Building
{
    public interface IFormulaCollectionBuilder : IFormulaCollection
    {
        public bool IsBuilt { get; }

        public new IReadOnlyFormulaCollection About { get; set; }

        public IReadOnlyFormulaCollection Build(bool isReadOnly = true); // perhaps other parameters, e.g., bool isSelfSchema = false
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
    // "Fluent N-ary SPARQL"
    // version 0.2.1
    // 
    // the expressiveness for querying n-ary formula collections with Nifty should be comparable with or exceed that of SPARQL for triple collections
    // see also: https://www.w3.org/2001/sw/DataAccess/rq23/examples.html
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



    public interface IQueryable
    {
        public bool Query(IAskQuery query);
        public IEnumerable<IReadOnlyDictionary<IVariable, ITerm>> Query(ISelectQuery query);
        public IEnumerable<IReadOnlyFormulaCollection> Query(IConstructQuery query);
        public IReadOnlyFormulaCollection Query(IDescribeQuery query);

        // public IDisposable Query(IAskQuery query, IObserver<bool> observer);
        public IDisposable Query(ISelectQuery query, IObserver<IReadOnlyDictionary<IVariable, ITerm>> observer);
        public IDisposable Query(IConstructQuery query, IObserver<IReadOnlyFormulaCollection> observer);
        //public IDisposable Query(IDescribeQuery query, IObserver<IReadOnlyFormulaCollection> observer);
    }

    public interface IObservableQueryable : IQueryable
    {
        // to do: support advanced querying where observers can receive query results and subsequent notifications as query results change due to formulas being removed from and added to formula collections

        // public IDisposable Query(IAskQuery query, IObserver<Change<bool>> observer);
        // public IDisposable Query(ISelectQuery query, IObserver<Change<IReadOnlyDictionary<IVariableTerm, ITerm>>> observer);
        // public IDisposable Query(IConstructQuery query, IObserver<Change<IReadOnlyFormulaCollection>> observer);
        // public IDisposable Query(IDescribeQuery query, IObserver<Change<IReadOnlyFormulaCollection>> observer);

        // see also: "incremental tabling"

        // could also use components from Nifty.Knowledge.Updating
    }



    public static class Query
    {
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
            //if (query.GetComposition(out ITerm? qc) && pattern.GetComposition(out ITerm? pc))
            //{
            //    var builder = Factory.QueryBuilder(query.Schema);
            //    var metadata = Factory.FormulaCollectionBuilder(query.About.Schema);

            //    metadata.Add(Factory.Formula(Keys.type, builder.Id, Keys.Querying.Types.Query));
            //    metadata.Add(Factory.Formula(Keys.type, builder.Id, Keys.Querying.Types.WhereQuery));
            //    metadata.Add(Factory.Formula(Keys.Composition.hasComposition, builder.Id, Factory.Formula(Keys.Querying.where, qc, pc)));

            //    builder.About = metadata;

            //    var result = builder.Build();
            //    if (!result.About.IsValid) throw new Exception();

            //    return result;
            //}
            //throw new Exception();

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


        // might move some of these general-purpose extension methods, below, from static class Query to static class Composition

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
            //    var builder = Factory.FormulaCollectionBuilder();
            //    var metadata = Factory.FormulaCollectionBuilder(/* there should be builtin schema to use here */);

            //    metadata.Add(Factory.Formula(Keys.type, builder.Id, Keys.Composition.Types.Expression));
            //    metadata.Add(Factory.Formula(Keys.type, builder.Id, Keys.Composition.Types.UnionExpression));
            //    metadata.Add(Factory.Formula(Keys.Composition.hasComposition, builder.Id, Factory.Formula(Keys.Composition.union, fc, oc)));

            //    builder.About = metadata;

            //    var result = builder.Build();
            //    if (!result.About.IsValid) throw new Exception();

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
            //    var builder = Factory.FormulaCollectionBuilder();
            //    var metadata = Factory.FormulaCollectionBuilder(/* there should be a builtin schema */);

            //    var qe = Factory.Formula(Keys.quote, expression);

            //    metadata.Add(Factory.Formula(Keys.type, builder.Id, Keys.Composition.Types.Expression));
            //    metadata.Add(Factory.Formula(Keys.type, builder.Id, Keys.Composition.Types.FilterExpression));
            //    metadata.Add(Factory.Formula(Keys.Composition.hasComposition, builder.Id, Factory.Formula(Keys.Composition.filter, fc, qe)));
            //    metadata.Add(Factory.Formula(Keys.Constraints.hasConstraint, builder.Id, qe));

            //    builder.About = metadata;

            //    var result = builder.Build();
            //    if (!result.About.IsValid) throw new Exception();

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
        public static IReadOnlyFormulaCollection About(this IReadOnlyFormulaCollection formulas)
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
            if (formulas.About.IsEnumerable && formulas.About is IEnumerable<IFormula> fe)
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

    public static class Composition
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

    public interface IFormulaEvaluator
    {
        public bool Evaluate(IFormula formula, [NotNullWhen(true)] out ITerm? evaluation);
    }
}

namespace Nifty.Knowledge.Schema
{
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

    public interface IUpdate // : IReadOnlyFormulaCollection
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
    public interface ISessionDisposable
    {
        public void Dispose(ISession session);
    }

    public interface ISession : IHasReadOnlyMetadata, IInitializable, IMessageSource, IMessageHandler, IEventSource, IEventHandler, IDisposable, IAsyncEnumerable<IActivityGenerator>
    {
        [ImportMany]
        protected IEnumerable<Lazy<IComponent, ComponentMetadata>> Components { get; set; }

        public IConfiguration Configuration { get; }
        public ILogger Log { get; }



        public IDialogSystem DialogueSystem { get; }
        public IKnowledgebase Knowledgebase { get; }
        public IUserModel User { get; }
        public IActivityGeneratorStore Store { get; }
        public IAlgorithm Algorithm { get; }
        public IActivityScheduler Scheduler { get; }
        public IAnalytics Analytics { get; }


        IDisposable IInitializable.Initialize()
        {
            Compose();

            var disposables = new List<IDisposable>(new IDisposable[] {
                Analytics.Initialize(this),
                Knowledgebase.Initialize(this),
                User.Initialize(this),
                Store.Initialize(this),
                Algorithm.Initialize(this),
                Scheduler.Initialize(this),
                DialogueSystem.Initialize(this)
            });

            foreach (var component in Components)
            {
                disposables.Add(component.Value.Initialize(this));
            }

            return Disposable.All(disposables);
        }

        private void Compose()
        {
            string location = Assembly.GetEntryAssembly()?.Location ?? throw new Exception();
            string path = Path.GetDirectoryName(location) ?? throw new Exception();
            path = Path.Combine(path, "Plugins");

            var dlls = Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories).Select(AssemblyLoadContext.Default.LoadFromAssemblyPath).ToList();

            var configuration = new ContainerConfiguration().WithAssemblies(dlls);

            using (var container = configuration.CreateContainer())
            {
                Components = container.GetExports<Lazy<IComponent, ComponentMetadata>>();
            }
        }

        public Task SaveStateInBackground(CancellationToken cancellationToken);

        void IDisposable.Dispose()
        {
            GC.SuppressFinalize(this);

            Algorithm.Dispose(this);
            User.Dispose(this);
            Store.Dispose(this);
            Scheduler.Dispose(this);
            DialogueSystem.Dispose(this);
            Knowledgebase.Dispose(this);
            Analytics.Dispose(this);

            foreach (var component in Components)
            {
                component.Value.Dispose(this);
            }

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
        public static IAny Any()
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
        public static IReadOnlyFormulaCollection ReadOnlyFormulaCollection(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            var builder = Factory.FormulaCollectionBuilder(schema);
            foreach (var formula in formulas)
            {
                builder.Add(formula);
            }
            return builder.Build(isReadOnly: true);
        }
        public static IFormulaCollection FormulaCollection(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            var builder = Factory.FormulaCollectionBuilder(schema);
            foreach (var formula in formulas)
            {
                builder.Add(formula);
            }
            return builder.Build(isReadOnly: false) as IFormulaCollection ?? throw new InvalidCastException();
        }
        public static IReadOnlyFormulaCollection ReadOnlyKnowledgeGraph(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            var builder = Factory.KnowledgeGraphBuilder(schema);
            foreach (var formula in formulas)
            {
                builder.Add(formula);
            }
            return builder.Build(isReadOnly: true);
        }
        public static IFormulaCollection KnowledgeGraph(IEnumerable<IFormula> formulas, IReadOnlySchema schema)
        {
            var builder = Factory.KnowledgeGraphBuilder(schema);
            foreach (var formula in formulas)
            {
                builder.Add(formula);
            }
            return builder.Build(isReadOnly: false) as IFormulaCollection ?? throw new InvalidCastException();
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
        internal static IQueryBuilder QueryBuilder()
        {
            throw new NotImplementedException();
        }

        public static IFormulaCollectionBuilder FormulaCollectionBuilder(IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static IFormulaCollectionBuilder KnowledgeGraphBuilder(IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        public static ISchemaBuilder SchemaBuilder(IReadOnlySchema schema)
        {
            throw new NotImplementedException();
        }
        internal static IQueryBuilder QueryBuilder(IReadOnlySchema schema)
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
}