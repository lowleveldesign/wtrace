
# wtrace

![.NET](https://github.com/lowleveldesign/wtrace/workflows/build/badge.svg)

------------------------

**:warning: Some antivirus engines mark wtrace as malware/virus :warning:**

Those are **false-positives**. As you know, wtrace source code is open, and all the officially released binaries are built using GitHub Actions (you may check the workflow in the [release.yml](.github/workflows/release.yml) file). If your antivirus thinks that wtrace is malware, calculate the binary checksum, and if it matches the one on the release page, please report it as false-positive. Thank you!

------------------------

**Table of contents**:

<!-- MarkdownTOC -->

- [Introduction](#introduction)
- [Installation](#installation)
- [Tracing targets](#tracing-targets)
    - [System-only \(-s\)](#system-only-s)
    - [System-wide](#system-wide)
    - [A single process \(optionally, with child processes\)](#a-single-process-optionally-with-child-processes)
- [Filtering events](#filtering-events)
- [Event handlers](#event-handlers)
- [RPC](#rpc)
- [Error messages](#error-messages)
    - [WARNING: the session did not finish in the allotted time.](#warning-the-session-did-not-finish-in-the-allotted-time)
    - [WARNING: … events were lost in the session.](#warning-events-were-lost-in-the-session)
    - [Other issues](#other-issues)
- [Thanks](#thanks)

<!-- /MarkdownTOC -->

## Introduction

Wtrace [spelled: *wɪtreɪs*] is a command-line tool for recording trace events from the Operating System or a group of processes. Wtrace may collect, among others, **File I/O** and **Registry** operations, **TPC/IP** connections, and **RPC** calls. Its purpose is to give you some insights into what is happening in the system.

Additionally, it has various **filtering capabilities** and may also dump statistics at the end of the trace session. As it's just a standard command-line tool, you may pipe its output to another tool for further processing.

The available options are listed below:

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

  The HANDLERS parameter is a list of handler names, separated with a comma.

  Accepted handlers include:
    process   - only Process/Thread events (this handler is always enabled)
    file      - File I/O events
    registry  - Registry events (voluminous, disabled by default)
    rpc       - RPC events (enable image handler to allow RPC method name resolution)
    tcp       - TCP/IP events
    udp       - UDP events
    image     - image (module) events (load/unload)

  Example: --handlers 'tcp,file,registry'

  Each FILTER is built from a keyword, an operator, and a value. You may
  define multiple events (filters with the same keywords are OR-ed).

  Keywords include:
    pid     - filtering on the proces ID
    pname   - filtering on on the process name
    name    - filtering on the event name
    level   - filtering on the event level (1 [critical] - 5 [debug])
    path    - filtering on the event path
    details - filtering on the event details

  Operators include:
    =, <> (does not equal), <= (ends with), >= (starts with), ~ (contains)

  Example: -f 'pid = 1234', -f 'name ~ FileIO', -f 'level <= 4'
```

## Installation

Wtrace works on Windows 8.1+ and requires .NET 4.8.x. It is a **single file** application, and you may download the latest version from the [release page](https://github.com/lowleveldesign/wtrace/releases).

Alternatively, you may install wtrace using [Chocolatey](https://chocolatey.org/):

```
choco install wtrace
```

## Tracing targets

Wtrace may trace drivers, all processes in the system, or only a specific process with its children.

### System-only (-s)

The **-s option** (**system-only**) is a special mode in which wtrace collects statistics of the **ISR/DPC and process events**. It later dumps them at the end of the trace session. It will also show the tree of processes running during the session.

### System-wide

To trace all processes (**system-wide**), run wtrace with **no arguments**. Tracing system-wide produces lots of events, and if no filtering is applied, there is a risk that wtrace will lose some events. Therefore, I highly recommend setting event filters or limit the number of handles in the system-wide sessions. If you want to trace the system for a longer time, consider adding the **–no-summary** option. This option will turn off the statistics, keeping wtrace memory usage minimum.

```
# show File write events from all the processes
wtrace --handlers file -f ‘eventname=FileIO/Write’

# show RPC events from all the processes
wtrace --handlers rpc
```

### A single process (optionally, with child processes)

Wtrace can either trace a **running process** or start and **trace a new process**. In both scenarios, adding the **-c/--children** option makes wtrace also trace the processes launched by the target process, including their future children.

If **the first command-line argument is a number**, wtrace assumes that it's a process ID that it should start tracing:

```
# Trace File I/O operations of the process with id 1234 and its children
wtrace -f “name >= FileIO/” -c 1234
```

If **the command-line argument is not a number**, wtrace tries to start the process with arguments that follow the executable path.

```
# Start and trace the opening of the test.txt file by notepad.exe
wtrace notepad c:\temp\test.txt
```

## Filtering events

We may define an event filter with the **-f/--filter** option. The filter is built from a **keyword**, an **operator**, and a **value**. The **keyword** represents an event field and must be one of the following values:

- **pid** - the process ID (useful in system-wide tracing)
- **pname** - the process name
- **name** - the event name
- **level** - the event level (debug [5], info [4], warning [3], error [2], critical [1])
- **path** - the event path
- **details** - the event details

The **operators** are the same for numeric and text values and include: =, <>, <=, >=, ~. For numbers, the ~ operator has the same effect as the = operator. For text fields, the >= operator returns true if the field value starts with a given text value. Consequently, the <= operator returns true if the field value ends with a given text value. The ~ operator returns true if the field value contains a given text value. The text filters are case-insensitive.

The **value** part of the filter string is everything that comes after the operator sign, except for white spaces at the beginning and the end of the text value. Therefore, you don't need to use any apostrophes inside the filter text unless you want them to be a part of the text value.

You may define **multiple filters** for a trace session. Wtrace combines them similarly to Process Monitor, so **filters with the same keyword** are OR-ed together (disjunction). **Filters which keywords differ are AND-ed together** (conjunction). At the start, wtrace will print the parsed filters so you can verify if it's what you expected. Event filters do not affect statistics. If the statistics collection is on (you haven't used the --nosummary flag), you will see the statistics at the end of the session for all the enabled handlers' events (check the Event Handlers section to learn more)

```
# Trace system-wide and filter events for processes which name 
# is either notepad or notepad2 and the path starts with "d:\temp"
wtrace -f “pname = notepad” -f “pname = notepad2” -f “path >= d:\temp”
```

```
# Trace a process with id 12572 and its children and show only TCP/IP events
wtrace -f "name >= tcp" -c 12572
```

## Event handlers

Apart from defining filters, we may also specify which handlers wtrace should enable in the session. Handlers are the components responsible for collecting and parsing trace events. Each handler handles a unique set of events. If we disable a handler, none of its events will appear in the live trace output. The statistics built from the handler's events will also be missing. The following handlers are available:

- **process** - for collecting process and thread lifetime events
- **file** - for collecting File I/O events
- **registry** - for collecting Windows Registry events (careful, > 1000 events / s)
- **rpc** - for collecting RPC events
- **tcp** - for collecting TCP/IP events
- **udp** - for collecting UDP events
- **image** - for collecting module load/unload events (this handler is required for RPC endpoint parsing)

By default, wtrace enables process, image, file, rpc,tcp, and udp handlers for a trace session. Even when tracing system-wide, this set of handlers should not be too voluminous and should not overload the console output. However, if you enable, for example, the registry handler, the number of events might quickly make the console window unusable. Therefore, it's essential to choose the right set of handlers for a session and apply filters, if only possible.

```
# Trace only registry and tcp events system-wide
wtrace --handlers registry,tcp
```

## RPC

Wtrace displays the endpoint name, the interface ID, and the procedure index, for example:

```
14:14:53.3295 firefox (12572.21620) RPC/ClientCallEnd 'fb8a0729-2d04-4658-be93-27b4ad553fac (lsapolicylookup) [5]' -> SUCCESS
```

Thanks to the [NtApiDotNet](https://www.nuget.org/packages/NtApiDotNet) library, wtrace may resolve RPC procedure names in the summary view. To make it work, make sure the image handler is enabled and you have symbols configured for the wtrace session. If you have the `_NT_SYMBOL_PATH` environment variable set (I highly recommend configuring it), wtrace will use it. Otherwise, you need to set debugging symbols path through the `--symbols` parameter, for example:

```
wtrace.exe --symbols="SRV*C:\symbols\*https://msdl.microsoft.com/download/symbols" -v notepad.exe
```

If RPC procedure name resolution worked, you should see a procedure name in the curly braces, next to the procedure number:

```
--------------------------------
       RPC (client calls)
--------------------------------
fb8a0729-2d04-4658-be93-27b4ad553fac (ncalrpc:[lsapolicylookup]) [5]{LsaLookuprGetDomainInfo} calls: 2

```

## Error messages

### WARNING: the session did not finish in the allotted time.

This warning may indicate a problem with ETW session handling. If it happens, the wtrace ETW session might still be running in your system. You may stop it using the logman tool:

```
logman stop wtrace-rt -ets
```

### WARNING: … events were lost in the session.

This warning usually indicates that the number of events was too high, and wtrace could not process them. In such a case, add some additional filters to the command line or disable the unneeded handlers.

### Other issues

If you find an error in wtrace, please [report it on GitHub](https://github.com/lowleveldesign/wtrace/issues), providing the error message and steps to reproduce the problem. Thank you!

## Thanks

I would like to thank the authors of the [TraceEvent](https://github.com/Microsoft/perfview) and [NtApiDotNet](https://github.com/googleprojectzero/sandbox-attacksurface-analysis-tools) libraries. Wtrace would not exist without those libraries.
