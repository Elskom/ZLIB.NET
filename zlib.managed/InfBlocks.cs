// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;

    internal sealed class InfBlocks
    {
        private readonly int[] bb = new int[1]; // bit length tree depth
        private readonly int[] tb = new int[1]; // bit length decoding tree
        private readonly object checkfn; // check function
        private int mode; // current inflate_block mode
        private int left; // if STORED, bytes left to copy
        private int table; // table lengths (14 bits)
        private int index; // index into blens (or border)
        private int[] blens; // bit lengths of codes
        private InfCodes codes; // if CODES, current state
        private int last; // true if this block is the last block
        private int[] hufts; // single malloc for tree space
        private long check; // check on output

        /// <summary>
        /// Initializes a new instance of the <see cref="InfBlocks"/> class.
        /// </summary>
        /// <param name="z">Zlib Stream.</param>
        /// <param name="checkfn">check function.</param>
        /// <param name="w">Window size.</param>
        internal InfBlocks(ZlibStream z, object checkfn, int w)
        {
            this.hufts = new int[4320];
            this.Window = new byte[w];
            this.End = w;
            this.checkfn = checkfn;
            this.mode = 0;
            this.Reset(z, null);
        }

        // Table for deflate from PKZIP's appnote.txt.
        internal static ReadOnlySpan<int> Border => new[]
        {
            16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15,
        };

        internal int End { get; } // one byte after sliding window

        internal int Bitk { get; set; } // bits in bit buffer

        internal int Bitb { get; set; } // bit buffer

        internal byte[] Window { get; private set; } // sliding window

        internal int Read { get; private set; } // window read pointer

        internal int Write { get; set; } // window write pointer

        // And'ing with mask[n] masks the lower n bits
        private static ReadOnlySpan<int> InflateMask => new[]
        {
            0x00000000, 0x00000001, 0x00000003, 0x00000007, 0x0000000f, 0x0000001f, 0x0000003f,
            0x0000007f, 0x000000ff, 0x000001ff, 0x000003ff, 0x000007ff, 0x00000fff, 0x00001fff,
            0x00003fff, 0x00007fff, 0x0000ffff,
        };

        internal void Reset(ZlibStream z, long[] c)
        {
            if (c is not null)
            {
                c[0] = this.check;
            }

            switch (this.mode)
            {
                case 4 or 5:
                    this.blens = null;
                    break;
                case 6:
                    InfCodes.Free();
                    break;
                default:
                    throw new InvalidOperationException();
            }

            this.mode = 0;
            this.Bitk = 0;
            this.Bitb = 0;
            this.Read = this.Write = 0;
            if (this.checkfn is not null)
            {
                z.Adler = this.check = Adler32.Calculate(0L, null, 0, 0);
            }
        }

        internal ZlibCompressionState Proc(ZlibStream z, ZlibCompressionState r)
        {
            // copy input/output information to locals (UPDATE macro restores)
            var p = z.NextInIndex;
            var n = z.AvailIn;
            var b = this.Bitb;
            var k = this.Bitk;
            var q = this.Write;
            var m = q < this.Read ? this.Read - q - 1 : this.End - q;

            // process input based on current state
            while (true)
            {
                int t; // temporary storage
                switch (this.mode)
                {
                    case 0:
                    {
                        while (k < 3)
                        {
                            if (n is not 0)
                            {
                                r = ZlibCompressionState.Ok;
                            }
                            else
                            {
                                this.Bitb = b;
                                this.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                this.Write = q;
                                return this.Inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.NextIn[p++] & 0xff) << k;
                            k += 8;
                        }

                        t = b & 7;
                        this.last = t & 1;
                        switch (t >= 0 ? t >> 1 : (t >> 1) + -2147483648)
                        {
                            case 0: // stored
                            {
                                b = b >= 0 ? b >> 3 : (b >> 3) + 536870912;
                                k -= 3;

                                t = k & 7; // go to byte boundary
                                b = b >= 0 ? b >> t : (b >> t) + (2 << ~t);
                                k -= t;

                                this.mode = 1; // get length of stored block
                                break;
                            }

                            case 1: // fixed
                            {
                                var bl = new int[1];
                                var bd = new int[1];
                                var tl = new int[1][];
                                var td = new int[1][];
                                _ = InfTree.Inflate_trees_fixed(bl, bd, tl, td);
                                this.codes = new(bl[0], bd[0], tl[0], td[0]);
                                b = b >= 0 ? b >> 3 : (b >> 3) + 536870912;
                                k -= 3;
                                this.mode = 6;
                                break;
                            }

                            case 2: // dynamic
                            {
                                b = b >= 0 ? b >> 3 : (b >> 3) + 536870912;
                                k -= 3;
                                this.mode = 3;
                                break;
                            }

                            case 3: // illegal
                            {
                                b = b >= 0 ? b >> 3 : (b >> 3) + 536870912;
                                k -= 3;
                                this.mode = 9;
                                z.Msg = "invalid block type";
                                r = ZlibCompressionState.DataError;
                                this.Bitb = b;
                                this.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                this.Write = q;
                                return this.Inflate_flush(z, r);
                            }

                            default:
                            {
                                throw new NotSupportedException();
                            }
                        }

                        break;
                    }

                    case 1:
                    {
                        while (k < 32)
                        {
                            if (n is not 0)
                            {
                                r = ZlibCompressionState.Ok;
                            }
                            else
                            {
                                this.Bitb = b;
                                this.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                this.Write = q;
                                return this.Inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.NextIn[p++] & 0xff) << k;
                            k += 8;
                        }

                        if (((~b >= 0 ? ~b >> 16 : (~b >> 16) + 65536) & 0xffff) != (b & 0xffff))
                        {
                            this.mode = 9;
                            z.Msg = "invalid stored block lengths";
                            r = ZlibCompressionState.DataError;
                            this.Bitb = b;
                            this.Bitk = k;
                            z.AvailIn = n;
                            z.TotalIn += p - z.NextInIndex;
                            z.NextInIndex = p;
                            this.Write = q;
                            return this.Inflate_flush(z, r);
                        }

                        this.left = b & 0xffff;
                        b = k = 0; // dump bits
                        this.mode = this.left != 0 ? 2 : this.last != 0 ? 7 : 0;
                        break;
                    }

                    case 2:
                    {
                        if (n is 0)
                        {
                            this.Bitb = b;
                            this.Bitk = k;
                            z.AvailIn = n;
                            z.TotalIn += p - z.NextInIndex;
                            z.NextInIndex = p;
                            this.Write = q;
                            return this.Inflate_flush(z, r);
                        }

                        if (m is 0)
                        {
                            if (q == this.End && this.Read is not 0)
                            {
                                q = 0;
                                m = q < this.Read ? this.Read - q - 1 : this.End - q;
                            }

                            if (m is 0)
                            {
                                this.Write = q;
                                r = this.Inflate_flush(z, r);
                                q = this.Write;
                                m = q < this.Read ? this.Read - q - 1 : this.End - q;
                                if (q == this.End && this.Read is not 0)
                                {
                                    q = 0;
                                    m = q < this.Read ? this.Read - q - 1 : this.End - q;
                                }

                                if (m is 0)
                                {
                                    this.Bitb = b;
                                    this.Bitk = k;
                                    z.AvailIn = n;
                                    z.TotalIn += p - z.NextInIndex;
                                    z.NextInIndex = p;
                                    this.Write = q;
                                    return this.Inflate_flush(z, r);
                                }
                            }
                        }

                        r = ZlibCompressionState.Ok;
                        t = this.left;
                        if (t > n)
                        {
                            t = n;
                        }

                        if (t > m)
                        {
                            t = m;
                        }

                        Array.Copy(z.NextIn, p, this.Window, q, t);
                        p += t;
                        n -= t;
                        q += t;
                        m -= t;
                        if ((this.left -= t) != 0)
                        {
                            break;
                        }

                        this.mode = this.last != 0 ? 7 : 0;
                        break;
                    }

                    case 3:
                    {
                        while (k < 14)
                        {
                            if (n != 0)
                            {
                                r = ZlibCompressionState.Ok;
                            }
                            else
                            {
                                this.Bitb = b;
                                this.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                this.Write = q;
                                return this.Inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.NextIn[p++] & 0xff) << k;
                            k += 8;
                        }

                        this.table = t = b & 0x3fff;
                        if ((t & 0x1f) > 29 || ((t >> 5) & 0x1f) > 29)
                        {
                            this.mode = 9;
                            z.Msg = "too many length or distance symbols";
                            r = ZlibCompressionState.DataError;
                            this.Bitb = b;
                            this.Bitk = k;
                            z.AvailIn = n;
                            z.TotalIn += p - z.NextInIndex;
                            z.NextInIndex = p;
                            this.Write = q;
                            return this.Inflate_flush(z, r);
                        }

                        t = 258 + (t & 0x1f) + ((t >> 5) & 0x1f);
                        this.blens = new int[t];
                        b = b >= 0 ? b >> 14 : (b >> 14) + 262144;
                        k -= 14;
                        this.index = 0;
                        this.mode = 4;
                        break;
                    }

                    case 4:
                    {
                        while (this.index < 4 + (this.table >= 0 ? this.table >> 10 : (this.table >> 10) + 4194304))
                        {
                            while (k < 3)
                            {
                                if (n is not 0)
                                {
                                    r = ZlibCompressionState.Ok;
                                }
                                else
                                {
                                    this.Bitb = b;
                                    this.Bitk = k;
                                    z.AvailIn = n;
                                    z.TotalIn += p - z.NextInIndex;
                                    z.NextInIndex = p;
                                    this.Write = q;
                                    return this.Inflate_flush(z, r);
                                }

                                n--;
                                b |= (z.NextIn[p++] & 0xff) << k;
                                k += 8;
                            }

                            this.blens[Border[this.index++]] = b & 7;
                            b = b >= 0 ? b >> 3 : (b >> 3) + 536870912;
                            k -= 3;
                        }

                        while (this.index < 19)
                        {
                            this.blens[Border[this.index++]] = 0;
                        }

                        this.bb[0] = 7;
                        t = (int)InfTree.Inflate_trees_bits(this.blens, this.bb, this.tb, this.hufts, z);
                        if (t is not (int)ZlibCompressionState.Ok)
                        {
                            r = (ZlibCompressionState)t;
                            if (r is ZlibCompressionState.DataError)
                            {
                                this.blens = null;
                                this.mode = 9;
                            }

                            this.Bitb = b;
                            this.Bitk = k;
                            z.AvailIn = n;
                            z.TotalIn += p - z.NextInIndex;
                            z.NextInIndex = p;
                            this.Write = q;
                            return this.Inflate_flush(z, r);
                        }

                        this.index = 0;
                        this.mode = 5;
                        break;
                    }

                    case 5:
                    {
                        while (true)
                        {
                            t = this.table;
                            if (this.index >= 258 + (t & 0x1f) + ((t >> 5) & 0x1f))
                            {
                                break;
                            }

                            t = this.bb[0];
                            while (k < t)
                            {
                                if (n is not 0)
                                {
                                    r = ZlibCompressionState.Ok;
                                }
                                else
                                {
                                    this.Bitb = b;
                                    this.Bitk = k;
                                    z.AvailIn = n;
                                    z.TotalIn += p - z.NextInIndex;
                                    z.NextInIndex = p;
                                    this.Write = q;
                                    return this.Inflate_flush(z, r);
                                }

                                n--;
                                b |= (z.NextIn[p++] & 0xff) << k;
                                k += 8;
                            }

                            t = this.hufts[((this.tb[0] + (b & InflateMask[t])) * 3) + 1];
                            var c = this.hufts[((this.tb[0] + (b & InflateMask[t])) * 3) + 2];
                            if (c < 16)
                            {
                                b = b >= 0 ? b >> t : (b >> t) + (2 << ~t);
                                k -= t;
                                this.blens[this.index++] = c;
                            }
                            else
                            {
                                // c == 16..18
                                var i = c is 18 ? 7 : c - 14;
                                var j = c is 18 ? 11 : 3;
                                while (k < t + i)
                                {
                                    if (n is not 0)
                                    {
                                        r = ZlibCompressionState.Ok;
                                    }
                                    else
                                    {
                                        this.Bitb = b;
                                        this.Bitk = k;
                                        z.AvailIn = n;
                                        z.TotalIn += p - z.NextInIndex;
                                        z.NextInIndex = p;
                                        this.Write = q;
                                        return this.Inflate_flush(z, r);
                                    }

                                    n--;
                                    b |= (z.NextIn[p++] & 0xff) << k;
                                    k += 8;
                                }

                                b = b >= 0 ? b >> t : (b >> t) + (2 << ~t);
                                k -= t;
                                j += b & InflateMask[i];
                                b = b >= 0 ? b >> i : (b >> i) + (2 << ~i);
                                k -= i;
                                i = this.index;
                                t = this.table;
                                if (i + j > 258 + (t & 0x1f) + ((t >> 5) & 0x1f) || (c is 16 && i < 1))
                                {
                                    this.blens = null;
                                    this.mode = 9;
                                    z.Msg = "invalid bit length repeat";
                                    r = ZlibCompressionState.DataError;
                                    this.Bitb = b;
                                    this.Bitk = k;
                                    z.AvailIn = n;
                                    z.TotalIn += p - z.NextInIndex;
                                    z.NextInIndex = p;
                                    this.Write = q;
                                    return this.Inflate_flush(z, r);
                                }

                                c = c is 16 ? this.blens[i - 1] : 0;
                                do
                                {
                                    this.blens[i++] = c;
                                }
                                while (--j != 0);

                                this.index = i;
                            }
                        }

                        this.tb[0] = -1;
                        var bl = new int[1];
                        var bd = new int[1];
                        var tl = new int[1];
                        var td = new int[1];
                        bl[0] = 9; // must be <= 9 for lookahead assumptions
                        bd[0] = 6; // must be <= 9 for lookahead assumptions
                        t = this.table;
                        t = (int)InfTree.Inflate_trees_dynamic(257 + (t & 0x1f), 1 + ((t >> 5) & 0x1f), this.blens, bl, bd, tl, td, this.hufts, z);
                        if (t is not (int)ZlibCompressionState.Ok)
                        {
                            if (t is (int)ZlibCompressionState.DataError)
                            {
                                this.blens = null;
                                this.mode = 9;
                            }

                            r = (ZlibCompressionState)t;
                            this.Bitb = b;
                            this.Bitk = k;
                            z.AvailIn = n;
                            z.TotalIn += p - z.NextInIndex;
                            z.NextInIndex = p;
                            this.Write = q;
                            return this.Inflate_flush(z, r);
                        }

                        this.codes = new(bl[0], bd[0], this.hufts, tl[0], this.hufts, td[0]);
                        this.blens = null;
                        this.mode = 6;
                        break;
                    }

                    case 6:
                    {
                        this.Bitb = b;
                        this.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        this.Write = q;
                        r = this.codes.Proc(this, z, r);
                        if (r is not ZlibCompressionState.StreamEnd)
                        {
                            return this.Inflate_flush(z, r);
                        }

                        r = ZlibCompressionState.Ok;
                        InfCodes.Free();
                        p = z.NextInIndex;
                        n = z.AvailIn;
                        b = this.Bitb;
                        k = this.Bitk;
                        q = this.Write;
                        m = q < this.Read ? this.Read - q - 1 : this.End - q;
                        if (this.last is 0)
                        {
                            this.mode = 0;
                            break;
                        }

                        this.mode = 7;
                        break;
                    }

                    case 7:
                    {
                        this.Write = q;
                        r = this.Inflate_flush(z, r);
                        q = this.Write;
                        m = q < this.Read ? this.Read - q - 1 : this.End - q;
                        if (this.Read != this.Write)
                        {
                            this.Bitb = b;
                            this.Bitk = k;
                            z.AvailIn = n;
                            z.TotalIn += p - z.NextInIndex;
                            z.NextInIndex = p;
                            this.Write = q;
                            return this.Inflate_flush(z, r);
                        }

                        this.mode = 8;
                        break;
                    }

                    case 8:
                    {
                        r = ZlibCompressionState.StreamEnd;
                        this.Bitb = b;
                        this.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        this.Write = q;
                        return this.Inflate_flush(z, r);
                    }

                    case 9:
                    {
                        r = ZlibCompressionState.DataError;
                        this.Bitb = b;
                        this.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        this.Write = q;
                        return this.Inflate_flush(z, r);
                    }

                    default:
                    {
                        r = ZlibCompressionState.StreamError;
                        this.Bitb = b;
                        this.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        this.Write = q;
                        return this.Inflate_flush(z, r);
                    }
                }
            }
        }

        internal void Free(ZlibStream z)
        {
            this.Reset(z, null);
            this.Window = null;
            this.hufts = null;
        }

        // copy as much as possible from the sliding window to the output area
        internal ZlibCompressionState Inflate_flush(ZlibStream z, ZlibCompressionState r)
        {
            // local copies of source and destination pointers
            var p = z.NextOutIndex;
            var q = this.Read;

            // compute number of bytes to copy as far as end of window
            var n = (q <= this.Write ? this.Write : this.End) - q;
            if (n > z.AvailOut)
            {
                n = z.AvailOut;
            }

            if (n != 0 && r == ZlibCompressionState.BufError)
            {
                r = ZlibCompressionState.Ok;
            }

            // update counters
            z.AvailOut -= n;
            z.TotalOut += n;

            // update check information
            if (this.checkfn is not null)
            {
                z.Adler = this.check = Adler32.Calculate(this.check, this.Window, q, n);
            }

            // copy as far as end of window
            Array.Copy(this.Window, q, z.NextOut, p, n);
            p += n;
            q += n;

            // see if more to copy at beginning of window
            if (q == this.End)
            {
                // wrap pointers
                q = 0;
                if (this.Write == this.End)
                {
                    this.Write = 0;
                }

                // compute bytes to copy
                n = this.Write;
                if (n > z.AvailOut)
                {
                    n = z.AvailOut;
                }

                if (n != 0 && r is ZlibCompressionState.BufError)
                {
                    r = ZlibCompressionState.Ok;
                }

                // update counters
                z.AvailOut -= n;
                z.TotalOut += n;

                // update check information
                if (this.checkfn is not null)
                {
                    z.Adler = this.check = Adler32.Calculate(this.check, this.Window, q, n);
                }

                // copy
                Array.Copy(this.Window, q, z.NextOut, p, n);
                p += n;
                q += n;
            }

            // update pointers
            z.NextOutIndex = p;
            this.Read = q;

            // done
            return r;
        }
    }
}
