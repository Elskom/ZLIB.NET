// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;

    internal sealed class InfCodes
    {
        internal InfCodes(int bl, int bd, int[] tl, int tlIndex, int[] td, int tdIndex)
        {
            this.Mode = 0;
            this.Lbits = (byte)bl;
            this.Dbits = (byte)bd;
            this.Ltree = tl;
            this.LtreeIndex = tlIndex;
            this.Dtree = td;
            this.DtreeIndex = tdIndex;
        }

        internal InfCodes(int bl, int bd, int[] tl, int[] td)
        {
            this.Mode = 0;
            this.Lbits = (byte)bl;
            this.Dbits = (byte)bd;
            this.Ltree = tl;
            this.LtreeIndex = 0;
            this.Dtree = td;
            this.DtreeIndex = 0;
        }

        internal int Mode { get; private set; } // current inflate_codes mode

        // mode dependent information
        internal int Len { get; private set; }

        internal int[] Tree { get; private set; } // pointer into tree

        internal int TreeIndex { get; private set; }

        internal int Need { get; private set; } // bits needed

        internal int Lit { get; private set; }

        // if EXT or COPY, where and how much
        internal int GetRenamed { get; private set; } // bits to get for extra

        internal int Dist { get; private set; } // distance back to copy from

        internal byte Lbits { get; } // ltree bits decoded per branch

        internal byte Dbits { get; } // dtree bits decoder per branch

        internal int[] Ltree { get; } // literal/length/eob tree

        internal int LtreeIndex { get; } // literal/length/eob tree

        internal int[] Dtree { get; } // distance tree

        internal int DtreeIndex { get; } // distance tree

        private static ReadOnlySpan<int> InflateMask => new[]
        {
            0x00000000, 0x00000001, 0x00000003, 0x00000007, 0x0000000f, 0x0000001f,
            0x0000003f, 0x0000007f, 0x000000ff, 0x000001ff, 0x000003ff, 0x000007ff,
            0x00000fff, 0x00001fff, 0x00003fff, 0x00007fff, 0x0000ffff,
        };

        internal static void Free()
        {
            // nothing.
        }

        // Called with number of bytes left to write in window at least 258
        // (the maximum string length) and number of input bytes available
        // at least ten.  The ten bytes are six bytes for the longest length/
        // distance pair plus four bytes for overloading the bit buffer.
        internal static ZlibCompressionState Inflate_fast(int bl, int bd, int[] tl, int tlIndex, int[] td, int tdIndex, InfBlocks s, ZlibStream z)
        {
            int c; // bytes to copy

            // load input, output, bit values
            var p = z.NextInIndex;
            var n = z.AvailIn;
            var b = s.Bitb;
            var k = s.Bitk;
            var q = s.Write;
            var m = q < s.Read ? s.Read - q - 1 : s.End - q;

            // initialize masks
            var ml = InflateMask[bl];
            var md = InflateMask[bd];

            // do until not enough input or output space for fast loop
            do
            {
                // assume called with m >= 258 && n >= 10
                // get literal/length code
                while (k < 20)
                {
                    // max bits for literal/length code
                    n--;
                    b |= (z.NextIn[p++] & 0xff) << k;
                    k += 8;
                }

                var t = b & ml; // temporary pointer
                var tp = tl; // temporary pointer
                var tpIndex = tlIndex; // temporary pointer
                int e; // extra bits or operation
                if ((e = tp[(tpIndex + t) * 3]) == 0)
                {
                    b >>= tp[((tpIndex + t) * 3) + 1];
                    k -= tp[((tpIndex + t) * 3) + 1];
                    s.Window[q++] = (byte)tp[((tpIndex + t) * 3) + 2];
                    m--;
                    continue;
                }

                do
                {
                    b >>= tp[((tpIndex + t) * 3) + 1];
                    k -= tp[((tpIndex + t) * 3) + 1];
                    if ((e & 16) != 0)
                    {
                        e &= 15;
                        c = tp[((tpIndex + t) * 3) + 2] + (b & InflateMask[e]);
                        b >>= e;
                        k -= e;

                        // decode distance base of block to copy
                        while (k < 15)
                        {
                            // max bits for distance code
                            n--;
                            b |= (z.NextIn[p++] & 0xff) << k;
                            k += 8;
                        }

                        t = b & md;
                        tp = td;
                        tpIndex = tdIndex;
                        e = tp[(tpIndex + t) * 3];
                        do
                        {
                            b >>= tp[((tpIndex + t) * 3) + 1];
                            k -= tp[((tpIndex + t) * 3) + 1];
                            if ((e & 16) != 0)
                            {
                                // get extra bits to add to distance base
                                e &= 15;
                                while (k < e)
                                {
                                    // get extra bits (up to 13)
                                    n--;
                                    b |= (z.NextIn[p++] & 0xff) << k;
                                    k += 8;
                                }

                                var d = tp[((tpIndex + t) * 3) + 2] + (b & InflateMask[e]); // distance back to copy from
                                b >>= e;
                                k -= e;

                                // do the copy
                                m -= c;
                                int r; // copy source pointer
                                if (q >= d)
                                {
                                    // offset before dest
                                    //  just copy
                                    r = q - d;
                                    if (q - r is > 0 and < 2)
                                    {
                                        s.Window[q++] = s.Window[r++];
                                        c--; // minimum count is three,
                                        s.Window[q++] = s.Window[r++];
                                        c--; // so unroll loop a little
                                    }
                                    else
                                    {
                                        Array.Copy(s.Window, r, s.Window, q, 2);
                                        q += 2;
                                        r += 2;
                                        c -= 2;
                                    }
                                }
                                else
                                {
                                    // else offset after destination
                                    r = q - d;
                                    do
                                    {
                                        r += s.End; // force pointer in window
                                    }
                                    while (r < 0); // covers invalid distances

                                    e = s.End - r;
                                    if (c > e)
                                    {
                                        // if source crosses,
                                        c -= e; // wrapped copy
                                        if (q - r > 0 && e > q - r)
                                        {
                                            do
                                            {
                                                s.Window[q++] = s.Window[r++];
                                            }
                                            while (--e != 0);
                                        }
                                        else
                                        {
                                            Array.Copy(s.Window, r, s.Window, q, e);
                                            q += e;
                                        }

                                        r = 0; // copy rest from start of window
                                    }
                                }

                                // copy all or what's left
                                if (q - r > 0 && c > q - r)
                                {
                                    do
                                    {
                                        s.Window[q++] = s.Window[r++];
                                    }
                                    while (--c != 0);
                                }
                                else
                                {
                                    Array.Copy(s.Window, r, s.Window, q, c);
                                    q += c;
                                }

                                break;
                            }
                            else if ((e & 64) == 0)
                            {
                                t += tp[((tpIndex + t) * 3) + 2];
                                t += b & InflateMask[e];
                                e = tp[(tpIndex + t) * 3];
                            }
                            else
                            {
                                z.Msg = "invalid distance code";
                                c = z.AvailIn - n;
                                c = k >> 3 < c ? k >> 3 : c;
                                n += c;
                                p -= c;
                                k -= c << 3;
                                s.Bitb = b;
                                s.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                s.Write = q;
                                return ZlibCompressionState.DataError;
                            }
                        }
                        while (true);

                        break;
                    }

                    if ((e & 64) == 0)
                    {
                        t += tp[((tpIndex + t) * 3) + 2];
                        t += b & InflateMask[e];
                        if ((e = tp[(tpIndex + t) * 3]) == 0)
                        {
                            b >>= tp[((tpIndex + t) * 3) + 1];
                            k -= tp[((tpIndex + t) * 3) + 1];
                            s.Window[q++] = (byte)tp[((tpIndex + t) * 3) + 2];
                            m--;
                            break;
                        }
                    }
                    else if ((e & 32) != 0)
                    {
                        c = z.AvailIn - n;
                        c = k >> 3 < c ? k >> 3 : c;
                        n += c;
                        p -= c;
                        k -= c << 3;
                        s.Bitb = b;
                        s.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        s.Write = q;
                        return ZlibCompressionState.StreamEnd;
                    }
                    else
                    {
                        z.Msg = "invalid literal/length code";
                        c = z.AvailIn - n;
                        c = k >> 3 < c ? k >> 3 : c;
                        n += c;
                        p -= c;
                        k -= c << 3;
                        s.Bitb = b;
                        s.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        s.Write = q;
                        return ZlibCompressionState.DataError;
                    }
                }
                while (true);
            }
            while (m >= 258 && n >= 10);

            // not enough input or output--restore pointers and return
            c = z.AvailIn - n;
            c = k >> 3 < c ? k >> 3 : c;
            n += c;
            p -= c;
            k -= c << 3;
            s.Bitb = b;
            s.Bitk = k;
            z.AvailIn = n;
            z.TotalIn += p - z.NextInIndex;
            z.NextInIndex = p;
            s.Write = q;
            return ZlibCompressionState.Ok;
        }

        internal ZlibCompressionState Proc(InfBlocks s, ZlibStream z, ZlibCompressionState r)
        {
            // copy input/output information to locals (UPDATE macro restores)
            var p = z.NextInIndex; // input data pointer
            var n = z.AvailIn; // bytes available there
            var b = s.Bitb; // bit buffer
            var k = s.Bitk; // bits in bit buffer
            var q = s.Write; // output window write pointer
            var m = q < s.Read ? s.Read - q - 1 : s.End - q; // bytes to end of window or read pointer

            // process input and output based on current state
            while (true)
            {
                int j; // temporary storage
                int tindex; // temporary pointer
                int e; // extra bits or operation
                switch (this.Mode)
                {
                    // waiting for "i:"=input, "o:"=output, "x:"=nothing
                    case 0: // x: set up for LEN
                    {
                        if (m >= 258 && n >= 10)
                        {
                            s.Bitb = b;
                            s.Bitk = k;
                            z.AvailIn = n;
                            z.TotalIn += p - z.NextInIndex;
                            z.NextInIndex = p;
                            s.Write = q;
                            r = Inflate_fast(this.Lbits, this.Dbits, this.Ltree, this.LtreeIndex, this.Dtree, this.DtreeIndex, s, z);
                            p = z.NextInIndex;
                            n = z.AvailIn;
                            b = s.Bitb;
                            k = s.Bitk;
                            q = s.Write;
                            m = q < s.Read ? s.Read - q - 1 : s.End - q;
                            if (r != ZlibCompressionState.Ok)
                            {
                                this.Mode = r == ZlibCompressionState.StreamEnd ? 7 : 9;
                                break;
                            }
                        }

                        this.Need = this.Lbits;
                        this.Tree = this.Ltree;
                        this.TreeIndex = this.LtreeIndex;
                        this.Mode = 1;
                        break;
                    }

                    case 1: // i: get length/literal/eob next
                    {
                        j = this.Need;
                        while (k < j)
                        {
                            if (n != 0)
                            {
                                r = ZlibCompressionState.Ok;
                            }
                            else
                            {
                                s.Bitb = b;
                                s.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                s.Write = q;
                                return s.Inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.NextIn[p++] & 0xff) << k;
                            k += 8;
                        }

                        tindex = (this.TreeIndex + (b & InflateMask[j])) * 3;
                        b = b >= 0 ? b >> this.Tree[tindex + 1] : (b >> this.Tree[tindex + 1]) + (2 << ~this.Tree[tindex + 1]);
                        k -= this.Tree[tindex + 1];
                        e = this.Tree[tindex];
                        if (e == 0)
                        {
                            // literal
                            this.Lit = this.Tree[tindex + 2];
                            this.Mode = 6;
                            break;
                        }

                        if ((e & 16) != 0)
                        {
                            // length
                            this.GetRenamed = e & 15;
                            this.Len = this.Tree[tindex + 2];
                            this.Mode = 2;
                            break;
                        }

                        if ((e & 64) == 0)
                        {
                            // next table
                            this.Need = e;
                            this.TreeIndex = (tindex / 3) + this.Tree[tindex + 2];
                            break;
                        }

                        if ((e & 32) != 0)
                        {
                            // end of block
                            this.Mode = 7;
                            break;
                        }

                        this.Mode = 9; // invalid code
                        z.Msg = "invalid literal/length code";
                        r = ZlibCompressionState.DataError;
                        s.Bitb = b;
                        s.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        s.Write = q;
                        return s.Inflate_flush(z, r);
                    }

                    case 2: // i: getting length extra (have base)
                    {
                        j = this.GetRenamed;
                        while (k < j)
                        {
                            if (n != 0)
                            {
                                r = ZlibCompressionState.Ok;
                            }
                            else
                            {
                                s.Bitb = b;
                                s.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                s.Write = q;
                                return s.Inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.NextIn[p++] & 0xff) << k;
                            k += 8;
                        }

                        this.Len += b & InflateMask[j];
                        b >>= j;
                        k -= j;
                        this.Need = this.Dbits;
                        this.Tree = this.Dtree;
                        this.TreeIndex = this.DtreeIndex;
                        this.Mode = 3;
                        break;
                    }

                    case 3: // i: get distance next
                    {
                        j = this.Need;
                        while (k < j)
                        {
                            if (n != 0)
                            {
                                r = ZlibCompressionState.Ok;
                            }
                            else
                            {
                                s.Bitb = b;
                                s.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                s.Write = q;
                                return s.Inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.NextIn[p++] & 0xff) << k;
                            k += 8;
                        }

                        tindex = (this.TreeIndex + (b & InflateMask[j])) * 3;
                        b >>= this.Tree[tindex + 1];
                        k -= this.Tree[tindex + 1];
                        e = this.Tree[tindex];
                        if ((e & 16) != 0)
                        {
                            // distance
                            this.GetRenamed = e & 15;
                            this.Dist = this.Tree[tindex + 2];
                            this.Mode = 4;
                            break;
                        }

                        if ((e & 64) == 0)
                        {
                            // next table
                            this.Need = e;
                            this.TreeIndex = (tindex / 3) + this.Tree[tindex + 2];
                            break;
                        }

                        this.Mode = 9; // invalid code
                        z.Msg = "invalid distance code";
                        r = ZlibCompressionState.DataError;
                        s.Bitb = b;
                        s.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        s.Write = q;
                        return s.Inflate_flush(z, r);
                    }

                    case 4: // i: getting distance extra
                    {
                        j = this.GetRenamed;
                        while (k < j)
                        {
                            if (n != 0)
                            {
                                r = ZlibCompressionState.Ok;
                            }
                            else
                            {
                                s.Bitb = b;
                                s.Bitk = k;
                                z.AvailIn = n;
                                z.TotalIn += p - z.NextInIndex;
                                z.NextInIndex = p;
                                s.Write = q;
                                return s.Inflate_flush(z, r);
                            }

                            n--;
                            b |= (z.NextIn[p++] & 0xff) << k;
                            k += 8;
                        }

                        this.Dist += b & InflateMask[j];
                        b >>= j;
                        k -= j;
                        this.Mode = 5;
                        break;
                    }

                    case 5: // o: copying bytes in window, waiting for space
                    {
                        var f = q - this.Dist; // pointer to copy strings from
                        while (f < 0)
                        {
                            // modulo window size-"while" instead
                            f += s.End; // of "if" handles invalid distances
                        }

                        while (this.Len != 0)
                        {
                            if (m == 0)
                            {
                                if (q == s.End && s.Read != 0)
                                {
                                    q = 0;
                                    m = q < s.Read ? s.Read - q - 1 : s.End - q;
                                }

                                if (m == 0)
                                {
                                    s.Write = q;
                                    r = s.Inflate_flush(z, r);
                                    q = s.Write;
                                    m = q < s.Read ? s.Read - q - 1 : s.End - q;
                                    if (q == s.End && s.Read != 0)
                                    {
                                        q = 0;
                                        m = q < s.Read ? s.Read - q - 1 : s.End - q;
                                    }

                                    if (m == 0)
                                    {
                                        s.Bitb = b;
                                        s.Bitk = k;
                                        z.AvailIn = n;
                                        z.TotalIn += p - z.NextInIndex;
                                        z.NextInIndex = p;
                                        s.Write = q;
                                        return s.Inflate_flush(z, r);
                                    }
                                }
                            }

                            s.Window[q++] = s.Window[f++];
                            m--;
                            if (f == s.End)
                            {
                                f = 0;
                            }

                            this.Len--;
                        }

                        this.Mode = 0;
                        break;
                    }

                    case 6: // o: got literal, waiting for output space
                    {
                        if (m == 0)
                        {
                            if (q == s.End && s.Read != 0)
                            {
                                q = 0;
                                m = q < s.Read ? s.Read - q - 1 : s.End - q;
                            }

                            if (m == 0)
                            {
                                s.Write = q;
                                r = s.Inflate_flush(z, r);
                                q = s.Write;
                                m = q < s.Read ? s.Read - q - 1 : s.End - q;
                                if (q == s.End && s.Read != 0)
                                {
                                    q = 0;
                                    m = q < s.Read ? s.Read - q - 1 : s.End - q;
                                }

                                if (m == 0)
                                {
                                    s.Bitb = b;
                                    s.Bitk = k;
                                    z.AvailIn = n;
                                    z.TotalIn += p - z.NextInIndex;
                                    z.NextInIndex = p;
                                    s.Write = q;
                                    return s.Inflate_flush(z, r);
                                }
                            }
                        }

                        r = ZlibCompressionState.Ok;
                        s.Window[q++] = (byte)this.Lit;
                        m--;
                        this.Mode = 0;
                        break;
                    }

                    case 7: // o: got eob, possibly more output
                    {
                        if (k > 7)
                        {
                            // return unused byte, if any
                            k -= 8;
                            n++;
                            p--; // can always return one
                        }

                        s.Write = q;
                        r = s.Inflate_flush(z, r);
                        q = s.Write;
                        m = q < s.Read ? s.Read - q - 1 : s.End - q;
                        if (s.Read != s.Write)
                        {
                            s.Bitb = b;
                            s.Bitk = k;
                            z.AvailIn = n;
                            z.TotalIn += p - z.NextInIndex;
                            z.NextInIndex = p;
                            s.Write = q;
                            return s.Inflate_flush(z, r);
                        }

                        this.Mode = 8;
                        break;
                    }

                    case 8:
                    {
                        r = ZlibCompressionState.StreamEnd;
                        s.Bitb = b;
                        s.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        s.Write = q;
                        return s.Inflate_flush(z, r);
                    }

                    case 9: // x: got error
                    {
                        r = ZlibCompressionState.DataError;
                        s.Bitb = b;
                        s.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        s.Write = q;
                        return s.Inflate_flush(z, r);
                    }

                    default:
                    {
                        r = ZlibCompressionState.StreamError;
                        s.Bitb = b;
                        s.Bitk = k;
                        z.AvailIn = n;
                        z.TotalIn += p - z.NextInIndex;
                        z.NextInIndex = p;
                        s.Write = q;
                        return s.Inflate_flush(z, r);
                    }
                }
            }
        }
    }
}
