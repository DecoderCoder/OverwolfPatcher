# Compatibility and repair attempt log

This log records each material experiment made while repairing the patcher. It
is based on the current installed state observed on 2026-09-08. Inputs and
installed files are kept recoverable; private logs and proprietary binaries
remain under the ignored `artifacts` directory.

## 2026-09-08

### Attempt 1: scoped Mono.Cecil Core rewrite

- Tried: rewrite only `GetExtensionSubscriptions` and
  `GetExtensionSubscriptionsIds` in the installed Core shape, scoped to the
  Outplayed extension ID and legacy plan 61.
- Result: offline staging and synthetic execution passed.
- Failure: the rewrite removed the Overwolf Authenticode signature. Overwolf
  `0.309.0.14` rejected the installed candidate before Outplayed startup with
  `Refusing to start - deployed assembly failed verification`.
- Evidence: `docs/compatibility-investigation-report.md`,
  `docs/compatibility-test-battery.md`, and
  `artifacts/diagnostics/failed-scoped-test-20260908`.
- Recovery: the clean Core hash was restored and verified as
  `9DA15E0CACF446E59B3F728BA78F5CC8F0EC6D6616BB0F490BF90ABD21EF098E`.

### Attempt 2: no-op rewrite signature control

- Tried: Mono.Cecil read/write round-trip without semantic changes.
- Result: method bodies compared equal across the inspected assembly.
- Failure: the resulting file still failed the exact embedded WinTrust
  predicate used by the launcher, proving that reducing the IL change cannot
  preserve the publisher signature.
- Recovery: no installed file was changed.

### Attempt 3: alternate `Locales` probing path

- Tried: on a disposable full installation clone, put the candidate Core under
  `0.309.0.14\Locales` and remove the active-version Core so the existing
  `Overwolf.exe.config` probing order would select the candidate.
- Result: the clone process exited with code `-2146232797` before a usable
  application session or fresh app log was produced.
- Failure: the candidate did not provide a working loader path. The live
  installation was never modified because the sandbox account could not write
  `Program Files`.
- Recovery: the clone was restored to its clean Core layout.

### Attempt 4: process-start in-memory runtime redirect (initial test)

- Tried: load a custom .NET Framework `AppDomainManager` before `Main`, then
  redirect the two subscription methods to generated in-memory methods while
  retaining a fallback delegate for other app IDs.
- Result: the detailed-plan method redirected in the synthetic test.
- Failure: the synthetic ID method was small enough for its caller to inline
  before the runtime redirect, so that call returned the original plan IDs.
  This is a test-order/inlining failure, not evidence that the early-load path
  fails for the real complex Core methods.
- Recovery: no installed files or processes were changed. The test will mark
  fixture methods `NoInlining` and verify the AppDomainManager child path.

Further attempts will be appended here with their exact result and recovery
status.

### Attempt 5: managed `MethodInfo` fallback delegate

- Tried: retain the original method for other app IDs with an open delegate
  created directly from the target `MethodInfo` before redirecting its method
  table entry.
- Result: both target methods could be redirected when the synthetic methods
  were marked `NoInlining`.
- Failure: the saved delegate followed the redirected `MethodInfo` entry
  instead of retaining the old implementation. The nonmatching-app fallback
  recursively called the replacement and terminated the test process with a
  `StackOverflowException`.
- Recovery: no installed files were changed. The fallback implementation is
  being changed to bind to the target's raw pre-redirect function pointer.

### Attempt 6: generic raw-pointer fallback delegate

- Tried: bind the saved pre-redirect method pointer with
  `Marshal.GetDelegateForFunctionPointer` using a closed `Func<,>` type.
- Failure: .NET Framework rejects generic delegate types for this API with
  `ArgumentException: The specified type must not be a generic type
  definition`.
- Recovery: no installed files were changed. The runtime now generates a
  non-generic delegate class with the exact receiver and return types.

### Attempt 7: generated delegate class for managed array returns

- Tried: generate a non-generic `MulticastDelegate` with the exact target
  signature and use `Marshal.GetDelegateForFunctionPointer` for the original
  method entry.
- Failure: .NET Framework rejected the managed array return type with
  `MarshalDirectiveException: invalid managed/unmanaged type combination`.
- Recovery: no installed files were changed. The fallback is being emitted as
  a managed `calli` against the saved JIT entry pointer instead of using
  unmanaged marshaling.

### Attempt 8: `calli` fallback using the original method pointer

- Tried: emit a replacement method that calls the saved original method
  pointer directly, preserving behavior for other app IDs without a managed
  delegate.
- Failure: the first implementation passed
  `System.Reflection.CallingConventions` to `DynamicILInfo.EmitCalli`, which
  requires `System.Runtime.InteropServices.CallingConvention`; the runtime
  project did not compile.
- Recovery: no installed files were changed. The call will use the correct
  interop calling-convention enum before runtime evaluation.

### Attempt 9: unmanaged `ThisCall` fallback

- Tried: compile and prepare the emitted `calli` replacement with
  `CallingConvention.ThisCall` so it could invoke the saved JIT entry pointer.
- Failure: the assembly built, but .NET Framework threw
  `InvalidProgramException` while preparing the generated replacement method;
  the unmanaged call signature is invalid for this managed method shape.
- Recovery: no installed files were changed. This fallback will be replaced by
  a managed IL copy or another CLR-safe way to retain the original behavior.
