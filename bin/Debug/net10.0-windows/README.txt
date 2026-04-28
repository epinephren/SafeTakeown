SafeTakeown

A simple Windows permission cleanup helper.

Features:
- Take ownership of files and folders
- Fix permissions using built-in Windows tools
- Safe delete (optional 1-pass overwrite)
- Delete on reboot for locked files
- Recycle Bin repair

Important:
- Does NOT bypass Windows security
- Uses standard admin commands (takeown, icacls)
- Dangerous system paths are blocked by default

Use at your own risk.