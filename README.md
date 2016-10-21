
# Wtrace

This application will trace in real-time all File I/O and TCP IP operations performed by a given process. It works on Windows 7+ and requires .NET 4.5.2+. The available options are:

```
Usage: wtrace [OPTIONS] pid|imagename args

Options:
      --newconsole           Start the process in a new console window.
      --summary              Prints only a summary of the collected trace.
      --nosummary            Prints only ETW events - no summary at the end.
  -h, --help                 Show this message and exit
  -?                         Show this message and exit
```

A sample session might look as follows:

```
> wtrace notepad
1161,2723 (5548) FileIO/Create 'C:\' (0xFFFFFA8019C23E60) rw-
1161,3690 (5548) FileIO/Create 'C:\tools\edit\notepad2_x86\Notepad2.exe' (0xFFFFFA80201F2380) rw-
1161,3929 (5548) FileIO/Close 'C:\' (0xFFFFFA8019C23E60)
1161,4618 (5548) FileIO/Close 'C:\tools\edit\notepad2_x86\Notepad2.exe' (0xFFFFFA80201F2380)
1161,5182 (5548) FileIO/Create 'C:\' (0xFFFFFA801D7DB870) rw-
1161,5618 (5548) FileIO/Create 'C:\tools\edit\notepad2_x86\Notepad2.exe' (0xFFFFFA8019C23E60) rw-
1161,5791 (5548) FileIO/Close 'C:\' (0xFFFFFA801D7DB870)
1161,8943 (5548) FileIO/Close 'C:\' (0xFFFFFA8019C23E60)
1162,4092 (5548) FileIO/Create 'C:\Windows\Prefetch\NOTEPAD2.EXE-33521769.pf' (0xFFFFFA8021C6C740) ---
1162,5236 (5548) FileIO/Read 'C:\Windows\Prefetch\NOTEPAD2.EXE-33521769.pf' (0xFFFFFA8021C6C740) 0x0 32b
1162,5844 (5548) FileIO/Read 'C:\Windows\Prefetch\NOTEPAD2.EXE-33521769.pf' (0xFFFFFA8021C6C740) 0x0 221918b
1163,9732 (5548) FileIO/Read 'C:\Windows\Prefetch\NOTEPAD2.EXE-33521769.pf' (0xFFFFFA8021C6C740) 0x0 221918b
1164,1271 (5548) FileIO/Create 'C:\Device\HarddiskVolume2' (0xFFFFFA801D7DB870) rw-
1164,1752 (5548) FileIO/Read '' (0xFFFFFA801CE62DE0) 0x1D36000 131072b
1165,1263 (5548) FileIO/Read '' (0xFFFFFA801CE62DE0) 0x242A000 4096b
1165,4730 (5548) FileIO/Create 'C:\PROGRAM FILES (X86)' (0xFFFFFA80201F2380) rw-
1165,5533 (5548) FileIO/Close 'C:\tools\edit\notepad2_x86\Notepad2.exe' (0xFFFFFA80201F2380)
1165,5885 (5548) FileIO/Create 'C:\PROGRAM FILES (X86)\NVIDIA CORPORATION' (0xFFFFFA8019C23E60) rw-
1165,6402 (5548) FileIO/Close 'C:\' (0xFFFFFA8019C23E60)
1165,6713 (5548) FileIO/Create 'C:\PROGRAM FILES (X86)\NVIDIA CORPORATION\COPROCMANAGER' (0xFFFFFA80201F2380) rw-
1165,7406 (5548) FileIO/Close 'C:\tools\edit\notepad2_x86\Notepad2.exe' (0xFFFFFA80201F2380)
1165,7710 (5548) FileIO/Create 'C:\PROGRAMDATA' (0xFFFFFA8019C23E60) rw-
1165,8410 (5548) FileIO/Close 'C:\' (0xFFFFFA8019C23E60)
1165,8711 (5548) FileIO/Create 'C:\PROGRAMDATA\NVIDIA CORPORATION' (0xFFFFFA80201F2380) rw-
1165,9169 (5548) FileIO/Close 'C:\tools\edit\notepad2_x86\Notepad2.exe' (0xFFFFFA80201F2380)
1165,9458 (5548) FileIO/Create 'C:\PROGRAMDATA\NVIDIA CORPORATION\DRS' (0xFFFFFA8019C23E60) rw-
…
8795,2575 (5548) FileIO/Close 'C:\WINDOWS\SYSWOW64\NVINIT.DLL' (0xFFFFFA8019D05B70)
8795,3008 (5548) FileIO/Close 'C:\WINDOWS\SYSTEM32\' (0xFFFFFA801F17B330)
8795,4049 (5548) FileIO/Close 'C:\WINDOWS\SYSWOW64\SSPICLI.DLL' (0xFFFFFA8019C10310)
======= ETW session =======
### ETW session stopped. Number of lost events: 0

======= System Configuration =======
Host: H46237 (intern.kmd.dk)
CPU: 2793MHz 8cores 32382MB
LOGICAL DISK: 0 C: NTFS 238GB
NIC: VMware Virtual Ethernet Adapter for VMnet1 fe80::18d4:b77e:29f8:a912;192.168.83.1
NIC: VMware Virtual Ethernet Adapter for VMnet8 fe80::2471:4d56:2d03:697c;192.168.19.1
NIC: Software Loopback Interface 1 ::1;127.0.0.1
NIC: Microsoft ISATAP Adapter #5 fe80::5efe:192.168.83.1
NIC: Microsoft ISATAP Adapter #4 fe80::5efe:192.168.19.1

======= File I/O =======
File name  Writes / Reads (bytes)
C:\tools\edit\notepad2_x86\notepad2.ini 7 140 / 2 209 860
 0 / 1 757 376
C:\tools\edit\notepad2_x86\notepad2.exe 0 / 1 454 080
C:\Windows\Prefetch\NOTEPAD2.EXE-33521769.pf 0 / 443 868
C:\WINDOWS\SYSTEM32\C_437.NLS 0 / 70 722
C:\WINDOWS\MICROSOFT.NET\FRAMEWORK\V2.0.50727\SHFUSION.DLL 0 / 53 248
C:\TOOLS\EDIT\NOTEPAD2_X86\NOTEPAD2.INI 0 / 38 132
C:\WINDOWS\MICROSOFT.NET\FRAMEWORK\V2.0.50727\CULTURE.DLL 0 / 20 480
C:\WINDOWS\MICROSOFT.NET\FRAMEWORK\V2.0.50727\FUSION.DLL 0 / 8 192
C:\WINDOWS\MICROSOFT.NET\FRAMEWORK\V2.0.50727\SHFUSRES.DLL 0 / 8 192
C:\WINDOWS\SYSWOW64\COMDLG32.DLL 0 / 4 096
C:\Windows\Fonts\staticcache.dat 0 / 60
C:\ProgramData\NVIDIA Corporation\Drs\nvdrssel.bin 0 / 1

======= Process/Thread =======
Number of child processes started: 0
Number of threads started: 0
```

## Links

- [Releasing wtrace 1.0 and procgov 2.0](https://lowleveldesign.wordpress.com/2016/10/21/releasing-wtrace-1-0-and-procgov-2-0/)
