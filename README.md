
# wtrace

This application will trace in real-time all File I/O, TCP IP, ALPC and RPC operations performed by a given process. It works on Windows 7+ and requires .NET 4.5.2+. Wtrace stops when the traced process exits, or if you issue Ctrl+C in its command line.

Use pipeline to filter the events, e.g.: `wtrace notepad | findstr  "FileIO/Write"`

It is possible to use wtrace as a **PowerShell cmdlet**. Please check the [wiki](https://github.com/lowleveldesign/wtrace/wiki/PowerShell-support) for more details.

The available options are:

```
Usage: wtrace [OPTIONS] pid|imagename args

Options:
      --newconsole           Start the process in a new console window.
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
```

**Please visit the project [wiki](https://github.com/lowleveldesign/wtrace/wiki) to learn more**

## Links

- [Releasing wtrace 1.0 and procgov 2.0](https://lowleveldesign.wordpress.com/2016/10/21/releasing-wtrace-1-0-and-procgov-2-0/)
