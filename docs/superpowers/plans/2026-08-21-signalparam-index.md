# Indexed `[SignalParam]` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a command read several same-typed values out of one signal payload by writing `[SignalParam(n)]`, where `n` counts within that property's type.

**Architecture:** A new instance class `SignalParamResolver` replaces the type-only lookup in `InjectionExtensions`. It builds a candidate slot list per property type, assigns explicitly indexed properties first, then lets unindexed ones consume the next free slot. It returns diagnostics instead of logging, so it stays free of Unity dependencies and is testable in EditMode. `Context` owns one resolver instance; property entries are cached per command type.

**Tech Stack:** Unity 6000.3.19f1, C# 9, `com.unity.test-framework` 1.6.0 (NUnit), Unity Test Runner in EditMode.

**Spec:** `docs/superpowers/specs/2026-08-21-signalparam-index-design.md`

## Global Constraints

- Two repositories are in play. `Packages/FlowIoC` is a git submodule pointing at `github.com/FlowIoC/FlowIoC`; everything else belongs to the template repository at the workspace root. Package changes are committed inside `Packages/FlowIoC`; the template repository only records the submodule pointer and its own `Packages/manifest.json`.
- Commit messages carry **no** `Co-Authored-By` trailer. Commits must be authored as `CNYT <cuneyt.taskent@gmail.com>` — already configured in both repositories, verify with `git config user.email` before the first commit.
- Commit message style follows the existing history: a plain imperative sentence, no `feat:` / `fix:` prefix.
- Work happens on the branch `signalparam-index` in both repositories. The template repository is already on it.
- Every asset in the package must ship with a committed `.meta` file. Files created outside the editor only get one after Unity imports them, so each task that adds a file ends by letting Unity import and committing the generated `.meta` alongside.
- The package's existing `[SignalParam]` sites must keep binding to the same values. They are in `Runtime/AssetModule/Commands/`, `Runtime/PoolModule/Controllers/` and `Runtime/ScreenModule/Commands/`; all declare distinct types, so none of them changes behaviour.
- `FlowLogger.LogError` is `[Conditional("ENABLE_LOG")]` and calls `Debug.LogError`, which fails an EditMode test. Never assert on console output — assert on `SignalParamResolver.Diagnostics` instead, and keep logging out of the resolver.
- Target package version after this work: `1.1.0`.

---

### Task 1: EditMode test harness

Nothing in the package is testable today. This task makes the harness exist and proves it runs, so every later task can end with a real test cycle.

**Files:**
- Create: `Packages/FlowIoC/Tests/Editor/FlowIoC.Tests.asmdef`
- Create: `Packages/FlowIoC/Tests/Editor/HarnessSmokeTests.cs`
- Modify: `Packages/FlowIoC/Runtime/AssemblyInfo.cs`
- Modify: `Packages/manifest.json`

**Interfaces:**
- Consumes: nothing.
- Produces: an assembly named `FlowIoC.Tests` in namespace `FlowIoC.Tests`, with access to `internal` types of the `FlowIoC` assembly. Every later task adds its test file to `Packages/FlowIoC/Tests/Editor/`.

- [ ] **Step 1: Create the branch in the submodule**

```bash
cd Packages/FlowIoC
git checkout -b signalparam-index
git config user.email    # expect cuneyt.taskent@gmail.com
```

- [ ] **Step 2: Write the test assembly definition**

Create `Packages/FlowIoC/Tests/Editor/FlowIoC.Tests.asmdef`:

```json
{
    "name": "FlowIoC.Tests",
    "rootNamespace": "FlowIoC.Tests",
    "references": [
        "FlowIoC",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: Grant the test assembly access to internals**

`Packages/FlowIoC/Runtime/AssemblyInfo.cs` currently ends with a single `InternalsVisibleTo`. Replace the whole file with:

```csharp
using System.Runtime.CompilerServices;
using UnityEngine.Scripting;

[assembly: Preserve]
[assembly: AlwaysLinkAssembly]
[assembly: InternalsVisibleTo("FlowIoC.Editor")]
[assembly: InternalsVisibleTo("FlowIoC.Tests")]
```

- [ ] **Step 4: Make the Test Runner look inside the package**

Unity only discovers tests in packages listed under `testables`. Add the key to `Packages/manifest.json`, as a sibling of `dependencies` (keep the existing `dependencies` block exactly as it is):

```json
  "testables": [
    "com.flowioc.core"
  ]
```

- [ ] **Step 5: Write the smoke test**

Create `Packages/FlowIoC/Tests/Editor/HarnessSmokeTests.cs`:

```csharp
using FlowIoC.BaseModule.Injectable.Attributes;
using NUnit.Framework;

namespace FlowIoC.Tests
{
    public class HarnessSmokeTests
    {
        [Test]
        public void The_test_assembly_can_see_the_FlowIoC_runtime_assembly()
        {
            Assert.That(typeof(SignalParamAttribute).Assembly.GetName().Name,
                Is.EqualTo("FlowIoC"));
        }
    }
}
```

- [ ] **Step 6: Let Unity import the new files**

If the Unity editor MCP server is connected, load the tool schemas and recompile:

```
ToolSearch("select:mcp__unity-editor-mcp__recompile,mcp__unity-editor-mcp__run_tests,mcp__unity-editor-mcp__console")
```

then call `mcp__unity-editor-mcp__recompile`. Otherwise focus the Unity editor window so it imports, and watch the Console for compile errors.

Expected: no compile errors. `Packages/FlowIoC/Tests/Editor.meta`, `Tests.meta`, `FlowIoC.Tests.asmdef.meta` and `HarnessSmokeTests.cs.meta` now exist.

- [ ] **Step 7: Run the test**

Run `mcp__unity-editor-mcp__run_tests` with EditMode mode and filter `FlowIoC.Tests`. Manual fallback: Window > General > Test Runner > EditMode > Run All.

Expected: `HarnessSmokeTests.The_test_assembly_can_see_the_FlowIoC_runtime_assembly` PASSES. If the test does not appear at all, `testables` in Step 4 did not take effect — reopen the project.

- [ ] **Step 8: Commit both repositories**

```bash
cd Packages/FlowIoC
git add Tests Runtime/AssemblyInfo.cs
git commit -m "Add an EditMode test assembly for the package"
cd ../..
git add Packages/manifest.json
git commit -m "Register FlowIoC as a testable package"
```

---

### Task 2: An index on `[SignalParam]`

**Files:**
- Modify: `Packages/FlowIoC/Runtime/BaseModule/Injectable/Attributes/SignalParamAttribute.cs`
- Test: `Packages/FlowIoC/Tests/Editor/SignalParamAttributeTests.cs`

**Interfaces:**
- Consumes: the test assembly from Task 1.
- Produces: `SignalParamAttribute` with `public int Index { get; }` and `public bool HasIndex { get; }`, a parameterless constructor leaving `HasIndex == false`, and a `SignalParamAttribute(int index)` constructor setting `HasIndex == true`.

- [ ] **Step 1: Write the failing test**

Create `Packages/FlowIoC/Tests/Editor/SignalParamAttributeTests.cs`:

```csharp
using FlowIoC.BaseModule.Injectable.Attributes;
using NUnit.Framework;

namespace FlowIoC.Tests
{
    public class SignalParamAttributeTests
    {
        [Test]
        public void A_bare_attribute_carries_no_index()
        {
            var attribute = new SignalParamAttribute();

            Assert.That(attribute.HasIndex, Is.False);
            Assert.That(attribute.Index, Is.EqualTo(0));
        }

        [Test]
        public void An_indexed_attribute_carries_its_index()
        {
            var attribute = new SignalParamAttribute(2);

            Assert.That(attribute.HasIndex, Is.True);
            Assert.That(attribute.Index, Is.EqualTo(2));
        }
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Run the EditMode tests filtered to `FlowIoC.Tests.SignalParamAttributeTests`.
Expected: compile error — `SignalParamAttribute` does not contain a definition for `HasIndex`, and no constructor takes one argument.

- [ ] **Step 3: Implement the attribute**

Replace `Packages/FlowIoC/Runtime/BaseModule/Injectable/Attributes/SignalParamAttribute.cs` with:

```csharp
using System;

namespace FlowIoC.BaseModule.Injectable.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SignalParamAttribute : Attribute
    {
        /// <summary>
        /// Which value of this property's own type to take from the signal payload,
        /// counting from zero. Only meaningful when <see cref="HasIndex"/> is true.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// False for <c>[SignalParam]</c>, which takes the next value of its type that
        /// no other property has claimed. True for <c>[SignalParam(n)]</c>, which takes
        /// the n-th value of its type whether or not anything else wanted it.
        /// </summary>
        public bool HasIndex { get; }

        public SignalParamAttribute()
        {
            Index = 0;
            HasIndex = false;
        }

        public SignalParamAttribute(int index)
        {
            Index = index;
            HasIndex = true;
        }
    }
}
```

- [ ] **Step 4: Run the tests and make sure they pass**

Expected: both tests PASS, and every existing `[SignalParam]` in the package still compiles because the parameterless constructor is unchanged.

- [ ] **Step 5: Commit**

```bash
cd Packages/FlowIoC
git add Runtime/BaseModule/Injectable/Attributes/SignalParamAttribute.cs Tests/Editor/SignalParamAttributeTests.cs
git commit -m "Let [SignalParam] carry an index"
```

---

### Task 3: Deterministic entry list

The resolver needs the annotated properties in a stable, source-declaration order — the existing `GetInjectablePropertyInfoList` cannot supply one. See the spec section "Deterministic property order" for why `GetAllChildClasses` is left alone.

**Files:**
- Create: `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamEntry.cs`
- Create: `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamEntryBuilder.cs`
- Test: `Packages/FlowIoC/Tests/Editor/SignalParamEntryBuilderTests.cs`

**Interfaces:**
- Consumes: `SignalParamAttribute.Index` / `.HasIndex` from Task 2.
- Produces:
  - `internal readonly struct SignalParamEntry` with fields `PropertyInfo Property`, `Type Type`, `int Index`, `bool HasIndex`, and constructor `SignalParamEntry(PropertyInfo property, int index, bool hasIndex)`.
  - `internal sealed class SignalParamEntryBuilder` with `List<SignalParamEntry> Build(Type targetType)`.

- [ ] **Step 1: Write the failing test**

Create `Packages/FlowIoC/Tests/Editor/SignalParamEntryBuilderTests.cs`:

```csharp
using FlowIoC.BaseModule.Injectable.Attributes;
using FlowIoC.BaseModule.Injectable.Utils;
using NUnit.Framework;

namespace FlowIoC.Tests
{
    public class SignalParamEntryBuilderTests
    {
        private class BaseTarget
        {
            [SignalParam] private string _first { get; set; }
            [SignalParam] private string _second { get; set; }
        }

        private class DerivedTarget : BaseTarget
        {
            [SignalParam(2)] private int _third { get; set; }
            private int _ignored { get; set; }
        }

        private class VirtualTarget
        {
            [SignalParam] protected virtual string Value { get; set; }
        }

        private class OverridingTarget : VirtualTarget
        {
            protected override string Value { get; set; }
        }

        [Test]
        public void Build_lists_base_properties_before_derived_ones_in_source_order()
        {
            var entries = new SignalParamEntryBuilder().Build(typeof(DerivedTarget));

            CollectionAssert.AreEqual(
                new[] { "_first", "_second", "_third" },
                entries.ConvertAll(entry => entry.Property.Name));
        }

        [Test]
        public void Build_skips_properties_without_the_attribute()
        {
            var entries = new SignalParamEntryBuilder().Build(typeof(DerivedTarget));

            Assert.That(entries.Exists(entry => entry.Property.Name == "_ignored"), Is.False);
        }

        [Test]
        public void Build_records_whether_an_index_was_written()
        {
            var entries = new SignalParamEntryBuilder().Build(typeof(DerivedTarget));

            SignalParamEntry first = entries.Find(entry => entry.Property.Name == "_first");
            SignalParamEntry third = entries.Find(entry => entry.Property.Name == "_third");

            Assert.That(first.HasIndex, Is.False);
            Assert.That(first.Index, Is.EqualTo(0));
            Assert.That(third.HasIndex, Is.True);
            Assert.That(third.Index, Is.EqualTo(2));
        }

        [Test]
        public void Build_records_the_property_type()
        {
            var entries = new SignalParamEntryBuilder().Build(typeof(DerivedTarget));

            Assert.That(entries.Find(entry => entry.Property.Name == "_third").Type,
                Is.EqualTo(typeof(int)));
        }

        [Test]
        public void Build_records_an_overridden_property_once()
        {
            var entries = new SignalParamEntryBuilder().Build(typeof(OverridingTarget));

            Assert.That(entries.Count, Is.EqualTo(1));
            Assert.That(entries[0].Property.DeclaringType, Is.EqualTo(typeof(VirtualTarget)));
        }

        [Test]
        public void Build_returns_an_empty_list_for_a_null_type()
        {
            Assert.That(new SignalParamEntryBuilder().Build(null), Is.Empty);
        }
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Expected: compile error — the type or namespace `SignalParamEntryBuilder` does not exist.

- [ ] **Step 3: Write the entry struct**

Create `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamEntry.cs`:

```csharp
using System;
using System.Reflection;

namespace FlowIoC.BaseModule.Injectable.Utils
{
    /// <summary>
    /// One <c>[SignalParam]</c> property of a command, with the index written on it.
    /// Built once per command type and cached.
    /// </summary>
    internal readonly struct SignalParamEntry
    {
        public readonly PropertyInfo Property;
        public readonly Type Type;
        public readonly int Index;
        public readonly bool HasIndex;

        public SignalParamEntry(PropertyInfo property, int index, bool hasIndex)
        {
            Property = property;
            Type = property.PropertyType;
            Index = index;
            HasIndex = hasIndex;
        }
    }
}
```

- [ ] **Step 4: Write the builder**

Create `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamEntryBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using FlowIoC.BaseModule.Injectable.Attributes;

namespace FlowIoC.BaseModule.Injectable.Utils
{
    /// <summary>
    /// Collects the <c>[SignalParam]</c> properties of a type in a stable order:
    /// most-base class first, and within a class, source declaration order. The order
    /// matters because an unindexed property takes the next payload slot no other
    /// property has claimed.
    /// </summary>
    internal sealed class SignalParamEntryBuilder
    {
        private const BindingFlags DeclaredMembers =
            BindingFlags.DeclaredOnly | BindingFlags.Instance |
            BindingFlags.Public | BindingFlags.NonPublic;

        public List<SignalParamEntry> Build(Type targetType)
        {
            var entries = new List<SignalParamEntry>();
            if (targetType == null)
                return entries;

            var chain = new List<Type>();
            for (Type type = targetType; type != null && type != typeof(object); type = type.BaseType)
                chain.Add(type);
            chain.Reverse();

            var seenAccessors = new HashSet<MethodInfo>();

            foreach (Type type in chain)
            {
                PropertyInfo[] properties = type.GetProperties(DeclaredMembers);
                Array.Sort(properties, CompareByMetadataToken);

                foreach (PropertyInfo property in properties)
                {
                    var attribute = property.GetCustomAttribute<SignalParamAttribute>(false);
                    if (attribute == null)
                        continue;

                    // An override re-declares a property the base class already gave us.
                    // Record it once, at the declaration that carries the attribute.
                    MethodInfo accessor = property.GetMethod ?? property.SetMethod;
                    MethodInfo declaration = accessor?.GetBaseDefinition() ?? accessor;
                    if (declaration != null && !seenAccessors.Add(declaration))
                        continue;

                    entries.Add(new SignalParamEntry(property, attribute.Index, attribute.HasIndex));
                }
            }

            return entries;
        }

        private int CompareByMetadataToken(PropertyInfo left, PropertyInfo right)
            => left.MetadataToken.CompareTo(right.MetadataToken);
    }
}
```

- [ ] **Step 5: Run the tests and make sure they pass**

Run the EditMode tests filtered to `FlowIoC.Tests.SignalParamEntryBuilderTests`.
Expected: all six PASS. If `Build_lists_base_properties_before_derived_ones_in_source_order` fails, the `MetadataToken` sort is not producing source order on this compiler — report it rather than reordering the test, because the whole unindexed-consumption rule rests on it.

- [ ] **Step 6: Let Unity import and commit**

Recompile so the `.meta` files appear, then:

```bash
cd Packages/FlowIoC
git add Runtime/BaseModule/Injectable/Utils/SignalParamEntry.cs \
        Runtime/BaseModule/Injectable/Utils/SignalParamEntry.cs.meta \
        Runtime/BaseModule/Injectable/Utils/SignalParamEntryBuilder.cs \
        Runtime/BaseModule/Injectable/Utils/SignalParamEntryBuilder.cs.meta \
        Tests/Editor/SignalParamEntryBuilderTests.cs \
        Tests/Editor/SignalParamEntryBuilderTests.cs.meta
git commit -m "Collect [SignalParam] properties in a stable declaration order"
```

---

### Task 4: Candidate slot matching

**Files:**
- Create: `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamCandidateFinder.cs`
- Test: `Packages/FlowIoC/Tests/Editor/SignalParamCandidateFinderTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `internal sealed class SignalParamCandidateFinder` with `List<int> Find(Type type, object[] values)` and `bool CanHoldNull(Type type)`.

- [ ] **Step 1: Write the failing test**

Create `Packages/FlowIoC/Tests/Editor/SignalParamCandidateFinderTests.cs`:

```csharp
using System.Collections.Generic;
using FlowIoC.BaseModule.Injectable.Utils;
using NUnit.Framework;

namespace FlowIoC.Tests
{
    public class SignalParamCandidateFinderTests
    {
        private class SubList : List<string> { }

        private SignalParamCandidateFinder _finder;

        [SetUp]
        public void SetUp() => _finder = new SignalParamCandidateFinder();

        [Test]
        public void Find_returns_every_slot_of_the_exact_type_in_payload_order()
        {
            List<int> candidates = _finder.Find(typeof(int), new object[] { "sword", 12, 3 });

            CollectionAssert.AreEqual(new[] { 1, 2 }, candidates);
        }

        [Test]
        public void Find_falls_back_to_assignable_slots_when_nothing_matches_exactly()
        {
            var payload = new object[] { new List<string>(), "text" };

            CollectionAssert.AreEqual(new[] { 0 },
                _finder.Find(typeof(IEnumerable<string>), payload));
        }

        [Test]
        public void Find_prefers_exact_matches_over_assignable_ones()
        {
            var payload = new object[] { new SubList(), new List<string>() };

            CollectionAssert.AreEqual(new[] { 1 }, _finder.Find(typeof(List<string>), payload));
        }

        [Test]
        public void Find_counts_null_as_a_candidate_for_a_reference_type()
        {
            CollectionAssert.AreEqual(new[] { 0, 1 },
                _finder.Find(typeof(string), new object[] { null, "b" }));
        }

        [Test]
        public void Find_does_not_count_null_for_a_non_nullable_value_type()
        {
            CollectionAssert.AreEqual(new[] { 1 },
                _finder.Find(typeof(int), new object[] { null, 5 }));
        }

        [Test]
        public void Find_matches_a_boxed_value_against_a_nullable_property_type()
        {
            CollectionAssert.AreEqual(new[] { 0, 1 },
                _finder.Find(typeof(int?), new object[] { 5, null }));
        }

        [Test]
        public void Find_returns_an_empty_list_for_an_empty_payload()
        {
            Assert.That(_finder.Find(typeof(int), new object[0]), Is.Empty);
        }
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Expected: compile error — the type or namespace `SignalParamCandidateFinder` does not exist.

- [ ] **Step 3: Write the finder**

Create `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamCandidateFinder.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FlowIoC.BaseModule.Injectable.Utils
{
    /// <summary>
    /// Works out which slots of a dispatched signal payload can supply a value for a
    /// given property type, in payload order.
    /// </summary>
    internal sealed class SignalParamCandidateFinder
    {
        private readonly List<int> _exact = new List<int>();
        private readonly List<int> _assignable = new List<int>();

        /// <summary>
        /// Exact type matches win outright; the assignable pass is consulted only when
        /// nothing matched exactly. A null counts in both passes for any type that can
        /// hold null, so a dispatched null does not shift the slots that follow it.
        /// </summary>
        public List<int> Find(Type type, object[] values)
        {
            var candidates = new List<int>();
            if (type == null || values == null)
                return candidates;

            _exact.Clear();
            _assignable.Clear();

            // A boxed int arrives as Int32, never as Nullable<Int32>, so an int?
            // property has to be matched against its underlying type.
            Type effective = Nullable.GetUnderlyingType(type) ?? type;
            bool acceptsNull = CanHoldNull(type);

            for (int i = 0; i < values.Length; i++)
            {
                object value = values[i];

                if (value == null)
                {
                    if (acceptsNull)
                    {
                        _exact.Add(i);
                        _assignable.Add(i);
                    }
                    continue;
                }

                if (value.GetType() == effective)
                    _exact.Add(i);

                if (effective.IsInstanceOfType(value))
                    _assignable.Add(i);
            }

            candidates.AddRange(_exact.Count > 0 ? _exact : _assignable);
            return candidates;
        }

        public bool CanHoldNull(Type type)
            => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
}
```

- [ ] **Step 4: Run the tests and make sure they pass**

Run the EditMode tests filtered to `FlowIoC.Tests.SignalParamCandidateFinderTests`.
Expected: all seven PASS.

- [ ] **Step 5: Let Unity import and commit**

```bash
cd Packages/FlowIoC
git add Runtime/BaseModule/Injectable/Utils/SignalParamCandidateFinder.cs \
        Runtime/BaseModule/Injectable/Utils/SignalParamCandidateFinder.cs.meta \
        Tests/Editor/SignalParamCandidateFinderTests.cs \
        Tests/Editor/SignalParamCandidateFinderTests.cs.meta
git commit -m "Match signal payload slots to a property type"
```

---

### Task 5: Two-phase resolver

**Files:**
- Create: `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamDiagnostic.cs`
- Create: `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamResolver.cs`
- Test: `Packages/FlowIoC/Tests/Editor/SignalParamResolverTests.cs`

**Interfaces:**
- Consumes: `SignalParamEntry` and `SignalParamEntryBuilder.Build` from Task 3, `SignalParamCandidateFinder.Find` from Task 4.
- Produces:
  - `internal enum SignalParamDiagnosticKind { IndexOutOfRange, DuplicateClaim, NoFreeSlot }`
  - `internal readonly struct SignalParamDiagnostic` with fields `Kind`, `TargetType`, `PropertyName`, `PropertyType`, `RequestedIndex`, `CandidateCount`, `ClaimedCount`.
  - `internal sealed class SignalParamResolver` with `void Resolve(object target, IReadOnlyList<SignalParamEntry> entries, object[] values)` and `IReadOnlyList<SignalParamDiagnostic> Diagnostics { get; }`.

- [ ] **Step 1: Write the failing test**

Create `Packages/FlowIoC/Tests/Editor/SignalParamResolverTests.cs`:

```csharp
using FlowIoC.BaseModule.Contexts;
using FlowIoC.BaseModule.Injectable.Attributes;
using FlowIoC.BaseModule.Injectable.Utils;
using NUnit.Framework;

namespace FlowIoC.Tests
{
    public class SignalParamResolverTests
    {
        private SignalParamResolver _resolver;
        private SignalParamEntryBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _resolver = new SignalParamResolver();
            _builder = new SignalParamEntryBuilder();
        }

        private void Resolve(object target, params object[] values)
            => _resolver.Resolve(target, _builder.Build(target.GetType()), values);

        private class IndexedInts
        {
            [SignalParam(0)] private int _x { get; set; }
            [SignalParam(1)] private int _y { get; set; }
            public int X => _x;
            public int Y => _y;
        }

        private class UnindexedInts
        {
            [SignalParam] private int _x { get; set; }
            [SignalParam] private int _y { get; set; }
            public int X => _x;
            public int Y => _y;
        }

        private class MixedPayload
        {
            [SignalParam] private string _weapon { get; set; }
            [SignalParam(0)] private int _amount { get; set; }
            [SignalParam(1)] private int _crit { get; set; }
            public string Weapon => _weapon;
            public int Amount => _amount;
            public int Crit => _crit;
        }

        private class ExplicitThenImplicit
        {
            [SignalParam(0)] private int _first { get; set; }
            [SignalParam] private int _next { get; set; }
            public int First => _first;
            public int Next => _next;
        }

        private class TwoStrings
        {
            [SignalParam(0)] private string _from { get; set; }
            [SignalParam(1)] private string _to { get; set; }
            public string From => _from;
            public string To => _to;
        }

        private class IndexTooHigh
        {
            [SignalParam(3)] private int _crit { get; set; }
        }

        private class SameSlotTwice
        {
            [SignalParam(0)] private int _a { get; set; }
            [SignalParam(0)] private int _b { get; set; }
        }

        private class WantsContext
        {
            [SignalParam] private IContext _context { get; set; }
            public IContext Context => _context;
        }

        [Test]
        public void Indexed_properties_of_one_type_take_distinct_slots()
        {
            var target = new IndexedInts();
            Resolve(target, 3, 7);

            Assert.That(target.X, Is.EqualTo(3));
            Assert.That(target.Y, Is.EqualTo(7));
            Assert.That(_resolver.Diagnostics, Is.Empty);
        }

        [Test]
        public void Unindexed_properties_consume_the_payload_in_declaration_order()
        {
            var target = new UnindexedInts();
            Resolve(target, 3, 7);

            Assert.That(target.X, Is.EqualTo(3));
            Assert.That(target.Y, Is.EqualTo(7));
        }

        [Test]
        public void An_index_counts_within_its_own_type_not_across_the_payload()
        {
            var target = new MixedPayload();
            Resolve(target, "sword", 12, 3);

            Assert.That(target.Weapon, Is.EqualTo("sword"));
            Assert.That(target.Amount, Is.EqualTo(12));
            Assert.That(target.Crit, Is.EqualTo(3));
            Assert.That(_resolver.Diagnostics, Is.Empty);
        }

        [Test]
        public void An_unindexed_property_skips_a_slot_an_indexed_one_claimed()
        {
            var target = new ExplicitThenImplicit();
            Resolve(target, 3, 7);

            Assert.That(target.First, Is.EqualTo(3));
            Assert.That(target.Next, Is.EqualTo(7));
        }

        [Test]
        public void A_dispatched_null_binds_without_a_diagnostic()
        {
            var target = new TwoStrings();
            Resolve(target, null, "b");

            Assert.That(target.From, Is.Null);
            Assert.That(target.To, Is.EqualTo("b"));
            Assert.That(_resolver.Diagnostics, Is.Empty);
        }

        [Test]
        public void An_index_beyond_the_candidate_count_reports_IndexOutOfRange()
        {
            Resolve(new IndexTooHigh(), "sword", 12);

            Assert.That(_resolver.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(_resolver.Diagnostics[0].Kind,
                Is.EqualTo(SignalParamDiagnosticKind.IndexOutOfRange));
            Assert.That(_resolver.Diagnostics[0].CandidateCount, Is.EqualTo(1));
            Assert.That(_resolver.Diagnostics[0].RequestedIndex, Is.EqualTo(3));
        }

        [Test]
        public void Two_properties_claiming_one_slot_report_DuplicateClaim()
        {
            Resolve(new SameSlotTwice(), 3, 7);

            Assert.That(_resolver.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(_resolver.Diagnostics[0].Kind,
                Is.EqualTo(SignalParamDiagnosticKind.DuplicateClaim));
        }

        [Test]
        public void An_unindexed_property_with_no_free_slot_reports_NoFreeSlot()
        {
            var target = new UnindexedInts();
            Resolve(target, 3);

            Assert.That(target.X, Is.EqualTo(3));
            Assert.That(_resolver.Diagnostics.Count, Is.EqualTo(1));
            Assert.That(_resolver.Diagnostics[0].Kind,
                Is.EqualTo(SignalParamDiagnosticKind.NoFreeSlot));
            Assert.That(_resolver.Diagnostics[0].PropertyName, Is.EqualTo("_y"));
        }

        [Test]
        public void An_interface_typed_property_binds_through_the_assignable_pass()
        {
            var context = new Context();
            var target = new WantsContext();
            Resolve(target, "manager", context);

            Assert.That(target.Context, Is.SameAs(context));
        }

        [Test]
        public void Diagnostics_do_not_leak_from_one_resolve_call_into_the_next()
        {
            Resolve(new IndexTooHigh(), "sword", 12);
            Assert.That(_resolver.Diagnostics.Count, Is.EqualTo(1));

            Resolve(new IndexedInts(), 3, 7);
            Assert.That(_resolver.Diagnostics, Is.Empty);
        }

        [Test]
        public void An_empty_payload_assigns_nothing_and_reports_nothing()
        {
            var target = new IndexedInts();
            Resolve(target);

            Assert.That(target.X, Is.EqualTo(0));
            Assert.That(_resolver.Diagnostics, Is.Empty);
        }
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Expected: compile error — the type or namespace `SignalParamResolver` does not exist.

- [ ] **Step 3: Write the diagnostic type**

Create `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamDiagnostic.cs`:

```csharp
using System;

namespace FlowIoC.BaseModule.Injectable.Utils
{
    internal enum SignalParamDiagnosticKind
    {
        /// <summary>The written index is past the last value of that type in the payload.</summary>
        IndexOutOfRange,

        /// <summary>Another property already took the slot this index points at.</summary>
        DuplicateClaim,

        /// <summary>No unclaimed value of this property's type is left in the payload.</summary>
        NoFreeSlot
    }

    /// <summary>
    /// A binding failure the resolver found. The resolver reports rather than logs, so it
    /// stays free of Unity dependencies and stays assertable from a plain unit test.
    /// </summary>
    internal readonly struct SignalParamDiagnostic
    {
        public readonly SignalParamDiagnosticKind Kind;
        public readonly Type TargetType;
        public readonly string PropertyName;
        public readonly Type PropertyType;
        public readonly int RequestedIndex;
        public readonly int CandidateCount;
        public readonly int ClaimedCount;

        public SignalParamDiagnostic(
            SignalParamDiagnosticKind kind,
            Type targetType,
            string propertyName,
            Type propertyType,
            int requestedIndex,
            int candidateCount,
            int claimedCount)
        {
            Kind = kind;
            TargetType = targetType;
            PropertyName = propertyName;
            PropertyType = propertyType;
            RequestedIndex = requestedIndex;
            CandidateCount = candidateCount;
            ClaimedCount = claimedCount;
        }
    }
}
```

- [ ] **Step 4: Write the resolver**

Create `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/SignalParamResolver.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace FlowIoC.BaseModule.Injectable.Utils
{
    /// <summary>
    /// Fills the <c>[SignalParam]</c> properties of a command from a dispatched signal
    /// payload. Explicitly indexed properties are assigned first so that an unindexed
    /// property's result never depends on where the indexed ones sit in the file.
    /// </summary>
    internal sealed class SignalParamResolver
    {
        private readonly SignalParamCandidateFinder _candidateFinder = new SignalParamCandidateFinder();
        private readonly Dictionary<Type, List<int>> _candidatesByType = new Dictionary<Type, List<int>>();
        private readonly List<SignalParamDiagnostic> _diagnostics = new List<SignalParamDiagnostic>();
        private bool[] _claimed = Array.Empty<bool>();

        public IReadOnlyList<SignalParamDiagnostic> Diagnostics => _diagnostics;

        public void Resolve(object target, IReadOnlyList<SignalParamEntry> entries, object[] values)
        {
            _diagnostics.Clear();
            _candidatesByType.Clear();

            if (target == null || entries == null || entries.Count == 0)
                return;

            if (values == null || values.Length == 0)
                return;

            if (_claimed.Length < values.Length)
                _claimed = new bool[values.Length];
            else
                Array.Clear(_claimed, 0, values.Length);

            Type targetType = target.GetType();

            ResolveIndexed(target, targetType, entries, values);
            ResolveUnindexed(target, targetType, entries, values);
        }

        private void ResolveIndexed(object target, Type targetType, IReadOnlyList<SignalParamEntry> entries, object[] values)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                SignalParamEntry entry = entries[i];
                if (!entry.HasIndex)
                    continue;

                List<int> candidates = GetCandidates(entry.Type, values);

                if (entry.Index < 0 || entry.Index >= candidates.Count)
                {
                    Report(SignalParamDiagnosticKind.IndexOutOfRange, targetType, entry, candidates.Count, 0);
                    continue;
                }

                int slot = candidates[entry.Index];
                if (_claimed[slot])
                {
                    Report(SignalParamDiagnosticKind.DuplicateClaim, targetType, entry, candidates.Count, 1);
                    continue;
                }

                _claimed[slot] = true;
                entry.Property.SetValue(target, values[slot]);
            }
        }

        private void ResolveUnindexed(object target, Type targetType, IReadOnlyList<SignalParamEntry> entries, object[] values)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                SignalParamEntry entry = entries[i];
                if (entry.HasIndex)
                    continue;

                List<int> candidates = GetCandidates(entry.Type, values);

                int slot = -1;
                int claimedCount = 0;

                for (int c = 0; c < candidates.Count; c++)
                {
                    if (_claimed[candidates[c]])
                    {
                        claimedCount++;
                        continue;
                    }

                    slot = candidates[c];
                    break;
                }

                if (slot < 0)
                {
                    Report(SignalParamDiagnosticKind.NoFreeSlot, targetType, entry, candidates.Count, claimedCount);
                    continue;
                }

                _claimed[slot] = true;
                entry.Property.SetValue(target, values[slot]);
            }
        }

        private List<int> GetCandidates(Type type, object[] values)
        {
            if (_candidatesByType.TryGetValue(type, out List<int> cached))
                return cached;

            List<int> candidates = _candidateFinder.Find(type, values);
            _candidatesByType[type] = candidates;
            return candidates;
        }

        private void Report(SignalParamDiagnosticKind kind, Type targetType, SignalParamEntry entry, int candidateCount, int claimedCount)
        {
            _diagnostics.Add(new SignalParamDiagnostic(
                kind, targetType, entry.Property.Name, entry.Type,
                entry.Index, candidateCount, claimedCount));
        }
    }
}
```

- [ ] **Step 5: Run the tests and make sure they pass**

Run the EditMode tests filtered to `FlowIoC.Tests.SignalParamResolverTests`.
Expected: all eleven PASS.

- [ ] **Step 6: Let Unity import and commit**

```bash
cd Packages/FlowIoC
git add Runtime/BaseModule/Injectable/Utils/SignalParamDiagnostic.cs \
        Runtime/BaseModule/Injectable/Utils/SignalParamDiagnostic.cs.meta \
        Runtime/BaseModule/Injectable/Utils/SignalParamResolver.cs \
        Runtime/BaseModule/Injectable/Utils/SignalParamResolver.cs.meta \
        Tests/Editor/SignalParamResolverTests.cs \
        Tests/Editor/SignalParamResolverTests.cs.meta
git commit -m "Resolve [SignalParam] properties by index within their own type"
```

---

### Task 6: Wire the resolver into command injection

**Files:**
- Modify: `Packages/FlowIoC/Runtime/BaseModule/Contexts/IContext.cs`
- Modify: `Packages/FlowIoC/Runtime/BaseModule/Contexts/Context.cs`
- Modify: `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/InjectionExtensions.cs:402-460` and `:24-28`
- Test: `Packages/FlowIoC/Tests/Editor/SignalParamInjectionTests.cs`

**Interfaces:**
- Consumes: `SignalParamEntryBuilder.Build`, `SignalParamResolver.Resolve`, `SignalParamResolver.Diagnostics`, `SignalParamDiagnosticKind`.
- Produces: `IContext.SignalParamResolver` (internal getter). `InjectionExtensions.InjectCommand` keeps its existing signature `internal static void InjectCommand(this IContext context, ICommandBody command, params object[] signalParams)` and now fills same-typed properties correctly.

- [ ] **Step 1: Write the failing test**

Create `Packages/FlowIoC/Tests/Editor/SignalParamInjectionTests.cs`. `InjectCommand` only touches the context when a command has `[Inject]` or `[InjectSignal]` properties, so a command with only `[SignalParam]` can be injected with a null context.

```csharp
using FlowIoC.BaseModule.Controller;
using FlowIoC.BaseModule.Injectable.Attributes;
using FlowIoC.BaseModule.Injectable.Utils;
using NUnit.Framework;

namespace FlowIoC.Tests
{
    public class SignalParamInjectionTests
    {
        private class MoveCommand : Command
        {
            [SignalParam(0)] private int _x { get; set; }
            [SignalParam(1)] private int _y { get; set; }

            public int X => _x;
            public int Y => _y;

            public override void Execute() { }
        }

        private class DamageCommand : Command
        {
            [SignalParam] private string _weapon { get; set; }
            [SignalParam(0)] private int _amount { get; set; }
            [SignalParam(1)] private int _crit { get; set; }

            public string Weapon => _weapon;
            public int Amount => _amount;
            public int Crit => _crit;

            public override void Execute() { }
        }

        [Test]
        public void InjectCommand_fills_same_typed_properties_from_distinct_slots()
        {
            var command = new MoveCommand();

            InjectionExtensions.InjectCommand(null, command, 3, 7);

            Assert.That(command.X, Is.EqualTo(3));
            Assert.That(command.Y, Is.EqualTo(7));
        }

        [Test]
        public void InjectCommand_mixes_indexed_and_unindexed_properties()
        {
            var command = new DamageCommand();

            InjectionExtensions.InjectCommand(null, command, "sword", 12, 3);

            Assert.That(command.Weapon, Is.EqualTo("sword"));
            Assert.That(command.Amount, Is.EqualTo(12));
            Assert.That(command.Crit, Is.EqualTo(3));
        }

        [Test]
        public void InjectCommand_reuses_the_cached_entry_list_across_calls()
        {
            var first = new MoveCommand();
            var second = new MoveCommand();

            InjectionExtensions.InjectCommand(null, first, 1, 2);
            InjectionExtensions.InjectCommand(null, second, 8, 9);

            Assert.That(first.X, Is.EqualTo(1));
            Assert.That(first.Y, Is.EqualTo(2));
            Assert.That(second.X, Is.EqualTo(8));
            Assert.That(second.Y, Is.EqualTo(9));
        }
    }
}
```

- [ ] **Step 2: Run it to make sure it fails**

Expected: `InjectCommand_fills_same_typed_properties_from_distinct_slots` FAILS with `Expected: 7 But was: 3` — the old type-only lookup hands slot 0 to both properties.

- [ ] **Step 3: Give `IContext` a resolver**

In `Packages/FlowIoC/Runtime/BaseModule/Contexts/IContext.cs`, add the `using` and the internal member. `IContext` already declares internal members (`InjectAllInstances`), so this follows the existing shape.

Add to the using block:

```csharp
using FlowIoC.BaseModule.Injectable.Utils;
```

Add inside the interface, directly under `internal void ExecutePostConstructMethods();`:

```csharp
        internal SignalParamResolver SignalParamResolver { get; }
```

- [ ] **Step 4: Implement it on `Context`**

`Packages/FlowIoC/Runtime/BaseModule/Contexts/Context.cs` already has `using FlowIoC.BaseModule.Injectable.Utils;`. Add the backing field next to `private GameObject _gameObject;`:

```csharp
        private SignalParamResolver _signalParamResolver;
```

and the explicit implementation next to `void IContext.InjectAllInstances()`:

```csharp
        SignalParamResolver IContext.SignalParamResolver
            => _signalParamResolver ??= new SignalParamResolver();
```

- [ ] **Step 5: Cache the entry list**

In `Packages/FlowIoC/Runtime/BaseModule/Injectable/Utils/InjectionExtensions.cs`, extend the cache class (currently at lines 25-29):

```csharp
        private class CachedInjectableData
        {
            public Dictionary<Type, List<FieldInfo>> CachedFieldInfoList = new();
            public Dictionary<Type, List<PropertyInfo>> CachedPropertyInfoList = new();
            public List<SignalParamEntry> SignalParamEntries;
        }
```

- [ ] **Step 6: Replace the resolution code**

Still in `InjectionExtensions.cs`, delete `InjectSignalParamsToCommand`, `AssignSignalParameters`, `TryGetSignalParameter` and `LogSignalParamError` (lines 402-460, including the commented-out `FieldInfo` blocks inside them) and put this in their place:

```csharp
        private static void InjectSignalParamsToCommand(IContext context, ICommandBody command, params object[] signalParams)
        {
            if (signalParams == null || signalParams.Length == 0)
                return;

            List<SignalParamEntry> entries = GetSignalParamEntries(command.GetType());
            if (entries.Count == 0)
                return;

            SignalParamResolver resolver = context?.SignalParamResolver ?? new SignalParamResolver();
            resolver.Resolve(command, entries, signalParams);

            IReadOnlyList<SignalParamDiagnostic> diagnostics = resolver.Diagnostics;
            for (int i = 0; i < diagnostics.Count; i++)
                LogSignalParamDiagnostic(diagnostics[i]);
        }

        private static List<SignalParamEntry> GetSignalParamEntries(Type commandType)
        {
            if (!_cachedInjectableData.TryGetValue(commandType, out CachedInjectableData data))
            {
                data = new CachedInjectableData();
                _cachedInjectableData.Add(commandType, data);
            }

            return data.SignalParamEntries ??= new SignalParamEntryBuilder().Build(commandType);
        }

        private static void LogSignalParamDiagnostic(SignalParamDiagnostic diagnostic)
        {
            string reason = diagnostic.Kind switch
            {
                SignalParamDiagnosticKind.IndexOutOfRange =>
                    $"[SignalParam({diagnostic.RequestedIndex})] asks for {diagnostic.PropertyType.Name} value {diagnostic.RequestedIndex}, but the signal carried {diagnostic.CandidateCount}.",
                SignalParamDiagnosticKind.DuplicateClaim =>
                    $"[SignalParam({diagnostic.RequestedIndex})] asks for a {diagnostic.PropertyType.Name} value another property already took. Give the two properties different indices.",
                _ =>
                    $"No unclaimed {diagnostic.PropertyType.Name} value is left. The signal carried {diagnostic.CandidateCount} and {diagnostic.ClaimedCount} were already taken."
            };

            FlowLogger.LogError(SystemLogType.CommandOperation,
                "<b><color=#FF6666>► Signal Param could not be bound!</color></b>\n" +
                "<b><color=#FF6666>► Command:</color><color=#FFEFD5> " + diagnostic.TargetType.Name + "</color></b>\n" +
                "<b><color=#FF6666>► Property:</color><color=#FFEFD5> " + diagnostic.PropertyName + "</color></b>\n" +
                "<b><color=#FF6666>► Type:</color><color=#FFEFD5> " + diagnostic.PropertyType.Name + "</color></b>\n" +
                "<b><color=#FF6666>► Reason:</color><color=#FFEFD5> " + reason + "</color></b>",

                "► Signal Param could not be bound!\n" +
                "► Command: " + diagnostic.TargetType.Name + "\n" +
                "► Property: " + diagnostic.PropertyName + "\n" +
                "► Type: " + diagnostic.PropertyType.Name + "\n" +
                "► Reason: " + reason);
        }
```

- [ ] **Step 7: Run the tests and make sure they pass**

Run the whole `FlowIoC.Tests` EditMode suite, not just the new file — Task 3 through Task 5 must still be green.
Expected: every test PASSES. If `System.Linq` is now unused in `InjectionExtensions.cs`, leave the using alone; other methods in the file still use it.

- [ ] **Step 8: Check the package's own commands still bind**

Grep the eight in-package `[SignalParam]` sites and confirm none of them declares two properties of the same type:

```bash
cd Packages/FlowIoC
grep -rn "SignalParam" --include=*.cs Runtime/AssetModule Runtime/PoolModule Runtime/ScreenModule
```

Expected: `RegisterScreenConfigCommand` and `UnRegisterScreenConfigCommand` pair `int` with `List<ScreenConfig>`, `RegisterScreenManagerCommand` pairs `ScreenManagerVO` with `IContext`, and the rest declare one property each. No same-type pair — nothing changes behaviour.

- [ ] **Step 9: Let Unity import and commit**

```bash
cd Packages/FlowIoC
git add Runtime/BaseModule/Contexts/IContext.cs \
        Runtime/BaseModule/Contexts/Context.cs \
        Runtime/BaseModule/Injectable/Utils/InjectionExtensions.cs \
        Tests/Editor/SignalParamInjectionTests.cs \
        Tests/Editor/SignalParamInjectionTests.cs.meta
git commit -m "Bind command signal parameters through the indexed resolver"
```

---

### Task 7: Documentation, changelog and version

**Files:**
- Modify: `Packages/FlowIoC/README.md:485-494`
- Modify: `Packages/FlowIoC/Runtime/BaseModule/Controller/Documentation/Controller.md:77-96` and `:616-621`
- Modify: `Packages/FlowIoC/CHANGELOG.md`
- Modify: `Packages/FlowIoC/package.json`

**Interfaces:**
- Consumes: the behaviour built in Tasks 2-6.
- Produces: nothing other tasks read.

- [ ] **Step 1: Rewrite the README section**

In `Packages/FlowIoC/README.md`, replace the whole `### Reading signal parameters` section (from that heading down to the blank line before `### Command groups`) with:

````markdown
### Reading signal parameters

Each `[SignalParam]` property is filled from the payload of the signal that
triggered the command.

```csharp
public Signal<CurrencyType, int> DecreaseCurrency = new();
```

```csharp
[SignalParam] private CurrencyType _type   { get; set; }
[SignalParam] private int          _amount { get; set; }
```

When a signal carries more than one value of the same type, write the index of the
one you want. The index counts within that property's type, so inserting a
parameter of some other type into the signal does not shift it.

```csharp
public Signal<string, int, int> Damage = new();   // Dispatch("sword", 12, 3)
```

```csharp
[SignalParam]    private string _weapon { get; set; }   // "sword"
[SignalParam(0)] private int    _amount { get; set; }   // 12
[SignalParam(1)] private int    _crit   { get; set; }   // 3
```

A property with no index takes the first value of its type that no other property
has claimed, so two same-typed properties also resolve correctly on their own:

```csharp
public Signal<int, int> Move = new();   // Dispatch(3, 7)
```

```csharp
[SignalParam] private int _x { get; set; }   // 3
[SignalParam] private int _y { get; set; }   // 7
```
````

- [ ] **Step 2: Rewrite the Controller.md payload section**

In `Packages/FlowIoC/Runtime/BaseModule/Controller/Documentation/Controller.md`, the section `### Reading the signal's payload` opens with "Each `[SignalParam]` property is filled from the dispatched signal, in order." Leave the existing `DecreaseCurrency` example in place and append, after its closing code fence:

````markdown
Signals that carry two values of the same type need an index. It counts within the
property's own type, not across the whole payload.

```csharp
// PlayerSignals.cs
public Signal<string, int, int> Damage = new();   // Dispatch("sword", 12, 3)
```

```csharp
public class ApplyDamageCommand : Command
{
    [SignalParam]    private string _weapon { get; set; }   // "sword"
    [SignalParam(0)] private int    _amount { get; set; }   // 12
    [SignalParam(1)] private int    _crit   { get; set; }   // 3

    public override void Execute() { }
}
```

Without an index a property takes the first value of its type that no other
property has claimed, so `[SignalParam] int _x` followed by `[SignalParam] int _y`
receives the first and second int. Use an explicit index when the declaration order
is not obvious from reading the class.
````

- [ ] **Step 3: Rewrite the troubleshooting entry**

Still in `Controller.md`, replace the body of `### The parameters arrive wrong` — the paragraph that currently ends "will bind without complaint" — with:

```markdown
`[SignalParam]` properties are filled from the signal's payload by type, and typed
`Execute(...)` overloads are matched against what the previous step released. If
either shape changes, update both ends.

Two `[SignalParam]` properties of the same type are distinguished by their index:
`[SignalParam(0)]` and `[SignalParam(1)]` take the first and second value of that
type. Properties with no index take the next unclaimed value of their type, in
declaration order. A property that cannot be bound — an index past the end, two
properties claiming the same value, or no unclaimed value left — logs an error
naming the command, the property and the reason.
```

- [ ] **Step 4: Add the changelog entry**

In `Packages/FlowIoC/CHANGELOG.md`, insert directly under the intro paragraph and above `## [1.0.1] - 2026-08-19`:

```markdown
## [1.1.0] - 2026-08-21

### Added

- `[SignalParam]` accepts an index: `[SignalParam(1)]` binds to the second value of
  that property's type in the signal payload. The index counts within the type, so
  adding a parameter of another type to the signal does not shift it. Commands can
  now read a `Signal<int, int>` or a `Signal<string, string>` correctly.
- An EditMode test assembly at `Tests/Editor`, covering signal parameter resolution.

### Fixed

- A command with two `[SignalParam]` properties of the same type received the same
  value in both. Properties without an index now take the next value of their type
  that no other property has claimed.
- A dispatched `null` was reported as a missing parameter. `null` now binds to any
  property whose type can hold it, and "no value at this index" is reported
  separately from "the value is null".
- Binding failures now name the command, the property and the reason instead of
  logging only the parameter type.
```

- [ ] **Step 5: Bump the version**

In `Packages/FlowIoC/package.json`, change `"version": "1.0.1"` to `"version": "1.1.0"`.

- [ ] **Step 6: Re-run the full suite**

Run every EditMode test one more time. Expected: all PASS. Documentation changes cannot break them, but this is the last gate before the package is committed.

- [ ] **Step 7: Commit**

```bash
cd Packages/FlowIoC
git add README.md CHANGELOG.md package.json \
        Runtime/BaseModule/Controller/Documentation/Controller.md
git commit -m "Document indexed signal parameters and release 1.1.0"
```

---

### Task 8: Record the package version in the template repository

**Files:**
- Modify: the `Packages/FlowIoC` submodule pointer in the template repository

**Interfaces:**
- Consumes: the commits from Tasks 1-7.
- Produces: nothing.

- [ ] **Step 1: Confirm the submodule is clean**

```bash
cd Packages/FlowIoC
git status --short      # expect no output
git log --oneline -7
cd ../..
```

Expected: the seven commits from Tasks 1-7 on branch `signalparam-index`.

- [ ] **Step 2: Stage and commit the pointer**

The template repository already had an unrelated submodule pointer bump staged before this work started (`2bcc411..93ff059`, the LICENSE.md rename). Committing now folds that into the same commit, which is correct — it is the same submodule moving forward.

```bash
git add Packages/FlowIoC
git commit -m "Update FlowIoC to indexed signal parameters"
git log --oneline -3
```

- [ ] **Step 3: Report, do not push**

Both repositories now hold the work on branch `signalparam-index`. Do not push and do not tag — say so and let the user decide when to publish.

---

## Self-Review

**Spec coverage:** The resolution algorithm is Task 5; the candidate rule including `null` is Task 4; the attribute is Task 2; deterministic property order is Task 3; diagnostics are Tasks 5 and 6; caching and the `Context`-owned resolver are Task 6; the file list and documentation are Task 7; the verification list is spread across the test files of Tasks 3-6. Every spec section maps to a task. The spec's "Out of scope" section — `Command<T1>` as a first step — is intentionally absent.

**Type consistency:** `SignalParamEntry(PropertyInfo, int, bool)`, `SignalParamEntryBuilder.Build(Type)`, `SignalParamCandidateFinder.Find(Type, object[])`, `SignalParamResolver.Resolve(object, IReadOnlyList<SignalParamEntry>, object[])` and `SignalParamResolver.Diagnostics` are spelled the same in the interfaces blocks, the implementations and the tests.

**Known trade-off carried from the spec:** the value-based candidate rule cannot disambiguate a payload that mixes a base type with a `null`, such as `Signal<object, string>` dispatched as `(null, "x")`. The `NoFreeSlot` and `DuplicateClaim` diagnostics make it visible rather than silent.
