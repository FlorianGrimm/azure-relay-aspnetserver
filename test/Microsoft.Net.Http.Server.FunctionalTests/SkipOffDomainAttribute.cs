// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.DirectoryServices.ActiveDirectory;
using Microsoft.AspNet.Testing.xunit;

namespace Microsoft.Net.Http.Server
{
    /// <summary>
    /// Skips an auth test if the machine is not joined to a Windows domain.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SkipOffDomainAttribute : Attribute, ITestCondition
    {
        public bool IsMet
        {
            get
            {
                try
                {
                    return !string.IsNullOrEmpty(Domain.GetComputerDomain().Name);
                }
                catch
                {
                }
                return false;
            }
        }

        public string SkipReason
        {
            get
            {
                return "Machine is not joined to a domain.";
            }
        }
    }
}