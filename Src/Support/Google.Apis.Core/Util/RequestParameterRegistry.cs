/*
Copyright 2026 Google Inc

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Google.Apis.Util
{
    /// <summary>
    /// A single request parameter described without reflection: its wire name, its location (path/query), whether the
    /// declaring property is a value type, and a delegate that reads the value from a request instance.
    /// </summary>
    /// <remarks>
    /// <see cref="IsValueType"/> mirrors the legacy reflective behavior, where a property whose declared type is a
    /// value type (including <see cref="System.Nullable{T}"/>) is always passed to the parameter sink even when its
    /// value is <c>null</c>; reference-typed properties are only passed when non-null.
    /// </remarks>
    public sealed class RequestParameterDescriptor
    {
        /// <summary>The parameter's wire name (the name from its <see cref="RequestParameterAttribute"/>).</summary>
        public string Name { get; }

        /// <summary>Where the parameter belongs in the request (path, query or user-defined queries).</summary>
        public RequestParameterType ParameterType { get; }

        /// <summary>Whether the declaring property's type is a value type (including <see cref="System.Nullable{T}"/>).</summary>
        public bool IsValueType { get; }

        /// <summary>Reads the parameter's value from a request instance.</summary>
        public Func<object, object> ValueGetter { get; }

        /// <summary>Creates a new <see cref="RequestParameterDescriptor"/>.</summary>
        public RequestParameterDescriptor(string name, RequestParameterType parameterType, bool isValueType, Func<object, object> valueGetter)
        {
            Name = name.ThrowIfNull(nameof(name));
            ParameterType = parameterType;
            IsValueType = isValueType;
            ValueGetter = valueGetter.ThrowIfNull(nameof(valueGetter));
        }
    }

    /// <summary>
    /// Registry of source-generated request-parameter descriptors, keyed by the concrete request type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Historically request parameters were discovered with reflection
    /// (<c>type.GetProperties()</c> filtered by <see cref="RequestParameterAttribute"/>; see
    /// <see cref="RequestParameterDescriptorProvider"/>). That is not trim-safe (IL2070): AOT trimming can drop
    /// request properties, silently losing query/path parameters. Instead, each generated client emits a flattened
    /// descriptor list per concrete request type (its own parameters plus those inherited from the client's common
    /// request base) and registers it here from a module initializer, so parameter discovery needs no reflection.
    /// </para>
    /// <para>
    /// This mirrors the JSON context registration performed by
    /// <see cref="Google.Apis.Json.SystemTextJsonSerializer.RegisterTypeInfoResolver"/>.
    /// </para>
    /// </remarks>
    public static class RequestParameterRegistry
    {
        private static readonly ConcurrentDictionary<Type, IReadOnlyList<RequestParameterDescriptor>> s_descriptors =
            new ConcurrentDictionary<Type, IReadOnlyList<RequestParameterDescriptor>>();

        /// <summary>
        /// Registers the (flattened) request-parameter descriptors for a concrete request type. Intended to be called
        /// once per request type, usually from a generated module initializer. Registration is idempotent and
        /// thread-safe.
        /// </summary>
        /// <param name="requestType">The concrete request type. Must not be null.</param>
        /// <param name="descriptors">The request's parameter descriptors. Must not be null.</param>
        public static void Register(Type requestType, IReadOnlyList<RequestParameterDescriptor> descriptors)
        {
            requestType.ThrowIfNull(nameof(requestType));
            descriptors.ThrowIfNull(nameof(descriptors));
            s_descriptors[NormalizeKey(requestType)] = descriptors;
        }

        /// <summary>Attempts to get the registered descriptors for a request type.</summary>
        /// <remarks>
        /// If the exact type has no registration, the base types are tried in turn. This lets a hand-written subclass
        /// of a registered (e.g. generated) request/upload type — such as a wrapper that adds no request parameters of
        /// its own — reuse the base type's descriptors; the descriptor value-getters cast to the base type, which is
        /// valid for a derived instance. <see cref="Type.BaseType"/> is trim/AOT-safe.
        /// </remarks>
        /// <returns><c>true</c> if descriptors were registered for the type or one of its base types; otherwise <c>false</c>.</returns>
        public static bool TryGet(Type requestType, out IReadOnlyList<RequestParameterDescriptor> descriptors)
        {
            for (Type type = requestType; type != null; type = type.BaseType)
            {
                if (s_descriptors.TryGetValue(NormalizeKey(type), out descriptors))
                {
                    return true;
                }
            }
            descriptors = null;
            return false;
        }

        // Generic request/upload types (e.g. a generic ResumableUpload subclass) are a distinct constructed type per
        // type argument; key by the generic type definition so any instantiation resolves to one registration. The
        // generated REST clients register only non-generic concrete request types, for which this is the identity.
        private static Type NormalizeKey(Type requestType) =>
            requestType.IsGenericType ? requestType.GetGenericTypeDefinition() : requestType;
    }
}
