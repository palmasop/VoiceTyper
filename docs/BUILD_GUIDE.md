# VoiceTyper Build Guide

This guide explains how to build both the portable (RAR) and installer (MSI) versions of VoiceTyper.

## Prerequisites

- Visual Studio 2022 or later
- .NET 6.0 SDK
- WiX Toolset v4
- WiX Toolset Visual Studio 2022 Extension
- 7-Zip or WinRAR (for creating portable version)

## Building the Application

### 1. Portable Version (RAR)

1. Open Command Prompt or PowerShell
2. Navigate to the VoiceTyper project directory (not the solution directory):
   ```powershell
   cd VoiceTyper
   ```
3. Run the publish command:
   ```powershell
   dotnet publish -c Release -r win-x64 --self-contained true
   ```
4. The published files will be in:
   ```
   bin\Release\net6.0-windows\win-x64\publish\
   ```
5. Create a RAR archive:
   - Navigate to the publish directory
   - Select all files
   - Right-click > Add to archive...
   - Name it `VoiceTyper.rar`
   - Use RAR format
   - Click OK

### 2. MSI Installer

1. First, build the main project:

   - Open Command Prompt or PowerShell
   - Navigate to the VoiceTyper project directory:
     ```powershell
     cd VoiceTyper
     ```
   - Build the project:
     ```powershell
     dotnet build -c Release
     ```

2. Then build the installer:

   - Open `VoiceTyper.sln` in Visual Studio
   - Make sure WiX Toolset is properly installed:
     - Check Visual Studio Extensions are installed
     - Verify WiX Toolset v4 is installed
   - Right-click on `VoiceTyperSetup` project
   - Select "Build" or "Rebuild"

3. The MSI will be created at:

   ```
   VoiceTyperSetup\bin\Release\VoiceTyperSetup.msi
   ```

## Directory Structure

```
voice_input/                      # Solution root
├── VoiceTyper/                  # Main project
│   └── bin/Release/net6.0-windows/win-x64/publish/  # Portable version output
├── VoiceTyperSetup/            # Installer project
│   └── bin/Release/            # MSI installer output
└── docs/                       # Documentation
```

## Verifying the Build

### Check Portable Version

1. Extract `VoiceTyper.rar` to a new folder
2. Run `VoiceTyper.exe`
3. Verify in Task Manager that it shows as "VoiceTyper"
4. Test basic functionality

### Check MSI Installation

1. Run `VoiceTyperSetup.msi`
2. Complete the installation
3. Check Start Menu for "VoiceTyper"
4. Verify in Task Manager and Add/Remove Programs
5. Test basic functionality

## Common Issues

### Application Shows as "icon.ico"

- Ensure `AssemblyName` is set in `.csproj`
- Verify `app.manifest` is included
- Rebuild both versions

### Missing Dependencies

- Check `PublishSingleFile` is true in `.csproj`
- Verify `SelfContained` is true
- Ensure all NuGet packages are restored

### Build Errors

- If WiX build fails, verify WiX Toolset is properly installed
- For publish errors, make sure you're in the correct directory
- Check Visual Studio is running as administrator for MSI builds

## Release Checklist

- [ ] Update version numbers in:
  - `VoiceTyper.csproj`
  - `app.manifest`
- [ ] Build portable version
- [ ] Build MSI installer
- [ ] Test both versions
- [ ] Create GitHub release
- [ ] Upload both files
- [ ] Update documentation

## Quick Commands Reference

```powershell
# Build Portable Version
cd path\to\voice_input\VoiceTyper
dotnet publish -c Release -r win-x64 --self-contained true

# Build for MSI
cd path\to\voice_input\VoiceTyper
dotnet build -c Release
# Then use Visual Studio to build the MSI

# Output Locations
Portable: VoiceTyper\bin\Release\net6.0-windows\win-x64\publish\
MSI: VoiceTyperSetup\bin\Release\VoiceTyperSetup.msi
```
