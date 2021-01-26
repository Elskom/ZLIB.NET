// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;
    using System.IO;

    /// <summary>
    /// Class that provices a zlib output stream that supports
    /// compression and decompression.
    /// </summary>
    public class ZOutputStream : Stream
    {
        private readonly bool keepOpen;
        private readonly byte[] pBuf1 = new byte[1];
        private byte[] pBuf;
        private bool isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZOutputStream"/> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        public ZOutputStream(Stream output)
        {
            this.BaseStream = output;
            this.InitBlock();
            _ = this.Z.InflateInit();
            this.Compress = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZOutputStream"/> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="keepOpen">Optionally keep the output stream open when this stream is closed.</param>
        public ZOutputStream(Stream output, bool keepOpen)
            : this(output)
            => this.keepOpen = keepOpen;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZOutputStream"/> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="level">The compression level for the data to compress.</param>
        public ZOutputStream(Stream output, ZlibCompression level)
        {
            this.BaseStream = output;
            this.InitBlock();
            _ = this.Z.DeflateInit(level);
            this.Compress = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZOutputStream"/> class.
        /// </summary>
        /// <param name="output">The output stream.</param>
        /// <param name="level">The compression level for the data to compress.</param>
        /// <param name="keepOpen">Optionally keep the output stream open when this stream is closed.</param>
        public ZOutputStream(Stream output, ZlibCompression level, bool keepOpen)
            : this(output, level)
            => this.keepOpen = keepOpen;

        /// <summary>
        /// Gets the base stream that this stream contains.
        /// </summary>
        public Stream BaseStream { get; private set; }

        /// <summary>
        /// Gets the base zlib stream.
        /// </summary>
        public ZStream Z { get; private set; } = new ZStream();

        /// <summary>
        /// Gets a value indicating whether the stream is finished.
        /// </summary>
        public bool IsFinished { get; private set; }

        /// <summary>
        /// Gets or sets the flush mode for this stream.
        /// </summary>
        public virtual ZlibFlushStrategy FlushMode { get; set; }

        /// <summary>Gets the total number of bytes input so far.</summary>
        public virtual long TotalIn => this.Z.TotalIn;

        /// <summary>Gets the total number of bytes output so far.</summary>
        public virtual long TotalOut => this.Z.TotalOut;

        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override long Length => this.BaseStream.Length;

        /// <inheritdoc/>
        public override long Position { get => this.BaseStream.Position; set => this.BaseStream.Position = value; }

        /// <summary>
        /// Gets the stream's buffer size.
        /// </summary>
        protected internal int Bufsize { get; private set; } = 4096;

        /// <summary>
        /// Gets a value indicating whether this stream is setup for compression.
        /// </summary>
        protected internal bool Compress { get; private set; }

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
                err = this.Compress ? this.Z.Deflate(this.FlushMode) : this.Z.Inflate(this.FlushMode);
                if (err != ZlibCompressionState.ZOK && err != ZlibCompressionState.ZSTREAMEND)
                {
                    throw new ZStreamException((this.Compress ? "de" : "in") + "flating: " + this.Z.Msg);
                }

                this.BaseStream.Write(this.pBuf, 0, this.Bufsize - this.Z.AvailOut);
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
        public virtual void Finish()
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
                        throw new ZStreamException((this.Compress ? "de" : "in") + "flating: " + this.Z.Msg);
                    }

                    if (this.Bufsize - this.Z.AvailOut > 0)
                    {
                        this.BaseStream.Write(this.pBuf, 0, this.Bufsize - this.Z.AvailOut);
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
        public virtual void EndStream()
        {
            _ = this.Compress ? this.Z.DeflateEnd() : this.Z.InflateEnd();
            this.Z.Free();
            this.Z = null;
        }

        /// <inheritdoc/>
        public override void Flush()
            => this.BaseStream.Flush();

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
            => 0;

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
            => 0;

        /// <inheritdoc/>
        public override void SetLength(long value)
            => throw new NotImplementedException();

        /// <summary>
        /// Gets the stream's buffer.
        /// </summary>
        /// <returns>The stream's buffer.</returns>
        protected internal byte[] GetBuf()
            => this.pBuf;

        /// <summary>
        /// Gets the stream's single byte buffer value.
        /// For reading 1 byte at a time.
        /// </summary>
        /// <returns>The stream's single byte buffer value.</returns>
        protected internal byte[] GetBuf1()
            => this.pBuf1;

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
                if (!this.keepOpen)
                {
                    base.Dispose(disposing);
                }
            }
        }

        private void InitBlock()
        {
            this.FlushMode = ZlibFlushStrategy.ZNOFLUSH;
            this.pBuf = new byte[this.Bufsize];
        }
    }
}
