---
name: TwinCAT target lines
overview: Publish TC3.1.4024 and TC3.1.4026 as exact targets. A request returns that row or nothing. TC3.1 stays the universal package. An any package is out of scope.
todos:
  - id: t1-server-accept
    content: "Server: allow TC3.1.4024 / TC3.1.4026. s_target rows are created on the server by hand"
    status: completed
  - id: t2-server-resolve
    content: "Server: package-resolve returns the requested target or nothing"
    status: completed
  - id: t3-restore
    content: "Twinpack restore: unpinned references try the solution line, then TC3.1"
    status: completed
  - id: t4-vsix
    content: "VSIX: catalog keeps TC3.1 unless the user picks a line; publish preselects the solution line"
    status: completed
  - id: t5-framework-publish
    content: "Framework deploy, snapshot, and Distribution zip: publish each tc_versions entry"
    status: pending
  - id: t6-devkit
    content: "Development Kit staging: lock and pin the line it is built for"
    status: pending
  - id: t7-cutover
    content: "Cutover: references.json tc_versions and Jenkins node labels"
    status: pending
isProject: false
---

# TwinCAT 4024 and 4026 targets

Publish `TC3.1.4024` and `TC3.1.4026` as separate packages. A target on the server is exact: the requested row comes back, or nothing does. `TC3.1` stays the universal package that already exists, and a request for it stays on `TC3.1`.

A 4026 solution does not receive a `TC3.1.4024` package, and a 4024 solution does not receive a `TC3.1.4026` package. Reusing an older build on a newer line waits for a later `any` package. That package is out of scope here.

Framework staging and `zkbuild` already build once per `tc_versions` entry. [get_locklabels.groovy](Z:/Templates/Common/vars/get_locklabels.groovy) already locks the agent on `params.TCVERSION` (else `TC3.1`), plus `INNOSETUP` and `CLIENT:ZEUGWERK`. Those two files need no code change.

Do the server tasks before any pipeline can push the new targets. Change `references.json` and the Jenkins labels last.

## 1. Server accepts the product lines

[index.php](Z:/Twinpack-Server/twinpack/index.php) `check_target_or_die` allowed `TC3.1` and `TC3.1.4024.47`, and rejected `TC3.1.4024`.

- Accept `TC3.1`, a product line (`TC3.1.4024`), and a full patch (`TC3.1.4024.47`).
- `TC3.1.4024` and `TC3.1.4026` are created in `s_target` on the server, with no prerequisite. The server does not insert them.
- A later line such as `TC3.1.4028` passes the format check and still fails publish until its own `s_target` row exists.

Done when a publish with `TC3.1.4024` or `TC3.1.4026` is accepted.

## 2. Server resolve is exact

`package_resolve_by_context` used to prefer `st.target` and then return some other target. A request for `TC3.1.4024` could come back as `TC3.1.4026`.

- A request with `target=TC3.1.4024` returns a `TC3.1.4024` row or an empty result.
- A request with `target=TC3.1.4026` returns a `TC3.1.4026` row or an empty result.
- A request with `target=TC3.1` returns a `TC3.1` row or an empty result.
- An empty target does not filter, which is what a client does when it omits the target.

`package-versions` and `package-version` already filter with an exact `st.target`. They stay as they are.

## 3. Twinpack restore

[PackageServerCollection.cs](Z:/Twinpack/TwinpackCore/Core/PackageServerCollection.cs) `FetchPackageAsync` sends `item.Target`. [Config.cs](Z:/Twinpack/TwinpackCore/Configuration/Config.cs) defaults that to `TC3.1`, so an unpinned reference asks for the universal package.

- `TC3.1.4024` or `TC3.1.4026` in `config.json` is a pin. Send that string only.
- `TC3.1` or an empty target is unpinned. Send the open solution's product line (`TC3.1.4024.59` becomes `TC3.1.4024`). If that resolve is empty, send `TC3.1`.
- Do not try another product line. A 4026 solution with only a `TC3.1.4024` package keeps the universal `TC3.1` package when that is what is published.

`FindTargetSystem` in [VisualStudio.cs](Z:/Twinpack/TwinpackCore/Core/VisualStudio.cs) already maps `TC3.1.4024` onto an installed `3.1.4024.*`.

## 4. VSIX

[CatalogWindow.xaml.cs](Z:/Twinpack/TwinpackVsixShared/Dialogs/CatalogWindow.xaml.cs) selects the first target when the reference has none, then stores `TargetsView.SelectedItem` on the reference.

- Leave the stored target at `TC3.1` unless the user picks `TC3.1.4024` or `TC3.1.4026`. Saving the solution line on every add would pin the reference, and a missing package for that line would no longer fall back to `TC3.1`.
- [PackageVersionWindow.xaml.cs](Z:/Twinpack/TwinpackVsixShared/Dialogs/PackageVersionWindow.xaml.cs) is the publish dialog. Preselect the solution line there, reduced from `TC3.1.4024.59` to `TC3.1.4024`, so a push names the build that was just compiled.

## 5. Framework publish

- [deploy.groovy](Z:/Templates/Framework/vars/deploy.groovy) and [upload_snapshot.groovy](Z:/Templates/Framework/vars/upload_snapshot.groovy) copy and push only `TC3.1`. Walk `tc_versions`, copy each folder, and push with `--target` set to that string.
- [deploy.groovy](Z:/Zeugwerk-Framework/.jenkins/deploy.groovy) Distribution filter is `TC3.1`. Use both entries. Debug already uses `*`.

## 6. Development Kit

[staging.groovy](Z:/Templates/Development Kit/vars/staging.groovy) does not use `get_locklabels`. The agent lock is `tc_versions[0]`, and `pin-version` and `configure` are hardcoded to `TC3.1`. Lock and pin the line the devkit is built for.

## 7. Cutover

Set [references.json](Z:/Zeugwerk-Framework/references.json) `tc_versions` to `TC3.1.4024` and `TC3.1.4026`. Label the Jenkins agents the same way. They already need `INNOSETUP` and `CLIENT:ZEUGWERK`.

## Out of scope

An `any` target, one nupkg that contains a folder per product line and lets the client pick the folder, follows later. Until then the server does not choose a substitute line, and the NuGet reader keeps treating Beckhoff packages as `TC3.1`.
