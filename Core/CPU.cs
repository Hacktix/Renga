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

            Renga.Log.Debug($"AF: ${AF:X4} BC: ${BC:X4} DE: ${DE:X4} HL: ${HL:X4} PC: ${PC:X4} SP: ${SP:X4} | {Memory.Read(PC):X2} {Memory.Read((ushort)(PC+1)):X2} {Memory.Read((ushort)(PC+2)):X2}");

            byte opcode = FetchNextByte();
            try
            {
                _opcodeMap[opcode]();
            }
            catch (Exception)
            {
                Renga.Log.Error($"Encountered unknown opcode ${opcode:X2} at memory address ${PC - 1:X4}");
                throw new NotImplementedException($"Encountered unknown opcode ${opcode:X2} at memory address ${PC - 1:X4}");
            }
        }

        public void FetchInstructionCB()
        {
            byte opcode = FetchNextByte();
            try
            {
                _opcodeMapCB[opcode]();
            }
            catch (Exception)
            {
                Renga.Log.Error($"Encountered unknown 0xCB opcode ${opcode:X2} at memory address ${PC - 1:X4}");
                throw new NotImplementedException($"Encountered unknown 0xCB opcode ${opcode:X2} at memory address ${PC - 1:X4}");
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OperationBIT(byte regValue, byte bitmask)
        {
            FlagN = false;
            FlagH = true;
            FlagZ = (regValue & bitmask) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OperationJR(bool condition)
        {
            if(!condition)
            {
                _actionQueue.Enqueue(() => FetchNextByte());
                _actionQueue.Enqueue(FetchInstruction);
            } else
            {
                sbyte offset = 0;
                EnqueueInstructionOperations(
                    () => offset = (sbyte)FetchNextByte(),
                    () => { },
                    () => PC = (ushort)(PC + offset)
                );
            }
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OperationCALL(bool condition)
        {
            if(!condition)
                EnqueueInstructionOperations(() => FetchNextByte(), () => FetchNextByte());
            else
            {
                ushort callAddr = 0;
                EnqueueInstructionOperations(
                    () => callAddr |= FetchNextByte(),
                    () => callAddr |= (ushort)(FetchNextByte() << 8),
                    () => { },
                    () => Memory.Write(--SP, (byte)(PC >> 8)),
                    () => { Memory.Write(--SP, (byte)(PC & 0xFF)); PC = callAddr; }
                );
            }
        }

        private void InitializeOpcodeMaps()
        {
            #region Control Flow
            _opcodeMap[0x18] = () => { OperationJR(true); };
            _opcodeMap[0x20] = () => { OperationJR(!FlagZ); };
            _opcodeMap[0x28] = () => { OperationJR(FlagZ); };
            _opcodeMap[0x30] = () => { OperationJR(!FlagC); };
            _opcodeMap[0x38] = () => { OperationJR(FlagC); };

            _opcodeMap[0xCD] = () => { OperationCALL(true); };
            _opcodeMap[0xC4] = () => { OperationCALL(!FlagZ); };
            _opcodeMap[0xCC] = () => { OperationCALL(FlagZ); };
            _opcodeMap[0xD4] = () => { OperationCALL(!FlagC); };
            _opcodeMap[0xDC] = () => { OperationCALL(FlagC); };
            #endregion

            #region Register Loads
            _opcodeMap[0x40] = () => { B = B; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x41] = () => { B = C; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x42] = () => { B = D; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x43] = () => { B = E; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x44] = () => { B = H; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x45] = () => { B = L; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x46] = () => { _actionQueue.Enqueue(() => B = Memory.Read(HL)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x47] = () => { B = A; _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x48] = () => { C = B; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x49] = () => { C = C; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x4A] = () => { C = D; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x4B] = () => { C = E; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x4C] = () => { C = H; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x4D] = () => { C = L; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x4E] = () => { _actionQueue.Enqueue(() => C = Memory.Read(HL)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x4F] = () => { C = A; _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x50] = () => { D = B; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x51] = () => { D = C; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x52] = () => { D = D; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x53] = () => { D = E; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x54] = () => { D = H; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x55] = () => { D = L; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x56] = () => { _actionQueue.Enqueue(() => D = Memory.Read(HL)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x57] = () => D = A;

            _opcodeMap[0x58] = () => { E = B; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x59] = () => { E = C; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x5A] = () => { E = D; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x5B] = () => { E = E; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x5C] = () => { E = H; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x5D] = () => { E = L; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x5E] = () => { _actionQueue.Enqueue(() => E = Memory.Read(HL)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x5F] = () => { E = A; _actionQueue.Enqueue(FetchInstruction); };


            _opcodeMap[0x60] = () => { H = B; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x61] = () => { H = C; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x62] = () => { H = D; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x63] = () => { H = E; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x64] = () => { H = H; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x65] = () => { H = L; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x66] = () => { _actionQueue.Enqueue(() => H = Memory.Read(HL)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x67] = () => { H = A; _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x68] = () => { L = B; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x69] = () => { L = C; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x6A] = () => { L = D; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x6B] = () => { L = E; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x6C] = () => { L = H; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x6D] = () => { L = L; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x6E] = () => { _actionQueue.Enqueue(() => L = Memory.Read(HL)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x6F] = () => { L = A; _actionQueue.Enqueue(FetchInstruction); };


            _opcodeMap[0x70] = () => { _actionQueue.Enqueue(() => Memory.Write(HL, B)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x71] = () => { _actionQueue.Enqueue(() => Memory.Write(HL, C)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x72] = () => { _actionQueue.Enqueue(() => Memory.Write(HL, D)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x73] = () => { _actionQueue.Enqueue(() => Memory.Write(HL, E)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x74] = () => { _actionQueue.Enqueue(() => Memory.Write(HL, H)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x75] = () => { _actionQueue.Enqueue(() => Memory.Write(HL, L)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x77] = () => { _actionQueue.Enqueue(() => Memory.Write(HL, A)); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x78] = () => { A = B; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x79] = () => { A = C; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x7A] = () => { A = D; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x7B] = () => { A = E; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x7C] = () => { A = H; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x7D] = () => { A = L; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x7E] = () => { _actionQueue.Enqueue(() => A = Memory.Read(HL)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x7F] = () => { A = A; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region MMIO Loads (LDH)
            _opcodeMap[0xE0] = () => { byte off = 0; EnqueueInstructionOperations(() => off = FetchNextByte(), () => Memory.Write((ushort)(0xFF00+off), A)); };
            _opcodeMap[0xF0] = () => { byte off = 0; EnqueueInstructionOperations(() => off = FetchNextByte(), () => A = Memory.Read((ushort)(0xFF00 + off))); };
            _opcodeMap[0xE2] = () => EnqueueInstructionOperations(() => Memory.Write((ushort)(0xFF00 + C), A));
            _opcodeMap[0xF2] = () => EnqueueInstructionOperations(() => A = Memory.Read((ushort)(0xFF00 + C)));
            #endregion

            #region 8-bit Immediate Loads
            _opcodeMap[0x06] = () => { _actionQueue.Enqueue(() => B = FetchNextByte()); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x0E] = () => { _actionQueue.Enqueue(() => C = FetchNextByte()); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x16] = () => { _actionQueue.Enqueue(() => D = FetchNextByte()); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x1E] = () => { _actionQueue.Enqueue(() => E = FetchNextByte()); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x26] = () => { _actionQueue.Enqueue(() => H = FetchNextByte()); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x2E] = () => { _actionQueue.Enqueue(() => L = FetchNextByte()); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x36] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = FetchNextByte(), () => Memory.Write(HL, tmp)); };
            _opcodeMap[0x3E] = () => { _actionQueue.Enqueue(() => A = FetchNextByte()); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region 16-bit Indirect Loads
            _opcodeMap[0x02] = () => { _actionQueue.Enqueue(() => Memory.Write(BC, A)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x12] = () => { _actionQueue.Enqueue(() => Memory.Write(DE, A)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x22] = () => { _actionQueue.Enqueue(() => Memory.Write(HL++, A)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x32] = () => { _actionQueue.Enqueue(() => Memory.Write(HL--, A)); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x0A] = () => { _actionQueue.Enqueue(() => A = Memory.Read(BC)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x1A] = () => { _actionQueue.Enqueue(() => A = Memory.Read(DE)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x2A] = () => { _actionQueue.Enqueue(() => A = Memory.Read(HL++)); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x3A] = () => { _actionQueue.Enqueue(() => A = Memory.Read(HL--)); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

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

            #region Stack Ops
            _opcodeMap[0xC5] = () => EnqueueInstructionOperations(() => { }, () => Memory.Write(--SP, B), () => Memory.Write(--SP, C));
            _opcodeMap[0xD5] = () => EnqueueInstructionOperations(() => { }, () => Memory.Write(--SP, D), () => Memory.Write(--SP, E));
            _opcodeMap[0xE5] = () => EnqueueInstructionOperations(() => { }, () => Memory.Write(--SP, H), () => Memory.Write(--SP, L));
            _opcodeMap[0xF5] = () => EnqueueInstructionOperations(() => { }, () => Memory.Write(--SP, A), () => Memory.Write(--SP, F));

            _opcodeMap[0xC1] = () => EnqueueInstructionOperations(() => C = Memory.Read(SP++), () => B = Memory.Read(SP++));
            _opcodeMap[0xD1] = () => EnqueueInstructionOperations(() => D = Memory.Read(SP++), () => E = Memory.Read(SP++));
            _opcodeMap[0xE1] = () => EnqueueInstructionOperations(() => H = Memory.Read(SP++), () => L = Memory.Read(SP++));
            _opcodeMap[0xF1] = () => EnqueueInstructionOperations(() => A = Memory.Read(SP++), () => F = Memory.Read(SP++));
            #endregion

            #region 8-bit ALU
            _opcodeMap[0x80] = () => { OperationALU8(ALUOperation.ADD, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x81] = () => { OperationALU8(ALUOperation.ADD, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x82] = () => { OperationALU8(ALUOperation.ADD, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x83] = () => { OperationALU8(ALUOperation.ADD, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x84] = () => { OperationALU8(ALUOperation.ADD, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x85] = () => { OperationALU8(ALUOperation.ADD, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x86] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.ADD, FetchNextByte()); }); };
            _opcodeMap[0x87] = () => { OperationALU8(ALUOperation.ADD, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x88] = () => { OperationALU8(ALUOperation.ADC, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x89] = () => { OperationALU8(ALUOperation.ADC, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8A] = () => { OperationALU8(ALUOperation.ADC, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8B] = () => { OperationALU8(ALUOperation.ADC, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8C] = () => { OperationALU8(ALUOperation.ADC, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8D] = () => { OperationALU8(ALUOperation.ADC, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8E] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.ADC, FetchNextByte()); }); };
            _opcodeMap[0x8F] = () => { OperationALU8(ALUOperation.ADC, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x90] = () => { OperationALU8(ALUOperation.SUB, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x91] = () => { OperationALU8(ALUOperation.SUB, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x92] = () => { OperationALU8(ALUOperation.SUB, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x93] = () => { OperationALU8(ALUOperation.SUB, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x94] = () => { OperationALU8(ALUOperation.SUB, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x95] = () => { OperationALU8(ALUOperation.SUB, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x96] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.SUB, FetchNextByte()); }); };
            _opcodeMap[0x97] = () => { OperationALU8(ALUOperation.SUB, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x98] = () => { OperationALU8(ALUOperation.SBC, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x99] = () => { OperationALU8(ALUOperation.SBC, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9A] = () => { OperationALU8(ALUOperation.SBC, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9B] = () => { OperationALU8(ALUOperation.SBC, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9C] = () => { OperationALU8(ALUOperation.SBC, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9D] = () => { OperationALU8(ALUOperation.SBC, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9E] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.SBC, FetchNextByte()); }); };
            _opcodeMap[0x9F] = () => { OperationALU8(ALUOperation.SBC, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xA0] = () => { OperationALU8(ALUOperation.AND, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA1] = () => { OperationALU8(ALUOperation.AND, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA2] = () => { OperationALU8(ALUOperation.AND, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA3] = () => { OperationALU8(ALUOperation.AND, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA4] = () => { OperationALU8(ALUOperation.AND, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA5] = () => { OperationALU8(ALUOperation.AND, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA6] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.AND, FetchNextByte()); }); };
            _opcodeMap[0xA7] = () => { OperationALU8(ALUOperation.AND, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xA8] = () => { OperationALU8(ALUOperation.XOR, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA9] = () => { OperationALU8(ALUOperation.XOR, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAA] = () => { OperationALU8(ALUOperation.XOR, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAB] = () => { OperationALU8(ALUOperation.XOR, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAC] = () => { OperationALU8(ALUOperation.XOR, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAD] = () => { OperationALU8(ALUOperation.XOR, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAE] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.XOR, FetchNextByte()); }); };
            _opcodeMap[0xAF] = () => { OperationALU8(ALUOperation.XOR, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xB0] = () => { OperationALU8(ALUOperation.OR, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB1] = () => { OperationALU8(ALUOperation.OR, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB2] = () => { OperationALU8(ALUOperation.OR, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB3] = () => { OperationALU8(ALUOperation.OR, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB4] = () => { OperationALU8(ALUOperation.OR, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB5] = () => { OperationALU8(ALUOperation.OR, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB6] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.OR, FetchNextByte()); }); };
            _opcodeMap[0xB7] = () => { OperationALU8(ALUOperation.OR, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xB8] = () => { OperationALU8(ALUOperation.CP, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB9] = () => { OperationALU8(ALUOperation.CP, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBA] = () => { OperationALU8(ALUOperation.CP, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBB] = () => { OperationALU8(ALUOperation.CP, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBC] = () => { OperationALU8(ALUOperation.CP, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBD] = () => { OperationALU8(ALUOperation.CP, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBE] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.CP, FetchNextByte()); }); };
            _opcodeMap[0xBF] = () => { OperationALU8(ALUOperation.CP, A); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region INC/DEC
            _opcodeMap[0x04] = () => { FlagH = (B & 0xF) == 0xF; FlagN = false; FlagZ = B == 0xFF; B++; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x0C] = () => { FlagH = (C & 0xF) == 0xF; FlagN = false; FlagZ = C == 0xFF; C++; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x14] = () => { FlagH = (D & 0xF) == 0xF; FlagN = false; FlagZ = D == 0xFF; D++; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x1C] = () => { FlagH = (E & 0xF) == 0xF; FlagN = false; FlagZ = E == 0xFF; E++; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x24] = () => { FlagH = (H & 0xF) == 0xF; FlagN = false; FlagZ = H == 0xFF; H++; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x2C] = () => { FlagH = (L & 0xF) == 0xF; FlagN = false; FlagZ = L == 0xFF; L++; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x34] = () => { byte tmp = 0; EnqueueInstructionOperations(
                () => { tmp = Memory.Read(HL); FlagH = (tmp & 0xF) == 0xF; FlagN = false; FlagZ = tmp == 0xFF; },
                () => Memory.Write(HL, (byte)(tmp + 1))
            ); };
            _opcodeMap[0x3C] = () => { FlagH = (A & 0xF) == 0xF; FlagN = false; FlagZ = A == 0xFF; A++; _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x04] = () => { FlagH = (B & 0xF) == 0; FlagN = true; FlagZ = B == 0; B--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x0C] = () => { FlagH = (C & 0xF) == 0; FlagN = true; FlagZ = C == 0; C--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x14] = () => { FlagH = (D & 0xF) == 0; FlagN = true; FlagZ = D == 0; D--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x1C] = () => { FlagH = (E & 0xF) == 0; FlagN = true; FlagZ = E == 0; E--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x24] = () => { FlagH = (H & 0xF) == 0; FlagN = true; FlagZ = H == 0; H--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x2C] = () => { FlagH = (L & 0xF) == 0; FlagN = true; FlagZ = L == 0; L--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x34] = () => { byte tmp = 0; EnqueueInstructionOperations(
                () => { tmp = Memory.Read(HL); FlagH = (tmp & 0xF) == 0; FlagN = true; FlagZ = tmp == 0; },
                () => Memory.Write(HL, (byte)(tmp - 1))
            ); };
            _opcodeMap[0x3C] = () => { FlagH = (A & 0xF) == 0; FlagN = true; FlagZ = A == 0; A--; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region Miscellaneous
            _opcodeMap[0x00] = () => { _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xCB] = () => { _actionQueue.Enqueue(FetchInstructionCB); };
            #endregion

            #region CB Instructions

            #region RL
            _opcodeMapCB[0x10] = () => { int c = FlagC ? 1 : 0; FlagC = (B & 0x80) == 0x80; B = (byte)((B << 1) | c); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x11] = () => { int c = FlagC ? 1 : 0; FlagC = (C & 0x80) == 0x80; C = (byte)((C << 1) | c); FlagZ = C == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x12] = () => { int c = FlagC ? 1 : 0; FlagC = (D & 0x80) == 0x80; D = (byte)((D << 1) | c); FlagZ = D == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x13] = () => { int c = FlagC ? 1 : 0; FlagC = (E & 0x80) == 0x80; E = (byte)((E << 1) | c); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x14] = () => { int c = FlagC ? 1 : 0; FlagC = (H & 0x80) == 0x80; H = (byte)((H << 1) | c); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x15] = () => { int c = FlagC ? 1 : 0; FlagC = (L & 0x80) == 0x80; L = (byte)((L << 1) | c); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x16] = () => { byte tmp = 0; EnqueueInstructionOperations(
                () => tmp = Memory.Read(HL),
                () => {
                    int c = FlagC ? 1 : 0;
                    FlagC = (tmp & 0x80) == 0x80;
                    byte res = (byte)((tmp << 1) | c);
                    Memory.Write(HL, tmp);
                    FlagZ = res == 0;
                    FlagN = false;
                    FlagH = false;
                }
            ); };
            _opcodeMapCB[0x17] = () => { int c = FlagC ? 1 : 0; FlagC = (A & 0x80) == 0x80; A = (byte)((A << 1) | c); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region BIT
            _opcodeMapCB[0x40] = () => { OperationBIT(B, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x41] = () => { OperationBIT(C, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x42] = () => { OperationBIT(D, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x43] = () => { OperationBIT(E, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x44] = () => { OperationBIT(H, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x45] = () => { OperationBIT(L, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x46] = () => { EnqueueInstructionOperations(() => { OperationBIT(FetchNextByte(), 1 << 0); }); };
            _opcodeMapCB[0x47] = () => { OperationBIT(A, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x48] = () => { OperationBIT(B, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x49] = () => { OperationBIT(C, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4A] = () => { OperationBIT(D, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4B] = () => { OperationBIT(E, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4C] = () => { OperationBIT(H, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4D] = () => { OperationBIT(L, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4E] = () => { EnqueueInstructionOperations(() => { OperationBIT(FetchNextByte(), 1 << 1); }); };
            _opcodeMapCB[0x4F] = () => { OperationBIT(A, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x50] = () => { OperationBIT(B, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x51] = () => { OperationBIT(C, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x52] = () => { OperationBIT(D, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x53] = () => { OperationBIT(E, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x54] = () => { OperationBIT(H, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x55] = () => { OperationBIT(L, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x56] = () => { EnqueueInstructionOperations(() => { OperationBIT(FetchNextByte(), 1 << 2); }); };
            _opcodeMapCB[0x57] = () => { OperationBIT(A, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x58] = () => { OperationBIT(B, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x59] = () => { OperationBIT(C, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5A] = () => { OperationBIT(D, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5B] = () => { OperationBIT(E, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5C] = () => { OperationBIT(H, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5D] = () => { OperationBIT(L, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5E] = () => { EnqueueInstructionOperations(() => { OperationBIT(FetchNextByte(), 1 << 3); }); };
            _opcodeMapCB[0x5F] = () => { OperationBIT(A, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x60] = () => { OperationBIT(B, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x61] = () => { OperationBIT(C, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x62] = () => { OperationBIT(D, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x63] = () => { OperationBIT(E, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x64] = () => { OperationBIT(H, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x65] = () => { OperationBIT(L, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x66] = () => { EnqueueInstructionOperations(() => { OperationBIT(FetchNextByte(), 1 << 4); }); };
            _opcodeMapCB[0x67] = () => { OperationBIT(A, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x68] = () => { OperationBIT(B, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x69] = () => { OperationBIT(C, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6A] = () => { OperationBIT(D, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6B] = () => { OperationBIT(E, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6C] = () => { OperationBIT(H, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6D] = () => { OperationBIT(L, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6E] = () => { EnqueueInstructionOperations(() => { OperationBIT(FetchNextByte(), 1 << 5); }); };
            _opcodeMapCB[0x6F] = () => { OperationBIT(A, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };


            _opcodeMapCB[0x70] = () => { OperationBIT(B, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x71] = () => { OperationBIT(C, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x72] = () => { OperationBIT(D, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x73] = () => { OperationBIT(E, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x74] = () => { OperationBIT(H, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x75] = () => { OperationBIT(L, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x76] = () => { EnqueueInstructionOperations(() => { OperationBIT(FetchNextByte(), 1 << 6); }); };
            _opcodeMapCB[0x77] = () => { OperationBIT(A, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x78] = () => { OperationBIT(B, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x79] = () => { OperationBIT(C, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7A] = () => { OperationBIT(D, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7B] = () => { OperationBIT(E, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7C] = () => { OperationBIT(H, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7D] = () => { OperationBIT(L, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7E] = () => { EnqueueInstructionOperations(() => { OperationBIT(FetchNextByte(), 1 << 7); }); };
            _opcodeMapCB[0x7F] = () => { OperationBIT(A, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #endregion
        }
    }
}
