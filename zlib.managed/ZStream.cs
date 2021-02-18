// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    internal sealed class ZStream
    {
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Only way this library works with this.")]
        internal byte[] INextIn;
        [SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1401:Fields should be private", Justification = "Only way this library works with this.")]
        internal byte[] INextOut;
        private const int MAXWBITS = 15; // 32K LZ77 window
        private const int DEFWBITS = MAXWBITS;

        internal int NextInIndex { get; set; }

        internal int AvailIn { get; set; }

        internal long TotalIn { get; set; }

        internal int NextOutIndex { get; set; }

        internal int AvailOut { get; set; }

        internal long TotalOut { get; set; }

        internal string Msg { get; set; }

        internal long Adler { get; set; }

        internal Deflate Dstate { get; set; }

        internal Inflate Istate { get; private set; }

        internal int DataType { get; set; } // best guess about the data type: ascii or binary

        internal ZlibCompressionState InflateInit()
            => this.InflateInit(DEFWBITS);

        internal ZlibCompressionState InflateInit(int w)
        {
            this.Istate = new Inflate();
            return this.Istate.InflateInit(this, w);
        }

        internal ZlibCompressionState Inflate(ZlibFlushStrategy f)
            => this.Istate == null ? ZlibCompressionState.ZSTREAMERROR : Libs.Inflate.Decompress(this, f);

        internal ZlibCompressionState InflateEnd()
        {
            if (this.Istate == null)
            {
                return ZlibCompressionState.ZSTREAMERROR;
            }

            var ret = this.Istate.InflateEnd(this);
            this.Istate = null;
            return ret;
        }

        internal ZlibCompressionState DeflateInit(ZlibCompression level)
            => this.DeflateInit(level, MAXWBITS);

        internal ZlibCompressionState DeflateInit(ZlibCompression level, int bits)
        {
            this.Dstate = new Deflate();
            return this.Dstate.DeflateInit(this, level, bits);
        }

        internal ZlibCompressionState Deflate(ZlibFlushStrategy flush)
            => this.Dstate == null ? ZlibCompressionState.ZSTREAMERROR : this.Dstate.Compress(this, flush);

        internal ZlibCompressionState DeflateEnd()
        {
            if (this.Dstate == null)
            {
                return ZlibCompressionState.ZSTREAMERROR;
            }

            var ret = this.Dstate.DeflateEnd();
            this.Dstate = null;
            return ret;
        }

        internal void Free()
        {
            this.INextIn = null;
            this.INextOut = null;
            this.Msg = null;
        }

        // Flush as much pending output as possible. All deflate() output goes
        // through this function so some applications may wish to modify it
        // to avoid allocating a large strm->next_out buffer and copying into it.
        // (See also read_buf()).
        internal void Flush_pending()
        {
            var len = this.Dstate.Pending;
            if (len > this.AvailOut)
            {
                len = this.AvailOut;
            }

            if (len == 0)
            {
                return;
            }

            Array.Copy(this.Dstate.PendingBuf, this.Dstate.PendingOut, this.INextOut, this.NextOutIndex, len);
            this.NextOutIndex += len;
            this.Dstate.PendingOut += len;
            this.TotalOut += len;
            this.AvailOut -= len;
            this.Dstate.Pending -= len;
            if (this.Dstate.Pending == 0)
            {
                this.Dstate.PendingOut = 0;
            }
        }

        // Read a new buffer from the current input stream, update the adler32
        // and total number of bytes read.  All deflate() input goes through
        // this function so some applications may wish to modify it to avoid
        // allocating a large strm->next_in buffer and copying from it.
        // (See also flush_pending()).
        internal int Read_buf(byte[] buf, int start, int size)
        {
            var len = this.AvailIn;
            if (len > size)
            {
                len = size;
            }

            if (len == 0)
            {
                return 0;
            }

            this.AvailIn -= len;
            if (this.Dstate.Noheader == 0)
            {
                this.Adler = Adler32.Calculate(this.Adler, this.INextIn, this.NextInIndex, len);
            }

            Array.Copy(this.INextIn, this.NextInIndex, buf, start, len);
            this.NextInIndex += len;
            this.TotalIn += len;
            return len;
        }
    }
}
