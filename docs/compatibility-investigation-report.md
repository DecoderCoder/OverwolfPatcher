# OverwolfPatcher compatibility investigation report

Date of latest observation: 2026-09-08

Continuation results and executable commands are recorded in
[compatibility-test-battery.md](compatibility-test-battery.md). The latest
continuation is appended below; earlier login observations retain their original
timestamps and must not be treated as a fresh authenticated baseline.

This report records the code and installed-app evidence collected while
investigating whether the patcher can support premium-feature testing in the
current public Outplayed installation. It describes local behavior only. It
does not establish or promise a real Overwolf, Outplayed, or Tebex entitlement.

## Scope and current versions

The observed installation is:

- Overwolf: `0.309.0.14`
- Outplayed: `175.3.12981`
- Outplayed extension ID: `cghphpbjeabdkomiphingnegihoigeggcfphdofo`
- Overwolf installation: `C:\Program Files (x86)\Overwolf`
- Active managed assembly directory: `C:\Program Files (x86)\Overwolf\0.309.0.14`
- Outplayed extension directory:
  `C:\Users\bruno\AppData\Local\Overwolf\Extensions\cghphpbjeabdkomiphingnegihoigeggcfphdofo\175.3.12981`

The active version is selected by the probing paths in
`Overwolf.exe.config`. The launcher entry assembly is the managed
`Overwolf.exe` version `0.309.0.14`; `OverwolfLauncher.exe` is native. The
current configuration has `bypassTrustedAppStrongNames=false` and targets .NET
Framework 4.8.

## Repository state and experimental code

The worktree contains the user's existing uncommitted changes. No changes were
committed during this investigation. The current development additions are:

- `OverwolfPatcher/Testing/PremiumAssembly.cs`
- `OverwolfPatcher/Testing/PremiumCommand.cs`
- `tests/OverwolfPatcher.Tests.csproj`
- `tests/Program.cs`
- `build/build.ps1`
- `build/NuGet.Config`

The existing modified files route the entry point to the experimental command,
target .NET Framework 4.8, use `asInvoker`, expose internals to the test
assembly, enable nullable annotations, document the workflow, and allow the
local NuGet configuration through `.gitignore`. The old `LegacyMain` and patch
registration fields remain in `Program.cs`, but the active entry point does not
invoke that workflow.

The experimental command provides four operations:

- `status` reads the active installation and checks the expected legacy API
  shape.
- `stage` creates a modified copy, an immutable clean `original.dll`, and a
  hash manifest in a new output directory. It does not modify the installation.
- `apply` performs the checked replacement workflow, but explicitly refuses
  Overwolf `0.309.0.14` because the live test proved that this version rejects
  modified Core before startup.
- `restore` verifies the manifest and current target hash before replacing a
  test file. It refuses to overwrite a file that may have been changed by an
  update or repair.

The default fixture targets only Outplayed's legacy plan ID `61`. It changes
only the two legacy detailed-plan query methods in Core and branches on the
Outplayed extension ID. Other applications retain the original method bodies.
The login check remains in the original methods. The fabricated plan is marked
Active and receives a seven-day future expiry, but current Outplayed boolean
checks primarily use the plan ID, so automatic expiration must not be assumed.

The code rejects a strong-name-signed Core assembly, validates the required
method and property signatures before changing method bodies, rejects repeated
patching through an embedded marker, and uses hash-checked replacement and
restoration. These protections improve experiment safety; they do not make the
result trusted by Overwolf.

The synthetic test suite passed after the latest build. It covers generated
method execution, plan-ID and `double` Price handling, app-ID isolation,
preservation of original exceptions, signed-assembly and changed-signature
rejection, repeated-patch rejection, replacement hash guards, restoration of
present and missing DLLs, and refusal to overwrite a subsequently changed DLL.
Those tests load a synthetic assembly and never prove installed-app startup or
premium behavior.

## Clean installation evidence

The clean Core file was restored from the preserved backup before the latest
read-only checks. Its current evidence is:

- Path: `C:\Program Files (x86)\Overwolf\0.309.0.14\OverWolf.Client.Core.dll`
- SHA-256: `9DA15E0CACF446E59B3F728BA78F5CC8F0EC6D6616BB0F490BF90ABD21EF098E`
- Authenticode status: `Valid`
- Signer subject: `CN=Overwolf Ltd, O=Overwolf Ltd, L=Ramat Gan, C=IL`
- Signer issuer: DigiCert Trusted G4 Code Signing RSA4096 SHA384 2021 CA1
- Signer thumbprint: `67597B92C16D34929D69E990F6A51A5350FCBCE6`
- Signer validity observed through: 2027-05-31

Core has no strong-name public key. That fact is separate from the valid
publisher signature and does not make post-signing edits acceptable.
`OverWolf.Client.BL.dll` and `OverWolf.Client.CommonUtils.dll` were also
observed with valid Overwolf signatures. A read-only scan of the current
version directory found valid signatures on the relevant Overwolf assemblies.

The Outplayed manifest identifies it as a WebApp, requires Overwolf at least
`0.236.0.11`, starts a background window, and requests the `Subscription`
permission. Its current JavaScript bundles were present and intact. The
background and index logs are under:

`C:\Users\bruno\AppData\Local\Overwolf\Log\Apps\Outplayed`

The preserved diagnostic and premium-test directories remain under the
repository's ignored `artifacts` directory. They contain private logs and
proprietary binaries and should not be published wholesale.

## What the launcher verifies

The relevant launcher code was inspected read-only. `Program.Main` registers an
assembly-load guard, calls `VerifyDeployedAssembliesOrFail()` before the normal
application `Run()`, and only then enters normal startup.

The initial scan covers the entry directory and the active version directory.
The solution assembly set contains these 20 names:

```text
OverWolf.BL.Communication.dll
OverWolf.BL.Interfaces.dll
Overwolf.Campaigns.dll
Overwolf.Cef.dll
OverWolf.Client.BL.dll
OverWolf.Client.CommonUtils.dll
OverWolf.Client.Core.dll
Overwolf.exe
Overwolf.Extensions.dll
Overwolf.ExtensionsUpdate.dll
OverWolf.Kernel32.dll
OverWolf.Login.dll
Overwolf.Notifications.dll
Overwolf.ODK.Common.dll
Overwolf.RemoteController.dll
Overwolf.Social.dll
Overwolf.Subscriptions.dll
Overwolf.Video.dll
OverwolfBrowser.exe
OverwolfCrashHandler.exe
```

For every existing named file, the launcher calls
`VerifyDeployedAssembly(FileInfo)`. The same verifier is used by
`AssemblyResolveByVersion()` when it enumerates the active version directory.
The `OnAssemblyLoad()` handler also observes selected loaded assemblies and can
call `Environment.FailFast` when a loaded file fails verification.

The verifier delegates to:

```text
OverWolf.Client.CommonUtils.Utils.FileUtils.FileSignedByOverwolf(
    path, ref signatureIsBroken, true, false, true)
```

The current `FileSignedByOverwolf` implementation verifies the embedded
Authenticode signature, looks for a certificate subject containing Overwolf,
and accepts the known Overwolf subject forms. The launcher passes
`skipSerialTest=true`, so the observed launcher requirement is a valid embedded
signature from an Overwolf-subject certificate rather than merely strong-name
metadata.

The diagnostic strings distinguish a missing or non-Overwolf signature from a
signature that existed but was broken by a post-signing modification. This is
why an otherwise valid Mono.Cecil rewrite is still rejected.

## Rewrite and live-failure evidence

An offline Mono.Cecil read/write round-trip of clean Core, with no deliberate
method changes, produced an unsigned output file. A top-level method IL
comparison found zero differences after the resolver was correctly given the
installation directory. The first attempt had a missing dependency resolver
and produced a zero-byte output; its reported differences were discarded.

This establishes that rewriting loses the publisher signature. It does not
prove complete semantic equivalence and is not a live startup test.

Two live failure mechanisms are now confirmed:

1. The earlier broad patch caused a crash dump identifying
   `OverWolf.Client.CommonUtils` with `System.IO.FileLoadException`,
   `System.Security.SecurityException`, and strong-name validation failure
   `0x8013141A`.
2. The later Core-only scoped patch reached the launcher and was rejected with:

   ```text
   Refusing to start - deployed assembly failed verification:
   C:\Program Files (x86)\Overwolf\0.309.0.14\OverWolf.Client.Core.dll
   ```

The second failure occurs before Outplayed startup, subscription calls, or
login-dependent premium decisions. The fact that the user is now logged in
cannot change that earlier trust decision. The current command therefore
blocks `apply` on this exact Overwolf version.

## Outplayed subscription paths

The installed Outplayed bundles contain both the legacy Overwolf provider and a
Tebex provider.

The legacy provider calls:

```text
overwolf.profile.subscriptions.getDetailedActivePlans()
```

It maps returned `planId` values and considers plan `61` subscribed. The
current Core methods still have the expected shape:

- `getDetailedActivePlans` retains the login check and obtains detailed plans
  from the subscription manager.
- `GetExtensionSubscriptions` maps the subscription data to
  `ODKv2API.DetailedActivePlan[]`.
- `GetExtensionSubscriptionsIds` maps the same source to integer plan IDs.
- `ODKv2API.DetailedActivePlan.set_Price` currently accepts `System.Double`.

The Tebex provider obtains a user session token through
`overwolf.profile.generateUserSessionToken`, then makes an authenticated
request to `subscriptions-api.overwolf.com`. It recognizes current Outplayed
package identifiers for legacy, monthly, and yearly products. Without a real
server entitlement, the expected result is an empty Tebex plan set.

The service aggregator registers the legacy provider and adds Tebex on current
Overwolf versions. Its subscription decision first checks login, then asks all
providers and combines their results with an OR operation. Its preferred source
is Tebex when that provider is available, but preferred-source selection does
not mean that an entitlement exists.

The practical consequence is narrow: if a modified Core could be loaded, a
fabricated legacy plan `61` could plausibly make some local `isSubscribed` and
`hasPremium` decisions true. It would not create a Tebex result, change the
server account, or prove that uploads and remote storage limits were lifted.

## Authenticated baseline observation

The user reported signing in before the latest testing. The existing Outplayed
background log contains a login transition at approximately 15:04:42:

- Outplayed recorded `isSignedIn: true` in its Sentry user state.
- The legacy provider then logged subscription data as `null`.
- Tebex logged subscription data as `[]`.
- No active plan 61 was observed.

Earlier startup calls in the same log occurred before that transition and
reported `Not signed in`, `generateUserSessionToken` as `not logged in`, and
legacy `getDetailedActivePlans` with `user not logged in`. These lines are
consistent with the user's observation that login matters for the provider
path, but they also show why the timestamp and fresh app session matter.

The clean index log initialized the ad SDK and logged ad placements as shown.
That is a useful free-tier baseline. It is not evidence of failure in the
premium experiment because no modified Core was running.

The normal Overwolf process was started again through its installed executable
for a read-only baseline check. Outplayed remained resident in the existing
Overwolf instance, so no new app log was generated by that second launch
attempt. No installed file was changed.

## Premium decisions observed in the app code

Outplayed's `hasPremium` state is populated during authentication and
subscription initialization. The state affects several independent UI paths:

- ad suppression;
- advanced layout choices and game-art options;
- enhanced fullscreen timeline behavior;
- premium plan and promotion views;
- some sidebar and account display decisions.

The current plan configuration describes premium features such as permanent
host uploads, access to uploads on any device, no ads, advanced layouts, and
enhanced fullscreen mode. Those strings are configuration and display evidence;
each behavior still needs a separate live observation. A premium badge or plan
screen is therefore only a partial result.

The strongest feature evidence would be a clean signed-in baseline followed by
the same action under a candidate, with the provider path and visible result
recorded after a restart. Upload, export, storage, and account operations need
special care because they may depend on server authorization even if a local
UI flag changes.

## Feasibility conclusion

The narrow legacy API target is structurally compatible with the current Core.
The current live deployment is incompatible with Overwolf `0.309.0.14` because
editing Core removes the publisher signature required at startup, assembly
resolution, and post-load verification. Login is relevant after startup, but
it is not the cause of the confirmed launcher rejection.

The repository can safely stage and test the local fixture offline. It cannot
currently apply that fixture to the observed installation without defeating a
publisher trust check, and bypassing one verifier call would not address the
other verification paths. There is no evidence that this repository can grant
a server-side Overwolf or Tebex entitlement.

## Recommended continuation battery

Use a new output directory for every staged result and keep the installation
clean until there is independent evidence that a candidate can pass the exact
launcher verifier.

1. Record the active version, Core SHA-256, Authenticode status, and Outplayed
   manifest before each session.
2. With clean files and the user signed in, launch Outplayed normally and
   capture fresh timestamps for authentication, legacy plans, Tebex plans, ads,
   and the visible free-tier behavior.
3. Run `status`, then `stage --output <new-empty-directory>`. Inspect the staged
   marker, method signatures, app-ID guard, expiry data, and hash manifest.
4. Do not run `apply` on `0.309.0.14`; the command's refusal is intentional and
   is supported by the live failure.
5. If a future Overwolf build is observed, first establish through read-only
   evidence that its complete verification lifecycle accepts the exact
   candidate. A successful build or staging step is not enough.
6. If a candidate ever starts, validate startup and subscription calls first,
   then test ads, layout, fullscreen timeline, capture, upload, export, and
   remote media independently. Compare with the clean signed-in baseline.
7. On any startup rejection, crash, app loss, update, repair, or unexpected
   target hash, stop. Restore only when the target hash equals the manifest's
   expected test hash. Otherwise recover from the known clean installation or
   installer rather than overwriting a changed file.

The existing restoration code intentionally keeps the backup and refuses to
overwrite a target changed by a later update. That behavior should be retained.

## Continuation: isolated signature controls and feature persistence

At approximately 18:22 UTC on 2026-09-08, active probing still selected
0.309.0.14. Installed Core retained SHA-256
`9DA15E0CACF446E59B3F728BA78F5CC8F0EC6D6616BB0F490BF90ABD21EF098E`,
matching the clean backup, with a valid Overwolf Authenticode signature.
The launcher and CommonUtils also had valid publisher signatures. Overwolf
remained running with PID 7648, started 15:09:29 local. No installed binaries,
configuration, account settings, or verification settings were changed.

Additional clean launcher/configuration/CommonUtils copies were preserved in:

```text
artifacts/diagnostics/compatibility-baseline-feeaafa1f5ee466abdf67338aa9299f1/
```

The existing immutable Core backup remains in `outplayed-live-02/original.dll`.
New snapshot hashes:

| File | SHA-256 |
| --- | --- |
| Overwolf.exe | F189159FD6B51A02B8991168EEE551C08D0D2B42F248482850E166863CE2CF51 |
| Overwolf.exe.config | 691568EF37D9E6DAA335FE41732B7D3C3D99A776F6E235572DBCD124CBC77CD9 |
| OverWolf.Client.CommonUtils.dll | 33298BC101F699732EE6A52CB7BE7A02FBC97F97AC4217C61F1801BB1A335F43 |

`tests/CompatibilityProbe.cs` adds an opt-in C# probe with reviewed-binary hash
guards. A separate test process invoked the exact clean
`WinTrust.VerifyEmbeddedSignature(string, ref bool)` method against the three
preserved Core copies. Clean was accepted; the scoped patch and no-edit rewrite
were rejected. All three reported `broken=false`: that flag only distinguishes
the native bad-digest result, not all rejection causes. This directly reproduces
a necessary predicate of the launcher without executing the launcher or Core.
It is not execution of the complete `FileSignedByOverwolf` policy.

The probe compared 16,992 methods including nested types, method flags, local
types and exception-handler boundaries. Only the two intended methods differed
in the staged file; zero differed in the round-trip. Its exclusions and the
initial comparison-harness failure are documented in the battery. No claim of
complete semantic equivalence is made.

The synthetic suite now also executes a past-expiry fixture (still Active,
still returns plan 61), and tests the known-version apply refusal before Core
access using a disposable fake installation configuration. Build: zero warnings
and errors; synthetic suite and opt-in compatibility probe passed. The existing
patch generation, apply refusal and restoration implementation were retained.

### Outplayed evidence anchors

The smaller exploratory agent inspected bounded portions of the installed
bundles read-only. Offsets below are approximate zero-based character offsets
in the decoded source, not PE offsets or byte offsets. Hashes identify the
specific files; line numbers provide additional navigation.

| Bundle | Size (bytes) | SHA-256 |
| --- | --- | --- |
| background.js | 4175956 | BD77D930EC6FEEAF6619B5AFCC63E4D74DE74425F7444F90BFD6DE11F0658666 |
| index.js | 5351534 | 1C31EABA52CCA9096F0F4021C7E3D43AC9E012F0D168D20103E6912FA2AFDF15 |

- `background.js`, `mW`, ~3696501 (line 69114): plan ID 61, detailed API
  binding and subscription-change listener. At ~3697300, `isSubscribed`
  tests mapped IDs only. `mapPlan` preserves expiry/state; separate active-plan
  selection prefers Active, then PendingCancellation. Raw subscription events
  also test whether IDs contain 61. Thus boolean and plan-display paths differ.
- `background.js`, `tW.getStatus`, ~3682300 (line 68836): empty session token
  returns an empty result, same-token memory/in-flight results can be reused,
  authenticated requests have a five-second timeout, and cache generation guards
  writes. `dW` filters supported Tebex packages and Active/PendingCancellation
  states; its boolean method catches errors as false.
- `background.js`, shared helper `cs`, ~1482516 (line 21484), uses
  `Promise.allSettled`. Aggregator `kW`, ~3700600, registers Tebex from
  Overwolf 0.240.0.5; at ~3701750 it checks login, settles providers independently
  and uses `.some(Boolean)`. Tebex failure therefore does not negate a true legacy
  result. Refresh at ~3704100 emits only the latest subscription-check sequence.
- `index.js`, authentication/subscription initialization, ~2321254 (line
  50539), populates `hasPremium` through `isSubscribedStrict`. At ~2323000,
  saved layouts are restored for premium; free state defaults to standard
  layout and hidden fullscreen timeline. Hook at ~4451550 retries errors and
  listens to aggregate subscription-change events.
- `index.js`, ad predicate `Xo`, ~1514008 (line 32358), suppresses ads for
  `hasPremium`. Hidden game art, onboarding, login modal and bootstrap state
  can also suppress them: observing no ad by itself is ambiguous.
- `index.js`, `DA.setAppLayout`, ~3533700 (line 75936), dispatches a layout
  action regardless, but persists it only for premium. A one-time visual
  layout change is weaker evidence than surviving app reopening.
- `index.js`, `DA.setFullscreenTimelineLayout`, ~3062850 (line 66062), gates
  persistence on premium. `JO._toggleTimeline`, ~3019950 (line 65222), changes
  the opening path for non-premium; the player receives both layout and premium
  state near ~3063894.
- `background.js`, `GH`/UploadFeedService, ~3659800 (line 68359), combines
  remote and local feed storage. Share success checks login; metadata is fetched
  from `api.outplayed.tv/metadata/<id>/` near ~3669864 (line 68554). These bounded
  methods establish separate login/remote-response boundaries, but do not prove
  where upload quotas or every server authorization rule are enforced.

### Remaining limits

The current Outplayed background and index logs were last written at 15:04:51
local, predating the observed 15:09:29 process start. No fresh signed-in UI
baseline or live premium feature result was obtained in this continuation.

The current Core wrapper IL still checks login before calling the two query
methods, and the recursive comparison confirms those wrappers are unchanged.
The UID getter is null-tolerant for a missing extension but delegates to
`IExtension.get_UID` when present. The inserted app guard adds an early getter
call outside the original bodies; existing synthetic tests do not prove all
side-effect/exception behavior for every real extension implementation.

No trustworthy historical working launcher was located, so the introduction
date of these checks is unresolved. No binary candidate has demonstrated the
complete trust/load lifecycle. The detailed battery separates completed tests
from proposed provider cases and the next clean layout-persistence observation.
