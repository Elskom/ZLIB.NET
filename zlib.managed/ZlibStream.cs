// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;
    using System.IO;
    using IDisposableGenerator;

    /// <summary>
    /// Class that provides support for zlib compression/decompression for an input stream.
    /// This is an sealed class.
    /// </summary>
    [GenerateDispose(true)]
    public sealed partial class ZlibStream : Stream
    {
        private byte[] pBuf1;
        private byte[] pBuf;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibStream"/> class for compression.
        /// </summary>
        /// <param name="input">The input stream to decompress with.</param>
        public ZlibStream(Stream input)
            : this(input, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibStream"/> class for compression.
        /// </summary>
        /// <param name="input">The input stream to decompress with.</param>
        /// <param name="keepOpen">Whether to keep the input stream open or not.</param>
        public ZlibStream(Stream input, bool keepOpen)
        {
            this.BaseStream = input;
            this.KeepOpen = keepOpen;
            this.InitBlock();
            this.IState = new Inflate();
            _ = this.InflateInit();
            this.Compress = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibStream"/> class for compression.
        /// </summary>
        /// <param name="output">The output stream to compress to.</param>
        /// <param name="level">The compression level for the data to compress.</param>
        public ZlibStream(Stream output, ZlibCompression level)
            : this(output, level, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibStream"/> class for compression.
        /// </summary>
        /// <param name="output">The output stream to compress to.</param>
        /// <param name="level">The compression level for the data to compress.</param>
        /// <param name="keepOpen">Whether to keep the output stream open or not.</param>
        public ZlibStream(Stream output, ZlibCompression level, bool keepOpen)
        {
            this.BaseStream = output;
            this.KeepOpen = keepOpen;
            this.InitBlock();
            this.DState = new Deflate();
            _ = this.DeflateInit(level);
            this.Compress = true;
        }

        /// <summary>
        /// Gets a value indicating whether the stream is finished.
        /// </summary>
        public bool IsFinished { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether there is more input.
        /// </summary>
        public bool MoreInput { get; set; }

        /// <summary>
        /// Gets the total number of bytes input so far.
        /// </summary>
        public long TotalIn { get; internal set; }

        /// <summary>
        /// Gets the total number of bytes output so far.
        /// </summary>
        public long TotalOut { get; internal set; }

        /// <inheritdoc/>
        public override bool CanRead => this.BaseStream != null && !this.Compress && this.BaseStream.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => this.BaseStream != null && this.Compress && this.BaseStream.CanWrite;

        /// <inheritdoc/>
        public override long Length => this.BaseStream?.Length ?? throw new ObjectDisposedException(nameof(this.BaseStream));

        /// <inheritdoc/>
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        internal int BufSize => 8192;

        internal bool Compress { get; }

        internal ZlibFlushStrategy FlushMode { get; set; }

        [DisposeField(true)]
        internal Stream BaseStream { get; set; }

        internal long Adler { get; set; }

        internal string Msg { get; set; }

        internal int NextOutIndex { get; set; }

        internal int AvailOut { get; set; }

        internal byte[] NextIn { get; set; }

        internal byte[] NextOut { get; set; }

        internal int NextInIndex { get; set; }

        internal int AvailIn { get; set; }

        internal Deflate DState { get; set; }

        internal Inflate IState { get; set; }

        internal int DataType { get; set; } // best guess about the data type: ascii or binary

        /// <inheritdoc/>
        public override int ReadByte()
            => this.Read(this.pBuf1, 0, 1) == 0 ? -1 : this.pBuf1[0] & 0xFF;

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!this.CanRead)
            {
                throw new NotSupportedException(this.Compress ? "Read() should not be used to Compress. Use Write() instead. If decompressing is intended pass the input stream without an compression level argument." : "The stream cannot be Read.");
            }

            if (count == 0)
            {
                return 0;
            }

            ZlibCompressionState err;
            this.NextOut = buffer;
            this.NextOutIndex = offset;
            this.AvailOut = count;
            do
            {
                if (this.AvailIn == 0 && !this.MoreInput)
                {
                    // if buffer is empty and more input is available, refill it
                    this.NextInIndex = 0;
                    if (this.pBuf.Length == 0)
                    {
                        this.AvailIn = 0;
                    }

                    var bytesRead = this.BaseStream.Read(this.pBuf, 0, this.BufSize);
                    if (bytesRead == 0)
                    {
                        this.AvailIn = 0;
                        this.MoreInput = true;
                    }
                }

                err = this.Inflate(this.FlushMode);
                if (this.MoreInput && err == ZlibCompressionState.BufError)
                {
                    // we must always return 0 if nothing can be read (and when no exceptions are thrown) to not break any Async stream methods too.
                    // see issue: https://github.com/Elskom/zlib.managed/issues/136/ for more details.
                    return 0;
                }

                if (err is not ZlibCompressionState.Ok and not ZlibCompressionState.StreamEnd)
                {
                    throw new NotUnpackableException($"inflating: {this.Msg}");
                }

                if (this.MoreInput && this.AvailOut == count)
                {
                    // we must always return 0 if nothing can be read (and when no exceptions are thrown) to not break any Async stream methods too.
                    // see issue: https://github.com/Elskom/zlib.managed/issues/136/ for more details.
                    return 0;
                }
            }
            while (this.AvailOut > 0 && err == ZlibCompressionState.Ok);

            return count - this.AvailOut;
        }

        /// <inheritdoc/>
        public override void WriteByte(byte value)
            => this.WriteByte(value);

        /// <summary>
        /// Writes a byte to the current position in the stream and advances the position
        /// within the stream by one byte.
        /// </summary>
        /// <param name="value">
        /// The byte to write to the stream.
        /// </param>
        /// <exception cref="IOException">
        /// An I/O error occurs.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// The stream does not support writing, or the stream is already closed.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// Methods were called after the stream was closed.
        /// </exception>
        public void WriteByte(int value)
        {
            this.pBuf1[0] = (byte)value;
            this.Write(this.pBuf1, 0, 1);
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!this.CanWrite)
            {
                throw new NotSupportedException(!this.Compress ? "Write() should not be used to decompress. Use Read() instead. If Compressing is intended pass the output stream with a compression level argument." : "The stream cannot be Written to.");
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (count == 0)
            {
                return;
            }

            this.NextIn = buffer;
            this.NextInIndex = offset;
            this.AvailIn = count;
            do
            {
                this.NextOut = this.pBuf;
                this.NextOutIndex = 0;
                this.AvailOut = this.BufSize;
                var err = this.Deflate(this.FlushMode);
                if (err is not ZlibCompressionState.Ok and not ZlibCompressionState.StreamEnd)
                {
                    throw new NotPackableException($"deflating: {this.Msg}");
                }

                this.BaseStream.Write(this.pBuf, 0, this.BufSize - this.AvailOut);
                if (!this.Compress && this.AvailIn == 0 && this.AvailOut == 0)
                {
                    break;
                }

                if (err == ZlibCompressionState.StreamEnd)
                {
                    break;
                }
            }
            while (this.AvailIn > 0 || this.AvailOut == 0);
        }

        /// <inheritdoc/>
        public override void Flush()
            => this.BaseStream?.Flush();

        /// <summary>
        /// Finishes the stream.
        /// </summary>
        public void Finish()
        {
            if (!this.IsFinished)
            {
                do
                {
                    this.NextOut = this.pBuf;
                    this.NextOutIndex = 0;
                    this.AvailOut = this.BufSize;
                    var err = this.Compress ? this.Deflate(ZlibFlushStrategy.Finish) : this.Inflate(ZlibFlushStrategy.Finish);
                    if (err is not ZlibCompressionState.StreamEnd and not ZlibCompressionState.Ok)
                    {
                        if (this.Compress)
                        {
                            throw new NotPackableException($"deflating: {this.Msg}");
                        }
                        else
                        {
                            throw new NotUnpackableException($"inflating: {this.Msg}");
                        }
                    }

                    if (this.BufSize - this.AvailOut > 0)
                    {
                        this.BaseStream.Write(this.pBuf, 0, this.BufSize - this.AvailOut);
                    }

                    if (err == ZlibCompressionState.StreamEnd)
                    {
                        break;
                    }
                }
                while (this.AvailIn > 0 || this.AvailOut == 0);

                try
                {
                    this.Flush();
                }
                catch
                {
                    // ensure no throws on this.
                }

                this.IsFinished = true;
            }
        }

        /// <summary>
        /// Ends the compression or decompression on the stream.
        /// </summary>
        public void EndStream()
        {
            _ = this.Compress ? this.DeflateEnd() : this.InflateEnd();
            this.Free();
        }

        /// <summary>
        /// Gets the Adler32 hash of the stream's data.
        /// </summary>
        /// <returns>The Adler32 hash of the stream's data.</returns>
        public long GetAdler32()
            => this.Adler;

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
            => 0;

        /// <inheritdoc/>
        public override void SetLength(long value)
            => throw new NotSupportedException("Setting length is not supported.");

        // internal and private non-stream members.
        internal ZlibCompressionState InflateInit()
            => this.IState.InflateInit(this, 15); // 32K LZ77 window

        internal ZlibCompressionState Inflate(ZlibFlushStrategy f)
            => this.IState == null ? ZlibCompressionState.StreamError : Libs.Inflate.Decompress(this, f);

        internal ZlibCompressionState InflateEnd()
        {
            if (this.IState == null)
            {
                return ZlibCompressionState.StreamError;
            }

            var ret = this.IState.InflateEnd(this);
            this.IState = null;
            return ret;
        }

        internal ZlibCompressionState DeflateInit(ZlibCompression level)
            => this.DState.DeflateInit(this, level, 15); // 32K LZ77 window

        internal ZlibCompressionState Deflate(ZlibFlushStrategy flush)
            => this.DState?.Compress(this, flush) ?? ZlibCompressionState.StreamError;

        internal ZlibCompressionState DeflateEnd()
        {
            if (this.DState == null)
            {
                return ZlibCompressionState.StreamError;
            }

            var ret = this.DState.DeflateEnd();
            this.DState = null;
            return ret;
        }

        internal void Free()
        {
            this.NextIn = null;
            this.NextOut = null;
            this.Msg = null;

            // ensure the buffers always get cleared when disposing!!!
            this.pBuf = null;
            this.pBuf1 = null;
        }

        // Flush as much pending output as possible. All deflate() output goes
        // through this function so some applications may wish to modify it
        // to avoid allocating a large strm->next_out buffer and copying into it.
        // (See also read_buf()).
        internal void Flush_pending()
        {
            var len = this.DState.Pending;
            if (len > this.AvailOut)
            {
                len = this.AvailOut;
            }

            if (len == 0)
            {
                return;
            }

            Array.Copy(this.DState.PendingBuf, this.DState.PendingOut, this.NextOut, this.NextOutIndex, len);
            this.NextOutIndex += len;
            this.DState.PendingOut += len;
            this.TotalOut += len;
            this.AvailOut -= len;
            this.DState.Pending -= len;
            if (this.DState.Pending == 0)
            {
                this.DState.PendingOut = 0;
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
            if (this.DState.Noheader == 0)
            {
                this.Adler = Adler32.Calculate(this.Adler, this.NextIn, this.NextInIndex, len);
            }

            Array.Copy(this.NextIn, this.NextInIndex, buf, start, len);
            this.NextInIndex += len;
            this.TotalIn += len;
            return len;
        }

        [CallOnDispose]
        private void FinishAndEnd()
        {
            try
            {
                this.Finish();
            }
            catch
            {
                // should never throw.
            }
            finally
            {
                this.EndStream();
            }
        }

        private void InitBlock()
        {
            this.FlushMode = ZlibFlushStrategy.NoFlush;
            this.pBuf = new byte[this.BufSize];
            this.pBuf1 = new byte[1];
        }
    }
}
