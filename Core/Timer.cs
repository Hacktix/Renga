using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Renga.Core
{
    internal class Timer
    {
        public ushort DIV = 0;
        public byte TIMA = 0;
        public byte TMA = 0;
        public byte TAC = 0;

        private bool _lastEdgeCheck = false;
        private bool _timaReloadScheduled = false;
        private bool _timaReloadedThisCycle = false;

        private Emulator _emu;

        public Timer(Emulator emu)
        {
            _emu = emu;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tick()
        {
            if (_timaReloadedThisCycle)
                _timaReloadedThisCycle = false;

            if(_timaReloadScheduled) {
                TIMA = TMA;
                _emu.CPU.IF |= 0b100;
                _timaReloadScheduled = false;
                _timaReloadedThisCycle = true;
            }

            DIV += 4;
            CheckIncrement();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckIncrement()
        {
            int divCheckBit = (TAC & 0b11) == 0 ? 0 : ((TAC & 0b11) * 2 + 1);
            bool divBitSet = (DIV & (1 << divCheckBit)) != 0;
            bool tacBitSet = (TAC & 4) != 0;
            bool edgeCheck = divBitSet && tacBitSet;

            if (_lastEdgeCheck && !edgeCheck)
            {
                _lastEdgeCheck = edgeCheck;
                if (TIMA < 0xFF)
                    TIMA++;
                else
                {
                    TIMA = 0;
                    _timaReloadScheduled = true;
                }
            }
            _lastEdgeCheck = edgeCheck;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Read(ushort address)
        {
            switch(address)
            {
                case 0xFF04: return (byte)(DIV >> 8);
                case 0xFF05: return TIMA;
                case 0xFF06: return TMA;
                case 0xFF07: return TAC;
            }
            return 0xFF;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ushort address, byte value) {
            switch(address)
            {
                case 0xFF04:
                    DIV = 0;
                    CheckIncrement();
                    break;
                case 0xFF05:
                    if(!_timaReloadedThisCycle)
                        TIMA = value;
                    _timaReloadScheduled = false;
                    break;
                case 0xFF06:
                    TMA = value;
                    if(_timaReloadedThisCycle)
                        TIMA = value;
                    break;
                case 0xFF07:
                    TAC = value;
                    break;
            }
        }
    }
}
