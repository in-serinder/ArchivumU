**ArchivumU — A Customized Offline Key Storage Solution Based on TC8G1K08A**

[English](ReadME.md)/[中文](ReadME_CN.md)

### Project Overview

- **Main Control Chip**: STC8G1K17A microcontroller
- **Storage Medium**: Two 24Cxx EEPROMs communicating via the I²C bus. The 0x50 chip stores index blocks, and the 0x51 chip stores data key-value pairs. The specific capacity depends on the combination of the two EEPROMs.
- **Interface Protocol**: Uses a non-standard USB communication protocol, unlike conventional mass storage devices; the operating system's native file manager cannot recognize or access it.
- **Security Features**: Supports user-defined encryption algorithms (AES128, RC4, Caesar, XOR, etc., but the current architecture's encryption reduces the actual storage size). Data read/write requires dedicated host software and an authorized access key.

### Applicable Scenarios

Designed specifically for personal text-based private data, covering the following two typical use cases:

- **Lightweight Credentials**: Passwords, authorization keys, private passcodes, and other short texts
- **Dense Materials**: Classified documents, encrypted notes, private text files, and other longer content

> Note: The maximum total size of a single storage operation is 16 KB, making it suitable for offline storage scenarios where security requirements outweigh capacity needs.

> [!WARNING]
> **Security Risk Notice**: The device architecture is relatively simple, primarily targeting unconventional offline storage scenarios, and does not include built-in anti-tampering or physical attack protection mechanisms. If an attacker obtains the device and has the ability to modify the main control firmware or directly read the EEPROM, there is a potential risk of bypassing the access key and directly extracting the original data. It is recommended that users store the device in a physically controllable secure environment and combine it with external encryption measures (such as secondary encryption of stored content) to improve overall security.
