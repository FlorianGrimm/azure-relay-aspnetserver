// Copyright (c) Microsoft Open Technologies, Inc.
// All Rights Reserved
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED *AS IS* BASIS, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING
// WITHOUT LIMITATION ANY IMPLIED WARRANTIES OR CONDITIONS OF
// TITLE, FITNESS FOR A PARTICULAR PURPOSE, MERCHANTABLITY OR
// NON-INFRINGEMENT.
// See the Apache 2 License for the specific language governing
// permissions and limitations under the License.

// -----------------------------------------------------------------------
// <copyright file="RequestUriBuilder.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Microsoft.Net.Server
{
    // We don't use the cooked URL because http.sys unescapes all percent-encoded values. However,
    // we also can't just use the raw Uri, since http.sys supports not only Utf-8, but also ANSI/DBCS and
    // Unicode code points. System.Uri only supports Utf-8.
    // The purpose of this class is to convert all ANSI, DBCS, and Unicode code points into percent encoded
    // Utf-8 characters.
    internal sealed class RequestUriBuilder
    {
        private static readonly bool UseCookedRequestUrl;
        private static readonly Encoding Utf8Encoding;
        private static readonly Encoding AnsiEncoding;

        private readonly string _rawUri;
        private readonly string _cookedUriScheme;
        private readonly string _cookedUriHost;
        private readonly string _cookedUriPath;
        private readonly string _cookedUriQuery;

        // This field is used to build the final request Uri string from the Uri parts passed to the ctor.
        private StringBuilder _requestUriString;

        // The raw path is parsed by looping through all characters from left to right. 'rawOctets'
        // is used to store consecutive percent encoded octets as actual byte values: e.g. for path /pa%C3%84th%2F/
        // rawOctets will be set to { 0xC3, 0x84 } when we reach character 't' and it will be { 0x2F } when
        // we reach the final '/'. I.e. after a sequence of percent encoded octets ends, we use rawOctets as 
        // input to the encoding and percent encode the resulting string into UTF-8 octets.
        //
        // When parsing ANSI (Latin 1) encoded path '/pa%C4th/', %C4 will be added to rawOctets and when
        // we reach 't', the content of rawOctets { 0xC4 } will be fed into the ANSI encoding. The resulting 
        // string '�' will be percent encoded into UTF-8 octets and appended to requestUriString. The final
        // path will be '/pa%C3%84th/', where '%C3%84' is the UTF-8 percent encoded character '�'.
        private List<byte> _rawOctets;
        private string _rawPath;

        // Holds the final request Uri.
        private Uri _requestUri;

        static RequestUriBuilder()
        {
            // TODO: False triggers more detailed/correct parsing, but it's rather slow.
            UseCookedRequestUrl = true; // SettingsSectionInternal.Section.HttpListenerUnescapeRequestUrl;
            Utf8Encoding = new UTF8Encoding(false, true);
#if NET45
            AnsiEncoding = Encoding.GetEncoding(0, new EncoderExceptionFallback(), new DecoderExceptionFallback());
#else
            AnsiEncoding = Utf8Encoding;
#endif
        }

        private RequestUriBuilder(string rawUri, string cookedUriScheme, string cookedUriHost,
            string cookedUriPath, string cookedUriQuery)
        {
            Debug.Assert(!string.IsNullOrEmpty(rawUri), "Empty raw URL.");
            Debug.Assert(!string.IsNullOrEmpty(cookedUriScheme), "Empty cooked URL scheme.");
            Debug.Assert(!string.IsNullOrEmpty(cookedUriHost), "Empty cooked URL host.");
            Debug.Assert(!string.IsNullOrEmpty(cookedUriPath), "Empty cooked URL path.");

            this._rawUri = rawUri;
            this._cookedUriScheme = cookedUriScheme;
            this._cookedUriHost = cookedUriHost;
            this._cookedUriPath = AddSlashToAsteriskOnlyPath(cookedUriPath);
            this._cookedUriQuery = cookedUriQuery ?? string.Empty;
        }

        private RequestUriBuilder(string rawUri, string cookedUriPath)
        {
            Debug.Assert(!string.IsNullOrEmpty(rawUri), "Empty raw URL.");
            Debug.Assert(!string.IsNullOrEmpty(cookedUriPath), "Empty cooked URL path.");

            this._rawUri = rawUri;
            this._cookedUriScheme = string.Empty;
            this._cookedUriHost = string.Empty;
            this._cookedUriPath = AddSlashToAsteriskOnlyPath(cookedUriPath);
            this._cookedUriQuery = string.Empty;
        }

        private enum ParsingResult
        {
            Success,
            InvalidString,
            EncodingError
        }

        private enum EncodingType
        {
            Primary,
            Secondary
        }

        public static Uri GetRequestUri(string rawUri, string cookedUriScheme, string cookedUriHost,
            string cookedUriPath, string cookedUriQuery)
        {
            RequestUriBuilder builder = new RequestUriBuilder(rawUri,
                cookedUriScheme, cookedUriHost, cookedUriPath, cookedUriQuery);

            return builder.Build();
        }

        private Uri Build()
        {
            // if the user enabled the "use raw Uri" setting in <httpListener> section, we'll use the raw
            // path rather than the cooked path.
            if (UseCookedRequestUrl)
            {
                // corresponds to pre-4.0 behavior: use the cooked URI.
                BuildRequestUriUsingCookedPath();

                if (_requestUri == null)
                {
                    BuildRequestUriUsingRawPath();
                }
            }
            else
            {
                BuildRequestUriUsingRawPath();

                if (_requestUri == null)
                {
                    BuildRequestUriUsingCookedPath();
                }
            }

            return _requestUri;
        }

        // Process only the path.
        internal static string GetRequestPath(string rawUri, string cookedUriPath)
        {
            RequestUriBuilder builder = new RequestUriBuilder(rawUri, cookedUriPath);

            return builder.GetPath();
        }

        private string GetPath()
        {
            if (UseCookedRequestUrl)
            {
                return _cookedUriPath;
            }

            // Initialize 'rawPath' only if really needed; i.e. if we build the request Uri from the raw Uri.
            _rawPath = GetPath(_rawUri);

            // If HTTP.sys only parses Utf-8, we can safely use the raw path: it must be a valid Utf-8 string.
            if (!HttpSysSettings.EnableNonUtf8 || string.IsNullOrEmpty(_rawPath))
            {
                if (string.IsNullOrEmpty(_rawPath))
                {
                    _rawPath = "/";
                }
                return _rawPath;
            }

            // Try to check the raw path using first the primary encoding (according to http.sys settings);
            // if it fails try the secondary encoding.
            _rawOctets = new List<byte>();
            _requestUriString = new StringBuilder();
            ParsingResult result = ParseRawPath(GetEncoding(EncodingType.Primary));
            if (result == ParsingResult.EncodingError)
            {
                _rawOctets = new List<byte>();
                _requestUriString = new StringBuilder();
                result = ParseRawPath(GetEncoding(EncodingType.Secondary));
            }

            if (result == ParsingResult.Success)
            {
                return _requestUriString.ToString();
            }

            // Fallback
            return _cookedUriPath;
        }

        private void BuildRequestUriUsingCookedPath()
        {
            bool isValid = Uri.TryCreate(_cookedUriScheme + Constants.SchemeDelimiter + _cookedUriHost + _cookedUriPath +
                _cookedUriQuery, UriKind.Absolute, out _requestUri);

            // Creating a Uri from the cooked Uri should really always work: If not, we log at least.
            if (!isValid)
            {
                LogWarning("BuildRequestUriUsingCookedPath", "Unable to create URI: " + _cookedUriScheme + Constants.SchemeDelimiter +
                    _cookedUriHost + _cookedUriPath + _cookedUriQuery);
            }
        }

        private void BuildRequestUriUsingRawPath()
        {
            bool isValid = false;

            // Initialize 'rawPath' only if really needed; i.e. if we build the request Uri from the raw Uri.
            _rawPath = GetPath(_rawUri);

            // If HTTP.sys only parses Utf-8, we can safely use the raw path: it must be a valid Utf-8 string.
            if (!HttpSysSettings.EnableNonUtf8 || string.IsNullOrEmpty(_rawPath))
            {
                string path = _rawPath;
                if (string.IsNullOrEmpty(path))
                {
                    path = "/";
                    Debug.Assert(string.IsNullOrEmpty(_cookedUriQuery),
                        "Query is only allowed if there is a non-empty path. At least '/' path required.");
                }

                isValid = Uri.TryCreate(_cookedUriScheme + Constants.SchemeDelimiter + _cookedUriHost + path + _cookedUriQuery,
                    UriKind.Absolute, out _requestUri);
            }
            else
            {
                // Try to check the raw path using first the primary encoding (according to http.sys settings);
                // if it fails try the secondary encoding.
                ParsingResult result = BuildRequestUriUsingRawPath(GetEncoding(EncodingType.Primary));
                if (result == ParsingResult.EncodingError)
                {
                    Encoding secondaryEncoding = GetEncoding(EncodingType.Secondary);
                    result = BuildRequestUriUsingRawPath(secondaryEncoding);
                }
                isValid = (result == ParsingResult.Success) ? true : false;
            }

            // Log that we weren't able to create a Uri from the raw string.
            if (!isValid)
            {
                LogWarning("BuildRequestUriUsingRawPath", "Unable to create Uri: " + _cookedUriScheme + Constants.SchemeDelimiter
                    + _cookedUriHost + _rawPath + _cookedUriQuery);
            }
        }

        private static Encoding GetEncoding(EncodingType type)
        {
            Debug.Assert(HttpSysSettings.EnableNonUtf8,
                "If 'EnableNonUtf8' is false we shouldn't require an encoding. It's always Utf-8.");
            /* This is mucking up the profiler for some reason.
            Debug.Assert((type == EncodingType.Primary) || (type == EncodingType.Secondary),
                "Unknown 'EncodingType' value: " + type.ToString());
            */
            if (((type == EncodingType.Primary) && (!HttpSysSettings.FavorUtf8)) ||
                ((type == EncodingType.Secondary) && (HttpSysSettings.FavorUtf8)))
            {
                return AnsiEncoding;
            }
            else
            {
                return Utf8Encoding;
            }
        }

        private ParsingResult BuildRequestUriUsingRawPath(Encoding encoding)
        {
            Debug.Assert(encoding != null, "'encoding' must be assigned.");
            Debug.Assert(!string.IsNullOrEmpty(_rawPath), "'rawPath' must have at least one character.");

            _rawOctets = new List<byte>();
            _requestUriString = new StringBuilder();
            _requestUriString.Append(_cookedUriScheme);
            _requestUriString.Append(Constants.SchemeDelimiter);
            _requestUriString.Append(_cookedUriHost);

            ParsingResult result = ParseRawPath(encoding);
            if (result == ParsingResult.Success)
            {
                _requestUriString.Append(_cookedUriQuery);

                Debug.Assert(_rawOctets.Count == 0,
                    "Still raw octets left. They must be added to the result path.");

                if (!Uri.TryCreate(_requestUriString.ToString(), UriKind.Absolute, out _requestUri))
                {
                    // If we can't create a Uri from the string, this is an invalid string and it doesn't make 
                    // sense to try another encoding.
                    result = ParsingResult.InvalidString;
                }
            }

            if (result != ParsingResult.Success)
            {
                LogWarning("BuildRequestUriUsingRawPath", "Can't convert the raw path: " + _rawPath + " Encoding: " + encoding.WebName);
            }

            return result;
        }

        private ParsingResult ParseRawPath(Encoding encoding)
        {
            Debug.Assert(encoding != null, "'encoding' must be assigned.");

            int index = 0;
            char current = '\0';
            while (index < _rawPath.Length)
            {
                current = _rawPath[index];
                if (current == '%')
                {
                    // Assert is enough, since http.sys accepted the request string already. This should never happen.
                    Debug.Assert(index + 2 < _rawPath.Length, "Expected >=2 characters after '%' (e.g. %2F)");

                    index++;
                    current = _rawPath[index];
                    if (current == 'u' || current == 'U')
                    {
                        // We found "%u" which means, we have a Unicode code point of the form "%uXXXX".
                        Debug.Assert(index + 4 < _rawPath.Length, "Expected >=4 characters after '%u' (e.g. %u0062)");

                        // Decode the content of rawOctets into percent encoded UTF-8 characters and append them
                        // to requestUriString.
                        if (!EmptyDecodeAndAppendRawOctetsList(encoding))
                        {
                            return ParsingResult.EncodingError;
                        }
                        if (!AppendUnicodeCodePointValuePercentEncoded(_rawPath.Substring(index + 1, 4)))
                        {
                            return ParsingResult.InvalidString;
                        }
                        index += 5;
                    }
                    else
                    {
                        // We found '%', but not followed by 'u', i.e. we have a percent encoded octed: %XX 
                        if (!AddPercentEncodedOctetToRawOctetsList(encoding, _rawPath.Substring(index, 2)))
                        {
                            return ParsingResult.InvalidString;
                        }
                        index += 2;
                    }
                }
                else
                {
                    // We found a non-'%' character: decode the content of rawOctets into percent encoded
                    // UTF-8 characters and append it to the result. 
                    if (!EmptyDecodeAndAppendRawOctetsList(encoding))
                    {
                        return ParsingResult.EncodingError;
                    }
                    // Append the current character to the result.
                    _requestUriString.Append(current);
                    index++;
                }
            }

            // if the raw path ends with a sequence of percent encoded octets, make sure those get added to the
            // result (requestUriString).
            if (!EmptyDecodeAndAppendRawOctetsList(encoding))
            {
                return ParsingResult.EncodingError;
            }

            return ParsingResult.Success;
        }

        private bool AppendUnicodeCodePointValuePercentEncoded(string codePoint)
        {
            // http.sys only supports %uXXXX (4 hex-digits), even though unicode code points could have up to
            // 6 hex digits. Therefore we parse always 4 characters after %u and convert them to an int.
            int codePointValue;
            if (!int.TryParse(codePoint, NumberStyles.HexNumber, null, out codePointValue))
            {
                LogWarning("AppendUnicodeCodePointValuePercentEncoded", "Can't convert code point: " + codePoint);
                return false;
            }

            string unicodeString = null;
            try
            {
                unicodeString = char.ConvertFromUtf32(codePointValue);
                AppendOctetsPercentEncoded(_requestUriString, Utf8Encoding.GetBytes(unicodeString));

                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                LogWarning("AppendUnicodeCodePointValuePercentEncoded", "Can't convert code point: " + codePoint);
            }
            catch (EncoderFallbackException e)
            {
                // If utf8Encoding.GetBytes() fails
                LogWarning("AppendUnicodeCodePointValuePercentEncoded", "Can't convert code point: " + unicodeString, e.Message);
            }

            return false;
        }

        private bool AddPercentEncodedOctetToRawOctetsList(Encoding encoding, string escapedCharacter)
        {
            byte encodedValue;
            if (!byte.TryParse(escapedCharacter, NumberStyles.HexNumber, null, out encodedValue))
            {
                LogWarning("AddPercentEncodedOctetToRawOctetsList", "Can't convert code point: " + escapedCharacter);
                return false;
            }

            _rawOctets.Add(encodedValue);

            return true;
        }

        private bool EmptyDecodeAndAppendRawOctetsList(Encoding encoding)
        {
            if (_rawOctets.Count == 0)
            {
                return true;
            }

            string decodedString = null;
            try
            {
                // If the encoding can get a string out of the byte array, this is a valid string in the
                // 'encoding' encoding.
                byte[] bytes = _rawOctets.ToArray();
                decodedString = encoding.GetString(bytes, 0, bytes.Length);

                if (encoding == Utf8Encoding)
                {
                    AppendOctetsPercentEncoded(_requestUriString, bytes);
                }
                else
                {
                    AppendOctetsPercentEncoded(_requestUriString, Utf8Encoding.GetBytes(decodedString));
                }

                _rawOctets.Clear();

                return true;
            }
            catch (DecoderFallbackException e)
            {
                LogWarning("EmptyDecodeAndAppendRawOctetsList", "Can't convert bytes: " + GetOctetsAsString(_rawOctets), e.Message);
            }
            catch (EncoderFallbackException e)
            {
                // If utf8Encoding.GetBytes() fails
                LogWarning("EmptyDecodeAndAppendRawOctetsList", "Can't convert bytes: " + decodedString, e.Message);
            }

            return false;
        }

        private static void AppendOctetsPercentEncoded(StringBuilder target, IEnumerable<byte> octets)
        {
            foreach (byte octet in octets)
            {
                target.Append('%');
                target.Append(octet.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        private static string GetOctetsAsString(IEnumerable<byte> octets)
        {
            StringBuilder octetString = new StringBuilder();

            bool first = true;
            foreach (byte octet in octets)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    octetString.Append(" ");
                }
                octetString.Append(octet.ToString("X2", CultureInfo.InvariantCulture));
            }

            return octetString.ToString();
        }

        private static string GetPath(string uriString)
        {
            Debug.Assert(uriString != null, "uriString must not be null");
            Debug.Assert(uriString.Length > 0, "uriString must not be empty");

            int pathStartIndex = 0;

            // Perf. improvement: nearly all strings are relative Uris. So just look if the
            // string starts with '/'. If so, we have a relative Uri and the path starts at position 0.
            // (http.sys already trimmed leading whitespaces)
            if (uriString[0] != '/')
            {
                // We can't check against cookedUriScheme, since http.sys allows for request http://myserver/ to
                // use a request line 'GET https://myserver/' (note http vs. https). Therefore check if the
                // Uri starts with either http:// or https://.
                int authorityStartIndex = 0;
                if (uriString.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    authorityStartIndex = 7;
                }
                else if (uriString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    authorityStartIndex = 8;
                }

                if (authorityStartIndex > 0)
                {
                    // we have an absolute Uri. Find out where the authority ends and the path begins.
                    // Note that Uris like "http://server?query=value/1/2" are invalid according to RFC2616
                    // and http.sys behavior: If the Uri contains a query, there must be at least one '/'
                    // between the authority and the '?' character: It's safe to just look for the first
                    // '/' after the authority to determine the beginning of the path.
                    pathStartIndex = uriString.IndexOf('/', authorityStartIndex);
                    if (pathStartIndex == -1)
                    {
                        // e.g. for request lines like: 'GET http://myserver' (no final '/')
                        pathStartIndex = uriString.Length;
                    }
                }
                else
                {
                    // RFC2616: Request-URI = "*" | absoluteURI | abs_path | authority
                    // 'authority' can only be used with CONNECT which is never received by HttpListener.
                    // I.e. if we don't have an absolute path (must start with '/') and we don't have
                    // an absolute Uri (must start with http:// or https://), then 'uriString' must be '*'.
                    Debug.Assert((uriString.Length == 1) && (uriString[0] == '*'), "Unknown request Uri string format; "
                        + "Request Uri string is not an absolute Uri, absolute path, or '*': " + uriString);

                    // Should we ever get here, be consistent with 2.0/3.5 behavior: just add an initial
                    // slash to the string and treat it as a path:
                    uriString = "/" + uriString;
                }
            }

            // Find end of path: The path is terminated by
            // - the first '?' character
            // - the first '#' character: This is never the case here, since http.sys won't accept 
            //   Uris containing fragments. Also, RFC2616 doesn't allow fragments in request Uris.
            // - end of Uri string
            int queryIndex = uriString.IndexOf('?');
            if (queryIndex == -1)
            {
                queryIndex = uriString.Length;
            }

            // will always return a != null string.
            return AddSlashToAsteriskOnlyPath(uriString.Substring(pathStartIndex, queryIndex - pathStartIndex));
        }

        private static string AddSlashToAsteriskOnlyPath(string path)
        {
            Debug.Assert(path != null, "'path' must not be null");

            // If a request like "OPTIONS * HTTP/1.1" is sent to the listener, then the request Uri
            // should be "http[s]://server[:port]/*" to be compatible with pre-4.0 behavior.
            if ((path.Length == 1) && (path[0] == '*'))
            {
                return "/*";
            }

            return path;
        }

        private void LogWarning(string methodName, string message, params object[] args)
        {
            // TODO: Verbose log
        }
    }
}