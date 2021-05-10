using Iced.Intel;
using Mono.Cecil;
using System;
using System.Collections.Generic;

namespace NativeCilDetective.Disassembler
{
    public class MethodDisassembler
    {
        public OffsetsCollector Offsets { get; }
        public byte[] NativeDllCode { get; }

        public MethodDisassembler(byte[] nativeDllCode, OffsetsCollector offsets)
        {
            NativeDllCode = nativeDllCode;
            Offsets = offsets;
        }

        public IList<TranslatedInstruction> DisassembleMethod(long methodOffset)
        {
            var method = Offsets.MethodsFromOffsets[methodOffset];
            if (method == null)
            {
                throw new Exception($"Could not find a method at the offset 0x{methodOffset:X}.");
            }

            return DisassembleMethod(method, methodOffset);
        }

        public IList<TranslatedInstruction> DisassembleMethod(MethodDefinition method, long methodOffset)
        {
            InstructionList instructions = DisassembleMethodInstructions(methodOffset);
            return TranslateInstructions(instructions, method, methodOffset);
        }

        private InstructionList DisassembleMethodInstructions(long methodOffset)
        {
            const int NativeBitness = 64;
            const int MaxMethodLength = 0xFFFF;

            var codeReader = new ByteArrayCodeReader(NativeDllCode, (int)methodOffset, (int)Math.Min(MaxMethodLength, NativeDllCode.Length - methodOffset));
            var decoder = Decoder.Create(NativeBitness, codeReader);
            decoder.InstructionPointer = 0;
            ulong endRip = decoder.InstructionPointer + MaxMethodLength;

            var instructions = new InstructionList();
            int int3count = 0;
            while (decoder.InstructionPointer < endRip)
            {
                if (decoder.InstructionPointer > 0)
                {
                    long currentOffset = methodOffset + (long)decoder.InstructionPointer;
                    if (Offsets.MethodsFromOffsets.ContainsKey(currentOffset))
                    {
                        break;
                    }
                }
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID)
                {
                    break;
                }
                else if (instruction.Code == Code.Int3)
                {
                    int3count++;
                    if (int3count >= 2)
                    {
                        break;
                    }
                }
                else
                {
                    int3count = 0;
                }
                instructions.Add(instruction);
            }

            return instructions;
        }

        private IList<TranslatedInstruction> TranslateInstructions(InstructionList instructions, MethodDefinition currentMethod, long methodOffset)
        {
            InstructionTranslater translater = new InstructionTranslater(Offsets, currentMethod, methodOffset);

            var translated = new List<TranslatedInstruction>();
            ulong offset = 0;
            foreach (var instruction in instructions)
            {
                translated.Add(translater.Translate(instruction, offset, translated));
                offset += (ulong)instruction.ByteLength;
            }

            return translated;
        }
    }
}
