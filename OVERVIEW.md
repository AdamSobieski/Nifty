# Technical Overview

## Introduction

The [Nifty](https://github.com/AdamSobieski/Nifty) project will be a framework for creating adaptive instructional systems. It is currently under development.

It will be extensible, providing developers with means of conveniently exploring new theories and rapidly prototyping adaptive instructional systems. Developers will be able to create interoperable components, add-ons, plug-ins, and extensions.

It will support configurability, enabling end-users ? learners, parents, educators, administrators, and schoolboard officials ? to configure resultant adaptive instructional systems.

It will provide scalability, enabling the development of solutions which serve a large number of learners simultaneously.

### Adaptive Instructional Systems
![AIS](Images/AIS.png)



## Use Cases

### Mathematics Education

The Nifty project will support the development of [intelligent tutoring systems](https://en.wikipedia.org/wiki/Intelligent_tutoring_system) for mathematics education.

### Character Education and Social and Emotional Learning

The Nifty project will support the exploration of uses of interactive stories, e.g., story-based items, [digital gamebooks](https://en.wikipedia.org/wiki/Gamebook), [interactive films](https://en.wikipedia.org/wiki/Interactive_film), and [serious games](https://en.wikipedia.org/wiki/Serious_game), as items, exercises, and activities for character education and social and emotional learning.

### Software Training

The Nifty project intends to enable the construction of next-generation intelligent tutoring systems which can train end-users in the use of software applications, e.g., Office, IDE, 3D modelling, and CAD/CAE software applications.

#### Video-calling, Screencasting, and Remote Desktop Software

The [Microsoft Bot Framework](https://github.com/microsoft/botframework-sdk) supports a number of [video-calling](https://en.wikipedia.org/wiki/Videotelephony) communication channels and, via interoperation with video-calling [screencasting](https://en.wikipedia.org/wiki/Screencast) and/or [remote desktop software](https://en.wikipedia.org/wiki/Remote_desktop_software), intelligent tutoring systems could be developed which can observe learners' performances of items, exercises, and activities pertaining to software use.

Technical topics, in these regards, include attaching user-input events, application commands, and other application events in ancillary tracks which can be streamed to and be processed by intelligent tutoring systems. Without these data in ancillary tracks, intelligent tutoring systems would have to utilize computer vision algorithms to process video to detect precise user interactions with software applications.



## Dialogue Systems

### Bot Framework

The [Microsoft Bot Framework](https://github.com/microsoft/botframework-sdk) supports building large-scale dialogue systems and chatbots which can utilize multiple channels including: Microsoft Teams, Direct Line, Web Chat, Skype, Email, Facebook, Slack, Kik, Telegram, Line, GroupMe, Twilio (SMS), Alexa Skills, Google Actions, Google Hangouts, WebEx, WhatsApp (Infobip), Zoom, RingCentral, and Cortana. Accordingly, the Nifty project is exploring using the Bot Framework for accelerating adaptive instruction systems research and development.

Learners could use [conversational user interfaces](https://en.wikipedia.org/wiki/Conversational_user_interface) to interact with educational items, exercises, and activities.

Learners could engage in meaningful, contextual dialogues with adaptive instructional systems about educational items, exercises, and activities.

### Speech Recognition and Synthesis

[Coqui](https://coqui.ai/) (available on GitHub [here](https://github.com/coqui-ai/)) is a state-of-the-art, open-source software system for speech-to-text and text-to-speech. The Nifty project will explore the utilization of Coqui and its interoperability with the Bot Framework.

Scenarios of interest include the capability to train and utilize models which support the recognition and synthesis of mathematical expressions.



## Educational Items, Exercises and Activities

Educational items, exercises, and activities can each be stored in their own .NET assembly and loaded and unloaded by adaptive instructional systems at runtime. In this way, per the use cases, educational items, exercises, or activities could be mathematics exercises, interactive stories, software training exercises, and more.

Educational items, exercises, and activities can be "compiled" from other formats, e.g., QTI, into components in .NET assemblies.

Topics of technical interest include educational items, exercises, and activities being able to render and display text, imagery, video, and 3D graphics across video-calling channels, e.g., Skype, Zoom, and WebRTC, and to be able to process related input video streams.

Topics of technical interest include the querying and selecting of educational items, exercises, and activities by adaptive instructional systems.



## Knowledge Representation and Reasoning

### Collections of Formulas

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
IFormula nary = Term.Formula(...);
```
```cs
IFormula triple = Term.Triple(...);
```

When creating collections of formulas, developers can specify whether they desire for them to be knowledge graphs.

```cs
IFormulaCollection formulaCollection = Factory.FormulaCollection(...);
```
```cs
IFormulaCollection knowledgeGraph = Factory.KnowledgeGraph(...);
```

Benefits of this n-ary, URI-based approach include both its expressiveness and modularity.



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
IFormulaCollection formulas = ...;
IAskQuery askQuery = Factory.Query().Where(...).Ask();
bool result = formulas.Query(askQuery);
```
```cs
IFormulaCollection formulas = ...;
ISelectQuery selectQuery = Factory.Query().Where(...).Select(...);
foreach(var result in formulas.Query(selectQuery))
{
    ...
}
```

#### Planning and Optimization

Nifty will deliver n-ary SPARQL query planning and optimization.

#### Dynamic and Reactive Queries

Nifty will deliver both pull- and push-based querying (`IEnumerable`- and `IObservable`-based).

Nifty intends to explore the powerful feature of enabling push-based queries which deliver notifications as sets of query results change for mutable formula collections.

### Updating

Nifty provides both immutable (read-only) and mutable collections of formulas. Both can be updated; when applying an update to an immutable collection of formulas, a new collection of formulas is created (which could be an overlay); when updating a mutable collection of formulas, updates can occur in place.

```cs
public interface IUpdate
{
    public UpdateType UpdateType { get; }

    public IFormulaCollection Apply(IFormulaCollection formulas);
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
public interface IAction : IHasMetadata
{
    public IAskQuery Preconditions { get; }
    public IUpdate Effects { get; }
}
```

The inspectable preconditions of actions are represented by Boolean queries for collections of formulas, e.g., those formulas describing a state of a modelled world. The inspectable effects of actions are represented by updates for collections of formulas. By extending `IHasMetadata`, actions can have metadata.