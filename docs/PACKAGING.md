# Debian Packaging Guide for Maintainers

This guide explains how to build, test, and distribute Debian packages for the Localization Resource Manager (LRM).

## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Package Variants](#package-variants)
- [Local Package Building](#local-package-building)
- [PPA Upload Process](#ppa-upload-process)
- [Release Checklist](#release-checklist)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)
- [Debian Package Structure](#debian-package-structure)

---

## Overview

LRM uses Debian packaging to distribute binary packages and source packages:

**Binary Packages (for GitHub Releases):**
- Built locally or in CI
- Direct download and installation with `apt install ./file.deb`
- Available for amd64 and arm64 architectures

**Source Packages (for PPA):**
- Built and uploaded to Launchpad PPA
- Launchpad builds binary packages for all supported Ubuntu versions
- Users install via `apt install lrm` after adding the PPA

**Distribution:**
- **PPA:** `ppa:nickprotop/lrm-tool`
- **GitHub Releases:** https://github.com/nickprotop/LocalizationManager/releases

---

## Prerequisites

### Required Packages

```bash
sudo apt-get install \
    dpkg-dev \
    debhelper \
    devscripts \
    dput-ng \
    dotnet-sdk-9.0 \
    zip
```

### GPG Key Setup

You need a GPG key for signing packages for PPA upload:

```bash
# Generate GPG key (if you don't have one)
gpg --full-generate-key

# Upload to Ubuntu keyserver
gpg --keyserver keyserver.ubuntu.com --send-keys YOUR_KEY_ID

# Upload to Launchpad
# Visit: https://launchpad.net/~nickprotop/+editpgpkeys
```

See the main README for detailed GPG setup instructions.

---

## Package Overview

LRM provides a single self-contained package:

| Package | Size | Dependencies | Benefits |
|---------|------|--------------|----------|
| **lrm-standalone** | ~72MB | None (includes .NET runtime) | Works everywhere, no runtime dependencies |

The package installs to `/usr/bin/lrm`.

---

## Local Package Building

### Build Binary .deb Packages

Build Debian packages for local testing or GitHub Releases:

**Prerequisites:**
- Run `./build.sh` first to build the platform binaries (required)
- The `build-deb.sh` script reuses pre-built binaries from `publish/linux-{x64|arm64}/lrm`

```bash
# Build for amd64 (standalone only)
./build-deb.sh amd64

# Build for arm64 (standalone only)
./build-deb.sh arm64

# Output: publish/deb/lrm-standalone_VERSION-1_ARCH.deb
```

**Note:** The script will fail with a clear error message if pre-built binaries are not found. Always run `./build.sh` first.

### Test Locally

```bash
# Install the package
sudo apt install ./publish/deb/lrm-standalone_0.6.12-1_amd64.deb

# Test the binary
lrm --version
lrm --help

# Check man page
man lrm

# Test bash completion
lrm <Tab><Tab>

# Remove the package
sudo apt remove lrm-standalone
```

---

## PPA Upload Process

### 1. Build Source Package

Create source package for Launchpad:

```bash
# Build unsigned source package
./build-source-package.sh

# Output directory: publish/source/
# Files created:
#   - lrm_VERSION.orig.tar.gz      (original source tarball)
#   - lrm_VERSION-1.debian.tar.xz  (debian packaging files)
#   - lrm_VERSION-1.dsc            (source package description - unsigned)
#   - lrm_VERSION-1_source.changes (changes file - unsigned)
```

**Using main build script:**

```bash
./build.sh --source
```

### 2. Sign the Source Package

```bash
cd publish/source

# Sign with your GPG key
debsign -k nikolaos.protopapas@gmail.com lrm_*_source.changes

# Verify signature
gpg --verify lrm_*_source.changes
```

### 3. Configure dput

Create or edit `~/.dput.cf`:

```ini
[lrm-tool-ppa]
fqdn = ppa.launchpad.net
method = ftp
incoming = ~nickprotop/ubuntu/lrm-tool/
login = anonymous
allow_unsigned_uploads = 0
```

### 4. Upload to PPA

```bash
cd publish/source

# Upload signed source package
dput lrm-tool-ppa lrm_*_source.changes

# Check upload status
# Visit: https://launchpad.net/~nickprotop/+archive/ubuntu/lrm-tool/+packages
```

### 5. Monitor Build

After upload, Launchpad builds packages for all Ubuntu releases:

1. Go to: https://launchpad.net/~nickprotop/+archive/ubuntu/lrm-tool/+packages
2. Wait for builds to complete (usually 5-30 minutes)
3. Check build logs for any errors
4. Once published, packages are available via apt

**Supported Ubuntu Releases:**
- Ubuntu 22.04 LTS (Jammy)
- Ubuntu 24.04 LTS (Noble)
- Ubuntu 24.10 (Oracular)

---

## Release Checklist

### Before Release

- [ ] Update version in `LocalizationManager.csproj`
- [ ] Update `CHANGELOG.md` with release notes
- [ ] Run full test suite: `dotnet test`
- [ ] Update `debian/changelog` with new version
- [ ] Commit all changes

### Manual Release Process

If not using automated GitHub Actions:

```bash
# 1. Build all platform binaries
./build.sh

# 2. Build and test binary packages
./build-deb.sh amd64
./build-deb.sh arm64
sudo apt install ./publish/deb/lrm-standalone_*_amd64.deb
lrm --version
sudo apt remove lrm-standalone

# 3. Build source package
./build-source-package.sh

# 4. Sign source package
cd publish/source
debsign -k nikolaos.protopapas@gmail.com lrm_*_source.changes

# 5. Upload to PPA
dput lrm-tool-ppa lrm_*_source.changes

# 6. Create Git tag
git tag -a v0.6.12 -m "Release v0.6.12"
git push origin v0.6.12

# 7. Create GitHub Release manually with all binaries and .deb files
```

### Automated Release (GitHub Actions)

Push a version tag to trigger automated release:

```bash
git tag -a v0.6.12 -m "Release v0.6.12"
git push origin v0.6.12
```

The GitHub Actions workflow will:
1. ✅ Run tests
2. ✅ Build all platforms (Linux x64/ARM64, macOS x64/ARM64, Windows x64/ARM64)
3. ✅ Build standalone .deb packages (amd64 + arm64)
4. ✅ Build source package
5. ✅ Sign source package with GPG key from secrets
6. ✅ Upload to PPA
7. ✅ Create GitHub Release
8. ✅ Upload all binaries (6 tar.gz/zip files + 2 .deb files)

---

## Testing

### Test Binary Packages

**On clean Ubuntu VM or container:**

```bash
# Download standalone package
wget https://github.com/nickprotop/LocalizationManager/releases/download/v0.6.12/lrm-standalone_0.6.12-1_amd64.deb
sudo apt install ./lrm-standalone_0.6.12-1_amd64.deb

# Verify no .NET runtime dependencies needed
dpkg -l | grep dotnet  # Should show no .NET packages

# Test the binary
lrm --version
lrm validate --help

# Test man page
man lrm

# Test shell completion
complete -p lrm
lrm <Tab><Tab>

# Remove
sudo apt remove lrm-standalone
```

### Test PPA Installation

**On clean Ubuntu system:**

```bash
# Add PPA
sudo add-apt-repository ppa:nickprotop/lrm-tool
sudo apt update

# Check available versions
apt-cache policy lrm-standalone

# Install
sudo apt install lrm-standalone

# Test
lrm --version

# Update test
sudo apt update && sudo apt upgrade

# Remove
sudo apt remove lrm-standalone
sudo add-apt-repository --remove ppa:nickprotop/lrm-tool
```

### ARM64 Testing

If you have ARM64 hardware (Raspberry Pi, ARM server):

```bash
# Build binaries first
./build.sh

# Build ARM64 .deb package
./build-deb.sh arm64

# Install and test
sudo apt install ./publish/deb/lrm-standalone_*_arm64.deb
lrm --version
```

---

## Troubleshooting

### Build Errors

**Error: "dpkg-deb: command not found"**
```bash
sudo apt-get install dpkg-dev
```

**Error: "debuild: command not found"**
```bash
sudo apt-get install devscripts
```

**Error: "dotnet: command not found"**
```bash
# Install .NET SDK 9.0
wget https://dot.net/v1/dotnet-install.sh
bash dotnet-install.sh --channel 9.0
```

### Signing Errors

**Error: "gpg: signing failed: No secret key"**
```bash
# List your keys
gpg --list-secret-keys

# Ensure you're using the correct key ID or email
debsign -k YOUR_EMAIL_OR_KEY_ID lrm_*_source.changes
```

**Error: "gpg: signing failed: Inappropriate ioctl for device"**
```bash
export GPG_TTY=$(tty)
debsign -k nikolaos.protopapas@gmail.com lrm_*_source.changes
```

### Upload Errors

**Error: "Unauthenticated upload rejected"**

- Ensure your GPG key is registered on Launchpad
- Verify key was used to sign the package: `gpg --verify lrm_*_source.changes`
- Check that public key is uploaded to Ubuntu keyserver

**Error: "File already exists in target archive"**

- You've already uploaded this version
- Increment the Debian revision: `0.6.12-1` → `0.6.12-2`
- Or bump the upstream version

### Package Installation Errors

**Error: "Package not found: lrm-standalone"**

- Make sure you added the PPA: `sudo add-apt-repository ppa:nickprotop/lrm-tool`
- Update package list: `sudo apt update`
- Or download directly from GitHub Releases

---

## Debian Package Structure

### Directory Layout

```
debian/
├── changelog          # Debian changelog (version history)
├── compat             # Debhelper compatibility level (13)
├── control            # Package metadata and dependencies
├── copyright          # License information (MIT)
├── rules              # Build script (executable makefile)
├── source/
│   └── format         # Source package format (3.0 quilt)
└── .gitignore         # Ignore build artifacts
```

### Key Files

**debian/control:**
- Defines one binary package: `lrm-standalone`
- No .NET runtime dependencies

**debian/rules:**
- Builds self-contained variant using `dotnet publish`
- Uses `--self-contained true` to bundle .NET runtime
- Installs binary, man page, and shell completions

**debian/changelog:**
- Must be updated for each release
- Format: `lrm (VERSION-REVISION) DISTRIBUTION; urgency=LEVEL`
- Example: `lrm (0.6.12-1) unstable; urgency=medium`

### File Mappings

**lrm-standalone package installs:**
- `/usr/bin/lrm` - Main binary (self-contained with .NET runtime)
- `/usr/share/man/man1/lrm.1.gz` - Man page
- `/usr/share/bash-completion/completions/lrm` - Bash completion
- `/usr/share/zsh/site-functions/_lrm` - Zsh completion
- `/usr/share/doc/lrm/` - Documentation

---

## Updating the Package

### Increment Version

1. **Update version in `LocalizationManager.csproj`:**
   ```xml
   <Version>0.7.0</Version>
   ```

2. **Update `debian/changelog`:**
   ```bash
   dch -v 0.7.0-1 "New upstream release"
   # Or manually edit debian/changelog
   ```

3. **Commit changes:**
   ```bash
   git add LocalizationManager.csproj debian/changelog
   git commit -m "Bump version to 0.7.0"
   ```

### Debian Revision vs Upstream Version

- **Upstream version:** `0.6.12` (from .csproj)
- **Debian revision:** `-1`, `-2`, etc.
- **Full Debian version:** `0.6.12-1`

**When to increment revision:**
- Packaging changes only (no code changes)
- Example: `0.6.12-1` → `0.6.12-2`

**When to increment upstream version:**
- New LRM release with code changes
- Example: `0.6.12-1` → `0.7.0-1`

---

## Additional Resources

- **Debian Packaging Tutorial:** https://www.debian.org/doc/manuals/maint-guide/
- **Launchpad PPA Help:** https://help.launchpad.net/Packaging/PPA
- **Ubuntu Packaging Guide:** https://packaging.ubuntu.com/html/
- **LRM PPA:** https://launchpad.net/~nickprotop/+archive/ubuntu/lrm-tool

---

**Maintainer:** Nikolaos Protopapas <nikolaos.protopapas@gmail.com>
**Last Updated:** 2025-01-18
