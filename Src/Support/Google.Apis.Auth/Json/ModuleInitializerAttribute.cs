/*
Copyright 2025 Google Inc

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

#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Polyfill for the module-initializer attribute, which is recognized by the compiler but is not present in the
    /// netstandard2.0 reference assemblies. On net8.0+ the framework-provided type is used instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : Attribute
    {
    }
}
#endif
