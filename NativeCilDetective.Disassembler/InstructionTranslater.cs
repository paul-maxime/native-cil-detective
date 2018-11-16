using Iced.Intel;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace NativeCilDetective.Disassembler
{
    public class InstructionTranslater
    {
        public abstract class RegisterValue { }

        public class ThisValue : RegisterValue
        {
        }

        public class NumberValue : RegisterValue
        {
            public int Value { get; set; }
        }

        private readonly OffsetsCollector offsets;
        private readonly MethodDefinition currentMethod;
        private readonly long currentMethodOffset;

        private IDictionary<Register, RegisterValue> registers;

        public InstructionTranslater(OffsetsCollector offsets, MethodDefinition currentMethod, long currentMethodOffset)
        {
            this.offsets = offsets;
            this.currentMethod = currentMethod;
            this.currentMethodOffset = currentMethodOffset;

            Init();
        }

        private void Init()
        {
            registers = new Dictionary<Register, RegisterValue>
            {
                [Register.RAX] = null, // return value
                [Register.RBX] = null, // temp variables
                [Register.RCX] = null, // this
                [Register.RDX] = null, // 1st param
                [Register.R8] = null, // 2nd param
                [Register.R9] = null, // 3rd param
                [Register.RDI] = null, // newly constructed variables
            };

            if (!currentMethod.IsStatic && !currentMethod.IsConstructor)
            {
                registers[Register.RCX] = new ThisValue();
            }
        }

        public TranslatedInstruction Translate(Instruction instruction, ulong instructionOffset)
        {
            if (instruction.OpCount == 2 && instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Memory && instruction.MemoryBase == Register.RIP)
            {
                long offset = (long)instruction.IPRelativeMemoryAddress + currentMethodOffset;
                if (offsets.StringsFromOffsets.ContainsKey(offset))
                {
                    return new TranslatedInstruction(instruction, instructionOffset, offsets.StringsFromOffsets[offset]);
                }
            }

            if (instruction.Code == Code.Call_rel32_64 || instruction.Code == Code.Jmp_rel32_64)
            {
                if (instruction.OpCount == 1 && instruction.Op0Kind == OpKind.NearBranch64)
                {
                    long offset = (long)instruction.NearBranch64 + currentMethodOffset;
                    if (offsets.MethodsFromOffsets.ContainsKey(offset))
                    {
                        var method = offsets.MethodsFromOffsets[offset];
                        return new TranslatedInstruction(instruction, instructionOffset, method);
                    }
                }
            }

            if (instruction.OpCount == 2 && (instruction.Op0Kind == OpKind.Memory || instruction.Op1Kind == OpKind.Memory))
            {
                if (instruction.MemoryIndexScale == 1 && registers.ContainsKey(instruction.MemoryBase) && registers[instruction.MemoryBase] is ThisValue)
                {
                    long offset = instruction.MemoryDisplacement;
                    var field = currentMethod.DeclaringType.Fields.FirstOrDefault(x => (string)x.CustomAttributes.FirstOrDefault(y => y.AttributeType.Name == "FieldOffsetAttribute")?.Fields.First(z => z.Name == "Offset").Argument.Value == "0x" + offset.ToString("X"));
                    if (field != null)
                    {
                        return new TranslatedInstruction(instruction, instructionOffset, field);
                    }
                }
            }

            if (instruction.Code == Code.Mov_r64_rm64)
            {
                if (instruction.OpCount == 2 && instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Register && registers.ContainsKey(instruction.Op0Register) && registers.ContainsKey(instruction.Op1Register))
                {
                    registers[instruction.Op0Register] = registers[instruction.Op1Register];
                }
                else if (instruction.OpCount == 2 && instruction.Op0Kind == OpKind.Register && registers.ContainsKey(instruction.Op0Register))
                {
                    registers[instruction.Op0Register] = null;
                }
            }

            if (instruction.Code == Code.Xor_r32_rm32)
            {
                if (instruction.OpCount == 2 && instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Register)
                {
                    if (instruction.Op0Register == instruction.Op1Register)
                    {
                        var op0Register = instruction.Op0Register;
                        registers[op0Register] = new NumberValue { Value = 0 };
                    }
                }
            }

            return new TranslatedInstruction(instruction, instructionOffset);
        }
    }
}
