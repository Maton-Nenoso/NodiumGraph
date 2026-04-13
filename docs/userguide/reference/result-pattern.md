# Result Pattern Reference

NodiumGraph uses a lightweight `Result` pattern for handler return values where operations can fail without the failure being exceptional. The only place in the public API where this currently appears is `IConnectionHandler.OnConnectionRequested`, but the three types (`Error`, `Result`, `Result<T>`) are general-purpose and safe to reuse in your own code. They all live in the root `NodiumGraph` namespace.

## Error

Namespace: `NodiumGraph`

```csharp
public record Error(string Message, string? Code = null);
```

An opaque failure record. `Message` is user-visible prose; `Code` is an optional machine-readable token your application can switch on. Because `Error` is a record, value equality and `with`-expressions are available for free.

## Result

Namespace: `NodiumGraph`

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error);

    public static Result Success();
    public static Result Failure(Error error);

    public static implicit operator Result(Error error);
}
```

### Members

| Member | Description |
|---|---|
| `IsSuccess` | `true` if the result is a success. |
| `Error` | The error on failure; `null` on success. |
| `Result.Success()` | Static factory for the success case. |
| `Result.Failure(Error error)` | Static factory for the failure case. Throws `ArgumentNullException` if `error` is `null`. |
| implicit `Error → Result` | Write `return new Error("...");` from a method returning `Result`. |

## Result\<T\>

Namespace: `NodiumGraph`

```csharp
public class Result<T> : Result
{
    public T? Value { get; }

    public static implicit operator Result<T>(T value);
    public static implicit operator Result<T>(Error error);
}
```

### Members

| Member | Description |
|---|---|
| `Value` | The success value when `IsSuccess` is `true`; `null` on failure. |
| implicit `T → Result<T>` | Write `return myValue;` from a method returning `Result<T>`. Throws `ArgumentNullException` if `value` is `null`. |
| implicit `Error → Result<T>` | Write `return new Error("...");` from the same method. Throws `ArgumentNullException` if `error` is `null`. |

Both constructors are private — the implicit operators are the intended construction path. `Result<T>` inherits `IsSuccess` and `Error` from `Result`.

## Usage patterns

### Returning a success

```csharp
// from: samples/GettingStarted/MainWindow.axaml.cs
public Result<Connection> OnConnectionRequested(Port source, Port target)
{
    var connection = new Connection(source, target);
    graph.AddConnection(connection);
    return connection; // implicit Connection → Result<Connection>
}
```

### Returning a failure

```csharp
// Example — not from the repo.
public Result<Connection> OnConnectionRequested(Port source, Port target)
{
    if (graph.Connections.Count >= 100)
        return new Error("Graph is full", Code: "GRAPH_FULL");

    var connection = new Connection(source, target);
    graph.AddConnection(connection);
    return connection;
}
```

## Library behavior

`NodiumGraphCanvas` inspects only `Result.IsSuccess` when processing a returned `Result<Connection>` from `IConnectionHandler.OnConnectionRequested`. It never reads `Value` and never adds the returned connection to the graph on your behalf — accepting the result means "the handler took care of it". See the [handlers reference](handlers.md#onconnectionrequestedsource-port-target-port--resultconnection).

## Anti-patterns

- **Don't throw exceptions for expected failures.** The `Result` pattern exists specifically to avoid this. Exceptions should signal bugs or broken invariants, not "the user tried to connect ports with mismatched `DataType`s."
- **Don't return `null`** from methods that declare `Result<T>`. Use `new Error(...)` instead. The implicit operators forbid `null` on both sides.
- **Don't read `Value` without checking `IsSuccess` first.** On failure, `Value` is `null` — accessing it is the C# equivalent of dereferencing a failed optional.

## See also

- [Handler interfaces reference](handlers.md) — where `Result<Connection>` appears in the API
