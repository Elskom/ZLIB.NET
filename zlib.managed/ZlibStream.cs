// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;
    using System.IO;

    /// <summary>
    /// Class that provides support for zlib compression/decompression for an input stream.
    /// This is an sealed class.
    /// </summary>
    public sealed class ZlibStream : MemoryStream
    {
        private readonly byte[] pBuf1 = new byte[1];
        private byte[] pBuf;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibStream"/> class for decompression.
        /// </summary>
        /// <param name="input">The input data.</param>
        public ZlibStream(byte[] input)
            : base(input)
        {
            this.InitBlock();
            _ = this.Z.InflateInit();
            this.Compress = false;
        }

        // cannot input data as this is to decompress (the data would come in from the Write() method).

        /// <summary>
        /// Initializes a new instance of the <see cref="ZlibStream"/> class for compression.
        /// </summary>
        /// <param name="level">The compression level for the data to compress.</param>
        public ZlibStream(ZlibCompression level)
            : base()
        {
            this.InitBlock();
            _ = this.Z.DeflateInit(level);
            this.Compress = true;
        }

        /// <summary>
        /// Gets a value indicating whether the stream is finished.
        /// </summary>
        public bool IsFinished { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether there is more input.
        /// </summary>
        public bool Moreinput { get; set; }

        /// <summary>Gets the total number of bytes input so far.</summary>
        public long TotalIn => this.Z.TotalIn;

        /// <summary>Gets the total number of bytes output so far.</summary>
        public long TotalOut => this.Z.TotalOut;

        /// <inheritdoc/>
        public override bool CanRead => !this.Compress && base.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => this.Compress && base.CanWrite;

        internal ZStream Z { get; private set; } = new ZStream();

        internal int Bufsize { get; private set; } = 8192;

        internal bool Compress { get; private set; }

        internal ZlibFlushStrategy FlushMode { get; set; }

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
            this.Z.INextOut = buffer;
            this.Z.NextOutIndex = offset;
            this.Z.AvailOut = count;
            do
            {
                if (this.Z.AvailIn == 0 && !this.Moreinput)
                {
                    // if buffer is empty and more input is avaiable, refill it
                    this.Z.NextInIndex = 0;
                    if (this.pBuf.Length == 0)
                    {
                        this.Z.AvailIn = 0;
                    }

                    var receiver = new byte[this.pBuf.Length];
                    var bytesRead = base.Read(receiver, 0, this.Bufsize);
                    if (bytesRead == 0)
                    {
                        this.Z.AvailIn = -1;
                    }

                    for (var i = 0; i < 0 + bytesRead; i++)
                    {
                        this.pBuf[i] = receiver[i];
                    }

                    if (this.Z.AvailIn == -1)
                    {
                        this.Z.AvailIn = 0;
                        this.Moreinput = true;
                    }
                }

                err = this.Z.Inflate(this.FlushMode);
                if (this.Moreinput && err == ZlibCompressionState.ZBUFERROR)
                {
                    // we must always return 0 if nothing can be read (and when no exceptions are thrown) to not break any Async stream methods too.
                    // see issue: https://github.com/Elskom/zlib.managed/issues/136/ for more details.
                    return 0;
                }

                if (err != ZlibCompressionState.ZOK && err != ZlibCompressionState.ZSTREAMEND)
                {
                    throw new NotUnpackableException($"inflating: {this.Z.Msg}");
                }

                if (this.Moreinput && this.Z.AvailOut == count)
                {
                    // we must always return 0 if nothing can be read (and when no exceptions are thrown) to not break any Async stream methods too.
                    // see issue: https://github.com/Elskom/zlib.managed/issues/136/ for more details.
                    return 0;
                }
            }
            while (this.Z.AvailOut > 0 && err == ZlibCompressionState.ZOK);

            return count - this.Z.AvailOut;
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

            ZlibCompressionState err;
            var b = new byte[buffer.Length];
            Array.Copy(buffer, 0, b, 0, buffer.Length);
            this.Z.INextIn = b;
            this.Z.NextInIndex = offset;
            this.Z.AvailIn = count;
            do
            {
                this.Z.INextOut = this.pBuf;
                this.Z.NextOutIndex = 0;
                this.Z.AvailOut = this.Bufsize;
                err = this.Z.Deflate(this.FlushMode);
                if (err != ZlibCompressionState.ZOK && err != ZlibCompressionState.ZSTREAMEND)
                {
                    throw new NotPackableException($"deflating: {this.Z.Msg}");
                }

                /*this.BaseStream*/base.Write(this.pBuf, 0, this.Bufsize - this.Z.AvailOut);
                if (!this.Compress && this.Z.AvailIn == 0 && this.Z.AvailOut == 0)
                {
                    break;
                }

                if (err == ZlibCompressionState.ZSTREAMEND)
                {
                    break;
                }
            }
            while (this.Z.AvailIn > 0 || this.Z.AvailOut == 0);
        }

        /// <summary>
        /// Finishes the stream.
        /// </summary>
        public void Finish()
        {
            if (!this.IsFinished)
            {
                ZlibCompressionState err;
                do
                {
                    this.Z.INextOut = this.pBuf;
                    this.Z.NextOutIndex = 0;
                    this.Z.AvailOut = this.Bufsize;
                    err = this.Compress ? this.Z.Deflate(ZlibFlushStrategy.ZFINISH) : this.Z.Inflate(ZlibFlushStrategy.ZFINISH);
                    if (err != ZlibCompressionState.ZSTREAMEND && err != ZlibCompressionState.ZOK)
                    {
                        if (this.Compress)
                        {
                            throw new NotPackableException($"deflating: {this.Z.Msg}");
                        }
                        else
                        {
                            throw new NotUnpackableException($"inflating: {this.Z.Msg}");
                        }
                    }

                    if (this.Bufsize - this.Z.AvailOut > 0)
                    {
                        base.Write(this.pBuf, 0, this.Bufsize - this.Z.AvailOut);
                    }

                    if (err == ZlibCompressionState.ZSTREAMEND)
                    {
                        break;
                    }
                }
                while (this.Z.AvailIn > 0 || this.Z.AvailOut == 0);

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
            _ = this.Compress ? this.Z.DeflateEnd() : this.Z.InflateEnd();
            this.Z.Free();
            this.Z = null;
        }

        /// <summary>
        /// Gets the Adler32 hash of the stream's data.
        /// </summary>
        /// <returns>The Adler32 hash of the stream's data.</returns>
        public long GetAdler32()
            => this.Z.Adler;

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin loc)
            => 0;

        /// <inheritdoc/>
        public override void SetLength(long value)
            => throw new NotSupportedException("Setting length is not supported.");

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    try
                    {
                        try
                        {
                            this.Finish();
                        }
                        catch
                        {
                            // should never throw.
                        }
                    }
                    finally
                    {
                        this.EndStream();
                    }
                }

                this.isDisposed = true;
                base.Dispose(disposing);
            }
        }

        private void InitBlock()
        {
            this.FlushMode = ZlibFlushStrategy.ZNOFLUSH;
            this.pBuf = new byte[this.Bufsize];
        }
    }
}
