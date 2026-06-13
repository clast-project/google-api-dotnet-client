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
using System;
using System.Runtime.CompilerServices;

namespace Google.Apis.Auth.OAuth2.Requests
{
    /// <summary>
    /// Registers source-generated request-parameter descriptors for the hand-written OAuth request types, so that
    /// request-parameter discovery needs no reflection and is therefore AOT/trim-safe. This mirrors what the Clast
    /// transform emits for generated REST clients (see <see cref="RequestParameterRegistry"/>).
    /// </summary>
    /// <remarks>
    /// Descriptors are listed in the same order the previous reflective discovery produced
    /// (<c>Type.GetProperties</c>): a type's own declared parameters first, then those inherited from its base
    /// types. Some tests assert the exact resulting query/form ordering, so this order is significant.
    /// </remarks>
    internal static class AuthRequestParameterRegistration
    {
        private static RequestParameterDescriptor Query(string name, Func<object, object> valueGetter) =>
            new RequestParameterDescriptor(name, RequestParameterType.Query, isValueType: false, valueGetter);

        private static RequestParameterDescriptor UserQueries(string name, Func<object, object> valueGetter) =>
            new RequestParameterDescriptor(name, RequestParameterType.UserDefinedQueries, isValueType: false, valueGetter);

#pragma warning disable CA2255 // The ModuleInitializer attribute is intentional: this type self-registers its parameters.
        [ModuleInitializer]
        internal static void Initialize()
        {
            // --- TokenRequest hierarchy (form-encoded; RequestParameterAttribute defaults to Query type). ---

            RequestParameterRegistry.Register(typeof(TokenRequest), new[]
            {
                Query("scope", r => ((TokenRequest)r).Scope),
                Query("grant_type", r => ((TokenRequest)r).GrantType),
                Query("client_id", r => ((TokenRequest)r).ClientId),
                Query("client_secret", r => ((TokenRequest)r).ClientSecret),
            });

            RequestParameterRegistry.Register(typeof(AuthorizationCodeTokenRequest), new[]
            {
                Query("code", r => ((AuthorizationCodeTokenRequest)r).Code),
                Query("redirect_uri", r => ((AuthorizationCodeTokenRequest)r).RedirectUri),
                Query("code_verifier", r => ((AuthorizationCodeTokenRequest)r).CodeVerifier),
                Query("scope", r => ((AuthorizationCodeTokenRequest)r).Scope),
                Query("grant_type", r => ((AuthorizationCodeTokenRequest)r).GrantType),
                Query("client_id", r => ((AuthorizationCodeTokenRequest)r).ClientId),
                Query("client_secret", r => ((AuthorizationCodeTokenRequest)r).ClientSecret),
            });

            RequestParameterRegistry.Register(typeof(GoogleAssertionTokenRequest), new[]
            {
                Query("assertion", r => ((GoogleAssertionTokenRequest)r).Assertion),
                Query("scope", r => ((GoogleAssertionTokenRequest)r).Scope),
                Query("grant_type", r => ((GoogleAssertionTokenRequest)r).GrantType),
                Query("client_id", r => ((GoogleAssertionTokenRequest)r).ClientId),
                Query("client_secret", r => ((GoogleAssertionTokenRequest)r).ClientSecret),
            });

            RequestParameterRegistry.Register(typeof(RefreshTokenRequest), new[]
            {
                Query("refresh_token", r => ((RefreshTokenRequest)r).RefreshToken),
                Query("scope", r => ((RefreshTokenRequest)r).Scope),
                Query("grant_type", r => ((RefreshTokenRequest)r).GrantType),
                Query("client_id", r => ((RefreshTokenRequest)r).ClientId),
                Query("client_secret", r => ((RefreshTokenRequest)r).ClientSecret),
            });

            // --- AuthorizationRequestUrl hierarchy (query string). ---

            RequestParameterRegistry.Register(typeof(AuthorizationRequestUrl), new[]
            {
                Query("response_type", r => ((AuthorizationRequestUrl)r).ResponseType),
                Query("client_id", r => ((AuthorizationRequestUrl)r).ClientId),
                Query("redirect_uri", r => ((AuthorizationRequestUrl)r).RedirectUri),
                Query("scope", r => ((AuthorizationRequestUrl)r).Scope),
                Query("state", r => ((AuthorizationRequestUrl)r).State),
            });

            // AuthorizationCodeRequestUrl adds no parameters of its own.
            RequestParameterRegistry.Register(typeof(AuthorizationCodeRequestUrl), new[]
            {
                Query("response_type", r => ((AuthorizationCodeRequestUrl)r).ResponseType),
                Query("client_id", r => ((AuthorizationCodeRequestUrl)r).ClientId),
                Query("redirect_uri", r => ((AuthorizationCodeRequestUrl)r).RedirectUri),
                Query("scope", r => ((AuthorizationCodeRequestUrl)r).Scope),
                Query("state", r => ((AuthorizationCodeRequestUrl)r).State),
            });

#pragma warning disable CS0618 // ApprovalPrompt is obsolete but is still a request parameter; reading it here mirrors the old reflective behavior.
            RequestParameterRegistry.Register(typeof(GoogleAuthorizationCodeRequestUrl), new[]
            {
                Query("access_type", r => ((GoogleAuthorizationCodeRequestUrl)r).AccessType),
                Query("prompt", r => ((GoogleAuthorizationCodeRequestUrl)r).Prompt),
                Query("approval_prompt", r => ((GoogleAuthorizationCodeRequestUrl)r).ApprovalPrompt),
                Query("login_hint", r => ((GoogleAuthorizationCodeRequestUrl)r).LoginHint),
                Query("include_granted_scopes", r => ((GoogleAuthorizationCodeRequestUrl)r).IncludeGrantedScopes),
                Query("nonce", r => ((GoogleAuthorizationCodeRequestUrl)r).Nonce),
                Query("code_challenge", r => ((GoogleAuthorizationCodeRequestUrl)r).CodeChallenge),
                Query("code_challenge_method", r => ((GoogleAuthorizationCodeRequestUrl)r).CodeChallengeMethod),
                UserQueries("user_defined_query_params", r => ((GoogleAuthorizationCodeRequestUrl)r).UserDefinedQueryParams),
                Query("response_type", r => ((GoogleAuthorizationCodeRequestUrl)r).ResponseType),
                Query("client_id", r => ((GoogleAuthorizationCodeRequestUrl)r).ClientId),
                Query("redirect_uri", r => ((GoogleAuthorizationCodeRequestUrl)r).RedirectUri),
                Query("scope", r => ((GoogleAuthorizationCodeRequestUrl)r).Scope),
                Query("state", r => ((GoogleAuthorizationCodeRequestUrl)r).State),
            });
#pragma warning restore CS0618

            // --- Other standalone request types. ---

            RequestParameterRegistry.Register(typeof(GoogleRevokeTokenRequest), new[]
            {
                Query("token", r => ((GoogleRevokeTokenRequest)r).Token),
            });

            RequestParameterRegistry.Register(typeof(StsTokenRequest), new[]
            {
                Query("grant_type", r => ((StsTokenRequest)r).GrantType),
                Query("audience", r => ((StsTokenRequest)r).Audience),
                Query("scope", r => ((StsTokenRequest)r).Scope),
                Query("requested_token_type", r => ((StsTokenRequest)r).RequestedTokenType),
                Query("subject_token", r => ((StsTokenRequest)r).SubjectToken),
                Query("subject_token_type", r => ((StsTokenRequest)r).SubjectTokenType),
                Query("options", r => ((StsTokenRequest)r).GoogleOptions),
            });
        }
#pragma warning restore CA2255
    }
}
