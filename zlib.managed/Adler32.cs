// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    internal static class Adler32
    {
        internal static long Calculate(long adler, byte[] buf, int index, int len)
        {
            if (buf is null)
            {
                return 1L;
            }

            var s1 = adler & 0xffff;
            var s2 = (adler >> 16) & 0xffff;
            while (len > 0)
            {
                // 5552 is the largest n such that 255n(n+1)/2 + (n+1)(BASE-1) <= 2^32-1
                var k = len < 5552 ? len : 5552;
                len -= k;
                while (k >= 16)
                {
                    for (var i = 0; i < 16; i++)
                    {
                        s1 += buf[index++] & 0xff;
                        s2 += s1;
                    }

                    k -= 16;
                }

                if (k != 0)
                {
                    do
                    {
                        s1 += buf[index++] & 0xff;
                        s2 += s1;
                    }
                    while (--k != 0);
                }

                // largest prime smaller than 65536.
                s1 %= 65521;
                s2 %= 65521;
            }

            return (s2 << 16) | s1;
        }
    }
}
