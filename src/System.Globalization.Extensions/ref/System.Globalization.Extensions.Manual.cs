// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// ------------------------------------------------------------------------------
// Changes to this file must follow the http://aka.ms/api-review process.
// ------------------------------------------------------------------------------

//
//

namespace System
{
    using System.Text; // for NormalizationForm type

    public static class StringNormalizationExtensions
    {
        public static bool IsNormalized(this string value) { return default(bool); }
        [System.Security.SecurityCritical]
        public static bool IsNormalized(this string value, NormalizationForm normalizationForm) { return default(bool); }
        public static String Normalize(this string value) { return default(string); }
        [System.Security.SecurityCritical]
        public static String Normalize(this string value, NormalizationForm normalizationForm) { return default(string); }
    }
}

namespace System.Globalization
{
    public static partial class GlobalizationExtensions
    {
        public static StringComparer GetStringComparer(this CompareInfo compareInfo, CompareOptions options) { return default(StringComparer); }
    }
}
