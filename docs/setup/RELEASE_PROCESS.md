# Release Process and Integrity Verification

This document describes how to build, verify, and release AVEX OS binaries with integrity checks.

## Current State

As of 2026-11-08, AVEX OS has **no official releases**. The project is in active development. Binaries built from source can be verified using SHA256 checksums, but **code signing is not yet implemented**.

## Building from Source

```powershell
# Restore dependencies
dotnet restore AVEXOS.slnx

# Build the application
dotnet build src/AVEXOS.App/AVEXOS.App.csproj -c Debug

# Run tests
dotnet test AVEXOS.slnx
```

## Generating Checksums

After building, generate SHA256 checksums for all binaries:

```powershell
.\scripts\generate-checksums.ps1
```

This creates `checksums.sha256` in the repository root with hashes for:
- `AVEXOS.App.exe`
- All `AVEXOS.*.dll` assemblies
- `e_sqlite3.dll` (native SQLite library)

## Verifying Checksums

To verify that binaries haven't been tampered with:

```powershell
.\scripts\verify-checksums.ps1
```

Exit codes:
- `0` — All checksums match
- `1+` — Number of failed/missing files

## Release Workflow (Future)

When the project is ready for official releases, the workflow will be:

1. **Build** release binaries:
   ```powershell
   dotnet publish src/AVEXOS.App/AVEXOS.App.csproj -c Release -r win-x64 --self-contained
   ```

2. **Generate checksums**:
   ```powershell
   .\scripts\generate-checksums.ps1 -OutputFile "release-checksums.sha256"
   ```

3. **Code sign** (requires certificate):
   - Obtain a code signing certificate from a trusted CA
   - Sign `AVEXOS.App.exe` and all DLLs using `signtool.exe`
   - Store certificate securely (HSM or encrypted key file)

4. **Create release archive**:
   ```powershell
   Compress-Archive -Path "bin\Release\*" -DestinationPath "AVEXOS-v1.0.0.zip"
   ```

5. **Generate checksum for archive**:
   ```powershell
   Get-FileHash -Path "AVEXOS-v1.0.0.zip" -Algorithm SHA256
   ```

6. **Publish release**:
   - Upload archive and checksums to GitHub Releases
   - Publish checksums separately (e.g., in release notes, on website)
   - Sign the checksum file with GPG (optional, for additional verification)

## Code Signing Roadmap

### Phase 1: Checksum Verification (Current)
- ✅ SHA256 checksum generation
- ✅ Checksum verification script
- ✅ Documentation

### Phase 2: Code Signing Infrastructure (Planned)
- [ ] Obtain code signing certificate (EV or standard)
- [ ] Set up secure key storage (Azure Key Vault, HSM, or encrypted file)
- [ ] Configure build pipeline to sign binaries automatically
- [ ] Add timestamping to signatures (prevents expiration issues)

### Phase 3: Automated Release Pipeline (Future)
- [ ] CI/CD pipeline (GitHub Actions or Azure DevOps)
- [ ] Automated build, test, sign, and package
- [ ] Automated checksum generation and publication
- [ ] GPG-signed checksums for additional verification

## Security Considerations

### Why Code Signing Matters

Without code signing:
- Users cannot verify that binaries come from the official AVEX OS project
- Man-in-the-middle attacks could replace downloads with malicious versions
- No protection against tampering after release

With code signing:
- Windows SmartScreen trusts the application (reduces warnings)
- Users can verify the publisher's identity
- Tampering invalidates the signature

### Certificate Types

- **Standard Code Signing Certificate**: Validates publisher identity, costs ~$200/year
- **EV Code Signing Certificate**: Extended validation, immediate SmartScreen trust, costs ~$400/year, requires hardware token

### Threat Model Reference

This addresses **T-S1** (Supply Chain Integrity) from [THREAT_MODEL.md](../docs/security/THREAT_MODEL.md):
> "No code signing or release integrity — cannot distinguish real AVEX OS from an impostor"

**Current mitigation**: SHA256 checksums provide tamper detection but not publisher authentication.

**Full mitigation**: Code signing with a trusted certificate.

## Verification Checklist

Before distributing binaries:

- [ ] All tests pass (`dotnet test AVEXOS.slnx`)
- [ ] Checksums generated (`scripts\generate-checksums.ps1`)
- [ ] Checksums verified (`scripts\verify-checksums.ps1`)
- [ ] Binaries code-signed (when infrastructure is ready)
- [ ] Checksums published alongside binaries
- [ ] Release notes include installation instructions

## Rollback

If a release is found to be compromised:

1. Revoke the code signing certificate (if applicable)
2. Publish a security advisory
3. Remove compromised binaries from distribution
4. Release a new version with updated signatures
5. Notify users to verify checksums before running

## References

- [Microsoft: Code Signing Best Practices](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/code-signing-best-practices)
- [SignTool Documentation](https://learn.microsoft.com/en-us/dotnet/framework/tools/signtool-exe)
- [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
