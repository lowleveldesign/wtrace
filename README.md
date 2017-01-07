
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

**Please visit [wiki](https://github.com/lowleveldesign/wtrace/wiki) to learn more!**

## Links

- [Releasing wtrace 1.0 and procgov 2.0](https://lowleveldesign.wordpress.com/2016/10/21/releasing-wtrace-1-0-and-procgov-2-0/)
