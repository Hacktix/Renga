using Chroma.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Renga.Core
{
    public enum FetcherState
    {
        FetchTileNo = 0,
        FetchDataLo = 1,
        FetchDataHi = 2,
        Push = 3
    }

    public enum PPUMode
    {
        HBlank = 0,
        VBlank = 1,
        OAMScan = 2,
        Rendering = 3
    }

    public enum TileDataMethod
    {
        Base8000,
        Base8800
    }

    internal class PPU
    {
        public Color[] Pixels = new Color[160 * 144];

        public byte[] VRAM = new byte[0x2000];
        public byte[] OAM = new byte[0xA0];

        // LCDC
        public byte LCDC
        {
            get
            {
                return (byte)(
                    (EnablePPU ? 0x80 : 0) |
                    (WindowTilemapBase == 0x1C00 ? 0x40 : 0) |
                    (EnableWindow ? 0x20 : 0) |
                    (DataFetchMethod == TileDataMethod.Base8000 ? 0x10 : 0) |
                    (BackgroundTilemapBase == 0x1C00 ? 0x08 : 0) |
                    (TallSprites ? 0x04 : 0) |
                    (EnableSprites ? 0x02 : 0) |
                    (EnableBackground ? 0x01 : 0));
            }
            set
            {
                EnablePPU = (value & 0x80) != 0;
                WindowTilemapBase = (ushort)((value & 0x40) == 0 ? 0x1800 : 0x1C00);
                EnableWindow = (value & 0x20) != 0;
                DataFetchMethod = (value & 0x10) == 0 ? TileDataMethod.Base8800 : TileDataMethod.Base8000;
                BackgroundTilemapBase = (ushort)((value & 0x08) == 0 ? 0x1800 : 0x1C00);
                TallSprites = (value & 0x04) != 0;
                EnableSprites = (value & 0x02) != 0;
                EnableBackground = (value & 0x01) != 0;
            }
        }
        public bool EnablePPU = false;
        public ushort WindowTilemapBase = 0x1800;
        public bool EnableWindow = false;
        public TileDataMethod DataFetchMethod = TileDataMethod.Base8800;
        public ushort BackgroundTilemapBase = 0x1800;
        public bool TallSprites = false;
        public bool EnableSprites = false;
        public bool EnableBackground = false;

        // STAT
        public byte STAT
        {
            get
            {
                return (byte)(0x80 |
                    (EnableInterruptLYC ? 0x40 : 0) |
                    (EnableInterruptMode2 ? 0x20 : 0) |
                    (EnableInterruptMode1 ? 0x10 : 0) |
                    (EnableInterruptMode0 ? 0x08 : 0) |
                    (FlagLYC ? 0x40 : 0) |
                    (int)Mode
                );
            }

            set
            {
                EnableInterruptLYC = (value & 0x40) != 0;
                EnableInterruptMode2 = (value & 0x20) != 0;
                EnableInterruptMode1 = (value & 0x10) != 0;
                EnableInterruptMode0 = (value & 0x08) != 0;
            }
        }
        public bool EnableInterruptLYC = false;
        public bool EnableInterruptMode2 = false;
        public bool EnableInterruptMode1 = false;
        public bool EnableInterruptMode0 = false;
        public bool FlagLYC { get { return LYC == LY; } }
        public PPUMode Mode = PPUMode.HBlank;
        private bool _lastStateSTAT = false;

        public byte SCX = 0;
        public byte SCY = 0;
        public byte LY = 0;
        public byte LYC = 0;
        public byte WX = 0;
        public byte WY = 0;

        private int _tileX = 0;
        private int _lx = 0;
        private int _pix = 0;
        private int _cycle = 0;

        private Queue<Color> _bgFifo = new Queue<Color>();
        private bool _initFetch = false;
        private FetcherState _fetcherState = FetcherState.FetchTileNo;
        private byte _tileNo = 0;
        private byte _tileDataLo = 0;
        private byte _tileDataHi = 0;

        // DMG Palette
        public static readonly Color[] DMGPalette = new Color[]
        {
            System.Drawing.ColorTranslator.FromHtml(Renga.Config.GetProperty("dmgColor1", "#9BBC0F")),
            System.Drawing.ColorTranslator.FromHtml(Renga.Config.GetProperty("dmgColor2", "#8BAC0F")),
            System.Drawing.ColorTranslator.FromHtml(Renga.Config.GetProperty("dmgColor3", "#306230")),
            System.Drawing.ColorTranslator.FromHtml(Renga.Config.GetProperty("dmgColor4", "#0F380F")),
        };
        private int[] _palette = new int[] { 0, 1, 2, 3 };
        public byte BGP
        {
            get
            {
                return (byte)(
                    ((_palette[0] & 0b11) << 0) |
                    ((_palette[1] & 0b11) << 2) |
                    ((_palette[2] & 0b11) << 4) |
                    ((_palette[3] & 0b11) << 6)
                );
            }
            set
            {
                _palette[0] = (value & 0b00000011) >> 0;
                _palette[1] = (value & 0b00001100) >> 2;
                _palette[2] = (value & 0b00110000) >> 4;
                _palette[3] = (value & 0b11000000) >> 6;
            }
        }



        private Emulator _emu;
        public PPU(Emulator emu)
        {
            _emu = emu;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadVRAM(ushort addr)
        {
            return VRAM[addr & 0x1FFF];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteVRAM(ushort addr, byte value)
        {
            VRAM[addr & 0x1FFF] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadOAM(ushort addr)
        {
            return OAM[addr % 0xA0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteOAM(ushort addr, byte value)
        {
            OAM[addr % 0xA0] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadRegister(ushort addr)
        {
            switch(addr)
            {
                case 0xFF40: return LCDC;
                case 0xFF41: return STAT;
                case 0xFF42: return SCY;
                case 0xFF43: return SCX;
                case 0xFF44: return LY;
                case 0xFF45: return LYC;
                case 0xFF47: return BGP;
                case 0xFF4A: return WY;
                case 0xFF4B: return WX;
                default:
                    Renga.Log.Warning($"Read from unknown PPU Register ${addr:X4}");
                    return 0xFF;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteRegister(ushort addr, byte value)
        {
            switch(addr)
            {
                case 0xFF40: LCDC = value; return;
                case 0xFF41: STAT = value; return;
                case 0xFF42: SCY = value; return;
                case 0xFF43: SCX = value; return;
                case 0xFF44: return;
                case 0xFF45: LYC = value; return;
                case 0xFF47: BGP = value; return;
                case 0xFF4A: WY = value; return;
                case 0xFF4B: WX = value; return;
                default:
                    Renga.Log.Warning($"Wrote ${value:X2} to unknown PPU Register ${addr:X4}");
                    return;
            }
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tick()
        {
            if ((LCDC & (1 << 7)) == 0)
                return;

            TickPPU();
            CheckSTAT();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckSTAT()
        {
            bool statIntr =
                (EnableInterruptMode2 && Mode == PPUMode.OAMScan) ||
                (EnableInterruptMode1 && Mode == PPUMode.VBlank) ||
                (EnableInterruptMode0 && Mode == PPUMode.HBlank) ||
                (EnableInterruptLYC && LY == LYC);

            if (!_lastStateSTAT && statIntr)
            {
                _emu.CPU.IF |= 0b10;
            }
            _lastStateSTAT = statIntr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TickPPU()
        {
            if (LY >= 144)
            {
                if (++_cycle == 456)
                {
                    if (++LY == 154)
                        LY = 0;
                    _cycle = 0;
                }
                return;
            }

            if (_cycle == 0)
            {
                Mode = PPUMode.OAMScan;
                // Do OAM Scan Here
            }

            if (_cycle < 80)
            {
                _cycle++;
                return;
            }

            if (_lx < 160)
            {
                Mode = PPUMode.Rendering;

                if(_tileX < 20)
                {
                    switch (_fetcherState)
                    {
                        case FetcherState.FetchTileNo:
                            if ((_cycle & 1) == 0)
                                break;
                            int tileNoOffset = (
                                ((_tileX + (SCX / 8)) & 0x1F)
                                + 32 * (((LY + SCY) & 0xFF) / 8)
                            ) & 0x3FF;
                            _tileNo = VRAM[BackgroundTilemapBase + tileNoOffset];
                            _fetcherState = FetcherState.FetchDataLo;
                            break;

                        case FetcherState.FetchDataLo:
                            if ((_cycle & 1) == 0)
                                break;
                            int tileDataLoAddr = DataFetchMethod == TileDataMethod.Base8000
                                ? 16 * _tileNo
                                : 0x1000 + 16 * (sbyte)_tileNo;
                            tileDataLoAddr += 2 * ((LY + SCY) & 7);
                            _tileDataLo = VRAM[tileDataLoAddr];
                            _fetcherState = FetcherState.FetchDataHi;
                            break;

                        case FetcherState.FetchDataHi:
                            if ((_cycle & 1) == 0)
                                break;
                            int tileDataHiAddr = DataFetchMethod == TileDataMethod.Base8000
                                ? 16 * _tileNo
                                : 0x1000 + 16 * (sbyte)_tileNo;
                            tileDataHiAddr += 2 * ((LY + SCY) & 7) + 1;
                            _tileDataHi = VRAM[tileDataHiAddr];
                            if (!_initFetch)
                            {
                                _initFetch = true;
                                _fetcherState = FetcherState.FetchTileNo;
                            }
                            else
                                _fetcherState = FetcherState.Push;
                            break;

                        case FetcherState.Push:
                            if (_bgFifo.Count != 0)
                                break;

                            _tileX++;
                            for (int bit = 7; bit >= 0; bit--)
                            {
                                int colorIndex =
                                    (((_tileDataHi >> bit) & 1) << 1) |
                                    ((_tileDataLo >> bit) & 1);
                                if (EnableBackground)
                                    _bgFifo.Enqueue(DMGPalette[_palette[colorIndex]]);
                                else
                                    _bgFifo.Enqueue(DMGPalette[0]);
                            }
                            _fetcherState = FetcherState.FetchTileNo;
                            break;
                    }
                }

                if (_bgFifo.Count != 0)
                {
                    Pixels[_pix++] = _bgFifo.Dequeue();
                    _lx++;
                }
            }
            else
                Mode = PPUMode.HBlank;

            if (++_cycle == 456)
            {
                if (++LY == 144)
                {
                    Mode = PPUMode.VBlank;
                    _pix = 0;
                    _emu.CPU.IF |= 1;
                }
                _tileX = 0;
                _lx = 0;
                _initFetch = false;
                _bgFifo.Clear();
                _cycle = 0;
                _fetcherState = FetcherState.FetchTileNo;
            }
        }
    }
}
