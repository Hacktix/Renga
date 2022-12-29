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

        public bool InterruptsEnabled = false;
        public bool Halted = false;
        public byte IE = 0;
        public byte IF = 0;
        private byte _ifMasked { get { return (byte)(IF & IE); } }

        public MemoryBus Memory;
        
        private Queue<Action> _actionQueue = new Queue<Action>();
        private Dictionary<byte, Action> _opcodeMap = new Dictionary<byte, Action>();
        private Dictionary<byte, Action> _opcodeMapCB = new Dictionary<byte, Action>();

        private bool _debugging = false;
        private ushort _breakpoint = 0xFFFF;

        private bool _writeDiffLogs = false;

        public CPU(MemoryBus memory)
        {
            Memory = memory;

            InitializeOpcodeMaps();
            _actionQueue.Enqueue(FetchInstruction);

#if DEBUG
            if(_writeDiffLogs)
            {
                A = 0x01;
                F = 0xB0;
                B = 0x00;
                C = 0x13;
                D = 0x00;
                E = 0xD8;
                H = 0x01;
                L = 0x4D;
                SP = 0xFFFE;
                PC = 0x0100;
                Memory.Write(0xFF50, 1);
            }
#endif
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
            if (Halted)
            {
                if (_ifMasked != 0)
                    Halted = false;
                else
                {
                    _actionQueue.Enqueue(FetchInstruction);
                    return;
                }
            }

            if(InterruptsEnabled && _ifMasked != 0)
            {
                byte ifMasked = _ifMasked;
                if(ifMasked != 0)
                {
                    ushort routine = 0x40;
                    byte ifBit = 1;
                    while ((ifMasked & 1) == 0)
                    {
                        routine += 8;
                        ifMasked >>= 1;
                        ifBit <<= 1;
                    }
                    IF &= (byte)(~ifBit);
                    InterruptsEnabled = false;
                    EnqueueInstructionOperations(
                        () => { },
                        () => { },
                        () => Memory.Write(--SP, (byte)(PC >> 8)),
                        () => Memory.Write(--SP, (byte)(PC & 0xFF)),
                        () => PC = routine
                    );
                    return;
                }
            }

            byte opcode = FetchNextByte();

#if DEBUG
            if (_breakpoint == PC - 1)
                _debugging = true;

            if (_debugging)
            {
                Renga.Log.Debug($"AF: ${AF:X4} BC: ${BC:X4} DE: ${DE:X4} HL: ${HL:X4} PC: ${PC:X4} SP: ${SP:X4} | {Memory.Read(PC):X2} {Memory.Read((ushort)(PC + 1)):X2} {Memory.Read((ushort)(PC + 2)):X2}");
                var key = Console.ReadKey();
                if (key.Key == ConsoleKey.Q)
                    _debugging = false;
            }

            if (_writeDiffLogs)
                File.AppendAllLines("difflog.txt", new string[] { $"A:{A:X2} F:{F:X2} B:{B:X2} C:{C:X2} D:{D:X2} E:{E:X2} H:{H:X2} L:{L:X2} SP:{SP:X4} PC:{PC-1:X4} PCMEM:{Memory.Read((ushort)(PC - 1)):X2},{Memory.Read((ushort)(PC)):X2},{Memory.Read((ushort)(PC + 1)):X2},{Memory.Read((ushort)(PC + 2)):X2}" });
#endif
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
        private byte OperationRES(byte regValue, byte bitmask)
        {
            return (byte)(regValue & (~bitmask));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte OperationSET(byte regValue, byte bitmask)
        {
            return (byte)(regValue | bitmask);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OperationRET(bool condition, bool hasCondition = true, bool enableInterrupts = false)
        {
            if (!condition)
                EnqueueInstructionOperations(() => { });
            else
            {
                ushort retAddr = 0;
                if (!hasCondition)
                    EnqueueInstructionOperations(
                        () => retAddr = Memory.Read(SP++),
                        () => retAddr |= (ushort)(Memory.Read(SP++) << 8),
                        () => PC = retAddr
                    );
                else
                    EnqueueInstructionOperations(
                        () => { },
                        () => retAddr = Memory.Read(SP++),
                        () => retAddr |= (ushort)(Memory.Read(SP++) << 8),
                        () => PC = retAddr
                    );
                if (enableInterrupts)
                    InterruptsEnabled = true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OperationJP(bool condition)
        {
            if (!condition)
                EnqueueInstructionOperations(() => FetchNextByte(), () => FetchNextByte());
            else
            {
                ushort jpAddr = 0;
                EnqueueInstructionOperations(
                    () => jpAddr = FetchNextByte(),
                    () => jpAddr |= (ushort)(FetchNextByte() << 8),
                    () => PC = jpAddr
                );
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void OperationRST(byte vec)
        {
            EnqueueInstructionOperations(
                () => { },
                () => Memory.Write(--SP, (byte)(PC >> 8)),
                () => { Memory.Write(--SP, (byte)(PC & 0xFF)); PC = vec; }
            );
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

            _opcodeMap[0xC9] = () => { OperationRET(true, false); };
            _opcodeMap[0xD9] = () => { OperationRET(true, false, true); };
            _opcodeMap[0xC0] = () => { OperationRET(!FlagZ); };
            _opcodeMap[0xD0] = () => { OperationRET(!FlagC); };
            _opcodeMap[0xC8] = () => { OperationRET(FlagZ); };
            _opcodeMap[0xD8] = () => { OperationRET(FlagC); };

            _opcodeMap[0xC3] = () => { OperationJP(true); };
            _opcodeMap[0xC2] = () => { OperationJP(!FlagZ); };
            _opcodeMap[0xD2] = () => { OperationJP(!FlagC); };
            _opcodeMap[0xCA] = () => { OperationJP(FlagZ); };
            _opcodeMap[0xDA] = () => { OperationJP(FlagC); };
            _opcodeMap[0xE9] = () => { PC = HL; _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xC7] = () => { OperationRST(0x00); };
            _opcodeMap[0xCF] = () => { OperationRST(0x08); };
            _opcodeMap[0xD7] = () => { OperationRST(0x10); };
            _opcodeMap[0xDF] = () => { OperationRST(0x18); };
            _opcodeMap[0xE7] = () => { OperationRST(0x20); };
            _opcodeMap[0xEF] = () => { OperationRST(0x28); };
            _opcodeMap[0xF7] = () => { OperationRST(0x30); };
            _opcodeMap[0xFF] = () => { OperationRST(0x38); };
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
            _opcodeMap[0x57] = () => { D = A; _actionQueue.Enqueue(FetchInstruction); };

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

            _opcodeMap[0xEA] = () => { ushort r = 0; EnqueueInstructionOperations(
                () => r = FetchNextByte(),
                () => r |= (ushort)(FetchNextByte() << 8),
                () => Memory.Write(r, A)
            ); };
            _opcodeMap[0xFA] = () => { ushort r = 0; EnqueueInstructionOperations(
                () => r = FetchNextByte(),
                () => r |= (ushort)(FetchNextByte() << 8),
                () => A = Memory.Read(r)
            ); };

            _opcodeMap[0x08] = () => { ushort r = 0; EnqueueInstructionOperations(
                () => r = FetchNextByte(),
                () => r |= (ushort)(FetchNextByte() << 8),
                () => Memory.Write(r, (byte)(SP & 0xFF)),
                () => Memory.Write((ushort)(r+1), (byte)(SP >> 8))
            ); };
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
                    () => SP = FetchNextByte(),
                    () => SP |= (ushort)(FetchNextByte() << 8)
                );
            };
            #endregion

            #region Stack Ops
            _opcodeMap[0xC5] = () => EnqueueInstructionOperations(() => { }, () => Memory.Write(--SP, B), () => Memory.Write(--SP, C));
            _opcodeMap[0xD5] = () => EnqueueInstructionOperations(() => { }, () => Memory.Write(--SP, D), () => Memory.Write(--SP, E));
            _opcodeMap[0xE5] = () => EnqueueInstructionOperations(() => { }, () => Memory.Write(--SP, H), () => Memory.Write(--SP, L));
            _opcodeMap[0xF5] = () => EnqueueInstructionOperations(() => { }, () => Memory.Write(--SP, A), () => Memory.Write(--SP, F));

            _opcodeMap[0xC1] = () => EnqueueInstructionOperations(() => C = Memory.Read(SP++), () => B = Memory.Read(SP++));
            _opcodeMap[0xD1] = () => EnqueueInstructionOperations(() => E = Memory.Read(SP++), () => D = Memory.Read(SP++));
            _opcodeMap[0xE1] = () => EnqueueInstructionOperations(() => L = Memory.Read(SP++), () => H = Memory.Read(SP++));
            _opcodeMap[0xF1] = () => EnqueueInstructionOperations(() => F = Memory.Read(SP++), () => A = Memory.Read(SP++));
            #endregion

            #region 8-bit ALU
            _opcodeMap[0x80] = () => { OperationALU8(ALUOperation.ADD, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x81] = () => { OperationALU8(ALUOperation.ADD, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x82] = () => { OperationALU8(ALUOperation.ADD, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x83] = () => { OperationALU8(ALUOperation.ADD, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x84] = () => { OperationALU8(ALUOperation.ADD, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x85] = () => { OperationALU8(ALUOperation.ADD, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x86] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.ADD, Memory.Read(HL)); }); };
            _opcodeMap[0x87] = () => { OperationALU8(ALUOperation.ADD, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x88] = () => { OperationALU8(ALUOperation.ADC, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x89] = () => { OperationALU8(ALUOperation.ADC, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8A] = () => { OperationALU8(ALUOperation.ADC, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8B] = () => { OperationALU8(ALUOperation.ADC, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8C] = () => { OperationALU8(ALUOperation.ADC, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8D] = () => { OperationALU8(ALUOperation.ADC, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x8E] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.ADC, Memory.Read(HL)); }); };
            _opcodeMap[0x8F] = () => { OperationALU8(ALUOperation.ADC, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x90] = () => { OperationALU8(ALUOperation.SUB, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x91] = () => { OperationALU8(ALUOperation.SUB, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x92] = () => { OperationALU8(ALUOperation.SUB, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x93] = () => { OperationALU8(ALUOperation.SUB, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x94] = () => { OperationALU8(ALUOperation.SUB, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x95] = () => { OperationALU8(ALUOperation.SUB, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x96] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.SUB, Memory.Read(HL)); }); };
            _opcodeMap[0x97] = () => { OperationALU8(ALUOperation.SUB, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x98] = () => { OperationALU8(ALUOperation.SBC, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x99] = () => { OperationALU8(ALUOperation.SBC, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9A] = () => { OperationALU8(ALUOperation.SBC, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9B] = () => { OperationALU8(ALUOperation.SBC, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9C] = () => { OperationALU8(ALUOperation.SBC, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9D] = () => { OperationALU8(ALUOperation.SBC, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x9E] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.SBC, Memory.Read(HL)); }); };
            _opcodeMap[0x9F] = () => { OperationALU8(ALUOperation.SBC, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xA0] = () => { OperationALU8(ALUOperation.AND, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA1] = () => { OperationALU8(ALUOperation.AND, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA2] = () => { OperationALU8(ALUOperation.AND, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA3] = () => { OperationALU8(ALUOperation.AND, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA4] = () => { OperationALU8(ALUOperation.AND, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA5] = () => { OperationALU8(ALUOperation.AND, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA6] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.AND, Memory.Read(HL)); }); };
            _opcodeMap[0xA7] = () => { OperationALU8(ALUOperation.AND, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xA8] = () => { OperationALU8(ALUOperation.XOR, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xA9] = () => { OperationALU8(ALUOperation.XOR, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAA] = () => { OperationALU8(ALUOperation.XOR, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAB] = () => { OperationALU8(ALUOperation.XOR, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAC] = () => { OperationALU8(ALUOperation.XOR, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAD] = () => { OperationALU8(ALUOperation.XOR, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xAE] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.XOR, Memory.Read(HL)); }); };
            _opcodeMap[0xAF] = () => { OperationALU8(ALUOperation.XOR, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xB0] = () => { OperationALU8(ALUOperation.OR, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB1] = () => { OperationALU8(ALUOperation.OR, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB2] = () => { OperationALU8(ALUOperation.OR, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB3] = () => { OperationALU8(ALUOperation.OR, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB4] = () => { OperationALU8(ALUOperation.OR, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB5] = () => { OperationALU8(ALUOperation.OR, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB6] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.OR, Memory.Read(HL)); }); };
            _opcodeMap[0xB7] = () => { OperationALU8(ALUOperation.OR, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xB8] = () => { OperationALU8(ALUOperation.CP, B); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xB9] = () => { OperationALU8(ALUOperation.CP, C); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBA] = () => { OperationALU8(ALUOperation.CP, D); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBB] = () => { OperationALU8(ALUOperation.CP, E); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBC] = () => { OperationALU8(ALUOperation.CP, H); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBD] = () => { OperationALU8(ALUOperation.CP, L); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xBE] = () => { EnqueueInstructionOperations(() => { OperationALU8(ALUOperation.CP, Memory.Read(HL)); }); };
            _opcodeMap[0xBF] = () => { OperationALU8(ALUOperation.CP, A); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xC6] = () => EnqueueInstructionOperations(() => OperationALU8(ALUOperation.ADD, FetchNextByte()));
            _opcodeMap[0xD6] = () => EnqueueInstructionOperations(() => OperationALU8(ALUOperation.SUB, FetchNextByte()));
            _opcodeMap[0xE6] = () => EnqueueInstructionOperations(() => OperationALU8(ALUOperation.AND, FetchNextByte()));
            _opcodeMap[0xF6] = () => EnqueueInstructionOperations(() => OperationALU8(ALUOperation.OR, FetchNextByte()));
            _opcodeMap[0xCE] = () => EnqueueInstructionOperations(() => OperationALU8(ALUOperation.ADC, FetchNextByte()));
            _opcodeMap[0xDE] = () => EnqueueInstructionOperations(() => OperationALU8(ALUOperation.SBC, FetchNextByte()));
            _opcodeMap[0xEE] = () => EnqueueInstructionOperations(() => OperationALU8(ALUOperation.XOR, FetchNextByte()));
            _opcodeMap[0xFE] = () => EnqueueInstructionOperations(() => OperationALU8(ALUOperation.CP, FetchNextByte()));
            #endregion

            #region 16-bit ALU
            _opcodeMap[0x09] = () => EnqueueInstructionOperations(
                () => { FlagN = false; FlagC = (HL + BC) > 0xFFFF; FlagH = ((HL & 0xFFF) + (BC & 0xFFF)) > 0xFFF; HL += BC; }
            );
            _opcodeMap[0x19] = () => EnqueueInstructionOperations(
                () => { FlagN = false; FlagC = (HL + DE) > 0xFFFF; FlagH = ((HL & 0xFFF) + (DE & 0xFFF)) > 0xFFF; HL += DE; }
            );
            _opcodeMap[0x29] = () => EnqueueInstructionOperations(
                () => { FlagN = false; FlagC = (HL + HL) > 0xFFFF; FlagH = ((HL & 0xFFF) + (HL & 0xFFF)) > 0xFFF; HL += HL; }
            );
            _opcodeMap[0x39] = () => EnqueueInstructionOperations(
                () => { FlagN = false; FlagC = (HL + SP) > 0xFFFF; FlagH = ((HL & 0xFFF) + (SP & 0xFFF)) > 0xFFF; HL += SP; }
            );

            _opcodeMap[0xE8] = () => { sbyte e = 0; EnqueueInstructionOperations(
                () => e = (sbyte)FetchNextByte(),
                () => { FlagZ = false; FlagN = false; FlagH = (e & 0xF) + (SP & 0xF) > 0xF; FlagC = (e & 0xFF) + (SP & 0xFF) > 0xFF; },
                () => SP = (ushort)(SP + e)
            ); };
            _opcodeMap[0xF8] = () => { sbyte e = 0; EnqueueInstructionOperations(
                () => e = (sbyte)FetchNextByte(),
                () => { FlagZ = false; FlagN = false; FlagH = (e & 0xF) + (SP & 0xF) > 0xF; FlagC = (e & 0xFF) + (SP & 0xFF) > 0xFF; HL = (ushort)(SP + e); }
            ); };
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

            _opcodeMap[0x05] = () => { FlagH = (B & 0xF) == 0; FlagN = true; FlagZ = B == 1; B--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x0D] = () => { FlagH = (C & 0xF) == 0; FlagN = true; FlagZ = C == 1; C--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x15] = () => { FlagH = (D & 0xF) == 0; FlagN = true; FlagZ = D == 1; D--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x1D] = () => { FlagH = (E & 0xF) == 0; FlagN = true; FlagZ = E == 1; E--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x25] = () => { FlagH = (H & 0xF) == 0; FlagN = true; FlagZ = H == 1; H--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x2D] = () => { FlagH = (L & 0xF) == 0; FlagN = true; FlagZ = L == 1; L--; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x35] = () => { byte tmp = 0; EnqueueInstructionOperations(
                () => { tmp = Memory.Read(HL); FlagH = (tmp & 0xF) == 0; FlagN = true; FlagZ = tmp == 1; },
                () => Memory.Write(HL, (byte)(tmp - 1))
            ); };
            _opcodeMap[0x3D] = () => { FlagH = (A & 0xF) == 0; FlagN = true; FlagZ = A == 1; A--; _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x03] = () => { _actionQueue.Enqueue(() => BC++); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x13] = () => { _actionQueue.Enqueue(() => DE++); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x23] = () => { _actionQueue.Enqueue(() => HL++); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x33] = () => { _actionQueue.Enqueue(() => SP++); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x0B] = () => { _actionQueue.Enqueue(() => BC--); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x1B] = () => { _actionQueue.Enqueue(() => DE--); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x2B] = () => { _actionQueue.Enqueue(() => HL--); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x3B] = () => { _actionQueue.Enqueue(() => SP--); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region Miscellaneous
            _opcodeMap[0x00] = () => { _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xCB] = () => { _actionQueue.Enqueue(FetchInstructionCB); };

            _opcodeMap[0xF3] = () => { InterruptsEnabled = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0xFB] = () => _actionQueue.Enqueue(() => { FetchInstruction(); InterruptsEnabled = true; });
            _opcodeMap[0x76] = () => { Halted = true; _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x07] = () => { FlagZ = false; FlagN = false; FlagH = false; int r = A >> 7; FlagC = (A & 0x80) == 0x80; A = (byte)((A << 1) | r); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x0F] = () => { FlagZ = false; FlagN = false; FlagH = false; int r = (A << 7) & 0xFF; FlagC = (A & 1) == 1; A = (byte)((A >> 1) | r); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x17] = () => { FlagZ = false; FlagN = false; FlagH = false; int c = FlagC ? 1 : 0; FlagC = (A & 0x80) == 0x80; A = (byte)((A << 1) | c); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x1F] = () => { FlagZ = false; FlagN = false; FlagH = false; int c = FlagC ? 0x80 : 0; FlagC = (A & 1) == 1; A = (byte)((A >> 1) | c); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x2F] = () => { A = (byte)~A; FlagN = true; FlagH = true; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x37] = () => { FlagN = false; FlagH = false; FlagC = true; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMap[0x3F] = () => { FlagN = false; FlagH = false; FlagC = !FlagC; _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0xF9] = () => { _actionQueue.Enqueue(() => SP = HL); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMap[0x27] = () =>
            {
                byte corr = 0;
                bool c = false;
                if (FlagH || (!FlagN && (A & 0xF) > 9))
                    corr |= 0x6;
                if(FlagC || (!FlagN && A > 0x99))
                {
                    corr |= 0x60;
                    c = true;
                }

                if (FlagN) A -= corr;
                else A += corr;

                FlagC = c;
                FlagH = false;
                FlagZ = A == 0;
                _actionQueue.Enqueue(FetchInstruction);
            }; // DAA
            #endregion

            #region CB Instructions

            #region RLC
            _opcodeMapCB[0x00] = () => { FlagC = (B & 0x80) == 0x80; byte r = (byte)(B >> 7); B = (byte)((B << 1) | r); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x01] = () => { FlagC = (C & 0x80) == 0x80; byte r = (byte)(C >> 7); C = (byte)((C << 1) | r); FlagZ = C == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x02] = () => { FlagC = (D & 0x80) == 0x80; byte r = (byte)(D >> 7); D = (byte)((D << 1) | r); FlagZ = D == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x03] = () => { FlagC = (E & 0x80) == 0x80; byte r = (byte)(E >> 7); E = (byte)((E << 1) | r); FlagZ = E == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x04] = () => { FlagC = (H & 0x80) == 0x80; byte r = (byte)(H >> 7); H = (byte)((H << 1) | r); FlagZ = H == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x05] = () => { FlagC = (L & 0x80) == 0x80; byte r = (byte)(L >> 7); L = (byte)((L << 1) | r); FlagZ = L == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x06] = () => { byte tmp = 0; EnqueueInstructionOperations(
                () => tmp = Memory.Read(HL),
                () => {
                    FlagC = (tmp & 0x80) == 0x80;
                    byte r = (byte)(tmp >> 7);
                    tmp = (byte)((tmp << 1) | r);
                    FlagZ = tmp == 0;
                    FlagN = false;
                    FlagH = false;
                    Memory.Write(HL, tmp);
                }
            ); };
            _opcodeMapCB[0x07] = () => { FlagC = (A & 0x80) == 0x80; byte r = (byte)(A >> 7); A = (byte)((A << 1) | r); FlagZ = A == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region RRC
            _opcodeMapCB[0x08] = () => { FlagC = (B & 1) == 1; byte r = (byte)((B & 1) << 7); B = (byte)((B >> 1) | r); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x09] = () => { FlagC = (C & 1) == 1; byte r = (byte)((C & 1) << 7); C = (byte)((C >> 1) | r); FlagZ = C == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x0A] = () => { FlagC = (D & 1) == 1; byte r = (byte)((D & 1) << 7); D = (byte)((D >> 1) | r); FlagZ = D == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x0B] = () => { FlagC = (E & 1) == 1; byte r = (byte)((E & 1) << 7); E = (byte)((E >> 1) | r); FlagZ = E == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x0C] = () => { FlagC = (H & 1) == 1; byte r = (byte)((H & 1) << 7); H = (byte)((H >> 1) | r); FlagZ = H == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x0D] = () => { FlagC = (L & 1) == 1; byte r = (byte)((L & 1) << 7); L = (byte)((L >> 1) | r); FlagZ = L == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x0E] = () => {
                byte tmp = 0; EnqueueInstructionOperations(
                () => tmp = Memory.Read(HL),
                () => {
                    FlagC = (tmp & 1) == 1;
                    byte r = (byte)((tmp & 1) << 7);
                    tmp = (byte)((tmp >> 1) | r);
                    FlagZ = tmp == 0;
                    FlagN = false;
                    FlagH = false;
                    Memory.Write(HL, tmp);
                }
            );
            };
            _opcodeMapCB[0x0F] = () => { FlagC = (A & 1) == 1; byte r = (byte)((A & 1) << 7); A = (byte)((A >> 1) | r); FlagZ = A == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region RL
            _opcodeMapCB[0x10] = () => { int c = FlagC ? 1 : 0; FlagC = (B & 0x80) == 0x80; B = (byte)((B << 1) | c); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x11] = () => { int c = FlagC ? 1 : 0; FlagC = (C & 0x80) == 0x80; C = (byte)((C << 1) | c); FlagZ = C == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x12] = () => { int c = FlagC ? 1 : 0; FlagC = (D & 0x80) == 0x80; D = (byte)((D << 1) | c); FlagZ = D == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x13] = () => { int c = FlagC ? 1 : 0; FlagC = (E & 0x80) == 0x80; E = (byte)((E << 1) | c); FlagZ = E == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x14] = () => { int c = FlagC ? 1 : 0; FlagC = (H & 0x80) == 0x80; H = (byte)((H << 1) | c); FlagZ = H == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x15] = () => { int c = FlagC ? 1 : 0; FlagC = (L & 0x80) == 0x80; L = (byte)((L << 1) | c); FlagZ = L == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x16] = () => { byte tmp = 0; EnqueueInstructionOperations(
                () => tmp = Memory.Read(HL),
                () => {
                    int c = FlagC ? 1 : 0;
                    FlagC = (tmp & 0x80) == 0x80;
                    tmp = (byte)((tmp << 1) | c);
                    Memory.Write(HL, tmp);
                    FlagZ = tmp == 0;
                    FlagN = false;
                    FlagH = false;
                }
            ); };
            _opcodeMapCB[0x17] = () => { int c = FlagC ? 1 : 0; FlagC = (A & 0x80) == 0x80; A = (byte)((A << 1) | c); FlagZ = A == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region RR
            _opcodeMapCB[0x18] = () => { int c = FlagC ? 0x80 : 0; FlagC = (B & 1) == 1; B = (byte)((B >> 1) | c); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x19] = () => { int c = FlagC ? 0x80 : 0; FlagC = (C & 1) == 1; C = (byte)((C >> 1) | c); FlagZ = C == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x1A] = () => { int c = FlagC ? 0x80 : 0; FlagC = (D & 1) == 1; D = (byte)((D >> 1) | c); FlagZ = D == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x1B] = () => { int c = FlagC ? 0x80 : 0; FlagC = (E & 1) == 1; E = (byte)((E >> 1) | c); FlagZ = E == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x1C] = () => { int c = FlagC ? 0x80 : 0; FlagC = (H & 1) == 1; H = (byte)((H >> 1) | c); FlagZ = H == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x1D] = () => { int c = FlagC ? 0x80 : 0; FlagC = (L & 1) == 1; L = (byte)((L >> 1) | c); FlagZ = L == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x1E] = () => { byte tmp = 0; EnqueueInstructionOperations(
                () => tmp = Memory.Read(HL),
                () => {
                    int c = FlagC ? 0x80 : 0;
                    FlagC = (tmp & 1) == 1;
                    tmp = (byte)((tmp >> 1) | c);
                    Memory.Write(HL, tmp);
                    FlagZ = tmp == 0;
                    FlagN = false;
                    FlagH = false;
                }
            ); };
            _opcodeMapCB[0x1F] = () => { int c = FlagC ? 0x80 : 0; FlagC = (A & 1) == 1; A = (byte)((A >> 1) | c); FlagZ = A == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region SLA
            _opcodeMapCB[0x20] = () => { FlagC = (B & 0x80) == 0x80; B <<= 1; FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x21] = () => { FlagC = (C & 0x80) == 0x80; C <<= 1; FlagZ = C == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x22] = () => { FlagC = (D & 0x80) == 0x80; D <<= 1; FlagZ = D == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x23] = () => { FlagC = (E & 0x80) == 0x80; E <<= 1; FlagZ = E == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x24] = () => { FlagC = (H & 0x80) == 0x80; H <<= 1; FlagZ = H == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x25] = () => { FlagC = (L & 0x80) == 0x80; L <<= 1; FlagZ = L == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x26] = () => {
                byte tmp = 0; EnqueueInstructionOperations(
                () => tmp = Memory.Read(HL),
                () => {
                    FlagC = (tmp & 0x80) == 0x80;
                    tmp <<= 1;
                    FlagZ = tmp == 0;
                    FlagN = false;
                    FlagH = false;
                    Memory.Write(HL, tmp);
                }
            );
            };
            _opcodeMapCB[0x27] = () => { FlagC = (A & 0x80) == 0x80; A <<= 1; FlagZ = A == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region SRA
            _opcodeMapCB[0x28] = () => { FlagC = (B & 1) == 1; B = (byte)(((sbyte)B) >> 1); FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x29] = () => { FlagC = (C & 1) == 1; C = (byte)(((sbyte)C) >> 1); FlagZ = C == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x2A] = () => { FlagC = (D & 1) == 1; D = (byte)(((sbyte)D) >> 1); FlagZ = D == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x2B] = () => { FlagC = (E & 1) == 1; E = (byte)(((sbyte)E) >> 1); FlagZ = E == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x2C] = () => { FlagC = (H & 1) == 1; H = (byte)(((sbyte)H) >> 1); FlagZ = H == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x2D] = () => { FlagC = (L & 1) == 1; L = (byte)(((sbyte)L) >> 1); FlagZ = L == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x2E] = () => {
                byte tmp = 0; EnqueueInstructionOperations(
                () => tmp = Memory.Read(HL),
                () => {
                    FlagC = (tmp & 1) == 1;
                    tmp = (byte)(((sbyte)tmp) >> 1);
                    FlagZ = tmp == 0;
                    FlagN = false;
                    FlagH = false;
                    Memory.Write(HL, tmp);
                }
            );
            };
            _opcodeMapCB[0x2F] = () => { FlagC = (A & 1) == 1; A = (byte)(((sbyte)A) >> 1); FlagZ = A == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region SWAP
            _opcodeMapCB[0x30] = () => { B = (byte)((B << 4) | (B >> 4)); FlagZ = B == 0; FlagN = false; FlagH = false; FlagC = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x31] = () => { C = (byte)((C << 4) | (C >> 4)); FlagZ = C == 0; FlagN = false; FlagH = false; FlagC = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x32] = () => { D = (byte)((D << 4) | (D >> 4)); FlagZ = D == 0; FlagN = false; FlagH = false; FlagC = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x33] = () => { E = (byte)((E << 4) | (E >> 4)); FlagZ = E == 0; FlagN = false; FlagH = false; FlagC = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x34] = () => { H = (byte)((H << 4) | (H >> 4)); FlagZ = H == 0; FlagN = false; FlagH = false; FlagC = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x35] = () => { L = (byte)((L << 4) | (L >> 4)); FlagZ = L == 0; FlagN = false; FlagH = false; FlagC = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x36] = () => {
                byte tmp = 0;
                EnqueueInstructionOperations(
                    () => tmp = Memory.Read(HL),
                    () => {
                        tmp = (byte)((tmp << 4) | (tmp >> 4));
                        Memory.Write(HL, tmp);
                        FlagZ = tmp == 0;
                        FlagN = false;
                        FlagH = false;
                        FlagC = false;
                    }
                );
            };
            _opcodeMapCB[0x37] = () => { A = (byte)((A << 4) | (A >> 4)); FlagZ = A == 0; FlagN = false; FlagH = false; FlagC = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region SRL
            _opcodeMapCB[0x38] = () => { FlagC = (B & 1) != 0; B >>= 1; FlagZ = B == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x39] = () => { FlagC = (C & 1) != 0; C >>= 1; FlagZ = C == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x3A] = () => { FlagC = (D & 1) != 0; D >>= 1; FlagZ = D == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x3B] = () => { FlagC = (E & 1) != 0; E >>= 1; FlagZ = E == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x3C] = () => { FlagC = (H & 1) != 0; H >>= 1; FlagZ = H == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x3D] = () => { FlagC = (L & 1) != 0; L >>= 1; FlagZ = L == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x3E] = () => { byte tmp = 0; EnqueueInstructionOperations(
                () => tmp = Memory.Read(HL),
                () => {
                    FlagC = (tmp & 1) != 0;
                    tmp >>= 1;
                    FlagZ = tmp == 0;
                    FlagN = false;
                    FlagH = false;
                    Memory.Write(HL, tmp);
                }
            ); };
            _opcodeMapCB[0x3F] = () => { FlagC = (A & 1) != 0; A >>= 1; FlagZ = A == 0; FlagN = false; FlagH = false; _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region BIT
            _opcodeMapCB[0x40] = () => { OperationBIT(B, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x41] = () => { OperationBIT(C, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x42] = () => { OperationBIT(D, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x43] = () => { OperationBIT(E, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x44] = () => { OperationBIT(H, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x45] = () => { OperationBIT(L, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x46] = () => { EnqueueInstructionOperations(() => { OperationBIT(Memory.Read(HL), 1 << 0); }); };
            _opcodeMapCB[0x47] = () => { OperationBIT(A, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x48] = () => { OperationBIT(B, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x49] = () => { OperationBIT(C, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4A] = () => { OperationBIT(D, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4B] = () => { OperationBIT(E, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4C] = () => { OperationBIT(H, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4D] = () => { OperationBIT(L, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x4E] = () => { EnqueueInstructionOperations(() => { OperationBIT(Memory.Read(HL), 1 << 1); }); };
            _opcodeMapCB[0x4F] = () => { OperationBIT(A, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x50] = () => { OperationBIT(B, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x51] = () => { OperationBIT(C, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x52] = () => { OperationBIT(D, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x53] = () => { OperationBIT(E, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x54] = () => { OperationBIT(H, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x55] = () => { OperationBIT(L, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x56] = () => { EnqueueInstructionOperations(() => { OperationBIT(Memory.Read(HL), 1 << 2); }); };
            _opcodeMapCB[0x57] = () => { OperationBIT(A, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x58] = () => { OperationBIT(B, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x59] = () => { OperationBIT(C, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5A] = () => { OperationBIT(D, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5B] = () => { OperationBIT(E, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5C] = () => { OperationBIT(H, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5D] = () => { OperationBIT(L, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x5E] = () => { EnqueueInstructionOperations(() => { OperationBIT(Memory.Read(HL), 1 << 3); }); };
            _opcodeMapCB[0x5F] = () => { OperationBIT(A, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x60] = () => { OperationBIT(B, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x61] = () => { OperationBIT(C, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x62] = () => { OperationBIT(D, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x63] = () => { OperationBIT(E, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x64] = () => { OperationBIT(H, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x65] = () => { OperationBIT(L, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x66] = () => { EnqueueInstructionOperations(() => { OperationBIT(Memory.Read(HL), 1 << 4); }); };
            _opcodeMapCB[0x67] = () => { OperationBIT(A, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x68] = () => { OperationBIT(B, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x69] = () => { OperationBIT(C, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6A] = () => { OperationBIT(D, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6B] = () => { OperationBIT(E, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6C] = () => { OperationBIT(H, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6D] = () => { OperationBIT(L, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x6E] = () => { EnqueueInstructionOperations(() => { OperationBIT(Memory.Read(HL), 1 << 5); }); };
            _opcodeMapCB[0x6F] = () => { OperationBIT(A, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };


            _opcodeMapCB[0x70] = () => { OperationBIT(B, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x71] = () => { OperationBIT(C, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x72] = () => { OperationBIT(D, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x73] = () => { OperationBIT(E, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x74] = () => { OperationBIT(H, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x75] = () => { OperationBIT(L, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x76] = () => { EnqueueInstructionOperations(() => { OperationBIT(Memory.Read(HL), 1 << 6); }); };
            _opcodeMapCB[0x77] = () => { OperationBIT(A, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x78] = () => { OperationBIT(B, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x79] = () => { OperationBIT(C, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7A] = () => { OperationBIT(D, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7B] = () => { OperationBIT(E, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7C] = () => { OperationBIT(H, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7D] = () => { OperationBIT(L, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x7E] = () => { EnqueueInstructionOperations(() => { OperationBIT(Memory.Read(HL), 1 << 7); }); };
            _opcodeMapCB[0x7F] = () => { OperationBIT(A, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region RES
            _opcodeMapCB[0x80] = () => { B = OperationRES(B, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x81] = () => { C = OperationRES(C, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x82] = () => { D = OperationRES(D, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x83] = () => { E = OperationRES(E, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x84] = () => { H = OperationRES(H, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x85] = () => { L = OperationRES(L, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x86] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationRES(tmp, 1 << 0))); };
            _opcodeMapCB[0x87] = () => { A = OperationRES(A, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x88] = () => { B = OperationRES(B, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x89] = () => { C = OperationRES(C, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x8A] = () => { D = OperationRES(D, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x8B] = () => { E = OperationRES(E, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x8C] = () => { H = OperationRES(H, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x8D] = () => { L = OperationRES(L, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x8E] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationRES(tmp, 1 << 1))); };
            _opcodeMapCB[0x8F] = () => { A = OperationRES(A, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x90] = () => { B = OperationRES(B, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x91] = () => { C = OperationRES(C, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x92] = () => { D = OperationRES(D, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x93] = () => { E = OperationRES(E, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x94] = () => { H = OperationRES(H, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x95] = () => { L = OperationRES(L, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x96] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationRES(tmp, 1 << 2))); };
            _opcodeMapCB[0x97] = () => { A = OperationRES(A, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0x98] = () => { B = OperationRES(B, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x99] = () => { C = OperationRES(C, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x9A] = () => { D = OperationRES(D, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x9B] = () => { E = OperationRES(E, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x9C] = () => { H = OperationRES(H, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x9D] = () => { L = OperationRES(L, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0x9E] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationRES(tmp, 1 << 3))); };
            _opcodeMapCB[0x9F] = () => { A = OperationRES(A, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xA0] = () => { B = OperationRES(B, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xA1] = () => { C = OperationRES(C, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xA2] = () => { D = OperationRES(D, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xA3] = () => { E = OperationRES(E, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xA4] = () => { H = OperationRES(H, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xA5] = () => { L = OperationRES(L, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xA6] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationRES(tmp, 1 << 4))); };
            _opcodeMapCB[0xA7] = () => { A = OperationRES(A, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xA8] = () => { B = OperationRES(B, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xA9] = () => { C = OperationRES(C, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xAA] = () => { D = OperationRES(D, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xAB] = () => { E = OperationRES(E, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xAC] = () => { H = OperationRES(H, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xAD] = () => { L = OperationRES(L, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xAE] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationRES(tmp, 1 << 5))); };
            _opcodeMapCB[0xAF] = () => { A = OperationRES(A, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xB0] = () => { B = OperationRES(B, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xB1] = () => { C = OperationRES(C, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xB2] = () => { D = OperationRES(D, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xB3] = () => { E = OperationRES(E, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xB4] = () => { H = OperationRES(H, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xB5] = () => { L = OperationRES(L, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xB6] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationRES(tmp, 1 << 6))); };
            _opcodeMapCB[0xB7] = () => { A = OperationRES(A, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xB8] = () => { B = OperationRES(B, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xB9] = () => { C = OperationRES(C, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xBA] = () => { D = OperationRES(D, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xBB] = () => { E = OperationRES(E, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xBC] = () => { H = OperationRES(H, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xBD] = () => { L = OperationRES(L, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xBE] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationRES(tmp, 1 << 7))); };
            _opcodeMapCB[0xBF] = () => { A = OperationRES(A, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #region SET
            _opcodeMapCB[0xC0] = () => { B = OperationSET(B, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xC1] = () => { C = OperationSET(C, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xC2] = () => { D = OperationSET(D, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xC3] = () => { E = OperationSET(E, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xC4] = () => { H = OperationSET(H, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xC5] = () => { L = OperationSET(L, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xC6] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationSET(tmp, 1 << 0))); };
            _opcodeMapCB[0xC7] = () => { A = OperationSET(A, 1 << 0); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xC8] = () => { B = OperationSET(B, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xC9] = () => { C = OperationSET(C, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xCA] = () => { D = OperationSET(D, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xCB] = () => { E = OperationSET(E, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xCC] = () => { H = OperationSET(H, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xCD] = () => { L = OperationSET(L, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xCE] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationSET(tmp, 1 << 1))); };
            _opcodeMapCB[0xCF] = () => { A = OperationSET(A, 1 << 1); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xD0] = () => { B = OperationSET(B, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xD1] = () => { C = OperationSET(C, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xD2] = () => { D = OperationSET(D, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xD3] = () => { E = OperationSET(E, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xD4] = () => { H = OperationSET(H, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xD5] = () => { L = OperationSET(L, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xD6] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationSET(tmp, 1 << 2))); };
            _opcodeMapCB[0xD7] = () => { A = OperationSET(A, 1 << 2); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xD8] = () => { B = OperationSET(B, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xD9] = () => { C = OperationSET(C, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xDA] = () => { D = OperationSET(D, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xDB] = () => { E = OperationSET(E, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xDC] = () => { H = OperationSET(H, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xDD] = () => { L = OperationSET(L, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xDE] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationSET(tmp, 1 << 3))); };
            _opcodeMapCB[0xDF] = () => { A = OperationSET(A, 1 << 3); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xE0] = () => { B = OperationSET(B, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xE1] = () => { C = OperationSET(C, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xE2] = () => { D = OperationSET(D, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xE3] = () => { E = OperationSET(E, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xE4] = () => { H = OperationSET(H, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xE5] = () => { L = OperationSET(L, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xE6] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationSET(tmp, 1 << 4))); };
            _opcodeMapCB[0xE7] = () => { A = OperationSET(A, 1 << 4); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xE8] = () => { B = OperationSET(B, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xE9] = () => { C = OperationSET(C, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xEA] = () => { D = OperationSET(D, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xEB] = () => { E = OperationSET(E, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xEC] = () => { H = OperationSET(H, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xED] = () => { L = OperationSET(L, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xEE] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationSET(tmp, 1 << 5))); };
            _opcodeMapCB[0xEF] = () => { A = OperationSET(A, 1 << 5); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xF0] = () => { B = OperationSET(B, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xF1] = () => { C = OperationSET(C, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xF2] = () => { D = OperationSET(D, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xF3] = () => { E = OperationSET(E, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xF4] = () => { H = OperationSET(H, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xF5] = () => { L = OperationSET(L, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xF6] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationSET(tmp, 1 << 6))); };
            _opcodeMapCB[0xF7] = () => { A = OperationSET(A, 1 << 6); _actionQueue.Enqueue(FetchInstruction); };

            _opcodeMapCB[0xF8] = () => { B = OperationSET(B, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xF9] = () => { C = OperationSET(C, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xFA] = () => { D = OperationSET(D, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xFB] = () => { E = OperationSET(E, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xFC] = () => { H = OperationSET(H, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xFD] = () => { L = OperationSET(L, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            _opcodeMapCB[0xFE] = () => { byte tmp = 0; EnqueueInstructionOperations(() => tmp = Memory.Read(HL), () => Memory.Write(HL, OperationSET(tmp, 1 << 7))); };
            _opcodeMapCB[0xFF] = () => { A = OperationSET(A, 1 << 7); _actionQueue.Enqueue(FetchInstruction); };
            #endregion

            #endregion
        }
    }
}
