// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    /// <summary>
    /// Compression levels for zlib.
    /// </summary>
    public enum ZlibCompression
    {
        /// <summary>
        /// The default compression level.
        /// </summary>
        DefaultCompression = -1,

        /// <summary>
        /// No compression.
        /// </summary>
        NoCompression,

        /// <summary>
        /// best speed compression level.
        /// </summary>
        BestSpeed,

        /// <summary>
        /// Compression level 2.
        /// </summary>
        Level2,

        /// <summary>
        /// Compression level 3.
        /// </summary>
        Level3,

        /// <summary>
        /// Compression level 4.
        /// </summary>
        Level4,

        /// <summary>
        /// Compression level 5.
        /// </summary>
        Level5,

        /// <summary>
        /// Compression level 6.
        /// </summary>
        Level6,

        /// <summary>
        /// Compression level 7.
        /// </summary>
        Level7,

        /// <summary>
        /// Compression level 8.
        /// </summary>
        Level8,

        /// <summary>
        /// Compression level 9.
        /// </summary>
        Level9,

        /// <summary>
        /// the best compression level.
        /// </summary>
        BestCompression = Level9,
    }
}
