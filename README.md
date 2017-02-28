
# Wtrace

This application will trace in real-time all File I/O, TCP IP, ALPC and RPC operations performed by a given process. It works on Windows 7+ and requires .NET 4.5.2+. Wtrace stops when the traced process exits, or if you issue Ctrl+C (Ctrl+Break in Powershell, when pipes are used) in its command line.

The available options are:

```
Usage: wtrace [OPTIONS] pid|imagename args

Options:
      --newconsole           Start the process in a new console window.
      --summary              Prints only a summary of the collected trace.
      --nosummary            Prints only ETW events - no summary at the end.
  -h, --help                 Show this message and exit
  -?                         Show this message and exit
```

A sample trace session might look as follows:

```
PS temp> wtrace mspaint
1134,4316 (1072) FileIO/Create 'C:\' (0xFFFFFA801D789CA0) rw-
1135,2725 (1072) FileIO/Create 'C:\Windows\Prefetch\MSPAINT.EXE-B4A5B5E8.pf' (0xFFFFFA8023E185A0) ---
1135,5118 (1072) FileIO/Create 'C:\Windows' (0xFFFFFA8023E185A0) rw-
1135,5514 (1072) FileIO/Create 'C:\Windows\SYSTEM32\wow64.dll' (0xFFFFFA801D789CA0) rw-
1135,8384 (1072) FileIO/Close 'C:\' (0xFFFFFA801D789CA0)
1135,8542 (1072) FileIO/Create 'C:\Windows\SYSTEM32\wow64.dll' (0xFFFFFA801D789CA0) rw-
1135,8956 (1072) FileIO/Create 'C:\Windows\SYSTEM32\' (0xFFFFFA802110BD50) rw-
1135,9198 (1072) FileIO/Close 'C:\Windows\SYSTEM32\' (0xFFFFFA802110BD50)
1136,0825 (1072) FileIO/Close 'C:\' (0xFFFFFA801D789CA0)
1136,1668 (1072) FileIO/Create 'C:\Windows\SYSTEM32\wow64win.dll' (0xFFFFFA801D789CA0) rw-
1136,1873 (1072) FileIO/Close 'C:\' (0xFFFFFA801D789CA0)
1136,2049 (1072) FileIO/Create 'C:\Windows\SYSTEM32\wow64win.dll' (0xFFFFFA801D789CA0) rw-
...
1363,8894 (1072) FileIO/Read '' (0xFFFFFA80230F5970) 0x173400 32768b
1364,7208 (1072) FileIO/Read '' (0xFFFFFA80230F5970) 0x117400 32768b
1365,6873 (1072) FileIO/Read '' (0xFFFFFA80230F5970) 0x1CD400 32768b
1375,6284 (1072) FileIO/Create 'C:\Windows\win.ini' (0xFFFFFA801A43F2F0) rw-
1375,6702 (1072) FileIO/Read 'C:\Windows\win.ini' (0xFFFFFA801A43F2F0) 0x0 516b
1375,7369 (1072) FileIO/Create 'C:\Windows\SysWOW64\MAPI32.DLL' (0xFFFFFA8023E50710) rw-
1375,7585 (1072) FileIO/Close 'C:\Windows\SysWOW64\msxml6r.dll' (0xFFFFFA8023E50710)
1384,8796 (1072) FileIO/Read '' (0xFFFFFA801FDBFCD0) 0x58200 16384b
1385,3323 (1072) FileIO/Read '' (0xFFFFFA801FDBFCD0) 0x5C200 16384b
2318,6876 (1072) FileIO/Read '' (0xFFFFFA80230F5970) 0x209400 32768b
2319,3279 (1072) FileIO/Read '' (0xFFFFFA80230F5970) 0x213400 32768b
### Stopping ETW session...
======= ETW session =======
### ETW session stopped. Number of lost events: 0

======= System Configuration =======
Host: TEST (test.example.com)
CPU: 2793MHz 8cores 32382MB
LOGICAL DISK: 0 C: NTFS 238GB
NIC: VirtualBox Host-Only Ethernet Adapter fe80::9d86:1063:fe66:ef0b;169.254.239.11
NIC: Software Loopback Interface 1 ::1;127.0.0.1

======= File I/O =======
File name  Writes / Reads (bytes)
C:\Windows\SysWOW64\mspaint.exe 0 / 12 759 040
 0 / 1 905 760
C:\Windows\SysWOW64\MFC42u.dll 0 / 455 728
C:\Windows\SysWOW64\sti.dll 0 / 431 472
C:\Windows\SysWOW64\UIRibbon.dll 0 / 412 016
C:\Windows\SysWOW64\UIRibbonRes.dll 0 / 255 088
C:\Windows\SysWOW64\en-US\UIRibbon.dll.mui 0 / 179 312
C:\Windows\win.ini 0 / 516
C:\Windows\Fonts\staticcache.dat 0 / 60
C:\ProgramData\NVIDIA Corporation\Drs\nvdrssel.bin 0 / 1

======= Process/Thread =======
Number of child processes started: 0
Number of threads started: 7
```

In [wiki](https://github.com/lowleveldesign/wtrace/wiki) there are also details description:

- [how to filter the output](https://github.com/lowleveldesign/wtrace/wiki/Filtering-Output)
- [how to trace RPC calls](https://github.com/lowleveldesign/wtrace/wiki/Tracing-RPC)

## Links

- [Releasing wtrace 1.0 and procgov 2.0](https://lowleveldesign.wordpress.com/2016/10/21/releasing-wtrace-1-0-and-procgov-2-0/)
