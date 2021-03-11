// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;
    using System.IO;

    /// <summary>
    /// Zlib Memory Compression and Decompression Helper Class.
    /// </summary>
    public static class MemoryZlib
    {
        /// <summary>
        /// Compresses data using the default compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// The compressed data.
        /// </returns>
        public static byte[] Compress(byte[] inData)
            => Compress(inData, ZlibCompression.ZDEFAULTCOMPRESSION);

        /// <summary>
        /// Compresses a file using the default compression level.
        /// </summary>
        /// <param name="path">The file to compress.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// The compressed data.
        /// </returns>
        public static byte[] Compress(string path)
            => Compress(path, ZlibCompression.ZDEFAULTCOMPRESSION);

        /// <summary>
        /// Compresses data using an specific compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// The compressed data.
        /// </returns>
        // discard returned adler32. The caller does not want it.
        public static byte[] Compress(byte[] inData, ZlibCompression level)
            => CompressHash(inData, level).OutData;

        /// <summary>
        /// Compresses a file using the default compression level.
        /// </summary>
        /// <param name="path">The file to compress.</param>
        /// <param name="level">The compression level to use.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// The compressed data.
        /// </returns>
        // discard returned adler32. The caller does not want it.
        public static byte[] Compress(string path, ZlibCompression level)
            => CompressHash(path, level).OutData;

        /// <summary>
        /// Compresses data using the default compression level and outputs an adler32 hash with the data.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// A <see cref="ValueTuple"/> containing the compressed data, as well as the adler32 hash of that data.
        /// </returns>
        public static (byte[] OutData, uint Adler32) CompressHash(byte[] inData)
            => CompressHash(inData, ZlibCompression.ZDEFAULTCOMPRESSION);

        /// <summary>
        /// Compresses a file using the default compression level and outputs an adler32 hash with the data.
        /// </summary>
        /// <param name="path">The file to compress.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// A <see cref="ValueTuple"/> containing the compressed data, as well as the adler32 hash of that data.
        /// </returns>
        public static (byte[] OutData, uint Adler32) CompressHash(string path)
            => CompressHash(File.ReadAllBytes(path), ZlibCompression.ZDEFAULTCOMPRESSION);

        /// <summary>
        /// Compresses data using an specific compression level and outputs an adler32 hash with the data.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outStream">The compressed output data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// The adler32 hash of the compressed data.
        /// </returns>
        public static uint CompressHash(byte[] inData, Stream outStream, ZlibCompression level)
        {
            try
            {
                using var tmpStrm = new MemoryStream(inData);
                using var outZStream = new ZlibStream(outStream, level, true);
                tmpStrm.CopyTo(outZStream);
                outZStream.Flush();
                outZStream.Finish();
                return (uint)(outZStream.GetAdler32() & 0xffff);
            }
            catch (NotPackableException ex)
            {
                throw new NotPackableException("Compression Failed.", ex);
            }
            catch (NotSupportedException ex)
            {
                // the compression failed because of a support failure.
                throw new NotPackableException("Compression Failed.", ex);
            }
            catch (StackOverflowException ex)
            {
                throw new NotPackableException("Compression Failed due to a stack overflow.", ex);
            }
            catch (IOException ex) when (ex is not NotPackableException)
            {
                throw new NotPackableException("Compression Failed.", ex);
            }
        }

        /// <summary>
        /// Compresses data using an specific compression level and outputs an adler32 hash with the data.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// A <see cref="ValueTuple"/> containing the compressed data, as well as the adler32 hash of that data.
        /// </returns>
        public static (byte[] OutData, uint Adler32) CompressHash(byte[] inData, ZlibCompression level)
        {
            using var outMemoryStream = new MemoryStream();
            var result = CompressHash(inData, outMemoryStream, level);
            return (outMemoryStream.ToArray(), result);
        }

        /// <summary>
        /// Compresses a file using an specific compression level and outputs an adler32 hash with the data.
        /// </summary>
        /// <param name="path">The file to compress.</param>
        /// <param name="level">The compression level to use.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        /// <returns>
        /// A <see cref="ValueTuple"/> containing the compressed data, as well as the adler32 hash of that data.
        /// </returns>
        public static (byte[] OutData, uint Adler32) CompressHash(string path, ZlibCompression level)
            => CompressHash(File.ReadAllBytes(path), level);

        /// <summary>
        /// Decompresses data.
        /// </summary>
        /// <param name="inData">The compressed input data.</param>
        /// <param name="outStream">The decompressed output data.</param>
        /// <exception cref="NotUnpackableException">
        /// Thrown when the internal decompression stream errors in any way.
        /// </exception>
        public static void Decompress(byte[] inData, Stream outStream)
        {
            try
            {
                using var outZStream = new ZlibStream(inData);
                outZStream.CopyTo(outStream);
                outZStream.Flush();
                outZStream.Finish();
            }
            catch (NotUnpackableException ex)
            {
                throw new NotUnpackableException("Decompression Failed.", ex);
            }
            catch (StackOverflowException ex)
            {
                throw new NotPackableException("Decompression Failed due to a stack overflow.", ex);
            }
            catch (NotSupportedException ex)
            {
                // the decompression failed because of a support failure.
                throw new NotPackableException("Compression Failed.", ex);
            }
            catch (IOException ex) when (ex is not NotUnpackableException)
            {
                throw new NotUnpackableException("Decompression Failed.", ex);
            }
        }

        /// <summary>
        /// Decompresses data.
        /// </summary>
        /// <param name="inData">The compressed input data.</param>
        /// <exception cref="NotUnpackableException">
        /// Thrown when the internal decompression stream errors in any way.
        /// </exception>
        /// <returns>
        /// The decompressed data.
        /// </returns>
        public static byte[] Decompress(byte[] inData)
        {
            using var outMemoryStream = new MemoryStream();
            Decompress(inData, outMemoryStream);
            return outMemoryStream.ToArray();
        }

        /// <summary>
        /// Decompresses a file.
        /// </summary>
        /// <param name="path">The file to decompress.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal decompression stream errors in any way.
        /// </exception>
        /// <returns>
        /// The decompressed data.
        /// </returns>
        public static byte[] Decompress(string path)
            => Decompress(File.ReadAllBytes(path));

        /// <summary>
        /// Check data for compression by zlib.
        /// </summary>
        /// <param name="stream">Input stream.</param>
        /// <returns>Returns <see langword="true" /> if data is compressed by zlib, else <see langword="false" />.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="stream"/> is <see langword="null" />.</exception>
        public static bool IsCompressedByZlib(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var data = new byte[2];
            _ = stream.Read(data, 0, 2);
            _ = stream.Seek(-2, SeekOrigin.Current);
            return IsCompressedByZlib(data);
        }

        /// <summary>
        /// Check data for compression by zlib.
        /// </summary>
        /// <param name="path">The file to check on if it is compressed by zlib.</param>
        /// <returns>Returns <see langword="true" /> if data is compressed by zlib, else <see langword="false" />.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="path"/> is <see langword="null" /> or <see cref="string.Empty"/>.</exception>
        public static bool IsCompressedByZlib(string path)
            => IsCompressedByZlib(File.ReadAllBytes(path));

        /// <summary>
        /// Check data for compression by zlib.
        /// </summary>
        /// <param name="data">Input array.</param>
        /// <returns>Returns <see langword="true" /> if data is compressed by zlib, else <see langword="false" />.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="data"/> is <see langword="null" />.</exception>
        public static bool IsCompressedByZlib(byte[] data)
            => data == null
            ? throw new ArgumentNullException(nameof(data))
            : data.Length >= 2 && data[0] == 0x78 && (data[1] == 0x01 || data[1] == 0x5E || data[1] == 0x9C || data[1] == 0xDA);

        // NEW: Zlib version check.

        /// <summary>
        /// Gets the version to zlib.managed.
        /// </summary>
        /// <returns>The version string to this version of zlib.managed.</returns>
        public static string ZlibVersion()
            => typeof(MemoryZlib).Assembly.GetName().Version.ToString(3);

        // NEW: Adler32 hasher.

        /// <summary>
        /// Gets the Adler32 checksum of the input data at the specified index and length.
        /// </summary>
        /// <param name="data">The data to checksum.</param>
        /// <param name="index">The index of which to checksum.</param>
        /// <param name="length">The length of the data to checksum.</param>
        /// <returns>The Adler32 hash of the input data.</returns>
        public static long ZlibGetAdler32(byte[] data, int index, int length)
            => Adler32.Calculate(Adler32.Calculate(0, null, 0, 0), data, index, length);
    }
}
