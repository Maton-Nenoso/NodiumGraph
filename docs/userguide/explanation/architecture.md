# Architecture: Base Classes and Strategy Interfaces

NodiumGraph makes one peculiar design choice over and over: it uses **concrete, unsealed base classes for the model** and **interfaces for everything else**. `Node`, `Port`, `Connection`, and `Graph` are plain C# classes you can `new` up and subclass. Routing, validation, styling, port generation, and interaction handling are all interfaces with one or two small default implementations. This essay explains why the split exists and what it buys you.

## Two extension points, not one

Most graph libraries have a single extension model — either "inherit from `Node` and override twelve methods" or "implement `INode` from scratch, including the rendering pipeline". Both feel wrong in different ways.

Inheriting from a fat base class makes the easy case easy and the hard case almost impossible: every time the library changes the base, every subclass has to be re-vetted, and any subclass that overrides a core method is locked to the library's mental model of what a node *is*.

Implementing a full interface makes the hard case possible and the easy case brutal: a minimal "draggable box with two ports" turns into a four-hundred-line class nobody wants to maintain.

NodiumGraph splits the difference. The model is concrete and minimal — `Node` holds an id, a title, `X`/`Y`, a `PortProvider`, and a `Style`. Nothing else. Attaching domain data is one sentence: `public class MathNode : Node { public string Formula { get; set; } }`. The base class is small enough that staying compatible with future library versions is cheap.

Everything else — routing, validation, styling, port generation, interaction response — lives behind interfaces. Want orthogonal connections? Implement `IConnectionRouter`. Want a type lattice for validation? Implement `IConnectionValidator`. These are testable in isolation, swappable without touching the canvas, and they compose naturally — you can build a validator that wraps the default and layers your own rules on top.

## Why concrete for the model

Using an interface for `Node` would cost more than it saved. Every Avalonia `DataTemplate` resolves by runtime type (`DataType="local:MathNode"`). If `Node` were an interface, consumers would either lose type-keyed templates or end up with two parallel type hierarchies — the domain types on one side, the display types on the other, kept in sync by hand. The concrete-subclass pattern lets your domain object *be* the thing the canvas renders, which is usually what you want when "the thing the canvas renders" is a small visual that wraps a small data object.

The base class also earns its keep by handling the parts that are genuinely hard to get right: `INotifyPropertyChanged` plumbing, id generation, port-provider wiring, collection-change notifications on `Graph.Nodes` / `Graph.Connections`. You would write those the same way in every app — the library writes them once.

## Why interfaces for strategies

Routing and validation sit at the other extreme. Every NodiumGraph app has a `MathNode`, but only some apps want orthogonal routing, and the few that do all want it slightly differently. Making `IConnectionRouter` an interface means the library can ship a single default (`BezierRouter`) without assuming it's correct for your use case, and your custom implementation does not inherit any behaviour you'll have to override away.

Strategies also fit naturally into dependency injection and testing. A `DagValidator` that takes a `Graph` in its constructor is a plain class with a pure method; you can unit-test it with a handful of synthetic nodes and zero Avalonia machinery. A base class would drag in enough infrastructure to make that painful.

## Handlers: report, don't decide

Interactions use the same interface-based shape, but with different semantics. `INodeInteractionHandler.OnNodesMoved` and friends do not *perform* actions — they *report* that an action was requested. The handler decides whether to accept it, whether to write to the graph, whether to push an undo entry. The canvas never mutates the model on its own.

This is a load-bearing rule, important enough to have its own essay — see [Report, don't decide](report-dont-decide.md). It is also what makes the handler-as-interface pattern possible: if the canvas were mutating the graph itself, the handlers would be events, not responsibilities, and features like undo/redo and soft-delete would be much harder to layer on.

## Small surface area

The final piece of the architecture is what's *missing*. NodiumGraph ships no undo/redo, no layout engine, no serializer, no context menus, no keyboard shortcuts, no search, no connection validation *rules*. Each of those is an explicit non-goal because each of them would turn into a disagreement with every consumer's existing systems.

The cost is that your first few hours with the library are spent wiring things up. The benefit is that after those first hours you never fight the library about anything structural — if you need behaviour the library doesn't provide, you write it once, in your own code, and it never gets stepped on by the next version.

## In short

- **Concrete base classes** where consumers want to attach data (`Node`, `Port`, `Connection`, `Graph`).
- **Strategy interfaces** where consumers want to plug in algorithms (`IConnectionRouter`, `IConnectionValidator`, `IConnectionStyle`, `IPortProvider`).
- **Handler interfaces** where consumers want to *own* the response to user actions (`INodeInteractionHandler`, `IConnectionHandler`, `ISelectionHandler`, `ICanvasInteractionHandler`).
- **Deliberate absences** for anything that would overlap with an existing app concern.

You end up with a library that feels small — four model classes, four handler interfaces, four strategy interfaces, one canvas control — and a codebase that stays small even as your diagrams grow.
