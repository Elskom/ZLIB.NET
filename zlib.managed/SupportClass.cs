// Copyright (c) 2018-2021, Els_kom org.
// https://github.com/Elskom/
// All rights reserved.
// license: see LICENSE for more details.

namespace Elskom.Generic.Libs
{
    using System;
    using System.IO;
    using System.Text;

    internal static class SupportClass
    {
        internal static long Identity(long literal)
            => literal;

        /*******************************/

        internal static int URShift(int number, int bits)
            => number >= 0 ? number >> bits : (number >> bits) + (2 << ~bits);

        internal static long URShift(long number, int bits)
            => number >= 0 ? number >> bits : (number >> bits) + (2L << ~bits);

        /*******************************/

        internal static int ReadInput(Stream sourceStream, byte[] target, int start, int count)
        {
            if (sourceStream == null)
            {
                throw new ArgumentNullException(nameof(sourceStream));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            // Returns 0 bytes if not enough space in target
            if (target.Length == 0)
            {
                return 0;
            }

            var receiver = new byte[target.Length];
            var bytesRead = sourceStream.Read(receiver, start, count);

            // Returns -1 if EOF
            if (bytesRead == 0)
            {
                return -1;
            }

            for (var i = start; i < start + bytesRead; i++)
            {
                target[i] = receiver[i];
            }

            return bytesRead;
        }
    }
}
