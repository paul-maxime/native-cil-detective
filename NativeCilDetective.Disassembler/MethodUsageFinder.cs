using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace NativeCilDetective.Disassembler
{
    public class MethodUsageFinder
    {
        public class Usage
        {
            public MethodDefinition Method { get; set; }
            public int Count { get; set; }
        }

        private readonly OffsetsCollector offsets;
        private readonly MethodDisassembler disassembler;

        public MethodUsageFinder(OffsetsCollector offsets, MethodDisassembler disassembler)
        {
            this.offsets = offsets;
            this.disassembler = disassembler;
        }

        public List<Usage> FindUsages(MethodDefinition methodToFind)
        {
            var usages = new List<Usage>();

            foreach (MethodDefinition method in offsets.Methods)
            {
                long offset = OffsetsCollector.GetMethodOffset(method);
                if (offset > -1)
                {
                    int count = GetMethodUsagesInMethod(methodToFind, method);
                    if (count > 0)
                    {
                        usages.Add(new Usage { Method = method, Count = count });
                    }
                }
            }

            return usages;
        }

        private int GetMethodUsagesInMethod(MethodDefinition methodToFind, MethodDefinition methodToSearch)
        {
            var instructions = disassembler.DisassembleMethod(OffsetsCollector.GetMethodOffset(methodToSearch));
            return instructions.Where(x => x.CalledMethod == methodToFind).Count();
        }

        public List<Usage> FindUsages(FieldDefinition fieldToFind)
        {
            var usages = new List<Usage>();

            foreach (MethodDefinition method in offsets.Methods)
            {
                long offset = OffsetsCollector.GetMethodOffset(method);
                if (offset > -1)
                {
                    int count = GetFieldUsagesInMethod(fieldToFind, method);
                    if (count > 0)
                    {
                        usages.Add(new Usage { Method = method, Count = count });
                    }
                }
            }

            return usages;
        }

        private int GetFieldUsagesInMethod(FieldDefinition fieldToFind, MethodDefinition methodToSearch)
        {
            var instructions = disassembler.DisassembleMethod(OffsetsCollector.GetMethodOffset(methodToSearch));
            return instructions.Where(x => x.UsedField == fieldToFind).Count();
        }
    }
}
