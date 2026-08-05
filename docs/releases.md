# Releases

A version tag starts the release workflow. Push a tag named `v<version>`, such as `v0.1.0`, and GitHub builds the two
front ends for every supported target before creating a GitHub Release for that tag.

Each release contains these self-contained assets:

| Front end | Linux | macOS | Windows |
|---|---|---|---|
| CLI | `wooly-cli-linux-x64.tar.gz` | `wooly-cli-macos-x64.tar.gz`, `wooly-cli-macos-arm64.tar.gz` | `wooly-cli-windows-x64.exe` |
| TUI | `wooly-tui-linux-x64.tar.gz` | `wooly-tui-macos-x64.tar.gz`, `wooly-tui-macos-arm64.tar.gz` | `wooly-tui-windows-x64.exe` |

The Linux and macOS assets are tarballs solely to preserve the executable bit; each holds one executable named for its
front end. For example:

```sh
tar -xzf wooly-cli-macos-arm64.tar.gz
./wooly-cli version
```

The executables carry the .NET runtime with them, so users do not need to install .NET first. Before the release is
created, the packaged CLI is unpacked the way a user would and made to report its version, and the version it reports
has to be the tag being released. The Linux build does this inside a container holding the native libraries a
self-contained app needs and no .NET at all, which is the claim itself; the macOS and Windows builds do it on their own
platforms, which is the narrower check that a binary cross-built on Linux starts where it was meant to.

Carrying the runtime is not the same as needing nothing at all. Two things a user's machine still has to supply:

- **Linux** — the ICU libraries (`libicu`), which .NET reads locale and text-comparison data from. Desktop and server
  distributions generally have them; a minimal container image generally does not.
- **macOS** — a way past Gatekeeper, because these builds are neither signed nor notarized. Extracting the tarball with
  `tar` on the command line is usually enough; if macOS refuses to open the executable anyway, clear the quarantine flag
  the download left on it with `xattr -d com.apple.quarantine ./wooly-cli`.

The version a build reports is the tag it was built from, with the leading `v` dropped — `v0.1.0` produces an
executable that answers `0.1.0`.
