# Clast republish

This is a **Clast** package — an independent republish (from the [`clast-project`](https://github.com/clast-project) fork) of a Google .NET library. **It is not an official Google package and is not affiliated with or endorsed by Google.**

**How it differs from the upstream package:**

- `Newtonsoft.Json` is replaced with **source-generated `System.Text.Json`** (no reflection-based serialization).
- The library is **trimming / Native-AOT compatible**, with a **`net10.0`** target added (alongside `netstandard2.0` and `net8.0`).
- **Namespaces and public type names are unchanged.** Only the package id, assembly name, and strong-name key differ, so you recompile against the `Clast.*` packages rather than dropping them in as binary replacements.

**Packages republished from this repository:** `Clast.Google.Apis.Core`, `Clast.Google.Apis`, `Clast.Google.Apis.Auth`, `Clast.Google.Apis.Storage.v1`, `Clast.Google.Apis.Bigquery.v2`.

See [github.com/clast-project/google-api-dotnet-client](https://github.com/clast-project/google-api-dotnet-client) — including `PLAN.md` and `BEHAVIORAL-CHANGES.md` — for the full design and the catalogue of Newtonsoft→System.Text.Json behavior differences.
