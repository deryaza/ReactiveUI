// Copyright (c) 2009-2026 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.Versioning;
using System.Windows.Threading;
using TUnit.Core.Interfaces;

namespace ReactiveUI.Tests.Wpf;

/// <summary>Runs a test on an STA thread that owns a WPF dispatcher, as a WPF UI thread does.</summary>
/// <remarks>
/// <para>
/// <see cref="DispatcherSequencer.Main"/> never creates a dispatcher, so <c>WithWpf()</c> throws on a thread that has
/// none. A real application configures ReactiveUI once its <see cref="System.Windows.Application"/> exists; tests
/// have no application, so this executor gives the test thread its dispatcher before the test configures anything.
/// </para>
/// <para>
/// Derived executors configure global state in <see cref="SetUp"/> and restore it in <see cref="TearDown"/>, not in
/// <c>Initialize</c> and <c>CleanUp</c>. <see cref="DedicatedThreadExecutor"/> reports the test finished before it
/// calls <c>CleanUp</c>, so the next test would start while that cleanup still resets the app builder, the service
/// locator and the schedulers underneath it. <see cref="SetUp"/> and <see cref="TearDown"/> run inside the test's own
/// task, so the test only finishes once its teardown has.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public class DispatcherThreadExecutor : STAThreadExecutor, ITestExecutor
{
    /// <inheritdoc/>
    ValueTask ITestExecutor.ExecuteTest(TestContext context, Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteTest(context, () => RunTestAsync(action));
    }

    /// <summary>Prepares the test on its dedicated thread, before the test body runs.</summary>
    /// <remarks>The base implementation gives the thread its dispatcher. Call it before configuring anything.</remarks>
    protected virtual void SetUp() => _ = Dispatcher.CurrentDispatcher;

    /// <summary>Restores what <see cref="SetUp"/> changed, on the dedicated thread, before the test reports finished.</summary>
    protected virtual void TearDown()
    {
    }

    /// <summary>Runs the test body between <see cref="SetUp"/> and <see cref="TearDown"/>.</summary>
    /// <param name="action">The test body.</param>
    /// <returns>A task that completes once the test body and <see cref="TearDown"/> have both finished.</returns>
    private async ValueTask RunTestAsync(Func<ValueTask> action)
    {
        try
        {
            SetUp();

            // Resume on the dedicated thread, where TearDown has to run.
            await action();
        }
        finally
        {
            TearDown();
        }
    }
}
