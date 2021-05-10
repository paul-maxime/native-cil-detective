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
            public ulong Value { get; set; }
        }

        public class PointerValue : RegisterValue
        {
            public RegisterValue Content { get; set; }
        }

        public class NewInstanceValue : RegisterValue
        {
            public TypeDefinition InstanceType { get; set; }
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
                [Register.R10] = null,
                [Register.R11] = null,
                [Register.R12] = null,
                [Register.R13] = null,
                [Register.R14] = null,
                [Register.R15] = null,
                [Register.RDI] = null, // newly constructed variables
            };

            if (!currentMethod.IsStatic && !currentMethod.IsConstructor)
            {
                registers[Register.RDI] = new ThisValue();
            }
        }

        public TranslatedInstruction Translate(Instruction instruction, ulong instructionOffset, IList<TranslatedInstruction> previousInstructions)
        {
            if (instruction.Code == Code.Test_rm64_r64)
            {
                if (instruction.Op0Register == instruction.Op1Register && registers.ContainsKey(instruction.Op0Register))
                {
                    var lastInstruction = previousInstructions.LastOrDefault();
                    if (lastInstruction != null && lastInstruction.CalledMethod != null && lastInstruction.CalledMethod.IsConstructor)
                    {
                        registers[instruction.Op0Register] = new PointerValue
                        {
                            Content = new NewInstanceValue
                            {
                                InstanceType = lastInstruction.CalledMethod.DeclaringType
                            }
                        };
                    }
                }
            }

            if (instruction.Code == Code.Mov_r64_rm64 || instruction.Code == Code.Mov_rm64_r64)
            {
                if (instruction.Op0Kind == OpKind.Register && registers.ContainsKey(instruction.Op0Register) && instruction.Op1Kind == OpKind.Memory && instruction.MemoryIndexScale == 1 && instruction.MemoryDisplacement == 0 && registers.ContainsKey(instruction.MemoryBase) && registers[instruction.MemoryBase] is PointerValue ptrValue)
                {
                    registers[instruction.Op0Register] = ptrValue.Content;
                }
                else if (instruction.OpCount == 2 && instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Register && registers.ContainsKey(instruction.Op0Register) && registers.ContainsKey(instruction.Op1Register))
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

            if (instruction.Code == Code.Lea_r64_m)
            {
                if (instruction.OpCount == 2 && instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Memory)
                {
                    registers[instruction.Op0Register] = new NumberValue { Value = instruction.IPRelativeMemoryAddress };
                }
            }

            if (instruction.OpCount == 2 && instruction.Op0Kind == OpKind.Register && instruction.Op1Kind == OpKind.Memory && instruction.MemoryBase == Register.RIP)
            {
                long offset = (long)instruction.IPRelativeMemoryAddress + currentMethodOffset;
                if (offset != 0 && offsets.StringsFromOffsets.ContainsKey(offset))
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

            if ((instruction.Op0Kind == OpKind.Memory || instruction.Op1Kind == OpKind.Memory) && instruction.MemoryIndexScale == 1 && registers.ContainsKey(instruction.MemoryBase))
            {
                if (registers[instruction.MemoryBase] is ThisValue)
                {
                    long offset = instruction.MemoryDisplacement;
                    var field = currentMethod.DeclaringType.Fields.FirstOrDefault(x => (string)x.CustomAttributes.FirstOrDefault(y => y.AttributeType.Name == "FieldOffsetAttribute")?.Fields.First(z => z.Name == "Offset").Argument.Value == "0x" + offset.ToString("X"));
                    if (field != null)
                    {
                        return new TranslatedInstruction(instruction, instructionOffset, field);
                    }
                }
            }

            return new TranslatedInstruction(instruction, instructionOffset);
        }
    }
}
