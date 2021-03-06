# Contributing

Thank you for your interest in contributing. You may email Adam at [adamsobieski@hotmail.com](mailto:adamsobieski@hotmail.com) for more information.

# Roadmap

## Near-term Tasks

Here is a list of tasks that are being worked on:

1. Knowledge representation and reasoning. `Nifty.Knowledge.*`. It is important to get these models and interfaces right as they will be utilized throughout the framework.
2. Fluent n-ary SPARQL querying, query planning, and query optimization. `Nifty.Knowledge.Querying.*` and `Nifty.Knowledge.Querying.Planning.*`. Developers will be provided with "just works" access to the fluent querying of formula collections and datasets, e.g., objects' metadata and/or local or remote knowledgebases.
3. Extensibility. `Nifty.Extensibility.*`. Nifty will be an extensible framework with support for plug-ins, add-ons, and extensions.
4. Cloud computing. Nifty will support the development of scalable, cloud-based, adaptive instructional systems.
5. Messaging and events. Should the Nifty project utilize an existing message queueing service technology for `Nifty.Messaging` and `Nifty.Messaging.Events`? If so, which one? There are, for instance, [Azure messaging services](https://azure.microsoft.com/en-us/solutions/messaging-services/#products), [ActiveMQ](https://activemq.apache.org/components/nms/), and [other solutions](https://en.wikipedia.org/wiki/Message_queuing_service) to consider.
   - Could also create a new message queueing service technology, utilizing existing protocols, which supports "semantics-enhanced message filtering", using n-ary SPARQL queries on messages or their metadata.
6. Educational items, exercises, and activities. These can be components in .NET assemblies which are dynamically loaded and unloaded at runtime. Educational items, exercises, and activities in other formats, e.g., QTI, can be "compiled" for use with Nifty.
   - Per the [use cases](https://github.com/AdamSobieski/Nifty/blob/master/OVERVIEW.md#use-cases), scenarios for educational items, exercises, and activities include mathematics exercises, interactive stories, and software training exercises.
7. State-of-the-art speech recognition and speech synthesis. Will explore interoperation with [Coqui](https://github.com/coqui-ai/) speech-to-text and text-to-speech.
8. Implement learner, domain, pedagogical, and other models and modules.
9. Implement a rough-draft dialog system of an intelligent tutoring system in the [Microsoft Bot Framework](https://github.com/microsoft/botframework-sdk/) style.
10. Your ideas for contributions and tasks are welcomed!

# Have a Question, Comment, Idea or Discussion Topic?

Please feel free to make use of the [discussion area](https://github.com/AdamSobieski/Nifty/discussions).