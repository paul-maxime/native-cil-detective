using Mono.Cecil;
using System.Collections.Generic;
using System.IO;

namespace NativeCilDetective.Disassembler
{
    public class NativeDetective
    {
        public OffsetsCollector Offsets { get; private set; }
        public MethodDisassembler Disassembler { get; private set; }
        public MethodUsageFinder UsageFinder { get; set; }

        public NativeDetective(string il2cppDumpPath, string nativeAssemblyPath)
        {
            Offsets = new OffsetsCollector(il2cppDumpPath);
            Offsets.ReadStrings();
            Offsets.ReadMethods();

            Disassembler = new MethodDisassembler(File.ReadAllBytes(nativeAssemblyPath), Offsets);

            UsageFinder = new MethodUsageFinder(Offsets, Disassembler);
        }

        public IList<TranslatedInstruction> DisassembleMethod(MethodDefinition method)
        {
            long offset = OffsetsCollector.GetMethodOffset(method);
            if (offset == -1) return null;
            return Disassembler.DisassembleMethod(method, OffsetsCollector.GetMethodOffset(method));
        }
    }
}
