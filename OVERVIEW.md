# Discussion
This document includes a preliminary overview of the components of the [Nifty](https://github.com/AdamSobieski/Nifty) project. More content coming soon.

## Knowledge Representation and Reasoning
### Formula Collections
Nifty intends to deliver to developers the benefits of multiple approachs to knowledge representation and reasoning. Its knowledge representation combines the best of [Prolog](https://en.wikipedia.org/wiki/Prolog) (and [Scheme](https://en.wikipedia.org/wiki/Scheme_(programming_language))) with [Semantic Web](https://en.wikipedia.org/wiki/Semantic_Web) technologies.

Utilizing a [Turtle](https://www.w3.org/TR/turtle/)-based syntax, we can represent binary formulas:
```
@prefix foaf: <http://xmlns.com/foaf/0.1/>.

foaf:knows(_:alice, _:bob).
foaf:knows(_:bob, _:alice).
```
and can represent n-ary formulas, in this case ternary:
```
@prefix example: <http://example.com/>.

example:f(1, 2, 3).
```

In this approach, terms are URI-based, utilizing XML namespaces and, as n-ary encompasses binary, collections of formulas can be knowledge graphs. When creating collections of formulas, developers can specify that they desire for them to be knowledge graphs.

Benefits of the n-ary URI-based approach include expressiveness and modularity.

### Querying
The expressiveness for querying collections of formulas with Nifty will be comparable with or exceed that of [SPARQL](https://www.w3.org/TR/sparql11-query/) for collections of triples.

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
or, for a ternary example:
```
PREFIX example: <http://example.com/>
SELECT ?x
WHERE
{
    example:f(?x, 2, 3).
}
```

Nifty provides a [fluent](https://en.wikipedia.org/wiki/Fluent_interface) approach for constructing queries.
```cs
IReadOnlyFormulaCollection formulas = ...;
IAskQuery ask = Factory.Query().Where(...).Ask();
bool result = formulas.Query(ask);
```

#### Dynamic and Reactive Queries
Nifty will deliver both pull- and push-based querying (`IEnumerable` and `IObservable`) and intends to explore the powerful feature of enabling push-based queries which deliver notifications as query results change.

### Updating
Nifty provides immutable, read-only, and mutable collections of formulas.

### Schema
Drawing upon Semantic Web technologies, e.g., schema and ontologies, Nifty intends to enable specifying schema of use for validating collections of n-ary formulas.

### Inference
Nifty intends to deliver reasoning capabilities for performing inference over collections of n-ary formulas.