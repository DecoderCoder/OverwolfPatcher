# Compatibility test battery

Observed 2026-09-08, approximately 18:22 UTC (15:22 America/Sao_Paulo).
Scope: public Overwolf 0.309.0.14 and Outplayed 175.3.12981; C# tooling.

## Working hypothesis

The current Core-only fixture fails a necessary publisher-signature predicate
before its subscription behavior can be exercised. This is now reproduced in
a separate test process using the **clean installed WinTrust helper**, in
addition to the earlier live launcher rejection. A no-edit Cecil rewrite also
fails that predicate. More subscription IL changes cannot resolve this failure.

If a candidate could pass deployment and loading, legacy plan 61 would plausibly
set Outplayed's local premium boolean while Tebex remains empty or fails.
Outplayed settles provider calls independently and ORs their results after login.
That is a code-backed hypothesis about local decisions, not observed premium
operation or a remote entitlement. Layout persistence and remote media access
require different observations from a badge or temporary layout change.

## Executable checks, cheapest first

Run from the repository root, retaining all dependency DLLs:

```powershell
.\build\build.ps1 -Test
.\OverwolfPatcher\bin\Release\net48\OverwolfPatcher.exe status
.\tests\bin\Release\net48\OverwolfPatcher.Tests.exe --compatibility `
  'C:\Program Files (x86)\Overwolf' `
  artifacts\premium-tests\outplayed-live-02\original.dll `
  artifacts\premium-tests\outplayed-live-02\OverWolf.Client.Core.dll `
  artifacts\diagnostics\verification-isolation\Core-no-semantic-edits.dll
```

The opt-in probe reads the current configuration and pins execution to the
reviewed clean CommonUtils SHA-256 and Core baseline. It loads only the clean
CommonUtils helper for execution, calls `WinTrust.VerifyEmbeddedSignature`,
and inspects Core/launcher metadata with Cecil. It does not execute Core,
launcher initialization, `FileUtils` logging initialization, or app APIs. It
does not write to input files or register an Overwolf assembly-load handler.
It verifies every input hash again on exit. New versions require a new review;
do not edit the expected hashes merely to make a changed installation pass.

| Check | Observed result | What it establishes |
| --- | --- | --- |
| Build and synthetic battery | Zero warnings/errors; passed | Generated IL executes in a synthetic assembly; replacement/recovery guards work in temporary fixtures |
| Known-version apply refusal | Passed against a fake install config, before accessing Core | The 0.309.0.14 refusal remains active |
| Expiry moved into the past in a synthetic copy | Plan still Active; ID 61 still returned | Seven-day expiry is data, with no generated runtime expiration gate |
| Clean Core copy, exact embedded-signature helper | `accepted=True`, `broken=False` | Positive control for the helper in this environment |
| Existing staged Core, same helper | `accepted=False`, `broken=False` | Candidate fails a necessary launcher predicate |
| Existing no-edit round-trip Core, same helper | `accepted=False`, `broken=False` | Rewriting alone is sufficient to fail that predicate |
| Recursive method comparison, staged Core | 16,992 methods; exactly the two expected bodies differ | No other method IL/flags/locals/EH differences under this comparison |
| Recursive method comparison, round-trip Core | 16,992 methods; zero differences | Extends the previous top-level comparison to nested types |
| Input hashes before/after | All unchanged | Probe did not alter its inputs |

The comparison includes method inventory/order, attributes, implementation
attributes, IL, local types, InitLocals and exception-handler boundaries. It
excludes MaxStack, other metadata, resources, PE layout and signatures. It is
not complete semantic equivalence or CLR verification. The probe's launcher
checks assert call-site presence, not complete control-flow equivalence.

The first probe run reached the expected three signature results but its
comparison failed because Cecil method FullName is not unique for all overloads.
The comparison now includes declaration order. Only the successful rerun's
16,992-method counts are valid comparison evidence.

## Verification lifecycle and limits

Read-only IL inspection of current `Overwolf.exe` establishes:

1. `Program.Main` registers `OnAssemblyLoad` at IL_0000. Its single-instance
   check can return at IL_000d; launching again while resident is not a fresh
   startup test. Initial setup can already cause guarded assembly loads.
2. `VerifyDeployedAssembliesOrFail` is called at IL_0022, before `Run` at IL_0039.
   It checks existing files in the solution assembly set in scan directories.
3. `AssemblyResolveByVersion` checks selected files while populating its version
   assembly cache. A rejected entry is skipped; this is distinct from the
   startup scan and load-time termination.
4. `OnAssemblyLoad` checks selected assemblies with a usable file location and
   calls `Environment.FailFast` at IL_0052 on rejection.
5. All three paths call `VerifyDeployedAssembly`, which invokes
   `FileSignedByOverwolf(path, ref broken, true, false, true)` at IL_000f:
   embedded verification enabled, subject check enabled, serial check skipped.
6. `FileSignedByOverwolf` calls the WinTrust wrapper first, then checks accepted
   Overwolf certificate subject forms. The wrapper succeeds only when native
   WinVerifyTrust returns zero. Its `broken` flag only marks the bad-digest
   result; **false does not mean accepted**, as the unsigned controls show.

The probe executes the exact embedded-signature predicate, not the entire
`FileSignedByOverwolf` policy or the complete launcher. Rejecting that necessary
predicate is sufficient to rule out these candidates under the inspected
unchanged policy. Passing it would still require publisher checks, strong-name
validation where applicable, successful loading, and feature validation. This
mapping covers the inspected managed launcher, not every possible native or
extension trust check in Overwolf.

The repository history confirms patcher 1.33 at 2eee788 on 2022-03-29. No
trustworthy historical, known-working Overwolf launcher was located in the
preserved artifacts. The date this verification lifecycle appeared remains
unknown; a patcher version or old screenshot cannot establish it.

## Next discriminating experiment

The signature-control experiment proposed during this session is complete; it
predicts rejection of the current candidate, so another live Core replacement
has no evidential justification. Keep the version refusal in place.

The next useful experiment is a **clean, freshly opened, signed-in Outplayed
session measuring layout persistence**, with its ordinary subscription refresh.
No binary edits are involved. Record the session start, authenticated state,
legacy plans, Tebex result/error, and selected layout; attempt the premium
layout through normal UI, then close/reopen Outplayed and observe what persists.
Expected free baseline: no plan 61, no active Tebex package, premium promotion
or default layout after reopening. Unexpected premium or missing provider
results invalidate the assumed baseline and must be explained first.

Current log files were last written at 15:04:51 local, before the observed
Overwolf process start at 15:09:29. The previous report's signed-in transition
is historical evidence; this session did not establish fresh signed-in UI
behavior. Do not relabel those old logs as a new baseline. Never include session
tokens, identity fields or whole private logs in a shareable result.

For an isolated provider harness later, exercise the actual extracted provider
methods with stubbed APIs and no network, recording the source bundle hash:

| Input case | Expected local result from inspected control flow |
| --- | --- |
| Signed out, legacy fixture present | Aggregator false |
| Signed in, no plans from either provider | False |
| Signed in, legacy ID 61, Tebex empty or throws | True |
| Legacy ID other than 61, Tebex empty | False |
| Legacy ID 61 with expired timestamp or non-active state | Boolean true; selected plan/display can differ |
| Legacy throws, Tebex qualifying Active/PendingCancellation plan | True |
| Both providers fail | False |
| Overlapping refreshes completed in reverse order | Only latest sequence emits the aggregate event |

These provider cases are a proposed harness, not tests executed by the C#
synthetic battery. Avoid translating them into a second invented implementation
and treating that implementation's success as proof about the app.

## Feature observations if a deployable candidate is ever established

| Feature | Required comparison | Confounder or limit |
| --- | --- | --- |
| Ads | Same visible ad placement after initialization, baseline versus candidate | Hidden game art, onboarding, login modal and bootstrap also suppress ads |
| Advanced layout | Select layout, reopen app, verify persistence | Redux action can change layout immediately without premium; saving is gated |
| Enhanced fullscreen timeline | Exercise opening and saved timeline layout after reopening | Premium controls both persistence and an opening behavior |
| Upload/remote media | Independent upload outcome and returned metadata, then access after reopening | Login and remote API results are separate from the local premium flag; quotas remain unverified |
| Capture/export | Same short recording and export action with explicit output result | No completed call-path analysis here proves these are premium-gated |

Before any future live candidate: establish the entire relevant trust/load path,
pin the candidate and clean hashes, rerun recovery checks on disposable copies,
and preserve a new immutable backup. One component/feature per experiment.
Failure criteria: first signature rejection, crash, app disappearance, stale
authentication evidence, unexpected update/config/hash change, or feature result
that fails its defined comparison. Stop at the first failure.

Restoration remains conditional on the current target matching the expected
test hash (or being missing); retain the clean backup. If an update/repair has
changed it, do not overwrite it with an older backup. Existing restoration tests
cover this filesystem behavior, not UAC success or all live recovery conditions.
There is currently no evidence-backed binary candidate ready for live testing.
