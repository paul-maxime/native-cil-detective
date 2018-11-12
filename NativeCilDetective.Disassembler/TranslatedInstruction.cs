using Iced.Intel;
using Mono.Cecil;

namespace NativeCilDetective.Disassembler
{
    public class TranslatedInstruction
    {
        public Instruction AsmInstruction { get; }
        public ulong InstructionOffset { get; }

        public MethodDefinition CalledMethod { get; }
        public FieldDefinition UsedField { get; }
        public string StringContent { get; }

        public TranslatedInstruction(Instruction instruction, ulong instructionOffset)
        {
            AsmInstruction = instruction;
            InstructionOffset = instructionOffset;
        }

        public TranslatedInstruction(Instruction instruction, ulong instructionOffset, MethodDefinition calledMethod)
        {
            AsmInstruction = instruction;
            InstructionOffset = instructionOffset;
            CalledMethod = calledMethod;
        }

        public TranslatedInstruction(Instruction instruction, ulong instructionOffset, FieldDefinition usedField)
        {
            AsmInstruction = instruction;
            InstructionOffset = instructionOffset;
            UsedField = usedField;
        }

        public TranslatedInstruction(Instruction instruction, ulong instructionOffset, string stringContent)
        {
            AsmInstruction = instruction;
            InstructionOffset = instructionOffset;
            StringContent = stringContent;
        }
    }
}
