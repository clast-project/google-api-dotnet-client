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

using Google.Apis.Requests;
using Google.Apis.Upload;
using Google.Apis.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Google.Apis.Tests
{
    /// <summary>
    /// Registers request-parameter descriptors and enum wire-string maps for the test-only request types and enums,
    /// so the tests exercise the same registry-driven path that generated clients use, including on the AOT target
    /// where reflective discovery in Google.Apis.Core is unavailable.
    /// </summary>
    /// <remarks>
    /// Unlike the shipped libraries, the test assembly is not AOT/trim-constrained, so it may use reflection to build
    /// the registrations generically (covering private nested mock types). The descriptor order matches
    /// <c>Type.GetProperties</c> (own/derived members first, then inherited), which is what the previous reflective
    /// discovery produced and what some URI/form-ordering assertions depend on.
    /// </remarks>
    internal static class TestRequestParameterRegistration
    {
#pragma warning disable CA2255 // Module initializer: the test assembly self-registers its parameter/enum metadata.
        [ModuleInitializer]
        internal static void Initialize()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!type.IsEnum)
                {
                    RegisterRequestParametersIfNeeded(type);
                }
            }
            // Note: test enums are deliberately NOT registered here. Tests that assert the reflective
            // GetEnumStringValue behavior (e.g. UtilitiesTest.StringValueTest) rely on the non-registry path on
            // non-AOT targets; generated clients register their own enums, and the AOT enum path is covered by the
            // transformed-client round-trip tests and the native-AOT proof.
        }
#pragma warning restore CA2255

        private static void RegisterRequestParametersIfNeeded(Type type)
        {
            if (type.IsAbstract)
            {
                return;
            }
            var descriptors = new List<RequestParameterDescriptor>();
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var attribute = prop.GetCustomAttribute<RequestParameterAttribute>(inherit: false);
                if (attribute is null)
                {
                    continue;
                }
                string name = attribute.Name ?? prop.Name.ToLowerInvariant();
                var local = prop;
                descriptors.Add(new RequestParameterDescriptor(
                    name, attribute.Type, prop.PropertyType.IsValueType, obj => local.GetValue(obj, null)));
            }
            // Register if the type carries request parameters, or is a request/upload type that flows through the
            // parameter pipeline (even with no parameters of its own) — generated clients register such types with
            // an empty descriptor list, and the AOT path treats an unregistered request type as an error.
            bool isRequestType = typeof(ClientServiceRequest).IsAssignableFrom(type) || typeof(ResumableUpload).IsAssignableFrom(type);
            if (descriptors.Count > 0 || isRequestType)
            {
                RequestParameterRegistry.Register(type, descriptors);
            }
        }
    }
}
