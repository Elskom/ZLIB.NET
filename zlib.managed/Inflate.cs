// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System.Diagnostics.CodeAnalysis;

    internal sealed class Inflate
    {
        // preset dictionary flag in zlib header
        private const int PRESETDICT = 0x20;
        private const int ZDEFLATED = 8;
        private const int METHOD = 0; // waiting for method byte
        private const int FLAG = 1; // waiting for flag byte
        private const int DICT4 = 2; // four dictionary check bytes to go
        private const int DICT3 = 3; // three dictionary check bytes to go
        private const int DICT2 = 4; // two dictionary check bytes to go
        private const int DICT1 = 5; // one dictionary check byte to go
        private const int DICT0 = 6; // waiting for inflateSetDictionary
        private const int BLOCKS = 7; // decompressing blocks
        private const int CHECK4 = 8; // four check bytes to go
        private const int CHECK3 = 9; // three check bytes to go
        private const int CHECK2 = 10; // two check bytes to go
        private const int CHECK1 = 11; // one check byte to go
        private const int DONE = 12; // finished check, done
        private const int BAD = 13; // got an error--stay here

        internal int Mode { get; private set; } // current inflate mode

        // mode dependent information
        internal int Method { get; private set; } // if FLAGS, method byte

        // if CHECK, check values to compare
        internal long[] Was { get; private set; } = new long[1]; // computed check value

        internal long Need { get; private set; } // stream check value

        // if BAD, inflateSync's marker bytes count
        internal int Marker { get; private set; }

        // mode independent information
        internal int Nowrap { get; private set; } // flag for no wrapper

        internal int Wbits { get; private set; } // log2(window size)  (8..15, defaults to 15)

        internal InfBlocks Blocks { get; private set; } // current inflate_blocks state

        internal static ZlibCompressionState InflateReset(ZStream z)
        {
            if (z == null || z.Istate == null)
            {
                return ZlibCompressionState.ZSTREAMERROR;
            }

            z.TotalIn = z.TotalOut = 0;
            z.Msg = null;
            z.Istate.Mode = z.Istate.Nowrap != 0 ? BLOCKS : METHOD;
            z.Istate.Blocks.Reset(z, null);
            return ZlibCompressionState.ZOK;
        }

        [SuppressMessage("Major Code Smell", "S1854:Unused assignments should be removed", Justification = "Needed for inflate.")]
        internal static ZlibCompressionState Decompress(ZStream z, ZlibFlushStrategy f)
        {
            if (z == null || z.Istate == null || z.INextIn == null)
            {
                return ZlibCompressionState.ZSTREAMERROR;
            }

            while (true)
            {
                switch (z.Istate.Mode)
                {
                    case METHOD:
                    {
                        if (z.AvailIn == 0)
                        {
                            return ZlibCompressionState.ZBUFERROR;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Method = z.INextIn[z.NextInIndex++] & 0xf;
                        if (z.Istate.Method != ZDEFLATED)
                        {
                            z.Istate.Mode = BAD;
                            z.Msg = "unknown compression method";
                            z.Istate.Marker = 5; // can't try inflateSync
                            break;
                        }

                        if ((z.Istate.Method >> 4) + 8 > z.Istate.Wbits)
                        {
                            z.Istate.Mode = BAD;
                            z.Msg = "invalid window size";
                            z.Istate.Marker = 5; // can't try inflateSync
                            break;
                        }

                        z.Istate.Mode = FLAG;
                        break;
                    }

                    case FLAG:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        var b = z.INextIn[z.NextInIndex++] & 0xff;
                        if (((z.Istate.Method << 8) + b) % 31 != 0)
                        {
                            z.Istate.Mode = BAD;
                            z.Msg = "incorrect header check";
                            z.Istate.Marker = 5; // can't try inflateSync
                            break;
                        }

                        if ((b & PRESETDICT) == 0)
                        {
                            z.Istate.Mode = BLOCKS;
                            break;
                        }

                        z.Istate.Mode = DICT4;
                        break;
                    }

                    case DICT4:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Need = ((z.INextIn[z.NextInIndex++] & 0xff) << 24) & unchecked((int)0xff000000L);
                        z.Istate.Mode = DICT3;
                        break;
                    }

                    case DICT3:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Need += ((z.INextIn[z.NextInIndex++] & 0xff) << 16) & 0xff0000L;
                        z.Istate.Mode = DICT2;
                        break;
                    }

                    case DICT2:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Need += ((z.INextIn[z.NextInIndex++] & 0xff) << 8) & 0xff00L;
                        z.Istate.Mode = DICT1;
                        break;
                    }

                    case DICT1:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Need += z.INextIn[z.NextInIndex++] & 0xffL;
                        z.Adler = z.Istate.Need;
                        z.Istate.Mode = DICT0;
                        return ZlibCompressionState.ZNEEDDICT;
                    }

                    case DICT0:
                    {
                        z.Istate.Mode = BAD;
                        z.Msg = "need dictionary";
                        z.Istate.Marker = 0; // can try inflateSync
                        return ZlibCompressionState.ZSTREAMERROR;
                    }

                    case BLOCKS:
                    {
                        var r = z.Istate.Blocks.Proc(z, f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK);
                        if (r == ZlibCompressionState.ZDATAERROR)
                        {
                            z.Istate.Mode = BAD;
                            z.Istate.Marker = 0; // can try inflateSync
                            break;
                        }

                        if (r == ZlibCompressionState.ZOK)
                        {
                            r = f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        if (r != ZlibCompressionState.ZSTREAMEND)
                        {
                            return r;
                        }

                        z.Istate.Blocks.Reset(z, z.Istate.Was);
                        if (z.Istate.Nowrap != 0)
                        {
                            z.Istate.Mode = DONE;
                            break;
                        }

                        z.Istate.Mode = CHECK4;
                        break;
                    }

                    case CHECK4:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Need = ((z.INextIn[z.NextInIndex++] & 0xff) << 24) & unchecked((int)0xff000000L);
                        z.Istate.Mode = CHECK3;
                        break;
                    }

                    case CHECK3:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Need += ((z.INextIn[z.NextInIndex++] & 0xff) << 16) & 0xff0000L;
                        z.Istate.Mode = CHECK2;
                        break;
                    }

                    case CHECK2:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Need += ((z.INextIn[z.NextInIndex++] & 0xff) << 8) & 0xff00L;
                        z.Istate.Mode = CHECK1;
                        break;
                    }

                    case CHECK1:
                    {
                        if (z.AvailIn == 0)
                        {
                            return f == ZlibFlushStrategy.ZFINISH ? ZlibCompressionState.ZBUFERROR : ZlibCompressionState.ZOK;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.Istate.Need += z.INextIn[z.NextInIndex++] & 0xffL;
                        if ((int)z.Istate.Was[0] != (int)z.Istate.Need)
                        {
                            z.Istate.Mode = BAD;
                            z.Msg = "incorrect data check";
                            z.Istate.Marker = 5; // can't try inflateSync
                            break;
                        }

                        z.Istate.Mode = DONE;
                        break;
                    }

                    case DONE:
                    {
                        return ZlibCompressionState.ZSTREAMEND;
                    }

                    case BAD:
                    {
                        return ZlibCompressionState.ZDATAERROR;
                    }

                    default:
                    {
                        return ZlibCompressionState.ZSTREAMERROR;
                    }
                }
            }
        }

        internal ZlibCompressionState InflateEnd(ZStream z)
        {
            if (this.Blocks != null)
            {
                this.Blocks.Free(z);
            }

            this.Blocks = null;
            return ZlibCompressionState.ZOK;
        }

        internal ZlibCompressionState InflateInit(ZStream z, int w)
        {
            z.Msg = null;
            this.Blocks = null;

            // handle undocumented nowrap option (no zlib header or check)
            this.Nowrap = 0;
            if (w < 0)
            {
                w = -w;
                this.Nowrap = 1;
            }

            // set window size
            if (w < 8 || w > 15)
            {
                _ = this.InflateEnd(z);
                return ZlibCompressionState.ZSTREAMERROR;
            }

            this.Wbits = w;
            z.Istate.Blocks = new InfBlocks(z, z.Istate.Nowrap != 0 ? null : this, 1 << w);

            // reset state
            _ = InflateReset(z);
            return ZlibCompressionState.ZOK;
        }
    }
}
