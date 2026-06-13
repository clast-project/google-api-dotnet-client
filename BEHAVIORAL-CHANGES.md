# Behavioral Changes Ledger (Newtonsoft.Json → System.Text.Json)

Authoritative record of every observable behavior difference introduced by the Clast port (see `PLAN.md`).
Each entry exists so that a later break or functional regression can be **attributed** to a specific,
intentional change — and so we can decide, per change, whether to drive **compatibility work** (e.g. an
input-preprocessing shim or a custom converter) instead of accepting the difference.

## How to use this ledger

- When you hit a regression, search this file by the **Detection signature** (exception text, symptom, or
  changed API). If it matches an entry, you have the cause and the documented **Compat path**.
- When you discover a *new* divergence during the port, add an entry here in the same turn — don't leave it
  only in code comments or commit messages.
- **Dispositions:** `Preserved` (parity kept, usually via a mitigation that is itself a thing to watch) ·
  `Accepted` (intentional change, no compat planned) · `Fixture-fixed` (stricter behavior kept; test data was
  corrected) · `Open` (undecided or not yet root-caused).
- A central place to land runtime compat shims is `SystemTextJsonSerializer` in `Google.Apis.Core` (the
  `Deserialize`/`Serialize` entry points) — input preprocessing or option toggles would go there so entries
  can point at one mechanism.

---

## BC-001 — Property-name matching is now case-sensitive
- **Area:** Core (all deserialization)
- **Was → Now:** Newtonsoft matched JSON keys to members case-insensitively. STJ is case-sensitive by default
  (and we keep it that way — the two-ETags test depends on `etag` vs `ETag` being distinct).
- **Disposition:** `Preserved` via mitigation — added explicit `[JsonPropertyName]` to attribute-less support
  types (`RequestError`, `SingleError`, `StandardResponse`, AWS response struct). Generated models already
  carry exact names.
- **Detection signature:** a property deserializes to `null`/default even though the payload "has" it, but with
  different casing than the C# member or the `[JsonPropertyName]`.
- **Compat path:** per-type — add/repair `[JsonPropertyName]`. Avoid global `PropertyNameCaseInsensitive=true`
  (breaks BC by re-merging `etag`/`ETag`). Risk: any *unattributed* hand-written model still relying on
  case-insensitivity is a latent break — audit when porting each library.

## BC-002 — Deserializing to `object` yields `JsonElement`, not `DateTime`/`string`
- **Area:** Core
- **Was → Now:** Newtonsoft's `DateParseHandling` auto-parsed date-shaped strings to `DateTime` (and gave
  `string`/`JObject` otherwise) when the target was `object`. STJ returns a `JsonElement`.
- **Disposition:** `Accepted`. The two `NewtonsoftJsonSerializer` tests encoding this were dropped/replaced.
  The generated-client closure never deserializes to `object` (models are concretely typed); typed `DateTime`
  properties still work (RFC 3339 read is preserved).
- **Detection signature:** `InvalidCastException`/null when code does `(DateTime)serializer.Deserialize<object>(...)`
  or `value is DateTime`/`value is string` on untyped results.
- **Compat path:** a custom `JsonConverter<object>` that sniffs RFC 3339 strings — fragile; only if a real
  consumer needs it.

## BC-003 — `NewtonsoftJsonSerializer` type and its settings API are removed
- **Area:** Core (public API)
- **Was → Now:** `NewtonsoftJsonSerializer` (incl. `Instance`, `CreateDefaultSettings()`, the
  `JsonSerializerSettings` ctor) is replaced by `SystemTextJsonSerializer`. `IJsonSerializer` /
  `ISerializer` and the `BaseClientService.Initializer.Serializer` hook are unchanged.
- **Disposition:** `Accepted` — implied by removing Newtonsoft; consumers recompile (assemblies are renamed
  `Clast.*` anyway).
- **Detection signature:** compile error CS0246/CS0117 referencing `NewtonsoftJsonSerializer` or
  `JsonSerializerSettings`.
- **Compat path:** none intended.

## BC-004 — Unescaped control characters in JSON strings are rejected
- **Area:** Core (all deserialization)
- **Was → Now:** Newtonsoft tolerated raw control characters (e.g. `0x0D` CR, `0x0A` LF) inside JSON string
  values. STJ rejects them per RFC 8259: `'0x0D' is invalid within a JSON string. The string should be
  correctly escaped.`
- **Disposition:** `Open` — **recommend the preprocessing shim** (revised 2026-06-13). The "fix the fixtures"
  approach hit collateral damage: a blanket PEM-newline repair across the test tree corrupted private-key
  constants that are passed *directly* to the decoder (not via JSON), e.g. `Pkcs8Tests`, producing 32 spurious
  Base-64 failures. Distinguishing "PEM inside a JSON string" from "PEM passed directly" mechanically is
  error-prone. The shim avoids touching any fixture and is more faithfully drop-in.
- **Detection signature:** `System.Text.Json.JsonException: '0x..' is invalid within a JSON string` (often on
  `$.private_key`); surfaced by Auth as `InvalidOperationException: Error deserializing JSON credential data`.
- **Compat path (recommended):** a try-strict / on-control-char-failure-retry shim in
  `SystemTextJsonSerializer.Deserialize` that escapes raw control characters *inside JSON strings* (a small
  state-tracking scan) and re-parses. STJ has **no** reader option for this, so preprocessing is the only lever.
  Keep the happy path strict so there's no perf cost on valid input.

## BC-005 — JWT `aud` claim (`object`) materializes as `string`/`List<string>`
- **Area:** Auth (`JsonWebToken.Payload.Audience`)
- **Was → Now:** Newtonsoft produced a `string` or `List<string>` for the `object`-typed `aud`; STJ would
  produce a `JsonElement`, breaking `AudienceAsList` and audience validation.
- **Disposition:** `Preserved` via mitigation — `AudienceJsonConverter` reproduces the `string`/`List<string>`
  result.
- **Detection signature:** `AudienceAsList` empty / audience validation failing for tokens with an `aud` array.
- **Compat path:** the converter *is* the compat shim; any new `object`-typed JSON property elsewhere needs the
  same treatment.

## BC-006 — Private members are not populated unless opted in
- **Area:** Auth (and any hand-written model with private JSON members)
- **Was → Now:** Newtonsoft populated private/`private set` members by default. STJ source-gen does not.
- **Disposition:** `Preserved` via mitigation — but note the **stronger constraint** found 2026-06-13:
  `internal` + `[JsonInclude]` is **NOT** enough under the *source generator* (it errors
  `"...annotated with the JsonIncludeAttribute but is not visible to the source generator"`). The member must be
  **public** (or a public property with a non-public accessor). So `TokenResponse.ImpersonatedAccessToken/
  ImpersonatedIdToken/ImpersonatedAccessTokenExpireTime` and `GoogleClientSecrets.Installed/Web` were made
  **public** — a small public-API addition (acceptable: assemblies are renamed `Clast.*` and consumers recompile).
- **Detection signature:** build/first-use `InvalidOperationException: '<member>' has been annotated with the
  JsonIncludeAttribute but is not visible to the source generator`; or a private-set property staying default.
- **Compat path:** make the member public (set-only is fine — it becomes deserialize-only). Audit each ported
  library for non-public JSON members.

## BC-007 — Open-generic deserialization fails for caller-defined subclasses
- **Area:** Auth (`JsonWebSignature.VerifySignedTokenAsync<TPayload>`) and any public generic `Deserialize<T>`
- **Was → Now:** Newtonsoft (reflection) deserialized any caller-supplied `TPayload`. Source-gen STJ only knows
  types listed in a registered `JsonSerializerContext`, so a consumer's own subclass throws.
- **Disposition:** `Open` — documented AOT limitation.
- **Detection signature:** `System.NotSupportedException: JsonTypeInfo metadata for type 'X' was not provided`.
- **Compat path:** (a) provide a public API for consumers to register their own context;
  (b) on non-AOT targets, chain a `DefaultJsonTypeInfoResolver` (reflection) fallback — but that reintroduces
  reflection, so it must be opt-in / non-net10.

## BC-008 — Null values omitted on write (parity, recorded for completeness)
- **Area:** Core
- **Was → Now:** Newtonsoft `NullValueHandling.Ignore` ⇒ STJ `DefaultIgnoreCondition = WhenWritingNull`.
  Behavior preserved; explicit-null sentinel (`JsonExplicitNull`) preserved via a resolver modifier.
- **Disposition:** `Preserved` (configuration).
- **Detection signature:** unexpected `"prop":null` appearing, or an expected explicit null being omitted.
- **Compat path:** the `ExplicitNullJsonModifier` is the mechanism; see `clast-serializer-design`.

## BC-009 — Serialization output differences ("Strings differ") — UNDER INVESTIGATION
- **Area:** Auth (≈43 failing tests as of 2026-06-13)
- **Was → Now:** Not yet root-caused. Candidate causes: property write ordering, RFC 3339 formatting nuance,
  byte[]/base64 framing, or round-trip artifacts of BC-004/BC-006.
- **Disposition:** `Open`.
- **Detection signature:** xUnit `Assert.Equal() Failure: Strings differ` on serialized JSON.
- **Compat path:** TBD once root-caused; record concrete sub-entries here.
- **Update 2026-06-13:** root-caused — the *tests* serialized our models with `NewtonsoftJsonSerializer`, which
  (now that models carry only STJ attributes) emits PascalCase C# names that STJ-deserialize can't read back.
  Resolved by porting the test project's serializer calls to `SystemTextJsonSerializer`. Not a production change.

## BC-010 — Anonymous types cannot be serialized by the source generator
- **Area:** anywhere `Serialize(new { ... })` was used (production + tests)
- **Was → Now:** Newtonsoft serialized anonymous types via reflection. Source-gen STJ has no `JsonTypeInfo` for
  them → `NotSupportedException: metadata for type '<>f__AnonymousType…' was not provided`.
- **Disposition:** `Accepted` — production occurrence already fixed (the STS options type became the named
  `StsRequestOptions`, BC in `PLAN.md`). Test fakes that build JSON via anonymous types must switch to a named
  type or a raw JSON string literal (≈25 test sites as of 2026-06-13).
- **Detection signature:** `NotSupportedException: ... metadata for type '<>f__AnonymousType…' was not provided`.
- **Compat path:** introduce a named type (register it) or emit a JSON string literal directly.

## BC-011 — Single-quoted JSON is rejected
- **Area:** all deserialization
- **Was → Now:** Newtonsoft accepted `'`-quoted property names/strings; STJ requires `"` per spec
  (`''' is an invalid start of a value`).
- **Disposition:** `Fixture-fixed` — test JSON literals using single quotes are corrected to double quotes.
- **Detection signature:** `System.Text.Json.JsonException: ''' is an invalid start of a value`.
- **Compat path:** same control-char shim location could normalize quotes, but prefer fixing inputs; only a real
  consumer feeding single-quoted JSON would justify a runtime shim.

## BC-013 — JSON string escaping (encoder)
- **Area:** Core (all serialization)
- **Was → Now:** STJ's default encoder `\uXXXX`-escapes `"`, `<`, `>`, `&`, `'` (HTML-safe); Newtonsoft uses
  minimal escaping (`\"`, etc.). This changed serialized bytes and lengths (e.g. ETag values).
- **Disposition:** `Preserved` via `JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping`
  in `SystemTextJsonSerializer` — matches Newtonsoft. Safe for API JSON payloads (not HTML-embedded).
- **Detection signature:** serialized JSON differs only in escaping (`"` vs `\"`), Content-Length mismatch.
- **Compat path:** the encoder option (already applied).

## BC-014 — Lenient read: trailing commas and comments
- **Area:** Core (all deserialization)
- **Was → Now:** Newtonsoft accepted trailing commas and `//`/`/* */` comments; STJ rejects both by default
  (`The JSON object contains a trailing comma ...`).
- **Disposition:** `Preserved` via `AllowTrailingCommas = true` and `ReadCommentHandling = JsonCommentHandling.Skip`.
  (Unlike BC-004 control chars, STJ has native reader options for these.)
- **Detection signature:** `JsonException: ... trailing comma ...` or `... comments are not supported ...`.
- **Compat path:** the two options (already applied).

## BC-015 — Default serializer is now System.Text.Json
- **Area:** Google.Apis (`BaseClientService.Initializer.Serializer`)
- **Was → Now:** the default `IJsonSerializer` is `SystemTextJsonSerializer.Instance`, not
  `NewtonsoftJsonSerializer.Instance`. Generated clients must therefore carry STJ attributes + a registered
  `JsonSerializerContext` (the Phase-2 transform). Legacy data-wrapping (`Features.LegacyDataResponse`) needs
  `StandardResponse<TResponse>` registered per response type.
- **Disposition:** `Accepted` (the core goal). Tests asserting the default type were updated.
- **Detection signature:** `NotSupportedException: JsonTypeInfo metadata for type '<model>' was not provided`
  at runtime for an un-transformed client, or `IsType<NewtonsoftJsonSerializer>` assertion failures.
- **Compat path:** transform the client (attributes + context); for legacy data-wrapping, register
  `StandardResponse<T>` for each response type.

## BC-016 — Request-parameter & enum-StringValue discovery is source-generated, not reflective (AOT)
- **Area:** Core (`Utilities.ConvertToString`/`GetEnumStringValue`, `RequestParameterDescriptorProvider`,
  `ParameterUtils`), Apis (`ResumableUpload`), and every generated client + Auth's OAuth request types.
- **Was → Now:** Request parameters were discovered with `Type.GetProperties` (IL2070) and an enum's wire string
  with `Type.GetField(...).GetCustomAttribute<StringValueAttribute>()` (IL2075) — both reflect over generated
  types and are not trim-safe. Now each client source-generates, per concrete request type, a flattened
  `RequestParameterDescriptor` list (registered in `RequestParameterRegistry`) and, per `[StringValue]` enum, a
  value→wire-string converter (registered in `EnumStringValueRegistry`), via a module initializer. Core consults
  these registries first on all TFMs. Auth's hand-written request types and Core's `Discovery.Features` enum are
  registered by hand-written module initializers; the Clast transform emits the registrations for generated clients.
- **Disposition:** `Preserved` on netstandard2.0/net8.0 (the reflective fallback remains for un-registered/
  hand-written types). On the **net10.0 (AOT) target the reflective fallback is compiled out**: an unregistered
  request type throws `NotSupportedException` (loud, not a silent loss of query params), and an unregistered enum
  falls back to its member name (`value.ToString()`) instead of its `[StringValue]`. The reflective
  `GetEnumStringValue` throw-on-missing-StringValue semantics are likewise unavailable on net10.
- **Detection signature:** `NotSupportedException: ... has no source-generated request-parameter metadata` or
  `... No source-generated StringValue map is registered for enum type ...`; or an enum query/path parameter
  serializing as its C# member name instead of its wire string on a trimmed/AOT app.
- **Compat path:** run the client through the Clast transform (it registers all request types and enums); for
  hand-written request types/enums, register them in a module initializer (see Auth's
  `AuthRequestParameterRegistration` and Core's `FeaturesRegistration`). Generic-nested enums and generic request
  types are keyed by their generic type definition (`GetGenericTypeDefinition`), so any closed instantiation
  resolves to one registration. Ordering mirrors `Type.GetProperties` (a type's own/derived members first, then
  inherited) because some query/form-order assertions depend on it.

## BC-017 — UrlSigner V4 JSON writing: Utf8JsonWriter + manual non-ASCII escaping
- **Area:** `Google.Cloud.Storage.V1` `UrlSigner` (V4 POST policy / signed-URL JSON), google-cloud-dotnet.
- **Was → Now:** the V4 POST policy was written with Newtonsoft `JsonTextWriter` +
  `StringEscapeHandling.EscapeNonAscii`. Now it uses `Utf8JsonWriter` with `UnsafeRelaxedJsonEscaping`. STJ cannot
  reproduce Newtonsoft's escaping byte-for-byte: it emits **upper-case** `\uXXXX` and `\uXXXX` for control chars,
  whereas the V4 POST-policy contract (and conformance fixtures) expect **lower-case** `é` for non-ASCII and
  leave `< > & ' + % = /` unescaped. Relaxed matches the ASCII expectation exactly but leaves non-ASCII raw, so a
  post-pass (`EscapeNonAscii`) rewrites every non-ASCII char to lower-case `\uXXXX`.
- **Disposition:** `Preserved` via mitigation — `UnsafeRelaxedJsonEscaping` + the `EscapeNonAscii` post-pass make
  the signed policy byte-identical to the previous Newtonsoft output for the conformance suite (incl. the
  "Character Escaping" and "Additional Metadata" cases, which contain `é`, `"`, `%=/` etc.). One residual
  difference: control characters in policy values would be `\uXXXX` (STJ) vs Newtonsoft's short forms (`\n`); such
  characters do not occur in POST-policy data.
- **Detection signature:** `V4SignerConformanceTest.PostPolicyTest` signature/`policy`-field mismatch, or a signed
  policy differing only in `\u` case or non-ASCII escaping.
- **Compat path:** the `EscapeNonAscii` helper in `UrlSigner.V4Signer.cs` is the mechanism. `IPostPolicyCondition`
  and the policy writers now take `System.Text.Json.Utf8JsonWriter` (all internal; values written via a typed
  `WritePostPolicyValue` switch replacing Newtonsoft's `WriteValue(object)`).

## BC-018 — int64/uint64 read from JSON strings (NumberHandling)
- **Area:** Core / Auth / every generated client (all deserialization of numeric model properties).
- **Was → Now:** Google's JSON APIs serialize 64-bit integers (and similar) as JSON **strings** (e.g.
  `"projectNumber":"0"`, `"size":"12345"`), and generated models type them as `long?`/`ulong?`. Newtonsoft
  coerced string↔number automatically; STJ throws `JsonException: The JSON value could not be converted to
  System.Nullable<UInt64> ... Cannot get the value of a token type 'String' as a number.`
- **Disposition:** `Preserved` via mitigation — `JsonNumberHandling.AllowReadingFromString` set on the runtime
  `SystemTextJsonSerializer` options AND on every source-generated context's `[JsonSourceGenerationOptions]`
  (Core, Auth, and the per-client contexts emitted by the transform). This reads numbers from string tokens while
  still writing them as numbers (matching Newtonsoft's output direction; Google accepts numbers on input).
- **Detection signature:** the `JsonException`/`Cannot get the value of a token type 'String' as a number` above,
  typically on `$.size`, `$.projectNumber`, `$.generation`, `$.metageneration`, etc.
- **Compat path:** the `NumberHandling` option is the mechanism. NOTE: source-gen type infos honor the *context's*
  `JsonSourceGenerationOptions.NumberHandling`, so it must be set on each `JsonSerializerContext` (not only the
  runtime options) — the transform now emits it for every generated client. Found via the fake-gcs-server
  round-trip (realistic API JSON with quoted numbers); hand-written unit-test fixtures had used numeric literals.

## BC-019 — Windows GCE BIOS (WMI) detection disabled on the AOT target
- **Area:** Auth (`ComputeCredential` GCE-residency detection), net10.0 only.
- **Was → Now:** `ComputeCredential.IsRunningOnComputeEngine` is `metadata-server-ping OR BIOS-check`. The Windows
  BIOS check used WMI (`System.Management` `Win32_BIOS`), which is **not trimming/AOT-compatible** (publish emits
  IL2104/IL3053 for `System.Management.dll`). On net10.0 the WMI fast-path is compiled out (`IsWindowsGoogleBios`
  returns false) and the `System.Management` package is no longer referenced; on netstandard2.0/net8.0 it is
  unchanged.
- **Disposition:** `Accepted`. The metadata-server ping is tried first and is authoritative on GCE (the metadata
  service is a core GCE component, virtually always reachable there), so the only lost capability is the BIOS
  fallback that mattered solely when the ping fails *while actually on GCE Windows* — a narrow, transient case.
- **Detection signature:** native-AOT publish warnings `IL2104: Assembly 'System.Management' produced trim
  warnings` / `IL3053: ... AOT analysis warnings` when the credential-detection path is rooted; or GCE-on-Windows
  not detected on net10 when the metadata server is unreachable.
- **Compat path:** if the BIOS fast-path is needed under AOT, replace the WMI lookup with an AOT-safe registry read
  of the GCE BIOS signal (`Microsoft.Win32.Registry`) rather than re-introducing `System.Management`.

## BC-012 — Thrown JSON exception type changed
- **Area:** all serialization
- **Was → Now:** parse failures throw `System.Text.Json.JsonException` instead of `Newtonsoft.Json.JsonException`.
  Both derive from `System.Exception`; catch sites in product code were realigned via the `using` swap.
- **Disposition:** `Accepted`.
- **Detection signature:** `Assert.Throws<JsonException>`/`Exception type was not an exact match` in tests, or a
  `catch (Newtonsoft.Json.JsonException)` no longer catching STJ failures.
- **Compat path:** none; update catch/assert sites to `System.Text.Json.JsonException`.
