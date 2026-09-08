# OverwolfInsiderPatcher

## Current development build: scoped premium API testing

**Experimental; live validation failed on Overwolf 0.309.0.14.** Its launcher
rejects modified Core with `deployed assembly failed verification`, before login.
`apply` is blocked for this version. `stage` remains available for investigation;
successful staging or synthetic tests do not mean premium works in Outplayed.

The executable now defaults to a read-only compatibility check. The previous
all-DLL patch sequence is retained for offline investigation. `apply` remains
blocked for the reviewed live version.

Build with the .NET 8 SDK on Windows (the executable targets .NET Framework 4.8):

```powershell
.\build\build.ps1
.\build\build.ps1 -Test
```

Run from `OverwolfPatcher\bin\Release\net48` and keep the accompanying DLLs:

```powershell
.\OverwolfPatcher.exe status
.\OverwolfPatcher.exe stage
# Close Overwolf and its apps; use an elevated terminal for installation changes.
.\OverwolfPatcher.exe apply
.\OverwolfPatcher.exe restore --backup "FULL_PATH_PRINTED_BY_APPLY"
```

The supported live experiment uses the x64 CLR profiler built beside the
launcher. It starts the native `OverwolfLauncher.exe` with the same
`-from-desktop` argument as the installed shortcut. The profiling variables
are inherited only by the authorized managed `Overwolf.exe` child, and the
profiler removes them before that child can create more CLR processes, so the
signed installation remains unchanged:

```powershell
.\OverwolfPatcher.exe baseline --entry launcher --wait-ms 5000
.\OverwolfPatcher.exe baseline --entry managed --wait-ms 5000
```

`baseline` starts Overwolf without profiling and captures visible window text
for Overwolf processes. Use it before any instrumented mode. The profiler
diagnostic modes are:

```powershell
.\OverwolfPatcher.exe instrument --mode bootstrap --wait-ms 5000
.\OverwolfPatcher.exe instrument --mode observe --wait-ms 5000
.\OverwolfPatcher.exe instrument --mode flags --wait-ms 5000
.\OverwolfPatcher.exe instrument --mode neutral
.\OverwolfPatcher.exe instrument --mode premium --app cghphpbjeabdkomiphingnegihoigeggcfphdofo --plans 61
```

Run `baseline`, `bootstrap`, `observe`, and `flags` before `neutral`. The first
four modes isolate the launch context, profiler presence, callbacks, and CLR
flags. `neutral` verifies the reviewed Core identity, replaces both
subscription method bodies with equivalent original IL before JIT compilation,
and records the startup result in the profiler log. `premium` then enables the
local legacy API fixture for the selected app and plan IDs. The profiler
refuses unknown versions, hashes, or method metadata, and normal Overwolf
launches without the command do not inherit the profiling environment. The
fixture does not create a Tebex entitlement or guarantee server-backed premium
features.

The default test targets Outplayed's legacy subscription plan 61. It modifies
only the two legacy subscription query methods in `OverWolf.Client.Core.dll`,
and only for Outplayed's extension ID. Original method bodies remain available
for other apps. The returned plan expiry is seven days after generation. Login checks remain
in place: sign in to Overwolf before testing. No server subscription, Tebex
entitlement, payment, or account change is made; server-dependent premium
features are outside this test. Other apps require their own explicit IDs:
`--app EXTENSION_ID --plans PLAN_ID,ANOTHER_PLAN_ID`.

The active version is read from the launcher configuration. `--install DIR`
overrides installation discovery, and `--output DIR` selects an empty directory
for the staged DLL, original DLL, and hash manifest. Every apply creates a new
backup; preserve the printed directory for restoration. A changed installation
or incompatible method signature is rejected. Strong-name-signed Core assemblies
are rejected; runtime verification settings and other DLLs are untouched. Editing
Core removes its publisher Authenticode signature, even when it is not
strong-name signed. Updates/repair may replace Core and remove the local fixture.
Automatic reapplication and update blocking are not implemented.

Live validation must run in the interactive Windows account that owns the
Overwolf profile and must have write access to its CEF cache and Crashpad data.
Running the installed binaries from a restricted automation or sandbox account
can produce `Failed to initialize CEF runtime` before the subscription methods
are reached; that result does not validate or invalidate the profiler.

Validation covers synthetic execution of both API methods, app isolation, changed
signatures, repeated patch attempts, replacement hash guards, restore of both
present and missing files, and compilation of the x64 profiler. Staging against
a real installation checks structure; the profiler's neutral and premium modes
still require a fresh live startup test on the matching installed build.

The detailed investigation report is in
[docs/compatibility-investigation-report.md](docs/compatibility-investigation-report.md).
The reproducible read-only verifier probe, results, and next experiment are in
[docs/compatibility-test-battery.md](docs/compatibility-test-battery.md).
The seven-day expiry is fixture data: an isolated past-expiry test still returns
Active plans and plan IDs. It is not an automatic expiration mechanism.

## Historical upstream README

Patch Overwolf app to work on Windows Insider version and unlock new features

Now premium will work wherever there is a subscription through Overwolf in any App

## Porofessor.gg

![screenshot](https://i.imgur.com/5DDAJde.png)

## Outplayed

![screenshot](https://i.imgur.com/qXAtQJ8.png)



*~~*Premium "Outplayed" (Need Overwolf account logged)~~
*~~*Premium "Porofessor.gg"~~
*~~*Premium "Orca"~~

~~DevTools in any window (Overwolf windows and apps)~~

![screenshot](https://i.imgur.com/17uDpEG.png)

## Mirrors

Mirrors:
- [OverwolfInsiderPatcher.zip](https://multipload.net/Icep)
   - https://workupload.com/file/69sqwareKbs
   - https://upfiles.com/wCCqO
   - https://gofile.io/d/xLbv8w
   - https://krakenfiles.com/view/cxbV4a4C1X/file.html
   - https://megaup.net/6ec99f4c8cf6d317554521ad2595909a/OverwolfInsiderPatcher.zip
   - https://1fichier.com/?uqa3r5ixaxpwz6x7pvmp
   - https://send.cm/plwsanttgws7
   - https://turb.pw/hm3vfx0hwcdq.html
   - https://usersdrive.com/ulv0e0oo7fua
   - https://ufile.io/h376icnv

- [OverwolfInsiderPatcher.7z, password=oip](https://multipload.io/9kNpxkh1)
   - https://workupload.com/file/zT2pHRt6GDr
   - https://ufile.io/nnr3lk6j
   - https://upfiles.com/k3MzFDVy
   - https://gofile.io/d/520viw
   - https://krakenfiles.com/view/m5ewuq1OLJ/file.html
   - https://megaup.net/0653f8ed0208b6c0426627bd7d9b7971/OverwolfInsiderPatcher.7z
   - https://1fichier.com/?rmdhyuj71zzmg9l2gvwn
   - https://send.cm/7cjxt8919js9
   - https://turb.pw/xyvo0oeha1i0.html
   - https://usersdrive.com/sd41ofprahmr
  
- [DecoderCoder/OverwolfInsiderPatcher](https://github.com/DecoderCoder/OverwolfInsiderPatcher)
   - https://web.archive.org/web/*/https://github.com/DecoderCoder/OverwolfInsiderPatcher*#
   - https://archive.is/tlGKg
   - https://cc.bingj.com/cache.aspx?q=url%3ahttps%3a%2f%2fgithub.com%2fDecoderCoder%2fOverwolfInsiderPatcher&d=5044808902771814&mkt=en-US&setlang=en-US&w=CYpNiNbNWfP2HTxHG3FHXv25K-D-sYnb
   - https://megalodon.jp/2024-0911-0640-19/https://github.com:443/DecoderCoder/OverwolfInsiderPatcher
   - https://github.com/Bluscream/OverwolfInsiderPatcher
   - https://web.archive.org/web/*/https://github.com/Bluscream/OverwolfInsiderPatcher*#
   - https://archive.is/IsK5x
   - https://megalodon.jp/2024-0911-0716-10/https://github.com:443/Bluscream/OverwolfInsiderPatcher/tree/main
   - https://gitlab.com/Bluscream/OverwolfInsiderPatcher

- [Program.cs](https://github.com/DecoderCoder/OverwolfInsiderPatcher/blob/main/OverwolfInsiderPatcher/Program.cs)
   - https://archive.is/4Yr0F
   - https://megalodon.jp/2024-0911-0651-16/https://github.com:443/DecoderCoder/OverwolfInsiderPatcher/blob/main/OverwolfInsiderPatcher/Program.cs

- [InjectMethods.cs](https://github.com/DecoderCoder/OverwolfInsiderPatcher/blob/main/OverwolfInsiderPatcher/InjectMethods.cs)
   - https://archive.is/nFplI
   - https://megalodon.jp/2024-0911-0642-44/https://github.com:443/DecoderCoder/OverwolfInsiderPatcher/blob/main/OverwolfInsiderPatcher/InjectMethods.cs
