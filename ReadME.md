**ArchivumU — Customized Offline Key Storage Solution Based on TC8G1K08A**

[English](ReadME.md)/[中文](ReadME_CN.md)

### Project Overview

- **MCU**: TC8G1K08A microcontroller
- **Storage**: Two 24C64 EEPROMs via I²C bus, total capacity 16 KB
- **Interface Protocol**: Uses non-standard USB protocol, different from regular mass storage devices, not recognized by OS native file managers
- **Security Features**: Supports user-defined encryption algorithms, requires dedicated host software and authorized access key for data read/write

### Application Scenarios

Designed for personal text privacy data, covering two typical use cases:

- **Lightweight Credentials**: Passwords, authorization keys, private passphrases and other short texts
- **Dense Documents**: Classified documents, encrypted notes, private text files and other longer content

> Note: Maximum single storage size is 16 KB, suitable for offline storage scenarios where security requirements exceed capacity needs.

> [!WARNING]
> **Security Risk Warning**: The device architecture is relatively simple, mainly targeting unconventional offline storage scenarios, and does not have built-in anti-tampering or physical attack protection mechanisms. If an attacker obtains the device and has the ability to modify the MCU firmware or directly read the EEPROM, there is a potential risk of bypassing the access key and directly extracting raw data. Users are advised to store the device in a physically controlled secure environment and use external encryption measures (such as double encryption of stored content) to enhance overall security.
