﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Monitoring.TestCommon
{
    /// <summary>
    /// Runner for running dotnet processes.
    /// </summary>
    public sealed class DotNetRunner : IDisposable
    {
        // Event handler for the Process.Exited event
        private readonly EventHandler _exitedHandler;

        // Completion source that is signaled when the process exits
        private readonly TaskCompletionSource<object> _exitedSource;

        // The process object of the started process
        private readonly Process _process;

        /// <summary>
        /// The arguments to the entrypoint method.
        /// </summary>
        public string Arguments { get; set; }

        /// <summary>
        /// The path of the assembly containing the entrypoint method.
        /// </summary>
        public string EntrypointAssemblyPath { get; set; }

        /// <summary>
        /// Retrives the starting environment block of the process.
        /// </summary>
        public IDictionary<string, string> Environment => _process.StartInfo.Environment;

        /// <summary>
        /// Retrieves the exit code of the process.
        /// </summary>
        public int ExitCode => _process.ExitCode;

        /// <summary>
        /// Determines if the process has exited.
        /// </summary>
        public bool HasExited => _process.HasExited;

        /// <summary>
        /// Gets the process ID of the running process.
        /// </summary>
        public int ProcessId => _process.Id;
        
        /// <summary>
        /// Gets a <see cref="StreamReader"/> that reads stderr.
        /// </summary>
        public StreamReader StandardError => _process.StandardError;

        /// <summary>
        /// Gets a <see cref="StreamWriter"/> that writes to stdin.
        /// </summary>
        public StreamWriter StandardInput => _process.StandardInput;

        /// <summary>
        /// Gets a <see cref="StreamReader"/> that reads stdout.
        /// </summary>
        public StreamReader StandardOutput => _process.StandardOutput;

        /// <summary>
        /// Determines if <see cref="StartAsync(CancellationToken)" /> should wait for the diagnostic pipe to be available.
        /// </summary>
        public bool WaitForDiagnosticPipe { get; set; }

        public DotNetRunner()
        {
            _process = new Process();
            _process.StartInfo.FileName = DotNetHost.HostExePath;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.RedirectStandardInput = true;
            _process.StartInfo.RedirectStandardOutput = true;

            _exitedSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _exitedHandler = (s, e) => _exitedSource.SetResult(null);

            _process.EnableRaisingEvents = true;
            _process.Exited += _exitedHandler;
        }

        public void Dispose()
        {
            ForceClose();

            _process.Dispose();
        }

        /// <summary>
        /// Starts the dotnet process.
        /// </summary>
        public async Task StartAsync(CancellationToken token)
        {
            _process.StartInfo.Arguments = $"--fx-version {DotNetHost.CurrentRuntimeVersion} \"{EntrypointAssemblyPath}\" {Arguments}";

            if (!_process.Start())
            {
                throw new InvalidOperationException($"Unable to start: {_process.StartInfo.FileName} {_process.StartInfo.Arguments}");
            }

            if (WaitForDiagnosticPipe)
            {
                // On Windows, named pipe connection will block until the named pipe is ready to connect so no need to block here
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // On Unix, we wait until the socket is created.
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();

                        var matchingFiles = Directory.GetFiles(Path.GetTempPath(), $"dotnet-diagnostic-{_process.Id}-*-socket");
                        if (matchingFiles.Length > 0)
                        {
                            break;
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                    }
                }
            }
        }

        /// <summary>
        /// Waits for the process to exit.
        /// </summary>
        public async Task WaitForExitAsync(CancellationToken token)
        {
            if (!_process.HasExited)
            {
                TaskCompletionSource<object> cancellationSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
                using IDisposable _ = token.Register(() => cancellationSource.TrySetCanceled(token));

                await Task.WhenAny(
                    _exitedSource.Task,
                    cancellationSource.Task).Unwrap();
            }
        }

        /// <summary>
        /// Forces the process to exit.
        /// </summary>
        public void ForceClose()
        {
            if (!_process.HasExited)
            {
                _process.Kill();
            }
        }
    }
}
