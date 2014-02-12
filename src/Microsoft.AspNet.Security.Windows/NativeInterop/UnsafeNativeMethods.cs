//------------------------------------------------------------------------------
// <copyright file="UnsafeNativeMethods.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Security;

namespace Microsoft.AspNet.Security.Windows
{
    [System.Security.SuppressUnmanagedCodeSecurityAttribute]
    internal static class UnsafeNclNativeMethods
    {
        private const string KERNEL32 = "kernel32.dll";
        private const string SECUR32 = "secur32.dll";
        private const string CRYPT32 = "crypt32.dll";

        [DllImport(KERNEL32, ExactSpelling = true, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        internal static extern uint GetCurrentThreadId();

        [System.Security.SuppressUnmanagedCodeSecurityAttribute]
        internal static class SafeNetHandles
        {
            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern int FreeContextBuffer(
                [In] IntPtr contextBuffer);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern int FreeCredentialsHandle(
                  ref SSPIHandle handlePtr);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern int DeleteSecurityContext(
                  ref SSPIHandle handlePtr);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static unsafe extern int AcceptSecurityContext(
                      ref SSPIHandle credentialHandle,
                      [In] void* inContextPtr,
                      [In] SecurityBufferDescriptor inputBuffer,
                      [In] ContextFlags inFlags,
                      [In] Endianness endianness,
                      ref SSPIHandle outContextPtr,
                      [In, Out] SecurityBufferDescriptor outputBuffer,
                      [In, Out] ref ContextFlags attributes,
                      out long timeStamp);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static unsafe extern int QueryContextAttributesW(
                ref SSPIHandle contextHandle,
                [In] ContextAttribute attribute,
                [In] void* buffer);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static unsafe extern int SetContextAttributesW(
                ref SSPIHandle contextHandle,
                [In] ContextAttribute attribute,
                [In] byte[] buffer,
                [In] int bufferSize);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            internal static extern int EnumerateSecurityPackagesW(
                [Out] out int pkgnum,
                [Out] out SafeFreeContextBuffer handle);

            [DllImport(SECUR32, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
            internal static unsafe extern int AcquireCredentialsHandleW(
                      [In] string principal,
                      [In] string moduleName,
                      [In] int usage,
                      [In] void* logonID,
                      [In] ref AuthIdentity authdata,
                      [In] void* keyCallback,
                      [In] void* keyArgument,
                      ref SSPIHandle handlePtr,
                      [Out] out long timeStamp);

            [DllImport(SECUR32, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
            internal static unsafe extern int AcquireCredentialsHandleW(
                      [In] string principal,
                      [In] string moduleName,
                      [In] int usage,
                      [In] void* logonID,
                      [In] IntPtr zero,
                      [In] void* keyCallback,
                      [In] void* keyArgument,
                      ref SSPIHandle handlePtr,
                      [Out] out long timeStamp);

            // Win7+
            [DllImport(SECUR32, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
            internal static unsafe extern int AcquireCredentialsHandleW(
                      [In] string principal,
                      [In] string moduleName,
                      [In] int usage,
                      [In] void* logonID,
                      [In] SafeSspiAuthDataHandle authdata,
                      [In] void* keyCallback,
                      [In] void* keyArgument,
                      ref SSPIHandle handlePtr,
                      [Out] out long timeStamp);

            [DllImport(SECUR32, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
            internal static unsafe extern int AcquireCredentialsHandleW(
                      [In] string principal,
                      [In] string moduleName,
                      [In] int usage,
                      [In] void* logonID,
                      [In] ref SecureCredential authData,
                      [In] void* keyCallback,
                      [In] void* keyArgument,
                      ref SSPIHandle handlePtr,
                      [Out] out long timeStamp);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static unsafe extern int InitializeSecurityContextW(
                      ref SSPIHandle credentialHandle,
                      [In] void* inContextPtr,
                      [In] byte* targetName,
                      [In] ContextFlags inFlags,
                      [In] int reservedI,
                      [In] Endianness endianness,
                      [In] SecurityBufferDescriptor inputBuffer,
                      [In] int reservedII,
                      ref SSPIHandle outContextPtr,
                      [In, Out] SecurityBufferDescriptor outputBuffer,
                      [In, Out] ref ContextFlags attributes,
                      out long timeStamp);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static unsafe extern int CompleteAuthToken(
                      [In] void* inContextPtr,
                      [In, Out] SecurityBufferDescriptor inputBuffers);

            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            internal static extern int QuerySecurityContextToken(ref SSPIHandle phContext, [Out] out SafeCloseHandle handle);

            [DllImport(KERNEL32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern bool CloseHandle(IntPtr handle);

            [DllImport(KERNEL32, ExactSpelling = true, SetLastError = true)]
            internal static extern SafeLocalFree LocalAlloc(int uFlags, UIntPtr sizetdwBytes);

            [DllImport(KERNEL32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern IntPtr LocalFree(IntPtr handle);

            [DllImport(KERNEL32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern unsafe bool FreeLibrary([In] IntPtr hModule);

            [DllImport(CRYPT32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern void CertFreeCertificateChain(
                [In] IntPtr pChainContext);

            [DllImport(CRYPT32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern void CertFreeCertificateChainList(
                [In] IntPtr ppChainContext);

            [DllImport(CRYPT32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern bool CertFreeCertificateContext(      // Suppressing returned status check, it's always==TRUE,
                [In] IntPtr certContext);

            [DllImport(KERNEL32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
            internal static extern IntPtr GlobalFree(IntPtr handle);
        }

        [System.Security.SuppressUnmanagedCodeSecurityAttribute]
        internal static class NativeNTSSPI
        {
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            internal static extern int EncryptMessage(
                  ref SSPIHandle contextHandle,
                  [In] uint qualityOfProtection,
                  [In, Out] SecurityBufferDescriptor inputOutput,
                  [In] uint sequenceNumber);

            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            internal static unsafe extern int DecryptMessage(
                  [In] ref SSPIHandle contextHandle,
                  [In, Out] SecurityBufferDescriptor inputOutput,
                  [In] uint sequenceNumber,
                       uint* qualityOfProtection);
        } // class UnsafeNclNativeMethods.NativeNTSSPI

        [SuppressUnmanagedCodeSecurityAttribute]
        internal static class SspiHelper
        {
            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true)]
            [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
            internal static unsafe extern SecurityStatus SspiFreeAuthIdentity(
                [In] IntPtr authData);

            [SuppressMessage("Microsoft.Security", "CA2118:ReviewSuppressUnmanagedCodeSecurityUsage", Justification = "Implementation requires unmanaged code usage")]
            [DllImport(SECUR32, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
            internal static unsafe extern SecurityStatus SspiEncodeStringsAsAuthIdentity(
                [In] string userName,
                [In] string domainName,
                [In] string password,
                [Out] out SafeSspiAuthDataHandle authData);
        }
    }
}