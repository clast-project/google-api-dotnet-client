# ClastJsonContextGen

Regenerates the committed `Src/Generated/*/*.JsonContext.cs` files — the reflection-free AOT
registries the Clast port relies on — from their generated REST clients.

```
python Tools/ClastJsonContextGen/regenerate.py            # rewrite in place
python Tools/ClastJsonContextGen/regenerate.py --check    # report drift, exit 1
```

Requires only Python 3 (no packages).

## Why this exists

Each ported client ships a `<Client>.JsonContext.cs` holding three things:

1. the `[JsonSerializable]` list for the `System.Text.Json` source generator,
2. `RequestParameterRegistry` descriptors, so building a request URL needs no reflection over
   `[RequestParameter]` attributes,
3. `EnumStringValueRegistry` maps, so enum-valued parameters need no reflection over
   `[StringValue]`.

These were originally emitted by an external Clast transform tool that was never checked in.
That left a real gap on every upstream sync: when upstream **removes** a request type the build
catches it (`CS0426`), but when upstream **adds** one, nothing fails — the new type silently
falls back to reflection, which is exactly what the AOT port is meant to eliminate. It had
already bitten us: `Bigquery.v2`'s `ArrowRecordBatch`, `ArrowSchema` and `ArrowSerializationOptions`
arrived in `1.75.0.4188` and went unregistered until the 2026-08-24 sync.

This script replaces the missing emitter. It was validated by regenerating all four committed
`*.JsonContext.cs` files byte-for-byte from their then-current clients — the only difference
anywhere was the three unregistered `Arrow*` entries above.

## How the output is derived

`csparse.py` is a brace-tracking line scanner for the *generated* client `.cs` (machine-generated
and very regular, so this is exact — it is not a general C# parser). `gen_jsoncontext.py` turns
its output into the file:

| Section | Rule |
|---|---|
| `[JsonSerializable]` | every class in the `.Data` namespace, nested path joined with `_`, sorted `OrdinalIgnoreCase` |
| …plus carried-over entries | any `[JsonSerializable]` line in the existing file that isn't derivable is preserved verbatim at the end — e.g. the two explicit `IList<T>` registrations `Bigquery.v2` needs to disambiguate two nested types both named `ReservationUsageData` |
| `RequestParameterRegistry` | request classes in **declaration** order (including `*MediaUpload : ResumableUpload<>`); each request's own parameters, then the base service request's appended — except media-upload classes, which redeclare the common parameters themselves and get no append |
| `isValueType` | true iff the property type is `System.Nullable<...>`; a required, non-nullable `long` parameter is `false` |
| `EnumStringValueRegistry` | enums with `[StringValue]` members nested in the base service request (emitted as `Base<object>.XEnum`) or in a registered request, in declaration order |

Because unknown `[JsonSerializable]` lines are carried over rather than dropped, hand-added
registrations survive a regeneration. Nothing else in the file is preserved: a hand edit to a
descriptor or an enum map will be overwritten, so make such changes here instead.

## Adding a client

Append its directory and namespace to `CLIENTS` in `regenerate.py`.
