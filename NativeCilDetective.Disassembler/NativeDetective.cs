using System.IO;

namespace NativeCilDetective.Disassembler
{
    public class NativeDetective
    {
        public OffsetsCollector Offsets { get; private set; }
        public MethodDisassembler Disassembler { get; private set; }

        public NativeDetective(string il2cppDumpPath, string nativeAssemblyPath)
        {
            Offsets = new OffsetsCollector(il2cppDumpPath);
            Offsets.ReadStrings();
            Offsets.ReadMethods();

            Disassembler = new MethodDisassembler(File.ReadAllBytes(nativeAssemblyPath), Offsets);
        }
    }
}
