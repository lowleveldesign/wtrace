
# wtrace

![.NET](https://github.com/lowleveldesign/wtrace/workflows/build/badge.svg)

**The project homepage is at <https://wtrace.net>.**

Wtrace [spelled: *wɪtreɪs*] is a command-line tool for recording trace events from the Operating System or a group of processes. Wtrace may collect, among others, **File I/O** and **Registry** operations, **TPC/IP** connections, and **RPC** calls. Its purpose is to give you some insights into what is happening in the system.

Additionally, it has various **filtering capabilities** and may also dump statistics at the end of the trace session. As it's just a standard command-line tool, you may pipe its output to another tool for further processing.

It works on Windows 8.1+ and requires .NET 4.7.2+. Wtrace is just one executable, wtrace.exe, and you may download it from the [release page](https://github.com/lowleveldesign/wtrace/releases).

The available options are listed below. Please check the [wtrace documentation page](https://wtrace.net/documentation/wtrace) to learn details about them with some usage examples.

```
Usage: wtrace [OPTIONS] [pid|imagename args]

Options:
  -f, --filter=FILTER   Displays only events which satisfy a given FILTER.
                        (Does not impact the summary)
  --handlers=HANDLERS   Displays only events coming from the specified HANDLERS.
  -c, --children        Collects traces from the selected process and all its
                        children.
  --newconsole          Starts the process in a new console window.
  -s, --system          Collect only system statistics (Processes and DPC/ISR)
                        - shown in the summary.
  --nosummary           Prints only ETW events - no summary at the end.
  -v, --verbose         Shows wtrace diagnostics logs.
  -h, --help            Shows this message and exits.
```

A sample trace session might look as follows:

```
PS temp> .\wtrace.exe notepad

wtrace v3.0.0 - collects process or system traces
Copyright (C) 2021 Sebastian Solnica (lowleveldesign.org)
Visit https://wtrace.net to learn more

HANDLERS
  process, file, rpc, tcp

Preparing the realtime trace session. Please wait...

Tracing session started. Press Ctrl + C to stop it.
19:47:42.9656 notepad (13712.24820) FileIO/Create 'C:\WINDOWS\Prefetch\NOTEPAD.EXE-C5670914.pf' disposition: OPEN_EXISTING, options: 0x20 -> SUCCESS
19:47:42.9704 notepad (13712.24820) FileIO/FSControl 'C:\WINDOWS\Prefetch\NOTEPAD.EXE-C5670914.pf' class info: 0x900EB -> SUCCESS
19:47:42.9713 notepad (13712.24820) FileIO/QueryInfo 'C:\WINDOWS\Prefetch\NOTEPAD.EXE-C5670914.pf' class info: 0x5 -> SUCCESS
19:47:42.9715 notepad (13712.24820) FileIO/Read 'C:\WINDOWS\Prefetch\NOTEPAD.EXE-C5670914.pf' offset: 0, size: 12288 -> SUCCESS
19:47:42.9713 notepad (13712.24820) FileIO/Read 'C:\WINDOWS\Prefetch\NOTEPAD.EXE-C5670914.pf' offset: 0, size: 9947 -> SUCCESS
19:47:42.9794 notepad (13712.24820) FileIO/Create 'D:\temp\' disposition: OPEN_EXISTING, options: 0x21 -> SUCCESS
19:47:42.9808 notepad (13712.24820) FileIO/QueryInfo '<0xFFFFCD064B771CE0>' class info: 0x9 -> SUCCESS
19:47:42.9809 notepad (13712.24820) FileIO/QueryInfo '<0xFFFFCD064B771CE0>' class info: 0x9 -> SUCCESS
19:47:42.9810 notepad (13712.24820) FileIO/QueryInfo '<0xFFFFCD064B9FB700>' class info: 0x9 -> SUCCESS
19:47:42.9823 notepad (13712.27256) Thread/Start
19:47:42.9824 notepad (13712.26824) Thread/Start
19:47:42.9828 notepad (13712.25724) Thread/Start
19:47:42.9838 notepad (13712.24820) FileIO/Create 'C:\WINDOWS\SYSTEM32\notepad.exe.Local\' disposition: OPEN_EXISTING, options: 0x200000 -> OBJECT_NAME_NOT_FOUND
19:47:42.9839 notepad (13712.24820) FileIO/Create 'C:\WINDOWS\WinSxS\amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.746_none_ca02b4b61b8320a4' disposition: OPEN_EXISTING, options: 0x21 -> SUCCESS
19:47:42.9840 notepad (13712.25724) FileIO/Create 'C:\WINDOWS\WinSxS\amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.746_none_ca02b4b61b8320a4\COMCTL32.dll' disposition: OPEN_EXISTING, options: 0x200000 -> SUCCESS
19:47:42.9840 notepad (13712.25724) FileIO/QueryInfo 'C:\WINDOWS\WinSxS\amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.746_none_ca02b4b61b8320a4\COMCTL32.dll' class info: 0x4 -> SUCCESS
19:47:42.9840 notepad (13712.25724) FileIO/Cleanup 'C:\WINDOWS\WinSxS\amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.746_none_ca02b4b61b8320a4\COMCTL32.dll' -> SUCCESS
19:47:42.9841 notepad (13712.25724) FileIO/Close 'C:\WINDOWS\WinSxS\amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.746_none_ca02b4b61b8320a4\COMCTL32.dll' -> SUCCESS
19:47:42.9841 notepad (13712.25724) FileIO/Create 'C:\WINDOWS\WinSxS\amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.746_none_ca02b4b61b8320a4\COMCTL32.dll' disposition: OPEN_EXISTING, options: 0x60 -> SUCCESS
19:47:42.9845 notepad (13712.25724) FileIO/Cleanup 'C:\WINDOWS\WinSxS\amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.746_none_ca02b4b61b8320a4\COMCTL32.dll' -> SUCCESS
19:47:42.9845 notepad (13712.25724) FileIO/Close 'C:\WINDOWS\WinSxS\amd64_microsoft.windows.common-controls_6595b64144ccf1df_6.0.19041.746_none_ca02b4b61b8320a4\COMCTL32.dll' -> SUCCESS
19:47:42.9860 notepad (13712.24820) FileIO/Create 'C:\WINDOWS\system32\IMM32.DLL' disposition: OPEN_EXISTING, options: 0x200000 -> SUCCESS
19:47:42.9860 notepad (13712.24820) FileIO/QueryInfo 'C:\WINDOWS\system32\IMM32.DLL' class info: 0x4 -> SUCCESS
19:47:42.9860 notepad (13712.24820) FileIO/Cleanup 'C:\WINDOWS\system32\IMM32.DLL' -> SUCCESS```
...

Process (13712) exited.
Closing the trace session. Please wait...

--------------------------------
           Processes
--------------------------------
├─ notepad [13712]

--------------------------------
            File I/O
--------------------------------
'C:\Windows\System32\config\SOFTWARE' Total: 32768B, Writes: 0B, Reads: 32768B
'C:\WINDOWS\Prefetch\NOTEPAD.EXE-C5670914.pf' Total: 19894B, Writes: 0B, Reads: 19894B
'C:\Windows\Fonts\staticcache.dat' Total: 60B, Writes: 0B, Reads: 60B

--------------------------------
              RPC
--------------------------------
fb8a0729-2d04-4658-be93-27b4ad553fac (lsapolicylookup) [0] calls: 4
fb8a0729-2d04-4658-be93-27b4ad553fac (lsapolicylookup) [1] calls: 4
fb8a0729-2d04-4658-be93-27b4ad553fac (lsapolicylookup) [5] calls: 4
e60c73e6-88f9-11cf-9af1-0020af6e72f4 (epmapper) [0] calls: 2
fb8a0729-2d04-4658-be93-27b4ad553fac (lsapolicylookup) [2] calls: 2
e60c73e6-88f9-11cf-9af1-0020af6e72f4 (epmapper) [6] calls: 2
```