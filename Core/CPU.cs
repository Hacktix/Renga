using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Renga.Core
{
    public enum ALUOperation
    {
        ADD, ADC, SUB, SBC, AND, XOR, OR, CP
    }

    internal class CPU
    {
        private byte _regF = 0;

        public byte A = 0;
        public byte F {
            get { return _regF; }
            set { _regF = (byte)(value & 0xF0); }
        }
        public byte B = 0;
        public byte C = 0;
        public byte D = 0;
        public byte E = 0;
        public byte H = 0;
        public byte L = 0;

        public ushort AF
        {
            get { return (ushort)(F + (A << 8)); }
            set
            {
                _regF = (byte)(value & 0xF0);
                A = (byte)(value >> 8);
            }
        }
        public ushort BC
        {
            get { return (ushort)(C + (B << 8)); }
            set
            {
                C = (byte)(value & 0xFF);
                B = (byte)(value >> 8);
            }
        }
        public ushort DE
        {
            get { return (ushort)(E + (D << 8)); }
            set
            {
                E = (byte)(value & 0xFF);
                D = (byte)(value >> 8);
            }
        }
        public ushort HL
        {
            get { return (ushort)(L + (H << 8)); }
            set
            {
                L = (byte)(value & 0xFF);
                H = (byte)(value >> 8);
            }
        }

        public ushort SP = 0;
        public ushort PC = 0;

        public bool FlagZ
        {
            get { return (F & (1 << 7)) != 0; }
            set
            {
                if (value)
                    _regF |= (1 << 7);
                else
                    unchecked { _regF &= (byte)~(1 << 7); }
            }
        }
        public bool FlagN
        {
            get { return (F & (1 << 6)) != 0; }
            set
            {
                if (value)
                    _regF |= (1 << 6);
                else
                    unchecked { _regF &= (byte)~(1 << 6); }
            }
        }
        public bool FlagH
        {
            get { return (F & (1 << 5)) != 0; }
            set
            {
                if (value)
                    _regF |= (1 << 5);
                else
                    unchecked { _regF &= (byte)~(1 << 5); }
            }
        }
        public bool FlagC
        {
            get { return (F & (1 << 4)) != 0; }
            set
            {
                if (value)
                    _regF |= (1 << 4);
                else
                    unchecked { _regF &= (byte)~(1 << 4); }
            }
        }

        public MemoryBus Memory;
        
        private Queue<Action> _actionQueue = new Queue<Action>();

        public byte Opcode = 0;
        public byte Operand1 = 0;
        public byte Operand2 = 0;

        private Dictionary<byte, Action> _opcodeMap = new Dictionary<byte, Action>();
        private Dictionary<byte, Action> _opcodeMapCB = new Dictionary<byte, Action>();

        public CPU(MemoryBus memory)
        {
            Memory = memory;

            InitializeOpcodeMaps();
            _actionQueue.Enqueue(FetchInstruction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tick()
        {
            _actionQueue.Dequeue()();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnqueueInstructionOperations(params Action[] actions)
        {
            foreach(Action a in actions)
                _actionQueue.Enqueue(a);
            _actionQueue.Enqueue(FetchInstruction);
        }

        public void FetchInstruction()
        {
            // TODO: Check for interrupts

            Console.WriteLine($"AF: ${AF:X4} BC: ${BC:X4} DE: ${DE:X4} HL: ${HL:X4} PC: ${PC:X4} SP: ${SP:X4} | {Memory.Read(PC):X2} {Memory.Read((ushort)(PC+1)):X2} {Memory.Read((ushort)(PC+2)):X2}");

            Opcode = FetchNextByte();
            try
            {
                _opcodeMap[Opcode]();
            }
            catch (Exception)
            {
                throw new NotImplementedException($"Encountered unknown opcode ${Opcode:X2} at memory address ${PC-1:X4}");
            }
        }

        public void FetchInstructionCB()
        {

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte FetchNextByte() { return Memory.Read(PC++); }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OperationALU8(ALUOperation operation, byte value)
        {
            byte cFlagVal = (byte)(FlagC ? 1 : 0);
            switch(operation)
            {
                case ALUOperation.ADD:
                    FlagH = (A & 0xF) + (value & 0xF) > 0xF;
                    FlagC = A + value > 0xFF;
                    A += value;
                    FlagZ = A == 0;
                    FlagN = false;
                    break;
                case ALUOperation.ADC:
                    FlagH = (A & 0xF) + (value & 0xF) + cFlagVal > 0xF;
                    FlagC = A + value + cFlagVal > 0xFF;
                    A += (byte)(value + cFlagVal);
                    FlagZ = A == 0;
                    FlagN = false;
                    break;
                case ALUOperation.SUB:
                    FlagH = (A & 0xF) - (value & 0xF) < 0;
                    FlagC = A - value < 0;
                    A -= value;
                    FlagZ = A == 0;
                    FlagN = true;
                    break;
                case ALUOperation.SBC:
                    FlagH = (A & 0xF) - (value & 0xF) - cFlagVal < 0;
                    FlagC = A - value - cFlagVal < 0;
                    A -= (byte)(value + cFlagVal);
                    FlagZ = A == 0;
                    FlagN = true;
                    break;
                case ALUOperation.AND:
                    FlagN = false;
                    FlagH = true;
                    FlagC = false;
                    A &= value;
                    FlagZ = A == 0;
                    break;
                case ALUOperation.XOR:
                    FlagN = false;
                    FlagH = false;
                    FlagC = false;
                    A ^= value;
                    FlagZ = A == 0;
                    break;
                case ALUOperation.OR:
                    FlagN = false;
                    FlagH = false;
                    FlagC = false;
                    A |= value;
                    FlagZ = A == 0;
                    break;
                case ALUOperation.CP:
                    FlagH = (A & 0xF) - (value & 0xF) < 0;
                    FlagC = A - value < 0;
                    FlagZ = A == value;
                    FlagN = true;
                    break;
            }
        }

        private void InitializeOpcodeMaps()
        {
            #region 16-bit Immediate Loads
            _opcodeMap[0x01] = () => {
                EnqueueInstructionOperations(
                    () => C = FetchNextByte(),
                    () => B = FetchNextByte()
                );
            };
            _opcodeMap[0x11] = () => {
                EnqueueInstructionOperations(
                    () => E = FetchNextByte(),
                    () => D = FetchNextByte()
                );
            };
            _opcodeMap[0x21] = () => {
                EnqueueInstructionOperations(
                    () => L = FetchNextByte(),
                    () => H = FetchNextByte()
                );
            };
            _opcodeMap[0x31] = () => {
                EnqueueInstructionOperations(
                    () => SP = (ushort)((SP & 0xFF00) | FetchNextByte()),
                    () => SP = (ushort)((SP & 0xFF) | (FetchNextByte() << 8))
                );
            };
            #endregion

            #region 8-bit ALU
            _opcodeMap[0x80] = () => { OperationALU8(ALUOperation.ADD, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x81] = () => { OperationALU8(ALUOperation.ADD, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x82] = () => { OperationALU8(ALUOperation.ADD, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x83] = () => { OperationALU8(ALUOperation.ADD, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x84] = () => { OperationALU8(ALUOperation.ADD, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x85] = () => { OperationALU8(ALUOperation.ADD, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x86] = () => { EnqueueInstructionOperations(() => { Operand1 = FetchNextByte(); OperationALU8(ALUOperation.ADD, Operand1); }); };
            _opcodeMap[0x87] = () => { OperationALU8(ALUOperation.ADD, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x88] = () => { OperationALU8(ALUOperation.ADC, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x89] = () => { OperationALU8(ALUOperation.ADC, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8A] = () => { OperationALU8(ALUOperation.ADC, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8B] = () => { OperationALU8(ALUOperation.ADC, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8C] = () => { OperationALU8(ALUOperation.ADC, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8D] = () => { OperationALU8(ALUOperation.ADC, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8E] = () => { EnqueueInstructionOperations(() => { Operand1 = FetchNextByte(); OperationALU8(ALUOperation.ADC, Operand1); }); };
            _opcodeMap[0x8F] = () => { OperationALU8(ALUOperation.ADC, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x90] = () => { OperationALU8(ALUOperation.SUB, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x91] = () => { OperationALU8(ALUOperation.SUB, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x92] = () => { OperationALU8(ALUOperation.SUB, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x93] = () => { OperationALU8(ALUOperation.SUB, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x94] = () => { OperationALU8(ALUOperation.SUB, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x95] = () => { OperationALU8(ALUOperation.SUB, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x96] = () => { EnqueueInstructionOperations(() => { Operand1 = FetchNextByte(); OperationALU8(ALUOperation.SUB, Operand1); }); };
            _opcodeMap[0x97] = () => { OperationALU8(ALUOperation.SUB, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x98] = () => { OperationALU8(ALUOperation.SBC, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x99] = () => { OperationALU8(ALUOperation.SBC, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9A] = () => { OperationALU8(ALUOperation.SBC, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9B] = () => { OperationALU8(ALUOperation.SBC, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9C] = () => { OperationALU8(ALUOperation.SBC, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9D] = () => { OperationALU8(ALUOperation.SBC, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9E] = () => { EnqueueInstructionOperations(() => { Operand1 = FetchNextByte(); OperationALU8(ALUOperation.SBC, Operand1); }); };
            _opcodeMap[0x9F] = () => { OperationALU8(ALUOperation.SBC, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xA0] = () => { OperationALU8(ALUOperation.AND, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA1] = () => { OperationALU8(ALUOperation.AND, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA2] = () => { OperationALU8(ALUOperation.AND, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA3] = () => { OperationALU8(ALUOperation.AND, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA4] = () => { OperationALU8(ALUOperation.AND, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA5] = () => { OperationALU8(ALUOperation.AND, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA6] = () => { EnqueueInstructionOperations(() => { Operand1 = FetchNextByte(); OperationALU8(ALUOperation.AND, Operand1); }); };
            _opcodeMap[0xA7] = () => { OperationALU8(ALUOperation.AND, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xA8] = () => { OperationALU8(ALUOperation.XOR, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA9] = () => { OperationALU8(ALUOperation.XOR, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAA] = () => { OperationALU8(ALUOperation.XOR, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAB] = () => { OperationALU8(ALUOperation.XOR, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAC] = () => { OperationALU8(ALUOperation.XOR, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAD] = () => { OperationALU8(ALUOperation.XOR, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAE] = () => { EnqueueInstructionOperations(() => { Operand1 = FetchNextByte(); OperationALU8(ALUOperation.XOR, Operand1); }); };
            _opcodeMap[0xAF] = () => { OperationALU8(ALUOperation.XOR, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xB0] = () => { OperationALU8(ALUOperation.OR, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB1] = () => { OperationALU8(ALUOperation.OR, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB2] = () => { OperationALU8(ALUOperation.OR, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB3] = () => { OperationALU8(ALUOperation.OR, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB4] = () => { OperationALU8(ALUOperation.OR, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB5] = () => { OperationALU8(ALUOperation.OR, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB6] = () => { EnqueueInstructionOperations(() => { Operand1 = FetchNextByte(); OperationALU8(ALUOperation.OR, Operand1); }); };
            _opcodeMap[0xB7] = () => { OperationALU8(ALUOperation.OR, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xB8] = () => { OperationALU8(ALUOperation.CP, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB9] = () => { OperationALU8(ALUOperation.CP, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBA] = () => { OperationALU8(ALUOperation.CP, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBB] = () => { OperationALU8(ALUOperation.CP, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBC] = () => { OperationALU8(ALUOperation.CP, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBD] = () => { OperationALU8(ALUOperation.CP, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBE] = () => { EnqueueInstructionOperations(() => { Operand1 = FetchNextByte(); OperationALU8(ALUOperation.CP, Operand1); }); };
            _opcodeMap[0xBF] = () => { OperationALU8(ALUOperation.CP, A); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region Miscellaneous
            _opcodeMap[0x00] = () => { _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xCB] = () => { _actionQueue.Enqueue(FetchInstructionCB); };
            #endregion
        }
    }
}
