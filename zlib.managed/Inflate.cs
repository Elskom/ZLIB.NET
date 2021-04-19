// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    internal sealed class Inflate
    {
        private int Mode { get; set; } // current inflate mode

        // mode dependent information
        private int Method { get; set; } // if FLAGS, method byte

        // if CHECK, check values to compare
        private long[] Was { get; } = new long[1]; // computed check value

        private long Need { get; set; } // stream check value

        // if BAD, inflateSync's marker bytes count
        private int Marker { get; set; }

        // mode independent information
        private int Nowrap { get; set; } // flag for no wrapper

        private int Wbits { get; set; } // log2(window size)  (8..15, defaults to 15)

        private InfBlocks Blocks { get; set; } // current inflate_blocks state

        internal static ZlibCompressionState Decompress(ZlibStream z, ZlibFlushStrategy f)
        {
            if (z?.IState is null || z.NextIn is null)
            {
                return ZlibCompressionState.StreamError;
            }

            while (true)
            {
                switch (z.IState.Mode)
                {
                    case 0:
                    {
                        if (z.AvailIn is 0)
                        {
                            return ZlibCompressionState.BufError;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Method = z.NextIn[z.NextInIndex++] & 0xf;
                        if (z.IState.Method is not 8)
                        {
                            z.IState.Mode = 13;
                            z.Msg = "unknown compression method";
                            z.IState.Marker = 5; // can't try inflateSync
                            break;
                        }

                        if ((z.IState.Method >> 4) + 8 > z.IState.Wbits)
                        {
                            z.IState.Mode = 13;
                            z.Msg = "invalid window size";
                            z.IState.Marker = 5; // can't try inflateSync
                            break;
                        }

                        z.IState.Mode = 1;
                        break;
                    }

                    case 1:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        var b = z.NextIn[z.NextInIndex++] & 0xff;
                        if (((z.IState.Method << 8) + b) % 31 != 0)
                        {
                            z.IState.Mode = 13;
                            z.Msg = "incorrect header check";
                            z.IState.Marker = 5; // can't try inflateSync
                            break;
                        }

                        if ((b & 0x20) is 0)
                        {
                            z.IState.Mode = 7;
                            break;
                        }

                        z.IState.Mode = 2;
                        break;
                    }

                    case 2:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Need = ((z.NextIn[z.NextInIndex++] & 0xff) << 24) & unchecked((int)0xff000000L);
                        z.IState.Mode = 3;
                        break;
                    }

                    case 3:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Need += ((z.NextIn[z.NextInIndex++] & 0xff) << 16) & 0xff0000L;
                        z.IState.Mode = 4;
                        break;
                    }

                    case 4:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Need += ((z.NextIn[z.NextInIndex++] & 0xff) << 8) & 0xff00L;
                        z.IState.Mode = 5;
                        break;
                    }

                    case 5:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Need += z.NextIn[z.NextInIndex++] & 0xffL;
                        z.Adler = z.IState.Need;
                        z.IState.Mode = 6;
                        return ZlibCompressionState.NeedDict;
                    }

                    case 6:
                    {
                        z.IState.Mode = 13;
                        z.Msg = "need dictionary";
                        z.IState.Marker = 0; // can try inflateSync
                        return ZlibCompressionState.StreamError;
                    }

                    case 7:
                    {
                        var r = z.IState.Blocks.Proc(z, f == ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok);
                        if (r is ZlibCompressionState.DataError)
                        {
                            z.IState.Mode = 13;
                            z.IState.Marker = 0; // can try inflateSync
                            break;
                        }

                        if (r is ZlibCompressionState.Ok)
                        {
                            r = f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        if (r is not ZlibCompressionState.StreamEnd)
                        {
                            return r;
                        }

                        z.IState.Blocks.Reset(z, z.IState.Was);
                        if (z.IState.Nowrap is not 0)
                        {
                            z.IState.Mode = 12;
                            break;
                        }

                        z.IState.Mode = 8;
                        break;
                    }

                    case 8:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Need = ((z.NextIn[z.NextInIndex++] & 0xff) << 24) & unchecked((int)0xff000000L);
                        z.IState.Mode = 9;
                        break;
                    }

                    case 9:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Need += ((z.NextIn[z.NextInIndex++] & 0xff) << 16) & 0xff0000L;
                        z.IState.Mode = 10;
                        break;
                    }

                    case 10:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Need += ((z.NextIn[z.NextInIndex++] & 0xff) << 8) & 0xff00L;
                        z.IState.Mode = 11;
                        break;
                    }

                    case 11:
                    {
                        if (z.AvailIn is 0)
                        {
                            return f is ZlibFlushStrategy.Finish ? ZlibCompressionState.BufError : ZlibCompressionState.Ok;
                        }

                        z.AvailIn--;
                        z.TotalIn++;
                        z.IState.Need += z.NextIn[z.NextInIndex++] & 0xffL;
                        if ((int)z.IState.Was[0] != (int)z.IState.Need)
                        {
                            z.IState.Mode = 13;
                            z.Msg = "incorrect data check";
                            z.IState.Marker = 5; // can't try inflateSync
                            break;
                        }

                        z.IState.Mode = 12;
                        break;
                    }

                    case 12:
                    {
                        return ZlibCompressionState.StreamEnd;
                    }

                    case 13:
                    {
                        return ZlibCompressionState.DataError;
                    }

                    default:
                    {
                        return ZlibCompressionState.StreamError;
                    }
                }
            }
        }

        internal ZlibCompressionState InflateEnd(ZlibStream z)
        {
            this.Blocks?.Free(z);
            this.Blocks = null;
            return ZlibCompressionState.Ok;
        }

        internal ZlibCompressionState InflateInit(ZlibStream z, int w)
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
            if (w is < 8 or > 15)
            {
                _ = this.InflateEnd(z);
                return ZlibCompressionState.StreamError;
            }

            this.Wbits = w;
            z.IState.Blocks = new(z, z.IState.Nowrap is not 0 ? null : this, 1 << w);

            // reset state
            _ = InflateReset(z);
            return ZlibCompressionState.Ok;
        }

        private static ZlibCompressionState InflateReset(ZlibStream z)
        {
            if (z?.IState is null)
            {
                return ZlibCompressionState.StreamError;
            }

            z.TotalIn = z.TotalOut = 0;
            z.Msg = null;
            z.IState.Mode = z.IState.Nowrap is not 0 ? 7 : 0;
            z.IState.Blocks.Reset(z, null);
            return ZlibCompressionState.Ok;
        }
    }
}
