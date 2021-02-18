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
        /// <param name="outData">The compressed output data.</param>
        /// <param name="adler32">The output adler32 of the data.</param>
        /// <exception cref="NotPackableException">Thrown when the stream Errors in any way.</exception>
        [Obsolete("Use MemoryZlib.Compress(byte[], out byte[], out int) instead. This will be removed in a future release.")]
        public static void CompressData(byte[] inData, out byte[] outData, out uint adler32)
            => Compress(inData, out outData, out adler32);

        /// <summary>
        /// Compresses data using the default compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <exception cref="NotPackableException">Thrown when the stream Errors in any way.</exception>
        [Obsolete("Use MemoryZlib.Compress(byte[], out byte[]) instead. This will be removed in a future release.")]
        public static void CompressData(byte[] inData, out byte[] outData)
            => Compress(inData, out outData);

        /// <summary>
        /// Compresses data using an specific compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <exception cref="NotPackableException">Thrown when the stream Errors in any way.</exception>
        [Obsolete("Use MemoryZlib.Compress(byte[], out byte[], ZlibCompression) instead. This will be removed in a future release.")]
        public static void CompressData(byte[] inData, out byte[] outData, ZlibCompression level)
            => Compress(inData, out outData, level);

        /// <summary>
        /// Compresses data using an specific compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <param name="adler32">The output adler32 of the data.</param>
        /// <exception cref="NotPackableException">Thrown when the stream Errors in any way.</exception>
        [Obsolete("Use MemoryZlib.Compress(byte[], out byte[], ZlibCompression, out int) instead. This will be removed in a future release.")]
        public static void CompressData(byte[] inData, out byte[] outData, ZlibCompression level, out uint adler32)
            => Compress(inData, out outData, level, out adler32);

        /// <summary>
        /// Decompresses data.
        /// </summary>
        /// <param name="inData">The compressed input data.</param>
        /// <param name="outData">The decompressed output data.</param>
        /// <exception cref="NotUnpackableException">Thrown when the stream Errors in any way.</exception>
        [Obsolete("Use MemoryZlib.Decompress(byte[], out byte[]) instead. This will be removed in a future release.")]
        public static void DecompressData(byte[] inData, out byte[] outData)
            => Decompress(inData, out outData);

        // NEW: Now there are shortcut methods for compressing a file using the fully qualified path.
        // NEW: Now can compress and decompress with stream outputs instead of byte arrays too.

        /// <summary>
        /// Compresses data using the default compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <param name="adler32">The output adler32 of the data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        public static void Compress(byte[] inData, out byte[] outData, out uint adler32)
            => Compress(inData, out outData, ZlibCompression.ZDEFAULTCOMPRESSION, out adler32);

        /// <summary>
        /// Compresses a file using the default compression level.
        /// </summary>
        /// <param name="path">The file to compress.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <param name="adler32">The output adler32 of the data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        public static void Compress(string path, out byte[] outData, out uint adler32)
            => Compress(File.ReadAllBytes(path), out outData, ZlibCompression.ZDEFAULTCOMPRESSION, out adler32);

        /// <summary>
        /// Compresses data using the default compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        public static void Compress(byte[] inData, out byte[] outData)
            => Compress(inData, out outData, ZlibCompression.ZDEFAULTCOMPRESSION);

        /// <summary>
        /// Compresses a file using the default compression level.
        /// </summary>
        /// <param name="path">The file to compress.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        public static void Compress(string path, out byte[] outData)
            => Compress(File.ReadAllBytes(path), out outData, ZlibCompression.ZDEFAULTCOMPRESSION);

        /// <summary>
        /// Compresses data using an specific compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        // discard returned adler32. The caller does not want it.
        public static void Compress(byte[] inData, out byte[] outData, ZlibCompression level)
            => Compress(inData, out outData, level, out var _);

        /// <summary>
        /// Compresses a file using the default compression level.
        /// </summary>
        /// <param name="path">The file to compress.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        // discard returned adler32. The caller does not want it.
        public static void Compress(string path, out byte[] outData, ZlibCompression level)
            => Compress(File.ReadAllBytes(path), out outData, level, out var _);

        /// <summary>
        /// Compresses data using an specific compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outStream">The compressed output data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <param name="adler32">The output adler32 of the data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        public static void Compress(byte[] inData, Stream outStream, ZlibCompression level, out uint adler32)
        {
            try
            {
                using var tmpStrm = new MemoryStream(inData);
                using var outZStream = new ZOutputStream(outStream, level, true);
                try
                {
                    tmpStrm.CopyTo(outZStream);
                }
                catch (NotPackableException ex)
                {
                    // the compression or decompression failed.
                    throw new NotPackableException("Compression Failed.", ex);
                }

                try
                {
                    outZStream.Flush();
                }
                catch (StackOverflowException ex)
                {
                    throw new NotPackableException("Compression Failed due to a stack overflow.", ex);
                }

                try
                {
                    outZStream.Finish();
                }
                catch (NotPackableException ex)
                {
                    throw new NotPackableException("Compression Failed.", ex);
                }

                var contents = tmpStrm.ToArray();
                adler32 = (uint)(ZlibGetAdler32(contents, 0, contents.Length) & 0xffff);
            }
            catch (IOException ex)
            {
                throw new NotPackableException("Compression Failed.", ex);
            }
        }

        /// <summary>
        /// Compresses data using an specific compression level.
        /// </summary>
        /// <param name="inData">The original input data.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <param name="adler32">The output adler32 of the data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        public static void Compress(byte[] inData, out byte[] outData, ZlibCompression level, out uint adler32)
        {
            using var outMemoryStream = new MemoryStream();
            Compress(inData, outMemoryStream, level, out adler32);
            outData = outMemoryStream.ToArray();
        }

        /// <summary>
        /// Compresses a file using an specific compression level.
        /// </summary>
        /// <param name="path">The file to compress.</param>
        /// <param name="outData">The compressed output data.</param>
        /// <param name="level">The compression level to use.</param>
        /// <param name="adler32">The output adler32 of the data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal compression stream errors in any way.
        /// </exception>
        public static void Compress(string path, out byte[] outData, ZlibCompression level, out uint adler32)
            => Compress(File.ReadAllBytes(path), out outData, level, out adler32);

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
                using var tmpStrm = new MemoryStream(inData);
                using var outZStream = new ZOutputStream(outStream, true);
                try
                {
                    tmpStrm.CopyTo(outZStream);
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
            }
            catch (IOException ex)
            {
                throw new NotUnpackableException("Decompression Failed.", ex);
            }
        }

        /// <summary>
        /// Decompresses data.
        /// </summary>
        /// <param name="inData">The compressed input data.</param>
        /// <param name="outData">The decompressed output data.</param>
        /// <exception cref="NotUnpackableException">
        /// Thrown when the internal decompression stream errors in any way.
        /// </exception>
        public static void Decompress(byte[] inData, out byte[] outData)
        {
            using var outMemoryStream = new MemoryStream();
            Decompress(inData, outMemoryStream);
            outData = outMemoryStream.ToArray();
        }

        /// <summary>
        /// Decompresses a file.
        /// </summary>
        /// <param name="path">The file to decompress.</param>
        /// <param name="outData">The decompressed output data.</param>
        /// <exception cref="NotPackableException">
        /// Thrown when the internal decompression stream errors in any way.
        /// </exception>
        public static void Decompress(string path, out byte[] outData)
            => Decompress(File.ReadAllBytes(path), out outData);

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
