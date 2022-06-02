# Technical Overview

This document includes a preliminary technical overview of some of the components of the [Nifty](https://github.com/AdamSobieski/Nifty) project.

More content is coming soon.



## Extensibility

The architecture of the Nifty project will provide developers with means of conveniently exploring new theories and rapidly prototyping adaptive instructional systems. Nifty will utilize the Managed Extensibility Framework ([System.Composition](https://www.nuget.org/packages/System.Composition/)) to enable the development of interoperable components, addons, plugins, and extensions.



## Knowledge Representation and Reasoning

### Formula Collections

Nifty intends to deliver to developers the benefits of multiple approaches to knowledge representation and reasoning. Its knowledge representation combines the best of [Prolog](https://en.wikipedia.org/wiki/Prolog) (and [Scheme](https://en.wikipedia.org/wiki/Scheme_(programming_language))) with the best of [Semantic Web](https://en.wikipedia.org/wiki/Semantic_Web) technologies.

Utilizing a [Turtle](https://www.w3.org/TR/turtle/)-based syntax, we can represent binary formulas using a predicate-calculus notation:

```
@prefix foaf: <http://xmlns.com/foaf/0.1/>.

foaf:knows(_:alice, _:bob).
foaf:knows(_:bob, _:alice).
```

and we can similarly represent n-ary formulas, in this case ternary:

```
@prefix example: <http://example.com/>.

example:f(1, 2, 3).
```

In this approach, terms are URI-based, utilizing XML namespaces and, as n-ary encompasses binary, collections of formulas include knowledge graphs.

Formulas can be n-ary and can be triples.

```cs
IFormula nary = Factory.Formula(...);
```
```cs
IFormula triple = Factory.Triple(...);
```

When creating collections of formulas, developers can specify whether they desire for them to be knowledge graphs.

```cs
IReadOnlyFormulaCollection formulaCollection = Factory.ReadOnlyFormulaCollection(...);
```
```cs
IReadOnlyFormulaCollection knowledgeGraph = Factory.ReadOnlyKnowledgeGraph(...);
```

Benefits of this n-ary, URI-based approach include both its expressiveness and modularity.

With respect to its expressiveness, in addition to being able to express ternary and higher arity formulas, this approach can express unary formulas.

Uses of unary formulas include, but are not limited to: quoting nested formulas,

```
@prefix example: <http://example.com/>.
@prefix builtin: <http://www.builtin.com/>.

builtin:quote(example:f(1, 2, 3)).
```

evaluating nested formulas,

```
@prefix example: <http://example.com/>.
@prefix builtin: <http://www.builtin.com/>.

builtin:evaluate(example:f(1, 2, 3)).
```

and asserting that nested formulas, e.g., constraints on variables, evaluate to true

```
@prefix example: <http://example.com/>.
@prefix builtin: <http://www.builtin.com/>.

builtin:holds(builtin:greaterThan(?x, ?y)).
```



### Querying

The expressiveness for querying collections of n-ary formulas with Nifty will be comparable with or exceed that of [SPARQL](https://www.w3.org/TR/sparql11-query/).

N-ary queries can be visualized utilizing a SPARQL-based syntax:

```
PREFIX foaf: <http://xmlns.com/foaf/0.1/>
SELECT ?name ?mbox
WHERE
{
    foaf:name(?x, ?name).
    foaf:mbox(?x, ?mbox).
}
```

or, for an n-ary, in this case ternary, example:

```
PREFIX example: <http://example.com/>
SELECT ?x
WHERE
{
    example:f(?x, 2, 3).
}
```

Nifty provides a [fluent](https://en.wikipedia.org/wiki/Fluent_interface) approach for constructing queries. This includes constructing all four kinds of SPARQL-based queries: ASK, SELECT, CONSTRUCT, and DESCRIBE.

```cs
IReadOnlyFormulaCollection formulas = ...;
IAskQuery askQuery = Factory.Query().Where(...).Ask();
bool result = formulas.Query(askQuery);
```
```cs
IReadOnlyFormulaCollection formulas = ...;
ISelectQuery selectQuery = Factory.Query().Where(...).Select(...);
foreach(var result in formulas.Query(selectQuery))
{
    ...
}
```

#### Dynamic and Reactive Queries

Nifty will deliver both pull- and push-based querying (`IEnumerable`- and `IObservable`-based) and intends to explore the powerful feature of enabling push-based queries which deliver notifications as sets of query results change for mutable collections for formula.

### Updating

Nifty provides both immutable (read-only) and mutable collections of formulas. Both can be updated; when applying an update to an immutable collection of formulas, a new collection of formulas is created (which could be an overlay); when updating a mutable collection of formulas, the update can occur in place.

```cs
public interface IUpdate
{
    public UpdateType UpdateType { get; }

    public IReadOnlyFormulaCollection Apply(IReadOnlyFormulaCollection formulas);
    public void Update(IFormulaCollection formulas);

    public ICompositeUpdate Then(IUpdate action);
}
```

In Nifty, there are different types of updates: simple, query-based, conditional, and composite.

### Schema

Drawing upon the Semantic Web technologies of schema and ontologies, Nifty intends to enable specifying schema of use for validating collections of n-ary formulas.

### Inference

Nifty intends to deliver reasoning capabilities for performing inference over collections of n-ary formulas.



## Automated Planning and Scheduling

### Modelling Actions

The Nifty project's approach to modelling actions utilizes the interfaces for querying and updating collections of n-ary formulas.

```cs
public interface IAction : IHasReadOnlyMetadata
{
    public IAskQuery Preconditions { get; }
    public IUpdate Effects { get; }
}
```

The inspectable preconditions of actions are represented by Boolean queries for collections of formulas, e.g., those formulas describing a state of a modelled world. The inspectable effects of actions are represented by updates for collections of formulas. By extending `IHasReadOnlyMetadata`, actions can have metadata.



## Automata

Coming soon.