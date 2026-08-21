# Indexed `[SignalParam]` — design

Date: 2026-08-21
Target: `Packages/FlowIoC` (submodule `github.com/FlowIoC/FlowIoC`)
Status: approved, ready for an implementation plan

## The problem

A command reads the payload of the signal that triggered it through
`[SignalParam]` properties. Today those properties are matched to payload values
**by type only**, so a signal that carries two values of the same type cannot be
read correctly.

```csharp
public Signal<int, int> Move = new();   // Dispatch(3, 7)

public class MoveCommand : Command
{
    [SignalParam] private int _x { get; set; }   // 3
    [SignalParam] private int _y { get; set; }   // 3  <- wrong, should be 7
}
```

The cause is in `Runtime/BaseModule/Injectable/Utils/InjectionExtensions.cs`.
`AssignSignalParameters` loops over the properties and calls
`TryGetSignalParameter`, which searches the payload from the start every time and
never removes what it already handed out:

```csharp
param = enumerable.FirstOrDefault(x => x != null && x.GetType() == paramType);
if (param == null)
    param = enumerable.FirstOrDefault(x => x != null && paramType.IsInstanceOfType(x));
```

Mediators are unaffected because `SignalConnector.Connect` binds an
`Action<T1, T2>` straight to the signal, so position is carried by the delegate
signature rather than being rediscovered from types.

Positional information is available at the point of failure — it is simply
discarded. The payload keeps its order the whole way down:
`Signal<T1,T2>.Dispatch` builds `new[] { param1, param2 }`, hands it to
`ISignalBody.InternalCallback`, `CommandBinder.InitializeGroupWithSignal` passes
it to `CommandGroupResolver.Initialize`, and the resolver forwards it to
`context.InjectCommand(command, _signalParameters)`.

Two further defects share the same code path:

- A dispatched `null` is reported as a missing parameter. `TryGetSignalParameter`
  signals success by returning a non-null value, so `Dispatch("a", null)` logs
  `Signal Param is not found!` for the second property even though the payload
  was well formed.
- The property list is cached but the attribute is not, so reading an index would
  cost a `GetCustomAttribute` call on every dispatch.

The documentation already describes the intended behaviour rather than the
implemented one. `Runtime/BaseModule/Controller/Documentation/Controller.md` says
`[SignalParam]` properties are filled "in order", and further down admits the
trap outright: "Two `[SignalParam]` properties of the same type in the wrong
order will bind without complaint."

## The chosen approach

`[SignalParam(n)]` selects the **n-th payload value of that property's type**,
counting from zero. An index is only needed on the properties that are ambiguous;
everything else keeps working untouched.

```csharp
public Signal<string, int, int> Damage = new();   // Dispatch("sword", 12, 3)

[SignalParam]    private string _weapon { get; set; }   // "sword"
[SignalParam(0)] private int    _amount { get; set; }   // 12   (first int)
[SignalParam(1)] private int    _crit   { get; set; }   // 3    (second int)
```

Because the index counts within a type, inserting a parameter of some *other*
type into the signal does not shift it. Changing the signal above to
`Signal<string, float, int, int>` leaves both int indices correct.

A property with no index takes the first value of its type that no other property
has claimed yet. Two same-type properties therefore resolve correctly with no
attribute argument at all:

```csharp
public Signal<int, int> Move = new();   // Dispatch(3, 7)

[SignalParam] private int _x { get; set; }   // 3
[SignalParam] private int _y { get; set; }   // 7
```

This is the one behavioural change for existing code: previously both properties
received `3`. No command inside the package relies on the old behaviour — the
eight `[SignalParam]` sites in `AssetModule`, `PoolModule` and `ScreenModule` all
declare distinct types.

Two alternatives were considered and rejected. An **absolute payload index**
(`[SignalParam(1)]` meaning `signalParams[1]`) reads one-to-one against the signal
declaration and handles `null` for free, but it puts two different resolution
rules in one class and every index shifts whenever the signal signature grows. A
**type-aware variant**, where `Signal<T1,T2>` passes `typeof(T1), typeof(T2)`
alongside the values, removes the residual `null` ambiguity described below, but
it requires changing all five signal classes and the internal callback signature
for a case that the null rule below already covers in practice.

## Resolution algorithm

For a given property type, the **candidate list** is the payload indices whose
value matches that type, in payload order.

```
CanHoldNull(t)        = !t.IsValueType || Nullable.GetUnderlyingType(t) != null
ExactMatch(v, t)      = v == null ? CanHoldNull(t) : v.GetType() == t
AssignableMatch(v, t) = v == null ? CanHoldNull(t) : t.IsInstanceOfType(v)

Candidates(t):
    exact = [i for i, v in values if ExactMatch(v, t)]
    return exact.Count > 0
        ? exact
        : [i for i, v in values if AssignableMatch(v, t)]
```

The exact pass runs first and the assignable pass is only consulted when the
exact pass finds nothing. This keeps the spirit of the current two-pass lookup
while making the two passes coherent with each other — today they are independent
`FirstOrDefault` calls, so a property could match one value in the first pass and
a different one in the second.

Resolution then runs in two phases over a `claimed` flag per payload slot:

```
Phase 1 — properties with an explicit index, in source order:
    c = Candidates(entry.Type)
    if entry.Index < 0 || entry.Index >= c.Count  -> log IndexOutOfRange; skip
    slot = c[entry.Index]
    if claimed[slot]                              -> log DuplicateClaim;   skip
    claimed[slot] = true; assign values[slot]

Phase 2 — properties with no index, in source order:
    c = Candidates(entry.Type)
    slot = first element of c with claimed[slot] == false
    if none                                       -> log NoFreeSlot; skip
    claimed[slot] = true; assign values[slot]
```

Indexed properties resolve first so that an unindexed property's result never
depends on where the indexed ones happen to sit in the file.

`HasIndex` distinguishes `[SignalParam]` from `[SignalParam(0)]`. The first means
"the next unclaimed value of my type"; the second means "candidate zero,
specifically". Given `Signal<int,int>`, a `[SignalParam(0)]` claims the first int
in phase 1 and a plain `[SignalParam] int` then receives the second.

### Why `null` works

A `null` counts as a candidate — in both passes — for any type that can hold
null. Without this, a null would drop out of the candidate list and shift every
subsequent index:

```csharp
public Signal<string, string> Rename = new();   // Dispatch(null, "b")

[SignalParam(0)] private string _from { get; set; }   // null
[SignalParam(1)] private string _to   { get; set; }   // "b"
```

Candidates for `string` are `[0, 1]`, so both properties land on the right slot
and neither logs an error. The existing `param != null` success test is removed;
"no slot at this index" and "the slot holds null" become distinct outcomes.

Ambiguity between two `null` slots of compatible types is harmless, because the
value assigned is `null` either way. The residual limitation is a payload that
mixes a base type with a null, such as `Signal<object, string>` dispatched as
`(null, "x")`, where both candidate lists become `[0, 1]`. The validation logging
below makes that visible rather than silent. The type-aware variant would have
removed it entirely and remains available as a follow-up.

## Attribute

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class SignalParamAttribute : Attribute
{
    public int  Index    { get; }
    public bool HasIndex { get; }

    public SignalParamAttribute()          { Index = 0; HasIndex = false; }
    public SignalParamAttribute(int index) { Index = index; HasIndex = true; }
}
```

Adding a constructor overload is additive; every existing `[SignalParam]` still
compiles and still binds to the same value.

## Deterministic property order

"Take the next unclaimed value" only holds if the property order is stable, and
today it is not. `GetInjectablePropertyInfoList` builds its type list from
`GetAllChildClasses` (`InjectionExtensions.cs:652`):

```csharp
Assembly.GetAssembly(type).GetTypes().Where(x => x.IsAssignableFrom(type) && !x.IsInterface)
```

This has three problems. `Assembly.GetTypes()` has no specified order, so the
base-before-derived grouping is accidental. Base classes living in another
assembly are never found, which is why a `[SignalParam]` on a base class in the
FlowIoC assembly is silently ignored by a command in a game module. And
`GetProperties` is called without `DeclaredOnly`, so a `public` inherited property
appears once for the base type and once for the derived type — under the new
consumption rule that duplicate would eat two payload slots instead of one.

The fix is a dedicated entry builder for `[SignalParam]` that walks the base-type
chain directly:

```csharp
var chain = new List<Type>();
for (Type t = commandType; t != null && t != typeof(object); t = t.BaseType)
    chain.Add(t);
chain.Reverse();                       // most-base first

// per type in chain:
t.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic)
 .Where(p => p.GetCustomAttribute<SignalParamAttribute>() != null)
 .OrderBy(p => p.MetadataToken)        // source order within a type
```

`GetAllChildClasses` itself is left alone. Correcting it would also change how
`[Inject]` and `[InjectSignal]` discover properties: base-class properties that
are silently skipped today would start being injected, which can surface fresh
`Injection value is null!` errors in consumer projects. That is a separate change
with its own risk profile.

## Diagnostics

`LogSignalParamError` is replaced by three specific messages. Each names the
command, the property, the property type and the payload shape.

- **IndexOutOfRange** — `[SignalParam(3)] int _crit` on a payload holding only two
  `Int32` values.
- **DuplicateClaim** — `_crit` asks for slot 2, which `_amount` already took.
- **NoFreeSlot** — no unclaimed `Int32` remains for `_y`; two candidates existed
  and both were consumed.

This closes the "will bind without complaint" trap named in `Controller.md`.

## Caching

Signal parameter entries are built once per command type and stored beside the
existing property caches:

```csharp
private class CachedInjectableData
{
    public Dictionary<Type, List<FieldInfo>>    CachedFieldInfoList    = new();
    public Dictionary<Type, List<PropertyInfo>> CachedPropertyInfoList = new();
    public List<SignalParamEntry>               SignalParamEntries;      // new
}

internal readonly struct SignalParamEntry
{
    public readonly PropertyInfo Property;
    public readonly Type         Type;
    public readonly int          Index;
    public readonly bool         HasIndex;
}
```

No reflection runs per dispatch. Within a single dispatch, candidate lists are
memoised per property type so a command with several properties of one type
scans the payload once.

## Placement

The algorithm moves into an instance class, `SignalParamResolver`, rather than
growing the static `InjectionExtensions`. `InjectionExtensions` holds one
resolver instance and delegates to it. This adds no new static state and makes
the resolver testable on its own.

## Files

| File | Change |
|---|---|
| `Runtime/BaseModule/Injectable/Attributes/SignalParamAttribute.cs` | `Index` / `HasIndex`, indexed constructor |
| `Runtime/BaseModule/Injectable/Utils/SignalParamResolver.cs` | new — algorithm, entry builder, diagnostics |
| `Runtime/BaseModule/Injectable/Utils/InjectionExtensions.cs` | drop `AssignSignalParameters` and `TryGetSignalParameter`, delegate to the resolver, add the entry cache |
| `README.md` | "Reading signal parameters" section |
| `Runtime/BaseModule/Controller/Documentation/Controller.md` | payload section, and the troubleshooting entry that documented the bug |
| `CHANGELOG.md` | Added / Fixed |
| `package.json` | `1.0.1` → `1.1.0` |

## Verification

The package has no test assembly. One is added at `Tests/Editor/` with a
`FlowIoC.Tests.asmdef`, running in EditMode — the resolver is pure reflection and
needs no play mode.

Cases:

- `Signal<int,int>` with explicit indices
- `Signal<int,int>` with no indices, relying on consumption
- `Signal<string,int,int>` mixing an unindexed and two indexed properties
- `Dispatch("a", null)` — no false "not found"
- `Dispatch(null, "b")` with explicit indices — both land correctly
- index beyond the candidate count
- two properties claiming the same slot
- `[SignalParam(0)]` alongside a plain `[SignalParam]` of the same type
- properties inherited from a base class, order preserved
- a single property of a unique type — regression against current behaviour
- an interface-typed property such as `IContext`, exercising the assignable pass

## Out of scope

`Command<T1>` used as the first step of a binding does not receive the signal
payload in its typed `Execute`. `CommandGroupResolver.Initialize` calls
`CheckExecuteNextStep()` with no arguments, so the first step is invoked with an
empty array and `InvokeCommandExecute` logs `Signature mismatch for Execute`. The
typed `Execute` is only fed by bind-time parameters or by the previous step's
`Release(...)`. Feeding the signal payload into it would open a second, fully
positional way to read a signal, but it interacts with the `Release` chain and
needs its own design. Tracked separately.
