// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    internal enum ZlibCompressionState
    {
        ZVERSIONERROR = -6,
        ZBUFERROR,
        ZMEMERROR,
        ZDATAERROR,
        ZSTREAMERROR,
        ZERRNO,
        ZOK,
        ZSTREAMEND,
        ZNEEDDICT,
    }
}
