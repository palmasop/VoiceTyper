# VoiceTyper

![VoiceTyper Screenshot](docs/images/screenshot_1.png)
![VoiceTyper Screenshot](docs/images/screenshot_2.png)

VoiceTyper is a Windows application that enables voice-to-text input using Microsoft Azure's Speech Services. Simply press a hotkey and start speaking - your words will be typed automatically!

<div align="center">

## ⬇️ Quick Download

[![Download Latest Release](https://img.shields.io/github/v/release/palmasop/VoiceTyper?label=Download%20Installer&style=for-the-badge&color=blue)](https://github.com/palmasop/VoiceTyper/releases/latest/download/VoiceTyper_Setup.exe)

Just download and run the installer!

[View All Releases](https://github.com/palmasop/VoiceTyper/releases) | [Report Bug](https://github.com/palmasop/VoiceTyper/issues)

</div>

## Features

- Voice-to-text typing in multiple languages:
  - Chinese (Cantonese)
  - Chinese (Mandarin)
  - English
- Customizable hotkey for starting/stopping voice input
- System tray integration
- Real-time speech recognition
- Secure Azure Speech Service configuration

## Installation

1. Download [VoiceTyper_Setup.exe](https://github.com/palmasop/VoiceTyper/releases/latest/download/VoiceTyper_Setup.exe)
2. Run the installer
3. Follow the installation wizard
4. Launch VoiceTyper from the Start Menu or desktop shortcut
5. Configure your Azure Speech Service settings:
   - Right-click the tray icon
   - Select "Settings"
   - Enter your Azure region and key
   - Choose your preferred language

## First-Time Setup

1. Get your Azure Speech Service credentials:

   - Create an [Azure Account](https://azure.microsoft.com/free/) if you don't have one
   - Create a [Speech Service resource](https://portal.azure.com/#create/Microsoft.CognitiveServicesSpeechServices)
   - Copy your resource's region and key

2. Configure VoiceTyper:
   - Right-click the tray icon
   - Select "Settings"
   - Enter your Azure region and key
   - Choose your preferred language

## Usage

1. Press the default hotkey (Ctrl + Shift + /) to start voice input
   - You can change this in Settings
2. Start speaking
3. Press the hotkey again to stop

## Configuration

Access settings through the system tray icon:

- Language selection
- Hotkey configuration
- Azure Speech Service settings

## Building from Source

### Prerequisites

- Visual Studio 2022 or later
- .NET 6.0 SDK
- Windows 10 or later

### Build Steps

1. Clone the repository:

   ```bash
   git clone https://github.com/palmasop/VoiceTyper.git
   cd VoiceTyper
   ```

2. Build with Visual Studio:

   - Open `VoiceTyper.sln`
   - Build solution (F6)

3. Or build with .NET CLI:
   ```bash
   dotnet build
   ```

### Create Distribution Build

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

The executable will be in `bin\Release\net6.0-windows\win-x64\publish\`

## Project Structure

```
VoiceTyper/
├── dist/               # Distribution builds
├── docs/              # Documentation
│   └── images/        # Screenshots and images
├── src/               # Source code
│   └── VoiceTyper/    # Main project
├── .gitignore
├── LICENSE
└── README.md
```

## License

This project is licensed under the [MIT License](LICENSE)
