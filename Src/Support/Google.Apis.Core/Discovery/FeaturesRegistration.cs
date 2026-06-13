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

using Google.Apis.Util;
using System.Runtime.CompilerServices;

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : System.Attribute { }
}
#endif

namespace Google.Apis.Discovery
{
    /// <summary>
    /// Registers the source-generated (reflection-free) wire-string map for the <see cref="Features"/> enum, which is
    /// the only <see cref="StringValueAttribute"/>-carrying enum defined in this library. Registering it from a module
    /// initializer keeps <see cref="Utilities.GetEnumStringValue"/> / <see cref="Utilities.ConvertToString(object)"/>
    /// trim-safe on AOT targets (no reflective field/attribute lookup).
    /// </summary>
    internal static class FeaturesRegistration
    {
#pragma warning disable CA2255 // Module initializer: this library self-registers its enum metadata.
        [ModuleInitializer]
        internal static void Initialize()
        {
            EnumStringValueRegistry.Register(typeof(Features), value =>
            {
                switch (value.ToString())
                {
                    case nameof(Features.LegacyDataResponse): return "dataWrapper";
                    default: return value.ToString();
                }
            });
        }
#pragma warning restore CA2255
    }
}
